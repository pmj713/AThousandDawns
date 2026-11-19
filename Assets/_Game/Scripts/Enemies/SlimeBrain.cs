using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 슬라임 (차원 겹침, 판타지): 느리게 튀어 오다가 몸통으로 덮쳐요 (뛰어들기). 쓰러지면 작은 슬라임 둘로 갈라져요 (작은 것은 다시 갈라지지 않아요).
    //   덮친 자리에 점액 웅덩이가 잠시 남아요 (반경 slime.puddle_radius · slime.puddle_life초, 안에 있으면 이동 −slime.puddle_slow, 피해 없음. 플레이테스트 11차)
    public sealed class SlimeBrain : LeapBrain
    {
        float _puddleRadius, _puddleLife, _puddleSlow;

        public override EnemyKind Kind => EnemyKind.Slime;
        protected override string Key => "slime";
        public override string AttackName => "몸통 덮치기";

        // 점액 웅덩이가 생긴 순간 (아트: 끈적한 초록 웅덩이): 누가, 어디, 반경, 지속
        public static event System.Action<SlimeBrain, Vector3, float, float> PuddleMade;

        protected override void ReadMoreTuning()
        {
            _puddleRadius = Tuning("puddle_radius", 1f);
            _puddleLife = Tuning("puddle_life", 3f);
            _puddleSlow = Tuning("puddle_slow", 0.35f);
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d > AttackRange * 0.8f) Move(dir * MoveSpeed, dt);
        }

        protected override void OnLeapFinished()
        {
            Vector3 at = transform.position;
            SlimePuddle.Create(at, _puddleRadius, _puddleLife, _puddleSlow, Player, Body);
            PuddleMade?.Invoke(this, at, _puddleRadius, _puddleLife);
            Attack.EndActive();
        }

        protected override void OnDied()
        {
            if (IsSplitChild) return;
            Split(Mathf.RoundToInt(Tuning("split_count", 2f)), Tuning("split_health", 0.4f));
        }
    }
}
