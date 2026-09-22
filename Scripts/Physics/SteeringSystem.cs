using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Steering system physics model.
    /// 
    /// Steering Rack Model:
    ///   steer_wheel_angle → rack_position → tie_rod_displacement
    ///   → wheel_steer_angle
    ///   
    ///   Steering ratio (fixed or variable):
    ///   ratio(steer_angle) = ratio_center + ratio_gain × |steer_angle|
    ///   
    /// Steering Torque Feedback (for FFB):
    ///   τ_steering = Σ(Fy × pneumatic_trail × cos(caster))
    ///              + Σ(Fz × scrub_radius × sin(KPI))
    ///              + friction_torque + inertia_torque
    ///   
    /// Steering Compliance:
    ///   Under high lateral load, steering deflects:
    ///   Δsteer = Fy × steeringCompliance
    ///   steeringCompliance ≈ 0.001-0.005 deg/N
    /// </summary>
    [BurstCompile]
    public struct SteeringConfig
    {
        public float SteeringRatio;
        public float RatioGain;
        public float MaxSteerAngle;
        public float PneumaticTrail;
        public float ScrubRadius;
        public float KPI;
        public float CasterAngle;
        public float SteeringCompliance;
        public float FrictionTorque;
        public float SteeringInertia;

        public static SteeringConfig Default()
        {
            return new SteeringConfig
            {
                SteeringRatio = 16f,
                RatioGain = 2f,
                MaxSteerAngle = 22f,
                PneumaticTrail = 0.03f,
                ScrubRadius = 0.01f,
                KPI = 12f,
                CasterAngle = 6f,
                SteeringCompliance = 0.002f,
                FrictionTorque = 2f,
                SteeringInertia = 0.05f
            };
        }
    }

    [BurstCompile]
    public struct SteeringState
    {
        public float WheelAngle;
        public float RackPosition;
        public float SteeringTorque;
        public float SelfAligningTorque;
        public float FrictionTorque;
    }

    [BurstCompile]
    public static class SteeringSystem
    {
        [BurstCompile]
        public static SteeringState Update(
            SteeringState state,
            SteeringConfig config,
            float steeringWheelAngle,
            float lateralForce,
            float verticalLoad,
            float casterAngle,
            float dt)
        {
            float currentRatio = config.SteeringRatio
                                + config.RatioGain * math.abs(state.WheelAngle);

            state.WheelAngle = steeringWheelAngle / currentRatio;

            state.WheelAngle = math.clamp(
                state.WheelAngle,
                -config.MaxSteerAngle,
                config.MaxSteerAngle);

            float compliance = lateralForce * config.SteeringCompliance;
            state.WheelAngle += compliance;

            state.SelfAligningTorque = CalculateSelfAligningTorque(
                lateralForce, verticalLoad, config, casterAngle);

            state.FrictionTorque = config.FrictionTorque *
                (math.abs(state.WheelAngle) > 0.01f ? 1f : 0f);

            float totalTorque = state.SelfAligningTorque - state.FrictionTorque;
            state.SteeringTorque = totalTorque;

            return state;
        }

        [BurstCompile]
        public static float CalculateSelfAligningTorque(
            float lateralForce,
            float verticalLoad,
            SteeringConfig config,
            float casterAngle)
        {
            float pneumaticTrail = lateralForce * config.PneumaticTrail *
                math.cos(math.radians(casterAngle));

            float mechanicalTrail = verticalLoad * config.ScrubRadius *
                math.sin(math.radians(config.KPI));

            return pneumaticTrail + mechanicalTrail;
        }

        [BurstCompile]
        public static float CalculateVariableRatio(
            float steeringAngle,
            float baseRatio,
            float ratioGain)
        {
            return baseRatio + ratioGain * math.abs(steeringAngle);
        }

        [BurstCompile]
        public static float CalculateAckermann(
            float steerAngle,
            float wheelbase,
            float trackWidth)
        {
            if (math.abs(steerAngle) < 0.001f)
                return steerAngle;

            float turnRadius = wheelbase / math.tan(math.abs(steerAngle));
            float innerAngle = math.atan(wheelbase / (turnRadius - trackWidth * 0.5f));
            float outerAngle = math.atan(wheelbase / (turnRadius + trackWidth * 0.5f));

            return steerAngle > 0 ? innerAngle : -innerAngle;
        }
    }
}
