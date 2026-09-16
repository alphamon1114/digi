using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Digi.Prototype
{
    [Serializable] public class InputCommand
    {
        public float x, z, yaw;
        public bool attack, interact, evolve, deploy;
    }
    [Serializable] public class ActorState
    {
        public int id, kind, stage, data, site = -1; // 0 survivor, 1 villain, 2 wild, 3 scavenger, 4 sensor
        public ulong owner;
        public string label;
        public Vector3 position;
        public float yaw, hp, maxHP, energy, scale = 1, attackUntil, hurtUntil;
        public bool bot;
        [NonSerialized] public float nextAttack, nextSense, nextDeploy, lastInput;
        [NonSerialized] public InputCommand input = new InputCommand();
    }
    [Serializable] public class SiteState { public int id; public float progress; public bool resolved, reported; }
    [Serializable] public class Detection { public int actor; public Vector3 position; public float expires; }
    [Serializable] public class WorldSnapshot
    {
        public List<ActorState> actors = new List<ActorState>();
        public List<SiteState> sites = new List<SiteState>();
        public List<Detection> detections = new List<Detection>();
        public int teamXP, unlock, reportSequence, localActor;
        public float time, remaining, rescue;
        public bool started, rescueReady;
        public string outcome = "";
    }

    // All mutating rules run here, exclusively on the host. Visual GameObjects are projections only.
    public sealed class MatchSimulation
    {
        public readonly PrototypeConfig Rules;
        public readonly List<ActorState> Actors = new List<ActorState>();
        public readonly List<SiteState> Sites = new List<SiteState>();
        public readonly List<Detection> Detections = new List<Detection>();
        private readonly List<(float at, Vector3 position)> pending = new List<(float, Vector3)>();
        public int TeamXP { get; private set; }
        public int ReportSequence { get; private set; }
        public float Time { get; private set; }
        public float Rescue { get; private set; }
        public bool Started { get; private set; }
        public string Outcome { get; private set; } = "";
        private int nextId = 1;
        public int Unlock => StageFor(Rules.survivors, TeamXP);
        public bool RescueReady => Sites.All(s => s.resolved);

        public MatchSimulation(PrototypeConfig rules)
        {
            Rules = rules;
            for (int i = 0; i < 3; i++)
            {
                Sites.Add(new SiteState { id = i });
                for (int j = 0; j < rules.wildPerSite; j++)
                    Spawn(2, Rules.sites[i] + new Vector3((j - .5f) * 2, 0, 0), 45, i);
            }
        }
        public ActorState AddPlayer(ulong owner, bool villain, bool bot = false)
        {
            if (Started || Actors.Any(a => a.kind <= 1 && a.owner == owner)) return null;
            if (villain ? Actors.Any(a => a.kind == 1) : Actors.Count(a => a.kind == 0) >= 3) return null;
            int slot = Actors.Count(a => a.kind == 0);
            var a = Spawn(villain ? 1 : 0, villain ? Rules.villainSpawn : Rules.survivorSpawn + new Vector3(-6 + slot * 6, 0, 0), 1);
            a.owner = owner; a.bot = bot; a.energy = Rules.energyMaximum;
            a.label = villain ? Rules.villain[0].name : (bot ? "BOT " : "P ") + (slot + 1);
            ApplyForm(a, 0, true);
            return a;
        }
        public bool Start(bool fillBots)
        {
            if (Started) return false;
            if (fillBots) for (int i = Actors.Count(a => a.kind == 0); i < 3; i++) AddPlayer(ulong.MaxValue - (ulong)i, false, true);
            if (!Actors.Any(a => a.kind == 1) || !Actors.Any(a => a.kind == 0)) return false;
            Started = true; return true;
        }
        public void Disconnect(ulong owner)
        {
            var a = Actors.Find(p => p.kind <= 1 && p.owner == owner);
            if (a == null) return;
            if (Started) { if (Outcome.Length == 0) a.hp = 0; } else Actors.Remove(a);
        }
        public void Submit(ulong sender, InputCommand command)
        {
            var a = Actors.Find(p => p.kind <= 1 && p.owner == sender);
            if (a == null || a.bot || a.hp <= 0 || command == null || !Finite(command.x) || !Finite(command.z) || !Finite(command.yaw)) return;
            var move = Vector2.ClampMagnitude(new Vector2(command.x, command.z), 1);
            // Merge edge commands until the next simulation tick; held input expires after packet loss.
            a.input.x = move.x; a.input.z = move.y; a.input.yaw = Mathf.Repeat(command.yaw, 360);
            a.input.interact = command.interact;
            a.input.attack |= command.attack; a.input.evolve |= command.evolve; a.input.deploy |= command.deploy;
            a.lastInput = Time;
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public FormStats Form(ActorState a) => (a.kind == 1 ? Rules.villain : Rules.survivors)[a.stage];
        private static int StageFor(FormStats[] forms, int xp)
        { int s = 0; for (int i = 1; i < forms.Length; i++) if (xp >= forms[i].threshold) s = i; return s; }
        private ActorState Spawn(int kind, Vector3 at, float hp, int site = -1)
        {
            var a = new ActorState { id = nextId++, kind = kind, position = at, hp = hp, maxHP = hp, site = site,
                label = kind == 2 ? "Wild" : kind == 3 ? "Scavenger" : "Sensor" };
            Actors.Add(a); return a;
        }
        private void ApplyForm(ActorState a, int stage, bool full = false)
        {
            float ratio = full ? 1 : a.hp / a.maxHP;
            a.stage = stage; var f = Form(a); a.maxHP = f.health; a.hp = f.health * ratio; a.scale = f.scale;
            if (a.kind == 1) a.label = f.name;
        }
        public void Tick(float dt)
        {
            if (!Started || Outcome.Length > 0 || dt <= 0) return;
            dt = Mathf.Min(dt, .1f); Time += dt;
            foreach (var a in Actors.Where(a => a.kind <= 1 && a.hp > 0).ToArray())
            {
                if (a.hp <= 0) continue; // An earlier actor may have killed this actor during the same tick.
                if (a.bot) Think(a);
                if (Time - a.lastInput > .4f && !a.bot) a.input = new InputCommand { yaw = a.yaw };
                a.yaw = a.input.yaw;
                Vector3 p = a.position + new Vector3(a.input.x, 0, a.input.z) * Form(a).speed * dt;
                a.position = Rules.Constrain(p);
                if (a.kind == 0)
                {
                    if (a.stage > 0)
                    { a.energy = Mathf.Max(0, a.energy - Rules.energyDrain * dt); if (a.energy <= 0) ApplyForm(a, 0); }
                    else a.energy = Mathf.Min(Rules.energyMaximum, a.energy + Rules.energyRecovery * dt);
                    if (a.input.evolve && a.stage == 0 && Unlock > 0 && a.energy >= Rules.evolutionMinimum)
                        ApplyForm(a, Mathf.Min(Unlock, Rules.playableSurvivorStage));
                }
                if (a.input.attack) Attack(a);
                if (a.input.deploy && a.kind == 1 && Time >= a.nextDeploy && Actors.Count(v => v.kind == 4 && v.hp > 0) < Rules.sensorLimit)
                { Spawn(4, a.position, 35); a.nextDeploy = Time + Rules.deployCooldown; }
                a.input.attack = a.input.evolve = a.input.deploy = false;
            }
            foreach (var a in Actors.Where(a => a.kind == 3 && a.hp > 0).ToArray())
            {
                var target = Actors.Where(p => p.kind <= 1 && p.hp > 0).OrderBy(p => Vector3.Distance(p.position, a.position)).FirstOrDefault();
                if (target == null) continue;
                float d = Vector3.Distance(target.position, a.position);
                if (d < 8 && d > 1.5f) a.position = Rules.Constrain(a.position + Rules.Steer(a.position, target.position, 1.5f) * dt * 2.5f);
                if (d < 2 && Time >= a.nextAttack && !Rules.BlocksSight(a.position, target.position)) { Hurt(target, 5); a.nextAttack = Time + 1.5f; }
            }
            foreach (var site in Sites)
            {
                if (site.resolved) continue;
                var wild = Actors.Where(a => a.kind == 2 && a.site == site.id && a.hp > 0).ToArray();
                if (wild.Length == 0) { site.resolved = true; continue; }
                Vector3 at = Rules.sites[site.id];
                bool working = Actors.Any(a => a.kind == 0 && a.hp > 0 && a.input.interact && Vector3.Distance(a.position, at) <= Rules.evacuationRadius);
                bool contested = Actors.Any(a => a.kind == 1 && a.hp > 0 && Vector3.Distance(a.position, at) <= Rules.contestRadius);
                if (!working || contested) continue;
                site.progress = Mathf.Min(Rules.evacuationSeconds, site.progress + dt);
                if (!site.reported) { site.reported = true; ReportSequence++; }
                if (site.progress >= Rules.evacuationSeconds)
                { foreach (var w in wild) w.hp = 0; TeamXP += wild.Length * Rules.evacuationXP; site.resolved = true; }
            }
            for (int i = pending.Count - 1; i >= 0; i--)
                if (pending[i].at <= Time)
                {
                    for (int j = 0; j < Rules.scavengerCount; j++) Spawn(3, pending[i].position + new Vector3(j - .5f, 0, 1), 40);
                    pending.RemoveAt(i);
                }
            Detections.RemoveAll(d => d.expires <= Time);
            foreach (var sensor in Actors.Where(a => a.kind == 4 && a.hp > 0))
            {
                if (Time < sensor.nextSense) continue;
                sensor.nextSense = Time + Rules.sensorInterval;
                foreach (var p in Actors.Where(a => a.kind == 0 && a.hp > 0 && Vector3.Distance(a.position, sensor.position) <= Rules.sensorRadius))
                {
                    Detections.RemoveAll(d => d.actor == p.id);
                    Detections.Add(new Detection { actor = p.id, position = p.position, expires = Time + Rules.markerLifetime });
                }
            }
            if (RescueReady && Actors.Any(a => a.kind == 0 && a.hp > 0 && a.input.interact && a.position.magnitude < 4)
                && !Actors.Any(a => a.kind == 1 && a.hp > 0 && a.position.magnitude < Rules.contestRadius)) Rescue += dt;
            if (!Actors.Any(a => a.kind == 1 && a.hp > 0)) Outcome = "SURVIVORS WIN - villain defeated";
            else if (!Actors.Any(a => a.kind == 0 && a.hp > 0)) Outcome = "VILLAIN WINS - team eliminated";
            else if (Rescue >= Rules.rescueSeconds) Outcome = "SURVIVORS WIN - rescue complete";
            else if (Time >= Rules.matchSeconds) Outcome = "VILLAIN WINS - time expired";
        }
        private void Attack(ActorState a)
        {
            if (Time < a.nextAttack) return;
            var f = Form(a); a.nextAttack = Time + f.cooldown; a.attackUntil = Time + .2f;
            var forward = Quaternion.Euler(0, a.yaw, 0) * Vector3.forward;
            var targets = Actors.Where(t => t.hp > 0 && t.id != a.id &&
                (a.kind == 1 ? t.kind == 0 || t.kind == 2 || t.kind == 3 : t.kind == 1 || t.kind == 3 || t.kind == 4))
                .OrderBy(t => Vector3.Distance(t.position, a.position)).ToArray();
            foreach (var t in targets)
            {
                Vector3 delta = t.position - a.position; float distance = delta.magnitude;
                if (distance > f.range || Vector3.Dot(delta.normalized, forward) < (f.attack == AttackStyle.Beam ? .95f : .35f)) continue;
                bool blocked = Rules.BlocksSight(a.position, t.position);
                if (blocked) continue;
                Hurt(t, f.damage);
                if (t.hp <= 0)
                {
                    if (t.kind == 2) { a.data += Rules.wildData; pending.Add((Time + Rules.scavengerDelay, t.position)); }
                    if (t.kind == 3) { if (a.kind == 1) a.data += Rules.scavengerReward; else TeamXP += Rules.scavengerReward; }
                    if (t.kind == 4 && a.kind == 0) TeamXP += Rules.minionXP;
                    if (a.kind == 1) ApplyForm(a, StageFor(Rules.villain, a.data));
                }
                if (f.attack != AttackStyle.Cone) break;
            }
        }
        private void Hurt(ActorState a, float damage) { a.hp = Mathf.Max(0, a.hp - damage); a.hurtUntil = Time + .2f; }
        private void Think(ActorState a)
        {
            var enemy = Actors.Where(t => t.hp > 0 && (t.kind == 1 || t.kind == 3 || t.kind == 4)
                && !Rules.BlocksSight(a.position, t.position)).OrderBy(t => Vector3.Distance(t.position, a.position)).FirstOrDefault();
            Vector3 target = Vector3.zero;
            var site = Sites.Where(s => !s.resolved).OrderBy(s => Vector3.Distance(a.position, Rules.sites[s.id])).FirstOrDefault();
            if (site != null) target = Rules.sites[site.id];
            bool fight = enemy != null && Vector3.Distance(enemy.position, a.position) < 7;
            if (fight) target = enemy.position;
            Vector3 d = target - a.position;
            Vector3 move = Rules.Steer(a.position, target, fight ? Form(a).range * .8f : 2);
            a.input = new InputCommand { x = move.x, z = move.z, yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg,
                attack = fight, evolve = fight, interact = !fight };
        }
        public WorldSnapshot Snapshot(ulong recipient)
        {
            var me = Actors.Find(a => a.kind <= 1 && a.owner == recipient);
            bool villain = me != null && me.kind == 1;
            // Copy only at serialization boundary. Never broadcast an unfiltered snapshot.
            return new WorldSnapshot { actors = Actors, sites = villain ? new List<SiteState>() : Sites,
                detections = villain ? Detections : new List<Detection>(), teamXP = villain ? 0 : TeamXP,
                unlock = villain ? 0 : Unlock, reportSequence = villain ? ReportSequence : 0,
                localActor = me == null ? -1 : me.id, time = Time, remaining = Mathf.Max(0, Rules.matchSeconds - Time),
                rescue = villain ? 0 : Rescue, rescueReady = RescueReady, started = Started, outcome = Outcome };
        }
    }
}
