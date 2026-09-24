using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Force models for suspension: spring, damper (4-way), ARB, bump stop, droop stop.
    /// All calculations in native C# for performance.
    /// </summary>

    public static class SuspensionForces
    {

        public static float CalculateSpringForce(
            float travel,
            float springRate,
            float preloadTravel,
            float motionRatio,
            bool isProgressive,
            float progressiveRate)
        {
            float springTravel = (travel + preloadTravel) * motionRatio;

            float force;
            if (isProgressive)
            {
                force = springRate * springTravel + progressiveRate * springTravel * springTravel;
            }
            else
            {
                force = springRate * springTravel;
            }

            return force * motionRatio;
        }

        public static float CalculateDamperForce(
            float travelVelocity,
            in DamperConfig config,
            float damperTemperature,
            float damperDamage)
        {
            float vDamper = travelVelocity * config.MotionRatio;
            float vAbs = math.abs(vDamper);
            float force = 0f;

            if (vDamper < 0f)
            {
                if (vAbs < config.KneeVelocityComp)
                {
                    force = config.LSC_Rate * vAbs;
                }
                else
                {
                    float fKnee = config.LSC_Rate * config.KneeVelocityComp;
                    force = fKnee + config.HSC_Rate * (vAbs - config.KneeVelocityComp);
                }

                if (config.IsDigressiveComp)
                {
                    force = math.min(force, config.MaxCompForce);
                }
            }
            else
            {
                if (vAbs < config.KneeVelocityReb)
                {
                    force = config.LSR_Rate * vAbs;
                }
                else
                {
                    float fKnee = config.LSR_Rate * config.KneeVelocityReb;
                    force = fKnee + config.HSR_Rate * (vAbs - config.KneeVelocityReb);
                }

                if (config.IsDigressiveReb)
                {
                    force = math.min(force, config.MaxRebForce);
                }
            }

            float tempFactor = 1f + (damperTemperature - 40f) * -0.003f;
            force *= tempFactor;

            if (damperDamage > 0f)
            {
                force *= (1f - damperDamage * 0.5f);
            }

            float direction = vDamper < 0f ? 1f : -1f;
            return force * direction * config.MotionRatio;
        }

        public static float CalculateARBForce(
            float travel,
            float oppositeTravel,
            float arbRate,
            float arbPreload,
            float arbRatio)
        {
            float travelDiff = travel - oppositeTravel;
            return arbRate * travelDiff * arbRatio + arbPreload;
        }

        public static float CalculateBumpStopForce(
            float travel,
            float engageTravel,
            float stiffness,
            float exponent,
            float maxLength)
        {
            if (travel <= engageTravel) return 0f;

            float compression = travel - engageTravel;
            float ratio = compression / maxLength;
            ratio = math.clamp(ratio, 0f, 1f);

            return stiffness * math.pow(ratio, exponent) * maxLength;
        }

        public static float CalculateDroopStopForce(
            float travel,
            float maxDroopTravel,
            float stiffness)
        {
            if (travel >= -maxDroopTravel) return 0f;

            float extension = math.abs(travel) - maxDroopTravel;
            return -stiffness * extension;
        }

        public static float CalculateTotalForce(
            float springForce,
            float damperForce,
            float arbForce,
            float bumpStopForce,
            float droopStopForce)
        {
            return springForce + damperForce + arbForce + bumpStopForce + droopStopForce;
        }

        public static float CalculateWheelRate(
            float springRate,
            float motionRatio)
        {
            return springRate * motionRatio * motionRatio;
        }
    }
}
