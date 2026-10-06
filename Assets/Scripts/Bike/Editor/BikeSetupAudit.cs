using MotoSquid.Audio;
using MotoSquid.Combat;
using MotoSquid.Race;
using MotoSquid.Rider;
using MotoSquid.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Bike
{
    public class BikeSetupAudit : EditorWindow
    {
        const string PrefabDir   = BikeRoster.PrefabDir;
        const string PlayerDonor = PrefabDir + "/Player1.prefab";
        const string AIDonor     = PrefabDir + "/Voodoo.prefab";
        static readonly string[] Anchors = { "SeatAnchor", "GripAnchor_L", "GripAnchor_R", "FootPegGrip_L", "FootPegGrip_R" };

        enum Level { Ok, Warn, Fail }

        class Check
        {
            public string Label;
            public Level  Level;
            public string Detail;
            public Check(string label, Level level, string detail = null) { Label = label; Level = level; Detail = detail; }
        }

        class Report
        {
            public string Name;
            public string Path;
            public bool   Missing;
            public readonly List<Check> Audio     = new List<Check>();
            public readonly List<Check> Animation = new List<Check>();
            public readonly List<Check> ArtRig    = new List<Check>();

            public IEnumerable<Check> All => Audio.Concat(Animation).Concat(ArtRig);
            public int Fails => All.Count(c => c.Level == Level.Fail);
            public int Warns => All.Count(c => c.Level == Level.Warn);
        }

        List<Report>  _reports;
        List<Check>   _scene;
        string        _sceneName = "(none audited)";
        Vector2       _scroll;
        readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>();

        [MenuItem("MotoSquid/Bike Setup Audit")]
        static void Open()
        {
            var w = GetWindow<BikeSetupAudit>("Bike Setup");
            w.minSize = new Vector2(460, 400);
            w.Audit();
        }

        void Audit()
        {
            _reports = new List<Report>();
            foreach (var (rel, isPlayer) in BikeRoster.All) _reports.Add(AuditPrefab(BikeRoster.PathOf(rel), isPlayer));
            AuditScene();
            Repaint();
        }

        Report AuditPrefab(string path, bool isPlayer)
        {
            var r = new Report { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path };
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { r.Missing = true; return r; }

            AuditAudio(root, r, isPlayer);
            AuditAnimation(root, r);
            AuditArtRig(root, r);
            return r;
        }

        void AuditAudio(GameObject root, Report r, bool isPlayer)
        {
            var hub = root.GetComponentInChildren<BikeAudioController>(true);
            if (hub == null)
            {
                r.Audio.Add(new Check("BikeAudioController", Level.Fail, "absent, bike is silent"));
                return;
            }
            r.Audio.Add(new Check("BikeAudioController", Level.Ok));

            var engine = root.GetComponentInChildren<EngineAudio>(true);
            if (engine == null)
                r.Audio.Add(new Check("EngineAudio", Level.Fail, "absent, no engine note"));
            else
            {
                r.Audio.Add(Pair("Engine loops (on/off)", engine.onClip, engine.offClip));
                r.Audio.Add(Pair("Engine helmet loops", engine.onHelmetClip, engine.offHelmetClip, Level.Warn));
                r.Audio.Add(new Check("Engine mixer group", engine.output != null ? Level.Ok : Level.Warn,
                                      engine.output != null ? engine.output.name : "unrouted"));
                r.Audio.Add(new Check("EngineAudio.isLocalPlayer",
                                      engine.isLocalPlayer == isPlayer ? Level.Ok : Level.Fail,
                                      $"is {engine.isLocalPlayer}, expected {isPlayer}"));
            }

            r.Audio.Add(Source("Wind source",      hub.windSource));
            r.Audio.Add(Source("Skid source",      hub.skidSound));
            r.Audio.Add(Source("Gear shift source", hub.gearShiftSound));
            r.Audio.Add(Source("Helmet source",    hub.helmetSource));
            r.Audio.Add(new Check("Landing thud", hub.landingThud != null ? Level.Ok : Level.Warn));
            r.Audio.Add(Pair("Skid surface clips (road/off road)", hub.skidRoadClip, hub.skidOffRoadClip, Level.Warn));

            bool combat = Filled(hub.swingWhooshClips) && Filled(hub.hitImpactClips) && hub.knockOffClip != null;
            r.Audio.Add(new Check("Combat clips (whoosh/impact/knock off)", combat ? Level.Ok : Level.Warn,
                                  combat ? null : $"whoosh {Count(hub.swingWhooshClips)}, impact {Count(hub.hitImpactClips)}, knock off {(hub.knockOffClip != null ? "set" : "none")}, CombatSystem already calls these"));

            r.Audio.Add(new Check("Hub mixer group", hub.engineOutput != null ? Level.Ok : Level.Warn,
                                  hub.engineOutput != null ? hub.engineOutput.name : "unrouted"));
            r.Audio.Add(new Check("Hub isLocalPlayer", hub.isLocalPlayer == isPlayer ? Level.Ok : Level.Fail,
                                  $"is {hub.isLocalPlayer}, expected {isPlayer}"));
        }

        void AuditAnimation(GameObject root, Report r)
        {
            var animators = root.GetComponentsInChildren<Animator>(true);
            int withController = animators.Count(a => a.runtimeAnimatorController != null);
            r.Animation.Add(new Check("Animators with a controller",
                                      withController >= 2 ? Level.Ok : (withController == 1 ? Level.Warn : Level.Fail),
                                      $"{withController} of {animators.Length} (rider + bike art = 2)"));

            var withAvatar = animators.FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
            r.Animation.Add(new Check("Humanoid avatar on the rider", withAvatar != null ? Level.Ok : Level.Fail,
                                      withAvatar != null ? withAvatar.avatar.name : "no humanoid Animator, retarget will not run"));

            int drivers = root.GetComponentsInChildren<BikeRiderAnimator>(true).Length;
            r.Animation.Add(new Check("BikeRiderAnimator", drivers > 0 ? Level.Ok : Level.Fail, $"{drivers} instance(s)"));

            r.Animation.Add(new Check("AnimatedRootPin",
                                      root.GetComponentInChildren<AnimatedRootPin>(true) != null ? Level.Ok : Level.Warn));

            bool rigBuilder = root.GetComponentInChildren<RigBuilder>(true) != null;
            int  ik         = root.GetComponentsInChildren<TwoBoneIKConstraint>(true).Length;
            r.Animation.Add(new Check("RigBuilder", rigBuilder ? Level.Ok : Level.Fail,
                                      rigBuilder ? null : "no Animation Rigging, hands/feet will not reach the bike"));
            r.Animation.Add(new Check("Two Bone IK constraints", ik >= 4 ? Level.Ok : Level.Fail,
                                      $"{ik} (expected 4: two hands, two feet)"));

            int rigRefs  = root.GetComponentsInChildren<BikerRigReferences>(true).Length;
            int animCtrl = root.GetComponentsInChildren<BikeAnimationController>(true).Length;
            int targets  = root.GetComponentsInChildren<BikeAnimationTargets>(true).Length;
            r.Animation.Add(new Check("BikerRigReferences leftover", rigRefs == 0 ? Level.Ok : Level.Fail,
                                      rigRefs == 0 ? null : $"{rigRefs}, silently reroutes combat down the dead procedural path"));
            r.Animation.Add(new Check("BikeAnimationController leftover", animCtrl == 0 ? Level.Ok : Level.Fail, animCtrl == 0 ? null : $"{animCtrl}"));
            r.Animation.Add(new Check("BikeAnimationTargets leftover", targets == 0 ? Level.Ok : Level.Warn, targets == 0 ? null : $"{targets} (inert, but sweep it)"));
        }

        void AuditArtRig(GameObject root, Report r)
        {
            var names = new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t => t.name));

            bool hasRider = root.GetComponentInChildren<BikeRiderAnimator>(true) != null;
            var missing = Anchors.Where(a => !names.Contains(a)).ToArray();
            r.ArtRig.Add(new Check("Rider anchors",
                                   missing.Length == 0 ? Level.Ok : (hasRider ? Level.Fail : Level.Warn),
                                   missing.Length == 0 ? "all five present"
                                   : hasRider ? "missing " + string.Join(", ", missing)
                                   : "not a built racer, anchors live on the bike art prefab you feed the builder"));

            bool front = names.Contains("Front_Wheel");
            bool back  = names.Contains("Back_Wheel") || names.Contains("Rear_Wheel");
            r.ArtRig.Add(new Check("Wheel transforms", front && back ? Level.Ok : Level.Warn,
                                   front && back ? null : $"Front_Wheel {(front ? "ok" : "missing")}, Back_Wheel/Rear_Wheel {(back ? "ok" : "missing")}"));

            bool canDriveFX = root.GetComponentInChildren<BikeController>(true) != null;
            r.ArtRig.Add(FxCheck("LandingImpactFX", root.GetComponentInChildren<LandingImpactFX>(true) != null, canDriveFX));
            r.ArtRig.Add(FxCheck("SurfaceAudio",    root.GetComponentInChildren<SurfaceAudio>(true) != null, canDriveFX));
        }

        static Check FxCheck(string label, bool present, bool canDrive)
        {
            if (!canDrive) return new Check(label, Level.Ok, "n/a needs BikeController (player bikes only)");
            return new Check(label, present ? Level.Ok : Level.Warn);
        }

        void AuditScene()
        {
            _scene = new List<Check>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            _sceneName = scene.name;

            _scene.Add(Singleton<AccessibilityManager>("AccessibilityManager", Level.Fail,
                                                                "absent, rumble gate and every accessibility toggle are inert"));
            _scene.Add(Singleton<HitStopController>("HitStopController", Level.Warn, "absent, no impact freeze"));
            _scene.Add(Singleton<AmbienceController>("AmbienceController", Level.Warn, "absent, no ambience bed"));
            _scene.Add(Singleton<SkidmarkController>("SkidmarkController", Level.Warn, "absent, no skidmarks"));

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _scene.Add(new Check("AudioListeners", listeners.Length == 1 ? Level.Ok : Level.Fail,
                                 $"{listeners.Length} active (Unity uses one and warns about the rest)"));

            var race = Object.FindFirstObjectByType<RaceManager>();
            if (race == null)
                _scene.Add(new Check("RaceManager", Level.Warn, "none in this scene"));
            else
            {
                _scene.Add(new Check("RaceManager.playerTransform", race.playerTransform != null ? Level.Ok : Level.Fail,
                                     race.playerTransform != null ? race.playerTransform.name : "unassigned"));
                if (race.aiPrefab == null)
                    _scene.Add(new Check("RaceManager.aiPrefab", Level.Warn, "unassigned"));
                else
                {
                    bool aiHub = race.aiPrefab.GetComponentInChildren<BikeAudioController>(true) != null;
                    bool aiRig = race.aiPrefab.GetComponentInChildren<BikeRiderAnimator>(true) != null;
                    _scene.Add(new Check("RaceManager.aiPrefab", aiHub && aiRig ? Level.Ok : Level.Warn,
                                         $"{race.aiPrefab.name}" + (aiHub ? "" : ", no audio hub")
                                                                 + (aiRig ? "" : ", no BikeRiderAnimator")
                                                                 + " (only matters if it actually spawns, see Grid below)"));
                }

                bool hasSceneBikes = race.aiOpponents != null && race.aiOpponents.Length > 0 && race.aiOpponents[0] != null;
                int  sceneAI = Object.FindObjectsByType<BikeAIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                bool willSpawn = !hasSceneBikes && race.aiPrefab != null && race.aiCount > 0;

                string grid = willSpawn
                    ? $"spawns {race.aiCount} x {race.aiPrefab.name}, plus {sceneAI} scene AI auto registered"
                    : $"races the {sceneAI} AI bike(s) already in the scene (aiOpponents has {(race.aiOpponents == null ? 0 : race.aiOpponents.Length)} entr(y/ies), prefab not spawned)";

                bool gridBad = willSpawn && race.aiPrefab.GetComponentInChildren<BikeAudioController>(true) == null;
                _scene.Add(new Check("Grid at race start", gridBad ? Level.Fail : Level.Ok,
                                     grid + (gridBad ? ", SPAWNED OPPONENTS WILL BE SILENT" : "")));

                _scene.Add(new Check("Split screen / Time Trial note", Level.Ok,
                                     "RaceManager disables all live AI in split screen and Time Trial, so this grid applies to single-player Race only"));
            }

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(mb is IBikeAudioState)) continue;
                var go   = RootOf(mb.gameObject);
                bool on  = go.activeInHierarchy;
                bool hub = go.GetComponentInChildren<BikeAudioController>(true) != null;
                string detail = (on ? "enabled" : "DISABLED in scene") + (hub ? "" : ", NO BikeAudioController, silent");
                _scene.Add(new Check($"Instance: {go.name}", !on || !hub ? Level.Warn : Level.Ok, detail));
            }
        }

        static GameObject RootOf(GameObject go)
        {
            var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return outer != null ? outer : go.transform.root.gameObject;
        }

        Check Singleton<T>(string label, Level whenMissing, string detail) where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) return new Check(label, whenMissing, detail);
            if (found.Length > 1)  return new Check(label, Level.Fail, $"{found.Length} in the scene, singleton expects one");
            return new Check(label, Level.Ok);
        }

        static Check Pair(string label, Object a, Object b, Level whenMissing = Level.Fail)
        {
            if (a != null && b != null) return new Check(label, Level.Ok);
            if (a == null && b == null) return new Check(label, whenMissing, "both unassigned");
            return new Check(label, whenMissing, "only one of the pair assigned");
        }

        static Check Source(string label, AudioSource s)
        {
            if (s == null)      return new Check(label, Level.Fail, "no AudioSource assigned");
            if (s.clip == null) return new Check(label, Level.Warn, "source assigned but no clip");
            return new Check(label, Level.Ok, s.clip.name);
        }

        static bool Filled(AudioClip[] a) => a != null && a.Length > 0 && a.Any(c => c != null);
        static int  Count(AudioClip[] a)  => a == null ? 0 : a.Count(c => c != null);

        static void AddPlayFeelToDonors()
        {
            int changed = 0;
            var log = new StringBuilder();

            foreach (var path in new[] { PlayerDonor })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { log.AppendLine($"x {path}: not found"); continue; }

                try
                {
                    var bike = root.GetComponentInChildren<BikeController>(true);
                    var hub  = root.GetComponentInChildren<BikeAudioController>(true);
                    bool touched = false;

                    var landing = root.GetComponentInChildren<LandingImpactFX>(true);
                    if (landing == null)
                    {
                        landing = root.AddComponent<LandingImpactFX>();
                        landing.bikeController = bike;
                        touched = true;
                        log.AppendLine($"  + LandingImpactFX (assign landingDust + cameraDipTarget by hand)");
                    }

                    var surface = root.GetComponentInChildren<SurfaceAudio>(true);
                    if (surface == null)
                    {
                        surface = root.AddComponent<SurfaceAudio>();
                        surface.bikeController = bike;
                        if (hub != null) { surface.roadTag = hub.skidRoadTag; surface.roadLayers = hub.skidRoadMask; }
                        touched = true;
                        log.AppendLine($"  + SurfaceAudio (assign roadDust + offRoadDust by hand)");
                    }

                    if (touched)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                        log.Insert(0, $"{System.IO.Path.GetFileNameWithoutExtension(path)}:\n");
                    }
                    else log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: already had both");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Play feel components",
                $"Updated {changed} donor prefab(s).\n\n{log}\n" +
                "Particle systems and the camera dip target still need assigning by hand, the tool " +
                "cannot know where the dust should come from.\n\n" +
                "The AI donor is deliberately skipped: LandingImpactFX and SurfaceAudio both require " +
                "BikeController, which AI bikes do not have.", "OK");
        }

        void OnGUI()
        {
            if (_reports == null) Audit();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Re-audit", EditorStyles.toolbarButton, GUILayout.Width(70))) Audit();
                if (GUILayout.Button("Copy report", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    EditorGUIUtility.systemCopyBuffer = BuildTextReport();
                GUILayout.FlexibleSpace();
                int fails = _reports.Sum(r => r.Fails), warns = _reports.Sum(r => r.Warns);
                GUILayout.Label($"{fails} fail · {warns} warn", EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField($"Open scene, {_sceneName}", EditorStyles.boldLabel);
            foreach (var c in _scene) DrawCheck(c);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Bike prefabs", EditorStyles.boldLabel);
            foreach (var r in _reports)
            {
                if (!_expanded.ContainsKey(r.Name)) _expanded[r.Name] = r.Fails > 0;

                string summary = r.Missing ? "prefab not found"
                               : r.Fails > 0 ? $"{r.Fails} fail, {r.Warns} warn"
                               : r.Warns > 0 ? $"{r.Warns} warn" : "ok";

                GUI.color = r.Missing || r.Fails > 0 ? new Color(1f, 0.5f, 0.5f)
                          : r.Warns > 0 ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 1f, 0.6f);
                _expanded[r.Name] = EditorGUILayout.Foldout(_expanded[r.Name], $"{r.Name} ,  {summary}", true);
                GUI.color = Color.white;

                if (!_expanded[r.Name] || r.Missing) continue;

                EditorGUI.indentLevel++;
                Section("Audio", r.Audio);
                Section("Animation", r.Animation);
                Section("Art / play feel", r.ArtRig);
                if (GUILayout.Button("Select prefab", GUILayout.Width(110)))
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(r.Path);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fixers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each fixer is idempotent. Anchors, humanoid bone mapping, particle systems and clip " +
                "choice stay manual, the audit above tells you which are still outstanding.",
                MessageType.None);

            if (GUILayout.Button("Wire chassis + engine audio on all bikes"))
            {
                EditorApplication.ExecuteMenuItem("MotoSquid/Wire All Bike Audio");
                Audit();
            }
            if (GUILayout.Button("Add LandingImpactFX + SurfaceAudio to the donors"))
            {
                AddPlayFeelToDonors();
                Audit();
            }

            EditorGUILayout.EndScrollView();
        }

        void Section(string title, List<Check> checks)
        {
            if (checks.Count == 0) return;
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            foreach (var c in checks) DrawCheck(c);
        }

        static void DrawCheck(Check c)
        {
            GUI.color = c.Level == Level.Fail ? new Color(1f, 0.5f, 0.5f)
                      : c.Level == Level.Warn ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 1f, 0.6f);
            string mark = c.Level == Level.Fail ? "x" : c.Level == Level.Warn ? "!" : "+";
            EditorGUILayout.LabelField($"{mark} {c.Label}" + (string.IsNullOrEmpty(c.Detail) ? "" : $" ,  {c.Detail}"));
            GUI.color = Color.white;
        }

        string BuildTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MotoSquid bike setup audit, scene '{_sceneName}'");
            sb.AppendLine();
            sb.AppendLine("SCENE");
            foreach (var c in _scene) sb.AppendLine($"  [{c.Level}] {c.Label}{(string.IsNullOrEmpty(c.Detail) ? "" : " , " + c.Detail)}");
            foreach (var r in _reports)
            {
                sb.AppendLine();
                sb.AppendLine(r.Missing ? $"{r.Name}: PREFAB NOT FOUND" : $"{r.Name} ({r.Fails} fail, {r.Warns} warn)");
                if (r.Missing) continue;
                foreach (var c in r.All.Where(c => c.Level != Level.Ok))
                    sb.AppendLine($"  [{c.Level}] {c.Label}{(string.IsNullOrEmpty(c.Detail) ? "" : ", " + c.Detail)}");
            }
            return sb.ToString();
        }
    }
}
