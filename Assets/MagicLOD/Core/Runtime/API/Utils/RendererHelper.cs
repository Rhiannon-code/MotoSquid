using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public static class RendererHelper
    {
        public static void CopySettings(this Renderer to, Renderer from)
        {
            if (to == null || from == null)
                return;

            to.sharedMaterials = from.sharedMaterials;

            to.shadowCastingMode = from.shadowCastingMode;
            to.receiveShadows = from.receiveShadows;

            to.lightmapIndex = from.lightmapIndex;
            to.lightmapScaleOffset = from.lightmapScaleOffset;
            to.realtimeLightmapIndex = from.realtimeLightmapIndex;
            to.realtimeLightmapScaleOffset = from.realtimeLightmapScaleOffset;

            to.lightProbeUsage = from.lightProbeUsage;
            to.lightProbeProxyVolumeOverride = from.lightProbeProxyVolumeOverride;
            to.reflectionProbeUsage = from.reflectionProbeUsage;
            to.probeAnchor = from.probeAnchor;

            to.motionVectorGenerationMode = from.motionVectorGenerationMode;
            to.renderingLayerMask = from.renderingLayerMask;
            to.rendererPriority = from.rendererPriority;
            to.allowOcclusionWhenDynamic = from.allowOcclusionWhenDynamic;

            to.sortingLayerID = from.sortingLayerID;
            to.sortingOrder = from.sortingOrder;

            if (to is SkinnedMeshRenderer toSkinned && from is SkinnedMeshRenderer fromSkinned)
            {
                toSkinned.rootBone = fromSkinned.rootBone;
                toSkinned.bones = fromSkinned.bones;
                toSkinned.quality = fromSkinned.quality;
                toSkinned.updateWhenOffscreen = fromSkinned.updateWhenOffscreen;
                toSkinned.skinnedMotionVectors = fromSkinned.skinnedMotionVectors;
                toSkinned.localBounds = fromSkinned.localBounds;
            } 
        }

        public static bool IsPartOfLODGroup(this Renderer renderer)
        {
            LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();

            if (lodGroup == null)
                return false;

            foreach (var lod in lodGroup.GetLODs())
            {
                foreach (var other in lod.renderers)
                {
                    if (renderer == other)
                        return true;
                }
            }

            return false;
        }
    }
}