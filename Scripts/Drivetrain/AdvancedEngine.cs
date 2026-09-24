using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Drivetrain
{
    /// <summary>
    /// Advanced engine physics with turbo, supercharger, engine braking,
    /// flywheel inertia, rev limiter, and anti-lag.
    /// 
    /// Turbo Lag Model:
    ///   τ_turbine = exhaust_gas_force × turbine_efficiency
    ///   boost_pressure += (τ_compressor / I_turbo_shaft 
    ///                      - boost_pressure × wastegate_flow) × dt
    ///   τ_engine = τ_naturally_aspirated × (1 + boost_pressure × turbo_gain)
    ///   
    ///   Turbo lag: 0.5-2 seconds to full boost
    ///   Boost threshold: ~3000-4000 RPM
    /// 
    /// Engine Braking:
    ///   τ_engine_brake = τ_compression + τ_friction + τ_pumping
    ///   τ_compression = V_displacement × compression_ratio × f(RPM)
    ///   
    /// Flywheel & Rotating Inertia:
    ///   I_total = I_flywheel + I_crankshaft + I_pistons + I_clutch + I_gearbox_input
    ///   
    /// Rev Limiter:
    ///   IF RPM > RPM_redline: fuel_cut = true, τ_engine = 0
    /// </summary>
    [BurstCompile]
    public struct AdvancedEngineConfig
    {
        public float Displacement;
        public float RedlineRPM;
        public float IdleRPM;
        public float StallRPM;
        public float CompressionRatio;
        public float FlywheelInertia;
        public float CrankshaftInertia;
        public float PistonInertia;
        public float FrictionCoeff;
        public float PumpingCoeff;
        public float RevLimiterHysteresis;

        public static AdvancedEngineConfig Default()
        {
            return new AdvancedEngineConfig
            {
                Displacement = 2.4f,
                RedlineRPM = 15000f,
                IdleRPM = 4500f,
                StallRPM = 800f,
                CompressionRatio = 12.5f,
                FlywheelInertia = 0.15f,
                CrankshaftInertia = 0.08f,
                PistonInertia = 0.05f,
                FrictionCoeff = 0.02f,
                PumpingCoeff = 0.015f,
                RevLimiterHysteresis = 200f
            };
        }
    }

    [BurstCompile]
    public struct TurboConfig
    {
        public bool HasTurbo;
        public float TurboGain;
        public float MaxBoostPressure;
        public float TurboInertia;
        public float WastegateFlow;
        public float BoostThresholdRPM;
        public float TurboLag;

        public static TurboConfig Default()
        {
            return new TurboConfig
            {
                HasTurbo = false,
                TurboGain = 0.5f,
                MaxBoostPressure = 1.5f,
                TurboInertia = 0.01f,
                WastegateFlow = 0.8f,
                BoostThresholdRPM = 3500f,
                TurboLag = 1.0f
            };
        }
    }

    [BurstCompile]
    public struct EngineState
    {
        public float RPM;
        public float TorqueOutput;
        public float EngineBrakeTorque;
        public float BoostPressure;
        public float TurboRPM;
        public bool RevLimiterActive;
        public bool EngineRunning;
        public bool AntiLagActive;
    }

    [BurstCompile]
    public static class AdvancedEngine
    {
        public const float RPM_TO_RADS = 0.10472f;

        [BurstCompile]
        public static EngineState Update(
            EngineState state,
            AdvancedEngineConfig config,
            TurboConfig turbo,
            float throttle,
            float loadTorque,
            float dt)
        {
            if (!state.EngineRunning)
            {
                state.TorqueOutput = 0f;
                return state;
            }

            float rpmNorm = state.RPM / config.RedlineRPM;
            float naTorque = CalculateNATorque(rpmNorm, config.Displacement);

            if (turbo.HasTurbo && state.RPM > turbo.BoostThresholdRPM)
            {
                float targetBoost = throttle * turbo.MaxBoostPressure;
                float boostRate = (targetBoost - state.BoostPressure) / turbo.TurboLag;
                state.BoostPressure += boostRate * dt;
                state.BoostPressure = math.clamp(state.BoostPressure, 0f, turbo.MaxBoostPressure);

                state.TurboRPM += (state.BoostPressure * 100000f / turbo.TurboInertia
                                  - state.TurboRPM * turbo.WastegateFlow) * dt;
                state.TurboRPM = math.clamp(state.TurboRPM, 0f, 150000f);

                naTorque *= (1f + state.BoostPressure * turbo.TurboGain);
            }
            else
            {
                state.BoostPressure = math.max(0f, state.BoostPressure - dt * 2f);
                state.TurboRPM = math.max(0f, state.TurboRPM - dt * 50000f);
            }

            state.EngineBrakeTorque = CalculateEngineBraking(state.RPM, config);

            bool wasRevLimiterActive = state.RevLimiterActive;
            state.RevLimiterActive = false;
            if (state.RPM > config.RedlineRPM)
            {
                state.RevLimiterActive = true;
                naTorque = 0f;
                state.RPM -= (state.RPM - config.RedlineRPM) * dt * 10f;
            }
            else if (state.RPM > config.RedlineRPM - config.RevLimiterHysteresis && wasRevLimiterActive)
            {
                state.RevLimiterActive = true;
                naTorque = 0f;
            }

            if (throttle < 0.01f && state.RPM > config.IdleRPM)
            {
                state.TorqueOutput = naTorque - state.EngineBrakeTorque;
            }
            else
            {
                state.TorqueOutput = naTorque;
            }

            float inertia = config.FlywheelInertia + config.CrankshaftInertia + config.PistonInertia;
            float angularAccel = (state.TorqueOutput - loadTorque) / inertia;
            state.RPM += angularAccel * dt * (1f / RPM_TO_RADS);

            state.RPM = math.clamp(state.RPM, 0f, config.RedlineRPM + 500f);

            if (state.RPM < config.StallRPM && throttle < 0.1f)
            {
                state.EngineRunning = false;
            }

            return state;
        }

        [BurstCompile]
        private static float CalculateNATorque(float rpmNorm, float displacement)
        {
            float peakTorque = displacement * 1000f;
            float torqueCurve = rpmNorm * (1f - rpmNorm * 0.8f);
            return peakTorque * math.max(0f, torqueCurve);
        }

        [BurstCompile]
        private static float CalculateEngineBraking(float rpm, AdvancedEngineConfig config)
        {
            float rpmNorm = rpm / config.RedlineRPM;
            float compressionBrake = config.Displacement * config.CompressionRatio * rpmNorm * 10f;
            float frictionBrake = rpmNorm * config.FrictionCoeff * 1000f;
            float pumpingBrake = rpmNorm * config.PumpingCoeff * rpm;
            return compressionBrake + frictionBrake + pumpingBrake;
        }

        [BurstCompile]
        public static float CalculateIdleControl(
            float currentRPM,
            float targetRPM,
            float throttle,
            float dt)
        {
            if (throttle > 0.05f) return throttle;

            float rpmError = targetRPM - currentRPM;
            float idleThrottle = rpmError * 0.001f;
            return math.clamp(idleThrottle, 0f, 0.3f);
        }

        [BurstCompile]
        public static float CalculateRevMatch(
            float currentRPM,
            float targetRPM,
            float blipDuration)
        {
            float rpmDiff = targetRPM - currentRPM;
            return math.clamp(rpmDiff * 0.0005f, 0f, 1f);
        }
    }
}
