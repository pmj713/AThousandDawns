using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 석상 나한 (폐사찰, 몬스터 시트 42행): 방에 석상처럼 서 있다가 (맞힐 수 없고 몸이 잿빛) 3칸 안으로 다가오면 깨어나요.
    // 깨어나면 멧돼지처럼 예고 뒤 직선 돌진. 느리고 짧지만 맞으면 기절 (0.8초). 돌진이 끝난 자리에 반경 1.8칸 충격파 ×0.6.
    // 벽에 부딪히면 멧돼지처럼 멈춰 서요 (그 자리에도 충격파). 돌진 중엔 경직이 없어요.
    public sealed class ArhatBrain : BoarBrain
    {
        static readonly Color Stone = new Color(0.35f, 0.35f, 0.33f);

        float _stun, _wake, _quakeRadius, _quakeRatio;

        public override EnemyKind Kind => EnemyKind.Arhat;
        protected override string Key => "arhat";
        public bool IsDormant => !IsAwake;

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _stun = Tuning("stun", 0.8f);
            _wake = Tuning("statue_wake", 3f);
            _quakeRadius = Tuning("quake_radius", 1.8f);
            _quakeRatio = Tuning("quake_ratio", 0.6f);
        }

        // 석상: 맞힐 수 없어요 (장식처럼 보여요)
        protected override void OnInitialized()
        {
            Body.Targetable = false;
            Body.SetTint(Stone);
        }

        protected override bool ShouldWake() => ToPlayer().magnitude <= _wake;

        protected override void OnWoke()
        {
            Body.Targetable = true;
            Body.SetTint(null);
        }

        protected override void OnChargeHit() => Player.Stagger(_stun);

        protected override void OnChargeEnded()
        {
            Director.SpawnBurst(transform.position, _quakeRadius);
            if (ToPlayer().magnitude <= _quakeRadius + PlayerRadius)
                Player.TryReceiveHit(AttackPower * _quakeRatio, transform.position, -1f, Body);
        }
    }
}
