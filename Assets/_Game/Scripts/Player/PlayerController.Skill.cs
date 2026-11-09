using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 초식 바꾸기 (비급). 고수를 쓰러뜨리고 "비급 전수"를 고르면 그 고수의 초식으로 바뀌어요.
    // 형태는 고수에게서, 속성은 내 빌드에서: 초식 적중이라 키워드 2단계 효과(화상·둔화·연쇄 번개)가 그대로 붙어요.
    // 죽림섬 (청죽객): 가까운 적 쪽으로 3번 순간이동하며 베기 (0.25초 간격). 지나간 직선 위의 적이 모두 맞아요. 베는 동안 무적.
    //   수치는 시트에 아직 없어서 임시 (1번에 회선참 피해의 50%, 사거리 6칸, 폭 1.2칸).
    public enum SkillKind { Spin, BambooFlash }

    public sealed partial class PlayerController
    {
        int _flashDone;
        bool _flashChained;

        public SkillKind Skill { get; private set; }
        public string SkillName => Skill == SkillKind.BambooFlash ? "죽림섬" : "회선참";

        public void LearnSkill(SkillKind skill)
        {
            Skill = skill;
            _skillElapsed = -1f;
            _skillCooldownLeft = 0f;
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
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeSkill * GaugeGainMultiplier);

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
