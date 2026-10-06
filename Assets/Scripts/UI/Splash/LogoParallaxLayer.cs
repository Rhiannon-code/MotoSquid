using UnityEngine;

namespace MotoSquid.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class LogoParallaxLayer : MonoBehaviour
    {
        [Header("How far this layer floats above the backdrop. 0 is pinned to it, 1 drifts most")]
        [Range(0f, 1f)] public float depth;

        RectTransform rt;
        Vector2 home;

        void Awake()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
        }

        public void Apply(Vector2 sway, float scalePerDepth)
        {
            if (rt == null) return;

            rt.anchoredPosition = home + sway * depth;

            float scale = 1f + scalePerDepth * depth;
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
