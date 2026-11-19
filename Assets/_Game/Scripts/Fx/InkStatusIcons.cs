using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 상태이상 아이콘 줄 (사용자 요청): 체력바 옆에 상태 아이콘을 나란히, 아이콘 오른쪽 아래에 스택 숫자 (스택이 없으면 남은 초).
    //   적 (머리 위 바 · 고수 바 · 보스 바): 화상 · 둔화 · 감전 = 스택, 빙결 = 남은 초 (StatusEffects). 가호는 체력바 흰 코팅.
    //   플레이어 (InkHud 체력 획 옆): 화상 = 스택, 둔화 · 묶임 · 독 · 저주 = 남은 초 (PlayerController). 묶임이면 둔화 대신 사슬.
    //   그림: Resources/StatusIcons/<Burn · Freeze · Shock · Slow · Poison · Bound · Curse> (먹붓 아이콘, InkUi 톤). 없으면 색 동그라미로 대신해요.
    public static class InkStatusIcons
    {
        public enum Kind { Burn, Freeze, Shock, Slow, Poison, Bound, Curse }

        static readonly Color[] Fallback =
        {
            new Color(1f, 0.45f, 0.15f), new Color(0.6f, 0.9f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.5f, 0.6f, 0.85f),
            new Color(0.45f, 0.9f, 0.3f), new Color(0.6f, 0.6f, 0.65f), new Color(0.65f, 0.3f, 0.9f),
        };

        static Texture2D[] _icons;
        static readonly List<(Kind kind, string corner)> Items = new List<(Kind, string)>();

        static Texture2D Icon(Kind k)
        {
            if (_icons == null)
            {
                _icons = new Texture2D[Fallback.Length];
                for (int i = 0; i < _icons.Length; i++) _icons[i] = Resources.Load<Texture2D>("StatusIcons/" + (Kind)i);
            }
            return _icons[(int)k];
        }

        // 적: 체력바 오른쪽에 (bar = 그 체력바 칸). 아무 상태도 없으면 그리지 않아요
        public static void Enemy(Rect bar, StatusEffects s, float size)
        {
            if (s == null) return;
            Items.Clear();
            if (s.BurnStacks > 0) Items.Add((Kind.Burn, s.BurnStacks.ToString()));
            if (s.IsFrozen) Items.Add((Kind.Freeze, Seconds(s.FreezeLeft)));
            if (s.ShockStacks > 0) Items.Add((Kind.Shock, s.ShockStacks.ToString()));
            if (s.SlowStacks > 0) Items.Add((Kind.Slow, s.SlowStacks.ToString()));
            Row(bar.xMax + 4f, bar.center.y, size);
        }

        // 플레이어: 체력 획 오른쪽에
        public static void Player(Rect bar, PlayerController p, float size)
        {
            if (p == null) return;
            Items.Clear();
            if (p.BurnStacks > 0) Items.Add((Kind.Burn, p.BurnStacks.ToString()));
            if (p.IsBound) Items.Add((Kind.Bound, Seconds(p.SlowLeft)));
            else if (p.SlowRatio > 0f) Items.Add((Kind.Slow, Seconds(p.SlowLeft)));
            if (p.PoisonLeft > 0f) Items.Add((Kind.Poison, Seconds(p.PoisonLeft)));
            if (p.IsCursed) Items.Add((Kind.Curse, Seconds(p.CurseTime)));
            Row(bar.xMax + 8f, bar.center.y, size);
        }

        static string Seconds(float s) => s >= 10f ? Mathf.CeilToInt(s).ToString() : s.ToString("0.0");

        static void Row(float x, float centerY, float size)
        {
            if (Items.Count == 0) return;
            var old = GUI.color;
            var number = InkUi.Text(size >= 26f ? InkUi.Small : InkUi.Tiny, InkUi.PaperText, TextAnchor.LowerRight, true);
            var shadow = InkUi.Text(size >= 26f ? InkUi.Small : InkUi.Tiny, new Color(0f, 0f, 0f, 0.85f), TextAnchor.LowerRight, true);
            foreach (var (kind, corner) in Items)
            {
                var r = new Rect(x, centerY - size * 0.5f, size, size);
                var icon = Icon(kind);
                if (icon != null)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.45f); // 어느 바탕에서도 보이게 뒤에 옅은 먹 그림자
                    GUI.DrawTexture(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                    GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.color = Fallback[(int)kind];
                    GUI.DrawTexture(new Rect(r.x + size * 0.2f, r.y + size * 0.2f, size * 0.6f, size * 0.6f), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                var corner_ = new Rect(r.x, r.y, r.width + size * 0.35f, r.height + size * 0.2f); // 오른쪽 아래 모서리에 걸치게
                GUI.Label(new Rect(corner_.x + 1f, corner_.y + 1f, corner_.width, corner_.height), corner, shadow);
                GUI.Label(corner_, corner, number);
                x += size * 1.25f;
            }
            GUI.color = old;
        }
    }
}
