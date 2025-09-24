using iText.Layout.Element;

namespace PdfTableMvp.Core
{
    public class KeepColumnsStep : StepBase
    {
        public List<ColumnSelector> Keep { get; set; } = new();
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            var idx = Keep.Select(k => k.ResolveIndex(t)).Where(i => i >= 0).ToArray();
            if (idx.Length == 0) { log("KeepColumns: no valid columns."); return; }
            t.KeepColumns(idx);
            log($"KeepColumns -> {idx.Length} col(s)");
        }
    }

    public class DropFirstRowStep : StepBase
    {
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            t.DropFirstRow();
            log("DropFirstRow");
        }
    }

    public class RenameColumnsStep : StepBase
    {
        public List<string> Names { get; set; } = new();
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            t.RenameColumns(Names);
            log("RenameColumns");
        }
    }

    public class InsertBlankColumnStep : StepBase
    {
        public int InsertIndex { get; set; } = 0;
        public string? ColumnName { get; set; }
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int pos = t.InsertBlankColumn(InsertIndex, ColumnName);
            log($"InsertBlankColumn at {pos} name=\"{ColumnName}\"");
        }
    }

    public class CopyColumnStep : StepBase
    {
        public ColumnSelector Source { get; set; } = ColumnSelector.ByIndex(0);
        public bool CreateNewDestination { get; set; } = false;
        public int DestinationIndex { get; set; } = 0;
        public string NewColumnName { get; set; } = "";

        public bool Append { get; set; } = false;
        public bool Overwrite { get; set; } = true;
        public string Separator { get; set; } = " ";
        public bool OnlyWhenSourceNonEmpty { get; set; } = false;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int src = ResolveOrPolicy(t, Source, policy, log);
            if (src < 0) return;

            int destIndex = DestinationIndex;

            if (CreateNewDestination)
            {
                destIndex = t.InsertBlankColumn(DestinationIndex, string.IsNullOrWhiteSpace(NewColumnName) ? null : NewColumnName);
            }

            t.CopyColumn(src, destIndex, overwrite: Overwrite, append: Append, separator: Separator, onlyWhenSrcNonEmpty: OnlyWhenSourceNonEmpty);
            log($"CopyColumn src={src} dest={destIndex} append={Append} overwrite={Overwrite}");
        }
    }

    public class MergeRowsByGroupStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string StartPattern { get; set; } = "";
        public string? EndPattern { get; set; }
        public bool CaseInsensitive { get; set; } = false;
        public bool ResetPerPage { get; set; } = false;
        public MergeJoinStrategy Strategy { get; set; } = MergeJoinStrategy.ConcatSpace;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            int groups = t.MergeRowsByGroup(col, StartPattern, EndPattern, CaseInsensitive, ResetPerPage, Strategy);
            log($"MergeRowsByGroup col={col} start=/{StartPattern}/ end=/{EndPattern}/ strat={Strategy} => {groups} group(s)");
        }
    }
}
