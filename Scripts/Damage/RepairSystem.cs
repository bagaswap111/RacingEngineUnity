using UnityEngine;
using Unity.Mathematics;

namespace RacingSim.Damage
{
    /// <summary>
    /// Handles vehicle repair with 3 levels:
    /// Level 1: Quick Fix - light damage only
    /// Level 2: Full Repair - all deformation
    /// Level 3: Component Replacement - everything new
    /// </summary>
    public class RepairSystem : MonoBehaviour
    {
        [Header("Repair Settings")]
        public float quickFixSpeed = 2f;
        public float quickFixThreshold = 0.3f;
        public float fullRepairDuration = 20f;
        public float componentReplaceDuration = 60f;

        private DamageZone[] damageZones;
        private float repairTimer;
        private bool isRepairing;
        private int currentRepairLevel;

        public bool IsRepairing => isRepairing;
        public float RepairProgress { get; private set; }

        private void Awake()
        {
            damageZones = GetComponentsInChildren<DamageZone>();
        }

        public void RegisterZones(DamageZone[] zones)
        {
            damageZones = zones;
        }

        /// <summary>
        /// Start a repair operation.
        /// level 1 = Quick Fix, level 2 = Full Repair, level 3 = Component Replacement
        /// </summary>
        public void StartRepair(int level)
        {
            if (isRepairing) return;

            currentRepairLevel = Mathf.Clamp(level, 1, 3);
            isRepairing = true;
            repairTimer = 0f;
            RepairProgress = 0f;

            switch (currentRepairLevel)
            {
                case 1:
                    repairTimer = quickFixSpeed;
                    break;
                case 2:
                    repairTimer = fullRepairDuration;
                    break;
                case 3:
                    repairTimer = componentReplaceDuration;
                    break;
            }
        }

        public void CancelRepair()
        {
            isRepairing = false;
            repairTimer = 0f;
            RepairProgress = 0f;
        }

        private void Update()
        {
            if (!isRepairing) return;

            float dt = Time.deltaTime;
            repairTimer -= dt;
            RepairProgress = 1f - (repairTimer / GetRepairDuration());

            if (repairTimer <= 0f)
            {
                ExecuteRepair();
                isRepairing = false;
                RepairProgress = 1f;
            }
        }

        private float GetRepairDuration()
        {
            switch (currentRepairLevel)
            {
                case 1: return quickFixSpeed;
                case 2: return fullRepairDuration;
                case 3: return componentReplaceDuration;
                default: return fullRepairDuration;
            }
        }

        private void ExecuteRepair()
        {
            if (damageZones == null) return;

            foreach (var zone in damageZones)
            {
                if (zone == null) continue;

                switch (currentRepairLevel)
                {
                    case 1:
                        QuickFixZone(zone);
                        break;
                    case 2:
                        FullRepairZone(zone);
                        break;
                    case 3:
                        ComponentReplaceZone(zone);
                        break;
                }
            }
        }

        private void QuickFixZone(DamageZone zone)
        {
            if (zone.GetDamageNormalized() > quickFixThreshold) return;

            float repairAmount = quickFixSpeed * Time.deltaTime;

            switch (zone)
            {
                case PanelZone panel:
                    RepairPanelLerp(panel, repairAmount);
                    break;
                case NodeBeamZone nodeBeam:
                    RepairNodeBeamLerp(nodeBeam, repairAmount);
                    break;
                case RigidZone rigid:
                    rigid.ResetToOriginal();
                    break;
                case FractureZone fracture:
                    if (!fracture.IsFractured)
                        fracture.ResetToOriginal();
                    break;
            }
        }

        private void FullRepairZone(DamageZone zone)
        {
            zone.ResetToOriginal();
        }

        private void ComponentReplaceZone(DamageZone zone)
        {
            zone.ResetToOriginal();
        }

        private void RepairPanelLerp(PanelZone panel, float speed)
        {
            if (panel.DeformableVertices == null || panel.OriginalVertices == null) return;
            for (int i = 0; i < panel.DeformableVertices.Length; i++)
            {
                panel.DeformableVertices[i] = Unity.Mathematics.math.lerp(
                    panel.DeformableVertices[i], panel.OriginalVertices[i], speed);
            }
        }

        private void RepairNodeBeamLerp(NodeBeamZone nodeBeam, float speed)
        {
            var nodes = nodeBeam.GetNodes();
            if (nodes == null) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                node.Position = Unity.Mathematics.math.lerp(node.Position, node.OriginalPosition, speed);
                nodes[i] = node;
            }
        }

        /// <summary>
        /// Quick repair for a specific zone (used by pit crew system).
        /// </summary>
        public void RepairZone(DamageZone zone, int level)
        {
            if (zone == null) return;

            switch (level)
            {
                case 1:
                    QuickFixZone(zone);
                    break;
                case 2:
                    FullRepairZone(zone);
                    break;
                case 3:
                    ComponentReplaceZone(zone);
                    break;
            }
        }
    }
}
