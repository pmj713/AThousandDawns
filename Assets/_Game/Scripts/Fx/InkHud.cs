using AThousandDawns.Core;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 수묵 HUD (아트 목록 UI): 왼쪽 위 한 덩어리 (높이 Height). 시험판 글자 목록은 그 아래로.
    //   초식: 원상(먹 동그라미) 안에 초식 그림. 쿨타임 동안 먹물이 덮였다가 위에서부터 걷혀요. 남은 초 · 키캡.
    //         비도 0개면 그림이 회색(회수 불가), 금창약을 마시는 동안은 쿨타임 표시를 숨기고 머리 위 원형 게이지를 띄워요 (기획 시트).
    //   체력: 붓 획 하나 — 먹색 바탕 획 위에 매화 홍 획이 줄어들고, 깎인 자리는 한지색으로 잠깐 남았다 따라 줄어요. 보호막은 위에 흰 획.
    //   대시: 붓 ">" 하나가 한 번 — 최대 충전만큼 늘어요(>>>). 찬 것은 짙게, 채워지는 것은 왼쪽부터 먹이 차올라요.
    //   금창약: 호리병 — 남은 만큼 짙게, 마시는 중엔 아래 붉은 줄이 차요.
    //   기세: 체력 아래 가는 쪽빛 획, 차면 금빛 + 절초 이름 · 키캡. 키워드: HUD 아래 줄 (아이콘 · 개수 · 2·4·6단계 붓점).
    // 그림: Art/UI/Hud/Resources/HudArt (없으면 단색으로 대신해요). 기준 1920×1080 (UiScale) 위에서 그려요.
    // 사용: OnGUI에서 InkHud.Draw(player);
    public static class InkHud
    {
        static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);
        static readonly Color Plum = new Color(0.78f, 0.27f, 0.24f); // 매화 홍 #C8463C (HUD 체력과 같은 색)
        static readonly Color Indigo = new Color(0.22f, 0.32f, 0.52f); // 쪽빛 (기세 차는 중)
        static readonly Color Gold = new Color(0.93f, 0.74f, 0.3f);    // 금빛 (절초 준비)

        const float Margin = 30f;
        public const float Height = Margin + 112f + 14f + 38f + 18f; // HUD(+키워드 줄)가 차지하는 위쪽 높이 (아래에 다른 것을 놓을 때)
        const float SkillSize = 112f;
        const float BarW = 400f, BarH = 40f, GaugeW = 330f, GaugeH = 20f, KeywordSize = 38f;
        const float PipSize = 30f, GourdSize = 48f, ChevronW = 26f;

        static Texture2D _stroke, _enso, _gourd, _blot, _chevron, _spin, _bamboo, _quake, _recall, _recallGray, _palm, _takrang, _takrangVortex, _daggerFlash, _daggerBurst, _daggerHalo;
        static bool _loaded;
        static GUIStyle _number, _small, _badge, _gaugeLabel, _key;
        static float _lagHp = -1f;   // 깎인 자리 (따라 줄어드는 체력)
        static float _lagHold;       // 깎인 뒤 잠깐 멈춤
        static float _lastHp = 1f;

        public static void Draw(PlayerController player)
        {
            if (player == null || Event.current.type != EventType.Repaint) return;
            Load();
            Styles();
            var old = GUI.color;
            var skillRect = new Rect(Margin, Margin, SkillSize, SkillSize); // 왼쪽 위 (사용자 지정)
            DrawSkill(player, skillRect);
            float x = skillRect.xMax + 14f;
            var barRect = new Rect(x, skillRect.y, BarW, BarH);
            DrawHealth(player, barRect);
            DrawGauge(player, new Rect(x + 4f, barRect.yMax + 2f, GaugeW, GaugeH));
            float row = barRect.yMax + GaugeH + 10f;
            float after = DrawDash(player, new Vector2(x + 6f, row));
            DrawPotions(player, new Vector2(Mathf.Max(after + 26f, x + 170f), row - 8f));
            DrawKeywords(player, new Vector2(Margin, skillRect.yMax + 14f));
            DrawDrinkGauge(player);
            GUI.color = old;
        }

        // ── 기세 ── 체력 획 아래 가는 쪽빛 획. 차면 금빛으로 일렁이고 끝에 절초 이름 · 키캡.
        //   기세 200%(면벽수련 기 20)면 가운데 먹 눈금으로 두 칸을 나눠요.
        static void DrawGauge(PlayerController p, Rect r)
        {
            float cap = Mathf.Max(1f, p.GaugeCap);
            float ratio = Mathf.Clamp01(p.Gauge / cap);
            bool ready = p.UltimateReady || p.IsCastingUltimate;
            StrokePart(r, 1f, Ink, 0.7f);
            float shimmer = ready ? 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            StrokePart(Shrink(r, 0f, 0.18f), ratio, ready ? Gold : Indigo, shimmer);
            if (p.DoubleGauge)
            {
                Tint(Ink, 0.9f);
                GUI.DrawTexture(new Rect(r.x + r.width * 0.5f - 1.5f, r.y + 2f, 3f, r.height - 4f), Texture2D.whiteTexture);
            }
            string name = p.UsesDagger ? "만천화우" : "만검귀종";
            Tint(Paper, 1f);
            if (ready)
            {
                Shadowed(new Rect(r.xMax + 8f, r.y - 4f, 120f, r.height + 8f), p.IsCastingUltimate ? $"{name}!" : name, _gaugeLabel, TextAnchor.MiddleLeft);
                if (!p.IsCastingUltimate)
                {
                    GUI.color = Color.white;
                    KeyHint.Draw(new Rect(r.xMax + 8f + _gaugeLabel.CalcSize(new GUIContent(name)).x + 6f, r.y - 3f, 40f, r.height + 6f), KeyHint.Key(Bind.Ultimate), _key, 12);
                }
            }
            else Shadowed(new Rect(r.x + 14f, r.y - 3f, r.width, r.height + 6f), $"기세 {p.Gauge:0}", _gaugeLabel, TextAnchor.MiddleLeft);
        }

        // ── 키워드 ── HUD 아래 한 줄. 개수가 있는 키워드만: 아이콘 + 개수, 아래 붓점 셋이 2·4·6단계 (4단계 = 절초 각인)
        static void DrawKeywords(PlayerController p, Vector2 at)
        {
            var keywords = p.Keywords;
            if (keywords == null) return;
            float x = at.x;
            for (int i = 0; i < PlayerKeywords.KeywordCount; i++)
            {
                var k = (Keyword)i;
                int count = keywords.Count(k);
                if (count <= 0) continue;
                var cell = new Rect(x, at.y, KeywordSize, KeywordSize);
                Tint(Ink, 0.85f);
                Blot(Shrink(cell, -0.1f));
                var icon = AThousandDawns.Items.ItemIcons.Get(k);
                Tint(Color.white, 1f);
                if (icon != null) GUI.DrawTexture(Shrink(cell, 0.12f), icon, ScaleMode.ScaleToFit);
                else { Tint(Paper, 1f); Shadowed(cell, PlayerKeywords.Label(k).Substring(0, 1), _small); }
                Tint(Paper, 1f);
                Shadowed(new Rect(cell.xMax - 18f, cell.yMax - 22f, 24f, 22f), $"{count}", _small);
                for (int s = 0; s < 3; s++) // 단계 2·4·6
                {
                    bool lit = count >= (s + 1) * 2;
                    Tint(lit ? (s == 1 ? Gold : Plum) : Ink, lit ? 1f : 0.3f);
                    Blot(new Rect(cell.x + 6f + s * 12f, cell.yMax + 3f, 10f, 10f));
                }
                x += KeywordSize + 14f;
            }
        }

        // 금창약 마시기: 머리 위 원형 게이지 (기획 조작 탭). 먹점이 둘레를 따라 매화 홍으로 차올라요
        static void DrawDrinkGauge(PlayerController p)
        {
            if (!p.IsDrinking) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.4f);
            if (sp.z <= 0f) return;
            Vector2 c = UiScale.FromScreen(sp);
            const int Dots = 20;
            const float Radius = 26f, Dot = 11f;
            float progress = p.PotionProgress;
            for (int i = 0; i < Dots; i++)
            {
                float a = (i / (float)Dots) * Mathf.PI * 2f; // 12시부터 시계 방향
                var pos = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * Radius;
                bool filled = (i + 0.5f) / Dots <= progress;
                Tint(filled ? Plum : Ink, filled ? 1f : 0.35f);
                Blot(new Rect(pos.x - Dot * 0.5f, pos.y - Dot * 0.5f, Dot, Dot));
            }
            if (_gourd != null) // 가운데 호리병
            {
                Tint(Color.white, 1f);
                GUI.DrawTexture(new Rect(c.x - 14f, c.y - 17f, 28f, 34f), _gourd, ScaleMode.ScaleToFit);
            }
        }

        // ── 초식 ──
        static void DrawSkill(PlayerController p, Rect r)
        {
            // 한지 원판 (원상 안쪽)
            var inner = Shrink(r, 0.14f);
            Tint(Paper, 0.92f);
            if (_blot != null) GUI.DrawTexture(Shrink(r, 0.06f), _blot, ScaleMode.ScaleToFit); // 먹 번짐 모양 원판
            Tint(Paper, 1f);
            if (_blot != null) GUI.DrawTexture(Shrink(r, 0.1f), _blot, ScaleMode.ScaleToFit);
            else GUI.DrawTexture(inner, Texture2D.whiteTexture);

            // 기획(무기 탭): 비도 0개면 회수 불가 — 초식 그림이 회색. 기획(조작 탭): 금창약을 마시는 동안 쿨타임 표시는 숨김
            bool noDaggers = p.UsesDagger && !p.CanRecall;
            var icon = noDaggers && _recallGray != null ? _recallGray : SkillIcon(p);
            bool cooling = p.SkillCooldownLeft > 0f && !p.IsCastingSkill && !p.IsDrinking && !noDaggers;
            if (icon != null)
            {
                Tint(Color.white, cooling ? 0.45f : noDaggers ? 0.6f : 1f);
                GUI.DrawTexture(Shrink(r, 0.18f), icon, ScaleMode.ScaleToFit);
            }

            // 쿨타임: 먹물이 원판을 덮었다가 위에서부터 걷혀요 (남은 비율만큼 아래쪽에 남아요)
            if (cooling)
            {
                float left = Mathf.Clamp01(p.SkillCooldownLeft / Mathf.Max(0.01f, p.SkillCooldown));
                float h = inner.height * left;
                var cover = new Rect(inner.x, inner.yMax - h, inner.width, h);
                Tint(Ink, 0.55f);
                if (_blot != null)
                {
                    GUI.BeginGroup(cover);
                    GUI.DrawTexture(new Rect(0f, -(inner.height - h), inner.width, inner.height), _blot, ScaleMode.ScaleToFit);
                    GUI.EndGroup();
                }
                else GUI.DrawTexture(cover, Texture2D.whiteTexture);
                Tint(Paper, 1f);
                Shadowed(r, p.SkillCooldownLeft >= 1f ? $"{p.SkillCooldownLeft:0}" : $"{p.SkillCooldownLeft:0.0}", _number);
            }

            // 원상 테두리 (준비되면 짙게)
            Tint(Ink, cooling ? 0.65f : 1f);
            if (_enso != null) GUI.DrawTexture(r, _enso, ScaleMode.ScaleToFit);

            if (p.UsesDagger) // 비도 남은 수: 오른쪽 위에 먹 번짐 배지 + 큰 숫자 (0개면 매화 홍)
            {
                var badge = new Rect(r.xMax - 46f, r.y - 8f, 54f, 54f);
                Tint(Ink, 0.92f);
                Blot(badge);
                Tint(p.DaggerCount > 0 ? Paper : Plum, 1f);
                Shadowed(badge, $"{p.DaggerCount}", _badge);
            }
            GUI.color = Color.white;
            KeyHint.Draw(new Rect(r.x, r.yMax - 16f, r.width, 28f), KeyHint.Key(Bind.Skill), _key, 14);
        }

        // 비도를 들면 비급을 배워도 초식은 늘 "회수" (비급은 회수에 효과만 덧붙여요) → 회수 그림
        // 비도 + 탁랑참만 따로 "탁류 소용돌이" 그림
        static Texture2D SkillIcon(PlayerController p) => p.UsesDagger ? DaggerIcon(p.Skill) : p.Skill switch
        {
            SkillKind.Takrang when _takrang != null => _takrang,
            SkillKind.BambooFlash => _bamboo,
            SkillKind.Quake => _quake,
            _ when p.Skill.ToString() == "Palm" && _palm != null => _palm, // 금강장 (파계승 혜광의 비급) — 기획 쪽 SkillKind.Palm이 들어오기 전에도 컴파일되게 이름으로
            _ => _spin,
        };

        // 비도: 비급마다 초식이 달라요 (회선참=회수 · 죽림섬=비도 순간이동 · 붕산권=비도 솟구치기 · 금강장=비도 호신륜 · 탁랑참=탁류 소용돌이). 그림이 없으면 회수 그림
        static Texture2D DaggerIcon(SkillKind s) => (s switch
        {
            SkillKind.BambooFlash => _daggerFlash,
            SkillKind.Quake => _daggerBurst,
            SkillKind.Palm => _daggerHalo,
            SkillKind.Takrang => _takrangVortex,
            _ => null,
        }) ?? _recall;

        // ── 체력 ──
        static void DrawHealth(PlayerController p, Rect r)
        {
            float max = Mathf.Max(1f, p.MaxHealth);
            float hp = Mathf.Clamp01(p.Health / max);
            float dt = Time.unscaledDeltaTime;
            if (hp < _lastHp - 0.0001f) _lagHold = 0.45f; // 새로 맞으면 깎인 자리를 잠깐 붙들어요
            _lastHp = hp;
            if (_lagHp < 0f || hp >= _lagHp) _lagHp = hp;
            else if (_lagHold > 0f) _lagHold -= dt;
            else _lagHp = Mathf.MoveTowards(_lagHp, hp, dt * 0.6f);

            // 바탕 획 (먹), 깎인 자리 (한지색), 남은 체력 (매화 홍)
            StrokePart(r, 1f, Ink, 0.85f);
            StrokePart(r, _lagHp, Paper, 0.9f);
            float low = hp < 0.3f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f) : 1f; // 위태로우면 맥박처럼
            StrokePart(Shrink(r, 0.0f, 0.12f), hp, Plum, low);

            int shield = p.Shields != null ? p.Shields.Total : 0;
            if (shield > 0) // 보호막: 위에 가는 흰 획
                StrokePart(new Rect(r.x, r.y - 8f, r.width, 14f), Mathf.Clamp01(shield / max), Color.white, 0.95f);

            Tint(Paper, 1f);
            Shadowed(new Rect(r.x + 18f, r.y, r.width - 36f, r.height), $"{p.Health:0} / {p.MaxHealth:0}", _small, TextAnchor.MiddleLeft);
        }

        // 붓 획을 왼쪽에서 ratio만큼만 (획 그림의 왼쪽 부분을 잘라 그려요)
        static void StrokePart(Rect r, float ratio, Color color, float alpha)
        {
            if (ratio <= 0f) return;
            Tint(color, alpha);
            var part = new Rect(r.x, r.y, r.width * ratio, r.height);
            if (_stroke != null) GUI.DrawTextureWithTexCoords(part, _stroke, new Rect(0f, 0f, ratio, 1f));
            else GUI.DrawTexture(Shrink(part, 0f, 0.2f), Texture2D.whiteTexture);
        }

        // ── 대시 ── (끝 x를 돌려줘요)
        static float DrawDash(PlayerController p, Vector2 at)
        {
            var dash = p.Dash;
            if (dash == null) return at.x;
            float x = at.x;
            bool blocked = p.NoDashLeft > 0f;
            // 붓 '>' 하나 = 대시 한 번. 최대 충전이 늘면 '>'도 늘어요 (>>>)
            for (int i = 0; i < dash.MaxCharges; i++)
            {
                var r = new Rect(x, at.y - 2f, ChevronW, PipSize + 4f);
                Tint(Ink, 0.22f); // 빈 자리
                Chevron(r, 1f);
                if (i < dash.Charges) { Tint(blocked ? Plum : Ink, 1f); Chevron(r, 1f); }
                else if (i == dash.Charges) // 채워지는 중: 왼쪽부터 먹이 차올라요
                {
                    Tint(Ink, 0.6f);
                    Chevron(r, dash.RechargeRatio);
                }
                x += ChevronW * 0.72f; // 살짝 겹쳐 >>> 처럼
            }
            x += ChevronW * 0.4f;
            GUI.color = Color.white;
            KeyHint.Draw(new Rect(x, at.y + 2f, 60f, PipSize - 4f), KeyHint.Key(Bind.Dash), _key, 12);
            return x + 50f;
        }

        // ── 금창약 ──
        static void DrawPotions(PlayerController p, Vector2 at)
        {
            int max = p.PotionMax;
            if (max <= 0) return;
            float x = at.x;
            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x, at.y, GourdSize * 0.8f, GourdSize);
                bool full = i < p.Potions;
                Tint(full ? Color.white : Ink, full ? 1f : 0.22f);
                if (_gourd != null) GUI.DrawTexture(r, _gourd, ScaleMode.ScaleToFit);
                else GUI.DrawTexture(Shrink(r, 0.2f), Texture2D.whiteTexture);
                if (full && i == p.Potions - 1 && p.IsDrinking) // 마시는 중: 아래 붉은 줄이 차요
                {
                    Tint(Ink, 0.6f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax + 2f, r.width, 4f), Texture2D.whiteTexture);
                    Tint(Plum, 1f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax + 2f, r.width * p.PotionProgress, 4f), Texture2D.whiteTexture);
                }
                x += GourdSize * 0.8f + 2f;
            }
            GUI.color = Color.white;
            KeyHint.Draw(new Rect(x + 2f, at.y + GourdSize * 0.5f - 13f, 60f, 26f), KeyHint.Key(Bind.Potion), _key, 12);
        }

        // ── 도움 ──
        // 붓 ">" (ratio = 왼쪽부터 채운 비율)
        static void Chevron(Rect r, float ratio)
        {
            if (ratio <= 0f) return;
            var part = new Rect(r.x, r.y, r.width * ratio, r.height);
            if (_chevron != null) GUI.DrawTextureWithTexCoords(part, _chevron, new Rect(0f, 0f, ratio, 1f));
            else
            {
                GUI.BeginGroup(part);
                GUI.Label(new Rect(0f, 0f, r.width, r.height), ">", _badge);
                GUI.EndGroup();
            }
        }

        static void Blot(Rect r)
        {
            if (_blot != null) GUI.DrawTexture(r, _blot, ScaleMode.ScaleToFit);
            else GUI.DrawTexture(Shrink(r, 0.15f), Texture2D.whiteTexture);
        }

        static void Tint(Color c, float a) => GUI.color = new Color(c.r, c.g, c.b, a);

        static Rect Shrink(Rect r, float ratio) => Shrink(r, ratio, ratio);

        static Rect Shrink(Rect r, float rx, float ry)
        {
            float dx = r.width * rx * 0.5f, dy = r.height * ry * 0.5f;
            return new Rect(r.x + dx, r.y + dy, r.width - dx * 2f, r.height - dy * 2f);
        }

        // 먹 그림자 위에 글 (한지색)
        static void Shadowed(Rect r, string text, GUIStyle style, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            style.alignment = anchor;
            var color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * color.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
        }

        static void Styles()
        {
            if (_number != null) return;
            _number = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _number.normal.textColor = Color.white;
            _small = new GUIStyle(_number) { fontSize = 19 };
            _badge = new GUIStyle(_number) { fontSize = 28 };
            _gaugeLabel = new GUIStyle(_number) { fontSize = 15 };
            _key = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _key.normal.textColor = Paper;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
            _enso = Resources.Load<Texture2D>("HudArt/Enso");
            _gourd = Resources.Load<Texture2D>("HudArt/Potion");
            _spin = Resources.Load<Texture2D>("HudArt/Skill_Spin");
            _bamboo = Resources.Load<Texture2D>("HudArt/Skill_Bamboo");
            _quake = Resources.Load<Texture2D>("HudArt/Skill_Quake");
            _recall = Resources.Load<Texture2D>("HudArt/Skill_Recall");
            _recallGray = Resources.Load<Texture2D>("HudArt/Skill_Recall_Gray"); // 비도 0개 (회수 불가)
            _palm = Resources.Load<Texture2D>("HudArt/Skill_Palm");
            _takrang = Resources.Load<Texture2D>("HudArt/Skill_Takrang");
            _takrangVortex = Resources.Load<Texture2D>("HudArt/Skill_TakrangVortex");
            _daggerFlash = Resources.Load<Texture2D>("HudArt/Skill_DaggerFlash");
            _daggerBurst = Resources.Load<Texture2D>("HudArt/Skill_DaggerBurst");
            _daggerHalo = Resources.Load<Texture2D>("HudArt/Skill_DaggerHalo");
            _blot = Resources.Load<Texture2D>("HudArt/Blot"); // 흰 모양 (GUI.color로 물들여요)
            _chevron = Resources.Load<Texture2D>("HudArt/Chevron"); // 흰 모양 붓 >
        }
    }
}
