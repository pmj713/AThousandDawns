using AThousandDawns.Player;

namespace AThousandDawns.Prototype
{
    // 대장간 (회귀록 무기 가지): 갈림길 "대장간" 길의 보상 방에서 기물을 녹여 주 키워드를 무기 홈에 넣어요 (짙은 길이면 두 번).
    // 기물 고르기 → 홈 고르기 (차 있으면 바뀜). 보조 키워드(#마도·#정종) 기물은 못 녹여요. 그만두면 아무것도 잃지 않아요.
    public sealed partial class RoomRun
    {
        bool _forgeUnlocked;

        public bool ForgeUnlocked => _forgeUnlocked;

        // 테스트·시험 키용
        public void SetForgeUnlocked(bool unlocked) => _forgeUnlocked = unlocked;

        public void OpenForge(int count)
        {
            if (count <= 0 || Player.Items == null) return;
            var items = Player.Items;
            var v = new EventView
            {
                Id = "forge",
                Title = count > 1 ? $"대장간 (남은 {count}번)" : "대장간",
                Text = $"\"녹일 물건을 주시오. 검에 그 기운을 새겨 드리리다.\"\n무기 홈: {SlotText(0)} · {SlotText(1)}",
            };
            foreach (var item in items.Items)
            {
                var it = item;
                var k = it.Def.keyword;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{it.Def.name} 녹이기 · {PlayerKeywords.Label(k)}{(it.OnBoard ? "" : " (주머니)")}",
                    Locked = PlayerItems.CanFuse(it) ? null : "보조 키워드는 못 넣어요",
                    Act = () => { OpenForgeSlot(it, count); return null; },
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => null });
            OpenEventView(v, null);
        }

        void OpenForgeSlot(OwnedItem item, int count)
        {
            var k = item.Def.keyword;
            var v = new EventView
            {
                Id = "forge_slot",
                Title = $"{item.Def.name} · {PlayerKeywords.Label(k)}를 어느 홈에?",
                Text = $"{(Player.UsesDagger ? "비도" : "검")} · 기본: {ForgeEffect(k, false, Player.UsesDagger)}\n강화판 (두 홈 모두 {PlayerKeywords.Label(k)}): {ForgeEffect(k, true, Player.UsesDagger)}\n<size=13>(무기를 바꾸면 그 무기의 효과로 바뀌어요)</size>",
            };
            for (int i = 0; i < PlayerItems.ForgeSlotCount; i++)
            {
                int slot = i;
                var old = Player.Items.ForgeSlot(slot);
                v.Choices.Add(new EventChoice
                {
                    Label = $"{slot + 1}번 홈 · {(old.HasValue ? $"{PlayerKeywords.Label(old.Value)} → {PlayerKeywords.Label(k)} 바꾸기" : "비어 있음")}",
                    Act = () =>
                    {
                        if (!AThousandDawns.Core.GameSettings.Current.confirmDialogs) return Melt(item, slot);
                        OpenForgeConfirm(item, slot, count); // 설정 "확인 창": 녹이기 전에 묻기
                        return null;
                    },
                });
            }
            v.Choices.Add(new EventChoice { Label = "뒤로", Act = () => { OpenForge(count); return null; } });
            OpenEventView(v, count > 1 ? () => OpenForge(count - 1) : (System.Action)null);
        }

        void OpenForgeConfirm(OwnedItem item, int slot, int count)
        {
            var v = new EventView
            {
                Id = "forge_confirm",
                Title = $"{item.Def.name}을(를) 녹일까요?",
                Text = "녹인 기물은 돌아오지 않아요.",
            };
            v.Choices.Add(new EventChoice { Label = $"녹인다 · {slot + 1}번 홈에 {PlayerKeywords.Label(item.Def.keyword)}", Act = () => Melt(item, slot) });
            v.Choices.Add(new EventChoice { Label = "뒤로", Act = () => { OpenForgeSlot(item, count); return null; } });
            OpenEventView(v, count > 1 ? () => OpenForge(count - 1) : (System.Action)null);
        }

        string Melt(OwnedItem item, int slot)
        {
            var k = item.Def.keyword;
            Player.Items.Fuse(item, slot);
            bool strong = Player.Items.ForgeLevel(k) >= 2;
            return $"{item.Def.name}이(가) 녹아 무기에 {PlayerKeywords.Label(k)}이(가) 새겨졌어요{(strong ? " · 강화판!" : "")}.\n{ForgeEffect(k, strong, Player.UsesDagger)}";
        }

        string SlotText(int slot)
        {
            var k = Player.Items.ForgeSlot(slot);
            return k.HasValue ? PlayerKeywords.Label(k.Value) : "빈 홈";
        }

        public static string ForgeEffect(Keyword k, bool strong, bool dagger) => dagger ? DaggerForgeEffect(k, strong) : ForgeEffect(k, strong);

        // 비도 융합 (무기 시트 5번 표). 강화판은 기본 효과에 더해져요.
        public static string DaggerForgeEffect(Keyword k, bool strong) => k switch
        {
            Keyword.Yeolyang => "비도가 꽂혀 있는 적은 1초마다 화상 1중첩" + (strong ? " + 꽂힌 비도 2개마다 화상 최대 중첩 +1 (최대 +3)" : ""),
            Keyword.Bingbaek => "비도가 꽂혀 있는 동안 둔화가 풀리지 않음" + (strong ? " + 회수할 때 꽂혀 있던 적에게 둔화 2중첩" : ""),
            Keyword.Noejeong => "비도가 꽂힐 때마다 감전 1중첩" + (strong ? " + 꽂힌 비도 1개당 감전 최대 중첩 +1 (최대 +5)" : ""),
            Keyword.Ganggi => strong ? "비도가 2명 관통, 관통할 때마다 피해 +15%" : "비도가 1명 관통해 다음 적에게 꽂힘",
            Keyword.Gyeonggong => strong ? "대시할 때 반경 6칸 안 비도를 모아 앞쪽 적에게 다시 던짐 (피해 +50%)" : "대시할 때 반경 3칸 안 비도를 모아 앞쪽 적에게 다시 던짐",
            Keyword.Geumgang => strong ? "회수하면 돌아온 비도 1개당 보호막 최대 체력 2% (최대 15%), 5초" : "회수하면 돌아온 비도 1개당 보호막 최대 체력 1% (최대 10%), 3초",
            _ => "",
        };

        public static string ForgeEffect(Keyword k, bool strong) => k switch
        {
            Keyword.Yeolyang => strong ? "화상 피해 간격 0.5 → 0.4초 (+ 평타가 화상 시간 +1초)" : "평타로 화상 중인 적을 맞히면 화상 시간 +1초 (최대 8초)",
            Keyword.Bingbaek => strong ? "빙결된 적을 3타로 깨면 면역 −5초, 2칸 안 둔화 3중첩" : "빙결된 적을 3타로 깨면 면역 −3초, 2칸 안 둔화 2중첩",
            Keyword.Noejeong => strong ? "감전 최대인 적을 3타로 맞히면 낙뢰 3번 (3타 피해 150%)" : "감전 최대인 적을 3타로 맞히면 낙뢰 (3타 피해 150%)",
            Keyword.Ganggi => strong ? "평타 범위 +60%" : "평타 범위 +30%",
            Keyword.Gyeonggong => strong ? "대시 공격이 맞으면 대시 2회 회복" : "대시 공격이 맞으면 대시 1회 회복",
            Keyword.Geumgang => strong ? "3타 적중마다 보호막 최대 체력 3% (최대 15%)" : "3타 적중마다 보호막 최대 체력 2% (최대 10%)",
            _ => "",
        };
    }
}
