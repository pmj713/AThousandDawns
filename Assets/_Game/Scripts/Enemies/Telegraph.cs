using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Enemies
{
    // 바닥 예고 (색감 시트: #FF9A2E + 흰 테두리). 예고 시간 동안 안쪽이 앞으로 차올라요.
    public sealed class Telegraph : MonoBehaviour
    {
        const float BaseHeight = 0.03f;
        const float FillHeight = 0.04f;
        const float OutlineHeight = 0.05f;
        const float OutlineWidth = 0.06f;

        Mesh _mesh;
        MeshRenderer _base;
        MeshRenderer _fill;
        LineRenderer _outline;
        Transform _fillTransform;

        public bool IsVisible => _base != null && _base.enabled;

        public static Telegraph Create(Transform parent, Material baseMat, Material fillMat, Material outlineMat)
        {
            var root = new GameObject("Telegraph");
            root.transform.SetParent(parent, false);
            var telegraph = root.AddComponent<Telegraph>();
            telegraph._mesh = new Mesh { name = "Telegraph" };
            telegraph._base = CreateLayer(root.transform, "Base", BaseHeight, telegraph._mesh, baseMat).GetComponent<MeshRenderer>();
            var fill = CreateLayer(root.transform, "Fill", FillHeight, telegraph._mesh, fillMat);
            telegraph._fill = fill.GetComponent<MeshRenderer>();
            telegraph._fillTransform = fill.transform;

            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(root.transform, false);
            var line = outlineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = OutlineWidth;
            line.numCornerVertices = 2;
            line.sharedMaterial = outlineMat;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            telegraph._outline = line;

            telegraph.Hide();
            return telegraph;
        }

        static GameObject CreateLayer(Transform parent, string name, float height, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        public void Show(List<Vector2> polygon)
        {
            SetShape(polygon);
            SetProgress(0f);
            _base.enabled = _fill.enabled = _outline.enabled = true;
        }

        // 모양만 바꿔요 (차오른 정도는 그대로). 돌진·화살 예고가 장애물에 맞춰 짧아질 때 써요.
        public void SetShape(List<Vector2> polygon)
        {
            var vertices = new Vector3[polygon.Count + 1];
            var triangles = new int[polygon.Count * 3];
            Vector2 center = Vector2.zero;
            foreach (var p in polygon) center += p;
            center /= polygon.Count;
            vertices[0] = new Vector3(center.x, 0f, center.y);
            for (int i = 0; i < polygon.Count; i++)
            {
                vertices[i + 1] = new Vector3(polygon[i].x, 0f, polygon[i].y);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % polygon.Count + 1;
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            _outline.positionCount = polygon.Count;
            for (int i = 0; i < polygon.Count; i++)
                _outline.SetPosition(i, new Vector3(polygon[i].x, OutlineHeight, polygon[i].y));
        }

        public void SetPose(Vector3 origin, Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            transform.SetPositionAndRotation(new Vector3(origin.x, 0f, origin.z), Quaternion.LookRotation(forward));
        }

        public void SetProgress(float t) => _fillTransform.localScale = new Vector3(1f, 1f, Mathf.Clamp(t, 0.001f, 1f));

        public void Hide()
        {
            if (_base == null) return;
            _base.enabled = _fill.enabled = _outline.enabled = false;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
