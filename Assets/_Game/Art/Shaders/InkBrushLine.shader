// 붓으로 그은 선 (수묵 시험판): 라인·트레일 렌더러용. _MainTex의 알파로 마른 붓 결을 그리고, 색은 _BaseColor × 꼭짓점 색.
// 공격 예고 테두리(원래 테두리 색 그대로, 붓선으로), 화살·표창 꼬리 먹선에 써요.
Shader "AThousandDawns/InkBrushLine"
{
    Properties
    {
        _MainTex ("Brush (알파)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.06, 0.06, 0.07, 1)
        _Grain ("Paper Grain (종이 결로 비는 정도)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "BrushLine"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Grain;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half brush = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                // 종이 결에 먹이 덜 묻은 듯 군데군데 비게
                half grain = Hash12(floor(input.positionWS.xz * 40.0));
                brush *= 1.0 - _Grain * step(0.82, grain);
                half4 c = _BaseColor * input.color;
                return half4(c.rgb, c.a * brush);
            }
            ENDHLSL
        }
    }
}
