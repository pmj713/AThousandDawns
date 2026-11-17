using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 보스·고수 패턴 개편 이펙트 (그림만 — 판정·수치는 게임 코드 그대로). 빛 · 파티클 · 단색 도형, 채색 화풍.
    //   찌르기 (EnemyBrain.Thrust): 청죽객 쾌검 = 가는 대나무빛 직선 / 탁랑객 물살 찌르기 = 굵은 물빛 직선 + 물보라
    //   혈영검마 혈조 (BloodShadowBrain SpikeStart · Spike): 바닥을 타고 뻗는 핏빛 예고 선 → 자리마다 핏빛 가시가 솟았다 가라앉아요
    //   녹림왕 불타는 대나무 (NokrimBrain.BambooFall): 마디진 대나무가 불붙은 채 기울다 쓰러지고, 그 자리에서 2초 타다 숯이 되어 꺼져요
    //   혈불 혈해 (BloodBuddhaBrain Pool): 반들거리는 핏물 웅덩이 + 거품. 혈불이 안에 서 있으면 붉은 빛이 몸을 감싸며 피어올라요 (회복)
    //   철룡 닻 충격파 (IronDragonBrain Ring): 퍼지는 물결 고리 (판정 폭 그대로) + 둘레 물보라
    //   천마: 발도 일섬 (DrawCut) 긴 흰빛 직선 · 마기 포박 (Bind) 느린 보라 구슬 · 묶임 (Bound) 플레이어 둘레 보랏빛 사슬 고리
    //         차원 베기 (RiftOpen → RiftCut) 세로 균열 셋이 벌어지고 꿰뚫는 보랏빛 선이 번쩍 · 유성 (MeteorTrack) 큰 운석 그림자가 따라와요
    //   철권 계열 (IronFistBrain): 잡아 던지기 (Grabbed) 잡는 자리 먼지 + 내동댕이친 자리 흙먼지 고리
    //         먼 거리 도약 (FarLeaped): 오우거 땅울림 = 흙빛 진동 고리 셋 · 족장 덮치기 = 흙먼지 · 데스나이트 어둠 걸음 = 그림자 웅덩이 → 청록 그림자가 솟아요
    //   고블린 족장 덫 (TrapThrown): 쇠덫이 떨어져 벌어져 있다가 플레이어가 밟으면 철컥 닫혀요 (6초)
    //   오우거: 분노 (Enraged) 머리에서 김이 확 터지고 2페이즈 동안 이따금 김이 뿜어져요 · 내려찍기 (가까운 Quake) 바닥 균열
    //   데스나이트 망령 기병 (SpectralRide): 청록 유령 말이 줄을 따라 질주하며 유령불 자국을 남겨요
    //   다크 엘프: 그림자 숨기 (Vanished · Emerged) 보랏빛 연기 · 난무 (DanceCut) 초록 독 방울 (은보라 궤적은 InkSwingFx)
    // 그림: FxArt/InkMist (부드러운 빛), PouchFx/Ring (고리).
    public class InkBossPatternFx : MonoBehaviour
    {
        static InkBossPatternFx _host;
        static Sprite _glow, _ring;
        static Material _solid;
        static MaterialPropertyBlock _block;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        PlayerController _player;
        EnemyDirector _director;
        float _find;

        static readonly Color Teal = new Color(0.3f, 1f, 0.85f);
        static readonly Color Violet = new Color(0.62f, 0.3f, 1f);
        static readonly Color Earth = new Color(0.75f, 0.55f, 0.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.Thrust -= OnThrust;
            EnemyBrain.Thrust += OnThrust;
            BloodShadowBrain.Cue -= OnBloodShadow;
            BloodShadowBrain.Cue += OnBloodShadow;
            NokrimBrain.BambooFall -= OnBamboo;
            NokrimBrain.BambooFall += OnBamboo;
            BloodBuddhaBrain.Cue -= OnBloodBuddha;
            BloodBuddhaBrain.Cue += OnBloodBuddha;
            IronDragonBrain.Cue -= OnIronDragon;
            IronDragonBrain.Cue += OnIronDragon;
            HeavenlyDemonBrain.Cue -= OnDemon;
            HeavenlyDemonBrain.Cue += OnDemon;
            IronFistBrain.Grabbed -= OnGrabbed;
            IronFistBrain.Grabbed += OnGrabbed;
            IronFistBrain.FarLeaped -= OnFarLeaped;
            IronFistBrain.FarLeaped += OnFarLeaped;
            GoblinChiefBrain.TrapThrown -= OnTrap;
            GoblinChiefBrain.TrapThrown += OnTrap;
            OgreBrain.Enraged -= OnOgreEnraged;
            OgreBrain.Enraged += OnOgreEnraged;
            DeathKnightBrain.SpectralRide -= OnRide;
            DeathKnightBrain.SpectralRide += OnRide;
            DarkElfBrain.Vanished -= OnVanished;
            DarkElfBrain.Vanished += OnVanished;
            DarkElfBrain.Emerged -= OnEmerged;
            DarkElfBrain.Emerged += OnEmerged;
            DarkElfBrain.DanceCut -= OnDanceCut;
            DarkElfBrain.DanceCut += OnDanceCut;
            EnemyDirector.Burst -= OnBurst;
            EnemyDirector.Burst += OnBurst;
        }

        static InkBossPatternFx Host()
        {
            if (!_loaded)
            {
                _loaded = true;
                var tex = Resources.Load<Texture2D>("FxArt/InkMist");
                if (tex != null) _glow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _ring = Resources.Load<Sprite>("PouchFx/Ring");
                _solid = new Material(Shader.Find("Sprites/Default")) { name = "Boss Pattern Solid" };
                _block = new MaterialPropertyBlock();
            }
            if (_host == null) _host = new GameObject("Ink Boss Pattern Fx").AddComponent<InkBossPatternFx>();
            return _host;
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            _find -= dt;
            if (_find <= 0f && (_player == null || _director == null)) // 1초에 한 번만 찾아요
            {
                _find = 1f;
                if (_player == null) _player = FindAnyObjectByType<PlayerController>();
                if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }
        static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // ── 찌르기

        static void OnThrust(EnemyBrain who, Vector3 at, Vector3 dir, float length, float width)
        {
            if (who == null) return;
            var h = Host();
            dir = Flat(dir);
            at.y = 0f;
            if (who.Kind == EnemyKind.Takrang) // 물살: 굵은 물빛 줄기 + 줄을 따라 물보라
            {
                h.Streak(at + dir * 0.3f, dir, length, width * 1.4f, new Color(0.35f, 0.7f, 1f, 0.75f), 0.95f, 0.3f, 8);
                h.Streak(at + dir * 0.3f, dir, length, width * 0.4f, new Color(0.85f, 0.95f, 1f, 1f), 0.95f, 0.25f, 9);
                for (int i = 1; i <= 4; i++)
                    InkParticles.Burst(at + dir * (length * i / 4f) + Vector3.up * 0.9f, 6, new Color(0.9f, 0.97f, 1f, 1f), new Color(0.3f, 0.6f, 1f, 0.5f), 2.2f, 0.18f, 0.5f, 1.6f);
            }
            else // 청죽객 쾌검: 가는 대나무빛 직선
            {
                h.Streak(at + dir * 0.3f, dir, length, width * 0.9f, new Color(0.35f, 0.95f, 0.45f, 0.7f), 0.95f, 0.2f, 8);
                h.Streak(at + dir * 0.3f, dir, length, width * 0.22f, new Color(0.9f, 1f, 0.85f, 1f), 0.95f, 0.16f, 9);
                InkParticles.Burst(at + dir * length + Vector3.up * 0.95f, 6, new Color(0.85f, 1f, 0.7f, 1f), new Color(0.3f, 0.8f, 0.3f, 0.3f), 1.6f, 0.1f, 0.3f, 0f);
            }
        }

        // 바닥에 눕힌 부드러운 빛 줄기 (from에서 dir로 length): 0.06초에 뻗고 life 동안 옅어져요
        void Streak(Vector3 from, Vector3 dir, float length, float width, Color color, float height, float life, int order)
        {
            if (_glow == null || length <= 0.01f) return;
            var sr = FlatSprite(_glow, from, 1f, Yaw(dir), height, order, color);
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x);
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float grow = Mathf.Clamp01(t / 0.06f);
                float l = length * grow;
                sr.transform.position = new Vector3(from.x, height, from.z) + dir * (l * 0.5f);
                sr.transform.localScale = new Vector3(width * unit, l * unit * 1.15f, 1f);
                float k = Mathf.Clamp01((t - 0.06f) / life);
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── 혈영검마 혈조

        static void OnBloodShadow(BloodShadowBrain b, BloodShadowBrain.FxCue c)
        {
            switch (c.Kind)
            {
                case BloodShadowBrain.FxKind.SpikeStart: Host().SpikeLine(c.From, c.To, c.Time); break;
                case BloodShadowBrain.FxKind.Spike: Host().Spike(c.From); break;
            }
        }

        void SpikeLine(Vector3 from, Vector3 to, float time)
        {
            if (_glow == null) return;
            from.y = 0f; to.y = 0f;
            Vector3 dir = Flat(to - from);
            float length = Vector3.Distance(from, to);
            var sr = FlatSprite(_glow, from, 1f, Yaw(dir), 0.05f, 4, new Color(0.9f, 0.05f, 0.1f, 0f));
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x);
            float t = 0f, life = Mathf.Max(0.1f, time);
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / life);
                float l = Mathf.Max(0.3f, length * k);
                sr.transform.position = new Vector3(from.x, 0.05f, from.z) + dir * (l * 0.5f);
                sr.transform.localScale = new Vector3(0.7f * unit, l * unit * 1.1f, 1f);
                float end = t <= life ? 1f : Mathf.Clamp01(1f - (t - life) / 0.4f);
                sr.color = new Color(0.9f, 0.05f, 0.1f, 0.75f * end);
                if (t < life + 0.4f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 핏빛 가시 셋: 0.08초에 솟고, 0.35초 버티다 가라앉아요
        void Spike(Vector3 at)
        {
            at.y = 0f;
            var root = new GameObject("Blood Spikes").transform;
            root.SetParent(transform, false);
            root.position = at;
            var parts = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                float h = UnityEngine.Random.Range(1f, 1.6f);
                Vector3 off = i == 0 ? Vector3.zero : Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward * 0.3f;
                var pivot = new GameObject("Spike").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = off;
                pivot.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(0f, 90f), UnityEngine.Random.Range(-18f, 18f));
                Solid(pivot, PrimitiveType.Cube, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.2f, h, 0.2f), Quaternion.Euler(0f, 45f, 0f), new Color(0.7f, 0.04f, 0.09f));
                Solid(pivot, PrimitiveType.Cube, new Vector3(0f, h, 0f), new Vector3(0.12f, 0.3f, 0.12f), Quaternion.Euler(0f, 45f, 0f), new Color(1f, 0.3f, 0.3f));
                parts.Add(pivot);
            }
            if (_glow != null) FadeSprite(FlatSprite(_glow, at, 1.6f, 0f, 0.04f, 4, new Color(1f, 0.1f, 0.1f, 0.8f)), 0.6f);
            InkParticles.Burst(at + Vector3.up * 0.3f, 12, new Color(1f, 0.25f, 0.25f, 1f), new Color(0.5f, 0f, 0.05f, 0.5f), 2.5f, 0.16f, 0.5f, 1.4f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (root == null) return false;
                t += dt;
                float rise = t < 0.08f ? t / 0.08f : t < 0.43f ? 1f : 1f - Mathf.Clamp01((t - 0.43f) / 0.25f);
                foreach (var p in parts) if (p != null) p.localScale = new Vector3(1f, Mathf.Max(0.01f, rise), 1f);
                if (t < 0.7f) return true;
                Destroy(root.gameObject);
                return false;
            });
        }

        // ── 녹림왕 불타는 대나무

        static void OnBamboo(NokrimBrain boss, Vector3 center, Vector3 dir, float length, float width, float fall)
            => Host().Bamboo(center, Flat(dir), length, width, fall);

        void Bamboo(Vector3 center, Vector3 dir, float length, float width, float fall)
        {
            center.y = 0f;
            Vector3 hinge = center - dir * (length * 0.5f);
            Vector3 axis = Vector3.Cross(Vector3.up, dir).normalized;
            var pivot = new GameObject("Burning Bamboo").transform;
            pivot.SetParent(transform, false);
            pivot.position = hinge;
            float d = Mathf.Clamp(width * 0.45f, 0.18f, 0.45f);
            var green = new Color(0.45f, 0.72f, 0.18f);
            var stem = Solid(pivot, PrimitiveType.Cylinder, new Vector3(0f, length * 0.5f, 0f), new Vector3(d, length * 0.5f, d), Quaternion.identity, green);
            var nodes = new List<Renderer> { stem };
            for (float y = 1.4f; y < length - 0.3f && nodes.Count < 16; y += 1.4f)
                nodes.Add(Solid(pivot, PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(d * 1.2f, 0.05f, d * 1.2f), Quaternion.identity, new Color(0.25f, 0.42f, 0.1f)));
            var fire = InkParticles.Fire(pivot, Vector3.zero, 0.5f, 1f);
            InkParticles.EmitBox(fire, new Vector3(0f, length * 0.5f, 0f), new Vector3(d * 1.5f, length, d * 1.5f), 1.2f);
            InkParticles.SetRate(fire, length * 2f);
            var light = PointLight(pivot, new Color(1f, 0.55f, 0.2f), length * 0.6f, 0f);
            light.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            float t = 0f, burn = 2f;
            bool landed = false;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / Mathf.Max(0.1f, fall));
                pivot.rotation = Quaternion.AngleAxis(12f + 78f * k * k * k, axis); // 천천히 기울다 확 쓰러져요
                if (!landed && k >= 1f)
                {
                    landed = true;
                    InkParticles.SetRate(fire, length * 7f);
                    for (int i = 0; i < 4; i++)
                        InkParticles.Burst(hinge + dir * (length * (i + 0.5f) / 4f) + Vector3.up * 0.3f, 10, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.3f, 0.05f, 0.5f), 3f, 0.25f, 0.6f, -0.5f);
                    Flash(center + Vector3.up, new Color(1f, 0.5f, 0.2f), length * 0.8f, 5f, 0.5f);
                }
                if (light != null) light.intensity = landed ? Mathf.Lerp(3f, 0f, Mathf.Clamp01((t - fall) / (burn + 0.4f))) : k * 1.5f;
                if (landed) // 타면서 숯빛으로
                {
                    float c = Mathf.Clamp01((t - fall) / burn);
                    foreach (var r in nodes) if (r != null) Tint(r, Color.Lerp(r == stem ? green : new Color(0.25f, 0.42f, 0.1f), new Color(0.13f, 0.08f, 0.06f), c));
                    if (c >= 1f && fire != null) { InkParticles.SetRate(fire, 0f); }
                    float sink = Mathf.Clamp01((t - fall - burn) / 0.6f);
                    pivot.position = hinge + Vector3.down * (sink * d * 2.5f);
                    if (sink >= 1f) { Destroy(pivot.gameObject); return false; }
                }
                return true;
            });
        }

        // ── 혈불 혈해

        static void OnBloodBuddha(BloodBuddhaBrain b, BloodBuddhaBrain.FxCue c)
        {
            if (c.Kind == BloodBuddhaBrain.FxKind.Pool) Host().Pool(b, c.At, c.Radius, c.Time);
        }

        void Pool(BloodBuddhaBrain boss, Vector3 at, float radius, float life)
        {
            if (_glow == null) return;
            at.y = 0f;
            var pool = FlatSprite(_glow, at, radius * 2.4f, 0f, 0.035f, 3, new Color(0.55f, 0.02f, 0.05f, 0f));
            var shine = FlatSprite(_glow, at + new Vector3(-0.2f, 0f, 0.25f) * radius, radius * 1.1f, 0f, 0.04f, 4, new Color(1f, 0.35f, 0.35f, 0f));
            var rim = _ring != null ? FlatSprite(_ring, at, radius * 2.05f, 0f, 0.045f, 4, new Color(0.35f, 0f, 0.03f, 0f)) : null;
            Vector3 poolUnit = pool.transform.localScale;
            Light heal = null;
            float t = 0f, bubble = 0f, rise = 0f, glow = 0f;
            _live.Add(dt =>
            {
                if (pool == null) return false;
                t += dt;
                float a = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((life - t) / 0.5f);
                pool.transform.localScale = poolUnit * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / 0.3f)));
                pool.color = new Color(0.55f, 0.02f, 0.05f, 0.9f * a);
                if (shine != null) shine.color = new Color(1f, 0.35f, 0.35f, a * (0.25f + 0.1f * Mathf.Sin(t * 3f)));
                if (rim != null) rim.color = new Color(0.35f, 0f, 0.03f, 0.8f * a);
                bubble -= dt;
                if (bubble <= 0f && t < life - 0.3f) // 거품
                {
                    bubble = 0.12f;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 0.9f;
                    InkParticles.Burst(at + new Vector3(r.x, 0.08f, r.y), 2, new Color(1f, 0.3f, 0.3f, 0.9f), new Color(0.5f, 0f, 0.05f, 0f), 0.4f, 0.14f, 0.5f, -0.4f);
                }
                // 혈불이 안에 서 있으면 붉은 빛이 몸을 감싸며 피어올라요 (회복)
                bool inside = boss != null && !boss.IsDead && (new Vector3(boss.transform.position.x, 0f, boss.transform.position.z) - at).sqrMagnitude <= radius * radius;
                glow = Mathf.MoveTowards(glow, inside && t < life ? 1f : 0f, dt * 3f);
                if (glow > 0f && boss != null)
                {
                    if (heal == null) { heal = PointLight(boss.transform, new Color(1f, 0.15f, 0.15f), 7f, 0f); heal.transform.localPosition = Vector3.up * boss.Height * 0.6f; }
                    heal.intensity = glow * (2.5f + 0.8f * Mathf.Sin(t * 6f));
                    rise -= dt;
                    if (rise <= 0f && inside)
                    {
                        rise = 0.1f;
                        Vector2 r = UnityEngine.Random.insideUnitCircle * 1.2f;
                        InkParticles.Burst(boss.transform.position + new Vector3(r.x, 0.3f, r.y), 3, new Color(1f, 0.4f, 0.35f, 1f), new Color(0.9f, 0.05f, 0.1f, 0f), 0.6f, 0.25f, 0.9f, -1.5f);
                    }
                }
                else if (heal != null) heal.intensity = 0f;
                if (t < life) return true;
                Destroy(pool.gameObject);
                if (shine != null) Destroy(shine.gameObject);
                if (rim != null) Destroy(rim.gameObject);
                if (heal != null) Destroy(heal.gameObject);
                return false;
            });
        }

        // ── 철룡 닻 충격파

        static void OnIronDragon(IronDragonBrain b, IronDragonBrain.FxCue c)
        {
            if (c.Kind == IronDragonBrain.FxKind.Ring) Host().WaveRing(c.At, c.Radius, c.Length, c.Time);
        }

        void WaveRing(Vector3 at, float max, float width, float time)
        {
            at.y = 0f;
            var outer = Circle(at, 0.08f, Mathf.Max(0.2f, width), new Color(0.35f, 0.7f, 1f, 0.85f));
            var inner = Circle(at, 0.1f, Mathf.Max(0.08f, width * 0.3f), new Color(0.9f, 0.97f, 1f, 1f));
            float t = 0f, life = Mathf.Max(0.1f, time), spray = 0f;
            _live.Add(dt =>
            {
                if (outer == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / life);
                float r = max * k;
                SetCircle(outer, r);
                SetCircle(inner, r);
                float a = Mathf.Clamp01((1f - k) / 0.15f);
                outer.startColor = outer.endColor = new Color(0.35f, 0.7f, 1f, 0.85f * a);
                inner.startColor = inner.endColor = new Color(0.9f, 0.97f, 1f, a);
                spray -= dt;
                if (spray <= 0f && r > 0.5f)
                {
                    spray = 0.04f;
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    InkParticles.Burst(at + new Vector3(Mathf.Cos(ang), 0.1f, Mathf.Sin(ang)) * r, 3, new Color(0.9f, 0.97f, 1f, 1f), new Color(0.3f, 0.6f, 1f, 0.3f), 2f, 0.16f, 0.45f, 2f);
                }
                if (k < 1f) return true;
                Destroy(outer.gameObject);
                if (inner != null) Destroy(inner.gameObject);
                return false;
            });
        }

        // ── 천마

        static void OnDemon(HeavenlyDemonBrain b, HeavenlyDemonBrain.FxCue c)
        {
            switch (c.Kind)
            {
                case HeavenlyDemonBrain.FxKind.DrawCut: Host().Cut(c.At, c.To, c.Radius, new Color(0.85f, 0.8f, 1f, 0.9f), Color.white); break;
                case HeavenlyDemonBrain.FxKind.RiftCut: Host().Cut(c.At, c.To, c.Radius, new Color(Violet.r, Violet.g, Violet.b, 0.9f), new Color(0.95f, 0.85f, 1f, 1f)); break;
                case HeavenlyDemonBrain.FxKind.Bind: Host().BindOrb(c.Target, c.Radius, c.Time); break;
                case HeavenlyDemonBrain.FxKind.Bound: Host().Chains(c.Target, c.Time); break;
                case HeavenlyDemonBrain.FxKind.RiftOpen: Host().RiftOpen(c.At, c.Time); break;
                case HeavenlyDemonBrain.FxKind.MeteorTrack: Host().MeteorShadow(c.Target, c.At, c.Radius, c.Time); break;
            }
        }

        // 긴 빛 직선 (발도 일섬 = 흰빛, 차원 베기 = 보랏빛): 공중 빛줄기 + 바닥 자국 + 불빛 번쩍 + 불티
        void Cut(Vector3 from, Vector3 to, float width, Color outer, Color core)
        {
            from.y = 0f; to.y = 0f;
            Vector3 dir = Flat(to - from);
            float length = Vector3.Distance(from, to);
            width = Mathf.Max(0.3f, width);
            Streak(from, dir, length, width * 1.8f, outer, 1f, 0.35f, 8);
            Streak(from, dir, length, width * 0.45f, core, 1f, 0.3f, 9);
            Streak(from, dir, length, width * 1.2f, new Color(outer.r, outer.g, outer.b, 0.5f), 0.05f, 0.6f, 4);
            Flash(Vector3.Lerp(from, to, 0.5f) + Vector3.up, new Color(outer.r, outer.g, outer.b), length * 0.6f, 5f, 0.35f);
            for (int i = 0; i < 5; i++)
                InkParticles.Burst(from + dir * (length * (i + 0.5f) / 5f) + Vector3.up, 4, core, new Color(outer.r, outer.g, outer.b, 0.3f), 1.8f, 0.12f, 0.4f, 0f);
        }

        // 마기 포박 구슬: 날아가는 구슬(Target)에 보라 빛 + 흰 속 + 도는 사슬 고리
        void BindOrb(Transform target, float radius, float time)
        {
            if (target == null || _glow == null) return;
            var pivot = new GameObject("Bind Orb").transform;
            pivot.SetParent(target, false);
            pivot.gameObject.AddComponent<InkFaceCamera>();
            float size = Mathf.Max(0.6f, radius * 2f);
            GlowOrb(pivot, size * 1.6f, new Color(Violet.r, Violet.g, Violet.b, 0.7f), 9);
            GlowOrb(pivot, size * 0.6f, new Color(0.95f, 0.85f, 1f, 1f), 10);
            SpriteRenderer chain = _ring != null ? FaceSprite(pivot, _ring, size * 1.1f, new Color(0.35f, 0.2f, 0.55f, 0.9f), 11) : null;
            var smoke = InkParticles.Smoke(target, Vector3.zero, size * 0.4f, 1.5f, 1f);
            var light = PointLight(target, Violet, 4f, 1.5f);
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (target == null || t > time + 2f)
                {
                    if (pivot != null) Destroy(pivot.gameObject);
                    if (smoke != null) Destroy(smoke.gameObject);
                    if (light != null) Destroy(light.gameObject);
                    return false;
                }
                pivot.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(t * 10f));
                if (chain != null) chain.transform.localRotation = Quaternion.Euler(0f, 0f, t * 160f);
                return true;
            });
        }

        // 묶임: 플레이어 둘레 보랏빛 사슬 고리 셋이 돌다가 풀리면 부서져요
        void Chains(Transform target, float time)
        {
            if (target == null || _ring == null) return;
            var root = new GameObject("Bound Chains").transform;
            root.SetParent(target, false);
            var rings = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var spin = new GameObject("Chain").transform;
                spin.SetParent(root, false);
                spin.localPosition = Vector3.up * (0.45f + 0.4f * i);
                var sr = new GameObject("Ring").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(spin, false);
                sr.transform.localRotation = Quaternion.Euler(78f, 0f, i * 25f);
                sr.transform.localScale = Vector3.one * (1.3f / Mathf.Max(0.01f, _ring.bounds.size.x));
                sr.sprite = _ring;
                sr.color = new Color(0.55f, 0.3f, 0.9f, 0.95f);
                sr.sortingOrder = 9;
                rings.Add(spin);
            }
            Vector3 at = target.position;
            InkParticles.Burst(at + Vector3.up, 16, new Color(0.8f, 0.7f, 1f, 1f), new Color(0.3f, 0.1f, 0.5f, 0.4f), 2.5f, 0.2f, 0.5f, 0f);
            float t = 0f, life = Mathf.Max(0.2f, time);
            _live.Add(dt =>
            {
                t += dt;
                if (root == null || target == null) { if (root != null) Destroy(root.gameObject); return false; }
                for (int i = 0; i < rings.Count; i++) rings[i].localRotation = Quaternion.Euler(0f, t * (i % 2 == 0 ? 220f : -180f), 0f);
                float squeeze = Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(t / 0.15f));
                root.localScale = Vector3.one * squeeze;
                if (t < life) return true;
                InkParticles.Burst(target.position + Vector3.up, 14, new Color(0.75f, 0.65f, 0.9f, 1f), new Color(0.4f, 0.3f, 0.5f, 0f), 3f, 0.14f, 0.45f, 2f);
                Destroy(root.gameObject);
                return false;
            });
        }

        // 차원 균열: 세로로 갈라진 보랏빛 틈이 Time 동안 벌어지고, 베기 직후 닫혀요
        void RiftOpen(Vector3 at, float time)
        {
            if (_glow == null) return;
            at.y = 0f;
            var pivot = new GameObject("Dimension Rift").transform;
            pivot.SetParent(transform, false);
            pivot.position = at + Vector3.up * 1.3f;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var outer = GlowOrb(pivot, 1f, new Color(Violet.r, Violet.g, Violet.b, 0f), 9);
            var inner = GlowOrb(pivot, 1f, new Color(0.95f, 0.9f, 1f, 0f), 10);
            var ground = FlatSprite(_glow, at, 2f, 0f, 0.05f, 4, new Color(Violet.r, Violet.g, Violet.b, 0f));
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x);
            float t = 0f, life = Mathf.Max(0.2f, time), spark = 0f;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float open = t < life ? Mathf.Clamp01(t / life) : Mathf.Clamp01(1f - (t - life) / 0.25f);
                float jitter = 1f + 0.12f * Mathf.Sin(t * 45f);
                outer.transform.localScale = new Vector3(0.9f * open * jitter, 3.4f * (0.5f + 0.5f * open), 1f) * unit;
                inner.transform.localScale = new Vector3(0.28f * open, 2.8f * (0.5f + 0.5f * open), 1f) * unit;
                outer.color = new Color(Violet.r, Violet.g, Violet.b, 0.85f * open);
                inner.color = new Color(0.95f, 0.9f, 1f, open);
                if (ground != null) ground.color = new Color(Violet.r, Violet.g, Violet.b, 0.5f * open);
                spark -= dt;
                if (spark <= 0f && t < life)
                {
                    spark = 0.08f;
                    InkParticles.Burst(at + Vector3.up * UnityEngine.Random.Range(0.3f, 2.4f), 2, new Color(0.85f, 0.75f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0f), 0.8f, 0.12f, 0.5f, -0.5f);
                }
                if (t < life + 0.25f) return true;
                Destroy(pivot.gameObject);
                if (ground != null) Destroy(ground.gameObject);
                return false;
            });
        }

        // 유성 추적: 큰 운석 그림자가 예고(Target)를 따라오며 짙어지고, 하늘에 붉은 불덩이 빛이 커져요 (떨어지는 불덩이는 InkHeavenlyDemonFx Mark)
        void MeteorShadow(Transform target, Vector3 at, float radius, float time)
        {
            if (_glow == null) return;
            var shadow = FlatSprite(_glow, at, radius * 2.4f, 0f, 0.06f, 4, new Color(0.08f, 0.02f, 0.04f, 0f));
            var sky = new GameObject("Meteor Glow").transform;
            sky.SetParent(transform, false);
            sky.gameObject.AddComponent<InkFaceCamera>();
            var hot = GlowOrb(sky, 1f, new Color(1f, 0.35f, 0.08f, 0f), 9);
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x);
            Vector3 shadowUnit = shadow.transform.localScale;
            float t = 0f, life = Mathf.Max(0.2f, time);
            _live.Add(dt =>
            {
                if (shadow == null) return false;
                t += dt;
                if (target != null) at = target.position;
                at.y = 0f;
                float k = Mathf.Clamp01(t / life);
                shadow.transform.position = new Vector3(at.x, 0.06f, at.z);
                shadow.transform.localScale = shadowUnit * Mathf.Lerp(0.6f, 1f, k);
                shadow.color = new Color(0.08f, 0.02f, 0.04f, Mathf.Lerp(0.25f, 0.7f, k));
                sky.position = at + new Vector3(1.5f, 10f, -1.5f);
                hot.transform.localScale = Vector3.one * unit * Mathf.Lerp(1f, 3f, k);
                hot.color = new Color(1f, 0.35f, 0.08f, Mathf.Lerp(0.3f, 0.9f, k));
                if (t < life) return true;
                Destroy(shadow.gameObject);
                Destroy(sky.gameObject);
                return false;
            });
        }

        // ── 철권 계열 (철권 마웅 · 고블린 족장 · 오우거 · 데스나이트)

        static void OnGrabbed(IronFistBrain boss, Vector3 at, Vector3 dir)
        {
            if (boss == null) return;
            var h = Host();
            at.y = 0f;
            InkParticles.Burst(at + Vector3.up * 0.8f, 12, new Color(0.9f, 0.8f, 0.6f, 1f), new Color(0.6f, 0.5f, 0.35f, 0.4f), 2f, 0.3f, 0.5f, 0.5f);
            float t = 0f;
            h._live.Add(dt => // 내동댕이친 플레이어가 떨어지는 자리에 흙먼지 고리
            {
                t += dt;
                if (t < 0.4f) return true;
                Vector3 p = h._player != null ? h._player.transform.position : at + Flat(dir) * 3f;
                p.y = 0f;
                h.Dust(p, 2.2f, Earth);
                return false;
            });
        }

        static void OnFarLeaped(IronFistBrain boss, Vector3 from, Vector3 to)
        {
            if (boss == null) return;
            var h = Host();
            from.y = 0f; to.y = 0f;
            switch (boss.Kind)
            {
                case EnemyKind.Ogre: // 땅울림: 흙빛 진동 고리 셋 + 흙먼지 + 균열
                    for (int i = 0; i < 3; i++) h.GroundRing(to, 3f + i * 2.5f, 0.5f, new Color(0.95f, 0.6f, 0.3f, 0.85f), i * 0.15f);
                    h.Dust(to, 3f, Earth);
                    h.Cracks(to, 3f);
                    break;
                case EnemyKind.DeathKnight: // 어둠 걸음: 떠난 자리 그림자 웅덩이가 꺼지고, 내려앉은 자리에서 그림자가 솟아요
                    h.ShadowPool(from, 2.2f, 0.7f, false);
                    h.ShadowPool(to, 2.6f, 0.9f, true);
                    h.GroundRing(to, 4f, 0.45f, new Color(Teal.r, Teal.g, Teal.b, 0.8f), 0f);
                    break;
                case EnemyKind.GoblinChief: // 덮치기: 초록빛 흙먼지
                    h.Dust(to, 2.4f, new Color(0.6f, 0.65f, 0.35f));
                    h.GroundRing(to, 3f, 0.4f, new Color(0.55f, 0.85f, 0.3f, 0.8f), 0f);
                    break;
                default:
                    h.Dust(to, 2.4f, Earth);
                    h.GroundRing(to, 3f, 0.4f, new Color(0.6f, 0.9f, 1f, 0.8f), 0f);
                    break;
            }
        }

        void Dust(Vector3 at, float radius, Color color)
        {
            InkParticles.Burst(at + Vector3.up * 0.2f, 22, new Color(color.r, color.g, color.b, 0.9f), new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 0.3f), radius * 1.6f, 0.45f, 0.8f, -0.1f);
            GroundRing(at, radius * 1.6f, 0.45f, new Color(color.r, color.g, color.b, 0.7f), 0f);
        }

        // 바닥 고리가 size까지 퍼지며 옅어져요
        void GroundRing(Vector3 at, float size, float life, Color color, float delay)
        {
            if (_ring == null) return;
            var sr = FlatSprite(_ring, at, size, UnityEngine.Random.Range(0f, 360f), 0.06f, 5, new Color(color.r, color.g, color.b, 0f));
            Vector3 unit = sr.transform.localScale;
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 그림자 웅덩이 (데스나이트 어둠 걸음): rise면 웅덩이에서 청록 그림자가 솟구쳐요, 아니면 빨려 들며 꺼져요
        void ShadowPool(Vector3 at, float size, float life, bool rise)
        {
            if (_glow == null) return;
            var dark = FlatSprite(_glow, at, size * 1.4f, 0f, 0.05f, 4, new Color(0.03f, 0.08f, 0.08f, 0f));
            var rim = FlatSprite(_glow, at, size * 1.8f, 0f, 0.04f, 3, new Color(Teal.r, Teal.g, Teal.b, 0f));
            Vector3 unit = dark.transform.localScale, rimUnit = rim.transform.localScale;
            if (rise) InkParticles.Burst(at + Vector3.up * 0.2f, 26, new Color(0.6f, 1f, 0.95f, 1f), new Color(0.05f, 0.3f, 0.3f, 0.5f), 1.4f, 0.4f, 0.9f, -2.5f);
            else InkParticles.Burst(at + Vector3.up * 1.2f, 16, new Color(0.1f, 0.25f, 0.25f, 0.9f), new Color(Teal.r, Teal.g, Teal.b, 0f), 1.2f, 0.35f, 0.5f, 2f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (dark == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / life);
                float s = rise ? Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(k)) : Mathf.Lerp(1f, 0.2f, k);
                dark.transform.localScale = unit * s;
                rim.transform.localScale = rimUnit * s;
                float a = rise ? Mathf.Sin(k * Mathf.PI) : 1f - k;
                dark.color = new Color(0.03f, 0.08f, 0.08f, 0.85f * a);
                rim.color = new Color(Teal.r, Teal.g, Teal.b, 0.5f * a);
                if (k < 1f) return true;
                Destroy(dark.gameObject);
                Destroy(rim.gameObject);
                return false;
            });
        }

        // ── 고블린 족장 덫

        static void OnTrap(GoblinChiefBrain chief, Vector3 at) => Host().Trap(at);

        void Trap(Vector3 at)
        {
            at.y = 0f;
            var root = new GameObject("Goblin Trap").transform;
            root.SetParent(transform, false);
            root.position = at + Vector3.up * 1.5f;
            var metal = new Color(0.42f, 0.42f, 0.48f);
            var tooth = new Color(0.8f, 0.8f, 0.85f);
            Solid(root, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.03f, 0.5f), Quaternion.identity, new Color(0.25f, 0.22f, 0.2f));
            Solid(root, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(1.3f, 0.05f, 0.08f), Quaternion.identity, metal); // 경첩 막대
            var jaws = new Transform[2];
            for (int j = 0; j < 2; j++)
            {
                var jaw = new GameObject("Jaw").transform;
                jaw.SetParent(root, false);
                jaw.localPosition = new Vector3(0f, 0.06f, 0f);
                float side = j == 0 ? 1f : -1f;
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.PI * i / 6f;
                    var p = new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f * side);
                    Solid(jaw, PrimitiveType.Cube, p, new Vector3(0.18f, 0.05f, 0.08f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg * side, 0f), metal); // 테
                    Solid(jaw, PrimitiveType.Cube, p + Vector3.up * 0.1f, new Vector3(0.05f, 0.2f, 0.05f), Quaternion.Euler(0f, 45f, 0f), tooth); // 이빨
                }
                jaws[j] = jaw;
            }
            float t = 0f, close = -1f, life = 6f;
            _live.Add(dt =>
            {
                if (root == null) return false;
                t += dt;
                float drop = Mathf.Clamp01(t / 0.25f);
                root.position = at + Vector3.up * (1.5f * (1f - drop * drop));
                if (t >= 0.25f && t - dt < 0.25f) InkParticles.Burst(at + Vector3.up * 0.1f, 8, new Color(0.7f, 0.6f, 0.45f, 0.9f), new Color(0.5f, 0.4f, 0.3f, 0f), 1.2f, 0.25f, 0.5f, 0f);
                if (close < 0f && t > 0.3f && _player != null)
                {
                    Vector3 d = _player.transform.position - at;
                    d.y = 0f;
                    if (d.sqrMagnitude < 0.9f * 0.9f)
                    {
                        close = 0f;
                        InkParticles.Burst(at + Vector3.up * 0.4f, 12, new Color(1f, 0.95f, 0.7f, 1f), new Color(0.6f, 0.6f, 0.6f, 0f), 3f, 0.08f, 0.3f, 1f);
                    }
                }
                if (close >= 0f)
                {
                    close += dt;
                    float c = Mathf.Clamp01(close / 0.08f) * 82f; // 철컥
                    jaws[0].localRotation = Quaternion.Euler(-c, 0f, 0f);
                    jaws[1].localRotation = Quaternion.Euler(c, 0f, 0f);
                }
                if (t < life) return true;
                float sink = Mathf.Clamp01((t - life) / 0.4f);
                root.localScale = Vector3.one * (1f - sink);
                if (sink < 1f) return true;
                Destroy(root.gameObject);
                return false;
            });
        }

        // ── 오우거

        static void OnOgreEnraged(OgreBrain ogre)
        {
            if (ogre == null) return;
            var h = Host();
            float head = Mathf.Max(2f, ogre.Height);
            InkParticles.Burst(ogre.transform.position + Vector3.up * head, 30, new Color(1f, 0.95f, 0.9f, 0.9f), new Color(1f, 0.35f, 0.25f, 0f), 3f, 0.6f, 1.1f, -0.5f);
            h.Flash(ogre.transform.position + Vector3.up * head, new Color(1f, 0.3f, 0.2f), 8f, 4f, 0.5f);
            float puff = 1f;
            h._live.Add(dt => // 2페이즈 동안 이따금 머리에서 김이 뿜어져요
            {
                if (ogre == null || ogre.IsDead || !ogre.IsEnraged) return false;
                puff -= dt;
                if (puff > 0f) return true;
                puff = UnityEngine.Random.Range(1.2f, 2f);
                Vector3 side = new Vector3(ogre.transform.right.x, 0f, ogre.transform.right.z) * 0.35f;
                for (int s = -1; s <= 1; s += 2)
                    InkParticles.Burst(ogre.transform.position + Vector3.up * head * 0.95f + side * s, 6, new Color(1f, 1f, 1f, 0.8f), new Color(1f, 0.6f, 0.5f, 0f), 1f, 0.4f, 0.8f, -0.8f);
                return true;
            });
        }

        // 오우거 내려찍기 (가까운 Quake): 바닥 균열
        static void OnBurst(Vector3 at, float radius, AThousandDawns.Combat.BurstKind kind)
        {
            if (kind != AThousandDawns.Combat.BurstKind.Quake || _host == null || _host._director == null || _host._director.Enemies == null) return;
            foreach (var e in _host._director.Enemies)
                if (e is OgreBrain o && !o.IsDead && (o.transform.position - at).sqrMagnitude < (radius + 3f) * (radius + 3f))
                {
                    _host.Cracks(at, Mathf.Max(1.5f, radius));
                    return;
                }
        }

        void Cracks(Vector3 at, float radius)
        {
            at.y = 0f;
            int count = 6;
            var lines = new List<LineRenderer>();
            float turn = UnityEngine.Random.Range(0f, 60f);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, turn + i * 360f / count + UnityEngine.Random.Range(-15f, 15f), 0f) * Vector3.forward;
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                var lr = Line(new Color(0.2f, 0.1f, 0.05f, 1f), 0.14f, 5);
                float len = radius * UnityEngine.Random.Range(0.8f, 1.3f);
                for (int p = 0; p < 5; p++)
                {
                    float f = p / 4f;
                    lr.SetPosition(p, at + dir * (len * f) + side * (p == 0 ? 0f : UnityEngine.Random.Range(-0.25f, 0.25f)) + Vector3.up * 0.04f);
                }
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
                lines.Add(lr);
            }
            SpriteRenderer heat = _glow != null ? FlatSprite(_glow, at, radius * 2f, 0f, 0.03f, 3, new Color(1f, 0.5f, 0.15f, 0.6f)) : null;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float a = 1f - Mathf.Clamp01((t - 0.8f) / 0.6f);
                foreach (var lr in lines) if (lr != null) lr.startColor = lr.endColor = new Color(0.2f, 0.1f, 0.05f, a);
                if (heat != null) heat.color = new Color(1f, 0.5f, 0.15f, 0.6f * (1f - Mathf.Clamp01(t / 0.6f)));
                if (t < 1.4f) return true;
                foreach (var lr in lines) if (lr != null) Destroy(lr.gameObject);
                if (heat != null) Destroy(heat.gameObject);
                return false;
            });
        }

        // ── 데스나이트 망령 기병

        static void OnRide(DeathKnightBrain k, Vector3 from, Vector3 to) => Host().Ride(from, to);

        void Ride(Vector3 from, Vector3 to)
        {
            if (_glow == null) return;
            from.y = 0f; to.y = 0f;
            Vector3 dir = Flat(to - from);
            const float run = 0.45f;
            var pivot = new GameObject("Spectral Rider").transform;
            pivot.SetParent(transform, false);
            pivot.position = from + Vector3.up * 1f;
            var face = new GameObject("Glow").transform;
            face.SetParent(pivot, false);
            face.gameObject.AddComponent<InkFaceCamera>();
            GlowOrb(face, 2.4f, new Color(Teal.r, Teal.g, Teal.b, 0.6f), 9);          // 말 몸
            GlowOrb(face, 1f, new Color(0.85f, 1f, 0.97f, 0.95f), 10);                  // 빛나는 속
            var head = new GameObject("Head").transform;                                   // 앞으로 내민 말 머리
            head.SetParent(pivot, false);
            head.localPosition = dir * 1.1f + Vector3.up * 0.4f;
            head.gameObject.AddComponent<InkFaceCamera>();
            GlowOrb(head, 1.1f, new Color(Teal.r, Teal.g, Teal.b, 0.75f), 9);
            var rider = new GameObject("Rider").transform;                                 // 등 위 기수
            rider.SetParent(pivot, false);
            rider.localPosition = -dir * 0.2f + Vector3.up * 1f;
            rider.gameObject.AddComponent<InkFaceCamera>();
            GlowOrb(rider, 1f, new Color(0.5f, 1f, 0.9f, 0.7f), 9);
            var wisp = InkParticles.Frost(pivot, Vector3.zero, 0.6f, 3f);
            var trail = Line(new Color(Teal.r, Teal.g, Teal.b, 0.7f), 0.7f, 2);
            trail.SetPosition(0, from + Vector3.up * 0.05f);
            trail.SetPosition(1, from + Vector3.up * 0.05f);
            float t = 0f, hoof = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / run);
                Vector3 p = Vector3.Lerp(from, to, k);
                if (pivot != null)
                {
                    pivot.position = p + Vector3.up * (1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 30f)));
                    hoof -= dt;
                    if (hoof <= 0f && k < 1f) { hoof = 0.05f; InkParticles.Burst(p + Vector3.up * 0.1f, 3, new Color(0.7f, 1f, 0.95f, 1f), new Color(Teal.r, Teal.g, Teal.b, 0f), 1f, 0.2f, 0.4f, -1f); }
                    if (k >= 1f) { if (wisp != null) wisp.transform.SetParent(transform, true); Destroy(pivot.gameObject); pivot = null; }
                }
                if (trail != null)
                {
                    trail.SetPosition(1, p + Vector3.up * 0.05f);
                    float a = 0.7f * (1f - Mathf.Clamp01((t - run) / 0.6f));
                    trail.startColor = trail.endColor = new Color(Teal.r, Teal.g, Teal.b, a);
                }
                if (wisp != null && k >= 1f) InkParticles.SetRate(wisp, 0f);
                if (t < run + 0.6f) return true;
                if (trail != null) Destroy(trail.gameObject);
                if (wisp != null) Destroy(wisp.gameObject, 1.5f);
                return false;
            });
        }

        // ── 다크 엘프

        static void OnVanished(DarkElfBrain elf, Vector3 at) => Host().Shadow(at, false);
        static void OnEmerged(DarkElfBrain elf, Vector3 at) => Host().Shadow(at, true);

        void Shadow(Vector3 at, bool emerge)
        {
            at.y = 0f;
            InkParticles.Burst(at + Vector3.up * 0.9f, 26, new Color(0.35f, 0.2f, 0.5f, 0.95f), new Color(0.1f, 0.05f, 0.15f, 0f), 2.2f, 0.7f, 0.9f, -0.3f);
            InkParticles.Burst(at + Vector3.up * 0.9f, 10, new Color(0.85f, 0.75f, 1f, 1f), new Color(0.6f, 0.3f, 1f, 0f), 2.6f, 0.12f, 0.4f, 0f);
            if (emerge) GroundRing(at, 3f, 0.4f, new Color(0.7f, 0.4f, 1f, 0.8f), 0f);
            Flash(at + Vector3.up, new Color(0.6f, 0.35f, 1f), 4f, 2.5f, 0.3f);
        }

        static void OnDanceCut(DarkElfBrain elf, Vector3 at, Vector3 dir, bool left)
        {
            dir = Flat(dir);
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (left ? -1f : 1f);
            Host();
            InkParticles.Burst(at + dir * 1.2f + side * 0.5f + Vector3.up * 0.9f, 10, new Color(0.65f, 1f, 0.35f, 1f), new Color(0.25f, 0.7f, 0.1f, 0.4f), 2.4f, 0.14f, 0.55f, 2.5f);
        }

        // ── 도움

        SpriteRenderer FlatSprite(Sprite sprite, Vector3 at, float size, float yaw, float height, int order, Color color)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void FadeSprite(SpriteRenderer sr, float life)
        {
            if (sr == null) return;
            Color c = sr.color;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        static SpriteRenderer GlowOrb(Transform parent, float size, Color color, int order) => FaceSprite(parent, _glow, size, color, order);

        static SpriteRenderer FaceSprite(Transform parent, Sprite sprite, float size, Color color, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // 단색 도형 (콜라이더 없음, 그림자 없음)
        static Renderer Solid(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Quaternion rot, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _solid;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Tint(r, color);
            return r;
        }

        static void Tint(Renderer r, Color color)
        {
            r.GetPropertyBlock(_block);
            _block.SetColor("_Color", color);
            r.SetPropertyBlock(_block);
        }

        LineRenderer Line(Color color, float width, int points)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.sharedMaterial = _solid;
            lr.startColor = lr.endColor = color;
            lr.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕혀요
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        LineRenderer Circle(Vector3 at, float height, float width, Color color)
        {
            var lr = Line(color, width, 48);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.transform.position = new Vector3(at.x, height, at.z);
            lr.transform.rotation = Quaternion.identity;
            lr.alignment = LineAlignment.TransformZ;
            lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetCircle(lr, 0f);
            return lr;
        }

        static readonly Vector3[] CirclePoints = new Vector3[48];
        static void SetCircle(LineRenderer lr, float radius)
        {
            if (lr == null) return;
            for (int i = 0; i < CirclePoints.Length; i++)
            {
                float a = i * Mathf.PI * 2f / CirclePoints.Length;
                CirclePoints[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f); // 눕힌 transform 기준 (x, y) = 바닥 (x, z)
            }
            lr.SetPositions(CirclePoints);
        }

        static Light PointLight(Transform parent, Color color, float range, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        void Flash(Vector3 at, Color color, float range, float intensity, float life)
        {
            var holder = new GameObject("Flash").transform;
            holder.SetParent(transform, false);
            holder.position = at;
            var light = PointLight(holder, color, range, intensity);
            float t = 0f;
            _live.Add(dt =>
            {
                if (holder == null) return false;
                t += dt;
                light.intensity = intensity * Mathf.Clamp01(1f - t / life);
                if (t < life) return true;
                Destroy(holder.gameObject);
                return false;
            });
        }
    }
}
