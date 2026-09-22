using UnityEngine;
using System.Collections.Generic;

namespace RacingSim.Damage
{
    /// <summary>
    /// Level 3: Fracture & Detachment system.
    /// Used for glass, lights, bumpers, spoilers, side mirrors.
    /// Parts can break off, hang, or fully detach as debris.
    /// </summary>
    public class FractureZone : DamageZone
    {
        [Header("Fracture Settings")]
        public GameObject intactMeshObject;
        public GameObject[] fragmentObjects;
        public Transform[] attachmentTransforms;
        public float[] attachmentBreakForces;
        public float fractureThreshold = 2000f;

        [Header("Debris Settings")]
        public float debrisLifetime = 15f;
        public float fragmentSpeedFactor = 0.5f;
        public float fragmentSpinFactor = 5f;

        private AttachmentPoint[] attachments;
        private FragmentData[] fragments;
        private bool isFractured;
        private bool isHanging;
        private int hangingAttachmentIndex;
        private float hangingSwingFactor;
        private List<GameObject> activeFragments = new List<GameObject>();

        public bool IsFractured => isFractured;
        public bool IsHanging => isHanging;
        public float AeroContribution { get; set; }

        protected override void Awake()
        {
            base.Awake();
            config.Type = DamageZoneType.Fracture;
            AeroContribution = 0.1f;
        }

        public override void Initialize(DamageZoneConfig cfg)
        {
            base.Initialize(cfg);
            InitializeFractureSystem();
        }

        private void InitializeFractureSystem()
        {
            if (attachmentBreakForces == null)
                attachmentBreakForces = new float[0];

            if (attachmentTransforms == null || attachmentTransforms.Length == 0)
            {
                attachmentTransforms = new Transform[0];
                attachmentBreakForces = new float[0];
            }

            attachments = new AttachmentPoint[attachmentTransforms.Length];
            for (int i = 0; i < attachmentTransforms.Length; i++)
            {
                attachments[i] = new AttachmentPoint
                {
                    Position = attachmentTransforms[i] != null ?
                        (Unity.Mathematics.float3)attachmentTransforms[i].localPosition : Unity.Mathematics.float3.zero,
                    BreakForce = i < attachmentBreakForces.Length ?
                        attachmentBreakForces[i] : config.BreakThreshold,
                    IsBroken = false,
                    BreakTime = 0f
                };
            }

            if (fragmentObjects != null)
            {
                fragments = new FragmentData[fragmentObjects.Length];
                for (int i = 0; i < fragmentObjects.Length; i++)
                {
                    if (fragmentObjects[i] == null) continue;

                    Rigidbody rb = fragmentObjects[i].GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = fragmentObjects[i].AddComponent<Rigidbody>();

                    rb.isKinematic = true;
                    rb.mass = config.FragmentMass / fragmentObjects.Length;

                    fragments[i] = new FragmentData
                    {
                        MeshObject = fragmentObjects[i],
                        RigidBody = rb,
                        Mass = rb.mass,
                        Center = (Unity.Mathematics.float3)fragmentObjects[i].transform.position
                    };

                    fragmentObjects[i].SetActive(false);
                }
            }

            isFractured = false;
            isHanging = false;
        }

        public override void ApplyImpact(ImpactData impact)
        {
            if (isFractured) return;

            for (int i = 0; i < attachments.Length; i++)
            {
                if (attachments[i].IsBroken) continue;

                float dist = Unity.Mathematics.math.distance(
                    (Unity.Mathematics.float3)transform.TransformPoint((Vector3)attachments[i].Position),
                    impact.ImpactPoint
                );

                float influence = 1f / Mathf.Max(dist, 0.01f);
                float totalInfluence = 0f;
                for (int j = 0; j < attachments.Length; j++)
                {
                    if (!attachments[j].IsBroken)
                        totalInfluence += 1f / Mathf.Max(
                            Unity.Mathematics.math.distance(
                                (Unity.Mathematics.float3)transform.TransformPoint((Vector3)attachments[j].Position),
                                impact.ImpactPoint
                            ), 0.01f);
                }

                float localForce = impact.ImpactEnergy * influence / Mathf.Max(totalInfluence, 0.001f);

                if (localForce > attachments[i].BreakForce)
                {
                    attachments[i].IsBroken = true;
                    attachments[i].BreakTime = Time.time;
                }
            }

            int brokenCount = 0;
            for (int i = 0; i < attachments.Length; i++)
            {
                if (attachments[i].IsBroken) brokenCount++;
            }

            int totalCount = attachments.Length;
            if (totalCount == 0)
            {
                FracturePart(impact.ImpactPoint, impact.ImpactVelocity);
                return;
            }

            if (brokenCount == totalCount)
            {
                DetachPart(impact.ImpactPoint, impact.ImpactVelocity);
            }
            else if (brokenCount >= Mathf.CeilToInt(totalCount * 0.5f))
            {
                isHanging = true;
                hangingSwingFactor = 0.5f;
                for (int i = 0; i < attachments.Length; i++)
                {
                    if (!attachments[i].IsBroken)
                    {
                        hangingAttachmentIndex = i;
                        break;
                    }
                }
            }

            if (impact.ImpactEnergy > fractureThreshold)
            {
                FracturePart(impact.ImpactPoint, impact.ImpactVelocity);
            }

            TotalDamage = (float)brokenCount / Mathf.Max(totalCount, 1);
            IsDirty = true;
        }

        private void FracturePart(Vector3 impactPoint, Vector3 impactVelocity)
        {
            if (isFractured) return;
            isFractured = true;

            if (intactMeshObject != null)
                intactMeshObject.SetActive(false);

            if (fragments == null) return;

            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i].MeshObject == null) continue;

                fragments[i].MeshObject.SetActive(true);
                fragments[i].RigidBody.isKinematic = false;

                float distToImpact = Vector3.Distance(fragments[i].Center, impactPoint);
                float falloff = 1f / Mathf.Max(distToImpact, 0.1f);

                Vector3 fragVelocity = impactVelocity * falloff * fragmentSpeedFactor;
                fragments[i].RigidBody.velocity = fragVelocity;

                fragments[i].RigidBody.angularVelocity = new Vector3(
                    Random.Range(-fragmentSpinFactor, fragmentSpinFactor),
                    Random.Range(-fragmentSpinFactor, fragmentSpinFactor),
                    Random.Range(-fragmentSpinFactor, fragmentSpinFactor)
                );

                activeFragments.Add(fragments[i].MeshObject);

                Destroy(fragments[i].MeshObject, debrisLifetime);
            }

            TotalDamage = 1f;
        }

        private void DetachPart(Vector3 impactPoint, Vector3 impactVelocity)
        {
            FracturePart(impactPoint, impactVelocity);
        }

        public override void UpdateZone(float dt)
        {
            if (!isHanging || isFractured) return;

            if (hangingAttachmentIndex < attachments.Length && !attachments[hangingAttachmentIndex].IsBroken)
            {
                transform.Rotate(
                    Random.Range(-hangingSwingFactor, hangingSwingFactor) * dt * 10f,
                    0f,
                    Random.Range(-hangingSwingFactor, hangingSwingFactor) * dt * 10f
                );
            }
        }

        public override void ResetToOriginal()
        {
            isFractured = false;
            isHanging = false;

            for (int i = 0; i < attachments.Length; i++)
            {
                attachments[i].IsBroken = false;
                attachments[i].BreakTime = 0f;
            }

            if (intactMeshObject != null)
                intactMeshObject.SetActive(true);

            if (fragmentObjects != null)
            {
                for (int i = 0; i < fragmentObjects.Length; i++)
                {
                    if (fragmentObjects[i] != null)
                    {
                        fragmentObjects[i].SetActive(false);
                        Rigidbody rb = fragmentObjects[i].GetComponent<Rigidbody>();
                        if (rb != null) rb.isKinematic = true;
                    }
                }
            }

            foreach (var frag in activeFragments)
            {
                if (frag != null) Destroy(frag);
            }
            activeFragments.Clear();

            transform.localRotation = Quaternion.identity;

            TotalDamage = 0f;
            IsDirty = false;
        }

        private void OnDestroy()
        {
            foreach (var frag in activeFragments)
            {
                if (frag != null) Destroy(frag);
            }
        }
    }
}
