using UnityEngine;
using System.Collections.Generic;

namespace RacingSim.Damage
{
    /// <summary>
    /// Logs damage events for telemetry and debugging.
    /// Provides impact history, zone damage history, and damage reports.
    /// </summary>
    public class DamageTelemetry : MonoBehaviour
    {
        [Header("Telemetry Settings")]
        public int maxImpactLogEntries = 100;
        public bool enableTelemetry = true;

        private List<ImpactLogEntry> impactLog = new List<ImpactLogEntry>();
        private Dictionary<string, float> zoneDamageHistory = new Dictionary<string, float>();

        public struct ImpactLogEntry
        {
            public float Timestamp;
            public Vector3 ImpactPoint;
            public float ImpactSpeed;
            public float ImpactEnergy;
            public string ZoneName;
            public float TotalDamageAfter;
        }

        public void LogImpact(ImpactData impact, string zoneName, float totalDamage)
        {
            if (!enableTelemetry) return;

            ImpactLogEntry entry = new ImpactLogEntry
            {
                Timestamp = Time.time,
                ImpactPoint = (Vector3)impact.ImpactPoint,
                ImpactSpeed = impact.ImpactSpeed,
                ImpactEnergy = impact.ImpactEnergy,
                ZoneName = zoneName,
                TotalDamageAfter = totalDamage
            };

            impactLog.Add(entry);

            if (impactLog.Count > maxImpactLogEntries)
            {
                impactLog.RemoveAt(0);
            }

            if (!zoneDamageHistory.ContainsKey(zoneName))
                zoneDamageHistory[zoneName] = 0f;
            zoneDamageHistory[zoneName] = totalDamage;
        }

        public void LogZoneDamage(string zoneName, float damage)
        {
            if (!enableTelemetry) return;
            zoneDamageHistory[zoneName] = damage;
        }

        public DamageReport GenerateReport()
        {
            DamageReport report = new DamageReport();
            report.ZoneCount = zoneDamageHistory.Count;

            float totalDamage = 0f;
            foreach (var kvp in zoneDamageHistory)
            {
                totalDamage += kvp.Value;
            }

            report.TotalDamage = report.ZoneCount > 0 ? totalDamage / report.ZoneCount : 0f;
            report.FracturedCount = impactLog.Count;

            return report;
        }

        public List<ImpactLogEntry> GetImpactHistory()
        {
            return new List<ImpactLogEntry>(impactLog);
        }

        public float GetZoneDamage(string zoneName)
        {
            if (zoneDamageHistory.ContainsKey(zoneName))
                return zoneDamageHistory[zoneName];
            return 0f;
        }

        public void ClearLog()
        {
            impactLog.Clear();
            zoneDamageHistory.Clear();
        }
    }
}
