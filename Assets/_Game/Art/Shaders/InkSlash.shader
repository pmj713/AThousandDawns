// 평타 칼끝 궤적 (InkSlashFX가 그리는 바닥 호). uv.x = 휘두른 방향(0 시작 → 1 끝), uv.y = 안쪽(0) → 바깥(1).
// 칼끝(_Progress)을 따라 가는 초승달 획이 지나가고, 뒤따르는 꼬리는 가늘어지며 마른 붓처럼 갈라져요.
Shader "AThousandDawns/InkSlash"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.06, 0.06, 0.07, 1)
        _WashColor ("Wash (번짐)", Color) = (0.25, 0.25, 0.27, 1)
        _Progress ("Progress (칼끝 위치)", Range(0, 2)) = 1
        _Trail ("Trail (꼬리 길이)", Range(0.05, 1)) = 0.5
        _Fade ("Fade", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Slash"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half4 _WashColor;
                half _Progress;
                half _Trail;
                half _Fade;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.uv.x, v = input.uv.y;

                // 칼끝 궤적: 머리(_Progress)가 앞서 가고 꼬리(_Progress - _Trail)가 뒤따라와요.
                // trail = 0(꼬리 끝) → 1(머리). 머리를 넘어선 곳은 아직 안 그어졌고, 꼬리 뒤는 이미 사라졌어요
                float trail = saturate((u - (_Progress - _Trail)) / max(_Trail, 0.01));
                float ahead = smoothstep(_Progress + 0.02, _Progress - 0.01, u);

                // 초승달: 머리 쪽이 가장 굵고 꼬리로 갈수록 가늘게 (바깥 가장자리 쪽에 붙어서)
                float thickness = lerp(0.04, 0.42, trail * trail);
                float edgeNoise = (Noise2(float2(u * 14.0 + _Seed, v * 2.0)) - 0.5) * 0.12;
                float body = 1.0 - smoothstep(thickness - 0.06, thickness + 0.03, abs(v - 0.62) * 2.0 + edgeNoise);

                // 꼬리는 마른 붓처럼 결이 갈라져요
                float bristle = Noise2(float2(u * 4.0 + _Seed, v * 42.0));
                float dry = smoothstep(0.55 - trail * 0.5, 0.75 - trail * 0.4, bristle);
                float alpha = body * lerp(dry, 1.0, trail) * ahead * smoothstep(0.0, 0.25, trail);

                half3 color = lerp(_WashColor.rgb, _InkColor.rgb, saturate(trail * 1.4));
                alpha *= 1.0 - _Fade;
                clip(alpha - 0.02);
                return half4(color, alpha * 0.92);
            }
            ENDHLSL
        }
    }
}
