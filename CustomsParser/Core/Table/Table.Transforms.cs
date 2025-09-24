using System.Globalization;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public void TrimAll()
        {
            for (int i = 0; i < Rows.Count; i++)
                for (int c = 0; c < Rows[i].Length; c++)
                    Rows[i][c] = (Rows[i][c] ?? "").Trim();
        }

        public void TransformTrim(int col)
        {
            foreach (var r in Rows)
                if (col < r.Length) r[col] = (r[col] ?? "").Trim();
        }

        public void TransformReplaceRegex(int col, string pattern, string replacement)
        {
            var rx = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Compiled);
            foreach (var r in Rows)
                if (col < r.Length) r[col] = rx.Replace(r[col] ?? "", replacement ?? "");
        }

        public void TransformLeft(int col, int n)
        {
            foreach (var r in Rows)
                if (col < r.Length)
                {
                    var s = r[col] ?? "";
                    n = Math.Clamp(n, 0, s.Length);
                    r[col] = s.Substring(0, n);
                }
        }

        public void TransformRight(int col, int n)
        {
            foreach (var r in Rows)
                if (col < r.Length)
                {
                    var s = r[col] ?? "";
                    n = Math.Clamp(n, 0, s.Length);
                    r[col] = s.Substring(s.Length - n, n);
                }
        }

        public void TransformCutLastWords(int col, int wordCount)
        {
            foreach (var r in Rows)
                if (col < r.Length)
                    r[col] = CutLastWords(r[col] ?? "", wordCount).rest;
        }
        // --- NEW: casing transforms ---
        public void TransformToUpper(int col, string? cultureName = null)
        {
            var ci = !string.IsNullOrWhiteSpace(cultureName) ? new CultureInfo(cultureName) : CultureInfo.CurrentCulture;
            foreach (var r in Rows)
                if (col < r.Length) r[col] = (r[col] ?? "").ToUpper(ci);
        }

        public void TransformToLower(int col, string? cultureName = null)
        {
            var ci = !string.IsNullOrWhiteSpace(cultureName) ? new CultureInfo(cultureName) : CultureInfo.CurrentCulture;
            foreach (var r in Rows)
                if (col < r.Length) r[col] = (r[col] ?? "").ToLower(ci);
        }

        /// <summary>TitleCase per CultureInfo (optionally lower the string first).</summary>
        public void TransformToTitleCase(int col, bool forceLowerFirst = true, string? cultureName = null)
        {
            var ci = !string.IsNullOrWhiteSpace(cultureName) ? new CultureInfo(cultureName) : CultureInfo.CurrentCulture;
            var ti = ci.TextInfo;

            foreach (var r in Rows)
            {
                if (col >= r.Length) continue;
                var s = r[col] ?? "";
                if (forceLowerFirst) s = s.ToLower(ci);
                r[col] = ti.ToTitleCase(s);
            }
        }


        public void FillEmptyFromPrevious(int col)
        {
            string last = "";
            for (int i = 0; i < Rows.Count; i++)
            {
                if (col >= Rows[i].Length) continue;
                string val = Rows[i][col] ?? "";
                if (string.IsNullOrWhiteSpace(val))
                {
                    if (!string.IsNullOrEmpty(last))
                        Rows[i][col] = last;
                }
                else
                {
                    last = val;
                }
            }
        }

        public void FillEmptyFromNext(int col)
        {
            string next = "";
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                if (col >= Rows[i].Length) continue;
                string val = Rows[i][col] ?? "";
                if (string.IsNullOrWhiteSpace(val))
                {
                    if (!string.IsNullOrEmpty(next))
                        Rows[i][col] = next;
                }
                else
                {
                    next = val;
                }
            }
        }

        public void FillEmptyWithRowIndex(int col, int startIndex)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (col >= Rows[i].Length) continue;
                var current = Rows[i][col] ?? "";
                if (string.IsNullOrWhiteSpace(current))
                    Rows[i][col] = (startIndex + i).ToString();
            }
        }

        public void FillEmptyWithStaticValue(int col, string value)
        {
            string v = value ?? "";
            for (int i = 0; i < Rows.Count; i++)
            {
                if (col >= Rows[i].Length) continue;
                var current = Rows[i][col] ?? "";
                if (string.IsNullOrWhiteSpace(current))
                    Rows[i][col] = v;
            }
        }
    }
}
