using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 판타지 전용 정예 특성 (차원 겹침): 판타지 적에게만 붙어요 (차원 감염이 켜져 있을 때, InfectionAllows).
    //   마법 방벽: 최대 체력 elite.barrier_ratio만큼 가호를 둘러요. 다 깎이면 elite.barrier_interval초 뒤 다시 둘러요
    //   저주: 이 정예에게 맞으면 플레이어가 elite.curse_time초 동안 모든 피해 −elite.curse_attack (PlayerController.Curse)
    //   분노: elite.frenzy_radius칸 안에서 다른 적이 쓰러지면 쌓여요 (최대 elite.frenzy_max). 한 겹마다 공격 +elite.frenzy_attack · 이동 +elite.frenzy_speed
    public abstract partial class EnemyBrain
    {
        public static event System.Action<EnemyBrain> BarrierRaised;        // 마법 방벽을 둘렀어요 (아트)
        public static event System.Action<EnemyBrain, int> Frenzied;        // 분노가 쌓였어요 (아트), 지금 겹 수

        public static bool IsFantasyTrait(EliteTrait trait) => trait == EliteTrait.Barrier || trait == EliteTrait.Curse || trait == EliteTrait.Frenzy;

        public int FrenzyStacks { get; private set; }
        public bool IsCurser => HasTrait(EliteTrait.Curse);

        static readonly List<EnemyBrain> FrenzyLive = new List<EnemyBrain>();
        static bool _frenzyHooked;
        float _barrierTimer;

        void ApplyFantasyTrait(EliteTrait trait)
        {
            if (trait == EliteTrait.Barrier) RaiseBarrier();
            if (trait == EliteTrait.Frenzy)
            {
                FrenzyLive.Add(this);
                if (!_frenzyHooked)
                {
                    _frenzyHooked = true;
                    Damageable.AnyDied += OnAnyDiedForFrenzy;
                }
            }
        }

        void RaiseBarrier()
        {
            Body.AddWard(Mathf.Max(1, Mathf.RoundToInt(Body.MaxHealth * GameConfig.GetOptional("elite.barrier_ratio", 0.3f))));
            _barrierTimer = 0f;
            BarrierRaised?.Invoke(this);
        }

        // TickElite에서: 방벽이 다 깎였으면 잠시 뒤 다시
        void TickFantasyTraits(float dt)
        {
            if (!HasTrait(EliteTrait.Barrier) || Body.Ward > 0 || _dead) return;
            _barrierTimer += dt;
            if (_barrierTimer >= GameConfig.GetOptional("elite.barrier_interval", 6f)) RaiseBarrier();
        }

        static void OnAnyDiedForFrenzy(Damageable died)
        {
            if (died == null) return;
            for (int i = FrenzyLive.Count - 1; i >= 0; i--)
            {
                var b = FrenzyLive[i];
                if (b == null || b._dead) { FrenzyLive.RemoveAt(i); continue; }
                if (b.Body == died) continue;
                if (!died.TryGetComponent(out EnemyBrain other) || other == b) continue; // 플레이어 · 항아리는 아니에요
                Vector3 d = died.transform.position - b.transform.position;
                d.y = 0f;
                if (d.magnitude > GameConfig.GetOptional("elite.frenzy_radius", 6f)) continue;
                b.AddFrenzy();
            }
        }

        void AddFrenzy()
        {
            if (FrenzyStacks >= Mathf.RoundToInt(GameConfig.GetOptional("elite.frenzy_max", 3f))) return;
            FrenzyStacks++;
            AttackPower *= 1f + GameConfig.GetOptional("elite.frenzy_attack", 0.15f);
            MoveSpeed *= 1f + GameConfig.GetOptional("elite.frenzy_speed", 0.1f);
            Frenzied?.Invoke(this, FrenzyStacks);
        }

        void CleanupFantasyTraits() => FrenzyLive.Remove(this);
    }
}
