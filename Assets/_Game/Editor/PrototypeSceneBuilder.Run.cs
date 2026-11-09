using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Data;
using AThousandDawns.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판 구조 씬: 전투 방(20×12, 조작 시트 방 크기) · 정비실 · 녹림왕 방을 멀리 떨어뜨려 놓고, 문으로 나가면 다음 방으로 옮겨요.
    // 전투 방 하나를 층·방마다 다시 써요 (적 구성만 바뀜).
    public static partial class PrototypeSceneBuilder
    {
        public const string RunScenePath = "Assets/_Game/Scenes/Prototype_Run.unity";

        static readonly Vector3 CombatCenter = Vector3.zero;
        static readonly Vector3 RestCenter = new Vector3(60f, 0f, 0f);
        static readonly Vector3 BossCenter = new Vector3(120f, 0f, 0f);

        static void BuildRun(BalanceConfig balance, AThousandDawns.Items.ItemDatabase itemDb)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMat = GetMaterial("Floor", FloorColor, false);
            var wallMat = GetMaterial("Wall", WallColor, false);
            var bambooMat = GetMaterial("Bamboo", BambooColor, false);
            var gateMat = GetMaterial("BossGate", GateColor, false);
            var dummyMat = GetMaterial("Dummy", DummyColor, false);

            // 전투 방: 대나무 기둥 6개, 청죽사 풀숲 4곳. 가운데 세로 길(입구 → 문)은 비워 둬요.
            var combatDoor = BuildShell("Room_Combat", CombatCenter, new Vector2(20f, 12f), floorMat, wallMat, gateMat, true);
            Vector3[] pillars =
            {
                new Vector3(-5f, 0f, 2.5f), new Vector3(5f, 0f, -2f), new Vector3(-7f, 0f, -3f),
                new Vector3(7f, 0f, 3f), new Vector3(3f, 0f, 2f), new Vector3(-3f, 0f, -1f),
            };
            var pillarParent = GameObject.Find("Room_Combat").transform;
            for (int i = 0; i < pillars.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.name = $"Bamboo_{i + 1}";
                b.transform.SetParent(pillarParent, false);
                b.transform.localPosition = pillars[i] + Vector3.up * 1.5f;
                b.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
                b.GetComponent<Renderer>().sharedMaterial = bambooMat;
            }
            BuildBushes(GetMaterial("Bush", BushColor, false), new[]
            {
                new Vector3(-7.5f, 0f, 4f), new Vector3(7.5f, 0f, -4f), new Vector3(3.5f, 0f, 4.2f), new Vector3(-3.5f, 0f, -4f),
            });

            // 정비실: 12×10, 문은 늘 열려 있어요
            BuildShell("Room_Rest", RestCenter, new Vector2(12f, 10f), floorMat, wallMat, null, false);

            // 녹림왕 방: 16×12 (몬스터 시트 결투장), 테두리는 대나무 숲. 들어오면 바로 결투
            var boss = new GameObject("Room_Boss").transform;
            Box("Floor", boss, BossCenter + new Vector3(0f, -0.1f, 0f), new Vector3(17f, 0.2f, 13f), floorMat);
            Box("Bamboo_W", boss, BossCenter + new Vector3(-8.25f, 1.25f, 0f), new Vector3(0.5f, 2.5f, 12.5f), bambooMat);
            Box("Bamboo_E", boss, BossCenter + new Vector3(8.25f, 1.25f, 0f), new Vector3(0.5f, 2.5f, 12.5f), bambooMat);
            Box("Bamboo_N", boss, BossCenter + new Vector3(0f, 1.25f, 6.25f), new Vector3(17f, 2.5f, 0.5f), bambooMat);
            Box("Bamboo_S", boss, BossCenter + new Vector3(0f, 1.25f, -6.25f), new Vector3(17f, 2.5f, 0.5f), bambooMat);

            var config = new GameObject("GameConfig").AddComponent<GameConfig>();
            SetRef(config, "balance", LoadBalance());
            SetRef(config, "items", LoadItems());

            var player = BuildPlayer(GetMaterial("Player", PlayerColor, false), wallMat, GetMaterial("SwingFlash", FlashColor, true),
                GetMaterial("UltimateSword", UltimateSwordColor, false), CombatCenter + new Vector3(0f, 0f, -4.5f));
            var dummyA = BuildDummy("목인장 (강인함 0)", RestCenter + new Vector3(-2.5f, 0f, 1.5f), 0f, 0f, false, dummyMat);
            var dummyB = BuildDummy("목인장 (녹림왕: 강인함 5, 방어 10%)", RestCenter + new Vector3(2.5f, 0f, 1.5f), 5f, 0.1f, true, dummyMat);
            BuildCamera(player.transform);

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var director = BuildDirector(player);
            var arena = new GameObject("PrototypeArena").AddComponent<PrototypeArena>();
            SetRef(arena, "director", director);
            var arenaSo = new SerializedObject(arena);
            arenaSo.FindProperty("sandbox").boolValue = false;
            arenaSo.FindProperty("bossZoneCenter").vector3Value = BossCenter;
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            var run = arena.gameObject.AddComponent<RoomRun>();
            var runSo = new SerializedObject(run);
            runSo.FindProperty("director").objectReferenceValue = director;
            runSo.FindProperty("arena").objectReferenceValue = arena;
            SetSite(runSo.FindProperty("combat"), CombatCenter, new Vector2(20f, 12f), new Vector3(0f, 0f, -4.5f), new Vector3(0f, 0f, 7.6f), combatDoor);
            SetSite(runSo.FindProperty("rest"), RestCenter, new Vector2(12f, 10f), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 6.6f), null);
            SetSite(runSo.FindProperty("boss"), BossCenter, new Vector2(16f, 12f), new Vector3(0f, 0f, -4.5f), new Vector3(0f, 0f, 99f), null);
            runSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = new GameObject("PrototypeHud").AddComponent<PrototypeHud>();
            SetRef(hud, "player", player);
            SetRef(hud, "director", director);
            SetRef(hud, "arena", arena);
            SetRef(hud, "run", run);
            var so = new SerializedObject(hud);
            var arr = so.FindProperty("dummies");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = dummyA;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = dummyB;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(RunScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, RunScenePath);
            Debug.Log($"[시험판 씬] {RunScenePath} 를 만들었어요.");
        }

        // 방 껍데기: 바닥, 사방 벽, 북쪽 가운데 3칸 문과 짧은 통로. 잠그는 문(gateMat이 있을 때)을 돌려줘요.
        static GameObject BuildShell(string name, Vector3 center, Vector2 size, Material floorMat, Material wallMat, Material gateMat, bool lockable)
        {
            var room = new GameObject(name).transform;
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Box("Floor", room, center + new Vector3(0f, -0.1f, 0f), new Vector3(size.x + 1f, 0.2f, size.y + 1f), floorMat);
            Box("Wall_S", room, center + new Vector3(0f, 0.75f, -hz - 0.25f), new Vector3(size.x + 1f, 1.5f, 0.5f), wallMat);
            Box("Wall_W", room, center + new Vector3(-hx - 0.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, size.y + 1f), wallMat);
            Box("Wall_E", room, center + new Vector3(hx + 0.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, size.y + 1f), wallMat);
            float side = hx + 0.5f - 1.5f; // 문 양옆 벽 길이
            Box("Wall_N_West", room, center + new Vector3(-1.5f - side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);
            Box("Wall_N_East", room, center + new Vector3(1.5f + side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);

            // 통로 (문 너머 2칸): 여기 들어서면 다음 방
            Box("Corridor_Floor", room, center + new Vector3(0f, -0.1f, hz + 1.25f), new Vector3(3f, 0.2f, 2.5f), floorMat);
            Box("Corridor_W", room, center + new Vector3(-1.75f, 0.75f, hz + 1.5f), new Vector3(0.5f, 1.5f, 2.5f), wallMat);
            Box("Corridor_E", room, center + new Vector3(1.75f, 0.75f, hz + 1.5f), new Vector3(0.5f, 1.5f, 2.5f), wallMat);
            Box("Corridor_N", room, center + new Vector3(0f, 0.75f, hz + 2.75f), new Vector3(4f, 1.5f, 0.5f), wallMat);

            if (!lockable || gateMat == null) return null;
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door (결계)";
            door.transform.SetParent(room, false);
            door.transform.localPosition = center + new Vector3(0f, 0.75f, hz + 0.25f);
            door.transform.localScale = new Vector3(3f, 1.5f, 0.5f);
            door.GetComponent<Renderer>().sharedMaterial = gateMat;
            return door;
        }

        // entry·exit은 방 가운데 기준
        static void SetSite(SerializedProperty site, Vector3 center, Vector2 size, Vector3 entry, Vector3 exit, GameObject door)
        {
            site.FindPropertyRelative("center").vector3Value = center;
            site.FindPropertyRelative("size").vector2Value = size;
            site.FindPropertyRelative("entry").vector3Value = center + entry;
            site.FindPropertyRelative("exit").vector3Value = center + exit;
            site.FindPropertyRelative("door").objectReferenceValue = door;
        }
    }
}
