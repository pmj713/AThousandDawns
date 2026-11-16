
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 무기 채색: 무기는 모델마다 한 덩어리 메시(prepare_enemy.py "_Weapon_*")라, 프리팹을 만들 때 메시 복사본에 부위별 정점 색을 칠해요.
    //   기준: 무기를 쥔 뼈(손) 자리와 무기의 긴 축. 손 둘레는 손잡이, 손에서 먼 쪽 끝은 머리(날·추), 그 사이가 자루·칼날이에요.
    //     칼(dao·jian·dagger·bigdao·hooksword): 손잡이 짙은 가죽 감개 · 코등이 놋쇠 · 칼날 푸른 강철
    //     자루 무기(선장·작살·깃대·도끼·싸리비·몽둥이·해골 지팡이·쇠닻): 자루 나무, 머리는 무기마다 (놋쇠 고리 · 강철 날 · 볏짚 · 쇠못 박은 짙은 나무 · 뼈 · 쇠)
    //     활·방패·목탁: 나무 / 큰 방패·유성추: 쇠
    //   InkCharacter의 _VertexTint = 1인 무기 재질이 이 정점 색을 곱해요 (재질 색은 흰색).
    // 복사한 메시는 그 적 폴더에 <이름>_<무기>_Tinted.asset으로 남아요 (프리팹처럼 시험판 빌드 때마다 다시 만들어요)
    public static partial class PrototypeSceneBuilder
    {
        static readonly Color Steel = new Color(0.58f, 0.64f, 0.74f), Brass = new Color(0.78f, 0.56f, 0.2f), Wrap = new Color(0.26f, 0.17f, 0.12f),
            Wood = new Color(0.5f, 0.32f, 0.18f), DarkWood = new Color(0.32f, 0.2f, 0.12f), Iron = new Color(0.34f, 0.36f, 0.4f),
            Straw = new Color(0.86f, 0.72f, 0.4f), Bone = new Color(0.93f, 0.88f, 0.74f);

        static void TintWeapon(Renderer r, string folder, string enemy)
        {
            if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) return;
            string key = r.name.Substring(r.name.IndexOf("_Weapon_") + 8).ToLowerInvariant();
            var src = smr.sharedMesh;
            var verts = src.vertices;
            if (verts.Length == 0) return;

            // 쥔 뼈 자리 (메시 공간): 무게가 가장 많이 실린 뼈의 bindpose 역행렬 원점
            var bw = src.boneWeights;
            int bone = bw.Length > 0 ? bw[0].boneIndex0 : 0;
            var poses = src.bindposes;
            var anchor = bone < poses.Length ? poses[bone].inverse.MultiplyPoint(Vector3.zero) : Vector3.zero;

            // 긴 축: 손에서 가장 먼 정점 쪽
            var far = verts[0];
            foreach (var v in verts) if ((v - anchor).sqrMagnitude > (far - anchor).sqrMagnitude) far = v;
            var axis = (far - anchor).normalized;
            float reach = Vector3.Dot(far - anchor, axis);

            // 면마다 따로 칠해요: 무기는 면이 길어서(칼날이 코등이에서 끝까지 한 면) 정점을 나눠 쓰면 색이 번져 띠가 흐려져요.
            //   삼각형마다 정점을 따로 두고 삼각형 가운데로 부위를 골라요
            Color Part(Vector3 p)
            {
                float t = Vector3.Dot(p - anchor, axis);           // 손에서 머리 쪽으로 (손 = 0)
                float k = reach > 1e-4f ? t / reach : 0f;
                return key switch
                {
                    "dao" or "jian" or "dagger" or "bigdao" or "hooksword" => t < 0.12f ? Wrap : t < 0.17f ? Brass : Steel,
                    "khakkhara" => k > 0.8f ? Brass : t < -0.2f ? Brass : Wood,
                    "harpoon" => k > 0.8f ? Steel : Wood,
                    "axe" => k > 0.62f ? Steel : Wood,
                    "broom" => k > 0.72f ? Straw : Wood,
                    "club" => k > 0.45f ? DarkWood : Wood,
                    "skullstaff" => k > 0.8f ? Bone : DarkWood,
                    "flagpole" or "pole" => Wood,
                    "anchor" or "meteor" or "towershield" or "bomb" => Iron,
                    "shield" or "bow" or "moktak" or "mallet" => Wood,
                    _ => Steel,
                };
            }
            var normals = src.normals;
            var uv = src.uv;
            var nv = new System.Collections.Generic.List<Vector3>();
            var nn = new System.Collections.Generic.List<Vector3>();
            var nuv = new System.Collections.Generic.List<Vector2>();
            var nbw = new System.Collections.Generic.List<BoneWeight>();
            var ncol = new System.Collections.Generic.List<Color>();
            var subs = new System.Collections.Generic.List<int[]>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                var outTris = new int[tris.Length];
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var col = Part((verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f).linear;   // 정점 색은 선형 값으로 셰이더에 가요 (sRGB 그대로 넣으면 하얗게 떠요)
                    for (int j = 0; j < 3; j++)
                    {
                        int v = tris[i + j];
                        outTris[i + j] = nv.Count;
                        nv.Add(verts[v]);
                        nn.Add(normals.Length > v ? normals[v] : Vector3.up);
                        nuv.Add(uv.Length > v ? uv[v] : Vector2.zero);
                        nbw.Add(bw.Length > v ? bw[v] : new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                        ncol.Add(col);
                    }
                }
                subs.Add(outTris);
            }
            var mesh = new Mesh { name = $"{src.name}_Tinted" };
            if (nv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(nv);
            mesh.SetNormals(nn);
            mesh.SetUVs(0, nuv);
            mesh.SetColors(ncol);
            mesh.boneWeights = nbw.ToArray();
            mesh.bindposes = poses;
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            string path = $"{folder}/{enemy}_{key}_Tinted.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            smr.sharedMesh = mesh;
        }

        static Material WeaponMaterial(Material m)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_VertexTint", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
