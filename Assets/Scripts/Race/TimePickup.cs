using UnityEngine;

namespace MotoSquid.Race
{
    public class TimePickup : MonoBehaviour
    {
        [SerializeField] private float timeToAdd = 5f;
        private TimerSystem timer;

        void Start()
        {
            timer = Object.FindFirstObjectByType<TimerSystem>();
            
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (timer != null)
                {
                    // Add time bonus to the timer system
                    timer.AddTimeBonus(timeToAdd);
                    gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning("TimePickup: Tried to add time, but the timer was null");
                }
            }
        }
    }
}
