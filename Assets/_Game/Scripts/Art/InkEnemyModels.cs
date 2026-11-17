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
            // 적은 모두 EnemyDirector.Spawn을 거쳐 목록에 들어가요. 씬 전체를 뒤지는 건 감독이 없을 때(샌드박스)와 2초에 한 번만
            if (_director == null) _director = FindFirstObjectByType<EnemyDirector>();
            if (_director != null)
                foreach (var brain in _director.Enemies) Consider(brain);
            _fullScan -= 0.2f;
            if (_director == null || _fullScan <= 0f)
            {
                _fullScan = 2f;
                foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) Consider(brain);
                _done.RemoveWhere(b => b == null);
            }
        }

        EnemyDirector _director;
        float _fullScan;

        void Consider(EnemyBrain brain)
        {
            if (brain == null || !_done.Add(brain)) return;
            foreach (var e in entries)
                if (e.kind == brain.Kind && e.model != null)
                {
                    Dress(brain, e);
                    break;
                }
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
        static readonly int RunState = Animator.StringToHash("Run");
        float _oneShotUntil;           // 등장(Intro·Spawn)을 그대로 재생하는 중 (그 사이 움직이기 시작하면 달리기로 넘겨요)

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
        Func<int> _phase;   // 천마: 페이즈별 모습 (Phase가 있고 페이즈 텍스처가 있는 적만). 적마다 한 번 묶은 대리자라 매 프레임 박싱이 없어요
        int _shownPhase = 1;
        float _whirlAngle;
        bool _hasStatue, _statue;   // 석상 나한 (Statue·Wake 동작이 있을 때)
        RattanBrain _rattan;        // 등패수: 방패를 굴려 던지면 손 방패를 감춰요
        bool _rattanAway;
        WaterGhostBrain _waterGhost; // 수귀: 끌고 가는 동안 Drag
        bool _dragging;
        float _anchorAway;          // 손 소품을 던진 동안 감춰요 (쇠닻 거한 닻 · 철룡 유성추)
        string _thrownProp = "_Weapon_anchor";
        Func<bool> _transitioning;   // 페이즈 전환 중인지 (IsTransitioning이 있고 전환 자세가 있는 적만)
        string _transitionState;
        bool _hasIntro, _introDone;   // 보스·고수 등장 연출 (Intro 상태가 있을 때, 깨어나는 순간 한 번)
        bool _hasSpawn;               // 일반 적 등장: 처음 한 번 웅크렸다 일어서요
        bool _roaring;
        CultistBrain _cultist;      // 마교도: 광폭
        bool _frenzied;
        float _wakeFade;
        static readonly Color StatueGray = new Color(0.62f, 0.6f, 0.57f, 1f);

        // 몸에 덧입히는 표시 (InkCharacter의 _AuraColor · _CrackColor)
        //  - 분열해 나온 적(IsSplitChild): 모델 0.8배, 몸 군데군데 보랏빛 금 (체력이 적어 약하다는 표시). 작은 슬라임은 금 없이 0.6배
        //  - 특성 둘 이상 정예: 특성 색을 섞은 은은한 가장자리 기운이 숨 쉬듯 (하나면 머리 위 표식만)
        static readonly int AuraId = Shader.PropertyToID("_AuraColor");
        static readonly int CrackId = Shader.PropertyToID("_CrackColor");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        static readonly Color SplitCrack = new Color(0.62f, 0.32f, 0.85f, 1f);
        Renderer[] _bodyRenderers;
        Color _aura, _crack;
        float _age;
        bool _splitShown;
        int _auraTraits;
        bool _frozenShown, _statusShown;   // 빙결로 동작을 멈췄는지 · 상태 이상 기운을 보이는 중인지
        bool _alliedShown;                 // 협력한 적 보랏빛 가장자리를 보이는 중인지
        HeavenlyDemonBrain _demon;          // 마검사 천마: 페이즈 기운 · 순간이동 흐려짐
        string _demonAttack;
        bool _blinkFade, _fadeUsed;
        float _fade;
        static readonly Color DemonViolet = new Color(0.62f, 0.3f, 1f), DemonBlue = new Color(0.3f, 0.55f, 1f), DemonFlame = new Color(1f, 0.38f, 0.1f);
        Transform _lastRival;              // 마지막으로 노린 다른 진영 적 (그 적에게 쓰러지면 그 반대쪽으로 넘어가요)
        static readonly Color AlliedGlow = new Color(0.62f, 0.3f, 1f, 1f);   // 차원 균열과 같은 보랏빛
        MaterialPropertyBlock _overlayBlock;
        bool _overlayUsed;

        static Color TraitGlow(EliteTrait t) => t == EliteTrait.Split ? new Color(0.66f, 0.42f, 0.9f)
            : t == EliteTrait.IronShirt ? new Color(0.62f, 0.7f, 0.84f)
            : t == EliteTrait.Rejuvenation ? new Color(0.42f, 0.82f, 0.52f)
            : new Color(1f, 0.45f, 0.2f);   // 열화

        // 덧입힌 표시를 다시 얹어요 (몸 색 덧칠이 블록을 지운 뒤에도)
        void ApplyOverlay()
        {
            if (_bodyRenderers == null) return;
            if (_aura.a > 0f || _crack.a > 0f || _fade > 0f) _overlayUsed = true;
            if (!_overlayUsed) return;   // 한 번도 안 썼으면 블록을 건드리지 않아요 (SRP 배칭 유지)
            if (_overlayBlock == null) _overlayBlock = new MaterialPropertyBlock();
            foreach (var r in _bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_overlayBlock);
                _overlayBlock.SetColor(AuraId, _aura);
                _overlayBlock.SetColor(CrackId, _crack);
                if (_fadeUsed && !_brain.IsDead) _overlayBlock.SetFloat(DissolveId, _fade);   // 천마 순간이동 검격: 몸이 흐려져요 (쓴 적 있는 적만 — 쓰러진 뒤 흩어짐을 덮지 않게)
                r.SetPropertyBlock(_overlayBlock);
            }
        }

        void TickOverlay()
        {
            _age += Time.deltaTime;
            // 분열해 나온 적: 나온 직후(1초 안)에 한 번 알아봐요
            if (!_splitShown && _age < 1f && _brain.IsSplitChild)
            {
                _splitShown = true;
                bool slime = _brain.Kind == EnemyKind.Slime;   // 갈라진 작은 슬라임: 같은 모델을 작게 (젤리라 금은 없어요)
                float shrink = slime ? 0.6f : 0.8f;
                _animator.transform.localScale *= shrink;
                _modelScale *= shrink;
                if (!slime) { _crack = SplitCrack; ApplyOverlay(); }
            }
            // 상태 이상 (몸 가장자리 기운이 정예 기운보다 먼저): 빙결은 얼음빛 테두리 + 동작이 그 자리에서 멈춰요,
            //   화상은 중첩이 쌓일수록 가장자리가 그을린 듯 어두워져요. 몸 둘레 불꽃·서리 입자는 아트 2가 그려요
            var status = _brain.Statuses;
            bool frozen = status != null && status.IsFrozen && !_brain.IsDead;
            if (frozen != _frozenShown)
            {
                _frozenShown = frozen;
                _animator.speed = frozen ? 0f : 1f;
            }
            float burn = status != null && !_brain.IsDead && status.BurnStacks > 0 ? Mathf.Clamp01(status.BurnStacks / (float)Mathf.Max(1, status.BurnMax)) : 0f;
            if (frozen || burn > 0f)
            {
                _aura = frozen ? new Color(0.6f, 0.85f, 1f, 0.8f + 0.1f * Mathf.Sin(Time.time * 5f))
                               : new Color(0.08f, 0.06f, 0.05f, 0.25f + 0.45f * burn);
                _statusShown = true;
                ApplyOverlay();
                return;
            }
            if (_statusShown) { _statusShown = false; _aura = Color.clear; _auraTraits = -1; ApplyOverlay(); }   // 풀리면 정예 기운을 다시 따져요

            // 마검사 천마 (HeavenlyDemonBrain.Phase): 2페이즈 마법 — 몸 가장자리에 보랏빛·청색 마력, 3페이즈 마검 융합 — 보라와 주홍이 번갈아 타올라요.
            //   순간이동 검격: 예고 동안 몸이 흐려졌다가(_Dissolve 일부) 나타나는 순간(Cue Blink) 또렷해져요
            if (_demon != null && !_brain.IsDead)
            {
                string attack = _brain.AttackName;
                if (attack != _demonAttack) { _demonAttack = attack; if (attack == "순간이동 검격") _blinkFade = true; }
                if (attack != "순간이동 검격") _blinkFade = false;
                float goal = _blinkFade ? 0.55f : 0f;
                if (_fade != goal) { _fadeUsed = true; _fade = Mathf.MoveTowards(_fade, goal, Time.deltaTime * (_blinkFade ? 1.2f : 4f)); }
                int phase = _demon.Phase;
                float t = Time.time;
                _aura = phase >= 3 ? Color.Lerp(DemonViolet, DemonFlame, 0.5f + 0.5f * Mathf.Sin(t * 3f))
                      : phase == 2 ? Color.Lerp(DemonViolet, DemonBlue, 0.5f + 0.5f * Mathf.Sin(t * 1.5f)) : Color.clear;
                if (phase >= 2) _aura.a = 0.35f + 0.12f * Mathf.Sin(t * 2.4f);
                ApplyOverlay();
                return;
            }

            if ((_enraged || _stoned) && !_brain.IsDead)
            {
                _aura = _stoned ? new Color(StoneGlow.r, StoneGlow.g, StoneGlow.b, 0.7f) : new Color(EnrageGlow.r, EnrageGlow.g, EnrageGlow.b, 0.45f + 0.15f * Mathf.Sin(Time.time * 6f));
                ApplyOverlay();
                return;
            }
            // 협력한 적 (차원 겹침, EnemyBrain.Allied): 몸 가장자리에 은은한 보랏빛 차원 기운이 숨 쉬어요 (발밑 원·차원 끈은 아트 2의 InkRivalFx).
            //   상태 이상보다 뒤, 정예 기운보다 앞이에요
            if (_brain.Rival != null) _lastRival = _brain.Rival.transform;
            if (_brain.Allied && !_brain.IsDead)
            {
                _aura = new Color(AlliedGlow.r, AlliedGlow.g, AlliedGlow.b, 0.3f + 0.12f * Mathf.Sin(Time.time * 1.8f));
                _alliedShown = true;
                ApplyOverlay();
                return;
            }
            if (_alliedShown) { _alliedShown = false; _aura = Color.clear; _auraTraits = -1; ApplyOverlay(); }

            // 특성이 둘 이상인 정예: 특성 색 기운 (특성이 늘면 다시 섞어요)
            int count = _brain.IsElite ? _brain.Traits.Count : 0;
            if (count != _auraTraits)
            {
                _auraTraits = count;
                if (count < 2) { _aura = Color.clear; ApplyOverlay(); }
            }
            if (_auraTraits >= 2 && !_brain.IsDead)
            {
                var mix = Color.black;
                var traits = _brain.Traits;
                for (int i = 0; i < traits.Count; i++) mix += TraitGlow(traits[i]);   // foreach는 목록 인터페이스라 매 프레임 열거자를 만들어요
                mix /= _brain.Traits.Count;
                float strength = (_auraTraits >= 3 ? 0.55f : 0.38f) * (0.75f + 0.25f * Mathf.Sin(Time.time * 2.4f));
                _aura = new Color(mix.r, mix.g, mix.b, strength);
                ApplyOverlay();
            }
        }

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
            foreach (var st in new[] { "Summon", "Roar", "Ascend", "ShadowRush" })   // 데스나이트는 2페이즈에 검을 꽂아 죽은 자를 불러요 (Summon)
                if (animator.HasState(0, Animator.StringToHash(st))) { _transitionState = st; break; }
            if (_transitionState != null) _transitioning = Getter<bool>(brain, "IsTransitioning");
            _hasIntro = (brain.Rank == EnemyRank.Boss || brain.Rank == EnemyRank.Master) && animator.HasState(0, Animator.StringToHash("Intro"));
            _cultist = brain as CultistBrain;
            _demon = brain as HeavenlyDemonBrain;
            if (_demon != null) HeavenlyDemonBrain.Cue += OnDemonCue;
            if (Resources.Load<Texture2D>($"{brain.Kind}_P2") != null) _phase = Getter<int>(brain, "Phase");
            _brain = brain;
            _animator = animator;
            _modelRest = animator.transform.localRotation;
            _modelScale = animator.transform.lossyScale;
            _body = brain.GetComponent<Damageable>();
            if (_body != null) _body.Damaged += OnDamaged;   // 브레인이 먼저 구독해서, 여기 올 땐 경직·쓰러짐이 이미 정해져 있어요
            if (brain.Rank == EnemyRank.Boss || brain.Rank == EnemyRank.Master) flinchAngle = 5f;   // 고수·보스는 덩치가 커서 살짝만 움찔해요
            _hasStatue = brain.Kind == EnemyKind.Arhat && animator.HasState(0, Animator.StringToHash("Statue")) && animator.HasState(0, Animator.StringToHash("Wake"));
            _hasSpawn = !_hasIntro && !_hasStatue && animator.HasState(0, Animator.StringToHash("Spawn"));
            if (brain.Rank == EnemyRank.Boss || brain.Rank == EnemyRank.Master)   // 보스·고수 공통 견제기·도약 (EnemyBrain.BossKit, 고수는 약한 판)
            {
                _bossKit = true;
                EnemyBrain.BossVolley += OnBossVolley;
                EnemyBrain.BossLeapStarted += OnLeapStarted;
                EnemyBrain.BossLeapLanded += OnLeapLanded;
                if (brain is OgreBrain) OgreBrain.RockThrown += OnRockThrown;
                if (brain is DeathKnightBrain) DeathKnightBrain.CurseWave += OnCurseWave;
                // 보스·고수 패턴 개편: 공격과 따로 오는 신호마다 손짓 동작
                if (brain is NokrimBrain) NokrimBrain.BambooFall += OnBambooFall;
                if (brain is IronFistBrain) IronFistBrain.FistWave += OnFistWave;
                if (brain is GoblinChiefBrain) GoblinChiefBrain.TrapThrown += OnTrapThrown;
                if (brain is OgreBrain) OgreBrain.Enraged += OnOgreEnraged;
                if (brain is IronFistBrain) IronFistBrain.FarLeaped += OnFarLeaped;
                if (brain is DeathKnightBrain) DeathKnightBrain.SpectralRide += OnSpectralRide;
                if (brain is DarkElfBrain) { DarkElfBrain.Emerged += OnEmerged; DarkElfBrain.Vanished += OnVanished; DarkElfBrain.DanceCut += OnDanceCut; }
                if (brain is IronFistBrain) IronFistBrain.Grabbed += OnGrabbed;
                if (brain is DeathKnightBrain) DeathKnightBrain.Raise += OnKnightRaise;
            }
            _modelRestPos = animator.transform.localPosition;
            if (brain is WerewolfBrain) { _werewolf = true; WerewolfBrain.Enraged += OnEnraged; }
            if (brain is GargoyleBrain && animator.HasState(0, Animator.StringToHash("Stone"))) { _gargoyle = true; GargoyleBrain.StoneChanged += OnStoneChanged; }
            if (brain is SkeletonBrain && animator.HasState(0, Animator.StringToHash("Reassemble")))
            {
                _skeleton = true;
                SkeletonBrain.Reassembling += OnReassembling;
            }
            _lastPos = transform.position;
            var body = new System.Collections.Generic.List<Renderer>();
            foreach (var r in animator.GetComponentsInChildren<Renderer>(true))
                if (!r.name.Contains("Weapon") && !r.name.Contains("_Prop_")) body.Add(r);
            _bodyRenderers = body.ToArray();
        }

        // 그 적 클래스에만 있는 속성(Phase, IsTransitioning)을 그 적에게 묶은 대리자로 (없으면 null). 읽을 때 박싱·리플렉션이 없어요
        static Func<T> Getter<T>(EnemyBrain brain, string property)
        {
            var getter = brain.GetType().GetProperty(property)?.GetGetMethod();
            return getter != null && getter.ReturnType == typeof(T) ? (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), brain, getter) : null;
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
            ApplyOverlay();
        }

        void Update()
        {
            if (_brain == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            TickOverlay();
            if (_leaping) TickLeap();
            TickPreLeap();
            if (_arcing) TickArc();
            if (_emergeFade) TickEmerge();
            if (Time.time < _holdUntil) return;   // 한 번 트는 동작(스켈레톤 다시 일어남·보스 견제·도약) 동안은 그 동작만
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
                // 플레이 테스트 3차: 정해 둔 3.2초가 끝나도 닻이 아직 땅에 꽂혀 있으면 손에 다시 보였어요 → 꽂힌 닻(AnchorBrain 기둥)이 남아 있는 동안은 계속 감춰요
                bool planted = _thrownProp == "_Weapon_anchor" && AnchorBrain.PostCount > 0;
                if ((_anchorAway <= 0f && !planted) || _brain.IsDead) { _anchorAway = 0f; SetPropVisible(_thrownProp, true); }
                else if (_anchorAway <= 0f) _anchorAway = 0.05f;
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
            if (_transitioning != null && _transitioning() != _roaring)
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
            if (_phase != null)
            {
                int ph = _phase();
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
                    ApplyOverlay();
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
            // 일반 적 등장: 처음 나타난 순간 웅크렸다 일어서요 (곧바로 공격하면 공격 동작이 이어받아요)
            if (_hasSpawn)
            {
                _hasSpawn = false;
                _animator.SetFloat(AttackSpeedId, 1f);
                Play("Spawn", 0f);
                _oneShotUntil = Time.time + 0.7f;
            }
            // 보스·고수 등장: 깨어나는 순간(방에 들어오면) 위세 있는 동작을 한 번 보여요. 곧바로 공격하면 공격 동작이 이어받아요
            if (_hasIntro && !_introDone && _brain.IsAwake && !_brain.IsDead)
            {
                _introDone = true;
                _animator.SetFloat(AttackSpeedId, 1f);
                Play("Intro", 0.1f);
                _oneShotUntil = Time.time + 1.4f;
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
                ApplyOverlay();
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
            if (_squash > 0f)   // 슬라임 피격: 납작하게 퍼졌다가 통 돌아와요
            {
                _squash -= Time.deltaTime;
                float k = _squash > 0f ? Mathf.Sin(Mathf.PI * (1f - _squash / 0.25f)) : 0f;
                _animator.transform.localScale = Vector3.Scale(_squashBase, new Vector3(1f + 0.28f * k, 1f - 0.32f * k, 1f + 0.28f * k));
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
            float speed = busy || Time.deltaTime <= 0f ? 0f : d.magnitude / Time.deltaTime;
            // 등장 동작은 대기로만 돌아가는 한 번짜리라, 그 사이 걸어 나오면 등장 자세로 미끄러졌어요 → 움직이기 시작하면 달리기로
            if (_oneShotUntil > Time.time && speed > 0.5f) { _oneShotUntil = 0f; _animator.CrossFadeInFixedTime(RunState, 0.15f, 0); }
            _animator.SetFloat(SpeedId, speed);
        }

        int _lastHit;   // 마지막으로 받은 피해 (크게 맞아 쓰러지면 날아가듯 넘어가요)

        void OnDamaged(Damageable body, int amount)
        {
            _lastHit = amount;
            if (_brain == null || _animator == null) return;
            if (_brain.IsDead) { MakeCorpse(); return; }
            if (!body.LastHitReacts) return;            // 화상·번개 같은 파생 피해는 움찔하지 않아요
            if (!_brain.IsStunned) _flinchTimer = flinchTime;   // 경직이면 Update가 비틀거리게 해요
            HitFlavor();
        }

        // 판타지 적 피격 맛: 스켈레톤은 뼛조각 둘, 가고일은 돌 조각 셋이 튀고, 슬라임은 말랑하게 찌그러졌다 돌아와요
        static readonly Color BoneColor = new Color(0.92f, 0.88f, 0.76f), StoneColor = new Color(0.55f, 0.55f, 0.56f), SlimeColor = new Color(0.45f, 0.85f, 0.4f);
        Renderer _bodyRenderer;
        float _squash;
        Vector3 _squashBase;

        Renderer BodyRenderer()
        {
            if (_bodyRenderer == null && _animator != null)
                foreach (var r in _animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!r.name.Contains("_Weapon_") && !r.name.Contains("_Prop_")) { _bodyRenderer = r; break; }
            return _bodyRenderer;
        }

        void HitFlavor()
        {
            var kind = _brain.Kind;
            if (kind != EnemyKind.Skeleton && kind != EnemyKind.Gargoyle && kind != EnemyKind.Slime) return;
            var body = BodyRenderer();
            if (body == null) return;
            var b = body.bounds;
            if (kind == EnemyKind.Slime)
            {
                if (_squash <= 0f) _squashBase = _animator.transform.localScale;
                _squash = 0.25f;
                return;
            }
            bool bone = kind == EnemyKind.Skeleton;
            InkDebris.Burst(b.center + Vector3.up * b.extents.y * 0.2f, transform.position.y, bone ? InkDebris.Shape.Bone : InkDebris.Shape.Stone,
                bone ? BoneColor : StoneColor, body.sharedMaterial, bone ? 2 : 3, b.size.y * 0.045f, 3.5f, 1.6f, 0.9f);
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
            var style = _brain.Kind == EnemyKind.BloodBomber ? InkCorpse.Style.Swell : _brain.Kind == EnemyKind.WaterGhost || _brain.Kind == EnemyKind.Slime ? InkCorpse.Style.Sink : InkCorpse.Style.Fall;   // 슬라임은 녹아 가라앉아요
            // 마지막 동작(Defeat)이 있는 보스: 넘어지지 않고 그 자세로 무너지며 무기를 떨궈요. 천마는 엔딩 내내 남고, 다른 보스는 잠시 뒤 먹으로 흩어져요
            if (_animator.HasState(0, Animator.StringToHash("Defeat")))
            {
                style = _brain.Kind == EnemyKind.HeavenlyDemon ? InkCorpse.Style.Kneel : InkCorpse.Style.Dissolve;
                _animator.SetFloat(AttackSpeedId, 1f);
                Play("Defeat", 0.15f);
            }
            var c = corpse.AddComponent<InkCorpse>();
            if ((style == InkCorpse.Style.Fall || style == InkCorpse.Style.Sink) && FantasyCorpse(c, corpse.transform)) { enabled = false; return; }
            bool varied = style == InkCorpse.Style.Fall && !beast && !forward && _brain.Kind != EnemyKind.Lantern && _brain.Kind != EnemyKind.Wraith;
            if (varied) VariedFall(c, corpse.transform);
            else c.Init(_animator, beast ? Vector3.forward : Vector3.right,
                _brain.Kind == EnemyKind.Snake ? 160f : beast ? 88f : forward ? 80f : -85f, style);
            enabled = false;
        }

        // 사람형 적이 쓰러지는 갈래 (매번 같지 않게). 넘어가는 쪽은 플레이어(다른 적에게 쓰러졌으면 그 적) 반대쪽이 기준이에요
        //  - 크게 맞음(마지막 한 방이 최대 체력의 1/4 이상): 맞은 반대쪽으로 미끄러지며 날아가듯 넘어가요
        //  - 그 밖엔 무작위로: 뒤로 넘어가기 · 앞으로 고꾸라지기(맞은 쪽으로) · 무릎부터 주저앉았다 앞으로 · 옆으로 돌며 쓰러지기
        // 판타지 적 고유 쓰러짐: 스켈레톤은 뼈가 무너져 쌓이며 뼛조각이 흩어지고, 가고일은 돌로 굳어 금이 가다 산산이 부서지고,
        //   슬라임은 납작하게 퍼지며 녹아 꺼지고, 하피·임프는 날갯짓을 멈추고 몸을 비틀며 앞으로 곤두박질쳐요. 오크·늑대인간은 무릎부터 꺾여 앞으로 쓰러져요
        bool FantasyCorpse(InkCorpse c, Transform t)
        {
            var body = BodyRenderer();
            var kind = _brain.Kind;
            switch (kind)
            {
                case EnemyKind.Skeleton:
                    c.Init(_animator, Vector3.right, 0f, InkCorpse.Style.Collapse);
                    break;
                case EnemyKind.Gargoyle:
                    c.Init(_animator, Vector3.right, 0f, InkCorpse.Style.Petrify);
                    break;
                case EnemyKind.Slime:
                    c.Init(_animator, Vector3.right, 0f, InkCorpse.Style.Melt);
                    break;
                case EnemyKind.Harpy:
                case EnemyKind.Imp:
                {
                    foreach (var w in _animator.GetComponentsInChildren<InkImpWings>(true)) w.enabled = false;   // 날갯짓이 멈춰요
                    if (s_player == null) s_player = FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
                    var away = s_player != null ? t.position - s_player.transform.position : -t.forward;
                    away.y = 0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : -t.forward;
                    var axis = Quaternion.Inverse(t.rotation) * Vector3.Cross(Vector3.up, away);
                    float twist = UnityEngine.Random.value < 0.5f ? 110f : -110f;
                    c.Init(_animator, axis, 96f, InkCorpse.Style.Fall, away * 0.7f + Vector3.down * 0.2f * _modelScale.y, twist: twist);
                    break;
                }
                case EnemyKind.Orc:
                case EnemyKind.Werewolf:
                    VariedFall(c, t, 70);   // 무릎부터 털썩 주저앉았다 앞으로
                    break;
                default:
                    return false;
            }
            if (body != null) c.SetDebris(body.sharedMaterial, body.bounds);
            return true;
        }

        static AThousandDawns.Player.PlayerController s_player;
        void VariedFall(InkCorpse corpse, Transform t, int forcePick = -1)
        {
            if (s_player == null) s_player = FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
            // 다른 진영 적에게 쓰러졌으면 그 적 반대쪽으로 (적끼리 싸움)
            var killer = _brain.KilledByRival && _lastRival != null ? _lastRival : s_player != null ? s_player.transform : null;
            var away = killer != null ? t.position - killer.position : -t.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -t.forward;
            float max = _body != null ? _body.MaxHealth : 1f;
            bool heavy = forcePick < 0 && max > 0f && _lastHit >= max * 0.25f;
            int pick = forcePick >= 0 ? forcePick : UnityEngine.Random.Range(0, 100);
            // 위를 dir 쪽으로 넘기는 회전축 (월드 기준을 시체 기준으로 바꿔 넘겨요)
            Vector3 Axis(Vector3 dir) => Quaternion.Inverse(t.rotation) * Vector3.Cross(Vector3.up, dir);
            if (heavy)
                corpse.Init(_animator, Axis(away), 88f, InkCorpse.Style.Fall, away * 1.1f);
            else if (pick < 40)
                corpse.Init(_animator, Axis(away), 85f, InkCorpse.Style.Fall);
            else if (pick < 62)
                corpse.Init(_animator, Axis(-away), 80f, InkCorpse.Style.Fall);
            else if (pick < 84)
            {
                // 무릎부터: 등장 동작의 웅크린 첫 자세(있으면)로 털썩 주저앉았다가 앞으로
                if (_animator.HasState(0, Animator.StringToHash("Spawn"))) _animator.Play("Spawn", 0, 0f);
                corpse.Init(_animator, Axis(-away), 72f, InkCorpse.Style.Fall, Vector3.zero, crumple: true);
            }
            else
            {
                var side = Vector3.Cross(Vector3.up, away) * (pick % 2 == 0 ? 1f : -1f);
                corpse.Init(_animator, Axis(side), 86f, InkCorpse.Style.Fall, Vector3.zero, twist: pick % 2 == 0 ? 50f : -50f);
            }
        }

        // 보스 공통 견제기·도약 (EnemyBrain.BossKit): 견제는 던지기·쏘기 동작 한 번, 도약은 0.9초 웅크렸다가(LeapCrouch)
        //   0.35초 동안 모델만 들어 올려 떠날 자리 → 내려찍을 자리로 포물선을 그리고 (브레인은 착지 순간에 순간이동해요), 착지하며 내려찍어요(LeapLand)
        const float LeapWindup = 0.9f, LeapAir = 0.35f;   // boss_kit.leap_windup · leap_air 기본값과 같아요
        bool _bossKit, _leaping;
        float _leapStart;
        Vector3 _leapFrom, _leapTo, _modelRestPos;

        void OnDemonCue(HeavenlyDemonBrain demon, HeavenlyDemonBrain.FxCue cue)
        {
            if (demon == _brain && cue.Kind == HeavenlyDemonBrain.FxKind.Blink) { _blinkFade = false; _fade = 0f; }   // 나타나는 순간 또렷하게
        }

        void OnBossVolley(EnemyBrain boss, Vector3 dir)
        {
            if (boss == _brain) PlayHeld("Volley", 0.6f);
        }

        // 늑대인간 광폭화: 한 번 울부짖고(Roar) 그 뒤로 몸 가장자리가 붉게 타올라요 · 가고일 석화: 웅크린 석상 자세로 굳었다가(Stone) 풀리면 날개를 펴요(Unstone)
        bool _werewolf, _gargoyle, _enraged, _stoned;
        static readonly Color EnrageGlow = new Color(1f, 0.15f, 0.08f), StoneGlow = new Color(0.55f, 0.55f, 0.55f);

        void OnEnraged(WerewolfBrain wolf)
        {
            if (wolf != _brain) return;
            _enraged = true;
            PlayHeld("Roar", 1f);
        }

        void OnStoneChanged(GargoyleBrain gargoyle, bool stone)
        {
            if (gargoyle != _brain) return;
            _stoned = stone;
            if (stone) { Play("Stone", 0.1f); _holdUntil = float.MaxValue; }
            else { _holdUntil = 0f; PlayHeld("Unstone", 0.7f); }
        }

        // 망령 기병 돌격 예고: 대검을 하늘로 들어 앞을 겨눠 기병을 불러요 (예전 저주 검기 휘두르기 대신)
        void OnCurseWave(DeathKnightBrain knight, Vector3 dir)
        {
            if (knight == _brain) PlayHeld(_animator.HasState(0, Animator.StringToHash("RideCall")) ? "RideCall" : "CurseWave", 1f);
        }

        // 녹림왕 2페이즈 불타는 대나무: 대도를 어깨 위로 감았다 비스듬히 베어 넘기는 손짓
        void OnBambooFall(NokrimBrain nokrim, Vector3 center, Vector3 dir, float length, float width, float fall)
        {
            if (nokrim == _brain && !_moving) PlayHeld("BambooChop", 1f);
        }

        // 철권 마웅 권풍: 허리에 당겼다 정권을 크게 내질러요
        void OnFistWave(IronFistBrain fist, Vector3 dir)
        {
            if (fist == _brain && !_moving) PlayHeld("FistWave", 0.7f);
        }

        // 고블린 족장 덫 던지기: 덫마다 신호가 와서, 이미 던지는 중이면 다시 틀지 않아요
        float _trapUntil;
        void OnTrapThrown(GoblinChiefBrain chief, Vector3 at)
        {
            if (chief != _brain || _moving || Time.time < _trapUntil) return;
            _trapUntil = Time.time + 0.8f;
            PlayHeld("TrapThrow", 0.8f);
        }

        // 먼 거리 도약 (철권 계열: 철권 마웅·고블린 족장·오우거·데스나이트): 게임은 착지 순간 순간이동해요.
        //   모델만 떠난 자리에서 포물선을 그리며 날아와 내려앉게 보여요 (0.3초, 거리만큼 높이)
        const float ArcTime = 0.3f;
        bool _arcing;
        float _arcStart, _arcHeight;
        Vector3 _arcFrom;

        void OnFarLeaped(IronFistBrain boss, Vector3 from, Vector3 to)
        {
            if (boss != _brain) return;
            _arcFrom = from - to;
            _arcFrom.y = 0f;
            _arcHeight = Mathf.Clamp(_arcFrom.magnitude * 0.3f, 1.2f, 3.5f);
            if (_preY < 0f) { _arcFrom = Vector3.up * _preY; _arcHeight = 0f; }            // 어둠 걸음: 새 자리 땅속에서 곧게 솟아올라요
            else if (_preY > 0f) { _arcFrom += Vector3.up * _preY; _arcHeight = 0.6f; }    // 예고 동안 뛰어오른 높이에서 내려앉아요
            _preY = 0f;
            _preActive = false;
            _arcStart = Time.time;
            _arcing = true;
            TickArc();
        }

        void TickArc()
        {
            float k = Mathf.Clamp01((Time.time - _arcStart) / ArcTime);
            if (k >= 1f) { _arcing = false; _animator.transform.localPosition = _modelRestPos; return; }
            var offset = Vector3.Lerp(_arcFrom, Vector3.zero, k) + Vector3.up * (4f * k * (1f - k) * _arcHeight);
            _animator.transform.position = transform.TransformPoint(_modelRestPos) + offset;
        }

        // 예고 동안의 위치: 어둠 걸음은 땅속으로 가라앉고(옛 자리), 점프 덮치기·땅울림 도약은 0.3초 웅크린 뒤 위로 뛰어올라 떠 있어요.
        //   판정 순간 게임이 새 자리로 옮기며 FarLeaped를 보내면 위 OnFarLeaped가 그 높이에서 이어받아요. 신호가 안 오면(기절 등) 제자리로
        bool _preActive;
        float _preStart, _preY;

        void TickPreLeap()
        {
            string attack = _brain.AttackName;
            bool sink = attack == "어둠 걸음", jump = attack == "점프 덮치기" || attack == "땅울림 도약";
            bool windup = (sink || jump) && _brain.AttackPhase == EnemyAttackTimeline.Phase.Windup && !_brain.IsDead;
            if (windup)
            {
                if (!_preActive) { _preActive = true; _preStart = Time.time; }
                float t = Time.time - _preStart;
                float scale = Mathf.Max(0.5f, _modelScale.y);
                _preY = sink ? -Mathf.Min(1.9f * scale, Mathf.Max(0f, t - 0.15f) * 2.4f * scale)
                             : t < 0.3f ? 0f : (attack == "땅울림 도약" ? 1.4f : 1.8f) * scale * (1f - Mathf.Exp(-(t - 0.3f) * 4f));
                _animator.transform.position = transform.TransformPoint(_modelRestPos) + Vector3.up * _preY;
            }
            else if (_preActive && !_arcing && _brain.AttackPhase != EnemyAttackTimeline.Phase.Active)
            {
                _preActive = false;
                _preY = 0f;
                _animator.transform.localPosition = _modelRestPos;
            }
        }

        // 잡아 던지기: 붙잡힌 순간 들어 올려 내던지는 뒷부분부터
        void OnGrabbed(IronFistBrain boss, Vector3 at, Vector3 dir)
        {
            if (boss != _brain || !_animator.HasState(0, Animator.StringToHash("GrabThrow"))) return;
            _animator.Play("GrabThrow", 0, 0.3f);
            _holdUntil = Time.time + 0.7f;
        }

        // 데스나이트 죽은 자의 부름 (2페이즈): 대검을 높이 들어 부르는 손짓
        void OnKnightRaise(DeathKnightBrain knight)
        {
            if (knight == _brain) PlayHeld("RideCall", 1.2f);
        }

        // 다크 엘프 쌍검 난무: 베기 신호마다 그쪽 칼이 나가는 자세로 맞춰요 (left = 왼 칼)
        void OnDanceCut(DarkElfBrain elf, Vector3 at, Vector3 dir, bool left)
        {
            if (elf != _brain || !_animator.HasState(0, Animator.StringToHash("Dance5"))) return;
            _animator.Play("Dance5", 0, left ? 0.15f : 0.26f);
            _holdUntil = Time.time + 0.2f;
        }

        // 다크 엘프 그림자 숨기: 게임이 몸을 곧장 감추니, 그 자리에 지금 자세의 잔상을 남겨 먹으로 흩어지게 해요
        void OnVanished(DarkElfBrain elf, Vector3 at)
        {
            if (elf != _brain) return;
            foreach (var sr in _animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!sr.enabled) continue;
                var mesh = new Mesh();
                sr.BakeMesh(mesh, true);
                var go = new GameObject("Shadow Afterimage");
                go.transform.SetPositionAndRotation(sr.transform.position, sr.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = sr.sharedMaterials;
                go.AddComponent<InkAfterimage>();
            }
        }

        // 데스나이트 망령 기병이 줄마다 지나가는 동안: 대검을 들어 겨눈 자세를 이어 버텨요
        void OnSpectralRide(DeathKnightBrain knight, Vector3 from, Vector3 to)
        {
            if (knight == _brain && Time.time < _holdUntil + 0.2f) _holdUntil = Mathf.Max(_holdUntil, Time.time + 0.5f);
        }

        // 다크 엘프 그림자 기습: 옆에 나타나는 순간 몸이 흐릿했다가 또렷해져요 (사라질 땐 게임이 몸을 통째로 감춰요)
        bool _emergeFade;

        void OnEmerged(DarkElfBrain elf, Vector3 at)
        {
            if (elf != _brain) return;
            _fadeUsed = true;
            _fade = 0.75f;
            _emergeFade = true;
            ApplyOverlay();
        }

        void TickEmerge()
        {
            _fade = Mathf.MoveTowards(_fade, 0f, Time.deltaTime * 2.5f);
            ApplyOverlay();
            if (_fade <= 0f) _emergeFade = false;
        }

        // 오우거 분노 (2페이즈): 가슴을 치며 포효
        void OnOgreEnraged(OgreBrain ogre)
        {
            if (ogre == _brain) PlayHeld("Roar", 1.3f);
        }

        void OnRockThrown(OgreBrain ogre)
        {
            if (ogre == _brain) PlayHeld("Volley", 0.6f);
        }

        void OnLeapStarted(EnemyBrain boss, Vector3 from, Vector3 to)
        {
            if (boss != _brain) return;
            _leaping = true;
            _leapStart = Time.time;
            _leapFrom = from;
            _leapTo = to;
            PlayHeld("LeapCrouch", LeapWindup + LeapAir + 0.1f);
        }

        void OnLeapLanded(EnemyBrain boss, Vector3 at)
        {
            if (boss != _brain) return;
            _leaping = false;
            _animator.transform.localPosition = _modelRestPos;
            PlayHeld("LeapLand", 0.5f);
        }

        void TickLeap()
        {
            float t = Time.time - _leapStart - LeapWindup;
            if (t < 0f) return;
            float k = Mathf.Clamp01(t / LeapAir);
            var offset = Vector3.Lerp(Vector3.zero, _leapTo - _leapFrom, k);
            offset.y = 0f;
            float height = 4f * k * (1f - k) * Mathf.Clamp(Vector3.Distance(_leapFrom, _leapTo) * 0.35f, 1.5f, 3.5f);
            _animator.transform.position = transform.TransformPoint(_modelRestPos) + offset + Vector3.up * height;
        }

        void PlayHeld(string state, float seconds)
        {
            if (!_animator.HasState(0, Animator.StringToHash(state))) return;
            Play(state, 0.08f);
            _holdUntil = Time.time + seconds;
        }

        // 스켈레톤 뼈 무너짐 → 1.5초 끝에 다시 맞춰져 일어나요 (Reassemble 클립이 그 길이에 맞춰져 있어요)
        bool _skeleton;
        float _holdUntil;

        void OnReassembling(SkeletonBrain skeleton)
        {
            if (skeleton != _brain) return;
            Play("Reassemble", 0.05f);
            _holdUntil = Time.time + 1.5f;
        }

        void OnDestroy()
        {
            if (_body != null) _body.Damaged -= OnDamaged;
            if (_skeleton) SkeletonBrain.Reassembling -= OnReassembling;
            if (_werewolf) WerewolfBrain.Enraged -= OnEnraged;
            if (_gargoyle) GargoyleBrain.StoneChanged -= OnStoneChanged;
            if (_demon != null) HeavenlyDemonBrain.Cue -= OnDemonCue;
            if (_bossKit)
            {
                EnemyBrain.BossVolley -= OnBossVolley;
                EnemyBrain.BossLeapStarted -= OnLeapStarted;
                EnemyBrain.BossLeapLanded -= OnLeapLanded;
                OgreBrain.RockThrown -= OnRockThrown;
                DeathKnightBrain.CurseWave -= OnCurseWave;
                NokrimBrain.BambooFall -= OnBambooFall;
                IronFistBrain.FistWave -= OnFistWave;
                GoblinChiefBrain.TrapThrown -= OnTrapThrown;
                OgreBrain.Enraged -= OnOgreEnraged;
                IronFistBrain.FarLeaped -= OnFarLeaped;
                DeathKnightBrain.SpectralRide -= OnSpectralRide;
                DarkElfBrain.Emerged -= OnEmerged;
                DarkElfBrain.Vanished -= OnVanished;
                DarkElfBrain.DanceCut -= OnDanceCut;
                IronFistBrain.Grabbed -= OnGrabbed;
                DeathKnightBrain.Raise -= OnKnightRaise;
            }
        }
    }

    // 쓰러진 적: 발을 축으로 넘어가(점점 빨리) 살짝 튕긴 뒤, 잠시 누워 있다가 땅속으로 가라앉아 사라져요.
    //   Swell(혈폭자): 넘어가지 않고 1.2초 동안 부들부들 떨며 부풀어 오르다가 터지는 순간 사라져요 (터짐은 게임·이펙트)
    //   Sink(수귀): 선 채로 물 밑으로 꺼지듯 빠르게 가라앉아요
    public class InkCorpse : MonoBehaviour
    {
        public enum Style { Fall, Swell, Sink, Kneel, Dissolve, Collapse, Petrify, Melt }
        //   Collapse(스켈레톤): 뼈가 무너지는 동작(Reassemble 앞부분)으로 주저앉아 쌓이며 뼛조각이 사방으로 흩어지고, 무기를 떨군 채 잠시 뒤 가라앉아요
        //   Petrify(가고일): 돌 자세(Stone)로 웅크리며 잿빛으로 굳고 금이 번지다가 산산이 부서져 돌 조각만 흩어져요
        //   Melt(슬라임): 납작하게 퍼지며 출렁이다 녹아 바닥으로 꺼지고, 방울이 튀어요
        Material _debrisMat;
        Bounds _debrisBounds;
        bool _burst;
        static readonly int CrackId = Shader.PropertyToID("_CrackColor"), AuraId = Shader.PropertyToID("_AuraColor"), TintId = Shader.PropertyToID("_BaseColor");

        public void SetDebris(Material mat, Bounds bounds) { _debrisMat = mat; _debrisBounds = bounds; }

        // 방을 옮기면 (방은 같은 자리를 다시 써요) 앞 방 시체가 새 방에서 쓰러지지 않게 바로 치워요
        void OnEnable() => EnemyDirector.RoomReset += OnRoomReset;
        void OnDisable() => EnemyDirector.RoomReset -= OnRoomReset;
        void OnRoomReset() => Destroy(gameObject);
        //   Dissolve(보스): Kneel처럼 마지막 자세로 무너져 무기를 떨군 뒤, 잠시 있다가 발밑부터 먹으로 흩어져 사라져요 (InkCharacter _Dissolve)
        const float DissolveHold = 1.8f, DissolveTime = 1.6f;
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        MaterialPropertyBlock _dissolveBlock;
        Renderer[] _dissolveRenderers;
        bool _dissolveStarted;
        // 보스가 먹으로 흩어지기 시작할 때 (자리, 흩어지는 시간). 아트 2 먹 흩날림 이펙트용
        public static event System.Action<Vector3, float> BossDissolve;
        const float FallTime = 0.4f, LieTime = 1.2f, SinkTime = 1.4f, SinkDepth = 0.8f;
        const float SwellScale = 1.4f, GhostSinkTime = 0.7f, GhostSinkDepth = 2.4f;
        float _swellTime = 1.2f;   // 혈폭자 터지기까지 (bloodbomber.blast_fuse)
        Animator _animator;
        Vector3 _axis;
        float _angle, _t;
        Quaternion _start;
        Vector3 _startPos, _startScale;
        Style _style;

        // slide: 넘어가는 동안 미끄러지는 거리(월드, 날아가듯) · crumple: 웅크린 채 잠깐 버티다 넘어가요 · twist: 넘어가며 몸을 이만큼 비틀어요(도)
        Vector3 _slide;
        float _delay, _twist;

        public void Init(Animator animator, Vector3 localAxis, float angle, Style style = Style.Fall, Vector3 slide = default, bool crumple = false, float twist = 0f)
        {
            _animator = animator;
            _axis = localAxis;
            _angle = angle;
            _style = style;
            _slide = slide;
            _delay = crumple ? 0.22f : 0f;
            _twist = twist;
            _start = transform.rotation;
            _startPos = transform.position;
            _startScale = transform.localScale;
            if (style == Style.Swell) _swellTime = Mathf.Max(0.1f, AThousandDawns.Core.GameConfig.Get("bloodbomber.blast_fuse", 1.2f));
            if (_animator != null)
            {
                _animator.SetFloat(Animator.StringToHash("Speed"), 0f);
                _animator.speed = style == Style.Fall ? (crumple ? 0f : 0.3f) : style == Style.Kneel || style == Style.Dissolve ? 1f : 0f;   // 넘어가는 동안 굳어 가요 (마지막 동작은 끝까지, 주저앉기는 웅크린 자세 그대로)
            }
            if ((style == Style.Kneel || style == Style.Dissolve) && _animator != null) DropWeapon();
            if (_animator != null && (style == Style.Collapse || style == Style.Petrify))
            {
                string state = style == Style.Collapse ? "Reassemble" : "Stone";
                if (_animator.HasState(0, Animator.StringToHash(state))) { _animator.speed = 1f; _animator.Play(state, 0, 0f); }
                if (style == Style.Collapse) DropWeapon();
            }
        }

        // 손의 무기를 그 자세 그대로 떼어 내 바닥으로 떨어뜨려요 (스킨 메시를 지금 모양으로 구워서)
        Transform _dropped;
        Vector3 _dropFrom;
        Quaternion _dropRot;
        float _dropDown;

        void DropWeapon()
        {
            foreach (var sr in _animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!sr.name.Contains("_Weapon_")) continue;
                var mesh = new Mesh();
                sr.BakeMesh(mesh, true);
                var go = new GameObject("Dropped Weapon");
                go.transform.SetPositionAndRotation(sr.transform.position, sr.transform.rotation);
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = sr.sharedMaterials;
                go.transform.SetParent(transform, true);
                sr.enabled = false;
                _dropped = go.transform;
                _dropFrom = go.transform.position;
                _dropRot = go.transform.rotation;
                _dropDown = Mathf.Max(0f, go.GetComponent<MeshRenderer>().bounds.min.y - transform.position.y - 0.02f);
                break;
            }
        }
        void Update()
        {
            _t += Time.deltaTime;
            if (_style == Style.Collapse || _style == Style.Petrify || _style == Style.Melt) { TickFantasy(); return; }
            if (_style == Style.Dissolve)
            {
                if (_dropped != null)   // 무기가 기울며 툭 떨어져 바닥에 누워요
                {
                    float k = Mathf.Clamp01(_t / 0.35f);
                    _dropped.position = _dropFrom + Vector3.down * _dropDown * k * k;
                    _dropped.rotation = Quaternion.Slerp(_dropRot, Quaternion.Euler(0f, _dropRot.eulerAngles.y, 0f) * Quaternion.Euler(0f, 0f, 90f), k);
                }
                float d = Mathf.Clamp01((_t - DissolveHold) / DissolveTime);
                if (d <= 0f) return;
                if (!_dissolveStarted)
                {
                    _dissolveStarted = true;
                    if (_animator != null) _animator.speed = 0f;   // 흩어지는 동안 자세는 굳어요
                    _dissolveRenderers = GetComponentsInChildren<Renderer>(true);
                    _dissolveBlock = new MaterialPropertyBlock();
                    BossDissolve?.Invoke(transform.position, DissolveTime);
                }
                float amount = d * d * (3f - 2f * d);
                foreach (var r in _dissolveRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_dissolveBlock);
                    _dissolveBlock.SetFloat(DissolveId, amount);
                    r.SetPropertyBlock(_dissolveBlock);
                }
                if (d >= 1f) Destroy(gameObject);
                return;
            }
            if (_style == Style.Kneel)
            {
                if (_dropped != null)   // 칼이 기울며 툭 떨어져 바닥에 누워요
                {
                    float k = Mathf.Clamp01(_t / 0.35f);
                    _dropped.position = _dropFrom + Vector3.down * _dropDown * k * k;
                    _dropped.rotation = Quaternion.Slerp(_dropRot, Quaternion.Euler(0f, _dropRot.eulerAngles.y, 0f) * Quaternion.Euler(0f, 0f, 90f), k);
                }
                return;   // 사라지지 않고 엔딩 내내 남아요
            }
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
            float ft = Mathf.Max(0f, _t - _delay);         // 주저앉기는 웅크린 채 잠깐 버틴 뒤 넘어가요
            float fall = Mathf.Clamp01(ft / FallTime);
            float a = fall * fall;                          // 점점 빨리 넘어가요
            if (fall >= 1f)
            {
                float b = ft - FallTime;                    // 바닥에 닿고 살짝 튕겨요
                a = 1f - 0.08f * Mathf.Max(0f, Mathf.Sin(b * 18f)) * Mathf.Exp(-b * 8f);
                if (_animator != null) _animator.speed = 0f;
            }
            // 옆으로 쓰러질 땐 넘어가며 몸을 비틀어요 (발밑 축으로 돌고 나서 넘어가요)
            var twist = Quaternion.AngleAxis(_twist * Mathf.SmoothStep(0f, 1f, fall), Vector3.up);
            transform.rotation = twist * _start * Quaternion.AngleAxis(_angle * a, _axis);
            // 날아가듯: 넘어가는 동안 맞은 반대쪽으로 미끄러지다 바닥에 닿으며 멈춰요
            float slide = 1f - (1f - Mathf.Clamp01(ft / (FallTime * 1.3f))) * (1f - Mathf.Clamp01(ft / (FallTime * 1.3f)));
            float sink = Mathf.Clamp01((ft - FallTime - LieTime) / SinkTime);
            transform.position = _startPos + _slide * slide + Vector3.down * SinkDepth * sink * sink;
            if (sink >= 1f) Destroy(gameObject);
        }

        void TickFantasy()
        {
            if (_dropped != null)   // 떨군 무기가 기울며 바닥에 누워요
            {
                float k = Mathf.Clamp01(_t / 0.35f);
                _dropped.position = _dropFrom + Vector3.down * _dropDown * k * k;
                _dropped.rotation = Quaternion.Slerp(_dropRot, Quaternion.Euler(0f, _dropRot.eulerAngles.y, 0f) * Quaternion.Euler(0f, 0f, 90f), k);
            }
            var b = _debrisBounds;
            float size = Mathf.Max(0.3f, b.size.y);
            if (_style == Style.Collapse)
            {
                if (_animator != null && _t >= 0.3f) _animator.speed = 0f;   // 뼈 무더기 자세에서 멈춰요
                if (!_burst && _t >= 0.12f)
                {
                    _burst = true;
                    InkDebris.Burst(b.center, _startPos.y, InkDebris.Shape.Bone, new Color(0.92f, 0.88f, 0.76f), _debrisMat, 9, size * 0.06f, 4f, 2.6f, 2.2f);
                }
                float sink = Mathf.Clamp01((_t - 0.3f - LieTime - 0.4f) / SinkTime);
                transform.position = _startPos + Vector3.down * SinkDepth * sink * sink;
                if (sink >= 1f) Destroy(gameObject);
                return;
            }
            if (_style == Style.Petrify)
            {
                float k = Mathf.Clamp01(_t / 0.7f);   // 잿빛으로 굳으며 금이 번져요
                if (_dissolveRenderers == null) { _dissolveRenderers = GetComponentsInChildren<Renderer>(true); _dissolveBlock = new MaterialPropertyBlock(); }
                if (!_burst)
                {
                    foreach (var r in _dissolveRenderers)
                    {
                        if (r == null) continue;
                        r.GetPropertyBlock(_dissolveBlock);
                        // 채색 셰이더엔 채도가 없어서: 몸을 밝혀 옅은 돌빛으로, 가장자리는 잿빛, 금은 밝은 돌빛 둘레에 짙은 선
                        _dissolveBlock.SetColor(CrackId, new Color(0.85f, 0.84f, 0.82f, k));
                        _dissolveBlock.SetColor(AuraId, new Color(0.78f, 0.78f, 0.8f, 0.9f * k));
                        _dissolveBlock.SetColor(TintId, Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.65f), k));
                        r.SetPropertyBlock(_dissolveBlock);
                    }
                    if (_animator != null && _t >= 0.35f) _animator.speed = 0f;
                    // 부서지기 직전 부르르 떨어요
                    float shake = _t > 0.55f ? 0.03f : 0f;
                    transform.position = _startPos + new Vector3(Mathf.Sin(_t * 80f), 0f, Mathf.Cos(_t * 67f)) * shake;
                }
                if (!_burst && _t >= 0.85f)
                {
                    _burst = true;
                    foreach (var r in _dissolveRenderers) if (r != null) r.enabled = false;
                    InkDebris.Burst(b.center, _startPos.y, InkDebris.Shape.Stone, new Color(0.55f, 0.55f, 0.56f), _debrisMat, 14, size * 0.08f, 5f, 3.2f, 2.2f);
                    InkDebris.Burst(b.center + Vector3.up * b.extents.y * 0.3f, _startPos.y, InkDebris.Shape.Stone, new Color(0.42f, 0.42f, 0.44f), _debrisMat, 8, size * 0.05f, 3f, 4f, 1.6f);
                }
                if (_t >= 1f) Destroy(gameObject);
                return;
            }
            // Melt: 납작하게 퍼지며 출렁이다 녹아 꺼져요
            float m = Mathf.Clamp01(_t / 0.8f);
            float wob = Mathf.Sin(_t * 18f) * 0.08f * (1f - m);
            transform.localScale = Vector3.Scale(_startScale, new Vector3(1f + 0.7f * m + wob, Mathf.Lerp(1f, 0.12f, m * m) - wob, 1f + 0.7f * m + wob));
            if (!_burst && _t >= 0.05f)
            {
                _burst = true;
                InkDebris.Burst(b.center, _startPos.y, InkDebris.Shape.Drop, new Color(0.45f, 0.85f, 0.4f), _debrisMat, 7, size * 0.12f, 3f, 2.2f, 1.2f);
            }
            float melt = Mathf.Clamp01((_t - 0.8f) / 1.2f);
            transform.position = _startPos + Vector3.down * 0.25f * melt;
            if (melt >= 1f) Destroy(gameObject);
        }
    }
}

namespace AThousandDawns.Art
{
    // 잔상: 구운 메시가 0.45초 동안 발밑부터 먹으로 흩어지며 사라져요 (InkCharacter _Dissolve)
    public class InkAfterimage : MonoBehaviour
    {
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        MaterialPropertyBlock _block;
        Renderer _renderer;
        float _t;

        void Awake() { _renderer = GetComponent<Renderer>(); _block = new MaterialPropertyBlock(); }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / 0.45f);
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(DissolveId, k);
            _renderer.SetPropertyBlock(_block);
            if (k >= 1f)
            {
                var mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                Destroy(gameObject);
            }
        }
    }
}
