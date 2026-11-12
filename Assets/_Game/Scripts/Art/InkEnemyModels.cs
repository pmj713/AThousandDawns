using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
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
        Vector3 _modelScale;           // 처음 월드 크기 (쓰러질 때 Visual이 납작해져도 시체는 이 크기로)
        Damageable _body;
        // 맞은 반응: 맞은 반대쪽(뒤)으로 몸이 젖혀져요. 경직이면 크게 젖혀 비틀거려요
        [SerializeField] float flinchAngle = 12f, flinchTime = 0.2f, staggerAngle = 24f;
        float _flinchTimer, _staggerTime, _staggerBlend;
        bool _corpse;
        // 파계승 금강불괴: 자세(Stance)로 넘어가 버티는 동안 몸이 금빛으로 물들어요
        MonkBrain _monk;
        BloodBuddhaBrain _buddha;   // 혈불: 2페이즈에 갈라져 나오는 팔(이름에 _Phase2_)을 그때 보여요
        int _buddhaPhase;
        bool _stanced;
        static readonly Color StanceGold = new Color(1f, 0.78f, 0.32f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        System.Reflection.PropertyInfo _phaseProp;   // 천마: 페이즈별 모습 (Phase가 있고 페이즈 텍스처가 있는 적만)
        int _shownPhase = 1;
        float _whirlAngle;
        bool _hasStatue, _statue;   // 석상 나한 (Statue·Wake 동작이 있을 때)
        RattanBrain _rattan;        // 등패수: 방패를 굴려 던지면 손 방패를 감춰요
        bool _rattanAway;
        WaterGhostBrain _waterGhost; // 수귀: 끌고 가는 동안 Drag
        bool _dragging;
        float _anchorAway;          // 손 소품을 던진 동안 감춰요 (쇠닻 거한 닻 · 철룡 유성추)
        string _thrownProp = "_Weapon_anchor";
        System.Reflection.PropertyInfo _transitionProp;   // 페이즈 전환 중인지 (IsTransitioning이 있고 전환 자세가 있는 적만)
        string _transitionState;
        bool _roaring;
        CultistBrain _cultist;      // 마교도: 광폭
        bool _frenzied;
        float _wakeFade;
        static readonly Color StatueGray = new Color(0.62f, 0.6f, 0.57f, 1f);

        public void Init(EnemyBrain brain, Animator animator, bool windupPlays = false, bool noAttackAnim = false, InkEnemyModels.Move[] moves = null)
        {
            _windupPlays = windupPlays;
            _noAttack = noAttackAnim;
            _moves = moves ?? Array.Empty<InkEnemyModels.Move>();
            _shieldBrain = brain as ShieldBrain;
            _monk = brain as MonkBrain;
            _buddha = brain as BloodBuddhaBrain;
            _rattan = brain as RattanBrain;
            _waterGhost = brain as WaterGhostBrain;
            foreach (var st in new[] { "Roar", "Ascend", "ShadowRush" })
                if (animator.HasState(0, Animator.StringToHash(st))) { _transitionState = st; break; }
            if (_transitionState != null) _transitionProp = brain.GetType().GetProperty("IsTransitioning");
            _cultist = brain as CultistBrain;
            if (Resources.Load<Texture2D>($"{brain.Kind}_P2") != null) _phaseProp = brain.GetType().GetProperty("Phase");
            _brain = brain;
            _animator = animator;
            _modelRest = animator.transform.localRotation;
            _modelScale = animator.transform.lossyScale;
            _body = brain.GetComponent<Damageable>();
            if (_body != null) _body.Damaged += OnDamaged;   // 브레인이 먼저 구독해서, 여기 올 땐 경직·쓰러짐이 이미 정해져 있어요
            if (brain.Rank == EnemyRank.Boss || brain.Rank == EnemyRank.Master) flinchAngle = 5f;   // 고수·보스는 덩치가 커서 살짝만 움찔해요
            _hasStatue = brain.Kind == EnemyKind.Arhat && animator.HasState(0, Animator.StringToHash("Statue")) && animator.HasState(0, Animator.StringToHash("Wake"));
            _lastPos = transform.position;
        }

        InkEnemyModels.Move PickMove()
        {
            string name = _brain.AttackName ?? "";   // 이름 없는 공격(멧돼지 돌진 등)은 attack이 ""인 동작
            foreach (var m in _moves)
                if (m.attack == name)
                {
                    // 혈불 2페이즈 손바닥 연타: 한 번 내려찍기 대신 세 번 이어 내려찍는 동작
                    var move = m;
                    if (_buddha != null && _buddha.Phase >= 2 && m.state == "Palm" && _animator.HasState(0, Animator.StringToHash("Palm3"))) move.state = "Palm3";
                    return move;
                }
            return new InkEnemyModels.Move { state = "Attack", mode = _windupPlays ? InkEnemyModels.MoveMode.Play : InkEnemyModels.MoveMode.Swing };
        }

        void Play(string state, float blend) => _animator.CrossFadeInFixedTime(Animator.StringToHash(state), blend, 0, 0f);

        void SetPropVisible(string part, bool visible)
        {
            foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains(part)) r.enabled = visible;
        }

        // 몸 색 덧칠 (null이면 원래 색). 무기·소품은 빼요
        void Tint(Color? color)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Weapon") || r.name.Contains("_Prop_")) continue;
                if (color.HasValue) { r.GetPropertyBlock(block); block.SetColor(BaseColorId, color.Value); r.SetPropertyBlock(block); }
                else r.SetPropertyBlock(null);
            }
        }

        void Update()
        {
            if (_brain == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            // 방패병: 대시 공격으로 방패가 부서지면 방패 소품을 감춰요
            if (_shieldBrain != null && !_shieldHidden && _shieldBrain.ShieldBroken)
            {
                foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("_Prop_Shield") || r.name.Contains("_Rattan_Shield") || r.name.Contains("_Weapon_towershield")) r.enabled = false;
                _shieldHidden = true;
            }
            if (_buddha != null && _buddha.Phase != _buddhaPhase)
            {
                _buddhaPhase = _buddha.Phase;
                foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("_Phase2_")) r.enabled = _buddhaPhase >= 2;
            }
            // 석상 나한: 깨기 전엔 잿빛 석상 자세(Statue)로 굳어 있다가, 깨어나면 몸을 떨고 두 팔을 떨쳐 일어나요(Wake). 잿빛은 서서히 빠져요
            if (_hasStatue)
            {
                bool dormant = !_brain.IsAwake && !_brain.IsDead;
                if (dormant && !_statue) { _statue = true; _wakeFade = 0f; Play("Statue", 0f); Tint(StatueGray); }
                else if (!dormant && _statue) { _statue = false; _wakeFade = 1f; _animator.SetFloat(AttackSpeedId, 1f); Play("Wake", 0.05f); }
                if (_wakeFade > 0f)
                {
                    _wakeFade = Mathf.MoveTowards(_wakeFade, 0f, Time.deltaTime / 0.8f);
                    if (_wakeFade > 0f) Tint(Color.Lerp(Color.white, StatueGray, _wakeFade)); else Tint(null);
                }
                if (_statue) { _animator.SetFloat(SpeedId, 0f); return; }
            }
            // 등패수: 방패를 굴려 던진 동안엔 손에 방패가 없어요 (부서지면 아예 감춘 채)
            if (_rattan != null && !_shieldHidden && _rattan.ShieldThrown != _rattanAway)
            {
                _rattanAway = _rattan.ShieldThrown;
                SetPropVisible("_Rattan_Shield", !_rattanAway);
            }
            // 쇠닻 거한: 던진 닻이 땅에 꽂혀 있는 동안(약 3초)엔 손이 비어요
            if (_anchorAway > 0f)
            {
                _anchorAway -= Time.deltaTime;
                if (_anchorAway <= 0f || _brain.IsDead) { _anchorAway = 0f; SetPropVisible(_thrownProp, true); }
            }
            // 수귀: 붙잡아 끌고 가는 동안 뒷걸음질 (끝나면 대기로)
            if (_waterGhost != null && _waterGhost.Dragging != _dragging)
            {
                _dragging = _waterGhost.Dragging;
                if (_dragging) Play("Drag", 0.1f); else _animator.CrossFadeInFixedTime(IdleState, 0.2f, 0);
            }
            if (_dragging) { _animator.SetFloat(SpeedId, 0f); return; }
            // 페이즈 전환(IsTransitioning, 무적) 동안의 자세: 철룡은 휘청 물러났다가 포효(Roar), 천마는 무릎 꿇었다 일어서기(Ascend),
            //   혈영검마는 낮게 웅크린 채 버텨요(ShadowRush 첫 자세에서 멈춤). 전환 동작이 없는 적은 그대로
            if (_transitionProp != null && (bool)_transitionProp.GetValue(_brain) != _roaring)
            {
                _roaring = !_roaring;
                if (_roaring)
                {
                    bool hold = _transitionState == "ShadowRush";
                    _animator.SetFloat(AttackSpeedId, hold ? 0f : 1f);
                    Play(_transitionState, 0.1f);
                }
                else _animator.SetFloat(AttackSpeedId, 1f);
            }
            if (_roaring) { _animator.SetFloat(SpeedId, 0f); return; }
            // 천마: 페이즈마다 몸 텍스처를 갈아 끼워요 (1 정갈한 검객 → 2 핏빛 마기 무늬 → 3 몸이 갈라져 금이 붉게 달아올라요).
            //   텍스처는 HeavenlyDemon/Resources/HeavenlyDemon_P2·P3 (Enemies/Tools~/phase_textures.py). 브레인이 들어오기 전에도 컴파일되게 Phase는 이름으로 읽어요
            if (_phaseProp != null)
            {
                int ph = (int)_phaseProp.GetValue(_brain);
                if (ph != _shownPhase)
                {
                    _shownPhase = ph;
                    var tex = ph >= 2 ? Resources.Load<Texture2D>($"{_brain.Kind}_P{Mathf.Min(ph, 3)}") : null;
                    var block = new MaterialPropertyBlock();
                    foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.name.Contains("Weapon") || r.name.Contains("_Prop_")) continue;
                        r.GetPropertyBlock(block);
                        if (tex != null) block.SetTexture(BaseMapId, tex); else block.Clear();
                        r.SetPropertyBlock(block);
                    }
                }
            }
            // 마교도: 체력이 절반 아래로 떨어져 광폭에 들면 낮게 웅크려 칼을 끄는 자세를 한 번 보이고, 몸이 붉게 물들어요
            if (_cultist != null && !_frenzied && _cultist.IsFrenzied)
            {
                _frenzied = true;
                _animator.SetFloat(AttackSpeedId, 1f);
                Play("Frenzy", 0.1f);
                Tint(new Color(1f, 0.55f, 0.52f, 1f));
            }
            var phase = _brain.AttackPhase;
            // 금강불괴: 파계승은 버티는 자세(IsStanced), 혜광은 금강불괴 예고 동안 몸이 금빛
            bool gold = _monk != null ? _monk.IsStanced
                : phase == EnemyAttackTimeline.Phase.Windup && _brain.AttackName == "금강불괴";
            if (gold != _stanced)
            {
                _stanced = gold;
                if (_monk != null)
                {
                    if (_stanced) Play("Stance", 0.15f);
                    else _animator.CrossFadeInFixedTime(IdleState, 0.2f, 0);
                }
                var block = new MaterialPropertyBlock();
                foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.Contains("Weapon")) continue;
                    if (_stanced) { r.GetPropertyBlock(block); block.SetColor(BaseColorId, StanceGold); r.SetPropertyBlock(block); }
                    else r.SetPropertyBlock(null);
                }
            }
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
                        if (_move.attack == "닻 던지기") { _thrownProp = "_Weapon_anchor"; _anchorAway = 3.2f; SetPropVisible(_thrownProp, false); }   // 닻을 던졌어요 (꽂힌 동안)
                        if (_move.attack == "사슬 당기기") { _thrownProp = "_Weapon_meteor"; _anchorAway = 0.9f; SetPropVisible(_thrownProp, false); }   // 유성추가 사슬째 날아갔어요 (이펙트가 그려요)
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
            // 회전베기: 판정 동안 모델째 돌고(위에서 보면 시계 반대 방향), 끝나면 원래 방향으로
            if (_moving && _move.mode == InkEnemyModels.MoveMode.Whirl)
                _whirlAngle = (_whirlAngle + whirlSpeed * Time.deltaTime) % 360f;
            else
                _whirlAngle = 0f;
            // 맞은 반응: 움찔(짧게 젖혔다 돌아옴) · 경직(크게 젖혀 비틀거리다 풀리면 돌아옴)
            float tilt = 0f;
            if (_flinchTimer > 0f)
            {
                _flinchTimer -= Time.deltaTime;
                tilt = flinchAngle * Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - _flinchTimer / flinchTime));
            }
            _staggerBlend = Mathf.MoveTowards(_staggerBlend, _brain.IsStunned ? 1f : 0f, Time.deltaTime / (_brain.IsStunned ? 0.08f : 0.25f));
            if (_staggerBlend > 0f)
            {
                _staggerTime += Time.deltaTime;
                float wobble = 0.8f + 0.2f * Mathf.Sin(_staggerTime * 11f);
                tilt = Mathf.Max(tilt, staggerAngle * wobble * _staggerBlend);
            }
            else _staggerTime = 0f;
            // 적의 앞(+Z) 기준으로 뒤로 젖혀요 (X축 −회전이 위를 뒤로 보내요). 발밑이 원점이라 발을 축으로 젖혀져요
            _animator.transform.localRotation = Quaternion.AngleAxis(-tilt, Vector3.right) * _modelRest * Quaternion.Euler(0f, -_whirlAngle, 0f);
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

        void OnDamaged(Damageable body, int amount)
        {
            if (_brain == null || _animator == null) return;
            if (_brain.IsDead) { MakeCorpse(); return; }
            if (!body.LastHitReacts) return;            // 화상·번개 같은 파생 피해는 움찔하지 않아요
            if (!_brain.IsStunned) _flinchTimer = flinchTime;   // 경직이면 Update가 비틀거리게 해요
        }

        // 쓰러짐: 모델을 적에게서 떼어 시체로 남겨요 (적 오브젝트는 곧 지워져요).
        // 사람은 뒤로, 짐승은 옆으로 쓰러지고, 잠시 있다가 땅으로 가라앉아 사라져요. 화약 산적은 터져서 남기지 않아요
        void MakeCorpse()
        {
            if (_corpse) return;
            _corpse = true;
            if (_brain.Kind == EnemyKind.Bomber) return;   // 터지는 적은 남기지 않아요 (등불 귀신은 이제 터지지 않고 쓰러져요)
            var model = _animator.transform;
            var corpse = new GameObject($"{_brain.name} (쓰러짐)");
            corpse.transform.SetPositionAndRotation(model.position, transform.rotation);
            model.SetParent(corpse.transform, false);
            model.localPosition = Vector3.zero;
            model.localRotation = _modelRest;
            model.localScale = _modelScale;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (!r.name.Contains("_Prop_Shield") || !_shieldHidden) r.enabled = true;
            bool beast = _brain.Kind == EnemyKind.Boar || _brain.Kind == EnemyKind.Hound || _brain.Kind == EnemyKind.Snake;
            // 무거운 갑옷·술사는 앞으로 고꾸라져요, 혈폭자는 부풀어 터지고, 수귀는 물 밑으로 꺼져요
            bool forward = _brain.Kind == EnemyKind.DemonGuard || _brain.Kind == EnemyKind.BloodShaman || _brain.Kind == EnemyKind.Arhat;
            var style = _brain.Kind == EnemyKind.BloodBomber ? InkCorpse.Style.Swell : _brain.Kind == EnemyKind.WaterGhost ? InkCorpse.Style.Sink : InkCorpse.Style.Fall;
            corpse.AddComponent<InkCorpse>().Init(_animator, beast ? Vector3.forward : Vector3.right,
                _brain.Kind == EnemyKind.Snake ? 160f : beast ? 88f : forward ? 80f : -85f, style);
            enabled = false;
        }

        void OnDestroy()
        {
            if (_body != null) _body.Damaged -= OnDamaged;
        }
    }

    // 쓰러진 적: 발을 축으로 넘어가(점점 빨리) 살짝 튕긴 뒤, 잠시 누워 있다가 땅속으로 가라앉아 사라져요.
    //   Swell(혈폭자): 넘어가지 않고 1.2초 동안 부들부들 떨며 부풀어 오르다가 터지는 순간 사라져요 (터짐은 게임·이펙트)
    //   Sink(수귀): 선 채로 물 밑으로 꺼지듯 빠르게 가라앉아요
    public class InkCorpse : MonoBehaviour
    {
        public enum Style { Fall, Swell, Sink }
        const float FallTime = 0.4f, LieTime = 1.2f, SinkTime = 1.4f, SinkDepth = 0.8f;
        const float SwellScale = 1.4f, GhostSinkTime = 0.7f, GhostSinkDepth = 2.4f;
        float _swellTime = 1.2f;   // 혈폭자 터지기까지 (bloodbomber.blast_fuse)
        Animator _animator;
        Vector3 _axis;
        float _angle, _t;
        Quaternion _start;
        Vector3 _startPos, _startScale;
        Style _style;

        public void Init(Animator animator, Vector3 localAxis, float angle, Style style = Style.Fall)
        {
            _animator = animator;
            _axis = localAxis;
            _angle = angle;
            _style = style;
            _start = transform.rotation;
            _startPos = transform.position;
            _startScale = transform.localScale;
            if (style == Style.Swell) _swellTime = Mathf.Max(0.1f, AThousandDawns.Core.GameConfig.Get("bloodbomber.blast_fuse", 1.2f));
            if (_animator != null)
            {
                _animator.SetFloat(Animator.StringToHash("Speed"), 0f);
                _animator.speed = style == Style.Fall ? 0.3f : 0f;   // 넘어가는 동안 굳어 가요
            }
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_style == Style.Swell)
            {
                float k = Mathf.Clamp01(_t / _swellTime);
                float shake = 0.02f + 0.06f * k;                                 // 터질 때가 가까울수록 크게 떨어요
                transform.localScale = _startScale * (1f + (SwellScale - 1f) * k * k) + Vector3.one * Mathf.Sin(_t * 60f) * shake * 0.3f;
                transform.position = _startPos + new Vector3(Mathf.Sin(_t * 71f), 0f, Mathf.Cos(_t * 53f)) * shake;
                if (k >= 1f) Destroy(gameObject);
                return;
            }
            if (_style == Style.Sink)
            {
                float s = Mathf.Clamp01(_t / GhostSinkTime);
                transform.position = _startPos + Vector3.down * GhostSinkDepth * s * s;
                transform.rotation = _start * Quaternion.AngleAxis(12f * s, Vector3.right);   // 뒤로 살짝 젖혀지며
                if (s >= 1f) Destroy(gameObject);
                return;
            }
            float fall = Mathf.Clamp01(_t / FallTime);
            float a = fall * fall;                          // 점점 빨리 넘어가요
            if (fall >= 1f)
            {
                float b = _t - FallTime;                    // 바닥에 닿고 살짝 튕겨요
                a = 1f - 0.08f * Mathf.Max(0f, Mathf.Sin(b * 18f)) * Mathf.Exp(-b * 8f);
                if (_animator != null) _animator.speed = 0f;
            }
            transform.rotation = _start * Quaternion.AngleAxis(_angle * a, _axis);
            float sink = Mathf.Clamp01((_t - FallTime - LieTime) / SinkTime);
            transform.position = _startPos + Vector3.down * SinkDepth * sink * sink;
            if (sink >= 1f) Destroy(gameObject);
        }
    }
}
