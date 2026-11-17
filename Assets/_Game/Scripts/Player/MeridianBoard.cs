using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 혈자리 (레벨업 보상, 경맥도 탭): 기혈 그 칸의 기 + · 통혈 기가 그 칸을 지날 때 줄지 않음 · 한혈 그 칸 기물의 최대 기 +
    public enum Acupoint { None, Gihyeol, Tonghyeol, Hanhyeol }

    // 부품 (회귀록 경맥 가지에서 해금, 칸 하나를 차지하고 기물은 못 놓음):
    //   통맥 (도관) 기가 이 칸을 지날 때 줄지 않음 · 점혈 (절연체) 기가 지나가지 못함 · 한옥 (냉각기) 붙어 있는 기물의 최대 기 +2
    public enum PartKind { None, Tongmaek, Jeomhyeol, Hanok }

    // 경맥도 (경맥도 탭): 기물을 놓는 판. 한 칸에 기물 하나.
    //   칸의 기 = 단전마다 max(0, 공력 − 거리)를 더한 값 (+ 기혈). 거리는 상하좌우로 한 칸씩 센 가장 짧은 길이고, 통혈 칸으로 들어갈 때는 줄지 않아요.
    //   단전은 최대 3개 (하단전 → 중단전 → 상단전), 단전 칸에도 기물을 놓을 수 있어요 (그 칸의 기 = 공력).
    // 부품(통맥·점혈·한옥)은 칸을 차지해요: 통맥은 통혈처럼 줄지 않게, 점혈은 막고, 한옥은 상하좌우 기물의 최대 기 +.
    public sealed class MeridianBoard
    {
        readonly List<Vector2Int> _danjeons = new List<Vector2Int>();
        readonly Dictionary<Vector2Int, Acupoint> _acupoints = new Dictionary<Vector2Int, Acupoint>();
        readonly Dictionary<Vector2Int, PartKind> _parts = new Dictionary<Vector2Int, PartKind>();
        readonly List<int[]> _distances = new List<int[]>(); // 단전마다 칸별 거리 (바뀔 때만 다시 계산)
        bool _dirty = true;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int MaxDanjeons { get; }
        public int Power { get; set; } // 공력: 모든 단전의 출력
        public int GihyeolQi { get; set; } = 1;     // 기혈: 그 칸의 기 +
        public int HanhyeolMaxQi { get; set; } = 1; // 한혈: 그 칸 기물의 최대 기 +
        public int HanokMaxQi { get; set; } = 2;    // 한옥: 붙어 있는 기물의 최대 기 +
        public IReadOnlyList<Vector2Int> Danjeons => _danjeons;

        // ── 장비 칸 방식 (경맥도 대신): 칸 10개가 한 줄 (x = 칸 번호). 단전 · 거리 없이 칸의 기 = 칸 기본 기 + (공력 − 시작 공력) + 기혈.
        // 잠긴 칸은 지역 보스를 쓰러뜨릴 때 열려요 (EquipSlots.UnlockAfterBoss).
        public bool EquipMode { get; private set; }
        public int PowerBase { get; set; }          // 시작 공력 (이만큼일 때 칸 기본 기 그대로)
        readonly bool[] _unlocked = new bool[AThousandDawns.Items.EquipSlots.Count];
        readonly int[] _slotQi = new int[AThousandDawns.Items.EquipSlots.Count];

        public void SetEquipMode(int powerBase)
        {
            EquipMode = true;
            Width = AThousandDawns.Items.EquipSlots.Count;
            Height = 1;
            PowerBase = powerBase;
            _danjeons.Clear();
            _parts.Clear();
            for (int i = 0; i < _slotQi.Length; i++)
            {
                _slotQi[i] = AThousandDawns.Items.EquipSlots.BaseQi(i);
                _unlocked[i] = AThousandDawns.Items.EquipSlots.OpenAtStart(i);
            }
            _dirty = true;
        }

        public bool IsUnlocked(Vector2Int cell) => !EquipMode || (Contains(cell) && _unlocked[cell.x]);

        public void Unlock(int slot)
        {
            if (slot >= 0 && slot < _unlocked.Length) _unlocked[slot] = true;
        }

        public int SlotBaseQi(int slot) => slot >= 0 && slot < _slotQi.Length ? _slotQi[slot] : 0;

        public MeridianBoard(int width, int height, int maxDanjeons, int power)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            MaxDanjeons = Mathf.Max(1, maxDanjeons);
            Power = power;
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

        public bool IsDanjeon(Vector2Int cell) => _danjeons.Contains(cell);

        // 단전을 열어요. 판 밖이거나 이미 단전이거나 최대 개수면 false.
        public bool AddDanjeon(Vector2Int cell)
        {
            if (EquipMode) return false; // 장비 칸엔 단전이 없어요
            if (!Contains(cell) || IsDanjeon(cell) || PartAt(cell) != PartKind.None || _danjeons.Count >= MaxDanjeons) return false;
            _danjeons.Add(cell);
            _dirty = true;
            return true;
        }

        public bool RemoveDanjeon(Vector2Int cell)
        {
            if (!_danjeons.Remove(cell)) return false;
            _dirty = true;
            return true;
        }

        public IReadOnlyDictionary<Vector2Int, Acupoint> Acupoints => _acupoints; // 판 중간 저장용

        public Acupoint AcupointAt(Vector2Int cell) => _acupoints.TryGetValue(cell, out var kind) ? kind : Acupoint.None;

        // 장비 칸: 같은 칸에 기혈 · 한혈을 몇 번이든 (섞어도) 열 수 있어요. 겹친 수만큼 기 · 최대 기가 올라요
        readonly Dictionary<Vector2Int, int> _gihyeolStacks = new Dictionary<Vector2Int, int>();
        readonly Dictionary<Vector2Int, int> _hanhyeolStacks = new Dictionary<Vector2Int, int>();

        public int AcupointStacks(Vector2Int cell, Acupoint kind)
        {
            if (!EquipMode) return AcupointAt(cell) == kind ? 1 : 0;
            var map = kind == Acupoint.Gihyeol ? _gihyeolStacks : kind == Acupoint.Hanhyeol ? _hanhyeolStacks : null;
            return map != null && map.TryGetValue(cell, out int n) ? n : 0;
        }

        // 저장용: 연 혈자리를 하나씩 (장비 칸은 겹친 수만큼 같은 칸이 여러 번)
        public IEnumerable<(Vector2Int cell, Acupoint kind)> OpenedAcupoints()
        {
            if (!EquipMode) { foreach (var kv in _acupoints) yield return (kv.Key, kv.Value); yield break; }
            foreach (var kv in _gihyeolStacks) for (int i = 0; i < kv.Value; i++) yield return (kv.Key, Acupoint.Gihyeol);
            foreach (var kv in _hanhyeolStacks) for (int i = 0; i < kv.Value; i++) yield return (kv.Key, Acupoint.Hanhyeol);
        }

        // 혈자리를 열어요. 경맥도는 한 칸에 하나만, 장비 칸은 기혈 · 한혈을 겹쳐서
        public bool OpenAcupoint(Vector2Int cell, Acupoint kind)
        {
            if (kind == Acupoint.None || !Contains(cell)) return false;
            if (EquipMode)
            {
                if (kind == Acupoint.Tonghyeol) return false; // 장비 칸엔 기가 흐르지 않아요
                var map = kind == Acupoint.Gihyeol ? _gihyeolStacks : _hanhyeolStacks;
                map[cell] = (map.TryGetValue(cell, out int n) ? n : 0) + 1;
                if (!_acupoints.ContainsKey(cell)) _acupoints[cell] = kind; // 그림 · 예전 저장용 (처음 연 것)
                _dirty = true;
                return true;
            }
            if (_acupoints.ContainsKey(cell)) return false;
            _acupoints[cell] = kind;
            _dirty = true;
            return true;
        }

        public PartKind PartAt(Vector2Int cell) => _parts.TryGetValue(cell, out var kind) ? kind : PartKind.None;

        // 부품을 놓아요. 판 밖·단전 칸·이미 부품이 있는 칸이면 false. (기물이 있는지는 PlayerItems가 봐요)
        public bool SetPart(Vector2Int cell, PartKind kind)
        {
            if (EquipMode) return false; // 장비 칸엔 부품을 못 놓아요
            if (kind == PartKind.None || !Contains(cell) || IsDanjeon(cell) || _parts.ContainsKey(cell)) return false;
            _parts[cell] = kind;
            _dirty = true;
            return true;
        }

        public bool ClearPart(Vector2Int cell)
        {
            if (!_parts.Remove(cell)) return false;
            _dirty = true;
            return true;
        }

        public int MaxQiBonusAt(Vector2Int cell)
        {
            int bonus = AcupointStacks(cell, Acupoint.Hanhyeol) * HanhyeolMaxQi;
            foreach (var step in Steps)
                if (PartAt(cell + step) == PartKind.Hanok) bonus += HanokMaxQi; // 한옥이 붙어 있으면
            return bonus;
        }

        public int QiAt(Vector2Int cell)
        {
            if (!Contains(cell)) return 0;
            if (EquipMode) return Mathf.Max(0, _slotQi[cell.x] + Power - PowerBase + AcupointStacks(cell, Acupoint.Gihyeol) * GihyeolQi);
            Refresh();
            int qi = AcupointAt(cell) == Acupoint.Gihyeol ? GihyeolQi : 0;
            int index = cell.y * Width + cell.x;
            foreach (var map in _distances)
                qi += Mathf.Max(0, Power - map[index]);
            return qi;
        }

        // 경맥도 확장 (예: 녹림왕 처치 후 5×4 → 6×4). 줄이지는 않아요.
        public void Expand(int width, int height)
        {
            if (EquipMode) return; // 장비 칸은 보스 처치로 칸을 열어요 (Unlock)
            Width = Mathf.Max(Width, width);
            Height = Mathf.Max(Height, height);
            _dirty = true;
        }

        void Refresh()
        {
            if (!_dirty) return;
            _dirty = false;
            _distances.Clear();
            foreach (var d in _danjeons) _distances.Add(DistanceMap(d));
        }

        static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // 단전에서 모든 칸까지 가장 짧은 거리. 한 칸 갈 때마다 1, 통혈·통맥 칸으로 들어갈 때는 0, 점혈 칸은 못 들어감 (0-1 너비 우선 탐색).
        int[] DistanceMap(Vector2Int from)
        {
            var dist = new int[Width * Height];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var queue = new LinkedList<Vector2Int>();
            dist[from.y * Width + from.x] = 0;
            queue.AddFirst(from);
            while (queue.Count > 0)
            {
                var cell = queue.First.Value;
                queue.RemoveFirst();
                int here = dist[cell.y * Width + cell.x];
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    if (!Contains(next)) continue;
                    var part = PartAt(next);
                    if (part == PartKind.Jeomhyeol) continue; // 점혈: 기가 지나가지 못함
                    int cost = AcupointAt(next) == Acupoint.Tonghyeol || part == PartKind.Tongmaek ? 0 : 1; // 통혈·통맥: 줄지 않음
                    int index = next.y * Width + next.x;
                    if (here + cost >= dist[index]) continue;
                    dist[index] = here + cost;
                    if (cost == 0) queue.AddFirst(next);
                    else queue.AddLast(next);
                }
            }
            return dist;
        }
    }
}
