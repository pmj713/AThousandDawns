using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Fx
{
    // 수묵 화면 전환 (아트 목록 1. 연출). 화면을 덮은 뒤 midpoint를 부르고(씬을 다시 불러도 됨), 새 화면을 드러내요.
    // 씬을 넘어 살아 있는 오버레이라 SceneManager.LoadScene을 midpoint에 넣으면 돼요.
    //   Regression: 사망 → 내단의 붉은 기운이 번쩍 → 먹이 화면을 덮음 → (씬 로드) → 먹이 거꾸로 모이며 되감기듯 걷히고 색이 돌아와요.
    //               firstDawn이면 덮인 사이에 "그날 아침으로 돌아왔다" → 一朝千回가 먹 번지듯 떠올라요 (Enter·Esc로 넘김).
    //               두 번째부터는 1초 남짓으로 짧게.
    //   Depart:     마을 출발. 족자가 내려와 덮고, 죽림 그림이 번져 나온 뒤 (씬 로드) 족자가 말려 올라가며 죽림이 드러나요.
    //   Floor:      층 사이 먹 번짐 0.8초. origin(화면 픽셀, 위가 0)을 주면 그 자리에서 번져요 (예: 고른 갈림길).
    // 사용: InkTransition.Play(InkTransition.Kind.Regression, () => SceneManager.LoadScene(...), firstDawn: 처음이면 true);
    public sealed class InkTransition : MonoBehaviour
    {
        public enum Kind { Regression, Depart, Floor }

        static InkTransition s_instance;
        public static bool IsPlaying => s_instance != null && s_instance._playing;

        // 그림 (Art/UI/Fx/Resources/FxArt, Art/UI/Title/Resources/TitleArt). 없으면 단색으로 대신해요
        Texture2D _blot, _rod, _bamboo, _title, _glow;

        bool _playing;
        Kind _kind;
        Vector2? _origin;
        float _ink;          // 먹 덮임 0~1
        float _inkSpin;      // 먹 회전 (도)
        float _red;          // 붉은 기운 0~1
        float _flash;        // 색이 돌아오는 밝은 번쩍임 0~1
        float _scroll;       // 족자가 덮은 정도 0~1 (위에서 아래로)
        float _scrollArt;    // 족자 위 죽림 그림 0~1
        float _paper;        // 첫 회귀: 먹 위로 번져 나온 한지 0~1
        float _line, _titleAlpha, _titleBleed; // 첫 회귀 글·제목

        static readonly Color Paper = new Color(0.937f, 0.902f, 0.824f);
        static readonly Color Ink = new Color(0.06f, 0.055f, 0.05f);
        static readonly Color InnerRed = new Color(0.78f, 0.12f, 0.08f);

        public static void Play(Kind kind, Action midpoint, bool firstDawn = false, Vector2? origin = null)
        {
            if (s_instance == null)
            {
                var go = new GameObject("Ink Transition");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<InkTransition>();
            }
            if (s_instance._playing) { midpoint?.Invoke(); return; } // 겹치면 바로 실행
            s_instance.StartCoroutine(s_instance.Run(kind, midpoint, firstDawn, origin));
        }

        void Awake()
        {
            _blot = Resources.Load<Texture2D>("FxArt/InkBlot");
            _rod = Resources.Load<Texture2D>("FxArt/ScrollRod");
            _bamboo = Resources.Load<Texture2D>("TitleArt/Bg_Bamboo");
            _title = Resources.Load<Texture2D>("TitleArt/Title");
            _glow = MakeGlow();
        }

        IEnumerator Run(Kind kind, Action midpoint, bool firstDawn, Vector2? origin)
        {
            _playing = true;
            SetPlayerInput(false);
            _kind = kind;
            _origin = origin;
            int scene = s_loads;

            switch (kind)
            {
                case Kind.Regression:
                {
                    float k = firstDawn ? 1f : 0.6f; // 두 번째부터는 짧게
                    yield return Tween(0.25f * k, t => _red = Mathf.Sin(t * Mathf.PI * 0.5f));
                    yield return Tween(0.5f * k, t => { _ink = EaseIn(t); _inkSpin = t * 40f; _red = 1f - t; });
                    midpoint?.Invoke();
                    yield return WaitForLoad(scene);
                    if (firstDawn)
                    {
                        yield return Tween(0.5f, t => _paper = t, true); // 먹 위로 한지가 번져 나와요
                        yield return Tween(0.5f, t => _line = t, true);
                        yield return Hold(0.8f);
                        yield return Tween(1f, t => { _titleAlpha = t; _titleBleed = 1f - t; }, true);
                        yield return Hold(0.9f);
                        yield return Tween(0.4f, t => { _line = 1f - t; _titleAlpha = 1f - t; }, true);
                        yield return Tween(0.35f, t => _paper = 1f - t, true); // 다시 먹으로 덮였다가 아래에서 되감겨요
                        _line = _titleAlpha = _paper = 0f;
                    }
                    // 먹이 거꾸로 감기며 가운데로 모이고, 색이 한꺼번에 돌아와요
                    yield return Tween(0.8f * k, t => { _ink = 1f - EaseOut(t); _inkSpin = 40f - t * 220f; _flash = Mathf.Sin(t * Mathf.PI) * 0.45f; });
                    break;
                }
                case Kind.Depart:
                    yield return Tween(0.6f, t => _scroll = EaseInOut(t));
                    yield return Tween(0.35f, t => _scrollArt = t);
                    midpoint?.Invoke();
                    yield return WaitForLoad(scene);
                    yield return Hold(0.25f);
                    yield return Tween(0.7f, t => _scroll = 1f - EaseInOut(t));
                    _scrollArt = 0f;
                    break;
                default: // Floor
                    yield return Tween(0.35f, t => { _ink = EaseIn(t); _inkSpin = t * 30f; });
                    midpoint?.Invoke();
                    yield return Hold(0.05f);
                    yield return Tween(0.45f, t => { _ink = 1f - EaseOut(t); _inkSpin = 30f + t * 30f; });
                    break;
            }
            _ink = _red = _flash = _scroll = _scrollArt = _paper = 0f;
            _playing = false;
            SetPlayerInput(true);
        }

        // 연출 중에는 조작을 막아요: 클릭·키를 누르고 있어도 평타·대시가 나가지 않게 (플레이어 입력 읽기를 끔).
        // 씬을 다시 부르면 새 플레이어가 생기니 매 프레임 확인해요
        void LateUpdate()
        {
            if (_playing) SetPlayerInput(false);
        }

        static void SetPlayerInput(bool on)
        {
            foreach (var reader in FindObjectsByType<AThousandDawns.Player.PlayerInputReader>(FindObjectsSortMode.None))
                if (reader.enabled != on) reader.enabled = on;
        }

        // midpoint가 씬을 다시 불렀으면 새 씬이 다 뜰 때까지 덮은 채로 기다려요 (부르지 않았으면 바로 지나감)
        static IEnumerator WaitForLoad(int loadsBefore)
        {
            yield return null;
            yield return null; // LoadScene은 다음 프레임에 바뀌어요
            float limit = Time.unscaledTime + 10f;
            while (Time.unscaledTime < limit && s_loads > loadsBefore && !SceneManager.GetActiveScene().isLoaded)
                yield return null;
            yield return null; // 새 씬 첫 프레임이 그려진 뒤
        }

        static int s_loads;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CountLoads()
        {
            s_loads = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => s_loads++;

        static IEnumerator Tween(float duration, Action<float> set, bool skippable = false)
        {
            float start = Time.unscaledTime;
            while (true)
            {
                float t = duration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - start) / duration);
                if (skippable && SkipPressed()) t = 1f;
                set(t);
                if (t >= 1f) yield break;
                yield return null;
            }
        }

        static IEnumerator Hold(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !SkipPressed()) yield return null;
        }

        // 넘기기는 Enter·Esc만: 클릭·스페이스는 같은 프레임에 게임에도 들어가 평타·대시가 나가요
        static bool SkipPressed()
        {
            var k = Keyboard.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) || AThousandDawns.Core.Pad.Confirm; // 패드 A로도 넘겨요
        }

        static float EaseIn(float t) => t * t;
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        static float EaseInOut(float t) => t * t * (3f - 2f * t);

        void OnGUI()
        {
            if (!_playing) return;
            GUI.depth = -1000;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.identity; // 화면 픽셀 그대로 (레터박스까지 덮어요)
            float w = Screen.width, h = Screen.height;
            var center = _origin ?? new Vector2(w * 0.5f, h * 0.5f);

            if (_red > 0f) // 내단의 붉은 기운
            {
                float size = Mathf.Min(w, h) * (0.5f + 0.9f * _red);
                GUI.color = new Color(InnerRed.r, InnerRed.g, InnerRed.b, _red * 0.9f);
                GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _glow);
            }
            if (_ink > 0f) DrawInk(center, w, h);
            if (_paper > 0f)
            {
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, _paper);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
            if (_flash > 0f)
            {
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, _flash);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
            if (_scroll > 0f) DrawScroll(w, h);
            if (_line > 0f || _titleAlpha > 0f) DrawDawn(w, h);

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // 먹 얼룩: 가장자리가 번진 그림을 크게 키워 덮어요. 그림이 없으면 단색
        void DrawInk(Vector2 center, float w, float h)
        {
            float far = 0f;
            foreach (var corner in new[] { Vector2.zero, new Vector2(w, 0f), new Vector2(0f, h), new Vector2(w, h) })
                far = Mathf.Max(far, Vector2.Distance(center, corner));
            if (_blot == null)
            {
                GUI.color = new Color(Ink.r, Ink.g, Ink.b, _ink);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                return;
            }
            float size = far * 3.2f * _ink; // 얼룩의 짙은 가운데가 화면 귀퉁이까지 덮도록
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(_inkSpin, center);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _blot);
            GUI.matrix = matrix;
            if (_ink >= 0.999f) // 다 덮였으면 틈 없이
            {
                GUI.color = Ink;
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
        }

        // 족자: 위에서 한지가 내려와 덮고, 아래 끝에 족자 축. 덮인 동안 죽림 그림이 번져 나와요
        void DrawScroll(float w, float h)
        {
            float sheet = h * _scroll;
            GUI.color = Paper;
            GUI.DrawTexture(new Rect(0, 0, w, sheet), Texture2D.whiteTexture);
            if (_scrollArt > 0f && _bamboo != null)
            {
                GUI.color = new Color(1f, 1f, 1f, _scrollArt);
                GUI.BeginGroup(new Rect(0, 0, w, sheet));
                GUI.DrawTexture(new Rect(0, 0, w, h), _bamboo, ScaleMode.ScaleAndCrop);
                GUI.EndGroup();
            }
            float rodH = Mathf.Max(18f, h * 0.05f);
            var rod = new Rect(-w * 0.02f, sheet - rodH * 0.5f, w * 1.04f, rodH);
            if (_rod != null) { GUI.color = Color.white; GUI.DrawTexture(rod, _rod, ScaleMode.StretchToFill); }
            else { GUI.color = new Color(0.22f, 0.14f, 0.09f); GUI.DrawTexture(rod, Texture2D.whiteTexture); }
        }

        // 첫 회귀: 먹 위로 번진 한지에 글 한 줄, 그 아래 一朝千回가 먹 번지듯 떠올라요
        void DrawDawn(float w, float h)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(h * 0.04f),
                normal = { textColor = Ink },
            };
            GUI.color = new Color(1f, 1f, 1f, _line);
            GUI.Label(new Rect(0, h * 0.12f, w, h * 0.1f), "그날 아침으로 돌아왔다", style);
            if (_title != null && _titleAlpha > 0f)
            {
                float th = h * 0.6f * (1f + 0.08f * _titleBleed); // 번지듯 살짝 커졌다가 자리 잡아요
                float tw = th * _title.width / _title.height;
                GUI.color = new Color(1f, 1f, 1f, _titleAlpha);
                GUI.DrawTexture(new Rect((w - tw) * 0.5f, h * 0.27f - (th - h * 0.6f) * 0.5f, tw, th), _title);
            }
        }

        static Texture2D MakeGlow()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
