using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    public enum FloorRoomKind { Start, Combat, Reward, Gate }

    public sealed class FloorRoom
    {
        public Vector2Int Cell;
        public FloorRoomKind Kind;
        public bool Optional;  // 최소 경로 밖 선택 방
        public bool Visited, Revealed, Cleared;

        public bool HasFight => Kind == FloorRoomKind.Combat || Kind == FloorRoomKind.Reward; // 보상 방은 앞에 전투가 한 번 붙어요
    }

    // 층 하나의 방 배치 (아이작 방식): 격자 위에서 시작 방부터 옆 칸으로 뻗어 나가요.
    // 새 방은 이미 있는 방 하나와만 붙게 놓아서 길이 나무 모양이 돼요 (지름길 없음, 보상 방·관문은 늘 막다른 곳).
    // 시작 → 전투 방 (pathRooms − 1)개 → 관문. 보상 방은 그 길의 전투 방 하나에 매달린 막다른 방이라,
    // 보상과 관문을 다 얻는 최소 경로의 전투 방 수가 pathRooms예요. 선택 방은 시작 방이나 전투 방에 매달려요.
    public sealed class FloorMap
    {
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left }; // 북 동 남 서

        readonly List<FloorRoom> _rooms = new List<FloorRoom>();
        readonly Dictionary<Vector2Int, FloorRoom> _byCell = new Dictionary<Vector2Int, FloorRoom>();

        public IReadOnlyList<FloorRoom> Rooms => _rooms;
        public FloorRoom Start => _rooms[0];

        public FloorRoom At(Vector2Int cell) => _byCell.TryGetValue(cell, out var room) ? room : null;

        public FloorRoom Neighbor(FloorRoom room, int direction) => At(room.Cell + Directions[direction]);

        public static int Opposite(int direction) => (direction + 2) % 4;

        public static FloorMap Generate(System.Random rng, int pathRooms, int optionalRooms)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var map = new FloorMap();
                if (map.TryBuild(rng, Math.Max(1, pathRooms), Math.Max(0, optionalRooms))) return map;
            }
            throw new InvalidOperationException("층 배치를 만들지 못했어요");
        }

        bool TryBuild(System.Random rng, int pathRooms, int optionalRooms)
        {
            var start = Add(Vector2Int.zero, FloorRoomKind.Start, false);

            // 최소 경로: 시작 → 전투 방들 → 관문
            var chain = new List<FloorRoom> { start };
            for (int i = 0; i < pathRooms - 1; i++)
            {
                var next = Grow(chain[chain.Count - 1], FloorRoomKind.Combat, false, rng);
                if (next == null) return false;
                chain.Add(next);
            }
            if (Grow(chain[chain.Count - 1], FloorRoomKind.Gate, false, rng) == null) return false;

            // 보상 방: 길 위 전투 방 하나에 매달기 (전투 방이 없으면 시작 방)
            var hosts = chain.Count > 1 ? chain.GetRange(1, chain.Count - 1) : chain;
            if (!GrowFromAny(hosts, FloorRoomKind.Reward, false, rng)) return false;

            // 선택 방: 시작 방이나 전투 방(선택 방 포함)에 매달기
            for (int i = 0; i < optionalRooms; i++)
            {
                var anchors = _rooms.FindAll(r => r.Kind == FloorRoomKind.Start || r.Kind == FloorRoomKind.Combat);
                if (!GrowFromAny(anchors, FloorRoomKind.Combat, true, rng)) return false;
            }
            return true;
        }

        bool GrowFromAny(List<FloorRoom> anchors, FloorRoomKind kind, bool optional, System.Random rng)
        {
            var order = new List<FloorRoom>(anchors);
            Shuffle(order, rng);
            foreach (var anchor in order)
                if (Grow(anchor, kind, optional, rng) != null) return true;
            return false;
        }

        // from 옆의 빈 칸 가운데, from 말고는 아무 방과도 붙지 않는 칸에 새 방
        FloorRoom Grow(FloorRoom from, FloorRoomKind kind, bool optional, System.Random rng)
        {
            var dirs = new List<int> { 0, 1, 2, 3 };
            Shuffle(dirs, rng);
            foreach (int d in dirs)
            {
                var cell = from.Cell + Directions[d];
                if (_byCell.ContainsKey(cell)) continue;
                int touching = 0;
                foreach (var dir in Directions)
                    if (_byCell.ContainsKey(cell + dir)) touching++;
                if (touching != 1) continue;
                return Add(cell, kind, optional);
            }
            return null;
        }

        FloorRoom Add(Vector2Int cell, FloorRoomKind kind, bool optional)
        {
            var room = new FloorRoom { Cell = cell, Kind = kind, Optional = optional };
            _rooms.Add(room);
            _byCell[cell] = room;
            return room;
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // 들어간 방과 그 옆방이 지도에 드러나요
        public void Visit(FloorRoom room)
        {
            room.Visited = true;
            room.Revealed = true;
            for (int d = 0; d < 4; d++)
            {
                var n = Neighbor(room, d);
                if (n != null) n.Revealed = true;
            }
        }

        public int FightsLeft()
        {
            int n = 0;
            foreach (var r in _rooms) if (r.HasFight && !r.Cleared) n++;
            return n;
        }
    }
}
