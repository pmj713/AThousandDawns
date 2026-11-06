using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 피해 숫자 모양 (상태이상 탭 표시 규칙: 화상은 작고 흐리게, 방전은 크고 굵게)
    public enum PopupStyle { Normal, Player, Dot, Burst }

    // 시험판용 피해 숫자. 나중에 월드 UI로 바꿀 자리예요.
    public static class DamagePopups
    {
        public struct Popup
        {
            public Vector3 Position;
            public int Amount;
            public float Age;
            public PopupStyle Style;
            public bool OnPlayer => Style == PopupStyle.Player; // 플레이어가 받은 피해 (빨간 숫자)
        }

        public const float Lifetime = 0.8f;
        static readonly List<Popup> Active = new List<Popup>();

        public static IReadOnlyList<Popup> Current => Active;

        public static void Spawn(Vector3 position, int amount, PopupStyle style = PopupStyle.Normal)
        {
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.2f, 0.2f));
            Active.Add(new Popup { Position = position + jitter, Amount = amount, Style = style });
        }

        public static void Tick(float dt)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var p = Active[i];
                p.Age += dt;
                p.Position += Vector3.up * dt * 1.2f;
                if (p.Age >= Lifetime) Active.RemoveAt(i);
                else Active[i] = p;
            }
        }
    }
}
