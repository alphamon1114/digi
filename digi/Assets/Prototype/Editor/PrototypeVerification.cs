using System;
using System.Linq;
using UnityEngine;

namespace Digi.Prototype.Editor
{
    // Deterministic integration checks using the exact simulation used by the host.
    public static class PrototypeVerification
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception("DIGI verification failed: " + message); }
        private static void Step(MatchSimulation s, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) s.Tick(.05f); }
        private static void Hold(MatchSimulation s, ulong owner, float seconds, bool attack = false, bool interact = false, float yaw = 0)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { s.Submit(owner, new InputCommand { attack = attack, interact = interact, yaw = yaw }); s.Tick(.05f); } }
        public static void Run()
        {
            assertions = 0;
            var rules = ScriptableObject.CreateInstance<PrototypeConfig>();
            try
            {
                var s = new MatchSimulation(rules); var v = s.AddPlayer(0, true); var p = s.AddPlayer(1, false); s.AddPlayer(2, false); s.AddPlayer(3, false);
                Check(s.AddPlayer(4, false) == null, "fifth player rejected"); Check(s.Start(false), "start 1v3");
                Check(s.AddPlayer(9, false) == null, "late join rejected");
                Vector3 initial = p.position;
                s.Submit(999, new InputCommand { x = 100 }); s.Submit(1, new InputCommand { x = float.NaN }); Step(s, .1f);
                Check(p.position == initial, "unknown sender and NaN ignored");
                s.Submit(1, new InputCommand { x = 1000 }); s.Tick(.05f);
                Check(Vector3.Distance(initial, p.position) <= rules.survivors[0].speed * .05f + .001f, "speed bounded");
                Step(s, 1); initial = p.position; Step(s, 1); Check(initial == p.position, "stale input expires");

                p.position = PrototypeConfig.Sites[0];
                Hold(s, 1, .2f, interact: true);
                Check(s.ReportSequence == 1 && s.Sites[0].progress > 0, "report only when actual progress");
                var villainView = JsonUtility.ToJson(s.Snapshot(0)); var survivorView = JsonUtility.ToJson(s.Snapshot(1));
                Check(s.Snapshot(0).sites.Count == 0 && !villainView.Contains("progress"), "report payload contains no site progress");
                Check(s.Snapshot(1).reportSequence == 0 && survivorView.Contains("progress"), "report private, survivor progress visible");
                float progress = s.Sites[0].progress; v.position = p.position;
                Hold(s, 1, 1, interact: true); Check(s.Sites[0].progress == progress, "villain contests evacuation");
                v.position = new Vector3(0, 0, 27); Hold(s, 1, 13, interact: true);
                Check(s.TeamXP == 70 && s.Unlock == 1, "evacuation independently unlocks Champion");
                Hold(s, 1, 1, interact: true); Check(s.TeamXP == 70 && s.ReportSequence == 1, "evac rewards/report exactly once");
                Check(s.Actors.All(a => a.kind != 2 || a.site != 0 || a.hp == 0), "evacuated wild removed");
                s.Submit(1, new InputCommand { evolve = true }); s.Tick(.05f); Check(p.stage == 1, "individual evolution");
                Step(s, 13); Check(p.stage == 0 && s.TeamXP == 70 && s.Unlock == 1, "devolution retains XP/unlock");
                Check(s.Snapshot(1).teamXP == s.Snapshot(2).teamXP && s.Snapshot(2).unlock == s.Snapshot(3).unlock, "team state consistent across recipient snapshots");

                v.position = new Vector3(24, 0, 0); p.position = v.position + new Vector3(2, 0, 0);
                s.Submit(0, new InputCommand { deploy = true }); s.Tick(.05f);
                Check(s.Snapshot(0).detections.Count > 0 && s.Snapshot(1).detections.Count == 0, "sensor marks villain only");
                p.position = new Vector3(-28, 0, -28); Step(s, 4);
                Check(s.Detections.Count == 0, "sensor mark expiry");
                var sensor = s.Actors.First(a => a.kind == 4);
                p.position = sensor.position - Vector3.forward * 2;
                v.position = new Vector3(0, 0, 27);
                Hold(s, 1, 3, attack: true);
                Check(sensor.hp == 0 && s.TeamXP == 90, "survivor removes sensor for shared reward");
                Hold(s, 1, 1, attack: true); Check(s.TeamXP == 90, "sensor reward once");
                for (int i = 0; i < 5; i++) { s.Submit(0, new InputCommand { deploy = true }); Step(s, 5.1f); }
                Check(s.Actors.Count(a => a.kind == 4 && a.hp > 0) == rules.sensorLimit, "sensor placement cap");

                var hunt = new MatchSimulation(rules); var hunter = hunt.AddPlayer(0, true); hunt.AddPlayer(1, false); hunt.Start(false);
                var wild = hunt.Actors.First(a => a.kind == 2); hunter.position = wild.position - Vector3.forward * 2;
                foreach (var other in hunt.Actors.Where(a => a.kind == 2 && a.id != wild.id)) other.position = new Vector3(27, 0, -27);
                Hold(hunt, 0, 2, attack: true);
                Check(wild.hp == 0 && hunter.data == 40, "wild reward once");
                Hold(hunt, 0, 2, attack: true); Check(hunter.data == 40, "dead target cannot reward again");
                Step(hunt, 16); Check(hunt.Actors.Count(a => a.kind == 3) == 2, "one delayed spawn group");
                foreach (var scav in hunt.Actors.Where(a => a.kind == 3)) { scav.position = hunter.position + Vector3.forward * 2; scav.hp = 1; }
                Hold(hunt, 0, 3, attack: true); Step(hunt, 20);
                Check(hunt.Actors.Count(a => a.kind == 3) == 2 && hunter.data == 90, "scavenger reward and no recursive spawn");
                foreach (var other in hunt.Actors.Where(a => a.kind == 2 && a.hp > 0).ToArray())
                { other.position = hunter.position + Vector3.forward * 2; other.hp = 1; Hold(hunt, 0, 1.3f, attack: true); }
                Check(hunter.stage == 2 && hunter.data == 290, "three permanent villain forms");
                Step(hunt, 1); Check(hunter.stage == 2, "villain form persists");

                var escape = new MatchSimulation(rules); escape.AddPlayer(0, true); var runner = escape.AddPlayer(1, false); escape.Start(false);
                foreach (var site in PrototypeConfig.Sites) { runner.position = site; Hold(escape, 1, 13, interact: true); }
                Check(escape.RescueReady, "all sites resolve"); runner.position = Vector3.zero;
                Hold(escape, 1, 21, interact: true); Check(escape.Outcome.Contains("rescue complete"), "round ends in rescue victory");
                int xp = escape.TeamXP; Hold(escape, 1, 2, attack: true); Check(escape.TeamXP == xp, "finished match frozen");
                var defeat = new MatchSimulation(rules); defeat.AddPlayer(0, true); defeat.AddPlayer(1, false); defeat.Start(false); defeat.Disconnect(1); defeat.Tick(.05f);
                Check(defeat.Outcome.Contains("eliminated"), "disconnect elimination win");
                var kill = new MatchSimulation(rules); kill.AddPlayer(0, true); kill.AddPlayer(1, false); kill.Start(false); kill.Disconnect(0); kill.Tick(.05f);
                Check(kill.Outcome.Contains("defeated"), "villain defeat win");
                rules.matchSeconds = .1f;
                var timeout = new MatchSimulation(rules); timeout.AddPlayer(0, true); timeout.AddPlayer(1, false); timeout.Start(false); Step(timeout, .2f);
                Check(timeout.Outcome.Contains("time expired"), "timeout victory");
                Debug.Log("DIGI: " + assertions + " simulation assertions passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }
    }
}
