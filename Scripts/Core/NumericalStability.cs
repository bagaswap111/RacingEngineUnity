using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Numerical stability and error recovery utilities.
    /// Guards against NaN, Inf, force spikes, and simulation corruption.
    /// 
    /// Strategy:
    /// 1. NaN Detection — check every physics tick
    /// 2. Force Clamping — physical limits on all forces
    /// 3. Velocity Clamping — prevent explosion
    /// 4. Quaternion Renormalization — prevent rotation drift
    /// 5. Penetration Recovery — push objects apart
    /// 6. Fallback State — rollback ring buffer
    /// </summary>
    [BurstCompile]
    public static class NumericalStability
    {
        public const float MAX_VELOCITY = 150f;
        public const float MAX_ANGULAR_VELOCITY = 20f;
        public const float MAX_FORCE = 100000f;
        public const float MAX_TORQUE = 50000f;
        public const float MIN_SPEED_FOR_DIVISION = 0.1f;
        public const float NaN_CHECK_EPSILON = 1e-10f;

        [BurstCompile]
        public static bool IsFinite(float value)
        {
            return !math.isnan(value) && !math.isinf(value);
        }

        [BurstCompile]
        public static bool IsFinite(float3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        [BurstCompile]
        public static bool IsFinite(quaternion q)
        {
            return IsFinite(q.value.x) && IsFinite(q.value.y) &&
                   IsFinite(q.value.z) && IsFinite(q.value.w);
        }

        [BurstCompile]
        public static float ClampForce(float force, float maxForce = MAX_FORCE)
        {
            if (!IsFinite(force)) return 0f;
            return math.clamp(force, -maxForce, maxForce);
        }

        [BurstCompile]
        public static float3 ClampForce(float3 force, float maxForce = MAX_FORCE)
        {
            if (!IsFinite(force)) return float3.zero;
            float mag = math.length(force);
            if (mag > maxForce)
                return force / mag * maxForce;
            return force;
        }

        [BurstCompile]
        public static float3 ClampVelocity(float3 velocity)
        {
            if (!IsFinite(velocity)) return float3.zero;
            float speed = math.length(velocity);
            if (speed > MAX_VELOCITY)
                return velocity / speed * MAX_VELOCITY;
            return velocity;
        }

        [BurstCompile]
        public static float3 ClampAngularVelocity(float3 angularVelocity)
        {
            if (!IsFinite(angularVelocity)) return float3.zero;
            float speed = math.length(angularVelocity);
            if (speed > MAX_ANGULAR_VELOCITY)
                return angularVelocity / speed * MAX_ANGULAR_VELOCITY;
            return angularVelocity;
        }

        [BurstCompile]
        public static quaternion NormalizeSafe(quaternion q)
        {
            float len = math.length(q.value);
            if (len < NaN_CHECK_EPSILON)
                return quaternion.identity;
            return q / len;
        }

        [BurstCompile]
        public static float SafeDivide(float numerator, float denominator, float fallback = 0f)
        {
            if (math.abs(denominator) < NaN_CHECK_EPSILON)
                return fallback;
            float result = numerator / denominator;
            return IsFinite(result) ? result : fallback;
        }

        [BurstCompile]
        public static float3 SafeNormalize(float3 v, float3 fallback)
        {
            float len = math.length(v);
            if (len < NaN_CHECK_EPSILON)
                return fallback;
            return v / len;
        }

        [BurstCompile]
        public static float SafeSqrt(float value)
        {
            if (value < 0f) return 0f;
            float result = math.sqrt(value);
            return IsFinite(result) ? result : 0f;
        }

        [BurstCompile]
        public static float SafeAtan2(float y, float x)
        {
            if (math.abs(x) < NaN_CHECK_EPSILON && math.abs(y) < NaN_CHECK_EPSILON)
                return 0f;
            return math.atan2(y, x);
        }

        [BurstCompile]
        public static float ForceDamageCheck(
            float3 force,
            float3 position,
            float3 centerOfMass)
        {
            float torque = math.length(math.cross(position - centerOfMass, force));
            return SafeDivide(torque, MAX_TORQUE, 0f);
        }

        [BurstCompile]
        public static float PenetrationDepth(
            float3 position,
            float groundHeight,
            float objectRadius)
        {
            float lowestPoint = position.y - objectRadius;
            if (lowestPoint < groundHeight)
                return groundHeight - lowestPoint;
            return 0f;
        }

        [BurstCompile]
        public static float3 PenetrationCorrection(
            float3 position,
            float groundHeight,
            float objectRadius,
            float3 velocity,
            float correctionFactor = 0.8f)
        {
            float depth = PenetrationDepth(position, groundHeight, objectRadius);
            if (depth <= 0f) return float3.zero;

            float3 correction = new float3(0, depth, 0);
            float3 dampedVelocity = velocity * (1f - correctionFactor);

            return correction - dampedVelocity * 0.01f;
        }
    }
}
