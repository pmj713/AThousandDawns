using AThousandDawns.Core;
using AThousandDawns.Items;

namespace AThousandDawns.Prototype
{
    // 기물 도감: 판에서 처음 얻은 기물을 meta.json에 남겨요 (회귀해도 남음). 무공 요결 창의 "기물 도감" 탭에서 봐요.
    // 기물 보상 · 객잔 · 이벤트(도박꾼 · 대장장이) · 주머니가 가득할 때 바꿔 받기 모두 세요. 판 중간 저장에서 되살릴 땐 안 세요.
    public sealed partial class RoomRun
    {
        void WatchCodex()
        {
            if (Player.Items == null) return;
            Player.Items.Acquired -= OnItemAcquired;
            Player.Items.Acquired += OnItemAcquired;
        }

        void OnItemAcquired(ItemDefinition def)
        {
            var meta = MetaSave.Load();
            if (meta.seenItems.Contains(def.id)) return;
            meta.seenItems.Add(def.id);
            meta.Save();
            _record.NewItems++;
            ShowBanner($"기물 도감에 새로 올랐어요 · {def.name}");
            Notify("codex", "기물 도감: 처음 얻은 기물은 일시정지 → 무공 요결의 기물 도감에 남아요");
        }
    }
}
