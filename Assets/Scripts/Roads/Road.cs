using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    [RequireComponent(typeof(SplineContainer))]
    public class Road : MonoBehaviour
    {
        public RoadSettings settings = new RoadSettings();
        [Tooltip("Numbers that shape this road until its knots are dragged by hand. Editing them reshapes the spline.")]
        public RoadRecipe recipe = new RoadRecipe();

        // Made by a layout, so Update Roads may replace it while nobody has edited it.
        [HideInInspector] public bool generated;
        // Changed by hand: Update Roads never touches it.
        [HideInInspector] public bool edited;
        // Knots dragged by hand: the spline is the road now, and the recipe no longer drives it.
        [HideInInspector] public bool handShaped;

        public static bool IsReshaping { get; private set; }

        public Spline Spline => GetComponent<SplineContainer>().Spline;

        // Once its knots are dragged, a road's knots shape it in plan but its heights still come from its height
        // points, which hand edits keep within the ride rules (HeightEdit).
        public VerticalProfile HandHeights => handShaped && recipe.heights.Count > 1 ? recipe.Profile() : null;

        public void SetKnots(BezierKnot[] knots)
        {
            IsReshaping = true;
            try
            {
                var spline = Spline;
                spline.Clear();
                foreach (var knot in knots) spline.Add(knot, TangentMode.Continuous);
            }
            finally { IsReshaping = false; }
        }

        public void ShapeFromRecipe()
        {
            if (!recipe.IsEmpty) SetKnots(recipe.Knots());
        }
    }
}
