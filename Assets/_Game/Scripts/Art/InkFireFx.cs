using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 불길 (플레이 테스트: 주황 원판·카드 불이 2D처럼 보여요) — 그림만, 화상·범위는 게임 코드 그대로.
    //   화염 장판 (FirePatch: 열양 각인 · 염화 호리병) · 열화 장판 (EnemyFirePatch): 단색 원판은 숨기고 그을린 불씨 바닥을 깔고,
    //       그 위에서 입체 불길 입자(InkParticles.Fire)가 솟아요. 장판이 꺼지면 불길이 사그라들어요.
    //   녹림왕 2페이즈 (불타는 죽림):
    //       방 가장자리 불길 띠 (BurningGrove) — 불이 붙으면 단색 띠를 숨기고 그을린 바닥 + 띠 전체에서 솟는 불길·불티·연기. 띠가 좁혀지면 다시 깔아요.
    //       대숲 불꽃 (InkBurningGroveFX 카드 불) — 카드 대신 그 자리에서 입체 불길이 솟아요 (켜지는 차례는 그대로).
    //       녹림왕 몸 · 대도 — 오른손(대도)에서 불길이 일고 몸 둘레로 불티·그을음이 올라요.
    // 바닥 판 높이 0.022는 공격 예고(0.03) 아래예요. 그림: SkillFx/Resources/FireFx (FireGround), 입자는 InkParticles.
    public class InkFireFx : MonoBehaviour
    {
        const float GroundHeight = 0.022f;
        static Sprite _ground;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<Component> _dressed = new HashSet<Component>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>(); // 새로 생긴 씬 루트 (장판이 생긴 그 프레임에 꾸미려고, 루트마다 한 번만 봐요)
        float _scan, _groveScan, _nokrimScan;
        BurningGrove _grove;
        InkBurningGroveFX _groveCards;
        readonly HashSet<GameObject> _cardsDone = new HashSet<GameObject>();
        readonly Transform[] _strips = new Transform[4];
        readonly Vector3[] _stripKey = new Vector3[8];
        readonly GameObject[] _stripFx = new GameObject[4];
        Renderer[] _stripRenderers;
        NokrimBrain _nokrim;
        GameObject _nokrimFire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            _ground = Resources.Load<Sprite>("FireFx/FireGround");
            var go = new GameObject("Ink Fire Fx");
            DontDestroyOnLoad(go);
            go.AddComponent<InkFireFx>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.12f; DressPatches(); } // 루트가 아닌 장판까지 (예비)
            TickGrove(dt);
            InkRoomReset.Tick(_live, dt); // 방 꾸밈이라 방을 옮겨도 지우지 않아요
        }

        // ── 화염 · 열화 장판

        // 그리기 직전(LateUpdate)에 새 장판을 꾸며요 — 장판이 생긴 프레임부터 새 그림만 보여요 (플레이테스트 10차: 혈폭자 장판에 단색 원판이 잠깐 보였어요)
        void LateUpdate() => DressNewPatches();

        // 새 씬 루트만 한 번씩: 장판 컴포넌트가 붙어 있으면 그 프레임에 바로 원판을 숨기고 그을린 불길을 깔아요
        void DressNewPatches()
        {
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots))
            {
                if (root.TryGetComponent<EnemyFirePatch>(out var e) && _dressed.Add(e)) Patch(e);
                else if (root.TryGetComponent<FirePatch>(out var f) && _dressed.Add(f)) Patch(f);
            }
        }

        void DressPatches()
        {
            _dressed.RemoveWhere(c => c == null);
            foreach (var p in FindObjectsByType<FirePatch>()) if (_dressed.Add(p)) Patch(p);
            foreach (var p in FindObjectsByType<EnemyFirePatch>()) if (_dressed.Add(p)) Patch(p);
        }

        void Patch(Component patch)
        {
            var source = patch.GetComponent<Renderer>();
            if (source != null) source.enabled = false; // 단색 원판은 숨겨요
            var at = patch.transform.position;
            float radius = Mathf.Max(0.4f, patch.transform.localScale.x * 0.5f);
            var holder = new GameObject("Fire Patch").transform;
            holder.SetParent(transform, false);
            holder.position = new Vector3(at.x, 0f, at.z);
            var ground = Flat(_ground, at, radius * 2.3f, holder);
            Vector3 unit = ground != null ? ground.transform.localScale : Vector3.one;
            var fire = InkParticles.Fire(holder, Vector3.up * 0.05f, Mathf.Clamp(radius * 0.55f, 0.4f, 0.9f), radius * 1.2f);
            var shape = fire.shape;
            shape.radius = radius * 0.75f; // 장판 둘레 전체에서 솟아요
            var embers = InkParticles.Embers(holder, Vector3.up * 0.1f, radius * 0.7f, radius);
            float t = 0f, fadeOut = -1f, seed = UnityEngine.Random.Range(0f, 10f);
            _live.Add(dt =>
            {
                if (holder == null) return false;
                t += dt;
                if (patch == null && fadeOut < 0f) // 꺼졌어요: 불길은 더 나오지 않고 사그라들어요
                {
                    fadeOut = 0f;
                    fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                float a = Mathf.Clamp01(t / 0.2f);
                if (fadeOut >= 0f) { fadeOut += dt; a *= 1f - Mathf.Clamp01(fadeOut / 0.4f); }
                if (ground != null)
                {
                    ground.transform.Rotate(0f, 0f, 12f * dt, Space.Self);
                    ground.transform.localScale = unit * Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / 0.25f))) * (1f + 0.03f * Mathf.Sin(Time.time * 6f + seed));
                    ground.color = new Color(1f, 1f, 1f, 0.92f * a * (0.9f + 0.1f * Mathf.Sin(Time.time * 9f + seed)));
                }
                if (fadeOut < 1.2f) return true; // 남은 불길 입자가 다 사라질 때까지
                Destroy(holder.gameObject);
                return false;
            });
        }

        // ── 녹림왕 2페이즈

        void TickGrove(float dt)
        {
            TickNokrim(dt);
            if (_grove == null)
            {
                _groveScan -= dt;
                if (_groveScan > 0f) return;
                _groveScan = 1f;
                if (_groveCards == null) _groveCards = FindAnyObjectByType<InkBurningGroveFX>();
                _grove = FindAnyObjectByType<BurningGrove>();
                if (_grove == null) { ClearStrips(); return; }
                _stripRenderers = _grove.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < 4 && i < _grove.transform.childCount; i++) _strips[i] = _grove.transform.GetChild(i);
            }
            if (!_grove.IsBurning) return; // 붙기 전 깜빡이는 예고는 그대로
            foreach (var r in _stripRenderers) if (r != null && r.enabled) r.enabled = false; // 단색 띠는 숨겨요
            for (int i = 0; i < 4; i++)
            {
                var strip = _strips[i];
                if (strip == null) continue;
                Vector3 pos = strip.position, size = strip.localScale;
                if (_stripKey[i * 2] == pos && _stripKey[i * 2 + 1] == size && _stripFx[i] != null) continue;
                _stripKey[i * 2] = pos;
                _stripKey[i * 2 + 1] = size;
                Strip(i, pos, size); // 띠가 좁혀지면 다시 깔아요
            }
            DressGroveCards();
        }

        void Strip(int index, Vector3 center, Vector3 size)
        {
            if (_stripFx[index] != null) Destroy(_stripFx[index]);
            _stripFx[index] = null;
            if (size.x <= 0.05f || size.z <= 0.05f) return;
            var holder = new GameObject("Grove Fire " + index).transform;
            holder.SetParent(transform, false);
            _stripFx[index] = holder.gameObject;
            const float tile = 2.4f;
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / tile)), nz = Mathf.Max(1, Mathf.CeilToInt(size.z / tile));
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var at = center + new Vector3((x + 0.5f) / nx * size.x - size.x * 0.5f, 0f, (z + 0.5f) / nz * size.z - size.z * 0.5f);
                var g = Flat(_ground, at, tile * 1.35f, holder);
                if (g != null) g.color = new Color(1f, 1f, 1f, 0.9f);
            }
            float area = size.x * size.z;
            var c = new Vector3(center.x, 0.05f, center.z);
            var fire = InkParticles.Fire(holder, Vector3.zero, 0.9f, Mathf.Clamp(area / 3f, 2f, 10f)); // 띠 전체에서 불길이 솟아요
            var main = fire.main;
            main.maxParticles = 260;
            InkParticles.EmitBox(fire, c, new Vector3(size.x, 0.1f, size.z), 2.2f);
            var smoke = InkParticles.Smoke(holder, Vector3.zero, 1.1f, Mathf.Clamp(area / 6f, 1f, 6f), 1f);
            InkParticles.EmitBox(smoke, c + Vector3.up * 1.2f, new Vector3(size.x, 0.3f, size.z), 1.2f);
            var embers = InkParticles.Embers(holder, Vector3.zero, 0.5f, Mathf.Clamp(area / 4f, 1f, 8f));
            InkParticles.EmitBox(embers, c, new Vector3(size.x, 0.1f, size.z), 2.6f);
        }

        // 대숲 카드 불 (메인 아트 InkBurningGroveFX가 차례로 켜요): 카드는 숨기고 그 자리에서 입체 불길
        void DressGroveCards()
        {
            if (_groveCards == null || _groveCards.Flames == null) return;
            foreach (var group in _groveCards.Flames)
            {
                if (group == null || !_cardsDone.Add(group)) continue;
                float height = 1.2f;
                foreach (var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    height = Mathf.Max(r.transform.localScale.y, 0.4f); // 카드 키 = 불꽃 키
                    r.enabled = false;
                }
                InkParticles.Fire(group.transform, Vector3.zero, height * 0.55f, 1.2f);
                InkParticles.Smoke(group.transform, Vector3.up * height, height * 0.5f, 0.8f, 1f);
                InkParticles.Embers(group.transform, Vector3.up * height * 0.3f, height * 0.2f, 0.8f);
            }
        }

        // 녹림왕 몸 · 대도: 2페이즈부터 오른손에서 불길, 몸 둘레 불티·그을음
        void TickNokrim(float dt)
        {
            if (_nokrim == null || _nokrim.IsDead)
            {
                if (_nokrimFire != null) { Destroy(_nokrimFire); _nokrimFire = null; }
                _nokrimScan -= dt; // 2페이즈 판정은 _grove 대신 Phase>=2 (불타는 죽림이 없어졌어요) — 녹림왕은 1초마다 한 번만 찾아요
                if (_nokrimScan > 0f) return;
                _nokrimScan = 1f;
                _nokrim = FindAnyObjectByType<NokrimBrain>();
                if (_nokrim == null || _nokrim.IsDead) return;
            }
            if (_nokrimFire != null || _nokrim.Phase < 2) return;
            var hand = FindDeep(_nokrim.transform, "RightHand");
            var body = new GameObject("Nokrim Fire");
            body.transform.SetParent(_nokrim.transform, false);
            _nokrimFire = body;
            float h = Mathf.Max(2f, _nokrim.Height);
            var embers = InkParticles.Embers(body.transform, Vector3.zero, 0.5f, 2f);
            InkParticles.EmitBox(embers, Vector3.up * h * 0.5f, new Vector3(h * 0.4f, h * 0.8f, h * 0.4f), 1.4f);
            var soot = InkParticles.Smoke(body.transform, Vector3.zero, 0.6f, 1.5f, 1f);
            InkParticles.EmitBox(soot, Vector3.up * h * 0.7f, new Vector3(h * 0.35f, h * 0.3f, h * 0.35f), 0.8f);
            if (hand != null) // 대도를 쥔 손에서 불길이 일어요 (손을 따라다녀요)
            {
                var blade = InkParticles.Fire(hand, Vector3.zero, 0.45f, 1.4f);
                InkParticles.EmitBox(blade, Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), 1.6f); // 손 방향과 상관없이 위로 타올라요
            }
        }

        void ClearStrips()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_stripFx[i] != null) Destroy(_stripFx[i]);
                _stripFx[i] = null;
                _strips[i] = null;
            }
            _stripRenderers = null;
            _cardsDone.Clear();
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name || root.name.EndsWith(":" + name)) return root;
            foreach (Transform child in root) { var t = FindDeep(child, name); if (t != null) return t; }
            return null;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, Transform parent)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, GroundHeight, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            return sr;
        }
    }
}
