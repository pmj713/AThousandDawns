using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 판 도중에 게임 코드가 만드는 소품(항아리 "Jar", 금 간 벽 "HiddenCrack", 기물 주머니)에 수묵 겉모습을 입혀요.
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
        [Header("3D 모델 (PrototypeSceneBuilder.InkRunModels)")]
        [SerializeField] Mesh pouchMesh;
        [SerializeField] Material pouchMaterial;
        [SerializeField] Mesh pouchCordMesh;
        [SerializeField] Material pouchCordMaterial;
        [SerializeField] Mesh crackStonesMesh;
        [SerializeField] Material crackStonesMaterial;
        [SerializeField] Mesh crackDarkMesh;
        [SerializeField] Material crackDarkMaterial;

        const string PouchName = "기물 주머니"; // RoomRun.CreatePouch
        const string AnchorName = "쇠닻";       // AnchorBrain.PlacePost (던져 꽂은 닻, 길을 막는 원기둥)
        [SerializeField] Mesh anchorMesh;
        [SerializeField] Material anchorMaterial;

        const float ScanInterval = 0.1f;   // 새로 생긴 소품이 원래 모양(공·원기둥)으로 보이는 틈을 줄여요
        const string Marker = "Ink Visual";
        float _nextScan;
        readonly List<GameObject> _roots = new List<GameObject>();

        public void Setup(Mesh[] jars, Material jar, Material lid, Mesh shard, Sprite crack, Mesh rubble, Material rubbleMat)
        {
            jarMeshes = jars; jarMaterial = jar; lidMaterial = lid; shardMesh = shard;
            crackSprite = crack; rubbleMesh = rubble; rubbleMaterial = rubbleMat;
        }

        public void SetupAnchor(Mesh anchor, Material iron) { anchorMesh = anchor; anchorMaterial = iron; }

        // 보상 방 보상 소품 (RoomRun.RewardKind 순서, 비어 있는 자리 = 비단 주머니). PrototypeSceneBuilder.InkRewards가 채워요
        [SerializeField] GameObject[] rewardModels = new GameObject[0];
        public void SetupRewards(GameObject[] models) => rewardModels = models ?? new GameObject[0];

        // 놓인 보상 구에 단 소품들: 그 방 보상 종류(RoomRun.CurrentPath.Reward, 입문 층은 기물)에 맞는 것만 켜요
        Transform _pouchVisual;
        GameObject _pouchSilk;
        GameObject[] _rewardViews;
        int _shownReward = -1;
        AThousandDawns.Prototype.RoomRun _run;

        void ShowReward()
        {
            if (_pouchVisual == null || !_pouchVisual.gameObject.activeInHierarchy) return;
            if (_run == null) _run = FindAnyObjectByType<AThousandDawns.Prototype.RoomRun>();
            if (_run == null) return;
            int kind = _run.InTutorialFloor ? (int)AThousandDawns.Prototype.RoomRun.RewardKind.Item : (int)_run.CurrentPath.Reward;
            if (kind == _shownReward) return;
            _shownReward = kind;
            bool own = _rewardViews != null && kind < _rewardViews.Length && _rewardViews[kind] != null;
            if (_pouchSilk != null) _pouchSilk.SetActive(!own);
            if (_rewardViews != null)
                for (int i = 0; i < _rewardViews.Length; i++)
                    if (_rewardViews[i] != null) _rewardViews[i].SetActive(i == kind);
        }

        public void SetupModels(Mesh pouch, Material silk, Mesh cord, Material cordMat, Mesh stones, Material stoneMat, Mesh dark, Material darkMat)
        {
            pouchMesh = pouch; pouchMaterial = silk; pouchCordMesh = cord; pouchCordMaterial = cordMat;
            crackStonesMesh = stones; crackStonesMaterial = stoneMat; crackDarkMesh = dark; crackDarkMaterial = darkMat;
        }

        // 에디터 미리보기용: 지금 바로 입혀요 (아트 → 판 소품 미리보기 저장)
        public void DressNow(GameObject go)
        {
            if (go.name == "Jar") DressJar(go);
            else if (go.name == "HiddenCrack") DressCrack(go);
            else if (go.name == PouchName) DressPouch(go);
            else if (go.name == AnchorName) DressAnchor(go);
        }

        void Update()
        {
            ShowReward();
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);
            // 처음 보는 루트만 이름을 봐요 (name은 읽을 때마다 문자열을 새로 만들어서, 씬 루트 수백 개를 0.1초마다 읽으면 쓰레기가 쌓여요)
            foreach (var go in _roots)
            {
                if (!_seen.Add(go)) continue;
                string n = go.name;
                if (n != "Jar" && n != "HiddenCrack" && n != PouchName && n != AnchorName) continue;
                if (go.transform.Find(Marker) != null) continue;
                if (n == "Jar") DressJar(go);
                else if (n == "HiddenCrack") DressCrack(go);
                else if (n == PouchName) DressPouch(go);
                else DressAnchor(go);
            }
            if (_seen.Count > _roots.Count * 2 + 64) _seen.RemoveWhere(g => g == null);   // 지워진 것은 가끔 비워요
        }

        readonly HashSet<GameObject> _seen = new HashSet<GameObject>();

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

        // 던져 꽂은 쇠닻: 원기둥(지름 0.7, 높이 1.2) 대신 땅에 비스듬히 박힌 닻. 닻가지 하나는 땅에 묻히고 하나는 비죽 솟았고,
        // 위 고리에서 사슬이 늘어져 땅에 끌려요. 둘레에 깨진 흙·돌 부스러기
        void DressAnchor(GameObject post)
        {
            if (anchorMesh == null || anchorMaterial == null) return;
            var r = post.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var t = post.transform;
            var p = t.position;
            var visual = Child(t, Marker);
            visual.position = new Vector3(p.x, p.y - t.lossyScale.y, p.z);   // 원기둥 밑면 = 바닥
            visual.rotation = Quaternion.Euler(0f, (p.x * 61f + p.z * 37f) % 360f, 0f);
            Unscale(visual, t, 1f);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = anchorMesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = anchorMaterial;
            if (rubbleMesh != null && rubbleMaterial != null)
                for (int i = 0; i < 5; i++)
                {
                    var stone = new GameObject("Rubble").transform;
                    stone.SetParent(visual, false);
                    float a = i * 72f * Mathf.Deg2Rad;
                    stone.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.45f + (i % 2) * 0.2f);
                    stone.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
                    stone.localScale = Vector3.one * (0.14f + (i % 3) * 0.06f);
                    stone.gameObject.AddComponent<MeshFilter>().sharedMesh = rubbleMesh;
                    stone.gameObject.AddComponent<MeshRenderer>().sharedMaterial = rubbleMaterial;
                }
        }

        // 기물 주머니: 금색 공 대신 끈 묶인 비단 주머니. 공(0.7×0.6×0.7, 가운데가 높이 0.5)의 밑동에 세워요.
        // 게임이 주머니 크기를 맥동시키니(AnimatePouch) 모델도 같이 숨 쉬어요. 반짝임 불빛은 그대로 둬요
        void DressPouch(GameObject pouch)
        {
            if (pouchMesh == null || pouchMaterial == null) return;
            var r = pouch.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var t = pouch.transform;
            var visual = Child(t, Marker);
            visual.localPosition = new Vector3(0f, -0.5f, 0f);   // 공 밑면 (지름 1 기준)
            visual.localRotation = Quaternion.Euler(0f, 20f, 0f); // 매듭(-z)이 카메라(남쪽) 쪽으로, 살짝 비껴
            Unscale(visual, t, 1f);
            var silk = Child(visual, "Silk Pouch");   // 기물 (그리고 소품이 없는 보상)
            silk.gameObject.AddComponent<MeshFilter>().sharedMesh = pouchMesh;
            silk.gameObject.AddComponent<MeshRenderer>().sharedMaterial = pouchMaterial;
            if (pouchCordMesh != null && pouchCordMaterial != null)
            {
                var cord = Child(silk, "Cord");
                cord.gameObject.AddComponent<MeshFilter>().sharedMesh = pouchCordMesh;
                cord.gameObject.AddComponent<MeshRenderer>().sharedMaterial = pouchCordMaterial;
            }
            // 보상 종류마다 다른 소품 (같은 자리에 모두 달고 ShowReward가 하나만 켜요)
            _rewardViews = new GameObject[rewardModels.Length];
            for (int i = 0; i < rewardModels.Length; i++)
            {
                if (rewardModels[i] == null) continue;
                var view = Instantiate(rewardModels[i], visual, false);
                view.SetActive(false);
                _rewardViews[i] = view;
            }
            _pouchVisual = visual;
            _pouchSilk = silk.gameObject;
            _shownReward = -1;
            ShowReward();
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
            // 카메라는 남쪽 위에서 북쪽을 내려다봐요. 남쪽 담의 안쪽 면은 카메라를 등지고, 담(1.5) 바로 뒤 바닥 약 1m도 가려져요.
            // 그래서 남쪽 담이면 금을 바깥 면(카메라 쪽)에도 그리고, 부스러기는 가려지는 곳 너머에 둬요.
            // 어느 담이든 담 밑에서 방 쪽 바닥으로 금이 이어져 어디서 봐도 보여요.
            bool south = t.forward.z < -0.5f;
            // 금 간 돌벽 조각: 돌들이 금 양옆으로 밀려 나오고 틈 사이로 먹빛 속이 보여요 (남쪽 담은 바깥 면에도)
            if (crackStonesMesh != null && crackStonesMaterial != null)
            {
                StonePatch(visual, 0f, false);
                if (south) StonePatch(visual, WallThickness + 0.04f, true);
            }
            if (crackSprite != null)
            {
                if (crackStonesMesh == null) WallCard(visual, 0f);   // 돌 조각이 있으면 벽 면 금 그림은 그 아래 가려져요
                if (south && crackStonesMesh == null) WallCard(visual, WallThickness + 0.04f);
                // 바닥으로 이어진 금: 담 밑동에서 방 쪽으로 눕혀요
                var floor = new GameObject("Crack_Floor").transform;
                floor.SetParent(visual, false);
                floor.localPosition = new Vector3(0f, -1.1f + 0.02f, south ? -0.9f : 0f); // 남쪽은 담·기와에 가리는 곳 너머에서 시작
                floor.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 스프라이트 위쪽이 방 안쪽(-forward)으로
                float kf = 1.6f / crackSprite.bounds.size.y;
                floor.localScale = new Vector3(kf * 0.8f, kf, kf);
                floor.gameObject.AddComponent<SpriteRenderer>().sprite = crackSprite;
            }
            float rubbleOut = south ? 1.15f : 0.25f;
            if (rubbleMesh != null && rubbleMaterial != null)
                for (int i = 0; i < 4; i++)
                {
                    var stone = new GameObject("Rubble").transform;
                    stone.SetParent(visual, false);
                    stone.localPosition = new Vector3((i - 1.5f) * 0.28f, -1.1f, -rubbleOut - (i % 2) * 0.2f);
                    stone.localRotation = Quaternion.Euler(0f, i * 77f, 0f);
                    stone.localScale = Vector3.one * (0.18f + (i % 3) * 0.07f);
                    stone.gameObject.AddComponent<MeshFilter>().sharedMesh = rubbleMesh;
                    stone.gameObject.AddComponent<MeshRenderer>().sharedMaterial = rubbleMaterial;
                }
        }

        const float WallThickness = 0.5f; // PrototypeSceneBuilder.BuildShell의 담 두께
        const float WallHeight = 1.5f;

        // 벽 면에 붙인 금 간 돌 조각. depth = 안쪽 면에서 담 속으로 들어간 거리, outer면 바깥 면이라 뒤집어 붙여요
        void StonePatch(Transform visual, float depth, bool outer)
        {
            var patch = Child(visual, outer ? "Crack_Stones_Outer" : "Crack_Stones");
            patch.localPosition = new Vector3(0f, 0f, depth);
            patch.localRotation = outer ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            patch.gameObject.AddComponent<MeshFilter>().sharedMesh = crackStonesMesh;
            patch.gameObject.AddComponent<MeshRenderer>().sharedMaterial = crackStonesMaterial;
            if (crackDarkMesh != null && crackDarkMaterial != null)
            {
                var dark = Child(patch, "Crack_Dark");
                dark.gameObject.AddComponent<MeshFilter>().sharedMesh = crackDarkMesh;
                dark.gameObject.AddComponent<MeshRenderer>().sharedMaterial = crackDarkMaterial;
            }
        }

        // 담 면에 세운 금 그림. depth = 안쪽 면에서 담 속으로 들어간 거리 (두께만큼이면 바깥 면)
        void WallCard(Transform visual, float depth)
        {
            var card = new GameObject(depth > 0f ? "Crack_Outer" : "Crack").transform;
            card.SetParent(visual, false);
            card.localPosition = new Vector3(0f, -1.1f + 0.05f, depth); // 금 그림 밑동 = 바닥 (crack 오브젝트는 바닥 위 1.1)
            float k = (WallHeight - 0.15f) / crackSprite.bounds.size.y; // 담 높이 안에서 (기와까지 올라가지 않게)
            card.localScale = new Vector3(k, k, k);
            card.gameObject.AddComponent<SpriteRenderer>().sprite = crackSprite;
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

    // 항아리가 깨지면(부모가 Destroy) 사금파리 몇 조각이 튀었다가 바닥에서 옅어져요.
    // 다른 방으로 넘어갈 때 게임이 남은 항아리를 치우는 것도 Destroy라서, 꾸밀 때의 방(RoomRun.Room)을 기억해 두고
    // 사라질 때 방이 바뀌었으면 치운 것이라 조용히 사라져요 (깨기는 평타·부딪힘 어느 쪽이든 같은 방에서 일어나요)
    public class InkJarShatter : MonoBehaviour
    {
        Mesh _shard;
        Material _material;
        static bool s_quitting;
        AThousandDawns.Prototype.RoomRun _run;
        object _room;

        public void Setup(Mesh shard, Material material)
        {
            _shard = shard;
            _material = material;
            _run = FindFirstObjectByType<AThousandDawns.Prototype.RoomRun>();
            _room = _run != null ? _run.Room : null;
        }

        void OnApplicationQuit() => s_quitting = true;

        void OnDestroy()
        {
            if (_run != null && !ReferenceEquals(_run.Room, _room)) return;   // 방을 옮길 때 치워진 항아리
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
