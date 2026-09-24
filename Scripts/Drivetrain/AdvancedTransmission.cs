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
            in TransmissionState state,
            in TransmissionConfig config,
            float engineRPM,
            float wheelRPM,
            float clutchInput,
            float dt)
        {
            TransmissionState result = state;
            result.InputShaftRPM = engineRPM;
            result.OutputShaftRPM = wheelRPM * config.FinalDriveRatio;

            if (result.ShiftState == ShiftState.Engaged)
            {
                result.ClutchPosition = math.lerp(result.ClutchPosition, clutchInput, math.saturate(dt * 10f));
                result.DrivelineTwist = CalculateDrivelineTwist(
                    result.InputShaftRPM, result.OutputShaftRPM,
                    config.DrivelineStiffness, config.DrivelineDamping, dt);
            }
            else
            {
                result = ProcessShift(result, config, dt);
            }

            return result;
        }

        [BurstCompile]
        private static TransmissionState ProcessShift(
            in TransmissionState state,
            in TransmissionConfig config,
            float dt)
        {
            TransmissionState result = state;
            result.ShiftTimer += dt;

            switch (result.ShiftState)
            {
                case ShiftState.ClutchDisengaging:
                    result.ClutchPosition = math.max(0f, result.ClutchPosition - dt / 0.05f);
                    if (result.ClutchPosition <= 0f)
                    {
                        result.ShiftState = ShiftState.Neutral;
                        result.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.Neutral:
                    if (result.ShiftTimer > 0.05f)
                    {
                        result.ShiftState = ShiftState.ClutchEngaging;
                        result.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.ClutchEngaging:
                    result.ClutchPosition = math.min(1f, result.ClutchPosition + dt / config.ClutchEngagementTime);
                    if (result.ClutchPosition >= 1f)
                    {
                        result.ShiftState = ShiftState.Engaged;
                        result.CurrentGear = result.TargetGear;
                        result.ShiftTimer = 0f;
                    }
                    break;

                case ShiftState.Failed:
                    if (result.ShiftTimer > 0.5f)
                    {
                        result.ShiftState = ShiftState.Engaged;
                        result.ShiftTimer = 0f;
                        result.ShiftFailed = false;
                    }
                    break;
            }

            return result;
        }

        [BurstCompile]
        public static TransmissionState InitiateShift(
            in TransmissionState state,
            in TransmissionConfig config,
            int targetGear)
        {
            if (state.ShiftState != ShiftState.Engaged)
                return state;

            if (targetGear < 0 || targetGear >= config.GearRatios.Length)
                return state;

            TransmissionState result = state;
            result.TargetGear = targetGear;
            result.ShiftState = ShiftState.ClutchDisengaging;
            result.ShiftTimer = 0f;
            return result;
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
            float twistForce = stiffness * (rpmDiff * 0.001f) + damping * (rpmDiff * 0.001f);
            return twistForce * dt;
        }

        [BurstCompile]
        public static bool CanEngage(
            in TransmissionState state,
            in TransmissionConfig config)
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
