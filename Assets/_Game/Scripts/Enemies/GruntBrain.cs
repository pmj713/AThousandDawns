using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 졸개: 다가와서 전진 베기 (부채꼴 60°, 반경 1.2 + 전진 1.5 = 실제 사거리 약 2.7칸).
    // 공격 토큰이 없거나 공격 간격이 남았으면 사거리 바깥 고리를 옆으로 돌아요.
    public sealed class GruntBrain : EnemyBrain
    {
        float _angle, _lunge, _lungeTime, _engage, _lungeRemaining;
        bool _hitPlayer;

        public override EnemyKind Kind => EnemyKind.Grunt;
        protected override string Key => "grunt";
        protected override float ActiveTime => _lungeTime;

        protected override void ReadTuning()
        {
            _angle = Tuning("attack_angle", 60f);
            _lunge = Tuning("lunge", 1.5f);
            _lungeTime = Mathf.Max(0.01f, Tuning("lunge_time", 0.15f));
            // 예고 앞 절반에 조금 더 다가오니까, 실제 사거리보다 약간 안쪽에서 예고를 시작해요.
            _engage = _lunge + AttackRange * 0.8f;
        }

        protected override bool WantsToAttack(float distance) => distance <= _engage;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (canAttack)
            {
                Move(dir * MoveSpeed, dt);
                return;
            }

            float ring = _engage + 1f;
            Vector3 velocity = Strafe(dir) * (MoveSpeed * 0.5f);
            if (d > ring + 0.5f) velocity = dir * MoveSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (MoveSpeed * 0.6f) + velocity * 0.5f;
            Move(velocity, dt);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Lunge(AttackRange, _angle, _lunge);

        protected override void OnActiveStarted()
        {
            _lungeRemaining = _lunge;
            _hitPlayer = false;
            CheckHit();
        }

        protected override void TickActive(float dt)
        {
            float step = Mathf.Min(_lungeRemaining, _lunge / _lungeTime * dt);
            // 플레이어 몸에 닿으면 더 밀고 들어가지 않아요
            Vector3 to = ToPlayer();
            if (Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f) step = 0f;
            if (step > 0f)
            {
                MoveRaw(AimDirection * step);
                _lungeRemaining -= step;
            }
            CheckHit();
        }

        void CheckHit()
        {
            if (_hitPlayer) return;
            if (!HitShapes.InSector(transform.position, AimDirection, AttackRange, _angle, Player.transform.position, PlayerRadius)) return;
            _hitPlayer = TryHitPlayer();
        }
    }
}
