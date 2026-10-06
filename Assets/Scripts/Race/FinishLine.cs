using UnityEngine;

namespace MotoSquid.Race
{
    public class FinishLine : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;

        void Start()
        {
            if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
            if (raceManager == null)
                Debug.LogError("[FinishLine] No RaceManager assigned or found in scene.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (raceManager == null) return;
            // ReportFinish matches the collider to a registered racer by transform root and ignores
            // anything that isn't one, so every trigger can pass through without tag checks
            raceManager.ReportFinish(other.transform);
            //Debug.Log("Finish line hit by: " + other.name + " | Race state: " + raceManager.State);

        }
    }
}
