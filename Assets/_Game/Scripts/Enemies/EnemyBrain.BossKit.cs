using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 보스 · 고수 공통 견제기 · 이동기 (지역 보스 전부: 녹림왕 · 혈불 · 철룡 · 천마 · 오우거). 보스 고유 패턴 사이사이에 끼어들어요.
    //   원거리 견제기: 플레이어가 boss_kit.far_distance보다 멀리 boss_kit.far_time초 넘게 있으면, 플레이어 쪽 부채꼴로 투사체 boss_kit.volley_count발
    //                  (멀리서 비도만 던지며 버티는 것을 막아요). 보스마다 이름과 그림이 달라요 (VolleyName · 아트는 Arrow.Owner로)
    //   이동기: 플레이어가 boss_kit.leap_distance보다 멀면, 플레이어 발밑에 예고 원 → boss_kit.leap_windup초 뒤 그 자리로 도약해 내려찍어요 (원 안이면 맞음)
    // 고수(Rank Master: 청죽객 · 철권 마웅 · 혜광 · 탁랑객 · 혈영검마 · 고블린 족장)도 약한 판으로 써요 (master_kit.*, 없으면 boss_kit.*).
    // 값은 보스마다 {key}.kit_* 로 덮어쓸 수 있어요 (없으면 boss_kit.* · 고수는 master_kit.*). 2페이즈 전환 · 빙결 · 공격 중에는 쓰지 않아요.
    public abstract partial class EnemyBrain
    {
        public static event System.Action<EnemyBrain, Vector3> BossVolley;                 // 보스, 쏜 방향 (아트)
        public static event System.Action<EnemyBrain, Vector3, Vector3> BossLeapStarted;   // 보스, 떠날 자리, 내려찍을 자리 (예고 원이 뜰 때)
        public static event System.Action<EnemyBrain, Vector3> BossLeapLanded;             // 보스, 내려찍은 자리

        public virtual string VolleyName => Kind switch
        {
            EnemyKind.Nokrim => "대도 검풍",
            EnemyKind.BloodBuddha => "혈장 날리기",
            EnemyKind.IronDragon => "작살 세례",
            EnemyKind.HeavenlyDemon => "마기탄",
            EnemyKind.Ogre => "돌팔매",
            EnemyKind.Qingzhu => "죽엽비도",
            EnemyKind.IronFist => "권풍",
            EnemyKind.Hyegwang => "금강 염주",
            EnemyKind.Takrang => "물보라 갈고리",
            EnemyKind.BloodShadow => "혈영 비검",
            EnemyKind.GoblinChief => "투척 도끼",
            EnemyKind.DarkElf => "독 단검",
            EnemyKind.DeathKnight => "저주 검기 난사",
            _ => "견제",
        };

        public virtual string LeapName => Kind switch
        {
            EnemyKind.Nokrim => "산적왕 도약",
            EnemyKind.BloodBuddha => "불상 낙하",
            EnemyKind.IronDragon => "닻 뛰어들기",
            EnemyKind.HeavenlyDemon => "천마 강림",
            EnemyKind.Ogre => "몸통 덮치기",
            EnemyKind.Qingzhu => "청죽 경공",
            EnemyKind.IronFist => "철권 낙하",
            EnemyKind.Hyegwang => "나한 도약",
            EnemyKind.Takrang => "탁랑 뛰어들기",
            EnemyKind.BloodShadow => "혈영 강하",
            EnemyKind.GoblinChief => "족장 덮치기",
            EnemyKind.DarkElf => "그림자 도약",
            EnemyKind.DeathKnight => "망령마 도약",
            _ => "도약",
        };

        public bool IsLeaping => _leapState > 0;

        public static int BossKitOverride = -1; // 테스트용 (0 끔 · 1 켬)
        bool KitRank => Rank == EnemyRank.Boss || Rank == EnemyRank.Master;
        bool BossKitOn => BossKitOverride >= 0 ? BossKitOverride == 1 : GameConfig.GetOptional(Rank == EnemyRank.Boss ? "boss_kit.enabled" : "master_kit.enabled", 1f) > 0.5f;

        float _farTime, _volleyCooldown = 3f, _leapCooldown = 4f, _leapTimer;
        int _leapState; // 0 없음 · 1 예고 (제자리) · 2 날아감
        Vector3 _leapFrom, _leapTo;
        Telegraph _leapMarker;
        readonly Dictionary<string, float> _kitValues = new Dictionary<string, float>();

        float Kit(string name, float fallback)
        {
            if (_kitValues.TryGetValue(name, out float v)) return v;
            float shared = GameConfig.GetOptional($"boss_kit.{name}", fallback);
            if (Rank != EnemyRank.Boss) shared = GameConfig.GetOptional($"master_kit.{name}", shared); // 고수는 약한 판
            v = GameConfig.GetOptional($"{Key}.kit_{name}", shared);
            _kitValues[name] = v;
            return v;
        }

        // Update에서 (경직 확인 전): 보스만
        void TickBossKit(float dt)
        {
            if (!KitRank || !_awake || Player == null || Player.IsDead || !BossKitOn) return;
            if (_leapState > 0) { TickLeap(dt); return; }
            if (_volleyCooldown > 0f) _volleyCooldown -= dt;
            if (_leapCooldown > 0f) _leapCooldown -= dt;
            float dist = RivalFreeDistance();
            _farTime = dist > Kit("far_distance", 6f) ? _farTime + dt : 0f; // 보스가 공격 중이어도 쌓여요 (공격이 끝나는 대로 견제)
            if (Attack.IsAttacking || IsStunned || Status.IsFrozen || (this is IMasterBrain m && m.IsTransitioning)) return;

            if (_leapCooldown <= 0f && dist > Kit("leap_distance", 7f))
            {
                StartLeap();
                return;
            }
            if (_volleyCooldown <= 0f && _farTime >= Kit("far_time", 2.5f)) FireVolley();
        }

        float RivalFreeDistance()
        {
            Vector3 d = Player.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void FireVolley()
        {
            _volleyCooldown = Kit("volley_cooldown", 8f);
            _farTime = 0f;
            Vector3 to = Player.transform.position - transform.position;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            FaceToward(dir);
            int count = Mathf.Max(1, Mathf.RoundToInt(Kit("volley_count", 3f)));
            float spread = Kit("volley_spread", 18f);
            float speed = Kit("volley_speed", 11f), range = Kit("volley_range", 14f), radius = Kit("volley_radius", 0.4f);
            float damage = AttackPower * Kit("volley_ratio", 0.5f);
            Vector3 origin = transform.position + Vector3.up * Mathf.Min(1.4f, Height * 0.6f) + dir * (BodyRadius + 0.3f);
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * spread;
                Director.SpawnArrow(origin, Quaternion.Euler(0f, angle, 0f) * dir, speed, range, radius, damage, Body);
            }
            Sfx.Play(SfxId.Arrow);
            BossVolley?.Invoke(this, dir);
        }

        void StartLeap()
        {
            _leapState = 1;
            _leapTimer = Kit("leap_windup", 0.9f);
            _leapFrom = transform.position;
            _leapTo = Director.FindFreeSpot(Player.transform.position, BodyRadius + PlayerRadius + 0.3f); // 플레이어 몸 위로 내려앉으면 둘이 겹쳐 빠져나오지 못해요 (덩치 큰 오우거)
            float radius = Kit("leap_radius", 2.2f);
            _leapMarker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            _leapMarker.Show(TelegraphShapes.Circle(radius), true);
            _leapMarker.SetPose(_leapTo, Vector3.forward);
            _leapMarker.SetProgress(0f);
            FaceToward(_leapTo - transform.position);
            HoldStill(_leapTimer + Kit("leap_air", 0.35f) + 0.2f); // 도약하는 동안 다른 패턴은 쉬어요
            BossLeapStarted?.Invoke(this, _leapFrom, _leapTo);
        }

        void TickLeap(float dt)
        {
            _leapTimer -= dt;
            if (_leapState == 1)
            {
                float windup = Kit("leap_windup", 0.9f);
                if (_leapMarker != null) _leapMarker.SetProgress(1f - Mathf.Clamp01(_leapTimer / Mathf.Max(0.01f, windup)));
                if (_leapTimer > 0f) return;
                _leapState = 2;
                _leapTimer = Kit("leap_air", 0.35f);
                return;
            }
            if (_leapTimer > 0f) return;
            _leapState = 0;
            _leapCooldown = Kit("leap_cooldown", 10f);
            if (_leapMarker != null) Destroy(_leapMarker.gameObject);
            _leapMarker = null;
            Teleport(_leapTo);
            float radius = Kit("leap_radius", 2.2f);
            Director.SpawnBurst(_leapTo, radius, BurstKind.Slam);
            Vector3 d = Player.transform.position - _leapTo;
            d.y = 0f;
            if (d.magnitude <= radius + PlayerRadius) Player.TryReceiveHit(AttackPower * Kit("leap_ratio", 0.8f), _leapTo, Kit("leap_knockback", 1.5f), Body);
            BossLeapLanded?.Invoke(this, _leapTo);
        }

        void CleanupBossKit()
        {
            if (_leapMarker != null) Destroy(_leapMarker.gameObject);
            _leapMarker = null;
            _leapState = 0;
        }
    }
}
