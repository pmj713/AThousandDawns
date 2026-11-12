using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 업보 등불 여섯 (마을 사당 앞): 사당 계단 양옆으로 셋씩 비스듬히 늘어선 낮은 돌기둥 위 붉은 종이 등불.
    // 왼쪽 바깥부터 오른쪽 바깥까지 차례로 1~6단계예요. 켜고 끄기·색은 InkKarmaLanterns가 업보 단계를 보고 해요
    public static partial class PrototypeSceneBuilder
    {
        static void DressKarmaLanterns(Material stoneMat, Material roofX)
        {
            var paper = GetInkMaterial("Ink_Karma_Lantern", "#FFFFFF", 0.8f);   // 색은 MaterialPropertyBlock으로 단계마다
            var root = new GameObject("Karma Lanterns (업보 등불)").transform;
            // 사당(BuildVillageShrine)은 ShrineOffset + (-1, 0, -1)에 서서 남쪽(-Z)을 봐요. 계단 앞 양옆으로 늘어세워요
            Vector3 shrine = VillageCenter + RoomRun.ShrineOffset + new Vector3(-1f, 0f, -1f);
            var shades = new Renderer[6];
            var flames = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                int side = i < 3 ? -1 : 1;
                int k = i < 3 ? 2 - i : i - 3;                                   // 계단에서 바깥으로 0·1·2
                var foot = shrine + new Vector3(side * (1.05f + k * 0.62f), 0f, -1.25f - k * 0.38f);
                var t = new GameObject($"Karma Lantern {i + 1}").transform;
                t.SetParent(root, false);
                t.position = foot;
                Block(t, "Post", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0f), new Vector3(0.2f, 0.64f, 0.2f), Quaternion.identity, stoneMat);
                Block(t, "Shade", PrimitiveType.Cylinder, new Vector3(0f, 0.86f, 0f), new Vector3(0.34f, 0.2f, 0.34f), Quaternion.identity, paper);
                shades[i] = t.Find("Shade").GetComponent<Renderer>();
                var cap = new GameObject("Cap");
                cap.transform.SetParent(t, false);
                cap.transform.localPosition = new Vector3(0f, 1.12f, 0f);
                cap.transform.localScale = new Vector3(0.5f, 0.18f, 0.5f);
                cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
                cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
                flames[i] = FlameCards(t, foot + new Vector3(0f, 1.05f, 0f), 0.22f, 1, i * 41f);
                flames[i].SetActive(false);
            }
            StripColliders(root);
            root.gameObject.AddComponent<AThousandDawns.Art.InkKarmaLanterns>().Setup(shades, flames);
        }
    }
}
