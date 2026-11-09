// 대충 만든 3D 구조물(상자·원기둥 등)용 수묵 셰이더 (URP).
// 텍스처를 월드 기준 세 방향(위·앞·옆)에서 투영해 UV 없이 입히고, 그 위에 InkCharacter와 같은
// 먹 농담 단계·번짐·가장자리 먹·붓선 외곽선을 얹어요. 상자를 늘려도 텍스처가 늘어나지 않아요.
Shader "AThousandDawns/InkTriplanar"
{
    Properties
    {
        _BaseMap ("Texture (이음새 없는 것)", 2D) = "white" {}
        _TileSize ("Tile Size (월드 칸)", Float) = 3
        _BlendSharpness ("Projection Blend Sharpness", Range(1, 16)) = 6
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation (0 = 흑백)", Range(0, 1)) = 0.1
        _PaperLift ("Paper Lift", Range(0, 1)) = 0.2
        _PaperColor ("Paper", Color) = (0.93, 0.91, 0.86, 1)
        _InkColor ("Ink", Color) = (0.07, 0.07, 0.08, 1)

        [Header(Wash)]
        _Bands ("Ink Bands", Range(2, 5)) = 3
        _BandSoftness ("Band Softness", Range(0.01, 0.5)) = 0.15
        _ShadowInk ("Shadow Ink", Range(0, 1)) = 0.5
        _WashNoiseScale ("Wash Noise Scale", Float) = 1.5
        _WashNoiseStrength ("Wash Noise Strength", Range(0, 0.6)) = 0.25
        _RimInk ("Rim Ink", Range(0, 1)) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _PaperGrain ("Paper Grain", Range(0, 0.2)) = 0.05

        [Header(Brush Outline)]
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 2.5
        _OutlineJitter ("Outline Brush Jitter", Range(0, 1)) = 0.7
        _OutlineNoiseScale ("Outline Noise Scale", Float) = 2
        _OutlineFromCenter ("Outline From Center (각진 물체는 1)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float _TileSize;
            half _BlendSharpness;
            half4 _BaseColor;
            half _Saturation;
            half _PaperLift;
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
            half _OutlineFromCenter;
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
            float a = lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), f.x),
                           lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), f.x), f.y);
            float b = lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), f.x),
                           lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), f.x), f.y);
            return lerp(a, b, f.z);
        }

        float Fbm(float3 p)
        {
            return (ValueNoise(p) * 0.5 + ValueNoise(p * 2.03) * 0.25 + ValueNoise(p * 4.01) * 0.125) / 0.875;
        }
        ENDHLSL

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

            half3 SampleTriplanar(float3 positionWS, float3 n)
            {
                float3 w = pow(abs(n), _BlendSharpness);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 p = positionWS / max(_TileSize, 0.01);
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy).rgb;
                return x * w.x + y * w.y + z * w.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float lit = dot(n, light.direction) * 0.5 + 0.5;
                lit *= lerp(1.0 - _ShadowInk, 1.0, light.shadowAttenuation);

                float wash = Fbm(input.positionWS * _WashNoiseScale);
                float t = saturate(lit + (wash - 0.5) * _WashNoiseStrength);

                float steps = max(_Bands - 1.0, 1.0);
                float x = t * steps;
                float fl = floor(x);
                float tone = saturate((fl + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, x - fl)) / steps);

                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimInk;
                tone *= 1.0 - rim;

                // 텍스처: 채도를 낮추고 종이색 쪽으로 조금 띄워요. 텍스처의 어두운 결은 그대로 먹으로 남아요
                half3 albedo = SampleTriplanar(input.positionWS, n) * _BaseColor.rgb;
                albedo = lerp(Luminance(albedo).xxx, albedo, _Saturation);
                albedo = lerp(albedo, _PaperColor.rgb, _PaperLift);
                half3 color = lerp(_InkColor.rgb, albedo, tone);

                float grain = Hash13(float3(floor(input.positionCS.xy * 0.5), 7.0));
                color *= 1.0 - (grain - 0.5) * _PaperGrain;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 붓선 외곽선. 각진 물체(상자)는 법선이 모서리에서 갈라져 선이 끊기니,
        // _OutlineFromCenter = 1 이면 물체 가운데에서 바깥쪽으로 화면상 부풀려요.
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
                float2 fromNormal = normalize(normalCS.xy + 1e-5);
                float4 centerCS = TransformObjectToHClip(float3(0, 0, 0));
                float2 fromCenter = positionCS.xy / positionCS.w - centerCS.xy / centerCS.w;
                fromCenter = normalize(fromCenter + 1e-5);
                float2 dir = normalize(lerp(fromNormal, fromCenter, _OutlineFromCenter) + 1e-5);

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
