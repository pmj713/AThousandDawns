// 차원 균열 (차원 겹침): 허공이 세로로 찢어진 틈. 가운데는 희게 타고 가장자리는 보랏빛으로 일렁이며, 테두리는 들쭉날쭉 떨려요.
// 판(Quad)의 UV 가로 가운데가 틈의 중심선, 위아래 끝으로 갈수록 가늘어져요. 더해지는 빛(가산)이라 먹 화면 위에서 또렷해요.
// 정적 배칭으로 묶여도 저마다 다르게 일렁이도록 흔들림 씨앗은 정점의 월드 위치에서 얻어요.
// _Boost(InkRiftGlow가 재질 복사본에 줘요): 판타지 적이 방에 있으면 1로 올라 틈이 더 밝고 넓어져요. 0이면 평소 밝기
// (전역 값으로 주면 다른 셰이더의 전역 값 묶음이 흐트러져 대나무 잎이 파랗게 나왔어요)
Shader "AThousandDawns/InkRift"
{
    Properties
    {
        _CoreColor ("Core", Color) = (1.0, 0.92, 1.0, 1)
        _EdgeColor ("Edge", Color) = (0.55, 0.22, 0.95, 1)
        _Speed ("Speed", Float) = 1.0
        _Width ("Width", Range(0.1, 1)) = 0.55
        _Boost ("Boost (판타지 적이 있을 때 1)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Rift"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                float _Speed;
                half _Width;
                half _Boost;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float seed : TEXCOORD1;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash12(float2(i, 7.1)), Hash12(float2(i + 1.0, 7.1)), f);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.seed = (world.x + world.z) * 0.37;   // 이어진 값 (판 하나 안에서 끊기지 않게)
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed + input.seed;
                float taper = pow(saturate(sin(uv.y * PI)), 0.7);                          // 위아래 끝이 가늘게
                float jag = Noise1(uv.y * 9.0 + t * 1.7) * 0.55 + Noise1(uv.y * 23.0 - t * 2.3) * 0.25;
                float width = _Width * taper * (0.45 + jag) * (1.0 + 0.35 * _Boost);
                float d = abs(uv.x - 0.5 + (Noise1(uv.y * 4.0 + t) - 0.5) * 0.12) * 2.0;   // 중심선도 조금 굽이쳐요
                float a = saturate(1.0 - d / max(width, 1e-3));
                float core = pow(a, 5.0);
                half3 color = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
                float pulse = 0.75 + 0.25 * sin(t * 2.0 + uv.y * 7.0);
                float alpha = smoothstep(0.0, 0.35, a) * pulse * (0.75 + 0.6 * _Boost);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
