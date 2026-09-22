using UnityEngine;

namespace RacingSim.Damage
{
    /// <summary>
    /// Abstract base class for all damage zones.
    /// Each zone represents a physical part of the vehicle that can deform, break, or fracture.
    /// </summary>
    public abstract class DamageZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        public DamageZoneConfig config;

        public float TotalDamage { get; protected set; }
        public bool IsActive { get; set; }
        public int LODLevel { get; set; }
        public bool IsDirty { get; set; }

        protected bool isInitialized;

        protected virtual void Awake()
        {
            TotalDamage = 0f;
            IsActive = false;
            LODLevel = 0;
            IsDirty = false;
        }

        /// <summary>
        /// Initialize the zone with its configuration.
        /// </summary>
        public virtual void Initialize(DamageZoneConfig cfg)
        {
            config = cfg;
            isInitialized = true;
        }

        /// <summary>
        /// Apply an impact force to this zone.
        /// </summary>
        public abstract void ApplyImpact(ImpactData impact);

        /// <summary>
        /// Update zone physics (called per physics tick when active).
        /// </summary>
        public abstract void UpdateZone(float dt);

        /// <summary>
        /// Reset all deformation back to original state.
        /// </summary>
        public abstract void ResetToOriginal();

        /// <summary>
        /// Get the normalized damage level (0 = pristine, 1 = destroyed).
        /// </summary>
        public float GetDamageNormalized()
        {
            return Mathf.Clamp01(TotalDamage);
        }

        /// <summary>
        /// Get the center of this zone in world space.
        /// </summary>
        public Vector3 GetZoneCenter()
        {
            return transform.position;
        }

        /// <summary>
        /// Check if a world point is within this zone's influence radius.
        /// </summary>
        public bool IsPointInZone(Vector3 worldPoint)
        {
            float dist = Vector3.Distance(worldPoint, GetZoneCenter());
            return dist <= config.DeformRadius;
        }
    }
}
