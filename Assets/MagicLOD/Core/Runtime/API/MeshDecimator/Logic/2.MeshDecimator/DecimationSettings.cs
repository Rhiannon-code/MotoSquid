using System;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public enum PenaltyWeight : ushort
    {
        None = 0,
        Low = 10,
        Medium = 100,
        High = 1000
    }

    public enum AttributeInterpolationMode : byte
    {
        None,
        Barycentric,
        Quadric
    }

    public enum AttributeInterpolationMode1 : byte
    {
        None,
        Barycentric,
        Quadric,
        Recalculate
    }

    public enum AttributeInterpolationMode2 : byte
    {
        None,
        Barycentric,
        Quadric,
        Recalculate_EditorOnly
    }

    public enum BoneWeightInterpolationMode : byte
    {
        None,
        Interpolate
    }

    [Serializable]
    public struct DecimationSettings : IEquatable<DecimationSettings>
    {
        public static DecimationSettings Default
        {
            get
            {
                return new DecimationSettings()
                {
                    bordersPenaltyWeight = PenaltyWeight.High,
                    uvSeamsPenaltyWeight = PenaltyWeight.Low,
                    normalSeamsPenaltyWeight = PenaltyWeight.None,

                    uvMode = AttributeInterpolationMode.Quadric,
                    uv2Mode = AttributeInterpolationMode2.Quadric,
                    uv3Mode = AttributeInterpolationMode.None,
                    uv4Mode = AttributeInterpolationMode.None,

                    normalsMode = AttributeInterpolationMode1.Quadric,
                    tangentsMode = AttributeInterpolationMode1.Barycentric,
                    colorsMode = AttributeInterpolationMode.Barycentric,
                    boneWeightsInterpolationMode = BoneWeightInterpolationMode.Interpolate
                };
            }
        }

        public PenaltyWeight bordersPenaltyWeight;
        public PenaltyWeight uvSeamsPenaltyWeight;
        public PenaltyWeight normalSeamsPenaltyWeight;

        public AttributeInterpolationMode uvMode;
        public AttributeInterpolationMode2 uv2Mode;
        public AttributeInterpolationMode uv3Mode;
        public AttributeInterpolationMode uv4Mode;

        public AttributeInterpolationMode1 normalsMode;
        public AttributeInterpolationMode1 tangentsMode;
        public AttributeInterpolationMode colorsMode;
        public BoneWeightInterpolationMode boneWeightsInterpolationMode;

        public PreservationVolume[] preservationVolumes;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)bordersPenaltyWeight;
                hash = hash * 31 + (int)uvSeamsPenaltyWeight;
                hash = hash * 31 + (int)normalSeamsPenaltyWeight;
                hash = hash * 31 + (int)uvMode;
                hash = hash * 31 + (int)uv2Mode;
                hash = hash * 31 + (int)uv3Mode;
                hash = hash * 31 + (int)uv4Mode;
                hash = hash * 31 + (int)normalsMode;
                hash = hash * 31 + (int)tangentsMode;
                hash = hash * 31 + (int)colorsMode;
                hash = hash * 31 + (int)boneWeightsInterpolationMode;

                if (preservationVolumes != null)
                {
                    for (int i = 0; i < preservationVolumes.Length; i++)
                    {
                        hash = hash * 31 + preservationVolumes[i].GetHashCode();
                    }
                }

                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is DecimationSettings other && Equals(other);
        }

        public bool Equals(DecimationSettings other)
        {
            if (bordersPenaltyWeight != other.bordersPenaltyWeight ||
                uvSeamsPenaltyWeight != other.uvSeamsPenaltyWeight ||
                normalSeamsPenaltyWeight != other.normalSeamsPenaltyWeight ||
                uvMode != other.uvMode ||
                uv2Mode != other.uv2Mode ||
                uv3Mode != other.uv3Mode ||
                uv4Mode != other.uv4Mode ||
                normalsMode != other.normalsMode ||
                tangentsMode != other.tangentsMode ||
                colorsMode != other.colorsMode ||
                boneWeightsInterpolationMode != other.boneWeightsInterpolationMode)
            {
                return false;
            }

            if (ReferenceEquals(preservationVolumes, other.preservationVolumes)) 
                return true;

            if (preservationVolumes == null || other.preservationVolumes == null) 
                return false;

            if (preservationVolumes.Length != other.preservationVolumes.Length) 
                return false;

            for (int i = 0; i < preservationVolumes.Length; i++)
            {
                if (!preservationVolumes[i].Equals(other.preservationVolumes[i])) 
                    return false;
            }

            return true;
        }
    }
}