using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 임프 (차원 겹침, 판타지): 날아다니며 거리를 두고 느린 화염구를 던져요 (플레이테스트 11차).
    //   화염구는 imp.fireball_speed로 곧게 날아가다 플레이어가 imp.fireball_trigger칸 안에 들어오면 (또는 벽에 막히거나 사거리 끝에서) 터져요.
    //   폭발 반경 imp.fireball_blast 안의 플레이어만 맞고 (공격 × fireball_ratio) 화상 imp.fireball_burn_stacks중첩 (정예 열화와 같은 player_burn.damage).
    public sealed class ImpBrain : KiteBrain
    {
        float _ballSpeed, _ballRadius, _ballTrigger, _ballBlast, _ballRatio;
        int _ballBurn;

        public override EnemyKind Kind => EnemyKind.Imp;
        protected override string Key => "imp";
        public override string AttackName => "화염구";
        protected override float LineRadius => _ballRadius;
        protected override float TelegraphHalfWidth => _ballTrigger; // 지나가는 길에서 이만큼 안이면 터져요

        // 던진 순간 (아트: 불덩이 · 불꼬리를 공에 붙여요): 누가, 화염구, 어디서 (손 높이), 방향
        public static event System.Action<ImpBrain, ImpFireball, Vector3, Vector3> FireballLaunched;
        // 터진 순간 (아트: 불 폭발): 화염구, 자리, 반경, 플레이어가 맞았나. 던진 임프가 쓰러졌어도 와요 (ImpFireball.Owner)
        public static event System.Action<ImpFireball, Vector3, float, bool> FireballExploded;
        internal static void RaiseExploded(ImpFireball ball, Vector3 at, float radius, bool hit) => FireballExploded?.Invoke(ball, at, radius, hit);

        protected override void ReadShotTuning()
        {
            _ballSpeed = Tuning("fireball_speed", 6f);
            _ballRadius = Tuning("fireball_radius", 0.3f);
            _ballTrigger = Tuning("fireball_trigger", 1.2f);
            _ballBlast = Tuning("fireball_blast", 1.2f);
            _ballRatio = Tuning("fireball_ratio", 0.8f);
            _ballBurn = Mathf.Max(0, Mathf.RoundToInt(Tuning("fireball_burn_stacks", 1f)));
        }

        protected override void Fire(Vector3 origin, Vector3 direction)
        {
            var ball = ImpFireball.Launch(Director, origin, direction, _ballSpeed, AttackRange, _ballRadius, _ballTrigger, _ballBlast, AttackPower * _ballRatio, _ballBurn, Body);
            FireballLaunched?.Invoke(this, ball, origin, direction);
        }
    }
}
