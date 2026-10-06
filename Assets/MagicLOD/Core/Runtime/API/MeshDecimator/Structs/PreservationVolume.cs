using System;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    [Serializable]
    public struct PreservationVolume : IEquatable<PreservationVolume>
    {
        public Bounds bounds;
        public PenaltyWeight penaltyWeight;

        public PreservationVolume(Bounds bounds, PenaltyWeight penaltyWeight = PenaltyWeight.High)
        {
            this.bounds = bounds;
            this.penaltyWeight = penaltyWeight;
        }

        public bool Contains(double3 localPoint)
        {
            return bounds.Contains(new Vector3((float)localPoint.x, (float)localPoint.y, (float)localPoint.z));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + bounds.GetHashCode();
                hash = hash * 31 + (int)penaltyWeight;

                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is PreservationVolume other && Equals(other);
        }

        public bool Equals(PreservationVolume other)
        {
            return bounds == other.bounds && penaltyWeight == other.penaltyWeight;
        }
    }
}
