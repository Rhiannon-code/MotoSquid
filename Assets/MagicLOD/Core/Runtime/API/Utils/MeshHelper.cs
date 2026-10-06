using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public static class MeshHelper
    {
        public static uint GetTotalTriangles(this Mesh mesh)
        {
            uint totalTriangles = 0;

            int submeshCount = mesh.subMeshCount;

            for (int i = 0; i < submeshCount; i++)
                totalTriangles += mesh.GetIndexCount(i);

            return (totalTriangles / 3);
        }
    }
}
