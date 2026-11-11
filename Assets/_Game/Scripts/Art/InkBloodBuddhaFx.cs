using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈불 패턴 이펙트 (붉은 먹). BloodBuddhaBrain.Cue 신호를 듣고 그림만 그려요 — 판정·타이밍은 게임 코드 그대로.
    //   손바닥 (내려찍기·연타): 바닥에 손바닥 그림자가 짙어지며 내려와 → 떨어지는 순간 붉은 먹 손자국이 찍히고 바래요
    //   염불: 바닥에 붉은 만다라 인장이 돌며 피어나고 → 떨어지면 번쩍 커지며 사라져요
    //   피의 비 (혈장): 바닥에 손바닥 그림자 → 피로 된 손바닥이 하늘에서 내려와 내리찍고 → 피 웅덩이와 손자국이 바래요
    //   팔 휘두르기: 휘두를 때마다 부채꼴 붉은 붓 획이 쓸고 지나가요
    //   혈수: 혈불이 손을 뻗은 자리에 핏빛 소용돌이가 커지며 빨라지다가, 판정 순간 확 빨아들이며 오므라들어요
    //   2페이즈 전환: 불상 발밑으로 큰 피 번짐 + 만다라가 퍼져요
    // 그림: Art/Env/BossFx/Resources/BloodBuddhaFx (스프라이트). 씬에 미리 둘 것 없이 게임이 켜지면 저절로 들어요.
    public class InkBloodBuddhaFx : MonoBehaviour
    {
        static InkBloodBuddhaFx _host;
        static Sprite _palm, _mandala, _drop, _splash, _slash, _vortex;
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
                case BloodBuddhaBrain.FxKind.GrabStart: _host.GrabStart(cue); break;
                case BloodBuddhaBrain.FxKind.Grab: _host.Grab(cue); break;
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

        // 피의 비 (혈장 = 피로 이루어진 손바닥): 바닥에 손바닥 그림자가 짙어지고, 마지막에 피 손바닥이 하늘에서 내려와
        //   그 자리를 내리찍어요 → 피 웅덩이와 함께 손자국이 남았다 바래요
        void Rain(BloodBuddhaBrain.FxCue c)
        {
            float yaw = UnityEngine.Random.Range(-30f, 30f), width = c.Radius * 2.4f;
            var shadow = Flat(_palm, c.At, width, yaw, 0.03f);
            float t = 0f, fall = Mathf.Max(0.05f, c.Time), drop = Mathf.Min(0.35f, fall);
            const float Sky = 6f;
            SpriteRenderer hand = null, pool = null;
            bool landed = false;
            Vector3 handScale = Vector3.one, poolScale = Vector3.one;
            _live.Add(dt =>
            {
                t += dt;
                if (!landed && shadow != null) shadow.color = new Color(0.25f, 0.02f, 0.02f, 0.4f * Mathf.Clamp01(t / fall));
                if (hand == null && !landed && t >= fall - drop)
                {
                    hand = Flat(_palm, c.At + Vector3.up * Sky, width, yaw, Sky);
                    if (hand != null) handScale = hand.transform.localScale;
                }
                if (hand != null && !landed) // 하늘에서 내려와요 (높을수록 크게)
                {
                    float k = Mathf.Clamp01((t - (fall - drop)) / drop);
                    float y = Mathf.Lerp(Sky, 0.05f, k * k);
                    hand.transform.position = new Vector3(c.At.x, y, c.At.z);
                    hand.transform.localScale = handScale * Mathf.Lerp(1.5f, 1f, k);
                    hand.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
                }
                if (!landed && t >= fall)
                {
                    landed = true;
                    if (shadow != null) { Destroy(shadow.gameObject); shadow = null; }
                    pool = Flat(_splash, c.At, width * 1.2f, UnityEngine.Random.Range(0f, 360f), 0.025f);
                    if (pool != null) poolScale = pool.transform.localScale;
                }
                if (!landed) return true;
                float a = t - fall; // 찍힌 뒤: 피가 튀고 0.5초 머물다 1초 동안 바래요
                float alpha = a < 0.5f ? 1f : Mathf.Clamp01(1f - (a - 0.5f) / 1f);
                if (hand != null) { hand.color = new Color(1f, 1f, 1f, alpha); hand.transform.localScale = handScale * (1f + 0.08f * Mathf.Max(0f, 1f - a / 0.12f)); }
                if (pool != null) { pool.color = new Color(1f, 1f, 1f, alpha * 0.85f); pool.transform.localScale = poolScale * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(a / 0.2f)); }
                if (a < 1.5f) return true;
                if (hand != null) Destroy(hand.gameObject);
                if (pool != null) Destroy(pool.gameObject);
                return false;
            });
        }

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

        // 혈수: 혈불이 앞으로 손을 뻗으면 그 손끝에 핏빛 소용돌이가 생겨요 — 예고 동안 커지며 빨라지고,
        //   판정(Grab) 순간 확 빨아들이며 오므라들어 사라져요.
        class GrabState { public Transform Pivot; public SpriteRenderer Swirl; public Vector3 SwirlScale; public float T, Windup, PullT = -1f; }
        GrabState _grab;

        void GrabStart(BloodBuddhaBrain.FxCue c)
        {
            Vector3 dir = Flatten(c.Dir);
            var g = new GrabState { Windup = Mathf.Max(0.1f, c.Time) };
            if (_vortex != null)
            {
                g.Pivot = new GameObject("Blood Vortex").transform; // 카메라를 보는 받침 · 그 안에서 그림이 돌아요
                g.Pivot.SetParent(transform, false);
                g.Pivot.position = c.At + dir * 2.4f + Vector3.up * 2.2f;
                g.Pivot.gameObject.AddComponent<InkFaceCamera>();
                var go = new GameObject("Swirl");
                go.transform.SetParent(g.Pivot, false);
                g.Swirl = go.AddComponent<SpriteRenderer>();
                g.Swirl.sprite = _vortex;
                g.Swirl.sortingOrder = 6;
                g.SwirlScale = Vector3.one * (2.6f / Mathf.Max(0.01f, _vortex.bounds.size.x));
            }
            _grab = g;
            _live.Add(dt =>
            {
                g.T += dt;
                bool pulling = g.PullT >= 0f;
                float pullAge = pulling ? g.T - g.PullT : 0f;
                if (g.Swirl != null)
                {
                    float grow = Mathf.Clamp01(g.T / g.Windup);
                    float spin = pulling ? 1080f : Mathf.Lerp(180f, 720f, grow);
                    g.Swirl.transform.Rotate(0f, 0f, -spin * dt, Space.Self);
                    float size = pulling ? (pullAge < 0.1f ? Mathf.Lerp(1f, 1.3f, pullAge / 0.1f) : Mathf.Lerp(1.3f, 0f, (pullAge - 0.1f) / 0.35f)) : Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(grow));
                    g.Swirl.transform.localScale = g.SwirlScale * Mathf.Max(0f, size);
                    g.Swirl.color = new Color(1f, 1f, 1f, pulling ? 1f : Mathf.Lerp(0.4f, 1f, grow));
                }
                bool done = pulling ? pullAge > 0.45f : g.T > g.Windup + 0.8f; // 얼려서 끊기면 알아서 끝나요
                if (!done) return true;
                if (g.Pivot != null) Destroy(g.Pivot.gameObject);
                if (_grab == g) _grab = null;
                return false;
            });
        }

        void Grab(BloodBuddhaBrain.FxCue c)
        {
            if (_grab == null) GrabStart(new BloodBuddhaBrain.FxCue { At = c.At, Dir = c.Dir, Time = 0.1f });
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
            _vortex = Resources.Load<Sprite>("BloodBuddhaFx/Vortex");
        }
    }
}
