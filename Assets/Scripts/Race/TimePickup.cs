using MotoSquid.AI;
using MotoSquid.Bike;
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
            // A human racer: a bike with no AI brain. The bikes carry no Player tag
            var bike = other.GetComponentInParent<BikeController>();
            if (bike != null && bike.GetComponentInChildren<BikeAILogic>(true) == null)
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
