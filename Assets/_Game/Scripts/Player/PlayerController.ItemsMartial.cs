using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Player
{
    // #강기·#금강·#경공 기물 30종 (기물 탭 46~75행, data_items).
    // 새로 생기는 공격(금강저 충격파, 버들잎 표창, 잔영 폭발, 구름 비단)의 "(피해 +N%)"는 기준 피해 × (1 + N%)로 읽어요 (파생 피해).
    public sealed partial class PlayerController
    {
        float _itemShieldDuration, _afterimagePullSpeed, _dashPathWidth;
        ComboTimeline.HitTiming _finisherTiming;

        int _whetstoneSwings, _bladeStacks, _swingKills;
        bool _sandbagReady, _cloakFirstAttack, _tabletUsed;
        float _bladeIdle, _chainTimer, _windKnotTimer, _cloakTimer;
        readonly HashSet<Damageable> _dashTouched = new HashSet<Damageable>();

        public int BladeStacks => _bladeStacks;
        public bool TabletUsed => _tabletUsed;

        void ReadMartialItemTuning()
        {
            _itemShieldDuration = GameConfig.Get("item.shield_duration", 3f);
            _afterimagePullSpeed = GameConfig.Get("item.afterimage_pull_speed", 4f);
            _dashPathWidth = GameConfig.Get("item.dash_path_width", 1.2f);
        }

        void TickMartialItemTimers(float dt)
        {
            if (_chainTimer > 0f) _chainTimer -= dt;
            if (_windKnotTimer > 0f) _windKnotTimer -= dt;
            if (_cloakTimer > 0f) _cloakTimer -= dt;
            // 부러진 검날: 3초 쉬면 초기화
            if (_bladeStacks > 0)
            {
                _bladeIdle += dt;
                if (_bladeIdle >= V("broken_blade", 3) || !Has("broken_blade")) _bladeStacks = 0;
            }
        }

        // ── 스탯

        // 청풍 검수 · 부러진 검날 중첩 · 바람 매듭 (주화입마, 대시 후)
        float ItemAttackSpeedBonus =>
            V("breeze_tassel", 1)
            + Mathf.Min(V("broken_blade", 2), _bladeStacks * V("broken_blade", 1))
            + (_windKnotTimer > 0f ? V("wind_knot", 3) : 0f);

        // 짚신 · 흑풍 망토 (대시 후)
        float ItemMoveSpeedBonus => V("straw_sandals", 1) + (_cloakTimer > 0f ? V("black_wind_cloak", 1) : 0f);

        // 거북 등껍질 (0개면 2배)
        float ItemToughness => V("turtle_shell", 1) * (Pure ? V("turtle_shell", 2) : 1f) + PackToughness; // + 철포삼 조끼

        // 죽갑 조각
        float EffectiveDefense => Defense + V("bamboo_armor", 1);

        // 쇠사슬 (피격 후 잠시) · 금빛 가사 (체력 절반 이하, 0개면 2배)
        float ItemDamageReduction
        {
            get
            {
                float reduction = _chainTimer > 0f ? V("iron_chain", 1) : 0f;
                if (Has("golden_kasaya") && Health <= MaxHealth * V("golden_kasaya", 3))
                    reduction += V("golden_kasaya", 1) * (Pure ? V("golden_kasaya", 2) : 1f);
                return reduction;
            }
        }

        // 비연 깃털: 3레벨이면 대시 +1
        int ItemDashBonus
        {
            get
            {
                if (_items == null || !_items.Active("swallow_feather", out var feather)) return PackDashBonus;
                return (PlayerItems.LevelOf(feather) >= C("swallow_feather", 2) ? C("swallow_feather", 3) : 0) + PackDashBonus; // + 천리 신발
            }
        }

        // 바람개비 (0개면 2배) · 구름 비단 부작용 (대시 무적 시간 절반)
        float DashIframe => (_dashIframe + V("pinwheel", 1) * (Pure ? V("pinwheel", 2) : 1f)) * Mathf.Max(0f, 1f - Side("dash_iframe"));

        void RefreshMartialItems()
        {
            // 검보 조각: 3타째 뒤에 4타째 (3타째와 같은 시간)
            _combo.SetExtraHit(Has("sword_manual") ? _finisherTiming : (ComboTimeline.HitTiming?)null, V("sword_manual", 1));
        }

        // ── 평타

        // 가죽 손목대: 평타 사거리 + · 묵철 칼집 (0개면): 3타째 범위 +
        float BasicRangeMultiplier(bool finisher) =>
            (1f + V("leather_bracer", 1)) * (finisher && Pure ? 1f + V("ink_scabbard", 2) : 1f);

        // 평타 한 번에 더하는 피해 (② 공격 증가): 목검 · 혈수 붕대 · 묵철 칼집(3타째) · 검보 조각(4타째) · 연마석(초식 뒤) · 철장 모래주머니(처치 뒤)
        float BasicSwingBonus(bool finisher, bool extra)
        {
            float bonus = V("wooden_sword", 1) * (Pure ? V("wooden_sword", 2) : 1f) + V("blood_bandage", 1);
            if (finisher) bonus += V("ink_scabbard", 1);
            if (extra) bonus += V("sword_manual", 2);
            bonus += PackSwingBonus(); // 그림자 보법서 · 일섬 검결
            if (_whetstoneSwings > 0)
            {
                _whetstoneSwings--;
                bonus += V("whetstone", 1);
            }
            if (_sandbagReady)
            {
                _sandbagReady = false;
                bonus += V("iron_sandbag", 1);
            }
            return bonus;
        }

        void AfterBasicSwing(int hits, HitDamageInput swing)
        {
            if (_swingKills > 0 && Has("iron_sandbag")) _sandbagReady = true; // 철장 모래주머니
            FireIlseom(swing); // 일섬 검결
            if (hits <= 0) return;

            // 부러진 검날: 적중마다 공격 속도 +1% 중첩
            if (Has("broken_blade"))
            {
                float per = V("broken_blade", 1);
                int maxStacks = per > 0f ? Mathf.CeilToInt(V("broken_blade", 2) / per - 1e-4f) : 0;
                _bladeStacks = Mathf.Min(maxStacks, _bladeStacks + 1);
                _bladeIdle = 0f;
            }

            // 혈수 붕대 주화입마: 평타 적중 시 강기 파편
            if (Deviated("blood_bandage"))
            {
                float damage = DamageFormula.BeforeToughness(swing) * V("blood_bandage", 3);
                for (int i = 0; i < C("blood_bandage", 2); i++) FireGanggiBolt(damage, 0.6f);
            }
        }

        // ── 초식 · 대시 공격

        void OnSkillStartedForItems()
        {
            _whetstoneSwings = C("whetstone", 2); // 연마석: 초식 직후 평타 3회
            AddItemShield(V("guardian_norigae", 1)); // 수호 노리개
        }

        // 비연 깃털 · 운룡 허리띠: 대시 공격 피해 +
        float DashAttackBonus => V("swallow_feather", 1) + V("cloud_dragon_belt", 1) + PackDashAttackBonus; // + 천리 신발

        void AfterDashAttack(int hits, HitDamageInput hit)
        {
            AfterDashAttackForPack(); // 뇌격 장갑: 대시 공격 한 번 끝
            // 운룡 허리띠: 대시가 지나온 길의 모든 적도 대시 공격으로 베어요
            if (Has("cloud_dragon_belt")) hits += HitPath(_dashStart, transform.position, _dashPathWidth * 0.5f, hit, HitTag.DashAttack);
            if (hits <= 0) return;
            AddItemShield(V("iron_bracelet", 1)); // 철갑 팔찌
            Gauge = Mathf.Min(GaugeCap, Gauge + _gaugeMax * V("cloud_silk", 2)); // 구름 비단
        }

        // 대시 공격 판정과 같은 방식으로, from → to 선분에서 halfWidth 안의 적 (이미 맞은 적 빼고)
        int HitPath(Vector3 from, Vector3 to, float halfWidth, HitDamageInput template, HitTag tag)
        {
            int hits = 0;
            foreach (var target in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (target.IsDead || !target.Targetable || _hitThisSwing.Contains(target)) continue;
                if (DistanceToSegment(target.transform.position, from, to) > halfWidth + target.BodyRadius) continue;
                _hitThisSwing.Add(target);
                HitTarget(target, template, tag);
                hits++;
            }
            return hits;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0f;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        // ── 대시

        void OnDashStartedForItems()
        {
            _dashTouched.Clear();

            // 버들잎 표창: 대시할 때 앞으로 표창
            int darts = C("willow_dart", 2);
            if (Has("willow_dart"))
            {
                float damage = BaseDamage * (1f + V("willow_dart", 1));
                for (int i = 0; i < darts; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, (i - (darts - 1) * 0.5f) * 10f, 0f) * _dashDirection;
                    GanggiBolt.Fire(transform.position + Vector3.up, dir, _ganggiSpeed, _ganggiRange, 0.4f, damage, 0, dart: true);
                }
            }

            // 잔영 가면: 대시를 시작한 자리에 잔상 (0개면 사라질 때 터짐)
            if (Has("afterimage_mask"))
            {
                float explode = Pure ? BaseDamage * V("afterimage_mask", 3) : 0f;
                Afterimage.Create(transform.position, V("afterimage_mask", 1), V("afterimage_mask", 2), _afterimagePullSpeed, explode, V("afterimage_mask", 4));
            }
        }

        // 구름 비단: 대시가 지나간 적에게 피해 (대시 한 번에 적마다 한 번)
        void TouchDuringDash()
        {
            if (!Has("cloud_silk")) return;
            float raw = BaseDamage * (1f + V("cloud_silk", 1));
            float reach = BodyRadius + _dashPathWidth * 0.5f;
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d.IsDead || !d.Targetable || _dashTouched.Contains(d)) continue;
                Vector3 offset = d.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude > reach + d.BodyRadius) continue;
                _dashTouched.Add(d);
                d.ApplyHit(DamageFormula.Proc(raw, 1f, d.Defense), false);
            }
        }

        void OnDashEndedForItems()
        {
            if (Deviated("wind_knot")) _windKnotTimer = V("wind_knot", 2); // 바람 매듭 주화입마: 대시 후 공격 속도
            if (Has("black_wind_cloak"))
            {
                _cloakTimer = V("black_wind_cloak", 2); // 흑풍 망토: 대시 후 이동속도
                if (Deviated("black_wind_cloak")) _cloakFirstAttack = true; // 주화입마: 대시 후 첫 공격 피해 +
            }
        }

        // 흑풍 망토 주화입마: 대시 후 첫 공격 한 번
        float ConsumeCloakBonus()
        {
            if (!_cloakFirstAttack) return 0f;
            _cloakFirstAttack = false;
            return V("black_wind_cloak", 3);
        }

        // ── 피격 · 처치

        // 불괴 호패: 쓰러질 피해를 받으면 체력 1로 버티고 잠시 무적 (방마다 1회). 실제로 깎을 피해를 돌려줘요.
        int SurviveLethal(int damage)
        {
            if (damage < Health || _tabletUsed || !Has("unbreakable_tablet")) return damage;
            _tabletUsed = true;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, V("unbreakable_tablet", 1));
            return Mathf.Max(0, Mathf.CeilToInt(Health) - 1);
        }

        void OnHurtForMartialItems()
        {
            if (_items == null || _items.Items.Count == 0) return;
            if (Has("iron_chain")) _chainTimer = V("iron_chain", 2); // 쇠사슬: 피격 후 받는 피해 −
            if (HasSide("hurt_reset_stacks")) _bladeStacks = 0;      // 부러진 검날 부작용

            // 금강저: 충격파와 기세 (주화입마면 넉백)
            if (Has("vajra"))
            {
                float raw = BaseDamage * (1f + V("vajra", 1));
                float radius = V("vajra", 3);
                float push = Deviated("vajra") ? V("vajra", 4) : 0f;
                Vector3 center = transform.position;
                ForEachEnemyNear(center, radius, d =>
                {
                    d.ApplyHit(DamageFormula.Proc(raw, 1f, d.Defense), false, PopupStyle.Burst);
                    if (push > 0f && d.TryGetComponent(out EnemyBrain brain))
                    {
                        Vector3 away = d.transform.position - center;
                        away.y = 0f;
                        if (away.sqrMagnitude > 1e-4f) brain.Displace(away.normalized * push);
                    }
                });
                Gauge = Mathf.Min(GaugeCap, Gauge + _gaugeMax * V("vajra", 2));
                if (swingFlash != null) swingFlash.Show(center, _facing, radius, 360f, 0.15f);
            }
        }

        // 혈석: 적 처치 시 체력 + (주화입마면 더)
        void OnEnemyDiedForMartialItems()
        {
            if (!Has("blood_stone")) return;
            Heal(Deviated("blood_stone") ? C("blood_stone", 2) : C("blood_stone", 1));
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        // ── 도우미

        bool HasSide(string kind) => Side(kind) > 0f;

        void AddItemShield(float maxHealthRatio)
        {
            if (maxHealthRatio <= 0f) return;
            _shields.Add(Mathf.Max(1, Mathf.RoundToInt(_maxHealth * maxHealthRatio)), _itemShieldDuration, ShieldCap);
        }
    }
}
