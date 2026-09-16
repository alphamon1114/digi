using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Digi.Prototype
{
    public sealed class PrototypeSession : MonoBehaviour
    {
        public PrototypeConfig rules;
        private NetworkManager network;
        private UnityTransport transport;
        private MatchSimulation simulation;
        private WorldSnapshot world;
        private readonly Dictionary<int, GameObject> models = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<GameObject> mapObjects = new List<GameObject>();
        private Camera view;
        private float yaw = 180, pitch = 25, sendClock, tickClock, reportUntil;
        private int lastReport;
        private string address = "127.0.0.1", status = "Host a match or connect to a host.", portText = "7777";
        private bool fillBots = true;
        private InputCommand queued = new InputCommand();
        private AudioSource reportAudio;
        private AudioClip reportClip;
        private bool automated;
        private float autoQuit;
        private string traceFile;
        private string screenshotFile;
        private Texture2D portrait;
        private bool headless;

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            if (rules == null) rules = ScriptableObject.CreateInstance<PrototypeConfig>();
            var net = new GameObject("NGO Session");
            network = net.AddComponent<NetworkManager>(); transport = net.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, TickRate = 30 };
            network.ConnectionApprovalCallback = Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (headless) return;
            BuildMap();
            reportAudio = gameObject.AddComponent<AudioSource>(); reportAudio.spatialBlend = 0;
            reportClip = AudioClip.Create("Non directional radio report", 11025, 1, 22050, false);
            var wave = new float[11025];
            for (int i = 0; i < wave.Length; i++) wave[i] = Mathf.Sin(i * .12f) * .12f * (1 - i / (float)wave.Length);
            reportClip.SetData(wave, 0); reportAudio.clip = reportClip;
            portrait = new Texture2D(64, 64) { filterMode = FilterMode.Point };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                bool face = (x - 32) * (x - 32) + (y - 30) * (y - 30) < 650;
                bool ears = y > 39 && y < 60 && ((x > 10 && x < 23) || (x > 41 && x < 54));
                bool eye = y > 29 && y < 36 && ((x > 17 && x < 26) || (x > 38 && x < 47));
                portrait.SetPixel(x, y, eye ? Color.yellow : face || ears ? new Color(.48f, .22f, .65f) : new Color(.04f, .04f, .08f));
            }
            portrait.Apply();
        }
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            if (Arg("-digiPort") != null) portText = Arg("-digiPort");
            if (Arg("-digiTrace") != null) traceFile = Arg("-digiTrace");
            screenshotFile = Arg("-digiScreenshot");
            automated = args.Contains("-digiAuto");
            if (float.TryParse(Arg("-digiQuit"), out float seconds)) autoQuit = seconds;
            if (args.Contains("-digiHost")) Connect(true);
            else if (Arg("-digiClient") != null) { address = Arg("-digiClient"); Connect(false); }
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = simulation != null && !simulation.Started && network.ConnectedClientsIds.Count < 4;
            response.CreatePlayerObject = false; response.Pending = false;
            if (!response.Approved) response.Reason = "Match started or lobby full (1 versus 3).";
        }
        private void Connect(bool host)
        {
            if (!ushort.TryParse(portText, out var port) || port == 0) { status = "Port must be 1..65535."; return; }
            transport.SetConnectionData(address.Trim(), port, "0.0.0.0");
            simulation = host ? new MatchSimulation(rules) : null;
            bool ok = host ? network.StartHost() : network.StartClient();
            status = ok ? "Connecting / lobby..." : "Could not start transport. Check address and port.";
            if (!ok) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler("digi.input", ReceiveInput);
            network.CustomMessagingManager.RegisterNamedMessageHandler("digi.state", ReceiveState);
        }
        private void Connected(ulong id)
        {
            if (network.IsServer)
            { simulation.AddPlayer(id, id == NetworkManager.ServerClientId); world = simulation.Snapshot(network.LocalClientId); }
            status = "Connected. Host starts the round.";
        }
        private void Disconnected(ulong id)
        {
            if (network.IsServer) simulation?.Disconnect(id);
            else { world = null; status = "Disconnected: " + network.DisconnectReason; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        private void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || reader.Length > 2048) return;
            try { reader.ReadValueSafe(out string json); simulation.Submit(sender, JsonUtility.FromJson<InputCommand>(json)); }
            catch (Exception e) { Debug.LogWarning("Rejected malformed input: " + e.GetType().Name); }
        }
        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (network.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json); world = JsonUtility.FromJson<WorldSnapshot>(json);
        }
        private void Send(string channel, ulong client, object value)
        {
            string json = JsonUtility.ToJson(value);
            using var writer = new FastBufferWriter(json.Length * 4 + 16, Allocator.Temp);
            writer.WriteValueSafe(json);
            network.CustomMessagingManager.SendNamedMessage(channel, client, writer,
                channel == "digi.state" ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.ReliableSequenced);
        }
        private void Update()
        {
            if (!string.IsNullOrEmpty(screenshotFile) && Time.realtimeSinceStartup > 25)
            { ScreenCapture.CaptureScreenshot(screenshotFile); if (!headless) CaptureWorld(screenshotFile + ".world.png"); screenshotFile = null; }
            if (autoQuit > 0 && Time.realtimeSinceStartup > autoQuit) { WriteTrace(); Application.Quit(); }
            if (network == null || !network.IsListening) return;
            if (automated && network.IsServer && simulation != null && !simulation.Started && Time.realtimeSinceStartup > 12) simulation.Start(true);
            ReadControls();
            sendClock += Time.unscaledDeltaTime;
            if (sendClock >= .05f && network.IsConnectedClient)
            {
                sendClock = 0;
                if (network.IsServer) simulation.Submit(network.LocalClientId, queued); else Send("digi.input", NetworkManager.ServerClientId, queued);
                queued.attack = queued.evolve = queued.deploy = false;
            }
            if (network.IsServer && simulation != null)
            {
                tickClock += Mathf.Min(Time.unscaledDeltaTime, .2f);
                while (tickClock >= .05f) { simulation.Tick(.05f); tickClock -= .05f; }
                if (sendClock == 0)
                {
                    foreach (ulong id in network.ConnectedClientsIds)
                        if (id != network.LocalClientId) Send("digi.state", id, simulation.Snapshot(id));
                    world = simulation.Snapshot(network.LocalClientId);
                }
            }
            if (!headless) RenderWorld();
        }
        private void ReadControls()
        {
            if (automated && world != null && world.started)
            {
                var actor = world.actors.Find(a => a.id == world.localActor);
                if (actor != null && actor.kind == 0 && actor.hp > 0)
                {
                    var site = world.sites.Where(s => !s.resolved).OrderBy(s => Vector3.Distance(actor.position, PrototypeConfig.Sites[s.id])).FirstOrDefault();
                    Vector3 goal = site == null ? Vector3.zero : PrototypeConfig.Sites[site.id];
                    Vector3 delta = goal - actor.position;
                    Vector3 autoMove = delta.magnitude > 2 ? delta.normalized : Vector3.zero;
                    foreach (var rock in PrototypeConfig.Rocks)
                        if (Vector3.Distance(actor.position + autoMove * 2, rock) < 3) autoMove = Quaternion.Euler(0, 70, 0) * autoMove;
                    queued = new InputCommand { x = autoMove.x, z = autoMove.z, yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg,
                        interact = true, evolve = world.unlock > 0 && world.time < 35 };
                }
                return;
            }
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (world == null || !world.started || world.outcome.Length > 0) return;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                yaw += mouse.delta.ReadValue().x * .15f; pitch = Mathf.Clamp(pitch - mouse.delta.ReadValue().y * .1f, 10, 65);
            }
            Vector2 axis = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y).normalized;
            queued.x = move.x; queued.z = move.z; queued.yaw = yaw;
            queued.attack |= mouse != null && mouse.leftButton.isPressed;
            queued.interact = keys.eKey.isPressed;
            queued.evolve |= keys.qKey.wasPressedThisFrame; queued.deploy |= keys.fKey.wasPressedThisFrame;
        }
        private GameObject Shape(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            var shader = rules.surfaceShader != null ? rules.surfaceShader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader); material.color = color; materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        private void BuildMap()
        {
            view = Camera.main;
            if (view == null) { var cameraObject = new GameObject("Third Person Camera", typeof(Camera), typeof(AudioListener)); view = cameraObject.GetComponent<Camera>(); cameraObject.tag = "MainCamera"; }
            view.transform.position = new Vector3(0, 36, -32); view.transform.LookAt(Vector3.zero);
            view.backgroundColor = new Color(.035f, .055f, .09f); view.clearFlags = CameraClearFlags.SolidColor;
            mapObjects.Add(Shape("Digital arena", PrimitiveType.Cube, new Vector3(0, -.6f, 0), new Vector3(60, 1, 60), new Color(.08f, .16f, .2f)));
            foreach (var at in PrototypeConfig.Sites) mapObjects.Add(Shape("Evacuation site", PrimitiveType.Cylinder, at + Vector3.up * .04f, new Vector3(8, .06f, 8), new Color(.05f, .65f, .55f)));
            mapObjects.Add(Shape("Rescue uplink", PrimitiveType.Cylinder, Vector3.up * .1f, new Vector3(6, .15f, 6), new Color(.25f, .5f, 1)));
            foreach (var at in PrototypeConfig.Rocks) mapObjects.Add(Shape("Data pillar", PrimitiveType.Cylinder, at + Vector3.up * 2, new Vector3(3.6f, 2, 3.6f), new Color(.12f, .24f, .32f)));
            for (int i = -30; i <= 30; i += 10)
            {
                mapObjects.Add(Shape("Grid X", PrimitiveType.Cube, new Vector3(i, -.085f, 0), new Vector3(.04f, .02f, 60), new Color(.1f, .35f, .4f)));
                mapObjects.Add(Shape("Grid Z", PrimitiveType.Cube, new Vector3(0, -.085f, i), new Vector3(60, .02f, .04f), new Color(.1f, .35f, .4f)));
            }
        }
        private void RenderWorld()
        {
            if (world == null) return;
            foreach (var a in world.actors)
            {
                if (!models.TryGetValue(a.id, out var go))
                {
                    go = Shape(a.label, a.kind == 4 ? PrimitiveType.Cube : PrimitiveType.Capsule, a.position, Vector3.one, Color.white);
                    models.Add(a.id, go);
                    var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); nose.name = "Facing"; nose.transform.SetParent(go.transform, false);
                    nose.transform.localPosition = new Vector3(0, .2f, .55f); nose.transform.localScale = new Vector3(.25f, .25f, .45f);
                    nose.GetComponent<Renderer>().sharedMaterial = go.GetComponent<Renderer>().sharedMaterial;
                }
                go.SetActive(a.hp > 0);
                go.transform.position = Vector3.Lerp(go.transform.position, a.position + Vector3.up * a.scale, Mathf.Min(1, Time.unscaledDeltaTime * 20));
                go.transform.rotation = Quaternion.Euler(0, a.yaw, 0); go.transform.localScale = Vector3.one * a.scale;
                Color c = a.kind == 1 ? new Color(.85f, .15f, .25f) : a.kind == 0 ? (a.stage > 0 ? Color.cyan : new Color(.25f, .6f, 1)) : a.kind == 2 ? Color.green : a.kind == 3 ? new Color(1, .65f, .1f) : new Color(.7f, .25f, .9f);
                go.GetComponent<Renderer>().sharedMaterial.color = a.hurtUntil > world.time ? Color.white : a.attackUntil > world.time ? Color.yellow : c;
            }
            foreach (int id in models.Keys.Where(id => world.actors.All(a => a.id != id)).ToArray()) { Destroy(models[id]); models.Remove(id); }
            var me = world.actors.Find(a => a.id == world.localActor);
            if (me != null)
            {
                Vector3 focus = me.position + Vector3.up * 1.5f;
                Vector3 cameraPosition = focus + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -9 - me.scale);
                if (Physics.Linecast(focus + Vector3.up * 2, cameraPosition, out var hit) && hit.collider.gameObject.name == "Data pillar") cameraPosition = hit.point + hit.normal * .4f;
                view.transform.position = Vector3.Lerp(view.transform.position, cameraPosition, Time.unscaledDeltaTime * 12); view.transform.LookAt(focus);
            }
            if (world.reportSequence > lastReport)
            { lastReport = world.reportSequence; reportUntil = Time.unscaledTime + 6; reportAudio.Play(); }
        }
        private void OnGUI()
        {
            if (headless) return;
            GUI.skin.label.fontSize = 16; GUI.skin.button.fontSize = 16;
            GUILayout.BeginArea(new Rect(15, 15, 470, 390), GUI.skin.box);
            GUILayout.Label("DIGITAL FRONTIER | 1 vs 3 prototype");
            if (network == null || !network.IsListening)
            {
                GUILayout.Label(status); GUILayout.Label("Host IP / port");
                address = GUILayout.TextField(address); portText = GUILayout.TextField(portText);
                if (GUILayout.Button("HOST (Villain)")) Connect(true);
                if (GUILayout.Button("JOIN (Rookie)")) Connect(false);
            }
            else if (world == null || !world.started)
            {
                GUILayout.Label(status);
                GUILayout.Label("Players: " + (world == null ? "connecting" : world.actors.Count(a => a.kind <= 1).ToString()) + " / 4");
                if (network.IsServer)
                {
                    fillBots = GUILayout.Toggle(fillBots, "Fill empty survivor slots with practice bots");
                    if (GUILayout.Button("START ROUND")) { if (!simulation.Start(fillBots)) status = "Need at least one survivor or enable bots."; }
                }
            }
            else
            {
                var me = world.actors.Find(a => a.id == world.localActor);
                GUILayout.Label($"Time {world.remaining:0}s | {world.outcome}");
                if (me != null)
                {
                    GUILayout.Label($"{me.label} | HP {me.hp:0}/{me.maxHP:0} | Stage {me.stage + 1}");
                    GUILayout.Label(me.kind == 1 ? $"Growth data {me.data} | F: deploy sensor" : $"Team XP {world.teamXP} | Unlock {world.unlock} | Energy {me.energy:0}");
                    if (me.hp <= 0) GUILayout.Label("ELIMINATED - spectating until round ends");
                }
                GUILayout.Label("WASD move | Hold RMB look/aim | LMB attack");
                GUILayout.Label("Hold E evacuate / rescue | Q evolve | Esc cursor");
                GUILayout.Label(world.rescueReady ? "RESCUE READY - hold E at central blue uplink" : "Contest wild sites / hunt to grow");
                foreach (var s in world.sites) GUILayout.Label($"Site {s.id + 1}: {(s.resolved ? "Resolved" : (s.progress / rules.evacuationSeconds * 100).ToString("0") + "%")}");
                if (world.sites.Count > 0) GUILayout.Label($"Rescue {world.rescue:0}/{rules.rescueSeconds:0}s");
            }
            if (network != null && network.IsListening && GUILayout.Button("DISCONNECT / RETURN TO LOBBY")) ResetSession();
            GUILayout.EndArea();
            if (world == null) return;
            var local = world.actors.Find(a => a.id == world.localActor);
            foreach (var a in world.actors)
            {
                if (a.hp <= 0 || (local != null && Vector3.Distance(local.position, a.position) > 22)) continue;
                Vector3 point = view.WorldToScreenPoint(a.position + Vector3.up * (a.scale * 2 + .6f));
                if (Physics.Linecast(view.transform.position, a.position + Vector3.up * a.scale, out var sight)
                    && models.TryGetValue(a.id, out var body) && sight.collider.transform.root != body.transform) continue;
                if (point.z > 0) GUI.Label(new Rect(point.x - 75, Screen.height - point.y, 200, 25), $"{a.label} {a.hp:0}");
            }
            foreach (var d in world.detections)
            {
                Vector3 p = view.WorldToScreenPoint(d.position + Vector3.up * 3);
                if (p.z > 0) GUI.Label(new Rect(Mathf.Clamp(p.x - 60, 10, Screen.width - 180), Mathf.Clamp(Screen.height - p.y, 10, Screen.height - 30), 180, 30), "[ SENSOR CONTACT ]");
            }
            if (Time.unscaledTime < reportUntil)
            {
                string reporter = rules.reporter == ReporterFamily.GrandDracumon ? "Dracmon" : rules.reporter == ReporterFamily.Myotismon ? "DemiDevimon" : "Relay minion (placeholder)";
                GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height - 140, 520, 110), "");
                GUI.DrawTexture(new Rect(Screen.width / 2 - 250, Screen.height - 118, 64, 64), portrait);
                GUI.Label(new Rect(Screen.width / 2 - 177, Screen.height - 130, 425, 95), reporter + " - RADIO REPORT\nGround tremors suggest many\nDigimon are moving.");
            }
            GUI.Label(new Rect(Screen.width / 2 - 6, Screen.height / 2 - 10, 20, 25), "+");
        }
        private void WriteTrace()
        {
            if (string.IsNullOrEmpty(traceFile) || world == null) return;
            System.IO.File.WriteAllText(traceFile, JsonUtility.ToJson(world, true));
        }
        private void CaptureWorld(string path)
        {
            // Hidden/occluded Windows swapchains can return black screenshots. Render the scene explicitly for QA.
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view,
                    new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
        }
        private void ResetSession()
        {
            WriteTrace(); network.Shutdown(); simulation = null; world = null; lastReport = 0; reportUntil = 0;
            foreach (var go in models.Values) Destroy(go); models.Clear();
            queued = new InputCommand(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; status = "Disconnected. Ready for a new session.";
        }
        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (network != null) { network.Shutdown(); Destroy(network.gameObject); }
            foreach (var mat in materials) if (mat != null) Destroy(mat);
            if (reportClip != null) Destroy(reportClip);
            if (portrait != null) Destroy(portrait);
        }
    }
}
