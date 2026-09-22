using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// State untuk satu roda dalam simulasi.
    /// Menyimpan semua data kinematika, gaya, dan termal ban.
    /// </summary>
    [System.Obsolete("Pending wire to TireModel. Use SuspensionState for suspension-only data.")]
    [BurstCompile]
    public struct WheelState
    {
        // Identitas roda
        public int wheelIndex;  // 0=FL, 1=FR, 2=RL, 3=RR
        
        // Kinematika (posisi dalam world space)
        public float3 hubPosition;          // Posisi pusat roda
        public float3 contactPatch;         // Titik kontak dengan aspal
        public float rideHeight;            // Jarak hub ke ground
        
        // Sudut-sudut (radian)
        public float camberAngle;           // Camber
        public float toeAngle;              // Toe
        public float casterAngle;           // Caster
        public float steeringAngle;         // Steering angle aktual
        
        // Kecepatan (m/s)
        public float3 hubVelocity;          // Kecepatan hub
        public float3 contactPointVelocity; // Kecepatan titik kontak
        
        // Beban dan Gaya (Newton)
        public float verticalLoad;          // Fz - beban vertikal
        public float longitudinalForce;     // Fx - gaya drive/brake
        public float lateralForce;          // Fy - gaya cornering
        public float aligningTorque;        // Mz - torsi aligning
        
        // Slip
        public float slipRatio;             // κ (kappa)
        public float slipAngle;             // α (alpha)
        public float slipVelocity;          // Magnitude slip velocity
        
        // Termal (°C)
        public float tireTempInner;
        public float tireTempMiddle;
        public float tireTempOuter;
        public float brakeTemp;
        
        // Kondisi ban
        public float tirePressure;          // kPa
        public float tireWear;              // 0-1 (0=new, 1=bald)
        public bool tirePunctured;          // true jika ban pecah
        
        // Rotasi
        public float angularVelocity;       // rad/s (rotasi ban)
        public float rotationAngle;         // rad (untuk visual)
        
        // Suspension
        public float suspensionTravel;      // m (defleksi saat ini)
        public float suspensionVelocity;    // m/s (kecepatan defleksi)
        public float springForce;           // N
        public float damperForce;           // N
        
        // Brake
        public float brakePressure;         // Pa
        public float brakeTorque;           // Nm
        
        // Status
        public bool isGrounded;             // true jika kontak dengan ground
        public float groundHeight;          // Tinggi ground di bawah roda
    }
    
    /// <summary>
    /// Sistem suspensi dengan model spring-damper dan kinematika.
    /// Menghitung gaya suspensi berdasarkan travel dan velocity.
    /// </summary>
    [BurstCompile]
    public static class SuspensionSystem
    {
        /// <summary>
        /// Hitung gaya suspensi total (spring + damper + bump stop).
        /// </summary>
        /// <param name="travel">Defleksi suspensi (m), positif = compression</param>
        /// <param name="velocity">Kecepatan defleksi (m/s), positif = compression</param>
        /// <param name="springRate">Spring rate (N/m)</param>
        /// <param name="dampingCompression">Compression damping (N·s/m)</param>
        /// <param name="dampingRebound">Rebound damping (N·s/m)</param>
        /// <param name="bumpStopTravel">Travel saat bump stop mulai (m)</param>
        /// <param name="bumpStopRate">Bump stop spring rate (N/m)</param>
        /// <returns>Gaya suspensi total (N), positif = menekan ke atas</returns>
        [BurstCompile]
        public static float CalculateSuspensionForce(
            float travel,
            float velocity,
            float springRate,
            float dampingCompression,
            float dampingRebound,
            float bumpStopTravel,
            float bumpStopRate)
        {
            if (travel <= 0f) return 0f;
            
            // Spring force: F_spring = -k * x
            // Progressive spring bisa ditambahkan dengan kuadratik term
            float springForce = -springRate * travel;
            
            // Damper force: F_damper = -c * v
            // Different damping untuk compression vs rebound
            float dampingCoeff = velocity < 0f ? dampingCompression : dampingRebound;
            float damperForce = -dampingCoeff * velocity;
            
            // Bump stop: tambahan spring saat travel melebihi threshold
            float bumpStopForce = 0f;
            if (travel > bumpStopTravel)
            {
                float bumpTravel = travel - bumpStopTravel;
                // Quadratic bump stop untuk progresivitas
                bumpStopForce = -bumpStopRate * bumpTravel * bumpTravel;
            }
            
            // Total gaya suspensi (positif = mendorong body ke atas)
            float totalForce = -(springForce + damperForce + bumpStopForce);
            
            return math.max(totalForce, 0f);  // Suspensi tidak bisa menarik
        }
        
        /// <summary>
        /// Hitung kinematika suspensi (camber, toe, caster) berdasarkan travel dan steering.
        /// Model sederhana dengan linear gain - bisa diperluas dengan lookup table.
        /// </summary>
        /// <param name="travel">Suspensi travel (m)</param>
        /// <param name="steeringInput">Input steering (-1 sampai 1)</param>
        /// <param name="maxSteerAngle">Maximum steering angle (radian)</param>
        /// <param name="camberGain">Camber change per meter travel (rad/m)</param>
        /// <param name="toeChangePerTravel">Toe change per meter travel (rad/m)</param>
        /// <param name="bumpSteerGain">Bump steer gain (rad/m)</param>
        /// <param name="staticCamber">Static camber (radian)</param>
        /// <param name="staticToe">Static toe (radian)</param>
        /// <returns>Tuple (camber, toe, caster) dalam radian</returns>
        [BurstCompile]
        public static (float camber, float toe, float caster) CalculateKinematics(
            float travel,
            float steeringInput,
            float maxSteerAngle,
            float camberGain,
            float toeChangePerTravel,
            float bumpSteerGain,
            float staticCamber,
            float staticToe)
        {
            // Camber: static + perubahan akibat travel
            float camber = staticCamber + camberGain * travel;
            
            // Toe: static + perubahan akibat travel + bump steer
            float toe = staticToe + toeChangePerTravel * travel;
            toe += bumpSteerGain * travel * math.sign(steeringInput);
            
            // Caster: biasanya konstan atau sedikit berubah dengan travel
            // Untuk simplifikasi, kita anggap konstan
            float caster = 0.087f;  // ~5 derajat
            
            return (camber, toe, caster);
        }
        
        /// <summary>
        /// Hitung posisi hub roda dalam world space berdasarkan suspensi travel.
        /// </summary>
        /// <param name="chassisPosition">Posisi chassis (world space)</param>
        /// <param name="chassisRotation">Rotasi chassis (quaternion)</param>
        /// <param name="localHubPosition">Posisi hub dalam local space (saat rest)</param>
        /// <param name="travelDirection">Arah travel suspensi dalam local space</param>
        /// <param name="travel">Suspensi travel (m)</param>
        /// <returns>Posisi hub dalam world space</returns>
        [BurstCompile]
        public static float3 CalculateHubPosition(
            float3 chassisPosition,
            quaternion chassisRotation,
            float3 localHubPosition,
            float3 travelDirection,
            float travel)
        {
            // Hub bergerak sepanjang travel direction
            float3 localPosition = localHubPosition + travelDirection * travel;
            
            // Transform ke world space
            float3 worldPosition = math.rotate(chassisRotation, localPosition) + chassisPosition;
            
            return worldPosition;
        }
        
        /// <summary>
        /// Deteksi kontak dengan ground dan hitung ride height.
        /// </summary>
        /// <param name="hubPosition">Posisi hub (world space)</param>
        /// <param name="groundHeight">Tinggi ground di posisi tersebut</param>
        /// <param name="wheelRadius">Radius ban (m)</param>
        /// <returns>Ride height (m), negatif jika ban di bawah ground</returns>
        [BurstCompile]
        public static float CalculateRideHeight(float3 hubPosition, float groundHeight, float wheelRadius)
        {
            float hubHeight = hubPosition.y;
            float rideHeight = hubHeight - groundHeight - wheelRadius;
            
            return rideHeight;
        }
    }
}
