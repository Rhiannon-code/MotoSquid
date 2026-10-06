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
    public class Engine : MonoBehaviour
    {
        [Header("Base Settings")]
        public float idle = 1000f;
        public float limiter = 7000f;
        public float softLimiter = 6950f;

        [Header("Inertia")]
        public float inertia = 1.0f;

        [Header("Torque")]
        public float torque = 400f;
        public float engineBraking = 200f;
        public float rollingBraking = 100f;

        [Header("Wobble")]
        // Oscillation frequency (Hz)
        public float wobbleFrequency = 8f;
        // Damping speed: higher values result in faster damping (hu: Csillapodás sebessége: nagyobb érték = gyorsabb csillapodás)
        public float wobbleDamping = 3f;
        // Amplitude multiplier: determines the magnitude of the RPM fluctuation.
        public float wobbleAmplitude = 200f;

        public Drivetrain drivetrain;

        // Runtime state
        [HideInInspector] public float throttle = 0f;
        [HideInInspector] public float rpm = 0f;
        [HideInInspector] public float theta = 0f;
        [HideInInspector] public float omega = 0f;
        [HideInInspector] public float wobbleRpm = 0f;
        [HideInInspector] public float realRpm = 0f;

        // Transient oscillator state
        private float _wobblePhase = 0f;
        private float _wobbleEnvelope = 0f; // Current amplitude (decays exponentially)

        public void Init(EngineConfig config = null)
        {
            if (config != null) config.Apply(this);
            if (softLimiter <= 0f) softLimiter = limiter * 0.99f;

            theta = 0f;
            omega = idle * (2f * Mathf.PI) / 60f;
            rpm = idle;
            wobbleRpm = 0f;
            _wobblePhase = 0f;
            _wobbleEnvelope = 0f;
        }

        // Wobble trigger: called on gear changes or sudden load changes.
        // The amplitude is set proportionally to the difference between the engine omega and drivetrain omega.
        public void TriggerWobble(float omegaDelta)
        {
            float _wobbleAmplitude;
            if (drivetrain.downShift)
                _wobbleAmplitude = wobbleAmplitude / 4; // reduce wobble amplitude during downshifts for realism
            else
                _wobbleAmplitude = wobbleAmplitude;
            // The amplitude is determined by the magnitude of the omega change.
            _wobbleEnvelope = Mathf.Abs(omegaDelta) * _wobbleAmplitude; //wobbleAmplitude;
            _wobblePhase = 0f;
        }

        public void Integrate(float loadInertia, float dt, bool inGear = false)
        {
            float t = throttle;

            if (rpm >= softLimiter)
            {
                float r = Mathf.Clamp01((rpm - softLimiter) / Mathf.Max(limiter - softLimiter, 1f));
                t *= Mathf.Pow(1f - r, 0.05f);
            }

            if (rpm >= limiter)
                t = 0f;

            float idleTorque = 0f;
            if (t < 0.1f && rpm < idle * 1.5f)
            {
                float idleRatio = Mathf.Clamp01((rpm - idle * 0.9f) / (idle * 0.1f));
                idleTorque = (1f - idleRatio) * engineBraking * 10f;
            }

            float t1 = Mathf.Pow(t, 1.2f) * torque;
            float t2 = Mathf.Pow(1f - t, 1.2f) * engineBraking;
            float rollingT = (inGear && t < 0.1f) ? rollingBraking : 0f;
            float netTorque = t1 - t2 + idleTorque - rollingT;

            float I = Mathf.Max(0.0001f, loadInertia + inertia);
            omega += (netTorque / I) * dt;
            theta += omega * dt;

            rpm = omega * 60f / (2f * Mathf.PI);

            if (rpm < idle)
            {
                rpm = idle;
                omega = idle * (2f * Mathf.PI) / 60f;
            }

            // Wobble oscillator update
            _wobbleEnvelope *= Mathf.Exp(-wobbleDamping * dt);
            _wobblePhase += wobbleFrequency * 2f * Mathf.PI * dt;
            wobbleRpm = Mathf.Sin(_wobblePhase) * _wobbleEnvelope;

            // real rpm (engine + drivetrain)
            realRpm = rpm + wobbleRpm;
        }
        public void EngineAngularConstraint(Drivetrain drivetrain, float h)
        {
            if (drivetrain.gear == 0) return;

            // Position correction
            float c = drivetrain.theta - theta;
            if (Mathf.Abs(c) > 0.0001f)
            {
                float compliance = Mathf.Max(0.0006f - 0.00015f * drivetrain.gear, 0.00007f);
                float w = c * c / inertia;
                theta += c * (c / (w + compliance / (h * h)));
            }

            // Velocity correction
            float damping = drivetrain.gear > 3 ? 9f : 12f;
            omega += (drivetrain.omega - omega) * Mathf.Clamp01(damping * h);
        }
    }
}
