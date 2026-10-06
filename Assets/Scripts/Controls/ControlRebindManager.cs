using MotoSquid.Combat;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Controls
{
    public class ControlRebindManager : MonoBehaviour
    {
        public const string PrefKey = "MotoSquid_BindingOverrides";

        [SerializeField] private InputActionAsset actionAsset;

        [SerializeField] private string[] excludedControlPaths = { "<Mouse>/position", "<Mouse>/delta" };

        InputActionRebindingExtensions.RebindingOperation _rebindOp;

        public InputActionAsset Asset => actionAsset;

        void Awake()
        {
            if (actionAsset != null) ApplySavedOverrides(actionAsset);
        }

        public static void ApplySavedOverrides(InputActionAsset asset)
        {
            if (asset == null) return;
            string json = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
                asset.LoadBindingOverridesFromJson(json);
        }

        public void StartRebind(string actionName, int bindingIndex, Action<bool> onComplete = null)
        {
            if (actionAsset == null) { onComplete?.Invoke(false); return; }

            var action = actionAsset.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                Debug.LogWarning($"[ControlRebind] '{actionName}' binding {bindingIndex} not found.", this);
                onComplete?.Invoke(false);
                return;
            }

            _rebindOp?.Dispose();

            bool wasEnabled = action.enabled;
            action.Disable();   // Required before rebinding

            var op = action.PerformInteractiveRebinding(bindingIndex)
                           .WithControlsExcluding("<Pointer>/position")
                           .OnMatchWaitForAnother(0.1f);

            if (excludedControlPaths != null)
                foreach (var p in excludedControlPaths)
                    if (!string.IsNullOrEmpty(p)) op.WithControlsExcluding(p);

            op.OnComplete(o =>
            {
                o.Dispose();
                _rebindOp = null;
                if (wasEnabled) action.Enable();
                SaveOverrides();
                onComplete?.Invoke(true);
            });
            op.OnCancel(o =>
            {
                o.Dispose();
                _rebindOp = null;
                if (wasEnabled) action.Enable();
                onComplete?.Invoke(false);
            });

            _rebindOp = op;
            op.Start();
        }

        public void CancelRebind() => _rebindOp?.Cancel();

        // Human readable label for a binding (for the settings UI), e.g. "W" or "Left Stick/Left"
        public string GetBindingDisplayString(string actionName, int bindingIndex)
        {
            var action = actionAsset?.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "";
            return action.GetBindingDisplayString(bindingIndex);
        }

        public void ResetBinding(string actionName, int bindingIndex)
        {
            var action = actionAsset?.FindAction(actionName);
            if (action == null) return;
            action.RemoveBindingOverride(bindingIndex);
            SaveOverrides();
        }

        public void ResetAll()
        {
            if (actionAsset == null) return;
            foreach (var map in actionAsset.actionMaps)
                map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        void SaveOverrides()
        {
            if (actionAsset == null) return;
            PlayerPrefs.SetString(PrefKey, actionAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        void OnDestroy() => _rebindOp?.Dispose();
    }
}
