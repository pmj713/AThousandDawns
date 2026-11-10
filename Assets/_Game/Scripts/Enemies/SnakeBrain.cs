using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 청죽사: 풀숲에 숨어 있다가(보이지 않고 맞지도 않음) 2.5칸 안에 들어오면 3칸 뛰어들어 물고 둔화(이동 −25%, 2초).
    // 빈틈 1초 뒤 2초 동안 다른 풀숲으로 옮겨 가요 (이때가 잡을 기회). 방에 풀숲이 없으면 그냥 다가와서 물어요.
    public sealed class SnakeBrain : LeapBrain
    {
        float _ambushDistance, _slowRatio, _slowDuration, _relocateTime, _relocateSpeed, _relocateElapsed;
        AmbushBush _bush, _lastBush;
        bool _relocating;

        public override EnemyKind Kind => EnemyKind.Snake;
        protected override string Key => "snake";

        protected override void ReadMoreTuning()
        {
            _ambushDistance = Tuning("ambush_distance", 2.5f);
            _slowRatio = Tuning("slow_ratio", 0.25f);
            _slowDuration = Tuning("slow_duration", 2f);
            _relocateTime = Mathf.Max(0.1f, Tuning("relocate_time", 2f));
        }

        protected override void OnInitialized()
        {
            _bush = Director.ClaimBush(this, transform.position, null);
            if (_bush == null) return;
            Teleport(_bush.transform.position);
            SetHidden(true);
        }

        protected override bool WantsToAttack(float distance)
        {
            if (_relocating) return false;
            if (IsHidden) return distance <= _ambushDistance;
            return distance <= AttackRange * 0.9f;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsHidden) return; // 풀숲에서 기다려요

            if (_relocating && _bush != null)
            {
                // CharacterController를 껐다 켜면 플레이어·적과 안 부딪히게 한 설정이 풀려서, 순간이동 없이 걸어 들어가요.
                Vector3 to = _bush.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                _relocateElapsed += dt;
                if (d <= 0.2f)
                {
                    _relocating = false;
                    SetHidden(true);
                    Sfx.Play(SfxId.BushRustle, 0.6f); // 다른 풀숲으로 숨어들 때 바스락 (단서)
                    return;
                }
                if (_relocateElapsed > _relocateTime * 3f)
                {
                    // 대나무 등에 걸려 못 가면 포기하고 드러난 채 싸워요. 다음 공격 뒤에 다시 풀숲을 찾아요.
                    _relocating = false;
                    _bush.Release(this);
                    _bush = null;
                    return;
                }
                FaceToward(to);
                MoveRaw(to / d * Mathf.Min(_relocateSpeed * dt, d));
                return;
            }

            Vector3 toPlayer = ToPlayer();
            FaceToward(toPlayer);
            if (toPlayer.magnitude > AttackRange * 0.8f) Move(toPlayer.normalized * MoveSpeed, dt);
        }

        protected override void OnAttackStarted()
        {
            if (!IsHidden) return;
            SetHidden(false); // 뛰어나와요
            Sfx.Play(SfxId.SnakeStrike);
            if (_bush != null) _bush.Release(this);
            _lastBush = _bush;
            _bush = null;
        }

        protected override void OnBite() => Player.ApplySlow(_slowRatio, _slowDuration);

        protected override void OnAttackFinished()
        {
            var next = Director.ClaimBush(this, transform.position, _lastBush);
            if (next == null) return;
            _bush = next;
            _relocating = true;
            _relocateElapsed = 0f;
            float distance = Vector3.Distance(new Vector3(next.transform.position.x, 0f, next.transform.position.z), transform.position);
            _relocateSpeed = Mathf.Clamp(distance / _relocateTime, MoveSpeed * 0.5f, MoveSpeed * 2f);
        }

        protected override void OnDied()
        {
            if (_bush != null) _bush.Release(this);
        }
    }
}
