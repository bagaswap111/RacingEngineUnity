using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Electronics
{
    /// <summary>
    /// PID Controller for TC, ABS, and other closed-loop systems
    /// Implements: u = Kp·e + Ki·∫e·dt + Kd·de/dt
    /// </summary>
    [BurstCompile]
    public struct PIDController
    {
        public float Kp;  // Proportional gain
        public float Ki;  // Integral gain
        public float Kd;  // Derivative gain
        
        private float integralAccumulator;
        private float previousError;
        private float integralClamp;
        
        public PIDController(float kp, float ki, float kd, float integralClamp = 1f)
        {
            Kp = kp;
            Ki = ki;
            Kd = kd;
            this.integralAccumulator = 0f;
            this.previousError = 0f;
            this.integralClamp = integralClamp;
        }

        /// <summary>
        /// Calculate PID output
        /// </summary>
        [BurstCompile]
        public float Update(float error, float dt)
        {
            // Proportional term
            float pTerm = Kp * error;
            
            // Integral term with anti-windup
            integralAccumulator += error * dt;
            integralAccumulator = math.clamp(integralAccumulator, -integralClamp, integralClamp);
            float iTerm = Ki * integralAccumulator;
            
            // Derivative term
            float derivative = (error - previousError) / dt;
            float dTerm = Kd * derivative;
            
            previousError = error;
            
            return pTerm + iTerm + dTerm;
        }

        /// <summary>
        /// Reset integrator
        /// </summary>
        [BurstCompile]
        public void Reset()
        {
            integralAccumulator = 0f;
            previousError = 0f;
        }

        /// <summary>
        /// Set gains
        /// </summary>
        [BurstCompile]
        public void SetGains(float kp, float ki, float kd)
        {
            Kp = kp;
            Ki = ki;
            Kd = kd;
        }
    }

    /// <summary>
    /// Traction Control System
    /// Reduces engine torque when wheel slip exceeds target
    /// </summary>
    [BurstCompile]
    public static class TractionControl
    {
        /// <summary>
        /// Calculate throttle reduction based on wheel slip
        /// </summary>
        [BurstCompile]
        public static (float correctedThrottle, float tcReduction, bool tcActive) Calculate(
            float[] wheelSlipRatios,  // κ for each wheel
            int[] drivenWheels,       // Indices of driven wheels
            float throttleInput,
            float tcLevel,            // 0.0 - 1.0 (intervention aggressiveness)
            in VehicleConfig config,
            ref PIDController pid)
        {
            if (tcLevel <= 0f || drivenWheels.Length == 0)
            {
                return (throttleInput, 0f, false);
            }

            // Calculate average slip ratio of driven wheels
            float sumSlip = 0f;
            for (int i = 0; i < drivenWheels.Length; i++)
            {
                sumSlip += wheelSlipRatios[drivenWheels[i]];
            }
            float avgSlip = sumSlip / drivenWheels.Length;

            // Target slip ratio (optimal for acceleration)
            float targetSlip = config.tcTargetSlip * (1f - tcLevel * 0.3f); // More aggressive = lower target

            // Calculate error
            float error = avgSlip - targetSlip;

            // Only intervene if slip is too high
            if (error <= 0f)
            {
                return (throttleInput, 0f, false);
            }

            // PID controller for throttle reduction
            float dt = 1f / 240f; // Fixed timestep
            float tcIntervention = pid.Update(error, dt);

            // Scale by TC level
            tcIntervention *= tcLevel;

            // Clamp intervention
            tcIntervention = math.saturate(tcIntervention);

            // Apply throttle reduction
            float correctedThrottle = throttleInput * (1f - tcIntervention);

            return (correctedThrottle, tcIntervention, true);
        }

        /// <summary>
        /// Alternative: Direct engine torque reduction
        /// </summary>
        [BurstCompile]
        public static (float correctedTorque, float torqueReduction, bool tcActive) CalculateTorqueCut(
            float[] wheelSlipRatios,
            int[] drivenWheels,
            float engineTorque,
            float tcLevel,
            in VehicleConfig config,
            ref PIDController pid)
        {
            if (tcLevel <= 0f || drivenWheels.Length == 0)
            {
                return (engineTorque, 0f, false);
            }

            // Calculate average slip
            float sumSlip = 0f;
            for (int i = 0; i < drivenWheels.Length; i++)
            {
                sumSlip += wheelSlipRatios[drivenWheels[i]];
            }
            float avgSlip = sumSlip / drivenWheels.Length;

            float targetSlip = config.tcTargetSlip;
            float error = avgSlip - targetSlip;

            if (error <= 0f)
            {
                return (engineTorque, 0f, false);
            }

            float dt = 1f / 240f;
            float reduction = pid.Update(error, dt);
            reduction *= tcLevel;
            reduction = math.saturate(reduction);

            float correctedTorque = engineTorque * (1f - reduction);

            return (correctedTorque, reduction, true);
        }
    }

    /// <summary>
    /// Anti-lock Braking System
    /// Modulates brake pressure to prevent wheel lockup
    /// </summary>
    [BurstCompile]
    public static class ABSSystem
    {
        /// <summary>
        /// Calculate brake pressure for each wheel independently
        /// </summary>
        [BurstCompile]
        public static (float[] pressures, float absReduction, bool absActive) Calculate(
            float[] wheelSlipRatios,  // κ for each wheel (negative during braking)
            float brakeInput,         // 0.0 - 1.0
            float maxBrakePressure,   // Maximum hydraulic pressure
            float absLevel,           // 0.0 - 1.0
            in VehicleConfig config,
            ref PIDController[] wheelPIDs)  // One PID per wheel
        {
            float[] pressures = new float[4];
            float totalReduction = 0f;
            bool absActive = false;

            if (absLevel <= 0f || brakeInput <= 0.01f)
            {
                // No ABS or no braking - full pressure
                for (int i = 0; i < 4; i++)
                {
                    pressures[i] = brakeInput * maxBrakePressure;
                }
                return (pressures, 0f, false);
            }

            // Target slip ratio for optimal braking
            float targetSlip = config.absTargetSlip; // Typically -0.10 to -0.15

            for (int i = 0; i < 4; i++)
            {
                float slip = wheelSlipRatios[i];
                
                // Check if wheel is approaching lockup (slip too negative)
                if (slip < targetSlip)
                {
                    absActive = true;
                    
                    // Error: how much slip exceeds target
                    float error = slip - targetSlip;
                    
                    // PID controller for pressure reduction
                    float dt = 1f / 240f;
                    float reduction = wheelPIDs[i].Update(error, dt);
                    
                    // Scale by ABS level
                    reduction *= absLevel;
                    reduction = math.saturate(reduction);
                    
                    // Calculate brake pressure
                    float basePressure = brakeInput * maxBrakePressure;
                    pressures[i] = basePressure * (1f - reduction);
                    
                    totalReduction += reduction;
                }
                else
                {
                    // Normal braking
                    pressures[i] = brakeInput * maxBrakePressure;
                    
                    // Reset PID for this wheel
                    wheelPIDs[i].Reset();
                }
            }

            float avgReduction = totalReduction / 4f;
            return (pressures, avgReduction, absActive);
        }

        /// <summary>
        /// Simplified ABS: single threshold-based intervention
        /// </summary>
        [BurstCompile]
        public static (float[] pressures, bool absActive) CalculateSimple(
            float[] wheelSlipRatios,
            float brakeInput,
            float maxBrakePressure,
            in VehicleConfig config)
        {
            float[] pressures = new float[4];
            bool absActive = false;

            float lockupThreshold = config.absSlipThreshold; // e.g., -0.20

            for (int i = 0; i < 4; i++)
            {
                if (wheelSlipRatios[i] < lockupThreshold)
                {
                    absActive = true;
                    // Pulse brake pressure (simplified ABS cycling)
                    float pulsePhase = (float)(System.DateTime.Now.Ticks % 10000000) / 10000000f;
                    float pulseFactor = 0.5f + 0.5f * math.sin(pulsePhase * math.PI * 2f);
                    pressures[i] = brakeInput * maxBrakePressure * pulseFactor;
                }
                else
                {
                    pressures[i] = brakeInput * maxBrakePressure;
                }
            }

            return (pressures, absActive);
        }
    }

    /// <summary>
    /// Electronic Brakeforce Distribution
    /// Optimizes front/rear brake bias based on conditions
    /// </summary>
    [BurstCompile]
    public static class EBDSytem
    {
        /// <summary>
        /// Calculate optimal brake bias based on deceleration and load transfer
        /// </summary>
        [BurstCompile]
        public static float CalculateOptimalBias(
            float longitudinalAccel,  // m/s² (negative during braking)
            float lateralAccel,       // m/s²
            float vehicleSpeed,
            in VehicleConfig config)
        {
            // Base brake bias from config
            float baseBias = config.brakeBiasFront;

            // Load transfer during braking shifts weight forward
            // More forward weight = can apply more front brake
            float loadTransfer = (-longitudinalAccel) * config.cgHeight / config.wheelbase;
            
            // Adjust bias based on load transfer
            float biasAdjustment = loadTransfer * config.ebdLoadTransferGain;

            // Reduce rear bias under heavy braking to prevent lockup
            float brakingIntensity = math.abs(longitudinalAccel) / 10f; // Normalize to ~10m/s²
            biasAdjustment += brakingIntensity * config.ebdBrakingGain;

            // Account for lateral load transfer (cornering)
            float corneringFactor = math.abs(lateralAccel) / 10f;
            biasAdjustment -= corneringFactor * config.ebdCorneringGain * math.sign(lateralAccel);

            // Apply limits
            float finalBias = baseBias + biasAdjustment;
            finalBias = math.clamp(finalBias, config.minBrakeBias, config.maxBrakeBias);

            return finalBias;
        }
    }

    /// <summary>
    /// Launch Control System
    /// Optimizes acceleration from standstill
    /// </summary>
    [BurstCompile]
    public static class LaunchControl
    {
        /// <summary>
        /// Calculate throttle limit for optimal launch
        /// </summary>
        [BurstCompile]
        public static float CalculateLaunchThrottle(
            float vehicleSpeed,
            float engineRPM,
            float clutchEngagement,
            bool launchControlActive,
            in VehicleConfig config)
        {
            if (!launchControlActive)
            {
                return 1f; // Full throttle
            }

            // Target RPM for optimal launch
            float targetRPM = config.launchControlRPM;

            // Below target RPM: limit throttle to prevent bogging
            if (engineRPM < targetRPM)
            {
                float rpmError = (targetRPM - engineRPM) / targetRPM;
                float throttleLimit = 1f - rpmError * config.launchControlSensitivity;
                return math.max(0.3f, throttleLimit);
            }

            // At/above target RPM: allow full throttle but monitor slip
            return 1f;
        }

        /// <summary>
        /// Calculate optimal clutch engagement for launch
        /// </summary>
        [BurstCompile]
        public static float CalculateLaunchClutch(
            float vehicleSpeed,
            float engineRPM,
            float wheelSlipAvg,
            bool launchControlActive,
            in VehicleConfig config)
        {
            if (!launchControlActive)
            {
                return 1f; // Fully engaged
            }

            // Standstill: engage clutch progressively
            if (vehicleSpeed < 1f)
            {
                // Engage based on RPM
                float engagement = engineRPM / config.launchControlRPM;
                engagement = math.saturate(engagement * 0.8f); // Don't fully engage until moving
                return engagement;
            }

            // Moving: monitor slip
            if (wheelSlipAvg > config.launchControlMaxSlip)
            {
                // Disengage slightly to reduce slip
                return 0.7f;
            }

            return 1f; // Fully engaged
        }
    }

    /// <summary>
    /// Shift Light / Rev Warning
    /// </summary>
    [BurstCompile]
    public static class ShiftLightSystem
    {
        /// <summary>
        /// Determine shift light state
        /// </summary>
        [BurstCompile]
        public static (bool shouldShift, float intensity, int activeLights) Calculate(
            float engineRPM,
            in VehicleConfig config)
        {
            float redline = config.engineRedlineRPM;
            float shiftPoint = redline - config.shiftLightEarlyMargin;
            
            // Number of LEDs (typically 10-12)
            int numLights = config.numShiftLights;
            
            // RPM range for shift lights
            float startRPM = config.shiftLightStartRPM;
            float range = redline - startRPM;
            
            // Calculate how many lights should be on
            float normalizedRPM = (engineRPM - startRPM) / range;
            normalizedRPM = math.saturate(normalizedRPM);
            
            int activeLights = (int)(normalizedRPM * numLights);
            activeLights = math.clamp(activeLights, 0, numLights);
            
            // Should shift?
            bool shouldShift = engineRPM >= shiftPoint;
            
            // Intensity (for LED brightness)
            float intensity = normalizedRPM;
            
            return (shouldShift, intensity, activeLights);
        }
    }

    /// <summary>
    /// Complete electronics state
    /// </summary>
    public struct ElectronicsState
    {
        // Traction Control
        public bool TCActive;
        public float TCReduction;
        public float TCLevel;
        
        // ABS
        public bool ABSActive;
        public float ABSReduction;
        public float[] BrakePressures; // Per wheel
        
        // EBD
        public float CurrentBrakeBias;
        
        // Launch Control
        public bool LaunchControlActive;
        public float LaunchThrottleLimit;
        
        // Shift Lights
        public bool ShiftRequested;
        public int ActiveShiftLights;
        public float ShiftLightIntensity;
        
        // Engine Maps
        public int CurrentEngineMap; // 0=Normal, 1=Sport, 2=Wet, 3=Quali
        public float FuelMixture;    // AFR target
        public float IgnitionTiming; // Degrees BTDC
    }
}
