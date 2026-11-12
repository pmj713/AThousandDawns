using UnityEngine;

namespace AThousandDawns.Fx
{
    // 이벤트 그림 위 연출 (천마총 이벤트): 고른 결과가 그림 위에서 벌어져요. 이벤트 창은 시간이 멈춰 있어 실제 시간으로 움직여요.
    //   피의 연못 (손을 넣는다): 그림 가운데 아래 웅덩이에서 핏자국이 번지고, 붉은 핏줄이 사방으로 뻗고, 핏방울이 손을 타고 올라가요. 끝난 뒤 그대로 남아요.
    //   마교 제단 (기물을 바친다): 바친 기물 그림이 핏빛 불길 속에서 붉게 물들며 아래로 녹아내려요.
    //   마교 제단 (부순다): 그림이 흔들리고 먹 금이 가며 돌 조각이 튀어 떨어지고 먼지가 번져요.
    // 그림: BloodBuddhaFx/Splash · Drop, TombFx/BloodFlame, TempleFx/Rock, HudArt/HpStroke · Blot (없으면 그 부분만 빠져요).
    // 사용: 고른 순간 InkEventFx.Play(kind, 이벤트 id, 아이콘) → 이벤트 창에서 그림을 그린 뒤 InkEventFx.Draw(그림 칸, 이벤트 id).
    //       그림 칸은 InkEventFx.Shake(칸)으로 흔들어 그려요.
    public static class InkEventFx
    {
        public const string BloodSeep = "blood_seep", AltarMelt = "altar_melt", AltarBreak = "altar_break",
            RelicStamp = "relic_stamp", RelicWipe = "relic_wipe";

        static string _kind, _eventId;
        static float _start;
        static Texture2D _icon;
        static int _seed;
        static Matrix4x4 _matrix; // 돌린 뒤 되돌릴 HUD 배율
        static Texture2D _splash, _drop, _flame, _rock, _stroke, _blot, _seal;
        static bool _loaded;

        // 유품록: 고르거나 뺄 때 (RoomRun.RelicToggled)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            AThousandDawns.Prototype.RoomRun.RelicToggled -= OnRelic;
            AThousandDawns.Prototype.RoomRun.RelicToggled += OnRelic;
        }

        static void OnRelic(string itemId, bool picked) =>
            Play(picked ? RelicStamp : RelicWipe, "relic_book", AThousandDawns.Items.ItemIcons.Get(itemId));

        // 그림마다 같은 자리에 같은 모양이 나오게, 정해진 씨앗에서 같은 순서로 나오는 난수 (System.Random을 매 프레임 만들지 않게)
        sealed class SeqRandom
        {
            uint _s;
            public void Reset(uint seed) => _s = seed * 747796405u + 2891336453u;
            public double NextDouble()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (double)0x1000000;
            }
        }
        static readonly SeqRandom _rng = new SeqRandom();

        public static void Play(string kind, string eventId, Texture2D icon = null)
        {
            _kind = kind;
            _eventId = eventId;
            _icon = icon;
            _start = Time.unscaledTime;
            _seed = Random.Range(0, 10000);
        }

        static bool Active(string eventId) => _kind != null && eventId != null && _eventId != null && eventId.StartsWith(Prefix(_eventId));
        static string Prefix(string id) { int i = id.IndexOf('_', id.IndexOf('_') + 1); return i > 0 ? id.Substring(0, i) : id; } // demon_altar_pick → demon_altar

        public static Rect Shake(Rect r, string eventId)
        {
            if (!Active(eventId) || _kind != AltarBreak) return r;
            float t = Time.unscaledTime - _start;
            if (t > 0.45f) return r;
            float k = (1f - t / 0.45f) * 9f;
            return new Rect(r.x + Mathf.Sin(t * 90f) * k, r.y + Mathf.Cos(t * 73f) * k * 0.6f, r.width, r.height);
        }

        public static void Draw(Rect art, string eventId)
        {
            if (!Active(eventId)) { if (_kind != null && eventId != null) _kind = null; return; }
            Load();
            float t = Time.unscaledTime - _start;
            var oldColor = GUI.color;
            var oldMatrix = _matrix = GUI.matrix;
            GUI.BeginGroup(art); // 그림 밖으로는 안 그려요
            var r = new Rect(0f, 0f, art.width, art.height);
            var rand = _rng; // 같은 순서의 난수를 매 프레임 다시 (새로 만들지 않고 처음으로 되돌려요)
            rand.Reset((uint)_seed);
            switch (_kind)
            {
                case BloodSeep: Seep(r, t, rand); break;
                case AltarMelt: Melt(r, t, rand); break;
                case AltarBreak: Break(r, t, rand); break;
                case RelicStamp: Stamp(r, t, true); break;
                case RelicWipe: Stamp(r, t, false); break;
            }
            GUI.EndGroup();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        // ── 피의 연못: 핏자국이 번지고 핏줄이 뻗어요
        static void Seep(Rect r, float t, SeqRandom rand)
        {
            var c = new Vector2(r.width * 0.5f, r.height * 0.66f);
            float grow = Smooth(t / 2.2f);
            Tint(new Color(0.55f, 0.02f, 0.03f), 0.22f * grow); // 그림 전체가 붉게 물들어요
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            for (int i = 0; i < 9; i++) // 핏자국: 가운데부터 차례로 번져요
            {
                float delay = i * 0.14f, k = Smooth((t - delay) / 1.1f);
                float ang = (float)rand.NextDouble() * Mathf.PI * 2f, dist = i == 0 ? 0f : (0.08f + 0.22f * (float)rand.NextDouble()) * r.width;
                float size = (i == 0 ? 0.34f : 0.12f + 0.1f * (float)rand.NextDouble()) * r.width;
                if (k <= 0f) continue;
                var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.45f) * dist * k;
                Tint(new Color(0.42f, 0.02f, 0.03f), 0.78f * k);
                Blot(_splash, p, size * Mathf.Lerp(0.3f, 1f, k), size * 0.5f * Mathf.Lerp(0.3f, 1f, k), ang * Mathf.Rad2Deg);
            }

            for (int i = 0; i < 10; i++) // 핏줄: 웅덩이에서 사방으로 가늘게 뻗어요
            {
                float ang = i * 36f + (float)rand.NextDouble() * 25f, len = (0.18f + 0.2f * (float)rand.NextDouble()) * r.width;
                float k = Smooth((t - 0.3f - i * 0.05f) / 1.4f);
                if (k <= 0f || _stroke == null) continue;
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f + i);
                Tint(new Color(0.7f, 0.06f, 0.05f), 0.85f * pulse);
                GUIUtility.RotateAroundPivot(ang, c);
                GUI.DrawTextureWithTexCoords(new Rect(c.x, c.y - 5f, len * k, 10f), _stroke, new Rect(0f, 0f, k, 1f));
                GUI.matrix = _matrix;
            }

            if (_drop != null) // 핏방울이 손을 타고 위로 스며 올라가요
                for (int i = 0; i < 12; i++)
                {
                    float life = 1.1f, phase = (t - i * 0.18f) % (life * 2.5f);
                    if (t < i * 0.18f || phase > life) continue;
                    float k = phase / life;
                    float x = c.x + ((float)rand.NextDouble() - 0.5f) * r.width * 0.1f;
                    var p = new Vector2(x + Mathf.Sin(k * 6f + i) * 6f, Mathf.Lerp(c.y, r.height * 0.18f, k));
                    Tint(new Color(0.8f, 0.08f, 0.06f), Mathf.Sin(k * Mathf.PI) * 0.9f);
                    float s = r.height * 0.045f;
                    GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s * 1.3f), _drop, ScaleMode.ScaleToFit);
                }
        }

        // ── 마교 제단 (바친다): 기물이 핏빛 불길에 녹아내려요
        static void Melt(Rect r, float t, SeqRandom rand)
        {
            var c = new Vector2(r.width * 0.5f, r.height * 0.5f);
            float size = r.height * 0.42f, baseY = c.y + size * 0.5f;
            float melt = Smooth((t - 0.5f) / 1.8f); // 0 → 다 녹음
            float fire = Mathf.Clamp01(t / 0.4f) * (1f - Smooth((t - 2.4f) / 0.8f));

            Tint(new Color(0.3f, 0.02f, 0.02f), 0.35f * fire); // 제단 둘레가 어두워져요
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            if (_flame != null) // 뒤 불길
                for (int i = 0; i < 5; i++)
                {
                    float flick = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 13f + i * 1.7f);
                    float w = size * (0.45f + 0.1f * (i % 3)), h = w * 1.6f * flick * fire;
                    float x = c.x + (i - 2f) * size * 0.22f;
                    Tint(new Color(0.9f, 0.25f, 0.2f), 0.75f * fire);
                    GUI.DrawTexture(new Rect(x - w * 0.5f, baseY - h, w, h), _flame, ScaleMode.StretchToFill);
                }

            if (_icon != null && melt < 1f) // 기물: 붉게 물들며 위부터 주저앉아요
            {
                float h = size * (1f - melt), w = size * (1f + 0.25f * melt);
                Tint(Color.Lerp(Color.white, new Color(0.85f, 0.15f, 0.1f), Mathf.Clamp01(t / 1.2f)), 1f - melt * 0.6f);
                var rect = new Rect(c.x - w * 0.5f, baseY - h + Mathf.Sin(Time.unscaledTime * 20f) * 1.5f * melt, w, h);
                GUI.DrawTextureWithTexCoords(rect, _icon, new Rect(0f, 0f, 1f, 1f)); // 비율이 무너지듯 눌려요
            }

            if (_drop != null) // 녹은 핏물이 아래로 떨어져요
                for (int i = 0; i < 10; i++)
                {
                    float start = 0.7f + i * 0.15f, k = (t - start) / 0.6f;
                    if (k < 0f || k > 1f) continue;
                    float x = c.x + ((float)rand.NextDouble() - 0.5f) * size * 0.8f;
                    float s = r.height * 0.04f;
                    Tint(new Color(0.7f, 0.05f, 0.05f), 1f - k);
                    GUI.DrawTexture(new Rect(x - s * 0.5f, baseY + k * k * r.height * 0.3f, s, s * 1.3f), _drop, ScaleMode.ScaleToFit);
                }

            float pool = Smooth((t - 0.8f) / 1.6f); // 바닥에 남는 핏물
            Tint(new Color(0.4f, 0.02f, 0.03f), 0.8f * pool);
            Blot(_splash, new Vector2(c.x, baseY + 4f), size * 1.3f * pool, size * 0.35f * pool, 0f);

            if (_flame != null) // 앞 불길 (기물을 감싸요)
                for (int i = 0; i < 3; i++)
                {
                    float flick = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 17f + i * 2.3f);
                    float w = size * 0.35f, h = w * 1.7f * flick * fire;
                    float x = c.x + (i - 1f) * size * 0.3f;
                    Tint(new Color(1f, 0.35f, 0.25f), 0.85f * fire);
                    GUI.DrawTexture(new Rect(x - w * 0.5f, baseY - h + 6f, w, h), _flame, ScaleMode.StretchToFill);
                }
        }

        // ── 유품록: 고르면 장부에 기물이 먹으로 그려지고 붉은 낙관이 쾅 찍혀요. 빼면 낙관 위로 먹 획이 그어지며 지워져요
        static void Stamp(Rect r, float t, bool picked)
        {
            var c = new Vector2(r.width * 0.5f, r.height * 0.58f); // 펼친 장부 가운데
            float size = r.height * 0.3f;
            float fade = 1f - Smooth((t - (picked ? 1.3f : 0.9f)) / 0.5f);
            if (fade <= 0f) return;
            if (_icon != null) // 기물: 먹이 번지듯 나타나요 (뺄 때는 이미 있던 채로)
            {
                float k = picked ? Smooth(t / 0.25f) : 1f;
                Tint(new Color(0.15f, 0.12f, 0.1f), 0.9f * k * fade * (picked ? 1f : 1f - Smooth((t - 0.25f) / 0.5f)));
                float s = size * Mathf.Lerp(1.15f, 1f, k);
                GUI.DrawTexture(new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s), _icon, ScaleMode.ScaleToFit);
            }
            if (_seal != null)
            {
                var at = c + new Vector2(size * 0.42f, size * 0.3f); // 낙관은 오른쪽 아래 귀퉁이
                float hit = picked ? Smooth((t - 0.2f) / 0.12f) : 1f;
                float s = size * 0.42f * (picked ? Mathf.Lerp(1.8f, 1f, hit) : 1f);
                float a = (picked ? hit : 1f - Smooth((t - 0.2f) / 0.45f)) * fade;
                Tint(Color.white, a);
                GUIUtility.RotateAroundPivot(-8f, at);
                GUI.DrawTexture(new Rect(at.x - s * 0.5f, at.y - s * 0.5f, s, s), _seal, ScaleMode.ScaleToFit);
                GUI.matrix = _matrix;
                if (picked && t > 0.32f && t < 0.6f && _blot != null) // 찍힌 순간 붉은 인주가 튀어요
                {
                    float k = (t - 0.32f) / 0.28f;
                    Tint(new Color(0.8f, 0.1f, 0.08f), 0.5f * (1f - k));
                    float bs = size * 0.42f * (1f + k * 0.8f);
                    GUI.DrawTexture(new Rect(at.x - bs * 0.5f, at.y - bs * 0.5f, bs, bs), _blot, ScaleMode.StretchToFill);
                }
            }
            if (!picked && _stroke != null) // 먹 획이 왼쪽 위에서 오른쪽 아래로 그어져요
            {
                float k = Smooth(t / 0.25f);
                var from = c + new Vector2(-size * 0.6f, -size * 0.45f);
                float len = size * 1.7f;
                Tint(new Color(0.08f, 0.06f, 0.05f), 0.95f * fade);
                GUIUtility.RotateAroundPivot(32f, from);
                GUI.DrawTextureWithTexCoords(new Rect(from.x, from.y - 14f, len * k, 28f), _stroke, new Rect(0f, 0f, k, 1f));
                GUI.matrix = _matrix;
            }
        }

        // ── 마교 제단 (부순다): 먹 금이 가고 돌 조각이 튀어 떨어져요
        static void Break(Rect r, float t, SeqRandom rand)
        {
            var c = new Vector2(r.width * 0.5f, r.height * 0.55f);
            if (_stroke != null) // 금: 가운데서 지그재그로 뻗고 남아요
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * 60f + (float)rand.NextDouble() * 30f;
                    var p = c;
                    for (int seg = 0; seg < 3; seg++)
                    {
                        float len = (0.08f + 0.06f * (float)rand.NextDouble()) * r.width;
                        float k = Mathf.Clamp01((t - seg * 0.06f) / 0.12f);
                        float a = ang + ((float)rand.NextDouble() - 0.5f) * 50f;
                        if (k <= 0f) break;
                        Tint(new Color(0.08f, 0.06f, 0.05f), 0.9f);
                        GUIUtility.RotateAroundPivot(a, p);
                        GUI.DrawTextureWithTexCoords(new Rect(p.x, p.y - 4f, len * k, 8f - seg * 2f), _stroke, new Rect(0f, 0f, k, 1f));
                        GUI.matrix = _matrix;
                        p += new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * len;
                    }
                }

            float dust = Mathf.Clamp01(t / 0.3f) * (1f - Smooth((t - 0.6f) / 1.4f)); // 먼지
            if (_blot != null)
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * 60f * Mathf.Deg2Rad, spread = Smooth(t / 1.2f) * r.width * 0.2f;
                    var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f) * spread;
                    float s = r.width * (0.16f + 0.1f * Smooth(t / 1.5f));
                    Tint(new Color(0.45f, 0.42f, 0.38f), 0.5f * dust);
                    GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.35f, s, s * 0.7f), _blot, ScaleMode.StretchToFill);
                }

            if (_rock != null) // 돌 조각: 튀어 올랐다가 떨어져요
                for (int i = 0; i < 14; i++)
                {
                    float vx = ((float)rand.NextDouble() - 0.5f) * r.width * 1.1f, vy = -(0.4f + 0.6f * (float)rand.NextDouble()) * r.height * 1.4f;
                    float spin = ((float)rand.NextDouble() - 0.5f) * 720f, s = r.height * (0.05f + 0.07f * (float)rand.NextDouble());
                    float tt = t - (float)rand.NextDouble() * 0.08f;
                    if (tt < 0f || tt > 2f) continue;
                    var p = c + new Vector2(vx * tt, vy * tt + 0.5f * r.height * 4.5f * tt * tt);
                    if (p.y > r.height + s) continue;
                    Tint(Color.white, 1f - Smooth((tt - 1.4f) / 0.6f));
                    GUIUtility.RotateAroundPivot(spin * tt, p);
                    GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), _rock, ScaleMode.ScaleToFit);
                    GUI.matrix = _matrix;
                }
        }

        // ── 도움

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static void Tint(Color c, float a) => GUI.color = new Color(c.r, c.g, c.b, a);

        static void Blot(Texture2D tex, Vector2 at, float w, float h, float angle)
        {
            if (tex == null || w <= 0f) return;
            GUIUtility.RotateAroundPivot(angle, at);
            GUI.DrawTexture(new Rect(at.x - w * 0.5f, at.y - h * 0.5f, w, h), tex, ScaleMode.StretchToFill);
            GUI.matrix = _matrix;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _splash = Resources.Load<Texture2D>("BloodBuddhaFx/Splash");
            _drop = Resources.Load<Texture2D>("BloodBuddhaFx/Drop");
            _flame = Resources.Load<Texture2D>("TombFx/BloodFlame");
            _rock = Resources.Load<Texture2D>("TempleFx/Rock");
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
            _blot = Resources.Load<Texture2D>("HudArt/Blot");
            _seal = Resources.Load<Texture2D>("TitleArt/Seal");
        }
    }
}
