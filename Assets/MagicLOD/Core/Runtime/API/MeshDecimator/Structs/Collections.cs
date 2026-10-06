using System;
using Unity.Collections;

namespace NGS.MagicLOD.Runtime
{
    public struct Aliases
    {
        private NativeArray<int> _alias;

        public Aliases(int count)
        {
            _alias = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < _alias.Length; i++)
                _alias[i] = i;
        }

        public int GetAlias(int index)
        {
            int root = index;

            while (root != _alias[root])
                root = _alias[root];

            int current = index;

            while (current != root)
            {
                int next = _alias[current];

                _alias[current] = root;

                current = next;
            }

            return root;
        }

        public void SetAlias(int index, int value)
        {
            _alias[index] = value;
        }

        public void Dispose()
        {
            if (_alias.IsCreated)
                _alias.Dispose();
        }
    }


    public struct EdgeRef
    {
        public int edgeIndex;
        public double edgeError;

        public EdgeRef(int edgeIndex, double edgeError)
        {
            this.edgeIndex = edgeIndex;
            this.edgeError = edgeError;
        }
    }

    public struct EdgeMinHeap
    {
        public int Count
        {
            get
            {
                return _edges.Length;
            }
        }

        private NativeList<EdgeRef> _edges;


        public EdgeMinHeap(int capacity = 64)
        {
            _edges = new NativeList<EdgeRef>(capacity, Allocator.Persistent);
        }

        public void Push(EdgeRef edge)
        {
            _edges.Add(edge);

            SiftUp(Count - 1);
        }

        public EdgeRef Pop()
        {
            EdgeRef min = _edges[0];

            _edges.RemoveAtSwapBack(0);

            if (_edges.Length > 0)
                SiftDown(0);

            return min;
        }

        public void Dispose()
        {
            if (_edges.IsCreated)
                _edges.Dispose();
        }


        private void SiftUp(int index)
        {
            int parent = (index - 1) / 2;

            while (index > 0 && _edges[index].edgeError < _edges[parent].edgeError)
            {
                EdgeRef temp = _edges[index];

                _edges[index] = _edges[parent];
                _edges[parent] = temp;

                index = parent;
                parent = (index - 1) / 2;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int smallest = index;

                if (leftChild < Count && _edges[leftChild].edgeError < _edges[smallest].edgeError)
                    smallest = leftChild;

                if (rightChild < Count && _edges[rightChild].edgeError < _edges[smallest].edgeError)
                    smallest = rightChild;

                if (smallest == index)
                    break;

                EdgeRef temp = _edges[index];

                _edges[index] = _edges[smallest];
                _edges[smallest] = temp;

                index = smallest;
            }
        }
    }


    public struct AdjacencyNode
    {
        public int triangleIndex;
        public int nextNodeIndex;

        public AdjacencyNode(int triangleIndex, int nextNodeIndex = -1)
        {
            this.triangleIndex = triangleIndex;
            this.nextNodeIndex = nextNodeIndex;
        }
    }

    public struct VertexToTriangles
    {
        private NativeList<AdjacencyNode> _nodes;
        private NativeList<int> _resultBuffer;
        private NativeList<int> _tempUniqTriangles;


        public void Initialize()
        {
            _nodes = new NativeList<AdjacencyNode>(64, Allocator.Persistent);
            _resultBuffer = new NativeList<int>(32, Allocator.Persistent);
            _tempUniqTriangles = new NativeList<int>(32, Allocator.Persistent);
        }

        public void CreateAdjacencies(NativeArray<Vertex> vertices, NativeArray<Triangle> triangles)
        {
            for (int i = 0; i < vertices.Length; i++)
                _nodes.Add(new AdjacencyNode(-1, -1));

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle triangle = triangles[i];

                if (triangle.IsDegenerated())
                    continue;

                AppendTriangle(triangle.v0, i);
                AppendTriangle(triangle.v1, i);
                AppendTriangle(triangle.v2, i);
            }
        }

        public void MergeAdjacency(int survivedVertex, int deletedVertex)
        {
            _tempUniqTriangles.Clear();

            int nodeIndex = survivedVertex;
            int lastSurvivedNodeIndex = survivedVertex;

            while (nodeIndex >= 0)
            {
                AdjacencyNode node = _nodes[nodeIndex];

                if (node.triangleIndex >= 0)
                    _tempUniqTriangles.Add(node.triangleIndex);

                lastSurvivedNodeIndex = nodeIndex;
                nodeIndex = node.nextNodeIndex;
            }

            int mergedNodeIndex = deletedVertex;

            while (mergedNodeIndex >= 0)
            {
                AdjacencyNode mergedNode = _nodes[mergedNodeIndex];
                int nextStepIndex = mergedNode.nextNodeIndex;

                if (mergedNode.triangleIndex >= 0)
                {
                    bool isUniqTriangle = true;
                    int currentTriangle = mergedNode.triangleIndex;

                    for (int i = 0; i < _tempUniqTriangles.Length; i++)
                    {
                        if (_tempUniqTriangles[i] == currentTriangle)
                        {
                            isUniqTriangle = false;
                            break;
                        }
                    }

                    if (isUniqTriangle)
                    {
                        AdjacencyNode lastSurvivedNode = _nodes[lastSurvivedNodeIndex];
                        lastSurvivedNode.nextNodeIndex = mergedNodeIndex;

                        mergedNode.nextNodeIndex = -1;

                        _nodes[lastSurvivedNodeIndex] = lastSurvivedNode;
                        _nodes[mergedNodeIndex] = mergedNode;

                        lastSurvivedNodeIndex = mergedNodeIndex;

                        _tempUniqTriangles.Add(currentTriangle);
                    }
                }

                mergedNodeIndex = nextStepIndex;
            }
        }

        public NativeList<int> GetTriangles(int vertexIndex)
        {
            _resultBuffer.Clear();

            int nodeIndex = vertexIndex;

            while (nodeIndex >= 0)
            {
                AdjacencyNode node = _nodes[nodeIndex];

                if (node.triangleIndex >= 0)
                    _resultBuffer.Add(node.triangleIndex);

                nodeIndex = node.nextNodeIndex;
            }

            return _resultBuffer;
        }

        public NativeList<int> GetSharedTriangles(int vertex1Index, int vertex2Index)
        {
            _resultBuffer.Clear();

            int nodeIndex1 = vertex1Index;

            while (nodeIndex1 >= 0)
            {
                AdjacencyNode node1 = _nodes[nodeIndex1];

                if (node1.triangleIndex >= 0)
                {
                    int nodeIndex2 = vertex2Index;

                    while (nodeIndex2 >= 0)
                    {
                        AdjacencyNode node2 = _nodes[nodeIndex2];

                        if (node2.triangleIndex == node1.triangleIndex)
                        {
                            _resultBuffer.Add(node1.triangleIndex);
                            break;
                        }

                        nodeIndex2 = node2.nextNodeIndex;
                    }
                }

                nodeIndex1 = node1.nextNodeIndex;
            }

            return _resultBuffer;
        }

        public void Dispose()
        {
            if (_nodes.IsCreated)
                _nodes.Dispose();

            if (_resultBuffer.IsCreated)
                _resultBuffer.Dispose();

            if (_tempUniqTriangles.IsCreated)
                _tempUniqTriangles.Dispose();
        }


        private void AppendTriangle(int vertexIndex, int triangleIndex)
        {
            int nodeIndex = vertexIndex;

            if (_nodes[nodeIndex].triangleIndex == -1)
            {
                AdjacencyNode rootNode = _nodes[nodeIndex];

                rootNode.triangleIndex = triangleIndex;

                _nodes[nodeIndex] = rootNode;

                return;
            }

            AdjacencyNode newNode = new AdjacencyNode(triangleIndex);
            newNode.nextNodeIndex = _nodes[vertexIndex].nextNodeIndex;

            _nodes.Add(newNode);

            AdjacencyNode shiftedNode = _nodes[vertexIndex];
            shiftedNode.nextNodeIndex = _nodes.Length - 1;

            _nodes[vertexIndex] = shiftedNode;
        }
    }
}
