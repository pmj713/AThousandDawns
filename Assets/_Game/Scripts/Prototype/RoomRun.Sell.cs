using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AThousandDawns.Prototype
{
    // 매각 (은자 시트 C33): 객잔 진열대 매각 칸 → 가진 기물 목록에서 골라 판매가의 40%에 팔아요.
    // 떠돌이 상인 (이벤트): 상인이 고른 기물 하나만 판매가의 60%에 사 가요.
    public sealed partial class RoomRun
    {
        public bool IsSelling { get; private set; }

        public static int SellPrice(ItemDefinition def) => InnShop.SellPrice(def);

        public void OpenSell()
        {
            if (!IsShopping || Player.Items.Items.Count == 0) return;
            IsSelling = true;
            ShopNotice = null;
            Notify("sell", "매각: 가진 기물을 객잔 판매가의 40%에 팔아요");
        }

        public void CloseSell()
        {
            IsSelling = false;
            ShopNotice = null;
        }

        public int SellCursor { get; private set; } // 게임패드로 고른 팔 기물

        // 1~9로 팔기, F·Esc 진열대로 돌아가기
        void TickSellKeys(Keyboard keyboard)
        {
            SellCursor = Pad.Step(SellCursor, Player.Items.Items.Count); // 게임패드: 위아래 · A 팔기 · B 돌아가기
            if (Pad.Confirm && Player.Items.Items.Count > 0) { Sell(UnityEngine.Mathf.Min(SellCursor, Player.Items.Items.Count - 1)); return; }
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || KeyBindings.Pressed(Bind.Interact))
            {
                CloseSell();
                return;
            }
            if (keyboard == null) return;
            for (int i = 0; i < Player.Items.Items.Count && i < 9; i++)
                if (((KeyControl)keyboard[(Key)((int)Key.Digit1 + i)]).wasPressedThisFrame)
                {
                    Sell(i);
                    return;
                }
        }

        // 가진 기물 index번째를 팔아요. 다 팔면 진열대로 돌아가요.
        public bool Sell(int index)
        {
            var items = Player.Items.Items;
            if (!IsSelling || index < 0 || index >= items.Count) return false;
            var item = items[index];
            int price = SellPrice(item.Def);
            if (!Player.Items.Remove(item)) return false;
            Silver += price;
            ShopNotice = $"{item.Def.name}을(를) 팔았어요 · 은자 +{price}";
            if (items.Count == 0) IsSelling = false;
            return true;
        }

        // ── 떠돌이 상인

        void BuildPeddler(EventView v)
        {
            v.Title = "떠돌이 상인";
            v.Text = "등짐을 진 상인이 기물을 훑어보더니 눈을 반짝인다. \"그거 하나만 넘기시오. 값은 후하게 쳐 드리리다.\"";
            var items = Player.Items.Items;
            if (items.Count > 0)
            {
                var item = items[_rng.Next(items.Count)];
                int price = (int)System.Math.Round(InnShop.ItemPrice(item.Def.grade) * GameConfig.GetOptional("event.peddler_ratio", 0.6f));
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name}({ItemDefinition.GradeLabel(item.Def.grade)})을(를) 판다 · 은자 +{price}",
                    Act = () =>
                    {
                        if (!Player.Items.Remove(item)) return "상인이 고개를 갸웃해요. 그 기물이 없어요.";
                        Silver += price;
                        v.Text = "상인이 흡족하게 웃으며 기물을 등짐 깊숙이 넣는다. \"좋은 거래였소. 또 봅시다.\""; // 고른 뒤엔 상인 대사도 바뀌어요
                        return $"상인이 {item.Def.name}을(를) 챙기고 은자 {price}를 세어 줬어요.";
                    },
                });
            }
            else v.Choices.Add(new EventChoice { Label = "팔 기물이 없다", Locked = "가진 기물이 없어요", Act = () => null });
            v.Choices.Add(new EventChoice
            {
                Label = "거절한다",
                Act = () =>
                {
                    v.Text = "상인이 아쉬운 듯 혀를 차더니 등짐을 고쳐 멘다. \"마음이 바뀌면 다음 길목에서 찾으시오.\"";
                    return "상인이 길을 떠났어요.";
                },
            });
        }
    }
}
