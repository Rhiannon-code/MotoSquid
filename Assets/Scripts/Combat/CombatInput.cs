using MotoSquid.Controls;
using MotoSquid.Race;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Combat
{
public class CombatInput : MonoBehaviour
{
    public CombatSystem combatSystem;

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private int  playerDeviceIndex = -1;
    [SerializeField] private bool lockToKeyboard    = false;
    [SerializeField] private bool allowKeyboard      = false;

    private InputAction _attackLeft, _attackRight;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("CombatInput has no Input Actions asset on " + gameObject.name +
                           ", this racer will not attack.", this);
            enabled = false;
            return;
        }
        inputActions = Instantiate(inputActions);
        var map      = inputActions.FindActionMap("Combat", throwIfNotFound: true);
        _attackLeft  = map.FindAction("AttackLeft",  throwIfNotFound: true);
        _attackRight = map.FindAction("AttackRight", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _attackLeft?.Enable();
        _attackRight?.Enable();
    }

    private void OnDisable()
    {
        _attackLeft?.Disable();
        _attackRight?.Disable();
    }

    private RaceManager _raceManager;
    private bool _lookedForRaceManager;

    // Nothing stopped Q/E firing from the moment the scene loaded, so both players could brawl on the
    // grid through the whole countdown. Racing state is the same gate the bike's own inputs use.
    private bool RaceHasStarted()
    {
        if (!_lookedForRaceManager)
        {
            _raceManager = FindFirstObjectByType<RaceManager>();
            _lookedForRaceManager = true;
        }

        return _raceManager == null || _raceManager.State == RaceManager.RaceState.Racing;
    }

    private void Update()
    {
        if (combatSystem == null) return;
        if (!RaceHasStarted()) return;

        if (WasPressedThisFrame(_attackLeft))
            combatSystem.TryPunch(-1);

        if (WasPressedThisFrame(_attackRight))
            combatSystem.TryPunch(1);
    }

    // Single player still spawns a P2 rig, and every device filter resolves to "some device is
    // allowed", so slot 2 has to be silenced outright rather than filtered.
    bool _inputDisabled;
    public void DisableInput() => _inputDisabled = true;
    public void EnableInput()  => _inputDisabled = false;

    // Both players run this from the same prefab with the default "any device" filter, so either pad
    // or the keyboard fired both racers' attacks at once.
    public void SetDeviceFilter(bool lockKb, int deviceIndex, bool allowKb = false)
    {
        lockToKeyboard    = lockKb;
        playerDeviceIndex = deviceIndex;
        allowKeyboard     = allowKb;
    }

    // Returns true only if the action was pressed this frame from an allowed device
    bool WasPressedThisFrame(InputAction action)
    {
        if (_inputDisabled) return false;
        return InputDeviceFilter.PressedThisFrame(action, lockToKeyboard,
                                                           playerDeviceIndex, allowKeyboard);
    }
}
}
