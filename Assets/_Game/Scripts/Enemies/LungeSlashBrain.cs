using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 전진 베기 계열 (졸개, 방패 산적): 다가와서 부채꼴(반경 attack_range, 각도 attack_angle)을 앞으로 lunge만큼 밀며 베요.
    // 실제 사거리 = attack_range + lunge. 공격 토큰이 없거나 공격 간격이 남았으면 사거리 바깥 고리를 옆으로 돌아요.
    // 모양 (StrikeShape)을 바꾸면 직선 (길이 attack_range · 폭 attack_width, 전진 포함)이나 원 (반경 attack_range)으로 쳐요.
    public abstract class LungeSlashBrain : EnemyBrain
    {
        public const int ShapeSector = 0, ShapeLine = 1, ShapeRing = 2; // RegularStrike 신호의 모양 값

        protected float SlashAngle, SlashWidth, Lunge, LungeTime;
        float _lungeRemaining, _elapsed, _secondAt;
        bool _hitPlayer, _struck, _secondStruck;
        Vector3 _origin;

        protected override float ActiveTime => LungeTime;

        protected override void ReadTuning()
        {
            if (StrikeShape == ShapeSector) SlashAngle = Tuning("attack_angle", 60f);
            if (StrikeShape == ShapeLine) SlashWidth = Tuning("attack_width", 0.9f);
            Lunge = Tuning("lunge", 1.5f);
            LungeTime = Mathf.Max(0.01f, Tuning("lunge_time", 0.15f));
            _secondAt = GameConfig.GetOptional($"{Key}.second_slash", -1f); // 둘째 칼 (수적 졸개): 그때 신호를 한 번 더 보내요
            ReadMoreTuning();
        }

        protected virtual void ReadMoreTuning() { }

        // 다가오는 속도 (방패 산적은 방패를 든 동안 느려요)
        protected virtual float ApproachSpeed => MoveSpeed;
        protected virtual bool CircleWhileWaiting => true; // 공격 간격 동안 사거리 바깥을 옆으로 돌아요 (작은 고블린은 꺼요)

        // 치는 모양: 부채꼴 (기본) · 직선 · 원
        protected virtual int StrikeShape => ShapeSector;
        // 판정이 나기 시작하는 때 (활성 시작부터 초). 0이면 바로 (고블린은 뛰어서 내려앉은 뒤)
        protected virtual float StrikeDelay => 0f;

        // 예고를 시작하는 거리. 부채꼴은 예고 앞 절반에 조금 더 다가오니까, 실제 사거리보다 약간 안쪽에서 시작해요.
        // 직선 · 원은 attack_range가 이미 끝까지 닿는 거리예요.
        protected virtual float EngageDistance => StrikeShape == ShapeSector ? Lunge + AttackRange * 0.8f : AttackRange * 0.8f;

        protected override bool WantsToAttack(float distance) => distance <= EngageDistance;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            TurnToward(dir, dt);

            if (canAttack)
            {
                Move(dir * ApproachSpeed, dt);
                return;
            }

            float ring = EngageDistance + 1f;
            Vector3 velocity = CircleWhileWaiting ? Strafe(dir) * (ApproachSpeed * 0.5f) : Vector3.zero;
            if (d > ring + 0.5f) velocity = dir * ApproachSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (ApproachSpeed * 0.6f) + velocity * 0.5f;
            Move(velocity, dt);
        }

        protected override List<Vector2> TelegraphShape() => StrikeShape switch
        {
            ShapeLine => TelegraphShapes.Rect(SlashWidth, AttackRange),
            ShapeRing => TelegraphShapes.Circle(AttackRange),
            _ => TelegraphShapes.Lunge(AttackRange, SlashAngle, Lunge),
        };

        protected override void OnActiveStarted()
        {
            _lungeRemaining = Lunge;
            _hitPlayer = _struck = _secondStruck = false;
            _elapsed = 0f;
            _origin = transform.position; // 직선은 전진을 시작한 자리부터 (길이에 전진이 들어 있어요)
            CheckHit();
        }

        protected override void TickActive(float dt)
        {
            float step = Mathf.Min(_lungeRemaining, Lunge / LungeTime * dt);
            // 플레이어 몸에 닿으면 더 밀고 들어가지 않아요
            Vector3 to = ToPlayer();
            if (Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f) step = 0f;
            if (step > 0f)
            {
                MoveRaw(AimDirection * step);
                _lungeRemaining -= step;
            }
            _elapsed += dt;
            CheckHit();
            if (_secondAt >= 0f && !_secondStruck && _elapsed >= _secondAt)
            {
                _secondStruck = true;
                StrikeSignal();
            }
        }

        void CheckHit()
        {
            if (_elapsed < StrikeDelay) return;
            if (!_struck)
            {
                _struck = true;
                StrikeSignal();
            }
            if (_hitPlayer || !StrikeHits()) return;
            _hitPlayer = TryHitPlayer();
        }

        // 진짜 플레이어가 이번 모양 안에 있나요 (몸 반경까지)
        bool StrikeHits()
        {
            Vector3 p = Player.transform.position;
            switch (StrikeShape)
            {
                case ShapeLine: return HitShapes.InRect(_origin, AimDirection, AttackRange, SlashWidth, p, PlayerRadius);
                case ShapeRing:
                    Vector3 d = p - transform.position;
                    d.y = 0f;
                    return d.magnitude <= AttackRange + PlayerRadius;
                default: return HitShapes.InSector(transform.position, AimDirection, AttackRange, SlashAngle, p, PlayerRadius);
            }
        }

        // 아트 신호: 부채꼴은 지금 자리에서 전진까지 닿는 반경, 직선은 시작한 자리부터, 원은 지금 자리 (내려앉은 곳)
        void StrikeSignal()
        {
            switch (StrikeShape)
            {
                case ShapeLine: SignalRegularStrike(_origin, AttackRange, SlashWidth, ShapeLine); break;
                case ShapeRing: SignalRegularStrike(transform.position, AttackRange, 360f, ShapeRing); break;
                default: SignalRegularStrike(transform.position, AttackRange + _lungeRemaining, SlashAngle, ShapeSector); break;
            }
        }
    }
}
