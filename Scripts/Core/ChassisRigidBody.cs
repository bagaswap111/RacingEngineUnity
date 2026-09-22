using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Core
{
    /// <summary>
    /// 6-DOF rigid body integration for the vehicle chassis.
    /// Uses semi-implicit Euler integration at configurable tick rate.
    /// 
    /// Integration method: Semi-implicit Euler
    ///   v(t+dt) = v(t) + a(t) * dt
    ///   x(t+dt) = x(t) + v(t+dt) * dt  (note: uses NEW velocity)
    ///   
    ///   This is first-order but symplectic (preserves energy better
    ///   than explicit Euler). Standard for real-time physics.
    /// 
    /// Quaternion integration:
    ///   q̇ = 0.5 * q * ω_quaternion
    ///   q(t+dt) = normalize(q(t) + q̇ * dt)
    ///   
    ///   Always renormalize to prevent drift.
    /// 
    /// Sub-stepping:
    ///   Physics runs at fixed tick rate (default 240 Hz).
    ///   Render runs at variable frame rate (60 Hz typical).
    ///   Multiple physics steps per render frame if needed.
    ///   Accumulator pattern prevents spiral of death.
    /// </summary>
    public class ChassisRigidBody : MonoBehaviour
    {
        [Header("Integration Settings")]
        public float physicsTickRate = 240f;
        public int maxStepsPerFrame = 8;

        [Header("Vehicle Parameters")]
        public float mass = 1400f;
        public float3 inertiaDiagonal = new float3(500f, 1500f, 2000f);

        [Header("State")]
        public float3 position;
        public quaternion rotation = quaternion.identity;
        public float3 velocity;
        public float3 angularVelocity;

        [Header("Debug")]
        public bool showForces = false;

        private float3 accumulatedForce;
        private float3 accumulatedTorque;
        private float accumulator;
        private float fixedDeltaTime;
        private float3 previousPosition;
        private quaternion previousRotation;
        private float3 lastValidPosition;
        private quaternion lastValidRotation;
        private float3 lastValidVelocity;
        private float3 lastValidAngularVelocity;
        private int validStateIndex;
        private const int RING_BUFFER_SIZE = 16;
        private float3[] positionBuffer = new float3[RING_BUFFER_SIZE];
        private quaternion[] rotationBuffer = new quaternion[RING_BUFFER_SIZE];
        private float3[] velocityBuffer = new float3[RING_BUFFER_SIZE];
        private float3[] angularVelocityBuffer = new float3[RING_BUFFER_SIZE];

        public float FixedDeltaTime => fixedDeltaTime;
        public float3 Forward => math.mul(rotation, CoordinateSystem.VehicleForward);
        public float3 Right => math.mul(rotation, CoordinateSystem.VehicleLeft);
        public float3 Up => math.mul(rotation, CoordinateSystem.VehicleUp);

        private void Awake()
        {
            fixedDeltaTime = 1f / physicsTickRate;
            position = transform.position;
            rotation = transform.rotation;
            SaveValidState();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            accumulator += dt;

            int steps = 0;
            while (accumulator >= fixedDeltaTime && steps < maxStepsPerFrame)
            {
                StepPhysics(fixedDeltaTime);
                accumulator -= fixedDeltaTime;
                steps++;
            }

            if (steps >= maxStepsPerFrame)
                accumulator = 0f;

            ApplyStateToTransform();
        }

        private void StepPhysics(float dt)
        {
            previousPosition = position;
            previousRotation = rotation;

            float3 acceleration = accumulatedForce / mass;
            velocity += acceleration * dt;

            float3 angularAcceleration = angularVelocity != float3.zero
                ? new float3(
                    accumulatedTorque.x / inertiaDiagonal.x,
                    accumulatedTorque.y / inertiaDiagonal.y,
                    accumulatedTorque.z / inertiaDiagonal.z)
                : float3.zero;

            angularVelocity += angularAcceleration * dt;

            position += velocity * dt;

            quaternion omegaQuat = new quaternion(
                angularVelocity.x * 0.5f,
                angularVelocity.y * 0.5f,
                angularVelocity.z * 0.5f,
                0f);
            quaternion qDot = math.mul(omegaQuat, rotation);
            rotation = math.normalize(rotation + qDot * dt);

            ClampVelocity();
            ClampAngularVelocity();

            accumulatedForce = float3.zero;
            accumulatedTorque = float3.zero;

            if (IsStateValid())
            {
                SaveValidState();
            }
            else
            {
                RollbackToValidState();
            }
        }

        public void AddForce(float3 force, ForceMode mode = ForceMode.Force)
        {
            switch (mode)
            {
                case ForceMode.Force:
                    accumulatedForce += force;
                    break;
                case ForceMode.Impulse:
                    velocity += force / mass;
                    break;
                case ForceMode.Acceleration:
                    accumulatedForce += force * mass;
                    break;
                case ForceMode.VelocityChange:
                    velocity += force;
                    break;
            }
        }

        public void AddForceAtPosition(float3 force, float3 worldPosition, ForceMode mode = ForceMode.Force)
        {
            AddForce(force, mode);
            float3 leverArm = worldPosition - position;
            float3 torque = math.cross(leverArm, force);
            AddTorque(torque, mode);
        }

        public void AddTorque(float3 torque, ForceMode mode = ForceMode.Force)
        {
            switch (mode)
            {
                case ForceMode.Force:
                    accumulatedTorque += torque;
                    break;
                case ForceMode.Impulse:
                    angularVelocity += new float3(
                        torque.x / inertiaDiagonal.x,
                        torque.y / inertiaDiagonal.y,
                        torque.z / inertiaDiagonal.z);
                    break;
                case ForceMode.Acceleration:
                    accumulatedTorque += torque * mass;
                    break;
                case ForceMode.VelocityChange:
                    angularVelocity += torque;
                    break;
            }
        }

        private void ClampVelocity()
        {
            float speed = math.length(velocity);
            if (speed > 150f)
                velocity = velocity / speed * 150f;

            if (math.any(math.isnan(velocity)) || math.any(math.isinf(velocity)))
                RollbackToValidState();
        }

        private void ClampAngularVelocity()
        {
            float angSpeed = math.length(angularVelocity);
            if (angSpeed > 20f)
                angularVelocity = angularVelocity / angSpeed * 20f;

            if (math.any(math.isnan(angularVelocity)) || math.any(math.isinf(angularVelocity)))
                RollbackToValidState();
        }

        private bool IsStateValid()
        {
            if (math.any(math.isnan(position)) || math.any(math.isinf(position)))
                return false;
            if (math.any(math.isnan(rotation.value)) || math.any(math.isinf(rotation.value)))
                return false;
            if (math.any(math.isnan(velocity)) || math.any(math.isinf(velocity)))
                return false;
            if (math.any(math.isnan(angularVelocity)) || math.any(math.isinf(angularVelocity)))
                return false;
            if (math.lengthsq(rotation.value) < 0.9f)
                return false;
            return true;
        }

        private void SaveValidState()
        {
            positionBuffer[validStateIndex] = position;
            rotationBuffer[validStateIndex] = rotation;
            velocityBuffer[validStateIndex] = velocity;
            angularVelocityBuffer[validStateIndex] = angularVelocity;
            validStateIndex = (validStateIndex + 1) % RING_BUFFER_SIZE;
        }

        private void RollbackToValidState()
        {
            int idx = (validStateIndex - 1 + RING_BUFFER_SIZE) % RING_BUFFER_SIZE;
            position = positionBuffer[idx];
            rotation = rotationBuffer[idx];
            velocity = velocityBuffer[idx];
            angularVelocity = angularVelocityBuffer[idx];

            velocity *= 0.5f;
            angularVelocity *= 0.5f;

            Debug.LogWarning($"[ChassisRigidBody] NaN/Inf detected, rolled back to valid state");
        }

        public void PenetrationRecovery(float3 correction, float damping = 0.8f)
        {
            position += correction;
            velocity *= damping;
        }

        public void GroundContactCorrection(float groundHeight, float contactRadius)
        {
            float wheelY = position.y;
            if (wheelY < groundHeight)
            {
                float penetration = groundHeight - wheelY;
                PenetrationRecovery(new float3(0, penetration, 0));
            }
        }

        private void ApplyStateToTransform()
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        public StateSnapshot CaptureState()
        {
            return new StateSnapshot
            {
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                AngularVelocity = angularVelocity
            };
        }

        public void RestoreState(StateSnapshot snapshot)
        {
            position = snapshot.Position;
            rotation = snapshot.Rotation;
            velocity = snapshot.Velocity;
            angularVelocity = snapshot.AngularVelocity;
            ApplyStateToTransform();
        }

        public struct StateSnapshot
        {
            public float3 Position;
            public quaternion Rotation;
            public float3 Velocity;
            public float3 AngularVelocity;
        }
    }
}
