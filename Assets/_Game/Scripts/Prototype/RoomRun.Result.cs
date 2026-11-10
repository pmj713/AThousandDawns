using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 판 결과 화면: 쓰러지거나 녹림왕을 잡으면 (보상 기물을 고른 뒤) 떠요.
    // 돌파한 층과 처치 기록을 회귀의 기억으로 바꿔 보여 주고, 판 밖 저장(MetaSave)에 더해요. Enter·F5로 다시 회귀.
    public sealed partial class RoomRun
    {
        public sealed class ResultView
        {
            public bool Victory;
            public string Where;
            public (string label, float count, float memory)[] Lines;
            public int Memory, StoredMemory, Runs;
            public float PlaySeconds;
            public (string label, string value)[] Stats;
            public float ShownAt; // 기록이 한 줄씩 차오르는 연출 시작 (unscaled)
        }

        readonly RunRecord _record = new RunRecord();
        float _deathTimer = -1f;

        public RunRecord Record => _record;
        public ResultView Result { get; private set; }
        public bool IsShowingResult => Result != null;

        void OnEnable()
        {
            Damageable.AnyDied += OnAnyDied;
            Damageable.AnyDamaged += OnAnyDamaged;
        }

        void OnDisable()
        {
            Damageable.AnyDied -= OnAnyDied;
            Damageable.AnyDamaged -= OnAnyDamaged;
            if (_hurtHooked != null) _hurtHooked.Hurt -= OnPlayerHurt;
        }

        // 기록: 적에게 준 피해 · 최대 단일 피해 (목인장 같은 적 아닌 것은 빼요), 받은 피해
        void OnAnyDamaged(Damageable d, int amount)
        {
            if (IsShowingResult || amount <= 0 || d == null || !d.TryGetComponent(out EnemyBrain _)) return;
            _record.DamageDealt += amount;
            _record.MaxHit = Mathf.Max(_record.MaxHit, amount);
        }

        AThousandDawns.Player.PlayerController _hurtHooked;

        void HookPlayerRecord()
        {
            if (_hurtHooked != null || Player == null) return;
            _hurtHooked = Player;
            Player.Hurt += OnPlayerHurt;
        }

        void OnPlayerHurt(int amount)
        {
            if (!IsShowingResult && amount > 0) _record.DamageTaken += amount;
        }

        // 플레이 시간: 마을·결과 화면은 빼고, 멈춰 있는 동안은 안 셈 (Update에서)
        void TickRecord()
        {
            HookPlayerRecord();
            if (IsShowingResult || _phase == Phase.Village) return;
            _record.PlaySeconds += Time.deltaTime;
            _record.MaxTracking = Mathf.Max(_record.MaxTracking, Tracking);
        }

        // 처치 기록 (목인장처럼 적이 아닌 것은 빼요)
        void OnAnyDied(Damageable d)
        {
            if (IsShowingResult || d == null || !d.TryGetComponent(out EnemyBrain brain)) return;
            _record.AddKill(brain.Rank);
        }

        // Update 맨 앞에서: 결과 화면 키, 쓰러짐 감지, 판 끝 감지. 결과를 보는 중이면 true.
        bool TickResult()
        {
            if (IsShowingResult)
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return true;
                if (_book != null && _book.IsOpen)
                {
                    if (keyboard.bKey.wasPressedThisFrame) _book.Close();
                    return true;
                }
                if (keyboard.bKey.wasPressedThisFrame && _book != null) _book.Open(); // 회귀록
                else if (keyboard.enterKey.wasPressedThisFrame) Restart();
                return true;
            }
            if (Player.IsDead)
            {
                if (_deathTimer < 0f) _deathTimer = GameConfig.Get("result.death_delay", 1.5f);
                _deathTimer -= Time.unscaledDeltaTime;
                if (_deathTimer <= 0f) ShowResult(false);
                return true;
            }
            _deathTimer = -1f; // 기사회생했으면 다시 처음부터
            if (_phase == Phase.Done && !OffersOpen) // 녹림왕 보상 기물을 고른 뒤
            {
                ShowResult(true);
                return true;
            }
            return false;
        }

        public void ShowResult(bool victory)
        {
            if (IsShowingResult) return;
            var weights = RunRecord.Weights.FromConfig();
            int memory = _record.Memory(weights);
            var meta = MetaSave.Load();
            meta.memory += memory;
            meta.totalMemory += memory;
            meta.runs++;
            meta.bestFloors = Mathf.Max(meta.bestFloors, _record.FloorsCleared);
            meta.bossKills += _record.Kills[(int)EnemyRank.Boss];
            meta.Save();
            RunSave.Delete(); // 판이 끝났으니 이어할 것도 없어요
            _saveDirty = false;

            Result = new ResultView
            {
                Victory = victory,
                Where = victory ? $"{RegionName} 돌파" : $"{WhereText()} · {DeathCause()}",
                Lines = _record.Lines(weights),
                Memory = memory,
                StoredMemory = meta.memory,
                Runs = meta.runs,
                PlaySeconds = _record.PlaySeconds,
                Stats = _record.Stats(),
                ShownAt = Time.unscaledTime,
            };
            if (IsShopping) CloseShop();
            if (IsInEvent)
            {
                _afterEvent = null; // 고수 선택 뒤 갈림길로 넘어가지 않게
                CloseEvent();
            }
            ClearPending();
        }

        // 사망 원인 (기획: "청죽객의 죽림섬에 쓰러짐")
        string DeathCause() => string.IsNullOrEmpty(Player.LastHitBy) ? "쓰러짐" : $"{Player.LastHitBy} 쓰러짐";

        string WhereText() => _phase switch
        {
            Phase.Floor => $"{RegionName} {_floor}층",
            Phase.Fork => $"{RegionName} {_floor}층 갈림길",
            Phase.Master => $"{RegionName} 2층 관문 ({(_master != null ? _master.DisplayName : "고수")})",
            Phase.Inn => "객잔",
            Phase.Rest => "정비실",
            Phase.Village => "마을",
            Phase.Boss => $"{RegionName} 보스 앞",
            _ => RegionName,
        };

        // 회귀: 먹이 덮었다가 되감기듯 걷혀요. 첫 회귀(끝난 판이 1번)면 "그날 아침으로 돌아왔다" → 一朝千回
        public void Restart()
        {
            if (AThousandDawns.Fx.InkTransition.IsPlaying) return; // 연출 중에 다시 눌러도 한 번만
            Time.timeScale = 1f;
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (!Transitions)
            {
                SceneManager.LoadScene(scene);
                return;
            }
            bool firstDawn = MetaSave.Load().runs == 1;
            AThousandDawns.Fx.InkTransition.Play(AThousandDawns.Fx.InkTransition.Kind.Regression, () => SceneManager.LoadScene(scene), firstDawn);
        }
    }
}
