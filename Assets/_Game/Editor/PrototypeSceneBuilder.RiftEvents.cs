using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판타지 이벤트 방 가운데 소품 (차원 겹침): RoomRun이 이벤트 방에 들어가면 Combat.center에 세워요 (천마총 이벤트 소품과 같은 방식).
    //   이계 대장간 (otherworldForgeProp): 드워프 모루·청보라 불꽃이 오르는 화로·룬 새긴 망치 · 쇠 덩이와 집게
    //   무너진 성당 제단 (chapelAltarProp): 체스판 대리석 바닥·두 단 위 금테 대리석 제단과 붉은 제단보·금 십자가·세 갈래 촛대 · 뒤로 빛나는 색유리 뾰족 아치 창 둘과 장미창 · 부러진 기둥·넘어진 신도석
    //   마나 샘 (manaSpringProp): 돌로 둘러친 둥근 샘, 청보라로 빛나는 물, 둘레에 솟은 작은 수정
    //   봉인된 마검 (sealedBladeProp): 룬 원 가운데 바닥에 꽂힌 검은 서양 장검을 사방 말뚝의 사슬이 묶어요. 뽑으면 코드가 자식 "Blade"를 꺼요
    // 모두 판타지 화풍, 밑에 보랏빛 차원 흙과 균열, 지름 3칸 안쪽. 그림만이라 충돌체는 없어요.
    // RoomRun 칸(필드)이 아직 없으면 프리팹만 만들어 두고 연결은 건너뛰어요
    public static partial class PrototypeSceneBuilder
    {
        const string RiftEventFolder = "Assets/_Game/Art/Env/Rift/Events";

        static void BuildRiftEventPrefabs(Material stoneMat, Material unused)
        {
            EnsureFolder("Assets/_Game/Art/Env/Rift");
            EnsureFolder(RiftEventFolder);
            var rift = GetShaderMaterial("Ink_Rift", "AThousandDawns/InkRift");
            Material Fantasy(string name, string hex) => FantasyLook(GetInkMaterial(name, hex, 1f));
            var sandstone = Fantasy("Ink_Rift_Sandstone", "#B9AC92");
            var runestone = Fantasy("Ink_Rift_Runestone", "#565B63");
            var soil = Fantasy("Ink_Rift_Soil", "#4B3A63");
            var crystal = Fantasy("Ink_Rift_Crystal", "#5E6BFF");
            var crystalPale = Fantasy("Ink_Rift_CrystalPale", "#A9B4FF");
            var lead = Fantasy("Ink_Rift_Lead", "#26262B");
            var glass = new[] { Fantasy("Ink_Rift_GlassRed", "#B3261E"), Fantasy("Ink_Rift_GlassBlue", "#2A4FB0"), Fantasy("Ink_Rift_GlassGold", "#D9A43A"), Fantasy("Ink_Rift_GlassGreen", "#3E8E4A") };
            var iron = Fantasy("Ink_Rift_Iron", "#4A4E57");
            var darkSteel = Fantasy("Ink_Rift_DarkSteel", "#2A2C36");
            var bladeSteel = Fantasy("Ink_Rift_BladeSteel", "#9BA3BC");   // 보랏빛 바닥 위에서 보이게 밝은 강철
            var brass = Fantasy("Ink_Rift_Brass", "#C99A3C");
            var wax = Fantasy("Ink_Rift_Wax", "#F1E6CC");
            var wood = Fantasy("Ink_Rift_Wood", "#7A4E2E");
            var flame = GetInkFlameMaterial();
            var manaFlame = GetShaderMaterial("Ink_ManaFlame", "AThousandDawns/InkFlame");
            manaFlame.SetColor("_CoreColor", new Color(0.85f, 0.9f, 1f, 1f));
            manaFlame.SetColor("_FlameColor", new Color(0.45f, 0.35f, 1f, 1f));
            manaFlame.SetColor("_SmokeColor", new Color(0.18f, 0.12f, 0.35f, 1f));
            EditorUtility.SetDirty(manaFlame);

            Transform Root(string name)
            {
                var t = new GameObject(name).transform;
                // 플레이테스트 10차: 납작한 보라 원판 대신 차원 소품과 같은 셀 풍 얼룩 그림 (RiftPatch.png). 없으면 예전 원판과 금
                if (RiftPatchSprite() is Sprite patch)
                {
                    var go = new GameObject("Rift Patch");
                    go.transform.SetParent(t, false);
                    go.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float k = 3.2f / patch.bounds.size.x;
                    go.transform.localScale = new Vector3(k, k * 0.85f, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = patch;
                    sr.sortingOrder = 2;
                    return t;
                }
                Block(t, "Rift Soil", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(3f, 0.012f, 2.6f), Quaternion.identity, soil);
                GroundCrack(t, new Vector3(0.6f, 0.012f, -0.5f), 30f, 1.6f, rift);
                GroundCrack(t, new Vector3(-0.7f, 0.012f, 0.4f), -50f, 1.3f, rift);
                return t;
            }

            // 1) 이계 대장간
            var forge = Root("RiftEvent_Forge (이계 대장간)");
            Block(forge, "Anvil Base", PrimitiveType.Cube, new Vector3(-0.35f, 0.22f, 0f), new Vector3(0.36f, 0.44f, 0.3f), Quaternion.identity, iron);
            Block(forge, "Anvil Waist", PrimitiveType.Cube, new Vector3(-0.35f, 0.5f, 0f), new Vector3(0.24f, 0.14f, 0.22f), Quaternion.identity, iron);
            Block(forge, "Anvil Face", PrimitiveType.Cube, new Vector3(-0.35f, 0.63f, 0f), new Vector3(0.78f, 0.14f, 0.3f), Quaternion.identity, iron);
            Block(forge, "Anvil Horn", PrimitiveType.Capsule, new Vector3(0.12f, 0.63f, 0f), new Vector3(0.12f, 0.2f, 0.12f), Quaternion.Euler(0f, 0f, 90f), iron);
            Block(forge, "Hammer Head", PrimitiveType.Cube, new Vector3(-0.45f, 0.76f, 0.05f), new Vector3(0.22f, 0.11f, 0.11f), Quaternion.Euler(0f, 25f, 0f), darkSteel);
            Block(forge, "Hammer Handle", PrimitiveType.Cylinder, new Vector3(-0.3f, 0.73f, -0.12f), new Vector3(0.04f, 0.2f, 0.04f), Quaternion.Euler(80f, 25f, 0f), wood);
            Block(forge, "Hammer Rune", PrimitiveType.Quad, new Vector3(-0.45f, 0.76f, -0.01f), new Vector3(0.18f, 0.12f, 1f), Quaternion.Euler(0f, 25f, 0f), rift);
            // 화로: 돌을 쌓은 둥근 화덕, 그 안에 청보라 불꽃
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Rock(forge, "Hearth Stone", new Vector3(0.75f + Mathf.Cos(a) * 0.38f, 0.1f, 0.55f + Mathf.Sin(a) * 0.38f), 0.2f, k, k * 40f, runestone, false);
            }
            Block(forge, "Coals", PrimitiveType.Cylinder, new Vector3(0.75f, 0.12f, 0.55f), new Vector3(0.6f, 0.06f, 0.6f), Quaternion.identity, crystal);
            FlameCardsWith(forge, new Vector3(0.75f, 0.15f, 0.55f), 0.9f, 3, manaFlame);
            Rock(forge, "Ore", new Vector3(-0.8f, 0f, 0.55f), 0.24f, 1, 20f, crystalPale, false);
            Rock(forge, "Ore Small", new Vector3(-0.95f, 0f, 0.3f), 0.15f, 2, 70f, crystal, false);
            Block(forge, "Tongs", PrimitiveType.Cube, new Vector3(0.2f, 0.02f, -0.6f), new Vector3(0.5f, 0.02f, 0.04f), Quaternion.Euler(0f, 30f, 0f), darkSteel);
            SaveRiftEvent(forge.gameObject, "RiftEvent_Forge", "otherworldForgeProp");

            // 2) 무너진 성당 제단 (플레이 테스트: 돌 상자 제단이 허접해 보였어요 → 성당 안쪽 성소처럼)
            //   두 단 대리석 단 위 흰 대리석 제단(앞면 금테 판 둘)·붉은 제단보에 금 십자, 위에 금 십자가와 세 갈래 촛대 둘.
            //   뒤로 뾰족 아치 창 둘(색유리 칸이 빛나요, 오른쪽은 깨져 위 칸이 비었어요)과 그 위 둥근 장미창, 양옆 기둥(오른쪽은 부러져 쓰러짐),
            //   앞에는 체스판 대리석 바닥·붉은 카펫·짧은 신도석 둘(하나는 넘어짐), 바닥에 깨진 색유리. 빛 이펙트는 아트 2 몫
            var chapel = Root("RiftEvent_Chapel (무너진 성당 제단)");
            var marble = Fantasy("Ink_Chapel_Marble", "#EDE8DC");
            var marbleDark = Fantasy("Ink_Chapel_MarbleDark", "#3C3A44");
            var gold = Fantasy("Ink_Chapel_Gold", "#E8B83A");
            var crimson = Fantasy("Ink_Chapel_Crimson", "#A4161E");
            var pew = Fantasy("Ink_Chapel_Pew", "#6E4426");
            Material Pane(string name, string hex)
            {
                var m = Fantasy(name, hex);
                ColorUtility.TryParseHtmlString(hex, out var c);
                m.SetColor("_AuraColor", new Color(c.r, c.g, c.b, 0.9f));   // 색유리는 가장자리가 빛나요
                m.SetFloat("_OutlineWidth", 1.5f);
                return m;
            }
            var panes = new[] { Pane("Ink_Chapel_PaneRed", "#E0362A"), Pane("Ink_Chapel_PaneBlue", "#3F6BEF"), Pane("Ink_Chapel_PaneGold", "#F2C23E"), Pane("Ink_Chapel_PaneGreen", "#3FB45A"), Pane("Ink_Chapel_PaneViolet", "#9A4FE0") };
            // 바닥: 검고 흰 대리석 체스판 + 두 단 계단
            for (int i = -3; i <= 3; i++)
                for (int j = -2; j <= 2; j++)
                    Block(chapel, "Floor Tile", PrimitiveType.Cube, new Vector3(i * 0.4f, 0.03f, j * 0.4f + 0.1f), new Vector3(0.4f, 0.04f, 0.4f), Quaternion.identity, ((i + j) % 2 + 2) % 2 == 0 ? marble : marbleDark);
            // 축복 제단 (플레이 테스트 3차: 상자 제단이 허접했어요 → VARCO 모델 BlessingAltar: 흰 대리석 단 위 금테 제단·금 촛대·성배·성서, 높이 1.9)
            //   없으면 예전 상자 제단으로
            if (VarcoProp(chapel, "BlessingAltar", new Vector3(0f, 0.05f, 0.45f), 180f) == null)
            {
                Block(chapel, "Dais Low", PrimitiveType.Cube, new Vector3(0f, 0.11f, 0.42f), new Vector3(2f, 0.12f, 0.95f), Quaternion.identity, marble);
                Block(chapel, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0.5f), new Vector3(1.25f, 0.74f, 0.5f), Quaternion.identity, marble);
                Block(chapel, "Altar Top", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.5f), new Vector3(1.42f, 0.07f, 0.62f), Quaternion.identity, marble);
            }
            // 뒤쪽 뾰족 아치 창 둘: 기둥 사이 색유리 칸 (오른쪽 창은 깨져 위 칸들이 비었어요)
            foreach (float cx in new[] { -0.55f, 0.55f })
            {
                bool broken = cx > 0f;
                for (int s = -1; s <= 1; s += 2)
                    Block(chapel, "Window Mullion", PrimitiveType.Cube, new Vector3(cx + s * 0.32f, 1.25f, 1.0f), new Vector3(0.1f, 2.1f, 0.14f), Quaternion.identity, marble);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (broken && row >= 3 && col != 0) continue;
                        var at = new Vector3(cx - 0.18f + col * 0.18f, 0.55f + row * 0.3f, 1.0f);
                        Block(chapel, "Stained Pane", PrimitiveType.Cube, at, new Vector3(0.17f, 0.29f, 0.03f), Quaternion.identity, panes[(row * 3 + col + (broken ? 2 : 0)) % panes.Length]);
                    }
                for (int row = 1; row < 5; row++)
                    Block(chapel, "Lead Bar", PrimitiveType.Cube, new Vector3(cx, 0.4f + row * 0.3f, 0.985f), new Vector3(0.56f, 0.025f, 0.02f), Quaternion.identity, lead);
                // 뾰족 아치 머리: 양쪽에서 기울어 만나는 돌 둘 (깨진 쪽은 한쪽만)
                Block(chapel, "Arch Head", PrimitiveType.Cube, new Vector3(cx - 0.15f, 2.42f, 1.0f), new Vector3(0.1f, 0.5f, 0.14f), Quaternion.Euler(0f, 0f, -35f), marble);
                if (!broken)
                {
                    Block(chapel, "Arch Head", PrimitiveType.Cube, new Vector3(cx + 0.15f, 2.42f, 1.0f), new Vector3(0.1f, 0.5f, 0.14f), Quaternion.Euler(0f, 0f, 35f), marble);
                    Block(chapel, "Arch Pane", PrimitiveType.Cube, new Vector3(cx, 2.28f, 1.0f), new Vector3(0.3f, 0.3f, 0.03f), Quaternion.Euler(0f, 0f, 45f), panes[2]);
                }
            }
            // 가운데 위 둥근 장미창: 금 가운데, 꽃잎 여덟 색유리, 대리석 테
            var rose = new Vector3(0f, 2.75f, 1.02f);
            Block(chapel, "Rose Rim", PrimitiveType.Cylinder, rose + new Vector3(0f, 0f, 0.02f), new Vector3(0.82f, 0.04f, 0.82f), Quaternion.Euler(90f, 0f, 0f), marble);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                Block(chapel, "Rose Petal", PrimitiveType.Cube, rose + dir * 0.22f - new Vector3(0f, 0f, 0.03f), new Vector3(0.14f, 0.26f, 0.02f), Quaternion.Euler(0f, 0f, a), panes[k % panes.Length]);
            }
            Block(chapel, "Rose Heart", PrimitiveType.Cylinder, rose - new Vector3(0f, 0f, 0.05f), new Vector3(0.14f, 0.015f, 0.14f), Quaternion.Euler(90f, 0f, 0f), panes[2]);
            // 양옆 기둥 (주두 금테), 오른쪽은 부러져 앞으로 쓰러졌어요
            Block(chapel, "Pillar", PrimitiveType.Cylinder, new Vector3(-1.25f, 1.3f, 0.85f), new Vector3(0.28f, 1.3f, 0.28f), Quaternion.identity, marble);
            Block(chapel, "Pillar Capital", PrimitiveType.Cube, new Vector3(-1.25f, 2.64f, 0.85f), new Vector3(0.38f, 0.1f, 0.38f), Quaternion.identity, gold);
            Block(chapel, "Pillar Base", PrimitiveType.Cube, new Vector3(-1.25f, 0.06f, 0.85f), new Vector3(0.38f, 0.12f, 0.38f), Quaternion.identity, marble);
            Block(chapel, "Broken Pillar", PrimitiveType.Cylinder, new Vector3(1.25f, 0.35f, 0.85f), new Vector3(0.28f, 0.35f, 0.28f), Quaternion.identity, marble);
            Block(chapel, "Fallen Drum", PrimitiveType.Cylinder, new Vector3(1.15f, 0.15f, -0.2f), new Vector3(0.28f, 0.45f, 0.28f), Quaternion.Euler(90f, 20f, 0f), marble);
            Rock(chapel, "Fallen Block", new Vector3(1.35f, 0f, 0.3f), 0.2f, 3, 30f, marble, false);
            // 앞: 붉은 카펫과 짧은 신도석 둘 (왼쪽은 넘어졌어요)
            Block(chapel, "Carpet Trim", PrimitiveType.Cube, new Vector3(0f, 0.054f, -0.55f), new Vector3(0.66f, 0.008f, 1.12f), Quaternion.identity, gold);
            Block(chapel, "Carpet", PrimitiveType.Cube, new Vector3(0f, 0.058f, -0.55f), new Vector3(0.58f, 0.01f, 1.1f), Quaternion.identity, crimson);
            Block(chapel, "Pew Seat", PrimitiveType.Cube, new Vector3(0.85f, 0.3f, -0.75f), new Vector3(0.7f, 0.06f, 0.26f), Quaternion.identity, pew);
            Block(chapel, "Pew Back", PrimitiveType.Cube, new Vector3(0.85f, 0.48f, -0.86f), new Vector3(0.7f, 0.36f, 0.05f), Quaternion.identity, pew);
            foreach (float x in new[] { 0.55f, 1.15f }) Block(chapel, "Pew Leg", PrimitiveType.Cube, new Vector3(x, 0.15f, -0.75f), new Vector3(0.05f, 0.3f, 0.24f), Quaternion.identity, pew);
            var fallenPew = new GameObject("Fallen Pew").transform;
            fallenPew.SetParent(chapel, false);
            fallenPew.localPosition = new Vector3(-0.9f, 0.13f, -0.8f);
            fallenPew.localRotation = Quaternion.Euler(-80f, 15f, 0f);
            Block(fallenPew, "Pew Seat", PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.06f, 0.26f), Quaternion.identity, pew);
            Block(fallenPew, "Pew Back", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.11f), new Vector3(0.7f, 0.36f, 0.05f), Quaternion.identity, pew);
            for (int k = 0; k < 6; k++)   // 바닥에 흩어진 깨진 색유리
            {
                var rot = Quaternion.Euler(90f - k * 6f, k * 53f, 0f);
                var at = new Vector3(0.35f + (k % 3) * 0.25f, 0.07f + (k % 2) * 0.02f, 0.05f - (k / 3) * 0.3f);
                Block(chapel, "Glass Shard", PrimitiveType.Cube, at, new Vector3(0.16f, 0.2f, 0.02f), rot, panes[k % panes.Length]);
            }
            SaveRiftEvent(chapel.gameObject, "RiftEvent_Chapel", "chapelAltarProp");

            // 3) 마나 샘
            var spring = Root("RiftEvent_ManaSpring (마나 샘)");
            for (int k = 0; k < 14; k++)   // 둥글게 둘러친 돌 테
            {
                float a = k * 360f / 14f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Block(spring, "Rim Stone", PrimitiveType.Cube, dir * 1.0f + Vector3.up * 0.14f, new Vector3(0.44f, 0.28f, 0.26f), Quaternion.LookRotation(dir), runestone);
            }
            Block(spring, "Spring Water", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(1.85f, 0.02f, 1.85f), Quaternion.identity, crystal);
            Block(spring, "Spring Glow", PrimitiveType.Quad, new Vector3(0f, 0.14f, 0f), new Vector3(1.6f, 1.6f, 1f), Quaternion.Euler(90f, 0f, 0f), rift);
            Block(spring, "Spring Glow Cross", PrimitiveType.Quad, new Vector3(0f, 0.14f, 0f), new Vector3(1.6f, 1.6f, 1f), Quaternion.Euler(90f, 90f, 0f), rift);
            for (int k = 0; k < 6; k++)   // 둘레에 솟은 작은 수정
            {
                float a = k * 60f + 15f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var foot = dir * 1.3f;
                var tilt = Quaternion.LookRotation(dir) * Quaternion.Euler(18f, 0f, 0f);
                float h = 0.35f + (k % 3) * 0.12f;
                var m = k % 2 == 0 ? crystal : crystalPale;
                Block(spring, "Crystal", PrimitiveType.Cube, foot + tilt * new Vector3(0f, h * 0.45f, 0f), new Vector3(h * 0.3f, h * 0.9f, h * 0.3f), tilt * Quaternion.Euler(0f, 45f, 0f), m);
                Block(spring, "Crystal Tip", PrimitiveType.Cube, foot + tilt * new Vector3(0f, h * 0.92f, 0f), Vector3.one * h * 0.21f, tilt * Quaternion.Euler(45f, 45f, 0f), m);
            }
            SaveRiftEvent(spring.gameObject, "RiftEvent_ManaSpring", "manaSpringProp");

            // 4) 봉인된 마검
            var sealed_ = Root("RiftEvent_SealedBlade (봉인된 마검)");
            for (int k = 0; k < 3; k++)   // 바닥 룬 원: 빛나는 고리 조각 셋 겹
                for (int s = 0; s < 12; s++)
                {
                    float a = (s * 30f + k * 10f) * Mathf.Deg2Rad, r = 0.8f + k * 0.18f;
                    Block(sealed_, "Rune Ring", PrimitiveType.Quad, new Vector3(Mathf.Cos(a) * r, 0.025f, Mathf.Sin(a) * r), new Vector3(0.18f, 0.32f, 1f),
                        Quaternion.Euler(90f, -a * Mathf.Rad2Deg, 0f), rift);
                }
            var blade = new GameObject("Blade").transform;   // 뽑으면 코드가 이 자식을 꺼요
            blade.SetParent(sealed_, false);
            blade.localRotation = Quaternion.Euler(0f, 0f, 6f);
            Block(blade, "Blade Steel", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(0.24f, 1.4f, 0.06f), Quaternion.identity, bladeSteel);   // 위에서 봐도 보이게 넓고 길게
            Block(blade, "Blade Fuller", PrimitiveType.Quad, new Vector3(0f, 0.75f, -0.032f), new Vector3(0.22f, 1.2f, 1f), Quaternion.identity, rift);
            Block(blade, "Blade Fuller Back", PrimitiveType.Quad, new Vector3(0f, 0.75f, 0.032f), new Vector3(0.22f, 1.2f, 1f), Quaternion.Euler(0f, 180f, 0f), rift);
            Block(blade, "Crossguard", PrimitiveType.Cube, new Vector3(0f, 1.42f, 0f), new Vector3(0.7f, 0.1f, 0.12f), Quaternion.identity, brass);
            Block(blade, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 1.62f, 0f), new Vector3(0.07f, 0.18f, 0.07f), Quaternion.identity, lead);
            Block(blade, "Pommel", PrimitiveType.Sphere, new Vector3(0f, 1.84f, 0f), Vector3.one * 0.15f, Quaternion.identity, crystal);
            for (int k = 0; k < 4; k++)   // 사방 말뚝에서 검으로 팽팽한 사슬
            {
                var dir = Quaternion.Euler(0f, 45f + k * 90f, 0f) * Vector3.forward;
                var post = dir * 1.25f;
                Block(sealed_, "Chain Post", PrimitiveType.Cylinder, post + Vector3.up * 0.22f, new Vector3(0.14f, 0.22f, 0.14f), Quaternion.identity, runestone);
                var from = post + Vector3.up * 0.4f;
                var to = new Vector3(0f, 1.1f, 0f);
                var d = to - from;
                int links = 7;
                for (int n = 0; n < links; n++)
                {
                    var at = from + d * ((n + 0.5f) / links);
                    Block(blade, "Chain Link", PrimitiveType.Cube, at, new Vector3(0.035f, d.magnitude / links * 0.85f, 0.015f),
                        Quaternion.FromToRotation(Vector3.up, d.normalized) * Quaternion.Euler(0f, n % 2 == 0 ? 0f : 90f, 0f), iron);   // 사슬은 검과 함께 사라져요
                }
            }
            SaveRiftEvent(sealed_.gameObject, "RiftEvent_SealedBlade", "sealedBladeProp");
        }

        static void FlameCardsWith(Transform parent, Vector3 foot, float height, int count, Material mat)
        {
            for (int k = 0; k < count; k++)
                Block(parent, "Flame", PrimitiveType.Quad, foot + Vector3.up * height * 0.5f, new Vector3(height * 0.7f, height, 1f), Quaternion.Euler(0f, k * 180f / count, 0f), mat);
        }

        static void SaveRiftEvent(GameObject root, string name, string field)
        {
            StripColliders(root.transform);
            var prefab = SaveStablePrefab(root, $"{RiftEventFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            var run = Object.FindFirstObjectByType<AThousandDawns.Prototype.RoomRun>(FindObjectsInactive.Include);
            if (run == null) return;
            var so = new SerializedObject(run);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.Log($"[이벤트 소품] RoomRun.{field} 칸이 아직 없어 {name}은 프리팹만 만들었어요"); return; }
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
