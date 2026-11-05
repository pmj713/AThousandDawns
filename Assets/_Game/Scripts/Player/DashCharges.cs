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

        public void SetMaxCharges(int maxCharges)
        {
            MaxCharges = Math.Max(1, maxCharges);
            Charges = Math.Min(Charges, MaxCharges);
        }

        public void Tick(float dt)
        {
            if (Charges >= MaxCharges) { _rechargeProgress = 0f; return; }
            _rechargeProgress += dt;
            while (_rechargeProgress >= _rechargeTime && Charges < MaxCharges)
            {
                _rechargeProgress -= _rechargeTime;
                Charges++;
            }
            if (Charges >= MaxCharges) _rechargeProgress = 0f;
        }
    }
}
