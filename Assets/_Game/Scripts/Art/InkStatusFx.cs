using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 상태이상을 적 모델에 직접 (플레이 테스트: 머리 위 점 대신).
    //   화상 (BurnStacks): 몸 둘레 여기저기서 불꽃이 솟고 그을음 연기가 올라요. 중첩이 많을수록 불꽃·연기가 짙어져요 (최대 중첩 대비).
    //   둔화 (SlowStacks): 몸에서 옅은 서리 김이 조금씩. 빙결 (IsFrozen): 서리 김과 얼음 결정이 짙게 감돌아요.
    //   (얼음빛 몸 색은 모델 재질을 쓰는 메인 아트 InkEnemyAnimator 쪽.)
    // 입자는 적의 자식이라 적이 사라지면 같이 사라져요. 그림: InkParticles (Sprites/Default + FxArt/InkMist).
    public class InkStatusFx : MonoBehaviour
    {
        sealed class State
        {
            public ParticleSystem Fire, Smoke, Frost;
            public float Height;
        }

        readonly Dictionary<EnemyBrain, State> _states = new Dictionary<EnemyBrain, State>();
        readonly List<EnemyBrain> _gone = new List<EnemyBrain>();
        EnemyDirector _director;
        float _find;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var go = new GameObject("Ink Status Fx");
            DontDestroyOnLoad(go);
            go.AddComponent<InkStatusFx>();
        }

        void Update()
        {
            if (_director == null)
            {
                _find -= Time.deltaTime;
                if (_find > 0f) return;
                _find = 1f;
                _director = FindAnyObjectByType<EnemyDirector>();
                if (_director == null) return;
            }
            var list = _director.Enemies;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                var s = e.Statuses;
                if (s == null) continue;
                int burn = e.IsDead ? 0 : s.BurnStacks;
                bool frozen = !e.IsDead && s.IsFrozen;
                int slow = e.IsDead ? 0 : s.SlowStacks;
                _states.TryGetValue(e, out var st);
                if (st == null && burn == 0 && !frozen && slow == 0) continue; // 아무 것도 안 걸렸으면 만들지 않아요
                if (st == null) { st = Build(e); _states[e] = st; }
                float k = burn > 0 ? Mathf.Clamp01(burn / (float)Mathf.Max(1, s.BurnMax)) : 0f;
                InkParticles.SetRate(st.Fire, burn > 0 ? Mathf.Lerp(8f, 40f, k) : 0f);
                InkParticles.SetRate(st.Smoke, burn > 0 ? Mathf.Lerp(2f, 10f, k) : 0f);
                InkParticles.SetRate(st.Frost, frozen ? 36f : slow * 3f);
            }
            _gone.Clear();
            foreach (var pair in _states) if (pair.Key == null) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++) _states.Remove(_gone[i]);
        }

        static State Build(EnemyBrain e)
        {
            var st = new State { Height = Mathf.Max(1f, e.Height) };
            var root = e.transform;
            float h = st.Height;
            st.Fire = InkParticles.Fire(root, Vector3.up * h * 0.3f, h * 0.3f, 0f);
            st.Smoke = InkParticles.Smoke(root, Vector3.up * h * 0.6f, h * 0.25f, 0f, 1f);
            st.Frost = InkParticles.Frost(root, Vector3.up * h * 0.4f, h * 0.25f, 0f);
            float w = Mathf.Clamp(h * 0.45f, 0.5f, 1.8f); // 몸 크기 상자 안 여기저기서 솟아요
            InkParticles.EmitBox(st.Fire, Vector3.up * h * 0.45f, new Vector3(w, h * 0.8f, w), 1.6f);
            InkParticles.EmitBox(st.Smoke, Vector3.up * h * 0.7f, new Vector3(w, h * 0.4f, w), 1f);
            InkParticles.EmitBox(st.Frost, Vector3.up * h * 0.5f, new Vector3(w * 1.1f, h, w * 1.1f), 0.2f); // 서리는 거의 제자리에서 맴돌아요
            return st;
        }

    }
}
