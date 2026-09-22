using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Aero
{
    /// <summary>
    /// Slipstream and wake turbulence system.
    /// Calculates drag reduction and downforce loss when following another vehicle.
    /// </summary>
    [BurstCompile]
    public static class SlipstreamSystem
    {
        [BurstCompile]
        public static SlipstreamResult Calculate(
            float3 egoPosition,
            float3 egoForward,
            float3[] otherPositions,
            float3[] otherVelocities,
            float[] otherSpeeds,
            in AeroConfig config)
        {
            SlipstreamResult result = new SlipstreamResult();
            result.DragMultiplier = 1f;
            result.DownforceMultiplier = 1f;
            result.TurbulenceIntensity = 0f;
            result.TotalVelocityDeficit = 0f;

            if (otherPositions == null || otherPositions.Length == 0)
                return result;

            float totalVelocityDeficit = 0f;
            float maxTurbulence = 0f;

            for (int i = 0; i < otherPositions.Length; i++)
            {
                float3 toOther = otherPositions[i] - egoPosition;
                float distance = math.length(toOther);

                if (distance > config.WakeLength || distance < 0.1f)
                    continue;

                float forwardDot = math.dot(math.normalize(toOther), egoForward);
                if (forwardDot < 0.3f)
                    continue;

                float3 toOtherNorm = math.normalize(toOther);
                float3 right = math.cross(egoForward, new float3(0, 1, 0));
                float lateralOffset = math.abs(math.dot(toOtherNorm, right));
                float lateralOffsetM = lateralOffset * distance;

                float wakeWidth = config.CarWidth * (1f + distance * config.WakeExpansionRate);
                if (lateralOffsetM > wakeWidth)
                    continue;

                float distanceFactor = 1f - (distance / config.WakeLength);
                distanceFactor = math.pow(distanceFactor, 1.5f);

                float lateralFactor = 1f - (lateralOffsetM / wakeWidth);
                lateralFactor = math.pow(lateralFactor, 2f);

                float velocityDeficit = config.MaxVelocityDeficit * distanceFactor * lateralFactor;
                totalVelocityDeficit += velocityDeficit;

                float turbulence = config.BaseTurbulence * distanceFactor;
                maxTurbulence = math.max(maxTurbulence, turbulence);
            }

            totalVelocityDeficit = math.clamp(totalVelocityDeficit, 0f, 0.6f);

            result.TotalVelocityDeficit = totalVelocityDeficit;
            result.DragMultiplier = 1f - totalVelocityDeficit * config.DragReductionFactor;
            result.DownforceMultiplier = 1f - totalVelocityDeficit * config.DownforceReductionFactor;
            result.TurbulenceIntensity = maxTurbulence;
            result.SlipstreamActive = totalVelocityDeficit > 0.01f;

            return result;
        }

        [BurstCompile]
        public static float3 CalculateTurbulenceForce(
            float turbulenceIntensity,
            float dynamicPressure,
            float frontalArea,
            float time)
        {
            if (turbulenceIntensity < 0.01f)
                return float3.zero;

            float turbForce = turbulenceIntensity * dynamicPressure * frontalArea * 0.01f;

            float fx = math.sin(time * 7.3f) * turbForce * 0.3f;
            float fy = math.sin(time * 11.1f + 100f) * turbForce * 0.5f;
            float fz = math.sin(time * 5.7f + 200f) * turbForce * 0.2f;

            return new float3(fx, fy, fz);
        }
    }
}
