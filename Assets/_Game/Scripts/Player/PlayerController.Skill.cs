using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 초식 바꾸기 (비급). 고수를 쓰러뜨리고 "비급 전수"를 고르면 그 고수의 초식으로 바뀌어요.
    // 형태는 고수에게서, 속성은 내 빌드에서: 초식 적중이라 키워드 2단계 효과(화상·둔화·연쇄 번개)가 그대로 붙어요.
    // 죽림섬 (청죽객): 가까운 적 쪽으로 3번 순간이동하며 베기 (0.25초 간격). 지나간 직선 위의 적이 모두 맞아요. 베는 동안 무적.
    //   수치는 시트에 아직 없어서 임시 (1번에 회선참 피해의 50%, 사거리 6칸, 폭 1.2칸).
    // 붕산권 (철권 마웅): 땅을 내리쳐 반경 3칸 원형 충격파, 맞은 적을 2칸 밀어내요 (보스·고수는 안 밀림). 임시: 회선참 피해 그대로.
    // 금강장 (파계승 혜광): 앞으로 장력을 뻗어 부채꼴 4칸 (100도), 회선참 피해 ×1.3 · 맞은 적을 1.5칸 밀어냄. 하나라도 맞히면 보호막 (최대 체력 5%, 8초).
    // 비도는 초식이 늘 회수예요. 배운 비급은 회수에 효과를 덧붙여요 (DaggerScrollText):
    //   죽림섬: 비도가 가장 많이 꽂힌 적에게 순간이동해 한 번 베기 (회선참 피해 50%) · 붕산권: 회수와 함께 내 둘레 충격파 (반경 2.5칸, 60%, 밀어냄)
    //   금강장: 돌아온 비도 1개당 보호막 (최대 체력 1%, 8초)
    public enum SkillKind { Spin, BambooFlash, Quake, Palm }

    public sealed partial class PlayerController
    {
        int _flashDone;
        int _flashSoundStart; // 죽림섬 소리를 고를 첫 자리 (시전마다 무작위)
        bool _flashChained;

        public SkillKind Skill { get; private set; }
        public string SkillName => !UsesDagger ? SkillLabel(Skill)
            : Skill == SkillKind.Spin ? $"회수 (비도 {DaggerCount})" : $"회수 · {SkillLabel(Skill)} (비도 {DaggerCount})";

        // 비급 이름과 한 줄 설명 (고수 보상 · 낡은 서고 · 객잔 공통)
        public static string SkillLabel(SkillKind s) => s switch
        {
            SkillKind.BambooFlash => "죽림섬",
            SkillKind.Quake => "붕산권",
            SkillKind.Palm => "금강장",
            _ => "회선참",
        };

        public static string SkillText(SkillKind s) => s switch
        {
            SkillKind.BambooFlash => "가까운 적에게 3번 순간이동하며 베기",
            SkillKind.Quake => "땅을 내리쳐 원형 충격파, 맞은 적을 밀어냄",
            SkillKind.Palm => "앞으로 장력을 뻗어 부채꼴 공격, 밀어내고 맞히면 보호막",
            _ => "주변을 한 바퀴 베기",
        };

        // 비도일 때 비급이 회수에 덧붙이는 효과
        public static string DaggerScrollText(SkillKind s) => s switch
        {
            SkillKind.BambooFlash => "회수할 때 비도가 가장 많이 꽂힌 적에게 순간이동해 베기",
            SkillKind.Quake => "회수할 때 내 둘레에 충격파, 적을 밀어냄",
            SkillKind.Palm => "회수할 때 돌아온 비도 1개당 보호막",
            _ => "방 안의 비도를 모두 거두며 피해",
        };
        // 비도를 들면 초식은 늘 회수 (비급은 회수에 효과를 덧붙여요)
        bool RecallSkill => UsesDagger;

        public void LearnSkill(SkillKind skill)
        {
            Skill = skill;
            _skillElapsed = -1f;
            _skillCooldownLeft = 0f;
        }

        // 붕산권: 회선참과 같은 시전 시간, 판정 순간에 원형 충격파 + 밀어내기
        void TickQuake()
        {
            if (_skillHitDone || _skillElapsed < _skillHitTime) return;
            _skillHitDone = true;
            _hitThisSwing.Clear();
            Sfx.Play(SfxId.Quake); // 붕산권
            QuakeBurst(GameConfig.Get("skill_quake.radius", 3f), GameConfig.Get("skill_quake.ratio", 1f), GameConfig.Get("skill_quake.knockback", 2f));
        }

        // 원형 충격파 + 밀어내기 (붕산권 · 비도 회수 붕산권)
        void QuakeBurst(float radius, float ratio, float push)
        {
            _hitThisSwing.Clear();
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, 360f, 0.2f);
            var hit = PlayerHit(_skillDamage * ratio);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int count = HitSector(radius, 360f, hit, HitTag.Skill);
            Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);
            foreach (var target in _hitThisSwing)
            {
                if (target == null || !target.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain)) continue;
                Vector3 away = target.transform.position - transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) brain.Displace(away.normalized * push);
            }
            int thunder = _keywords.Count(Keyword.Noejeong);
            if (count > 0 && thunder >= 2)
            {
                float trigger = DamageFormula.BeforeToughness(hit);
                Lightning.Chain(_firstHit, transform.position + Vector3.up, trigger, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
                StrikeAfterSkillChain(_firstHit, trigger, thunder);
            }
        }

        // ── 비도: 회수에 덧붙는 비급 효과 (회수 직전에 고른 표적 · 돌아온 비도 수)

        Damageable MostStuckTarget()
        {
            Damageable best = null;
            int most = 0;
            foreach (var d in _daggers)
            {
                if (d.Stuck == null || d.Stuck.IsDead || !d.Stuck.Targetable) continue;
                int n = StuckDaggers(d.Stuck);
                if (n > most) { most = n; best = d.Stuck; }
            }
            return best;
        }

        void ScrollOnRecall(Damageable mark, int returning)
        {
            switch (Skill)
            {
                case SkillKind.BambooFlash:
                    if (mark == null || mark.IsDead) return;
                    Sfx.Play(SfxId.BambooFlash);
                    Vector3 from = transform.position, to = mark.transform.position - from;
                    to.y = 0f;
                    if (to.sqrMagnitude < 1e-4f) return;
                    Vector3 dir = to.normalized;
                    _controller.Move(dir * (to.magnitude + 1f)); // 적을 지나쳐 뒤에 서요 (벽에는 막힘)
                    _facing = dir;
                    ApplyFacing();
                    _invulnerableTimer = Mathf.Max(_invulnerableTimer, 0.2f);
                    if (swingFlash != null) swingFlash.Show(transform.position, -dir, 1.2f, 90f, 0.12f);
                    var hit = PlayerHit(_skillDamage * GameConfig.GetOptional("knife.scroll_flash_ratio", 0.5f));
                    hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                    int count = HitLine(from, transform.position, GameConfig.Get("skill_flash.width", 1.2f), hit, HitTag.Skill);
                    Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);
                    break;
                case SkillKind.Quake:
                    Sfx.Play(SfxId.Quake);
                    QuakeBurst(GameConfig.GetOptional("knife.scroll_quake_radius", 2.5f), GameConfig.GetOptional("knife.scroll_quake_ratio", 0.6f), GameConfig.Get("skill_quake.knockback", 2f));
                    break;
                case SkillKind.Palm:
                    if (returning <= 0) return;
                    int amount = Mathf.Max(1, Mathf.RoundToInt(_maxHealth * GameConfig.GetOptional("knife.scroll_palm_shield", 0.01f) * returning));
                    _shields.Add(amount, GameConfig.GetOptional("skill_palm.shield_duration", 8f), ShieldCap);
                    break;
            }
        }

        // 금강장: 회선참과 같은 시전 시간, 판정 순간에 앞쪽 부채꼴 + 밀어내기 · 맞히면 보호막
        void TickPalm()
        {
            if (_skillHitDone || _skillElapsed < _skillHitTime) return;
            _skillHitDone = true;
            _hitThisSwing.Clear();
            Sfx.Play(SfxId.Quake); // 전용 소리가 생기기 전까지 붕산권 소리
            float radius = GameConfig.GetOptional("skill_palm.radius", 4f);
            float angle = GameConfig.GetOptional("skill_palm.angle", 100f);
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, angle, 0.2f);
            var hit = PlayerHit(_skillDamage * GameConfig.GetOptional("skill_palm.ratio", 1.3f));
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int count = HitSector(radius, angle, hit, HitTag.Skill);
            Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);
            float push = GameConfig.GetOptional("skill_palm.knockback", 1.5f);
            foreach (var target in _hitThisSwing)
            {
                if (target == null || !target.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain)) continue;
                Vector3 away = target.transform.position - transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) brain.Displace(away.normalized * push);
            }
            if (count > 0)
                _shields.Add(Mathf.Max(1, Mathf.RoundToInt(_maxHealth * GameConfig.GetOptional("skill_palm.shield", 0.05f))),
                    GameConfig.GetOptional("skill_palm.shield_duration", 8f), ShieldCap);
            int thunder = _keywords.Count(Keyword.Noejeong);
            if (count > 0 && thunder >= 2)
            {
                float trigger = DamageFormula.BeforeToughness(hit);
                Lightning.Chain(_firstHit, transform.position + Vector3.up, trigger, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
                StrikeAfterSkillChain(_firstHit, trigger, thunder);
            }
        }

        int FlashCount => Mathf.Max(1, Mathf.RoundToInt(GameConfig.Get("skill_flash.count", 3f)));
        float FlashGap => GameConfig.Get("skill_flash.gap", 0.25f);

        void TickBambooFlash()
        {
            if (!_skillHitDone)
            {
                _skillHitDone = true;
                _flashDone = 0;
                _flashChained = false;
            }
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, 0.1f); // 베는 동안 무적
            while (_flashDone < FlashCount && _skillElapsed >= _flashDone * FlashGap) FlashSlash();
            if (_skillElapsed < FlashCount * FlashGap + 0.05f) return;
            _skillElapsed = -1f;
            ApplyFacing();
        }

        void FlashSlash()
        {
            _flashDone++;
            // 죽림섬: 시전마다 무작위 자리에서 시작해 1·2·3타가 서로 다른 소리
            if (_flashDone == 1) _flashSoundStart = Random.Range(0, Mathf.Max(1, Sfx.VariantCount(SfxId.BambooFlash)));
            Sfx.PlayEach(SfxId.BambooFlash, _flashSoundStart + _flashDone - 1);
            float range = GameConfig.Get("skill_flash.range", 6f);
            float width = GameConfig.Get("skill_flash.width", 1.2f);
            Vector3 from = transform.position;
            Vector3 dir = _facing;
            float length = range * 0.7f;
            var target = NearestTarget(range);
            if (target != null)
            {
                Vector3 to = target.transform.position - from;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                {
                    dir = to.normalized;
                    length = Mathf.Min(range, to.magnitude + 1f); // 적을 지나쳐 뒤에 서요
                }
            }
            _controller.Move(dir * length); // 벽에는 막히고 적은 지나가요
            Vector3 end = transform.position;
            _facing = dir;
            ApplyFacing();
            if (swingFlash != null) swingFlash.Show(end, -dir, 1.2f, 90f, 0.12f);

            var hit = PlayerHit(_skillDamage * GameConfig.Get("skill_flash.ratio", 0.5f));
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int count = HitLine(from, end, width, hit, HitTag.Skill);
            Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);

            // #뇌정 2단계: 초식이 적중하면 연쇄 번개 (시전마다 한 번)
            int thunder = _keywords.Count(Keyword.Noejeong);
            if (count > 0 && thunder >= 2 && !_flashChained)
            {
                _flashChained = true;
                float trigger = DamageFormula.BeforeToughness(hit);
                Lightning.Chain(_firstHit, transform.position + Vector3.up, trigger, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
                StrikeAfterSkillChain(_firstHit, trigger, thunder);
            }
        }

        Damageable NearestTarget(float range)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, range, _overlap, ~0, QueryTriggerInteraction.Collide);
            Damageable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                var d = _overlap[i].GetComponentInParent<Damageable>();
                if (d == null || d.IsDead || !d.Targetable) continue;
                float distance = Vector3.Distance(d.transform.position, transform.position);
                if (distance < 0.3f || distance >= bestDistance) continue;
                best = d;
                bestDistance = distance;
            }
            return best;
        }

        // from → to 직선 (폭 width) 위의 적. 베기 한 번마다 같은 적은 한 번.
        int HitLine(Vector3 from, Vector3 to, float width, HitDamageInput template, HitTag tag)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float length = d.magnitude;
            Vector3 dir = length > 1e-3f ? d / length : _facing;
            Vector3 mid = (from + to) * 0.5f;
            int found = Physics.OverlapSphereNonAlloc(mid, length * 0.5f + width + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
            _hitThisSwing.Clear();
            _firstHit = null;
            _swingKills = 0;
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                var target = _overlap[i].GetComponentInParent<Damageable>();
                if (target == null || target.IsDead || !target.Targetable || _hitThisSwing.Contains(target)) continue;
                Vector3 rel = target.transform.position - from;
                rel.y = 0f;
                float along = Vector3.Dot(rel, dir);
                float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(dir.z, 0f, -dir.x)));
                if (along < -target.BodyRadius || along > length + target.BodyRadius || side > width * 0.5f + target.BodyRadius) continue;
                _hitThisSwing.Add(target);
                HitTarget(target, template, tag);
                hits++;
            }
            return hits;
        }
    }
}
