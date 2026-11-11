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
        public float RunOpenerSpeed { get; private set; }       // 쾌 5: 평타 1·2타 공격 속도 +
        public float RunFinisherRecoveryCut { get; private set; } // 쾌 10: 3타째 뒤 쉬는 틈 −
        public float RunEchoRatio { get; private set; }         // 쾌 20: 3타째 잔상 피해 비율
        public float RunMaxHealthFlat { get; private set; }     // 강: 최대 체력 +
        public float RunXpBonus { get; private set; }           // 맥: 수련치 +
        public float RunSkillCooldownCut { get; private set; }  // 기 5: 초식 쿨타임 −
        public bool ReviveReady { get; private set; }           // 강 20: 한 판에 한 번 기사회생
        public bool DoubleGauge { get; private set; }           // 기 20: 기세를 200%까지 (절초 2회)
        float _reviveRatio;

        // 기세 상한 (절초 한 번 = GaugeMax)
        public float GaugeCap => _gaugeMax * (DoubleGauge ? 2f : 1f);

        public void ApplyMeditation(float attackSpeed, float openerSpeed, float finisherRecoveryCut, float echoRatio,
            float maxHealth, float toughness, float xp, float gauge, float skillCooldownCut, float reviveRatio, bool doubleGauge)
        {
            RunAttackSpeedBonus = attackSpeed;
            RunOpenerSpeed = openerSpeed;
            RunFinisherRecoveryCut = finisherRecoveryCut;
            RunEchoRatio = echoRatio;
            RunMaxHealthFlat = maxHealth;
            Toughness += toughness;
            RunXpBonus = xp;
            RunGaugeBonus += gauge;
            RunSkillCooldownCut = skillCooldownCut;
            ReviveReady = reviveRatio > 0f;
            _reviveRatio = reviveRatio;
            DoubleGauge = doubleGauge;
            OnKeywordsChanged(); // 최대 체력 다시 계산
            if (!IsDead) Health = MaxHealth; // 판 시작: 가득
        }

        // 쾌 5: 1·2타를 휘두르는 동안 빠르게 · 쾌 10: 3타째 후딜을 줄여요 (속도를 올려 같은 효과)
        float MeditationComboSpeed
        {
            get
            {
                int last = _combo.BaseHitCount - 1;
                if (RunFinisherRecoveryCut > 0f && _combo.HitIndex == last && _combo.CurrentPhase == ComboTimeline.Phase.Recovery)
                    return 1f / UnityEngine.Mathf.Max(0.1f, 1f - RunFinisherRecoveryCut);
                if (RunOpenerSpeed > 0f && _combo.HitIndex < last && _combo.IsAttacking) return 1f + RunOpenerSpeed;
                return 1f;
            }
        }

        // 쾌 20: 3타째를 잔상이 한 번 더 베요 (잠시 뒤, 같은 방향·범위, 피해 비율만큼, 평타로 쳐요)
        float _echoTimer = -1f, _echoRadius, _echoAngle;
        UnityEngine.Vector3 _echoFacing;
        AThousandDawns.Combat.HitDamageInput _echoHit;

        public int EchoHits { get; private set; } // 테스트용: 잔상이 맞힌 수

        void QueueEcho(float radius, float angle, AThousandDawns.Combat.HitDamageInput hit)
        {
            if (RunEchoRatio <= 0f) return;
            _echoRadius = radius;
            _echoAngle = angle;
            _echoFacing = _facing;
            _echoHit = hit;
            _echoHit.BaseDamage *= RunEchoRatio;
            _echoTimer = AThousandDawns.Core.GameConfig.GetOptional("meditation.swift_echo_delay", 0.12f);
        }

        void TickEcho(float dt)
        {
            if (_echoTimer < 0f) return;
            _echoTimer -= dt;
            if (_echoTimer > 0f) return;
            _echoTimer = -1f;
            if (IsDead) return;
            var facing = _facing;
            _facing = _echoFacing;
            _hitThisSwing.Clear();
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, _echoRadius, _echoAngle, 0.1f);
            EchoHits = HitSector(_echoRadius, _echoAngle, _echoHit, AThousandDawns.Combat.HitTag.Basic);
            _facing = facing;
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
            AThousandDawns.Core.Sfx.Play(AThousandDawns.Core.SfxId.Revive); // 기사회생
            AThousandDawns.Combat.DamagePopups.Spawn(transform.position + UnityEngine.Vector3.up * 2.2f, UnityEngine.Mathf.RoundToInt(Health), AThousandDawns.Combat.PopupStyle.Burst);
            return true;
        }

        // 판 중간 저장에서 되살릴 때 (기물·면벽수련을 먼저 적용해 최대 체력이 정해진 뒤에)
        public void RestoreRunState(float health, int potions, float gauge, bool revive)
        {
            Potions = UnityEngine.Mathf.Clamp(potions, 0, PotionMax);
            Gauge = UnityEngine.Mathf.Clamp(gauge, 0f, GaugeCap);
            Health = UnityEngine.Mathf.Clamp(health, 1f, MaxHealth);
            ReviveReady = revive;
        }

        // 마지막으로 맞은 것 (결과 화면 사망 원인): "청죽객의 죽림섬에" · "산적 졸개에게" · "화상으로"
        public string LastHitBy { get; private set; }

        static string DescribeAttacker(AThousandDawns.Combat.Damageable attacker)
        {
            if (attacker == null || !attacker.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain)) return null;
            string name = brain.DisplayName;
            string attack = brain.AttackName;
            return string.IsNullOrEmpty(attack) ? $"{name}에게" : $"{name}의 {attack}에";
        }

        // 기절 (석상 나한 돌진): 그동안 움직이지도 공격하지도 못해요. 맞은 뒤 경직보다 길면 늘려요
        public void Stagger(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            _stunTimer = UnityEngine.Mathf.Max(_stunTimer, seconds);
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
