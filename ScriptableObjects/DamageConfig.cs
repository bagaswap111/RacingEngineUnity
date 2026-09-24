using UnityEngine;

namespace RacingSim.Vehicle
{
    /// <summary>
    /// ScriptableObject for damage parameters.
    /// Separated from VehicleConfig because damage is authored by
    /// VFX/technical artists, not vehicle engineers.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "Racing Sim/Damage Config")]
    public class DamageConfig : ScriptableObject
    {
        [Header("Engine Damage")]
        public float EngineDamageOverrevRate = 0.05f;
        public float EngineDamageOverheatRate = 0.03f;
        public float EngineDamageColdRate = 0.01f;
        public float EngineTempOptimalMax = 110f;
        public float OptimalOilTempMin = 70f;

        [Header("Suspension Damage")]
        public float SuspensionMaxLoad = 8000f;
        public float SuspensionDamageOverloadRate = 0.1f;
        public float SuspensionDamageBottomingRate = 0.05f;
        public float SuspensionDamageOvertravelRate = 0.02f;

        [Header("Tire Damage")]
        public float TireWearBaseRate = 0.001f;
        public float TireWearSlipRate = 0.01f;
        public float TireOverheatThreshold = 120f;
        public float TireWearFlatSpotRate = 0.05f;
        public float TireMaxLoad = 6000f;
        public float TireBurstTemperature = 160f;

        [Header("Aero Damage")]
        public float AeroDamageThreshold = 500f;
        public float AeroDamageCoefficient = 0.001f;
    }
}
