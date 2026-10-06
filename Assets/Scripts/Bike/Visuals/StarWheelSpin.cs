using UnityEngine;

namespace MotoSquid.Bike
{
    public class StarWheelSpin : MonoBehaviour
    {
        public float degreesPerSecond = 18f;
        public Transform pivot;

        void Update()
        {
            if (pivot == null) return; 
            transform.RotateAround(pivot.position, transform.up, degreesPerSecond * Time.deltaTime);
        }
    }
}