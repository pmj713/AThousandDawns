using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 마검사 천마 페이즈 모습 (모델에 붙는 것만 — 패턴 이펙트는 아트 2의 InkHeavenlyDemonFx):
    //   2페이즈 마법: 몸 둘레 가슴 높이로 보라·청색 룬 여섯이 천천히 돌고, 두 손에 마력 빛이 맺혀 숨 쉬어요.
    //   3페이즈 마검 융합: 위에 더해 검날을 따라 보라 마력과 주홍 불길이 번갈아 일렁이고, 룬이 주홍빛을 섞으며 빨라져요.
    // 몸 가장자리 기운·순간이동 흐려짐은 InkEnemyAnimator가 해요. 그림: FantasyFx/Ward (룬·마력 빛).
    // 씬 빌더(PrototypeSceneBuilder.InkEnemy.cs)가 천마 프리팹에 붙여요
    public class InkDemonPhaseFx : MonoBehaviour
    {
        const int RuneCount = 6;
        const int FlameCount = 4;
        static readonly Color Violet = new Color(0.62f, 0.32f, 1f), Blue = new Color(0.35f, 0.6f, 1f), Flame = new Color(1f, 0.42f, 0.12f);

        HeavenlyDemonBrain _brain;
        Transform _leftHand, _rightHand;
        Renderer _sword;
        SpriteRenderer[] _runes, _hands, _flames;
        Transform _cam;
        int _shownPhase = -1;

        void Start()
        {
            _brain = GetComponentInParent<HeavenlyDemonBrain>();
            var sprite = Resources.Load<Sprite>("FantasyFx/Ward");
            if (_brain == null || sprite == null) { enabled = false; return; }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeftHand") _leftHand = t;
                else if (t.name == "RightHand") _rightHand = t;
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("Weapon")) { _sword = r; break; }
            _runes = Make(sprite, RuneCount, "Rune", 0.32f);
            _hands = Make(sprite, 2, "Hand Mana", 0.28f);
            _flames = Make(sprite, FlameCount, "Blade Fire", 0.3f);
            Show(1);
        }

        SpriteRenderer[] Make(Sprite sprite, int count, string name, float size)
        {
            var list = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"{name} {i + 1}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * size;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 5;
                list[i] = sr;
            }
            return list;
        }

        void Show(int phase)
        {
            _shownPhase = phase;
            foreach (var r in _runes) r.enabled = phase >= 2;
            foreach (var r in _hands) r.enabled = phase >= 2 && _leftHand != null;
            foreach (var r in _flames) r.enabled = phase >= 3 && _sword != null;
        }

        void LateUpdate()
        {
            int phase = _brain != null && !_brain.IsDead ? _brain.Phase : 0;
            if (phase != _shownPhase) Show(phase);
            if (phase < 2) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            var face = _cam != null ? _cam.rotation : Quaternion.identity;
            float t = Time.time;
            bool fused = phase >= 3;

            // 룬: 가슴 높이 둘레를 돌아요 (3페이즈엔 빨라지고 주홍빛이 섞여요)
            var center = transform.position + Vector3.up * 1.4f;
            float spin = t * (fused ? 1.4f : 0.7f);
            for (int i = 0; i < _runes.Length; i++)
            {
                float a = spin + i * Mathf.PI * 2f / _runes.Length;
                var rt = _runes[i].transform;
                rt.position = center + new Vector3(Mathf.Cos(a) * 1.05f, 0.25f * Mathf.Sin(t * 1.3f + i), Mathf.Sin(a) * 1.05f);
                rt.rotation = face;
                var c = Color.Lerp(i % 2 == 0 ? Violet : Blue, Flame, fused ? 0.35f + 0.35f * Mathf.Sin(t * 3f + i) : 0f);
                c.a = 0.75f + 0.25f * Mathf.Sin(t * 2f + i);
                _runes[i].color = c;
            }

            // 두 손의 마력 빛
            for (int i = 0; i < _hands.Length; i++)
            {
                var hand = i == 0 ? _leftHand : _rightHand;
                if (hand == null) continue;
                var ht = _hands[i].transform;
                ht.position = hand.position;
                ht.rotation = face * Quaternion.Euler(0f, 0f, t * 90f * (i == 0 ? 1f : -1f));
                ht.localScale = Vector3.one * (0.24f + 0.05f * Mathf.Sin(t * 5f + i));
                var c = Color.Lerp(Blue, Violet, 0.5f + 0.5f * Mathf.Sin(t * 2.5f));
                c.a = 0.85f;
                _hands[i].color = c;
            }

            // 3페이즈: 검날을 따라 보라 마력과 주홍 불길 (손에서 검 끝까지)
            if (!fused || _sword == null || _rightHand == null) return;
            var b = _sword.bounds;
            var grip = _rightHand.position;
            var tip = b.center + (b.center - grip);   // 검은 손에서 뻗어 있어 경계 상자 가운데의 반대편 끝이 검 끝이에요
            for (int i = 0; i < _flames.Length; i++)
            {
                float k = (i + 1f) / (_flames.Length + 0.5f);
                var ft = _flames[i].transform;
                ft.position = Vector3.Lerp(grip, tip, k) + Vector3.up * 0.06f * Mathf.Sin(t * 9f + i * 2f);
                ft.rotation = face * Quaternion.Euler(0f, 0f, t * 140f + i * 50f);
                ft.localScale = Vector3.one * (0.26f + 0.08f * Mathf.Sin(t * 11f + i));
                var c = Color.Lerp(Violet, Flame, 0.5f + 0.5f * Mathf.Sin(t * 6f + i * 1.7f));
                c.a = 0.9f;
                _flames[i].color = c;
            }
        }
    }
}
