using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 주술사: 거리를 두고 주문 (예고 중 맞으면 끊김).
    //   광폭화: 둘레 다른 적이 잠시 빨라져요 (깃발수 지휘와 같은 규칙) · 저주 장판: 플레이어 발밑에 예고 원이 먼저 차오르고 (goblinshaman.curse_delay초)
    //   그 뒤 그 자리에 저주가 깔려요 (안에 있으면 맞고 느려져요)
    public sealed class GoblinShamanBrain : FlagBearerBrain
    {
        float _curseRadius, _curseLife, _curseRatio, _curseSlow, _curseSlowTime, _curseDelay;
        readonly List<(Vector3 at, float timer, Telegraph marker)> _curses = new List<(Vector3, float, Telegraph)>();

        public override EnemyKind Kind => EnemyKind.GoblinShaman;
        protected override string Key => "goblinshaman";
        public override string AttackName => "주술";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _curseRadius = Tuning("curse_radius", 1.5f);
            _curseLife = Tuning("curse_life", 4f);
            _curseRatio = Tuning("curse_ratio", 0.5f);
            _curseSlow = Tuning("curse_slow", 0.3f);
            _curseSlowTime = Tuning("curse_slow_time", 1.5f);
            _curseDelay = Mathf.Max(0f, Tuning("curse_delay", 0.9f));
        }

        protected override bool WantsToAttack(float distance) => true; // 공격 간격마다 늘 주문 (도울 적이 없어도 저주는 깔아요)

        protected override void OnActiveStarted()
        {
            base.OnActiveStarted(); // 광폭화
            // 저주는 바로 깔지 않고 바닥 예고 원부터 보여줘요 (차오르면 그 자리에 장판)
            Vector3 at = Player.transform.position;
            var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            marker.Show(TelegraphShapes.Circle(_curseRadius), true);
            marker.SetPose(at, Vector3.forward);
            marker.SetProgress(0f);
            _curses.Add((at, _curseDelay, marker));
        }

        protected override void TickAlways(float dt)
        {
            base.TickAlways(dt);
            for (int i = _curses.Count - 1; i >= 0; i--)
            {
                var (at, timer, marker) = _curses[i];
                timer -= dt;
                if (marker != null) marker.SetProgress(1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, _curseDelay)));
                if (timer > 0f)
                {
                    _curses[i] = (at, timer, marker);
                    continue;
                }
                if (marker != null) Destroy(marker.gameObject);
                _curses.RemoveAt(i);
                if (Player != null) GhostFire.Create(at, _curseRadius, _curseLife, AttackPower * _curseRatio, _curseSlow, _curseSlowTime, Player, Body);
            }
        }

        protected override void OnDied()
        {
            base.OnDied();
            foreach (var c in _curses) if (c.marker != null) Destroy(c.marker.gameObject); // 쓰러지면 아직 안 깔린 저주는 없어져요
            _curses.Clear();
        }
    }
}
