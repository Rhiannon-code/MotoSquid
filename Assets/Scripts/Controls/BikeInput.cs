using MotoSquid.Bike;
using MotoSquid.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Controls
{
public class BikeInput : MonoBehaviour
{
    public BikeController bikeControllerRhiannon;

    [SerializeField] private InputActionAsset inputActions;

    // -1 = all devices 0+ = specific gamepad only
    [SerializeField] private int  playerDeviceIndex = -1;
    // When true, locks input to keyboard only
    [SerializeField] private bool lockToKeyboard    = false;
    // Single player: one player holds a pad and the keyboard at once. Never set in split screen.
    [SerializeField] private bool allowKeyboard      = false;

    private InputAction _accelerate, _reverse, _handBrake, _steeringLeft, _steeringRight, _wheelie;

    private void Awake()
    {
        // Unassigned, Instantiate(null) throws before anything says which object is misconfigured
        if (inputActions == null)
        {
            Debug.LogError("BikeInput has no Input Actions asset on " + gameObject.name +
                           ", this bike will not respond to input.", this);
            enabled = false;
            return;
        }
        inputActions   = Instantiate(inputActions);
        // Pick up the player's saved control rebinds on this per player copy of the asset
        ControlRebindManager.ApplySavedOverrides(inputActions);
        var map        = inputActions.FindActionMap("Bike", throwIfNotFound: true);
        _accelerate    = map.FindAction("Accelerate",    throwIfNotFound: true);
        _reverse       = map.FindAction("Reverse",       throwIfNotFound: true);
        _handBrake     = map.FindAction("HandBrake",     throwIfNotFound: true);
        _steeringLeft  = map.FindAction("SteeringLeft",  throwIfNotFound: true);
        _steeringRight = map.FindAction("SteeringRight", throwIfNotFound: true);
        _wheelie       = map.FindAction("Wheelie",       throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _accelerate?.Enable();
        _reverse?.Enable();
        _handBrake?.Enable();
        _steeringLeft?.Enable();
        _steeringRight?.Enable();
        _wheelie?.Enable();
    }

    private void OnDisable()
    {
        _accelerate?.Disable();
        _reverse?.Disable();
        _handBrake?.Disable();
        _steeringLeft?.Disable();
        _steeringRight?.Disable();
        _wheelie?.Disable();
    }

    [System.NonSerialized] public bool accelOnly = true;
    [System.NonSerialized] public bool preRaceInputLocked = false;

    // Single player still has a P2 rig in the scene. Every device filter resolves to "some device is
    // allowed", so without an explicit off P2 reads the same keyboard as P1 and mirrors every input
    // as a ghost bike on the grid.
    bool _inputDisabled;
    public void DisableInput() => _inputDisabled = true;
    public void EnableInput()  => _inputDisabled = false;

    public void SetFullControl() { accelOnly = false; preRaceInputLocked = false; }
    public void LockForPreRace()    => preRaceInputLocked = true;
    public void UnlockForCountdown() => preRaceInputLocked = false;

    // Accessibility, latched steering state for Toggle Steer (tap to hold a direction)
    float _toggleSteerLeft, _toggleSteerRight;
    bool  _prevSteerLeftDown, _prevSteerRightDown;

    private void Update()
    {
        if (bikeControllerRhiannon == null) return;

        // Locked through the camera fly in, feed zero input so nothing revs or burns out early
        if (preRaceInputLocked || _inputDisabled)
        {
            bikeControllerRhiannon.ProvideInput(0f, 0f, 0f, 0f, 0f, 0f);
            return;
        }

        float accel    = IsPressed(_accelerate);
        float reverse  = IsPressed(_reverse);
        float handBrk  = accelOnly ? 0f : IsPressed(_handBrake);
        float steerL   = accelOnly ? 0f : ReadAxis(_steeringLeft);
        float steerR   = accelOnly ? 0f : ReadAxis(_steeringRight);
        float wheelie  = accelOnly ? 0f : IsPressed(_wheelie);

        var acc = AccessibilityManager.Instance;
        if (acc != null && !accelOnly)
        {
            // Toggle Steer, a fresh press latches that direction on, pressing it again releases
            if (acc.ToggleSteer)
            {
                bool lDown = steerL > 0.5f, rDown = steerR > 0.5f;
                if (lDown && !_prevSteerLeftDown)  { _toggleSteerLeft  = _toggleSteerLeft  > 0f ? 0f : 1f; _toggleSteerRight = 0f; }
                if (rDown && !_prevSteerRightDown) { _toggleSteerRight = _toggleSteerRight > 0f ? 0f : 1f; _toggleSteerLeft  = 0f; }
                _prevSteerLeftDown = lDown; _prevSteerRightDown = rDown;
                steerL = _toggleSteerLeft; steerR = _toggleSteerRight;
            }
            else if (_toggleSteerLeft != 0f || _toggleSteerRight != 0f)
            {
                _toggleSteerLeft = _toggleSteerRight = 0f;   // clear latch when option turned off
            }

            if (acc.SteeringAssist > 0f)
            {
                float sens = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(acc.SteeringAssist));
                steerL *= sens; steerR *= sens;
            }

            if (acc.AutoBrake && accel <= 0f && reverse <= 0f)
                handBrk = Mathf.Max(handBrk, 1f);
        }

        bikeControllerRhiannon.ProvideInput(accel, reverse, handBrk, steerL, steerR, wheelie);
    }

    // Returns 1 if the action is pressed from an allowed device, else 0
    float IsPressed(InputAction action) =>
        InputDeviceFilter.Pressed(action, lockToKeyboard, playerDeviceIndex, allowKeyboard)
            ? 1f : 0f;

    float ReadAxis(InputAction action) =>
        InputDeviceFilter.Magnitude(action, lockToKeyboard, playerDeviceIndex, allowKeyboard);

    public void SetDeviceFilter(bool lockKb, int deviceIndex, bool allowKb = false)
    {
        lockToKeyboard    = lockKb;
        playerDeviceIndex = deviceIndex;
        allowKeyboard     = allowKb;
    }

    // Exposed so feedback systems (e.g. RumbleManager) can target this player's gamepad
    public int PlayerDeviceIndex => playerDeviceIndex;
}
}
