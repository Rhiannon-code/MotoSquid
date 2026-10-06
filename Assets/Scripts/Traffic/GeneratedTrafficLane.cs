using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Traffic
{
    public class GeneratedTrafficLane : MonoBehaviour
    {
        public int roadIndex;
        public int laneIndex;
        public bool isOncoming;

        // Flow continuation across road sections. next is where a vehicle leaving the end of this
        // lane carries on, prev is the lane that feeds into this one. Both may be null at a dead end
        [HideInInspector] public SplineContainer next;
        [HideInInspector] public SplineContainer prev;

        [HideInInspector] public bool isInnerLane;
        [HideInInspector] public bool trafficLocked;

        public void Setup(int road, int lane, bool oncoming = false)
        {
            roadIndex = road;
            laneIndex = lane;
            isOncoming = oncoming;
        }
    }
}
