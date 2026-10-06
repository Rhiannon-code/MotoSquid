#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NGS.MagicLOD.Runtime;
using NGS.MagicLOD.Editors.Components;

namespace NGS.MagicLOD.Editors.API
{
    public class MagicLODEditorExport
    {
        [MenuItem("Tools/NGSTools/MagicLOD/Export/Selected")]
        public static void ExportSelectedMeshes()
        {
            try
            {
                PersistentDecimationRequestsCache.BeginExportBatch();

                HashSet<Renderer> uniqRenderers = new HashSet<Renderer>();
                HashSet<Mesh> uniqMeshes = new HashSet<Mesh>();

                string folderPath = GetExportFolderPath();

                foreach (var selectedGO in Selection.gameObjects)
                {
                    foreach (var renderer in selectedGO.GetComponentsInChildren<Renderer>())
                    {
                        if (uniqRenderers.Add(renderer))
                        {
                            if (MagicLODSourcesUtil.TryGatherMesh(renderer, out Mesh mesh))
                            {
                                if (uniqMeshes.Add(mesh))
                                    PersistentDecimationRequestsCache.TryExportMesh(mesh, folderPath, $"{mesh.name}_{mesh.GetHashCode()}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                PersistentDecimationRequestsCache.EndExportBatch();
            }
        }


        private static string GetExportFolderPath()
        {
            MagicLODOverlay overlay = GameObject.FindAnyObjectByType<MagicLODOverlay>();

            if (overlay != null)
                return overlay.Settings.ExportFolder;

            return PersistentDecimationRequestsCache.DEFAULT_FOLDER_PATH;
        }
    }
}

#endif
