// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkrilStudio.RES2
{
    [RequireComponent(typeof(Vehicle))]
    public class VehicleInput : MonoBehaviour
    {
        private Vehicle _vehicle;
        private Engine _engine;
        private Drivetrain _drivetrain;
        private VehiclePreset presetConfig;

        [Header("Input Settings")]
        public float throttleSpeed = 5f;
        public float brakePower = 600f;

        [Header("Auto Gearbox")]
        public float upShiftRPM = 6500f;
        public float downShiftRPM = 4600f;
        public enum VehicleType
        {
            StreetCar,
            SportsCar,
            Racecar,
            FormulaRacecar,
            HyperCar,
            Truck,
            Bike
        }
        [SerializeField] public VehicleType vehicleType = VehicleType.StreetCar;
        public Key acceleration = Key.W;
        public Key brake = Key.S;
        public Key shiftUp = Key.LeftShift;
        public Key shiftDown = Key.LeftCtrl;

        public bool isManual = false;
        private bool gasPressed;
        private bool gasButtonHeldDown;
        private bool brakePressed;
        float _rollingBraking;

        private void Awake()
        {
            _vehicle = GetComponent<Vehicle>();
            _engine = _vehicle.engine;
            _drivetrain = _vehicle.drivetrain;
        }

        private void Start()
        {
            presetConfig = GetConfiguration(vehicleType);
            _vehicle.Init(presetConfig);
            _vehicle.mass = presetConfig.mass;
            _rollingBraking = _engine.rollingBraking;
        }

        private void Update()
        {
            HandleThrottle(); // accel input handling
            HandleBrake(); // brake input handling

            if (!isManual) // automatically upshifts and downshifts, based on the acceleration UI button state
            {
                AutoDriver();
                if (gasButtonHeldDown)
                    gasPressed = true;
                else
                    gasPressed = false;
            }
            else
            {
                HandleGearInput(); // manual shifts
            }
        }
        public void ChangeVehiclePreset (int i)
        {
            vehicleType = (VehicleType)i;
            presetConfig = GetConfiguration(vehicleType);
            _vehicle.Init(presetConfig);
            _vehicle.mass = presetConfig.mass;
            _rollingBraking = _engine.rollingBraking;
        }
        public VehiclePreset GetConfiguration(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.StreetCar:
                    return VehiclePresets.StreetCar; // id 0

                case VehicleType.SportsCar:
                    return VehiclePresets.SportsCar; // id 1

                case VehicleType.Racecar:
                    return VehiclePresets.Racecar; // id 2

                case VehicleType.FormulaRacecar:
                    return VehiclePresets.FormulaRacecar; // id 3

                case VehicleType.HyperCar:
                    return VehiclePresets.HyperCar; // id 4

                case VehicleType.Truck:
                    return VehiclePresets.Truck; // id 5

                case VehicleType.Bike:
                    return VehiclePresets.Bike; // id 6

                default:
                    return VehiclePresets.StreetCar;
            }
        }

        private void HandleThrottle()
        {
            // downshifting rev match blip
            if (_drivetrain.downShift)
            {
                _engine.throttle = 0.8f;
                return;
            }

            // release gas during shifting
            if (_drivetrain.isShifting)
            {
                _engine.throttle = Mathf.MoveTowards(_engine.throttle, 0f, 10f * Time.deltaTime);
                return;
            }

            if (!gasButtonHeldDown)
                gasPressed = Keyboard.current[acceleration].isPressed; // used for manual shifts

            if (gasPressed)
                _engine.throttle = Mathf.Clamp01(_engine.throttle + throttleSpeed * Time.deltaTime);
            else
                _engine.throttle = Mathf.Clamp01(_engine.throttle - throttleSpeed * Time.deltaTime);
        }
        private void HandleBrake()
        {
            brakePressed = Keyboard.current[brake].isPressed;

            if (brakePressed)
                _engine.rollingBraking = _rollingBraking + brakePower; // apply brake power
            else
                _engine.rollingBraking = _rollingBraking; // restore braking to original value
        }
        private void HandleGearInput()
        {
            if (Keyboard.current[shiftUp].wasPressedThisFrame)
                _drivetrain.NextGear();
            if (Keyboard.current[shiftDown].wasPressedThisFrame)
                _drivetrain.PrevGear();
        }
        private void AutoDriver () // automatically upshifts and downshifts, based on the acceleration button state
        {
            if (gasPressed)
            {
                if (_engine.rpm >
                    upShiftRPM)
                {
                    if (_drivetrain.gear <
                        _drivetrain.gears.Length)
                    {
                        _drivetrain.NextGear();
                    }
                }
            }
            else
            {
                if (_engine.rpm <
                    downShiftRPM)
                {
                    if (_drivetrain.gear > 0)
                    {
                        _drivetrain.PrevGear();
                    }
                }
            }
        }
        public void Acceleration()
        {
            gasButtonHeldDown = true;
        }
        public void Deceleration()
        {
            gasButtonHeldDown = false;
        }
    }
}
