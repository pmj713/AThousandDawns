using System;

namespace AThousandDawns.Combat
{
    // 피해 계산기 시트와 같은 공식이에요. 시트의 예시 값(21, 46, 17)은 EditMode 테스트로 고정돼 있어요.
    public struct HitDamageInput
    {
        public float BaseDamage;          // ① 기본 피해
        public float AttackIncrease;      // ② 공격 증가 합 (0.6 = +60%)
        public float GongryeokMultiplier; // ③ 공력 배율. 초식·절초가 아니면 1
        public float SpecialMultiplier;   // ④ "×"로 적힌 효과만
        public float Vulnerability;       // ⑤ 취약 합
        public float Toughness;           // ⑥ 대상 강인함
        public float Defense;             // ⑦ 대상 방어력 (0.1 = 10%)
        public float Situational;         // ⑧ 방패 정면 0.2, 결계 0.7
        public bool IgnoreToughness;      // 절초

        public static HitDamageInput Of(float baseDamage) => new HitDamageInput
        {
            BaseDamage = baseDamage,
            GongryeokMultiplier = 1f,
            SpecialMultiplier = 1f,
            Situational = 1f,
        };
    }

    public struct PlayerHitInput
    {
        public float EnemyAttack;
        public float TakenIncrease;   // 받는 피해 증가 합
        public float DamageReduction; // 피해 감소 합
        public float Toughness;       // 플레이어 강인함
        public float Defense;         // 플레이어 방어력 (정수 스탯)
    }

    public readonly struct DamageRules
    {
        public readonly float ToughnessFloorRatio;   // 강인함을 빼도 이 비율 아래로는 내려가지 않음
        public readonly float GongryeokPerPoint;     // 공력 1당 초식·절초 피해 증가
        public readonly float PlayerMultiplierFloor; // 받는 피해 배율의 최저치
        public readonly float PlayerDefenseConstant; // 방어 감소율 = 방어력 / (방어력 + 이 값)

        public DamageRules(float toughnessFloorRatio, float gongryeokPerPoint, float playerMultiplierFloor, float playerDefenseConstant)
        {
            ToughnessFloorRatio = toughnessFloorRatio;
            GongryeokPerPoint = gongryeokPerPoint;
            PlayerMultiplierFloor = playerMultiplierFloor;
            PlayerDefenseConstant = playerDefenseConstant;
        }

        public static readonly DamageRules Default = new DamageRules(0.2f, 0.1f, 0.4f, 50f);
    }

    public static class DamageFormula
    {
        public static float GongryeokMultiplier(int gongryeok, in DamageRules rules) => 1f + gongryeok * rules.GongryeokPerPoint;

        // ①×(1+②)×③×④×(1+⑤). 파생 피해의 기준값이기도 해요.
        public static float BeforeToughness(in HitDamageInput i) =>
            i.BaseDamage * (1f + i.AttackIncrease) * i.GongryeokMultiplier * i.SpecialMultiplier * (1f + i.Vulnerability);

        public static int ToEnemy(in HitDamageInput i, in DamageRules rules)
        {
            float before = BeforeToughness(i);
            float afterToughness = i.IgnoreToughness ? before : Math.Max(before - i.Toughness, before * rules.ToughnessFloorRatio);
            return AtLeastOne(afterToughness * (1f - i.Defense) * i.Situational);
        }

        // 낙뢰, 화상, 연쇄 번개 같은 파생 피해: 강인함 무시, 방어력·상황 배율 적용.
        public static int Proc(float triggerBeforeToughness, float ratio, float defense, float situational = 1f) =>
            AtLeastOne(triggerBeforeToughness * ratio * (1f - defense) * situational);

        public static int ToPlayer(in PlayerHitInput p, in DamageRules rules)
        {
            float multiplier = Math.Max(rules.PlayerMultiplierFloor, 1f + p.TakenIncrease - p.DamageReduction);
            float afterToughness = p.EnemyAttack * multiplier - p.Toughness;
            float defenseRate = p.Defense / (p.Defense + rules.PlayerDefenseConstant);
            return AtLeastOne(afterToughness * (1f - defenseRate));
        }

        // 시트의 ROUND와 같게 0.5는 올려요. float 오차로 4.5가 4.4999…가 되는 경우를 위해 아주 작은 여유를 줘요.
        const float RoundingSlack = 1e-4f;

        static int AtLeastOne(float value) => Math.Max(1, (int)Math.Floor(Math.Max(0f, value) + 0.5f + RoundingSlack));
    }
}
