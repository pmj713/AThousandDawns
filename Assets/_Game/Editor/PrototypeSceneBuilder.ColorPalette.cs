using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 지역 색표 (채색 전환): 소품 재질(GetInkMaterial)은 처음 만들 때만 색을 넣어서, 수묵 시절의 칙칙한 먹빛 색이 그대로 남아 있어요.
    // 씬을 만들 때마다 여기 색으로 다시 칠해요 (재질 이름 → 색). 지역마다 알아보는 색을 살려요:
    //   폐사찰: 이끼 초록 · 주황 번(幡) · 단청(붉은 칠 기둥, 청록 띠) / 장강 수채: 푸른 물 · 칠한 나무 · 붉은 깃발 · 누런 갈대
    //   천마총: 검붉은 돌 · 선홍 휘장 · 달군 쇠 · 뼛빛 / 마을·공용: 옹기 갈색 · 쪽빛 · 황토
    // 목록에 없는 재질은 처음 색 그대로예요
    public static partial class PrototypeSceneBuilder
    {
        static readonly Dictionary<string, Color> ColorPalette = new Dictionary<string, Color>
        {
            // 폐사찰
            ["Ink_Temple_Moss"] = new Color(0.36f, 0.56f, 0.24f),
            ["Ink_Temple_Banner"] = new Color(0.9f, 0.55f, 0.12f),
            ["Ink_Temple_Earth"] = new Color(0.58f, 0.44f, 0.3f),
            ["Ink_Dancheong_Green"] = new Color(0.16f, 0.58f, 0.5f),
            ["Ink_Dancheong_Red"] = new Color(0.78f, 0.16f, 0.12f),
            // 장강 수채
            ["Ink_River_OutsideWater"] = new Color(0.22f, 0.55f, 0.72f),
            ["Ink_River_DarkWood"] = new Color(0.46f, 0.3f, 0.18f),
            ["Ink_River_Rope"] = new Color(0.82f, 0.68f, 0.42f),
            ["Ink_River_Reed"] = new Color(0.66f, 0.7f, 0.3f),
            ["Ink_River_Net"] = new Color(0.55f, 0.5f, 0.36f),
            ["Ink_River_WetStone"] = new Color(0.42f, 0.52f, 0.55f),
            ["Ink_River_Flag"] = new Color(0.86f, 0.2f, 0.14f),
            // 천마총
            ["Ink_Tomb_Marble"] = new Color(0.24f, 0.14f, 0.16f),
            ["Ink_Tomb_Red"] = new Color(0.86f, 0.1f, 0.12f),
            ["Ink_Tomb_Blood"] = new Color(0.55f, 0.04f, 0.06f),
            ["Ink_Tomb_Mound"] = new Color(0.42f, 0.3f, 0.24f),
            ["Ink_Tomb_Bone"] = new Color(0.93f, 0.88f, 0.74f),
            ["Ink_Tomb_HotIron"] = new Color(1f, 0.42f, 0.08f),
            ["Ink_Tomb_Iron"] = new Color(0.32f, 0.33f, 0.38f),
            ["Ink_Tomb_Thorn"] = new Color(0.3f, 0.17f, 0.13f),
            // 공용 소품
            ["Ink_Jar"] = new Color(0.56f, 0.34f, 0.2f),
            ["Ink_Onggi"] = new Color(0.36f, 0.22f, 0.13f),
            ["Ink_Iron"] = new Color(0.36f, 0.38f, 0.42f),
            ["Ink_Barrier_Wood"] = new Color(0.4f, 0.24f, 0.15f),
            ["Ink_Bamboo_Earth"] = new Color(0.46f, 0.4f, 0.24f),
            ["Ink_Bag_Hemp"] = new Color(0.72f, 0.6f, 0.4f),
            ["Ink_CaveDark"] = new Color(0.16f, 0.13f, 0.12f),
            // 보상·방 소품 (보상 종류마다 한눈에: 붉은 비단 주머니 · 놋쇠 상자 · 은괴 · 숯불 · 두루마리 종이 · 남빛 보따리)
            ["Ink_Pouch_Silk"] = new Color(0.86f, 0.18f, 0.16f),
            ["Ink_Pouch_Cord"] = new Color(0.98f, 0.78f, 0.28f),
            ["Ink_Reward_Brass"] = new Color(0.92f, 0.7f, 0.26f),
            ["Ink_Reward_Cloth"] = new Color(0.2f, 0.36f, 0.72f),
            ["Ink_Reward_Dark"] = new Color(0.3f, 0.24f, 0.2f),
            ["Ink_Reward_Ember"] = new Color(1f, 0.45f, 0.12f),
            ["Ink_Reward_Paper"] = new Color(0.96f, 0.9f, 0.74f),
            ["Ink_Reward_Red"] = new Color(0.82f, 0.2f, 0.16f),
            ["Ink_Reward_Silver"] = new Color(0.86f, 0.9f, 0.95f),
            ["Ink_Reward_Wood"] = new Color(0.6f, 0.38f, 0.22f),
            ["Ink_PaperLid"] = new Color(0.95f, 0.88f, 0.7f),
            ["Ink_Crack_Dark"] = new Color(0.2f, 0.15f, 0.12f),
            // 마을
            ["Ink_Village_Indigo"] = new Color(0.18f, 0.3f, 0.66f),
            ["Ink_Village_Ochre"] = new Color(0.86f, 0.6f, 0.22f),
            ["Ink_Village_Straw"] = new Color(0.86f, 0.72f, 0.4f),
            ["Ink_Village_JarGlaze"] = new Color(0.5f, 0.26f, 0.14f),
            ["Ink_Villager_Tool"] = new Color(0.55f, 0.42f, 0.28f),
        };

        static void ApplyColorPalette()
        {
            int n = 0;
            foreach (var (name, color) in ColorPalette)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{InkMaterialFolder}/{name}.mat");
                if (mat == null) continue;
                mat.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(mat);
                n++;
            }
            Debug.Log($"[채색] 지역 색표: 재질 {n}개");

            // 흙·이끼처럼 땅에 반쯤 묻힌 납작한 구(흙더미·이끼·봉분)에 쓰는 재질은 구조물 셰이더(결 있는 흙)로 바꿔요.
            // 캐릭터 셰이더로 그리면 가까이서 새까맣게 나왔어요 (멀리선 괜찮음). 색은 위 색표 그대로
            var soil = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/Floor_Ground.png");
            var triplanar = Shader.Find(InkTriplanarShaderName);
            foreach (var name in SoilMaterials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{InkMaterialFolder}/{name}.mat");
                if (mat == null || triplanar == null) continue;
                var color = mat.GetColor("_BaseColor");
                mat.shader = triplanar;
                mat.SetTexture("_BaseMap", soil);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_TileSize", 1.5f);
                mat.SetFloat("_OutlineWidth", 0f);
                EditorUtility.SetDirty(mat);
            }
        }

        static readonly string[] SoilMaterials = { "Ink_Temple_Moss", "Ink_Temple_Earth", "Ink_Tomb_Mound", "Ink_Bamboo_Earth" };

        // 단청: 절 건물 기둥은 붉은 칠(주칠), 기둥머리에 청록 띠. 돌·마을 나무와 따로 써요
        static Material s_templeLacquer, s_dancheongBand;

        static Material TempleLacquer()
        {
            if (s_templeLacquer == null) s_templeLacquer = GetInkTriplanar("Ink3D_TempleLacquer", "Tex_Wood", 1f, 2f);
            s_templeLacquer.SetColor("_BaseColor", new Color(0.74f, 0.2f, 0.13f, 1f));
            return s_templeLacquer;
        }

        static Material DancheongBand()
        {
            if (s_dancheongBand == null) s_dancheongBand = GetInkMaterial("Ink_Dancheong_Green", "#29947F", 1f);
            return s_dancheongBand;
        }

        // 처마 단청: 지붕(용마루가 x축인 RoofCap) 앞뒤 처마 끝에 청록 판과 붉은 테를 대요. 처마 밖으로 조금 내밀어 위에서 내려다봐도 보여요
        static void DancheongEaves(Transform roof)
        {
            var s = roof.localScale;
            var holder = new GameObject("Dancheong Eaves").transform;
            holder.SetParent(roof.parent, false);
            holder.localPosition = roof.localPosition;
            holder.localRotation = roof.localRotation;
            var red = GetInkMaterial("Ink_Dancheong_Red", "#C7291F", 1f);
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector3(0f, -0.5f * s.y + 0.07f, side * (0.5f * s.z + 0.05f));
                Block(holder, "Eave Band", PrimitiveType.Cube, at, new Vector3(s.x * 0.98f, 0.16f, 0.1f), Quaternion.identity, DancheongBand());
                Block(holder, "Eave Trim", PrimitiveType.Cube, at + new Vector3(0f, -0.1f, 0f), new Vector3(s.x * 0.98f, 0.05f, 0.12f), Quaternion.identity, red);
            }
        }

        // 기둥 꼭대기 아래에 청록 단청 띠 (기둥 지름보다 조금 굵게)
        static void DancheongBandOn(Transform parent, Vector3 columnTop, float radius)
        {
            Block(parent, "Dancheong", PrimitiveType.Cylinder, columnTop - new Vector3(0f, 0.14f, 0f), new Vector3(radius * 2.2f, 0.1f, radius * 2.2f), Quaternion.identity, DancheongBand());
        }
    }
}
