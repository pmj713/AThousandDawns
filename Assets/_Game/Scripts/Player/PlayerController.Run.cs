namespace AThousandDawns.Player
{
    // 판 안에서 이벤트가 거는 보정 (이벤트 방). RoomRun이 정하고 층·판이 끝나면 되돌려요.
    public sealed partial class PlayerController
    {
        public float RunMoveBonus { get; set; }      // 산신당 축복: 이동속도 +10% (이번 지역)
        public float RunGaugeBonus { get; set; }     // 산신당 축복: 기세 충전 +15% (이번 지역)
        public float RunTakenIncrease { get; set; }  // 산신의 노여움: 받는 피해 +15% (이번 층)

        float GaugeGainMultiplier => 1f + RunGaugeBonus;

        // 면벽수련 (판을 시작할 때 RoomRun이 정해요)
        public float RunAttackSpeedBonus { get; private set; }  // 쾌: 공격 속도 +
        public int RunDashBonus { get; private set; }           // 쾌 5: 대시 +1
        public float RunDashAttackBonus { get; private set; }   // 쾌 10: 대시 공격 +15%
        public float RunMaxHealthFlat { get; private set; }     // 강: 최대 체력 +
        public float RunXpBonus { get; private set; }           // 맥: 수련치 +
        public float RunSkillCooldownCut { get; private set; }  // 기 5: 초식 쿨타임 −
        public bool FloorGuard { get; set; }                    // 강 10: 층마다 첫 피격 무효 (층에 들어갈 때 켜요)
        public bool ReviveReady { get; private set; }           // 강 20: 한 판에 한 번 기사회생
        float _reviveRatio;

        public void ApplyMeditation(float attackSpeed, int dash, float dashAttack, float maxHealth, float toughness, float xp, float gauge, float skillCooldownCut, float reviveRatio)
        {
            RunAttackSpeedBonus = attackSpeed;
            RunDashBonus = dash;
            RunDashAttackBonus = dashAttack;
            RunMaxHealthFlat = maxHealth;
            Toughness += toughness;
            RunXpBonus = xp;
            RunGaugeBonus += gauge;
            RunSkillCooldownCut = skillCooldownCut;
            ReviveReady = reviveRatio > 0f;
            _reviveRatio = reviveRatio;
            OnKeywordsChanged(); // 대시 횟수·최대 체력 다시 계산
            if (!IsDead) Health = MaxHealth; // 판 시작: 가득
        }

        // 기 10: 전투가 시작되면 기세를 적어도 이만큼
        public void RaiseGaugeTo(float ratio) => Gauge = UnityEngine.Mathf.Max(Gauge, _gaugeMax * ratio);

        // 강 20: 쓰러지면 한 번 체력 40%로 일어나요
        bool TryRevive()
        {
            if (!IsDead || !ReviveReady) return false;
            ReviveReady = false;
            Health = MaxHealth * _reviveRatio;
            _invulnerableTimer = UnityEngine.Mathf.Max(_invulnerableTimer, 1.5f);
            if (visual != null) visual.localScale = UnityEngine.Vector3.one;
            AThousandDawns.Combat.DamagePopups.Spawn(transform.position + UnityEngine.Vector3.up * 2.2f, UnityEngine.Mathf.RoundToInt(Health), AThousandDawns.Combat.PopupStyle.Burst);
            return true;
        }

        // 시험판 K 키: 바로 쓰러지기 (결과 화면 확인용)
        public void KillForTest()
        {
            if (IsDead) return;
            Health = 0f;
            if (visual != null) visual.localScale = new UnityEngine.Vector3(1.2f, 0.25f, 1.2f);
        }

        // 버려진 약방 독약: 체력을 직접 깎아요 (쓰러지지는 않아요)
        public void TakeEventDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = UnityEngine.Mathf.Max(1f, Health - amount);
        }
    }
}
