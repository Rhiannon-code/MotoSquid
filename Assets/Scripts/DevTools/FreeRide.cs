using MotoSquid.Controls;
using MotoSquid.Race;
using UnityEngine;

namespace MotoSquid.DevTools
{
    // Lets a player bike be ridden in a scene with no race, for trying out roads. A race hands the keyboard to the
    // single player and unlocks steering at the start; without one the bike answers only a gamepad's throttle. Put
    // it on the bike. It does nothing when the scene has a RaceManager.
    public class FreeRide : MonoBehaviour
    {
        void Start()
        {
            if (FindAnyObjectByType<RaceManager>() != null) return;
            foreach (var input in GetComponentsInChildren<BikeInput>())
            {
                input.SetDeviceFilter(lockKb: false, deviceIndex: -1, allowKb: true);
                input.SetFullControl();
            }
        }
    }
}
