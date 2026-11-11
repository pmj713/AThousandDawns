using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈불 패턴 이펙트 (붉은 먹). BloodBuddhaBrain.Cue 신호를 듣고 그림만 그려요 — 판정·타이밍은 게임 코드 그대로.
    //   손바닥 (내려찍기·연타): 바닥에 손바닥 그림자가 짙어지며 내려와 → 떨어지는 순간 붉은 먹 손자국이 찍히고 바래요
    //   염불: 바닥에 붉은 만다라 인장이 돌며 피어나고 → 떨어지면 번쩍 커지며 사라져요
    //   피의 비: 손바닥 내려찍기와 같은 연출 (그림자가 짙어지며 내려와 → 붉은 먹 손자국이 찍히고 바래요)
    //   팔 휘두르기: 휘두를 때마다 부채꼴 붉은 붓 획이 쓸고 지나가요
    //   혈수: 혈불 손끝에서 앞으로 넓게 벌어지는 깔때기 나선(검은 선·빨간 선)이 손 쪽으로 감겨 들다가, 판정 순간 확 빨려 들어요
    //   2페이즈 전환: 불상 발밑으로 큰 피 번짐 + 만다라가 퍼져요
    // 그림: Art/Env/BossFx/Resources/BloodBuddhaFx (스프라이트). 씬에 미리 둘 것 없이 게임이 켜지면 저절로 들어요.
    public class InkBloodBuddhaFx : MonoBehaviour
    {
        static InkBloodBuddhaFx _host;
        static Sprite _palm, _mandala, _drop, _splash, _slash;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>(); // 매 프레임 부르고, false면 끝

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            BloodBuddhaBrain.Cue -= OnCue;
            BloodBuddhaBrain.Cue += OnCue;
        }

        static void OnCue(BloodBuddhaBrain brain, BloodBuddhaBrain.FxCue cue)
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Blood Buddha Fx").AddComponent<InkBloodBuddhaFx>();
            switch (cue.Kind)
            {
                case BloodBuddhaBrain.FxKind.Palm: _host.Palm(cue); break;
                case BloodBuddhaBrain.FxKind.Chant: _host.Chant(cue); break;
                case BloodBuddhaBrain.FxKind.Rain: _host.Rain(cue); break;
                case BloodBuddhaBrain.FxKind.Swing: _host.Swing(cue); break;
                case BloodBuddhaBrain.FxKind.GrabStart: _host.GrabStart(cue, brain); break;
                case BloodBuddhaBrain.FxKind.Grab: _host.Grab(cue, brain); break;
                case BloodBuddhaBrain.FxKind.Transition: _host.Transition(cue); break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        // ── 패턴별

        void Palm(BloodBuddhaBrain.FxCue c)
        {
            var sr = Flat(_palm, c.At, c.Radius * 2.5f, UnityEngine.Random.Range(-25f, 25f), 0.04f);
            if (sr == null) return;
            float t = 0f, fall = Mathf.Max(0.05f, c.Time);
            Vector3 baseScale = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < fall) // 그림자가 내려와요: 크고 옅게 → 제 크기로 짙게
                {
                    float k = t / fall;
                    sr.color = new Color(0.25f, 0.02f, 0.02f, Mathf.Lerp(0.1f, 0.55f, k * k));
                    sr.transform.localScale = baseScale * Mathf.Lerp(1.35f, 1f, k);
                    return true;
                }
                float a = t - fall; // 찍힌 뒤: 살짝 튀고, 0.6초 머물다 1.2초 동안 바래요
                sr.color = new Color(1f, 1f, 1f, a < 0.6f ? 1f : Mathf.Clamp01(1f - (a - 0.6f) / 1.2f));
                sr.transform.localScale = baseScale * (1f + 0.08f * Mathf.Max(0f, 1f - a / 0.12f));
                if (a < 1.8f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Chant(BloodBuddhaBrain.FxCue c)
        {
            var sr = Flat(_mandala, c.At, c.Radius * 2.7f, UnityEngine.Random.Range(0f, 360f), 0.035f);
            if (sr == null) return;
            float t = 0f, fall = Mathf.Max(0.05f, c.Time);
            Vector3 baseScale = sr.transform.localScale;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.Rotate(0f, 0f, 90f * dt, Space.Self); // 천천히 돌아요
                if (t < fall)
                {
                    float k = t / fall;
                    sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 0.9f, k));
                    sr.transform.localScale = baseScale * Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(k));
                    return true;
                }
                float a = (t - fall) / 0.4f; // 번쩍 커지며 사라져요
                sr.color = new Color(1f, 0.85f, 0.7f, 1f - a);
                sr.transform.localScale = baseScale * (1f + 0.35f * a);
                if (a < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 피의 비: 손바닥 내려찍기와 같은 연출 (손바닥 그림자가 크고 옅게 → 짙고 작게 내려와 → 붉은 먹 손자국이 찍히고 바래요)
        void Rain(BloodBuddhaBrain.FxCue c) => Palm(c);

        void Swing(BloodBuddhaBrain.FxCue c)
        {
            Vector3 dir = Flatten(c.Dir);
            float yaw = Yaw(dir);
            var sr = Flat(_slash, c.At + dir * c.Radius * 0.55f, c.Radius * 2f, yaw - 90f, 0.06f); // 그림의 활 등(아래)이 앞으로
            if (sr == null) return;
            float t = 0f;
            var baseRot = sr.transform.rotation;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / 0.35f; // 쓸고 지나가며 옅어져요
                sr.transform.rotation = Quaternion.AngleAxis(Mathf.Lerp(18f, -18f, k), Vector3.up) * baseRot;
                sr.color = new Color(1f, 1f, 1f, 1f - k * k);
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 혈수: 혈불이 앞으로 손을 뻗으면 손끝에서 앞으로 누운 깔때기 모양 나선(검은 선·빨간 선 몇 가닥)이 생겨요.
        //   손 쪽은 좁고 앞(플레이어 쪽)으로 갈수록 넓게 벌어지며, 선이 돌면서 손 쪽으로 흘러 들어가요.
        //   예고 동안 길어지고 빨라지다가, 판정(Grab) 순간 손 쪽으로 확 빨려 들며 사라져요.
        class GrabState { public Transform Root, Palm; public LineRenderer[] Arms; public Vector3 Hand, Dir, Side; public float T, Windup, PullT = -1f, Phase, Length; }
        GrabState _grab;
        static Material _lineMat;
        const int ArmCount = 4, ArmPoints = 24;
        const float HandRadius = 0.12f, MouthRadius = 1.1f; // 손 쪽 · 앞쪽 나선 반지름

        void GrabStart(BloodBuddhaBrain.FxCue c, BloodBuddhaBrain brain)
        {
            Vector3 dir = Flatten(c.Dir);
            var g = new GrabState
            {
                Windup = Mathf.Max(0.1f, c.Time), Dir = dir, Side = new Vector3(dir.z, 0f, -dir.x),
                Hand = c.At + dir * 2f + Vector3.up * 1.8f, Length = Mathf.Clamp(c.Length * 0.28f, 1.5f, 3f), // 절반 길이
                Palm = brain != null ? FindDeep(brain.transform, "RightHand") : null, // 뻗은 손바닥 중심에서 나와요
            };
            if (_lineMat == null) // 마른 붓 획 그림을 선 따라 늘여 그려요 (붓 결이 보이게)
            {
                _lineMat = new Material(Shader.Find("Sprites/Default"));
                var brush = Resources.Load<Texture2D>("HudArt/HpStroke");
                if (brush != null) _lineMat.mainTexture = brush;
            }
            g.Root = new GameObject("Blood Vortex").transform;
            g.Root.SetParent(transform, false);
            g.Arms = new LineRenderer[ArmCount];
            for (int k = 0; k < ArmCount; k++)
            {
                var lr = new GameObject("Arm").AddComponent<LineRenderer>();
                lr.transform.SetParent(g.Root, false);
                lr.useWorldSpace = true;
                lr.positionCount = ArmPoints;
                lr.sharedMaterial = _lineMat;
                lr.numCapVertices = 2;
                bool red = k % 2 == 1;
                // 점 0 = 앞쪽(넓고 굵고 옅게) → 끝 = 손(좁고 가늘고 짙게)
                lr.textureMode = LineTextureMode.Stretch;
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, red ? 0.2f : 0.26f), new Keyframe(0.6f, red ? 0.12f : 0.15f), new Keyframe(1f, 0.03f));
                lr.sortingOrder = 6;
                g.Arms[k] = lr;
            }
            _grab = g;
            _live.Add(dt =>
            {
                g.T += dt;
                if (g.Palm != null) g.Hand = g.Palm.position + g.Dir * 0.12f; // 손바닥 중심 (손 뼈에서 앞으로 약 0.1m · 팔이 움직이면 따라가요)
                bool pulling = g.PullT >= 0f;
                float pullAge = pulling ? g.T - g.PullT : 0f;
                float grow = Mathf.Clamp01(g.T / g.Windup);
                g.Phase += (pulling ? 1100f : Mathf.Lerp(200f, 700f, grow)) * dt; // 돌수록 선이 손 쪽으로 흘러 보여요
                float reach = pulling ? Mathf.Lerp(1f, 0f, Mathf.Clamp01(pullAge / 0.3f)) : Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(grow)); // 길이 배율
                float alpha = pulling ? Mathf.Clamp01(1f - (pullAge - 0.15f) / 0.2f) : Mathf.Lerp(0.3f, 1f, grow);
                float len = g.Length * reach;
                for (int k = 0; k < ArmCount; k++)
                {
                    var lr = g.Arms[k];
                    float start = k * 360f / ArmCount;
                    for (int i = 0; i < ArmPoints; i++)
                    {
                        float s = i / (ArmPoints - 1f);            // 0 = 앞쪽, 1 = 손
                        float along = len * (1f - s);
                        float r = Mathf.Lerp(MouthRadius, HandRadius, s) * Mathf.Lerp(0.6f, 1f, reach);
                        float a = (start + g.Phase - s * 300f) * Mathf.Deg2Rad; // 손 쪽으로 감겨 들어가는 나선
                        Vector3 axis = g.Hand + g.Dir * along + Vector3.down * (along * 0.12f); // 앞으로 갈수록 살짝 낮게
                        lr.SetPosition(i, axis + (g.Side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * r);
                    }
                    var col = k % 2 == 1 ? new Color(0.75f, 0.05f, 0.05f) : new Color(0.06f, 0.04f, 0.04f);
                    lr.startColor = new Color(col.r, col.g, col.b, 0.35f * alpha); // 앞쪽은 옅게 퍼져요
                    lr.endColor = new Color(col.r, col.g, col.b, alpha);
                }
                bool done = pulling ? pullAge > 0.4f : g.T > g.Windup + 0.8f; // 얼려서 끊기면 알아서 끝나요
                if (!done) return true;
                Destroy(g.Root.gameObject);
                if (_grab == g) _grab = null;
                return false;
            });
        }

        void Grab(BloodBuddhaBrain.FxCue c, BloodBuddhaBrain brain)
        {
            if (_grab == null) GrabStart(new BloodBuddhaBrain.FxCue { At = c.At, Dir = c.Dir, Length = c.Length, Time = 0.1f }, brain);
            if (_grab != null && _grab.PullT < 0f) _grab.PullT = _grab.T;
        }

        void Transition(BloodBuddhaBrain.FxCue c)
        {
            var pool = Flat(_splash, c.At, c.Radius * 3.2f, UnityEngine.Random.Range(0f, 360f), 0.025f);
            var ring = Flat(_mandala, c.At, c.Radius * 2f, 0f, 0.045f);
            float t = 0f, hold = Mathf.Max(1f, c.Time);
            Vector3 poolScale = pool != null ? pool.transform.localScale : Vector3.one;
            Vector3 ringScale = ring != null ? ring.transform.localScale : Vector3.one;
            _live.Add(dt =>
            {
                t += dt;
                if (pool != null) // 피가 번져 퍼지고, 전환이 끝나면 바래요
                {
                    pool.transform.localScale = poolScale * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(t / 0.6f));
                    pool.color = new Color(1f, 1f, 1f, t < hold ? 1f : Mathf.Clamp01(1f - (t - hold) / 1.5f));
                }
                if (ring != null)
                {
                    float k = Mathf.Clamp01(t / 1.2f);
                    ring.transform.localScale = ringScale * Mathf.Lerp(0.5f, 2.2f, k);
                    ring.transform.Rotate(0f, 0f, 60f * dt, Space.Self);
                    ring.color = new Color(1f, 0.85f, 0.7f, 1f - k);
                }
                if (t < hold + 1.5f) return true;
                if (pool != null) Destroy(pool.gameObject);
                if (ring != null) Destroy(ring.gameObject);
                return false;
            });
        }

        // ── 도움

        // 바닥에 눕힌 그림 (가운데 기준, 오른쪽(+X)이 yaw 방향)
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

        // 카메라를 보고 선 그림 (높이 height, 폭 width — 폭을 주면 따로 늘여요)
        SpriteRenderer Upright(Sprite sprite, Vector3 at, float height, float width)
        {
            var sr = Upright(sprite, at, height);
            if (sr != null) sr.transform.localScale = new Vector3(width / Mathf.Max(0.01f, sprite.bounds.size.x), height / Mathf.Max(0.01f, sprite.bounds.size.y), 1f);
            return sr;
        }

        // 카메라를 보고 선 그림 (높이 height)
        SpriteRenderer Upright(Sprite sprite, Vector3 at, float height)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, sprite.bounds.size.y));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 6;
            go.AddComponent<InkFaceCamera>();
            return sr;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name || root.name.EndsWith(":" + name)) return root;
            foreach (Transform child in root) { var t = FindDeep(child, name); if (t != null) return t; }
            return null;
        }

        static Vector3 Flatten(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
        static float Yaw(Vector3 dir) => -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg; // 그림의 +X를 dir로

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _palm = Resources.Load<Sprite>("BloodBuddhaFx/Palm");
            _mandala = Resources.Load<Sprite>("BloodBuddhaFx/Mandala");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _slash = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
        }
    }
}
