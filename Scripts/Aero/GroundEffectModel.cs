using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Aero
{
    /// <summary>
    /// Ground effect aerodynamics model.
    /// Calculates additional downforce from venturi effect and diffuser.
    /// Includes pitch sensitivity, yaw degradation, and stall detection.
    /// </summary>

    public static class GroundEffectModel
    {

        public static GroundEffectResult Calculate(
            float rideHeightFront,
            float rideHeightRear,
            float pitch,
            float yawAngle,
            float brakeDuctOpening,
            in AeroConfig config)
        {
            GroundEffectResult result = new GroundEffectResult();

            float rhNormF = config.RHReference > 0.001f ? rideHeightFront / config.RHReference : 1f;
            float rhNormR = config.RHReference > 0.001f ? rideHeightRear / config.RHReference : 1f;

            float fHeightF = CalculateHeightFactor(rhNormF);
            float fHeightR = CalculateHeightFactor(rhNormR);

            float fPitchF = math.clamp(1f - pitch * config.PitchSensitivityFront, 0f, 2f);
            float fPitchR = math.clamp(1f + pitch * config.PitchSensitivityRear, 0f, 2.5f);

            float fYaw = math.clamp(1f - math.abs(yawAngle) * config.YawGroundEffectLoss, 0.2f, 1f);

            result.HeightFactorFront = fHeightF;
            result.HeightFactorRear = fHeightR;

            result.FrontContribution = config.ClGroundEffectFront * fHeightF * fPitchF * fYaw;
            result.RearContribution = config.ClGroundEffectRear * fHeightR * fPitchR * fYaw;

            result.DiffuserStalled = false;
            if (pitch > config.DiffuserStallPitch)
            {
                result.RearContribution *= config.DiffuserStallMultiplier;
                result.DiffuserStalled = true;
            }

            result.FrontContribution *= (1f - brakeDuctOpening * 0.10f);
            result.RearContribution *= (1f - brakeDuctOpening * 0.05f);

            return result;
        }

        private static float CalculateHeightFactor(float rhNormalized)
        {
            if (rhNormalized > 1.5f)
                return 0.3f;
            else if (rhNormalized > 0.5f)
                return 1f + (1.5f - rhNormalized) * 1.5f;
            else if (rhNormalized > 0.2f)
                return 2.5f;
            else
                return 2.5f * (rhNormalized / 0.2f);
        }
    }
}
