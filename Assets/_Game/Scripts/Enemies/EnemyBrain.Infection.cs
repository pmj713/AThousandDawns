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

        float _infectionTimer = -1f, _dashLeft;
        Vector3 _dashDir;

        void TickInfection(float dt)
        {
            if (_dashLeft > 0f) // 경공 내달리기 중
            {
                float step = Mathf.Min(_dashLeft, GameConfig.GetOptional("elite.gyeonggong_speed", 18f) * dt);
                var flags = MoveRaw(_dashDir * step);
                _dashLeft = (flags & CollisionFlags.Sides) != 0 ? 0f : _dashLeft - step;
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
                    if (dist >= 2.5f) // 멀면 곧장 파고들고
                    {
                        _dashDir = to / dist;
                        _dashLeft = Mathf.Min(dist - 1.5f, GameConfig.GetOptional("elite.gyeonggong_distance", 4f));
                    }
                    else // 붙어 있으면 옆으로 내달려 돌아 들어가요
                    {
                        _dashDir = Quaternion.Euler(0f, Random.Range(0, 2) == 0 ? 90f : -90f, 0f) * (dist > 1e-3f ? to / dist : Facing);
                        _dashLeft = GameConfig.GetOptional("elite.gyeonggong_distance", 4f) * 0.6f;
                    }
                    FaceToward(_dashDir);
                    Dashed?.Invoke(this, transform.position);
                    break;
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
                    Vector3 at = Director.FindFreeSpot(Player.transform.position + side * GameConfig.GetOptional("elite.blink_distance", 2.5f), 0.5f);
                    Teleport(at);
                    FaceToward(Player.transform.position - at);
                    Blinked?.Invoke(this, from, at);
                    break;
            }
        }
    }
}
