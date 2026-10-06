using UnityEngine;

namespace MotoSquid.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class LogoCoverFit : MonoBehaviour
    {
        [Header("Size the artwork was authored at, in canvas pixels")]
        public Vector2 designSize = new Vector2(5120f, 2160f);

        [Header("Margin so parallax never drags an artwork edge into view")]
        [Range(1f, 1.2f)] public float overscan = 1.02f;

        RectTransform rt;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            Apply();
        }

        void Apply()
        {
            RectTransform parent = rt != null ? rt.parent as RectTransform : null;
            if (parent == null || designSize.x <= 0f || designSize.y <= 0f) return;

            rt.sizeDelta = designSize;

            Rect area = parent.rect;
            float scale = Mathf.Max(area.width / designSize.x, area.height / designSize.y) * overscan;
            if (rt.localScale.x != scale) rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
