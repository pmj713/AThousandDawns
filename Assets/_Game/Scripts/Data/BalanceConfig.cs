using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Data
{
    // 구글 시트 data_balance 탭의 상수들. 직접 고치지 말고 시트를 고친 뒤 "AThousandDawns/시트 가져오기"를 실행하세요.
    [CreateAssetMenu(menuName = "AThousandDawns/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public float value;
            public string unit;
            public string note;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        Dictionary<string, float> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = new List<Entry>(newEntries);
            _lookup = null;
        }

        public bool TryGet(string key, out float value)
        {
            EnsureLookup();
            return _lookup.TryGetValue(key, out value);
        }

        public float Get(string key)
        {
            if (TryGet(key, out var value)) return value;
            throw new KeyNotFoundException($"BalanceConfig에 '{key}' 키가 없어요. 시트의 data_balance 탭을 확인하세요.");
        }

        public int GetInt(string key) => Mathf.RoundToInt(Get(key));

        void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var e in entries) _lookup[e.key] = e.value;
        }

        void OnValidate() => _lookup = null;
    }
}
