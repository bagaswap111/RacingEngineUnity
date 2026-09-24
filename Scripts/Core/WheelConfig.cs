using UnityEngine;

namespace RacingSim.Core
{
    /// <summary>
    /// Wheel and tire configuration.
    /// Replaces hardcoded 0.33f wheel radius across the codebase.
    /// </summary>
    [System.Serializable]
    public struct WheelConfig
    {
        [Header("Geometry")]
        public float Radius;
        public float Width;
        public float RimRadius;

        [Header("Mass")]
        public float TireMass;
        public float RimMass;
        public float BrakeDiscMass;

        [Header("Tire")]
        public float TireStiffness;
        public float TireDamping;

        public float TotalUnsprungMass => TireMass + RimMass + BrakeDiscMass;

        public static WheelConfig Default()
        {
            return new WheelConfig
            {
                Radius = 0.33f,
                Width = 0.25f,
                RimRadius = 0.19f,
                TireMass = 12f,
                RimMass = 8f,
                BrakeDiscMass = 5f,
                TireStiffness = 300000f,
                TireDamping = 1500f
            };
        }
    }
}
