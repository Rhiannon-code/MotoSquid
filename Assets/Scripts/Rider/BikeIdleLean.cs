using UnityEngine;

namespace MotoSquid.Rider
{
    public static class BikeIdleLean
    {
        public static float Angle(float idleLeanAngle, float fadeOutSpeed, float speed, float uprightBlend01 = 0f)
        {
            if (idleLeanAngle == 0f) return 0f;

            float moving = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, fadeOutSpeed));
            return idleLeanAngle * (1f - moving) * (1f - moving) * (1f - Mathf.Clamp01(uprightBlend01));
        }
    }
}
