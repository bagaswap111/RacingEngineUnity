using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Chassis torsional flex model.
    /// 
    /// Real chassis flexes under load. This affects:
    /// - Suspension geometry (camber/toe under load)
    /// - Effective spring rate
    /// - Vehicle feel over kerbs
    /// 
    /// Torsional stiffness:
    ///   Road car: 10,000-20,000 N·m/rad
    ///   GT3 car: 25,000-40,000 N·m/rad
    ///   F1 car: 50,000+ N·m/rad
    /// 
    /// Implementation:
    ///   φ_chassis = T_roll / K_torsional
    ///   flex_offset = φ_chassis × suspension_arm_length
    ///   camber_actual += flex_offset × camberFlexFactor
    ///   toe_actual += flex_offset × toeFlexFactor
    /// </summary>
    [BurstCompile]
    public struct ChassisFlexConfig
    {
        public float TorsionalStiffness;
        public float FlexDamping;
        public float ArmLength;
        public float CamberFlexFactor;
        public float ToeFlexFactor;

        public static ChassisFlexConfig Default()
        {
            return new ChassisFlexConfig
            {
                TorsionalStiffness = 30000f,
                FlexDamping = 500f,
                ArmLength = 0.4f,
                CamberFlexFactor = 0.3f,
                ToeFlexFactor = 0.15f
            };
        }
    }

    [BurstCompile]
    public struct ChassisFlexState
    {
        public float TorsionAngle;
        public float TorsionVelocity;
        public float CamberOffset;
        public float ToeOffset;
    }

    [BurstCompile]
    public static class ChassisTorsionalFlex
    {
        [BurstCompile]
        public static ChassisFlexState Update(
            in ChassisFlexState state,
            in ChassisFlexConfig config,
            float rollTorque,
            float dt)
        {
            ChassisFlexState result = state;
            float restoringTorque = config.TorsionalStiffness * result.TorsionAngle;
            float dampingTorque = config.FlexDamping * result.TorsionVelocity;

            float inertia = config.TorsionalStiffness * 0.0001f;
            float angularAccel = inertia > 0.0001f
                ? (rollTorque - restoringTorque - dampingTorque) / inertia
                : 0f;

            result.TorsionVelocity += angularAccel * dt;
            result.TorsionAngle += result.TorsionVelocity * dt;

            result.TorsionAngle = math.clamp(result.TorsionAngle, -0.1f, 0.1f);

            float flexOffset = result.TorsionAngle * config.ArmLength;
            result.CamberOffset = flexOffset * config.CamberFlexFactor;
            result.ToeOffset = flexOffset * config.ToeFlexFactor;

            return result;
        }

        [BurstCompile]
        public static float CalculateEffectiveSpringRate(
            float baseSpringRate,
            in ChassisFlexConfig config)
        {
            float flexCompliance = 1f / config.TorsionalStiffness;
            float effectiveCompliance = 1f / baseSpringRate + flexCompliance;
            return 1f / effectiveCompliance;
        }

        [BurstCompile]
        public static float CalculateChassisFrequency(
            in ChassisFlexConfig config,
            float vehicleMass)
        {
            return math.sqrt(config.TorsionalStiffness / (vehicleMass * 0.1f))
                 / (2f * math.PI);
        }
    }
}
