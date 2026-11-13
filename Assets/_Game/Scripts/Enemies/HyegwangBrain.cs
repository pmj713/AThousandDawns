using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 파계승 혜광 (폐사찰 고수, 몬스터 시트 63행): 체력 1100 · 공격 18 (폐사찰 배율 전). 결투장은 사방 돌벽.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 선장 휘두르기: 가까울 때. 예고 0.5, 2칸 내디디며 앞 부채꼴 반경 3칸 150°, ×1.0, 빈틈 0.8
    // 금강불괴: 9초마다 (2페이즈 6초). 1.8초 (2페이즈 1초) 동안 서서 버티며 맞아도 1씩만 (원형 예고가 차올라요) → 끝에 반경 3.5칸 충격파 ×1.2 + 넉백 2칸
    // 염주 던지기: 3칸 밖이면. 예고 0.6, 염주가 8칸 날아갔다가 돌아와요 (가는 길·오는 길 한 번씩 맞음) ×0.7, 초속 14칸. 세 갈래 (2페이즈는 다섯 갈래)
    // 축지: 5칸 밖에 2초 넘게 있으면. 예고 0.4, 플레이어 1.5칸 앞까지 순식간에 다가가 (최대 10칸) 그대로 선장 휘두르기 판정 (원거리로만 싸우지 못하게)
    // 2페이즈: 휘두르기 예고 20% 빨라짐 · 염주 예고 40% 빨라짐 · 금강불괴 강화 (버티는 동안 맞을 때마다 충격파 반경 +0.4칸, 최대 +2칸)
    public sealed class HyegwangBrain : EnemyBrain, IMasterBrain
    {
        static readonly Color Gold = new Color(0.95f, 0.78f, 0.3f);

        public override string AttackName => _pattern switch { Pattern.Sweep => "선장 휘두르기", Pattern.Vajra => "금강불괴", Pattern.Beads => "염주", Pattern.Rush => "축지", _ => null };

        enum Pattern { None, Sweep, Vajra, Beads, Rush }

        // 이펙트 신호 (아트: Scripts/Art/InkHyegwangFx). 판정과 상관없이 그림만 그려요.
        public enum FxKind { VajraStart, VajraGrow, VajraBurst, Rush }
        public struct FxCue { public FxKind Kind; public Vector3 At, Dir; public float Radius, Time, Length, Grow; }
        public static event System.Action<HyegwangBrain, FxCue> Cue;
        void Fx(FxCue cue) => Cue?.Invoke(this, cue);

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio, _phase2Speed;
        float _sweepWindup, _sweepRecovery, _sweepRatio, _sweepRadius, _sweepAngle, _sweepLunge, _sweepLungeTime, _lungeLeft;
        bool _sweepHitDone;
        float _vajraTime, _vajraTime2, _vajraRecovery, _vajraRatio, _vajraRadius, _vajraKnockback, _vajraInterval, _vajraInterval2;
        float _vajraGrow, _vajraGrowMax, _vajraBonus;
        float _beadWindup, _beadRecovery, _beadRatio, _beadRange, _beadSpeed, _beadSpread, _beadWindup2;
        float _rushDelay, _rushWindup, _rushTime, _rushStop, _rushMax, _farTimer, _lungeLength, _lungeTime;
        int _beadCount, _beadCount2;
        float _decisionTimer, _transitionTimer, _vajraTimer;

        public override EnemyKind Kind => EnemyKind.Hyegwang;
        protected override string Key => "hyegwang";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override bool RadialTelegraph => _pattern == Pattern.Vajra;
        protected override float ActiveTime => _pattern == Pattern.Sweep ? _sweepLungeTime + 0.05f : _pattern == Pattern.Rush ? _rushTime + 0.05f : 0.1f;

        // 지금 금강불괴 충격파 반경 (2페이즈는 버티는 동안 맞을수록 커져요)
        public float VajraRadius => _vajraRadius + _vajraBonus;

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool IsVajra => _pattern == Pattern.Vajra && Attack.Current == EnemyAttackTimeline.Phase.Windup;

        public void SetZone(Vector3 center, Vector2 size) { } // 염주는 벽을 지나가요

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 3f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _phase2Speed = Tuning("p2_windup_ratio", 0.8f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _sweepWindup = Tuning("sweep_windup", 0.5f);
            _sweepLunge = Tuning("sweep_lunge", 2f);
            _sweepLungeTime = Mathf.Max(0.05f, Tuning("sweep_lunge_time", 0.15f));
            _sweepRecovery = Tuning("sweep_recovery", 0.8f);
            _sweepRatio = Tuning("sweep_ratio", 1f);
            _sweepRadius = Tuning("sweep_radius", 3f);
            _sweepAngle = Tuning("sweep_angle", 150f);
            _vajraTime = Tuning("vajra_time", 1.8f);
            _vajraTime2 = Tuning("p2_vajra_time", 1f);
            _vajraGrow = Tuning("p2_vajra_grow", 0.4f);
            _vajraGrowMax = Tuning("p2_vajra_grow_max", 2f);
            _vajraRecovery = Tuning("vajra_recovery", 1f);
            _vajraRatio = Tuning("vajra_ratio", 1.2f);
            _vajraRadius = Tuning("vajra_radius", 3.5f);
            _vajraKnockback = Tuning("vajra_knockback", 2f);
            _vajraInterval = Tuning("vajra_interval", 9f);
            _vajraInterval2 = Tuning("p2_vajra_interval", 6f);
            _beadWindup = Tuning("bead_windup", 0.6f);
            _beadRecovery = Tuning("bead_recovery", 0.6f);
            _beadRatio = Tuning("bead_ratio", 0.7f);
            _beadRange = Tuning("bead_range", 8f);
            _beadSpeed = Tuning("bead_speed", 14f);
            _beadWindup2 = Tuning("p2_bead_windup_ratio", 0.6f);
            _beadCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("bead_count", 3f)));
            _beadCount2 = Mathf.Max(1, Mathf.RoundToInt(Tuning("p2_bead_count", 5f)));
            _rushDelay = Tuning("rush_delay", 2f);
            _rushWindup = Tuning("rush_windup", 0.4f);
            _rushTime = Mathf.Max(0.05f, Tuning("rush_time", 0.2f));
            _rushStop = Tuning("rush_stop", 1.5f);
            _rushMax = Tuning("rush_max", 10f);
            _beadSpread = Tuning("p2_bead_spread", 25f);
            _vajraTimer = _vajraInterval * 0.5f;
        }

        protected override void OnInitialized()
        {
            Body.SituationalResolver = (attacker, tag) => IsVajra ? 0f : 1f; // 금강불괴: 0이어도 최소 1은 들어가요
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_vajraTimer > 0f) _vajraTimer -= dt;
            _farTimer = ToPlayer().magnitude > _farDistance ? _farTimer + dt : 0f; // 멀리서만 싸우면 축지
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_farTimer >= _rushDelay) next = Pattern.Rush;
            else if (_vajraTimer <= 0f && distance <= _vajraRadius + 1f) next = Pattern.Vajra;
            else if (distance <= _closeDistance) next = Pattern.Sweep;
            else next = Pattern.Beads; // 3칸 밖이면 염주 (두 번 던지면 다가가요)
            if (next == _lastPattern && _repeat >= _maxRepeat && next != Pattern.Rush)
            {
                if (next == Pattern.Beads) return false;
                next = next == Pattern.Sweep ? Pattern.Beads : Pattern.Sweep;
            }

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float speed = Phase == 2 ? _phase2Speed : 1f;
            switch (next)
            {
                case Pattern.Sweep: WindupTime = _sweepWindup * speed; RecoveryTime = _sweepRecovery; break;
                case Pattern.Vajra:
                    WindupTime = Phase == 2 ? _vajraTime2 : _vajraTime; RecoveryTime = _vajraRecovery;
                    _vajraBonus = 0f;
                    _vajraTimer = Phase == 1 ? _vajraInterval : _vajraInterval2;
                    Body.SetTint(Gold);
                    Fx(new FxCue { Kind = FxKind.VajraStart, At = transform.position, Radius = _vajraRadius, Time = WindupTime });
                    break;
                case Pattern.Beads: WindupTime = _beadWindup * (Phase == 2 ? _beadWindup2 : 1f); RecoveryTime = _beadRecovery; break;
                case Pattern.Rush:
                    WindupTime = _rushWindup * speed; RecoveryTime = _sweepRecovery;
                    _farTimer = 0f;
                    break;
            }
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _closeDistance * 0.8f) Move(to.normalized * MoveSpeed, dt);
        }

        // 금강불괴는 버티고 서 있어요
        protected override void TickWindup(float dt, bool tracking)
        {
            if (_pattern == Pattern.Vajra)
            {
                FaceToward(ToPlayer());
                return;
            }
            base.TickWindup(dt, tracking);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Sweep => TelegraphShapes.Lunge(_sweepRadius, _sweepAngle, _sweepLunge),
            Pattern.Vajra => TelegraphShapes.Circle(_vajraRadius),
            Pattern.Beads => TelegraphShapes.Rect(0.6f, _beadRange),
            Pattern.Rush => TelegraphShapes.Rect(1.4f, RushLength() + _sweepRadius),
            _ => TelegraphShapes.Circle(0.5f),
        };

        // ── 판정

        protected override void OnActiveStarted()
        {
            switch (_pattern)
            {
                case Pattern.Sweep: // 내디디며 휘둘러요 (판정은 다 내디딘 뒤)
                    StartLunge(_sweepLunge, _sweepLungeTime);
                    break;
                case Pattern.Rush: // 축지: 순식간에 다가가 휘둘러요
                    Fx(new FxCue { Kind = FxKind.Rush, At = transform.position, Dir = AimDirection, Length = RushLength(), Time = _rushTime });
                    StartLunge(RushLength(), _rushTime);
                    break;
                case Pattern.Vajra:
                    Body.SetTint(null);
                    Director.SpawnBurst(transform.position, VajraRadius, AThousandDawns.Combat.BurstKind.Vajra);
                    Fx(new FxCue { Kind = FxKind.VajraBurst, At = transform.position, Radius = VajraRadius, Grow = _vajraGrowMax > 0f ? _vajraBonus / _vajraGrowMax : 0f });
                    if (ToPlayer().magnitude <= VajraRadius + PlayerRadius) HitPlayer(_vajraRatio, _vajraKnockback);
                    _vajraBonus = 0f;
                    break;
                case Pattern.Beads:
                    int count = Phase == 1 ? _beadCount : _beadCount2;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, (i - (count - 1) * 0.5f) * _beadSpread, 0f) * AimDirection;
                        ThrowBead(dir);
                    }
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            if ((_pattern != Pattern.Sweep && _pattern != Pattern.Rush) || _sweepHitDone) return;
            if (_lungeLeft > 0f)
            {
                float step = Mathf.Min(_lungeLeft, _lungeLength / _lungeTime * dt);
                Vector3 to = ToPlayer();
                if (!(Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f)) MoveRaw(AimDirection * step);
                _lungeLeft -= step;
            }
            if (_lungeLeft > 0f) return;
            _sweepHitDone = true;
            if (HitShapes.InSector(transform.position, AimDirection, _sweepRadius, _sweepAngle, Player.transform.position, PlayerRadius))
                HitPlayer(_sweepRatio, HitKnockback);
        }

        void StartLunge(float length, float time)
        {
            _lungeLength = _lungeLeft = Mathf.Max(0f, length);
            _lungeTime = time;
            _sweepHitDone = false;
        }

        // 축지 거리: 플레이어 1.5칸 앞까지 (최대 10칸, 벽 앞에서 멈춤)
        float RushLength()
        {
            float along = Mathf.Max(0f, Vector3.Dot(ToPlayer(), AimDirection) - _rushStop);
            float free = ObstacleProbe.Distance(transform.position + Vector3.up * 0.8f, AimDirection, BodyRadius, _rushMax + BodyRadius) - BodyRadius;
            return Mathf.Clamp(Mathf.Min(along, free), 0f, _rushMax);
        }

        void ThrowBead(Vector3 direction)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "염주";
            EnemyFactory.StripCollider(go);
            go.transform.position = transform.position + Vector3.up;
            go.transform.localScale = Vector3.one * 0.5f;
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(0.45f, 0.25f, 0.15f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<BeadThrow>().Launch(Player, transform, direction, _beadSpeed, _beadRange, 0.35f, AttackPower * _beadRatio, Body);
        }

        bool HitPlayer(float ratio, float knockback) => Player.TryReceiveHit(AttackPower * ratio, transform.position, knockback, Body);

        protected override void OnAttackFinished() => _decisionTimer = _decisionPause;

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            Body.SetTint(null);
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        // ── 페이즈

        protected override void OnHitTaken()
        {
            // 금강불괴 강화 (2페이즈): 버티는 동안 맞을 때마다 충격파가 커져요 (예고 원도 같이)
            if (Phase == 2 && IsVajra && _vajraBonus < _vajraGrowMax)
            {
                _vajraBonus = Mathf.Min(_vajraGrowMax, _vajraBonus + _vajraGrow);
                Telegraph.SetShape(TelegraphShapes.Circle(VajraRadius));
                Fx(new FxCue { Kind = FxKind.VajraGrow, At = transform.position, Radius = VajraRadius, Grow = _vajraGrowMax > 0f ? _vajraBonus / _vajraGrowMax : 0f });
            }
            if (Phase == 1 && !IsTransitioning && Body.Health <= Body.MaxHealth * _phase2Ratio) StartTransition();
        }

        void StartTransition()
        {
            if (Attack.IsAttacking) CancelAttack();
            Body.SetTint(null);
            _pattern = Pattern.None;
            _transitionTimer = _transitionTime;
            Body.Targetable = false;
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
        }
    }

    // 혜광의 염주: 곧게 range만큼 날아갔다가 던진 사람에게 돌아와요. 가는 길·오는 길에 한 번씩 맞을 수 있어요. 벽은 지나가요.
    public sealed class BeadThrow : MonoBehaviour
    {
        PlayerController _target;
        Transform _owner;
        Damageable _ownerBody;
        Vector3 _direction;
        float _speed, _left, _radius, _attack;
        bool _returning, _hitOut, _hitBack;

        public void Launch(PlayerController target, Transform owner, Vector3 direction, float speed, float range, float radius, float attack, Damageable ownerBody)
        {
            _target = target;
            _owner = owner;
            _ownerBody = ownerBody;
            _direction = new Vector3(direction.x, 0f, direction.z).normalized;
            _speed = speed;
            _left = range;
            _radius = radius;
            _attack = attack;
        }

        void Update()
        {
            if (_target == null || _owner == null)
            {
                Destroy(gameObject);
                return;
            }
            float step = _speed * Time.deltaTime;
            if (!_returning)
            {
                transform.position += _direction * step;
                _left -= step;
                if (_left <= 0f) _returning = true;
            }
            else
            {
                Vector3 home = _owner.position + Vector3.up - transform.position;
                if (home.magnitude <= step + 0.3f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position += home.normalized * step;
            }

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > _radius + _target.BodyRadius) return;
            if (_returning ? _hitBack : _hitOut) return;
            _target.TryReceiveHit(_attack, transform.position, -1f, _ownerBody); // 대시 무적이면 그냥 지나가요 (그 길은 끝)
            if (_returning) _hitBack = true;
            else _hitOut = true;
        }
    }
}
