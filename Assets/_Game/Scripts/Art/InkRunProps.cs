using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 판 도중에 게임 코드가 만드는 소품(항아리 "Jar", 금 간 벽 "HiddenCrack")에 수묵 겉모습을 입혀요.
    // 게임 코드는 그대로 두고, 씬 맨 위에 새로 생긴 이름을 보고 원래 렌더러만 끈 뒤 수묵 모양을 자식으로 달아요.
    // 항아리가 깨지면(게임이 Destroy) 수묵 모양도 같이 사라지고, 그 자리에 사금파리가 흩어져요.
    // 참조(메시·재질·그림)는 씬 빌더(PrototypeSceneBuilder.InkProps)가 채워요.
    public class InkRunProps : MonoBehaviour
    {
        [SerializeField] Mesh[] jarMeshes = new Mesh[0];
        [SerializeField] Material jarMaterial;
        [SerializeField] Material lidMaterial;
        [SerializeField] Mesh shardMesh;
        [SerializeField] Sprite crackSprite;
        [SerializeField] Mesh rubbleMesh;
        [SerializeField] Material rubbleMaterial;

        const float ScanInterval = 0.2f;
        const string Marker = "Ink Visual";
        float _nextScan;
        readonly List<GameObject> _roots = new List<GameObject>();

        public void Setup(Mesh[] jars, Material jar, Material lid, Mesh shard, Sprite crack, Mesh rubble, Material rubbleMat)
        {
            jarMeshes = jars; jarMaterial = jar; lidMaterial = lid; shardMesh = shard;
            crackSprite = crack; rubbleMesh = rubble; rubbleMaterial = rubbleMat;
        }

        // 에디터 미리보기용: 지금 바로 입혀요 (아트 → 판 소품 미리보기 저장)
        public void DressNow(GameObject go)
        {
            if (go.name == "Jar") DressJar(go);
            else if (go.name == "HiddenCrack") DressCrack(go);
        }

        void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);
            foreach (var go in _roots)
            {
                if (go.name != "Jar" && go.name != "HiddenCrack") continue;
                if (go.transform.Find(Marker) != null) continue;
                if (go.name == "Jar") DressJar(go);
                else DressCrack(go);
            }
        }

        // 원래 원기둥(지름 0.55, 높이 0.7)과 같은 자리에 옹기. 부모 크기를 되돌려 메시는 실제 크기로
        void DressJar(GameObject jar)
        {
            if (jarMeshes.Length == 0 || jarMaterial == null) return;
            var r = jar.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var p = jar.transform.position;
            int variant = Mathf.Abs(Mathf.RoundToInt(p.x * 7.3f + p.z * 3.1f)) % jarMeshes.Length;

            var visual = Child(jar.transform, Marker);
            visual.position = new Vector3(p.x, p.y - jar.transform.lossyScale.y, p.z); // 원기둥 밑면 = 바닥
            visual.rotation = Quaternion.Euler(0f, (p.x * 53f + p.z * 31f) % 360f, 0f);
            Unscale(visual, jar.transform, 1f);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = jarMeshes[variant];
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = jarMaterial;
            if (variant == 1 && lidMaterial != null)
            {
                // 술독: 입을 한지로 싸매고 끈으로 묶었어요
                var lid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var lidCollider = lid.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(lidCollider); else DestroyImmediate(lidCollider);
                lid.name = "Paper Lid";
                lid.transform.SetParent(visual, false);
                lid.transform.localPosition = new Vector3(0f, 0.74f, 0f);
                lid.transform.localScale = new Vector3(0.26f, 0.09f, 0.26f);
                lid.GetComponent<Renderer>().sharedMaterial = lidMaterial;
            }
            visual.gameObject.AddComponent<InkJarShatter>().Setup(shardMesh, jarMaterial);
        }

        // 벽에 세로로 난 금: 방 쪽으로 금 그림 한 장, 발밑에 떨어진 돌 부스러기
        void DressCrack(GameObject crack)
        {
            var r = crack.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var t = crack.transform;
            var visual = Child(t, Marker);
            visual.position = t.position - t.forward * 0.02f; // 벽 안쪽 면 (crack의 forward = 벽 쪽)
            visual.rotation = t.rotation;
            Unscale(visual, t, 1f);
            if (crackSprite != null)
            {
                var card = new GameObject("Crack").transform;
                card.SetParent(visual, false);
                card.localPosition = new Vector3(0f, -1.1f, 0f); // 금 그림 밑동 = 바닥
                float k = 1.9f / crackSprite.bounds.size.y; // 담 높이 안에서 (기와까지 올라가지 않게)
                card.localScale = new Vector3(k, k, k);
                card.gameObject.AddComponent<SpriteRenderer>().sprite = crackSprite;
            }
            if (rubbleMesh != null && rubbleMaterial != null)
                for (int i = 0; i < 4; i++)
                {
                    var stone = new GameObject("Rubble").transform;
                    stone.SetParent(visual, false);
                    stone.localPosition = new Vector3((i - 1.5f) * 0.28f, -1.1f, -0.25f - (i % 2) * 0.2f);
                    stone.localRotation = Quaternion.Euler(0f, i * 77f, 0f);
                    stone.localScale = Vector3.one * (0.18f + (i % 3) * 0.07f);
                    stone.gameObject.AddComponent<MeshFilter>().sharedMesh = rubbleMesh;
                    stone.gameObject.AddComponent<MeshRenderer>().sharedMaterial = rubbleMaterial;
                }
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // 부모의 크기 조정을 되돌려 자식이 월드에서 size 배율로 보이게
        static void Unscale(Transform child, Transform parent, float size)
        {
            var s = parent.lossyScale;
            child.localScale = new Vector3(size / Mathf.Max(0.0001f, s.x), size / Mathf.Max(0.0001f, s.y), size / Mathf.Max(0.0001f, s.z));
        }
    }

    // 항아리가 깨지면(부모가 Destroy) 사금파리 몇 조각이 튀었다가 바닥에서 옅어져요
    public class InkJarShatter : MonoBehaviour
    {
        Mesh _shard;
        Material _material;
        static bool s_quitting;

        public void Setup(Mesh shard, Material material) { _shard = shard; _material = material; }

        void OnApplicationQuit() => s_quitting = true;

        void OnDestroy()
        {
            if (!Application.isPlaying || s_quitting || _shard == null || !gameObject.scene.isLoaded) return; // 에디터 미리보기·씬을 닫을 때는 만들지 않아요
            var origin = transform.position + Vector3.up * 0.3f;
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Jar Shard");
                go.transform.position = origin;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
                go.AddComponent<MeshFilter>().sharedMesh = _shard;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
                var dir = Quaternion.Euler(0f, i * 51f + Random.Range(-20f, 20f), 0f) * Vector3.forward;
                go.AddComponent<InkShardFlight>().Launch(dir * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(2f, 3.5f));
            }
        }
    }

    // 관문 먹 소용돌이: 바닥에 누운 채 천천히 돌아요
    public class InkSpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = -25f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }

    // 사금파리 한 조각: 포물선으로 떨어져 바닥에 멈췄다가 작아지며 사라져요
    public class InkShardFlight : MonoBehaviour
    {
        Vector3 _velocity;
        Vector3 _spin;
        float _restTime = -1f;
        Vector3 _baseScale;

        public void Launch(Vector3 velocity)
        {
            _velocity = velocity;
            _spin = Random.insideUnitSphere * 720f;
            _baseScale = transform.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_restTime < 0f)
            {
                _velocity += Physics.gravity * dt;
                transform.position += _velocity * dt;
                transform.Rotate(_spin * dt, Space.World);
                if (transform.position.y <= 0.03f)
                {
                    transform.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
                    _restTime = Time.time;
                }
                return;
            }
            float t = (Time.time - _restTime - 1.2f) / 0.6f; // 1.2초 놓였다가 0.6초에 걸쳐 사라짐
            if (t <= 0f) return;
            if (t >= 1f) { Destroy(gameObject); return; }
            transform.localScale = _baseScale * (1f - t);
        }
    }
}
