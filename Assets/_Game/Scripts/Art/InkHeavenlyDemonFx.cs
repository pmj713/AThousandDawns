using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 천마 이펙트 (천마총 최종 보스) — 그림만, 판정·수치는 게임 코드 그대로.
    //   천마일검 · 천마군림 (Slash): 검은 마기 초승달이 부채꼴로 휩쓸어요 (220°면 두 겹).
    //   십자 검기 (Cross, 이름 "천마 검기" 화살): 회색 상자는 숨기고 검은 초승달 + 붉은 테 검기가 날아가요.
    //   마기 장판 (GhostFire "마기 장판"): 푸른 도깨비불 대신 검붉은 마기 웅덩이 + 일렁이는 마기 연기.
    //   마기 손아귀 (Grasp → Grasped): 마기 팔이 직선으로 뻗어 손이 열렸다가, 잡으면 플레이어를 쥐고 끌고 와요.
    //   환영 (Mark, Sector): 플레이어 양옆에 검은 그림자 검객이 짙어지다가 함께 베고 흩어져요.
    //   흡인 (IsPulling): 둘레 6칸에서 검붉은 먹선이 소용돌이치며 천마에게 빨려 들고, 끝에 마기가 터져요 (PullBurst).
    //   해체대법 (SafeRadius > 0): 안전 반경 밖이 검붉은 마기 안개로 가득 차고, 경계에서 마기 연기가 피어올라요. 반경이 줄면 같이 좁혀 들어와요.
    //   검비 (Mark, 원): 하늘에서 검이 떨어져 꽂히고 (MarkFall) 핏물·금이 번져요.
    //   페이즈 전환 (Transition): 마기가 터지고 전환 동안 천마 둘레에서 마기 연기가 솟아요.
    //   천마전 보호막 (RoomRun.HeroShieldCue): 둘레에서 금빛 내력 줄기가 소용돌이치며 플레이어에게 흘러들고 흰 고리가 퍼져요.
    // 그림: Art/Env/BossFx/Resources/HeavenlyDemonFx (Sword · Hand · Wisp · Crescent · Phantom).
    //       없으면 BloodBuddhaFx · TombFx 그림을 검게 물들여 대신해요.
    public class InkHeavenlyDemonFx : MonoBehaviour
    {
        static InkHeavenlyDemonFx _host;
        static Sprite _sword, _hand, _wisp, _crescent, _phantom, _splash, _ring, _drop;
        static bool _ownWisp, _ownCrescent, _ownPhantom, _ownHand;
        static Material _lineMat;
        static Sprite _miasmaA, _miasmaB;
        static bool _loaded;

        static readonly Color Dark = new Color(0.16f, 0.05f, 0.07f);      // 마기 (검붉은 먹)
        static readonly Color Blood = new Color(0.62f, 0.06f, 0.07f);
        static readonly Color Qi = new Color(1f, 0.88f, 0.55f);           // 내력 (금빛)

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        const int MaxLive = 400; // 동시에 움직이는 그림 상한
        readonly HashSet<GhostFire> _dressed = new HashSet<GhostFire>();
        HeavenlyDemonBrain _boss;
        float _scan, _bossScan;
        bool _pulling;
        float _pullSpawn;
        SpriteRenderer _fogA, _fogB;
        float _fogRadius, _fogAlpha, _wispSpawn;
        Transform _graspHand;
        Transform _graspTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            HeavenlyDemonBrain.Cue -= OnBoss;
            HeavenlyDemonBrain.Cue += OnBoss;
            RoomRun.HeroShieldCue -= OnShield;
            RoomRun.HeroShieldCue += OnShield;
            Host(); // 마기 장판·해체대법은 신호 없이 찾아요
        }

        static InkHeavenlyDemonFx Host()
        {
            Load();
            if (_host == null)
            {
                _host = new GameObject("Ink Heavenly Demon Fx").AddComponent<InkHeavenlyDemonFx>();
                DontDestroyOnLoad(_host.gameObject); // 마기 장판·해체대법은 신호 없이 찾으니 씬이 바뀌어도 남아요
            }
            return _host;
        }

        static void OnBoss(HeavenlyDemonBrain b, HeavenlyDemonBrain.FxCue c)
        {
            var h = Host();
            if (b != null) h._boss = b;
            switch (c.Kind)
            {
                case HeavenlyDemonBrain.FxKind.Slash: h.Slash(c.At, Flat(c.Dir), c.Radius, c.Angle); break;
                case HeavenlyDemonBrain.FxKind.Cross: h.Wave(c.Target, Flat(c.Dir)); break;
                case HeavenlyDemonBrain.FxKind.Grasp: h.Grasp(b != null ? b.transform : null, c.At, Flat(c.Dir), c.Radius); break;
                case HeavenlyDemonBrain.FxKind.Grasped: h._graspTarget = c.Target; break;
                case HeavenlyDemonBrain.FxKind.Mark:
                    if (c.Sector) h.Phantom(c.At, Flat(c.Dir), c.Time, c.Radius);
                    else h.FallingSword(c.At, c.Time);
                    break;
                case HeavenlyDemonBrain.FxKind.MarkFall:
                    if (!c.Sector) h.SwordImpact(c.At, c.Radius);
                    break;
                case HeavenlyDemonBrain.FxKind.PullBurst: h.Burst(c.At, c.Radius + 1f, 8); break;
                case HeavenlyDemonBrain.FxKind.Transition: h.Transition(b != null ? b.transform : null, c.At, c.Radius, c.Time); break;
            }
        }

        static void OnShield(PlayerController p)
        {
            if (p != null) Host().Inflow(p.transform);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f && _boss != null) { _scan = 0.1f; DressMiasmaPools(); } // 천마가 있을 때만 (십자 검기는 Cross 신호로 받아요)
            _bossScan -= dt;
            if (_boss == null && _bossScan <= 0f)
            {
                _bossScan = 1f;
                _boss = FindAnyObjectByType<HeavenlyDemonBrain>();
            }
            TickPull(dt);
            TickFog(dt);
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 천마일검 · 천마군림: 검은 초승달이 휩쓸어요
        void Slash(Vector3 at, Vector3 dir, float radius, float angle)
        {
            Crescent(at + dir * radius * 0.45f, dir, radius * 2.1f, 0.3f, 0f);
            if (angle > 180f) // 천마군림: 뒤로 한 겹 더 (옆까지 휩쓸어요)
            {
                Crescent(at + dir * radius * 0.2f, Quaternion.Euler(0f, 70f, 0f) * dir, radius * 1.8f, 0.3f, 0.05f);
                Crescent(at + dir * radius * 0.2f, Quaternion.Euler(0f, -70f, 0f) * dir, radius * 1.8f, 0.3f, 0.05f);
            }
        }

        void Crescent(Vector3 at, Vector3 dir, float width, float life, float delay)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Flat(_crescent, at, width, CrescentYaw(dir), 0.9f, 7); // 활 등이 앞으로
            if (sr == null) return;
            if (!_ownCrescent) sr.color = Dark;
            Color baseColor = sr.color;
            Vector3 unit = sr.transform.localScale;
            sr.enabled = delay <= 0f;
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                sr.enabled = true;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.65f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - k * k);
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── 십자 검기: "천마 검기" 화살에 검은 초승달을 입혀요
        readonly HashSet<Transform> _waves = new HashSet<Transform>();

        void Wave(Transform wave, Vector3 dir)
        {
            if (wave == null || _waves.Contains(wave)) return;
            _waves.Add(wave);
            foreach (var r in wave.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var sr = Flat(_crescent, wave.position, 1.8f, CrescentYaw(dir), 1f, 7);
            if (sr == null) return;
            if (!_ownCrescent) sr.color = Dark;
            var rim = Flat(_crescent, wave.position, 2.1f, CrescentYaw(dir), 0.98f, 6); // 붉은 테
            if (rim != null) rim.color = new Color(Blood.r, Blood.g, Blood.b, 0.8f);
            var trail = Line(wave.position, wave.position, Dark, 0.5f, 5);
            Vector3 start = wave.position;
            Vector3 last = wave.position;
            float fade = 0f;
            _live.Add(dt =>
            {
                if (wave != null) last = wave.position;
                else fade += dt / 0.2f;
                Vector3 p = new Vector3(last.x, 1f, last.z);
                if (sr != null) sr.transform.position = p;
                if (rim != null) rim.transform.position = p - Vector3.up * 0.02f;
                if (trail != null)
                {
                    Vector3 tail = Vector3.MoveTowards(new Vector3(start.x, 1f, start.z), p, Mathf.Max(0f, (p - start).magnitude - 3f));
                    trail.SetPosition(0, tail);
                    trail.SetPosition(1, p);
                }
                float a = 1f - Mathf.Clamp01(fade);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, a);
                if (rim != null) rim.color = new Color(Blood.r, Blood.g, Blood.b, 0.8f * a);
                if (trail != null) trail.startColor = trail.endColor = new Color(Dark.r, Dark.g, Dark.b, 0.7f * a);
                if (fade < 1f) return true;
                if (sr != null) Destroy(sr.gameObject);
                if (rim != null) Destroy(rim.gameObject);
                if (trail != null) Destroy(trail.gameObject);
                return false;
            });
        }

        // ── 마기 장판: 검붉은 웅덩이 + 마기 연기
        void DressMiasmaPools()
        {
            _dressed.RemoveWhere(f => f == null);
            foreach (var fire in FindObjectsByType<GhostFire>(FindObjectsSortMode.None))
            {
                if (!_dressed.Add(fire) || fire.name != "마기 장판") continue; // 이름은 처음 볼 때 한 번만
                var source = fire.GetComponent<Renderer>();
                if (source != null) source.enabled = false;
                float radius = fire.transform.localScale.x * 0.5f;
                var pool = Flat(_splash, fire.transform.position, radius * 2.4f, UnityEngine.Random.Range(0f, 360f), 0.022f, 4);
                var ring = Flat(_ring, fire.transform.position, radius * 2.2f, UnityEngine.Random.Range(0f, 360f), 0.024f, 4);
                var wisps = new List<(SpriteRenderer sr, Vector3 at, float seed)>();
                for (int i = 0; i < 4; i++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 0.7f;
                    var at = fire.transform.position + new Vector3(r.x, 0.05f, r.y);
                    var sr = Upright(_wisp, at, UnityEngine.Random.Range(0.5f, 0.8f), 6, bottom: true);
                    if (sr != null) wisps.Add((sr, at, UnityEngine.Random.Range(0f, 10f)));
                }
                Vector3 poolUnit = pool != null ? pool.transform.localScale : Vector3.one;
                _live.Add(dt =>
                {
                    if (fire == null)
                    {
                        if (pool != null) Destroy(pool.gameObject);
                        if (ring != null) Destroy(ring.gameObject);
                        foreach (var w in wisps) if (w.sr != null) Destroy(w.sr.transform.parent.gameObject);
                        return false;
                    }
                    float alpha = source != null ? Mathf.Clamp01(source.sharedMaterial.color.a / 0.55f) : 1f;
                    float tt = Time.time;
                    if (pool != null)
                    {
                        pool.transform.Rotate(0f, 0f, 18f * dt, Space.Self);
                        pool.transform.localScale = poolUnit * (1f + 0.06f * Mathf.Sin(tt * 4f));
                        pool.color = new Color(0.28f, 0.03f, 0.06f, 0.9f * alpha);
                    }
                    if (ring != null)
                    {
                        ring.transform.Rotate(0f, 0f, -40f * dt, Space.Self);
                        ring.color = new Color(Blood.r, Blood.g, Blood.b, 0.45f * alpha);
                    }
                    foreach (var w in wisps)
                    {
                        if (w.sr == null) continue;
                        float flick = 1f + 0.15f * Mathf.Sin(tt * 9f + w.seed) + 0.08f * Mathf.Sin(tt * 19f + w.seed * 2f);
                        w.sr.transform.parent.position = w.at + Vector3.up * 0.06f * Mathf.Sin(tt * 3f + w.seed);
                        w.sr.transform.localScale = new Vector3(1f / flick, flick, 1f) * w.sr.transform.localScale.z;
                        w.sr.color = WispColor(alpha * 0.9f);
                    }
                    return true;
                });
            }
        }

        static Color WispColor(float a) => _ownWisp ? new Color(1f, 1f, 1f, a) : new Color(0.3f, 0.06f, 0.1f, a);

        // ── 마기 손아귀: 마기 팔이 뻗어 손이 열리고, 잡으면 쥐고 끌고 와요
        void Grasp(Transform boss, Vector3 from, Vector3 dir, float range)
        {
            _graspTarget = null;
            var hand = Flat(_hand, from, 2.4f, Yaw(dir), 1f, 8);
            if (hand == null) return;
            if (!_ownHand) hand.color = new Color(0.42f, 0.08f, 0.1f); // 혈장 그림을 검붉게
            _graspHand = hand.transform;
            var armInk = Line(from, from, Dark, 0.9f, 6);
            var armRed = Line(from, from, Blood, 0.35f, 7);
            Vector3 unit = hand.transform.localScale;
            float t = 0f, reach = 0f;
            const float outTime = 0.15f, hold = 0.12f, back = 0.3f;
            _live.Add(dt =>
            {
                if (hand == null) return false;
                t += dt;
                Vector3 root = (boss != null ? boss.position : from);
                root.y = 1f;
                Vector3 tip;
                if (_graspTarget != null && t > outTime * 0.5f) // 잡았어요: 쥔 손이 플레이어를 따라 와요
                {
                    tip = _graspTarget.position;
                    tip.y = 1f;
                    hand.transform.localScale = unit * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01((t - outTime) / 0.1f)); // 움켜쥐어요
                    if (t > 0.9f || (tip - root).magnitude < 2.2f) t = Mathf.Max(t, outTime + hold + back * 0.5f);
                }
                else
                {
                    if (t < outTime) reach = range * Mathf.Sqrt(t / outTime);
                    else if (t < outTime + hold) reach = range;
                    else reach = range * (1f - Mathf.Clamp01((t - outTime - hold) / back));
                    tip = root + dir * reach;
                }
                hand.transform.position = tip;
                SetArm(armInk, root, tip - dir * 0.6f);
                SetArm(armRed, root, tip - dir * 0.6f);
                float end = outTime + hold + back;
                float a = t > end ? 1f - (t - end) / 0.15f : 1f;
                hand.color = new Color(hand.color.r, hand.color.g, hand.color.b, a);
                if (armInk != null) armInk.startColor = armInk.endColor = new Color(Dark.r, Dark.g, Dark.b, 0.9f * a);
                if (armRed != null) armRed.startColor = armRed.endColor = new Color(Blood.r, Blood.g, Blood.b, 0.8f * a);
                if (a > 0f) return true;
                Destroy(hand.gameObject);
                if (armInk != null) Destroy(armInk.gameObject);
                if (armRed != null) Destroy(armRed.gameObject);
                if (_graspHand == hand.transform) _graspTarget = null;
                return false;
            });
        }

        static void SetArm(LineRenderer lr, Vector3 a, Vector3 b)
        {
            if (lr == null) return;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        // ── 천마군림 환영: 그림자 검객이 짙어지다가 베고 흩어져요
        void Phantom(Vector3 at, Vector3 dir, float delay, float radius)
        {
            var sr = Upright(_phantom, at, 1.4f, 6, bottom: true);
            if (sr == null) return;
            float t = 0f, life = Mathf.Max(0.1f, delay);
            Vector3 unit = sr.transform.localScale;
            sr.flipX = Vector3.Dot(dir, Camera.main != null ? Camera.main.transform.right : Vector3.right) < 0f;
            Mist(at, 1.4f);
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < life)
                {
                    float k = t / life;
                    sr.color = PhantomColor(Mathf.Lerp(0.15f, 0.95f, k));
                    sr.transform.localScale = unit * (1f + 0.03f * Mathf.Sin(t * 30f));
                    return true;
                }
                if (sr.enabled && t - dt < life) Crescent(at + dir * radius * 0.45f, dir, radius * 1.9f, 0.28f, 0f); // 같이 벱니다
                float a = (t - life) / 0.35f;
                sr.color = PhantomColor(0.95f * (1f - a));
                sr.transform.localScale = unit * (1f + 0.35f * a);
                if (a < 1f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        static Color PhantomColor(float a) => _ownPhantom ? new Color(1f, 1f, 1f, a) : new Color(0.22f, 0.12f, 0.16f, a);

        // ── 검비: 하늘에서 검이 떨어져 꽂혀요
        void FallingSword(Vector3 at, float fall)
        {
            var sr = Upright(_sword, at, 0.55f, 8, bottom: true);
            if (sr == null) return;
            if (_sword == _crescent) sr.color = Dark;
            var pivot = sr.transform.parent;
            float t = 0f, life = Mathf.Max(0.1f, fall);
            const float high = 9f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                if (k < 1f) // 높이 떠 있다가 마지막에 확 떨어져요
                {
                    float drop = k < 0.6f ? 0f : Mathf.Pow((k - 0.6f) / 0.4f, 2f);
                    pivot.position = at + Vector3.up * Mathf.Lerp(high, -0.25f, drop);
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Clamp01(k * 3f));
                    return true;
                }
                pivot.position = at + Vector3.down * 0.25f; // 꽂혀 있다가 흐려져요
                float a = 1f - Mathf.Clamp01((t - life - 0.5f) / 0.4f);
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, a);
                if (a > 0f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        void SwordImpact(Vector3 at, float radius)
        {
            Mist(at, radius * 2f);
            var ring = Flat(_ring, at, radius * 1.4f, UnityEngine.Random.Range(0f, 360f), 0.06f, 5);
            if (ring == null) return;
            Vector3 unit = ring.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (ring == null) return false;
                t += dt;
                float k = t / 0.35f;
                ring.transform.localScale = unit * Mathf.Lerp(0.5f, 1.3f, Mathf.Sqrt(Mathf.Clamp01(k)));
                ring.color = new Color(Dark.r, Dark.g, Dark.b, 0.9f * (1f - k));
                if (k < 1f) return true;
                Destroy(ring.gameObject);
                return false;
            });
        }

        // ── 흡인: 둘레에서 먹선이 소용돌이치며 빨려 들어요
        void TickPull(float dt)
        {
            bool pulling = _boss != null && _boss.IsPulling;
            if (pulling && !_pulling && _boss != null) Swirl(_boss.transform);
            _pulling = pulling;
            if (!pulling) return;
            _pullSpawn -= dt;
            if (_pullSpawn > 0f) return;
            _pullSpawn = 0.05f;
            SpawnPullStreak(); // 람다가 잡는 변수는 따로 (여기 두면 끌어당기지 않는 프레임에도 매번 할당돼요)
        }

        void SpawnPullStreak()
        {
            Vector3 c = _boss.transform.position;
            float ang = UnityEngine.Random.Range(0f, 360f);
            bool red = UnityEngine.Random.value < 0.35f;
            var lr = Line(c, c, red ? Blood : Dark, red ? 0.18f : 0.3f, 6);
            if (lr == null) return;
            var boss = _boss.transform;
            float t = 0f;
            const float life = 0.5f, radius = 6f;
            _live.Add(d =>
            {
                if (lr == null) return false;
                t += d;
                Vector3 center = boss != null ? boss.position : c;
                center.y = 0.6f;
                float k = t / life;
                Vector3 Point(float kk) // 나선: 멀리서 돌며 들어와요
                {
                    float a = (ang + kk * 140f) * Mathf.Deg2Rad, r = radius * (1f - kk);
                    return center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                }
                lr.SetPosition(0, Point(Mathf.Clamp01(k - 0.25f)));
                lr.SetPosition(1, Point(Mathf.Clamp01(k)));
                var col = lr.startColor;
                lr.startColor = lr.endColor = new Color(col.r, col.g, col.b, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.9f);
                if (k < 1f) return true;
                Destroy(lr.gameObject);
                return false;
            });
        }

        void Swirl(Transform boss)
        {
            var sr = Flat(_ring, boss.position, 4f, 0f, 0.05f, 4);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                bool on = _boss != null && _boss.IsPulling;
                if (boss != null) sr.transform.position = new Vector3(boss.position.x, 0.05f, boss.position.z);
                sr.transform.Rotate(0f, 0f, 220f * dt, Space.Self);
                sr.transform.localScale = unit * (1f + 0.08f * Mathf.Sin(t * 12f));
                float a = on ? Mathf.Clamp01(t / 0.3f) : sr.color.a - dt / 0.25f;
                sr.color = new Color(Dark.r, Dark.g, Dark.b, Mathf.Clamp01(a) * 0.85f);
                if (on || a > 0f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── 마기 폭발 (흡인 끝 · 페이즈 전환)
        void Burst(Vector3 at, float radius, int arcs)
        {
            Mist(at, radius * 2.2f);
            for (int i = 0; i < arcs; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 360f / arcs + UnityEngine.Random.Range(-10f, 10f), 0f) * Vector3.forward;
                Crescent(at + dir * radius * 0.6f, dir, radius * 0.9f, 0.35f, i * 0.01f);
            }
        }

        void Transition(Transform boss, Vector3 at, float radius, float time)
        {
            Burst(at, radius, 10);
            float t = 0f, spawn = 0f;
            _live.Add(dt =>
            {
                t += dt;
                spawn -= dt;
                Vector3 c = boss != null ? boss.position : at;
                if (spawn <= 0f && t < time) // 전환 동안 둘레에서 마기 연기가 솟아요
                {
                    spawn = 0.06f;
                    Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.6f, 2.2f);
                    RisingWisp(c + new Vector3(r.x, 0f, r.y), UnityEngine.Random.Range(0.6f, 1.1f), 0.8f);
                }
                if (t < time) return true;
                Burst(c, radius * 0.8f, 6);
                return false;
            });
        }

        void RisingWisp(Vector3 at, float width, float life)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Upright(_wisp, at, width, 6, bottom: true);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                pivot.position = at + Vector3.up * k * 0.8f;
                sr.transform.localScale = new Vector3(unit.x * (1f - 0.4f * k), unit.y * (0.6f + 0.6f * k), unit.z);
                sr.color = WispColor(Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.85f);
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 해체대법: 안전 반경 밖이 마기 안개로 가득 차요
        void TickFog(float dt)
        {
            float target = _boss != null && !_boss.IsDead ? _boss.SafeRadius : 0f;
            bool on = target > 0f;
            _fogAlpha = Mathf.MoveTowards(_fogAlpha, on ? 1f : 0f, dt / (on ? 1.5f : 0.8f));
            if (_fogAlpha <= 0f)
            {
                if (_fogA != null) { Destroy(_fogA.gameObject); Destroy(_fogB.gameObject); _fogA = _fogB = null; }
                _fogRadius = 0f;
                return;
            }
            Vector3 c = _boss != null ? _boss.ZoneCenter : Vector3.zero;
            if (_fogA == null)
            {
                _fogA = Flat(_miasmaA, c, 1f, 0f, 0.022f, 3);
                _fogB = Flat(_miasmaB, c, 1f, 0f, 0.023f, 3);
                _fogRadius = on ? target + 4f : 0f; // 가장자리에서부터 차올라요
            }
            if (on) _fogRadius = Mathf.MoveTowards(_fogRadius, target, dt * Mathf.Max(1.5f, (_fogRadius - target) * 2f));
            float half = _fogRadius / MiasmaHole; // 텍스처 구멍 반지름(MiasmaHole)이 안전 반경에 맞게
            float tt = Time.time;
            FogLayer(_fogA, c, half, 6f, 0f, tt, dt);
            FogLayer(_fogB, c, half, -9f, 1.7f, tt, dt);
            if (!on) return;
            _wispSpawn -= dt; // 경계에서 마기 연기가 피어올라요
            while (_wispSpawn <= 0f)
            {
                _wispSpawn += 0.05f;
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = _fogRadius + UnityEngine.Random.Range(0.1f, 1.6f);
                RisingWisp(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r, UnityEngine.Random.Range(0.6f, 1.2f), UnityEngine.Random.Range(0.9f, 1.4f));
            }
        }

        void FogLayer(SpriteRenderer sr, Vector3 c, float half, float spin, float phase, float tt, float dt)
        {
            if (sr == null) return;
            sr.transform.position = new Vector3(c.x, sr.transform.position.y, c.z);
            sr.transform.localScale = Vector3.one * (half * 2f / Mathf.Max(0.01f, sr.sprite.bounds.size.x)) * (1f + 0.012f * Mathf.Sin(tt * 1.3f + phase));
            sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
            sr.color = new Color(1f, 1f, 1f, (0.72f + 0.1f * Mathf.Sin(tt * 0.9f + phase)) * _fogAlpha);
        }

        const float MiasmaHole = 0.3f; // 안개 텍스처에서 구멍 반지름 (텍스처 반 폭 = 1)

        static Sprite MakeMiasma(int seed)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Miasma" };
            var px = new Color32[size * size];
            float ox = seed * 13.7f, oy = seed * 7.3f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                    float edge = MiasmaHole + 0.035f * (Mathf.PerlinNoise(ox + Mathf.Cos(ang) * 3f + 5f, oy + Mathf.Sin(ang) * 3f + 5f) - 0.3f);
                    float inside = Mathf.Clamp01((d - edge) / 0.07f);
                    inside = inside * inside * (3f - 2f * inside);
                    float n = Mathf.PerlinNoise(ox + u * 5f, oy + v * 5f) * 0.6f + Mathf.PerlinNoise(ox + u * 13f, oy + v * 13f) * 0.4f;
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - edge - 0.05f) / 0.05f); // 경계는 핏빛이 짙어요
                    float r = Mathf.Lerp(0.12f, 0.5f, rim * 0.8f + n * 0.15f), g = 0.03f, b = Mathf.Lerp(0.06f, 0.08f, n);
                    float a = inside * (0.55f + 0.45f * n);
                    px[y * size + x] = new Color(r, g, b, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }

        // ── 천마전 보호막: 금빛 내력이 흘러들어요
        void Inflow(Transform player)
        {
            for (int i = 0; i < 10; i++)
            {
                var lr = Line(player.position, player.position, Qi, 0.25f, 8);
                if (lr == null) return;
                float ang = i * 36f, t = -i * 0.06f;
                const float life = 0.9f, radius = 4.5f;
                _live.Add(dt =>
                {
                    if (lr == null) return false;
                    t += dt;
                    if (t < 0f) return true;
                    Vector3 c = player != null ? player.position : lr.GetPosition(1);
                    float k = t / life;
                    Vector3 Point(float kk)
                    {
                        float a = (ang - kk * 200f) * Mathf.Deg2Rad, r = radius * (1f - kk);
                        return c + new Vector3(Mathf.Cos(a) * r, 0.9f + (1f - kk) * 1.2f, Mathf.Sin(a) * r);
                    }
                    lr.SetPosition(0, Point(Mathf.Clamp01(k - 0.3f)));
                    lr.SetPosition(1, Point(Mathf.Clamp01(k)));
                    lr.startColor = lr.endColor = new Color(Qi.r, Qi.g, Qi.b, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                    if (k < 1f) return true;
                    Destroy(lr.gameObject);
                    return false;
                });
            }
            var ring = Flat(_ring, player.position, 1f, 0f, 0.08f, 7);
            if (ring == null) return;
            Vector3 unit = ring.transform.localScale;
            float rt = -1.3f;
            ring.enabled = false;
            _live.Add(dt =>
            {
                if (ring == null) return false;
                rt += dt;
                if (rt < 0f) return true;
                ring.enabled = true;
                if (player != null) ring.transform.position = new Vector3(player.position.x, 0.08f, player.position.z);
                float k = rt / 0.6f;
                ring.transform.localScale = unit * Mathf.Lerp(1f, 4f, Mathf.Sqrt(Mathf.Clamp01(k)));
                ring.color = new Color(1f, 0.97f, 0.85f, 1f - k);
                if (k < 1f) return true;
                Destroy(ring.gameObject);
                return false;
            });
        }

        // ── 도움

        void Mist(Vector3 at, float size)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Flat(_splash, at, size, UnityEngine.Random.Range(0f, 360f), 0.04f, 4);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t / 0.2f));
                sr.color = new Color(Dark.r * 1.4f, Dark.g, Dark.b, 0.85f * (1f - Mathf.Clamp01((t - 0.2f) / 0.7f)));
                if (t < 0.9f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }
        static float Yaw(Vector3 dir) => -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        static float CrescentYaw(Vector3 dir) => _ownCrescent ? Yaw(dir) + 180f : Yaw(dir) - 90f; // 천마 초승달은 볼록한 등이 그림 왼쪽, 혈불 것은 아래

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        LineRenderer Line(Vector3 a, Vector3 b, Color color, float width, int order)
        {
            if (_lineMat == null) return null;
            var lr = new GameObject("Demon Line").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.sharedMaterial = _lineMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, width * 0.2f), new Keyframe(0.7f, width), new Keyframe(1f, width * 0.6f));
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = order;
            return lr;
        }

        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width, int order, bool bottom = false)
        {
            if (sprite == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = at;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * s;
            if (bottom) go.transform.localPosition = Vector3.up * sprite.bounds.size.y * s * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _ring = Resources.Load<Sprite>("BloodBuddhaFx/Mandala");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _crescent = Resources.Load<Sprite>("HeavenlyDemonFx/Crescent");
            _ownCrescent = _crescent != null;
            if (_crescent == null) _crescent = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
            _phantom = Resources.Load<Sprite>("HeavenlyDemonFx/Phantom");
            _ownPhantom = _phantom != null;
            if (_phantom == null) _phantom = Resources.Load<Sprite>("TombFx/BloodShade");
            _wisp = Resources.Load<Sprite>("HeavenlyDemonFx/Wisp");
            _ownWisp = _wisp != null;
            if (_wisp == null) _wisp = Resources.Load<Sprite>("TombFx/BloodFlame");
            _hand = Resources.Load<Sprite>("HeavenlyDemonFx/Hand");
            _ownHand = _hand != null;
            if (_hand == null) _hand = Resources.Load<Sprite>("BloodBuddhaFx/Palm") ?? _ring;
            _sword = Resources.Load<Sprite>("HeavenlyDemonFx/Sword") ?? _crescent;
            _miasmaA = MakeMiasma(1);
            _miasmaB = MakeMiasma(2);
            _lineMat = new Material(Shader.Find("Sprites/Default"));
            var brush = Resources.Load<Texture2D>("HudArt/HpStroke");
            if (brush != null) _lineMat.mainTexture = brush;
        }
    }
}
