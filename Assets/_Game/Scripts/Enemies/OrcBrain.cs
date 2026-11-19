using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 오크 (차원 겹침, 판타지): 땅 내려찍기 충격파. 예고 (orc.windup, 일직선 직사각형) 뒤 몽둥이로 땅을 내려찍으면
    //   발밑에서 충격파가 땅을 타고 일직선으로 뻗어 나가요 (길이 orc.slam_length · 폭 orc.slam_width · 초당 orc.slam_speed칸).
    //   충격파 끝이 지나가는 순간 그 자리의 플레이어가 한 번 맞고 조금 밀려요. 벽 · 엄폐물에서 멈춰요.
    //   멧돼지 돌진과 겹쳐서 돌진은 뺐어요 (플레이테스트 11차). 오크 자신은 그 자리에 서 있어요 (빈틈 orc.recovery).
    public sealed class OrcBrain : EnemyBrain
    {
        const float ProbeHeight = 0.6f;

        float _length, _width, _speed, _ratio, _knockback;
        float _plannedLength, _front, _waveLength;
        Vector3 _waveFrom, _waveDir;
        bool _hitPlayer;

        // 충격파 (아트): 누가, 시작 자리 (발밑), 방향, 길이 (벽에서 줄어든 길이), 폭, 초당 칸
        public static event System.Action<OrcBrain, Vector3, Vector3, float, float, float> Shockwave;

        public override EnemyKind Kind => EnemyKind.Orc;
        protected override string Key => "orc";
        public override string AttackName => "땅 내려찍기";
        protected override float ActiveTime => float.PositiveInfinity; // 충격파가 끝까지 가면 EndActive
        protected override float HitKnockback => _knockback;

        public bool IsSlamming => Attack.Current == EnemyAttackTimeline.Phase.Active;
        public float WaveFront => _front; // 충격파 끝이 발밑에서 얼마나 나갔나 (칸)

        protected override void ReadTuning()
        {
            _length = Tuning("slam_length", 6f);
            _width = Tuning("slam_width", 1f);
            _speed = Mathf.Max(0.1f, Tuning("slam_speed", 12f));
            _ratio = Tuning("slam_ratio", 1f);
            _knockback = Tuning("slam_knockback", 0.5f);
        }

        protected override bool WantsToAttack(float distance) =>
            distance <= AttackRange && HasClearLine(distance, _width * 0.4f, ProbeHeight);

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            // 준비됐으면 충격파가 닿는 거리 안쪽까지, 아니면 그 언저리에서 어슬렁거려요
            float keep = canAttack ? AttackRange * 0.8f : AttackRange;
            if (d > keep) Move(dir * MoveSpeed, dt);
            else if (!canAttack) Move(Strafe(dir) * (MoveSpeed * 0.3f), dt);
            else if (!HasClearLine(d, _width * 0.4f, ProbeHeight) && (Move(Reposition(dir) * MoveSpeed, dt) & CollisionFlags.Sides) != 0) FlipReposition(); // 엄폐물 너머로는 안 찍고 트인 자리로 (제 빠르기로: 느리면 큰 담을 도는 데 한참 걸려요)
        }

        protected override List<Vector2> TelegraphShape()
        {
            _plannedLength = PlannedLength();
            return TelegraphShapes.Rect(_width, _plannedLength);
        }

        // 따라오는 동안엔 벽까지 거리에 맞춰 예고 길이를 바꿔요
        protected override void UpdateTelegraph()
        {
            if (Attack.IsTracking)
            {
                float length = PlannedLength();
                if (Mathf.Abs(length - _plannedLength) > 0.05f)
                {
                    _plannedLength = length;
                    Telegraph.SetShape(TelegraphShapes.Rect(_width, _plannedLength));
                }
            }
            base.UpdateTelegraph();
        }

        float PlannedLength() =>
            Mathf.Max(0.5f, ObstacleProbe.Distance(transform.position + Vector3.up * ProbeHeight, AimDirection, _width * 0.4f, _length));

        protected override void OnActiveStarted()
        {
            _waveFrom = transform.position;
            _waveDir = AimDirection;
            _waveLength = PlannedLength();
            _front = 0f;
            _hitPlayer = false;
            Director.SpawnBurst(_waveFrom + _waveDir * 0.6f, 0.8f, BurstKind.Slam); // 몽둥이가 땅에 닿는 자리 (소리도 같이)
            Shockwave?.Invoke(this, _waveFrom, _waveDir, _waveLength, _width, _speed);
        }

        protected override void TickActive(float dt)
        {
            float before = _front;
            _front = Mathf.Min(_waveLength, _front + _speed * dt);
            CheckHit(before, _front);
            if (_front >= _waveLength - 1e-4f) Attack.EndActive();
        }

        // 충격파 끝이 이번 프레임에 지나간 구간 [before, front] 안에 플레이어가 있으면 한 번 맞아요
        void CheckHit(float before, float front)
        {
            if (_hitPlayer || Player == null || Player.IsDead) return;
            Vector3 p = Player.transform.position - _waveFrom;
            p.y = 0f;
            float along = Vector3.Dot(p, _waveDir);
            float side = (p - _waveDir * along).magnitude;
            if (side > _width * 0.5f + PlayerRadius) return;
            if (along < before - PlayerRadius || along > front + PlayerRadius) return;
            _hitPlayer = Player.TryReceiveHit(AttackPower * _ratio, _waveFrom, HitKnockback, Body);
        }
    }
}
