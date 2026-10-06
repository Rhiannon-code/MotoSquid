using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArcadeBP_Pro
{
    public class BikeInputProvider : MonoBehaviour
    {
        public ArcadeBikeControllerPro arcadeBikeControllerPro;

        [Header("Input Mode Selection")]
        [Tooltip("Select which input system to use")]
        public InputMode inputMode = InputMode.DirectKeys;

        public enum InputMode
        {
            DirectKeys,
            LegacyInputManager,
            NewInputSystem,
            Custom
        }

        [Header("Input Configuration")]
        public DirectKeyInputs directKeyInputs = new DirectKeyInputs();
        public LegacyInputManagerInputs legacyInputs = new LegacyInputManagerInputs();
        public NewInputSystemInputs newInputSystemInputs = new NewInputSystemInputs();
        public InputSettings inputSettings = new InputSettings();

        [System.Serializable]
        public class DirectKeyInputs
        {
            [Header("Keyboard Inputs")]
            [Tooltip("The key to accelerate the bike. Default is usually `KeyCode.W`.")]
            public KeyCode AccelerateKey = KeyCode.W;

            [Tooltip("The key to reverse the bike. Default is `KeyCode.S`.")]
            public KeyCode ReverseKey = KeyCode.S;

            [Tooltip("The key to apply the hand brake. Default is `KeyCode.Space`.")]
            public KeyCode HandBrakeKey = KeyCode.Space;

            [Tooltip("The key to steer the bike left. Default is `KeyCode.A`.")]
            public KeyCode SteeringLeftKey = KeyCode.A;

            [Tooltip("The key to steer the bike right. Default is `KeyCode.D`.")]
            public KeyCode SteeringRightKey = KeyCode.D;

            [Tooltip("The key to perform a wheelie. Default is usually `KeyCode.LeftShift`.")]
            public KeyCode WheelieKey = KeyCode.LeftShift;

            [Header("Mobile Inputs")]
            [Tooltip("UI button for accelerating the bike.")]
            public UiButton_ABP_Pro AccelerateButton;

            [Tooltip("UI button for reversing the bike.")]
            public UiButton_ABP_Pro ReverseButton;

            [Tooltip("UI button for applying the handbrake.")]
            public UiButton_ABP_Pro HandBrakeButton;

            [Tooltip("UI button for steering the bike to the left.")]
            public UiButton_ABP_Pro SteeringLeftButton;

            [Tooltip("UI button for steering the bike to the right.")]
            public UiButton_ABP_Pro SteeringRightButton;

            [Tooltip("UI button for performing a wheelie.")]
            public UiButton_ABP_Pro WheelieButton;

            [Header("Axis Simulation")]
            [Tooltip("Enable axis simulation for keyboard inputs (smooth acceleration/deceleration)")]
            public bool simulateAxis = false;

            [Tooltip("Speed of axis simulation (how fast input goes from 0 to 1)")]
            public float axisSimulationSpeed = 5f;
        }

        [System.Serializable]
        public class LegacyInputManagerInputs
        {
            [Header("Axis Names")]
            [Tooltip("Name of the acceleration axis in Input Manager")]
            public string accelerateAxis = "Vertical";

            [Tooltip("Name of the steering axis in Input Manager")]
            public string steeringAxis = "Horizontal";

            [Tooltip("Name of the handbrake axis/button in Input Manager")]
            public string handBrakeButton = "Jump";

            [Tooltip("Name of the wheelie button in Input Manager")]
            public string wheelieButton = "Fire1";

            [Header("Axis Configuration")]
            [Tooltip("Invert the acceleration axis")]
            public bool invertAcceleration = false;

            [Tooltip("Invert the steering axis")]
            public bool invertSteering = false;

            [Tooltip("Use separate axes for acceleration and braking")]
            public bool useSeparateAccelerationBraking = false;

            [Tooltip("Name of the brake axis (if using separate axes)")]
            public string brakeAxis = "Brake";
        }

        [System.Serializable]
        public class NewInputSystemInputs
        {
#if ENABLE_INPUT_SYSTEM
            [Header("Input Actions")]
            [Tooltip("Accelerate action - configure bindings in the inspector")]
            public InputAction accelerateAction;
            
            [Tooltip("Brake/Reverse action - configure bindings in the inspector")]
            public InputAction brakeAction;
            
            [Tooltip("Steering action - configure bindings in the inspector")]
            public InputAction steeringAction;
            
            [Tooltip("Handbrake action - configure bindings in the inspector")]
            public InputAction handBrakeAction;
            
            [Tooltip("Wheelie action - configure bindings in the inspector")]
            public InputAction wheelieAction;

            public NewInputSystemInputs()
            {
                // Initialize with default bindings
                accelerateAction = new InputAction("Accelerate", InputActionType.Value);
                accelerateAction.AddBinding("<Keyboard>/w");
                accelerateAction.AddBinding("<Gamepad>/rightTrigger");
                
                brakeAction = new InputAction("Brake", InputActionType.Value);
                brakeAction.AddBinding("<Keyboard>/s");
                brakeAction.AddBinding("<Gamepad>/leftTrigger");
                
                steeringAction = new InputAction("Steering", InputActionType.Value);
                steeringAction.AddCompositeBinding("1DAxis")
                    .With("positive", "<Keyboard>/d")
                    .With("negative", "<Keyboard>/a");
                steeringAction.AddBinding("<Gamepad>/leftStick/x");
                
                handBrakeAction = new InputAction("HandBrake", InputActionType.Button);
                handBrakeAction.AddBinding("<Keyboard>/space");
                handBrakeAction.AddBinding("<Gamepad>/buttonSouth");
                
                wheelieAction = new InputAction("Wheelie", InputActionType.Button);
                wheelieAction.AddBinding("<Keyboard>/leftShift");
                wheelieAction.AddBinding("<Gamepad>/buttonWest");
            }
#else
            [Header("New Input System Not Available")]
            [HelpBox("New Input System package is not installed. Install it via Package Manager to use this mode.", HelpBoxMessageType.Info)]
            public bool placeholderField;
#endif
        }

        [System.Serializable]
        public class InputSettings
        {
            [Header("Input Processing")]
            [Tooltip("Dead zone for analog inputs (values below this are treated as 0)")]
            [Range(0f, 0.5f)]
            public float deadZone = 0.1f;

            [Tooltip("Sensitivity multiplier for all inputs")]
            [Range(0.1f, 2f)]
            public float sensitivity = 1f;

            [Header("Steering")]
            [Tooltip("Apply smoothing to steering input")]
            public bool smoothSteering = true;

            [Tooltip("Steering smoothing speed")]
            public float steeringSmoothSpeed = 5f;

            [Tooltip("Maximum steering input value")]
            [Range(0.5f, 2f)]
            public float maxSteeringInput = 1f;

            [Header("Acceleration/Braking")]
            [Tooltip("Apply smoothing to acceleration input")]
            public bool smoothAcceleration = false;

            [Tooltip("Acceleration smoothing speed")]
            public float accelerationSmoothSpeed = 3f;

            [Header("Debug")]
            [Tooltip("Show current input values in inspector during play mode")]
            public bool showDebugValues = false;
        }

        // Current input values
        private float accelerateRaw, reverseRaw, handBrakeRaw, steeringLeftRaw, steeringRightRaw, wheelieRaw;
        private float accelerateSmooth, reverseSmooth, steeringSmooth;

        // Axis simulation values for DirectKeys mode
        private float accelerateAxisSim, reverseAxisSim, steerLeftAxisSim, steerRightAxisSim;

        [Header("Debug (Runtime Only)")]
        [SerializeField] private float debugAccelerate;
        [SerializeField] private float debugReverse;
        [SerializeField] private float debugSteering;
        [SerializeField] private float debugHandBrake;
        [SerializeField] private float debugWheelie;

        private void Awake()
        {
            if (!arcadeBikeControllerPro)
            {
                TryGetComponent(out arcadeBikeControllerPro);
            }

#if ENABLE_INPUT_SYSTEM
            // Initialize InputActions if they're null
            if (newInputSystemInputs.accelerateAction == null ||
                newInputSystemInputs.brakeAction == null ||
                newInputSystemInputs.steeringAction == null ||
                newInputSystemInputs.handBrakeAction == null ||
                newInputSystemInputs.wheelieAction == null)
            {
                newInputSystemInputs = new NewInputSystemInputs();
            }
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (inputMode == InputMode.NewInputSystem)
            {
                EnableNewInputSystemActions();
            }
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (inputMode == InputMode.NewInputSystem)
            {
                DisableNewInputSystemActions();
            }
#endif
        }

        private void Update()
        {
            if (!arcadeBikeControllerPro && inputMode != InputMode.Custom)
            {
                return;
            }

            if (inputMode != InputMode.Custom)
            {
                ResetRawInputs();
            }

            // Get raw inputs based on selected mode
            GetRawInputs();

            // Process inputs (smoothing, dead zones, etc.)
            ProcessInputs();

            // Send processed inputs to bike controller
            SendInputsToBike();

            // Update debug values
            if (inputSettings.showDebugValues)
            {
                UpdateDebugValues();
            }
        }

        private void GetRawInputs()
        {
            switch (inputMode)
            {
                case InputMode.DirectKeys:
                    GetDirectKeyInputs();
                    break;
                case InputMode.LegacyInputManager:
                    GetLegacyInputManagerInputs();
                    break;
                case InputMode.NewInputSystem:
                    GetNewInputSystemInputs();
                    break;
                case InputMode.Custom:
                    // Custom mode: inputs should be set externally via SetCustomInput method
                    break;
            }
        }

        private void GetDirectKeyInputs()
        {
            if (directKeyInputs.simulateAxis)
            {
                // Simulate axis behavior for keyboard inputs
                float targetAccel = (GetDirectKey(directKeyInputs.AccelerateKey) || directKeyInputs.AccelerateButton?.isPressed == true) ? 1f : 0f;
                float targetReverse = (GetDirectKey(directKeyInputs.ReverseKey) || directKeyInputs.ReverseButton?.isPressed == true) ? 1f : 0f;
                float targetSteerLeft = (GetDirectKey(directKeyInputs.SteeringLeftKey) || directKeyInputs.SteeringLeftButton?.isPressed == true) ? 1f : 0f;
                float targetSteerRight = (GetDirectKey(directKeyInputs.SteeringRightKey) || directKeyInputs.SteeringRightButton?.isPressed == true) ? 1f : 0f;

                accelerateAxisSim = Mathf.MoveTowards(accelerateAxisSim, targetAccel, Time.deltaTime * directKeyInputs.axisSimulationSpeed);
                reverseAxisSim = Mathf.MoveTowards(reverseAxisSim, targetReverse, Time.deltaTime * directKeyInputs.axisSimulationSpeed);
                steerLeftAxisSim = Mathf.MoveTowards(steerLeftAxisSim, targetSteerLeft, Time.deltaTime * directKeyInputs.axisSimulationSpeed);
                steerRightAxisSim = Mathf.MoveTowards(steerRightAxisSim, targetSteerRight, Time.deltaTime * directKeyInputs.axisSimulationSpeed);

                accelerateRaw = accelerateAxisSim;
                reverseRaw = reverseAxisSim;
                steeringLeftRaw = steerLeftAxisSim;
                steeringRightRaw = steerRightAxisSim;
            }
            else
            {
                // Direct on/off inputs
                accelerateRaw = (GetDirectKey(directKeyInputs.AccelerateKey) || directKeyInputs.AccelerateButton?.isPressed == true) ? 1f : 0f;
                reverseRaw = (GetDirectKey(directKeyInputs.ReverseKey) || directKeyInputs.ReverseButton?.isPressed == true) ? 1f : 0f;
                steeringLeftRaw = (GetDirectKey(directKeyInputs.SteeringLeftKey) || directKeyInputs.SteeringLeftButton?.isPressed == true) ? 1f : 0f;
                steeringRightRaw = (GetDirectKey(directKeyInputs.SteeringRightKey) || directKeyInputs.SteeringRightButton?.isPressed == true) ? 1f : 0f;
            }

            handBrakeRaw = (GetDirectKey(directKeyInputs.HandBrakeKey) || directKeyInputs.HandBrakeButton?.isPressed == true) ? 1f : 0f;
            wheelieRaw = (GetDirectKey(directKeyInputs.WheelieKey) || directKeyInputs.WheelieButton?.isPressed == true) ? 1f : 0f;
        }

        private static bool GetDirectKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        private void GetLegacyInputManagerInputs()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (legacyInputs.useSeparateAccelerationBraking)
            {
                accelerateRaw = Mathf.Max(0, Input.GetAxis(legacyInputs.accelerateAxis));
                reverseRaw = Mathf.Max(0, Input.GetAxis(legacyInputs.brakeAxis));
            }
            else
            {
                float verticalInput = Input.GetAxis(legacyInputs.accelerateAxis);
                if (legacyInputs.invertAcceleration) verticalInput = -verticalInput;
                
                accelerateRaw = Mathf.Max(0, verticalInput);
                reverseRaw = Mathf.Max(0, -verticalInput);
            }

            float steeringInput = Input.GetAxis(legacyInputs.steeringAxis);
            if (legacyInputs.invertSteering) steeringInput = -steeringInput;
            
            steeringRightRaw = Mathf.Max(0, steeringInput);
            steeringLeftRaw = Mathf.Max(0, -steeringInput);

            handBrakeRaw = Input.GetButton(legacyInputs.handBrakeButton) ? 1f : 0f;
            wheelieRaw = Input.GetButton(legacyInputs.wheelieButton) ? 1f : 0f;
#endif
        }

        private void GetNewInputSystemInputs()
        {
#if ENABLE_INPUT_SYSTEM
            if (newInputSystemInputs.accelerateAction != null)
                accelerateRaw = newInputSystemInputs.accelerateAction.ReadValue<float>();
            
            if (newInputSystemInputs.brakeAction != null)
                reverseRaw = newInputSystemInputs.brakeAction.ReadValue<float>();
            
            if (newInputSystemInputs.steeringAction != null)
            {
                float steeringInput = newInputSystemInputs.steeringAction.ReadValue<float>();
                steeringRightRaw = Mathf.Max(0, steeringInput);
                steeringLeftRaw = Mathf.Max(0, -steeringInput);
            }
            
            if (newInputSystemInputs.handBrakeAction != null)
                handBrakeRaw = newInputSystemInputs.handBrakeAction.ReadValue<float>();
            
            if (newInputSystemInputs.wheelieAction != null)
                wheelieRaw = newInputSystemInputs.wheelieAction.ReadValue<float>();
#endif
        }

        private void ProcessInputs()
        {
            float sensitivity = inputSettings != null ? inputSettings.sensitivity : 1f;
            float accelerationSmoothSpeed = inputSettings != null ? Mathf.Max(0f, inputSettings.accelerationSmoothSpeed) : 0f;
            float steeringSmoothSpeed = inputSettings != null ? Mathf.Max(0f, inputSettings.steeringSmoothSpeed) : 0f;
            float maxSteeringInput = inputSettings != null ? inputSettings.maxSteeringInput : 1f;

            // Apply dead zone
            accelerateRaw = ApplyDeadZone(accelerateRaw);
            reverseRaw = ApplyDeadZone(reverseRaw);
            steeringLeftRaw = ApplyDeadZone(steeringLeftRaw);
            steeringRightRaw = ApplyDeadZone(steeringRightRaw);
            handBrakeRaw = ApplyDeadZone(handBrakeRaw);
            wheelieRaw = ApplyDeadZone(wheelieRaw);

            // Apply sensitivity
            accelerateRaw *= sensitivity;
            reverseRaw *= sensitivity;
            steeringLeftRaw *= sensitivity;
            steeringRightRaw *= sensitivity;

            // Clamp values
            accelerateRaw = Mathf.Clamp01(accelerateRaw);
            reverseRaw = Mathf.Clamp01(reverseRaw);
            steeringLeftRaw = Mathf.Clamp01(steeringLeftRaw);
            steeringRightRaw = Mathf.Clamp01(steeringRightRaw);
            handBrakeRaw = Mathf.Clamp01(handBrakeRaw);
            wheelieRaw = Mathf.Clamp01(wheelieRaw);

            // Apply smoothing
            if (inputSettings != null && inputSettings.smoothAcceleration)
            {
                accelerateSmooth = Mathf.MoveTowards(accelerateSmooth, accelerateRaw, Time.deltaTime * accelerationSmoothSpeed);
                reverseSmooth = Mathf.MoveTowards(reverseSmooth, reverseRaw, Time.deltaTime * accelerationSmoothSpeed);
            }
            else
            {
                accelerateSmooth = accelerateRaw;
                reverseSmooth = reverseRaw;
            }

            if (inputSettings != null && inputSettings.smoothSteering)
            {
                float targetSteering = (steeringRightRaw - steeringLeftRaw) * maxSteeringInput;
                steeringSmooth = Mathf.MoveTowards(steeringSmooth, targetSteering, Time.deltaTime * steeringSmoothSpeed);
            }
            else
            {
                steeringSmooth = (steeringRightRaw - steeringLeftRaw) * maxSteeringInput;
            }
        }

        private float ApplyDeadZone(float value)
        {
            float deadZone = inputSettings != null ? inputSettings.deadZone : 0f;
            if (Mathf.Abs(value) < deadZone)
                return 0f;
            
            return value;
        }

        private void SendInputsToBike()
        {
            if (!arcadeBikeControllerPro)
            {
                return;
            }

            // For steering, we now send separate left/right values that support analog input
            float steeringLeft = steeringSmooth < 0 ? -steeringSmooth : 0f;
            float steeringRight = steeringSmooth > 0 ? steeringSmooth : 0f;

            arcadeBikeControllerPro.provideInput(
                accelerateSmooth,
                reverseSmooth,
                handBrakeRaw,
                steeringLeft,
                steeringRight,
                wheelieRaw
            );
        }

        private void UpdateDebugValues()
        {
            debugAccelerate = accelerateSmooth;
            debugReverse = reverseSmooth;
            debugSteering = steeringSmooth;
            debugHandBrake = handBrakeRaw;
            debugWheelie = wheelieRaw;
        }

        private void ResetRawInputs()
        {
            accelerateRaw = 0f;
            reverseRaw = 0f;
            handBrakeRaw = 0f;
            steeringLeftRaw = 0f;
            steeringRightRaw = 0f;
            wheelieRaw = 0f;
        }

        #region Public Methods

        [ContextMenu("Set Default Inputs")]
        private void SetDefaultInputs()
        {
            directKeyInputs.AccelerateKey = KeyCode.W;
            directKeyInputs.ReverseKey = KeyCode.S;
            directKeyInputs.HandBrakeKey = KeyCode.Space;
            directKeyInputs.SteeringLeftKey = KeyCode.A;
            directKeyInputs.SteeringRightKey = KeyCode.D;
            directKeyInputs.WheelieKey = KeyCode.LeftShift;

            legacyInputs.accelerateAxis = "Vertical";
            legacyInputs.steeringAxis = "Horizontal";
            legacyInputs.handBrakeButton = "Jump";
            legacyInputs.wheelieButton = "Fire1";

#if ENABLE_INPUT_SYSTEM
            SetupDefaultNewInputSystemBindings();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void RemoveAllBindings(InputAction action)
        {
            if (action == null) return;
            
            // Remove bindings in reverse order to avoid index shifting
            for (int i = action.bindings.Count - 1; i >= 0; i--)
            {
                action.ChangeBinding(i).Erase();
            }
        }

        [ContextMenu("Setup Keyboard Only (New Input System)")]
        private void SetupKeyboardOnlyBindings()
        {
            if (newInputSystemInputs.accelerateAction != null)
            {
                newInputSystemInputs.accelerateAction.Disable();
                RemoveAllBindings(newInputSystemInputs.accelerateAction);
                newInputSystemInputs.accelerateAction.AddBinding("<Keyboard>/w");
                newInputSystemInputs.accelerateAction.AddBinding("<Keyboard>/upArrow");
            }

            if (newInputSystemInputs.brakeAction != null)
            {
                newInputSystemInputs.brakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.brakeAction);
                newInputSystemInputs.brakeAction.AddBinding("<Keyboard>/s");
                newInputSystemInputs.brakeAction.AddBinding("<Keyboard>/downArrow");
            }

            if (newInputSystemInputs.steeringAction != null)
            {
                newInputSystemInputs.steeringAction.Disable();
                RemoveAllBindings(newInputSystemInputs.steeringAction);
                newInputSystemInputs.steeringAction.AddCompositeBinding("1DAxis")
                    .With("positive", "<Keyboard>/d")
                    .With("negative", "<Keyboard>/a");
                newInputSystemInputs.steeringAction.AddCompositeBinding("1DAxis")
                    .With("positive", "<Keyboard>/rightArrow")
                    .With("negative", "<Keyboard>/leftArrow");
            }

            if (newInputSystemInputs.handBrakeAction != null)
            {
                newInputSystemInputs.handBrakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.handBrakeAction);
                newInputSystemInputs.handBrakeAction.AddBinding("<Keyboard>/space");
            }

            if (newInputSystemInputs.wheelieAction != null)
            {
                newInputSystemInputs.wheelieAction.Disable();
                RemoveAllBindings(newInputSystemInputs.wheelieAction);
                newInputSystemInputs.wheelieAction.AddBinding("<Keyboard>/leftShift");
                newInputSystemInputs.wheelieAction.AddBinding("<Keyboard>/rightShift");
            }

            if (inputMode == InputMode.NewInputSystem && isActiveAndEnabled)
                EnableNewInputSystemActions();
        }

        [ContextMenu("Setup Gamepad Only (New Input System)")]
        private void SetupGamepadOnlyBindings()
        {
            if (newInputSystemInputs.accelerateAction != null)
            {
                newInputSystemInputs.accelerateAction.Disable();
                RemoveAllBindings(newInputSystemInputs.accelerateAction);
                newInputSystemInputs.accelerateAction.AddBinding("<Gamepad>/rightTrigger");
                newInputSystemInputs.accelerateAction.AddBinding("<Gamepad>/buttonEast");
            }

            if (newInputSystemInputs.brakeAction != null)
            {
                newInputSystemInputs.brakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.brakeAction);
                newInputSystemInputs.brakeAction.AddBinding("<Gamepad>/leftTrigger");
                newInputSystemInputs.brakeAction.AddBinding("<Gamepad>/buttonSouth");
            }

            if (newInputSystemInputs.steeringAction != null)
            {
                newInputSystemInputs.steeringAction.Disable();
                RemoveAllBindings(newInputSystemInputs.steeringAction);
                newInputSystemInputs.steeringAction.AddBinding("<Gamepad>/leftStick/x");
                newInputSystemInputs.steeringAction.AddBinding("<Gamepad>/dpad/x");
            }

            if (newInputSystemInputs.handBrakeAction != null)
            {
                newInputSystemInputs.handBrakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.handBrakeAction);
                newInputSystemInputs.handBrakeAction.AddBinding("<Gamepad>/buttonWest");
                newInputSystemInputs.handBrakeAction.AddBinding("<Gamepad>/leftShoulder");
            }

            if (newInputSystemInputs.wheelieAction != null)
            {
                newInputSystemInputs.wheelieAction.Disable();
                RemoveAllBindings(newInputSystemInputs.wheelieAction);
                newInputSystemInputs.wheelieAction.AddBinding("<Gamepad>/buttonNorth");
                newInputSystemInputs.wheelieAction.AddBinding("<Gamepad>/rightShoulder");
            }

            if (inputMode == InputMode.NewInputSystem && isActiveAndEnabled)
                EnableNewInputSystemActions();
        }

        [ContextMenu("Setup Default Bindings (New Input System)")]
        private void SetupDefaultNewInputSystemBindings()
        {
            // Reinitialize with default bindings
            var newInputs = new NewInputSystemInputs();
            
            if (newInputSystemInputs.accelerateAction != null)
            {
                newInputSystemInputs.accelerateAction.Disable();
                RemoveAllBindings(newInputSystemInputs.accelerateAction);
                foreach (var binding in newInputs.accelerateAction.bindings)
                {
                    newInputSystemInputs.accelerateAction.AddBinding(binding);
                }
            }

            if (newInputSystemInputs.brakeAction != null)
            {
                newInputSystemInputs.brakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.brakeAction);
                foreach (var binding in newInputs.brakeAction.bindings)
                {
                    newInputSystemInputs.brakeAction.AddBinding(binding);
                }
            }

            if (newInputSystemInputs.steeringAction != null)
            {
                newInputSystemInputs.steeringAction.Disable();
                RemoveAllBindings(newInputSystemInputs.steeringAction);
                foreach (var binding in newInputs.steeringAction.bindings)
                {
                    newInputSystemInputs.steeringAction.AddBinding(binding);
                }
            }

            if (newInputSystemInputs.handBrakeAction != null)
            {
                newInputSystemInputs.handBrakeAction.Disable();
                RemoveAllBindings(newInputSystemInputs.handBrakeAction);
                foreach (var binding in newInputs.handBrakeAction.bindings)
                {
                    newInputSystemInputs.handBrakeAction.AddBinding(binding);
                }
            }

            if (newInputSystemInputs.wheelieAction != null)
            {
                newInputSystemInputs.wheelieAction.Disable();
                RemoveAllBindings(newInputSystemInputs.wheelieAction);
                foreach (var binding in newInputs.wheelieAction.bindings)
                {
                    newInputSystemInputs.wheelieAction.AddBinding(binding);
                }
            }

            if (inputMode == InputMode.NewInputSystem && isActiveAndEnabled)
                EnableNewInputSystemActions();
        }
#endif

        public void SetCustomInput(float accelerate, float reverse, float steering, float handBrake, float wheelie)
        {
            if (inputMode != InputMode.Custom)
            {
                Debug.LogWarning("SetCustomInput called but input mode is not set to Custom!");
                return;
            }

            accelerateRaw = accelerate;
            reverseRaw = reverse;
            steeringRightRaw = Mathf.Max(0, steering);
            steeringLeftRaw = Mathf.Max(0, -steering);
            handBrakeRaw = handBrake;
            wheelieRaw = wheelie;
        }

        #endregion

        #region New Input System Helpers

#if ENABLE_INPUT_SYSTEM
        private void EnableNewInputSystemActions()
        {
            newInputSystemInputs.accelerateAction?.Enable();
            newInputSystemInputs.brakeAction?.Enable();
            newInputSystemInputs.steeringAction?.Enable();
            newInputSystemInputs.handBrakeAction?.Enable();
            newInputSystemInputs.wheelieAction?.Enable();
        }

        private void DisableNewInputSystemActions()
        {
            newInputSystemInputs.accelerateAction?.Disable();
            newInputSystemInputs.brakeAction?.Disable();
            newInputSystemInputs.steeringAction?.Disable();
            newInputSystemInputs.handBrakeAction?.Disable();
            newInputSystemInputs.wheelieAction?.Disable();
        }
#endif

        #endregion
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class HelpBoxAttribute : PropertyAttribute
    {
        public string text;
        public HelpBoxMessageType messageType;

        public HelpBoxAttribute(string text, HelpBoxMessageType messageType = HelpBoxMessageType.None)
        {
            this.text = text;
            this.messageType = messageType;
        }
    }

    public enum HelpBoxMessageType
    {
        None,
        Info,
        Warning,
        Error
    }
}
