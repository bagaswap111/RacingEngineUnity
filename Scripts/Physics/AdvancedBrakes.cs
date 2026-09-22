using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Advanced brake system with temperature-dependent friction,
    /// brake fade, disc warping, and bias migration.
    /// 
    /// Pad Friction Curve vs Temperature:
    ///   0-100°C: μ = 0.30 (cold)
    ///   100-300°C: μ = 0.42 (warming up)
    ///   300-600°C: μ = 0.48 (optimal)
    ///   600-800°C: μ = 0.45 (starting to fade)
    ///   800+°C: μ = 0.30 (severe fade)
    /// 
    /// Brake Fade:
    ///   fade_factor = 1.0 - max(0, (T - T_fade_start)) / (T_fade_end - T_fade_start)
    ///   
    /// Brake Disc Warping:
    ///   IF T > T_warp_threshold: warp_amount += warp_rate × dt
    ///   
    /// Brake Bias Migration:
    ///   bias_dynamic = bias_static + load_transfer × migration_factor
    ///   
    /// Brake Fluid Boiling:
    ///   IF T > T_fluid_boil: F_brake *= 0.3
    /// </summary>
    [BurstCompile]
    public struct AdvancedBrakeConfig
    {
        public float DiscRadius;
        public float PadRadius;
        public float NumPads;
        public float DiscMass;
        public float PadMass;
        public float SpecificHeat;
        public float CoolingRate;
        public float FadeStartTemp;
        public float FadeEndTemp;
        public float WarpThreshold;
        public float WarpRate;
        public float FluidBoilTemp;
        public float StaticBias;
        public float BiasMigrationFactor;
        public float MaxBrakeTorque;

        public static AdvancedBrakeConfig Default()
        {
            return new AdvancedBrakeConfig
            {
                DiscRadius = 0.15f,
                PadRadius = 0.12f,
                NumPads = 6,
                DiscMass = 4f,
                PadMass = 1f,
                SpecificHeat = 500f,
                CoolingRate = 0.02f,
                FadeStartTemp = 600f,
                FadeEndTemp = 800f,
                WarpThreshold = 700f,
                WarpRate = 0.001f,
                FluidBoilTemp = 310f,
                StaticBias = 0.6f,
                BiasMigrationFactor = 0.1f,
                MaxBrakeTorque = 4000f
            };
        }
    }

    [BurstCompile]
    public struct AdvancedBrakeState
    {
        public float Temperature;
        public float FrictionCoeff;
        public float FadeFactor;
        public float WarpAmount;
        public float FluidTemperature;
        public bool FluidBoiled;
        public float BrakeTorque;
        public float DynamicBias;
    }

    [BurstCompile]
    public static class AdvancedBrakes
    {
        [BurstCompile]
        public static AdvancedBrakeState Update(
            AdvancedBrakeState state,
            AdvancedBrakeConfig config,
            float brakeInput,
            float verticalLoad,
            float loadTransfer,
            float speed,
            float dt)
        {
            float brakeTorque = brakeInput * config.MaxBrakeTorque;
            float frictionTorque = brakeTorque * state.FrictionCoeff;

            float heatGen = frictionTorque * speed * 0.001f;
            float cooling = config.CoolingRate * (state.Temperature - 25f) * dt;
            state.Temperature += (heatGen - cooling) * dt;
            state.Temperature = math.max(state.Temperature, 25f);

            state.FrictionCoeff = CalculateFrictionCoefficient(state.Temperature);
            state.FadeFactor = CalculateFadeFactor(state.Temperature, config);
            state.WarpAmount = CalculateWarp(state.WarpAmount, state.Temperature, config, dt);

            state.FluidTemperature = state.Temperature * 0.4f;
            state.FluidBoiled = state.FluidTemperature > config.FluidBoilTemp;

            if (state.FluidBoiled)
                brakeTorque *= 0.3f;

            state.DynamicBias = config.StaticBias + loadTransfer * config.BiasMigrationFactor;
            state.DynamicBias = math.clamp(state.DynamicBias, 0.4f, 0.7f);

            state.BrakeTorque = brakeTorque * state.FrictionCoeff * state.FadeFactor;

            return state;
        }

        [BurstCompile]
        private static float CalculateFrictionCoefficient(float temperature)
        {
            if (temperature < 100f) return 0.30f;
            if (temperature < 300f) return 0.30f + (temperature - 100f) * 0.0006f;
            if (temperature < 600f) return 0.42f + (temperature - 300f) * 0.0002f;
            if (temperature < 800f) return 0.48f - (temperature - 600f) * 0.00015f;
            return 0.30f;
        }

        [BurstCompile]
        private static float CalculateFadeFactor(float temperature, AdvancedBrakeConfig config)
        {
            if (temperature < config.FadeStartTemp)
                return 1.0f;

            float fade = 1.0f - (temperature - config.FadeStartTemp)
                       / (config.FadeEndTemp - config.FadeStartTemp);
            return math.clamp(fade, 0.2f, 1.0f);
        }

        [BurstCompile]
        private static float CalculateWarp(
            float currentWarp,
            float temperature,
            AdvancedBrakeConfig config,
            float dt)
        {
            if (temperature < config.WarpThreshold)
                return currentWarp * 0.99f;

            return currentWarp + config.WarpRate * dt;
        }

        [BurstCompile]
        public static float CalculatePulsation(
            float warpAmount,
            float wheelAngle,
            int discOrder)
        {
            return warpAmount * math.sin(wheelAngle * discOrder);
        }

        [BurstCompile]
        public static float CalculateBrakeBias(
            float staticBias,
            float loadTransfer,
            float migrationFactor)
        {
            return math.clamp(staticBias + loadTransfer * migrationFactor, 0.4f, 0.7f);
        }
    }
}
