using UnityEngine.InputSystem;

namespace MotoSquid.Controls
{
    public static class InputDeviceFilter
    {
        // padIndex < 0 means any gamepad. allowKeyboard lets one player hold a pad and the keyboard at
        // once, which is what single player wants and split screen must never do
        public static bool Allows(InputDevice device, bool lockToKeyboard, int padIndex,
                                  bool allowKeyboard, bool allowMouse = false)
        {
            if (device == null) return false;
            if (lockToKeyboard) return device is Keyboard || (allowMouse && device is Mouse);

            if (device is Keyboard) return allowKeyboard;
            // Strictly allowMouse: falling back to allowKeyboard here handed the mouse to every player
            // at once, so one mouse drove both split screen cameras
            if (device is Mouse)    return allowMouse;

            if (padIndex >= 0 && padIndex < Gamepad.all.Count) return device == Gamepad.all[padIndex];
            return true;
        }

        public static bool Pressed(InputAction action, bool lockToKeyboard, int padIndex,
                                   bool allowKeyboard, bool allowMouse = false)
        {
            if (action == null || !action.IsPressed()) return false;

            foreach (var control in action.controls)
                if (Allows(control.device, lockToKeyboard, padIndex, allowKeyboard, allowMouse)
                    && control.IsActuated(0.01f))
                    return true;

            return false;
        }

        // How far the allowed device's controls are pushed, 0-1, for axes. Going through Pressed first gated
        // a stick on the 0.5 button press point, so the first half of the stick did nothing and steering
        // jumped straight to half lock, which is what made analogue steering feel digital
        public static float Magnitude(InputAction action, bool lockToKeyboard, int padIndex,
                                      bool allowKeyboard, bool allowMouse = false)
        {
            if (action == null) return 0f;

            float best = 0f;
            foreach (var control in action.controls)
                if (Allows(control.device, lockToKeyboard, padIndex, allowKeyboard, allowMouse))
                    best = UnityEngine.Mathf.Max(best, control.EvaluateMagnitude());
            return UnityEngine.Mathf.Clamp01(best);
        }

        public static bool PressedThisFrame(InputAction action, bool lockToKeyboard, int padIndex,
                                            bool allowKeyboard, bool allowMouse = false)
        {
            if (action == null || !action.WasPressedThisFrame()) return false;
            return Allows(action.activeControl?.device, lockToKeyboard, padIndex, allowKeyboard, allowMouse);
        }
    }
}
