using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public void SplitOnKeyword(int col, string keyword, bool caseInsensitive, bool splitAllOccurrences)
        {
            if (string.IsNullOrEmpty(keyword)) return;

            if (!splitAllOccurrences)
            {
                var newRows = new List<string[]>();
                foreach (var r in Rows)
                {
                    string cell = SafeCell(r, col);
                    int idx = caseInsensitive
                        ? cell.ToLowerInvariant().IndexOf(keyword.ToLowerInvariant())
                        : cell.IndexOf(keyword, StringComparison.Ordinal);

                    string left = cell, right = "";
                    if (idx >= 0) { left = cell.Substring(0, idx); right = cell[(idx + keyword.Length)..]; }
                    newRows.Add(ExpandWithSplit(r, col, left, right));
                }
                Rows = newRows;
                InsertNewColumnName(col, $"{ColumnNames[col]}_R");
                return;
            }

            var pattern = Regex.Escape(keyword);
            var options = caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;

            var allParts = new List<string[]>();
            int maxParts = 1;
            foreach (var r in Rows)
            {
                string cell = SafeCell(r, col);
                var parts = Regex.Split(cell, pattern, options);
                allParts.Add(parts);
                if (parts.Length > maxParts) maxParts = parts.Length;
            }

            var finalRows = new List<string[]>();
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                var parts = allParts[i];
                var newRow = new List<string>(r.Length + (maxParts - 1));

                for (int c = 0; c < col; c++) newRow.Add(SafeCell(r, c));
                newRow.Add(parts.Length > 0 ? parts[0] ?? "" : "");
                for (int k = 1; k < maxParts; k++) newRow.Add(k < parts.Length ? (parts[k] ?? "") : "");
                for (int c = col + 1; c < r.Length; c++) newRow.Add(SafeCell(r, c));

                finalRows.Add(newRow.ToArray());
            }

            Rows = finalRows;

            var baseName = ColumnNames[col];
            for (int k = 2; k <= maxParts; k++)
                ColumnNames.Insert(col + (k - 1), $"{baseName}_Part{k}");
        }

        public void SplitAfterChars(int col, int n)
        {
            var newRows = new List<string[]>();
            foreach (var r in Rows)
            {
                string cell = SafeCell(r, col);
                n = Math.Clamp(n, 0, cell.Length);
                string left = cell[..n];
                string right = cell[n..];
                newRows.Add(ExpandWithSplit(r, col, left, right));
            }
            Rows = newRows;
            InsertNewColumnName(col, $"{ColumnNames[col]}_R");
        }

        public void SplitCutLastWordsToNewColumn(int col, int wordCount)
        {
            var newRows = new List<string[]>();
            foreach (var r in Rows)
            {
                string cell = SafeCell(r, col);
                var (rest, last) = CutLastWords(cell, wordCount);
                newRows.Add(ExpandWithSplit(r, col, rest, last));
            }
            Rows = newRows;
            InsertNewColumnName(col, $"{ColumnNames[col]}_Tail");
        }

        public void SplitOnRegexDelimiter(int col, string pattern, bool caseInsensitive)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;
            var options = RegexOptions.Compiled | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            var rx = new Regex(pattern, options);

            var newRows = new List<string[]>();
            foreach (var r in Rows)
            {
                string cell = SafeCell(r, col);
                var m = rx.Match(cell);
                string left = cell, right = "";
                if (m.Success) { left = cell.Substring(0, m.Index); right = cell[(m.Index + m.Length)..]; }
                newRows.Add(ExpandWithSplit(r, col, left, right));
            }
            Rows = newRows;
            InsertNewColumnName(col, $"{ColumnNames[col]}_R");
        }
    }
}
