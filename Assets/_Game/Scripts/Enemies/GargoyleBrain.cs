using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 가고일 (차원 겹침, 판타지): 다가와 날개를 펼쳐 제 둘레 원 (반경 gargoyle.attack_range)을 내려찍고, 공격이 끝나면 gargoyle.stone_time초 동안 석화해요.
    //   석화 중엔 제자리에 굳어 받는 피해가 gargoyle.stone_ratio배 (움직일 때 · 내려찍을 때 노려요)
    public sealed class GargoyleBrain : LungeSlashBrain
    {
        public override EnemyKind Kind => EnemyKind.Gargoyle;
        protected override string Key => "gargoyle";
        public override string AttackName => "날개 내려찍기";
        protected override int StrikeShape => ShapeRing;
        protected override bool RadialTelegraph => true;

        public static event System.Action<GargoyleBrain, bool> StoneChanged; // 석화 시작 · 풀림 (아트)
        public bool IsStone => _stoneTimer > 0f;

        float _stoneTimer, _stoneRatio;

        protected override void ReadMoreTuning()
        {
            _stoneRatio = Tuning("stone_ratio", 0.3f);
            Lunge = 0f; // 제자리에서 내려찍어요
        }

        protected override void OnInitialized() => Body.SituationalResolver = (attacker, tag) => IsStone ? _stoneRatio : 1f;

        protected override void TickAlways(float dt)
        {
            if (_stoneTimer <= 0f) return;
            _stoneTimer -= dt;
            if (_stoneTimer <= 0f) StoneChanged?.Invoke(this, false);
        }

        protected override void OnAttackFinished()
        {
            _stoneTimer = Tuning("stone_time", 2f);
            StoneChanged?.Invoke(this, true);
        }

        protected override bool WantsToAttack(float distance) => !IsStone && base.WantsToAttack(distance);

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsStone) return; // 굳어 있어요
            base.TickMovement(dt, canAttack);
        }
    }
}
