using MotoSquid.AI;
using MotoSquid.Bike;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MotoSquid.Performance
{
    public class LODGeneratorWindow : EditorWindow
    {
        float lod0Screen  = 0.60f;

        bool  useLOD1     = true;
        float lod1Screen  = 0.25f;
        float lod1Quality = 0.50f;

        bool  useLOD2     = true;
        float lod2Screen  = 0.08f;
        float lod2Quality = 0.25f;

        bool  useLOD3     = false;
        float lod3Screen  = 0.02f;
        float lod3Quality = 0.10f;

        int    minTriangles  = 300;
        bool   selectedOnly  = false;
        bool   skipExisting  = true;
        string outputFolder  = "Assets/Generated/LODs";

        Vector2           scroll;
        readonly List<string> log = new List<string>();

        [MenuItem("Tools/LOD Generator")]
        static void Open() => GetWindow<LODGeneratorWindow>("LOD Generator");

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("LOD Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Scope", EditorStyles.miniBoldLabel);
            selectedOnly = EditorGUILayout.Toggle("Selected Only", selectedOnly);
            skipExisting = EditorGUILayout.Toggle("Skip Existing LODGroups", skipExisting);
            minTriangles = EditorGUILayout.IntField("Min Triangles (skip below)", minTriangles);
            outputFolder = EditorGUILayout.TextField("Mesh Asset Folder", outputFolder);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("LOD Levels", EditorStyles.miniBoldLabel);

            DrawLOD0();
            DrawLODLevel(ref useLOD1, 1, ref lod1Screen, ref lod1Quality);
            DrawLODLevel(ref useLOD2, 2, ref lod2Screen, ref lod2Quality);
            DrawLODLevel(ref useLOD3, 3, ref lod3Screen, ref lod3Quality);

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview")) Preview();
                var prev = GUI.color;
                GUI.color = Color.green;
                if (GUILayout.Button("Generate LODs")) Generate();
                GUI.color = prev;
            }

            if (log.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Result", EditorStyles.miniBoldLabel);
                foreach (var line in log)
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawLOD0()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("LOD 0: original mesh");
            lod0Screen = EditorGUILayout.Slider("Screen transition", lod0Screen, 0.05f, 1.00f);
            EditorGUI.indentLevel--;
        }

        void DrawLODLevel(ref bool enabled, int level, ref float screen, ref float quality)
        {
            enabled = EditorGUILayout.Toggle($"LOD {level}", enabled);
            if (!enabled) return;
            EditorGUI.indentLevel++;
            screen  = EditorGUILayout.Slider("Screen transition", screen,  0.005f, 0.95f);
            quality = EditorGUILayout.Slider("Quality (tri ratio)", quality, 0.02f, 0.95f);
            EditorGUI.indentLevel--;
        }

        List<MeshFilter> GatherCandidates()
        {
            MeshFilter[] all;
            if (selectedOnly)
            {
                var list = new List<MeshFilter>();
                foreach (var go in Selection.gameObjects)
                    list.AddRange(go.GetComponentsInChildren<MeshFilter>(true));
                all = list.ToArray();
            }
            else
            {
                all = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            }

            var result = new List<MeshFilter>();
            foreach (var mf in all)
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                if (skipExisting && mf.GetComponentInParent<LODGroup>() != null) continue;
                if (mf.sharedMesh.triangles.Length / 3 < minTriangles) continue;
                if (IsInBikeHierarchy(mf.transform)) continue;
                result.Add(mf);
            }
            return result;
        }

        static bool IsInBikeHierarchy(Transform t)
        {
            Transform current = t;
            while (current != null)
            {
                if (current.GetComponent<BikeController>() != null) return true;
                if (current.GetComponent<BikeAILogic>()    != null) return true;
                current = current.parent;
            }
            return false;
        }

        void Preview()
        {
            var cands = GatherCandidates();
            int tris = 0;
            foreach (var mf in cands) tris += mf.sharedMesh.triangles.Length / 3;
            log.Clear();
            log.Add($"{cands.Count} mesh(es) eligible, {tris:N0} total triangles.");
            Repaint();
        }

        void Generate()
        {
            var cands = GatherCandidates();
            if (cands.Count == 0)
            {
                log.Clear();
                log.Add("No candidates found. Lower Min Triangles or disable Skip Existing LODGroups.");
                Repaint();
                return;
            }

            EnsureFolderExists(outputFolder);

            int trisBefore = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Auto Generate LODs");

            for (int i = 0; i < cands.Count; i++)
            {
                EditorUtility.DisplayProgressBar("LOD Generator", cands[i].name, (float)i / cands.Count);
                trisBefore += ProcessMesh(cands[i]);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            log.Clear();
            log.Add($"Generated LODs for {cands.Count} mesh(es).");

            if (useLOD1)
            {
                int est = Mathf.RoundToInt(trisBefore * lod1Quality);
                float pct = (1f - lod1Quality) * 100f;
                log.Add($"LOD0: {trisBefore:N0} tris  →  LOD1 est: ~{est:N0} tris (~{pct:F0}% fewer at distance).");
            }

            log.Add("Save the scene to persist (Ctrl+S).");
            Repaint();
        }

        int ProcessMesh(MeshFilter mf)
        {
            var srcMesh = mf.sharedMesh;
            var srcMR   = mf.GetComponent<MeshRenderer>();
            int srcTris = srcMesh.triangles.Length / 3;

            // Wrap original under a new LODGroup parent
            var parent = new GameObject(mf.name + "_LODGroup");
            Undo.RegisterCreatedObjectUndo(parent, "LOD Parent");
            parent.transform.SetParent(mf.transform.parent, false);
            parent.transform.localPosition = mf.transform.localPosition;
            parent.transform.localRotation = mf.transform.localRotation;
            parent.transform.localScale    = mf.transform.localScale;
            parent.isStatic                = mf.gameObject.isStatic;

            Undo.SetTransformParent(mf.transform, parent.transform, "Move to LOD0");
            mf.transform.localPosition = Vector3.zero;
            mf.transform.localRotation = Quaternion.identity;
            mf.transform.localScale    = Vector3.one;

            var lodGroup = Undo.AddComponent<LODGroup>(parent);
            var lods     = new List<LOD>();
            lods.Add(new LOD(lod0Screen, new Renderer[] { srcMR }));

            string baseName = string.IsNullOrEmpty(srcMesh.name) ? mf.name : srcMesh.name;

            void AddLevel(bool enabled, int level, float screen, float quality)
            {
                if (!enabled) return;

                Mesh lodMesh = MeshDecimator.Decimate(srcMesh, quality);
                lodMesh.name = $"{baseName}_LOD{level}";
                string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{lodMesh.name}.asset");
                AssetDatabase.CreateAsset(lodMesh, path);

                var lodGO = new GameObject($"{mf.name}_LOD{level}");
                Undo.RegisterCreatedObjectUndo(lodGO, $"LOD{level} Object");
                lodGO.transform.SetParent(parent.transform, false);
                lodGO.isStatic = mf.gameObject.isStatic;

                Undo.AddComponent<MeshFilter>(lodGO).sharedMesh        = lodMesh;
                var lodMR = Undo.AddComponent<MeshRenderer>(lodGO);
                lodMR.sharedMaterials = srcMR.sharedMaterials;

                lods.Add(new LOD(screen, new Renderer[] { lodMR }));
            }

            AddLevel(useLOD1, 1, lod1Screen, lod1Quality);
            AddLevel(useLOD2, 2, lod2Screen, lod2Quality);
            AddLevel(useLOD3, 3, lod3Screen, lod3Quality);

            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(parent);

            return srcTris;
        }

        static void EnsureFolderExists(string path)
        {
            string[] parts   = path.Split('/');
            string   current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    public static class MeshDecimator
    {
        public static Mesh Decimate(Mesh source, float quality)
        {
            quality = Mathf.Clamp(quality, 0.01f, 0.99f);

            var srcVerts   = source.vertices;
            var srcNormals = source.normals;
            var srcUVs     = source.uv;
            int n = srcVerts.Length;

            if (n < 6) return Object.Instantiate(source);

            Bounds  bounds = source.bounds;
            Vector3 size   = bounds.size;

            // Grid resolution chosen so we get roughly quality*n representative vertices
            int   res  = Mathf.Max(2, Mathf.CeilToInt(Mathf.Pow(n * quality, 1f / 3f)));
            float invX = size.x > 1e-5f ? res / size.x : 0f;
            float invY = size.y > 1e-5f ? res / size.y : 0f;
            float invZ = size.z > 1e-5f ? res / size.z : 0f;

            var cellMap   = new Dictionary<long, int>(n);
            var newVerts  = new List<Vector3>(Mathf.RoundToInt(n * quality));
            var newNormals = srcNormals.Length == n ? new List<Vector3>() : null;
            var newUVs     = srcUVs.Length    == n ? new List<Vector2>() : null;
            var remap = new int[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 v  = srcVerts[i];
                int gx = invX > 0 ? Mathf.Clamp(Mathf.FloorToInt((v.x - bounds.min.x) * invX), 0, res - 1) : 0;
                int gy = invY > 0 ? Mathf.Clamp(Mathf.FloorToInt((v.y - bounds.min.y) * invY), 0, res - 1) : 0;
                int gz = invZ > 0 ? Mathf.Clamp(Mathf.FloorToInt((v.z - bounds.min.z) * invZ), 0, res - 1) : 0;
                long key = gx + (long)res * gy + (long)res * res * gz;

                if (!cellMap.TryGetValue(key, out int idx))
                {
                    idx = newVerts.Count;
                    cellMap[key] = idx;
                    newVerts.Add(v);
                    newNormals?.Add(srcNormals[i]);
                    newUVs?.Add(srcUVs[i]);
                }
                remap[i] = idx;
            }

            var result = new Mesh
            {
                indexFormat = newVerts.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            result.vertices = newVerts.ToArray();
            if (newNormals != null) result.normals = newNormals.ToArray();
            if (newUVs     != null) result.uv      = newUVs.ToArray();

            result.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] srcTris = source.GetTriangles(s);
                var   outTris = new List<int>(srcTris.Length / 2);
                for (int i = 0; i + 2 < srcTris.Length; i += 3)
                {
                    int a = remap[srcTris[i]], b = remap[srcTris[i + 1]], c = remap[srcTris[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    outTris.Add(a); outTris.Add(b); outTris.Add(c);
                }
                result.SetTriangles(outTris, s);
            }

            result.RecalculateBounds();
            if (newNormals == null) result.RecalculateNormals();
            // Tangents are never copied above, and a normal-mapped material on a mesh without them
            // shades against an undefined basis.
            if (newUVs != null) result.RecalculateTangents();
            return result;
        }
    }
}
#endif
