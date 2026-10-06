using MotoSquid.Audio;
using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.DevTools;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Characters
{
    public static class CharacterBuilder
    {
        const string DefaultPlayerDonor = "Assets/Prefabs/Player1.prefab";
        const string DefaultAIDonor     = "Assets/Prefabs/Voodoo.prefab";
        const string ControllerDir      = "Assets/Animations/Test";

        static readonly string[] FunctionalNamePrefixes =
            { "Cam Follow", "Cam Look", "Slipstream", "Trail", "Point Light", "FPP", "Behind" };

        public static string RiderControllerPath(RidingStyle s) => $"{ControllerDir}/RiderMaster_{s}.controller";
        public static string BikeControllerPath(RidingStyle s)  => $"{ControllerDir}/BikeMaster_{s}.controller";

        public enum Role { Player, AI }
        public static List<string> Validate(CharacterDefinition def)
        {
            var problems = new List<string>();
            if (def == null) { problems.Add("No CharacterDefinition assigned."); return problems; }

            if (string.IsNullOrWhiteSpace(def.characterName))
                problems.Add("characterName is empty. It is the key that ties this character to its "
                           + "voice, music and the name GameSession carries between scenes.");

            if (def.riderPrefab == null)
                problems.Add("riderPrefab is empty, assign the rigged character model.");
            else
            {
                var animator = def.riderPrefab.GetComponent<Animator>()
                            ?? def.riderPrefab.GetComponentInChildren<Animator>();
                if (animator == null)
                    problems.Add($"'{def.riderPrefab.name}' has no Animator. Set the FBX's Rig tab to "
                               + "Animation Type = Humanoid, then drag the FBX into a prefab.");
                else if (animator.avatar == null || !animator.avatar.isHuman)
                    problems.Add($"'{def.riderPrefab.name}' is not Humanoid (Rig > Animation Type = "
                               + "Humanoid, then Apply). Generic rigs cannot be retargeted to the "
                               + "shared AS_* clip set and have no bone chains for the IK rig.");
                else
                {
                    var needed = new[]
                    {
                        HumanBodyBones.Hips,
                        HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,
                        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                        HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,
                        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                    };
                    var absent = needed.Where(b => animator.GetBoneTransform(b) == null).ToArray();
                    if (absent.Length > 0)
                        problems.Add($"'{def.riderPrefab.name}' avatar is missing bone(s): "
                                   + $"{string.Join(", ", absent)}. Map them in Rig > Configure.");
                }
            }

            if (def.bikePrefab == null)
                problems.Add("bikePrefab is empty, assign the bike art model.");
            else
            {
                bool front = FindDeep(def.bikePrefab.transform, "Front_Wheel") != null;
                bool rear  = FindDeep(def.bikePrefab.transform, "Back_Wheel") != null
                          || FindDeep(def.bikePrefab.transform, "Rear_Wheel") != null;
                if (!front || !rear)
                    problems.Add($"'{def.bikePrefab.name}' needs wheel transforms named Front_Wheel and "
                               + "Back_Wheel (or Rear_Wheel). They set the wheelbase and wheel radii, "
                               + "and they are what the visual spin drives.");
            }

            if (ResolveDonor(def, Role.Player) == null)
                problems.Add($"Player donor prefab not found (expected {DefaultPlayerDonor}, or set "
                           + "playerDonor on the definition).");
            if (ResolveDonor(def, Role.AI) == null)
                problems.Add($"AI donor prefab not found (expected {DefaultAIDonor}, or set aiDonor "
                           + "on the definition).");

            if (string.IsNullOrWhiteSpace(def.outputFolder) || !AssetDatabase.IsValidFolder(def.outputFolder))
                problems.Add($"outputFolder '{def.outputFolder}' is not a folder in the project.");

            if (def.voice == null)
                problems.Add("[warning] No CharacterVoice assigned, this racer will be silent in combat.");
            if (def.score == null)
                problems.Add("[warning] No CharacterScore assigned, no adaptive music for this racer.");
            if (def.defaultWeapon != null && def.defaultWeapon.prop == null)
                problems.Add($"[warning] Weapon '{def.defaultWeapon.displayName}' has no prop model, so "
                           + "the swing will play empty handed.");

            return problems;
        }

        public static bool HasBlockingProblems(IEnumerable<string> problems) =>
            problems.Any(p => !p.StartsWith("[warning]"));

        public static bool BuildBoth(CharacterDefinition def) =>
            Build(def, Role.Player) & Build(def, Role.AI);

        public static bool Build(CharacterDefinition def, Role role)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[CharacterBuild] Editor is compiling/importing. Wait for it to settle, then run again.");
                return false;
            }

            var problems = Validate(def);
            if (HasBlockingProblems(problems))
            {
                Debug.LogError($"[CharacterBuild] '{(def != null ? def.characterName : "null")}' cannot be built yet:\n  "
                             + string.Join("\n  ", problems.Where(p => !p.StartsWith("[warning]"))));
                return false;
            }

            var donor      = ResolveDonor(def, role);
            var donorPath  = AssetDatabase.GetAssetPath(donor);
            string outName = role == Role.Player ? def.PlayerPrefabName : def.AIPrefabName;
            string outPath = $"{def.outputFolder}/{outName}.prefab";

            var root = PrefabUtility.LoadPrefabContents(donorPath);
            try
            {
                var bikeModel        = FindDeep(root.transform, "Bike Model");
                var bodyMesh         = FindDeep(root.transform, "Body Mesh");
                var steeringMeshes   = FindDeep(root.transform, "Steering Meshes") ?? FindDeep(root.transform, "Bike Steering");
                var frontWheel       = FindDeep(root.transform, "Front Wheel");
                var rearWheel        = FindDeep(root.transform, "Rear Wheel");
                var frontWheelParent = FindDeep(root.transform, "Front Wheel Parent");
                var rearWheelParent  = FindDeep(root.transform, "Rear Wheel Parent");

                string missing =
                      bikeModel        == null ? "Bike Model"
                    : bodyMesh         == null ? "Body Mesh"
                    : steeringMeshes   == null ? "Steering Meshes / Bike Steering"
                    : frontWheel       == null ? "Front Wheel"
                    : rearWheel        == null ? "Rear Wheel"
                    : frontWheelParent == null ? "Front Wheel Parent"
                    : rearWheelParent  == null ? "Rear Wheel Parent"
                    : null;
                if (missing != null)
                {
                    Debug.LogError($"[CharacterBuild] Donor {donorPath} is missing wrapper node '{missing}'. Aborted.");
                    return false;
                }

                var rescueHolder = new GameObject("Rescued FX").transform;
                rescueHolder.SetParent(bikeModel, false);
                var slots = new[] { bodyMesh, steeringMeshes, frontWheel, rearWheel };
                foreach (var slot in slots) RescueFunctional(slot, rescueHolder);
                foreach (var slot in slots) StripChildren(slot);

                var art = (GameObject)Object.Instantiate(def.bikePrefab);
                art.name = def.bikePrefab.name;
                art.transform.SetPositionAndRotation(bikeModel.position, bikeModel.rotation);
                art.transform.SetParent(bikeModel, true);

                var frontArt = FindDeep(art.transform, "Front_Wheel");
                var rearArt  = FindDeep(art.transform, "Back_Wheel") ?? FindDeep(art.transform, "Rear_Wheel");

                float frontR = 0.3f, rearR = 0.3f;
                if (frontArt != null) MoveWheelRef(frontArt, frontWheel, frontWheelParent, out frontR);
                if (rearArt  != null) MoveWheelRef(rearArt,  rearWheel,  rearWheelParent,  out rearR);

                MoveWheeliePivot(root, rearWheelParent, rearR);

                var spin = art.GetComponent<WheelVisualSpin>() ?? art.AddComponent<WheelVisualSpin>();
                var sso = new SerializedObject(spin);
                SetRef(sso, "frontWheel", frontArt);      SetRef(sso, "rearWheel", rearArt);
                SetRef(sso, "frontAxisRef", frontWheelParent); SetRef(sso, "rearAxisRef", rearWheelParent);
                SetFloat(sso, "frontRadius", frontR);     SetFloat(sso, "rearRadius", rearR);
                sso.ApplyModifiedPropertiesWithoutUndo();

                var controller = (Component)root.GetComponent<BikeController>()
                              ?? root.GetComponent<BikeAIController>();
                if (controller != null)
                {
                    var so = new SerializedObject(controller);
                    if (frontArt != null) SetFloat(so, "bikeGeometry.FrontWheelRadius", frontR);
                    if (rearArt  != null) SetFloat(so, "bikeGeometry.RearWheelRadius", rearR);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var rider = InstallRider(def, root, bikeModel, art.transform);

                InstallWeapon(def, root, rider);
                WireAudioIdentity(def, root);

                if (rescueHolder.childCount == 0) Object.DestroyImmediate(rescueHolder.gameObject);
                root.name = outName;

                if (System.IO.File.Exists(outPath)) AssetDatabase.DeleteAsset(outPath);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, outPath);
                AssetDatabase.Refresh();

                RecordOutput(def, role, saved);

                Debug.Log($"[CharacterBuild] Built {role} prefab for '{def.characterName}' -> {outPath} "
                        + $"(style {def.style}).\n"
                        + "VERIFY IN EDITOR: ride height (both wheels planted), steering turns the fork, "
                        + "rider seated with hands on the grips and feet on the pegs, weapon sitting in the hand.");
                if (problems.Count > 0)
                    Debug.LogWarning($"[CharacterBuild] '{def.characterName}' built with warnings:\n  "
                                   + string.Join("\n  ", problems));
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject InstallRider(CharacterDefinition def, GameObject root, Transform riderParent,
                                       Transform bikeArt)
        {
            foreach (var stale in root.GetComponentsInChildren<Transform>(true))
                if (stale != null && stale.name == "Dummy_Mannequin")
                    Object.DestroyImmediate(stale.gameObject);

            var rider = (GameObject)Object.Instantiate(def.riderPrefab);
            rider.name = def.riderPrefab.name;
            rider.transform.SetParent(riderParent, false);

            var animator = rider.GetComponent<Animator>() ?? rider.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError($"[CharacterBuild] '{rider.name}' has no Animator, rider installed but " +
                               "unanimated. Set the model's Rig to Humanoid and rebuild.");
                return rider;
            }

            animator.applyRootMotion = false;

            var bikeCtrl = root.GetComponent<BikeController>();
            if (bikeCtrl == null)
            {
                Debug.LogError($"[CharacterBuild] '{def.characterName}': no BikeController on the root, " +
                               "so the rig builder has no bike to anchor the rider to.");
                return rider;
            }
            if (!BikerAnimationCreator.Build(animator, bikeCtrl))
            {
                Debug.LogError($"[CharacterBuild] '{def.characterName}': rig build failed, see above.");
                return rider;
            }

            foreach (var stale in root.GetComponentsInChildren<RiderSeatFollow>(true))
                Object.DestroyImmediate(stale);

            foreach (var combat in root.GetComponentsInChildren<CombatSystem>(true))
            {
                var cso = new SerializedObject(combat);
                SetRef(cso, "riderAnimator", animator);
                SetRef(cso, "bikeAnimController", null);
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var ragdoll in root.GetComponentsInChildren<RagdollActivator>(true))
                SetRef(new SerializedObject(ragdoll), "characterAnimator", animator, apply: true);

            return rider;
        }

        static void InstallWeapon(CharacterDefinition def, GameObject root, GameObject rider)
        {
            var animator = rider.GetComponent<Animator>() ?? rider.GetComponentInChildren<Animator>();
            if (animator == null) return;

            var sockets = new List<WeaponSocket>
            {
                EnsureSocket(animator, HumanBodyBones.LeftHand,  WeaponHand.Left,  "WeaponSocket_L"),
                EnsureSocket(animator, HumanBodyBones.RightHand, WeaponHand.Right, "WeaponSocket_R"),
            };
            sockets.RemoveAll(s => s == null);

            var holder = root.GetComponent<MeleeWeaponHolder>() ?? root.AddComponent<MeleeWeaponHolder>();
            var hso = new SerializedObject(holder);
            SetRef(hso, "startingWeapon", def.defaultWeapon);
            SetRef(hso, "riderAnimator", animator);
            var arr = hso.FindProperty("sockets");
            if (arr != null)
            {
                arr.arraySize = sockets.Count;
                for (int i = 0; i < sockets.Count; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = sockets[i];
            }
            hso.ApplyModifiedPropertiesWithoutUndo();

            foreach (var combat in root.GetComponentsInChildren<CombatSystem>(true))
                SetRef(new SerializedObject(combat), "weaponHolder", holder, apply: true);
        }

        static WeaponSocket EnsureSocket(Animator animator, HumanBodyBones bone, WeaponHand hand, string name)
        {
            var boneT = animator.GetBoneTransform(bone);
            if (boneT == null) return null;

            Transform t = null;
            foreach (Transform c in boneT) if (c.name == name) { t = c; break; }
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(boneT, false); 
                t = go.transform;
            }
            var socket = t.GetComponent<WeaponSocket>() ?? t.gameObject.AddComponent<WeaponSocket>();
            socket.hand = hand;
            return socket;
        }

        static void WireAudioIdentity(CharacterDefinition def, GameObject root)
        {
            foreach (var bark in root.GetComponentsInChildren<VoiceBarkController>(true))
            {
                if (def.voice == null) continue;
                SetRef(new SerializedObject(bark), "voice", def.voice, apply: true);
            }
        }

        static void RecordOutput(CharacterDefinition def, Role role, GameObject saved)
        {
            var so = new SerializedObject(def);
            so.FindProperty(role == Role.Player ? "builtPlayerPrefab" : "builtAIPrefab").objectReferenceValue = saved;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssetIfDirty(def);
        }

        static GameObject ResolveDonor(CharacterDefinition def, Role role)
        {
            if (def == null) return null;
            var explicitDonor = role == Role.Player ? def.playerDonor : def.aiDonor;
            if (explicitDonor != null) return explicitDonor;
            return AssetDatabase.LoadAssetAtPath<GameObject>(role == Role.Player ? DefaultPlayerDonor : DefaultAIDonor);
        }

        static void RescueFunctional(Transform slot, Transform holder)
        {
            var toMove = new List<Transform>();
            foreach (var t in slot.GetComponentsInChildren<Transform>(true))
            {
                if (t == slot) continue;
                bool functional =
                    t.GetComponent<Camera>() || t.GetComponent<Light>() || t.GetComponent<AudioSource>() ||
                    t.GetComponent<ParticleSystem>() || t.GetComponent<TrailRenderer>() ||
                    FunctionalNamePrefixes.Any(p => t.name.StartsWith(p));
                if (functional) toMove.Add(t);
            }
            foreach (var t in toMove.OrderBy(Depth))
                if (t.parent != holder && t.IsChildOf(slot))
                    t.SetParent(holder, true);
        }

        static void StripChildren(Transform slot)
        {
            foreach (var child in slot.Cast<Transform>().ToArray())
                Object.DestroyImmediate(child.gameObject);
        }

        static void MoveWheeliePivot(GameObject root, Transform rearWheelParent, float rearRadius)
        {
            var wheelie = FindDeep(root.transform, "Wheelie Transform");
            if (wheelie == null || rearWheelParent == null) return;

            var kids = new List<(Transform t, Vector3 pos, Quaternion rot)>();
            foreach (Transform k in wheelie) kids.Add((k, k.position, k.rotation));

            wheelie.position = rearWheelParent.position - Vector3.up * rearRadius;

            foreach (var (t, pos, rot) in kids) t.SetPositionAndRotation(pos, rot);
        }

        static void MoveWheelRef(Transform artWheel, Transform wheelNode, Transform wheelParent, out float radius)
        {
            radius = 0.3f;
            Vector3 axle = TryTyreBounds(artWheel, out var c, out float r) ? c : artWheel.position;
            if (r > 0.0001f) radius = r;
            wheelParent.position = axle;
            wheelNode.position   = axle;
        }

        static bool TryTyreBounds(Transform wheel, out Vector3 center, out float radius)
        {
            center = wheel.position; radius = 0f;
            Renderer best = null;
            foreach (var r in wheel.GetComponentsInChildren<Renderer>())
                if (best == null || r.bounds.size.y > best.bounds.size.y) best = r;
            if (best == null) return false;
            center = best.bounds.center;

            var e = best.bounds.extents;
            float a = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            float c = Mathf.Min(e.x, Mathf.Min(e.y, e.z));
            float b = e.x + e.y + e.z - a - c;
            radius = (a + b) * 0.5f;
            return true;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        static int Depth(Transform t) { int d = 0; while (t.parent) { d++; t = t.parent; } return d; }

        static void SetFloat(SerializedObject so, string path, float v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = v;
        }

        static void SetRef(SerializedObject so, string path, Object v, bool apply = false)
        {
            var p = so.FindProperty(path);
            if (p != null) p.objectReferenceValue = v;
            if (apply) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
