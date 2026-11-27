using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 1지역 죽림의 차원 겹침 (플레이테스트 9차: 대나무뿐이라 판타지가 섞인 느낌이 하나도 없었어요).
    //   벽 너머 대숲: 대나무 묶음 넷 중 하나를 서양 나무(VARCO 참나무 OakTree · 전나무 FirTree)로 바꿔요.
    //   방 안 대나무 묶음(장애물 자리): 둘 중 하나를 중세 판타지 소품(VARCO 돌 십자가 이정표 · 랜턴 기둥 · 나무 수레, 키 큰 자리는 무너진 아치)으로 바꿔요.
    //   바꾼 자리의 대나무는 꺼 두고, 새 소품은 죽림 꾸밈 묶음에 넣어 다른 지역에선 꺼져요. 충돌은 원래 상자 그대로 (소품엔 충돌체 없음).
    //   모델: Art/Env/BambooFantasy/Models (원본 glb는 Source~, 변환은 Rift/Tools~/prepare_prop.py). 모델이 없으면 대나무 그대로
    public static partial class PrototypeSceneBuilder
    {
        const string BambooFantasyFolder = "Assets/_Game/Art/Env/BambooFantasy/Models";

        static Transform BambooFantasyTree(Transform parent, Vector3 localPos, int seed)
        {
            string name = seed % 2 == 0 ? "OakTree" : "FirTree";
            float scale = 0.85f + (seed * 37 % 5) * 0.08f;
            var tree = VarcoProp(parent, name, localPos, seed * 67f % 360f, scale, BambooFantasyFolder);
            if (tree != null) ApplyVarcoTreeLeaves(tree, name);   // 잎 덩어리를 대나무 같은 잎다발로 (죽림 시안 2차, PrototypeSceneBuilder.Trees.cs)
            return tree;
        }

        static Transform BambooFantasyProp(Transform parent, Vector3 localPos, float height, int seed)
        {
            // 넷 중 하나는 무너진 아치 (키 큰 자리만), 나머지는 돌 십자가 이정표 · 랜턴 기둥 · 나무 수레를 돌아가며
            string[] low = { "StoneCross", "LanternPost", "WoodCart" };
            string name = height >= 2.2f && seed % 8 == 0 ? "RuinedArch" : low[(seed / 2) % low.Length];
            return VarcoProp(parent, name, localPos, seed * 53f % 360f, 1f, BambooFantasyFolder);
        }
    }
}
