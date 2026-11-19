using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 보스·고수 패턴 개편 이펙트 (그림만 — 판정·수치는 게임 코드 그대로). 빛 · 파티클 · 단색 도형, 채색 화풍.
    //   찌르기 (EnemyBrain.Thrust): 청죽객 쾌검 = 가는 대나무빛 직선 / 탁랑객 물살 찌르기 = 굵은 물빛 직선 + 물보라
    //   혈영검마 혈조 (BloodShadowBrain SpikeStart · Spike): 바닥을 타고 뻗는 핏빛 예고 선 → 자리마다 핏빛 가시가 솟았다 가라앉아요
    //   녹림왕 불타는 대나무 (NokrimBrain.BambooFall): 마디진 대나무가 불붙은 채 기울다 쓰러지고, 그 자리에서 2초 타다 숯이 되어 꺼져요
    //   혈불 혈해 (BloodBuddhaBrain Pool): 반들거리는 핏물 웅덩이 + 거품. 혈불이 안에 서 있으면 붉은 빛이 몸을 감싸며 피어올라요 (회복)
    //   철룡 닻 충격파 (IronDragonBrain Ring): 물마루가 말려 올라가는 3D 파도 고리가 판정 띠를 따라 퍼지고 + 흰 물보라
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
            IronFistBrain.FarLeapDescending -= OnFarLeapDescending;
            IronFistBrain.FarLeapDescending += OnFarLeapDescending;
            GoblinChiefBrain.TrapThrown -= OnTrap;
            GoblinChiefBrain.TrapThrown += OnTrap;
            GoblinChiefBrain.TrapLanded -= OnTrapLanded;
            GoblinChiefBrain.TrapLanded += OnTrapLanded;
            OgreBrain.Enraged -= OnOgreEnraged;
            OgreBrain.Enraged += OnOgreEnraged;
            DeathKnightBrain.SpectralRide -= OnRide;
            DeathKnightBrain.SpectralRide += OnRide;
            DeathKnightBrain.CurseWave -= OnCurseWave;
            DeathKnightBrain.CurseWave += OnCurseWave;
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
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
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
            Renderer stem = null;
            var nodes = new List<Renderer>();
            // 플레이테스트 6차: VARCO 대나무 (Resources/Models/BurningBamboo: 밑동이 원점, 길이 1로 +Y, 꺾인 밑동은 그을렸어요). 길이에 맞춰 통째로 키워요
            var model = BambooModel != null ? Instantiate(BambooModel, pivot, false) : null;
            if (model != null)
            {
                model.transform.localScale = Vector3.one * length;
                model.GetComponentsInChildren(true, _modelRenderers);
                foreach (var r in _modelRenderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; nodes.Add(r); }
            }
            else
            {
                stem = Solid(pivot, PrimitiveType.Cylinder, new Vector3(0f, length * 0.5f, 0f), new Vector3(d, length * 0.5f, d), Quaternion.identity, green);
                nodes.Add(stem);
                for (float y = 1.4f; y < length - 0.3f && nodes.Count < 16; y += 1.4f)
                    nodes.Add(Solid(pivot, PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(d * 1.2f, 0.05f, d * 1.2f), Quaternion.identity, new Color(0.25f, 0.42f, 0.1f)));
            }
            var fire = InkParticles.Fire(pivot, Vector3.zero, 0.5f, 1f);
            InkParticles.EmitBox(fire, new Vector3(0f, length * 0.5f, 0f), new Vector3(d * 1.5f, length, d * 1.5f), 1.2f);
            InkParticles.SetRate(fire, length * 2f);
            var light = PointLight(pivot, new Color(1f, 0.55f, 0.2f), length * 0.6f, 0f);
            light.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            float t = 0f, burn = 2f;
            bool landed = false, hidden = false;
            SpriteRenderer strip = null;
            Transform ground = null;
            float stripUnit = _glow != null ? 1f / Mathf.Max(0.01f, _glow.bounds.size.x) : 1f;
            _live.Add(dt =>
            {
                if (pivot == null)
                {
                    if (strip != null) Destroy(strip.gameObject);
                    if (ground != null) Destroy(ground.gameObject);
                    return false;
                }
                t += dt;
                float k = Mathf.Clamp01(t / Mathf.Max(0.1f, fall));
                pivot.rotation = Quaternion.AngleAxis(12f + 78f * k * k * k, axis); // 천천히 기울다 확 쓰러져요
                if (landed && !hidden && t - fall < 0.5f) hidden = HideRoot("Burning Bamboo (불타는 대나무)", center, 1f); // 임시 불 띠는 렌더러만 꺼요
                if (strip != null) // 판정 폭(width) 그대로 바닥 불길 띠
                {
                    float fade = 1f - Mathf.Clamp01((t - fall - burn) / 0.4f);
                    strip.color = new Color(1f, 0.45f, 0.1f, (0.6f + 0.12f * Mathf.Sin(t * 13f)) * fade);
                }
                if (!landed && k >= 1f)
                {
                    landed = true;
                    if (_glow != null)
                    {
                        strip = FlatSprite(_glow, center, 1f, Yaw(dir), 0.04f, 3, new Color(1f, 0.45f, 0.1f, 0.6f));
                        strip.transform.localScale = new Vector3(width * 1.2f * stripUnit, length * 1.08f * stripUnit, 1f);
                    }
                    ground = new GameObject("Bamboo Ground Fire").transform;
                    ground.SetParent(transform, false);
                    ground.SetPositionAndRotation(center, Quaternion.LookRotation(dir));
                    var flames = InkParticles.Fire(ground, Vector3.zero, 0.35f, 1f);
                    InkParticles.EmitBox(flames, Vector3.zero, new Vector3(width, 0.1f, length), 0.9f);
                    InkParticles.SetRate(flames, length * width * 4f);
                    Destroy(ground.gameObject, burn + 0.2f);
                    InkParticles.SetRate(fire, length * 7f);
                    for (int i = 0; i < 4; i++)
                        InkParticles.Burst(hinge + dir * (length * (i + 0.5f) / 4f) + Vector3.up * 0.3f, 10, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.3f, 0.05f, 0.5f), 3f, 0.25f, 0.6f, -0.5f);
                    Flash(center + Vector3.up, new Color(1f, 0.5f, 0.2f), length * 0.8f, 5f, 0.5f);
                }
                if (light != null) light.intensity = landed ? Mathf.Lerp(3f, 0f, Mathf.Clamp01((t - fall) / (burn + 0.4f))) : k * 1.5f;
                if (landed) // 타면서 숯빛으로
                {
                    float c = Mathf.Clamp01((t - fall) / burn);
                    if (model != null) foreach (var r in nodes) { if (r != null) TintBase(r, Color.Lerp(Color.white, new Color(0.2f, 0.13f, 0.1f), c)); }   // 모델은 그림 색에 곱해요
                    else foreach (var r in nodes) if (r != null) Tint(r, Color.Lerp(r == stem ? green : new Color(0.25f, 0.42f, 0.1f), new Color(0.13f, 0.08f, 0.06f), c));
                    if (c >= 1f && fire != null) { InkParticles.SetRate(fire, 0f); }
                    float sink = Mathf.Clamp01((t - fall - burn) / 0.6f);
                    pivot.position = hinge + Vector3.down * (sink * d * 2.5f);
                    if (sink >= 1f)
                    {
                        Destroy(pivot.gameObject);
                        if (strip != null) Destroy(strip.gameObject);
                        return false;
                    }
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
            HideRoot("혈해", at, 1f); // 임시 붉은 원판은 렌더러만 꺼요 (오브젝트·이름은 테스트용으로 남겨요)
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

        // (플레이테스트 8차: 얇은 선 고리 대신 "파도가 퍼지는 걸로") 물이 솟아 말려 올라가는 3D 파도 고리가 바깥으로 퍼져요.
        //   단면: 뒤쪽 물 → 등 → 물마루(흰 포말, 판정 띠 자리) → 바깥으로 말려 나온 입술 → 아래로 말린 끝 → 앞면 → 앞 발치.
        //   반경은 판정 고리와 같이 max까지 time초, 처음 0.15초에 솟고 끝 15%에 가라앉아요. 물마루를 따라 흰 물보라가 튀어요.
        //   메시는 고리마다 하나, 매 프레임 꼭짓점만 다시 놓아요 (둘레가 조금씩 출렁여요).
        const int MoundSides = 64;
        static readonly float[] MoundOffset = { -0.75f, -0.4f, 0f, 0.3f, 0.42f, 0.28f, 0.2f };   // × 띠 두께, + = 바깥
        static readonly float[] MoundHeight = { 0f, 0.45f, 1f, 0.96f, 0.72f, 0.3f, 0f };          // × 파도 높이
        static readonly Color[] MoundColor =
        {
            new Color(0.12f, 0.38f, 0.72f, 0.55f), new Color(0.25f, 0.6f, 0.92f, 0.8f), new Color(0.95f, 0.98f, 1f, 1f),
            new Color(0.9f, 0.97f, 1f, 1f), new Color(0.75f, 0.9f, 1f, 0.95f), new Color(0.3f, 0.65f, 0.95f, 0.85f), new Color(0.15f, 0.42f, 0.78f, 0.5f),
        };
        static Material _moundMat;

        void WaveRing(Vector3 at, float max, float width, float time)
        {
            at.y = 0f;
            HideRoot("충격파", at, 1f); // 임시 LineRenderer 고리는 렌더러만 꺼요 (판정 고리와 같은 반경 · 두께로 아래 둔덕이 대신 퍼져요)
            if (_moundMat == null) _moundMat = new Material(Shader.Find("Sprites/Default")) { name = "Shock Wave" };
            width = Mathf.Max(0.3f, width);
            float height = Mathf.Clamp(width * 1.1f, 0.5f, 1f); // 파도 높이
            int rows = MoundOffset.Length;
            var mesh = new Mesh { name = "Shock Wave Ring" };
            var verts = new Vector3[MoundSides * rows];
            var colors = new Color[verts.Length];
            var bumps = new float[MoundSides];
            for (int i = 0; i < MoundSides; i++)
            {
                bumps[i] = i * 0.7f; // 둘레마다 출렁이는 위상
                for (int s = 0; s < rows; s++) colors[i * rows + s] = MoundColor[s];
            }
            var tris = new int[MoundSides * (rows - 1) * 6];
            int n = 0;
            for (int i = 0; i < MoundSides; i++)
            {
                int i1 = (i + 1) % MoundSides;
                for (int s = 0; s < rows - 1; s++)
                {
                    int a = i * rows + s, b = i * rows + s + 1, c = i1 * rows + s + 1, d = i1 * rows + s;
                    tris[n++] = a; tris[n++] = b; tris[n++] = c;
                    tris[n++] = a; tris[n++] = c; tris[n++] = d;
                }
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            var go = new GameObject("Shock Wave");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _moundMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            float t = 0f, life = Mathf.Max(0.1f, time), dust = 0f;
            _live.Add(dt =>
            {
                if (go == null) { Destroy(mesh); return false; }
                t += dt;
                float k = Mathf.Clamp01(t / life);
                float r = max * k;
                float rise = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - k) / 0.15f);
                for (int i = 0; i < MoundSides; i++)
                {
                    float ang = i * Mathf.PI * 2f / MoundSides;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    for (int s = 0; s < rows; s++)
                    {
                        float rr = Mathf.Max(0f, r + MoundOffset[s] * width);
                        verts[i * rows + s] = new Vector3(cs * rr, MoundHeight[s] * height * rise * (1f + 0.15f * Mathf.Sin(t * 6f + bumps[i])), sn * rr);
                    }
                }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
                dust -= dt;
                if (dust <= 0f && r > 0.5f && rise > 0.3f) // 물마루를 따라 흰 물보라
                {
                    dust = 0.03f;
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    var p = at + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (r + 0.2f * width);
                    InkParticles.Burst(p + Vector3.up * height * 0.9f, 4, new Color(0.95f, 0.99f, 1f, 1f), new Color(0.4f, 0.7f, 1f, 0f), 2f, 0.18f, 0.5f, 2.2f);
                }
                if (k < 1f) return true;
                Destroy(go);
                Destroy(mesh);
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
                case HeavenlyDemonBrain.FxKind.RiftOpen: { var h = Host(); h.RiftOpen(c.At, c.Time); h.QueueRift(c.At, c.Radius, c.Time); break; }
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

        // 먼 거리 도약 착지 직전 (FarLeapDescending: 땅에 닿기까지 time초): 내려앉을 자리에 보스 그림자가 점점 짙고 커지며, 둘레 고리가 조여 들어요.
        //   땅에 닿는 순간 사라지고 착지 연출(FarLeaped)이 이어요.
        static void OnFarLeapDescending(IronFistBrain boss, Vector3 from, Vector3 to, float time)
        {
            if (boss == null) return;
            Host().LandingShadow(to, Mathf.Max(2f, boss.Height * 1.2f), Mathf.Max(0.05f, time));
        }

        void LandingShadow(Vector3 at, float size, float time)
        {
            if (_glow == null) return;
            at.y = 0f;
            var shadow = FlatSprite(_glow, at, size, 0f, 0.05f, 4, new Color(0.05f, 0.03f, 0.06f, 0f));
            var ring = _ring != null ? FlatSprite(_ring, at, size * 1.1f, 0f, 0.055f, 5, new Color(0.15f, 0.08f, 0.1f, 0f)) : null;
            Vector3 unit = shadow.transform.localScale, ringUnit = ring != null ? ring.transform.localScale : Vector3.one;
            float t = 0f;
            _live.Add(dt =>
            {
                if (shadow == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / time);
                shadow.transform.localScale = unit * Mathf.Lerp(0.45f, 1f, k);
                shadow.color = new Color(0.05f, 0.03f, 0.06f, Mathf.Lerp(0.15f, 0.8f, k * k));
                if (ring != null)
                {
                    ring.transform.localScale = ringUnit * Mathf.Lerp(1.7f, 1f, k);
                    ring.color = new Color(0.15f, 0.08f, 0.1f, 0.7f * k);
                }
                if (t < time + 0.04f) return true;
                Destroy(shadow.gameObject);
                if (ring != null) Destroy(ring.gameObject);
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

        // (플레이테스트 8차: 덫은 던진 뒤 예고 원이 0.7초 차오르고 나서 GhostFire "덫"이 생겨요)
        //   던지는 순간(TrapThrown) 족장 손에서 쇠덫이 빙글 돌며 포물선으로 날아가 0.7초 뒤 떨어질 자리에 내려앉고,
        //   덫이 깔리는 순간 (GoblinChiefBrain.TrapLanded: 자리 · 덫 장판) 그 장판의 하늘색 원판을 끄고 판정 반경에 톱니 원을 깔아요 (장판이 끝나면 같이 걷혀요).
        const float TrapFlight = 0.7f;

        static void OnTrap(GoblinChiefBrain chief, Vector3 at) => Host().Trap(chief != null ? chief.transform.position : at, at);

        // 덫이 실제로 깔린 순간: 날아가는 쇠덫 중 그 자리에 가장 가까운 것에 장판을 넘겨요 (이름으로 찾지 않아요)
        sealed class PendingTrap { public Vector3 At; public GhostFire Zone; }
        readonly List<PendingTrap> _pendingTraps = new List<PendingTrap>();

        static void OnTrapLanded(GoblinChiefBrain chief, Vector3 at, GhostFire zone)
        {
            if (zone == null) return;
            var h = Host();
            at.y = 0f;
            PendingTrap best = null;
            float bestD = 1f;
            foreach (var p in h._pendingTraps)
            {
                float d = (p.At - at).sqrMagnitude;
                if (p.Zone == null && d < bestD) { bestD = d; best = p; }
            }
            if (best != null) best.Zone = zone;
        }

        void Trap(Vector3 from, Vector3 at)
        {
            at.y = 0f;
            Vector3 start = new Vector3(from.x, 1.6f, from.z);
            var root = new GameObject("Goblin Trap").transform;
            root.SetParent(transform, false);
            root.position = start;
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
            // 덫 장판 (GhostFire "덫"): 내려앉을 무렵 생겨요 — 0.1초마다 찾다가 찾으면 하늘색 원판을 끄고 판정 반경 그대로 녹슨 톱니 원
            GhostFire zone = null;
            LineRenderer saw = null;
            SpriteRenderer floor = null;
            float radius = 0.9f;
            var pending = new PendingTrap { At = at };
            _pendingTraps.Add(pending);
            bool tracked = false, settled = false;
            float t = 0f, close = -1f, life = TrapFlight + 6f;
            _live.Add(dt =>
            {
                if (root == null) return false;
                t += dt;
                if (t < TrapFlight) // 포물선으로 빙글 돌며 날아가요
                {
                    float k = t / TrapFlight;
                    root.position = Vector3.Lerp(start, at, k) + Vector3.up * (2.2f * 4f * k * (1f - k));
                    root.rotation = Quaternion.Euler(k * 540f, 0f, 0f);
                    return true;
                }
                if (!settled)
                {
                    settled = true;
                    root.SetPositionAndRotation(at, Quaternion.identity);
                    InkParticles.Burst(at + Vector3.up * 0.1f, 8, new Color(0.7f, 0.6f, 0.45f, 0.9f), new Color(0.5f, 0.4f, 0.3f, 0f), 1.2f, 0.25f, 0.5f, 0f);
                }
                if (!tracked && pending.Zone != null) // 덫이 깔린 순간 (GoblinChiefBrain.TrapLanded가 장판을 넘겨줘요)
                {
                    zone = pending.Zone;
                    tracked = true;
                    radius = zone.transform.localScale.x * 0.5f;
                    var source = zone.GetComponent<Renderer>(); // 하늘색 원판은 렌더러만 꺼요
                    if (source != null) source.enabled = false;
                }
                if (tracked || t >= TrapFlight + 0.8f) _pendingTraps.Remove(pending); // 신호를 받았거나 기다릴 만큼 기다렸어요
                if (saw == null && (tracked || t >= TrapFlight + 0.8f) && t < life)
                {
                    saw = SawRing(at, radius);
                    floor = _glow != null ? FlatSprite(_glow, at, radius * 2.2f, 0f, 0.035f, 3, new Color(0.45f, 0.12f, 0.05f, 0.45f)) : null;
                }
                if (tracked && zone == null && t < life) life = t; // 장판이 먼저 끝나면 같이 걷혀요
                if (saw != null) saw.transform.rotation = Quaternion.Euler(90f, t * 25f, 0f);
                if (t >= life && saw != null)
                {
                    Destroy(saw.gameObject);
                    if (floor != null) Destroy(floor.gameObject);
                    saw = null;
                }
                if (close < 0f && saw != null && _player != null)
                {
                    Vector3 d = _player.transform.position - at;
                    d.y = 0f;
                    if (d.sqrMagnitude < radius * radius)
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

        // ── 데스나이트 망령 기병 (플레이테스트 3차 다시): 반투명 청록 유령 말(코드로 만든 3D 말, 네 다리가 달려요) 위에
        //   데스나이트 실루엣(그 순간 모습을 구운 메시)이 타고 줄을 따라 질주해요. 지나간 자리엔 말 잔상이 흐려지며 남고,
        //   말발굽마다 유령불이 튀고, 바닥엔 청록 유령불 자국이 남았다 사라져요.

        static Mesh _horse, _leg;
        static Material _ghost;
        Mesh _riderMesh;
        DeathKnightBrain _riderOf;
        float _riderBaked = -10f;

        static void OnRide(DeathKnightBrain k, Vector3 from, Vector3 to) => Host().Ride(k, from, to);

        void Ride(DeathKnightBrain knight, Vector3 from, Vector3 to)
        {
            from.y = 0f; to.y = 0f;
            Vector3 dir = Flat(to - from);
            float length = Vector3.Distance(from, to);
            float run = Mathf.Clamp(length / 30f, 0.35f, 0.7f); // 초당 약 30칸
            GhostAssets();
            var horse = new GameObject("Spectral Rider").transform;
            horse.SetParent(transform, false);
            horse.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            var model = HorseModel(); // 메인 아트 VARCO 유령마 (말 + 망령 기사 한 덩어리, 플레이테스트 9차). 없으면 코드 말 + 데스나이트 실루엣
            var modelParts = model != null ? GhostModel(horse, model) : null;
            var body = model == null ? GhostPart(horse, _horse, Vector3.zero, Teal, 0.55f) : null;
            var legs = new Transform[model == null ? 4 : 0];
            var legRenderers = new Renderer[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                var hip = new GameObject("Leg").transform;
                hip.SetParent(horse, false);
                hip.localPosition = new Vector3(i % 2 == 0 ? 0.2f : -0.2f, 1f, i < 2 ? 0.6f : -0.6f);
                legRenderers[i] = GhostPart(hip, _leg, Vector3.zero, Teal, 0.55f);
                legs[i] = hip;
            }
            var riderMesh = model == null ? RiderMesh(knight) : null;
            Renderer rider = null;
            if (riderMesh != null)
            {
                rider = GhostPart(horse, riderMesh, new Vector3(0f, 0.75f, -0.15f), new Color(0.6f, 1f, 0.95f), 0.5f);
                rider.transform.localScale = Vector3.one * 0.85f;
            }
            var eyes = new GameObject("Eyes").transform; // 말 머리 쪽 빛
            eyes.SetParent(horse, false);
            eyes.localPosition = new Vector3(0f, 1.9f, 1.35f);
            eyes.gameObject.AddComponent<InkFaceCamera>();
            if (_glow != null) GlowOrb(eyes, 0.5f, new Color(0.8f, 1f, 0.95f, 0.9f), 10);
            var wisp = InkParticles.Frost(horse, new Vector3(0f, 1.1f, -0.6f), 0.6f, 3f);
            // 지나간 자리: 딱딱한 띠 대신 옅은 청록 유령불 자국 (부드러운 빛 줄기가 말을 따라 늘어났다 걷혀요)
            SpriteRenderer trail = _glow != null ? FlatSprite(_glow, from, 1f, Yaw(dir), 0.05f, 4, new Color(Teal.r, Teal.g, Teal.b, 0f)) : null;
            float trailUnit = _glow != null ? 1f / Mathf.Max(0.01f, _glow.bounds.size.x) : 1f;
            Flash(from + Vector3.up, Teal, 6f, 3f, 0.4f);
            float t = 0f, hoof = 0f, ghost = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / run);
                Vector3 p = Vector3.Lerp(from, to, k);
                if (horse != null)
                {
                    float gallop = t * 22f;
                    horse.position = p + Vector3.up * (0.12f * Mathf.Abs(Mathf.Sin(gallop)));
                    horse.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(6f * Mathf.Sin(gallop), 0f, 0f); // 앞뒤로 들썩
                    for (int i = 0; i < legs.Length; i++) legs[i].localRotation = Quaternion.Euler(38f * Mathf.Sin(gallop + (i < 2 ? 0f : Mathf.PI) + (i % 2) * 0.6f), 0f, 0f);
                    float a = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((run - t) / 0.12f + 0.3f); // 나타났다 끝에서 흐려져요
                    var c = new Color(Teal.r, Teal.g, Teal.b, 0.55f * a);
                    if (body != null) Tint(body, c);
                    if (modelParts != null) foreach (var mr in modelParts) Tint(mr, c);
                    foreach (var lr in legRenderers) Tint(lr, c);
                    if (rider != null) Tint(rider, new Color(0.6f, 1f, 0.95f, 0.5f * a));
                    hoof -= dt;
                    if (hoof <= 0f && k < 1f) { hoof = 0.05f; InkParticles.Burst(p + Vector3.up * 0.1f, 3, new Color(0.7f, 1f, 0.95f, 1f), new Color(Teal.r, Teal.g, Teal.b, 0f), 1.2f, 0.22f, 0.45f, -1f); }
                    ghost -= dt;
                    if (ghost <= 0f && k < 1f) { ghost = 0.06f; Afterimage(horse.position, horse.rotation, model); }
                    if (k >= 1f)
                    {
                        InkParticles.Burst(p + Vector3.up * 1.2f, 18, new Color(0.7f, 1f, 0.95f, 1f), new Color(Teal.r, Teal.g, Teal.b, 0f), 2.5f, 0.35f, 0.6f, -0.8f); // 끝에서 흩어져요
                        if (wisp != null) { wisp.transform.SetParent(transform, true); InkParticles.SetRate(wisp, 0f); Destroy(wisp.gameObject, 1.5f); }
                        Destroy(horse.gameObject);
                        horse = null;
                    }
                }
                if (trail != null)
                {
                    float len = Mathf.Max(0.3f, Vector3.Distance(from, p));
                    trail.transform.position = new Vector3(from.x, 0.05f, from.z) + dir * (len * 0.5f);
                    trail.transform.localScale = new Vector3(1.1f * trailUnit, len * 1.05f * trailUnit, 1f);
                    trail.color = new Color(Teal.r, Teal.g, Teal.b, 0.35f * (1f - Mathf.Clamp01((t - run) / 0.6f)));
                }
                if (t < run + 0.6f) return true;
                if (trail != null) Destroy(trail.gameObject);
                return false;
            });
        }

        // 메인 아트 VARCO 유령마 (Resources/Models/SpectralHorse: 앞 +Z, 발이 원점, 높이 2.2, 애니메이션 없음) — 처음 한 번만 불러요
        static GameObject _horseModel;
        static bool _horseChecked;
        static GameObject HorseModel()
        {
            if (!_horseChecked) { _horseChecked = true; _horseModel = Resources.Load<GameObject>("Models/SpectralHorse"); }
            return _horseModel;
        }

        // 모델을 parent 밑에 놓고 모든 렌더러를 반투명 청록 유령 재질로 (원래 그림(_BaseMap)은 _MainTex로 곱해요). 충돌체 · 그림자 없음
        List<Renderer> GhostModel(Transform parent, GameObject model)
        {
            var go = Instantiate(model, parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var list = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                Texture tex = null;
                var src = r.sharedMaterial;
                if (src != null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                else if (src != null && src.HasProperty("_MainTex")) tex = src.mainTexture;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = _ghost;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (tex != null)
                {
                    r.GetPropertyBlock(_block);
                    _block.SetTexture("_MainTex", tex);
                    r.SetPropertyBlock(_block);
                }
                Tint(r, new Color(Teal.r, Teal.g, Teal.b, 0.55f));
                list.Add(r);
            }
            return list;
        }

        // 말 잔상: 몸통만 그 자리에 남아 0.3초에 흐려져요
        void Afterimage(Vector3 at, Quaternion rot, GameObject model)
        {
            var go = new GameObject("Rider Afterimage");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at, rot);
            var parts = model != null ? GhostModel(go.transform, model) : new List<Renderer> { GhostPart(go.transform, _horse, Vector3.zero, Teal, 0.3f) };
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                foreach (var r in parts) if (r != null) Tint(r, new Color(Teal.r, Teal.g, Teal.b, 0.3f * (1f - t / 0.3f)));
                if (t < 0.3f) return true;
                Destroy(go);
                return false;
            });
        }

        Renderer GhostPart(Transform parent, Mesh mesh, Vector3 local, Color color, float alpha)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _ghost;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Tint(r, new Color(color.r, color.g, color.b, alpha));
            return r;
        }

        // 기수: 데스나이트의 그 순간 모습을 메시로 구워요 (한 돌격 묶음에 한 번, 1초 안이면 다시 써요)
        Mesh RiderMesh(DeathKnightBrain knight)
        {
            if (knight == null) return _riderMesh;
            if (_riderMesh != null && _riderOf == knight && Time.time - _riderBaked < 1f) return _riderMesh;
            var skin = knight.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) return null;
            if (_riderMesh == null) _riderMesh = new Mesh { name = "Spectral Knight" };
            skin.BakeMesh(_riderMesh, true);
            // 구운 메시는 skin 자리 기준이라, 데스나이트 몸 기준(앞 = +Z)으로 돌려 놓아요
            var toKnight = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(knight.transform.rotation) * skin.transform.rotation, Vector3.one);
            var v = _riderMesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = toKnight.MultiplyPoint3x4(v[i]);
            _riderMesh.vertices = v;
            var colors = new Color[v.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.Lerp(new Color(0.3f, 0.75f, 0.7f), Color.white, Mathf.Clamp01(v[i].y / 2.2f)); // 위로 갈수록 밝게
            _riderMesh.colors = colors;
            _riderMesh.RecalculateBounds();
            _riderOf = knight;
            _riderBaked = Time.time;
            return _riderMesh;
        }

        // 유령 말 (앞 = +Z, 발이 원점 높이): 몸통 · 목 · 갈기 · 머리 · 귀 · 꼬리 / 다리는 따로 (엉덩이가 원점, 아래로 1)
        static void GhostAssets()
        {
            if (_ghost == null) _ghost = new Material(Shader.Find("Sprites/Default")) { name = "Spectral Rider" };
            if (_horse != null) return;
            Color light = new Color(0.75f, 1f, 0.95f), dark = new Color(0.1f, 0.5f, 0.5f), mane = new Color(0.9f, 1f, 1f);
            var b = new MeshBits();
            b.Xf = Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            b.Cone(-0.85f, 0.85f, 0.32f, 0.4f, 10, light, dark);                                // 몸통 (엉덩이 → 가슴)
            b.Xf = Matrix4x4.TRS(new Vector3(0f, 1.3f, 0.7f), Quaternion.Euler(40f, 0f, 0f), Vector3.one);
            b.Cone(0f, 0.75f, 0.24f, 0.15f, 8, light, dark);                                   // 목
            b.Box(new Vector3(0f, 0.4f, -0.17f), new Vector3(0.05f, 0.75f, 0.12f), mane, light); // 갈기 (목 뒤쪽)
            b.Xf = Matrix4x4.TRS(new Vector3(0f, 1.9f, 1.25f), Quaternion.Euler(60f, 0f, 0f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.22f, 0.6f, 0.3f), light, dark);                  // 머리
            b.Xf = Matrix4x4.TRS(new Vector3(0.07f, 2.1f, 1.08f), Quaternion.Euler(-15f, 0f, 0f), Vector3.one);
            b.Cone(0f, 0.18f, 0.04f, 0.005f, 4, light, dark);                                  // 귀
            b.Xf = Matrix4x4.TRS(new Vector3(-0.07f, 2.1f, 1.08f), Quaternion.Euler(-15f, 0f, 0f), Vector3.one);
            b.Cone(0f, 0.18f, 0.04f, 0.005f, 4, light, dark);
            b.Xf = Matrix4x4.TRS(new Vector3(0f, 1.3f, -0.85f), Quaternion.Euler(-130f, 0f, 0f), Vector3.one);
            b.Cone(0f, 0.8f, 0.12f, 0.02f, 6, mane, light);                                    // 꼬리 (뒤로 날려요)
            _horse = b.Build("Ghost Horse");
            var l = new MeshBits();
            l.Cone(-1f, 0f, 0.07f, 0.11f, 6, light, dark);
            l.Box(new Vector3(0f, -0.97f, 0.02f), new Vector3(0.13f, 0.08f, 0.16f), mane, light); // 발굽
            _leg = l.Build("Ghost Leg");
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

        // ── 예고 흐름 화살표 (곧 이 줄을 따라 지나가요)
        //   데스나이트 망령 기병 줄: CurseWave 순간 새로 뜬 띠 예고(Telegraph, 방향 = 달리는 방향) 셋마다, 예고가 사라질 때까지
        //   천마 차원 베기: RiftOpen 셋의 가운데(= 플레이어 자리)를 지나 맞은편까지, 균열이 벌어지는 동안

        readonly HashSet<Telegraph> _flowed = new HashSet<Telegraph>();
        readonly List<(Vector3 at, float radius, float time)> _rifts = new List<(Vector3, float, float)>();
        int _riftFrame = -1;

        static void OnCurseWave(DeathKnightBrain k, Vector3 dir)
        {
            if (k == null) return;
            var h = Host();
            dir = Flat(dir);
            h._flowed.RemoveWhere(m => m == null);
            foreach (var m in FindObjectsByType<Telegraph>()) // 예고가 뜰 때 한 번만 찾아요
            {
                if (m == null || !m.IsVisible || h._flowed.Contains(m) || Vector3.Dot(m.transform.forward, dir) < 0.98f) continue;
                var filter = m.GetComponentInChildren<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                h._flowed.Add(m);
                var marker = m;
                h.LaneMist(() => marker != null && marker.IsVisible, m.transform.position, dir, b.max.z, b.size.x);
            }
        }

        // 망령 기병 줄 (플레이테스트 10차: 갈매기 선 · 띠가 어색 → 게임풍): 예고 띠 위로 옅은 청록 유령 안개가 깔려 숨 쉬고,
        //   청록 유령불 알갱이가 달리는 방향으로 흘러가요 (선 없이 흐름만). 예고가 사라지면 알갱이가 멎고 안개가 걷혀요.
        void LaneMist(Func<bool> alive, Vector3 from, Vector3 dir, float length, float width)
        {
            from.y = 0f;
            Vector3 mid = from + dir * (length * 0.5f);
            SpriteRenderer mist = null;
            float unit = _glow != null ? 1f / Mathf.Max(0.01f, _glow.bounds.size.x) : 1f;
            if (_glow != null)
            {
                mist = FlatSprite(_glow, mid, 1f, Yaw(dir), 0.06f, 4, new Color(Teal.r, Teal.g, Teal.b, 0f));
                mist.transform.localScale = new Vector3(width * 1.3f * unit, length * 1.05f * unit, 1f);
            }
            var holder = new GameObject("Lane Wisps").transform;
            holder.SetParent(transform, false);
            holder.SetPositionAndRotation(mid, Quaternion.LookRotation(dir));
            var wisps = InkParticles.Frost(holder, Vector3.zero, 0.3f, Mathf.Clamp(length * 0.6f, 4f, 14f));
            InkParticles.EmitBox(wisps, Vector3.zero, new Vector3(width * 0.8f, 0.1f, length), 0.25f);
            var main = wisps.main;
            main.startColor = new Color(0.55f, 1f, 0.9f, 1f);
            var vel = wisps.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(dir.x * 3f, dir.x * 5f);
            vel.z = new ParticleSystem.MinMaxCurve(dir.z * 3f, dir.z * 5f);
            float t = 0f, fade = 1f;
            bool on = true;
            _live.Add(dt =>
            {
                t += dt;
                if (on && (t > 4f || !alive())) { on = false; if (wisps != null) InkParticles.SetRate(wisps, 0f); }
                if (!on) fade -= dt / 0.4f;
                if (mist != null) mist.color = new Color(Teal.r, Teal.g, Teal.b, (0.16f + 0.08f * Mathf.Sin(t * 5f)) * Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01(fade));
                if (fade > 0f || (wisps != null && wisps.particleCount > 0)) return true;
                if (mist != null) Destroy(mist.gameObject);
                if (holder != null) Destroy(holder.gameObject);
                return false;
            });
        }

        void QueueRift(Vector3 at, float radius, float time)
        {
            if (_riftFrame != Time.frameCount) { _rifts.Clear(); _riftFrame = Time.frameCount; }
            at.y = 0f;
            _rifts.Add((at, radius, time));
            if (_rifts.Count < 3) return;
            Vector3 center = Vector3.zero;
            foreach (var r in _rifts) center += r.at;
            center /= _rifts.Count;
            foreach (var r in _rifts)
            {
                Vector3 dir = Flat(center - r.at);
                float length = Vector3.Distance(center, r.at) * 2f;
                float t = 0f, life = r.time;
                FlowArrows(() => (t += Time.deltaTime) < life, r.at, dir, length, 1f, Violet, life + 0.1f);
            }
            _rifts.Clear();
        }

        // 띠 위로 갈매기(>) 무늬가 달리는 방향으로 흘러가요. 앞쪽 끝은 진하게, 뒤쪽은 옅게.
        void FlowArrows(Func<bool> alive, Vector3 from, Vector3 dir, float length, float width, Color color, float maxLife)
        {
            from.y = 0f;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            int count = Mathf.Clamp(Mathf.RoundToInt(length / 2.2f), 2, 14);
            float gap = length / count, half = Mathf.Clamp(width * 0.35f, 0.25f, 0.6f);
            var arrows = new List<LineRenderer>();
            for (int i = 0; i < count; i++)
            {
                var lr = Line(new Color(color.r, color.g, color.b, 0f), 0.16f, 3);
                lr.numCapVertices = 0;
                lr.numCornerVertices = 2;
                arrows.Add(lr);
            }
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                bool on = t < maxLife && alive();
                float fade = on ? Mathf.Clamp01(t / 0.15f) : 0f;
                float shift = (t * 4.5f) % gap; // 초당 4.5칸으로 흘러요
                for (int i = 0; i < arrows.Count; i++)
                {
                    var lr = arrows[i];
                    if (lr == null) continue;
                    float along = i * gap + shift;
                    Vector3 tip = from + dir * along + Vector3.up * 0.1f;
                    lr.SetPosition(0, tip - dir * half + side * half);
                    lr.SetPosition(1, tip);
                    lr.SetPosition(2, tip - dir * half - side * half);
                    float edge = Mathf.Clamp01(along / 0.6f) * Mathf.Clamp01((length - along) / 0.6f);
                    float a = (0.35f + 0.6f * (along / length)) * edge * fade;
                    lr.startColor = lr.endColor = new Color(color.r, color.g, color.b, a);
                }
                if (on) return true;
                foreach (var lr in arrows) if (lr != null) Destroy(lr.gameObject);
                return false;
            });
        }

        // 녹슨 톱니 원 (덫 장판 둘레): 바깥 톱니 지그재그 + 안쪽 쇠테
        LineRenderer SawRing(Vector3 at, float radius)
        {
            const int teeth = 18;
            var lr = Line(new Color(0.55f, 0.5f, 0.48f, 0.95f), 0.09f, teeth * 2);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.numCapVertices = 0;
            lr.transform.position = new Vector3(at.x, 0.05f, at.z);
            for (int i = 0; i < teeth * 2; i++)
            {
                float a = i * Mathf.PI / teeth;
                float r = i % 2 == 0 ? radius : radius * 0.82f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            var rim = Line(new Color(0.6f, 0.2f, 0.08f, 0.9f), 0.07f, 32);
            rim.useWorldSpace = false;
            rim.loop = true;
            rim.transform.SetParent(lr.transform, false);
            rim.transform.localRotation = Quaternion.identity;
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                rim.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius * 0.74f);
            }
            return lr;
        }

        // 게임 코드의 임시 그림(씬 루트 오브젝트)을 이름으로 찾아 렌더러만 꺼요 — 신호가 올 때 한 번만 훑어요
        static bool HideRoot(string name, Vector3 near, float within)
        {
            bool found = false;
            near.y = 0f;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Vector3 p = go.transform.position;
                p.y = 0f;
                if ((p - near).sqrMagnitude > within * within || go.name != name) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; found = true; }
            }
            return found;
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

        // VARCO 모델(채색 캐릭터 셰이더)은 _BaseColor가 그림 색에 곱해져요
        static void TintBase(Renderer r, Color color)
        {
            r.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(_block);
        }

        static GameObject _bambooModel;
        static bool _bambooLoaded;
        static readonly List<Renderer> _modelRenderers = new List<Renderer>();
        static GameObject BambooModel
        {
            get
            {
                if (!_bambooLoaded) { _bambooLoaded = true; _bambooModel = Resources.Load<GameObject>("Models/BurningBamboo"); }
                return _bambooModel;
            }
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
