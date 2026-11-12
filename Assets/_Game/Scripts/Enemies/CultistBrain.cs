using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 마교도 (천마총, 몬스터 시트 52행 "체력 50% 이하 광폭"): 졸개처럼 다가와 곡도로 전진 베기.
    // 체력이 절반 아래로 떨어지면 광폭 (붉게 달아올라요): 이동 +50%, 예고 −40%, 공격 간격 절반. 한 번에 잡지 못하면 더 위험해져요.
    public sealed class CultistBrain : LungeSlashBrain
    {
        static readonly Color Rage = new Color(0.95f, 0.25f, 0.2f);

        float _threshold, _move, _windup, _interval, _baseMove, _baseWindup, _baseInterval;

        public override EnemyKind Kind => EnemyKind.Cultist;
        protected override string Key => "cultist";
        public override string AttackName => IsFrenzied ? "광폭 베기" : "베기";
        public bool IsFrenzied { get; private set; }

        protected override void ReadMoreTuning()
        {
            _threshold = Tuning("frenzy_hp", 0.5f);
            _move = Tuning("frenzy_move", 1.5f);
            _windup = Tuning("frenzy_windup", 0.6f);
            _interval = Tuning("frenzy_interval", 0.5f);
            _baseMove = MoveSpeed;
            _baseWindup = WindupTime;
            _baseInterval = AttackInterval;
        }

        protected override void OnHitTaken()
        {
            if (IsFrenzied || Body.Health > Body.MaxHealth * _threshold) return;
            IsFrenzied = true;
            MoveSpeed = _baseMove * _move;
            WindupTime = _baseWindup * _windup;
            AttackInterval = _baseInterval * _interval;
            Body.SetTint(Rage);
        }
    }
}
