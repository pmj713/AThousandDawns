using AThousandDawns.Prototype;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 마을 채색 손질: 회귀할 때마다 보는 곳이라 이계가 섞이기 전의 따뜻한 마을로.
    //   마을 바닥은 판과 따로 (지역마다 바뀌는 판 바닥 재질과 떨어져 늘 따뜻한 황토 마당)
    //   어귀에서 매화나무·객잔 쪽으로 이어진 디딤돌 길 · 마당 북쪽을 가로지르는 줄에 붉은·노란 종이 등롱
    //   · 어귀 안쪽과 사당 앞 꽃 화분. 모두 보기용이라 충돌체는 장식 규칙(AddDecorColliders)에 맡겨요 (낮은 것은 안 막아요)
    //   남서쪽 텃밭(배추·고추 두둑)은 사용자 요청으로 뺐어요
    public static partial class PrototypeSceneBuilder
    {
        static void DressVillageColor(Transform deco, Vector3 c, GameObject village, Material stoneMat, Material woodMat)
        {
            // 마을 바닥: 판 바닥과 같은 흙 그림에 따뜻한 황토빛
            var yard = GetInkTriplanar("Ink3D_VillageYard", "Floor_Ground", 4f, 0f);
            yard.SetColor("_BaseColor", new Color(0.62f, 0.54f, 0.44f, 1f));
            foreach (Transform t in village.transform)
                if (t.name == "Floor" && t.GetComponent<Renderer>() is Renderer fr) fr.sharedMaterial = yard;

            var rope = GetInkMaterial("Ink_River_Rope", "#A88F62", 0.45f);
            var lanternRed = GetInkMaterial("Ink_Village_LanternRed", "#D8352A", 1f);
            var lanternGold = GetInkMaterial("Ink_Village_LanternGold", "#F2B43A", 1f);
            // 화분 잎: 대나무 잎과 같은 잎 그림 (원기둥 화분일 때만 쓰여요)
            LeafPaintTexture();
            var pepperLeaf = StalkStyle(GetInkTriplanar("Ink3D_PepperLeaf", "Tex_LeafPaint", 0.4f, 1f), 0.45f, 1f);
            pepperLeaf.SetColor("_BaseColor", new Color(0.36f, 0.4f, 0.28f, 1f));
            EditorUtility.SetDirty(pepperLeaf);
            var pot = GetInkMaterial("Ink_Village_Pot", "#B5603A", 1f);
            var petals = new[] { GetInkMaterial("Ink_Village_FlowerPink", "#F07AA0", 1f), GetInkMaterial("Ink_Village_FlowerYellow", "#F5C842", 1f), GetInkMaterial("Ink_Village_FlowerWhite", "#F4EEE0", 1f) };
            var leaf = pepperLeaf;
            var root = new GameObject("Village Color (마을 채색)").transform;
            root.SetParent(deco, false);
            var rng = new System.Random(17);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // 디딤돌 길: 어귀(남쪽 문)에서 북쪽으로, 매화나무 앞에서 서쪽 객잔 쪽으로 꺾여요
            void Slabs(Vector3 from, Vector3 to)
            {
                float len = Vector3.Distance(from, to);
                int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.8f));
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(from, to, k / (float)n) + new Vector3(R(-0.12f, 0.12f), 0.025f, R(-0.12f, 0.12f));
                    Block(root, "Stepping Stone", PrimitiveType.Cylinder, p, new Vector3(R(0.55f, 0.75f), 0.025f, R(0.45f, 0.65f)), Quaternion.Euler(0f, R(0f, 180f), 0f), stoneMat);
                }
            }
            var gate = c + new Vector3(VillageExit.x, 0f, VillageExit.z + 0.4f);
            var bend = c + new Vector3(VillageExit.x, 0f, -1.6f);
            Slabs(gate, bend);
            Slabs(bend + new Vector3(-0.8f, 0f, 0f), c + new Vector3(-8.4f, 0f, -1.6f));

            // 종이 등롱 줄: 마당 북쪽을 가로질러요 (기둥 둘 · 늘어진 줄 · 붉은·노란 등롱 번갈아)
            var a = c + new Vector3(-0.5f, 0f, 2.6f);
            var b = c + new Vector3(8f, 0f, 2.6f);
            const float poleH = 2.7f;
            foreach (var p in new[] { a, b })
                Block(root, "Lantern Pole", PrimitiveType.Cylinder, p + Vector3.up * poleH * 0.5f, new Vector3(0.1f, poleH * 0.5f, 0.1f), Quaternion.identity, woodMat);
            const int lanterns = 8;
            Vector3 prev = a + Vector3.up * poleH;
            for (int k = 1; k <= lanterns + 1; k++)
            {
                float t = k / (float)(lanterns + 1);
                var p = Vector3.Lerp(a, b, t) + Vector3.up * (poleH - 0.45f * Mathf.Sin(Mathf.PI * t));   // 줄이 가운데로 늘어져요
                var d = p - prev;
                Block(root, "Rope", PrimitiveType.Cylinder, (p + prev) * 0.5f, new Vector3(0.025f, d.magnitude * 0.5f, 0.025f), Quaternion.FromToRotation(Vector3.up, d.normalized), rope);
                if (k <= lanterns)
                {
                    var m = k % 2 == 0 ? lanternGold : lanternRed;
                    Block(root, "Paper Lantern", PrimitiveType.Sphere, p + Vector3.down * 0.22f, new Vector3(0.26f, 0.32f, 0.26f), Quaternion.identity, m);
                    Block(root, "Lantern Cap", PrimitiveType.Cylinder, p + Vector3.down * 0.05f, new Vector3(0.12f, 0.02f, 0.12f), Quaternion.identity, woodMat);
                }
                prev = p;
            }

            // 꽃 화분: 어귀 안쪽 양옆과 사당 앞
            var pots = new[]
            {
                c + new Vector3(VillageExit.x - 1.6f, 0f, VillageExit.z + 0.9f), c + new Vector3(VillageExit.x + 1.6f, 0f, VillageExit.z + 0.9f),
                c + RoomRun.ShrineOffset + new Vector3(1.4f, 0f, 1.6f), c + RoomRun.ShrineOffset + new Vector3(-0.4f, 0f, 1.9f),
            };
            for (int i = 0; i < pots.Length; i++)
            {
                var p = pots[i];
                // 플레이 테스트 4차: VARCO 화분(FlowerPot, 높이 0.6) — 없으면 예전 원기둥 화분
                if (VarcoProp(root, "FlowerPot", p, i * 47f, 1f, VillagePropFolder) != null) continue;
                Block(root, "Flower Pot", PrimitiveType.Cylinder, p + Vector3.up * 0.16f, new Vector3(0.36f, 0.16f, 0.36f), Quaternion.identity, pot);
                Block(root, "Leaves", PrimitiveType.Sphere, p + Vector3.up * 0.36f, new Vector3(0.38f, 0.2f, 0.38f), Quaternion.identity, leaf);
                for (int k = 0; k < 5; k++)
                {
                    float ang = k * 72f + i * 20f;
                    var fp = p + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * 0.11f, 0.47f + (k % 2) * 0.04f, Mathf.Sin(ang * Mathf.Deg2Rad) * 0.11f);
                    Block(root, "Flower", PrimitiveType.Sphere, fp, Vector3.one * 0.1f, Quaternion.identity, petals[(i + k) % petals.Length]);
                }
            }
            StripColliders(root);
        }
    }
}
