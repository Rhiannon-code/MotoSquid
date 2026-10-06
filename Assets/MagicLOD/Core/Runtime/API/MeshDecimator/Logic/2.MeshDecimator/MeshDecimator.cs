using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;


namespace NGS.MagicLOD.Runtime
{
    public static class MeshDecimator
    {
        public static DecimationResult Decimate(Mesh originalMesh, DecimationSettings settings, IReadOnlyList<float> quality)
        {
            return StartDecimationTask(originalMesh, settings, quality)
                .CompleteAndGetResult();
        }

        public static DecimationResult Simplify(Mesh originalMesh, DecimationSettings settings, float maxSimplificationError)
        {
            return StartSimplificationTask(originalMesh, settings, maxSimplificationError)
                .CompleteAndGetResult();
        }

        public static Handle StartDecimationTask(Mesh originalMesh, DecimationSettings settings, IReadOnlyList<float> quality)
        {
            return StartDecimationTaskInternal(originalMesh, settings, quality, float.MaxValue);
        }

        public static Handle StartSimplificationTask(Mesh originalMesh, DecimationSettings settings, float maxSimplificationError)
        {
            return StartDecimationTaskInternal(originalMesh, settings, new float[] { 0.001f }, maxSimplificationError);
        }


        private static Handle StartDecimationTaskInternal(Mesh originalMesh, DecimationSettings settings, IReadOnlyList<float> quality, float maxError)
        {
            if (!VerifyInput(originalMesh, settings, quality, out DecimationResult verificationResult))
                return new Handle(new TaskInternal(verificationResult));

            TaskInternal internalTask = new TaskInternal
            {
                originalMesh = originalMesh,
                quality = quality
            };

            NativeList<float> uniqQuality = default;
            bool originalMeshDataCreated = false;
            bool writableMeshDataCreated = false;
            bool jobCreated = false;

            try
            {
                uniqQuality = CreateUniqQualityList(quality);

                internalTask.originalMeshData = Mesh.AcquireReadOnlyMeshData(originalMesh);
                originalMeshDataCreated = true;

                internalTask.writableMeshData = Mesh.AllocateWritableMeshData(uniqQuality.Length);
                writableMeshDataCreated = true;

                internalTask.job = CreateMeshDecimationJob(originalMesh, internalTask.originalMeshData[0], internalTask.writableMeshData, settings, uniqQuality, maxError);
                jobCreated = true;

                internalTask.handle = internalTask.job.Schedule();

                return new Handle(internalTask);
            }
            catch (Exception ex)
            {
                if (jobCreated)
                {
                    internalTask.handle.Complete();
                    DisposeMeshDecimationJob(internalTask.job);
                }
                else if (uniqQuality.IsCreated)
                {
                    uniqQuality.Dispose();
                }

                if (originalMeshDataCreated)
                    internalTask.originalMeshData.Dispose();

                if (writableMeshDataCreated)
                    internalTask.writableMeshData.Dispose();

                return new Handle(new TaskInternal(new DecimationResult($"{ex.Message}\n{ex.StackTrace}")));
            }
        }

        private static DecimationResult CompleteDecimationTaskAndGetResult(TaskInternal internalTask)
        {
            if (internalTask.hasVerificationError)
                return internalTask.verificationErrorResult;

            Mesh originalMesh = internalTask.originalMesh;

            Mesh.MeshDataArray meshDataArray = internalTask.originalMeshData;
            Mesh.MeshDataArray writableMeshData = internalTask.writableMeshData;

            MeshDecimationJob job = internalTask.job;
            JobHandle handle = internalTask.handle;

            bool writableMeshDataDisposed = false;

            try
            {
                handle.Complete();

                Mesh[] decimatedMeshes = new Mesh[writableMeshData.Length];

                for (int i = 0; i < decimatedMeshes.Length; i++)
                    decimatedMeshes[i] = new Mesh();

                Mesh.ApplyAndDisposeWritableMeshData(writableMeshData, decimatedMeshes);
                writableMeshDataDisposed = true;

                ApplyAttributesProcessor(job.attributesProcessor, decimatedMeshes);
                ApplyBindposesIfNotNull(originalMesh, decimatedMeshes);

                for (int i = 0; i < decimatedMeshes.Length; i++)
                {
                    Mesh decimatedMesh = decimatedMeshes[i];

                    decimatedMesh.name = $"{originalMesh.name}_{decimatedMesh.GetTotalTriangles()}tris";
                    decimatedMesh.RecalculateBounds();
                }

                IReadOnlyList<float> quality = internalTask.quality;

                if (quality.Count == decimatedMeshes.Length)
                    return new DecimationResult(decimatedMeshes);

                Mesh[] result = new Mesh[quality.Count];

                for (int i = 0, idx = 0; i < quality.Count; i++)
                {
                    float q = quality[i];

                    if (q.Equals(1f))
                        result[i] = originalMesh;

                    else if (q.Equals(0f))
                        result[i] = null;

                    else if (i > 0 && q.Equals(quality[i - 1]))
                        result[i] = result[i - 1];

                    else
                        result[i] = decimatedMeshes[idx++];
                }

                return new DecimationResult(result);
            }
            catch (Exception ex)
            {
                return new DecimationResult($"{ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                DisposeMeshDecimationJob(job);
                meshDataArray.Dispose();

                if (!writableMeshDataDisposed)
                    writableMeshData.Dispose();
            }
        }


        private static bool VerifyInput(Mesh originalMesh, DecimationSettings settings, IReadOnlyList<float> quality, out DecimationResult errorResult)
        {
            if (originalMesh == null)
            {
                errorResult = new DecimationResult("MeshDecimator::originalMesh is null");
                return false;
            }

            if (quality == null || quality.Count == 0)
            {
                errorResult = new DecimationResult("MeshDecimator::quality array is null or length is 0");
                return false;
            }

            for (int i = 0; i < quality.Count; i++)
            {
                float q = quality[i];

                if (q <= 0 || q > 1f)
                {
                    errorResult = new DecimationResult($"MeshDecimator::quality[{i}]={q} not in range [0,1]");
                    return false;
                }

                if (i < quality.Count - 1 && q < quality[i + 1])
                {
                    errorResult = new DecimationResult("MeshDecimator::quality array should be strictly ordered from max to min");
                    return false;
                }
            }

            errorResult = default;
            return true;
        }

        private static NativeList<float> CreateUniqQualityList(IReadOnlyList<float> quality)
        {
            NativeList<float> uniqQuality = new NativeList<float>(quality.Count, Allocator.Persistent);

            for (int i = 0; i < quality.Count; i++)
            {
                float q = quality[i];

                if (q.Equals(1f) || q.Equals(0f))
                    continue;

                if (i > 0 && q.Equals(quality[i - 1]))
                    continue;

                uniqQuality.Add(q);
            }

            return uniqQuality;
        }

        private static MeshDecimationJob CreateMeshDecimationJob(
            Mesh mesh, 
            Mesh.MeshData originalMeshData, 
            Mesh.MeshDataArray writableMeshData, 
            DecimationSettings settings, 
            NativeList<float> uniqQuality,
            float maxError)
        {
            return new MeshDecimationJob()
            {
                bordersPenaltyWeight = (int) settings.bordersPenaltyWeight,
                normalSeamsPenaltyWeight = (int) settings.normalSeamsPenaltyWeight,
                uvSeamsPenaltyWeight = (int) settings.uvSeamsPenaltyWeight,
                preservationVolumes = CreatePreservationVolumes(settings.preservationVolumes),
                quality = uniqQuality,
                maxError = maxError,

                originalMeshData = originalMeshData,
                writableMeshData = writableMeshData,

                vertices = new NativeArray<Vertex>(originalMeshData.vertexCount, Allocator.Persistent),
                triangles = CreateTriangles(mesh),
                edges = new NativeList<Edge>(originalMeshData.vertexCount, Allocator.Persistent),
                verticesRemap = new NativeArray<int>(originalMeshData.vertexCount, Allocator.Persistent),

                vertexQuadrics = new NativeArray<VertexQuadric>(originalMeshData.vertexCount, Allocator.Persistent),
                vertexAliases = new Aliases(originalMeshData.vertexCount),

                sortedEdges = new EdgeMinHeap(originalMeshData.vertexCount),
                vertexToTriangles = CreateVertexToTriangles(),

                attributesProcessor = CreateAttributesProcessor(mesh, originalMeshData, settings)
            };
        }

        private static NativeArray<PreservationVolume> CreatePreservationVolumes(PreservationVolume[] volumes)
        {
            if (volumes == null)
                return new NativeArray<PreservationVolume>(0, Allocator.Persistent);

            return new NativeArray<PreservationVolume>(volumes, Allocator.Persistent);
        }

        private static NativeArray<Triangle> CreateTriangles(Mesh mesh)
        {
            int subMeshCount = mesh.subMeshCount;
            int totalTrianglesCount = 0;

            int[][] subMeshesTriangles = new int[subMeshCount][];

            for (int i = 0; i < subMeshCount; i++)
            {
                subMeshesTriangles[i] = mesh.GetTriangles(i);
                totalTrianglesCount += subMeshesTriangles[i].Length / 3;
            }

            NativeArray<Triangle> triangles = new NativeArray<Triangle>(totalTrianglesCount, Allocator.Persistent);
            int tIndex = 0;

            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                int[] currentTriangles = subMeshesTriangles[subMesh];
                int trisCount = currentTriangles.Length / 3;

                for (int i = 0; i < trisCount; i++)
                {
                    int origV0 = currentTriangles[i * 3];
                    int origV1 = currentTriangles[i * 3 + 1];
                    int origV2 = currentTriangles[i * 3 + 2];

                    Triangle triangle = default;

                    triangle.origV0 = origV0;
                    triangle.origV1 = origV1;
                    triangle.origV2 = origV2;
                    triangle.subMeshIndex = subMesh;

                    triangles[tIndex] = triangle;
                    tIndex++;
                }
            }

            return triangles;
        }

        private static VertexToTriangles CreateVertexToTriangles()
        {
            VertexToTriangles vertexToTriangles = new VertexToTriangles();

            vertexToTriangles.Initialize();

            return vertexToTriangles;
        }

        private static AttributesProcessor CreateAttributesProcessor(Mesh mesh, Mesh.MeshData meshData, DecimationSettings settings)
        {
            AttributesProcessor attributesProcessor = new AttributesProcessor();

            attributesProcessor.InitializeAttributes(mesh, meshData, settings);

            return attributesProcessor;
        }

        private static void ApplyAttributesProcessor(AttributesProcessor attributesProcessor, Mesh[] meshes)
        {
            attributesProcessor.ApplyAttributes(meshes);
        }

        private static void ApplyBindposesIfNotNull(Mesh originalMesh, Mesh[] meshes)
        {
            Matrix4x4[] bindposes = originalMesh.bindposes;

            if (bindposes == null || bindposes.Length == 0)
                return;

            for (int i = 0; i < meshes.Length; i++)
                meshes[i].bindposes = bindposes;
        }

        private static void DisposeMeshDecimationJob(MeshDecimationJob job)
        {
            if (job.preservationVolumes.IsCreated)
                job.preservationVolumes.Dispose();

            if (job.quality.IsCreated)
                job.quality.Dispose();

            if (job.vertices.IsCreated)
                job.vertices.Dispose();

            if (job.triangles.IsCreated)
                job.triangles.Dispose();

            if (job.edges.IsCreated)
                job.edges.Dispose();

            if (job.verticesRemap.IsCreated)
                job.verticesRemap.Dispose();

            if (job.vertexQuadrics.IsCreated)
                job.vertexQuadrics.Dispose();

            job.vertexAliases.Dispose();
            job.sortedEdges.Dispose();
            job.vertexToTriangles.Dispose();

            job.attributesProcessor.Dispose();
        }



        public struct InternalData
        {
            public NativeArray<Vertex> vertices;
            public NativeArray<Triangle> triangles;
            public NativeArray<int> verticesRemap;

            public Aliases vertexAliases;
            public VertexToTriangles vertexToTriangles;
        }

        public struct TaskInternal
        {
            public Mesh originalMesh;
            public IReadOnlyList<float> quality;

            public Mesh.MeshDataArray originalMeshData;
            public Mesh.MeshDataArray writableMeshData;

            public MeshDecimationJob job;
            public JobHandle handle;

            public bool hasVerificationError;
            public DecimationResult verificationErrorResult;


            public TaskInternal(DecimationResult verificationErrorResult)
            {
                originalMesh = null;
                quality = null;

                originalMeshData = default;
                writableMeshData = default;

                job = default;
                handle = new JobHandle();

                hasVerificationError = true;
                this.verificationErrorResult = verificationErrorResult;
            }

            public DecimationResult CompleteAndGetResult()
            {
                if (hasVerificationError)
                    return verificationErrorResult;

                return CompleteDecimationTaskAndGetResult(this);
            }

            public bool IsCompleted()
            {
                return handle.IsCompleted;
            }
        }

        public struct Handle
        {
            private TaskInternal _task;

            public Handle(TaskInternal internalTask)
            {
                _task = internalTask;
            }

            public bool IsCompleted()
            {
                return _task.handle.IsCompleted;   
            }

            public DecimationResult CompleteAndGetResult()
            {
                return _task.CompleteAndGetResult();
            }
        }



        [BurstCompile]
        public struct MeshDecimationJob : IJob
        {
            public int bordersPenaltyWeight;
            public int uvSeamsPenaltyWeight;
            public int normalSeamsPenaltyWeight;
            public NativeArray<PreservationVolume> preservationVolumes;
            public NativeList<float> quality;
            public float maxError;

            [ReadOnly]
            public Mesh.MeshData originalMeshData;
            public Mesh.MeshDataArray writableMeshData;

            public NativeArray<Vertex> vertices;
            public NativeArray<Triangle> triangles;
            public NativeList<Edge> edges;
            public NativeArray<int> verticesRemap;

            public NativeArray<VertexQuadric> vertexQuadrics;
            public Aliases vertexAliases;

            public EdgeMinHeap sortedEdges;
            public VertexToTriangles vertexToTriangles;

            public AttributesProcessor attributesProcessor;


            public void Execute()
            {
                InitVerticesAndPerformLinking();
                InitTriangles();
                InitAdjacency();
                InitAttributesProcessor();
                CreateEdges();
                CalculatePreservationVolumesPenalty();
                CalculateEdgesPenalty();
                CalculateEdgesError();
                SortEdgesByError();

                int initialTrianglesCount = triangles.Length;

                for (int i = 0; i < quality.Length; i++)
                {
                    CollapseEdges(initialTrianglesCount, quality[i]);
                    WriteMeshData(i);
                }
            }


            private void InitVerticesAndPerformLinking()
            {
                NativeArray<Vector3> originalVertices = new NativeArray<Vector3>(originalMeshData.vertexCount, Allocator.Temp);
                NativeHashMap<Vector3IntExact, int> uniqPositions = new NativeHashMap<Vector3IntExact, int>(originalVertices.Length, Allocator.Temp);

                originalMeshData.GetVertices(originalVertices);

                for (int i = 0; i < originalVertices.Length; i++)
                {
                    Vector3 originalVertex = originalVertices[i];
                    Vector3IntExact position = new Vector3IntExact(originalVertex);

                    vertices[i] = new Vertex(originalVertex);

                    if (uniqPositions.TryGetValue(position, out int masterIndex))
                    {
                        verticesRemap[i] = masterIndex;
                    }
                    else
                    {
                        uniqPositions.Add(position, i);
                        verticesRemap[i] = i;
                    }
                }

                originalVertices.Dispose();
                uniqPositions.Dispose();
            }

            private void InitTriangles()
            {
                for (int i = 0; i < triangles.Length; i++)
                {
                    Triangle triangle = triangles[i];

                    triangle.v0 = verticesRemap[triangle.origV0];
                    triangle.v1 = verticesRemap[triangle.origV1];
                    triangle.v2 = verticesRemap[triangle.origV2];

                    double3 p0 = vertices[triangle.v0].position;
                    double3 p1 = vertices[triangle.v1].position;
                    double3 p2 = vertices[triangle.v2].position;

                    triangle.normal = math.normalizesafe(math.cross(p1 - p0, p2 - p0));

                    triangles[i] = triangle;
                }
            }

            private void InitAdjacency()
            {
                vertexToTriangles.CreateAdjacencies(vertices, triangles);
            }

            private void InitAttributesProcessor()
            {
                InternalData internalData = GetInternalData();

                attributesProcessor.Initialize(ref internalData);
            }

            private void CreateEdges()
            {
                NativeArray<Vector3> originalNormals = default;
                NativeArray<Vector2> originalUVs = default;
                NativeHashMap<Edge, int> edgeToIndex = new NativeHashMap<Edge, int>(triangles.Length * 2, Allocator.Temp);

                if (originalMeshData.HasVertexAttribute(VertexAttribute.Normal))
                {
                    originalNormals = new NativeArray<Vector3>(originalMeshData.vertexCount, Allocator.Temp);
                    originalMeshData.GetNormals(originalNormals);
                }

                if (originalMeshData.HasVertexAttribute(VertexAttribute.TexCoord0))
                {
                    originalUVs = new NativeArray<Vector2>(originalMeshData.vertexCount, Allocator.Temp);
                    originalMeshData.GetUVs(0, originalUVs);
                }

                for (int i = 0; i < triangles.Length; i++)
                {
                    Triangle triangle = triangles[i];

                    if (triangle.IsDegenerated())
                        continue;

                    int v0 = triangle.v0;
                    int v1 = triangle.v1;
                    int v2 = triangle.v2;

                    int origV0 = triangle.origV0;
                    int origV1 = triangle.origV1;
                    int origV2 = triangle.origV2;

                    double3 p0 = vertices[v0].position;
                    double3 p1 = vertices[v1].position;
                    double3 p2 = vertices[v2].position;

                    VertexQuadric vq = new VertexQuadric(triangle.normal, p0);

                    VertexQuadric v0Quadric = vertexQuadrics[v0];
                    VertexQuadric v1Quadric = vertexQuadrics[v1];
                    VertexQuadric v2Quadric = vertexQuadrics[v2];

                    v0Quadric.Add(ref vq);
                    v1Quadric.Add(ref vq);
                    v2Quadric.Add(ref vq);

                    vertexQuadrics[v0] = v0Quadric;
                    vertexQuadrics[v1] = v1Quadric;
                    vertexQuadrics[v2] = v2Quadric;

                    Edge edge1 = new Edge(v0, v1, origV0, origV1, triangle.normal);
                    Edge edge2 = new Edge(v1, v2, origV1, origV2, triangle.normal);
                    Edge edge3 = new Edge(v0, v2, origV0, origV2, triangle.normal);

                    AddOrUpdateEdge(ref edgeToIndex, ref originalNormals, ref originalUVs, edge1);
                    AddOrUpdateEdge(ref edgeToIndex, ref originalNormals, ref originalUVs, edge2);
                    AddOrUpdateEdge(ref edgeToIndex, ref originalNormals, ref originalUVs, edge3);
                }

                if (originalNormals.IsCreated)
                    originalNormals.Dispose();

                if (originalUVs.IsCreated)
                    originalUVs.Dispose();

                edgeToIndex.Dispose();
            }

            private void CalculatePreservationVolumesPenalty()
            {
                if (preservationVolumes.Length == 0)
                    return;

                for (int v = 0; v < vertices.Length; v++)
                {
                    if (verticesRemap[v] != v)
                        continue;

                    double3 localPoint = vertices[v].position;
                    int penaltyMultiplier = 1;

                    for (int c = 0; c < preservationVolumes.Length; c++)
                    {
                        PreservationVolume volume = preservationVolumes[c];

                        if (volume.Contains(localPoint))
                            penaltyMultiplier = math.max(penaltyMultiplier, (int) volume.penaltyWeight);
                    }

                    if (penaltyMultiplier > 1)
                    {
                        VertexQuadric vertexQuadric = vertexQuadrics[v];

                        vertexQuadric.Multiply(penaltyMultiplier);

                        vertexQuadrics[v] = vertexQuadric;
                    }
                }
            }

            private void CalculateEdgesPenalty()
            {
                for (int i = 0; i < edges.Length; i++)
                {
                    Edge edge = edges[i];

                    if (edge.isBorder)
                        AddPenaltyForBorderEdge(ref edge, bordersPenaltyWeight);

                    if (edge.isNormalSeam)
                        AddPenaltyForSeam(ref edge, normalSeamsPenaltyWeight);

                    if (edge.isUVSeam)
                        AddPenaltyForSeam(ref edge, uvSeamsPenaltyWeight);

                    edges[i] = edge;
                }
            }

            private void CalculateEdgesError()
            {
                for (int i = 0; i < edges.Length; i++)
                {
                    Edge edge = edges[i];

                    CalculateEdgeError(ref edge);

                    edges[i] = edge;
                }
            }

            private void SortEdgesByError()
            {
                for (int i = 0; i < edges.Length; i++)
                {
                    sortedEdges.Push(new EdgeRef(i, edges[i].error));
                }
            }

            private void CollapseEdges(int initialTrianglesCount, float quality)
            {
                int targetTrianglesCount = (int)(initialTrianglesCount * quality);
                int currentTrianglesCount = triangles.Length;

                for (int i = 0; i < triangles.Length; i++)
                {
                    int v0 = vertexAliases.GetAlias(triangles[i].v0);
                    int v1 = vertexAliases.GetAlias(triangles[i].v1);
                    int v2 = vertexAliases.GetAlias(triangles[i].v2);

                    if (v0 == v1 || v1 == v2 || v2 == v0)
                        currentTrianglesCount--;
                }

                while (currentTrianglesCount > targetTrianglesCount && sortedEdges.Count > 0)
                {
                    EdgeRef edgeRef = sortedEdges.Pop();
                    Edge edge = edges[edgeRef.edgeIndex];

                    int v1 = vertexAliases.GetAlias(edge.vertex1);
                    int v2 = vertexAliases.GetAlias(edge.vertex2);

                    if (v1 == v2)
                        continue;

                    int expectedVersionSum = vertices[edge.vertex1].version + vertices[edge.vertex2].version;
                    double oldError = edge.error;

                    if (edge.versionSum != expectedVersionSum || v1 != edge.vertex1 || v2 != edge.vertex2)
                    {
                        CalculateEdgeError(ref edge);

                        edgeRef.edgeError = edge.error;

                        sortedEdges.Push(edgeRef);
                        edges[edgeRef.edgeIndex] = edge;

                        continue;
                    }

                    if (edge.error >= maxError)
                        break;

                    TryCollapseEdge(ref edge, out int removedTrianglesCount);

                    if (removedTrianglesCount > 0)
                        currentTrianglesCount -= removedTrianglesCount;
                }
            }

            private void WriteMeshData(int meshDataIndex)
            {
                Mesh.MeshData outputMesh = writableMeshData[meshDataIndex];
                
                NativeArray<int> oldToNewVMap = new NativeArray<int>(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < oldToNewVMap.Length; i++)
                    oldToNewVMap[i] = -1;

                int newVertexCount = 0;
                int validTrianglesCount = 0;

                for (int i = 0; i < triangles.Length; i++)
                {
                    Triangle t = triangles[i];

                    int v0 = vertexAliases.GetAlias(t.v0);
                    int v1 = vertexAliases.GetAlias(t.v1);
                    int v2 = vertexAliases.GetAlias(t.v2);

                    if (v0 == v1 || v1 == v2 || v2 == v0)
                        continue;

                    validTrianglesCount++;

                    if (oldToNewVMap[t.origV0] == -1) oldToNewVMap[t.origV0] = newVertexCount++;
                    if (oldToNewVMap[t.origV1] == -1) oldToNewVMap[t.origV1] = newVertexCount++;
                    if (oldToNewVMap[t.origV2] == -1) oldToNewVMap[t.origV2] = newVertexCount++;
                }

                NativeArray<VertexAttributeDescriptor> attributesLayout = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp);
                attributesLayout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);

                IndexFormat indexFormat = newVertexCount >= ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;

                outputMesh.SetVertexBufferParams(newVertexCount, attributesLayout);
                outputMesh.SetIndexBufferParams(validTrianglesCount * 3, indexFormat);
                outputMesh.subMeshCount = originalMeshData.subMeshCount;

                NativeArray<Vector3> newPositions = outputMesh.GetVertexData<Vector3>(0);

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (oldToNewVMap[i] == -1)
                        continue;

                    int newIdx = oldToNewVMap[i];
                    int rootAlias = vertexAliases.GetAlias(verticesRemap[i]);

                    double3 finalPos = vertices[rootAlias].position;

                    newPositions[newIdx] = new Vector3((float)finalPos.x, (float)finalPos.y, (float)finalPos.z);
                }

                if (indexFormat == IndexFormat.UInt32)
                    WriteIndices32(outputMesh, ref oldToNewVMap);
                else
                    WriteIndices16(outputMesh, ref oldToNewVMap);

                InternalData internalData = GetInternalData();
                attributesProcessor.OutputInterpolatedData(ref internalData, oldToNewVMap, newPositions);

                oldToNewVMap.Dispose();
                attributesLayout.Dispose();
            }


            private InternalData GetInternalData()
            {
                return new InternalData()
                {
                    vertices = vertices,
                    triangles = triangles,
                    verticesRemap = verticesRemap,
                    vertexAliases = vertexAliases,
                    vertexToTriangles = vertexToTriangles
                };
            }

            private void AddOrUpdateEdge(
                ref NativeHashMap<Edge, int> edgeToIndex,
                ref NativeArray<Vector3> originalNormals,
                ref NativeArray<Vector2> originalUVs,
                Edge edge)
            {
                if (edgeToIndex.TryGetValue(edge, out int edgeIndex))
                {
                    Edge storedEdge = edges[edgeIndex];

                    storedEdge.triangleNormal2 = edge.triangleNormal1;

                    storedEdge.isBorder = false;

                    if (originalNormals.IsCreated &&
                        (!originalNormals[storedEdge.origVertex1].Equals(originalNormals[edge.origVertex1]) ||
                        !originalNormals[storedEdge.origVertex2].Equals(originalNormals[edge.origVertex2])))
                    {
                        storedEdge.isNormalSeam = true;
                    }

                    if (originalUVs.IsCreated &&
                        (!originalUVs[storedEdge.origVertex1].Equals(originalUVs[edge.origVertex1]) ||
                         !originalUVs[storedEdge.origVertex2].Equals(originalUVs[edge.origVertex2])))
                    {
                        storedEdge.isUVSeam = true;
                    }

                    edges[edgeIndex] = storedEdge;
                }
                else
                {
                    edge.isBorder = true;

                    edgeToIndex.Add(edge, edges.Length);
                    edges.Add(edge);
                }
            }

            private void AddPenaltyForBorderEdge(ref Edge edge, int bordersPenaltyWeight)
            {
                double3 p1 = vertices[edge.vertex1].position;
                double3 p2 = vertices[edge.vertex2].position;

                double3 edgeDir = math.normalizesafe(p2 - p1);
                double3 penaltyNormal = math.normalizesafe(math.cross(edge.triangleNormal1, edgeDir));

                VertexQuadric penaltyMatrix = new VertexQuadric(penaltyNormal, p1, bordersPenaltyWeight);

                VertexQuadric vertex1Quadric = vertexQuadrics[edge.vertex1];
                VertexQuadric vertex2Quadric = vertexQuadrics[edge.vertex2];

                vertex1Quadric.Add(ref penaltyMatrix);
                vertex2Quadric.Add(ref penaltyMatrix);

                vertexQuadrics[edge.vertex1] = vertex1Quadric;
                vertexQuadrics[edge.vertex2] = vertex2Quadric;
            }

            private void AddPenaltyForSeam(ref Edge edge, double penaltyWeight)
            {
                double3 p1 = vertices[edge.vertex1].position;
                double3 p2 = vertices[edge.vertex2].position;

                double3 edgeDir = math.normalizesafe(p2 - p1);

                double3 penaltyNormal1 = math.normalizesafe(math.cross(edge.triangleNormal1, edgeDir));
                double3 penaltyNormal2 = math.normalizesafe(math.cross(edge.triangleNormal2, edgeDir));

                VertexQuadric penaltyMatrix1 = new VertexQuadric(penaltyNormal1, p1, penaltyWeight);
                VertexQuadric penaltyMatrix2 = new VertexQuadric(penaltyNormal2, p1, penaltyWeight);

                VertexQuadric vertex1Quadric = vertexQuadrics[edge.vertex1];
                VertexQuadric vertex2Quadric = vertexQuadrics[edge.vertex2];

                vertex1Quadric.Add(ref penaltyMatrix1);
                vertex1Quadric.Add(ref penaltyMatrix2);

                vertex2Quadric.Add(ref penaltyMatrix1);
                vertex2Quadric.Add(ref penaltyMatrix2);

                vertexQuadrics[edge.vertex1] = vertex1Quadric;
                vertexQuadrics[edge.vertex2] = vertex2Quadric;
            }

            private void CalculateEdgeError(ref Edge edge)
            {
                edge.vertex1 = vertexAliases.GetAlias(edge.vertex1);
                edge.vertex2 = vertexAliases.GetAlias(edge.vertex2);

                Vertex v1 = vertices[edge.vertex1];
                Vertex v2 = vertices[edge.vertex2];

                VertexQuadric errorQuadric = vertexQuadrics[edge.vertex1];
                VertexQuadric vertex2Quadric = vertexQuadrics[edge.vertex2];

                errorQuadric.Add(ref vertex2Quadric);

                if (errorQuadric.TryGetOptimalPosition(out double3 optimalPosition))
                {
                    edge.optimalPosition = optimalPosition;
                    edge.error = errorQuadric.ComputeVertexError(optimalPosition);
                }
                else
                {
                    double3 p1 = v1.position;
                    double3 p2 = v2.position;
                    double3 p3 = (p1 + p2) * 0.5;

                    double error1 = errorQuadric.ComputeVertexError(p1);
                    double error2 = errorQuadric.ComputeVertexError(p2);
                    double error3 = errorQuadric.ComputeVertexError(p3);

                    edge.error = error1;
                    edge.optimalPosition = p1;

                    if (error2 < edge.error)
                    {
                        edge.error = error2;
                        edge.optimalPosition = p2;
                    }

                    if (error3 < edge.error)
                    {
                        edge.error = error3;
                        edge.optimalPosition = p3;
                    }
                }

                edge.versionSum = v1.version + v2.version;
            }

            private void TryCollapseEdge(ref Edge edge, out int removedTrianglesCount)
            {
                int survivedIndex = vertexAliases.GetAlias(edge.vertex1);
                int deletedIndex = vertexAliases.GetAlias(edge.vertex2);

                int degenerateCount = 0;

                if (survivedIndex == deletedIndex)
                {
                    removedTrianglesCount = 0;
                    return;
                }

                NativeList<int> survivedTriangles = vertexToTriangles.GetTriangles(survivedIndex);

                for (int i = 0; i < survivedTriangles.Length; i++)
                {
                    int tIndex = survivedTriangles[i];

                    int t_v0 = vertexAliases.GetAlias(triangles[tIndex].v0);
                    int t_v1 = vertexAliases.GetAlias(triangles[tIndex].v1);
                    int t_v2 = vertexAliases.GetAlias(triangles[tIndex].v2);

                    if (t_v0 == t_v1 || t_v1 == t_v2 || t_v2 == t_v0)
                        continue;

                    if ((t_v0 == deletedIndex || t_v1 == deletedIndex || t_v2 == deletedIndex) &&
                        (t_v0 == survivedIndex || t_v1 == survivedIndex || t_v2 == survivedIndex))
                    {
                        degenerateCount++;
                        continue;
                    }

                    if (WillTriangleFlip(triangles[tIndex], deletedIndex, survivedIndex, edge.optimalPosition))
                    {
                        removedTrianglesCount = 0;
                        return;
                    }
                }

                NativeList<int> deletedTriangles = vertexToTriangles.GetTriangles(deletedIndex);

                for (int i = 0; i < deletedTriangles.Length; i++)
                {
                    int tIndex = deletedTriangles[i];

                    int t_v0 = vertexAliases.GetAlias(triangles[tIndex].v0);
                    int t_v1 = vertexAliases.GetAlias(triangles[tIndex].v1);
                    int t_v2 = vertexAliases.GetAlias(triangles[tIndex].v2);

                    if (t_v0 == t_v1 || t_v1 == t_v2 || t_v2 == t_v0)
                        continue;

                    if ((t_v0 == survivedIndex || t_v1 == survivedIndex || t_v2 == survivedIndex))
                        continue;

                    if (WillTriangleFlip(triangles[tIndex], deletedIndex, survivedIndex, edge.optimalPosition))
                    {
                        removedTrianglesCount = 0;
                        return;
                    }
                }

                InterpolateVertexAttributes(ref edge, survivedIndex, deletedIndex);

                Vertex survivedVertex = vertices[survivedIndex];
                survivedVertex.position = edge.optimalPosition;
                survivedVertex.version++;

                Vertex deletedVertex = vertices[deletedIndex];
                deletedVertex.version++;

                vertices[survivedIndex] = survivedVertex;
                vertices[deletedIndex] = deletedVertex;

                VertexQuadric survivedVertexQuadric = vertexQuadrics[survivedIndex];
                VertexQuadric deletedVertexQuadric = vertexQuadrics[deletedIndex];

                survivedVertexQuadric.Add(ref deletedVertexQuadric);

                vertexQuadrics[survivedIndex] = survivedVertexQuadric;

                vertexAliases.SetAlias(deletedIndex, survivedIndex);

                MergeAdjacency(survivedIndex, deletedIndex);

                NativeList<int> affectedTris = vertexToTriangles.GetTriangles(survivedIndex);

                for (int i = 0; i < affectedTris.Length; i++)
                    UpdateTriangleNormal(affectedTris[i]);

                removedTrianglesCount = degenerateCount;
            }

            private void InterpolateVertexAttributes(ref Edge edge, int survivedIndex, int deletedIndex)
            {
                InternalData internalData = GetInternalData();
                attributesProcessor.OnCollapseEdge(ref internalData, ref edge, survivedIndex, deletedIndex);
            }

            private void MergeAdjacency(int survivedVertexId, int deletedVertexId)
            {
                vertexToTriangles.MergeAdjacency(survivedVertexId, deletedVertexId);
            }

            private void UpdateTriangleNormal(int triangleIndex)
            {
                Triangle triangle = triangles[triangleIndex];

                int v0 = vertexAliases.GetAlias(triangle.v0);
                int v1 = vertexAliases.GetAlias(triangle.v1);
                int v2 = vertexAliases.GetAlias(triangle.v2);

                if (v0 == v1 || v1 == v2 || v2 == v0)
                    return;

                double3 p0 = vertices[v0].position;
                double3 p1 = vertices[v1].position;
                double3 p2 = vertices[v2].position;

                triangle.normal = math.normalizesafe(math.cross(p1 - p0, p2 - p0));

                triangles[triangleIndex] = triangle;
            }

            private bool WillTriangleFlip(Triangle triangle, int deletedIndex, int survivedIndex, double3 newPosition)
            {
                int v0 = vertexAliases.GetAlias(triangle.v0);
                int v1 = vertexAliases.GetAlias(triangle.v1);
                int v2 = vertexAliases.GetAlias(triangle.v2);

                int movingIndex = -1;

                if (v0 == deletedIndex || v0 == survivedIndex)
                    movingIndex = 0;

                else if (v1 == deletedIndex || v1 == survivedIndex)
                    movingIndex = 1;

                else if (v2 == deletedIndex || v2 == survivedIndex)
                    movingIndex = 2;

                if (movingIndex == -1)
                    return false;

                int id1 = -1, id2 = -1;

                if (movingIndex == 0)
                {
                    id1 = v1;
                    id2 = v2;
                }
                else if (movingIndex == 1)
                {
                    id1 = v2;
                    id2 = v0;
                }
                else if (movingIndex == 2)
                {
                    id1 = v0;
                    id2 = v1;
                }

                double3 p1 = vertices[id1].position;
                double3 p2 = vertices[id2].position;

                double3 d1 = math.normalizesafe(p1 - newPosition);
                double3 d2 = math.normalizesafe(p2 - newPosition);

                double3 newNormal = math.normalizesafe(math.cross(d1, d2));

                return math.dot(newNormal, triangle.normal) < 0.2;
            }

            private void WriteIndices16(Mesh.MeshData meshData, ref NativeArray<int> oldToNewVMap)
            {
                NativeArray<ushort> outIndices = meshData.GetIndexData<ushort>();

                int indexOffset = 0;

                for (int sm = 0; sm < meshData.subMeshCount; sm++)
                {
                    int subMeshStart = indexOffset;
                    int currentSubMeshIndexCount = 0;

                    for (int i = 0; i < triangles.Length; i++)
                    {
                        Triangle t = triangles[i];

                        if (t.subMeshIndex != sm)
                            continue;

                        int v0 = vertexAliases.GetAlias(t.v0);
                        int v1 = vertexAliases.GetAlias(t.v1);
                        int v2 = vertexAliases.GetAlias(t.v2);

                        if (v0 == v1 || v1 == v2 || v2 == v0)
                            continue;

                        outIndices[indexOffset++] = (ushort)oldToNewVMap[t.origV0];
                        outIndices[indexOffset++] = (ushort)oldToNewVMap[t.origV1];
                        outIndices[indexOffset++] = (ushort)oldToNewVMap[t.origV2];
                        currentSubMeshIndexCount += 3;
                    }

                    meshData.SetSubMesh(sm, new SubMeshDescriptor(subMeshStart, currentSubMeshIndexCount, MeshTopology.Triangles));
                }
            }

            private void WriteIndices32(Mesh.MeshData meshData, ref NativeArray<int> oldToNewVMap)
            {
                NativeArray<int> outIndices = meshData.GetIndexData<int>();

                int indexOffset = 0;

                for (int sm = 0; sm < meshData.subMeshCount; sm++)
                {
                    int subMeshStart = indexOffset;
                    int currentSubMeshIndexCount = 0;

                    for (int i = 0; i < triangles.Length; i++)
                    {
                        Triangle t = triangles[i];

                        if (t.subMeshIndex != sm)
                            continue;

                        int v0 = vertexAliases.GetAlias(t.v0);
                        int v1 = vertexAliases.GetAlias(t.v1);
                        int v2 = vertexAliases.GetAlias(t.v2);

                        if (v0 == v1 || v1 == v2 || v2 == v0)
                            continue;

                        outIndices[indexOffset++] = oldToNewVMap[t.origV0];
                        outIndices[indexOffset++] = oldToNewVMap[t.origV1];
                        outIndices[indexOffset++] = oldToNewVMap[t.origV2];
                        currentSubMeshIndexCount += 3;
                    }

                    meshData.SetSubMesh(sm, new SubMeshDescriptor(subMeshStart, currentSubMeshIndexCount, MeshTopology.Triangles));
                }
            }
        }
    }
}
