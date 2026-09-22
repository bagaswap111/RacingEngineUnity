using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Tire pressure dynamics based on ideal gas law.
    /// 
    /// P × V = n × R × T
    /// P_actual = P_cold × (T_actual / T_cold)
    /// 
    /// Pressure effects:
    /// - Higher pressure → smaller contact patch → less grip
    /// - Lower pressure → larger contact patch → more grip
    /// - Too low → tire overheating, shoulder wear
    /// - Too high → center wear, reduced grip
    /// 
    /// Puncture model:
    ///   P_tire → 0 over 1-3 seconds
    ///   grip → 10-20% of normal
    /// </summary>
    [BurstCompile]
    public struct TirePressureConfig
    {
        public float ColdPressure;
        public float ColdTemperature;
        public float MaxPressure;
        public float MinPressure;
        public float PressureGripFactor;
        public float PunctureDecayRate;

        public static TirePressureConfig Default()
        {
            return new TirePressureConfig
            {
                ColdPressure = 200f,
                ColdTemperature = 298f,
                MaxPressure = 280f,
                MinPressure = 120f,
                PressureGripFactor = 0.15f,
                PunctureDecayRate = 0.5f
            };
        }
    }

    [BurstCompile]
    public struct TirePressureState
    {
        public float Pressure;
        public float Temperature;
        public float ContactPatchArea;
        public float GripMultiplier;
        public bool Punctured;
        public float PunctureTimer;
    }

    [BurstCompile]
    public static class TirePressureDynamics
    {
        [BurstCompile]
        public static TirePressureState Update(
            TirePressureState state,
            TirePressureConfig config,
            float verticalLoad,
            float tireTemperature,
            float dt)
        {
            state.Temperature = tireTemperature;

            if (state.Punctured)
            {
                state.PunctureTimer += dt;
                float punctureFactor = math.exp(-config.PunctureDecayRate * state.PunctureTimer);
                state.Pressure = config.ColdPressure * punctureFactor * 0.1f;
                state.GripMultiplier = 0.15f;
                state.ContactPatchArea = verticalLoad / math.max(state.Pressure, 10f);
                return state;
            }

            state.Pressure = config.ColdPressure * (state.Temperature / config.ColdTemperature);
            state.Pressure = math.clamp(state.Pressure, config.MinPressure, config.MaxPressure);

            float pressureRatio = state.Pressure / config.ColdPressure;
            state.GripMultiplier = 1f - (pressureRatio - 1f) * config.PressureGripFactor;
            state.GripMultiplier = math.clamp(state.GripMultiplier, 0.7f, 1.1f);

            state.ContactPatchArea = verticalLoad / state.Pressure;

            return state;
        }

        [BurstCompile]
        public static TirePressureState CreatePuncture(
            TirePressureState state,
            TirePressureConfig config)
        {
            state.Punctured = true;
            state.PunctureTimer = 0f;
            return state;
        }

        [BurstCompile]
        public static float CalculatePressureEffect(
            float currentPressure,
            float referencePressure)
        {
            float ratio = currentPressure / referencePressure;
            return 1f - (ratio - 1f) * 0.15f;
        }
    }
}
