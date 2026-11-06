namespace AThousandDawns.Combat
{
    // 기물이 화상·둔화·빙결·감전·번개에 더하는 값 (기물 탭 #열양·#빙백·#뇌정).
    // 플레이어가 기물이나 기가 바뀔 때마다 새로 채워 Current에 넣고, 적의 StatusEffects와 Lightning이 읽어요.
    // 플레이어가 없으면 기본값 (더하는 것 없음).
    public sealed class ElementalMods
    {
        public static ElementalMods Current { get; set; } = new ElementalMods();

        // 화상
        public float BurnDurationBonus;          // 향로: 지속 +초
        public float BurnDamageBonus;            // 주작 깃털: 화상 피해 +%
        public int BurnMaxBonus;                 // 주작 깃털: 최대 중첩 +
        public float BurnRampPer, BurnRampMax;   // 등잔: 화상이 이어지면 간격마다 그 적의 화상 피해 +% (최대)
        public float BurnRampInterval = 2f;
        public int BurnOvercap;                  // 화룡 비늘: 최대 중첩인 적에게 또 걸면 그 적만 최대 중첩 + (화상이 꺼지면 처음으로)

        // 둔화·빙결
        public float SlowDurationBonus;          // 빙설 두건: 둔화 지속 +초
        public float BossSlowBonus;              // 빙백 염주: 고수·보스에게 들어가는 둔화 효과 +%p (절반 규칙을 메움)
        public float SlowWindupExtra;            // 눈송이 수정: 둔화 3중첩인 적의 공격 예고 +초
        public float FreezeDurationBonus;        // 한빙 거울: 빙결 시간 +초
        public float FreezeImmunityMultiplier = 1f;  // 한빙 거울 주화입마: 빙결 면역 시간 절반
        public float BossFreezeImmunityReduction;    // 빙백 염주 (0개면): 보스 빙결 면역 −초
        public float PatternBan;                 // 설련 향낭: 빙결로 취소된 보스 패턴이 다시 나오지 않는 시간

        // 감전·번개
        public float ShockDurationBonus;         // 뇌석: 감전 지속 +초
        public int ChainTargetsBonus;            // 천둥 북: 연쇄 수 +
        public float ChainDamageBonus;           // 천둥 북 (0개면): 연쇄 피해 +%
        public float DischargeBonus;             // 뇌정 옥새: 방전 피해 +%
        public float DischargeMaxBonus;          // 뇌정 붓: 최대 중첩 방전 피해 +%
        public float CohesionShockMultiplier = 1f;   // 뇌정 옥새: 응집된 번개로 방전하면 감전 중첩을 이만큼 곱해 셈
        public float SplashRatio, SplashRadius;  // 운뢰 피리: 방전 피해의 일부가 주변 적에게 퍼짐
        public int Reshock;                      // 매 발톱 주화입마: 연쇄 번개에 맞은 적에게 방전 뒤 감전을 남김
    }
}
