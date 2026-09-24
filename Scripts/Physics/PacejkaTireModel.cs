using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Koefisien Pacejka Magic Formula untuk ban.
    /// Nilai-nilai ini biasanya didapat dari data tire testing atau fitting.
    /// </summary>
    [Serializable]
    public struct TireCoefficients
    {
        // Koefisien untuk gaya lateral (Fy)
        public float pCy1;      // Shape factor Cfy
        public float pDy1, pDy2, pDy3;  // Peak value Dfy
        public float pEy1, pEy2, pEy3, pEy4;  // Curvature factor Efy
        public float pKy1, pKy2, pKy3;  // Stiffness factor Kfy
        public float pHy1, pHy2, pHy3;  // Horizontal shift Hy
        public float pVy1, pVy2;  // Vertical shift Vy
        
        // Koefisien untuk gaya longitudinal (Fx)
        public float pCx1;      // Shape factor Cfx
        public float pDx1, pDx2, pDx3;  // Peak value Dfx
        public float pEx1, pEx2, pEx3, pEx4;  // Curvature factor Efx
        public float pKx1, pKx2, pKx3;  // Stiffness factor Kfx
        public float pHx1, pHx2;  // Horizontal shift Hx
        public float pVx1, pVx2;  // Vertical shift Vx
        
        // Koefisien untuk aligning torque (Mz)
        public float qBz1, qBz2, qBz3, qBz4, qBz5;
        public float qCz1, qCz2;
        public float qDz1, qDz2, qDz3;
        public float qEz1, qEz2, qEz3, qEz4;
        
        // Koefisien untuk combined slip
        public float rBx1, rBx2;  // Gx weighting
        public float rCx1;
        public float rBy1, rBy2;  // Gy weighting
        public float rCy1;
        public float rHx1;  // Cosine weighting
        
        // Koefisien camber thrust
        public float qVz1, qVz2, qVz3, qVz4;
        public float qCz3;
        
        // Koefisien thermal
        public float muBase;  // Grip base
        public float tempOptimal;  // Suhu optimal (°C)
        public float tempRange;  // Range suhu optimal
        
        // Beban nominal untuk normalisasi
        public float fzNominal;  // Beban vertikal nominal (N)
    }

    /// <summary>
    /// Hasil gabungan gaya ban (Burst-safe, menggantikan ValueTuple).
    /// </summary>
    public struct TireForceResult
    {
        public float Fx;
        public float Fy;
        public float Mz;
    }

    /// <summary>
    /// Implementasi Pacejka Magic Formula untuk simulasi ban.
    /// Menggunakan koefisien load-dependent untuk akurasi tinggi.
    /// Optimized dengan Burst Compiler untuk performa.
    /// </summary>
    [BurstCompile]
    public static class PacejkaTireModel
    {
        /// <summary>
        /// Hitung gaya lateral murni (pure lateral force) menggunakan Pacejka Magic Formula.
        /// </summary>
        /// <param name="slipAngle">Slip angle (radian)</param>
        /// <param name="fz">Beban vertikal (Newton), harus >= 0</param>
        /// <param name="camber">Camber angle (radian)</param>
        /// <param name="coeff">Koefisien Pacejka</param>
        /// <returns>Gaya lateral Fy (Newton)</returns>
        [BurstCompile]
        public static float CalculateLateralForce(float slipAngle, float fz, float camber, in TireCoefficients coeff)
        {
            if (fz <= 0f) return 0f;
            
            // Normalisasi beban
            float dfz = (fz - coeff.fzNominal) / coeff.fzNominal;
            dfz = math.clamp(dfz, -0.5f, 0.5f);  // Clamp untuk stabilitas
            
            // Shape factor C (bentuk kurva)
            float c = coeff.pCy1;
            c = math.clamp(c, 1.0f, 2.5f);
            
            // Peak value D (gaya maksimum)
            // D = Fz * (pDy1 + pDy2*dfz) * (1 - pDy3*camber²)
            float d = fz * (coeff.pDy1 + coeff.pDy2 * dfz) * (1.0f - coeff.pDy3 * camber * camber);
            
            // Curvature factor E (kelengkungan kurva)
            // E = pEy1 + pEy2*dfz + pEy3*dfz² + pEy4*sign(slipAngle)
            float e = coeff.pEy1 + coeff.pEy2 * dfz + coeff.pEy3 * dfz * dfz;
            e += coeff.pEy4 * math.sign(slipAngle);
            e = math.clamp(e, 0.0f, 1.0f);  // E harus <= 1 untuk stabilitas
            
            // Stiffness factor BCD (slope di titik asal)
            // BCD = Fz * (pKy1 + pKy2*dfz + pKy3*dfz²)
            float bcd = fz * (coeff.pKy1 + coeff.pKy2 * dfz + coeff.pKy3 * dfz * dfz);
            
            // Stiffness factor B
            float b = bcd / (c * d + 1e-6f);  // +epsilon untuk hindagi division by zero
            
            // Horizontal shift H
            float h = coeff.pHy1 + coeff.pHy2 * dfz + coeff.pHy3 * camber;
            
            // Vertical shift V
            float v = fz * (coeff.pVy1 + coeff.pVy2 * dfz);
            
            // Slip angle dengan shift
            float alpha = slipAngle + h;
            
            // Pacejka Magic Formula:
            // Y = D * sin(C * arctan(B*α - E*(B*α - arctan(B*α))))
            float bx_alpha = b * alpha;
            float atan_bx_alpha = math.atan(bx_alpha);
            float term = bx_alpha - e * (bx_alpha - atan_bx_alpha);
            
            float fy = d * math.sin(c * math.atan(term)) + v;
            
            return fy;
        }
        
        /// <summary>
        /// Hitung gaya longitudinal murni (pure longitudinal force) menggunakan Pacejka Magic Formula.
        /// </summary>
        /// <param name="slipRatio">Slip ratio κ (-1 sampai 1)</param>
        /// <param name="fz">Beban vertikal (Newton), harus >= 0</param>
        /// <param name="coeff">Koefisien Pacejka</param>
        /// <returns>Gaya longitudinal Fx (Newton)</returns>
        [BurstCompile]
        public static float CalculateLongitudinalForce(float slipRatio, float fz, in TireCoefficients coeff)
        {
            if (fz <= 0f) return 0f;
            
            // Normalisasi beban
            float dfz = (fz - coeff.fzNominal) / coeff.fzNominal;
            dfz = math.clamp(dfz, -0.5f, 0.5f);
            
            // Shape factor C
            float c = coeff.pCx1;
            c = math.clamp(c, 1.0f, 2.0f);
            
            // Peak value D
            float d = fz * (coeff.pDx1 + coeff.pDx2 * dfz) * (1.0f - coeff.pDx3 * dfz * dfz);
            
            // Curvature factor E
            float e = coeff.pEx1 + coeff.pEx2 * dfz + coeff.pEx3 * dfz * dfz;
            e = math.clamp(e, 0.0f, 1.0f);
            
            // Stiffness factor BCD
            float bcd = fz * (coeff.pKx1 + coeff.pKx2 * dfz + coeff.pKx3 * dfz * dfz);
            
            // Stiffness factor B
            float b = bcd / (c * d + 1e-6f);
            
            // Horizontal shift H
            float h = coeff.pHx1 + coeff.pHx2 * dfz;
            
            // Vertical shift V
            float v = fz * (coeff.pVx1 + coeff.pVx2 * dfz);
            
            // Slip ratio dengan shift
            float kappa = slipRatio + h;
            
            // Pacejka Magic Formula
            float bx_kappa = b * kappa;
            float atan_bx_kappa = math.atan(bx_kappa);
            float term = bx_kappa - e * (bx_kappa - atan_bx_kappa);
            
            float fx = d * math.sin(c * math.atan(term)) + v;
            
            return fx;
        }
        
        /// <summary>
        /// Hitung gaya gabungan (combined slip) dengan weighting function.
        /// Menghitung Fx, Fy, dan Mz secara bersamaan.
        /// </summary>
        /// <param name="slipAngle">Slip angle (radian)</param>
        /// <param name="slipRatio">Slip ratio κ</param>
        /// <param name="fz">Beban vertikal (Newton)</param>
        /// <param name="camber">Camber angle (radian)</param>
        /// <param name="gripMultiplier">Grip multiplier (thermal, wear)</param>
        /// <param name="coeff">Koefisien Pacejka</param>
        /// <returns>Tuple (Fx, Fy, Mz) dalam Newton dan Nm</returns>
        [BurstCompile]
        public static TireForceResult CalculateCombinedForces(
            float slipAngle, 
            float slipRatio, 
            float fz, 
            float camber,
            float gripMultiplier,
            in TireCoefficients coeff)
        {
            if (fz <= 0f) return new TireForceResult();
            
            // 1. Hitung pure forces terlebih dahulu
            float fyPure = CalculateLateralForce(slipAngle, fz, camber, coeff);
            float fxPure = CalculateLongitudinalForce(slipRatio, fz, coeff);
            
            // 2. Hitung combined slip magnitude
            // σ = sqrt(κ² + tan²(α))
            float sigma = math.sqrt(slipRatio * slipRatio + math.tan(slipAngle) * math.tan(slipAngle));
            
            // 3. Hitung weighting functions Gx dan Gy
            // Gx = cos(Cx * arctan(Bx * σ)) / cos(Cx * arctan(Bx * κ))
            float cx = coeff.rCx1;
            float bx = coeff.rBx1 + coeff.rBx2 * (fz / coeff.fzNominal - 1f);
            
            float numeratorX = math.cos(cx * math.atan(bx * sigma));
            float denominatorX = math.cos(cx * math.atan(bx * math.abs(slipRatio))) + 1e-6f;
            float gx = numeratorX / denominatorX;
            gx = math.clamp(gx, 0f, 1.5f);
            
            // Gy = cos(Cy * arctan(By * σ)) / cos(Cy * arctan(By * α))
            float cy = coeff.rCy1;
            float by = coeff.rBy1 + coeff.rBy2 * (fz / coeff.fzNominal - 1f);
            
            float numeratorY = math.cos(cy * math.atan(by * sigma));
            float denominatorY = math.cos(cy * math.atan(by * math.abs(slipAngle))) + 1e-6f;
            float gy = numeratorY / denominatorY;
            gy = math.clamp(gy, 0f, 1.5f);
            
            // 4. Apply weighting ke pure forces
            float fx = gx * fxPure;
            float fy = gy * fyPure;
            
            // 5. Tambahkan camber thrust
            // Fvy = Dvy * sin(Cvy * arctan(By * γ))
            float dvy = fz * (coeff.qVz1 + coeff.qVz2 * (fz / coeff.fzNominal - 1f));
            float cvy = coeff.qCz3;
            float fvy = dvy * math.sin(cvy * math.atan(by * camber));
            fy += fvy;
            
            // 6. Hitung aligning torque Mz
            // Pneumatic trail: t = t0 * cos(Ct * arctan(Bt * α))
            float qcz1 = coeff.qCz1;
            float qcz2 = coeff.qCz2;
            float ct = qcz1;
            float bt = coeff.qBz1 + coeff.qBz2 * (fz / coeff.fzNominal - 1f);
            
            float pneumaticTrail = coeff.qDz1 * fz * math.cos(ct * math.atan(bt * math.abs(slipAngle)));
            
            // Residual torque
            float dzRes = fz * (coeff.qDz2 + coeff.qDz3 * (fz / coeff.fzNominal - 1f));
            float ezRes = coeff.qEz1 + coeff.qEz2 * (fz / coeff.fzNominal - 1f);
            ezRes = math.clamp(ezRes, 0f, 1f);
            
            float bzRes = coeff.qBz4;
            float czRes = coeff.qBz5;
            float psi = math.atan(bzRes * math.tan(czRes * slipAngle));
            float residualTorque = dzRes * math.cos(ct * math.atan(psi)) * math.sign(slipAngle);
            
            // Mz = -Fy * pneumaticTrail + residualTorque
            float mz = -fy * pneumaticTrail + residualTorque;

            return new TireForceResult
            {
                Fx = fx * gripMultiplier,
                Fy = fy * gripMultiplier,
                Mz = mz * gripMultiplier
            };
        }

        [BurstCompile]
        public static TireForceResult CalculateCombinedForces(
            float slipAngle,
            float slipRatio,
            float fz,
            float camber,
            float gripMultiplier,
            in RacingSim.Vehicle.TireCoefficientsData data)
        {
            var coeff = ToTireCoefficients(data);
            return CalculateCombinedForces(slipAngle, slipRatio, fz, camber, gripMultiplier, in coeff);
        }

        private static TireCoefficients ToTireCoefficients(in RacingSim.Vehicle.TireCoefficientsData data)
        {
            return new TireCoefficients
            {
                pCy1 = data.pCy1,
                pDy1 = data.pDy1,
                pDy2 = data.pDy2,
                pEy1 = data.pEy1,
                pEy2 = data.pEy2,
                pEy3 = data.pEy3,
                pKy1 = data.pKy1,
                pKy2 = data.pKy2,
                pKy3 = data.pKy3,
                pCx1 = data.pCx1,
                pDx1 = data.pDx1,
                pDx2 = data.pDx2,
                pEx1 = data.pEx1,
                pEx2 = data.pEx2,
                pEx3 = data.pEx3,
                pKx1 = data.pKx1,
                pKx2 = data.pKx2,
                rBx1 = data.rBx1,
                rBx2 = data.rBx2,
                rBy1 = data.rBy1,
                rBy2 = data.rBy2,
                rCx1 = data.rCx1,
                rCy1 = data.rCy1,
                qBz1 = data.qBz1,
                qBz2 = data.qBz2,
                qCz1 = data.qCz1,
                qDz1 = data.qDz1,
                qDz2 = data.qDz2,
                qEz1 = data.qEz1,
                qEz2 = data.qEz2,
                fzNominal = 4000f,
                tempOptimal = 100f,
                tempRange = 35f,
                muBase = 1.8f
            };
        }
        
        /// <summary>
        /// Hitung multiplier grip berdasarkan suhu ban.
        /// Grip optimal pada suhu tertentu, menurun jika terlalu dingin atau panas.
        /// </summary>
        /// <param name="tireTemp">Suhu ban saat ini (°C)</param>
        /// <param name="coeff">Koefisien Pacejka dengan info suhu optimal</param>
        /// <returns>Multiplier grip (0.5 - 1.2)</returns>
        [BurstCompile]
        public static float CalculateTemperatureMultiplier(float tireTemp, in TireCoefficients coeff)
        {
            float tempDiff = tireTemp - coeff.tempOptimal;
            float normalizedDiff = tempDiff / coeff.tempRange;
            
            // Gaussian curve: μ = μ_base * exp(-(T-T_opt)² / T_range²)
            float multiplier = math.exp(-normalizedDiff * normalizedDiff);
            
            // Clamp untuk stabilitas
            multiplier = math.clamp(multiplier, 0.5f, 1.2f);
            
            return multiplier;
        }
        
        /// <summary>
        /// Hitung multiplier grip berdasarkan keausan ban.
        /// Ban baru memiliki grip lebih tinggi, ban aus grip berkurang.
        /// </summary>
        /// <param name="wear">Keausan ban (0 = baru, 1 = habis)</param>
        /// <returns>Multiplier grip (0.6 - 1.0)</returns>
        [BurstCompile]
        public static float CalculateWearMultiplier(float wear)
        {
            wear = math.clamp(wear, 0f, 1f);
            
            // Linear degradation dengan slight curve
            float multiplier = 1.0f - 0.4f * wear * wear;
            
            return math.clamp(multiplier, 0.6f, 1.0f);
        }
    }
}
