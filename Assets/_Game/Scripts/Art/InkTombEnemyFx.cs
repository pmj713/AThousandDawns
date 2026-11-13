using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 천마총 적 이펙트 (적 상태를 읽어서 그림만 — 판정·수치는 게임 코드 그대로, 게임 코드 수정 없음).
    //   마교도 광폭 (CultistBrain.IsFrenzied): 몸 둘레로 핏빛 불꽃이 일렁이고 발밑에 붉은 고리가 돌아요.
    //   혈인 부활 (EnemyBrain.Revived): 되살아난 자리에서 피가 솟구치고 바닥에 핏물이 번져요.
    //   천마 근위 반격 (DemonGuardBrain.Counters가 늘 때): 앞쪽으로 붉은 초승달 섬광과 불똥.
    //   혈폭자 ("핏덩이" BloodBurst): 핏덩이가 혈폭자에게서 포물선으로 날아가 떨어지고, 터질 때 핏물이 퍼져요 (죽은 뒤 폭발은 그 자리에서).
    //   혈궁수 붉은 화살은 InkProjectiles가 맡아요.
    // 그림: TombFx(BloodFlame · BloodFountain), BloodBuddhaFx(Splash · Slash · Drop), PouchFx(Ring · Glint).
    public class InkTombEnemyFx : MonoBehaviour
    {
        static InkTombEnemyFx _host;
        static Sprite _flame, _fountain, _splash, _slash, _drop, _ring, _glint;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly Dictionary<CultistBrain, GameObject> _frenzy = new Dictionary<CultistBrain, GameObject>();
        readonly HashSet<EnemyBrain> _revived = new HashSet<EnemyBrain>();
        readonly Dictionary<DemonGuardBrain, int> _counters = new Dictionary<DemonGuardBrain, int>();
        readonly HashSet<GameObject> _globs = new HashSet<GameObject>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>(); // 이름을 이미 확인한 씬 루트
        EnemyDirector _director;
        float _scan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (FindAnyObjectByType<EnemyDirector>() == null) return;
            Load();
            if (_host == null) _host = new GameObject("Ink Tomb Enemy Fx").AddComponent<InkTombEnemyFx>();
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            TickEnemies();
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.05f; DressGlobs(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void TickEnemies()
        {
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
            {
                if (e == null || e.IsDead) continue;
                if (e is CultistBrain c && c.IsFrenzied && !_frenzy.ContainsKey(c)) _frenzy[c] = Frenzy(c);
                if (e.Revived && _revived.Add(e)) Revive(e.transform.position);
                if (e is DemonGuardBrain g)
                {
                    _counters.TryGetValue(g, out int seen);
                    if (g.Counters > seen) Counter(g);
                    _counters[g] = g.Counters;
                }
            }
            _revived.RemoveWhere(e => e == null);
            var gone = new List<CultistBrain>();
            foreach (var pair in _frenzy)
                if (pair.Key == null || pair.Key.IsDead || !pair.Key.IsFrenzied) { if (pair.Value != null) Destroy(pair.Value); gone.Add(pair.Key); }
            foreach (var c in gone) _frenzy.Remove(c);
        }

        // ── 마교도 광폭: 적 자식으로 단 핏빛 불꽃 · 발밑 고리
        GameObject Frenzy(CultistBrain c)
        {
            var root = new GameObject("Frenzy Aura");
            root.transform.SetParent(c.transform, false);
            if (_ring != null)
            {
                var ring = new GameObject("Ring");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = Vector3.one * (1.6f / Mathf.Max(0.01f, _ring.bounds.size.x));
                var sr = ring.AddComponent<SpriteRenderer>();
                sr.sprite = _ring;
                sr.color = new Color(1f, 0.25f, 0.2f, 0.8f);
                sr.sortingOrder = 4;
                ring.AddComponent<InkSpin>().Speed(150f);
            }
            if (_flame != null)
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * Mathf.PI * 2f;
                    var pivot = new GameObject("Flame");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.45f, 0.1f, Mathf.Sin(a) * 0.45f);
                    pivot.AddComponent<InkFaceCamera>();
                    var go = new GameObject("Sprite");
                    go.transform.SetParent(pivot.transform, false);
                    float s = 0.55f / Mathf.Max(0.01f, _flame.bounds.size.x);
                    go.transform.localScale = Vector3.one * s;
                    go.transform.localPosition = Vector3.up * _flame.bounds.size.y * s * 0.5f;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _flame;
                    sr.sortingOrder = 6;
                    float seed = i * 1.7f, baseY = go.transform.localPosition.y;
                    var t = go.transform;
                    _live.Add(dt => // 일렁여요
                    {
                        if (t == null) return false;
                        float f = 1f + 0.18f * Mathf.Sin(Time.time * 11f + seed) + 0.08f * Mathf.Sin(Time.time * 23f + seed);
                        t.localScale = new Vector3(s / f, s * f, s);
                        t.localPosition = Vector3.up * baseY * f;
                        sr.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.time * 7f + seed));
                        return true;
                    });
                }
            return root;
        }

        // ── 혈인 부활: 피가 솟구치고 핏물이 번져요
        void Revive(Vector3 at)
        {
            Spread(_splash, at, 2.4f, 1.6f);
            var sr = Upright(_fountain != null ? _fountain : _drop, at, 1.3f, 7, bottom: true);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float up = t < 0.15f ? t / 0.15f : Mathf.Lerp(1f, 0.1f, (t - 0.15f) / 0.6f); // 솟구쳤다 가라앉아요
                sr.transform.localScale = new Vector3(unit.x, unit.y * Mathf.Max(0.05f, up), unit.z);
                var sp = sr.sprite;
                sr.transform.localPosition = Vector3.up * sp.bounds.size.y * sr.transform.localScale.y * 0.5f;
                sr.color = new Color(1f, 1f, 1f, t < 0.5f ? 1f : Mathf.Clamp01(1f - (t - 0.5f) / 0.25f));
                if (t < 0.75f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        // ── 천마 근위 반격: 붉은 초승달 섬광과 불똥
        void Counter(DemonGuardBrain g)
        {
            Vector3 dir = g.transform.forward; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 at = g.transform.position + dir * 1.2f;
            if (_slash != null)
            {
                var go = new GameObject("Counter Slash");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(at.x, 0.9f, at.z);
                go.transform.rotation = Quaternion.Euler(90f, -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg - 90f, 0f); // 활 등이 앞으로
                float s = 2.2f / Mathf.Max(0.01f, _slash.bounds.size.x);
                go.transform.localScale = Vector3.one * s;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _slash;
                sr.sortingOrder = 7;
                float t = 0f;
                _live.Add(dt =>
                {
                    if (go == null) return false;
                    t += dt;
                    go.transform.localScale = Vector3.one * s * Mathf.Lerp(0.6f, 1.2f, t / 0.25f);
                    sr.color = new Color(1f, 0.85f, 0.8f, 1f - t / 0.25f);
                    if (t < 0.25f) return true;
                    Destroy(go);
                    return false;
                });
            }
            for (int i = 0; i < 8; i++)
            {
                var v = (dir + UnityEngine.Random.insideUnitSphere * 0.7f) * UnityEngine.Random.Range(3f, 5f);
                v.y = Mathf.Abs(v.y) + 1f;
                Spark(at + Vector3.up * 0.9f, v);
            }
        }

        // ── 혈폭자: 날아가는 핏덩이 · 터지는 핏물
        void DressGlobs()
        {
            _globs.RemoveWhere(g => g == null);
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 확인해요
            {
                if (root.name != "핏덩이" || _globs.Contains(root) || root.GetComponent<BloodBurst>() == null) continue;
                _globs.Add(root);
                var go = root;
                Vector3 land = root.transform.position;
                var bomber = Nearest<BloodBomberBrain>(land);
                Vector3 from = bomber != null ? bomber.transform.position + Vector3.up * 1.2f : land;
                bool thrown = bomber != null && (Flat(bomber.transform.position) - Flat(land)).magnitude > 1f; // 죽은 뒤 폭발은 제자리
                var glob = thrown ? Upright(_drop, from, 0.35f, 7) : null;
                float t = 0f, flight = 0.45f;
                Vector3 last = land;
                _live.Add(dt =>
                {
                    t += dt;
                    if (go != null) last = go.transform.position;
                    if (glob != null) // 포물선으로 날아가 떨어져요
                    {
                        float k = Mathf.Clamp01(t / flight);
                        glob.transform.parent.position = Vector3.Lerp(from, land + Vector3.up * 0.2f, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
                        if (k >= 1f) { Destroy(glob.transform.parent.gameObject); glob = null; }
                    }
                    if (go != null) return true;
                    // 터졌어요: 터지는 핏물은 InkBurstFx(BurstKind.BloodGlob / BloodDeath)가 그려요
                    if (glob != null) Destroy(glob.transform.parent.gameObject);
                    return false;
                });
            }
        }

        // ── 도움

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        T Nearest<T>(Vector3 at) where T : EnemyBrain
        {
            T best = null;
            float bestD = float.MaxValue;
            if (_director == null || _director.Enemies == null) return null;
            foreach (var e in _director.Enemies)
                if (e is T t && !t.IsDead)
                {
                    float d = (Flat(t.transform.position) - Flat(at)).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = t; }
                }
            return best;
        }

        // 바닥 핏물이 번졌다(0.2초) life 동안 바래요
        void Spread(Sprite sprite, Vector3 at, float size, float life)
        {
            if (sprite == null) return;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.035f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            float unit = 1f / Mathf.Max(0.01f, sprite.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                go.transform.localScale = Vector3.one * unit * Mathf.Lerp(size * 0.3f, size, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f));
                sr.color = new Color(1f, 1f, 1f, t < 0.4f ? 1f : Mathf.Clamp01(1f - (t - 0.4f) / life));
                if (t < 0.4f + life) return true;
                Destroy(go);
                return false;
            });
        }

        void Droplet(Vector3 at, Vector3 v)
        {
            var sr = Upright(_drop, at, 0.12f, 7);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                v += Vector3.down * 12f * dt;
                pivot.position += v * dt;
                if (pivot.position.y > 0.05f && t < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        void Spark(Vector3 at, Vector3 v)
        {
            var sr = Upright(_glint, at, UnityEngine.Random.Range(0.15f, 0.28f), 8);
            if (sr == null) return;
            sr.color = new Color(1f, 0.45f, 0.35f);
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                v += Vector3.down * 9f * dt;
                pivot.position += v * dt;
                sr.transform.localScale = unit * (1f - t / 0.3f);
                if (t < 0.3f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // 카메라를 보는 받침(부모) 안의 그림. bottom이면 아래 끝이 at
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
            _flame = Resources.Load<Sprite>("TombFx/BloodFlame");
            _fountain = Resources.Load<Sprite>("TombFx/BloodFountain");
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _slash = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
        }
    }
}
