using Unity.Burst;
using Unity.Mathematics;
using RacingSim.Core;

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
                WheelRadius = VehicleConstants.DEFAULT_WHEEL_RADIUS
            };
        }
    }

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

    public static class DrivelineOscillation
    {

        public static DrivelineState Update(
            in DrivelineState state,
            in DrivelineConfig config,
            float engineTorque,
            float loadTorque,
            float dt)
        {
            DrivelineState result = state;
            float twistAngle = result.EngineAngle - result.WheelAngle;
            float twistVelocity = result.EngineAngularVelocity - result.WheelAngularVelocity;

            float springTorque = config.DrivelineStiffness * twistAngle;
            float dampingTorque = config.DrivelineDamping * twistVelocity;

            result.DrivelineTorque = springTorque + dampingTorque;

            float engineAccel = (engineTorque - result.DrivelineTorque) / config.EngineInertia;
            result.EngineAngularVelocity += engineAccel * dt;
            result.EngineAngle += result.EngineAngularVelocity * dt;

            float wheelAccel = (result.DrivelineTorque - loadTorque) / config.WheelInertia;
            result.WheelAngularVelocity += wheelAccel * dt;
            result.WheelAngle += result.WheelAngularVelocity * dt;

            result.DrivelineTwist = twistAngle;

            result.VerticalReactionForce = result.DrivelineTorque
                * math.tan(config.DriveshaftAngle) / config.WheelRadius;

            return result;
        }

        public static float CalculateWheelHopFrequency(in DrivelineConfig config)
        {
            float effectiveInertia = (config.EngineInertia * config.WheelInertia)
                                   / (config.EngineInertia + config.WheelInertia);
            return math.sqrt(config.DrivelineStiffness / effectiveInertia) / (2f * math.PI);
        }

        public static float CalculateDrivelineDampingRatio(in DrivelineConfig config)
        {
            float effectiveInertia = (config.EngineInertia * config.WheelInertia)
                                   / (config.EngineInertia + config.WheelInertia);
            float criticalDamping = 2f * math.sqrt(config.DrivelineStiffness * effectiveInertia);
            return config.DrivelineDamping / criticalDamping;
        }

        public static float CalculateVerticalReaction(
            float drivelineTorque,
            float driveshaftAngle,
            float wheelRadius)
        {
            return drivelineTorque * math.tan(driveshaftAngle) / wheelRadius;
        }
    }
}
