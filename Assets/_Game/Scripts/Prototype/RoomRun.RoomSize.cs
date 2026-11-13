using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 방 크기 세 가지: 작은 방 (20×12, 원래 방) · 중간 방 · 큰 방. 씬에 세 전투 장소를 따로 지어 두고, 방에 들어갈 때 그 방 크기의 장소를 써요.
    // 싸우는 방만 크기가 달라지고 (입문 죽림 · 이벤트 · 기연 방 · 시작 방은 작은 방), 판 시드와 방 자리로 정해서 다시 들어가도 같은 크기예요.
    // 큰 방일수록 적이 많아요: 방 예산 × room.size_budget.(m|l), 큰 방은 웨이브 +room.size_l.extra_waves.
    // 방 구성 (기둥 · 물 · 혈지)과 대나무 기둥 · 풀숲은 방 크기에 맞춰 늘어난 자리에 놓여요.
    public sealed partial class RoomRun
    {
        public enum RoomSizeKind { Small, Medium, Large }
        public static readonly string[] RoomSizeNames = { "작은 방", "중간 방", "큰 방" };

        [SerializeField] CombatSite combatMedium = new CombatSite();
        [SerializeField] CombatSite combatLarge = new CombatSite();

        CombatSite _site;
        CombatSite Combat => _site ?? combat; // 지금 방의 전투 장소

        public RoomSizeKind RoomSize { get; private set; }
        public Vector3 RoomCenter => Combat.center;
        public Vector2 RoomExtent => Combat.size;
        public static int RoomSizeOverride = -1; // 테스트용 (0 작은 방 · 1 중간 · 2 큰 방)

        static bool Built(CombatSite s) => s != null && s.locks != null && s.locks.Length == 4 && s.locks[0] != null; // 씬 빌더가 지어 둔 장소인지 (예전 씬이면 작은 방만)

        // 방에 들어갈 때 (MovePlayer 전에): 크기를 골라 그 장소로 바꿔요. 떠나는 장소의 결계 · 관문 표시는 꺼요.
        void PickSite(FloorRoom room)
        {
            var size = SizeFor(room);
            var next = size == RoomSizeKind.Medium && Built(combatMedium) ? combatMedium : size == RoomSizeKind.Large && Built(combatLarge) ? combatLarge : combat;
            if (next == combat) size = RoomSizeKind.Small;
            if (_site != null && _site != next)
            {
                foreach (var l in _site.locks) if (l != null) l.SetActive(false);
                if (_site.gateMarker != null) _site.gateMarker.SetActive(false);
            }
            _site = next;
            RoomSize = size;
        }

        RoomSizeKind SizeFor(FloorRoom room)
        {
            if (RoomSizeOverride >= 0) return (RoomSizeKind)Mathf.Clamp(RoomSizeOverride, 0, 2);
            if (room == null || !room.HasFight || room.FixedWaves != null) return RoomSizeKind.Small;
            var rng = new System.Random(Seed ^ (room.Cell.x * 49979687) ^ (room.Cell.y * 15485863) ^ (_floor * 32452843) ^ (_region * 86028121));
            float s = GameConfig.GetOptional("room.size_weight.s", 0.45f), m = GameConfig.GetOptional("room.size_weight.m", 0.35f), l = GameConfig.GetOptional("room.size_weight.l", 0.2f);
            double roll = rng.NextDouble() * (s + m + l);
            return roll < s ? RoomSizeKind.Small : roll < s + m ? RoomSizeKind.Medium : RoomSizeKind.Large;
        }

        // 방 크기만큼 적 예산 (작은 방 1배)
        float SizeBudget => RoomSize == RoomSizeKind.Large ? GameConfig.GetOptional("room.size_budget.l", 1.8f)
            : RoomSize == RoomSizeKind.Medium ? GameConfig.GetOptional("room.size_budget.m", 1.4f) : 1f;

        int SizeExtraWaves => RoomSize == RoomSizeKind.Large ? Mathf.RoundToInt(GameConfig.GetOptional("room.size_l.extra_waves", 1f)) : 0;

        // 작은 방(20×12) 기준 자리를 지금 방 크기로 늘려요 (방 구성 · 풀숲 등)
        Vector2 ScaleToRoom(Vector2 at) => new Vector2(at.x * Combat.size.x / 20f, at.y * Combat.size.y / 12f);
    }
}
