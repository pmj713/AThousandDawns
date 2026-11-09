using System.Collections.Generic;
using AThousandDawns.Player;

namespace AThousandDawns.Prototype
{
    // 부품 얻기 (회귀록 경맥 가지에서 연 종류만): 갈림길 "부품" 길의 보상 방에서 고르기 (짙은 길이면 둘), 객잔 진열 1개 (은자 100).
    // 놓는 곳은 경맥도 (MeridianScreen 부품 줄).
    public sealed partial class RoomRun
    {
        List<PartKind> _unlockedParts = new List<PartKind>();

        public IReadOnlyList<PartKind> UnlockedParts => _unlockedParts;

        string PartNames()
        {
            var names = new List<string>();
            foreach (var k in _unlockedParts) names.Add(PlayerItems.PartLabel(k));
            return string.Join("·", names);
        }

        // 해금한 부품 중 하나 고르기. count번 이어서 골라요.
        void OpenPartChoice(int count)
        {
            if (count <= 0 || _unlockedParts.Count == 0) return;
            var v = new EventView
            {
                Id = "part",
                Title = count > 1 ? $"부품 고르기 (남은 {count}개)" : "부품 고르기",
                Text = "경맥도에서 부품 줄을 골라 빈 칸에 놓아요 (E).",
            };
            foreach (var kind in _unlockedParts)
            {
                var k = kind;
                string hint = k == PartKind.Tongmaek ? "지나는 기가 줄지 않음" : k == PartKind.Jeomhyeol ? "기를 막음" : "붙은 기물 최대 기 +2";
                v.Choices.Add(new EventChoice
                {
                    Label = $"{PlayerItems.PartLabel(k)} · {hint} (가진 것 {Player.Items.CountParts(k)})",
                    Act = () => { Player.Items.AddPart(k); return $"{PlayerItems.PartLabel(k)}을(를) 얻었어요."; },
                });
            }
            OpenEventView(v, count > 1 ? () => OpenPartChoice(count - 1) : (System.Action)null);
        }

        // 테스트·시험 키용
        public void SetUnlockedParts(params PartKind[] kinds) => _unlockedParts = new List<PartKind>(kinds);
    }
}
