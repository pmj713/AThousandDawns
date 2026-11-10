using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 청죽객 (죽림 고수, 몬스터 탭 60·103~106·115행): 체력 1100, 공격 18, 이동 5, 3~4칸 거리를 유지.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소 (10초 면역), 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 쾌검 3연격: 가까울 때. 예고 0.5, 앞 부채꼴 2칸 90° × 3번 (0.3초 간격, 2페이즈 0.25), 한 번에 0.8칸 전진, ×0.6, 빈틈 0.8
    // 죽림섬: 8초마다 (2페이즈 6초). 예고 0.6, 플레이어를 가로지르는 직선 4칸 3개 (2페이즈 4개) → 0.25초 간격 순간이동 베기, ×0.8, 빈틈 1.2
    // 대나무 베기: 10초마다. 예고 1.0, 직선 5칸 폭 1칸, ×1.0. 쓰러진 대나무는 6초 동안 벽 (투사체도 막음). 2페이즈 2그루씩
    public sealed class QingzhuBrain : EnemyBrain, IMasterBrain
    {
        public override string AttackName => _pattern switch { Pattern.Combo => "쾌검 3연격", Pattern.Flash => "죽림섬", Pattern.Bamboo => "대나무 베기", _ => null };

        enum Pattern { None, Combo, Flash, Bamboo }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat, _strikes, _flashLines, _bambooLines;
        float _keepMin, _keepMax, _decisionPause, _transitionTime, _phase2Ratio;
        float _comboWindup, _comboRecovery, _comboRatio, _comboRadius, _comboAngle, _comboGap, _comboLunge;
        float _flashWindup, _flashRecovery, _flashRatio, _flashLength, _flashWidth, _flashGap, _flashInterval;
        float _bambooWindup, _bambooRecovery, _bambooRatio, _bambooLength, _bambooWidth, _bambooInterval, _bambooLife, _bambooSpread;
        float _decisionTimer, _transitionTimer, _flashTimer, _bambooTimer, _activeTimer, _lungeLeft;
        bool _hitPlayer;
        readonly List<(Vector3 from, Vector3 to)> _lines = new List<(Vector3, Vector3)>();
        readonly List<Telegraph> _extra = new List<Telegraph>();
        static Material _bambooMat;

        public override EnemyKind Kind => EnemyKind.Qingzhu;
        protected override string Key => "qingzhu";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Combo => (_strikes - 1) * ComboGap + 0.1f,
            Pattern.Flash => _flashLines * _flashGap + 0.05f,
            _ => 0.1f,
        };

        Vector3 _zoneCenter;
        Vector2 _zoneSize;
        bool _hasZone;

        // 결투장 (14×10). 죽림섬·대나무 끝점이 벽 밖으로 나가지 않게 이 안으로 당겨요.
        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
            _hasZone = true;
        }

        Vector3 Clamp(Vector3 p)
        {
            if (!_hasZone) return p;
            float hx = _zoneSize.x * 0.5f - 0.8f, hz = _zoneSize.y * 0.5f - 0.8f;
            return new Vector3(Mathf.Clamp(p.x, _zoneCenter.x - hx, _zoneCenter.x + hx), 0f, Mathf.Clamp(p.z, _zoneCenter.z - hz, _zoneCenter.z + hz));
        }

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        float ComboGap => Phase == 1 ? _comboGap : Tuning("p2_combo_gap", 0.25f);

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _keepMin = Tuning("keep_min", 3f);
            _keepMax = Tuning("keep_max", 4f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _comboWindup = Tuning("combo_windup", 0.5f);
            _comboRecovery = Tuning("combo_recovery", 0.8f);
            _comboRatio = Tuning("combo_ratio", 0.6f);
            _comboRadius = Tuning("combo_radius", 2f);
            _comboAngle = Tuning("combo_angle", 90f);
            _comboGap = Tuning("combo_gap", 0.3f);
            _comboLunge = Tuning("combo_lunge", 0.8f);
            _strikes = 3;
            _flashWindup = Tuning("flash_windup", 0.6f);
            _flashRecovery = Tuning("flash_recovery", 1.2f);
            _flashRatio = Tuning("flash_ratio", 0.8f);
            _flashLength = Tuning("flash_length", 4f);
            _flashWidth = Tuning("flash_width", 1f);
            _flashGap = Tuning("flash_gap", 0.25f);
            _flashInterval = Tuning("flash_interval", 8f);
            _bambooWindup = Tuning("bamboo_windup", 1f);
            _bambooRecovery = Tuning("bamboo_recovery", 0.6f);
            _bambooRatio = Tuning("bamboo_ratio", 1f);
            _bambooLength = Tuning("bamboo_length", 5f);
            _bambooWidth = Tuning("bamboo_width", 1f);
            _bambooInterval = Tuning("bamboo_interval", 10f);
            _bambooLife = Tuning("bamboo_life", 6f);
            _bambooSpread = Tuning("bamboo_spread", 25f);
            _flashTimer = _flashInterval * 0.5f;
            _bambooTimer = _bambooInterval * 0.7f;
        }

        protected override void OnInitialized()
        {
            for (int i = 0; i < 4; i++)
                _extra.Add(Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline));
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_flashTimer > 0f) _flashTimer -= dt;
            if (_bambooTimer > 0f) _bambooTimer -= dt;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next = Pattern.None;
            if (_flashTimer <= 0f) next = Pattern.Flash;
            else if (_bambooTimer <= 0f) next = Pattern.Bamboo;
            else if (distance <= _keepMax + 0.5f) next = Pattern.Combo;
            if (next == Pattern.None) return false;
            if (next == _lastPattern && _repeat >= _maxRepeat && next == Pattern.Combo) return false; // 연속 2번까지: 거리를 다시 잡아요

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            switch (next)
            {
                case Pattern.Combo: WindupTime = _comboWindup; RecoveryTime = _comboRecovery; break;
                case Pattern.Flash:
                    WindupTime = _flashWindup; RecoveryTime = _flashRecovery;
                    _flashLines = Phase == 1 ? 3 : 4;
                    _flashTimer = Phase == 1 ? _flashInterval : Tuning("p2_flash_interval", 6f);
                    break;
                case Pattern.Bamboo:
                    WindupTime = _bambooWindup; RecoveryTime = _bambooRecovery;
                    _bambooLines = Phase == 1 ? 1 : 2;
                    _bambooTimer = _bambooInterval;
                    break;
            }
            return true;
        }

        // 3~4칸을 유지하며 옆걸음
        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            Vector3 v = Strafe(dir) * (MoveSpeed * 0.5f);
            if (d > _keepMax) v += dir * MoveSpeed;
            else if (d < _keepMin) v -= dir * MoveSpeed;
            Move(v, dt);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Combo => TelegraphShapes.Lunge(_comboRadius, _comboAngle, _comboLunge),
            _ => TelegraphShapes.Circle(0.01f), // 죽림섬·대나무는 직선 예고 (_extra)를 써요
        };

        protected override void OnAttackStarted()
        {
            _lines.Clear();
            Vector3 p = Player.transform.position;
            if (_pattern == Pattern.Flash)
            {
                // 플레이어를 가로지르는 직선들 (각도를 고르게 나눠 무작위로 돌림)
                float baseAngle = Random.Range(0f, 180f);
                for (int i = 0; i < _flashLines; i++)
                {
                    var dir = Quaternion.Euler(0f, baseAngle + i * 180f / _flashLines, 0f) * Vector3.forward;
                    if (i % 2 == 1) dir = -dir;
                    _lines.Add((Clamp(p - dir * (_flashLength * 0.5f)), Clamp(p + dir * (_flashLength * 0.5f))));
                }
            }
            else if (_pattern == Pattern.Bamboo)
            {
                Vector3 to = Flat(p - transform.position);
                Vector3 baseDir = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
                for (int i = 0; i < _bambooLines; i++)
                {
                    float angle = _bambooLines == 1 ? 0f : (i == 0 ? -_bambooSpread : _bambooSpread);
                    var dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                    _lines.Add((transform.position, Clamp(transform.position + dir * _bambooLength)));
                }
            }
            for (int i = 0; i < _lines.Count && i < _extra.Count; i++)
            {
                var (from, to) = _lines[i];
                Vector3 d = Flat(to - from);
                _extra[i].Show(TelegraphShapes.Rect(_pattern == Pattern.Flash ? _flashWidth : _bambooWidth, d.magnitude));
                _extra[i].SetPose(from, d.normalized);
                _extra[i].SetProgress(0f);
            }
            if (_pattern != Pattern.Combo) Telegraph.Hide();
        }

        protected override void UpdateTelegraph()
        {
            if (_pattern == Pattern.Combo)
            {
                base.UpdateTelegraph();
                return;
            }
            Telegraph.Hide();
            foreach (var t in _extra) if (t.IsVisible) t.SetProgress(Attack.WindupProgress);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _hitPlayer = false;
            switch (_pattern)
            {
                case Pattern.Combo:
                    _strikesDone = 0;
                    ComboStrike();
                    break;
                case Pattern.Flash:
                    _flashesDone = 0;
                    FlashStrike();
                    break;
                case Pattern.Bamboo:
                    HideExtra();
                    foreach (var (from, to) in _lines)
                    {
                        if (!_hitPlayer && InLine(from, to, _bambooWidth)) _hitPlayer = HitPlayer(_bambooRatio);
                        FellBamboo(from, to);
                    }
                    break;
            }
        }

        int _strikesDone, _flashesDone;

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            switch (_pattern)
            {
                case Pattern.Combo:
                    if (_lungeLeft > 0f)
                    {
                        float step = Mathf.Min(_lungeLeft, _comboLunge / 0.1f * dt);
                        Vector3 to = ToPlayer();
                        if (!(Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f)) MoveRaw(AimDirection * step);
                        _lungeLeft -= step;
                    }
                    while (_strikesDone < _strikes && _activeTimer >= _strikesDone * ComboGap) ComboStrike();
                    break;
                case Pattern.Flash:
                    while (_flashesDone < _lines.Count && _activeTimer >= _flashesDone * _flashGap) FlashStrike();
                    break;
            }
        }

        void ComboStrike()
        {
            _strikesDone++;
            _lungeLeft = _comboLunge;
            if (_strikesDone > 1) Telegraph.Hide();
            if (HitShapes.InSector(transform.position, AimDirection, _comboRadius, _comboAngle, Player.transform.position, PlayerRadius))
                HitPlayer(_comboRatio); // 피격 무적 0.6초라 3번 다 맞지는 않아요
        }

        // 직선 시작점에 나타나 끝으로 베며 지나가요
        void FlashStrike()
        {
            var (from, to) = _lines[_flashesDone];
            if (_flashesDone < _extra.Count) _extra[_flashesDone].Hide();
            _flashesDone++;
            Teleport(to);
            FaceToward(to - from);
            AimDirection = Flat(to - from).normalized;
            if (InLine(from, to, _flashWidth)) HitPlayer(_flashRatio);
        }

        bool InLine(Vector3 from, Vector3 to, float width)
        {
            Vector3 d = Flat(to - from);
            float len = d.magnitude;
            if (len < 1e-3f) return false;
            Vector3 dir = d / len;
            Vector3 rel = Flat(Player.transform.position - from);
            float along = Vector3.Dot(rel, dir);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(dir.z, 0f, -dir.x)));
            return along >= -PlayerRadius && along <= len + PlayerRadius && side <= width * 0.5f + PlayerRadius;
        }

        // 쓰러진 대나무: 6초 동안 벽 (충돌체가 있어서 이동·투사체를 막아요)
        void FellBamboo(Vector3 from, Vector3 to)
        {
            Vector3 d = Flat(to - from);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Fallen Bamboo (쓰러진 대나무)";
            go.transform.position = (from + to) * 0.5f + Vector3.up * 0.35f;
            go.transform.rotation = Quaternion.LookRotation(d.normalized);
            go.transform.localScale = new Vector3(0.5f, 0.7f, d.magnitude);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _bambooMat, new Color(0.24f, 0.61f, 0.45f));
            Destroy(go, _bambooLife);
        }

        void HideExtra()
        {
            foreach (var t in _extra) t.Hide();
        }

        bool HitPlayer(float ratio) => Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body);

        protected override void OnAttackFinished()
        {
            HideExtra();
            _decisionTimer = _decisionPause;
        }

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            HideExtra();
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        // ── 페이즈

        protected override void OnHitTaken()
        {
            if (Phase == 1 && !IsTransitioning && Body.Health <= Body.MaxHealth * _phase2Ratio) StartTransition();
        }

        void StartTransition()
        {
            if (Attack.IsAttacking) CancelAttack();
            HideExtra();
            _pattern = Pattern.None;
            _transitionTimer = _transitionTime;
            Body.Targetable = false; // 전환 연출 동안 무적
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            _flashTimer = 0f; // 바뀌자마자 강해진 죽림섬
        }

        protected override void OnDied()
        {
            foreach (var t in _extra) if (t != null) Destroy(t.gameObject);
        }
    }
}
