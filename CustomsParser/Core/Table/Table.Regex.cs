using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public void RegexExtract(
            int col, string pattern, bool caseInsensitive, int group,
            bool allMatches, bool inPlace, bool expandToMultipleColumns,
            string? newColumnBaseName, string joinSeparator)
        {
            var opts = RegexOptions.Compiled | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            var rx = new Regex(pattern, opts);

            if (!allMatches)
            {
                var values = new List<string>(Rows.Count);
                foreach (var r in Rows)
                {
                    string cell = SafeCell(r, col);
                    var m = rx.Match(cell);
                    string val = "";
                    if (m.Success) val = (group >= 0 && group < m.Groups.Count) ? (m.Groups[group].Value ?? "") : (m.Value ?? "");
                    values.Add(val);
                }

                if (inPlace)
                {
                    for (int i = 0; i < Rows.Count; i++)
                        if (col < Rows[i].Length) Rows[i][col] = values[i];
                }
                else
                {
                    InsertNewColumnAfter(col, values, string.IsNullOrWhiteSpace(newColumnBaseName) ? $"{ColumnNames[col]}_rx" : newColumnBaseName!.Trim());
                }
                return;
            }

            var all = new List<List<string>>(Rows.Count);
            int maxMatches = 0;
            foreach (var r in Rows)
            {
                string cell = SafeCell(r, col);
                var matches = rx.Matches(cell);
                var vals = new List<string>();
                foreach (Match m in matches)
                {
                    string v = (group >= 0 && group < m.Groups.Count) ? (m.Groups[group].Value ?? "") : (m.Value ?? "");
                    vals.Add(v);
                }
                all.Add(vals);
                if (vals.Count > maxMatches) maxMatches = vals.Count;
            }

            if (inPlace)
            {
                for (int i = 0; i < Rows.Count; i++)
                    Rows[i][col] = string.Join(joinSeparator ?? ", ", all[i]);
                return;
            }

            if (expandToMultipleColumns)
            {
                string baseName = string.IsNullOrWhiteSpace(newColumnBaseName) ? $"{ColumnNames[col]}_rx" : newColumnBaseName!.Trim();
                for (int m = 0; m < maxMatches; m++)
                {
                    var values = new List<string>(Rows.Count);
                    for (int i = 0; i < Rows.Count; i++)
                        values.Add(m < all[i].Count ? all[i][m] : "");
                    InsertNewColumnAfter(col + m, values, $"{baseName}_m{m + 1}");
                }
            }
            else
            {
                var values = all.Select(v => string.Join(joinSeparator ?? ", ", v)).ToList();
                InsertNewColumnAfter(col, values, string.IsNullOrWhiteSpace(newColumnBaseName) ? $"{ColumnNames[col]}_rx" : newColumnBaseName!.Trim());
            }
        }
    }
}
