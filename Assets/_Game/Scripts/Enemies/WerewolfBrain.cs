namespace AThousandDawns.Enemies
{
    // 늑대인간 (차원 겹침, 판타지): 들개처럼 파고들어 발톱으로 베고 빠져요. 체력 werewolf.enrage_hp 아래로 떨어지면 광폭화:
    //   이동 werewolf.enrage_speed배 · 공격 간격 werewolf.enrage_interval배 (한 번만)
    public sealed class WerewolfBrain : HoundBrain
    {
        public override EnemyKind Kind => EnemyKind.Werewolf;
        protected override string Key => "werewolf";
        public override string AttackName => "발톱 베기";

        public static event System.Action<WerewolfBrain> Enraged; // 광폭화 (아트: 포효 · 붉은 김)
        public bool IsEnraged { get; private set; }

        protected override void OnHitTaken()
        {
            if (IsEnraged || Body.Health > Body.MaxHealth * Tuning("enrage_hp", 0.5f)) return;
            IsEnraged = true;
            MoveSpeed *= Tuning("enrage_speed", 1.4f);
            AttackInterval *= Tuning("enrage_interval", 0.6f);
            Enraged?.Invoke(this);
        }
    }
}
