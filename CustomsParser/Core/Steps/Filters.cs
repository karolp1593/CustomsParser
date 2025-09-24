using iText.Layout.Element;
using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public class KeepTableSectionStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string StartRegex { get; set; } = "";
        public string EndRegex { get; set; } = "";
        public bool IncludeStart { get; set; } = true;
        public bool IncludeEnd { get; set; } = true;
        public bool CaseInsensitive { get; set; } = false;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.KeepTableSectionByRegexPerPage(col, StartRegex, EndRegex, IncludeStart, IncludeEnd, CaseInsensitive);
            log($"KeepTableSection col={col} start=/{StartRegex}/ end=/{EndRegex}/ ci={CaseInsensitive}");
        }
        public override string Describe() => $"KeepTableSection({Col})";
    }

    public class KeepRowsWhereRegexStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Regex { get; set; } = ".*";
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.KeepRowsWhereRegex(col, Regex);
            log($"KeepRowsWhereRegex col={col} rx=/{Regex}/");
        }
    }

    public class KeepRowsWhereNotEmptyStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.KeepRowsWhereNotEmpty(col);
            log($"KeepRowsWhereNotEmpty col={col}");
        }
    }
}
