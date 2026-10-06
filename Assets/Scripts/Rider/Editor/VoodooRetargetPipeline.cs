using System.Collections.Generic;
using System.IO;
using System.Linq;
using KINEMATION.RetargetPro.Editor.Scripts.Bakers;
using KINEMATION.RetargetPro.Editor.Scripts.Mapping;
using KINEMATION.RetargetPro.Runtime;
using KINEMATION.RetargetPro.Runtime.Features;
using KINEMATION.RetargetPro.Runtime.Features.BasicRetargeting;
using KINEMATION.RetargetPro.Runtime.Features.IKRetargeting;
using KINEMATION.RetargetPro.Runtime.Features.RootPelvisRetargeting;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.Rider
{
    public static class VoodooRetargetPipeline
    {
        const string Tag = "[Retarget]";

        const string SourceModel  = "Assets/Animations/Test/Characters/SM_Mannequin/SKM_Mannequin.fbx";
        const string TargetModel  = "Assets/Models/Voodoo/VoodooRigged.fbx";
        const string TargetTPose  = "Assets/KINEMATION/RetargetPro/Poses/A_TPose_CC4.anim";
        const string ClipRoot     = "Assets/Animations/Test";
        const string OutRoot      = "Assets/Animations/Voodoo";
        const string ProfileRoot  = "Assets/Animations/Voodoo/Profiles";
        const string ProbeClip    = "AS_Idle_Riding";

        const string BikePrefabRoot = "Assets/Prefabs/Characters";

        static readonly string[] Styles = { "SuperSport", "Upright" };

        static readonly Dictionary<string, string> StyleBikes = new Dictionary<string, string>
        {
            { "SuperSport", $"{BikePrefabRoot}/VoodooBike_R.prefab" },
            { "Upright",    $"{BikePrefabRoot}/Futuristic_Bike.prefab" },
        };

        static readonly (string bone, string anchor)[] ContactAnchors =
        {
            ("CC_Base_L_Hand", "GripAnchor_L"),
            ("CC_Base_R_Hand", "GripAnchor_R"),
            ("CC_Base_L_Foot", "FootPegGrip_L"),
            ("CC_Base_R_Foot", "FootPegGrip_R"),
        };

        static readonly (string human, string bone)[] SourceHumanMap =
        {
            ("Hips",          "Root_M"),
            ("Spine",         "Spine1_M"),
            ("Chest",         "Spine2_M"),
            ("UpperChest",    "Chest_M"),
            ("Neck",          "Neck_M"),
            ("Head",          "Head_M"),
            ("LeftShoulder",  "Scapula_L"),
            ("RightShoulder", "Scapula_R"),
            ("LeftUpperArm",  "Shoulder_L"),
            ("RightUpperArm", "Shoulder_R"),
            ("LeftLowerArm",  "Elbow_L"),
            ("RightLowerArm", "Elbow_R"),
            ("LeftHand",      "Wrist_L"),
            ("RightHand",     "Wrist_R"),
            ("LeftUpperLeg",  "Hip_L"),
            ("RightUpperLeg", "Hip_R"),
            ("LeftLowerLeg",  "Knee_L"),
            ("RightLowerLeg", "Knee_R"),
            ("LeftFoot",      "Ankle_L"),
            ("RightFoot",     "Ankle_R"),
        };

        enum ChainKind { RootPelvis, Basic, Ik }

        static readonly (string name, ChainKind kind, string[] source, string[] target, Vector3 pole)[] Chains =
        {
            ("Pelvis", ChainKind.RootPelvis,
                new[] { "Root_M" },
                new[] { "CC_Base_Hip" }, Vector3.zero),
            ("Spine", ChainKind.Basic,
                new[] { "Spine1_M", "Spine2_M", "Chest_M" },
                new[] { "CC_Base_Waist", "CC_Base_Spine01", "CC_Base_Spine02" }, Vector3.zero),
            ("Neck", ChainKind.Basic,
                new[] { "Neck_M", "Head_M" },
                new[] { "CC_Base_NeckTwist01", "CC_Base_Head" }, Vector3.zero),
            ("LeftArm", ChainKind.Ik,
                new[] { "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L" },
                new[] { "CC_Base_L_Clavicle", "CC_Base_L_Upperarm", "CC_Base_L_Forearm", "CC_Base_L_Hand" }, -Vector3.forward),
            ("RightArm", ChainKind.Ik,
                new[] { "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R" },
                new[] { "CC_Base_R_Clavicle", "CC_Base_R_Upperarm", "CC_Base_R_Forearm", "CC_Base_R_Hand" }, -Vector3.forward),
            ("LeftLeg", ChainKind.Ik,
                new[] { "Hip_L", "Knee_L", "Ankle_L" },
                new[] { "CC_Base_L_Thigh", "CC_Base_L_Calf", "CC_Base_L_Foot" }, Vector3.forward),
            ("RightLeg", ChainKind.Ik,
                new[] { "Hip_R", "Knee_R", "Ankle_R" },
                new[] { "CC_Base_R_Thigh", "CC_Base_R_Calf", "CC_Base_R_Foot" }, Vector3.forward),
        };

        [MenuItem("MotoSquid/Animation/Retarget/1, Configure and Solve")]
        public static void ConfigureAndSolve()
        {
            if (!Ready()) return;

            if (!SetSourceAvatar()) return;

            EnsureFolder(OutRoot);
            EnsureFolder(ProfileRoot);

            foreach (var style in Styles)
            {
                EnsureFolder($"{OutRoot}/{style}");

                var profile = LoadOrCreateProfile(style);
                if (profile == null) continue;

                if (!RetargetProfileModelRigUtility.TryComposeProfileRigs(profile, false, out var message))
                {
                    Debug.LogError($"{Tag} {style}: rig compose failed. {message}");
                    continue;
                }

                Debug.Log($"{Tag} {style}: {message}");
                if (!BuildFeatures(profile, style)) continue;
                SolveOffsets(profile, style);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Configure complete. Review the warnings above, then run '2, Bake Test Clip'.");
        }

        [MenuItem("MotoSquid/Animation/Retarget/2, Bake Test Clip")]
        public static void BakeTestClip()
        {
            if (!Ready()) return;

            foreach (var style in Styles)
            {
                var profile = AssetDatabase.LoadAssetAtPath<RetargetProfile>(ProfilePath(style));
                var clip = FindSourceClip(style, ProbeClip);
                if (profile == null || clip == null)
                {
                    Debug.LogError($"{Tag} {style}: run step 1 first ({ProbeClip} or its profile is missing).");
                    continue;
                }

                var baked = BakeClips(profile, new List<(string, AnimationClip)> { (ProbeClip, clip) }, style);
                foreach (var result in baked) Debug.Log($"{Tag} {style}: baked {AssetDatabase.GetAssetPath(result)}");
            }

            Debug.Log($"{Tag} Test bake done. Put each clip on Voodoo_R_Player and look at it before running step 3.");
        }

        [MenuItem("MotoSquid/Animation/Retarget/3, Bake All")]
        public static void BakeAll()
        {
            if (!Ready()) return;

            if (!EditorUtility.DisplayDialog("Bake all rider clips",
                    "This writes ~148 animation clips and churns temporary FBX files through the output folders.\n\n" +
                    "Commit first. Continue?", "Bake", "Cancel"))
            {
                return;
            }

            foreach (var style in Styles)
            {
                var profile = AssetDatabase.LoadAssetAtPath<RetargetProfile>(ProfilePath(style));
                if (profile == null)
                {
                    Debug.LogError($"{Tag} {style}: no profile at {ProfilePath(style)}. Run step 1.");
                    continue;
                }

                var clips = GatherRiderClips(style);
                Debug.Log($"{Tag} {style}: baking {clips.Count} rider clip(s).");
                BakeClips(profile, clips, style);
                AuditLoopFlags(style);
            }
        }

        static void Release(RetargetAnimBaker baker)
        {
            baker.UnInitializeBaker();
            baker.CleanupPreviewResources();
        }

        static bool Ready()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating) return true;
            Debug.LogWarning($"{Tag} Editor is compiling/importing. Wait for it to settle, then run again.");
            return false;
        }

        static bool SetSourceAvatar()
        {
            var importer = AssetImporter.GetAtPath(SourceModel) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{Tag} Source model not found at {SourceModel}.");
                return false;
            }

            var bones = new HashSet<string>(BoneNames(SourceModel));
            var missing = SourceHumanMap.Where(m => !bones.Contains(m.bone)).Select(m => m.bone).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"{Tag} SKM_Mannequin is missing expected bones: {string.Join(", ", missing)}. " +
                               "The skeleton is not the Advanced Skeleton rig this tool expects.");
                return false;
            }

            var description = importer.humanDescription;
            description.human = SourceHumanMap.Select(m => new HumanBone
            {
                humanName = m.human,
                boneName  = m.bone,
                limit     = new HumanLimit { useDefaultValues = true }
            }).ToArray();

            importer.animationType   = ModelImporterAnimationType.Human;
            importer.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar    = null;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            var animator = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel)?.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid)
            {
                Debug.LogError($"{Tag} Avatar did not validate after reimport. Open Rig > Configure and check by hand.");
                return false;
            }

            Debug.Log($"{Tag} Source avatar mapped explicitly ({SourceHumanMap.Length} bones). Hips = Root_M, LeftUpperLeg = Hip_L.");
            return true;
        }

        static RetargetProfile LoadOrCreateProfile(string style)
        {
            var path = ProfilePath(style);
            var profile = AssetDatabase.LoadAssetAtPath<RetargetProfile>(path);
            var created = profile == null;

            if (created)
            {
                profile = ScriptableObject.CreateInstance<RetargetProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.sourceCharacter = LoadModel(SourceModel);
            profile.targetCharacter = LoadModel(TargetModel);
            profile.saveFolderPath  = $"{OutRoot}/{style}";

            if (created) profile.targetPose = EnsureTargetPose();

            if (profile.sourceCharacter == null || profile.targetCharacter == null)
            {
                Debug.LogError($"{Tag} {style}: source or target model missing on disk.\n" +
                               $"  source: {SourceModel} -> {(profile.sourceCharacter == null ? "NOT FOUND" : "ok")}\n" +
                               $"  target: {TargetModel} -> {(profile.targetCharacter == null ? "NOT FOUND" : "ok")}");
                return null;
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static bool BuildFeatures(RetargetProfile profile, string style)
        {
            if (profile.retargetFeatures != null)
            {
                foreach (var existing in profile.retargetFeatures.Where(f => f != null))
                    Undo.DestroyObjectImmediate(existing);
                profile.retargetFeatures.Clear();
            }
            else
            {
                profile.retargetFeatures = new List<RetargetFeature>();
            }

            foreach (var (name, kind, source, target, pole) in Chains)
            {
                if (!TryChain(profile.sourceRig, source, name, out var sourceChain, out var missing) ||
                    !TryChain(profile.targetRig, target, name, out var targetChain, out missing))
                {
                    Debug.LogError($"{Tag} {style}: chain '{name}' cannot be built, {missing} is not in the rig.");
                    return false;
                }

                var feature = (RetargetFeature)ScriptableObject.CreateInstance(
                    kind == ChainKind.Ik ? typeof(IKRetargetFeature) :
                    kind == ChainKind.RootPelvis ? typeof(RootPelvisRetargetFeature) :
                    typeof(BasicRetargetFeature));

                feature.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                feature.name      = feature.GetType().Name;
                feature.sourceRig = profile.sourceRig;
                feature.targetRig = profile.targetRig;

                var basic = (BasicRetargetFeature)feature;
                basic.sourceChain = sourceChain;
                basic.targetChain = targetChain;

                if (kind == ChainKind.RootPelvis)
                {
                    basic.translationWeight = 1f;
                    basic.scaleWeight = 1f;
                }

                if (feature is IKRetargetFeature ik)
                {
                    ik.maxReachMultiplier = 0.95f;
                    ik.poleWeight = 1f;
                    ik.jointOffset = pole;
                    ik.OnFeatureAdded();
                }

                AssetDatabase.AddObjectToAsset(feature, profile);
                profile.retargetFeatures.Add(feature);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {style}: built {Chains.Length} chain(s) explicitly, no preset involved.");
            return true;
        }

        static bool TryChain(KRig rig, string[] bones, string chainName, out KRigElementChain chain, out string missing)
        {
            chain = new KRigElementChain { chainName = chainName };
            missing = null;

            foreach (var bone in bones)
            {
                var index = rig.rigHierarchy.FindIndex(e => e.name == bone);
                if (index < 0)
                {
                    missing = bone;
                    return false;
                }

                chain.elementChain.Add(rig.rigHierarchy[index]);
            }

            return true;
        }

        static void SolveOffsets(RetargetProfile profile, string style)
        {
            var clip = FindSourceClip(style, ProbeClip);
            if (clip == null)
            {
                Debug.LogWarning($"{Tag} {style}: {ProbeClip} not found, skipping offset solve.");
                return;
            }

            var anchors = BikeAnchorOffsets(style);
            if (anchors == null) return;

            var baker = new RetargetAnimBaker();
            baker.SetProfile(profile, false);

            try
            {
                if (!baker.TryInitializeBaker(out var error))
                {
                    Debug.LogError($"{Tag} {style}: baker init failed. {error}");
                    return;
                }

                var features = IkFeatures(profile).ToArray();
                foreach (var feature in features) feature.effectorOffset = Vector3.zero;

                var sourceBones = BonesByName(baker.SourcePreviewInstance);
                var targetBones = BonesByName(baker.TargetPreviewInstance);
                var sourceRoot  = baker.SourcePreviewInstance.transform;
                var targetRoot  = baker.TargetPreviewInstance.transform;

                ReportPoseMatch(profile, sourceBones, targetBones, sourceRoot, targetRoot, baker, style);

                if (!targetBones.TryGetValue("CC_Base_Hip", out var pelvis))
                {
                    Debug.LogError($"{Tag} {style}: CC_Base_Hip not found on the target, cannot seat the bike.");
                    return;
                }

                var totals = new Dictionary<IKRetargetFeature, (Vector3 want, Vector3 tip, Vector3 shoulder)>();
                const int samples = 12;

                for (int i = 0; i < samples; i++)
                {
                    baker.RetargetAtTime(clip, null, clip.length * i / (samples - 1f));
                    var seat = targetRoot.InverseTransformPoint(pelvis.position);

                    foreach (var feature in features)
                    {
                        if (!TryContact(feature, targetBones, anchors, out var tip, out var anchorOffset)) continue;
                        if (!targetBones.TryGetValue(feature.targetChain.elementChain[0].name, out var shoulder))
                            continue;

                        totals.TryGetValue(feature, out var sum);
                        totals[feature] = (sum.want + seat + anchorOffset,
                                           sum.tip + targetRoot.InverseTransformPoint(tip.position),
                                           sum.shoulder + targetRoot.InverseTransformPoint(shoulder.position));
                    }
                }

                Debug.Log($"{Tag} {style}: pelvis at {Fmt(targetRoot.InverseTransformPoint(pelvis.position))}");

                foreach (var (bone, anchor) in ContactAnchors)
                {
                    var feature = features.FirstOrDefault(f => TipName(f.targetChain) == bone);
                    if (feature == null || !totals.TryGetValue(feature, out var sum))
                    {
                        Debug.LogError($"{Tag} {style}: {bone} -> {anchor} produced no result.");
                        continue;
                    }

                    var want  = sum.want / samples;
                    var mean  = want - sum.tip / samples;
                    var reach = ChainLength(feature, targetBones) * feature.GetReachMultiplier();
                    var need  = Vector3.Distance(want, sum.shoulder / samples);

                    feature.effectorOffset = Vector3.zero;
                    EditorUtility.SetDirty(feature);

                    Debug.Log($"{Tag} {style}: {bone} sits {mean.magnitude * 100f:0.0} cm from {anchor}, " +
                              $"delta {Fmt(mean)}, needs {need * 100f:0.0} cm of {reach * 100f:0.0} cm reach" +
                              (need <= reach ? " (reachable)" : $" (SHORT BY {(need - reach) * 100f:0.0} cm)"));
                }
            }
            finally
            {
                Release(baker);
            }
        }

        static Dictionary<string, Vector3> BikeAnchorOffsets(string style)
        {
            var path = StyleBikes[style];
            var bike = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (bike == null)
            {
                Debug.LogError($"{Tag} {style}: bike prefab not found at {path}.");
                return null;
            }

            var nodes = BonesByName(bike);
            if (!nodes.TryGetValue("SeatAnchor", out var seat))
            {
                Debug.LogError($"{Tag} {style}: {bike.name} has no SeatAnchor. Add the five anchors first.");
                return null;
            }

            var root = bike.transform;
            var seatLocal = root.InverseTransformPoint(seat.position);
            var offsets = new Dictionary<string, Vector3>();

            foreach (var (_, anchor) in ContactAnchors)
            {
                if (!nodes.TryGetValue(anchor, out var node))
                {
                    Debug.LogError($"{Tag} {style}: {bike.name} has no {anchor}.");
                    return null;
                }

                offsets[anchor] = root.InverseTransformPoint(node.position) - seatLocal;
            }

            Debug.Log($"{Tag} {style}: solving contacts against {bike.name} " +
                      "(assumes the rider and bike prefabs share a forward axis).");
            return offsets;
        }

        static bool TryContact(IKRetargetFeature feature, Dictionary<string, Transform> targetBones,
            Dictionary<string, Vector3> anchors, out Transform tip, out Vector3 anchorOffset)
        {
            tip = null;
            anchorOffset = Vector3.zero;

            var tipName = TipName(feature.targetChain);
            if (tipName == null) return false;

            var match = ContactAnchors.FirstOrDefault(c => c.bone == tipName);
            return match.anchor != null
                   && anchors.TryGetValue(match.anchor, out anchorOffset)
                   && targetBones.TryGetValue(tipName, out tip);
        }

        static void ReportPoseMatch(RetargetProfile profile, Dictionary<string, Transform> sourceBones,
            Dictionary<string, Transform> targetBones, Transform sourceRoot, Transform targetRoot,
            RetargetAnimBaker baker, string style)
        {
            if (profile.sourcePose != null) profile.sourcePose.SampleAnimation(baker.SourcePreviewInstance, 0f);

            if (!sourceBones.TryGetValue("Shoulder_L", out var sourceArm) ||
                !sourceBones.TryGetValue("Wrist_L", out var sourceHand) ||
                !targetBones.TryGetValue("CC_Base_L_Upperarm", out var targetArm) ||
                !targetBones.TryGetValue("CC_Base_L_Hand", out var targetHand))
            {
                Debug.LogError($"{Tag} {style}: rest pose check skipped, an arm bone was not found on a preview instance.");
                return;
            }

            var sourceAngle = ArmPitch(sourceRoot, sourceArm, sourceHand);
            var bindAngle = ArmPitch(targetRoot, targetArm, targetHand);

            var posedAngle = bindAngle;
            if (profile.targetPose != null)
            {
                profile.targetPose.SampleAnimation(baker.TargetPreviewInstance, 0f);
                posedAngle = ArmPitch(targetRoot, targetArm, targetHand);
            }

            var usePose = profile.targetPose != null &&
                          Mathf.Abs(sourceAngle - posedAngle) <= Mathf.Abs(sourceAngle - bindAngle);

            if (!usePose && profile.targetPose != null)
            {
                profile.targetPose = null;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.LogWarning($"{Tag} {style}: dropped the CC4 T-pose, the bind pose is closer to the source. " +
                                 "The basis is cached at init, so RUN STEP 1 AGAIN to solve against it.");
            }

            var spread = Mathf.Abs(sourceAngle - (usePose ? posedAngle : bindAngle));

            Debug.Log($"{Tag} {style}: reference poses, source {sourceAngle:0.0}°, " +
                      $"target bind {bindAngle:0.0}°, target posed {posedAngle:0.0}° from horizontal. " +
                      $"Using {(usePose ? profile.targetPose.name : "the bind pose")} ({spread:0.0}° off source).");

            if (spread > 10f)
            {
                Debug.LogWarning($"{Tag} {style}: reference poses are still {spread:0.0}° apart. " +
                                 "Author a matching Source Pose clip or every chain inherits this error.");
            }
        }

        static string Fmt(Vector3 v) => $"({v.x * 100f:0.0}, {v.y * 100f:0.0}, {v.z * 100f:0.0}) cm";

        static float ArmPitch(Transform root, Transform upper, Transform hand)
        {
            var local = root.InverseTransformPoint(hand.position) - root.InverseTransformPoint(upper.position);
            return Mathf.Abs(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg);
        }

        static float ChainLength(IKRetargetFeature feature, Dictionary<string, Transform> bones)
        {
            var names = feature.targetChain.elementChain.Select(e => e.name).ToArray();
            float length = 0f;

            for (int i = 1; i < names.Length; i++)
            {
                if (!bones.TryGetValue(names[i - 1], out var a) || !bones.TryGetValue(names[i], out var b)) continue;
                length += Vector3.Distance(a.position, b.position);
            }

            return length;
        }

        static List<AnimationClip> BakeClips(RetargetProfile profile, List<(string name, AnimationClip clip)> clips,
            string style)
        {
            var baked = new List<AnimationClip>();
            var baker = new RetargetAnimBaker();
            baker.SetProfile(profile, false);
            baker.SetRigType(true);
            baker.OutputType      = RetargetAnimBaker.BakeOutputType.AnimationClip;
            baker.LoopCount       = 1;
            baker.CopyClipSettings = true;

            try
            {
                if (!baker.TryInitializeBaker(out var error))
                {
                    Debug.LogError($"{Tag} {style}: baker init failed. {error}");
                    return baked;
                }

                for (int i = 0; i < clips.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar($"Baking {style}",
                            clips[i].name, (i + 1f) / clips.Count))
                    {
                        Debug.LogWarning($"{Tag} {style}: cancelled after {i} clip(s).");
                        break;
                    }

                    var result = baker.BakeAnimation(clips[i].clip);
                    if (result == null)
                    {
                        Debug.LogError($"{Tag} {style}: {clips[i].name} failed to bake.");
                        continue;
                    }

                    Rename(result, $"Voodoo_{clips[i].name}");
                    baked.Add(result);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Release(baker);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"{Tag} {style}: {baked.Count}/{clips.Count} clip(s) baked to {OutRoot}/{style}.");
            return baked;
        }

        static List<(string name, AnimationClip clip)> GatherRiderClips(string style)
        {
            var clips = new List<(string, AnimationClip)>();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{ClipRoot}/{style}" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_Bike")) continue;

                var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                if (clip != null) clips.Add((name, clip));
                else Debug.LogWarning($"{Tag} {style}: {name}.fbx contains no animation clip.");
            }

            return clips;
        }

        static void Rename(Object asset, string name)
        {
            var from = AssetDatabase.GetAssetPath(asset);
            var to = $"{Path.GetDirectoryName(from).Replace('\\', '/')}/{name}.anim";
            if (from == to) return;

            if (AssetDatabase.LoadAssetAtPath<Object>(to) != null)
                AssetDatabase.DeleteAsset(to);

            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"{Tag} could not move {from} -> {to}: {error}");
        }

        static AnimationClip EnsureTargetPose()
        {
            var path = $"{ProfileRoot}/A_TPose_CC4_Rebound.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetTPose);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TargetModel);
            if (source == null || model == null) return null;

            var paths = new Dictionary<string, string>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                paths[t.name] = AnimationUtility.CalculateTransformPath(t, model.transform);

            var rebound = new AnimationClip { frameRate = source.frameRate };
            int bound = 0, dropped = 0;

            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var bone = binding.path.Split('/').Last();
                if (!paths.TryGetValue(bone, out var actual)) { dropped++; continue; }

                var rebind = binding;
                rebind.path = actual;
                AnimationUtility.SetEditorCurve(rebound, rebind, AnimationUtility.GetEditorCurve(source, binding));
                bound++;
            }

            AssetDatabase.CreateAsset(rebound, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Rebound the CC4 T-pose onto {model.name}: {bound} curve(s) bound, {dropped} dropped " +
                      $"(bones absent from this rig). Saved to {path}.");
            return rebound;
        }

        static void AuditLoopFlags(string style)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { $"{OutRoot}/{style}" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                var expected = ShouldLoop(clip.name);
                var actual = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
                if (expected == actual) continue;

                Debug.LogWarning($"{Tag} {style}: {clip.name} has Loop Time {actual}, expected {expected}.");
            }
        }

        static bool ShouldLoop(string name)
        {
            if (name.EndsWith("_Loop")) return true;
            return name.EndsWith("AS_Idle_Mounted") || name.EndsWith("AS_Idle_Riding") || name.EndsWith("AS_Brake");
        }

        static AnimationClip FindSourceClip(string style, string fbxName)
        {
            return GatherRiderClips(style).FirstOrDefault(c => c.name == fbxName).clip;
        }

        static IEnumerable<IKRetargetFeature> IkFeatures(RetargetProfile profile)
        {
            return profile.retargetFeatures == null
                ? Enumerable.Empty<IKRetargetFeature>()
                : profile.retargetFeatures.OfType<IKRetargetFeature>();
        }

        static string TipName(KRigElementChain chain)
        {
            return chain == null || chain.elementChain == null || chain.elementChain.Count == 0
                ? null
                : chain.elementChain[chain.elementChain.Count - 1].name;
        }

        static Dictionary<string, Transform> BonesByName(GameObject root)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) map[t.name] = t;
            return map;
        }

        static IEnumerable<string> BoneNames(string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            return model == null
                ? Enumerable.Empty<string>()
                : model.GetComponentsInChildren<Transform>(true).Select(t => t.name);
        }

        static GameObject LoadModel(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (direct != null) return direct;

            var name = Path.GetFileNameWithoutExtension(path);
            var found = AssetDatabase.FindAssets($"{name} t:Model")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);

            if (found == null) return null;
            Debug.LogWarning($"{Tag} '{path}' not found; using '{found}' instead.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(found);
        }

        static string ProfilePath(string style) => $"{ProfileRoot}/Voodoo_{style}.asset";

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
