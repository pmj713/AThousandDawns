using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 일반 · 엘리트 적 공격 그림 (일반 · 엘리트 겹침 점검, 사용자 승인) — 그림만, 판정 · 수치는 게임 코드 그대로.
    //   일반 근접 한 방 (EnemyBrain.RegularStrike, 판정이 처음 나는 순간):
    //     부채꼴 = 적마다 색이 다른 검기 초승달 (방패 산적 쇳빛 · 등패수 등나무빛 · 마교도 핏빛 · 파계승 금빛 · 수적 졸개 물빛)
    //     직선 = 산적 졸개 내려찍기는 먹빛 내려찍기 자국 + 흙먼지 / 스켈레톤 방패 밀치기는 뼛빛 충격 띠 + 뼛가루
    //     원 = 고블린 뛰어 내려치기는 흙빛 착지 고리 + 흙먼지 / 가고일 날개 내려찍기는 잿빛 돌 고리 + 바람
    //   오크 땅 내려찍기 (OrcBrain.Shockwave): 밝은 충격 띠와 흙 · 돌이 솟는 앞머리가 직선으로 달려 나가고, 지나간 자리에 먹빛 금이 남아요
    //   고블린 주술사 회복 (Healed): 받은 적 몸에서 연두 · 금빛 티끌이 솟고 발밑 고리 / 저주 장판 (CursePlaced): 연두 포자가 확 퍼져요 (장판 그림은 InkFantasyFx)
    //   화염구 정예 불화살 (FireboltShot → 투사체 "불화살"): 단색 구는 숨기고 길쭉한 불덩이 + 불꼬리 + 불티
    //   슬라임 점액 (PuddleMade → "점액"): 단색 원판은 숨기고 반들거리는 연두 점액 웅덩이가 거품을 내다 마르며 사라져요
    //   고블린 궁수 곡사 착지 (LobLanded): 흙먼지 + 작은 고리 (맞으면 붉은 불티) / 임프 화염구 폭발 (FireballExploded): 불덩이가 터지고 불빛 번쩍 + 그을음
    //   (곡사 화살 · 화염구 그림은 InkProjectiles, 고블린 단검꾼 단검도 InkProjectiles, 늑대인간 할퀴기는 InkBeastFx)
    // 그림: SkillFx/SwordQi, PouchFx/Ring, FxArt/InkMist, FantasyFx/Slime.
    public class InkEnemyStrikeFx : MonoBehaviour
    {
        static InkEnemyStrikeFx _host;
        static Sprite _qi, _ring, _glow, _slime;
        static Material _trailMat;
        static bool _loaded;
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.RegularStrike -= OnStrike; EnemyBrain.RegularStrike += OnStrike;
            OrcBrain.Shockwave -= OnShockwave; OrcBrain.Shockwave += OnShockwave;
            GoblinShamanBrain.Healed -= OnHealed; GoblinShamanBrain.Healed += OnHealed;
            GoblinShamanBrain.CursePlaced -= OnCursePlaced; GoblinShamanBrain.CursePlaced += OnCursePlaced;
            EnemyBrain.FireboltShot -= OnFirebolt; EnemyBrain.FireboltShot += OnFirebolt;
            SlimeBrain.PuddleMade -= OnPuddle; SlimeBrain.PuddleMade += OnPuddle;
            GoblinArcherBrain.LobLanded -= OnLobLanded; GoblinArcherBrain.LobLanded += OnLobLanded;
            ImpBrain.FireballExploded -= OnImpBlast; ImpBrain.FireballExploded += OnImpBlast;
        }

        static InkEnemyStrikeFx Host()
        {
            if (!_loaded)
            {
                _loaded = true;
                _qi = Resources.Load<Sprite>("SkillFx/SwordQi");
                _ring = Resources.Load<Sprite>("PouchFx/Ring");
                _slime = Resources.Load<Sprite>("FantasyFx/Slime");
                var tex = Resources.Load<Texture2D>("FxArt/InkMist");
                if (tex != null) _glow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (_host == null) _host = new GameObject("Ink Enemy Strike Fx").AddComponent<InkEnemyStrikeFx>();
            return _host;
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update() => InkRoomReset.Tick(_live, Time.deltaTime, transform); // 방을 옮기면 제 밑의 연출을 지워요

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

        // PouchFx/Ring 그림의 가장 진한 선은 그림 폭의 0.34배 반지름에 있어요 → 반경 r 고리 = 폭 r × 2.9 (코드 점검: 예전 2.1배는 판정의 71%로 작게 보였어요)
        const float RingScale = 2.9f;

        static Color StrikeColor(EnemyKind kind) => kind switch
        {
            EnemyKind.Shield => new Color(0.65f, 0.75f, 0.85f),     // 쇳빛
            EnemyKind.Rattan => new Color(0.85f, 0.7f, 0.4f),       // 등나무빛
            EnemyKind.Cultist => new Color(0.9f, 0.15f, 0.2f),      // 핏빛
            EnemyKind.Monk => new Color(1f, 0.8f, 0.35f),           // 금빛
            EnemyKind.Pirate => new Color(0.4f, 0.8f, 0.9f),        // 물빛
            EnemyKind.Grunt => new Color(0.22f, 0.16f, 0.12f),      // 먹빛
            EnemyKind.Skeleton => new Color(0.92f, 0.88f, 0.76f),   // 뼛빛
            EnemyKind.Goblin => new Color(0.6f, 0.5f, 0.3f),        // 흙빛
            EnemyKind.Gargoyle => new Color(0.6f, 0.6f, 0.62f),     // 잿빛 돌
            _ => new Color(0.9f, 0.85f, 0.75f),
        };

        // ── 일반 근접 한 방

        static void OnStrike(EnemyBrain who, Vector3 from, Vector3 dir, float length, float width, int shape)
        {
            if (who == null) return;
            var h = Host();
            from.y = 0f;
            dir = Flat(dir);
            var c = StrikeColor(who.Kind);
            switch (shape)
            {
                case 0: h.Crescent(from + dir * length * 0.5f, dir, Mathf.Max(1f, length) * 1.7f, c); break;
                case 1:
                    h.Streak(from, dir, length, width, c, 0.05f); // 예고 띠와 같은 바닥 높이 (뒤쪽 가운데부터 length · 폭 width)
                    InkParticles.Burst(from + dir * length + Vector3.up * 0.15f, 10, new Color(c.r, c.g, c.b, 0.9f), new Color(0.5f, 0.45f, 0.4f, 0f), 1.6f, 0.25f, 0.5f, 0.6f);
                    break;
                default:
                    h.Ring(from, length * RingScale, 0.4f, new Color(c.r, c.g, c.b, 0.85f)); // 고리 그림의 진한 선이 판정 반경에 오게
                    var dust = who.Kind == EnemyKind.Gargoyle ? new Color(0.85f, 0.88f, 0.9f, 0.6f) : new Color(0.62f, 0.5f, 0.34f, 0.8f);
                    InkParticles.Burst(from + Vector3.up * 0.15f, 14, dust, new Color(dust.r, dust.g, dust.b, 0f), length * 2.2f, 0.35f, 0.55f, 0f);
                    break;
            }
        }

        // 검기 초승달 (볼록한 쪽이 앞으로), 0.25초에 퍼지며 옅어져요
        void Crescent(Vector3 at, Vector3 dir, float width, Color color)
        {
            if (_qi == null) return;
            var go = new GameObject("Strike Arc");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.85f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, InkSwordQi.Yaw(dir), 0f);
            float unit = width / Mathf.Max(0.01f, _qi.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _qi;
            sr.sortingOrder = 7;
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float k = t / 0.25f;
                go.transform.localScale = Vector3.one * unit * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, 0.9f * (1f - k * k));
                if (k < 1f) return true;
                Destroy(go);
                return false;
            });
        }

        // 바닥에 누운 띠 (뒤쪽 가운데 from에서 dir로 length, 폭 width): 0.06초에 뻗고 0.35초에 옅어져요
        void Streak(Vector3 from, Vector3 dir, float length, float width, Color color, float height, float grow = 0.06f, float fade = 0.35f)
        {
            if (_glow == null) return;
            var go = new GameObject("Strike Streak");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glow;
            sr.sortingOrder = 6;
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x), t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float l = length * Mathf.Clamp01(t / grow);
                go.transform.position = new Vector3(from.x, height, from.z) + dir * (l * 0.5f);
                go.transform.localScale = new Vector3(width * 1.3f * unit, Mathf.Max(0.05f, l) * 1.15f * unit, 1f);
                float k = Mathf.Clamp01((t - grow) / fade);
                sr.color = new Color(color.r, color.g, color.b, 0.85f * (1f - k * k));
                if (k < 1f) return true;
                Destroy(go);
                return false;
            });
        }

        // 충격파 앞머리 띠 (바닥에 누운 빛 덩이, 판정 폭보다 조금 넓게 · 진행 방향으로 짧게)
        SpriteRenderer FrontGlow(float width)
        {
            if (_glow == null) return null;
            var go = new GameObject("Shockwave Front");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glow;
            sr.sortingOrder = 7;
            float unit = 1f / Mathf.Max(0.01f, _glow.bounds.size.x);
            go.transform.localScale = new Vector3(width * 1.8f * unit, 1.1f * unit, 1f);
            return sr;
        }

        // 바닥 고리가 size까지 퍼지며 옅어져요
        void Ring(Vector3 at, float size, float life, Color color)
        {
            if (_ring == null) return;
            var go = new GameObject("Strike Ring");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.06f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _ring;
            sr.sortingOrder = 6;
            float unit = size / Mathf.Max(0.01f, _ring.bounds.size.x), t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float k = t / life;
                go.transform.localScale = Vector3.one * unit * Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(go);
                return false;
            });
        }

        // ── 오크 땅 내려찍기: 직선 충격파 (발밑에서 length칸, 폭 width, 초당 speed칸)

        static void OnShockwave(OrcBrain orc, Vector3 from, Vector3 dir, float length, float width, float speed)
        {
            var h = Host();
            from.y = 0f;
            dir = Flat(dir);
            // 플레이테스트 11-27 (죽림에서 잘 안 보였어요 — 갈색 금·흙먼지가 초록·흙 바닥에 묻혔어요): 명암을 벌려요.
            //   지나간 자리 금은 거의 먹빛으로 진하게, 앞머리엔 밝은 주황·크림빛 충격 띠가 함께 달리고, 흙먼지도 밝은 크림색
            float life = length / Mathf.Max(0.1f, speed);
            h.Streak(from, dir, length, width * 0.9f, new Color(0.09f, 0.05f, 0.03f), 0.035f, life, 0.7f); // 지나간 자리 금 (먹빛): 앞머리와 같은 속도로 늘어나요
            float t = 0f, step = 0f;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            var front = h.FrontGlow(width);
            h._live.Add(dt =>
            {
                t += dt;
                step -= dt;
                Vector3 p = from + dir * Mathf.Min(length, speed * t);
                if (front != null) // 앞머리 충격 띠: 판정 폭만큼, 끝에 닿으면 빠르게 사그라져요
                {
                    front.transform.position = new Vector3(p.x, 0.05f, p.z);
                    front.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
                    float fade = Mathf.Clamp01((t - life) / 0.15f);
                    front.color = new Color(1f, 0.78f, 0.38f, 0.95f * (1f - fade));
                }
                if (step <= 0f && t <= life) // 앞머리에서 흙 · 돌이 솟아요
                {
                    step = 0.04f;
                    InkParticles.Burst(p + side * UnityEngine.Random.Range(-0.3f, 0.3f) * width + Vector3.up * 0.1f, 7, new Color(0.98f, 0.9f, 0.68f, 1f), new Color(0.8f, 0.66f, 0.45f, 0f), 2.6f, 0.34f, 0.5f, 1.8f);
                    InkParticles.Burst(p + Vector3.up * 0.1f, 3, new Color(0.2f, 0.17f, 0.15f, 1f), new Color(0.12f, 0.1f, 0.08f, 0f), 3f, 0.16f, 0.45f, 3.5f); // 돌 조각 (진하게)
                }
                if (t <= life + 0.2f) return true;
                if (front != null) Destroy(front.gameObject);
                return false;
            });
        }

        // ── 고블린 주술사

        static void OnHealed(GoblinShamanBrain shaman, EnemyBrain target)
        {
            if (target == null) return;
            var h = Host();
            Vector3 at = target.transform.position;
            h.Ring(at, 2.2f, 0.5f, new Color(0.6f, 1f, 0.4f, 0.85f));
            InkParticles.Burst(at + Vector3.up * 0.4f, 16, new Color(0.75f, 1f, 0.55f, 1f), new Color(1f, 0.85f, 0.35f, 0f), 0.8f, 0.18f, 1f, -1.6f); // 연두 · 금빛 티끌이 솟아요
        }

        static void OnCursePlaced(GoblinShamanBrain shaman, Vector3 at, float radius)
        {
            var h = Host();
            at.y = 0f;
            h.Ring(at, radius * RingScale, 0.45f, new Color(0.55f, 0.95f, 0.3f, 0.8f));
            InkParticles.Burst(at + Vector3.up * 0.2f, 14, new Color(0.7f, 1f, 0.35f, 1f), new Color(0.25f, 0.1f, 0.35f, 0f), radius * 1.3f, 0.25f, 0.9f, -0.8f);
        }

        // ── 화염구 정예 불화살: 막 쏜 "불화살" 루트를 찾아 (쏜 순간 한 번만 씬 루트를 훑어요) 단색 구를 숨기고 불덩이를 입혀요

        static void OnFirebolt(EnemyBrain elite, Vector3 from, Vector3 dir)
        {
            var h = Host();
            from.y = 0f;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var p = root.transform.position;
                p.y = 0f;
                if ((p - from).sqrMagnitude > 4f || root.name != "불화살" || root.transform.Find("Ink Firebolt") != null) continue;
                h.DressFirebolt(root.transform);
                return;
            }
        }

        void DressFirebolt(Transform bolt)
        {
            foreach (var r in bolt.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var visual = new GameObject("Ink Firebolt").transform;
            visual.SetParent(bolt, false);
            var s = bolt.lossyScale;
            visual.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x), 1f / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));
            var face = new GameObject("Glow").transform;
            face.SetParent(visual, false);
            face.gameObject.AddComponent<InkFaceCamera>();
            if (_glow != null)
            {
                Orb(face, 1.1f, new Color(1f, 0.35f, 0.06f, 0.8f), 9);
                Orb(face, 0.5f, new Color(1f, 0.92f, 0.6f, 1f), 10);
            }
            InkParticles.Fire(visual, Vector3.zero, 0.35f, 2.5f);
            InkParticles.Embers(visual, Vector3.zero, 0.25f, 2f);
            var trail = visual.gameObject.AddComponent<TrailRenderer>();
            if (_trailMat == null) _trailMat = new Material(Shader.Find("Sprites/Default")) { name = "Firebolt Trail" }; // 불화살마다 새 재질을 만들지 않게
            trail.sharedMaterial = _trailMat;
            trail.time = 0.25f;
            trail.widthMultiplier = 0.45f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.8f, 0.35f), 0f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Orb(Transform parent, float size, Color color, int order)
        {
            var go = new GameObject("Orb");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, _glow.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glow;
            sr.color = color;
            sr.sortingOrder = order;
        }

        // ── 슬라임 점액 웅덩이: 막 깔린 "점액"을 찾아 단색 원판을 숨기고, 반들거리는 연두 점액이 거품을 내다 마르며 사라져요

        static void OnPuddle(SlimeBrain slime, Vector3 at, float radius, float life)
        {
            var h = Host();
            at.y = 0f;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) // 깔리는 순간 한 번만
            {
                var p = root.transform.position;
                p.y = 0f;
                if ((p - at).sqrMagnitude > 0.25f || root.name != "점액") continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
            if (_glow == null) return;
            var pool = new GameObject("Slime Puddle");
            pool.transform.SetParent(h.transform, false);
            pool.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f));
            var sr = pool.AddComponent<SpriteRenderer>();
            sr.sprite = _slime != null ? _slime : _glow;
            sr.sortingOrder = 4;
            float unit = radius * 2.3f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            var shine = new GameObject("Shine").AddComponent<SpriteRenderer>();
            shine.transform.SetParent(pool.transform, false);
            shine.transform.localPosition = new Vector3(-0.15f, 0.2f, -0.01f) * (1f / Mathf.Max(0.01f, unit));
            shine.sprite = _glow;
            shine.sortingOrder = 5;
            float t = 0f, bubble = 0f;
            h._live.Add(dt =>
            {
                if (pool == null) return false;
                t += dt;
                float grow = Mathf.Sqrt(Mathf.Clamp01(t / 0.2f)), fade = Mathf.Clamp01((life - t) / 0.6f);
                pool.transform.localScale = Vector3.one * unit * grow;
                sr.color = new Color(0.55f, 0.95f, 0.35f, 0.85f * fade);
                shine.transform.localScale = Vector3.one * 0.35f;
                shine.color = new Color(0.9f, 1f, 0.8f, 0.35f * fade);
                bubble -= dt;
                if (bubble <= 0f && t < life - 0.5f) // 거품
                {
                    bubble = 0.25f;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 0.8f;
                    InkParticles.Burst(at + new Vector3(r.x, 0.08f, r.y), 2, new Color(0.75f, 1f, 0.55f, 0.9f), new Color(0.4f, 0.8f, 0.3f, 0f), 0.3f, 0.12f, 0.4f, -0.4f);
                }
                if (t < life) return true;
                Destroy(pool);
                return false;
            });
        }

        // ── 고블린 궁수 곡사 착지 · 임프 화염구 폭발

        static void OnLobLanded(LobbedArrow arrow, Vector3 at, float radius, bool hit)
        {
            var h = Host();
            at.y = 0f;
            h.Ring(at, radius * RingScale, 0.35f, new Color(0.7f, 0.6f, 0.4f, 0.8f));
            InkParticles.Burst(at + Vector3.up * 0.1f, 10, new Color(0.62f, 0.5f, 0.34f, 0.9f), new Color(0.45f, 0.36f, 0.25f, 0f), radius * 1.6f, 0.25f, 0.45f, 0.8f);
            if (hit) InkParticles.Burst(at + Vector3.up * 0.8f, 8, new Color(1f, 0.5f, 0.3f, 1f), new Color(0.7f, 0.1f, 0.05f, 0f), 2f, 0.1f, 0.3f, 1f);
        }

        static void OnImpBlast(ImpFireball ball, Vector3 at, float radius, bool hit)
        {
            var h = Host();
            at.y = 0f;
            h.Ring(at, radius * RingScale, 0.45f, new Color(1f, 0.55f, 0.2f, 0.9f));
            InkParticles.Burst(at + Vector3.up * 0.5f, 22, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.3f, 0.05f, 0.5f), radius * 2.4f, 0.3f, 0.6f, -0.4f);
            InkParticles.Burst(at + Vector3.up * 0.7f, 10, new Color(0.3f, 0.25f, 0.22f, 0.7f), new Color(0.2f, 0.18f, 0.16f, 0f), radius, 0.6f, 0.9f, -0.5f); // 그을음
            var flash = new GameObject("Imp Blast Light").AddComponent<Light>();
            flash.transform.SetParent(h.transform, false);
            flash.transform.position = at + Vector3.up;
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.5f, 0.2f);
            flash.range = radius * 4f;
            flash.shadows = LightShadows.None;
            float t = 0f;
            h._live.Add(dt =>
            {
                if (flash == null) return false;
                t += dt;
                flash.intensity = 4f * Mathf.Clamp01(1f - t / 0.35f);
                if (t < 0.35f) return true;
                Destroy(flash.gameObject);
                return false;
            });
        }
    }

    // 날아가는 방향으로 머리(+Z)를 돌려요 (곡사 화살처럼 위치만 움직이는 투사체)
    public class InkFaceVelocity : MonoBehaviour
    {
        Vector3 _last;
        bool _has;

        void LateUpdate()
        {
            var p = transform.position;
            if (_has)
            {
                var v = p - _last;
                if (v.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(v.normalized);
            }
            _last = p;
            _has = true;
        }
    }
}
