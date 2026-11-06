using System;
using System.Collections.Generic;
using AThousandDawns.Data;
using AThousandDawns.Player;

namespace AThousandDawns.Items
{
    // data_items CSV → 기물 정의. 머리줄: id, keyword, name, grade, aux, min_qi, max_qi, v1~v4, scales, side, side_value, effect, side_text
    public static class ItemTable
    {
        public static List<ItemDefinition> Read(string csv, List<string> errors)
        {
            var items = new List<ItemDefinition>();
            var rows = CsvReader.Parse(csv);
            if (rows.Count == 0)
            {
                errors.Add("data_items: 내용이 비어 있어요.");
                return items;
            }

            var header = rows[0];
            int Col(string name)
            {
                for (int i = 0; i < header.Length; i++)
                    if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
                return -1;
            }
            int idCol = Col("id"), keywordCol = Col("keyword"), nameCol = Col("name"), gradeCol = Col("grade"), auxCol = Col("aux");
            int minCol = Col("min_qi"), maxCol = Col("max_qi"), scalesCol = Col("scales"), sideCol = Col("side"), sideValueCol = Col("side_value");
            int effectCol = Col("effect"), sideTextCol = Col("side_text");
            int[] vCols = { Col("v1"), Col("v2"), Col("v3"), Col("v4") };
            if (idCol < 0 || keywordCol < 0 || minCol < 0 || maxCol < 0)
            {
                errors.Add("data_items: 첫 줄에 id, keyword, min_qi, max_qi 머리글이 있어야 해요.");
                return items;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string Cell(int c) => c >= 0 && c < row.Length ? row[c].Trim() : "";
                string id = Cell(idCol);
                if (id.Length == 0 || id.StartsWith("#")) continue;
                if (!seen.Add(id))
                {
                    errors.Add($"data_items {r + 1}행: '{id}'가 두 번 나와요.");
                    continue;
                }
                if (!TryKeyword(Cell(keywordCol), out var keyword))
                {
                    errors.Add($"data_items {r + 1}행: 키워드 '{Cell(keywordCol)}'를 모르겠어요.");
                    continue;
                }
                if (!int.TryParse(Cell(minCol), out int minQi) || !int.TryParse(Cell(maxCol), out int maxQi) || maxQi < minQi)
                {
                    errors.Add($"data_items {r + 1}행: '{id}'의 최소·최대 기를 읽을 수 없어요.");
                    continue;
                }

                var v = new float[4];
                for (int i = 0; i < 4; i++) SheetValue.TryParseFloat(Cell(vCols[i]), out v[i]); // 빈 칸은 0
                string scales = " " + Cell(scalesCol) + " ";
                SheetValue.TryParseFloat(Cell(sideValueCol), out float sideValue);

                items.Add(new ItemDefinition
                {
                    id = id,
                    keyword = keyword,
                    name = Cell(nameCol),
                    grade = Grade(Cell(gradeCol)),
                    aux = Cell(auxCol),
                    minQi = minQi,
                    maxQi = maxQi,
                    v1 = v[0], v2 = v[1], v3 = v[2], v4 = v[3],
                    v1Scales = scales.Contains(" v1 "), v2Scales = scales.Contains(" v2 "),
                    v3Scales = scales.Contains(" v3 "), v4Scales = scales.Contains(" v4 "),
                    side = Cell(sideCol),
                    sideValue = sideValue,
                    effectText = Cell(effectCol),
                    sideText = Cell(sideTextCol),
                });
            }
            return items;
        }

        static bool TryKeyword(string raw, out Keyword keyword)
        {
            for (int i = 0; i < PlayerKeywords.KeywordCount; i++)
            {
                if (PlayerKeywords.Label((Keyword)i) != raw) continue;
                keyword = (Keyword)i;
                return true;
            }
            keyword = default;
            return false;
        }

        static ItemGrade Grade(string raw) => raw switch
        {
            "고급" => ItemGrade.Uncommon,
            "희귀" => ItemGrade.Rare,
            "전설" => ItemGrade.Legendary,
            _ => ItemGrade.Common,
        };
    }
}
