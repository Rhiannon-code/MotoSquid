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
    public class Vehicle : MonoBehaviour
    {
        [Header("References")]
        public Engine engine;
        public Drivetrain drivetrain;

        [Header("Vehicle")]
        public float mass = 1800f;
        public float wheelRadius = 0.25f;

        [Header("Simulation")]
        public int subSteps = 20;

        [HideInInspector] public float wheelRpm = 0f;
        [HideInInspector] public float speedMs = 0f;  // m/s
        [HideInInspector] public float speedKmh = 0f;  // km/h

        public void Init(VehiclePreset config)
        {
            engine.Init(config.engine);
            drivetrain.Init(config.drivetrain, engine);
        }

        private void FixedUpdate()
        {
            drivetrain.UpdateShift(engine);
            SimulationUpdate(Time.fixedDeltaTime);
        }

        public void SimulationUpdate(float dt)
        {
            if (dt == 0f) return;

            float h = dt / subSteps;
            float loadInertia = GetLoadInertia();

            for (int i = 0; i < subSteps; i++)
            {
                engine.Integrate(loadInertia, h, drivetrain.gear > 0);
                drivetrain.Integrate(h);

                engine.EngineAngularConstraint(drivetrain, h);
                drivetrain.DrivetrainAngularConstraint(engine, h);
            }
            //Debug.Log($"engineTheta={engine.theta:0.00} engineOmega={engine.omega:0.00} drivetrainTheta={drivetrain.theta:0.00} drivetrainOmega={drivetrain.omega:0.00} diff={engine.omega - drivetrain.omega:0.00} rpm={engine.rpm:0}");
            
            if (drivetrain.gear > 0)
            {
                float totalRatio = drivetrain.GetTotalGearRatio();
                wheelRpm = engine.rpm / totalRatio;
                speedMs = (wheelRpm / 60f) * 2f * Mathf.PI * wheelRadius;
                speedKmh = speedMs * 3.6f;
            }
            else
            {
                speedKmh = 0;
            }
        }

        public float GetLoadInertia()
        {
            if (drivetrain.gear == 0) return 0f;

            float gearRatio = drivetrain.GetGearRatio();
            float totalGearRatio = drivetrain.GetTotalGearRatio();

            float I_veh = mass * wheelRadius * wheelRadius;
            float I_wheels = 4f * 12f * wheelRadius * wheelRadius;

            float I1 = I_veh / (totalGearRatio * totalGearRatio);
            float I2 = I_wheels / (totalGearRatio * totalGearRatio);
            float I3 = drivetrain.inertia / (gearRatio * gearRatio);

            return I1 + I2 + I3;
        }
    }
}
