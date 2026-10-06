using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Race;
using MotoSquid.UI;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabBikeSwitch
    {
        const string HostName = "Bike Switch (P1)";

        // Player 2 keeps its own bike, so it must never end up in a switch that deactivates the others
        const string PlayerTwoPrefix = "Player2";

        [MenuItem("Tools/Rig Lab/60. Collect Player Bikes Into Switch", false, 600)]
        static void Collect()
        {
            var candidates = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => !b.name.StartsWith(PlayerTwoPrefix))
                .OrderBy(b => b.name)
                .Select(b => b.gameObject)
                .ToArray();

            if (candidates.Length < 2)
            {
                Debug.LogError("Rig Lab: found " + candidates.Length + " player bike(s) in the open scene, " +
                               "a switch needs at least two.\n   Bikes named '" + PlayerTwoPrefix +
                               "...' are left out on purpose.");
                return;
            }

            var host = Object.FindObjectsByType<BikeSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .FirstOrDefault();
            if (host == null)
            {
                var go = new GameObject(HostName);
                Undo.RegisterCreatedObjectUndo(go, "Collect player bikes");
                host = Undo.AddComponent<BikeSwitch>(go);
            }

            Undo.RecordObject(host, "Collect player bikes");
            host.bikes = candidates;
            host.raceManager = Object.FindFirstObjectByType<RaceManager>();
            host.hud = Object.FindFirstObjectByType<HUDManager>();
            host.splitScreen = Object.FindFirstObjectByType<SplitScreenSetup>();

            int active = System.Array.FindIndex(candidates, b => b.activeSelf);
            if (active < 0) active = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                Undo.RecordObject(candidates[i], "Collect player bikes");
                candidates[i].SetActive(i == active);
            }
            host.Rebind();
            EditorUtility.SetDirty(host);

            RigLabScope.MarkDirty();
            Selection.activeGameObject = host.gameObject;

            Debug.Log("Rig Lab: " + HostName + " holds " + candidates.Length + " bike(s).\n   " +
                      string.Join("\n   ", candidates.Select((b, i) => (i == active ? "active  " : "        ") + b.name)) +
                      "\n\nNothing was created or reparented, the bikes already in the scene were adopted." +
                      "\nCycle with [ and ] in Play, or Alt+Shift+B in edit mode." +
                      Missing(host));
        }

        [MenuItem("Tools/Rig Lab/61. Swap Player 1 Bike", false, 601)]
        static void SwapMenu() => Swap();

        [Shortcut("Rig Lab/Swap Player 1 Bike", KeyCode.B, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void SwapShortcut() => Swap();

        static void Swap()
        {
            var host = Object.FindObjectsByType<BikeSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .FirstOrDefault();
            if (host == null)
            {
                Debug.LogError("Rig Lab: no BikeSwitch in the open scene. Run 60 first.");
                return;
            }

            var live = host.bikes.Where(b => b != null).ToArray();
            if (live.Length < 2) { Debug.LogWarning("Rig Lab: the switch holds fewer than two bikes."); return; }

            int current = System.Array.FindIndex(live, b => b.activeSelf);
            int next = (current + 1) % live.Length;

            if (host.matchTransform && current >= 0)
            {
                Undo.RecordObject(live[next].transform, "Swap player 1 bike");
                live[next].transform.SetPositionAndRotation(live[current].transform.position,
                                                            live[current].transform.rotation);
            }

            for (int i = 0; i < live.Length; i++)
            {
                Undo.RecordObject(live[i], "Swap player 1 bike");
                live[i].SetActive(i == next);
            }

            Undo.RecordObject(host, "Swap player 1 bike");
            host.Rebind();
            EditorUtility.SetDirty(host);

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: Player 1 bike -> " + live[next].name);
        }

        static string Missing(BikeSwitch host)
        {
            var gaps = new System.Collections.Generic.List<string>();
            if (host.splitScreen == null) gaps.Add("no SplitScreenSetup, camera channel and device filter stay as authored");
            if (host.raceManager == null) gaps.Add("no RaceManager, the grid and standings will not follow the swap");
            if (host.hud == null) gaps.Add("no HUDManager, the HUD will keep reading the old bike");
            return gaps.Count == 0 ? "" : "\n\nNeeds attention:\n   " + string.Join("\n   ", gaps);
        }
    }
}
