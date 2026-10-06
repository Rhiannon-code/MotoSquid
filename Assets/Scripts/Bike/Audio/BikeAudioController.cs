using MotoSquid.Audio;
using MotoSquid.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public class BikeAudioController : MonoBehaviour
    {
        [Header("Gear shift")]
        public AudioSource gearShiftSound;
        [Range(0f, 1.5f)] public float gearShiftVolume = 1f;  // Loud enough to mask the engine dip on a shift

        [Header("Tyre skid")]
        public AudioSource skidSound;
        // Surface aware skid, swap the skid loop + pitch by what's under the bike (tarmac vs gravel)
        // Leave both clips null to keep whatever single loop is already on the skid source
        public AudioClip skidRoadClip;
        public AudioClip skidOffRoadClip;
        public LayerMask skidRoadMask = ~0;
        public string    skidRoadTag  = "Road";
        public float     skidOffRoadPitch = 0.85f;
        public float     skidSurfaceProbeDistance = 2f;
        // Raw slip is normalised against maxSpeed upstream, so hard cornering only reads ~0.02 and a
        // flat 0.1 gate made the tyre a brake only sound. Map the useful slice instead
        public float     skidSlipFloor = 0.02f;
        public float     skidSlipFull  = 0.35f;
        private bool     _skidOnRoad = true;

        [Header("Wind noise")]
        public AudioSource windSource;
        public float windStartSpeed = 100f;   // Km/h, above this the rush is always present
        public float windFullSpeed  = 260f;   // Km/h
        public float windIdleVolume = 0.28f;  // The persistent floor once past windStartSpeed
        public float windMaxVolume  = 0.5f;   // Kept low so it colours the mix, never drowns the engine
        public float windFadeInBand = 15f;    // Km/h below the start speed the floor eases in over
        // A wide pitch swing on a 3.5 s noise loop reads as tape speed, not air speed. Keep pitch
        // almost still and let a filter do the work: real wind gets brighter with speed, not higher
        public float windMinPitch   = 0.95f;
        public float windMaxPitch   = 1.12f;
        public float windMinCutoff  = 700f;
        public float windMaxCutoff  = 12000f;
        public float windSmoothTime = 0.15f;
        // Inside the helmet (first person) the rush of air is behind a visor: the mixer's helmet low pass only
        // takes the hiss off, so the rumble also drops and its own filter closes further
        [Range(0f, 1f)] public float helmetWindVolume = 0.4f;
        public float helmetWindCutoff = 1500f;
        public float helmetWindBlendTime = 0.25f;

        [Header("Helmet noise")]
        // The muffled layer INSIDE the lid: low-passed buffet and breathing room tone. Distinct from
        // the exterior wind rush above, it starts earlier, tops out quieter and never fully leaves,
        // which is what stops the two reading as one doubled wind loop
        public AudioSource helmetSource;
        public float helmetIdleVolume  = 0.25f;   // Room tone while stationary, the lid is still on
        public float helmetMaxVolume   = 0.45f;
        public float helmetStartSpeed  = 15f;     // Km/h, buffet builds well before the wind rush does
        public float helmetFullSpeed   = 110f;    // Km/h
        public float helmetMinPitch    = 0.85f;
        public float helmetMaxPitch    = 1.05f;   // Narrow range, a muffled layer shouldn't whistle
        public float helmetSmoothTime  = 0.25f;
        private bool  _helmetAllowed = true;      // Settings/accessibility toggle
        private bool  _accSubscribed;
        private float _helmetVel;

        [Header("Landing thud")]
        public AudioClip landingThud;
        public float landingMinAirtime  = 0.4f;
        public float landingFullAirtime = 1.5f;

        [Header("Combat")]
        // Combat one-shots run through the shared chassis one-shot source so they inherit this bike's
        // 3D settings and mixer group (the player's hits stay centred, an AI scrap across the track
        // stays positional). Arrays are picked from at random so repeated hits don't machine gun
        public AudioClip[] swingWhooshClips;
        public AudioClip[] hitImpactClips;
        public AudioClip   knockOffClip;
        [Range(0f, 1.5f)] public float combatVolume = 1f;

        [Header("3D / output")]
        [Range(0f, 1f)] public float oneShotSpatialBlend = 1f;
        public float oneShotMinDistance = 3f;
        public float oneShotMaxDistance = 60f;
        public AudioMixerGroup engineOutput;   // Bike output group (Player Bike / Opponent Bike mixer)

        [Header("Player")]
        // The local player's own bike, part 2D so its sounds don't pan as the chase cam swings
        // Leave off for AI so they stay fully positional
        public bool isLocalPlayer = false;
        [Range(0f, 1f)] public float localPlayerSpatialBlend = 0.7f;
        public float worldSpread = 40f;   // Applied to the 3D child sources (skid, gear) and one shots

        private AudioLowPassFilter _windFilter;
        private const int AUDIO_INTERVAL = 3; // Skid is cheap to throttle
        private int   _skidCounter;
        private float _airtime;
        private bool  _wasGrounded = true;
        private float _windVel, _helmetWindBlend;

        private AudioSource         _oneShotSource;
        private IBikeAudioState     _state;
        private Transform           _bikeRoot;
        private IEngineAudio _engine;
        private bool                 _engineSearched;

        // Either backend (custom synth or RES2 driver) both implement IEngineAudio
        private IEngineAudio Engine
        {
            get
            {
                if (_engine == null && !_engineSearched)
                {
                    _engineSearched = true;
                    Transform root = _bikeRoot != null ? _bikeRoot : transform;
                    _engine = root.GetComponentInChildren<IEngineAudio>(true);
                }
                return _engine;
            }
        }

        // The local player's audio hub, so scene-level systems (ambience ducking, zone triggers) can
        // ask "how fast is the human going" without walking the bike hierarchy
        public static BikeAudioController LocalPlayer { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearLocalPlayer() => LocalPlayer = null;

        public float SpeedKmh => _state != null ? Mathf.Abs(_state.SpeedMs) * 3.6f : 0f;

        public float EngineRpm01     => Engine != null ? Engine.EngineRpm01 : 0f;
        public bool  EngineAtLimiter => Engine != null && Engine.EngineAtLimiter;
        public bool  IsFirstPersonView => Engine is EngineAudio e && e.IsFirstPersonView;
        public void  SetEngineMuted(bool muted) { Engine?.SetEngineMuted(muted); }
        public void  SilenceEngine() { Engine?.SilenceEngine(); }

        private void Awake()
        {
            _state    = GetComponentInParent<IBikeAudioState>();
            _bikeRoot = (_state as Component) != null ? (_state as Component).transform : transform;

            if (gearShiftSound == null) gearShiftSound = FindChildSource("Gear Shift");
            if (skidSound      == null) skidSound      = FindChildSource("Skid Sound");
            if (windSource     == null) windSource     = FindChildSource("Wind");
            if (helmetSource   == null) helmetSource   = FindChildSource("Helmet");

            SilenceChildSource(windSource);
            if (windSource != null)
            {
                _windFilter = windSource.GetComponent<AudioLowPassFilter>();
                if (_windFilter == null) _windFilter = windSource.gameObject.AddComponent<AudioLowPassFilter>();
                _windFilter.lowpassResonanceQ = 1f;
                AudioLoop.RouteToGroup(windSource, "Wind");
            }
            SilenceChildSource(helmetSource);
            // You're inside your own helmet, it can't pan or fall off with distance
            if (helmetSource != null) helmetSource.spatialBlend = 0f;
            SilenceChildSource(skidSound);
            SilenceChildSource(gearShiftSound);
            if (skidSound      != null) skidSound.spread      = worldSpread;
            if (gearShiftSound != null) gearShiftSound.spread = worldSpread;

            _oneShotSource = CreateOneShotSource("Chassis_OneShot");
        }

        private void OnEnable()
        {
            if (_state == null) _state = GetComponentInParent<IBikeAudioState>();
            if (_state != null) _state.GearChanged += OnGearChanged;

            if (isLocalPlayer) LocalPlayer = this;

            TrySubscribeAccessibility();
        }

        private void OnDisable()
        {
            if (_state != null) _state.GearChanged -= OnGearChanged;

            if (LocalPlayer == this) LocalPlayer = null;

            var acc = AccessibilityManager.Instance;
            if (acc != null) acc.OnHelmetAudioChanged -= OnHelmetAudioSettingChanged;
            _accSubscribed = false;
        }

        // The manager is a DontDestroyOnLoad singleton created in the menu scene, but a bike enabling
        // on the first frame of a directly-loaded race scene can beat it. Retry until it's there
        private void TrySubscribeAccessibility()
        {
            if (_accSubscribed) return;
            var acc = AccessibilityManager.Instance;
            if (acc == null) return;
            acc.OnHelmetAudioChanged += OnHelmetAudioSettingChanged;
            _helmetAllowed = acc.HelmetAudio;
            _accSubscribed = true;
        }

        private void OnHelmetAudioSettingChanged(bool enabled) => _helmetAllowed = enabled;

        private void Update()
        {
            if (_state == null) return;

            WindUpdate();
            HelmetUpdate();
            LandingUpdate();

            _skidCounter++;
            if (_skidCounter >= AUDIO_INTERVAL)
            {
                _skidCounter = 0;
                UpdateSkidSound();
            }
        }

        // Skid
        private void UpdateSkidSound()
        {
            if (skidSound == null) return;
            // BurnoutAudio owns the tyre while burning out, so the two loops can't stack
            if (_state.IsDoingBurnout) { skidSound.mute = true; return; }

            float skidIntensity = Mathf.InverseLerp(skidSlipFloor, skidSlipFull, _state.SkidIntensity01);

            if (skidIntensity <= 0f)
            {
                skidSound.mute = true;
                return;
            }

            // Surface aware clip/pitch swap (only when surface clips are assigned, otherwise the
            // existing single skid loop is used unchanged)
            if (skidRoadClip != null || skidOffRoadClip != null)
            {
                bool onRoad = ProbeSkidSurface();
                if (onRoad != _skidOnRoad || skidSound.clip == null)
                {
                    _skidOnRoad = onRoad;
                    AudioClip clip = onRoad ? skidRoadClip : skidOffRoadClip;
                    if (clip != null && skidSound.clip != clip)
                    {
                        skidSound.clip = clip;
                        skidSound.loop = true;
                    }
                }
                skidSound.pitch = _skidOnRoad ? 1f : skidOffRoadPitch;
            }

            skidSound.mute   = false;
            skidSound.volume = skidIntensity;

            // The skid source isn't started elsewhere, make sure it's actually playing while skidding
            AudioLoop.EnsurePlaying(skidSound, "skid restart", this);
        }

        // Raycast under the bike to classify the surface. Mirrors the old SurfaceAudio probe
        private bool ProbeSkidSurface()
        {
            Transform root = _bikeRoot != null ? _bikeRoot : transform;
            Vector3 origin = root.position + Vector3.up * 0.2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                skidSurfaceProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is TerrainCollider) return false;   // Terrain is always off road
                bool layerIsRoad = (skidRoadMask.value & (1 << hit.collider.gameObject.layer)) != 0;
                bool tagIsRoad   = !string.IsNullOrEmpty(skidRoadTag) && hit.collider.CompareTag(skidRoadTag);
                return layerIsRoad || tagIsRoad;
            }
            return true;   // Nothing under us, assume road, don't spam gravel
        }

        // Wind
        private void WindUpdate()
        {
            if (windSource == null) return;

            // Wind is the rush of air past the local rider's own helmet, a first-person immersion
            // effect. AI bikes broadcasting a constant wind loop just pile up into noise (and you
            // wouldn't hear the wind past a bike ahead, you'd hear its engine), so wind is local 
            // player only
            if (!isLocalPlayer)
            {
                if (windSource.isPlaying) windSource.Stop();
                windSource.volume = 0f;
                return;
            }

            float speedKmh = Mathf.Abs(_state.SpeedMs) * 3.6f;
            float t = Mathf.Clamp01(Mathf.InverseLerp(windStartSpeed, windFullSpeed, speedKmh));

            // Eased in over the band below windStartSpeed so the persistent layer arrives without a pop
            float gate   = Mathf.Clamp01(Mathf.InverseLerp(windStartSpeed - windFadeInBand, windStartSpeed, speedKmh));
            float target = gate * Mathf.Lerp(windIdleVolume, windMaxVolume, t);

            bool inHelmet = IsFirstPersonView && _helmetAllowed;
            _helmetWindBlend = Mathf.MoveTowards(_helmetWindBlend, inHelmet ? 1f : 0f,
                                                 Time.deltaTime / Mathf.Max(0.01f, helmetWindBlendTime));
            target *= Mathf.Lerp(1f, helmetWindVolume, _helmetWindBlend);

            windSource.volume = Mathf.SmoothDamp(windSource.volume, target, ref _windVel, windSmoothTime);
            windSource.pitch  = Mathf.Lerp(windMinPitch, windMaxPitch, t);

            if (_windFilter != null)
            {
                float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(windMinCutoff), Mathf.Log(windMaxCutoff), t));
                float helmet = Mathf.Min(cutoff, helmetWindCutoff);
                _windFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(helmet), _helmetWindBlend));
            }
            AudioLoop.EnsurePlaying(windSource, "wind restart", this);
        }

        // Helmet noise
        private void HelmetUpdate()
        {
            if (helmetSource == null) return;
            if (!_accSubscribed) TrySubscribeAccessibility();

            // Same reasoning as the wind: you are only ever inside your OWN helmet. AI bikes must
            // never emit it, and it's off entirely when the player has switched it off in settings
            if (!isLocalPlayer || !_helmetAllowed)
            {
                if (helmetSource.isPlaying) helmetSource.Stop();
                helmetSource.volume = 0f;
                return;
            }

            // The lid is only on your head in the first person view, from outside the bike it's silent
            bool firstPerson = Engine is EngineAudio engine && engine.IsFirstPersonView;

            float t = Mathf.Clamp01(Mathf.InverseLerp(helmetStartSpeed, helmetFullSpeed, SpeedKmh));
            float target = firstPerson ? Mathf.Lerp(helmetIdleVolume, helmetMaxVolume, t) : 0f;
            helmetSource.volume = Mathf.SmoothDamp(helmetSource.volume, target, ref _helmetVel, helmetSmoothTime);
            helmetSource.pitch  = Mathf.Lerp(helmetMinPitch, helmetMaxPitch, t);

            if (firstPerson)
            {
                AudioLoop.EnsurePlaying(helmetSource, "helmet restart", this);
            }
            else if (helmetSource.isPlaying && helmetSource.volume < 0.001f)
            {
                helmetSource.Stop();
            }
        }

        // Combat one-shots (called by CombatSystem)
        public void PlaySwingWhoosh() => PlayRandomOneShot(swingWhooshClips, combatVolume);

        // An equipped melee weapon can bring its own swing/impact set. Routing it through here (rather
        // than a loose AudioSource on the prop) keeps every combat sound on this bike's 3D settings
        // and mixer group, so a weapon can't end up louder or unspatialised
        public void PlaySwingWhoosh(AudioClip[] weaponClips) =>
            PlayRandomOneShot(weaponClips != null && weaponClips.Length > 0 ? weaponClips : swingWhooshClips,
                              combatVolume);

        // strength01: how hard the hit landed, 0..1. Scales the impact so a glancing shove and a
        // finishing blow don't sound identical
        public void PlayHitImpact(float strength01)
        {
            PlayRandomOneShot(hitImpactClips, combatVolume * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(strength01)));
        }

        public void PlayHitImpact(float strength01, AudioClip[] weaponClips)
        {
            PlayRandomOneShot(weaponClips != null && weaponClips.Length > 0 ? weaponClips : hitImpactClips,
                              combatVolume * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(strength01)));
        }

        public void PlayKnockOff()
        {
            if (knockOffClip == null || _oneShotSource == null) return;
            _oneShotSource.PlayOneShot(knockOffClip, combatVolume);
            AudioBurstLog.Note("knock off", this);
        }

        private void PlayRandomOneShot(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || _oneShotSource == null) return;
            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null)
            {
                _oneShotSource.PlayOneShot(clip, volume);
                AudioBurstLog.Note($"combat ({clip.name})", this);
                if (isLocalPlayer && !_reportedCulled) StartCoroutine(ReportIfCulled(clip.name));
            }
        }

        // Landing thud
        private void LandingUpdate()
        {
            bool grounded = _state.IsGrounded;

            // Held on the start line, the bike settles from its spawn height onto the track. That
            // touchdown must not fire the landing impact one shot before the race has begun
            if (!_state.CanMove)
            {
                _airtime     = 0f;
                _wasGrounded = grounded;
                return;
            }

            if (!grounded)
            {
                _airtime += Time.deltaTime;
            }
            else
            {
                if (!_wasGrounded && _airtime >= landingMinAirtime && landingThud != null && _oneShotSource != null)
                {
                    float vol = Mathf.Clamp01(Mathf.InverseLerp(landingMinAirtime, landingFullAirtime, _airtime));
                    _oneShotSource.PlayOneShot(landingThud, Mathf.Lerp(0.5f, 1f, vol));
                    AudioBurstLog.Note("landing thud", this);
                }
                _airtime = 0f;
            }
            _wasGrounded = grounded;
        }

        private float _lastGearSoundTime = -1f;

        private void OnGearChanged()
        {
            if (gearShiftSound == null) return;
            // Restarting on every shift stacked into a burst whenever gears changed several times fast
            if (Time.time - _lastGearSoundTime < 0.25f) return;
            _lastGearSoundTime = Time.time;

            // Restore the volume (Awake silences child sources against play on awake) and restart on
            // every shift, so the shift clip lands on, and masks, the brief engine dip through the change
            gearShiftSound.volume = gearShiftVolume;
            gearShiftSound.Play();
            AudioBurstLog.Note("gear shift", this);
        }

        // Source creation/lookup
        bool _reportedCulled;

        // Confirms or rules out the voice limit for missing swing sounds: a one-shot the audio system
        // culled leaves the source virtual. Logged once
        System.Collections.IEnumerator ReportIfCulled(string clipName)
        {
            yield return null;
            if (_oneShotSource == null || !_oneShotSource.isVirtual) yield break;
            _reportedCulled = true;
            int playing = 0;
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (src.isPlaying) playing++;
            Debug.LogWarning($"[BikeAudio] Player one-shot '{clipName}' was culled by the voice limit " +
                             $"({playing} sources playing, {AudioSettings.GetConfiguration().numRealVoices} real voices).", this);
        }

        private AudioSource CreateOneShotSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop          = false;
            s.playOnAwake   = false;

            // PlayOneShot scales its volumeScale by AudioSource.volume, so this must sit at full volume
            s.volume        = 1f;
            s.spatialBlend  = isLocalPlayer ? localPlayerSpatialBlend : oneShotSpatialBlend;
            s.minDistance   = oneShotMinDistance;
            s.maxDistance   = oneShotMaxDistance;
            s.rolloffMode   = AudioRolloffMode.Logarithmic;
            s.spread        = worldSpread;
            // The player's swings and hits outrank everything; opponents stay on the default 128 so the voice
            // limit drops the quietest and furthest of them rather than every one
            if (isLocalPlayer) s.priority = 0;
            if (engineOutput != null) s.outputAudioMixerGroup = engineOutput;
            return s;
        }

        // Look under this component's own object first (so it grabs the Gear Shift/Skid/Wind
        // sources sitting next to it when placed on the "Audios" child), then the whole bike
        private AudioSource FindChildSource(string childName)
        {
            var src = SearchUnder(transform, childName);
            if (src == null && _bikeRoot != null && _bikeRoot != transform)
                src = SearchUnder(_bikeRoot, childName);
            return src;
        }

        // Stop a hand wired child source from Play On Awake blasting at scene load, its own per frame
        // logic re starts it when needed (and at volume 0, so it fades up rather than popping)
        private static void SilenceChildSource(AudioSource s)
        {
            if (s == null) return;
            s.playOnAwake = false;
            s.volume      = 0f;
            s.Stop();
        }

        private static AudioSource SearchUnder(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                {
                    var s = t.GetComponent<AudioSource>();
                    if (s != null) return s;
                }
            }
            return null;
        }

#if UNITY_EDITOR
        // One click, wire the chassis sounds, the Gear Shift/Skid/Wind child AudioSources
        // (created if missing) and the landing thud clip, from the shared audio folder
        [ContextMenu("Wire Chassis Audio")]
        public void WireChassisAudio()
        {
            const string baseF = "Assets/Audio";
            AudioClip B(string rel) => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseF + "/" + rel);

            gearShiftSound = WireChild("Gear Shift", B("Shifting Sounds/shift_1.wav"),        loop: false);
            skidSound      = WireChild("Skid Sound", B("skid loop 1.wav"),                    loop: true);
            windSource     = WireChild("Wind",       B("Wind_Sounds/wind_1.wav"),             loop: true);
            helmetSource   = WireChild("Helmet",     B("Wind_Sounds/Interior/int_wind_1.wav"), loop: true);
            landingThud    = B("HUGE IMPACT HIT - Large Deep Low-Pitched Falling Thump Noise - 01.wav");

            Debug.Log("[BikeAudio] Wired chassis audio (gear shift/skid/wind/helmet/landing thud).", this);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Find (or create under this object) a named child AudioSource and set its clip
        private AudioSource WireChild(string childName, AudioClip clip, bool loop)
        {
            Transform root = transform;
            var bike = GetComponentInParent<IBikeAudioState>() as Component;
            if (bike != null) root = bike.transform;

            // Look under the audio host FIRST. Searching the whole bike by name collides with rider
            // art, the character models carry a "Helmet" mesh, and which one wins depends on
            // hierarchy order. A source parented to rider art also dies on the next character
            // rebuild, taking the reference with it
            Transform found = null;
            foreach (var t in transform.GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name == childName) { found = t; break; }

            if (found == null)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == childName) { found = t; break; }

            if (found == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
                found = go.transform;
            }

            var src = found.GetComponent<AudioSource>() ?? found.gameObject.AddComponent<AudioSource>();
            if (clip != null) src.clip = clip;
            src.loop        = loop;
            src.playOnAwake = false;
            UnityEditor.EditorUtility.SetDirty(src);
            return src;
        }
#endif
    }
}
