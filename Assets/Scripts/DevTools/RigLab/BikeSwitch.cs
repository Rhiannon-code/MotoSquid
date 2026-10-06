using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Race;
using MotoSquid.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MotoSquid.DevTools
{
    // The bike level twin of RiderSwitch, every candidate bike lives under this object and one is
    // active, so the menu's pick is a name lookup rather than a spawn
    [DefaultExecutionOrder(-60)]
    public class BikeSwitch : MonoBehaviour
    {
        public GameObject[] bikes = new GameObject[0];

        [Header("Scene references to keep pointed at the active bike")]
        public RaceManager raceManager;
        public HUDManager hud;
        public SplitScreenSetup splitScreen;

        [Tooltip("The bike coming in takes the outgoing one's place, so a swap does not teleport you")]
        public bool matchTransform = true;

        public bool showReadout;

        int index;

        void Start()
        {
            Compact();
            for (int i = 0; i < bikes.Length; i++)
                if (bikes[i] != null && bikes[i].activeSelf) { index = i; break; }
            Apply();
        }

        public bool SetBike(string bikeName)
        {
            if (string.IsNullOrEmpty(bikeName)) return false;
            Compact();

            int found = IndexOf(bikeName, true);
            if (found < 0) found = IndexOf(bikeName, false);
            if (found < 0) return false;

            index = found;
            Apply();
            return true;
        }

        int IndexOf(string bikeName, bool exact)
        {
            for (int i = 0; i < bikes.Length; i++)
            {
                if (bikes[i] == null) continue;
                bool hit = exact
                    ? bikes[i].name.Equals(bikeName, StringComparison.OrdinalIgnoreCase)
                    : bikes[i].name.StartsWith(bikeName, StringComparison.OrdinalIgnoreCase);
                if (hit) return i;
            }
            return -1;
        }

        public GameObject ActiveBike
        {
            get
            {
                foreach (var b in bikes) if (b != null && b.activeSelf) return b;
                return index < bikes.Length ? bikes[index] : null;
            }
        }

        public string Current => index < bikes.Length && bikes[index] != null ? bikes[index].name : "-";

        public IEnumerable<string> BikeNames
        {
            get { foreach (var b in bikes) if (b != null) yield return b.name; }
        }

        void Compact()
        {
            var live = new List<GameObject>();
            foreach (var b in bikes) if (b != null) live.Add(b);
            if (live.Count != bikes.Length) bikes = live.ToArray();
            if (index >= bikes.Length) index = 0;
        }

        void Update()
        {
            if (bikes.Length < 2) return;
            if (Pressed(true)) Step(1);
            else if (Pressed(false)) Step(-1);
        }

        void Step(int dir)
        {
            index = (index + dir + bikes.Length) % bikes.Length;
            Apply();
        }

        void Apply()
        {
            var outgoing = ActiveBike;
            var incoming = index < bikes.Length ? bikes[index] : null;

            if (matchTransform && outgoing != null && incoming != null && outgoing != incoming)
                incoming.transform.SetPositionAndRotation(outgoing.transform.position,
                                                          outgoing.transform.rotation);

            for (int i = 0; i < bikes.Length; i++)
                if (bikes[i] != null) bikes[i].SetActive(i == index);

            Rebind();
        }

        public void Rebind()
        {
            var bike = ActiveBike;
            if (bike == null) return;

            var controller = bike.GetComponent<BikeController>();
            if (controller == null) return;

            // Camera channel and device filter first: both are what make the incoming bike the
            // player's rather than a second bike answering every device
            if (splitScreen != null) splitScreen.BindPlayer1(controller);

            if (raceManager != null)
            {
                raceManager.playerTransform = bike.transform;
                raceManager.playerBike = controller;
            }

            if (hud != null)
                hud.BindPlayer1(controller, bike.GetComponentInChildren<BoostSystem>(true));
        }

        static bool Pressed(bool next)
        {
            // Editor only: in a build a keyboard P2 would swap riders mid race
            if (!UnityEngine.Application.isEditor) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
                return next ? kb.rightBracketKey.wasPressedThisFrame : kb.leftBracketKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(next ? KeyCode.RightBracket : KeyCode.LeftBracket);
#else
            return false;
#endif
        }

        void OnGUI()
        {
            if (!showReadout || bikes.Length < 2) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            GUI.Label(new Rect(14, 14, 600, 20),
                      "Bike  [ / ]   " + Current + "   (" + (index + 1) + " of " + bikes.Length + ")", style);
        }
    }
}
