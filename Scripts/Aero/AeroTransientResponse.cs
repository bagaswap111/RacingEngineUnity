using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Aero
{
    /// <summary>
    /// Aero transient response model.
    /// 
    /// Real aero has response time, not instant.
    /// When ride height changes suddenly (over kerb),
    /// downforce doesn't change instantly.
    /// 
    /// F_down_actual += (F_down_steady - F_down_actual) × (dt / τ_aero)
    /// 
    /// Different τ for different components:
    ///   Front wing: τ ≈ 0.05 s
    ///   Rear wing: τ ≈ 0.08 s
    ///   Ground effect: τ ≈ 0.15 s
    ///   Diffuser: τ ≈ 0.20 s
    /// </summary>
    [BurstCompile]
    public struct AeroTransientConfig
    {
        public float FrontWingTau;
        public float RearWingTau;
        public float GroundEffectTau;
        public float DiffuserTau;

        public static AeroTransientConfig Default()
        {
            return new AeroTransientConfig
            {
                FrontWingTau = 0.05f,
                RearWingTau = 0.08f,
                GroundEffectTau = 0.15f,
                DiffuserTau = 0.20f
            };
        }
    }

    [BurstCompile]
    public struct AeroTransientState
    {
        public float FrontDownforceActual;
        public float RearDownforceActual;
        public float GroundEffectActual;
        public float DiffuserActual;
    }

    [BurstCompile]
    public static class AeroTransientResponse
    {
        [BurstCompile]
        public static AeroTransientState Update(
            AeroTransientState state,
            AeroTransientConfig config,
            float frontDownforceSteady,
            float rearDownforceSteady,
            float groundEffectSteady,
            float diffuserSteady,
            float dt)
        {
            state.FrontDownforceActual = SmoothToward(
                state.FrontDownforceActual, frontDownforceSteady, config.FrontWingTau, dt);

            state.RearDownforceActual = SmoothToward(
                state.RearDownforceActual, rearDownforceSteady, config.RearWingTau, dt);

            state.GroundEffectActual = SmoothToward(
                state.GroundEffectActual, groundEffectSteady, config.GroundEffectTau, dt);

            state.DiffuserActual = SmoothToward(
                state.DiffuserActual, diffuserSteady, config.DiffuserTau, dt);

            return state;
        }

        [BurstCompile]
        private static float SmoothToward(float current, float target, float tau, float dt)
        {
            if (tau < 0.001f)
                return target;

            float alpha = dt / (tau + dt);
            return math.lerp(current, target, math.clamp(alpha, 0f, 1f));
        }

        [BurstCompile]
        public static float CalculateTransientLoss(
            float steadyStateForce,
            float actualForce)
        {
            if (math.abs(steadyStateForce) < 0.001f)
                return 0f;
            return 1f - (actualForce / steadyStateForce);
        }

        [BurstCompile>
        public static float CalculateOscillationRisk(
            float rideHeightChange,
            float speed,
            AeroTransientConfig config)
        {
            float freq = speed / (2f * math.PI * 0.3f);
            float geFreq = 1f / config.GroundEffectTau;
            float resonance = 1f / (1f + math.abs(freq - geFreq) * 0.1f);
            return resonance * math.abs(rideHeightChange);
        }
    }
}
