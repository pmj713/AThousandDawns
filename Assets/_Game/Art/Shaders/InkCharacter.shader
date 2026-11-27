// 3D 캐릭터·소품용 채색 툰 셰이더 (URP). 이름은 예전 수묵 셰이더 그대로예요 (재질·스크립트가 이 이름과 속성을 써요).
// 텍스처·재질 원래 색에 빛 받는 면·그늘 두 단계, 그늘은 푸르스름하게. 뒷면을 부풀린 고른 가는 윤곽선을 따로 그려요.
// 수묵 시절 속성(채도·종이·번짐·먹 단계·종이 결)은 남아 있지만 이제 쓰지 않아요. 덧입힘(_AuraColor·_CrackColor·_Dissolve)은 그대로
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

        [Header(Overlay)]
        _AuraColor ("Aura (가장자리 기운, a = 세기)", Color) = (0, 0, 0, 0)
        _CrackColor ("Crack (갈라진 금, a = 양)", Color) = (0, 0, 0, 0)
        _Dissolve ("Dissolve (먹으로 흩어짐 0~1)", Range(0, 1)) = 0
        _VertexTint ("Vertex Tint (정점 색을 곱해요, 무기 부위 색)", Range(0, 1)) = 0

        [Header(Brush Outline)]
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 3
        _OutlineJitter ("Outline Brush Jitter", Range(0, 1)) = 0.7
        _OutlineNoiseScale ("Outline Noise Scale", Float) = 5

        // 몸통 면 그리기: 기본은 뒷면 안 그림(Back = 2). 한 겹 천(주인공 옷)은 Off(0)로 두면 소매·옷자락 안쪽도 그려요 (뒷면은 법선을 뒤집어 칠해요)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (몸통)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "InkRoomFog.hlsl"   // 맵 바깥 안개 (방 테두리에서 멀어질수록)

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
            half4 _AuraColor;
            half4 _CrackColor;
            half _Dissolve;
            half _VertexTint;
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

        // 3D 보로노이 칸 경계까지 거리 (가장 가까운 두 점 거리 차, 0이면 경계 위)
        float VoronoiEdge(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float d1 = 8.0, d2 = 8.0;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float3 o = float3(x, y, z);
                float3 c = i + o;
                float3 r = o + float3(Hash13(c), Hash13(c + 17.1), Hash13(c + 31.7)) - f;
                float d = dot(r, r);
                if (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) d2 = d;
            }
            return sqrt(d2) - sqrt(d1);
        }

        // 먹으로 흩어짐 (_Dissolve 0~1): 얼룩 노이즈에 발밑부터 차오르는 높이를 더한 값이 문턱 아래면 지워요.
        //   0이면 하나도 안 지워지고 1이면 다 지워져요. 반환값은 문턱까지 남은 거리 (0 근처가 타들어 가는 가장자리)
        float DissolveCut(float3 positionOS)
        {
            float n = Fbm(positionOS * 3.5 + 11.0);
            float rise = saturate(positionOS.y / 3.0);   // 발밑이 먼저 (몸이 아래부터 먹물로 풀려요)
            return n * 0.7 + rise * 0.5 - _Dissolve * 1.25;
        }
        ENDHLSL

        // 몸통: 먹 농담 단계 + 번짐 + 가장자리 먹 + 종이 결
        Pass
        {
            Name "InkForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                half4 color : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = InkRoomFog(pos.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                n = isFront ? n : -n;   // 뒷면(Cull Off일 때만 그려져요)은 안쪽을 향한 법선으로
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // 채색 툰 (수묵 걷어 냄): 텍스처·재질 원래 색 그대로, 빛 받는 면과 그늘 두 단계 (경계는 부드럽게).
                //   그늘은 색을 살린 채 푸르스름하게 어둡고, 하늘빛 환경광을 조금 더해 새까맣게 떨어지지 않아요. 가장자리에 옅은 역광
                half3 baseRgb = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                baseRgb *= lerp(half3(1, 1, 1), input.color.rgb, _VertexTint);   // 무기: 부위마다 칠한 정점 색 (칼날 강철 · 자루 나무 · 코등이 금속)
                float ndl = dot(n, light.direction);
                float lightAmt = smoothstep(-0.1, 0.2, ndl) * lerp(0.35, 1.0, light.shadowAttenuation);
                half3 lightCol = lerp(half3(1, 1, 1), light.color, 0.5);
                half3 color = baseRgb * lerp(half3(0.58, 0.6, 0.75), lightCol * 1.08, lightAmt);
                color += baseRgb * SampleSH(n) * 0.25;
                color += pow(1.0 - saturate(dot(n, v)), 4.0) * 0.18 * lightAmt;
                // 어두운 곳 밝히기 (InkLookSettings 밝기, 밝은 곳은 거의 그대로)
                color = 1.0 - (1.0 - color) * (1.0 - _InkCharLift * 0.3);

                // 갈라진 금 (분열해 나온 적): 몸 군데군데 보로노이 경계를 따라 짙은 금, 둘레는 금 색으로 번져요 (물체 공간이라 몸에 붙어 다녀요)
                if (_CrackColor.a > 0.001)
                {
                    float3 po = input.positionOS;
                    float3 warp = float3(ValueNoise(po * 3.0), ValueNoise(po * 3.0 + 7.3), ValueNoise(po * 3.0 + 13.7)) - 0.5;   // 금이 구불구불하게
                    float edge = VoronoiEdge((po + warp * 0.12) * 5.0);
                    float patch = smoothstep(0.55, 0.66, ValueNoise(po * 2.2 + 3.1));   // 금이 몰린 몇 군데만
                    float core = (1.0 - smoothstep(0.024, 0.07, edge)) * patch;
                    float halo = (1.0 - smoothstep(0.05, 0.2, edge)) * patch;
                    color = lerp(color, _CrackColor.rgb, halo * 0.45 * _CrackColor.a);
                    color = lerp(color, _CrackColor.rgb * 0.22, core * _CrackColor.a);
                }
                // 가장자리 기운 (정예 특성 색): 실루엣 가장자리가 은은히 물들어요
                if (_AuraColor.a > 0.001)
                {
                    float glow = pow(1.0 - saturate(dot(n, v)), 2.2);
                    color = lerp(color, _AuraColor.rgb, saturate(glow * _AuraColor.a * 1.6));
                }
                // 먹으로 흩어짐: 지워지는 자리 둘레는 짙은 먹으로 번져요
                if (_Dissolve > 0.001)
                {
                    float cut = DissolveCut(input.positionOS);
                    clip(cut);
                    color = lerp(_InkColor.rgb, color, smoothstep(0.0, 0.08, cut));
                }

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
                float3 positionOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalize(normalCS.xy + 1e-5);
                float brush = 0.6;   // 채색 전환: 붓 떨림 없는 고른 가는 윤곽 (_OutlineJitter는 이제 안 써요)
                positionCS.xy += dir * (_OutlineWidth * (1.0 - _InkCharOutlineThin) * brush * 2.0 / _ScreenParams.xy) * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = InkRoomFog(positionWS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Dissolve > 0.001) clip(DissolveCut(input.positionOS));   // 몸과 같이 외곽선도 흩어져요
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

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            ShadowVaryings Vert(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                ShadowVaryings output;
                output.positionCS = positionCS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(ShadowVaryings input) : SV_Target
            {
                if (_Dissolve > 0.001) clip(DissolveCut(input.positionOS));   // 그림자도 같이 흩어져요
                return 0;
            }
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

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            DepthVaryings Vert(float4 positionOS : POSITION)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.positionOS = positionOS.xyz;
                return output;
            }

            half Frag(DepthVaryings input) : SV_Target
            {
                if (_Dissolve > 0.001) clip(DissolveCut(input.positionOS));   // 깊이에도 흩어진 자리가 남지 않게
                return 0;
            }
            ENDHLSL
        }
    }
}
