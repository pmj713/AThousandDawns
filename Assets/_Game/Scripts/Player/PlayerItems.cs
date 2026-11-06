using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using UnityEngine;

namespace AThousandDawns.Player
{
    public sealed class OwnedItem
    {
        public ItemDefinition Def;
        public int Qi; // 경맥도가 생기기 전까지는 기물마다 기 값을 직접 가져요 (시험판: ←/→)
    }

    // 가진 기물 (기물 탭 공통 규칙):
    //   기 < 최소 기 → 기 부족 (효과 없음, 키워드 0) · 최소 ≤ 기 ≤ 최대 → 운기 (키워드 1) · 기 > 최대 → 주화입마 (키워드 2, 효과 ×1.5 + 부작용)
    //   레벨 = (기와 최대 기 중 작은 값) − 최소 기 + 1. 비율·시간·거리 수치만 레벨당 증가와 주화입마 배율을 받아요 (#마도 4단계면 ×1.8).
    public sealed class PlayerItems : MonoBehaviour
    {
        public const int QiOverflow = 2; // 시험판: 최대 기보다 이만큼 더 올릴 수 있어요 (주화입마 시험용)

        readonly List<OwnedItem> _items = new List<OwnedItem>();
        readonly int[] _keywordCounts = new int[PlayerKeywords.KeywordCount];
        readonly int[] _qiCounts = new int[3];
        PlayerKeywords _keywords;
        float _deviationMultiplier, _madoDeviation;

        public IReadOnlyList<OwnedItem> Items => _items;
        public int Selected { get; private set; }
        public event Action Changed;

        void Awake()
        {
            if (!TryGetComponent(out _keywords)) _keywords = gameObject.AddComponent<PlayerKeywords>();
            _deviationMultiplier = GameConfig.Get("item.deviation_multiplier", 1.5f);
            _madoDeviation = GameConfig.Get("mado.stage4_deviation", 1.8f);
        }

        public bool Has(string id) => Find(id) != null;

        public OwnedItem Find(string id)
        {
            foreach (var item in _items) if (item.Def.id == id) return item;
            return null;
        }

        // 새 기물은 최소 기에서 시작 (운기 1레벨)
        public void Add(ItemDefinition def)
        {
            if (def == null || Has(def.id)) return;
            _items.Add(new OwnedItem { Def = def, Qi = def.minQi });
            Selected = _items.Count - 1;
            Recount();
        }

        public void Select(int delta)
        {
            if (_items.Count == 0) return;
            Selected = (Selected + delta + _items.Count) % _items.Count;
            Changed?.Invoke();
        }

        public void AdjustSelectedQi(int delta)
        {
            if (Selected < 0 || Selected >= _items.Count) return;
            var item = _items[Selected];
            item.Qi = Mathf.Clamp(item.Qi + delta, 0, item.Def.maxQi + QiOverflow);
            Recount();
        }

        public static QiState StateOf(OwnedItem item) =>
            item.Qi < item.Def.minQi ? QiState.Lacking : item.Qi > item.Def.maxQi ? QiState.Deviated : QiState.Circulating;

        public static int LevelOf(OwnedItem item) =>
            item.Qi < item.Def.minQi ? 0 : Mathf.Min(item.Qi, item.Def.maxQi) - item.Def.minQi + 1;

        // 주화입마 효과 배율 (#마도 4단계면 ×1.5 → ×1.8)
        public float DeviationMultiplier => _keywords != null && _keywords.Count(Keyword.Mado) >= 4 ? _madoDeviation : _deviationMultiplier;

        // #정종 보조 "0개면": 주화입마 기물이 하나도 없을 때
        public bool NoDeviated => _keywords == null || _keywords.Qi(QiState.Deviated) == 0;

        // 효과가 켜진 기물 (운기·주화입마)
        public bool Active(string id, out OwnedItem item)
        {
            item = Find(id);
            return item != null && LevelOf(item) > 0;
        }

        public bool Active(string id) => Active(id, out _);

        public bool IsDeviated(string id) => Active(id, out var item) && StateOf(item) == QiState.Deviated;

        // vN 값. 기 부족이면 0. scales면 × 레벨 × (주화입마면 배율)
        public float Value(string id, int index) => Value(id, index, true);

        // 주화입마 배율 대신 기물 글이 정한 배율을 쓸 때 (홍염 장갑 "주화입마면 범위 2배")
        public float Value(string id, int index, bool deviationMultiplier)
        {
            if (!Active(id, out var item)) return 0f;
            var d = item.Def;
            float value; bool scales;
            switch (index)
            {
                case 1: value = d.v1; scales = d.v1Scales; break;
                case 2: value = d.v2; scales = d.v2Scales; break;
                case 3: value = d.v3; scales = d.v3Scales; break;
                default: value = d.v4; scales = d.v4Scales; break;
            }
            if (!scales) return value;
            bool deviated = deviationMultiplier && StateOf(item) == QiState.Deviated;
            return value * LevelOf(item) * (deviated ? DeviationMultiplier : 1f);
        }

        public int Count(string id, int index) => Mathf.RoundToInt(Value(id, index));

        // 주화입마 부작용 합 (부작용은 체력을 직접 깎지 않음)
        public float SideTotal(string kind)
        {
            float sum = 0f;
            foreach (var item in _items)
                if (StateOf(item) == QiState.Deviated && item.Def.side == kind) sum += item.Def.sideValue;
            return sum;
        }

        public bool HasSide(string kind) => SideTotal(kind) > 0f;

        void Recount()
        {
            Array.Clear(_keywordCounts, 0, _keywordCounts.Length);
            Array.Clear(_qiCounts, 0, _qiCounts.Length);
            foreach (var item in _items)
            {
                var state = StateOf(item);
                _qiCounts[(int)state]++;
                _keywordCounts[(int)item.Def.keyword] += state == QiState.Lacking ? 0 : state == QiState.Circulating ? 1 : 2;
            }
            if (_keywords != null) _keywords.SetFromItems(_keywordCounts, _qiCounts);
            Changed?.Invoke();
        }
    }
}
