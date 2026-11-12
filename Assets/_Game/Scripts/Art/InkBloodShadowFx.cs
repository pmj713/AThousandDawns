using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈영 이펙트 (천마총 고수 혈영검마 · 비급 혈영참) — 그림만, 판정·수치는 게임 코드 그대로.
    //   잔상 (BloodShadowBrain EchoStart → EchoCut): 그 자리에 붉은 그림자 사람이 서서 짙어지다가, 벨 때 붉은 섬광을 긋고 피 안개로 흩어져요.
    //   혈영보 (Blink): 사라진 자리 · 나타난 자리에 피 안개가 번져요.
    //   흡혈 (Drain): 맞힌 플레이어에게서 핏방울이 혈영검마 쪽으로 빨려 들어가요.
    //   혈영참 (PlayerController.SkillCue ShadowCut): 돌진·잔상 베기에 붉은 먹 잔상 선, 비도 잔상(Radius>0)은 원형 붉은 베기 고리.
    // 그림: TombFx(BloodShade), BloodBuddhaFx(Slash · Splash · Drop · Mandala), HudArt/HpStroke(붓 획).
    public class InkBloodShadowFx : MonoBehaviour
    {
        static InkBloodShadowFx _host;
        static Sprite _shade, _slash, _splash, _drop, _ring;
        static Material _lineMat;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            BloodShadowBrain.Cue -= OnBoss;
            BloodShadowBrain.Cue += OnBoss;
            PlayerController.SkillCue -= OnSkill;
            PlayerController.SkillCue += OnSkill;
        }

        static InkBloodShadowFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Blood Shadow Fx").AddComponent<InkBloodShadowFx>();
            return _host;
        }

        static void OnBoss(BloodShadowBrain b, BloodShadowBrain.FxCue c)
        {
            var h = Host();
            switch (c.Kind)
            {
                case BloodShadowBrain.FxKind.EchoStart: h.Shade(c.From, c.Dir, c.Time); break;
                case BloodShadowBrain.FxKind.EchoCut: h.Cut(c.From, Flat(c.Dir), c.Line); break;
                case BloodShadowBrain.FxKind.Blink: h.Mist(c.From, 1.8f); h.Mist(c.To, 1.8f); break;
                case BloodShadowBrain.FxKind.Drain: h.Drain(c.From, b != null ? b.transform : null, c.To); break;
            }
        }

        static void OnSkill(PlayerController p, PlayerController.SkillFxCue c)
        {
            if (c.Kind != PlayerController.SkillFxKind.ShadowCut) return;
            var h = Host();
            if (c.Radius > 0f) h.RingCut(c.From, c.Radius); // 비도 잔상: 원형 베기
            else h.Streak(c.From, c.To);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 잔상: 붉은 그림자 사람이 짙어지다가 피 안개로 흩어져요
        void Shade(Vector3 at, Vector3 dir, float delay)
        {
            var sr = Upright(_shade, at, 1.3f, 6, bottom: true);
            if (sr == null) return;
            float t = 0f, life = Mathf.Max(0.1f, delay);
            Vector3 unit = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < life)
                {
                    float k = t / life;
                    sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.2f, 0.85f, k));
                    sr.transform.localScale = unit * (1f + 0.03f * Mathf.Sin(t * 30f)); // 떨려요
                    return true;
                }
                float a = (t - life) / 0.3f; // 벤 뒤 흩어져요
                sr.color = new Color(1f, 1f, 1f, 0.85f * (1f - a));
                sr.transform.localScale = unit * (1f + 0.3f * a);
                if (a < 1f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        // 잔상이 벨 때: 부채꼴은 붉은 초승달, 직선은 붉은 먹 선
        void Cut(Vector3 at, Vector3 dir, bool line)
        {
            if (line) { Streak(at, at + dir * 6f); return; }
            if (_slash == null) return;
            var go = new GameObject("Shadow Slash");
            go.transform.SetParent(transform, false);
            Vector3 p = at + dir * 1.2f;
            go.transform.position = new Vector3(p.x, 0.9f, p.z);
            go.transform.rotation = Quaternion.Euler(90f, -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg - 90f, 0f); // 활 등이 앞으로
            float s = 2.6f / Mathf.Max(0.01f, _slash.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _slash;
            sr.sortingOrder = 7;
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                go.transform.localScale = Vector3.one * s * Mathf.Lerp(0.6f, 1.15f, t / 0.25f);
                sr.color = new Color(1f, 1f, 1f, 1f - t / 0.25f);
                if (t < 0.25f) return true;
                Destroy(go);
                return false;
            });
            Mist(at, 1.2f);
        }

        // 피 안개: 바닥 핏물이 번졌다 옅어져요
        void Mist(Vector3 at, float size)
        {
            if (_splash == null) return;
            var go = new GameObject("Blood Mist");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.04f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            float unit = 1f / Mathf.Max(0.01f, _splash.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _splash;
            sr.sortingOrder = 4;
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                go.transform.localScale = Vector3.one * unit * size * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t / 0.2f));
                sr.color = new Color(1f, 1f, 1f, 0.8f * (1f - Mathf.Clamp01((t - 0.2f) / 0.7f)));
                if (t < 0.9f) return true;
                Destroy(go);
                return false;
            });
        }

        // 흡혈: 핏방울이 혈영검마에게 빨려 들어가요
        void Drain(Vector3 from, Transform boss, Vector3 fallback)
        {
            for (int i = 0; i < 6; i++)
            {
                var sr = Upright(_drop, from + Vector3.up * 0.9f + UnityEngine.Random.insideUnitSphere * 0.4f, 0.14f, 7);
                if (sr == null) return;
                var pivot = sr.transform.parent;
                Vector3 start = pivot.position;
                float t = -i * 0.04f, arc = UnityEngine.Random.Range(0.6f, 1.4f);
                _live.Add(dt =>
                {
                    if (sr == null) return false;
                    t += dt;
                    if (t < 0f) return true;
                    float k = Mathf.Clamp01(t / 0.35f);
                    Vector3 end = (boss != null ? boss.position : fallback) + Vector3.up * 1.1f;
                    pivot.position = Vector3.Lerp(start, end, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                    if (k < 1f) return true;
                    Destroy(pivot.gameObject);
                    return false;
                });
            }
        }

        // ── 혈영참: 붉은 먹 잔상 선 · 원형 베기
        void Streak(Vector3 from, Vector3 to)
        {
            Vector3 a = from + Vector3.up * 0.9f, b = to + Vector3.up * 0.9f;
            if ((b - a).sqrMagnitude < 0.04f || _lineMat == null) return;
            var ink = Line(a, b, new Color(0.1f, 0.04f, 0.04f), 0.7f);
            var red = Line(a, b, new Color(0.8f, 0.1f, 0.08f), 0.3f);
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / 0.4f);
                Fade(ink, a, b, k);
                Fade(red, a, b, k);
                if (k < 1f) return true;
                if (ink != null) Destroy(ink.gameObject);
                if (red != null) Destroy(red.gameObject);
                return false;
            });
        }

        static void Fade(LineRenderer lr, Vector3 a, Vector3 b, float k)
        {
            if (lr == null) return;
            var c = lr.startColor;
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, 1f - k);
            lr.SetPosition(0, Vector3.Lerp(a, b, k * 0.85f)); // 꼬리부터 걷혀요
        }

        void RingCut(Vector3 at, float radius)
        {
            if (_ring == null) return;
            var go = new GameObject("Shadow Ring");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.6f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            float unit = 1f / Mathf.Max(0.01f, _ring.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _ring;
            sr.sortingOrder = 6;
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                go.transform.localScale = Vector3.one * unit * radius * 2.2f * Mathf.Lerp(0.5f, 1.05f, Mathf.Clamp01(t / 0.15f));
                go.transform.Rotate(0f, 0f, -540f * dt, Space.Self);
                sr.color = new Color(1f, 0.6f, 0.55f, 1f - Mathf.Clamp01(t / 0.35f));
                if (t < 0.35f) return true;
                Destroy(go);
                return false;
            });
        }

        // ── 도움

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

        LineRenderer Line(Vector3 a, Vector3 b, Color color, float width)
        {
            var lr = new GameObject("Shadow Line").AddComponent<LineRenderer>();
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
            lr.sortingOrder = 6;
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
            _shade = Resources.Load<Sprite>("TombFx/BloodShade");
            _slash = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _ring = Resources.Load<Sprite>("BloodBuddhaFx/Mandala");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
            var brush = Resources.Load<Texture2D>("HudArt/HpStroke");
            if (brush != null) _lineMat.mainTexture = brush;
        }
    }
}
