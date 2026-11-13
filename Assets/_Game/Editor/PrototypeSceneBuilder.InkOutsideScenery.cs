using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 벽 너머 지역 풍경 (4차 플레이 테스트 "맵 밖으로 허전하다"): 죽림 대숲처럼 폐사찰·장강·천마총도 방 바깥 띠를 채워요.
    // 자리 잡기는 벽 너머 대나무와 같은 규칙(모든 방 바닥 둘레, 북·동·서 12 · 카메라 쪽 남 7, 방 안·키 큰 것 자리는 비움)이라
    // 시작 방·관문 방·객잔·정비실·결투장·보스방까지 같은 지역 배경이 보여요. 남쪽 띠는 시야를 가리지 않게 낮은 것만.
    //   폐사찰: 소나무 · 무너진 전각 · 석등 · 비석 · 부러진 돌기둥 · 돌계단 · 이끼 낀 바위
    //   장강 수채: 물웅덩이 물가 · 갈대 · 나루 말뚝과 잔교 · 거룻배/부서진 배
    //   천마총: 무덤 봉분과 묘비 · 마른 나무 · 피 비문 · 검은 바위 · 붉은 등불 기둥 · 핏자국 (붉은 안개는 InkTombAir가 카메라 둘레에 깔아요)
    // 보기용이라 충돌 없음, 그림자 끔·정적 배칭 (InkPerf의 OutsideRoots/StaticDecoRoots). InkOutsideGround가 지역마다 하나만 켜요.
    public static partial class PrototypeSceneBuilder
    {
        static GameObject[] DressOutsideScenery(Material stoneMat, Material woodMat, Material roofX)
        {
            var moss = GetInkMaterial("Ink_Temple_Moss", "#5E6B3A", 0.5f);
            var earth = GetInkMaterial("Ink_Temple_Earth", "#5E5142", 0.4f);
            var reed = RiverMat("Ink_River_Reed", "#7C8452", 0.45f);
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var dark = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);
            var wet = RiverMat("Ink_River_WetStone", "#4A4E4C", 0.3f);
            var water = RiverMat("Ink_River_OutsideWater", "#56676A", 0.35f);
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var blood = GetInkMaterial("Ink_Tomb_Blood", "#4E0B0A", 0.7f);
            var thorn = GetInkMaterial("Ink_Tomb_Thorn", "#2A1E1C", 0.3f);
            var mound = GetInkMaterial("Ink_Tomb_Mound", "#3B3430", 0.3f);

            var temple = new GameObject("Ink Outside Scenery (폐사찰)").transform;
            var river = new GameObject("Ink Outside Scenery (장강 수채)").transform;
            var tomb = new GameObject("Ink Outside Scenery (천마총)").transform;
            var rng = new System.Random(77);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int seed = 900;

            foreach (var (p, south) in OutsideSpots(2.8f, 0.15f, 2.2f, 4321))
            {
                float pick = R(0f, 1f), yaw = R(0f, 360f);
                // ── 폐사찰
                if (south)
                {
                    if (pick < 0.35f) StoneSteps(temple, p, yaw, stoneMat, moss);
                    else if (pick < 0.75f) RockCluster(temple, p, R(0.3f, 0.5f), seed, stoneMat);
                    else FoliageCards(temple, "Grass", "Bush_B", p + Vector3.up * 0.2f, 0.45f, 2, yaw);
                }
                else if (pick < 0.42f) PineTree(temple, p, R(3.5f, 5.5f), seed, woodMat);
                else if (pick < 0.54f) RuinedHall(temple, p, seed, stoneMat, woodMat, roofX);
                else if (pick < 0.66f) StoneLantern(temple, p, stoneMat, roofX);
                else if (pick < 0.75f) Stele(At(temple, "Stele (비석)", p, yaw), seed, stoneMat, roofX);
                else if (pick < 0.84f) BrokenPillar(At(temple, "Broken Pillar (부러진 돌기둥)", p, yaw), R(0.8f, 2f), seed, stoneMat);
                else if (pick < 0.92f) StoneSteps(temple, p, yaw, stoneMat, moss);
                else RockCluster(temple, p, R(0.5f, 0.8f), seed, stoneMat);

                // ── 장강 수채 (같은 자리, 다른 뽑기)
                pick = R(0f, 1f);
                if (south)
                {
                    if (pick < 0.6f) ReedClump(river, p, seed, reed);
                    else RockCluster(river, p, R(0.25f, 0.45f), seed, wet);
                }
                else if (pick < 0.3f) { Pool(river, p, R(2.2f, 3.4f), water, wet); ReedClump(river, p + new Vector3(R(-1.6f, 1.6f), 0f, 1.4f), seed + 1, reed); }
                else if (pick < 0.42f) { Pool(river, p, 3.2f, water, wet); Boat(river, p + Vector3.up * 0.02f, seed, dark, woodMat, rng.NextDouble() < 0.4); }
                else if (pick < 0.5f) Jetty(river, p, yaw, woodMat, dark, rope);
                else if (pick < 0.62f) Pilings(river, p, seed, dark, rope);
                else if (pick < 0.9f) for (int k = 0; k < 3; k++) ReedClump(river, p + new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f)), seed + k, reed);
                else RockCluster(river, p, R(0.4f, 0.7f), seed, wet);

                // ── 천마총
                pick = R(0f, 1f);
                if (south)
                {
                    if (pick < 0.4f) BloodPool(tomb, p, R(0.5f, 0.9f), seed, blood, marble);
                    else if (pick < 0.75f) RockCluster(tomb, p, R(0.3f, 0.5f), seed, marble);
                    else Burial(tomb, p, yaw, 0.6f, mound, marble);
                }
                else if (pick < 0.32f) Burial(tomb, p, yaw, R(0.9f, 1.4f), mound, marble);
                else if (pick < 0.56f) DeadTree(tomb, p, R(2.6f, 4f), seed, thorn);
                else if (pick < 0.66f) BloodStele(tomb, p, seed, marble, blood, roofX);
                else if (pick < 0.86f) RockCluster(tomb, p, R(0.6f, 1f), seed, marble);
                else if (pick < 0.92f) LanternPillar(tomb, p, seed, marble, red, roofX);
                else BloodPool(tomb, p, R(0.7f, 1.2f), seed, blood, marble);
                seed++;
            }

            var roots = new[] { temple.gameObject, river.gameObject, tomb.gameObject };
            foreach (var root in roots) { StripColliders(root.transform); root.SetActive(false); }
            Debug.Log($"[수묵 시험판] 벽 너머 지역 풍경: 폐사찰 {temple.childCount} · 장강 {river.childCount} · 천마총 {tomb.childCount}");
            return roots;
        }

        static Transform At(Transform parent, string name, Vector3 foot, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        // 이끼 낀 돌계단 세 단 (어디로도 이어지지 않는 옛 절 계단)
        static void StoneSteps(Transform parent, Vector3 foot, float yaw, Material stone, Material moss)
        {
            var t = At(parent, "Stone Steps (돌계단)", foot, yaw);
            for (int k = 0; k < 3; k++)
                Block(t, $"Step_{k + 1}", PrimitiveType.Cube, new Vector3(0f, 0.09f + k * 0.18f, k * 0.38f), new Vector3(1.6f - k * 0.08f, 0.18f, 0.42f), Quaternion.Euler(0f, (k - 1) * 2f, 0f), stone);
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(0.55f, 0.2f, 0.1f), new Vector3(0.45f, 0.1f, 0.3f), Quaternion.identity, moss);
        }

        // 물가 웅덩이: 낮은 물 원판과 둘레 젖은 돌
        static void Pool(Transform parent, Vector3 foot, float size, Material water, Material wet)
        {
            var t = At(parent, "Pool (물가)", foot, 0f);
            Block(t, "Water", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(size, 0.01f, size * 0.7f), Quaternion.identity, water);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f * Mathf.Deg2Rad + size;
                Rock(t, "Bank Stone", new Vector3(Mathf.Cos(a) * size * 0.5f, 0f, Mathf.Sin(a) * size * 0.35f), 0.18f + k % 2 * 0.08f, k, k * 53f, wet, false);
            }
        }

        // 나루 잔교: 말뚝 넷 위에 판자, 끝에 밧줄 감긴 말뚝
        static void Jetty(Transform parent, Vector3 foot, float yaw, Material wood, Material dark, Material rope)
        {
            var t = At(parent, "Jetty (나루)", foot, yaw);
            for (int k = 0; k < 4; k++)
                Block(t, "Post", PrimitiveType.Cylinder, new Vector3(k % 2 == 0 ? -0.55f : 0.55f, 0.3f, k < 2 ? -1f : 1f), new Vector3(0.14f, 0.3f, 0.14f), Quaternion.identity, dark);
            for (int k = 0; k < 6; k++)
                Block(t, "Plank", PrimitiveType.Cube, new Vector3(0f, 0.62f, -1.1f + k * 0.44f), new Vector3(1.3f, 0.06f, 0.38f), Quaternion.Euler(0f, (k % 3 - 1) * 2f, 0f), wood);
            Block(t, "Rope", PrimitiveType.Cylinder, new Vector3(0.55f, 0.5f, 1f), new Vector3(0.2f, 0.04f, 0.2f), Quaternion.identity, rope);
        }

        // 무덤 봉분과 앞에 선 작은 묘비
        static void Burial(Transform parent, Vector3 foot, float yaw, float size, Material mound, Material marble)
        {
            var t = At(parent, "Burial Mound (봉분)", foot, yaw);
            Block(t, "Mound", PrimitiveType.Sphere, Vector3.zero, new Vector3(size * 1.8f, size * 0.9f, size * 1.6f), Quaternion.identity, mound);
            Block(t, "Gravestone", PrimitiveType.Cube, new Vector3(0f, size * 0.35f, -size * 1.0f), new Vector3(size * 0.45f, size * 0.7f, 0.12f), Quaternion.Euler(-4f, 0f, 3f), marble);
        }

        // 잎 없는 마른 나무: 비틀린 줄기에 갈래 가지
        static void DeadTree(Transform parent, Vector3 foot, float height, int seed, Material thorn)
        {
            var t = At(parent, "Dead Tree (마른 나무)", foot, seed * 47f % 360f);
            Block(t, "Trunk", PrimitiveType.Capsule, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.22f, height * 0.42f, 0.22f), Quaternion.Euler(0f, 0f, (seed % 3 - 1) * 5f), thorn);
            for (int k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 90f + seed * 13f, 38f + k % 2 * 14f);
                float y = height * (0.55f + k * 0.1f), len = height * (0.28f - k * 0.03f);
                Block(t, "Branch", PrimitiveType.Capsule, new Vector3(0f, y, 0f) + rot * new Vector3(0f, len * 0.5f, 0f), new Vector3(0.09f, len * 0.5f, 0.09f), rot, thorn);
                var twig = rot * Quaternion.Euler(0f, 0f, -45f);
                Block(t, "Twig", PrimitiveType.Capsule, new Vector3(0f, y, 0f) + rot * new Vector3(0f, len, 0f) + twig * new Vector3(0f, len * 0.25f, 0f), new Vector3(0.05f, len * 0.25f, 0.05f), twig, thorn);
            }
        }
    }
}
