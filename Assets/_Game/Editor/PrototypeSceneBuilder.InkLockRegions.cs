using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 결계 문을 지역마다 다르게 (채색): DressLocksInk가 세운 검은 격자·붉은 부적 문("Barrier")을 지우고 네 모양을 달아 InkRegionDressing이 하나만 켜요.
    //   죽림: 굵은 대나무를 엮은 사립문에 끈 매듭 · 폐사찰: 붉은 칠 기둥·청록 단청 띠, 살문 위에 금빛 부적
    //   장강 수채: 짙은 판자를 쇠띠로 묶은 나무 수문 · 천마총: 검은 쇠창살 위로 가시, 늘어진 붉은 휘장
    // 문 크기·방향은 DressLocksInk와 같은 규칙 (Ink Visual 아래, 너비가 놓인 축으로 돌려요). 충돌은 원래 상자가 맡아요
    public static partial class PrototypeSceneBuilder
    {
        static void DressLockRegions(List<GameObject> bamboo, List<GameObject> temple, List<GameObject> river, List<GameObject> tomb)
        {
            var (_, _, _, bambooMat) = GetInkEnvMaterials();
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var dark = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);
            var iron = GetInkMaterial("Ink_Tomb_Iron", "#2E3236", 0.1f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var gold = GetInkMaterial("Ink_Lock_Gold", "#E8B53A", 1f);
            var lattice = GetInkMaterial("Ink_Lock_Lattice", "#7A4A2C", 1f);
            int count = 0;
            // 먼저 결계 문 목록을 모아요 (옛 문을 지우면 그 자식 Transform도 사라져 목록을 돌다 깨졌어요)
            var locks = new List<Transform>();
            foreach (var t0 in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t0.name.EndsWith("(결계)")) locks.Add(t0);
            foreach (var t in locks)
            {
                if (!t.name.EndsWith("(결계)")) continue;
                var visual = t.Find("Ink Visual");
                var old = visual != null ? visual.Find("Barrier") : null;
                if (old == null) continue;
                var s = t.lossyScale;
                bool alongX = s.x >= s.z;
                float width = alongX ? s.x : s.z, height = s.y, hw = width * 0.5f;
                var rot = old.localRotation;
                Object.DestroyImmediate(old.gameObject);

                Transform Gate(string name)
                {
                    var g = new GameObject(name).transform;
                    g.SetParent(visual, false);
                    g.localRotation = rot;
                    return g;
                }

                // 죽림: 대나무 사립문
                var b = Gate("Barrier 죽림");
                for (int side = -1; side <= 1; side += 2)
                    StripColliders(Stalk(b, new Vector3(side * (hw - 0.1f), 0f, 0f), 0.2f, height * 1.15f, side * 40f, bambooMat));
                int poles = Mathf.Max(4, Mathf.RoundToInt(width / 0.2f));
                for (int k = 1; k < poles; k++)
                    StripColliders(Stalk(b, new Vector3(-hw + width * k / poles, 0f, (k % 2) * 0.04f - 0.02f), 0.12f, height * (0.92f + (k % 3) * 0.04f), k * 33f, bambooMat));
                foreach (float y in new[] { 0.35f, height - 0.25f })
                {
                    Block(b, "Tie", PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(width - 0.1f, 0.06f, 0.18f), Quaternion.identity, rope);
                    Block(b, "Knot", PrimitiveType.Sphere, new Vector3(0f, y, -0.1f), new Vector3(0.12f, 0.12f, 0.08f), Quaternion.identity, rope);
                }

                // 폐사찰: 단청 살문
                var p = Gate("Barrier 폐사찰");
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(p, "Post", PrimitiveType.Cylinder, new Vector3(side * (hw - 0.1f), height * 0.56f, 0f), new Vector3(0.2f, height * 0.56f, 0.2f), Quaternion.identity, TempleLacquer());
                    DancheongBandOn(p, new Vector3(side * (hw - 0.1f), height * 1.12f, 0f), 0.1f);
                }
                Block(p, "Lintel", PrimitiveType.Cube, new Vector3(0f, height * 1.08f, 0f), new Vector3(width, 0.16f, 0.2f), Quaternion.identity, TempleLacquer());
                Block(p, "Lintel Band", PrimitiveType.Cube, new Vector3(0f, height * 1.08f, -0.105f), new Vector3(width - 0.2f, 0.08f, 0.01f), Quaternion.identity, DancheongBand());
                int slats = Mathf.Max(3, Mathf.RoundToInt(width / 0.28f));
                for (int k = 1; k < slats; k++)
                    Block(p, "Slat", PrimitiveType.Cube, new Vector3(-hw + width * k / slats, height * 0.5f, 0f), new Vector3(0.05f, height * 0.9f, 0.05f), Quaternion.identity, lattice);
                for (int k = 0; k < 4; k++)
                    Block(p, "Cross Slat", PrimitiveType.Cube, new Vector3(0f, height * (0.2f + k * 0.22f), 0f), new Vector3(width - 0.2f, 0.04f, 0.05f), Quaternion.identity, lattice);
                foreach (float z in new[] { -0.05f, 0.05f })
                    Block(p, "Gold Talisman", PrimitiveType.Cube, new Vector3(0f, height * 0.55f, z), new Vector3(0.22f, 0.5f, 0.01f), Quaternion.identity, gold);

                // 장강: 쇠띠 두른 나무 수문
                var w = Gate("Barrier 장강");
                for (int side = -1; side <= 1; side += 2)
                    Block(w, "Post", PrimitiveType.Cylinder, new Vector3(side * (hw - 0.1f), height * 0.6f, 0f), new Vector3(0.22f, height * 0.6f, 0.22f), Quaternion.identity, dark);
                int planks = Mathf.Max(3, Mathf.RoundToInt(width / 0.3f));
                for (int k = 0; k < planks; k++)
                {
                    float x = -hw + 0.15f + (width - 0.3f) * (k + 0.5f) / planks;
                    Block(w, "Plank", PrimitiveType.Cube, new Vector3(x, height * 0.48f, 0f), new Vector3((width - 0.3f) / planks - 0.02f, height * (0.86f + (k % 2) * 0.04f), 0.08f), Quaternion.identity, lattice);
                }
                foreach (float y in new[] { 0.25f, 0.55f, 0.85f })
                    Block(w, "Iron Band", PrimitiveType.Cube, new Vector3(0f, height * y, 0f), new Vector3(width - 0.3f, 0.07f, 0.1f), Quaternion.identity, iron);
                Block(w, "Rope Coil", PrimitiveType.Cylinder, new Vector3(hw - 0.1f, height * 0.7f, -0.12f), new Vector3(0.3f, 0.06f, 0.3f), Quaternion.Euler(90f, 0f, 0f), rope);

                // 천마총: 가시 박힌 쇠창살과 붉은 휘장
                var m = Gate("Barrier 천마총");
                for (int side = -1; side <= 1; side += 2)
                    Block(m, "Post", PrimitiveType.Cube, new Vector3(side * (hw - 0.1f), height * 0.55f, 0f), new Vector3(0.2f, height * 1.1f, 0.2f), Quaternion.identity, iron);
                int bars = Mathf.Max(4, Mathf.RoundToInt(width / 0.24f));
                for (int k = 1; k < bars; k++)
                {
                    float x = -hw + width * k / bars;
                    Block(m, "Bar", PrimitiveType.Cylinder, new Vector3(x, height * 0.5f, 0f), new Vector3(0.05f, height * 0.5f, 0.05f), Quaternion.identity, iron);
                    Block(m, "Spike", PrimitiveType.Cube, new Vector3(x, height + 0.06f, 0f), new Vector3(0.06f, 0.14f, 0.06f), Quaternion.Euler(0f, 45f, 45f), iron);
                }
                Block(m, "Cross Bar", PrimitiveType.Cube, new Vector3(0f, height * 0.85f, 0f), new Vector3(width - 0.1f, 0.08f, 0.08f), Quaternion.identity, iron);
                for (int side = -1; side <= 1; side += 2)   // 양쪽 기둥에 늘어진 붉은 휘장
                    foreach (float z in new[] { -0.08f, 0.08f })
                        Block(m, "Drape", PrimitiveType.Cube, new Vector3(side * (hw - 0.32f), height * 0.6f, z), new Vector3(0.36f, height * 0.75f, 0.015f), Quaternion.Euler(0f, 0f, side * 4f), red);

                foreach (var g in new[] { b, p, w, m }) StripColliders(g);
                bamboo.Add(b.gameObject); temple.Add(p.gameObject); river.Add(w.gameObject); tomb.Add(m.gameObject);
                p.gameObject.SetActive(false); w.gameObject.SetActive(false); m.gameObject.SetActive(false);   // 처음(죽림)엔 대나무 문만
                count++;
            }
            Debug.Log($"[채색] 지역별 결계 문: {count}개");
        }
    }
}
