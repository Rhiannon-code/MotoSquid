// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using System;
using UnityEngine;

namespace SkrilStudio.RES2
{
    [Serializable]
    public class EngineConfig
    {
        public float idle = 1000f;
        public float limiter = 7000f;
        public float softLimiter = 0f;   // 0 = auto (limiter * 0.99)
        public float inertia = 1.0f;
        public float torque = 400f;
        public float engineBraking = 200f;
        public float rollingBraking = 200;
        public float wobbleFrequency = 8; // how fast is teh wobble
        public float wobbleDamping = 3; // Damping speed: higher values result in faster damping (hu: Csillapodás sebessége: nagyobb érték = gyorsabb csillapodás)
        public float wobbleAmplitude = 2; // higher value apply higher volume change

        public void Apply(Engine e)
        {
            e.idle = idle;
            e.limiter = limiter;
            e.softLimiter = softLimiter;
            e.inertia = inertia;
            e.torque = torque;
            e.engineBraking = engineBraking;
            e.rollingBraking = rollingBraking;
            e.wobbleFrequency = wobbleFrequency;
            e.wobbleDamping = wobbleDamping;
            e.wobbleAmplitude = wobbleAmplitude;
        }
    }

    [Serializable]
    public class DrivetrainConfig
    {
        public float[] gears = { 3.99f,
         2.65f,
         1.81f,
         1.39f,
         1.16f,
         1.00f,
         0.83f };
        public float finalDrive = 3.62f;
        public float inertia = 0.15f;
        public float damping = 12f;
        public float compliance = 0.01f;
        public float shiftTime = 0.25f;
        public float downShiftTimeMs = 0.15f;

        public void Apply(Drivetrain d)
        {
            d.gears = gears;
            d.finalDrive = finalDrive;
            d.inertia = inertia;
            d.damping = damping;
            d.compliance = compliance;
            d.shiftTime = shiftTime;
        }
    }

    [Serializable]
    public class VehiclePreset
    {
        public string name;
        public EngineConfig engine = new EngineConfig();
        public DrivetrainConfig drivetrain = new DrivetrainConfig();
        public float mass;
    }

    public static class VehiclePresets
    {
        public static VehiclePreset StreetCar => new VehiclePreset
        {
            name = "Street Car",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.4f,
                torque = 300,
                engineBraking = 200,
                rollingBraking = 400,
                wobbleFrequency = 8,
                wobbleDamping = 4,
                wobbleAmplitude = 3,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.25f,
                damping = 16f,
                finalDrive = 3.8f,
            },
            mass = 1800
        };

        public static VehiclePreset SportsCar => new VehiclePreset
        {
            name = "Sports Car",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.3f,
                torque = 400,
                engineBraking = 250,
                rollingBraking = 400,
                wobbleFrequency = 8,
                wobbleDamping = 3,
                wobbleAmplitude = 3,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.2f,
                damping = 6f,
                finalDrive = 3.52f,
            },
            mass = 1500
        };

        public static VehiclePreset Racecar => new VehiclePreset
        {
            name = "Race Car",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.12f,
                torque = 550,
                engineBraking = 250,
                rollingBraking = 800,
                wobbleFrequency = 8,
                wobbleDamping = 3,
                wobbleAmplitude = 5,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.1f,
                damping = 12f,
                finalDrive = 3.42f,
            },
            mass = 1300
        };
        public static VehiclePreset FormulaRacecar => new VehiclePreset
        {
            name = "Formula 1",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.1f,
                torque = 650,
                engineBraking = 400,
                rollingBraking = 800,
                wobbleFrequency = 12,
                wobbleDamping = 3,
                wobbleAmplitude = 4,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.1f,
                damping = 12f,
                finalDrive = 3.22f,
            },
            mass = 1000
        };
        public static VehiclePreset HyperCar => new VehiclePreset
        {
            name = "Hyper Car",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.2f,
                torque = 700,
                engineBraking = 400,
                rollingBraking = 800,
                wobbleFrequency = 8,
                wobbleDamping = 3,
                wobbleAmplitude = 4,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.1f,
                damping = 12f,
                finalDrive = 3.0f,
            },
            mass = 1500
        };
        public static VehiclePreset Truck => new VehiclePreset
        {
            name = "Truck",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 1.0f,
                torque = 3000,
                engineBraking = 3000,
                rollingBraking = 1200,
                wobbleFrequency = 4,
                wobbleDamping = 3,
                wobbleAmplitude = 3,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.8f,
                damping = 12f,
                finalDrive = 5f,
            },
            mass = 18000
        };
        public static VehiclePreset Bike => new VehiclePreset
        {
            name = "Bike",
            engine = new EngineConfig
            {
                limiter = 7000f,
                softLimiter = 6950f,
                inertia = 0.1f,
                torque = 150,
                engineBraking = 100,
                rollingBraking = 200,
                wobbleFrequency = 10,
                wobbleDamping = 6,
                wobbleAmplitude = 3,
            },
            drivetrain = new DrivetrainConfig
            {
                shiftTime = 0.1f,
                damping = 2f,
                finalDrive = 3.3f,
            },
            mass = 180
        };
    }
}
