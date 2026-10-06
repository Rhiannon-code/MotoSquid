using MotoSquid.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Traffic
{
    public class TrafficEngineSound : MonoBehaviour
    {
        public static bool GlobalMuted = false;

        public float idlePitch = 0.7f;
        public float maxPitch  = 1.6f;

        public float idleVolume   = 0.35f;
        public float movingVolume = 1f;

        public float movingThreshold = 0.5f;
        public float volumeSmooth    = 6f;

        [Header("3D / culling")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 5f;
        public float maxDistance = 120f;
        public float spread      = 22f;
        public float maxAudibleDistance = 150f;
        public AudioMixerGroup output;

        private SplineMover _mover;
        private AudioSource _src;
        private float       _refMaxSpeed = 40f; 

        // Shared across the fleet: without it, a scene that is briefly missing a listener has every
        // traffic car running a scene-wide search every frame
        static Transform s_listener;
        static int       s_listenerSearchFrame = -1;

        static Transform Listener()
        {
            if (s_listener == null && s_listenerSearchFrame != Time.frameCount)
            {
                s_listenerSearchFrame = Time.frameCount;
                var al = FindFirstObjectByType<AudioListener>();
                s_listener = al != null ? al.transform : null;
            }
            return s_listener;
        }

        public void Init(SplineMover mover, AudioClip engineClip, float referenceMaxSpeed)
        {
            _mover       = mover;
            _refMaxSpeed = Mathf.Max(1f, referenceMaxSpeed);

            if (_src == null)
            {
                _src = gameObject.AddComponent<AudioSource>();
                _src.loop         = true;
                _src.playOnAwake  = false;
                _src.spatialBlend = spatialBlend;
                _src.minDistance  = minDistance;
                _src.maxDistance  = maxDistance;
                _src.rolloffMode  = AudioRolloffMode.Linear;
                _src.spread       = spread;
                if (output != null) _src.outputAudioMixerGroup = output;
            }

            if (engineClip != null && _src.clip != engineClip)
            {
                _src.clip = engineClip;
                _src.Play();
            }

            _src.pitch  = idlePitch;
            _src.volume = idleVolume;
        }

        private void Update()
        {
            if (_src == null || _mover == null || _src.clip == null) return;

            _src.mute = GlobalMuted;

            if (maxAudibleDistance > 0f)
            {
                Transform listener = Listener();
                if (listener != null)
                {
                    bool audible = (transform.position - listener.position).sqrMagnitude
                                   <= maxAudibleDistance * maxAudibleDistance;
                    if (_src.enabled != audible) _src.enabled = audible;
                    if (!audible) return;
                }
            }

            // Disabling the source for distance (or reusing a pooled car with the same clip) stops it
            AudioLoop.EnsurePlaying(_src, "traffic engine restart", this);

            float speed  = _mover.Velocity.magnitude; 
            float band01 = Mathf.Clamp01(speed / _refMaxSpeed);

            _src.pitch = Mathf.Lerp(idlePitch, maxPitch, band01);
            float targetVol = speed > movingThreshold ? Mathf.Lerp(idleVolume, movingVolume, band01) : idleVolume;
            _src.volume = Mathf.MoveTowards(_src.volume, targetVol, volumeSmooth * Time.deltaTime);
        }
    }
}
