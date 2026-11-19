using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 종류 없는 SpawnBurst (BurstKind.Default)를 쓰던 일반 적 여섯이 같은 주황 먹 폭발을 돌려썼어요 (일반 · 엘리트 겹침 점검, 사용자 승인).
    //   폭발 자리 가까이 있는 그 적 종류에 따라 각자 그림으로 그려요 (InkProjectiles.SpawnBurst가 먼저 물어봐요). 없으면 예전 먹 폭발 그대로.
    //   목탁승 가호: 금빛 목탁 울림 — 금빛 고리 셋이 차례로 퍼지고 금빛 티끌이 떠올라요
    //   깃발수 지휘: 붉은 깃발 바람 — 붉은 고리 + 붉은 천 조각이 휘날리고 바람 줄기가 바깥으로
    //   쇠닻 휘두르기: 물 튀김 — 물빛 잔물결 고리 + 물보라가 솟았다 떨어져요
    //   천마 근위 반격: 검붉은 충격 — 검은 고리 + 진홍 불티 + 검은 연기
    //   혈인 되살리기: 핏빛 안개 — 검붉은 안개가 솟고 핏방울이 튀어요
    //   고블린 주술사 저주 장판 착지: 연두 · 검보라 포자 — 연두 고리 + 포자가 피어올라요
    // 고리 크기: PouchFx/Ring의 가장 진한 선은 그림 폭의 0.34배 반지름 → 폭 = 반경 × 2.9면 폭발 반경에 와요.
    // 그림: PouchFx/Ring, RiverFx/Ripple, InkParticles.
    public static class InkKindBursts
    {
        static readonly EnemyKind[] Kinds = { EnemyKind.Moktak, EnemyKind.FlagBearer, EnemyKind.Anchor, EnemyKind.DemonGuard, EnemyKind.BloodShaman, EnemyKind.GoblinShaman };
        static Sprite _ring, _ripple;
        static bool _loaded;

        // 그 자리 가까이 (반경 + 2.5칸, 혈인은 시체 자리라 9칸) 여섯 종류 중 가장 가까운 적이 있으면 그 그림으로 그리고 true
        public static bool TryPlay(Vector3 at, float radius)
        {
            at.y = 0f;
            EnemyBrain best = null;
            float bestD = float.MaxValue;
            foreach (var e in Object.FindObjectsByType<EnemyBrain>()) // 폭발 순간에만 한 번 훑어요
            {
                if (e == null || System.Array.IndexOf(Kinds, e.Kind) < 0) continue;
                float within = e.Kind == EnemyKind.BloodShaman ? 9f : radius + 2.5f;
                var d = e.transform.position - at;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < within * within && sq < bestD) { bestD = sq; best = e; }
            }
            if (best == null) return false;
            Load();
            switch (best.Kind)
            {
                case EnemyKind.Moktak:
                    for (int i = 0; i < 3; i++) Pulse(_ring, at, radius * 2.9f, 0.55f, new Color(1f, 0.85f, 0.4f, 0.9f), i * 0.12f);
                    InkParticles.Burst(at + Vector3.up * 0.3f, 18, new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.75f, 0.25f, 0f), radius * 0.8f, 0.14f, 0.9f, -1.2f);
                    break;
                case EnemyKind.FlagBearer:
                    Pulse(_ring, at, radius * 2.9f, 0.45f, new Color(0.95f, 0.25f, 0.18f, 0.85f), 0f);
                    InkParticles.Burst(at + Vector3.up * 1f, 16, new Color(0.9f, 0.15f, 0.12f, 1f), new Color(0.6f, 0.08f, 0.06f, 0f), radius * 2.2f, 0.22f, 0.9f, 0.6f, false); // 천 조각
                    InkParticles.Burst(at + Vector3.up * 0.4f, 14, new Color(1f, 0.85f, 0.75f, 0.6f), new Color(1f, 0.6f, 0.5f, 0f), radius * 3f, 0.5f, 0.5f, 0f); // 바람 줄기
                    break;
                case EnemyKind.Anchor:
                    if (_ripple != null) for (int i = 0; i < 2; i++) Pulse(_ripple, at, radius * 2.4f, 0.6f, new Color(0.55f, 0.8f, 1f, 0.85f), i * 0.1f);
                    InkParticles.Burst(at + Vector3.up * 0.2f, 22, new Color(0.9f, 0.97f, 1f, 1f), new Color(0.35f, 0.6f, 0.95f, 0.3f), radius * 2f, 0.2f, 0.7f, 2.5f);
                    break;
                case EnemyKind.DemonGuard:
                    Pulse(_ring, at, radius * 2.9f, 0.4f, new Color(0.12f, 0.02f, 0.04f, 0.9f), 0f);
                    Pulse(_ring, at, radius * 2.1f, 0.3f, new Color(0.85f, 0.1f, 0.12f, 0.9f), 0.04f);
                    InkParticles.Burst(at + Vector3.up * 0.8f, 16, new Color(1f, 0.35f, 0.3f, 1f), new Color(0.5f, 0.02f, 0.05f, 0f), radius * 2.4f, 0.1f, 0.4f, 0.8f);
                    InkParticles.Burst(at + Vector3.up * 0.5f, 8, new Color(0.1f, 0.05f, 0.06f, 0.8f), new Color(0.2f, 0.05f, 0.05f, 0f), radius * 0.8f, 0.6f, 0.8f, -0.3f);
                    break;
                case EnemyKind.BloodShaman:
                    InkParticles.Burst(at + Vector3.up * 0.3f, 18, new Color(0.45f, 0.04f, 0.06f, 0.85f), new Color(0.2f, 0.02f, 0.03f, 0f), radius * 1f, 0.7f, 1.1f, -0.6f);
                    InkParticles.Burst(at + Vector3.up * 0.6f, 12, new Color(0.8f, 0.08f, 0.08f, 1f), new Color(0.4f, 0.02f, 0.03f, 0f), radius * 2f, 0.12f, 0.6f, 2f);
                    Pulse(_ring, at, radius * 2.9f, 0.5f, new Color(0.55f, 0.05f, 0.08f, 0.75f), 0f);
                    break;
                case EnemyKind.GoblinShaman:
                    Pulse(_ring, at, radius * 2.9f, 0.5f, new Color(0.6f, 0.95f, 0.3f, 0.85f), 0f);
                    InkParticles.Burst(at + Vector3.up * 0.2f, 20, new Color(0.7f, 1f, 0.35f, 1f), new Color(0.3f, 0.1f, 0.4f, 0f), radius * 1.2f, 0.25f, 1f, -0.8f); // 포자
                    break;
            }
            return true;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
        }

        // 바닥에 누운 고리가 퍼지며 옅어져요 (delay초 뒤 시작)
        static void Pulse(Sprite sprite, Vector3 at, float size, float life, Color color, float delay)
        {
            if (sprite == null) return;
            var go = new GameObject("Kind Burst Ring");
            go.transform.position = new Vector3(at.x, 0.06f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 6;
            sr.color = new Color(color.r, color.g, color.b, 0f);
            go.AddComponent<InkPulse>().Begin(size / Mathf.Max(0.01f, sprite.bounds.size.x), life, color, delay);
        }
    }

    // 고리 한 번 퍼지기: 0.25배 → 1배로 커지며 옅어지고 끝나면 지워져요
    public class InkPulse : MonoBehaviour
    {
        SpriteRenderer _sr;
        float _unit, _life, _t;
        Color _color;

        public void Begin(float unit, float life, Color color, float delay)
        {
            _sr = GetComponent<SpriteRenderer>();
            _unit = unit;
            _life = Mathf.Max(0.05f, life);
            _color = color;
            _t = -delay;
            transform.localScale = Vector3.one * unit * 0.25f;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < 0f) return;
            float k = _t / _life;
            transform.localScale = Vector3.one * _unit * Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
            if (_sr != null) _sr.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - k * k));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
