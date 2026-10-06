using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    [Serializable]
    public class ScreenSpaceLODConfigurator : ILODGroupConfigurator
    {
        private const float BaseResolution = 1080f;
        private const float MaxFirstTransition = 0.8f;
        private const int AbsoluteMinReduction = 500;

        private const float MinTargetTrianglesPer100Pixels = 0.1f;
        private const float MinMeshQualityValue = 0.01f;
        private const float MaxMeshQualityValue = 0.99f;
        private const float MinReductionPercentageValue = 0.01f;
        private const float MaxReductionPercentageValue = 0.99f;
        private const int MinLODCount = 1;
        private const int MaxLODCount = 7;
        private const float MinCullHeight = 0.01f;
        private const float MaxCullHeight = 0.99f;
        private const float MinNeverCullMaxSize = 0.01f;
        private const float MinDimension = 0.001f;
        private const float MinLODSpreadEnd = 0.01f;


        public LODFadeMode FadeMode
        {
            get
            {
                return _fadeMode;
            }
            set
            {
                _fadeMode = value;
            }
        }
        public float TargetTrianglesPer100Pixels
        {
            get
            {
                return _targetTrianglesPer100Pixels;
            }
            set
            {
                _targetTrianglesPer100Pixels = Mathf.Max(value, MinTargetTrianglesPer100Pixels);
            }
        }
        public float MinMeshQuality
        {
            get
            {
                return _minMeshQuality;
            }
            set
            {
                _minMeshQuality = Mathf.Clamp(value, MinMeshQualityValue, MaxMeshQualityValue);
            }
        }
        public float MinReductionPercentage
        {
            get
            {
                return _minReductionPercentage;
            }
            set
            {
                _minReductionPercentage = Mathf.Clamp(
                    value,
                    MinReductionPercentageValue,
                    MaxReductionPercentageValue);
            }
        }
        public int MaxLODs
        {
            get
            {
                return _maxLODs;
            }
            set
            {
                _maxLODs = Mathf.Clamp(value, MinLODCount, MaxLODCount);
            }
        }

        public float CullHeight
        {
            get
            {
                return _cullHeight;
            }
            set
            {
                _cullHeight = Mathf.Clamp(value, MinCullHeight, MaxCullHeight);
            }
        }
        public float NeverCullMaxSize
        {
            get
            {
                return _neverCullMaxSize;
            }
            set
            {
                _neverCullMaxSize = Mathf.Max(value, MinNeverCullMaxSize);
            }
        }

        [SerializeField]
        private LODFadeMode _fadeMode;

        [SerializeField]
        private float _targetTrianglesPer100Pixels = 3f;

        [SerializeField]
        private float _minMeshQuality = 0.1f;

        [SerializeField]
        private float _minReductionPercentage = 0.25f;

        [SerializeField]
        private int _maxLODs = 4;

        [SerializeField]
        private float _cullHeight = 0.03f;

        [SerializeField]
        private float _neverCullMaxSize = 30f;

        [NonSerialized]
        private int[] _meshTrianglesBuffer;

        [NonSerialized]
        private float[] _meshMaxDimensionsBuffer;

        [NonSerialized]
        private float[] _transitionBuffer;

        [NonSerialized]
        private float[][] _qualityBuffer;


        public LODGroupDescriptor CreateLODGroupDescriptor(IReadOnlyList<Mesh> meshes, IReadOnlyList<Renderer> renderers)
        {
            if (meshes == null ||
                renderers == null ||
                meshes.Count == 0 ||
                meshes.Count != renderers.Count)
            {
                Debug.Log("ScreenSpaceLODConfigurator::CreateLODGroupDescriptor() invalid input");
                return default;
            }

            int meshCount = meshes.Count;
            int maxLODCount = Mathf.Clamp(_maxLODs, MinLODCount, MaxLODCount);

            EnsureBufferCapacity(meshCount, maxLODCount);

            if (!TryCacheSourceData(meshes, renderers, meshCount, out Bounds totalBounds, out int totalOriginalTriangles))
            {
                Debug.Log("ScreenSpaceLODConfigurator::CreateLODGroupDescriptor() mesh or renderer is null");
                return default;
            }

            if (totalOriginalTriangles == 0)
                return default;

            float maxDimension = GetMaxDimension(totalBounds);
            maxDimension = Mathf.Max(maxDimension, MinDimension);

            float targetTrianglesPer100Pixels = Mathf.Max(_targetTrianglesPer100Pixels, MinTargetTrianglesPer100Pixels);
            float targetDensity = targetTrianglesPer100Pixels / 100f;

            float neverCullMaxSize = Mathf.Max(_neverCullMaxSize, MinNeverCullMaxSize);
            float cullHeight = Mathf.Clamp(_cullHeight, MinCullHeight, MaxCullHeight);
            float cullFactor = 1f - Mathf.Clamp01(maxDimension / neverCullMaxSize);
            float actualCullDistance = cullHeight * cullFactor;
            float lodSpreadEnd = Mathf.Max(actualCullDistance, MinLODSpreadEnd);

            float firstTransition = CalculateFirstTransition(totalOriginalTriangles, targetDensity);
            int actualLODCount = 1;

            _transitionBuffer[0] = firstTransition;

            float[] lod0Qualities = _qualityBuffer[0];

            for (int i = 0; i < meshCount; i++)
                lod0Qualities[i] = 1f;

            float autoStepMultiplier = 0.5f;

            if (maxLODCount > 1)
                autoStepMultiplier = Mathf.Pow(lodSpreadEnd / firstTransition, 1f / (maxLODCount - 1));

            float minMeshQuality = Mathf.Clamp(
                _minMeshQuality,
                MinMeshQualityValue,
                MaxMeshQualityValue);
            float minReductionPercentage = Mathf.Clamp(
                _minReductionPercentage,
                MinReductionPercentageValue,
                MaxReductionPercentageValue);

            float currentTransition = firstTransition;
            int previousLODTriangles = totalOriginalTriangles;

            for (int level = 1; level < maxLODCount; level++)
            {
                float evaluationHeight = currentTransition;
                currentTransition *= autoStepMultiplier;

                if (currentTransition < lodSpreadEnd || level == maxLODCount - 1)
                    currentTransition = lodSpreadEnd;

                float[] qualities = _qualityBuffer[actualLODCount];
                int currentLODTriangles = CalculateQualities(
                    meshCount,
                    evaluationHeight,
                    targetDensity,
                    maxDimension,
                    minMeshQuality,
                    qualities);

                int requiredDrop = Mathf.RoundToInt(previousLODTriangles * minReductionPercentage);
                requiredDrop = Mathf.Max(requiredDrop, AbsoluteMinReduction);

                if (previousLODTriangles - currentLODTriangles < requiredDrop)
                    continue;

                previousLODTriangles = currentLODTriangles;
                _transitionBuffer[actualLODCount] = currentTransition;
                actualLODCount++;

                if (currentTransition <= lodSpreadEnd)
                    break;
            }

            _transitionBuffer[actualLODCount - 1] = actualCullDistance;

            return CreateDescriptor(meshCount, actualLODCount);
        }

        public ILODGroupConfigurator Clone()
        {
            return new ScreenSpaceLODConfigurator
            {
                _fadeMode = _fadeMode,
                _targetTrianglesPer100Pixels = _targetTrianglesPer100Pixels,
                _minMeshQuality = _minMeshQuality,
                _minReductionPercentage = _minReductionPercentage,
                _maxLODs = _maxLODs,
                _cullHeight = _cullHeight,
                _neverCullMaxSize = _neverCullMaxSize
            };
        }


        private void EnsureBufferCapacity(int meshCount, int maxLODCount)
        {
            int meshCapacity = Mathf.NextPowerOfTwo(meshCount);

            if (_meshTrianglesBuffer == null || _meshTrianglesBuffer.Length < meshCount)
                _meshTrianglesBuffer = new int[meshCapacity];

            if (_meshMaxDimensionsBuffer == null || _meshMaxDimensionsBuffer.Length < meshCount)
                _meshMaxDimensionsBuffer = new float[meshCapacity];

            if (_transitionBuffer == null || _transitionBuffer.Length < maxLODCount)
                _transitionBuffer = new float[maxLODCount];

            if (_qualityBuffer == null || _qualityBuffer.Length < maxLODCount)
            {
                float[][] qualityBuffer = new float[maxLODCount][];

                if (_qualityBuffer != null)
                    Array.Copy(_qualityBuffer, qualityBuffer, _qualityBuffer.Length);

                _qualityBuffer = qualityBuffer;
            }

            for (int i = 0; i < maxLODCount; i++)
            {
                float[] qualities = _qualityBuffer[i];

                if (qualities == null || qualities.Length < meshCount)
                    _qualityBuffer[i] = new float[meshCapacity];
            }
        }

        private bool TryCacheSourceData(
            IReadOnlyList<Mesh> meshes,
            IReadOnlyList<Renderer> renderers,
            int meshCount,
            out Bounds totalBounds,
            out int totalOriginalTriangles)
        {
            totalBounds = default;
            totalOriginalTriangles = 0;

            for (int i = 0; i < meshCount; i++)
            {
                Mesh mesh = meshes[i];
                Renderer renderer = renderers[i];

                if (mesh == null || renderer == null)
                    return false;

                int triangles = (int)mesh.GetTotalTriangles();
                Bounds bounds = renderer.bounds;

                _meshTrianglesBuffer[i] = triangles;
                _meshMaxDimensionsBuffer[i] = GetMaxDimension(bounds);
                totalOriginalTriangles += triangles;

                if (i == 0)
                    totalBounds = bounds;
                else
                    totalBounds.Encapsulate(bounds);
            }

            return true;
        }

        private int CalculateQualities(
            int meshCount,
            float evaluationHeight,
            float targetDensity,
            float maxDimension,
            float minMeshQuality,
            float[] qualities)
        {
            int totalTriangles = 0;
            float screenHeight = evaluationHeight * BaseResolution;

            for (int i = 0; i < meshCount; i++)
            {
                int originalTriangles = _meshTrianglesBuffer[i];

                if (originalTriangles == 0)
                {
                    qualities[i] = 1f;
                    continue;
                }

                float relativeSize = _meshMaxDimensionsBuffer[i] / maxDimension;
                float childScreenHeight = screenHeight * relativeSize;
                float targetTriangles = childScreenHeight * childScreenHeight * targetDensity;
                float quality = Mathf.Clamp(targetTriangles / originalTriangles, minMeshQuality, 1f);

                qualities[i] = quality;
                totalTriangles += Mathf.RoundToInt(originalTriangles * quality);
            }

            return totalTriangles;
        }

        private LODGroupDescriptor CreateDescriptor(int meshCount, int lodCount)
        {
            float[] transitions = new float[lodCount];
            float[][] qualities = new float[meshCount][];

            Array.Copy(_transitionBuffer, transitions, lodCount);

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                float[] meshQualities = new float[lodCount];

                for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
                    meshQualities[lodIndex] = _qualityBuffer[lodIndex][meshIndex];

                qualities[meshIndex] = meshQualities;
            }

            return new LODGroupDescriptor
            {
                fadeMode = _fadeMode,
                transitionHeight = transitions,
                quality = qualities
            };
        }

        private static float CalculateFirstTransition(int totalOriginalTriangles, float targetDensity)
        {
            float firstTransition = Mathf.Sqrt(
                totalOriginalTriangles /
                (BaseResolution * BaseResolution * targetDensity));

            return Mathf.Clamp(firstTransition, 0.2f, MaxFirstTransition);
        }

        private static float GetMaxDimension(Bounds bounds)
        {
            Vector3 size = bounds.size;

            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }
}
