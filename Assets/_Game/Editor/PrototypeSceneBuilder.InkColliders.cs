using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 장식 충돌체 (플레이 테스트: 방 꾸밈과 마을 사람을 그대로 통과했어요).
    // 장식은 만들 때 충돌체를 모두 지웠어요(StripColliders). 씬을 다 꾸민 뒤, 길을 막아야 할 조각에만 상자 충돌체를 다시 달아요.
    //  - 대상: 수묵 셰이더(InkCharacter · InkTriplanar)를 쓰는 메시 조각 (지역 꾸밈처럼 꺼져 있는 것도, 켜질 때 같이 켜져요)
    //  - 빼는 것: 바닥에 깔린 납작한 것·밟고 넘는 낮은 단(높이 0.35 미만) · 머리 위에 뜬 것(밑면 0.6 위: 지붕·등불·빨래) · 아주 작은 조각(가로·높이 0.3 미만)
    //    · 잎·불꽃 카드 · 벽 너머 숲 · 플레이어 · 흔들리는 빨래·닭 · 이미 충돌체가 있는 것 · 아주 큰 바닥판(가로 8 넘음)
    //  - 마을 주민: 몸통 크기 캡슐
    public static partial class PrototypeSceneBuilder
    {
        static void AddDecorColliders(UnityEngine.SceneManagement.Scene scene)
        {
            int added = 0;
            var player = Object.FindAnyObjectByType<AThousandDawns.Player.PlayerController>(FindObjectsInactive.Include);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (StartsWithAny(root.name, OutsideRoots)) continue;
                added += AddBlockingColliders(root.transform, player != null ? player.transform : null);
            }
            // 마을 주민: 몸통만큼 캡슐 (마을 꾸밈은 충돌체를 한꺼번에 지운 뒤라 여기서 달아요)
            foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!a.name.Contains("마을 주민") || a.GetComponent<Collider>() != null) continue;
                bool sitting = a.name.StartsWith("VillagerSit");
                var body = a.gameObject.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, sitting ? 0.55f : 0.85f, 0f);
                body.height = sitting ? 1.1f : 1.7f;
                body.radius = 0.3f;
                added++;
            }
            Debug.Log($"[수묵 시험판] 장식 충돌체 {added}개");
        }

        // 한 묶음 안의 수묵 조각마다, 길을 막아야 하면 상자 충돌체를 달아요 (규칙은 위 설명). 천마총 이벤트 소품 프리팹도 저장할 때 써요
        static readonly Vector3[] s_corners = new Vector3[8];

        static int AddBlockingColliders(Transform root, Transform exclude = null)
        {
            int added = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || r.sharedMaterial == null || r.GetComponent<Collider>() != null) continue;
                string shader = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "";
                if (!shader.Contains("InkCharacter") && !shader.Contains("InkTriplanar")) continue;
                if (exclude != null && r.transform.IsChildOf(exclude)) continue;
                if (SkipCollider(r.transform)) continue;
                // 메시 경계 상자를 월드로 (꺼진 오브젝트는 renderer.bounds가 비어서 직접 계산해요)
                var b = mf.sharedMesh.bounds;
                var m = r.transform.localToWorldMatrix;
                int i = 0;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    s_corners[i++] = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                var w = new Bounds(s_corners[0], Vector3.zero);
                for (int k = 1; k < 8; k++) w.Encapsulate(s_corners[k]);
                float height = w.size.y, span = Mathf.Max(w.size.x, w.size.z);
                if (height < 0.35f || w.min.y > 0.6f || span > 8f) continue;   // 낮은 단·계단은 밟고 넘어가요
                if (span < 0.3f && height < 0.3f) continue;
                r.gameObject.AddComponent<BoxCollider>();   // 메시 경계에 맞춰 붙어요
                added++;
            }
            return added;
        }

        // 흔들리는 빨래·닭, 벽 너머 숲, 그리고 상호작용 자리가 있는 마을 건물·나무(객잔 마루에서 시작해요, 매화나무·사당 앞에 서야 해요)
        static readonly string[] NoColliderParts = { "Cloth", "Chicken", "Forest (3D stalks)", "Inn", "Shrine", "Plum", "Gate", "Weapon Rack", "Cave", "Border " };

        static bool SkipCollider(Transform t)
        {
            foreach (var part in NoColliderParts) if (UnderName(t, part)) return true;
            return false;
        }

        static bool UnderName(Transform t, string part)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.Contains(part)) return true;
            return false;
        }
    }
}
