using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 천마 근위 (천마총, 몬스터 시트 55행 "정면 공격에 반격"): 큰 철판 방패로 정면 120°를 막고 다가와 베요.
    // 3칸 안에서 정면으로 맞으면 막고 그 자리에서 곧바로 되받아 베요 (앞 부채꼴 반경 2.5칸 ×1.0, 1.2초마다 한 번).
    // 옆·뒤로 돌아가거나, 대시 공격으로 방패를 깨야 해요 (방패 산적과 같아요). 멀리서 쏜 공격은 막기만 해요.
    public sealed class DemonGuardBrain : ShieldBrain
    {
        float _counterRange, _counterRadius, _counterAngle, _counterRatio, _counterCooldown, _counterTimer;

        public override EnemyKind Kind => EnemyKind.DemonGuard;
        protected override string Key => "demonguard";
        public override string AttackName => _countering ? "반격" : null;
        public int Counters { get; private set; }

        bool _countering;

        protected override void ReadMoreTuning()
        {
            base.ReadMoreTuning();
            _counterRange = Tuning("counter_range", 3f);
            _counterRadius = Tuning("counter_radius", 2.5f);
            _counterAngle = Tuning("counter_angle", 120f);
            _counterRatio = Tuning("counter_ratio", 1f);
            _counterCooldown = Tuning("counter_cooldown", 1.2f);
        }

        protected override void TickAlways(float dt)
        {
            if (_counterTimer > 0f) _counterTimer -= dt;
            _countering = _counterTimer > _counterCooldown - 0.3f; // 반격 직후 잠깐
        }

        protected override float BlockedRatio(Vector3 attacker, float guardRatio)
        {
            Vector3 to = attacker - transform.position;
            to.y = 0f;
            if (to.magnitude <= _counterRange && _counterTimer <= 0f) Counter();
            return guardRatio;
        }

        void Counter()
        {
            _counterTimer = _counterCooldown;
            Counters++;
            Director.SpawnBurst(transform.position + Facing * 1.2f, 1f);
            if (HitShapes.InSector(transform.position, Facing, _counterRadius, _counterAngle, Player.transform.position, PlayerRadius))
                Player.TryReceiveHit(AttackPower * _counterRatio, transform.position, HitKnockback, Body);
        }
    }
}
