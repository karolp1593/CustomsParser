using iText.Layout.Element;
using System.Text.Json;

namespace PdfTableMvp.Core
{
    public static class ParserRunner
    {
        public static void RunRule(ParserConfig cfg, string ruleName, Table t, Action<string> log)
        {
            var rule = cfg.Rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
            if (rule == null) throw new Exception($"Rule '{ruleName}' not found in parser '{cfg.Name}'.");

            int totalEnabled = rule.RuleSteps.Count(s => s.Enabled);
            log($"Running {cfg.Name} › {rule.Name}: {totalEnabled} step(s). Policy={cfg.MissingPolicy}");

            int step = 0;
            foreach (var s in rule.RuleSteps)
            {
                if (!s.Enabled) { log($"(skipped) {FriendlyStepName(s)}"); continue; }
                step++;
                log($"[{step}/{totalEnabled}] {FriendlyStepName(s)}");
                ApplySingleStep(s, t, cfg.MissingPolicy, m => log($"    {m}"));
            }
        }

        public static void ApplySingleStep(StepBase step, Table t, MissingColumnPolicy policy, Action<string> log)
            => step.Apply(t, policy, log);

        public static Dictionary<string, object> RunAllRulesToJsonValues(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in cfg.Rules)
            {
                var table = freshTableFactory();
                log($"--- Running Rule: {rule.Name} ---");

                int totalEnabled = rule.RuleSteps.Count(s => s.Enabled);
                int step = 0;
                foreach (var s in rule.RuleSteps)
                {
                    if (!s.Enabled) { log($"(skipped) {FriendlyStepName(s)}"); continue; }
                    step++;
                    log($"[{step}/{totalEnabled}] {FriendlyStepName(s)}");
                    ApplySingleStep(s, table, cfg.MissingPolicy, msg => log($"    {msg}"));
                }

                if (table.IsScalar)
                {
                    result[rule.Name] = table.ScalarValue ?? "";
                    log($"Rule '{rule.Name}' produced a scalar value.");
                }
                else
                {
                    var objects = table.ToObjects();
                    result[rule.Name] = objects;
                    log($"Rule '{rule.Name}' produced {objects.Count} row(s) with {table.ColumnCount} column(s).");
                }
            }

            return result;
        }

        public static void ExportAllRulesToOneJson(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            string outputPath,
            Action<string> log)
        {
            var all = RunAllRulesToJsonValues(cfg, freshTableFactory, log);
            var json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);
        }

        static string FriendlyStepName(StepBase s) => s switch
        {
            KeepTableSectionStep k => $"Keep table section (start=/{k.StartRegex}/ end=/{k.EndRegex}/ ci={k.CaseInsensitive})",
            KeepRowsWhereRegexStep k => $"Keep rows where /{k.Regex}/",
            KeepRowsWhereNotEmptyStep => "Keep rows where not empty",
            TrimAllStep => "Trim all cells",
            TransformTrimStep => "Trim column",
            TransformReplaceRegexStep tr => $"Replace regex /{tr.Pattern}/ → \"{tr.Replacement}\"",
            TransformLeftStep tl => $"Left {tl.N} chars",
            TransformRightStep tr => $"Right {tr.N} chars",
            TransformCutLastWordsStep cw => $"Cut last {cw.W} word(s) (in-place)",
            FillEmptyStep fe => $"Fill empty from {fe.Direction}",
            FillEmptyWithRowIndexStep fri => $"Fill empty with row index start={fri.StartIndex}",
            FillEmptyWithStaticValueStep fsv => $"Fill empty with static \"{fsv.Value}\"",
            TransformToUpperStep => "To UPPERCASE",
            TransformToLowerStep => "To lowercase",
            TransformToTitleCaseStep tc => $"To Title Case (lowerFirst={tc.ForceLowerFirst})",
            SplitOnKeywordStep sk => $"Split on keyword \"{sk.Keyword}\" (all={sk.AllOccurrences}, ci={sk.CaseInsensitive})",
            SplitAfterCharsStep sa => $"Split after {sa.N} char(s)",
            SplitCutLastWordsToNewColumnStep sw => $"Cut last {sw.W} word(s) → new column",
            SplitOnRegexDelimiterStep sd => $"Split on regex /{sd.Pattern}/ (ci={sd.CaseInsensitive})",
            KeepColumnsStep kc => $"Keep {kc.Keep.Count} selected column(s)",
            DropFirstRowStep => "Drop first row",
            RenameColumnsStep => "Rename columns",
            ToScalarFromCellStep sc => $"To scalar from cell (row={sc.Row}, rx set={!string.IsNullOrWhiteSpace(sc.Pattern)})",
            InsertBlankColumnStep ib => $"Insert blank column at {ib.InsertIndex}",
            CopyColumnStep cc => $"Copy column (src={cc.Source}, destIndex={(cc.CreateNewDestination ? "new" : cc.DestinationIndex)}, append={cc.Append})",
            MergeRowsByGroupStep mg => $"Merge rows by group (start=/{mg.StartPattern}/ end=/{mg.EndPattern}/ strat={mg.Strategy})",
            RegexExtractStep rx => $"Regex extract (/{rx.Pattern}/ all={rx.AllMatches} inPlace={rx.InPlace})",
            _ => s.GetType().Name
        };
    }
}
