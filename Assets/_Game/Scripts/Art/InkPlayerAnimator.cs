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

            bool dashing = _player.IsDashing;
            if (dashing && !_wasDashing)
            {
                _animator.ResetTrigger(AttackId);
                _animator.SetTrigger(DashId);
            }
            _wasDashing = dashing;

            bool casting = _player.IsCastingSkill;
            if (casting && !_wasCasting && _player.Skill == SkillKind.Spin)
            {
                _animator.ResetTrigger(AttackId);
                _animator.SetTrigger(SkillId);
            }
            if (casting && !_wasCasting && _player.Skill == SkillKind.Quake)   // 붕산권: 땅 내려치기
            {
                _animator.ResetTrigger(AttackId);
                _animator.SetTrigger(SkillQuakeId);
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
