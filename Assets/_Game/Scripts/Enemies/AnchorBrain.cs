using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 쇠닻 거한 (장강 수채, 몬스터 시트 48행): 쇠닻을 사슬째 던져 꽂고, 꽂힌 자리로 자기 몸을 끌어당겨 날아와요 (강인함 3).
    //   닻 던지기: 3~8칸이면 예고 1초 (직선) → 닻이 벽 앞까지 날아가 꽂히고, 거한이 초당 14칸으로 끌려가요. 지나가는 길 (폭 1.6칸)에 있으면 맞아요 ×1.0.
    //   꽂힌 닻은 3초 동안 길을 막는 기둥으로 남아요 (플레이어·적 모두 못 지나감). 방을 옮기면 사라져요.
    //   쇠닻 휘두르기: 2.5칸 안이면 예고 0.8초 뒤 앞쪽 반원 (반경 2.2칸) ×0.8 + 넉백 1.5칸.
    public sealed class AnchorBrain : EnemyBrain
    {
        const float ProbeHeight = 0.6f;
        const float MaxStep = 0.25f;

        enum Pattern { None, Throw, Swing }

        float _throwMin, _throwMax, _speed, _width, _postTime, _swingRange, _swingRadius, _swingAngle, _swingRatio, _swingKnockback;
        float _planned, _traveled;
        bool _hitPlayer;
        Pattern _pattern;

        static readonly List<GameObject> Posts = new List<GameObject>();

        public override EnemyKind Kind => EnemyKind.Anchor;
        protected override string Key => "anchor";
        public override string AttackName => _pattern == Pattern.Swing ? "쇠닻 휘두르기" : "닻 던지기";
        protected override float ActiveTime => _pattern == Pattern.Throw ? float.PositiveInfinity : 0.1f; // 끌려가기가 끝나면 EndActive
        protected override bool SuperArmor => Attack.Current == EnemyAttackTimeline.Phase.Active;

        public static int PostCount { get { Posts.RemoveAll(p => p == null); return Posts.Count; } }

        protected override void ReadTuning()
        {
            _throwMin = Tuning("throw_min", 3f);
            _throwMax = Tuning("throw_max", 8f);
            _speed = Tuning("pull_speed", 14f);
            _width = Tuning("pull_width", 1.6f);
            _postTime = Tuning("post_time", 3f);
            _swingRange = Tuning("swing_range", 2.5f);
            _swingRadius = Tuning("swing_radius", 2.2f);
            _swingAngle = Tuning("swing_angle", 180f);
            _swingRatio = Tuning("swing_ratio", 0.8f);
            _swingKnockback = Tuning("swing_knockback", 1.5f);
        }

        protected override bool WantsToAttack(float distance)
        {
            if (distance <= _swingRange)
            {
                _pattern = Pattern.Swing;
                WindupTime = Tuning("swing_windup", 0.8f);
                return true;
            }
            if (distance < _throwMin || distance > _throwMax || !HasClearLine(distance, BodyRadius * 0.8f, ProbeHeight)) return false;
            _pattern = Pattern.Throw;
            WindupTime = Tuning("throw_windup", 1f);
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d > _swingRange * 0.8f) Move(dir * MoveSpeed, dt);
        }

        // 닻은 플레이어 너머 1칸까지 (벽 앞에서 멈춤)
        float PlannedLength()
        {
            float want = Mathf.Min(_throwMax, ToPlayer().magnitude + 1f);
            return ObstacleProbe.Distance(transform.position + Vector3.up * ProbeHeight, AimDirection, BodyRadius * 0.8f, want);
        }

        protected override List<Vector2> TelegraphShape()
        {
            if (_pattern == Pattern.Swing) return TelegraphShapes.Lunge(_swingRadius, _swingAngle, 0f);
            _planned = PlannedLength();
            return TelegraphShapes.Rect(_width, _planned + _width * 0.5f);
        }

        protected override void UpdateTelegraph()
        {
            if (_pattern == Pattern.Throw && Attack.IsTracking)
            {
                float length = PlannedLength();
                if (Mathf.Abs(length - _planned) > 0.05f)
                {
                    _planned = length;
                    Telegraph.SetShape(TelegraphShapes.Rect(_width, _planned + _width * 0.5f));
                }
            }
            base.UpdateTelegraph();
        }

        protected override void OnActiveStarted()
        {
            if (_pattern == Pattern.Swing)
            {
                Director.SpawnBurst(transform.position + AimDirection * (_swingRadius * 0.5f), _swingRadius * 0.6f);
                if (HitShapes.InSector(transform.position, AimDirection, _swingRadius, _swingAngle, Player.transform.position, PlayerRadius))
                    Player.TryReceiveHit(AttackPower * _swingRatio, transform.position, _swingKnockback, Body);
                return;
            }
            _planned = PlannedLength();
            _traveled = 0f;
            _hitPlayer = false;
            PlacePost(transform.position + AimDirection * (_planned + BodyRadius + 0.3f));
        }

        // 꽂힌 자리로 끌려가요 (벽에 막히면 거기서 멈춤)
        protected override void TickActive(float dt)
        {
            if (_pattern != Pattern.Throw) return;
            float total = _speed * dt;
            while (total > 0f)
            {
                float step = Mathf.Min(total, MaxStep, _planned - _traveled);
                if (step <= 0f) break;
                var flags = MoveRaw(AimDirection * step);
                _traveled += step;
                total -= step;
                CheckHit();
                if ((flags & CollisionFlags.Sides) != 0) { Attack.EndActive(); return; }
            }
            if (_traveled >= _planned - 1e-4f) Attack.EndActive();
        }

        void CheckHit()
        {
            if (_hitPlayer || ToPlayer().magnitude > _width * 0.5f + PlayerRadius) return;
            _hitPlayer = TryHitPlayer();
        }

        // 길을 막는 쇠닻 기둥
        void PlacePost(Vector3 at)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "쇠닻";
            post.transform.position = new Vector3(at.x, 0.6f, at.z);
            post.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
            var renderer = post.GetComponent<Renderer>();
            renderer.material.color = new Color(0.25f, 0.27f, 0.3f);
            Destroy(post, _postTime);
            Posts.Add(post);
        }

        public static void ClearPosts()
        {
            foreach (var p in Posts) if (p != null) Destroy(p);
            Posts.Clear();
        }
    }
}
