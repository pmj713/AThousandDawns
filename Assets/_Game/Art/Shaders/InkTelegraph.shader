// 적 공격 예고 (수묵 시험판). 원래 색·밝기는 그대로 두고 붓 자국 같은 농담과 종이 결만 얹어요 (읽히는 건 그대로).
Shader "AThousandDawns/InkTelegraph"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.6, 0.18, 1)
        _Brush ("Brush Variation", Range(0, 0.4)) = 0.18
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brush;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x),
                            lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                // 한 방향으로 그은 붓 자국 같은 길쭉한 농담 + 잔 종이 결
                float stroke = Noise2(float2(p.x * 1.2 + p.y * 0.4, p.y * 6.0)) * 0.7 + Noise2(p * 9.0) * 0.3;
                half shade = 1.0 + (stroke - 0.5) * 2.0 * _Brush;
                return half4(_BaseColor.rgb * shade, 1.0);
            }
            ENDHLSL
        }
    }
}
