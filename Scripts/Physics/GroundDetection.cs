using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Physics
{
    /// <summary>
    /// Ground detection using spherecast or multi-ray for racing simulation.
    /// Provides surface type, grip, and accurate height/normal data.
    /// </summary>
    public class GroundDetection : MonoBehaviour
    {
        [Header("Detection Settings")]
        public float maxDistance = 1.5f;
        public float sphereRadius = 0.05f;
        public LayerMask groundLayer = ~0;
        public int multiRayCount = 3;
        public float multiRaySpread = 0.15f;

        [Header("Surface Types")]
        public float asphaltGrip = 1.0f;
        public float concreteGrip = 0.95f;
        public float grassGrip = 0.6f;
        public float gravelGrip = 0.45f;
        public float dirtGrip = 0.5f;
        public float kerbGrip = 0.8f;
        public float sandGrip = 0.35f;
        public float iceGrip = 0.15f;

        private RaycastHit[] raycastResults;
        private bool useNonAlloc;

        private void Awake()
        {
            raycastResults = new RaycastHit[16];
            useNonAlloc = true;
        }

        public GroundHitData DetectGround(Vector3 origin, Vector3 direction)
        {
            GroundHitData hitData = new GroundHitData();
            hitData.DidHit = false;
            hitData.SurfaceGrip = 1f;

            if (useNonAlloc)
            {
                int count = Physics.SphereCastNonAlloc(
                    origin,
                    sphereRadius,
                    direction,
                    raycastResults,
                    maxDistance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                );

                if (count > 0)
                {
                    float bestDist = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        if (raycastResults[i].distance < bestDist)
                        {
                            bestDist = raycastResults[i].distance;
                            hitData.DidHit = true;
                            hitData.Distance = raycastResults[i].distance;
                            hitData.Point = (float3)raycastResults[i].point;
                            hitData.Normal = (float3)raycastResults[i].normal;
                            hitData.SurfaceType = DetectSurfaceType(raycastResults[i].collider);
                            hitData.SurfaceGrip = GetSurfaceGrip(hitData.SurfaceType);
                        }
                    }
                }
            }
            else
            {
                RaycastHit hit;
                if (Physics.SphereCast(origin, sphereRadius, direction, out hit, maxDistance, groundLayer))
                {
                    hitData.DidHit = true;
                    hitData.Distance = hit.distance;
                    hitData.Point = (float3)hit.point;
                    hitData.Normal = (float3)hit.normal;
                    hitData.SurfaceType = DetectSurfaceType(hit.collider);
                    hitData.SurfaceGrip = GetSurfaceGrip(hitData.SurfaceType);
                }
            }

            return hitData;
        }

        public GroundHitData DetectGroundMultiRay(Vector3 center, Vector3 forward, Vector3 right)
        {
            GroundHitData avgHit = new GroundHitData();
            avgHit.DidHit = false;
            avgHit.Normal = (float3)Vector3.up;
            avgHit.SurfaceGrip = 1f;

            float totalDist = 0f;
            int hitCount = 0;
            float3 avgNormal = float3.zero;
            float avgGrip = 0f;

            Vector3[] offsets = new Vector3[multiRayCount];
            offsets[0] = Vector3.zero;

            if (multiRayCount >= 3)
            {
                offsets[1] = right * multiRaySpread;
                offsets[2] = -right * multiRaySpread;
            }
            if (multiRayCount >= 5)
            {
                offsets[3] = forward * multiRaySpread;
                offsets[4] = -forward * multiRaySpread;
            }
            if (multiRayCount >= 7)
            {
                offsets[5] = (right + forward) * multiRaySpread * 0.7f;
                offsets[6] = (-right - forward) * multiRaySpread * 0.7f;
            }

            for (int i = 0; i < multiRayCount; i++)
            {
                Vector3 rayOrigin = center + offsets[i] + Vector3.up * 0.1f;
                GroundHitData hit = DetectGround(rayOrigin, Vector3.down);

                if (hit.DidHit)
                {
                    totalDist += hit.Distance;
                    avgNormal += hit.Normal;
                    avgGrip += hit.SurfaceGrip;
                    hitCount++;

                    if (!avgHit.DidHit || hit.Distance < avgHit.Distance)
                    {
                        avgHit.DidHit = true;
                        avgHit.Distance = hit.Distance;
                        avgHit.Point = hit.Point;
                        avgHit.SurfaceType = hit.SurfaceType;
                    }
                }
            }

            if (hitCount > 0)
            {
                avgHit.Normal = math.normalize(avgNormal / hitCount);
                avgHit.SurfaceGrip = avgGrip / hitCount;
                avgHit.Distance = totalDist / hitCount;
            }

            return avgHit;
        }

        private int DetectSurfaceType(Collider col)
        {
            if (col == null) return 0;

            string tag = col.tag;
            switch (tag)
            {
                case "Asphalt": return 0;
                case "Concrete": return 1;
                case "Grass": return 2;
                case "Gravel": return 3;
                case "Dirt": return 4;
                case "Kerb": return 5;
                case "Sand": return 6;
                case "Ice": return 7;
                default: return 0;
            }
        }

        private float GetSurfaceGrip(int surfaceType)
        {
            switch (surfaceType)
            {
                case 0: return asphaltGrip;
                case 1: return concreteGrip;
                case 2: return grassGrip;
                case 3: return gravelGrip;
                case 4: return dirtGrip;
                case 5: return kerbGrip;
                case 6: return sandGrip;
                case 7: return iceGrip;
                default: return asphaltGrip;
            }
        }
    }
}
