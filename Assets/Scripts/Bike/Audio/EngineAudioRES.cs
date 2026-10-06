using SkrilStudio;
using UnityEngine;

namespace MotoSquid.Bike
{
    public class EngineAudioRES : MonoBehaviour, IEngineAudio
    {
        [Header("RES2 engine")]
        public RealisticEngineSound res;   // Auto found in children if left empty
        public float idleRpm = 900f;       // Absolute RPM at rest (RES2 1000cc idle ~894)

        [Header("RPM feel")]
        public float rpmSlew = 4f;                            // Normalised RPM per second, engine inertia
        [Range(0.7f, 1f)] public float revCeiling01 = 0.9f;  // In gear revs top out at this fraction of redline

        private IBikeAudioState _state;
        private float _rpm01;
        private float _baseVolume = 1f;
        private bool  _muted;

        public float EngineRpm01 =>
            res != null && res.maxRPMLimit > 0f ? Mathf.Clamp01(res.engineCurrentRPM / res.maxRPMLimit) : _rpm01;
        public bool  EngineAtLimiter => false;

        public void SetEngineMuted(bool muted) => _muted = muted;
        public void SilenceEngine() { if (res != null) res.masterVolume = 0f; }

        private void Awake()
        {
            _state = GetComponentInParent<IBikeAudioState>();
            if (res == null) res = GetComponentInChildren<RealisticEngineSound>(true);
            if (res != null) _baseVolume = res.masterVolume;
        }

        private void OnEnable()
        {
            if (_state == null) _state = GetComponentInParent<IBikeAudioState>();
            if (_state != null) _state.EngineMuteRequested += OnEngineMuteRequested;
        }

        private void OnDisable()
        {
            if (_state != null) _state.EngineMuteRequested -= OnEngineMuteRequested;
        }

        private void OnEngineMuteRequested(bool muted) => _muted = muted;

        private void Update()
        {
            if (res == null || _state == null) return;

            // RPM purely from road speed (per current gear); RES2 handles clip crossfade + pitch
            float speedKmh = Mathf.Abs(_state.SpeedMs) * 3.6f;
            float norm     = GearNorm(speedKmh);
            _rpm01 = Mathf.MoveTowards(_rpm01, Mathf.Clamp01(norm), rpmSlew * Time.deltaTime);

            res.engineCurrentRPM = Mathf.Lerp(idleRpm, revCeiling01 * res.maxRPMLimit, _rpm01);
            res.gasPedalPressing = _state.ThrottleInput > 0.1f;
            res.carCurrentSpeed  = speedKmh;
            res.carMaxSpeed      = Mathf.Max(1f, _state.MaxSpeedKmh);
            res.masterVolume     = _muted ? 0f : _baseVolume;
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
    }
}
