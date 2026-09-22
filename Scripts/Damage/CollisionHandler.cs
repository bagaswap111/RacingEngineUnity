using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RacingSim.Damage
{
    /// <summary>
    /// Handles collision detection and distributes impact forces to damage zones.
    /// Implements multi-layer collision strategy for performance.
    /// </summary>
    public class CollisionHandler : MonoBehaviour
    {
        [Header("Collision Settings")]
        public float minDamageVelocity = 0.5f;
        public float impactSpreadRadius = 2f;
        public float rigidBodyForceRatio = 0.7f;

        [Header("Effect Prefabs")]
        public GameObject sparkPrefab;
        public GameObject debrisPrefab;
        public GameObject glassShatterPrefab;

        private Rigidbody vehicleRigidbody;
        private DamageZone[] damageZones;
        private List<GameObject> activeDebris = new List<GameObject>();

        private const int MAX_ACTIVE_DEBRIS = 50;
        private const float DEBRIS_LIFETIME = 10f;

        private void Awake()
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
            if (vehicleRigidbody == null)
                vehicleRigidbody = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            damageZones = GetComponentsInChildren<DamageZone>();
        }

        public void RegisterZones(DamageZone[] zones)
        {
            damageZones = zones;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < minDamageVelocity)
                return;

            ImpactData impact = CalculateImpact(collision);
            DistributeImpact(impact);
        }

        public ImpactData CalculateImpact(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            float3 impactPoint = (float3)contact.point;
            float3 impactNormal = (float3)contact.normal;
            float3 impactVelocity = (float3)collision.relativeVelocity;
            float impactSpeed = math.length(impactVelocity);

            float cosAngle = math.dot(impactVelocity, impactNormal) / math.max(impactSpeed, 0.001f);
            cosAngle = math.abs(cosAngle);
            float effectiveMass = vehicleRigidbody != null ?
                vehicleRigidbody.mass * cosAngle : 1000f * cosAngle;

            float impactEnergy = 0.5f * effectiveMass * impactSpeed * impactSpeed;

            int hitZoneIndex = DetermineDamageZone(impactPoint);

            return new ImpactData
            {
                ImpactPoint = impactPoint,
                ImpactNormal = impactNormal,
                ImpactVelocity = impactVelocity,
                ImpactSpeed = impactSpeed,
                ImpactEnergy = impactEnergy,
                EffectiveMass = effectiveMass,
                HitZoneIndex = hitZoneIndex
            };
        }

        private int DetermineDamageZone(float3 worldPoint)
        {
            if (damageZones == null || damageZones.Length == 0) return -1;

            float bestDist = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < damageZones.Length; i++)
            {
                if (damageZones[i] == null) continue;

                float dist = math.distance(worldPoint, (float3)damageZones[i].GetZoneCenter());
                if (dist < bestDist && dist <= damageZones[i].config.DeformRadius)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public void DistributeImpact(ImpactData impact)
        {
            if (damageZones == null || damageZones.Length == 0) return;

            List<int> affectedZones = GetNearbyZones(impact.ImpactPoint, impactSpreadRadius);

            foreach (int zoneIndex in affectedZones)
            {
                if (zoneIndex < 0 || zoneIndex >= damageZones.Length) continue;
                if (damageZones[zoneIndex] == null) continue;

                float distToZone = math.distance(
                    (float3)damageZones[zoneIndex].GetZoneCenter(),
                    impact.ImpactPoint
                );

                float influence = 1f - (distToZone / impactSpreadRadius);
                influence = math.max(influence, 0f);

                ImpactData zoneImpact = new ImpactData
                {
                    ImpactPoint = impact.ImpactPoint,
                    ImpactNormal = impact.ImpactNormal,
                    ImpactVelocity = impact.ImpactVelocity * influence,
                    ImpactSpeed = impact.ImpactSpeed * influence,
                    ImpactEnergy = impact.ImpactEnergy * influence,
                    EffectiveMass = impact.EffectiveMass * influence,
                    HitZoneIndex = zoneIndex
                };

                damageZones[zoneIndex].ApplyImpact(zoneImpact);
            }

            if (vehicleRigidbody != null)
            {
                float3 impulse = impact.ImpactVelocity * impact.EffectiveMass * rigidBodyForceRatio;
                vehicleRigidbody.AddForceAtPosition(
                    (Vector3)impulse,
                    (Vector3)impact.ImpactPoint,
                    ForceMode.Impulse
                );
            }

            SpawnEffects(impact);
        }

        private List<int> GetNearbyZones(float3 point, float radius)
        {
            List<int> nearby = new List<int>();

            if (damageZones == null) return nearby;

            for (int i = 0; i < damageZones.Length; i++)
            {
                if (damageZones[i] == null) continue;

                float dist = math.distance(point, (float3)damageZones[i].GetZoneCenter());
                if (dist <= radius)
                {
                    nearby.Add(i);
                }
            }

            return nearby;
        }

        private void SpawnEffects(ImpactData impact)
        {
            if (impact.ImpactSpeed > 2f && sparkPrefab != null)
            {
                SpawnSparks(impact.ImpactPoint, impact.ImpactNormal);
            }

            if (impact.ImpactSpeed > 5f && debrisPrefab != null)
            {
                SpawnDebris(impact.ImpactPoint, impact.ImpactNormal);
            }

            if (impact.HitZoneIndex >= 0 && impact.HitZoneIndex < damageZones.Length)
            {
                DamageZone zone = damageZones[impact.HitZoneIndex];
                if (zone is FractureZone fractureZone &&
                    fractureZone.config.Type == DamageZoneType.Fracture &&
                    impact.ImpactSpeed > 3f && glassShatterPrefab != null)
                {
                    SpawnGlassShatter(impact.ImpactPoint);
                }
            }
        }

        private void SpawnSparks(float3 position, float3 normal)
        {
            if (sparkPrefab == null) return;

            GameObject spark = Instantiate(sparkPrefab,
                (Vector3)position,
                Quaternion.LookRotation((Vector3)normal));

            ParticleSystem ps = spark.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startLifetime = 0.3f;
            }

            Destroy(spark, 1f);
        }

        private void SpawnDebris(float3 position, float3 normal)
        {
            if (debrisPrefab == null) return;

            if (activeDebris.Count >= MAX_ACTIVE_DEBRIS)
            {
                GameObject oldest = activeDebris[0];
                activeDebris.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }

            GameObject debris = Instantiate(debrisPrefab,
                (Vector3)position + (Vector3)normal * 0.1f,
                Quaternion.Euler(
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f)
                ));

            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = (Vector3)normal * 2f + new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(1f, 3f),
                    UnityEngine.Random.Range(-1f, 1f)
                );
            }

            activeDebris.Add(debris);
            Destroy(debris, DEBRIS_LIFETIME);
        }

        private void SpawnGlassShatter(float3 position)
        {
            if (glassShatterPrefab == null) return;

            GameObject shatter = Instantiate(glassShatterPrefab,
                (Vector3)position,
                Quaternion.identity);

            Destroy(shatter, 3f);
        }

        public void CleanupExpiredDebris()
        {
            activeDebris.RemoveAll(item => item == null);
        }
    }
}
