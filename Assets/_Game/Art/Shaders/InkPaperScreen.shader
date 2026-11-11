// 화면 전체 수묵 질감 (URP 전체 화면 패스, InkPaperPostFX가 씀).
// 색을 조금 바래게 하되 붉은색(플레이어·결계·공격 예고·불)은 남기고, 한지색으로 물들이고,
// 한지 결과 섬유를 얹고, 화면 가장자리는 들쭉날쭉하게 한지색으로 바래요 (족자 그림처럼).
Shader "AThousandDawns/InkPaperScreen"
{
    Properties
    {
        _PaperColor ("Paper", Color) = (0.93, 0.91, 0.86, 1)
        _Desaturate ("Desaturate (색 바램)", Range(0, 1)) = 0.35
        _KeepRed ("Keep Red (붉은색은 남김)", Range(0, 1)) = 0.85
        _PaperTint ("Paper Tint (한지색 물들임)", Range(0, 1)) = 0.3

        [Header(Paper Texture)]
        _Grain ("Grain (한지 결)", Range(0, 0.2)) = 0.05
        _Fiber ("Fiber (종이 섬유)", Range(0, 0.2)) = 0.06

        [Header(Edge Fade)]
        _EdgeStart ("Edge Start", Range(0, 1.2)) = 0.8
        _EdgeWidth ("Edge Width", Range(0.05, 1)) = 0.2
        _EdgeFade ("Edge Fade (가장자리 바램)", Range(0, 1)) = 0.55
        _EdgeRagged ("Edge Ragged (번진 테두리)", Range(0, 0.4)) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "InkPaper"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PaperColor;
                half _Desaturate;
                half _KeepRed;
                half _PaperTint;
                half _Grain;
                half _Fiber;
                half _EdgeStart;
                half _EdgeWidth;
                half _EdgeFade;
                half _EdgeRagged;
            CBUFFER_END

                // 지역 색 양 (InkColorZones가 전역으로 정해요): 0 = 재질 채도 그대로(수묵), 1 = 원래 색 전부
                float _InkColorAmount;
                // 화면 종이 효과 줄이기 (InkLookSettings가 전역으로 정해요): 0 = 그대로, 1 = 효과 없음
                float _InkScreenSoften;

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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                half3 original = color;

                // 색 바램: 붉은 기가 강한 곳은 덜 바래요 (공격 예고·결계·플레이어가 읽히게)
                half luminance = Luminance(color);
                half red = saturate((color.r - max(color.g, color.b)) * 3.0);
                color = lerp(color, luminance.xxx, _Desaturate * (1.0 - _InkColorAmount) * (1.0 - red * _KeepRed));

                // 한지색 물들임 (밝은 곳일수록 종이색에 가깝게)
                color *= lerp(1.0, _PaperColor.rgb * 1.07, _PaperTint);

                // 가장자리: 노이즈로 들쭉날쭉하게 한지색으로 바래요
                float2 centered = (uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float ragged = (Noise2(uv * 5.0) - 0.5) * 2.0 + (Noise2(uv * 19.0) - 0.5) * 0.6;
                float edge = smoothstep(_EdgeStart, _EdgeStart + _EdgeWidth, length(centered) + ragged * _EdgeRagged);
                color = lerp(color, _PaperColor.rgb, edge * _EdgeFade);

                // 한지 결(화소 단위 잔무늬)과 섬유(가늘고 긴 결) — 화면에 고정돼 종이 위에 그린 느낌
                float2 px = input.positionCS.xy;
                float grain = Hash12(floor(px * 0.75)) - 0.5;
                float fiber = Noise2(px * float2(0.012, 0.18)) * 0.6 + Noise2(px.yx * float2(0.015, 0.2)) * 0.4 - 0.5;
                color *= 1.0 + grain * _Grain + fiber * _Fiber;

                color = lerp(color, original, _InkScreenSoften);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
