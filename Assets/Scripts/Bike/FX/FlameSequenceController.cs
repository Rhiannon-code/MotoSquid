using MotoSquid.Controls;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace MotoSquid.Bike
{
    [ExecuteInEditMode]
    public class FlameSequenceController : MonoBehaviour
    {
        [Header("References")]
        public VisualEffect vfx;

        [Header("Burst")]
        public float burstIntensity = 1f;
        public float burstFlameSize = 4.7f;
        [Range(1, 200)] public float burstSpawnCount = 3f;
        public float burstRiseHeight = 2f;
        public float burstDuration = 0.8f;

        [Header("Dissipation")]
        public float dissipationDuration = 1.2f;

        [Header("Timer")]
        public bool useTimer = true;
        public float interval = 10f;
        public float initialDelay = 2f;

        [Header("Playback")]
        public bool playOnEnable = false;

        [Header("Range Activation")]
        public float activationRange = 300f;

        private Vector3 vfxOriginLocalPosition;
        private Coroutine sequenceCoroutine;
        private Coroutine timerCoroutine;
        private static readonly int OnPlayID = Shader.PropertyToID("OnPlay");

        private Transform[] _playerTransforms;
        private bool _isActive;

        void Awake()
        {
            if (vfx == null)
                vfx = GetComponentInChildren<VisualEffect>();
        }

        void OnValidate()
        {
            if (vfx == null)
                vfx = GetComponentInChildren<VisualEffect>();
        }

        void OnEnable()
        {
            if (vfx == null)
                vfx = GetComponentInChildren<VisualEffect>();

            if (!Application.isPlaying)
            {
                if (vfx != null) vfx.enabled = false;
                return;
            }

            if (vfx != null) vfx.enabled = false;
            StartCoroutine(StaggeredStart());
        }

        void OnDisable()
        {
            if (timerCoroutine    != null) { StopCoroutine(timerCoroutine);    timerCoroutine    = null; }
            if (sequenceCoroutine != null) { StopCoroutine(sequenceCoroutine); sequenceCoroutine = null; }
            ResetVFX();
            _isActive = false;
        }

        private IEnumerator StaggeredStart()
        {
            yield return new WaitForSeconds(Random.Range(0f, initialDelay));
            CachePlayerTransforms();
            StartCoroutine(RangeCheckLoop());
        }

        private void CachePlayerTransforms()
        {
            var inputs = FindObjectsByType<BikeInput>(FindObjectsSortMode.None);
            _playerTransforms = new Transform[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
                _playerTransforms[i] = inputs[i].transform;
        }

        private IEnumerator RangeCheckLoop()
        {
            var wait = new WaitForSeconds(0.5f);
            while (true)
            {
                // Lazy re cache handles the case where bikes spawn after this script starts
                if (_playerTransforms == null || _playerTransforms.Length == 0)
                    CachePlayerTransforms();

                bool inRange = activationRange <= 0f || IsPlayerInRange();

                if (inRange && !_isActive)
                    Activate();
                else if (!inRange && _isActive)
                    Deactivate();

                yield return wait;
            }
        }

        private bool IsPlayerInRange()
        {
            if (_playerTransforms == null) return false;
            float rangeSq = activationRange * activationRange;
            foreach (var pt in _playerTransforms)
            {
                if (pt == null) continue;
                if ((pt.position - transform.position).sqrMagnitude <= rangeSq)
                    return true;
            }
            return false;
        }

        private void Activate()
        {
            _isActive = true;
            if (vfx != null)
            {
                vfx.enabled = true;
                vfxOriginLocalPosition = vfx.transform.localPosition;
                vfx.Play();
            }

            if (useTimer)
                timerCoroutine = StartCoroutine(TimerLoop());
            else if (playOnEnable)
                Play();
        }

        private void Deactivate()
        {
            _isActive = false;
            if (timerCoroutine    != null) { StopCoroutine(timerCoroutine);    timerCoroutine    = null; }
            if (sequenceCoroutine != null) { StopCoroutine(sequenceCoroutine); sequenceCoroutine = null; }
            ResetVFX();
            if (vfx != null) vfx.enabled = false;
        }

        private IEnumerator TimerLoop()
        {
            yield return new WaitForSeconds(initialDelay);
            while (true)
            {
                Play();
                yield return new WaitForSeconds(interval);
            }
        }

        public void Play()
        {
            if (sequenceCoroutine != null)
                StopCoroutine(sequenceCoroutine);

            if (vfx != null)
                vfxOriginLocalPosition = vfx.transform.localPosition;

            sequenceCoroutine = StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            if (vfx == null) yield break; 

            vfx.transform.localPosition = vfxOriginLocalPosition;

            ApplyVFXProperties(burstIntensity, burstFlameSize, burstSpawnCount);
            vfx.SendEvent(OnPlayID);

            Vector3 riseTarget = vfxOriginLocalPosition + Vector3.up * burstRiseHeight;
            float burstTimer = 0f;

            while (burstTimer < burstDuration)
            {
                burstTimer += Time.deltaTime;
                vfx.transform.localPosition = Vector3.Lerp(vfxOriginLocalPosition, riseTarget, EaseOutQuad(burstTimer / burstDuration));
                yield return null;
            }

            vfx.transform.localPosition = riseTarget;

            float dissipTimer = 0f;
            while (dissipTimer < dissipationDuration)
            {
                dissipTimer += Time.deltaTime;
                float t = dissipTimer / dissipationDuration;
                ApplyVFXProperties(
                    Mathf.Lerp(burstIntensity, 0f, t),
                    burstFlameSize,
                    Mathf.Lerp(burstSpawnCount, 0f, t));
                yield return null;
            }

            ApplyVFXProperties(0f, 0f, 0f);
            vfx.transform.localPosition = vfxOriginLocalPosition;
            sequenceCoroutine = null;
        }

        private void ApplyVFXProperties(float intensity, float flameSize, float spawnCount)
        {
            if (vfx == null) return;
            vfx.SetFloat("Fire Intensity", intensity);
            vfx.SetFloat("FlameSize", flameSize);
            vfx.SetFloat("FireSpawn_Count", spawnCount);
        }

        private void ResetVFX()
        {
            if (vfx == null) return;
            ApplyVFXProperties(0f, 0f, 0f);
            vfx.Stop();
            vfx.transform.localPosition = vfxOriginLocalPosition;
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
