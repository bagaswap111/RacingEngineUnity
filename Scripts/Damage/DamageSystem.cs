using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Vehicle;

namespace RacingSim.Damage
{
    /// <summary>
    /// Damage state for a component
    /// </summary>
    public enum DamageState
    {
        Perfect = 0,      // health > 0.90
        Damaged = 1,      // 0.60 < health ≤ 0.90
        Critical = 2,     // 0.30 < health ≤ 0.60
        Failing = 3,      // 0.10 < health ≤ 0.30
        Destroyed = 4     // health ≤ 0.10
    }

    /// <summary>
    /// Visual mesh deformation from collision
    /// Implements vertex displacement based on impact energy
    /// </summary>
    [BurstCompile]
    public static class VisualDamageSystem
    {
        /// <summary>
        /// Calculate mesh deformation from impact
        /// </summary>
        [BurstCompile]
        public static void ApplyDeformation(
            NativeArray<float3> vertices,
            NativeArray<float3> normals,
            float3 collisionPoint,
            float3 collisionNormal,
            float impactEnergy,
            float deformRadius,
            float maxDeformation,
            ref NativeArray<float3> originalVertices)
        {
            // Save original vertices on first impact
            if (originalVertices.Length == 0)
            {
                originalVertices = new NativeArray<float3>(vertices.Length, Allocator.Persistent);
                for (int i = 0; i < vertices.Length; i++)
                {
                    originalVertices[i] = vertices[i];
                }
            }

            float deformRadiusSq = deformRadius * deformRadius;

            for (int i = 0; i < vertices.Length; i++)
            {
                float3 vertex = vertices[i];
                float distanceSq = math.distancesq(vertex, collisionPoint);

                if (distanceSq < deformRadiusSq)
                {
                    float distance = math.sqrt(distanceSq);
                    
                    // Quadratic falloff
                    float falloff = 1f - (distance / deformRadius);
                    falloff = falloff * falloff;

                    // Calculate displacement magnitude
                    float displacementMag = impactEnergy * falloff * 0.001f;
                    displacementMag = math.min(displacementMag, maxDeformation);

                    // Add some random noise for realism
                    float noise = (math.sin(i * 12.9898f) * 0.5f + 0.5f) * 0.3f;
                    float3 displacement = collisionNormal * displacementMag * (1f + noise * 0.2f);

                    // Apply deformation
                    vertices[i] = originalVertices[i] + displacement;
                }
            }

            // Recalculate normals
            RecalculateNormals(vertices, ref normals);
        }

        /// <summary>
        /// Simple normal recalculation (simplified for burst)
        /// </summary>
        [BurstCompile]
        private static void RecalculateNormals(NativeArray<float3> vertices, ref NativeArray<float3> normals)
        {
            // Simplified: use vertex position differences
            // Full implementation would need triangle indices
            for (int i = 0; i < vertices.Length; i++)
            {
                // Placeholder: keep original normal direction
                normals[i] = math.normalize(vertices[i]);
            }
        }

        /// <summary>
        /// Calculate impact energy from collision
        /// E = 0.5 × m_effective × V²
        /// </summary>
        [BurstCompile]
        public static float CalculateImpactEnergy(
            float relativeVelocity,
            float effectiveMass,
            float collisionAngle)
        {
            // Effective mass based on collision angle
            float angleFactor = math.abs(math.cos(collisionAngle));
            float adjustedMass = effectiveMass * (0.5f + 0.5f * angleFactor);

            return 0.5f * adjustedMass * relativeVelocity * relativeVelocity;
        }
    }

    /// <summary>
    /// Mechanical damage system for vehicle components
    /// TODO: [ARCH-001] Wire to DamageConfig asset when created.
    /// Current VehicleConfig lacks damage fields (engineRedlineRPM,
    /// engineDamageOverrevRate, etc.). See architectural decision doc.
    /// </summary>
    [BurstCompile]
    public static class MechanicalDamageSystem
    {
        /// <summary>
        /// Update engine damage based on various factors
        /// </summary>
        [BurstCompile]
        public static float UpdateEngineDamage(
            float currentHealth,
            float rpm,
            float engineTemp,
            float oilTemp,
            float throttle,
            float dt,
            in VehicleConfig config,
            out bool isMisfiring)
        {
            float damage = 0f;
            isMisfiring = false;

            // Over-rev damage
            if (rpm > config.engineRedlineRPM)
            {
                float overrevAmount = (rpm - config.engineRedlineRPM) / 1000f;
                damage += config.engineDamageOverrevRate * overrevAmount * dt;
            }

            // Overheat damage
            if (engineTemp > config.engineTempOptimalMax)
            {
                float overheatAmount = (engineTemp - config.engineTempOptimalMax);
                damage += config.engineDamageOverheatRate * (overheatAmount / 50f) * dt;
            }

            // Oil pressure damage (low oil temp = poor lubrication)
            if (oilTemp < config.optimalOilTempMin && rpm > config.engineIdleRPM * 2f)
            {
                damage += config.engineDamageColdRate * dt;
            }

            // Update health
            currentHealth -= damage;
            currentHealth = math.max(0f, currentHealth);

            // Check for misfire
            if (currentHealth < 0.3f)
            {
                isMisfiring = true;
            }

            return currentHealth;
        }

        /// <summary>
        /// Update suspension damage
        /// </summary>
        [BurstCompile]
        public static float UpdateSuspensionDamage(
            float currentHealth,
            float verticalLoad,
            float suspensionTravel,
            float bumpStopCompression,
            in VehicleConfig config)
        {
            float damage = 0f;

            // Overload damage
            if (verticalLoad > config.suspensionMaxLoad)
            {
                float overloadRatio = verticalLoad / config.suspensionMaxLoad;
                damage += config.suspensionDamageOverloadRate * (overloadRatio - 1f);
            }

            // Bottoming out damage
            if (bumpStopCompression > 0f)
            {
                damage += config.suspensionDamageBottomingRate * bumpStopCompression;
            }

            // Impact damage from curbs/kerbs
            if (suspensionTravel > config.suspensionTravelMax * 0.95f)
            {
                damage += config.suspensionDamageOvertravelRate;
            }

            currentHealth -= damage;
            return math.max(0f, currentHealth);
        }

        /// <summary>
        /// Update tire damage and wear
        /// </summary>
        [BurstCompile]
        public static (float wear, bool punctured) UpdateTireDamage(
            float currentWear,
            float temperature,
            float verticalLoad,
            float slipRatio,
            float slipAngle,
            float distanceTraveled,
            bool isLocked,
            in VehicleConfig config)
        {
            float wearRate = 0f;
            bool punctured = false;

            // Base wear from distance
            wearRate += config.tireWearBaseRate * distanceTraveled;

            // Wear from slip (aggressive driving)
            float slipSeverity = math.abs(slipRatio) + math.abs(math.sin(slipAngle));
            wearRate += config.tireWearSlipRate * slipSeverity;

            // Wear from overheating
            if (temperature > config.tireOverheatThreshold)
            {
                float overheatFactor = (temperature - config.tireOverheatThreshold) / 50f;
                wearRate *= (1f + overheatFactor * 2f);
            }

            // Flat spot from locked braking
            if (isLocked)
            {
                wearRate += config.tireWearFlatSpotRate;
            }

            // Check for puncture/blowout
            if (verticalLoad > config.tireMaxLoad || temperature > config.tireBurstTemperature)
            {
                float punctureChance = 0f;
                
                if (verticalLoad > config.tireMaxLoad)
                {
                    punctureChance += (verticalLoad / config.tireMaxLoad - 1f) * 0.1f;
                }
                
                if (temperature > config.tireBurstTemperature)
                {
                    punctureChance += (temperature - config.tireBurstTemperature) / 100f * 0.2f;
                }

                // Deterministic puncture check (in real game, use random)
                if (punctureChance > 0.5f)
                {
                    punctured = true;
                }
            }

            currentWear += wearRate;
            currentWear = math.min(1f, currentWear);

            return (currentWear, punctured);
        }

        /// <summary>
        /// Update aerodynamic damage
        /// TODO: [ARCH-001] Remove string param (Burst incompatible),
        /// use enum or int zone ID instead.
        /// </summary>
        [BurstCompile]
        public static (float frontDamage, float rearDamage) UpdateAeroDamage(
            float frontDamage,
            float rearDamage,
            float3 collisionPoint,
            float collisionForce,
            string collisionZone,
            in VehicleConfig config)
        {
            if (collisionForce < config.aeroDamageThreshold)
            {
                return (frontDamage, rearDamage);
            }

            float damageAmount = collisionForce * config.aeroDamageCoefficient;

            // Determine which aero elements are damaged
            if (collisionZone.Contains("front") || collisionZone.Contains("wing_f"))
            {
                frontDamage += damageAmount;
                frontDamage = math.min(1f, frontDamage);
            }

            if (collisionZone.Contains("rear") || collisionZone.Contains("wing_r"))
            {
                rearDamage += damageAmount;
                rearDamage = math.min(1f, rearDamage);
            }

            // Floor/underbody damage affects both
            if (collisionZone.Contains("floor") || collisionZone.Contains("under"))
            {
                frontDamage += damageAmount * 0.5f;
                rearDamage += damageAmount * 0.5f;
                frontDamage = math.min(1f, frontDamage);
                rearDamage = math.min(1f, rearDamage);
            }

            return (frontDamage, rearDamage);
        }

        /// <summary>
        /// Get damage state from health value
        /// </summary>
        [BurstCompile]
        public static DamageState GetDamageState(float health)
        {
            if (health > 0.9f) return DamageState.Perfect;
            if (health > 0.6f) return DamageState.Damaged;
            if (health > 0.3f) return DamageState.Critical;
            if (health > 0.1f) return DamageState.Failing;
            return DamageState.Destroyed;
        }

        /// <summary>
        /// Calculate performance penalty from damage
        /// </summary>
        [BurstCompile]
        public static float CalculatePerformancePenalty(float health, DamageState state)
        {
            switch (state)
            {
                case DamageState.Perfect:
                    return 0f;
                case DamageState.Damaged:
                    return 0.1f * (1f - health);
                case DamageState.Critical:
                    return 0.3f + 0.3f * (0.6f - health) / 0.3f;
                case DamageState.Failing:
                    return 0.6f + 0.3f * (0.3f - health) / 0.2f;
                case DamageState.Destroyed:
                    return 1f;
                default:
                    return 0f;
            }
        }
    }

    /// <summary>
    /// Complete damage state for vehicle
    /// </summary>
    public struct DamageStateData
    {
        // Component health (0.0 - 1.0)
        public float EngineHealth;
        public float GearboxHealth;
        public float ClutchHealth;
        
        public float SuspensionHealthFL, SuspensionHealthFR;
        public float SuspensionHealthRL, SuspensionHealthRR;
        
        public float TireWearFL, TireWearFR, TireWearRL, TireWearRR;
        public bool TirePuncturedFL, TirePuncturedFR, TirePuncturedRL, TirePuncturedRR;
        
        public float BrakeWearFL, BrakeWearFR, BrakeWearRL, BrakeWearRR;
        
        public float AeroDamageFront;
        public float AeroDamageRear;
        
        // Body damage (visual)
        public float BodyDamageNose;
        public float BodyDamageTail;
        public float BodyDamageSideL;
        public float BodyDamageSideR;
        public float BodyDamageFloor;
        
        // State enums
        public DamageState EngineState;
        public DamageState GearboxState;
        public DamageState SuspensionStateFL, SuspensionStateFR;
        public DamageState SuspensionStateRL, SuspensionStateRR;
        
        // Effects flags
        public bool EngineMisfiring;
        public bool EngineSmoking;
        public bool SuspensionBrokenFL, SuspensionBrokenFR;
        public bool SuspensionBrokenRL, SuspensionBrokenRR;
        public bool AeroFlapping;
        
        // Collision tracking
        public float TotalImpactEnergy;
        public int CollisionCount;
        public float LastCollisionTime;
        public float3 LastCollisionPoint;
        public float3 LastCollisionNormal;
    }
}
