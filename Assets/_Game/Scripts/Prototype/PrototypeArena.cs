using System;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 시험판 방: 시작하면 한 무리가 나오고, 다 쓰러뜨리면 잠시 뒤 다시 나와요.
    // 1 졸개 · 2 멧돼지 · 3 궁수 (방마다 최대 2명) · 4 들개 둘 · 5 방패 산적 · 6 나팔수 · 7 화약 산적 · 8 청죽사 를 부르고,
    // G로 기세를 채우고, F5로 처음부터 다시 해요.
    // (R은 조작 시트에서 금창약 키라 비워 둬요)
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
        [SerializeField] float respawnDelay = 2f;
        [SerializeField] float minSpawnDistance = 4f;
        [Serializable]
        public struct Wave
        {
            public SpawnEntry[] entries;
        }

        // 무리는 차례로 돌아가며 나와요. 청죽사는 나온 자리에서 가장 가까운 빈 풀숲으로 숨어요.
        [SerializeField] Wave[] waves =
        {
            new Wave { entries = new[]
            {
                new SpawnEntry { kind = EnemyKind.Grunt, position = new Vector3(-6.5f, 0f, 0.5f) },
                new SpawnEntry { kind = EnemyKind.Grunt, position = new Vector3(6.5f, 0f, 0.5f) },
                new SpawnEntry { kind = EnemyKind.Grunt, position = new Vector3(-1.5f, 0f, 5f) },
                new SpawnEntry { kind = EnemyKind.Boar, position = new Vector3(2f, 0f, 5f) },
                new SpawnEntry { kind = EnemyKind.Archer, position = new Vector3(6.8f, 0f, 5.2f) },
            } },
            new Wave { entries = new[]
            {
                new SpawnEntry { kind = EnemyKind.Hound, position = new Vector3(-6.5f, 0f, 4.8f) },
                new SpawnEntry { kind = EnemyKind.Hound, position = new Vector3(-6.8f, 0f, 3.6f) },
                new SpawnEntry { kind = EnemyKind.Hound, position = new Vector3(-7f, 0f, 2.4f) },
                new SpawnEntry { kind = EnemyKind.Shield, position = new Vector3(6.5f, 0f, 0.5f) },
                new SpawnEntry { kind = EnemyKind.Bomber, position = new Vector3(2f, 0f, 5f) },
            } },
            new Wave { entries = new[]
            {
                new SpawnEntry { kind = EnemyKind.Horn, position = new Vector3(6.8f, 0f, 5.2f) },
                new SpawnEntry { kind = EnemyKind.Shield, position = new Vector3(-6.5f, 0f, 0.5f) },
                new SpawnEntry { kind = EnemyKind.Snake, position = new Vector3(-3f, 0f, 4.8f) },
                new SpawnEntry { kind = EnemyKind.Snake, position = new Vector3(3.5f, 0f, -4.5f) },
                new SpawnEntry { kind = EnemyKind.Archer, position = new Vector3(-6.8f, 0f, -5f) },
            } },
        };
        [SerializeField] Vector3[] spawnPoints =
        {
            new Vector3(-6.5f, 0f, 0.5f), new Vector3(6.5f, 0f, 0.5f), new Vector3(-1.5f, 0f, 5f),
            new Vector3(2f, 0f, 5f), new Vector3(6.8f, 0f, 5.2f), new Vector3(-6.8f, 0f, -5f),
            new Vector3(0f, 0f, -5.2f), new Vector3(6.8f, 0f, -5.2f),
        };

        float _respawnTimer = -1f;
        int _archerLimit;

        public int WaveNumber { get; private set; }

        void Start()
        {
            _archerLimit = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f));
            if (AutoSpawn) SpawnWave();
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
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnOne(EnemyKind.Grunt);
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnOne(EnemyKind.Boar);
                if (keyboard.digit3Key.wasPressedThisFrame) SpawnOne(EnemyKind.Archer);
                if (keyboard.digit4Key.wasPressedThisFrame) { SpawnOne(EnemyKind.Hound); SpawnOne(EnemyKind.Hound); } // 들개는 무리로
                if (keyboard.digit5Key.wasPressedThisFrame) SpawnOne(EnemyKind.Shield);
                if (keyboard.digit6Key.wasPressedThisFrame) SpawnOne(EnemyKind.Horn);
                if (keyboard.digit7Key.wasPressedThisFrame) SpawnOne(EnemyKind.Bomber);
                if (keyboard.digit8Key.wasPressedThisFrame) SpawnOne(EnemyKind.Snake);
                if (keyboard.gKey.wasPressedThisFrame && director != null && director.Player != null) director.Player.FillGauge();
            }

            if (!AutoSpawn || director == null) return;
            if (director.Enemies.Count > 0)
            {
                _respawnTimer = -1f;
                return;
            }
            if (_respawnTimer < 0f) _respawnTimer = respawnDelay;
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f) SpawnWave();
        }

        void SpawnWave()
        {
            WaveNumber++;
            _respawnTimer = -1f;
            if (waves == null || waves.Length == 0) return;
            var wave = waves[(WaveNumber - 1) % waves.Length];
            if (wave.entries == null) return;
            foreach (var entry in wave.entries) director.Spawn(entry.kind, entry.position);
        }

        public EnemyBrain SpawnOne(EnemyKind kind)
        {
            if (kind == EnemyKind.Archer && director.Count(EnemyKind.Archer) >= _archerLimit) return null;
            return director.Spawn(kind, PickSpawnPoint());
        }

        Vector3 PickSpawnPoint()
        {
            Vector3 player = director.Player != null ? director.Player.transform.position : Vector3.zero;
            for (int tries = 0; tries < 10; tries++)
            {
                Vector3 p = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (Vector3.Distance(p, player) >= minSpawnDistance) return p;
            }
            return spawnPoints[0];
        }
    }
}
