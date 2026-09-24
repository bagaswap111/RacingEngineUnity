using Unity.Burst;
using Unity.Mathematics;
using RacingSim.Vehicle;

namespace RacingSim.Thermal
{
    /// <summary>
    /// Tire thermal model with 3 zones (inner, middle, outer)
    /// Implements: dT/dt = (Q_in - Q_out) / (m × c)
    /// </summary>
    [BurstCompile]
    public static class TireThermalModel
    {
        /// <summary>
        /// Specific heat capacity of rubber (J/kg·K)
        /// </summary>
        public const float SPECIFIC_HEAT_RUBBER = 1800f;

        /// <summary>
        /// Density of rubber (kg/m³)
        /// </summary>
        public const float DENSITY_RUBBER = 1150f;

        /// <summary>
        /// Update tire temperatures for all three zones
        /// </summary>
        [BurstCompile]
        public static (float tempInner, float tempMiddle, float tempOuter) UpdateTemperatures(
            float tempInner,
            float tempMiddle,
            float tempOuter,
            float ambientTemp,
            float roadTemp,
            float verticalLoad,     // Fz (N)
            float slipRatio,        // κ
            float slipAngle,        // α
            float wheelOmega,       // rad/s
            float vehicleSpeed,     // m/s
            float tirePressure,     // kPa
            float tireWear,         // 0.0 - 1.0 (1.0 = new)
            bool isContactingRoad,
            float dt,
            in VehicleConfig config)
        {
            // Calculate heat generation from slip
            float slipVelocityLong = math.abs(slipRatio) * vehicleSpeed;
            float slipVelocityLat = math.abs(math.tan(slipAngle)) * vehicleSpeed;
            
            // Power from longitudinal slip: P = Fx × Vslip
            float powerLong = math.abs(verticalLoad * slipRatio * 0.3f) * slipVelocityLong;
            
            // Power from lateral slip: P = Fy × Vslip
            float powerLat = math.abs(verticalLoad * math.sin(slipAngle) * 0.5f) * slipVelocityLat;
            
            // Hysteresis heating (flexing of rubber)
            float hysteresisPower = config.tireHysteresisBase * math.abs(wheelOmega);
            
            // Total heat input (Q_in)
            float totalPower = powerLong + powerLat + hysteresisPower;
            
            // Distribute power across zones (simplified)
            // Inner zone gets more heat during cornering
            float loadDistribution = math.abs(slipAngle) / math.PI; // 0-0.5 typically
            
            float powerInner = totalPower * (0.3f + loadDistribution * 0.4f);
            float powerMiddle = totalPower * (0.4f - loadDistribution * 0.2f);
            float powerOuter = totalPower * (0.3f + loadDistribution * 0.4f);
            
            // Heat dissipation (Q_out)
            // Convection to air
            float convectionCoeff = config.tireConvectionBase + 
                                   config.tireConvectionSpeedFactor * vehicleSpeed;
            
            float qConvInner = convectionCoeff * config.tireSurfaceAreaInner * (tempInner - ambientTemp);
            float qConvMiddle = convectionCoeff * config.tireSurfaceAreaMiddle * (tempMiddle - ambientTemp);
            float qConvOuter = convectionCoeff * config.tireSurfaceAreaOuter * (tempOuter - ambientTemp);
            
            // Radiation (Stefan-Boltzmann)
            float emissivity = config.tireEmissivity;
            float stefanBoltzmann = 5.67e-8f;
            
            float kelvinInner = tempInner + 273.15f;
            float kelvinAmbient = ambientTemp + 273.15f;
            float qRadInner = emissivity * stefanBoltzmann * config.tireSurfaceAreaInner * 
                             (kelvinInner * kelvinInner * kelvinInner * kelvinInner - 
                              kelvinAmbient * kelvinAmbient * kelvinAmbient * kelvinAmbient);
            
            float kelvinMiddle = tempMiddle + 273.15f;
            float qRadMiddle = emissivity * stefanBoltzmann * config.tireSurfaceAreaMiddle * 
                              (kelvinMiddle * kelvinMiddle * kelvinMiddle * kelvinMiddle - 
                               kelvinAmbient * kelvinAmbient * kelvinAmbient * kelvinAmbient);
            
            float kelvinOuter = tempOuter + 273.15f;
            float qRadOuter = emissivity * stefanBoltzmann * config.tireSurfaceAreaOuter * 
                             (kelvinOuter * kelvinOuter * kelvinOuter * kelvinOuter - 
                              kelvinAmbient * kelvinAmbient * kelvinAmbient * kelvinAmbient);
            
            // Conduction to road (only when contacting)
            float qCondInner = 0f, qCondMiddle = 0f, qCondOuter = 0f;
            if (isContactingRoad)
            {
                float contactArea = config.tireContactPatchArea * (1f - tireWear * 0.3f);
                float conductionCoeff = config.tireRoadConductionCoeff;
                
                qCondInner = conductionCoeff * contactArea * 0.3f * (tempInner - roadTemp);
                qCondMiddle = conductionCoeff * contactArea * 0.4f * (tempMiddle - roadTemp);
                qCondOuter = conductionCoeff * contactArea * 0.3f * (tempOuter - roadTemp);
            }
            
            // Mass of each zone (simplified)
            float tireVolume = config.tireWidth * config.tireAspectRatio * config.wheelRadius * 0.01f;
            float tireMass = DENSITY_RUBBER * tireVolume * (1f - tireWear * 0.2f);
            float massPerZone = tireMass / 3f;
            
            // Temperature change: dT/dt = (Q_in - Q_out) / (m × c)
            float dTInner = (powerInner - qConvInner - qRadInner - qCondInner) / (massPerZone * SPECIFIC_HEAT_RUBBER);
            float dTMiddle = (powerMiddle - qConvMiddle - qRadMiddle - qCondMiddle) / (massPerZone * SPECIFIC_HEAT_RUBBER);
            float dTOuter = (powerOuter - qConvOuter - qRadOuter - qCondOuter) / (massPerZone * SPECIFIC_HEAT_RUBBER);
            
            // Integrate temperatures
            tempInner += dTInner * dt;
            tempMiddle += dTMiddle * dt;
            tempOuter += dTOuter * dt;
            
            // Clamp to realistic range
            tempInner = math.clamp(tempInner, ambientTemp, 200f);
            tempMiddle = math.clamp(tempMiddle, ambientTemp, 200f);
            tempOuter = math.clamp(tempOuter, ambientTemp, 200f);
            
            // Heat transfer between zones (thermal equilibrium tendency)
            float transferRate = config.tireInternalTransferRate * dt;
            float avgTemp = (tempInner + tempMiddle + tempOuter) / 3f;
            
            tempInner = math.lerp(tempInner, avgTemp, transferRate);
            tempMiddle = math.lerp(tempMiddle, avgTemp, transferRate);
            tempOuter = math.lerp(tempOuter, avgTemp, transferRate);
            
            return (tempInner, tempMiddle, tempOuter);
        }

        /// <summary>
        /// Calculate grip multiplier based on temperature
        /// μ(T) = μ_optimal × exp(-((T - T_optimal) / T_range)²)
        /// </summary>
        [BurstCompile]
        public static float CalculateGripMultiplier(
            float tempInner,
            float tempMiddle,
            float tempOuter,
            in VehicleConfig config)
        {
            // Average temperature weighted by zone importance
            float avgTemp = (tempInner * 0.3f + tempMiddle * 0.4f + tempOuter * 0.3f);
            
            // Optimal temperature for this tire compound
            float optimalTemp = config.tireOptimalTemp;
            float tempRange = config.tireOptimalTempRange;
            
            // Gaussian grip curve
            float tempDiff = avgTemp - optimalTemp;
            float gripMultiplier = math.exp(-(tempDiff * tempDiff) / (tempRange * tempRange));
            
            // Cold penalty (additional reduction when very cold)
            if (avgTemp < config.tireColdThreshold)
            {
                float coldFactor = avgTemp / config.tireColdThreshold;
                gripMultiplier *= math.lerp(0.5f, 1f, coldFactor);
            }
            
            // Overheated penalty
            if (avgTemp > config.tireOverheatThreshold)
            {
                float overheatFactor = (avgTemp - config.tireOverheatThreshold) / 50f;
                gripMultiplier *= (1f - math.saturate(overheatFactor) * 0.5f);
            }
            
            return math.max(0.3f, gripMultiplier);
        }

        /// <summary>
        /// Calculate tire pressure change due to temperature
        /// P/T = constant (ideal gas law, simplified)
        /// </summary>
        [BurstCompile]
        public static float CalculatePressureChange(
            float initialPressure,
            float initialTemp,
            float currentTemp,
            float leakRate)
        {
            // Convert to Kelvin
            float initialK = initialTemp + 273.15f;
            float currentK = currentTemp + 273.15f;
            
            // Ideal gas law: P2 = P1 × (T2/T1)
            float pressureFromTemp = initialPressure * (currentK / initialK);
            
            // Account for slow leak/puncture
            float pressureLoss = leakRate; // kPa per second
            
            return math.max(100f, pressureFromTemp - pressureLoss);
        }
    }

    /// <summary>
    /// Brake thermal model
    /// Implements: dT/dt = (τ×ω - h×A×ΔT - ε×σ×A×(T⁴-T_amb⁴)) / (m×c)
    /// </summary>
    [BurstCompile]
    public static class BrakeThermalModel
    {
        /// <summary>
        /// Specific heat capacity of cast iron (J/kg·K)
        /// </summary>
        public const float SPECIFIC_HEAT_IRON = 540f;

        /// <summary>
        /// Density of cast iron (kg/m³)
        /// </summary>
        public const float DENSITY_IRON = 7200f;

        /// <summary>
        /// Update brake disc temperature
        /// </summary>
        [BurstCompile]
        public static float UpdateTemperature(
            float currentTemp,
            float ambientTemp,
            float brakeTorque,
            float wheelOmega,
            float vehicleSpeed,
            float padWear,
            bool isBraking,
            float dt,
            in VehicleConfig config)
        {
            // Heat input from braking: Q_in = τ × ω
            float powerInput = 0f;
            if (isBraking && brakeTorque > 0f)
            {
                powerInput = math.abs(brakeTorque * wheelOmega);
                
                // Not all kinetic energy goes to the disc (some to pads, some to environment)
                powerInput *= config.brakeEnergyToDiscFraction;
            }
            
            // Convective cooling: Q_conv = h × A × (T - T_amb)
            // Convection coefficient increases with speed (airflow through disc)
            float convectionCoeff = config.brakeConvectionBase + 
                                   config.brakeConvectionSpeedFactor * vehicleSpeed;
            
            // Cooling area (vented disc has more surface area)
            float coolingArea = config.brakeDiscArea * config.brakeVaneFactor;
            
            float qConvection = convectionCoeff * coolingArea * (currentTemp - ambientTemp);
            
            // Radiative cooling: Q_rad = ε × σ × A × (T⁴ - T_amb⁴)
            float emissivity = config.brakeEmissivity;
            float stefanBoltzmann = 5.67e-8f;
            
            float kelvinTemp = currentTemp + 273.15f;
            float kelvinAmbient = ambientTemp + 273.15f;
            
            float qRadiation = emissivity * stefanBoltzmann * coolingArea * 
                              (kelvinTemp * kelvinTemp * kelvinTemp * kelvinTemp - 
                               kelvinAmbient * kelvinAmbient * kelvinAmbient * kelvinAmbient);
            
            // Conductive cooling to hub/caliper (simplified)
            float qConduction = config.brakeConductionCoeff * (currentTemp - ambientTemp);
            
            // Pad friction heating (additional heat from pad to disc)
            float padHeating = 0f;
            if (isBraking)
            {
                // Worn pads generate more heat due to reduced friction efficiency
                float wearFactor = 1f + padWear * 0.3f;
                padHeating = powerInput * 0.1f * wearFactor; // 10% of braking power as additional heat
            }
            
            // Disc mass
            float discVolume = math.PI * config.brakeDiscRadius * config.brakeDiscRadius * 
                              config.brakeDiscThickness * config.brakeVaneFactor;
            float discMass = DENSITY_IRON * discVolume;
            
            // Temperature change: dT/dt = (Q_in - Q_out) / (m × c)
            float netPower = powerInput + padHeating - qConvection - qRadiation - qConduction;
            float dT = netPower / (discMass * SPECIFIC_HEAT_IRON);
            
            // Integrate temperature
            float newTemp = currentTemp + dT * dt;
            
            // Clamp to realistic range
            newTemp = math.clamp(newTemp, ambientTemp, config.brakeMaxTemperature);
            
            return newTemp;
        }

        /// <summary>
        /// Calculate brake fade factor based on temperature
        /// </summary>
        [BurstCompile]
        public static float CalculateFadeFactor(float brakeTemp, in VehicleConfig config)
        {
            float fadeFactor = 1f;
            
            // Onset of fade
            if (brakeTemp > config.brakeFadeOnsetTemp)
            {
                float fadeIntensity = (brakeTemp - config.brakeFadeOnsetTemp) / 
                                     (config.brakeMaxTemperature - config.brakeFadeOnsetTemp);
                fadeFactor = 1f - math.saturate(fadeIntensity) * config.brakeFadeSeverity;
            }
            
            // Cold brakes also have reduced performance
            if (brakeTemp < config.brakeOptimalTempMin)
            {
                float coldFactor = (config.brakeOptimalTempMin - brakeTemp) / 
                                  config.brakeOptimalTempMin;
                fadeFactor *= (1f - coldFactor * 0.3f);
            }
            
            return math.max(0.2f, fadeFactor);
        }

        /// <summary>
        /// Check for brake fluid boil
        /// </summary>
        [BurstCompile]
        public static bool CheckFluidBoil(float brakeTemp, float dotRating)
        {
            // DOT ratings have different boiling points
            float boilingPoint = dotRating switch
            {
                3 => 205f,   // DOT 3: 205°C dry
                4 => 230f,   // DOT 4: 230°C dry
                5 => 260f,   // DOT 5.1: 260°C dry
                _ => 205f
            };
            
            return brakeTemp >= boilingPoint;
        }
    }

    /// <summary>
    /// Engine/oil thermal model (simplified lumped capacitance)
    /// </summary>
    [BurstCompile]
    public static class EngineThermalModel
    {
        /// <summary>
        /// Update engine and oil temperatures
        /// </summary>
        [BurstCompile]
        public static (float engineTemp, float oilTemp) UpdateTemperatures(
            float engineTemp,
            float oilTemp,
            float ambientTemp,
            float coolantTemp,
            float engineRPM,
            float engineLoad,
            float vehicleSpeed,
            float radiatorFanSpeed,
            float dt,
            in VehicleConfig config)
        {
            // Heat generation from engine operation
            // Proportional to RPM and load
            float baseHeatGeneration = config.engineHeatGenerationBase;
            float rpmFactor = engineRPM / config.engineRedlineRPM;
            float loadFactor = engineLoad;
            
            float heatGeneration = baseHeatGeneration * (0.5f + 0.5f * rpmFactor) * (0.3f + 0.7f * loadFactor);
            
            // Oil heating from friction with engine components
            float oilHeating = heatGeneration * 0.4f; // 40% of heat goes to oil
            
            // Engine cooling via coolant system
            float coolantEfficiency = config.coolantEfficiencyBase + 
                                     config.coolantEfficiencyFlowFactor * radiatorFanSpeed;
            float qCoolant = coolantEfficiency * (engineTemp - coolantTemp);
            
            // Oil cooling via oil cooler
            float oilCoolerEfficiency = config.oilCoolerEfficiencyBase +
                                       config.oilCoolerAirflowFactor * vehicleSpeed;
            float qOilCooler = oilCoolerEfficiency * (oilTemp - ambientTemp);
            
            // Heat exchange between oil and engine
            float oilEngineExchange = config.oilEngineHeatTransferCoeff * (engineTemp - oilTemp);
            
            // Engine cooling to ambient (convection/radiation)
            float engineSurfaceArea = config.engineSurfaceArea;
            float qEngineAmbient = config.engineConvectionCoeff * engineSurfaceArea * (engineTemp - ambientTemp);
            
            // Oil mass and specific heat
            float oilMass = config.oilCapacity * 0.85f; // ~85% of capacity in use
            float oilSpecificHeat = 2000f; // J/kg·K for oil
            
            // Engine effective mass
            float engineMass = config.engineMass * 0.6f; // Effective heated mass
            float engineSpecificHeat = 500f; // J/kg·K for aluminum/iron mix
            
            // Temperature changes
            float dOil = (oilHeating - qOilCooler + oilEngineExchange) / (oilMass * oilSpecificHeat);
            float dEngine = (heatGeneration - qCoolant - qEngineAmbient - oilEngineExchange) / 
                           (engineMass * engineSpecificHeat);
            
            // Integrate
            oilTemp += dOil * dt;
            engineTemp += dEngine * dt;
            
            // Clamp to realistic ranges
            oilTemp = math.clamp(oilTemp, ambientTemp, config.oilMaxTemperature);
            engineTemp = math.clamp(engineTemp, ambientTemp, config.engineTempCritical);
            
            // Oil warms up slower than engine when cold
            if (oilTemp < ambientTemp + 10f)
            {
                oilTemp = math.lerp(oilTemp, engineTemp, 0.1f * dt);
            }
            
            return (engineTemp, oilTemp);
        }

        /// <summary>
        /// Check for overheating condition
        /// </summary>
        [BurstCompile]
        public static bool CheckOverheating(float engineTemp, float oilTemp, in VehicleConfig config)
        {
            return engineTemp > config.engineTempCritical || 
                   oilTemp > config.oilMaxTemperature;
        }
    }

    /// <summary>
    /// Complete thermal state
    /// </summary>
    public struct ThermalState
    {
        // Tires (4 wheels × 3 zones)
        public float TireTempFL_Inner, TireTempFL_Middle, TireTempFL_Outer;
        public float TireTempFR_Inner, TireTempFR_Middle, TireTempFR_Outer;
        public float TireTempRL_Inner, TireTempRL_Middle, TireTempRL_Outer;
        public float TireTempRR_Inner, TireTempRR_Middle, TireTempRR_Outer;
        
        // Brakes
        public float BrakeTempFL, BrakeTempFR, BrakeTempRL, BrakeTempRR;
        
        // Engine
        public float EngineTemp;
        public float OilTemp;
        public float CoolantTemp;
        
        // Grip multipliers
        public float TireGripFL, TireGripFR, TireGripRL, TireGripRR;
        public float BrakeEfficiencyFL, BrakeEfficiencyFR, BrakeEfficiencyRL, BrakeEfficiencyRR;
        
        // Warning flags
        public bool TireOverheatedFL, TireOverheatedFR, TireOverheatedRL, TireOverheatedRR;
        public bool BrakeOverheatedFL, BrakeOverheatedFR, BrakeOverheatedRL, BrakeOverheatedRR;
        public bool EngineOverheating;
        public bool BrakeFluidBoiling;
    }
}
