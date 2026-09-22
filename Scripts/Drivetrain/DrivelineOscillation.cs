using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Drivetrain
{
    /// <summary>
    /// Driveline oscillation and wheel hop modeling.
    /// 
    /// Wheel Hop:
    ///   Under hard acceleration, driveline torque excites suspension.
    ///   
    ///   τ_driveshaft = k_ds × (θ_engine_out - θ_wheel)
    ///                 + c_ds × (ω_engine_out - ω_wheel)
    ///   
    ///   F_vertical_reaction = τ_driveshaft × tan(driveshaft_angle) 
    ///                         / wheel_radius
    /// 
    /// Driveline Oscillation (2-mass model):
    ///   I_1 × ω̇_1 = τ_engine - k × (θ_1 - θ_2) - c × (ω_1 - ω_2)
    ///   I_2 × ω̇_2 = k × (θ_1 - θ_2) + c × (ω_1 - ω_2) - τ_load
    /// </summary>
    [BurstCompile]
    public struct DrivelineConfig
    {
        public float EngineInertia;
        public float WheelInertia;
        public float DrivelineStiffness;
        public float DrivelineDamping;
        public float DriveshaftAngle;
        public float WheelRadius;

        public static DrivelineConfig Default()
        {
            return new DrivelineConfig
            {
                EngineInertia = 0.2f,
                WheelInertia = 1.5f,
                DrivelineStiffness = 5000f,
                DrivelineDamping = 50f,
                DriveshaftAngle = 0.1f,
                WheelRadius = 0.33f
            };
        }
    }

    [BurstCompile]
    public struct DrivelineState
    {
        public float EngineAngle;
        public float EngineAngularVelocity;
        public float WheelAngle;
        public float WheelAngularVelocity;
        public float DrivelineTwist;
        public float DrivelineTorque;
        public float VerticalReactionForce;
    }

    [BurstCompile]
    public static class DrivelineOscillation
    {
        [BurstCompile]
        public static DrivelineState Update(
            DrivelineState state,
            DrivelineConfig config,
            float engineTorque,
            float loadTorque,
            float dt)
        {
            float twistAngle = state.EngineAngle - state.WheelAngle;
            float twistVelocity = state.EngineAngularVelocity - state.WheelAngularVelocity;

            float springTorque = config.DrivelineStiffness * twistAngle;
            float dampingTorque = config.DrivelineDamping * twistVelocity;

            state.DrivelineTorque = springTorque + dampingTorque;

            float engineAccel = (engineTorque - state.DrivelineTorque) / config.EngineInertia;
            state.EngineAngularVelocity += engineAccel * dt;
            state.EngineAngle += state.EngineAngularVelocity * dt;

            float wheelAccel = (state.DrivelineTorque - loadTorque) / config.WheelInertia;
            state.WheelAngularVelocity += wheelAccel * dt;
            state.WheelAngle += state.WheelAngularVelocity * dt;

            state.DrivelineTwist = twistAngle;

            state.VerticalReactionForce = state.DrivelineTorque
                * math.tan(config.DriveshaftAngle) / config.WheelRadius;

            return state;
        }

        [BurstCompile]
        public static float CalculateWheelHopFrequency(DrivelineConfig config)
        {
            float effectiveInertia = (config.EngineInertia * config.WheelInertia)
                                   / (config.EngineInertia + config.WheelInertia);
            return math.sqrt(config.DrivelineStiffness / effectiveInertia) / (2f * math.PI);
        }

        [BurstCompile]
        public static float CalculateDrivelineDampingRatio(DrivelineConfig config)
        {
            float effectiveInertia = (config.EngineInertia * config.WheelInertia)
                                   / (config.EngineInertia + config.WheelInertia);
            float criticalDamping = 2f * math.sqrt(config.DrivelineStiffness * effectiveInertia);
            return config.DrivelineDamping / criticalDamping;
        }

        [BurstCompile]
        public static float CalculateVerticalReaction(
            float drivelineTorque,
            float driveshaftAngle,
            float wheelRadius)
        {
            return drivelineTorque * math.tan(driveshaftAngle) / wheelRadius;
        }
    }
}
