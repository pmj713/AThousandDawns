using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 차원 겹침 소품: 무협 방 안에 이계(판타지) 물건이 잘못 끼어들어 있어요. 지역 방 구성(RoomRun.Layout) 위에 더 놓여요.
    //   무너진 성벽 조각 · 기사 석상 · 룬 기둥: 사람 · 화살 · 돌진을 막는 엄폐물
    //   마나 수정: 공격하면 깨지며 둘레의 적에게 마력이 터져요 (rift.crystal_damage × 지역 배율, 플레이어는 안 맞아요)
    // 깊이 갈수록 더 많이 섞여요 (rift.count.r0~r3 + rift.extra_chance로 하나 더). 방 구성과 같은 방에서만 (입문 죽림 1층 · 고정 웨이브 방은 빼요).
    // 문 사이 십자 길 · 방 가운데 · 다른 소품 · 물 · 혈지 · 대나무 기둥 · 풀숲을 비켜 놓아요. 판 시드와 방 자리로 정해서 _rng를 건드리지 않아요.
    public sealed partial class RoomRun
    {
        public static int RiftOverride = -1; // 테스트용 (방마다 놓을 수, -1이면 시트)
        public static int RiftKindOverride = -1; // 테스트용 (FeatureKind 번호로 한 가지만)
        public static event Action<RoomRun, Vector3, float> CrystalBurst; // 마나 수정이 깨져 터짐 (자리, 반경)

        // 작은 방 기준 고정 대나무 기둥 · 청죽사 풀숲 자리 (씬에 있는 것과 같아요)
        static readonly Vector2[] RiftAvoid = { new Vector2(-5.5f, 3f), new Vector2(5.5f, -3f), new Vector2(-7f, -3.5f), new Vector2(7f, 3.5f), new Vector2(3.5f, 3f), new Vector2(-3.5f, -3f),
            new Vector2(-7.5f, 4.3f), new Vector2(7.5f, -4.3f), new Vector2(-3f, 4.3f), new Vector2(3f, -4.3f) };
        readonly List<EnemyBrain> _crystalTargets = new List<EnemyBrain>();
        readonly List<Vector2> _riftSpots = new List<Vector2>();

        public static bool IsRift(FeatureKind kind) => kind >= FeatureKind.Rampart;

        public int RiftPropCount
        {
            get
            {
                int n = 0;
                foreach (var f in _features) if (IsRift(f.Kind)) n++;
                return n;
            }
        }

        static string RiftName(FeatureKind kind) => kind switch
        {
            FeatureKind.Rampart => "무너진 성벽 조각",
            FeatureKind.KnightStatue => "기사 석상",
            FeatureKind.RunePillar => "룬 기둥",
            FeatureKind.ManaCrystal => "마나 수정",
            _ => null,
        };

        static Color? RiftColor(FeatureKind kind) => kind switch
        {
            FeatureKind.Rampart => new Color(0.62f, 0.6f, 0.55f),
            FeatureKind.KnightStatue => new Color(0.55f, 0.6f, 0.58f),
            FeatureKind.RunePillar => new Color(0.4f, 0.42f, 0.6f),
            FeatureKind.ManaCrystal => new Color(0.45f, 0.55f, 1f),
            _ => null,
        };

        static (Vector2 size, float height) RiftShape(FeatureKind kind) => kind switch
        {
            FeatureKind.Rampart => (new Vector2(2.4f, 0.6f), 1.6f),
            FeatureKind.KnightStatue => (new Vector2(1f, 1f), 2.6f),
            FeatureKind.RunePillar => (new Vector2(0.8f, 0.8f), 2.8f),
            _ => (new Vector2(0.9f, 0.9f), 1.4f),
        };

        int RiftCountFor(System.Random rng)
        {
            if (RiftOverride >= 0) return RiftOverride;
            int n = Mathf.RoundToInt(GameConfig.GetOptional($"rift.count.r{Mathf.Clamp(_region, 0, 3)}", 1f + _region * 0.7f));
            if (rng.NextDouble() < GameConfig.GetOptional("rift.extra_chance", 0.35f)) n++;
            return Mathf.Max(0, n);
        }

        FeatureKind PickRiftKind(System.Random rng)
        {
            if (RiftKindOverride >= 0) return (FeatureKind)RiftKindOverride;
            float wr = GameConfig.GetOptional("rift.weight.rampart", 3f), ws = GameConfig.GetOptional("rift.weight.statue", 2f);
            float wp = GameConfig.GetOptional("rift.weight.rune", 2f), wc = GameConfig.GetOptional("rift.weight.crystal", 2f);
            double roll = rng.NextDouble() * Mathf.Max(0.01f, wr + ws + wp + wc);
            if ((roll -= wr) < 0) return FeatureKind.Rampart;
            if ((roll -= ws) < 0) return FeatureKind.KnightStatue;
            if ((roll -= wp) < 0) return FeatureKind.RunePillar;
            return FeatureKind.ManaCrystal;
        }

        // PlaceLayout에서 지역 방 구성 다음에
        void PlaceRiftProps(FloorRoom room)
        {
            var rng = new System.Random(Seed ^ (room.Cell.x * 49979687) ^ (room.Cell.y * 15485863) ^ (_floor * 32452843) ^ (_region * 7919));
            int count = RiftCountFor(rng);
            if (count <= 0) return;
            Physics.SyncTransforms(); // 방금 놓은 방 구성 충돌체도 IsFree가 보게
            var bushes = FindObjectsByType<AmbushBush>(FindObjectsSortMode.None); // 방에 들어갈 때 한 번만
            int placed = 0;
            // 작은 방(20×12) 기준 격자 자리를 섞어 차례로 훑어요 (빈자리가 좁은 방에서도 찾게). 벽가에서 조금 떨어져, 십자 길(|x| < 1.8 · |z| < 1.6)과 가운데를 비켜서
            _riftSpots.Clear();
            for (float x = 2.2f; x <= 8.6f; x += 0.4f)
                for (float z = 1.8f; z <= 4.9f; z += 0.4f)
                    for (int q = 0; q < 4; q++) _riftSpots.Add(new Vector2((q & 1) == 0 ? x : -x, (q & 2) == 0 ? z : -z));
            for (int i = _riftSpots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_riftSpots[i], _riftSpots[j]) = (_riftSpots[j], _riftSpots[i]);
            }
            for (int n = 0; n < count; n++)
            {
                var kind = PickRiftKind(rng);
                var (size, height) = RiftShape(kind);
                if (kind == FeatureKind.Rampart && rng.Next(2) == 0) size = new Vector2(size.y, size.x); // 세로로 놓인 조각
                for (int s = 0; s < _riftSpots.Count; s++)
                {
                    var at = _riftSpots[s];
                    if (Mathf.Abs(at.x) + size.x * 0.5f > 9f || Mathf.Abs(at.y) + size.y * 0.5f > 5.2f) continue; // 벽 안쪽
                    if (Mathf.Abs(at.x) - size.x * 0.5f < 1.8f || Mathf.Abs(at.y) - size.y * 0.5f < 1.6f) continue;
                    if (NearFixed(at, size)) continue;
                    Vector2 scaled = ScaleToRoom(at);
                    Vector3 world = Combat.center + new Vector3(scaled.x, 0f, scaled.y);
                    if (!RiftSpotFree(world, size, bushes)) continue;
                    AddSolid(kind, at, size, height);
                    if (kind == FeatureKind.ManaCrystal) MakeCrystal(_features[_features.Count - 1]);
                    Physics.SyncTransforms();
                    _riftSpots.RemoveAt(s);
                    placed++;
                    break;
                }
            }
            if (placed > 0) Notify("rift_props", "차원 겹침: 방에 이계의 잔해가 끼어들었어요 · 성벽 조각 · 석상 · 룬 기둥은 엄폐물, 마나 수정은 공격하면 둘레 적에게 터져요");
        }

        static bool NearFixed(Vector2 at, Vector2 size)
        {
            foreach (var p in RiftAvoid)
                if (Mathf.Abs(p.x - at.x) < size.x * 0.5f + 1f && Mathf.Abs(p.y - at.y) < size.y * 0.5f + 1f) return true;
            return false;
        }

        bool RiftSpotFree(Vector3 world, Vector2 size, AmbushBush[] bushes)
        {
            const float margin = 0.8f; // 소품 사이로 지나갈 틈
            foreach (var f in _features)
                if (Mathf.Abs(world.x - f.Center.x) <= (f.Size.x + size.x) * 0.5f + margin && Mathf.Abs(world.z - f.Center.z) <= (f.Size.y + size.y) * 0.5f + margin) return false;
            foreach (var b in bushes)
                if (b != null && Flat(b.transform.position - world).magnitude < Mathf.Max(size.x, size.y) * 0.5f + 1.5f) return false;
            if (director == null) return true;
            // 바닥에 닿지 않는 작은 구로 소품 길이를 따라 (긴 성벽 조각은 양 끝까지) 벽 · 대나무와 겹치는지 봐요
            Vector3 along = size.x >= size.y ? Vector3.right : Vector3.forward;
            float half = Mathf.Max(0f, Mathf.Max(size.x, size.y) * 0.5f - 0.3f);
            for (int i = -1; i <= 1; i++)
                if (!director.IsFree(world + along * half * i, 0.55f)) return false;
            return true;
        }

        void MakeCrystal(RoomFeature f)
        {
            var breakable = f.Go.AddComponent<AThousandDawns.Combat.Breakable>(); // 평타 · 초식 · 비도로 깨져요
            breakable.Radius = Mathf.Max(f.Size.x, f.Size.y) * 0.5f;
            breakable.Broken += _ => BurstCrystal(f);
        }

        // 테스트용으로도 써요
        public void BurstCrystal(RoomFeature f)
        {
            if (f == null || !_features.Contains(f)) return;
            _features.Remove(f);
            if (f.Go != null)
            {
                var col = f.Go.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                Destroy(f.Go);
            }
            float radius = GameConfig.GetOptional("rift.crystal_radius", 2.5f);
            int damage = Mathf.RoundToInt(GameConfig.GetOptional("rift.crystal_damage", 40f) * (1f + _region * GameConfig.GetOptional("rift.crystal_region_scale", 0.6f)));
            if (director != null)
            {
                director.SpawnBurst(f.Center, radius, AThousandDawns.Combat.BurstKind.Rune);
                _crystalTargets.Clear();
                _crystalTargets.AddRange(director.Enemies); // 맞아 죽으면 목록에서 빠지니까 복사본으로
                foreach (var e in _crystalTargets)
                {
                    if (e == null || e.IsDead) continue;
                    if (Flat(e.transform.position - f.Center).magnitude <= radius + e.Damageable.BodyRadius) e.Damageable.ApplyHit(damage);
                }
                _crystalTargets.Clear();
            }
            CrystalBurst?.Invoke(this, f.Center, radius);
            LayoutChanged?.Invoke(this);
        }
    }
}
