using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Rider
{
    public class RiderClipPreview : EditorWindow
    {
        const string RiderModelName = "VoodooRigged";
        const string ClipRoot = "Assets/Animations/Test";

        static readonly string[] Styles = { "SuperSport", "Upright" };

        GameObject _rig;
        Animator _animator;
        readonly List<(string name, AnimationClip clip, string style, bool tdof)> _clips = new();
        int _styleIndex;
        int _clipIndex;
        float _time;
        bool _pendingSpawn, _pendingClear;
        string[] _clipLabels = System.Array.Empty<string>();

        [MenuItem("MotoSquid/Animation/Rider Clip Preview")]
        public static void Open() => GetWindow<RiderClipPreview>("Rider Clips").minSize = new Vector2(340, 220);

        void OnDisable() => Clear();

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Rider only, no bike, no IK. A bad joint here is the rig or the clip; a bad joint " +
                "only once mounted is the IK or the anchors.", MessageType.None);

            using (new EditorGUI.DisabledScope(_rig != null))
                if (GUILayout.Button("Spawn preview rig", GUILayout.Height(24)))
                    _pendingSpawn = true;

            using (new EditorGUI.DisabledScope(_rig == null))
                if (GUILayout.Button("Remove preview rig"))
                    _pendingClear = true;

            if (_rig == null)
            {
                EditorGUILayout.LabelField("No preview rig in the scene.", EditorStyles.miniLabel);
                Defer();
                return;
            }

            int style = EditorGUILayout.Popup("Style", _styleIndex, Styles);
            if (style != _styleIndex) { _styleIndex = style; Reload(); }

            if (_clipLabels.Length == 0)
            {
                EditorGUILayout.HelpBox($"No Humanoid clips found under {ClipRoot}/{Styles[_styleIndex]}.",
                    MessageType.Warning);
                Defer();
                return;
            }

            int clip = EditorGUILayout.Popup("Clip", Mathf.Clamp(_clipIndex, 0, _clipLabels.Length - 1), _clipLabels);
            if (clip != _clipIndex) { _clipIndex = clip; _time = 0f; }

            var current = _clips[_clipIndex];
            float length = Mathf.Max(current.clip.length, 0.0001f);
            _time = EditorGUILayout.Slider("Time", Mathf.Clamp(_time, 0f, length), 0f, length);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start")) _time = 0f;
                if (GUILayout.Button("Middle")) _time = length * 0.5f;
                if (GUILayout.Button("End")) _time = length;
            }

            bool rigTdof = TranslationDoF(AssetDatabase.GetAssetPath(_animator.avatar));
            if (current.tdof != rigTdof)
            {
                EditorGUILayout.HelpBox(
                    $"Translation DoF mismatch: '{current.name}' is {(current.tdof ? "on" : "off")} but " +
                    $"{RiderModelName} is {(rigTdof ? "on" : "off")}. Translation muscles are what displace " +
                    "bones off the skeleton, make both agree before judging a pose.", MessageType.Error);
            }

            EditorGUILayout.LabelField($"{current.clip.length:0.00} s · {current.clip.frameRate:0} fps · " +
                                       $"{(current.clip.isLooping ? "looping" : "one shot")}", EditorStyles.miniLabel);

            Sample(current.clip);
            Defer();
        }

        void Defer()
        {
            if (_pendingSpawn) { _pendingSpawn = false; EditorApplication.delayCall += Spawn; }
            if (_pendingClear) { _pendingClear = false; EditorApplication.delayCall += Clear; }
        }

        void Spawn()
        {
            var path = AssetDatabase.FindAssets($"{RiderModelName} t:Model")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == RiderModelName);

            var model = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"[ClipPreview] {RiderModelName}.fbx not found under Assets.");
                return;
            }

            _rig = Instantiate(model);
            _rig.name = $"{RiderModelName} (clip preview)";
            _rig.hideFlags = HideFlags.DontSave;
            _animator = _rig.GetComponent<Animator>();

            if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman)
            {
                Debug.LogError($"[ClipPreview] {RiderModelName} is not Humanoid, set Rig > Animation Type = Humanoid.");
                Clear();
                return;
            }

            Selection.activeGameObject = _rig;
            SceneView.lastActiveSceneView?.FrameSelected();
            Reload();
            Repaint();
        }

        void Clear()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (_rig != null) DestroyImmediate(_rig);
            _rig = null;
            _animator = null;
            _clips.Clear();
            _clipLabels = System.Array.Empty<string>();
        }

        void Reload()
        {
            _clips.Clear();
            _clipIndex = 0;
            _time = 0f;

            var folder = $"{ClipRoot}/{Styles[_styleIndex]}";
            if (!AssetDatabase.IsValidFolder(folder)) { _clipLabels = System.Array.Empty<string>(); return; }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_Bike")) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter imp) ||
                    imp.animationType != ModelImporterAnimationType.Human) continue;

                var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                if (clip != null) _clips.Add((name, clip, Styles[_styleIndex], imp.humanDescription.hasTranslationDoF));
            }

            _clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _clipLabels = _clips.Select(c => c.name).ToArray();
        }

        void Sample(AnimationClip clip)
        {
            if (_rig == null) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_rig, clip, _time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        static bool TranslationDoF(string avatarPath) =>
            AssetImporter.GetAtPath(avatarPath) is ModelImporter imp && imp.humanDescription.hasTranslationDoF;
    }
}
