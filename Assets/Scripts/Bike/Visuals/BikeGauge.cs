using UnityEngine;

namespace MotoSquid.Bike
{
    public static class BikeGauge
    {
        public const int FrameCount = 11;

        const int BoostFirstFrame = 8;
        const int RedlineDropBack = 3;

        public static float SpeedKmh(BikeController bike) =>
            bike.canMove ? bike.localBikeVelocity.magnitude * 3.6f : 0f;

        public static float TargetFrame(BikeController bike, BoostSystem boost)
        {
            if (!bike.canMove)
                return Mathf.Clamp01(bike.EngineRpm01) * (FrameCount - 0.0001f);

            int   gear    = Mathf.Max(1, bike.currentGear);
            int[] speeds  = bike.gearSpeeds;
            float gearMin = gear > 1 ? speeds[gear - 2] : 0f;
            float gearMax = gear <= speeds.Length ? speeds[gear - 1] : bike.MaxSpeedKmh;
            float revT    = Mathf.Clamp01((SpeedKmh(bike) - gearMin) / Mathf.Max(1f, gearMax - gearMin));

            return boost != null && boost.isBoosting
                ? BoostFirstFrame + revT * 2.9999f
                : revT * (BoostFirstFrame - 0.0001f);
        }

        public static int DisplayFrame(float smoothedFrame, BikeController bike, float flashPeriod)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(smoothedFrame), 0, FrameCount - 1);

            if (frame >= BoostFirstFrame && bike.EngineAtLimiter &&
                Mathf.Repeat(Time.unscaledTime, flashPeriod) < flashPeriod * 0.5f)
                frame = Mathf.Clamp(frame - RedlineDropBack, 0, FrameCount - 1);

            return frame;
        }
    }
}
