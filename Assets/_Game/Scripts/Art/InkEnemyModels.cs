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
        // 패턴 동작이 예고·판정·빈틈 동안 어떻게 움직이는지
        public enum MoveMode
        {
            Swing,  // 예고 동안 첫 자세(치켜듦)에서 멈췄다가 판정 순간 끝까지 (이어지는 상태가 있으면 차례로)
            Play,   // 예고부터 그대로 재생 (나팔 불기·붕산권)
            Run,    // 예고 동안 달리는 첫 자세로 멈췄다가 판정(돌진) 동안 달려요
            Loop,   // 예고 동안 멈췄다가 판정 동안 되풀이
            Whirl,  // 예고 동안 멈췄다가 판정 동안 모델째 시계 반대 방향으로 빙글빙글 돌아요 (녹림왕 회전베기, 힐윈드처럼)
        }

        // 고수·보스 패턴별 동작: 브레인의 AttackName이 attack과 같으면 state로 넘어가요.
        // after가 있으면 빈틈(Recovery)이 시작될 때 그 상태를 이어 재생해요 (돌진 뒤 내려찍기)
        [Serializable]
        public struct Move
        {
            public string attack;
            public string state;
            public MoveMode mode;
            public string after;
            public string ready;    // 있으면 예고 동안 이 상태(준비 자세)를 쓰고, 판정이 시작되면 state로 넘어가요 (돌진 준비 → 돌진)
        }

        [Serializable]
        public struct Entry
        {
            public EnemyKind kind;
            public GameObject model;                          // 발밑 원점, +Z 앞 (Unity로 가져온 리깅 모델)
            public RuntimeAnimatorController controller;      // Idle·Run·Attack (InkEnemyAnimator가 움직여요)
            public float scale;                               // 0이면 1
            public float yaw;                                 // 모델 앞을 +Z로 맞추는 회전
            public bool windupPlays;                          // 예고 동안 공격 동작을 그대로 재생 (나팔 불기·활 쏘기). 아니면 치켜든 자세로 멈춤
            public bool noAttackAnim;                         // 공격 동작 없음 (화약 산적: 손 들지 않고 그 자리에서 터져요)
            public Move[] moves;                              // 패턴별 동작 (없으면 모든 공격이 Attack 상태)
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
            driver.Init(brain, animator, e.windupPlays, e.noAttackAnim, e.moves);
        }
    }

    // 적 모델 동작: 이동 속도 → 달리기/대기.
    // 공격은 한 번만 휘둘러요: 예고(Windup)가 시작되면 칼을 치켜든 첫 자세로 넘어가 멈춰 있다가, 판정(Active) 순간 끝까지 내려찍어요.
    // 예고가 끊기면(맞아서 등) 바로 대기로 돌아가요. 고수·보스는 패턴(AttackName)마다 다른 상태와 방식(Move)을 써요.
    public class InkEnemyAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");
        static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField] float raiseBlend = 0.2f;   // 치켜드는 데 걸리는 시간
        EnemyBrain _brain;
        Animator _animator;
        EnemyAttackTimeline.Phase _last;
        bool _swung;
        Vector3 _lastPos;

        bool _windupPlays;
        bool _noAttack;
        InkEnemyModels.Move[] _moves;
        InkEnemyModels.Move _move;     // 지금 공격의 동작
        bool _moving;                  // 달리기·되풀이 동작 중 (기절하거나 빈틈이 오면 대기로)
        ShieldBrain _shieldBrain;
        bool _shieldHidden;
        [SerializeField] float whirlSpeed = 1800f;  // 회전베기 빠르기 (도/초, 1초에 5바퀴: 자세가 잘 안 보일 만큼)
        Quaternion _modelRest;
        float _whirlAngle;

        public void Init(EnemyBrain brain, Animator animator, bool windupPlays = false, bool noAttackAnim = false, InkEnemyModels.Move[] moves = null)
        {
            _windupPlays = windupPlays;
            _noAttack = noAttackAnim;
            _moves = moves ?? Array.Empty<InkEnemyModels.Move>();
            _shieldBrain = brain as ShieldBrain;
            _brain = brain;
            _animator = animator;
            _modelRest = animator.transform.localRotation;
            _lastPos = transform.position;
        }

        InkEnemyModels.Move PickMove()
        {
            string name = _brain.AttackName ?? "";   // 이름 없는 공격(멧돼지 돌진 등)은 attack이 ""인 동작
            foreach (var m in _moves)
                if (m.attack == name) return m;
            return new InkEnemyModels.Move { state = "Attack", mode = _windupPlays ? InkEnemyModels.MoveMode.Play : InkEnemyModels.MoveMode.Swing };
        }

        void Play(string state, float blend) => _animator.CrossFadeInFixedTime(Animator.StringToHash(state), blend, 0, 0f);

        void Update()
        {
            if (_brain == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            // 방패병: 대시 공격으로 방패가 부서지면 방패 소품을 감춰요
            if (_shieldBrain != null && !_shieldHidden && _shieldBrain.ShieldBroken)
            {
                foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("_Prop_Shield")) r.enabled = false;
                _shieldHidden = true;
            }
            var phase = _brain.AttackPhase;
            if (_noAttack) _last = phase;   // 공격 동작 없이 대기·달리기만
            if (phase != _last)
            {
                switch (phase)
                {
                    case EnemyAttackTimeline.Phase.Windup:
                        _move = PickMove();
                        // 첫 자세(치켜듦)에서 멈추거나, 나팔·붕산권은 그대로 재생해요
                        _animator.SetFloat(AttackSpeedId, _move.mode == InkEnemyModels.MoveMode.Play ? 1f : 0f);
                        Play(string.IsNullOrEmpty(_move.ready) ? _move.state : _move.ready, raiseBlend);
                        _swung = false;
                        _moving = false;
                        break;
                    case EnemyAttackTimeline.Phase.Active:
                        _animator.SetFloat(AttackSpeedId, 1f);                              // 한 번 내려찍어요 (돌진·회전은 이어서)
                        if (!string.IsNullOrEmpty(_move.ready)) Play(_move.state, 0.1f);    // 준비 자세에서 박차고 나가요
                        _swung = _move.mode == InkEnemyModels.MoveMode.Swing;
                        _moving = _move.mode != InkEnemyModels.MoveMode.Swing && _move.mode != InkEnemyModels.MoveMode.Play;
                        break;
                    case EnemyAttackTimeline.Phase.Recovery:
                        _animator.SetFloat(AttackSpeedId, 1f);
                        if (!string.IsNullOrEmpty(_move.after)) { Play(_move.after, 0.1f); _swung = false; }  // 돌진 끝에 내려찍고 그 자세로 멈춰요 (빈틈이 끝나면 대기로)
                        else if (_moving) { _animator.CrossFadeInFixedTime(IdleState, 0.2f, 0); _swung = true; }
                        _moving = false;
                        break;
                    case EnemyAttackTimeline.Phase.Ready:
                        _animator.SetFloat(AttackSpeedId, 1f);
                        if (!_swung || _moving) _animator.CrossFadeInFixedTime(IdleState, 0.15f, 0);   // 휘두르기 전에 끊김
                        _moving = false;
                        break;
                }
                _last = phase;
            }
            if (_moving && (_brain.IsStunned || _brain.IsDead))
            {
                _animator.CrossFadeInFixedTime(IdleState, 0.15f, 0);   // 돌진하다 벽에 부딪혀 기절
                _moving = false;
            }
            // 회전베기: 판정 동안 모델째 돌고, 끝나면 원래 방향으로
            if (_moving && _move.mode == InkEnemyModels.MoveMode.Whirl)
            {
                _whirlAngle = (_whirlAngle + whirlSpeed * Time.deltaTime) % 360f;
                _animator.transform.localRotation = _modelRest * Quaternion.Euler(0f, -_whirlAngle, 0f);   // 위에서 보면 시계 반대 방향
            }
            else if (_whirlAngle != 0f)
            {
                _whirlAngle = 0f;
                _animator.transform.localRotation = _modelRest;
            }
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            _lastPos = transform.position;
            // 화약 산적: 불붙인 뒤 터질 때까지 계속 달려요 (붙어서 멈춰도 달리는 동작 유지)
            if (_noAttack && phase != EnemyAttackTimeline.Phase.Ready && !_brain.IsStunned)
            {
                _animator.SetFloat(SpeedId, 1f);
                return;
            }
            bool busy = phase != EnemyAttackTimeline.Phase.Ready || _brain.IsStunned || _brain.IsDead;
            _animator.SetFloat(SpeedId, busy || Time.deltaTime <= 0f ? 0f : d.magnitude / Time.deltaTime);
        }
    }
}
