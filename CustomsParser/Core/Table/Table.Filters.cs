using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public int KeepTableSectionByRegexPerPage(
            int col, string startPattern, string endPattern,
            bool includeStart, bool includeEnd, bool caseInsensitive)
        {
            var options = RegexOptions.Compiled | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            Regex? rxStart = string.IsNullOrWhiteSpace(startPattern) ? null : new Regex(startPattern, options);
            Regex? rxEnd = string.IsNullOrWhiteSpace(endPattern) ? null : new Regex(endPattern, options);

            var keepMask = new bool[Rows.Count];
            int? currentPage = null;
            bool inSeg = false;

            for (int i = 0; i < Rows.Count; i++)
            {
                int page = (i < RowPages.Count) ? RowPages[i] : 1;
                if (currentPage == null || page != currentPage)
                {
                    inSeg = false;
                    currentPage = page;
                }

                string cell = SafeCell(i, col);
                bool isStart = rxStart != null && rxStart.IsMatch(cell);
                bool isEnd = rxEnd != null && rxEnd.IsMatch(cell);

                if (!inSeg)
                {
                    if (isStart)
                    {
                        if (includeStart) keepMask[i] = true;
                        inSeg = true;
                    }
                }
                else
                {
                    if (isEnd)
                    {
                        if (includeEnd) keepMask[i] = true;
                        inSeg = false;
                    }
                    else
                    {
                        keepMask[i] = true;
                    }
                }
            }

            var newRows = new List<string[]>();
            var newPages = new List<int>();
            for (int i = 0; i < Rows.Count; i++)
                if (keepMask[i]) { newRows.Add(Rows[i]); newPages.Add(RowPages[i]); }
            Rows = newRows; RowPages = newPages;
            return Rows.Count;
        }

        public void KeepRowsWhereRegex(int col, string pattern)
        {
            var rx = new Regex(pattern, RegexOptions.Compiled);
            var newRows = new List<string[]>();
            var newPages = new List<int>();
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                if (col < r.Length && rx.IsMatch(r[col] ?? string.Empty))
                {
                    newRows.Add(r);
                    newPages.Add(RowPages[i]);
                }
            }
            Rows = newRows; RowPages = newPages;
        }

        public void KeepRowsWhereNotEmpty(int col)
        {
            var newRows = new List<string[]>();
            var newPages = new List<int>();
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                if (col < r.Length && !string.IsNullOrWhiteSpace(r[col]))
                {
                    newRows.Add(r);
                    newPages.Add(RowPages[i]);
                }
            }
            Rows = newRows; RowPages = newPages;
        }
    }
}
