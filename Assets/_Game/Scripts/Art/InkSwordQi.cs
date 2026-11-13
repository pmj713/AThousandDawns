using UnityEngine;

namespace AThousandDawns.Art
{
    // 날아가는 검기 공용 그림 (플레이 테스트: 검기 모양을 붓질 초승달로).
    //   한 획 먹 초승달(SkillFx/SwordQi, 볼록한 등이 그림 위쪽)을 바닥과 나란히 눕혀 등이 날아가는 쪽을 보게 하고,
    //   그 뒤에 조금 큰 같은 획을 주인 색으로 물들여 테두리처럼 깔아요.
    //   쓰는 곳: 탁랑참 파도 검기(플레이어) · 탁랑객 물결 검기 · 천마 십자 검기.
    public sealed class InkSwordQi
    {
        static Sprite _sprite;
        static bool _loaded;

        public static Sprite Sprite
        {
            get
            {
                if (!_loaded) { _loaded = true; _sprite = Resources.Load<Sprite>("SkillFx/SwordQi"); }
                return _sprite;
            }
        }

        readonly SpriteRenderer _core, _rim;
        readonly Vector3 _coreUnit, _rimUnit;
        readonly Color _rimColor;

        public bool Valid => _core != null;

        // 등이 dir을 보게 눕혀요 (그림 위쪽 = 진행 방향)
        public static float Yaw(Vector3 dir) => -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + 90f;

        public InkSwordQi(Transform parent, Vector3 at, Vector3 dir, float width, Color rimColor, float height = 0.9f)
        {
            var sprite = Sprite;
            if (sprite == null) return;
            _rimColor = rimColor;
            _rim = Make(parent, sprite, "Sword Qi Rim", 6);
            _core = Make(parent, sprite, "Sword Qi", 7);
            float unit = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            _coreUnit = Vector3.one * unit;
            _rimUnit = Vector3.one * unit * 1.12f;
            Pose(at, dir, 1f, 1f, height);
        }

        static SpriteRenderer Make(Transform parent, Sprite sprite, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Pose(Vector3 at, Vector3 dir, float scale, float alpha, float height = 0.9f)
        {
            if (_core == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            var rot = Quaternion.Euler(90f, Yaw(dir.normalized), 0f);
            var p = new Vector3(at.x, height, at.z);
            _core.transform.SetPositionAndRotation(p, rot);
            _rim.transform.SetPositionAndRotation(p - dir.normalized * 0.06f + Vector3.down * 0.02f, rot); // 테두리는 살짝 뒤·아래
            _core.transform.localScale = _coreUnit * scale;
            _rim.transform.localScale = _rimUnit * scale;
            _core.color = new Color(1f, 1f, 1f, alpha);
            _rim.color = new Color(_rimColor.r, _rimColor.g, _rimColor.b, _rimColor.a * alpha);
        }

        public void Destroy()
        {
            if (_core != null) Object.Destroy(_core.gameObject);
            if (_rim != null) Object.Destroy(_rim.gameObject);
        }
    }
}
