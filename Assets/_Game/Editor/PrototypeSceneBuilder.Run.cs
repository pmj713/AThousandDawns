using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Data;
using AThousandDawns.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판 구조 씬: 층 방(20×12, 조작 시트 방 크기) · 정비실 · 녹림왕 방을 멀리 떨어뜨려 놓고, 문으로 나가면 옮겨요.
    // 층 방 하나를 층의 모든 방이 같이 써요 (옆방이 없는 쪽 문은 벽으로 막고, 적 구성만 바뀜).
    public static partial class PrototypeSceneBuilder
    {
        public const string RunScenePath = "Assets/_Game/Scenes/Prototype_Run.unity";

        const string PortalColor = "#3A3A46"; // 관문 (임시, 먹색)

        static readonly Vector3 CombatCenter = Vector3.zero;
        static readonly Vector3 RestCenter = new Vector3(60f, 0f, 0f);
        static readonly Vector3 InnCenter = new Vector3(180f, 0f, 0f);
        static readonly Vector3 MasterCenter = new Vector3(240f, 0f, 0f);
        static readonly Vector3 IronMasterCenter = new Vector3(300f, 0f, 0f);
        const string WoodColor = "#7A5536";   // 객잔 계산대 (임시)
        const string ClerkColor = "#D9C9A8";  // 점소이 (임시)
        const string StoneColor = "#6E6A64";  // 철권 마웅 결투장 돌벽 (임시)
        static readonly Vector3 BossCenter = new Vector3(120f, 0f, 0f);
        static readonly Vector2 CombatSize = new Vector2(20f, 12f);

        static void BuildRun(BalanceConfig balance, AThousandDawns.Items.ItemDatabase itemDb)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMat = GetMaterial("Floor", FloorColor, false);
            var wallMat = GetMaterial("Wall", WallColor, false);
            var bambooMat = GetMaterial("Bamboo", BambooColor, false);
            var gateMat = GetMaterial("BossGate", GateColor, false);
            var dummyMat = GetMaterial("Dummy", DummyColor, false);

            // 층 방: 문 넷, 대나무 기둥 6개, 청죽사 풀숲 4곳. 가운데 십자 길(문 ↔ 문)과 관문 자리는 비워 둬요.
            var combatRoom = new GameObject("Room_Floor").transform;
            BuildFourDoorRoom(combatRoom, CombatCenter, CombatSize, floorMat, wallMat, gateMat, out var locks, out var plugs);
            Vector3[] pillars =
            {
                new Vector3(-5.5f, 0f, 3f), new Vector3(5.5f, 0f, -3f), new Vector3(-7f, 0f, -3.5f),
                new Vector3(7f, 0f, 3.5f), new Vector3(3.5f, 0f, 3f), new Vector3(-3.5f, 0f, -3f),
            };
            for (int i = 0; i < pillars.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.name = $"Bamboo_{i + 1}";
                b.transform.SetParent(combatRoom, false);
                b.transform.localPosition = CombatCenter + pillars[i] + Vector3.up * 1.5f;
                b.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
                b.GetComponent<Renderer>().sharedMaterial = bambooMat;
            }
            BuildBushes(GetMaterial("Bush", BushColor, false), new[]
            {
                new Vector3(-7.5f, 0f, 4.3f), new Vector3(7.5f, 0f, -4.3f), new Vector3(-3f, 0f, 4.3f), new Vector3(3f, 0f, -4.3f),
            });

            // 관문: 방 가운데 먹색 원판 (관문 방에서만 켜져요). 충돌체 없음
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portal.name = "Gate (관문)";
            Object.DestroyImmediate(portal.GetComponent<Collider>());
            portal.transform.SetParent(combatRoom, false);
            portal.transform.localPosition = CombatCenter + new Vector3(0f, 0.03f, 0f);
            portal.transform.localScale = new Vector3(2.4f, 0.03f, 2.4f);
            portal.GetComponent<Renderer>().sharedMaterial = GetMaterial("Gate_Portal", PortalColor, false);
            portal.SetActive(false);

            // 객잔: 12×10, 왼쪽 위에 계산대와 점소이. 가운데 길(입구 → 북쪽 문)은 비워 둬요
            BuildShell("Room_Inn", InnCenter, new Vector2(12f, 10f), floorMat, wallMat);
            var innRoom = GameObject.Find("Room_Inn").transform;
            Box("Counter", innRoom, InnCenter + new Vector3(-3.5f, 0.5f, 3f), new Vector3(2.5f, 1f, 0.6f), GetMaterial("Wood", WoodColor, false));
            var clerk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            clerk.name = "점소이";
            clerk.transform.SetParent(innRoom, false);
            clerk.transform.localPosition = InnCenter + new Vector3(-3.5f, 0.8f, 3.9f);
            clerk.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            clerk.GetComponent<Renderer>().sharedMaterial = GetMaterial("Clerk", ClerkColor, false);

            // 고수 결투장 (청죽객): 14×10, 테두리 대나무 숲, 안에 대나무 기둥 6그루 (몬스터 시트 115행)
            var masterRoom = new GameObject("Room_Master").transform;
            Box("Floor", masterRoom, MasterCenter + new Vector3(0f, -0.1f, 0f), new Vector3(15f, 0.2f, 11f), floorMat);
            Box("Bamboo_W", masterRoom, MasterCenter + new Vector3(-7.25f, 1.25f, 0f), new Vector3(0.5f, 2.5f, 10.5f), bambooMat);
            Box("Bamboo_E", masterRoom, MasterCenter + new Vector3(7.25f, 1.25f, 0f), new Vector3(0.5f, 2.5f, 10.5f), bambooMat);
            Box("Bamboo_N", masterRoom, MasterCenter + new Vector3(0f, 1.25f, 5.25f), new Vector3(15f, 2.5f, 0.5f), bambooMat);
            Box("Bamboo_S", masterRoom, MasterCenter + new Vector3(0f, 1.25f, -5.25f), new Vector3(15f, 2.5f, 0.5f), bambooMat);
            Vector3[] masterPillars =
            {
                new Vector3(-4f, 0f, 2.5f), new Vector3(4f, 0f, 2.5f), new Vector3(-4f, 0f, -2.5f),
                new Vector3(4f, 0f, -2.5f), new Vector3(-5.5f, 0f, 0f), new Vector3(5.5f, 0f, 0f),
            };
            for (int i = 0; i < masterPillars.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.name = $"Bamboo_{i + 1}";
                b.transform.SetParent(masterRoom, false);
                b.transform.localPosition = MasterCenter + masterPillars[i] + Vector3.up * 1.5f;
                b.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
                b.GetComponent<Renderer>().sharedMaterial = bambooMat;
            }

            // 철권 마웅 결투장: 14×10, 사방 돌벽 (기둥 없음, 돌진을 벽으로 유도) (몬스터 시트 116행)
            var ironRoom = new GameObject("Room_IronMaster").transform;
            var stoneMat = GetMaterial("Stone", StoneColor, false);
            Box("Floor", ironRoom, IronMasterCenter + new Vector3(0f, -0.1f, 0f), new Vector3(15f, 0.2f, 11f), floorMat);
            Box("Stone_W", ironRoom, IronMasterCenter + new Vector3(-7.25f, 1f, 0f), new Vector3(0.5f, 2f, 10.5f), stoneMat);
            Box("Stone_E", ironRoom, IronMasterCenter + new Vector3(7.25f, 1f, 0f), new Vector3(0.5f, 2f, 10.5f), stoneMat);
            Box("Stone_N", ironRoom, IronMasterCenter + new Vector3(0f, 1f, 5.25f), new Vector3(15f, 2f, 0.5f), stoneMat);
            Box("Stone_S", ironRoom, IronMasterCenter + new Vector3(0f, 1f, -5.25f), new Vector3(15f, 2f, 0.5f), stoneMat);

            BuildVillage(floorMat, wallMat);

            // 정비실: 12×10, 북쪽 문은 늘 열려 있어요
            BuildShell("Room_Rest", RestCenter, new Vector2(12f, 10f), floorMat, wallMat);

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
                GetMaterial("UltimateSword", UltimateSwordColor, false), CombatCenter);
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
            var music = arena.gameObject.AddComponent<RunMusic>();
            foreach (var (field, file) in new[] { ("village", "Village"), ("bamboo", "Bamboo"), ("inn", "Inn"), ("boss", "Boss"), ("result", "Result") })
                SetRef(music, field, AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Game/Audio/BGM/{file}.mp3"));
            AddSfx(arena.gameObject);
            arena.gameObject.AddComponent<SfxHooks>();
            var runSo = new SerializedObject(run);
            runSo.FindProperty("director").objectReferenceValue = director;
            runSo.FindProperty("arena").objectReferenceValue = arena;
            var combat = runSo.FindProperty("combat");
            combat.FindPropertyRelative("center").vector3Value = CombatCenter;
            combat.FindPropertyRelative("size").vector2Value = CombatSize;
            combat.FindPropertyRelative("gateMarker").objectReferenceValue = portal;
            var lockArr = combat.FindPropertyRelative("locks");
            var plugArr = combat.FindPropertyRelative("plugs");
            lockArr.arraySize = 4;
            plugArr.arraySize = 4;
            for (int d = 0; d < 4; d++)
            {
                lockArr.GetArrayElementAtIndex(d).objectReferenceValue = locks[d];
                plugArr.GetArrayElementAtIndex(d).objectReferenceValue = plugs[d];
            }
            SetSite(runSo.FindProperty("master"), MasterCenter, new Vector2(14f, 10f), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 99f));
            SetSite(runSo.FindProperty("ironMaster"), IronMasterCenter, new Vector2(14f, 10f), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 99f));
            SetSite(runSo.FindProperty("inn"), InnCenter, new Vector2(12f, 10f), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 6.6f));
            SetSite(runSo.FindProperty("rest"), RestCenter, new Vector2(12f, 10f), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 6.6f));
            SetSite(runSo.FindProperty("boss"), BossCenter, new Vector2(16f, 12f), new Vector3(0f, 0f, -4.5f), new Vector3(0f, 0f, 99f));
            SetSite(runSo.FindProperty("village"), VillageCenter, VillageSize, VillageEntry, VillageExit);
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

        // 문 넷(북·동·남·서, 가운데 3칸)과 문마다 짧은 통로. 문마다 결계(lock)와 막는 벽(plug)을 하나씩 만들어 꺼 둬요.
        static void BuildFourDoorRoom(Transform room, Vector3 center, Vector2 size, Material floorMat, Material wallMat, Material gateMat,
            out GameObject[] locks, out GameObject[] plugs)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Box("Floor", room, center + new Vector3(0f, -0.1f, 0f), new Vector3(size.x + 1f, 0.2f, size.y + 1f), floorMat);

            float side = (size.x + 1f - 3f) * 0.5f; // 북·남 벽 한쪽 길이
            float sideZ = (size.y + 1f - 3f) * 0.5f; // 동·서 벽 한쪽 길이
            Box("Wall_N_W", room, center + new Vector3(-1.5f - side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);
            Box("Wall_N_E", room, center + new Vector3(1.5f + side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);
            Box("Wall_S_W", room, center + new Vector3(-1.5f - side * 0.5f, 0.75f, -hz - 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);
            Box("Wall_S_E", room, center + new Vector3(1.5f + side * 0.5f, 0.75f, -hz - 0.25f), new Vector3(side, 1.5f, 0.5f), wallMat);
            Box("Wall_E_N", room, center + new Vector3(hx + 0.25f, 0.75f, 1.5f + sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), wallMat);
            Box("Wall_E_S", room, center + new Vector3(hx + 0.25f, 0.75f, -1.5f - sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), wallMat);
            Box("Wall_W_N", room, center + new Vector3(-hx - 0.25f, 0.75f, 1.5f + sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), wallMat);
            Box("Wall_W_S", room, center + new Vector3(-hx - 0.25f, 0.75f, -1.5f - sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), wallMat);

            string[] names = { "N", "E", "S", "W" };
            locks = new GameObject[4];
            plugs = new GameObject[4];
            for (int d = 0; d < 4; d++)
            {
                var dir = FloorMap.Directions[d];
                var out3 = new Vector3(dir.x, 0f, dir.y);
                var across = new Vector3(Mathf.Abs(dir.y), 0f, Mathf.Abs(dir.x)); // 문 너비 방향
                float half = dir.x != 0 ? hx : hz;
                Vector3 door = center + out3 * (half + 0.25f) + Vector3.up * 0.75f;
                Vector3 doorScale = Sized(dir, 0.5f, 3f, 1.5f);

                // 통로 (문 너머 2.5칸): 끝까지 들어서면 옆방
                Vector3 mid = center + out3 * (half + 1.25f);
                Box($"Corridor_{names[d]}_Floor", room, mid + Vector3.down * 0.1f, Sized(dir, 2.5f, 3f, 0.2f), floorMat);
                Box($"Corridor_{names[d]}_A", room, mid + across * 1.75f + Vector3.up * 0.75f, Sized(dir, 2.5f, 0.5f, 1.5f), wallMat);
                Box($"Corridor_{names[d]}_B", room, mid - across * 1.75f + Vector3.up * 0.75f, Sized(dir, 2.5f, 0.5f, 1.5f), wallMat);
                Box($"Corridor_{names[d]}_End", room, center + out3 * (half + 2.75f) + Vector3.up * 0.75f, Sized(dir, 0.5f, 4f, 1.5f), wallMat);

                plugs[d] = MakeBlock($"Door_{names[d]} (벽)", room, door, doorScale, wallMat);
                locks[d] = MakeBlock($"Door_{names[d]} (결계)", room, door, doorScale, gateMat);
                locks[d].SetActive(false);
            }
        }

        // 문 방향(out)으로 alongOut, 옆(across)으로 alongAcross, 높이 height인 상자 크기
        static Vector3 Sized(Vector2Int dir, float alongOut, float alongAcross, float height) =>
            dir.x != 0 ? new Vector3(alongOut, height, alongAcross) : new Vector3(alongAcross, height, alongOut);

        static GameObject MakeBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // 방 껍데기: 바닥, 사방 벽, 북쪽 가운데 3칸 문(늘 열림)과 짧은 통로.
        static void BuildShell(string name, Vector3 center, Vector2 size, Material floorMat, Material wallMat)
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
        }

        // ── 마을 (24×16): 객잔 마루(시작) · 매화나무(회귀록) · 뒷산 동굴(면벽수련) · 사당(유품록) · 마을 어귀(무기 거치대, 출발)
        static readonly Vector3 VillageCenter = new Vector3(360f, 0f, 0f);
        static readonly Vector2 VillageSize = new Vector2(24f, 16f);
        static readonly Vector3 VillageEntry = new Vector3(-7f, 0f, 0f);  // 객잔 마루
        static readonly Vector3 VillageExit = new Vector3(5f, 0f, -7.6f); // 마을 어귀 (남쪽 문)
        const string VillageFloorColor = "#EFE6D2"; // 색감 시트 마을 바탕
        const string VillageInkColor = "#4A4540";   // 마을 그림자
        const string PlumColor = "#C8463C";         // 매화 홍
        const string CaveColor = "#2E2B28";

        static void BuildVillage(Material floorMat, Material wallMat)
        {
            var root = new GameObject("Village").transform;
            var c = VillageCenter;
            float hx = VillageSize.x * 0.5f, hz = VillageSize.y * 0.5f;
            var ground = GetMaterial("VillageGround", VillageFloorColor, false);
            var ink = GetMaterial("VillageInk", VillageInkColor, false);
            var wood = GetMaterial("Wood", WoodColor, false);

            Box("Floor", root, c + new Vector3(0f, -0.1f, 0f), new Vector3(VillageSize.x + 1f, 0.2f, VillageSize.y + 1f), ground);
            Box("Wall_N", root, c + new Vector3(0f, 0.75f, hz + 0.25f), new Vector3(VillageSize.x + 1f, 1.5f, 0.5f), wallMat);
            Box("Wall_W", root, c + new Vector3(-hx - 0.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, VillageSize.y + 1f), wallMat);
            Box("Wall_E", root, c + new Vector3(hx + 0.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, VillageSize.y + 1f), wallMat);
            // 남쪽 벽: 마을 어귀(x = 5) 자리 3칸만 비워요
            float gx = VillageExit.x, left = gx - 1.5f + hx, right = hx - (gx + 1.5f);
            Box("Wall_S_West", root, c + new Vector3(-hx + left * 0.5f, 0.75f, -hz - 0.25f), new Vector3(left + 0.5f, 1.5f, 0.5f), wallMat);
            Box("Wall_S_East", root, c + new Vector3(hx - right * 0.5f, 0.75f, -hz - 0.25f), new Vector3(right + 0.5f, 1.5f, 0.5f), wallMat);
            Box("Gate_Post_W", root, c + new Vector3(gx - 1.7f, 1.5f, -hz - 0.25f), new Vector3(0.4f, 3f, 0.4f), wood);
            Box("Gate_Post_E", root, c + new Vector3(gx + 1.7f, 1.5f, -hz - 0.25f), new Vector3(0.4f, 3f, 0.4f), wood);
            Box("Gate_Beam", root, c + new Vector3(gx, 3f, -hz - 0.25f), new Vector3(4f, 0.3f, 0.5f), wood);

            // 객잔 마루 (서쪽): 건물 벽과 마루
            Box("Inn_Building", root, c + new Vector3(-hx + 1.5f, 1.5f, 0f), new Vector3(3f, 3f, 6f), wood);
            Box("Inn_Porch", root, c + new Vector3(-hx + 3.6f, 0.05f, 0f), new Vector3(1.4f, 0.1f, 4f), wood);

            // 매화나무 (객잔 입구 바로 앞)
            var tree = c + RoomRun.TreeOffset;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Plum_Trunk";
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = tree + new Vector3(0f, 1f, 1f);
            trunk.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
            trunk.GetComponent<Renderer>().sharedMaterial = ink;
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Plum_Blossom";
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            crown.transform.SetParent(root, false);
            crown.transform.localPosition = tree + new Vector3(0f, 2.4f, 1f);
            crown.transform.localScale = new Vector3(2.4f, 1.4f, 2.4f);
            crown.GetComponent<Renderer>().sharedMaterial = GetMaterial("Plum", PlumColor, false);

            // 뒷산 동굴 (북쪽): 산자락과 어두운 입구
            Box("Hill", root, c + RoomRun.CaveOffset + new Vector3(0f, 1.5f, 1.4f), new Vector3(6f, 3f, 1.2f), wallMat);
            Box("Cave_Mouth", root, c + RoomRun.CaveOffset + new Vector3(0f, 0.9f, 0.75f), new Vector3(1.8f, 1.8f, 0.2f), GetMaterial("Cave", CaveColor, false));

            // 사당 (어귀 옆, 남서쪽)
            Box("Shrine", root, c + RoomRun.ShrineOffset + new Vector3(-1f, 0.9f, -1f), new Vector3(1.6f, 1.8f, 1.2f), wood);
            Box("Shrine_Roof", root, c + RoomRun.ShrineOffset + new Vector3(-1f, 1.95f, -1f), new Vector3(2.2f, 0.3f, 1.8f), ink);

            // 무기 거치대 (어귀 앞)
            Box("Weapon_Rack", root, c + RoomRun.RackOffset + new Vector3(0f, 0.6f, -0.6f), new Vector3(1.6f, 1.2f, 0.25f), wood);
        }

        // entry·exit은 방 가운데 기준
        static void SetSite(SerializedProperty site, Vector3 center, Vector2 size, Vector3 entry, Vector3 exit)
        {
            site.FindPropertyRelative("center").vector3Value = center;
            site.FindPropertyRelative("size").vector2Value = size;
            site.FindPropertyRelative("entry").vector3Value = center + entry;
            site.FindPropertyRelative("exit").vector3Value = center + exit;
        }
    }
}
