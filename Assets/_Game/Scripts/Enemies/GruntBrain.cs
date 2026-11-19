namespace AThousandDawns.Enemies
{
    // 졸개: 다가와 도를 머리 뒤로 들었다가 한 걸음 내디디며 바닥까지 내려찍어요.
    // 판정은 앞으로 좁고 긴 직선 (길이 grunt.attack_range, 내디딘 걸음 grunt.lunge 포함 · 폭 grunt.attack_width). 옆은 비어요.
    public sealed class GruntBrain : LungeSlashBrain
    {
        public override EnemyKind Kind => EnemyKind.Grunt;
        protected override string Key => "grunt";
        public override string AttackName => "내려찍기";
        protected override int StrikeShape => ShapeLine;
    }
}
