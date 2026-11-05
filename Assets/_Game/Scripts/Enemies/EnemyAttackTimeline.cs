using System;

namespace AThousandDawns.Enemies
{
    // 적 공격 한 번: 예고 → 판정 → 빈틈. 예고 중에 맞으면 멈춰서 늦춰지고(끊기지 않음), 빈틈 중에 맞으면 빈틈이 길어져요.
    public sealed class EnemyAttackTimeline
    {
        public enum Phase { Ready, Windup, Active, Recovery }

        float _windup, _active, _recovery;
        float _elapsed;  // 지금 단계에서 흐른 시간 (늦춰진 시간은 빼고)
        float _duration; // 지금 단계 길이
        float _pause;    // 예고가 멈춰 있는 남은 시간

        public Phase Current { get; private set; } = Phase.Ready;
        public bool IsAttacking => Current != Phase.Ready;
        public bool IsPaused => _pause > 0f;

        // 예고 절반 규칙: 앞 절반은 따라오며 방향을 맞추고, 뒤 절반은 고정
        public bool IsTracking => Current == Phase.Windup && _elapsed < _windup * 0.5f;

        public float WindupProgress => Current switch
        {
            Phase.Ready => 0f,
            Phase.Windup => _windup <= 0f ? 1f : Math.Min(1f, _elapsed / _windup),
            _ => 1f,
        };

        public event Action ActiveStarted;
        public event Action Finished;

        // active에 float.PositiveInfinity를 주면 EndActive를 부를 때까지 판정이 이어져요 (멧돼지 돌진).
        public void Start(float windup, float active, float recovery)
        {
            _windup = windup;
            _active = active;
            _recovery = recovery;
            _pause = 0f;
            Enter(Phase.Windup, windup);
        }

        public void Delay(float seconds)
        {
            if (Current == Phase.Windup) _pause += seconds;
        }

        public void ExtendRecovery(float seconds)
        {
            if (Current == Phase.Recovery) _duration += seconds;
        }

        public void EndActive(float? recoveryOverride = null)
        {
            if (Current != Phase.Active) return;
            Enter(Phase.Recovery, recoveryOverride ?? _recovery);
        }

        public void Cancel()
        {
            Current = Phase.Ready;
            _elapsed = 0f;
            _pause = 0f;
        }

        public void Tick(float dt)
        {
            while (dt > 0f && Current != Phase.Ready)
            {
                if (Current == Phase.Windup && _pause > 0f)
                {
                    float p = Math.Min(_pause, dt);
                    _pause -= p;
                    dt -= p;
                    continue;
                }

                float remaining = _duration - _elapsed;
                if (dt < remaining)
                {
                    _elapsed += dt;
                    return;
                }
                dt -= remaining;
                Advance();
            }
        }

        void Advance()
        {
            switch (Current)
            {
                case Phase.Windup:
                    Enter(Phase.Active, _active);
                    ActiveStarted?.Invoke();
                    break;
                case Phase.Active:
                    Enter(Phase.Recovery, _recovery);
                    break;
                case Phase.Recovery:
                    Cancel();
                    Finished?.Invoke();
                    break;
            }
        }

        void Enter(Phase phase, float duration)
        {
            Current = phase;
            _elapsed = 0f;
            _duration = duration;
        }
    }
}
