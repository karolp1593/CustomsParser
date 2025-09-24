using iText.Layout.Element;

namespace PdfTableMvp.Core
{
    public class TrimAllStep : StepBase
    {
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            t.TrimAll();
            log("TrimAll");
        }
    }

    public class TransformTrimStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformTrim(col);
            log($"TransformTrim col={col}");
        }
    }

    public class TransformReplaceRegexStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Pattern { get; set; } = "";
        public string Replacement { get; set; } = "";
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformReplaceRegex(col, Pattern, Replacement);
            log($"Replace /{Pattern}/ -> \"{Replacement}\" col={col}");
        }
    }

    public class TransformLeftStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int N { get; set; } = 0;
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformLeft(col, N);
            log($"Left {N} col={col}");
        }
    }

    public class TransformRightStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int N { get; set; } = 0;
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformRight(col, N);
            log($"Right {N} col={col}");
        }
    }

    public class TransformCutLastWordsStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int W { get; set; } = 1;
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformCutLastWords(col, W);
            log($"CutLastWords W={W} col={col}");
        }
    }

    public class FillEmptyStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public FillDirection Direction { get; set; } = FillDirection.Previous;
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            if (Direction == FillDirection.Previous) t.FillEmptyFromPrevious(col);
            else t.FillEmptyFromNext(col);
            log($"FillEmpty direction={Direction} col={col}");
        }
    }

    public class FillEmptyWithRowIndexStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int StartIndex { get; set; } = 0;
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.FillEmptyWithRowIndex(col, StartIndex);
            log($"FillEmptyWithRowIndex col={col} start={StartIndex}");
        }
    }
    public class TransformToUpperStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string? Culture { get; set; }  // e.g. "en-US" (blank = current)

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformToUpper(col, Culture);
            log($"ToUpper col={col} culture=\"{Culture}\"");
        }
    }

    public class TransformToLowerStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string? Culture { get; set; }  // e.g. "en-US" (blank = current)

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformToLower(col, Culture);
            log($"ToLower col={col} culture=\"{Culture}\"");
        }
    }

    public class TransformToTitleCaseStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public bool ForceLowerFirst { get; set; } = true;
        public string? Culture { get; set; }  // e.g. "en-US" (blank = current)

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.TransformToTitleCase(col, ForceLowerFirst, Culture);
            log($"ToTitleCase col={col} lowerFirst={ForceLowerFirst} culture=\"{Culture}\"");
        }
    }


    public class FillEmptyWithStaticValueStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Value { get; set; } = "";
        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.FillEmptyWithStaticValue(col, Value ?? "");
            log($"FillEmptyWithStaticValue col={col} value=\"{Value}\"");
        }
    }
}
