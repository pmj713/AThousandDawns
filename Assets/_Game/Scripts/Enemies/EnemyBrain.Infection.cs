using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 차원 감염 정예: 차원이 겹치며 반대편 세계의 능력을 얻은 정예예요.
    //   판타지 적 → 무공: 경공 (elite.gyeonggong_interval마다 순식간에 몇 칸 내달려요: 멀면 파고들고, 붙어 있으면 옆으로 돌아 들어가요)
    //   무협 적 → 마법: 화염구 (elite.fireball_interval마다 플레이어 발밑에 불덩이가 떨어져요, 예고 원 → 폭발 + 불길)
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
                    if (dist > GameConfig.GetOptional("elite.fireball_range", 10f)) { _infectionTimer = 0.5f; return; }
                    BloodBurst.Create(Director, Player.transform.position, GameConfig.GetOptional("elite.fireball_radius", 1.3f),
                        GameConfig.GetOptional("elite.fireball_fuse", 1f), AttackPower * GameConfig.GetOptional("elite.fireball_ratio", 0.8f), Body,
                        GameConfig.GetOptional("elite.fireball_fire_life", 2.5f), BurstKind.Fireball);
                    break;
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
    }
}
