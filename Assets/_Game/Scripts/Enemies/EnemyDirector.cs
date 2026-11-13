using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    [Serializable]
    public sealed class EnemyVisuals
    {
        public Material grunt;
        public Material boar;
        public Material archer;
        public Material hound;
        public Material shield;
        public Material horn;
        public Material bomber;
        public Material snake;
        public Material nokrim;
        public Material assassin;
        public Material qingzhu;
        public Material ironfist;
        public Material fire;
        public Material accent;
        public Material arrow;
        public Material telegraphBase;
        public Material telegraphFill;
        public Material telegraphOutline;
    }

    // 방 하나의 적 관리: 공격 토큰(동시 공격 수), 살아 있는 적 목록, 생성, 풀숲, 소환 횟수, 폭발.
    public sealed class EnemyDirector : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] EnemyVisuals visuals = new EnemyVisuals();

        readonly List<EnemyBrain> _enemies = new List<EnemyBrain>();
        readonly List<AmbushBush> _bushes = new List<AmbushBush>();
        readonly List<Damageable> _blastTargets = new List<Damageable>();
        readonly Collider[] _overlap = new Collider[64];
        DamageRules _rules;

        public PlayerController Player => player;
        public EnemyVisuals Visuals => visuals;
        public AttackTokenPool MeleeTokens { get; private set; }
        public AttackTokenPool RangedTokens { get; private set; }
        public List<EnemyBrain> Enemies => _enemies; // List 그대로 (IReadOnlyList로 foreach하면 매번 할당해요). 밖에서 바꾸지 마세요
        public int SummonsLeft { get; private set; } // 나팔수 호출: 방마다 최대 2번

        void Awake()
        {
            MeleeTokens = new AttackTokenPool(Mathf.RoundToInt(GameConfig.Get("enemy.melee_tokens", 2f)));
            RangedTokens = new AttackTokenPool(Mathf.RoundToInt(GameConfig.Get("enemy.ranged_tokens", 1f)));
            SummonsLeft = Mathf.RoundToInt(GameConfig.Get("horn.summon_max", 2f));
            _bushes.AddRange(FindObjectsByType<AmbushBush>(FindObjectsSortMode.None));
            _rules = new DamageRules(
                GameConfig.Get("damage.toughness_floor_ratio", 0.2f),
                GameConfig.Get("damage.gongryeok_per_point", 0.1f),
                GameConfig.Get("damage.player_multiplier_floor", 0.4f),
                GameConfig.Get("damage.player_defense_constant", 50f));
        }

        public int Count(EnemyKind kind)
        {
            int n = 0;
            foreach (var e in _enemies) if (e.Kind == kind) n++;
            return n;
        }

        public EnemyBrain Spawn(EnemyKind kind, Vector3 position)
        {
            var enemy = EnemyFactory.Create(this, kind, new Vector3(position.x, 0f, position.z), visuals);
            RestoreIgnores(enemy);
            _enemies.Add(enemy);
            return enemy;
        }

        // 적이 부른 부하 (나팔수 · 혈불 · 철룡 · 녹림왕 · 혈인 부활): 판이 정한 지역 배율을 똑같이 받아요 (RoomRun이 SummonScale을 정해요)
        public System.Action<EnemyBrain> SummonScale;

        public EnemyBrain SpawnSummon(EnemyKind kind, Vector3 position)
        {
            var enemy = Spawn(kind, position);
            if (enemy != null) SummonScale?.Invoke(enemy);
            return enemy;
        }

        // 적끼리, 적과 플레이어는 서로 밀지 않아요 (거리는 각자 Separation으로 유지)
        public void RestoreIgnores(EnemyBrain enemy)
        {
            foreach (var other in _enemies)
                if (other != null && other != enemy) Physics.IgnoreCollision(enemy.BodyCollider, other.BodyCollider);
            if (player != null && player.Body != null) Physics.IgnoreCollision(enemy.BodyCollider, player.Body);
        }

        public Arrow SpawnArrow(Vector3 origin, Vector3 direction, float speed, float range, float radius, float attack, Damageable owner = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arrow";
            EnemyFactory.StripCollider(go);
            go.transform.position = origin;
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.7f);
            var renderer = go.GetComponent<Renderer>();
            if (visuals.arrow != null) renderer.sharedMaterial = visuals.arrow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var arrow = go.AddComponent<Arrow>();
            arrow.Launch(player, direction, speed, range, radius, attack, owner);
            Sfx.Play(SfxId.Arrow);
            return arrow;
        }

        public bool TryUseSummon()
        {
            if (SummonsLeft <= 0) return false;
            SummonsLeft--;
            return true;
        }

        // 비어 있는 풀숲 가운데 near에서 가장 가까운 곳을 owner 몫으로 잡아요. exclude는 될 수 있으면 피해요 (방금 나온 풀숲).
        public AmbushBush ClaimBush(EnemyBrain owner, Vector3 near, AmbushBush exclude)
        {
            AmbushBush best = null;
            float bestDistance = float.MaxValue;
            foreach (var bush in _bushes)
            {
                if (bush == null || bush == exclude || (bush.Occupant != null && bush.Occupant != owner)) continue;
                float d = Vector3.Distance(bush.transform.position, near);
                if (d < bestDistance)
                {
                    best = bush;
                    bestDistance = d;
                }
            }
            if (best == null && exclude != null && (exclude.Occupant == null || exclude.Occupant == owner)) best = exclude;
            if (best != null) best.Occupant = owner;
            return best;
        }

        // 화약 산적 폭발: 반경 안 다른 적·목인장에게 피해 (강인함·방어력 적용).
        public void Blast(Vector3 center, float radius, float attack, Damageable source)
        {
            _blastTargets.Clear();
            int found = Physics.OverlapSphereNonAlloc(center, radius + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var d = _overlap[i].GetComponentInParent<Damageable>();
                if (d == null || d == source || d.IsDead || !d.Targetable || _blastTargets.Contains(d)) continue;
                Vector3 offset = d.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > radius + d.BodyRadius) continue;
                _blastTargets.Add(d);
            }
            foreach (var d in _blastTargets)
            {
                var hit = HitDamageInput.Of(attack);
                hit.Toughness = d.Toughness;
                hit.Defense = d.Defense;
                d.ApplyHit(DamageFormula.ToEnemy(hit, _rules));
            }
        }

        // 폭발 순간 바닥에 잠깐 보이는 원
        // 터지는 자리 (아트가 종류마다 다른 폭발을 그려요: Burst 신호). Default가 아니면 예고 오브젝트 이름이 "Burst"라 공용 먹 폭발과 겹치지 않아요
        public static event System.Action<Vector3, float, AThousandDawns.Combat.BurstKind> Burst;

        public void SpawnBurst(Vector3 center, float radius, AThousandDawns.Combat.BurstKind kind = AThousandDawns.Combat.BurstKind.Default)
        {
            var burst = Telegraph.Create(null, visuals.telegraphFill, visuals.telegraphFill, visuals.telegraphOutline);
            if (kind != AThousandDawns.Combat.BurstKind.Default) burst.gameObject.name = "Burst";
            Burst?.Invoke(center, radius, kind);
            burst.Show(TelegraphShapes.Circle(radius), true);
            burst.SetPose(center, Vector3.forward);
            burst.SetProgress(1f);
            Destroy(burst.gameObject, 0.2f);
            Sfx.Play(SfxId.Explosion); // 화약 산적 폭발 · 녹림왕 내려찍기·회전베기
        }

        // around 둘레에서 벽·대나무와 겹치지 않고, around에서 벽을 넘지 않고 갈 수 있는 자리 (소환된 졸개가 설 곳)
        public Vector3 FindFreeSpot(Vector3 around, float distance)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float r = distance + (i / 4) * 0.6f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 p = new Vector3(around.x, 0f, around.z) + dir * r;
                if (ObstacleProbe.Distance(new Vector3(around.x, 0.8f, around.z), dir, 0.3f, r) < r) continue;
                if (IsFree(p, 0.45f)) return p;
            }
            return new Vector3(around.x, 0f, around.z);
        }

        // 새 방에 들어갈 때: 방마다 세는 것(나팔수 호출 횟수)을 처음으로
        public void ResetRoom()
        {
            SummonsLeft = Mathf.RoundToInt(GameConfig.Get("horn.summon_max", 2f));
            GhostFire.ClearAll(); // 방을 옮기면 도깨비불 장판도 사라져요
            AnchorBrain.ClearPosts(); // 쇠닻 기둥도
            BloodShamanBrain.ClearCorpses(); // 혈인이 되살릴 시체도 방마다
        }

        public bool IsFree(Vector3 p, float radius)
        {
            int found = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.8f, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
                if (!ObstacleProbe.IsActor(_overlap[i])) return false;
            return true;
        }

        internal void Unregister(EnemyBrain enemy) => _enemies.Remove(enemy);

        // 남은 적을 조용히 치워요 (처치로 세지 않음): 지역 보스를 쓰러뜨렸는데 소환된 졸개가 남아 다음 지역까지 "전투 중"이 되지 않게
        public void DespawnAll()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                _enemies.RemoveAt(i);
                if (e != null) Destroy(e.gameObject);
            }
        }
    }
}
