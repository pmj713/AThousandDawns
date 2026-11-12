using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마총 전용 이벤트 3개 (4지역 기획): 천마총에서만 이벤트 풀에 더해져요.
    //   피의 연못: 이번 판 동안 최대 체력 −10%, 대신 전설이 잘 나오는 기물 고르기 (일반 0 · 고급 30 · 희귀 40 · 전설 30).
    //   갇힌 무림맹 고수: 구하면 혈인 · 천마 근위와 전투, 이기면 천마전을 보호막 (최대 체력 20%)을 두르고 시작해요.
    //   마교 제단: 기물 하나를 바치면 공력 +1 / 부수면 추적도 +3, 은자 +120.
    public sealed partial class RoomRun
    {
        public static readonly string[] TombEventIds = { "blood_pool", "captive_hero", "demon_altar" };

        bool _heroFight;   // 무림맹 고수를 구하려 싸우는 중
        bool _heroShield;  // 구했음: 천마전을 보호막 두르고 시작

        public bool HeroShieldPending => _heroShield;
        public static event System.Action<PlayerController> HeroShieldCue; // 그림 신호: 보호막을 받는 순간 (아트 InkHeavenlyDemonFx가 내력이 흘러드는 연출)

        bool BuildTombEvent(EventView v, string id)
        {
            switch (id)
            {
                case "blood_pool":
                {
                    v.Title = "피의 연못";
                    v.Text = "무덤 바닥이 꺼진 자리에 검붉은 피가 고여 있다. 그 아래에서 무언가 빛난다.";
                    float cut = GameConfig.GetOptional("event.blood_pool_cut", 0.1f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"손을 넣는다 · 이번 판 동안 최대 체력 −{cut:0%}, 전설이 잘 나오는 기물 고르기",
                        Act = () =>
                        {
                            Player.CutMaxHealth(cut);
                            AThousandDawns.Fx.InkEventFx.Play(AThousandDawns.Fx.InkEventFx.BloodSeep, "blood_pool");
                            arena.GrantOffer(BloodPoolWeights());
                            return $"피가 손을 타고 스며들어요. 최대 체력 −{cut:0%}";
                        },
                    });
                    v.Choices.Add(Pass("물러선다"));
                    return true;
                }

                case "captive_hero":
                {
                    v.Title = "갇힌 무림맹 고수";
                    v.Text = "쇠사슬에 묶인 무림맹 고수가 혈인과 천마 근위에게 둘러싸여 있다. \"천마에게 가거든… 나를 풀어 주게.\"";
                    int shield = Mathf.RoundToInt(GameConfig.GetOptional("event.hero_shield", 0.2f) * 100f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"구한다 · 혈인 · 천마 근위와 전투, 이기면 천마전을 보호막 (최대 체력 {shield}%)을 두르고 시작",
                        Act = () => { CloseEvent(); StartHeroFight(); return null; },
                    });
                    v.Choices.Add(Pass("못 본 척한다"));
                    return true;
                }

                case "demon_altar":
                {
                    v.Title = "마교 제단";
                    v.Text = "천마를 모시는 제단 위에 핏빛 향이 피어오른다. 바쳐진 무기들이 녹아 있다.";
                    int noise = Price("event.altar_tracking", 3f), loot = Price("event.altar_silver", 120f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = "기물을 바친다 · 기물 하나를 잃고 공력 +1",
                        Locked = Player.Items == null || Player.Items.Items.Count == 0 ? "바칠 기물이 없어요" : null,
                        Act = () => { OpenAltarPick(); return null; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"부순다 · 추적도 +{noise}, 은자 +{loot}",
                        Act = () =>
                        {
                            AddTracking(noise);
                            Silver += loot;
                            AThousandDawns.Fx.InkEventFx.Play(AThousandDawns.Fx.InkEventFx.AltarBreak, "demon_altar");
                            return $"제단이 무너지며 공물이 쏟아져요. 은자 +{loot} · 소란에 추적도 {Tracking}";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    return true;
                }
            }
            return false;
        }

        static float[] BloodPoolWeights() => new[]
        {
            GameConfig.GetOptional("event.blood_pool_grade.common", 0f), GameConfig.GetOptional("event.blood_pool_grade.uncommon", 0.3f),
            GameConfig.GetOptional("event.blood_pool_grade.rare", 0.4f), GameConfig.GetOptional("event.blood_pool_grade.legendary", 0.3f),
        };

        // ── 마교 제단: 바칠 기물 고르기
        void OpenAltarPick()
        {
            var v = new EventView { Id = "demon_altar_pick", Title = "마교 제단", Text = "무엇을 바칠까?" };
            int shown = 0;
            foreach (var owned in Player.Items.Items)
            {
                if (shown++ >= 8) break;
                var item = owned;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name} · {ItemDefinition.GradeLabel(item.Def.grade)}",
                    Act = () =>
                    {
                        Player.Items.Remove(item);
                        Player.Gongryeok += 1;
                        AThousandDawns.Fx.InkEventFx.Play(AThousandDawns.Fx.InkEventFx.AltarMelt, "demon_altar", ItemIcons.Get(item.Def));
                        return $"{item.Def.name}이(가) 핏빛 불길에 녹아요. 공력 +1";
                    },
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => "제단을 등졌어요." });
            OpenEventView(v, null);
        }

        // ── 갇힌 무림맹 고수: 혈인 + 천마 근위 (ClearRoom에서 보호막 약속)
        void StartHeroFight()
        {
            _heroFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            _waves = new List<List<EnemyKind>> { new List<EnemyKind> { EnemyKind.BloodShaman, EnemyKind.DemonGuard } };
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = -1;
            StartNextWave();
        }

        // 천마전을 시작할 때 (보스 방): 구한 고수가 보호막을 둘러 줘요
        void ApplyHeroShield()
        {
            if (!_heroShield || !LastRegion) return;
            _heroShield = false;
            int amount = Mathf.Max(1, Mathf.RoundToInt(Player.MaxHealth * GameConfig.GetOptional("event.hero_shield", 0.2f)));
            Player.Shields.Add(amount, 0f, amount);
            HeroShieldCue?.Invoke(Player);
            ShowBanner("무림맹 고수가 내력을 나눠 줘요 · 보호막");
        }
    }
}
