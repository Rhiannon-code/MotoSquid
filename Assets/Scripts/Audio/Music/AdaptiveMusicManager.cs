using MotoSquid.Combat;
using MotoSquid.Core;
using MotoSquid.Race;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Audio
{
    public class AdaptiveMusicManager : MonoBehaviour
    {
        public static AdaptiveMusicManager Instance { get; private set; }

        [Header("Audio Mixer Group")]
        [SerializeField] AudioMixerGroup musicMixerGroup;

        // Loop Stems
        [Header("Loop Stems")]
        [SerializeField] AudioClip ambienceClip;
        [SerializeField] AudioClip baseClip;
        [SerializeField] AudioClip percussionClip;
        [SerializeField] AudioClip intensityClip;
        [SerializeField] AudioClip tensionClip;
        [SerializeField] AudioClip rhythmGuitarClip;   // Slick only sixth stem, optional for other characters

        // One shot Stingers
        [Header("One shot Stingers")]
        [SerializeField] AudioClip countdownStingerClip;
        [SerializeField] AudioClip raceStartStingerClip;
        [SerializeField] AudioClip combatHitStingerClip;
        [SerializeField] AudioClip winStingerClip;
        [SerializeField] AudioClip loseStingerClip;

        // Per character scores
        [Header("Per Character Scores")]
        [SerializeField] CharacterScore[] characterScores;
        [SerializeField] CharacterScore   defaultScore;

        // Scene References
        [Header("Scene References")]
        [SerializeField] RaceManager raceManager;
        [SerializeField] CombatSystem playerCombat;

        // Tuning
        [Header("Tuning")]
        [SerializeField] float stemFadeSpeed  = 1.5f;
        [SerializeField] float combatLinger   = 5f;
        [SerializeField] float finishFadeTime = 1.5f;
        [SerializeField] float beatsPerMinute = 140f;
        [SerializeField] int   barsPerSection = 2;

        [Header("Stinger/Dialogue Ducking")]

        [SerializeField, Range(0f, 1f)] float stingerVolume        = 1f;
        [SerializeField, Range(0f, 1f)] float stingerDuckUnderVoice = 0.45f;
        [SerializeField] float stingerDuckSpeed = 6f;   // How fast the duck fades in/out (units/sec)

        // Stems
        AudioSource _ambience;
        AudioSource _base;
        AudioSource _percussion;
        AudioSource _intensity;
        AudioSource _tension;
        AudioSource _rhythmGuitar;
        AudioSource _stinger;

        // State machine
        enum MusicState { Idle, Countdown, Racing, Finished }
        MusicState  _state   = MusicState.Idle;
        MusicState? _pending;

        float _combatLingerRemaining;
        bool  _playerWon;

        // Beat clock
        float _nextBarTime;
        float BarDuration => (60f / beatsPerMinute) * 4f * barsPerSection;

        // Race state edge detect
        RaceManager.RaceState _lastRaceState = RaceManager.RaceState.Waiting;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _ambience   = MakeStem(loop: true);
            _base       = MakeStem(loop: true);
            _percussion = MakeStem(loop: true);
            _intensity  = MakeStem(loop: true);
            _tension    = MakeStem(loop: true);
            _rhythmGuitar = MakeStem(loop: true);
            _stinger    = MakeStem(loop: false);
        }

        void Start()
        {
            enabled = false;
        }

        void OnDestroy()
        {
            if (raceManager != null)
            {
                raceManager.OnRaceStart    -= OnRaceStart;
                raceManager.OnRaceComplete -= OnRaceComplete;
            }

            if (playerCombat != null)
            {
                playerCombat.onHitReceived.RemoveListener(OnPlayerHit);
                playerCombat.onAttack.RemoveListener(OnPlayerAttack);
            }
        }

        void Update()
        {
            TickBeat();
            PollRaceState();
            TickCombatLinger();
            TickStingerDuck();
            BlendStems();
        }

        void TickStingerDuck()
        {
            float target = VoiceBarkDirector.IsBarkOnFloor
                ? stingerVolume * stingerDuckUnderVoice
                : stingerVolume;
            _stinger.volume = Mathf.MoveTowards(_stinger.volume, target, stingerDuckSpeed * Time.deltaTime);
        }

        // Beat clock

        void TickBeat()
        {
            if (Time.time < _nextBarTime) return;
            _nextBarTime += BarDuration;

            if (_pending.HasValue)
            {
                EnterState(_pending.Value);
                _pending = null;
            }
        }

        // Queues a state change for the next bar boundary (beat synced transition)
        void QueueState(MusicState next)
        {
            if (_state == next || _pending == next) return;
            _pending = next;
        }

        // Race state polling
        void PollRaceState()
        {
            if (raceManager == null) return;
            var rs = raceManager.State;
            if (rs == _lastRaceState) return;
            _lastRaceState = rs;

            // Countdown is triggered by the start UI apply immediately so the stinger fires on time
            if (rs == RaceManager.RaceState.Countdown)
                EnterState(MusicState.Countdown);
        }

        // Combat linger

        void TickCombatLinger()
        {
            if (_combatLingerRemaining > 0f)
                _combatLingerRemaining -= Time.deltaTime;
        }

        bool InCombat => _state == MusicState.Racing && _combatLingerRemaining > 0f;

        // Stem blending

        void BlendStems()
        {
            bool racing  = _state == MusicState.Racing;
            bool combat  = InCombat;
            bool leading = IsPlayerLeading();
            bool ambient = _state == MusicState.Idle || _state == MusicState.Countdown;

            float tAmbience   = ambient ? 1f : 0f;
            float tBase       = racing  ? 1f : 0f;
            float tPercussion = combat  ? 1f : 0f;

            // Slick's sixth stem layers in for the whole race alongside the base (silent if no clip assigned)
            float tRhythm     = racing  ? 1f : 0f;

            // When in combat, intensity/tension duck to 40% rather than cutting completely
            float tIntensity  = racing && leading  ? (combat ? 0.4f : 1f) : 0f;
            float tTension    = racing && !leading ? (combat ? 0.4f : 1f) : 0f;

            float dt = stemFadeSpeed * Time.deltaTime;
            _ambience.volume   = Mathf.MoveTowards(_ambience.volume,   tAmbience,   dt);
            _base.volume       = Mathf.MoveTowards(_base.volume,       tBase,       dt);
            _percussion.volume = Mathf.MoveTowards(_percussion.volume, tPercussion, dt);
            _intensity.volume  = Mathf.MoveTowards(_intensity.volume,  tIntensity,  dt);
            _tension.volume    = Mathf.MoveTowards(_tension.volume,    tTension,    dt);
            _rhythmGuitar.volume = Mathf.MoveTowards(_rhythmGuitar.volume, tRhythm,  dt);
        }

        bool IsPlayerLeading()
        {
            if (raceManager?.playerTransform == null) return false;
            return raceManager.GetPosition(raceManager.playerTransform) == 1;
        }

        // State machine

        void EnterState(MusicState next)
        {
            _state = next;

            switch (next)
            {
                case MusicState.Idle:
                    CueLoop(_ambience,   ambienceClip);
                    CueLoop(_base,       baseClip);
                    CueLoop(_percussion, percussionClip);
                    CueLoop(_intensity,  intensityClip);
                    CueLoop(_tension,    tensionClip);
                    CueLoop(_rhythmGuitar, rhythmGuitarClip);
                    break;

                case MusicState.Countdown:
                    // Pre load all race stems so they're in sync when they fade in at race start
                    CueLoop(_base,       baseClip);
                    CueLoop(_percussion, percussionClip);
                    CueLoop(_intensity,  intensityClip);
                    CueLoop(_tension,    tensionClip);
                    CueLoop(_rhythmGuitar, rhythmGuitarClip);
                    PlayStinger(countdownStingerClip);
                    break;

                case MusicState.Racing:
                    PlayStinger(raceStartStingerClip);
                    // BlendStems() fades the base (and leading/chasing) stems in from here
                    break;

                case MusicState.Finished:
                    StopCoroutine(nameof(FinishSequence));
                    StartCoroutine(nameof(FinishSequence));
                    break;
            }
        }

        IEnumerator FinishSequence()
        {
            AudioSource[] stems  = { _base, _percussion, _intensity, _tension, _rhythmGuitar };
            float[]       starts = { _base.volume, _percussion.volume, _intensity.volume, _tension.volume, _rhythmGuitar.volume };
            float elapsed = 0f;

            while (elapsed < finishFadeTime)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / finishFadeTime);
                for (int i = 0; i < stems.Length; i++)
                    stems[i].volume = starts[i] * t;
                yield return null;
            }

            foreach (var s in stems) { s.Stop(); s.volume = 0f; }

            yield return new WaitForSeconds(0.3f);
            PlayStinger(_playerWon ? winStingerClip : loseStingerClip);
        }

        // Game event handlers
        void OnRaceStart() => QueueState(MusicState.Racing);

        void OnRaceComplete(List<RaceManager.RacerInfo> finishOrder)
        {
            _playerWon = finishOrder.Count > 0 && finishOrder[0].ai == null;
            EnterState(MusicState.Finished);
        }

        void OnPlayerHit()
        {
            _combatLingerRemaining = combatLinger;
            PlayStinger(combatHitStingerClip);
        }

        // Attacking refreshes linger at half duration music stays up while player is aggressive
        void OnPlayerAttack() => _combatLingerRemaining = Mathf.Max(_combatLingerRemaining, combatLinger * 0.5f);

        public void TriggerNearMiss() { }

        void ApplyCharacterScore()
        {
            CharacterScore score = null;

            string character = GameSession.Instance != null
                ? GameSession.Instance.SelectedCharacterName : null;

            if (!string.IsNullOrEmpty(character) && characterScores != null)
                foreach (var s in characterScores)
                    if (s != null && string.Equals(s.characterName, character, System.StringComparison.OrdinalIgnoreCase))
                    { score = s; break; }

            if (score == null) score = defaultScore;
            if (score == null) return;   // Keep the Inspector clips

            if (score.ambience   != null) ambienceClip   = score.ambience;
            if (score.baseStem   != null) baseClip       = score.baseStem;
            if (score.percussion != null) percussionClip = score.percussion;
            if (score.intensity  != null) intensityClip  = score.intensity;
            if (score.tension    != null) tensionClip    = score.tension;
            if (score.rhythmGuitar != null) rhythmGuitarClip = score.rhythmGuitar;

            if (score.countdownStinger != null) countdownStingerClip = score.countdownStinger;
            if (score.raceStartStinger != null) raceStartStingerClip = score.raceStartStinger;
            if (score.combatHitStinger != null) combatHitStingerClip = score.combatHitStinger;
            if (score.winStinger       != null) winStingerClip       = score.winStinger;
            if (score.loseStinger      != null) loseStingerClip      = score.loseStinger;
        }

        AudioSource MakeStem(bool loop)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop         = loop;
            src.playOnAwake  = false;
            src.volume       = 0f;
            src.spatialBlend = 0f;
            if (musicMixerGroup != null) src.outputAudioMixerGroup = musicMixerGroup;
            return src;
        }

        // Cues a looping stem without restarting it if it's already playing the same clip
        void CueLoop(AudioSource src, AudioClip clip)
        {
            if (clip == null || src.clip == clip) return;
            src.clip = clip;
            src.Play();
        }

        void PlayStinger(AudioClip clip)
        {
            if (clip == null) return;
            _stinger.PlayOneShot(clip);
        }
    }
}
