using UnityEngine;

namespace AThousandDawns.Fx
{
    // 경맥도 화면 수묵 그림 (아트 목록 UI). MeridianScreen이 단색 사각형 대신 부르는 자리만 맡아요 — 글자·배치·입력은 그쪽 그대로.
    //   Backdrop: 화면 전체 — 먹 번짐 위로 경혈도(사람 몸 경락선)가 옅게 비치는 어두운 바탕.
    //   Cell:     칸 — 기 색으로 물든 먹 번짐 + 옅은 한지색 붓 네모 테두리. 단전 칸은 아래에 붉은 먹이 번져요.
    //   Panel:    주머니·설명 칸 — 짙은 먹 한 획 바탕.
    //   Restyle:  버튼·주머니 줄 — 먹 붓 획 바탕 (마우스를 올리면 붉은 먹).
    // 그림: Art/UI/Hud/Resources/HudArt (MeridianBg, InkSwatch, CellFrame, Blot). 없으면 예전처럼 단색.
    public static class InkMeridianArt
    {
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);
        static readonly Color Plum = new Color(0.92f, 0.2f, 0.18f);
        static readonly Color PanelInk = new Color(0.08f, 0.1f, 0.24f); // 채색: 짙은 남빛 (먹 대신)

        static Texture2D _bg, _swatch, _frame, _blot, _button, _buttonHover;
        static bool _loaded;

        public static void Backdrop(float width, float height)
        {
            Load();
            var old = GUI.color;
            var screen = new Rect(0f, 0f, width, height);
            if (_bg != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(screen, _bg, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0f, 0f, 0f, 0.15f); // 글자가 읽히게 한 겹 더 어둡게
            }
            else GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Cell(Rect rect, Color qiColor, bool danjeon)
        {
            Load();
            if (_swatch == null || _frame == null) { Fill(rect, qiColor); return; }
            var old = GUI.color;
            if (danjeon && _blot != null) // 단전: 붉은 먹이 칸 밖으로 살짝 번져요
            {
                float pulse = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
                GUI.color = new Color(Plum.r, Plum.g, Plum.b, pulse);
                GUI.DrawTexture(Grow(rect, 0.18f), _blot, ScaleMode.StretchToFill);
            }
            GUI.color = new Color(qiColor.r, qiColor.g, qiColor.b, Mathf.Max(0.75f, qiColor.a));
            GUI.DrawTexture(Grow(rect, -0.04f), _swatch, ScaleMode.StretchToFill);
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, danjeon ? 0.75f : 0.32f);
            GUI.DrawTexture(Grow(rect, 0.04f), _frame, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        public static void Panel(Rect rect)
        {
            Load();
            if (_swatch == null) { GUI.Box(rect, GUIContent.none); return; }
            var old = GUI.color;
            GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.92f);
            GUI.DrawTexture(Grow(rect, 0.03f), _swatch, ScaleMode.StretchToFill);
            if (_frame != null) // 옅은 한지색 붓 테두리 (밋밋한 검은 상자로 보이지 않게)
            {
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, 0.18f);
                GUI.DrawTexture(Grow(rect, 0.02f), _frame, ScaleMode.StretchToFill);
            }
            GUI.color = old;
        }

        // 버튼·주머니 줄 스타일을 먹 붓 획 바탕으로 (보통 = 짙은 먹, 마우스·누름 = 붉은 먹). 글자색은 한지색.
        // 고해상도용 scaledBackgrounds도 비워야 Unity 기본 회색 버튼이 다시 나오지 않아요.
        public static void Restyle(GUIStyle style)
        {
            Load();
            if (_button == null) return;
            var none = new Texture2D[0];
            foreach (var (state, tex) in new[] { (style.normal, _button), (style.focused, _button), (style.hover, _buttonHover), (style.active, _buttonHover),
                                                  (style.onNormal, _buttonHover), (style.onHover, _buttonHover), (style.onActive, _buttonHover), (style.onFocused, _buttonHover) })
            {
                state.background = tex;
#if UNITY_EDITOR
                state.scaledBackgrounds = none; // 에디터 전용 (빌드에는 고해상도 바탕이 없어요)
#endif
                state.textColor = Paper;
            }
            style.border = new RectOffset(28, 28, 12, 12); // 번진 가장자리는 늘리지 않게
        }

        // 마우스를 올리거나 누르거나 포커스가 가도 글자색이 바뀌지 않게 (플레이테스트: Unity 기본 hover 글자색이 흰색이라 글자가 하얗게 변해요).
        // normal.textColor를 hover · active · focused · on* 상태에 그대로 복사해요. 스타일을 만든 뒤 한 번 부르면 돼요.
        public static GUIStyle LockText(GUIStyle style)
        {
            if (style == null) return null;
            var c = style.normal.textColor;
            style.hover.textColor = style.active.textColor = style.focused.textColor = c;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = c;
            return style;
        }

        static Rect Grow(Rect r, float ratio)
        {
            float dx = r.width * ratio * 0.5f, dy = r.height * ratio * 0.5f;
            return new Rect(r.x - dx, r.y - dy, r.width + dx * 2f, r.height + dy * 2f);
        }

        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // 한지 화면 (영혼수련 등 밝은 화면용): 한지 결 그림을 깔고 가장자리에 옅은 먹 번짐
        public static void PaperBackdrop(float width, float height)
        {
            Load();
            var old = GUI.color;
            var screen = new Rect(0f, 0f, width, height);
            GUI.color = Paper;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            if (_paper != null) { GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.DrawTexture(screen, _paper, ScaleMode.ScaleAndCrop); } // 결은 옅게 (글자가 묻히지 않게)
            if (_blot != null) // 귀퉁이 먹 번짐
            {
                GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.08f);
                float s = height * 0.9f;
                GUI.DrawTexture(new Rect(-s * 0.45f, -s * 0.4f, s, s), _blot, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(width - s * 0.55f, height - s * 0.55f, s, s), _blot, ScaleMode.StretchToFill);
            }
            GUI.color = old;
        }

        // 한지 판 + 먹 붓 테두리 (밝은 화면의 줄·칸). fill로 판 색을 물들여요 (기본 옅은 한지)
        public static void PaperPanel(Rect rect, Color fill)
        {
            Load();
            var old = GUI.color;
            GUI.color = fill;
            if (_swatch != null) GUI.DrawTexture(Grow(rect, 0.02f), _swatch, ScaleMode.StretchToFill);
            else GUI.DrawTexture(rect, Texture2D.whiteTexture);
            if (_frame != null)
            {
                GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.75f);
                GUI.DrawTexture(Grow(rect, 0.02f), _frame, ScaleMode.StretchToFill);
            }
            GUI.color = old;
        }

        public static void PaperPanel(Rect rect) => PaperPanel(rect, new Color(1f, 0.99f, 0.96f, 0.97f)); // 밝고 거의 불투명하게 (짙은 먹 글자와 대비)

        // ── 장비 화면 (차원 겹침): 인체 경혈도 위 장비 칸 10개 (MeridianScreen.DrawBody가 불러요)

        // 바탕: area 전체에 먹 패널, 가운데 정사각에 한지 인체 경혈도 (HudArt/BodyChart) + 먹 붓 테두리. 그린 그림 칸(정사각)을 돌려줘요.
        public static Rect BodyChart(Rect area)
        {
            Load();
            Panel(area);
            float s = Mathf.Min(area.width, area.height);
            var chart = new Rect(area.center.x - s * 0.5f, area.center.y - s * 0.5f, s, s);
            var old = GUI.color;
            if (_body != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(chart, _body, ScaleMode.ScaleToFit);
            }
            else Fill(chart, Paper);
            if (_frame != null)
            {
                GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.85f);
                GUI.DrawTexture(Grow(chart, 0.03f), _frame, ScaleMode.StretchToFill);
            }
            GUI.color = old;
            return chart;
        }

        // 열린 칸 테두리: Cell(…) 위에 짙은 먹 붓 네모 (한지 위에서도 칸이 또렷하게). 끼운 칸은 붉은 먹이 한 겹 더.
        public static void EquipFrame(Rect rect, bool worn)
        {
            Load();
            if (_frame == null) return;
            var old = GUI.color;
            GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.9f);
            GUI.DrawTexture(Grow(rect, 0.1f), _frame, ScaleMode.StretchToFill);
            if (worn)
            {
                GUI.color = new Color(Plum.r, Plum.g, Plum.b, 0.55f);
                GUI.DrawTexture(Grow(rect, 0.16f), _frame, ScaleMode.StretchToFill);
            }
            GUI.color = old;
        }

        // 잠긴 칸: 옅은 잿빛 먹 번짐 + 흐린 붓 테두리 + 가운데 빈 원상 (글자 "잠김"은 그 위에)
        public static void LockedSlot(Rect rect)
        {
            Load();
            var old = GUI.color;
            GUI.color = new Color(0.32f, 0.3f, 0.28f, 0.75f);
            if (_swatch != null) GUI.DrawTexture(Grow(rect, -0.04f), _swatch, ScaleMode.StretchToFill);
            else GUI.DrawTexture(rect, Texture2D.whiteTexture);
            if (_enso != null)
            {
                GUI.color = new Color(Paper.r, Paper.g, Paper.b, 0.22f);
                GUI.DrawTexture(Grow(rect, -0.3f), _enso, ScaleMode.ScaleToFit);
            }
            if (_frame != null)
            {
                GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.45f);
                GUI.DrawTexture(Grow(rect, 0.1f), _frame, ScaleMode.StretchToFill);
            }
            GUI.color = old;
        }

        // 칸 이름 밑판: 한지 위에 놓인 밝은 글자가 읽히게 짙은 먹 띠 (글자를 그리기 전에)
        public static void LabelPlate(Rect rect)
        {
            Load();
            var old = GUI.color;
            GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.8f);
            if (_swatch != null) GUI.DrawTexture(Grow(rect, 0.06f), _swatch, ScaleMode.StretchToFill);
            else GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // 칸에서 혈자리까지 가는 먹 붓선. GUI.matrix 배율(UiScale)에서도 어긋나지 않게 행렬을 직접 곱해요 (RotateAroundPivot은 배율이 있으면 축이 어긋나요).
        public static void InkLine(Vector2 a, Vector2 b, float alpha, float width = 3f)
        {
            Load();
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            var m = GUI.matrix;
            var old = GUI.color;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, alpha);
            var r = new Rect(0f, -width * 0.5f, len, width);
            if (_stroke != null) GUI.DrawTexture(r, _stroke, ScaleMode.StretchToFill);
            else GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.matrix = m;
            GUI.color = old;
        }

        // 혈자리 점: 먹 점 (끼운 칸이면 붉은 먹 점 둘레로 붉은 먹이 은은히 번져요)
        public static void Acupoint(Vector2 at, float size, bool worn, bool open)
        {
            Load();
            var old = GUI.color;
            if (worn && _blot != null)
            {
                float pulse = 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
                GUI.color = new Color(Plum.r, Plum.g, Plum.b, pulse);
                float g = size * 2.6f;
                GUI.DrawTexture(new Rect(at.x - g * 0.5f, at.y - g * 0.5f, g, g), _blot, ScaleMode.StretchToFill);
            }
            GUI.color = worn ? new Color(0.75f, 0.15f, 0.1f) : new Color(PanelInk.r, PanelInk.g, PanelInk.b, open ? 0.85f : 0.3f);
            var dot = new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size);
            if (_blot != null) GUI.DrawTexture(dot, _blot, ScaleMode.StretchToFill);
            else GUI.DrawTexture(dot, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ── 혼종 세트 문장 (이계 세트 효과, 장비 화면 키워드 목록 아래 "혼종 세트" 줄)
        // 문장: Resources/HybridIcons/{id}. 켜진 세트는 또렷하게, 꺼진 세트는 옅은 잿빛.
        // 꺼져 있다가 켜지는 순간 (화면이 열려 있는 동안): 보랏빛 균열 테두리가 크게 번쩍 조여들고 보랏빛 번짐이 터져요.
        //   신호 없이 이 함수가 id마다 지난 상태를 기억해서 알아차려요 (화면을 처음 열 때 이미 켜져 있던 세트는 번쩍이지 않아요).
        public static void HybridCrest(Rect rect, string id, bool active)
        {
            Load();
            if (string.IsNullOrEmpty(id)) return;
            if (!_hybridTex.TryGetValue(id, out var tex)) _hybridTex[id] = tex = Resources.Load<Texture2D>("HybridIcons/" + id);
            float now = Time.unscaledTime;
            if (!_hybridState.TryGetValue(id, out var state) || now - state.seen > 0.5f) state = (active, -10f, now); // 화면을 새로 열었어요: 번쩍이지 않아요
            else if (active && !state.on) state.since = now; // 방금 켜졌어요
            state.on = active;
            state.seen = now;
            _hybridState[id] = state;
            var old = GUI.color;
            if (tex != null)
            {
                GUI.color = active ? Color.white : new Color(0.62f, 0.6f, 0.6f, 0.45f);
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            }
            float age = now - state.since;
            if (active && age < 0.8f)
            {
                float k = age / 0.8f;
                if (_blot != null)
                {
                    GUI.color = new Color(0.62f, 0.3f, 1f, 0.6f * (1f - k));
                    float g = Mathf.Lerp(1.2f, 2.4f, Mathf.Sqrt(k));
                    GUI.DrawTexture(new Rect(rect.center.x - rect.width * g * 0.5f, rect.center.y - rect.height * g * 0.5f, rect.width * g, rect.height * g), _blot, ScaleMode.StretchToFill);
                }
                if (_rift != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, 1f - k * k);
                    float s = Mathf.Lerp(1.9f, 1.15f, 1f - (1f - k) * (1f - k));
                    GUI.DrawTexture(new Rect(rect.center.x - rect.width * s * 0.5f, rect.center.y - rect.height * s * 0.5f, rect.width * s, rect.height * s), _rift, ScaleMode.StretchToFill);
                }
            }
            GUI.color = old;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> _hybridTex = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static readonly System.Collections.Generic.Dictionary<string, (bool on, float since, float seen)> _hybridState = new System.Collections.Generic.Dictionary<string, (bool on, float since, float seen)>();

        static Texture2D _paper, _body, _enso, _stroke, _rift;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _paper = Resources.Load<Texture2D>("FxArt/ScrollPaper");
            _body = Resources.Load<Texture2D>("HudArt/BodyChart");
            _enso = Resources.Load<Texture2D>("HudArt/Enso");
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
            _rift = Resources.Load<Texture2D>("HudArt/RiftFrame");
            _bg = Resources.Load<Texture2D>("HudArt/MeridianBg");
            _swatch = Resources.Load<Texture2D>("HudArt/InkSwatch");
            _frame = Resources.Load<Texture2D>("HudArt/CellFrame");
            _blot = Resources.Load<Texture2D>("HudArt/Blot");
            _button = Resources.Load<Texture2D>("HudArt/InkButton");
            _buttonHover = Resources.Load<Texture2D>("HudArt/InkButtonHover");
        }
    }
}
