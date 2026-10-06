using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;


namespace NGS.MagicLOD.Runtime
{
    public struct AttributesProcessor
    {

        public AttributeInterpolationMode uvMode;
        public VertexAttributeInterpolator2D uvInterpolator;
        public bool interpolateUV;

        public AttributeInterpolationMode2 uv2Mode;
        public VertexAttributeInterpolator2D uv2Interpolator;
        public bool interpolateUV2;

        public AttributeInterpolationMode uv3Mode;
        public VertexAttributeInterpolator2D uv3Interpolator;
        public bool interpolateUV3;

        public AttributeInterpolationMode uv4Mode;
        public VertexAttributeInterpolator2D uv4Interpolator;
        public bool interpolateUV4;

        public AttributeInterpolationMode1 normalsMode;
        public VertexAttributeInterpolator3D normalsInterpolator;
        public bool interpolateNormals;

        public AttributeInterpolationMode1 tangentsMode;
        public VertexAttributeInterpolator4D tangentsInterpolator;
        public bool interpolateTangents;

        public AttributeInterpolationMode colorsMode;
        public VertexAttributeInterpolator4D colorsInterpolator;
        public bool interpolateColors;

        public BoneWeightInterpolationMode boneWeightsMode;
        public VertexAttributeInterpolatorBoneWeight boneWeightsInterpolator;
        public bool interpolateBoneWeights;


        public void InitializeAttributes(Mesh mesh, Mesh.MeshData meshData, DecimationSettings settings)
        {
            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord0))
                uvMode = settings.uvMode;

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord1))
            {
                uv2Mode = settings.uv2Mode;

#if !UNITY_EDITOR

                if (uv2Mode == AttributeInterpolationMode2.Recalculate_EditorOnly)
                {
                    uv2Mode = AttributeInterpolationMode2.Quadric;
                    Debug.Log("MagicLOD::AttributesProcessor 'uv2InterpolationMode.Recalculate' available only in Editor");
                }
                
#endif
            }

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord2))
                uv3Mode = settings.uv3Mode;

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord3))
                uv4Mode = settings.uv4Mode;

            if (meshData.HasVertexAttribute(VertexAttribute.Normal))
                normalsMode = settings.normalsMode;

            if (meshData.HasVertexAttribute(VertexAttribute.Tangent))
                tangentsMode = settings.tangentsMode;

            if (meshData.HasVertexAttribute(VertexAttribute.Color))
                colorsMode = settings.colorsMode;

            BoneWeight[] boneWeights = null;

            if (settings.boneWeightsInterpolationMode != BoneWeightInterpolationMode.None)
            {
                boneWeights = mesh.boneWeights;

                if (boneWeights != null && boneWeights.Length > 0)
                    boneWeightsMode = settings.boneWeightsInterpolationMode;
            }

            interpolateUV = (uvMode != AttributeInterpolationMode.None);
            interpolateUV2 = (uv2Mode != AttributeInterpolationMode2.None && uv2Mode != AttributeInterpolationMode2.Recalculate_EditorOnly);
            interpolateUV3 = (uv3Mode != AttributeInterpolationMode.None);
            interpolateUV4 = (uv4Mode != AttributeInterpolationMode.None);
            interpolateNormals = (normalsMode != AttributeInterpolationMode1.None && normalsMode != AttributeInterpolationMode1.Recalculate);
            interpolateTangents = (tangentsMode != AttributeInterpolationMode1.None && tangentsMode != AttributeInterpolationMode1.Recalculate);
            interpolateColors = (colorsMode != AttributeInterpolationMode.None);
            interpolateBoneWeights = (boneWeightsMode != BoneWeightInterpolationMode.None);

            CreateUVInterpolator(meshData);
            CreateUV2Interpolator(meshData);
            CreateUV3Interpolator(meshData);
            CreateUV4Interpolator(meshData);
            CreateNormalsInterpolator(meshData);
            CreateTangentsInterpolator(meshData);
            CreateColorsInterpolator(meshData);
            CreateBoneWeightInterpolator(boneWeights);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            if (interpolateUV)
                uvInterpolator.Initialize(ref internalData);

            if (interpolateUV2)
                uv2Interpolator.Initialize(ref internalData);

            if (interpolateUV3)
                uv3Interpolator.Initialize(ref internalData);

            if (interpolateUV4)
                uv4Interpolator.Initialize(ref internalData);

            if (interpolateNormals)
                normalsInterpolator.Initialize(ref internalData);

            if (interpolateTangents)
                tangentsInterpolator.Initialize(ref internalData);

            if (interpolateColors)
                colorsInterpolator.Initialize(ref internalData);

            if (interpolateBoneWeights)
                boneWeightsInterpolator.Initialize(ref internalData);
        }

        public void OnCollapseEdge(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            if (interpolateUV)
                uvInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateUV2)
                uv2Interpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateUV3)
                uv3Interpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateUV4)
                uv4Interpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateNormals)
                normalsInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateTangents)
                tangentsInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateColors)
                colorsInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            if (interpolateBoneWeights)
                boneWeightsInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);
        }

        public void OutputInterpolatedData(ref MeshDecimator.InternalData internalData, NativeArray<int> oldToNewVMap, NativeArray<Vector3> newPositions)
        {
            if (interpolateUV)
                uvInterpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: false, saveW: false);

            if (interpolateUV2)
                uv2Interpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: false, saveW: false);

            if (interpolateUV3)
                uv3Interpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: false, saveW: false);

            if (interpolateUV4)
                uv4Interpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: false, saveW: false);

            if (interpolateNormals)
                normalsInterpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: true, saveW: false);

            if (interpolateTangents)
                tangentsInterpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: true, saveW: true);

            if (interpolateColors)
                colorsInterpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions, normalize: false, saveW: false);

            if (interpolateBoneWeights)
                boneWeightsInterpolator.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions);
        }

        public void ApplyAttributes(Mesh[] meshes)
        {
            if (interpolateUV)
                ApplyUVInterpolator(meshes);

            if (interpolateUV2)
            {
                ApplyUV2Interpolator(meshes);
            }
            else if (uv2Mode == AttributeInterpolationMode2.Recalculate_EditorOnly)
            {
                RecalculateUV2AndApply(meshes);
            }

            if (interpolateUV3)
                ApplyUV3Interpolator(meshes);

            if (interpolateUV4)
                ApplyUV4Interpolator(meshes);

            if (interpolateNormals)
            {
                ApplyNormalsInterpolator(meshes);
            }
            else if (normalsMode == AttributeInterpolationMode1.Recalculate)
            {
                RecalculateNormalsAndApply(meshes);
            }

            if (interpolateTangents)
            {
                ApplyTangentsInterpolator(meshes);
            }
            else if (tangentsMode == AttributeInterpolationMode1.Recalculate)
            {
                RecalculateTangentsAndApply(meshes);
            }

            if (interpolateColors)
                ApplyColorsInterpolator(meshes);

            if (interpolateBoneWeights)
                ApplyBoneWeightsInterpolator(meshes);
        }

        public void Dispose()
        {
            uvInterpolator.Dispose();
            uv2Interpolator.Dispose();
            uv3Interpolator.Dispose();
            uv4Interpolator.Dispose();
            normalsInterpolator.Dispose();
            tangentsInterpolator.Dispose();
            colorsInterpolator.Dispose();
            boneWeightsInterpolator.Dispose();
        }



        private void CreateUVInterpolator(Mesh.MeshData meshData)
        {
            if (!interpolateUV)
            {
                uvInterpolator = new VertexAttributeInterpolator2D();
                uvInterpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector2>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector2> uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetUVs(0, uvs);

            uvInterpolator = new VertexAttributeInterpolator2D();
            uvInterpolator.InitializeInMainThread(uvMode, uvs);
        }

        private void CreateUV2Interpolator(Mesh.MeshData meshData)
        {
            if (!interpolateUV2)
            {
                uv2Interpolator = new VertexAttributeInterpolator2D();
                uv2Interpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector2>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector2> uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetUVs(1, uvs);

            uv2Interpolator = new VertexAttributeInterpolator2D();
            uv2Interpolator.InitializeInMainThread(ConvertInterpolationMode(uv2Mode), uvs);
        }

        private void CreateUV3Interpolator(Mesh.MeshData meshData)
        {
            if (!interpolateUV3)
            {
                uv3Interpolator = new VertexAttributeInterpolator2D();
                uv3Interpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector2>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector2> uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetUVs(2, uvs);

            uv3Interpolator = new VertexAttributeInterpolator2D();
            uv3Interpolator.InitializeInMainThread(uv3Mode, uvs);
        }

        private void CreateUV4Interpolator(Mesh.MeshData meshData)
        {
            if (!interpolateUV4)
            {
                uv4Interpolator = new VertexAttributeInterpolator2D();
                uv4Interpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector2>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector2> uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetUVs(3, uvs);

            uv4Interpolator = new VertexAttributeInterpolator2D();
            uv4Interpolator.InitializeInMainThread(uv4Mode, uvs);
        }

        private void CreateNormalsInterpolator(Mesh.MeshData meshData)
        {
            if (!interpolateNormals)
            {
                normalsInterpolator = new VertexAttributeInterpolator3D();
                normalsInterpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector3>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector3> normals = new NativeArray<Vector3>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetNormals(normals);

            normalsInterpolator = new VertexAttributeInterpolator3D();
            normalsInterpolator.InitializeInMainThread(ConvertInterpolationMode(normalsMode), normals);
        }

        private void CreateTangentsInterpolator(Mesh.MeshData meshData)
        {
            if (!interpolateTangents)
            {
                tangentsInterpolator = new VertexAttributeInterpolator4D();
                tangentsInterpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector4>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Vector4> tangents = new NativeArray<Vector4>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetTangents(tangents);

            tangentsInterpolator = new VertexAttributeInterpolator4D();
            tangentsInterpolator.InitializeInMainThread(ConvertInterpolationMode(tangentsMode), tangents);
        }

        private void CreateColorsInterpolator(Mesh.MeshData meshData)
        {
            if (!interpolateColors)
            {
                colorsInterpolator = new VertexAttributeInterpolator4D();
                colorsInterpolator.InitializeInMainThread(AttributeInterpolationMode.None, new NativeArray<Vector4>(0, Allocator.Persistent));

                return;
            }

            NativeArray<Color> colors = new NativeArray<Color>(meshData.vertexCount, Allocator.Persistent);
            meshData.GetColors(colors);

            colorsInterpolator = new VertexAttributeInterpolator4D();
            colorsInterpolator.InitializeInMainThread(colorsMode, colors.Reinterpret<Vector4>());
        }

        private void CreateBoneWeightInterpolator(BoneWeight[] boneWeights)
        {
            if (!interpolateBoneWeights)
            {
                boneWeightsInterpolator = new VertexAttributeInterpolatorBoneWeight();
                boneWeightsInterpolator.InitializeInMainThread(new NativeArray<BoneWeight>(0, Allocator.Persistent));

                return;
            }

            NativeArray<BoneWeight> boneWeightsData = new NativeArray<BoneWeight>(boneWeights.Length, Allocator.Persistent);

            for (int i = 0; i < boneWeights.Length; i++)
                boneWeightsData[i] = boneWeights[i];

            boneWeightsInterpolator = new VertexAttributeInterpolatorBoneWeight();
            boneWeightsInterpolator.InitializeInMainThread(boneWeightsData);
        }


        private void ApplyUVInterpolator(Mesh[] meshes)
        {
            NativeArray<Vector2> uv = uvInterpolator.OutputData.AsArray();
            NativeList<int> pointers = uvInterpolator.OutputDataPointers;

            int uvChannel = 0;
            int start = 0;

            for (int i = 0; i < pointers.Length; i++)
            {
                int length = pointers[i];

                meshes[i].SetUVs(uvChannel, uv, start, length);

                start += length;
            }
        }

        private void ApplyUV2Interpolator(Mesh[] meshes)
        {
            NativeArray<Vector2> uv2 = uv2Interpolator.OutputData.AsArray();
            NativeList<int> pointers = uv2Interpolator.OutputDataPointers;

            int uvChannel = 1;
            int start = 0;

            for (int i = 0; i < pointers.Length; i++)
            {
                int length = pointers[i];

                meshes[i].SetUVs(uvChannel, uv2, start, length);

                start += length;
            }
        }

        private void ApplyUV3Interpolator(Mesh[] meshes)
        {
            NativeArray<Vector2> uv3 = uv3Interpolator.OutputData.AsArray();
            NativeList<int> pointers = uv3Interpolator.OutputDataPointers;

            int uvChannel = 2;
            int start = 0;

            for (int i = 0; i < pointers.Length; i++)
            {
                int length = pointers[i];

                meshes[i].SetUVs(uvChannel, uv3, start, length);

                start += length;
            }
        }

        private void ApplyUV4Interpolator(Mesh[] meshes)
        {
            NativeArray<Vector2> uv4 = uv4Interpolator.OutputData.AsArray();
            NativeList<int> pointers = uv4Interpolator.OutputDataPointers;

            int uvChannel = 3;
            int start = 0;

            for (int i = 0; i < pointers.Length; i++)
            {
                int length = pointers[i];

                meshes[i].SetUVs(uvChannel, uv4, start, length);

                start += length;
            }
        }

        private void ApplyNormalsInterpolator(Mesh[] meshes)
        {
            NativeArray<Vector3> normals = normalsInterpolator.OutputData.AsArray();
            NativeArray<int> lengths = normalsInterpolator.OutputDataPointers.AsArray();

            int start = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];

                meshes[i].SetNormals(normals, start, length);

                start += length;
            }
        }

        private void ApplyTangentsInterpolator(Mesh[] meshes)
        {
            NativeArray<Vector4> tangents = tangentsInterpolator.OutputData.AsArray();
            NativeArray<int> lengths = tangentsInterpolator.OutputDataPointers.AsArray();

            int start = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];

                meshes[i].SetTangents(tangents, start, length);

                start += length;
            }
        }

        private void ApplyColorsInterpolator(Mesh[] meshes)
        {
            NativeArray<Color> colors = colorsInterpolator.OutputData.AsArray().Reinterpret<Color>();
            NativeArray<int> lengths = colorsInterpolator.OutputDataPointers.AsArray();

            int start = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];

                meshes[i].SetColors(colors, start, length);

                start += length;
            }
        }

        private void ApplyBoneWeightsInterpolator(Mesh[] meshes)
        {
            NativeArray<BoneWeight> boneWeights = boneWeightsInterpolator.OutputData.AsArray();
            NativeArray<int> lengths = boneWeightsInterpolator.OutputDataPointers.AsArray();

            int start = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];

                meshes[i].boneWeights = boneWeights.GetSubArray(start, length).ToArray();

                start += length;
            }
        }


        private void RecalculateUV2AndApply(Mesh[] meshes)
        {
            #if UNITY_EDITOR

            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];

                UnityEditor.Unwrapping.GenerateSecondaryUVSet(mesh);
            }
            
            #endif
        }

        private void RecalculateNormalsAndApply(Mesh[] meshes)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];

                mesh.RecalculateNormals();
            }
        }

        private void RecalculateTangentsAndApply(Mesh[] meshes)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];

                mesh.RecalculateTangents();
            }
        }


        private AttributeInterpolationMode ConvertInterpolationMode(AttributeInterpolationMode1 from)
        {
            if (from == AttributeInterpolationMode1.Barycentric)
                return AttributeInterpolationMode.Barycentric;

            if (from == AttributeInterpolationMode1.Quadric)
                return AttributeInterpolationMode.Quadric;

            return AttributeInterpolationMode.None;
        }

        private AttributeInterpolationMode ConvertInterpolationMode(AttributeInterpolationMode2 from)
        {
            if (from == AttributeInterpolationMode2.Barycentric)
                return AttributeInterpolationMode.Barycentric;

            if (from == AttributeInterpolationMode2.Quadric)
                return AttributeInterpolationMode.Quadric;

            return AttributeInterpolationMode.None;
        }
    }



    public struct VertexAttributeInterpolator2D
    {
        public AttributeInterpolationMode Mode
        {
            get
            {
                return _mode;
            }
        }
        public NativeList<Vector2> OutputData
        {
            get
            {
                return _outputData;
            }
        }
        public NativeList<int> OutputDataPointers
        {
            get
            {
                return _outputDataPointers;
            }
        }

        private AttributeInterpolationMode _mode;
        private AttributeQuadricInterpolator<Quadric2D, Vector2> _quadricInterpolator;
        private AttributeBarycentricInterpolator<Vector2BarycentricApply, Vector2> _barycentricInterpolator;

        private NativeList<Vector2> _outputData;
        private NativeList<int> _outputDataPointers;


        public void InitializeInMainThread(AttributeInterpolationMode mode, NativeArray<Vector2> attributes)
        {
            _mode = mode;

            if (mode == AttributeInterpolationMode.Quadric)
            {
                _barycentricInterpolator.InitializeInMainThread(new NativeArray<Vector2>(0, Allocator.Persistent));
                _quadricInterpolator.InitializeInMainThread(attributes);
            }
            else
            {
                _barycentricInterpolator.InitializeInMainThread(attributes);
                _quadricInterpolator.InitializeInMainThread(new NativeArray<Vector2>(0, Allocator.Persistent));
            }

            _outputData = new NativeList<Vector2>(Allocator.Persistent);
            _outputDataPointers = new NativeList<int>(Allocator.Persistent);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Initialize(ref internalData);
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            else
                _barycentricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);
        }

        public void OutputInterpolatedData(
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            NativeArray<Vector3> newPositions,
            bool normalize, bool saveW)
        {
            int start = _outputData.Length;
            int length = newPositions.Length;

            _outputData.Resize(start + length, NativeArrayOptions.UninitializedMemory);

            NativeSlice<Vector2> dataSlice = new NativeSlice<Vector2>(_outputData.AsArray(), start, length);

            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, newPositions, normalize, saveW);

            else
                _barycentricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, normalize, saveW);

            _outputDataPointers.Add(length);
        }

        public void Dispose()
        {
            _quadricInterpolator.Dispose();
            _barycentricInterpolator.Dispose();

            if (_outputData.IsCreated)
            {
                _outputData.Dispose();
                _outputDataPointers.Dispose();
            }
        }
    }

    public struct VertexAttributeInterpolator3D
    {
        public AttributeInterpolationMode Mode
        {
            get
            {
                return _mode;
            }
        }
        public NativeList<Vector3> OutputData
        {
            get
            {
                return _outputData;
            }
        }
        public NativeList<int> OutputDataPointers
        {
            get
            {
                return _outputDataPointers;
            }
        }

        private AttributeInterpolationMode _mode;
        private AttributeQuadricInterpolator<Quadric3D, Vector3> _quadricInterpolator;
        private AttributeBarycentricInterpolator<Vector3BarycentricApply, Vector3> _barycentricInterpolator;

        private NativeList<Vector3> _outputData;
        private NativeList<int> _outputDataPointers;


        public void InitializeInMainThread(AttributeInterpolationMode mode, NativeArray<Vector3> attributes)
        {
            _mode = mode;

            if (mode == AttributeInterpolationMode.Quadric)
            {
                _barycentricInterpolator.InitializeInMainThread(new NativeArray<Vector3>(0, Allocator.Persistent));
                _quadricInterpolator.InitializeInMainThread(attributes);
            }
            else
            {
                _barycentricInterpolator.InitializeInMainThread(attributes);
                _quadricInterpolator.InitializeInMainThread(new NativeArray<Vector3>(0, Allocator.Persistent));
            }

            _outputData = new NativeList<Vector3>(Allocator.Persistent);
            _outputDataPointers = new NativeList<int>(Allocator.Persistent);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Initialize(ref internalData);
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            else
                _barycentricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);
        }

        public void OutputInterpolatedData(
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            NativeArray<Vector3> newPositions,
            bool normalize, bool saveW)
        {
            int start = _outputData.Length;
            int length = newPositions.Length;

            _outputData.Resize(start + length, NativeArrayOptions.UninitializedMemory);

            NativeSlice<Vector3> dataSlice = new NativeSlice<Vector3>(_outputData.AsArray(), start, length);

            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, newPositions, normalize, saveW);

            else
                _barycentricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, normalize, saveW);

            _outputDataPointers.Add(length);
        }

        public void Dispose()
        {
            _quadricInterpolator.Dispose();
            _barycentricInterpolator.Dispose();

            if (_outputData.IsCreated)
            {
                _outputData.Dispose();
                _outputDataPointers.Dispose();
            }
        }
    }

    public struct VertexAttributeInterpolator4D
    {
        public AttributeInterpolationMode Mode
        {
            get
            {
                return _mode;
            }
        }
        public NativeList<Vector4> OutputData
        {
            get
            {
                return _outputData;
            }
        }
        public NativeList<int> OutputDataPointers
        {
            get
            {
                return _outputDataPointers;
            }
        }

        private AttributeInterpolationMode _mode;
        private AttributeQuadricInterpolator<Quadric4D, Vector4> _quadricInterpolator;
        private AttributeBarycentricInterpolator<Vector4BarycentricApply, Vector4> _barycentricInterpolator;

        private NativeList<Vector4> _outputData;
        private NativeList<int> _outputDataPointers;


        public void InitializeInMainThread(AttributeInterpolationMode mode, NativeArray<Vector4> attributes)
        {
            _mode = mode;

            if (mode == AttributeInterpolationMode.Quadric)
            {
                _barycentricInterpolator.InitializeInMainThread(new NativeArray<Vector4>(0, Allocator.Persistent));
                _quadricInterpolator.InitializeInMainThread(attributes);
            }
            else
            {
                _barycentricInterpolator.InitializeInMainThread(attributes);
                _quadricInterpolator.InitializeInMainThread(new NativeArray<Vector4>(0, Allocator.Persistent));
            }

            _outputData = new NativeList<Vector4>(Allocator.Persistent);
            _outputDataPointers = new NativeList<int>(Allocator.Persistent);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Initialize(ref internalData);
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);

            else
                _barycentricInterpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);
        }

        public void OutputInterpolatedData(
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            NativeArray<Vector3> newPositions,
            bool normalize, bool saveW)
        {
            int start = _outputData.Length;
            int length = newPositions.Length;

            _outputData.Resize(start + length, NativeArrayOptions.UninitializedMemory);

            NativeSlice<Vector4> dataSlice = new NativeSlice<Vector4>(_outputData.AsArray(), start, length);

            if (_mode == AttributeInterpolationMode.Quadric)
                _quadricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, newPositions, normalize, saveW);

            else
                _barycentricInterpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap, normalize, saveW);

            _outputDataPointers.Add(length);
        }

        public void Dispose()
        {
            _quadricInterpolator.Dispose();
            _barycentricInterpolator.Dispose();

            if (_outputData.IsCreated)
            {
                _outputData.Dispose();
                _outputDataPointers.Dispose();
            }
        }
    }

    public struct VertexAttributeInterpolatorBoneWeight
    {
        public bool IsCreated
        {
            get
            {
                return _isCreated;
            }
        }
        public NativeList<BoneWeight> OutputData
        {
            get
            {
                return _outputData;
            }
        }
        public NativeList<int> OutputDataPointers
        {
            get
            {
                return _outputDataPointers;
            }
        }

        private BoneWeightsInterpolator _interpolator;
        private bool _isCreated;

        private NativeList<BoneWeight> _outputData;
        private NativeList<int> _outputDataPointers;


        public void InitializeInMainThread(NativeArray<BoneWeight> originalBones)
        {
            _interpolator = new BoneWeightsInterpolator();
            _isCreated = true;

            _outputData = new NativeList<BoneWeight>(Allocator.Persistent);
            _outputDataPointers = new NativeList<int>(Allocator.Persistent);

            _interpolator.InitializeInMainThread(originalBones);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            _interpolator.Initialize(ref internalData);
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            _interpolator.Interpolate(ref internalData, ref edge, survivedIndex, deletedIndex);
        }

        public void OutputInterpolatedData(
            ref MeshDecimator.InternalData internalData,
            NativeArray<int> oldToNewVMap,
            NativeArray<Vector3> newPositions)
        {
            int start = _outputData.Length;
            int length = newPositions.Length;

            _outputData.Resize(start + length, NativeArrayOptions.UninitializedMemory);

            NativeSlice<BoneWeight> dataSlice = new NativeSlice<BoneWeight>(_outputData.AsArray(), start, length);

            _interpolator.OutputInterpolatedData(dataSlice, ref internalData, oldToNewVMap);

            _outputDataPointers.Add(length);
        }

        public void Dispose()
        {
            if (!_isCreated)
                return;

            _interpolator.Dispose();
            _outputData.Dispose();
            _outputDataPointers.Dispose();
        }
    }
}