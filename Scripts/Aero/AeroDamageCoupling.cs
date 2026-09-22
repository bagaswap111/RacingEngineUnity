using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Aero
{
    /// <summary>
    /// Couples visual damage from the hybrid deformation engine
    /// to aerodynamic coefficients. Handles front/rear/wing/underbody/side damage.
    /// </summary>
    [BurstCompile]
    public static class AeroDamageCoupling
    {
        [BurstCompile]
        public static AeroDamageResult Calculate(
            float frontDamage,
            float rearDamage,
            float sideDamage,
            float underbodyDamage,
            float frontWingDamage,
            float rearWingDamage)
        {
            AeroDamageResult result = new AeroDamageResult();

            result.DragIncrease = frontDamage * 0.12f + rearDamage * 0.10f;
            result.FrontDownLoss = frontDamage * 0.35f + frontWingDamage * 0.50f;
            result.RearDownLoss = rearDamage * 0.40f + rearWingDamage * 0.50f;
            result.SideSensitivity = sideDamage * 0.30f;
            result.GroundEffectLoss = underbodyDamage * 0.40f;
            result.DRSDisabled = rearWingDamage > 0.5f;

            return result;
        }

        [BurstCompile]
        public static void ApplyDamage(
            ref float Cd,
            ref float ClFront,
            ref float ClRear,
            ref float Cs,
            in AeroDamageResult damage)
        {
            Cd *= (1f + damage.DragIncrease);
            ClFront *= (1f - damage.FrontDownLoss);
            ClRear *= (1f - damage.RearDownLoss);
            Cs *= (1f + damage.SideSensitivity);
        }

        [BurstCompile]
        public static float ApplyGroundEffectDamage(
            float geContribution,
            in AeroDamageResult damage)
        {
            return geContribution * (1f - damage.GroundEffectLoss);
        }
    }
}
