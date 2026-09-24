using UnityEngine;
using RacingSim.Aero;

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
        
        [Tooltip("Moment of inertia around X axis (roll) (kg·m²)")]
        public float momentOfInertiaX = 900f;
        
        [Tooltip("Moment of inertia around Y axis (pitch) (kg·m²)")]
        public float momentOfInertiaY = 1100f;
        
        [Tooltip("Moment of inertia around Z axis (yaw) (kg·m²)")]
        public float momentOfInertiaZ = 1400f;
        
        [Tooltip("Rolling resistance coefficient")]
        public float rollingResistanceCoefficient = 0.015f;
        
        [Tooltip("Angular damping coefficient")]
        public float angularDampingCoefficient = 0.02f;
        
        [Tooltip("Wheel radius (m)")]
        public float wheelRadius = 0.33f;
        
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
        
        [Tooltip("Engine torque curve: RPM vs Torque (Nm). Array of (RPM, Torque) pairs.")]
        public AnimationCurve engineTorqueCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(3000f, 280f),
            new Keyframe(6000f, 350f),
            new Keyframe(9000f, 380f),
            new Keyframe(11500f, 360f),
            new Keyframe(13500f, 280f)
        );
        
        [Tooltip("Tire coefficients for Pacejka Magic Formula")]
        public TireCoefficientsData tireCoefficients = new TireCoefficientsData();
        
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
        public DifferentialType differentialType = DifferentialType.LimitedSlip;
        
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
        [Tooltip("Enable Traction Control")]
        public bool tcEnabled = true;
        
        [Tooltip("Level Traction Control (0 = off, 10 = max)")]
        public int tcLevel = 5;
        
        [Tooltip("Enable ABS")]
        public bool absEnabled = true;
        
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

        [Header("Transmission")]
        public TransmissionType transmissionType = TransmissionType.Manual;
        public float shiftDuration = 0.08f;
        public float reverseGearRatio = -3.5f;
        public float optimalShiftRPM = 11000f;
        public float awdFrontBias = 0.4f;

        [Header("Engine Extended")]
        public float engineMomentOfInertia = 0.015f;
        public float engineFrictionConstant = 5f;
        public float engineBrakingBase = 40f;
        public float engineHeatGenerationBase = 80000f;
        public float engineSurfaceArea = 1.2f;
        public float engineConvectionCoeff = 25f;
        public float engineMass = 120f;
        public float engineTempOptimalMax = 105f;
        public float engineTempCritical = 125f;
        public float engineDamageOverrevRate = 0.5f;
        public float engineDamageOverheatRate = 0.3f;
        public float engineDamageColdRate = 0.1f;
        public float optimalOilTempMin = 80f;
        public float oilCapacity = 4.5f;
        public float oilMaxTemperature = 150f;
        public float oilCoolerEfficiencyBase = 0.4f;
        public float oilCoolerAirflowFactor = 0.02f;
        public float oilEngineHeatTransferCoeff = 150f;
        public float coolantEfficiencyBase = 0.5f;
        public float coolantEfficiencyFlowFactor = 0.1f;

        [Header("Clutch Extended")]
        public float clutchFrictionCoeff = 0.35f;
        public float clutchNormalForce = 5000f;
        public int clutchNumFrictionSurfaces = 4;

        [Header("Differential Extended")]
        public float lsdPreloadTorque = 80f;
        public float lsdRampFactor = 1.5f;
        public float lsdRampAngle = 45f;
        public float viscousDiffCoefficient = 15f;
        public float torsenTBR = 3.0f;

        [Header("Electronics Extended")]
        public float tcTargetSlip = 0.08f;
        public float absTargetSlip = -0.10f;
        public float absSlipThreshold = -0.20f;
        public float brakeBiasFront = 0.58f;
        public float cgHeight = 0.28f;
        public float ebdLoadTransferGain = 0.15f;
        public float ebdBrakingGain = 0.1f;
        public float ebdCorneringGain = 0.05f;
        public float minBrakeBias = 0.5f;
        public float maxBrakeBias = 0.7f;
        public float launchControlRPM = 5500f;
        public float launchControlSensitivity = 0.8f;
        public float launchControlMaxSlip = 0.25f;
        public float shiftLightEarlyMargin = 500f;
        public int numShiftLights = 10;
        public float shiftLightStartRPM = 10000f;

        [Header("Brake Extended")]
        public float brakeCaliperAreaFront = 0.0025f;
        public float brakeCaliperAreaRear = 0.0018f;
        public float brakeDiscRadiusFront = 0.165f;
        public float brakeDiscRadiusRear = 0.155f;
        public float brakePadFrictionFront = 0.45f;
        public float brakePadFrictionRear = 0.45f;
        public float brakeEnergyToDiscFraction = 0.95f;
        public float brakeConvectionBase = 80f;
        public float brakeConvectionSpeedFactor = 5f;
        public float brakeDiscArea = 0.04f;
        public float brakeVaneFactor = 1.0f;
        public float brakeEmissivity = 0.8f;
        public float brakeConductionCoeff = 10f;
        public float brakeDiscRadius = 0.165f;
        public float brakeDiscThickness = 0.028f;
        public float brakeMaxTemperature = 700f;
        public float brakeFadeOnsetTemp = 500f;
        public float brakeFadeSeverity = 0.5f;
        public float brakeOptimalTempMin = 200f;

        [Header("Suspension Damage")]
        public float suspensionMaxLoad = 8000f;
        public float suspensionTravelMax = 0.08f;
        public float suspensionDamageOverloadRate = 0.2f;
        public float suspensionDamageBottomingRate = 0.1f;
        public float suspensionDamageOvertravelRate = 0.15f;

        [Header("Tire Extended")]
        public float tireWearBaseRate = 0.00005f;
        public float tireWearSlipRate = 0.0002f;
        public float tireWearFlatSpotRate = 0.001f;
        public float tireOverheatThreshold = 140f;
        public float tireMaxLoad = 12000f;
        public float tireBurstTemperature = 180f;
        public float tireHysteresisBase = 500f;
        public float tireConvectionBase = 20f;
        public float tireConvectionSpeedFactor = 2f;
        public float tireSurfaceAreaInner = 0.05f;
        public float tireSurfaceAreaMiddle = 0.05f;
        public float tireSurfaceAreaOuter = 0.05f;
        public float tireEmissivity = 0.9f;
        public float tireContactPatchArea = 0.02f;
        public float tireRoadConductionCoeff = 500f;
        public float tireAspectRatio = 0.35f;
        public float tireInternalTransferRate = 2f;
        public float tireColdThreshold = 60f;

        [Header("Aero Extended")]
        public float drsCdMultiplier = 0.75f;
        public float drsClMultiplier = 0.60f;
        public float altitude = 0f;
        public float frontalArea = 1.65f;
        public float planformAreaFront = 1.4f;
        public float planformAreaRear = 1.8f;
        public float sideArea = 2.5f;
        public float aeroCenterOfPressureX = 0.1f;
        public float aeroCenterOfPressureY = -0.05f;
        public float aeroCenterOfPressureZ = 0f;
        public float cdBase = 0.85f;
        public float clFrontBase = -1.2f;
        public float clRearBase = -1.6f;
        public float csBase = 0.8f;
        public float rideHeightSensitivityFront = -2.0f;
        public float rideHeightSensitivityRear = -2.5f;
        public float rideHeightDragSensitivity = 1.5f;
        public float wingFrontLiftGain = 0.5f;
        public float wingFrontDragGain = 0.1f;
        public float wingRearLiftGain = 1.0f;
        public float wingRearDragGain = 0.2f;
        public float wheelDragCoefficient = 0.7f;
        public AeroMapPoint[] aeroMap;

        [Header("Aero Damage")]
        public float aeroDamageThreshold = 5000f;
        public float aeroDamageCoefficient = 0.0001f;
    }

    public enum DifferentialType
    {
        Open,
        LimitedSlip,
        Viscous,
        Torsen
    }

    public enum DrivetrainType
    {
        FWD,
        RWD,
        AWD
    }

    public enum TransmissionType
    {
        Manual,
        Automatic,
        SemiAutomatic
    }
    
    /// <summary>
    /// Tire coefficients for Pacejka Magic Formula
    /// Can be serialized in Unity Inspector
    /// </summary>
    [System.Serializable]
    public struct TireCoefficientsData
    {
        [Header("Lateral Coefficients (Fy)")]
        public float pCy1;  // Shape factor lateral
        public float pDy1, pDy2;  // Peak value lateral
        public float pEy1, pEy2, pEy3;  // Curvature lateral
        public float pKy1, pKy2, pKy3;  // Stiffness lateral
        
        [Header("Longitudinal Coefficients (Fx)")]
        public float pCx1;  // Shape factor longitudinal
        public float pDx1, pDx2;  // Peak value longitudinal
        public float pEx1, pEx2, pEx3;  // Curvature longitudinal
        public float pKx1, pKx2;  // Stiffness longitudinal
        
        [Header("Combined Slip Coefficients")]
        public float rBx1, rBx2;  // Combined longitudinal
        public float rBy1, rBy2;  // Combined lateral
        public float rCx1, rCy1;  // Combined shape factors
        
        [Header("Aligning Torque Coefficients")]
        public float qBz1, qBz2;  // Trail stiffness
        public float qCz1;  // Trail shape
        public float qDz1, qDz2;  // Trail peak
        public float qEz1, qEz2;  // Trail curvature
        
        /// <summary>
        /// Default coefficients for F1 slick tires
        /// </summary>
        public static TireCoefficientsData Default => new TireCoefficientsData
        {
            // Lateral
            pCy1 = 1.3f,
            pDy1 = 1.9f,
            pDy2 = 0f,
            pEy1 = -1.7f,
            pEy2 = 0f,
            pEy3 = 0f,
            pKy1 = 15f,
            pKy2 = 2f,
            pKy3 = 0.5f,
            
            // Longitudinal
            pCx1 = 1.65f,
            pDx1 = 1.1f,
            pDx2 = 0f,
            pEx1 = 0.48f,
            pEx2 = 0f,
            pEx3 = 0f,
            pKx1 = 25f,
            pKx2 = 3f,
            
            // Combined
            rBx1 = 8f,
            rBx2 = 0f,
            rBy1 = 8f,
            rBy2 = 0f,
            rCx1 = 1.3f,
            rCy1 = 1.3f,
            
            // Aligning torque
            qBz1 = 9f,
            qBz2 = -3f,
            qCz1 = 1.01f,
            qDz1 = 0f,
            qDz2 = 0f,
            qEz1 = 0.01f,
            qEz2 = 0f
        };
    }
}
