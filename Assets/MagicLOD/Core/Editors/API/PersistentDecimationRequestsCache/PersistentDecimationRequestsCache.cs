#if UNITY_EDITOR

using NGS.MagicLOD.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NGS.MagicLOD.Editors.API
{
    public static class PersistentDecimationRequestsCache
    {
        public const string DEFAULT_FOLDER_PATH = "Assets/MagicLOD/GeneratedMeshes/";
        private const float IDLE_TIMER_SECONDS = 30;
        private const float DEBOUNCE_TIMER_SECONDS = 0.1f;
        private const int MAX_PENDING_MESHES_SIZE = 1000;

        private static Dictionary<string, Mesh> _pendingMeshes;
        private static bool _isEditorUpdating;
        private static int _editingCounter;
        private static double _lastActivityTime;


        static PersistentDecimationRequestsCache()
        {
            _pendingMeshes = new Dictionary<string, Mesh>();

            SubscribeEditorUpdating();
            UpdateLastActivityTimer();

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        public static bool Contains(DecimationRequest request, string folderPath)
        {
            if (!AssetsPathUtil.ValidateFolderPath(folderPath, out string normalizedPath))
                return false;

            if (!ValidateDecimationRequest(request))
                return false;

            int expectedCount = GetExpectedMeshCount(request);

            for (int i = 0; i < expectedCount; i++)
            {
                if (IsOriginalMeshExpected(request, i))
                    continue;

                string assetPath = $"{normalizedPath}{CreateMeshName(request, i)}.asset";

                if (!_pendingMeshes.ContainsKey(assetPath) && !File.Exists(assetPath))
                    return false;
            }

            return true;
        }

        public static void BeginExportBatch()
        {
            _editingCounter++;
        }

        public static bool TryExportMesh(Mesh mesh, string folderPath, string meshName)
        {
            UpdateLastActivityTimer();

            if (mesh == null)
                return false;

            if (!AssetsPathUtil.ValidateFolderPath(folderPath, out string normalizedPath))
                normalizedPath = DEFAULT_FOLDER_PATH;

            if (!AssetsPathUtil.IsDirectoryExists(normalizedPath))
            {
                if (!AssetsPathUtil.TryCreateFolder(normalizedPath))
                    return false;
            }

            string assetPath = $"{normalizedPath}{meshName}.asset";

            if (_pendingMeshes.ContainsKey(assetPath))
                return true;

            if (!AssetDatabase.Contains(mesh))
            {
                _pendingMeshes[assetPath] = mesh;

                if (!_isEditorUpdating)
                    SubscribeEditorUpdating();
            }
            
            return true;
        }

        public static bool TryExportDecimationResult(DecimationRequest request, ref DecimationResult result, string folderPath)
        {
            UpdateLastActivityTimer();

            if (!ValidateDecimationRequest(request))
                return false;

            if (!ValidateDecimationResult(result))
                return false;

            if (!AssetsPathUtil.ValidateFolderPath(folderPath, out string normalizedPath))
                normalizedPath = DEFAULT_FOLDER_PATH;
            
            if (!AssetsPathUtil.IsDirectoryExists(normalizedPath))
            {
                if (!AssetsPathUtil.TryCreateFolder(normalizedPath))
                    return false;
            }

            for (int i = 0; i < result.decimatedMeshes.Length; i++)
            {
                string assetName = CreateMeshName(request, i);
                string assetPath = $"{normalizedPath}{assetName}.asset";

                if (TryLoadMeshAtPath(assetPath, out Mesh existedMesh))
                {
                    result.decimatedMeshes[i] = existedMesh;
                    continue;
                }

                Mesh decimatedMesh = result.decimatedMeshes[i];

                if (decimatedMesh == request.originalMesh)
                    continue;

                if (AssetDatabase.Contains(decimatedMesh))
                    continue;
                
                _pendingMeshes[assetPath] = decimatedMesh;

                if (!_isEditorUpdating)
                    SubscribeEditorUpdating();
            }

            return true;
        }

        public static bool TryLoadDecimationResult(DecimationRequest request, string folderPath, out DecimationResult result)
        {
            if (!Contains(request, folderPath))
            {
                result = default;
                return false;
            }

            if (!AssetsPathUtil.ValidateFolderPath(folderPath, out string normalizedPath))
                normalizedPath = DEFAULT_FOLDER_PATH;

            int expectedCount = GetExpectedMeshCount(request);

            Mesh[] decimatedMeshes = new Mesh[expectedCount];

            for (int i = 0; i < expectedCount; i++)
            {
                if (IsOriginalMeshExpected(request, i))
                {
                    decimatedMeshes[i] = request.originalMesh;
                    continue;
                }

                string assetPath = $"{normalizedPath}{CreateMeshName(request, i)}.asset";

                if (!TryLoadMeshAtPath(assetPath, out Mesh existedMesh))
                {
                    result = default;
                    return false;
                }

                decimatedMeshes[i] = existedMesh;
            }

            result = new DecimationResult(decimatedMeshes);
            return true;
        }

        public static void EndExportBatch()
        {
            _editingCounter--;

            _editingCounter = Mathf.Max(0, _editingCounter);
        }

        public static void ClearUnused(string folderPath)
        {
            throw new NotImplementedException();
        }


        private static void EditorUpdate()
        {
            if (_pendingMeshes.Count == 0)
            {
                UnsubscribeEditorUpdating();
                return;
            }

            if (_pendingMeshes.Count > MAX_PENDING_MESHES_SIZE)
            {
                ExportPendingMeshes();
                return;
            }

            double secondsSinceLastActivity = GetSecondsSinceLastActivity();

            if (_editingCounter == 0 && secondsSinceLastActivity > DEBOUNCE_TIMER_SECONDS)
            {
                ExportPendingMeshes();
                return;
            }

            if (_pendingMeshes.Count > 0 && secondsSinceLastActivity > IDLE_TIMER_SECONDS)
            {
                ExportPendingMeshes();
                return;
            }
        }

        private static void SubscribeEditorUpdating()
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            _isEditorUpdating = true;
        }

        private static void UnsubscribeEditorUpdating()
        {
            EditorApplication.update -= EditorUpdate;

            _isEditorUpdating = false;
        }

        private static void ExportPendingMeshes()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var keyValue in _pendingMeshes)
                {
                    string path = keyValue.Key;
                    Mesh mesh = keyValue.Value;

                    if (mesh == null)
                        continue;

                    try
                    {
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    catch(Exception ex)
                    {
                        Debug.LogError(ex);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(); 

                _pendingMeshes.Clear();
            }
        }

        private static bool TryLoadMeshAtPath(string assetPath, out Mesh mesh)
        {
            try
            {
                if (_pendingMeshes.TryGetValue(assetPath, out mesh))
                {
                    if (mesh != null)
                        return true;

                    _pendingMeshes.Remove(assetPath);
                }

                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                return mesh != null;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                mesh = null;
                return false;
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            UnsubscribeEditorUpdating();

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            if (_pendingMeshes.Count > 0)
                ExportPendingMeshes();
        }


        private static void UpdateLastActivityTimer()
        {
            _lastActivityTime = EditorApplication.timeSinceStartup;
        }

        private static double GetSecondsSinceLastActivity()
        {
            return EditorApplication.timeSinceStartup - _lastActivityTime;
        }

        private static bool IsOriginalMeshExpected(DecimationRequest request, int index)
        {
            if (request.quality != null && request.quality.Count > index)
                return request.quality[index] >= 0.9999f;

            return false;
        }

        private static int GetExpectedMeshCount(DecimationRequest request)
        {
            return (request.quality != null && request.quality.Count > 0) ? request.quality.Count : 1;
        }

        private static bool ValidateDecimationRequest(DecimationRequest request)
        {
            if (request.originalMesh == null)
                return false;

            return true;
        }

        private static bool ValidateDecimationResult(DecimationResult result)
        {
            if (result.taskStatus == DecimationStatus.Failed || result.decimatedMeshes == null)
                return false;

            foreach (var mesh in result.decimatedMeshes)
            {
                if (mesh == null)
                    return false;
            }

            return true;
        }

        private static string CreateMeshName(DecimationRequest request, int index)
        {
            string originalName = AssetsPathUtil.SanitizeFileName(request.originalMesh.name);

            bool isCreateLODs = request.quality != null && request.quality.Count > 0;
            char operationSymbol = isCreateLODs ? 'q' : 's';

            string parameter = (isCreateLODs ? request.quality[index] : request.maxSimplificationError)
                .ToString(CultureInfo.InvariantCulture).Replace('.', '_');

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(request.originalMesh, out string guid, out long localId);

            string hash = Hash128
                .Compute($"{guid}_{localId}_{request.settings.GetHashCode()}")
                .ToString()
                .Substring(0, 12);

            return $"{originalName}_{operationSymbol}{parameter}_{hash}";
        }
    }
}

#endif
