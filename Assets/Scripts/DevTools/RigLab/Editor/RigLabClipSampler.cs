using MotoSquid.Rider;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabClipSampler : EditorWindow
    {
        const string ClipFolder = "Assets/Animations";
        const string Root = "Root_M";

        string filter = "";
        bool includeDigits;
        string status = "";
        Vector2 scroll;

        static string OutDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../RigLabData")); }
        }

        [MenuItem("Tools/Rig Lab/12. Sample Animation Clips", false, 140)]
        static void Open()
        {
            GetWindow<RigLabClipSampler>("Clip Sampler").minSize = new Vector2(430, 300);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Clip Sampler", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Every frame of every matching clip, whole skeleton.\n" +
                "Recorded in BIKE space (shows body shift) and hip space (shows limb motion).\n" +
                "Writes frames.csv (per frame data) and summary.csv (per bone travel) to\n" +
                OutDir, MessageType.None);

            EditorGUILayout.Space();
            filter = EditorGUILayout.TextField("Name filter", filter);
            includeDigits = EditorGUILayout.Toggle("Include fingers/toes", includeDigits);

            var paths = Paths();
            EditorGUILayout.LabelField("Matching clips", paths.Count.ToString());

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(paths.Count == 0))
                if (GUILayout.Button("Sample every frame")) Run(paths);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        List<string> Paths()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) return new List<string>();
            return AssetDatabase.FindAssets("t:Model", new[] { ClipFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.IsNullOrEmpty(filter) ||
                            Path.GetFileNameWithoutExtension(p).ToLower().Contains(filter.ToLower()))
                .OrderBy(p => p).ToList();
        }

        void Run(List<string> paths)
        {
            Directory.CreateDirectory(OutDir);
            var frames = new StringBuilder("clip,frameRate,frame,time,bone,bx,by,bz,hx,hy,hz,rx,ry,rz\n");
            var summary = new StringBuilder("clip,bone,bikeTravel,bikeRangeX,bikeRangeY,bikeRangeZ,hipTravel,peakFrame,restBX,restBY,restBZ\n");
            var log = new StringBuilder();
            int total = 0;

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Sampling clips",
                        Path.GetFileNameWithoutExtension(paths[i]), i / (float)paths.Count);
                    total += SampleOne(paths[i], frames, summary, log);
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
            }

            File.WriteAllText(Path.Combine(OutDir, "frames.csv"), frames.ToString());
            File.WriteAllText(Path.Combine(OutDir, "summary.csv"), summary.ToString());

            status = log.ToString();
            Debug.Log("Rig Lab: sampled " + paths.Count + " clip(s), " + total + " frame rows -> " + OutDir + "\n\n" + status);
        }

        int SampleOne(string path, StringBuilder frames, StringBuilder summary, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            string name = Path.GetFileNameWithoutExtension(path);
            if (prefab == null || clip == null) { log.Append("SKIP ").Append(name).Append(", no clip\n"); return 0; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                Transform root = null;
                var bones = new List<Transform>();
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == Root) root = t;
                    if (!includeDigits && IsDigit(t.name)) continue;
                    bones.Add(t);
                }
                if (root == null) { log.Append("SKIP ").Append(name).Append(", no ").Append(Root).Append('\n'); return 0; }

                float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
                int frameCount = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);

                var rest = new Dictionary<Transform, Vector3>();
                var min = new Dictionary<Transform, Vector3>();
                var max = new Dictionary<Transform, Vector3>();
                var peak = new Dictionary<Transform, KeyValuePair<float, int>>();

                var bike = instance.transform;
                var restHip = new Dictionary<Transform, Vector3>();
                var peakHip = new Dictionary<Transform, float>();

                AnimationMode.SampleAnimationClip(instance, clip, 0f);
                foreach (var b in bones)
                {
                    var p = bike.InverseTransformPoint(b.position);
                    rest[b] = p; min[b] = p; max[b] = p;
                    peak[b] = new KeyValuePair<float, int>(0f, 0);
                    restHip[b] = root.InverseTransformPoint(b.position);
                    peakHip[b] = 0f;
                }

                int rows = 0;
                for (int f = 0; f < frameCount; f++)
                {
                    float time = Mathf.Min(f / fps, clip.length);
                    AnimationMode.SampleAnimationClip(instance, clip, time);

                    foreach (var b in bones)
                    {
                        var p = bike.InverseTransformPoint(b.position);
                        var h = root.InverseTransformPoint(b.position);
                        var r = (Quaternion.Inverse(bike.rotation) * b.rotation).eulerAngles;

                        frames.Append(name).Append(',').Append(fps.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                              .Append(f).Append(',').Append(time.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                              .Append(b.name).Append(',')
                              .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                              .Append(F(h.x)).Append(',').Append(F(h.y)).Append(',').Append(F(h.z)).Append(',')
                              .Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.z)).Append('\n');
                        rows++;

                        min[b] = Vector3.Min(min[b], p);
                        max[b] = Vector3.Max(max[b], p);
                        float d = (p - rest[b]).magnitude;
                        if (d > peak[b].Key) peak[b] = new KeyValuePair<float, int>(d, f);
                        float dh = (h - restHip[b]).magnitude;
                        if (dh > peakHip[b]) peakHip[b] = dh;
                    }
                }

                foreach (var b in bones)
                {
                    var range = max[b] - min[b];
                    summary.Append(name).Append(',').Append(b.name).Append(',')
                           .Append(F(peak[b].Key)).Append(',')
                           .Append(F(range.x)).Append(',').Append(F(range.y)).Append(',').Append(F(range.z)).Append(',')
                           .Append(F(peakHip[b])).Append(',').Append(peak[b].Value).Append(',')
                           .Append(F(rest[b].x)).Append(',').Append(F(rest[b].y)).Append(',').Append(F(rest[b].z)).Append('\n');
                }

                var movers = bones.Where(b => peak[b].Key > 0.01f)
                                  .OrderByDescending(b => peak[b].Key).Take(6).ToList();
                log.Append('\n').Append(name)
                   .Append("  ").Append(frameCount).Append(" frames @ ").Append(fps.ToString("0.#")).Append("fps")
                   .Append("  len ").Append(clip.length.ToString("0.00")).Append("s\n");
                if (movers.Count == 0) log.Append("   (nothing moves more than 1cm)\n");
                foreach (var b in movers)
                    log.Append("   ").Append(b.name.PadRight(16))
                       .Append(" travel ").Append(peak[b].Key.ToString("0.000"))
                       .Append(" peak@f").Append(peak[b].Value).Append('\n');

                return rows;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static bool IsDigit(string n)
        {
            return n.Contains("Finger") || n.Contains("Thumb") || n.Contains("Pinky")
                || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring")
                || n.StartsWith("Toes") || n.Contains("Cup");
        }

        static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }
    }
}
