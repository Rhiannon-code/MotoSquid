using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using Michsky.UI.Heat;

namespace MotoSquid.UI
{
    public class ControlsCard : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField] private Image  card;
        [SerializeField] private Sprite keyboardCard;
        [SerializeField] private Sprite gamepadCard;

        [Header("Hidden while the card is up")]
        [SerializeField] private GameObject[] hideWhileShown;

        IDisposable _pressSubscription;
        bool _showingGamepad;

        void OnEnable()
        {
            foreach (var go in hideWhileShown)
                if (go != null) go.SetActive(false);

            Show(ResolveInitialScheme());

            // Switching on a real button press rather than on device presence: a gamepad left plugged in
            // must not override someone who is playing on the keyboard
            _pressSubscription = InputSystem.onAnyButtonPress.Call(OnAnyPress);
        }

        void OnDisable()
        {
            _pressSubscription?.Dispose();
            _pressSubscription = null;
        }

        static bool ResolveInitialScheme()
        {
            var heat = ControllerManager.instance;
            if (heat != null) return heat.gamepadEnabled;

            return Gamepad.current != null;
        }

        void OnAnyPress(InputControl control)
        {
            if (control.device is Gamepad)  Show(true);
            else if (control.device is Keyboard || control.device is Mouse) Show(false);
        }

        void Show(bool gamepad)
        {
            if (card == null) return;

            // The keyboard art is coming from someone else. Until it is dropped into Keyboard Card, the
            // gamepad card stands in for both rather than the loading screen showing nothing.
            Sprite wanted = gamepad || keyboardCard == null ? gamepadCard : keyboardCard;
            if (wanted == null || (card.sprite == wanted && _showingGamepad == gamepad)) return;

            _showingGamepad = gamepad;
            card.sprite     = wanted;
            card.enabled    = true;
        }
    }
}
