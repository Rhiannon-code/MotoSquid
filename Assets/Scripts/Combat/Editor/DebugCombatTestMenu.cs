using MotoSquid.Bike;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Combat
{

    [InitializeOnLoad]
    public static class DebugCombatTestMenu
    {
        const string MenuPath   = "MotoSquid/Debug/Hold AI For Combat Test";
        const string SessionKey = "MotoSquid.DebugHoldAIForCombatTest";

        static DebugCombatTestMenu()
        {
            BikeAIController.DebugHoldForCombatTest = SessionState.GetBool(SessionKey, false);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                BikeAIController.DebugHoldForCombatTest = SessionState.GetBool(SessionKey, false);
                if (BikeAIController.DebugHoldForCombatTest)
                    Debug.Log("[CombatTest] AI hold is ON, AI bikes stand still but still attack and take hits.");
            };
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            bool on = !BikeAIController.DebugHoldForCombatTest;
            BikeAIController.DebugHoldForCombatTest = on;
            SessionState.SetBool(SessionKey, on);
            Menu.SetChecked(MenuPath, on);
            Debug.Log($"[CombatTest] AI hold {(on ? "ON, AI bikes stand still but still attack and take hits" : "OFF, AI racing normally")}.");
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, BikeAIController.DebugHoldForCombatTest);
            return true;
        }
    }
}
