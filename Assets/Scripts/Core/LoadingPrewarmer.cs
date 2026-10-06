using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MotoSquid.Core
{
    public class LoadingPrewarmer : MonoBehaviour
    {
        [Header("Shaders (optional)")]
        public ShaderVariantCollection shaderVariants;
        public int variantsPerFrame = 12;

        [Header("Audio")]
        public AudioClip[] preloadClips;
        public UnityEngine.Object[] preloadAudioFrom;
        public int clipsPerFrame = 4;

        [Header("Memory")]
        public bool collectGarbageWhenDone = true;
        public bool Finished { get; private set; }
        public float Progress => Finished ? 1f : _total > 0 ? (float)_done / _total : 0f;
        int _done, _total = 2;

        public IEnumerator PrewarmAsync()
        {
            yield return WarmShaders();
            _done++;
            yield return WarmAudio();
            yield return SettleMemory();
            _done++;
            Finished = true;
        }

        IEnumerator WarmShaders()
        {
            // Silent when unassigned: this is opt-in now, not something to be nagged about
            if (shaderVariants == null) yield break;

            if (shaderVariants.variantCount == 0)
            {
                Debug.LogWarning("[LoadingPrewarmer] The variant collection is empty. Record one from a " +
                                 "full race, a collection captured in the menus holds only UI shaders.", this);
                yield break;
            }

            int done = 0;
            while (shaderVariants.WarmUpProgressively(Mathf.Max(1, variantsPerFrame)))
            {
                done += variantsPerFrame;
                yield return null;
            }

            Debug.Log($"[LoadingPrewarmer] Warmed {shaderVariants.variantCount} shader variant(s) over " +
                      $"~{Mathf.CeilToInt(done / (float)Mathf.Max(1, variantsPerFrame))} frame(s).", this);
        }

        IEnumerator WarmAudio()
        {
            var clips = new System.Collections.Generic.HashSet<AudioClip>();

            if (preloadClips != null)
                foreach (var clip in preloadClips)
                    if (clip != null) clips.Add(clip);

            // Walking AudioSource.clip alone finds almost nothing here: the engine, skid, landing, combat
            // and character-voice clips are all serialized fields on components (EngineAudio.onClip,
            // BikeAudioController.hitImpactClips, CharacterVoice.startTaunt and so on), never assigned to
            // a source until the moment they play. Every AudioClip field and array is read instead.
            if (preloadAudioFrom != null)
                foreach (var source in preloadAudioFrom)
                    CollectFrom(source, clips);

            if (clips.Count == 0) yield break;
            _total += clips.Count;

            int loaded = 0, inFrame = 0;
            foreach (var clip in clips)
            {
                if (clip == null) continue;

                // Decompress-on-load clips stall on first Play otherwise, and an engine loop starting as a
                // rival comes into earshot is exactly when that lands
                if (clip.loadState != AudioDataLoadState.Loaded && clip.LoadAudioData()) loaded++;
                _done++;

                if (++inFrame >= Mathf.Max(1, clipsPerFrame)) { inFrame = 0; yield return null; }
            }

            Debug.Log($"[LoadingPrewarmer] Preloaded {loaded} of {clips.Count} audio clip(s).", this);
        }

        static readonly System.Collections.Generic.Dictionary<System.Type, FieldInfo[]> s_clipFields =
            new System.Collections.Generic.Dictionary<System.Type, FieldInfo[]>();

        const BindingFlags FieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Takes whatever is dropped in: a prefab is walked component by component, a ScriptableObject
        // (the CharacterVoice assets hold the eight bark arrays) is read directly, a clip is itself.
        static void CollectFrom(UnityEngine.Object source,
                                System.Collections.Generic.HashSet<AudioClip> into)
        {
            switch (source)
            {
                case null:
                    return;
                case AudioClip clip:
                    into.Add(clip);
                    return;
                case GameObject go:
                    foreach (var component in go.GetComponentsInChildren<Component>(true))
                        ReadClipFields(component, into);
                    return;
                default:
                    ReadClipFields(source, into);
                    return;
            }
        }

        static void ReadClipFields(UnityEngine.Object obj,
                                   System.Collections.Generic.HashSet<AudioClip> into)
        {
            if (obj == null) return;

            if (obj is AudioSource source && source.clip != null) into.Add(source.clip);

            var type = obj.GetType();
            if (!s_clipFields.TryGetValue(type, out FieldInfo[] fields))
            {
                var found = new System.Collections.Generic.List<FieldInfo>();
                foreach (var f in type.GetFields(FieldFlags))
                    if (f.FieldType == typeof(AudioClip) || f.FieldType == typeof(AudioClip[]))
                        found.Add(f);
                fields = found.ToArray();
                s_clipFields[type] = fields;
            }

            foreach (var field in fields)
            {
                object value = field.GetValue(obj);
                if (value is AudioClip clip) { if (clip != null) into.Add(clip); }
                else if (value is AudioClip[] array)
                    foreach (var c in array) if (c != null) into.Add(c);
            }
        }

        // The managed heap has just churned through a scene load and a pool prewarm. Collecting here
        // spends the pause on a screen that is already waiting, instead of on the first corner.
        IEnumerator SettleMemory()
        {
            if (!collectGarbageWhenDone) yield break;

            // No Resources.UnloadUnusedAssets here: async operations complete in order, and the race scene is
            // held at 90% until this screen finishes, so the unload queued behind it and never completed. The
            // loading screen then sat on its 30 s timeout. SceneLoader unloads once the scene has activated
            System.GC.Collect();
            yield return null;

            Debug.Log("[LoadingPrewarmer] Garbage collected while the loading screen is still up.", this);
        }
    }
}
