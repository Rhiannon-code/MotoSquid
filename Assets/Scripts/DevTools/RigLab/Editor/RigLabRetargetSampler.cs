using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabRetargetSampler : EditorWindow
    {
        const string ClipFolder = "Assets/Animations";
        const string OutFolder = "Assets/MotoSquid/Data/RigLab/Tracks";

        // Driven by IK: target position, plus the joint that sets the bend plane.
        static readonly (HumanBodyBones tip, HumanBodyBones hint, string name)[] Chains =
        {
            (HumanBodyBones.LeftHand,  HumanBodyBones.LeftLowerArm,  "LeftHand"),
            (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, "RightHand"),
            (HumanBodyBones.LeftFoot,  HumanBodyBones.LeftLowerLeg,  "LeftFoot"),
            (HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, "RightFoot"),
        };

        static readonly HumanBodyBones[] FreeJoints =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
        };

        string filter = "Long_Weapon,Kick,Get_Hit,Turn_V2";
        Animator rider;
        string status = "";
        Vector2 scroll;

        [MenuItem("Tools/Rig Lab/20. Sample Clips Onto Rider", false, 215)]
        static void Open()
        {
            GetWindow<RigLabRetargetSampler>("Retarget Sampler").minSize = new Vector2(440, 300);
        }

        void OnEnable()
        {
            if (rider != null) return;
            var bike = Object.FindFirstObjectByType<BikeController>();
            if (bike != null) rider = bike.GetComponentInChildren<Animator>(true);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Retarget Sampler", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Plays Humanoid clips on the rider and records the result in HER skeleton.\n" +
                "Clips must be set to Humanoid first, or there is nothing to retarget through.",
                MessageType.None);

            EditorGUILayout.Space();
            rider = (Animator)EditorGUILayout.ObjectField("Rider", rider, typeof(Animator), true);
            filter = EditorGUILayout.TextField("Name filter (comma sep)", filter);

            var clips = Clips();
            EditorGUILayout.LabelField("Matching clips", clips.Count.ToString());

            if (rider != null && !rider.isHuman)
                EditorGUILayout.HelpBox("Rider is not Humanoid, set her Rig to Humanoid first.", MessageType.Error);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(rider == null || !rider.isHuman || clips.Count == 0))
                if (GUILayout.Button("Sample onto rider")) Run(clips);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        List<string> Clips()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) return new List<string>();
            var terms = filter.Split(',').Select(t => t.Trim().ToLower()).Where(t => t.Length > 0).ToList();
            return AssetDatabase.FindAssets("t:Model", new[] { ClipFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => terms.Count == 0 || terms.Any(t => Path.GetFileNameWithoutExtension(p).ToLower().Contains(t)))
                .OrderBy(p => p).ToList();
        }

        void Run(List<string> paths)
        {
            Directory.CreateDirectory(OutFolder);
            var log = new System.Text.StringBuilder();
            int made = 0;

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Sampling onto rider",
                        Path.GetFileNameWithoutExtension(paths[i]), i / (float)paths.Count);
                    if (SampleOne(paths[i], log)) made++;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            status = log.ToString();
            Debug.Log("Rig Lab: sampled " + made + " clip(s) onto " + rider.gameObject.name + "\n\n" + status);
        }

        bool SampleOne(string path, System.Text.StringBuilder log)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) { log.Append("SKIP ").Append(name).Append(" , no clip\n"); return false; }
            if (!clip.isHumanMotion)
            {
                log.Append("SKIP ").Append(name).Append(" , not Humanoid; set its Rig to Humanoid\n");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                PrefabUtility.GetCorrespondingObjectFromSource(rider.gameObject) ?? rider.gameObject);
            if (instance == null) instance = Object.Instantiate(rider.gameObject);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var anim = instance.GetComponent<Animator>();
            foreach (var r in instance.GetComponentsInChildren<UnityEngine.Animations.Rigging.RigBuilder>(true))
                r.enabled = false;   // the rig would overwrite the retargeted pose

            PlayableGraph graph = default;
            try
            {
                anim.applyRootMotion = false;
                graph = PlayableGraph.Create(name + "_sample");
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
                playable.SetApplyFootIK(false);

                float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
                int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);

                var root = instance.transform;
                var track = LoadOrCreate(name);
                track.sourceClips = name + "   retargeted onto " + rider.gameObject.name;
                track.duration = clip.length;

                var keys = new List<PoseTrack.Key>();
                var channels = FreeJoints
                    .Where(b => anim.GetBoneTransform(b) != null)
                    .Select(b => new PoseTrack.BoneChannel
                    {
                        bone = b,
                        local = new Quaternion[frames],
                        root = new Quaternion[frames]
                    })
                    .ToArray();
                var chainKeys = Chains.ToDictionary(c => c.name, c => new Vector3[frames * 2]);
                var chainRots = Chains.ToDictionary(c => c.name, c => new Quaternion[frames]);

                for (int f = 0; f < frames; f++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, Mathf.Min(f / fps, clip.length));
                    AnimationMode.EndSampling();

                    for (int c = 0; c < channels.Length; c++)
                    {
                        var b = anim.GetBoneTransform(channels[c].bone);
                        channels[c].local[f] = b.localRotation;
                        channels[c].root[f] = Quaternion.Inverse(root.rotation) * b.rotation;
                    }

                    foreach (var ch in Chains)
                    {
                        var tip = anim.GetBoneTransform(ch.tip);
                        var hint = anim.GetBoneTransform(ch.hint);
                        var arr = chainKeys[ch.name];
                        arr[f * 2] = tip != null ? root.InverseTransformPoint(tip.position) : Vector3.zero;
                        arr[f * 2 + 1] = hint != null ? root.InverseTransformPoint(hint.position) : Vector3.zero;
                        chainRots[ch.name][f] = tip != null
                            ? Quaternion.Inverse(root.rotation) * tip.rotation : Quaternion.identity;
                    }

                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    keys.Add(new PoseTrack.Key
                    {
                        t = frames == 1 ? 0f : f / (float)(frames - 1),
                        hip = hips != null ? root.InverseTransformPoint(hips.position) : Vector3.zero
                    });
                }

                track.keys = keys.ToArray();
                track.channels = channels;
                track.chains = chainKeys.Select(kv => new PoseTrack.ChainChannel
                {
                    name = kv.Key,
                    points = kv.Value,
                    rotations = chainRots[kv.Key]
                }).ToArray();

                EditorUtility.SetDirty(track);

                float travel = 0f;
                foreach (var ch in track.chains)
                {
                    int n = ch.points.Length / 2;
                    for (int f = 1; f < n; f++)
                        travel = Mathf.Max(travel, Vector3.Distance(ch.points[f * 2], ch.points[0]));
                }

                log.Append(name).Append("  ").Append(frames).Append(" frames, ")
                   .Append(channels.Length).Append(" joint channels, ")
                   .Append(track.chains.Length).Append(" IK chains, peak travel ")
                   .Append(travel.ToString("0.000")).Append("m");
                if (travel < 0.01f) log.Append("   <-- NOTHING MOVED, the clip did not drive the rider");
                log.Append('\n');
                return true;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(instance);
            }
        }

        static PoseTrack LoadOrCreate(string name)
        {
            string path = OutFolder + "/" + name + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<PoseTrack>(path);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<PoseTrack>();
            AssetDatabase.CreateAsset(t, path);
            return t;
        }
    }
}
