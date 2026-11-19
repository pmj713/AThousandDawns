using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 단검꾼: 파고들지 않고 4~6칸을 유지하다 (가까이 오면 물러나요) 단검 한 자루를 곧게 던져요 (플레이테스트 11차).
    //   예고는 날아갈 길의 가는 직사각형 (goblinrogue.windup초). 단검은 화살과 같은 투사체라 플레이어 몸에 닿을 때만 맞아요
    //   (Arrow, 쏜 적이 Owner). 피해 = 공격 × goblinrogue.dagger_ratio
    public sealed class GoblinRogueBrain : KiteBrain
    {
        float _daggerSpeed, _daggerRadius, _daggerRatio;

        public override EnemyKind Kind => EnemyKind.GoblinRogue;
        protected override string Key => "goblinrogue";
        public override string AttackName => "단검 던지기";
        protected override float LineRadius => _daggerRadius;

        // 단검을 던진 순간 (아트: 던지는 손짓 · 단검 모양): 누가, 어디서 (손 높이), 방향
        public static event System.Action<GoblinRogueBrain, Vector3, Vector3> DaggerThrown;

        protected override void ReadShotTuning()
        {
            _daggerSpeed = Tuning("dagger_speed", 13f);
            _daggerRadius = Tuning("dagger_radius", 0.2f);
            _daggerRatio = Tuning("dagger_ratio", 0.8f);
        }

        protected override void Fire(Vector3 origin, Vector3 direction)
        {
            Director.SpawnArrow(origin, direction, _daggerSpeed, AttackRange, _daggerRadius, AttackPower * _daggerRatio, Body);
            DaggerThrown?.Invoke(this, origin, direction);
        }
    }

}
