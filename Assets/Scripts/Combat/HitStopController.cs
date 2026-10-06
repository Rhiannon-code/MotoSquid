using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.Rider;
using System.Collections;
using UnityEngine;

namespace MotoSquid.Combat
{
    public class HitStopController : MonoBehaviour
    {
        public static HitStopController Instance { get; private set; }

        [Header("Auto hook (optional)")]
        public CombatSystem combatSystem;
        public RagdollActivator ragdoll;

        [Header("Hit stop")]
        [Range(0f, 1f)] public float frozenTimeScale = 0.05f;
        public float freezeDuration = 0.08f;

        [Header("Rumble on impact")]
        public int playerDeviceIndex = -1;
        public float rumbleLow  = 0.8f;
        public float rumbleHigh = 0.6f;

        bool  _frozen;
        float _restoreTimeScale = 1f;
        float _restoreFixedDelta;
        Coroutine _freezeRoutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnEnable()
        {
            if (combatSystem != null) combatSystem.onDealtKnockoff += OnImpact;
            if (ragdoll != null && ragdoll.onRagdollActivated != null)
                ragdoll.onRagdollActivated.AddListener(OnImpact);
        }

        void OnDisable()
        {
            if (combatSystem != null) combatSystem.onDealtKnockoff -= OnImpact;
            if (ragdoll != null && ragdoll.onRagdollActivated != null)
                ragdoll.onRagdollActivated.RemoveListener(OnImpact);
        }

        void OnImpact() => Freeze();

        public void Freeze() => Freeze(freezeDuration, frozenTimeScale);

        public void Freeze(float duration, float timeScale)
        {
            // Don't stack hit stops or fight a real pause (timeScale already ~0)
            if (_frozen || Time.timeScale <= 0.001f) return;

            RumbleManager.Instance.Rumble(playerDeviceIndex, rumbleLow, rumbleHigh, duration);

            if (GameSession.Instance != null && GameSession.Instance.IsSplitScreen) return;

            _freezeRoutine = StartCoroutine(FreezeRoutine(duration, Mathf.Clamp01(timeScale)));
        }

        public void CancelActiveFreeze()
        {
            if (!_frozen) return;
            if (_freezeRoutine != null) { StopCoroutine(_freezeRoutine); _freezeRoutine = null; }
            Time.timeScale      = _restoreTimeScale > 0.001f ? _restoreTimeScale : 1f;
            Time.fixedDeltaTime = _restoreFixedDelta;
            _frozen = false;
        }

        IEnumerator FreezeRoutine(float duration, float timeScale)
        {
            _frozen = true;
            _restoreTimeScale  = Time.timeScale;
            _restoreFixedDelta = Time.fixedDeltaTime;

            Time.timeScale = timeScale;
            // Keep physics steps proportional so the slow-mo stays stable
            Time.fixedDeltaTime = _restoreFixedDelta * Mathf.Max(timeScale, 0.0001f);

            yield return new WaitForSecondsRealtime(duration);

            if (Mathf.Approximately(Time.timeScale, timeScale))
            {
                Time.timeScale      = _restoreTimeScale > 0.001f ? _restoreTimeScale : 1f;
                Time.fixedDeltaTime = _restoreFixedDelta;
            }
            _frozen = false;
            _freezeRoutine = null;
        }

        void OnDestroy()
        {
            if (_frozen)
            {
                Time.timeScale      = _restoreTimeScale > 0.001f ? _restoreTimeScale : 1f;
                Time.fixedDeltaTime = _restoreFixedDelta;
            }
            if (Instance == this) Instance = null;
        }
    }
}
