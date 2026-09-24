namespace RacingSim.Core
{
    /// <summary>
    /// Centralized vehicle constants.
    /// Eliminates magic numbers scattered across systems.
    /// </summary>
    public static class VehicleConstants
    {
        public const float DEFAULT_WHEEL_RADIUS = 0.33f;

        public const float GRAVITY = 9.81f;
        public const float AIR_DENSITY_SEA_LEVEL = 1.225f;
        public const float DEG_TO_RAD = 0.0174533f;
        public const float RAD_TO_DEG = 57.2958f;
    }
}
