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
            in TirePressureState state,
            in TirePressureConfig config,
            float verticalLoad,
            float tireTemperature,
            float dt)
        {
            TirePressureState result = state;
            result.Temperature = tireTemperature;

            if (result.Punctured)
            {
                result.PunctureTimer += dt;
                float punctureFactor = math.exp(-config.PunctureDecayRate * result.PunctureTimer);
                result.Pressure = config.ColdPressure * punctureFactor * 0.1f;
                result.GripMultiplier = 0.15f;
                result.ContactPatchArea = verticalLoad / math.max(result.Pressure, 10f);
                return result;
            }

            result.Pressure = config.ColdPressure * (result.Temperature / config.ColdTemperature);
            result.Pressure = math.clamp(result.Pressure, config.MinPressure, config.MaxPressure);

            float pressureRatio = result.Pressure / config.ColdPressure;
            result.GripMultiplier = 1f - (pressureRatio - 1f) * config.PressureGripFactor;
            result.GripMultiplier = math.clamp(result.GripMultiplier, 0.7f, 1.1f);

            result.ContactPatchArea = verticalLoad / math.max(result.Pressure, 1f);

            return result;
        }

        [BurstCompile]
        public static TirePressureState CreatePuncture(
            in TirePressureState state,
            in TirePressureConfig config)
        {
            TirePressureState result = state;
            result.Punctured = true;
            result.PunctureTimer = 0f;
            return result;
        }

        [BurstCompile]
        public static float CalculatePressureEffect(
            float currentPressure,
            float referencePressure)
        {
            if (referencePressure < 1f) return 1f;
            float ratio = currentPressure / referencePressure;
            return 1f - (ratio - 1f) * 0.15f;
        }
    }
}
