using UnityEngine;
using NUnit.Framework;
using Unity.Mathematics;
using RacingSim.Physics;

namespace RacingSim.Tests
{
    /// <summary>
    /// Unit tests untuk Pacejka Tire Model.
    /// Memvalidasi bahwa implementasi Pacejka menghasilkan nilai yang fisikal.
    /// </summary>
    public class PacejkaTireModelTests
    {
        private TireCoefficients defaultCoeffs;
        
        [SetUp]
        public void Setup()
        {
            // Koefisien contoh untuk ban racing slick
            // Nilai-nilai ini harus di-fit dari data tire testing nyata
            defaultCoeffs = new TireCoefficients
            {
                // Lateral coefficients
                pCy1 = 1.5f,
                pDy1 = 1.6f,
                pDy2 = -0.1f,
                pDy3 = 0.02f,
                pEy1 = 0.9f,
                pEy2 = 0.0f,
                pEy3 = 0.0f,
                pEy4 = 0.0f,
                pKy1 = 18.0f,
                pKy2 = 2.0f,
                pKy3 = 0.0f,
                pHy1 = 0.0f,
                pHy2 = 0.0f,
                pHy3 = 0.5f,
                pVy1 = 0.0f,
                pVy2 = 0.0f,
                
                // Longitudinal coefficients
                pCx1 = 1.65f,
                pDx1 = 1.7f,
                pDx2 = -0.05f,
                pDx3 = 0.0f,
                pEx1 = 0.85f,
                pEx2 = 0.0f,
                pEx3 = 0.0f,
                pKx1 = 25.0f,
                pKx2 = 3.0f,
                pKx3 = 0.0f,
                pHx1 = 0.0f,
                pHx2 = 0.0f,
                pVx1 = 0.0f,
                pVx2 = 0.0f,
                
                // Aligning torque coefficients
                qBz1 = 9.0f,
                qBz2 = -2.0f,
                qBz3 = 0.0f,
                qBz4 = 0.5f,
                qBz5 = 1.0f,
                qCz1 = 1.0f,
                qCz2 = 0.0f,
                qDz1 = 0.02f,
                qDz2 = 0.0f,
                qDz3 = 0.0f,
                qEz1 = 0.5f,
                qEz2 = 0.0f,
                qEz3 = 0.0f,
                qEz4 = 0.0f,
                
                // Combined slip coefficients
                rBx1 = 8.0f,
                rBx2 = -1.0f,
                rCx1 = 1.3f,
                rBy1 = 8.0f,
                rBy2 = -1.0f,
                rCy1 = 1.3f,
                rHx1 = 0.0f,
                
                // Camber thrust coefficients
                qVz1 = 0.05f,
                qVz2 = -0.01f,
                qVz3 = 0.0f,
                qVz4 = 0.0f,
                qCz3 = 1.0f,
                
                // Thermal coefficients
                muBase = 1.5f,
                tempOptimal = 100f,
                tempRange = 35f,
                
                // Nominal load
                fzNominal = 4000f  // 4000 N (~400 kg per roda)
            };
        }
        
        [Test]
        public void TestLateralForce_ZeroSlipAngle_ReturnsZero()
        {
            // Arrange
            float slipAngle = 0f;
            float fz = 4000f;
            float camber = 0f;
            
            // Act
            float fy = PacejkaTireModel.CalculateLateralForce(slipAngle, fz, camber, defaultCoeffs);
            
            // Assert
            Assert.AreEqual(0f, fy, 10f);  // Tolerance 10N untuk numerical error
        }
        
        [Test]
        public void TestLateralForce_IncreasesWithSlipAngle()
        {
            // Arrange
            float fz = 4000f;
            float camber = 0f;
            
            // Act
            float fy1 = PacejkaTireModel.CalculateLateralForce(math.radians(2f), fz, camber, defaultCoeffs);
            float fy2 = PacejkaTireModel.CalculateLateralForce(math.radians(4f), fz, camber, defaultCoeffs);
            float fy3 = PacejkaTireModel.CalculateLateralForce(math.radians(8f), fz, camber, defaultCoeffs);
            
            // Assert
            Assert.Greater(fy2, fy1, "Gaya lateral harus meningkat dengan slip angle");
            Assert.Greater(fy3, fy2, "Gaya lateral harus meningkat dengan slip angle");
        }
        
        [Test]
        public void TestLateralForce_IncreasesWithLoad()
        {
            // Arrange
            float slipAngle = math.radians(5f);
            float camber = 0f;
            
            // Act
            float fy1 = PacejkaTireModel.CalculateLateralForce(slipAngle, 2000f, camber, defaultCoeffs);
            float fy2 = PacejkaTireModel.CalculateLateralForce(slipAngle, 4000f, camber, defaultCoeffs);
            float fy3 = PacejkaTireModel.CalculateLateralForce(slipAngle, 6000f, camber, defaultCoeffs);
            
            // Assert
            Assert.Greater(fy2, fy1, "Gaya lateral harus meningkat dengan beban");
            Assert.Greater(fy3, fy2, "Gaya lateral harus meningkat dengan beban");
        }
        
        [Test]
        public void TestLateralForce_CamberAddsForce()
        {
            // Arrange
            float slipAngle = math.radians(3f);
            float fz = 4000f;
            
            // Act
            float fyNoCamber = PacejkaTireModel.CalculateLateralForce(slipAngle, fz, 0f, defaultCoeffs);
            float fyWithCamber = PacejkaTireModel.CalculateLateralForce(slipAngle, fz, math.radians(-3f), defaultCoeffs);
            
            // Assert
            Assert.Greater(fyWithCamber, fyNoCamber, "Negative camber harus menambah gaya lateral");
        }
        
        [Test]
        public void TestLongitudinalForce_ZeroSlipRatio_ReturnsZero()
        {
            // Arrange
            float slipRatio = 0f;
            float fz = 4000f;
            
            // Act
            float fx = PacejkaTireModel.CalculateLongitudinalForce(slipRatio, fz, defaultCoeffs);
            
            // Assert
            Assert.AreEqual(0f, fx, 10f);
        }
        
        [Test]
        public void TestLongitudinalForce_BrakingVsAccelerating()
        {
            // Arrange
            float fz = 4000f;
            
            // Act
            float fxBraking = PacejkaTireModel.CalculateLongitudinalForce(-0.1f, fz, defaultCoeffs);
            float fxAccelerating = PacejkaTireModel.CalculateLongitudinalForce(0.1f, fz, defaultCoeffs);
            
            // Assert
            Assert.Less(fxBraking, 0f, "Gaya braking harus negatif");
            Assert.Greater(fxAccelerating, 0f, "Gaya accelerating harus positif");
        }
        
        [Test]
        public void TestCombinedForces_ReducedUnderCombinedSlip()
        {
            // Arrange
            float slipAngle = math.radians(5f);
            float slipRatioPure = 0f;
            float slipRatioCombined = 0.1f;
            float fz = 4000f;
            float camber = 0f;
            
            // Act
            var forcesPure = PacejkaTireModel.CalculateCombinedForces(slipAngle, slipRatioPure, fz, camber, 1f, defaultCoeffs);
            var forcesCombined = PacejkaTireModel.CalculateCombinedForces(slipAngle, slipRatioCombined, fz, camber, 1f, defaultCoeffs);
            
            // Assert
            Assert.Less(math.abs(forcesCombined.Fy), math.abs(forcesPure.Fy), 
                "Gaya lateral harus berkurang saat ada slip ratio (friction circle)");
        }
        
        [Test]
        public void TestTemperatureMultiplier_OptimalTemp_ReturnsOne()
        {
            // Arrange
            float optimalTemp = defaultCoeffs.tempOptimal;
            
            // Act
            float multiplier = PacejkaTireModel.CalculateTemperatureMultiplier(optimalTemp, defaultCoeffs);
            
            // Assert
            Assert.AreEqual(1.0f, multiplier, 0.01f);
        }
        
        [Test]
        public void TestTemperatureMultiplier_ColdOrHot_ReducedGrip()
        {
            // Arrange
            float coldTemp = 40f;
            float hotTemp = 160f;
            
            // Act
            float multiplierCold = PacejkaTireModel.CalculateTemperatureMultiplier(coldTemp, defaultCoeffs);
            float multiplierHot = PacejkaTireModel.CalculateTemperatureMultiplier(hotTemp, defaultCoeffs);
            
            // Assert
            Assert.Less(multiplierCold, 1.0f, "Grip berkurang saat ban dingin");
            Assert.Less(multiplierHot, 1.0f, "Grip berkurang saat ban terlalu panas");
        }
        
        [Test]
        public void TestWearMultiplier_NewTire_FullGrip()
        {
            // Act
            float multiplier = PacejkaTireModel.CalculateWearMultiplier(0f);
            
            // Assert
            Assert.AreEqual(1.0f, multiplier, 0.01f);
        }
        
        [Test]
        public void TestWearMultiplier_WornTire_ReducedGrip()
        {
            // Act
            float multiplier = PacejkaTireModel.CalculateWearMultiplier(1f);
            
            // Assert
            Assert.Less(multiplier, 1.0f, "Grip berkurang saat ban aus");
            Assert.Greater(multiplier, 0.5f, "Grip minimal 60% bahkan saat ban habis");
        }
        
        [Test]
        public void TestZeroLoad_ReturnsZeroForces()
        {
            // Arrange
            float zeroLoad = 0f;
            float slipAngle = math.radians(5f);
            float slipRatio = 0.1f;
            float camber = 0f;
            
            // Act
            float fy = PacejkaTireModel.CalculateLateralForce(slipAngle, zeroLoad, camber, defaultCoeffs);
            float fx = PacejkaTireModel.CalculateLongitudinalForce(slipRatio, zeroLoad, defaultCoeffs);
            var combined = PacejkaTireModel.CalculateCombinedForces(slipAngle, slipRatio, zeroLoad, camber, 1f, defaultCoeffs);
            
            // Assert
            Assert.AreEqual(0f, fy);
            Assert.AreEqual(0f, fx);
            Assert.AreEqual(0f, combined.Fx);
            Assert.AreEqual(0f, combined.Fy);
            Assert.AreEqual(0f, combined.Mz);
        }
        
        [Test]
        public void TestAligningTorque_SignCorrect()
        {
            // Arrange
            float slipAnglePositive = math.radians(5f);
            float slipAngleNegative = math.radians(-5f);
            float fz = 4000f;
            float camber = 0f;
            float slipRatio = 0f;
            
            // Act
            var mzPositive = PacejkaTireModel.CalculateCombinedForces(slipAnglePositive, slipRatio, fz, camber, 1f, defaultCoeffs);
            var mzNegative = PacejkaTireModel.CalculateCombinedForces(slipAngleNegative, slipRatio, fz, camber, 1f, defaultCoeffs);
            
            // Assert
            Assert.Less(mzPositive.Mz, 0f, "Aligning torque harus negatif untuk positive slip angle (self-aligning)");
            Assert.Greater(mzNegative.Mz, 0f, "Aligning torque harus positif untuk negative slip angle");
        }
    }
}
