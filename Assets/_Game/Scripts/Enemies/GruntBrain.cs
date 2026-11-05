namespace AThousandDawns.Enemies
{
    // 졸개: 다가와서 전진 베기 (부채꼴 60°, 반경 1.2 + 전진 1.5 = 실제 사거리 약 2.7칸).
    public sealed class GruntBrain : LungeSlashBrain
    {
        public override EnemyKind Kind => EnemyKind.Grunt;
        protected override string Key => "grunt";
    }
}
