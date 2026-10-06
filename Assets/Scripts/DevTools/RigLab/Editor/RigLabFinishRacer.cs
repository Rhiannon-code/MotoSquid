using MotoSquid.Bike;
using MotoSquid.Combat;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabFinishRacer
    {
        [MenuItem("Tools/Rig Lab/44. Finish Racers (wheels, weapon, speed FX)", false, 440)]
        static void FinishScene()
        {
            var found = RigLabScope.Bikes(true);
            if (found.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in " + RigLabScope.Where() + "."); return; }
            var bikes = RigLabScope.Narrow(found);

            var gaps = new List<string>();
            foreach (var bike in bikes) Finish(bike.gameObject, bike, null, gaps);

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: finished " + bikes.Length + " racer(s) in " + RigLabScope.Where() + "." +
                      RigLabScope.ScopeNote(bikes.Length, found.Length) +
                      Report(gaps) + "\n   AI prefabs are done separately, run 45.");
        }

        [MenuItem("Tools/Rig Lab/45. Finish AI Racer Prefabs", false, 450)]
        static void FinishAI()
        {
            const string dir = "Assets/Prefabs/Characters/AI";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogError("Rig Lab: " + dir + " not found, run 43 first."); return; }

            var gaps = new List<string>();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var ai = root.GetComponent<BikeAIController>();
                    Finish(root, null, ai, gaps);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Rig Lab: finished " + n + " AI prefab(s)." + Report(gaps));
        }

        static string Report(List<string> gaps) =>
            gaps.Count > 0 ? "\n\nNeeds attention:\n   " + string.Join("\n   ", gaps.Distinct()) : "\n   everything resolved.";

        static void Finish(GameObject root, BikeController player, BikeAIController ai, List<string> gaps)
        {
            var refs = player != null ? player.bikeReferences : null;
            var aiRefs = ai != null ? ai.bikeReferences : null;

            Transform frontWheel = refs != null ? refs.FrontWheel : aiRefs != null ? aiRefs.FrontWheel : null;
            Transform rearWheel = refs != null ? refs.RearWheel : aiRefs != null ? aiRefs.RearWheel : null;
            Transform frontAxle = refs != null ? refs.FrontWheelParent : aiRefs != null ? aiRefs.FrontWheelParent : null;
            Transform rearAxle = refs != null ? refs.RearWheelParent : aiRefs != null ? aiRefs.RearWheelParent : null;
            float frontR = player != null ? player.bikeGeometry.FrontWheelRadius : ai != null ? ai.bikeGeometry.FrontWheelRadius : 0.3f;
            float rearR = player != null ? player.bikeGeometry.RearWheelRadius : ai != null ? ai.bikeGeometry.RearWheelRadius : 0.3f;

            var spin = root.GetComponentInChildren<WheelVisualSpin>(true) ?? root.AddComponent<WheelVisualSpin>();
            spin.frontWheel = frontWheel; spin.rearWheel = rearWheel;
            spin.frontAxisRef = frontAxle; spin.rearAxisRef = rearAxle;
            spin.frontRadius = frontR; spin.rearRadius = rearR;
            EditorUtility.SetDirty(spin);
            if (frontWheel == null || rearWheel == null) gaps.Add(root.name + ": WheelVisualSpin has no wheel transforms");

            var riders = root.GetComponentsInChildren<Animator>(true)
                             .Where(a => a.avatar != null && a.avatar.isHuman).ToList();
            if (riders.Count == 0) { gaps.Add(root.name + ": no Humanoid rider, weapon not installed"); }
            else
            {
                // Every rider needs its own pair, MeleeWeaponHolder.BindRider re-resolves the sockets
                // off whichever rider RiderSwitch made active, so a rider without them loses its weapon
                foreach (var r in riders)
                {
                    Socket(r, HumanBodyBones.LeftHand,  WeaponHand.Left,  "WeaponSocket_L");
                    Socket(r, HumanBodyBones.RightHand, WeaponHand.Right, "WeaponSocket_R");
                }

                var animator = riders.FirstOrDefault(a => a.gameObject.activeInHierarchy) ?? riders[0];
                var sockets = animator.GetComponentsInChildren<WeaponSocket>(true).ToList();

                var holder = root.GetComponentInChildren<MeleeWeaponHolder>(true) ?? root.AddComponent<MeleeWeaponHolder>();
                var hso = new SerializedObject(holder);
                var ra = hso.FindProperty("riderAnimator"); if (ra != null) ra.objectReferenceValue = animator;
                var arr = hso.FindProperty("sockets");
                if (arr != null)
                {
                    arr.arraySize = sockets.Count;
                    for (int i = 0; i < sockets.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sockets[i];
                }
                hso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(holder);

                foreach (var combat in root.GetComponentsInChildren<CombatSystem>(true))
                { combat.weaponHolder = holder; EditorUtility.SetDirty(combat); }

                if (holder.startingWeapon == null)
                    gaps.Add(root.name + ": MeleeWeaponHolder has no starting weapon, pick one from Definitions/Weapons");
            }

            if (player != null)
            {
                var fx = root.GetComponentInChildren<BikeSpeedFX>(true) ?? root.AddComponent<BikeSpeedFX>();
                fx.bikeController = player;
                fx.boostSystem = root.GetComponentInChildren<BoostSystem>(true);
                EditorUtility.SetDirty(fx);
                if (fx.postProcessVolume == null)
                    gaps.Add(root.name + ": BikeSpeedFX has no post process volume (scene asset)");
            }
        }

        static WeaponSocket Socket(Animator animator, HumanBodyBones bone, WeaponHand hand, string name)
        {
            var parent = animator.GetBoneTransform(bone);
            if (parent == null) return null;
            var existing = parent.Find(name);
            var t = existing != null ? existing : new GameObject(name).transform;
            if (existing == null) { t.SetParent(parent, false); t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; }
            var socket = t.GetComponent<WeaponSocket>() ?? t.gameObject.AddComponent<WeaponSocket>();
            socket.hand = hand;
            EditorUtility.SetDirty(socket);
            return socket;
        }
    }
}
