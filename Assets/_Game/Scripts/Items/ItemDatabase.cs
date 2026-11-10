using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Items
{
    // 시트 data_items → CSV → 이 에셋 (AThousandDawns/시트 가져오기). GameConfig가 연결해 둬요.
    [CreateAssetMenu(menuName = "AThousandDawns/Item Database", fileName = "ItemDatabase")]
    public sealed class ItemDatabase : ScriptableObject
    {
        [SerializeField] List<ItemDefinition> items = new List<ItemDefinition>();

        Dictionary<string, ItemDefinition> _byId;

        public IReadOnlyList<ItemDefinition> Items => items;

        // 회귀록 견문 가지 "새 기물 묶음 1·2"로 풀리는 기물. 피우기 전엔 보상·객잔·이벤트에 나오지 않아요.
        public static readonly string[] Pack1 = { "ash_urn", "frost_bead", "lightning_rod", "finishing_edge", "iron_vest", "shadow_step" };
        public static readonly string[] Pack2 = { "charcoal_bag", "thunder_glove", "frost_armor", "guard_talisman", "sword_scroll", "cloud_boots" };

        // 판을 시작할 때 RoomRun이 정해요 (회귀록). 시험장은 다 나와요.
        public static int UnlockedPacks = 2;

        public static int PackOf(string id) =>
            System.Array.IndexOf(Pack1, id) >= 0 ? 1 : System.Array.IndexOf(Pack2, id) >= 0 ? 2 : 0;

        // 보상·객잔·이벤트에서 뽑을 수 있는 기물 (풀린 꾸러미까지). 저장에서 되살릴 땐 Items·Find를 써요.
        public List<ItemDefinition> Available
        {
            get
            {
                var list = new List<ItemDefinition>();
                foreach (var item in items)
                    if (item != null && PackOf(item.id) <= UnlockedPacks) list.Add(item);
                return list;
            }
        }

        public void SetItems(IEnumerable<ItemDefinition> newItems)
        {
            items = new List<ItemDefinition>(newItems);
            _byId = null;
        }

        public ItemDefinition Find(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, ItemDefinition>();
                foreach (var item in items) _byId[item.id] = item;
            }
            return _byId.TryGetValue(id, out var def) ? def : null;
        }

        void OnValidate() => _byId = null;
    }
}
