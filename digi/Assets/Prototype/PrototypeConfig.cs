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
            new FormStats("MetalTyrannomon", 0, 360, 5.3f, 1.5f, 28, 3.8f, .8f, AttackStyle.Strike),
            new FormStats("Machinedramon", 120, 480, 5.1f, 2, 35, 6, 1, AttackStyle.Cone),
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
        public static readonly Vector3[] Sites = { new Vector3(-18, 0, 15), new Vector3(18, 0, 15), new Vector3(0, 0, -22) };
        public static readonly Vector3[] Rocks = { new Vector3(-9, 0, 0), new Vector3(9, 0, 0), new Vector3(0, 0, 12), new Vector3(-10, 0, -13), new Vector3(10, 0, -13) };
    }
}
