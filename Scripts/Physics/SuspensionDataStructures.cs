using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Physics
{
    // ====================================================================
    // ENUMS
    // ====================================================================

    public enum SuspensionType
    {
        DoubleWishbone,
        MacPherson,
        Pushrod,
        Pullrod,
        MultiLink,
        TrailingArm,
        SemiTrailingArm,
        SolidAxle
    }

    // ====================================================================
    // DAMPER CURVE
    // ====================================================================

    [System.Serializable]
    public struct DamperCurve
    {
        public float[] VelocityPoints;
        public float[] ForcePoints;
        public int PointCount;

        public float Sample(float velocity)
        {
            if (PointCount < 2) return 0f;

            for (int i = 0; i < PointCount - 1; i++)
            {
                if (velocity >= VelocityPoints[i] && velocity <= VelocityPoints[i + 1])
                {
                    float t = (velocity - VelocityPoints[i]) /
                              (VelocityPoints[i + 1] - VelocityPoints[i]);
                    return math.lerp(ForcePoints[i], ForcePoints[i + 1], t);
                }
            }

            if (velocity <= VelocityPoints[0])
                return ForcePoints[0];
            return ForcePoints[PointCount - 1];
        }
    }

    // ====================================================================
    // DAMPER CONFIG (4-Way Adjustable)
    // ====================================================================

    [System.Serializable]
    public struct DamperConfig
    {
        [Header("Low-Speed Compression")]
        public float LSC_Rate;
        [Header("High-Speed Compression")]
        public float HSC_Rate;
        [Header("Low-Speed Rebound")]
        public float LSR_Rate;
        [Header("High-Speed Rebound")]
        public float HSR_Rate;
        [Header("Knee Velocities")]
        public float KneeVelocityComp;
        public float KneeVelocityReb;
        [Header("Motion Ratio")]
        public float MotionRatio;
        [Header("Digressive Limits")]
        public bool IsDigressiveComp;
        public bool IsDigressiveReb;
        public float MaxCompForce;
        public float MaxRebForce;
        [Header("Response")]
        public float Smoothing;
    }

    // ====================================================================
    // SUSPENSION CONFIGURATION
    // ====================================================================

    [System.Serializable]
    public struct SuspensionConfig
    {
        [Header("Type")]
        public SuspensionType Type;

        [Header("Hardpoints (local space, meters)")]
        public float3 UCA_Front;
        public float3 UCA_Rear;
        public float3 UCA_Outboard;
        public float3 LCA_Front;
        public float3 LCA_Rear;
        public float3 LCA_Outboard;
        public float3 Strut_Top;
        public float3 SteeringAxis_Top;
        public float3 SteeringAxis_Bottom;
        public float3 WheelCenter;
        public float3 ContactPatch;

        [Header("Spring")]
        public float SpringRate;
        public float SpringFreeLength;
        public float SpringInstalledLength;
        public float SpringMotionRatio;
        public bool IsProgressiveSpring;
        public float ProgressiveRate;

        [Header("Damper")]
        public DamperConfig Damper;

        [Header("Anti-Roll Bar")]
        public float ARBRate;
        public float ARBPreload;
        public float ARBRatio;

        [Header("Bump Stop")]
        public float BumpStopEngageTravel;
        public float BumpStopStiffness;
        public float BumpStopExponent;
        public float BumpStopLength;

        [Header("Droop Stop")]
        public float DroopStopStiffness;

        [Header("Travel Limits")]
        public float MaxBumpTravel;
        public float MaxDroopTravel;

        [Header("Mass")]
        public float UnsprungMass;
        public float UprightMass;

        [Header("Bushing")]
        public float BushingRadialStiffness;
        public float BushingAxialStiffness;
        public float BushingComplianceFactor;
        public float CamberComplianceFactor;
        public float ToeComplianceFactor;

        [Header("Steering")]
        public float StaticCamber;
        public float StaticToe;
        public float BumpSteerGradient;
        public float SteeringAxisInclination;
        public float CasterAngle;
        public float ScrubRadius;
        public float SteeringRatio;
        public float AckermannRatio;
        public bool IsFrontWheel;

        [Header("Geometry")]
        public float TrackWidth;
    }

    // ====================================================================
    // SUSPENSION STATE
    // ====================================================================

    public struct SuspensionState
    {
        [Header("Travel")]
        public float Travel;
        public float TravelVelocity;
        public float RideHeight;

        [Header("Forces")]
        public float SpringForce;
        public float DamperForce;
        public float ARBForce;
        public float BumpStopForce;
        public float DroopStopForce;
        public float TotalForce;

        [Header("Geometry")]
        public float Camber;
        public float Toe;
        public float Caster;
        public float KingpinInclination;
        public float ScrubRadius;
        public float RollCenterHeight;
        public float3 InstantCenterPos;

        [Header("Kinematics")]
        public float MotionRatio;
        public float3 HubPosition;
        public float3 ContactPatchPos;
        public float3 WheelNormal;

        [Header("Steering")]
        public float SteeringAngle;
        public float AckermannAngle;

        [Header("Bushing")]
        public float3 BushingOffset;

        [Header("Status")]
        public bool IsAtBumpStop;
        public bool IsAtDroopStop;
        public bool IsGrounded;
        public bool IsBroken;
        public float DamageLevel;

        [Header("Anti-Dive/Squat")]
        public float AntiDive;
        public float AntiSquat;
    }

    // ====================================================================
    // GROUND HIT DATA
    // ====================================================================

    public struct GroundHitData
    {
        public bool DidHit;
        public float Distance;
        public float3 Point;
        public float3 Normal;
        public float SurfaceGrip;
        public int SurfaceType;
    }

    // ====================================================================
    // LOAD TRANSFER RESULT
    // ====================================================================

    public struct LoadTransferResult
    {
        public float LongitudinalFront;
        public float LongitudinalRear;
        public float LateralFront;
        public float LateralRear;
        public float GeometricFront;
        public float GeometricRear;
        public float ElasticFront;
        public float ElasticRear;
    }
}
