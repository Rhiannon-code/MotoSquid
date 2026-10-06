using MotoSquid.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public class BurnoutAudio : MonoBehaviour
    {
        [SerializeField] AudioClip loop;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField] float fadeInTime  = 0.08f;
        [SerializeField] float fadeOutTime = 0.25f;
        [SerializeField] AudioMixerGroup output;
        [SerializeField] float spatialBlend = 1f;
        [SerializeField] float minDistance  = 6f;
        [SerializeField] float maxDistance  = 60f;
        [SerializeField] float spread       = 45f;
        [SerializeField] float localPlayerSpatialBlend = 0.55f;
        [SerializeField] float localPlayerMinDistance  = 14f;

        IBikeAudioState _state;
        AudioSource     _source;

        void Awake()
        {
            _state  = GetComponentInParent<IBikeAudioState>();

            _source = new GameObject("Burnout Loop").AddComponent<AudioSource>();
            _source.transform.SetParent(transform, false);
            bool local = GetComponent<BikeAudioController>() is BikeAudioController bike
                         && bike.isLocalPlayer;

            _source.spatialBlend = local ? localPlayerSpatialBlend : spatialBlend;
            _source.minDistance  = local ? localPlayerMinDistance  : minDistance;
            _source.maxDistance  = maxDistance;
            _source.rolloffMode  = AudioRolloffMode.Logarithmic;
            _source.spread       = spread;

            _source.clip        = loop;
            _source.loop        = true;
            _source.playOnAwake = false;
            _source.volume      = 0f;
            if (output != null) _source.outputAudioMixerGroup = output;
            AudioLoop.RouteToGroup(_source, "Burnout");
        }

        void Update()
        {
            if (_state == null || _source.clip == null) return;

            bool  burning = _state.IsDoingBurnout;
            float fade    = burning ? fadeInTime : fadeOutTime;
            _source.volume = Mathf.MoveTowards(_source.volume, burning ? volume : 0f,
                                               Time.deltaTime / Mathf.Max(0.01f, fade));

            if (_source.volume > 0f) AudioLoop.EnsurePlaying(_source, "burnout loop", this);
            else if (_source.volume <= 0f && _source.isPlaying) _source.Stop();
        }
    }
}
