using MotoSquid.Bike;
using MotoSquid.Cameras;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabFitCameras
    {
        static CameraFitTuning Tuning()
        {
            var guid = AssetDatabase.FindAssets("t:CameraFitTuning").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<CameraFitTuning>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [MenuItem("Tools/Rig Lab/48. Fit Camera Mounts (measured)", false, 480)]
        static void Fit()
        {
            var bikes = Targets();
            if (bikes.Count == 0)
            {
                Debug.LogWarning("Rig Lab: no bike to fit. Open a bike prefab, select one, or open a scene with bikes.");
                return;
            }

            var tune = Tuning();
            if (tune == null)
            {
                tune = ScriptableObject.CreateInstance<CameraFitTuning>();
                Debug.LogWarning("Rig Lab: no CameraFitTuning asset, using built in defaults. " +
                                 "Create > Rig Lab > Camera Fit Tuning to tune the framing by eye.");
            }

            var log = new System.Text.StringBuilder();
            int done = 0;
            foreach (var bike in bikes) if (FitOne(bike, tune, log)) done++;

            Debug.Log("Rig Lab: camera mounts fitted on " + done + " of " + bikes.Count + " bike(s).\n" + log);
        }

        static List<BikeController> Targets()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var one = stage.prefabContentsRoot.GetComponent<BikeController>();
                return one != null ? new List<BikeController> { one } : new List<BikeController>();
            }

            var picked = Selection.gameObjects
                .Select(g => g.GetComponentInParent<BikeController>())
                .Where(b => b != null).Distinct().ToList();
            if (picked.Count == 0)
                picked = RigLabScope.Bikes().ToList();

            return picked.Where(Writable).ToList();
        }

        static bool Writable(BikeController bike)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(bike.gameObject)) return true;

            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bike.gameObject);
            bool open = EditorUtility.DisplayDialog("Rig Lab",
                "'" + bike.name + "' is a prefab instance in this scene.\n\nFitting it here would " +
                "write instance overrides, the prefab would keep its old mounts and the two would " +
                "disagree.\n\nFit the prefab itself instead.",
                "Open the prefab", "Skip this bike");

            if (open && !string.IsNullOrEmpty(path)) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            return false;
        }

        static bool FitOne(BikeController bike, CameraFitTuning tune, System.Text.StringBuilder log)
        {
            log.Append("\n").Append(bike.name).Append('\n');

            var model = Find(bike.transform, "Bike Model");
            if (model == null) { log.Append("   SKIPPED: no 'Bike Model'\n"); return false; }

            var rotator = Find(bike.transform, "Rotator");
            if (rotator == null) { log.Append("   SKIPPED: no 'Rotator'\n"); return false; }

            var rider = ActiveRider(bike);
            if (rider == null) log.Append("   no active Humanoid rider, first person left alone\n");

            var box = ArtBounds(model, rider);
            if (box.size == Vector3.zero) { log.Append("   SKIPPED: the bike art has no renderers to measure\n"); return false; }
            log.Append("   bike art  ").Append(F(box.size)).Append("  centre ").Append(F(box.center)).Append('\n');

            Undo.RegisterFullObjectHierarchyUndo(bike.gameObject, "Fit camera mounts");

            Set(rotator, "Cam Follow", new Vector3(0f, 0f, box.min.z - tune.chaseGap), Quaternion.identity, log);
            Set(rotator, "Cam Look At", new Vector3(0f, tune.aimHeight, box.max.z + tune.aimAhead), Quaternion.identity, log);
            Set(rotator, "BehindLookTarget", new Vector3(0f, tune.lookBehindHeight, -tune.lookBehindDistance), Quaternion.identity, log);

            FitFirstPerson(bike, model, box, rider, tune, log);

            foreach (var n in AimCameras(bike, null)) log.Append("   ").Append(n).Append('\n');
            EditorUtility.SetDirty(bike.gameObject);
            return true;
        }

        static void FitFirstPerson(BikeController bike, Transform model, Bounds box,
                                   Animator rider, CameraFitTuning tune, System.Text.StringBuilder log)
        {
            var head = rider != null ? rider.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) { log.Append("   no rider head bone, first person left alone\n"); return; }

            var eye = model.InverseTransformPoint(head.position);

            var screen = LocalBounds(model, Find(model, "Windshield"));
            if (screen.HasValue)
            {
                eye.y = screen.Value.max.y + tune.eyeAboveScreen;
                log.Append("   windshield top ").Append(screen.Value.max.y.ToString("0.###"))
                   .Append("  -> eyeline ").Append(eye.y.ToString("0.###")).Append('\n');
            }
            else
            {
                eye.y -= tune.eyeDropNoScreen;
                log.Append("   no 'Windshield', dropped ").Append(tune.eyeDropNoScreen)
                   .Append(" below the head bone instead\n");
            }
            eye.x = 0f;

            Neutralise(Find(model, "Cam Follow 2")?.parent, model, log);

            Set(model, "FPP cam", eye, Quaternion.identity, log);
            Set(model, "Cam Follow 2", eye, Quaternion.Euler(-90f, 0f, 0f), log);
            Set(model, "Cam Follow 2 (1)", eye + new Vector3(0f, -tune.gazeDrop, tune.gazeDistance),
                Quaternion.Euler(-90f, 0f, 0f), log);

            var axle = Find(model, "Rear Wheel Parent") ?? Find(model, "Rear Wheel");
            if (axle == null)
            {
                log.Append("   no 'Rear Wheel Parent', rear view left on the tail of the art\n");
                Set(model, "FPP cam look behind", new Vector3(0f, eye.y, box.min.z + tune.aimAhead),
                    Quaternion.Euler(0f, 180f, 0f), log);
                return;
            }

            var hub = model.InverseTransformPoint(axle.position);
            log.Append("   rear axle ").Append(F(hub)).Append('\n');

            float rearY = hub.y + tune.rearCamAbove;
            float rearZ = hub.z + tune.rearCamForward;

            if (rearZ > box.min.z && rearZ < box.max.z && rearY < box.max.y + tune.rearClearance)
            {
                log.Append("   rear view was ").Append((box.max.y + tune.rearClearance - rearY).ToString("0.###"))
                   .Append(" inside the bodywork - lifted clear\n");
                rearY = box.max.y + tune.rearClearance;
            }

            Set(model, "FPP cam look behind", new Vector3(0f, rearY, rearZ), Quaternion.Euler(0f, 180f, 0f), log);

            var aim = Ensure(model, "RearWheelLookTarget", log);
            aim.localPosition = new Vector3(0f, rearY - tune.rearAimDrop, rearZ - tune.rearAimDistance);
            aim.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(aim);
        }

        static Bounds? LocalBounds(Transform model, Transform t)
        {
            if (t == null) return null;
            bool started = false;
            var box = new Bounds();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var local = new Bounds(model.InverseTransformPoint(r.bounds.center),
                                       Vector3.Scale(r.bounds.size, Inv(model.lossyScale)));
                if (!started) { box = local; started = true; } else box.Encapsulate(local);
            }
            return started ? box : (Bounds?)null;
        }

        static Transform Ensure(Transform parent, string name, System.Text.StringBuilder log)
        {
            var t = Find(parent, name);
            if (t != null) return t;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Fit camera mounts");
            go.transform.SetParent(parent, false);
            log.Append("   '").Append(name).Append("' CREATED under ").Append(parent.name).Append('\n');
            return go.transform;
        }

        internal static List<string> AimCameras(BikeController bike, Transform rigRoot)
        {
            var pairs = new Dictionary<string, (string follow, string lookAt)>
            {
                { "ThirdPerson",            ("Cam Follow",          "Cam Look At") },
                { "ThirdPerson_LookBehind", ("Cam Follow",          "BehindLookTarget") },
                { "FirstPerson",            ("Cam Follow 2",        "Cam Follow 2 (1)") },
                { "FirstPerson_LookBehind", ("FPP cam look behind", "RearWheelLookTarget") },
            };

            var notes = new List<string>();
            foreach (var cam in (rigRoot != null ? rigRoot : bike.transform).GetComponentsInChildren<CinemachineCamera>(true))
            {
                if (!pairs.TryGetValue(cam.name, out var want))
                { notes.Add(bike.name + ": camera '" + cam.name + "' is not one of the four, left alone"); continue; }

                var follow = Find(bike.transform, want.follow);
                var lookAt = Find(bike.transform, want.lookAt);
                if (follow == null || lookAt == null)
                { notes.Add(bike.name + ": " + cam.name + " NOT aimed: missing " + (follow == null ? want.follow : want.lookAt)); continue; }

                cam.Follow = follow;
                cam.LookAt = lookAt;
                EditorUtility.SetDirty(cam);
                notes.Add(bike.name + ": " + cam.name + " -> " + want.follow + " / " + want.lookAt);
            }
            return notes;
        }

        static Bounds ArtBounds(Transform model, Animator rider)
        {
            bool started = false;
            var box = new Bounds();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (rider != null && r.transform.IsChildOf(rider.transform)) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

                var local = new Bounds(model.InverseTransformPoint(r.bounds.center),
                                       Vector3.Scale(r.bounds.size, Inv(model.lossyScale)));
                if (!started) { box = local; started = true; } else box.Encapsulate(local);
            }
            return started ? box : new Bounds();
        }

        static Vector3 Inv(Vector3 v) => new Vector3(
            Mathf.Approximately(v.x, 0f) ? 1f : 1f / v.x,
            Mathf.Approximately(v.y, 0f) ? 1f : 1f / v.y,
            Mathf.Approximately(v.z, 0f) ? 1f : 1f / v.z);

        static Animator ActiveRider(BikeController bike)
        {
            Animator fallback = null;
            foreach (var a in bike.GetComponentsInChildren<Animator>(true))
            {
                if (a.avatar == null || !a.avatar.isHuman) continue;
                if (a.gameObject.activeInHierarchy) return a;
                if (fallback == null) fallback = a;
            }
            return fallback;
        }

        static void Neutralise(Transform container, Transform model, System.Text.StringBuilder log)
        {
            if (container == null || container == model) return;
            if (container.localPosition == Vector3.zero && container.localRotation == Quaternion.identity) return;

            log.Append("   '").Append(container.name).Append("' was at ").Append(F(container.localPosition))
               .Append(", reset, it was offsetting the first person mounts\n");
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
        }

        static void Set(Transform parent, string name, Vector3 pos, Quaternion rot, System.Text.StringBuilder log)
        {
            var t = Find(parent, name);
            if (t == null) { log.Append("   '").Append(name).Append("' MISSING: not created, nothing written\n"); return; }

            log.Append("   ").Append(name.PadRight(24)).Append(F(t.localPosition)).Append("  ->  ").Append(F(pos)).Append('\n');
            t.localPosition = pos;
            t.localRotation = rot;
            EditorUtility.SetDirty(t);
        }

        static string F(Vector3 v) => string.Format("({0,7:0.###},{1,7:0.###},{2,7:0.###})", v.x, v.y, v.z);

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = Find(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
