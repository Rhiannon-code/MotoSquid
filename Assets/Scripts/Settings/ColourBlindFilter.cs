using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.Settings
{
    public class ColourBlindFilter : MonoBehaviour
    {
        public enum ColourBlindMode
        {
            None          = 0,
            Protanopia    = 1, // Red blind
            Deuteranopia  = 2, // Green blind
            Tritanopia    = 3, // Blue blind
            Achromatopsia = 4  // Total colour blindness (greyscale)
        }

        static readonly Color[] TintColours =
        {
            new Color(0f, 0f, 0f, 0f),           // None, fully transparent
            new Color(0.6f, 0.2f, 0.0f, 0.18f),  // Protanopia, warm amber tint
            new Color(0.2f, 0.5f, 0.1f, 0.18f),  // Deuteranopia, muted green tint
            new Color(0.1f, 0.3f, 0.6f, 0.18f),  // Tritanopia, cool blue tint
            new Color(0f, 0f, 0f, 0f),           // Achromatopsia, handled via saturation
        };

        Canvas          _canvas;
        Image           _overlay;
        bool            _highContrast;
        ColourBlindMode _currentMode = ColourBlindMode.None;

        void Awake()
        {
            BuildOverlayCanvas();
            DontDestroyOnLoad(gameObject);
        }

        void BuildOverlayCanvas()
        {
            var go = new GameObject("ColourBlindOverlay");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32767; // Always on top

            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>().blockingMask = 0; // Doesn't block input

            var imgGo = new GameObject("Tint");
            imgGo.transform.SetParent(go.transform, false);

            var rect = imgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _overlay = imgGo.AddComponent<Image>();
            _overlay.color        = Color.clear;
            _overlay.raycastTarget = false;
        }

        public void ApplyMode(ColourBlindMode mode)
        {
            _currentMode = mode;
            RefreshOverlay();
        }

        public void SetHighContrast(bool enabled)
        {
            _highContrast = enabled;
            RefreshOverlay();
        }

        void RefreshOverlay()
        {
            if (_overlay == null) return;

            Color tint = TintColours[(int)_currentMode];


            if (_highContrast)
                tint.a = Mathf.Min(tint.a + 0.15f, 0.85f);

            _overlay.color = tint;
        }
    }
}
