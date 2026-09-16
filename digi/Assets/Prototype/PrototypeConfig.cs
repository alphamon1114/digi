using System;
using UnityEngine;

namespace Digi.Prototype
{
    public enum AttackStyle { Strike, Cone, Beam }
    public enum ReporterFamily { MechanicalPlaceholder, GrandDracumon, Myotismon }

    [Serializable]
    public class FormStats
    {
        public string name;
        public int threshold;
        public float health, speed, scale, damage, range, cooldown;
        public AttackStyle attack;
        public FormStats(string label, int xp, float hp, float move, float size, float hit, float reach, float delay, AttackStyle style)
        { name = label; threshold = xp; health = hp; speed = move; scale = size; damage = hit; range = reach; cooldown = delay; attack = style; }
    }

    [CreateAssetMenu(menuName = "Digi/Prototype Rules")]
    public sealed class PrototypeConfig : ScriptableObject
    {
        public Shader surfaceShader;
        public FormStats[] villain = {
            new FormStats("MetalTyrannomon", 0, 360, 6.2f, 1.5f, 28, 3.8f, .8f, AttackStyle.Strike),
            new FormStats("Machinedramon", 80, 480, 5.1f, 2, 35, 6, 1, AttackStyle.Cone),
            new FormStats("Chaosdramon", 280, 600, 5.5f, 2.5f, 48, 13, 1.2f, AttackStyle.Beam)
        };
        public FormStats[] survivors = {
            new FormStats("Rookie", 0, 100, 6, 1, 9, 3, .55f, AttackStyle.Strike),
            new FormStats("Champion", 60, 160, 6.8f, 1.5f, 24, 4, .65f, AttackStyle.Cone),
            new FormStats("Ultimate (reserved)", 180, 210, 7, 1.8f, 32, 5, .7f, AttackStyle.Cone),
            new FormStats("Mega (reserved)", 360, 260, 7.2f, 2, 40, 7, .8f, AttackStyle.Beam)
        };
        [Range(1, 3)] public int playableSurvivorStage = 1;
        public float matchSeconds = 600, evacuationSeconds = 12, evacuationRadius = 4, contestRadius = 5, rescueSeconds = 20;
        public int wildPerSite = 2, evacuationXP = 35, wildData = 40, scavengerReward = 25, minionXP = 20;
        public float scavengerDelay = 15;
        public int scavengerCount = 2;
        public float energyMaximum = 100, energyDrain = 8, energyRecovery = 5, evolutionMinimum = 30;
        public float sensorRadius = 12, sensorInterval = 2, markerLifetime = 3, deployCooldown = 5;
        public int sensorLimit = 3;
        public ReporterFamily reporter;
        [Header("Arena / temporary balance D005")]
        [Min(30)] public float mapHalfExtent = 60;
        [Min(1)] public float coverRadius = 5, coverHeight = 9;
        [Min(10)] public float sightDistance = 32;
        public Vector3 villainSpawn = new Vector3(-38, 0, 48);
        public Vector3 survivorSpawn = new Vector3(0, 0, -48);
        public Vector3[] sites = { new Vector3(-36, 0, 32), new Vector3(36, 0, 32), new Vector3(0, 0, -40) };
        public Vector3[] rocks = {
            new Vector3(-18, 0, 20), new Vector3(18, 0, 20), new Vector3(0, 0, 20),
            new Vector3(-18, 0, -18), new Vector3(18, 0, -18), new Vector3(0, 0, -18),
            new Vector3(-35, 0, 5), new Vector3(35, 0, 5), new Vector3(0, 0, 44)
        };
        public bool BlocksSight(Vector3 from, Vector3 to, float clearance = 0)
        {
            Vector3 d = to - from;
            foreach (var rock in rocks)
            {
                Vector3 nearest = from + d * Mathf.Clamp01(Vector3.Dot(rock - from, d) / Mathf.Max(.001f, d.sqrMagnitude));
                if (Vector3.Distance(rock, nearest) < coverRadius + clearance) return true;
            }
            return false;
        }
        public Vector3 Constrain(Vector3 p)
        {
            foreach (var rock in rocks)
                if (Vector3.Distance(p, rock) < coverRadius + .6f)
                    p = rock + (p == rock ? Vector3.forward : (p - rock).normalized) * (coverRadius + .6f);
            float limit = mapHalfExtent - 1;
            p.x = Mathf.Clamp(p.x, -limit, limit); p.z = Mathf.Clamp(p.z, -limit, limit);
            return p;
        }
        public Vector3 Steer(Vector3 from, Vector3 goal, float stoppingDistance)
        {
            Vector3 delta = goal - from;
            Vector3 move = delta.magnitude > stoppingDistance ? delta.normalized : Vector3.zero;
            foreach (var rock in rocks)
                if (Vector3.Distance(from + move * 2, rock) < coverRadius + 1.2f)
                    move = Quaternion.Euler(0, 70, 0) * move;
            return move;
        }
    }
}
