using System;
using System.Collections.Generic;

namespace AThousandDawns.Items
{
    // 기물 고르기 후보 뽑기: 등급을 먼저 굴리고 (등급확률 탭, 시험판은 죽림 줄), 그 등급에서 아직 없는 기물을 하나.
    // 후보가 없는 등급은 빼고 굴려요. 같은 기물은 한 번에 한 장만 나와요.
    public static class ItemOffer
    {
        public static List<ItemDefinition> Roll(IReadOnlyList<ItemDefinition> pool, Func<string, bool> owned, int count, IReadOnlyList<float> gradeWeights, Func<float> random)
        {
            var candidates = new List<ItemDefinition>();
            foreach (var def in pool)
                if (def != null && !owned(def.id)) candidates.Add(def);

            var result = new List<ItemDefinition>();
            var ofGrade = new List<ItemDefinition>();
            while (result.Count < count && candidates.Count > 0)
            {
                float total = 0f;
                for (int g = 0; g < 4; g++)
                    if (HasGrade(candidates, (ItemGrade)g)) total += Weight(gradeWeights, g);

                ofGrade.Clear();
                if (total <= 0f) ofGrade.AddRange(candidates);
                else
                {
                    float r = random() * total;
                    var grade = ItemGrade.Legendary;
                    for (int g = 0; g < 4; g++)
                    {
                        if (!HasGrade(candidates, (ItemGrade)g)) continue;
                        float w = Weight(gradeWeights, g);
                        grade = (ItemGrade)g;
                        if (r < w) break;
                        r -= w;
                    }
                    foreach (var def in candidates)
                        if (def.grade == grade) ofGrade.Add(def);
                }

                var pick = ofGrade[Math.Min(ofGrade.Count - 1, (int)(random() * ofGrade.Count))];
                result.Add(pick);
                candidates.Remove(pick);
            }
            return result;
        }

        static float Weight(IReadOnlyList<float> weights, int grade) => weights != null && grade < weights.Count ? Math.Max(0f, weights[grade]) : 1f;

        static bool HasGrade(List<ItemDefinition> candidates, ItemGrade grade)
        {
            foreach (var def in candidates)
                if (def.grade == grade) return true;
            return false;
        }
    }
}
