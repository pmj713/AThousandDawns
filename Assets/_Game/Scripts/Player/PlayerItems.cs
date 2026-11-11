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
        public bool OnBoard;   // 경맥도에 놓였는가. 아니면 주머니 (효과·키워드·상태 개수 없음)
        public Vector2Int Cell;
        public int Qi;         // 놓인 칸의 기 (주머니면 0)
        public int MaxQiBonus; // 놓인 칸의 한혈 (최대 기 +)
        public bool Purified;  // 참회실: 주화입마 부작용 없음
        public int MaxQi => Def.maxQi + MaxQiBonus;
    }

    // 부품 하나 (판에 놓였거나 부품 주머니에). 부품 주머니는 칸 제한이 없어요.
    public sealed class OwnedPart
    {
        public PartKind Kind;
        public bool OnBoard;
        public Vector2Int Cell;
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
        readonly List<OwnedPart> _parts = new List<OwnedPart>();
        public IReadOnlyList<OwnedPart> Parts => _parts;
        public MeridianBoard Board => _board;
        public event Action Changed;
        public event Action<ItemDefinition> Acquired; // 새 기물을 실제로 받았을 때 (기물 도감). 저장에서 되살릴 땐 안 불러요

        public int BagCount
        {
            get
            {
                int count = 0;
                foreach (var item in _items) if (!item.OnBoard) count++;
                return count;
            }
        }

        // 주머니 칸 수: −1이면 제한 없음 (샌드박스). 판 구조는 회귀록에서 연 만큼 (주머니 없음 0 → 4 → 6 → 8).
        public int BagCapacity { get; set; } = -1;
        public bool HasBag => BagCapacity != 0;
        public bool BagFull => BagCapacity >= 0 && BagCount >= BagCapacity;

        // 둘 곳이 없어 버릴 기물을 고르는 중인 새 기물 (경맥도도 주머니도 가득)
        public ItemDefinition Pending { get; private set; }
        Action<bool> _pendingResolved;

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
                HanokMaxQi = Mathf.RoundToInt(GameConfig.Get("part.hanok_max_qi", 2f)),
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

        // 새 기물은 경맥도 빈 칸에 왼쪽 위부터 (한 줄씩 오른쪽으로) 차례로 놓여요. 가득 차면 주머니로.
        // 주머니도 없거나 가득이면 null을 돌려주고 Pending에 남겨요: 가진 기물 하나를 버리고 받거나 (ResolvePending) 포기해요.
        // resolved: 결국 받았는지 (객잔은 포기하면 은자를 돌려줘요)
        public OwnedItem Add(ItemDefinition def, Action<bool> resolved = null)
        {
            if (def == null || Has(def.id)) return null;
            bool onBoard = TryFindEmptyCell(out var cell);
            if (!onBoard && BagFull)
            {
                Pending = def;
                _pendingResolved = resolved;
                Changed?.Invoke();
                return null;
            }
            var item = new OwnedItem { Def = def, OnBoard = onBoard, Cell = cell };
            _items.Add(item);
            Recount();
            Acquired?.Invoke(def);
            resolved?.Invoke(true);
            return item;
        }

        // 이미 대기 중인 새 기물에 결과 알림을 붙여요 (객잔: 포기하면 은자 돌려주기)
        public void OnPendingResolved(Action<bool> resolved) => _pendingResolved += resolved;

        // discard를 버리고 그 자리에 새 기물을 놓아요. null이면 새 기물을 포기해요.
        public OwnedItem ResolvePending(OwnedItem discard)
        {
            var def = Pending;
            var resolved = _pendingResolved;
            Pending = null;
            _pendingResolved = null;
            if (def == null) return null;
            if (discard == null || !_items.Remove(discard))
            {
                Recount();
                resolved?.Invoke(false);
                return null;
            }
            var item = new OwnedItem { Def = def, OnBoard = discard.OnBoard, Cell = discard.Cell };
            _items.Add(item);
            Recount();
            Acquired?.Invoke(def);
            resolved?.Invoke(true);
            return item;
        }

        // 판 중간 저장에서 되살릴 때: 정해진 자리에 그대로 (자동 배치 없이)
        public OwnedItem Restore(ItemDefinition def, bool onBoard, Vector2Int cell)
        {
            if (def == null || Has(def.id)) return null;
            var item = new OwnedItem { Def = def, OnBoard = onBoard && _board.Contains(cell), Cell = cell };
            _items.Add(item);
            Recount();
            return item;
        }

        // 흡성: 이번 지역 동안 모든 기물의 최대 기 −1 (이질적인 기운)
        public int GlobalMaxQiBonus { get; private set; }

        public void SetGlobalMaxQiBonus(int bonus)
        {
            GlobalMaxQiBonus = bonus;
            Recount();
        }

        // 투전판에서 잃었을 때처럼 기물을 내놓아요
        public bool Remove(OwnedItem item)
        {
            if (item == null || !_items.Remove(item)) return false;
            Recount();
            return true;
        }

        // 서툰 대장장이: 그 자리 (경맥도 칸이나 주머니) 그대로 다른 기물로 바꿔요
        public OwnedItem Replace(OwnedItem item, ItemDefinition def)
        {
            if (item == null || def == null || Has(def.id) || !_items.Contains(item)) return null;
            var next = new OwnedItem { Def = def, OnBoard = item.OnBoard, Cell = item.Cell };
            _items[_items.IndexOf(item)] = next;
            Recount();
            Acquired?.Invoke(def);
            return next;
        }

        public bool TryFindEmptyCell(out Vector2Int cell)
        {
            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                cell = new Vector2Int(x, y);
                if (At(cell) == null && _board.PartAt(cell) == PartKind.None) return true;
            }
            cell = default;
            return false;
        }

        // 칸에 놓아요. 그 칸에 다른 기물이 있으면 자리를 바꾸고 (주머니에서 왔으면 그 기물이 주머니로).
        public void Place(OwnedItem item, Vector2Int cell)
        {
            if (item == null || !_board.Contains(cell) || _board.PartAt(cell) != PartKind.None) return; // 부품 칸에는 기물을 못 놓아요
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

        // 주머니가 없거나 가득이면 false
        public bool ToBag(OwnedItem item)
        {
            if (item == null || !item.OnBoard || BagFull) return false;
            item.OnBoard = false;
            Recount();
            return true;
        }

        // ── 부품

        public OwnedPart AddPart(PartKind kind)
        {
            if (kind == PartKind.None) return null;
            var part = new OwnedPart { Kind = kind };
            _parts.Add(part);
            Changed?.Invoke();
            return part;
        }

        public OwnedPart PartAt(Vector2Int cell)
        {
            foreach (var p in _parts) if (p.OnBoard && p.Cell == cell) return p;
            return null;
        }

        // 부품을 칸에 놓아요. 기물·다른 부품·단전이 있는 칸이면 false (옮기는 중이면 원래 칸은 비워요)
        public bool PlacePart(OwnedPart part, Vector2Int cell)
        {
            if (part == null || !_board.Contains(cell) || At(cell) != null) return false;
            var other = PartAt(cell);
            if (other != null && other != part) return false;
            if (part.OnBoard) _board.ClearPart(part.Cell);
            if (!_board.SetPart(cell, part.Kind))
            {
                if (part.OnBoard) _board.SetPart(part.Cell, part.Kind); // 되돌리기
                return false;
            }
            part.OnBoard = true;
            part.Cell = cell;
            Recount();
            return true;
        }

        public void RemovePart(OwnedPart part)
        {
            if (part == null || !part.OnBoard) return;
            _board.ClearPart(part.Cell);
            part.OnBoard = false;
            Recount();
        }

        public int CountParts(PartKind kind, bool onlyLoose = false)
        {
            int n = 0;
            foreach (var p in _parts) if (p.Kind == kind && (!onlyLoose || !p.OnBoard)) n++;
            return n;
        }

        public static string PartLabel(PartKind kind) => kind switch
        {
            PartKind.Tongmaek => "통맥",
            PartKind.Jeomhyeol => "점혈",
            PartKind.Hanok => "한옥",
            _ => "",
        };

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

        // 판을 시작할 때 고르는 단전 수 (면벽수련 맥 20이면 2)
        public int StartDanjeons { get; set; } = 1;

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

        // 이혈 (객잔): 산 만큼 단전을 옮길 수 있어요
        public int DanjeonMoves { get; set; }

        // 단전 하나를 다른 칸으로 (판 밖·단전·부품 칸이면 false, 그대로)
        public bool MoveDanjeon(Vector2Int from, Vector2Int to)
        {
            if (DanjeonMoves <= 0 || !_board.IsDanjeon(from) || from == to) return false;
            _board.RemoveDanjeon(from);
            if (!_board.AddDanjeon(to))
            {
                _board.AddDanjeon(from);
                return false;
            }
            DanjeonMoves--;
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

        // 경맥도 위 주화입마 기물의 부작용 합 (부작용은 체력을 직접 깎지 않음 · 참회실에서 정화한 기물은 빼요)
        public float SideTotal(string kind)
        {
            float sum = 0f;
            foreach (var item in _items)
                if (item.OnBoard && !item.Purified && StateOf(item) == QiState.Deviated && item.Def.side == kind) sum += item.Def.sideValue;
            return sum;
        }

        // ── 대장간 융합 (무기 탭): 기물을 녹여 주 키워드를 무기 홈 2개 중 하나에 넣어요. 녹인 기물은 사라져요.
        //   홈마다 그 키워드 개수 +1. 두 홈이 같은 키워드면 강화판. 보조 키워드(#마도·#정종)는 못 넣어요.
        public const int ForgeSlotCount = 2;
        readonly Keyword?[] _forge = new Keyword?[ForgeSlotCount];

        public Keyword? ForgeSlot(int slot) => slot >= 0 && slot < ForgeSlotCount ? _forge[slot] : null;

        public static bool CanFuse(Keyword keyword) => keyword != Keyword.Mado && keyword != Keyword.Jeongjong;
        public static bool CanFuse(OwnedItem item) => item != null && CanFuse(item.Def.keyword);

        // 이 키워드가 들어간 홈 수 (0 · 1 · 2=강화판)
        public int ForgeLevel(Keyword keyword)
        {
            int n = 0;
            foreach (var k in _forge) if (k == keyword) n++;
            return n;
        }

        // 기물을 녹여 홈에 넣어요 (홈에 있던 키워드는 바뀌어요)
        public bool Fuse(OwnedItem item, int slot)
        {
            if (!CanFuse(item) || slot < 0 || slot >= ForgeSlotCount || !_items.Remove(item)) return false;
            _forge[slot] = item.Def.keyword;
            Recount();
            return true;
        }

        // 테스트·시험 키용
        public void SetForgeSlot(int slot, Keyword? keyword)
        {
            if (slot < 0 || slot >= ForgeSlotCount || (keyword.HasValue && !CanFuse(keyword.Value))) return;
            _forge[slot] = keyword;
            Recount();
        }

        void Recount()
        {
            Array.Clear(_keywordCounts, 0, _keywordCounts.Length);
            foreach (var k in _forge) if (k.HasValue) _keywordCounts[(int)k.Value]++;
            Array.Clear(_qiCounts, 0, _qiCounts.Length);
            foreach (var item in _items)
            {
                item.Qi = item.OnBoard ? _board.QiAt(item.Cell) : 0;
                item.MaxQiBonus = (item.OnBoard ? _board.MaxQiBonusAt(item.Cell) : 0) + GlobalMaxQiBonus;
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
