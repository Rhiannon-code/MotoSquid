using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabRiderFit : EditorWindow
    {
        const string BaselinePath = "Assets/MotoSquid/Data/RigLab/Data/RiderFitBaseline.json";
        const string SettingsPath = "Assets/MotoSquid/Data/RigLab/Data/RiderFitSettings.json";

        [System.Serializable] class Entry { public string bike; public string target; public Vector3 pos; public Vector3 euler; }
        [System.Serializable] class Baseline { public List<Entry> entries = new List<Entry>(); }

        [System.Serializable]
        class Fit { public string bike; public float forward; public float rise; public float tuck; public float foot = 1f; public bool custom; }

        static string Key(BikeController bike, BikeAnimationController rider)
        {
            return bike.name + " / " + (rider != null ? rider.gameObject.name : "?");
        }

        [System.Serializable]
        class Settings
        {
            public float forward = 0.03f;
            public float rise = 0f;
            public float tuck = 6f;
            public bool includeAirTarget;
            public List<Fit> perBike = new List<Fit>();
        }

        static readonly string[] HipFields =
            { "hipIdleTarget", "hipNormalSpeedTarget", "hipHighSpeedTarget", "hipReverseTarget" };
        static readonly string[] SpineFields =
            { "spineIdleTarget", "spineNormalSpeedTarget", "spineHighSpeedTarget", "spineReverseTarget" };

        Settings settings;
        Vector2 scroll;
        int grown;
        float footRotation = 1f;
        readonly HashSet<string> warnedMissing = new HashSet<string>();
        float comfort = 0.90f;

        [MenuItem("Tools/Rig Lab/9. Rider Fit", false, 80)]
        static void Open()
        {
            GetWindow<RigLabRiderFit>("Rider Fit").minSize = new Vector2(430, 380);
        }

        void OnEnable()
        {
            settings = File.Exists(SettingsPath)
                ? JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath))
                : new Settings();
        }

        void OnGUI()
        {
            if (settings == null) settings = new Settings();

            EditorGUILayout.HelpBox(
                "Applied from a stored baseline every time, values are absolute, not cumulative. " +
                "Per bike rows override the global for that bike only.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
            settings.forward = EditorGUILayout.FloatField("Hip forward (m)", settings.forward);
            settings.rise = EditorGUILayout.FloatField("Hip rise (m)", settings.rise);
            settings.tuck = EditorGUILayout.FloatField("Extra spine tuck (deg)", settings.tuck);
            settings.includeAirTarget = EditorGUILayout.Toggle("Include air target", settings.includeAirTarget);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Per rider on each bike", EditorStyles.boldLabel);

            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .OrderBy(b => b.transform.position.x).ToArray();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var bike in bikes)
            foreach (var rider in RigLabRiders.AllOn(bike))
            {
                var fit = Lookup(Key(bike, rider));
                EditorGUILayout.BeginHorizontal();

                bool custom = EditorGUILayout.ToggleLeft(fit.bike, fit.custom, GUILayout.Width(210));
                if (custom != fit.custom)
                {
                    fit.custom = custom;
                    if (custom) { fit.forward = settings.forward; fit.rise = settings.rise; fit.tuck = settings.tuck; fit.foot = footRotation; }
                }

                using (new EditorGUI.DisabledScope(!fit.custom))
                {
                    fit.forward = EditorGUILayout.FloatField(fit.custom ? fit.forward : settings.forward, GUILayout.Width(58));
                    fit.rise = EditorGUILayout.FloatField(fit.custom ? fit.rise : settings.rise, GUILayout.Width(58));
                    fit.tuck = EditorGUILayout.FloatField(fit.custom ? fit.tuck : settings.tuck, GUILayout.Width(58));
                    fit.foot = Mathf.Clamp01(EditorGUILayout.FloatField(fit.custom ? fit.foot : footRotation, GUILayout.Width(44)));
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("                                        forward   rise    tuck  follow", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            bool haveBaseline = File.Exists(BaselinePath);
            EditorGUILayout.LabelField("Baseline", haveBaseline ? "captured" : "not captured yet");

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Play mode: Apply updates the live pose so you can judge it immediately. " +
                    "Settings are saved, but scene changes are not, re-apply once in edit mode to bake.", MessageType.Info);

            if (GUILayout.Button(haveBaseline ? "Apply" : "Capture baseline and apply")) Apply();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feet", EditorStyles.boldLabel);
            footRotation = EditorGUILayout.Slider("Follow target angle (global)", footRotation, 0f, 1f);
            EditorGUILayout.HelpBox(
                "The vendor ships every IK with target rotation OFF, so a foot's angle falls out of " +
                "however the leg solved, which differs per character. At 1 the foot takes its " +
                "target's rotation exactly, so every character sits the same way on the peg and you " +
                "tune the ANGLE on that rider's own leg target, this slider is the mode, not the " +
                "angle. Feet only, hands are left alone.\nTick a rider above to give it its own " +
                "value in the 'follow' column.",
                MessageType.None);
            if (GUILayout.Button("Apply foot angle to every rider")) ApplyFeet();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Auto fit", EditorStyles.boldLabel);
            comfort = EditorGUILayout.Slider("Elbow bend (reach used)", comfort, 0.80f, 1.00f);
            EditorGUILayout.HelpBox(
                "Measures each rider's shoulder against that bike's bar target and writes the hip " +
                "forward/rise needed to leave the arm at the reach fraction above. Below 1 leaves a " +
                "real elbow bend, which is what an IK hint needs to have any leverage.\n" +
                "Needs Play, because the rig does not pose the rider in edit mode. Iterative, " +
                "Auto fit, Apply, repeat until the deficits read ~0.", MessageType.None);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Measure only (changes nothing)")) AutoFit(false);
                if (GUILayout.Button("Auto-fit every rider from the live pose")) AutoFit(true);
            }

            using (new EditorGUI.DisabledScope(!haveBaseline))
            {
                if (GUILayout.Button("Restore baseline (zero every fit)"))
                {
                    settings = new Settings { forward = 0f, rise = 0f, tuck = 0f };
                    Apply();
                }
                if (GUILayout.Button("Forget baseline"))
                {
                    AssetDatabase.DeleteAsset(BaselinePath);
                    Debug.Log("Rig Lab: baseline forgotten, the next Apply recaptures from current values.");
                }
            }
        }

        const float MaxForward = 0.25f, MinForward = -0.15f, MaxRise = 0.20f;

        void AutoFit(bool write)
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder(
                write ? "Rig Lab auto fit\n" : "Rig Lab reach measurement, nothing changed\n");
            int done = 0;

            int asleep = 0;
            foreach (var bike in bikes)
            {
                var ctrl = RigLabRiders.ActiveOn(bike);

                if (ctrl == null || !ctrl.gameObject.activeInHierarchy) { asleep++; continue; }
                var refs = ctrl != null ? ctrl.GetComponentInChildren<BikerRigReferences>(true) : null;
                var anim = ctrl != null ? ctrl.GetComponent<Animator>() : null;
                var targets = bike.bikeReferences.bikeAnimationTargets;
                if (anim == null || !anim.isHuman || refs == null || targets == null) continue;

                Transform space = HipSpace(targets);
                if (space == null) continue;

                float worst = 0f; Vector3 fix = Vector3.zero;
                Reach(anim, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                      refs.leftHandTarget, ref worst, ref fix);
                Reach(anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                      refs.rightHandTarget, ref worst, ref fix);

                var local = space.InverseTransformVector(fix);
                var fit = Lookup(Key(bike, ctrl));

                float wantF = fit.forward + local.z, wantR = fit.rise + local.y;
                float gotF = Mathf.Clamp(wantF, MinForward, MaxForward);
                float gotR = Mathf.Clamp(wantR, -MaxRise, MaxRise);
                bool clamped = !Mathf.Approximately(wantF, gotF) || !Mathf.Approximately(wantR, gotR);

                if (write) { fit.custom = true; fit.forward = gotF; fit.rise = gotR; }

                sb.Append(string.Format("   {0,-30} {1} over-reach {2,6:0.000} m  ->  forward {3,6:0.000}   rise {4,6:0.000}{5}\n",
                                        bike.name, anim.gameObject.name.PadRight(24), worst, gotF, gotR,
                                        clamped ? "   CLAMPED - raise Elbow bend for this bike instead" : ""));

                Foot(sb, anim, HumanBodyBones.LeftFoot, refs.leftLegTarget, "L foot", refs.LeftLegRig,
                     ctrl.leftlegInMotionTarget);
                Foot(sb, anim, HumanBodyBones.RightFoot, refs.rightLegTarget, "R foot", refs.RightLegRig,
                     ctrl.rightlegInMotionTarget);
                done++;
            }

            if (write)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
            }
            sb.Append("\nrig state, every rider (a rig that never built drives nothing):\n");
            foreach (var bike in bikes)
                foreach (var rider in RigLabRiders.AllOn(bike))
                {
                    var rb = rider.GetComponentInChildren<RigBuilder>(true);
                    var rigs = rider.GetComponentsInChildren<Rig>(true);
                    var rr = rider.GetComponentInChildren<BikerRigReferences>(true);

                    string built = rb == null ? "NO RigBuilder"
                                 : !rb.enabled ? "RigBuilder DISABLED"
                                 : rb.layers.Count == 0 ? "0 layers"
                                 : !rb.graph.IsValid() ? "GRAPH NOT BUILT"
                                 : rb.layers.Count + " layers ok";

                    sb.Append(string.Format("   {0,-28} {1,-24} {2,-18} rig w {3}   legIK w {4}\n",
                        bike.name, rider.gameObject.name,
                        (rider.gameObject.activeInHierarchy ? "active" : "INACTIVE") + " / " + built,
                        rigs.Length > 0 ? rigs[0].weight.ToString("0.00") : ",",
                        rr != null && rr.LeftLegRig != null ? rr.LeftLegRig.weight.ToString("0.00") : ","));

                    if (rider.gameObject.activeInHierarchy)
                    {
                        sb.Append("        L InMotion -> ").Append(Owner(rider.leftlegInMotionTarget)).Append('\n');
                        sb.Append("        R InMotion -> ").Append(Owner(rider.rightlegInMotionTarget)).Append('\n');
                        Leg(sb, "L", rider.leftLegTargetRig, rr != null ? rr.leftLegTarget : null,
                            rr != null && rr.LeftLegRig != null ? rr.LeftLegRig.data.target : null);
                        Leg(sb, "R", rider.rightLegTargetRig, rr != null ? rr.rightLegTarget : null,
                            rr != null && rr.RightLegRig != null ? rr.RightLegRig.data.target : null);
                        Writers(sb, rider, rr != null ? rr.leftLegTarget : null, "L");
                        Writers(sb, rider, rr != null ? rr.rightLegTarget : null, "R");
                        Frozen(sb, "L leg target", rr != null ? rr.leftLegTarget : null);
                        Frozen(sb, "R leg target", rr != null ? rr.rightLegTarget : null);
                        Duplicates(sb, rider);
                        WhoWrites(sb, rider.leftlegInMotionTarget, "L InMotion");
                        WhoWrites(sb, rider.rightlegInMotionTarget, "R InMotion");
                    }
                }

            if (asleep > 0)
                sb.Append("\n   ").Append(asleep).Append(" bike(s) skipped , inactive, so their riders are in bind pose.\n")
                  .Append("   Untick 'Solo active bike' on Rig Review to measure them all at once,\n")
                  .Append("   and use , / . to measure the other characters.\n");

            sb.Append("\n").Append(done).Append(write
                ? " rider(s) fitted. Press Apply to see it, then Auto fit again if any over reach remains.\nExit Play and Apply once in edit mode to bake it into the scene."
                : " rider(s) measured. Nothing was written.");
            Debug.Log(sb.ToString());
            Repaint();
        }

        static void Frozen(System.Text.StringBuilder sb, string label, Transform t)
        {
            if (t == null) return;

            bool anyStatic = false;
            for (var p = t; p != null; p = p.parent)
                if (p.gameObject.isStatic)
                {
                    sb.Append("             ").Append(label).Append(" FROZEN: '").Append(p.name)
                      .Append("' is marked static").Append(p == t ? "" : " (a parent)").Append('\n');
                    anyStatic = true;
                }

            sb.Append("             ").Append(label).Append("  static ").Append(t.gameObject.isStatic)
              .Append("   hideFlags ").Append(t.hideFlags)
              .Append("   world ").Append(t.position.ToString("F3"))
              .Append(anyStatic ? "   <-- writes to this are dropped" : "").Append('\n');
        }

        static void Check(System.Text.StringBuilder sb, Dictionary<Transform, string> seen, Transform t, string name)
        {
            if (t == null) return;
            string first;
            if (seen.TryGetValue(t, out first))
                sb.Append("             SHARED TRANSFORM: ").Append(first).Append(" and ").Append(name)
                  .Append(" are both '").Append(t.name).Append("'\n");
            else seen[t] = name;
        }

        static void WhoWrites(System.Text.StringBuilder sb, Transform pose, string label)
        {
            if (pose == null) return;

            foreach (var c in Object.FindObjectsByType<BikeAnimationController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var f in typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (f.FieldType != typeof(Transform)) continue;
                    if (f.GetValue(c) as Transform != pose) continue;

                    bool driven = f.Name.EndsWith("ReverseTarget");
                    sb.Append("             ").Append(label).Append(" referenced by ")
                      .Append(c.gameObject.name).Append('.').Append(f.Name)
                      .Append(c.gameObject.activeInHierarchy ? " [ACTIVE]" : " [inactive]")
                      .Append(driven ? "   <-- WRITTEN EVERY FRAME by ReverseLegAnimation" : "")
                      .Append('\n');
                }
        }

        static void Duplicates(System.Text.StringBuilder sb, BikeAnimationController rider)
        {
            var seen = new Dictionary<Transform, string>();

            foreach (var f in typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(Transform) || f.Name.EndsWith("Rig")) continue;
                Check(sb, seen, f.GetValue(rider) as Transform, "controller." + f.Name);
            }

            var rr = rider.GetComponentInChildren<BikerRigReferences>(true);
            if (rr != null)
                foreach (var f in typeof(BikerRigReferences).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (f.FieldType != typeof(Transform)) continue;
                    Check(sb, seen, f.GetValue(rr) as Transform, "rigRefs." + f.Name);
                }

            foreach (var f in typeof(BikeAnimationController).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(Transform) || !f.Name.EndsWith("Rig")) continue;
                Check(sb, seen, f.GetValue(rider) as Transform, "controller." + f.Name);
            }
        }

        static void Writers(System.Text.StringBuilder sb, BikeAnimationController rider, Transform leg, string side)
        {
            if (leg == null) return;

            var chain = new HashSet<Transform>();
            for (var t = leg; t != null && t != rider.transform; t = t.parent) chain.Add(t);

            foreach (var c in rider.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var rc = c as IRigConstraint;
                if (rc == null) continue;
                var data = rc.data;
                foreach (var f in data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(Transform)) continue;
                    var t = f.GetValue(data) as Transform;
                    if (t == null || !chain.Contains(t)) continue;
                    sb.Append("             ").Append(side).Append(" written by ")
                      .Append(c.GetType().Name).Append(" on ").Append(c.gameObject.name)
                      .Append("  (").Append(f.Name).Append(" -> ").Append(t.name)
                      .Append(", weight ").Append(rc.weight.ToString("0.00")).Append(")\n");
                }
            }
        }

        static void Leg(System.Text.StringBuilder sb, string side, Transform moved, Transform named, Transform solved)
        {
            bool agree = moved != null && moved == named && named == solved;
            sb.Append("        ").Append(side).Append(agree ? " leg targets AGREE  " : " leg targets DISAGREE  ")
              .Append(Owner(moved)).Append('\n');
            if (agree) return;
            sb.Append("             controller moves : ").Append(Owner(moved)).Append('\n');
            sb.Append("             references name  : ").Append(Owner(named)).Append('\n');
            sb.Append("             IK solves to     : ").Append(Owner(solved)).Append('\n');
        }

        static string Owner(Transform t)
        {
            if (t == null) return "(null)";
            var parts = new List<string>();
            for (var p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            int from = System.Math.Max(0, parts.Count - 4);
            return string.Join("/", parts.Skip(from)) +
                   (t.gameObject.activeInHierarchy ? "" : "   [INACTIVE]");
        }

        static void Foot(System.Text.StringBuilder sb, Animator anim, HumanBodyBones bone,
                         Transform target, string label, TwoBoneIKConstraint ik, Transform pose)
        {
            var f = anim.GetBoneTransform(bone);
            if (f == null || target == null) return;

            Vector3 off = target.InverseTransformPoint(f.position);
            float gap = off.magnitude;

            string reach = "";
            var hip = anim.GetBoneTransform(bone == HumanBodyBones.LeftFoot
                                            ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var knee = anim.GetBoneTransform(bone == HumanBodyBones.LeftFoot
                                             ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            if (hip != null && knee != null)
            {
                float leg = Vector3.Distance(hip.position, knee.position) + Vector3.Distance(knee.position, f.position);
                float want = Vector3.Distance(hip.position, target.position);
                reach = string.Format("   leg {0,5:0.000} needs {1,5:0.000}", leg, want);
                if (want > leg) reach += string.Format("   CANNOT REACH by {0:0.000} m", want - leg);
            }

            string rot = ik != null && ik.data.targetRotationWeight > 0f
                ? string.Format("   rotation {0,5:0.0} deg", Quaternion.Angle(f.rotation, target.rotation))
                : "   (target rotation unused)";

            sb.Append(string.Format("      {0}  off by {1,6:0.000} m  (side {2,8:0.000;-0.000; 0.000}  up {3,8:0.000;-0.000; 0.000}  fwd {4,8:0.000;-0.000; 0.000}){5}{6}\n",
                                    label, gap, off.x, off.y, off.z, rot + reach,
                                    gap > 0.03f ? "   <-- drag this rider's leg target" : ""));

            if (pose != null)
            {
                float lag = Vector3.Distance(target.position, pose.position);
                sb.Append(string.Format("              rig target is {0,6:0.000} m from the pose target{1}\n",
                                        lag, lag > 0.02f
                                            ? "   <-- controller has not converged (is Play running?)"
                                            : "   ok"));
            }
        }

        void Reach(Animator anim, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand,
                   Transform target, ref float worst, ref Vector3 fix)
        {
            if (target == null) return;
            Transform a = anim.GetBoneTransform(upper), b = anim.GetBoneTransform(lower), c = anim.GetBoneTransform(hand);
            if (a == null || b == null || c == null) return;

            float arm = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
            Vector3 toBar = target.position - a.position;
            float over = toBar.magnitude - arm * comfort;
            if (over <= worst) return;

            worst = over;
            fix = toBar.normalized * over;
        }

        static Transform HipSpace(BikeAnimationTargets targets)
        {
            foreach (var name in HipFields)
            {
                var f = typeof(BikeAnimationTargets).GetField(name);
                var tr = f != null ? f.GetValue(targets) as Transform : null;
                if (tr != null && tr.parent != null) return tr.parent;
            }
            return null;
        }

        void ApplyFeet()
        {
            int touched = 0;
            foreach (var bike in Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var rider in RigLabRiders.AllOn(bike))
                {
                    var refs = rider.GetComponentInChildren<BikerRigReferences>(true);
                    if (refs == null) continue;
                    var fit = Lookup(Key(bike, rider));
                    float w = fit.custom ? Mathf.Clamp01(fit.foot) : footRotation;

                    foreach (var ik in new[] { refs.LeftLegRig, refs.RightLegRig })
                    {
                        if (ik == null) continue;
                        Undo.RecordObject(ik, "Foot angle");
                        ik.data.targetRotationWeight = w;
                        EditorUtility.SetDirty(ik);
                        touched++;
                    }
                }

            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
            Debug.Log("Rig Lab: foot follow applied to " + touched + " leg constraint(s), global " +
                      footRotation.ToString("0.00") + " with per rider overrides.\n" +
                      (Application.isPlaying ? "Play mode: re-apply in edit mode to bake it." :
                       "Tune the angle on each rider's own leg target; the others will not move."));
        }

        Fit Lookup(string key)
        {
            var fit = settings.perBike.FirstOrDefault(f => f.bike == key);
            if (fit == null)
            {
                fit = new Fit { bike = key, forward = settings.forward, rise = settings.rise, tuck = settings.tuck };
                settings.perBike.Add(fit);
            }
            return fit;
        }

        void Apply()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var baseline = File.Exists(BaselinePath)
                ? JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath))
                : null;
            bool capturing = baseline == null;
            if (capturing && Application.isPlaying)
            {
                Debug.LogError("Rig Lab: capture the baseline in edit mode - in Play the targets are already moved.");
                return;
            }
            if (capturing) baseline = new Baseline();

            var lookup = new Dictionary<string, Entry>();
            foreach (var e in baseline.entries) lookup[e.bike + "/" + e.target] = e;

            int touched = 0;
            grown = 0;
            warnedMissing.Clear();
            foreach (var bike in bikes)
                foreach (var rider in RigLabRiders.AllOn(bike))
                {
                    var fit = Lookup(Key(bike, rider));
                    float forward = fit.custom ? fit.forward : settings.forward;
                    float rise = fit.custom ? fit.rise : settings.rise;
                    float tuck = fit.custom ? fit.tuck : settings.tuck;

                    foreach (var f in HipFields) touched += Nudge(bike, rider, f, lookup, baseline, capturing, true, forward, rise, tuck);
                    if (settings.includeAirTarget) touched += Nudge(bike, rider, "hipInAirTarget", lookup, baseline, capturing, true, forward, rise, tuck);
                    foreach (var f in SpineFields) touched += Nudge(bike, rider, f, lookup, baseline, capturing, false, forward, rise, tuck);
                }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
            if (capturing || grown > 0) File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));

            if (!Application.isPlaying)
            {
                AssetDatabase.Refresh();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log("Rig Lab: rider fit applied to " + touched + " target(s) across " + bikes.Length +
                      " bike(s)" + (capturing ? "   (baseline captured)" : "") +
                      (grown > 0 ? "   (+" + grown + " new baseline entries)" : "") +
                      (Application.isPlaying ? "   [Play: settings saved, re-apply in edit mode to bake]" : ""));
        }

        int Nudge(BikeController bike, BikeAnimationController rider, string field,
                  Dictionary<string, Entry> lookup, Baseline baseline, bool capturing, bool isHip,
                  float forward, float rise, float tuck)
        {
            var f = typeof(BikeAnimationController).GetField(field);
            if (f == null || rider == null) return 0;
            var tr = f.GetValue(rider) as Transform;
            if (tr == null) return 0;

            string key = Key(bike, rider) + "/" + field;
            Entry entry;
            if (!lookup.TryGetValue(key, out entry))
            {
                if (Application.isPlaying)
                {
                    if (!warnedMissing.Contains(Key(bike, rider)))
                    {
                        warnedMissing.Add(Key(bike, rider));
                        Debug.LogWarning("Rig Lab: no baseline for " + Key(bike, rider) +
                                         " , leave Play and Apply once to capture it.");
                    }
                    return 0;
                }
                entry = new Entry { bike = Key(bike, rider), target = field, pos = tr.localPosition, euler = tr.localEulerAngles };
                baseline.entries.Add(entry);
                lookup[key] = entry;
                grown++;
            }

            Undo.RecordObject(tr, "Rider fit");
            if (isHip) tr.localPosition = entry.pos + new Vector3(0f, rise, forward);
            else tr.localEulerAngles = entry.euler + new Vector3(tuck, 0f, 0f);
            return 1;
        }
    }
}
