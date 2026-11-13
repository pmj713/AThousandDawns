using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 청죽객이 쓰러뜨리는 대나무 (QingzhuBrain.FellBamboo, 이름 "Fallen Bamboo (쓰러진 대나무)", 6초 동안 벽인 초록 상자):
    //   상자는 숨기고 방 구성의 쓰러진 대나무 모델(InkRoomFeatures.FallenBambooModel, 길이가 X)을 길이에 맞춰 늘려 눕혀요.
    //   위에서 툭 떨어지며 흙먼지가 일고, 벽이 사라지면(상자가 지워지면) 땅으로 가라앉아요. 모델이 없으면 먹 대나무 획 두 줄로 대신해요.
    // 게임이 만든 루트 오브젝트는 이름을 처음 볼 때 한 번만 확인해요 (InkRootWatch).
    public class InkQingzhuFx : MonoBehaviour
    {
        const string Name = "Fallen Bamboo (쓰러진 대나무)";
        const float ModelLength = 3.5f;

        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        InkRoomFeatures _features;
        float _scan;
        static Material _lineMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var go = new GameObject("Ink Qingzhu Fx");
            DontDestroyOnLoad(go);
            go.AddComponent<InkQingzhuFx>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f)
            {
                _scan = 0.05f; // 쓰러지는 순간 바로
                InkRootWatch.Prune(_roots);
                foreach (var root in InkRootWatch.New(_roots))
                    if (root.name == Name) Dress(root);
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void Dress(GameObject wall)
        {
            var box = wall.GetComponent<Renderer>();
            if (box != null) box.enabled = false; // 초록 상자는 숨겨요 (충돌은 그대로)
            float length = wall.transform.localScale.z;
            var rot = wall.transform.rotation * Quaternion.Euler(0f, -90f, 0f); // 모델 길이(X)를 쓰러진 방향(상자 Z)으로
            var ground = new Vector3(wall.transform.position.x, 0f, wall.transform.position.z);

            if (_features == null) _features = FindAnyObjectByType<InkRoomFeatures>(); // 쓰러질 때만 (드물어요)
            var prefab = _features != null ? _features.FallenBambooModel : null;
            Transform model;
            if (prefab != null)
            {
                model = Instantiate(prefab).transform;
                model.name = "Ink Fallen Bamboo";
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c); // 막는 건 게임 상자가 해요
                model.localScale = new Vector3(length / ModelLength, 1f, 1f);
            }
            else model = Strokes(length);
            model.SetPositionAndRotation(ground + Vector3.up * 1.6f, rot);

            InkParticles.Burst(ground + Vector3.up * 0.1f, 14, new Color(0.5f, 0.46f, 0.38f, 0.65f), new Color(0.55f, 0.5f, 0.42f, 0.3f), 2.2f, 0.7f, 0.9f, 0.05f);
            float t = 0f, sink = -1f;
            _live.Add(dt =>
            {
                if (model == null) return false;
                t += dt;
                if (t < 0.22f) // 위에서 툭 떨어져요 (살짝 튕김)
                {
                    float k = t / 0.22f;
                    model.position = ground + Vector3.up * Mathf.Lerp(1.6f, 0f, k * k);
                    return true;
                }
                if (wall != null) { model.position = ground + Vector3.up * Mathf.Max(0f, Mathf.Sin((t - 0.22f) * 20f) * 0.06f * Mathf.Exp(-(t - 0.22f) * 10f)); return true; }
                if (sink < 0f) sink = 0f; // 벽이 사라졌어요: 땅으로 가라앉아요
                sink += dt;
                model.position = ground + Vector3.down * sink * 1.2f;
                if (sink < 0.5f) return true;
                Destroy(model.gameObject);
                return false;
            });
        }

        // 모델이 없을 때: 짙은 먹 대나무 줄기 두 줄
        Transform Strokes(float length)
        {
            if (_lineMat == null)
            {
                _lineMat = new Material(Shader.Find("Sprites/Default"));
                var brush = Resources.Load<Texture2D>("HudArt/HpStroke");
                if (brush != null) _lineMat.mainTexture = brush;
            }
            var root = new GameObject("Ink Fallen Bamboo").transform;
            for (int i = 0; i < 2; i++)
            {
                var lr = new GameObject("Stalk").AddComponent<LineRenderer>();
                lr.transform.SetParent(root, false);
                lr.useWorldSpace = false;
                lr.sharedMaterial = _lineMat;
                lr.positionCount = 2;
                float z = (i - 0.5f) * 0.18f;
                lr.SetPosition(0, new Vector3(-length * 0.5f, 0.25f, z));
                lr.SetPosition(1, new Vector3(length * 0.5f, 0.25f + i * 0.05f, z));
                lr.widthMultiplier = 0.22f - i * 0.06f;
                lr.startColor = lr.endColor = i == 0 ? new Color(0.14f, 0.2f, 0.12f) : new Color(0.32f, 0.45f, 0.26f);
                lr.sortingOrder = 6;
            }
            return root;
        }
    }
}
