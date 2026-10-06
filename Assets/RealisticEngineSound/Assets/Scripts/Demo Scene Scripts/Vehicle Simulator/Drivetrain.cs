// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using UnityEngine;

namespace SkrilStudio.RES2
{
    public class Drivetrain : MonoBehaviour
    {
        [Header("Gears")]
        public float[] gears = { 3.99f, 2.65f, 1.81f, 1.39f, 1.16f, 1.00f, 0.83f };
        /// <todo>
        /// 
        /// add support for reverse gear
        /// 
        /// </todo>
        public float finalDrive = 3.62f;

        [Header("Drivetrain Physics")]
        public float inertia = 0.15f;
        public float damping = 4f;
        public float compliance = 0.01f;

        [Header("Shifting")]
        public float shiftTime = 0.25f;
        public float downShiftTime = 0.1f;

        [HideInInspector] public int gear = 0;
        [HideInInspector] public float clutch = 1f;
        [HideInInspector] public bool downShift = false;
        [HideInInspector] public bool isShifting = false;

        [HideInInspector] public float theta = 0f;
        [HideInInspector] public float omega = 0f;

        private int _pendingGear = 1;
        private float _shiftTimer = 0f;
        private float _shiftRatio = 1f;
        private bool _wasDownShift = false;

        public bool isDownShift => _wasDownShift && isShifting;

        public void Init(DrivetrainConfig config = null, Engine engine = null)
        {
            if (config != null) config.Apply(this);
            theta = 0f;
            omega = 0f;
            gear = 0;
            downShift = false;
            isShifting = false;
        }

        public void Integrate(float dt)
        {
            clutch = Mathf.Clamp01(clutch);
            theta += omega * dt;
        }
        
        public void DrivetrainAngularConstraint(Engine engine, float h)
        {
            if (gear == 0) return;

            // Position
            float c = engine.theta - theta;
            if (Mathf.Abs(c) > 0.0001f)
            {
                float w = c * c / inertia;
                theta += c * (c / (w + compliance / (h * h)));
            }

            // Velocity
            float d = gear > 3 ? damping * 0.75f : damping;
            float diff = Mathf.Abs(engine.omega - omega);
            omega += (engine.omega - omega) * Mathf.Clamp01(d * h * (1f + diff));
        }

        public float GetFinalDriveRatio() => finalDrive;

        public float GetGearRatio(int g = -1)
        {
            if (g < 0) g = gear;
            g = Mathf.Clamp(g, 0, gears.Length);
            return g > 0 ? gears[g - 1] : 0f;
        }

        public float GetTotalGearRatio() => GetGearRatio() * finalDrive;

        public void UpdateShift(Engine engine)
        {
            if (!isShifting) return;

            _shiftTimer -= Time.fixedDeltaTime;
            if (_shiftTimer > 0f) return;

            int prevGear = gear;
            gear = _pendingGear;

            float omegaBefore = engine.omega;

            if (prevGear == 0)
            {
                omega = engine.omega;
            }
            else
            {
                engine.omega *= _shiftRatio;
                engine.rpm = engine.omega * 60f / (2f * Mathf.PI);
                omega = engine.omega;
            }

            theta = engine.theta;

            // Wobble trigger, based on the magnitude of the omega change
            float omegaDelta = engine.omega - omegaBefore;
            engine.TriggerWobble(omegaDelta);

            downShift = false;
            _wasDownShift = false;
            isShifting = false;
        }

        public void NextGear() => ChangeGear(gear + 1);
        public void PrevGear() => ChangeGear(gear - 1);

        public void ChangeGear(int nextGear)
        {
            if (isShifting) return;
            nextGear = Mathf.Clamp(nextGear, 0, gears.Length);
            if (nextGear == gear) return;

            float prevRatio = GetGearRatio(gear);
            float nextRatio = GetGearRatio(nextGear);

            _shiftRatio = prevRatio > 0f ? nextRatio / prevRatio : 1f;
            _pendingGear = nextGear;
            downShift = nextRatio > prevRatio;
            _wasDownShift = downShift;
            isShifting = true;
            _shiftTimer = downShift ? downShiftTime : shiftTime;
        }
    }
}
