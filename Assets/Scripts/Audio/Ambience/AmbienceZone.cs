using MotoSquid.Bike;
using UnityEngine;

namespace MotoSquid.Audio
{
    [RequireComponent(typeof(Collider))]
    public class AmbienceZone : MonoBehaviour
    {
        public string bedName = "Tunnel";

        int _inside;   // Bikes have several colliders, count enters so one leaving doesn't end the zone

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayer(other)) return;
            _inside++;
            if (_inside == 1) AmbienceController.Instance?.PushZone(bedName, this);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsLocalPlayer(other)) return;
            _inside = Mathf.Max(0, _inside - 1);
            if (_inside == 0) AmbienceController.Instance?.PopZone(this);
        }

        void OnDisable()
        {
            _inside = 0;
            AmbienceController.Instance?.PopZone(this);
        }

        static bool IsLocalPlayer(Collider other)
        {
            var local = BikeAudioController.LocalPlayer;
            if (local == null) return false;
            var audioCtrl = other.GetComponentInParent<BikeAudioController>();
            return audioCtrl == local;
        }
    }
}
