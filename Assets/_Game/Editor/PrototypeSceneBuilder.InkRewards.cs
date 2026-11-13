using UnityEditor;
using UnityEngine;
using RewardKind = AThousandDawns.Prototype.RoomRun.RewardKind;

namespace AThousandDawns.EditorTools
{
    // 보상 방 가운데 보상의 수묵 소품 (InkRunProps가 게임의 금빛 구 "기물 주머니"에 달고, 그 방 보상 종류만 켜요).
    // 바닥 기준 원점, 너비 0.6~0.8 남짓, 앞(-Z)이 카메라 쪽. 충돌체 없음 (게임 구도 충돌체가 없어요).
    //  - 기물: 비단 주머니 (원래 모델, InkRunProps가 그대로 써요)
    //  - 운기조식: 둥근 방석과 향로, 향 셋 · 부품: 쇠띠 두른 나무 궤짝, 뚜껑이 조금 열려요
    //  - 대장간: 돌 화로에 달군 숯, 기대 세운 망치 · 은자: 말굽은 더미와 꿰미 엽전
    //  - 수련: 돌에 꽂힌 검과 매단 천 · 잡화: 끈으로 묶은 봇짐과 지팡이 · 비급: 두루마리 더미와 펼친 두루마리 하나
    public static partial class PrototypeSceneBuilder
    {
        const string RewardFolder = "Assets/_Game/Art/Env/Rewards";

        // RewardKind 순서대로 (기물 자리는 비워 두면 비단 주머니를 써요)
        static GameObject[] BuildRewardModels(Material stone)
        {
            EnsureFolder(RewardFolder);
            var wood = GetInkMaterial("Ink_Reward_Wood", "#6B4A2E", 0.45f);
            var dark = GetInkMaterial("Ink_Reward_Dark", "#2B2622", 0.1f);
            var brass = GetInkMaterial("Ink_Reward_Brass", "#B08A3E", 0.6f);
            var silver = GetInkMaterial("Ink_Reward_Silver", "#D8D8D2", 0.1f);
            var cloth = GetInkMaterial("Ink_Reward_Cloth", "#3E4E6E", 0.6f);    // 쪽빛 봇짐·방석
            var red = GetInkMaterial("Ink_Reward_Red", "#9C2A20", 0.8f);
            var ember = GetInkMaterial("Ink_Reward_Ember", "#E0561C", 1f);      // 달군 숯
            var paper = GetInkMaterial("Ink_Reward_Paper", "#E6D9B8", 0.3f);

            var models = new GameObject[8];
            models[(int)RewardKind.Heal] = SaveReward(RewardHeal(cloth, brass, dark, red), "Reward_Heal");
            models[(int)RewardKind.Part] = SaveReward(RewardChest(wood, brass, dark), "Reward_Part");
            models[(int)RewardKind.Forge] = SaveReward(RewardForge(stone, ember, dark, wood), "Reward_Forge");
            models[(int)RewardKind.Silver] = SaveReward(RewardSilver(silver, brass, red), "Reward_Silver");
            models[(int)RewardKind.Training] = SaveReward(RewardTraining(stone, silver, dark, red), "Reward_Training");
            models[(int)RewardKind.Supply] = SaveReward(RewardSupply(cloth, wood, brass), "Reward_Supply");
            models[(int)RewardKind.Scroll] = SaveReward(RewardScrolls(paper, wood, red), "Reward_Scroll");
            return models;
        }

        static GameObject SaveReward(GameObject root, string name)
        {
            StripColliders(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{RewardFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject RewardHeal(Material cloth, Material brass, Material dark, Material red)
        {
            var t = new GameObject("Reward Heal (운기조식 방석·향로)").transform;
            Block(t, "Cushion", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0.05f), new Vector3(0.62f, 0.07f, 0.62f), Quaternion.identity, cloth);
            Block(t, "Cushion Top", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.05f), new Vector3(0.56f, 0.08f, 0.56f), Quaternion.identity, cloth);
            Block(t, "Burner", PrimitiveType.Cylinder, new Vector3(0.38f, 0.1f, -0.22f), new Vector3(0.2f, 0.08f, 0.2f), Quaternion.identity, brass);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Burner Ear", PrimitiveType.Cube, new Vector3(0.38f + s * 0.11f, 0.17f, -0.22f), new Vector3(0.03f, 0.06f, 0.05f), Quaternion.identity, brass);
            Block(t, "Ash", PrimitiveType.Cylinder, new Vector3(0.38f, 0.185f, -0.22f), new Vector3(0.17f, 0.005f, 0.17f), Quaternion.identity, dark);
            for (int k = 0; k < 3; k++)
                Block(t, "Incense", PrimitiveType.Cylinder, new Vector3(0.35f + k * 0.03f, 0.3f, -0.22f), new Vector3(0.012f, 0.12f, 0.012f), Quaternion.Euler(0f, 0f, (k - 1) * 8f), red);
            return t.gameObject;
        }

        static GameObject RewardChest(Material wood, Material brass, Material dark)
        {
            var t = new GameObject("Reward Part (부품 궤짝)").transform;
            Block(t, "Box", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.45f), Quaternion.identity, wood);
            Block(t, "Inside", PrimitiveType.Cube, new Vector3(0f, 0.395f, 0f), new Vector3(0.64f, 0.01f, 0.39f), Quaternion.identity, dark);
            Block(t, "Lid", PrimitiveType.Cube, new Vector3(0f, 0.47f, 0.17f), new Vector3(0.72f, 0.06f, 0.47f), Quaternion.Euler(-24f, 0f, 0f), wood);   // 뒤로 조금 열려 있어요
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Band", PrimitiveType.Cube, new Vector3(s * 0.25f, 0.2f, 0f), new Vector3(0.05f, 0.41f, 0.46f), Quaternion.identity, brass);
            Block(t, "Lock", PrimitiveType.Cube, new Vector3(0f, 0.3f, -0.235f), new Vector3(0.1f, 0.12f, 0.02f), Quaternion.identity, brass);
            Block(t, "Gear", PrimitiveType.Cylinder, new Vector3(-0.08f, 0.43f, -0.05f), new Vector3(0.16f, 0.02f, 0.16f), Quaternion.Euler(70f, 0f, 0f), brass);   // 비치는 부품
            return t.gameObject;
        }

        static GameObject RewardForge(Material stone, Material ember, Material dark, Material wood)
        {
            var t = new GameObject("Reward Forge (대장간 화로)").transform;
            Block(t, "Bowl", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.6f, 0.16f, 0.6f), Quaternion.identity, stone);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                Block(t, "Foot", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.24f, 0.04f, Mathf.Sin(a) * 0.24f), new Vector3(0.1f, 0.08f, 0.1f), Quaternion.Euler(0f, k * 120f, 0f), stone);
            }
            for (int k = 0; k < 6; k++)   // 달군 숯
                Block(t, "Coal", PrimitiveType.Cube, new Vector3(((k * 37) % 5 - 2) * 0.07f, 0.33f, ((k * 13) % 5 - 2) * 0.06f), Vector3.one * (0.08f + k % 2 * 0.03f), Quaternion.Euler(k * 30f, k * 47f, k * 11f), k % 3 == 0 ? dark : ember);
            Block(t, "Hammer Handle", PrimitiveType.Cube, new Vector3(0.36f, 0.24f, -0.18f), new Vector3(0.04f, 0.46f, 0.04f), Quaternion.Euler(0f, 0f, 22f), wood);
            Block(t, "Hammer Head", PrimitiveType.Cube, new Vector3(0.28f, 0.45f, -0.18f), new Vector3(0.16f, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, 22f), dark);
            return t.gameObject;
        }

        static GameObject RewardSilver(Material silver, Material brass, Material red)
        {
            var t = new GameObject("Reward Silver (은괴·엽전)").transform;
            // 말굽은: 납작한 몸에 양끝이 치켜 올라간 모양 (상자 셋)
            Vector3[] spots = { new Vector3(-0.15f, 0f, 0.05f), new Vector3(0.14f, 0f, 0.08f), new Vector3(0f, 0.12f, 0.06f), new Vector3(-0.02f, 0f, -0.18f) };
            for (int k = 0; k < spots.Length; k++)
            {
                var ingot = new GameObject("Ingot").transform;
                ingot.SetParent(t, false);
                ingot.localPosition = spots[k];
                ingot.localRotation = Quaternion.Euler(0f, k * 37f, 0f);
                Block(ingot, "Body", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.1f, 0.12f), Quaternion.identity, silver);
                for (int s = -1; s <= 1; s += 2)
                    Block(ingot, "Wing", PrimitiveType.Cube, new Vector3(s * 0.11f, 0.1f, 0f), new Vector3(0.08f, 0.06f, 0.13f), Quaternion.Euler(0f, 0f, -s * 30f), silver);
            }
            // 꿰미 엽전: 붉은 끈에 꿴 동전 줄
            for (int k = 0; k < 7; k++)
                Block(t, "Coin", PrimitiveType.Cylinder, new Vector3(0.3f, 0.05f, -0.15f + k * 0.04f), new Vector3(0.09f, 0.01f, 0.09f), Quaternion.Euler(90f, 0f, 0f), brass);
            Block(t, "String", PrimitiveType.Cube, new Vector3(0.3f, 0.05f, -0.03f), new Vector3(0.015f, 0.015f, 0.34f), Quaternion.identity, red);
            return t.gameObject;
        }

        static GameObject RewardTraining(Material stone, Material steel, Material dark, Material red)
        {
            var t = new GameObject("Reward Training (돌에 꽂힌 검)").transform;
            Rock(t, "Stone", Vector3.zero, 0.45f, 2, 30f, stone, false);
            Block(t, "Blade", PrimitiveType.Cube, new Vector3(0.02f, 0.45f, 0f), new Vector3(0.05f, 0.62f, 0.012f), Quaternion.Euler(0f, 0f, 6f), steel);
            Block(t, "Guard", PrimitiveType.Cube, new Vector3(0.05f, 0.76f, 0f), new Vector3(0.16f, 0.03f, 0.04f), Quaternion.Euler(0f, 0f, 6f), dark);
            Block(t, "Grip", PrimitiveType.Cube, new Vector3(0.06f, 0.86f, 0f), new Vector3(0.035f, 0.18f, 0.035f), Quaternion.Euler(0f, 0f, 6f), dark);
            Block(t, "Ribbon", PrimitiveType.Cube, new Vector3(0.1f, 0.78f, -0.02f), new Vector3(0.02f, 0.26f, 0.05f), Quaternion.Euler(0f, 0f, -14f), red);   // 매단 붉은 천
            return t.gameObject;
        }

        static GameObject RewardSupply(Material cloth, Material wood, Material brass)
        {
            var t = new GameObject("Reward Supply (봇짐)").transform;
            Block(t, "Bundle", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.38f, 0.45f), Quaternion.Euler(0f, 15f, 0f), cloth);
            Block(t, "Knot", PrimitiveType.Sphere, new Vector3(0f, 0.4f, -0.05f), new Vector3(0.16f, 0.1f, 0.14f), Quaternion.identity, cloth);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Ear", PrimitiveType.Cube, new Vector3(s * 0.08f, 0.44f, -0.06f), new Vector3(0.1f, 0.04f, 0.08f), Quaternion.Euler(0f, 0f, s * 30f), cloth);
            Block(t, "Staff", PrimitiveType.Cylinder, new Vector3(0.32f, 0.36f, 0.05f), new Vector3(0.035f, 0.38f, 0.035f), Quaternion.Euler(0f, 0f, -18f), wood);
            Block(t, "Gourd", PrimitiveType.Sphere, new Vector3(-0.3f, 0.09f, -0.12f), new Vector3(0.12f, 0.16f, 0.12f), Quaternion.identity, brass);   // 매단 호리병
            return t.gameObject;
        }

        static GameObject RewardScrolls(Material paper, Material wood, Material red)
        {
            var t = new GameObject("Reward Scroll (두루마리 더미)").transform;
            Vector3[] spots = { new Vector3(-0.12f, 0.05f, 0.05f), new Vector3(0.08f, 0.05f, 0.08f), new Vector3(-0.02f, 0.14f, 0.07f), new Vector3(0.2f, 0.05f, -0.05f) };
            for (int k = 0; k < spots.Length; k++)
            {
                var rot = Quaternion.Euler(0f, k * 23f - 20f, 90f);
                Block(t, "Scroll", PrimitiveType.Cylinder, spots[k], new Vector3(0.1f, 0.24f, 0.1f), rot, paper);
                Block(t, "Tie", PrimitiveType.Cylinder, spots[k], new Vector3(0.105f, 0.02f, 0.105f), rot, red);
            }
            // 앞에 펼친 두루마리 하나: 종이 띠와 양끝 축
            Block(t, "Open Sheet", PrimitiveType.Cube, new Vector3(0f, 0.012f, -0.25f), new Vector3(0.5f, 0.01f, 0.2f), Quaternion.Euler(0f, -8f, 0f), paper);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Rod", PrimitiveType.Cylinder, new Vector3(s * 0.26f, 0.03f, -0.25f + s * 0.035f), new Vector3(0.035f, 0.11f, 0.035f), Quaternion.Euler(90f, -8f, 0f), wood);
            return t.gameObject;
        }
    }
}
