using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 거리 두고 던지기 계열 (고블린 단검꾼 · 고블린 궁수 · 임프): keep_min~keep_max칸을 유지하고, 가까이 오면 retreat_distance만큼 물러나요.
    //   궁수(ArcherBrain)와 같은 거리 두기지만, 겨누는 자리가 노리는 적(Rival) 쪽이에요 (다른 적을 노릴 때 플레이어 쪽으로 던지지 않게).
    //   예고 앞 절반은 그 자리를 따라가고 뒤 절반은 고정. 던지는 순간 shot_pause초 멈춰요. 무엇을 던질지는 하위 클래스가 정해요 (Fire).
    public abstract class KiteBrain : EnemyBrain
    {
        protected const float ShotHeight = 1f;

        float _keepMin, _keepMax, _retreatDistance, _strafeSpeed, _shotPause;
        float _retreatLeft, _plannedLength;
        protected Vector3 AimPoint;

        protected override bool IsRanged => true;
        protected override float ActiveTime => _shotPause;
        protected virtual float LineRadius => 0.2f;   // 사선 검사 반경
        protected virtual bool NeedsClearLine => true; // 곡사는 엄폐물 너머로도 쏴요
        protected virtual float TelegraphHalfWidth => LineRadius; // 예고 폭의 절반 (임프 화염구는 터지는 거리만큼)

        protected override void ReadTuning()
        {
            _keepMin = Tuning("keep_min", 5f);
            _keepMax = Tuning("keep_max", 7f);
            _retreatDistance = Tuning("retreat_distance", 2f);
            _strafeSpeed = Tuning("strafe_speed", 2f);
            _shotPause = Tuning("shot_pause", 0.2f);
            ReadShotTuning();
        }

        protected virtual void ReadShotTuning() { }

        // 던지는 순간 (origin은 손 높이, direction은 겨눈 방향)
        protected abstract void Fire(Vector3 origin, Vector3 direction);

        // 노리는 자리: 다른 진영 적을 노리면 그 적, 아니면 플레이어
        protected Vector3 TargetPoint => transform.position + ToPlayer();
        protected virtual Vector3 PickAimPoint() => TargetPoint;

        bool LineClear(float distance) => !NeedsClearLine || HasClearLine(distance, LineRadius, ShotHeight);

        protected override bool WantsToAttack(float distance) => distance <= AttackRange && _retreatLeft <= 0f && LineClear(distance);

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
            else if (LineClear(d)) Move(Strafe(dir) * (_strafeSpeed * 0.5f), dt);
            else if ((Move(Reposition(dir) * MoveSpeed, dt) & CollisionFlags.Sides) != 0) FlipReposition(); // 사선이 막혔으면 한쪽으로 꾸준히 옮겨요
        }

        protected override void OnAttackStarted() => AimPoint = PickAimPoint();

        // 던질 준비 동안은 제자리에 서서 겨눠요 (옆걸음 없음)
        protected override void TickWindup(float dt, bool tracking)
        {
            if (tracking) AimPoint = PickAimPoint();
            AimAtPoint();
        }

        protected void AimAtPoint()
        {
            Vector3 to = AimPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
            FaceToward(AimDirection);
        }

        // 기본 예고: 날아갈 길을 따라 가는 직사각형 (벽에 막히면 거기까지)
        protected override List<Vector2> TelegraphShape()
        {
            _plannedLength = PlannedLength();
            return TelegraphShapes.Rect(TelegraphHalfWidth * 2f, _plannedLength);
        }

        protected override void UpdateTelegraph()
        {
            float length = PlannedLength();
            if (Mathf.Abs(length - _plannedLength) > 0.05f)
            {
                _plannedLength = length;
                Telegraph.SetShape(TelegraphShapes.Rect(TelegraphHalfWidth * 2f, _plannedLength));
            }
            base.UpdateTelegraph();
        }

        float PlannedLength() =>
            ObstacleProbe.Distance(transform.position + Vector3.up * ShotHeight, AimDirection, LineRadius, AttackRange);

        protected override void OnActiveStarted()
        {
            AimAtPoint();
            Fire(transform.position + Vector3.up * ShotHeight, AimDirection);
        }

        protected override void TickActive(float dt) { } // 던지는 순간 멈춤
    }
}
