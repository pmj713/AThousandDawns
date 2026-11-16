namespace AThousandDawns.Enemies
{
    // 하피 (차원 겹침, 판타지): 공중에 떠서 들개처럼 옆 · 뒤로 맴돌다가 멀리서(harpy.attack_range) 급강하로 뛰어들어 할퀴고 빠져요
    public sealed class HarpyBrain : HoundBrain
    {
        public override EnemyKind Kind => EnemyKind.Harpy;
        protected override string Key => "harpy";
        public override string AttackName => "급강하";
    }
}
