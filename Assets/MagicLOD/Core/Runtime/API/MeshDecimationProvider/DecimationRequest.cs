using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public struct DecimationRequest : IEquatable<DecimationRequest>
    {
        public Mesh originalMesh;
        public IReadOnlyList<float> quality;
        public float maxSimplificationError;
        public DecimationSettings settings;

        public DecimationRequest(Mesh originalMesh, IReadOnlyList<float> quality)
            : this (originalMesh, quality, DecimationSettings.Default)
        {
        
        }

        public DecimationRequest(Mesh originalMesh, IReadOnlyList<float> quality, DecimationSettings settings)
        {
            this.originalMesh = originalMesh;
            this.quality = quality;
            this.settings = settings;

            maxSimplificationError = float.MaxValue;
        }

        public DecimationRequest(Mesh originalMesh, float maxSimplificationError)
            : this(originalMesh, maxSimplificationError, DecimationSettings.Default)
        {
            
        }

        public DecimationRequest(Mesh originalMesh, float maxSimplificationError, DecimationSettings settings)
        {
            this.originalMesh = originalMesh;
            this.maxSimplificationError = maxSimplificationError;
            this.settings = settings;

            quality = null;
        }

        public DecimationRequest(Mesh originalMesh, IReadOnlyList<float> quality, float maxSimplificationError, DecimationSettings settings)
        {
            this.originalMesh = originalMesh;
            this.quality = quality;
            this.maxSimplificationError = maxSimplificationError;
            this.settings = settings;
        }

        public override bool Equals(object obj)
        {
            return obj is DecimationRequest other && Equals(other);
        }

        public bool Equals(DecimationRequest other)
        {
            if (originalMesh != other.originalMesh) 
                return false;

            if (maxSimplificationError != other.maxSimplificationError) 
                return false;

            if (!settings.Equals(other.settings)) 
                return false;

            if (ReferenceEquals(quality, other.quality)) 
                return true;

            if (quality == null || other.quality == null) 
                return false;

            if (quality.Count != other.quality.Count) 
                return false;

            for (int i = 0; i < quality.Count; i++)
            {
                if (quality[i] != other.quality[i]) 
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + (originalMesh != null ? originalMesh.GetHashCode() : 0);
                hash = hash * 31 + maxSimplificationError.GetHashCode();

                if (quality != null)
                {
                    for (int i = 0; i < quality.Count; i++)
                    {
                        hash = hash * 31 + quality[i].GetHashCode();
                    }
                }

                hash = hash * 31 + settings.GetHashCode();

                return hash;
            }
        }
    }
}
