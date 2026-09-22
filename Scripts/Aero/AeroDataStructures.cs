using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Aero
{
    public enum AeroTier
    {
        Minimum = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    [System.Serializable]
    public struct AeroConfig
    {
        [Header("Base Coefficients")]
        public float CdBase;
        public float ClFrontBase;
        public float ClRearBase;
        public float CsBase;
        public float CyawBase;

        [Header("Areas")]
        public float FrontalArea;
        public float PlanformAreaFront;
        public float PlanformAreaRear;
        public float SideArea;
        public float Wheelbase;
        public float TrackWidth;

        [Header("Balance")]
        public float BalanceFront;

        [Header("Wing")]
        public float FrontWingClGain;
        public float FrontWingCdGain;
        public float RearWingClGain;
        public float RearWingCdGain;
        public float FrontWingAngle;
        public float RearWingAngle;
        public float MaxFrontWingAngle;
        public float MaxRearWingAngle;

        [Header("DRS")]
        public float DRS_ClReduction;
        public float DRS_CdReduction;
        public float DRS_TransitionTime;
        public float DRS_MinSpeed;

        [Header("Ground Effect")]
        public float ClGroundEffectFront;
        public float ClGroundEffectRear;
        public float RHReference;
        public float PitchSensitivityFront;
        public float PitchSensitivityRear;
        public float YawGroundEffectLoss;
        public float DiffuserStallPitch;
        public float DiffuserStallMultiplier;

        [Header("Slipstream")]
        public float WakeLength;
        public float WakeExpansionRate;
        public float MaxVelocityDeficit;
        public float DragReductionFactor;
        public float DownforceReductionFactor;
        public float BaseTurbulence;
        public float CarWidth;

        [Header("Crosswind")]
        public float GustAmplitude;
        public float GustFrequency;
        public float TurbulenceIntensity;
        public float TurbulenceScale;
        public float CrosswindSensitivity;

        [Header("Air")]
        public float AirDensitySeaLevel;
        public float Altitude;

        public static AeroConfig Default()
        {
            return new AeroConfig
            {
                CdBase = 0.45f,
                ClFrontBase = -1.2f,
                ClRearBase = -1.5f,
                CsBase = 0.3f,
                CyawBase = 0.02f,

                FrontalArea = 2.0f,
                PlanformAreaFront = 1.0f,
                PlanformAreaRear = 1.2f,
                SideArea = 4.5f,
                Wheelbase = 2.6f,
                TrackWidth = 1.6f,

                BalanceFront = 0.45f,

                FrontWingClGain = 0.12f,
                FrontWingCdGain = 0.008f,
                RearWingClGain = 0.15f,
                RearWingCdGain = 0.012f,
                FrontWingAngle = 15f,
                RearWingAngle = 20f,
                MaxFrontWingAngle = 30f,
                MaxRearWingAngle = 40f,

                DRS_ClReduction = 0.50f,
                DRS_CdReduction = 0.25f,
                DRS_TransitionTime = 0.3f,
                DRS_MinSpeed = 28f,

                ClGroundEffectFront = 0.8f,
                ClGroundEffectRear = 1.2f,
                RHReference = 50f,
                PitchSensitivityFront = 3.0f,
                PitchSensitivityRear = 4.0f,
                YawGroundEffectLoss = 0.5f,
                DiffuserStallPitch = 0.04f,
                DiffuserStallMultiplier = 0.4f,

                WakeLength = 15f,
                WakeExpansionRate = 0.08f,
                MaxVelocityDeficit = 0.35f,
                DragReductionFactor = 0.7f,
                DownforceReductionFactor = 0.5f,
                BaseTurbulence = 0.3f,
                CarWidth = 1.8f,

                GustAmplitude = 5f,
                GustFrequency = 0.5f,
                TurbulenceIntensity = 0.2f,
                TurbulenceScale = 0.01f,
                CrosswindSensitivity = 0.4f,

                AirDensitySeaLevel = 1.225f,
                Altitude = 0f
            };
        }
    }

    public struct AeroForces
    {
        public float Drag;
        public float DownforceFront;
        public float DownforceRear;
        public float SideForce;
        public float YawMoment;
        public float CdTotal;
        public float ClFrontTotal;
        public float ClRearTotal;
        public float CsTotal;
        public float DynamicPressure;
        public float AirDensity;
        public float Speed;
        public float YawAngle;
        public bool SlipstreamActive;
        public bool DRSActive;
        public bool DiffuserStalled;
    }

    public struct WakeData
    {
        public float3 Position;
        public float3 Direction;
        public float Width;
        public float Length;
        public float VelocityDeficit;
        public float Turbulence;
        public float Age;
    }

    [System.Serializable]
    public struct YawMapEntry
    {
        public float YawAngleDeg;
        public float CdMultiplier;
        public float ClMultiplier;
        public float CsCoefficient;
        public float CyawCoefficient;
    }

    [System.Serializable]
    public struct RideHeightMapEntry
    {
        public float RideHeightFront;
        public float RideHeightRear;
        public float ClBase;
        public float CdBase;
    }

    public struct SlipstreamResult
    {
        public float DragMultiplier;
        public float DownforceMultiplier;
        public float TurbulenceIntensity;
        public float TotalVelocityDeficit;
    }

    public struct GroundEffectResult
    {
        public float FrontContribution;
        public float RearContribution;
        public bool DiffuserStalled;
        public float HeightFactorFront;
        public float HeightFactorRear;
    }

    public struct CrosswindResult
    {
        public float3 WindVector;
        public float SideForce;
        public float YawMoment;
        public float EffectiveYawAngle;
    }

    public struct AeroDamageResult
    {
        public float DragIncrease;
        public float FrontDownLoss;
        public float RearDownLoss;
        public float SideSensitivity;
        public float GroundEffectLoss;
        public bool DRSDisabled;
    }
}
