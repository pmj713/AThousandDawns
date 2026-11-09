using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;

namespace AThousandDawns.Prototype
{
    // 객잔 (첫 버전): 진열 기물 3개 · 금창약 2개 · 숙박. 가격은 은자 시트 (지역과 상관없이 고정).
    // 기물 첫 장은 내 주 키워드 2개(가진 기물에 가장 많은 키워드) 중에서, 나머지는 무작위. 등급은 그 지역 확률표.
    // 부품 (해금한 것만) · 비급 (25%, 지금 초식이 아닌 것) · 이혈 (단전 하나 옮기기). 나중에: 소문 듣기·매각 (매각 비율은 시트에서 미정)
    public sealed class InnShop
    {
        public enum SlotKind { Item, Potion, Lodging, Part, Scroll, MoveDanjeon }

        public sealed class Slot
        {
            public SlotKind Kind;
            public ItemDefinition Item;
            public PartKind Part;
            public SkillKind Skill;   // 비급
            public int Price;
            public bool Sold;
        }

        readonly List<Slot> _slots = new List<Slot>();
        public IReadOnlyList<Slot> Slots => _slots;

        // 판 중간 저장에서 되살릴 때 (진열을 다시 뽑지 않게)
        public static InnShop FromSlots(IEnumerable<Slot> slots)
        {
            var shop = new InnShop();
            shop._slots.AddRange(slots);
            return shop;
        }

        public static int ItemPrice(ItemGrade grade) => (int)Math.Round(GameConfig.Get(grade switch
        {
            ItemGrade.Common => "price.item_common",
            ItemGrade.Uncommon => "price.item_uncommon",
            ItemGrade.Rare => "price.item_rare",
            _ => "price.item_legendary",
        }, grade == ItemGrade.Common ? 120f : grade == ItemGrade.Uncommon ? 300f : grade == ItemGrade.Rare ? 500f : 900f));

        public static InnShop Stock(IReadOnlyList<ItemDefinition> pool, PlayerItems owned, IReadOnlyList<float> gradeWeights, Random rng, int extraItems = 0, IReadOnlyList<PartKind> parts = null, float priceScale = 1f, SkillKind? currentSkill = null)
        {
            var shop = new InnShop();
            int count = (int)Math.Round(GameConfig.Get("inn.item_stock", 3f)) + extraItems; // 회귀록 객잔 진열 +1
            Func<float> random = () => (float)rng.NextDouble();
            var picked = new List<ItemDefinition>();
            bool Taken(string id) => (owned != null && owned.Has(id)) || picked.Exists(d => d.id == id);

            // 첫 장: 주 키워드 2개 중에서 (가진 기물이 없으면 그냥 무작위)
            var main = MainKeywords(owned);
            if (main.Count > 0 && count > 0)
            {
                var mainPool = new List<ItemDefinition>();
                foreach (var def in pool) if (def != null && main.Contains(def.keyword)) mainPool.Add(def);
                picked.AddRange(ItemOffer.Roll(mainPool, Taken, 1, gradeWeights, random));
            }
            picked.AddRange(ItemOffer.Roll(pool, Taken, count - picked.Count, gradeWeights, random));
            foreach (var def in picked) shop._slots.Add(new Slot { Kind = SlotKind.Item, Item = def, Price = ItemPrice(def.grade) });

            // 부품 1개: 해금한 종류 중 무작위 (은자 시트 B23)
            if (parts != null && parts.Count > 0)
                shop._slots.Add(new Slot { Kind = SlotKind.Part, Part = parts[rng.Next(parts.Count)], Price = (int)Math.Round(GameConfig.Get("price.part", 100f)) });

            // 비급: 25%로 하나 (은자 시트 B22). 지금 초식이 아닌 고수 초식 중에서. currentSkill이 없으면 (테스트) 안 놓아요.
            if (currentSkill.HasValue && rng.NextDouble() < GameConfig.GetOptional("inn.scroll_chance", 0.25f))
            {
                var skill = currentSkill.Value == SkillKind.BambooFlash ? SkillKind.Quake
                    : currentSkill.Value == SkillKind.Quake ? SkillKind.BambooFlash
                    : rng.Next(2) == 0 ? SkillKind.BambooFlash : SkillKind.Quake;
                shop._slots.Add(new Slot { Kind = SlotKind.Scroll, Skill = skill, Price = (int)Math.Round(GameConfig.GetOptional("price.scroll", 300f)) });
            }

            // 이혈: 단전 하나 옮기기 (은자 시트 B26)
            shop._slots.Add(new Slot { Kind = SlotKind.MoveDanjeon, Price = (int)Math.Round(GameConfig.GetOptional("price.move_danjeon", 100f)) });

            int potions = (int)Math.Round(GameConfig.Get("inn.potion_stock", 2f));
            int potionPrice = (int)Math.Round(GameConfig.Get("price.potion", 60f));
            for (int i = 0; i < potions; i++) shop._slots.Add(new Slot { Kind = SlotKind.Potion, Price = potionPrice });
            shop._slots.Add(new Slot { Kind = SlotKind.Lodging, Price = (int)Math.Round(GameConfig.Get("price.lodging", 40f)) });
            if (priceScale != 1f) foreach (var s in shop._slots) s.Price = (int)Math.Round(s.Price * priceScale); // 면벽수련 은 10: 객잔 −15%
            return shop;
        }

        // 가진 기물에 가장 많이 나온 키워드 2개
        public static List<Keyword> MainKeywords(PlayerItems owned)
        {
            var counts = new int[PlayerKeywords.KeywordCount];
            if (owned != null) foreach (var item in owned.Items) counts[(int)item.Def.keyword]++;
            var result = new List<Keyword>();
            for (int n = 0; n < 2; n++)
            {
                int best = -1;
                for (int k = 0; k < counts.Length; k++)
                    if (counts[k] > 0 && !result.Contains((Keyword)k) && (best < 0 || counts[k] > counts[best])) best = k;
                if (best >= 0) result.Add((Keyword)best);
            }
            return result;
        }

        // 못 사는 이유 (살 수 있으면 null)
        public string Blocker(int index, PlayerController player, int silver)
        {
            if (index < 0 || index >= _slots.Count) return "없는 칸";
            var slot = _slots[index];
            if (slot.Sold) return "팔렸어요";
            if (silver < slot.Price) return "은자가 모자라요";
            if (slot.Kind == SlotKind.Potion && player.Potions >= player.PotionMax) return $"금창약은 {player.PotionMax}개까지";
            if (slot.Kind == SlotKind.Lodging && player.Health >= player.MaxHealth) return "체력이 가득해요";
            if (slot.Kind == SlotKind.Scroll && player.Skill == slot.Skill) return "이미 배웠어요";
            if (slot.Kind == SlotKind.MoveDanjeon && player.Items.Board.Danjeons.Count == 0) return "아직 단전이 없어요";
            return null;
        }

        // 사면 쓴 은자를 돌려줘요 (못 사면 0)
        public int Buy(int index, PlayerController player, int silver)
        {
            if (Blocker(index, player, silver) != null) return 0;
            var slot = _slots[index];
            switch (slot.Kind)
            {
                case SlotKind.Item: player.Items.Add(slot.Item); break;
                case SlotKind.Potion: player.AddPotion(); break;
                case SlotKind.Part: player.Items.AddPart(slot.Part); break;
                case SlotKind.Lodging: player.Heal(player.MaxHealth * GameConfig.Get("lodging.heal", 0.3f)); break;
                case SlotKind.Scroll: player.LearnSkill(slot.Skill); break;
                case SlotKind.MoveDanjeon: player.Items.DanjeonMoves++; break; // 경맥도에서 옮겨요
            }
            slot.Sold = true;
            return slot.Price;
        }
    }
}
