using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 마검사 천마 (천마총 최종 보스, 몬스터 시트 67행): 체력 3150 · 공격 25 · 강인함 6 · 방어 20% (천마총 배율 전 → 14175 · 50). 3페이즈.
    // 보스 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 페이즈 전환 2초 (무적), 같은 패턴은 연속 2번까지.
    // 1페이즈 · 천마검
    //   발도 일섬: 6칸 안이면 예고 0.7 · 앞으로 가늘고 긴 일직선 베기 (폭 1칸, 길이 7칸) ×1.0
    //   십자 검기: 멀면 예고 0.8 · 플레이어 쪽을 향한 십자로 검기 네 갈래 (10칸, 초속 10칸) ×0.7
    //   마기 장판: 10초마다 예고 0.8 · 플레이어 둘레 세 곳에 마기 장판 (반경 1.5칸, 5초, 안에 있으면 0.5초마다 ×0.3 · 이동 −20%)
    //   마기 포박: 6칸 밖에 2초 넘게 있으면 예고 0.7 · 느린 마기 구슬 (초속 5칸, 반경 0.6칸, 12칸) ×0.5, 맞으면 1.2초 묶여요 (원거리 대응)
    // 2페이즈 · 마도 (체력 66%): 마검사 천마가 이계의 마법을 꺼내요. 무공(발도 일섬 · 십자 검기 · 마기 장판 · 포박)에 마법이 섞여요.
    //   유성: 9초마다 예고 0.8 · 큰 유성 하나 (반경 3칸)가 1.5초 동안 플레이어를 따라오다 멈추고 0.6초 뒤 떨어져요 ×1.4
    //   차원 베기: 7초마다 예고 0.8 · 플레이어 둘레 3.5칸에 균열 셋 (삼각형), 1.0초 뒤 세 변이 베어져요 (폭 1칸, ×0.9)
    //   마법진: 11초마다 예고 0.8 · 플레이어 발밑 큰 마법진 (반경 2.5칸, 1.6초 뒤 ×1.0) + 둘레 4칸에 작은 마법진 넷 (반경 1.5칸, 2.0초 뒤 ×0.6)
    // 3페이즈 · 마검 해체대법 (체력 33%): 무공과 마법이 하나로. 방 가장자리부터 마기가 차올라요. 안전 반경이 11칸에서 3초마다 1칸씩 줄어 5칸까지 (밖에 있으면 0.5초마다 ×0.2).
    //   발도 일섬이 "마검 천마군림"이 돼요 (3칸 안이면 예고 0.6 · 앞 부채꼴 반경 3칸 220° ×1.0).
    //   마검비: 8초마다 예고 1.0 · 1.5초 동안 플레이어 둘레에 불길 두른 검 12자루가 떨어져 터져요 (예고 0.6초, 반경 1.2칸, ×0.6)
    //   마검 천멸: 10초마다 예고 1.0 · 둘레 2.5칸 폭발 (×1.0) + 8방향 검기 (×0.7), 검기 줄 위 3칸 · 6칸에 불길이 0.7 · 1.0초 뒤 터져요 (반경 1.1칸, ×0.6)
    //   유성 · 차원 베기는 계속 써요 (마법진 · 마기 장판은 해체대법 마기가 대신해요)
    public sealed class HeavenlyDemonBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Slash, Cross, Miasma, Bind, Rain, Meteor, Rift, Rune, Cleave }

        // 바닥 예고가 무엇인지 (그림이 달라요. 판정은 원 · 부채꼴 · 직선 셋)
        //   Phantom은 더 안 써요 (환영 베기 없어짐). Rift는 차원 베기 변이라 Mark · MarkFall 대신 RiftOpen · RiftCut으로 알려요
        public enum MarkStyle { Sword, Phantom, Fireball, Rune, FireSword, Rift }

        struct Mark
        {
            public Vector3 At, Dir;
            public float Timer, Total, Radius, Ratio, Angle;
            public bool Sector;
            public bool Line;   // 직선 (At에서 Dir로 길이 Radius, 폭 Width)
            public float Width;
            public float Track; // 남은 따라가기 시간 (0보다 크면 플레이어 발밑을 따라와요)
            public MarkStyle Style;
            public Telegraph Marker;
        }

        // 마기 포박 구슬: 곧게 날아가다 플레이어에 닿으면 묶어요
        struct Orb
        {
            public Vector3 At, Dir;
            public float Left;
            public Transform View;
            public Telegraph Marker;
        }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _decisionPause, _transitionTime, _p2Ratio, _p3Ratio;
        float _slashWindup, _slashRadius, _slashAngle2, _slashRatio, _closeDistance;
        float _drawWindup, _drawWidth, _drawLength, _drawRange, _drawRatio;
        float _crossWindup, _crossRange, _crossSpeed, _crossRatio;
        float _miasmaWindup, _miasmaInterval, _miasmaTimer, _miasmaRadius, _miasmaLife, _miasmaRatio, _miasmaSlow;
        float _graspFar, _graspDelay, _farTimer;
        float _bindWindup, _bindSpeed, _bindRadius, _bindRange, _bindRatio, _bindTime;
        float _rainWindup, _rainInterval, _rainTimer, _rainFall, _rainRadius, _rainRatio, _rainTime;
        int _rainCount, _rainDone;
        float _meteorWindup, _meteorInterval, _meteorTimer, _meteorRadius, _meteorTrack, _meteorFall, _meteorRatio;
        float _riftWindup, _riftInterval, _riftTimer, _riftRadius, _riftDelay, _riftWidth, _riftRatio;
        float _runeWindup, _runeInterval, _runeTimer, _runeRadius, _runeDelay, _runeRatio, _runeRing, _runeSmallRadius, _runeSmallDelay, _runeSmallRatio;
        float _cleaveWindup, _cleaveInterval, _cleaveTimer, _cleaveRadius, _cleaveRatio, _cleaveWaveRatio, _cleaveFireRadius, _cleaveFireRatio;
        float _safeStart, _safeMin, _safeStep, _safeShrink, _safeRatio, _safeTick, _safeRadius, _safeTimer, _safeHurt;
        float _decisionTimer, _transitionTimer, _activeTimer;
        Vector3 _zoneCenter;
        Vector2 _zoneSize = new Vector2(24f, 18f);
        Telegraph _safeMarker;
        readonly List<Mark> _marks = new List<Mark>();
        readonly List<Orb> _orbs = new List<Orb>();

        public override EnemyKind Kind => EnemyKind.HeavenlyDemon;
        protected override string Key => "heavenlydemon";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 2f;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Rain => _rainTime + 0.05f,
            _ => 0.1f,
        };

        public override string AttackName => _pattern switch
        {
            Pattern.Slash => Phase >= 3 ? "마검 천마군림" : "발도 일섬", Pattern.Cross => "십자 검기", Pattern.Miasma => "마기 장판",
            Pattern.Bind => "마기 포박", Pattern.Rain => "마검비", Pattern.Meteor => "유성", Pattern.Rift => "차원 베기",
            Pattern.Rune => "마법진", Pattern.Cleave => "마검 천멸", _ => null,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public float SafeRadius => _safeRadius; // 3페이즈: 마기가 차오르지 않은 안전 반경 (0이면 아직)
        public bool IsPulling => false; // 마력 흡인은 없어졌어요 (소용돌이는 탁랑객 몫). 아트 호환용
        public Vector3 ZoneCenter => _zoneCenter; // 해체대법 안전 반경의 가운데

        // 그림 신호 (아트 InkHeavenlyDemonFx가 받아 그려요. 판정·수치와는 상관없어요)
        //   Slash: 마검 천마군림 부채꼴 (At, Dir, Radius, Angle) · DrawCut: 발도 일섬 (At 시작 → To 끝, Dir, Radius = 폭)
        //   Mark · MarkFall: Style로 검비 · 화염구(유성) · 마법진 · 마검비를 나눠요
        //   MeteorTrack: 유성 예고가 따라오기 시작 (Target = 바닥 예고, Radius, Time = 따라오는 시간). 멈추는 순간 Mark (Time = 떨어지기까지)
        //   Bind: 포박 구슬 발사 (At, Dir, Radius, Time = 최대 비행 시간, Target = 구슬) · Bound: 플레이어가 묶임 (Target = 플레이어, Time = 묶이는 시간)
        //   RiftOpen: 균열 하나 열림 (At, Radius = 둘레 반경, Time = 베기까지) · RiftCut: 변 하나 베기 (At → To, Dir, Radius = 폭)
        //   Cleave: 마검 천멸 폭발 (At, Radius) · Transition: Phase = 들어갈 페이즈
        //   Grasp · Grasped · PullBurst · Blink는 더 안 보내요 (패턴 없어짐, 아트 호환용으로 남겨 둬요)
        public enum FxKind { Slash, Cross, Grasp, Grasped, Mark, MarkFall, PullBurst, Transition, Blink, Cleave, DrawCut, Bind, Bound, RiftOpen, RiftCut, MeteorTrack }
        public struct FxCue
        {
            public FxKind Kind;
            public Vector3 At, Dir, To;
            public float Radius, Angle, Time;
            public bool Sector;
            public MarkStyle Style;
            public int Phase;
            public Transform Target;
        }
        public static event System.Action<HeavenlyDemonBrain, FxCue> Cue;
        void Fx(FxCue c) => Cue?.Invoke(this, c);

        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
        }

        protected override void OnInitialized() => _zoneCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 3f);
            _decisionPause = Tuning("decision_pause", 0.5f);
            _transitionTime = Tuning("transition_time", 2f);
            _p2Ratio = Tuning("phase2_hp_ratio", 0.66f);
            _p3Ratio = Tuning("phase3_hp_ratio", 0.33f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _slashWindup = Tuning("slash_windup", 0.6f);
            _slashRadius = Tuning("slash_radius", 3f);
            _slashAngle2 = Tuning("p2_slash_angle", 220f);
            _slashRatio = Tuning("slash_ratio", 1f);
            _drawWindup = Tuning("draw_windup", 0.7f);
            _drawWidth = Tuning("draw_width", 1f);
            _drawLength = Tuning("draw_length", 7f);
            _drawRange = Tuning("draw_range", 6f);
            _drawRatio = Tuning("draw_ratio", 1f);
            _crossWindup = Tuning("cross_windup", 0.8f);
            _crossRange = Tuning("cross_range", 10f);
            _crossSpeed = Tuning("cross_speed", 10f);
            _crossRatio = Tuning("cross_ratio", 0.7f);
            _miasmaWindup = Tuning("miasma_windup", 0.8f);
            _miasmaInterval = Tuning("miasma_interval", 10f);
            _miasmaRadius = Tuning("miasma_radius", 1.5f);
            _miasmaLife = Tuning("miasma_life", 5f);
            _miasmaRatio = Tuning("miasma_ratio", 0.3f);
            _miasmaSlow = Tuning("miasma_slow", 0.2f);
            _graspFar = Tuning("grasp_far", 6f);
            _graspDelay = Tuning("grasp_delay", 2f);
            _bindWindup = Tuning("bind_windup", 0.7f);
            _bindSpeed = Tuning("bind_speed", 5f);
            _bindRadius = Tuning("bind_radius", 0.6f);
            _bindRange = Tuning("bind_range", 12f);
            _bindRatio = Tuning("bind_ratio", 0.5f);
            _bindTime = Tuning("bind_time", 1.2f);
            _rainWindup = Tuning("p3_rain_windup", 1f);
            _rainInterval = Tuning("p3_rain_interval", 8f);
            _rainFall = Tuning("p3_rain_fall", 0.6f);
            _rainRadius = Tuning("p3_rain_radius", 1.2f);
            _rainRatio = Tuning("p3_rain_ratio", 0.6f);
            _rainTime = Tuning("p3_rain_time", 1.5f);
            _rainCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("p3_rain_count", 12f)));
            _safeStart = Tuning("p3_safe_start", 11f);
            _safeMin = Tuning("p3_safe_min", 5f);
            _safeStep = Tuning("p3_safe_step", 1f);
            _safeShrink = Tuning("p3_safe_interval", 3f);
            _safeRatio = Tuning("p3_safe_ratio", 0.2f);
            _safeTick = Tuning("p3_safe_tick", 0.5f);
            _meteorInterval = Tuning("p2_meteor_interval", 9f);
            _meteorWindup = Tuning("p2_meteor_windup", 0.8f);
            _meteorRadius = Tuning("meteor_big_radius", 3f);
            _meteorTrack = Tuning("meteor_track", 1.5f);
            _meteorFall = Tuning("meteor_lock_fall", 0.6f);
            _meteorRatio = Tuning("meteor_big_ratio", 1.4f);
            _riftInterval = Tuning("rift_interval", 7f);
            _riftWindup = Tuning("rift_windup", 0.8f);
            _riftRadius = Tuning("rift_radius", 3.5f);
            _riftDelay = Tuning("rift_delay", 1f);
            _riftWidth = Tuning("rift_width", 1f);
            _riftRatio = Tuning("rift_ratio", 0.6f);
            _runeInterval = Tuning("p2_rune_interval", 11f);
            _runeWindup = Tuning("p2_rune_windup", 0.8f);
            _runeRadius = Tuning("p2_rune_radius", 2.5f);
            _runeDelay = Tuning("p2_rune_delay", 1.6f);
            _runeRatio = Tuning("p2_rune_ratio", 1f);
            _runeRing = Tuning("p2_rune_ring", 4f);
            _runeSmallRadius = Tuning("p2_rune_small_radius", 1.5f);
            _runeSmallDelay = Tuning("p2_rune_small_delay", 2f);
            _runeSmallRatio = Tuning("p2_rune_small_ratio", 0.6f);
            _cleaveInterval = Tuning("p3_cleave_interval", 10f);
            _cleaveWindup = Tuning("p3_cleave_windup", 1f);
            _cleaveRadius = Tuning("p3_cleave_radius", 2.5f);
            _cleaveRatio = Tuning("p3_cleave_ratio", 1f);
            _cleaveWaveRatio = Tuning("p3_cleave_wave_ratio", 0.7f);
            _cleaveFireRadius = Tuning("p3_cleave_fire_radius", 1.1f);
            _cleaveFireRatio = Tuning("p3_cleave_fire_ratio", 0.6f);
            _miasmaTimer = _miasmaInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_miasmaTimer > 0f) _miasmaTimer -= dt;
            if (_rainTimer > 0f) _rainTimer -= dt;
            if (_meteorTimer > 0f) _meteorTimer -= dt;
            if (_riftTimer > 0f) _riftTimer -= dt;
            if (_runeTimer > 0f) _runeTimer -= dt;
            if (_cleaveTimer > 0f) _cleaveTimer -= dt;
            _farTimer = ToPlayer().magnitude > _graspFar ? _farTimer + dt : 0f; // 멀리서만 싸우면 마기 포박
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase(Phase + 1);
            }
            TickMarks(dt);
            TickOrbs(dt);
            TickSafeZone(dt);
        }

        // ── 고르기

        float SlashRange => Phase >= 3 ? _closeDistance : _drawRange;

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_farTimer >= _graspDelay) next = Pattern.Bind;
            else if (Phase >= 3 && _rainTimer <= 0f) next = Pattern.Rain;
            else if (Phase >= 3 && _cleaveTimer <= 0f) next = Pattern.Cleave;
            else if (Phase >= 2 && _meteorTimer <= 0f) next = Pattern.Meteor;
            else if (Phase == 2 && _runeTimer <= 0f) next = Pattern.Rune;
            else if (Phase >= 2 && _riftTimer <= 0f) next = Pattern.Rift;
            else if (Phase <= 2 && _miasmaTimer <= 0f) next = Pattern.Miasma;
            else if (distance <= SlashRange) next = Pattern.Slash;
            else next = Pattern.Cross;
            if (next == _lastPattern && _repeat >= _maxRepeat && (next == Pattern.Slash || next == Pattern.Cross))
                next = next == Pattern.Slash ? Pattern.Cross : Pattern.Slash;
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            RecoveryTime = Tuning("recovery", 0.8f);
            switch (next)
            {
                case Pattern.Slash: WindupTime = Phase >= 3 ? _slashWindup : _drawWindup; break;
                case Pattern.Cross: WindupTime = _crossWindup; break;
                case Pattern.Miasma:
                    WindupTime = _miasmaWindup;
                    _miasmaTimer = _miasmaInterval;
                    break;
                case Pattern.Bind:
                    WindupTime = _bindWindup;
                    RecoveryTime = 0.3f;
                    _farTimer = 0f;
                    break;
                case Pattern.Rain:
                    WindupTime = _rainWindup;
                    _rainTimer = _rainInterval;
                    break;
                case Pattern.Meteor:
                    WindupTime = _meteorWindup;
                    _meteorTimer = _meteorInterval;
                    break;
                case Pattern.Rift:
                    WindupTime = _riftWindup;
                    _riftTimer = _riftInterval;
                    break;
                case Pattern.Rune:
                    WindupTime = _runeWindup;
                    _runeTimer = _runeInterval;
                    break;
                case Pattern.Cleave:
                    WindupTime = _cleaveWindup;
                    _cleaveTimer = _cleaveInterval;
                    break;
            }
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _closeDistance * 0.8f) Move(to.normalized * MoveSpeed, dt);
        }

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Slash => Phase >= 3 ? TelegraphShapes.Lunge(_slashRadius, _slashAngle2, 0f) : TelegraphShapes.Rect(_drawWidth, _drawLength),
            Pattern.Bind => TelegraphShapes.Rect(_bindRadius * 2f, _bindRange),
            Pattern.Cleave => TelegraphShapes.Circle(_cleaveRadius),
            _ => TelegraphShapes.Circle(BodyRadius + 0.4f), // 몸 둘레에 마기가 모여요
        };

        protected override bool RadialTelegraph => _pattern == Pattern.Cleave;

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _rainDone = 0;
            switch (_pattern)
            {
                case Pattern.Slash:
                    if (Phase >= 3) // 마검 천마군림: 넓은 부채꼴
                    {
                        if (HitShapes.InSector(transform.position, AimDirection, _slashRadius, _slashAngle2, Player.transform.position, PlayerRadius))
                            Player.TryReceiveHit(AttackPower * _slashRatio, transform.position, HitKnockback, Body);
                        Fx(new FxCue { Kind = FxKind.Slash, At = transform.position, Dir = AimDirection, Radius = _slashRadius, Angle = _slashAngle2 });
                    }
                    else DrawCut();
                    break;
                case Pattern.Cross:
                    for (int i = 0; i < 4; i++) SwordWave(Quaternion.Euler(0f, 90f * i, 0f) * AimDirection, _crossRatio);
                    break;
                case Pattern.Rift:
                    OpenRifts();
                    break;
                case Pattern.Meteor:
                    BigMeteor();
                    break;
                case Pattern.Rune:
                    {
                        Vector3 p = Player.transform.position;
                        AddMark(p, Vector3.forward, _runeDelay, _runeRadius, _runeRatio, false, 0f, MarkStyle.Rune);
                        float turn = Random.Range(0f, 90f);
                        for (int i = 0; i < 4; i++)
                            AddMark(p + Quaternion.Euler(0f, turn + 90f * i, 0f) * Vector3.forward * _runeRing, Vector3.forward,
                                _runeSmallDelay, _runeSmallRadius, _runeSmallRatio, false, 0f, MarkStyle.Rune);
                    }
                    break;
                case Pattern.Cleave:
                    Director.SpawnBurst(transform.position, _cleaveRadius, BurstKind.Fireball);
                    Fx(new FxCue { Kind = FxKind.Cleave, At = transform.position, Radius = _cleaveRadius });
                    if (ToPlayer().magnitude <= _cleaveRadius + PlayerRadius) Player.TryReceiveHit(AttackPower * _cleaveRatio, transform.position, -1f, Body);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 45f * i, 0f) * AimDirection;
                        SwordWave(dir, _cleaveWaveRatio);
                        AddMark(transform.position + dir * 3f, dir, 0.7f, _cleaveFireRadius, _cleaveFireRatio, false, 0f, MarkStyle.FireSword);
                        AddMark(transform.position + dir * 6f, dir, 1f, _cleaveFireRadius, _cleaveFireRatio, false, 0f, MarkStyle.FireSword);
                    }
                    break;
                case Pattern.Miasma:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 at = Player.transform.position + Quaternion.Euler(0f, 120f * i + Random.Range(0f, 60f), 0f) * Vector3.forward * (i == 0 ? 0f : 2f);
                        GhostFire.Create(at, _miasmaRadius, _miasmaLife, AttackPower * _miasmaRatio, _miasmaSlow, 0.6f, Player, Body).name = "마기 장판";
                    }
                    break;
                case Pattern.Bind:
                    LaunchBind();
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            if (_pattern == Pattern.Rain)
                while (_rainDone < _rainCount && _activeTimer >= _rainDone * (_rainTime / _rainCount)) RainSword();
        }

        void SwordWave(Vector3 dir, float ratio)
        {
            var wave = Director.SpawnArrow(transform.position + Vector3.up, dir, _crossSpeed, _crossRange, 0.45f, AttackPower * ratio, Body);
            if (wave == null) return;
            wave.gameObject.name = "천마 검기";
            Fx(new FxCue { Kind = FxKind.Cross, At = wave.transform.position, Dir = dir, Target = wave.transform });
        }

        // 발도 일섬: 앞으로 가늘고 긴 일직선
        void DrawCut()
        {
            Vector3 from = transform.position;
            from.y = 0f;
            if (HitShapes.InRect(from, AimDirection, _drawLength, _drawWidth, Player.transform.position, PlayerRadius))
                Player.TryReceiveHit(AttackPower * _drawRatio, transform.position, HitKnockback, Body);
            Fx(new FxCue { Kind = FxKind.DrawCut, At = from, To = from + AimDirection * _drawLength, Dir = AimDirection, Radius = _drawWidth });
        }

        // 유성: 큰 예고가 플레이어를 따라오다 멈추고 떨어져요
        void BigMeteor()
        {
            Vector3 p = Player.transform.position;
            AddMark(p, Vector3.forward, _meteorFall, _meteorRadius, _meteorRatio, false, 0f, MarkStyle.Fireball, _meteorTrack);
            var m = _marks[_marks.Count - 1];
            Fx(new FxCue { Kind = FxKind.MeteorTrack, At = m.At, Radius = _meteorRadius, Time = _meteorTrack, Style = MarkStyle.Fireball,
                Target = m.Marker != null ? m.Marker.transform : null });
        }

        // 차원 베기: 플레이어 둘레에 균열 셋, 잠시 뒤 균열마다 플레이어 자리를 꿰뚫고 맞은편까지 베어요 (가운데가 가장 위험, 베기 사이 틈으로 피해요)
        void OpenRifts()
        {
            Vector3 p = Player.transform.position;
            p.y = 0f;
            float turn = Random.Range(0f, 120f);
            var points = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                points[i] = p + Quaternion.Euler(0f, turn + 120f * i, 0f) * Vector3.forward * _riftRadius;
                Fx(new FxCue { Kind = FxKind.RiftOpen, At = points[i], Radius = _riftRadius, Time = _riftDelay, Style = MarkStyle.Rift });
            }
            for (int i = 0; i < 3; i++) AddLine(points[i], p - (points[i] - p), _riftDelay, _riftWidth, _riftRatio);
        }

        // 마기 포박: 느린 구슬이 플레이어 쪽으로 날아가요
        void LaunchBind()
        {
            Vector3 dir = ToPlayer();
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : AimDirection;
            Vector3 at = transform.position;
            at.y = 0f;
            var view = new GameObject("마기 포박").transform;
            view.position = at + Vector3.up;
            var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            marker.gameObject.name = "마기 포박 예고";
            marker.Show(TelegraphShapes.Circle(_bindRadius), true);
            marker.SetPose(at, dir);
            marker.SetProgress(1f);
            _orbs.Add(new Orb { At = at, Dir = dir, Left = _bindRange, View = view, Marker = marker });
            Fx(new FxCue { Kind = FxKind.Bind, At = at, Dir = dir, Radius = _bindRadius, Time = _bindRange / Mathf.Max(0.01f, _bindSpeed), Target = view });
        }

        void TickOrbs(float dt)
        {
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                var o = _orbs[i];
                float step = _bindSpeed * dt;
                o.At += o.Dir * step;
                o.Left -= step;
                if (o.View != null) o.View.position = o.At + Vector3.up;
                if (o.Marker != null) o.Marker.SetPose(o.At, o.Dir);
                Vector3 d = Player.transform.position - o.At;
                d.y = 0f;
                // 대시 무적이면 지나가요
                bool hit = d.magnitude <= _bindRadius + PlayerRadius && Player.TryReceiveHit(AttackPower * _bindRatio, o.At, 0f, Body);
                if (hit)
                {
                    Player.ApplySlow(1f, _bindTime); // 묶임: 이동 −100%
                    Fx(new FxCue { Kind = FxKind.Bound, At = Player.transform.position, Dir = o.Dir, Time = _bindTime, Target = Player.transform });
                }
                if (hit || o.Left <= 0f)
                {
                    RemoveOrb(o);
                    _orbs.RemoveAt(i);
                    continue;
                }
                _orbs[i] = o;
            }
        }

        static void RemoveOrb(Orb o)
        {
            if (o.View != null) Destroy(o.View.gameObject);
            if (o.Marker != null) Destroy(o.Marker.gameObject);
        }

        void RainSword()
        {
            _rainDone++;
            Vector2 r = Random.insideUnitCircle * 3f;
            AddMark(Player.transform.position + new Vector3(r.x, 0f, r.y), Vector3.forward, _rainFall, _rainRadius, _rainRatio, false, 0f, MarkStyle.FireSword);
        }

        // ── 바닥 예고 (마검비 · 유성 · 마법진 · 마검 천멸 불길 · 차원 베기)

        static BurstKind BurstOf(MarkStyle s) => s switch
        {
            MarkStyle.Fireball or MarkStyle.FireSword => BurstKind.Fireball,
            MarkStyle.Rune => BurstKind.Rune,
            MarkStyle.Rift => BurstKind.Miasma,
            _ => BurstKind.Slam,
        };

        Telegraph NewMarker()
            => Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);

        // track > 0이면 그동안 플레이어 발밑을 따라오다 멈춘 뒤 fall초 뒤 떨어져요 (Mark 신호는 멈출 때)
        void AddMark(Vector3 at, Vector3 dir, float fall, float radius, float ratio, bool sector, float angle, MarkStyle style, float track = 0f)
        {
            at.y = 0f;
            var marker = NewMarker();
            marker.Show(sector ? TelegraphShapes.Lunge(radius, angle, 0f) : TelegraphShapes.Circle(radius), !sector);
            marker.SetPose(at, dir);
            marker.SetProgress(0f);
            _marks.Add(new Mark { At = at, Dir = dir, Timer = fall, Total = fall, Radius = radius, Ratio = ratio, Sector = sector, Angle = angle, Style = style, Marker = marker, Track = track });
            if (track <= 0f) Fx(new FxCue { Kind = FxKind.Mark, At = at, Dir = dir, Radius = radius, Angle = angle, Time = fall, Sector = sector, Style = style });
        }

        // 직선 예고: a에서 b까지 폭 width (차원 베기 변)
        void AddLine(Vector3 a, Vector3 b, float fall, float width, float ratio)
        {
            a.y = 0f;
            b.y = 0f;
            Vector3 dir = b - a;
            float length = dir.magnitude;
            dir = length > 1e-4f ? dir / length : Vector3.forward;
            var marker = NewMarker();
            marker.Show(TelegraphShapes.Rect(width, length));
            marker.SetPose(a, dir);
            marker.SetProgress(0f);
            _marks.Add(new Mark { At = a, Dir = dir, Timer = fall, Total = fall, Radius = length, Ratio = ratio, Line = true, Width = width, Style = MarkStyle.Rift, Marker = marker });
        }

        void TickMarks(float dt)
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                if (m.Track > 0f)
                {
                    m.Track -= dt;
                    Vector3 p = Player.transform.position;
                    m.At = new Vector3(p.x, 0f, p.z);
                    if (m.Marker != null) m.Marker.SetPose(m.At, m.Dir);
                    if (m.Track <= 0f) // 자리를 굳혔어요: 이제 떨어져요
                        Fx(new FxCue { Kind = FxKind.Mark, At = m.At, Dir = m.Dir, Radius = m.Radius, Angle = m.Angle, Time = m.Total, Sector = m.Sector, Style = m.Style });
                    _marks[i] = m;
                    continue;
                }
                m.Timer -= dt;
                if (m.Marker != null) m.Marker.SetProgress(1f - Mathf.Clamp01(m.Timer / Mathf.Max(0.01f, m.Total)));
                if (m.Timer > 0f)
                {
                    _marks[i] = m;
                    continue;
                }
                if (m.Marker != null) Destroy(m.Marker.gameObject);
                _marks.RemoveAt(i);
                bool hit;
                if (m.Line)
                {
                    Director.SpawnBurst(m.At + m.Dir * (m.Radius * 0.5f), 0.8f, BurstOf(m.Style));
                    Fx(new FxCue { Kind = FxKind.RiftCut, At = m.At, To = m.At + m.Dir * m.Radius, Dir = m.Dir, Radius = m.Width, Style = m.Style });
                    hit = HitShapes.InRect(m.At, m.Dir, m.Radius, m.Width, Player.transform.position, PlayerRadius);
                }
                else
                {
                    Director.SpawnBurst(m.Sector ? m.At + m.Dir * 1.2f : m.At, m.Sector ? 0.8f : m.Radius, BurstOf(m.Style));
                    Fx(new FxCue { Kind = FxKind.MarkFall, At = m.At, Dir = m.Dir, Radius = m.Radius, Angle = m.Angle, Sector = m.Sector, Style = m.Style });
                    hit = m.Sector
                        ? HitShapes.InSector(m.At, m.Dir, m.Radius, m.Angle, Player.transform.position, PlayerRadius)
                        : new Vector2(Player.transform.position.x - m.At.x, Player.transform.position.z - m.At.z).magnitude <= m.Radius + PlayerRadius;
                }
                if (hit) Player.TryReceiveHit(AttackPower * m.Ratio, m.At, -1f, Body);
            }
        }

        // ── 3페이즈: 방 가장자리부터 마기가 차올라 안전 반경이 줄어요

        void TickSafeZone(float dt)
        {
            if (_safeRadius <= 0f || IsDead) return;
            _safeTimer -= dt;
            if (_safeTimer <= 0f && _safeRadius > _safeMin)
            {
                _safeTimer = _safeShrink;
                _safeRadius = Mathf.Max(_safeMin, _safeRadius - _safeStep);
                if (_safeMarker != null) _safeMarker.SetShape(TelegraphShapes.Circle(_safeRadius));
            }
            _safeHurt -= dt;
            Vector3 d = Player.transform.position - _zoneCenter;
            d.y = 0f;
            if (_safeHurt > 0f || d.magnitude <= _safeRadius) return;
            _safeHurt = _safeTick;
            Player.TryReceiveHit(AttackPower * _safeRatio, _zoneCenter, 0f, Body);
        }

        public bool PlayerInMiasma
        {
            get
            {
                if (_safeRadius <= 0f) return false;
                Vector3 d = Player.transform.position - _zoneCenter;
                d.y = 0f;
                return d.magnitude > _safeRadius;
            }
        }

        protected override void OnAttackFinished() => _decisionTimer = _decisionPause;

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        // ── 페이즈

        protected override void OnHitTaken()
        {
            if (IsTransitioning) return;
            if (Phase == 1 && Body.Health <= Body.MaxHealth * _p2Ratio) StartTransition();
            else if (Phase == 2 && Body.Health <= Body.MaxHealth * _p3Ratio) StartTransition();
        }

        void StartTransition()
        {
            if (Attack.IsAttacking) CancelAttack();
            _pattern = Pattern.None;
            _transitionTimer = _transitionTime;
            Body.Targetable = false;
            Director.SpawnBurst(transform.position, 3f, AThousandDawns.Combat.BurstKind.Miasma);
            Fx(new FxCue { Kind = FxKind.Transition, At = transform.position, Radius = 3f, Time = _transitionTime, Phase = Phase + 1 });
        }

        void EnterPhase(int phase)
        {
            Phase = phase;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            if (phase == 2)
            {
                _meteorTimer = _meteorInterval * 0.3f;
                _riftTimer = _riftInterval * 0.5f;
                _runeTimer = _runeInterval * 0.7f;
            }
            if (phase == 3)
            {
                _rainTimer = _rainInterval * 0.4f;
                _cleaveTimer = _cleaveInterval * 0.6f;
                _safeRadius = _safeStart;
                _safeTimer = _safeShrink;
                _safeMarker = NewMarker();
                _safeMarker.gameObject.name = "해체대법 안전 반경";
                _safeMarker.Show(TelegraphShapes.Circle(_safeRadius));
                _safeMarker.SetPose(_zoneCenter, Vector3.forward);
                _safeMarker.SetProgress(0f);
            }
        }

        protected override void OnDied()
        {
            foreach (var m in _marks) if (m.Marker != null) Destroy(m.Marker.gameObject);
            _marks.Clear();
            foreach (var o in _orbs) RemoveOrb(o);
            _orbs.Clear();
            if (_safeMarker != null) Destroy(_safeMarker.gameObject);
            _safeRadius = 0f;
        }
    }
}
