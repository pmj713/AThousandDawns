using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 분열 정예가 쓰러져 둘로 갈라지는 순간 (EnemyBrain 분열 신호): 금 간 보랏빛 먹 원(분열 표식)이 두 쪽으로 쪼개져
    // 갈라진 두 적 쪽으로 날아가며 흩어지고, 그 자리엔 보랏빛·검은 먹이 양쪽으로 터져요.
    // 그림: Resources/EliteFx (Split · SplitShard). 사용: InkSplitBurst.Play(쓰러진 자리, 한쪽 방향, 다른 쪽 방향)
    public class InkSplitBurst : MonoBehaviour
    {
        const float Life = 0.55f;
        static Sprite _ring, _shard, _left, _right;
        static bool _loaded;

        Transform _a, _b, _splash;
        SpriteRenderer _ra, _rb, _rs;
        Vector3 _from, _dirA, _dirB, _unitA, _unitB, _unitS;
        float _t;

        // 신호: EnemyBrain.SplitApart (쓰러진 정예, 쓰러진 자리, 갈라진 둘)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.SplitApart -= OnSplit;
            EnemyBrain.SplitApart += OnSplit;
        }

        static void OnSplit(EnemyBrain elite, Vector3 at, EnemyBrain a, EnemyBrain b)
        {
            Vector3 side = elite != null ? elite.transform.right : Vector3.right; // 갈라진 둘은 좌우로 벌어져 서요
            Vector3 dirA = a != null ? a.transform.position - at : -side;
            Vector3 dirB = b != null ? b.transform.position - at : side;
            Play(at, dirA, dirB);
        }

        public static void Play(Vector3 at, Vector3 dirA, Vector3 dirB)
        {
            Load();
            dirA.y = 0f; dirB.y = 0f;
            if (dirA.sqrMagnitude < 1e-4f) dirA = Vector3.left;
            if (dirB.sqrMagnitude < 1e-4f) dirB = -dirA;
            var go = new GameObject("Ink Split Burst");
            go.AddComponent<InkSplitBurst>().Begin(at, dirA.normalized, dirB.normalized);
        }

        void Begin(Vector3 at, Vector3 dirA, Vector3 dirB)
        {
            _from = at + Vector3.up * 1.1f;
            _dirA = dirA;
            _dirB = dirB;
            // 두 쪽: 화면에서 왼쪽 반은 화면 왼쪽으로 가는 적 쪽으로
            var cam = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            if (Vector3.Dot(_dirA, cam) > Vector3.Dot(_dirB, cam)) (_dirA, _dirB) = (_dirB, _dirA);
            _a = Half(_left, out _ra, out _unitA, 1.3f);
            _b = Half(_right, out _rb, out _unitB, 1.3f);
            _splash = Half(_shard, out _rs, out _unitS, 2.2f);
            if (_splash != null) _splash.position = _from;
        }

        Transform Half(Sprite sprite, out SpriteRenderer sr, out Vector3 unit, float width)
        {
            sr = null;
            unit = Vector3.one;
            if (sprite == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = _from;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            unit = Vector3.one * (width / Mathf.Max(0.01f, _ring != null ? _ring.bounds.size.x : sprite.bounds.size.x));
            if (sprite == _shard) unit = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            go.transform.localScale = unit;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 8;
            return go.transform;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Life), ease = 1f - (1f - k) * (1f - k);
            Fly(_a, _ra, _dirA, ease, k, -1f);
            Fly(_b, _rb, _dirB, ease, k, 1f);
            if (_splash != null)
            {
                _splash.localScale = _unitS * Mathf.Lerp(0.5f, 1.2f, ease);
                _rs.color = new Color(1f, 1f, 1f, 0.9f * (1f - k));
            }
            if (k >= 1f) Destroy(gameObject);
        }

        void Fly(Transform half, SpriteRenderer sr, Vector3 dir, float ease, float k, float spin)
        {
            if (half == null) return;
            half.parent.position = _from + dir * 1.6f * ease + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.35f;
            half.localRotation = Quaternion.Euler(0f, 0f, spin * 50f * ease);
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _ring = Resources.Load<Sprite>("EliteFx/Split");
            _shard = Resources.Load<Sprite>("EliteFx/SplitShard");
            if (_ring == null) return;
            // 금 간 원을 가운데에서 둘로 잘라요 (텍스처는 그대로, 사각만 반씩)
            var r = _ring.rect;
            float ppu = _ring.pixelsPerUnit;
            _left = Sprite.Create(_ring.texture, new Rect(r.x, r.y, r.width * 0.5f, r.height), new Vector2(1f, 0.5f), ppu);
            _right = Sprite.Create(_ring.texture, new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height), new Vector2(0f, 0.5f), ppu);
            _left.name = "SplitLeft";
            _right.name = "SplitRight";
        }
    }
}
