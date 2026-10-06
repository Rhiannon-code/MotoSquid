using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime.Components
{
    public class MagicLODRuntimeComponent : MonoBehaviour
    {
        public const string PARENT_GO_NAME = "[MagicLOD]";

        public int ControllerID
        {
            get
            {
                return _controllerID;
            }
            set
            {
                if (!ValidateComponentNotDecimated())
                    return;

                _controllerID = value;
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
                if (!ValidateComponentNotDecimated())
                    return;

                _includeChildren = value;
            }
        }
        public bool DecimateAtStart
        {
            get
            {
                return _decimateAtStart;
            }
            set
            {
                if (!ValidateComponentNotDecimated())
                    return;

                _decimateAtStart = value;
            }
        }
        public bool IsDecimated
        {
            get; private set;
        }
        public bool IsDecimationInProgress
        {
            get; private set;
        }
        public int OriginalTrianglesCount
        {
            get
            {
                return _originalTrianglesCount;
            }
        }
        public int[] DecimatedTrianglesCount
        {
            get
            {
                return _decimatedTrianglesCount;
            }
        }
        public bool HasErrors
        {
            get
            {
                return !string.IsNullOrEmpty(ErrorsText);
            }
        }
        public string ErrorsText
        {
            get; private set;
        }

        [SerializeField]
        private int _controllerID;

        [SerializeField]
        private bool _includeChildren;

        [SerializeField]
        private bool _decimateAtStart;

        private List<Mesh> _meshes;
        private List<Renderer> _renderers;
        private OperationMode _operationMode;
        private LODGroupDescriptor _usedLODsDescriptor;
        private DecimationOutput[] _decimationOutputs;

        private int _originalTrianglesCount;
        private int[] _decimatedTrianglesCount;

        private int _completedRequests;


        private void Reset()
        {
            _controllerID = 0;
            _includeChildren = true;
            _decimateAtStart = true;
        }

        private void Start()
        {
            if (_decimateAtStart)
                Decimate();
        }


        public void Decimate()
        {
            try
            {
                if (!ValidateComponentNotDecimated())
                    return;

                MagicLODRuntimeController controller = MagicLODRuntimeController.GetInstance(_controllerID);

                if (controller == null)
                {
                    Debug.Log($"MagicLODRuntimeComponent::Controller({_controllerID}) not found");
                    return;
                }

                _meshes = new List<Mesh>();
                _renderers = new List<Renderer>();

                bool includeLODs = controller.OperationMode == OperationMode.Simplify;
                MagicLODSourcesUtil.GatherSources(gameObject, _includeChildren, includeLODs, true, ref _meshes, ref _renderers);

                if (!ValidateGatheredSources())
                {
                    ErrorsText += "Sources Are Empty Or Not Valid. Try enable 'includeChildren' and check 'Read/Write' meshes flag";
                    return;
                }

                if (!TryRequestDecimation(controller))
                {
                    ErrorsText += "Unable to Request Decimation";
                    return;
                }

                IsDecimationInProgress = true;
            }
            catch (Exception ex)
            {
                ErrorsText += $"{ex.Message}\n{ex.StackTrace}\n";

                IsDecimated = false;
                IsDecimationInProgress = false;

                CleanupAfterDecimation();
            }
        }


        private bool ValidateComponentNotDecimated()
        {
            if (IsDecimated || IsDecimationInProgress || HasErrors)
                return false;

            return true;
        }

        private bool ValidateGatheredSources()
        {
            if (_meshes == null || _renderers == null || _meshes.Count == 0 || _renderers.Count == 0 || _meshes.Count != _renderers.Count)
                return false;

            for (int i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] == null)
                    return false;

                if (_renderers[i] == null)
                    return false;
            }

            return true;
        }

        private bool TryRequestDecimation(MagicLODRuntimeController controller)
        {
            float[][] quality = null;
            float maxSimplificationError = controller.MaxSimplificationError;
            DecimationSettings settings = controller.DecimationSettings;

            int lodsCount = 1;

            if (controller.OperationMode == OperationMode.GenerateLODs)
            {
                ILODGroupConfigurator lodsConfugurator = controller.LODGroupConfigurator;

                if (lodsConfugurator == null)
                    return false;
                
                _usedLODsDescriptor = lodsConfugurator.CreateLODGroupDescriptor(_meshes, _renderers);

                quality = _usedLODsDescriptor.quality;
                lodsCount = _usedLODsDescriptor.transitionHeight.Length;
            }

            _originalTrianglesCount = 0;
            _decimatedTrianglesCount = new int[lodsCount];

            _operationMode = controller.OperationMode;
            _decimationOutputs = new DecimationOutput[_meshes.Count];

            _completedRequests = 0;

            for (int i = 0; i < _meshes.Count; i++)
            {
                Mesh mesh = _meshes[i];
                Renderer renderer = _renderers[i];
                
                DecimationRequest request = new DecimationRequest(mesh, quality?[i], maxSimplificationError, settings);

                int outputIndex = i;
                controller.RequestDecimation(this, request, (result) =>
                {
                    OnRequestCompleted(outputIndex, request, result);
                });
            }

            return true;
        }

        private void OnRequestCompleted(int outputIndex, DecimationRequest request, DecimationResult result)
        {
            if (this == null)
                return;

            if (!IsDecimationInProgress)
                return;

            try
            {
                if (result.taskStatus == DecimationStatus.Failed)
                {
                    ErrorsText += $"{result.errorText}\n";
                    return;
                }

                DecimationOutput output = new DecimationOutput(request.originalMesh, _renderers[outputIndex], result);
                _decimationOutputs[outputIndex] = output;

                if (!ValidateDecimationOutput(output))
                {
                    ErrorsText += $"DecimationOutput not valid\n";
                    return;
                }
                
                UpdateStatistic(output);
            }
            catch (Exception ex)
            {
                ErrorsText += $"{ex.Message}\n{ex.StackTrace}\n";
            }
            finally
            {
                _completedRequests++;

                if (_completedRequests == _decimationOutputs.Length)
                    OnDecimationFinished();
            }
        }

        private bool ValidateDecimationOutput(DecimationOutput output)
        {
            if (output.originalMesh == null || output.originalRenderer == null)
                return false;

            Mesh[] decimatedMeshes = output.decimationResult.decimatedMeshes;

            if (decimatedMeshes == null)
                return false;

            for (int i = 0; i < decimatedMeshes.Length; i++)
            {
                if (decimatedMeshes[i] == null)
                    return false;
            }

            return true;
        }

        private void UpdateStatistic(DecimationOutput output)
        {
            Mesh[] decimatedMeshes = output.decimationResult.decimatedMeshes;

            for (int i = 0; i < decimatedMeshes.Length; i++)
                _decimatedTrianglesCount[i] += (int)decimatedMeshes[i].GetTotalTriangles();

            _originalTrianglesCount += (int)output.originalMesh.GetTotalTriangles();
        }

        private void OnDecimationFinished()
        {
            IsDecimationInProgress = false;

            if (!HasErrors)
            {
                try
                {
                    if (ValidateBeforeApply())
                    {
                        ApplyDecimationOutput();
                        IsDecimated = true;
                    }
                    else
                    {
                        ErrorsText += $"Can't Validate Before Apply";
                    }
                }
                catch (Exception ex)
                {
                    ErrorsText += $"{ex.Message}\n{ex.StackTrace}\n";
                    IsDecimated = false;
                }
            }

            CleanupAfterDecimation();
        }

        private bool ValidateBeforeApply()
        {
            if (_meshes == null || _renderers == null || _meshes.Count == 0 || _renderers.Count == 0)
                return false;

            if (_decimationOutputs == null || _decimationOutputs.Length == 0)
                return false;

            for (int i = 0; i < _decimationOutputs.Length; i++)
            {
                if (!ValidateDecimationOutput(_decimationOutputs[i]))
                    return false;
            }    

            return true;
        }

        private void ApplyDecimationOutput()
        {
            if (_operationMode == OperationMode.Simplify)
            {
                ApplySimplifiedMesh();
            }
            else
            {
                ApplyLODs();
            }
        }

        private void ApplySimplifiedMesh()
        {
            for (int i = 0; i < _decimationOutputs.Length; i++)
            {
                DecimationOutput output = _decimationOutputs[i];
                DecimationResult result = output.decimationResult;

                Mesh originalMesh = output.originalMesh;
                Renderer originalRenderer = output.originalRenderer;

                if (originalMesh == null || originalRenderer == null)
                    continue;

                Mesh simplifiedMesh = result.decimatedMeshes[0];

                if (simplifiedMesh == null)
                    continue;

                MagicLODSourcesUtil.TrySetMesh(originalRenderer, simplifiedMesh);
            }
        }

        private void ApplyLODs()
        {
            GameObject createdParent = CreateParentGO();

            LODGroupDescriptor descriptor = _usedLODsDescriptor;
            LOD[] lods = CreateLODs(createdParent);

            CreateLODGroup(lods, descriptor);
        }

        private void CleanupAfterDecimation()
        {
            _meshes = null;
            _renderers = null;
            _decimationOutputs = null;
            _completedRequests = 0;
        }


        private GameObject CreateParentGO()
        {
            GameObject parent = new GameObject(PARENT_GO_NAME);

            parent.transform.parent = gameObject.transform;
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
                throw new NotSupportedException($"MagicLODRuntimeComponent::CreateDecimatedRenderer() {originalRenderer.GetType()} renderer type not supported, {originalRenderer.gameObject.name}");

            return decimatedRenderer;
        }

        private LOD[] CreateLODs(GameObject createdParent)
        {
            LODGroupDescriptor descriptor = _usedLODsDescriptor;

            float[] transitionHeights = descriptor.transitionHeight;

            LOD[] lods = new LOD[transitionHeights.Length];
            Renderer[][] renderers = new Renderer[lods.Length][];

            for (int lodIdx = 0; lodIdx < lods.Length; lodIdx++)
            {
                Renderer[] lodRenderers = new Renderer[_completedRequests];

                for (int meshIdx = 0, rendIdx = 0; meshIdx < _decimationOutputs.Length; meshIdx++)
                {
                    DecimationOutput output = _decimationOutputs[meshIdx];
                    DecimationResult result = output.decimationResult;

                    Mesh lodMesh = result.decimatedMeshes[lodIdx];

                    if (lodMesh == output.originalMesh)
                    {
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

        private void CreateLODGroup( LOD[] lods, LODGroupDescriptor descriptor)
        {
            LODGroup lodGroup = gameObject.AddComponent<LODGroup>();

            lodGroup.fadeMode = descriptor.fadeMode;
            lodGroup.SetLODs(lods);

            lodGroup.RecalculateBounds();
        }


        private struct DecimationOutput
        {
            public Mesh originalMesh;
            public Renderer originalRenderer;
            public DecimationResult decimationResult;

            public DecimationOutput(Mesh originalMesh, Renderer originalRenderer, DecimationResult decimationResult)
            {
                this.originalMesh = originalMesh;
                this.originalRenderer = originalRenderer;
                this.decimationResult = decimationResult;
            }
        }
    }
}