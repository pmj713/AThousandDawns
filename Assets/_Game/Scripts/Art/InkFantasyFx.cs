using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 차원 겹침 판타지 적 이펙트 (그림만 — 판정·수치는 게임 코드 그대로). 수묵과 일부러 다른 화풍: 굵은 납선 스테인드글라스 · 채색 동판화.
    //   고블린 주술사 저주 장판 (GhostFire, 주인이 GoblinShamanBrain): 하늘색 원판 대신 보라·연두 가시덩굴 뱀눈 원이 펼쳐지며 천천히 돌아요.
    //     (등불 귀신 푸른 불 꾸밈 InkTempleEnemyFx는 IsCurse로 이 장판을 건너뛰어요)
    //   광폭화 (판타지 적의 EnemyBrain.IsHastened): 발밑에 붉은·금빛 장미창이 빠르게 돌며 맥박쳐요. (수묵 적의 붉은 먹 기운은 InkRiverEnemyFx 그대로)
    //   슬라임 갈라짐 (EnemyBrain.SplitApart, 쓰러진 쪽이 SlimeBrain): 초록 유리 슬라임이 바닥에 철퍽 퍼지고, 방울들이 튀어 떨어져 끈적이 자국으로 남았다 사라져요.
    //     (먹 갈라짐 InkSplitBurst는 슬라임이면 건너뛰어요)
    // 그림: FantasyFx (Slime · Ward · Curse).
    public class InkFantasyFx : MonoBehaviour
    {
        static InkFantasyFx _host;
        static Sprite _slime, _ward, _curse;
        static bool _loaded;
        static readonly Dictionary<GhostFire, bool> CurseCache = new Dictionary<GhostFire, bool>();

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GhostFire> _dressed = new HashSet<GhostFire>();
        readonly Dictionary<EnemyBrain, SpriteRenderer> _haste = new Dictionary<EnemyBrain, SpriteRenderer>();
        EnemyDirector _director;
        float _scan;

        public static bool IsFantasy(EnemyBrain e) => e != null && e.Kind >= EnemyKind.Slime;

        // 고블린 주술사가 깐 장판인지 (장판마다 처음 한 번만 GhostFire.Owner를 봐요)
        public static bool IsCurse(GhostFire fire)
        {
            if (fire == null) return false;
            if (CurseCache.TryGetValue(fire, out bool curse)) return curse;
            var owner = fire.Owner;
            curse = owner != null && owner.GetComponentInParent<GoblinShamanBrain>() != null;
            if (CurseCache.Count > 64) CurseCache.Clear(); // 꺼진 장판 정리 (다시 읽으면 그만)
            CurseCache[fire] = curse;
            return curse;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.SplitApart -= OnSplit;
            EnemyBrain.SplitApart += OnSplit;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkFantasyFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Fantasy Fx").AddComponent<InkFantasyFx>();
            return _host;
        }

        static void OnSplit(EnemyBrain elite, Vector3 at, EnemyBrain a, EnemyBrain b)
        {
            if (elite is SlimeBrain) Host().Splat(at, 2.2f);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.1f; DressCurses(); }
            TickHaste();
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        // ── 고블린 주술사 저주 장판

        void DressCurses()
        {
            _dressed.RemoveWhere(f => f == null);
            foreach (var fire in FindObjectsByType<GhostFire>())
            {
                if (_dressed.Contains(fire) || !IsCurse(fire)) continue;
                _dressed.Add(fire);
                var source = fire.GetComponent<Renderer>();
                if (source != null) source.enabled = false; // 하늘색 원판은 숨기고 (흐려지는 값은 그 재질 알파를 읽어요)
                float width = fire.transform.localScale.x * 1.15f;
                var circle = Flat(_curse, fire.transform.position, width, UnityEngine.Random.Range(0f, 360f), 0.022f);
                var glow = Flat(_curse, fire.transform.position, width * 1.12f, UnityEngine.Random.Range(0f, 360f), 0.021f);
                if (circle == null) continue;
                Vector3 unit = circle.transform.localScale, glowUnit = glow != null ? glow.transform.localScale : Vector3.one;
                float t = 0f;
                _live.Add(dt =>
                {
                    if (fire == null)
                    {
                        if (circle != null) Destroy(circle.gameObject);
                        if (glow != null) Destroy(glow.gameObject);
                        return false;
                    }
                    t += dt;
                    float alpha = source != null ? Mathf.Clamp01(source.sharedMaterial.color.a / 0.55f) : 1f;
                    float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f); // 펼쳐져요
                    circle.transform.Rotate(0f, 0f, 14f * dt, Space.Self);
                    circle.transform.localScale = unit * Mathf.Lerp(0.3f, 1f, open);
                    circle.color = new Color(1f, 1f, 1f, 0.95f * alpha);
                    if (glow != null) // 뒤에서 보랏빛이 맥박쳐요
                    {
                        glow.transform.Rotate(0f, 0f, -22f * dt, Space.Self);
                        glow.transform.localScale = glowUnit * Mathf.Lerp(0.3f, 1f + 0.05f * Mathf.Sin(t * 5f), open);
                        glow.color = new Color(0.7f, 0.35f, 1f, (0.25f + 0.15f * Mathf.Sin(t * 5f)) * alpha);
                    }
                    return true;
                });
            }
        }

        // ── 광폭화: 판타지 적 발밑 붉은·금빛 장미창

        void TickHaste()
        {
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                    if (e != null && !e.IsDead && e.IsHastened && IsFantasy(e) && !_haste.ContainsKey(e))
                        _haste[e] = Flat(_ward, e.transform.position, 1.7f, 0f, 0.026f);
            List<EnemyBrain> done = null;
            foreach (var kv in _haste)
            {
                var e = kv.Key;
                var sr = kv.Value;
                if (sr == null) { (done ??= new List<EnemyBrain>()).Add(e); continue; }
                bool on = e != null && !e.IsDead && e.IsHastened;
                float a = sr.color.a + (on ? 1f : -1f) * Time.deltaTime / 0.25f;
                if (!on && a <= 0f) { Destroy(sr.gameObject); (done ??= new List<EnemyBrain>()).Add(e); continue; }
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 9f);
                if (e != null) sr.transform.position = new Vector3(e.transform.position.x, 0.026f, e.transform.position.z);
                sr.transform.Rotate(0f, 0f, 120f * Time.deltaTime, Space.Self);
                sr.transform.localScale = Vector3.one * (1.7f / Mathf.Max(0.01f, sr.sprite.bounds.size.x)) * pulse;
                sr.color = new Color(1f, 0.5f, 0.3f, Mathf.Clamp(a, 0f, 0.85f));
            }
            if (done != null) foreach (var e in done) _haste.Remove(e);
        }

        // ── 슬라임 갈라짐: 철퍽 퍼지는 유리 슬라임 + 튀는 방울 → 끈적이 자국

        void Splat(Vector3 at, float width)
        {
            var pool = Flat(_slime, at, width, UnityEngine.Random.Range(0f, 360f), 0.021f);
            if (pool == null) return;
            Vector3 unit = pool.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (pool == null) return false;
                t += dt;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.15f), 3f);
                pool.transform.localScale = unit * Mathf.Lerp(0.4f, 1f, k) * (t < 0.3f ? 1f + 0.08f * Mathf.Sin(t * 40f) * (1f - t / 0.3f) : 1f); // 철퍽 — 출렁
                pool.color = new Color(1f, 1f, 1f, t < 2.5f ? 0.95f : Mathf.Clamp01(0.95f * (1f - (t - 2.5f) / 1f)));
                if (t < 3.5f) return true;
                Destroy(pool.gameObject);
                return false;
            });
            for (int i = 0; i < 6; i++) // 방울: 포물선을 그리며 튀었다 떨어져 작은 자국으로
            {
                float ang = (i * 60f + UnityEngine.Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float dist = UnityEngine.Random.Range(0.9f, 1.6f), size = UnityEngine.Random.Range(0.25f, 0.4f), air = 0.35f;
                var drop = Flat(_slime, at, size, UnityEngine.Random.Range(0f, 360f), 0.3f);
                if (drop == null) continue;
                float dt0 = 0f;
                _live.Add(dt =>
                {
                    if (drop == null) return false;
                    dt0 += dt;
                    float f = Mathf.Clamp01(dt0 / air);
                    var p = at + dir * dist * f;
                    drop.transform.position = new Vector3(p.x, 0.021f + 1.1f * f * (1f - f) * 4f * 0.3f, p.z);
                    drop.color = new Color(1f, 1f, 1f, dt0 < 2.2f ? 1f : Mathf.Clamp01(1f - (dt0 - 2.2f) / 0.8f));
                    if (dt0 < 3f) return true;
                    Destroy(drop.gameObject);
                    return false;
                });
            }
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
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
            _slime = Resources.Load<Sprite>("FantasyFx/Slime");
            _ward = Resources.Load<Sprite>("FantasyFx/Ward");
            _curse = Resources.Load<Sprite>("FantasyFx/Curse");
        }
    }
}
