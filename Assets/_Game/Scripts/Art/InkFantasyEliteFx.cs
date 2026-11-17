using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 판타지 전용 정예 특성 이펙트 (마법 방벽 · 저주 · 분노) — 그림만, 판정·수치는 게임 코드 그대로. 머리 위 표식은 InkEliteMarks (EliteFx/Barrier · Curse · Frenzy).
    //   마법 방벽 (Barrier, Damageable.Ward > 0 동안): 몸을 감싸는 청색 반투명 육각 방벽이 일렁여요 (둘러질 때 BarrierRaised 순간 퍼지며 나타나요).
    //     Ward가 0이 되는 순간: 청색 유리 조각이 사방으로 튀고 방벽이 사라져요. (목탁승 금빛 가호 InkTempleEnemyFx는 방벽 정예를 건너뛰어요)
    //   저주 (PlayerController.IsCursed): 플레이어 발밑에 보라 저주 문양이 돌고, 몸에서 보라 연기가 피어올라요.
    //   분노 (Frenzied, FrenzyStacks): 쌓일 때 붉은 김이 터지고, 겹 수만큼 붉은 기운이 짙고 잦게 피어올라요.
    // 그림: EliteFx/Barrier (육각 방벽 구), FantasyFx/Curse (저주 문양), PouchFx/Ring.
    public class InkFantasyEliteFx : MonoBehaviour
    {
        static readonly Color Barrier = new Color(0.45f, 0.8f, 1f);
        static readonly Color Violet = new Color(0.65f, 0.3f, 1f);

        static InkFantasyEliteFx _host;
        static Sprite _barrier, _curse, _ring;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly Dictionary<EnemyBrain, SpriteRenderer> _shields = new Dictionary<EnemyBrain, SpriteRenderer>();
        readonly Dictionary<EnemyBrain, float> _frenzy = new Dictionary<EnemyBrain, float>();
        readonly List<EnemyBrain> _keys = new List<EnemyBrain>();
        EnemyDirector _director;
        PlayerController _player;
        SpriteRenderer _curseMark;
        float _curseSmoke;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.BarrierRaised -= OnRaised;
            EnemyBrain.BarrierRaised += OnRaised;
            EnemyBrain.Frenzied -= OnFrenzied;
            EnemyBrain.Frenzied += OnFrenzied;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkFantasyEliteFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Fantasy Elite Fx").AddComponent<InkFantasyEliteFx>();
            return _host;
        }

        static void OnRaised(EnemyBrain e) { if (e != null) Host().Ring(e.transform.position, 2.4f, 0.35f, Barrier); }

        static void OnFrenzied(EnemyBrain e, int stacks)
        {
            if (e == null) return;
            var p = e.transform.position;
            InkParticles.Burst(p + Vector3.up * 1.2f, 10 + stacks * 6, new Color(1f, 0.4f, 0.3f, 0.9f), new Color(0.6f, 0.06f, 0.05f, 0f), 2f, 0.5f, 0.7f, -0.8f);
            Host().Ring(p, 2f + stacks * 0.4f, 0.35f, new Color(1f, 0.2f, 0.12f));
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_player == null) _player = _director != null ? _director.Player : null;
            TickShields(dt);
            TickFrenzy(dt);
            TickCurse(dt);
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        // ── 마법 방벽

        void TickShields(float dt)
        {
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                    if (e != null && !e.IsDead && e.Damageable != null && e.Damageable.Ward > 0 && !_shields.ContainsKey(e) && e.HasTrait(EliteTrait.Barrier))
                        _shields[e] = Billboard(_barrier, Center(e), Mathf.Max(1.4f, e.Height * 1.4f), 6);
            _keys.Clear();
            _keys.AddRange(_shields.Keys);
            float time = Time.time;
            foreach (var e in _keys)
            {
                var sr = _shields[e];
                bool on = e != null && !e.IsDead && e.Damageable != null && e.Damageable.Ward > 0;
                if (!on || sr == null)
                {
                    if (sr != null)
                    {
                        if (e != null && !e.IsDead) Shatter(sr.transform.parent.position); // 깨졌어요
                        Destroy(sr.transform.parent.gameObject);
                    }
                    _shields.Remove(e);
                    continue;
                }
                sr.transform.parent.position = Center(e);
                sr.color = new Color(1f, 1f, 1f, 0.32f + 0.08f * Mathf.Sin(time * 4f));
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, time * 15f);
            }
        }

        void Shatter(Vector3 at)
        {
            InkParticles.Burst(at, 22, new Color(0.8f, 0.95f, 1f, 1f), new Color(Barrier.r, Barrier.g, Barrier.b, 0.6f), 4f, 0.12f, 0.6f, 1.4f); // 유리 조각
            Ring(at - Vector3.up * at.y, 3f, 0.35f, Barrier);
        }

        // ── 분노: 겹 수만큼 붉은 기운

        void TickFrenzy(float dt)
        {
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
            {
                if (e == null || e.IsDead || e.FrenzyStacks <= 0) continue;
                _frenzy.TryGetValue(e, out float next);
                next -= dt;
                if (next <= 0f)
                {
                    int n = e.FrenzyStacks;
                    next = 0.3f / n;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 0.45f;
                    InkParticles.Burst(e.transform.position + new Vector3(r.x, 0.6f, r.y), 1 + n, new Color(1f, 0.25f, 0.15f, 0.4f + 0.15f * n), new Color(0.6f, 0.05f, 0.05f, 0f), 0.4f, 0.4f + 0.1f * n, 0.8f, -1.2f);
                }
                _frenzy[e] = next;
            }
        }

        // ── 저주 (플레이어)

        void TickCurse(float dt)
        {
            bool cursed = _player != null && !_player.IsDead && _player.IsCursed;
            if (cursed && _curseMark == null) _curseMark = Flat(_curse, _player.transform.position, 1.8f, 0.027f);
            if (_curseMark == null) return;
            if (!cursed)
            {
                _curseMark.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(_curseMark.color.a, 0f, dt / 0.3f));
                if (_curseMark.color.a <= 0f) { Destroy(_curseMark.gameObject); _curseMark = null; }
                return;
            }
            Vector3 p = _player.transform.position;
            _curseMark.transform.position = new Vector3(p.x, 0.027f, p.z);
            _curseMark.transform.Rotate(0f, 0f, 60f * dt, Space.Self);
            _curseMark.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(_curseMark.color.a, 0.85f, dt / 0.2f));
            _curseSmoke -= dt;
            if (_curseSmoke <= 0f) // 몸에서 보라 연기
            {
                _curseSmoke = 0.12f;
                Vector2 r = UnityEngine.Random.insideUnitCircle * 0.35f;
                InkParticles.Burst(p + new Vector3(r.x, 0.5f, r.y), 2, new Color(Violet.r, Violet.g, Violet.b, 0.7f), new Color(0.25f, 0.05f, 0.35f, 0f), 0.4f, 0.4f, 0.9f, -1.1f);
            }
        }

        // ── 도움

        static Vector3 Center(EnemyBrain e) => e.transform.position + Vector3.up * Mathf.Max(0.6f, e.Height * 0.55f);

        void Ring(Vector3 at, float size, float life, Color color)
        {
            var sr = Flat(_ring, at, size, 0.03f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, 1f - k * k);
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
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
            sr.color = new Color(1f, 1f, 1f, 0f);
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
            sr.sortingOrder = 5;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _barrier = Resources.Load<Sprite>("EliteFx/Barrier");
            _curse = Resources.Load<Sprite>("FantasyFx/Curse");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
        }
    }
}
