using iText.Layout.Element;

namespace PdfTableMvp.Core
{
    public class ToScalarFromCellStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int Row { get; set; } = 0;
        public string? Pattern { get; set; }
        public int Group { get; set; } = 1;
        public bool RequireSingleCell { get; set; } = false;
        public bool Trim { get; set; } = true;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.ToScalarFromCell(col, Row, Pattern, Group, RequireSingleCell, Trim);
            log($"ToScalarFromCell col={col} row={Row} rx set={(Pattern is not null)}");
        }
    }

    public class RegexExtractStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Pattern { get; set; } = "";
        public bool CaseInsensitive { get; set; } = false;
        public int Group { get; set; } = 1;
        public bool AllMatches { get; set; } = false;
        public bool InPlace { get; set; } = false;
        public bool ExpandToMultipleColumns { get; set; } = false;
        public string? NewColumnName { get; set; }
        public string JoinSeparator { get; set; } = ", ";

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;

            t.RegexExtract(col, Pattern, CaseInsensitive, Group, AllMatches, InPlace,
                ExpandToMultipleColumns, NewColumnName, JoinSeparator);

            log($"RegexExtract col={col} rx=/{Pattern}/ all={AllMatches} inPlace={InPlace} expand={ExpandToMultipleColumns}");
        }
    }
}
