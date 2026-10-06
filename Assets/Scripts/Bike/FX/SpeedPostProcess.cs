// SpeedPostProcess.cs

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace MotoSquid.Bike
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/Speed Effect")]
    public sealed class SpeedPostProcess : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        // Master
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        // Radial blur
        [Header("Radial Blur")]
        public ClampedFloatParameter radialBlurStrength = new ClampedFloatParameter(0.05f, 0f,   0.4f);
        public ClampedIntParameter   blurSamples        = new ClampedIntParameter  (12,    2,    16);
        public ClampedFloatParameter blurCentreRadius   = new ClampedFloatParameter(0.35f, 0f,   1f);

        // Depth masked blur
        // Blur strength scales with 1/distance: nearest road streaks most, far road sharp.
        [Header("Depth-Masked Blur")]
        public MinFloatParameter blurDepthStart = new MinFloatParameter(4f,  0f);
        public MinFloatParameter blurDepthEnd   = new MinFloatParameter(60f, 0f);

        // Vignette
        [Header("Vignette")]
        public ClampedFloatParameter vignetteRadius   = new ClampedFloatParameter(0.6f,  0f,    1f);
        public ClampedFloatParameter vignetteSoftness = new ClampedFloatParameter(0.4f,  0.01f, 1f);
        public ClampedFloatParameter vignettePulse    = new ClampedFloatParameter(0f,    0f,    1f);
        public ClampedFloatParameter vignetteDarkness = new ClampedFloatParameter(0.55f, 0f,   1f);

        // Speed lines
        [Header("Speed Lines")]
        // Literal opacity of the line layer at full speed.
        public ClampedFloatParameter lineIntensity    = new ClampedFloatParameter(0.1f,  0f,   1f);
        public ClampedFloatParameter lineCount        = new ClampedFloatParameter(80f,   20f,  200f);
        public ClampedFloatParameter lineSharpness    = new ClampedFloatParameter(18f,   1f,   40f);
        public ClampedFloatParameter lineMaskRadius   = new ClampedFloatParameter(0.75f, 0f,   1.0f);
        public ClampedFloatParameter lineFadeRadius   = new ClampedFloatParameter(0.35f, 0.1f, 1.5f);
        public ClampedFloatParameter lineFlickerSpeed = new ClampedFloatParameter(18f,   0f,   60f);
        public ClampedFloatParameter lineMaxLength    = new ClampedFloatParameter(0.6f,  0.1f, 2f);
        public ColorParameter        lineTint         = new ColorParameter(Color.white, false, false, true);

        // Barrel distortion
        [Header("Barrel Distortion")]
        public ClampedFloatParameter barrelStrength = new ClampedFloatParameter(0.5f,  0f,   2f);
        public ClampedFloatParameter barrelPulse    = new ClampedFloatParameter(0f,    0f,   1f);
        public ClampedFloatParameter barrelEdgeBias = new ClampedFloatParameter(1.4f,  0.1f, 3f);


        // Anamorphic lens flares
        // Near miss directional blur
        [Header("Near Miss")]
        public ClampedFloatParameter nearMissBlurStrength = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter nearMissBlurSide     = new ClampedFloatParameter(1f, -1f, 1f);

        [Header("Anamorphic Lens Flares")]
        public ClampedFloatParameter flareIntensity  = new ClampedFloatParameter(0f,    0f, 1f);
        public ClampedFloatParameter flareThreshold  = new ClampedFloatParameter(0.92f, 0f, 1f);

        // Colour grade at speed
        [Header("Speed Colour Grade")]
        public ClampedFloatParameter saturationEdge   = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ClampedFloatParameter saturationCentre = new ClampedFloatParameter(1.3f, 1f, 2f);
        // Scales the grade past its authored ceiling on boost over-speed: 0 = none, 1 = double.
        public ClampedFloatParameter overSpeedGradeBleed = new ClampedFloatParameter(0.5f, 0f, 1f);

        // Runtime driven intensities (set by BikeSpeedFX, not tuned in Inspector)
        [HideInInspector] public ClampedFloatParameter blurIntensity          = new ClampedFloatParameter(0f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter speedLinesIntensity    = new ClampedFloatParameter(0f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter vignetteIntensity      = new ClampedFloatParameter(0f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter overSpeedIntensity     = new ClampedFloatParameter(0f, 0f, 1f);

        // Internals
        Material m_Material;

        public bool IsActive()
        {
            return m_Material != null &&
                (intensity.value > 0f || blurIntensity.value > 0f ||
                 speedLinesIntensity.value > 0f || vignetteIntensity.value > 0f);
        }

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            if (Shader.Find("Hidden/SpeedEffect") != null)
                m_Material = new Material(Shader.Find("Hidden/SpeedEffect"));
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null) return;

            // Depth is read straight from HDRP's own _CameraDepthTexture via LoadCameraDepth()
            // in the shader, so there's nothing to bind here. (The old _DepthTexture path copied
            // from a legacy C#-side global that HDRP never populates, which silently disabled the
            // depth-masked road blur.)

            m_Material.SetFloat("_Intensity",          intensity.value);
            m_Material.SetFloat("_RadialBlurStrength",  radialBlurStrength.value);
            m_Material.SetInt  ("_BlurSamples",         blurSamples.value);
            m_Material.SetFloat("_BlurCentreRadius",    blurCentreRadius.value);
            m_Material.SetFloat("_VignetteRadius",      vignetteRadius.value);
            m_Material.SetFloat("_VignetteSoftness",    vignetteSoftness.value);
            m_Material.SetFloat("_VignetteDarkness",    vignetteDarkness.value);
            m_Material.SetFloat("_VignettePulse",       vignettePulse.value);
            m_Material.SetFloat("_BlurDepthStart",      blurDepthStart.value);
            m_Material.SetFloat("_BlurDepthEnd",        blurDepthEnd.value);
            m_Material.SetFloat("_LineIntensity",       lineIntensity.value);
            m_Material.SetFloat("_LineCount",           lineCount.value);
            m_Material.SetFloat("_LineSharpness",       lineSharpness.value);
            m_Material.SetFloat("_LineMaskRadius",      lineMaskRadius.value);
            m_Material.SetFloat("_LineFadeRadius",      lineFadeRadius.value);
            m_Material.SetFloat("_LineFlickerSpeed",    lineFlickerSpeed.value);
            m_Material.SetFloat("_LineMaxLength",       lineMaxLength.value);
            m_Material.SetColor("_LineTint",            lineTint.value);
            m_Material.SetFloat("_BarrelStrength",      barrelStrength.value * intensity.value);
            m_Material.SetFloat("_BarrelPulse",         barrelPulse.value);
            m_Material.SetFloat("_BarrelEdgeBias",      barrelEdgeBias.value);
            m_Material.SetFloat("_NearMissBlurStrength", nearMissBlurStrength.value);
            m_Material.SetFloat("_NearMissBlurSide",    nearMissBlurSide.value);
            m_Material.SetFloat("_BlurIntensity",          blurIntensity.value);
            m_Material.SetFloat("_SpeedLinesIntensity",    speedLinesIntensity.value);
            m_Material.SetFloat("_VignetteIntensity",      vignetteIntensity.value);
            m_Material.SetFloat("_OverSpeedIntensity",     overSpeedIntensity.value);
            m_Material.SetFloat("_OverSpeedGradeBleed",    overSpeedGradeBleed.value);
            m_Material.SetFloat("_FlarIntensity",       flareIntensity.value);
            m_Material.SetFloat("_FlareThreshold",      flareThreshold.value);
            m_Material.SetFloat("_SaturationEdge",      saturationEdge.value);
            m_Material.SetFloat("_SaturationCentre",    saturationCentre.value);

            m_Material.SetTexture("_InputTexture", source);
            HDUtils.DrawFullScreen(cmd, m_Material, destination);
        }

        public override void Cleanup() => CoreUtils.Destroy(m_Material);
    }
}
