using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 공격에 닿으면 깨지는 물건 (항아리 등). 적이 아니라서 피해 숫자·처치 효과·수련치가 없어요.
    // 플레이어 공격이 닿는 자리를 알려 주면 그 안의 물건이 깨져요 (평타·초식 부채꼴, 비도가 지나가는 자리, 회수 길).
    public sealed class Breakable : MonoBehaviour
    {
        static readonly List<Breakable> All = new List<Breakable>();
        static readonly List<Breakable> Hit = new List<Breakable>();

        public float Radius = 0.35f;
        public event Action<Breakable> Broken;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Break() => Broken?.Invoke(this);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // 부채꼴 (평타·초식): 깬 수
        public static int BreakInSector(Vector3 origin, Vector3 forward, float radius, float angle) =>
            BreakWhere(b => HitShapes.InSector(origin, forward, radius, angle, b.transform.position, b.Radius));

        // 한 점 둘레 (날아가는 비도, 떨어지는 비도)
        public static int BreakNear(Vector3 point, float radius) =>
            BreakWhere(b => Vector3.Distance(Flat(b.transform.position), Flat(point)) <= radius + b.Radius);

        // 선분 둘레 (회수로 돌아오는 비도)
        public static int BreakOnSegment(Vector3 from, Vector3 to, float halfWidth) =>
            BreakWhere(b =>
            {
                Vector3 a = Flat(from), c = Flat(to), p = Flat(b.transform.position);
                Vector3 ab = c - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                return Vector3.Distance(p, a + ab * t) <= halfWidth + b.Radius;
            });

        static int BreakWhere(Func<Breakable, bool> inside)
        {
            Hit.Clear();
            foreach (var b in All) if (b != null && inside(b)) Hit.Add(b);
            foreach (var b in Hit) b.Break(); // 깨지면 목록에서 빠지니 모아 둔 뒤에 깨요
            return Hit.Count;
        }
    }
}
