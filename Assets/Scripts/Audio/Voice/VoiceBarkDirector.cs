using UnityEngine;

namespace MotoSquid.Audio
{

    public static class VoiceBarkDirector
    {
        const float GapAfterBark = 0.15f;
        const float MaxHold = 2.5f;

        static float _floorHeldUntil = -999f;
        static int   _activePriority = int.MinValue;

        public static bool IsBarkOnFloor => Time.unscaledTime < _floorHeldUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _floorHeldUntil = -999f;
            _activePriority = int.MinValue;
        }

        public static bool TryClaimFloor(int priority, float clipLength, bool allowInterrupt = true)
        {
            float now = Time.unscaledTime;

            bool floorFree = now >= _floorHeldUntil;
            bool outranks  = allowInterrupt && priority > _activePriority;
            if (!floorFree && !outranks) return false;

            // Capped at MaxHold only when lines may cut each other off, with interrupts off the cap let the
            // next line in after 2.5 s and clipped every longer one (lines run to 6.4 s)
            float hold = Mathf.Max(clipLength, 0.3f);
            if (allowInterrupt) hold = Mathf.Min(hold, MaxHold);
            _floorHeldUntil = now + hold + GapAfterBark;
            _activePriority = priority;
            return true;
        }
    }
}
