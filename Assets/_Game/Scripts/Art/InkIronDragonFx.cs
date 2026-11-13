using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 수채주 철룡 이펙트 (IronDragonBrain.Cue를 듣고 그림만 — 판정·수치는 게임 코드 그대로).
    //   유성추 내려찍기: 바닥에 철구 그림자가 짙어지고, 마지막에 쇠사슬 달린 철구가 하늘에서 떨어져 → 땅이 갈라지고 돌이 튀어요.
    //   물기둥: 바닥 물결이 커지다가 물기둥이 솟구쳤다 무너져요.
    //   사슬 당기기: 쇠사슬이 직선으로 뻗었다 감겨요. 걸렸으면 사슬 끝이 플레이어에게 붙은 채 끌려와요.
    //   유성추 휘두르기: 사슬 달린 철구가 앞 부채꼴을 크게 휩쓸어요.
    //   2페이즈 전환: 배가 부서져 나뭇조각이 튀고 물보라가 일어요.
    //   물길("물길" 원판): 불투명한 청록 원판을 숨기고 출렁이는 물 표면을 깔아요.
    // 그림: RiverFx(Ripple · Splash · Whirlpool · ChainTile), TempleFx(Rock), SkillFx(QuakeRing).
    public class InkIronDragonFx : MonoBehaviour
    {
        static InkIronDragonFx _host;
        static Sprite _ripple, _splash, _whirl, _rock, _crack;
        static Material _chainMat;
        static bool _loaded;

        static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f);
        static readonly Color Wood = new Color(0.45f, 0.33f, 0.22f);

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GameObject> _dressed = new HashSet<GameObject>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>(); // 이름을 이미 확인한 씬 루트
        PlayerController _player;
        float _scan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            IronDragonBrain.Cue -= OnCue;
            IronDragonBrain.Cue += OnCue;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkIronDragonFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Iron Dragon Fx").AddComponent<InkIronDragonFx>();
            return _host;
        }

        static void OnCue(IronDragonBrain boss, IronDragonBrain.FxCue c)
        {
            var h = Host();
            switch (c.Kind)
            {
                case IronDragonBrain.FxKind.Slam: h.Slam(boss, c); break;
                case IronDragonBrain.FxKind.Geyser: h.Geyser(c); break;
                case IronDragonBrain.FxKind.Hook: h.Hook(boss, c); break;
                case IronDragonBrain.FxKind.Swing: h.Swing(boss, c); break;
                case IronDragonBrain.FxKind.Transition: h.Wreck(c); break;
            }
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.1f; DressWater(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 유성추 내려찍기

        void Slam(IronDragonBrain boss, IronDragonBrain.FxCue c)
        {
            float fall = Mathf.Max(0.05f, c.Time), drop = Mathf.Min(0.3f, fall), size = Mathf.Max(0.8f, c.Radius * 0.7f);
            var shadow = Flat(_crack, c.At, c.Radius * 1.6f, UnityEngine.Random.Range(0f, 360f), 0.035f);
            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0f);
            SpriteRenderer ball = null;
            LineRenderer chain = null;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (shadow != null) shadow.color = new Color(0.1f, 0.1f, 0.1f, 0.35f * Mathf.Clamp01(t / fall)); // 철구 그림자가 짙어져요
                if (ball == null && t >= fall - drop)
                {
                    ball = Upright(_rock, c.At + Vector3.up * 8f, size, 7);
                    if (ball != null) ball.color = Iron;
                    chain = Chain();
                }
                if (ball != null && t < fall)
                {
                    float k = Mathf.Clamp01((t - (fall - drop)) / drop);
                    ball.transform.parent.position = c.At + Vector3.up * Mathf.Lerp(8f, size * 0.5f, k * k);
                    ball.transform.Rotate(0f, 0f, 720f * dt, Space.Self);
                }
                if (chain != null && ball != null)
                {
                    Vector3 hand = (boss != null ? boss.transform.position : c.At) + Vector3.up * 2f;
                    SetChain(chain, hand, ball.transform.parent.position);
                }
                if (t < fall) return true;
                // 떨어졌어요
                if (shadow != null) Destroy(shadow.gameObject);
                if (ball != null) Destroy(ball.transform.parent.gameObject);
                if (chain != null) Destroy(chain.gameObject);
                Crack(c.At, c.Radius * 2.3f);
                for (int i = 0; i < 7; i++) Debris(c.At, UnityEngine.Random.Range(0.2f, 0.4f), new Color(0.6f, 0.58f, 0.55f));
                return false;
            });
        }

        // ── 물기둥

        void Geyser(IronDragonBrain.FxCue c)
        {
            float fall = Mathf.Max(0.05f, c.Time);
            var ripple = Flat(_ripple, c.At, c.Radius * 1.2f, 0f, 0.04f);
            Vector3 rippleUnit = ripple != null ? ripple.transform.localScale : Vector3.one;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (ripple != null) // 물결이 커지며 끓어요
                {
                    ripple.transform.localScale = rippleUnit * Mathf.Lerp(0.6f, 1.8f, Mathf.Clamp01(t / fall));
                    ripple.transform.Rotate(0f, 0f, 90f * dt, Space.Self);
                    ripple.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.4f, 1f, t / fall));
                }
                if (t < fall) return true;
                if (ripple != null) Destroy(ripple.gameObject);
                Pillar(c.At, c.Radius);
                return false;
            });
        }

        void Pillar(Vector3 at, float radius)
        {
            var sr = Upright(_splash, at, radius * 1.8f, 7, bottom: true);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float up = t < 0.15f ? t / 0.15f : Mathf.Lerp(1f, 0.2f, (t - 0.15f) / 0.5f); // 솟구쳤다 무너져요
                sr.transform.localScale = new Vector3(unit.x * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t / 0.15f)), unit.y * 2.6f * Mathf.Max(0.05f, up), unit.z);
                sr.transform.localPosition = Vector3.up * _splash.bounds.size.y * sr.transform.localScale.y * 0.5f;
                sr.color = new Color(1f, 1f, 1f, t < 0.4f ? 1f : Mathf.Clamp01(1f - (t - 0.4f) / 0.25f));
                if (t < 0.65f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        // ── 사슬 당기기

        void Hook(IronDragonBrain boss, IronDragonBrain.FxCue c)
        {
            Vector3 dir = c.Dir; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            var chain = Chain();
            var ball = Upright(_rock, c.At, 0.45f, 7);
            if (ball != null) ball.color = Iron;
            Transform target = c.Hooked && _player != null ? _player.transform : null;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                Vector3 hand = (boss != null ? boss.transform.position : c.At) + Vector3.up * 1.6f;
                Vector3 tip;
                if (t < 0.12f) tip = hand + dir * (c.Length * t / 0.12f);
                else if (target != null) tip = target.position + Vector3.up * 0.9f;
                else tip = hand + dir * (c.Length * Mathf.Clamp01(1f - (t - 0.15f) / 0.3f));
                if (chain != null) SetChain(chain, hand, tip);
                if (ball != null) { ball.transform.parent.position = tip; ball.transform.Rotate(0f, 0f, 900f * dt, Space.Self); }
                if (t < (target != null ? 0.55f : 0.45f)) return true;
                if (chain != null) Destroy(chain.gameObject);
                if (ball != null) Destroy(ball.transform.parent.gameObject);
                return false;
            });
        }

        // ── 유성추 휘두르기

        void Swing(IronDragonBrain boss, IronDragonBrain.FxCue c)
        {
            Vector3 dir = c.Dir; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            float half = Mathf.Max(30f, c.Angle) * 0.5f, reach = Mathf.Max(2f, c.Radius) * 0.9f;
            var chain = Chain();
            var ball = Upright(_rock, c.At, 0.9f, 7);
            if (ball != null) ball.color = Iron;
            float t = 0f, last = -half;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / 0.28f);
                float ang = Mathf.Lerp(-half, half, 1f - (1f - k) * (1f - k));
                Vector3 hand = (boss != null ? boss.transform.position : c.At) + Vector3.up * 1.6f;
                Vector3 at = hand + Quaternion.Euler(0f, ang, 0f) * dir * reach + Vector3.down * 0.9f;
                if (ball != null) { ball.transform.parent.position = at; ball.transform.Rotate(0f, 0f, 1000f * dt, Space.Self); }
                if (chain != null) SetChain(chain, hand, at);
                if (Mathf.Abs(ang - last) > 18f) { last = ang; Dust(new Vector3(at.x, 0f, at.z)); } // 휩쓸고 간 자리 먼지
                if (k < 1f) return true;
                if (chain != null) Destroy(chain.gameObject);
                if (ball != null) Destroy(ball.transform.parent.gameObject);
                return false;
            });
        }

        // ── 2페이즈: 배가 부서져요

        void Wreck(IronDragonBrain.FxCue c)
        {
            Crack(c.At, c.Radius * 2.6f);
            for (int i = 0; i < 16; i++) Debris(c.At + Vector3.up * 0.5f, UnityEngine.Random.Range(0.3f, 0.65f), Wood);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Pillar(c.At + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * c.Radius * 0.8f, 0.9f);
            }
        }

        // ── 물길: 출렁이는 물 표면

        void DressWater()
        {
            _dressed.RemoveWhere(g => g == null);
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 확인해요
            {
                if (root.name != "물길" || _dressed.Contains(root)) continue;
                _dressed.Add(root);
                var source = root.GetComponent<Renderer>();
                if (source != null) source.enabled = false;
                float radius = root.transform.localScale.x * 0.5f;
                var whirl = Flat(_whirl, root.transform.position, radius * 2.1f, UnityEngine.Random.Range(0f, 360f), 0.03f);
                var ripple = Flat(_ripple, root.transform.position, radius * 1.6f, 0f, 0.035f);
                if (whirl != null) whirl.color = new Color(0.75f, 0.85f, 0.85f, 0.75f); // 잔잔하게 옅게
                var go = root;
                float seed = UnityEngine.Random.Range(0f, 10f);
                Vector3 rippleUnit = ripple != null ? ripple.transform.localScale : Vector3.one;
                _live.Add(dt =>
                {
                    if (go == null || !go.activeInHierarchy)
                    {
                        if (whirl != null) Destroy(whirl.gameObject);
                        if (ripple != null) Destroy(ripple.gameObject);
                        return false;
                    }
                    if (whirl != null) whirl.transform.Rotate(0f, 0f, 12f * dt, Space.Self); // 천천히 흘러요
                    if (ripple != null)
                    {
                        float p = Mathf.Repeat(Time.time * 0.5f + seed, 1f); // 물결이 번져요
                        ripple.transform.localScale = rippleUnit * Mathf.Lerp(0.4f, 1.2f, p);
                        ripple.color = new Color(1f, 1f, 1f, 0.7f * (1f - p));
                    }
                    return true;
                });
            }
        }

        // ── 도움

        LineRenderer Chain()
        {
            if (_chainMat == null) return null;
            var lr = new GameObject("Chain").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = _chainMat; // 길이마다 고리 되풀이 배율이 달라서 복사본
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.widthMultiplier = 0.18f;
            lr.startColor = lr.endColor = new Color(0.4f, 0.4f, 0.42f);
            lr.sortingOrder = 6;
            return lr;
        }

        static void SetChain(LineRenderer lr, Vector3 a, Vector3 b)
        {
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.material.mainTextureScale = new Vector2(Vector3.Distance(a, b) / 3f, 1f); // 고리 12개가 3칸마다
        }

        void Crack(Vector3 at, float size)
        {
            var sr = Flat(_crack, at, 1f, UnityEngine.Random.Range(0f, 360f), 0.05f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            sr.color = new Color(0.7f, 0.68f, 0.64f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.localScale = unit * Mathf.Lerp(size * 0.3f, size, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f));
                sr.color = new Color(0.7f, 0.68f, 0.64f, t < 0.4f ? 1f : Mathf.Clamp01(1f - (t - 0.4f) / 0.7f));
                if (t < 1.1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Debris(Vector3 at, float size, Color color)
        {
            var sr = Upright(_rock, at, size, 6);
            if (sr == null) return;
            sr.color = color;
            var pivot = sr.transform.parent;
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var v = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(2f, 4.5f), UnityEngine.Random.Range(3.5f, 6.5f), Mathf.Sin(a) * UnityEngine.Random.Range(2f, 4.5f));
            float t = 0f, spin = UnityEngine.Random.Range(-700f, 700f);
            bool landed = false;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (!landed)
                {
                    v += Vector3.down * 14f * dt;
                    pivot.position += v * dt;
                    sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                    if (pivot.position.y <= size * 0.4f) { landed = true; t = 0f; }
                    return true;
                }
                sr.color = new Color(color.r, color.g, color.b, 1f - Mathf.Clamp01((t - 0.5f) / 0.6f));
                if (t < 1.1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        void Dust(Vector3 at)
        {
            var sr = Flat(_ripple, at, 0.9f, UnityEngine.Random.Range(0f, 360f), 0.04f);
            if (sr == null) return;
            sr.color = new Color(0.6f, 0.55f, 0.5f, 0.6f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.color = new Color(0.6f, 0.55f, 0.5f, 0.6f * (1f - t / 0.5f));
                if (t < 0.5f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 카메라를 보는 받침(부모) 안의 그림 — 그림은 그 안에서 돌릴 수 있어요. bottom이면 아래 끝이 at
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
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
            _splash = Resources.Load<Sprite>("RiverFx/Splash");
            _whirl = Resources.Load<Sprite>("RiverFx/Whirlpool");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _crack = Resources.Load<Sprite>("SkillFx/QuakeRing");
            var chainTex = Resources.Load<Texture2D>("RiverFx/ChainTile");
            if (chainTex != null)
            {
                _chainMat = new Material(Shader.Find("Sprites/Default"));
                _chainMat.mainTexture = chainTex;
            }
        }
    }
}
