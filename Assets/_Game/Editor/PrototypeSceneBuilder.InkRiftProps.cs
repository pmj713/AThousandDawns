using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 차원 겹침 소품 (RoomRun.FeatureKind Rampart · KnightStatue · RunePillar · ManaCrystal): 무협 방 바닥에 이계 물건이 "잘못 끼어든" 모습.
    //   모두 판타지 화풍(FantasyLook: 원색 · 굵고 곧은 윤곽)이고, 밑동엔 보랏빛 차원 균열(InkRift)과 다른 결의 보랏빛 흙이 깔려요.
    //   무너진 성벽 조각 (2.4 × 0.6, 높이 1.6, 길이가 X): 서양 성의 사암 성벽, 위턱에 총안(凹), 한쪽 끝이 부서져 돌무더기
    //   기사 석상 (1 × 1, 높이 2.6, 앞이 -Z): 투구 쓴 기사가 두 손으로 검을 세워 짚고 서 있어요. 왼팔 어깨가 깨져 나가고 이끼가 꼈어요
    //   룬 기둥 (0.8 × 0.8, 높이 2.8): 고대 엘프·드워프풍 팔각 돌기둥, 네 면에 새긴 룬이 청보라로 은은히 빛나요
    //   마나 수정 (0.9 × 0.9, 높이 1.4): 바닥에서 솟은 청보라 수정 무리, 속에서 빛이 일렁여요 (깨지면 게임이 상자를 지우고 모델도 같이 사라져요)
    // 모델은 발밑이 원점, 크기는 작은 방 기준 — InkRoomFeatures가 상자 크기에 맞춰 늘려요
    public static partial class PrototypeSceneBuilder
    {
        static (GameObject rampart, GameObject knight, GameObject rune, GameObject crystal) BuildRiftProps()
        {
            var rift = GetShaderMaterial("Ink_Rift", "AThousandDawns/InkRift");
            Material Fantasy(string name, string hex) => FantasyLook(GetInkMaterial(name, hex, 1f));
            var sandstone = Fantasy("Ink_Rift_Sandstone", "#B9AC92");
            var runestone = Fantasy("Ink_Rift_Runestone", "#565B63");
            var statueStone = Fantasy("Ink_Rift_StatueStone", "#A3A6A0");
            var moss = Fantasy("Ink_Rift_Moss", "#4F8A3A");
            var soil = Fantasy("Ink_Rift_Soil", "#4B3A63");
            var crystal = Fantasy("Ink_Rift_Crystal", "#5E6BFF");
            var crystalPale = Fantasy("Ink_Rift_CrystalPale", "#A9B4FF");

            // 밑동: 보랏빛 흙 판 + 갈라진 빛 금 둘
            void RiftBase(Transform root, float radius)
            {
                Block(root, "Rift Soil", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(radius * 2f, 0.012f, radius * 1.6f), Quaternion.identity, soil);
                GroundCrack(root, new Vector3(radius * 0.3f, 0.01f, -radius * 0.2f), 25f, radius * 1.6f, rift);
                GroundCrack(root, new Vector3(-radius * 0.35f, 0.01f, radius * 0.25f), -40f, radius * 1.1f, rift);
            }

            // 무너진 성벽 조각
            var rampart = new GameObject("Rift Rampart (무너진 성벽 조각)").transform;
            RiftBase(rampart, 1.5f);
            Block(rampart, "Wall", PrimitiveType.Cube, new Vector3(-0.15f, 0.62f, 0f), new Vector3(2.1f, 1.24f, 0.6f), Quaternion.identity, sandstone);
            Block(rampart, "Broken End", PrimitiveType.Cube, new Vector3(1.0f, 0.38f, 0f), new Vector3(0.5f, 0.76f, 0.56f), Quaternion.Euler(0f, 0f, -8f), sandstone);
            for (int k = 0; k < 4; k++)   // 총안 위턱: 솟은 머리돌 넷 (부서진 쪽은 비어요)
                Block(rampart, "Merlon", PrimitiveType.Cube, new Vector3(-1.0f + k * 0.55f, 1.24f + 0.18f, 0f), new Vector3(0.32f, 0.36f, 0.62f), Quaternion.Euler(0f, 0f, k == 3 ? 6f : 0f), sandstone);
            Block(rampart, "Coping", PrimitiveType.Cube, new Vector3(-0.15f, 1.25f, 0f), new Vector3(2.15f, 0.06f, 0.66f), Quaternion.identity, sandstone);
            for (int k = 0; k < 4; k++)   // 부서진 쪽 돌무더기
                Rock(rampart, "Rubble", new Vector3(1.25f + (k % 2) * 0.25f, 0f, (k - 1.5f) * 0.25f), 0.18f + (k % 3) * 0.05f, k, k * 47f, sandstone, false);
            Block(rampart, "Moss", PrimitiveType.Cube, new Vector3(-0.6f, 0.08f, -0.31f), new Vector3(0.7f, 0.16f, 0.04f), Quaternion.identity, moss);

            // 기사 석상
            var knight = new GameObject("Rift Knight Statue (기사 석상)").transform;
            RiftBase(knight, 0.75f);
            Block(knight, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.95f, 0.4f, 0.95f), Quaternion.identity, statueStone);
            foreach (float sx in new[] { -0.13f, 0.13f })
                Block(knight, "Leg", PrimitiveType.Cylinder, new Vector3(sx, 0.4f + 0.42f, 0f), new Vector3(0.18f, 0.42f, 0.18f), Quaternion.identity, statueStone);
            Block(knight, "Tasset", PrimitiveType.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.52f, 0.12f, 0.38f), Quaternion.identity, statueStone);
            Block(knight, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.7f, 0f), new Vector3(0.56f, 0.7f, 0.34f), Quaternion.identity, statueStone);
            Block(knight, "Pauldron", PrimitiveType.Sphere, new Vector3(0.33f, 1.98f, 0f), new Vector3(0.26f, 0.2f, 0.3f), Quaternion.identity, statueStone);
            Block(knight, "Broken Shoulder", PrimitiveType.Cube, new Vector3(-0.3f, 1.92f, 0f), new Vector3(0.12f, 0.14f, 0.24f), Quaternion.Euler(0f, 0f, 30f), statueStone);   // 왼팔이 깨져 나갔어요
            Block(knight, "Arm", PrimitiveType.Cylinder, new Vector3(0.22f, 1.62f, -0.16f), new Vector3(0.12f, 0.24f, 0.12f), Quaternion.Euler(-35f, 0f, -20f), statueStone);
            Block(knight, "Helm", PrimitiveType.Cylinder, new Vector3(0f, 2.2f, 0f), new Vector3(0.3f, 0.17f, 0.3f), Quaternion.identity, statueStone);
            Block(knight, "Helm Top", PrimitiveType.Sphere, new Vector3(0f, 2.37f, 0f), new Vector3(0.3f, 0.16f, 0.3f), Quaternion.identity, statueStone);
            Block(knight, "Visor Slit", PrimitiveType.Cube, new Vector3(0f, 2.24f, -0.15f), new Vector3(0.2f, 0.03f, 0.02f), Quaternion.identity, runestone);
            Block(knight, "Crest", PrimitiveType.Cube, new Vector3(0f, 2.48f, 0.02f), new Vector3(0.05f, 0.14f, 0.3f), Quaternion.identity, statueStone);
            Block(knight, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.3f), new Vector3(0.09f, 1.1f, 0.03f), Quaternion.identity, statueStone);   // 두 손으로 세워 짚은 검
            Block(knight, "Guard", PrimitiveType.Cube, new Vector3(0f, 1.52f, -0.3f), new Vector3(0.34f, 0.05f, 0.06f), Quaternion.identity, statueStone);
            Block(knight, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 1.64f, -0.3f), new Vector3(0.05f, 0.1f, 0.05f), Quaternion.identity, statueStone);
            Block(knight, "Hands", PrimitiveType.Sphere, new Vector3(0f, 1.66f, -0.28f), new Vector3(0.18f, 0.12f, 0.14f), Quaternion.identity, statueStone);
            Block(knight, "Moss Shoulder", PrimitiveType.Cube, new Vector3(0.3f, 2.06f, 0f), new Vector3(0.2f, 0.04f, 0.24f), Quaternion.identity, moss);
            Block(knight, "Moss Plinth", PrimitiveType.Cube, new Vector3(-0.25f, 0.41f, -0.25f), new Vector3(0.4f, 0.03f, 0.3f), Quaternion.identity, moss);
            Rock(knight, "Fallen Arm", new Vector3(-0.55f, 0f, -0.35f), 0.2f, 2, 60f, statueStone, false);

            // 룬 기둥
            var rune = new GameObject("Rift Rune Pillar (룬 기둥)").transform;
            RiftBase(rune, 0.65f);
            Block(rune, "Base", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f), Quaternion.identity, runestone);
            // 둥근 돌기둥 (사각을 엇갈려 겹치면 별 모양이 돼 룬이 모서리에 가려졌어요)
            Block(rune, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.3f + 1.1f, 0f), new Vector3(0.56f, 1.1f, 0.56f), Quaternion.identity, runestone);
            Block(rune, "Capital", PrimitiveType.Cube, new Vector3(0f, 2.6f, 0f), new Vector3(0.72f, 0.2f, 0.72f), Quaternion.Euler(0f, 22.5f, 0f), runestone);
            Block(rune, "Cap Stone", PrimitiveType.Cube, new Vector3(0f, 2.76f, 0f), new Vector3(0.36f, 0.12f, 0.36f), Quaternion.Euler(0f, 45f, 0f), runestone);
            for (int side = 0; side < 4; side++)   // 네 면의 새긴 룬 (빛나는 획)
            {
                var face = Quaternion.Euler(0f, side * 90f, 0f);
                for (int k = 0; k < 3; k++)
                    Block(rune, "Rune", PrimitiveType.Quad, face * new Vector3((k - 1) * 0.1f, 0.8f + k * 0.55f, -0.295f), new Vector3(0.24f, 0.42f, 1f),   // 균열 셰이더는 판 가운데 가는 틈만 그려서 판을 넓게
                        face * Quaternion.Euler(0f, 0f, (k - 1) * 30f + side * 15f), rift);
            }

            // 마나 수정
            var mana = new GameObject("Rift Mana Crystal (마나 수정)").transform;
            RiftBase(mana, 0.7f);
            // 플레이 테스트 3차: 상자를 엮은 수정이 허접했어요 → VARCO 모델 ManaCrystal (룬 새긴 검은 돌 받침 위 청보라 수정 무리, 높이 1.5). 없으면 예전 상자 수정
            if (VarcoProp(mana, "ManaCrystal", Vector3.zero, 0f) == null)
            {
                var rng = new System.Random(5);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                for (int k = 0; k < 7; k++)
                {
                    float h = k == 0 ? 1.3f : R(0.45f, 1.0f);
                    float a = k * 51f, r = k == 0 ? 0f : R(0.18f, 0.36f);
                    var foot = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * r);
                    var tilt = Quaternion.Euler(k == 0 ? 0f : R(-28f, 28f), R(0f, 90f), k == 0 ? 0f : R(-28f, 28f));
                    float w = h * 0.22f;
                    Block(mana, "Crystal", PrimitiveType.Cube, foot + tilt * new Vector3(0f, h * 0.42f, 0f), new Vector3(w, h * 0.84f, w), tilt * Quaternion.Euler(0f, 45f, 0f), k % 3 == 1 ? crystalPale : crystal);
                }
            }

            return (SaveFeature(rampart.gameObject, "Feature_RiftRampart"), SaveFeature(knight.gameObject, "Feature_RiftKnight"),
                    SaveFeature(rune.gameObject, "Feature_RiftRunePillar"), SaveFeature(mana.gameObject, "Feature_RiftManaCrystal"));
        }
    }
}
