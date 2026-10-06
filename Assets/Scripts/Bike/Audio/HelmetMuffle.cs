using MotoSquid.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public class HelmetMuffle : MonoBehaviour
    {
        static readonly string[] CutoffParams =
        {
            "HelmetCutoffLocal", "HelmetCutoffOpponents", "HelmetCutoffTraffic", "HelmetCutoffWorld"
        };

        [SerializeField] AudioMixer mixer;
        [SerializeField] float openCutoff     = 22000f;
        [SerializeField] float muffledCutoff  = 2200f;
        [SerializeField] float blendTime      = 0.25f;

        float _blend;
        float _lastWritten = -1f;

        void Start() => Write(0f, true);

        void Update()
        {
            if (mixer == null) return;

            var bike = BikeAudioController.LocalPlayer;
            bool muffled = bike != null
                        && bike.IsFirstPersonView
                        && (AccessibilityManager.Instance == null
                            || AccessibilityManager.Instance.HelmetAudio);

            _blend = Mathf.MoveTowards(_blend, muffled ? 1f : 0f,
                                       Time.unscaledDeltaTime / Mathf.Max(0.01f, blendTime));
            Write(_blend, false);
        }

        void OnDisable() => Write(0f, true);

        void Write(float blend, bool force)
        {
            if (mixer == null) return;

            // Cutoff is lerped in log space, otherwise the sweep dawdles at the top of the range
            // and slams shut at the bottom
            float hz = Mathf.Exp(Mathf.Lerp(Mathf.Log(openCutoff), Mathf.Log(muffledCutoff), blend));
            if (!force && Mathf.Abs(hz - _lastWritten) < 1f) return;

            foreach (string p in CutoffParams) mixer.SetFloat(p, hz);
            _lastWritten = hz;
        }
    }
}
