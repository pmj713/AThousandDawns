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
        }

        readonly RunRecord _record = new RunRecord();
        float _deathTimer = -1f;

        public RunRecord Record => _record;
        public ResultView Result { get; private set; }
        public bool IsShowingResult => Result != null;

        void OnEnable() => Damageable.AnyDied += OnAnyDied;
        void OnDisable() => Damageable.AnyDied -= OnAnyDied;

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

            Result = new ResultView
            {
                Victory = victory,
                Where = victory ? "녹림왕 격파 · 죽림 돌파" : $"{WhereText()}에서 쓰러짐",
                Lines = _record.Lines(weights),
                Memory = memory,
                StoredMemory = meta.memory,
                Runs = meta.runs,
            };
            if (IsShopping) CloseShop();
            if (IsInEvent)
            {
                _afterEvent = null; // 고수 선택 뒤 갈림길로 넘어가지 않게
                CloseEvent();
            }
            ClearPending();
        }

        string WhereText() => _phase switch
        {
            Phase.Floor => $"죽림 {_floor}층",
            Phase.Fork => $"죽림 {_floor}층 갈림길",
            Phase.Master => $"죽림 2층 관문 ({(_master != null ? _master.DisplayName : "고수")})",
            Phase.Inn => "객잔",
            Phase.Rest => "정비실",
            Phase.Boss => "녹림왕 앞",
            _ => "죽림",
        };

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
