using MotoSquid.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public class BoostAudioLoop : MonoBehaviour
    {
        [Header("References (found on this bike if left empty)")]
        public BoostSystem boostSystem;
        public AudioMixerGroup output;

        [Header("Clips")]
        public AudioClip loopClip;
        public AudioClip releaseClip;

        [Header("Loop")]
        [Range(0f, 1f)] public float loopVolume = 0.8f;
        public float fadeInTime    = 0.1f;
        public float fadeOutTime   = 0.35f;
        public float startPitch    = 0.9f;
        public float peakPitch     = 1.15f;
        public float pitchRampTime = 1.5f;

        [Header("Release")]
        [Range(0f, 1f)] public float releaseVolume = 0.7f;

        [Header("3D")]
        [Range(0f, 1f)] public float spatialBlend = 0.7f;
        public float minDistance = 3f;
        public float maxDistance = 60f;
        public float spread      = 45f;

        AudioSource m_Loop;
        AudioSource m_Release;
        bool  m_Boosting;
        float m_HeldFor;

        void Start()
        {
            if (boostSystem == null)
                boostSystem = GetComponentInParent<BoostSystem>(true);

            if (boostSystem == null)
            {
                Debug.LogError("BoostAudioLoop: no BoostSystem found in parents.", this);
                enabled = false;
                return;
            }

            m_Loop    = CreateSource("Boost Loop", loopClip, true);
            m_Release = CreateSource("Boost Release", null, false);

            boostSystem.OnBoostStart += HandleBoostStart;
            boostSystem.OnBoostEnd   += HandleBoostEnd;
        }

        void OnDestroy()
        {
            if (boostSystem == null) return;
            boostSystem.OnBoostStart -= HandleBoostStart;
            boostSystem.OnBoostEnd   -= HandleBoostEnd;
        }

        void OnDisable()
        {
            m_Boosting = false;
            m_HeldFor  = 0f;
            if (m_Loop != null)
            {
                m_Loop.volume = 0f;
                m_Loop.Stop();
            }
        }

        void HandleBoostStart()
        {
            if (m_Loop == null || m_Loop.clip == null) return;

            m_HeldFor    = 0f;
            m_Boosting   = true;
            m_Loop.pitch = startPitch;
            AudioLoop.EnsurePlaying(m_Loop, "boost loop", this);
        }

        void HandleBoostEnd()
        {
            m_Boosting = false;
            if (releaseClip != null && m_Release != null)
                m_Release.PlayOneShot(releaseClip, releaseVolume);
                AudioBurstLog.Note("boost release", this);
        }

        void Update()
        {
            if (m_Loop == null) return;

            if (m_Boosting)
            {
                m_HeldFor   += Time.deltaTime;
                m_Loop.pitch = Mathf.Lerp(startPitch, peakPitch,
                                          pitchRampTime > 0f ? Mathf.Clamp01(m_HeldFor / pitchRampTime) : 1f);
            }

            float target = m_Boosting ? loopVolume : 0f;
            float fade   = m_Boosting ? fadeInTime : fadeOutTime;
            m_Loop.volume = fade > 0f
                ? Mathf.MoveTowards(m_Loop.volume, target, loopVolume / fade * Time.deltaTime)
                : target;

            if (!m_Boosting && m_Loop.volume <= 0f && m_Loop.isPlaying)
                m_Loop.Stop();
        }

        AudioSource CreateSource(string sourceName, AudioClip clip, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);

            var s = go.AddComponent<AudioSource>();
            s.clip         = clip;
            s.loop         = loop;
            s.playOnAwake  = false;

            s.volume       = loop ? 0f : 1f;
            s.spatialBlend = spatialBlend;
            s.minDistance  = minDistance;
            s.maxDistance  = maxDistance;
            s.rolloffMode  = AudioRolloffMode.Logarithmic;
            s.spread       = spread;
            if (output != null) s.outputAudioMixerGroup = output;
            return s;
        }
    }
}
