using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    [Serializable]
    public struct LODGroupDescriptor
    {
        public LODFadeMode fadeMode;
        public float[] transitionHeight;
        public float[][] quality;
    }
}
