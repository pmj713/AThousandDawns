using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈불 패턴 이펙트 (붉은 먹). BloodBuddhaBrain.Cue 신호를 듣고 그림만 그려요 — 판정·타이밍은 게임 코드 그대로.
    //   손바닥 (내려찍기·연타): 바닥에 손바닥 그림자가 짙어지며 내려와 → 떨어지는 순간 붉은 먹 손자국이 찍히고 바래요
    //   염불: 바닥에 붉은 만다라 인장이 돌며 피어나고 → 떨어지면 번쩍 커지며 사라져요
    //   피의 비: 바닥에 옅은 핏자국 예고 → 마지막에 하늘에서 핏방울이 떨어져 → 피가 튀어요
    //   팔 휘두르기: 휘두를 때마다 부채꼴 붉은 붓 획이 쓸고 지나가요
    //   혈수: 핏빛 손이 앞으로 쭉 뻗었다가 돌아와요
    //   2페이즈 전환: 불상 발밑으로 큰 피 번짐 + 만다라가 퍼져요
    // 그림: Art/Env/BossFx/Resources/BloodBuddhaFx (스프라이트). 씬에 미리 둘 것 없이 게임이 켜지면 저절로 들어요.
    public class InkBloodBuddhaFx : MonoBehaviour
    {
        static InkBloodBuddhaFx _host;
        static Sprite _palm, _mandala, _drop, _splash, _hand, _slash;
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

        void Rain(BloodBuddhaBrain.FxCue c)
        {
            var mark = Flat(_splash, c.At, c.Radius * 1.6f, UnityEngine.Random.Range(0f, 360f), 0.03f);
            float t = 0f, fall = Mathf.Max(0.05f, c.Time), dropTime = Mathf.Min(0.35f, fall);
            SpriteRenderer drop = null;
            bool splashed = false;
            _live.Add(dt =>
            {
                t += dt;
                if (mark != null && !splashed) mark.color = new Color(0.35f, 0.02f, 0.02f, 0.3f * Mathf.Clamp01(t / fall));
                if (drop == null && !splashed && t >= fall - dropTime) drop = Upright(_drop, c.At + Vector3.up * 7f, 0.9f);
                if (drop != null && !splashed) // 하늘에서 떨어져요
                {
                    float k = Mathf.Clamp01((t - (fall - dropTime)) / dropTime);
                    drop.transform.position = c.At + Vector3.up * Mathf.Lerp(7f, 0.1f, k * k);
                }
                if (!splashed && t >= fall)
                {
                    splashed = true;
                    if (drop != null) Destroy(drop.gameObject);
                    if (mark != null) mark.transform.localScale *= 1.6f; // 피가 튀어요
                }
                if (!splashed) return true;
                float a = t - fall;
                if (mark != null) mark.color = new Color(1f, 1f, 1f, a < 0.4f ? 1f : Mathf.Clamp01(1f - (a - 0.4f) / 0.9f));
                if (a < 1.3f) return true;
                if (mark != null) Destroy(mark.gameObject);
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

        void Grab(BloodBuddhaBrain.FxCue c)
        {
            Vector3 dir = Flatten(c.Dir);
            var sr = Flat(_hand, c.At, Mathf.Max(2.6f, c.Radius * 4f), Yaw(dir), 0.07f);
            if (sr == null) return;
            float t = 0f, length = Mathf.Max(1f, c.Length);
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                // 0.15초 뻗고 · 0.12초 쥐고 · 0.3초 돌아와요
                float reach = t < 0.15f ? t / 0.15f : t < 0.27f ? 1f : 1f - (t - 0.27f) / 0.3f;
                reach = Mathf.Clamp01(reach);
                sr.transform.position = c.At + dir * (length * Mathf.Sin(reach * Mathf.PI * 0.5f)) + Vector3.up * 0.07f;
                sr.color = new Color(1f, 1f, 1f, t < 0.27f ? 1f : reach);
                if (t < 0.57f) return true;
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
        }
    }
}
