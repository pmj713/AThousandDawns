using System;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 적이 받는 상태이상 (상태이상 탭): 화상 · 둔화 → 빙결 · 감전.
    // 공통: 새 중첩이 붙으면 모든 중첩의 지속 시간이 처음으로 돌아가요. 상태이상끼리는 서로 지우지 않아요.
    // 화상은 파생 피해 (강인함 무시, 방어력 적용, 경직 없음). 소수점은 모아 뒀다가 정수가 되면 들어가요.
    [RequireComponent(typeof(Damageable))]
    public sealed class StatusEffects : MonoBehaviour
    {
        static readonly Color FrozenTint = new Color(0.66f, 0.85f, 0.94f);

        [SerializeField] bool isBoss; // 고수·보스: 둔화 효과 50%, 빙결 1초 + 면역 10초

        Damageable _body;
        int _burnMax, _slowMax, _freezeAtSlow, _shockMax;
        float _burnDuration, _burnTick, _slowPerStack, _slowDuration, _slowBossRatio;
        float _freezeNormal, _freezeNormalImmunity, _freezeBoss, _freezeBossImmunity, _shockDuration;

        int _burnStacks, _burnExtraMax, _slowStacks, _shockStacks;
        float _burnTimer, _burnTickTimer, _burnDps, _burnCarry;
        float _slowTimer, _slowBonus, _freezeTimer, _freezeImmunity, _shockTimer;

        public bool IsBoss { get => isBoss; set => isBoss = value; }
        public Func<bool> FreezeBlocked { get; set; } // 보스 페이즈 전환 연출 중에는 안 걸림
        public event Action Frozen;

        public int BurnStacks => _burnTimer > 0f ? _burnStacks : 0;
        public int BurnMax => _burnMax + _burnExtraMax;
        public int SlowStacks => _slowTimer > 0f ? _slowStacks : 0;
        public int SlowMax => _slowMax;
        public int ShockStacks => _shockTimer > 0f ? _shockStacks : 0;
        public int ShockMax => _shockMax;
        public bool IsFrozen => _freezeTimer > 0f;
        public float FreezeLeft => Mathf.Max(0f, _freezeTimer);
        public float FreezeImmunityLeft => Mathf.Max(0f, _freezeImmunity);

        // 이동·공격 속도 배율. 둔화 1중첩당 10% (4단계 +30%, 보스는 효과 50%). 빙결 중엔 0.
        public float SpeedMultiplier => IsFrozen ? 0f
            : Mathf.Max(0.05f, 1f - SlowStacks * _slowPerStack * (1f + _slowBonus) * (isBoss ? _slowBossRatio : 1f));

        public static StatusEffects For(Damageable target) =>
            target.TryGetComponent(out StatusEffects status) ? status : target.gameObject.AddComponent<StatusEffects>();

        void Awake()
        {
            _body = GetComponent<Damageable>();
            _burnMax = Mathf.RoundToInt(GameConfig.Get("burn.max_stacks", 5f));
            _burnDuration = GameConfig.Get("burn.duration", 4f);
            _burnTick = Mathf.Max(0.05f, GameConfig.Get("burn.tick", 0.5f));
            _slowPerStack = GameConfig.Get("slow.per_stack", 0.1f);
            _slowMax = Mathf.RoundToInt(GameConfig.Get("slow.max_stacks", 3f));
            _slowDuration = GameConfig.Get("slow.duration", 3f);
            _slowBossRatio = GameConfig.Get("slow.boss_ratio", 0.5f);
            _freezeAtSlow = Mathf.RoundToInt(GameConfig.Get("freeze.slow_stacks", 3f));
            _freezeNormal = GameConfig.Get("freeze.normal_duration", 2f);
            _freezeNormalImmunity = GameConfig.Get("freeze.normal_immunity", 3f);
            _freezeBoss = GameConfig.Get("freeze.boss_duration", 1f);
            _freezeBossImmunity = GameConfig.Get("freeze.boss_immunity", 10f);
            _shockMax = Mathf.RoundToInt(GameConfig.Get("shock.max_stacks", 5f));
            _shockDuration = GameConfig.Get("shock.duration", 5f);
        }

        // ── 화상 (#열양). perStackDps = 1중첩 초당 피해 (기준 피해 × 15%, 4단계 +50%). extraMax = 6단계 +2

        public void AddBurn(float perStackDps, int extraMax)
        {
            SetBurnSource(perStackDps, extraMax);
            _burnStacks = Mathf.Min(BurnMax, BurnStacks + 1);
            _burnTimer = _burnDuration;
        }

        // 절초 #열양 각인: 화상이 최대 중첩까지 바로 참
        public void FillBurn(float perStackDps, int extraMax)
        {
            SetBurnSource(perStackDps, extraMax);
            _burnStacks = BurnMax;
            _burnTimer = _burnDuration;
        }

        // 불길 안: 걸려 있는 화상의 지속 시간만 갱신
        public void RefreshBurn()
        {
            if (BurnStacks > 0) _burnTimer = _burnDuration;
        }

        void SetBurnSource(float perStackDps, int extraMax)
        {
            if (BurnStacks == 0) _burnCarry = 0f;
            _burnDps = BurnStacks > 0 ? Mathf.Max(_burnDps, perStackDps) : perStackDps;
            _burnExtraMax = Mathf.Max(_burnExtraMax, extraMax);
        }

        // ── 둔화·빙결 (#빙백). bonus = 4단계 +30%, freezeAtMax = 6단계 (3중첩이면 빙결)

        public void AddSlow(float bonus, bool freezeAtMax)
        {
            if (IsFrozen) return;
            _slowBonus = SlowStacks > 0 ? Mathf.Max(_slowBonus, bonus) : bonus;
            _slowStacks = Mathf.Min(_slowMax, SlowStacks + 1);
            _slowTimer = _slowDuration;
            if (freezeAtMax && _slowStacks >= _freezeAtSlow) TryFreeze();
        }

        // 면역 중이거나 보스 전환 중이면 안 걸려요. 빙결 중엔 모든 행동 정지 (적 쪽에서 Frozen을 받아 공격을 취소).
        public bool TryFreeze()
        {
            if (IsFrozen || _freezeImmunity > 0f || (FreezeBlocked != null && FreezeBlocked())) return false;
            _freezeTimer = isBoss ? _freezeBoss : _freezeNormal;
            _body.SetTint(FrozenTint);
            Frozen?.Invoke();
            return true;
        }

        // 절초 #빙백 각인: 즉시 빙결. 보스가 빙결 면역 중이면 대신 둔화 3중첩
        public void ImprintFreeze(float slowBonus)
        {
            if (TryFreeze() || IsFrozen) return;
            _slowBonus = Mathf.Max(_slowBonus, slowBonus);
            _slowStacks = _slowMax;
            _slowTimer = _slowDuration;
        }

        // ── 감전 (#뇌정): 그 자체로는 피해 없음. 낙뢰·연쇄 번개가 맞히면 모두 써서 방전

        public void AddShock()
        {
            _shockStacks = Mathf.Min(_shockMax, ShockStacks + 1);
            _shockTimer = _shockDuration;
        }

        public int ConsumeShock()
        {
            int stacks = ShockStacks;
            _shockStacks = 0;
            _shockTimer = 0f;
            return stacks;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_freezeTimer > 0f)
            {
                _freezeTimer -= dt;
                if (_freezeTimer <= 0f)
                {
                    // 빙결이 끝나면 둔화 0중첩, 그 뒤 면역 (면역 중에도 둔화는 걸림)
                    _freezeImmunity = isBoss ? _freezeBossImmunity : _freezeNormalImmunity;
                    _slowStacks = 0;
                    _slowTimer = 0f;
                    _body.SetTint(null);
                }
            }
            else if (_freezeImmunity > 0f) _freezeImmunity -= dt;

            if (_slowTimer > 0f) _slowTimer -= dt;
            if (_shockTimer > 0f) _shockTimer -= dt;
            TickBurn(dt);
        }

        void TickBurn(float dt)
        {
            if (_burnTimer <= 0f)
            {
                _burnStacks = 0;
                _burnTickTimer = 0f;
                return;
            }
            _burnTimer -= dt;
            _burnTickTimer += dt;
            while (_burnTickTimer >= _burnTick)
            {
                _burnTickTimer -= _burnTick;
                _burnCarry += _burnStacks * _burnDps * _burnTick * (1f - _body.Defense);
                int whole = Mathf.FloorToInt(_burnCarry);
                if (whole <= 0 || _body.IsDead) continue;
                _burnCarry -= whole;
                _body.ApplyHit(whole, false, PopupStyle.Dot);
            }
        }
    }
}
