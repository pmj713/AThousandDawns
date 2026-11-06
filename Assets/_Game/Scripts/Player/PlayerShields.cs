using System;
using System.Collections.Generic;

namespace AThousandDawns.Player
{
    // 보호막 (#금강, 키워드 탭): 피해 공식을 다 계산한 뒤 보호막부터 깎아요. 모든 보호막을 합쳐 최대 체력의 30%까지.
    // 보호막마다 따로 지속되고 (지속 0이면 깨질 때까지), 깨지거나 시간이 지나면 사라져요.
    public sealed class PlayerShields
    {
        sealed class Shield
        {
            public int Amount, Max;
            public float TimeLeft; // 0 이하면 깨질 때까지
            public bool Timed;
        }

        readonly List<Shield> _shields = new List<Shield>();

        public int Total
        {
            get
            {
                int sum = 0;
                foreach (var s in _shields) sum += s.Amount;
                return sum;
            }
        }

        public bool Any => _shields.Count > 0;

        // 깨진 보호막의 최대치 (#금강 6단계 충격파)
        public event Action<int> Broken;

        // cap = 최대 체력 × 30%. 남은 자리만큼만 붙어요.
        public void Add(int amount, float duration, int cap)
        {
            int room = cap - Total;
            amount = Math.Min(amount, room);
            if (amount <= 0) return;
            _shields.Add(new Shield { Amount = amount, Max = amount, TimeLeft = duration, Timed = duration > 0f });
        }

        // 보호막이 먼저 막고 남은 피해를 돌려줘요. 먼저 붙은 보호막부터 깎여요.
        public int Absorb(int damage)
        {
            for (int i = 0; i < _shields.Count && damage > 0;)
            {
                var s = _shields[i];
                int take = Math.Min(s.Amount, damage);
                s.Amount -= take;
                damage -= take;
                if (s.Amount <= 0)
                {
                    _shields.RemoveAt(i);
                    Broken?.Invoke(s.Max);
                    continue;
                }
                i++;
            }
            return damage;
        }

        public void Tick(float dt)
        {
            for (int i = _shields.Count - 1; i >= 0; i--)
            {
                var s = _shields[i];
                if (!s.Timed) continue;
                s.TimeLeft -= dt;
                if (s.TimeLeft <= 0f) _shields.RemoveAt(i);
            }
        }

        public void Clear() => _shields.Clear();
    }
}
