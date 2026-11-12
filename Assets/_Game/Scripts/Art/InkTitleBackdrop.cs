using AThousandDawns.Items;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 시작 화면 배경 그림에 움직임을 얹어요 (TitleScreen.BackdropFx: 배경 그림 바로 뒤, 잎·제목·메뉴보다 먼저 그려요).
    //  - 안개 띠 두세 겹이 서로 다른 빠르기로 옆으로 흘러요. 오른쪽 벼랑의 주인공 앞에서는 걷혀요
    //  - 해·빛이 있는 그림은 그 자리에 은은한 빛 번짐이 숨 쉬듯 밝아졌다 어두워져요 (천마총은 붉게 끓어요)
    // 자리는 그림(1536×864) 기준 비율이라 화면 크기·잘림(ScaleAndCrop)에 맞춰 옮겨요. 씬에 붙일 것 없이 시작할 때 스스로 걸어요
    public static class InkTitleBackdrop
    {
        struct Band
        {
            public float y, height, speed, alpha, fadeFrom, fadeIn;   // 그림 비율 높이·두께, UV/초, 진하기, 이 가로 비율부터 걷혀요, 이 가로 비율부터 피어나요
            public Color color;
            public Band(float y, float height, float speed, float alpha, float fadeFrom, Color color, float fadeIn = 0f)
            { this.y = y; this.height = height; this.speed = speed; this.alpha = alpha; this.fadeFrom = fadeFrom; this.color = color; this.fadeIn = fadeIn; }
        }

        struct Glow
        {
            public Vector2 at; public float radius, alpha, pulse; public Color color;
            public Glow(Vector2 at, float radius, float alpha, float pulse, Color color)
            { this.at = at; this.radius = radius; this.alpha = alpha; this.pulse = pulse; this.color = color; }
        }

        static readonly Color Mist = new Color(0.95f, 0.92f, 0.86f);
        static readonly Color WarmMist = new Color(0.97f, 0.9f, 0.82f);
        static readonly Color Smoke = new Color(0.22f, 0.2f, 0.21f);

        static Texture2D _mist, _glow;
        const int Slices = 24;          // 띠를 세로로 잘라 조각마다 진하기를 달리 (주인공 앞에서 걷히게)
        const float Repeat = 1.6f;      // 안개 무늬가 화면 너비에 되풀이되는 수

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            TitleScreen.BackdropFx -= Draw;
            TitleScreen.BackdropFx += Draw;
        }

        // 그림마다 한 번만 만들어 두어요 (OnGUI는 한 프레임에 여러 번 불려요)
        static readonly System.Collections.Generic.Dictionary<string, (Band[] bands, Glow[] glows)> s_looks = new System.Collections.Generic.Dictionary<string, (Band[] bands, Glow[] glows)>();

        static (Band[] bands, Glow[] glows) Look(string name)
        {
            if (!s_looks.TryGetValue(name, out var look)) s_looks[name] = look = MakeLook(name);
            return look;
        }

        static (Band[] bands, Glow[] glows) MakeLook(string name) => name switch
        {
            "Bg_Bamboo" => (new[]
            {
                new Band(0.46f, 0.10f, 0.010f, 0.35f, 0.78f, Mist),
                new Band(0.62f, 0.14f, -0.007f, 0.32f, 0.70f, Mist),
                new Band(0.80f, 0.16f, 0.013f, 0.26f, 0.66f, Mist),
            }, new[] { new Glow(new Vector2(0.725f, 0.373f), 0.11f, 0.6f, 0.6f, new Color(1f, 0.93f, 0.78f)) }),
            "Bg_Temple" => (new[]
            {
                new Band(0.40f, 0.10f, 0.009f, 0.30f, 0.76f, Mist),
                new Band(0.57f, 0.14f, -0.011f, 0.28f, 0.74f, Mist),
                new Band(0.78f, 0.14f, 0.008f, 0.22f, 0.70f, Mist),
            }, new Glow[0]),
            "Bg_River" => (new[]
            {
                new Band(0.38f, 0.10f, 0.010f, 0.30f, 0.74f, Mist),
                new Band(0.55f, 0.12f, -0.008f, 0.30f, 0.68f, Mist),
                new Band(0.76f, 0.14f, 0.012f, 0.24f, 0.62f, Mist),
            }, new Glow[0]),
            "Bg_Tomb" => (new[]
            {
                new Band(0.18f, 0.18f, 0.005f, 0.16f, 0.74f, Smoke, 0.34f),   // 산 위에 감도는 검은 연기 (왼쪽 빈 종이엔 안 번지게)
                new Band(0.60f, 0.12f, -0.009f, 0.26f, 0.70f, Mist),
                new Band(0.78f, 0.14f, 0.011f, 0.22f, 0.66f, Mist),
            }, new[] { new Glow(new Vector2(0.585f, 0.25f), 0.13f, 0.32f, 1.7f, new Color(0.85f, 0.18f, 0.12f)) }),
            "Bg_TombSunset" => (new[]
            {
                new Band(0.56f, 0.10f, 0.008f, 0.28f, 0.68f, WarmMist),
                new Band(0.73f, 0.15f, -0.010f, 0.26f, 0.62f, WarmMist),
            }, new[]
            {
                new Glow(new Vector2(0.74f, 0.44f), 0.11f, 0.45f, 0.5f, new Color(1f, 0.6f, 0.32f)),
                new Glow(new Vector2(0.70f, 0.62f), 0.07f, 0.2f, 0.9f, new Color(1f, 0.55f, 0.3f)),   // 강물에 비친 해
            }),
            _ => (new[] { new Band(0.5f, 0.12f, 0.009f, 0.26f, 0.72f, Mist), new Band(0.74f, 0.14f, -0.008f, 0.22f, 0.66f, Mist) }, new Glow[0]),
        };

        public static void Draw(float w, float h, string name)
        {
            var bg = ItemIcons.TitleArt(name);
            if (bg == null) return;
            EnsureTextures();
            // ScaleAndCrop과 같은 자리: 화면을 꽉 채우게 키우고 가운데 맞춤
            float s = Mathf.Max(w / bg.width, h / bg.height);
            var img = new Rect((w - bg.width * s) * 0.5f, (h - bg.height * s) * 0.5f, bg.width * s, bg.height * s);
            float t = Time.unscaledTime;
            var (bands, glows) = Look(name);
            var old = GUI.color;

            foreach (var g in glows)
            {
                float a = g.alpha * (0.7f + 0.3f * Mathf.Sin(t * g.pulse * Mathf.PI * 2f * 0.25f));
                float r = g.radius * img.height * (1f + 0.06f * Mathf.Sin(t * g.pulse * 0.7f));
                GUI.color = new Color(g.color.r, g.color.g, g.color.b, a);
                GUI.DrawTexture(new Rect(img.x + g.at.x * img.width - r, img.y + g.at.y * img.height - r, r * 2f, r * 2f), _glow);
            }

            foreach (var b in bands)
            {
                float bh = b.height * img.height;
                float y = img.y + b.y * img.height - bh * 0.5f + Mathf.Sin(t * 0.13f + b.y * 9f) * bh * 0.08f;   // 위아래로도 아주 느리게
                float scroll = t * b.speed;
                for (int i = 0; i < Slices; i++)
                {
                    float x0 = i / (float)Slices, x1 = (i + 1) / (float)Slices, xm = (x0 + x1) * 0.5f;
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.fadeIn, b.fadeIn + 0.12f, xm)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.fadeFrom, b.fadeFrom + 0.14f, xm)));
                    if (fade <= 0.001f) continue;
                    GUI.color = new Color(b.color.r, b.color.g, b.color.b, b.alpha * fade);
                    var rect = new Rect(img.x + x0 * img.width, y, (x1 - x0) * img.width + 1f, bh);
                    GUI.DrawTextureWithTexCoords(rect, _mist, new Rect(x0 * Repeat + scroll, 0f, (x1 - x0) * Repeat, 1f));
                }
            }
            GUI.color = old;
        }

        // 안개 무늬: 가로로 끊김 없이 이어지는 펄린 노이즈 구름, 위아래 끝은 옅게. 빛 번짐: 가운데가 밝은 둥근 번짐
        static void EnsureTextures()
        {
            if (_mist == null)
            {
                const int W = 512, H = 128;
                _mist = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = y / (H - 1f);
                    float bump = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 1.6f);
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W;
                        float n = Tile(u, v, 6f, 0f) * 0.6f + Tile(u, v, 14f, 37f) * 0.3f + Tile(u, v, 30f, 71f) * 0.1f;
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.72f, n)) * bump;
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                _mist.SetPixels32(px);
                _mist.Apply();
            }
            if (_glow == null)
            {
                const int S = 128;
                _glow = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float d = new Vector2(x - S * 0.5f + 0.5f, y - S * 0.5f + 0.5f).magnitude / (S * 0.5f);
                        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                _glow.SetPixels32(px);
                _glow.Apply();
            }
        }

        // 가로로 이어지는 노이즈 (u = 0과 1이 같아요): N(u)와 한 바퀴 앞 N(u - 1)을 u만큼 섞어요
        static float Tile(float u, float v, float scale, float seed)
        {
            float N(float x) => Mathf.PerlinNoise(x * scale + seed + 500f, v * scale * 0.35f + seed);
            return Mathf.Lerp(N(u), N(u - 1f), u);
        }
    }
}
