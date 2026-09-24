using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Vehicle-to-environment collision response.
    /// Handles walls, barriers, kerbs, gravel, and grass surfaces.
    /// 
    /// Wall/Barrier types:
    ///   Concrete: e = 0.3, μ = 0.4
    ///   Tire wall: e = 0.05, μ = 0.8, deformation
    ///   Guardrail: e = 0.1, μ = 0.3, redirect along rail
    ///   
    /// Kerb interaction:
    ///   Kerb as raised surface with height profile.
    ///   Wheel hits kerb → vertical impulse.
    ///   
    /// Gravel trap:
    ///   High rolling resistance, deceleration force.
    ///   
    /// Grass:
    ///   Low grip, μ ≈ 0.25-0.35
    /// </summary>
    public enum SurfaceType
    {
        Asphalt,
        Concrete,
        TireWall,
        Guardrail,
        Kerb,
        Gravel,
        Grass,
        GrassWet,
        Astroturf,
        Sand
    }

    [BurstCompile]
    public struct SurfaceConfig
    {
        public float Restitution;
        public float FrictionCoeff;
        public float RollingResistance;
        public float DeformationRate;
        public float EnergyAbsorption;

        public static SurfaceConfig Default(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.Concrete:
                    return new SurfaceConfig
                    {
                        Restitution = 0.3f, FrictionCoeff = 0.4f,
                        RollingResistance = 0.015f, DeformationRate = 0.1f,
                        EnergyAbsorption = 0.3f
                    };
                case SurfaceType.TireWall:
                    return new SurfaceConfig
                    {
                        Restitution = 0.05f, FrictionCoeff = 0.8f,
                        RollingResistance = 0.1f, DeformationRate = 0.8f,
                        EnergyAbsorption = 0.9f
                    };
                case SurfaceType.Guardrail:
                    return new SurfaceConfig
                    {
                        Restitution = 0.1f, FrictionCoeff = 0.3f,
                        RollingResistance = 0.02f, DeformationRate = 0.3f,
                        EnergyAbsorption = 0.7f
                    };
                case SurfaceType.Kerb:
                    return new SurfaceConfig
                    {
                        Restitution = 0.4f, FrictionCoeff = 0.6f,
                        RollingResistance = 0.025f, DeformationRate = 0.05f,
                        EnergyAbsorption = 0.2f
                    };
                case SurfaceType.Gravel:
                    return new SurfaceConfig
                    {
                        Restitution = 0.05f, FrictionCoeff = 0.5f,
                        RollingResistance = 0.15f, DeformationRate = 0f,
                        EnergyAbsorption = 0.5f
                    };
                case SurfaceType.Grass:
                    return new SurfaceConfig
                    {
                        Restitution = 0.1f, FrictionCoeff = 0.3f,
                        RollingResistance = 0.08f, DeformationRate = 0f,
                        EnergyAbsorption = 0.1f
                    };
                case SurfaceType.GrassWet:
                    return new SurfaceConfig
                    {
                        Restitution = 0.05f, FrictionCoeff = 0.2f,
                        RollingResistance = 0.1f, DeformationRate = 0f,
                        EnergyAbsorption = 0.1f
                    };
                default:
                    return new SurfaceConfig
                    {
                        Restitution = 0.2f, FrictionCoeff = 0.7f,
                        RollingResistance = 0.015f, DeformationRate = 0f,
                        EnergyAbsorption = 0.2f
                    };
            }
        }
    }

    public struct EnvironmentCollisionResult
    {
        public float3 NormalForce;
        public float3 FrictionForce;
        public float EnergyAbsorbed;
        public float DamageAmount;
        public bool Collided;
    }

    public static class EnvironmentCollision
    {
        public static EnvironmentCollisionResult ResolveWallCollision(
            float3 velocity,
            float3 angularVelocity,
            float3 contactPoint,
            float3 wallNormal,
            float mass,
            SurfaceConfig surface,
            float dt)
        {
            EnvironmentCollisionResult result = new EnvironmentCollisionResult();

            float velNormal = math.dot(velocity, wallNormal);
            if (velNormal >= 0f)
                return result;

            float3 normal = wallNormal;
            float impulse = -(1f + surface.Restitution) * velNormal;
            impulse = math.min(impulse, 50f);

            result.NormalForce = normal * impulse * mass;

            float3 velTangent = velocity - normal * velNormal;
            float tangentSpeed = math.length(velTangent);
            if (tangentSpeed > 0.01f)
            {
                float3 tangentDir = velTangent / tangentSpeed;
                float frictionMag = surface.FrictionCoeff * math.abs(impulse) * mass;
                result.FrictionForce = -tangentDir * math.min(frictionMag, tangentSpeed * mass);
            }

            result.EnergyAbsorbed = 0.5f * mass * velNormal * velNormal * surface.EnergyAbsorption;
            result.Collided = true;

            return result;
        }

        public static float CalculateKerbForce(
            float wheelPosition,
            float kerbHeight,
            float wheelVelocity,
            float verticalLoad,
            float springRate,
            float damperRate)
        {
            if (wheelPosition > kerbHeight)
                return 0f;

            float kerbPenetration = kerbHeight - wheelPosition;
            float kerbForce = springRate * kerbPenetration * 0.5f;
            kerbForce += damperRate * math.abs(wheelVelocity) * 0.3f;

            return kerbForce;
        }

        public static float CalculateGravelDrag(
            float speed,
            float verticalLoad,
            SurfaceConfig gravel)
        {
            float rollingResistance = gravel.RollingResistance * verticalLoad;
            float aeroDrag = 0.5f * 1.225f * 2.0f * speed * speed * 0.3f;
            return rollingResistance + aeroDrag;
        }

        public static float GetSurfaceGripMultiplier(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.Asphalt: return 1.0f;
                case SurfaceType.Concrete: return 0.9f;
                case SurfaceType.Kerb: return 0.85f;
                case SurfaceType.Gravel: return 0.6f;
                case SurfaceType.Grass: return 0.4f;
                case SurfaceType.GrassWet: return 0.25f;
                case SurfaceType.Astroturf: return 0.5f;
                case SurfaceType.Sand: return 0.35f;
                default: return 1.0f;
            }
        }
    }
}
