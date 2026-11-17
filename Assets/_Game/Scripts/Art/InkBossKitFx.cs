using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 보스 공통기 · 판타지 고수(고블린 족장)·보스(오우거) 이펙트 (그림만 — 판정·수치는 게임 코드 그대로). 채색 화풍.
    //   도약 내려찍기 (EnemyBrain.BossLeapStarted → BossLeapLanded): 떠날 자리에 흙먼지가 모이고, 날아오르는 0.35초 동안 보스 색 빛줄기가
    //     떠날 자리에서 내려찍을 자리까지 포물선으로 그어지며 그림자가 바닥을 따라가요. 착지 폭발은 InkBurstFx(Slam).
    //     보스 색: 녹림왕 금빛 · 혈불 진홍 · 철룡 하늘빛 · 천마 보랏빛 진홍 · 오우거 흙빛 주황.
    //     고수(약한 판)도 같은 신호: 청죽객 초록 · 철권 마웅 바람빛 · 혜광 금빛 · 탁랑객 물빛 · 혈영검마 핏빛 · 고블린 족장 초록.
    //   족장 함성 (GoblinChiefBrain.WarCry): 붉은 음파 고리 세 겹이 퍼지고, 그 순간 새로 나온 고블린 발밑마다 녹갈색 연기가 피어올라요.
    //   오우거 바위 던지기 (OgreBrain.RockThrown): 그 순간 새로 뜬 바위 예고 원("Telegraph")마다 바위가 오우거 손에서 포물선으로 날아가 떨어져요
    //     (0.8초). 떨어지는 흙먼지·파편은 InkBurstFx(Quake).
    //   돌진 (AttackName이 돌진·분노 돌진이고 빠르게 움직이는 동안): 흙먼지 꼬리 (오우거는 붉은 김도 같이).
    //   2페이즈 (IMasterBrain.Phase 2): 오우거·족장 몸에서 붉은 분노 기운이 피어올라요. 전환하는 순간(IsTransitioning) 붉은 기운이 확 터져요.
    //   데스나이트 (DeathKnightBrain): 망령마 돌격 = 청록 유령불 말발굽 자국 · 저주 검기(CurseWave) = 둘레 청록 유령불 (검기는 InkProjectiles 초승달)
    //     죽은 자의 부름(Raise) = 청록 룬 원 + 새 스켈레톤마다 무덤 흙 · 유령불 · 저주 내려찍기(가까운 Quake) = 청록 룬 금 · 분노 기운과 전환도 청록.
    // 그림: TempleFx/Rock, PouchFx/Ring.
    public class InkBossKitFx : MonoBehaviour
    {
        static InkBossKitFx _host;
        static Sprite _rock, _ring;
        static Material _lineMat;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        readonly HashSet<EnemyBrain> _known = new HashSet<EnemyBrain>();
        readonly Dictionary<EnemyBrain, (Vector3 last, float dust, float aura, bool transitioning)> _bosses = new Dictionary<EnemyBrain, (Vector3, float, float, bool)>();
        readonly List<EnemyBrain> _keys = new List<EnemyBrain>();
        EnemyDirector _director;
        OgreBrain _thrower;
        float _throwWindow, _cryWindow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.BossLeapStarted -= OnLeap;
            EnemyBrain.BossLeapStarted += OnLeap;
            GoblinChiefBrain.WarCry -= OnWarCry;
            GoblinChiefBrain.WarCry += OnWarCry;
            OgreBrain.RockThrown -= OnRockThrown;
            OgreBrain.RockThrown += OnRockThrown;
            DeathKnightBrain.CurseWave -= OnCurseWave;
            DeathKnightBrain.CurseWave += OnCurseWave;
            DeathKnightBrain.Raise -= OnRaise;
            DeathKnightBrain.Raise += OnRaise;
            EnemyDirector.Burst -= OnBurst;
            EnemyDirector.Burst += OnBurst;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkBossKitFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Boss Kit Fx").AddComponent<InkBossKitFx>();
            return _host;
        }

        static void OnLeap(EnemyBrain boss, Vector3 from, Vector3 to) { if (boss != null) Host().Leap(boss, from, to); }
        static void OnWarCry(GoblinChiefBrain chief) { if (chief != null) Host().WarCry(chief); }
        static void OnRockThrown(OgreBrain ogre) { if (ogre != null) { var h = Host(); h._thrower = ogre; h._throwWindow = 0.25f; } }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            WatchRocks(dt);
            WatchSummons(dt);
            TickBosses(dt);
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        static Color BossColor(EnemyKind kind) => kind switch
        {
            EnemyKind.Nokrim => new Color(1f, 0.8f, 0.3f),
            EnemyKind.BloodBuddha => new Color(0.95f, 0.15f, 0.15f),
            EnemyKind.IronDragon => new Color(0.4f, 0.75f, 1f),
            EnemyKind.HeavenlyDemon => new Color(0.75f, 0.2f, 0.6f),
            EnemyKind.Ogre => new Color(0.95f, 0.55f, 0.25f),
            EnemyKind.Qingzhu => new Color(0.45f, 0.95f, 0.35f),      // 고수: 청죽객 대나무 초록
            EnemyKind.IronFist => new Color(0.6f, 0.9f, 1f),          // 철권 마웅 바람빛
            EnemyKind.Hyegwang => new Color(1f, 0.82f, 0.25f),        // 혜광 금빛
            EnemyKind.Takrang => new Color(0.3f, 0.65f, 1f),          // 탁랑객 물빛
            EnemyKind.BloodShadow => new Color(0.9f, 0.1f, 0.2f),     // 혈영검마 핏빛
            EnemyKind.GoblinChief => new Color(0.55f, 0.85f, 0.3f),   // 고블린 족장 초록
            EnemyKind.DarkElf => new Color(0.7f, 0.35f, 1f),          // 다크 엘프 보라
            EnemyKind.DeathKnight => new Color(0.3f, 1f, 0.85f),      // 데스나이트 청록
            _ => new Color(1f, 0.6f, 0.3f),
        };

        // ── 도약 내려찍기: 떠날 자리 흙먼지 → 포물선 빛줄기 + 그림자

        void Leap(EnemyBrain boss, Vector3 from, Vector3 to)
        {
            var color = BossColor(boss.Kind);
            const float windup = 0.9f, flight = 0.35f, arc = 4f;
            from.y = 0f; to.y = 0f;
            var lr = Line(color, 0.5f, 16);
            lr.enabled = false;
            var shadow = Flat(_ring, from, 1.6f, 0.026f);
            float t = 0f;
            bool launched = false;
            _live.Add(dt =>
            {
                t += dt;
                if (t < windup) // 웅크려요: 발밑에 흙먼지가 모여요
                {
                    if (Mathf.Repeat(t, 0.15f) < dt) InkParticles.Burst(from + Vector3.up * 0.2f, 4, new Color(0.85f, 0.75f, 0.6f, 0.7f), new Color(color.r, color.g, color.b, 0.3f), 1.2f, 0.4f, 0.4f, -0.2f);
                    return true;
                }
                if (!launched)
                {
                    launched = true;
                    InkParticles.Burst(from + Vector3.up * 0.3f, 14, new Color(1f, 0.95f, 0.8f, 0.9f), new Color(color.r, color.g, color.b, 0.4f), 3f, 0.4f, 0.6f, -0.3f);
                    lr.enabled = true;
                }
                float k = Mathf.Clamp01((t - windup) / flight);
                int n = lr.positionCount;
                for (int i = 0; i < n; i++) // 지나간 데까지만 그어져요
                {
                    float s = k * i / (n - 1f);
                    lr.SetPosition(i, Vector3.Lerp(from, to, s) + Vector3.up * (0.8f + arc * 4f * s * (1f - s)));
                }
                if (shadow != null)
                {
                    shadow.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * 0.026f;
                    shadow.color = new Color(0f, 0f, 0f, 0.35f * Mathf.Sin(k * Mathf.PI) + 0.1f);
                }
                if (k < 1f) return true;
                // 착지: 빛줄기가 바래요
                float fade = Mathf.Clamp01(1f - (t - windup - flight) / 0.35f);
                lr.widthMultiplier = 0.5f * fade;
                if (shadow != null) Destroy(shadow.gameObject);
                shadow = null;
                if (fade > 0f) return true;
                Destroy(lr.gameObject);
                return false;
            });
        }

        // ── 족장 함성: 붉은 음파 고리 + 불려 나온 고블린 연기

        void WarCry(GoblinChiefBrain chief)
        {
            Vector3 at = chief.transform.position;
            for (int i = 0; i < 3; i++) Ring(_ring, at, 6f, 0.7f, new Color(1f, 0.2f, 0.12f, 0.9f), 0.03f, i * 0.15f);
            _known.Clear();
            if (_director != null && _director.Enemies != null) foreach (var e in _director.Enemies) if (e != null) _known.Add(e);
            _cryWindow = 0.3f;
            _raising = false;
        }

        void WatchSummons(float dt)
        {
            if (_cryWindow <= 0f) return;
            _cryWindow -= dt;
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
                if (e != null && _known.Add(e)) // 함성 · 부름 뒤 새로 나온 적
                {
                    var p = e.transform.position;
                    if (_raising) // 데스나이트 부름: 무덤 흙이 튀고 청록 유령불이 일어나요
                    {
                        InkParticles.Burst(p + Vector3.up * 0.2f, 14, new Color(0.55f, 0.45f, 0.35f, 1f), new Color(0.35f, 0.28f, 0.22f, 0.5f), 2.2f, 0.12f, 0.6f, 1.6f);
                        InkParticles.Burst(p + Vector3.up * 0.4f, 12, new Color(0.55f, 1f, 0.9f, 0.9f), new Color(0.1f, 0.5f, 0.45f, 0f), 0.8f, 0.45f, 1f, -1.4f);
                        Ring(_ring, p, 1.6f, 0.4f, new Color(0.3f, 1f, 0.85f, 0.9f), 0.028f, 0f);
                        continue;
                    }
                    InkParticles.Burst(p + Vector3.up * 0.4f, 16, new Color(0.6f, 0.7f, 0.35f, 0.9f), new Color(0.45f, 0.35f, 0.25f, 0.4f), 1.8f, 0.6f, 0.9f, -0.4f);
                    Ring(_ring, p, 1.6f, 0.35f, new Color(0.55f, 0.85f, 0.3f, 0.8f), 0.028f, 0f);
                }
        }

        // ── 데스나이트

        static Sprite _hoof, _runeTeal;
        bool _raising;

        static void OnCurseWave(DeathKnightBrain k, Vector3 dir) // 저주 검기를 쏘는 순간: 둘레에 청록 유령불 (검기 셋은 InkProjectiles 초승달)
        {
            if (k == null) return;
            Vector3 p = k.transform.position;
            Host();
            InkParticles.Burst(p + Vector3.up * 1f + dir.normalized * 0.6f, 18, new Color(0.6f, 1f, 0.95f, 1f), new Color(0.1f, 0.55f, 0.5f, 0.4f), 2.4f, 0.35f, 0.6f, -0.6f);
        }

        static void OnRaise(DeathKnightBrain k) // 죽은 자의 부름: 청록 룬 원이 펼쳐지고, 0.3초 안에 나온 스켈레톤마다 무덤 흙 · 유령불
        {
            if (k == null) return;
            var h = Host();
            h.Rune(k.transform.position, 7f, 1.6f);
            h._known.Clear();
            if (h._director != null && h._director.Enemies != null) foreach (var e in h._director.Enemies) if (e != null) h._known.Add(e);
            h._cryWindow = 0.3f;
            h._raising = true;
        }

        // 저주 내려찍기 (BurstKind.Quake, 데스나이트 가까이): 기존 진각 연출 위에 청록 유령불 금이 번져요
        static void OnBurst(Vector3 at, float radius, AThousandDawns.Combat.BurstKind kind)
        {
            if (kind != AThousandDawns.Combat.BurstKind.Quake || _host == null || _host._director == null || _host._director.Enemies == null) return;
            foreach (var e in _host._director.Enemies)
                if (e is DeathKnightBrain k && !k.IsDead && (k.transform.position - at).sqrMagnitude < (radius + 2f) * (radius + 2f))
                {
                    _host.Rune(at, radius * 2.2f, 0.7f);
                    InkParticles.Burst(at + Vector3.up * 0.3f, 20, new Color(0.55f, 1f, 0.9f, 1f), new Color(0.1f, 0.45f, 0.4f, 0f), radius * 1.6f, 0.4f, 0.8f, -1f);
                    return;
                }
        }

        void Rune(Vector3 at, float size, float life)
        {
            var sr = Flat(_runeTeal, at, size, 0.03f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(Mathf.Clamp01(k * 3f)));
                sr.transform.Rotate(0f, 0f, 40f * dt, Space.Self);
                sr.color = new Color(1f, 1f, 1f, k < 0.6f ? 0.9f : 0.9f * (1f - (k - 0.6f) / 0.4f));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 망령마 돌격: 청록 유령불 말발굽 자국 (번갈아 좌우) + 유령불 연기
        bool _hoofSide;
        float _lastHoof;
        void Hoof(Vector3 at, Vector3 move)
        {
            if (Time.time - _lastHoof < 0.16f) return; // 말발굽은 듬성듬성
            _lastHoof = Time.time;
            move.y = 0f;
            if (move.sqrMagnitude < 1e-5f) return;
            Vector3 dir = move.normalized, side = new Vector3(dir.z, 0f, -dir.x) * (_hoofSide ? 0.35f : -0.35f);
            _hoofSide = !_hoofSide;
            float yaw = -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + 90f;
            var sr = Flat(_hoof, at + side, 0.7f, 0.024f);
            if (sr == null) return;
            sr.transform.rotation = Quaternion.Euler(90f, yaw + 180f, 0f); // 발굽 열린 쪽이 뒤로
            InkParticles.Burst(at + Vector3.up * 0.3f, 2, new Color(0.5f, 1f, 0.9f, 0.7f), new Color(0.15f, 0.4f, 0.38f, 0f), 0.5f, 0.5f, 0.7f, -0.8f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.color = new Color(1f, 1f, 1f, t < 0.1f ? t / 0.1f : Mathf.Clamp01(1f - (t - 0.8f) / 0.6f));
                if (t < 1.4f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── 오우거 바위: 던진 순간 새로 뜬 예고 원마다 바위가 날아가 떨어져요

        void WatchRocks(float dt)
        {
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 봐요
            {
                if (_throwWindow <= 0f || _thrower == null || root.name != "Telegraph") continue;
                Throw(_thrower.transform.position + Vector3.up * 2.4f, root.transform.position);
            }
            if (_throwWindow > 0f) _throwWindow -= dt;
        }

        void Throw(Vector3 from, Vector3 land)
        {
            land.y = 0f;
            var sr = Billboard(_rock, from, 0.9f, 8);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            const float fall = 0.8f;
            float t = 0f, peak = 3f + Vector3.Distance(from, land) * 0.25f;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / fall);
                pivot.position = Vector3.Lerp(from, land + Vector3.up * 0.4f, k) + Vector3.up * peak * 4f * k * (1f - k);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, -t * 300f);
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 돌진 흙먼지 · 2페이즈 분노 기운 (오우거 · 족장)

        void TickBosses(float dt)
        {
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                    if ((e is OgreBrain || e is GoblinChiefBrain || e is DeathKnightBrain) && !e.IsDead && !_bosses.ContainsKey(e))
                        _bosses[e] = (e.transform.position, 0f, 0f, false);
            _keys.Clear();
            _keys.AddRange(_bosses.Keys);
            foreach (var e in _keys)
            {
                if (e == null || e.IsDead) { _bosses.Remove(e); continue; }
                var s = _bosses[e];
                Vector3 p = e.transform.position;
                float speed = dt > 0f ? (p - s.last).magnitude / dt : 0f;
                bool ogre = e is OgreBrain;
                bool knight = e is DeathKnightBrain; // 데스나이트: 청록 유령불
                string attack = e.AttackName;
                bool charging = speed > 4f && (attack == "돌진" || attack == "분노 돌진" || attack == "망령마 돌격");
                s.dust -= dt;
                if (charging && s.dust <= 0f)
                {
                    s.dust = 0.05f;
                    Vector3 back = p - (p - s.last).normalized * 0.8f;
                    InkParticles.Burst(back + Vector3.up * 0.2f, 3, new Color(0.8f, 0.7f, 0.5f, 0.8f), new Color(0.55f, 0.4f, 0.28f, 0.35f), 1f, 0.6f, 0.6f, -0.3f);
                    if (ogre) InkParticles.Burst(p + Vector3.up * 2f, 2, new Color(1f, 0.45f, 0.35f, 0.6f), new Color(1f, 0.6f, 0.5f, 0f), 0.6f, 0.5f, 0.6f, -0.6f); // 붉은 김
                    if (knight) Hoof(back, p - s.last);
                }
                var info = e as IMasterBrain;
                if (info != null)
                {
                    if (info.IsTransitioning && !s.transitioning) // 분노가 터져요
                    {
                        InkParticles.Burst(p + Vector3.up * 1.2f, 30, knight ? new Color(0.5f, 1f, 0.9f, 1f) : new Color(1f, 0.35f, 0.2f, 1f), knight ? new Color(0.1f, 0.5f, 0.45f, 0.5f) : new Color(0.7f, 0.05f, 0.05f, 0.5f), 4f, 0.5f, 0.8f, -0.5f);
                        Ring(_ring, p, 5f, 0.6f, knight ? new Color(0.3f, 1f, 0.85f, 1f) : new Color(1f, 0.15f, 0.1f, 1f), 0.03f, 0f);
                    }
                    s.transitioning = info.IsTransitioning;
                    s.aura -= dt;
                    if (info.Phase >= 2 && s.aura <= 0f) // 2페이즈: 붉은 분노 기운이 몸에서 피어올라요
                    {
                        s.aura = 0.12f;
                        Vector2 r = UnityEngine.Random.insideUnitCircle * (ogre ? 0.9f : 0.6f);
                        InkParticles.Burst(p + new Vector3(r.x, 0.6f, r.y), 2, knight ? new Color(0.35f, 1f, 0.85f, 0.7f) : new Color(1f, 0.25f, 0.15f, 0.7f), knight ? new Color(0.1f, 0.4f, 0.35f, 0f) : new Color(0.6f, 0.05f, 0.05f, 0f), 0.4f, ogre ? 0.7f : 0.5f, 0.9f, -1.2f);
                    }
                }
                s.last = p;
                _bosses[e] = s;
            }
        }

        // ── 도움

        void Ring(Sprite sprite, Vector3 at, float size, float life, Color color, float height, float delay)
        {
            var sr = Flat(sprite, at, size, height);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        LineRenderer Line(Color color, float width, int points)
        {
            var go = new GameObject("Leap Arc");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 1f));
            lr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.1f, 0f), new GradientAlphaKey(0.9f, 1f) },
            };
            lr.numCapVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        SpriteRenderer Billboard(Sprite sprite, Vector3 at, float width, int order)
        {
            if (sprite == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = at;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float height)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _hoof = Resources.Load<Sprite>("DeathKnightFx/Hoof");
            _runeTeal = Resources.Load<Sprite>("DeathKnightFx/RuneTeal");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
