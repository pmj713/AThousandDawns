namespace AThousandDawns.Enemies
{
    // 깃발수 (장강 수채, 몬스터 시트 51행): 나팔수처럼 거리를 두고 깃발을 흔들어요 (예고 1.5초, 맞으면 끊김).
    // 부르지 않고 지휘해요: 둘레 7칸 안 다른 적이 5초 동안 이동과 공격 (예고·빈틈 포함) +30%. 지휘받지 않은 적이 있을 때만, 공격 간격마다.
    // 목탁승(가호, 방어)과 달리 공격을 빠르게 해서 먼저 잡아야 해요.
    public sealed class FlagBearerBrain : HornBrain
    {
        float _radius, _ratio, _duration;

        public override EnemyKind Kind => EnemyKind.FlagBearer;
        protected override string Key => "flagbearer";
        public override string AttackName => "지휘";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _radius = Tuning("command_radius", 7f);
            _ratio = Tuning("command_haste", 0.3f);
            _duration = Tuning("command_time", 5f);
        }

        bool NeedsCommand(EnemyBrain e) =>
            e != null && e != this && !e.IsDead && !e.IsHastened && (e.transform.position - transform.position).magnitude <= _radius;

        protected override bool WantsToAttack(float distance)
        {
            foreach (var e in Director.Enemies) if (NeedsCommand(e)) return true;
            return false;
        }

        protected override void OnActiveStarted()
        {
            Director.SpawnBurst(transform.position, 1.5f); // 깃발을 휘둘러요
            foreach (var e in Director.Enemies)
                if (NeedsCommand(e)) e.Hasten(_ratio, _duration);
        }
    }
}
