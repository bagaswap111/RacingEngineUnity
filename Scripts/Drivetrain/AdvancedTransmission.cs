using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Drivetrain
{
    /// <summary>
    /// Advanced transmission physics with dog ring engagement,
    /// shift sequence, and driveline torsional vibration.
    /// 
    /// Dog Ring Engagement:
    ///   |ω_gear - ω_shaft| < engagement_threshold
    ///   If speed difference too high → gear clash / missed shift
    ///   Engagement time: 20-80 ms
    /// 
    /// Shift Sequence:
    ///   1. Clutch disengage (or throttle lift)
    ///   2. Current gear disengage
    ///   3. Neutral phase (50-100 ms)
    ///   4. Target gear engage
    ///   5. Clutch re-engage
    ///   
    ///   During shift: τ_engine = 0 (torque interruption)
    /// 
    /// Seamless Shift (F1/DCT):
    ///   Two shafts, pre-select next gear
    ///   Torque interruption < 20 ms
    /// </summary>
    [BurstCompile]
    public struct TransmissionConfig
    {
        public float[] GearRatios;
        public float FinalDriveRatio;
        public float ShiftTime;
        public float EngagementThreshold;
        public float ClutchEngagementTime;
        public float DrivelineStiffness;
        public float DrivelineDamping;

        public static TransmissionConfig Default()
        {
            var config = new TransmissionConfig
            {
                FinalDriveRatio = 3.5f,
                ShiftTime = 0.06f,
                EngagementThreshold = 500f,
                ClutchEngagementTime = 0.1f,
                DrivelineStiffness = 5000f,
                DrivelineDamping = 50f
            };

            config.GearRatios = new float[]
            {
                3.5f, 2.5f, 1.8f, 1.3f, 1.0f, 0.8f, 0.65f
            };

            return config;
        }
    }

    public enum ShiftState
    {
        Engaged,
        ClutchDisengaging,
        Neutral,
        ClutchEngaging,
        Failed
    }

    [BurstCompile]
    public struct TransmissionState
    {
        public int CurrentGear;
        public int TargetGear;
        public ShiftState ShiftState;
        public float ShiftTimer;
        public float ClutchPosition;
        public float InputShaftRPM;
        public float OutputShaftRPM;
        public float DrivelineTwist;
        public bool ShiftFailed;
    }

    [BurstCompile]
    public static class AdvancedTransmission
    {
        [BurstCompile]
        public static TransmissionState Update(
            TransmissionState state,
            TransmissionConfig config,
            float engineRPM,
            float wheelRPM,
            float clutchInput,
            float dt)
        {
            state.InputShaftRPM = engineRPM;
            state.OutputShaftRPM = wheelRPM * config.FinalDriveRatio;

            if (state.ShiftState == ShiftState.Engaged)
            {
                state.ClutchPosition = math.lerp(state.ClutchPosition, clutchInput, dt * 10f);
                state.DrivelineTwist = CalculateDrivelineTwist(
                    state.InputShaftRPM, state.OutputShaftRPM,
                    config.DrivelineStiffness, config.DrivelineDamping, dt);
            }
            else
            {
                state = ProcessShift(state, config, dt);
            }

            return state;
        }

        [BurstCompile]
        private static TransmissionState ProcessShift(
            TransmissionState state,
            TransmissionConfig config,
            float dt)
        {
            state.ShiftTimer += dt;

            switch (state.ShiftState)
            {
                case ShiftState.ClutchDisengaging:
                    state.ClutchPosition = math.max(0f, state.ClutchPosition - dt / 0.05f);
                    if (state.ClutchPosition <= 0f)
                    {
                        state.ShiftState = ShiftState.Neutral;
                        state.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.Neutral:
                    if (state.ShiftTimer > 0.05f)
                    {
                        state.ShiftState = ShiftState.ClutchEngaging;
                        state.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.ClutchEngaging:
                    state.ClutchPosition = math.min(1f, state.ClutchPosition + dt / config.ClutchEngagementTime);
                    if (state.ClutchPosition >= 1f)
                    {
                        state.ShiftState = ShiftState.Engaged;
                        state.CurrentGear = state.TargetGear;
                        state.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.Failed:
                    if (state.ShiftTimer > 0.5f)
                    {
                        state.ShiftState = ShiftState.Engaged;
                        state.ShiftTimer = 0f;
                        state.ShiftFailed = false;
                    }
                    break;
            }

            return state;
        }

        [BurstCompile]
        public static TransmissionState InitiateShift(
            TransmissionState state,
            TransmissionConfig config,
            int targetGear)
        {
            if (state.ShiftState != ShiftState.Engaged)
                return state;

            if (targetGear < 0 || targetGear >= config.GearRatios.Length)
                return state;

            state.TargetGear = targetGear;
            state.ShiftState = ShiftState.ClutchDisengaging;
            state.ShiftTimer = 0f;
            return state;
        }

        [BurstCompile]
        public static float CalculateDrivelineTwist(
            float inputRPM,
            float outputRPM,
            float stiffness,
            float damping,
            float dt)
        {
            float rpmDiff = inputRPM - outputRPM;
            float twistForce = stiffness * (rpmDiff * 0.001f);
            return twistForce * dt;
        }

        [BurstCompile]
        public static bool CanEngage(
            TransmissionState state,
            TransmissionConfig config)
        {
            float rpmDiff = math.abs(state.InputShaftRPM - state.OutputShaftRPM);
            return rpmDiff < config.EngagementThreshold;
        }

        [BurstCompile]
        public static float CalculateShiftCut(
            float rpm,
            float redlineRPM,
            float shiftPoint)
        {
            if (rpm > redlineRPM * shiftPoint)
                return 1f;
            return 0f;
        }
    }
}
