using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 등패수 (장강 수채, 몬스터 시트 49행): 방패 산적처럼 정면 120°를 막고 다가와 베요 (넉백 2칸).
    //   등나무 방패는 멀리서 날아온 공격 (3칸 밖: 비도·강기 등)을 정면에서 완전히 막아요 (최소 피해 1).
    //   방패 굴리기: 3~7칸이면 7초마다 예고 0.7초 뒤 방패를 굴려 던져요 (7칸 갔다가 돌아옴, 가는 길·오는 길 한 번씩 ×0.8).
    //   방패가 날아가 있는 동안은 정면도 열려요 (그때가 기회). 대시 공격에 방패가 깨지는 건 같아요.
    public sealed class RattanBrain : ShieldBrain
    {
        float _ranged, _throwMin, _throwMax, _throwRange, _throwSpeed, _throwRatio, _throwInterval, _throwTimer, _awayLeft;
        bool _throwing;

        public override EnemyKind Kind => EnemyKind.Rattan;
        protected override string Key => "rattan";
        public override string AttackName => _throwing ? "방패 굴리기" : null;
        protected override float ActiveTime => _throwing ? 0.1f : LungeTime;
        protected override bool ShieldAway => _awayLeft > 0f;

        public bool ShieldThrown => ShieldAway;

        protected override void ReadMoreTuning()
        {
            base.ReadMoreTuning();
            _ranged = Tuning("ranged_distance", 3f);
            _throwMin = Tuning("throw_min", 3f);
            _throwMax = Tuning("throw_max", 7f);
            _throwRange = Tuning("throw_range", 7f);
            _throwSpeed = Tuning("throw_speed", 9f);
            _throwRatio = Tuning("throw_ratio", 0.8f);
            _throwInterval = Tuning("throw_interval", 7f);
            _throwTimer = _throwInterval * 0.4f;
        }

        protected override float BlockedRatio(Vector3 attacker, float guardRatio)
        {
            Vector3 to = attacker - transform.position;
            to.y = 0f;
            return to.magnitude >= _ranged ? 0f : guardRatio;
        }

        protected override void TickAlways(float dt)
        {
            if (_throwTimer > 0f) _throwTimer -= dt;
            if (_awayLeft > 0f) _awayLeft -= dt;
        }

        protected override bool WantsToAttack(float distance)
        {
            _throwing = false;
            if (!ShieldAway && !ShieldBroken && _throwTimer <= 0f && distance >= _throwMin && distance <= _throwMax)
            {
                _throwing = true;
                WindupTime = Tuning("throw_windup", 0.7f);
                return true;
            }
            WindupTime = Tuning("windup", 0.6f);
            return !ShieldAway && base.WantsToAttack(distance); // 방패가 없으면 베러 오지 않고 기다려요
        }

        protected override List<Vector2> TelegraphShape() => _throwing ? TelegraphShapes.Rect(0.9f, _throwRange) : base.TelegraphShape();

        protected override void OnActiveStarted()
        {
            if (!_throwing)
            {
                base.OnActiveStarted();
                return;
            }
            _throwTimer = _throwInterval;
            _awayLeft = _throwRange * 2f / Mathf.Max(0.1f, _throwSpeed) + 0.2f; // 갔다가 돌아올 때까지
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "등패";
            EnemyFactory.StripCollider(go);
            go.transform.position = transform.position + Vector3.up * 0.6f;
            go.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
            go.GetComponent<Renderer>().material.color = new Color(0.65f, 0.55f, 0.35f);
            go.AddComponent<BeadThrow>().Launch(Player, transform, AimDirection, _throwSpeed, _throwRange, 0.45f, AttackPower * _throwRatio, Body);
        }

        protected override void TickActive(float dt)
        {
            if (!_throwing) base.TickActive(dt);
        }

        protected override void OnAttackFinished() => _throwing = false;
    }
}
