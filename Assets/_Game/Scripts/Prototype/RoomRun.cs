using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AThousandDawns.Prototype
{
    // 판 구조 시험판: 죽림 일반 층마다 갈림길 → 층 탐색 → 관문, 마지막 층 관문 뒤 객잔 → 정비실 → 녹림왕.
    // 갈림길: 길 2~3갈래. 길마다 보상 종류(기물·운기조식)와 기운(옅음·짙음).
    //   짙은 길: 적 체력 +30%, 방마다 정예 1명 추가 (특성 무작위), 보상은 한 단계 위 (기물은 다음 지역 등급표, 은자·회복 ×1.5).
    // 층: 격자에 방이 무작위로 놓여요 (FloorMap). 들어간 방의 옆방만 지도에 드러나요.
    //   전투 방은 들어가면 문이 잠기고 예산으로 뽑은 웨이브가 나와요. 비우면 은자. 보상 방은 전투 뒤 고른 보상.
    //   관문 방 가운데에서 F를 누르면 다음 층 (남은 방은 그냥 두고 가도 돼요).
    // 객잔: 지역마다 한 번, 일반 층을 다 지난 뒤 (은자 시트 가정). 점소이 앞에서 F를 누르면 진열대 (InnShop).
    // 정비실: 목인장 두 개. 나갈 때 기세와 회선참 쿨타임을 들어올 때로 되돌려요.
    // 녹림왕을 쓰러뜨리면 기물 하나 + 경맥도 5×4 → 6×4, 판 끝. 같은 시드면 같은 갈림길·배치·적 구성 (버그 재현용).
    public sealed partial class RoomRun : MonoBehaviour
    {
        public enum Phase { None, Fork, Floor, Master, Inn, Rest, Boss, Done, Village }
        public enum RewardKind { Item, Heal, Part, Forge }

        public struct Path
        {
            public RewardKind Reward;
            public bool Dense;
        }

        [Serializable]
        public sealed class Site
        {
            public Vector3 center;
            public Vector2 size = new Vector2(20f, 12f);
            public Vector3 entry;         // 들어왔을 때 서는 자리
            public Vector3 exit;          // 이 근처에 오면 다음 방
            public float exitRadius = 1.3f;
        }

        // 층의 모든 방이 같이 쓰는 방 하나 (20×12). 문은 북·동·남·서, 옆방이 없으면 막고 싸우는 동안 잠가요.
        [Serializable]
        public sealed class CombatSite
        {
            public Vector3 center;
            public Vector2 size = new Vector2(20f, 12f);
            public GameObject[] locks = new GameObject[4]; // 결계 (싸우는 동안)
            public GameObject[] plugs = new GameObject[4]; // 벽 (옆방이 없는 쪽)
            public GameObject gateMarker;                  // 관문
            public float exitRadius = 1.3f;
        }

        // 테스트에서 고정해요. 0이면 시간으로 정해요.
        public static int SeedOverride;

        [SerializeField] EnemyDirector director;
        [SerializeField] PrototypeArena arena;
        [SerializeField] CombatSite combat = new CombatSite();
        [SerializeField] Site inn = new Site();
        [SerializeField] Site rest = new Site();
        [SerializeField] Site boss = new Site();

        readonly List<(EnemyKind kind, Vector3 position, Telegraph marker, bool elite)> _pending = new List<(EnemyKind, Vector3, Telegraph, bool)>();
        List<List<EnemyKind>> _waves;
        [System.NonSerialized] List<Path> _forkOptions; // null = 갈림길 아님 (플레이 중 다시 컴파일돼도 빈 목록으로 되살아나지 않게)
        System.Random _rng;
        Phase _phase;
        FloorMap _map;
        FloorRoom _room;
        Path _path;
        bool _fighting, _warning;
        int _floor, _floors, _wave, _eliteWave = -1, _elitesThisRoom;
        EnemyKind _eliteKind;
        EliteTrait _eliteTrait;
        float _timer, _warningTime, _minSpawnDistance, _restGauge;
        string _banner;
        float _bannerTimer;

        public int Seed { get; private set; }
        public Phase CurrentPhase => _phase;
        public int Floor => _floor;
        public int Floors => _floors;
        public FloorMap Map => _map;
        public FloorRoom Room => _room;
        public Path CurrentPath => _path;
        public IReadOnlyList<Path> ForkOptions => _forkOptions;
        public bool IsForking => _forkOptions != null;
        public bool IsFighting => _fighting;
        public bool IsDone => _phase == Phase.Done;
        public int Wave => _wave;
        public int WaveCount => _waves != null ? _waves.Count : 0;
        public int Silver { get; private set; } // 은자 (판 안 재화, 죽으면 사라짐)
        public InnShop Shop { get; private set; }
        public bool IsShopping { get; private set; }
        public string ShopNotice { get; private set; }
        public Vector3 InnCounter => inn.center + new Vector3(-3.5f, 0f, 2f); // 점소이 앞 (계산대 왼쪽 위, 문 가는 길은 비워 둠)
        public bool AtCounter => _phase == Phase.Inn && !IsShopping && Flat(Player.transform.position - InnCounter).magnitude <= GameConfig.Get("interact.range", 1.2f) + 0.8f;
        public string Banner => _bannerTimer > 0f ? _banner : null;
        public bool AtGate => _phase == Phase.Floor && _room != null && _room.Kind == FloorRoomKind.Gate && !_fighting && Flat(Player.transform.position - combat.center).magnitude < 1.6f;

        PlayerController Player => director != null ? director.Player : null;
        bool OffersOpen => arena.IsChoosing || arena.PendingOffers > 0;

        void Start()
        {
            Seed = SeedOverride != 0 ? SeedOverride : Environment.TickCount & 0x7fffffff;
            _rng = new System.Random(Seed);
            _warningTime = GameConfig.Get("room.spawn_warning", 0.8f);
            _minSpawnDistance = GameConfig.Get("room.spawn_min_distance", 5f);
            _floors = Mathf.Max(1, Mathf.RoundToInt(GameConfig.Get("room.floors", 3f)));
            SetGateMarker(false);
            PlaceCrack(null);
            PlaceJars(null);
            ApplyBook();
            bool resume = ResumeRun;
            ResumeRun = false;
            if (PrototypeArena.AutoSpawn)
            {
                if (resume && RestoreRun()) return; // 시작 화면 "이어하기": 저장한 판부터
                RunSave.Delete(); // 새 판 (남아 있던 저장은 버려요)
                if (StartsInVillage()) EnterVillage(); // 첫 사망 뒤부터 마을에서
                else if (StartsTutorial()) BeginTutorialFloor(); // 처음엔 입문 죽림
                else BeginFork(1);
            }
        }

        void Update()
        {
            if (_bannerTimer > 0f) _bannerTimer -= Time.unscaledDeltaTime;
            if (Player == null) return;
            TickRecord(); // 플레이 시간 · 최대 추적도
            if (TickResult()) return; // 쓰러짐·판 끝 → 결과 화면
            if (TickPause()) return;  // Esc 일시정지
            TickTutorial();
            TickAutosave();

            if (IsForking)
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return;
                if (keyboard.digit1Key.wasPressedThisFrame) ChooseFork(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) ChooseFork(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) ChooseFork(2);
                return;
            }
            if (Player.IsDead) return;

            bool discarding = Player.Items != null && Player.Items.Pending != null; // 버릴 기물을 고르는 중 (PrototypeArena)
            if (IsShopping)
            {
                if (!discarding) TickShopKeys();
                return;
            }
            if (IsInEvent)
            {
                if (!discarding) TickEventKeys();
                return;
            }

            switch (_phase)
            {
                case Phase.Inn: TickInn(); break;
                case Phase.Master: TickMaster(); break;
                case Phase.Floor:
                    TickHunt(Time.deltaTime);
                    TickJars(); // 항아리는 싸우는 중에도 부딪히면 깨져요
                    if (_fighting) TickFight();
                    else TickFloorExits();
                    break;
                case Phase.Rest: TickRestExit(); break;
                case Phase.Village: TickVillage(); break;
                case Phase.Boss:
                    var b = arena.Boss;
                    if (b != null && b.IsDead) FinishRun();
                    break;
            }
        }

        // ── 갈림길

        public void BeginFork(int floor)
        {
            _floor = floor;
            _forkOptions = RollFork();
            arena.Blocked = true;
            Player.enabled = false; // 고르는 클릭이 평타로 남지 않게
            Time.timeScale = 0f;
            ShowBanner($"죽림 {floor}층 · 갈림길");
            MarkSave();
        }

        List<Path> RollFork()
        {
            if (_tutorialFork) return TutorialFork(); // 입문 뒤 첫 갈림길
            int min = Mathf.RoundToInt(GameConfig.Get("room.fork_min", 2f));
            int max = Mathf.RoundToInt(GameConfig.Get("room.fork_max", 3f));
            float dense = GameConfig.Get("room.fork_dense_chance", 0.4f);
            // 한 갈림길에 같은 보상 종류는 한 번만 (기물 옅은 길 + 기물 짙은 길 같이 안 나옴)
            var kinds = new List<RewardKind>((RewardKind[])Enum.GetValues(typeof(RewardKind)));
            if (_unlockedParts.Count == 0) kinds.Remove(RewardKind.Part); // 부품 길은 회귀록에서 부품을 연 뒤부터
            if (!_forgeUnlocked) kinds.Remove(RewardKind.Forge); // 대장간 길은 회귀록에서 대장간을 연 뒤부터
            for (int i = kinds.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }
            int count = Mathf.Min(kinds.Count, _rng.Next(min, Mathf.Max(min, max) + 1) + _forkBonus); // 면벽수련 오 10: 갈림길 +1
            var options = new List<Path>();
            for (int i = 0; i < count; i++) options.Add(new Path { Reward = kinds[i], Dense = _rng.NextDouble() < dense });
            return options;
        }

        public void ChooseFork(int index)
        {
            if (!IsForking || index < 0 || index >= _forkOptions.Count) return;
            _path = _forkOptions[index];
            _forkOptions = null;
            arena.Blocked = false;
            Player.enabled = true;
            Time.timeScale = 1f;
            int events = RollEventRooms();
            _map = FloorMap.Generate(_rng,
                Mathf.RoundToInt(GameConfig.Get("room.path_rooms", 3f)),
                Mathf.RoundToInt(GameConfig.Get("room.optional_rooms", 2f)), events, RollHidden());
            AssignEvents();
            SenseHidden();
            _phase = Phase.Floor;
            ShowBanner($"죽림 {_floor}층 · {PathLabel(_path)}");
            BeginHunt();
            Enter(_map.Start, -1);
            if (_startPartPick) // 면벽수련 맥 10: 시작 시 부품 고르기
            {
                _startPartPick = false;
                OpenPartChoice(1);
            }
        }

        public static string RewardLabel(RewardKind kind) => kind == RewardKind.Item ? "기물" : kind == RewardKind.Part ? "부품" : kind == RewardKind.Forge ? "대장간" : "운기조식";
        public static string PathLabel(Path p) => $"{RewardLabel(p.Reward)} · 기운 {(p.Dense ? "짙음" : "옅음")}";

        public string PathDetail(Path p)
        {
            float mult = p.Dense ? DenseMultiplier : 1f;
            string reward = p.Reward == RewardKind.Item
                ? (p.Dense ? "기물 3개 중 하나 (한 단계 위 등급표)" : "기물 3개 중 하나")
                : p.Reward == RewardKind.Part
                ? (p.Dense ? "부품 둘 고르기" : "부품 하나 고르기") + $" ({PartNames()})"
                : p.Reward == RewardKind.Forge
                ? (p.Dense ? "기물 둘까지 녹여 무기에 넣기" : "기물 하나를 녹여 무기에 넣기")
                : $"체력 {GameConfig.Get("room.heal_ratio", 0.3f) * mult:0%} 회복";
            string enemies = p.Dense ? $"적 체력 +{GameConfig.Get("room.dense_health", 1.3f) - 1f:0%} · 방마다 정예 1명" : "적 보통";
            return $"{reward}\n{enemies} · 방마다 은자 {Mathf.RoundToInt(GameConfig.Get("silver.room", 18f) * mult)}";
        }

        float DenseMultiplier => GameConfig.Get("room.dense_reward_multiplier", 1.5f);

        // ── 층 안의 방

        // movedDirection: 옆방에서 이 방향으로 넘어왔어요 (−1이면 층 입구, 가운데에 서요).
        public void Enter(FloorRoom room, int movedDirection)
        {
            ClearPending();
            _room = room;
            _map.Visit(room);
            _phase = Phase.Floor;
            MovePlayer(movedDirection < 0 ? combat.center : EntryFrom(movedDirection));
            director.ResetRoom();
            for (int d = 0; d < 4; d++)
            {
                bool open = FloorMap.IsOpen(_map.Neighbor(room, d)); // 찾지 못한 기연 방 쪽은 벽
                if (combat.plugs[d] != null) combat.plugs[d].SetActive(!open);
            }
            SetGateMarker(room.Kind == FloorRoomKind.Gate);
            PlaceCrack(room);
            PlaceJars(room);

            if (room.HasFight && !room.Cleared)
            {
                SetLocks(true);
                _fighting = true;
                Player.EnterRoom(); // #금강 2단계: 싸우는 방에 들어갈 때마다 보호막
                OnFightStart();
                _waves = room.FixedWaves ?? PlanRoom(_floor); // 입문 죽림은 손으로 짠 구성
                _wave = -1;
                _elitesThisRoom = 0;
                _eliteWave = -1;
                if (_path.Dense && Mathf.RoundToInt(GameConfig.Get("room.dense_elites", 1f)) > 0) PlanElite();
                StartNextWave();
            }
            else
            {
                SetLocks(false);
                _fighting = false;
                _waves = null;
                if (room.Kind == FloorRoomKind.Gate) ShowBanner("관문 · 가운데에서 F를 누르면 다음 층");
                if (room.Kind == FloorRoomKind.Event && !room.Cleared) OpenEvent(room, room.EventId);
                if (room.Kind == FloorRoomKind.Hidden && !room.Cleared) OpenHiddenRoom(room);
            }
            OnHuntEnter(room);
            OnTutorialEnter(room);
            MarkSave(); // 비운 방·싸움 없는 방이면 바로, 싸우는 방이면 비운 뒤에 저장돼요
        }

        Vector3 EntryFrom(int movedDirection)
        {
            var dir = FloorMap.Directions[FloorMap.Opposite(movedDirection)];
            float inset = dir.x != 0 ? combat.size.x * 0.5f - 1.5f : combat.size.y * 0.5f - 1.5f;
            return combat.center + new Vector3(dir.x, 0f, dir.y) * inset;
        }

        Vector3 ExitPoint(int direction)
        {
            var dir = FloorMap.Directions[direction];
            float reach = dir.x != 0 ? combat.size.x * 0.5f + 1.6f : combat.size.y * 0.5f + 1.6f;
            return combat.center + new Vector3(dir.x, 0f, dir.y) * reach;
        }

        void TickFloorExits()
        {
            if (OffersOpen) return; // 기물을 고른 뒤에 움직여요
            var keyboard = Keyboard.current;
            if (TickCrack(keyboard)) return; // 금 간 벽 부수기 (기연 방)
            if (AtGate && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                LeaveFloor();
                return;
            }
            Vector3 p = Player.transform.position;
            for (int d = 0; d < 4; d++)
            {
                var next = _map.Neighbor(_room, d);
                if (!FloorMap.IsOpen(next) || Flat(p - ExitPoint(d)).magnitude > combat.exitRadius) continue;
                Enter(next, d);
                return;
            }
        }

        public void LeaveFloor()
        {
            OnTutorialFloorLeft();
            ClearPending();
            EndHunt();
            EndFloorEffects();
            _record.FloorsCleared++; // 관문을 지나면 그 층 돌파
            _fighting = false;
            SetLocks(false);
            SetGateMarker(false);
            PlaceCrack(null);
            PlaceJars(null);
            if (_floor == MasterFloor && !_masterDone && _floor < _floors) EnterMaster(); // 2층 관문: 고수
            else if (_floor < _floors) BeginFork(_floor + 1);
            else EnterInn();
        }

        public List<List<EnemyKind>> PlanRoom(int floor, int fixedWaves = 0)
        {
            string f = Mathf.Clamp(floor, 1, 3).ToString();
            int min = Mathf.RoundToInt(GameConfig.Get($"room.budget_min.f{f}", 6f));
            int max = Mathf.RoundToInt(GameConfig.Get($"room.budget_max.f{f}", 8f));
            int budget = _rng.Next(min, Mathf.Max(min, max) + 1);
            int wavesMin = Mathf.RoundToInt(GameConfig.Get("room.waves_min", 2f));
            int wavesMax = Mathf.RoundToInt(GameConfig.Get("room.waves_max", 3f));
            int waves = fixedWaves > 0 ? fixedWaves : _rng.Next(wavesMin, Mathf.Max(wavesMin, wavesMax) + 1);
            var limits = new RoomBudget.Limits
            {
                Archers = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f)),
                Horns = Mathf.RoundToInt(GameConfig.Get("room.horn_max", 1f)),
                Snakes = Mathf.RoundToInt(GameConfig.Get("room.snake_max", 2f)),
            };
            return RoomBudget.Plan(budget, waves, RoomBudget.Bamboo, RoomBudget.Cost, limits, _rng);
        }

        // 짙은 길: 무작위 웨이브에 정예 하나 (방마다 제한은 일반 적과 같이 세요)
        void PlanElite()
        {
            var all = new List<EnemyKind>();
            foreach (var w in _waves) all.AddRange(w);
            int Count(EnemyKind k) => all.FindAll(x => x == k).Count;
            var kinds = new List<EnemyKind>();
            foreach (var k in RoomBudget.Bamboo)
            {
                if (k == EnemyKind.Archer && Count(k) >= Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f))) continue;
                if (k == EnemyKind.Horn && Count(k) >= Mathf.RoundToInt(GameConfig.Get("room.horn_max", 1f))) continue;
                if (k == EnemyKind.Snake && Count(k) >= Mathf.RoundToInt(GameConfig.Get("room.snake_max", 2f))) continue;
                kinds.Add(k);
            }
            if (kinds.Count == 0 || _waves.Count == 0) return;
            _eliteKind = kinds[_rng.Next(kinds.Count)];
            var traits = new List<EliteTrait>();
            foreach (EliteTrait t in Enum.GetValues(typeof(EliteTrait)))
                if (EnemyBrain.Allows(_eliteKind, t)) traits.Add(t);
            _eliteTrait = traits[_rng.Next(traits.Count)];
            _eliteWave = _rng.Next(_waves.Count);
        }

        public bool RoomHasElite => _eliteWave >= 0;

        // ── 웨이브

        void StartNextWave()
        {
            _wave++;
            if (_waves == null || _wave >= _waves.Count)
            {
                ClearRoom();
                return;
            }
            Vector3 houndSpot = Vector3.zero;
            bool houndPaired = false;
            foreach (var kind in _waves[_wave])
            {
                Vector3 p = kind == EnemyKind.Hound && houndPaired ? director.FindFreeSpot(houndSpot, 1.2f) : PickSpot(); // 들개 무리는 붙어서
                if (kind == EnemyKind.Hound)
                {
                    houndPaired = !houndPaired;
                    houndSpot = p;
                }
                var marker = Telegraph.Create(null, director.Visuals.telegraphBase, director.Visuals.telegraphFill, director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Circle(0.6f), true);
                marker.SetPose(p, Vector3.forward);
                marker.SetProgress(0f);
                _pending.Add((kind, p, marker, false));
            }
            if (_wave == _eliteWave)
            {
                Vector3 p = PickSpot();
                var marker = Telegraph.Create(null, director.Visuals.telegraphBase, director.Visuals.telegraphFill, director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Circle(0.85f), true); // 정예는 예고 원이 조금 더 커요
                marker.SetPose(p, Vector3.forward);
                marker.SetProgress(0f);
                _pending.Add((_eliteKind, p, marker, true));
            }
            _timer = _warningTime;
            _warning = true;
        }

        void TickFight()
        {
            if (_warning)
            {
                _timer -= Time.deltaTime;
                float t = _warningTime > 0f ? 1f - Mathf.Clamp01(_timer / _warningTime) : 1f;
                foreach (var p in _pending) if (p.marker != null) p.marker.SetProgress(t);
                if (_timer > 0f) return;
                SpawnPending();
                return;
            }
            foreach (var e in director.Enemies) if (e != null && !e.IsDead) return;
            if (AssassinIncoming) return; // 살수가 들어오는 중
            if (_duel)
            {
                EndDuel();
                return;
            }
            StartNextWave();
        }

        void SpawnPending()
        {
            int archers = director.Count(EnemyKind.Archer);
            int archerLimit = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f));
            float health = _path.Dense ? GameConfig.Get("room.dense_health", 1.3f) : 1f;
            foreach (var p in _pending)
            {
                if (p.marker != null) Destroy(p.marker.gameObject);
                if (p.kind == EnemyKind.Archer && archers >= archerLimit) continue; // 앞 웨이브 궁수가 살아 있으면 건너뜀
                if (p.kind == EnemyKind.Archer) archers++;
                var enemy = director.Spawn(p.kind, p.position);
                if (enemy == null) continue;
                if (p.elite)
                {
                    enemy.MakeElite(_eliteTrait);
                    _elitesThisRoom++;
                }
                if (health != 1f) enemy.Damageable.ScaleMaxHealth(health);
            }
            _pending.Clear();
            _warning = false;
        }

        void ClearRoom()
        {
            MarkSave(); // 기물 고르기 등이 끝난 뒤 저장돼요
            _fighting = false;
            _room.Cleared = true;
            SetLocks(false);
            AddTracking(Mathf.RoundToInt(GameConfig.Get("tracking.per_room", 1f)));
            float mult = _path.Dense ? DenseMultiplier : 1f;
            int silver = BoostSilver(Mathf.RoundToInt(GameConfig.Get("silver.room", 18f) * mult) + _elitesThisRoom * Mathf.RoundToInt(GameConfig.Get("silver.elite", 15f)));
            Silver += silver;
            string text = $"방을 비웠어요 · 은자 +{silver}";
            if (_roomHeal > 0f && !Player.IsDead) // 면벽수련 강 10: 방을 비우면 체력 회복
            {
                float heal = Player.MaxHealth * _roomHeal;
                Player.Heal(heal);
                text += $" · 체력 +{heal:0}";
            }
            if (_rescueFight)
            {
                _rescueFight = false;
                arena.GrantOffer(RareOnlyWeights()); // 붙잡힌 무인 구출 보상
                text += " · 무인을 구했어요, 희귀 기물";
            }
            if (_tombFight)
            {
                _tombFight = false;
                arena.GrantOffer(TombWeights()); // 검이 꽂힌 무덤: 전설이 잘 나와요
                text += " · 망령이 흩어지고 검이 빛나요, 기물 고르기";
            }
            if (_escortFight)
            {
                _escortFight = false;
                int pay = BoostSilver(Mathf.RoundToInt(GameConfig.Get("event.escort_silver", 150f)));
                Silver += pay;
                text += $" · 표국을 지켜 냈어요, 은자 +{pay}";
            }
            if (_room.Kind == FloorRoomKind.Reward)
            {
                if (GrantTutorialReward()) text += " · 첫 기물";
                else if (_path.Reward == RewardKind.Item)
                {
                    arena.GrantOffer(_path.Dense ? DenseGradeWeights() : null);
                    text += " · 기물 고르기";
                }
                else if (_path.Reward == RewardKind.Forge)
                {
                    OpenForge(_path.Dense ? 2 : 1);
                    text += " · 대장간";
                }
                else if (_path.Reward == RewardKind.Part)
                {
                    OpenPartChoice(_path.Dense ? 2 : 1);
                    text += " · 부품 고르기";
                }
                else
                {
                    float heal = Player.MaxHealth * GameConfig.Get("room.heal_ratio", 0.3f) * mult;
                    Player.Heal(heal);
                    text += $" · 운기조식 체력 +{heal:0}";
                }
            }
            ShowBanner(text);
        }

        // 짙은 길 기물: 다음 지역(폐사찰) 등급표 (등급확률 시트)
        static float[] DenseGradeWeights() => new[]
        {
            GameConfig.Get("item_grade_dense.common", 0.52f), GameConfig.Get("item_grade_dense.uncommon", 0.3f),
            GameConfig.Get("item_grade_dense.rare", 0.14f), GameConfig.Get("item_grade_dense.legendary", 0.04f),
        };

        // ── 객잔

        public void EnterInn()
        {
            var db = GameConfig.Items;
            EnterInnSite(db == null ? null : InnShop.Stock(db.Items, Player.Items, ApplyLuck(RegionGradeWeights(), _luck), _rng, _innStockBonus, _unlockedParts, _innPriceScale, Player.Skill));
            int before = Tracking;
            Tracking = Mathf.FloorToInt(Tracking * GameConfig.Get("tracking.inn_ratio", 0.5f)); // 객잔에 들르면 추적도 절반
            int interest = InnInterest(); // 면벽수련 은 20
            Silver += interest;
            Notify("inn", "객잔: 점소이(F)에게 기물·금창약을 사요 · 들르면 추적도 절반 · 북쪽 문은 정비실");
            ShowBanner($"객잔 · 점소이 앞에서 F로 진열대 (북쪽 문: 정비실) · 추적도 {before} → {Tracking}{(interest > 0 ? $" · 은자 이자 +{interest}" : "")}");
        }

        // 객잔 자리로 옮기기 (이어하기에서는 저장한 진열 그대로, 추적도·이자는 다시 안 받아요)
        void EnterInnSite(InnShop shop)
        {
            ClearPending();
            _phase = Phase.Inn;
            _room = null;
            _fighting = false;
            MovePlayer(inn.entry);
            director.ResetRoom();
            Shop = shop;
            MarkSave();
        }

        // 객잔 진열: 그 지역 등급표 (등급확률 시트 죽림 줄)
        static float[] RegionGradeWeights() => new[]
        {
            GameConfig.Get("item_grade.common", 0.58f), GameConfig.Get("item_grade.uncommon", 0.3f),
            GameConfig.Get("item_grade.rare", 0.1f), GameConfig.Get("item_grade.legendary", 0.02f),
        };

        void TickInn()
        {
            var keyboard = Keyboard.current;
            if (AtCounter && Shop != null && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                OpenShop();
                return;
            }
            if (OffersOpen || Flat(Player.transform.position - inn.exit).magnitude > inn.exitRadius) return;
            EnterRest();
        }

        public void OpenShop()
        {
            if (Shop == null) return;
            IsShopping = true;
            ShopNotice = null;
            arena.Blocked = true;
            Player.enabled = false;
            Time.timeScale = 0f;
        }

        public void CloseShop()
        {
            if (!IsShopping) return;
            MarkSave();
            IsShopping = false;
            arena.Blocked = false;
            Player.enabled = true;
            Time.timeScale = 1f;
        }

        // 1~9로 사기, F·Esc 닫기
        void TickShopKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            {
                CloseShop();
                return;
            }
            for (int i = 0; i < Shop.Slots.Count && i < 9; i++)
                if (((KeyControl)keyboard[(Key)((int)Key.Digit1 + i)]).wasPressedThisFrame) Buy(i);
        }

        public bool Buy(int index)
        {
            if (Shop == null) return false;
            string blocker = Shop.Blocker(index, Player, Silver);
            if (blocker != null)
            {
                ShopNotice = blocker;
                return false;
            }
            var slot = Shop.Slots[index];
            int paid = Shop.Buy(index, Player, Silver);
            Silver -= paid;
            ShopNotice = null;
            // 둘 곳이 없으면 버릴 기물을 고르고, 포기하면 은자를 돌려받아요
            if (slot.Kind == InnShop.SlotKind.Item && Player.Items.Pending == slot.Item)
                Player.Items.OnPendingResolved(taken =>
                {
                    if (taken) return;
                    Silver += paid;
                    slot.Sold = false;
                });
            return true;
        }

        // 테스트용
        public void GiveSilver(int amount) => Silver += amount;

        // ── 정비실과 녹림왕

        public void EnterRest()
        {
            ClearPending();
            _phase = Phase.Rest;
            _room = null;
            _fighting = false;
            MovePlayer(rest.entry);
            director.ResetRoom();
            _restGauge = Player.Gauge;
            Notify("rest", "정비실: 보스 전 마지막 정비 · 목인장으로 지금 빌드를 시험해 봐요");
            ShowBanner("정비실 · 목인장으로 시험해 봐요 (나가면 기세·쿨타임이 돌아와요)");
            MarkSave();
        }

        void TickRestExit()
        {
            if (OffersOpen || Flat(Player.transform.position - rest.exit).magnitude > rest.exitRadius) return;
            Player.RestoreAfterTraining(_restGauge);
            _phase = Phase.Boss;
            MovePlayer(boss.entry);
            director.ResetRoom();
            Player.EnterRoom();
            var nokrim = arena.SpawnBoss();
            OnFightStart();
            if (_bossGauge > 0f) Player.RaiseGaugeTo(_bossGauge); // 기시감: 천마에 대해 물음 (마지막 보스전 = 지금은 녹림왕)
            bool hunted = Tracking >= Mathf.RoundToInt(GameConfig.Get("tracking.assassin2", 10f));
            if (hunted && nokrim != null) nokrim.Damageable.ScaleMaxHealth(GameConfig.Get("tracking.boss_hp", 1.15f)); // 추적도 10 이상: 보스 체력 +15%
            ShowBanner(hunted ? $"녹림왕 · 추적도 {Tracking}: 체력 +15%" : "녹림왕");
        }

        void FinishRun()
        {
            _phase = Phase.Done;
            _record.FloorsCleared++; // 보스 층 돌파
            int silver = BoostSilver(Mathf.RoundToInt(GameConfig.Get("silver.boss", 50f)));
            Silver += silver;
            arena.GrantOffer(SpecialGradeWeights()); // 보스·살수 특별 보상 표
            var items = Player.Items;
            if (items != null)
                items.ExpandBoard(
                    Mathf.RoundToInt(GameConfig.Get("meridian.width_after_boss1", 6f)),
                    Mathf.RoundToInt(GameConfig.Get("meridian.height_after_boss1", 4f)));
            ShowBanner($"녹림왕 격파! 은자 +{silver} · 경맥도가 넓어졌어요 (6×4) · F5 새 판", 6f);
        }

        // ── 도움

        void SetLocks(bool locked)
        {
            for (int d = 0; d < 4; d++)
                if (combat.locks[d] != null) combat.locks[d].SetActive(locked && _map != null && _room != null && _map.Neighbor(_room, d) != null);
        }

        void SetGateMarker(bool on)
        {
            if (combat.gateMarker != null) combat.gateMarker.SetActive(on);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        Vector3 PickSpot()
        {
            Vector3 player = Player.transform.position;
            float halfX = combat.size.x * 0.5f - 1.5f, halfZ = combat.size.y * 0.5f - 1.5f;
            for (int i = 0; i < 40; i++)
            {
                var p = combat.center + new Vector3(((float)_rng.NextDouble() * 2f - 1f) * halfX, 0f, ((float)_rng.NextDouble() * 2f - 1f) * halfZ);
                if (Flat(p - player).magnitude < _minSpawnDistance) continue;
                bool nearOther = false;
                foreach (var q in _pending) if ((q.position - p).sqrMagnitude < 1.2f * 1.2f) nearOther = true;
                if (nearOther || !director.IsFree(p, 0.45f)) continue;
                return p;
            }
            return director.FindFreeSpot(combat.center, 3f);
        }

        void MovePlayer(Vector3 to)
        {
            var player = Player;
            player.ClearDaggers(); // 방을 옮기면 남은 비도는 사라져요
            var cc = player.GetComponent<CharacterController>();
            Vector3 from = player.transform.position;
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(to.x, 0f, to.z);
            if (cc != null) cc.enabled = true;
            CinemachineCore.OnTargetObjectWarped(player.transform, player.transform.position - from); // 카메라가 미끄러지지 않게
        }

        void ClearPending()
        {
            foreach (var p in _pending) if (p.marker != null) Destroy(p.marker.gameObject);
            _pending.Clear();
            _warning = false;
        }

        void ShowBanner(string text, float seconds = 2.5f)
        {
            _banner = text;
            _bannerTimer = seconds;
        }

        void OnDestroy()
        {
            if (IsForking || IsShopping || IsInEvent) Time.timeScale = 1f;
            AThousandDawns.Combat.Damageable.AnyDied -= OnAnyDied;
        }

        // HUD 한 줄
        public string StatusText
        {
            get
            {
                string where;
                switch (_phase)
                {
                    case Phase.Fork: where = $"죽림 {_floor}층 갈림길"; break;
                    case Phase.Floor:
                        string wave = _fighting && _waves != null ? $" · 웨이브 {Mathf.Clamp(_wave + 1, 1, _waves.Count)}/{_waves.Count}" : "";
                        where = $"죽림 {_floor}층 · {PathLabel(_path)} · 남은 전투 방 {_map.FightsLeft()}{wave}";
                        break;
                    case Phase.Inn: where = "죽림 · 객잔"; break;
                    case Phase.Master: where = "죽림 2층 관문 · 고수"; break;
                    case Phase.Rest: where = "죽림 보스 층 · 정비실"; break;
                    case Phase.Boss: where = "죽림 보스 층 · 녹림왕"; break;
                    case Phase.Done: where = "판 끝"; break;
                    default: where = IsForking ? $"죽림 {_floor}층 갈림길" : ""; break;
                }
                if (IsForking) where = $"죽림 {_floor}층 갈림길";
                return $"{where} · 은자 {Silver} · 추적도 {Tracking} · 시드 {Seed}";
            }
        }
    }
}
