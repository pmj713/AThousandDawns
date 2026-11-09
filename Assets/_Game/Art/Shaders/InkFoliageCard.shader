// 잎·덤불 카드용 셰이더 (URP). 투명 배경 PNG 그림을 판에 붙여 써요.
// 투명한 곳은 잘라내(알파 클립) 앞뒤 정렬 문제가 없고, 양면으로 그리고, 바닥에 잎 그림자를 드리워요.
// 그림자 속에서는 먹을 한 겹 더 얹어 수묵 구조물(InkTriplanar)과 같은 규칙으로 어두워져요.
//
// Billboard = 1 이면 덤불 덩어리 메시용: 카드마다 꼭짓점 넷이 카드 가운데에 모여 있고 TEXCOORD1에 모서리 오프셋(월드 단위)이
// 들어 있어, 정점 셰이더가 카메라를 정면으로 보게 펼쳐요 (위에서 내려다봐도 카드 옆면이 안 보여요).
// Normal Ink는 덩어리 중심에서 바깥쪽 법선으로 빛 반대편을 진하게 해 덩어리가 입체로 보이게 해요.
Shader "AThousandDawns/InkFoliageCard"
{
    Properties
    {
        _BaseMap ("Texture (투명 배경)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _InkColor ("Ink", Color) = (0.07, 0.07, 0.08, 1)
        _ShadowInk ("Shadow Ink", Range(0, 1)) = 0.45
        _BottomInk ("Bottom Ink (카드 아래쪽을 진하게)", Range(0, 1)) = 0.25
        _NormalInk ("Normal Ink (빛 반대편을 진하게)", Range(0, 1)) = 0
        [Toggle] _Billboard ("Billboard (덤불 덩어리 메시용)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Cutoff;
            half4 _Tint;
            half4 _InkColor;
            half _ShadowInk;
            half _BottomInk;
            half _NormalInk;
            float _Billboard;
        CBUFFER_END

        // 카드 꼭짓점의 월드 위치. 빌보드면 현재 뷰(그림자 패스에서는 빛 기준 뷰)를 정면으로 보게 펼쳐요
        float3 FoliagePositionWS(float3 positionOS, float2 cornerOffset)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            if (_Billboard > 0.5)
            {
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                positionWS += (right * cornerOffset.x + up * cornerOffset.y) * scale;
            }
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "FoliageForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 corner : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = FoliagePositionWS(input.positionOS.xyz, input.corner);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half facing = dot(normalize(input.normalWS), light.direction) * 0.5 + 0.5;
                half ink = (1.0 - light.shadowAttenuation) * _ShadowInk
                         + (1.0 - input.uv.y) * _BottomInk
                         + (1.0 - facing) * _NormalInk;
                half3 color = lerp(tex.rgb * _Tint.rgb, _InkColor.rgb, saturate(ink));
                return half4(MixFog(color, input.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 corner : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = FoliagePositionWS(input.positionOS.xyz, input.corner);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 corner : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(FoliagePositionWS(input.positionOS.xyz, input.corner));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
