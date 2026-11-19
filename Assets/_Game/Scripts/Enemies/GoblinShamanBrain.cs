using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 주술사: 거리를 두고 주문 (예고 중 맞으면 끊김). 깃발수 지휘 (빨라짐)와 겹쳐서 광폭화는 뺐어요 (플레이테스트 11차).
    //   치유: 둘레 goblinshaman.heal_radius칸 안 다친 동료 (도울 수 있는 적, CanSupport)를 최대 체력의 goblinshaman.heal_ratio만큼 (자기 자신은 빼요)
    //   저주 장판: 플레이어 발밑에 예고 원이 먼저 차오르고 (goblinshaman.curse_delay초) 그 뒤 그 자리에 저주가 깔려요.
    //     안에 있으면 맞고 (goblinshaman.curse_ratio), 서 있는 동안 저주 (모든 피해 −elite.curse_attack, PlayerController.Curse)
    //     장판을 나와도 goblinshaman.curse_linger초 남아요
    public sealed class GoblinShamanBrain : HornBrain
    {
        float _healRadius, _healRatio, _curseRadius, _curseLife, _curseRatio, _curseDelay, _curseLinger;
        readonly List<(Vector3 at, float timer, Telegraph marker)> _curses = new List<(Vector3, float, Telegraph)>();

        public static event System.Action<GoblinShamanBrain, EnemyBrain> Healed;     // 치유한 주술사, 치유받은 적 (아트)
        public static event System.Action<GoblinShamanBrain, Vector3, float> CursePlaced; // 저주 장판이 깔린 자리 · 반경 (아트)

        public override EnemyKind Kind => EnemyKind.GoblinShaman;
        protected override string Key => "goblinshaman";
        public override string AttackName => "주술";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _healRadius = Tuning("heal_radius", 7f);
            _healRatio = Tuning("heal_ratio", 0.15f);
            _curseRadius = Tuning("curse_radius", 1.5f);
            _curseLife = Tuning("curse_life", 4f);
            _curseRatio = Tuning("curse_ratio", 0.5f);
            _curseDelay = Mathf.Max(0f, Tuning("curse_delay", 0.9f));
            _curseLinger = Tuning("curse_linger", 0.5f);
        }

        protected override bool WantsToAttack(float distance) => true; // 공격 간격마다 늘 주문 (다친 동료가 없어도 저주는 깔아요)

        bool NeedsHeal(EnemyBrain e) =>
            CanSupport(e) && e.Damageable.Health < e.Damageable.MaxHealth && (e.transform.position - transform.position).magnitude <= _healRadius;

        protected override void OnActiveStarted()
        {
            // 나팔수 소환은 하지 않아요 (base 안 불러요)
            foreach (var e in Director.Enemies)
            {
                if (!NeedsHeal(e)) continue;
                e.Damageable.Heal(e.Damageable.MaxHealth * _healRatio);
                Healed?.Invoke(this, e);
            }
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
                Director.SpawnBurst(at, _curseRadius * 0.6f); // 장판이 깔리는 순간 소리
                if (Player == null) continue;
                var fire = GhostFire.Create(at, _curseRadius, _curseLife, AttackPower * _curseRatio, 0f, 0f, Player, Body); // 느려짐 없이 피해만
                fire.gameObject.AddComponent<CursePool>().Init(fire, Player, _curseLinger);
                CursePlaced?.Invoke(this, at, _curseRadius);
            }
        }

        protected override void OnDied()
        {
            base.OnDied();
            foreach (var c in _curses) if (c.marker != null) Destroy(c.marker.gameObject); // 쓰러지면 아직 안 깔린 저주는 없어져요
            _curses.Clear();
        }

        // 장판에 붙어서, 플레이어가 안에 서 있는 동안 저주를 계속 걸어요 (주술사가 쓰러져도 장판이 남아 있는 동안은 그대로)
        sealed class CursePool : MonoBehaviour
        {
            GhostFire _fire;
            PlayerController _player;
            float _linger;

            public void Init(GhostFire fire, PlayerController player, float linger)
            {
                _fire = fire;
                _player = player;
                _linger = Mathf.Max(0.05f, linger);
            }

            void Update()
            {
                if (_fire == null || _player == null || _player.IsDead) return;
                if (_fire.Touches(_player.transform.position)) _player.Curse(_linger);
            }
        }
    }
}
