using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마총 방 구성 (RoomRun.Layout과 같은 방식): 빈 묘실 · 무너진 관 · 마기 기둥 · 혈지.
    //   무너진 관: 석관 넷, 엄폐물 (충돌체).
    //   마기 기둥: 검은 돌기둥 둘 (충돌체). 싸우는 동안 tomb.miasma_interval초마다 예고 tomb.miasma_fuse초 뒤 둘레 tomb.miasma_radius칸에 마기 (플레이어만 맞아요).
    //   혈지: 핏물 웅덩이 둘 (바닥, 충돌체 없음). 위에 선 적은 초당 최대 체력 tomb.pool_heal씩 회복해요. 끌어내서 싸워요.
    public sealed partial class RoomRun
    {
        public static event Action<EnemyBrain> BloodPoolHealed;          // 혈지에서 회복이 시작될 때 (들어갈 때마다 한 번)
        public static event Action<Vector3, float, float> MiasmaPulse;    // 마기 분출 예고 (자리, 반경, 예고 시간)

        readonly HashSet<EnemyBrain> _poolHealing = new HashSet<EnemyBrain>();
        float _miasmaTimer;

        public int MiasmaPulses { get; private set; } // 테스트용

        void BuildTombLayout(int layout)
        {
            _poolHealing.Clear();
            _miasmaTimer = GameConfig.GetOptional("tomb.miasma_first", 3f);
            switch (layout)
            {
                case 1: // 무너진 관
                    AddSolid(FeatureKind.Coffin, new Vector2(-8f, 2.1f), new Vector2(2f, 1f), 1f);
                    AddSolid(FeatureKind.Coffin, new Vector2(8f, -2.1f), new Vector2(2f, 1f), 1f);
                    AddSolid(FeatureKind.Coffin, new Vector2(2.2f, 4.6f), new Vector2(1f, 2f), 1f);
                    AddSolid(FeatureKind.Coffin, new Vector2(-2.2f, -4.6f), new Vector2(1f, 2f), 1f);
                    break;
                case 2: // 마기 기둥
                    AddSolid(FeatureKind.MiasmaPillar, new Vector2(-6.3f, 4.8f), new Vector2(1f, 1f), 2.6f);
                    AddSolid(FeatureKind.MiasmaPillar, new Vector2(6.3f, -4.8f), new Vector2(1f, 1f), 2.6f);
                    break;
                case 3: // 혈지
                    AddBloodPool(new Vector2(-6.5f, -2.9f), new Vector2(3.5f, 2.5f));
                    AddBloodPool(new Vector2(6.5f, 2.9f), new Vector2(3.5f, 2.5f));
                    break;
            }
        }

        void AddBloodPool(Vector2 at, Vector2 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "혈지";
            var col = go.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            at = ScaleToRoom(at); // 작은 방 기준 자리를 지금 방 크기로
            Vector3 center = Combat.center + new Vector3(at.x, 0f, at.y);
            go.transform.position = center + Vector3.up * 0.02f;
            go.transform.localScale = new Vector3(size.x, 0.02f, size.y);
            var r = go.GetComponent<Renderer>();
            r.material.color = new Color(0.35f, 0.04f, 0.05f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _features.Add(new RoomFeature { Kind = FeatureKind.BloodPool, Center = center, Size = size, Go = go });
        }

        static bool Inside(RoomFeature f, Vector3 p) =>
            Mathf.Abs(p.x - f.Center.x) <= f.Size.x * 0.5f && Mathf.Abs(p.z - f.Center.z) <= f.Size.y * 0.5f;

        public bool InBloodPool(Vector3 p)
        {
            foreach (var f in _features) if (f.Kind == FeatureKind.BloodPool && Inside(f, p)) return true;
            return false;
        }

        // 층 방에서 매 프레임 (TickWater에서)
        void TickTombFeatures(float dt)
        {
            if (_region < 3 || _features.Count == 0) return;
            TickBloodPools(dt);
            TickMiasma(dt);
        }

        void TickBloodPools(float dt)
        {
            if (director == null) return;
            float heal = GameConfig.GetOptional("tomb.pool_heal", 0.05f);
            foreach (var e in director.Enemies)
            {
                if (e == null || e.IsDead) continue;
                bool inPool = InBloodPool(e.transform.position);
                if (!inPool)
                {
                    _poolHealing.Remove(e);
                    continue;
                }
                if (e.Damageable.Health >= e.Damageable.MaxHealth) continue;
                if (_poolHealing.Add(e)) BloodPoolHealed?.Invoke(e);
                e.Damageable.Heal(e.Damageable.MaxHealth * heal * dt);
            }
        }

        void TickMiasma(float dt)
        {
            if (!_fighting || director == null) return;
            _miasmaTimer -= dt;
            if (_miasmaTimer > 0f) return;
            _miasmaTimer = GameConfig.GetOptional("tomb.miasma_interval", 6f);
            float radius = GameConfig.GetOptional("tomb.miasma_radius", 2.5f), fuse = GameConfig.GetOptional("tomb.miasma_fuse", 1f);
            float damage = GameConfig.GetOptional("tomb.miasma_damage", 12f) * RegionAttack * KarmaAttack;
            foreach (var f in _features)
            {
                if (f.Kind != FeatureKind.MiasmaPillar) continue;
                BloodBurst.Create(director, f.Center, radius, fuse, damage, null, 0f, AThousandDawns.Combat.BurstKind.Miasma); // 마기 기둥
                MiasmaPulse?.Invoke(f.Center, radius, fuse);
                MiasmaPulses++;
            }
        }
    }
}
