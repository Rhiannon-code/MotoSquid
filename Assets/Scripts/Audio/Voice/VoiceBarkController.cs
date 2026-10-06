using MotoSquid.Combat;
using MotoSquid.Core;
using MotoSquid.Rider;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Audio
{
    public class VoiceBarkController : MonoBehaviour
    {
        [Header("Voice")]
        [SerializeField] CharacterVoice voice;

        [Header("Player resolution (Player1/Player2 prefabs)")]
        [SerializeField] bool resolveFromSelectedCharacter = false;
        [SerializeField] int  playerSlot = 1;
        [SerializeField] CharacterVoice[] voiceLibrary;

        [Header("References (auto found if left empty)")]
        [SerializeField] CombatSystem   combat;
        [SerializeField] RagdollActivator ragdoll;

        [Header("Audio")]
        [SerializeField] AudioMixerGroup voiceMixerGroup;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField] bool  spatial = true;
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 60f;

        [Header("Tuning")]
        [SerializeField] float categoryCooldown = 2.5f;

        // Per-category cooldowns alone let one racer talk constantly: the race director's overtake
        // poll fires on every position swap, and Overtaking/BeingOvertaken hold separate slots
        [SerializeField] float globalCooldown = 14f;

        // Combat lines run on their own clock, shared by attacking and being attacked. Under the global
        // quiet above, any overtake line (one fires on every position swap) silenced a fight for 14 s
        [SerializeField] float combatCooldown = 4f;

        // Grunts are effort sounds on every swing, not lines, their own source so they never hold the floor
        // and block the hit landed line that follows 0.18 s later, and a short cooldown of their own
        [SerializeField] float gruntCooldown = 0.5f;
        AudioSource _gruntSource;
        AudioClip   _lastGrunt;
        float       _gruntNextTime;

        // The VO carries ~0.3 s of dead air at the head, which on a reactive bark (a swing, a
        // crash) reads as the game lagging. The builder measures it; this starts past it
        [SerializeField] bool skipClipLeadIn = true;
        // Off: a line always plays to its end, and a more important one that arrives meanwhile is skipped
        // On: a crash or a hit cuts off whatever was being said, which is what clipped lines mid word
        [SerializeField] bool allowInterrupt = false;
        [SerializeField] int   playerPriorityBump = 1000;
        // Diagnostic for combat lines going unheard, logs the resolved voice at start and the outcome of
        // every combat bark. Turn off once they are confirmed
        [SerializeField] bool  logCombatBarks = true;

        AudioSource _source;
        AudioClip   _lastClip;            
        readonly float[] _categoryNextTime = new float[System.Enum.GetValues(typeof(BarkCategory)).Length];
        float _globalNextTime, _combatNextTime;

        bool IsPlayer => combat != null && combat.isPlayer;

        void Awake()
        {
            // Its own child source, never this object's. BoostSystem sits on the bike root and takes
            // whatever GetComponent<AudioSource> returns, so sharing one put boost one shots on the
            // Voice bus and let PlayBark's Stop() cut them off
            _source = new GameObject("Voice Bark Source").AddComponent<AudioSource>();
            _source.transform.SetParent(transform, false);

            _source.playOnAwake  = false;
            // Top claim on the 32 real voices, a line the player triggered is never the one culled
            _source.priority     = 0;
            _source.loop         = false;
            _source.spatialBlend = spatial ? 1f : 0f;
            _source.dopplerLevel = 0f;
            _source.rolloffMode  = AudioRolloffMode.Linear;
            _source.minDistance  = minDistance;
            _source.maxDistance  = maxDistance;
            if (voiceMixerGroup != null) _source.outputAudioMixerGroup = voiceMixerGroup;

            _gruntSource = Instantiate(_source, transform);
            _gruntSource.name = "Voice Grunt Source";

            if (combat  == null) combat  = GetComponentInParent<CombatSystem>()   ?? GetComponentInChildren<CombatSystem>();
            if (ragdoll == null) ragdoll = GetComponentInParent<RagdollActivator>() ?? GetComponentInChildren<RagdollActivator>();
        }

        void Start()
        {
            ResolveVoice();
            if (voice != null) voice.MeasureAll();

            if (combat != null)
            {
                combat.onAttack.AddListener(OnSwing);
                combat.onHitLanded.AddListener(OnHitLanded);
                combat.onHitReceived.AddListener(OnHitReceived);
            }
            if (ragdoll != null)
                ragdoll.onRagdollActivated.AddListener(OnCrash);

            if (logCombatBarks)
                Debug.Log($"[VoiceBark] {name}: voice '{(voice != null ? voice.characterName : "NONE")}', combat " +
                          $"{(combat != null ? combat.name : "NOT FOUND")}, ragdoll {(ragdoll != null ? "found" : "NOT FOUND")}", this);
        }

        void LogCombat(BarkCategory category, string outcome)
        {
            if (logCombatBarks && (category == BarkCategory.Attacking || category == BarkCategory.BeingAttacked ||
                                   category == BarkCategory.SwingGrunt))
                Debug.Log($"[VoiceBark] {category}: {outcome}", this);
        }

        void OnDestroy()
        {
            if (combat != null)
            {
                combat.onAttack.RemoveListener(OnSwing);
                combat.onHitLanded.RemoveListener(OnHitLanded);
                combat.onHitReceived.RemoveListener(OnHitReceived);
            }
            if (ragdoll != null)
                ragdoll.onRagdollActivated.RemoveListener(OnCrash);
        }

        void ResolveVoice()
        {
            // Start() runs after RiderSwitch (execution order -50), so without this the rider's
            // voice was set correctly and then immediately overwritten from the selection or,
            // with no selection made, by the first entry in voiceLibrary
            if (_voiceSetExplicitly) return;

            if (!resolveFromSelectedCharacter || voiceLibrary == null) return;

            var session = GameSession.Instance;
            if (session == null) return;

            string character = playerSlot == 2
                ? session.SelectedCharacterNameP2
                : session.SelectedCharacterName;

            if (string.IsNullOrEmpty(character)) return;

            foreach (var v in voiceLibrary)
                if (v != null && string.Equals(v.characterName, character, System.StringComparison.OrdinalIgnoreCase))
                { voice = v; return; }

            // A character with no entry in the library would otherwise leave the racer mute for the
            // whole race, so fall back to any voice rather than saying nothing
            if (voice == null)
                foreach (var v in voiceLibrary)
                    if (v != null)
                    {
                        Debug.LogWarning($"{name}: no voice for '{character}', falling back to '{v.characterName}'", this);
                        voice = v;
                        return;
                    }
        }

        void OnSwing()        => PlayGrunt();
        void OnHitLanded()    => PlayBark(BarkCategory.Attacking);
        void OnHitReceived()  => PlayBark(BarkCategory.BeingAttacked);
        void OnCrash()        => PlayBark(BarkCategory.Crash);

        // RiderSwitch knows who is actually ON the bike, which is what the player can see. That
        // beats the selection, nothing wires selection to RiderSwitch yet, so the two disagree
        public void SetVoice(CharacterVoice v)
        {
            if (v == null) return;
            voice = v;
            _voiceSetExplicitly = true;
            voice.MeasureAll();
        }

        bool _voiceSetExplicitly;

        void PlayGrunt()
        {
            if (voice == null || Time.unscaledTime < _gruntNextTime) return;

            AudioClip clip = voice.GetRandomClip(BarkCategory.SwingGrunt, _lastGrunt);
            if (clip == null) { LogCombat(BarkCategory.SwingGrunt, $"'{voice.characterName}' has no grunt"); return; }

            float lead = skipClipLeadIn ? voice.LeadIn(clip) : 0f;
            if (lead < 0f || lead >= clip.length - 0.05f) lead = 0f;

            _gruntSource.Stop();
            _gruntSource.clip   = clip;
            _gruntSource.volume = volume;
            if (lead > 0f) _gruntSource.time = lead;
            _gruntSource.Play();
            AudioBurstLog.Note($"grunt ({clip.name})", this);
            LogCombat(BarkCategory.SwingGrunt, $"PLAYED '{clip.name}'");

            _lastGrunt     = clip;
            _gruntNextTime = Time.unscaledTime + gruntCooldown;
        }

        public void PlayBark(BarkCategory category)
        {
            if (category == BarkCategory.SwingGrunt) { PlayGrunt(); return; }

            if (voice == null) { LogCombat(category, "no voice resolved"); return; }

            int idx = (int)category;
            bool combatLine = category == BarkCategory.Attacking || category == BarkCategory.BeingAttacked;
            if (Time.unscaledTime < _categoryNextTime[idx]) { LogCombat(category, "category cooldown"); return; }
            if (combatLine ? Time.unscaledTime < _combatNextTime
                           : !IgnoresGlobalCooldown(category) && Time.unscaledTime < _globalNextTime)
            { LogCombat(category, "combat cooldown"); return; }

            AudioClip clip = voice.GetRandomClip(category, _lastClip);
            if (clip == null) { LogCombat(category, $"'{voice.characterName}' has no clip for it"); return; }

            float lead = 0f;
            if (skipClipLeadIn)
            {
                lead = voice.LeadIn(clip);
                if (lead < 0f || lead >= clip.length - 0.05f) lead = 0f;
            }

            int priority = BasePriority(category) + (IsPlayer ? playerPriorityBump : 0);
            if (!VoiceBarkDirector.TryClaimFloor(priority, clip.length - lead, allowInterrupt))
            { LogCombat(category, "another line still playing"); return; }

            _source.Stop();
            _source.clip   = clip;
            _source.volume = volume;
            if (lead > 0f) _source.time = lead;   // Must follow the clip assignment
            _source.Play();
            AudioBurstLog.Note($"voice {category} ({clip.name})", this);
            LogCombat(category, $"PLAYED '{clip.name}' (vol {volume}, source {( _source.isActiveAndEnabled ? "active" : "INACTIVE")}, mixer {(_source.outputAudioMixerGroup != null ? _source.outputAudioMixerGroup.name : "none")})");

            _lastClip = clip;
            _categoryNextTime[idx] = Time.unscaledTime + categoryCooldown;
            if (combatLine) _combatNextTime = Time.unscaledTime + combatCooldown;
            else            _globalNextTime = Time.unscaledTime + globalCooldown;
        }

        // The race's loud moments always play, they still buy quiet afterwards
        static bool IgnoresGlobalCooldown(BarkCategory category) =>
            category == BarkCategory.Crash    || category == BarkCategory.WinRace ||
            category == BarkCategory.LoseRace || category == BarkCategory.StartTaunt;

        static int BasePriority(BarkCategory category)
        {
            switch (category)
            {
                case BarkCategory.Crash:          return 100;
                case BarkCategory.WinRace:        return 95;
                case BarkCategory.LoseRace:       return 90;
                case BarkCategory.StartTaunt:     return 80;
                case BarkCategory.BeingAttacked:  return 60;
                case BarkCategory.Overtaking:     return 45;
                case BarkCategory.BeingOvertaken: return 40;
                case BarkCategory.Attacking:      return 30;
                default:                          return 10;
            }
        }
    }
}
