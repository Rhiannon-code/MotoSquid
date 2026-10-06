// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using UnityEngine;
using UnityEngine.UI;

namespace SkrilStudio
{
    public class UISwitch : MonoBehaviour
    {
        public Toggle toggle;
        public RectTransform handle;
        public Image background;

        public Color onColor = new Color(0.0f, 0.7f, 1f);
        public Color offColor = Color.gray;

        Vector2 onPos = new Vector2(20, 0);
        Vector2 offPos = new Vector2(-20, 0);

        private void Start()
        {
            UpdateVisuals(toggle.isOn);

            toggle.onValueChanged.AddListener(UpdateVisuals);
        }

        void UpdateVisuals(bool isOn)
        {
            handle.anchoredPosition = isOn ? onPos : offPos;
            background.color = isOn ? onColor : offColor;
        }
    }
}