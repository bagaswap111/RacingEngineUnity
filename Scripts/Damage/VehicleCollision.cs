using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Impulse-based collision resolution for vehicle-to-vehicle collisions.
    /// 
    /// When two vehicles collide:
    ///   1. Detect contact point & normal
    ///   2. Calculate relative velocity at contact
    ///   3. Calculate impulse:
    ///      j = -(1 + e) × v_rel_n / (1/m_A + 1/m_B 
    ///          + (r_A × n)²/I_A + (r_B × n)²/I_B)
    ///   4. Apply impulse to linear and angular velocity
    ///   
    /// Friction at contact:
    ///   Tangential impulse with Coulomb friction model.
    ///   
    /// Anti-tunneling:
    ///   At high speed (> 200 km/h), CCD or sub-stepping needed.
    /// </summary>
    [BurstCompile]
    public struct CollisionConfig
    {
        public float Restitution;
        public float FrictionCoeff;
        public float ContactRadius;
        public float MaxImpulse;

        public static CollisionConfig Default()
        {
            return new CollisionConfig
            {
                Restitution = 0.2f,
                FrictionCoeff = 0.5f,
                ContactRadius = 0.5f,
                MaxImpulse = 50000f
            };
        }
    }

    public struct CollisionResult
    {
        public float3 Impulse;
        public float3 ContactPoint;
        public float3 Normal;
        public float RelativeVelocity;
        public bool Collided;
    }

    public static class VehicleCollision
    {
        public static CollisionResult ResolveCollision(
            float3 posA, float3 velA, float3 angVelA,
            float3 posB, float3 velB, float3 angVelB,
            float massA, float massB,
            float3 inertiaA, float3 inertiaB,
            float3 contactPoint, float3 normal,
            CollisionConfig config)
        {
            CollisionResult result = new CollisionResult
            {
                ContactPoint = contactPoint,
                Normal = normal,
                Collided = false
            };

            float3 rA = contactPoint - posA;
            float3 rB = contactPoint - posB;

            float3 vA = velA + math.cross(angVelA, rA);
            float3 vB = velB + math.cross(angVelB, rB);
            float3 vRel = vA - vB;

            float vRelN = math.dot(vRel, normal);

            if (vRelN > 0f)
                return result;

            float3 n = normal;
            float3 rAxN = math.cross(rA, n);
            float3 rBxN = math.cross(rB, n);

            float invInertiaA = (rAxN.x * rAxN.x / inertiaA.x)
                              + (rAxN.y * rAxN.y / inertiaA.y)
                              + (rAxN.z * rAxN.z / inertiaA.z);
            float invInertiaB = (rBxN.x * rBxN.x / inertiaB.x)
                              + (rBxN.y * rBxN.y / inertiaB.y)
                              + (rBxN.z * rBxN.z / inertiaB.z);

            float denom = (1f / massA + 1f / massB) + invInertiaA + invInertiaB;

            float j = -(1f + config.Restitution) * vRelN / denom;
            j = math.clamp(j, -config.MaxImpulse, config.MaxImpulse);

            float3 impulse = j * n;

            float3 tangent = vRel - n * vRelN;
            float tangentLen = math.length(tangent);
            if (tangentLen > 0.001f)
            {
                tangent /= tangentLen;
                float jT = -math.dot(vRel, tangent) / denom;
                float maxFriction = config.FrictionCoeff * math.abs(j);
                jT = math.clamp(jT, -maxFriction, maxFriction);
                impulse += tangent * jT;
            }

            result.Impulse = impulse;
            result.RelativeVelocity = vRelN;
            result.Collided = true;

            return result;
        }

        public static void ApplyImpulse(
            ref float3 velocity,
            ref float3 angularVelocity,
            float3 impulse,
            float mass,
            float3 inertia,
            float3 contactOffset)
        {
            velocity += impulse / mass;

            float3 angularImpulse = math.cross(contactOffset, impulse);
            angularVelocity += new float3(
                angularImpulse.x / inertia.x,
                angularImpulse.y / inertia.y,
                angularImpulse.z / inertia.z);
        }

        public static bool CheckTunneling(
            float3 posA, float3 velA,
            float3 posB, float3 velB,
            float radiusA, float radiusB,
            float dt)
        {
            float3 relativePos = posA - posB;
            float3 relativeVel = velA - velB;
            float combinedRadius = radiusA + radiusB;

            float relSpeed = math.length(relativeVel);
            if (relSpeed < 0.001f)
                return false;

            float3 relDir = relativeVel / relSpeed;
            float projDist = math.dot(relativePos, relDir);

            if (projDist < 0f)
                return false;

            float timeToImpact = (projDist - combinedRadius) / relSpeed;

            return timeToImpact < dt && timeToImpact > 0f;
        }
    }
}
