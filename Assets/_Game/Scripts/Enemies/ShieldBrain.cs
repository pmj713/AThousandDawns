using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 방패 산적: 정면 120°는 피해 ×0.2, 초당 180°로만 돌아서 옆·뒤가 열려요.
    // 방패를 든 채 초당 2칸으로 다가와 2칸 돌진 베기 + 넉백 1칸. 돌진 뒤 빈틈(0.8초) 동안 방패를 내려요.
    // 대시 공격에 맞으면 방패가 깨져요 (그 한 방은 온전히 들어가요).
    public sealed class ShieldBrain : LungeSlashBrain
    {
        static readonly Vector3 RaisedPosition = new Vector3(0f, 0.85f, 0.5f);
        static readonly Vector3 LoweredPosition = new Vector3(0f, 0.35f, 0.55f);

        float _guardAngle, _guardRatio, _turnSpeed, _guardMoveSpeed, _knockback;
        Transform _shield;
        bool _broken;

        public override EnemyKind Kind => EnemyKind.Shield;
        protected override string Key => "shield";
        protected override float TurnSpeed => _turnSpeed;
        protected override float HitKnockback => _knockback;
        protected override float ApproachSpeed => GuardUp ? _guardMoveSpeed : MoveSpeed;

        public bool ShieldBroken => _broken;
        bool GuardUp => !_broken && Attack.Current != EnemyAttackTimeline.Phase.Recovery;

        public void SetShieldVisual(Transform shield) => _shield = shield;

        protected override void ReadMoreTuning()
        {
            _guardAngle = Tuning("guard_angle", 120f);
            _guardRatio = Tuning("guard_ratio", 0.2f);
            _turnSpeed = Tuning("turn_speed", 180f);
            _guardMoveSpeed = Tuning("guard_move_speed", 2f);
            _knockback = Tuning("knockback", 1f);
        }

        protected override void OnInitialized() => Body.SituationalResolver = ResolveHit;

        float ResolveHit(Vector3 attacker, HitTag tag)
        {
            if (_broken) return 1f;
            if (tag == HitTag.DashAttack)
            {
                _broken = true;
                if (_shield != null) Destroy(_shield.gameObject);
                return 1f;
            }
            if (!GuardUp) return 1f;
            Vector3 to = attacker - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return _guardRatio; // 겹쳐 있으면 정면으로 쳐요
            return Vector3.Angle(Facing, to) <= _guardAngle * 0.5f ? _guardRatio : 1f;
        }

        void LateUpdate()
        {
            if (_shield == null) return;
            bool lowered = Attack.Current == EnemyAttackTimeline.Phase.Recovery;
            _shield.localPosition = lowered ? LoweredPosition : RaisedPosition;
            _shield.localRotation = lowered ? Quaternion.Euler(70f, 0f, 0f) : Quaternion.identity;
        }
    }
}
