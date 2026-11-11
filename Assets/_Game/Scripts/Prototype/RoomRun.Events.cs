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
    // 이벤트 방 (기획 대화 이벤트 10종 모두: 첫 버전 6개 + 서툰 대장장이 · 표국 행렬 · 검이 꽂힌 무덤 · 기시감 (회귀 3회 이상) + 폐사찰 전용 3개 (RoomRun.TempleEvents.cs)). 층마다 0~1개 (은자 시트 등장 확률 50%), 비전투 막다른 방.
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

        public static readonly string[] EventIds = { "doctor", "pharmacy", "gambler", "captive", "shrine", "scripture", "escort", "smith", "tomb", "deja", "peddler" };

        readonly HashSet<string> _usedEvents = new HashSet<string>();
        int _floorPower;           // 원기단: 이번 층 동안 공력 +1
        float _floorTaken;         // 산신의 노여움: 이번 층 동안 받는 피해 +15%
        bool _rescueFight;         // 붙잡힌 무인 구출 전투 중 (이기면 희귀 기물)
        bool _escortFight;         // 표국 호위 전투 중 (웨이브 3개를 버티면 은자)

        public EventView CurrentEvent { get; private set; }
        public bool IsInEvent => CurrentEvent != null;

        // 이번 층에 이벤트 방을 둘지 (아직 안 나온 이벤트가 남아 있을 때만)
        int RollEventRooms() =>
            UnusedEvents().Count > 0 && _rng.NextDouble() < GameConfig.Get("event.chance", 0.5f) ? 1 : 0;

        void AssignEvents()
        {
            foreach (var room in _map.Rooms)
            {
                if (room.Kind != FloorRoomKind.Event) continue;
                var left = UnusedEvents();
                if (left.Count == 0) continue;
                room.EventId = left[_rng.Next(left.Count)];
                _usedEvents.Add(room.EventId);
            }
        }

        // 이 지역에서 나올 수 있고 아직 안 나온 이벤트 (폐사찰에서만 사찰 이벤트 3개가 더해져요)
        List<string> UnusedEvents()
        {
            var left = new List<string>();
            foreach (var id in EventIds) if (!_usedEvents.Contains(id)) left.Add(id);
            if (_region == 1) foreach (var id in TempleEventIds) if (!_usedEvents.Contains(id)) left.Add(id);
            return left;
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

        void OpenEvent(FloorRoom room, string id)
        {
            Notify("event", "이벤트 방: 고르는 대로 결과가 달라요 · 대가 없는 선택지도 늘 있어요");
            OpenEventView(BuildEvent(room, id), null);
        }

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
            MarkSave();
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
                if (AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Interact) || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame) CloseEvent();
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

                case "escort":
                    v.Title = "표국 행렬";
                    v.Text = "짐수레를 끄는 표국 일행이 산적의 습격을 걱정하고 있다.";
                    int pay = Price("event.escort_silver", 150f);
                    int loot = Price("event.rob_silver", 100f);
                    int rumor = Price("event.rob_tracking", 3f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"호위한다 · 습격 3번을 막아 내면 은자 +{pay}",
                        Act = () => { CloseEvent(); StartEscortFight(); return null; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"짐을 턴다 · 은자 +{loot}, 추적도 +{rumor} (소문이 퍼짐)",
                        Act = () =>
                        {
                            Silver += loot;
                            AddTracking(rumor);
                            return $"표사들이 달아났어요. 은자 +{loot} · 소문이 퍼져 추적도 {Tracking}";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                case "peddler":
                    BuildPeddler(v); // RoomRun.Sell.cs
                    break;

                case "smith":
                    v.Title = "서툰 대장장이";
                    v.Text = "대장간 견습생이 실력을 시험해 보고 싶어 한다.";
                    int fee = Price("price.smith_fee", 60f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"기물을 맡긴다 · 삯 {fee} · 반반: 등급 한 단계 위 / 망가져 사라짐",
                        Locked = Silver < fee ? "은자가 모자라요" : SmithCandidates().Count == 0 ? "맡길 기물이 없어요 (전설은 더 못 올려요)" : null,
                        Act = () => { OpenSmithPick(fee); return null; },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    break;

                case "tomb":
                    v.Title = "검이 꽂힌 무덤";
                    v.Text = "이름 없는 무덤 위에 녹슨 검 한 자루가 꽂혀 있다.";
                    int bowXp = Price("event.tomb_xp", 10f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = "검을 뽑는다 · 무덤 주인의 망령(정예)과 전투, 이기면 전설이 잘 나오는 기물 고르기",
                        Act = () => { CloseEvent(); StartTombFight(); return null; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"절하고 간다 · 수련치 +{bowXp}",
                        Act = () => { Player.Level.Add(bowXp); return $"고개를 숙이자 검결 한 줄이 떠올라요. 수련치 +{bowXp}"; },
                    });
                    break;

                case "deja":
                    v.Title = "기시감";
                    v.Text = "처음 보는 노인이 말을 건다. \"또 왔구먼. 이번이 몇 번째인가?\"";
                    int dejaMemory = Price("memory.deja", 5f);
                    int bossGauge = Mathf.RoundToInt(GameConfig.Get("event.deja_boss_gauge", 0.5f) * 100f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"전생의 이야기를 듣는다 · 회귀의 기억 +{dejaMemory}, 이야기 조각 하나",
                        Act = () =>
                        {
                            _record.Deja++;
                            return $"{NextStoryFragment()}\n\n(판이 끝나면 회귀의 기억 +{dejaMemory})";
                        },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"천마에 대해 묻는다 · 이번 판 마지막 보스전을 기세 {bossGauge}%로 시작",
                        Act = () =>
                        {
                            _bossGauge = Mathf.Max(_bossGauge, bossGauge / 100f);
                            return "\"그자의 검은 첫수가 가장 무겁지. 먼저 기세를 쥐고 들어가게.\"";
                        },
                    });
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
                    if (!BuildTempleEvent(v, id)) return null; // RoomRun.TempleEvents.cs
                    break;
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
                foreach (var def in db.Available)
                    if (def != null && def.grade == item.Def.grade && !Player.Items.Has(def.id)) same.Add(def);
            if (same.Count == 0) return "이겼지만 같은 등급 기물이 남아 있지 않아요.";
            var won = same[_rng.Next(same.Count)];
            if (Player.Items.Add(won) == null && Player.Items.Pending == won)
                return $"이겼어요! {won.name} · 둘 곳이 없어 하나를 버려야 받아요.";
            return $"이겼어요! {won.name}을(를) 얻었어요.";
        }

        // ── 기시감 (회귀 3회 이상부터): 노인이 이야기를 조금씩 풀어 줘요. 들은 조각 수는 회귀해도 남아요 (MetaSave).
        // 이야기는 서사 초안이에요 (나중에 시나리오가 정해지면 바꿔요).
        float _bossGauge; // 천마에 대해 묻기: 마지막 보스전 시작 기세 (지금은 녹림왕)
        public float BossGauge => _bossGauge;

        public static readonly string[] StoryFragments =
        {
            "\"자네 눈빛, 처음 보는 게 아니야. 저번엔 대숲에서 쓰러졌지. 그 전엔… 기억이 흐리군.\"",
            "\"천 번의 새벽이라… 누군가 그런 말을 했었지. 끝나지 않는 밤을 끊으려면 같은 새벽을 천 번 맞아야 한다고.\"",
            "\"마교의 그자도 한때는 목검을 든 아이였네. 자네처럼 말이야.\"",
            "\"나도 한때는 돌아오는 자였지. 몇 번째에서 멈췄는지는 묻지 말게.\"",
            "\"다음에 만나면 내 이름을 알려 주지. 자네가 기억한다면 말일세.\"",
        };

        public int StoryRevealed => Mathf.Min(MetaSave.Load().storyFragments, StoryFragments.Length);

        string NextStoryFragment()
        {
            var meta = MetaSave.Load();
            if (meta.storyFragments >= StoryFragments.Length)
                return "노인은 말없이 웃기만 해요. 이미 다 들은 이야기예요.";
            string text = StoryFragments[meta.storyFragments];
            meta.storyFragments++;
            meta.Save();
            return $"{text}\n<size=13>(이야기 조각 {meta.storyFragments}/{StoryFragments.Length})</size>";
        }

        // ── 서툰 대장장이: 등급을 올릴 수 있는 기물 (전설이 아니고, 한 단계 위 기물이 남아 있음)

        List<OwnedItem> SmithCandidates()
        {
            var list = new List<OwnedItem>();
            foreach (var item in Player.Items.Items)
                if (item.Def.grade < ItemGrade.Legendary && UpgradeOf(item.Def, false) != null) list.Add(item);
            return list;
        }

        // 한 단계 위 등급 기물: 같은 주 키워드가 있으면 그중에서, 없으면 아무 키워드나 (아직 안 가진 것)
        ItemDefinition UpgradeOf(ItemDefinition def, bool pick = true)
        {
            var db = GameConfig.Items;
            if (db == null) return null;
            var same = new List<ItemDefinition>();
            var any = new List<ItemDefinition>();
            foreach (var d in db.Available)
            {
                if (d == null || d.grade != def.grade + 1 || Player.Items.Has(d.id)) continue;
                any.Add(d);
                if (d.keyword == def.keyword) same.Add(d);
            }
            var pool = same.Count > 0 ? same : any;
            return pool.Count == 0 ? null : pool[pick ? _rng.Next(pool.Count) : 0]; // 고르지 않으면 있는지만 (난수를 안 써요)
        }

        void OpenSmithPick(int fee)
        {
            var v = new EventView { Id = "smith_pick", Title = "서툰 대장장이", Text = $"어느 기물을 맡길까? (삯 {fee})" };
            int shown = 0;
            foreach (var owned in SmithCandidates())
            {
                if (shown++ >= 8) break;
                var item = owned;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name} · {ItemDefinition.GradeLabel(item.Def.grade)} → {ItemDefinition.GradeLabel(item.Def.grade + 1)}",
                    Act = () => Smith(item, fee),
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => "견습생이 아쉬워해요." });
            OpenEventView(v, null);
        }

        string Smith(OwnedItem item, int fee)
        {
            Silver -= fee;
            if (_rng.NextDouble() >= GameConfig.Get("event.smith_success", 0.5f))
            {
                Player.Items.Remove(item);
                return $"쨍! {item.Def.name}이(가) 두 동강 났어요.";
            }
            var up = UpgradeOf(item.Def);
            if (up == null || Player.Items.Replace(item, up) == null) return $"{item.Def.name}을(를) 두드려 봤지만 그대로예요.";
            return $"성공! {item.Def.name} → {up.name} ({ItemDefinition.GradeLabel(up.grade)})";
        }

        // ── 검이 꽂힌 무덤: 망령 (정예) 하나, 이기면 전설이 잘 나오는 기물 고르기 (ClearRoom에서)
        bool _tombFight;

        void StartTombFight()
        {
            _tombFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            _waves = new List<List<EnemyKind>> { new List<EnemyKind>() };
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

        static float[] TombWeights() => new[]
        {
            GameConfig.Get("event.tomb_grade.common", 0f), GameConfig.Get("event.tomb_grade.uncommon", 0.3f),
            GameConfig.Get("event.tomb_grade.rare", 0.4f), GameConfig.Get("event.tomb_grade.legendary", 0.3f),
        };

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

        // 표국 호위: 그 층 방 예산으로 웨이브 3개, 다 막으면 은자 (ClearRoom에서)
        void StartEscortFight()
        {
            _escortFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            _waves = PlanRoom(_floor, Price("event.escort_waves", 3f));
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = -1;
            StartNextWave();
        }

        static float[] RareOnlyWeights() => new[] { 0f, 0f, 1f, 0f }; // "희귀 기물"로 정해진 보상 (등급표 안 씀)
    }
}
