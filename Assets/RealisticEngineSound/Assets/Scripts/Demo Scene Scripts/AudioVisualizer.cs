// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using UnityEngine;
using UnityEngine.UI;

namespace SkrilStudio.RES2
{
    public class AudioVisualizer : MonoBehaviour
    {
        [Header("Bars")]
        public RectTransform[] bars;
        public Image extraItem;

        [Header("Spectrum")]
        public int sampleSize = 64;
        public float heightMultiplier = 2000f;
        public float minHeight = 2f;
        public float maxHeight = 250f;

        [Header("RPM")]
        public float currentRPM;
        public float maxRPM = 7000f;

        [Header("Colors")]
        public Color idleColor = Color.cyan;
        public Color midColor = Color.magenta;
        public Color highColor = Color.red;

        [Header("RPM Simulator")]
        public Slider sliderRpmValue;

        private float[] spectrum;

        private void Start()
        {
            spectrum = new float[sampleSize];
        }

        private void Update()
        {
            AudioListener.GetSpectrumData(
                spectrum,
                0,
                FFTWindow.BlackmanHarris);

            currentRPM = sliderRpmValue.value;

            UpdateColor();
            UpdateBars();
        }

        private void UpdateColor()
        {
            float rpm01 = Mathf.Clamp01(currentRPM / maxRPM);

            Color targetColor;

            if (rpm01 < 0.5f)
            {
                targetColor = Color.Lerp(
                    idleColor,
                    midColor,
                    rpm01 * 2f);
            }
            else
            {
                targetColor = Color.Lerp(
                    midColor,
                    highColor,
                    (rpm01 - 0.5f) * 2f);
            }

            foreach (RectTransform bar in bars)
            {
                Image image = bar.GetComponent<Image>();

                if (image != null)
                    image.color = targetColor;

                if (extraItem != null)
                {
                    Color alphaColor = targetColor;
                    alphaColor.a = rpm01/1.5f;
                    extraItem.color = alphaColor;
                }
            }
        }

        private void UpdateBars()
        {
            int count = Mathf.Min(bars.Length, spectrum.Length);

            for (int i = 0; i < count; i++)
            {
                Vector2 size = bars[i].sizeDelta;

                size.y = Mathf.Clamp(
                    spectrum[i] * heightMultiplier,
                    minHeight,
                    maxHeight);

                bars[i].sizeDelta = size;
            }
        }
    }
}
