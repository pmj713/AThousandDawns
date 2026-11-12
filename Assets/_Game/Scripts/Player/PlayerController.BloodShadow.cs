using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 혈영참 (혈영검마 비급): 잔상을 남기는 초식.
    //   검: 판정 순간 앞으로 4칸 돌진하며 직선 (폭 1.2칸)을 베요 (회선참 피해 ×0.8). 출발 자리에 잔상이 남아 0.4초 뒤 같은 길을 다시 베요 (×0.5).
    //   비도 (혈영 잔상): 꽂히거나 떨어진 비도마다 (최대 8개) 그 자리에 잔상이 나타나 0.3초 뒤 둘레 1.5칸을 베요 (1개당 ×0.3). 비도는 회수처럼 손으로 돌아와요 (회수 피해도 그대로).
    public sealed partial class PlayerController
    {
        struct ShadowEcho
        {
            public Vector3 From, To;
            public float Radius, Timer, Ratio;
            public bool Circle;
        }

        readonly List<ShadowEcho> _shadowEchoes = new List<ShadowEcho>();

        public int ShadowEchoesPending => _shadowEchoes.Count;
        public int ShadowEchoesMade { get; private set; } // 지금까지 생긴 잔상 수 (느린 프레임에서도 셀 수 있게)

        static float BS(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        // 검: 돌진 베기 + 잔상
        void TickBloodShadow()
        {
            if (_skillHitDone || _skillElapsed < _skillHitTime) return;
            _skillHitDone = true;
            Sfx.Play(SfxId.BambooFlash); // 전용 소리가 생기기 전까지 죽림섬 소리
            Vector3 from = transform.position;
            _controller.Move(_facing * BS("skill_bloodshadow.length", 4f)); // 벽에는 막혀요
            Vector3 to = transform.position;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, 0.15f);
            SkillFx(new SkillFxCue { Kind = SkillFxKind.ShadowCut, From = from, To = to, Dir = _facing });
            if (swingFlash != null) swingFlash.Show(to, -_facing, 1.2f, 90f, 0.12f);
            ShadowLine(from, to, BS("skill_bloodshadow.ratio", 0.8f));
            _shadowEchoes.Add(new ShadowEcho { From = from, To = to, Timer = BS("skill_bloodshadow.echo_delay", 0.4f), Ratio = BS("skill_bloodshadow.echo_ratio", 0.5f) });
        }

        // 비도: 비도 자리마다 잔상 → 회수
        void DaggerShadows()
        {
            int max = Mathf.RoundToInt(BS("knife.scroll_shadow_max", 8f)), n = 0;
            float radius = BS("knife.scroll_shadow_radius", 1.5f), delay = BS("knife.scroll_shadow_delay", 0.3f), ratio = BS("knife.scroll_shadow_ratio", 0.3f);
            foreach (var d in _daggers)
            {
                if (n >= max) break;
                if (d.Flying || d.Returning || d.Vortex) continue;
                Vector3 at = Flatten(d.Go.transform.position);
                _shadowEchoes.Add(new ShadowEcho { From = at, To = at, Radius = radius, Timer = delay, Ratio = ratio, Circle = true });
                ShadowEchoesMade++;
                n++;
            }
            RecallDaggers();
        }

        void TickShadowEchoes(float dt)
        {
            for (int i = _shadowEchoes.Count - 1; i >= 0; i--)
            {
                var e = _shadowEchoes[i];
                e.Timer -= dt;
                if (e.Timer > 0f)
                {
                    _shadowEchoes[i] = e;
                    continue;
                }
                _shadowEchoes.RemoveAt(i);
                Vector3 dir = Flatten(e.To - e.From);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : _facing;
                SkillFx(new SkillFxCue { Kind = SkillFxKind.ShadowCut, From = e.From, To = e.To, Dir = dir, Radius = e.Radius });
                if (e.Circle) ShadowCircle(e.From, e.Radius, e.Ratio);
                else ShadowLine(e.From, e.To, e.Ratio);
            }
        }

        void ShadowLine(Vector3 from, Vector3 to, float ratio)
        {
            var hit = PlayerHit(_skillDamage * ratio);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int count = HitLine(from, to, BS("skill_bloodshadow.width", 1.2f), hit, HitTag.Skill);
            Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);
        }

        void ShadowCircle(Vector3 at, float radius, float ratio)
        {
            if (swingFlash != null) swingFlash.Show(at, _facing, radius, 360f, 0.12f);
            var hit = PlayerHit(_skillDamage * ratio);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int hits = 0;
            foreach (var e in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (e == null || e.IsDead || !e.Targetable) continue;
                if ((Flatten(e.transform.position) - Flatten(at)).magnitude > radius + e.BodyRadius) continue;
                HitTarget(e, hit, HitTag.Skill);
                hits++;
            }
            Gauge = Mathf.Min(GaugeCap, Gauge + hits * _gaugeSkill * 0.5f * GaugeGainMultiplier);
        }
    }
}
