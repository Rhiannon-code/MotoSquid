#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MotoSquid.World
{
    public class TreeModelPostprocessor : AssetPostprocessor
    {
        const string PATH_FILTER = "Models/Trees";

        static readonly Quaternion FIX = Quaternion.Euler(-90f, 0f, 0f);

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').Contains(PATH_FILTER))
                return;

            int meshCount = 0;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                RotateMesh(mf.sharedMesh);
                meshCount++;
            }

            if (meshCount > 0)
                Debug.Log($"[TreePostprocessor] Rotated {meshCount} mesh(es) in {assetPath}");
        }

        static void RotateMesh(Mesh mesh)
        {
            Vector3[] verts   = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;

            for (int i = 0; i < verts.Length; i++)
                verts[i] = FIX * verts[i];

            for (int i = 0; i < normals.Length; i++)
                normals[i] = FIX * normals[i];

            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = FIX * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }

            mesh.vertices  = verts;
            mesh.normals   = normals;
            mesh.tangents  = tangents;
            mesh.RecalculateBounds();
        }

        [MenuItem("Tools/Force Reimport Tree Models")]
        static void ForceReimport()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Model",
                new[] { "Assets/Models/Trees" }
            );
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(
                    AssetDatabase.GUIDToAssetPath(guid),
                    ImportAssetOptions.ForceUpdate
                );
            Debug.Log($"[TreePostprocessor] Force reimported {guids.Length} tree model(s).");
        }
    }
}
#endif
