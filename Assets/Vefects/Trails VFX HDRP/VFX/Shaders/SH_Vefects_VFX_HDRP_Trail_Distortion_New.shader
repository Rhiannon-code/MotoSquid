// Made with Amplify Shader Editor v1.9.9.12
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_VFX_HDRP_Trail_Distortion_New"
{
	Properties
	{
		_EmissionMultiply( "Emission Multiply", Float ) = 0
		_DistortionIntensity( "Distortion Intensity", Float ) = 1
		_NoiseMultiply( "Noise Multiply", Float ) = 1
		_Erosion( "Erosion", Float ) = 0
		_ErosionSmoothness( "Erosion Smoothness", Float ) = 1
		_OpacityMultiply( "Opacity Multiply", Float ) = 1
		[Space(33)][Header(Noise)][Space(13)] _NoiseTexture( "Noise Texture", 2D ) = "white" {}
		_Noise01UVS( "Noise 01 UV S", Vector ) = ( 1, 1, 0, 0 )
		_Noise01UVP( "Noise 01 UV P", Vector ) = ( 0.1, 0.3, 0, 0 )
		_DistortionLerp( "Distortion Lerp", Float ) = 1
		_Noise01Intensity( "Noise 01 Intensity", Float ) = -1
		[Space(33)][Header(Intensity Mask)][Space(13)] _IntensityMask( "Intensity Mask", 2D ) = "black" {}
		_IntensityMaskUVS( "Intensity Mask UV S", Vector ) = ( 1, 1, 0, 0 )
		_IntensityMaskPanSpeed( "Intensity Mask Pan Speed", Vector ) = ( 0, 0, 0, 0 )
		_IntensityMaskOffset( "Intensity Mask Offset", Vector ) = ( 0, 0, 0, 0 )
		_IntensityMaskPower( "Intensity Mask Power", Float ) = 1
		_IntensityMaskMultiply( "Intensity Mask Multiply", Float ) = 1
		[Space(33)][Header(Depth Fade)][Space(13)] _DepthFade( "Depth Fade", Float ) = 0
		[Space(33)][Header(Camera Depth Fade)][Space(13)] _CameraDepthFadeLength( "Camera Depth Fade Length", Float ) = 3
		_CameraDepthFadeOffset( "Camera Depth Fade Offset", Float ) = 0.1
		[Space(33)][Header(Opacity Cutout Mask)][Space(13)] _OpacityMask( "Opacity Mask", 2D ) = "white" {}
		_OpacityMaskPower( "Opacity Mask Power", Float ) = 1
		_OpacityMaskMultiply( "Opacity Mask Multiply", Float ) = 1
		[Space(33)][Header(AR)][Space(13)] _Cull( "Cull", Float ) = 0
		_Src( "Src", Float ) = 5
		_Dst( "Dst", Float ) = 10
		_ZWrite1( "ZWrite1", Float ) = 0
		_ZTest( "ZTest", Float ) = 2

		[HideInInspector] _RenderQueueType("Render Queue Type", Float) = 1
		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
		//[HideInInspector] _ShadowMatteFilter("Shadow Matte Filter", Float) = 2.006836
		[HideInInspector] _StencilRef("Stencil Ref", Int) = 0 // StencilUsage.Clear
		[HideInInspector] _StencilWriteMask("Stencil Write Mask", Int) = 3 // StencilUsage.RequiresDeferredLighting | StencilUsage.SubsurfaceScattering
		[HideInInspector] _StencilRefDepth("Stencil Ref Depth", Int) = 0 // Nothing
		[HideInInspector] _StencilWriteMaskDepth("Stencil Write Mask Depth", Int) = 8 // StencilUsage.TraceReflectionRay
		[HideInInspector] _StencilRefMV("Stencil Ref MV", Int) = 32 // StencilUsage.ObjectMotionVector
		[HideInInspector] _StencilWriteMaskMV("Stencil Write Mask MV", Int) = 32 // StencilUsage.ObjectMotionVector
		[HideInInspector] _StencilRefDistortionVec("Stencil Ref Distortion Vec", Int) = 2 // StencilUsage.DistortionVectors
		[HideInInspector] _StencilWriteMaskDistortionVec("Stencil Write Mask Distortion Vec", Int) = 2 // StencilUsage.DistortionVectors
		[HideInInspector] _StencilWriteMaskGBuffer("Stencil Write Mask GBuffer", Int) = 3 // StencilUsage.RequiresDeferredLighting | StencilUsage.SubsurfaceScattering
		[HideInInspector] _StencilRefGBuffer("Stencil Ref GBuffer", Int) = 2 // StencilUsage.RequiresDeferredLighting
		[HideInInspector] _ZTestGBuffer("ZTest GBuffer", Int) = 4
		[HideInInspector][ToggleUI] _RequireSplitLighting("Require Split Lighting", Float) = 0
		[HideInInspector][ToggleUI] _ReceivesSSR("Receives SSR", Float) = 1
		[HideInInspector] _SurfaceType("Surface Type", Float) = 1
		[HideInInspector] _BlendMode("Blend Mode", Float) = 0
		[HideInInspector] _SrcBlend("Src Blend", Float) = 1
		[HideInInspector] _DstBlend("Dst Blend", Float) = 0
		[HideInInspector] _AlphaSrcBlend("Alpha Src Blend", Float) = 1
		[HideInInspector] _AlphaDstBlend("Alpha Dst Blend", Float) = 0
		[HideInInspector][ToggleUI] _ZWrite("ZWrite", Float) = 0
		[HideInInspector][ToggleUI] _TransparentZWrite("Transparent ZWrite", Float) = 0
		[HideInInspector] _CullMode("Cull Mode", Float) = 2
		[HideInInspector] _TransparentSortPriority("Transparent Sort Priority", Float) = 0
		[HideInInspector][ToggleUI] _EnableFogOnTransparent("Enable Fog", Float) = 0
		[HideInInspector] _CullModeForward("Cull Mode Forward", Float) = 2 // This mode is dedicated to Forward to correctly handle backface then front face rendering thin transparent
		[HideInInspector][Enum(Back, 2, Front, 1)] _TransparentCullMode("_TransparentCullMode", Int) = 2// Back culling by default
		[HideInInspector] _ZTestDepthEqualForOpaque("ZTest Depth Equal For Opaque", Int) = 4 // Less equal
		[HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _ZTestTransparent("ZTest Transparent", Int) = 4// Less equal
		[HideInInspector][ToggleUI] _TransparentBackfaceEnable("Transparent Backface Enable", Float) = 0
		//[HideInInspector][ToggleUI] _AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 1
		[HideInInspector][ToggleUI] _UseShadowThreshold("Use Shadow Threshold", Float) = 1
		[HideInInspector][ToggleUI] _DoubleSidedEnable("Double Sided Enable", Float) = 0
		[HideInInspector][Enum(Default, 1, Flip, 0, Mirror, 1, None, 2)] _DoubleSidedNormalMode("Double Sided Normal Mode", Float) = 1
		[HideInInspector] _DoubleSidedConstants("DoubleSidedConstants", Vector) = (1,1,-1,0)
		[HideInInspector] _DistortionEnable("_DistortionEnable",Float) = 0
		[HideInInspector] _DistortionOnly("_DistortionOnly",Float) = 0

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector][ToggleUI] _TransparentWritingMotionVec("Transparent Writing MotionVec", Float) = 0
		[HideInInspector][Enum(Back, 2, Front, 1)] _OpaqueCullMode("_OpaqueCullMode", Int) = 2 // Back culling by default
		[HideInInspector][ToggleUI] _SupportDecals("Support Decals", Float) = 1
		[HideInInspector][ToggleUI] _ReceivesSSRTransparent("Receives SSR Transparent", Float) = 0
		[HideInInspector] _EmissionColor("Color", Color) = (1, 1, 1)
		[HideInInspector] _UnlitColorMap_MipInfo("_UnlitColorMap_MipInfo", Vector) = (0, 0, 0, 0)

		[HideInInspector][Enum(Default, 0, Auto, 0, On, 1, Off, 2)] _DoubleSidedGIMode("Double sided GI mode", Float) = 0
	}

	SubShader
	{
		PackageRequirements
		{
			"com.unity.render-pipelines.high-definition": "[17.0,18.0]"
		}

		

		

		Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Transparent" "ShaderGraphShader"="true" }

	LOD 0

		HLSLINCLUDE
		#pragma target 4.5

		#if ( UNITY_VERSION >= 60030000 )
			#pragma exclude_renderers glcore gles gles3 webgpu 
		#else
			#pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
		#endif

        #define SUPPORT_GLOBAL_MIP_BIAS 1

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
		#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

		#define ASE_ADJUST_CLIP_POSITION( x ) x

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlaneASE (float3 pos, float4 plane)
		{
			return dot (float4(pos,1.0f), plane);
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlaneASE(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlaneASE(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlaneASE(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlaneASE(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlaneASE(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward Unlit"
			Tags { "LightMode"="ForwardOnly" }

			Blend [_SrcBlend] [_DstBlend], [_AlphaSrcBlend] [_AlphaDstBlend]
			Blend 1 One OneMinusSrcAlpha
			Blend 2 One [_DstBlend2]
			Blend 3 One [_DstBlend2]
			Blend 4 One OneMinusSrcAlpha

			Cull [_CullModeForward]
			ZTest [_ZTestDepthEqualForOpaque]
			ZWrite [_ZWrite]

			ColorMask [_ColorMaskTransparentVel] 1

			Stencil
			{
				Ref [_StencilRef]
				WriteMask [_StencilWriteMask]
				Comp Always
				Pass Replace
			}


			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define shader_feature_local _DISABLE_DECALS
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_OPAQUE_TEXTURE 1


			#pragma shader_feature _SURFACE_TYPE_TRANSPARENT

			#pragma multi_compile _ DEBUG_DISPLAY
			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex Vert
			#pragma fragment Frag

	        #if (defined(_TRANSPARENT_WRITES_MOTION_VEC) || defined(_TRANSPARENT_REFRACTIVE_SORT)) && defined(_SURFACE_TYPE_TRANSPARENT)
	        #define _WRITE_TRANSPARENT_MOTION_VECTOR
	        #endif

			#define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #define SUPPORT_GLOBAL_MIP_BIAS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

			#if defined(_ENABLE_SHADOW_MATTE) && SHADERPASS == SHADERPASS_FORWARD_UNLIT
				#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
				#define HAS_LIGHTLOOP
				#define SHADOW_OPTIMIZE_REGISTER_USAGE 1

				#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowContext.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadow.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadowLoop.hlsl"
			#endif

			CBUFFER_START( UnityPerMaterial )
			float4 _OpacityMask_ST;
			float2 _IntensityMaskPanSpeed;
			float2 _IntensityMaskUVS;
			float2 _IntensityMaskOffset;
			float2 _Noise01UVP;
			float2 _Noise01UVS;
			float _Dst;
			float _DistortionLerp;
			float _OpacityMultiply;
			float _DepthFade;
			float _CameraDepthFadeOffset;
			float _CameraDepthFadeLength;
			float _OpacityMaskMultiply;
			float _OpacityMaskPower;
			float _IntensityMaskMultiply;
			float _Noise01Intensity;
			float _NoiseMultiply;
			float _DistortionIntensity;
			float _ErosionSmoothness;
			float _Erosion;
			float _Cull;
			float _ZTest;
			float _ZWrite1;
			float _Src;
			float _IntensityMaskPower;
			float _EmissionMultiply;
			float4 _EmissionColor;
			float _RenderQueueType;
			#ifdef _ADD_PRECOMPUTED_VELOCITY
			float _AddPrecomputedVelocity;
			#endif
			#ifdef _ENABLE_SHADOW_MATTE
			float _ShadowMatteFilter;
			#endif
			float _StencilRef;
			float _StencilWriteMask;
			float _StencilRefDepth;
			float _StencilWriteMaskDepth;
			float _StencilRefMV;
			float _StencilWriteMaskMV;
			float _StencilRefDistortionVec;
			float _StencilWriteMaskDistortionVec;
			float _StencilWriteMaskGBuffer;
			float _StencilRefGBuffer;
			float _ZTestGBuffer;
			float _RequireSplitLighting;
			float _ReceivesSSR;
			float _SurfaceType;
			float _BlendMode;
			float _SrcBlend;
			float _DstBlend;
			float _DstBlend2;
			float _AlphaSrcBlend;
			float _AlphaDstBlend;
			float _ZWrite;
			float _TransparentZWrite;
			float _CullMode;
			float _TransparentSortPriority;
			float _EnableFogOnTransparent;
			float _CullModeForward;
			float _TransparentCullMode;
			float _ZTestDepthEqualForOpaque;
			float _ZTestTransparent;
			float _TransparentBackfaceEnable;
			float _AlphaCutoffEnable;
			float _AlphaCutoff;
			float _AlphaCutoffShadow;
			float _UseShadowThreshold;
			float _DoubleSidedEnable;
			float _DoubleSidedNormalMode;
			float4 _DoubleSidedConstants;
			float _EnableBlendModePreserveSpecularLighting;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _IntensityMask;
			sampler2D _NoiseTexture;
			sampler2D _OpacityMask;


			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#define ASE_NEEDS_FRAG_SCREEN_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryingsMeshToPS
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_Position;
				float3 positionRWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			
			float4 ASEHDSampleSceneColor(float2 uv, float lod, float exposureMultiplier)
			{
				#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT)
				return float4( SampleCameraColor(uv, lod) * exposureMultiplier, 1.0 );
				#endif
				return float4(0.0, 0.0, 0.0, 1.0);
			}
			

			struct SurfaceDescription
			{
				float3 Color;
				float3 Emission;
				float4 ShadowTint;
				float Alpha;
				float AlphaClipThreshold;
				float AlphaClipThresholdShadow;
				float4 VTPackedFeedback;
			};

			void BuildSurfaceData(FragInputs fragInputs, SurfaceDescription surfaceDescription, float3 V, out SurfaceData surfaceData)
			{
				ZERO_INITIALIZE(SurfaceData, surfaceData);
				surfaceData.color = surfaceDescription.Color;
			}

			void GetSurfaceAndBuiltinData(SurfaceDescription surfaceDescription , FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
			{
				#ifdef LOD_FADE_CROSSFADE
                LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
                #endif

				#ifdef _ALPHATEST_ON
				DoAlphaTest ( surfaceDescription.Alpha, surfaceDescription.AlphaClipThreshold );
				#endif

				#ifdef _DEPTHOFFSET_ON
                ApplyDepthOffsetPositionInput(V, surfaceDescription.DepthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
                #endif

				BuildSurfaceData(fragInputs, surfaceDescription, V, surfaceData);

				#ifdef WRITE_NORMAL_BUFFER
				surfaceData.normalWS = fragInputs.tangentToWorld[2];
				#endif

				#if defined(_ENABLE_SHADOW_MATTE) && SHADERPASS == SHADERPASS_FORWARD_UNLIT
					HDShadowContext shadowContext = InitShadowContext();
					float shadow;
					float3 shadow3;
					posInput = GetPositionInput(fragInputs.positionSS.xy, _ScreenSize.zw, fragInputs.positionSS.z, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
					float3 normalWS = normalize(fragInputs.tangentToWorld[1]);
					uint renderingLayers = GetMeshRenderingLayerMask();
					ShadowLoopMin(shadowContext, posInput, normalWS, asuint(_ShadowMatteFilter), renderingLayers, shadow3);
					shadow = dot(shadow3, float3(1.0f/3.0f, 1.0f/3.0f, 1.0f/3.0f));

					float4 shadowColor = (1 - shadow)*surfaceDescription.ShadowTint.rgba;
					float  localAlpha  = saturate(shadowColor.a + surfaceDescription.Alpha);

					#ifdef _SURFACE_TYPE_TRANSPARENT
						surfaceData.color = lerp(shadowColor.rgb*surfaceData.color, lerp(lerp(shadowColor.rgb, surfaceData.color, 1 - surfaceDescription.ShadowTint.a), surfaceData.color, shadow), surfaceDescription.Alpha);
					#else
						surfaceData.color = lerp(lerp(shadowColor.rgb, surfaceData.color, 1 - surfaceDescription.ShadowTint.a), surfaceData.color, shadow);
					#endif
					localAlpha = ApplyBlendMode(surfaceData.color, localAlpha).a;
					surfaceDescription.Alpha = localAlpha;
				#endif

				ZERO_INITIALIZE(BuiltinData, builtinData);
				builtinData.opacity = surfaceDescription.Alpha;

				#if defined(DEBUG_DISPLAY)
					builtinData.renderingLayers = GetMeshRenderingLayerMask();
				#endif

                #ifdef _ALPHATEST_ON
                    builtinData.alphaClipTreshold = surfaceDescription.AlphaClipThreshold;
                #endif

				builtinData.emissiveColor = surfaceDescription.Emission;

				#ifdef UNITY_VIRTUAL_TEXTURING
                builtinData.vtPackedFeedback = surfaceDescription.VTPackedFeedback;
                #endif

				#ifdef _DEPTHOFFSET_ON
                builtinData.depthOffset = surfaceDescription.DepthOffset;
                #endif

                ApplyDebugToBuiltinData(builtinData);
			}

			float GetDeExposureMultiplier()
			{
			#if defined(DISABLE_UNLIT_DEEXPOSURE)
				return 1.0;
			#else
				return _DeExposureMultiplier;
			#endif
			}

			PackedVaryingsMeshToPS VertexFunction( AttributesMesh inputMesh  )
			{
				PackedVaryingsMeshToPS o;
				UNITY_SETUP_INSTANCE_ID(inputMesh);
				UNITY_TRANSFER_INSTANCE_ID(inputMesh, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float3 objectToViewPos = TransformWorldToView( TransformObjectToWorld( inputMesh.positionOS ) );
				float eyeDepth = -objectToViewPos.z;
				o.ase_texcoord2.x = eyeDepth;
				
				o.ase_texcoord1.xy = inputMesh.ase_texcoord1.xy;
				o.ase_texcoord1.zw = inputMesh.ase_texcoord.xy;
				o.ase_color = inputMesh.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.yzw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				float3 defaultVertexValue = inputMesh.positionOS.xyz;
				#else
				float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				inputMesh.positionOS.xyz = vertexValue;
				#else
				inputMesh.positionOS.xyz += vertexValue;
				#endif

				inputMesh.normalOS = inputMesh.normalOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
				// Declared so the Velocity output port surfaces on the master node; only consumed by the motion vector pass.
				float3 aseCustomVelocity = float3( 0, 0, 0 );
				#endif

				float3 positionRWS = TransformObjectToWorld(inputMesh.positionOS);
				o.positionCS = ASE_ADJUST_CLIP_POSITION( TransformWorldToHClip(positionRWS) );
				o.positionRWS = positionRWS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float3 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl Vert ( AttributesMesh v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.positionOS = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_texcoord1 = v.ase_texcoord1;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
				float3 cameraPos = 0;
				#else
				float3 cameraPos = _WorldSpaceCameraPos;
				#endif
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), cameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, GetObjectToWorldMatrix(), cameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), cameraPos, _ScreenParams, _FrustumPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			PackedVaryingsMeshToPS DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				AttributesMesh o = (AttributesMesh) 0;
				o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			PackedVaryingsMeshToPS Vert ( AttributesMesh v )
			{
				return VertexFunction( v );
			}
			#endif

            // @diogo: had to place this here due to a Unity bug
            #if ( UNITY_VERSION >= 60050001 ) && defined( UNITY_VIRTUAL_TEXTURING )
				#pragma rendertarget_format_hint MRT1 R16G16_UNorm
			#endif

			#ifdef UNITY_VIRTUAL_TEXTURING
				#define VT_BUFFER_TARGET SV_Target1
				#define EXTRA_BUFFER_TARGET SV_Target2
				#if defined(SHADER_API_PSSL)
					#pragma PSSL_target_output_format(target 1 FMT_32_ABGR)
				#endif
			#else
			#define EXTRA_BUFFER_TARGET SV_Target1
			#endif

			void Frag( PackedVaryingsMeshToPS packedInput,
						out float4 outColor : SV_Target0
						#ifdef UNITY_VIRTUAL_TEXTURING
						,out float4 outVTFeedback : VT_BUFFER_TARGET
						#endif
						#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
						, out float outputDepth : DEPTH_OFFSET_SEMANTIC
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID( packedInput );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( packedInput );

				FragInputs input;
				ZERO_INITIALIZE(FragInputs, input);
				input.tangentToWorld = k_identity3x3;
				input.positionSS = packedInput.positionCS;
				input.positionRWS = packedInput.positionRWS;

				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

				float3 PositionRWS = packedInput.positionRWS;
				float3 V = GetWorldSpaceNormalizeViewDir( packedInput.positionRWS );
				float4 ScreenPosNorm = float4( posInput.positionNDC, packedInput.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, packedInput.positionCS.z ) * packedInput.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos, _ProjectionParams.x );

				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ScreenPos );
				float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
				float2 appendResult8 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float eros67 = _Erosion;
				float2 texCoord61 = packedInput.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_65_0 = ( texCoord61 * _IntensityMaskUVS );
				float2 panner199 = ( 1.0 * _Time.y * _IntensityMaskPanSpeed + ( temp_output_65_0 + _IntensityMaskOffset ));
				float2 texCoord48 = packedInput.ase_texcoord1.zw * float2( 1,1 ) + float2( 0,0 );
				float2 panner59 = ( 1.0 * _Time.y * _Noise01UVP + ( texCoord48 * _Noise01UVS ));
				float randomOffset163 = 0.0;
				float noiseMultiply51 = _NoiseMultiply;
				float smoothstepResult82 = smoothstep( eros67 , ( eros67 + _ErosionSmoothness ) , tex2D( _IntensityMask, ( panner199 + ( tex2D( _NoiseTexture, ( panner59 + ( randomOffset163 * 0.173 ) ) ).r * ( _Noise01Intensity * noiseMultiply51 ) ) ) ).g);
				float temp_output_100_0 = saturate( ( saturate( pow( smoothstepResult82 , _IntensityMaskPower ) ) * _IntensityMaskMultiply ) );
				float2 uv_OpacityMask = packedInput.ase_texcoord1.zw * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
				float temp_output_159_0 = saturate( ( saturate( pow( tex2D( _OpacityMask, uv_OpacityMask ).g , _OpacityMaskPower ) ) * _OpacityMaskMultiply ) );
				float eyeDepth = packedInput.ase_texcoord2.x;
				float cameraDepthFade187 = (( eyeDepth -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float screenDepth111 = LinearEyeDepth(SampleCameraDepth( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth111 = saturate( ( screenDepth111 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				float temp_output_141_0 = saturate( ( saturate( ( saturate( ( saturate( ( temp_output_100_0 * temp_output_159_0 ) ) * saturate( cameraDepthFade187 ) ) ) * distanceDepth111 ) ) * _OpacityMultiply ) );
				float2 temp_cast_0 = (temp_output_141_0).xx;
				float distortionIntensity137 = _DistortionIntensity;
				float2 lerpResult33 = lerp( float2( 0,0 ) , temp_cast_0 , ( _DistortionLerp * distortionIntensity137 ));
				float4 fetchOpaqueVal9 = ASEHDSampleSceneColor(( appendResult8 + lerpResult33 ).xy, 0.0, GetInverseCurrentExposureMultiplier());
				

				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				surfaceDescription.Color = ( fetchOpaqueVal9 * GetCurrentExposureMultiplier() ).rgb;
				surfaceDescription.Emission = ( fetchOpaqueVal9 * _EmissionMultiply ).rgb;
				surfaceDescription.Alpha = packedInput.ase_color.a;

				#ifdef _ALPHATEST_ON
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;
				#endif

				#ifdef _ALPHATEST_SHADOW_ON
				surfaceDescription.AlphaClipThresholdShadow = _AlphaCutoffShadow;
				surfaceDescription.AlphaClipThresholdShadow = _UseShadowThreshold ? surfaceDescription.AlphaClipThresholdShadow : surfaceDescription.AlphaClipThreshold;
				#endif

				surfaceDescription.ShadowTint = float4( 0, 0, 0, 1 );
				float2 Distortion = float2( 0, 0 );
				float DistortionBlur = 0;

				#if defined( ASE_WRITE_DEPTH )
				posInput.deviceDepth = posInput.deviceDepth;
				#endif

				#ifdef _DEPTHOFFSET_ON
				surfaceDescription.DepthOffset = 0;
				#endif

				surfaceDescription.VTPackedFeedback = float4(1.0f,1.0f,1.0f,1.0f);
				SurfaceData surfaceData;
				BuiltinData builtinData;
				GetSurfaceAndBuiltinData(surfaceDescription, input, V, posInput, surfaceData, builtinData);

				BSDFData bsdfData = ConvertSurfaceDataToBSDFData( input.positionSS.xy, surfaceData );

				#if defined(_ENABLE_SHADOW_MATTE)
				bsdfData.color *= GetScreenSpaceAmbientOcclusion(input.positionSS.xy);
				#endif


			#ifdef DEBUG_DISPLAY
				if (_DebugLightingMode >= DEBUGLIGHTINGMODE_DIFFUSE_LIGHTING && _DebugLightingMode <= DEBUGLIGHTINGMODE_EMISSIVE_LIGHTING)
				{
					if (_DebugLightingMode != DEBUGLIGHTINGMODE_EMISSIVE_LIGHTING)
					{
						builtinData.emissiveColor = 0.0;
					}
					else
					{
						bsdfData.color = 0.0;
					}
				}
			#endif

				float4 outResult = ApplyBlendMode(bsdfData.color * GetDeExposureMultiplier() + builtinData.emissiveColor * GetCurrentExposureMultiplier(), builtinData.opacity);
				outResult = EvaluateAtmosphericScattering(posInput, V, outResult);

				#ifdef DEBUG_DISPLAY
					int bufferSize = int(_DebugViewMaterialArray[0].x);
					for (int index = 1; index <= bufferSize; index++)
					{
						int indexMaterialProperty = int(_DebugViewMaterialArray[index].x);
						if (indexMaterialProperty != 0)
						{
							float3 result = float3(1.0, 0.0, 1.0);
							bool needLinearToSRGB = false;

							GetPropertiesDataDebug(indexMaterialProperty, result, needLinearToSRGB);
							GetVaryingsDataDebug(indexMaterialProperty, input, result, needLinearToSRGB);
							GetBuiltinDataDebug(indexMaterialProperty, builtinData, posInput, result, needLinearToSRGB);
							GetSurfaceDataDebug(indexMaterialProperty, surfaceData, result, needLinearToSRGB);
							GetBSDFDataDebug(indexMaterialProperty, bsdfData, result, needLinearToSRGB);

							if (!needLinearToSRGB)
								result = SRGBToLinear(max(0, result));

							outResult = float4(result, 1.0);
						}
					}

					if (_DebugFullScreenMode == FULLSCREENDEBUGMODE_TRANSPARENCY_OVERDRAW)
					{
						float4 result = _DebugTransparencyOverdrawWeight * float4(TRANSPARENCY_OVERDRAW_COST, TRANSPARENCY_OVERDRAW_COST, TRANSPARENCY_OVERDRAW_COST, TRANSPARENCY_OVERDRAW_A);
						outResult = result;
					}
				#endif

				outColor = outResult;

				#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
				outputDepth = posInput.deviceDepth;
				#endif

				#ifdef UNITY_VIRTUAL_TEXTURING
				outVTFeedback = builtinData.vtPackedFeedback;
				#endif
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "META"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define shader_feature_local _DISABLE_DECALS
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_OPAQUE_TEXTURE 1


			#pragma shader_feature _SURFACE_TYPE_TRANSPARENT

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex Vert
			#pragma fragment Frag

			#define SHADERPASS SHADERPASS_LIGHT_TRANSPORT
            #define SCENEPICKINGPASS
            #define SUPPORT_GLOBAL_MIP_BIAS 1

			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

			#if ( UNITY_VERSION < 60010000 && defined(_ENABLE_SHADOW_MATTE) && SHADERPASS == SHADERPASS_FORWARD_UNLIT )
				#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
				#define HAS_LIGHTLOOP
				#define SHADOW_OPTIMIZE_REGISTER_USAGE 1

				#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowContext.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadow.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl"
				#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadowLoop.hlsl"
			#endif

			CBUFFER_START( UnityPerMaterial )
			float4 _OpacityMask_ST;
			float2 _IntensityMaskPanSpeed;
			float2 _IntensityMaskUVS;
			float2 _IntensityMaskOffset;
			float2 _Noise01UVP;
			float2 _Noise01UVS;
			float _Dst;
			float _DistortionLerp;
			float _OpacityMultiply;
			float _DepthFade;
			float _CameraDepthFadeOffset;
			float _CameraDepthFadeLength;
			float _OpacityMaskMultiply;
			float _OpacityMaskPower;
			float _IntensityMaskMultiply;
			float _Noise01Intensity;
			float _NoiseMultiply;
			float _DistortionIntensity;
			float _ErosionSmoothness;
			float _Erosion;
			float _Cull;
			float _ZTest;
			float _ZWrite1;
			float _Src;
			float _IntensityMaskPower;
			float _EmissionMultiply;
			float4 _EmissionColor;
			float _RenderQueueType;
			#ifdef _ADD_PRECOMPUTED_VELOCITY
			float _AddPrecomputedVelocity;
			#endif
			#ifdef _ENABLE_SHADOW_MATTE
			float _ShadowMatteFilter;
			#endif
			float _StencilRef;
			float _StencilWriteMask;
			float _StencilRefDepth;
			float _StencilWriteMaskDepth;
			float _StencilRefMV;
			float _StencilWriteMaskMV;
			float _StencilRefDistortionVec;
			float _StencilWriteMaskDistortionVec;
			float _StencilWriteMaskGBuffer;
			float _StencilRefGBuffer;
			float _ZTestGBuffer;
			float _RequireSplitLighting;
			float _ReceivesSSR;
			float _SurfaceType;
			float _BlendMode;
			float _SrcBlend;
			float _DstBlend;
			float _DstBlend2;
			float _AlphaSrcBlend;
			float _AlphaDstBlend;
			float _ZWrite;
			float _TransparentZWrite;
			float _CullMode;
			float _TransparentSortPriority;
			float _EnableFogOnTransparent;
			float _CullModeForward;
			float _TransparentCullMode;
			float _ZTestDepthEqualForOpaque;
			float _ZTestTransparent;
			float _TransparentBackfaceEnable;
			float _AlphaCutoffEnable;
			float _AlphaCutoff;
			float _AlphaCutoffShadow;
			float _UseShadowThreshold;
			float _DoubleSidedEnable;
			float _DoubleSidedNormalMode;
			float4 _DoubleSidedConstants;
			float _EnableBlendModePreserveSpecularLighting;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _IntensityMask;
			sampler2D _NoiseTexture;
			sampler2D _OpacityMask;


            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_POSITION


			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				float4 uv3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryingsMeshToPS
			{
				float4 positionCS : SV_Position;
				#ifdef EDITOR_VISUALIZATION
				float2 VizUV : TEXCOORD0;
				float4 LightCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};


			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			
			float4 ASEHDSampleSceneColor(float2 uv, float lod, float exposureMultiplier)
			{
				#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT)
				return float4( SampleCameraColor(uv, lod) * exposureMultiplier, 1.0 );
				#endif
				return float4(0.0, 0.0, 0.0, 1.0);
			}
			

			struct SurfaceDescription
			{
				float3 Color;
				float3 Emission;
				float Alpha;
				float AlphaClipThreshold;
			};

			void BuildSurfaceData( FragInputs fragInputs, SurfaceDescription surfaceDescription, float3 V, out SurfaceData surfaceData )
			{
				ZERO_INITIALIZE( SurfaceData, surfaceData );
				surfaceData.color = surfaceDescription.Color;

				#ifdef WRITE_NORMAL_BUFFER
				surfaceData.normalWS = fragInputs.tangentToWorld[2];
				#endif
			}

			void GetSurfaceAndBuiltinData( SurfaceDescription surfaceDescription, FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData )
			{
				#ifdef LOD_FADE_CROSSFADE
                LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
                #endif

				#ifdef _ALPHATEST_ON
				DoAlphaTest( surfaceDescription.Alpha, surfaceDescription.AlphaClipThreshold );
				#endif

				#ifdef _DEPTHOFFSET_ON
                ApplyDepthOffsetPositionInput(V, surfaceDescription.DepthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
                #endif

				BuildSurfaceData( fragInputs, surfaceDescription, V, surfaceData );
				ZERO_INITIALIZE( BuiltinData, builtinData );
				builtinData.opacity = surfaceDescription.Alpha;
				#if defined(DEBUG_DISPLAY)
					builtinData.renderingLayers = GetMeshRenderingLayerMask();
				#endif

				#ifdef _ALPHATEST_ON
                    builtinData.alphaClipTreshold = surfaceDescription.AlphaClipThreshold;
                #endif

				builtinData.emissiveColor = surfaceDescription.Emission;

				#ifdef _DEPTHOFFSET_ON
                builtinData.depthOffset = surfaceDescription.DepthOffset;
                #endif


                ApplyDebugToBuiltinData(builtinData);
			}

			#define SCENEPICKINGPASS
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/MetaPass.hlsl"

			PackedVaryingsMeshToPS VertexFunction( AttributesMesh inputMesh  )
			{
				PackedVaryingsMeshToPS o;
				UNITY_SETUP_INSTANCE_ID( inputMesh );
				UNITY_TRANSFER_INSTANCE_ID( inputMesh, o );

				float4 ase_positionCS = TransformWorldToHClip( TransformObjectToWorld( ( inputMesh.positionOS ).xyz ) );
				float4 screenPos = ComputeScreenPos( ase_positionCS, _ProjectionParams.x );
				o.ase_texcoord2 = screenPos;
				float3 objectToViewPos = TransformWorldToView( TransformObjectToWorld( inputMesh.positionOS ) );
				float eyeDepth = -objectToViewPos.z;
				o.ase_texcoord4.x = eyeDepth;
				
				o.ase_texcoord3.xy = inputMesh.uv1.xy;
				o.ase_texcoord3.zw = inputMesh.uv0.xy;
				o.ase_color = inputMesh.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord4.yzw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				float3 defaultVertexValue = inputMesh.positionOS.xyz;
				#else
				float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				inputMesh.positionOS.xyz = vertexValue;
				#else
				inputMesh.positionOS.xyz += vertexValue;
				#endif

				inputMesh.normalOS = inputMesh.normalOS;

			#ifdef EDITOR_VISUALIZATION
				float2 vizUV = 0;
				float4 lightCoord = 0;
				UnityEditorVizData(inputMesh.positionOS.xyz, inputMesh.uv0.xy, inputMesh.uv1.xy, inputMesh.uv2.xy, vizUV, lightCoord);
			#endif

				float2 uv = float2( 0.0, 0.0 );
				if( unity_MetaVertexControl.x )
				{
					uv = inputMesh.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				}
				else if( unity_MetaVertexControl.y )
				{
					uv = inputMesh.uv2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				}

				#ifdef EDITOR_VISUALIZATION
					o.VizUV.xy = vizUV;
					o.LightCoord = lightCoord;
				#endif

				o.positionCS = float4( uv * 2.0 - 1.0, inputMesh.positionOS.z > 0 ? 1.0e-4 : 0.0, 1.0 );
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float3 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				float4 uv3 : TEXCOORD3;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl Vert ( AttributesMesh v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.positionOS = v.positionOS;
				o.normalOS = v.normalOS;
				o.uv0 = v.uv0;
				o.uv1 = v.uv1;
				o.uv2 = v.uv2;
				o.uv3 = v.uv3;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
				float3 cameraPos = 0;
				#else
				float3 cameraPos = _WorldSpaceCameraPos;
				#endif
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), cameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, GetObjectToWorldMatrix(), cameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), cameraPos, _ScreenParams, _FrustumPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			PackedVaryingsMeshToPS DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				AttributesMesh o = (AttributesMesh) 0;
				o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.uv0 = patch[0].uv0 * bary.x + patch[1].uv0 * bary.y + patch[2].uv0 * bary.z;
				o.uv1 = patch[0].uv1 * bary.x + patch[1].uv1 * bary.y + patch[2].uv1 * bary.z;
				o.uv2 = patch[0].uv2 * bary.x + patch[1].uv2 * bary.y + patch[2].uv2 * bary.z;
				o.uv3 = patch[0].uv3 * bary.x + patch[1].uv3 * bary.y + patch[2].uv3 * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			PackedVaryingsMeshToPS Vert ( AttributesMesh v )
			{
				return VertexFunction( v );
			}
			#endif

			float4 Frag( PackedVaryingsMeshToPS packedInput  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( packedInput );
				FragInputs input;
				ZERO_INITIALIZE( FragInputs, input );
				input.tangentToWorld = k_identity3x3;
				input.positionSS = packedInput.positionCS;

				PositionInputs posInput = GetPositionInput( input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS );

				float3 V = float3( 1.0, 1.0, 1.0 );

				float4 screenPos = packedInput.ase_texcoord2;
				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( screenPos );
				float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
				float2 appendResult8 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float eros67 = _Erosion;
				float2 texCoord61 = packedInput.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_65_0 = ( texCoord61 * _IntensityMaskUVS );
				float2 panner199 = ( 1.0 * _Time.y * _IntensityMaskPanSpeed + ( temp_output_65_0 + _IntensityMaskOffset ));
				float2 texCoord48 = packedInput.ase_texcoord3.zw * float2( 1,1 ) + float2( 0,0 );
				float2 panner59 = ( 1.0 * _Time.y * _Noise01UVP + ( texCoord48 * _Noise01UVS ));
				float randomOffset163 = 0.0;
				float noiseMultiply51 = _NoiseMultiply;
				float smoothstepResult82 = smoothstep( eros67 , ( eros67 + _ErosionSmoothness ) , tex2D( _IntensityMask, ( panner199 + ( tex2D( _NoiseTexture, ( panner59 + ( randomOffset163 * 0.173 ) ) ).r * ( _Noise01Intensity * noiseMultiply51 ) ) ) ).g);
				float temp_output_100_0 = saturate( ( saturate( pow( smoothstepResult82 , _IntensityMaskPower ) ) * _IntensityMaskMultiply ) );
				float2 uv_OpacityMask = packedInput.ase_texcoord3.zw * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
				float temp_output_159_0 = saturate( ( saturate( pow( tex2D( _OpacityMask, uv_OpacityMask ).g , _OpacityMaskPower ) ) * _OpacityMaskMultiply ) );
				float eyeDepth = packedInput.ase_texcoord4.x;
				float cameraDepthFade187 = (( eyeDepth -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float screenDepth111 = LinearEyeDepth(SampleCameraDepth( ase_positionSSNorm.xy ),_ZBufferParams);
				float distanceDepth111 = saturate( ( screenDepth111 - LinearEyeDepth( ase_positionSSNorm.z,_ZBufferParams ) ) / ( _DepthFade ) );
				float temp_output_141_0 = saturate( ( saturate( ( saturate( ( saturate( ( temp_output_100_0 * temp_output_159_0 ) ) * saturate( cameraDepthFade187 ) ) ) * distanceDepth111 ) ) * _OpacityMultiply ) );
				float2 temp_cast_0 = (temp_output_141_0).xx;
				float distortionIntensity137 = _DistortionIntensity;
				float2 lerpResult33 = lerp( float2( 0,0 ) , temp_cast_0 , ( _DistortionLerp * distortionIntensity137 ));
				float4 fetchOpaqueVal9 = ASEHDSampleSceneColor(( appendResult8 + lerpResult33 ).xy, 0.0, GetInverseCurrentExposureMultiplier());
				

				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				surfaceDescription.Color = ( fetchOpaqueVal9 * GetCurrentExposureMultiplier() ).rgb;
				surfaceDescription.Emission = ( fetchOpaqueVal9 * _EmissionMultiply ).rgb;
				surfaceDescription.Alpha = packedInput.ase_color.a;

				#ifdef _ALPHATEST_ON
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;
				#endif

				SurfaceData surfaceData;
				BuiltinData builtinData;
				GetSurfaceAndBuiltinData( surfaceDescription,input, V, posInput, surfaceData, builtinData );

				BSDFData bsdfData = ConvertSurfaceDataToBSDFData( input.positionSS.xy, surfaceData );
				LightTransportData lightTransportData = GetLightTransportData( surfaceData, builtinData, bsdfData );

				float4 res = float4( 0.0, 0.0, 0.0, 1.0 );
				UnityMetaInput metaInput;
				metaInput.Albedo = lightTransportData.diffuseColor.rgb;
				metaInput.Emission = lightTransportData.emissiveColor;
			#ifdef EDITOR_VISUALIZATION
				metaInput.VizUV = packedInput.VizUV;
				metaInput.LightCoord = packedInput.LightCoord;
			#endif
				res = UnityMetaFragment(metaInput);

				return res;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define shader_feature_local _DISABLE_DECALS
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003


			#pragma shader_feature _SURFACE_TYPE_TRANSPARENT

			#pragma editor_sync_compilation

			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex Vert
			#pragma fragment Frag

			#define SHADERPASS SHADERPASS_DEPTH_ONLY
			#define SCENESELECTIONPASS 1
            #define SUPPORT_GLOBAL_MIP_BIAS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

			int _ObjectId;
			int _PassValue;

			CBUFFER_START( UnityPerMaterial )
			float4 _OpacityMask_ST;
			float2 _IntensityMaskPanSpeed;
			float2 _IntensityMaskUVS;
			float2 _IntensityMaskOffset;
			float2 _Noise01UVP;
			float2 _Noise01UVS;
			float _Dst;
			float _DistortionLerp;
			float _OpacityMultiply;
			float _DepthFade;
			float _CameraDepthFadeOffset;
			float _CameraDepthFadeLength;
			float _OpacityMaskMultiply;
			float _OpacityMaskPower;
			float _IntensityMaskMultiply;
			float _Noise01Intensity;
			float _NoiseMultiply;
			float _DistortionIntensity;
			float _ErosionSmoothness;
			float _Erosion;
			float _Cull;
			float _ZTest;
			float _ZWrite1;
			float _Src;
			float _IntensityMaskPower;
			float _EmissionMultiply;
			float4 _EmissionColor;
			float _RenderQueueType;
			#ifdef _ADD_PRECOMPUTED_VELOCITY
			float _AddPrecomputedVelocity;
			#endif
			#ifdef _ENABLE_SHADOW_MATTE
			float _ShadowMatteFilter;
			#endif
			float _StencilRef;
			float _StencilWriteMask;
			float _StencilRefDepth;
			float _StencilWriteMaskDepth;
			float _StencilRefMV;
			float _StencilWriteMaskMV;
			float _StencilRefDistortionVec;
			float _StencilWriteMaskDistortionVec;
			float _StencilWriteMaskGBuffer;
			float _StencilRefGBuffer;
			float _ZTestGBuffer;
			float _RequireSplitLighting;
			float _ReceivesSSR;
			float _SurfaceType;
			float _BlendMode;
			float _SrcBlend;
			float _DstBlend;
			float _DstBlend2;
			float _AlphaSrcBlend;
			float _AlphaDstBlend;
			float _ZWrite;
			float _TransparentZWrite;
			float _CullMode;
			float _TransparentSortPriority;
			float _EnableFogOnTransparent;
			float _CullModeForward;
			float _TransparentCullMode;
			float _ZTestDepthEqualForOpaque;
			float _ZTestTransparent;
			float _TransparentBackfaceEnable;
			float _AlphaCutoffEnable;
			float _AlphaCutoff;
			float _AlphaCutoffShadow;
			float _UseShadowThreshold;
			float _DoubleSidedEnable;
			float _DoubleSidedNormalMode;
			float4 _DoubleSidedConstants;
			float _EnableBlendModePreserveSpecularLighting;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			

			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl"

			

			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryingsMeshToPS
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_Position;
				float3 positionRWS : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};


			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			void BuildSurfaceData(FragInputs fragInputs, SurfaceDescription surfaceDescription, float3 V, out SurfaceData surfaceData)
			{
				ZERO_INITIALIZE(SurfaceData, surfaceData);

				#ifdef WRITE_NORMAL_BUFFER
				surfaceData.normalWS = fragInputs.tangentToWorld[2];
				#endif
			}

			void GetSurfaceAndBuiltinData(SurfaceDescription surfaceDescription, FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
			{
				#ifdef LOD_FADE_CROSSFADE
                LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
                #endif

				#ifdef _ALPHATEST_ON
				DoAlphaTest ( surfaceDescription.Alpha, surfaceDescription.AlphaClipThreshold );
				#endif

				BuildSurfaceData(fragInputs, surfaceDescription, V, surfaceData);
				ZERO_INITIALIZE(BuiltinData, builtinData);
				builtinData.opacity =  surfaceDescription.Alpha;

				#ifdef _ALPHATEST_ON
                    builtinData.alphaClipTreshold = surfaceDescription.AlphaClipThreshold;
                #endif

				#ifdef _DEPTHOFFSET_ON
                builtinData.depthOffset = surfaceDescription.DepthOffset;
                #endif


                ApplyDebugToBuiltinData(builtinData);
			}

			PackedVaryingsMeshToPS VertexFunction( AttributesMesh inputMesh  )
			{
				PackedVaryingsMeshToPS o;
				UNITY_SETUP_INSTANCE_ID(inputMesh);
				UNITY_TRANSFER_INSTANCE_ID(inputMesh, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_color = inputMesh.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				float3 defaultVertexValue = inputMesh.positionOS.xyz;
				#else
				float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue =  defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				inputMesh.positionOS.xyz = vertexValue;
				#else
				inputMesh.positionOS.xyz += vertexValue;
				#endif

				inputMesh.normalOS = inputMesh.normalOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
				// Declared so the Velocity output port surfaces on the master node; only consumed by the motion vector pass.
				float3 aseCustomVelocity = float3( 0, 0, 0 );
				#endif

				float3 positionRWS = TransformObjectToWorld(inputMesh.positionOS);
				o.positionCS = ASE_ADJUST_CLIP_POSITION( TransformWorldToHClip(positionRWS) );
				o.positionRWS = positionRWS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float3 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl Vert ( AttributesMesh v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.positionOS = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
				float3 cameraPos = 0;
				#else
				float3 cameraPos = _WorldSpaceCameraPos;
				#endif
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), cameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, GetObjectToWorldMatrix(), cameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), cameraPos, _ScreenParams, _FrustumPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			PackedVaryingsMeshToPS DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				AttributesMesh o = (AttributesMesh) 0;
				o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			PackedVaryingsMeshToPS Vert ( AttributesMesh v )
			{
				return VertexFunction( v );
			}
			#endif

			void Frag( PackedVaryingsMeshToPS packedInput
						, out float4 outColor : SV_Target0
						#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
						, out float outputDepth : DEPTH_OFFSET_SEMANTIC
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID( packedInput );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( packedInput );
				FragInputs input;
				ZERO_INITIALIZE(FragInputs, input);
				input.tangentToWorld = k_identity3x3;
				input.positionSS = packedInput.positionCS;

				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

				float3 PositionRWS = packedInput.positionRWS;
				float3 V = GetWorldSpaceNormalizeViewDir( packedInput.positionRWS );
				float4 ScreenPosNorm = float4( posInput.positionNDC, packedInput.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, packedInput.positionCS.z ) * packedInput.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos, _ProjectionParams.x );

				

				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				surfaceDescription.Alpha = packedInput.ase_color.a;

				#ifdef _ALPHATEST_ON
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
				posInput.deviceDepth = posInput.deviceDepth;
				#endif

				#ifdef _DEPTHOFFSET_ON
				surfaceDescription.DepthOffset = 0;
				#endif

				SurfaceData surfaceData;
				BuiltinData builtinData;
				GetSurfaceAndBuiltinData(surfaceDescription, input, V, posInput, surfaceData, builtinData);

				#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
				outputDepth = posInput.deviceDepth;
				#endif

				outColor = float4( _ObjectId, _PassValue, 1.0, 1.0 );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthForwardOnly"
			Tags { "LightMode"="DepthForwardOnly" }

			Cull [_CullMode]
			ZWrite On
			Stencil
			{
				Ref [_StencilRefDepth]
				WriteMask [_StencilWriteMaskDepth]
				Comp Always
				Pass Replace
			}


			ColorMask 0 0

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define shader_feature_local _DISABLE_DECALS
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003


			#pragma shader_feature _SURFACE_TYPE_TRANSPARENT

			#pragma multi_compile _ WRITE_MSAA_DEPTH

			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex Vert
			#pragma fragment Frag

			#define SHADERPASS SHADERPASS_DEPTH_ONLY
            #define SUPPORT_GLOBAL_MIP_BIAS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

			CBUFFER_START( UnityPerMaterial )
			float4 _OpacityMask_ST;
			float2 _IntensityMaskPanSpeed;
			float2 _IntensityMaskUVS;
			float2 _IntensityMaskOffset;
			float2 _Noise01UVP;
			float2 _Noise01UVS;
			float _Dst;
			float _DistortionLerp;
			float _OpacityMultiply;
			float _DepthFade;
			float _CameraDepthFadeOffset;
			float _CameraDepthFadeLength;
			float _OpacityMaskMultiply;
			float _OpacityMaskPower;
			float _IntensityMaskMultiply;
			float _Noise01Intensity;
			float _NoiseMultiply;
			float _DistortionIntensity;
			float _ErosionSmoothness;
			float _Erosion;
			float _Cull;
			float _ZTest;
			float _ZWrite1;
			float _Src;
			float _IntensityMaskPower;
			float _EmissionMultiply;
			float4 _EmissionColor;
			float _RenderQueueType;
			#ifdef _ADD_PRECOMPUTED_VELOCITY
			float _AddPrecomputedVelocity;
			#endif
			#ifdef _ENABLE_SHADOW_MATTE
			float _ShadowMatteFilter;
			#endif
			float _StencilRef;
			float _StencilWriteMask;
			float _StencilRefDepth;
			float _StencilWriteMaskDepth;
			float _StencilRefMV;
			float _StencilWriteMaskMV;
			float _StencilRefDistortionVec;
			float _StencilWriteMaskDistortionVec;
			float _StencilWriteMaskGBuffer;
			float _StencilRefGBuffer;
			float _ZTestGBuffer;
			float _RequireSplitLighting;
			float _ReceivesSSR;
			float _SurfaceType;
			float _BlendMode;
			float _SrcBlend;
			float _DstBlend;
			float _DstBlend2;
			float _AlphaSrcBlend;
			float _AlphaDstBlend;
			float _ZWrite;
			float _TransparentZWrite;
			float _CullMode;
			float _TransparentSortPriority;
			float _EnableFogOnTransparent;
			float _CullModeForward;
			float _TransparentCullMode;
			float _ZTestDepthEqualForOpaque;
			float _ZTestTransparent;
			float _TransparentBackfaceEnable;
			float _AlphaCutoffEnable;
			float _AlphaCutoff;
			float _AlphaCutoffShadow;
			float _UseShadowThreshold;
			float _DoubleSidedEnable;
			float _DoubleSidedNormalMode;
			float4 _DoubleSidedConstants;
			float _EnableBlendModePreserveSpecularLighting;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			

			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl"

			

			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryingsMeshToPS
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_Position;
				float3 positionRWS : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			void BuildSurfaceData(FragInputs fragInputs, SurfaceDescription surfaceDescription, float3 V, out SurfaceData surfaceData)
			{
				ZERO_INITIALIZE(SurfaceData, surfaceData);
				#ifdef WRITE_NORMAL_BUFFER
				surfaceData.normalWS = fragInputs.tangentToWorld[2];
				#endif
			}

			void GetSurfaceAndBuiltinData(SurfaceDescription surfaceDescription, FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
			{
				#ifdef LOD_FADE_CROSSFADE
                LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
                #endif

				#ifdef _ALPHATEST_ON
				DoAlphaTest ( surfaceDescription.Alpha, surfaceDescription.AlphaClipThreshold );
				#endif

				#ifdef _DEPTHOFFSET_ON
                ApplyDepthOffsetPositionInput(V, surfaceDescription.DepthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
                #endif

				BuildSurfaceData(fragInputs, surfaceDescription, V, surfaceData);
				ZERO_INITIALIZE(BuiltinData, builtinData);
				builtinData.opacity =  surfaceDescription.Alpha;

				#if defined(DEBUG_DISPLAY)
					builtinData.renderingLayers = GetMeshRenderingLayerMask();
				#endif

                #ifdef _ALPHATEST_ON
                    builtinData.alphaClipTreshold = surfaceDescription.AlphaClipThreshold;
                #endif

				#ifdef _DEPTHOFFSET_ON
                builtinData.depthOffset = surfaceDescription.DepthOffset;
                #endif

                ApplyDebugToBuiltinData(builtinData);
			}

			PackedVaryingsMeshToPS VertexFunction( AttributesMesh inputMesh  )
			{
				PackedVaryingsMeshToPS o;
				UNITY_SETUP_INSTANCE_ID(inputMesh);
				UNITY_TRANSFER_INSTANCE_ID(inputMesh, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_color = inputMesh.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				float3 defaultVertexValue = inputMesh.positionOS.xyz;
				#else
				float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue =  defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				inputMesh.positionOS.xyz = vertexValue;
				#else
				inputMesh.positionOS.xyz += vertexValue;
				#endif

				inputMesh.normalOS = inputMesh.normalOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
				// Declared so the Velocity output port surfaces on the master node; only consumed by the motion vector pass.
				float3 aseCustomVelocity = float3( 0, 0, 0 );
				#endif

				float3 positionRWS = TransformObjectToWorld(inputMesh.positionOS);
				o.positionCS = ASE_ADJUST_CLIP_POSITION( TransformWorldToHClip(positionRWS) );
				o.positionRWS = positionRWS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float3 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl Vert ( AttributesMesh v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.positionOS = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
				float3 cameraPos = 0;
				#else
				float3 cameraPos = _WorldSpaceCameraPos;
				#endif
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), cameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, GetObjectToWorldMatrix(), cameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), cameraPos, _ScreenParams, _FrustumPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			PackedVaryingsMeshToPS DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				AttributesMesh o = (AttributesMesh) 0;
				o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			PackedVaryingsMeshToPS Vert ( AttributesMesh v )
			{
				return VertexFunction( v );
			}
			#endif

			void Frag( PackedVaryingsMeshToPS packedInput
						#ifdef WRITE_MSAA_DEPTH
						, out float4 depthColor : SV_Target0
							#ifdef WRITE_NORMAL_BUFFER
							, out float4 outNormalBuffer : SV_Target1
							#endif
						#else
							#ifdef WRITE_NORMAL_BUFFER
							, out float4 outNormalBuffer : SV_Target0
							#endif
						#endif
						#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
						, out float outputDepth : DEPTH_OFFSET_SEMANTIC
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID( packedInput );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( packedInput );

				FragInputs input;
				ZERO_INITIALIZE(FragInputs, input);
				input.tangentToWorld = k_identity3x3;
				input.positionSS = packedInput.positionCS;
				input.positionRWS = packedInput.positionRWS;

				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

				float3 PositionRWS = packedInput.positionRWS;
				float3 V = GetWorldSpaceNormalizeViewDir( packedInput.positionRWS );
				float4 ScreenPosNorm = float4( posInput.positionNDC, packedInput.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, packedInput.positionCS.z ) * packedInput.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos, _ProjectionParams.x );

				

				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				surfaceDescription.Alpha = packedInput.ase_color.a;

				#ifdef _ALPHATEST_ON
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
				posInput.deviceDepth = posInput.deviceDepth;
				#endif

				#ifdef _DEPTHOFFSET_ON
				surfaceDescription.DepthOffset = 0;
				#endif

				SurfaceData surfaceData;
				BuiltinData builtinData;
				GetSurfaceAndBuiltinData(surfaceDescription, input, V, posInput, surfaceData, builtinData);

				#if defined(_DEPTHOFFSET_ON) || defined(ASE_WRITE_DEPTH)
				outputDepth = posInput.deviceDepth;
				#endif

				#ifdef WRITE_MSAA_DEPTH
					depthColor = packedInput.positionCS.z;
					#ifdef _ALPHATOMASK_ON
					depthColor.a = SharpenAlpha(builtinData.opacity, builtinData.alphaClipTreshold);
					#endif
				#endif

				#if defined(WRITE_NORMAL_BUFFER)
					EncodeIntoNormalBuffer(ConvertSurfaceDataToNormalData(surfaceData), outNormalBuffer);
				#endif
			}

			ENDHLSL
		}

		
        Pass
		{
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

            Cull [_CullMode]

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define shader_feature_local _DISABLE_DECALS
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003


			#pragma shader_feature _SURFACE_TYPE_TRANSPARENT
			#pragma shader_feature_local _ _TRANSPARENT_WRITES_MOTION_VEC _TRANSPARENT_REFRACTIVE_SORT

			#pragma editor_sync_compilation

			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex Vert
			#pragma fragment Frag

			#if (defined(_TRANSPARENT_WRITES_MOTION_VEC) || defined(_TRANSPARENT_REFRACTIVE_SORT)) && defined(_SURFACE_TYPE_TRANSPARENT)
			#define _WRITE_TRANSPARENT_MOTION_VECTOR
			#endif

			#define SHADERPASS SHADERPASS_DEPTH_ONLY
			#define SCENEPICKINGPASS 1
            #define SUPPORT_GLOBAL_MIP_BIAS 1

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
        	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define VARYINGS_NEED_TANGENT_TO_WORLD

			#define SHADER_UNLIT

			float4 _SelectionID;

            CBUFFER_START( UnityPerMaterial )
			float4 _OpacityMask_ST;
			float2 _IntensityMaskPanSpeed;
			float2 _IntensityMaskUVS;
			float2 _IntensityMaskOffset;
			float2 _Noise01UVP;
			float2 _Noise01UVS;
			float _Dst;
			float _DistortionLerp;
			float _OpacityMultiply;
			float _DepthFade;
			float _CameraDepthFadeOffset;
			float _CameraDepthFadeLength;
			float _OpacityMaskMultiply;
			float _OpacityMaskPower;
			float _IntensityMaskMultiply;
			float _Noise01Intensity;
			float _NoiseMultiply;
			float _DistortionIntensity;
			float _ErosionSmoothness;
			float _Erosion;
			float _Cull;
			float _ZTest;
			float _ZWrite1;
			float _Src;
			float _IntensityMaskPower;
			float _EmissionMultiply;
			float4 _EmissionColor;
			float _RenderQueueType;
			#ifdef _ADD_PRECOMPUTED_VELOCITY
			float _AddPrecomputedVelocity;
			#endif
			#ifdef _ENABLE_SHADOW_MATTE
			float _ShadowMatteFilter;
			#endif
			float _StencilRef;
			float _StencilWriteMask;
			float _StencilRefDepth;
			float _StencilWriteMaskDepth;
			float _StencilRefMV;
			float _StencilWriteMaskMV;
			float _StencilRefDistortionVec;
			float _StencilWriteMaskDistortionVec;
			float _StencilWriteMaskGBuffer;
			float _StencilRefGBuffer;
			float _ZTestGBuffer;
			float _RequireSplitLighting;
			float _ReceivesSSR;
			float _SurfaceType;
			float _BlendMode;
			float _SrcBlend;
			float _DstBlend;
			float _DstBlend2;
			float _AlphaSrcBlend;
			float _AlphaDstBlend;
			float _ZWrite;
			float _TransparentZWrite;
			float _CullMode;
			float _TransparentSortPriority;
			float _EnableFogOnTransparent;
			float _CullModeForward;
			float _TransparentCullMode;
			float _ZTestDepthEqualForOpaque;
			float _ZTestTransparent;
			float _TransparentBackfaceEnable;
			float _AlphaCutoffEnable;
			float _AlphaCutoff;
			float _AlphaCutoffShadow;
			float _UseShadowThreshold;
			float _DoubleSidedEnable;
			float _DoubleSidedNormalMode;
			float4 _DoubleSidedConstants;
			float _EnableBlendModePreserveSpecularLighting;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl"

			

			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryingsMeshToPS
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionRWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
            struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};


            void GetSurfaceAndBuiltinData(SurfaceDescription surfaceDescription, FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
            {
                #ifdef LOD_FADE_CROSSFADE
			        LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
                #endif

                #ifdef _ALPHATEST_ON
                    float alphaCutoff = surfaceDescription.AlphaClipThreshold;
                    GENERIC_ALPHA_TEST(surfaceDescription.Alpha, alphaCutoff);
                #endif

                #if !defined(SHADER_STAGE_RAY_TRACING) && _DEPTHOFFSET_ON
                ApplyDepthOffsetPositionInput(V, surfaceDescription.DepthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
                #endif


				ZERO_INITIALIZE(SurfaceData, surfaceData);

				ZERO_BUILTIN_INITIALIZE(builtinData);
				builtinData.opacity = surfaceDescription.Alpha;

				#if defined(DEBUG_DISPLAY)
					builtinData.renderingLayers = GetMeshRenderingLayerMask();
				#endif

                #ifdef _ALPHATEST_ON
                    builtinData.alphaClipTreshold = alphaCutoff;
                #endif

                #ifdef _DEPTHOFFSET_ON
                builtinData.depthOffset = surfaceDescription.DepthOffset;
                #endif


                ApplyDebugToBuiltinData(builtinData);

            }


			PackedVaryingsMeshToPS VertexFunction(AttributesMesh inputMesh  )
			{

				PackedVaryingsMeshToPS o;
				ZERO_INITIALIZE(PackedVaryingsMeshToPS, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				UNITY_SETUP_INSTANCE_ID(inputMesh);
				UNITY_TRANSFER_INSTANCE_ID(inputMesh, o );

				o.ase_color = inputMesh.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				float3 defaultVertexValue = inputMesh.positionOS.xyz;
				#else
				float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue =  defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
				inputMesh.positionOS.xyz = vertexValue;
				#else
				inputMesh.positionOS.xyz += vertexValue;
				#endif

				inputMesh.normalOS = inputMesh.normalOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
				// Declared so the Velocity output port surfaces on the master node; only consumed by the motion vector pass.
				float3 aseCustomVelocity = float3( 0, 0, 0 );
				#endif

				float3 positionRWS = TransformObjectToWorld(inputMesh.positionOS);
				float3 normalWS = TransformObjectToWorldNormal(inputMesh.normalOS);
				float4 tangentWS = float4(TransformObjectToWorldDir(inputMesh.tangentOS.xyz), inputMesh.tangentOS.w);

				o.positionCS = ASE_ADJUST_CLIP_POSITION( TransformWorldToHClip(positionRWS) );
				o.positionRWS = positionRWS;
				o.normalWS.xyz =  normalWS;
				o.tangentWS.xyzw =  tangentWS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float3 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl Vert ( AttributesMesh v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.positionOS = v.positionOS;
				o.normalOS = v.normalOS;
				o.tangentOS = v.tangentOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
				float3 cameraPos = 0;
				#else
				float3 cameraPos = _WorldSpaceCameraPos;
				#endif
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), cameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, GetObjectToWorldMatrix(), cameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(float4(v[0].positionOS,1), float4(v[1].positionOS,1), float4(v[2].positionOS,1), edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), cameraPos, _ScreenParams, _FrustumPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			PackedVaryingsMeshToPS DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				AttributesMesh o = (AttributesMesh) 0;
				o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			PackedVaryingsMeshToPS Vert ( AttributesMesh v )
			{
				return VertexFunction( v );
			}
			#endif

			void Frag(	PackedVaryingsMeshToPS packedInput
						, out float4 outColor : SV_Target0
						
					)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(packedInput);
				UNITY_SETUP_INSTANCE_ID(packedInput);

				FragInputs input;
				ZERO_INITIALIZE(FragInputs, input);
				input.tangentToWorld = k_identity3x3;
				input.positionSS = packedInput.positionCS;

				input.tangentToWorld = BuildTangentToWorld(packedInput.tangentWS.xyzw, packedInput.normalWS.xyz);

				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

				float3 PositionRWS = packedInput.positionRWS;
				float3 V = GetWorldSpaceNormalizeViewDir( packedInput.positionRWS );
				float4 ScreenPosNorm = float4( posInput.positionNDC, packedInput.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, packedInput.positionCS.z ) * packedInput.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos, _ProjectionParams.x );

				

				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				surfaceDescription.Alpha = packedInput.ase_color.a;

				#ifdef _ALPHATEST_ON
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;
				#endif

				SurfaceData surfaceData;
				BuiltinData builtinData;
				GetSurfaceAndBuiltinData(surfaceDescription, input, V, posInput, surfaceData, builtinData);
				outColor = unity_SelectionID;
			}

            ENDHLSL
        }

		Pass
		{
			Name "FullScreenDebug"
			Tags
			{
				"LightMode" = "FullScreenDebug"
			}

			Cull [_CullMode]
			ZTest LEqual
			ZWrite Off

			HLSLPROGRAM

			/*ase_pragma_before*/

			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"

			#define SHADERPASS SHADERPASS_FULL_SCREEN_DEBUG
            #define SUPPORT_GLOBAL_MIP_BIAS 1

			struct AttributesMesh
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				#if UNITY_ANY_INSTANCING_ENABLED
					uint instanceID : INSTANCEID_SEMANTIC;
				#endif
			};

			struct VaryingsMeshToPS
			{
				SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				#if UNITY_ANY_INSTANCING_ENABLED
					uint instanceID : CUSTOM_INSTANCE_ID;
				#endif
			};

			struct PackedVaryingsMeshToPS
			{
				SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				#if UNITY_ANY_INSTANCING_ENABLED
					uint instanceID : CUSTOM_INSTANCE_ID;
				#endif
			};

			VaryingsMeshToPS UnpackVaryingsMeshToPS (PackedVaryingsMeshToPS input)
			{
				VaryingsMeshToPS output;
				output.positionCS = input.positionCS;
				#if UNITY_ANY_INSTANCING_ENABLED
				output.instanceID = input.instanceID;
				#endif
				return output;
			}

			PackedVaryingsMeshToPS PackVaryingsMeshToPS (VaryingsMeshToPS input)
			{
				PackedVaryingsMeshToPS output;
				ZERO_INITIALIZE(PackedVaryingsMeshToPS, output);
				output.positionCS = input.positionCS;
				#if UNITY_ANY_INSTANCING_ENABLED
				output.instanceID = input.instanceID;
				#endif
				return output;
			}

			FragInputs BuildFragInputs(VaryingsMeshToPS input)
			{
				FragInputs output;
				ZERO_INITIALIZE(FragInputs, output);

				output.tangentToWorld = k_identity3x3;
				output.positionSS = input.positionCS;

				return output;
			}

			FragInputs UnpackVaryingsMeshToFragInputs(PackedVaryingsMeshToPS input)
			{
				UNITY_SETUP_INSTANCE_ID(input);
				VaryingsMeshToPS unpacked = UnpackVaryingsMeshToPS(input);
				return BuildFragInputs(unpacked);
			}

			#define DEBUG_DISPLAY
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/FullScreenDebug.hlsl"

			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/VertMesh.hlsl"

			PackedVaryingsType Vert(AttributesMesh inputMesh)
			{
				VaryingsType varyingsType;
				varyingsType.vmesh = VertMesh(inputMesh);
				return PackVaryingsType(varyingsType);
			}

			#if !defined(_DEPTHOFFSET_ON)
			[earlydepthstencil] // quad overshading debug mode writes to UAV
			#endif
			void Frag(PackedVaryingsToPS packedInput)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(packedInput);
				FragInputs input = UnpackVaryingsToFragInputs(packedInput);

				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS.xyz);

			#ifdef PLATFORM_SUPPORTS_PRIMITIVE_ID_IN_PIXEL_SHADER
				if (_DebugFullScreenMode == FULLSCREENDEBUGMODE_QUAD_OVERDRAW)
				{
					IncrementQuadOverdrawCounter(posInput.positionSS.xy, input.primitiveID);
				}
			#endif
			}

			ENDHLSL
		}
		
	}
	

	

	CustomEditor "Rendering.HighDefinition.HDUnlitGUI"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=19912
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":45,"pos":[462,-50],"params":["Inherit","False","1253","162.95","Lush was here! <3","5","3","2","4","5","1","Lush was here! <3","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":48,"pos":[-11904,-256],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":49,"pos":[-11520,-128],"params":["Inherit","False","Property","_Noise01UVS","Noise 01 UV S","10","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":52,"pos":[-11520,-256],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":58,"pos":[-10240,128],"params":["Inherit","False","51","noiseMultiply","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":63,"pos":[-10624,-256],"params":["Inherit","True","Property","_TextureSample0","Texture Sample 0","0","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":64,"pos":[-9984,0],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":69,"pos":[-10112,-256],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":73,"pos":[-7424,0],"params":["Inherit","False","Property","_ErosionSmoothness","Erosion Smoothness","4","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":74,"pos":[-7424,-384],"params":["Inherit","False","67","eros","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":78,"pos":[-7424,-128],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":79,"pos":[-7040,-384],"params":["Inherit","False","Property","_IntensityMaskPower","Intensity Mask Power","22","0","Create","True","0","0","0","False","0","False","Object","-1","","1","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SmoothstepOpNode, AmplifyShaderEditor","id":82,"pos":[-7424,-256],"params":["Inherit","False","3","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PowerNode, AmplifyShaderEditor","id":83,"pos":[-7040,-256],"params":["Inherit","True","False","2","0","FLOAT","0","False","1","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":85,"pos":[-6528,-384],"params":["Inherit","False","Property","_IntensityMaskMultiply","Intensity Mask Multiply","23","0","Create","True","0","0","0","False","0","False","Object","-1","","1","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":86,"pos":[-6784,-256],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":93,"pos":[-6528,-256],"params":["Inherit","True","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":100,"pos":[-6272,-256],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DepthFade, AmplifyShaderEditor","id":111,"pos":[-4736,1664],"params":["Inherit","False","True","True","False","2","1","FLOAT3","0,0,0","False","0","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":120,"pos":[-4096,1792],"params":["Inherit","False","Property","_OpacityMultiply","Opacity Multiply","5","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":130,"pos":[-4224,384],"params":["Inherit","True","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":125,"pos":[-4096,1536],"params":["Inherit","True","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":140,"pos":[-3840,384],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":141,"pos":[-3840,1536],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":44,"pos":[-1024,512],"params":["Inherit","False","Property","_DistortionLerp","Distortion Lerp","12","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":42,"pos":[-768,512],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":31,"pos":[-1280,128],"params":["Inherit","False","Constant","_Vector0","Vector 0","3","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0","0,0","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.GrabScreenPosition, AmplifyShaderEditor","id":7,"pos":[-1664,0],"params":["Inherit","False","0","0","5","FLOAT4","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":8,"pos":[-1408,0],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":32,"pos":[-1024,0],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ScreenColorNode, AmplifyShaderEditor","id":9,"pos":[-768,0],"params":["Inherit","False","Global","_GrabScreen0","Grab Screen 0","2","0","Create","True","0","0","0","False","0","False","","Object","-1","False","False","False","False","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor","id":16,"pos":[-384,256],"params":["Inherit","False","0","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":3,"pos":[1024,0],"params":["Inherit","False","Property","_Dst","Dst","32","0","Create","True","0","0","0","True","0","False","Object","-1","","10","10","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":2,"pos":[768,0],"params":["Inherit","False","Property","_Src","Src","31","0","Create","True","0","0","0","True","0","False","Object","-1","","5","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":4,"pos":[1280,0],"params":["Inherit","False","Property","_ZWrite1","ZWrite1","33","0","Create","True","0","0","0","True","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":5,"pos":[1537,0],"params":["Inherit","False","Property","_ZTest","ZTest","34","0","Create","True","0","0","0","True","0","False","Object","-1","","2","2","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":1,"pos":[512,0],"params":["Inherit","False","Property","_Cull","Cull","30","0","Create","True","0","0","0","True","3","Space(33)","Header(AR)","Space(13)","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":137,"pos":[-3712,-1152],"params":["Inherit","False","distortionIntensity","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":154,"pos":[-3968,-1024],"params":["Inherit","False","Property","_NoiseMultiply","Noise Multiply","2","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":67,"pos":[-3712,-896],"params":["Inherit","False","eros","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.LerpOp, AmplifyShaderEditor","id":33,"pos":[-1024,256],"params":["Inherit","False","3","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":138,"pos":[-1024,640],"params":["Inherit","False","137","distortionIntensity","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":152,"pos":[-3968,-1152],"params":["Inherit","False","Property","_DistortionIntensity","Distortion Intensity","1","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":155,"pos":[-3968,-896],"params":["Inherit","False","Property","_Erosion","Erosion","3","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":72,"pos":[-8576,-640],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":51,"pos":[-3712,-1024],"params":["Inherit","False","noiseMultiply","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PowerNode, AmplifyShaderEditor","id":156,"pos":[-6784,1664],"params":["Inherit","False","False","2","0","FLOAT","0","False","1","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":157,"pos":[-6656,1664],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":159,"pos":[-6400,1664],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":160,"pos":[-6784,1792],"params":["Inherit","False","Property","_OpacityMaskPower","Opacity Mask Power","28","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":162,"pos":[-6528,1664],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":161,"pos":[-6528,1792],"params":["Inherit","False","Property","_OpacityMaskMultiply","Opacity Mask Multiply","29","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor","id":164,"pos":[-3968,-768],"params":["Inherit","False","0","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":53,"pos":[-11264,-128],"params":["Inherit","False","Property","_Noise01UVP","Noise 01 UV P","11","0","Create","True","0","0","0","False","0","False","Object","-1","","0.1,0.3","-0.07,-0.3","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.PannerNode, AmplifyShaderEditor","id":59,"pos":[-11264,-256],"params":["Inherit","False","3","0","FLOAT2","0,0","False","2","FLOAT2","0,0","False","1","FLOAT","1","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":168,"pos":[-11008,-256],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":170,"pos":[-11008,-384],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.173","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":169,"pos":[-11008,-512],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":60,"pos":[-10624,-512],"params":["Inherit","True","Property","_NoiseTexture","Noise Texture","9","0","Create","True","0","0","0","False","3","Space(33)","Header(Noise)","Space(13)","False","","3327b545e30728e47b34b9588ff64aef","3327b545e30728e47b34b9588ff64aef","False","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":54,"pos":[-10240,0],"params":["Inherit","False","Property","_Noise01Intensity","Noise 01 Intensity","16","0","Create","True","0","0","0","False","0","False","Object","-1","","-1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":139,"pos":[-3328,384],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":184,"pos":[-11264,0],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":185,"pos":[-10752,0],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":186,"pos":[-11008,0],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.31","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":118,"pos":[-4736,1536],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":123,"pos":[-4480,1536],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":188,"pos":[-5376,1536],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":189,"pos":[-5120,1536],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":190,"pos":[-5120,1664],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":105,"pos":[-4736,1792],"params":["Inherit","False","Property","_DepthFade","Depth Fade","24","0","Create","True","0","0","0","False","3","Space(33)","Header(Depth Fade)","Space(13)","False","Object","-1","","0","0.3","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.CameraDepthFade, AmplifyShaderEditor","id":187,"pos":[-5376,1664],"params":["Inherit","False","3","2","FLOAT3","0,0,0","False","0","FLOAT","1","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":192,"pos":[-5376,1920],"params":["Inherit","False","Property","_CameraDepthFadeLength","Camera Depth Fade Length","25","0","Create","True","0","0","0","False","3","Space(33)","Header(Camera Depth Fade)","Space(13)","False","Object","-1","","3","3","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":193,"pos":[-5376,2048],"params":["Inherit","False","Property","_CameraDepthFadeOffset","Camera Depth Fade Offset","26","0","Create","True","0","0","0","False","0","False","Object","-1","","0.1","0.1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":136,"pos":[-7424,768],"params":["Inherit","True","Property","_TextureSample5","Texture Sample 3","11","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":112,"pos":[-7424,1024],"params":["Inherit","True","Property","_BaseTexture","Base Texture","6","0","Create","True","0","0","0","False","3","Space(33)","Header(Base Texture)","Space(13)","False","","3327b545e30728e47b34b9588ff64aef","3327b545e30728e47b34b9588ff64aef","False","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":106,"pos":[-6016,1536],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":113,"pos":[-5760,1536],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":194,"pos":[-6016,1280],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":195,"pos":[-5760,1280],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":196,"pos":[-3712,-640],"params":["Inherit","False","Constant","_Float0","Float 0","32","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":163,"pos":[-3712,-768],"params":["Inherit","False","randomOffset","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":65,"pos":[-9472,-640],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":61,"pos":[-9984,-640],"params":["Inherit","False","1","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":197,"pos":[-9216,-640],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":165,"pos":[-9456,-928],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":167,"pos":[-9456,-1056],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PannerNode, AmplifyShaderEditor","id":199,"pos":[-8960,-640],"params":["Inherit","False","3","0","FLOAT2","0,0","False","2","FLOAT2","0,0","False","1","FLOAT","1","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":198,"pos":[-9216,-512],"params":["Inherit","False","Property","_IntensityMaskOffset","Intensity Mask Offset","21","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0","0,0","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":62,"pos":[-9472,-512],"params":["Inherit","False","Property","_IntensityMaskUVS","Intensity Mask UV S","19","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":200,"pos":[-8960,-512],"params":["Inherit","False","Property","_IntensityMaskPanSpeed","Intensity Mask Pan Speed","20","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0","0,0","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":77,"pos":[-8320,-640],"params":["Inherit","True","Property","_IntensityMask","Intensity Mask","18","0","Create","True","0","0","0","False","3","Space(33)","Header(Intensity Mask)","Space(13)","False","","-1","fa6235bc65ac4064f8d49fcc0c8ff503","fa6235bc65ac4064f8d49fcc0c8ff503","True","0","False","black","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":101,"pos":[-7168,1536],"params":["Inherit","True","Property","_OpacityMask","Opacity Mask","27","0","Create","True","0","0","0","False","3","Space(33)","Header(Opacity Cutout Mask)","Space(13)","False","","-1","171b695a70c8bb2439da87f69178c81b","171b695a70c8bb2439da87f69178c81b","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":15,"pos":[-3088,400],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":70,"pos":[-11904,1024],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":71,"pos":[-11520,1152],"params":["Inherit","False","Property","_NoiseDistUVS","Noise Dist UV S","14","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":75,"pos":[-11520,1024],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":84,"pos":[-10624,1024],"params":["Inherit","True","Property","_TextureSample1","Texture Sample 1","0","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","True","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":89,"pos":[-10240,1024],"params":["Inherit","True","True","True","False","True","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":91,"pos":[-9728,1408],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":95,"pos":[-9984,1024],"params":["Inherit","True","ConstantBiasScale","-1","","1","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT2","0,0","False","1","FLOAT","-0.5","False","2","FLOAT","2","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":97,"pos":[-9344,1408],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":102,"pos":[-9344,1024],"params":["Inherit","True","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.PannerNode, AmplifyShaderEditor","id":103,"pos":[-8832,1408],"params":["Inherit","False","3","0","FLOAT2","0,0","False","2","FLOAT2","0,0","False","1","FLOAT","1","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":76,"pos":[-11264,1152],"params":["Inherit","False","Property","_NoiseDistUVP","Noise Dist UV P","15","0","Create","True","0","0","0","False","0","False","Object","-1","","-0.2,-0.1","0.02,-0.1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.PannerNode, AmplifyShaderEditor","id":80,"pos":[-11264,1024],"params":["Inherit","False","3","0","FLOAT2","0,0","False","2","FLOAT2","0,0","False","1","FLOAT","1","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":171,"pos":[-11008,1024],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":174,"pos":[-8576,1408],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":175,"pos":[-8576,1152],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":107,"pos":[-8064,1024],"params":["Inherit","True","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":176,"pos":[-8576,1280],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","3.333","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":90,"pos":[-9728,1664],"params":["Inherit","False","Property","_BaseTextureUVS","Base Texture UV S","7","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":98,"pos":[-9088,1664],"params":["Inherit","False","Property","_BaseTextureUVP","Base Texture UV P","8","0","Create","True","0","0","0","False","0","False","Object","-1","","-0.2,-0.1","-0.05,-0.2","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":96,"pos":[-9728,1152],"params":["Inherit","False","Property","_BaseTextureDistortionIntensity","Base Texture Distortion Intensity","17","0","Create","True","0","0","0","False","0","False","Object","-1","","1","0.1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":173,"pos":[-11008,768],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":172,"pos":[-11008,896],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","1.777","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":180,"pos":[-11008,1408],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":178,"pos":[-11520,1408],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":183,"pos":[-9088,1792],"params":["Inherit","False","163","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":181,"pos":[-8576,1792],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":179,"pos":[-11264,1408],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.137","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":182,"pos":[-8832,1792],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.07","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":81,"pos":[-10624,768],"params":["Inherit","True","Property","_NoiseDistortionTexture","Noise Distortion Texture","13","1","[Normal]","Create","True","0","0","0","False","3","Space(33)","Header(Noise Distortion)","Space(13)","False","","23d93a568484acf42a53f25e56beb838","23d93a568484acf42a53f25e56beb838","True","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":210,"pos":[-464,-160],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","COLOR","0,0,0,0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.Exposure, AmplifyShaderEditor","id":212,"pos":[-768,-512],"params":["Inherit","False","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":211,"pos":[-512,-512],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","FLOAT","0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":213,"pos":[-384,0],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","FLOAT","0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":214,"pos":[-384,128],"params":["Inherit","False","Property","_EmissionMultiply","Emission Multiply","0","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":202,"pos":[0,0],"params":["Float","False","True","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","16","Vefects/SH_Vefects_VFX_HDRP_Trail_Distortion_New","7f5cb9c3ea6481f469fdd856555439ef","True","Forward Unlit","0","0","Forward Unlit","13","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Transparent=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","True","3","1","False","","10","False","","0","1","False","","0","False","","False","False","True","1","1","False","","0","True","_DstBlend2","0","1","False","","0","False","","False","False","True","1","1","False","","0","True","_DstBlend2","0","1","False","","0","False","","False","False","False","True","0","True","_CullModeForward","False","False","False","True","True","True","True","True","0","True","_ColorMaskTransparentVel","False","False","False","False","False","True","True","0","True","_StencilRef","255","False","","255","True","_StencilWriteMask","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","0","True","_ZWrite","True","0","True","_ZTestDepthEqualForOpaque","False","False","True","1","LightMode=ForwardOnly","False","False","0","Hidden/InternalErrorShader","0","0","Standard","35","Surface Type","1","638823713431042216","  Rendering Pass ","0","0","  Rendering Pass","1","0","  Blending Mode","0","0","  Receive Fog","0","639231927484794803","  Distortion","0","0","    Distortion Mode","0","0","    Distortion Only","1","0","  Depth Write","0","639231927461023616","  Cull Mode","0","0","  Depth Test","4","0","Double-Sided","0","0","Alpha Clipping","0","0","  Use Shadow Threshold","0","0","Receive Decals","0","639231927478572765","Motion Vectors","0","639231927476016385","  Add Precomputed Velocity","0","0","  Add Custom Velocity","0","0","Shadow Matte","0","0","Cast Shadows","0","639231927470024233","Write Depth","0","0","  Depth Offset","0","0","  Conservative","0","0","GPU Instancing","1","0","Tessellation","0","0","  Phong","0","0","  Strength","0.5,False,","0","  Type","0","0","  Tess","16,False,","0","  Min","10,False,","0","  Max","25,False,","0","  Edge Length","16,False,","0","  Max Displacement","25,False,","0","Vertex Position","1","0","LOD CrossFade","0","0","0","8","True","False","True","True","True","False","False","True","False","","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":203,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","ShadowCaster","0","1","ShadowCaster","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_CullMode","False","True","False","False","False","False","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","False","False","True","0","True","_ZClip","True","1","LightMode=ShadowCaster","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":204,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","META","0","2","META","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=Meta","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":205,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","SceneSelectionPass","0","3","SceneSelectionPass","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=SceneSelectionPass","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":206,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","DepthForwardOnly","0","4","DepthForwardOnly","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_CullMode","False","True","False","False","False","False","0","False","","False","False","False","False","False","False","False","True","True","0","True","_StencilRefDepth","255","False","","255","True","_StencilWriteMaskDepth","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","False","False","False","True","1","LightMode=DepthForwardOnly","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":207,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","MotionVectors","0","5","MotionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_CullMode","False","False","False","False","False","False","False","False","False","True","True","0","True","_StencilRefMV","255","False","","255","True","_StencilWriteMaskMV","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","False","False","False","True","1","LightMode=MotionVectors","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":208,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","DistortionVectors","0","6","DistortionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","True","4","1","False","","1","False","","4","1","False","","1","False","","True","1","False","","1","False","","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_CullMode","False","False","False","False","False","False","False","False","False","True","True","0","True","_StencilRefDistortionVec","255","False","","255","True","_StencilWriteMaskDistortionVec","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","2","False","","True","3","False","","False","False","True","1","LightMode=DistortionVectors","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":209,"pos":[0,0],"params":["Float","False","False","-1","2","Rendering.HighDefinition.HDUnlitGUI","0","1","New Amplify Shader","7f5cb9c3ea6481f469fdd856555439ef","True","ScenePickingPass","0","7","ScenePickingPass","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","4","RenderPipeline=HDRenderPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","ShaderGraphShader=true","True","5","True","9","d3d11","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_CullMode","False","False","False","False","False","False","False","False","False","False","False","True","2","False","","True","3","False","","False","False","True","1","LightMode=Picking","False","False","0","Hidden/InternalErrorShader","0","0","Standard","0","False","0"]}
{"wire":[52,0,48,0]}
{"wire":[52,1,49,0]}
{"wire":[63,0,60,0]}
{"wire":[63,1,168,0]}
{"wire":[64,0,54,0]}
{"wire":[64,1,58,0]}
{"wire":[69,0,63,1]}
{"wire":[69,1,64,0]}
{"wire":[78,0,74,0]}
{"wire":[78,1,73,0]}
{"wire":[82,0,77,2]}
{"wire":[82,1,74,0]}
{"wire":[82,2,78,0]}
{"wire":[83,0,82,0]}
{"wire":[83,1,79,0]}
{"wire":[86,0,83,0]}
{"wire":[93,0,86,0]}
{"wire":[93,1,85,0]}
{"wire":[100,0,93,0]}
{"wire":[111,0,105,0]}
{"wire":[130,0,136,2]}
{"wire":[130,1,100,0]}
{"wire":[125,0,123,0]}
{"wire":[125,1,120,0]}
{"wire":[140,0,130,0]}
{"wire":[141,0,125,0]}
{"wire":[42,0,44,0]}
{"wire":[42,1,138,0]}
{"wire":[8,0,7,1]}
{"wire":[8,1,7,2]}
{"wire":[32,0,8,0]}
{"wire":[32,1,33,0]}
{"wire":[9,0,32,0]}
{"wire":[137,0,152,0]}
{"wire":[67,0,155,0]}
{"wire":[33,0,31,0]}
{"wire":[33,1,141,0]}
{"wire":[33,2,42,0]}
{"wire":[72,0,199,0]}
{"wire":[72,1,69,0]}
{"wire":[51,0,154,0]}
{"wire":[156,0,101,2]}
{"wire":[156,1,160,0]}
{"wire":[157,0,156,0]}
{"wire":[159,0,162,0]}
{"wire":[162,0,157,0]}
{"wire":[162,1,161,0]}
{"wire":[59,0,52,0]}
{"wire":[59,2,53,0]}
{"wire":[168,0,59,0]}
{"wire":[168,1,170,0]}
{"wire":[170,0,169,0]}
{"wire":[139,0,140,0]}
{"wire":[139,1,141,0]}
{"wire":[185,0,53,0]}
{"wire":[185,1,186,0]}
{"wire":[186,0,184,0]}
{"wire":[118,0,189,0]}
{"wire":[118,1,111,0]}
{"wire":[123,0,118,0]}
{"wire":[188,0,113,0]}
{"wire":[188,1,190,0]}
{"wire":[189,0,188,0]}
{"wire":[190,0,187,0]}
{"wire":[187,0,192,0]}
{"wire":[187,1,193,0]}
{"wire":[136,0,112,0]}
{"wire":[136,1,107,0]}
{"wire":[106,0,100,0]}
{"wire":[106,1,159,0]}
{"wire":[113,0,106,0]}
{"wire":[194,0,136,2]}
{"wire":[194,1,159,0]}
{"wire":[195,0,194,0]}
{"wire":[163,0,196,0]}
{"wire":[65,0,61,0]}
{"wire":[65,1,62,0]}
{"wire":[197,0,65,0]}
{"wire":[197,1,198,0]}
{"wire":[165,0,65,0]}
{"wire":[165,1,167,0]}
{"wire":[199,0,197,0]}
{"wire":[199,2,200,0]}
{"wire":[77,1,72,0]}
{"wire":[15,0,139,0]}
{"wire":[75,0,70,0]}
{"wire":[75,1,71,0]}
{"wire":[84,0,81,0]}
{"wire":[84,1,171,0]}
{"wire":[89,0,84,0]}
{"wire":[95,3,89,0]}
{"wire":[97,0,91,0]}
{"wire":[97,1,90,0]}
{"wire":[102,0,95,0]}
{"wire":[102,1,96,0]}
{"wire":[103,0,97,0]}
{"wire":[103,2,98,0]}
{"wire":[80,0,75,0]}
{"wire":[80,2,76,0]}
{"wire":[171,0,80,0]}
{"wire":[171,1,172,0]}
{"wire":[174,0,103,0]}
{"wire":[174,1,176,0]}
{"wire":[107,0,102,0]}
{"wire":[107,1,103,0]}
{"wire":[176,0,175,0]}
{"wire":[172,0,173,0]}
{"wire":[180,0,76,0]}
{"wire":[180,1,179,0]}
{"wire":[181,0,98,0]}
{"wire":[181,1,182,0]}
{"wire":[179,0,178,0]}
{"wire":[182,0,183,0]}
{"wire":[210,0,9,0]}
{"wire":[211,0,9,0]}
{"wire":[211,1,212,0]}
{"wire":[213,0,9,0]}
{"wire":[213,1,214,0]}
{"wire":[202,0,211,0]}
{"wire":[202,1,213,0]}
{"wire":[202,2,16,4]}
ASEEND*/
//CHKSM=7B29FC6FB59A30942E8E0857B87786C2E9471C01