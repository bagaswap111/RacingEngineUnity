using UnityEngine;

namespace RacingSim.Vehicle
{
    /// <summary>
    /// ScriptableObject untuk menyimpan semua parameter kendaraan.
    /// Dapat di-assign di Unity Inspector dan direferensikan oleh sistem simulasi.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "Racing Sim/Vehicle Config")]
    public class VehicleConfig : ScriptableObject
    {
        [Header("General Vehicle Parameters")]
        [Tooltip("Total massa kendaraan termasuk driver (kg)")]
        public float totalMass = 750f;
        
        [Tooltip("Jarak wheelbase (m)")]
        public float wheelbase = 2.65f;
        
        [Tooltip("Jarak CoG ke as depan (m)")]
        public float cogToFrontAxle = 1.15f;
        
        [Tooltip("Jarak CoG ke as belakang (m)")]
        public float cogToRearAxle = 1.50f;
        
        [Tooltip("Tinggi pusat massa dari ground (m)")]
        public float cogHeight = 0.28f;
        
        [Tooltip("Track width depan (m)")]
        public float frontTrackWidth = 1.45f;
        
        [Tooltip("Track width belakang (m)")]
        public float rearTrackWidth = 1.40f;
        
        [Header("Engine Parameters")]
        [Tooltip("RPM idle")]
        public float engineIdleRPM = 900f;
        
        [Tooltip("RPM redline")]
        public float engineRedlineRPM = 13500f;
        
        [Tooltip("RPM max power")]
        public float engineMaxPowerRPM = 11500f;
        
        [Tooltip("Max power (HP)")]
        public float engineMaxPower = 550f;
        
        [Tooltip("Max torque (Nm)")]
        public float engineMaxTorque = 380f;
        
        [Tooltip("Inersia mesin (kg·m²)")]
        public float engineInertia = 0.015f;
        
        [Tooltip("Koefisien gesekan mesin")]
        public float engineFrictionBase = 5f;
        
        [Tooltip("Koefisien gesekan viskos")]
        public float engineFrictionViscous = 0.002f;
        
        [Range(0f, 1f)]
        [Tooltip("Efisiensi gearbox")]
        public float gearboxEfficiency = 0.97f;
        
        [Tooltip("Rasio gear: R, 1, 2, 3, 4, 5, 6, 7")]
        public float[] gearRatios = new float[] { -3.5f, 3.8f, 2.9f, 2.3f, 1.9f, 1.6f, 1.3f, 1.1f };
        
        [Tooltip("Rasio final drive")]
        public float finalDriveRatio = 4.2f;
        
        [Header("Clutch Parameters")]
        [Tooltip("Koefisien gesek clutch")]
        public float clutchFrictionCoefficient = 0.35f;
        
        [Tooltip("Gaya tekan clutch maksimum (N)")]
        public float clutchMaxForce = 5000f;
        
        [Tooltip("Radius efektif clutch (m)")]
        public float clutchEffectiveRadius = 0.12f;
        
        [Tooltip("Threshold untuk locked state (rad/s)")]
        public float clutchLockThreshold = 5f;
        
        [Header("Differential Parameters")]
        [Tooltip("Tipe differential: 0=Open, 1=LSD Clutch, 2=Viscous, 3=Torsen")]
        public DifferentialType differentialType = DifferentialType.LSD_Clutch;
        
        [Tooltip("Preload torque untuk LSD (Nm)")]
        public float diffPreloadTorque = 80f;
        
        [Tooltip("Ramp angle untuk LSD (derajat)")]
        public float diffRampAngle = 45f;
        
        [Tooltip("Koefisien viskos untuk viscous diff")]
        public float diffViscousCoefficient = 15f;
        
        [Tooltip("Torque Bias Ratio untuk Torsen")]
        public float diffTorqueBiasRatio = 3.0f;
        
        [Header("Brake Parameters")]
        [Tooltip("Luas piston caliper depan (m²)")]
        public float frontBrakeCaliperArea = 0.0025f;
        
        [Tooltip("Luas piston caliper belakang (m²)")]
        public float rearBrakeCaliperArea = 0.0018f;
        
        [Tooltip("Koefisien gesek pad (base)")]
        public float brakePadFrictionCoefficient = 0.45f;
        
        [Tooltip("Radius efektif disc depan (m)")]
        public float frontBrakeDiscRadius = 0.165f;
        
        [Tooltip("Radius efektif disc belakang (m)")]
        public float rearBrakeDiscRadius = 0.155f;
        
        [Tooltip("Jumlah piston caliper depan")]
        public int frontBrakePistons = 6;
        
        [Tooltip("Jumlah piston caliper belakang")]
        public int rearBrakePistons = 4;
        
        [Range(0f, 1f)]
        [Tooltip("Brake bias (0.5 = 50% depan)")]
        public float brakeBias = 0.58f;
        
        [Tooltip("Tekanan rem maksimum (Pa)")]
        public float brakeMaxPressure = 12000000f; // 120 bar
        
        [Tooltip("Massa disc depan per roda (kg)")]
        public float frontBrakeDiscMass = 1.2f;
        
        [Tooltip("Massa disc belakang per roda (kg)")]
        public float rearBrakeDiscMass = 0.9f;
        
        [Tooltip("Kapasitas panas disc (J/kg·K)")]
        public float brakeDiscSpecificHeat = 500f;
        
        [Tooltip("Luas permukaan disc untuk konveksi (m²)")]
        public float brakeDiscSurfaceArea = 0.04f;
        
        [Tooltip("Emissivitas disc")]
        public float brakeDiscEmissivity = 0.8f;
        
        [Tooltip("Koefisien konveksi (W/m²·K)")]
        public float brakeConvectionCoefficient = 80f;
        
        [Header("Suspension Parameters")]
        [Tooltip("Spring rate depan (N/m)")]
        public float frontSpringRate = 180000f;
        
        [Tooltip("Spring rate belakang (N/m)")]
        public float rearSpringRate = 200000f;
        
        [Tooltip("Compression damping depan (N·s/m)")]
        public float frontDampingCompression = 4500f;
        
        [Tooltip("Rebound damping depan (N·s/m)")]
        public float frontDampingRebound = 6500f;
        
        [Tooltip("Compression damping belakang (N·s/m)")]
        public float rearDampingCompression = 5000f;
        
        [Tooltip("Rebound damping belakang (N·s/m)")]
        public float rearDampingRebound = 7000f;
        
        [Tooltip("Travel suspensi maksimum (m)")]
        public float suspensionMaxTravel = 0.08f;
        
        [Tooltip("Travel bump start (m)")]
        public float suspensionBumpStopTravel = 0.065f;
        
        [Tooltip("Spring rate bump stop (N/m)")]
        public float suspensionBumpStopRate = 800000f;
        
        [Tooltip("Camber gain (rad/m)")]
        public float camberGain = -0.035f;
        
        [Tooltip("Toe change per travel (rad/m)")]
        public float toeChangePerTravel = 0.005f;
        
        [Tooltip("Bump steer gain (rad/m)")]
        public float bumpSteerGain = 0.008f;
        
        [Tooltip("Roll stiffness depan (N·m/rad)")]
        public float frontRollStiffness = 35000f;
        
        [Tooltip("Roll stiffness belakang (N·m/rad)")]
        public float rearRollStiffness = 42000f;
        
        [Header("Wheel & Tire Parameters")]
        [Tooltip("Radius ban (m)")]
        public float tireRadius = 0.33f;
        
        [Tooltip("Radius efektif ban untuk perhitungan slip (m)")]
        public float tireEffectiveRadius = 0.325f;
        
        [Tooltip("Tekanan ban nominal (kPa)")]
        public float tireNominalPressure = 115f;
        
        [Tooltip("Lebar ban (mm)")]
        public float tireWidth = 305f;
        
        [Tooltip("Massa ban per roda (kg)")]
        public float tireMass = 12f;
        
        [Tooltip("Kapasitas panas karet (J/kg·K)")]
        public float tireSpecificHeat = 1800f;
        
        [Tooltip("Suhu optimal ban (°C)")]
        public float tireOptimalTemp = 100f;
        
        [Tooltip("Range suhu optimal (°C)")]
        public float tireOptimalTempRange = 35f;
        
        [Header("Aerodynamics Parameters")]
        [Tooltip("Drag coefficient base")]
        public float aeroCdBase = 0.85f;
        
        [Tooltip("Lift coefficient base (negatif = downforce)")]
        public float aeroClBase = -2.8f;
        
        [Tooltip("Balance downforce depan (0.45 = 45% depan)")]
        public float aeroBalanceFront = 0.45f;
        
        [Tooltip("Luas frontal (m²)")]
        public float aeroFrontalArea = 1.65f;
        
        [Tooltip("Luas planform (m²)")]
        public float aeroPlanformArea = 3.2f;
        
        [Tooltip("DRS Cd multiplier")]
        public float aeroDRSCdMultiplier = 0.75f;
        
        [Tooltip("DRS Cl rear multiplier")]
        public float aeroDRSClRearMultiplier = 0.60f;
        
        [Header("Electronics Parameters")]
        [Tooltip("Level Traction Control (0 = off, 10 = max)")]
        public int tcLevel = 5;
        
        [Tooltip("Level ABS (0 = off, 10 = max)")]
        public int absLevel = 7;
        
        [Tooltip("Target slip ratio untuk TC")]
        public float tcTargetSlipRatio = 0.08f;
        
        [Tooltip("Target slip ratio untuk ABS")]
        public float absTargetSlipRatio = -0.10f;
        
        [Tooltip("Kp untuk TC PID")]
        public float tcKp = 2.5f;
        
        [Tooltip("Ki untuk TC PID")]
        public float tcKi = 0.5f;
        
        [Tooltip("Kd untuk TC PID")]
        public float tcKd = 0.1f;
        
        [Tooltip("Kp untuk ABS PID")]
        public float absKp = 3.0f;
        
        [Tooltip("Ki untuk ABS PID")]
        public float absKi = 0.3f;
        
        [Tooltip("Kd untuk ABS PID")]
        public float absKd = 0.05f;
        
        [Header("Damage Parameters")]
        [Tooltip("Threshold over-rev time sebelum damage (detik)")]
        public float engineOverRevThreshold = 2.5f;
        
        [Tooltip("Suhu kritis mesin (°C)")]
        public float engineCriticalTemp = 125f;
        
        [Tooltip("Threshold gaya untuk break suspensi (N)")]
        public float suspensionBreakForceThreshold = 25000f;
        
        [Tooltip("Threshold gaya untuk ban pecah (N)")]
        public float tireBurstForceThreshold = 30000f;
        
        [Tooltip("Suhu burst ban (°C)")]
        public float tireBurstTemp = 180f;
        
        [Header("Drivetrain Type")]
        [Tooltip("Tipe drivetrain: FWD, RWD, AWD")]
        public DrivetrainType drivetrainType = DrivetrainType.RWD;
    }
    
    public enum DifferentialType
    {
        Open,
        LSD_Clutch,
        Viscous,
        Torsen
    }
    
    public enum DrivetrainType
    {
        FWD,
        RWD,
        AWD
    }
}
