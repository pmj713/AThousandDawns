using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AThousandDawns.Prototype
{
    // 이벤트 방 (기획 대화 이벤트 10종 중 죽림 첫 버전 6개). 층마다 0~1개 (은자 시트 등장 확률 50%), 비전투 막다른 방.
    // 같은 판에서 같은 이벤트는 한 번만. 모든 이벤트에 대가 없는 선택지가 있어요.
    // 조건부 선택지(🔒)는 조건이 안 되면 흐리게 조건만 보여 줘요. 그림은 아직 없고 글만 있어요.
    public sealed partial class RoomRun
    {
        public sealed class EventChoice
        {
            public string Label;
            public string Locked;          // 못 고르는 이유 (고를 수 있으면 null)
            public Func<string> Act;       // 결과 글 (null이면 창을 닫고 다른 일로 넘어감)
        }

        public sealed class EventView
        {
            public string Id, Title, Text, Result;
            public List<EventChoice> Choices = new List<EventChoice>();
        }

        public static readonly string[] EventIds = { "doctor", "pharmacy", "gambler", "captive", "shrine", "scripture" };

        readonly HashSet<string> _usedEvents = new HashSet<string>();
        int _floorPower;           // 원기단: 이번 층 동안 공력 +1
        float _floorTaken;         // 산신의 노여움: 이번 층 동안 받는 피해 +15%
        bool _rescueFight;         // 붙잡힌 무인 구출 전투 중 (이기면 희귀 기물)

        public EventView CurrentEvent { get; private set; }
        public bool IsInEvent => CurrentEvent != null;

        // 이번 층에 이벤트 방을 둘지 (아직 안 나온 이벤트가 남아 있을 때만)
        int RollEventRooms() =>
            _usedEvents.Count < EventIds.Length && _rng.NextDouble() < GameConfig.Get("event.chance", 0.5f) ? 1 : 0;

        void AssignEvents()
        {
            foreach (var room in _map.Rooms)
            {
                if (room.Kind != FloorRoomKind.Event) continue;
                var left = new List<string>();
                foreach (var id in EventIds) if (!_usedEvents.Contains(id)) left.Add(id);
                room.EventId = left[_rng.Next(left.Count)];
                _usedEvents.Add(room.EventId);
            }
        }

        // 층을 떠날 때: 이번 층 효과를 되돌려요
        void EndFloorEffects()
        {
            if (_floorPower != 0) Player.Gongryeok -= _floorPower;
            Player.RunTakenIncrease -= _floorTaken;
            _floorPower = 0;
            _floorTaken = 0f;
        }

        // 테스트용: 이번 방을 그 이벤트로 열어요
        public void OpenEventForTest(string id)
        {
            if (_room != null) _room.EventId = id;
            OpenEvent(_room, id);
        }

        Action _afterEvent; // 창을 닫은 뒤 (고수 선택 → 다음 갈림길)

        void OpenEvent(FloorRoom room, string id) => OpenEventView(BuildEvent(room, id), null);

        void OpenEventView(EventView view, Action after)
        {
            CurrentEvent = view;
            _afterEvent = after;
            if (CurrentEvent == null) return;
            arena.Blocked = true;
            Player.enabled = false;
            Time.timeScale = 0f;
        }

        public void CloseEvent()
        {
            if (!IsInEvent) return;
            CurrentEvent = null;
            arena.Blocked = false;
            Player.enabled = true;
            Time.timeScale = 1f;
            var after = _afterEvent;
            _afterEvent = null;
            after?.Invoke();
        }

        // 결과가 나온 뒤엔 아무 숫자·F·Enter로 닫아요
        void TickEventKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (CurrentEvent.Result != null)
            {
                if (keyboard.fKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame) CloseEvent();
                return;
            }
            for (int i = 0; i < CurrentEvent.Choices.Count && i < 9; i++)
                if (((KeyControl)keyboard[(Key)((int)Key.Digit1 + i)]).wasPressedThisFrame)
                {
                    ChooseEvent(i);
                    return;
                }
        }

        public bool ChooseEvent(int index)
        {
            if (!IsInEvent || CurrentEvent.Result != null) return false;
            if (index < 0 || index >= CurrentEvent.Choices.Count) return false;
            var choice = CurrentEvent.Choices[index];
            if (choice.Locked != null) return false;
            var view = CurrentEvent;
            string result = choice.Act();
            if (CurrentEvent != view) return true; // 다음 단계 (기물 고르기 등)로 바뀌었거나 닫혔어요
            if (result == null)
            {
                CloseEvent();
                return true;
            }
            view.Result = result;
            if (_room != null) _room.Cleared = true;
            return true;
        }

        // ── 이벤트 6개

        EventView BuildEvent(FloorRoom room, string id)
        {
            var v = new EventView { Id = id };
            int stake = Price("price.gamble_stake", 50f);
            switch (id)
            {
                case "doctor":
                    v.Title = "떠돌이 의원";
                    v.Text = "약상자를 멘 노인이 길가에 앉아 있다.";
                    int potionPrice = Price("price.doctor_potion", 50f), needlePrice = Price("price.doctor_needle", 40f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"금창약을 산다 · 은자 {potionPrice}",
                        Locked = Silver < potionPrice ? "은자가 모자라요" : Player.Potions >= Player.PotionMax ? $"금창약은 {Player.PotionMax}개까지" : null,
                        Act = () => { Silver -= potionPrice; Player.AddPotion(); return "금창약 1개를 샀어요."; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"침을 맞는다 · 은자 {needlePrice} · 체력 {GameConfig.Get("event.needle_heal", 0.25f):0%} 회복",
                        Locked = Silver < needlePrice ? "은자가 모자라요" : null,
                        Act = () => { Silver -= needlePrice; Player.Heal(Player.MaxHealth * GameConfig.Get("event.needle_heal", 0.25f)); return "침을 맞고 기혈이 돌아요."; },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                case "pharmacy":
                    v.Title = "버려진 약방";
                    v.Text = "먼지 쌓인 약장에 이름표가 떨어진 약병들이 있다.";
                    v.Choices.Add(new EventChoice { Label = "아무 병이나 집는다 · 금창약 / 원기단 (이번 층 공력 +1) / 독약", Act = RandomBottle });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"샅샅이 뒤진다 · 추적도 +{Price("event.search_tracking", 1f)} · 금창약 1개",
                        Act = () =>
                        {
                            AddTracking(Price("event.search_tracking", 1f));
                            return Player.AddPotion() ? "금창약을 찾았어요." : "금창약을 찾았지만 더 들 수 없어 두고 왔어요.";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                case "gambler":
                    v.Title = "투전판 도박꾼";
                    v.Text = "주막 구석에서 사내가 투전 패를 섞고 있다.";
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"은자를 건다 · 은자 {stake} (절반 확률로 두 배)",
                        Locked = Silver < stake ? "은자가 모자라요" : null,
                        Act = () => _rng.NextDouble() < GameConfig.Get("event.gamble_win", 0.5f)
                            ? Win(stake) : Lose(stake),
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = "기물을 건다 · 이기면 같은 등급 기물 하나 더, 지면 잃음",
                        Locked = Player.Items == null || Player.Items.Items.Count == 0 ? "건 기물이 없어요" : null,
                        Act = () => { CurrentEvent = ItemBet(); return null; },
                    });
                    int power = Price("event.gamble_power", 4f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"🔒 속임수를 꿰뚫는다 (공력 {power} 이상) · 은자 {stake}을 걸고 확실히 이김",
                        Locked = Player.Gongryeok < power ? $"공력 {power} 이상 (지금 {Player.Gongryeok})" : Silver < stake ? "은자가 모자라요" : null,
                        Act = () => Win(stake),
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                case "captive":
                    v.Title = "붙잡힌 무인";
                    v.Text = "마교도들이 젊은 무인을 묶어 끌고 가고 있다.";
                    v.Choices.Add(new EventChoice
                    {
                        Label = "구해 준다 · 전투 (정예 1 + 졸개), 이기면 희귀 기물",
                        Act = () => { CloseEvent(); StartEventFight(); return null; },
                    });
                    int need = Price("event.rescue_geumgang", 4f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"🔒 기세로 물리친다 (#금강 {need}단계 이상) · 전투 없이 희귀 기물",
                        Locked = Player.Keywords == null || Player.Keywords.Count(Keyword.Geumgang) < need ? $"#금강 {need}단계 이상" : null,
                        Act = () => { arena.GrantOffer(RareOnlyWeights()); return "마교도들이 기세에 눌려 달아났어요. 무인이 답례를 해요."; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"못 본 척한다 · 추적도 −{Price("event.ignore_tracking", 1f)}",
                        Act = () => { AddTracking(-Price("event.ignore_tracking", 1f)); return "조용히 지나갔어요."; },
                    });
                    break;

                case "shrine":
                    v.Title = "버려진 산신당";
                    v.Text = "무너진 사당에 향로와 공물이 그대로 남아 있다.";
                    int incense = Price("price.shrine_incense", 20f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"향을 피운다 · 은자 {incense} · 이번 지역 동안 무작위 축복",
                        Locked = Silver < incense ? "은자가 모자라요" : null,
                        Act = () =>
                        {
                            Silver -= incense;
                            if (_rng.Next(2) == 0)
                            {
                                Player.RunMoveBonus += GameConfig.Get("event.shrine_move", 0.1f);
                                return $"산신의 축복: 이동속도 +{GameConfig.Get("event.shrine_move", 0.1f):0%}";
                            }
                            Player.RunGaugeBonus += GameConfig.Get("event.shrine_gauge", 0.15f);
                            return $"산신의 축복: 기세 충전 +{GameConfig.Get("event.shrine_gauge", 0.15f):0%}";
                        },
                    });
                    int offering = Price("event.offering_silver", 80f);
                    float wrath = GameConfig.Get("event.offering_taken", 0.15f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"공물을 챙긴다 · 은자 +{offering}, 이번 층 받는 피해 +{wrath:0%}",
                        Act = () =>
                        {
                            Silver += offering;
                            _floorTaken += wrath;
                            Player.RunTakenIncrease += wrath;
                            return $"은자 +{offering}. 산신의 노여움이 느껴져요 (이번 층 받는 피해 +{wrath:0%}).";
                        },
                    });
                    v.Choices.Add(Pass("절만 하고 간다"));
                    break;

                case "scripture":
                    v.Title = "벽에 새긴 검결";
                    v.Text = "무너진 수련 동굴 벽에 누군가 검술 요결을 새겨 두었다.";
                    int xp = Price("event.scripture_xp", 15f), deep = Price("event.scripture_tracking", 2f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"훑어본다 · 수련치 +{xp}",
                        Act = () => { Player.Level.Add(xp); return $"수련치 +{xp}"; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"깊이 참오한다 · 추적도 +{deep}, 혈자리 하나를 바로 연다",
                        Act = () =>
                        {
                            AddTracking(deep);
                            Player.Level.GrantPending();
                            return "깨달음을 얻었어요. 경맥도(E)에서 혈자리를 열 수 있어요.";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                default:
                    return null;
            }
            return v;
        }

        static EventChoice Pass(string label) => new EventChoice { Label = label, Act = () => "아무 일도 없었어요." };

        static int Price(string key, float fallback) => Mathf.RoundToInt(GameConfig.Get(key, fallback));

        string Win(int stake)
        {
            Silver += stake;
            return $"이겼어요! 은자 +{stake}";
        }

        string Lose(int stake)
        {
            Silver -= stake;
            return $"졌어요. 은자 −{stake}";
        }

        // 아무 병: 60% 금창약 / 25% 원기단 (이번 층 동안 공력 +1) / 15% 독약 (체력 −15%)
        string RandomBottle()
        {
            double r = _rng.NextDouble();
            float potion = GameConfig.Get("event.bottle_potion", 0.6f), pill = GameConfig.Get("event.bottle_pill", 0.25f);
            if (r < potion) return Player.AddPotion() ? "금창약이었어요." : "금창약이었지만 더 들 수 없어 두고 왔어요.";
            if (r < potion + pill)
            {
                _floorPower++;
                Player.Gongryeok += 1;
                return "원기단이었어요. 이번 층 동안 공력 +1";
            }
            float poison = Player.MaxHealth * GameConfig.Get("event.poison_damage", 0.15f);
            Player.TakeEventDamage(poison);
            return $"독약이었어요. 체력 −{poison:0}";
        }

        // 기물 걸기: 걸 기물 고르기
        EventView ItemBet()
        {
            var v = new EventView { Id = "gambler", Title = "투전판 도박꾼", Text = "어느 기물을 걸까?" };
            int shown = 0;
            foreach (var owned in Player.Items.Items)
            {
                if (shown++ >= 8) break;
                var item = owned;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name} · {ItemDefinition.GradeLabel(item.Def.grade)}",
                    Act = () => BetItem(item),
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => "판에서 일어났어요." });
            return v;
        }

        string BetItem(OwnedItem item)
        {
            if (_rng.NextDouble() >= GameConfig.Get("event.gamble_win", 0.5f))
            {
                Player.Items.Remove(item);
                return $"졌어요. {item.Def.name}을(를) 잃었어요.";
            }
            var db = GameConfig.Items;
            var same = new List<ItemDefinition>();
            if (db != null)
                foreach (var def in db.Items)
                    if (def != null && def.grade == item.Def.grade && !Player.Items.Has(def.id)) same.Add(def);
            if (same.Count == 0) return "이겼지만 같은 등급 기물이 남아 있지 않아요.";
            var won = same[_rng.Next(same.Count)];
            if (Player.Items.Add(won) == null && Player.Items.Pending == won)
                return $"이겼어요! {won.name} · 둘 곳이 없어 하나를 버려야 받아요.";
            return $"이겼어요! {won.name}을(를) 얻었어요.";
        }

        // 붙잡힌 무인 구출: 정예 졸개 1 + 졸개 둘, 이기면 희귀 기물 (ClearRoom에서)
        void StartEventFight()
        {
            _rescueFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            int grunts = Price("event.rescue_grunts", 2f);
            var wave = new List<EnemyKind>();
            for (int i = 0; i < grunts; i++) wave.Add(EnemyKind.Grunt);
            _waves = new List<List<EnemyKind>> { wave };
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = 0;
            _eliteKind = EnemyKind.Grunt;
            var traits = new List<EliteTrait>();
            foreach (EliteTrait t in Enum.GetValues(typeof(EliteTrait)))
                if (EnemyBrain.Allows(EnemyKind.Grunt, t)) traits.Add(t);
            _eliteTrait = traits[_rng.Next(traits.Count)];
            StartNextWave();
        }

        static float[] RareOnlyWeights() => new[] { 0f, 0f, 1f, 0f }; // "희귀 기물"로 정해진 보상 (등급표 안 씀)
    }
}
