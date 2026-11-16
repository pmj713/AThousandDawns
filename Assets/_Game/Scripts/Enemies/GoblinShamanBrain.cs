using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 주술사: 거리를 두고 주문 (예고 중 맞으면 끊김).
    //   광폭화: 둘레 다른 적이 잠시 빨라져요 (깃발수 지휘와 같은 규칙) · 저주 장판: 플레이어 발밑에 저주 (안에 있으면 맞고 느려져요)
    public sealed class GoblinShamanBrain : FlagBearerBrain
    {
        float _curseRadius, _curseLife, _curseRatio, _curseSlow, _curseSlowTime;

        public override EnemyKind Kind => EnemyKind.GoblinShaman;
        protected override string Key => "goblinshaman";
        public override string AttackName => "주술";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _curseRadius = Tuning("curse_radius", 1.5f);
            _curseLife = Tuning("curse_life", 4f);
            _curseRatio = Tuning("curse_ratio", 0.5f);
            _curseSlow = Tuning("curse_slow", 0.3f);
            _curseSlowTime = Tuning("curse_slow_time", 1.5f);
        }

        protected override bool WantsToAttack(float distance) => true; // 공격 간격마다 늘 주문 (도울 적이 없어도 저주는 깔아요)

        protected override void OnActiveStarted()
        {
            base.OnActiveStarted(); // 광폭화
            GhostFire.Create(Player.transform.position, _curseRadius, _curseLife, AttackPower * _curseRatio, _curseSlow, _curseSlowTime, Player, Body);
        }
    }
}
