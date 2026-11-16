using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 슬라임 (차원 겹침, 판타지): 느리게 튀어 오다가 몸통으로 덮쳐요 (뛰어들기). 쓰러지면 작은 슬라임 둘로 갈라져요 (작은 것은 다시 갈라지지 않아요).
    public sealed class SlimeBrain : LeapBrain
    {
        public override EnemyKind Kind => EnemyKind.Slime;
        protected override string Key => "slime";
        public override string AttackName => "몸통 덮치기";

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d > AttackRange * 0.8f) Move(dir * MoveSpeed, dt);
        }

        protected override void OnDied()
        {
            if (IsSplitChild) return;
            Split(Mathf.RoundToInt(Tuning("split_count", 2f)), Tuning("split_health", 0.4f));
        }
    }
}
