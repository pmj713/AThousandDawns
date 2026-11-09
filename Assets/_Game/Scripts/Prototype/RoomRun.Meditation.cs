using AThousandDawns.Core;
using UnityEngine;
using MArt = AThousandDawns.Core.Meditation.Art;

namespace AThousandDawns.Prototype
{
    // 면벽수련을 판에 적용 (판을 시작할 때). 플레이어 몸에 붙는 것은 PlayerController.ApplyMeditation, 판 진행에 붙는 것은 여기.
    // 쾌: 공격 속도 · 1·2타 빠르게 · 3타 뒤 틈 −50% · 3타 잔상    강: 최대 체력 · 강인함 +3 · 방을 비우면 체력 4% · 기사회생 40%
    // 맥: 수련치 · 시작 혈자리 · 시작 부품 고르기 · 단전 2개 (공력 −1)  오: 행운 · 기연 감지 −1 · 갈림길 +1 · 고수 보상 2가지
    // 은: 은자 · 시작 은자 +50 · 객잔 −15% · 객잔 이자 20%          기: 기세 충전 · 초식 쿨타임 −10% · 전투 시작 기세 30% · 기세 200%
    public sealed partial class RoomRun
    {
        int _luck, _forkBonus, _masterPicks = 1;
        float _silverBonus, _innPriceScale = 1f, _fightGauge, _roomHeal, _innInterest;
        bool _startPartPick;

        public int Luck => _luck;

        void ApplyMeditation(MetaSave meta)
        {
            Meditation.Clamp(meta);
            int P(MArt a) => Meditation.Points(meta, a);
            bool On(MArt a, int step) => Meditation.Active(meta, a, step);
            float G(string key, float fallback) => GameConfig.GetOptional(key, fallback);

            Player.ApplyMeditation(
                attackSpeed: P(MArt.Swift) * G("meditation.swift_per_point", 0.01f),
                openerSpeed: On(MArt.Swift, 0) ? G("meditation.swift_opener_speed", 0.15f) : 0f,
                finisherRecoveryCut: On(MArt.Swift, 1) ? G("meditation.swift_finisher_recovery_cut", 0.5f) : 0f,
                echoRatio: On(MArt.Swift, 2) ? G("meditation.swift_echo_ratio", 0.5f) : 0f,
                maxHealth: P(MArt.Iron) * G("meditation.iron_per_point", 2f),
                toughness: On(MArt.Iron, 0) ? G("meditation.iron_toughness", 3f) : 0f,
                xp: P(MArt.Meridian) * G("meditation.meridian_per_point", 0.02f),
                gauge: P(MArt.Qi) * G("meditation.qi_per_point", 0.01f),
                skillCooldownCut: On(MArt.Qi, 0) ? G("meditation.qi_skill_cooldown", 0.1f) : 0f,
                reviveRatio: On(MArt.Iron, 2) ? G("meditation.iron_revive", 0.4f) : 0f,
                doubleGauge: On(MArt.Qi, 2));

            _roomHeal = On(MArt.Iron, 1) ? G("meditation.iron_room_heal", 0.04f) : 0f;
            if (On(MArt.Meridian, 0)) Player.Level.GrantPending();       // 시작 혈자리
            _startPartPick = On(MArt.Meridian, 1) && _unlockedParts.Count > 0; // 1층에서 부품 고르기
            if (On(MArt.Meridian, 2) && Player.Items != null)             // 단전 2개로 시작, 대신 공력 −1
            {
                Player.Items.StartDanjeons = 2;
                Player.Gongryeok -= Mathf.RoundToInt(G("meditation.meridian_power_cut", 1f));
            }
            _luck = P(MArt.Insight);
            arena.Luck = _luck;
            _hiddenSenseCut = On(MArt.Insight, 0) ? 1 : 0;      // 오 5: 기연 감지 공력 4 → 3
            _forkBonus = On(MArt.Insight, 1) ? 1 : 0;
            _masterPicks = On(MArt.Insight, 2) ? 2 : 1;
            _silverBonus = P(MArt.Shadow) * G("meditation.shadow_per_point", 0.02f);
            if (On(MArt.Shadow, 0)) Silver += Mathf.RoundToInt(G("meditation.shadow_start_silver", 50f));
            _innPriceScale = On(MArt.Shadow, 1) ? 1f - G("meditation.shadow_inn_discount", 0.15f) : 1f;
            _innInterest = On(MArt.Shadow, 2) ? G("meditation.shadow_inn_interest", 0.2f) : 0f;
            _fightGauge = On(MArt.Qi, 1) ? G("meditation.qi_fight_gauge", 0.3f) : 0f;
        }

        // 은: 은자 획득 +%
        int BoostSilver(int amount) => Mathf.RoundToInt(amount * (1f + _silverBonus));

        // 은 20: 객잔에 들를 때 가진 은자의 20% (최대 100)
        int InnInterest() => _innInterest <= 0f ? 0
            : Mathf.Min(Mathf.RoundToInt(GameConfig.GetOptional("meditation.shadow_inn_interest_max", 100f)), Mathf.FloorToInt(Silver * _innInterest));

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
