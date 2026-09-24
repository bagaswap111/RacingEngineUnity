using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Core;

namespace RacingSim.Physics
{
    /// <summary>
    /// Kinematic solver for suspension geometry.
    /// Calculates camber, toe, roll center, instant center, motion ratio
    /// based on control arm hardpoints and travel.
    /// </summary>

    public static class SuspensionKinematics
    {
        private const int SOLVER_ITERATIONS = 5;
        private const float RAD2DEG = 57.29578f;
        private const float DEG2RAD = 0.01745329f;

        /// <summary>
        /// Main solver: compute full suspension state from config and travel.
        /// </summary>

        public static SuspensionState Solve(
            in SuspensionConfig config,
            float travel,
            float steerInput,
            float bodyRoll)
        {
            float t = travel / 1000f;
            float maxBump = config.MaxBumpTravel / 1000f;
            float maxDroop = config.MaxDroopTravel / 1000f;
            t = math.clamp(t, -maxDroop, maxBump);

            SuspensionState state = new SuspensionState();
            state.Travel = travel;
            state.IsAtBumpStop = travel >= config.MaxBumpTravel - 1f;
            state.IsAtDroopStop = travel <= -config.MaxDroopTravel + 1f;

            float3 uprightPos;
            if (config.Type == SuspensionType.DoubleWishbone)
            {
                uprightPos = SolveDoubleWishbone(config, t);
            }
            else if (config.Type == SuspensionType.MacPherson)
            {
                uprightPos = SolveMacPherson(config, t);
            }
            else
            {
                uprightPos = SolveGeneric(config, t);
            }

            state.HubPosition = uprightPos + config.WheelCenter;
            state.WheelNormal = CalculateWheelNormal(config, uprightPos);

            state.Camber = CalculateCamber(state.WheelNormal) * RAD2DEG;
            state.Caster = config.CasterAngle;
            state.KingpinInclination = CalculateKPI(config, uprightPos) * RAD2DEG;

            float toe = CalculateToe(config, travel, steerInput);
            state.Toe = toe * RAD2DEG;
            state.SteerAngle = CalculateSteeringAngle(config, steerInput);

            state.AckermannAngle = CalculateAckermann(config, steerInput) * RAD2DEG;

            float2 ic = CalculateInstantCenter(config, uprightPos);
            state.InstantCenterPos = new float3(ic.x, ic.y, 0f);
            state.RollCenterHeight = CalculateRollCenter(config, ic) * 1000f;

            state.MotionRatio = CalculateMotionRatio(config, travel);
            state.ScrubRadius = config.ScrubRadius;
            state.ContactPatchPos = CalculateContactPatch(state);

            state.AntiDive = CalculateAntiDive(config);
            state.AntiSquat = CalculateAntiSquat(config);

            return state;
        }

        private static float3 SolveDoubleWishbone(in SuspensionConfig config, float travel)
        {
            float3 basePos = config.LCA_Outboard;
            float3 uprightPos = basePos;

            float3 ucaDir = math.normalize(config.UCA_Outboard - config.UCA_Front);
            float ucaLength = math.length(config.UCA_Outboard - config.UCA_Front);
            float3 lcaDir = math.normalize(config.LCA_Outboard - config.LCA_Front);
            float lcaLength = math.length(config.LCA_Outboard - config.LCA_Front);

            for (int i = 0; i < SOLVER_ITERATIONS; i++)
            {
                float3 targetPos = uprightPos + new float3(0, travel, 0);

                float3 ucaPivot = config.UCA_Front;
                float3 lcaPivot = config.LCA_Front;

                float3 toTarget = targetPos - ucaPivot;
                float distU = math.length(toTarget);
                if (distU > 0.0001f)
                {
                    targetPos = ucaPivot + (toTarget / distU) * ucaLength;
                }

                toTarget = targetPos - lcaPivot;
                float distL = math.length(toTarget);
                if (distL > 0.0001f)
                {
                    targetPos = lcaPivot + (toTarget / distL) * lcaLength;
                }

                uprightPos = math.lerp(uprightPos, targetPos, 0.8f);
            }

            return uprightPos;
        }

        private static float3 SolveMacPherson(in SuspensionConfig config, float travel)
        {
            float3 strutDir = math.normalize(config.Strut_Top - config.LCA_Outboard);
            float3 lcaDir = math.normalize(config.LCA_Outboard - config.LCA_Front);

            float3 basePos = config.LCA_Outboard + strutDir * travel;
            float3 lcaCheck = config.LCA_Front + lcaDir * math.length(config.LCA_Outboard - config.LCA_Front);

            return math.lerp(basePos, lcaCheck, 0.5f) + new float3(0, travel, 0);
        }

        private static float3 SolveGeneric(in SuspensionConfig config, float travel)
        {
            return config.LCA_Outboard + new float3(0, travel, 0);
        }

        private static float3 CalculateWheelNormal(in SuspensionConfig config, in float3 uprightPos)
        {
            float3 steerAxis = math.normalize(config.SteeringAxis_Top - config.SteeringAxis_Bottom);
            float3 right = math.normalize(math.cross(steerAxis, new float3(0, 1, 0)));
            return math.normalize(math.cross(new float3(0, 1, 0), right));
        }

        private static float CalculateCamber(in float3 wheelNormal)
        {
            return math.asin(math.clamp(math.dot(wheelNormal, new float3(1, 0, 0)), -1f, 1f));
        }

        private static float CalculateKPI(in SuspensionConfig config, in float3 uprightPos)
        {
            float3 steerAxis = math.normalize(config.SteeringAxis_Top - config.SteeringAxis_Bottom);
            return math.asin(math.clamp(math.dot(steerAxis, new float3(1, 0, 0)), -1f, 1f));
        }

        private static float CalculateToe(in SuspensionConfig config, float travel, float steerInput)
        {
            float toeStatic = config.StaticToe * DEG2RAD;
            float bumpSteer = config.BumpSteerGradient * DEG2RAD * (travel / 1000f);

            float steerAngle = 0f;
            if (config.IsFrontWheel && config.SteeringRatio > 0f)
            {
                steerAngle = steerInput / config.SteeringRatio;
            }

            return toeStatic + bumpSteer + steerAngle;
        }

        private static float CalculateSteeringAngle(in SuspensionConfig config, float steerInput)
        {
            if (!config.IsFrontWheel || config.SteeringRatio <= 0f) return 0f;
            return steerInput / config.SteeringRatio;
        }

        private static float CalculateAckermann(in SuspensionConfig config, float steerInput)
        {
            if (!config.IsFrontWheel) return 0f;
            if (steerInput == 0f || config.AckermannRatio <= 0f) return 0f;

            float steerAngle = math.abs(steerInput / config.SteeringRatio);
            float ackermann = steerAngle * (1f - config.AckermannRatio);
            return ackermann * math.sign(steerInput);
        }

        private static float2 CalculateInstantCenter(in SuspensionConfig config, in float3 uprightPos)
        {
            float2 ucaStart = new float2(config.UCA_Front.y, config.UCA_Front.z);
            float2 ucaEnd = new float2(config.UCA_Outboard.y + (uprightPos.y - config.LCA_Outboard.y),
                                        config.UCA_Outboard.z);
            float2 lcaStart = new float2(config.LCA_Front.y, config.LCA_Front.z);
            float2 lcaEnd = new float2(config.LCA_Outboard.y + (uprightPos.y - config.LCA_Outboard.y),
                                        config.LCA_Outboard.z);

            float2 d1 = ucaEnd - ucaStart;
            float2 d2 = lcaEnd - lcaStart;

            float denom = d1.x * d2.y - d1.y * d2.x;
            if (math.abs(denom) < 0.0001f)
                return new float2(0f, 100f);

            float2 diff = lcaStart - ucaStart;
            float t = (diff.x * d2.y - diff.y * d2.x) / denom;

            return ucaStart + d1 * t;
        }

        private static float CalculateRollCenter(in SuspensionConfig config, float2 instantCenter)
        {
            float trackHalf = config.TrackWidth * 0.5f;
            if (trackHalf < 0.0001f) return 0f;

            float2 contactPatch = new float2(0f, 0f);
            float2 icToCp = contactPatch - instantCenter;

            float slope = icToCp.y / math.max(math.abs(icToCp.x), 0.0001f);
            float rcHeight = instantCenter.y + slope * (trackHalf - math.abs(instantCenter.x));

            return rcHeight;
        }

        private static float CalculateMotionRatio(in SuspensionConfig config, float travel)
        {
            float mr = config.SpringMotionRatio;

            if (config.Type == SuspensionType.Pushrod || config.Type == SuspensionType.Pullrod)
            {
                float travelAbs = math.abs(travel);
                float mrVariation = 1f + (travelAbs / config.MaxBumpTravel) * 0.1f;
                mr *= mrVariation;
            }

            return mr;
        }

        private static float3 CalculateContactPatch(in SuspensionState state)
        {
            return state.HubPosition + new float3(0, -VehicleConstants.DEFAULT_WHEEL_RADIUS, 0);
        }

        private static float CalculateAntiDive(in SuspensionConfig config)
        {
            float3 frontCP = config.ContactPatch;
            float3 rearIC = config.LCA_Front;
            float3 line = math.normalize(rearIC - frontCP);

            float angle = math.atan2(line.y, math.abs(line.z));
            float hCg = 0.45f;
            float wheelbase = math.length(config.UCA_Front - config.LCA_Front) * 4f;

            if (wheelbase < 0.001f) return 0f;
            return (math.tan(angle) * hCg / wheelbase) * 100f;
        }

        private static float CalculateAntiSquat(in SuspensionConfig config)
        {
            float3 rearCP = config.ContactPatch;
            float3 frontIC = config.UCA_Front;
            float3 line = math.normalize(frontIC - rearCP);

            float angle = math.atan2(line.y, math.abs(line.z));
            float hCg = 0.45f;
            float wheelbase = math.length(config.UCA_Front - config.LCA_Front) * 4f;

            if (wheelbase < 0.001f) return 0f;
            return (math.tan(angle) * hCg / wheelbase) * 100f;
        }
    }
}
