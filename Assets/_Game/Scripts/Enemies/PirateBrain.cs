using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 수적 졸개 (장강 수채, 몬스터 시트 46행): 둘씩 무리로 나와요. 졸개처럼 다가와 전진 베기, 0.3초 뒤 같은 방향으로 한 번 더 베어요.
    // 첫 칼을 대시로 넘겨도 바로 멈춰 서면 둘째 칼에 맞아요.
    public sealed class PirateBrain : LungeSlashBrain
    {
        float _second, _elapsed;
        bool _secondDone;

        public override EnemyKind Kind => EnemyKind.Pirate;
        protected override string Key => "pirate";
        public override string AttackName => "두 번 베기";
        protected override float ActiveTime => LungeTime + _second + 0.05f;

        protected override void ReadMoreTuning() => _second = Tuning("second_slash", 0.3f);

        protected override void OnActiveStarted()
        {
            base.OnActiveStarted();
            _elapsed = 0f;
            _secondDone = false;
        }

        protected override void TickActive(float dt)
        {
            base.TickActive(dt);
            _elapsed += dt;
            if (_secondDone || _elapsed < _second) return;
            _secondDone = true;
            if (HitShapes.InSector(transform.position, AimDirection, AttackRange, SlashAngle, Player.transform.position, PlayerRadius)) TryHitPlayer();
        }
    }
}
