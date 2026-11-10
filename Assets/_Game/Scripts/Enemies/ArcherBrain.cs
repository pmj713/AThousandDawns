using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 궁수: 5~7칸을 유지하고, 가까이 오면 2칸 물러나요. 옆걸음(초당 2칸)하며 조준하고, 쏘는 순간 0.2초 멈춰요.
    // 예고 절반 규칙은 "겨누는 지점"에 적용해요: 앞 절반은 플레이어를 따라가고, 뒤 절반은 그 지점에 고정.
    // 옆걸음을 계속해도 화살은 고정된 지점을 지나가니까, 가만히 있으면 맞고 예고를 보고 비키면 피해요.
    public sealed class ArcherBrain : EnemyBrain
    {
        public override string AttackName => "화살";

        const float ShotHeight = 1f;

        float _keepMin, _keepMax, _retreatDistance, _strafeSpeed, _shotPause, _arrowSpeed, _arrowRadius;
        float _retreatLeft, _plannedLength;
        Vector3 _aimPoint;

        public override EnemyKind Kind => EnemyKind.Archer;
        protected override string Key => "archer";
        protected override bool IsRanged => true;
        protected override float ActiveTime => _shotPause;

        protected override void ReadTuning()
        {
            _keepMin = Tuning("keep_min", 5f);
            _keepMax = Tuning("keep_max", 7f);
            _retreatDistance = Tuning("retreat_distance", 2f);
            _strafeSpeed = Tuning("strafe_speed", 2f);
            _shotPause = Tuning("shot_pause", 0.2f);
            _arrowSpeed = Tuning("arrow_speed", 10f);
            _arrowRadius = Tuning("arrow_radius", 0.2f);
        }

        protected override bool WantsToAttack(float distance) =>
            distance <= AttackRange && _retreatLeft <= 0f && HasClearLine(distance, _arrowRadius, ShotHeight);

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (_retreatLeft > 0f)
            {
                var flags = Move(-dir * MoveSpeed, dt);
                _retreatLeft -= MoveSpeed * dt;
                if ((flags & CollisionFlags.Sides) != 0) _retreatLeft = 0f; // 벽에 몰리면 그 자리에서 싸워요
                return;
            }

            if (d < _keepMin) _retreatLeft = _retreatDistance;
            else if (d > _keepMax) Move(dir * MoveSpeed, dt);
            else Move(Strafe(dir) * (_strafeSpeed * 0.5f), dt);
        }

        protected override void OnAttackStarted() => _aimPoint = Player.transform.position;

        protected override void TickWindup(float dt, bool tracking)
        {
            if (tracking) _aimPoint = Player.transform.position;
            Move(Strafe(ToPlayer().normalized) * _strafeSpeed, dt);
            AimAtPoint();
        }

        void AimAtPoint()
        {
            Vector3 to = _aimPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
            FaceToward(AimDirection);
        }

        protected override List<Vector2> TelegraphShape()
        {
            _plannedLength = PlannedLength();
            return TelegraphShapes.Rect(_arrowRadius * 2f, _plannedLength);
        }

        protected override void UpdateTelegraph()
        {
            float length = PlannedLength();
            if (Mathf.Abs(length - _plannedLength) > 0.05f)
            {
                _plannedLength = length;
                Telegraph.SetShape(TelegraphShapes.Rect(_arrowRadius * 2f, _plannedLength));
            }
            base.UpdateTelegraph();
        }

        float PlannedLength() =>
            ObstacleProbe.Distance(transform.position + Vector3.up * ShotHeight, AimDirection, _arrowRadius, AttackRange);

        protected override void OnActiveStarted()
        {
            AimAtPoint();
            Vector3 origin = transform.position + Vector3.up * ShotHeight;
            Director.SpawnArrow(origin, AimDirection, _arrowSpeed, AttackRange, _arrowRadius, AttackPower, Body);
        }

        protected override void TickActive(float dt) { } // 쏘는 순간 멈춤
    }
}
