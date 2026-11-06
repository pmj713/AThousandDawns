using System;

namespace AThousandDawns.Player
{
    // 대시 충전: 하나씩 재충전돼요.
    public sealed class DashCharges
    {
        readonly float _rechargeTime;
        float _rechargeProgress;

        public int MaxCharges { get; private set; }
        public int Charges { get; private set; }
        public float RechargeRatio => Charges >= MaxCharges ? 1f : _rechargeProgress / _rechargeTime;

        // 재충전 빠르기 배율 (기물 부작용 "대시 회복 −10%"면 0.9)
        public float RechargeSpeed { get; set; } = 1f;

        public DashCharges(int maxCharges, float rechargeTime)
        {
            if (maxCharges < 1) throw new ArgumentOutOfRangeException(nameof(maxCharges));
            MaxCharges = maxCharges;
            Charges = maxCharges;
            _rechargeTime = Math.Max(0.01f, rechargeTime);
        }

        public bool TryConsume()
        {
            if (Charges <= 0) return false;
            Charges--;
            return true;
        }

        // 즉시 충전 (#경공 간파: 대시 1회)
        public void Refill(int count)
        {
            Charges = Math.Min(MaxCharges, Charges + Math.Max(0, count));
            if (Charges >= MaxCharges) _rechargeProgress = 0f;
        }

        public void SetMaxCharges(int maxCharges)
        {
            MaxCharges = Math.Max(1, maxCharges);
            Charges = Math.Min(Charges, MaxCharges);
        }

        public void Tick(float dt)
        {
            if (Charges >= MaxCharges) { _rechargeProgress = 0f; return; }
            _rechargeProgress += dt * RechargeSpeed;
            while (_rechargeProgress >= _rechargeTime && Charges < MaxCharges)
            {
                _rechargeProgress -= _rechargeTime;
                Charges++;
            }
            if (Charges >= MaxCharges) _rechargeProgress = 0f;
        }
    }
}
