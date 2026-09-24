using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Fuel system physics with mass distribution, slosh, and starvation.
    /// 
    /// Fuel Mass & Distribution:
    ///   fuel_mass = fuel_volume × fuel_density (0.75 kg/L)
    ///   Full tank (100L) = 75 kg
    ///   
    ///   vehicle_mass = base_mass + fuel_mass + driver_mass
    ///   CoG_adjustment = fuel_position × fuel_mass / vehicle_mass
    /// 
    /// Fuel Slosh:
    ///   fuel_CoG_offset = -acceleration × slosh_factor
    ///   slosh_factor ≈ 0.01-0.05 m per g
    ///   
    /// Fuel Starvation:
    ///   IF |lateral_G| > threshold OR |long_G| > threshold:
    ///     IF fuel_level < starvation_threshold:
    ///       fuel_starved = true, engine_torque = 0
    /// </summary>
    [BurstCompile]
    public struct FuelSystemConfig
    {
        public float TankCapacity;
        public float FuelDensity;
        public float FuelPositionX;
        public float FuelPositionY;
        public float SloshFactor;
        public float StarvationThresholdLateralG;
        public float StarvationThresholdLongG;
        public float StarvationFuelLevel;

        public static FuelSystemConfig Default()
        {
            return new FuelSystemConfig
            {
                TankCapacity = 100f,
                FuelDensity = 0.75f,
                FuelPositionX = 0f,
                FuelPositionY = -0.3f,
                SloshFactor = 0.02f,
                StarvationThresholdLateralG = 2.5f,
                StarvationThresholdLongG = 2.0f,
                StarvationFuelLevel = 10f
            };
        }
    }

    [BurstCompile]
    public struct FuelSystemState
    {
        public float FuelLevel;
        public float FuelMass;
        public float3 CoGOffset;
        public bool Starved;
        public float TotalMass;
    }

    [BurstCompile]
    public static class FuelSystem
    {
        [BurstCompile]
        public static FuelSystemState Update(
            in FuelSystemState state,
            in FuelSystemConfig config,
            float baseMass,
            float driverMass,
            in float3 acceleration,
            float fuelConsumption,
            float dt)
        {
            FuelSystemState result = state;
            result.FuelLevel = math.max(0f, result.FuelLevel - fuelConsumption * dt);
            result.FuelMass = result.FuelLevel * config.FuelDensity;

            float3 localAccel = new float3(acceleration.x, 0f, acceleration.z);
            float lateralG = math.abs(acceleration.x) / 9.81f;
            float longG = math.abs(acceleration.z) / 9.81f;

            result.CoGOffset = new float3(
                config.FuelPositionX,
                config.FuelPositionY,
                0f) + localAccel * config.SloshFactor;

            result.TotalMass = baseMass + result.FuelMass + driverMass;

            result.Starved = false;
            if (result.FuelLevel < config.StarvationFuelLevel)
            {
                if (lateralG > config.StarvationThresholdLateralG ||
                    longG > config.StarvationThresholdLongG)
                {
                    result.Starved = true;
                }
            }

            return result;
        }

        [BurstCompile]
        public static float CalculateFuelMass(float volumeLiters, float density)
        {
            return volumeLiters * density;
        }

        [BurstCompile]
        public static float CalculateConsumptionRate(
            float throttle,
            float rpm,
            float baseConsumption)
        {
            return baseConsumption * (0.3f + throttle * 0.7f) * (rpm / 10000f);
        }

        [BurstCompile]
        public static float3 CalculateCoGShift(
            in float3 fuelPosition,
            float fuelMass,
            float totalMass)
        {
            return fuelPosition * (fuelMass / totalMass);
        }
    }
}
