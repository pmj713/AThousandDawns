using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 장강 수채 적 이펙트 (그림만 — 판정·수치는 게임 코드 그대로).
    //   작살수 (HarpoonerBrain.Thrust): 작살이 밧줄을 끌며 뻗었다 돌아와요. 걸렸으면 밧줄이 플레이어에게 붙은 채 감겨요.
    //   쇠닻 거한: 던질 때 닻 그림이 거한에게서 날아가 꽂히고(0.12초), 거한이 끌려가는 동안 사슬이 이어져요. 꽂힌 닻 모양은 메인 아트의 3D 닻(InkRunProps)이 맡아요.
    //   수귀: 숨어 있는 동안 물결이 퍼지고, 튀어나올 때 물보라, 끌고 가는 동안 바닥에 젖은 물 자국이 남아요.
    //   등패수: 멀리서 온 공격을 정면에서 다 막으면(RattanBrain.FullBlock) 방패 앞에서 불똥이 튀어요. 굴리는 방패("등패")는 등나무 방패 그림이 굴러가요.
    //   깃발수 지휘: 지휘받은 적(IsHastened) 발밑에 붉은 기운이 일렁이고, 움직이면 뒤로 붉은 속도선이 남아요.
    // 그림: RiverFx(Harpoon · Anchor · Chain · Ripple · Splash · Rattan), PouchFx(Ring · Glint), HudArt/HpStroke(밧줄·속도선 붓 획).
    public class InkRiverEnemyFx : MonoBehaviour
    {
        static InkRiverEnemyFx _host;
        static Sprite _harpoon, _anchor, _ripple, _splash, _rattan, _ring, _glint;
        static Texture2D _chainTex, _brushTex;
        static Material _ropeMat, _chainMat;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GameObject> _dressed = new HashSet<GameObject>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>(); // 이름을 이미 확인한 씬 루트
        readonly Dictionary<WaterGhostBrain, Ghost> _ghosts = new Dictionary<WaterGhostBrain, Ghost>();
        readonly Dictionary<EnemyBrain, Haste> _haste = new Dictionary<EnemyBrain, Haste>();
        EnemyDirector _director;
        PlayerController _player;
        float _scan;

        class Ghost { public SpriteRenderer Ripple; public bool WasSubmerged = true; public float Trail; }
        class Haste { public SpriteRenderer Ring; public Vector3 Last; public float Spawn; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            HarpoonerBrain.Thrust -= OnThrust;
            HarpoonerBrain.Thrust += OnThrust;
            RattanBrain.FullBlock -= OnBlock;
            RattanBrain.FullBlock += OnBlock;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
        }

        static InkRiverEnemyFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink River Enemy Fx").AddComponent<InkRiverEnemyFx>();
            return _host;
        }

        static void OnThrust(HarpoonerBrain h, Vector3 at, Vector3 dir, float reach, bool pulled) => Host().Harpoon(h, at, dir, reach, pulled);
        static void OnBlock(RattanBrain r, Vector3 attacker) => Host().Block(r, attacker);

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            TickEnemies(dt);
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.1f; DressSpawned(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 작살수: 작살이 밧줄을 끌며 뻗었다 돌아와요

        void Harpoon(HarpoonerBrain h, Vector3 at, Vector3 dir, float reach, bool pulled)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Transform bone = h != null ? FindDeep(h.transform, "RightHand") : null; // 작살꾼 오른손 뼈 (모델 Visual 안). 찌르는 동안 손의 작살은 메인 아트가 숨겨요
            var head = Upright(_harpoon, at + Vector3.up * 1f, 1.1f, 7);
            var rope = Line(_ropeMat, new Color(0.35f, 0.27f, 0.18f), 0.07f, 6);
            Transform target = pulled && _player != null ? _player.transform : null;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                Vector3 hand = bone != null ? bone.position : (h != null ? h.transform.position : at) + Vector3.up * 1f; // 손에서 뻗어 나가요
                Vector3 tip;
                if (t < 0.1f) tip = hand + dir * (reach * (t / 0.1f));                                        // 뻗어요
                else if (target != null) tip = target.position + Vector3.up * 0.9f;                         // 걸렸으면 플레이어에 붙어 끌려와요
                else tip = hand + dir * (reach * Mathf.Clamp01(1f - (t - 0.15f) / 0.25f));                 // 놓쳤으면 감겨 돌아와요
                if (head != null)
                {
                    head.transform.parent.position = tip;
                    var screen = Camera.main != null ? Camera.main.WorldToScreenPoint(tip) - Camera.main.WorldToScreenPoint(hand) : Vector3.right;
                    head.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg); // 화면에서 날아가는 쪽으로
                }
                if (rope != null) { rope.SetPosition(0, hand); rope.SetPosition(1, tip); }
                bool done = target != null ? t > 0.5f : t > 0.42f;
                if (!done) return true;
                if (head != null) Destroy(head.transform.parent.gameObject);
                if (rope != null) Destroy(rope.gameObject);
                return false;
            });
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name || root.name.EndsWith(":" + name)) return root;
            foreach (Transform child in root) { var t = FindDeep(child, name); if (t != null) return t; }
            return null;
        }

        // ── 등패수: 정면에서 다 막으면 불똥이 튀어요

        float _lastBlock;
        void Block(RattanBrain r, Vector3 attacker)
        {
            if (r == null || Time.time - _lastBlock < 0.06f) return;
            _lastBlock = Time.time;
            Vector3 to = attacker - r.transform.position; to.y = 0f;
            to = to.sqrMagnitude > 1e-4f ? to.normalized : r.transform.forward;
            Vector3 at = r.transform.position + to * 0.7f + Vector3.up * 1f;
            var flash = Upright(_ring, at, 0.9f, 8);
            if (flash != null)
            {
                float t = 0f;
                Vector3 unit = flash.transform.localScale;
                _live.Add(dt =>
                {
                    if (flash == null) return false;
                    t += dt;
                    flash.transform.localScale = unit * Mathf.Lerp(0.4f, 1.3f, t / 0.18f);
                    flash.color = new Color(1f, 0.9f, 0.6f, 1f - t / 0.18f);
                    if (t < 0.18f) return true;
                    Destroy(flash.transform.parent.gameObject);
                    return false;
                });
            }
            for (int i = 0; i < 7; i++)
            {
                var v = (to + UnityEngine.Random.insideUnitSphere * 0.8f) * UnityEngine.Random.Range(3f, 5f);
                v.y = Mathf.Abs(v.y) + 1f;
                Spark(at, v);
            }
        }

        void Spark(Vector3 at, Vector3 v)
        {
            var sr = Upright(_glint, at, UnityEngine.Random.Range(0.15f, 0.28f), 8);
            if (sr == null) return;
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

        // ── 게임이 만든 물건 입히기: 쇠닻 기둥 · 굴리는 방패

        void DressSpawned()
        {
            _dressed.RemoveWhere(g => g == null);
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 확인해요
            {
                if (_dressed.Contains(root)) continue;
                if (root.name == "쇠닻") { _dressed.Add(root); DressAnchor(root); }
                else if (root.name == "등패") { _dressed.Add(root); DressRattan(root); }
            }
        }

        void DressAnchor(GameObject post)
        {
            var source = post.GetComponent<Renderer>();
            if (source != null) source.enabled = false;
            Vector3 stick = new Vector3(post.transform.position.x, 0f, post.transform.position.z);
            var giant = Nearest<AnchorBrain>(stick);
            Vector3 from = giant != null ? giant.transform.position + Vector3.up * 1.4f : stick + Vector3.up * 1.4f;
            var anchor = Upright(_anchor, from, 1.3f, 6, bottom: true);
            var chain = Line(_chainMat, new Color(0.3f, 0.3f, 0.32f), 0.16f, 5, tile: true);
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (post == null) // 3초가 지나 사라졌어요
                {
                    if (anchor != null) Destroy(anchor.transform.parent.gameObject);
                    if (chain != null) Destroy(chain.gameObject);
                    return false;
                }
                if (anchor != null) // 날아가 꽂혀요 — 꽂힌 뒤 모양은 메인 아트의 3D 닻(InkRunProps.DressAnchor)이 맡아서 그림은 지워요
                {
                    float k = Mathf.Clamp01(t / 0.12f);
                    anchor.transform.parent.position = Vector3.Lerp(from, stick, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.6f);
                    if (k >= 1f) { Destroy(anchor.transform.parent.gameObject); anchor = null; }
                }
                if (chain != null) // 거한이 끌려오는 동안 사슬이 이어져요
                {
                    Vector3 hand = giant != null ? giant.transform.position + Vector3.up * 1.4f : from;
                    Vector3 end = anchor != null ? anchor.transform.parent.position + Vector3.up * 0.9f : stick + Vector3.up * 1.35f; // 꽂힌 뒤엔 3D 닻 고리 높이 (메인 아트: 바닥 위 약 1.35)
                    chain.SetPosition(0, hand);
                    chain.SetPosition(1, end);
                    chain.material.mainTextureScale = new Vector2(Vector3.Distance(hand, end) / 3f, 1f); // 사슬 그림 한 줄(고리 12개)이 3칸마다
                    bool arrived = giant == null || (giant.transform.position - stick).magnitude < 2f || t > 1.4f;
                    if (arrived) { Destroy(chain.gameObject); chain = null; }
                }
                return true;
            });
        }

        void DressRattan(GameObject disc)
        {
            var source = disc.GetComponent<Renderer>();
            if (source != null) source.enabled = false;
            var shield = Flat(_rattan, disc.transform.position, 1f, 0f, disc.transform.position.y); // 바닥과 나란히 눕혀 앞면이 위로
            if (shield == null) return;
            _live.Add(dt =>
            {
                if (disc == null) { if (shield != null) Destroy(shield.gameObject); return false; }
                shield.transform.position = disc.transform.position;
                shield.transform.Rotate(0f, 0f, -720f * dt, Space.Self); // 수평으로 팽이처럼 돌며 날아가요
                return true;
            });
        }

        // ── 수귀 · 깃발수 지휘 (적 상태를 읽어요)

        void TickEnemies(float dt)
        {
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
            {
                if (e == null || e.IsDead) continue;
                if (e is WaterGhostBrain g) TickGhost(g, dt);
                if (e.IsHastened && !_haste.ContainsKey(e))
                {
                    var ring = Flat(_ring, e.transform.position, 1.6f, 0f, 0.05f);
                    _haste[e] = new Haste { Ring = ring, Last = e.transform.position };
                }
            }
            CleanGhosts();
            TickHaste(dt);
        }

        void TickGhost(WaterGhostBrain g, float dt)
        {
            if (!_ghosts.TryGetValue(g, out var s)) _ghosts[g] = s = new Ghost();
            Vector3 at = g.transform.position;
            if (g.Submerged)
            {
                if (s.Ripple == null) s.Ripple = Flat(_ripple, at, 1.6f, 0f, 0.04f);
                if (s.Ripple != null)
                {
                    s.Ripple.transform.position = new Vector3(at.x, 0.04f, at.z);
                    float pulse = Mathf.Repeat(Time.time * 0.9f, 1f); // 물결이 퍼져요
                    s.Ripple.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.3f, pulse) * (1.6f / Mathf.Max(0.01f, _ripple.bounds.size.x));
                    s.Ripple.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.4f, pulse));
                }
            }
            else
            {
                if (s.WasSubmerged) Splash(at); // 튀어나왔어요
                if (s.Ripple != null) { Destroy(s.Ripple.gameObject); s.Ripple = null; }
            }
            s.WasSubmerged = g.Submerged;
            if (g.Dragging) // 끌고 가는 길에 젖은 물 자국
            {
                s.Trail -= dt;
                if (s.Trail <= 0f)
                {
                    s.Trail = 0.12f;
                    Vector3 p = _player != null ? _player.transform.position : at;
                    Puddle(p);
                }
            }
        }

        void CleanGhosts()
        {
            List<WaterGhostBrain> gone = null;
            foreach (var pair in _ghosts)
                if (pair.Key == null || pair.Key.IsDead)
                {
                    if (pair.Value.Ripple != null) Destroy(pair.Value.Ripple.gameObject);
                    (gone ??= new List<WaterGhostBrain>()).Add(pair.Key);
                }
            if (gone != null) foreach (var g in gone) _ghosts.Remove(g);
        }

        void Splash(Vector3 at)
        {
            var sr = Upright(_splash, at, 1.8f, 7, bottom: true);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.localScale = new Vector3(unit.x * Mathf.Lerp(0.6f, 1.1f, t / 0.5f), unit.y * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(t / 0.15f)), unit.z);
                sr.color = new Color(1f, 1f, 1f, t < 0.25f ? 1f : 1f - (t - 0.25f) / 0.3f);
                if (t < 0.55f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        void Puddle(Vector3 at)
        {
            var sr = Flat(_ripple, at, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0f, 360f), 0.03f);
            if (sr == null) return;
            float t = 0f;
            sr.color = new Color(0.55f, 0.62f, 0.68f, 0.7f); // 젖은 자국 (물빛 짙게)
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.color = new Color(0.55f, 0.62f, 0.68f, 0.7f * (1f - Mathf.Clamp01((t - 0.8f) / 1.4f)));
                if (t < 2.2f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void TickHaste(float dt)
        {
            List<EnemyBrain> gone = null;
            foreach (var pair in _haste)
            {
                var e = pair.Key;
                var h = pair.Value;
                if (e == null || e.IsDead || !e.IsHastened)
                {
                    if (h.Ring != null) Destroy(h.Ring.gameObject);
                    (gone ??= new List<EnemyBrain>()).Add(e);
                    continue;
                }
                Vector3 at = e.transform.position;
                if (h.Ring != null) // 발밑 붉은 기운
                {
                    h.Ring.transform.position = new Vector3(at.x, 0.05f, at.z);
                    h.Ring.transform.Rotate(0f, 0f, 120f * dt, Space.Self);
                    h.Ring.color = new Color(1f, 0.3f, 0.22f, 0.65f + 0.25f * Mathf.Sin(Time.time * 9f));
                }
                Vector3 moved = at - h.Last; moved.y = 0f;
                h.Last = at;
                h.Spawn -= dt;
                if (moved.magnitude > 0.02f && h.Spawn <= 0f) // 움직이면 뒤로 붉은 속도선
                {
                    h.Spawn = 0.07f;
                    Vector3 back = -moved.normalized;
                    Vector3 side = new Vector3(back.z, 0f, -back.x) * UnityEngine.Random.Range(-0.35f, 0.35f);
                    Vector3 a = at + Vector3.up * UnityEngine.Random.Range(0.5f, 1.4f) + side;
                    SpeedLine(a, a + back * UnityEngine.Random.Range(0.8f, 1.4f));
                }
            }
            if (gone != null) foreach (var e in gone) _haste.Remove(e);
        }

        void SpeedLine(Vector3 a, Vector3 b)
        {
            var lr = Line(_ropeMat, new Color(0.85f, 0.18f, 0.12f), 0.08f, 6);
            if (lr == null) return;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            float t = 0f;
            _live.Add(dt =>
            {
                if (lr == null) return false;
                t += dt;
                var c = new Color(0.85f, 0.18f, 0.12f, 1f - t / 0.25f);
                lr.startColor = lr.endColor = c;
                if (t < 0.25f) return true;
                Destroy(lr.gameObject);
                return false;
            });
        }

        // ── 도움

        T Nearest<T>(Vector3 at) where T : EnemyBrain
        {
            T best = null;
            float bestD = float.MaxValue;
            if (_director == null || _director.Enemies == null) return null;
            foreach (var e in _director.Enemies)
                if (e is T t && !t.IsDead)
                {
                    float d = (t.transform.position - at).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = t; }
                }
            return best;
        }

        LineRenderer Line(Material mat, Color color, float width, int order, bool tile = false)
        {
            var lr = new GameObject("Line").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = mat; // 사슬 타일 길이를 따로 바꾸려고 복사본
            lr.textureMode = LineTextureMode.Stretch; // 사슬은 재질 타일 배율(mainTextureScale)로 고리를 되풀이해요
            lr.alignment = LineAlignment.View;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = order;
            return lr;
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
            _harpoon = Resources.Load<Sprite>("RiverFx/Harpoon");
            _anchor = Resources.Load<Sprite>("RiverFx/Anchor");
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
            _splash = Resources.Load<Sprite>("RiverFx/Splash");
            _rattan = Resources.Load<Sprite>("RiverFx/Rattan");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _chainTex = Resources.Load<Texture2D>("RiverFx/ChainTile");
            _brushTex = Resources.Load<Texture2D>("HudArt/HpStroke");
            _ropeMat = new Material(Shader.Find("Sprites/Default"));
            if (_brushTex != null) _ropeMat.mainTexture = _brushTex;
            _chainMat = new Material(Shader.Find("Sprites/Default"));
            if (_chainTex != null) _chainMat.mainTexture = _chainTex;
        }
    }
}
