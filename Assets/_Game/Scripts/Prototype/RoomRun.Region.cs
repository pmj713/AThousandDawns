using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 지역 (판구조 시트): 죽림 → 폐사찰 (→ 장강 수채 → 천마총은 아직). 지역마다 일반 층 3개 · 고수 · 객잔 · 정비실 · 보스.
    // 보스를 쓰러뜨리면 보상 기물을 고른 뒤 다음 지역 1층 갈림길로 이어져요. 마지막 지역 보스면 판 끝 (승리).
    // 지역 배율 (몬스터 시트 15~17행): 적 체력 · 공격 × 배율 (일반 적 · 살수 · 고수 · 보스 모두).
    // 폐사찰 일반 적은 RoomBudget.Temple (망령·석상 나한·목탁승·파계승·등불 귀신). 고수·보스는 생기기 전까지 죽림 것을 폐사찰 배율로 써요.
    public sealed partial class RoomRun
    {
        public static readonly string[] RegionNames = { "죽림", "폐사찰" };

        int _region;
        bool _regionAdvancePending; // 보스 보상을 고른 뒤 다음 지역으로

        public int Region => _region;
        public string RegionName => RegionNames[Mathf.Clamp(_region, 0, RegionNames.Length - 1)];
        public bool LastRegion => _region >= RegionNames.Length - 1;

        static float RegionKey(string key, int region, float fallback) =>
            region == 0 ? fallback : GameConfig.GetOptional($"region.{region}.{key}", fallback);

        float RegionHealth => RegionKey("health", _region, _region == 0 ? 1f : 1.8f);
        float RegionAttack => RegionKey("attack", _region, _region == 0 ? 1f : 1.3f);
        int RoomSilver => Mathf.RoundToInt(_region == 0 ? GameConfig.Get("silver.room", 18f) : RegionKey("silver_room", _region, 22f));

        // 그 지역 등급표 (등급확률 시트 11~13행). 짙은 길은 다음 지역 표
        float[] RegionGradeWeights() => GradeRow(_region);
        float[] DenseGradeWeights() => GradeRow(_region + 1);

        static float[] GradeRow(int region)
        {
            if (region <= 0) return new[]
            {
                GameConfig.Get("item_grade.common", 0.58f), GameConfig.Get("item_grade.uncommon", 0.3f),
                GameConfig.Get("item_grade.rare", 0.1f), GameConfig.Get("item_grade.legendary", 0.02f),
            };
            if (region == 1) return new[] // 폐사찰 (죽림 짙은 길도 이 표)
            {
                GameConfig.Get("item_grade_dense.common", 0.52f), GameConfig.Get("item_grade_dense.uncommon", 0.3f),
                GameConfig.Get("item_grade_dense.rare", 0.14f), GameConfig.Get("item_grade_dense.legendary", 0.04f),
            };
            return new[] // 장강 수채 (폐사찰 짙은 길)
            {
                GameConfig.GetOptional("item_grade.r2.common", 0.46f), GameConfig.GetOptional("item_grade.r2.uncommon", 0.31f),
                GameConfig.GetOptional("item_grade.r2.rare", 0.17f), GameConfig.GetOptional("item_grade.r2.legendary", 0.06f),
            };
        }

        // 나온 적에 지역 배율 (체력은 지금 체력 비율 그대로 늘어요)
        void ScaleForRegion(EnemyBrain enemy)
        {
            if (enemy == null || _region == 0) return;
            enemy.Damageable.ScaleMaxHealth(RegionHealth);
            enemy.ScaleAttack(RegionAttack);
        }

        // 보스 보상을 고른 뒤 (ClearRoom 대신 Tick에서): 다음 지역 1층 갈림길
        void TickRegionAdvance()
        {
            if (!_regionAdvancePending || OffersOpen || (Player.Items != null && Player.Items.Pending != null)) return;
            _regionAdvancePending = false;
            _region++;
            Player.UltimateStage = Mathf.Max(0, _region - 1); // 절초 경지: 폐사찰 보스를 넘으면 중단전 (무기 시트 52행)
            _masterDone = false;
            _masterChoiceShown = false;
            BeginFork(1);
            ShowBanner($"{RegionName} · 1층 갈림길", 4f);
        }

        // 테스트용: 지역을 바로 정해요 (다음 갈림길부터)
        public void SetRegionForTest(int region) => _region = Mathf.Clamp(region, 0, RegionNames.Length - 1);
    }
}
