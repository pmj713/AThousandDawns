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
