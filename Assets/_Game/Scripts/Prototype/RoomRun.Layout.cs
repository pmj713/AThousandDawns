using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 방 구성 변형 (싸우는 방마다, 판 시드와 방 자리로 정해서 _rng · 저장을 건드리지 않아요).
    //   폐사찰: 빈 마당 · 기둥 회랑 (돌기둥 6) · 불상 (벽 앞 석불 둘) · 무너진 벽 (낮은 담 넷). 충돌체가 있어 사람·화살·돌진을 막아요.
    //   장강 수채: 마른 바닥 · 여울 (가운데 가로로 흐르는 얕은 물) · 웅덩이 (고인 물 둘) · 물길 (가운데 세로로 흐르는 물).
    //     물 안에서는 느려지고 (river.water_slow), 흐르는 물은 물살 쪽으로 밀어요 (river.current, 대시 중엔 안 밀려요). 수채 적은 물에 익숙해서 그대로예요.
    // 모두 문 사이 십자 길 · 방 가운데 · 고정 대나무 기둥 6개 · 청죽사 풀숲 자리를 비켜 놓아요.
    // 그림은 아트 쪽이 RoomFeatures를 읽어 바꿔 그려요 (기본판은 단색 상자).
    public sealed partial class RoomRun
    {
        public enum FeatureKind { Pillar, Statue, Wall, Water }

        public sealed class RoomFeature
        {
            public FeatureKind Kind;
            public Vector3 Center;  // 방 가운데 기준이 아니라 월드 자리
            public Vector2 Size;    // x · z 폭
            public Vector3 Current; // 물살 (칸/초, 고인 물은 0)
            public GameObject Go;   // 기본판 상자 (아트가 숨기고 바꿔 그려도 돼요)
        }

        public static event Action<RoomRun> LayoutChanged; // 방 구성이 바뀔 때 (방에 들어감 · 치움)

        public static readonly string[] TempleLayoutNames = { "빈 마당", "기둥 회랑", "불상", "무너진 벽" };
        public static readonly string[] RiverLayoutNames = { "마른 바닥", "여울", "웅덩이", "물길" };

        readonly List<RoomFeature> _features = new List<RoomFeature>();
        public IReadOnlyList<RoomFeature> RoomFeatures => _features;
        public int RoomLayout { get; private set; } = -1;
        public static int LayoutOverride = -1; // 테스트용

        public bool PlayerInWater => WaterAt(Player.transform.position) != null;

        // 방에 들어갈 때 (null이면 치움)
        void PlaceLayout(FloorRoom room)
        {
            foreach (var f in _features) if (f.Go != null) Destroy(f.Go);
            _features.Clear();
            RoomLayout = -1;
            if (room != null && room.HasFight && room.FixedWaves == null && (_region == 1 || _region == 2))
            {
                var rng = new System.Random(Seed ^ (room.Cell.x * 92837111) ^ (room.Cell.y * 689287499) ^ (_floor * 283923481) ^ _region);
                RoomLayout = LayoutOverride >= 0 ? LayoutOverride : rng.Next(4);
                if (_region == 1) BuildTempleLayout(RoomLayout);
                else BuildRiverLayout(RoomLayout);
                if (_region == 1 && RoomLayout > 0) Notify("temple_layout", "폐사찰: 방마다 돌기둥 · 석불 · 무너진 담이 달라요 · 화살과 돌진을 막는 엄폐물이에요");
                if (_region == 2 && RoomLayout > 0) Notify("river_water", "장강의 물: 들어가면 느려지고 흐르는 물은 물살 쪽으로 밀어요 · 대시 중엔 안 밀려요");
            }
            LayoutChanged?.Invoke(this);
        }

        // ── 폐사찰 (방 20×12, 가운데 기준 자리). 고정 대나무 기둥: (±5.5, ±3) · (±7, ±3.5) · (±3.5, ±3)
        void BuildTempleLayout(int layout)
        {
            switch (layout)
            {
                case 1: // 기둥 회랑
                    foreach (var p in new[] { new Vector2(-8.5f, -2.2f), new Vector2(8.5f, 2.2f), new Vector2(-5.8f, -2f), new Vector2(5.8f, 2f), new Vector2(-2.4f, -4.9f), new Vector2(2.4f, 4.9f) })
                        AddSolid(FeatureKind.Pillar, p, new Vector2(0.8f, 0.8f), 2.4f);
                    break;
                case 2: // 불상
                    AddSolid(FeatureKind.Statue, new Vector2(5.3f, 4.7f), new Vector2(1.6f, 1.4f), 2.6f); // 뒷벽 앞, 대나무 기둥 사이
                    AddSolid(FeatureKind.Statue, new Vector2(-5.3f, -4.7f), new Vector2(1.6f, 1.4f), 2.6f);
                    break;
                case 3: // 무너진 벽
                    AddSolid(FeatureKind.Wall, new Vector2(-6.5f, 1.8f), new Vector2(3f, 0.5f), 1.2f);
                    AddSolid(FeatureKind.Wall, new Vector2(6.5f, -1.8f), new Vector2(3f, 0.5f), 1.2f);
                    AddSolid(FeatureKind.Wall, new Vector2(-2.2f, 3.4f), new Vector2(0.5f, 2.2f), 1.2f);
                    AddSolid(FeatureKind.Wall, new Vector2(2.2f, -3.4f), new Vector2(0.5f, 2.2f), 1.2f);
                    break;
            }
        }

        void AddSolid(FeatureKind kind, Vector2 at, Vector2 size, float height)
        {
            var go = GameObject.CreatePrimitive(kind == FeatureKind.Pillar ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = kind == FeatureKind.Pillar ? "돌기둥" : kind == FeatureKind.Statue ? "석불" : "무너진 담";
            Vector3 center = combat.center + new Vector3(at.x, 0f, at.y);
            go.transform.position = center + Vector3.up * height * 0.5f;
            go.transform.localScale = kind == FeatureKind.Pillar ? new Vector3(size.x, height * 0.5f, size.y) : new Vector3(size.x, height, size.y);
            go.GetComponent<Renderer>().material.color = kind == FeatureKind.Statue ? new Color(0.42f, 0.38f, 0.33f) : new Color(0.5f, 0.48f, 0.45f);
            _features.Add(new RoomFeature { Kind = kind, Center = center, Size = size, Go = go });
        }

        // ── 장강 수채
        void BuildRiverLayout(int layout)
        {
            float flow = GameConfig.GetOptional("river.current", 1.2f);
            switch (layout)
            {
                case 1: // 여울: 가운데 가로 띠, 동쪽으로 흘러요
                    AddWater(new Vector2(0f, 0f), new Vector2(combat.size.x, 2.6f), Vector3.right * flow);
                    break;
                case 2: // 웅덩이: 고인 물 둘
                    AddWater(new Vector2(-6.5f, 3f), new Vector2(4f, 3f), Vector3.zero);
                    AddWater(new Vector2(6.5f, -3f), new Vector2(4f, 3f), Vector3.zero);
                    break;
                case 3: // 물길: 가운데 세로 띠, 남쪽으로 흘러요
                    AddWater(new Vector2(0f, 0f), new Vector2(2.6f, combat.size.y), Vector3.back * flow);
                    break;
            }
        }

        void AddWater(Vector2 at, Vector2 size, Vector3 current)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = current == Vector3.zero ? "고인 물" : "흐르는 물";
            var col = go.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            Vector3 center = combat.center + new Vector3(at.x, 0f, at.y);
            go.transform.position = center + Vector3.up * 0.02f;
            go.transform.localScale = new Vector3(size.x, 0.02f, size.y);
            var r = go.GetComponent<Renderer>();
            r.material.color = new Color(0.25f, 0.4f, 0.45f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _features.Add(new RoomFeature { Kind = FeatureKind.Water, Center = center, Size = size, Current = current, Go = go });
        }

        RoomFeature WaterAt(Vector3 p)
        {
            foreach (var f in _features)
            {
                if (f.Kind != FeatureKind.Water) continue;
                if (Mathf.Abs(p.x - f.Center.x) <= f.Size.x * 0.5f && Mathf.Abs(p.z - f.Center.z) <= f.Size.y * 0.5f) return f;
            }
            return null;
        }

        // 층 방에서 매 프레임: 물에 들어가면 느려지고 흐르는 물은 밀어요
        void TickWater(float dt)
        {
            if (_features.Count == 0 || Player == null || Player.IsDead) return;
            var w = WaterAt(Player.transform.position);
            if (w == null) return;
            Player.ApplySlow(GameConfig.GetOptional("river.water_slow", 0.3f), 0.2f);
            if (w.Current != Vector3.zero) Player.Drag(w.Current * dt);
        }

        // 항아리 자리가 기둥 · 석불 · 담 안이면 다시 골라요
        bool InSolid(Vector3 p, float margin)
        {
            foreach (var f in _features)
            {
                if (f.Kind == FeatureKind.Water) continue;
                if (Mathf.Abs(p.x - f.Center.x) <= f.Size.x * 0.5f + margin && Mathf.Abs(p.z - f.Center.z) <= f.Size.y * 0.5f + margin) return true;
            }
            return false;
        }
    }
}
