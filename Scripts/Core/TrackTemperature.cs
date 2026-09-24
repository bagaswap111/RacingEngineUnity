using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Track surface temperature and grip evolution.
    /// 
    /// Track Temperature Model:
    ///   T_track changes with ambient temp, sun exposure,
    ///   rubber buildup, and rain.
    ///   
    ///   T_track += (T_ambient - T_track) × heat_transfer_rate × dt
    ///   T_track += sun_intensity × absorption_rate × dt
    ///   T_track -= rain_intensity × cooling_rate × dt
    /// 
    /// Grip Evolution (Rubbering In):
    ///   rubber_level += tire_wear × rubber_deposit_rate × dt
    ///   grip_multiplier = 1.0 + rubber_level × grip_gain
    ///   
    ///   Racing line gets grippier over session.
    ///   Off-line stays at base grip.
    /// </summary>
    [BurstCompile]
    public struct TrackTempConfig
    {
        public float AmbientTemperature;
        public float SunIntensity;
        public float AbsorptionRate;
        public float HeatTransferRate;
        public float RainIntensity;
        public float CoolingRate;
        public float RubberDepositRate;
        public float GripGain;
        public float MaxRubberLevel;

        public static TrackTempConfig Default()
        {
            return new TrackTempConfig
            {
                AmbientTemperature = 25f,
                SunIntensity = 0.8f,
                AbsorptionRate = 0.01f,
                HeatTransferRate = 0.005f,
                RainIntensity = 0f,
                CoolingRate = 0.02f,
                RubberDepositRate = 0.001f,
                GripGain = 0.1f,
                MaxRubberLevel = 1.0f
            };
        }
    }

    [BurstCompile]
    public struct TrackTempState
    {
        public float SurfaceTemperature;
        public float RubberLevel;
        public float GripMultiplier;
        public bool IsRaining;
    }

    [BurstCompile]
    public static class TrackTemperature
    {
        [BurstCompile]
        public static TrackTempState Update(
            in TrackTempState state,
            in TrackTempConfig config,
            float tireWear,
            float dt)
        {
            TrackTempState result = state;
            float heatGain = (config.AmbientTemperature - result.SurfaceTemperature)
                           * config.HeatTransferRate * dt;

            float sunGain = config.SunIntensity * config.AbsorptionRate * dt;

            float rainCooling = 0f;
            if (config.RainIntensity > 0f)
            {
                rainCooling = config.RainIntensity * config.CoolingRate * dt;
                result.IsRaining = true;
            }
            else
            {
                result.IsRaining = false;
            }

            result.SurfaceTemperature += heatGain + sunGain - rainCooling;
            result.SurfaceTemperature = math.clamp(result.SurfaceTemperature, -10f, 80f);

            result.RubberLevel += tireWear * config.RubberDepositRate * dt;
            result.RubberLevel = math.clamp(result.RubberLevel, 0f, config.MaxRubberLevel);

            result.GripMultiplier = 1f + result.RubberLevel * config.GripGain;

            return result;
        }

        [BurstCompile]
        public static float CalculateGripFromTemperature(
            float surfaceTemp,
            float optimalTemp,
            float sensitivity)
        {
            float tempDiff = math.abs(surfaceTemp - optimalTemp);
            return 1f - tempDiff * sensitivity;
        }

        [BurstCompile]
        public static float CalculateRubberGrip(
            float rubberLevel,
            float gripGain)
        {
            return 1f + rubberLevel * gripGain;
        }

        [BurstCompile]
        public static float CalculateWetGripPenalty(
            float rainIntensity,
            float wetGripFactor)
        {
            return 1f - rainIntensity * (1f - wetGripFactor);
        }
    }
}
