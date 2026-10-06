using UnityEngine;
using UnityEngine.Splines;

namespace MotoSquid.Roads
{
    // A road sampled at even stations (about 1 m apart in plan): centreline, direction and bank.
    // Height and bank are smooth functions of distance, never linear between spline knots.
    public sealed class RoadPath
    {
        const float DenseStep = 0.25f;
        const float StationStep = 1f;
        const float CurvatureSmoothing = 10f;
        const float RollRounding = 60f;
        const float ClosedTolerance = 0.01f;
        const float BaseLift = 0.15f;

        public readonly string Name;
        public readonly RoadSettings Settings;
        public readonly float Spacing;
        public readonly Vector3[] Centre;
        public readonly Vector3[] Forward;
        public readonly Vector3[] Right;
        public readonly float[] Curvature;
        public readonly float[] Bank;
        public readonly bool Closed;

        public int Count => Centre.Length;
        public float Length => Spacing * (Count - 1);
        public float HalfWidth => Settings.Width / 2f;

        RoadPath(string name, RoadSettings settings, float spacing, Vector3[] centre)
        {
            Name = name;
            Settings = settings;
            Spacing = spacing;
            Centre = centre;
            Forward = new Vector3[centre.Length];
            Right = new Vector3[centre.Length];
            Curvature = new float[centre.Length];
            Bank = new float[centre.Length];
            Closed = centre.Length > 2 && (centre[0] - centre[centre.Length - 1]).magnitude < ClosedTolerance;
        }

        public static RoadPath Sample(string name, Spline spline, Matrix4x4 toWorld, RoadSettings settings)
        {
            int dense = Mathf.Max(8, Mathf.CeilToInt(spline.GetLength() / DenseStep));
            var raw = new Vector3[dense + 1];
            for (int i = 0; i <= dense; i++)
                raw[i] = toWorld.MultiplyPoint3x4((Vector3)spline.EvaluatePosition(i / (float)dense));
            return FromPoints(name, raw,
                toWorld.MultiplyVector((Vector3)spline.EvaluateTangent(0f)),
                toWorld.MultiplyVector((Vector3)spline.EvaluateTangent(1f)), settings);
        }

        // From points densely spaced along the road (well under a metre apart), in world space.
        public static RoadPath FromPoints(string name, Vector3[] raw, Vector3 startTangent, Vector3 endTangent, RoadSettings settings)
        {
            int dense = raw.Length - 1;
            var along = new float[dense + 1];
            for (int i = 1; i <= dense; i++)
                along[i] = along[i - 1] + Flat(raw[i] - raw[i - 1]).magnitude;

            int count = Mathf.Max(2, Mathf.RoundToInt(along[dense] / StationStep) + 1);
            float spacing = along[dense] / (count - 1);
            var centre = new Vector3[count];
            for (int i = 0, j = 0; i < count; i++)
            {
                float s = i * spacing;
                while (j < dense - 1 && along[j + 1] < s) j++;
                float span = along[j + 1] - along[j];
                centre[i] = Vector3.Lerp(raw[j], raw[j + 1], span > 0f ? Mathf.Clamp01((s - along[j]) / span) : 0f);
            }
            centre[0] = raw[0];
            centre[count - 1] = raw[dense];

            var path = new RoadPath(name, settings, spacing, centre);
            if (settings.heightSmoothing > 0f)
                path.SmoothHeights(Window(settings.heightSmoothing, spacing));
            path.ComputeFrames(Flat(startTangent), Flat(endTangent));
            path.ComputeBank();
            return path;
        }

        // Banked sections tilt about their low edge, so the inside of a curve never sinks below the road's
        // height. The lift blends smoothly through zero bank (where an S-bend reverses) instead of creasing,
        // which leaves every road sitting BaseLift proud of its design height, the same for every width so
        // roads still meet level at junctions.
        public Vector3 Point(int i, float lateral)
        {
            float bank = Bank[i] * Mathf.Deg2Rad;
            float sin = Mathf.Sin(bank);
            float edgeRise = HalfWidth * sin;
            float lift = Mathf.Sqrt(edgeRise * edgeRise + BaseLift * BaseLift);
            return Centre[i] + Right[i] * (lateral * Mathf.Cos(bank)) + Vector3.up * (lateral * sin + lift);
        }

        public Vector3 PointAt(float station, float lateral)
        {
            float f = Mathf.Clamp(station / Spacing, 0f, Count - 1);
            int i = Mathf.Min((int)f, Count - 2);
            return Vector3.Lerp(Point(i, lateral), Point(i + 1, lateral), f - i);
        }

        public float BankAt(float station)
        {
            float f = Mathf.Clamp(station / Spacing, 0f, Count - 1);
            int i = Mathf.Min((int)f, Count - 2);
            return Mathf.Lerp(Bank[i], Bank[i + 1], f - i);
        }

        // Junctions are flat: within reach of one, the bank is capped by how far the road can roll out
        // at its legal rate before getting there, so it arrives level without rolling too fast.
        public void FlattenTowards(float[] metresToJunction)
        {
            float rate = Settings.maxRollRate * 0.9f;
            for (int i = 0; i < Count; i++)
            {
                float allowed = rate * Mathf.Max(0f, metresToJunction[i] - RollRounding / 2f);
                Bank[i] = Mathf.Clamp(Bank[i], -allowed, allowed);
            }
            ShapeRoll();
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void SmoothHeights(int window)
        {
            var y = new float[Count];
            for (int i = 0; i < Count; i++) y[i] = Centre[i].y;
            y = MovingAverage(y, window, Closed);
            for (int i = Closed ? 0 : 1; i < Count - 1; i++) Centre[i].y = y[i];
            if (Closed) Centre[Count - 1].y = Centre[0].y;
        }

        void ComputeFrames(Vector3 startTangent, Vector3 endTangent)
        {
            for (int i = 0; i < Count; i++)
            {
                Vector3 d = i == 0 ? startTangent
                          : i == Count - 1 ? endTangent
                          : Flat(Centre[i + 1] - Centre[i - 1]);
                Forward[i] = d.sqrMagnitude > 1e-12f ? d.normalized : (i > 0 ? Forward[i - 1] : Vector3.forward);
                Right[i] = Vector3.Cross(Vector3.up, Forward[i]);
            }

            // Positive curvature turns left.
            for (int i = 0; i < Count; i++)
            {
                int before = i > 0 ? i - 1 : Closed ? Count - 2 : 0;
                int after = i < Count - 1 ? i + 1 : Closed ? 1 : Count - 1;
                float span = (after - before + (after < before ? Count - 1 : 0)) * Spacing;
                Curvature[i] = span > 0f ? -Vector3.SignedAngle(Forward[before], Forward[after], Vector3.up) * Mathf.Deg2Rad / span : 0f;
            }
            var smoothed = MovingAverage(Curvature, Window(CurvatureSmoothing, Spacing), Closed);
            System.Array.Copy(smoothed, Curvature, Count);
        }

        // A left turn raises the right edge: the road leans into the corner.
        void ComputeBank()
        {
            var s = Settings;
            for (int i = 0; i < Count; i++)
                Bank[i] = Mathf.Sign(Curvature[i]) * s.maxBank * Mathf.Clamp01(s.fullBankRadius * Mathf.Abs(Curvature[i]));
            var smoothed = MovingAverage(Bank, Window(s.bankSmoothing, Spacing), Closed);
            System.Array.Copy(smoothed, Bank, Count);
            ShapeRoll();
        }

        // Caps the roll rate, then rounds the corners the cap leaves: a bank that starts rolling abruptly
        // gives the outer lanes a crest sharp enough to lift a bike at speed.
        void ShapeRoll()
        {
            LimitRate(Bank, Settings.maxRollRate * Spacing, Closed);
            var rounded = MovingAverage(Bank, Window(RollRounding, Spacing), Closed);
            System.Array.Copy(rounded, Bank, Count);
        }

        static int Window(float metres, float spacing) => Mathf.Max(1, Mathf.RoundToInt(metres / spacing)) | 1;

        // On a closed road the last station repeats the first, and averaging wraps across the seam.
        static float[] MovingAverage(float[] values, int window, bool closed)
        {
            int n = closed ? values.Length - 1 : values.Length;
            int half = window / 2;
            var result = new float[values.Length];
            if (half == 0 || n < 2) { System.Array.Copy(values, result, values.Length); return result; }
            float At(int i) => closed ? values[((i % n) + n) % n] : values[Mathf.Clamp(i, 0, n - 1)];
            double sum = 0;
            for (int k = -half; k <= half; k++) sum += At(k);
            for (int i = 0; i < n; i++)
            {
                result[i] = (float)(sum / window);
                sum += At(i + half + 1) - At(i - half);
            }
            if (closed) result[n] = result[0];
            return result;
        }

        // Shrinks the bank wherever it would roll faster than allowed; it never grows it, so the result is
        // the largest bank under the original that obeys the rate. On a ring the passes repeat until settled.
        static void LimitRate(float[] values, float maxStep, bool closed)
        {
            int n = closed ? values.Length - 1 : values.Length;
            var size = new float[n];
            for (int i = 0; i < n; i++) size[i] = Mathf.Abs(values[i]);
            for (int pass = 0; pass < (closed ? 16 : 1); pass++)
            {
                bool changed = false;
                for (int i = 1; i < (closed ? n + 1 : n); i++)
                    changed |= Cap(size, i % n, i - 1, maxStep);
                for (int i = n - 2; i >= (closed ? -1 : 0); i--)
                    changed |= Cap(size, (i + n) % n, (i + 1) % n, maxStep);
                if (!changed) break;
            }
            for (int i = 0; i < n; i++) values[i] = Mathf.Sign(values[i]) * size[i];
            if (closed) values[n] = values[0];
        }

        static bool Cap(float[] size, int at, int neighbour, float maxStep)
        {
            if (size[at] <= size[neighbour] + maxStep) return false;
            size[at] = size[neighbour] + maxStep;
            return true;
        }
    }
}
