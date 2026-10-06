using UnityEngine;

namespace MotoSquid.UI
{
    public class LogoParallaxRig : MonoBehaviour
    {
        [Header("Sway reached at depth 1, in canvas pixels")]
        public Vector2 sway = new Vector2(26f, 16f);

        [Header("Seconds per cycle, kept unequal so the drift never visibly repeats")]
        public float periodX = 7.3f;
        public float periodY = 5.1f;

        [Header("How much nearer layers grow, as a fraction, at depth 1")]
        [Range(0f, 0.05f)] public float scalePerDepth = 0.012f;

        [Header("Seconds spent easing the sway up from rest")]
        public float easeIn = 1.5f;

        LogoParallaxLayer[] layers;
        float elapsed;

        void Start()
        {
            layers = GetComponentsInChildren<LogoParallaxLayer>(true);
        }

        void Update()
        {
            if (layers == null) return;

            elapsed += Time.unscaledDeltaTime;

            float ease = easeIn > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / easeIn)) : 1f;
            float tau = Mathf.PI * 2f;

            Vector2 offset = new Vector2(
                Mathf.Sin(elapsed * tau / Mathf.Max(0.01f, periodX)) * sway.x,
                Mathf.Sin(elapsed * tau / Mathf.Max(0.01f, periodY) + 1.3f) * sway.y) * ease;

            for (int i = 0; i < layers.Length; i++)
                layers[i].Apply(offset, scalePerDepth * ease);
        }
    }
}
