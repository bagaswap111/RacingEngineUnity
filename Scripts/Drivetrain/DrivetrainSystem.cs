using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Vehicle;

namespace RacingSim.Drivetrain
{
    /// <summary>
    /// [OBSOLETE] Use AdvancedEngine.cs and AdvancedTransmission.cs instead.
    /// This file is retained for reference only. It references VehicleConfig
    /// fields that no longer exist and will not compile.
    /// 
    /// Replacements:
    /// - AdvancedEngine.cs: Turbo, engine braking, rev limiter, anti-lag
    /// - AdvancedTransmission.cs: Dog ring engagement, shift sequence
    /// - DrivelineOscillation.cs: 2-mass torsional model
    /// </summary>
    [System.Obsolete("Use AdvancedEngine, AdvancedTransmission, DrivelineOscillation instead")]
    public static class DrivetrainSystem_OLD
    {
        // Intentionally empty - legacy code preserved in git history
    }

    /// <summary>
    /// Engine torque curve data point
    /// </summary>
    [System.Serializable]
    public struct TorqueCurvePoint
    {
        public float RPM;
        public float Torque; // Nm
    }

    /// <summary>
    /// Engine simulation with torque curve, inertia, friction, and damage effects
    /// Implements: τ_engine = τ_base(RPM) × throttle_map × f_damage × f_temp
    /// </summary>
    [BurstCompile]
    public static class EngineSim
    {
        /// <summary>
        /// Calculate engine torque based on RPM and throttle input
        /// Uses lookup table interpolation for torque curve
        /// </summary>
        [BurstCompile]
        public static float CalculateEngineTorque(
            float rpm, 
            float throttle, 
            in VehicleConfig config,
            float engineHealth,
            float engineTemp,
            float oilTemp)
        {
            // Get base torque from curve (linear interpolation)
            float baseTorque = GetTorqueFromCurve(rpm, config.engineTorqueCurve);
            
            // Throttle mapping (non-linear at low RPM for realism)
            float throttleMultiplier = CalculateThrottleMapping(throttle, rpm, config);
            
            // Damage factor
            float damageFactor = math.max(0.3f, engineHealth);
            
            // Temperature factor (cold engine = less torque, overheating = power loss)
            float tempFactor = CalculateTempFactor(engineTemp, oilTemp, config);
            
            // Apply rev limiter
            if (rpm > config.engineRedlineRPM)
            {
                // Fuel/spark cut above redline
                float cutIntensity = math.saturate((rpm - config.engineRedlineRPM) / 500f);
                baseTorque *= (1f - cutIntensity * 0.9f);
            }
            
            // Final torque calculation
            float torque = baseTorque * throttleMultiplier * damageFactor * tempFactor;
            
            return torque;
        }

        /// <summary>
        /// Calculate engine angular acceleration
        /// I_engine · dω/dt = τ_engine - τ_clutch - τ_friction
        /// </summary>
        [BurstCompile]
        public static float CalculateAngularAcceleration(
            float engineTorque,
            float clutchTorque,
            float angularVelocity,
            float momentOfInertia,
            in VehicleConfig config)
        {
            // Friction torque (viscous + constant)
            float frictionTorque = config.engineFrictionConstant + 
                                  config.engineFrictionViscous * math.abs(angularVelocity);
            
            // Net torque
            float netTorque = engineTorque - clutchTorque - frictionTorque;
            
            // Angular acceleration: α = τ / I
            float angularAccel = netTorque / momentOfInertia;
            
            return angularAccel;
        }

        /// <summary>
        /// Interpolate torque from curve using linear interpolation
        /// </summary>
        [BurstCompile]
        private static float GetTorqueFromCurve(float rpm, NativeSlice<TorqueCurvePoint> curve)
        {
            if (curve.Length == 0) return 0f;
            if (rpm <= curve[0].RPM) return curve[0].Torque;
            if (rpm >= curve[curve.Length - 1].RPM) return curve[curve.Length - 1].Torque;

            // Find interpolation interval
            for (int i = 0; i < curve.Length - 1; i++)
            {
                if (rpm >= curve[i].RPM && rpm < curve[i + 1].RPM)
                {
                    float t = (rpm - curve[i].RPM) / (curve[i + 1].RPM - curve[i].RPM);
                    return math.lerp(curve[i].Torque, curve[i + 1].Torque, t);
                }
            }

            return curve[curve.Length - 1].Torque;
        }

        private static float GetTorqueFromCurve(float rpm, AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            return curve.Evaluate(rpm);
        }

        /// <summary>
        /// Non-linear throttle mapping for realistic engine behavior
        /// </summary>
        [BurstCompile]
        private static float CalculateThrottleMapping(float throttle, float rpm, in VehicleConfig config)
        {
            // Simple linear mapping (can be enhanced with throttle map table)
            float mappedThrottle = throttle;
            
            // Reduce effectiveness at very low RPM (engine struggling)
            if (rpm < config.engineIdleRPM * 1.2f)
            {
                float lowRPMPenalty = 1f - (config.engineIdleRPM * 1.2f - rpm) / config.engineIdleRPM;
                mappedThrottle *= math.max(0.3f, lowRPMPenalty);
            }
            
            return mappedThrottle;
        }

        /// <summary>
        /// Temperature factor calculation
        /// Cold engine and overheating both reduce power
        /// </summary>
        [BurstCompile]
        private static float CalculateTempFactor(float engineTemp, float oilTemp, in VehicleConfig config)
        {
            float factor = 1f;
            
            // Cold engine penalty (optimal temp around 90-100°C)
            if (oilTemp < config.optimalOilTempMin)
            {
                float coldPenalty = (config.optimalOilTempMin - oilTemp) / config.optimalOilTempMin;
                factor *= (1f - coldPenalty * 0.15f); // Up to 15% power loss when cold
            }
            
            // Overheating penalty
            if (engineTemp > config.engineTempOptimalMax)
            {
                float overheatRatio = (engineTemp - config.engineTempOptimalMax) / 
                                     (config.engineTempCritical - config.engineTempOptimalMax);
                factor *= (1f - math.saturate(overheatRatio) * 0.4f); // Up to 40% power loss
            }
            
            return math.max(0.3f, factor);
        }

        /// <summary>
        /// Calculate engine braking torque when throttle is closed
        /// </summary>
        [BurstCompile]
        public static float CalculateEngineBraking(float rpm, float throttle, in VehicleConfig config)
        {
            if (throttle > 0.1f) return 0f; // No engine braking when on throttle
            
            // Base engine braking from compression
            float baseBraking = config.engineBrakingBase;
            
            // Increases with RPM
            float rpmFactor = rpm / config.engineRedlineRPM;
            float brakingTorque = baseBraking * (0.5f + rpmFactor * 0.5f);
            
            return -brakingTorque; // Negative = braking
        }
    }

    /// <summary>
    /// Clutch simulation with engagement, slipping, and lockup states
    /// τ_clutch = μ_clutch × F_clutch × R_clutch × sign(ω_engine - ω_input)
    /// </summary>
    [BurstCompile]
    public static class ClutchSim
    {
        /// <summary>
        /// Calculate clutch torque based on engagement and slip
        /// </summary>
        [BurstCompile]
        public static float CalculateClutchTorque(
            float engagement, // 0.0 - 1.0
            float engineOmega, // rad/s
            float inputShaftOmega, // rad/s
            in VehicleConfig config)
        {
            float omegaDiff = engineOmega - inputShaftOmega;
            float omegaDiffAbs = math.abs(omegaDiff);
            
            // Maximum clutch torque capacity
            float maxClutchTorque = config.clutchFrictionCoeff * 
                                   config.clutchNormalForce * 
                                   config.clutchEffectiveRadius * 
                                   config.clutchNumFrictionSurfaces;
            
            // Engagement-based capacity
            float currentCapacity = maxClutchTorque * engagement;
            
            // Lockup threshold (very small speed difference)
            const float lockupThreshold = 5f; // rad/s (~50 RPM difference)
            
            if (omegaDiffAbs < lockupThreshold && engagement > 0.95f)
            {
                // Locked state - transmit required torque to maintain lock
                // In reality, this would be solved by constraint solver
                return currentCapacity; // Return max as placeholder
            }
            else
            {
                // Slipping state
                float direction = math.sign(omegaDiff);
                return currentCapacity * direction;
            }
        }

        /// <summary>
        /// Calculate ideal engagement for smooth shifting
        /// </summary>
        [BurstCompile]
        public static float CalculateIdealEngagement(
            float pedalInput, // 0.0 - 1.0 (1.0 = pedal released)
            float shiftProgress, // 0.0 - 1.0 during gear change
            in VehicleConfig config)
        {
            // During shift, engagement ramps down then up
            if (shiftProgress > 0f && shiftProgress < 1f)
            {
                // S-curve for smooth engagement
                float shiftPhase = shiftProgress;
                if (shiftPhase < 0.5f)
                {
                    // Disengaging
                    return 1f - shiftPhase * 2f;
                }
                else
                {
                    // Engaging
                    return (shiftPhase - 0.5f) * 2f;
                }
            }
            
            // Normal operation - direct mapping from pedal
            return math.saturate(pedalInput);
        }
    }

    /// <summary>
    /// Gearbox simulation with gear ratios, shift time, and efficiency
    /// </summary>
    [BurstCompile]
    public static class GearboxSim
    {
        /// <summary>
        /// Get total gear ratio (gear × final drive)
        /// </summary>
        [BurstCompile]
        public static float GetTotalRatio(int gear, in VehicleConfig config)
        {
            if (gear <= 0 || gear > config.gearRatios.Length)
            {
                // Neutral or reverse
                return gear == 0 ? 0f : config.reverseGearRatio;
            }
            
            return config.gearRatios[gear - 1] * config.finalDriveRatio;
        }

        /// <summary>
        /// Calculate output torque from gearbox
        /// τ_output = τ_input × r_total × η_gearbox
        /// </summary>
        [BurstCompile]
        public static float CalculateOutputTorque(
            float inputTorque,
            int gear,
            bool isShifting,
            in VehicleConfig config)
        {
            if (isShifting || gear <= 0)
            {
                return 0f; // No torque during shift or neutral
            }
            
            float totalRatio = GetTotalRatio(gear, config);
            float efficiency = config.gearboxEfficiency;
            
            return inputTorque * totalRatio * efficiency;
        }

        /// <summary>
        /// Calculate output angular velocity
        /// ω_output = ω_input / r_total
        /// </summary>
        [BurstCompile]
        public static float CalculateOutputOmega(
            float inputOmega,
            int gear,
            in VehicleConfig config)
        {
            if (gear <= 0) return 0f;
            
            float totalRatio = GetTotalRatio(gear, config);
            if (totalRatio <= 0f) return 0f;
            
            return inputOmega / totalRatio;
        }

        /// <summary>
        /// Determine optimal shift point based on power curve
        /// </summary>
        [BurstCompile]
        public static bool ShouldShiftUp(
            float currentRPM,
            int currentGear,
            float throttle,
            in VehicleConfig config)
        {
            if (currentGear >= config.gearRatios.Length) return false;
            
            // Shift at redline under full throttle
            float shiftRPM = config.engineRedlineRPM - 200f; // Shift slightly before redline
            
            // Under partial throttle, shift earlier for efficiency
            if (throttle < 0.8f)
            {
                shiftRPM = math.lerp(config.optimalShiftRPM, config.engineRedlineRPM, throttle);
            }
            
            return currentRPM > shiftRPM;
        }

        /// <summary>
        /// Determine if should downshift
        /// </summary>
        [BurstCompile]
        public static bool ShouldShiftDown(
            float currentRPM,
            int currentGear,
            float throttle,
            in VehicleConfig config)
        {
            if (currentGear <= 1) return false;
            
            // Downshift when RPM drops too low
            float downshiftRPM = config.engineIdleRPM * 1.5f;
            
            // Downshift earlier under high throttle demand
            if (throttle > 0.7f)
            {
                downshiftRPM = config.optimalShiftRPM;
            }
            
            return currentRPM < downshiftRPM;
        }
    }

    /// <summary>
    /// Differential simulation supporting Open, LSD, Viscous, and Torsen types
    /// </summary>
    [BurstCompile]
    public static class DifferentialSim
    {
        /// <summary>
        /// Calculate torque split between left and right wheels
        /// </summary>
        [BurstCompile]
        public static (float torqueLeft, float torqueRight) CalculateTorqueSplit(
            float inputTorque,
            float omegaLeft,
            float omegaRight,
            DifferentialType diffType,
            in VehicleConfig config)
        {
            switch (diffType)
            {
                case DifferentialType.Open:
                    return CalculateOpenDiff(inputTorque, omegaLeft, omegaRight);
                
                case DifferentialType.LimitedSlip:
                    return CalculateLSD(inputTorque, omegaLeft, omegaRight, config);
                
                case DifferentialType.Viscous:
                    return CalculateViscousDiff(inputTorque, omegaLeft, omegaRight, config);
                
                case DifferentialType.Torsen:
                    return CalculateTorsen(inputTorque, omegaLeft, omegaRight, config);
                
                default:
                    return (inputTorque * 0.5f, inputTorque * 0.5f);
            }
        }

        /// <summary>
        /// Open differential: equal torque split
        /// τ_left = τ_right = τ_input / 2
        /// </summary>
        [BurstCompile]
        private static (float, float) CalculateOpenDiff(
            float inputTorque,
            float omegaLeft,
            float omegaRight)
        {
            float halfTorque = inputTorque * 0.5f;
            return (halfTorque, halfTorque);
        }

        /// <summary>
        /// Limited Slip Differential with clutch preload
        /// </summary>
        [BurstCompile]
        private static (float, float) CalculateLSD(
            float inputTorque,
            float omegaLeft,
            float omegaRight,
            in VehicleConfig config)
        {
            float halfTorque = inputTorque * 0.5f;
            float omegaDiff = omegaLeft - omegaRight;
            
            // Preload torque from spring
            float preloadTorque = config.lsdPreloadTorque;
            
            // Ramp-based locking torque
            float rampTorque = math.abs(inputTorque) * config.lsdRampFactor * 
                              math.tan(math.radians(config.lsdRampAngle));
            
            // Total locking torque
            float lockTorque = preloadTorque + rampTorque;
            
            // Apply lock torque opposite to speed difference
            float lockDirection = -math.sign(omegaDiff);
            float torqueTransfer = lockTorque * lockDirection;
            
            // Limit torque transfer to not exceed input
            float maxTransfer = math.abs(halfTorque);
            torqueTransfer = math.clamp(torqueTransfer, -maxTransfer, maxTransfer);
            
            return (halfTorque + torqueTransfer, halfTorque - torqueTransfer);
        }

        /// <summary>
        /// Viscous differential: torque transfer proportional to speed difference
        /// τ_transfer = k_viscous × (ω_left - ω_right)
        /// </summary>
        [BurstCompile]
        private static (float, float) CalculateViscousDiff(
            float inputTorque,
            float omegaLeft,
            float omegaRight,
            in VehicleConfig config)
        {
            float halfTorque = inputTorque * 0.5f;
            float omegaDiff = omegaLeft - omegaRight;
            
            // Viscous torque transfer
            float torqueTransfer = config.viscousDiffCoefficient * omegaDiff;
            
            // Limit transfer
            float maxTransfer = math.abs(halfTorque) * 0.8f;
            torqueTransfer = math.clamp(torqueTransfer, -maxTransfer, maxTransfer);
            
            return (halfTorque + torqueTransfer, halfTorque - torqueTransfer);
        }

        /// <summary>
        /// Torsen differential with Torque Bias Ratio
        /// τ_max_wheel = τ_min_wheel × TBR
        /// </summary>
        [BurstCompile]
        private static (float, float) CalculateTorsen(
            float inputTorque,
            float omegaLeft,
            float omegaRight,
            in VehicleConfig config)
        {
            float tbr = config.torsenTBR; // Torque Bias Ratio (typically 2.5-4.0)
            float halfTorque = inputTorque * 0.5f;
            
            // Determine which wheel has less traction (higher speed = less traction)
            float fasterWheel = omegaLeft > omegaRight ? 0 : 1;
            
            // Calculate max/min torque based on TBR
            // τ_max + τ_min = inputTorque
            // τ_max = τ_min × TBR
            // Solving: τ_min = inputTorque / (TBR + 1), τ_max = τ_min × TBR
            
            float minTorque = inputTorque / (tbr + 1f);
            float maxTorque = minTorque * tbr;
            
            if (fasterWheel == 0) // Left wheel spinning
            {
                return (minTorque, maxTorque);
            }
            else // Right wheel spinning
            {
                return (maxTorque, minTorque);
            }
        }
    }

    /// <summary>
    /// Complete drivetrain state
    /// </summary>
    public struct DrivetrainState
    {
        // Engine
        public float EngineRPM;
        public float EngineOmega; // rad/s
        public float EngineTorque;
        
        // Clutch
        public float ClutchEngagement;
        public float ClutchTorque;
        
        // Gearbox
        public int CurrentGear;
        public int TargetGear;
        public float ShiftProgress; // 0-1 during shift
        public bool IsShifting;
        public float ShiftTimeRemaining;
        
        // Differential
        public float DiffOmegaLeft;
        public float DiffOmegaRight;
        public float TorqueSplitLeft;
        public float TorqueSplitRight;
        
        // Wheels (output)
        public float WheelOmegaFL;
        public float WheelOmegaFR;
        public float WheelOmegaRL;
        public float WheelOmegaRR;
        public float DriveTorqueFL;
        public float DriveTorqueFR;
        public float DriveTorqueRL;
        public float DriveTorqueRR;
        
        // Drivetrain losses
        public float DrivetrainLossTorque;

        // Output to wheels
        public float OutputTorque;
    }

    /// <summary>
    /// Main drivetrain simulation class
    /// Integrates engine, clutch, gearbox, and differential
    /// </summary>
    [BurstCompile]
    public static class DrivetrainSystem
    {
        /// <summary>
        /// Update drivetrain state for one simulation step
        /// </summary>
        [BurstCompile]
        public static void Update(
            ref DrivetrainState state,
            float throttleInput,
            float clutchInput,
            float brakeInput,
            float dt,
            in VehicleConfig config,
            float engineHealth,
            float engineTemp,
            float oilTemp)
        {
            // 1. Update engine
            state.EngineTorque = EngineSim.CalculateEngineTorque(
                state.EngineRPM,
                throttleInput,
                config,
                engineHealth,
                engineTemp,
                oilTemp);
            
            // Add engine braking if off throttle
            if (throttleInput < 0.1f)
            {
                state.EngineTorque += EngineSim.CalculateEngineBraking(
                    state.EngineRPM, throttleInput, config);
            }
            
            // 2. Update clutch
            state.ClutchEngagement = ClutchSim.CalculateIdealEngagement(
                clutchInput,
                state.ShiftProgress,
                config);
            
            state.ClutchTorque = ClutchSim.CalculateClutchTorque(
                state.ClutchEngagement,
                state.EngineOmega,
                GetInputShaftOmega(state, config),
                config);
            
            // 3. Update engine angular velocity
            float engineInertia = config.engineMomentOfInertia;
            float engineAngularAccel = EngineSim.CalculateAngularAcceleration(
                state.EngineTorque,
                state.ClutchTorque,
                state.EngineOmega,
                engineInertia,
                config);
            
            state.EngineOmega += engineAngularAccel * dt;
            state.EngineRPM = state.EngineOmega * 60f / (2f * math.PI);
            
            // 4. Handle gear shifting
            HandleGearShifting(ref state, throttleInput, dt, config);
            
            // 5. Calculate gearbox output
            float gearboxOutputTorque = GearboxSim.CalculateOutputTorque(
                state.ClutchTorque,
                state.CurrentGear,
                state.IsShifting,
                config);
            
            float gearboxOutputOmega = GearboxSim.CalculateOutputOmega(
                state.EngineOmega,
                state.CurrentGear,
                config);
            
            // 6. Calculate differential torque split
            float diffInputTorque = gearboxOutputTorque;
            float diffInputOmega = gearboxOutputOmega;
            
            // Average wheel speeds for diff calculation
            float avgRearOmega = (state.WheelOmegaRL + state.WheelOmegaRR) * 0.5f;
            float frontOmegaAvg = (state.WheelOmegaFL + state.WheelOmegaFR) * 0.5f;
            
            if (config.drivetrainType == DrivetrainType.RWD || 
                config.drivetrainType == DrivetrainType.AWD)
            {
                var torqueSplit = DifferentialSim.CalculateTorqueSplit(
                    diffInputTorque,
                    state.WheelOmegaRL,
                    state.WheelOmegaRR,
                    config.differentialType,
                    config);
                
                state.TorqueSplitLeft = torqueSplit.torqueLeft;
                state.TorqueSplitRight = torqueSplit.torqueRight;
            }
            
            // 7. Apply drive torque to wheels based on drivetrain type
            ApplyDriveTorque(ref state, diffInputTorque, config);
            
            // 8. Calculate drivetrain losses
            state.DrivetrainLossTorque = math.abs(diffInputTorque) * (1f - config.gearboxEfficiency);
            state.OutputTorque = gearboxOutputTorque;
        }

        /// <summary>
        /// Handle automatic/manual gear shifting
        /// </summary>
        [BurstCompile]
        private static void HandleGearShifting(
            ref DrivetrainState state,
            float throttleInput,
            float dt,
            in VehicleConfig config)
        {
            if (state.IsShifting)
            {
                // Continue shift
                state.ShiftTimeRemaining -= dt;
                state.ShiftProgress = 1f - (state.ShiftTimeRemaining / config.shiftDuration);
                
                if (state.ShiftTimeRemaining <= 0f)
                {
                    // Shift complete
                    state.IsShifting = false;
                    state.CurrentGear = state.TargetGear;
                    state.ShiftProgress = 0f;
                    
                    // Match engine speed to new gear (simplified)
                    float targetOmega = GetWheelOmegaAverage(state) * 
                                       GearboxSim.GetTotalRatio(state.CurrentGear, config);
                    state.EngineOmega = targetOmega;
                }
            }
            else
            {
                // Check for shift requests
                if (config.transmissionType == TransmissionType.Automatic ||
                    config.transmissionType == TransmissionType.SemiAutomatic)
                {
                    if (GearboxSim.ShouldShiftUp(state.EngineRPM, state.CurrentGear, throttleInput, config))
                    {
                        RequestShift(ref state, state.CurrentGear + 1, config);
                    }
                    else if (GearboxSim.ShouldShiftDown(state.EngineRPM, state.CurrentGear, throttleInput, config))
                    {
                        RequestShift(ref state, state.CurrentGear - 1, config);
                    }
                }
            }
        }

        /// <summary>
        /// Request a gear shift
        /// </summary>
        [BurstCompile]
        private static void RequestShift(
            ref DrivetrainState state,
            int targetGear,
            in VehicleConfig config)
        {
            if (targetGear < 0 || targetGear > config.gearRatios.Length) return;
            if (targetGear == state.CurrentGear) return;
            
            state.TargetGear = targetGear;
            state.IsShifting = true;
            state.ShiftTimeRemaining = config.shiftDuration;
            state.ShiftProgress = 0f;
        }

        /// <summary>
        /// Apply drive torque to appropriate wheels based on drivetrain configuration
        /// </summary>
        [BurstCompile]
        private static void ApplyDriveTorque(
            ref DrivetrainState state,
            float diffTorque,
            in VehicleConfig config)
        {
            // Reset all drive torques
            state.DriveTorqueFL = 0f;
            state.DriveTorqueFR = 0f;
            state.DriveTorqueRL = 0f;
            state.DriveTorqueRR = 0f;
            
            float frontRatio = 0f;
            float rearRatio = 0f;
            
            switch (config.drivetrainType)
            {
                case DrivetrainType.FWD:
                    frontRatio = 1f;
                    break;
                    
                case DrivetrainType.RWD:
                    rearRatio = 1f;
                    break;
                    
                case DrivetrainType.AWD:
                    frontRatio = config.awdFrontBias;
                    rearRatio = 1f - config.awdFrontBias;
                    break;
            }
            
            // Apply front torque
            if (frontRatio > 0f)
            {
                float frontTorque = diffTorque * frontRatio * 0.5f;
                state.DriveTorqueFL = frontTorque;
                state.DriveTorqueFR = frontTorque;
            }
            
            // Apply rear torque
            if (rearRatio > 0f)
            {
                state.DriveTorqueRL = state.TorqueSplitLeft * rearRatio;
                state.DriveTorqueRR = state.TorqueSplitRight * rearRatio;
            }
        }

        /// <summary>
        /// Get input shaft omega (after clutch, before gearbox)
        /// </summary>
        [BurstCompile]
        private static float GetInputShaftOmega(DrivetrainState state, in VehicleConfig config)
        {
            // Simplified: use average driven wheel speed
            return GetWheelOmegaAverage(state);
        }

        /// <summary>
        /// Get average wheel omega for driven wheels
        /// </summary>
        [BurstCompile]
        private static float GetWheelOmegaAverage(DrivetrainState state)
        {
            // Simplified average - would need to know which wheels are driven
            return (state.WheelOmegaFL + state.WheelOmegaFR + 
                   state.WheelOmegaRL + state.WheelOmegaRR) * 0.25f;
        }
    }
}
