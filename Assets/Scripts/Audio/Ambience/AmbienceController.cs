using MotoSquid.Bike;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Audio
{
    public class AmbienceController : MonoBehaviour
    {
        public static AmbienceController Instance { get; private set; }

        [Serializable]
        public class Layer
        {
            public string     name = "Layer";
            public AudioClip  clip;
            [Range(0f, 1f)] public float volume = 0.3f;
            [Range(0.5f, 1.5f)] public float pitch = 1f;
            // Ambience is a 2D bed by default. Push toward 1 and parent the controller to a location
            // for a positional emitter (a generator hum, a crowd stand)
            [Range(0f, 1f)] public float spatialBlend = 0f;
            // Offset each loop's start so several copies of the same clip never phase together
            public bool randomStartOffset = true;

            [NonSerialized] public AudioSource source;
            [NonSerialized] public float       fadeVel;
        }

        [Serializable]
        public class Bed
        {
            public string  name = "Default";
            public Layer[] layers = new Layer[0];
        }

        [Header("Beds")]
        public Bed[] beds = new Bed[0];
        public string defaultBed = "Default";

        [Header("Output")]
        public AudioMixerGroup output;          // World SFX on MainMixer
        [Range(0f, 1f)] public float masterVolume = 1f;
        public float crossfadeTime = 1.5f;

        [Header("Duck under the engine")]
        // The bed is there to fill the silence at low speed. Once the player is moving the engine
        // Owns the mix, so pull the ambience down instead of letting it mask the bike
        public bool  duckWithSpeed   = true;
        public float duckStartSpeed  = 40f;     // Km/h
        public float duckFullSpeed   = 120f;    // Km/h
        [Range(0f, 1f)] public float duckedVolume = 0.35f;

        readonly List<(string bed, Component owner)> _zoneStack = new();
        string _activeBed;
        float  _duck = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance  = this;
            _activeBed = defaultBed;

            foreach (var bed in beds)
            {
                if (bed?.layers == null) continue;
                foreach (var layer in bed.layers)
                    BuildSource(bed, layer);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (duckWithSpeed)
            {
                var local = BikeAudioController.LocalPlayer;
                float t = local != null
                    ? Mathf.Clamp01(Mathf.InverseLerp(duckStartSpeed, duckFullSpeed, local.SpeedKmh))
                    : 0f;
                _duck = Mathf.Lerp(1f, duckedVolume, t);
            }
            else _duck = 1f;

            float fade = Mathf.Max(crossfadeTime, 0.01f);

            foreach (var bed in beds)
            {
                if (bed?.layers == null) continue;
                bool active = bed.name == _activeBed;

                foreach (var layer in bed.layers)
                {
                    if (layer?.source == null) continue;
                    float target = active ? layer.volume * masterVolume * _duck : 0f;

                    // A bed only holds voices while it is audible, start it as it fades in, stop it
                    // once it has faded fully out
                    if (target > 0f && !layer.source.isPlaying) StartLayer(layer);

                    layer.source.volume = Mathf.SmoothDamp(layer.source.volume, target, ref layer.fadeVel, fade);

                    if (!active && layer.source.isPlaying && layer.source.volume <= 0.001f)
                    {
                        layer.source.volume = 0f;
                        layer.source.Stop();
                    }
                }
            }
        }

        // Bed control

        public void PlayBed(string bedName)
        {
            _activeBed = string.IsNullOrEmpty(bedName) ? defaultBed : bedName;
        }

        public void PlayDefaultBed() => PlayBed(defaultBed);

        // Zones can overlap (a tunnel inside a city section), so they stack: the most recently
        // entered zone wins and leaving it falls back to the one underneath, not straight to default
        public void PushZone(string bedName, Component owner)
        {
            if (owner == null) return;
            _zoneStack.RemoveAll(e => e.owner == owner);
            _zoneStack.Add((bedName, owner));
            ResolveZone();
        }

        public void PopZone(Component owner)
        {
            _zoneStack.RemoveAll(e => e.owner == owner || e.owner == null);
            ResolveZone();
        }

        void ResolveZone()
        {
            PlayBed(_zoneStack.Count > 0 ? _zoneStack[_zoneStack.Count - 1].bed : defaultBed);
        }

        // Source setup

        void BuildSource(Bed bed, Layer layer)
        {
            if (layer == null || layer.clip == null) return;

            var go = new GameObject($"Ambience_{bed.name}_{layer.name}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.clip         = layer.clip;
            src.loop         = true;
            src.playOnAwake  = false;
            src.volume       = 0f;                 // Everything fades up from silence in Update
            src.pitch        = layer.pitch;
            src.spatialBlend = layer.spatialBlend;
            src.rolloffMode  = AudioRolloffMode.Logarithmic;
            if (output != null) src.outputAudioMixerGroup = output;

            layer.source = src;
        }

        static void StartLayer(Layer layer)
        {
            if (layer.randomStartOffset && layer.clip != null)
                layer.source.time = UnityEngine.Random.Range(0f, layer.clip.length);
            layer.source.Play();
        }
    }
}
