using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 차원 균열 풍경 (차원 겹침): 무협 땅 위로 서양 판타지가 새어 나온 흔적을 벽 너머 띠에 듬성듬성 세워요. 모든 지역에서 늘 보여요.
    //   허공의 세로 균열(InkRift 셰이더, 둘레에 떠 있는 돌·유리 조각, 발밑 땅 균열) · 반쯤 묻혀 기운 고딕 첨두 아치 ·
    //   깨진 스테인드글라스 조각 더미 · 빛나는 룬이 새겨진 선돌
    // 판타지 적과 같은 화풍(FantasyLook: 원색 · 굵고 곧은 윤곽). 자리는 벽 너머 풍경과 같은 규칙(OutsideSpots)이고, 카메라 쪽(남쪽) 띠엔 낮은 것만.
    // 균열을 먼저 세우고 대나무·지역 풍경이 그 둘레를 비워요 (RiftClear). 보기용이라 충돌 없음, 그림자 끔·정적 배칭 (InkPerf).
    // 판타지 적이 방에 있으면 균열이 타올라요 (InkRiftGlow → 전역 _RiftBoost)
    public static partial class PrototypeSceneBuilder
    {
        // 균열 자리: 나중에 세우는 벽 너머 대나무·지역 풍경(OutsideSpots)이 이 둘레를 비워요
        static readonly List<Vector3> RiftClear = new List<Vector3>();
        const float RiftClearRadius = 3.8f;   // 대나무 잎 갓이 넓어서 넉넉히

        static void DressOutsideRift()
        {
            var rift = GetShaderMaterial("Ink_Rift", "AThousandDawns/InkRift");
            Material Fantasy(string name, string hex) => FantasyLook(GetInkMaterial(name, hex, 1f));
            var sandstone = Fantasy("Ink_Rift_Sandstone", "#B9AC92");
            var runestone = Fantasy("Ink_Rift_Runestone", "#565B63");
            var lead = Fantasy("Ink_Rift_Lead", "#26262B");
            var glass = new[] { Fantasy("Ink_Rift_GlassRed", "#B3261E"), Fantasy("Ink_Rift_GlassBlue", "#2A4FB0"),
                                Fantasy("Ink_Rift_GlassGold", "#D9A43A"), Fantasy("Ink_Rift_GlassGreen", "#3E8E4A") };

            RiftClear.Clear();
            var parent = new GameObject("Ink Outside Rift (차원 균열)").transform;
            var rng = new System.Random(2468);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int seed = 0, count = 0;
            foreach (var (p, south) in OutsideSpots(5.5f, 0.5f, 2.4f, 2468))
            {
                RiftClear.Add(p);
                float pick = R(0f, 1f), yaw = R(0f, 360f);
                if (south)
                {
                    if (pick < 0.5f) GlassShards(parent, p, yaw, seed, glass, lead);
                    else if (pick < 0.8f) GroundCrack(parent, p, yaw, R(1.2f, 2f), rift);
                    else RuneStone(parent, p, yaw, 0.9f, runestone, rift);
                }
                else if (pick < 0.34f) Tear(parent, p, R(3f, 4.2f), seed, rift, runestone, glass);
                else if (pick < 0.58f) GothicArch(parent, p, yaw, seed, sandstone);
                else if (pick < 0.78f) RuneStone(parent, p, yaw, R(1.7f, 2.4f), runestone, rift);
                else GlassShards(parent, p, yaw, seed, glass, lead);
                seed++; count++;
            }
            StripColliders(parent);
            parent.gameObject.AddComponent<AThousandDawns.Art.InkRiftGlow>();
            Debug.Log($"[수묵 시험판] 차원 균열 풍경: {count}곳");
        }

        // 허공의 세로 균열: 빛나는 틈 판 둘(엇갈려 어느 쪽에서 봐도 보여요) + 둘레에 떠 있는 돌·유리 조각 + 발밑 땅 균열
        static void Tear(Transform parent, Vector3 foot, float height, int seed, Material rift, Material stone, Material[] glass)
        {
            var t = At(parent, "Rift Tear (허공 균열)", foot, 0f);
            float lean = (seed % 5 - 2) * 4f;
            Block(t, "Tear", PrimitiveType.Quad, new Vector3(0f, 0.25f + height * 0.5f, 0f), new Vector3(1.1f, height, 1f), Quaternion.Euler(0f, 0f, lean), rift);
            Block(t, "Tear Cross", PrimitiveType.Quad, new Vector3(0f, 0.25f + height * 0.5f, 0f), new Vector3(0.8f, height * 0.9f, 1f), Quaternion.Euler(0f, 90f, lean), rift);
            GroundCrack(t, Vector3.zero, seed * 37f, 1.6f, rift);
            for (int k = 0; k < 5; k++)                                       // 틈에서 빨려 나와 떠 있는 조각
            {
                float a = k * 72f + seed * 23f, r = 0.7f + (k % 2) * 0.35f;
                var at = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0.5f + height * (0.15f + 0.17f * k), Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.6f);
                var rot = Quaternion.Euler(a, a * 1.7f, a * 0.6f);
                if (k % 2 == 0) Rock(t, "Floating Stone", at, 0.16f + 0.05f * (k % 3), seed + k, a, stone, false);
                else Block(t, "Floating Glass", PrimitiveType.Cube, at, new Vector3(0.22f, 0.28f, 0.02f), rot, glass[(seed + k) % glass.Length]);
            }
        }

        // 땅에 난 빛나는 균열 (바닥에 누운 틈 판)
        static void GroundCrack(Transform parent, Vector3 foot, float yaw, float length, Material rift)
        {
            var t = At(parent, "Ground Crack (땅 균열)", foot, yaw);
            Block(t, "Crack", PrimitiveType.Quad, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, length, 1f), Quaternion.Euler(90f, 0f, 0f), rift);
        }

        // 반쯤 땅에 묻혀 기운 고딕 첨두 아치: 기둥 둘(한쪽은 부러짐) 위로 두 호가 뾰족하게 만나요
        static void GothicArch(Transform parent, Vector3 foot, float yaw, int seed, Material stone)
        {
            var t = At(parent, "Gothic Arch (고딕 아치)", foot + Vector3.down * 0.35f, yaw);
            t.localRotation *= Quaternion.Euler(0f, 0f, (seed % 2 == 0 ? 1f : -1f) * 7f);   // 땅이 꺼져 기울었어요
            const float span = 1.1f, pillarH = 2.4f, w = 0.32f;
            bool broken = seed % 3 == 0;
            for (int s = -1; s <= 1; s += 2)
            {
                float h = broken && s > 0 ? 1.3f : pillarH;
                Block(t, "Pillar", PrimitiveType.Cube, new Vector3(s * span, h * 0.5f, 0f), new Vector3(w, h, w), Quaternion.identity, stone);
                Block(t, "Plinth", PrimitiveType.Cube, new Vector3(s * span, 0.12f, 0f), new Vector3(w * 1.5f, 0.24f, w * 1.5f), Quaternion.identity, stone);
                if (broken && s > 0) continue;                                   // 부러진 쪽은 호가 없어요
                // 호: 기둥 꼭대기에서 가운데 위로 휘어 올라가요 (반지름 = 폭, 중심은 반대쪽 기둥 위 — 첨두 아치)
                var c = new Vector3(-s * span * 0.35f, pillarH, 0f);
                float radius = span * 1.35f;
                for (int k = 0; k < 5; k++)
                {
                    float a0 = Mathf.Lerp(0f, 75f, k / 5f), a1 = Mathf.Lerp(0f, 75f, (k + 1) / 5f);   // 75도에서 두 호가 가운데(x = 0)에서 만나요
                    Vector3 P(float a) => c + new Vector3(s * Mathf.Cos(a * Mathf.Deg2Rad) * radius, Mathf.Sin(a * Mathf.Deg2Rad) * radius, 0f);
                    var p0 = P(a0); var p1 = P(a1);
                    var dir = p1 - p0;
                    Block(t, "Arch Stone", PrimitiveType.Cube, (p0 + p1) * 0.5f, new Vector3(w * 0.9f, dir.magnitude + 0.04f, w * 0.9f),
                        Quaternion.FromToRotation(Vector3.up, dir.normalized), stone);
                }
            }
            Rock(t, "Fallen Block", new Vector3(span * 1.6f, 0.35f, 0.5f), 0.3f, seed, seed * 31f, stone, false);
        }

        // 깨진 스테인드글라스 조각 더미: 납틀 낀 유리판 몇 장이 기대고 눕고
        static void GlassShards(Transform parent, Vector3 foot, float yaw, int seed, Material[] glass, Material lead)
        {
            var t = At(parent, "Stained Glass (깨진 스테인드글라스)", foot, yaw);
            for (int k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(k == 0 ? 70f : 85f - k * 25f, k * 47f + seed * 11f, (k - 1.5f) * 12f);
                var at = new Vector3((k - 1.5f) * 0.28f, 0.12f + (k == 0 ? 0.2f : 0f), (k % 2) * 0.22f);
                var size = new Vector3(0.38f - k * 0.04f, 0.5f - k * 0.06f, 0.02f);
                Block(t, "Pane", PrimitiveType.Cube, at, size, rot, glass[(seed + k) % glass.Length]);
                Block(t, "Lead", PrimitiveType.Cube, at, new Vector3(size.x + 0.03f, 0.03f, 0.03f), rot, lead);   // 가운데 가로 납선
                Block(t, "Lead", PrimitiveType.Cube, at, new Vector3(0.03f, size.y + 0.03f, 0.03f), rot, lead);   // 세로 납선
            }
        }

        // 룬 선돌: 위로 갈수록 좁아지는 기운 돌기둥, 앞면에 빛나는 룬 획 셋
        static void RuneStone(Transform parent, Vector3 foot, float yaw, float height, Material stone, Material rift)
        {
            var t = At(parent, "Rune Stone (룬 선돌)", foot, yaw);
            var tilt = Quaternion.Euler(0f, 0f, 6f);
            Block(t, "Stone", PrimitiveType.Cube, tilt * new Vector3(0f, height * 0.4f, 0f), new Vector3(0.6f, height * 0.8f, 0.42f), tilt, stone);
            Block(t, "Top", PrimitiveType.Cube, tilt * new Vector3(0f, height * 0.88f, 0f), new Vector3(0.42f, height * 0.18f, 0.32f), tilt * Quaternion.Euler(0f, 0f, 8f), stone);
            for (int k = 0; k < 3; k++)
                Block(t, "Rune", PrimitiveType.Quad, tilt * new Vector3((k - 1) * 0.12f, height * (0.3f + 0.17f * k), -0.22f), new Vector3(0.12f, 0.3f, 1f),
                    tilt * Quaternion.Euler(0f, 0f, (k - 1) * 25f), rift);
            Rock(t, "Base", new Vector3(0.3f, 0f, 0.15f), 0.25f, (int)height, height * 40f, stone, false);
        }
    }
}
