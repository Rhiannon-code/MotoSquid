using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public struct BoneWeightsInterpolator
    {
        private struct BoneAccumulator
        {
            public int index;
            public float weight;
        }

        private NativeArray<BoneWeight> _originalBones;
        private NativeArray<BoneWeight> _masterBones;


        public void InitializeInMainThread(NativeArray<BoneWeight> originalBones)
        {
            _originalBones = originalBones;
            _masterBones = new NativeArray<BoneWeight>(originalBones.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Initialize(ref MeshDecimator.InternalData internalData)
        {
            NativeArray<int> verticesRemap = internalData.verticesRemap;

            for (int i = 0; i < _originalBones.Length; i++)
            {
                int master = verticesRemap[i];

                _masterBones[master] = _originalBones[i];
            }
        }

        public void Interpolate(ref MeshDecimator.InternalData internalData, ref Edge edge, int survivedIndex, int deletedIndex)
        {
            double3 p1 = internalData.vertices[survivedIndex].position;
            double3 p2 = internalData.vertices[deletedIndex].position;
            double3 opt = edge.optimalPosition;

            double d1 = math.dot(opt - p1, opt - p1);
            double d2 = math.dot(opt - p2, opt - p2);

            float weight2 = (d1 + d2) < 1e-8 ? 0.5f : (float)(d1 / (d1 + d2));
            float weight1 = 1f - weight2;

            BoneWeight b1 = _masterBones[survivedIndex];
            BoneWeight b2 = _masterBones[deletedIndex];

            _masterBones[survivedIndex] = BlendBones(b1, b2, weight1, weight2);
        }

        public void OutputInterpolatedData(NativeSlice<BoneWeight> output, ref MeshDecimator.InternalData internalData, NativeArray<int> oldToNewVMap)
        {
            NativeArray<int> verticesRemap = internalData.verticesRemap;
            Aliases vertexAliases = internalData.vertexAliases;

            for (int i = 0; i < oldToNewVMap.Length; i++)
            {
                if (oldToNewVMap[i] < 0)
                    continue;

                int newIdx = oldToNewVMap[i];
                int rootAlias = vertexAliases.GetAlias(verticesRemap[i]);

                output[newIdx] = _masterBones[rootAlias];
            }
        }

        public void Dispose()
        {
            if (_originalBones.IsCreated)
                _originalBones.Dispose();

            if (_masterBones.IsCreated)
                _masterBones.Dispose();
        }


        private BoneWeight BlendBones(BoneWeight b1, BoneWeight b2, float w1, float w2)
        {
            Span<BoneAccumulator> bones = stackalloc BoneAccumulator[8];
            int count = 0;

            AddBone(bones, ref count, b1.boneIndex0, b1.weight0 * w1);
            AddBone(bones, ref count, b1.boneIndex1, b1.weight1 * w1);
            AddBone(bones, ref count, b1.boneIndex2, b1.weight2 * w1);
            AddBone(bones, ref count, b1.boneIndex3, b1.weight3 * w1);

            AddBone(bones, ref count, b2.boneIndex0, b2.weight0 * w2);
            AddBone(bones, ref count, b2.boneIndex1, b2.weight1 * w2);
            AddBone(bones, ref count, b2.boneIndex2, b2.weight2 * w2);
            AddBone(bones, ref count, b2.boneIndex3, b2.weight3 * w2);

            for (int i = 0; i < math.min(4, count); i++)
            {
                for (int j = count - 1; j > i; j--)
                {
                    if (bones[j].weight > bones[j - 1].weight)
                    {
                        BoneAccumulator temp = bones[j];
                        bones[j] = bones[j - 1];
                        bones[j - 1] = temp;
                    }
                }
            }

            float totalWeight = 0f;
            int maxBones = math.min(4, count);

            for (int i = 0; i < maxBones; i++)
                totalWeight += bones[i].weight;

            BoneWeight result = new BoneWeight();

            if (totalWeight > 0.0001f)
            {
                float invTotal = 1f / totalWeight;

                if (maxBones > 0) { result.boneIndex0 = bones[0].index; result.weight0 = bones[0].weight * invTotal; }
                if (maxBones > 1) { result.boneIndex1 = bones[1].index; result.weight1 = bones[1].weight * invTotal; }
                if (maxBones > 2) { result.boneIndex2 = bones[2].index; result.weight2 = bones[2].weight * invTotal; }
                if (maxBones > 3) { result.boneIndex3 = bones[3].index; result.weight3 = bones[3].weight * invTotal; }
            }

            return result;
        }

        private void AddBone(Span<BoneAccumulator> bones, ref int count, int index, float weight)
        {
            if (weight <= 0.0001f)
                return;

            for (int i = 0; i < count; i++)
            {
                BoneAccumulator bone = bones[i];

                if (bone.index == index)
                {
                    bone.weight += weight;
                    bones[i] = bone;
                    return;
                }
            }

            bones[count] = new BoneAccumulator { index = index, weight = weight };
            count++;
        }
    }
}
