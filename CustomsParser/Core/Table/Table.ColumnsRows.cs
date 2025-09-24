using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        // Keep only these column/row utilities in this file!

        public void KeepColumns(int[] keep)
        {
            var newNames = keep.Select(i => ColumnNames[i]).ToList();
            var newRows = new List<string[]>();
            foreach (var r in Rows)
                newRows.Add(keep.Select(i => i < r.Length ? (r[i] ?? "") : "").ToArray());
            ColumnNames = newNames;
            Rows = newRows;
        }

        public void DropFirstRow()
        {
            if (Rows.Count > 0)
            {
                Rows.RemoveAt(0);
                if (RowPages.Count > 0) RowPages.RemoveAt(0);
            }
        }

        public void RenameColumns(IList<string> names)
        {
            var updated = new List<string>();
            for (int i = 0; i < ColumnNames.Count; i++)
            {
                if (names != null && i < names.Count && !string.IsNullOrWhiteSpace(names[i]))
                    updated.Add(names[i].Trim());
                else
                    updated.Add(ColumnNames[i]);
            }
            ColumnNames = updated;

            // ensure uniqueness
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ColumnNames.Count; i++)
            {
                string baseName = string.IsNullOrWhiteSpace(ColumnNames[i]) ? $"Col{i}" : ColumnNames[i];
                string name = baseName; int k = 2;
                while (!seen.Add(name)) name = baseName + "_" + k++;
                ColumnNames[i] = name;
            }
        }

        /// <summary>
        /// Inserts a blank column at index [insertIndex]. Returns the actual index used.
        /// </summary>
        public int InsertBlankColumn(int insertIndex, string? name)
        {
            int idx = Math.Clamp(insertIndex, 0, ColumnCount);
            string colName = EnsureUniqueName(string.IsNullOrWhiteSpace(name) ? $"Col{idx}" : name!.Trim());

            ColumnNames.Insert(idx, colName);
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                var expanded = new List<string>(r.Length + 1);
                for (int c = 0; c < r.Length + 1; c++)
                {
                    if (c == idx) expanded.Add("");
                    else expanded.Add(c < idx ? r[c] : r[c - 1]);
                }
                Rows[i] = expanded.ToArray();
            }
            return idx;
        }

        /// <summary>
        /// Copies column src -> dest. If append==true, concatenates with separator; if overwrite==true, replaces.
        /// </summary>
        public void CopyColumn(int src, int dest, bool overwrite, bool append, string separator, bool onlyWhenSrcNonEmpty)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                string s = src < r.Length ? (r[src] ?? "") : "";
                if (onlyWhenSrcNonEmpty && string.IsNullOrEmpty(s)) continue;

                string d = dest < r.Length ? (r[dest] ?? "") : "";

                if (append)
                {
                    if (string.IsNullOrEmpty(d)) r[dest] = s;
                    else if (string.IsNullOrEmpty(s)) { /* nothing */ }
                    else r[dest] = d + (separator ?? " ") + s;
                }
                else if (overwrite)
                {
                    r[dest] = s;
                }
            }
        }

        // ======================
        // NEW: Split after W words → new column at col+1
        // ======================
        public void SplitAfterWords(int colIndex, int w)
        {
            if (Rows.Count == 0 || ColumnCount == 0) return;
            if (colIndex < 0 || colIndex >= ColumnCount) return;
            if (w < 0) w = 0;

            // new column name based on source col
            string baseName = (colIndex >= 0 && colIndex < ColumnNames.Count) ? ColumnNames[colIndex] : $"Col{colIndex}";
            int insertAt = Math.Min(ColumnCount, colIndex + 1);
            int actualIdx = InsertBlankColumn(insertAt, baseName + "_afterWords");

            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                string src = (colIndex < row.Length && row[colIndex] != null) ? row[colIndex]! : "";

                var tokens = src.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); // split on any whitespace
                if (tokens.Length == 0)
                {
                    row[colIndex] = "";
                    row[actualIdx] = "";
                    continue;
                }

                if (w <= 0)
                {
                    row[colIndex] = "";
                    row[actualIdx] = string.Join(" ", tokens);
                    continue;
                }

                if (w >= tokens.Length)
                {
                    row[colIndex] = string.Join(" ", tokens);
                    row[actualIdx] = "";
                    continue;
                }

                var left = string.Join(" ", tokens.Take(w));
                var right = string.Join(" ", tokens.Skip(w));
                row[colIndex] = left;
                row[actualIdx] = right;
            }
        }

        // ======================
        // NEW: Cut last N chars → new column at col+1
        // ======================
        public void SplitCutLastCharsToNewColumn(int colIndex, int n)
        {
            if (Rows.Count == 0 || ColumnCount == 0) return;
            if (colIndex < 0 || colIndex >= ColumnCount) return;
            if (n < 0) n = 0;

            string baseName = (colIndex >= 0 && colIndex < ColumnNames.Count) ? ColumnNames[colIndex] : $"Col{colIndex}";
            int insertAt = Math.Min(ColumnCount, colIndex + 1);
            int actualIdx = InsertBlankColumn(insertAt, baseName + "_lastChars");

            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                string src = (colIndex < row.Length && row[colIndex] != null) ? row[colIndex]! : "";

                if (n == 0)
                {
                    row[actualIdx] = "";
                    continue;
                }

                if (src.Length <= n)
                {
                    row[actualIdx] = src;
                    row[colIndex] = "";
                }
                else
                {
                    row[actualIdx] = src.Substring(src.Length - n);
                    row[colIndex] = src.Substring(0, src.Length - n);
                }
            }
        }
    }
}
