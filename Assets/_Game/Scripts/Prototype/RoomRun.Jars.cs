using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 항아리·상자 (은자 탭 C4: 방당 3): 전투 방마다 벽가에 항아리가 놓여 있어요. 길을 막고, 공격(평타·초식·대시 공격·비도)이 닿으면 깨지고 은자가 나와요. 싸우는 중에도 돼요.
    // 한 번 깬 항아리는 그 층에서 다시 나오지 않아요 (FloorRoom.Jars가 남은 수). 판 시드와 방 자리로 놓을 곳을 정해서 _rng를 건드리지 않아요.
    public sealed partial class RoomRun
    {
        readonly List<GameObject> _jars = new List<GameObject>();

        public int JarsInRoom => _jars.Count;
        public IReadOnlyList<GameObject> Jars => _jars;

        // 방에 들어갈 때 (null이면 치움)
        void PlaceJars(FloorRoom room)
        {
            foreach (var j in _jars) if (j != null) Destroy(j);
            _jars.Clear();
            if (room == null || !room.HasFight) return;
            if (room.Jars < 0) room.Jars = Mathf.Max(0, Mathf.RoundToInt(GameConfig.GetOptional("jar.per_room", 3f)));
            var rng = new System.Random(Seed ^ (room.Cell.x * 73856093) ^ (room.Cell.y * 19349663) ^ (_floor * 83492791));
            float halfX = Combat.size.x * 0.5f - 1.2f, halfZ = Combat.size.y * 0.5f - 1.2f;
            for (int i = 0; i < room.Jars; i++)
            {
                // 모서리 쪽 벽가에 (문 앞은 피해서)
                Vector3 p = Combat.center;
                for (int tries = 0; tries < 8 && (tries == 0 || InSolid(p, 0.5f)); tries++) // 폐사찰 석불 · 담 안은 피해요 (RoomRun.Layout)
                {
                    float sx = rng.Next(2) == 0 ? -1f : 1f, sz = rng.Next(2) == 0 ? -1f : 1f;
                    p = Combat.center + new Vector3(sx * halfX * (0.55f + 0.4f * (float)rng.NextDouble()), 0f, sz * halfZ * (0.6f + 0.35f * (float)rng.NextDouble()));
                }
                var jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jar.name = "Jar";
                // 충돌체는 남겨요: 캐릭터 · 적이 통과하지 못하고, 공격으로만 깨져요
                jar.GetComponent<Renderer>().material.color = new Color(0.55f, 0.36f, 0.2f);
                jar.transform.position = p + Vector3.up * 0.35f;
                jar.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
                var breakable = jar.AddComponent<AThousandDawns.Combat.Breakable>(); // 평타·초식·비도로도 깨져요
                breakable.Radius = 0.3f;
                breakable.Broken += b => BreakJar(_jars.IndexOf(b.gameObject));
                _jars.Add(jar);
            }
        }

        // 테스트용으로도 써요
        public void BreakJar(int index)
        {
            if (index < 0 || index >= _jars.Count) return;
            var col = _jars[index].GetComponent<Collider>();
            if (col != null) col.enabled = false; // Destroy는 프레임 끝이라 그 사이 캐릭터가 걸리지 않게 먼저 꺼요
            Destroy(_jars[index]);
            _jars.RemoveAt(index);
            Sfx.Play(SfxId.JarBreak);
            _room.Jars = Mathf.Max(0, _room.Jars - 1);
            int silver = BoostSilver(Mathf.RoundToInt(GameConfig.GetOptional("jar.silver", 1f)));
            Silver += silver;
        }
    }
}
