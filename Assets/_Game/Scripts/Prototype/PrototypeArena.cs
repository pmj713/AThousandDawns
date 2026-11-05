using System;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 시험판 맵: 넓은 죽림에 일반 적 8종을 한 마리씩, 북쪽 결투장에 녹림왕을 놓아 둬요.
    // 적은 플레이어가 가까이 오거나(enemy.wake_radius) 맞기 전까지 가만히 있어요.
    // 결투장에 들어가 녹림왕이 깨어나면 입구가 결계로 막히고, 녹림왕이 쓰러지면 열려요. 다 쓰러뜨리면 잠시 뒤 다시 놓아요.
    // 1 졸개 · 2 멧돼지 · 3 궁수 (방마다 최대 2명) · 4 들개 둘 · 5 방패 산적 · 6 나팔수 · 7 화약 산적 · 8 청죽사 는 플레이어 근처에 바로 깨어 있는 채로,
    // 9 녹림왕은 결투장에 불러요. G로 기세를 채우고, F5로 처음부터 다시 해요. (R은 조작 시트에서 금창약 키라 비워 둬요)
    public sealed class PrototypeArena : MonoBehaviour
    {
        [Serializable]
        public struct SpawnEntry
        {
            public EnemyKind kind;
            public Vector3 position;
        }

        // PlayMode 테스트는 이걸 끄고 필요한 적만 직접 불러요.
        public static bool AutoSpawn = true;

        [SerializeField] EnemyDirector director;
        [SerializeField] GameObject bossGate;
        [SerializeField] Vector3 bossZoneCenter = new Vector3(0f, 0f, 16.5f);
        [SerializeField] float respawnDelay = 3f;
        [SerializeField] float minSpawnDistance = 4f;
        [SerializeField] float spawnRingRadius = 6f;

        // 일반 적 8종 한 마리씩 (깨어나는 거리가 서로 겹치지 않게 흩어 놓음)
        [SerializeField] SpawnEntry[] layout =
        {
            new SpawnEntry { kind = EnemyKind.Grunt, position = new Vector3(-12f, 0f, -1f) },
            new SpawnEntry { kind = EnemyKind.Boar, position = new Vector3(12f, 0f, -6f) },
            new SpawnEntry { kind = EnemyKind.Archer, position = new Vector3(-14f, 0f, -9f) },
            new SpawnEntry { kind = EnemyKind.Hound, position = new Vector3(14f, 0f, 3f) },
            new SpawnEntry { kind = EnemyKind.Shield, position = new Vector3(-7f, 0f, 7f) },
            new SpawnEntry { kind = EnemyKind.Horn, position = new Vector3(8f, 0f, 7f) },
            new SpawnEntry { kind = EnemyKind.Bomber, position = new Vector3(6f, 0f, -11f) },
            new SpawnEntry { kind = EnemyKind.Snake, position = new Vector3(-15f, 0f, 5f) },
        };

        float _respawnTimer = -1f;
        float _wakeRadius;
        int _archerLimit;
        NokrimBrain _boss;

        public int RoundNumber { get; private set; }
        public NokrimBrain Boss => _boss;

        void Start()
        {
            _archerLimit = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f));
            _wakeRadius = GameConfig.Get("enemy.wake_radius", 7f);
            SetGate(false);
            if (AutoSpawn) SpawnLayout();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f5Key.wasPressedThisFrame)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
                }
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Grunt);
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Boar);
                if (keyboard.digit3Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Archer);
                if (keyboard.digit4Key.wasPressedThisFrame) { SpawnNearPlayer(EnemyKind.Hound); SpawnNearPlayer(EnemyKind.Hound); } // 들개는 무리로
                if (keyboard.digit5Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Shield);
                if (keyboard.digit6Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Horn);
                if (keyboard.digit7Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Bomber);
                if (keyboard.digit8Key.wasPressedThisFrame) SpawnNearPlayer(EnemyKind.Snake);
                if (keyboard.digit9Key.wasPressedThisFrame) SpawnBoss();
                if (keyboard.gKey.wasPressedThisFrame && director != null && director.Player != null) director.Player.FillGauge();
            }

            // 녹림왕이 쓰러지면 결계가 풀려요
            if (bossGate != null && bossGate.activeSelf && (_boss == null || _boss.IsDead)) SetGate(false);

            if (!AutoSpawn || director == null) return;
            if (director.Enemies.Count > 0)
            {
                _respawnTimer = -1f;
                return;
            }
            if (_respawnTimer < 0f) _respawnTimer = respawnDelay;
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f) SpawnLayout();
        }

        void SpawnLayout()
        {
            RoundNumber++;
            _respawnTimer = -1f;
            foreach (var entry in layout)
            {
                var enemy = director.Spawn(entry.kind, entry.position);
                enemy.WakeRadius = _wakeRadius;
            }
            SpawnBoss();
        }

        public NokrimBrain SpawnBoss()
        {
            if (_boss != null && !_boss.IsDead) return _boss;
            _boss = (NokrimBrain)director.Spawn(EnemyKind.Nokrim, bossZoneCenter + new Vector3(0f, 0f, 2f));
            _boss.SetZone(bossZoneCenter, new Vector2(GameConfig.Get("nokrim.arena_width", 16f), GameConfig.Get("nokrim.arena_depth", 12f)));
            _boss.WakeRadius = 1f; // SetZone이 있으면 결투장 안에 들어와야 깨어나요
            _boss.Woke += _ => SetGate(true);
            return _boss;
        }

        void SetGate(bool closed)
        {
            if (bossGate != null) bossGate.SetActive(closed);
        }

        // 플레이어 둘레(6칸)에서 벽·대나무와 겹치지 않는 자리에 바로 깨어 있는 채로 불러요.
        public EnemyBrain SpawnNearPlayer(EnemyKind kind)
        {
            if (director == null || director.Player == null) return null;
            if (kind == EnemyKind.Archer && director.Count(EnemyKind.Archer) >= _archerLimit) return null;
            Vector3 center = director.Player.transform.position;
            Vector3 p = director.FindFreeSpot(center, Mathf.Max(minSpawnDistance, spawnRingRadius));
            return director.Spawn(kind, p);
        }
    }
}
