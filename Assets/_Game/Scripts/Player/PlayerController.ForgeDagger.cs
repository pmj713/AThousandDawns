using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 대장간 융합 비도 효과 (무기 시트 5번 표 98~108행). 비도를 들었을 때만, 검 효과 대신 붙어요. ▶ = 강화판 (같은 키워드 두 홈)
    //   열양: 비도가 꽂혀 있는 적은 1초마다 화상 1중첩 ▶ 꽂힌 비도 2개마다 그 적의 화상 최대 중첩 +1 (최대 +3, 회수하면 내려감)
    //   빙백: 비도가 꽂혀 있는 동안 그 적의 둔화가 풀리지 않음 ▶ 회수할 때 꽂혀 있던 적에게 둔화 2중첩
    //   뇌정: 비도가 꽂힐 때마다 감전 1중첩 ▶ 꽂힌 비도 1개당 그 적의 감전 최대 중첩 +1 (최대 +5)
    //   강기: 비도가 적 1명을 관통, 관통한 비도는 다음 적에게 꽂힘 ▶ 2명 관통, 관통할 때마다 피해 +15%
    //   경공: 대시할 때 반경 3칸 안의 남은 비도를 모아 앞쪽 적에게 다시 던짐 (1발 8, 다시 꽂힘) ▶ 반경 6칸, 다시 던진 비도 피해 +50%
    //   금강: 회수하면 보호막: 돌아온 비도 1개당 최대 체력 1% (최대 10%), 3초 ▶ 1개당 2% (최대 15%), 5초
    // 강화판은 기본 효과에 더해져요.
    public sealed partial class PlayerController
    {
        static float FK(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        readonly Dictionary<Damageable, float> _daggerBurnTimers = new Dictionary<Damageable, float>();
        readonly HashSet<StatusEffects> _daggerStatus = new HashSet<StatusEffects>();
        readonly Dictionary<Damageable, int> _stuckCounts = new Dictionary<Damageable, int>();
        System.Predicate<StatusEffects> _dropDaggerStatus;

        // 관통 (강기): 몇 명을 더 뚫고 지나가는지
        int DaggerPierce => DaggerForge(Keyword.Ganggi) switch { 0 => 0, 1 => 1, _ => 2 };

        // 비도가 적에게 맞아 꽂힐 때 (뇌정: 감전 1중첩)
        void OnDaggerStuck(Damageable target)
        {
            if (DaggerForge(Keyword.Noejeong) > 0 && target != null && !target.IsDead) StatusEffects.For(target).AddShock();
        }

        // 매 프레임 (TickDaggers 뒤): 꽂힌 비도 수에 따라 적의 상태를 맞춰요
        void TickDaggerForge(float dt)
        {
            _stuckCounts.Clear();
            foreach (var d in _daggers)
                if (!d.Flying && !d.Returning && d.Stuck != null && !d.Stuck.IsDead)
                    _stuckCounts[d.Stuck] = _stuckCounts.TryGetValue(d.Stuck, out int n) ? n + 1 : 1;

            // 꽂힌 비도가 빠진 적은 원래대로
            foreach (var s in _daggerStatus)
                if (s != null && !_stuckCounts.ContainsKey(s.Body)) { s.ExtraBurnMax = 0; s.ExtraShockMax = 0; s.HoldSlow = false; }
            _daggerStatus.RemoveWhere(_dropDaggerStatus ??= s => s == null || !_stuckCounts.ContainsKey(s.Body)); // 람다를 한 번만 만들어요 (매 프레임)
            if (_stuckCounts.Count == 0)
            {
                _daggerBurnTimers.Clear();
                return;
            }

            int fire = DaggerForge(Keyword.Yeolyang), frost = DaggerForge(Keyword.Bingbaek), thunder = DaggerForge(Keyword.Noejeong);
            float burnGap = FK("forge.knife_yeolyang_interval", 1f);
            foreach (var kv in _stuckCounts)
            {
                var status = StatusEffects.For(kv.Key);
                _daggerStatus.Add(status);
                status.ExtraBurnMax = fire >= 2 ? Mathf.Min(Mathf.RoundToInt(FK("forge.knife_yeolyang_max", 3f)), kv.Value / 2) : 0;
                status.ExtraShockMax = thunder >= 2 ? Mathf.Min(Mathf.RoundToInt(FK("forge.knife_noejeong_max", 5f)), kv.Value) : 0;
                status.HoldSlow = frost > 0;
                if (fire > 0)
                {
                    float t = (_daggerBurnTimers.TryGetValue(kv.Key, out float left) ? left : burnGap) - dt;
                    if (t <= 0f)
                    {
                        status.AddBurn(BurnPerStackDps(_keywords.Count(Keyword.Yeolyang)), 0);
                        t += burnGap;
                    }
                    _daggerBurnTimers[kv.Key] = t;
                }
            }
        }

        // 회수할 때 (돌아오는 비도 수, 꽂혀 있던 적들)
        void OnDaggersRecalled(int returning, IEnumerable<Damageable> stuckTargets)
        {
            // 빙백 강화판: 꽂혀 있던 적에게 둔화 2중첩
            if (DaggerForge(Keyword.Bingbaek) >= 2)
                foreach (var t in stuckTargets)
                    if (t != null && !t.IsDead) StatusEffects.For(t).AddSlow(0f, false, Mathf.RoundToInt(FK("forge.knife_bingbaek_slow", 2f)));

            // 금강: 돌아온 비도 1개당 보호막
            int guard = DaggerForge(Keyword.Geumgang);
            if (guard > 0 && returning > 0)
            {
                bool strong = guard >= 2;
                float per = strong ? FK("forge.knife_geumgang_per_strong", 0.02f) : FK("forge.knife_geumgang_per", 0.01f);
                float capRatio = strong ? FK("forge.knife_geumgang_cap_strong", 0.15f) : FK("forge.knife_geumgang_cap", 0.1f);
                float time = strong ? FK("forge.knife_geumgang_time_strong", 5f) : FK("forge.knife_geumgang_time", 3f);
                int amount = Mathf.Max(1, Mathf.RoundToInt(_maxHealth * Mathf.Min(capRatio, per * returning)));
                _shields.Add(amount, time, Mathf.Min(ShieldCap, Mathf.RoundToInt(_maxHealth * capRatio)));
            }
        }

        // 경공: 대시할 때 주변 남은 비도를 모아 앞쪽 적에게 다시 던져요
        void OnDashStartedForDagger()
        {
            int level = DaggerForge(Keyword.Gyeonggong);
            if (level <= 0) return;
            float radius = level >= 2 ? FK("forge.knife_gyeonggong_radius_strong", 6f) : FK("forge.knife_gyeonggong_radius", 3f);
            float bonus = level >= 2 ? FK("forge.knife_gyeonggong_bonus_strong", 0.5f) : 0f;
            Vector3 me = transform.position;

            // 앞쪽 (대시 방향 90° 안) 가장 가까운 적
            Damageable target = null;
            float best = float.MaxValue;
            foreach (var e in Damageable.All())
            {
                if (e.IsDead || !e.Targetable) continue;
                Vector3 to = Flatten(e.transform.position - me);
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(_dashDirection, to) > 90f) continue;
                if (to.magnitude < best) { best = to.magnitude; target = e; }
            }

            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning) continue;
                if (Vector3.Distance(Flatten(d.Go.transform.position), Flatten(me)) > radius) continue;
                Vector3 from = me + Vector3.up * 0.8f;
                Vector3 dir = target != null ? Flatten(target.transform.position - me) : _dashDirection;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : _facing;
                var hit = PlayerHit(D("knife.shot_damage", 8f));
                hit.AttackIncrease += bonus;
                d.Stuck = null;
                d.Go.transform.position = from + dir * 0.6f;
                d.Go.transform.rotation = Quaternion.LookRotation(dir);
                d.Dir = dir;
                d.Flight = D("knife.range", 8f);
                d.Hit = hit;
                d.Tag = HitTag.Basic;
                d.Gauge = _gaugeBasic;
                d.Pierced = 0;
                d.HitSet = null;
                d.Life = D("knife.lifetime", 6f);
                if (d.Rend != null) d.Rend.enabled = true;
            }
        }
    }
}
