// 3D 캐릭터용 수묵 셰이더 (URP).
// 명암을 먹 농담 몇 단계로 끊고, 단계 경계를 노이즈로 번지게 해요. 실루엣 안쪽은 먹을 더 얹고,
// 뒷면을 부풀린 붓선 외곽선(굵기를 노이즈로 흔듦)을 따로 그려요. 색은 채도로 조절 (0이면 흑백).
Shader "AThousandDawns/InkCharacter"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _Saturation ("Saturation (0 = 흑백)", Range(0, 1)) = 0.15
        _PaperColor ("Paper", Color) = (0.93, 0.91, 0.86, 1)
        _InkColor ("Ink", Color) = (0.07, 0.07, 0.08, 1)

        [Header(Wash)]
        _Bands ("Ink Bands", Range(2, 5)) = 3
        _BandSoftness ("Band Softness", Range(0.01, 0.5)) = 0.12
        _ShadowInk ("Shadow Ink", Range(0, 1)) = 0.6
        _WashNoiseScale ("Wash Noise Scale", Float) = 3
        _WashNoiseStrength ("Wash Noise Strength", Range(0, 0.6)) = 0.25
        _RimInk ("Rim Ink", Range(0, 1)) = 0.55
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _PaperGrain ("Paper Grain", Range(0, 0.2)) = 0.06

        [Header(Brush Outline)]
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 3
        _OutlineJitter ("Outline Brush Jitter", Range(0, 1)) = 0.7
        _OutlineNoiseScale ("Outline Noise Scale", Float) = 5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Saturation;
            half4 _PaperColor;
            half4 _InkColor;
            half _Bands;
            half _BandSoftness;
            half _ShadowInk;
            float _WashNoiseScale;
            half _WashNoiseStrength;
            half _RimInk;
            half _RimPower;
            half _PaperGrain;
            half _OutlineWidth;
            half _OutlineJitter;
            float _OutlineNoiseScale;
        CBUFFER_END

        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = Hash13(i);
            float n100 = Hash13(i + float3(1, 0, 0));
            float n010 = Hash13(i + float3(0, 1, 0));
            float n110 = Hash13(i + float3(1, 1, 0));
            float n001 = Hash13(i + float3(0, 0, 1));
            float n101 = Hash13(i + float3(1, 0, 1));
            float n011 = Hash13(i + float3(0, 1, 1));
            float n111 = Hash13(i + float3(1, 1, 1));
            float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
            float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
            return lerp(a, b, f.z);
        }

        float Fbm(float3 p)
        {
            return (ValueNoise(p) * 0.5 + ValueNoise(p * 2.03) * 0.25 + ValueNoise(p * 4.01) * 0.125) / 0.875;
        }
        ENDHLSL

        // 몸통: 먹 농담 단계 + 번짐 + 가장자리 먹 + 종이 결
        Pass
        {
            Name "InkForward"
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // 빛: 반 람베르트, 그림자는 먹을 조금 더 얹는 정도로만
                float lit = dot(n, light.direction) * 0.5 + 0.5;
                lit *= lerp(1.0 - _ShadowInk, 1.0, light.shadowAttenuation);

                // 번짐: 단계 경계를 노이즈로 흔들어요
                float wash = Fbm(input.positionWS * _WashNoiseScale);
                float t = saturate(lit + (wash - 0.5) * _WashNoiseStrength);

                // 먹 농담 단계 (경계는 부드럽게)
                float steps = max(_Bands - 1.0, 1.0);
                float x = t * steps;
                float fl = floor(x);
                float tone = saturate((fl + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, x - fl)) / steps);

                // 실루엣 안쪽 가장자리에 먹
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimInk;
                tone *= 1.0 - rim;

                // 바탕색: 채도를 낮추고 종이색 쪽으로 조금 밝혀요
                half3 gray = Luminance(_BaseColor.rgb).xxx;
                half3 tint = lerp(gray, _BaseColor.rgb, _Saturation);
                half3 paperLit = lerp(tint, _PaperColor.rgb, 0.35);
                half3 color = lerp(_InkColor.rgb, paperLit, tone);

                // 종이 결
                float grain = Hash13(float3(floor(input.positionCS.xy * 0.5), 7.0));
                color *= 1.0 - (grain - 0.5) * _PaperGrain;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 붓선 외곽선: 뒷면을 화면 기준 픽셀만큼 부풀려 먹색으로
        Pass
        {
            Name "InkOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalize(normalCS.xy + 1e-5);
                float brush = lerp(1.0, ValueNoise(positionWS * _OutlineNoiseScale) * 1.8, _OutlineJitter);
                positionCS.xy += dir * (_OutlineWidth * brush * 2.0 / _ScreenParams.xy) * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_InkColor.rgb, input.fogFactor), 1.0);
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
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
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

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
