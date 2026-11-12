using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈폭자 (천마총, 몬스터 시트 56행 "화상 장판"): 다가와 터지지 않아요. 4~6칸을 두고 핏덩이를 던져요.
    //   핏덩이: 플레이어 자리에 예고 원 (반경 1.2칸, 0.9초) → 맞으면 ×0.8 + 그 자리에 화상 장판 4초.
    //   쓰러지면 1.2초 동안 붉은 예고 원 (반경 2칸)이 차오르다가 터져요 (×1.0). 검으로 잡았으면 대시로 빠지면 돼요.
    public sealed class BloodBomberBrain : EnemyBrain
    {
        float _keepMin, _keepMax, _globRadius, _globFall, _globRatio, _fireLife, _blastRadius, _blastFuse, _blastRatio;

        public override EnemyKind Kind => EnemyKind.BloodBomber;
        protected override string Key => "bloodbomber";
        public override string AttackName => "핏덩이";
        protected override float ActiveTime => 0.1f;
        protected override bool IsRanged => true;

        protected override void ReadTuning()
        {
            _keepMin = Tuning("keep_min", 4f);
            _keepMax = Tuning("keep_max", 6f);
            _globRadius = Tuning("glob_radius", 1.2f);
            _globFall = Tuning("glob_fall", 0.9f);
            _globRatio = Tuning("glob_ratio", 0.8f);
            _fireLife = Tuning("fire_life", 4f);
            _blastRadius = Tuning("blast_radius", 2f);
            _blastFuse = Tuning("blast_fuse", 1.2f);
            _blastRatio = Tuning("blast_ratio", 1f);
        }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d < _keepMin) Move(-dir * MoveSpeed, dt);
            else if (d > _keepMax) Move(dir * MoveSpeed, dt);
            else Move(Strafe(dir) * (MoveSpeed * 0.4f), dt);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(0.6f);

        protected override void OnActiveStarted() =>
            BloodBurst.Create(Director, Player.transform.position, _globRadius, _globFall, AttackPower * _globRatio, Body, _fireLife);

        protected override void TickActive(float dt) { }

        // 쓰러지면 늦게 터져요
        protected override void OnDied() =>
            BloodBurst.Create(Director, transform.position, _blastRadius, _blastFuse, AttackPower * _blastRatio, null, 0f);
    }
}
