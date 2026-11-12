using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 천마총 이벤트 방 가운데 소품 프리팹 (RoomRun.TombEvents가 방 가운데에 세워요). 바닥 기준 원점, 충돌체 없음.
    //  - TombEvent_BloodPool: 무덤 바닥이 꺼진 자리의 검붉은 피 웅덩이. 깨진 대리석 판이 웅덩이 쪽으로 기울어 빠져들고, 해골이 떠 있어요
    //  - TombEvent_DemonAltar: 계단 위 마교 제단. 마두 석상을 등지고 향로에서 핏빛 향이 오르고, 녹아내린 무기가 공물로 쌓였어요
    //  - TombEvent_CaptiveHero: 쇠사슬에 묶여 무릎 꿇은 무림맹 고수 (모델이 들어오면 만들어요)
    public static partial class PrototypeSceneBuilder
    {
        public const string TombEventFolder = "Assets/_Game/Art/Env/Tomb/Events";

        static void BuildTombEventPrefabs(Material stoneMat, Material roofX)
        {
            EnsureFolder(TombEventFolder);
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var blood = GetInkMaterial("Ink_Tomb_Blood", "#4E0B0A", 0.7f);
            var bone = GetInkMaterial("Ink_Tomb_Bone", "#CFC6B0", 0.2f);
            var iron = GetInkMaterial("Ink_Tomb_Iron", "#2E3236", 0.1f);
            var hot = GetInkMaterial("Ink_Tomb_HotIron", "#C2410C", 0.9f);   // 녹아 달아오른 쇠
            SaveTombEvent(BloodPoolEvent(marble, blood, bone, stoneMat), "TombEvent_BloodPool");
            SaveTombEvent(DemonAltarEvent(marble, red, blood, bone, iron, hot, stoneMat, roofX), "TombEvent_DemonAltar");
            BuildCaptiveHeroEvent(stoneMat, iron);
        }

        // 갇힌 무림맹 고수: 무릎 꿇고 손이 등 뒤로 묶인 고수(Captive 반복) + 뒤의 돌기둥 둘에서 손목으로 이어진 쇠사슬("Chains" 자식).
        //   구출하면 코드가 애니메이터 트리거 "Free"를 켜고 "Chains"를 끄면 돼요 → 손을 풀고 일어나 포권(Freed, 끝 자세에서 멈춤)
        static void BuildCaptiveHeroEvent(Material stone, Material iron)
        {
            var (modelPrefab, _, _) = SetupEnemy("CaptiveHero", 1f);
            if (modelPrefab == null) return;   // 모델이 아직 없으면 건너뛰어요
            string modelPath = $"{CharactersFolder}/CaptiveHero/CaptiveHero_Rigged.fbx";
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (!clips.TryGetValue("Captive", out var captive) || !clips.TryGetValue("Freed", out var freed)) return;
            string controllerPath = $"{TombEventFolder}/CaptiveHero_Event.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Free", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var held = sm.AddState("Captive"); held.motion = captive;
            var free = sm.AddState("Freed"); free.motion = freed;
            sm.defaultState = held;
            var go = held.AddTransition(free);
            go.hasExitTime = false; go.duration = 0.15f;
            go.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Free");

            var root = new GameObject("TombEvent_CaptiveHero (갇힌 무림맹 고수)");
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            PrefabUtility.UnpackPrefabInstance(hero, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            hero.name = "Hero";
            hero.transform.SetParent(root.transform, false);
            hero.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 방 입구(남쪽)를 봐요
            var animator = hero.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var chains = new GameObject("Chains").transform;
            chains.SetParent(root.transform, false);
            Vector3 wrists = new Vector3(0f, 0.65f, 0.3f);   // 무릎 꿇은 고수의 등 뒤 손목
            for (int s = -1; s <= 1; s += 2)
            {
                var post = new Vector3(s * 0.75f, 0f, 0.75f);
                Block(chains, "Post", PrimitiveType.Cube, post + Vector3.up * 0.7f, new Vector3(0.3f, 1.4f, 0.3f), Quaternion.identity, stone);
                var from = post + Vector3.up * 1.05f;
                var link = new GameObject("Chain");
                link.transform.SetParent(chains, false);
                link.transform.localPosition = (from + wrists) * 0.5f;
                link.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(wrists - from, Vector3.up)) ;
                link.transform.localScale = new Vector3((wrists - from).magnitude / 1.4f, 0.4f, 1f);
                link.AddComponent<MeshFilter>().sharedMesh = GetHangingChainMesh();
                link.AddComponent<MeshRenderer>().sharedMaterial = iron;
            }
            Block(chains, "Shackle", PrimitiveType.Cylinder, wrists, new Vector3(0.18f, 0.05f, 0.18f), Quaternion.Euler(90f, 0f, 0f), iron);
            SaveTombEvent(root, "TombEvent_CaptiveHero");
        }

        static void SaveTombEvent(GameObject root, string name)
        {
            StripColliders(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, $"{TombEventFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        // 피의 연못 (지름 약 4.4): 꺼진 바닥 둘레의 깨진 판석, 검붉은 수면, 떠 있는 해골·뼈, 가장자리로 흘러넘친 핏자국
        static GameObject BloodPoolEvent(Material marble, Material blood, Material bone, Material stone)
        {
            var root = new GameObject("TombEvent_BloodPool (피의 연못)");
            var t = root.transform;
            Block(t, "Pit Shadow", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 0f), new Vector3(4.6f, 0.005f, 4.2f), Quaternion.identity, marble);
            Block(t, "Blood Surface", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(3.8f, 0.01f, 3.4f), Quaternion.identity, blood);
            for (int k = 0; k < 10; k++)   // 웅덩이 쪽으로 기울어 빠져드는 깨진 판석
            {
                float a = k * 36f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = 2.0f + (k % 3) * 0.12f;
                var rot = Quaternion.LookRotation(-dir) * Quaternion.Euler(-(10f + k % 4 * 5f), 0f, (k % 2 == 0 ? 6f : -5f));
                Block(t, "Sunken Slab", PrimitiveType.Cube, dir * r + Vector3.up * 0.05f, new Vector3(0.9f + k % 2 * 0.3f, 0.12f, 0.8f), rot, marble);
            }
            for (int k = 0; k < 6; k++)   // 가장자리 돌 부스러기
            {
                float a = (k * 60f + 20f) * Mathf.Deg2Rad;
                Block(t, "Rubble", PrimitiveType.Cube, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.55f + Vector3.up * 0.08f, Vector3.one * (0.18f + k % 3 * 0.06f), Quaternion.Euler(12f, k * 47f, 8f), stone);
            }
            Block(t, "Floating Skull", PrimitiveType.Sphere, new Vector3(0.6f, 0.08f, -0.4f), new Vector3(0.3f, 0.26f, 0.32f), Quaternion.Euler(-20f, 30f, 0f), bone);
            Block(t, "Rib", PrimitiveType.Cylinder, new Vector3(-0.7f, 0.04f, 0.5f), new Vector3(0.05f, 0.35f, 0.05f), Quaternion.Euler(0f, 0f, 80f), bone);
            Block(t, "Rib", PrimitiveType.Cylinder, new Vector3(-0.55f, 0.04f, 0.65f), new Vector3(0.05f, 0.3f, 0.05f), Quaternion.Euler(0f, 30f, 82f), bone);
            Block(t, "Bubble", PrimitiveType.Sphere, new Vector3(-0.2f, 0.03f, -0.9f), Vector3.one * 0.14f, Quaternion.identity, blood);
            Block(t, "Bubble", PrimitiveType.Sphere, new Vector3(0.9f, 0.03f, 0.6f), Vector3.one * 0.1f, Quaternion.identity, blood);
            for (int k = 0; k < 3; k++)   // 넘쳐 흐른 핏자국
            {
                float a = (k * 120f + 40f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Block(t, "Overflow", PrimitiveType.Cube, dir * 2.7f + Vector3.up * 0.012f, new Vector3(0.35f, 0.01f, 1.1f), Quaternion.LookRotation(dir), blood);
            }
            return root;
        }

        // 마교 제단: 계단 둘 위 검은 석대, 뒤에 마두 석상, 가운데 향로에서 오르는 핏빛 향(붉은 카드), 녹아내린 무기 더미, 촛대
        static GameObject DemonAltarEvent(Material marble, Material red, Material blood, Material bone, Material iron, Material hot, Material stone, Material roofX)
        {
            var root = new GameObject("TombEvent_DemonAltar (마교 제단)");
            var t = root.transform;
            Block(t, "Step 1", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.2f), new Vector3(3.6f, 0.3f, 2.4f), Quaternion.identity, marble);
            Block(t, "Step 2", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.35f), new Vector3(2.8f, 0.24f, 1.8f), Quaternion.identity, marble);
            Block(t, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.45f), new Vector3(2.2f, 0.8f, 1.0f), Quaternion.identity, marble);
            Block(t, "Altar Cloth", PrimitiveType.Cube, new Vector3(0f, 1.05f, -0.06f), new Vector3(1.2f, 0.65f, 0.02f), Quaternion.identity, red);
            DemonHead(t, new Vector3(0f, 0.54f, 1.15f), 3, stone, red);   // 제단 뒤 마두 석상
            // 향로와 핏빛 향 (가는 붉은 카드 셋이 꼬여 올라가요)
            Block(t, "Censer", PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0.4f), new Vector3(0.45f, 0.18f, 0.45f), Quaternion.identity, iron);
            for (int s = -1; s <= 1; s++)
                Block(t, "Incense", PrimitiveType.Cylinder, new Vector3(s * 0.1f, 1.75f, 0.4f), new Vector3(0.02f, 0.18f, 0.02f), Quaternion.Euler(0f, 0f, s * 8f), red);
            for (int k = 0; k < 3; k++)
                Block(t, "Blood Smoke", PrimitiveType.Cube, new Vector3(0.06f * (k - 1), 2.2f + k * 0.35f, 0.4f), new Vector3(0.12f - k * 0.02f, 0.5f, 0.01f), Quaternion.Euler(0f, k * 60f, (k - 1) * 18f), blood);
            // 녹아내린 무기 공물: 휘고 늘어진 칼날, 밑동에 달아오른 쇳물 웅덩이
            Block(t, "Molten Pool", PrimitiveType.Cylinder, new Vector3(-0.75f, 1.36f, 0.3f), new Vector3(0.6f, 0.02f, 0.45f), Quaternion.identity, hot);
            Block(t, "Bent Blade", PrimitiveType.Cube, new Vector3(-0.85f, 1.55f, 0.3f), new Vector3(0.05f, 0.45f, 0.1f), Quaternion.Euler(0f, 20f, 35f), iron);
            Block(t, "Drooping Blade", PrimitiveType.Cube, new Vector3(-0.6f, 1.45f, 0.45f), new Vector3(0.05f, 0.3f, 0.08f), Quaternion.Euler(60f, 0f, -20f), hot);
            Block(t, "Spear Head", PrimitiveType.Cube, new Vector3(-0.95f, 1.4f, 0.5f), new Vector3(0.06f, 0.06f, 0.35f), Quaternion.Euler(0f, 45f, 10f), iron);
            Block(t, "Melted Lump", PrimitiveType.Sphere, new Vector3(-0.7f, 1.4f, 0.2f), new Vector3(0.22f, 0.12f, 0.18f), Quaternion.identity, hot);
            // 오른쪽 해골 공물과 촛대
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(0.75f, 1.48f, 0.3f), new Vector3(0.24f, 0.22f, 0.26f), Quaternion.identity, bone);
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Candle", PrimitiveType.Cylinder, new Vector3(s * 0.95f, 1.5f, 0.75f), new Vector3(0.1f, 0.15f, 0.1f), Quaternion.identity, bone);
                FlameCards(t, new Vector3(s * 0.95f, 1.75f, 0.75f), 0.2f, 1, s * 13f);
            }
            // 제단 양옆 붉은 등불 기둥
            for (int s = -1; s <= 1; s += 2)
                LanternPillar(t, new Vector3(s * 2.1f, 0f, 0.6f), 5 + s, stone, red, roofX);
            return root;
        }
    }
}
