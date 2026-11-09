// 세운 불꽃 카드 (불타는 대나무, 화톳불). 판 하나에 노이즈로 일렁이는 불꽃 모양을 그려요:
// 아래는 노란 불씨, 위로 갈수록 붉은 불길, 꼭대기 가장자리는 먹빛 연기. 카드마다 위치로 무늬가 달라요.
// 반투명, 양면, 빛 영향 없음. 카드를 엇갈려 꽂아 써요.
Shader "AThousandDawns/InkFlame"
{
    Properties
    {
        _CoreColor ("Core", Color) = (1.0, 0.85, 0.45, 1)
        _FlameColor ("Flame", Color) = (0.95, 0.3, 0.07, 1)
        _SmokeColor ("Smoke (먹빛 연기)", Color) = (0.12, 0.1, 0.1, 1)
        _Speed ("Speed", Float) = 1.6
        _Wobble ("Wobble", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _FlameColor;
                half4 _SmokeColor;
                float _Speed;
                half _Wobble;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float seed : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                output.uv = input.uv;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = Hash12(origin.xz * 3.7) * 17.0;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed + input.seed;
                // 위로 흐르는 노이즈로 불꽃 가장자리를 흔들어요
                float n = Noise2(float2(uv.x * 3.0 + input.seed, uv.y * 2.5 - t)) * 0.65
                        + Noise2(float2(uv.x * 7.0 - input.seed, uv.y * 6.0 - t * 1.7)) * 0.35;
                float width = (1.0 - uv.y) * 0.55 + 0.08;
                float shape = width - abs(uv.x - 0.5 + (n - 0.5) * _Wobble * uv.y) + (n - 0.5) * 0.35 * uv.y;
                shape = shape * 2.2 - uv.y * 0.35;
                float alpha = smoothstep(0.02, 0.18, shape);
                clip(alpha - 0.01);

                half3 color = lerp(_FlameColor.rgb, _CoreColor.rgb, smoothstep(0.35, 0.8, shape) * (1.0 - uv.y));
                half smoke = smoothstep(0.45, 0.95, uv.y) * (1.0 - smoothstep(0.1, 0.4, shape));
                color = lerp(color, _SmokeColor.rgb, smoke);
                alpha *= lerp(1.0, 0.75, smoke);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
