using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Performance
{
    // Every performance setting the project tunes, in one place, one column per quality level.
    // MotoSquid > Performance > Apply Baseline writes it into the HDRP assets, Quality and Player
    // settings. Tune here and re-apply rather than editing those directly, or they drift apart.
    [CreateAssetMenu(menuName = "MotoSquid/Performance Profile")]
    public class PerformanceProfile : ScriptableObject
    {
        [Serializable]
        public class Tier
        {
            public string qualityLevel;

            [Header("Draw less")]
            public bool gpuResidentDrawer = true;
            public bool gpuOcclusionCulling = true;
            [Tooltip("Lower switches to cheaper LODs sooner.")]
            public float lodBias = 1f;
            public int maximumLodLevel;
            public float meshLodThreshold = 1f;

            [Header("Shadows and lights")]
            public int maxShadowsOnScreen = 32;
            public bool dynamicShadowRescale = true;
            public HDShadowFilteringQuality directionalShadowFiltering = HDShadowFilteringQuality.Medium;
            public HDShadowFilteringQuality punctualShadowFiltering = HDShadowFilteringQuality.Medium;
            public int maxPunctualLightsOnScreen = 128;

            [Header("Screen effects")]
            public bool screenSpaceReflections = true;
            public bool screenSpaceAmbientOcclusion = true;
            public bool screenSpaceGlobalIllumination;
            public bool volumetricFog = true;
            public bool lowResolutionTransparency = true;
            public int decalDrawDistance = 100;

            [Header("Lighting data")]
            public bool adaptiveProbeVolumes = true;
            public bool probeVolumeGpuStreaming = true;
            public bool probeVolumeDiskStreaming = true;

            [Header("Streaming and memory")]
            public bool textureStreaming = true;
            public int textureStreamingBudgetMB = 2048;
            public int asyncUploadTimeSliceMs = 2;
            public int asyncUploadBufferMB = 64;
        }

        public Tier[] tiers;

        [Header("Whole project")]
        public bool deferredOnly = true;
        public bool frameTimingStats = true;
    }
}
