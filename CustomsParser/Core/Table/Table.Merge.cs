using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        class Aggregator
        {
            public List<string> Values = new();
            public Aggregator(int cols)
            {
                for (int i = 0; i < cols; i++) Values.Add("");
            }
        }

        Aggregator InitAggregator(MergeJoinStrategy strat) => new Aggregator(ColumnCount);

        void MergeRowIntoAggregator(Aggregator a, string[] row, MergeJoinStrategy strat)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                string existing = a.Values[c] ?? "";
                string incoming = c < row.Length ? (row[c] ?? "") : "";

                switch (strat)
                {
                    case MergeJoinStrategy.ConcatSpace:
                        if (string.IsNullOrEmpty(existing)) a.Values[c] = incoming;
                        else if (!string.IsNullOrEmpty(incoming)) a.Values[c] = existing + " " + incoming;
                        break;

                    case MergeJoinStrategy.ConcatNewline:
                        if (string.IsNullOrEmpty(existing)) a.Values[c] = incoming;
                        else if (!string.IsNullOrEmpty(incoming)) a.Values[c] = existing + "\n" + incoming;
                        break;

                    case MergeJoinStrategy.FirstNonEmpty:
                        if (string.IsNullOrEmpty(existing) && !string.IsNullOrEmpty(incoming)) a.Values[c] = incoming;
                        break;

                    case MergeJoinStrategy.LastNonEmpty:
                        if (!string.IsNullOrEmpty(incoming)) a.Values[c] = incoming;
                        break;
                }
            }
        }

        string[] FinalizeAggregator(Aggregator a) => a.Values.ToArray();

        public int MergeRowsByGroup(
            int col, string startPattern, string? endPattern,
            bool caseInsensitive, bool resetPerPage, MergeJoinStrategy strategy)
        {
            var opts = RegexOptions.Compiled | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            var rxStart = new Regex(startPattern, opts);
            Regex? rxEnd = string.IsNullOrWhiteSpace(endPattern) ? null : new Regex(endPattern, opts);

            var newRows = new List<string[]>();
            var newPages = new List<int>();

            int i = 0;
            int currentPage = RowPages.Count > 0 ? RowPages[0] : 1;

            while (i < Rows.Count)
            {
                if (resetPerPage && RowPages[i] != currentPage)
                    currentPage = RowPages[i];

                while (i < Rows.Count && !rxStart.IsMatch(SafeCell(i, col)))
                {
                    if (resetPerPage && RowPages[i] != currentPage) currentPage = RowPages[i];
                    i++;
                }
                if (i >= Rows.Count) break;

                int groupStartIndex = i;
                int groupPage = RowPages[groupStartIndex];
                var agg = InitAggregator(strategy);

                while (i < Rows.Count)
                {
                    if (resetPerPage && RowPages[i] != currentPage) break;

                    string cell = SafeCell(i, col);
                    bool isEnd = rxEnd != null && rxEnd.IsMatch(cell);

                    MergeRowIntoAggregator(agg, Rows[i], strategy);
                    i++;

                    if (isEnd) break;
                    if (rxEnd == null && i < Rows.Count && rxStart.IsMatch(SafeCell(i, col))) break;
                }

                newRows.Add(FinalizeAggregator(agg));
                newPages.Add(groupPage);
            }

            Rows = newRows; RowPages = newPages;
            return Rows.Count;
        }
    }
}
