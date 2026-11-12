using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 회귀 비석 (마을 매화나무 곁, 앞면 -Z): 받침돌 위 낡은 돌비석과 이 빠진 머릿돌.
    // 앞면 오른쪽 두 줄에 正자 셈 획 50개(열 자, 위에서 아래로·오른쪽 줄부터), 왼쪽 줄에 붉은 낙관 여섯 자리.
    // 획·낙관은 꺼 두고 InkRegressionStele가 회차·천마 격파 수만큼 켜요. 넘치면 머릿돌·낙관 아래 숫자
    public static partial class PrototypeSceneBuilder
    {
        const float SteleFaceZ = -0.125f;   // 비석 앞면 (두께 0.25)

        static void BuildRegressionStele(Transform deco, Vector3 foot, float yaw, Material stone, Material dark)
        {
            var moss = GetInkMaterial("Ink_Temple_Moss", "#5E6B3A", 0.5f);
            var carve = GetInkMaterial("Ink_Stele_Carve", "#1A1714", 0f);      // 새긴 획 (먹)
            var seal = GetInkMaterial("Ink_Stele_Seal", "#B3261E", 0.9f);       // 낙관 인주
            var sealInk = GetInkMaterial("Ink_Stele_SealPale", "#E9D9C4", 0.2f); // 낙관 안 글자 (빈 획)

            var root = new GameObject("Regression Stele (회귀 비석)").transform;
            root.SetParent(deco, false);
            root.position = foot;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 받침돌·비신·머릿돌
            Block(root, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0.02f), new Vector3(1.25f, 0.34f, 0.62f), Quaternion.identity, stone);
            Block(root, "Plinth Chip", PrimitiveType.Cube, new Vector3(0.58f, 0.3f, -0.26f), new Vector3(0.16f, 0.1f, 0.14f), Quaternion.Euler(0f, 30f, 20f), dark);
            Block(root, "Body", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.9f, 1.62f, 0.25f), Quaternion.identity, stone);
            Block(root, "Cap", PrimitiveType.Cube, new Vector3(0f, 2.03f, 0f), new Vector3(1.04f, 0.16f, 0.32f), Quaternion.identity, stone);
            Block(root, "Cap Top", PrimitiveType.Cube, new Vector3(0f, 2.14f, 0f), new Vector3(0.8f, 0.08f, 0.26f), Quaternion.identity, stone);
            Block(root, "Cap Chip", PrimitiveType.Cube, new Vector3(-0.5f, 2.09f, -0.1f), new Vector3(0.16f, 0.14f, 0.18f), Quaternion.Euler(20f, 25f, 35f), dark);   // 이 빠진 귀퉁이
            Block(root, "Crack", PrimitiveType.Cube, new Vector3(0.39f, 0.75f, SteleFaceZ - 0.004f), new Vector3(0.02f, 0.5f, 0.01f), Quaternion.Euler(0f, 0f, 14f), carve);
            Block(root, "Crack", PrimitiveType.Cube, new Vector3(0.33f, 0.47f, SteleFaceZ - 0.004f), new Vector3(0.016f, 0.18f, 0.01f), Quaternion.Euler(0f, 0f, -20f), carve);
            Block(root, "Moss", PrimitiveType.Sphere, new Vector3(-0.42f, 0.36f, -0.12f), new Vector3(0.42f, 0.14f, 0.32f), Quaternion.identity, moss);
            Block(root, "Moss", PrimitiveType.Sphere, new Vector3(0.35f, 2.12f, 0.02f), new Vector3(0.3f, 0.07f, 0.24f), Quaternion.identity, moss);
            Block(root, "Moss", PrimitiveType.Sphere, new Vector3(0.45f, 0.38f, 0.1f), new Vector3(0.25f, 0.1f, 0.25f), Quaternion.identity, moss);

            // 正 셈 획: 열 자 (오른쪽 줄 위→아래 다섯, 왼쪽 줄 다섯). 한 자 안 획 순서는 위 가로 · 가운데 세로 · 가운데 짧은 가로 · 왼쪽 세로 · 아래 가로
            const float cell = 0.17f, w = 0.026f;
            var strokes = new List<GameObject>();
            float[] columns = { 0.27f, 0.03f };
            for (int ch = 0; ch < 10; ch++)
            {
                var c = new Vector2(columns[ch / 5], 1.76f - (ch % 5) * 0.24f);
                var glyph = new (Vector2 at, Vector2 size)[]
                {
                    (new Vector2(0f, cell * 0.5f), new Vector2(cell, w)),
                    (new Vector2(0f, 0f), new Vector2(w, cell)),
                    (new Vector2(cell * 0.22f, 0f), new Vector2(cell * 0.44f, w)),
                    (new Vector2(-cell * 0.36f, -cell * 0.22f), new Vector2(w, cell * 0.5f)),
                    (new Vector2(0f, -cell * 0.5f), new Vector2(cell * 1.15f, w)),
                };
                for (int k = 0; k < 5; k++)
                {
                    var (at, size) = glyph[k];
                    float tilt = ((ch * 5 + k) * 37 % 7 - 3) * 1.2f;   // 칼로 그어 획마다 살짝 비뚤어요
                    Block(root, $"Stroke {ch * 5 + k + 1}", PrimitiveType.Cube, new Vector3(c.x + at.x, c.y + at.y, SteleFaceZ - 0.003f), new Vector3(size.x, size.y, 0.012f), Quaternion.Euler(0f, 0f, tilt), carve);
                    var go = root.GetChild(root.childCount - 1).gameObject;
                    go.SetActive(false);
                    strokes.Add(go);
                }
            }

            // 붉은 낙관: 왼쪽 줄에 위에서 아래로 여섯 자리. 네모 인주 안에 빈 획 세 줄
            var seals = new List<GameObject>();
            for (int k = 0; k < 6; k++)
            {
                var s = new GameObject($"Seal {k + 1}").transform;
                s.SetParent(root, false);
                s.localPosition = new Vector3(-0.29f, 1.76f - k * 0.2f, SteleFaceZ - 0.004f);
                s.localRotation = Quaternion.Euler(0f, 0f, (k * 29 % 5 - 2) * 2f);
                Block(s, "Ink", PrimitiveType.Cube, Vector3.zero, new Vector3(0.13f, 0.13f, 0.01f), Quaternion.identity, seal);
                Block(s, "Glyph", PrimitiveType.Cube, new Vector3(-0.025f, 0f, -0.006f), new Vector3(0.016f, 0.09f, 0.004f), Quaternion.identity, sealInk);
                Block(s, "Glyph", PrimitiveType.Cube, new Vector3(0.022f, 0.02f, -0.006f), new Vector3(0.05f, 0.016f, 0.004f), Quaternion.identity, sealInk);
                Block(s, "Glyph", PrimitiveType.Cube, new Vector3(0.022f, -0.025f, -0.006f), new Vector3(0.016f, 0.05f, 0.004f), Quaternion.identity, sealInk);
                s.gameObject.SetActive(false);
                seals.Add(s.gameObject);
            }

            var runsLabel = SteleLabel(root, "Runs Number", new Vector3(0f, 2.03f, -0.165f), 0.11f, new Color(0.1f, 0.09f, 0.08f));
            var sealLabel = SteleLabel(root, "Seal Number", new Vector3(-0.29f, 0.56f, SteleFaceZ - 0.006f), 0.08f, new Color(0.7f, 0.15f, 0.12f));
            root.gameObject.AddComponent<AThousandDawns.Art.InkRegressionStele>().Setup(strokes.ToArray(), seals.ToArray(), runsLabel, sealLabel);
        }

        // 돌에 새긴 숫자 (기본 글꼴 TextMesh, 가운데 맞춤)
        static TextMesh SteleLabel(Transform root, string name, Vector3 at, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = size / 6.4f;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.text = "0";
            go.SetActive(false);
            return text;
        }
    }
}
