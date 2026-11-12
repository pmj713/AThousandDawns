using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 지역 (판구조 시트): 죽림 → 폐사찰 → 장강 수채 → 천마총 (마지막). 지역마다 일반 층 3개 · 고수 · 객잔 · 정비실 · 보스.
    // 보스를 쓰러뜨리면 보상 기물을 고른 뒤 다음 지역 1층 갈림길로 이어져요. 마지막 지역 보스면 판 끝 (승리).
    // 지역 배율 (몬스터 시트 15~17행): 적 체력 · 공격 × 배율 (일반 적 · 살수 · 고수 · 보스 모두).
    // 폐사찰 일반 적은 RoomBudget.Temple (망령·석상 나한·목탁승·파계승·등불 귀신).
    // 장강 수채 (몬스터 시트 17행: 체력 ×3.0 · 공격 ×1.6, 은자 탭 6행: 방당 26)의 일반 적은 RoomBudget.River (수적 졸개·작살수·쇠닻 거한·등패수·수귀·깃발수). 고수는 탁랑객, 보스는 수채주 철룡이에요.
    // 천마총 (몬스터 시트 18행: 체력 ×4.5 · 공격 ×2.0, 은자 탭 7행: 방당 30): 일반 적은 RoomBudget.Tomb (마교도·혈궁수·혈인·천마 근위·혈폭자). 고수는 혈영검마, 보스는 천마가 생기기 전까지 철룡을 천마총 배율로 써요.
    // 동시 근접 공격 (몬스터 B85): 2명에 지역마다 +1, 최대 4명.
    public sealed partial class RoomRun
    {
        public static readonly string[] RegionNames = { "죽림", "폐사찰", "장강 수채", "천마총" };

        int _region;
        bool _regionAdvancePending; // 보스 보상을 고른 뒤 다음 지역으로

        public int Region => _region;
        public string RegionName => RegionNames[Mathf.Clamp(_region, 0, RegionNames.Length - 1)];
        public bool LastRegion => _region >= RegionNames.Length - 1;

        static float RegionKey(string key, int region, float fallback) =>
            region == 0 ? fallback : GameConfig.GetOptional($"region.{region}.{key}", fallback);

        static readonly float[] HealthByRegion = { 1f, 1.8f, 3f, 4.5f }, AttackByRegion = { 1f, 1.3f, 1.6f, 2f }, SilverByRegion = { 18f, 22f, 26f, 30f };
        static float ByRegion(float[] table, int region) => table[Mathf.Clamp(region, 0, table.Length - 1)];

        float RegionHealth => RegionKey("health", _region, ByRegion(HealthByRegion, _region));
        float RegionAttack => RegionKey("attack", _region, ByRegion(AttackByRegion, _region));
        int RoomSilver => Mathf.RoundToInt(_region == 0 ? GameConfig.Get("silver.room", 18f) : RegionKey("silver_room", _region, ByRegion(SilverByRegion, _region)));

        // 동시 근접 공격: 2명 + 지역마다 1명 (최대 4명). 층에 들어설 때마다 맞춰요 (이어하기 포함)
        public int MeleeTokensForRegion => Mathf.Min(
            Mathf.RoundToInt(GameConfig.GetOptional("enemy.melee_tokens_max", 4f)),
            Mathf.RoundToInt(GameConfig.Get("enemy.melee_tokens", 2f) + _region * GameConfig.GetOptional("enemy.melee_tokens_per_region", 1f)));

        // 정예 특성 수 (몬스터 B142): 죽림·폐사찰 하나, 장강 수채 1~2개 (반반), 천마총 둘
        static readonly float[] SecondTraitByRegion = { 0f, 0f, 0.5f, 1f };
        public float SecondTraitChance => GameConfig.GetOptional($"elite.second_trait.r{_region}", ByRegion(SecondTraitByRegion, _region));

        void RollSecondTrait(EnemyBrain elite)
        {
            if (elite == null || !elite.IsElite || _rng.NextDouble() >= SecondTraitChance) return;
            var traits = new System.Collections.Generic.List<EliteTrait>();
            foreach (EliteTrait t in System.Enum.GetValues(typeof(EliteTrait)))
                if (t != elite.Trait && EnemyBrain.Allows(elite.Kind, t)) traits.Add(t);
            if (traits.Count > 0) elite.AddEliteTrait(traits[_rng.Next(traits.Count)]);
        }

        void ApplyRegionRules()
        {
            if (director != null && director.MeleeTokens != null) director.MeleeTokens.Capacity = MeleeTokensForRegion;
            if (director != null) director.SummonScale = ScaleForRegion; // 적이 부른 부하도 지역 배율
        }

        // 그 지역 등급표 (등급확률 시트 11~15행). 짙은 길은 다음 지역 표 (천마총 짙은 길은 전용 표)
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
            if (region == 3) return new[] // 천마총 (장강 짙은 길)
            {
                GameConfig.GetOptional("item_grade.r3.common", 0.4f), GameConfig.GetOptional("item_grade.r3.uncommon", 0.31f),
                GameConfig.GetOptional("item_grade.r3.rare", 0.21f), GameConfig.GetOptional("item_grade.r3.legendary", 0.08f),
            };
            if (region >= 4) return new[] // 천마총 짙은 길 전용
            {
                GameConfig.GetOptional("item_grade.r3_dense.common", 0.34f), GameConfig.GetOptional("item_grade.r3_dense.uncommon", 0.32f),
                GameConfig.GetOptional("item_grade.r3_dense.rare", 0.24f), GameConfig.GetOptional("item_grade.r3_dense.legendary", 0.1f),
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
            Player.UltimateStage = Mathf.Max(0, _region - 1); // 절초 경지: 폐사찰 보스를 넘으면 중단전, 장강 보스를 넘으면 상단전 (무기 시트 52행)
            _masterDone = false;
            _masterChoiceShown = false;
            BeginFork(1);
            ShowBanner($"{RegionName} · 1층 갈림길", 4f);
        }

        // 테스트용: 지역을 바로 정해요 (다음 갈림길부터)
        public void SetRegionForTest(int region) => _region = Mathf.Clamp(region, 0, RegionNames.Length - 1);
    }
}
