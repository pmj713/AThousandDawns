using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 적은 게임이 실행 중에 만들어요(EnemyFactory). 새 적이 생기면 종류에 맞는 모델이 있을 때
    // 캡슐 몸통·표시를 숨기고 그 자리(Visual 아래)에 리깅 모델을 세워요. 게임 코드·판정은 그대로예요.
    public class InkEnemyModels : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public EnemyKind kind;
            public GameObject model;                          // 발밑 원점, +Z 앞 (Unity로 가져온 리깅 모델)
            public RuntimeAnimatorController controller;      // Idle·Run·Attack (InkEnemyAnimator가 움직여요)
            public float scale;                               // 0이면 1
            public float yaw;                                 // 모델 앞을 +Z로 맞추는 회전
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        readonly HashSet<EnemyBrain> _done = new HashSet<EnemyBrain>();
        float _scan;

        public void SetEntries(Entry[] list) => entries = list ?? Array.Empty<Entry>();

        void Update()
        {
            _scan -= Time.deltaTime;
            if (_scan > 0f) return;
            _scan = 0.2f;
            foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (brain == null || _done.Contains(brain)) continue;
                _done.Add(brain);
                foreach (var e in entries)
                    if (e.kind == brain.Kind && e.model != null)
                    {
                        Dress(brain, e);
                        break;
                    }
            }
            _done.RemoveWhere(b => b == null);
        }

        static void Dress(EnemyBrain brain, Entry e)
        {
            var visual = brain.transform.Find("Visual");
            if (visual == null) return;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var model = Instantiate(e.model, visual, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, e.yaw, 0f);
            model.transform.localScale = Vector3.one * (e.scale > 0f ? e.scale : 1f);
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            if (e.controller != null) animator.runtimeAnimatorController = e.controller;
            var driver = brain.gameObject.AddComponent<InkEnemyAnimator>();
            driver.Init(brain, animator);
        }
    }

    // 적 모델 동작: 이동 속도 → 달리기/대기.
    // 공격은 한 번만 휘둘러요: 예고(Windup)가 시작되면 칼을 치켜든 첫 자세로 넘어가 멈춰 있다가, 판정(Active) 순간 끝까지 내려찍어요.
    // 예고가 끊기면(맞아서 등) 바로 대기로 돌아가요.
    public class InkEnemyAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");
        static readonly int AttackState = Animator.StringToHash("Attack");
        static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField] float raiseBlend = 0.2f;   // 치켜드는 데 걸리는 시간
        EnemyBrain _brain;
        Animator _animator;
        EnemyAttackTimeline.Phase _last;
        bool _swung;
        Vector3 _lastPos;

        public void Init(EnemyBrain brain, Animator animator)
        {
            _brain = brain;
            _animator = animator;
            _lastPos = transform.position;
        }

        void Update()
        {
            if (_brain == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            var phase = _brain.AttackPhase;
            if (phase != _last)
            {
                switch (phase)
                {
                    case EnemyAttackTimeline.Phase.Windup:
                        _animator.SetFloat(AttackSpeedId, 0f);                              // 첫 자세(치켜듦)에서 멈춰요
                        _animator.CrossFadeInFixedTime(AttackState, raiseBlend, 0, 0f);
                        _swung = false;
                        break;
                    case EnemyAttackTimeline.Phase.Active:
                        _animator.SetFloat(AttackSpeedId, 1f);                              // 한 번 내려찍어요
                        _swung = true;
                        break;
                    case EnemyAttackTimeline.Phase.Ready:
                        _animator.SetFloat(AttackSpeedId, 1f);
                        if (!_swung) _animator.CrossFadeInFixedTime(IdleState, 0.15f, 0);   // 휘두르기 전에 끊김
                        break;
                }
                _last = phase;
            }
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            _lastPos = transform.position;
            bool busy = phase != EnemyAttackTimeline.Phase.Ready || _brain.IsStunned || _brain.IsDead;
            _animator.SetFloat(SpeedId, busy || Time.deltaTime <= 0f ? 0f : d.magnitude / Time.deltaTime);
        }
    }
}
