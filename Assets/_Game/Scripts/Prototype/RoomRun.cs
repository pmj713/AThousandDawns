using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판 구조 시험판: 죽림 일반 층(층마다 최소 경로 전투 방) → 정비실 → 녹림왕 방.
    // 전투 방: 들어오면 문이 잠기고, 방 난이도 예산으로 뽑은 적이 웨이브로 나와요 (나오기 전 바닥에 예고 원).
    // 마지막 웨이브까지 쓰러뜨리면 문이 열리고 기물 3개 중 하나. 북쪽 문으로 나가면 다음 방.
    // 정비실: 목인장 두 개, 문은 처음부터 열려 있음. 나갈 때 기세와 회선참 쿨타임을 들어올 때로 되돌려요.
    // 녹림왕을 쓰러뜨리면 기물 하나 + 경맥도 5×4 → 6×4, 판 끝.
    // 같은 시드면 같은 방 구성이 나와요 (버그 재현용). 갈림길·선택 방·이벤트·객잔은 아직 없어요.
    public sealed class RoomRun : MonoBehaviour
    {
        public enum StepKind { Combat, Rest, Boss }

        public struct Step
        {
            public StepKind Kind;
            public int Floor;  // 1부터 (보스 층은 일반 층 수 + 1)
            public int Room;   // 그 층에서 몇 번째 방 (1부터)
            public int Rooms;  // 그 층의 방 수
        }

        [Serializable]
        public sealed class Site
        {
            public Vector3 center;
            public Vector2 size = new Vector2(20f, 12f);
            public Vector3 entry;         // 들어왔을 때 서는 자리
            public Vector3 exit;          // 이 근처에 오면 다음 방 (문이 열려 있을 때)
            public float exitRadius = 1.3f;
            public GameObject door;       // 켜져 있으면 잠김
        }

        enum State { Idle, Warning, Fighting, Cleared, Done }

        // 테스트에서 고정해요. 0이면 시간으로 정해요.
        public static int SeedOverride;

        [SerializeField] EnemyDirector director;
        [SerializeField] PrototypeArena arena;
        [SerializeField] Site combat = new Site();
        [SerializeField] Site rest = new Site();
        [SerializeField] Site boss = new Site();

        readonly List<Step> _steps = new List<Step>();
        readonly List<(EnemyKind kind, Vector3 position, Telegraph marker)> _pending = new List<(EnemyKind, Vector3, Telegraph)>();
        List<List<EnemyKind>> _waves;
        System.Random _rng;
        State _state;
        int _stepIndex = -1, _wave;
        float _timer, _warning, _minSpawnDistance, _restGauge;
        string _banner;
        float _bannerTimer;

        public int Seed { get; private set; }
        public IReadOnlyList<Step> Steps => _steps;
        public int StepIndex => _stepIndex;
        public Step Current => _stepIndex >= 0 && _stepIndex < _steps.Count ? _steps[_stepIndex] : default;
        public int Wave => _wave;
        public int WaveCount => _waves != null ? _waves.Count : 0;
        public bool DoorOpen => _state == State.Cleared;
        public bool IsDone => _state == State.Done;
        public string Banner => _bannerTimer > 0f ? _banner : null;
        public IReadOnlyList<List<EnemyKind>> Waves => _waves;

        PlayerController Player => director != null ? director.Player : null;

        void Start()
        {
            Seed = SeedOverride != 0 ? SeedOverride : Environment.TickCount & 0x7fffffff;
            _rng = new System.Random(Seed);
            _warning = GameConfig.Get("room.spawn_warning", 0.8f);
            _minSpawnDistance = GameConfig.Get("room.spawn_min_distance", 5f);
            BuildSteps();
            if (PrototypeArena.AutoSpawn) Enter(0);
        }

        void BuildSteps()
        {
            _steps.Clear();
            int floors = Mathf.Max(1, Mathf.RoundToInt(GameConfig.Get("room.floors", 3f)));
            int rooms = Mathf.Max(1, Mathf.RoundToInt(GameConfig.Get("room.path_rooms", 3f)));
            for (int f = 1; f <= floors; f++)
                for (int r = 1; r <= rooms; r++)
                    _steps.Add(new Step { Kind = StepKind.Combat, Floor = f, Room = r, Rooms = rooms });
            _steps.Add(new Step { Kind = StepKind.Rest, Floor = floors + 1, Room = 1, Rooms = 2 });
            _steps.Add(new Step { Kind = StepKind.Boss, Floor = floors + 1, Room = 2, Rooms = 2 });
        }

        void Update()
        {
            if (_bannerTimer > 0f) _bannerTimer -= Time.deltaTime;
            if (_stepIndex < 0 || Player == null || Player.IsDead) return;

            switch (_state)
            {
                case State.Warning: TickWarning(); break;
                case State.Fighting: TickFight(); break;
                case State.Cleared: TickExit(); break;
            }
        }

        // ── 방 들어가기

        public void Enter(int index)
        {
            ClearPending();
            _stepIndex = index;
            var step = _steps[index];
            var site = SiteOf(step.Kind);
            MovePlayer(site.entry);
            director.ResetRoom();
            Player.EnterRoom(); // #금강 2단계: 방에 들어갈 때마다 보호막

            switch (step.Kind)
            {
                case StepKind.Combat:
                    SetDoor(site, true);
                    _waves = PlanRoom(step.Floor);
                    _wave = -1;
                    if (step.Room == 1) ShowBanner($"죽림 {step.Floor}층");
                    StartNextWave();
                    break;
                case StepKind.Rest:
                    SetDoor(site, false);
                    _waves = null;
                    _restGauge = Player.Gauge;
                    _state = State.Cleared;
                    ShowBanner("정비실 · 목인장으로 시험해 봐요 (나가면 기세·쿨타임이 돌아와요)");
                    break;
                case StepKind.Boss:
                    _waves = null;
                    arena.SpawnBoss();
                    _state = State.Fighting;
                    ShowBanner("녹림왕");
                    break;
            }
        }

        public List<List<EnemyKind>> PlanRoom(int floor)
        {
            string f = Mathf.Clamp(floor, 1, 3).ToString();
            int min = Mathf.RoundToInt(GameConfig.Get($"room.budget_min.f{f}", 6f));
            int max = Mathf.RoundToInt(GameConfig.Get($"room.budget_max.f{f}", 8f));
            int budget = _rng.Next(min, Mathf.Max(min, max) + 1);
            int wavesMin = Mathf.RoundToInt(GameConfig.Get("room.waves_min", 2f));
            int wavesMax = Mathf.RoundToInt(GameConfig.Get("room.waves_max", 3f));
            int waves = _rng.Next(wavesMin, Mathf.Max(wavesMin, wavesMax) + 1);
            var limits = new RoomBudget.Limits
            {
                Archers = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f)),
                Horns = Mathf.RoundToInt(GameConfig.Get("room.horn_max", 1f)),
                Snakes = Mathf.RoundToInt(GameConfig.Get("room.snake_max", 2f)),
            };
            return RoomBudget.Plan(budget, waves, RoomBudget.Bamboo, RoomBudget.Cost, limits, _rng);
        }

        // ── 웨이브

        void StartNextWave()
        {
            _wave++;
            if (_waves == null || _wave >= _waves.Count)
            {
                ClearRoom();
                return;
            }
            var site = combat;
            Vector3 houndSpot = Vector3.zero;
            bool houndPaired = false;
            foreach (var kind in _waves[_wave])
            {
                Vector3 p;
                if (kind == EnemyKind.Hound && houndPaired) p = director.FindFreeSpot(houndSpot, 1.2f); // 들개 무리는 붙어서
                else p = PickSpot(site);
                if (kind == EnemyKind.Hound)
                {
                    houndPaired = !houndPaired;
                    houndSpot = p;
                }
                var marker = Telegraph.Create(null, director.Visuals.telegraphBase, director.Visuals.telegraphFill, director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Circle(0.6f), true);
                marker.SetPose(p, Vector3.forward);
                marker.SetProgress(0f);
                _pending.Add((kind, p, marker));
            }
            _timer = _warning;
            _state = State.Warning;
        }

        void TickWarning()
        {
            _timer -= Time.deltaTime;
            float t = _warning > 0f ? 1f - Mathf.Clamp01(_timer / _warning) : 1f;
            foreach (var p in _pending) if (p.marker != null) p.marker.SetProgress(t);
            if (_timer > 0f) return;

            int archers = director.Count(EnemyKind.Archer);
            int archerLimit = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f));
            foreach (var p in _pending)
            {
                if (p.marker != null) Destroy(p.marker.gameObject);
                if (p.kind == EnemyKind.Archer && archers >= archerLimit) continue; // 앞 웨이브 궁수가 살아 있으면 건너뜀
                if (p.kind == EnemyKind.Archer) archers++;
                director.Spawn(p.kind, p.position);
            }
            _pending.Clear();
            _state = State.Fighting;
        }

        void TickFight()
        {
            var step = Current;
            if (step.Kind == StepKind.Boss)
            {
                var b = arena.Boss;
                if (b != null && b.IsDead) FinishRun();
                return;
            }
            if (AliveCount() > 0) return;
            StartNextWave();
        }

        int AliveCount()
        {
            int n = 0;
            foreach (var e in director.Enemies) if (e != null && !e.IsDead) n++;
            return n;
        }

        void ClearRoom()
        {
            SetDoor(combat, false);
            _state = State.Cleared;
            arena.GrantOffer();
            ShowBanner("문이 열렸어요 · 북쪽 문으로");
        }

        void FinishRun()
        {
            _state = State.Done;
            arena.GrantOffer();
            var items = Player.Items;
            if (items != null)
                items.ExpandBoard(
                    Mathf.RoundToInt(GameConfig.Get("meridian.width_after_boss1", 6f)),
                    Mathf.RoundToInt(GameConfig.Get("meridian.height_after_boss1", 4f)));
            ShowBanner("녹림왕 격파! 경맥도가 넓어졌어요 (6×4) · F5 새 판", 6f);
        }

        // ── 나가기

        void TickExit()
        {
            var step = Current;
            var site = SiteOf(step.Kind);
            Vector3 d = Player.transform.position - site.exit;
            d.y = 0f;
            if (d.magnitude > site.exitRadius) return;
            if (arena.IsChoosing || arena.PendingOffers > 0) return; // 기물을 고른 뒤에 넘어가요
            if (step.Kind == StepKind.Rest) Player.RestoreAfterTraining(_restGauge);
            if (_stepIndex + 1 < _steps.Count) Enter(_stepIndex + 1);
        }

        // ── 도움

        Site SiteOf(StepKind kind) => kind == StepKind.Combat ? combat : kind == StepKind.Rest ? rest : boss;

        static void SetDoor(Site site, bool closed)
        {
            if (site.door != null) site.door.SetActive(closed);
        }

        Vector3 PickSpot(Site site)
        {
            Vector3 player = Player.transform.position;
            float halfX = site.size.x * 0.5f - 1.5f, halfZ = site.size.y * 0.5f - 1.5f;
            for (int i = 0; i < 40; i++)
            {
                var p = site.center + new Vector3(((float)_rng.NextDouble() * 2f - 1f) * halfX, 0f, ((float)_rng.NextDouble() * 2f - 1f) * halfZ);
                Vector3 off = p - player;
                off.y = 0f;
                if (off.magnitude < _minSpawnDistance) continue;
                bool nearOther = false;
                foreach (var q in _pending) if ((q.position - p).sqrMagnitude < 1.2f * 1.2f) nearOther = true;
                if (nearOther || !director.IsFree(p, 0.45f)) continue;
                return p;
            }
            return director.FindFreeSpot(site.center, 3f);
        }

        void MovePlayer(Vector3 to)
        {
            var player = Player;
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
        }

        void ShowBanner(string text, float seconds = 2.5f)
        {
            _banner = text;
            _bannerTimer = seconds;
        }

        // HUD 한 줄
        public string StatusText
        {
            get
            {
                if (_stepIndex < 0) return $"시드 {Seed}";
                var step = Current;
                string where = step.Kind == StepKind.Combat ? $"죽림 {step.Floor}층 · 방 {step.Room}/{step.Rooms}"
                    : step.Kind == StepKind.Rest ? "죽림 보스 층 · 정비실" : "죽림 보스 층 · 녹림왕";
                string wave = step.Kind == StepKind.Combat && _waves != null && _state != State.Cleared
                    ? $" · 웨이브 {Mathf.Min(_wave + 1, _waves.Count)}/{_waves.Count}" : "";
                string door = _state == State.Cleared ? " · 문 열림" : _state == State.Done ? " · 판 끝" : "";
                return $"{where}{wave}{door} · 시드 {Seed}";
            }
        }
    }
}
