using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    [RequireComponent(typeof(Image))]
    public class LogoGlitch : MonoBehaviour
    {
        [Header("Seconds of quiet between bursts")]
        public Vector2 idleRange = new Vector2(2.2f, 6f);

        [Header("Seconds a burst lasts")]
        public Vector2 burstRange = new Vector2(0.05f, 0.16f);

        [Header("How far the colour ghosts split, in canvas pixels")]
        public float splitPixels = 14f;

        [Header("Ghost opacity during a burst")]
        [Range(0f, 1f)] public float ghostAlpha = 0.45f;

        Image image;
        RectTransform redGhost;
        RectTransform cyanGhost;
        Image redImage;
        Image cyanImage;

        float timer;
        float burstLeft;

        void Awake()
        {
            image = GetComponent<Image>();

            redGhost = MakeGhost("GlitchGhostRed", new Color(1f, 0.15f, 0.2f), out redImage);
            cyanGhost = MakeGhost("GlitchGhostCyan", new Color(0.2f, 0.9f, 1f), out cyanImage);

            timer = Random.Range(idleRange.x, idleRange.y);
        }

        RectTransform MakeGhost(string name, Color tint, out Image ghostImage)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            ghostImage = go.AddComponent<Image>();
            ghostImage.sprite = image.sprite;
            ghostImage.type = image.type;
            ghostImage.raycastTarget = false;
            ghostImage.color = new Color(tint.r, tint.g, tint.b, 0f);
            return rect;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;

            if (burstLeft > 0f)
            {
                burstLeft -= Time.unscaledDeltaTime;
                if (burstLeft <= 0f) Settle();
                else Jitter();
            }
            else if (timer <= 0f)
            {
                burstLeft = Random.Range(burstRange.x, burstRange.y);
                timer = Random.Range(idleRange.x, idleRange.y) + burstLeft;
            }
        }

        void Jitter()
        {
            float split = splitPixels * Random.Range(0.4f, 1f);
            float drift = splitPixels * 0.3f * Random.Range(-1f, 1f);

            redGhost.anchoredPosition = new Vector2(-split, drift);
            cyanGhost.anchoredPosition = new Vector2(split, -drift);

            SetGhostAlpha(ghostAlpha * Random.Range(0.6f, 1f));
        }

        void Settle()
        {
            redGhost.anchoredPosition = Vector2.zero;
            cyanGhost.anchoredPosition = Vector2.zero;
            SetGhostAlpha(0f);
        }

        void SetGhostAlpha(float a)
        {
            Color r = redImage.color; r.a = a; redImage.color = r;
            Color c = cyanImage.color; c.a = a; cyanImage.color = c;
        }
    }
}
