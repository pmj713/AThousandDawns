// 3D 캐릭터용 수묵 셰이더 (URP).
// 명암을 먹 농담 몇 단계로 끊고, 단계 경계를 노이즈로 번지게 해요. 실루엣 안쪽은 먹을 더 얹고,
// 뒷면을 부풀린 붓선 외곽선(굵기를 노이즈로 흔듦)을 따로 그려요. 색은 채도로 조절 (0이면 흑백).
Shader "AThousandDawns/InkCharacter"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _BaseMap ("Base Map (색 텍스처, 없으면 흰색)", 2D) = "white" {}
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

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
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

            // 지역 색 양 (InkColorZones가 전역으로 정해요): 0 = 재질 채도 그대로(수묵), 1 = 원래 색 전부
            float _InkColorAmount;
            // 수묵 강도 조절 (InkLookSettings가 전역으로 정해요, 모두 0이면 원래 모습)
            //   _InkCharSoften: 0 = 수묵 그대로, 1 = 먹 단계·가장자리 먹 없이 원래 색과 부드러운 명암
            //   _InkCharColorFloor: 지역이 바래도 캐릭터·소품에 남길 최소 색 (0~1)
            float _InkCharSoften;
            float _InkCharColorFloor;
            //   _InkCharOutlineThin: 0 = 붓 외곽선 그대로, 1 = 외곽선 없음 (작은 캐릭터가 먹 덩어리로 뭉개지지 않게)
            //   _InkCharLift: 0 = 그대로, 1 = 어두운 곳을 크게 밝혀요 (짙은 옷 주름·얼굴이 보이게)
            float _InkCharOutlineThin;
            float _InkCharLift;

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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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
                half3 baseRgb = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 gray = Luminance(baseRgb).xxx;
                half3 tint = lerp(gray, baseRgb, max(lerp(_Saturation, 1.0, _InkColorAmount), _InkCharColorFloor));
                half3 paperLit = lerp(tint, _PaperColor.rgb, lerp(0.35, 0.12, _InkColorAmount));
                half3 color = lerp(_InkColor.rgb, paperLit, tone);

                // 종이 결
                float grain = Hash13(float3(floor(input.positionCS.xy * 0.5), 7.0));
                color *= 1.0 - (grain - 0.5) * _PaperGrain;

                // 수묵 강도 줄이기: 먹 단계·가장자리 먹 대신 원래 색에 부드러운 명암 (캐릭터가 배경에서 또렷하게)
                half3 plain = lerp(tint, baseRgb, _InkCharSoften) * lerp(0.55, 1.05, lit);
                color = lerp(color, plain, _InkCharSoften);
                // 어두운 곳 밝히기 (밝은 곳은 거의 그대로)
                color = 1.0 - (1.0 - color) * (1.0 - _InkCharLift * 0.5);

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
                positionCS.xy += dir * (_OutlineWidth * (1.0 - _InkCharOutlineThin) * brush * 2.0 / _ScreenParams.xy) * positionCS.w;

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
