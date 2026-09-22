using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Level 2: Simplified soft-body node-beam system.
    /// Used for roll cage, chassis rails, suspension mounts.
    /// Spring-damper physics with semi-implicit Euler integration.
    /// </summary>
    public class NodeBeamZone : DamageZone
    {
        [Header("Node-Beam Settings")]
        public MeshFilter targetMeshFilter;

        private NativeArray<DeformNode> nodes;
        private NativeArray<DeformBeam> beams;
        private NativeArray<VertexNodeBinding> vertexBindings;
        private NativeArray<MeshVertex> meshVertices;
        private int activeBeamCount;
        private Mesh deformedMesh;
        private bool meshInitialized;
        private float settleTimer;

        private const float GLOBAL_DAMPING = 0.98f;
        private const float GRAVITY = -9.81f;
        private const float SETTLE_TIME = 3f;
        private const float ATTACHMENT_SPRING = 50000f;
        private const float ATTACHMENT_DAMPING = 5000f;

        protected override void Awake()
        {
            base.Awake();
            config.Type = DamageZoneType.NodeBeam;
        }

        public override void Initialize(DamageZoneConfig cfg)
        {
            base.Initialize(cfg);
            InitializeNodeBeamSystem();
        }

        private void InitializeNodeBeamSystem()
        {
            if (targetMeshFilter == null)
                targetMeshFilter = GetComponent<MeshFilter>();

            if (targetMeshFilter == null || targetMeshFilter.mesh == null)
                return;

            deformedMesh = Object.Instantiate(targetMeshFilter.mesh);
            targetMeshFilter.mesh = deformedMesh;

            int nodeCount = Mathf.Max(8, config.NodeCount);
            int beamCount = Mathf.Max(12, config.BeamCount);

            nodes = new NativeArray<DeformNode>(nodeCount, Allocator.Persistent);
            beams = new NativeArray<DeformBeam>(beamCount, Allocator.Persistent);

            Vector3[] verts = deformedMesh.vertices;
            Vector3[] normals = deformedMesh.normals;
            int vertCount = verts.Length;

            float3 boundsCenter = (float3)deformedMesh.bounds.center;
            float3 boundsSize = (float3)deformedMesh.bounds.size;

            int nodesPerAxis = Mathf.CeilToInt(Mathf.Pow(nodeCount, 1f / 3f));
            int nodeIndex = 0;
            float spacingX = boundsSize.x / nodesPerAxis;
            float spacingY = boundsSize.y / nodesPerAxis;
            float spacingZ = boundsSize.z / nodesPerAxis;

            for (int x = 0; x < nodesPerAxis && nodeIndex < nodeCount; x++)
            {
                for (int y = 0; y < nodesPerAxis && nodeIndex < nodeCount; y++)
                {
                    for (int z = 0; z < nodesPerAxis && nodeIndex < nodeCount; z++)
                    {
                        float3 pos = boundsCenter + new float3(
                            (x - nodesPerAxis * 0.5f) * spacingX,
                            (y - nodesPerAxis * 0.5f) * spacingY,
                            (z - nodesPerAxis * 0.5f) * spacingZ
                        );

                        nodes[nodeIndex] = new DeformNode
                        {
                            Position = pos,
                            OriginalPosition = pos,
                            Velocity = float3.zero,
                            Force = float3.zero,
                            Mass = config.FragmentMass / nodeCount,
                            AccumulatedDamage = 0f,
                            IsBroken = false,
                            PlasticOffset = 0f,
                            ZoneIndex = 0
                        };
                        nodeIndex++;
                    }
                }
            }

            activeBeamCount = 0;
            for (int a = 0; a < nodeCount && activeBeamCount < beamCount; a++)
            {
                for (int b = a + 1; b < nodeCount && activeBeamCount < beamCount; b++)
                {
                    float dist = math.distance(nodes[a].OriginalPosition, nodes[b].OriginalPosition);
                    float maxConnDist = math.max(spacingX, math.max(spacingY, spacingZ)) * 1.5f;

                    if (dist <= maxConnDist)
                    {
                        beams[activeBeamCount] = new DeformBeam
                        {
                            NodeA = a,
                            NodeB = b,
                            RestLength = dist,
                            CurrentLength = dist,
                            Stiffness = config.Stiffness,
                            Damping = config.DampingRatio * 100f,
                            BreakForce = config.BeamBreakForce,
                            CurrentStress = 0f,
                            IsBroken = false,
                            Type = dist < maxConnDist * 0.5f ? BeamType.Structural : BeamType.Support
                        };
                        activeBeamCount++;
                    }
                }
            }

            meshVertices = new NativeArray<MeshVertex>(vertCount, Allocator.Persistent);
            vertexBindings = new NativeArray<VertexNodeBinding>(vertCount, Allocator.Persistent);

            for (int v = 0; v < vertCount; v++)
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

                for (int n = 0; n < nodeCount; n++)
                {
                    float dist = math.distance(vPos, nodes[n].OriginalPosition);
                    if (dist < best0)
                    {
                        best2 = best1; idx2 = idx1;
                        best1 = best0; idx1 = idx0;
                        best0 = dist; idx0 = n;
                    }
                    else if (dist < best1)
                    {
                        best2 = best1; idx2 = idx1;
                        best1 = dist; idx1 = n;
                    }
                    else if (dist < best2)
                    {
                        best2 = dist; idx2 = n;
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

                meshVertices[v] = new MeshVertex
                {
                    Position = (float3)verts[v],
                    OriginalPosition = (float3)verts[v],
                    DeformationOffset = float3.zero,
                    Normal = v < normals.Length ? (float3)normals[v] : new float3(0, 1, 0)
                };
            }

            meshInitialized = true;
            IsActive = false;
            settleTimer = 0f;
        }

        public override void ApplyImpact(ImpactData impact)
        {
            if (!meshInitialized) return;

            float3 localImpact = transform.InverseTransformPoint((Vector3)impact.ImpactPoint);
            float3 localImpulse = transform.InverseTransformDirection((Vector3)impact.ImpactVelocity);

            for (int n = 0; n < nodes.Length; n++)
            {
                float dist = math.distance(nodes[n].Position, localImpact);
                float impactRadius = config.DeformRadius;

                if (dist < impactRadius)
                {
                    float falloff = 1f - (dist / impactRadius);
                    falloff = falloff * falloff;

                    DeformNode node = nodes[n];
                    node.Force += localImpulse * impact.ImpactEnergy * falloff;
                    node.Force += new float3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                    ) * math.length(localImpulse) * 0.05f * falloff;
                    nodes[n] = node;
                }
            }

            TotalDamage = math.clamp(TotalDamage + impact.ImpactEnergy / config.BreakThreshold * 0.1f, 0f, 1f);
            IsActive = true;
            IsDirty = true;
            settleTimer = SETTLE_TIME;
        }

        public override void UpdateZone(float dt)
        {
            if (!meshInitialized) return;

            if (!IsActive) return;

            for (int n = 0; n < nodes.Length; n++)
            {
                DeformNode node = nodes[n];
                if (node.IsBroken) continue;

                node.Force = float3.zero;
                node.Force.y += GRAVITY * node.Mass;
                nodes[n] = node;
            }

            for (int b = 0; b < activeBeamCount; b++)
            {
                DeformBeam beam = beams[b];
                if (beam.IsBroken) continue;

                float3 posA = nodes[beam.NodeA].Position;
                float3 posB = nodes[beam.NodeB].Position;
                float3 dir = posB - posA;
                float currentLen = math.length(dir);

                if (currentLen < 0.0001f) continue;

                float3 normDir = dir / currentLen;
                float strain = (currentLen - beam.RestLength) / beam.RestLength;

                float F_spring = beam.Stiffness * (currentLen - beam.RestLength);

                float3 velA = nodes[beam.NodeA].Velocity;
                float3 velB = nodes[beam.NodeB].Velocity;
                float v_rel = math.dot(velA - velB, normDir);
                float F_damp = beam.Damping * v_rel;

                float F_total = F_spring + F_damp;

                float3 forceVec = F_total * normDir;

                DeformNode nodeA = nodes[beam.NodeA];
                DeformNode nodeB = nodes[beam.NodeB];
                nodeA.Force -= forceVec;
                nodeB.Force += forceVec;
                nodes[beam.NodeA] = nodeA;
                nodes[beam.NodeB] = nodeB;

                beam.CurrentStress = math.abs(F_spring) / beam.BreakForce;
                beam.CurrentLength = currentLen;

                if (beam.CurrentStress > 1f)
                {
                    beam.IsBroken = true;
                    TotalDamage = math.clamp(TotalDamage + 0.05f, 0f, 1f);
                }

                beams[b] = beam;
            }

            for (int n = 0; n < nodes.Length; n++)
            {
                DeformNode node = nodes[n];
                if (node.IsBroken) continue;

                node.Velocity += (node.Force / node.Mass) * dt;
                node.Velocity *= GLOBAL_DAMPING;
                node.Position += node.Velocity * dt;

                float disp = math.distance(node.Position, node.OriginalPosition);
                if (disp > config.ElasticLimit)
                {
                    float plasticRatio = (disp - config.ElasticLimit) / config.MaxDeformDepth;
                    node.AccumulatedDamage += plasticRatio * dt * 2f;
                    node.AccumulatedDamage = math.clamp(node.AccumulatedDamage, 0f, 1f);
                }

                if (disp > config.MaxDeformDepth)
                {
                    node.Position = node.OriginalPosition +
                        math.normalize(node.Position - node.OriginalPosition) * config.MaxDeformDepth;
                    node.Velocity *= 0.1f;
                }

                nodes[n] = node;
            }

            ApplyAttachmentConstraints(dt);

            settleTimer -= dt;
            if (settleTimer <= 0f && TotalDamage < 0.01f)
            {
                IsActive = false;
            }

            UpdateMeshFromNodes();
        }

        private void ApplyAttachmentConstraints(float dt)
        {
            for (int n = 0; n < nodes.Length; n++)
            {
                DeformNode node = nodes[n];
                if (node.IsBroken) continue;

                float3 toOriginal = node.OriginalPosition - node.Position;
                float3 attachForce = toOriginal * ATTACHMENT_SPRING;
                float3 attachDamp = -node.Velocity * ATTACHMENT_DAMPING;

                node.Force = attachForce + attachDamp;
                nodes[n] = node;
            }
        }

        private void UpdateMeshFromNodes()
        {
            Vector3[] verts = new Vector3[meshVertices.Length];

            for (int v = 0; v < meshVertices.Length; v++)
            {
                VertexNodeBinding binding = vertexBindings[v];
                float3 newPos = float3.zero;

                newPos += nodes[binding.NodeIndex0].Position * binding.Weight0;
                if (binding.Count > 1)
                    newPos += nodes[binding.NodeIndex1].Position * binding.Weight1;
                if (binding.Count > 2)
                    newPos += nodes[binding.NodeIndex2].Position * binding.Weight2;

                newPos += meshVertices[v].DeformationOffset;
                verts[v] = (Vector3)newPos;
            }

            deformedMesh.vertices = verts;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateTangents();
            deformedMesh.RecalculateBounds();
        }

        public override void ResetToOriginal()
        {
            if (!meshInitialized) return;

            for (int n = 0; n < nodes.Length; n++)
            {
                DeformNode node = nodes[n];
                node.Position = node.OriginalPosition;
                node.Velocity = float3.zero;
                node.Force = float3.zero;
                node.AccumulatedDamage = 0f;
                node.PlasticOffset = 0f;
                node.IsBroken = false;
                nodes[n] = node;
            }

            for (int b = 0; b < activeBeamCount; b++)
            {
                DeformBeam beam = beams[b];
                beam.IsBroken = false;
                beam.CurrentStress = 0f;
                beam.CurrentLength = beam.RestLength;
                beams[b] = beam;
            }

            for (int v = 0; v < meshVertices.Length; v++)
            {
                meshVertices[v] = new MeshVertex
                {
                    Position = meshVertices[v].OriginalPosition,
                    OriginalPosition = meshVertices[v].OriginalPosition,
                    DeformationOffset = float3.zero,
                    Normal = meshVertices[v].Normal
                };
            }

            Vector3[] verts = new Vector3[meshVertices.Length];
            for (int v = 0; v < meshVertices.Length; v++)
            {
                verts[v] = (Vector3)meshVertices[v].OriginalPosition;
            }
            deformedMesh.vertices = verts;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateTangents();
            deformedMesh.RecalculateBounds();

            TotalDamage = 0f;
            IsActive = false;
            IsDirty = false;
        }

        public int GetBrokenBeamCount()
        {
            int count = 0;
            for (int b = 0; b < activeBeamCount; b++)
            {
                if (beams[b].IsBroken) count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (beams.IsCreated) beams.Dispose();
            if (vertexBindings.IsCreated) vertexBindings.Dispose();
            if (meshVertices.IsCreated) meshVertices.Dispose();
            if (deformedMesh != null) Destroy(deformedMesh);
        }
    }
}
