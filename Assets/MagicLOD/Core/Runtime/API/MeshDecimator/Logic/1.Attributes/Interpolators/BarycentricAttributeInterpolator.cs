using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public interface IBarycentricAttributeApply<T> 
        where T : unmanaged
    {
        void Apply(NativeArray<T> attributes, double3 bary, int v0, int v1, int v2, bool needUpdateV0, bool needUpdateV1, bool needUpdateV2);

        T Normalize(T data);

        T NormalizeSaveW(T data);
    }


    public struct Vector2BarycentricApply : IBarycentricAttributeApply<Vector2>
    {
        public void Apply(NativeArray<Vector2> attributes, double3 bary, int v0, int v1, int v2, bool needUpdateV0, bool needUpdateV1, bool needUpdateV2)
        {
            Vector2 a0 = attributes[v0];
            Vector2 a1 = attributes[v1];
            Vector2 a2 = attributes[v2];

            Vector2 newData = a0 * (float)bary.x + a1 * (float)bary.y + a2 * (float)bary.z;

            if (needUpdateV0)
                attributes[v0] = newData;

            if (needUpdateV1)
                attributes[v1] = newData;

            if (needUpdateV2)
                attributes[v2] = newData;
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

    public struct Vector3BarycentricApply : IBarycentricAttributeApply<Vector3>
    {
        public void Apply(NativeArray<Vector3> attributes, double3 bary, int v0, int v1, int v2, bool needUpdateV0, bool needUpdateV1, bool needUpdateV2)
        {
            Vector3 a0 = attributes[v0];
            Vector3 a1 = attributes[v1];
            Vector3 a2 = attributes[v2];

            Vector3 newData = a0 * (float)bary.x + a1 * (float)bary.y + a2 * (float)bary.z;

            if (needUpdateV0)
                attributes[v0] = newData;

            if (needUpdateV1)
                attributes[v1] = newData;

            if (needUpdateV2)
                attributes[v2] = newData;
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

    public struct Vector4BarycentricApply : IBarycentricAttributeApply<Vector4>
    {
        public void Apply(NativeArray<Vector4> attributes, double3 bary, int v0, int v1, int v2, bool needUpdateV0, bool needUpdateV1, bool needUpdateV2)
        {
            Vector4 a0 = attributes[v0];
            Vector4 a1 = attributes[v1];
            Vector4 a2 = attributes[v2];

            Vector4 newData = a0 * (float)bary.x + a1 * (float)bary.y + a2 * (float)bary.z;

            if (needUpdateV0)
                attributes[v0] = newData;

            if (needUpdateV1)
                attributes[v1] = newData;

            if (needUpdateV2)
                attributes[v2] = newData;
        }

        public Vector4 Normalize(Vector4 data)
        {
            return data.normalized;
        }

        public Vector4 NormalizeSaveW(Vector4 data)
        {
            Vector3 xyz = ((Vector3)data).normalized;
            float w = (data.w >= 0f) ? 1f : -1f;

            return new Vector4(xyz.x, xyz.y, xyz.z, w);
        }
    }

    public struct AttributeBarycentricInterpolator<TBarycentricApply, TData>
       where TBarycentricApply : unmanaged, IBarycentricAttributeApply<TData>
       where TData : unmanaged
    {
        private NativeArray<TData> _attributes;
        private TBarycentricApply _attributesApply;

        private NativeArray<int> _timestamps;
        private int _currentIteration;


        public void InitializeInMainThread(NativeArray<TData> attributes)
        {
            _attributes = attributes;
            _attributesApply = new TBarycentricApply();
            _timestamps = new NativeArray<int>(attributes.Length, Allocator.Persistent);
            _currentIteration = 0;
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            _currentIteration++;

            UpdateUVsForTrianglesBarycentric(ref internalData, survivedIndex, survivedIndex, deletedIndex, edge.optimalPosition);
            UpdateUVsForTrianglesBarycentric(ref internalData, deletedIndex, survivedIndex, deletedIndex, edge.optimalPosition);
        }

        public void OutputInterpolatedData(
            NativeSlice<TData> output,
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            bool normalize, bool saveW)
        {
            NativeArray<Vertex> vertices = internalData.vertices;
            NativeArray<int> verticesRemap = internalData.verticesRemap;

            Aliases vertexAliases = internalData.vertexAliases;

            for (int i = 0; i < oldToNewVMap.Length; i++)
            {
                if (oldToNewVMap[i] < 0)
                    continue;

                TData newData = _attributes[i];
                int newIdx = oldToNewVMap[i];

                if (normalize)
                {
                    if (saveW)
                        newData = _attributesApply.NormalizeSaveW(newData);
                    else
                        newData = _attributesApply.Normalize(newData);
                }

                output[newIdx] = newData;
            }
        }

        public void Dispose()
        {
            if (_attributes.IsCreated)
                _attributes.Dispose();

            if (_timestamps.IsCreated)
                _timestamps.Dispose();
        }


        private void UpdateUVsForTrianglesBarycentric(ref MeshDecimator.InternalData internalData, int sourceVertexIndex, int survivedIndex, int deletedIndex, double3 optimalPosition)
        {
            NativeArray<Vertex> vertices = internalData.vertices;
            NativeArray<Triangle> triangles = internalData.triangles;

            VertexToTriangles vertexToTriangles = internalData.vertexToTriangles;
            Aliases vertexAliases = internalData.vertexAliases;

            NativeList<int> affectedTris = vertexToTriangles.GetTriangles(sourceVertexIndex);

            for (int i = 0; i < affectedTris.Length; i++)
            {
                Triangle t = triangles[affectedTris[i]];

                int v0 = vertexAliases.GetAlias(t.v0);
                int v1 = vertexAliases.GetAlias(t.v1);
                int v2 = vertexAliases.GetAlias(t.v2);

                if (v0 == v1 || v1 == v2 || v2 == v0)
                    continue;

                int orig0 = t.origV0;
                int orig1 = t.origV1;
                int orig2 = t.origV2;

                bool needsV0Update = (v0 == survivedIndex || v0 == deletedIndex) && (_timestamps[orig0] != _currentIteration);
                bool needsV1Update = (v1 == survivedIndex || v1 == deletedIndex) && (_timestamps[orig1] != _currentIteration);
                bool needsV2Update = (v2 == survivedIndex || v2 == deletedIndex) && (_timestamps[orig2] != _currentIteration);

                _timestamps[orig0] = _currentIteration;
                _timestamps[orig1] = _currentIteration;
                _timestamps[orig2] = _currentIteration;

                if (!needsV0Update && !needsV1Update && !needsV2Update)
                    continue;

                double3 p0 = vertices[v0].position;
                double3 p1 = vertices[v1].position;
                double3 p2 = vertices[v2].position;

                double3 bary = CalculateBarycentricCoords(optimalPosition, p0, p1, p2);

                _attributesApply.Apply(_attributes, bary, orig0, orig1, orig2, needsV0Update, needsV1Update, needsV2Update);
            }
        }

        private double3 CalculateBarycentricCoords(double3 point, double3 a, double3 b, double3 c)
        {
            double3 v0 = b - a;
            double3 v1 = c - a;
            double3 v2 = point - a;

            double d00 = math.dot(v0, v0);
            double d01 = math.dot(v0, v1);
            double d11 = math.dot(v1, v1);
            double d20 = math.dot(v2, v0);
            double d21 = math.dot(v2, v1);

            double denom = d00 * d11 - d01 * d01;

            if (math.abs(denom) < 1E-12)
                return new double3(1.0, 0.0, 0.0);

            double invDenom = 1.0 / denom;

            double v = (d11 * d20 - d01 * d21) * invDenom;
            double w = (d00 * d21 - d01 * d20) * invDenom;
            double u = 1.0 - v - w;

            return new double3(u, v, w);
        }
    }
}
