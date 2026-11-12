using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 엄폐물 너머 우회 (EnemyBrain.IsFlanking): 플레이어를 못 보고 돌아 들어오는 적의 발밑에 작은 먹 먼지가 튀어요.
    // 그림: BloodBuddhaFx/Splash (먹빛으로 물들여요).
    public class InkFlankDust : MonoBehaviour
    {
        static Sprite _puff;

        readonly Dictionary<EnemyBrain, (Vector3 last, float timer)> _seen = new Dictionary<EnemyBrain, (Vector3, float)>();
        readonly List<(SpriteRenderer sr, float t, Vector3 unit)> _puffs = new List<(SpriteRenderer, float, Vector3)>();
        readonly List<EnemyBrain> _gone = new List<EnemyBrain>();
        float _scan;
        EnemyBrain[] _enemies = new EnemyBrain[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            _puff = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            if (_puff == null) return;
            var go = new GameObject("Ink Flank Dust");
            DontDestroyOnLoad(go);
            go.AddComponent<InkFlankDust>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.5f; _enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None); }
            _gone.Clear();
            foreach (var e in _enemies)
            {
                if (e == null || e.IsDead || e.IsHidden) continue;
                var p = e.transform.position;
                var (last, timer) = _seen.TryGetValue(e, out var s) ? s : (p, 0f);
                timer -= dt;
                bool moving = (p - last).sqrMagnitude > 0.0004f;
                if (timer <= 0f && moving && e.IsFlanking)
                {
                    timer = 0.22f;
                    Puff(p - (p - last).normalized * 0.25f);
                }
                _seen[e] = (p, timer);
            }
            foreach (var key in _seen.Keys) if (key == null) _gone.Add(key);
            foreach (var key in _gone) _seen.Remove(key);

            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var (sr, t, unit) = _puffs[i];
                if (sr == null) { _puffs.RemoveAt(i); continue; }
                t += dt;
                float k = t / 0.45f;
                sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(0.35f, 0.32f, 0.28f, 0.55f * (1f - k));
                if (k >= 1f) { Destroy(sr.gameObject); _puffs.RemoveAt(i); continue; }
                _puffs[i] = (sr, t, unit);
            }
        }

        void Puff(Vector3 at)
        {
            var go = new GameObject("Flank Dust");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.026f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            var unit = Vector3.one * (0.55f / Mathf.Max(0.01f, _puff.bounds.size.x));
            go.transform.localScale = unit;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _puff;
            sr.sortingOrder = 4;
            _puffs.Add((sr, 0f, unit));
        }
    }
}
