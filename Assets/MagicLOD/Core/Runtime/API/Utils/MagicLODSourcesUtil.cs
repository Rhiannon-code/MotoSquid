using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public static class MagicLODSourcesUtil
    {
        public static bool TryGatherMesh(GameObject root, out Mesh mesh, out Renderer renderer)
        {
            renderer = root.GetComponent<Renderer>();
            
            return TryGatherMesh(renderer, out mesh);
        }

        public static bool TryGatherMesh(Renderer root, out Mesh mesh)
        {
            if (root is MeshRenderer)
            {
                MeshFilter filter = root.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null)
                {
                    mesh = null;
                    return false;
                }

                mesh = filter.sharedMesh;
                return true;
            }
            else if (root is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
                return mesh != null;
            }

            mesh = null;
            return false;
        }

        public static bool TrySetMesh(Renderer renderer, Mesh mesh)
        {
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null)
                    return false;

                filter.sharedMesh = mesh;
                return true;
            }
            else if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.sharedMesh = mesh;
                return true;
            }

            return false;
        }

        public static bool TryGatherSources(GameObject target, out Mesh mesh, out Renderer renderer, bool includeLODs)
        {
            if (!target.TryGetComponent(out renderer))
            {
                mesh = null;
                renderer = null;
                return false;
            }

            if (!TryGatherMesh(renderer, out mesh))
            {
                mesh = null;
                renderer = null;
                return false;
            }

            if (!includeLODs && renderer.IsPartOfLODGroup())
            {
                mesh = null;
                renderer = null;
                return false;
            }

            return true;
        }

        public static void GatherSources(GameObject root, bool includeChildren, bool includeLODs, bool checkMeshIsReadable, ref List<Mesh> meshes, ref List<Renderer> renderers)
        {
            meshes ??= new List<Mesh>();
            renderers ??= new List<Renderer>();

            meshes.Clear();
            renderers.Clear();

            if (includeChildren)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled)
                        continue;

                    if (!includeLODs && renderer.IsPartOfLODGroup())
                        continue;

                    if (TryGatherMesh(renderer, out Mesh mesh))
                    {
                        if (checkMeshIsReadable && !mesh.isReadable)
                            continue;

                        meshes.Add(mesh);
                        renderers.Add(renderer);
                    }
                }
            }
            else
            {
                if (TryGatherMesh(root, out Mesh mesh, out Renderer renderer))
                {
                    if (!renderer.enabled)
                        return;

                    if (!includeLODs && renderer.IsPartOfLODGroup())
                        return;

                    if (checkMeshIsReadable && !mesh.isReadable)
                        return;

                    meshes.Add(mesh);
                    renderers.Add(renderer);
                }
            }
        }

        public static Bounds GetExtendedBounds(IList<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one * 3);

            Bounds extendedBounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Count; i++)
                extendedBounds.Encapsulate(renderers[i].bounds);

            return extendedBounds;
        }
    }
}
