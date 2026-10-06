using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MotoSquid.UI
{
    [RequireComponent(typeof(HorizontalSelector))]
    public class SelectorPadInput : MonoBehaviour
    {
        public float repeatDelay = 0.35f;
        public float deadzone    = 0.6f;

        HorizontalSelector _selector;
        float _nextRepeat;
        bool  _wasNeutral = true;

        void Awake() => _selector = GetComponent<HorizontalSelector>();

        void Update()
        {
            var pad = Gamepad.current;
            if (pad == null || _selector == null) return;
            if (EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject != gameObject) { _wasNeutral = true; return; }

            float axis = pad.dpad.x.ReadValue();
            if (Mathf.Abs(axis) < deadzone) axis = pad.leftStick.x.ReadValue();

            if (Mathf.Abs(axis) < deadzone) { _wasNeutral = true; return; }

            // A fresh press moves immediately, a held direction repeats: waiting out the cooldown on the
            // first press is what made the selectors feel unresponsive.
            if (!_wasNeutral && Time.unscaledTime < _nextRepeat) return;

            if (axis > 0f) _selector.NextItem();
            else           _selector.PreviousItem();

            _wasNeutral = false;
            _nextRepeat = Time.unscaledTime + repeatDelay;
        }
    }
}
