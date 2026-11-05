using System;
using System.Collections.Generic;

namespace AThousandDawns.Data
{
    // data_balance CSV (머리줄: key, value, unit, note) → BalanceConfig 항목.
    public static class BalanceTable
    {
        public static List<BalanceConfig.Entry> Read(string csv, List<string> errors)
        {
            var entries = new List<BalanceConfig.Entry>();
            var rows = CsvReader.Parse(csv);
            if (rows.Count == 0)
            {
                errors.Add("data_balance: 내용이 비어 있어요.");
                return entries;
            }

            var header = rows[0];
            int keyCol = IndexOf(header, "key");
            int valueCol = IndexOf(header, "value");
            int unitCol = IndexOf(header, "unit");
            int noteCol = IndexOf(header, "note");
            if (keyCol < 0 || valueCol < 0)
            {
                errors.Add("data_balance: 첫 줄에 key, value 머리글이 있어야 해요.");
                return entries;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string key = Cell(row, keyCol).Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;

                if (!seen.Add(key))
                {
                    errors.Add($"data_balance {r + 1}행: '{key}' 키가 두 번 나와요.");
                    continue;
                }

                string raw = Cell(row, valueCol);
                if (!SheetValue.TryParseFloat(raw, out float value))
                {
                    errors.Add($"data_balance {r + 1}행: '{key}'의 값 '{raw}'를 숫자로 읽을 수 없어요.");
                    continue;
                }

                entries.Add(new BalanceConfig.Entry
                {
                    key = key,
                    value = value,
                    unit = unitCol >= 0 ? Cell(row, unitCol) : "",
                    note = noteCol >= 0 ? Cell(row, noteCol) : "",
                });
            }
            return entries;
        }

        static int IndexOf(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
                if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        static string Cell(string[] row, int col) => col < row.Length ? row[col] : "";
    }
}
