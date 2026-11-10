using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판의 판 안 소품: 항아리·금 간 벽(판 도중에 게임이 만듦)과 관문.
    //  - 항아리·금 간 벽: 씬에 InkRunProps를 두고 메시·재질·그림을 채워요. 생길 때마다 그 컴포넌트가 수묵 모양을 입혀요.
    //  - 관문: 씬에 이미 있는 먹색 원판에 돌 테두리와 천천히 도는 먹 소용돌이를 자식으로 달아요 (켜고 끄는 건 원판 그대로).
    public static partial class PrototypeSceneBuilder
    {
        const string RunPropsArtFolder = "Assets/_Game/Art/Env/RunProps";

        static void DressRunPropsInk(Material stoneMat)
        {
            ImportAsSprite($"{RunPropsArtFolder}/Wall_Crack.png");
            ImportAsSprite($"{RunPropsArtFolder}/Gate_Vortex.png");
            var crack = AssetDatabase.LoadAssetAtPath<Sprite>($"{RunPropsArtFolder}/Wall_Crack.png");
            var vortex = AssetDatabase.LoadAssetAtPath<Sprite>($"{RunPropsArtFolder}/Gate_Vortex.png");

            var jarMat = GetInkMaterial("Ink_Onggi", "#211712", 0.55f); // 짙은 갈색 유약 옹기 (객잔 술독 Ink_Jar보다 짙게)
            var lidMat = GetInkMaterial("Ink_PaperLid", "#E8DFC8", 0f);
            var jars = new[] { GetJarMesh(0), GetJarMesh(1), GetJarMesh(2) };
            new GameObject("Ink Run Props").AddComponent<AThousandDawns.Art.InkRunProps>()
                .Setup(jars, jarMat, lidMat, GetShardMesh(), crack, GetRockMesh(1), stoneMat);

            DressGateInk(stoneMat, vortex);
        }

        // 관문 원판(지름 2.4): 둘레를 두른 연석 여덟, 가운데 먹 소용돌이 (원판은 숨겨요)
        static void DressGateInk(Material stoneMat, Sprite vortex)
        {
            Transform gate = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Gate (관문)") { gate = t; break; }
            if (gate == null) return;
            var r = gate.GetComponent<Renderer>();
            if (r != null) r.enabled = false;

            var visual = new GameObject("Ink Visual").transform;
            visual.SetParent(gate, false);
            var s = gate.lossyScale;
            visual.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            visual.position = new Vector3(gate.position.x, 0f, gate.position.z);

            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Block(visual, "Ring_Stone", PrimitiveType.Cube, dir * 1.42f + Vector3.up * 0.05f, new Vector3(1.0f, 0.1f, 0.26f), Quaternion.Euler(0f, a, 0f), stoneMat); // 원을 따라 이어진 연석
            }
            if (vortex != null)
            {
                var spin = new GameObject("Vortex").transform;
                spin.SetParent(visual, false);
                spin.localPosition = Vector3.up * 0.04f;
                spin.gameObject.AddComponent<AThousandDawns.Art.InkSpin>();
                var card = new GameObject("Card").transform;
                card.SetParent(spin, false);
                card.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕혀요
                float k = 2.5f / Mathf.Max(vortex.bounds.size.x, vortex.bounds.size.y);
                card.localScale = new Vector3(k, k, k);
                card.localPosition = new Vector3(0f, 0f, -vortex.bounds.size.y * k * 0.5f); // 스프라이트 밑동 기준이라 가운데로
                card.gameObject.AddComponent<SpriteRenderer>().sprite = vortex;
            }
            StripColliders(visual);
        }

        // 판 소품은 플레이 중에만 생겨서, 게임과 같은 모양·자리로 잠깐 만들어 입혀 보고 찍은 뒤 지워요 (수묵 씬에서)
        [MenuItem("AThousandDawns/아트/판 소품 미리보기 저장")]
        static void CaptureRunProps()
        {
            var props = Object.FindFirstObjectByType<AThousandDawns.Art.InkRunProps>();
            if (props == null) { Debug.LogWarning("수묵 씬(Prototype_Run_Ink)을 열고 눌러 주세요"); return; }
            var temps = new List<GameObject>();
            float halfX = CombatSize.x * 0.5f - 1.2f, halfZ = CombatSize.y * 0.5f - 1.2f;
            Vector3[] jarSpots = { new Vector3(-halfX * 0.7f, 0f, halfZ * 0.8f), new Vector3(-halfX * 0.85f, 0f, halfZ * 0.65f), new Vector3(halfX * 0.75f, 0f, halfZ * 0.75f) };
            foreach (var spot in jarSpots)
            {
                var jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // RoomRun.PlaceJars와 같은 모양
                jar.name = "Jar";
                Object.DestroyImmediate(jar.GetComponent<Collider>());
                jar.transform.position = CombatCenter + spot + Vector3.up * 0.35f;
                jar.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
                props.DressNow(jar);
                temps.Add(jar);
            }
            var crack = GameObject.CreatePrimitive(PrimitiveType.Cube); // RoomRun.PlaceCrack, 북쪽 벽
            crack.name = "HiddenCrack";
            Object.DestroyImmediate(crack.GetComponent<Collider>());
            crack.transform.position = CombatCenter + Vector3.forward * CombatSize.y * 0.5f + Vector3.up * 1.1f;
            crack.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            crack.transform.localScale = new Vector3(0.25f, 2f, 0.6f);
            props.DressNow(crack);
            temps.Add(crack);
            var crackS = GameObject.CreatePrimitive(PrimitiveType.Cube); // 남쪽 벽 (카메라 쪽): 바깥 면·바닥 금으로 보여요
            crackS.name = "HiddenCrack";
            Object.DestroyImmediate(crackS.GetComponent<Collider>());
            crackS.transform.position = CombatCenter + Vector3.back * CombatSize.y * 0.5f + Vector3.right * 4f + Vector3.up * 1.1f;
            crackS.transform.rotation = Quaternion.LookRotation(Vector3.back);
            crackS.transform.localScale = new Vector3(0.25f, 2f, 0.6f);
            props.DressNow(crackS);
            temps.Add(crackS);

            GameObject gate = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Gate (관문)") { gate = t.gameObject; break; }
            bool wasActive = gate != null && gate.activeSelf;
            if (gate != null) gate.SetActive(true);

            ArtCapture.Capture("props_room", CombatCenter + new Vector3(0f, 0f, 1.5f), 1.1f);
            ArtCapture.Capture("props_jars", CombatCenter + new Vector3(-halfX * 0.75f, 0f, halfZ * 0.7f), 0.4f);
            ArtCapture.Capture("props_crack", CombatCenter + new Vector3(0f, 0.8f, CombatSize.y * 0.5f - 1f), 0.45f);
            ArtCapture.Capture("props_gate", CombatCenter, 0.45f);
            ArtCapture.Capture("props_crack_south", CombatCenter + new Vector3(4f, 0f, -CombatSize.y * 0.5f + 2f), 0.55f);

            if (gate != null) gate.SetActive(wasActive);
            foreach (var t in temps) Object.DestroyImmediate(t);
            EditorUtility.RevealInFinder(System.IO.Path.GetFullPath("Temp/ArtCapture"));
        }

        // 옹기 셋: 0 배부른 옹기, 1 목이 좁은 술독(입을 한지로 싸맴), 2 납작한 단지. 높이는 원래 원기둥(0.7)과 비슷하게
        static Mesh GetJarMesh(int variant)
        {
            Vector2[][] profiles =
            {
                new[] { new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.25f, 0.1f), new Vector2(0.3f, 0.3f), new Vector2(0.27f, 0.5f), new Vector2(0.17f, 0.62f), new Vector2(0.15f, 0.65f), new Vector2(0.18f, 0.69f), new Vector2(0.15f, 0.7f), new Vector2(0f, 0.68f) },
                new[] { new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.23f, 0.18f), new Vector2(0.25f, 0.4f), new Vector2(0.17f, 0.6f), new Vector2(0.1f, 0.68f), new Vector2(0.12f, 0.74f), new Vector2(0f, 0.74f) },
                new[] { new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.3f, 0.09f), new Vector2(0.32f, 0.24f), new Vector2(0.25f, 0.4f), new Vector2(0.2f, 0.44f), new Vector2(0.23f, 0.48f), new Vector2(0f, 0.47f) },
            };
            var profile = profiles[variant];
            const int sides = 18;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * sides + s, b = i * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            return SaveRunPropMesh($"Jar_{variant}", verts, tris);
        }

        // 사금파리: 얇게 휜 세모 조각
        static Mesh GetShardMesh()
        {
            var verts = new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.4f), new Vector3(0.5f, 0.05f, -0.3f), new Vector3(0f, 0.1f, 0.5f),
                new Vector3(-0.5f, -0.12f, -0.4f), new Vector3(0.5f, -0.07f, -0.3f), new Vector3(0f, -0.02f, 0.5f),
            };
            var tris = new List<int> { 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 };
            return SaveRunPropMesh("Jar_Shard", verts, tris);
        }

        static Mesh SaveRunPropMesh(string key, List<Vector3> verts, List<int> tris)
        {
            string path = $"{RunPropsArtFolder}/Meshes/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = key };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) { EnsureFolder($"{RunPropsArtFolder}/Meshes"); AssetDatabase.CreateAsset(mesh, path); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
