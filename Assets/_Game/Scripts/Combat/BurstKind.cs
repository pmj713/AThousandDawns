namespace AThousandDawns.Combat
{
    // 폭발 종류: 아트가 종류마다 다른 폭발을 그려요 (EnemyDirector.Burst · PlayerController.BurstCue 신호와 함께).
    // Default는 예전처럼 공용 먹 폭발이에요. 새 종류는 맨 뒤에 더해요.
    public enum BurstKind { Default, Gunpowder, BloodGlob, BloodDeath, Slam, Spin, Quake, Vajra, Miasma, Water, Shatter, Echo, Gangi, Lightning }
}
