using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    public class FloorMapTests
    {
        static List<FloorRoom> Neighbors(FloorMap map, FloorRoom room) =>
            Enumerable.Range(0, 4).Select(d => map.Neighbor(room, d)).Where(n => n != null).ToList();

        // 시작 방에서 target까지 가는 길 (나무 모양이라 하나뿐)
        static List<FloorRoom> PathTo(FloorMap map, FloorRoom target)
        {
            var prev = new Dictionary<FloorRoom, FloorRoom> { [map.Start] = null };
            var queue = new Queue<FloorRoom>();
            queue.Enqueue(map.Start);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var n in Neighbors(map, r))
                {
                    if (prev.ContainsKey(n)) continue;
                    prev[n] = r;
                    queue.Enqueue(n);
                }
            }
            var path = new List<FloorRoom>();
            for (var r = target; r != null; r = prev[r]) path.Add(r);
            return path;
        }

        [Test]
        public void 판구조대로_방이_놓이고_보상_방과_관문은_막다른_곳()
        {
            for (int seed = 1; seed <= 300; seed++)
            {
                var map = FloorMap.Generate(new System.Random(seed), 3, 2);
                Assert.AreEqual(1, map.Rooms.Count(r => r.Kind == FloorRoomKind.Start));
                Assert.AreEqual(1, map.Rooms.Count(r => r.Kind == FloorRoomKind.Gate));
                Assert.AreEqual(1, map.Rooms.Count(r => r.Kind == FloorRoomKind.Reward));
                Assert.AreEqual(2, map.Rooms.Count(r => r.Kind == FloorRoomKind.Combat && !r.Optional), $"시드 {seed}: 길 위 전투 방");
                Assert.AreEqual(2, map.Rooms.Count(r => r.Optional), $"시드 {seed}: 선택 방");

                var gate = map.Rooms.First(r => r.Kind == FloorRoomKind.Gate);
                var reward = map.Rooms.First(r => r.Kind == FloorRoomKind.Reward);
                Assert.AreEqual(1, Neighbors(map, gate).Count, $"시드 {seed}: 관문은 막다른 곳");
                Assert.AreEqual(1, Neighbors(map, reward).Count, $"시드 {seed}: 보상 방은 막다른 곳");

                // 나무 모양: 이웃 쌍(문) 수 = 방 수 − 1
                int doors = map.Rooms.Sum(r => Neighbors(map, r).Count) / 2;
                Assert.AreEqual(map.Rooms.Count - 1, doors, $"시드 {seed}: 지름길 없음");

                // 보상과 관문을 다 얻는 최소 경로의 전투 방 = 3 (최소 경로 방, 판구조 C5)
                var needed = new HashSet<FloorRoom>(PathTo(map, gate).Concat(PathTo(map, reward)));
                Assert.AreEqual(3, needed.Count(r => r.HasFight), $"시드 {seed}");
                Assert.IsFalse(needed.Any(r => r.Optional), $"시드 {seed}: 선택 방은 지나가지 않아도 돼요");
            }
        }

        [Test]
        public void 같은_시드면_같은_배치()
        {
            var a = FloorMap.Generate(new System.Random(9), 3, 2);
            var b = FloorMap.Generate(new System.Random(9), 3, 2);
            CollectionAssert.AreEqual(a.Rooms.Select(r => (r.Cell, r.Kind)), b.Rooms.Select(r => (r.Cell, r.Kind)));
        }

        [Test]
        public void 들어간_방과_옆방만_드러남()
        {
            var map = FloorMap.Generate(new System.Random(3), 3, 2);
            map.Visit(map.Start);
            foreach (var r in map.Rooms)
            {
                bool adjacent = r == map.Start || Neighbors(map, map.Start).Contains(r);
                Assert.AreEqual(adjacent, r.Revealed);
            }
            Assert.AreEqual(5, map.FightsLeft(), "길 위 전투 방 2 + 보상 방(전투 포함) 1 + 선택 방 2");
        }
    }
}
