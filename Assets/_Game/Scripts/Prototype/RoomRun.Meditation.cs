using AThousandDawns.Core;
using UnityEngine;
using MArt = AThousandDawns.Core.Meditation.Art;

namespace AThousandDawns.Prototype
{
    // 면벽수련을 판에 적용 (판을 시작할 때). 플레이어 몸에 붙는 것은 PlayerController.ApplyMeditation, 판 진행에 붙는 것은 여기.
    // 쾌: 공격 속도 · 대시 +1 · 대시 공격 +15%     강: 최대 체력 · 강인함 +3 · 층마다 첫 피격 무효 · 기사회생 40%
    // 맥: 수련치 · 시작 혈자리 · 시작 부품 고르기     오: 행운 · 갈림길 +1 · 고수 보상 2가지
    // 은: 은자 · 시작 은자 +50 · 객잔 −15% · 추적도 절반  기: 기세 충전 · 초식 쿨타임 −10% · 전투 시작 기세 30%
    public sealed partial class RoomRun
    {
        int _luck, _forkBonus, _masterPicks = 1;
        float _silverBonus, _innPriceScale = 1f, _fightGauge, _trackingCarry;
        bool _trackingHalf, _floorGuard, _startPartPick;

        public int Luck => _luck;

        void ApplyMeditation(MetaSave meta)
        {
            Meditation.Clamp(meta);
            int P(MArt a) => Meditation.Points(meta, a);
            bool On(MArt a, int step) => Meditation.Active(meta, a, step);
            float G(string key, float fallback) => GameConfig.GetOptional(key, fallback);

            Player.ApplyMeditation(
                attackSpeed: P(MArt.Swift) * G("meditation.swift_per_point", 0.01f),
                dash: On(MArt.Swift, 0) ? 1 : 0,
                dashAttack: On(MArt.Swift, 1) ? G("meditation.swift_dash_attack", 0.15f) : 0f,
                maxHealth: P(MArt.Iron) * G("meditation.iron_per_point", 2f),
                toughness: On(MArt.Iron, 0) ? G("meditation.iron_toughness", 3f) : 0f,
                xp: P(MArt.Meridian) * G("meditation.meridian_per_point", 0.02f),
                gauge: P(MArt.Qi) * G("meditation.qi_per_point", 0.01f),
                skillCooldownCut: On(MArt.Qi, 0) ? G("meditation.qi_skill_cooldown", 0.1f) : 0f,
                reviveRatio: On(MArt.Iron, 2) ? G("meditation.iron_revive", 0.4f) : 0f);

            _floorGuard = On(MArt.Iron, 1);
            if (On(MArt.Meridian, 0)) Player.Level.GrantPending();       // 시작 혈자리
            _startPartPick = On(MArt.Meridian, 1) && _unlockedParts.Count > 0; // 1층에서 부품 고르기
            _luck = P(MArt.Insight);
            arena.Luck = _luck;
            _forkBonus = On(MArt.Insight, 1) ? 1 : 0;
            _masterPicks = On(MArt.Insight, 2) ? 2 : 1;
            _silverBonus = P(MArt.Shadow) * G("meditation.shadow_per_point", 0.02f);
            if (On(MArt.Shadow, 0)) Silver += Mathf.RoundToInt(G("meditation.shadow_start_silver", 50f));
            _innPriceScale = On(MArt.Shadow, 1) ? 1f - G("meditation.shadow_inn_discount", 0.15f) : 1f;
            _trackingHalf = On(MArt.Shadow, 2);
            _fightGauge = On(MArt.Qi, 1) ? G("meditation.qi_fight_gauge", 0.3f) : 0f;
        }

        // 은: 은자 획득 +%
        int BoostSilver(int amount) => Mathf.RoundToInt(amount * (1f + _silverBonus));

        // 기 10: 싸움이 시작될 때 기세
        void OnFightStart()
        {
            if (_fightGauge > 0f) Player.RaiseGaugeTo(_fightGauge);
        }

        // 오: 행운 1당 일반 확률 −1%p를 고급 0.6 · 희귀 0.3 · 전설 0.1로 나눠요 (등급확률 시트)
        public static float[] ApplyLuck(float[] weights, int luck)
        {
            if (weights == null || luck <= 0) return weights;
            var w = (float[])weights.Clone();
            float per = GameConfig.GetOptional("luck.common_cut", 0.01f);
            float cut = Mathf.Min(w[0], luck * per);
            w[0] -= cut;
            w[1] += cut * GameConfig.GetOptional("luck.uncommon_share", 0.6f);
            w[2] += cut * GameConfig.GetOptional("luck.rare_share", 0.3f);
            w[3] += cut * GameConfig.GetOptional("luck.legendary_share", 0.1f);
            return w;
        }
    }
}
