using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판: 원래 시험판(Prototype_Run)을 그대로 만든 뒤 겉모습만 수묵으로 바꿔 다른 이름으로 저장해요.
    // 게임 규칙·충돌·배치는 원래 빌더 그대로라 (상자 충돌체는 남기고 렌더러만 바꿈) 실제 전투에서 읽히는지 확인하는 용도예요.
    // 원래 빌더 파일은 건드리지 않아요 (다른 작업과 충돌 없게).
    public static partial class PrototypeSceneBuilder
    {
        public const string InkRunScenePath = "Assets/_Game/Scenes/Prototype_Run_Ink.unity";

        [MenuItem("AThousandDawns/시험판 씬 만들기 (수묵)")]
        public static void BuildInkRunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInkRun();
        }

        // 배치 모드: -executeMethod AThousandDawns.EditorTools.PrototypeSceneBuilder.BuildInkRunBatch
        public static void BuildInkRunBatch()
        {
            BuildInkRun();
            EditorApplication.Exit(0);
        }

        public static void BuildInkRun()
        {
            Build(); // 원래 시험판 씬 둘 (Prototype_Run · Prototype_Combat)
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            DressRunInk();
            EditorSceneManager.SaveScene(scene, InkRunScenePath);

            // 처음부터(F5)·재시작이 빌드 순번으로 씬을 다시 불러서 빌드 목록에 넣어 둬요
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != InkRunScenePath)) scenes.Add(new EditorBuildSettingsScene(InkRunScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[수묵 시험판] {InkRunScenePath} 를 만들었어요.");
        }

        static void DressRunInk()
        {
            ImportInkArt();
            var (floorMat, stoneMat, woodMat, bambooMat) = GetInkEnvMaterials();
            int seed = 0;

            // 1) 대나무: 원기둥 기둥은 줄기 묶음으로, 상자 울타리는 줄기를 줄지어 세워요. 원래 상자는 충돌용으로 남기고 안 보이게
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers.Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "Bamboo").ToList())
            {
                var t = r.transform;
                r.enabled = false;
                bool pillar = r.GetComponent<MeshFilter>().sharedMesh.name == "Cylinder";
                if (pillar)
                {
                    var foot = t.parent.InverseTransformPoint(new Vector3(t.position.x, 0f, t.position.z));
                    StripColliders(BambooCluster(t.parent, foot, 4f, seed++, bambooMat));
                    continue;
                }
                Vector3 size = t.lossyScale;
                bool alongX = size.x >= size.z;
                float length = alongX ? size.x : size.z;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                Vector3 across = alongX ? Vector3.forward : Vector3.right;
                float thickness = alongX ? size.z : size.x;
                for (float s = -length * 0.5f + 0.4f; s <= length * 0.5f - 0.3f; s += 0.9f, seed++)
                {
                    float jitter = ((seed * 37 % 10) / 10f - 0.5f) * thickness * 0.6f;
                    var world = new Vector3(t.position.x, 0f, t.position.z) + axis * s + across * jitter;
                    StripColliders(BambooCluster(t.parent, t.parent.InverseTransformPoint(world), 5f + (seed % 4) * 0.4f, seed, bambooMat));
                }
            }

            // 2) 청죽사 풀숲: 흔들리는 Visual 밑에 수묵 덤불을 달아 같이 흔들려요 (Visual의 납작한 크기는 되돌려서)
            foreach (var bush in Object.FindObjectsByType<AmbushBush>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bush.transform.childCount == 0) continue;
                var visual = bush.transform.GetChild(0);
                var vr = visual.GetComponent<Renderer>();
                if (vr != null) vr.enabled = false;
                Vector3 vs = visual.localScale;
                var clump = BushClump(visual, new Vector3(0f, -visual.localPosition.y / vs.y, 0f), seed++);
                clump.localScale = new Vector3(1f / vs.x, 1f / vs.y, 1f / vs.z);
            }

            // 3) 층 방 둘레: 벽 너머 대나무 숲과 북쪽 먼 풍경 (보기용, 충돌 없음)
            float hx = CombatSize.x * 0.5f, hz = CombatSize.y * 0.5f;
            var deco = new GameObject("Ink Deco (Room_Floor)").transform;
            deco.position = CombatCenter;
            BuildEnv3DForest(deco, hx, hz, 1.75f, bambooMat);
            StripColliders(deco);
            Billboard(deco, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", CombatCenter + new Vector3(0f, 0f, hz + 6f), 22f);

            // 4) 재질 바꿔 끼우기: 씬의 모든 재질 참조(렌더러·적 연출 설정 등)를 이름으로 찾아 수묵 재질로.
            //    공격 예고·불·검기·결계·관문은 전투에서 읽혀야 하니 그대로 둬요
            //    시험판 재질 폴더(MaterialFolder)의 것만 바꿔요 (방금 단 수묵 그림·기본 스프라이트 재질은 그대로).
            //    대나무·풀숲 원래 모양은 이미 안 보이게 했으니 건너뛰어요
            var keep = new HashSet<string> { "Telegraph_Base", "Telegraph_Fill", "Telegraph_Outline", "Fire", "SwingFlash", "UltimateSword", "BossGate", "Gate_Portal", "Bamboo", "Bush" };
            var swapped = new Dictionary<Material, Material>();
            Material InkFor(Material m)
            {
                if (m == null || keep.Contains(m.name)) return m;
                if (!AssetDatabase.GetAssetPath(m).StartsWith(MaterialFolder + "/")) return m;
                if (swapped.TryGetValue(m, out var done)) return done;
                Material ink;
                switch (m.name)
                {
                    case "Floor": ink = floorMat; break;
                    case "Wall": case "Stone": ink = stoneMat; break;
                    case "Wood": ink = woodMat; break;
                    default:
                        // 사람·적·소품: 원래 색을 바탕으로 수묵 캐릭터 재질. 적은 종류가 구분되게 색을 조금 남겨요
                        float saturation = m.name == "Player" ? 0.75f : m.name.StartsWith("Enemy_") ? 0.45f : 0.3f;
                        string hex = "#" + ColorUtility.ToHtmlStringRGB(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.gray);
                        ink = GetInkMaterial($"Ink_{m.name}", hex, saturation);
                        break;
                }
                swapped[m] = ink;
                return ink;
            }

            // 플레이어 코(바라보는 방향 표시)는 벽 재질을 같이 써서 돌 무늬가 되지 않게 따로
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player != null)
            {
                var noseInk = GetInkMaterial("Ink_PlayerNose", WallColor, 0.3f);
                foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.name == "Wall") r.sharedMaterial = noseInk;
            }

            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform) continue;
                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (!(it.objectReferenceValue is Material m)) continue;
                        var ink = InkFor(m);
                        if (ink == m) continue;
                        it.objectReferenceValue = ink;
                        changed = true;
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }

            // 5) 한지색 배경과 원근 안개
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                ApplyPaperAtmosphere(cam);
            }
        }

        static void StripColliders(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
    }
}
