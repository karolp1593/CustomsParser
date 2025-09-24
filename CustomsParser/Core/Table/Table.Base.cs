using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public List<string[]> Rows { get; private set; } = new();
        public List<string> ColumnNames { get; private set; } = new();
        public List<int> RowPages { get; private set; } = new();
        public int ColumnCount => ColumnNames.Count;

        public bool IsScalar { get; private set; } = false;
        public string? ScalarValue { get; private set; } = null;

        public static Table FromSingleColumnLines(List<string> lines, List<int> rowPages)
        {
            var t = new Table();
            t.ColumnNames = new List<string> { "Col0" };
            t.RowPages = new List<int>(rowPages);
            foreach (var line in lines)
                t.Rows.Add(new[] { line ?? string.Empty });
            return t;
        }

        public Table Clone() => new Table
        {
            Rows = Rows.Select(r => r.ToArray()).ToList(),
            ColumnNames = new List<string>(ColumnNames),
            RowPages = new List<int>(RowPages),
            IsScalar = IsScalar,
            ScalarValue = ScalarValue
        };

        public void ToScalarFromCell(int col, int row = 0, string? pattern = null, int group = 1, bool requireSingleCell = true, bool trim = true)
        {
            if (Rows.Count == 0) { IsScalar = true; ScalarValue = ""; return; }
            row = Math.Clamp(row, 0, Rows.Count - 1);
            string cell = (col >= 0 && col < (Rows[row]?.Length ?? 0)) ? (Rows[row][col] ?? "") : "";
            string value = cell;
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                var rx = new Regex(pattern, RegexOptions.Compiled);
                var m = rx.Match(cell);
                value = m.Success ? (group >= 0 && group < m.Groups.Count ? m.Groups[group].Value : m.Value) : "";
            }
            if (trim) value = value.Trim();
            IsScalar = true; ScalarValue = value;
        }

        public void Preview(int maxRows = 50, bool showPages = true, bool showRowIndex = true)
        {
            if (IsScalar) { Console.WriteLine($"[SCALAR] {ScalarValue}"); return; }

            int rows = Math.Min(maxRows, Rows.Count);
            Console.WriteLine($"Preview: {rows}/{Rows.Count} row(s), {ColumnCount} col(s)");
            Console.WriteLine(new string('-', 80));
            for (int i = 0; i < rows; i++)
            {
                var cells = Rows[i];
                var left = new List<string>();
                if (showRowIndex) left.Add($"#{i:0000}");
                if (showPages && i < RowPages.Count) left.Add($"p{RowPages[i]}");
                var prefix = left.Count > 0 ? string.Join(" ", left) + " | " : "";
                Console.Write(prefix);
                for (int c = 0; c < ColumnCount; c++)
                {
                    string val = c < cells.Length ? (cells[c] ?? "") : "";
                    if (c > 0) Console.Write(" | ");
                    Console.Write(val.Replace("\n", "\\n"));
                }
                Console.WriteLine();
            }
            Console.WriteLine(new string('-', 80));
            Console.WriteLine("Columns: " + string.Join(" | ", ColumnNames));
        }

        // ---- helpers for other partials ----
        static (string rest, string last) CutLastWords(string input, int count)
        {
            if (count <= 0) return (input ?? "", "");
            var tokens = (input ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (tokens.Count == 0) return ("", "");
            if (count >= tokens.Count) return ("", string.Join(" ", tokens));
            var last = string.Join(" ", tokens.Skip(tokens.Count - count));
            var rest = string.Join(" ", tokens.Take(tokens.Count - count));
            return (rest, last);
        }

        static string[] ExpandWithSplit(string[] row, int col, string left, string right)
        {
            var expanded = new List<string>(row.Length + 1);
            for (int i = 0; i < row.Length; i++)
            {
                if (i == col) { expanded.Add(left ?? ""); expanded.Add(right ?? ""); }
                else expanded.Add(row[i] ?? "");
            }
            return expanded.ToArray();
        }

        void InsertNewColumnName(int col, string newRightName)
        {
            ColumnNames[col] = ColumnNames[col];
            ColumnNames.Insert(col + 1, EnsureUniqueName(newRightName));
        }

        void InsertNewColumnAfter(int col, List<string> values, string baseName)
        {
            string name = EnsureUniqueName(baseName);
            ColumnNames.Insert(col + 1, name);
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                var expanded = new List<string>(r);
                expanded.Insert(col + 1, i < values.Count ? (values[i] ?? "") : "");
                Rows[i] = expanded.ToArray();
            }
        }

        string EnsureUniqueName(string baseName)
        {
            string name = string.IsNullOrWhiteSpace(baseName) ? "Col" + ColumnCount : baseName.Trim();
            var seen = new HashSet<string>(ColumnNames, StringComparer.OrdinalIgnoreCase);
            if (!seen.Contains(name)) return name;
            int k = 2; while (seen.Contains(name + "_" + k)) k++;
            return name + "_" + k;
        }

        string SafeCell(int rowIndex, int colIndex) => (colIndex < Rows[rowIndex].Length) ? (Rows[rowIndex][colIndex] ?? "") : "";
        static string SafeCell(string[] row, int colIndex) => (colIndex < row.Length) ? (row[colIndex] ?? "") : "";
    }
}
