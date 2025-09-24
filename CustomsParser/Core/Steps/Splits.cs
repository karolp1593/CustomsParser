using System;

namespace PdfTableMvp.Core
{
    public class SplitOnKeywordStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Keyword { get; set; } = "";
        public bool CaseInsensitive { get; set; } = false;
        public bool AllOccurrences { get; set; } = false;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitOnKeyword(col, Keyword, CaseInsensitive, AllOccurrences);
            log($"SplitOnKeyword col={col} kw=\"{Keyword}\" all={AllOccurrences} ci={CaseInsensitive}");
        }

        public override string Describe() =>
            $"SplitOnKeyword col={Col} kw=\"{Keyword}\" all={AllOccurrences} ci={CaseInsensitive}";
    }

    public class SplitAfterCharsStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int N { get; set; } = 1;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitAfterChars(col, N);
            log($"SplitAfterChars col={col} n={N}");
        }

        public override string Describe() => $"SplitAfterChars col={Col}, n={N}";
    }

    public class SplitCutLastWordsToNewColumnStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int W { get; set; } = 1;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitCutLastWordsToNewColumn(col, W);
            log($"SplitCutLastWordsToNewColumn col={col} w={W}");
        }

        public override string Describe() => $"SplitCutLastWordsToNewColumn col={Col}, w={W}";
    }

    public class SplitOnRegexDelimiterStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public string Pattern { get; set; } = "";
        public bool CaseInsensitive { get; set; } = false;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitOnRegexDelimiter(col, Pattern, CaseInsensitive);
            log($"SplitOnRegexDelimiter col={col} rx=/{Pattern}/ ci={CaseInsensitive}");
        }

        public override string Describe() => $"SplitOnRegexDelimiter col={Col}, rx=/{Pattern}/, ci={CaseInsensitive}";
    }

    // ===== NEW =====

    public class SplitAfterWordsStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int W { get; set; } = 1;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitAfterWords(col, W);
            log($"SplitAfterWords col={col} w={W}");
        }

        public override string Describe() => $"SplitAfterWords col={Col}, w={W}";
    }

    public class SplitCutLastCharsToNewColumnStep : StepBase
    {
        public ColumnSelector Col { get; set; } = ColumnSelector.ByIndex(0);
        public int N { get; set; } = 1;

        public override void Apply(Table t, MissingColumnPolicy policy, Action<string> log)
        {
            int col = ResolveOrPolicy(t, Col, policy, log);
            if (col < 0) return;
            t.SplitCutLastCharsToNewColumn(col, N);
            log($"SplitCutLastCharsToNewColumn col={col} n={N}");
        }

        public override string Describe() => $"SplitCutLastCharsToNewColumn col={Col}, n={N}";
    }
}
