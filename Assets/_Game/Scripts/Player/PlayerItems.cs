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
        public bool OnBoard;   // 경맥도에 놓였는가. 아니면 보관함 (효과·키워드·상태 개수 없음)
        public Vector2Int Cell;
        public int Qi;         // 놓인 칸의 기 (보관함이면 0)
        public int MaxQiBonus; // 놓인 칸의 한혈 (최대 기 +)
        public int MaxQi => Def.maxQi + MaxQiBonus;
    }

    // 가진 기물과 경맥도 (기물 탭 공통 규칙, 경맥도 탭):
    //   경맥도에 놓은 기물만 효과가 있어요. 기물의 기 = 놓인 칸의 기.
    //   기 < 최소 기 → 기 부족 (효과 없음, 키워드 0) · 최소 ≤ 기 ≤ 최대 → 운기 (키워드 1) · 기 > 최대 → 주화입마 (키워드 2, 효과 ×1.5 + 부작용)
    //   레벨 = (기와 최대 기 중 작은 값) − 최소 기 + 1. 비율·시간·거리 수치만 레벨당 증가와 주화입마 배율을 받아요 (#마도 4단계면 ×1.8).
    public sealed class PlayerItems : MonoBehaviour
    {
        readonly List<OwnedItem> _items = new List<OwnedItem>();
        readonly int[] _keywordCounts = new int[PlayerKeywords.KeywordCount];
        readonly int[] _qiCounts = new int[3];
        PlayerKeywords _keywords;
        MeridianBoard _board;
        float _deviationMultiplier, _madoDeviation;

        public IReadOnlyList<OwnedItem> Items => _items;
        public MeridianBoard Board => _board;
        public event Action Changed;

        public int BagCount
        {
            get
            {
                int count = 0;
                foreach (var item in _items) if (!item.OnBoard) count++;
                return count;
            }
        }

        void Awake()
        {
            if (!TryGetComponent(out _keywords)) _keywords = gameObject.AddComponent<PlayerKeywords>();
            _deviationMultiplier = GameConfig.Get("item.deviation_multiplier", 1.5f);
            _madoDeviation = GameConfig.Get("mado.stage4_deviation", 1.8f);
            _board = new MeridianBoard(
                Mathf.RoundToInt(GameConfig.Get("meridian.width", 5f)),
                Mathf.RoundToInt(GameConfig.Get("meridian.height", 4f)),
                Mathf.RoundToInt(GameConfig.Get("meridian.max_danjeon", 3f)), 0)
            {
                GihyeolQi = Mathf.RoundToInt(GameConfig.Get("meridian.gihyeol_qi", 1f)),
                HanhyeolMaxQi = Mathf.RoundToInt(GameConfig.Get("meridian.hanhyeol_max_qi", 1f)),
            };
        }

        public bool Has(string id) => Find(id) != null;

        public OwnedItem Find(string id)
        {
            foreach (var item in _items) if (item.Def.id == id) return item;
            return null;
        }

        public OwnedItem At(Vector2Int cell)
        {
            foreach (var item in _items) if (item.OnBoard && item.Cell == cell) return item;
            return null;
        }

        // 새 기물은 경맥도 빈 칸에 왼쪽 위부터 (한 줄씩 오른쪽으로) 차례로 놓여요. 가득 차면 보관함으로.
        public OwnedItem Add(ItemDefinition def)
        {
            if (def == null || Has(def.id)) return null;
            var item = new OwnedItem { Def = def };
            _items.Add(item);
            if (TryFindEmptyCell(out var cell))
            {
                item.OnBoard = true;
                item.Cell = cell;
            }
            Recount();
            return item;
        }

        public bool TryFindEmptyCell(out Vector2Int cell)
        {
            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                cell = new Vector2Int(x, y);
                if (At(cell) == null) return true;
            }
            cell = default;
            return false;
        }

        // 칸에 놓아요. 그 칸에 다른 기물이 있으면 자리를 바꾸고 (보관함에서 왔으면 그 기물이 보관함으로).
        public void Place(OwnedItem item, Vector2Int cell)
        {
            if (item == null || !_board.Contains(cell)) return;
            var other = At(cell);
            if (other == item) return;
            if (other != null)
            {
                other.OnBoard = item.OnBoard;
                other.Cell = item.Cell;
            }
            item.OnBoard = true;
            item.Cell = cell;
            Recount();
        }

        public void ToBag(OwnedItem item)
        {
            if (item == null || !item.OnBoard) return;
            item.OnBoard = false;
            Recount();
        }

        // 경맥도 확장 (녹림왕 처치 5×4 → 6×4)
        public void ExpandBoard(int width, int height)
        {
            _board.Expand(width, height);
            Recount();
        }

        // 공력 (모든 단전의 출력). PlayerController가 공력이 바뀔 때마다 알려 줘요.
        public void SetPower(int power)
        {
            _board.Power = power;
            Recount();
        }

        public bool AddDanjeon(Vector2Int cell)
        {
            if (!_board.AddDanjeon(cell)) return false;
            Recount();
            return true;
        }

        public bool RemoveDanjeon(Vector2Int cell)
        {
            if (!_board.RemoveDanjeon(cell)) return false;
            Recount();
            return true;
        }

        // 혈자리 열기 (레벨업 보상). 한 칸에 하나.
        public bool OpenAcupoint(Vector2Int cell, Acupoint kind)
        {
            if (!_board.OpenAcupoint(cell, kind)) return false;
            Recount();
            return true;
        }

        public static QiState StateOf(OwnedItem item) =>
            item.Qi < item.Def.minQi ? QiState.Lacking : item.Qi > item.MaxQi ? QiState.Deviated : QiState.Circulating;

        public static int LevelOf(OwnedItem item) =>
            item.Qi < item.Def.minQi ? 0 : Mathf.Min(item.Qi, item.MaxQi) - item.Def.minQi + 1;

        // 주화입마 효과 배율 (#마도 4단계면 ×1.5 → ×1.8)
        public float DeviationMultiplier => _keywords != null && _keywords.Count(Keyword.Mado) >= 4 ? _madoDeviation : _deviationMultiplier;

        // #정종 보조 "0개면": 경맥도에 주화입마 기물이 하나도 없을 때
        public bool NoDeviated => _keywords == null || _keywords.Qi(QiState.Deviated) == 0;

        // 효과가 켜진 기물 (경맥도 위에서 운기·주화입마)
        public bool Active(string id, out OwnedItem item)
        {
            item = Find(id);
            return item != null && item.OnBoard && LevelOf(item) > 0;
        }

        public bool Active(string id) => Active(id, out _);

        public bool IsDeviated(string id) => Active(id, out var item) && StateOf(item) == QiState.Deviated;

        // vN 값. 효과가 꺼져 있으면 0. scales면 × 레벨 × (주화입마면 배율)
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

        // 경맥도 위 주화입마 기물의 부작용 합 (부작용은 체력을 직접 깎지 않음)
        public float SideTotal(string kind)
        {
            float sum = 0f;
            foreach (var item in _items)
                if (item.OnBoard && StateOf(item) == QiState.Deviated && item.Def.side == kind) sum += item.Def.sideValue;
            return sum;
        }

        void Recount()
        {
            Array.Clear(_keywordCounts, 0, _keywordCounts.Length);
            Array.Clear(_qiCounts, 0, _qiCounts.Length);
            foreach (var item in _items)
            {
                item.Qi = item.OnBoard ? _board.QiAt(item.Cell) : 0;
                item.MaxQiBonus = item.OnBoard ? _board.MaxQiBonusAt(item.Cell) : 0;
                if (!item.OnBoard) continue;
                var state = StateOf(item);
                _qiCounts[(int)state]++;
                _keywordCounts[(int)item.Def.keyword] += state == QiState.Lacking ? 0 : state == QiState.Circulating ? 1 : 2;
            }
            if (_keywords != null) _keywords.SetFromItems(_keywordCounts, _qiCounts);
            Changed?.Invoke();
        }
    }
}
