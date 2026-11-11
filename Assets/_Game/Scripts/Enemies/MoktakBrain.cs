namespace AThousandDawns.Enemies
{
    // 목탁승 (폐사찰, 몬스터 시트 43행): 나팔수처럼 거리를 두고 목탁을 쳐요 (예고 1.5초, 예고 중에 맞히면 끊김).
    // 부르는 대신 둘레 7칸 안 다른 적에게 가호 (최대 체력 25%만큼 피해를 먼저 막고, 그동안 몸이 금빛). 가호가 없는 적이 있을 때만, 공격 간격 8초마다.
    // 먼저 잡아야 하는 지원형이에요.
    public sealed class MoktakBrain : HornBrain
    {
        float _radius, _ratio;
        public static event System.Action<MoktakBrain, UnityEngine.Vector3, float> Struck; // 목탁을 친 자리 · 가호 범위

        public override EnemyKind Kind => EnemyKind.Moktak;
        protected override string Key => "moktak";
        public override string AttackName => "목탁 가호";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _radius = Tuning("ward_radius", 7f);
            _ratio = Tuning("ward_ratio", 0.25f);
        }

        protected override bool WantsToAttack(float distance)
        {
            foreach (var e in Director.Enemies) if (NeedsWard(e)) return true;
            return false;
        }

        bool NeedsWard(EnemyBrain e) =>
            e != null && e != this && !e.IsDead && e.Damageable.Ward <= 0 && (e.transform.position - transform.position).magnitude <= _radius;

        protected override void OnActiveStarted()
        {
            Director.SpawnBurst(transform.position, 1.5f); // 목탁 소리
            Struck?.Invoke(this, transform.position, _radius); // 이펙트 신호 (아트: InkTempleEnemyFx)
            foreach (var e in Director.Enemies)
                if (NeedsWard(e)) e.Damageable.AddWard(UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(e.Damageable.MaxHealth * _ratio)));
        }
    }
}
