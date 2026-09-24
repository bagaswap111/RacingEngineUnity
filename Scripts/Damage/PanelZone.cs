using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Level 1: Panel Deformation via vertex displacement.
    /// Used for bumpers, fenders, hood, doors, trunk.
    /// Uses simplified control points + smoothstep falloff.
    /// </summary>
    public class PanelZone : DamageZone
    {
        [Header("Panel Settings")]
        public MeshFilter targetMeshFilter;

        private NativeArray<PanelControlPoint> controlPoints;
        private NativeArray<VertexNodeBinding> vertexBindings;
        private NativeArray<MeshVertex> meshVertices;
        private int controlPointCount;
        private int vertexCount;
        private Mesh deformedMesh;
        private bool meshInitialized;

        private const float GLOBAL_DAMPING = 0.95f;
        private const float PLASTIC_FACTOR = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            config.Type = DamageZoneType.PanelDeform;
        }

        public void PartialRepair(float speed)
        {
            if (!meshVertices.IsCreated) return;
            for (int i = 0; i < meshVertices.Length; i++)
            {
                var v = meshVertices[i];
                v.Position = math.lerp(v.Position, v.OriginalPosition, speed);
                meshVertices[i] = v;
            }
            if (controlPoints.IsCreated)
            {
                for (int i = 0; i < controlPoints.Length; i++)
                {
                    var cp = controlPoints[i];
                    cp.Position = math.lerp(cp.Position, cp.OriginalPosition, speed);
                    controlPoints[i] = cp;
                }
            }
        }

        public NativeArray<MeshVertex> MeshVertices => meshVertices;

        public override void Initialize(DamageZoneConfig cfg)
        {
            base.Initialize(cfg);
            InitializeControlPoints();
        }

        private void InitializeControlPoints()
        {
            if (targetMeshFilter == null)
                targetMeshFilter = GetComponent<MeshFilter>();

            if (targetMeshFilter == null || targetMeshFilter.mesh == null)
                return;

            deformedMesh = Object.Instantiate(targetMeshFilter.mesh);
            targetMeshFilter.mesh = deformedMesh;

            controlPointCount = Mathf.Max(4, config.NodeCount);
            vertexCount = deformedMesh.vertexCount;

            controlPoints = new NativeArray<PanelControlPoint>(controlPointCount, Allocator.Persistent);
            meshVertices = new NativeArray<MeshVertex>(vertexCount, Allocator.Persistent);

            Vector3[] verts = deformedMesh.vertices;
            Vector3[] normals = deformedMesh.normals;

            float3 center = float3.zero;
            for (int i = 0; i < vertexCount; i++)
            {
                center += (float3)verts[i];
            }
            center /= vertexCount;

            float3 boundsSize = deformedMesh.bounds.size;
            float maxDim = math.max(boundsSize.x, math.max(boundsSize.y, boundsSize.z));
            float spacing = maxDim / Mathf.Sqrt(controlPointCount);

            int cpPerAxis = Mathf.CeilToInt(Mathf.Pow(controlPointCount, 1f / 3f));
            int cpIndex = 0;

            for (int x = 0; x < cpPerAxis && cpIndex < controlPointCount; x++)
            {
                for (int y = 0; y < cpPerAxis && cpIndex < controlPointCount; y++)
                {
                    for (int z = 0; z < cpPerAxis && cpIndex < controlPointCount; z++)
                    {
                        float3 pos = center + new float3(
                            (x - cpPerAxis * 0.5f) * spacing,
                            (y - cpPerAxis * 0.5f) * spacing,
                            (z - cpPerAxis * 0.5f) * spacing
                        );

                        controlPoints[cpIndex] = new PanelControlPoint
                        {
                            Position = pos,
                            OriginalPosition = pos,
                            Velocity = float3.zero,
                            Mass = 0.5f,
                            Stiffness = config.Stiffness,
                            Damping = config.DampingRatio,
                            Damage = 0f,
                            IsDetached = false,
                            AttachedToZone = -1
                        };
                        cpIndex++;
                    }
                }
            }

            vertexBindings = new NativeArray<VertexNodeBinding>(vertexCount, Allocator.Persistent);
            for (int v = 0; v < vertexCount; v++)
            {
                float3 vPos = (float3)verts[v];
                VertexNodeBinding binding = new VertexNodeBinding
                {
                    VertexIndex = v,
                    NodeIndex0 = 0,
                    NodeIndex1 = 0,
                    NodeIndex2 = 0,
                    Weight0 = 1f,
                    Weight1 = 0f,
                    Weight2 = 0f,
                    Count = 1
                };

                float best0 = float.MaxValue, best1 = float.MaxValue, best2 = float.MaxValue;
                int idx0 = 0, idx1 = 0, idx2 = 0;

                for (int cp = 0; cp < controlPointCount; cp++)
                {
                    float dist = math.distance(vPos, controlPoints[cp].OriginalPosition);
                    if (dist < best0)
                    {
                        best2 = best1; idx2 = idx1;
                        best1 = best0; idx1 = idx0;
                        best0 = dist; idx0 = cp;
                    }
                    else if (dist < best1)
                    {
                        best2 = best1; idx2 = idx1;
                        best1 = dist; idx1 = cp;
                    }
                    else if (dist < best2)
                    {
                        best2 = dist; idx2 = cp;
                    }
                }

                float inv0 = 1f / math.max(best0, 0.001f);
                float inv1 = 1f / math.max(best1, 0.001f);
                float inv2 = 1f / math.max(best2, 0.001f);
                float totalInv = inv0 + inv1 + inv2;

                binding.NodeIndex0 = idx0;
                binding.NodeIndex1 = idx1;
                binding.NodeIndex2 = idx2;
                binding.Weight0 = inv0 / totalInv;
                binding.Weight1 = inv1 / totalInv;
                binding.Weight2 = inv2 / totalInv;
                binding.Count = 3;

                vertexBindings[v] = binding;
            }

            for (int v = 0; v < vertexCount; v++)
            {
                meshVertices[v] = new MeshVertex
                {
                    Position = (float3)verts[v],
                    OriginalPosition = (float3)verts[v],
                    DeformationOffset = float3.zero,
                    Normal = v < normals.Length ? (float3)normals[v] : new float3(0, 1, 0)
                };
            }

            meshInitialized = true;
        }

        public override void ApplyImpact(ImpactData impact)
        {
            if (!meshInitialized) return;

            float3 localImpact = transform.InverseTransformPoint((Vector3)impact.ImpactPoint);
            float3 localNormal = transform.InverseTransformDirection((Vector3)impact.ImpactNormal);

            float deformRadius = config.DeformRadius * math.pow(impact.ImpactEnergy / 1000f, 0.33f);
            deformRadius = math.clamp(deformRadius, 0.05f, config.DeformRadius * 2f);

            float dentFactor = config.MaxDeformDepth / math.max(config.BreakThreshold, 1f);

            for (int cp = 0; cp < controlPointCount; cp++)
            {
                float dist = math.distance(controlPoints[cp].Position, localImpact);
                if (dist >= deformRadius) continue;

                float t = dist / deformRadius;
                float falloff = 1f - t * t * (3f - 2f * t);

                float3 displacement = localNormal * impact.ImpactEnergy * falloff * dentFactor;

                if (impact.ImpactEnergy > config.BreakThreshold * 0.3f)
                {
                    float wrinkleFreq = 20f;
                    float wrinkleAmp = config.WrinkleIntensity * 0.01f;
                    float wrinkle = math.sin(dist * wrinkleFreq) * wrinkleAmp * falloff;
                    displacement += (float3)transform.up * wrinkle;
                }

                PanelControlPoint cpData = controlPoints[cp];
                cpData.Position += displacement;
                cpData.Damage += falloff * (impact.ImpactEnergy / config.BreakThreshold);

                if (cpData.Damage > config.ElasticLimit)
                {
                    float plasticRatio = (cpData.Damage - config.ElasticLimit) / (1f - config.ElasticLimit);
                    cpData.Position = math.lerp(
                        cpData.Position,
                        cpData.OriginalPosition + displacement * plasticRatio,
                        plasticRatio
                    );
                }

                controlPoints[cp] = cpData;
            }

            TotalDamage = CalculateAverageDamage();
            IsDirty = true;
        }

        public override void UpdateZone(float dt)
        {
            if (!meshInitialized || !IsDirty) return;

            for (int cp = 0; cp < controlPointCount; cp++)
            {
                PanelControlPoint cpData = controlPoints[cp];
                if (cpData.IsDetached || cpData.Damage <= 0f) continue;

                float3 toOriginal = cpData.OriginalPosition - cpData.Position;
                float3 springForce = toOriginal * cpData.Stiffness;
                float3 dampForce = -cpData.Velocity * cpData.Damping;

                cpData.Velocity += (springForce + dampForce) * dt;
                cpData.Velocity *= GLOBAL_DAMPING;
                cpData.Position += cpData.Velocity * dt;

                float disp = math.distance(cpData.Position, cpData.OriginalPosition);
                if (disp > config.MaxDeformDepth)
                {
                    cpData.Position = cpData.OriginalPosition +
                        math.normalize(cpData.Position - cpData.OriginalPosition) * config.MaxDeformDepth;
                    cpData.Velocity *= 0.1f;
                }

                controlPoints[cp] = cpData;
            }

            UpdateMeshFromControlPoints();
        }

        private void UpdateMeshFromControlPoints()
        {
            Vector3[] verts = new Vector3[vertexCount];

            for (int v = 0; v < vertexCount; v++)
            {
                VertexNodeBinding binding = vertexBindings[v];
                float3 newPos = float3.zero;

                newPos += controlPoints[binding.NodeIndex0].Position * binding.Weight0;
                if (binding.Count > 1)
                    newPos += controlPoints[binding.NodeIndex1].Position * binding.Weight1;
                if (binding.Count > 2)
                    newPos += controlPoints[binding.NodeIndex2].Position * binding.Weight2;

                newPos += meshVertices[v].DeformationOffset;
                verts[v] = (Vector3)newPos;
            }

            deformedMesh.vertices = verts;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateTangents();
            deformedMesh.RecalculateBounds();
        }

        public void ApplyDent(float3 localImpactPoint, float3 impactDir, float dentDepth, float dentRadius)
        {
            if (!meshInitialized) return;

            Vector3[] verts = deformedMesh.vertices;

            for (int v = 0; v < vertexCount; v++)
            {
                float dist = math.distance((float3)verts[v], localImpactPoint);
                if (dist >= dentRadius) continue;

                float t = dist / dentRadius;
                float falloff = math.cos(t * math.PI * 0.5f);
                falloff = falloff * falloff;

                float noise = Mathf.PerlinNoise(verts[v].x * 5f, verts[v].z * 5f) * 0.1f;
                float3 dent = impactDir * dentDepth * falloff + impactDir * dentDepth * noise * falloff;

                meshVertices[v] = new MeshVertex
                {
                    Position = meshVertices[v].Position,
                    OriginalPosition = meshVertices[v].OriginalPosition,
                    DeformationOffset = meshVertices[v].DeformationOffset + dent,
                    Normal = meshVertices[v].Normal
                };
                verts[v] = (Vector3)(meshVertices[v].Position + meshVertices[v].DeformationOffset);
            }

            deformedMesh.vertices = verts;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateTangents();
            deformedMesh.RecalculateBounds();
        }

        private float CalculateAverageDamage()
        {
            float total = 0f;
            for (int i = 0; i < controlPointCount; i++)
            {
                total += controlPoints[i].Damage;
            }
            return total / controlPointCount;
        }

        public override void ResetToOriginal()
        {
            if (!meshInitialized) return;

            for (int cp = 0; cp < controlPointCount; cp++)
            {
                controlPoints[cp] = new PanelControlPoint
                {
                    Position = controlPoints[cp].OriginalPosition,
                    OriginalPosition = controlPoints[cp].OriginalPosition,
                    Velocity = float3.zero,
                    Mass = controlPoints[cp].Mass,
                    Stiffness = controlPoints[cp].Stiffness,
                    Damping = controlPoints[cp].Damping,
                    Damage = 0f,
                    IsDetached = false,
                    AttachedToZone = -1
                };
            }

            for (int v = 0; v < vertexCount; v++)
            {
                meshVertices[v] = new MeshVertex
                {
                    Position = meshVertices[v].OriginalPosition,
                    OriginalPosition = meshVertices[v].OriginalPosition,
                    DeformationOffset = float3.zero,
                    Normal = meshVertices[v].Normal
                };
            }

            Vector3[] verts = new Vector3[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                verts[v] = (Vector3)meshVertices[v].OriginalPosition;
            }
            deformedMesh.vertices = verts;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateTangents();
            deformedMesh.RecalculateBounds();

            TotalDamage = 0f;
            IsDirty = false;
        }

        private void OnDestroy()
        {
            if (controlPoints.IsCreated) controlPoints.Dispose();
            if (vertexBindings.IsCreated) vertexBindings.Dispose();
            if (meshVertices.IsCreated) meshVertices.Dispose();
            if (deformedMesh != null) Destroy(deformedMesh);
        }
    }
}
