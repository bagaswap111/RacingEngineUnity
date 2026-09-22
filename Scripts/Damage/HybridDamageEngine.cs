using UnityEngine;
using System.Collections.Generic;

namespace RacingSim.Damage
{
    /// <summary>
    /// Main manager for the hybrid soft-body damage engine.
    /// Coordinates all damage zones, LOD, performance mapping, and repair.
    /// Target: < 1ms per frame on GTX 750 Ti.
    /// </summary>
    public class HybridDamageEngine : MonoBehaviour
    {
        [Header("System References")]
        public CollisionHandler collisionHandler;
        public DamagePerformanceLink performanceLink;
        public RepairSystem repairSystem;
        public DamageLODManager lodManager;

        [Header("Zone Configuration")]
        public DamageZoneConfig[] zoneConfigs;

        [Header("Performance")]
        public float debrisCleanupInterval = 5f;

        private DamageZone[] damageZones;
        private List<FractureZone> fracturedZones = new List<FractureZone>();
        private float cleanupTimer;
        private bool isInitialized;

        public float TotalDamage { get; private set; }
        public int ActiveZoneCount { get; private set; }

        private void Awake()
        {
            if (collisionHandler == null)
                collisionHandler = GetComponent<CollisionHandler>();
            if (performanceLink == null)
                performanceLink = GetComponent<DamagePerformanceLink>();
            if (repairSystem == null)
                repairSystem = GetComponent<RepairSystem>();
            if (lodManager == null)
                lodManager = GetComponent<DamageLODManager>();
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            damageZones = GetComponentsInChildren<DamageZone>();

            foreach (var zone in damageZones)
            {
                if (zone == null) continue;

                if (zoneConfigs != null)
                {
                    foreach (var cfg in zoneConfigs)
                    {
                        if (cfg.ZoneName == zone.config.ZoneName)
                        {
                            zone.Initialize(cfg);
                            break;
                        }
                    }
                }

                if (zone is FractureZone fracture)
                {
                    fracturedZones.Add(fracture);
                }
            }

            if (collisionHandler != null)
                collisionHandler.RegisterZones(damageZones);
            if (repairSystem != null)
                repairSystem.RegisterZones(damageZones);
            if (lodManager != null)
                lodManager.RegisterZones(damageZones);

            isInitialized = true;
        }

        private void FixedUpdate()
        {
            if (!isInitialized) return;

            float dt = Time.fixedDeltaTime;

            UpdateLOD();
            UpdateActiveZones(dt);
            UpdateDamageToPerformance(dt);
            CleanupExpiredDebris();
        }

        private void UpdateLOD()
        {
            if (lodManager == null) return;

            Transform cam = Camera.main?.transform;
            if (cam != null)
            {
                lodManager.UpdateLOD(cam.position, transform.position);
            }
        }

        private void UpdateActiveZones(float dt)
        {
            if (damageZones == null) return;

            ActiveZoneCount = 0;

            foreach (var zone in damageZones)
            {
                if (zone == null || !zone.IsActive) continue;

                if (lodManager != null && !lodManager.ShouldUpdateNodeBeam())
                    continue;

                zone.UpdateZone(dt);
                ActiveZoneCount++;
            }
        }

        private void UpdateDamageToPerformance(float dt)
        {
            if (performanceLink == null || damageZones == null) return;

            performanceLink.UpdateDamage(damageZones);
        }

        private void CleanupExpiredDebris()
        {
            cleanupTimer += Time.fixedDeltaTime;
            if (cleanupTimer < debrisCleanupInterval) return;

            cleanupTimer = 0f;

            if (collisionHandler != null)
            {
                collisionHandler.CleanupExpiredDebris();
            }
        }

        /// <summary>
        /// Apply impact at a world point with force and direction.
        /// Useful for non-physics-triggered damage (e.g., rolling over debris).
        /// </summary>
        public void ApplyDirectImpact(Vector3 worldPoint, Vector3 force, Vector3 normal)
        {
            if (collisionHandler == null) return;

            float speed = force.magnitude;
            float energy = 0.5f * 1000f * speed * speed;

            ImpactData impact = new ImpactData
            {
                ImpactPoint = (Unity.Mathematics.float3)worldPoint,
                ImpactNormal = (Unity.Mathematics.float3)normal,
                ImpactVelocity = (Unity.Mathematics.float3)force,
                ImpactSpeed = speed,
                ImpactEnergy = energy,
                EffectiveMass = 1000f,
                HitZoneIndex = -1
            };

            collisionHandler.DistributeImpact(impact);
        }

        /// <summary>
        /// Start vehicle repair.
        /// </summary>
        public void StartRepair(int level)
        {
            if (repairSystem != null)
            {
                repairSystem.StartRepair(level);
            }
        }

        /// <summary>
        /// Get the total normalized damage across all zones.
        /// </summary>
        public float GetTotalDamage()
        {
            if (damageZones == null || damageZones.Length == 0) return 0f;

            float total = 0f;
            foreach (var zone in damageZones)
            {
                if (zone != null)
                    total += zone.GetDamageNormalized();
            }

            return total / damageZones.Length;
        }

        /// <summary>
        /// Get damage for a specific zone by name.
        /// </summary>
        public float GetZoneDamage(string zoneName)
        {
            if (damageZones == null) return 0f;

            foreach (var zone in damageZones)
            {
                if (zone != null && zone.config.ZoneName == zoneName)
                {
                    return zone.GetDamageNormalized();
                }
            }

            return 0f;
        }

        /// <summary>
        /// Get damage for a specific zone by index.
        /// </summary>
        public float GetZoneDamage(int index)
        {
            if (damageZones == null || index < 0 || index >= damageZones.Length) return 0f;
            if (damageZones[index] == null) return 0f;

            return damageZones[index].GetDamageNormalized();
        }

        /// <summary>
        /// Get all fracture zones that have detached.
        /// </summary>
        public List<FractureZone> GetFracturedZones()
        {
            return fracturedZones;
        }

        /// <summary>
        /// Reset all damage (used for race restart).
        /// </summary>
        public void ResetAllDamage()
        {
            if (damageZones == null) return;

            foreach (var zone in damageZones)
            {
                if (zone != null)
                    zone.ResetToOriginal();
            }

            fracturedZones.Clear();
            TotalDamage = 0f;

            if (performanceLink != null)
                performanceLink.Reset();
        }

        /// <summary>
        /// Get a summary report of the vehicle damage state.
        /// </summary>
        public DamageReport GetDamageReport()
        {
            DamageReport report = new DamageReport();
            report.TotalDamage = GetTotalDamage();
            report.ZoneCount = damageZones != null ? damageZones.Length : 0;
            report.FracturedCount = fracturedZones.Count;
            report.ActiveZoneCount = ActiveZoneCount;

            if (performanceLink != null)
            {
                report.AeroDamage = performanceLink.CurrentAeroDamage;
                report.SuspensionDamage = performanceLink.CurrentSuspDamage;
                report.EngineDamage = performanceLink.CurrentEngineDamage;
                report.TireDamage = performanceLink.CurrentTireDamage;
            }

            return report;
        }
    }

    [System.Serializable]
    public struct DamageReport
    {
        public float TotalDamage;
        public int ZoneCount;
        public int FracturedCount;
        public int ActiveZoneCount;
        public float AeroDamage;
        public float SuspensionDamage;
        public float EngineDamage;
        public float TireDamage;

        public override string ToString()
        {
            return $"Damage Report: Total={TotalDamage:F2}, Zones={ZoneCount}, " +
                   $"Fractured={FracturedCount}, Active={ActiveZoneCount}, " +
                   $"Aero={AeroDamage:F2}, Susp={SuspensionDamage:F2}, " +
                   $"Engine={EngineDamage:F2}, Tire={TireDamage:F2}";
        }
    }
}
