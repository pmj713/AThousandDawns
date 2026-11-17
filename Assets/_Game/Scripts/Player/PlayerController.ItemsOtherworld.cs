using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 이계 기물 30종 (차원 겹침) + 대시 쪽으로 바꾼 무협 짚신 · 흑풍 망토: 무협 기물처럼 조건과 발동이 있는 효과 (data_items v1~v4).
    //   경공: 고블린 가죽장화 (대시 회복) · 엘프 가죽 바지 (대시 거리, 대시 후 공격 속도) · 바람 정령 망토 (대시로 벰) · 엘프 깃털 귀걸이 (대시 무적) · 하피 깃털 팔찌 (대시가 끝나면 깃털)
    //   빙백: 슬라임 팔찌 (맞으면 잠시 받는 피해 −) · 마법사 고깔모자 (초식 쿨타임 −) · 얼음 정령 목걸이 (둔화된 적 피해 +) · 서리 마법사 로브 (맞으면 둘레 둔화) · 빙결 수정 장화 (대시 길에 서리)
    //   뇌정: 룬 귀걸이 (감전된 적 피해 +) · 폭풍 서클릿 (초식 적중 감전) · 뇌전 룬 허리띠 (처치 연쇄 번개) · 폭풍 정령 팔찌 (3타 낙뢰) · 번개 장화 (대시로 감전)
    //   금강: 드워프 강철 허리띠 (보호막 동안 방어 +) · 미스릴 사슬 갑옷 (받는 피해 −) · 성기사 투구 (방마다 보호막) · 드워프 강철 각반 (경직 · 넉백 −) · 성기사 성표 (위기에 회복)
    //   열양: 용 이빨 목걸이 (화상 중인 적 피해 +) · 화염 룬 반지 (초식 적중 화상) · 샐러맨더 망토 (화상 피해 +) · 불꽃 정령 귀걸이 (평타 화상) · 불사조 깃털 장식 (화상 적 처치 회복)
    //   강기: 마검 술 (초식 적중 뒤 공격 속도) · 기사 건틀릿 (3타 피해 +) · 룬 검집 (공격 속도) · 기사 투구 (정예 · 고수 · 보스 피해 +) · 마검사 장화 (대시 후 첫 평타 검기)
    //   무협 짚신 (대시 거리) · 흑풍 망토 (대시 후 공격 속도)
    public sealed partial class PlayerController
    {
        float _elvenTimer, _spellbladeTimer, _slimeTimer, _frostRobeCooldown;
        bool _holyUsed, _spellswordReady;

        // ── 합에 더하는 값 (기존 합 계산이 불러요)
        float OtherworldAttackSpeed =>
            (_elvenTimer > 0f ? V("elven_leggings", 3) : 0f) + (_spellbladeTimer > 0f ? V("spellblade_tassel", 2) : 0f)
            + (_cloakTimer > 0f ? V("black_wind_cloak", 1) : 0f) + V("rune_scabbard", 1);
        float OtherworldDashDistance => V("straw_sandals", 1) + V("elven_leggings", 1);
        float OtherworldDashRecharge => V("goblin_boots", 1);
        float OtherworldDashIframe => V("elf_feather_earring", 1);
        float OtherworldReduction => V("mithril_chainmail", 1) + (_slimeTimer > 0f ? V("slime_bracelet", 2) : 0f);
        float OtherworldDefense => _shields.Any ? V("dwarf_belt", 1) : 0f;
        float OtherworldSkillCooldownCut => V("wizard_hat", 1);
        float OtherworldStaggerCut => Mathf.Clamp01(V("dwarf_greaves", 1));

        void TickOtherworldItems(float dt)
        {
            if (_elvenTimer > 0f) _elvenTimer -= dt;
            if (_spellbladeTimer > 0f) _spellbladeTimer -= dt;
            if (_slimeTimer > 0f) _slimeTimer -= dt;
            if (_frostRobeCooldown > 0f) _frostRobeCooldown -= dt;
        }

        // 맞은 적의 상태 · 종류에 따라 (HitTarget의 상황 배율에 곱해요)
        float OtherworldTargetMultiplier(Damageable target, HitTag tag)
        {
            if (_items == null || _items.Items.Count == 0) return 1f;
            float bonus = 0f;
            if (tag == HitTag.Finisher) bonus += V("knight_gauntlet", 1); // 기사 건틀릿: 3타째
            if (target.TryGetComponent(out StatusEffects s))
            {
                if (s.SlowStacks > 0 || s.IsFrozen) bonus += V("ice_spirit_pendant", 1); // 얼음 정령 목걸이
                if (s.ShockStacks > 0) bonus += V("rune_earring", 1);                    // 룬 귀걸이
                if (s.BurnStacks > 0) bonus += V("dragon_tooth", 1);                     // 용 이빨 목걸이
            }
            if (Has("knight_helm") && target.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain)
                && (brain.IsElite || brain.Rank == AThousandDawns.Enemies.EnemyRank.Master || brain.Rank == AThousandDawns.Enemies.EnemyRank.Boss))
                bonus += V("knight_helm", 1); // 기사 투구: 정예 · 고수 · 보스
            return 1f + bonus;
        }

        // ApplyItemsOnHit에서 (맞힌 뒤)
        void OtherworldOnHit(Damageable target, HitTag tag, StatusEffects status)
        {
            int fire = K(Keyword.Yeolyang);
            switch (tag)
            {
                case HitTag.Basic:
                    if (Roll(V("fire_spirit_earring", 1))) status.AddBurn(BurnPerStackDps(fire), BurnExtra(fire)); // 불꽃 정령 귀걸이
                    break;
                case HitTag.Finisher:
                    if (Roll(V("storm_bracer", 1))) // 폭풍 정령 팔찌: 3타째 낙뢰
                    {
                        int thunder = K(Keyword.Noejeong);
                        Lightning.Strike(target, BaseDamage, V("storm_bracer", 2), DischargePerStack(thunder));
                    }
                    break;
                case HitTag.Skill:
                    status.AddShock(C("storm_circlet", 1));                                      // 폭풍 서클릿
                    status.AddBurn(BurnPerStackDps(fire), BurnExtra(fire), C("fire_rune_ring", 1)); // 화염 룬 반지
                    if (Has("spellblade_tassel")) _spellbladeTimer = V("spellblade_tassel", 1);   // 마검 술
                    break;
            }
            // 마검사 장화: 대시 후 첫 평타가 앞으로 검기를 날려요
            if (_spellswordReady && (tag == HitTag.Basic || tag == HitTag.Finisher))
            {
                _spellswordReady = false;
                GanggiBolt.Fire(transform.position + Vector3.up, _facing, _ganggiSpeed, _ganggiRange, 0.6f, BaseDamage * V("spellsword_boots", 1), 1);
            }
        }

        void OtherworldDashFeathers()
        {
            int feathers = C("harpy_feather_bracelet", 2); // 하피 깃털 팔찌: 대시가 끝나 도착한 자리에서 앞으로 깃털
            if (!Has("harpy_feather_bracelet") || feathers <= 0) return;
            float damage = BaseDamage * V("harpy_feather_bracelet", 1);
            for (int i = 0; i < feathers; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, (i - (feathers - 1) * 0.5f) * 14f, 0f) * _dashDirection;
                GanggiBolt.Fire(transform.position + Vector3.up, dir, _ganggiSpeed, _ganggiRange * 0.7f, 0.35f, damage, 0, dart: true, label: "하피 깃털");
            }
        }

        // TouchDuringDash에서: 대시가 지나가는 적마다 한 번
        bool OtherworldDashTouches => Has("wind_spirit_cloak") || Has("lightning_boots");

        void OtherworldDashTouch(Damageable d)
        {
            if (Has("wind_spirit_cloak")) d.ApplyHit(DamageFormula.Proc(BaseDamage * V("wind_spirit_cloak", 1), 1f, d.Defense), false); // 바람 정령 망토
            if (Has("lightning_boots")) StatusEffects.For(d).AddShock(C("lightning_boots", 1)); // 번개 장화
        }

        void OtherworldDashEnd()
        {
            if (Has("elven_leggings")) _elvenTimer = V("elven_leggings", 2);
            if (Has("spellsword_boots")) _spellswordReady = true;
            if (Has("ice_crystal_boots")) // 빙결 수정 장화: 대시한 길에 서리
            {
                int frost = K(Keyword.Bingbaek);
                FrostTrail.Create(_dashStart, transform.position, _frostTrailWidth, V("ice_crystal_boots", 1), SlowBonus(frost), frost >= 6);
            }
        }

        void OtherworldHurt()
        {
            if (Has("slime_bracelet")) _slimeTimer = V("slime_bracelet", 1); // 슬라임 팔찌: 맞은 뒤 잠시 말랑
            if (Has("frost_robe") && _frostRobeCooldown <= 0f) // 서리 마법사 로브: 맞으면 둘레 둔화
            {
                int frost = K(Keyword.Bingbaek);
                if (ForEachEnemyNear(transform.position, V("frost_robe", 1), d => StatusEffects.For(d).AddSlow(SlowBonus(frost), frost >= 6, C("frost_robe", 3))) > 0)
                    _frostRobeCooldown = V("frost_robe", 2);
            }
            if (Has("holy_amulet") && !_holyUsed && !IsDead && Health <= MaxHealth * V("holy_amulet", 2)) // 성기사 성표: 위기에 한 번
            {
                _holyUsed = true;
                Heal(MaxHealth * V("holy_amulet", 1));
            }
        }

        void OtherworldEnterRoom()
        {
            _holyUsed = false;
            if (Has("paladin_helm")) _shields.Add(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * V("paladin_helm", 1))), 0f, ShieldCap); // 성기사 투구: 방마다 보호막 (깨질 때까지)
        }

        void OtherworldEnemyDied(Damageable dead)
        {
            if (Roll(V("thunder_rune_belt", 1))) // 뇌전 룬 허리띠: 처치하면 연쇄 번개
            {
                int thunder = K(Keyword.Noejeong);
                Lightning.Chain(dead, dead.transform.position + Vector3.up, BaseDamage * V("thunder_rune_belt", 2), ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
            }
            if (Has("phoenix_charm") && dead.TryGetComponent(out StatusEffects s) && s.BurnStacks > 0) Heal(V("phoenix_charm", 1)); // 불사조 깃털 장식
        }
    }
}
