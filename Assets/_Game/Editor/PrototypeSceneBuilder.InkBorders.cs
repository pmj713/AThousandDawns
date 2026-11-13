using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 층 방(작은·중간·큰) 테두리를 지역마다 다르게 (4차 플레이 테스트: 지역을 옮겨도 같은 기와 담이라 옮긴 느낌이 안 났어요).
    // 담 상자("Wall_*")와 막힌 문("Door_* (벽)")은 충돌체 그대로 두고 그림만 끈 뒤, 담마다 네 모양을 달아 InkRegionDressing이 하나만 켜요.
    //   죽림: 대나무를 촘촘히 세워 끈으로 묶은 대울타리 · 폐사찰: 원래 돌담과 기와(무너진 돌·깨진 기왓장·이끼를 더해요)
    //   장강 수채: 끝이 뾰족한 나무 말뚝 목책에 밧줄 · 천마총: 검은 돌벽 위에 쇠 가시, 군데군데 늘어뜨린 붉은 천
    // 담은 움직이지 않으니 정적 배칭, 충돌은 원래 상자가 맡아요 (여기서 만든 건 충돌체 없음)
    public static partial class PrototypeSceneBuilder
    {
        static void DressRoomBorders(List<GameObject> bamboo, List<GameObject> temple, List<GameObject> river, List<GameObject> tomb, Material stoneMat, ref int seed)
        {
            var (_, _, _, bambooMat) = GetInkEnvMaterials();
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var stake = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);
            var iron = GetInkMaterial("Ink_Tomb_Iron", "#2E3236", 0.1f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var moss = GetInkMaterial("Ink_Temple_Moss", "#5E6B3A", 0.5f);
            var (roofX, roofZ) = GetInkRoofMaterials();
            int walls = 0;
            foreach (var (room, center, _) in InkFloorRooms())
            {
                foreach (var wall in room.Cast<Transform>().ToList())   // 무너진 담은 방 아래에 붙으니 먼저 목록으로
                {
                    if (!wall.name.StartsWith("Wall_") && !wall.name.EndsWith("(벽)")) continue;
                    var r = wall.GetComponent<Renderer>();
                    if (r == null || !r.enabled) continue;
                    var s = wall.lossyScale;
                    bool alongX = s.x >= s.z;
                    float length = alongX ? s.x : s.z, height = s.y, thick = alongX ? s.z : s.x;
                    r.enabled = false;

                    // 폐사찰: 긴 담은 고수 결투장과 같은 무너진 기와 담(RuinWall), 짧은 담(막힌 문)은 원래 돌담+기와에 떨어진 돌·이끼
                    var caps = new List<Transform>();
                    foreach (Transform c in wall) if (c.name.StartsWith("RoofCap")) caps.Add(c);
                    Transform ruin;
                    if (length >= 6f)
                    {
                        ruin = RuinWall(wall, r.sharedMaterial, roofX, roofZ, seed++, center);
                        ruin.name = $"Border 폐사찰 ({wall.name})";
                        foreach (var c in caps) Object.DestroyImmediate(c.gameObject);
                    }
                    else
                    {
                        ruin = new GameObject("Border 폐사찰").transform;
                        ruin.SetParent(wall, false);
                        Block(ruin, "Stone Wall", PrimitiveType.Cube, Vector3.zero, Vector3.one, Quaternion.identity, r.sharedMaterial);
                        foreach (var c in caps) c.SetParent(ruin, false);
                        var rubble = Unscaled(ruin, "Rubble", s, alongX);
                        for (int k = 0; k < 2; k++, seed++)
                        {
                            float x = (k - 0.5f) * length * 0.5f;
                            float side = seed % 2 == 0 ? 1f : -1f;
                            Rock(rubble, "Fallen Stone", new Vector3(x, -height * 0.5f, side * (thick * 0.5f + 0.2f)), 0.22f + (seed % 3) * 0.06f, seed, seed * 41f, stoneMat, false);
                            Block(rubble, "Moss", PrimitiveType.Sphere, new Vector3(x + 0.3f, -height * 0.5f + 0.05f, side * (thick * 0.5f + 0.05f)), new Vector3(0.4f, 0.12f, 0.2f), Quaternion.identity, moss);
                        }
                    }

                    // 죽림: 대울타리
                    var fence = Unscaled(wall, "Border 죽림", s, alongX);
                    for (float x = -length * 0.5f + 0.09f; x <= length * 0.5f - 0.05f; x += 0.17f, seed++)
                    {
                        float h = height + 0.25f + ((seed * 13) % 5) * 0.08f;
                        StripColliders(Stalk(fence, new Vector3(x, -height * 0.5f, ((seed * 7) % 3 - 1) * 0.04f), 0.15f, h, seed * 31f, bambooMat));
                    }
                    foreach (float y in new[] { 0.35f, height - 0.25f })
                        Block(fence, "Tie", PrimitiveType.Cube, new Vector3(0f, -height * 0.5f + y, 0f), new Vector3(length, 0.05f, 0.22f), Quaternion.identity, rope);

                    // 장강: 뾰족한 나무 말뚝 목책과 밧줄
                    var pales = Unscaled(wall, "Border 장강", s, alongX);
                    for (float x = -length * 0.5f + 0.12f; x <= length * 0.5f - 0.05f; x += 0.24f, seed++)
                    {
                        float h = height + 0.1f + ((seed * 11) % 5) * 0.1f;
                        float lean = ((seed * 17) % 5 - 2) * 0.8f;
                        var rot = Quaternion.Euler(0f, 0f, lean);
                        Block(pales, "Stake", PrimitiveType.Cylinder, new Vector3(x, -height * 0.5f + h * 0.5f, 0f), new Vector3(0.2f, h * 0.5f, 0.2f), rot, stake);
                        Block(pales, "Point", PrimitiveType.Cube, new Vector3(x, -height * 0.5f + h + 0.03f, 0f), new Vector3(0.13f, 0.13f, 0.13f), rot * Quaternion.Euler(45f, 0f, 45f), stake);
                    }
                    foreach (float y in new[] { 0.45f, height - 0.2f })
                        Block(pales, "Rope", PrimitiveType.Cylinder, new Vector3(0f, -height * 0.5f + y, -0.11f), new Vector3(0.04f, length * 0.5f, 0.04f), Quaternion.Euler(0f, 0f, 90f), rope);

                    // 천마총: 검은 돌벽, 위에 쇠 가시, 군데군데 붉은 천
                    var black = Unscaled(wall, "Border 천마총", s, alongX);
                    Block(black, "Black Wall", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(length, height + 0.16f, thick), Quaternion.identity, marble);
                    Block(black, "Coping", PrimitiveType.Cube, new Vector3(0f, height * 0.5f + 0.2f, 0f), new Vector3(length + 0.1f, 0.1f, thick + 0.16f), Quaternion.identity, marble);
                    for (float x = -length * 0.5f + 0.3f; x <= length * 0.5f - 0.2f; x += 0.6f)
                        Block(black, "Spike", PrimitiveType.Cube, new Vector3(x, height * 0.5f + 0.38f, 0f), new Vector3(0.06f, 0.3f, 0.06f), Quaternion.identity, iron);
                    for (float x = -length * 0.5f + 1.4f; x <= length * 0.5f - 1f; x += 3.2f, seed++)
                        foreach (float side in new[] { -1f, 1f })
                            Block(black, "Banner", PrimitiveType.Cube, new Vector3(x, height * 0.5f - 0.35f, side * (thick * 0.5f + 0.02f)), new Vector3(0.36f, 0.9f, 0.02f), Quaternion.Euler(0f, 0f, ((seed % 3) - 1) * 3f), red);

                    foreach (var v in new[] { ruin, fence, pales, black }) { StripColliders(v); MarkStatic(v); }
                    bamboo.Add(fence.gameObject);
                    temple.Add(ruin.gameObject);
                    river.Add(pales.gameObject);
                    tomb.Add(black.gameObject);
                    ruin.gameObject.SetActive(false); pales.gameObject.SetActive(false); black.gameObject.SetActive(false);   // 처음(죽림)엔 대울타리만
                    walls++;
                }
            }
            Debug.Log($"[수묵 시험판] 지역별 방 테두리: 담 {walls}개");
        }

        // 담(크기를 늘린 상자) 아래, 크기를 되돌린 묶음: 담 가운데가 원점이고 담 길이 방향이 +X (Z로 놓인 담이면 90도 돌려요)
        static Transform Unscaled(Transform wall, string name, Vector3 s, bool alongX)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(wall, false);
            holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            var inner = new GameObject("Shape").transform;   // 크기를 되돌린 층 아래에서 돌려요 (크기가 다른 부모 밑에서 바로 돌리면 비뚤어져요)
            inner.SetParent(holder, false);
            inner.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
            return inner;
        }

        static void MarkStatic(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.BatchingStatic);
        }
    }
}
