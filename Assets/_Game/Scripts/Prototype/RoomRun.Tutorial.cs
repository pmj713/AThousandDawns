using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 튜토리얼 (기획 대화 튜토리얼안). 원칙: 필요한 순간에 한 번에 하나씩, 해 보면 사라지기, 전투는 멈추지 않기.
    // 1단계 입문 죽림: 첫 판 1층만 손으로 짠 방 6개 (한 번 통과하면 다음부터는 무작위)
    //   1 빈 방: WASD 이동 · 2 졸개 둘: 첫 공격 예고에서 멈춤 → Space 대시로 피하면 다시 움직임 → 좌클릭 평타 · 3 방패 산적: 대시 공격
    //   4 졸개 무리: 우클릭 초식 → Q 절초 · 5 보상 방: 첫 기물(기 낮은 일반)과 경맥도 기초 · 6 관문
    //   2층 갈림길은 기물 옅은 길 · 짙은 길 둘만.
    // 2단계 상황별 첫 안내: 처음 일어날 때 한 번만 화면 구석에 한 줄 (본 것은 MetaSave.seenHints).
    // 조작 안내 = 화면 아래 가운데 (해 보면 사라짐), 상황 알림 = 오른쪽 위 한 줄 (몇 초 뒤 사라짐).
    public sealed partial class RoomRun
    {
        // 테스트에서 끄면 첫 판도 바로 무작위 갈림길이고 첫 안내도 안 떠요
        public static bool TutorialEnabled = true;

        public bool InTutorialFloor { get; private set; }
        public string Hint { get; private set; }          // 조작 안내 (아래 가운데)
        public string HintNotice { get; private set; }    // 상황 알림 (오른쪽 위)
        float _hintNoticeTimer;
        Func<bool> _hintDone;
        readonly Queue<string> _hintQueue = new Queue<string>();
        bool _tutorialFork;
        int _tutorialStep;
        Vector3 _hintStart;

        bool StartsTutorial() => TutorialEnabled && !MetaSave.Load().tutorialFloorDone;

        // 입문 죽림: 갈림길 없이 바로 1층 (기물 · 옅은 길)
        public void BeginTutorialFloor()
        {
            _floor = 1;
            _path = new Path { Reward = RewardKind.Item, Dense = false };
            InTutorialFloor = true;
            _map = FloorMap.Line(FloorRoomKind.Start, FloorRoomKind.Combat, FloorRoomKind.Combat, FloorRoomKind.Combat, FloorRoomKind.Reward, FloorRoomKind.Gate);
            var rooms = _map.Rooms;
            rooms[1].FixedWaves = Waves(new[] { EnemyKind.Grunt, EnemyKind.Grunt });
            rooms[2].FixedWaves = Waves(new[] { EnemyKind.Shield });
            rooms[3].FixedWaves = Waves(new[] { EnemyKind.Grunt, EnemyKind.Grunt, EnemyKind.Grunt }, new[] { EnemyKind.Grunt, EnemyKind.Grunt, EnemyKind.Grunt });
            rooms[4].FixedWaves = new List<List<EnemyKind>>(); // 보상 방은 싸움 없이
            arena.Blocked = false;
            Player.enabled = true;
            Time.timeScale = 1f;
            _phase = Phase.Floor;
            ShowBanner("죽림 1층 · 입문");
            BeginHunt();
            Enter(_map.Start, -1);
        }

        static List<List<EnemyKind>> Waves(params EnemyKind[][] waves)
        {
            var list = new List<List<EnemyKind>>();
            foreach (var w in waves) list.Add(new List<EnemyKind>(w));
            return list;
        }

        // 방에 들어갈 때 (Enter 끝에서)
        void OnTutorialEnter(FloorRoom room)
        {
            if (!InTutorialFloor) return;
            int index = room.Cell.x; // 일자 길이라 칸 x가 방 번호
            switch (index)
            {
                case 0:
                    if (_tutorialStep == 0)
                    {
                        _hintStart = Player.transform.position;
                        SetHint($"{KeyName(Bind.MoveUp)}{KeyName(Bind.MoveLeft)}{KeyName(Bind.MoveDown)}{KeyName(Bind.MoveRight)} 이동", () => Flat(Player.transform.position - _hintStart).magnitude > 2f);
                        _tutorialStep = 1;
                    }
                    break;
                case 1:
                    _dodgeArmed = true; // 졸개가 처음 공격하려는 순간 멈추고 대시를 알려 줘요
                    SetHint("산적 졸개가 다가와요 · 공격 예고(주황 바닥)를 잘 보세요", () => !_fighting);
                    break;
                case 2: SetHint("방패는 정면 공격을 막아요 · 대시(Space) 직후 좌클릭으로 대시 공격하면 깨져요", () => !_fighting); break;
                case 3:
                    Player.RaiseGaugeTo(0.8f); // 이 방에서 절초까지 써 보게 기세를 채워 둬요
                    SetHint($"{KeyName(Bind.Skill)} 초식 · 주변을 한 바퀴 베요", () => Player.IsCastingSkill || !_fighting);
                    break;
                case 5: SetHint($"관문 · 가운데에서 {KeyName(Bind.Interact)}를 누르면 다음 층", () => _phase != Phase.Floor); break;
            }
        }

        // 보상 방: 첫 기물은 기 낮은 일반 기물로 정해 두고, 경맥도를 열어 놓는 법을 보여 줘요. 처리했으면 true
        bool GrantTutorialReward()
        {
            if (!InTutorialFloor) return false;
            var db = GameConfig.Items;
            ItemDefinition pick = null;
            if (db != null)
                foreach (var d in db.Available)
                    if (d != null && d.grade == ItemGrade.Common && !Player.Items.Has(d.id) && (pick == null || d.maxQi < pick.maxQi || (d.maxQi == pick.maxQi && d.minQi < pick.minQi))) pick = d;
            if (pick == null) return false;
            Player.Items.Add(pick);
            SetHint($"첫 기물 · {pick.name} · {KeyName(Bind.Meridian)} 경맥도를 열어요", () => _meridianOpenedOnce);
            _tutorialStep = 5;
            return true;
        }

        bool _meridianOpenedOnce;

        // 입문 죽림을 나갈 때: 통과 기록 (다음 판부터 1층도 무작위) + 2층 갈림길은 둘만
        void OnTutorialFloorLeft()
        {
            if (!InTutorialFloor) return;
            InTutorialFloor = false;
            _tutorialFork = true;
            ClearHint();
            var meta = MetaSave.Load();
            meta.tutorialFloorDone = true;
            meta.Save();
        }

        // 입문 뒤 첫 갈림길: 기물 옅은 길 · 짙은 길 (기운의 개념 하나만)
        List<Path> TutorialFork()
        {
            _tutorialFork = false;
            Notify("fork_dense", "짙은 길은 적이 강한 대신 더 좋은 기물이 나와요");
            return new List<Path> { new Path { Reward = RewardKind.Item, Dense = false }, new Path { Reward = RewardKind.Item, Dense = true } };
        }

        // 매 프레임 (일시정지가 아닐 때): 조작 안내가 끝났는지 · 상황별 첫 안내
        void TickTutorial()
        {
            if (!TutorialEnabled) return;
            if (_hintNoticeTimer > 0f)
            {
                _hintNoticeTimer -= Time.unscaledDeltaTime;
                if (_hintNoticeTimer <= 0f) HintNotice = null;
            }
            if (HintNotice == null && _hintQueue.Count > 0)
            {
                HintNotice = _hintQueue.Dequeue();
                _hintNoticeTimer = GameConfig.GetOptional("tutorial.notice_time", 5f);
            }

            if (TickDodge()) return;
            if (_meridian == null) _meridian = FindFirstObjectByType<MeridianScreen>();
            if (_meridian != null && _meridian.IsOpen) _meridianOpenedOnce = true;
            if (_hintDone != null && _hintDone()) NextHint();

            // 상황별 첫 안내 (2단계)
            var items = Player.Items;
            if (items != null)
                foreach (var item in items.Items)
                {
                    if (!item.OnBoard) continue;
                    var state = PlayerItems.StateOf(item);
                    if (state == QiState.Lacking) Notify("qi_lacking", "기 부족 (회색): 단전 가까이 옮기면 깨어나요");
                    else if (state == QiState.Deviated) Notify("qi_deviated", "주화입마 (붉은색): 강해지지만 부작용이 생겨요");
                }
            if (Player.Level != null && Player.Level.Pending > 0) Notify("level_up", "개혈 대기: 싸움이 끝나면 경맥도(E)에서 혈자리를 열어요");
            if (Tracking >= 3) Notify("tracking", "추적도: 방을 비울수록 올라 마교 살수가 쫓아와요 · 객잔에 들르면 절반");
            if (Player.UltimateReady && _phase != Phase.Village) Notify("ultimate", $"기세가 가득 찼어요 · {KeyName(Bind.Ultimate)} 절초");
        }

        // ── 회피 배우기 (입문 2번 방): 졸개가 처음 공격하려는 순간 게임을 멈추고, Space를 누르면 다시 움직이며 대시가 나가요.
        bool _dodgeArmed;
        public bool DodgeFrozen { get; private set; }

        // 멈춰 있으면 true
        bool TickDodge()
        {
            if (DodgeFrozen)
            {
                var k = UnityEngine.InputSystem.Keyboard.current;
                bool dash = AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Dash); // 대시 키 (설정에서 바꾼 키)
                if (!dash) return true;
                ResumeFromDodge(); // 대시 입력은 플레이어가 같은 프레임에 받아요 (시간이 멈춰 있어도 읽혀요)
                return false;
            }
            if (_dodgeArmed && (!InTutorialFloor || _room == null || _room.Cell.x != 1)) _dodgeArmed = false; // 회피 안내는 입문 2번 방에서만
            if (!_dodgeArmed || !_fighting || _room == null || Player.IsDead) return false;
            // 절초·초식·대시 중이거나 무적이면 멈추지 않아요 (어차피 맞지 않아요)
            if (Player.IsCastingUltimate || Player.UltimateRaining || Player.IsCastingSkill || Player.IsDashing || Player.IsInvulnerable) return false;
            float reach = GameConfig.GetOptional("tutorial.dodge_distance", 4f);
            foreach (var e in director.Enemies)
            {
                if (e == null || e.IsDead || e.AttackPhase != EnemyAttackTimeline.Phase.Windup) continue;
                if (Flat(e.transform.position - Player.transform.position).magnitude > reach) continue;
                FreezeForDodge();
                return true;
            }
            return false;
        }

        public void FreezeForDodge()
        {
            _dodgeArmed = false;
            DodgeFrozen = true;
            Time.timeScale = 0f;
            SetHint($"{KeyName(Bind.Dash)} 대시로 피해요 · 대시하는 동안은 맞지 않아요", null);
        }

        public void ResumeFromDodge()
        {
            if (!DodgeFrozen) return;
            DodgeFrozen = false;
            Time.timeScale = 1f;
            SetHint($"{KeyName(Bind.Attack)} 공격 · 누르고 있으면 이어 베요. 3타째가 가장 강해요", () => !_fighting);
        }

        void NextHint()
        {
            _hintDone = null;
            Hint = null;
            bool inStart = InTutorialFloor && _room == _map.Start; // 이동·대시 안내는 첫 방에 있을 때만 이어져요
            if (inStart && _tutorialStep == 1)
            {
                _tutorialStep = 3;
                SetHint("동쪽 문으로 나가요", () => _room != _map.Start);
            }
            else if (InTutorialFloor && _tutorialStep == 5)
            {
                _tutorialStep = 6;
                SetHint("단전에서 멀수록 칸의 기가 줄어요 · 기물은 기 구간 안에 있어야 힘을 써요 (운기)", () => _meridian != null && !_meridian.IsOpen);
            }
        }

        void SetHint(string text, Func<bool> done)
        {
            Hint = text;
            _hintDone = done;
        }

        void ClearHint()
        {
            Hint = null;
            _hintDone = null;
        }

        // 상황별 첫 안내: 처음 한 번만 (회귀해도 기억해요)
        public void Notify(string id, string text)
        {
            if (!TutorialEnabled) return;
            _seenHints ??= new HashSet<string>(MetaSave.Load().seenHints); // 매 프레임 파일을 읽지 않게
            if (!_seenHints.Add(id)) return;
            var meta = MetaSave.Load();
            if (!meta.seenHints.Contains(id)) meta.seenHints.Add(id);
            meta.Save();
            _hintQueue.Enqueue(text);
        }

        HashSet<string> _seenHints;

        // 설정 "튜토리얼 다시 보기"
        public static void ResetTutorial()
        {
            var meta = MetaSave.Load();
            meta.seenHints.Clear();
            meta.tutorialFloorDone = false;
            meta.Save();
        }
    }
}
