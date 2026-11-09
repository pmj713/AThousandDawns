using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 항아리·상자 (은자 탭 C4: 방당 3): 전투 방마다 벽가에 항아리가 놓여 있어요. 부딪히면 깨지고 은자가 나와요.
    // 한 번 깬 항아리는 그 층에서 다시 나오지 않아요 (FloorRoom.Jars가 남은 수). 판 시드와 방 자리로 놓을 곳을 정해서 _rng를 건드리지 않아요.
    public sealed partial class RoomRun
    {
        readonly List<GameObject> _jars = new List<GameObject>();

        public int JarsInRoom => _jars.Count;

        // 방에 들어갈 때 (null이면 치움)
        void PlaceJars(FloorRoom room)
        {
            foreach (var j in _jars) if (j != null) Destroy(j);
            _jars.Clear();
            if (room == null || !room.HasFight) return;
            if (room.Jars < 0) room.Jars = Mathf.Max(0, Mathf.RoundToInt(GameConfig.GetOptional("jar.per_room", 3f)));
            var rng = new System.Random(Seed ^ (room.Cell.x * 73856093) ^ (room.Cell.y * 19349663) ^ (_floor * 83492791));
            float halfX = combat.size.x * 0.5f - 1.2f, halfZ = combat.size.y * 0.5f - 1.2f;
            for (int i = 0; i < room.Jars; i++)
            {
                // 모서리 쪽 벽가에 (문 앞은 피해서)
                float sx = rng.Next(2) == 0 ? -1f : 1f, sz = rng.Next(2) == 0 ? -1f : 1f;
                var p = combat.center + new Vector3(sx * halfX * (0.55f + 0.4f * (float)rng.NextDouble()), 0f, sz * halfZ * (0.6f + 0.35f * (float)rng.NextDouble()));
                var jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jar.name = "Jar";
                Destroy(jar.GetComponent<Collider>());
                jar.GetComponent<Renderer>().material.color = new Color(0.55f, 0.36f, 0.2f);
                jar.transform.position = p + Vector3.up * 0.35f;
                jar.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
                _jars.Add(jar);
            }
        }

        void TickJars()
        {
            if (_jars.Count == 0 || _room == null) return;
            float reach = GameConfig.GetOptional("jar.break_radius", 0.9f);
            Vector3 p = Player.transform.position;
            for (int i = _jars.Count - 1; i >= 0; i--)
            {
                if (Flat(_jars[i].transform.position - p).magnitude > reach) continue;
                BreakJar(i);
            }
        }

        // 테스트용으로도 써요
        public void BreakJar(int index)
        {
            if (index < 0 || index >= _jars.Count) return;
            Destroy(_jars[index]);
            _jars.RemoveAt(index);
            _room.Jars = Mathf.Max(0, _room.Jars - 1);
            int silver = BoostSilver(Mathf.RoundToInt(GameConfig.GetOptional("jar.silver", 1f)));
            Silver += silver;
            ShowBanner($"항아리를 깼어요 · 은자 +{silver}");
        }
    }
}
