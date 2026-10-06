using System;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public enum DecimationStatus : byte
    {
        Failed, Success
    }

    [Serializable]
    public struct DecimationResult
    {
        public Mesh[] decimatedMeshes;
        public DecimationStatus taskStatus;
        public string errorText;

        public DecimationResult(string errorText)
        {
            decimatedMeshes = null;
            taskStatus = DecimationStatus.Failed;

            this.errorText = errorText;
        }

        public DecimationResult(Mesh[] decimatedMeshes)
        {
            this.decimatedMeshes = decimatedMeshes;

            taskStatus = DecimationStatus.Success;
            errorText = string.Empty;
        }
    }
}
