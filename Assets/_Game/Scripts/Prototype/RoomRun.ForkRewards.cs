using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 갈림길 보상 (처음부터 나오는 것들): 은자 (전리품 더미) · 수련 (고수의 수련 흔적) · 잡화 (봇짐) · 비급 (낡은 서고, 검일 때만)
    //   짙은 길이면 더 커요: 은자 80 → 120, 수련치 25 → 40, 금창약 1 → 2 (가득하면 1개당 은자 40), 비급 1권 → 남은 두 권 중 고르기
    // 기물 · 운기조식 · 부품 · 대장간은 RoomRun.cs (ClearRoom)에서 그대로.
    public sealed partial class RoomRun
    {
        static int ForkValue(string key, bool dense, float light, float heavy) =>
            Mathf.RoundToInt(GameConfig.GetOptional(dense ? key + "_dense" : key, dense ? heavy : light));

        int ForkSilver(bool dense) => ForkValue("fork.silver", dense, 80f, 120f);
        int ForkXp(bool dense) => ForkValue("fork.xp", dense, 25f, 40f);
        int ForkPotions(bool dense) => ForkValue("fork.potions", dense, 1f, 2f);
        int PotionSilver => Mathf.RoundToInt(GameConfig.GetOptional("fork.potion_silver", 40f));

        // 갈림길 카드 설명 첫 줄 (새 보상만, 나머지는 null)
        string ForkRewardDetail(Path p) => p.Reward switch
        {
            RewardKind.Silver => $"은자 {ForkSilver(p.Dense)}",
            RewardKind.Training => $"수련치 {ForkXp(p.Dense)}",
            RewardKind.Supply => $"금창약 {ForkPotions(p.Dense)}개 (가득하면 1개당 은자 {PotionSilver})",
            RewardKind.Scroll => p.Dense ? "비급 두 권 중 하나를 골라 배우기" : "지금 초식이 아닌 비급 한 권",
            _ => null,
        };

        // 보상 방을 비웠을 때 (새 보상만). 배너에 덧붙일 글, 아니면 null
        string GrantForkReward(Path p)
        {
            switch (p.Reward)
            {
                case RewardKind.Silver:
                {
                    int silver = BoostSilver(ForkSilver(p.Dense));
                    Silver += silver;
                    return $" · 전리품 더미, 은자 +{silver}";
                }
                case RewardKind.Training:
                {
                    int xp = ForkXp(p.Dense);
                    Player.Level?.Add(xp);
                    return $" · 고수의 수련 흔적, 수련치 +{xp}";
                }
                case RewardKind.Supply:
                {
                    int potions = 0, silver = 0;
                    for (int i = ForkPotions(p.Dense); i > 0; i--)
                    {
                        if (Player.AddPotion()) potions++;
                        else silver += PotionSilver;
                    }
                    Silver += silver;
                    return $" · 봇짐{(potions > 0 ? $", 금창약 +{potions}" : "")}{(silver > 0 ? $", 은자 +{silver} (금창약이 가득해요)" : "")}";
                }
                case RewardKind.Scroll:
                    OpenScrollChoice(p.Dense);
                    return " · 낡은 서고";
            }
            return null;
        }

        // 테스트용: 그 길의 보상 방을 비운 것처럼
        public string GrantForkRewardForTest(RewardKind kind, bool dense) => GrantForkReward(new Path { Reward = kind, Dense = dense });

        // 낡은 서고: 지금 초식이 아닌 비급 (짙으면 두 권 다 보여 줘요). 안 배우고 지나갈 수도 있어요
        void OpenScrollChoice(bool dense)
        {
            var others = new List<SkillKind>();
            foreach (SkillKind s in System.Enum.GetValues(typeof(SkillKind)))
                if (s != Player.Skill && s != SkillKind.Palm && s != SkillKind.Takrang) others.Add(s); // 금강장 · 탁랑참은 고수에게서만
            if (!dense && others.Count > 1) others = new List<SkillKind> { others[_rng.Next(others.Count)] };
            var v = new EventView
            {
                Id = "scroll",
                Title = "낡은 서고",
                Text = $"먼지 쌓인 서가에 비급이 꽂혀 있다. 지금 초식은 {Player.SkillName}.",
            };
            foreach (var skill in others)
            {
                var s = skill;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{SkillLabel(s)}을(를) 익힌다 · {SkillText(s)}",
                    Act = () => { Player.LearnSkill(s); return $"{SkillLabel(s)}을(를) 깨우쳤어요. 우클릭 초식이 바뀌었어요."; },
                });
            }
            v.Choices.Add(new EventChoice { Label = "그냥 둔다", Act = () => "서고를 나섰어요." });
            OpenEventView(v, null);
        }

        static string SkillLabel(SkillKind s) => PlayerController.SkillLabel(s);

        static string SkillText(SkillKind s) => PlayerController.SkillText(s);
    }
}
