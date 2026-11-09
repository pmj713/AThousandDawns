using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판 중간 저장 (RunSave): 방에 들어가거나 방을 비우거나 갈림길·객잔·정비실에 오면 "저장할 일"이 생기고,
    // 싸움·기물 고르기·이벤트·진열대가 모두 끝난 안전한 순간에 저장해요. 이어하기는 마지막 저장 시점부터.
    // 저장하지 않는 것: 살수 (층을 다시 들어갈 때 추적도로 새로 정해짐), 보호막, 쿨타임, 입문 죽림 (첫 판 1층).
    public sealed partial class RoomRun
    {
        // 시작 화면 "이어하기"에서 켜요 (씬을 부른 뒤 Start에서 한 번 씀)
        public static bool ResumeRun;

        bool _saveDirty;
        public bool HasUnsavedProgress => _saveDirty;

        void MarkSave() => _saveDirty = true;

        // Update 끝에서: 안전한 순간이면 저장
        void TickAutosave()
        {
            if (!_saveDirty || !CanSave()) return;
            _saveDirty = false;
            Capture().Save();
        }

        bool CanSave(bool ignorePause = false)
        {
            if (Player == null || Player.IsDead || IsShowingResult || InTutorialFloor || (IsPaused && !ignorePause) || DodgeFrozen) return false;
            if (OffersOpen || IsInEvent || IsShopping || (Player.Items != null && Player.Items.Pending != null)) return false;
            if (_meridian != null && _meridian.IsOpen) return false;
            if (IsForking) return true; // 갈림길 (층 사이)
            return _phase switch
            {
                Phase.Floor => !_fighting && _room != null && !AssassinIncoming,
                Phase.Inn => true,
                Phase.Rest => true,
                _ => false,
            };
        }

        public RunSave Capture()
        {
            var s = new RunSave
            {
                phase = (int)(IsForking ? Phase.Fork : _phase), // 갈림길은 층 사이라 따로 표시
                floor = _floor,
                pathReward = (int)_path.Reward,
                pathDense = _path.Dense,
                masterDone = _masterDone,
                tracking = Tracking,
                silver = Silver,
                floorPower = _floorPower,
                floorTaken = _floorTaken,
                bossGauge = _bossGauge,
                floorsCleared = _record.FloorsCleared,
                hiddenRooms = _record.HiddenRooms,
                deja = _record.Deja,
                kills = (int[])_record.Kills.Clone(),
                health = Player.Health,
                gauge = Player.Gauge,
                potions = Player.Potions,
                gongryeok = Player.Gongryeok,
                skill = (int)Player.Skill,
                revive = Player.ReviveReady,
                moveBonus = Player.RunMoveBonus,
                gaugeBonus = Player.RunGaugeBonus,
                takenIncrease = Player.RunTakenIncrease,
            };
            s.usedEvents.AddRange(_usedEvents);
            if (_forkOptions != null)
                foreach (var p in _forkOptions) s.forkOptions.Add(new RunSave.Fork { reward = (int)p.Reward, dense = p.Dense });
            if (_map != null && _phase == Phase.Floor)
            {
                foreach (var r in _map.Rooms)
                    s.rooms.Add(new RunSave.Room
                    {
                        x = r.Cell.x, y = r.Cell.y, kind = (int)r.Kind, optional = r.Optional, visited = r.Visited, revealed = r.Revealed,
                        cleared = r.Cleared, found = r.Found, eventId = r.EventId, jars = r.Jars,
                    });
                if (_room != null) { s.roomX = _room.Cell.x; s.roomY = _room.Cell.y; }
            }
            if (Shop != null && _phase == Phase.Inn)
            {
                s.hasShop = true;
                foreach (var slot in Shop.Slots)
                    s.shop.Add(new RunSave.ShopSlot { kind = (int)slot.Kind, itemId = slot.Item != null ? slot.Item.id : null, part = (int)slot.Part, skill = (int)slot.Skill, price = slot.Price, sold = slot.Sold });
            }
            if (Player.Level != null) { s.level = Player.Level.Level; s.xp = Player.Level.Xp; s.pending = Player.Level.Pending; }

            var items = Player.Items;
            if (items != null)
            {
                var board = items.Board;
                s.boardWidth = board.Width;
                s.boardHeight = board.Height;
                foreach (var d in board.Danjeons) s.danjeons.Add(new RunSave.Point { x = d.x, y = d.y });
                foreach (var kv in board.Acupoints) s.acupoints.Add(new RunSave.Point { x = kv.Key.x, y = kv.Key.y, kind = (int)kv.Value });
                foreach (var it in items.Items) s.items.Add(new RunSave.Item { id = it.Def.id, onBoard = it.OnBoard, x = it.Cell.x, y = it.Cell.y });
                foreach (var p in items.Parts) s.parts.Add(new RunSave.Part { kind = (int)p.Kind, onBoard = p.OnBoard, x = p.Cell.x, y = p.Cell.y });
                s.forge0 = items.ForgeSlot(0).HasValue ? (int)items.ForgeSlot(0).Value : -1;
                s.forge1 = items.ForgeSlot(1).HasValue ? (int)items.ForgeSlot(1).Value : -1;
                s.globalMaxQi = items.GlobalMaxQiBonus;
                s.danjeonMoves = items.DanjeonMoves;
                s.startDanjeons = items.StartDanjeons;
            }
            return s;
        }

        // Start에서 (회귀록·면벽수련을 적용한 뒤): 저장한 판을 되살려요. 못 읽으면 false
        bool RestoreRun()
        {
            var s = RunSave.Load();
            if (s == null) return false;
            var db = GameConfig.Items;

            // 경맥도 먼저 (최대 체력·키워드가 정해지게)
            var items = Player.Items;
            if (items != null)
            {
                if (s.boardWidth > items.Board.Width || s.boardHeight > items.Board.Height) items.ExpandBoard(s.boardWidth, s.boardHeight);
                foreach (var d in new List<Vector2Int>(items.Board.Danjeons)) items.RemoveDanjeon(d);
                foreach (var d in s.danjeons) items.AddDanjeon(new Vector2Int(d.x, d.y));
                foreach (var a in s.acupoints) items.OpenAcupoint(new Vector2Int(a.x, a.y), (Acupoint)a.kind);
                foreach (var p in s.parts)
                {
                    var part = items.AddPart((PartKind)p.kind);
                    if (p.onBoard) items.PlacePart(part, new Vector2Int(p.x, p.y));
                }
                foreach (var it in s.items)
                {
                    var def = db != null ? FindItem(db, it.id) : null;
                    if (def != null) items.Restore(def, it.onBoard, new Vector2Int(it.x, it.y));
                }
                items.SetForgeSlot(0, s.forge0 >= 0 ? (Keyword?)s.forge0 : null);
                items.SetForgeSlot(1, s.forge1 >= 0 ? (Keyword?)s.forge1 : null);
                items.SetGlobalMaxQiBonus(s.globalMaxQi);
                items.DanjeonMoves = s.danjeonMoves;
                items.StartDanjeons = s.startDanjeons;
            }
            Player.Gongryeok = s.gongryeok;
            Player.LearnSkill((SkillKind)s.skill);
            Player.RunMoveBonus = s.moveBonus;
            Player.RunGaugeBonus = s.gaugeBonus;
            Player.RunTakenIncrease = s.takenIncrease;
            if (Player.Level != null) Player.Level.Restore(s.level, s.xp, s.pending);
            Player.RestoreRunState(s.health, s.potions, s.gauge, s.revive);

            // 판
            Silver = s.silver;
            Tracking = s.tracking;
            _floor = s.floor;
            _path = new Path { Reward = (RewardKind)s.pathReward, Dense = s.pathDense };
            _masterDone = s.masterDone;
            _floorPower = s.floorPower;
            _floorTaken = s.floorTaken;
            _bossGauge = s.bossGauge;
            _usedEvents.Clear();
            foreach (var e in s.usedEvents) _usedEvents.Add(e);
            _record.FloorsCleared = s.floorsCleared;
            _record.HiddenRooms = s.hiddenRooms;
            _record.Deja = s.deja;
            for (int i = 0; i < _record.Kills.Length && i < s.kills.Length; i++) _record.Kills[i] = s.kills[i];

            switch ((Phase)s.phase)
            {
                case Phase.Fork:
                    _forkOptions = new List<Path>();
                    foreach (var f in s.forkOptions) _forkOptions.Add(new Path { Reward = (RewardKind)f.reward, Dense = f.dense });
                    _phase = Phase.Fork;
                    arena.Blocked = true;
                    Player.enabled = false;
                    Time.timeScale = 0f;
                    ShowBanner($"이어하기 · 죽림 {_floor}층 갈림길");
                    break;
                case Phase.Inn:
                    EnterInnSite(s.hasShop ? RestoreShop(s, db) : null);
                    ShowBanner("이어하기 · 객잔");
                    break;
                case Phase.Rest:
                    EnterRest();
                    ShowBanner("이어하기 · 정비실");
                    break;
                default:
                    var map = FloorMap.Line();
                    foreach (var r in s.rooms)
                    {
                        var room = map.AddRestored(new Vector2Int(r.x, r.y), (FloorRoomKind)r.kind, r.optional);
                        room.Visited = r.visited;
                        room.Revealed = r.revealed;
                        room.Cleared = r.cleared;
                        room.Found = r.found;
                        room.EventId = r.eventId;
                        room.Jars = r.jars;
                    }
                    _map = map;
                    arena.Blocked = false;
                    Player.enabled = true;
                    Time.timeScale = 1f;
                    var at = _map.At(new Vector2Int(s.roomX, s.roomY)) ?? _map.Start;
                    Enter(at, -1);
                    ShowBanner($"이어하기 · 죽림 {_floor}층");
                    break;
            }
            _saveDirty = false;
            return true;
        }

        static ItemDefinition FindItem(ItemDatabase db, string id)
        {
            foreach (var d in db.Items) if (d != null && d.id == id) return d;
            return null;
        }

        static InnShop RestoreShop(RunSave s, ItemDatabase db)
        {
            var slots = new List<InnShop.Slot>();
            foreach (var x in s.shop)
                slots.Add(new InnShop.Slot
                {
                    Kind = (InnShop.SlotKind)x.kind,
                    Item = x.itemId != null && db != null ? FindItem(db, x.itemId) : null,
                    Part = (PartKind)x.part,
                    Skill = (SkillKind)x.skill,
                    Price = x.price,
                    Sold = x.sold,
                });
            return InnShop.FromSlots(slots);
        }
    }
}
