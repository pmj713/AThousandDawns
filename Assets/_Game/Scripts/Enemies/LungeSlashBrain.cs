using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 전진 베기 계열 (졸개, 방패 산적): 다가와서 부채꼴(반경 attack_range, 각도 attack_angle)을 앞으로 lunge만큼 밀며 베요.
    // 실제 사거리 = attack_range + lunge. 공격 토큰이 없거나 공격 간격이 남았으면 사거리 바깥 고리를 옆으로 돌아요.
    public abstract class LungeSlashBrain : EnemyBrain
    {
        protected float SlashAngle, Lunge, LungeTime;
        float _engage, _lungeRemaining;
        bool _hitPlayer;

        protected override float ActiveTime => LungeTime;

        protected override void ReadTuning()
        {
            SlashAngle = Tuning("attack_angle", 60f);
            Lunge = Tuning("lunge", 1.5f);
            LungeTime = Mathf.Max(0.01f, Tuning("lunge_time", 0.15f));
            // 예고 앞 절반에 조금 더 다가오니까, 실제 사거리보다 약간 안쪽에서 예고를 시작해요.
            _engage = Lunge + AttackRange * 0.8f;
            ReadMoreTuning();
        }

        protected virtual void ReadMoreTuning() { }

        // 다가오는 속도 (방패 산적은 방패를 든 동안 느려요)
        protected virtual float ApproachSpeed => MoveSpeed;

        protected override bool WantsToAttack(float distance) => distance <= _engage;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            TurnToward(dir, dt);

            if (canAttack)
            {
                Move(dir * ApproachSpeed, dt);
                return;
            }

            float ring = _engage + 1f;
            Vector3 velocity = Strafe(dir) * (ApproachSpeed * 0.5f);
            if (d > ring + 0.5f) velocity = dir * ApproachSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (ApproachSpeed * 0.6f) + velocity * 0.5f;
            Move(velocity, dt);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Lunge(AttackRange, SlashAngle, Lunge);

        protected override void OnActiveStarted()
        {
            _lungeRemaining = Lunge;
            _hitPlayer = false;
            CheckHit();
        }

        protected override void TickActive(float dt)
        {
            float step = Mathf.Min(_lungeRemaining, Lunge / LungeTime * dt);
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
            if (!HitShapes.InSector(transform.position, AimDirection, AttackRange, SlashAngle, Player.transform.position, PlayerRadius)) return;
            _hitPlayer = TryHitPlayer();
        }
    }
}
