#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using NGS.MagicLOD.Runtime;
using NGS.MagicLOD.Editors.API;

using Debug = UnityEngine.Debug;

using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace NGS.MagicLOD.Editors.Components
{
    public enum OperationMode { GenerateLODs, Simplify }
    public enum SyncMode { NotSync, WithSameMesh, WithSameGroupID }

    public class MagicLODEditorComponent : MonoBehaviour
    {
        public Option Options
        {
            get
            {
                if (_options == null)
                    _options = new Option(this);

                _options.SetOwner(this);

                return _options;
            }
        }

        public bool IsManualLODConfigurator
        {
            get
            {
                return Options.LODGroupConfigurator is ManualLODConfigurator;
            }
            set
            {
                if (value == true)
                {
                    if (!(Options.LODGroupConfigurator is ManualLODConfigurator))
                    {
                        Options.LODGroupConfigurator = new ManualLODConfigurator(
                            new List<float>() { 1f, 0.5f, 0.25f },
                            new List<float>() { 0.7f, 0.5f, 0.01f });
                    }
                }
                else
                {
                    if (!(Options.LODGroupConfigurator is ScreenSpaceLODConfigurator))
                        Options.LODGroupConfigurator = new ScreenSpaceLODConfigurator();
                }
            }
        }

        public bool IsPreviewStarted
        {
            get
            {
                return _preview?.IsStarted ?? false;
            }
        }
        public int CurrentPreviewLevel
        {
            get
            {
                return _preview.CurrentLOD;
            }
            set
            {
                _preview.CurrentLOD = value;
            }
        }
        public int MaxPreviewLevel
        {
            get
            {
                return _preview.MaxLODs;
            }
        }

        public bool IsPreservationsEditing
        {
            get
            {
                return _preservationsEdit?.IsStarted ?? false;
            }
        }

        public bool CanApplySimplifiedMesh
        {
            get
            {
                return _options.OperationMode == OperationMode.Simplify && _backup.IsCreated;
            }
        }

        public bool IsDecimated
        {
            get
            {
                return _backup.IsCreated;
            }
        }
        public bool IsDecimationInProgress
        {
            get
            {
                return !_decimation.IsAllRequestsCompleted;
            }
        }

        public int TotalDecimationRequests
        {
            get
            {
                return _decimation?.TotalRequests ?? 0;
            }
        }
        public int SuccessfulDecimationRequests
        {
            get
            {
                return _decimation?.SuccessRequests ?? 0;
            }
        }
        public int FailedDecimationRequests
        {
            get
            {
                return _decimation?.FailedRequests ?? 0;
            }
        }

        public int OriginalTrianglesCount
        {
            get
            {
                return _decimation.OriginalTrianglesCount;
            }
        }
        public int SimplifiedTrianglesCount
        {
            get
            {
                return _decimation.SimplifiedTrianglesCount;
            }
        }
        public int[] DecimatedTrianglesCount
        {
            get
            {
                return _decimation.DecimatedTrianglesCount;
            }
        }
        public string ErrorsText
        {
            get
            {
                return _decimation.ErrorsText;
            }
        }
        public bool HasErrors
        {
            get
            {
                return _decimation.HasErrors;
            }
        }


        [SerializeField]
        private Option _options;

        [SerializeField]
        private Decimation _decimation;

        [SerializeField]
        private Preview _preview;

        [SerializeField]
        private PreservationsEdit _preservationsEdit;

        [SerializeField]
        private Applier _applier;

        [SerializeField]
        private Backup _backup;

        private List<Mesh> _gatheredMeshes;
        private List<Renderer> _gatheredRenderers;
        private bool _isPersistentExportBatchBegin;


        private void Reset()
        {
            hideFlags |= HideFlags.DontSaveInBuild;

            _options = new Option(this);
            _decimation = new Decimation();
            _preview = new Preview();
            _preservationsEdit = new PreservationsEdit();
            _applier = new Applier();
            _backup = new Backup();

            TrySyncOptionsFromOthers();
        }

        private void OnDisable()
        {
            CloseAllSceneGUI();
        }

        private void OnDestroy()
        {
            CloseAllSceneGUI();
            CancelDecimation();
        }


        public void AddPreservationVolume()
        {
            DecimationSettings settings = _options.DecimationSettings;

            Bounds volumeBounds = new Bounds(Vector3.zero, Vector3.one * 5);

            GatherSources(out _, out List<Renderer> renderers);

            if (renderers != null && renderers.Count > 0)
            {
                volumeBounds = MagicLODSourcesUtil.GetExtendedBounds(renderers);

                Vector3 center = transform.worldToLocalMatrix.MultiplyPoint(volumeBounds.center);
                Vector3 size = transform.worldToLocalMatrix.MultiplyVector(volumeBounds.size);

                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

                volumeBounds.center = center;
                volumeBounds.size = size;
            }

            PreservationVolume newVolume = new PreservationVolume(volumeBounds);

            if (settings.preservationVolumes == null)
            {
                settings.preservationVolumes = new PreservationVolume[] { newVolume };
            }
            else
            {
                ArrayUtility.Add(ref settings.preservationVolumes, newVolume);
            }

            _options.DecimationSettings = settings;
        }

        public void RemovePreservationVolume(int index)
        {
            DecimationSettings settings = _options.DecimationSettings;

            if (settings.preservationVolumes == null)
                return;

            if (index < 0 || index >= settings.preservationVolumes.Length)
                return;

            ArrayUtility.RemoveAt(ref settings.preservationVolumes, index);

            _options.DecimationSettings = settings;
        }

        public void ClearPreservationVolumes()
        {
            DecimationSettings settings = _options.DecimationSettings;

            settings.preservationVolumes = null;

            _options.DecimationSettings = settings;
        }

        public void StartPreservationsEditing()
        {
            try
            {
                if (IsDecimated || IsDecimationInProgress)
                {
                    Debug.Log("MagicLODEditorComponent::StartPreservationsEditing() can't start PreservationsEditing since object already decimated or in process");
                    return;
                }

                if (_options.DecimationSettings.preservationVolumes == null)
                {
                    Debug.Log("MagicLODEditorComponent::StartPreservationsEditing(). Can't start editing since preservationVolumes is null");
                    return;
                }

                CloseAllSceneGUI();

                _gatheredMeshes ??= new List<Mesh>();
                _gatheredRenderers ??= new List<Renderer>();

                _gatheredMeshes.Clear();
                _gatheredRenderers.Clear();

                GatherSources(out List<Mesh> meshes, out List<Renderer> renderers);

                _preservationsEdit.StartEditing(transform, renderers, meshes, _options);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EndPreservationsEditing();
            }
        }

        public void UpdatePreservationsEditing()
        {
            if (!_preservationsEdit.IsStarted)
                return;

            StartPreservationsEditing();
        }

        public void EndPreservationsEditing()
        {
            _preservationsEdit?.EndEditing();
        }


        public void StartPreview()
        {
            try
            {
                if (IsDecimated || IsDecimationInProgress)
                {
                    Debug.Log("MagicLODEditorComponent::StartPreview() can't start preview since object already decimated or in process");
                    return;
                }

                CloseAllSceneGUI();

                GatherSources(out List<Mesh> meshes, out List<Renderer> renderers);

                _decimation.Decimate(meshes, renderers, _options);

                _preview.StartPreview(_decimation);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EndPreview();
            }
        }

        public void UpdatePreview()
        {
            if (!_preview.IsStarted)
                return;

            StartPreview();
        }

        public void EndPreview()
        {
            _preview?.EndPreview();
        }


        public void Decimate()
        {
            DecimateThreaded();
            ForceCompleteDecimation();
        }

        public void DecimateThreaded()
        {
            try
            {
                if (IsDecimated)
                    Revert();

                CloseAllSceneGUI();

                if (!ValidateDecimation(out string errorText))
                {
                    Debug.Log(errorText);
                    return;
                }

                GatherSources(out List<Mesh> meshes, out List<Renderer> renderers);

                if (renderers.Count == 0)
                {
                    Debug.Log("MagicLODEditorComponent::can't find suitable sources. Try to check 'IncludeChildren' flag");
                    return;
                }

                if (_options.ExportMeshes)
                {
                    PersistentDecimationRequestsCache.BeginExportBatch();
                    _isPersistentExportBatchBegin = true;
                }

                _decimation.DecimateThreaded(meshes, renderers, _options, OnDecimationFinished);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                if (_isPersistentExportBatchBegin)
                {
                    PersistentDecimationRequestsCache.EndExportBatch();
                    _isPersistentExportBatchBegin = false;
                }

                Revert();
            }
        }

        public void ForceCompleteDecimation()
        {
            try
            {
                if (!IsDecimationInProgress)
                    return;

                _decimation.ForceCompleteAllRequests();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Revert();
            }
            finally
            {
                if (_isPersistentExportBatchBegin)
                {
                    PersistentDecimationRequestsCache.EndExportBatch();
                    _isPersistentExportBatchBegin = false;
                }
            }
        }

        public void CancelDecimation()
        {
            try
            {
                if (!IsDecimationInProgress)
                    return;

                _decimation.CancelAllRequests();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Revert();
            }
            finally
            {
                if (_isPersistentExportBatchBegin)
                {
                    PersistentDecimationRequestsCache.EndExportBatch();
                    _isPersistentExportBatchBegin = false;
                }
            }
        }


        public void ApplySimplifiedMesh()
        {
            try
            {
                if (!CanApplySimplifiedMesh)
                {
                    Debug.Log("MagicLODEditorComponent::ApplySimplifiedMesh() can't replace simplified mesh since no data found");
                    return;
                }

                CloseAllSceneGUI();

                _decimation.Reset();
                _backup.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Revert();
            }
        }

        public void Revert()
        {
            try
            {
                if (!_backup.IsCreated)
                {
                    Debug.Log("MagicLODEditorComponent::Revert() can't revert since backup not created");
                    return;
                }

                CloseAllSceneGUI();

                _backup.Revert();
                _backup.Clear();

                _decimation.Reset();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        public bool IsSameSyncGroup(MagicLODEditorComponent other)
        {
            if (_options.SyncMode != other.Options.SyncMode)
                return false;

            if (_options.SyncMode == SyncMode.NotSync)
            {
                return false;
            }
            else if (_options.SyncMode == SyncMode.WithSameGroupID)
            {
                return _options.SyncGroupID == other.Options.SyncGroupID;
            }
            else
            {
                if (!MagicLODSourcesUtil.TryGatherMesh(gameObject, out Mesh meshA, out _))
                    return false;

                if (!MagicLODSourcesUtil.TryGatherMesh(other.gameObject, out Mesh meshB, out _))
                    return false;

                return meshA == meshB;
            }
        }

        public void OnSyncOptionsChanged()
        {
            TrySyncOptionsFromOthers();
        }

        public void OnDecimationOptionsChanged()
        {
            UpdatePreview();
            UpdatePreservationsEditing();

            TrySyncOptionsToOthers();
        }

        public void OnExportOptionsChanged()
        {
            TrySyncOptionsToOthers();
        }

        public void OnInspectorClosed()
        {
            CloseAllSceneGUI();
        }

        private void OnDecimationFinished(bool cancelled)
        {
            try
            {
                CloseAllSceneGUI();

                if (cancelled)
                    return;

                _applier.Apply(gameObject, _decimation, _options, _backup);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Revert();
            }
            finally
            {
                if (_isPersistentExportBatchBegin)
                {
                    PersistentDecimationRequestsCache.EndExportBatch();
                    _isPersistentExportBatchBegin = false;
                }
            }
        }


        private bool ValidateDecimation(out string errorText)
        {
            if (_options.OperationMode == OperationMode.GenerateLODs)
            {
                if (TryGetComponent<LODGroup>(out _))
                {
                    errorText = "MagicLODEditorComponent::CheckBeforeDecimation() can't create LODGroup since LODGroup already exists";
                    return false;
                }
            }

            errorText = string.Empty;
            return true;
        }

        private void CloseAllSceneGUI()
        {
            EndPreview();
            EndPreservationsEditing();
        }

        private void GatherSources(out List<Mesh> meshes, out List<Renderer> renderers)
        {
            _gatheredMeshes ??= new List<Mesh>();
            _gatheredRenderers ??= new List<Renderer>();

            _gatheredMeshes.Clear();
            _gatheredRenderers.Clear();

            bool includeLODs = (_options.OperationMode == OperationMode.Simplify);

            MagicLODSourcesUtil.GatherSources(gameObject, _options.IncludeChildren, includeLODs, false, ref _gatheredMeshes, ref _gatheredRenderers);

            meshes = _gatheredMeshes;
            renderers = _gatheredRenderers;
        }

        private void TrySyncOptionsFromOthers()
        {
            if (_options.StopSyncing)
                return;

            if (_options.SyncMode == SyncMode.NotSync)
                return;

            foreach (var other in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (other == this)
                    continue;

                if (IsSameSyncGroup(other))
                {
                    _options.CopyFrom(other.Options);
                    break;
                }
            }
        }

        private void TrySyncOptionsToOthers()
        {
            if (_options.StopSyncing)
                return;

            if (_options.SyncMode == SyncMode.NotSync)
                return;

            foreach (var other in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (other == this)
                    continue;

                if (IsSameSyncGroup(other))
                    _options.CopyTo(other.Options);
            }
        }



        [Serializable]
        public class Option
        {
            private const string DEFAULT_EXPORT_FOLDER = "Assets/MagicLOD_Export/";

            public bool StopSyncing
            {
                get; set;
            }
            public SyncMode SyncMode
            {
                get
                {
                    return _syncMode;
                }
                set
                {
                    if (_syncMode.Equals(value))
                        return;

                    _syncMode = value;
                    _owner.OnSyncOptionsChanged();
                }
            }
            public int SyncGroupID
            {
                get
                {
                    return _syncGroupID;
                }
                set
                {
                    if (_syncGroupID == value)
                        return;

                    _syncGroupID = value;
                    _owner.OnSyncOptionsChanged();
                }
            }
            public OperationMode OperationMode
            {
                get
                {
                    return _operationMode;
                }
                set
                {
                    if (_operationMode == value)
                        return;

                    _operationMode = value;
                    _owner.OnDecimationOptionsChanged();
                }
            }
            public bool IncludeChildren
            {
                get
                {
                    return _includeChildren;
                }
                set
                {
                    if (_includeChildren == value)
                        return;

                    _includeChildren = value;
                    _owner.OnDecimationOptionsChanged();
                }
            }
            public DecimationSettings DecimationSettings
            {
                get
                {
                    return _decimationSettings;
                }
                set
                {
                    _decimationSettings = value;
                    _owner.OnDecimationOptionsChanged();
                }
            }
            public ILODGroupConfigurator LODGroupConfigurator
            {
                get
                {
                    return _lodConfigurator;
                }
                set
                {
                    _lodConfigurator = value;
                    _owner.OnDecimationOptionsChanged();
                }
            }
            public float MaxSimplificationError
            {
                get
                {
                    return _maxSimplificationError;
                }
                set
                {
                    if (_maxSimplificationError == value)
                        return;

                    _maxSimplificationError = value;
                    _owner.OnDecimationOptionsChanged();
                }
            }
            public bool ExportMeshes
            {
                get
                {
                    return _exportMeshes;
                }
                set
                {
                    if (_exportMeshes == value)
                        return;

                    _exportMeshes = value;
                    _owner.OnExportOptionsChanged();
                }
            }
            public string ExportFolder
            {
                get
                {
                    return _exportFolder;
                }
                set
                {
                    if (string.Equals(_exportFolder, value))
                        return;

                    _exportFolder = value;
                    _owner.OnExportOptionsChanged();
                }
            }

            [SerializeField]
            private SyncMode _syncMode;

            [SerializeField]
            private int _syncGroupID;

            [SerializeField]
            private OperationMode _operationMode;

            [SerializeField]
            private bool _includeChildren;

            [SerializeField]
            private DecimationSettings _decimationSettings;

            [SerializeReference]
            private ILODGroupConfigurator _lodConfigurator;

            [SerializeField]
            private float _maxSimplificationError;

            [SerializeField]
            private bool _exportMeshes;

            [SerializeField]
            private string _exportFolder;

            [SerializeField]
            private MagicLODEditorComponent _owner;


            public Option(MagicLODEditorComponent owner)
            {
                _syncMode = SyncMode.WithSameMesh;
                _operationMode = OperationMode.GenerateLODs;
                _includeChildren = true;
                _decimationSettings = DecimationSettings.Default;
                _lodConfigurator = new ScreenSpaceLODConfigurator();
                _maxSimplificationError = 0.0001f;
                _exportMeshes = true;
                _exportFolder = DEFAULT_EXPORT_FOLDER;
                _owner = owner;
            }

            public void SetOwner(MagicLODEditorComponent owner)
            {
                _owner = owner;
            }

            public void CopyTo(Option other)
            {
                other.CopyFrom(this);
            }

            public void CopyFrom(Option other)
            {
                _operationMode = other._operationMode;
                _includeChildren = other._includeChildren;
                _decimationSettings = other._decimationSettings;

                if (other._decimationSettings.preservationVolumes != null)
                    _decimationSettings.preservationVolumes = (PreservationVolume[])other._decimationSettings.preservationVolumes.Clone();

                _lodConfigurator = other._lodConfigurator.Clone();
                _maxSimplificationError = other._maxSimplificationError;
                _exportMeshes = other._exportMeshes;
                _exportFolder = other._exportFolder;
            }
        }

        [Serializable]
        public class Decimation
        {
            public LODGroupDescriptor UsedDescriptor
            {
                get
                {
                    return _usedDescriptor;
                }
            }
            public IReadOnlyList<Output> Outputs
            {
                get
                {
                    return _outputs;
                }
            }
            public int OriginalTrianglesCount
            {
                get
                {
                    return _originalTrianglesCount;
                }
            }
            public int SimplifiedTrianglesCount
            {
                get
                {
                    if (_decimatedTrianglesCount == null || _decimatedTrianglesCount.Length != 1)
                        return 0;

                    return _decimatedTrianglesCount[0];
                }
            }
            public int[] DecimatedTrianglesCount
            {
                get
                {
                    _decimatedTrianglesCount ??= new int[0];

                    return _decimatedTrianglesCount;
                }
            }
            public bool IsAllRequestsCompleted
            {
                get
                {
                    return _pendingRequests == null || _pendingRequests.Count == 0;
                }
            }
            public int TotalRequests
            {
                get
                {
                    return _totalRequests;
                }
            }
            public int SuccessRequests
            {
                get
                {
                    return _successRequests;
                }
            }
            public int FailedRequests
            {
                get
                {
                    return _failedRequests;
                }
            }
            public bool HasErrors
            {
                get
                {
                    return !string.IsNullOrEmpty(_errorsText);
                }
            }
            public string ErrorsText
            {
                get
                {
                    return _errorsText;
                }
            }
            public bool Cancelled
            {
                get
                {
                    return _cancelled;
                }
            }

            private LODGroupDescriptor _usedDescriptor;
            private List<DecimationRequest> _pendingRequests;
            private List<Output> _outputs;

            [SerializeField]
            private int _totalRequests;

            [SerializeField]
            private int _successRequests;

            [SerializeField]
            private int _failedRequests;

            [SerializeField]
            private int _originalTrianglesCount;

            [SerializeField]
            private int[] _decimatedTrianglesCount;

            [SerializeField]
            private string _errorsText;

            private bool _isDispatching;
            private bool _cancelled;


            public void Decimate(IReadOnlyList<Mesh> meshes, IReadOnlyList<Renderer> renderers, Option options)
            {
                DecimateThreaded(meshes, renderers, options);
                ForceCompleteAllRequests();
            }

            public void DecimateThreaded(IReadOnlyList<Mesh> meshes, IReadOnlyList<Renderer> renderers, Option options, Action<bool> onFinished = null)
            {
                if (!IsAllRequestsCompleted)
                    CancelAllRequests();

                Reset();

                if (meshes == null || renderers == null || meshes.Count == 0 || renderers.Count == 0)
                {
                    Debug.Log("Decimation::DecimateThreaded() can't decimate since renderers are null or empty");
                    return;
                }

                float[][] quality = null;
                float maxSimplificationError = options.MaxSimplificationError;
                DecimationSettings settings = options.DecimationSettings;

                if (options.OperationMode == OperationMode.GenerateLODs)
                {
                    _usedDescriptor = options.LODGroupConfigurator.CreateLODGroupDescriptor(meshes, renderers);
                    _decimatedTrianglesCount = new int[_usedDescriptor.transitionHeight.Length];

                    quality = _usedDescriptor.quality;
                }
                else
                {
                    _decimatedTrianglesCount = new int[1];
                }

                string exportFolder = options.ExportMeshes ? options.ExportFolder : PersistentDecimationRequestsCache.DEFAULT_FOLDER_PATH;

                _isDispatching = true;

                for (int i = 0; i < meshes.Count; i++)
                {
                    Mesh mesh = meshes[i];
                    Renderer renderer = renderers[i];

                    if (mesh == null || renderer == null)
                        continue;

                    DecimationRequest request = new DecimationRequest(mesh, quality?[i], maxSimplificationError, settings);

                    _pendingRequests.Add(request);
                    _totalRequests++;

                    if (PersistentDecimationRequestsCache.TryLoadDecimationResult(request, exportFolder, out DecimationResult result))
                    {
                        OnRequestCompleted(renderer, request, result, onFinished);
                        continue;
                    }

                    EditorMeshDecimationProvider.RequestDecimation(request, (result) =>
                    {
                        if (result.taskStatus == DecimationStatus.Success && options.ExportMeshes)
                            SaveRequestInPersistentCache(request, ref result, exportFolder);

                        OnRequestCompleted(renderer, request, result, onFinished);
                    });
                }

                _isDispatching = false;

                if (_pendingRequests.Count == 0)
                    onFinished?.Invoke(_cancelled);
            }

            public void ForceCompleteAllRequests()
            {
                if (_pendingRequests == null || _pendingRequests.Count == 0)
                    return;

                for (int i = 0; i < _pendingRequests.Count; i++)
                    EditorMeshDecimationProvider.CompleteRequest(_pendingRequests[i]);

                EditorMeshDecimationProvider.ForceTick();
            }

            public void CancelAllRequests()
            {
                if (_pendingRequests == null || _pendingRequests.Count == 0)
                    return;

                _cancelled = true;

                while (_pendingRequests.Count > 0)
                {
                    EditorMeshDecimationProvider.Cancel(_pendingRequests[0]);
                }
            }

            public void Reset()
            {
                _usedDescriptor = default;

                _pendingRequests ??= new List<DecimationRequest>();
                _pendingRequests.Clear();

                _outputs ??= new List<Output>();
                _outputs.Clear();

                _totalRequests = 0;
                _successRequests = 0;
                _failedRequests = 0;

                _originalTrianglesCount = 0;
                _decimatedTrianglesCount = new int[0];
                _errorsText = string.Empty;

                _cancelled = false;
            }


            private void SaveRequestInPersistentCache(DecimationRequest request, ref DecimationResult result, string folderPath)
            {
                PersistentDecimationRequestsCache.TryExportDecimationResult(request, ref result, folderPath);
            }

            private void OnRequestCompleted(Renderer renderer, DecimationRequest request, DecimationResult result, Action<bool> onFinished)
            {
                if (_pendingRequests == null || _pendingRequests.Count == 0)
                    return;

                _pendingRequests.Remove(request);

                Output output = new Output(request.originalMesh, renderer, result);
                _outputs.Add(output);

                UpdateStatistic(output);

                if (!_isDispatching && _pendingRequests.Count == 0)
                    onFinished?.Invoke(_cancelled);
            }

            private void UpdateStatistic(Output output)
            {
                DecimationResult result = output.decimationResult;

                if (result.taskStatus == DecimationStatus.Failed)
                {
                    if (!_errorsText.Contains(result.errorText))
                        _errorsText += $"\n{result.errorText}";

                    _failedRequests++;

                    return;
                }

                _successRequests++;

                Mesh[] decimatedMeshes = result.decimatedMeshes;

                if (_decimatedTrianglesCount.Length < decimatedMeshes.Length)
                {
                    Debug.Log("Decimation::UpdateStatistic() something went wrong. You shouldn't see that message");
                    return;
                }

                _originalTrianglesCount += (int)output.originalMesh.GetTotalTriangles();

                for (int i = 0; i < decimatedMeshes.Length; i++)
                    _decimatedTrianglesCount[i] += (int)decimatedMeshes[i].GetTotalTriangles();
            }


            public struct Output
            {
                public Mesh originalMesh;
                public Renderer originalRenderer;
                public DecimationResult decimationResult;

                public Output(Mesh originalMesh, Renderer originalRenderer, DecimationResult decimationResult)
                {
                    this.originalMesh = originalMesh;
                    this.originalRenderer = originalRenderer;
                    this.decimationResult = decimationResult;
                }
            }
        }

        [Serializable]
        public class Preview
        {
            public bool IsStarted
            {
                get
                {
                    return _isStarted;
                }
            }
            public int MaxLODs
            {
                get
                {
                    return _maxLODs;
                }
            }
            public int CurrentLOD
            {
                get
                {
                    return _currentLOD;
                }
                set
                {
                    _currentLOD = Mathf.Clamp(value, 0, _maxLODs);
                }
            }

            private bool _isStarted;
            private int _currentLOD;
            private int _maxLODs;
            private List<PreviewData> _previewData;


            public void StartPreview(Decimation decimation)
            {
                if (_isStarted)
                    EndPreview();

                if (!decimation.IsAllRequestsCompleted)
                    return;

                _isStarted = true;
                _currentLOD = 0;
                _maxLODs = 0;

                _previewData ??= new List<PreviewData>();
                _previewData.Clear();

                FillPreviewData(decimation.Outputs);

                foreach (var preview in _previewData)
                    preview.HideOriginal();

                AssemblyReloadEvents.beforeAssemblyReload -= EndPreview;
                AssemblyReloadEvents.beforeAssemblyReload += EndPreview;

                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;

                SceneView.RepaintAll();
            }

            public void EndPreview()
            {
                if (_previewData != null)
                {
                    foreach (var preview in _previewData)
                        preview.ShowOriginal();

                    _previewData.Clear();
                }

                AssemblyReloadEvents.beforeAssemblyReload -= EndPreview;

                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.RepaintAll();

                _isStarted = false;
            }


            private void FillPreviewData(IReadOnlyList<Decimation.Output> decimationOutput)
            {
                _previewData ??= new List<PreviewData>();
                _previewData.Clear();

                foreach (var output in decimationOutput)
                {
                    if (output.decimationResult.taskStatus == DecimationStatus.Failed)
                        continue;

                    if (output.originalRenderer == null)
                        continue;

                    Mesh[] decimatedMeshes = output.decimationResult.decimatedMeshes;

                    if (decimatedMeshes == null)
                        continue;

                    _previewData.Add(new PreviewData(output.originalRenderer, decimatedMeshes));
                    _maxLODs = Mathf.Max(_maxLODs, decimatedMeshes.Length - 1);
                }
            }

            private void OnSceneGUI(SceneView sceneView)
            {
                if (Event.current.type != EventType.Repaint)
                    return;

                foreach (var data in _previewData)
                {
                    if (SceneView.currentDrawingSceneView == null)
                        return;

                    Camera sceneCamera = SceneView.currentDrawingSceneView.camera;

                    if (sceneCamera == null)
                        return;

                    data.DrawLOD(sceneCamera, _currentLOD);
                }
            }


            private struct PreviewData
            {
                private Renderer _originalRenderer;
                private Material[] _sharedMaterials;
                private Mesh[] _decimatedMeshes;
                private bool _storedForceRenderingOff;

                public PreviewData(Renderer renderer, Mesh[] decimatedMeshes)
                {
                    _originalRenderer = renderer;
                    _sharedMaterials = renderer.sharedMaterials;
                    _decimatedMeshes = decimatedMeshes;
                    _storedForceRenderingOff = renderer.forceRenderingOff;
                }

                public void HideOriginal()
                {
                    if (_originalRenderer == null)
                        return;

                    _originalRenderer.forceRenderingOff = true;
                }

                public void ShowOriginal()
                {
                    if (_originalRenderer == null)
                        return;

                    _originalRenderer.forceRenderingOff = _storedForceRenderingOff;
                }

                public void DrawLOD(Camera camera, int lodIndex)
                {
                    if (_originalRenderer == null || _decimatedMeshes == null)
                        return;

                    if (lodIndex < 0 || lodIndex >= _decimatedMeshes.Length)
                        return;

                    Mesh lodMesh = _decimatedMeshes[lodIndex];

                    if (lodMesh == null)
                        return;

                    Matrix4x4 matrix = _originalRenderer.localToWorldMatrix;

                    int subMeshCount = Mathf.Min(lodMesh.subMeshCount, _sharedMaterials.Length);

                    for (int i = 0; i < subMeshCount; i++)
                    {
                        Material material = _sharedMaterials[i];

                        if (material != null)
                            Graphics.DrawMesh(lodMesh, matrix, material, _originalRenderer.gameObject.layer, camera, i);
                    }
                }
            }
        }

        [Serializable]
        public class PreservationsEdit
        {
            public bool IsStarted { get; private set; }

            private Transform _rootTransform;
            private Option _options;

            private List<MeshViewData> _meshViewsData;
            private BoxBoundsHandle _boundsHandle;

            private List<Vector3> _linesBuffer = new List<Vector3>(1024);
            private Vector3[][] _cachedVolumeLines;
            private Bounds[] _cachedVolumeBounds;


            public void StartEditing(Transform rootTransform, IReadOnlyList<Renderer> renderers, IReadOnlyList<Mesh> meshes, Option options)
            {
                if (!CheckInput(rootTransform, renderers, meshes, options, out string errorText))
                {
                    Debug.Log(errorText);
                    return;
                }

                if (IsStarted || _rootTransform != null || (_meshViewsData != null && _meshViewsData.Count > 0))
                    EndEditing();

                _rootTransform = rootTransform;
                _options = options;

                CreateViewData(renderers, meshes);

                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;

                SceneView.RepaintAll();

                IsStarted = true;
            }

            public void EndEditing()
            {
                if (_meshViewsData != null)
                {
                    foreach (var data in _meshViewsData)
                        data.RestoreOriginalRenderer();

                    _meshViewsData.Clear();
                }

                _rootTransform = null;
                _options = null;

                _linesBuffer?.Clear();
                _cachedVolumeLines = null;
                _cachedVolumeBounds = null;

                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.RepaintAll();

                IsStarted = false;
            }


            private void OnSceneGUI(SceneView sceneView)
            {
                if (_options == null || _options.DecimationSettings.preservationVolumes == null || _meshViewsData == null || _rootTransform == null)
                {
                    EndEditing();
                    return;
                }

                _boundsHandle ??= new BoxBoundsHandle();

                PreservationVolume[] volumes = _options.DecimationSettings.preservationVolumes;
                EnsureCacheSize(volumes.Length);

                Matrix4x4 originalHandlesMatrix = Handles.matrix;
                CompareFunction originalZTest = Handles.zTest;

                Handles.matrix = _rootTransform.localToWorldMatrix;

                for (int i = 0; i < volumes.Length; i++)
                {
                    PreservationVolume volume = volumes[i];
                    Bounds rootLocalBounds = volume.bounds;

                    _boundsHandle.center = rootLocalBounds.center;
                    _boundsHandle.size = rootLocalBounds.size;

                    Color volumeColor = GetVolumeColor(volume.penaltyWeight);

                    DrawVolumeFill(volumes[i].bounds, volumeColor);
                    DrawEdgesInVolume(volumes[i].bounds, i);

                    Handles.color = volumeColor;
                    Handles.zTest = CompareFunction.Always;

                    EditorGUI.BeginChangeCheck();

                    _boundsHandle.DrawHandle();

                    if (EditorGUI.EndChangeCheck())
                    {
                        volume.bounds = new Bounds(_boundsHandle.center, _boundsHandle.size);
                        volumes[i] = volume;
                    }
                }

                Handles.matrix = originalHandlesMatrix;
                Handles.zTest = originalZTest;

                if (Event.current.type == EventType.Repaint)
                {
                    if (SceneView.currentDrawingSceneView == null)
                        return;

                    Camera sceneCamera = SceneView.currentDrawingSceneView.camera;

                    if (sceneCamera == null)
                        return;

                    foreach (var data in _meshViewsData)
                    {
                        data.DrawMesh(sceneCamera);
                    }
                }
            }

            private void EnsureCacheSize(int size)
            {
                if (_cachedVolumeLines == null || _cachedVolumeLines.Length != size)
                {
                    _cachedVolumeLines = new Vector3[size][];
                    _cachedVolumeBounds = new Bounds[size];
                }
            }

            private void DrawVolumeFill(Bounds bounds, Color color)
            {
                CompareFunction prevZTest = Handles.zTest;
                Handles.zTest = CompareFunction.LessEqual;

                Color fillColor = color;
                fillColor.a = 0.12f;
                Handles.color = fillColor;

                Vector3 c = bounds.center;
                Vector3 e = bounds.extents;

                Vector3 p0 = c + new Vector3(-e.x, -e.y, -e.z);
                Vector3 p1 = c + new Vector3(-e.x, -e.y, e.z);
                Vector3 p2 = c + new Vector3(e.x, -e.y, e.z);
                Vector3 p3 = c + new Vector3(e.x, -e.y, -e.z);
                Vector3 p4 = c + new Vector3(-e.x, e.y, -e.z);
                Vector3 p5 = c + new Vector3(-e.x, e.y, e.z);
                Vector3 p6 = c + new Vector3(e.x, e.y, e.z);
                Vector3 p7 = c + new Vector3(e.x, e.y, -e.z);

                Handles.DrawAAConvexPolygon(p0, p1, p2, p3);
                Handles.DrawAAConvexPolygon(p4, p5, p6, p7);
                Handles.DrawAAConvexPolygon(p0, p1, p5, p4);
                Handles.DrawAAConvexPolygon(p2, p3, p7, p6);
                Handles.DrawAAConvexPolygon(p0, p3, p7, p4);
                Handles.DrawAAConvexPolygon(p1, p2, p6, p5);

                Handles.zTest = prevZTest;
            }

            private Color GetVolumeColor(PenaltyWeight weight)
            {
                switch (weight)
                {
                    case PenaltyWeight.Low: return new Color(0.3f, 0.8f, 0.4f, 1f);
                    case PenaltyWeight.Medium: return new Color(1f, 0.7f, 0.2f, 1f);
                    case PenaltyWeight.High: return new Color(0.9f, 0.3f, 0.3f, 1f);
                    case PenaltyWeight.None:
                    default: return new Color(0.9f, 0.9f, 0.9f, 1f);
                }
            }

            private void DrawEdgesInVolume(Bounds rootLocalBounds, int volumeIndex)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.zTest = CompareFunction.LessEqual;

                if (_cachedVolumeBounds[volumeIndex] != rootLocalBounds || _cachedVolumeLines[volumeIndex] == null)
                {
                    _cachedVolumeBounds[volumeIndex] = rootLocalBounds;
                    _linesBuffer.Clear();

                    for (int i = 0; i < _meshViewsData.Count; i++)
                    {
                        MeshViewData data = _meshViewsData[i];

                        if (data.rootLocalVertices == null || data.triangles == null || data.renderer == null)
                            continue;

                        if (!rootLocalBounds.Intersects(data.rootLocalMeshBounds))
                            continue;

                        int[] tris = data.triangles;
                        Vector3[] rootLocalVerts = data.rootLocalVertices;

                        int skipTriangles = 2;
                        for (int t = 0; t < tris.Length; t += 3 * skipTriangles)
                        {
                            if (t + 2 >= tris.Length)
                                break;

                            Vector3 p1 = rootLocalVerts[tris[t]];
                            Vector3 p2 = rootLocalVerts[tris[t + 1]];
                            Vector3 p3 = rootLocalVerts[tris[t + 2]];

                            bool p1In = rootLocalBounds.Contains(p1);
                            bool p2In = rootLocalBounds.Contains(p2);
                            bool p3In = rootLocalBounds.Contains(p3);

                            if (p1In || p2In)
                            {
                                _linesBuffer.Add(p1);
                                _linesBuffer.Add(p2);
                            }
                            if (p2In || p3In)
                            {
                                _linesBuffer.Add(p2);
                                _linesBuffer.Add(p3);
                            }
                            if (p3In || p1In)
                            {
                                _linesBuffer.Add(p3);
                                _linesBuffer.Add(p1);
                            }
                        }
                    }

                    _cachedVolumeLines[volumeIndex] = _linesBuffer.ToArray();
                }

                if (_cachedVolumeLines[volumeIndex].Length > 0)
                    Handles.DrawLines(_cachedVolumeLines[volumeIndex]);

                Handles.zTest = CompareFunction.Always;
            }


            private bool CheckInput(Transform rootTransform, IReadOnlyList<Renderer> renderers, IReadOnlyList<Mesh> meshes, Option options, out string errorText)
            {
                if (rootTransform == null)
                {
                    errorText = "PreservationVolumesEdit::StartEditing() Can't edit volumes since rootTransform is null";
                    return false;
                }

                if (meshes == null || renderers == null || options == null)
                {
                    errorText = "PreservationVolumesEdit::StartEditing() Can't edit volumes since meshes, renderers or options are null";
                    return false;
                }

                if (renderers.Count != meshes.Count)
                {
                    errorText = "PreservationVolumesEdit::StartEditing() Can't edit volumes since meshes and renderers count isn't equal";
                    return false;
                }

                if (options.DecimationSettings.preservationVolumes == null)
                {
                    errorText = "PreservationVolumesEdit::StartEditing() Can't edit volumes since preservationVolumes is null";
                    return false;
                }

                if (options.DecimationSettings.preservationVolumes.Length == 0)
                {
                    errorText = "PreservationVolumesEdit::StartEditing() Can't edit volumes since preservationVolumes is zero";
                    return false;
                }

                errorText = string.Empty;
                return true;
            }

            private void CreateViewData(IReadOnlyList<Renderer> renderers, IReadOnlyList<Mesh> meshes)
            {
                _meshViewsData ??= new List<MeshViewData>();
                _meshViewsData.Clear();

                Matrix4x4 worldToRootLocal = _rootTransform.worldToLocalMatrix;

                for (int i = 0; i < renderers.Count; i++)
                {
                    Mesh mesh = meshes[i];
                    Renderer renderer = renderers[i];

                    if (mesh == null || renderer == null)
                        continue;

                    _meshViewsData.Add(new MeshViewData(mesh, renderer, worldToRootLocal));
                }
            }


            private struct MeshViewData
            {
                public Mesh mesh;
                public Renderer renderer;
                public Material[] materials;

                public Matrix4x4 matrix;
                public bool storedForceRenderingOff;

                public int[] triangles;
                public Vector3[] rootLocalVertices;
                public Bounds rootLocalMeshBounds;


                public MeshViewData(Mesh mesh, Renderer renderer, Matrix4x4 worldToRootLocal)
                {
                    this.mesh = mesh;
                    this.renderer = renderer;

                    materials = renderer.sharedMaterials;
                    matrix = renderer.transform.localToWorldMatrix;
                    storedForceRenderingOff = renderer.forceRenderingOff;
                    triangles = mesh.triangles;

                    renderer.forceRenderingOff = true;

                    Vector3[] rawVertices = mesh.vertices;
                    rootLocalVertices = new Vector3[rawVertices.Length];

                    Matrix4x4 currentToRootLocal = worldToRootLocal * matrix;

                    if (rawVertices.Length > 0)
                    {
                        rootLocalVertices[0] = currentToRootLocal.MultiplyPoint3x4(rawVertices[0]);
                        rootLocalMeshBounds = new Bounds(rootLocalVertices[0], Vector3.zero);

                        for (int i = 1; i < rawVertices.Length; i++)
                        {
                            rootLocalVertices[i] = currentToRootLocal.MultiplyPoint3x4(rawVertices[i]);
                            rootLocalMeshBounds.Encapsulate(rootLocalVertices[i]);
                        }
                    }
                    else
                    {
                        rootLocalMeshBounds = new Bounds();
                    }
                }

                public void DrawMesh(Camera camera)
                {
                    if (mesh == null || materials == null)
                        return;

                    int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);

                    for (int i = 0; i < subMeshCount; i++)
                    {
                        Material mat = materials[i];

                        if (mat != null)
                            Graphics.DrawMesh(mesh, matrix, mat, 0, camera, i);
                    }
                }

                public void RestoreOriginalRenderer()
                {
                    if (renderer == null)
                        return;

                    renderer.forceRenderingOff = storedForceRenderingOff;
                }
            }
        }

        [Serializable]
        public class Applier
        {
            private const string ParentGoName = "[MagicLOD]";


            public void Apply(GameObject root, Decimation decimation, Option options, Backup backup)
            {
                if (!CheckInput(decimation, out string errorText))
                {
                    Debug.Log(errorText);
                    return;
                }

                if (backup.IsCreated)
                {
                    backup.Revert();
                    backup.Clear();
                }


                if (options.OperationMode == OperationMode.Simplify)
                {
                    ApplySimplifiedMesh(decimation, options, backup);
                }
                else if (options.OperationMode == OperationMode.GenerateLODs)
                {
                    ApplyLODs(root, decimation, options, backup);
                }
                else
                    throw new NotSupportedException("Not Supported Operation Mode");
            }

            private void ApplySimplifiedMesh(Decimation decimation, Option options, Backup backup)
            {
                IReadOnlyList<Decimation.Output> outputs = decimation.Outputs;

                for (int i = 0; i < outputs.Count; i++)
                {
                    Decimation.Output output = outputs[i];
                    DecimationResult result = output.decimationResult;

                    if (result.taskStatus == DecimationStatus.Failed)
                        continue;

                    if (result.decimatedMeshes == null || result.decimatedMeshes.Length == 0)
                        continue;

                    Mesh originalMesh = output.originalMesh;
                    Renderer originalRenderer = output.originalRenderer;

                    if (originalMesh == null || originalRenderer == null)
                        continue;

                    Mesh simplifiedMesh = result.decimatedMeshes[0];

                    if (simplifiedMesh == null)
                        continue;

                    if (MagicLODSourcesUtil.TrySetMesh(originalRenderer, simplifiedMesh))
                        backup.SaveRenderer(originalMesh, originalRenderer);
                }
            }

            private void ApplyLODs(GameObject root, Decimation decimation, Option options, Backup backup)
            {
                GameObject createdParent = CreateParentGO(root);
                backup.SetCreatedGOsParent(createdParent);

                LODGroupDescriptor descriptor = decimation.UsedDescriptor;
                LOD[] lods = CreateLODs(createdParent, decimation, backup);

                LODGroup lodGroup = CreateLODGroup(root, lods, descriptor);
                backup.SetCreatedLODGroup(lodGroup);
            }


            private bool CheckInput(Decimation decimation, out string errorText)
            {
                if (decimation.Outputs == null || decimation.Outputs.Count == 0)
                {
                    errorText = "Applier::CheckInput() decimation output is null or empty";
                    return false;
                }

                if (!decimation.IsAllRequestsCompleted)
                {
                    errorText = "Applier::CheckInput() decimation not completed";
                    return false;
                }

                if (decimation.SuccessRequests == 0)
                {
                    errorText = "Applier::CheckInput() decimation SuccessRequests is zero";
                    return false;
                }

                foreach (var output in decimation.Outputs)
                {
                    if (output.originalMesh == null || output.originalRenderer == null)
                    {
                        errorText = "originalMesh or originalRenderer is missing in output";
                        return false;
                    }
                }

                errorText = string.Empty;
                return true;
            }

            private GameObject CreateParentGO(GameObject root)
            {
                GameObject parent = new GameObject(ParentGoName);

                parent.transform.parent = root.transform;
                parent.transform.localPosition = Vector3.zero;
                parent.transform.localRotation = Quaternion.identity;
                parent.transform.localScale = Vector3.one;

                return parent;
            }

            private Renderer CreateDecimatedRenderer(GameObject parent, Renderer originalRenderer, Mesh decimatedMesh)
            {
                GameObject decimatedGO = new GameObject();

                decimatedGO.transform.position = originalRenderer.transform.position;
                decimatedGO.transform.rotation = originalRenderer.transform.rotation;
                decimatedGO.transform.localScale = originalRenderer.transform.lossyScale;

                decimatedGO.layer = originalRenderer.gameObject.layer;
                decimatedGO.isStatic = originalRenderer.gameObject.isStatic;

                decimatedGO.transform.SetParent(parent.transform, true);

                Renderer decimatedRenderer;

                if (originalRenderer is MeshRenderer)
                {
                    decimatedRenderer = decimatedGO.AddComponent<MeshRenderer>();
                    decimatedRenderer.CopySettings(originalRenderer);

                    decimatedGO.AddComponent<MeshFilter>().sharedMesh = decimatedMesh;
                }
                else if (originalRenderer is SkinnedMeshRenderer)
                {
                    decimatedRenderer = decimatedGO.AddComponent<SkinnedMeshRenderer>();
                    decimatedRenderer.CopySettings(originalRenderer);

                    (decimatedRenderer as SkinnedMeshRenderer).sharedMesh = decimatedMesh;
                }
                else
                    throw new NotSupportedException($"Applier::CreateDecimatedRenderer() {originalRenderer.GetType()} renderer type not supported, {originalRenderer.gameObject.name}");

                return decimatedRenderer;
            }

            private LOD[] CreateLODs(GameObject createdParent, Decimation decimation, Backup backup)
            {
                LODGroupDescriptor descriptor = decimation.UsedDescriptor;
                IReadOnlyList<Decimation.Output> outputs = decimation.Outputs;

                float[] transitionHeights = descriptor.transitionHeight;

                LOD[] lods = new LOD[transitionHeights.Length];
                Renderer[][] renderers = new Renderer[lods.Length][];

                for (int lodIdx = 0; lodIdx < lods.Length; lodIdx++)
                {
                    Renderer[] lodRenderers = new Renderer[decimation.SuccessRequests];

                    for (int meshIdx = 0, rendIdx = 0; meshIdx < outputs.Count; meshIdx++)
                    {
                        Decimation.Output output = outputs[meshIdx];
                        DecimationResult result = output.decimationResult;

                        if (result.taskStatus == DecimationStatus.Failed)
                            continue;
                        
                        Mesh lodMesh = result.decimatedMeshes[lodIdx];

                        if (lodMesh == output.originalMesh)
                        {
                            lodRenderers[rendIdx] = output.originalRenderer;
                        }
                        else if (lodIdx == 0)
                        {
                            if (!MagicLODSourcesUtil.TrySetMesh(output.originalRenderer, lodMesh))
                                throw new InvalidOperationException("Applier::CreateLODs: Unable to set LOD0 mesh");

                            backup.SaveRenderer(output.originalMesh, output.originalRenderer);
                            lodRenderers[rendIdx] = output.originalRenderer;
                        }
                        else if (lodIdx > 0 && lodMesh == result.decimatedMeshes[lodIdx - 1])
                        {
                            lodRenderers[rendIdx] = renderers[lodIdx - 1][rendIdx];
                        }
                        else
                        {
                            Renderer decimatedRenderer = CreateDecimatedRenderer(createdParent, output.originalRenderer, lodMesh);

                            decimatedRenderer.gameObject.name = $"{output.originalRenderer.name}_LOD{lodIdx}_{Mathf.RoundToInt(descriptor.quality[meshIdx][lodIdx] * 100)}%";

                            lodRenderers[rendIdx] = decimatedRenderer;
                        }

                        rendIdx++;
                    }

                    renderers[lodIdx] = lodRenderers;

                    LOD lod = new LOD
                    {
                        renderers = lodRenderers,
                        screenRelativeTransitionHeight = transitionHeights[lodIdx]
                    };

                    lods[lodIdx] = lod;
                }

                return lods;
            }

            private LODGroup CreateLODGroup(GameObject root, LOD[] lods, LODGroupDescriptor descriptor)
            {
                LODGroup lodGroup = root.AddComponent<LODGroup>();

                lodGroup.fadeMode = descriptor.fadeMode;
                lodGroup.SetLODs(lods);

                lodGroup.RecalculateBounds();

                return lodGroup;
            }
        }

        [Serializable]
        public class Backup
        {
            public bool IsCreated
            {
                get
                {
                    if (_originalRenderers != null && _originalRenderers.Count > 0)
                        return true;

                    if (_createdGOsParent != null)
                        return true;

                    if (_createdLODGroup != null)
                        return true;

                    return false;
                }
            }

            [SerializeField]
            private List<Mesh> _originalMeshes;

            [SerializeField]
            private List<Renderer> _originalRenderers;

            [SerializeField]
            private GameObject _createdGOsParent;

            [SerializeField]
            private LODGroup _createdLODGroup;


            public Backup()
            {
                _originalMeshes = new List<Mesh>();
                _originalRenderers = new List<Renderer>();

                _createdGOsParent = null;
                _createdLODGroup = null;
            }

            public void SaveRenderer(Mesh originalMesh, Renderer originalRenderer)
            {
                _originalMeshes.Add(originalMesh);
                _originalRenderers.Add(originalRenderer);
            }

            public void SetCreatedGOsParent(GameObject parentGO)
            {
                _createdGOsParent = parentGO;
            }

            public void SetCreatedLODGroup(LODGroup lodGroup)
            {
                _createdLODGroup = lodGroup;
            }

            public void Revert()
            {
                if (_originalRenderers.Count == _originalMeshes.Count)
                {
                    for (int i = 0; i < _originalRenderers.Count; i++)
                    {
                        Mesh originalMesh = _originalMeshes[i];

                        if (originalMesh == null)
                            continue;

                        Renderer originalRenderer = _originalRenderers[i];

                        if (originalRenderer == null)
                            continue;

                        MagicLODSourcesUtil.TrySetMesh(originalRenderer, originalMesh);
                    }
                }
                else
                {
                    Debug.Log("Backup::Revert() strange case. Renderers and Meshes count should be equals");
                }

                if (_createdGOsParent != null)
                    DestroyImmediate(_createdGOsParent);

                if (_createdLODGroup != null)
                    DestroyImmediate(_createdLODGroup);
            }

            public void Clear()
            {
                _originalMeshes.Clear();
                _originalRenderers.Clear();

                _createdGOsParent = null;
                _createdLODGroup = null;
            }
        }

    }
}

#endif
