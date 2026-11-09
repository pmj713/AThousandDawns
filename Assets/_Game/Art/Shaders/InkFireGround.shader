// 바닥 불길 (불타는 죽림 띠, 불 장판). 월드 기준으로 흐르는 노이즈가 붉은 불길·노란 불씨·먹빛 그을음으로 일렁여요.
// 수묵 흑백 화면에서 붉은 포인트가 되도록 밝고 진하게. 빛·그림자 영향 없음.
Shader "AThousandDawns/InkFireGround"
{
    Properties
    {
        _EmberColor ("Ember (짙은 불)", Color) = (0.55, 0.07, 0.03, 1)
        _FlameColor ("Flame", Color) = (1.0, 0.38, 0.08, 1)
        _CoreColor ("Core (불씨)", Color) = (1.0, 0.85, 0.45, 1)
        _InkColor ("Soot (그을음)", Color) = (0.1, 0.08, 0.07, 1)
        _Speed ("Speed", Float) = 0.8
        _Scale ("Scale", Float) = 1.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "FireGround"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmberColor;
                half4 _FlameColor;
                half4 _CoreColor;
                half4 _InkColor;
                float _Speed;
                float _Scale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

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
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * _Scale;
                float t = _Time.y * _Speed;
                float heat = Noise2(p + float2(t * 0.6, -t)) * 0.55 + Noise2(p * 2.7 + float2(-t * 1.3, t * 0.4)) * 0.3
                           + Noise2(p * 6.1 + float2(0, -t * 2.2)) * 0.15;

                half3 color = lerp(_EmberColor.rgb, _FlameColor.rgb, smoothstep(0.35, 0.7, heat));
                color = lerp(color, _CoreColor.rgb, smoothstep(0.72, 0.9, heat));
                color = lerp(color, _InkColor.rgb, smoothstep(0.3, 0.18, heat)); // 식은 곳은 먹빛 그을음
                return half4(MixFog(color, input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
}
