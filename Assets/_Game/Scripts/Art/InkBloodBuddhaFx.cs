using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈불 패턴 이펙트 (붉은 먹). BloodBuddhaBrain.Cue 신호를 듣고 그림만 그려요 — 판정·타이밍은 게임 코드 그대로.
    //   손바닥 (내려찍기·연타): 바닥에 손바닥 그림자가 짙어지며 내려와 → 떨어지는 순간 붉은 먹 손자국이 찍히고 바래요
    //   염불: 바닥에 붉은 만다라 인장이 돌며 피어나고 → 떨어지면 번쩍 커지며 사라져요
    //   피의 비: 바닥에 옅은 피 그림자 → 마지막에 하늘에서 굵은 피 줄기가 쏟아져 → 바닥에 피 웅덩이(혈장)가 번져요
    //   팔 휘두르기: 휘두를 때마다 부채꼴 붉은 붓 획이 쓸고 지나가요
    //   혈수: 예고 동안 혈불 앞에 핏빛 손이 내밀어지고 직선 위 핏빛 기운이 손으로 빨려 들다가, 판정 순간 움켜쥐며 확 빨아들여요
    //   2페이즈 전환: 불상 발밑으로 큰 피 번짐 + 만다라가 퍼져요
    // 그림: Art/Env/BossFx/Resources/BloodBuddhaFx (스프라이트). 씬에 미리 둘 것 없이 게임이 켜지면 저절로 들어요.
    public class InkBloodBuddhaFx : MonoBehaviour
    {
        static InkBloodBuddhaFx _host;
        static Sprite _palm, _mandala, _drop, _splash, _hand, _slash, _pour;
        static float _columnUnit = 1f; // 피 줄기 그림 세로 1칸당 크기
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

        // 피의 비 → 혈장: 바닥에 옅은 피 그림자가 짙어지고, 마지막에 하늘에서 굵은 피 줄기가 쏟아져 내려와
        //   바닥에 피 웅덩이(혈장)로 번졌다가 바래요
        void Rain(BloodBuddhaBrain.FxCue c)
        {
            var pool = Flat(_splash, c.At, c.Radius * 2.2f, UnityEngine.Random.Range(0f, 360f), 0.03f);
            float t = 0f, fall = Mathf.Max(0.05f, c.Time), pour = Mathf.Min(0.3f, fall);
            const float Sky = 9f, ColumnH = 7f;
            SpriteRenderer column = null;
            bool landed = false;
            Vector3 poolScale = pool != null ? pool.transform.localScale : Vector3.one;
            _live.Add(dt =>
            {
                t += dt;
                if (!landed && pool != null) // 예고: 옅은 피 그림자
                {
                    pool.color = new Color(0.3f, 0.02f, 0.02f, 0.35f * Mathf.Clamp01(t / fall));
                    pool.transform.localScale = poolScale * 0.7f;
                }
                if (column == null && t >= fall - pour) column = Upright(_pour != null ? _pour : _drop, c.At + Vector3.up * (Sky + ColumnH * 0.5f), ColumnH, c.Radius * 1.4f);
                if (column != null) // 줄기 아래 끝이 하늘에서 바닥까지 내려와요 → 닿은 뒤엔 위에서부터 끊기며 사라져요
                {
                    float bottom = !landed ? Mathf.Lerp(Sky, 0f, Mathf.Clamp01((t - (fall - pour)) / pour)) : 0f;
                    float vanish = landed ? Mathf.Clamp01((t - fall) / 0.35f) : 0f;
                    float h = ColumnH * (1f - vanish);
                    var ct = column.transform;
                    ct.position = c.At + Vector3.up * (bottom + h * 0.5f);
                    ct.localScale = new Vector3(ct.localScale.x, _columnUnit * Mathf.Max(0.01f, h), ct.localScale.z);
                    column.color = new Color(1f, 1f, 1f, 1f - vanish * vanish);
                    if (vanish >= 1f) { Destroy(column.gameObject); column = null; }
                }
                if (!landed && t >= fall) landed = true;
                if (!landed) return true;
                float a = t - fall; // 혈장이 번져 머물다 바래요
                if (pool != null)
                {
                    pool.transform.localScale = poolScale * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(a / 0.25f));
                    pool.color = new Color(1f, 1f, 1f, a < 0.7f ? 1f : Mathf.Clamp01(1f - (a - 0.7f) / 1f));
                }
                if (a < 1.7f || column != null) return true;
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

        // 혈수: 예고(GrabStart) 동안 혈불 앞으로 핏빛 손이 내밀어지고, 직선 위 바닥에서 핏빛 기운이 손으로 빨려 들어가요.
        //   판정(Grab) 순간 손이 움켜쥐며 멀리서부터 확 빨아들이고, 손은 거둬져요.
        class GrabState { public SpriteRenderer Hand; public Vector3 Origin, Dir, HandAt; public float Length, Width, T, Windup, PullT = -1f; public Vector3 HandScale; }
        GrabState _grab;

        void GrabStart(BloodBuddhaBrain.FxCue c)
        {
            Vector3 dir = Flatten(c.Dir);
            var g = new GrabState
            {
                Origin = c.At, Dir = dir, Length = Mathf.Max(2f, c.Length), Width = Mathf.Max(0.5f, c.Radius * 2f),
                Windup = Mathf.Max(0.1f, c.Time), HandAt = c.At + dir * 1.9f,
            };
            g.Hand = Flat(_hand, g.HandAt, 2.4f, Yaw(dir), 0.08f); // 손끝이 앞(플레이어 쪽)으로
            if (g.Hand != null) g.HandScale = g.Hand.transform.localScale;
            _grab = g;
            float spawn = 0f;
            _live.Add(dt =>
            {
                g.T += dt;
                bool pulling = g.PullT >= 0f;
                float pullAge = pulling ? g.T - g.PullT : 0f;
                if (g.Hand != null)
                {
                    // 내밀어져요 (예고 동안 커지며 짙어짐) → 움켜쥘 때 툭 커졌다가 → 거둬지며 옅어져요
                    float grow = Mathf.Clamp01(g.T / (g.Windup * 0.6f));
                    float punch = pulling ? 1f + 0.25f * Mathf.Max(0f, 1f - pullAge / 0.15f) : 1f;
                    float fade = pulling ? Mathf.Clamp01(1f - (pullAge - 0.25f) / 0.35f) : 1f;
                    g.Hand.transform.localScale = g.HandScale * Mathf.Lerp(0.5f, 1f, grow) * punch;
                    g.Hand.transform.position = g.Origin + g.Dir * (1.9f * Mathf.Lerp(0.4f, 1f, grow) - (pulling ? Mathf.Max(0f, pullAge - 0.25f) * 2f : 0f)) + Vector3.up * 0.08f;
                    g.Hand.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, grow) * fade);
                }
                // 빨려 드는 핏빛 기운: 예고 동안 점점 촘촘하게, 움켜쥘 땐 한꺼번에
                if (!pulling || pullAge < 0.2f)
                {
                    spawn -= dt;
                    float rate = pulling ? 0.008f : Mathf.Lerp(0.09f, 0.025f, Mathf.Clamp01(g.T / g.Windup));
                    while (spawn <= 0f) { spawn += rate; Suck(g, pulling); }
                }
                bool done = pulling ? pullAge > 0.6f : g.T > g.Windup + 0.8f; // 얼려서 끊기면 알아서 끝나요
                if (!done) return true;
                if (g.Hand != null) Destroy(g.Hand.gameObject);
                if (_grab == g) _grab = null;
                return false;
            });
        }

        void Grab(BloodBuddhaBrain.FxCue c)
        {
            if (_grab == null) { GrabStart(new BloodBuddhaBrain.FxCue { At = c.At, Dir = c.Dir, Length = c.Length, Radius = c.Radius, Time = 0.1f }); }
            if (_grab != null && _grab.PullT < 0f) _grab.PullT = _grab.T;
        }

        // 핏빛 기운 한 가닥: 직선 위 바닥 어딘가에서 손 쪽으로 빨려 들어가요 (머리가 손 쪽, 꼬리가 뒤)
        void Suck(GrabState g, bool strong)
        {
            if (_drop == null) return;
            Vector3 side = new Vector3(g.Dir.z, 0f, -g.Dir.x);
            float along = UnityEngine.Random.Range(2.2f, g.Length + 1f);
            Vector3 from = g.Origin + g.Dir * along + side * UnityEngine.Random.Range(-g.Width * 1.6f, g.Width * 1.6f);
            float speed = strong ? UnityEngine.Random.Range(26f, 34f) : UnityEngine.Random.Range(9f, 14f);
            float len = strong ? 1.4f : 0.8f;
            var sr = Flat(_drop, from, len * 0.3f, 0f, 0.05f);
            if (sr == null) return;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                Vector3 target = g.Hand != null ? g.Hand.transform.position : g.HandAt;
                Vector3 to = target - sr.transform.position; to.y = 0f;
                float dist = to.magnitude;
                Vector3 m = dist > 0.001f ? to / dist : g.Dir * -1f;
                float step = speed * (1f + t * 2f) * dt; // 가까워질수록 빨라져요
                sr.transform.position += m * Mathf.Min(step, dist);
                sr.transform.rotation = Quaternion.Euler(90f, Yaw(m) - 90f, 0f); // 그림 아래(머리)가 손 쪽
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01(dist / 0.8f));
                if (dist > 0.35f && t < 1.5f) return true;
                Destroy(sr.gameObject);
                return false;
            });
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
            _hand = Resources.Load<Sprite>("BloodBuddhaFx/Hand");
            _slash = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
            _pour = Resources.Load<Sprite>("BloodBuddhaFx/Pour");
            var column = _pour != null ? _pour : _drop;
            if (column != null) _columnUnit = 1f / Mathf.Max(0.01f, column.bounds.size.y);
        }
    }
}
