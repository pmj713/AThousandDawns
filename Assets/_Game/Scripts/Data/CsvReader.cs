using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AThousandDawns.Data
{
    // 구글 시트 CSV 내보내기를 읽어요. 따옴표로 감싼 칸, 칸 안의 쉼표·줄바꿈을 처리해요.
    public static class CsvReader
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;

            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;
            int start = text[0] == '﻿' ? 1 : 0;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(cell.ToString()); cell.Clear();
                        rows.Add(row.ToArray()); row.Clear();
                        break;
                    default: cell.Append(c); break;
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }

    public static class SheetValue
    {
        // 시트가 보여 주는 형태 그대로 읽어요: "40%" → 0.4, "1,100" → 1100.
        public static bool TryParseFloat(string raw, out float value)
        {
            value = 0f;
            if (raw == null) return false;
            string s = raw.Trim().Replace(",", "");
            bool percent = s.EndsWith("%");
            if (percent) s = s.Substring(0, s.Length - 1).Trim();
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            if (percent) value /= 100f;
            return true;
        }
    }
}
