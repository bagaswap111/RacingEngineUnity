using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Manages damage LOD levels based on camera distance.
    /// LOD 0: Full detail (0-10m)
    /// LOD 1: Medium (10-20m) - skip normals
    /// LOD 2: Low (20-50m) - use damage texture only
    /// LOD 3: Minimal (50m+) - damage tint only
    /// </summary>
    public class DamageLODManager : MonoBehaviour
    {
        [Header("LOD Distances")]
        public float lod0Distance = 10f;
        public float lod1Distance = 20f;
        public float lod2Distance = 50f;

        private DamageZone[] damageZones;
        private Transform cameraTransform;

        public int CurrentLODLevel { get; private set; }

        private void Start()
        {
            damageZones = GetComponentsInChildren<DamageZone>();
            cameraTransform = Camera.main?.transform;
        }

        public void RegisterZones(DamageZone[] zones)
        {
            damageZones = zones;
        }

        public void UpdateLOD(Vector3 cameraPosition, Vector3 vehiclePosition)
        {
            float distance = Vector3.Distance(cameraPosition, vehiclePosition);

            if (distance < lod0Distance)
            {
                CurrentLODLevel = 0;
                SetAllZonesLOD(0, true, true, true, true);
            }
            else if (distance < lod1Distance)
            {
                CurrentLODLevel = 1;
                SetAllZonesLOD(1, true, true, false, true);
            }
            else if (distance < lod2Distance)
            {
                CurrentLODLevel = 2;
                SetAllZonesLOD(2, false, false, false, true);
            }
            else
            {
                CurrentLODLevel = 3;
                SetAllZonesLOD(3, false, false, false, false);
            }
        }

        private void SetAllZonesLOD(int lodLevel, bool nodeBeamEnabled,
            bool meshUpdateEnabled, bool normalRecalcEnabled, bool fractureEnabled)
        {
            if (damageZones == null) return;

            foreach (var zone in damageZones)
            {
                if (zone == null) continue;

                zone.LODLevel = lodLevel;

                switch (zone)
                {
                    case NodeBeamZone nodeBeam:
                        nodeBeam.IsActive = nodeBeamEnabled && nodeBeam.IsDirty;
                        break;
                    case PanelZone panel:
                        panel.IsActive = meshUpdateEnabled && panel.IsDirty;
                        break;
                    case FractureZone fracture:
                        if (!fractureEnabled && !fracture.IsFractured)
                        {
                            fracture.IsActive = false;
                        }
                        break;
                }
            }
        }

        public void UpdateLODFromCamera()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
                if (cameraTransform == null) return;
            }

            UpdateLOD(cameraTransform.position, transform.position);
        }

        public bool ShouldUpdateNodeBeam()
        {
            return CurrentLODLevel <= 1;
        }

        public bool ShouldUpdateMesh()
        {
            return CurrentLODLevel <= 1;
        }

        public bool ShouldRecalculateNormals()
        {
            return CurrentLODLevel == 0;
        }

        public bool ShouldCheckFracture()
        {
            return CurrentLODLevel <= 2;
        }
    }
}
