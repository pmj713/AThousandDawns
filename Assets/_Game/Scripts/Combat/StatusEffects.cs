using System;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 적이 받는 상태이상 (상태이상 탭): 화상 · 둔화 → 빙결 · 감전.
    // 공통: 새 중첩이 붙으면 모든 중첩의 지속 시간이 처음으로 돌아가요. 상태이상끼리는 서로 지우지 않아요.
    // 화상은 파생 피해 (강인함 무시, 방어력 적용, 경직 없음). 소수점은 모아 뒀다가 정수가 되면 들어가요.
    // 기물이 더하는 값은 ElementalMods.Current에서 읽어요.
    [RequireComponent(typeof(Damageable))]
    public sealed class StatusEffects : MonoBehaviour
    {
        static readonly Color FrozenTint = new Color(0.66f, 0.85f, 0.94f);

        [SerializeField] bool isBoss; // 고수·보스: 둔화 효과 50%, 빙결 1초 + 면역 10초

        Damageable _body;
        int _burnMax, _slowMax, _freezeAtSlow, _shockMax;
        float _burnDuration, _burnTick, _slowPerStack, _slowDuration, _slowBossRatio;
        float _freezeNormal, _freezeNormalImmunity, _freezeBoss, _freezeBossImmunity, _shockDuration;

        int _burnStacks, _burnExtraMax, _burnOvercap, _slowStacks, _shockStacks;
        float _burnTimer, _burnTickTimer, _burnDps, _burnCarry, _burnUptime;
        float _slowTimer, _slowBonus, _freezeTimer, _freezeImmunity, _shockTimer;

        static ElementalMods Mods => ElementalMods.Current;

        public bool IsBoss { get => isBoss; set => isBoss = value; }
        public float BurnTakenMultiplier { get; set; } = 1f; // 열화 정예: 자신이 받는 화상 피해 절반
        public Func<bool> FreezeBlocked { get; set; } // 보스 페이즈 전환 연출 중에는 안 걸림
        public event Action Frozen;
        public static event Action<StatusEffects> AnyFrozen; // 한기 호박 주화입마: 빙결될 때 주변 적 둔화

        public Damageable Body => _body;
        public int BurnStacks => _burnTimer > 0f ? _burnStacks : 0;
        public int BurnMax => _burnMax + _burnExtraMax + Mods.BurnMaxBonus + _burnOvercap;
        public int SlowStacks => _slowTimer > 0f ? _slowStacks : 0;
        public int SlowMax => _slowMax;
        public int ShockStacks => _shockTimer > 0f ? _shockStacks : 0;
        public int ShockMax => _shockMax;
        public bool IsFrozen => _freezeTimer > 0f;
        public float FreezeLeft => Mathf.Max(0f, _freezeTimer);
        public float FreezeImmunityLeft => Mathf.Max(0f, _freezeImmunity);

        float BurnDuration => _burnDuration + Mods.BurnDurationBonus;
        float SlowDuration => _slowDuration + Mods.SlowDurationBonus;
        float ShockDuration => _shockDuration + Mods.ShockDurationBonus;
        float BossSlowRatio => Mathf.Min(1f, _slowBossRatio + Mods.BossSlowBonus);

        // 이동·공격 속도 배율. 둔화 1중첩당 10% (4단계 +30%, 보스는 효과 50%). 빙결 중엔 0.
        public float SpeedMultiplier => IsFrozen ? 0f
            : Mathf.Max(0.05f, 1f - SlowStacks * _slowPerStack * (1f + _slowBonus) * (isBoss ? BossSlowRatio : 1f));

        // 눈송이 수정: 둔화 3중첩인 적은 공격 예고가 길어져요
        public float WindupExtra => SlowStacks >= _slowMax ? Mods.SlowWindupExtra : 0f;

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

        public void AddBurn(float perStackDps, int extraMax, int stacks = 1)
        {
            if (stacks <= 0) return;
            int current = BeginBurn(perStackDps, extraMax);
            for (int i = 0; i < stacks; i++)
            {
                // 화룡 비늘: 최대 중첩인 적에게 또 걸면 그 적만 최대 중첩 +1
                if (current >= BurnMax && _burnOvercap < Mods.BurnOvercap) _burnOvercap++;
                current = Mathf.Min(BurnMax, current + 1);
            }
            _burnStacks = current;
        }

        // 절초 #열양 각인: 화상이 최대 중첩까지 바로 참
        public void FillBurn(float perStackDps, int extraMax)
        {
            BeginBurn(perStackDps, extraMax);
            _burnStacks = BurnMax;
        }

        // 불길 안: 걸려 있는 화상의 지속 시간만 갱신
        public void RefreshBurn()
        {
            if (BurnStacks > 0) _burnTimer = BurnDuration;
        }

        // 대장간 열양: 걸려 있는 화상의 남은 시간 +초 (최대 cap초)
        public void ExtendBurn(float seconds, float cap)
        {
            if (BurnStacks > 0 && _burnTimer < cap) _burnTimer = Mathf.Min(cap, _burnTimer + seconds);
        }

        // 지금 중첩을 돌려주고 지속 시간을 처음으로. 새로 붙는 화상이면 등잔·화룡 비늘 기록도 처음부터.
        int BeginBurn(float perStackDps, int extraMax)
        {
            int current = BurnStacks;
            if (current == 0)
            {
                _burnCarry = 0f;
                _burnUptime = 0f;
                _burnOvercap = 0;
                _burnExtraMax = 0;
            }
            _burnDps = current > 0 ? Mathf.Max(_burnDps, perStackDps) : perStackDps;
            _burnExtraMax = Mathf.Max(_burnExtraMax, extraMax);
            _burnTimer = BurnDuration;
            return current;
        }

        // ── 둔화·빙결 (#빙백). bonus = 4단계 +30%, freezeAtMax = 6단계 (3중첩이면 빙결)

        public void AddSlow(float bonus, bool freezeAtMax, int stacks = 1)
        {
            if (IsFrozen || stacks <= 0) return;
            _slowBonus = SlowStacks > 0 ? Mathf.Max(_slowBonus, bonus) : bonus;
            _slowStacks = Mathf.Min(_slowMax, SlowStacks + stacks);
            _slowTimer = SlowDuration;
            if (freezeAtMax && _slowStacks >= _freezeAtSlow) TryFreeze();
        }

        // 면역 중이거나 보스 전환 중이면 안 걸려요. 빙결 중엔 모든 행동 정지 (적 쪽에서 Frozen을 받아 공격을 취소).
        // duration을 주면 그 시간 (설산 영지 1초), 아니면 일반 2초 · 보스 1초.
        public bool TryFreeze(float duration = -1f)
        {
            if (IsFrozen || _freezeImmunity > 0f || (FreezeBlocked != null && FreezeBlocked())) return false;
            float time = duration > 0f ? duration : isBoss ? _freezeBoss : _freezeNormal;
            _freezeTimer = time + Mods.FreezeDurationBonus;
            _body.SetTint(FrozenTint);
            Frozen?.Invoke();
            AnyFrozen?.Invoke(this);
            return true;
        }

        // 대장간 빙백: 빙결을 깨요. 바로 풀리고 둔화 0중첩, 그 뒤 면역은 immunityCut초 짧게.
        public bool BreakFreeze(float immunityCut)
        {
            if (!IsFrozen) return false;
            _freezeTimer = 0f;
            EndFreeze();
            _freezeImmunity = Mathf.Max(0f, _freezeImmunity - immunityCut);
            return true;
        }

        void EndFreeze()
        {
            // 빙결이 끝나면 둔화 0중첩, 그 뒤 면역 (면역 중에도 둔화는 걸림)
            float immunity = isBoss ? _freezeBossImmunity - Mods.BossFreezeImmunityReduction : _freezeNormalImmunity;
            _freezeImmunity = Mathf.Max(0f, immunity * Mods.FreezeImmunityMultiplier);
            _slowStacks = 0;
            _slowTimer = 0f;
            _body.SetTint(null);
        }

        // 절초 #빙백 각인: 즉시 빙결. 보스가 빙결 면역 중이면 대신 둔화 3중첩
        public void ImprintFreeze(float slowBonus)
        {
            if (TryFreeze() || IsFrozen) return;
            _slowBonus = Mathf.Max(_slowBonus, slowBonus);
            _slowStacks = _slowMax;
            _slowTimer = SlowDuration;
        }

        // ── 감전 (#뇌정): 그 자체로는 피해 없음. 낙뢰·연쇄 번개가 맞히면 모두 써서 방전

        public void AddShock(int stacks = 1)
        {
            if (stacks <= 0) return;
            _shockStacks = Mathf.Min(_shockMax, ShockStacks + stacks);
            _shockTimer = ShockDuration;
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
                if (_freezeTimer <= 0f) EndFreeze();
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
                _burnUptime = 0f;
                _burnOvercap = 0; // 화룡 비늘: 화상이 꺼지면 처음으로
                return;
            }
            _burnTimer -= dt;
            _burnUptime += dt;
            _burnTickTimer += dt;
            float tick = Mods.BurnTick > 0f ? Mods.BurnTick : _burnTick; // 대장간 열양 강화판: 0.5 → 0.4초
            while (_burnTickTimer >= tick)
            {
                _burnTickTimer -= tick;
                _burnCarry += _burnStacks * _burnDps * BurnDamageMultiplier * BurnTakenMultiplier * tick * (1f - _body.Defense);
                int whole = Mathf.FloorToInt(_burnCarry);
                if (whole <= 0 || _body.IsDead) continue;
                _burnCarry -= whole;
                _body.ApplyHit(whole, false, PopupStyle.Dot);
            }
        }

        // 주작 깃털 (화상 피해 +%) × 등잔 (꺼지지 않고 이어진 시간만큼 +%, 최대까지)
        float BurnDamageMultiplier => (1f + Mods.BurnDamageBonus) * (1f + Ramp);

        float Ramp
        {
            get
            {
                var m = Mods;
                if (m.BurnRampPer <= 0f) return 0f;
                return Mathf.Min(m.BurnRampMax, Mathf.Floor(_burnUptime / Mathf.Max(0.1f, m.BurnRampInterval)) * m.BurnRampPer);
            }
        }
    }
}
