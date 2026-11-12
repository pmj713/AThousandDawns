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
        int _vortexLayer = -1;
        float _vortexWeight;
        static readonly int DaggerFlashId = Animator.StringToHash("DaggerFlash");
        string _upperState = "SkillVortex";
        // 비도 순간이동·호신륜이 도는 중인지 (기획·개발이 PlayerController.Dagger.cs에 추가 중이라 이름으로 찾아요 — 들어오기 전에도 컴파일되게)
        static readonly System.Reflection.PropertyInfo IsDaggerFlashingProp = typeof(AThousandDawns.Player.PlayerController).GetProperty("IsDaggerFlashing");
        static readonly System.Reflection.PropertyInfo IsHaloingProp = typeof(AThousandDawns.Player.PlayerController).GetProperty("IsHaloing");
        bool DaggerState(System.Reflection.PropertyInfo prop) => prop != null && (bool)prop.GetValue(_player);

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
            _animator.SetBool(DaggerFlashId, daggerFlashSkill && (casting || DaggerState(IsDaggerFlashingProp)));
            // 비도가 도는 동안 윗몸은 손으로 이끄는 자세 (다리는 걷기·대기 그대로): 탁류 소용돌이 · 비도 호신륜
            if (_vortexLayer < 0) _vortexLayer = _animator.GetLayerIndex("Vortex");
            if (_vortexLayer > 0)
            {
                bool on = (vortexSkill && (casting || _player.IsVortexing)) || (haloSkill && (casting || DaggerState(IsHaloingProp)));
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

        // 0 = 1타(세로) · 1 = 2타(가로) · 2 = 3타(넓은 가로). 검보 조각의 4타째는 1타 동작을 다시 써요
        void OnSwingStarted(int index)
        {
            _animator.ResetTrigger(DashId);
            _animator.SetInteger(ComboId, index % 3 + 1);
            _animator.SetTrigger(AttackId);
        }
    }
}
