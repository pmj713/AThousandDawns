namespace AThousandDawns.Enemies
{
    // 오우거 (차원 겹침, 판타지 지역 보스): 철권 마웅과 같은 규칙에 덩치만 커요 (보스 칸, 50%에서 2페이즈). 패턴은 겹치지 않게 저만의 것:
    //   몽둥이 내려찍기 (가까울 때): 앞 좁고 긴 직선 한 방 (무거움)
    //   바위 던지기 (중간 거리): ogre.throw_interval마다 플레이어 둘레에 바위 셋 (예고 원 뒤 떨어짐)
    //   땅울림 도약 (멀 때): 내려앉을 자리 예고 원 → 뛰어 내려앉으며 넓은 충격파
    //   분노 (2페이즈): 이동 ×ogre.rage_speed, 예고 ×ogre.rage_windup
    public sealed class OgreBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.Ogre;
        protected override string Key => "ogre";
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override float DeathDelay => 1.5f;
        protected override string JabName => "몽둥이 내려찍기";
        protected override string FarLeapName => "땅울림 도약";
        protected override MeleeKind Melee => MeleeKind.Overhead;
        protected override SpecialKind Special => SpecialKind.None;
        protected override FarKind Far => FarKind.Leap;
        protected override float MoveP2 => IsEnraged ? _rageSpeed : 1f;
        protected override float WindupP2 => _rageWindup;

        public static event System.Action<OgreBrain> RockThrown; // 바위 던지기 (아트)
        public static event System.Action<OgreBrain> Enraged;    // 분노 (2페이즈, 아트)

        float _throwTimer = 2f, _rageSpeed = 1.3f, _rageWindup = 0.8f;

        public bool IsEnraged => Phase >= 2;

        protected override void ReadMoreTuning()
        {
            _rageSpeed = Tuning("rage_speed", 1.3f);
            _rageWindup = Tuning("rage_windup", 0.8f);
        }

        protected override void TickMore(float dt)
        {
            if (_throwTimer > 0f) _throwTimer -= dt;
        }

        protected override void TryMidRange()
        {
            if (_throwTimer > 0f || Attack.IsAttacking) return;
            _throwTimer = Tuning("throw_interval", 6f);
            RockThrown?.Invoke(this);
            DropRocks();
        }

        protected override void OnEnterPhase2() => Enraged?.Invoke(this);
    }
}
