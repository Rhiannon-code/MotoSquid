using MotoSquid.Bike;
using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabFullSampler : EditorWindow
    {
        const string ClipFolder = "Assets/Animations";
        const string OutFolder = "Assets/MotoSquid/Data/RigLab/FullPoses";

        string filter = "Long_Weapon,Kick,Get_Hit,Turn_V2";
        Animator rider;
        string status = "";
        Vector2 scroll;

        [MenuItem("Tools/Rig Lab/22. Full Skeleton Sample + Verify", false, 220)]
        static void Open()
        {
            GetWindow<RigLabFullSampler>("Full Sampler").minSize = new Vector2(460, 340);
        }

        void OnEnable()
        {
            if (rider != null) return;
            var bike = Object.FindFirstObjectByType<BikeController>();
            if (bike != null) rider = bike.GetComponentInChildren<Animator>(true);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Full Skeleton Sampler", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Records every transform under the rider, every frame, in her own skeleton.\n" +
                "Then replays each track and reports the worst bone error, if that is not ~0 the " +
                "capture is wrong and nothing downstream can be trusted.", MessageType.None);

            EditorGUILayout.Space();
            rider = (Animator)EditorGUILayout.ObjectField("Rider", rider, typeof(Animator), true);
            filter = EditorGUILayout.TextField("Name filter (comma sep)", filter);

            var clips = Clips();
            EditorGUILayout.LabelField("Matching clips", clips.Count.ToString());
            if (rider != null && !rider.isHuman)
                EditorGUILayout.HelpBox("Rider is not Humanoid.", MessageType.Error);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(rider == null || !rider.isHuman || clips.Count == 0))
                if (GUILayout.Button("Sample and verify")) Run(clips);

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
            int ok = 0, bad = 0;

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Full sample", Path.GetFileNameWithoutExtension(paths[i]),
                                                     i / (float)paths.Count);
                    float err;
                    if (!SampleOne(paths[i], log, out err)) continue;
                    if (err < 0.002f) ok++; else bad++;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Insert(0, "verified " + ok + " clip(s), " + bad + " with error above 2mm\n\n");
            status = log.ToString();
            Debug.Log("Rig Lab full sample\n\n" + status);
        }

        bool SampleOne(string path, System.Text.StringBuilder log, out float worstError)
        {
            worstError = 0f;
            string name = Path.GetFileNameWithoutExtension(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null || !clip.isHumanMotion)
            {
                log.Append("SKIP ").Append(name).Append(clip == null ? ", no clip\n" : ", not Humanoid\n");
                return false;
            }

            var instance = Spawn();
            var anim = instance.GetComponent<Animator>();
            var root = instance.transform;

            var bones = root.GetComponentsInChildren<Transform>(true)
                            .Where(t => t != root)
                            .ToArray();
            var paths = bones.Select(b => PathOf(root, b)).ToArray();

            PlayableGraph graph = default;
            try
            {
                anim.applyRootMotion = false;
                graph = PlayableGraph.Create(name + "_full");
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
                playable.SetApplyFootIK(false);

                float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
                int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);

                var rot = new Quaternion[frames * bones.Length];
                var pos = new Vector3[frames * bones.Length];
                var worldCheck = new Vector3[frames * bones.Length];

                for (int f = 0; f < frames; f++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, Mathf.Min(f / fps, clip.length));
                    AnimationMode.EndSampling();

                    for (int b = 0; b < bones.Length; b++)
                    {
                        int k = f * bones.Length + b;
                        rot[k] = bones[b].localRotation;
                        pos[k] = bones[b].localPosition;
                        worldCheck[k] = root.InverseTransformPoint(bones[b].position);
                    }
                }

                var track = LoadOrCreate(name);
                track.sourceClip = name;
                track.sampledOn = rider.gameObject.name;
                track.frameCount = frames;
                track.duration = clip.length;
                track.frameRate = fps;
                track.bonePaths = paths;
                track.rotations = rot;
                track.positions = pos;
                EditorUtility.SetDirty(track);

                graph.Destroy();
                graph = default;

                for (int f = 0; f < frames; f++)
                {
                    for (int b = 0; b < bones.Length; b++)
                    {
                        int k = f * bones.Length + b;
                        bones[b].localRotation = rot[k];
                        bones[b].localPosition = pos[k];
                    }
                    for (int b = 0; b < bones.Length; b++)
                    {
                        int k = f * bones.Length + b;
                        float e = Vector3.Distance(root.InverseTransformPoint(bones[b].position), worldCheck[k]);
                        if (e > worstError) worstError = e;
                    }
                }

                float travel = 0f;
                for (int b = 0; b < bones.Length; b++)
                    for (int f = 1; f < frames; f++)
                        travel = Mathf.Max(travel, Vector3.Distance(worldCheck[f * bones.Length + b], worldCheck[b]));

                log.Append(name).Append("  ").Append(frames).Append(" frames x ").Append(bones.Length)
                   .Append(" bones   travel ").Append(travel.ToString("0.000"))
                   .Append("m   replay error ").Append((worstError * 1000f).ToString("0.00")).Append("mm")
                   .Append(worstError < 0.002f ? "   OK" : "   <-- CAPTURE IS WRONG").Append('\n');
                return true;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(instance);
            }
        }

        GameObject Spawn()
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(rider.gameObject) ?? rider.gameObject;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src) ?? Object.Instantiate(rider.gameObject);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<RigBuilder>(true)) r.enabled = false;
            foreach (var r in go.GetComponentsInChildren<Rig>(true)) r.weight = 0f;
            return go;
        }

        static string PathOf(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static FullPose LoadOrCreate(string name)
        {
            string p = OutFolder + "/" + name + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<FullPose>(p);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<FullPose>();
            AssetDatabase.CreateAsset(t, p);
            return t;
        }
    }
}
