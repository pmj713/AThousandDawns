using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 폭발 종류마다 다른 연출 (플레이 테스트: 공용 먹 폭발 하나를 여기저기 돌려 써서 심심해요).
    //   신호: EnemyDirector.Burst · PlayerController.BurstCue (자리, 반경, BurstKind). Default는 예전 공용 먹 폭발(InkProjectiles) 그대로.
    //   Gunpowder 화약: 주황 불덩이가 확 부풀고 검은 연기 · 불티 · 그을린 자국
    //   BloodGlob 핏덩이: 핏물이 바닥에 철퍽 번지고 핏방울이 튀어요 / BloodDeath 혈폭자 죽음: 더 크게 + 붉은 피 안개
    //   Slam 내려찍기 · Quake 진각: 땅이 갈라진 고리 + 흙먼지 + 돌 부스러기 (진각은 더 넓고 누런 흙빛, 진각은 둘레에서 바윗돌이 솟았다 가라앉아요)
    //   Spin 회전베기: 먹 소용돌이 원이 빠르게 돌며 퍼지고 흙먼지가 둥글게
    //   Vajra 금강: 금빛 고리 두 겹이 퍼지고 금빛 반짝이 / Gangi 강기: 쪽빛 기운 고리와 푸른 빛 입자
    //   Lightning 뇌정: 노란 번개 가지가 사방으로 번쩍 / Miasma 마기: 검붉은 마기 고리 + 검은 연기
    //   Water 물: 물결 고리 두 겹 + 물보라 / Shatter 부서짐: 큰 돌 부스러기 · 먼지 · 갈라짐
    //   Echo 잔상: 먹 초승달 넷이 사방으로 벤 자국
    // 그림: PouchFx · SkillFx · TempleFx · BloodBuddhaFx · TombRoomFx · RiverFx · FireFx 스프라이트, 입자는 InkParticles.
    public class InkBurstFx : MonoBehaviour
    {
        const int MaxLive = 300;
        static InkBurstFx _host;
        static Sprite _ring, _glint, _quake, _rock, _mandala, _splash, _drop, _miasma, _ripple, _spray, _qi, _scorch;
        static Material _lineMat;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyDirector.Burst -= OnBurst;
            EnemyDirector.Burst += OnBurst;
            PlayerController.BurstCue -= OnBurst;
            PlayerController.BurstCue += OnBurst;
        }

        static void OnBurst(Vector3 at, float radius, BurstKind kind)
        {
            if (kind == BurstKind.Default) return; // 공용 먹 폭발은 InkProjectiles 그대로
            if (_host == null)
            {
                Load();
                var go = new GameObject("Ink Burst Fx");
                DontDestroyOnLoad(go);
                _host = go.AddComponent<InkBurstFx>();
            }
            if (kind == BurstKind.Vajra || kind == BurstKind.Echo) InkSlashFX.SuppressFrame = Time.frameCount; // 그 자리 범위 원은 먹 궤적 대신 이 연출로
            at.y = 0f;
            radius = Mathf.Max(0.5f, radius);
            _host.Play(at, radius, kind);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void Play(Vector3 at, float r, BurstKind kind)
        {
            switch (kind)
            {
                case BurstKind.Gunpowder:
                    InkParticles.Burst(at + Vector3.up * 0.4f, 26, new Color(1f, 0.85f, 0.4f, 1f), new Color(0.9f, 0.3f, 0.08f, 1f), r * 2.2f, r * 0.9f, 0.45f, -0.2f);
                    InkParticles.Burst(at + Vector3.up * 0.6f, 14, new Color(0.15f, 0.12f, 0.1f, 0.8f), new Color(0.3f, 0.28f, 0.26f, 0.6f), r * 1.2f, r * 1.1f, 1.1f, -0.3f);
                    InkParticles.Burst(at + Vector3.up * 0.3f, 18, new Color(1f, 0.8f, 0.3f, 1f), new Color(0.6f, 0.15f, 0.05f, 1f), r * 4f, 0.08f, 0.7f, 1f);
                    Ground(_scorch, at, r * 2.2f, 2f, Color.white, 0.8f);
                    break;
                case BurstKind.BloodGlob:
                case BurstKind.BloodDeath:
                    bool big = kind == BurstKind.BloodDeath;
                    Ground(_splash, at, r * (big ? 2.6f : 2f), big ? 2.4f : 1.4f, new Color(0.55f, 0.04f, 0.05f, 1f), 0.9f, grow: 0.15f);
                    Drops(at, big ? 14 : 8, r, new Color(0.7f, 0.05f, 0.05f, 1f));
                    if (big) InkParticles.Burst(at + Vector3.up * 0.6f, 16, new Color(0.6f, 0.05f, 0.06f, 0.75f), new Color(0.3f, 0.02f, 0.03f, 0.5f), r * 1.4f, r * 0.9f, 1f, -0.1f);
                    break;
                case BurstKind.Slam:
                case BurstKind.Quake:
                case BurstKind.Shatter:
                    float w = kind == BurstKind.Quake ? 2.6f : kind == BurstKind.Shatter ? 2.2f : 2f;
                    var dust = kind == BurstKind.Quake ? new Color(0.62f, 0.5f, 0.32f, 0.7f) : new Color(0.45f, 0.42f, 0.38f, 0.7f);
                    Ring(_quake, at, r * w, 0.5f, new Color(0.25f, 0.2f, 0.15f, 1f), 0.3f, 0f);
                    InkParticles.Burst(at + Vector3.up * 0.15f, kind == BurstKind.Shatter ? 22 : 16, dust, new Color(dust.r, dust.g, dust.b, 0.4f), r * 1.6f, r * 0.6f, 0.9f, 0.05f, upward: true);
                    Chips(at, kind == BurstKind.Shatter ? 14 : 8, r, kind == BurstKind.Shatter ? 0.3f : 0.18f);
                    if (kind == BurstKind.Quake) Spikes(at, r); // 진각 (철권 붕산권 · 나한): 둘레에서 바윗돌이 솟았다 가라앉아요
                    break;
                case BurstKind.Spin:
                    Ring(_mandala, at, r * 2.2f, 0.4f, new Color(0.1f, 0.08f, 0.07f, 0.85f), 0.5f, -900f);
                    InkParticles.Burst(at + Vector3.up * 0.1f, 14, new Color(0.45f, 0.42f, 0.38f, 0.6f), new Color(0.5f, 0.48f, 0.45f, 0.3f), r * 1.8f, r * 0.5f, 0.7f);
                    break;
                case BurstKind.Vajra:
                    Ring(_ring, at, r * 2.1f, 0.5f, new Color(1f, 0.85f, 0.4f, 1f), 0.25f, 120f);
                    Ring(_ring, at, r * 1.4f, 0.6f, new Color(1f, 0.92f, 0.6f, 0.8f), 0.2f, -160f, delay: 0.1f);
                    Glints(at, 10, r, new Color(1f, 0.88f, 0.45f, 1f));
                    break;
                case BurstKind.Gangi:
                    Ring(_ring, at, r * 2f, 0.4f, new Color(0.45f, 0.7f, 1f, 1f), 0.25f, 200f);
                    InkParticles.Burst(at + Vector3.up * 0.8f, 18, new Color(0.75f, 0.9f, 1f, 1f), new Color(0.3f, 0.5f, 0.95f, 0.8f), r * 2f, 0.2f, 0.5f, 0f, upward: false);
                    break;
                case BurstKind.Lightning:
                    Bolts(at, r);
                    Glints(at, 6, r * 0.6f, new Color(1f, 0.95f, 0.5f, 1f));
                    break;
                case BurstKind.Miasma:
                    Ring(_miasma, at, r * 2.2f, 0.55f, Color.white, 0.25f, 140f);
                    InkParticles.Burst(at + Vector3.up * 0.4f, 16, new Color(0.15f, 0.04f, 0.06f, 0.8f), new Color(0.08f, 0.04f, 0.05f, 0.5f), r * 1.3f, r * 0.9f, 1f, -0.2f);
                    break;
                case BurstKind.Water:
                    Ring(_ripple, at, r * 2f, 0.5f, new Color(0.7f, 0.85f, 1f, 0.9f), 0.3f, 0f);
                    Ring(_ripple, at, r * 1.3f, 0.6f, new Color(0.7f, 0.85f, 1f, 0.7f), 0.25f, 0f, delay: 0.12f);
                    Sprays(at, 6, r);
                    break;
                case BurstKind.Echo:
                    for (int i = 0; i < 4; i++)
                    {
                        var dir = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward;
                        Slash(at + dir * r * 0.5f, dir, r * 1.2f);
                    }
                    break;
            }
        }

        // ── 조각들

        // 바닥에 번지는 자국 (그을음 · 핏물): 퍼졌다가 life 동안 옅어져요
        void Ground(Sprite sprite, Vector3 at, float size, float life, Color color, float alpha, float grow = 0.2f)
        {
            if (_live.Count > MaxLive) return;
            var sr = Flat(sprite, at, size, 0.021f, 3);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / grow)));
                sr.color = new Color(color.r, color.g, color.b, alpha * (1f - Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f))));
                if (t < life) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        // 퍼지는 고리 (spin: 초당 회전, delay: 늦게 시작)
        void Ring(Sprite sprite, Vector3 at, float size, float life, Color color, float height, float spin, float delay = 0f)
        {
            if (_live.Count > MaxLive) return;
            var sr = Flat(sprite, at, size, 0.028f + height * 0.01f, 5);
            if (sr == null) return;
            sr.enabled = delay <= 0f;
            Vector3 unit = sr.transform.localScale;
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                sr.enabled = true;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                if (spin != 0f) sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Drops(Vector3 at, int count, float r, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var v = new Vector3(Mathf.Cos(a) * r * UnityEngine.Random.Range(1.2f, 2.6f), UnityEngine.Random.Range(3f, 5f), Mathf.Sin(a) * r * UnityEngine.Random.Range(1.2f, 2.6f));
                Fly(_drop, at + Vector3.up * 0.4f, v, UnityEngine.Random.Range(0.12f, 0.22f), color, 0.9f);
            }
        }

        void Chips(Vector3 at, int count, float r, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var v = new Vector3(Mathf.Cos(a) * r * UnityEngine.Random.Range(1f, 2.2f), UnityEngine.Random.Range(3f, 5.5f), Mathf.Sin(a) * r * UnityEngine.Random.Range(1f, 2.2f));
                Fly(_rock, at + Vector3.up * 0.2f, v, size * UnityEngine.Random.Range(0.7f, 1.3f), new Color(0.55f, 0.5f, 0.44f, 1f), 1.2f);
            }
        }

        // 진각: 고리 둘레에서 바윗돌이 땅을 뚫고 솟았다가 가라앉아요 (금빛 혜광 금강불괴와 확실히 달라 보이게)
        void Spikes(Vector3 at, float r)
        {
            if (_rock == null) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(r * 4f), 6, 12);
            for (int i = 0; i < n; i++)
            {
                if (_live.Count > MaxLive) return;
                float a = (i + UnityEngine.Random.Range(-0.2f, 0.2f)) / n * Mathf.PI * 2f;
                var foot = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * UnityEngine.Random.Range(0.6f, 0.95f);
                var pivot = new GameObject("Rock Spike").transform;
                pivot.SetParent(transform, false);
                pivot.gameObject.AddComponent<InkFaceCamera>();
                var go = new GameObject("Sprite");
                go.transform.SetParent(pivot, false);
                float w = UnityEngine.Random.Range(0.45f, 0.8f), s = w / Mathf.Max(0.01f, _rock.bounds.size.x);
                go.transform.localScale = new Vector3(s, s * 1.5f, s); // 길쭉한 바윗돌
                float h = _rock.bounds.size.y * s * 1.5f;
                go.transform.localPosition = Vector3.up * h * 0.5f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _rock;
                sr.color = new Color(0.5f, 0.46f, 0.4f, 1f);
                sr.sortingOrder = 6;
                float t = -i * 0.015f;
                pivot.position = foot + Vector3.down * h;
                _live.Add(dt =>
                {
                    if (pivot == null) return false;
                    t += dt;
                    if (t < 0f) return true;
                    float up = t < 0.12f ? t / 0.12f : t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.3f;
                    pivot.position = foot + Vector3.down * h * (1f - Mathf.Clamp01(up));
                    if (t < 0.85f) return true;
                    Destroy(pivot.gameObject);
                    return false;
                });
            }
        }

        void Glints(Vector3 at, int count, float r, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 p = UnityEngine.Random.insideUnitCircle * r;
                var v = new Vector3(p.x, UnityEngine.Random.Range(1.5f, 3f), p.y);
                Fly(_glint, at + new Vector3(p.x * 0.5f, 0.6f, p.y * 0.5f), v, UnityEngine.Random.Range(0.2f, 0.35f), color, 0.6f, gravity: 2f);
            }
        }

        void Sprays(Vector3 at, int count, float r)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * 0.8f;
                Fly(_spray, p, Vector3.up * 0.6f, UnityEngine.Random.Range(0.5f, 0.8f), new Color(0.85f, 0.95f, 1f, 1f), 0.45f, gravity: 0f, bottom: true);
            }
        }

        // 날아가는 조각 하나 (카메라를 봐요): 포물선으로 튀었다 떨어지며 옅어져요
        void Fly(Sprite sprite, Vector3 from, Vector3 v, float width, Color color, float life, float gravity = 12f, bool bottom = false)
        {
            if (sprite == null || _live.Count > MaxLive) return;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = from;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * s;
            if (bottom) go.transform.localPosition = Vector3.up * sprite.bounds.size.y * s * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            float t = 0f, spin = UnityEngine.Random.Range(-500f, 500f);
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                v += Vector3.down * gravity * dt;
                pivot.position += v * dt;
                if (!bottom) go.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                float k = t / life;
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f && pivot.position.y > -0.1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // 뇌정: 지그재그 번개 가지가 사방으로 번쩍였다 사라져요
        void Bolts(Vector3 at, float r)
        {
            if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default"));
            for (int b = 0; b < 5; b++)
            {
                var lr = new GameObject("Bolt").AddComponent<LineRenderer>();
                lr.transform.SetParent(transform, false);
                lr.useWorldSpace = true;
                lr.sharedMaterial = _lineMat;
                lr.alignment = LineAlignment.View;
                lr.positionCount = 6;
                float a = b / 5f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                var p = at + Vector3.up * 0.8f;
                for (int i = 0; i < 6; i++)
                {
                    lr.SetPosition(i, p);
                    p += dir * r * 0.35f + side * UnityEngine.Random.Range(-0.25f, 0.25f) + Vector3.down * 0.12f;
                }
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0.02f));
                lr.sortingOrder = 8;
                float t = 0f;
                _live.Add(dt =>
                {
                    if (lr == null) return false;
                    t += dt;
                    float flick = t < 0.06f || (t > 0.1f && t < 0.15f) ? 1f : 0.35f; // 두 번 번쩍
                    lr.startColor = lr.endColor = new Color(1f, 0.95f, 0.55f, flick * (1f - t / 0.25f));
                    if (t < 0.25f) return true;
                    Destroy(lr.gameObject);
                    return false;
                });
            }
        }

        // 잔상: 먹 초승달 벤 자국
        void Slash(Vector3 at, Vector3 dir, float width)
        {
            if (_qi == null || _live.Count > MaxLive) return;
            var sr = Flat(_qi, at, width, 0.8f, 7);
            if (sr == null) return;
            sr.transform.rotation = Quaternion.Euler(90f, InkSwordQi.Yaw(dir), 0f);
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / 0.3f;
                sr.transform.localScale = unit * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(0.25f, 0.22f, 0.24f, 1f - k);
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float height, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void Load()
        {
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _quake = Resources.Load<Sprite>("SkillFx/QuakeRing");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _mandala = Resources.Load<Sprite>("BloodBuddhaFx/Mandala");
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _miasma = Resources.Load<Sprite>("TombRoomFx/MiasmaRing");
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
            _spray = Resources.Load<Sprite>("RiverFx/Splash");
            _qi = Resources.Load<Sprite>("SkillFx/SwordQi");
            _scorch = Resources.Load<Sprite>("FireFx/FireGround");
        }
    }
}
