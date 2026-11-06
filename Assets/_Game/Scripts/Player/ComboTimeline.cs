using System;

namespace AThousandDawns.Player
{
    // 평타 3연타의 시간 흐름만 다루는 순수 로직이에요 (선딜 → 판정 → 후딜).
    // 누르고 있으면 1 → 2 → 3 → 1로 끊김 없이 이어지고, 후딜이 끝난 뒤 연결 시간 안에 누르면 다음 타로 이어져요.
    // 검보 조각이 있으면 3타째 뒤에 4타째가 붙어요 (SetExtraHit).
    public sealed class ComboTimeline
    {
        public readonly struct HitTiming
        {
            public readonly float Windup, Active, Recovery;
            public HitTiming(float windup, float active, float recovery) { Windup = windup; Active = active; Recovery = recovery; }
            public float Total => Windup + Active + Recovery;
        }

        public enum Phase { Idle, Windup, Active, Recovery, ChainWindow }

        readonly HitTiming[] _hits;
        HitTiming? _extraHit; // 검보 조각: 마지막 타 뒤에 한 타 더
        float _extraWindow;
        readonly float _chainWindow;
        readonly float _inputBuffer;
        float _phaseTime;
        float _sinceLastPress = float.PositiveInfinity;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public int HitIndex { get; private set; }
        public int BaseHitCount => _hits.Length;
        public int HitCount => _hits.Length + (_extraHit.HasValue ? 1 : 0);
        public bool IsExtraHit(int index) => index >= _hits.Length;
        public bool IsAttacking => CurrentPhase == Phase.Windup || CurrentPhase == Phase.Active || CurrentPhase == Phase.Recovery;
        public bool IsInActive => CurrentPhase == Phase.Active;
        // 대시로 끊기: 후딜 중(과 공격하지 않을 때)에만 가능
        public bool CanDashCancel => !IsAttacking || CurrentPhase == Phase.Recovery;

        public event Action<int> SwingStarted;
        public event Action<int> HitFrame;

        public ComboTimeline(HitTiming[] hits, float chainWindow, float inputBuffer)
        {
            if (hits == null || hits.Length == 0) throw new ArgumentException("타가 하나 이상 있어야 해요.", nameof(hits));
            _hits = hits;
            _chainWindow = chainWindow;
            _inputBuffer = inputBuffer;
        }

        // 검보 조각: 마지막 타 뒤 window초 안에 다시 공격하면(누르고 있어도) 한 타 더. null이면 끔.
        public void SetExtraHit(HitTiming? timing, float window)
        {
            _extraHit = timing;
            _extraWindow = window;
            if (!timing.HasValue && HitIndex >= _hits.Length && !IsAttacking) HitIndex = 0;
        }

        // 추가 타가 꺼진 뒤 그 타를 마저 휘두르는 중이면 마지막 타의 시간을 써요.
        HitTiming TimingOf(int index) => index < _hits.Length ? _hits[index] : _extraHit ?? _hits[_hits.Length - 1];

        public void Press() => _sinceLastPress = 0f;

        public void ConsumePress() => _sinceLastPress = float.PositiveInfinity;

        public void Cancel()
        {
            CurrentPhase = Phase.Idle;
            HitIndex = 0;
            _phaseTime = 0f;
        }

        public void Tick(float dt, bool held)
        {
            float remaining = dt;
            for (int guard = 0; guard < 32; guard++)
            {
                switch (CurrentPhase)
                {
                    case Phase.Idle:
                        if (Wants(held)) { StartSwing(0); continue; }
                        _sinceLastPress += remaining;
                        return;

                    case Phase.ChainWindow:
                        if (Wants(held)) { StartSwing(HitIndex); continue; }
                        if (!Advance(ref remaining, HitIndex >= _hits.Length ? _extraWindow : _chainWindow)) { _sinceLastPress += remaining; return; }
                        CurrentPhase = Phase.Idle;
                        HitIndex = 0;
                        continue;

                    case Phase.Windup:
                        if (!Advance(ref remaining, TimingOf(HitIndex).Windup)) return;
                        CurrentPhase = Phase.Active;
                        HitFrame?.Invoke(HitIndex);
                        continue;

                    case Phase.Active:
                        if (!Advance(ref remaining, TimingOf(HitIndex).Active)) return;
                        CurrentPhase = Phase.Recovery;
                        continue;

                    case Phase.Recovery:
                        if (!Advance(ref remaining, TimingOf(HitIndex).Recovery)) return;
                        int next = HitIndex + 1 >= HitCount ? 0 : HitIndex + 1;
                        if (Wants(held)) { StartSwing(next); continue; }
                        HitIndex = next;
                        CurrentPhase = Phase.ChainWindow;
                        continue;
                }
            }
        }

        // 남은 시간을 이 구간에 써요. 구간이 끝나면 true와 함께 남은 시간을 돌려줘요.
        bool Advance(ref float remaining, float duration)
        {
            float need = duration - _phaseTime;
            if (remaining < need)
            {
                _phaseTime += remaining;
                _sinceLastPress += remaining;
                remaining = 0f;
                return false;
            }
            remaining -= need;
            _sinceLastPress += need;
            _phaseTime = 0f;
            return true;
        }

        bool Wants(bool held) => held || _sinceLastPress <= _inputBuffer;

        void StartSwing(int index)
        {
            HitIndex = index;
            CurrentPhase = Phase.Windup;
            _phaseTime = 0f;
            _sinceLastPress = float.PositiveInfinity;
            SwingStarted?.Invoke(index);
        }
    }
}
