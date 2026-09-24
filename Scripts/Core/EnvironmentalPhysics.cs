using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Environmental physics: air density, humidity, wind field.
    /// 
    /// Air Density Variation:
    ///   ρ = P_atm / (R_specific × T_absolute)
    ///   
    ///   Altitude effect:
    ///   P_atm(h) = 101325 × (1 - 2.25577e-5 × h)^5.25588
    ///   
    ///   Example:
    ///     Sea level, 15°C: ρ = 1.225 kg/m³
    ///     1000m, 15°C:     ρ = 1.112 kg/m³ (-9.2%)
    ///     2000m, 15°C:     ρ = 1.007 kg/m³ (-17.8%)
    /// 
    /// Humidity effect:
    ///   Humid air is LESS dense than dry air
    ///   ρ_humid = ρ_dry × (1 - 0.378 × e / P_atm)
    /// 
    /// Wind Field:
    ///   wind(x, y, z) = V_mean + V_gust(t) + V_local(x,y,z)
    /// </summary>

    public struct EnvironmentConfig
    {
        public float Altitude;
        public float AmbientTemperature;
        public float Humidity;
        public float WindSpeed;
        public float WindHeading;
        public float TurbulenceIntensity;

        public static EnvironmentConfig Default()
        {
            return new EnvironmentConfig
            {
                Altitude = 0f,
                AmbientTemperature = 288.15f,
                Humidity = 0.5f,
                WindSpeed = 0f,
                WindHeading = 0f,
                TurbulenceIntensity = 0.1f
            };
        }
    }

    public struct EnvironmentState
    {
        public float AirDensity;
        public float AtmosphericPressure;
        public float3 WindVector;
        public float TemperatureCelsius;
    }

    public static class EnvironmentalPhysics
    {
        public const float R_SPECIFIC = 287.058f;
        public const float GRAVITY = 9.80665f;
        public const float LAPSE_RATE = 0.0065f;

        public static EnvironmentState Update(
            in EnvironmentConfig config,
            float time)
        {
            EnvironmentState state = new EnvironmentState();

            state.TemperatureCelsius = config.AmbientTemperature - 273.15f;

            state.AtmosphericPressure = CalculatePressure(config.Altitude, config.AmbientTemperature);
            state.AirDensity = CalculateDensity(state.AtmosphericPressure, config.AmbientTemperature);
            state.AirDensity *= CalculateHumidityCorrection(config.Humidity, config.AmbientTemperature, state.AtmosphericPressure);

            float gustX = math.sin(time * 0.5f) * config.WindSpeed * config.TurbulenceIntensity;
            float gustZ = math.cos(time * 0.7f + 1.5f) * config.WindSpeed * config.TurbulenceIntensity;

            float headingRad = math.radians(config.WindHeading);
            state.WindVector = new float3(
                math.cos(headingRad) * config.WindSpeed + gustX,
                0f,
                math.sin(headingRad) * config.WindSpeed + gustZ);

            return state;
        }

        public static float CalculatePressure(float altitude, float tempSeaLevel)
        {
            float tempAtAltitude = tempSeaLevel - LAPSE_RATE * altitude;
            float tempRatio = tempAtAltitude / tempSeaLevel;
            return 101325f * math.pow(tempRatio, GRAVITY / (R_SPECIFIC * LAPSE_RATE));
        }

        public static float CalculateDensity(float pressure, float temperature)
        {
            return pressure / (R_SPECIFIC * temperature);
        }

        public static float CalculateHumidityCorrection(float humidity, float temperature, float pressure)
        {
            float satVaporPressure = 610.78f * math.exp(17.27f * (temperature - 273.15f) / (temperature - 35.86f));
            float vaporPressure = humidity * satVaporPressure;
            return 1f - 0.378f * vaporPressure / pressure;
        }

        public static float3 CalculateWindAtPosition(
            in float3 position,
            in EnvironmentState env,
            float time)
        {
            float localVariation = math.sin(position.x * 0.01f + time * 0.1f) * 0.2f;
            return env.WindVector * (1f + localVariation);
        }

        public static float CalculateAltitudeEffect(float altitude)
        {
            float pressure = CalculatePressure(altitude, 288.15f);
            return pressure / 101325f;
        }
    }
}
