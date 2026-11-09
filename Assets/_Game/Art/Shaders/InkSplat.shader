// 바닥 먹물 튀김 (InkHitSplatter). 판 가운데 번진 얼룩 + 둘레로 흩어진 작은 방울.
// _Bloom으로 퍼지고, _Fade로 가장자리부터 마르듯 사라져요 (알파 클립이라 정렬 문제 없음).
Shader "AThousandDawns/InkSplat"
{
    Properties
    {
        _Color ("Color", Color) = (0.06, 0.06, 0.07, 1)
        _Bloom ("Bloom (퍼짐)", Range(0, 1)) = 1
        _Fade ("Fade (마름)", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off

        Pass
        {
            Name "Splat"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Bloom;
                half _Fade;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                float r = length(c);
                float angle = atan2(c.y, c.x);

                // 가운데 얼룩: 각도마다 반지름이 들쭉날쭉
                float rim = 0.38 + Noise2(float2(angle * 2.2 + _Seed, _Seed)) * 0.22;
                float blob = smoothstep(rim * _Bloom, rim * _Bloom - 0.06, r);

                // 둘레 방울: 칸마다 하나씩, 크기 제각각
                float2 cell = floor((c + 1.0) * 5.0);
                float2 local = frac((c + 1.0) * 5.0) - 0.5;
                float h = Hash12(cell + _Seed);
                float dropSize = step(0.72, h) * (0.12 + Hash12(cell * 1.7 + _Seed) * 0.22) * _Bloom;
                float drop = smoothstep(dropSize, dropSize - 0.05, length(local)) * step(rim * 0.9, r) * step(r, 0.95);

                float ink = max(blob, drop);
                // 마름: 가장자리·방울부터 사라지게 (노이즈로 문턱을 흔들어요)
                float dryEdge = r + Noise2(c * 6.0 + _Seed) * 0.25;
                clip(ink - 0.5 - step(1.0 - _Fade * 1.2, dryEdge));

                half3 color = _Color.rgb * lerp(1.0, 1.35, smoothstep(0.0, rim, r)); // 가운데가 더 진해요
                return half4(MixFog(color, input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
}
