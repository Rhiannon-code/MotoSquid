using System;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public struct Vertex
    {
        public double3 position;
        public int version;

        public Vertex(double3 position)
        {
            this.position = position;
            version = 0;
        }

        public Vertex(Vector3 position)
        {
            this.position = new double3(position.x, position.y, position.z);
            version = 0;
        }
    }

    public struct Triangle
    {
        public int v0;
        public int v1;
        public int v2;

        public int origV0;
        public int origV1;
        public int origV2;

        public double3 normal;
        public int subMeshIndex;

        public Triangle(int v0, int v1, int v2, int origV0, int origV1, int origV2, double3 normal, int subMeshIndex)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;

            this.origV0 = origV0;
            this.origV1 = origV1;
            this.origV2 = origV2;

            this.normal = normal;
            this.subMeshIndex = subMeshIndex;
        }

        public bool IsDegenerated()
        {
            return (v0 == v1) || (v1 == v2) || (v2 == v0);
        }
    }

    public struct Edge : IEquatable<Edge>
    {
        public int vertex1;
        public int vertex2;

        public int origVertex1;
        public int origVertex2;

        public bool isBorder;
        public bool isNormalSeam;
        public bool isUVSeam;

        public double3 triangleNormal1;
        public double3 triangleNormal2;
        public double3 optimalPosition;

        public double error;
        public int versionSum;

        public Edge(int vertex1, int vertex2, int origVertex1, int origVertex2, double3 normal, bool isBorder = false)
        {
            if (vertex1 > vertex2)
            {
                this.vertex1 = vertex1;
                this.vertex2 = vertex2;
                this.origVertex1 = origVertex1;
                this.origVertex2 = origVertex2;
            }
            else
            {
                this.vertex1 = vertex2;
                this.vertex2 = vertex1;
                this.origVertex1 = origVertex2;
                this.origVertex2 = origVertex1;
            }

            this.isBorder = isBorder;
            isNormalSeam = false;
            isUVSeam = false;

            triangleNormal1 = normal;
            triangleNormal2 = normal;

            optimalPosition = double3.zero;
            error = 0;
            versionSum = 0;
        }

        public bool Equals(Edge other)
        {
            return (vertex1 == other.vertex1) && (vertex2 == other.vertex2);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (vertex1 * 397) ^ vertex2;
            }
        }
    }
}