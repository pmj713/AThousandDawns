using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 비도 (두 번째 무기, 무기 시트 1~3번 표 · 조작 시트 31~33행). 회귀록 비도 봉우리를 피우면 마을 무기 거치대에서 골라요.
    //   평타: 1·2타 비도 1발 (초당 18칸, 사거리 8칸, 8) · 3타째 15° 간격 부채꼴 3발 (각 8)
    //   남는 비도: 적에게 맞으면 몸에 박혀 함께 움직이고, 빗나가거나 막히면 바닥에 떨어져요. 6초 뒤 사라짐 (마지막 1.5초 깜빡임), 최대 15개 (넘으면 오래된 것부터, 절초 비도는 안 셈)
    //     꽂힌 적이 죽으면 그 자리에 떨어지고, 방을 옮기면 전부 사라져요.
    //   초식 회수 (쿨타임 5초, 시전 0.3초): 방 안의 비도가 전부 돌아오며, 날아오는 비도가 몸을 지나간 적에게 1개당 초식 피해 25%.
    //     비도마다 따로 맞아요 (한 번 돌아올 때 같은 적은 비도 하나에 한 번, 한 적 최대 6개). 꽂혀 있던 비도는 그 적을 빠져나오며 맞혀요. 남은 비도가 없으면 못 써요.
    //   대시 공격: 대시가 끝나면 지나온 길(4칸)에 1칸 간격으로 4발, 반경 0.6칸 안 적 1명에게 꽂힘 (각 8, 대시 공격 1회로 셈)
    //   절초 만천화우: 3초 동안 비도 24발 (각 6)을 적을 쫓지 않고 마우스 커서 쪽으로 3 · 3 · 5 · 5 · 10발씩 다섯 번 부채꼴로 던져요. 강인함 무시 · 공력 배율.
    //     절초 비도는 최대 15개에 안 세요 (그 위로 더 남고, 평타 비도를 밀어내지도 않아요). 아래 "절초 만천화우" 참고.
    // 비도 융합 효과(무기 시트 5번 표)는 PlayerController.ForgeDagger.cs. 대장간 홈 효과가 무기마다 달라요.
    public enum WeaponKind { Sword, Dagger }

    public sealed partial class PlayerController
    {
        sealed class Dagger
        {
            public GameObject Go;
            public Renderer Rend;
            public Vector3 Dir;
            public float Flight;      // 남은 사거리 (0 이하면 땅에)
            public Damageable Stuck;
            public Vector3 Offset;    // 꽂힌 적 기준 자리 (적이 돌면 같이 돌아요)
            public Quaternion StuckRot; // 꽂힌 적 기준 방향
            public float Life;        // 땅에 떨어지거나 꽂힌 뒤 남은 시간
            public HitDamageInput Hit;
            public HitTag Tag;
            public float Gauge;
            public bool Returning;    // 나에게 날아오는 중
            public bool Recall;       // 회수로 돌아오는 중: 지나가는 적에게 회수 피해 (Hit · HitSet을 다시 써요)
            public int Pierced;       // 강기: 뚫고 지나간 적 수
            public bool Vortex;       // 비도 탁랑참: 탁류 소용돌이 속을 도는 중
            public bool Ultimate;     // 절초 만천화우로 생긴 비도: 최대 개수에 안 세요
            public bool Landed;       // 절초 비도가 꽂히거나 떨어졌음 (절초 각인은 한 번만)
            public bool Backflow;     // 회류: 절초 비도가 돌아오며 길의 적을 한 번 더 맞혀요
            public float Angle;       // 소용돌이 속 자리
            public HashSet<Damageable> HitSet; // 이미 뚫고 지나간 적 · 회수로 돌아오며 맞힌 적 (다시 맞지 않게)
            public bool Flying => Flight > 0f;
        }

        readonly List<Dagger> _daggers = new List<Dagger>();
        readonly List<Damageable> _targets = new List<Damageable>(); // 여러 번 도는 대상 목록 (다시 써요)
        Material _daggerMat;

        public WeaponKind Weapon { get; private set; }
        public bool UsesDagger => Weapon == WeaponKind.Dagger;
        // 남은 비도 (날아가는 중이 아닌 것)
        public int DaggerCount
        {
            get
            {
                int n = 0;
                foreach (var d in _daggers) if (!d.Flying && !d.Returning && !d.Vortex) n++;
                return n;
            }
        }
        public int StuckDaggers(Damageable target)
        {
            int n = 0;
            foreach (var d in _daggers) if (d.Stuck == target) n++;
            return n;
        }

        static float D(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        public void SetWeapon(WeaponKind weapon)
        {
            Weapon = weapon;
            ClearDaggers();
        }

        // ── 평타

        // count발을 바라보는 쪽으로 (3발이면 15° 간격 부채꼴)
        public void ThrowDaggers(int count, HitDamageInput hit, HitTag tag, float gaugePerHit)
        {
            Sfx.Play(SfxId.DaggerThrow);
            float spread = D("knife.fan_angle", 15f);
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * spread;
                var dir = Quaternion.Euler(0f, angle, 0f) * _facing;
                var d = NewDagger(transform.position + Vector3.up * 0.8f + dir * 0.6f + new Vector3(_facing.z, 0f, -_facing.x) * 0.25f); // 오른손 쪽에서 (던지는 손)
                d.Dir = dir;
                d.Flight = D("knife.range", 8f);
                d.Hit = hit;
                d.Tag = tag;
                d.Gauge = gaugePerHit;
                d.Go.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        static GameObject _daggerModel;
        static bool _daggerModelLoaded;

        Dagger NewDagger(Vector3 at)
        {
            if (!_daggerModelLoaded) { _daggerModelLoaded = true; _daggerModel = Resources.Load<GameObject>("Models/FlyingDagger"); } // 메인 아트 VARCO 비도 (칼끝 +Z, 충돌체 없음)
            GameObject go;
            Renderer rend;
            if (_daggerModel != null)
            {
                go = Instantiate(_daggerModel, at, Quaternion.identity);
                go.name = "비도";
                rend = go.GetComponentInChildren<Renderer>();
            }
            else // 모델이 없으면 예전 상자
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "비도";
                var col = go.GetComponent<Collider>();
                col.enabled = false; // Destroy는 프레임 끝에 지워져서, 그 사이 캐릭터가 이 충돌 상자 위로 밀려 올라가지 않게 먼저 꺼요
                Destroy(col);
                go.transform.position = at;
                go.transform.localScale = new Vector3(0.08f, 0.08f, 0.5f);
                rend = go.GetComponent<Renderer>();
                if (_daggerMat == null) _daggerMat = new Material(rend.sharedMaterial) { color = new Color(0.82f, 0.84f, 0.88f) };
                rend.sharedMaterial = _daggerMat;
            }
            var d = new Dagger { Go = go, Rend = rend, Life = D("knife.lifetime", 6f) };
            _daggers.Add(d);
            return d;
        }

        void TickDaggers(float dt)
        {
            float speed = D("knife.speed", 18f);
            float ultimateSpeed = speed * D("knife.ultimate_speed_mult", 2.5f); // 만천화우 비도는 훨씬 빨라요
            float blink = D("knife.blink", 1.5f);
            for (int i = _daggers.Count - 1; i >= 0; i--)
            {
                var d = _daggers[i];
                if (d.Go == null) { _daggers.RemoveAt(i); continue; }
                if (d.Vortex) continue; // 소용돌이 속 (TickVortex)
                if (d.Returning)
                {
                    if (ReturnDagger(d, dt)) RemoveDagger(i);
                    continue;
                }
                if (d.Flying)
                {
                    // 한 프레임에 많이 가도 사이의 적·항아리를 건너뛰지 않게 0.25칸씩 나눠 판정해요
                    float move = Mathf.Min((d.Ultimate ? ultimateSpeed : speed) * dt, d.Flight);
                    while (move > 1e-4f && d.Flying)
                    {
                        float part = Mathf.Min(move, 0.25f);
                        FlyDagger(d, part);
                        move -= part;
                    }
                    continue;
                }
                if (!ReferenceEquals(d.Stuck, null))
                {
                    // 적이 죽거나 (자폭처럼) 바로 사라지면 그 자리 바닥에 떨어져요
                    if (d.Stuck == null) Drop(d, d.Go.transform.position);
                    else if (d.Stuck.IsDead) Drop(d, d.Stuck.transform.position);
                    else
                    {
                        var body = d.Stuck.transform;
                        d.Go.transform.SetPositionAndRotation(body.position + body.rotation * d.Offset, body.rotation * d.StuckRot);
                    }
                }
                d.Life -= dt;
                if (d.Rend != null) d.Rend.enabled = d.Life > blink || Mathf.Repeat(d.Life, 0.25f) > 0.12f; // 마지막 1.5초 깜빡임
                if (d.Life <= 0f) RemoveDagger(i);
            }
            TickVortex(dt); // 비도 탁랑참: 탁류 소용돌이
            TickHalo(dt);   // 비도 금강장: 비도 호신륜
            TickDaggerFlash(dt); // 비도 죽림섬: 비도 순간이동
            TickDaggerForge(dt); // 융합: 꽂힌 비도 수에 따른 화상·감전·둔화
            TickDaggerRain(dt);  // 절초 만천화우
            // 최대 개수를 넘으면 오래된 것부터 (절초 비도는 세지도 지우지도 않아요)
            int max = Mathf.RoundToInt(D("knife.max_count", 15f));
            int regular = 0;
            foreach (var d in _daggers) if (IsRegularResting(d)) regular++;
            for (int i = 0; regular > max && i < _daggers.Count;)
            {
                if (!IsRegularResting(_daggers[i])) { i++; continue; }
                RemoveDagger(i);
                regular--;
            }
        }

        static bool IsRegularResting(Dagger d) => !d.Ultimate && !d.Flying && !d.Returning && !d.Vortex;

        void FlyDagger(Dagger d, float step)
        {
            var from = d.Go.transform.position;
            // 벽·엄폐물에 막히면 그 자리에 떨어져요
            if (Physics.Raycast(from, d.Dir, out var block, step, ~0, QueryTriggerInteraction.Ignore)
                && block.collider.GetComponentInParent<Damageable>() == null && !block.collider.transform.IsChildOf(transform))
            {
                Drop(d, block.point - d.Dir * 0.2f);
                return;
            }
            var to = from + d.Dir * step;
            d.Go.transform.position = to;
            d.Flight -= step;
            Breakable.BreakNear(to, D("knife.hit_radius", 0.35f)); // 항아리는 깨고 지나가요
            int found = Physics.OverlapSphereNonAlloc(to, D("knife.hit_radius", 0.35f), _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var target = _overlap[i].GetComponentInParent<Damageable>();
                if (target == null || target.IsDead || !target.Targetable || (d.HitSet != null && d.HitSet.Contains(target))) continue;
                if (d.Ultimate) // 만천화우: 방패도 뚫고 꽂혀요 (강인함 무시)
                {
                    RainStrike(target, 1f);
                    Stick(d, target);
                    RainLanded(d, target);
                    return;
                }
                bool blocked = target.ResolveSituational(transform.position, d.Tag) < 1f; // 방패 정면: 피해가 줄고 꽂히지 않아요
                HitTarget(target, d.Hit, d.Tag);
                Gauge = Mathf.Min(GaugeCap, Gauge + d.Gauge * GaugeGainMultiplier);
                if (blocked)
                {
                    Vector3 back = Flatten(from - target.transform.position);
                    back = back.sqrMagnitude > 1e-4f ? back.normalized : -d.Dir;
                    Drop(d, target.transform.position + back * (target.BodyRadius + 0.35f)); // 적 앞에 떨어져요
                }
                else if (d.Pierced < DaggerPierce) // 강기 융합: 뚫고 지나가 다음 적에게
                {
                    d.Pierced++;
                    (d.HitSet ??= new HashSet<Damageable>()).Add(target);
                    if (DaggerForge(Keyword.Ganggi) >= 2) d.Hit.AttackIncrease += FK("forge.knife_ganggi_pierce_bonus", 0.15f); // 강화판: 관통할 때마다 피해 +
                    continue;
                }
                else
                {
                    Stick(d, target);
                    OnDaggerStuck(target);
                }
                return;
            }
            if (d.Flight <= 0f) Drop(d, to);
        }

        // 회수: 나에게 날아와요 (초당 30칸, 칼끝이 나를 향함). 닿으면 true
        bool ReturnDagger(Dagger d, float dt)
        {
            Vector3 target = transform.position + Vector3.up * 0.8f;
            Vector3 pos = d.Go.transform.position;
            Vector3 to = target - pos;
            float step = (d.Backflow ? D("jinjeon.backflow_speed", 20f) : D("knife.return_speed", 30f)) * dt;
            bool arrived = to.magnitude <= Mathf.Max(step, 0.4f);
            Vector3 next = arrived ? target : pos + to.normalized * step;
            if (d.Recall) RecallHits(d, pos, next); // 이번에 지나간 길의 적
            else if (d.Backflow) BackflowHits(d, pos, next);
            if (arrived) return true;
            d.Go.transform.position = next;
            if (to.sqrMagnitude > 1e-6f) d.Go.transform.rotation = Quaternion.LookRotation(to);
            return false;
        }

        // 회수로 돌아오는 비도가 from → to를 지나며 몸에 닿은 적에게 회수 피해 (비도 하나에 적마다 한 번)
        void RecallHits(Dagger d, Vector3 from, Vector3 to)
        {
            float pathRadius = D("knife.recall_path_radius", 0.6f);
            int maxPer = Mathf.RoundToInt(D("knife.recall_max_per_target", 6f));
            Breakable.BreakOnSegment(from, to, pathRadius); // 항아리도 깨요
            Damageable.CopyAll(_targets);
            foreach (var e in _targets)
            {
                if (e == null || e.IsDead || !e.Targetable || d.HitSet.Contains(e)) continue;
                if (DistanceToSegment(e.transform.position, from, to) > pathRadius + e.BodyRadius) continue;
                d.HitSet.Add(e);
                int n = _recallCounts.TryGetValue(e, out int c) ? c : 0;
                if (n >= maxPer) continue; // 한 번 회수에 한 적 최대 6개
                _recallCounts[e] = n + 1;
                HitTarget(e, d.Hit, HitTag.Skill);
                if (n == 0) Gauge = Mathf.Min(GaugeCap, Gauge + _gaugeSkill * GaugeGainMultiplier); // 게이지는 예전처럼 맞힌 적마다 한 번
            }
        }

        // 적 몸에 박혀요: 충돌체 가장자리가 아니라 몸 가운데 가까이, 가슴 높이에 날아온 쪽에서 칼끝을 안으로 박아요
        void Stick(Dagger d, Damageable target)
        {
            d.Flight = 0f;
            d.Stuck = target;
            var body = target.transform;
            Vector3 pos = d.Go.transform.position;
            Vector3 dir = Flatten(d.Dir);
            if (dir.sqrMagnitude < 1e-4f) dir = Flatten(body.position - pos); // 대시 공격: 떨어진 자리에서 적 쪽으로
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : _facing;
            float height = D("knife.stick_height", 1f);
            if (target.TryGetComponent(out CharacterController cc)) // 작은 적은 몸 높이에 맞춰요 (꽂힐 때 한 번만)
                height = Mathf.Min(height, (cc.center.y + cc.height * 0.5f) * body.lossyScale.y * 0.65f);
            Vector3 at = body.position - dir * (target.BodyRadius * D("knife.stick_depth", 0.35f)) + Vector3.up * height; // 손잡이는 날아온 쪽으로 나와요
            var rot = Quaternion.LookRotation(dir + Vector3.down * 0.15f); // 칼끝이 몸 안쪽 (살짝 아래)
            d.Go.transform.SetPositionAndRotation(at, rot);
            var inv = Quaternion.Inverse(body.rotation);
            d.Offset = inv * (at - body.position);
            d.StuckRot = inv * rot;
            d.Life = D("knife.lifetime", 6f);
        }

        void Drop(Dagger d, Vector3 at)
        {
            d.Flight = 0f;
            d.Stuck = null;
            at.y = 0.05f;
            d.Go.transform.position = at;
            d.Go.transform.rotation = Quaternion.Euler(90f, d.Go.transform.eulerAngles.y, 0f);
            if (d.Ultimate && !d.Landed) RainLanded(d, null); // 빗나간 절초 비도도 떨어진 자리에 각인 (#열양 불길)
        }

        void RemoveDagger(int index)
        {
            if (_daggers[index].Go != null) Destroy(_daggers[index].Go);
            _daggers.RemoveAt(index);
        }

        // 방을 옮기면 전부 사라져요
        public void ClearDaggers()
        {
            for (int i = _daggers.Count - 1; i >= 0; i--) RemoveDagger(i);
            _vortexLeft = 0f;
            _haloLeft = 0f;
            _flashQueue.Clear();
            _rainLeft = 0;
            _rainFinishPending = false;
            _rainBloom.Clear();
        }

        // ── 절초 만천화우
        //   적을 쫓지 않아요. 시전 시간 동안 마우스 커서 쪽으로 다섯 번 부채꼴로 던져요: 3 · 3 · 5 · 5 · 10발 (하단전 24발 기준, 경지 · 폭우로 발 수가 늘면 같은 비율로 늘어요, 사거리 10칸).
        //   조준점이 없으면 (패드 · 조준 = 이동 방향) 바라보는 쪽으로.
        //   맞은 적에게 꽂히고 (강인함 무시 · 공력 배율, 방패도 뚫어요), 빗나가면 바닥에 떨어져 평타 비도처럼 남아요 (회수 · 비도 초식에 써요).
        //   절초 비도는 최대 15개에 안 세서 그 위로 더 남을 수 있고, 평타 비도를 밀어내지도 않아요.
        //   진전: 폭우 (더 많이 · 더 멀리 · 한 발은 약하게) · 산화 (다 던진 뒤 꽂힌 적이 한꺼번에 터져요) · 회류 (다 던진 뒤 절초 비도가 돌아오며 길의 적을 한 번 더)
        //   상단전 마무리: 다 던진 뒤 가장 센 적에게 큰 비도 한 자루. 절초 각인은 비도가 꽂히거나 떨어질 때마다, #뇌정은 마지막 비도가 닿은 뒤 한 번.
        const float RainBloomRadius = 1.5f; // 산화: 터지는 반경
        static readonly int[] RainVolleys = { 3, 3, 5, 5, 10 }; // 한 번에 던지는 비도 수 (플레이테스트 8차)
        readonly int[] _rainVolleySizes = new int[5];
        int _rainLeft, _rainSpawned, _rainDamage, _rainVolley;
        float _rainInterval, _rainElapsed, _rainRange, _rainFinale;
        bool _rainFinishPending;
        Vector3 _rainDir;
        UltimateForm _rainForm;
        HitDamageInput _rainHit;
        readonly Dictionary<Damageable, int> _rainBloom = new Dictionary<Damageable, int>(); // 산화: 적마다 꽂힌 절초 비도 수

        // 던지는 중이거나, 던진 비도가 아직 날아가거나, 회류로 돌아오는 중
        public bool DaggerRaining
        {
            get
            {
                if (_rainLeft > 0 || _rainFinishPending) return true;
                foreach (var d in _daggers) if (d.Backflow) return true;
                return false;
            }
        }

        void BeginDaggerRain(HitDamageInput hit, int count, float duration, float range, UltimateForm form, float finaleRatio)
        {
            _rainHit = hit;
            _rainHit.IgnoreToughness = true;
            // 하단전 24발이면 3 · 3 · 5 · 5 · 10 그대로, 발 수가 늘면 같은 비율로
            float scale = Mathf.Max(0, count) / 24f;
            int total = 0;
            for (int i = 0; i < RainVolleys.Length; i++)
            {
                _rainVolleySizes[i] = count > 0 ? Mathf.Max(1, Mathf.RoundToInt(RainVolleys[i] * scale)) : 0;
                total += _rainVolleySizes[i];
            }
            _rainLeft = total;
            _rainSpawned = 0;
            _rainVolley = 0;
            _rainInterval = D("knife.ultimate_volley_gap", 0.2f); // 묶음 사이 간격 (빠르게 연달아, 시전 시간과 상관없이)
            _rainElapsed = 0f;
            _rainRange = range;
            _rainForm = form;
            _rainFinale = finaleRatio;
            _rainDamage = 0;
            _rainDir = _facing;
            _rainFinishPending = _rainLeft > 0;
            _rainBloom.Clear();
        }

        void TickDaggerRain(float dt)
        {
            if (_rainLeft > 0)
            {
                _rainElapsed += dt;
                // i번째 묶음은 i × 간격에 (첫 묶음은 바로) → 다섯 번
                while (_rainLeft > 0 && _rainVolley < RainVolleys.Length && _rainElapsed >= _rainVolley * _rainInterval) ThrowRainVolley();
            }
            if (!_rainFinishPending || _rainLeft > 0) return;
            foreach (var d in _daggers) if (d.Ultimate && d.Flying) return; // 마지막 비도까지 꽂히거나 떨어지면
            _rainFinishPending = false;
            if (_rainForm == UltimateForm.Bloom) BurstRainBloom();
            if (_rainFinale > 0f) RainFinale();
            if (_rainForm == UltimateForm.Backflow) StartBackflow();
            OnSwordRainFinished(_rainDamage); // #뇌정 각인 · 뇌정 방울
        }

        // 지금 내 자리에서 마우스 커서 쪽으로 한 묶음 (부채꼴로 고르게)
        void ThrowRainVolley()
        {
            Vector3 aim = _facing;
            if (_input != null && _input.TryGetAimPoint(out Vector3 point))
            {
                Vector3 to = point - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f) aim = to.normalized;
            }
            int size = Mathf.Min(_rainVolleySizes[_rainVolley], _rainLeft);
            float step = D("knife.ultimate_fan_step", 6f), fan = Mathf.Min(step * (size - 1), D("knife.ultimate_fan_max", 60f));
            for (int i = 0; i < size; i++)
            {
                float angle = size > 1 ? -fan * 0.5f + fan * i / (size - 1) : 0f;
                ThrowRainDagger(Quaternion.Euler(0f, angle, 0f) * aim, i == 0);
            }
            _rainVolley++;
            if (_rainVolley >= RainVolleys.Length) _rainLeft = 0;
        }

        void ThrowRainDagger(Vector3 dir, bool sound)
        {
            var d = NewDagger(transform.position + Vector3.up * 0.8f + dir * 0.6f);
            d.Dir = dir;
            d.Flight = _rainRange;
            d.Hit = _rainHit;
            d.Tag = HitTag.Ultimate;
            d.Ultimate = true;
            d.Go.transform.rotation = Quaternion.LookRotation(dir);
            if (sound) Sfx.Play(SfxId.DaggerThrow); // 묶음마다 한 번 (소리가 겹치지 않게)
            _rainLeft--;
            _rainSpawned++;
        }

        // 한 발 피해 × ratio (강인함 무시, 방어력 적용)
        void RainStrike(Damageable target, float ratio)
        {
            if (target == null || target.IsDead || !target.Targetable) return;
            var hit = _rainHit;
            hit.SpecialMultiplier *= ratio;
            hit.Toughness = target.Toughness;
            hit.Defense = target.Defense;
            int damage = DamageFormula.ToEnemy(hit, _rules);
            target.ApplyHit(damage);
            _rainDamage += damage;
        }

        // 절초 비도가 꽂히거나 (target) 떨어졌을 때 (null): 절초 각인 · 산화 셈
        void RainLanded(Dagger d, Damageable target)
        {
            d.Landed = true;
            if (target != null && _rainForm == UltimateForm.Bloom) _rainBloom[target] = _rainBloom.TryGetValue(target, out int n) ? n + 1 : 1;
            OnSwordLanded(target, Flatten(target != null ? target.transform.position : d.Go.transform.position));
        }

        // 산화: 절초 비도가 꽂힌 적마다 (꽂힌 수 × 비율) 피해로 둘레까지 터져요
        void BurstRainBloom()
        {
            float ratio = D("jinjeon.bloom_ratio", 0.5f);
            Damageable.CopyAll(_targets);
            foreach (var kv in _rainBloom)
            {
                if (kv.Key == null || kv.Key.IsDead) continue;
                Vector3 at = Flatten(kv.Key.transform.position);
                foreach (var e in _targets)
                    if (e != null && (Flatten(e.transform.position) - at).magnitude <= RainBloomRadius + e.BodyRadius) RainStrike(e, ratio * kv.Value);
            }
            _rainBloom.Clear();
        }

        // 상단전 마무리: 가장 센 적에게 큰 비도 한 자루가 꽂혀요
        void RainFinale()
        {
            Damageable best = null;
            foreach (var e in Damageable.All())
                if (!e.IsDead && e.Targetable && (best == null || e.Health > best.Health)) best = e;
            if (best == null) return;
            RainStrike(best, _rainFinale);
            var d = NewDagger(best.transform.position + Vector3.up);
            d.Ultimate = d.Landed = true;
            d.Dir = Flatten(best.transform.position - transform.position);
            Stick(d, best);
            d.Go.transform.localScale *= 3f;
        }

        // 회류: 남은 절초 비도가 모두 나에게 돌아와요 (꽂힌 비도는 적을 빠져나오며 맞혀요)
        void StartBackflow()
        {
            foreach (var d in _daggers)
            {
                if (!d.Ultimate || d.Flying || d.Returning || d.Vortex) continue;
                d.Stuck = null;
                d.Returning = true;
                d.Backflow = true;
                (d.HitSet ??= new HashSet<Damageable>()).Clear();
                if (d.Rend != null) d.Rend.enabled = true;
            }
        }

        // 회류로 돌아오는 비도가 from → to를 지나며 닿은 적을 한 번씩 맞혀요
        void BackflowHits(Dagger d, Vector3 from, Vector3 to)
        {
            float radius = D("knife.recall_path_radius", 0.6f);
            Damageable.CopyAll(_targets);
            foreach (var e in _targets)
            {
                if (e == null || e.IsDead || !e.Targetable || d.HitSet.Contains(e)) continue;
                if (DistanceToSegment(e.transform.position, from, to) > radius + e.BodyRadius) continue;
                d.HitSet.Add(e);
                RainStrike(e, 1f);
            }
        }

        // ── 비도 + 죽림섬: 비도 순간이동
        //   적에게 꽂힌 비도 자리로 가까운 것부터 차례로 순간이동해요 (0.12초 간격, 최대 6곳). 꽂힌 비도가 하나도 없을 때만 바닥에 떨어진 비도로 가요.
        //   지나간 직선 위의 적을 베고 (1번에 초식 피해 35%), 그 자리의 비도를 뽑아 손에 거둬요. 순간이동하는 동안 무적. 남은 비도는 그대로 남아요.
        //   한 번에 8칸 (knife.scroll_flash_reach) 넘게 떨어졌거나 · 가는 길이 벽에 막혔거나 · 꽂힌 적이 숨은 비도로는 가지 않아요. 꽂힌 비도는 그 적 몸 바로 앞에 서요.
        readonly List<Dagger> _flashQueue = new List<Dagger>();
        float _daggerFlashTimer;

        public bool IsDaggerFlashing => _flashQueue.Count > 0;

        void StartDaggerFlash()
        {
            _flashQueue.Clear();
            var stuck = new List<Dagger>();
            var ground = new List<Dagger>();
            foreach (var d in _daggers) if (FlashReady(d)) (d.Stuck != null ? stuck : ground).Add(d);
            QueueFlash(stuck); // 적에게 꽂힌 비도가 먼저예요
            if (_flashQueue.Count == 0) QueueFlash(ground); // 갈 만한 꽂힌 비도가 하나도 없을 때만 바닥 비도
            _daggerFlashTimer = 0f;
        }

        // 가까운 것부터 이어서 (한 번에 knife.scroll_flash_reach칸 넘게 떨어진 비도는 빼요: 방 건너편 적에게 꽂힌 비도로 날아가던 것)
        void QueueFlash(List<Dagger> left)
        {
            Vector3 from = transform.position;
            int max = Mathf.RoundToInt(D("knife.scroll_flash_max", 6f));
            while (left.Count > 0 && _flashQueue.Count < max)
            {
                int best = NearestFlash(left, from);
                if (best < 0) break;
                from = left[best].Go.transform.position;
                _flashQueue.Add(left[best]);
                left.RemoveAt(best);
            }
        }

        float FlashReach => D("knife.scroll_flash_reach", 8f);

        // 순간이동할 수 있는 비도: 남아 있고, 꽂힌 적이 숨지 않았어요 (숨거나 사라졌다 다른 데서 나오는 적을 따라가지 않게)
        static bool FlashReady(Dagger d) =>
            d.Go != null && !d.Flying && !d.Returning && !d.Vortex && (d.Stuck == null || d.Stuck.Targetable);

        // from에서 가장 가까운 비도 (reach 안). 없으면 -1
        int NearestFlash(List<Dagger> list, Vector3 from)
        {
            float reach = FlashReach, bestDist = reach * reach;
            int best = -1;
            for (int i = 0; i < list.Count; i++)
            {
                float dist = (Flatten(list[i].Go.transform.position) - Flatten(from)).sqrMagnitude;
                if (dist <= bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        // 비도로 순간이동할 자리: 바닥 비도는 그 자리, 꽂힌 비도는 그 적 몸 바로 앞 (적 몸 한가운데 서지 않게).
        // 가는 길이 벽 · 엄폐물에 막혔으면 false: 예전엔 그대로 Move해서 벽을 따라 남은 거리만큼 미끄러져 엉뚱한 곳에 섰어요
        bool FlashSpot(Dagger d, Vector3 from, out Vector3 spot, out Vector3 dir)
        {
            from = Flatten(from);
            Vector3 at = Flatten(d.Stuck != null ? d.Stuck.transform.position : d.Go.transform.position);
            Vector3 to = at - from;
            float dist = to.magnitude;
            spot = from;
            dir = dist > 1e-3f ? to / dist : _facing;
            if (dist > FlashReach) return false;
            float stop = d.Stuck != null ? Mathf.Max(0f, dist - (d.Stuck.BodyRadius + BodyRadius + 0.05f)) : dist;
            if (stop < 0.05f) return true; // 이미 곁이에요
            float free = AThousandDawns.Enemies.ObstacleProbe.Distance(from + Vector3.up * 0.5f, dir, BodyRadius * 0.9f, stop); // 적 · 나는 장애물로 안 쳐요
            if (free < stop - 0.6f) return false;
            spot = from + dir * Mathf.Min(stop, free); // 벽에 붙어 떨어진 비도는 벽 바로 앞까지 (베기는 1칸 너머까지 닿아요)
            return true;
        }

        void TickDaggerFlash(float dt)
        {
            if (_flashQueue.Count == 0) return;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, 0.1f);
            _daggerFlashTimer -= dt;
            if (_daggerFlashTimer > 0f) return;
            _daggerFlashTimer = D("knife.scroll_flash_gap", 0.12f);
            Vector3 from = transform.position;
            // 순간이동할 때마다 지금 자리에서 가장 가까운 비도로 다시 골라요 (시전 때 정한 차례는 적이 움직이면 엉뚱해져요).
            // 사라졌거나 · 꽂힌 적이 숨었거나 · 너무 멀어졌거나 · 길이 막힌 비도는 건너뛰어요 (그 비도는 그대로 남아요)
            Dagger d = null;
            Vector3 spot = from, dir = _facing;
            while (d == null && _flashQueue.Count > 0)
            {
                for (int i = _flashQueue.Count - 1; i >= 0; i--)
                    if (!_daggers.Contains(_flashQueue[i]) || !FlashReady(_flashQueue[i])) _flashQueue.RemoveAt(i);
                int best = NearestFlash(_flashQueue, from);
                if (best < 0) { _flashQueue.Clear(); break; }
                var pick = _flashQueue[best];
                _flashQueue.RemoveAt(best);
                if (FlashSpot(pick, from, out spot, out dir)) d = pick;
            }
            if (d == null) return;
            Vector3 move = spot - Flatten(from);
            if (move.sqrMagnitude > 1e-4f) _controller.Move(move); // 길은 미리 봤으니 벽을 따라 미끄러지지 않아요 (적은 서로 통과)
            _facing = dir;
            ApplyFacing();
            Sfx.Play(SfxId.BambooFlash);
            SkillFx(new SkillFxCue { Kind = SkillFxKind.FlashDash, From = from, To = transform.position, Dir = _facing });
            if (swingFlash != null) swingFlash.Show(transform.position, -_facing, 1.2f, 90f, 0.12f);
            var hit = PlayerHit(_skillDamage * D("knife.scroll_flash_ratio", 0.35f));
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            int count = HitLine(from, transform.position + _facing * 1f, GameConfig.Get("skill_flash.width", 1.2f), hit, HitTag.Skill); // 비도 자리 너머 1칸까지 (적 몸에 막혀 앞에 서도 베요)
            Gauge = Mathf.Min(GaugeCap, Gauge + count * _gaugeSkill * GaugeGainMultiplier);
            RemoveDagger(_daggers.IndexOf(d)); // 뽑아 손에 거둬요
        }

        // ── 비도 + 붕산권: 비도 솟구치기
        //   땅을 내리쳐 내 둘레 충격파 (반경 2칸, 초식 피해 50%, 밀어냄). 둘레 5칸 안의 꽂히거나 떨어진 비도가 솟구쳐 흩어지며,
        //   비도마다 반경 1.5칸 안 적을 찔러요 (비도 1개당 35%). 솟구친 비도는 손으로 돌아와요.
        void DaggerBurst()
        {
            Sfx.Play(SfxId.Quake);
            QuakeBurst(D("knife.scroll_quake_center", 2f), D("knife.scroll_quake_center_ratio", 0.5f), GameConfig.Get("skill_quake.knockback", 2f));
            float reach = D("knife.scroll_quake_radius", 5f), pop = D("knife.scroll_quake_pop", 1.5f);
            var hit = PlayerHit(_skillDamage * D("knife.scroll_quake_ratio", 0.35f));
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            Damageable.CopyAll(_targets);
            var targets = _targets;
            Vector3 me = Flatten(transform.position);
            int hits = 0;
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning || d.Vortex) continue;
                Vector3 at = Flatten(d.Go.transform.position);
                if ((at - me).magnitude > reach) continue;
                SkillFx(new SkillFxCue { Kind = SkillFxKind.QuakeWave, From = at, Radius = pop });
                foreach (var e in targets)
                {
                    if (e == null || e.IsDead || !e.Targetable) continue;
                    if ((Flatten(e.transform.position) - at).magnitude > pop + e.BodyRadius) continue;
                    HitTarget(e, hit, HitTag.Skill);
                    hits++;
                }
                d.Stuck = null;
                d.Go.transform.position = at + Vector3.up * 1.5f; // 솟구쳤다가
                d.Returning = true;                               // 손으로 돌아와요
                if (d.Rend != null) d.Rend.enabled = true;
            }
            Gauge = Mathf.Min(GaugeCap, Gauge + hits * _gaugeSkill * 0.5f * GaugeGainMultiplier);
        }

        // ── 비도 + 금강장: 비도 호신륜
        //   방 안 비도를 모두 끌어와 3초 동안 내 둘레 (반경 1.6칸)를 돌게 해요. 0.3초마다 고리에 닿는 적을 베고 (비도 1개당 초식 피해 4%, 최대 12개),
        //   고리 안으로 날아오는 화살·작살·검기·염주 같은 투사체를 막아요. 끝나면 손으로 돌아와요.
        float _haloLeft, _haloTick;
        public bool IsHaloing => _haloLeft > 0f;

        void StartDaggerHalo()
        {
            _haloLeft = D("knife.scroll_halo_time", 3f);
            _haloTick = 0f;
            int n = 0;
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning || d.Vortex) continue;
                d.Vortex = true; // 고리 속을 돌아요 (소용돌이와 같은 표시: 남은 비도로 안 셈)
                d.Stuck = null;
                d.Angle = n++ * 137.5f;
                if (d.Rend != null) d.Rend.enabled = true;
            }
            Sfx.Play(SfxId.DaggerRecall);
            SkillFx(new SkillFxCue { Kind = SkillFxKind.PalmShield, From = transform.position, Count = n });
        }

        void TickHalo(float dt)
        {
            if (_haloLeft <= 0f) return;
            _haloLeft -= dt;
            float radius = D("knife.scroll_halo_orbit", 1.6f);
            Vector3 center = transform.position + Vector3.up * 0.8f;
            int count = 0;
            foreach (var d in _daggers)
            {
                if (!d.Vortex) continue;
                count++;
                d.Angle += D("knife.scroll_halo_spin", 360f) * dt;
                Vector3 at = center + Quaternion.Euler(0f, d.Angle, 0f) * Vector3.forward * radius;
                d.Go.transform.position = Vector3.MoveTowards(d.Go.transform.position, at, D("knife.return_speed", 30f) * dt);
                d.Go.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, d.Angle + 90f, 0f) * Vector3.forward);
            }
            // 날아오는 투사체를 막아요
            float block = radius + 0.6f;
            foreach (var arrow in FindObjectsByType<AThousandDawns.Enemies.Arrow>(FindObjectsSortMode.None))
                if ((Flatten(arrow.transform.position) - Flatten(transform.position)).magnitude <= block) Destroy(arrow.gameObject);
            foreach (var bead in FindObjectsByType<AThousandDawns.Enemies.BeadThrow>(FindObjectsSortMode.None))
                if ((Flatten(bead.transform.position) - Flatten(transform.position)).magnitude <= block) Destroy(bead.gameObject);
            _haloTick -= dt;
            if (_haloTick <= 0f && count > 0)
            {
                _haloTick = D("knife.scroll_halo_tick", 0.3f);
                var hit = PlayerHit(_skillDamage * D("knife.scroll_palm_shield", 0.04f) * Mathf.Min(count, Mathf.RoundToInt(D("knife.vortex_max", 12f))));
                hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                int hits = 0;
                foreach (var e in Damageable.All())
                {
                    if (e == null || e.IsDead || !e.Targetable) continue;
                    float dist = (Flatten(e.transform.position) - Flatten(transform.position)).magnitude;
                    if (dist > radius + 0.6f + e.BodyRadius || dist < 0.3f) continue;
                    HitTarget(e, hit, HitTag.Skill);
                    hits++;
                }
                Gauge = Mathf.Min(GaugeCap, Gauge + hits * _gaugeSkill * 0.25f * GaugeGainMultiplier);
            }
            if (_haloLeft > 0f) return;
            foreach (var d in _daggers) // 끝: 손으로 돌아와요
                if (d.Vortex) { d.Vortex = false; d.Returning = true; }
        }

        // ── 비도 + 탁랑참: 탁류 소용돌이
        //   회수 대신, 방 안 비도가 모두 바라보는 쪽 4칸 지점으로 날아가 2초 동안 소용돌이쳐요 (반경 1.2칸으로 돌아요).
        //   둘레 3칸 안 적을 가운데로 끌어당기고 (보스·고수는 안 끌림), 반경 1.8칸 안 적을 0.4초마다 벱니다 (비도 1개당 초식 피해 6%, 최대 12개).
        //   끝나면 비도가 모두 손으로 돌아와요. 비도를 많이 깔아 둘수록 세져요.
        float _vortexLeft, _vortexTick;
        Vector3 _vortexCenter;

        public bool IsVortexing => _vortexLeft > 0f;
        public Vector3 VortexCenter => _vortexCenter;

        void StartVortex()
        {
            _vortexCenter = transform.position + _facing * D("knife.vortex_distance", 4f); // 패드 · 조준점이 없을 땐 앞쪽 4칸
            if (_input != null && _input.TryGetAimPoint(out Vector3 aim)) // 마우스 커서 자리가 소용돌이 가운데 (너무 멀면 vortex_max_range까지)
            {
                Vector3 to = aim - transform.position;
                to.y = 0f;
                _vortexCenter = transform.position + Vector3.ClampMagnitude(to, D("knife.vortex_max_range", 9f));
            }
            _vortexCenter.y = 0.8f;
            _vortexLeft = D("knife.vortex_time", 2f);
            _vortexTick = 0f;
            int n = 0;
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning) continue;
                d.Vortex = true;
                d.Stuck = null;
                d.Angle = n++ * 137.5f; // 고르게 흩어져 돌아요
                if (d.Rend != null) d.Rend.enabled = true;
            }
            Sfx.Play(SfxId.DaggerRecall);
        }

        void TickVortex(float dt)
        {
            if (_vortexLeft <= 0f) return;
            _vortexLeft -= dt;
            float radius = D("knife.vortex_orbit", 1.2f);
            float spin = D("knife.vortex_spin", 540f);
            int count = 0;
            foreach (var d in _daggers)
            {
                if (!d.Vortex) continue;
                count++;
                d.Angle += spin * dt;
                Vector3 at = _vortexCenter + Quaternion.Euler(0f, d.Angle, 0f) * Vector3.forward * radius;
                Vector3 pos = d.Go.transform.position;
                d.Go.transform.position = Vector3.MoveTowards(pos, at, D("knife.return_speed", 30f) * dt); // 먼저 날아와 모여요
                Vector3 tangent = Quaternion.Euler(0f, d.Angle + 90f, 0f) * Vector3.forward;
                d.Go.transform.rotation = Quaternion.LookRotation(tangent);
            }
            float pullRadius = D("knife.vortex_pull_radius", 3f), pull = D("knife.vortex_pull", 3f);
            float cutRadius = D("knife.vortex_radius", 1.8f);
            Vector3 c = Flatten(_vortexCenter);
            Damageable.CopyAll(_targets);
            var targets = _targets;
            foreach (var e in targets)
            {
                if (e == null || e.IsDead || !e.Targetable) continue;
                Vector3 to = c - Flatten(e.transform.position);
                if (to.magnitude > pullRadius || to.magnitude < 0.3f) continue;
                if (e.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain)) brain.Displace(to.normalized * Mathf.Min(pull * dt, to.magnitude));
            }
            _vortexTick -= dt;
            if (_vortexTick <= 0f && count > 0)
            {
                _vortexTick = D("knife.vortex_tick", 0.4f);
                var hit = PlayerHit(_skillDamage * D("knife.vortex_ratio", 0.06f) * Mathf.Min(count, Mathf.RoundToInt(D("knife.vortex_max", 12f))));
                hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                int hits = 0;
                foreach (var e in targets)
                {
                    if (e == null || e.IsDead || !e.Targetable) continue;
                    if ((Flatten(e.transform.position) - c).magnitude > cutRadius + e.BodyRadius) continue;
                    HitTarget(e, hit, HitTag.Skill);
                    hits++;
                }
                Gauge = Mathf.Min(GaugeCap, Gauge + hits * _gaugeSkill * 0.25f * GaugeGainMultiplier);
            }
            if (_vortexLeft > 0f) return;
            foreach (var d in _daggers) // 끝: 손으로 돌아와요
                if (d.Vortex) { d.Vortex = false; d.Returning = true; }
        }

        // ── 초식 회수

        public bool CanRecall => DaggerCount > 0;

        readonly Dictionary<Damageable, int> _recallCounts = new Dictionary<Damageable, int>(); // 이번 회수에 적마다 맞은 비도 수

        // 방 안의 비도가 전부 돌아와요. 돌아오는 비도 수 (피해는 날아오는 비도가 적을 지나갈 때 비도마다 들어가요)
        public int RecallDaggers()
        {
            Sfx.Play(SfxId.DaggerRecall);
            var hit = PlayerHit(_skillDamage * D("knife.recall_ratio", 0.25f)); // 비도 1개 피해
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            _recallCounts.Clear();
            var stuckTargets = new List<Damageable>(); int returning = 0;
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning) continue;
                returning++;
                if (d.Stuck != null && !stuckTargets.Contains(d.Stuck)) stuckTargets.Add(d.Stuck);
            }
            OnDaggersRecalled(returning, stuckTargets); // 융합: 빙백 강화판 둔화 · 금강 보호막
            // 비도는 나에게 날아와요 (움직이는 나를 따라옴, 닿으면 사라짐)
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning) continue;
                d.Returning = true;
                d.Recall = true;
                d.Hit = hit;
                (d.HitSet ??= new HashSet<Damageable>()).Clear(); // 꽂혀 있던 적도 몸 안에서 빠져나오며 한 번 맞아요
                d.Stuck = null;
                if (d.Rend != null) d.Rend.enabled = true;
            }
            return returning;
        }

        // ── 대시 공격: 지나온 길에 1칸 간격으로 떨어뜨려요

        int DropDaggersOnPath(HitDamageInput hit)
        {
            int count = Mathf.RoundToInt(D("knife.dash_attack_count", 4f));
            float radius = D("knife.dash_radius", 0.6f);
            Vector3 from = _dashStart, to = transform.position;
            int hits = 0;
            var used = new HashSet<Damageable>();
            for (int i = 0; i < count; i++)
            {
                Vector3 at = count > 1 ? Vector3.Lerp(from, to, (i + 1f) / count) : to;
                var d = NewDagger(at + Vector3.up * 0.8f);
                Breakable.BreakNear(at, radius);
                Damageable best = null;
                float bestDist = float.MaxValue;
                foreach (var e in Damageable.All())
                {
                    if (e.IsDead || !e.Targetable) continue;
                    float dist = Vector3.Distance(Flatten(e.transform.position), Flatten(at)) - e.BodyRadius;
                    if (dist <= radius && dist < bestDist) { best = e; bestDist = dist; }
                }
                if (best == null)
                {
                    Drop(d, at);
                    continue;
                }
                HitTarget(best, hit, HitTag.DashAttack);
                Stick(d, best);
                OnDaggerStuck(best);
                if (used.Add(best)) hits++;
            }
            return hits;
        }

        static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // 바닥은 늘 높이 0이에요. 무엇에 밀려 떠오르면 (평타가 바닥의 적에게 안 닿아요) 바로 내려놓아요.
        void LateUpdate()
        {
            if (_controller == null || Mathf.Abs(transform.position.y) < 0.05f) return;
            _controller.enabled = false;
            transform.position = Flatten(transform.position);
            _controller.enabled = true;
        }
    }
}
