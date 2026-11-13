using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 탁류 이펙트 (장강 고수 탁랑객 · 비급 탁랑참) — 그림만, 판정·수치는 게임 코드 그대로.
    //   탁랑참 파도 검기 (PlayerController.SkillCue WaveSlash): 물결 파도가 앞으로 밀려 나가고 지나간 길에 물보라가 튀어요.
    //   탁류 소용돌이 (비도, IsVortexing · VortexCenter): 반경 1.8칸 물살 소용돌이가 돌며 물보라가 휘감겨요.
    //   탁랑객: 물결 검기("물결 검기" 화살)는 얇은 상자 대신 날아가는 물결 초승달, 소용돌이(IsWhirling)는 발밑 반경 4칸 물살,
    //           탁류 보법(AttackName "탁류 보법")으로 미끄러질 때 물자국이 남아요.
    // 그림: RiverFx(WaveCrest · Whirlpool · Splash · Ripple).
    public class InkTakrangFx : MonoBehaviour
    {
        static InkTakrangFx _host;
        static Sprite _wave, _whirl, _splash, _ripple;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GameObject> _dressed = new HashSet<GameObject>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>(); // 이름을 이미 확인한 씬 루트
        readonly Dictionary<TakrangBrain, (SpriteRenderer whirl, Vector3 last, float trail)> _masters = new Dictionary<TakrangBrain, (SpriteRenderer, Vector3, float)>();
        PlayerController _player;
        SpriteRenderer _playerVortex;
        EnemyDirector _director;
        float _scan, _spray;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            PlayerController.SkillCue -= OnSkill;
            PlayerController.SkillCue += OnSkill;
            if (FindAnyObjectByType<PlayerController>() != null) Host();
        }

        static InkTakrangFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Takrang Fx").AddComponent<InkTakrangFx>();
            return _host;
        }

        static void OnSkill(PlayerController p, PlayerController.SkillFxCue c)
        {
            if (c.Kind == PlayerController.SkillFxKind.WaveSlash) Host().WaveSlash(c);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            TickPlayerVortex(dt);
            TickMasters(dt);
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.05f; DressWaves(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 탁랑참: 파도가 밀려 나가요

        void WaveSlash(PlayerController.SkillFxCue c)
        {
            Vector3 dir = c.Dir; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            float length = Mathf.Max(1f, c.Radius), width = Mathf.Max(0.8f, c.Angle);
            var qi = new InkSwordQi(transform, c.From, dir, width * 1.6f, new Color(0.3f, 0.55f, 0.75f, 0.85f)); // 붓질 초승달 검기 (쪽빛 테)
            float t = 0f, travel = 0.3f, nextSpray = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / travel);
                Vector3 at = c.From + dir * (length * k);
                qi.Pose(at, dir, Mathf.Lerp(0.7f, 1.15f, k), k < 0.8f ? 1f : (1f - k) / 0.2f);
                if (k < 1f && t >= nextSpray) { nextSpray = t + 0.05f; SprayAt(at, 0.9f); } // 지나간 길에 물보라
                if (k < 1f) return true;
                qi.Destroy();
                return false;
            });
        }

        // ── 비도 탁류 소용돌이

        void TickPlayerVortex(float dt)
        {
            bool on = _player != null && _player.IsVortexing;
            if (on && _playerVortex == null) _playerVortex = Flat(_whirl, _player.VortexCenter, 1.8f * 2.2f, 0f, 0.045f);
            if (!on && _playerVortex != null) { Fade(_playerVortex, 0.3f); _playerVortex = null; }
            if (_playerVortex == null) return;
            var c = _player.VortexCenter;
            _playerVortex.transform.position = new Vector3(c.x, 0.045f, c.z);
            _playerVortex.transform.Rotate(0f, 0f, -360f * dt, Space.Self);
            _spray -= dt;
            if (_spray <= 0f) { _spray = 0.08f; SprayRing(c, 1.8f); }
        }

        // ── 탁랑객

        void TickMasters(float dt)
        {
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
                if (e is TakrangBrain m && !m.IsDead && !_masters.ContainsKey(m)) _masters[m] = (null, m.transform.position, 0f);
            var keys = new List<TakrangBrain>(_masters.Keys);
            foreach (var m in keys)
            {
                var (whirl, last, trail) = _masters[m];
                if (m == null || m.IsDead)
                {
                    if (whirl != null) Destroy(whirl.gameObject);
                    _masters.Remove(m);
                    continue;
                }
                Vector3 at = m.transform.position;
                // 소용돌이: 발밑 반경 4칸 물살
                if (m.IsWhirling && whirl == null) whirl = Flat(_whirl, at, 4f * 2.1f, 0f, 0.045f);
                if (!m.IsWhirling && whirl != null) { Fade(whirl, 0.35f); whirl = null; }
                if (whirl != null)
                {
                    whirl.transform.position = new Vector3(at.x, 0.045f, at.z);
                    whirl.transform.Rotate(0f, 0f, -300f * dt, Space.Self);
                    if (UnityEngine.Random.value < dt * 14f) SprayRing(at, 3.5f);
                }
                // 탁류 보법: 빠르게 미끄러지는 동안 물자국
                Vector3 moved = at - last; moved.y = 0f;
                trail -= dt;
                if (m.AttackName == "탁류 보법" && moved.magnitude > 4f * dt && trail <= 0f)
                {
                    trail = 0.05f;
                    Wet(at);
                }
                _masters[m] = (whirl, at, trail);
            }
        }

        // ── 물결 검기 ("물결 검기" 화살): 얇은 상자 대신 날아가는 물결 초승달

        void DressWaves()
        {
            _dressed.RemoveWhere(g => g == null);
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 확인해요
            {
                if (root.name != "물결 검기" || _dressed.Contains(root)) continue;
                _dressed.Add(root);
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var go = root;
                Vector3 last = go.transform.position, dir = go.transform.forward;
                var qi = new InkSwordQi(transform, last, dir, 1.6f, new Color(0.22f, 0.36f, 0.62f, 0.85f)); // 붓질 초승달 물결 검기 (쪽빛 테)
                if (!qi.Valid) continue;
                _live.Add(dt =>
                {
                    if (go == null || !go.activeInHierarchy) { qi.Destroy(); return false; }
                    var p = go.transform.position;
                    if ((p - last).sqrMagnitude > 1e-5f) dir = p - last; // 날아가는 쪽
                    last = p;
                    qi.Pose(p, dir, 1f, 1f);
                    return true;
                });
            }
        }

        // ── 도움

        void SprayAt(Vector3 at, float size)
        {
            var sr = Upright(_splash, at + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f)), size * UnityEngine.Random.Range(0.6f, 1f), 6, bottom: true);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.localScale = new Vector3(unit.x, unit.y * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t / 0.12f)), unit.z);
                sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((t - 0.15f) / 0.25f));
                if (t < 0.4f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        void SprayRing(Vector3 center, float radius)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            SprayAt(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * UnityEngine.Random.Range(0.6f, 1f), 0.6f);
        }

        void Wet(Vector3 at)
        {
            var sr = Flat(_ripple, at, UnityEngine.Random.Range(0.7f, 1.1f), UnityEngine.Random.Range(0f, 360f), 0.03f);
            if (sr == null) return;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.color = new Color(0.55f, 0.62f, 0.68f, 0.7f * (1f - Mathf.Clamp01((t - 0.6f) / 1.2f)));
                if (t < 1.8f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Fade(SpriteRenderer sr, float time)
        {
            float t = 0f;
            Vector3 unit = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.Rotate(0f, 0f, -400f * dt, Space.Self);
                sr.transform.localScale = unit * (1f - 0.3f * t / time);
                sr.color = new Color(1f, 1f, 1f, 1f - t / time);
                if (t < time) return true;
                Destroy(sr.gameObject);
                return false;
            });
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
            _wave = Resources.Load<Sprite>("RiverFx/WaveCrest");
            _whirl = Resources.Load<Sprite>("RiverFx/Whirlpool");
            _splash = Resources.Load<Sprite>("RiverFx/Splash");
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
        }
    }
}
