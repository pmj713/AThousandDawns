using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 차원 균열이 판타지 적에 반응해요: 방에 판타지 적(슬라임·고블린…)이 살아 있으면 벽 너머 균열이 넓어지고 밝게 타올라요.
    // 균열 재질(InkRift)의 _Boost를 0 ↔ 1로 천천히 옮겨요. 에셋을 건드리지 않게 처음에 재질 복사본을 만들어 균열 렌더러들에 끼워요
    // (정적 배칭된 렌더러도 재질은 바꿀 수 있어요). 적 목록은 EnemyDirector 것을 0.5초마다 훑어요.
    // 전역 셰이더 값으로 주지 않아요: 그러면 다른 셰이더의 전역 값 묶음이 흐트러져 대나무 잎이 파랗게 나왔어요.
    // 씬 빌더(PrototypeSceneBuilder.InkRift.cs)가 균열 묶음에 붙여요
    public class InkRiftGlow : MonoBehaviour
    {
        static readonly int BoostId = Shader.PropertyToID("_Boost");

        EnemyDirector _director;
        Material _material;
        float _scan, _target, _boost;

        void Start()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != "AThousandDawns/InkRift") continue;
                if (_material == null) _material = new Material(m);
                r.sharedMaterial = _material;
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void Update()
        {
            if (_material == null) return;
            _scan -= Time.deltaTime;
            if (_scan <= 0f)
            {
                _scan = 0.5f;
                if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
                _target = _director != null && AnyFantasy(_director) ? 1f : 0f;
            }
            if (Mathf.Approximately(_boost, _target)) return;
            _boost = Mathf.MoveTowards(_boost, _target, Time.deltaTime * (_target > _boost ? 1.5f : 0.4f));   // 빨리 타오르고 천천히 잦아들어요
            _material.SetFloat(BoostId, _boost);
        }

        static bool AnyFantasy(EnemyDirector director)
        {
            foreach (var e in director.Enemies)
                if (e != null && !e.IsDead && IsFantasy(e.Kind)) return true;
            return false;
        }

        static bool IsFantasy(EnemyKind kind) => kind >= EnemyKind.Slime;   // 판타지 적은 EnemyKind 끝에 이어 붙어요 (슬라임부터)
    }
}
