using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Combat
{
    // 연쇄 번개 (#뇌정, 상태이상 탭). 파생 피해: 강인함 무시, 방어력 적용, 경직 없음, 적중 횟수에 세지 않음.
    // 1명당 발동 공격의 25% (6단계 40%), 처음 맞은 적 + 3번 튐 (대상 4명), 튀는 거리 3칸, 감전된 적을 먼저 노림.
    // 감전된 적을 맞히면 중첩을 모두 써서 방전 피해를 그 번개에 더해 숫자 하나로 보여요.
    // 응집: 튈 새 적이 없으면 남은 횟수가 마지막 적에게 한 번에 꽂혀요.
    // 기물(천둥 북·뇌정 붓·뇌정 옥새·운뢰 피리·매 발톱)이 더하는 값은 ElementalMods.Current에서 읽어요.
    public static class Lightning
    {
        static readonly Color BoltColor = new Color(1f, 0.89f, 0.36f); // #FFE45C (임시)
        static Material _material;

        // 번개에 쓰러진 적이 다른 효과(구리 동전)로 또 번개를 부를 수 있어서 맞은 적 목록은 번개마다 따로 둬요.
        public static void Chain(Damageable first, Vector3 from, float triggerDamage, float ratio, int targets, float range, float dischargePerStack)
        {
            var mods = ElementalMods.Current;
            targets += mods.ChainTargetsBonus;
            if (first == null || targets <= 0) return;
            var hit = new List<Damageable>();
            var current = first;
            Vector3 previous = from;
            if (first.IsDead || !first.Targetable)
            {
                // 처음 맞은 적이 쓰러졌으면 그 자리에서 가장 가까운 적부터 튀어요
                previous = first.transform.position + Vector3.up;
                hit.Add(first);
                current = FindNext(first, range, hit);
            }
            int remaining = targets;
            while (remaining > 0 && current != null)
            {
                hit.Add(current);
                var next = FindNext(current, range, hit);
                int count = next == null ? remaining : 1; // 응집

                var status = StatusEffects.For(current);
                float discharge = Discharge(status, dischargePerStack, count > 1);
                float raw = triggerDamage * ratio * count * (1f + mods.ChainDamageBonus) + discharge;
                Vector3 at = current.transform.position + Vector3.up * 1f;
                current.ApplyHit(DamageFormula.Proc(raw, 1f, current.Defense), false, discharge > 0f ? PopupStyle.Burst : PopupStyle.Normal);
                if (!current.IsDead) status.AddShock(mods.Reshock); // 매 발톱 주화입마: 방전 뒤 감전을 남김
                if (discharge > 0f) Splash(current, discharge);
                Bolt(previous, at, 0.12f);

                previous = at;
                remaining -= count;
                current = next;
            }
        }

        // 낙뢰 (상태이상 탭): 발동 공격 × 150%. 감전된 적이면 중첩을 모두 써서 방전 피해를 더해요.
        public static void Strike(Damageable target, float triggerDamage, float ratio, float dischargePerStack)
        {
            if (target == null || target.IsDead || !target.Targetable) return;
            float discharge = Discharge(StatusEffects.For(target), dischargePerStack, false);
            float raw = triggerDamage * ratio + discharge;
            target.ApplyHit(DamageFormula.Proc(raw, 1f, target.Defense), false, discharge > 0f ? PopupStyle.Burst : PopupStyle.Normal);
            if (discharge > 0f) Splash(target, discharge);
            Vector3 at = target.transform.position + Vector3.up;
            Bolt(at + Vector3.up * 6f, at, 0.2f);
        }

        // 방전 피해 (강인함·방어력 빼기 전). 감전 중첩을 모두 써요.
        //   뇌정 옥새: 방전 피해 +%, 응집된 번개면 중첩을 2배로 셈 · 뇌정 붓: 최대 중첩이었으면 +%
        static float Discharge(StatusEffects status, float perStack, bool cohesion)
        {
            int stacks = status.ConsumeShock();
            if (stacks <= 0) return 0f;
            var mods = ElementalMods.Current;
            float counted = stacks * (cohesion ? mods.CohesionShockMultiplier : 1f);
            float bonus = mods.DischargeBonus + (stacks >= status.ShockMax ? mods.DischargeMaxBonus : 0f);
            return counted * perStack * (1f + bonus);
        }

        // 운뢰 피리: 방전 피해의 일부가 주변 적에게 퍼져요 (퍼진 피해는 다시 퍼지지 않음)
        static void Splash(Damageable center, float discharge)
        {
            var mods = ElementalMods.Current;
            if (mods.SplashRatio <= 0f || mods.SplashRadius <= 0f) return;
            Vector3 p = center.transform.position;
            foreach (var d in Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d == center || d.IsDead || !d.Targetable) continue;
                Vector3 offset = d.transform.position - p;
                offset.y = 0f;
                if (offset.magnitude > mods.SplashRadius + d.BodyRadius) continue;
                d.ApplyHit(DamageFormula.Proc(discharge, mods.SplashRatio, d.Defense), false);
            }
        }

        static Damageable FindNext(Damageable from, float range, List<Damageable> hit)
        {
            Damageable best = null;
            bool bestShocked = false;
            float bestDistance = float.MaxValue;
            Vector3 p = from.transform.position;
            foreach (var d in Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d == from || d.IsDead || !d.Targetable || hit.Contains(d)) continue;
                Vector3 offset = d.transform.position - p;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > range + d.BodyRadius) continue;
                bool shocked = d.TryGetComponent(out StatusEffects s) && s.ShockStacks > 0;
                // 감전된 적이 먼저, 같으면 가까운 적
                if (best == null || (shocked && !bestShocked) || (shocked == bestShocked && distance < bestDistance))
                {
                    best = d;
                    bestShocked = shocked;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // 번개 한 줄기
        static void Bolt(Vector3 a, Vector3 b, float life)
        {
            var go = new GameObject("번개");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.widthMultiplier = 0.12f;
            line.sharedMaterial = RuntimeMaterial.Unlit(ref _material, BoltColor);
            line.shadowCastingMode = ShadowCastingMode.Off;
            Object.Destroy(go, life);
        }
    }

    // 시험판용 단색 머티리얼 (씬 빌더가 만들지 않는 효과용)
    public static class RuntimeMaterial
    {
        public static Material Unlit(ref Material cache, Color color)
        {
            if (cache != null) return cache;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            cache = new Material(shader);
            if (cache.HasProperty("_BaseColor")) cache.SetColor("_BaseColor", color);
            cache.color = color;
            return cache;
        }
    }
}
