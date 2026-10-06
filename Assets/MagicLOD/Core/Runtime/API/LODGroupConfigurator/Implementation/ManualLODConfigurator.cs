using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.MagicLOD.Runtime
{
    [Serializable]
    public class ManualLODConfigurator : ILODGroupConfigurator
    {
        public int LODsCount
        {
            get
            {
                return _quality?.Count ?? 0;
            }
        }
        public LODFadeMode FadeMode
        {
            get
            {
                return _fadeMode;
            }
        }
        public IReadOnlyList<float> Quality
        {
            get
            {
                return _quality;
            }
        }
        public IReadOnlyList<float> TransitionHeights
        {
            get
            {
                return _transitionHeights;
            }
        }

        [SerializeField]
        private LODFadeMode _fadeMode;

        [SerializeField]
        private List<float> _quality;

        [SerializeField]
        private List<float> _transitionHeights;


        public ManualLODConfigurator()
        {
            _fadeMode = LODFadeMode.None;

            InitializeIfNeeded();
        }

        public ManualLODConfigurator(List<float> quality, List<float> transitions, LODFadeMode fadeMode = LODFadeMode.None)
        {
            if (quality == null || quality.Count == 0 || transitions == null || transitions.Count == 0 || quality.Count != transitions.Count)
            {
                InitializeIfNeeded();
                return;
            }

            _fadeMode = fadeMode;
            _quality = quality;
            _transitionHeights = transitions;
        }   

        public void SetLODsCount(int newLodCount)
        {
            InitializeIfNeeded();

            newLodCount = Mathf.Clamp(newLodCount, 1, 7);

            if (newLodCount == LODsCount)
                return;

            if (newLodCount > LODsCount)
            {
                int diff = newLodCount - LODsCount;
                for (int i = 0; i < diff; i++)
                {
                    _quality.Add(_quality[_quality.Count - 1]);
                    _transitionHeights.Add(_transitionHeights[_transitionHeights.Count - 1]);
                }
            }
            else
            {
                int start = newLodCount;
                int count = LODsCount - newLodCount;

                _quality.RemoveRange(start, count);
                _transitionHeights.RemoveRange(start, count);
            }
        }

        public void SetFadeMode(LODFadeMode fadeMode)
        {
            _fadeMode = fadeMode;
        }

        public void SetQuality(int lodIndex, float quality)
        {
            if (lodIndex < 0 || lodIndex >= LODsCount)
            {
                Debug.Log($"ManualLODConfigurator::SetQuality() lodIndex({lodIndex}) was greater or equal lodCount({LODsCount})");
                return;
            }

            quality = Mathf.Clamp(quality, 0.000001f, 1f);

            if (lodIndex > 0 && quality > _quality[lodIndex - 1])
            {
                Debug.Log($"ManualLODConfigurator::SetQuality() quality({quality}) can't be greater then previous level quality({_quality[lodIndex - 1]})");
                return;
            }

            if (lodIndex < LODsCount - 1 && quality < _quality[lodIndex + 1])
            {
                Debug.Log($"ManualLODConfigurator::SetQuality() quality({quality}) can't be less then nex level quality({_quality[lodIndex + 1]})");
                return;
            }
            
            _quality[lodIndex] = quality;
        }

        public void SetTransitionHeight(int lodIndex, float transitionHeight)
        {
            if (lodIndex < 0 || lodIndex >= LODsCount)
            {
                Debug.Log($"ManualLODConfigurator::SetTransitionHeight() lodIndex({lodIndex}) was greater or equal lodCount({LODsCount})");
                return;
            }

            transitionHeight = Mathf.Clamp(transitionHeight, 0f, 1f);

            if (lodIndex > 0 && transitionHeight > _transitionHeights[lodIndex - 1])
            {
                Debug.Log($"ManualLODConfigurator::SetTransitionHeight() transition({transitionHeight}) can't be greater then previous level transitionHeight({_transitionHeights[lodIndex - 1]})");
                return;
            }

            if (lodIndex < LODsCount - 1 && transitionHeight < _transitionHeights[lodIndex + 1])
            {
                Debug.Log($"ManualLODConfigurator::SetTransitionHeight() transition({transitionHeight}) can't be less then next level transitionHeight({_transitionHeights[lodIndex + 1]})");
                return;
            }

            _transitionHeights[lodIndex] = transitionHeight;
        }

        public LODGroupDescriptor CreateLODGroupDescriptor(IReadOnlyList<Mesh> meshes, IReadOnlyList<Renderer> renderers)
        {
            if (meshes == null)
            {
                return new LODGroupDescriptor()
                {
                    fadeMode = LODFadeMode.None,
                    quality = Array.Empty<float[]>(),
                    transitionHeight = new float[] { 0.01f }
                };
            }

            InitializeIfNeeded();

            LODGroupDescriptor descriptor = new LODGroupDescriptor();

            descriptor.fadeMode = _fadeMode;
            descriptor.quality = new float[meshes.Count][];
            descriptor.transitionHeight = _transitionHeights.ToArray();

            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                descriptor.quality[meshIndex] = new float[LODsCount];

                for (int lodIndex = 0; lodIndex < LODsCount; lodIndex++)
                {
                    descriptor.quality[meshIndex][lodIndex] = _quality[lodIndex];
                }
            }

            return descriptor;
        }

        public ILODGroupConfigurator Clone()
        {
            InitializeIfNeeded();

            ManualLODConfigurator clone = new ManualLODConfigurator();

            clone._quality.Clear();
            clone._transitionHeights.Clear();

            clone._fadeMode = _fadeMode;
            clone._quality.AddRange(_quality);
            clone._transitionHeights.AddRange(_transitionHeights);

            return clone;
        }


        private void InitializeIfNeeded()
        {
            if (_quality == null || _quality.Count == 0 || _transitionHeights == null || _quality.Count != _transitionHeights.Count)
            {
                _quality = new List<float>(4) { 1f };
                _transitionHeights = new List<float>(4) { 0.01f };
            }
        }
    }
}

