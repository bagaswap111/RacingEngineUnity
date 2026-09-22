using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Bushing compliance model.
    /// Simulates rubber bushing deflection at pivot points,
    /// affecting camber and toe under load.
    /// </summary>
    [BurstCompile]
    public static class BushingCompliance
    {
        private const float CAMBER_COMPLIANCE_FACTOR = 0.02f;
        private const float TOE_COMPLIANCE_FACTOR = 0.015f;

        [BurstCompile]
        public static float3 CalculateDeflection(
            float lateralForce,
            float longitudinalForce,
            float radialStiffness,
            float axialStiffness,
            float complianceFactor)
        {
            float radialDeflection = 0f;
            if (radialStiffness > 0.001f)
            {
                radialDeflection = (lateralForce / radialStiffness) * complianceFactor;
            }

            float axialDeflection = 0f;
            if (axialStiffness > 0.001f)
            {
                axialDeflection = (longitudinalForce / axialStiffness) * complianceFactor;
            }

            return new float3(radialDeflection, 0f, axialDeflection);
        }

        [BurstCompile]
        public static float CalculateCamberCompliance(
            float lateralForce,
            float radialStiffness,
            float complianceFactor)
        {
            if (radialStiffness < 0.001f) return 0f;

            float deflection = lateralForce / radialStiffness;
            return deflection * CAMBER_COMPLIANCE_FACTOR * complianceFactor;
        }

        [BurstCompile]
        public static float CalculateToeCompliance(
            float longitudinalForce,
            float axialStiffness,
            float complianceFactor)
        {
            if (axialStiffness < 0.001f) return 0f;

            float deflection = longitudinalForce / axialStiffness;
            return deflection * TOE_COMPLIANCE_FACTOR * complianceFactor;
        }

        [BurstCompile]
        public static float ApplyHysteresis(
            float currentDeflection,
            float previousDeflection,
            float force,
            float previousForce,
            float loadingStiffness,
            float unloadingStiffness,
            float hysteresisOffset)
        {
            bool isIncreasing = math.abs(force) > math.abs(previousForce);

            float stiffness = isIncreasing ? loadingStiffness : unloadingStiffness;
            if (stiffness < 0.001f) return currentDeflection;

            float targetDeflection = force / stiffness;
            if (!isIncreasing)
            {
                targetDeflection += hysteresisOffset * math.sign(force);
            }

            return math.lerp(currentDeflection, targetDeflection, 0.1f);
        }

        [BurstCompile]
        public static void ApplyCompliance(
            ref float camber,
            ref float toe,
            float lateralForce,
            float longitudinalForce,
            float radialStiffness,
            float axialStiffness,
            float complianceFactor,
            float camberComplianceFactor,
            float toeComplianceFactor)
        {
            if (complianceFactor < 0.001f) return;

            float deltaCamber = CalculateCamberCompliance(lateralForce, radialStiffness, complianceFactor);
            float deltaToe = CalculateToeCompliance(longitudinalForce, axialStiffness, complianceFactor);

            camber += deltaCamber * camberComplianceFactor;
            toe += deltaToe * toeComplianceFactor;
        }
    }
}
