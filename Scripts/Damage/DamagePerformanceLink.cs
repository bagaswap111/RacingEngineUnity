using UnityEngine;
using System.Collections.Generic;

namespace RacingSim.Damage
{
    /// <summary>
    /// Maps visual damage to vehicle performance parameters.
    /// Updates aerodynamics, suspension, engine, and weight distribution
    /// based on accumulated zone damage.
    /// </summary>
    public class DamagePerformanceLink : MonoBehaviour
    {
        [Header("Aero Damage")]
        public float frontAeroDamageFactor = 0.35f;
        public float rearAeroDamageFactor = 0.35f;
        public float dragDamageFactor = 0.2f;
        public float sideWindDamageFactor = 0.5f;

        [Header("Suspension Damage")]
        public float suspDamageThreshold = 0.3f;
        public float suspSpringRateFactor = 0.4f;
        public float suspDampingFactor = 0.3f;
        public float suspCamberFactor = 2.5f;
        public float suspToeFactor = 1f;

        [Header("Engine Damage")]
        public float engineDamageThreshold = 0.3f;
        public float engineTorqueFactor = 0.3f;
        public float engineCoolingFactor = 0.5f;
        public float engineMisfireThreshold = 0.7f;
        public float engineMisfireChance = 0.3f;

        [Header("Tire Damage")]
        public float tireDamageThreshold = 0.5f;
        public float tireGripFactor = 0.7f;
        public float tirePunctureThreshold = 0.8f;

        private float originalClFront;
        private float originalClRear;
        private float originalCd;
        private float originalSideWindSensitivity;
        private float[] originalSpringRate = new float[4];
        private float[] originalDamping = new float[4];
        private float[] originalCamber = new float[4];
        private float[] originalToe = new float[4];
        private float originalMaxTorque;
        private float originalCoolingEfficiency;
        private bool isInitialized;

        public float CurrentAeroDamage { get; private set; }
        public float CurrentSuspDamage { get; private set; }
        public float CurrentEngineDamage { get; private set; }
        public float CurrentTireDamage { get; private set; }

        /// <summary>
        /// Initialize with original vehicle parameters.
        /// Call this once at startup with the vehicle's baseline values.
        /// </summary>
        public void Initialize(float clFront, float clRear, float cd, float sideWindSensitivity,
            float[] springRate, float[] damping, float[] camber, float[] toe,
            float maxTorque, float coolingEfficiency)
        {
            if (isInitialized) return;

            originalClFront = clFront;
            originalClRear = clRear;
            originalCd = cd;
            originalSideWindSensitivity = sideWindSensitivity;

            for (int i = 0; i < 4; i++)
            {
                originalSpringRate[i] = springRate != null && i < springRate.Length ? springRate[i] : 50000f;
                originalDamping[i] = damping != null && i < damping.Length ? damping[i] : 3000f;
                originalCamber[i] = camber != null && i < camber.Length ? camber[i] : 0f;
                originalToe[i] = toe != null && i < toe.Length ? toe[i] : 0f;
            }

            originalMaxTorque = maxTorque;
            originalCoolingEfficiency = coolingEfficiency;
            isInitialized = true;
        }

        public void UpdateDamage(DamageZone[] zones)
        {
            if (!isInitialized || zones == null) return;

            float frontAero = GetAverageDamage(zones, new string[] {
                "FrontBumper", "Hood", "Fender_FL", "Fender_FR"
            });
            float rearAero = GetAverageDamage(zones, new string[] {
                "RearBumper", "Trunk", "Spoiler"
            });
            float engineDmg = GetAverageDamage(zones, new string[] {
                "FrontBumper", "Hood"
            });

            CurrentAeroDamage = (frontAero + rearAero) * 0.5f;
            CurrentEngineDamage = engineDmg;
            CurrentSuspDamage = GetAverageDamage(zones, new string[] {
                "SuspMount_FL", "SuspMount_FR", "SuspMount_RL", "SuspMount_RR"
            });
            CurrentTireDamage = GetAverageDamage(zones, new string[] {
                "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR"
            });
        }

        public void ApplyAeroDamage(ref float clFront, ref float clRear, ref float cd,
            ref float sideWindSensitivity)
        {
            clFront = originalClFront * (1f - CurrentAeroDamage * frontAeroDamageFactor);
            clRear = originalClRear * (1f - CurrentAeroDamage * rearAeroDamageFactor);
            cd = originalCd * (1f + CurrentAeroDamage * dragDamageFactor);
            sideWindSensitivity = originalSideWindSensitivity *
                (1f + CurrentAeroDamage * sideWindDamageFactor);
        }

        public void ApplySuspensionDamage(float[] springRate, float[] damping,
            float[] camber, float[] toe)
        {
            for (int i = 0; i < 4; i++)
            {
                springRate[i] = originalSpringRate[i];
                damping[i] = originalDamping[i];
                camber[i] = originalCamber[i];
                toe[i] = originalToe[i];

                if (CurrentSuspDamage > suspDamageThreshold)
                {
                    springRate[i] *= (1f - CurrentSuspDamage * suspSpringRateFactor);
                    damping[i] *= (1f - CurrentSuspDamage * suspDampingFactor);
                    camber[i] += CurrentSuspDamage * suspCamberFactor;
                    toe[i] += CurrentSuspDamage * suspToeFactor;
                }
            }
        }

        public float ApplyEngineDamage(float maxTorque, float coolingEfficiency,
            out bool isMisfiring)
        {
            isMisfiring = false;
            float torque = maxTorque;

            if (CurrentEngineDamage > engineDamageThreshold)
            {
                torque = originalMaxTorque * (1f - CurrentEngineDamage * engineTorqueFactor);
            }

            if (CurrentEngineDamage > engineMisfireThreshold)
            {
                isMisfiring = Random.value < (CurrentEngineDamage * engineMisfireChance);
                if (isMisfiring)
                {
                    torque *= (0.7f + Random.value * 0.3f);
                }
            }

            return torque;
        }

        public float[] ApplyTireDamage(float[] gripMultipliers, bool[] isPunctured)
        {
            float[] result = new float[4];

            for (int i = 0; i < 4; i++)
            {
                result[i] = gripMultipliers != null && i < gripMultipliers.Length ?
                    gripMultipliers[i] : 1f;

                if (CurrentTireDamage > tireDamageThreshold)
                {
                    result[i] *= (1f - CurrentTireDamage * tireGripFactor);
                }

                if (isPunctured != null && i < isPunctured.Length)
                {
                    isPunctured[i] = CurrentTireDamage > tirePunctureThreshold;
                }
            }

            return result;
        }

        public float CalculateWeightLoss(List<FractureZone> detachedZones)
        {
            float lostMass = 0f;
            if (detachedZones != null)
            {
                foreach (var zone in detachedZones)
                {
                    if (zone != null && zone.IsFractured)
                    {
                        lostMass += zone.config.FragmentMass;
                    }
                }
            }
            return lostMass;
        }

        private float GetAverageDamage(DamageZone[] zones, string[] zoneNames)
        {
            float total = 0f;
            int count = 0;

            foreach (string name in zoneNames)
            {
                foreach (var zone in zones)
                {
                    if (zone != null && zone.config.ZoneName == name)
                    {
                        total += zone.GetDamageNormalized();
                        count++;
                        break;
                    }
                }
            }

            return count > 0 ? total / count : 0f;
        }

        public void Reset()
        {
            CurrentAeroDamage = 0f;
            CurrentSuspDamage = 0f;
            CurrentEngineDamage = 0f;
            CurrentTireDamage = 0f;
        }
    }
}
