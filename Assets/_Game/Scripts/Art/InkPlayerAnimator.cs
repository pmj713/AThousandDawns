using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 게임 플레이어(PlayerController)의 움직임을 리깅 모델 애니메이터에 이어 줘요. 게임 코드는 건드리지 않아요.
    //   평타: 공격 박자 관리자(ComboTimeline)가 n타를 시작할 때 → 그 타의 베기 동작 (판정 부채꼴은 게임이 그대로 띄워요)
    //   대시: IsDashing이 켜지는 순간 → 대시 동작
    //   초식: 회선참(Spin)을 시작하는 순간 → 칼을 옆으로 뻗는 동작 (몸은 게임이 한 바퀴 돌려요)
    //   이동: 실제로 움직인 속도 → 달리기/대기
    // 바라보는 방향은 PlayerController가 Visual을 돌려서, 그 아래 모델도 같이 돌아요.
    [RequireComponent(typeof(PlayerController))]
    public class InkPlayerAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ComboId = Animator.StringToHash("Combo");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int DashId = Animator.StringToHash("Dash");
        static readonly int SkillId = Animator.StringToHash("Skill");
        static readonly int SkillQuakeId = Animator.StringToHash("SkillQuake");
        static readonly int SkillFlashId = Animator.StringToHash("SkillFlash");
        static readonly int SkillPalmId = Animator.StringToHash("SkillPalm");
        static readonly int SkillWaveId = Animator.StringToHash("SkillWave");
        static readonly int SkillDashId = Animator.StringToHash("SkillDash");
        static readonly int HurtId = Animator.StringToHash("Hurt");
        static readonly int DeadId = Animator.StringToHash("Dead");
        float _lastHealth = -1f;
        static readonly int CombatId = Animator.StringToHash("Combat");
        static readonly int DaggerHoldId = Animator.StringToHash("DaggerHold");
        const float CombatRange = 9f;   // 이 안에 살아 있는 적이 있으면 전투 대기 자세
        float _combatScan;
        AThousandDawns.Enemies.EnemyDirector _director;
        bool _combat;
        int _vortexLayer = -1;
        float _vortexWeight;
        static readonly int DaggerFlashId = Animator.StringToHash("DaggerFlash");
        static readonly int ArmedId = Animator.StringToHash("Armed");
        // 칼 넣기 (사용자 요청): 이동·대기 중엔 칼을 허리에 꽂아(Player_SwordBelt) 빈손, 평타·칼 초식을 시작하면 손에 쥐어요(Player_Sword).
        //   마지막 공격·초식이 끝나고 sword.sheathe_delay초(시트, 기본 1초) 뒤 허리로. 넣고 빼는 동작은 따로 없이 바꾸고 자세만 0.15초에 섞어요
        float _armedUntil = -1f, _armedBlend;
        string _upperState = "SkillVortex";
        // 비도 순간이동·호신륜이 도는 중인지 (기획·개발이 PlayerController.Dagger.cs에 추가 중이라 이름으로 찾아요 — 들어오기 전에도 컴파일되게)

        PlayerController _player;
        Animator _animator;
        ComboTimeline _combo;
        bool _wasDashing;
        bool _wasCasting;
        Vector3 _lastPosition;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;
        }

        // 비도를 들면 칼을 숨겨요 (플레이테스트 6차: 비도 무기인데 칼이 그대로 보였어요) — 손 칼·허리 칼 둘 다. 칼은 처음 한 번만 찾아요
        Renderer _sword, _beltSword;
        bool _swordSearched;
        int _swordShown = -1;   // 0 = 둘 다 숨김 · 1 = 허리 · 2 = 손

        void ShowSword(bool dagger, bool armed)
        {
            if (!_swordSearched)
            {
                _swordSearched = true;
                foreach (var r in _animator.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "Player_Sword") _sword = r;
                    else if (r.name == "Player_SwordBelt") _beltSword = r;
                }
            }
            if (_beltSword == null) armed = true;   // 허리 칼이 없는 예전 모델은 늘 손에 쥐어요
            int show = dagger ? 0 : armed ? 2 : 1;
            if (show == _swordShown) return;
            _swordShown = show;
            if (_sword != null) _sword.enabled = show == 2;
            if (_beltSword != null) _beltSword.enabled = show == 1;
        }

        // 칼을 쓰는 초식 (비도 아닐 때): 회선참 · 죽림섬 · 탁랑참 · 혈영참. 붕산권(주먹)·금강장(손바닥)은 빈손 그대로
        static bool SwordSkill(SkillKind k) => k == SkillKind.Spin || k == SkillKind.BambooFlash || k == SkillKind.Takrang || k == SkillKind.BloodShadow;

        void Arm() => _armedUntil = Time.time + AThousandDawns.Core.GameConfig.GetOptional("sword.sheathe_delay", 1f);

        void OnDestroy()
        {
            if (_combo != null) _combo.SwingStarted -= OnSwingStarted;
        }

        void Update()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            // 공격 박자 관리자는 PlayerController가 시작할 때 만들어서, 생긴 뒤에 이어 붙여요
            if (_combo == null && _player.Combo != null)
            {
                _combo = _player.Combo;
                _combo.SwingStarted += OnSwingStarted;
            }

            // 피격·쓰러짐: 체력이 줄며 경직에 걸리면 뒤로 젖혀지는 움찔, 체력이 다하면 무릎 꿇고 무너져요
            float health = _player.Health;
            if (_lastHealth >= 0f && health < _lastHealth && !_player.IsDead && _player.IsStunned)
            {
                _animator.ResetTrigger(AttackId);
                _animator.SetTrigger(HurtId);
            }
            _lastHealth = health;
            _animator.SetBool(DeadId, _player.IsDead);
            // 전투 대기: 0.25초마다 둘레의 살아 있는(깨어난) 적을 살펴 자세를 바꿔요
            _combatScan -= Time.deltaTime;
            if (_combatScan <= 0f)
            {
                _combatScan = 0.25f;
                _combat = false;
                if (_director == null) _director = FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();   // 씬을 뒤지지 않고 감독의 적 목록으로
                if (_director != null)
                foreach (var e in _director.Enemies)
                {
                    if (e == null || e.IsDead || !e.IsAwake) continue;
                    var d = e.transform.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude <= CombatRange * CombatRange) { _combat = true; break; }
                }
            }
            _animator.SetBool(CombatId, _combat);
            _animator.SetBool(DaggerHoldId, _player.UsesDagger);
            // 칼 쥐기: 평타(칼)·칼 초식이 이어지는 동안 계속 쥐고, 끝나고 잠시 뒤 허리로
            if (!_player.UsesDagger && ((_combo != null && _combo.IsAttacking) || (_player.IsCastingSkill && SwordSkill(_player.Skill)))) Arm();
            bool armed = Time.time < _armedUntil;
            ShowSword(_player.UsesDagger, armed);
            _armedBlend = Mathf.MoveTowards(_armedBlend, armed || _player.UsesDagger ? 1f : 0f, Time.deltaTime / 0.15f);
            _animator.SetFloat(ArmedId, _armedBlend);
            bool dashing = _player.IsDashing;
            if (dashing && !_wasDashing)
            {
                _animator.ResetTrigger(AttackId);
                _animator.SetTrigger(DashId);
            }
            _wasDashing = dashing;

            bool casting = _player.IsCastingSkill;
            bool dagger = _player.UsesDagger;
            var skill = _player.Skill;
            // 비도 초식은 비급마다 달라요: 회선참 = 회수 · 죽림섬 = 비도 순간이동 · 붕산권 = 비도 솟구치기(땅 내리치기)
            //   · 금강장 = 비도 호신륜(윗몸 층) · 탁랑참 = 탁류 소용돌이(윗몸 층)
            bool vortexSkill = dagger && skill == SkillKind.Takrang;
            bool haloSkill = dagger && skill == SkillKind.Palm;
            bool daggerFlashSkill = dagger && skill == SkillKind.BambooFlash;
            if (casting && !_wasCasting)
            {
                int trigger = 0;
                if (dagger)
                {
                    if (skill == SkillKind.Quake) trigger = SkillQuakeId;                                   // 비도 솟구치기: 땅을 내리쳐요
                    else if (!vortexSkill && !haloSkill && !daggerFlashSkill) trigger = SkillId;            // 회수 (회선참과 같은 동작)
                }
                else if (skill == SkillKind.Spin) trigger = SkillId;
                else if (skill == SkillKind.Quake) trigger = SkillQuakeId;            // 붕산권: 땅 내려치기
                else if (skill == SkillKind.BambooFlash) trigger = SkillFlashId;      // 죽림섬: 좌우로 번갈아 베어 넘기기
                else if (skill == SkillKind.Palm) trigger = SkillPalmId;              // 금강장: 왼손바닥 내뻗기
                else if (skill == SkillKind.Takrang) trigger = SkillWaveId;           // 탁랑참: 머리 위에서 앞으로 길게 내베기
                else if (skill == SkillKind.BloodShadow) trigger = SkillDashId;       // 혈영참: 앞으로 쏜살같이 지나가며 베기
                if (trigger != 0)
                {
                    _animator.ResetTrigger(AttackId);
                    _animator.SetTrigger(trigger);
                }
            }
            // 비도 순간이동: 순간이동이 이어지는 동안 좌우로 번갈아 베어 넘겨요
            _animator.SetBool(DaggerFlashId, daggerFlashSkill && (casting || _player.IsDaggerFlashing));
            // 비도가 도는 동안 윗몸은 손으로 이끄는 자세 (다리는 걷기·대기 그대로): 탁류 소용돌이 · 비도 호신륜
            if (_vortexLayer < 0) _vortexLayer = _animator.GetLayerIndex("Vortex");
            if (_vortexLayer > 0)
            {
                bool on = (vortexSkill && (casting || _player.IsVortexing)) || (haloSkill && (casting || _player.IsHaloing));
                string state = haloSkill ? "SkillHalo" : "SkillVortex";
                if (on && state != _upperState)
                {
                    _upperState = state;
                    _animator.CrossFadeInFixedTime(state, 0.1f, _vortexLayer);
                }
                _vortexWeight = Mathf.MoveTowards(_vortexWeight, on ? 1f : 0f, Time.deltaTime / (on ? 0.1f : 0.2f));
                _animator.SetLayerWeight(_vortexLayer, _vortexWeight);
            }
            _wasCasting = casting;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            bool busy = dashing || casting || (_combo != null && _combo.IsAttacking);
            float speed = busy || Time.deltaTime <= 0f ? 0f : delta.magnitude / Time.deltaTime;
            _animator.SetFloat(SpeedId, speed);
        }

        // 0 = 1타(세로) · 1 = 2타(가로) · 2 = 3타(넓은 가로). 검보 조각의 4타째는 가로베기(2타 동작)로 (플레이 테스트 3차: 세로베기가 어색했어요)
        void OnSwingStarted(int index)
        {
            if (!_player.UsesDagger) { Arm(); ShowSword(false, true); }   // 첫 베기 프레임부터 손에 칼
            _animator.ResetTrigger(DashId);
            _animator.SetInteger(ComboId, index == 3 ? 2 : index % 3 + 1);
            _animator.SetTrigger(AttackId);
        }
    }
}
