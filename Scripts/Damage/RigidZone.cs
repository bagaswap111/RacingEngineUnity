namespace RacingSim.Damage
{
    /// <summary>
    /// Level 0: Rigid zone - never deforms.
    /// Used for chassis core, structural members.
    /// Only tracks accumulated damage for performance mapping.
    /// </summary>
    public class RigidZone : DamageZone
    {
        public override void Initialize(DamageZoneConfig cfg)
        {
            cfg.Type = DamageZoneType.Rigid;
            base.Initialize(cfg);
        }

        public override void ApplyImpact(ImpactData impact)
        {
            float damageAmount = impact.ImpactEnergy / config.BreakThreshold;
            TotalDamage += damageAmount * 0.01f;
            TotalDamage = UnityEngine.Mathf.Clamp01(TotalDamage);
            IsDirty = true;
        }

        public override void UpdateZone(float dt)
        {
            // Rigid zones have no physics simulation
        }

        public override void ResetToOriginal()
        {
            TotalDamage = 0f;
            IsDirty = false;
        }
    }
}
