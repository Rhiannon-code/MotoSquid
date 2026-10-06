using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.Rider
{
    public static class RiderTrajectorySampler
    {
        const string Tag = "[Trajectory]";

        const string SourceModel = "Assets/Animations/Test/Characters/SM_Mannequin/SKM_Mannequin.fbx";
        const string ClipRoot    = "Assets/Animations/Test";
        const string OutRoot     = "Assets/Animations/Trajectories";
        const string ReferenceClip = "AS_Idle_Riding";
        const float MinSampleRate = 60f;
        static readonly string[] Styles = { "SuperSport", "Upright" };
        static readonly string[] OffSeatClips =
        {
            "AS_Adjust_The_Mirror", "AS_Dizzy", "AS_Fall_From_Bike_Back",
            "AS_Mount_Left", "AS_Mount_Right", "AS_Mount_Left_Passenger", "AS_Mount_Right_Passenger",
            "AS_Dismount_Left", "AS_Dismount_Right", "AS_Dismount_Left_Passenger", "AS_Dismount_Right_Passenger",
        };

        static readonly HumanBodyBones[] PositionBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Head,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        };

        [MenuItem("MotoSquid/Animation/Trajectory/Report Anchor Placement")]
        public static void ReportAnchors()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (model == null) { Debug.LogError($"{Tag} Source model not found at {SourceModel}."); return; }

            foreach (var style in Styles)
            {
                if (!EnsureSourceAvatarMatchesClips(style)) continue;

                var reference = GatherOnSeatClips(style, out _)
                    .FirstOrDefault(c => c.name == ReferenceClip).clip;
                if (reference == null) { Debug.LogError($"{Tag} {style}: {ReferenceClip} missing."); continue; }

                var instance = Object.Instantiate(model);
                instance.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var animator = instance.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null || !animator.avatar.isHuman) continue;

                    reference.SampleAnimation(instance, 0f);
                    var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips == null) continue;

                    var report = $"{Tag} {style}: anchor placement the clips expect, measured from SeatAnchor " +
                                 "(the hips) in bike space, forward is +Z, up is +Y.\n";

                    foreach (var (bone, anchor) in AnchorExpectations)
                    {
                        var t = animator.GetBoneTransform(bone);
                        if (t == null) continue;
                        var d = instance.transform.InverseTransformVector(t.position - hips.position);
                        report += $"    {anchor,-14} x {d.x * 100f,7:0.0}  y {d.y * 100f,7:0.0}  z {d.z * 100f,7:0.0}  (cm)\n";
                    }

                    Debug.Log(report + "    Place each anchor at that offset from SeatAnchor and the authored " +
                              "pose lands on the bike with no reach left over.");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        static readonly (HumanBodyBones bone, string anchor)[] AnchorExpectations =
        {
            (HumanBodyBones.LeftHand,  "GripAnchor_L"),
            (HumanBodyBones.RightHand, "GripAnchor_R"),
            (HumanBodyBones.LeftFoot,  "FootPegGrip_L"),
            (HumanBodyBones.RightFoot, "FootPegGrip_R"),
        };

        [MenuItem("MotoSquid/Animation/Trajectory/Sample All Styles")]
        public static void SampleAll()
        {
            foreach (var style in Styles) Sample(style);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MotoSquid/Animation/Trajectory/Sample SuperSport")]
        public static void SampleSuperSport() { Sample("SuperSport"); AssetDatabase.SaveAssets(); }

        [MenuItem("MotoSquid/Animation/Trajectory/Sample Upright")]
        public static void SampleUpright() { Sample("Upright"); AssetDatabase.SaveAssets(); }

        static void Sample(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning($"{Tag} Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (model == null)
            {
                Debug.LogError($"{Tag} Source model not found at {SourceModel}.");
                return;
            }

            if (!EnsureSourceAvatarMatchesClips(style)) return;

            var clips = GatherOnSeatClips(style, out var skipped);
            if (clips.Count == 0)
            {
                Debug.LogError($"{Tag} {style}: no clips found under {ClipRoot}/{style}.");
                return;
            }

            var reference = clips.FirstOrDefault(c => c.name == ReferenceClip).clip;
            if (reference == null)
            {
                Debug.LogError($"{Tag} {style}: {ReferenceClip} is missing, it defines the reference pose, " +
                               "so nothing can be sampled against it.");
                return;
            }

            var instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError($"{Tag} {SourceModel} is not Humanoid, set Rig > Animation Type = Humanoid first.");
                    return;
                }

                var bones = MappedBones(animator);
                Debug.Log($"{Tag} {style}: {bones.Count} humanoid bone(s) mapped on the source.");

                reference.SampleAnimation(instance, 0f);
                var restPos = new Dictionary<HumanBodyBones, Vector3>();
                foreach (var kv in bones)
                    restPos[kv.Key] = instance.transform.InverseTransformPoint(kv.Value.position);

                var proportions = Measure(bones);
                EnsureFolder($"{OutRoot}/{style}");

                int written = 0, muscleSum = 0, trackSum = 0;
                var poseHandler = new HumanPoseHandler(animator.avatar, instance.transform);
                try
                {
                    for (int i = 0; i < clips.Count; i++)
                    {
                        var (name, clip) = clips[i];
                        if (EditorUtility.DisplayCancelableProgressBar($"Sampling {style}", name, (i + 1f) / clips.Count))
                        {
                            Debug.LogWarning($"{Tag} {style}: cancelled after {written} clip(s).");
                            break;
                        }

                        var asset = Build(clip, name, style, instance, poseHandler, bones, restPos, proportions,
                            out var keptMuscles, out var keptTracks);
                        muscleSum += keptMuscles;
                        trackSum += keptTracks;
                        Write(asset, $"{OutRoot}/{style}/{name}.asset");
                        written++;
                    }
                }
                finally
                {
                    poseHandler.Dispose();
                }

                Debug.Log($"{Tag} {style}: wrote {written} trajectory asset(s) to {OutRoot}/{style}. " +
                          $"Skipped {skipped} off seat clip(s) those stay with the ragdoll. " +
                          $"Source proportions: arm {proportions.arm:0.000} m, leg {proportions.leg:0.000} m, " +
                          $"hip height {proportions.hip:0.000} m. " +
                          $"Moving muscles {muscleSum}/{written * HumanTrait.MuscleCount}, " +
                          $"contact tracks {trackSum}/{written * PositionBones.Length}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(instance);
            }
        }

        static RiderTrajectory Build(AnimationClip clip, string name, string style, GameObject instance,
            HumanPoseHandler poseHandler, Dictionary<HumanBodyBones, Transform> bones,
            Dictionary<HumanBodyBones, Vector3> restPos, (float arm, float leg, float hip) proportions,
            out int keptMuscles, out int keptTracks)
        {
            float rate = Mathf.Max(clip.frameRate, MinSampleRate);
            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * rate) + 1);
            int muscleCount = HumanTrait.MuscleCount;

            var times = new List<float>(frames);
            var muscles = new List<float[]>(frames);
            var bodyPos = new List<Vector3>(frames);
            var bodyRot = new List<Quaternion>(frames);
            var contacts = PositionBones.Where(bones.ContainsKey)
                .ToDictionary(b => b, _ => new List<Vector3>(frames));

            var pose = new HumanPose();
            for (int f = 0; f < frames; f++)
            {
                float t = clip.length * f / (frames - 1f);
                times.Add(t);
                clip.SampleAnimation(instance, t);
                poseHandler.GetHumanPose(ref pose);

                var frame = new float[muscleCount];
                Array.Copy(pose.muscles, frame, Mathf.Min(muscleCount, pose.muscles.Length));
                muscles.Add(frame);
                bodyPos.Add(pose.bodyPosition);

                var q = pose.bodyRotation;
                if (bodyRot.Count > 0 && Quaternion.Dot(bodyRot[bodyRot.Count - 1], q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                bodyRot.Add(q);

                foreach (var kv in contacts)
                    kv.Value.Add(instance.transform.InverseTransformPoint(bones[kv.Key].position) - restPos[kv.Key]);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var asset = ScriptableObject.CreateInstance<RiderTrajectory>();
            asset.clipName        = name;
            asset.style           = style;
            asset.length          = clip.length;
            asset.frameRate       = rate;
            asset.loop            = settings.loopTime;
            asset.sourceArmLength = proportions.arm;
            asset.sourceLegLength = proportions.leg;
            asset.sourceHipHeight = proportions.hip;

            asset.muscles = new AnimationCurve[muscleCount];
            keptMuscles = 0;
            for (int m = 0; m < muscleCount; m++)
            {
                int index = m;
                asset.muscles[m] = Curve(times, muscles.Select(f => f[index]));
                if (!Flatten(asset.muscles[m], MuscleEpsilon)) keptMuscles++;
            }

            asset.bodyPosX = Curve(times, bodyPos.Select(v => v.x));
            asset.bodyPosY = Curve(times, bodyPos.Select(v => v.y));
            asset.bodyPosZ = Curve(times, bodyPos.Select(v => v.z));
            Flatten(asset.bodyPosX, PositionEpsilon);
            Flatten(asset.bodyPosY, PositionEpsilon);
            Flatten(asset.bodyPosZ, PositionEpsilon);

            asset.bodyRotX = Curve(times, bodyRot.Select(q => q.x));
            asset.bodyRotY = Curve(times, bodyRot.Select(q => q.y));
            asset.bodyRotZ = Curve(times, bodyRot.Select(q => q.z));
            asset.bodyRotW = Curve(times, bodyRot.Select(q => q.w));
            Flatten(asset.bodyRotX, RotationEpsilon);
            Flatten(asset.bodyRotY, RotationEpsilon);
            Flatten(asset.bodyRotZ, RotationEpsilon);
            Flatten(asset.bodyRotW, RotationEpsilon);

            var tracks = new List<RiderBoneTrack>(contacts.Count);
            foreach (var kv in contacts)
            {
                var track = new RiderBoneTrack
                {
                    bone = kv.Key,
                    posX = Curve(times, kv.Value.Select(v => v.x)),
                    posY = Curve(times, kv.Value.Select(v => v.y)),
                    posZ = Curve(times, kv.Value.Select(v => v.z)),
                };

                bool flat = Flatten(track.posX, PositionEpsilon) & Flatten(track.posY, PositionEpsilon)
                          & Flatten(track.posZ, PositionEpsilon);
                if (flat && IsConstant(track.posX, PositionEpsilon, 0f)
                         && IsConstant(track.posY, PositionEpsilon, 0f)
                         && IsConstant(track.posZ, PositionEpsilon, 0f)) continue;

                tracks.Add(track);
            }

            asset.tracks = tracks.ToArray();
            keptTracks = tracks.Count;
            return asset;
        }

        const float MuscleEpsilon = 1e-3f;
        const float RotationEpsilon = 1e-4f;
        const float PositionEpsilon = 1e-4f;

        static bool IsConstant(AnimationCurve curve, float epsilon, float about)
        {
            for (int i = 0; i < curve.length; i++)
                if (Mathf.Abs(curve[i].value - about) > epsilon) return false;
            return true;
        }

        static bool Flatten(AnimationCurve curve, float epsilon)
        {
            if (curve == null || curve.length == 0) return true;

            float first = curve[0].value;
            for (int i = 1; i < curve.length; i++)
                if (Mathf.Abs(curve[i].value - first) > epsilon) return false;

            if (curve.length > 2)
            {
                float last = curve[curve.length - 1].time;
                while (curve.length > 1) curve.RemoveKey(curve.length - 1);
                curve.AddKey(new Keyframe(last, first));
            }

            return true;
        }

        static AnimationCurve Curve(List<float> times, IEnumerable<float> values)
        {
            var curve = new AnimationCurve(times.Zip(values, (t, v) => new Keyframe(t, v)).ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        static bool EnsureSourceAvatarMatchesClips(string style)
        {
            var importer = AssetImporter.GetAtPath(SourceModel) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{Tag} {SourceModel} is not a model asset.");
                return false;
            }

            var referencePath = AssetDatabase.FindAssets("t:Model", new[] { $"{ClipRoot}/{style}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == ReferenceClip);

            if (referencePath == null ||
                !(AssetImporter.GetAtPath(referencePath) is ModelImporter reference))
            {
                Debug.LogError($"{Tag} {style}: {ReferenceClip}.fbx not found, it supplies the bone map.");
                return false;
            }

            var wanted = reference.humanDescription.human;
            var current = importer.humanDescription.human ?? new HumanBone[0];
            if (wanted.Length == 0)
            {
                Debug.LogError($"{Tag} {style}: {ReferenceClip}.fbx has no humanoid bone map of its own.");
                return false;
            }

            var have = new HashSet<string>(current.Select(h => h.humanName));
            var missing = wanted.Where(h => !have.Contains(h.humanName)).Select(h => h.humanName).ToArray();
            if (missing.Length == 0) return true;

            var skeleton = new HashSet<string>(
                AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel)
                    .GetComponentsInChildren<Transform>(true).Select(t => t.name));

            var unresolvable = wanted.Where(h => !skeleton.Contains(h.boneName)).Select(h => h.boneName).ToArray();
            if (unresolvable.Length > 0)
            {
                Debug.LogError($"{Tag} {ReferenceClip} maps bones that SKM_Mannequin does not have: " +
                               $"{string.Join(", ", unresolvable)}. The two are not the same skeleton.");
                return false;
            }

            Debug.LogWarning($"{Tag} SKM_Mannequin maps {current.Length} bone(s); {ReferenceClip} maps " +
                             $"{wanted.Length}. Adding the {missing.Length} missing one(s) and reimporting: " +
                             $"{string.Join(", ", missing)}. " +
                             "This rewrites the shared source avatar, VoodooRetargetPipeline's 20 bone map " +
                             "will be replaced, so re-run its step 1 if you go back to that path.");

            var description = importer.humanDescription;
            description.human = wanted;
            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel)?.GetComponent<Animator>()?.avatar;
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError($"{Tag} SKM_Mannequin's avatar did not validate after the remap. " +
                               "Open Rig > Configure and fix it by hand before sampling.");
                return false;
            }

            return true;
        }

        static Dictionary<HumanBodyBones, Transform> MappedBones(Animator animator)
        {
            var bones = new Dictionary<HumanBodyBones, Transform>();
            for (var b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
            {
                var t = animator.GetBoneTransform(b);
                if (t != null) bones[b] = t;
            }

            return bones;
        }

        static (float arm, float leg, float hip) Measure(Dictionary<HumanBodyBones, Transform> bones)
        {
            float arm = Span(bones, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm)
                      + Span(bones, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            float leg = Span(bones, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg)
                      + Span(bones, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            float hip = bones.TryGetValue(HumanBodyBones.Hips, out var hips) ? hips.position.y : 0f;
            return (arm, leg, hip);
        }

        static float Span(Dictionary<HumanBodyBones, Transform> bones, HumanBodyBones a, HumanBodyBones b) =>
            bones.TryGetValue(a, out var ta) && bones.TryGetValue(b, out var tb)
                ? Vector3.Distance(ta.position, tb.position)
                : 0f;

        static List<(string name, AnimationClip clip)> GatherOnSeatClips(string style, out int skipped)
        {
            var clips = new List<(string, AnimationClip)>();
            skipped = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{ClipRoot}/{style}" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_Bike")) continue;
                if (name.StartsWith("AS_Check_Tyre") || OffSeatClips.Contains(name)) { skipped++; continue; }
                if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                    importer.animationType != ModelImporterAnimationType.Human)
                {
                    Debug.LogError($"{Tag} {style}: {name}.fbx is {importer.animationType}, not Humanoid, skipped. " +
                                   "Set Rig > Animation Type = Humanoid on it and re-run, or it will never sample.");
                    skipped++;
                    continue;
                }

                var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                if (clip != null) clips.Add((name, clip));
                else Debug.LogWarning($"{Tag} {style}: {name}.fbx contains no animation clip.");
            }

            return clips;
        }

        static void Write(RiderTrajectory asset, string path)
        {
            asset.name = Path.GetFileNameWithoutExtension(path);

            var existing = AssetDatabase.LoadAssetAtPath<RiderTrajectory>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset);
                return;
            }

            AssetDatabase.CreateAsset(asset, path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
