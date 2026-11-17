namespace AThousandDawns.Enemies
{
    // 하피 (차원 겹침, 판타지): 공중에 떠서 들개처럼 옆 · 뒤로 맴돌다가 멀리서(harpy.attack_range) 급강하로 뛰어들어 할퀴어요.
    //   들개와 달리 할퀸 뒤 옆으로 비켜서지 않고 그 자리에서 빈틈을 보여요 (플레이테스트 3차)
    public sealed class HarpyBrain : HoundBrain
    {
        public override EnemyKind Kind => EnemyKind.Harpy;
        protected override string Key => "harpy";
        public override string AttackName => "급강하";

        protected override void OnLeapFinished() => Attack.EndActive(); // 옆걸음 없이 바로 착지 빈틈
    }
}
