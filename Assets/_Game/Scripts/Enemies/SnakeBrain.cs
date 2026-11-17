using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 청죽사: 숨지 않고 드러난 채 다가와서, 사거리(attack_range) 안이면 lunge만큼 뛰어들어 물고 둔화(이동 −25%, 2초).
    public sealed class SnakeBrain : LeapBrain
    {
        float _slowRatio, _slowDuration;

        public override EnemyKind Kind => EnemyKind.Snake;
        protected override string Key => "snake";

        protected override void ReadMoreTuning()
        {
            AttackRange = Tuning("attack_range", 3.9f); // 드러난 채 싸우니 들개보다 멀리서 뛰어들어요
            LeapDistance = Tuning("lunge", 3.9f);
            _slowRatio = Tuning("slow_ratio", 0.25f);
            _slowDuration = Tuning("slow_duration", 2f);
        }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange * 0.9f;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 toPlayer = ToPlayer();
            FaceToward(toPlayer);
            if (toPlayer.magnitude > AttackRange * 0.8f) Move(toPlayer.normalized * MoveSpeed, dt);
        }

        protected override void OnAttackStarted() => Sfx.Play(SfxId.SnakeStrike);

        protected override void OnBite() => Player.ApplySlow(_slowRatio, _slowDuration);
    }
}
