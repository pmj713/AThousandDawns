using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.EditorTools
{
    // 수묵 판 씬 마무리 (성능): 장식 조각이 수만 개라 그림자와 그리기 횟수를 줄여요.
    //  1) 그림자: 작은 조각(경계 상자 대각선 1.2 미만), 잎·불꽃 카드, 벽 너머 숲은 그림자를 드리우지 않아요 (받는 건 그대로).
    //     큰 건물·바위·기둥·나무 줄기만 그림자를 남겨요
    //  2) 정적 배칭: 수묵 장식 묶음(아래 이름의 루트)은 움직이지 않으니 BatchingStatic으로 표시해 빌드에서 합쳐 그려요.
    //     카메라를 마주 보는 카드(InkFoliageCard)·불꽃, 움직이는 소품(돌거나 흔들리는 스크립트 아래)은 빼요.
    //     켜고 끄기(지역 바꿈)는 정적 배칭과 함께 써도 돼요. 게임 코드가 만든 방 루트(Room_*)는 문·관문이 움직일 수 있어 배칭하지 않아요
    public static partial class PrototypeSceneBuilder
    {
        static readonly string[] StaticDecoRoots =
        {
            "Ink Outside Bamboo", "Ink Outside Scenery", "Ink Deco (", "Edge Decor ",   // Ink Deco (Rooms) · (Room_Floor) · (Room_Floor_Medium) · (Room_Floor_Large) · (Village)
            "Demon Cult Training Hall", "River Boss Dock", "Heavenly Demon Throne", "River Pier Arena", "Master Cloister", "Blood Pools",
        };
        static readonly string[] OutsideRoots = { "Ink Outside Bamboo", "Ink Outside Scenery" };

        static void OptimizeInkScene(UnityEngine.SceneManagement.Scene scene)
        {
            int renderers = 0, shadowsBefore = 0, shadowsAfter = 0, batched = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                bool outside = StartsWithAny(root.name, OutsideRoots);
                bool deco = StartsWithAny(root.name, StaticDecoRoots);
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderers++;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) shadowsBefore++;
                    string shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "";
                    bool card = shader.Contains("InkFoliageCard") || shader.Contains("InkFlame") || shader.Contains("Sprite");
                    bool small = r.bounds.size.magnitude < 1.2f;
                    if (r.shadowCastingMode != ShadowCastingMode.Off && (outside || card || small))
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) shadowsAfter++;

                    if (deco && !card && !UnderMovingScript(r.transform, root.transform))
                    {
                        var go = r.gameObject;
                        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.BatchingStatic);
                        batched++;
                    }
                }
            }
            Debug.Log($"[수묵 시험판 성능] 메시 렌더러 {renderers}개 · 그림자 드리움 {shadowsBefore} → {shadowsAfter} · 정적 배칭 표시 {batched}");
        }

        static bool StartsWithAny(string name, string[] prefixes)
        {
            foreach (var p in prefixes) if (name.StartsWith(p)) return true;
            return false;
        }

        // 위로 올라가며 움직이는 아트 스크립트(돌기·흔들기·켜고 끄는 불꽃)가 붙은 오브젝트가 있으면 배칭에서 빼요
        static readonly HashSet<string> MovingScripts = new HashSet<string>
        {
            "InkSpin", "InkVillageLife", "InkKarmaLanterns", "PlumBlossoms", "InkRegressionStele", "InkBurningGroveFX",
        };

        static bool UnderMovingScript(Transform t, Transform root)
        {
            for (var p = t; p != null; p = p.parent)
            {
                foreach (var mb in p.GetComponents<MonoBehaviour>())
                    if (mb != null && MovingScripts.Contains(mb.GetType().Name)) return true;
                if (p == root) break;
            }
            return false;
        }
    }
}
