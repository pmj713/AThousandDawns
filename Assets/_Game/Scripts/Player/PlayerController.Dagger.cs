using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 비도 (두 번째 무기, 무기 시트 1~3번 표 · 조작 시트 31~33행). 회귀록 비도 봉우리를 피우면 마을 무기 거치대에서 골라요.
    //   평타: 1·2타 비도 1발 (초당 18칸, 사거리 8칸, 8) · 3타째 15° 간격 부채꼴 3발 (각 8)
    //   남는 비도: 적에게 맞으면 꽂혀 함께 움직이고, 빗나가거나 막히면 바닥에 떨어져요. 6초 뒤 사라짐 (마지막 1.5초 깜빡임), 최대 15개 (넘으면 오래된 것부터)
    //     꽂힌 적이 죽으면 그 자리에 떨어지고, 방을 옮기면 전부 사라져요.
    //   초식 회수 (쿨타임 5초, 시전 0.3초): 방 안의 비도가 전부 돌아오며, 꽂힌 적과 돌아오는 길의 적에게 1개당 초식 피해 25%.
    //     같은 적에게 꽂힌 비도는 한 번의 피해로 합쳐요 (한 적 최대 6개). 남은 비도가 없으면 못 써요.
    //   대시 공격: 대시가 끝나면 지나온 길(4칸)에 1칸 간격으로 4발, 반경 0.6칸 안 적 1명에게 꽂힘 (각 8, 대시 공격 1회로 셈)
    //   절초 만천화우: 3초 동안 비도 24발 (각 6), 강인함 무시 · 공력 배율
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
            public Vector3 Offset;
            public float Life;        // 땅에 떨어지거나 꽂힌 뒤 남은 시간
            public HitDamageInput Hit;
            public HitTag Tag;
            public float Gauge;
            public bool Returning;    // 회수: 나에게 날아오는 중 (피해는 회수한 순간에 들어가요)
            public int Pierced;       // 강기: 뚫고 지나간 적 수
            public bool Vortex;       // 비도 탁랑참: 탁류 소용돌이 속을 도는 중
            public float Angle;       // 소용돌이 속 자리
            public HashSet<Damageable> HitSet; // 이미 뚫고 지나간 적 (다시 맞지 않게)
            public bool Flying => Flight > 0f;
        }

        readonly List<Dagger> _daggers = new List<Dagger>();
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
                var d = NewDagger(transform.position + Vector3.up * 0.8f + dir * 0.6f);
                d.Dir = dir;
                d.Flight = D("knife.range", 8f);
                d.Hit = hit;
                d.Tag = tag;
                d.Gauge = gaugePerHit;
                d.Go.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        Dagger NewDagger(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "비도";
            var col = go.GetComponent<Collider>();
            col.enabled = false; // Destroy는 프레임 끝에 지워져서, 그 사이 캐릭터가 이 충돌 상자 위로 밀려 올라가지 않게 먼저 꺼요
            Destroy(col);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.5f);
            var rend = go.GetComponent<Renderer>();
            if (_daggerMat == null) _daggerMat = new Material(rend.sharedMaterial) { color = new Color(0.82f, 0.84f, 0.88f) };
            rend.sharedMaterial = _daggerMat;
            var d = new Dagger { Go = go, Rend = rend, Life = D("knife.lifetime", 6f) };
            _daggers.Add(d);
            return d;
        }

        void TickDaggers(float dt)
        {
            float speed = D("knife.speed", 18f);
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
                    float move = Mathf.Min(speed * dt, d.Flight);
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
                    else d.Go.transform.position = d.Stuck.transform.position + d.Offset;
                }
                d.Life -= dt;
                if (d.Rend != null) d.Rend.enabled = d.Life > blink || Mathf.Repeat(d.Life, 0.25f) > 0.12f; // 마지막 1.5초 깜빡임
                if (d.Life <= 0f) RemoveDagger(i);
            }
            TickVortex(dt); // 비도 탁랑참: 탁류 소용돌이
            TickHalo(dt);   // 비도 금강장: 비도 호신륜
            TickDaggerFlash(dt); // 비도 죽림섬: 비도 순간이동
            TickDaggerForge(dt); // 융합: 꽂힌 비도 수에 따른 화상·감전·둔화
            // 최대 개수를 넘으면 오래된 것부터
            int max = Mathf.RoundToInt(D("knife.max_count", 15f));
            while (DaggerCount > max)
                for (int i = 0; i < _daggers.Count; i++)
                    if (!_daggers[i].Flying && !_daggers[i].Returning && !_daggers[i].Vortex) { RemoveDagger(i); break; }
        }

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
            float step = D("knife.return_speed", 30f) * dt;
            if (to.magnitude <= Mathf.Max(step, 0.4f)) return true;
            d.Go.transform.position = pos + to.normalized * step;
            d.Go.transform.rotation = Quaternion.LookRotation(to);
            return false;
        }

        void Stick(Dagger d, Damageable target)
        {
            d.Flight = 0f;
            d.Stuck = target;
            d.Offset = d.Go.transform.position - target.transform.position;
            d.Life = D("knife.lifetime", 6f);
        }

        void Drop(Dagger d, Vector3 at)
        {
            d.Flight = 0f;
            d.Stuck = null;
            at.y = 0.05f;
            d.Go.transform.position = at;
            d.Go.transform.rotation = Quaternion.Euler(90f, d.Go.transform.eulerAngles.y, 0f);
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
        }

        // ── 비도 + 죽림섬: 비도 순간이동
        //   꽂히거나 떨어진 비도 자리로 가까운 것부터 차례로 순간이동해요 (0.12초 간격, 최대 6곳). 지나간 직선 위의 적을 베고 (1번에 초식 피해 35%),
        //   그 자리의 비도를 뽑아 손에 거둬요. 순간이동하는 동안 무적. 남은 비도는 그대로 남아요.
        readonly List<Dagger> _flashQueue = new List<Dagger>();
        float _daggerFlashTimer;

        public bool IsDaggerFlashing => _flashQueue.Count > 0;

        void StartDaggerFlash()
        {
            _flashQueue.Clear();
            Vector3 from = transform.position;
            var left = new List<Dagger>();
            foreach (var d in _daggers) if (!d.Flying && !d.Returning && !d.Vortex) left.Add(d);
            int max = Mathf.RoundToInt(D("knife.scroll_flash_max", 6f));
            while (left.Count > 0 && _flashQueue.Count < max) // 가까운 것부터 이어서
            {
                int best = 0;
                for (int i = 1; i < left.Count; i++)
                    if ((Flatten(left[i].Go.transform.position) - Flatten(from)).sqrMagnitude < (Flatten(left[best].Go.transform.position) - Flatten(from)).sqrMagnitude) best = i;
                from = left[best].Go.transform.position;
                _flashQueue.Add(left[best]);
                left.RemoveAt(best);
            }
            _daggerFlashTimer = 0f;
        }

        void TickDaggerFlash(float dt)
        {
            if (_flashQueue.Count == 0) return;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, 0.1f);
            _daggerFlashTimer -= dt;
            if (_daggerFlashTimer > 0f) return;
            _daggerFlashTimer = D("knife.scroll_flash_gap", 0.12f);
            var d = _flashQueue[0];
            _flashQueue.RemoveAt(0);
            if (d.Go == null || !_daggers.Contains(d)) return;
            Vector3 from = transform.position;
            Vector3 to = Flatten(d.Go.transform.position) - Flatten(from);
            if (to.sqrMagnitude > 1e-4f)
            {
                _controller.Move(to); // 벽에는 막혀요
                _facing = to.normalized;
                ApplyFacing();
            }
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
            var targets = FindObjectsByType<Damageable>(FindObjectsSortMode.None);
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
                foreach (var e in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
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
            _vortexCenter = transform.position + _facing * D("knife.vortex_distance", 4f);
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
            var targets = FindObjectsByType<Damageable>(FindObjectsSortMode.None);
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

        // 방 안의 비도가 전부 돌아와요. 맞힌 적 수
        public int RecallDaggers()
        {
            Sfx.Play(SfxId.DaggerRecall);
            float per = _skillDamage * D("knife.recall_ratio", 0.25f);
            int maxPer = Mathf.RoundToInt(D("knife.recall_max_per_target", 6f));
            float pathRadius = D("knife.recall_path_radius", 0.6f);
            var counts = new Dictionary<Damageable, int>();
            Vector3 me = transform.position;
            foreach (var d in _daggers)
            {
                if (d.Flying || d.Returning) continue;
                if (d.Stuck != null && !d.Stuck.IsDead) Add(d.Stuck);
                // 돌아오는 길의 적
                Vector3 from = d.Go.transform.position;
                Breakable.BreakOnSegment(from, me, pathRadius);
                foreach (var e in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                {
                    if (e == d.Stuck || e.IsDead || !e.Targetable) continue;
                    if (DistanceToSegment(e.transform.position, from, me) <= pathRadius + e.BodyRadius) Add(e);
                }
            }
            int hits = 0;
            foreach (var kv in counts)
            {
                var hit = PlayerHit(per * Mathf.Min(kv.Value, maxPer)); // 같은 적에게는 한 번으로 합쳐요
                hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                HitTarget(kv.Key, hit, HitTag.Skill);
                hits++;
            }
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
                d.Stuck = null;
                if (d.Rend != null) d.Rend.enabled = true;
            }
            Gauge = Mathf.Min(GaugeCap, Gauge + hits * _gaugeSkill * GaugeGainMultiplier);
            return hits;

            void Add(Damageable e) => counts[e] = counts.TryGetValue(e, out int n) ? n + 1 : 1;
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
                foreach (var e in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
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
