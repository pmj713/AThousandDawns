using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 맵 바깥 땅 (InkOutsideGround): 벽 너머가 흰 종이로 비어 보이지 않게, 모든 방 바닥 아래로 큰 수묵 땅 한 장을 깔아요.
    // 방 바닥(floorMat)과 같은 셰이더에 조금 어두운 색 · 지역별 이음새 없는 땅 그림. 충돌 없음 (보기용).
    public static partial class PrototypeSceneBuilder
    {
        const string OutsideArtFolder = "Assets/_Game/Art/Env/Outside";

        static void DressOutsideInk(Material floorMat)
        {
            var paths = new[] { $"{OutsideArtFolder}/Ground_Bamboo.png", $"{OutsideArtFolder}/Ground_Temple.png" };
            foreach (var p in paths) ImportAsTiledTexture(p);
            var textures = System.Array.ConvertAll(paths, p => AssetDatabase.LoadAssetAtPath<Texture2D>(p));

            // 방들이 서로 멀리 떨어져 있으니 씬의 모든 것을 감싸는 범위로 (땅 높이는 바닥 재질을 쓰는 것 중 가장 낮은 윗면)
            var bounds = new Bounds();
            bool any = false;
            float floorTop = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                if (r.sharedMaterial == floorMat) floorTop = Mathf.Min(floorTop, r.bounds.max.y);
            }
            if (!any) bounds = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
            if (floorTop == float.MaxValue) floorTop = 0f;

            var mat = GetShaderMaterial("Ink_OutsideGround", floorMat.shader.name);
            mat.CopyPropertiesFromMaterial(floorMat);
            mat.SetColor("_BaseColor", floorMat.GetColor("_BaseColor") * new Color(0.8f, 0.8f, 0.8f, 1f)); // 방 바닥보다 한 톤 어둡게 (방이 도드라지게)
            if (textures[0] != null) mat.SetTexture("_BaseMap", textures[0]);
            EditorUtility.SetDirty(mat);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ink Outside Ground (맵 바깥 땅)";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.position = new Vector3(bounds.center.x, floorTop - 0.06f, bounds.center.z);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(bounds.size.x + 240f, bounds.size.z + 240f, 1f);
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.AddComponent<AThousandDawns.Art.InkOutsideGround>().Setup(textures);
        }
    }
}
