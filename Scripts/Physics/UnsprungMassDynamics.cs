using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Physics
{
    /// <summary>
    /// Unsprung mass dynamics with wheel hop modeling.
    /// 
    /// Current model (simplified):
    ///   Chassis → Spring/Damper → Ground
    /// 
    /// Correct model:
    ///   Chassis → Spring/Damper → Wheel Mass → Tire Spring → Ground
    /// 
    /// The wheel (unsprung mass) is a separate body with its own vertical DOF.
    /// This adds 4 more DOF (one per wheel vertical), making total: 7 DOF
    /// (6 chassis + 4 wheel vertical - 3 constraints).
    /// 
    /// Equations:
    ///   m_unsprung × ÿ_wheel = F_spring + F_damper - F_tire - m_unsprung × g
    ///   F_tire = k_tire × (y_ground - y_wheel) + c_tire × (ẏ_ground - ẏ_wheel)
    ///   
    ///   k_tire ≈ 200-400 N/mm (tire vertical stiffness)
    ///   c_tire ≈ 5-15 N·s/mm (tire damping)
    /// </summary>
    [BurstCompile]
    public struct UnsprungMassState
    {
        public float WheelPosition;
        public float WheelVelocity;
        public float TireDeflection;
        public float ContactForce;
        public bool IsGrounded;
    }

    [BurstCompile]
    public struct UnsprungMassConfig
    {
        public float Mass;
        public float TireStiffness;
        public float TireDamping;
        public float MaxCompression;
        public float MaxDroop;

        public static UnsprungMassConfig Default()
        {
            return new UnsprungMassConfig
            {
                Mass = 18f,
                TireStiffness = 300000f,
                TireDamping = 1500f,
                MaxCompression = 60f,
                MaxDroop = 40f
            };
        }
    }

    [BurstCompile]
    public static class UnsprungMassDynamics
    {
        public const float GRAVITY = 9.81f;

        [BurstCompile]
        public static UnsprungMassState Update(
            UnsprungMassState state,
            UnsprungMassConfig config,
            float chassisVerticalForce,
            float groundHeight,
            float chassisAcceleration,
            float dt)
        {
            float tireForce = 0f;
            state.IsGrounded = false;

            if (state.WheelPosition <= groundHeight + 0.001f)
            {
                state.WheelPosition = groundHeight + 0.001f;
                state.WheelVelocity = math.max(state.WheelVelocity, 0f);
                state.IsGrounded = true;

                float penetration = groundHeight - state.WheelPosition;
                tireForce = config.TireStiffness * (-penetration)
                          + config.TireDamping * (-state.WheelVelocity);

                tireForce = math.max(tireForce, 0f);
            }

            float netForce = chassisVerticalForce
                           - config.Mass * GRAVITY
                           - tireForce;

            float wheelAcceleration = netForce / config.Mass;
            state.WheelVelocity += wheelAcceleration * dt;
            state.WheelPosition += state.WheelVelocity * dt;

            state.WheelPosition = math.clamp(
                state.WheelPosition,
                -config.MaxDroop,
                config.MaxCompression);

            state.ContactForce = tireForce;
            state.TireDeflection = groundHeight - state.WheelPosition;

            return state;
        }

        [BurstCompile]
        public static float CalculateWheelHopFrequency(
            UnsprungMassConfig config,
            float sprungMass,
            float springRate)
        {
            float effectiveSpring = 1f / (1f / springRate + 1f / config.TireStiffness);
            float freq = math.sqrt(effectiveSpring / config.Mass) / (2f * math.PI);
            return freq;
        }

        [BurstCompile]
        public static float CalculateWheelHopDampingRatio(
            UnsprungMassConfig config,
            float damperRate)
        {
            float criticalDamping = 2f * math.sqrt(config.TireStiffness * config.Mass);
            return damperRate / criticalDamping;
        }
    }
}
