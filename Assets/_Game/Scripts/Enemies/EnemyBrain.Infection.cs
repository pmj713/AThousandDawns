using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 차원 감염 정예: 차원이 겹치며 반대편 세계의 능력을 얻은 정예예요.
    //   판타지 적 → 무공: 경공 (elite.gyeonggong_interval마다 순식간에 몇 칸 내달려요: 멀면 파고들고, 붙어 있으면 옆으로 돌아 들어가요)
    //   무협 적 → 마법: 화염구 (elite.fireball_interval마다 플레이어 쪽으로 불화살 한 발을 곧게 쏴요, 맞으면 화상 1중첩)
    //                   (혈폭자 핏덩이와 겹쳐서 발밑 폭발은 뺐어요, 플레이테스트 11차)
    //                   순간이동 (elite.blink_interval마다 사라졌다 나타나요: 멀면 플레이어 옆, 붙어 있으면 등 뒤)
    // 시트 fantasy.infection = 0이면 나오지 않아요. 예전 테스트는 InfectionOverride = 0으로 끄고 돌려요.
    public abstract partial class EnemyBrain
    {
        public static int InfectionOverride = -1; // 테스트용 (0 끔 · 1 켬)
        static bool InfectionOn => InfectionOverride >= 0 ? InfectionOverride == 1 : GameConfig.GetOptional("fantasy.infection", 1f) > 0.5f;

        public static bool IsInfection(EliteTrait trait) => trait == EliteTrait.Gyeonggong || trait == EliteTrait.Fireball || trait == EliteTrait.Blink;

        static bool InfectionAllows(EnemyKind kind, EliteTrait trait)
        {
            bool fantasy = (int)kind >= (int)EnemyKind.Slime;
            if (IsFantasyTrait(trait)) return InfectionOn && fantasy; // 마법 방벽 · 저주 · 분노: 판타지 적만 (EnemyBrain.FantasyTraits)
            if (!IsInfection(trait)) return true;
            if (!InfectionOn) return false;
            return trait == EliteTrait.Gyeonggong ? fantasy : !fantasy;
        }

        public static event System.Action<EnemyBrain, Vector3, Vector3> Blinked; // 순간이동한 적, 떠난 자리, 나타난 자리 (아트)
        public static event System.Action<EnemyBrain, Vector3> Dashed;          // 경공으로 내달린 적, 출발 자리 (아트)
        public static event System.Action<EnemyBrain, Vector3, Vector3> QinggongMoved; // 누가, 떠난 자리, 도착 자리 (경공이 끝난 순간, 아트: 잔상)
        public static event System.Action<EnemyBrain, Vector3, Vector3> FireboltShot;  // 화염구 정예가 쏜 불화살: 쏜 적, 쏜 자리, 방향 (아트: 투사체는 EnemyBrain.Firebolt)

        const float DashProbeHeight = 0.5f, DashMaxStep = 0.25f, DashWallGap = 0.1f;
        float _infectionTimer = -1f, _dashLeft;
        Vector3 _dashDir, _dashFrom;

        void TickInfection(float dt)
        {
            if (_dashLeft > 0f) // 경공 내달리기 중
            {
                float total = Mathf.Min(_dashLeft, GameConfig.GetOptional("elite.gyeonggong_speed", 18f) * dt);
                bool stop = false;
                while (total > 1e-4f && !stop) // 한 번에 크게 옮기지 않게 잘게 나눠요 (벽을 뚫고 나가지 않게)
                {
                    float step = Mathf.Min(total, DashMaxStep);
                    Vector3 before = transform.position;
                    var flags = MoveRaw(_dashDir * step);
                    Vector3 moved = transform.position - before;
                    moved.y = 0f;
                    // 벽에 붙어 있다가 밀려 나가며 벽 너머로 넘어갔으면 되돌려요
                    if (moved.sqrMagnitude > 1e-6f && CrossedWall(before, transform.position))
                    {
                        Teleport(before);
                        stop = true;
                    }
                    total -= step;
                    _dashLeft -= step;
                    if ((flags & CollisionFlags.Sides) != 0) stop = true;
                }
                if (stop || _dashLeft <= 1e-4f)
                {
                    _dashLeft = 0f;
                    QinggongMoved?.Invoke(this, _dashFrom, transform.position);
                    Vector3 back = ToPlayer(); // 내달린 뒤엔 노리던 쪽 (플레이어나 다른 진영 적)을 다시 봐요 (옆으로 내달린 방향 그대로 등 돌리고 휘두르던 것)
                    if (back.sqrMagnitude > 1e-4f) FaceToward(back);
                }
                return;
            }
            EliteTrait trait = HasTrait(EliteTrait.Gyeonggong) ? EliteTrait.Gyeonggong : HasTrait(EliteTrait.Fireball) ? EliteTrait.Fireball : HasTrait(EliteTrait.Blink) ? EliteTrait.Blink : EliteTrait.None;
            if (trait == EliteTrait.None || !_awake || Player == null || Player.IsDead) return;
            float interval = trait == EliteTrait.Gyeonggong ? GameConfig.GetOptional("elite.gyeonggong_interval", 4f)
                : trait == EliteTrait.Fireball ? GameConfig.GetOptional("elite.fireball_interval", 5f)
                : GameConfig.GetOptional("elite.blink_interval", 6f);
            if (_infectionTimer < 0f) _infectionTimer = interval * Random.Range(0.4f, 0.8f); // 처음엔 조금 일찍, 정예마다 엇갈리게
            if (_infectionTimer > 0f) _infectionTimer = Mathf.Max(0f, _infectionTimer - dt); // 공격 중에도 줄어요 (붙어서 계속 휘두르면 영영 못 쓰던 것)
            if (_infectionTimer > 0f || Attack.IsAttacking || IsStunned) return; // 쓰는 건 공격이 끝난 뒤
            _infectionTimer = interval;

            Vector3 to = ToPlayer();
            float dist = to.magnitude;
            switch (trait)
            {
                case EliteTrait.Gyeonggong:
                {
                    float want;
                    if (dist >= 2.5f) // 멀면 곧장 파고들고
                    {
                        _dashDir = to / dist;
                        want = Mathf.Min(dist - 1.5f, GameConfig.GetOptional("elite.gyeonggong_distance", 4f));
                    }
                    else // 붙어 있으면 옆으로 내달려 돌아 들어가요
                    {
                        _dashDir = Quaternion.Euler(0f, Random.Range(0, 2) == 0 ? 90f : -90f, 0f) * (dist > 1e-3f ? to / dist : Facing);
                        want = GameConfig.GetOptional("elite.gyeonggong_distance", 4f) * 0.6f;
                    }
                    // 벽 · 엄폐물 앞에서 멈추게 미리 재요 (벽에 붙어 있을 때 벽 너머에 나타나던 것)
                    float free = DashFree(_dashDir, want);
                    if (free < 0.5f && dist < 2.5f) // 옆이 막혔으면 반대쪽 옆으로
                    {
                        _dashDir = -_dashDir;
                        free = DashFree(_dashDir, want);
                    }
                    if (free < 0.5f) { _infectionTimer = 0.5f; return; } // 내달릴 틈이 없으면 잠깐 뒤에 다시
                    _dashLeft = Mathf.Min(want, free);
                    _dashFrom = transform.position;
                    FaceToward(_dashDir);
                    Dashed?.Invoke(this, transform.position);
                    break;
                }
                case EliteTrait.Fireball:
                {
                    Vector3 toReal = ToRealPlayer(); // 다른 진영 적을 노리는 중에도 불화살은 플레이어에게
                    float range = GameConfig.GetOptional("elite.fireball_range", 10f);
                    if (toReal.magnitude > range) { _infectionTimer = 0.5f; return; }
                    Vector3 boltDir = toReal.sqrMagnitude > 1e-4f ? toReal.normalized : Facing;
                    FaceToward(boltDir);
                    Vector3 origin = transform.position + Vector3.up + boltDir * (BodyRadius + 0.1f);
                    Firebolt.Launch(origin, boltDir, GameConfig.GetOptional("elite.fireball_speed", 11f), range,
                        GameConfig.GetOptional("elite.fireball_bolt_radius", 0.3f), AttackPower * GameConfig.GetOptional("elite.fireball_ratio", 0.8f), Player, Body);
                    FireboltShot?.Invoke(this, origin, boltDir);
                    break;
                }
                case EliteTrait.Blink:
                    Vector3 from = transform.position;
                    Vector3 dir = dist > 1e-3f ? to / dist : Facing;
                    // 멀면 플레이어 옆으로, 붙어 있으면 플레이어 등 뒤로 넘어가요
                    Vector3 side = dist >= 4f ? Quaternion.Euler(0f, Random.Range(0, 2) == 0 ? 90f : -90f, 0f) * dir : dir;
                    Vector3 target = transform.position + to; // 노리는 쪽 (다른 진영 적을 노리면 그 적)
                    Vector3 at = Director.FindFreeSpot(target + side * GameConfig.GetOptional("elite.blink_distance", 2.5f), 0.5f);
                    Teleport(at);
                    FaceToward(target - at);
                    Blinked?.Invoke(this, from, at);
                    break;
            }
        }

        // 경공으로 dir 쪽으로 갈 수 있는 거리 (벽 앞 DashWallGap칸에서 멈춰요)
        float DashFree(Vector3 dir, float want)
        {
            float radius = Controller != null ? Controller.radius * 0.9f : 0.35f;
            return ObstacleProbe.Distance(transform.position + Vector3.up * DashProbeHeight, dir, radius, want + radius) - radius - DashWallGap;
        }

        // a → b 사이에 벽이 끼어 있나요 (양쪽에서 쏴 봐요: 벽에 파묻힌 쪽에서 쏜 것은 안 잡혀서)
        static bool CrossedWall(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-3f) return false;
            Vector3 up = Vector3.up * DashProbeHeight;
            return ObstacleProbe.Distance(a + up, d / len, 0.05f, len) < len - 1e-3f
                || ObstacleProbe.Distance(b + up, -d / len, 0.05f, len) < len - 1e-3f;
        }

        // 화염구 정예의 불화살: 곧게 날아가다 플레이어에게 맞으면 화상 1중첩, 벽 · 엄폐물에 막히거나 사거리 끝에서 사라져요.
        //   플레이어가 무적 (대시)이면 그대로 지나가요. 그림은 임시 주홍 구 (아트가 꾸며요: Owner · Direction)
        public sealed class Firebolt : MonoBehaviour
        {
            static Material _mat;
            PlayerController _target;
            Vector3 _direction;
            float _speed, _remaining, _radius, _attack;
            public Damageable Owner { get; private set; }
            public Vector3 Direction => _direction;

            public static Firebolt Launch(Vector3 origin, Vector3 direction, float speed, float range, float radius, float attack, PlayerController target, Damageable owner)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "불화살";
                EnemyFactory.StripCollider(go);
                go.transform.position = origin;
                go.transform.localScale = Vector3.one * 0.35f;
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _mat, new Color(1f, 0.42f, 0.1f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var bolt = go.AddComponent<Firebolt>();
                bolt._target = target;
                bolt._direction = new Vector3(direction.x, 0f, direction.z).normalized;
                bolt._speed = speed;
                bolt._remaining = range;
                bolt._radius = radius;
                bolt._attack = attack;
                bolt.Owner = owner;
                go.transform.rotation = Quaternion.LookRotation(bolt._direction);
                Sfx.Play(SfxId.BoltFire);
                return bolt;
            }

            void Update()
            {
                float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
                Vector3 from = transform.position;
                float free = ObstacleProbe.Distance(from, _direction, _radius, step);
                bool blocked = free < step;
                Vector3 to = from + _direction * (blocked ? free : step);

                if (_target != null && !_target.IsDead && DistanceXZ(from, to, _target.transform.position) <= _radius + _target.BodyRadius
                    && _target.TryReceiveHit(_attack, from, -1f, Owner))
                {
                    _target.AddBurnStack(GameConfig.Get("player_burn.damage", 2f)); // 화상 1중첩
                    Destroy(gameObject);
                    return;
                }

                transform.position = to;
                _remaining -= step;
                if (blocked || _remaining <= 0f) Destroy(gameObject);
            }

            static float DistanceXZ(Vector3 a, Vector3 b, Vector3 p)
            {
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(p.x, p.z);
                Vector2 ab = b2 - a2;
                float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude);
                return Vector2.Distance(a2 + ab * t, p2);
            }
        }
    }
}
