using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 멧돼지: 8칸 안이면 예고 뒤 초당 12칸으로 직선 돌진. 돌진 중엔 경직이 없고, 벽·대나무에 부딪히면 2초 기절.
    // 플레이어는 뚫고 지나가요 (한 번만 맞음).
    public sealed class BoarBrain : EnemyBrain
    {
        const float ProbeHeight = 0.6f;
        const float MaxStep = 0.25f;

        float _chargeSpeed, _chargeDistance, _wallStun, _width;
        float _traveled, _plannedLength;
        bool _hitPlayer;

        public override EnemyKind Kind => EnemyKind.Boar;
        protected override string Key => "boar";
        protected override float ActiveTime => float.PositiveInfinity; // 돌진이 끝날 때 EndActive
        protected override bool SuperArmor => Attack.Current == EnemyAttackTimeline.Phase.Active;
        protected override float TrackStopDistance => 3f;

        public bool IsCharging => Attack.Current == EnemyAttackTimeline.Phase.Active;
        public bool IsWallStunned { get; private set; }

        protected override void ReadTuning()
        {
            _chargeSpeed = Tuning("charge_speed", 12f);
            _chargeDistance = Tuning("charge_distance", 8f);
            _wallStun = Tuning("wall_stun", 2f);
            _width = Tuning("charge_width", 1.2f);
        }

        protected override bool WantsToAttack(float distance) =>
            distance <= AttackRange * 0.9f && HasClearLine(distance, BodyRadius * 0.8f, ProbeHeight);

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            // 준비됐으면 돌진 거리 안쪽까지, 아니면 그 언저리에서 어슬렁거려요.
            float keep = canAttack ? AttackRange * 0.8f : AttackRange;
            if (d > keep) Move(dir * MoveSpeed, dt);
            else if (!canAttack) Move(Strafe(dir) * (MoveSpeed * 0.3f), dt);
        }

        protected override List<Vector2> TelegraphShape()
        {
            _plannedLength = PlannedChargeLength();
            return TelegraphShapes.Rect(_width, _plannedLength + _width * 0.5f);
        }

        // 따라오는 동안엔 벽까지 거리에 맞춰 예고 길이를 바꿔요.
        protected override void UpdateTelegraph()
        {
            if (Attack.IsTracking)
            {
                float length = PlannedChargeLength();
                if (Mathf.Abs(length - _plannedLength) > 0.05f)
                {
                    _plannedLength = length;
                    Telegraph.SetShape(TelegraphShapes.Rect(_width, _plannedLength + _width * 0.5f));
                }
            }
            base.UpdateTelegraph();
        }

        float PlannedChargeLength() =>
            ObstacleProbe.Distance(transform.position + Vector3.up * ProbeHeight, AimDirection, BodyRadius * 0.8f, _chargeDistance);

        protected override void OnActiveStarted()
        {
            _traveled = 0f;
            _hitPlayer = false;
            IsWallStunned = false;
        }

        protected override void TickActive(float dt)
        {
            float total = _chargeSpeed * dt;
            while (total > 0f)
            {
                float step = Mathf.Min(total, MaxStep, _chargeDistance - _traveled);
                if (step <= 0f) break;
                var flags = MoveRaw(AimDirection * step);
                _traveled += step;
                total -= step;
                CheckHit();

                if ((flags & CollisionFlags.Sides) != 0)
                {
                    IsWallStunned = true;
                    Attack.EndActive(_wallStun);
                    return;
                }
            }
            if (_traveled >= _chargeDistance - 1e-4f) Attack.EndActive();
        }

        protected override void OnAttackFinished() => IsWallStunned = false;

        void CheckHit()
        {
            if (_hitPlayer) return;
            Vector3 to = ToPlayer();
            if (to.magnitude > _width * 0.5f + PlayerRadius) return;
            _hitPlayer = TryHitPlayer();
        }
    }
}
