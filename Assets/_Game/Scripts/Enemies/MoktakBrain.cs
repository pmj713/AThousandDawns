namespace AThousandDawns.Enemies
{
    // 목탁승 (폐사찰, 몬스터 시트 43행): 나팔수처럼 거리를 두고 목탁을 쳐서 (예고 중에 맞히면 끊김) 망령 2마리를 불러요.
    public sealed class MoktakBrain : HornBrain
    {
        public override EnemyKind Kind => EnemyKind.Moktak;
        protected override string Key => "moktak";
        protected override EnemyKind SummonKind => EnemyKind.Wraith;
    }
}
