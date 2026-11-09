using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
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
    // 기물이 생기기 전까지 키워드 개수를 0 → 2 → 4 → 6 → 0 으로 돌려요: F1 #열양 · F2 #빙백 · F3 #뇌정 · F4 #강기 · F6 #금강 · F7 #경공 · F8 #마도 · F9 #정종
    // 기물 상태별 개수는 0 → 5 → 0: F10 기 부족 · F11 운기 · F12 주화입마 (#경공·#마도·#정종이 읽어요)
    // 기물 (간이 방식, 경맥도 전): 들판의 무리나 녹림왕을 다 쓰러뜨리면 3개 중 하나 고르기 (1·2·3, Esc 넘기기, 고르는 동안 시간 정지).
    // I 기물 고르기 바로 열기 · E 경맥도 (MeridianScreen: 고른 기물은 경맥도 빈 칸에 왼쪽 위부터 놓이고, 가득 차면 보관함으로)
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
        [SerializeField] bool sandbox = true; // 끄면 판 구조 씬 (RoomRun이 적·보상을 맡고, 여기선 시험 키·기물 고르기·녹림왕만)
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

        int _choiceCount, _pendingOffers, _lastField;
        float[] _gradeWeights;
        bool _bossWasAlive;
        List<ItemDefinition> _offer;
        readonly Queue<float[]> _offerWeights = new Queue<float[]>(); // 짙은 길 보상처럼 등급표가 다른 고르기

        public int RoundNumber { get; private set; }
        public NokrimBrain Boss => _boss;
        public bool IsChoosing => _offer != null;
        public IReadOnlyList<ItemDefinition> Offer => _offer;
        public int PendingOffers => _pendingOffers;
        public MeridianScreen Meridian => _meridian;
        public bool Blocked { get; set; } // 갈림길 화면처럼 다른 창이 떠 있으면 시험 키를 받지 않아요
        MeridianScreen _meridian;

        void Start()
        {
            if (!TryGetComponent(out _meridian)) _meridian = gameObject.AddComponent<MeridianScreen>();
            _meridian.Init(director, this);
            _archerLimit = Mathf.RoundToInt(GameConfig.Get("archer.max_per_room", 2f));
            _wakeRadius = GameConfig.Get("enemy.wake_radius", 7f);
            _choiceCount = Mathf.Max(1, Mathf.RoundToInt(GameConfig.Get("item.choice_count", 3f)));
            _gradeWeights = new[]
            {
                GameConfig.Get("item_grade.common", 0.58f), GameConfig.Get("item_grade.uncommon", 0.3f),
                GameConfig.Get("item_grade.rare", 0.1f), GameConfig.Get("item_grade.legendary", 0.02f),
            };
            SetGate(false);
            if (AutoSpawn && sandbox) SpawnLayout();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (UpdateDiscard(keyboard)) return; // 둘 곳이 없는 새 기물: 버릴 기물 고르기 (갈림길·객잔·이벤트 위에도 떠요)
            if (Blocked) return;
            if (_meridian != null && _meridian.IsOpen) return; // 경맥도를 보는 동안은 다른 키를 받지 않아요
            if (UpdateItemChoice(keyboard)) return; // 기물을 고르는 동안은 다른 키를 받지 않아요
            if (keyboard != null)
            {
                bool elite = keyboard.shiftKey.isPressed; // Shift + 숫자: 정예 (특성은 무작위)
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnKey(EnemyKind.Grunt, elite);
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnKey(EnemyKind.Boar, elite);
                if (keyboard.digit3Key.wasPressedThisFrame) SpawnKey(EnemyKind.Archer, elite);
                if (keyboard.digit4Key.wasPressedThisFrame) { SpawnKey(EnemyKind.Hound, elite); SpawnKey(EnemyKind.Hound, elite); } // 들개는 무리로
                if (keyboard.digit5Key.wasPressedThisFrame) SpawnKey(EnemyKind.Shield, elite);
                if (keyboard.digit6Key.wasPressedThisFrame) SpawnKey(EnemyKind.Horn, elite);
                if (keyboard.digit7Key.wasPressedThisFrame) SpawnKey(EnemyKind.Bomber, elite);
                if (keyboard.digit8Key.wasPressedThisFrame) SpawnKey(EnemyKind.Snake, elite);
                if (keyboard.digit9Key.wasPressedThisFrame) SpawnBoss();
                if (keyboard.digit0Key.wasPressedThisFrame) SpawnKey(EnemyKind.Assassin, false); // 마교 살수
                if (keyboard.minusKey.wasPressedThisFrame) SpawnMaster(EnemyKind.Qingzhu); // 고수 청죽객
                if (keyboard.equalsKey.wasPressedThisFrame) SpawnMaster(EnemyKind.IronFist); // 고수 철권 마웅
                if (keyboard.gKey.wasPressedThisFrame && director != null && director.Player != null) director.Player.FillGauge();
                if (keyboard.lKey.wasPressedThisFrame && director != null && director.Player != null) director.Player.GainLevelForTest(); // 시험용 레벨업
                if (keyboard.kKey.wasPressedThisFrame && director != null && director.Player != null) director.Player.KillForTest(); // 시험용: 바로 쓰러지기
                if (keyboard.pKey.wasPressedThisFrame && director != null && director.Player != null && director.Player.Items != null) // 시험용: 부품 셋
                {
                    director.Player.Items.AddPart(PartKind.Tongmaek);
                    director.Player.Items.AddPart(PartKind.Jeomhyeol);
                    director.Player.Items.AddPart(PartKind.Hanok);
                }
                if (director != null && director.Player != null && director.Player.Keywords != null)
                {
                    if (keyboard.f1Key.wasPressedThisFrame) director.Player.Keywords.CycleStage(Keyword.Yeolyang);
                    if (keyboard.f2Key.wasPressedThisFrame) director.Player.Keywords.CycleStage(Keyword.Bingbaek);
                    var keywords = director.Player.Keywords;
                    if (keyboard.f3Key.wasPressedThisFrame) keywords.CycleStage(Keyword.Noejeong);
                    if (keyboard.f4Key.wasPressedThisFrame) keywords.CycleStage(Keyword.Ganggi);
                    if (keyboard.f6Key.wasPressedThisFrame && keywords.CycleStage(Keyword.Geumgang) >= 2) director.Player.EnterRoom(); // 바로 시험할 수 있게 방 보호막도
                    if (keyboard.f7Key.wasPressedThisFrame) keywords.CycleStage(Keyword.Gyeonggong);
                    if (keyboard.f8Key.wasPressedThisFrame) keywords.CycleStage(Keyword.Mado);
                    if (keyboard.f9Key.wasPressedThisFrame) keywords.CycleStage(Keyword.Jeongjong);
                    if (keyboard.f10Key.wasPressedThisFrame) keywords.CycleQi(QiState.Lacking);
                    if (keyboard.f11Key.wasPressedThisFrame) keywords.CycleQi(QiState.Circulating);
                    if (keyboard.f12Key.wasPressedThisFrame) keywords.CycleQi(QiState.Deviated);
                }
            }

            // 녹림왕이 쓰러지면 결계가 풀려요
            if (bossGate != null && bossGate.activeSelf && (_boss == null || _boss.IsDead)) SetGate(false);

            if (!sandbox || !AutoSpawn || director == null) return;
            DetectClears();
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
            if (director.Player != null) director.Player.EnterRoom(); // #금강 2단계: 방에 들어갈 때마다 보호막
        }

        public NokrimBrain SpawnBoss()
        {
            if (_boss != null && !_boss.IsDead) return _boss;
            _boss = (NokrimBrain)director.Spawn(EnemyKind.Nokrim, bossZoneCenter + new Vector3(0f, 0f, 2f));
            _boss.SetZone(bossZoneCenter, new Vector2(GameConfig.Get("nokrim.arena_width", 16f), GameConfig.Get("nokrim.arena_depth", 12f)));
            _boss.WakeRadius = 1f; // SetZone이 있으면 결투장 안에 들어와야 깨어나요
            _boss.Woke += _ =>
            {
                SetGate(true);
                if (director.Player != null) director.Player.EnterRoom(); // 결투장도 방
            };
            return _boss;
        }

        void SetGate(bool closed)
        {
            if (bossGate != null) bossGate.SetActive(closed);
        }

        // ── 기물 고르기 (간이 방식, 경맥도 전)

        // 들판의 무리(녹림왕 말고 전부)를 다 쓰러뜨리거나 녹림왕을 쓰러뜨리면 하나씩 쌓여요. 녹림왕과 싸우는 동안 부하는 세지 않아요.
        void DetectClears()
        {
            bool bossAlive = _boss != null && !_boss.IsDead;
            bool bossFight = bossAlive && _boss.IsAwake;
            int field = 0;
            foreach (var e in director.Enemies)
                if (e != null && e != _boss && !e.IsDead) field++;
            if (!bossFight && _lastField > 0 && field == 0) _pendingOffers++;
            _lastField = bossFight ? 0 : field;
            if (_bossWasAlive && !bossAlive) _pendingOffers++;
            _bossWasAlive = bossAlive;
        }

        // ── 버릴 기물 고르기 (경맥도도 주머니도 가득일 때 새 기물을 받으면)

        bool _discardPaused, _discardPrevEnabled;
        float _discardPrevScale;

        public ItemDefinition PendingItem => director != null && director.Player != null && director.Player.Items != null ? director.Player.Items.Pending : null;

        // 고르는 중이면 true. 1~9 가진 기물을 버리고 받기 · Esc 새 기물 포기
        bool UpdateDiscard(Keyboard keyboard)
        {
            var player = director != null ? director.Player : null;
            if (PendingItem == null)
            {
                if (_discardPaused)
                {
                    _discardPaused = false;
                    Time.timeScale = _discardPrevScale;
                    if (player != null) player.enabled = _discardPrevEnabled;
                }
                return false;
            }
            if (!_discardPaused)
            {
                _discardPaused = true;
                _discardPrevScale = Time.timeScale;
                _discardPrevEnabled = player.enabled;
                Time.timeScale = 0f;
                player.enabled = false;
            }
            if (keyboard == null) return true;
            if (keyboard.escapeKey.wasPressedThisFrame) DeclinePending();
            else
                for (int i = 0; i < 9 && i < player.Items.Items.Count; i++)
                    if (((UnityEngine.InputSystem.Controls.KeyControl)keyboard[(Key)((int)Key.Digit1 + i)]).wasPressedThisFrame)
                    {
                        Discard(i);
                        break;
                    }
            return true;
        }

        public void Discard(int index)
        {
            var items = director.Player.Items;
            if (items.Pending == null || index < 0 || index >= items.Items.Count) return;
            items.ResolvePending(items.Items[index]);
        }

        public void DeclinePending()
        {
            var items = director.Player.Items;
            if (items.Pending != null) items.ResolvePending(null);
        }

        // 판 구조: 방을 비우거나 녹림왕을 쓰러뜨리면 RoomRun이 불러요.
        // gradeWeights가 없으면 이 지역 등급표 (등급확률 시트)
        public void GrantOffer(float[] gradeWeights = null)
        {
            _offerWeights.Enqueue(gradeWeights ?? _gradeWeights);
            _pendingOffers++;
        }

        // 고르는 중이면 true. 1·2·3 (또는 화면 클릭) 고르기 · Esc 넘기기 · I 하나 열기
        bool UpdateItemChoice(Keyboard keyboard)
        {
            var player = director != null ? director.Player : null;
            if (player == null || player.Items == null) return false;

            if (IsChoosing)
            {
                if (keyboard == null) return true;
                if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
                else if (keyboard.escapeKey.wasPressedThisFrame) CloseOffer();
                return true;
            }

            if (keyboard != null)
            {
                if (keyboard.iKey.wasPressedThisFrame) _pendingOffers++;
            }
            if (_pendingOffers > 0 && !player.IsDead) OpenOffer(player);
            return IsChoosing;
        }

        void OpenOffer(PlayerController player)
        {
            var db = GameConfig.Items;
            var weights = _offerWeights.Count > 0 ? _offerWeights.Dequeue() : _gradeWeights;
            _offer = db == null ? null : ItemOffer.Roll(db.Items, player.Items.Has, _choiceCount, weights, () => UnityEngine.Random.value);
            if (_offer == null || _offer.Count == 0)
            {
                _offer = null;
                _pendingOffers = 0; // 더 얻을 기물이 없어요
                _offerWeights.Clear();
                return;
            }
            Time.timeScale = 0f;
            player.enabled = false; // 고르는 클릭이 평타로 남지 않게
        }

        public void Choose(int index)
        {
            if (!IsChoosing || index < 0 || index >= _offer.Count) return;
            director.Player.Items.Add(_offer[index]);
            CloseOffer();
        }

        public void CloseOffer()
        {
            if (!IsChoosing) return;
            _offer = null;
            _pendingOffers = Mathf.Max(0, _pendingOffers - 1);
            Time.timeScale = 1f;
            if (director != null && director.Player != null) director.Player.enabled = true;
        }

        void OnDestroy()
        {
            if (IsChoosing) Time.timeScale = 1f;
        }

        // 고수 (청죽객·철권 마웅): 플레이어 둘레에 바로 깨어 있는 채로. 죽림섬·대나무 끝점은 지금 맵(넓은 죽림이면 들판) 안으로 당겨요.
        [SerializeField] Vector3 masterZoneCenter = new Vector3(0f, 0f, -2f);
        [SerializeField] Vector2 masterZoneSize = new Vector2(40f, 24f);

        public EnemyBrain SpawnMaster(EnemyKind kind)
        {
            foreach (var e in director.Enemies)
                if (e is IMasterBrain && !e.IsDead) return e; // 고수는 한 번에 하나
            var master = SpawnNearPlayer(kind);
            if (master is IMasterBrain info) info.SetZone(masterZoneCenter, masterZoneSize);
            return master;
        }

        void SpawnKey(EnemyKind kind, bool elite)
        {
            var enemy = SpawnNearPlayer(kind);
            if (enemy == null || !elite) return;
            var traits = new List<EliteTrait>();
            foreach (EliteTrait t in Enum.GetValues(typeof(EliteTrait)))
                if (EnemyBrain.Allows(kind, t)) traits.Add(t);
            if (traits.Count > 0) enemy.MakeElite(traits[UnityEngine.Random.Range(0, traits.Count)]);
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
