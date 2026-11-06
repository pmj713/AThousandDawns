using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Combat
{
    // 연쇄 번개 (#뇌정, 상태이상 탭). 파생 피해: 강인함 무시, 방어력 적용, 경직 없음, 적중 횟수에 세지 않음.
    // 1명당 발동 공격의 25% (6단계 40%), 처음 맞은 적 + 3번 튐 (대상 4명), 튀는 거리 3칸, 감전된 적을 먼저 노림.
    // 감전된 적을 맞히면 중첩을 모두 써서 방전 피해를 그 번개에 더해 숫자 하나로 보여요.
    // 응집: 튈 새 적이 없으면 남은 횟수가 마지막 적에게 한 번에 꽂혀요.
    public static class Lightning
    {
        static readonly Color BoltColor = new Color(1f, 0.89f, 0.36f); // #FFE45C (임시)
        static Material _material;
        static readonly List<Damageable> Hit = new List<Damageable>();

        public static void Chain(Damageable first, Vector3 from, float triggerDamage, float ratio, int targets, float range, float dischargePerStack)
        {
            if (first == null || targets <= 0) return;
            Hit.Clear();
            var current = first;
            Vector3 previous = from;
            if (first.IsDead || !first.Targetable)
            {
                // 초식에 처음 맞은 적이 쓰러졌으면 그 자리에서 가장 가까운 적부터 튀어요
                previous = first.transform.position + Vector3.up;
                Hit.Add(first);
                current = FindNext(first, range);
            }
            int remaining = targets;
            while (remaining > 0 && current != null)
            {
                Hit.Add(current);
                var next = FindNext(current, range);
                int count = next == null ? remaining : 1; // 응집

                float raw = triggerDamage * ratio * count;
                int shock = StatusEffects.For(current).ConsumeShock();
                raw += shock * dischargePerStack;
                Vector3 at = current.transform.position + Vector3.up * 1f;
                current.ApplyHit(DamageFormula.Proc(raw, 1f, current.Defense), false, shock > 0 ? PopupStyle.Burst : PopupStyle.Normal);
                Bolt(previous, at);

                previous = at;
                remaining -= count;
                current = next;
            }
        }

        static Damageable FindNext(Damageable from, float range)
        {
            Damageable best = null;
            bool bestShocked = false;
            float bestDistance = float.MaxValue;
            Vector3 p = from.transform.position;
            foreach (var d in Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d == from || d.IsDead || !d.Targetable || Hit.Contains(d)) continue;
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

        // 번개 한 줄기 (0.12초)
        static void Bolt(Vector3 a, Vector3 b)
        {
            var go = new GameObject("연쇄 번개");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.widthMultiplier = 0.12f;
            line.sharedMaterial = RuntimeMaterial.Unlit(ref _material, BoltColor);
            line.shadowCastingMode = ShadowCastingMode.Off;
            Object.Destroy(go, 0.12f);
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
