namespace AThousandDawns.Enemies
{
    // 오크 (차원 겹침, 판타지): 멧돼지처럼 일직선으로 돌진해 들이받아요. 벽에 부딪히면 잠시 멍해져요 (빈틈).
    public sealed class OrcBrain : BoarBrain
    {
        public override EnemyKind Kind => EnemyKind.Orc;
        protected override string Key => "orc";
        public override string AttackName => "돌진";
    }
}
