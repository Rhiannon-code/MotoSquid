using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    [RequireComponent(typeof(Image))]
    public class LogoIntroReveal : MonoBehaviour
    {
        public enum Style
        {
            Fade,
            WipeRight,
            Splatter
        }

        [Header("How this layer arrives")]
        public Style style = Style.Fade;

        [Header("Seconds to wait, then seconds to play")]
        public float delay;
        public float duration = 0.8f;

        [Header("Splatter overshoots to this alpha before settling")]
        [Range(1f, 3f)] public float splatterFlash = 1.8f;

        Image image;
        float baseAlpha;
        float elapsed;

        void Awake()
        {
            image = GetComponent<Image>();
            baseAlpha = image.color.a;

            if (style == Style.WipeRight)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            Apply(0f);
        }

        void Update()
        {
            if (elapsed >= delay + duration) return;

            elapsed += Time.unscaledDeltaTime;
            Apply(duration <= 0f ? 1f : Mathf.Clamp01((elapsed - delay) / duration));
        }

        void Apply(float t)
        {
            Color c = image.color;

            switch (style)
            {
                case Style.WipeRight:
                    image.fillAmount = Mathf.SmoothStep(0f, 1f, t);
                    c.a = baseAlpha * Mathf.Clamp01(t * 4f);
                    break;

                case Style.Splatter:
                    float punch = t < 0.35f ? Mathf.Lerp(0f, splatterFlash, t / 0.35f)
                                            : Mathf.Lerp(splatterFlash, 1f, (t - 0.35f) / 0.65f);
                    c.a = Mathf.Clamp01(baseAlpha * punch);
                    break;

                default:
                    c.a = baseAlpha * Mathf.SmoothStep(0f, 1f, t);
                    break;
            }

            image.color = c;
        }
    }
}
