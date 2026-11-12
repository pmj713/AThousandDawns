using System;
using System.Collections.Generic;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 비급 초식 이펙트 (PlayerController.SkillCue 신호를 듣고 그림만 그려요 — 판정·수치는 게임 코드 그대로).
    //   죽림섬 (일반 · 비도+비급): 지나간 자리에 먹 붓 잔상과 옥빛 선이 그어졌다 바래요
    //   붕산권 충격파 (일반 · 비도+비급): 발밑으로 황토빛 먹 충격파 고리가 퍼져요
    //   금강장 보호막 (비도+비급): 돌아온 비도 수만큼 금빛 반짝이가 모여들고 금빛 고리가 오므라들어요
    //   금강장 장풍: 금빛 손바닥이 앞으로 날아가며 커지고, 바닥에 금빛 물결이 부채꼴로 퍼져요
    // 그림: Art/Env/SkillFx/Resources/SkillFx (QuakeRing · GoldPalm · GoldWave), PouchFx(Glint · Ring), HudArt/HpStroke(붓 획).
    public class InkSkillFx : MonoBehaviour
    {
        static InkSkillFx _host;
        static Sprite _quake, _palm, _wave, _glint, _ring, _rock;
        static Texture2D _brush;
        static Material _lineMat;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            PlayerController.SkillCue -= OnCue;
            PlayerController.SkillCue += OnCue;
        }

        static void OnCue(PlayerController player, PlayerController.SkillFxCue c)
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Skill Fx").AddComponent<InkSkillFx>();
            switch (c.Kind)
            {
                case PlayerController.SkillFxKind.FlashDash:
                    _host.FlashDash(c);
                    if (player != null && player.UsesDagger) _host.DaggerPluck(c.To); // 비도 순간이동: 비도를 뽑는 반짝임
                    break;
                case PlayerController.SkillFxKind.QuakeWave:
                    _host.QuakeWave(c);
                    if (player != null && player.UsesDagger && c.Radius < 1.9f) _host.DirtBurst(c.From); // 비도 솟구치기: 비도 자리마다 흙이 튀어요 (내 둘레 충격파는 반경 2라 빼요)
                    break;
                case PlayerController.SkillFxKind.PalmShield: _host.PalmShield(player, c); break;
                case PlayerController.SkillFxKind.PalmBlast: _host.PalmBlast(c); break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickHalo(dt);
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        // ── 죽림섬: 먹 붓 잔상 + 옥빛 선
        void FlashDash(PlayerController.SkillFxCue c)
        {
            Vector3 a = c.From + Vector3.up * 0.9f, b = c.To + Vector3.up * 0.9f;
            if ((b - a).sqrMagnitude < 0.04f) return;
            var ink = Line(a, b, new Color(0.08f, 0.07f, 0.06f), 0.75f, 0.15f, 0.06f);
            var jade = Line(a, b, new Color(0.3f, 0.75f, 0.55f), 0.28f, 0.06f, 0.07f);
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / 0.45f);
                Fade(ink, 1f - k * k);
                Fade(jade, 1f - k);
                if (ink != null) ink.SetPosition(0, Vector3.Lerp(a, b, k * 0.8f)); // 꼬리부터 걷혀요
                if (jade != null) jade.SetPosition(0, Vector3.Lerp(a, b, k * 0.9f));
                if (k < 1f) return true;
                if (ink != null) Destroy(ink.gameObject);
                if (jade != null) Destroy(jade.gameObject);
                return false;
            });
        }

        // ── 비도 순간이동: 도착한 비도 자리에서 은빛 반짝임 (비도를 뽑아 거둬요)
        void DaggerPluck(Vector3 at)
        {
            Vector3 p = at + Vector3.up * 0.4f;
            var flash = Upright(_glint, p, 0.9f);
            if (flash != null)
            {
                float t = 0f;
                Vector3 unit = flash.transform.localScale;
                flash.color = new Color(0.85f, 0.95f, 1f); // 은빛
                _live.Add(dt =>
                {
                    if (flash == null) return false;
                    t += dt;
                    flash.transform.localScale = unit * Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI) * 1.3f;
                    if (t < 0.25f) return true;
                    Destroy(flash.gameObject);
                    return false;
                });
            }
        }

        // ── 비도 솟구치기: 비도 자리에서 흙덩이가 튀어 올라요
        void DirtBurst(Vector3 at)
        {
            if (_rock == null) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.4f, 0.4f);
                var v = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(1f, 2.2f), UnityEngine.Random.Range(3f, 4.5f), Mathf.Sin(a) * UnityEngine.Random.Range(1f, 2.2f));
                var sr = Upright(_rock, at + Vector3.up * 0.1f, UnityEngine.Random.Range(0.12f, 0.22f));
                if (sr == null) continue;
                sr.color = new Color(0.55f, 0.45f, 0.35f); // 흙빛
                float t = 0f, spin = UnityEngine.Random.Range(-600f, 600f);
                var go = sr.gameObject;
                _live.Add(dt =>
                {
                    if (go == null) return false;
                    t += dt;
                    v += Vector3.down * 14f * dt;
                    go.transform.position += v * dt;
                    sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                    if (go.transform.position.y > 0.05f && t < 1.2f) return true;
                    Destroy(go);
                    return false;
                });
            }
        }

        // ── 비도 호신륜: 도는 동안 금빛 고리 잔광, 막아 사라진 투사체 자리에 불똥
        SpriteRenderer _halo;
        readonly Dictionary<Transform, Vector3> _incoming = new Dictionary<Transform, Vector3>();
        PlayerController _haloPlayer;
        readonly HashSet<Transform> _seen = new HashSet<Transform>(); // 매 프레임 새로 만들지 않게
        readonly List<Transform> _gone = new List<Transform>();

        void TickHalo(float dt)
        {
            if (_haloPlayer == null) _haloPlayer = FindAnyObjectByType<PlayerController>();
            bool on = _haloPlayer != null && _haloPlayer.IsHaloing;
            if (on && _halo == null) _halo = Flat(_ring, _haloPlayer.transform.position, 1.6f * 2.3f, 0f, 0.8f);
            if (_halo != null)
            {
                if (!on) { Destroy(_halo.gameObject); _halo = null; _incoming.Clear(); return; }
                var p = _haloPlayer.transform.position;
                _halo.transform.position = new Vector3(p.x, 0.8f, p.z);
                _halo.transform.Rotate(0f, 0f, -360f * dt, Space.Self);
                _halo.color = new Color(1f, 0.92f, 0.65f, 0.55f + 0.2f * Mathf.Sin(Time.time * 12f));
                // 투사체를 지켜보다가 고리 가까이서 사라지면 막힌 거예요
                var seen = _seen;
                seen.Clear();
                foreach (var a in FindObjectsByType<AThousandDawns.Enemies.Arrow>(FindObjectsSortMode.None)) { seen.Add(a.transform); _incoming[a.transform] = a.transform.position; }
                foreach (var b in FindObjectsByType<AThousandDawns.Enemies.BeadThrow>(FindObjectsSortMode.None)) { seen.Add(b.transform); _incoming[b.transform] = b.transform.position; }
                var gone = _gone;
                gone.Clear();
                foreach (var pair in _incoming)
                {
                    if (pair.Key != null && seen.Contains(pair.Key)) continue;
                    Vector3 last = pair.Value; last.y = 0f;
                    Vector3 me = new Vector3(p.x, 0f, p.z);
                    if ((last - me).magnitude <= 1.6f + 1.2f) Deflect(pair.Value);
                    gone.Add(pair.Key);
                }
                foreach (var g in gone) _incoming.Remove(g);
            }
        }

        void Deflect(Vector3 at)
        {
            for (int i = 0; i < 6; i++)
            {
                var sr = Upright(_glint, at, UnityEngine.Random.Range(0.15f, 0.25f));
                if (sr == null) return;
                var v = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2.5f, 4f);
                v.y = Mathf.Abs(v.y) + 0.5f;
                float t = 0f;
                var go = sr.gameObject;
                Vector3 unit = go.transform.localScale;
                _live.Add(dt =>
                {
                    if (go == null) return false;
                    t += dt;
                    v += Vector3.down * 8f * dt;
                    go.transform.position += v * dt;
                    go.transform.localScale = unit * (1f - t / 0.3f);
                    if (t < 0.3f) return true;
                    Destroy(go);
                    return false;
                });
            }
        }

        // ── 붕산권: 황토빛 먹 충격파 고리
        void QuakeWave(PlayerController.SkillFxCue c)
        {
            var sr = Flat(_quake, c.From, 1f, UnityEngine.Random.Range(0f, 360f), 0.05f);
            if (sr == null) return;
            float t = 0f, size = Mathf.Max(1f, c.Radius) * 2.3f;
            Vector3 unit = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.25f), 3f);
                sr.transform.localScale = unit * Mathf.Lerp(0.3f, size, grow);
                sr.color = new Color(1f, 1f, 1f, t < 0.2f ? 1f : Mathf.Clamp01(1f - (t - 0.2f) / 0.45f));
                if (t < 0.65f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // ── 금강장 보호막: 금빛 반짝이가 모여들고 고리가 오므라들어요
        void PalmShield(PlayerController player, PlayerController.SkillFxCue c)
        {
            Transform follow = player != null ? player.transform : null;
            Vector3 Center() => (follow != null ? follow.position : c.From) + Vector3.up * 1f;
            var ring = Upright(_ring, Center(), 2.6f);
            float t = 0f;
            Vector3 ringUnit = ring != null ? ring.transform.localScale : Vector3.one;
            _live.Add(dt =>
            {
                t += dt;
                if (ring != null)
                {
                    float k = Mathf.Clamp01(t / 0.35f);
                    ring.transform.position = Center();
                    ring.transform.localScale = ringUnit * Mathf.Lerp(1f, 0.55f, k * k);
                    ring.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                }
                if (t < 0.4f) return true;
                if (ring != null) Destroy(ring.gameObject);
                return false;
            });
            int n = Mathf.Clamp(c.Count, 1, 12) * 2; // 돌아온 비도 수만큼
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2f;
                Vector3 off = new Vector3(Mathf.Cos(ang), UnityEngine.Random.Range(-0.4f, 0.6f), Mathf.Sin(ang)) * UnityEngine.Random.Range(1.4f, 2.2f);
                Converge(_glint, Center, off, UnityEngine.Random.Range(0.25f, 0.4f), 0.3f + i * 0.01f);
            }
        }

        // ── 금강장 장풍: 금빛 손바닥이 날아가고 금빛 물결이 퍼져요
        void PalmBlast(PlayerController.SkillFxCue c)
        {
            Vector3 dir = c.Dir; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            float reach = Mathf.Max(1f, c.Radius);
            var palm = Upright(_palm, c.From + Vector3.up * 1.1f + dir * 0.6f, 1.2f);
            var wave = Flat(_wave, c.From + dir * reach * 0.5f, reach * 1.1f, Yaw(dir) - 90f, 0.05f); // 그림 아래(물결이 퍼지는 쪽)가 앞으로
            float t = 0f;
            Vector3 palmUnit = palm != null ? palm.transform.localScale : Vector3.one;
            Vector3 waveUnit = wave != null ? wave.transform.localScale : Vector3.one;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / 0.35f);
                float ease = 1f - (1f - k) * (1f - k);
                if (palm != null)
                {
                    palm.transform.position = c.From + Vector3.up * 1.1f + dir * Mathf.Lerp(0.6f, reach * 0.85f, ease);
                    palm.transform.localScale = palmUnit * Mathf.Lerp(0.8f, 2.2f, ease);
                    palm.color = new Color(1f, 1f, 1f, 1f - k * k);
                }
                if (wave != null)
                {
                    wave.transform.position = c.From + dir * Mathf.Lerp(reach * 0.25f, reach * 0.6f, ease) + Vector3.up * 0.05f;
                    wave.transform.localScale = waveUnit * Mathf.Lerp(0.5f, 1.1f, ease);
                    wave.color = new Color(1f, 1f, 1f, 1f - k);
                }
                if (k < 1f) return true;
                if (palm != null) Destroy(palm.gameObject);
                if (wave != null) Destroy(wave.gameObject);
                return false;
            });
        }

        // ── 도움

        // 반짝이 한 알이 center 둘레 off에서 center로 모여들어요
        void Converge(Sprite sprite, Func<Vector3> center, Vector3 off, float size, float life)
        {
            var sr = Upright(sprite, center() + off, size);
            if (sr == null) return;
            float t = 0f;
            Vector3 unit = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / life);
                sr.transform.position = center() + off * (1f - k * k);
                sr.transform.localScale = unit * Mathf.Lerp(1f, 0.4f, k);
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        LineRenderer Line(Vector3 a, Vector3 b, Color color, float startWidth, float endWidth, float lift)
        {
            var lr = new GameObject("Trail").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.sharedMaterial = _lineMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, endWidth), new Keyframe(0.7f, startWidth), new Keyframe(1f, startWidth * 0.6f)); // 꼬리 가늘게 · 도착 쪽 굵게
            lr.startColor = lr.endColor = color;
            lr.numCapVertices = 2;
            lr.sortingOrder = 6;
            lr.alignment = LineAlignment.View;
            return lr;
        }

        static void Fade(LineRenderer lr, float alpha)
        {
            if (lr == null) return;
            var c = lr.startColor;
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, alpha);
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
            sr.sortingOrder = 5;
            return sr;
        }

        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            go.AddComponent<InkFaceCamera>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            return sr;
        }

        static float Yaw(Vector3 dir) => -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg; // 그림의 +X를 dir로

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _quake = Resources.Load<Sprite>("SkillFx/QuakeRing");
            _palm = Resources.Load<Sprite>("SkillFx/GoldPalm");
            _wave = Resources.Load<Sprite>("SkillFx/GoldWave");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _brush = Resources.Load<Texture2D>("HudArt/HpStroke");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
            if (_brush != null) _lineMat.mainTexture = _brush;
        }
    }
}
