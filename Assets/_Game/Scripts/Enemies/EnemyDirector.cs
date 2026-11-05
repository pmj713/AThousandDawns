using System;
using System.Collections.Generic;
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
        public Material accent;
        public Material arrow;
        public Material telegraphBase;
        public Material telegraphFill;
        public Material telegraphOutline;
    }

    // 방 하나의 적 관리: 공격 토큰(동시 공격 수), 살아 있는 적 목록, 생성.
    public sealed class EnemyDirector : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] EnemyVisuals visuals = new EnemyVisuals();

        readonly List<EnemyBrain> _enemies = new List<EnemyBrain>();

        public PlayerController Player => player;
        public AttackTokenPool MeleeTokens { get; private set; }
        public AttackTokenPool RangedTokens { get; private set; }
        public IReadOnlyList<EnemyBrain> Enemies => _enemies;

        void Awake()
        {
            MeleeTokens = new AttackTokenPool(Mathf.RoundToInt(GameConfig.Get("enemy.melee_tokens", 2f)));
            RangedTokens = new AttackTokenPool(Mathf.RoundToInt(GameConfig.Get("enemy.ranged_tokens", 1f)));
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
            foreach (var other in _enemies) Physics.IgnoreCollision(enemy.BodyCollider, other.BodyCollider);
            if (player != null && player.Body != null) Physics.IgnoreCollision(enemy.BodyCollider, player.Body);
            _enemies.Add(enemy);
            return enemy;
        }

        public Arrow SpawnArrow(Vector3 origin, Vector3 direction, float speed, float range, float radius, float attack)
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
            arrow.Launch(player, direction, speed, range, radius, attack);
            return arrow;
        }

        internal void Unregister(EnemyBrain enemy) => _enemies.Remove(enemy);
    }
}
