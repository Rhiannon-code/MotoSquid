Shader "Hidden/SpeedEffect"
{
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    // ── Parameters ───────────────────────────────────────────────────────────────
    TEXTURE2D_X(_InputTexture);

    // Split screen cameras render into part of an RTHandle sized for the largest camera, so raw 0-1 UVs squash the image
    float3 SampleInput(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv)).rgb;
    }

    float  _Intensity;
    float  _RadialBlurStrength;
    int    _BlurSamples;
    float  _VignetteRadius;
    float  _VignetteSoftness;
    float  _VignetteDarkness;   // how dark the edge gets at full speed; over-speed drives it to black
    float  _BlurDepthStart;
    float  _BlurDepthEnd;
    float  _LineIntensity;
    float  _LineCount;
    float  _LineSharpness;
    float  _LineMaskRadius;
    float  _LineFadeRadius;
    float  _LineFlickerSpeed;
    float  _LineMaxLength;   // max streak length in radius units
    float4 _LineTint;
    float  _BarrelStrength;
    float  _BarrelPulse;
    float  _BarrelEdgeBias;
    float  _NearMissBlurStrength;
    float  _NearMissBlurSide;
    float  _BlurCentreRadius;   // normalized radius (0-1) kept sharp; 0 = no exclusion
    float  _BlurIntensity;          // motion blur gate, 0 below blurStartSpeed
    float  _SpeedLinesIntensity;    // lines gate, 0 below linesStartSpeed
    float  _VignetteIntensity;      // vignette gate, ramps in with the speed lines
    float  _OverSpeedIntensity;     // boost past the normal top speed; deepens the vignette

    // New effects
    float  _FlarIntensity;      // anamorphic flare strength
    float  _FlareThreshold;     // luminance threshold for flare source pixels
    float  _SaturationEdge;     // 0=full sat, 1=fully desaturated at edges
    float  _SaturationCentre;   // extra saturation boost at centre (1=normal, 1.3=boosted)
    float  _OverSpeedGradeBleed;// how far over-speed pushes the grade past its authored ceiling
    float  _VignettePulse;      // extra vignette on acceleration, driven from C#

    // ── Helpers ──────────────────────────────────────────────────────────────────

    float Hash(float n)
    {
        return frac(sin(n) * 43758.5453123);
    }

    float PerceivedLum(float3 c)
    {
        return dot(c, float3(0.2126, 0.7152, 0.0722));
    }

    float2 SafeNormalize2(float2 v)
    {
        return v / max(length(v), 0.0001);
    }

    // ── Depth ────────────────────────────────────────────────────────────────────
    // HDRP binds its camera depth pyramid as _CameraDepthTexture for custom post-process
    // passes; LoadCameraDepth() reads it at full resolution. (The previous _DepthTexture
    // path never worked on HDRP, it copied from a legacy C#-side global that HDRP leaves
    // empty, so the depth mask collapsed and the road blur was effectively disabled.)
    float SampleLinearDepth(float2 uv)
    {
        float raw = LoadCameraDepth(uint2(clamp(uv, 0.0, 1.0) * _ScreenSize.xy));
        // Reversed-Z: the sky / far plane reads as 0, treat it as "infinitely far".
        if (raw <= 0.0) return 1e5;
        return LinearEyeDepth(raw, _ZBufferParams);
    }

    // ── Barrel distortion ─────────────────────────────────────────────────────────
    // Uses tanh-based warp instead of polynomial so corners are smoothly clamped,
    // never producing the extreme stretching that power-based formulas cause.
    float2 BarrelDistort(float2 uv)
    {
        float2 c   = uv - 0.5;
        float  asp = _ScreenSize.x / _ScreenSize.y;
        float2 ac  = float2(c.x * asp, c.y);
        float  r   = length(ac);
        float  warpMag = (_BarrelStrength + _BarrelPulse) * _BarrelEdgeBias;
        // tanh keeps the warp bounded, no matter how large r gets, output is finite
        float  warpedR = (r < 0.0001) ? r : (tanh(r * warpMag) / max(warpMag, 0.0001));
        float2 dir     = SafeNormalize2(ac);
        float2 warped  = float2(dir.x / asp, dir.y) * warpedR;
        return clamp(warped + 0.5, 0.001, 0.999);
    }

    // ── Speed lines (raw UV) ──────────────────────────────────────────────────────
    float SpeedLines(float2 uv, float intensity)
    {
        if (intensity <= 0.0) return 0.0;

        float2 c  = uv - 0.5;
        c.x      *= _ScreenSize.x / _ScreenSize.y;
        float radius = max(abs(c.x), abs(c.y));
        float angle  = atan2(c.y, c.x);

        float fps       = max(_LineFlickerSpeed, 0.01);
        float timeFrame = floor(_Time.y * fps) / fps;
        float normAngle = (angle / TWO_PI) + 0.5;
        float spokeBin  = floor(normAngle * _LineCount);
        float spokeT    = frac(normAngle  * _LineCount);

        float spokeRand  = Hash(spokeBin + floor(timeFrame * 0.5) * 137.0);
        float spokeWidth = lerp(0.3, 1.0, spokeRand);
        float spokeOn    = step(0.25, Hash(spokeBin * 7.3 + timeFrame));

        float halfW          = 0.5 * spokeWidth;
        float distFromCentre = abs(spokeT - 0.5);
        float lineMask       = 1.0 - smoothstep(halfW - 1.0 / max(_LineSharpness, 0.01),
                                                 halfW + 1.0 / max(_LineSharpness, 0.01),
                                                 distFromCentre);
        lineMask *= spokeOn;

        // Edge-bleed: lines appear only near screen edges, bleeding inward.
        // Non-aspect-corrected box distance gives equal coverage on all four edges.
        float2 cEdge    = abs(uv - 0.5) * 2.0;  // 0 = centre, 1 = screen edge
        float  edgeDist = max(cEdge.x, cEdge.y);

        // Per-spoke length variation: each spoke has a slightly different inner cutoff
        // so the bleed looks organic rather than a uniform ring.
        float maxLen   = lerp(_LineMaxLength * 0.3, _LineMaxLength, Hash(spokeBin * 3.7));
        float innerBnd = _LineMaskRadius - maxLen;
        float edgeMask = smoothstep(innerBnd - 0.03, innerBnd + 0.03, edgeDist);

        // Radial fade, lines are nearly invisible near screen centre, full opacity at edges
        float circDist   = length((uv - 0.5) * 2.0);
        float radialFade = smoothstep(0.25, 1.1, circDist);

        return lineMask * edgeMask * radialFade * intensity;
    }


    // ── Anamorphic lens flares ────────────────────────────────────────────────────
    // Horizontal streaks from bright point sources, mimics anamorphic cinema lenses.
    float3 AnamorphicFlares(float2 uv, float intensity)
    {
        if (intensity <= 0.001) return float3(0, 0, 0);

        // Only pixels that are genuinely very bright generate a flare.
        // Sample the source pixel first, if it's not bright enough, early out.
        // This prevents every lit window in the scene spawning a streak.
        float3 srcCol = SampleInput(uv);
        float  srcLum = PerceivedLum(srcCol);

        // Hard gate, no flare below threshold. Smooth transition above it.
        float  srcBright = smoothstep(_FlareThreshold, _FlareThreshold + 0.05, srcLum);
        if (srcBright <= 0.001) return float3(0, 0, 0);

        int    steps = 16;
        float  rcp_s = 1.0 / (float)steps;
        float3 streak = float3(0, 0, 0);
        float  totalW = 0.0;

        [loop] for (int i = 1; i <= steps; i++)
        {
            float  t   = (float)i * rcp_s;
            float  w   = pow(max(1.0 - t, 0.0), 2.5);
            float  off = t * 0.1;

            float  uvLx = uv.x - off;
            float  uvRx = uv.x + off;

            // Fade to zero near screen edges, no edge rectangle artefact
            float  edgeFadeL = smoothstep(0.0, 0.06, uvLx);
            float  edgeFadeR = smoothstep(1.0, 0.94, uvRx);

            float2 uvL = float2(clamp(uvLx, 0.001, 0.999), uv.y);
            float2 uvR = float2(clamp(uvRx, 0.001, 0.999), uv.y);

            float3 sL  = SampleInput(uvL);
            float3 sR  = SampleInput(uvR);

            streak += (sL * edgeFadeL + sR * edgeFadeR) * w;
            totalW += (edgeFadeL + edgeFadeR) * w;
        }

        streak = (totalW > 0.001) ? streak / totalW : float3(0, 0, 0);

        // Vertical falloff relative to THIS pixel's Y, streak fades above
        // and below the source row, not relative to screen centre
        // (previously caused streaks at wrong heights across the frame)
        float  vertFade = pow(1.0 - abs(uv.y - uv.y), 1.0); // always 1 at source row
        // Encode falloff via streak thinness, use the source brightness
        // to scale the final contribution so dim windows contribute nothing
        float3 flareTint = float3(0.75, 0.88, 1.0);
        return clamp(streak * flareTint * intensity * srcBright * 0.25, 0.0, 0.25);
    }

    // ── Colour grade at speed ─────────────────────────────────────────────────────
    // Desaturates the screen edges and optionally boosts centre saturation.
    // overdrive pushes the grade PAST its authored ceiling on boost over-speed. The speed
    // master is already saturated by the time over-speed begins, so scaling the blend amount
    // there would be a no-op, the strengths themselves have to move.
    float3 SpeedColourGrade(float3 col, float2 uv, float intensity, float overdrive)
    {
        float  edgeDist = length((uv - 0.5) * 2.0);

        // Edge desaturation, grey toward screen edges
        float  edgeT = saturate(smoothstep(0.3, 1.2, edgeDist) * _SaturationEdge * overdrive * intensity);
        float  lum   = PerceivedLum(col);
        float3 grey  = float3(lum, lum, lum);
        col          = lerp(col, grey, edgeT);

        // Centre saturation boost, clamped so bright pixels don't blow out.
        // centreT is the blend weight (0 = no boost, 1 = full boost).
        // The boosted value is col + (col - grey) * (_SaturationCentre - 1),
        // then we lerp by centreT, then clamp to [0,1] to prevent HDR blowout.
        float  centreWeight = (1.0 - smoothstep(0.0, 0.6, edgeDist)) * intensity;
        float3 boosted      = col + (col - grey) * ((_SaturationCentre - 1.0) * overdrive);
        col                 = clamp(lerp(col, boosted, centreWeight), 0.0, 1.0);

        return col;
    }

    // ── Near miss directional blur ────────────────────────────────────────────────
    // Smears the screen horizontally on the side the traffic was on.
    // _NearMissBlurSide: -1 = traffic was on left, +1 = on right.
    float3 NearMissBlur(float2 uv, float3 baseCol)
    {
        if (_NearMissBlurStrength <= 0.001) return baseCol;

        // Only affect the side the vehicle passed on
        float2 centred   = uv - 0.5;
        float  sideMask  = smoothstep(0.0, 0.35, centred.x * _NearMissBlurSide);

        // Horizontal smear, samples toward the edge on the near-miss side
        int    steps     = 8;
        float  maxOffset = 0.06 * _NearMissBlurStrength;
        float3 blurred   = float3(0, 0, 0);
        float  wTotal    = 0;

        for (int i = 0; i < steps; i++)
        {
            float  t      = (float)i / (float)(steps - 1);
            float  w      = 1.0 - t;
            float2 sUV    = clamp(float2(uv.x + _NearMissBlurSide * maxOffset * t, uv.y),
                                  0.001, 0.999);
            blurred += SampleInput(sUV) * w;
            wTotal  += w;
        }
        blurred /= max(wTotal, 0.001);

        return lerp(baseCol, blurred, sideMask * _NearMissBlurStrength);
    }

    // ── Radial blur (depth-weighted ground streak) ────────────────────────────────
    // Emulates how the world smears in peripheral vision when you're moving fast:
    // a surface's apparent motion scales with 1/distance, so the road directly under
    // and beside the bike streaks hard, the road slightly ahead streaks less, and the
    // far road / sky stays sharp. Streaks emanate radially from the focus of expansion
    // (the point we're driving into, ~screen centre), so the periphery smears most.
    float3 RadialBlur(float2 uv)
    {
        float3 src   = SampleInput(uv);
        float  depth = SampleLinearDepth(uv);

        // Sky / distant scenery has ~zero apparent motion, never streak it (its bright
        // pixels would also bleed a glow toward the centre).
        if (depth >= 1e4) return src;

        // ── Distance weighting, the core of the realistic falloff ───────────────
        // _BlurDepthStart keeps the bike itself razor-sharp, then blur peaks on the
        // nearest road just past it and falls off toward _BlurDepthEnd (the forward
        // road, which is moving slowly enough to read crisp). Squaring biases the
        // smear strongly toward the closest ground.
        float nearKeep  = smoothstep(_BlurDepthStart, _BlurDepthStart + 3.0, depth); // 0 on bike → 1 on road
        float distFall  = saturate((_BlurDepthEnd - depth) /
                                   max(_BlurDepthEnd - _BlurDepthStart, 0.001));      // 1 near → 0 far
        float depthMask = nearKeep * distFall * distFall;

        // ── Screen-position weighting ────────────────────────────────────────────
        // Streak length grows with distance from the focus of expansion, so the road
        // to the sides and the ground at the bottom smear most while the vanishing
        // point we're aiming at stays sharp. _BlurCentreRadius is the sharp pocket,
        // keep it small so the road "slightly in front" still gets a touch of motion.
        float centreExclude = smoothstep(_BlurCentreRadius, _BlurCentreRadius + 0.25,
                                         length((uv - 0.5) * 2.0));

        int    n     = max(_BlurSamples, 2);
        float  rcp_n = 1.0 / (float)(n - 1);
        float2 dir   = (0.5 - uv) * _RadialBlurStrength * _BlurIntensity * depthMask * centreExclude;
        float3 col   = float3(0, 0, 0);
        float  w     = 0;
        [loop] for (int i = 0; i < n; i++)
        {
            float2 sUV    = clamp(uv + dir * ((float)i * rcp_n), 0.001, 0.999);
            float3 sample = SampleInput(sUV);
            // Per-sample sky rejection, don't drag distant bright pixels into the streak
            float  sDepth = SampleLinearDepth(sUV);
            float  valid  = (sDepth < 1e4) ? 1.0 : 0.0;
            col  += sample * valid;
            w    += valid;
        }
        // If every sample landed on sky, leave the pixel untouched
        return (w > 0.0) ? col / w : src;
    }

    // ── Vignette ──────────────────────────────────────────────────────────────────
    float VignetteMask(float2 uv, float extraPulse)
    {
        float dist     = length((uv - 0.5) * 2.0);
        float radius   = _VignetteRadius - extraPulse * 0.2;   // pulse tightens radius
        float softness = _VignetteSoftness * lerp(1.0, 0.5, extraPulse);
        float mask     = 1.0 - smoothstep(radius, radius + softness, dist);
        // Cruising at top speed stops at _VignetteDarkness rather than crushing the edge to
        // black; only over-speed takes it all the way down.
        return lerp(1.0, mask, lerp(_VignetteDarkness, 1.0, extraPulse));
    }

    // ── Fragment ──────────────────────────────────────────────────────────────────
    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 rawUV    = input.texcoord;
        float2 warpedUV = rawUV;

        if (_BarrelStrength + _BarrelPulse > 0.001)
            warpedUV = BarrelDistort(rawUV);

        // ── Scene colour ──────────────────────────────────────────────────────
        float3 baseCol = clamp(SampleInput(warpedUV), 0.0, 1.0);
        float3 blurCol = clamp(RadialBlur(warpedUV), 0.0, 1.0);

        // Blur blends in based on its own speed threshold, not the master intensity
        float3 speedCol = lerp(baseCol, blurCol, _BlurIntensity);

        // Barrel edge crush on gear-shift pulse only
        float edgeCrush = 1.0 - _BarrelPulse * length((rawUV - 0.5) * 2.0) * 0.4;
        speedCol       *= edgeCrush;

        float3 finalCol = clamp(lerp(baseCol, speedCol, saturate(_Intensity)), 0.0, 1.0);

        // ── Colour grade ──────────────────────────────────────────────────────
        // Clamp after so saturation boost can't push past 1
        finalCol = clamp(SpeedColourGrade(finalCol, rawUV, _Intensity,
                             1.0 + _OverSpeedIntensity * _OverSpeedGradeBleed), 0.0, 1.0);

        // ── Speed lines ───────────────────────────────────────────────────────
        // Use additive blend capped at 1 instead of screen blend
        // is multiplicative and can push mid-bright pixels to white in groups.
        float  lineVal  = SpeedLines(rawUV, _SpeedLinesIntensity * _LineIntensity);
        // Suppress lines entirely above half brightness
        float  lumMask  = 1.0 - smoothstep(0.35, 0.6, PerceivedLum(finalCol));
        finalCol        = clamp(finalCol + lineVal * _LineTint.rgb * lumMask, 0.0, 1.0);

        // ── Anamorphic lens flares ─────────────────────────────────────────────
        finalCol = clamp(finalCol + AnamorphicFlares(rawUV, _Intensity * _FlarIntensity), 0.0, 1.0);

        // ── Near miss blur ─────────────────────────────────────────────────────
        finalCol = clamp(NearMissBlur(rawUV, finalCol), 0.0, 1.0);

        // ── Vignette ──────────────────────────────────────────────────────────
        // Depth comes from the acceleration pulse and boost over-speed; presence comes from
        // the speed ramp, so the vignette fades in alongside the lines and only deepens on boost.
        float vigDeepen = saturate(_VignettePulse + _OverSpeedIntensity);
        float vigMask   = VignetteMask(warpedUV, vigDeepen);
        finalCol       *= lerp(1.0, vigMask, saturate(_VignetteIntensity + _VignettePulse));
        finalCol      = clamp(finalCol, 0.0, 1.0);


        return float4(finalCol, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "SpeedEffect"
            ZWrite Off ZTest Always Blend Off Cull Off
            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
