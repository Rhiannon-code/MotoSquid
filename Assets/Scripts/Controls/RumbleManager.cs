using MotoSquid.Rider;
using MotoSquid.Settings;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Controls
{

    public class RumbleManager : MonoBehaviour
    {
        static RumbleManager _instance;

        public static RumbleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[RumbleManager]");
                    _instance = go.AddComponent<RumbleManager>();
                }
                return _instance;
            }
        }

        struct Pulse { public float low; public float high; public float deadline; }
        readonly Dictionary<Gamepad, Pulse> _active = new Dictionary<Gamepad, Pulse>();

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (AccessibilityManager.Instance != null)
                AccessibilityManager.Instance.OnRumbleChanged += HandleRumbleChanged;
        }

        void HandleRumbleChanged(bool enabled)
        {
            if (!enabled) StopAll();
        }

        public void Rumble(int playerDeviceIndex, float low, float high, float duration)
        {
            Gamepad pad = null;
            if (playerDeviceIndex >= 0 && playerDeviceIndex < Gamepad.all.Count)
                pad = Gamepad.all[playerDeviceIndex];
            else if (playerDeviceIndex < 0)
                pad = Gamepad.current;

            Rumble(pad, low, high, duration);
        }

        public void Rumble(Gamepad pad, float low, float high, float duration)
        {
            if (pad == null) return;
            if (AccessibilityManager.Instance != null &&
                !AccessibilityManager.Instance.RumbleEnabled) return;

            low  = Mathf.Clamp01(low);
            high = Mathf.Clamp01(high);
            if (duration <= 0f || (low <= 0f && high <= 0f)) return;

            float deadline = Time.unscaledTime + duration;

            if (_active.TryGetValue(pad, out var cur))
            {
                low      = Mathf.Max(low, cur.low);
                high     = Mathf.Max(high, cur.high);
                deadline = Mathf.Max(deadline, cur.deadline);
            }

            _active[pad] = new Pulse { low = low, high = high, deadline = deadline };
            pad.SetMotorSpeeds(low, high);
        }

        public void StopAll()
        {
            foreach (var pad in Gamepad.all)
                pad?.SetMotorSpeeds(0f, 0f);
            _active.Clear();
        }

        static readonly List<Gamepad> _expired = new List<Gamepad>();

        void Update()
        {
            if (_active.Count == 0) return;

            _expired.Clear();
            foreach (var kvp in _active)
                if (Time.unscaledTime >= kvp.Value.deadline)
                    _expired.Add(kvp.Key);

            foreach (var pad in _expired)
            {
                pad?.SetMotorSpeeds(0f, 0f);
                _active.Remove(pad);
            }
        }

        void OnApplicationPause(bool paused)   { if (paused) StopAll(); }
        void OnApplicationFocus(bool focused)  { if (!focused) StopAll(); }
        void OnApplicationQuit()               { StopAll(); }

        void OnDisable() { StopAll(); }

        void OnDestroy()
        {
            StopAll();
            if (AccessibilityManager.Instance != null)
                AccessibilityManager.Instance.OnRumbleChanged -= HandleRumbleChanged;
            if (_instance == this) _instance = null;
        }
    }
}
