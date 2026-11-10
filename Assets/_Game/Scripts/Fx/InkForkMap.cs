using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 갈림길 지도 (아트 목록 1. 연출): 옅은 수묵 지도 위에 아래 가운데(지금 자리)에서 갈래마다 먹선 길이 그려져요.
    // 짙은 길은 굵은 먹선에 검은 안개가 감돌아요. 갈림길 화면이 뜨면 길이 붓으로 긋듯 0.7초에 걸쳐 그려지고,
    // 마우스를 올린 길은 조금 더 짙어져요. IMGUI에서 지금 GUI.matrix(UiScale) 그대로 그려요.
    // 사용 (PrototypeHud.DrawFork 안, 카드보다 먼저):
    //   var ends = InkForkMap.Draw(mapRect, denseFlags, hoverIndex);  // 길 끝 자리(카드 놓을 곳)를 돌려줘요
    //   고르면 InkTransition.Play(Kind.Floor, ..., origin: 고른 길 끝의 화면 픽셀 위치)로 그 자리에서 먹이 번져요.
    public static class InkForkMap
    {
        static Texture2D s_map, s_mist, s_dot;
        static float s_lastDraw = -10f, s_shownAt;
        static readonly List<Vector2> s_ends = new List<Vector2>();

        public static Vector2[] Draw(Rect area, IList<bool> dense, int hover = -1)
        {
            Load();
            float now = Time.unscaledTime;
            if (now - s_lastDraw > 0.3f) s_shownAt = now; // 갈림길 화면이 새로 떴으면 그리기 애니메이션 처음부터
            s_lastDraw = now;
            float progress = Mathf.Clamp01((now - s_shownAt) / 0.7f);

            var old = GUI.color;
            if (s_map != null) { GUI.color = Color.white; GUI.DrawTexture(area, s_map, ScaleMode.ScaleAndCrop); }

            int n = dense.Count;
            var start = new Vector2(area.center.x, area.yMax - area.height * 0.08f);
            s_ends.Clear();
            for (int i = 0; i < n; i++)
            {
                var end = new Vector2(area.x + area.width * (i + 1f) / (n + 1f), area.y + area.height * 0.2f);
                s_ends.Add(end);
                DrawPath(start, end, i, dense[i], i == hover, progress, area.height);
            }
            // 지금 자리: 작은 먹 점 둘
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            float r = area.height * 0.03f;
            GUI.DrawTexture(new Rect(start.x - r, start.y - r, r * 2f, r * 2f), s_dot);
            GUI.color = old;
            return s_ends.ToArray();
        }

        // 휘어진 길 하나: 붓 점을 촘촘히 찍어 먹선을 만들어요. progress만큼만 (붓이 긋는 중)
        static void DrawPath(Vector2 a, Vector2 b, int index, bool dense, bool hover, float progress, float scale)
        {
            float side = (index % 2 == 0 ? 1f : -1f) * scale * 0.12f;
            var c1 = Vector2.Lerp(a, b, 0.33f) + new Vector2(side, 0f);
            var c2 = Vector2.Lerp(a, b, 0.66f) - new Vector2(side * 0.8f, 0f);
            float width = scale * (dense ? 0.022f : 0.011f) * (hover ? 1.35f : 1f);
            float ink = dense ? 0.95f : (hover ? 0.85f : 0.65f);
            const int steps = 90;
            int last = Mathf.RoundToInt(steps * progress);
            float time = Time.unscaledTime;

            if (dense && s_mist != null) // 짙은 길: 검은 안개가 천천히 감돌아요
                for (int i = 0; i <= last; i += 10)
                {
                    var p = Bezier(a, c1, c2, b, i / (float)steps);
                    float s = scale * (0.16f + 0.05f * Mathf.Sin(time * 0.8f + i));
                    p += new Vector2(Mathf.Sin(time * 0.5f + i * 0.7f), Mathf.Cos(time * 0.4f + i)) * scale * 0.015f;
                    GUI.color = new Color(1f, 1f, 1f, 0.28f);
                    GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), s_mist);
                }
            for (int i = 0; i <= last; i++)
            {
                float t = i / (float)steps;
                var p = Bezier(a, c1, c2, b, t);
                // 붓이 눌렸다 떼어지듯: 처음엔 굵고 끝으로 갈수록 가늘게, 군데군데 마른 붓 자국
                float press = Mathf.Lerp(1.15f, 0.55f, t) * (0.85f + 0.3f * Mathf.PerlinNoise(index * 3.1f, t * 9f));
                float dot = width * press;
                GUI.color = new Color(0.06f, 0.055f, 0.05f, ink * (0.75f + 0.25f * Mathf.PerlinNoise(t * 20f, index)));
                GUI.DrawTexture(new Rect(p.x - dot, p.y - dot, dot * 2f, dot * 2f), s_dot);
            }
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static void Load()
        {
            if (s_dot != null) return;
            s_map = Resources.Load<Texture2D>("FxArt/ForkMap");
            s_mist = Resources.Load<Texture2D>("FxArt/InkMist");
            // 붓 점: 가장자리가 살짝 번진 둥근 먹 점
            const int size = 32;
            s_dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    float a = Mathf.Clamp01((1f - d) * 3f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            s_dot.SetPixels32(px);
            s_dot.Apply();
        }
    }
}
