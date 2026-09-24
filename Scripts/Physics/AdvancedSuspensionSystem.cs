using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Core;

namespace RacingSim.Physics
{
    /// <summary>
    /// Advanced suspension system integrating kinematics, forces, roll dynamics,
    /// bushing compliance, and ground detection.
    /// Target: < 0.5ms for 4 wheels on GTX 7 series.
    /// </summary>
    public class AdvancedSuspensionSystem : MonoBehaviour
    {
        [Header("Suspension Configs (per wheel)")]
        public SuspensionConfig frontLeftConfig;
        public SuspensionConfig frontRightConfig;
        public SuspensionConfig rearLeftConfig;
        public SuspensionConfig rearRightConfig;

        [Header("Vehicle Parameters")]
        public float vehicleMass = 1400f;
        public float cgHeight = 0.45f;
        public float wheelbase = 2.6f;
        public float distToFrontAxle = 1.3f;
        public float trackWidth = 1.6f;
        public float rollInertia = 500f;
        public float rollDamping = 2000f;

        [Header("System References")]
        public GroundDetection groundDetection;
        public IVehicleInput inputProvider;

        [Header("Runtime State")]
        public SuspensionState[] wheelStates = new SuspensionState[4];
        public float[] travels = new float[4];
        public float[] travelVelocities = new float[4];
        public float rollAngle;
        public float rollVelocity;

        private Rigidbody vehicleRigidbody;
        private float[] prevTravels = new float[4];
        private float3 prevLocalVelocity;

        private void Awake()
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
            if (groundDetection == null)
                groundDetection = GetComponentInChildren<GroundDetection>();
            if (inputProvider == null)
                inputProvider = new LegacyInputProvider();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float steerInput = GetSteerInput();
            float throttleInput = GetThrottleInput();
            float brakeInput = GetBrakeInput();

            UpdateAllWheels(dt, steerInput, throttleInput, brakeInput);
        }

        public void UpdateAllWheels(float dt, float steerInput, float throttleInput, float brakeInput)
        {
            float longAccel = CalculateLongitudinalAccel(throttleInput, brakeInput);
            float latAccel = CalculateLateralAccel();

            float rollStiffFront = RollDynamics.CalculateRollStiffnessFront(
                frontLeftConfig.SpringRate, frontRightConfig.SpringRate,
                trackWidth, frontLeftConfig.ARBRate, frontLeftConfig.ARBRatio);

            float rollStiffRear = RollDynamics.CalculateRollStiffnessRear(
                rearLeftConfig.SpringRate, rearRightConfig.SpringRate,
                trackWidth, rearLeftConfig.ARBRate, rearLeftConfig.ARBRatio);

            float totalRollStiff = rollStiffFront + rollStiffRear;

            float targetRoll = RollDynamics.CalculateRollAngle(
                latAccel, vehicleMass, cgHeight, totalRollStiff);

            float newRollAngle = RollDynamics.CalculateDynamicRoll(
                rollAngle, rollVelocity,
                latAccel, vehicleMass, cgHeight,
                totalRollStiff, rollDamping, rollInertia, dt);

            rollVelocity = dt > 0.0001f ? (newRollAngle - rollAngle) / dt : 0f;
            rollAngle = newRollAngle;

            LoadTransferResult loadTransfer = RollDynamics.CalculateLoadTransfer(
                vehicleMass, longAccel, latAccel, cgHeight,
                wheelbase, distToFrontAxle,
                trackWidth, trackWidth,
                wheelStates[0].RollCenterHeight, wheelStates[2].RollCenterHeight);

            float staticWeight = vehicleMass * 9.81f / 4f;

            for (int i = 0; i < 4; i++)
            {
                SuspensionConfig config = GetConfig(i);
                bool isFront = i < 2;
                bool isLeft = i % 2 == 0;

                GroundHitData groundHit = DetectGroundForWheel(i);

                float targetTravel = 0f;
                if (groundHit.DidHit)
                {
                    targetTravel = (VehicleConstants.DEFAULT_WHEEL_RADIUS - groundHit.Distance) * 1000f;
                }

                targetTravel = math.clamp(targetTravel, -config.MaxDroopTravel, config.MaxBumpTravel);

                float suspensionResponse = 0.8f;
                travels[i] = math.lerp(travels[i], targetTravel, suspensionResponse);
                travelVelocities[i] = (travels[i] - prevTravels[i]) / dt;
                prevTravels[i] = travels[i];

                float lateralForce = CalculateLateralForceForWheel(i);
                float longForce = CalculateLongitudinalForceForWheel(i);

                float3 bushingOffset = BushingCompliance.CalculateDeflection(
                    lateralForce, longForce,
                    config.BushingRadialStiffness, config.BushingAxialStiffness,
                    config.BushingComplianceFactor);

                wheelStates[i] = SuspensionKinematics.Solve(
                    config, travels[i], steerInput, rollAngle);

                wheelStates[i].BushingOffset = bushingOffset;

                float camberCompliance = BushingCompliance.CalculateCamberCompliance(
                    lateralForce, config.BushingRadialStiffness, config.BushingComplianceFactor);
                float toeCompliance = BushingCompliance.CalculateToeCompliance(
                    longForce, config.BushingAxialStiffness, config.BushingComplianceFactor);

                wheelStates[i].Camber += camberCompliance * config.CamberComplianceFactor;
                wheelStates[i].Toe += toeCompliance * config.ToeComplianceFactor;

                float springForce = SuspensionForces.CalculateSpringForce(
                    travels[i], config.SpringRate,
                    config.SpringInstalledLength - config.SpringFreeLength,
                    wheelStates[i].MotionRatio,
                    config.IsProgressiveSpring, config.ProgressiveRate);

                float damperForce = SuspensionForces.CalculateDamperForce(
                    travelVelocities[i], config.Damper, 40f, 0f);

                float oppositeTravel = GetOppositeWheelTravel(i);
                float arbForce = SuspensionForces.CalculateARBForce(
                    travels[i], oppositeTravel,
                    config.ARBRate, config.ARBPreload, config.ARBRatio);

                float bumpStopForce = SuspensionForces.CalculateBumpStopForce(
                    travels[i], config.BumpStopEngageTravel,
                    config.BumpStopStiffness, config.BumpStopExponent, config.BumpStopLength);

                float droopStopForce = SuspensionForces.CalculateDroopStopForce(
                    travels[i], config.MaxDroopTravel, config.DroopStopStiffness);

                float totalForce = SuspensionForces.CalculateTotalForce(
                    springForce, damperForce, arbForce, bumpStopForce, droopStopForce);

                wheelStates[i].SpringForce = springForce;
                wheelStates[i].DamperForce = damperForce;
                wheelStates[i].ARBForce = arbForce;
                wheelStates[i].BumpStopForce = bumpStopForce;
                wheelStates[i].DroopStopForce = droopStopForce;
                wheelStates[i].TotalForce = totalForce;

                float wheelLoad = RollDynamics.GetLoadPerWheel(
                    staticWeight,
                    isFront ? loadTransfer.LongitudinalFront : loadTransfer.LongitudinalRear,
                    isLeft ? loadTransfer.LateralFront : loadTransfer.LateralRear,
                    isFront, isLeft);

                wheelLoad += config.UnsprungMass * 9.81f;
                wheelStates[i].VerticalLoad = math.max(wheelLoad, 0f);

                wheelStates[i].IsGrounded = groundHit.DidHit;
                wheelStates[i].GroundHeight = groundHit.DidHit ? groundHit.Distance : maxDistance;

                ApplyForcesToRigidbody(i, wheelStates[i]);
            }
        }

        private GroundHitData DetectGroundForWheel(int wheelIndex)
        {
            if (groundDetection == null)
            {
                return new GroundHitData
                {
                    DidHit = true,
                    Distance = VehicleConstants.DEFAULT_WHEEL_RADIUS,
                    Normal = (float3)Vector3.up,
                    SurfaceGrip = 1f
                };
            }

            Vector3 hubPos = transform.TransformPoint(GetHubLocalPos(wheelIndex));
            return groundDetection.DetectGround(hubPos, Vector3.down);
        }

        private Vector3 GetHubLocalPos(int wheelIndex)
        {
            SuspensionConfig config = GetConfig(wheelIndex);
            return (Vector3)config.WheelCenter;
        }

        private SuspensionConfig GetConfig(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return frontLeftConfig;
                case 1: return frontRightConfig;
                case 2: return rearLeftConfig;
                case 3: return rearRightConfig;
                default: return frontLeftConfig;
            }
        }

        private float GetOppositeWheelTravel(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return travels[1];
                case 1: return travels[0];
                case 2: return travels[3];
                case 3: return travels[2];
                default: return 0f;
            }
        }

        private float CalculateLongitudinalAccel(float throttle, float brake)
        {
            if (vehicleRigidbody == null) return 0f;
            Vector3 localVel = transform.InverseTransformDirection(vehicleRigidbody.velocity);
            float currentZ = localVel.z;
            float dt = Time.fixedDeltaTime;
            float accel = dt > 0.0001f ? (currentZ - prevLocalVelocity.z) / dt : 0f;
            prevLocalVelocity.z = currentZ;
            return accel;
        }

        private float CalculateLateralAccel()
        {
            if (vehicleRigidbody == null) return 0f;
            Vector3 localVel = transform.InverseTransformDirection(vehicleRigidbody.velocity);
            float currentX = localVel.x;
            float dt = Time.fixedDeltaTime;
            float accel = dt > 0.0001f ? (currentX - prevLocalVelocity.x) / dt : 0f;
            prevLocalVelocity.x = currentX;
            return accel;
        }

        private float CalculateLateralForceForWheel(int wheelIndex)
        {
            return wheelStates[wheelIndex].LateralForce;
        }

        private float CalculateLongitudinalForceForWheel(int wheelIndex)
        {
            return wheelStates[wheelIndex].LongitudinalForce;
        }

        private float GetSteerInput()
        {
            return inputProvider != null ? inputProvider.Steering : 0f;
        }

        private float GetThrottleInput()
        {
            return inputProvider != null ? inputProvider.Throttle : 0f;
        }

        private float GetBrakeInput()
        {
            return inputProvider != null ? inputProvider.Brake : 0f;
        }

        private void ApplyForcesToRigidbody(int wheelIndex, SuspensionState state)
        {
            if (vehicleRigidbody == null || !state.IsGrounded) return;

            Vector3 forceDir = (Vector3)state.WheelNormal;
            float forceMag = state.TotalForce + state.VerticalLoad;

            Vector3 forcePos = transform.TransformPoint((Vector3)state.ContactPatchPos);

            vehicleRigidbody.AddForceAtPosition(forceDir * forceMag, forcePos, ForceMode.Force);
        }

        public SuspensionState GetWheelState(int index)
        {
            if (index < 0 || index >= 4) return new SuspensionState();
            return wheelStates[index];
        }

        public float GetTotalRollStiffness()
        {
            float front = RollDynamics.CalculateRollStiffnessFront(
                frontLeftConfig.SpringRate, frontRightConfig.SpringRate,
                trackWidth, frontLeftConfig.ARBRate, frontLeftConfig.ARBRatio);
            float rear = RollDynamics.CalculateRollStiffnessRear(
                rearLeftConfig.SpringRate, rearRightConfig.SpringRate,
                trackWidth, rearLeftConfig.ARBRate, rearLeftConfig.ARBRatio);
            return front + rear;
        }

        public float GetRollDistribution()
        {
            float front = RollDynamics.CalculateRollStiffnessFront(
                frontLeftConfig.SpringRate, frontRightConfig.SpringRate,
                trackWidth, frontLeftConfig.ARBRate, frontLeftConfig.ARBRatio);
            float rear = RollDynamics.CalculateRollStiffnessRear(
                rearLeftConfig.SpringRate, rearRightConfig.SpringRate,
                trackWidth, rearLeftConfig.ARBRate, rearLeftConfig.ARBRatio);
            return RollDynamics.CalculateRollDistribution(front, rear);
        }

        private float maxDistance = 1.5f;
    }
}
