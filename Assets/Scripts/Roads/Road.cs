using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    [RequireComponent(typeof(SplineContainer))]
    public class Road : MonoBehaviour
    {
        public RoadSettings settings = new RoadSettings();

        public Spline Spline => GetComponent<SplineContainer>().Spline;
    }
}
