using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 허리춤 주머니: 회귀록 "주머니" 봉우리를 피운 판(PlayerItems.HasBag)이면 플레이어 왼쪽 허리에 삼베 주머니가 달려요.
    // 칸 수(BagCapacity 4·6·8)만큼 조금씩 커지고, 넣은 기물(BagCount)이 많을수록 불룩해져요. 걷고 달리면 허리 움직임에 끌려 흔들려요 (진자).
    // 뼈 축에 기대지 않게 매 프레임 골반 위치·모델 방향으로 자리를 잡아요. 충돌체 없음.
    // 값은 PlayerItems.HasBag · BagCapacity · BagCount를 매 프레임 읽어요
    [RequireComponent(typeof(PlayerController))]
    public class InkPlayerBag : MonoBehaviour
    {
        [SerializeField] Mesh pouchMesh;     // 주머니 몸통 (지름 약 0.68, 높이 0.72, 밑이 원점)
        [SerializeField] Material cloth;
        [SerializeField] Mesh cordMesh;      // 목을 졸라맨 끈
        [SerializeField] Material cord;
        [SerializeField] float baseScale = 0.3f;
        [SerializeField] Vector3 hipOffset = new Vector3(-0.17f, -0.03f, 0.05f);   // 모델 기준 (왼쪽 허리, 살짝 앞)

        PlayerController _player;
        Animator _animator;
        Transform _hips;
        SkinnedMeshRenderer _skin;
        Transform _pivot, _body;
        Vector3 _lastPos, _lastVel;
        Vector2 _swing, _swingVel;   // (앞뒤, 옆) 기울기(도)와 속도
        float _fill;

        public void Setup(Mesh pouch, Material clothMat, Mesh cordM, Material cordMat)
        {
            pouchMesh = pouch; cloth = clothMat; cordMesh = cordM; cord = cordMat;
        }

        void Awake() => _player = GetComponent<PlayerController>();

        void Build()
        {
            _pivot = new GameObject("Ink Bag (허리춤 주머니)").transform;
            _body = new GameObject("Pouch").transform;
            _body.SetParent(_pivot, false);
            // 주머니는 끈에 졸린 목(높이 0.5)이 허리띠에 걸리게 아래로 내려 달아요
            var mesh = new GameObject("Body");
            mesh.transform.SetParent(_body, false);
            mesh.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            mesh.AddComponent<MeshFilter>().sharedMesh = pouchMesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = cloth;
            if (cordMesh != null)
            {
                var c = new GameObject("Cord");
                c.transform.SetParent(mesh.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = cordMesh;
                c.AddComponent<MeshRenderer>().sharedMaterial = cord;
            }
        }

        void LateUpdate()
        {
            var items = _player != null ? _player.Items : null;
            bool has = items != null && items.HasBag;
            if (!has || pouchMesh == null)
            {
                if (_pivot != null) _pivot.gameObject.SetActive(false);
                return;
            }
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_hips == null && _animator != null && _animator.isHuman) _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (_hips == null) return;
            if (_skin == null) _skin = _animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (_pivot == null) { Build(); _lastPos = _hips.position; }
            // 모델이 숨으면(비도 순간이동 등) 주머니도 숨어요
            bool shown = _skin == null || (_skin.enabled && _skin.gameObject.activeInHierarchy);
            _pivot.gameObject.SetActive(shown);
            if (!shown) return;

            // 크기: 칸 4 → 1배, 6 → 1.12배, 8 → 1.24배. 불룩함은 넣은 비율만큼 (천천히 따라가요)
            int capacity = items.BagCapacity;   // −1은 제한 없음(샌드박스): 기본 크기
            int count = items.BagCount;
            float size = baseScale * (1f + Mathf.Max(0, capacity - 4) * 0.06f);
            float target = capacity > 0 ? Mathf.Clamp01((float)count / capacity) : 0f;
            _fill = Mathf.MoveTowards(_fill, target, Time.deltaTime * 2f);
            _body.localScale = new Vector3(size * (0.8f + 0.4f * _fill), size * (0.92f + 0.12f * _fill), size * (0.75f + 0.45f * _fill));

            // 자리: 골반 위치 + 모델 방향 기준 왼쪽 허리
            var frame = _animator.transform;
            _pivot.SetPositionAndRotation(_hips.position + frame.rotation * hipOffset, frame.rotation);

            // 흔들림: 허리가 가속하는 반대쪽으로 처지는 진자 + 걸음마다 통통 튀는 흔들림
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var vel = (_hips.position - _lastPos) / dt;
            var acc = (vel - _lastVel) / dt;
            _lastPos = _hips.position; _lastVel = vel;
            var localAcc = Quaternion.Inverse(frame.rotation) * acc;
            float speed = new Vector2(vel.x, vel.z).magnitude;
            // X축 +는 밑이 뒤(-Z)로, Z축 +는 밑이 +X로 가요
            var push = new Vector2(Mathf.Clamp(localAcc.z * 1.2f, -35f, 35f), Mathf.Clamp(-localAcc.x * 1.2f, -30f, 30f));
            push.x += Mathf.Clamp(speed * 4f, 0f, 18f);                   // 달리면 뒤로 끌려요
            push.y += Mathf.Clamp(-localAcc.y * 0.6f, -15f, 15f);         // 위아래 걸음에 옆으로 튀어요
            var force = (push - _swing) * 90f - _swingVel * 9f;           // 스프링·댐퍼
            _swingVel += force * dt;
            _swing += _swingVel * dt;
            _body.localRotation = Quaternion.Euler(_swing.x, 0f, _swing.y);
        }

        void OnDisable()
        {
            if (_pivot != null) _pivot.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_pivot != null) Destroy(_pivot.gameObject);
        }
    }
}
