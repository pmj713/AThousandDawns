using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 파계승 (폐사찰, 몬스터 시트 44행 "정면 방어 대신 일정 시간 무적"): 졸개처럼 다가와 선장으로 전진 베기.
    // 가까이 오면 가끔 금강불괴 자세 (2초, 몸이 금빛): 그동안 서서 버티며 맞아도 1씩만 들어가고 경직도 없어요.
    // 자세가 풀리면 곧바로 베러 와요. 지속 피해(화상 등)는 그대로 들어가요.
    public sealed class MonkBrain : LungeSlashBrain
    {
        static readonly Color Gold = new Color(0.95f, 0.78f, 0.3f);

        float _stanceTime, _stanceCooldown, _stanceRange;
        float _stanceLeft, _stanceReady;

        public override EnemyKind Kind => EnemyKind.Monk;
        protected override string Key => "monk";
        public override string AttackName => IsStanced ? "금강불괴" : "선장";
        protected override bool SuperArmor => IsStanced;

        public bool IsStanced => _stanceLeft > 0f;

        protected override void ReadMoreTuning()
        {
            _stanceTime = Tuning("stance_time", 2f);
            _stanceCooldown = Tuning("stance_cooldown", 7f);
            _stanceRange = Tuning("stance_range", 4f);
            _stanceReady = _stanceCooldown * 0.4f; // 첫 자세는 조금 빨리
        }

        protected override void OnInitialized()
        {
            Body.SituationalResolver = (attacker, tag) => IsStanced ? 0f : 1f; // 0이어도 최소 1은 들어가요
        }

        protected override void TickAlways(float dt)
        {
            if (IsStanced)
            {
                _stanceLeft -= dt;
                if (!IsStanced) EndStance();
                return;
            }
            _stanceReady -= dt;
            if (_stanceReady <= 0f && Attack.Current == EnemyAttackTimeline.Phase.Ready && Player != null && ToPlayer().magnitude <= _stanceRange)
                StartStance();
        }

        protected override bool WantsToAttack(float distance) => !IsStanced && base.WantsToAttack(distance);

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsStanced)
            {
                FaceToward(ToPlayer()); // 버티고 서서 노려봐요
                return;
            }
            base.TickMovement(dt, canAttack);
        }

        void StartStance()
        {
            _stanceLeft = _stanceTime;
            Body.SetTint(Gold);
        }

        void EndStance()
        {
            _stanceReady = _stanceCooldown;
            Body.SetTint(null);
        }
    }
}
