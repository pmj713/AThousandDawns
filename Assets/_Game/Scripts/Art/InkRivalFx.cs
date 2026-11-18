using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 차원 겹침 적끼리 싸움 · 협력 (그림만 — 누가 누구를 노리는지는 게임 코드 EnemyBrain.Rival.cs 그대로).
    //   적대 (RivalChanged → Rival): 노리기 시작하면 머리 위 "!"가 튀어 올랐다 사라지고 (작게, 무협 = 호박빛 · 판타지 = 연보라, 짙은 테두리),
    //     노리는 동안 그 적 쪽으로 짧은 적대선이 뻗어요 (무협 = 먹선, 판타지 = 붉은 금빛선).
    //   적끼리 피격 (RivalHit): 먹 튐과 유리 조각이 부딪치는 충돌 빛 — 먹 쪽이 무협 적 쪽을 향해요 (플레이어 타격의 먹 베기와 구분).
    //   적이 적을 쓰러뜨림 (RivalHit 뒤 맞은 적이 쓰러짐): 그 자리에 쓰러뜨린 쪽 진영 표시가 쾅 (무협 = 붉은 낙관, 판타지 = 유리 문장).
    //   협력 (Allied): 표시 없어요 (발밑 원 · 보랏빛 끈은 정예 고리와 헷갈려서 뺐어요)
    // 그림: FantasyFx (BangInk · BangGlass · Clash · Crest), TitleArt/Seal.
    public class InkRivalFx : MonoBehaviour
    {

        static InkRivalFx _host;
        static Sprite _bangInk, _bangGlass, _clash, _crest, _seal;
        static Material _lineMat;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly Dictionary<EnemyBrain, LineRenderer> _rivalLines = new Dictionary<EnemyBrain, LineRenderer>();
        readonly Dictionary<EnemyBrain, float> _heads = new Dictionary<EnemyBrain, float>(); // 머리 높이 (처음 한 번만 잼)
        readonly List<EnemyBrain> _gone = new List<EnemyBrain>();
        EnemyDirector _director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.RivalChanged -= OnRivalChanged;
            EnemyBrain.RivalChanged += OnRivalChanged;
            EnemyBrain.RivalHit -= OnRivalHit;
            EnemyBrain.RivalHit += OnRivalHit;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkRivalFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Rival Fx").AddComponent<InkRivalFx>();
            return _host;
        }

        static void OnRivalChanged(EnemyBrain e, EnemyBrain rival) { if (e != null && rival != null) Host().Bang(e); }
        static void OnRivalHit(EnemyBrain attacker, EnemyBrain victim, Vector3 at) => Host().Hit(attacker, victim, at);

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            TickRivalLines();
            if (_heads.Count > 128) _heads.Clear(); // 쓰러진 적 머리 높이 정리 (다시 재면 그만)
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        static bool Fantasy(EnemyBrain e) => e != null && e.Faction == EnemyFaction.Fantasy;

        float Head(EnemyBrain e)
        {
            if (_heads.TryGetValue(e, out float h)) return h;
            h = 2f;
            var renderers = e.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                float top = float.MinValue;
                foreach (var r in renderers) if (!(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer)) top = Mathf.Max(top, r.bounds.max.y);
                if (top > float.MinValue) h = Mathf.Clamp(top - e.transform.position.y, 0.8f, 4f);
            }
            _heads[e] = h;
            return h;
        }

        // ── 적대: 머리 위 "!" + 짧은 적대선

        void Bang(EnemyBrain e)
        {
            // (플레이테스트: 너무 크고 색이 튀어요) → 머리 위에 작게, 흰 마스크 ! 하나를 진영 색으로 + 짙은 테두리 한 겹 (밝은 바닥에서도 읽히게)
            var sprite = _bangInk;
            var tint = Fantasy(e) ? new Color(0.8f, 0.58f, 1f) : new Color(1f, 0.78f, 0.22f); // 판타지 연보라 · 무협 호박빛
            if (sprite == null) return;
            var sr = Billboard(sprite, e.transform.position + Vector3.up * (Head(e) + 0.25f), 0.3f, 12);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            var rim = new GameObject("Rim").AddComponent<SpriteRenderer>();
            rim.transform.SetParent(sr.transform, false);
            rim.transform.localScale = Vector3.one * 1.25f;
            rim.sprite = sprite;
            rim.sortingOrder = 11;
            Vector3 unit = sr.transform.localScale;
            float t = 0f, head = Head(e);
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (e != null) pivot.position = e.transform.position + Vector3.up * (head + 0.25f + 0.12f * Mathf.Clamp01(t / 0.9f));
                float pop = t < 0.12f ? Mathf.Lerp(0.3f, 1.25f, t / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.12f) / 0.1f));
                sr.transform.localScale = unit * pop;
                sr.color = new Color(tint.r, tint.g, tint.b, t < 0.65f ? 1f : Mathf.Clamp01(1f - (t - 0.65f) / 0.3f));
                rim.color = new Color(0.18f, 0.1f, 0.06f, 0.8f * sr.color.a);
                if (t < 0.95f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        void TickRivalLines()
        {
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                    if (e != null && !e.IsDead && e.Rival != null && !_rivalLines.ContainsKey(e))
                        _rivalLines[e] = Line(Fantasy(e) ? new Color(0.95f, 0.35f, 0.15f) : new Color(1f, 0.78f, 0.2f), Fantasy(e) ? new Color(1f, 0.8f, 0.3f) : new Color(0.62f, 0.08f, 0.07f), 0.07f, 2);
            _gone.Clear();
            foreach (var kv in _rivalLines)
            {
                var e = kv.Key;
                var lr = kv.Value;
                if (lr == null || e == null || e.IsDead || e.Rival == null) { if (lr != null) Destroy(lr.gameObject); _gone.Add(e); continue; }
                Vector3 a = e.transform.position, b = e.Rival.transform.position;
                Vector3 d = b - a;
                d.y = 0f;
                float len = d.magnitude;
                if (len < 0.3f) { lr.enabled = false; continue; }
                lr.enabled = true;
                Vector3 dir = d / len;
                float reach = Mathf.Min(1.4f, len * 0.45f) * (0.9f + 0.1f * Mathf.Sin(Time.time * 12f)); // 짧게 뻗어 떨려요
                lr.SetPosition(0, a + Vector3.up * 1f + dir * 0.4f);
                lr.SetPosition(1, a + Vector3.up * 1f + dir * (0.4f + reach));
            }
            foreach (var e in _gone) _rivalLines.Remove(e);
        }

        // ── 적끼리 피격 · 쓰러뜨림

        void Hit(EnemyBrain attacker, EnemyBrain victim, Vector3 at)
        {
            Vector3 p = at + Vector3.up * (victim != null ? Head(victim) * 0.55f : 1f);
            var sr = Billboard(_clash, p, 1.3f, 11);
            if (sr != null)
            {
                // 그림 왼쪽 = 먹, 오른쪽 = 유리: 먹 쪽이 무협 적을 향하게
                var cam = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                bool attackerRight = attacker != null && Vector3.Dot(attacker.transform.position - at, cam) > 0f;
                sr.flipX = attackerRight == !Fantasy(attacker);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
                var pivot = sr.transform.parent;
                Vector3 unit = sr.transform.localScale;
                float t = 0f;
                _live.Add(dt =>
                {
                    if (sr == null) return false;
                    t += dt;
                    sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1.15f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.1f), 3f));
                    sr.color = new Color(1f, 1f, 1f, t < 0.1f ? 1f : Mathf.Clamp01(1f - (t - 0.1f) / 0.25f));
                    if (t < 0.35f) return true;
                    Destroy(pivot.gameObject);
                    return false;
                });
            }
            if (victim != null && (victim.IsDead || victim.KilledByRival)) Stamp(Fantasy(attacker) ? _crest : _seal, victim.transform.position, Head(victim));
        }

        void Stamp(Sprite sprite, Vector3 at, float head)
        {
            var sr = Billboard(sprite, at + Vector3.up * (head * 0.6f + 0.3f), 0.9f, 13);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / 0.12f);
                sr.transform.localScale = unit * Mathf.Lerp(1.9f, 1f, k * k); // 쾅
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                sr.color = new Color(1f, 1f, 1f, t < 0.12f ? k : t < 0.85f ? 1f : Mathf.Clamp01(1f - (t - 0.85f) / 0.4f));
                if (t < 1.25f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 도움

        LineRenderer Line(Color edge, Color core, float width, int points)
        {
            var go = new GameObject("Rival Line");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, points > 2 ? 1f : 0.2f));
            lr.colorGradient = new Gradient
            {
                colorKeys = points > 2
                    ? new[] { new GradientColorKey(edge, 0f), new GradientColorKey(core, 0.5f), new GradientColorKey(edge, 1f) }
                    : new[] { new GradientColorKey(edge, 0f), new GradientColorKey(core, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(points > 2 ? 0.9f : 0.3f, 1f) },
            };
            lr.numCapVertices = 2;
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
            _bangInk = Resources.Load<Sprite>("FantasyFx/BangInk");
            _bangGlass = Resources.Load<Sprite>("FantasyFx/BangGlass");
            _clash = Resources.Load<Sprite>("FantasyFx/Clash");
            _crest = Resources.Load<Sprite>("FantasyFx/Crest");
            var seal = Resources.Load<Texture2D>("TitleArt/Seal"); // 텍스처로 들어와 있어서 스프라이트로 감싸요
            if (seal != null) _seal = Sprite.Create(seal, new Rect(0f, 0f, seal.width, seal.height), new Vector2(0.5f, 0.5f), 100f);
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
