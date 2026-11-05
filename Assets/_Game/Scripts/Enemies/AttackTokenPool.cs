using System.Collections.Generic;

namespace AThousandDawns.Enemies
{
    // 동시 공격 제한: 토큰을 가진 적만 예고를 시작해요. 공격이 끝나거나 죽으면 돌려놓아요.
    public sealed class AttackTokenPool
    {
        readonly HashSet<object> _holders = new HashSet<object>();

        public AttackTokenPool(int capacity) => Capacity = capacity;

        public int Capacity { get; set; }
        public int InUse => _holders.Count;
        public bool HasFree => _holders.Count < Capacity;

        public bool Holds(object owner) => _holders.Contains(owner);

        public bool TryAcquire(object owner)
        {
            if (_holders.Contains(owner)) return true;
            if (_holders.Count >= Capacity) return false;
            _holders.Add(owner);
            return true;
        }

        public void Release(object owner) => _holders.Remove(owner);
    }
}
