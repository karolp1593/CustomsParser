using System.Text.Json;
using System.Text.RegularExpressions;

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

        // Base version (kept)
        public static Dictionary<string, object> RunAllRulesToJsonValues(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log)
            => RunAllRulesToJsonValues(cfg, freshTableFactory, log, excludeRules: null);

        // New overload with exclude list
        public static Dictionary<string, object> RunAllRulesToJsonValues(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log,
            IEnumerable<string>? excludeRules)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var exclude = new HashSet<string>(
                (excludeRules ?? Array.Empty<string>()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var rule in cfg.Rules)
            {
                if (exclude.Contains(rule.Name))
                {
                    log($"[skip] Rule '{rule.Name}' excluded by router.");
                    continue;
                }

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

        // ---------- Multi-Parser router support ----------

        public static bool TryEvaluateTag(
            ParserConfig cfg,
            string tagRuleName,
            Func<Table> freshTableFactory,
            Action<string> log,
            out string tagValue)
        {
            tagValue = "";
            var tagRule = cfg.Rules.FirstOrDefault(r => r.Name.Equals(tagRuleName, StringComparison.OrdinalIgnoreCase));
            if (tagRule == null)
            {
                log($"Router: Tag rule '{tagRuleName}' not found in parser '{cfg.Name}'.");
                return false;
            }

            var t = freshTableFactory();
            int step = 0;
            foreach (var s in tagRule.RuleSteps)
            {
                if (!s.Enabled) continue;
                step++;
                ApplySingleStep(s, t, cfg.MissingPolicy, _ => { });
            }

            if (t.IsScalar)
            {
                tagValue = (t.ScalarValue ?? "").Trim();
                log($"Router: Tag value = \"{tagValue}\"");
                return true;
            }

            // Fallback: first cell if they forgot to convert to scalar
            if (t.Rows.Count > 0 && t.ColumnCount > 0)
            {
                tagValue = (t.Rows[0][0] ?? "").Trim();
                log($"Router: Tag fallback from [0,0] = \"{tagValue}\"");
                return true;
            }

            log("Router: Unable to compute Tag (no scalar and table is empty).");
            return false;
        }

        // NEW: resolve a target parser, then RUN ALL RULES of that parser and return aggregated JSON
        public static bool TryRunWithRouting(
            ParserConfig parentParser,
            Func<Table> freshTableFactory,
            Action<string> log,
            out string? targetParserName,
            out Dictionary<string, object>? aggregatedJson)
        {
            targetParserName = null;
            aggregatedJson = null;

            var router = ParserStore.LoadRouter(parentParser.Name);
            if (router.Routes.Count == 0 && string.IsNullOrWhiteSpace(router.DefaultTargetParser))
            {
                log($"Router: no routes configured for parser '{parentParser.Name}'.");
                return false;
            }

            if (!TryEvaluateTag(parentParser, router.TagRuleName, freshTableFactory, log, out var tag))
                return false;

            // find match
            RouteRule? winner = null;
            foreach (var r in router.Routes)
            {
                bool match = r.Kind switch
                {
                    RouteMatchKind.Exact => string.Equals(
                        tag,
                        r.Pattern,
                        r.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture),
                    RouteMatchKind.Regex => Regex.IsMatch(
                        tag ?? "",
                        r.Pattern ?? "",
                        r.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None),
                    _ => false
                };
                if (match) { winner = r; break; }
            }

            string? targetParser = winner?.TargetParser ?? router.DefaultTargetParser;

            if (winner != null)
                log($"Router: matched {winner.Kind} '{winner.Pattern}' -> {targetParser}");
            else if (!string.IsNullOrWhiteSpace(targetParser))
                log($"Router: no rule matched; using DEFAULT -> {targetParser}");
            else
            {
                log("Router: no route matched and no default specified.");
                return false;
            }

            var targetCfg = ParserStore.Load(targetParser!);
            targetParserName = targetCfg.Name;

            // Run ALL rules of the target parser, optionally excluding some
            aggregatedJson = RunAllRulesToJsonValues(
                targetCfg,
                freshTableFactory: freshTableFactory,
                log: m => log($"[routed] {m}"),
                excludeRules: router.ExcludeRules
            );
            return true;
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
            TransformRightStep trr => $"Right {trr.N} chars",
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
            SplitAfterWordsStep saw => $"Split after {saw.W} word(s)",
            SplitCutLastCharsToNewColumnStep scl => $"Cut last {scl.N} char(s) → new column",
            KeepColumnsStep kc => $"Keep {kc.Keep.Count} selected column(s)",
            DropFirstRowStep => "Drop first row",
            RenameColumnsStep => "Rename columns",
            ToScalarFromCellStep sc => $"To scalar from cell (row={sc.Row}, rx set={!string.IsNullOrWhiteSpace(sc.Pattern)})",
            InsertBlankColumnStep ib => $"Insert blank column at {ib.InsertIndex}",
            CopyColumnStep cc => $"Copy column (src={cc.Source}, destIndex={(cc.CreateNewDestination ? "new" : cc.DestinationIndex.ToString())}, append={cc.Append})",
            MergeRowsByGroupStep mg => $"Merge rows by group (start=/{mg.StartPattern}/ end=/{mg.EndPattern}/ strat={mg.Strategy})",
            RegexExtractStep rx => $"Regex extract (/{rx.Pattern}/ all={rx.AllMatches} inPlace={rx.InPlace})",
            _ => s.GetType().Name
        };
    }
}
