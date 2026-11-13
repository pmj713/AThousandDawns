using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 수귀 (장강 수채, 몬스터 시트 50행 "물속 매복"): 물길 지형이 생기기 전까지는 땅속에 숨어요.
    //   숨은 동안: 보이지 않고 맞힐 수 없어요. 바닥에 작은 물결 원만 보이며 플레이어 쪽으로 초당 2.5칸 다가와요.
    //   1.6칸 안이면 0.5초 예고 (물결이 커져요) 뒤 튀어나와 붙잡아요 (반경 1.2칸 ×1.0).
    //   붙잡히면 1.5초 동안 수귀가 뒷걸음질 치며 플레이어를 끌고 가요 (초당 2.5칸, 그동안 플레이어 이동 −60%). 수귀를 때리거나 대시하면 풀려나요.
    //   튀어나온 뒤 3초 동안은 드러나 있어요 (잡을 기회). 그다음 다시 숨어요. 청죽사(매복 한 번 물기)와 달리 잡힌 뒤가 위험해요.
    public sealed class WaterGhostBrain : EnemyBrain
    {
        float _ambush, _dragTime, _dragSpeed, _dragSlow, _surfaceTime, _surfaceLeft, _dragLeft;

        public override EnemyKind Kind => EnemyKind.WaterGhost;
        protected override string Key => "waterghost";
        public override string AttackName => "물귀신 손";
        protected override float ActiveTime => 0.1f;
        protected override bool RadialTelegraph => true;

        public bool Submerged => IsHidden;
        public bool Dragging => _dragLeft > 0f;

        protected override void ReadTuning()
        {
            _ambush = Tuning("ambush_distance", 1.6f);
            _dragTime = Tuning("drag_time", 1.5f);
            _dragSpeed = Tuning("drag_speed", 2.5f);
            _dragSlow = Tuning("drag_slow", 0.6f);
            _surfaceTime = Tuning("surface_time", 3f);
        }

        protected override void OnInitialized()
        {
            SetHidden(true);
            ShowRipple();
        }

        // 숨어 다가올 땐 바닥 표시를 띄우지 않아요 (주황 예고 원이 물 위를 따라다녀 어색했어요. 물결 그림은 아트가 그려요)
        void ShowRipple() => Telegraph.Hide();

        protected override void TickAlways(float dt)
        {
            if (_dragLeft > 0f) TickDrag(dt);
            if (IsHidden || _surfaceLeft <= 0f) return;
            _surfaceLeft -= dt;
            if (_surfaceLeft > 0f || Attack.IsAttacking || Dragging) return;
            SetHidden(true); // 다시 숨어요
            ShowRipple();
        }

        // 뒷걸음질 치며 끌고 가요. 플레이어가 대시하면 (끌리지 않고) 풀려나요
        void TickDrag(float dt)
        {
            if (Player == null || Player.IsDead || Player.IsDashing)
            {
                _dragLeft = 0f;
                return;
            }
            _dragLeft -= dt;
            Vector3 back = -Facing * (_dragSpeed * dt);
            var flags = MoveRaw(back);
            if ((flags & CollisionFlags.Sides) == 0) Player.Drag(back);
        }

        protected override void OnHitTaken() => _dragLeft = 0f; // 맞으면 놓아요

        protected override bool WantsToAttack(float distance) => IsHidden && distance <= _ambush;

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (Dragging) return;
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (IsHidden)
            {
                if (d > _ambush * 0.6f) Move(dir * MoveSpeed, dt);
                return;
            }
            Move(-dir * (MoveSpeed * 0.4f), dt); // 드러난 동안은 슬금슬금 물러나요
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(AttackRange);

        protected override void OnActiveStarted()
        {
            SetHidden(false);
            _surfaceLeft = _surfaceTime;
            if (ToPlayer().magnitude > AttackRange + PlayerRadius) return;
            if (!TryHitPlayer()) return;
            _dragLeft = _dragTime; // 붙잡아 끌고 가요 (그동안 크게 느려져서 버티기 어려워요)
            Player.ApplySlow(_dragSlow, _dragTime);
        }

        protected override void TickActive(float dt) { }
    }
}
