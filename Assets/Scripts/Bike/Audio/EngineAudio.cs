using MotoSquid.Audio;
using MotoSquid.Cameras;
using MotoSquid.DevTools;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public class EngineAudio : MonoBehaviour, IEngineAudio
    {
        [Header("Engine loops")]
        public AudioClip onClip;    
        public AudioClip offClip;   

        [Header("First person (helmet)")]
        public AudioClip onHelmetClip;
        public AudioClip offHelmetClip;
        public CameraController cameraController;  

        [Header("RPM (from road speed/free rev)")]
        public float idleRpm01   = 0.14f;
        public float revCeiling01 = 0.9f;
        public float burnoutRpm01 = 0.75f;
        public float rpmRise = 2.5f;
        public float rpmFall = 2.0f;

        [Header("Engine braking (off throttle at speed)")]
        public float loadSmooth = 4f;  
        public float engineBrakeRpm = 0.4f;

        [Header("Pitch (idle → redline)")]
        public float minPitch = 0.9f;
        public float maxPitch = 1.4f;
        // Same clips at the same speed would play identical, phase-aligned loops that comb-filter when two bikes are close
        [Range(0f, 0.1f)] public float pitchDetune = 0.025f;

        [Header("Levels / 3D / output")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        public float unmuteFadeTime = 1f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 5f;
        public float maxDistance = 120f;
        // A point source pans hard and reads as a speaker. Some spread gives the engine a body
        public float spread      = 28f;
        public float localPlayerSpread = 70f;
        public AudioMixerGroup output;

        [Header("Player")]
        public bool isLocalPlayer = false;
        [Range(0f, 1f)] public float localPlayerSpatialBlend = 0.6f;

        private IBikeAudioState _state;
        private AudioSource _on, _off;
        private float _rpm01, _load, _detune = 1f;
        private float _muteFactor = 0f, _muteTarget = 0f;
        private bool  _helmet, _cameraSearched;

        public float EngineRpm01     => _rpm01;
        public bool  EngineAtLimiter => _rpm01 >= revCeiling01 - 0.02f;

        public void SetEngineMuted(bool muted) { _muteTarget = muted ? 0f : 1f; if (muted) _muteFactor = 0f; }
        public void SilenceEngine() { if (_on != null) _on.volume = 0f; if (_off != null) _off.volume = 0f; }
        public bool IsFirstPersonView => _helmet;

        private void Awake()
        {
            _state = GetComponentInParent<IBikeAudioState>();
            _on  = CreateSource("Engine_On");  _on.clip  = onClip;
            _off = CreateSource("Engine_Off"); _off.clip = offClip;
            _rpm01 = idleRpm01;
            _detune = 1f + Random.Range(-pitchDetune, pitchDetune);
            PlayFromRandomPoint(_on);
            PlayFromRandomPoint(_off);
        }

        private void OnEnable()
        {
            if (_state == null) _state = GetComponentInParent<IBikeAudioState>();
            if (_state != null) _state.EngineMuteRequested += OnMute;
        }
        private void OnDisable() { if (_state != null) _state.EngineMuteRequested -= OnMute; }
        private void OnMute(bool muted) => SetEngineMuted(muted);

        private void Update()
        {
            if (_state == null || _on == null) return;

            // First person (helmet) clip set, local player only, follows the camera view
            if (isLocalPlayer)
            {
                if (cameraController == null && !_cameraSearched)
                {
                    _cameraSearched = true;
                    // CameraController unparents itself to the scene root in Awake, so it is never
                    // a child of the bike by the time this runs. Match on its Bike instead
                    var bike = GetComponentInParent<BikeController>();
                    if (bike != null)
                        foreach (var cc in FindObjectsByType<CameraController>(
                                     FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (cc.Bike == bike) { cameraController = cc; break; }
                }
                if (cameraController != null) _helmet = cameraController.IsFirstPersonView;
                SwapClip(_on,  _helmet && onHelmetClip  != null ? onHelmetClip  : onClip);
                SwapClip(_off, _helmet && offHelmetClip != null ? offHelmetClip : offClip);
            }

            // RPM, from road speed, capped below redline, open drivetrain free rev on the grid/burnout
            float speedKmh = Mathf.Abs(_state.SpeedMs) * 3.6f;
            float target   = GearNorm(speedKmh) * revCeiling01;
            if (!_state.CanMove || _state.IsDoingBurnout || _state.IsRevving)
            {
                // A burnout is a pinned throttle against a held brake, so it should hit the limiter
                // exactly like revving on the grid does. It used to sit on a flat burnoutRpm01 (0.75)
                // which is below the limiter threshold (revCeiling01 - 0.02), so it never bounced.
                // burnoutRpm01 stays as a floor, so a part-throttle trigger still sounds like a burnout
                float rev = Mathf.Lerp(idleRpm01, revCeiling01, _state.ThrottleInput);
                if (_state.IsDoingBurnout) rev = Mathf.Max(rev, burnoutRpm01);
                target = Mathf.Max(target, rev);
            }
            target = Mathf.Max(idleRpm01, target);
            float rate = target > _rpm01 ? rpmRise : rpmFall;
            _rpm01 = Mathf.MoveTowards(_rpm01, Mathf.Clamp01(target), rate * Time.deltaTime);

            // Throttle load, smoothed, drives the accel/coast blend
            _load = Mathf.MoveTowards(_load, Mathf.Clamp01(_state.ThrottleInput), loadSmooth * Time.deltaTime);

            _muteFactor = Mathf.MoveTowards(_muteFactor, _muteTarget, Time.deltaTime / Mathf.Max(0.01f, unmuteFadeTime));
            float master = masterVolume * Mathf.SmoothStep(0f, 1f, _muteFactor);
            float pitch  = Mathf.Lerp(minPitch, maxPitch, Mathf.Clamp01(_rpm01)) * _detune;
            float offGate   = Mathf.Clamp01(Mathf.InverseLerp(engineBrakeRpm, engineBrakeRpm + 0.2f, _rpm01));
            float offWeight = (1f - _load) * offGate;

            _on.volume = master * Mathf.Cos(offWeight * Mathf.PI * 0.5f);
            _on.pitch  = pitch;
            AudioLoop.EnsurePlaying(_on, "engine on-throttle restart", this);

            if (_off != null && _off.clip != null)
            {
                _off.volume = master * Mathf.Sin(offWeight * Mathf.PI * 0.5f);
                _off.pitch  = pitch;
                AudioLoop.EnsurePlaying(_off, "engine off-throttle restart", this);
            }
        }

        // Swap a source's clip, keeping playback position so the exterior/helmet switch is seamless
        private static void SwapClip(AudioSource s, AudioClip clip)
        {
            if (s == null || clip == null || s.clip == clip) return;
            float t = s.time;
            s.clip = clip;
            s.time = Mathf.Clamp(t, 0f, clip.length - 0.01f);
            s.Play();
            AudioBurstLog.Note($"engine clip swap ({clip.name})", s);
        }

        private static void PlayFromRandomPoint(AudioSource s)
        {
            if (s.clip == null) return;
            s.time = Random.Range(0f, s.clip.length - 0.01f);
            s.Play();
        }

        private float GearNorm(float speedKmh)
        {
            int[] gs = _state.GearSpeeds;
            if (gs == null || gs.Length == 0) return 0f;
            int   gear = _state.CurrentGear;
            int   gi   = Mathf.Clamp(gear - 1, 0, gs.Length - 1);
            float top  = gear > gs.Length ? _state.MaxSpeedKmh : gs[gi];
            return top > 0f ? Mathf.Clamp01(speedKmh / top) : 0f;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop         = true;
            s.playOnAwake  = false;
            s.volume       = 0f;   // Start silent, Update ramps it, avoids a full volume burst on the first frame
            s.spatialBlend = isLocalPlayer ? localPlayerSpatialBlend : spatialBlend;
            // Raise the player only. Lowering everyone else below the default 128 made them lose to every
            // wind, skid and ambience source, so opponent engines were culled wholesale once past 32 voices
            if (isLocalPlayer) s.priority = 16;
            s.minDistance  = minDistance;
            s.maxDistance  = maxDistance;
            s.rolloffMode  = AudioRolloffMode.Logarithmic;
            s.spread       = isLocalPlayer ? localPlayerSpread : spread;
            if (output != null) s.outputAudioMixerGroup = output;
            return s;
        }

#if UNITY_EDITOR
        [ContextMenu("Wire Engine From Model")]
        public void WireEngineFromModel()
        {
            const string model = "Assets/Audio/Engine/Bike/1000cc_Sport";
            AudioClip C(string f) => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(model + "/" + f);

            onClip        = C("low_on.wav");
            offClip       = C("low_off.wav");
            onHelmetClip  = C("Helmet/int_low_on.wav");
            offHelmetClip = C("Helmet/int_low_off.wav");

            if (onClip == null)
                Debug.LogWarning($"[EngineAudio] No low_on.wav in {model}, set clips manually.", this);
            else
                Debug.Log("[EngineAudio] Wired on/off engine loops + helmet variants.", this);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
