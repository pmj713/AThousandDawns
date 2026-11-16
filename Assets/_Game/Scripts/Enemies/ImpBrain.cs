namespace AThousandDawns.Enemies
{
    // 임프 (차원 겹침, 판타지): 날아다니며 거리를 두고 화염탄을 던져요 (궁수와 같은 규칙, 투사체는 Arrow · 아트가 불덩이로 꾸며요).
    public sealed class ImpBrain : ArcherBrain
    {
        public override EnemyKind Kind => EnemyKind.Imp;
        protected override string Key => "imp";
        public override string AttackName => "화염탄";
    }
}
