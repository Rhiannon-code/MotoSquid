using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public interface IQuadric<TQuadric, TData>
      where TQuadric : unmanaged
      where TData : unmanaged
    {
        QuadricInitContext InitFromTriangle(double3 v0, double3 v1, double3 v2, TData attr0, TData attr1, TData attr2);

        void Add(ref TQuadric other);

        TData GetOptimalValue(double3 finalPosition, TData fallback);

        TData Normalize(TData data);

        TData NormalizeSaveW(TData data);
    }


    public struct QuadricInitContext
    {
        public bool isValid;
        public double3 e2_x_n_inv;
        public double3 n_x_e1_inv;
        public double weight;

        public void Initialize(double3 v0, double3 v1, double3 v2)
        {
            double3 e1 = v1 - v0;
            double3 e2 = v2 - v0;

            double3 n = math.cross(e1, e2);

            double sqrMagN = math.dot(n, n);

            if (sqrMagN < 1E-12)
            {
                isValid = false;
                weight = 0;
                e2_x_n_inv = default;
                n_x_e1_inv = default;
                return;
            }

            isValid = true;
            weight = math.sqrt(sqrMagN);

            double invSqrMagN = 1.0 / sqrMagN;

            e2_x_n_inv = math.cross(e2, n) * invSqrMagN;
            n_x_e1_inv = math.cross(n, e1) * invSqrMagN;
        }

        public double3 GetGradient(double attr0, double attr1, double attr2)
        {
            return e2_x_n_inv * (attr1 - attr0) + n_x_e1_inv * (attr2 - attr0);
        }
    }

    public struct Quadric2D : IQuadric<Quadric2D, Vector2>
    {
        public double3 sumGradientX;
        public double3 sumGradientY;
        public double sumOffsetX;
        public double sumOffsetY;
        public double sumWeight;

        public QuadricInitContext InitFromTriangle(double3 v0, double3 v1, double3 v2, Vector2 attr0, Vector2 attr1, Vector2 attr2)
        {
            QuadricInitContext context = default;

            context.Initialize(v0, v1, v2);

            if (!context.isValid)
                return context;

            double3 gx = context.GetGradient(attr0.x, attr1.x, attr2.x);
            double3 gy = context.GetGradient(attr0.y, attr1.y, attr2.y);

            double offsetX = attr0.x - math.dot(gx, v0);
            double offsetY = attr0.y - math.dot(gy, v0);

            sumGradientX = gx * context.weight;
            sumGradientY = gy * context.weight;

            sumOffsetX = offsetX * context.weight;
            sumOffsetY = offsetY * context.weight;

            sumWeight = context.weight;

            return context;
        }

        public void Add(ref Quadric2D other)
        {
            sumGradientX += other.sumGradientX;
            sumGradientY += other.sumGradientY;
            sumOffsetX += other.sumOffsetX;
            sumOffsetY += other.sumOffsetY;
            sumWeight += other.sumWeight;
        }

        public Vector2 GetOptimalValue(double3 finalPosition, Vector2 fallback)
        {
            if (sumWeight < 1E-12)
                return fallback;

            double invWeight = 1.0 / sumWeight;

            double x = (math.dot(sumGradientX, finalPosition) + sumOffsetX) * invWeight;
            double y = (math.dot(sumGradientY, finalPosition) + sumOffsetY) * invWeight;

            return new Vector2((float)x, (float)y);
        }

        public Vector2 Normalize(Vector2 data)
        {
            return data.normalized;
        }

        public Vector2 NormalizeSaveW(Vector2 data)
        {
            return data.normalized;
        }
    }

    public struct Quadric3D : IQuadric<Quadric3D, Vector3>
    {
        public Quadric2D xy;

        public double3 sumGradientZ;
        public double sumOffsetZ;

        public QuadricInitContext InitFromTriangle(double3 v0, double3 v1, double3 v2, Vector3 attr0, Vector3 attr1, Vector3 attr2)
        {
            xy = default;

            QuadricInitContext context = xy.InitFromTriangle(v0, v1, v2, attr0, attr1, attr2);

            if (!context.isValid)
                return context;

            double3 gz = context.GetGradient(attr0.z, attr1.z, attr2.z);
            double offsetZ = attr0.z - math.dot(gz, v0);

            sumGradientZ = gz * context.weight;
            sumOffsetZ = offsetZ * context.weight;

            return context;
        }

        public void Add(ref Quadric3D other)
        {
            xy.Add(ref other.xy);

            sumGradientZ += other.sumGradientZ;
            sumOffsetZ += other.sumOffsetZ;
        }

        public Vector3 GetOptimalValue(double3 finalPosition, Vector3 fallback)
        {
            if (xy.sumWeight < 1E-12)
                return fallback;

            double invWeight = 1.0 / xy.sumWeight;

            Vector2 optimal2D = xy.GetOptimalValue(finalPosition, fallback);

            double z = (math.dot(sumGradientZ, finalPosition) + sumOffsetZ) * invWeight;

            return new Vector3(optimal2D.x, optimal2D.y, (float)z);
        }

        public Vector3 Normalize(Vector3 data)
        {
            return data.normalized;
        }

        public Vector3 NormalizeSaveW(Vector3 data)
        {
            return data.normalized;
        }
    }

    public struct Quadric4D : IQuadric<Quadric4D, Vector4>
    {
        public Quadric3D xyz;

        public double3 sumGradientW;
        public double sumOffsetW;

        public QuadricInitContext InitFromTriangle(double3 v0, double3 v1, double3 v2, Vector4 attr0, Vector4 attr1, Vector4 attr2)
        {
            xyz = default;

            QuadricInitContext context = xyz.InitFromTriangle(v0, v1, v2, attr0, attr1, attr2);

            if (!context.isValid)
                return context;

            double3 gw = context.GetGradient(attr0.w, attr1.w, attr2.w);
            double offsetW = attr0.w - math.dot(gw, v0);

            sumGradientW = gw * context.weight;
            sumOffsetW = offsetW * context.weight;

            return context;
        }

        public void Add(ref Quadric4D other)
        {
            xyz.Add(ref other.xyz);

            sumGradientW += other.sumGradientW;
            sumOffsetW += other.sumOffsetW;
        }

        public Vector4 GetOptimalValue(double3 finalPosition, Vector4 fallback)
        {
            if (xyz.xy.sumWeight < 1E-12)
                return fallback;

            double invWeight = 1.0 / xyz.xy.sumWeight;

            Vector3 optimal3D = xyz.GetOptimalValue(finalPosition, fallback);

            double w = (math.dot(sumGradientW, finalPosition) + sumOffsetW) * invWeight;

            return new Vector4(optimal3D.x, optimal3D.y, optimal3D.z, (float)w);
        }

        public Vector4 Normalize(Vector4 data)
        {
            return data.normalized;
        }

        public Vector4 NormalizeSaveW(Vector4 data)
        {
            Vector3 xyz = data;
            float w = data.w;

            xyz = xyz.normalized;
            w = (w >= 0f) ? 1f : -1f;

            return new Vector4(xyz.x, xyz.y, xyz.z, w);
        }
    }

    public struct AttributeQuadricInterpolator<TQuadric, TData>
      where TQuadric : unmanaged, IQuadric<TQuadric, TData>
      where TData : unmanaged
    {
        private NativeArray<TData> _originalAttributes;
        private NativeArray<TQuadric> _quadrics;
        private Aliases _aliases;


        public void InitializeInMainThread(NativeArray<TData> originalAttributes)
        {
            _originalAttributes = originalAttributes;
            _quadrics = new NativeArray<TQuadric>(originalAttributes.Length, Allocator.Persistent);
            _aliases = new Aliases(originalAttributes.Length);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            NativeArray<Vertex> vertices = internalData.vertices;
            NativeArray<Triangle> triangles = internalData.triangles;

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle t = triangles[i];

                if (t.IsDegenerated())
                    continue;

                double3 p0 = vertices[t.v0].position;
                double3 p1 = vertices[t.v1].position;
                double3 p2 = vertices[t.v2].position;

                TData attr0 = _originalAttributes[t.origV0];
                TData attr1 = _originalAttributes[t.origV1];
                TData attr2 = _originalAttributes[t.origV2];

                TQuadric q = default;
                q.InitFromTriangle(p0, p1, p2, attr0, attr1, attr2);

                TQuadric q0 = _quadrics[t.origV0];
                TQuadric q1 = _quadrics[t.origV1];
                TQuadric q2 = _quadrics[t.origV2];

                q0.Add(ref q);
                q1.Add(ref q);
                q2.Add(ref q);

                _quadrics[t.origV0] = q0;
                _quadrics[t.origV1] = q1;
                _quadrics[t.origV2] = q2;
            }
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            Aliases vertexAliases = internalData.vertexAliases;

            int survOrig = (vertexAliases.GetAlias(edge.vertex1) == survivedIndex) ? edge.origVertex1 : edge.origVertex2;
            int delOrig = (vertexAliases.GetAlias(edge.vertex1) == survivedIndex) ? edge.origVertex2 : edge.origVertex1;

            int survRoot = _aliases.GetAlias(survOrig);
            int delRoot = _aliases.GetAlias(delOrig);

            if (survRoot != delRoot)
            {
                TQuadric survivedQuadric = _quadrics[survRoot];
                TQuadric deletedQuadric = _quadrics[delRoot];

                survivedQuadric.Add(ref deletedQuadric);

                _quadrics[survRoot] = survivedQuadric;
                _aliases.SetAlias(delRoot, survRoot);
            }
        }

        public void OutputInterpolatedData(
            NativeSlice<TData> output,
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            NativeArray<Vector3> newPositions,
            bool normalize, bool saveW)
        {
            for (int i = 0; i < oldToNewVMap.Length; i++)
            {
                if (oldToNewVMap[i] == -1)
                    continue;

                int newIdx = oldToNewVMap[i];
                int dataRoot = _aliases.GetAlias(i);

                Vector3 finalPos = newPositions[newIdx];
                TQuadric quadric = _quadrics[dataRoot];

                TData optimalAttribute = quadric.GetOptimalValue(new double3(finalPos.x, finalPos.y, finalPos.z), _originalAttributes[i]);

                if (normalize)
                {
                    if (saveW)
                        optimalAttribute = quadric.NormalizeSaveW(optimalAttribute);

                    else
                        optimalAttribute = quadric.Normalize(optimalAttribute);
                }

                output[newIdx] = optimalAttribute;
            }
        }

        public void Dispose()
        {
            if (_originalAttributes.IsCreated)
                _originalAttributes.Dispose();

            if (_quadrics.IsCreated)
                _quadrics.Dispose();

            _aliases.Dispose();
        }
    }
}