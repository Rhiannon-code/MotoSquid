using System;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public struct Vector3IntExact : IEquatable<Vector3IntExact>
    {
        private const int POSITION_THRESHOLD = 1000;

        public int3 pos;


        public Vector3IntExact(float3 v)
        {
            unchecked
            {
                pos = (int3)math.round(v * POSITION_THRESHOLD);
            }
        }

        public override int GetHashCode()
        {
            return (int)math.hash(pos);
        }

        public bool Equals(Vector3IntExact other)
        {
            return math.all(pos == other.pos);
        }
    }

    public struct VertexQuadric
    {
        public double m0, m1, m2, m3;
        public double m4, m5, m6;
        public double m7, m8;
        public double m9;

        public VertexQuadric(double3 normal, double3 point, double weight = 1.0)
        {
            double a = normal.x;
            double b = normal.y;
            double c = normal.z;
            double d = -math.dot(normal, point);

            m0 = a * a * weight; m1 = a * b * weight; m2 = a * c * weight; m3 = a * d * weight;
            m4 = b * b * weight; m5 = b * c * weight; m6 = b * d * weight;
            m7 = c * c * weight; m8 = c * d * weight;
            m9 = d * d * weight;
        }

        public void Add(ref VertexQuadric other)
        {
            m0 += other.m0; m1 += other.m1; m2 += other.m2; m3 += other.m3;
            m4 += other.m4; m5 += other.m5; m6 += other.m6;
            m7 += other.m7; m8 += other.m8;
            m9 += other.m9;
        }

        public void Multiply(double multiplier)
        {
            m0 *= multiplier; m1 *= multiplier; m2 *= multiplier; m3 *= multiplier;
            m4 *= multiplier; m5 *= multiplier; m6 *= multiplier;
            m7 *= multiplier; m8 *= multiplier;
            m9 *= multiplier;
        }

        public double ComputeVertexError(double3 pos)
        {
            return m0 * pos.x * pos.x + 2 * m1 * pos.x * pos.y + 2 * m2 * pos.x * pos.z + 2 * m3 * pos.x
                + m4 * pos.y * pos.y + 2 * m5 * pos.y * pos.z + 2 * m6 * pos.y
                + m7 * pos.z * pos.z + 2 * m8 * pos.z
                + m9;
        }

        public bool TryGetOptimalPosition(out double3 result)
        {
            double det = m0 * m4 * m7 + m2 * m1 * m5 + m1 * m5 * m2
                       - m2 * m4 * m2 - m0 * m5 * m5 - m1 * m1 * m7;

            if (math.abs(det) < 1E-6)
            {
                result = new double3(0, 0, 0);
                return false;
            }

            double invDet = 1.0 / det;

            double detX = -m3 * m4 * m7 - m6 * m5 * m2 - m8 * m1 * m5
                        + m8 * m4 * m2 + m3 * m5 * m5 + m6 * m1 * m7;

            double detY = -m0 * m6 * m7 - m1 * m8 * m2 - m2 * m3 * m5
                        + m2 * m6 * m2 + m0 * m8 * m5 + m1 * m3 * m7;

            double detZ = -m0 * m4 * m8 - m1 * m5 * m3 - m2 * m1 * m6
                        + m2 * m4 * m3 + m0 * m5 * m6 + m1 * m1 * m8;

            result = new double3(detX * invDet, detY * invDet, detZ * invDet);

            return true;
        }
    }
}

