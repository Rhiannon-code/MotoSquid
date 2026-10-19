Shader "FullScreen/InkOutline"
{
    Properties
    {
        _InkColor        ("Ink Colour", Color) = (0.02, 0.01, 0.05, 1)
        _Opacity         ("Opacity", Range(0, 1)) = 1
        _Thickness       ("Thickness (px at 1080p)", Range(0.5, 4)) = 2
        _DepthThreshold  ("Depth Threshold (relative)", Range(0.001, 0.5)) = 0.04
        _NormalThreshold ("Normal Threshold", Range(0.01, 1)) = 0.35
        _FadeStart       ("Fade Start (m)", Float) = 60
        _FadeEnd         ("Fade End (m)", Float) = 250
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

    float4 _InkColor;
    float  _Opacity, _Thickness, _DepthThreshold, _NormalThreshold, _FadeStart, _FadeEnd;

    float EyeDepth(int2 px)
    {
        px = clamp(px, int2(0, 0), int2(_ScreenSize.xy) - 1);
        return LinearEyeDepth(LoadCameraDepth(px), _ZBufferParams);
    }

    float3 Normal(int2 px)
    {
        px = clamp(px, int2(0, 0), int2(_ScreenSize.xy) - 1);
        NormalData n;
        DecodeFromNormalBuffer(px, n);
        return n.normalWS;
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        int2 px = int2(varyings.positionCS.xy);

        float raw = LoadCameraDepth(px);
        if (raw == UNITY_RAW_FAR_CLIP_VALUE) return 0;

        float  d = LinearEyeDepth(raw, _ZBufferParams);
        float3 n = Normal(px);
        int    r = max(1, (int)round(_Thickness * _ScreenSize.y / 1080.0));

        static const int2 dirs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
        float edge = 0;
        [unroll] for (int i = 0; i < 4; i++)
        {
            int2 q = px + dirs[i] * r;
            // Only the nearer side of a depth step draws, so a silhouette line sits on the object, not the sky
            edge = max(edge, step(_DepthThreshold * d, EyeDepth(q) - d));
            edge = max(edge, step(_NormalThreshold, 1 - dot(n, Normal(q))));
        }

        float fade = 1 - saturate((d - _FadeStart) / max(1, _FadeEnd - _FadeStart));
        return float4(_InkColor.rgb, edge * fade * _Opacity);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
