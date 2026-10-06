using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MotoSquid.Track
{
    public class RoadColliderFixer : MonoBehaviour
    {
        [Header("Legacy mesh rebuild (destructive: leave off)")]
        public bool rebuildMeshes = false;
        public float degenerateThresholdSqr = 1e-8f;

        const MeshColliderCookingOptions FullCooking =
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.UseFastMidphase;

        private void Awake()
        {
            int fixedUp = 0, rebuilt = 0, skipped = 0, unreadable = 0, degenTotal = 0;

            foreach (var mc in FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rebuildMeshes)
                {
                    RebuildCollider(mc, ref rebuilt, ref skipped, ref unreadable, ref degenTotal);
                    continue;
                }

                // Only touch the ones that are wrong: assigning cookingOptions re-cooks the mesh.
                if (mc.sharedMesh == null || mc.cookingOptions == FullCooking) continue;
                mc.cookingOptions = FullCooking;
                fixedUp++;
            }

            if (rebuildMeshes)
                Debug.LogWarning($"[RoadColliderFixer] LEGACY REBUILD: rebuilt {rebuilt}, left {unreadable} " +
                                 $"unreadable and {skipped} empty alone, deleted {degenTotal} triangle(s).", this);
            else
                Debug.Log($"[RoadColliderFixer] Cooking options corrected on {fixedUp} collider(s).");
        }

    #region Legacy rebuild

        private void RebuildCollider(MeshCollider mc, ref int rebuilt, ref int skipped, ref int unreadable, ref int degenTotal)
        {
            Mesh original = mc.sharedMesh;
            if (original == null) return;

            if (!original.isReadable) { unreadable++; return; }

            Mesh physics = BuildPhysicsMesh(original, mc.transform.lossyScale, out int removed, out int kept);
            degenTotal += removed;

            if (kept == 0)
            {
                DestroyPhysicsMesh(physics);

                if (removed > 0)
                {
                    Debug.LogWarning($"[RoadColliderFixer] Every triangle on '{original.name}' measured degenerate. " +
                                     "Leaving the collider alone rather than deleting a surface.", mc);
                    return;
                }

                mc.enabled = false;
                skipped++;
                return;
            }

            Mesh previous = mc.sharedMesh;
            mc.sharedMesh     = null;
            mc.cookingOptions = FullCooking;
            mc.sharedMesh     = physics;

            if (previous != null && previous.name.EndsWith("_physics")) DestroyPhysicsMesh(previous);
            rebuilt++;
        }

        private static void DestroyPhysicsMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }

        private Mesh BuildPhysicsMesh(Mesh source, Vector3 lossyScale, out int removedTriangles, out int keptTriangles)
        {
            removedTriangles = 0;
            keptTriangles    = 0;
            Vector3[] verts  = source.vertices;

            Mesh physics    = new Mesh();
            physics.name    = source.name + "_physics";
            physics.indexFormat  = source.indexFormat;
            physics.vertices     = verts;
            physics.subMeshCount = source.subMeshCount;

            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] src   = source.GetTriangles(s);
                List<int> clean = new List<int>(src.Length);

                for (int i = 0; i + 2 < src.Length; i += 3)
                {
                    Vector3 a = Vector3.Scale(verts[src[i]],     lossyScale);
                    Vector3 b = Vector3.Scale(verts[src[i + 1]], lossyScale);
                    Vector3 c = Vector3.Scale(verts[src[i + 2]], lossyScale);

                    if (Vector3.Cross(b - a, c - a).sqrMagnitude < degenerateThresholdSqr)
                    {
                        removedTriangles++;
                        continue;
                    }

                    clean.Add(src[i]);
                    clean.Add(src[i + 1]);
                    clean.Add(src[i + 2]);
                }

                keptTriangles += clean.Count / 3;
                physics.SetTriangles(clean, s);
            }

            physics.RecalculateBounds();
            return physics;
        }

    #endregion

#if UNITY_EDITOR
        [ContextMenu("Bake Cooking Options Into Scene")]
        private void BakeCookingOptions()
        {
            int n = 0;
            foreach (var mc in FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mc.cookingOptions == FullCooking) continue;
                Undo.RecordObject(mc, "Bake Collider Cooking Options");
                mc.cookingOptions = FullCooking;
                EditorUtility.SetDirty(mc);
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[RoadColliderFixer] Baked full cooking options into {n} collider(s). Save the scene to persist.");
        }

        [ContextMenu("Report Colliders With Disabled Or Missing Meshes")]
        private void ReportBrokenColliders()
        {
            int missing = 0, off = 0, unreadable = 0;
            foreach (var mc in FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mc.sharedMesh == null) { Debug.LogWarning($"[RoadColliderFixer] '{GetPath(mc.transform)}' has no mesh.", mc); missing++; }
                else if (!mc.enabled)      { Debug.LogWarning($"[RoadColliderFixer] '{GetPath(mc.transform)}' is disabled.", mc); off++; }
                else if (!mc.sharedMesh.isReadable) unreadable++;
            }
            Debug.Log($"[RoadColliderFixer] {missing} collider(s) with no mesh, {off} disabled, {unreadable} on non-readable meshes.");
        }

        static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
#endif
    }
}
