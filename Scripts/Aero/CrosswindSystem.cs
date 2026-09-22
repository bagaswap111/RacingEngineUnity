using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Aero
{
    /// <summary>
    /// Crosswind simulation with mean wind, gusts, and turbulence.
    /// Calculates side force and yaw moment from wind.
    /// </summary>
    [BurstCompile]
    public static class CrosswindSystem
    {
        [BurstCompile]
        public static CrosswindResult Calculate(
            float3 vehiclePosition,
            float3 vehicleVelocity,
            float3 vehicleForward,
            float windSpeed,
            float3 windDirection,
            float time,
            in AeroConfig config)
        {
            CrosswindResult result = new CrosswindResult();

            float3 gust = float3.zero;
            gust.x = math.sin(time * config.GustFrequency * 6.28f) * config.GustAmplitude;
            gust.z = math.cos(time * config.GustFrequency * 7.3f + 1.5f) * config.GustAmplitude * 0.7f;

            float noiseX = Mathf.PerlinNoise(vehiclePosition.x * 0.01f + time * 0.1f, time * 0.2f);
            float noiseZ = Mathf.PerlinNoise(vehiclePosition.z * 0.01f + time * 0.15f, time * 0.25f + 50f);
            float3 turbulence = new float3(
                (noiseX - 0.5f) * 2f,
                0f,
                (noiseZ - 0.5f) * 2f
            ) * config.TurbulenceIntensity * config.GustAmplitude;

            float3 windVector = windDirection * windSpeed + gust + turbulence;
            result.WindVector = windVector;

            float3 relWind = windVector;
            float relSpeed = math.length(relWind);

            if (relSpeed < 0.1f)
                return result;

            float3 relWindLocal = relWind;
            float effectiveYaw = math.atan2(relWindLocal.z, math.abs(relWindLocal.x));
            result.EffectiveYawAngle = effectiveYaw;

            float sideCoeff = config.CsBase * (1f + math.abs(effectiveYaw) * config.CrosswindSensitivity);
            float yawCoeff = config.CyawBase * math.sin(effectiveYaw * 2f);

            float q = 0.5f * config.AirDensitySeaLevel * relSpeed * relSpeed;

            result.SideForce = q * sideCoeff * config.SideArea * math.sign(effectiveYaw);
            result.YawMoment = q * yawCoeff * config.SideArea * config.Wheelbase * math.sign(effectiveYaw);

            return result;
        }

        [BurstCompile]
        public static float3 GetWindDirection(float windHeadingDeg)
        {
            float rad = math.radians(windHeadingDeg);
            return new float3(math.cos(rad), 0f, math.sin(rad));
        }
    }
}
