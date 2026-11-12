using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈궁수 (천마총, 몬스터 시트 53행 "3연발"): 궁수처럼 거리를 두다가 붉은 화살 세 발을 0.25초 간격으로 쏴요.
    // 첫 발은 지금 자리, 둘째·셋째 발은 플레이어가 움직이는 쪽을 앞질러 겨눠요 (한 방향으로만 피하면 맞아요).
    public sealed class BloodArcherBrain : EnemyBrain
    {
        const float ShotHeight = 1f;

        float _keepMin, _keepMax, _arrowSpeed, _arrowRadius, _gap, _lead, _shotTimer;
        int _shots, _fired;
        Vector3 _lastPlayer, _playerVelocity;

        public override EnemyKind Kind => EnemyKind.BloodArcher;
        protected override string Key => "bloodarcher";
        protected override bool IsRanged => true;
        public override string AttackName => "혈시 3연발";
        protected override float ActiveTime => (_shots - 1) * _gap + 0.1f;

        protected override void ReadTuning()
        {
            _keepMin = Tuning("keep_min", 5f);
            _keepMax = Tuning("keep_max", 7f);
            _arrowSpeed = Tuning("arrow_speed", 12f);
            _arrowRadius = Tuning("arrow_radius", 0.2f);
            _gap = Tuning("shot_gap", 0.25f);
            _lead = Tuning("lead_time", 0.35f);
            _shots = Mathf.Max(1, Mathf.RoundToInt(Tuning("shots", 3f)));
        }

        protected override void TickAlways(float dt)
        {
            if (Player == null || dt <= 0f) return;
            Vector3 p = Player.transform.position;
            _playerVelocity = Vector3.Lerp(_playerVelocity, (p - _lastPlayer) / dt, 0.3f);
            _playerVelocity.y = 0f;
            _lastPlayer = p;
        }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange && HasClearLine(distance, _arrowRadius, ShotHeight);

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d < _keepMin) Move(-dir * MoveSpeed, dt);
            else if (d > _keepMax) Move(dir * MoveSpeed, dt);
            else if (HasClearLine(d, _arrowRadius, ShotHeight)) Move(Strafe(dir) * (MoveSpeed * 0.4f), dt);
            else if ((Move(Reposition(dir) * MoveSpeed, dt) & CollisionFlags.Sides) != 0) FlipReposition(); // 사선이 막혔으면 한쪽으로 꾸준히 옮겨 쏠 자리를 찾아요
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Rect(_arrowRadius * 2f, AttackRange);

        protected override void OnActiveStarted()
        {
            _fired = 0;
            _shotTimer = 0f;
            Shoot();
        }

        protected override void TickActive(float dt)
        {
            _shotTimer += dt;
            while (_fired < _shots && _shotTimer >= _fired * _gap) Shoot();
        }

        // n번째 발은 플레이어가 움직이는 쪽을 n × lead초 앞질러 겨눠요
        void Shoot()
        {
            Vector3 aim = Player.transform.position + _playerVelocity * (_lead * _fired) - transform.position;
            aim.y = 0f;
            Vector3 dir = aim.sqrMagnitude > 1e-4f ? aim.normalized : Facing;
            FaceToward(dir);
            Director.SpawnArrow(transform.position + Vector3.up * ShotHeight, dir, _arrowSpeed, AttackRange, _arrowRadius, AttackPower, Body);
            _fired++;
        }
    }
}
