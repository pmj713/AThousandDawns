using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판타지 이벤트 4종 (차원 겹침): 어느 지역에서나 무협 이벤트 사이에 섞여요. 깊이 갈수록 자주 (fantasy_event.chance.r0~r3).
    //   이계 대장간: 은자를 내면 기물 하나를 같은 키워드의 이계 기물로 바꿔 줘요 (혼종 세트 맞추기).
    //   무너진 성당 제단: 은자를 바치면 축복 (이번 층 받는 피해 −) 아니면 저주 (+). 기도만 하면 체력 조금.
    //   마나 샘: 마시면 체력 회복, 몸에 부으면 혈자리 하나 (둘 중 하나).
    //   봉인된 마검: 뽑으면 이계 정예(오크) + 스켈레톤 둘과 싸워 이기면 희귀 이상 기물 고르기. 다시 봉인하면 수련치.
    // 판타지 적이 꺼져 있으면(RoomBudget.FantasyOn) 나오지 않아요 (예전 테스트의 난수 흐름이 그대로).
    public sealed partial class RoomRun
    {
        public static readonly string[] FantasyEventIds = { "otherworld_smith", "chapel", "mana_spring", "sealed_blade" };
        public static event Action<RoomRun, string, Vector3> FantasyEventFx; // forge · bless · curse · spring · unseal (아트)

        public static int ChapelOverride = -1; // 테스트용 (1 축복 · 0 저주)

        [SerializeField] GameObject otherworldForgeProp;
        [SerializeField] GameObject chapelAltarProp;
        [SerializeField] GameObject manaSpringProp;
        [SerializeField] GameObject sealedBladeProp;

        bool _sealedFight; // 봉인된 마검 전투 중 (이기면 희귀 이상 기물)

        public static bool IsFantasyEvent(string id) => Array.IndexOf(FantasyEventIds, id) >= 0;
        public bool EventPoolHas(string id) => UnusedEvents().Contains(id); // 테스트용

        float FantasyEventChance => GameConfig.GetOptional($"fantasy_event.chance.r{Mathf.Clamp(_region, 0, 3)}", 0.25f + 0.1f * _region);

        // AssignEvents에서: 판타지 이벤트가 남아 있으면 지역 확률로 그쪽에서 골라요
        string PickEventId(List<string> left)
        {
            if (RoomBudget.FantasyOn)
            {
                var fantasy = left.FindAll(IsFantasyEvent);
                var wuxia = left.FindAll(id => !IsFantasyEvent(id));
                if (fantasy.Count > 0 && (wuxia.Count == 0 || _rng.NextDouble() < FantasyEventChance)) return fantasy[_rng.Next(fantasy.Count)];
                if (wuxia.Count > 0) return wuxia[_rng.Next(wuxia.Count)];
            }
            return left[_rng.Next(left.Count)];
        }

        void FxAtPlayer(string kind) => FantasyEventFx?.Invoke(this, kind, Player.transform.position);

        bool BuildFantasyEvent(EventView v, string id)
        {
            switch (id)
            {
                case "otherworld_smith":
                {
                    v.Title = "이계 대장간";
                    v.Text = "차원 틈 너머에서 수염 난 드워프가 망치를 내리친다. \"이쪽 쇠는 무르구먼. 내가 다시 벼려 주지.\"";
                    int fee = Price("price.forge_fee", 80f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"기물을 맡긴다 · 은자 −{fee} · 같은 키워드의 이계 기물로 바뀌어요 (혼종 세트)",
                        Locked = Silver < fee ? "은자가 모자라요" : ForgeCandidates().Count == 0 ? "벼릴 수 있는 기물이 없어요" : null,
                        Act = () => { OpenForgePick(fee); return null; },
                    });
                    v.Choices.Add(Pass("고개를 젓고 지나간다"));
                    return true;
                }

                case "chapel":
                {
                    v.Title = "무너진 성당 제단";
                    v.Text = "무협 땅에 어울리지 않는 뾰족한 아치가 반쯤 무너져 있다. 돌 제단 위 촛불이 바람도 없이 흔들린다.";
                    int offer = Price("price.chapel_offer", 60f);
                    float bless = GameConfig.GetOptional("event.chapel_bless", 0.2f), curse = GameConfig.GetOptional("event.chapel_curse", 0.15f);
                    float chance = GameConfig.GetOptional("event.chapel_bless_chance", 0.65f);
                    float pray = GameConfig.GetOptional("event.chapel_pray_heal", 0.15f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"제물을 바친다 · 은자 −{offer} · {chance:0%}: 축복 (이번 층 받는 피해 −{bless:0%}) / 저주 (+{curse:0%})",
                        Locked = Silver < offer ? "은자가 모자라요" : null,
                        Act = () =>
                        {
                            Silver -= offer;
                            bool blessed = ChapelOverride >= 0 ? ChapelOverride == 1 : _rng.NextDouble() < chance;
                            float delta = blessed ? -bless : curse;
                            _floorTaken += delta;
                            Player.RunTakenIncrease += delta;
                            FxAtPlayer(blessed ? "bless" : "curse");
                            return blessed ? $"금빛이 내려와 몸을 감싸요. 이번 층 받는 피해 −{bless:0%}" : $"촛불이 검붉게 꺼져요. 이번 층 받는 피해 +{curse:0%}";
                        },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"기도만 한다 · 체력 {pray:0%} 회복",
                        Act = () =>
                        {
                            Player.Heal(Player.MaxHealth * pray);
                            FxAtPlayer("bless");
                            return $"낯선 기도문이 입에서 흘러나와요. 체력 {pray:0%} 회복";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    return true;
                }

                case "mana_spring":
                {
                    v.Title = "마나 샘";
                    v.Text = "바위 틈에서 청보라 빛 샘물이 솟는다. 손을 대자 단전이 저릿하다. 한 번밖에 쓸 수 없을 것 같다.";
                    float heal = GameConfig.GetOptional("event.spring_heal", 0.4f);
                    int power = Price("event.spring_power", 1f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"마신다 · 체력 {heal:0%} 회복",
                        Act = () =>
                        {
                            Player.Heal(Player.MaxHealth * heal);
                            FxAtPlayer("spring");
                            return $"차가운 마나가 상처를 메워요. 체력 {heal:0%} 회복";
                        },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = "몸에 붓는다 · 혈자리 하나를 바로 열 수 있어요 (개혈)",
                        Act = () =>
                        {
                            Player.Level.GrantPending(); // 공력 대신 혈자리 (공력이 너무 자주 올라 기물이 넘치던 것)
                            FxAtPlayer("spring");
                            return $"마나가 경맥을 타고 돌아요. 혈자리를 하나 열 수 있어요 ({PlayerItems.ScreenName}에서)";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    return true;
                }

                case "sealed_blade":
                {
                    v.Title = "봉인된 마검";
                    v.Text = "사슬과 룬에 묶인 검은 장검이 바닥에 꽂혀 있다. 검이 낮게 속삭인다. \"뽑아라.\"";
                    int sealXp = Price("event.sealed_xp", 10f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = "검을 뽑는다 · 이계 정예와 스켈레톤이 튀어나와요, 이기면 희귀 이상 기물 고르기",
                        Act = () => { CloseEvent(); StartSealedFight(); return null; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"봉인을 다시 다진다 · 수련치 +{sealXp}",
                        Act = () => { Player.Level.Add(sealXp); return $"룬이 다시 빛나며 검이 잠잠해져요. 수련치 +{sealXp}"; },
                    });
                    return true;
                }
            }
            return false;
        }

        // ── 이계 대장간: 같은 키워드의 이계 기물이 아직 남아 있는 무협 기물만
        List<OwnedItem> ForgeCandidates()
        {
            var list = new List<OwnedItem>();
            if (Player.Items == null) return list;
            foreach (var item in Player.Items.Items)
                if (!item.Def.otherworld && OtherworldFor(item.Def, false) != null) list.Add(item);
            return list;
        }

        ItemDefinition OtherworldFor(ItemDefinition def, bool pick = true)
        {
            var db = GameConfig.Items;
            if (db == null) return null;
            var pool = new List<ItemDefinition>();
            foreach (var d in db.Available)
                if (d != null && d.otherworld && d.keyword == def.keyword && !Player.Items.Has(d.id)) pool.Add(d);
            if (pool.Count == 0) return null;
            if (!pick) return pool[0]; // 있는지만 (난수를 안 써요)
            var sameSlot = pool.FindAll(d => d.slot == def.slot); // 장비 칸 방식이면 같은 부위를 먼저
            return sameSlot.Count > 0 ? sameSlot[_rng.Next(sameSlot.Count)] : pool[_rng.Next(pool.Count)];
        }

        void OpenForgePick(int fee)
        {
            var v = new EventView { Id = "otherworld_smith_pick", Art = "otherworld_smith", Title = "이계 대장간", Text = "무엇을 벼릴까?" };
            int shown = 0;
            foreach (var owned in ForgeCandidates())
            {
                if (shown++ >= 8) break;
                var item = owned;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name} · {ItemDefinition.GradeLabel(item.Def.grade)}",
                    Act = () => Forge(item, fee),
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => "드워프가 어깨를 으쓱해요." });
            OpenEventView(v, null);
        }

        public string Forge(OwnedItem item, int fee)
        {
            if (Silver < fee) return "은자가 모자라요.";
            var other = OtherworldFor(item.Def);
            var next = other != null ? Player.Items.Replace(item, other) : null;
            if (next == null) return $"{item.Def.name}을(를) 벼리려 했지만 쇠가 받아들이지 않아요.";
            Silver -= fee;
            FantasyEventFx?.Invoke(this, "forge", _eventProp != null ? _eventProp.transform.position : Player.transform.position); // 모루 자리
            return $"{item.Def.name}이(가) 청보라 불꽃 속에서 {other.name}(으)로 다시 벼려졌어요. 이계 기물이에요.";
        }

        // ── 봉인된 마검: 이계 정예 오크 + 스켈레톤 둘
        void StartSealedFight()
        {
            _sealedFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            HideSealedBlade();
            if (_eventProp != null) FantasyEventFx?.Invoke(this, "unseal", _eventProp.transform.position);
            else FxAtPlayer("unseal");
            var wave = new List<EnemyKind>();
            for (int i = 0, n = Price("event.sealed_skeletons", 2f); i < n; i++) wave.Add(EnemyKind.Skeleton);
            _waves = new List<List<EnemyKind>> { wave };
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = 0;
            _eliteKind = EnemyKind.Orc;
            var traits = new List<EliteTrait>();
            foreach (EliteTrait t in Enum.GetValues(typeof(EliteTrait)))
                if (EnemyBrain.Allows(EnemyKind.Orc, t)) traits.Add(t);
            _eliteTrait = traits[_rng.Next(traits.Count)];
            ShowBanner("봉인이 끊어지고 · 차원 틈에서 이계의 전사들이 쏟아져요");
            StartNextWave();
        }

        // 뽑은 검은 소품에서 사라져요 (다시 들어와도)
        void HideSealedBlade()
        {
            if (_eventProp == null) return;
            var blade = _eventProp.transform.Find("Blade");
            if (blade != null) blade.gameObject.SetActive(false);
        }

        static float[] SealedWeights() => new[]
        {
            GameConfig.GetOptional("event.sealed_grade.common", 0f), GameConfig.GetOptional("event.sealed_grade.uncommon", 0f),
            GameConfig.GetOptional("event.sealed_grade.rare", 0.75f), GameConfig.GetOptional("event.sealed_grade.legendary", 0.25f),
        };

        // ClearRoom에서
        bool FinishSealedFight(ref string text)
        {
            if (!_sealedFight) return false;
            _sealedFight = false;
            arena.GrantOffer(SealedWeights());
            text += " · 이계의 전사들이 흩어지고 마검이 손에 남아요, 기물 고르기";
            return true;
        }

        GameObject FantasyPropFor(string id) => id switch
        {
            "otherworld_smith" => otherworldForgeProp,
            "chapel" => chapelAltarProp,
            "mana_spring" => manaSpringProp,
            "sealed_blade" => sealedBladeProp,
            _ => null,
        };
    }
}
