using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Users;

namespace MotoSquid.UI
{
    [DefaultExecutionOrder(-50)]
    public class MenuControllerBinder : MonoBehaviour
    {
        [SerializeField] private GameObject firstSelected;
        [SerializeField] private bool restrictToFirstGamepad = true;

        EventSystem _eventSystem;

        void Start()
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem == null)
            {
                Debug.LogError("[MenuControllerBinder] No EventSystem in the scene, this menu cannot be " +
                               "navigated by anything.", this);
                return;
            }

            SelectFirst();

            if (restrictToFirstGamepad) RestrictToPlayerOne();
        }

        void SelectFirst()
        {
            GameObject target = firstSelected != null ? firstSelected : _eventSystem.firstSelectedGameObject;

            if (target == null)
            {
                Debug.LogWarning("[MenuControllerBinder] Nothing to select: assign First Selected, or the " +
                                 "menu will only respond to a mouse.", this);
                return;
            }

            _eventSystem.firstSelectedGameObject = target;
            _eventSystem.SetSelectedGameObject(target);
        }

        void RestrictToPlayerOne()
        {
            var module = _eventSystem.currentInputModule as InputSystemUIInputModule;
            if (module == null || module.actionsAsset == null) return;

            // No pad: leave the module alone so keyboard and mouse still drive everything
            if (Gamepad.all.Count == 0) return;

            var pad  = Gamepad.all[0];
            var user = InputUser.PerformPairingWithDevice(pad);

            if (Keyboard.current != null) InputUser.PerformPairingWithDevice(Keyboard.current, user);
            if (Mouse.current    != null) InputUser.PerformPairingWithDevice(Mouse.current,    user);

            user.AssociateActionsWithUser(module.actionsAsset);

            Debug.Log($"[MenuControllerBinder] Menu bound to P1 pad '{pad.displayName}' " +
                      $"({Gamepad.all.Count} connected).", this);
        }
    }
}
