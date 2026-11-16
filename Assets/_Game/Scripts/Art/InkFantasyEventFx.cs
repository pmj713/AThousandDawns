using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 판타지 이벤트 방 결과 이펙트 (RoomRun.FantasyEventFx: forge · bless · curse · spring · unseal) — 그림만.
    //   이벤트 창이 떠 있는 동안 (Time.timeScale 0) 온 신호는 모아 뒀다가 창이 닫혀 시간이 흐르면 재생해요.
    //   forge  (모루 자리): 청보라 불티가 망치질처럼 세 번 튀고 보랏빛 고리가 퍼져요.
    //   bless  (플레이어): 하늘에서 금빛 빛줄기가 내려와 몸을 감싸고 금빛 티끌이 내려앉아요.
    //   curse  (플레이어): 검붉은 고리가 발밑에서 조여들며 돌고 검붉은 기운이 몸에 감겨 올라요.
    //   spring (플레이어): 발밑에서 청보라 물보라가 솟고, 빛 티끌이 몸으로 스며들며 잠시 몸에 맴돌아요.
    //   unseal (검 자리): 사슬 조각이 사방으로 튀고 룬 원이 크게 번쩍 깨지며 보랏빛 폭발이 일어나요.
    // 그림: PouchFx/Ring, HeavenlyDemonFx/RuneCircle.
    public class InkFantasyEventFx : MonoBehaviour
    {
        static readonly Color Violet = new Color(0.65f, 0.35f, 1f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        static readonly Color Curse = new Color(0.65f, 0.05f, 0.12f);

        static InkFantasyEventFx _host;
        static Sprite _ring, _rune;
        static Material _lineMat;
        static bool _loaded;

        readonly List<(string kind, Vector3 at)> _queue = new List<(string, Vector3)>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        Transform _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            RoomRun.FantasyEventFx -= OnFx;
            RoomRun.FantasyEventFx += OnFx;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkFantasyEventFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Fantasy Event Fx").AddComponent<InkFantasyEventFx>();
            return _host;
        }

        static void OnFx(RoomRun run, string kind, Vector3 at) => Host()._queue.Add((kind, at)); // 창이 닫힐 때까지 기다려요

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            if (Time.timeScale > 0f && _queue.Count > 0)
            {
                if (_player == null) { var p = FindAnyObjectByType<PlayerController>(); _player = p != null ? p.transform : null; }
                foreach (var (kind, at) in _queue) Play(kind, at);
                _queue.Clear();
            }
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void Play(string kind, Vector3 at)
        {
            switch (kind)
            {
                case "forge": Forge(at); break;
                case "bless": Bless(at); break;
                case "curse": Cursed(at); break;
                case "spring": Spring(at); break;
                case "unseal": Unseal(at); break;
            }
        }

        Vector3 PlayerAt(Vector3 fallback) => _player != null ? _player.position : fallback;

        // ── forge: 청보라 불티가 세 번

        void Forge(Vector3 at)
        {
            float t = 0f;
            int hits = 0;
            _live.Add(dt =>
            {
                t += dt;
                if (hits < 3 && t >= hits * 0.22f)
                {
                    hits++;
                    InkParticles.Burst(at + Vector3.up * 0.9f, 16, new Color(0.8f, 0.85f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0.7f), 3f, 0.08f, 0.45f, 1.2f);
                    Ring(at, 1.6f, 0.35f, Violet);
                }
                return hits < 3;
            });
        }

        // ── bless: 하늘에서 금빛 빛줄기

        void Bless(Vector3 at)
        {
            var beam = Line(Gold, 1.4f);
            InkParticles.Burst(PlayerAt(at) + Vector3.up * 3f, 24, new Color(1f, 0.95f, 0.7f, 1f), new Color(Gold.r, Gold.g, Gold.b, 0.4f), 1.2f, 0.12f, 1.2f, 1.5f); // 금빛 티끌이 내려앉아요
            Ring(PlayerAt(at), 2.2f, 0.6f, Gold);
            float t = 0f;
            _live.Add(dt =>
            {
                if (beam == null) return false;
                t += dt;
                Vector3 p = PlayerAt(at);
                float down = Mathf.Clamp01(t / 0.2f), fade = t < 0.8f ? 1f : Mathf.Clamp01(1f - (t - 0.8f) / 0.5f);
                beam.SetPosition(0, p + Vector3.up * 9f);
                beam.SetPosition(1, p + Vector3.up * Mathf.Lerp(9f, 0f, down));
                beam.widthMultiplier = 1.4f * fade * (1f + 0.08f * Mathf.Sin(t * 20f));
                if (fade > 0f) return true;
                Destroy(beam.gameObject);
                return false;
            });
        }

        // ── curse: 검붉은 고리가 조여들며 기운이 감겨요

        void Cursed(Vector3 at)
        {
            var sr = Flat(_ring, PlayerAt(at), 3f, 0.03f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f, puff = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                Vector3 p = PlayerAt(at);
                sr.transform.position = new Vector3(p.x, 0.03f, p.z);
                sr.transform.Rotate(0f, 0f, -240f * dt, Space.Self);
                float k = Mathf.Clamp01(t / 1.2f);
                sr.transform.localScale = unit * Mathf.Lerp(1f, 0.35f, k * k); // 조여들어요
                sr.color = new Color(Curse.r, Curse.g, Curse.b, (1f - k) * 0.9f + 0.1f);
                puff -= dt;
                if (puff <= 0f && t < 1.1f)
                {
                    puff = 0.1f;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 0.5f;
                    InkParticles.Burst(p + new Vector3(r.x, 0.3f, r.y), 3, new Color(0.85f, 0.15f, 0.2f, 0.8f), new Color(0.2f, 0.02f, 0.05f, 0f), 0.5f, 0.35f, 0.7f, -1.4f);
                }
                if (t < 1.3f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── spring: 청보라 물보라가 솟고 빛이 몸으로 스며요

        void Spring(Vector3 at)
        {
            Vector3 p0 = PlayerAt(at);
            InkParticles.Burst(p0 + Vector3.up * 0.2f, 26, new Color(0.6f, 0.9f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0.6f), 2.6f, 0.12f, 0.8f, 1.6f); // 물보라
            Ring(p0, 2.4f, 0.5f, new Color(0.4f, 0.75f, 1f));
            var root = new GameObject("Spring Glow").transform;
            root.SetParent(transform, false);
            var motes = InkParticles.Frost(root, Vector3.up * 0.8f, 0.6f, 2.5f);
            if (motes != null) { var main = motes.main; main.startColor = new Color(0.75f, 0.7f, 1f, 1f); }
            float t = 0f;
            _live.Add(dt =>
            {
                if (root == null) return false;
                t += dt;
                root.position = PlayerAt(at); // 몸에 맴돌아요
                if (t < 1.6f) return true;
                Destroy(root.gameObject);
                return false;
            });
        }

        // ── unseal: 사슬이 끊어지고 룬 원이 깨지며 보랏빛 폭발

        void Unseal(Vector3 at)
        {
            InkParticles.Burst(at + Vector3.up * 0.9f, 22, new Color(0.75f, 0.75f, 0.8f, 1f), new Color(0.4f, 0.4f, 0.45f, 0.6f), 4f, 0.1f, 0.7f, 1.6f); // 사슬 조각
            InkParticles.Burst(at + Vector3.up * 0.8f, 40, new Color(0.9f, 0.75f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0.5f), 5f, 0.35f, 0.9f, -0.3f);
            Ring(at, 4.5f, 0.6f, Violet);
            var rune = Flat(_rune, at, 3f, 0.035f);
            if (rune == null) return;
            Vector3 unit = rune.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (rune == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / 0.5f);
                rune.transform.localScale = unit * Mathf.Lerp(1f, 1.8f, Mathf.Sqrt(k)); // 크게 번쩍 깨져 흩어져요
                rune.transform.Rotate(0f, 0f, 300f * dt, Space.Self);
                rune.color = new Color(1f, 1f, 1f, 1f - k);
                if (k < 1f) return true;
                Destroy(rune.gameObject);
                return false;
            });
        }

        // ── 도움

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

        LineRenderer Line(Color color, float width)
        {
            var go = new GameObject("Bless Beam");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.positionCount = 2;
            lr.widthMultiplier = width;
            lr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.5f), 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 1f) },
            };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
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
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _rune = Resources.Load<Sprite>("HeavenlyDemonFx/RuneCircle");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
