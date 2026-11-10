using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 들개: 빠르게 플레이어 옆·뒤로 돌아 들어와 2칸 뛰어들어 물고, 옆으로 1.5칸 빠져요. 착지 순간 0.5초가 빈틈.
    // 2~3마리가 같이 나와요.
    public sealed class HoundBrain : LeapBrain
    {
        float _hopDistance, _hopTime, _flankAngle, _hopRemaining, _flankSign = 1f;
        Vector3 _hopDirection;

        public override EnemyKind Kind => EnemyKind.Hound;
        protected override string Key => "hound";

        protected override void ReadMoreTuning()
        {
            _hopDistance = Tuning("hop_distance", 1.5f);
            _hopTime = Mathf.Max(0.05f, Tuning("hop_time", 0.2f));
            _flankAngle = Tuning("flank_angle", 100f);
        }

        protected override void OnInitialized() => _flankSign = Random.value < 0.5f ? -1f : 1f;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (canAttack)
            {
                // 플레이어가 보는 방향에서 옆·뒤쪽 지점으로 돌아 들어가요
                Vector3 side = Quaternion.Euler(0f, _flankSign * _flankAngle, 0f) * Player.Facing;
                Vector3 goal = Player.transform.position + side * (AttackRange * 0.8f) - transform.position;
                goal.y = 0f;
                if (goal.magnitude > 0.2f) Move(goal.normalized * MoveSpeed, dt);
                return;
            }

            float ring = AttackRange + 2f;
            Vector3 velocity = Strafe(dir) * (MoveSpeed * 0.7f);
            if (d > ring + 0.5f) velocity = dir * MoveSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (MoveSpeed * 0.6f) + velocity * 0.5f;
            Move(velocity, dt);
        }

        protected override void OnLeapFinished()
        {
            _hopDirection = Vector3.Cross(Vector3.up, AimDirection) * (Random.value < 0.5f ? -1f : 1f);
            _hopRemaining = _hopDistance;
        }

        protected override void TickAfterLeap(float dt)
        {
            float step = Mathf.Min(_hopRemaining, _hopDistance / _hopTime * dt);
            var flags = MoveRaw(_hopDirection * step);
            _hopRemaining -= step;
            if (_hopRemaining <= 1e-4f || (flags & CollisionFlags.Sides) != 0) Attack.EndActive(); // 착지 → 빈틈
        }

        protected override void OnAttackFinished() => _flankSign = -_flankSign; // 다음엔 반대쪽으로

        protected override void OnBite() => Sfx.Play(SfxId.HoundBite); // 2~3마리가 같이 나와서 예고 으르렁은 빼고 물기만
    }
}
