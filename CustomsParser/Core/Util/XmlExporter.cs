using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PdfTableMvp.Core
{
    public static class XmlExporter
    {
        // ===== Public entry point used by Program.cs (option 17) =====
        public static void ExportParserOrRoutedToXml(
            ParserConfig parentParser,
            Func<Table> freshTableFactory,
            string outputPath,
            Action<string> log)
        {
            XDocument doc;

            // If no router configured — export single parser
            if (!ParserStore.RouterExists(parentParser.Name))
            {
                doc = BuildSingleParserDocument(parentParser, freshTableFactory, log);
                doc.Save(outputPath);
                return;
            }

            // Router exists: evaluate tag, pick target, then export combined
            var router = ParserStore.LoadRouter(parentParser.Name);

            string? tagValue = null;
            bool tagOk = ParserRunner.TryEvaluateTag(parentParser, router.TagRuleName, freshTableFactory, log, out var tv);
            if (tagOk) tagValue = tv;

            // Pick target parser (match Exact/Regex or fallback to default)
            string? targetParserName = null;
            if (tagOk)
            {
                RouteRule? winner = null;
                foreach (var r in router.Routes)
                {
                    bool match = r.Kind == RouteMatchKind.Exact
                        ? string.Equals(tagValue, r.Pattern,
                            r.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture)
                        : System.Text.RegularExpressions.Regex.IsMatch(tagValue ?? "", r.Pattern ?? "",
                            r.CaseInsensitive ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None);
                    if (match) { winner = r; break; }
                }
                targetParserName = winner?.TargetParser ?? router.DefaultTargetParser;
                if (winner != null)
                    log($"Router: matched {winner.Kind} '{winner.Pattern}' -> {targetParserName}");
                else if (!string.IsNullOrWhiteSpace(targetParserName))
                    log($"Router: no rule matched; using DEFAULT -> {targetParserName}");
            }
            else
            {
                // No tag — fallback to default if present
                targetParserName = router.DefaultTargetParser;
            }

            if (string.IsNullOrWhiteSpace(targetParserName))
            {
                log("Router: no route matched and no default specified — exporting PARENT only.");
                doc = BuildSingleParserDocument(parentParser, freshTableFactory, log, tagRuleName: router.TagRuleName, tagValue: tagValue);
                doc.Save(outputPath);
                return;
            }

            var targetCfg = ParserStore.Load(targetParserName!);

            // Build combined doc
            doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("Result",
                new XAttribute("ranAt", DateTime.UtcNow.ToString("o")));
            doc.Add(root);

            // ParserInfo
            root.Add(BuildParserInfo(
                parser: parentParser.Name,
                subparser: targetCfg.Name,
                routed: true,
                tagRuleName: router.TagRuleName,
                tagValue: tagValue));

            // HeaderInfo (scalars from parent + target). Resolve duplicates.
            var header = new XElement("HeaderInfo");
            root.Add(header);

            // Run parent rules
            var parentRuns = RunAllRulesToTables(parentParser, freshTableFactory, msg => log($"[parent] {msg}"));

            // Run target rules (optionally exclude)
            ISet<string>? exclude = null;
            try
            {
                // If you added RouterConfig.ExcludeRules, use it. Otherwise this remains null.
                // ReSharper disable once ConstantConditionalAccessQualifier
                if (router?.ExcludeRules != null && router.ExcludeRules.Count > 0)
                    exclude = new HashSet<string>(router.ExcludeRules, StringComparer.OrdinalIgnoreCase);
            }
            catch { /* property may not exist in older RouterConfig; ignore */ }

            var targetRuns = RunAllRulesToTables(targetCfg, freshTableFactory, msg => log($"[target] {msg}"), exclude);

            // 1) Scalars to HeaderInfo (unique attribute names)
            AppendScalars(header, parentRuns);
            AppendScalars(header, targetRuns);

            // 2) Table rules as elements (unique element names)
            AppendTables(root, parentRuns);
            AppendTables(root, targetRuns);

            // Save
            doc.Save(outputPath);
        }

        // ===== Single parser export (no routing) =====
        private static XDocument BuildSingleParserDocument(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log,
            string? tagRuleName = null,
            string? tagValue = null)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("Result",
                new XAttribute("ranAt", DateTime.UtcNow.ToString("o")));
            doc.Add(root);

            // ParserInfo (no subparser)
            root.Add(BuildParserInfo(cfg.Name, subparser: null, routed: false, tagRuleName: tagRuleName, tagValue: tagValue));

            // HeaderInfo + table rules
            var header = new XElement("HeaderInfo");
            root.Add(header);

            var runs = RunAllRulesToTables(cfg, freshTableFactory, log);
            AppendScalars(header, runs);
            AppendTables(root, runs);

            return doc;
        }

        // ===== Helpers to run rules and gather tables =====
        private static IEnumerable<(RuleDefinition rule, Table table)> RunAllRulesToTables(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log,
            ISet<string>? excludeRules = null)
        {
            foreach (var rule in cfg.Rules)
            {
                if (excludeRules != null && excludeRules.Contains(rule.Name))
                {
                    log($"[skip] Rule '{rule.Name}' excluded.");
                    continue;
                }

                var table = freshTableFactory();
                log($"--- Running Rule: {rule.Name} ---");

                int totalEnabled = rule.RuleSteps.Count(s => s.Enabled);
                int step = 0;
                foreach (var s in rule.RuleSteps)
                {
                    if (!s.Enabled) { log($"(skipped) {s.Describe()}"); continue; }
                    step++;
                    ParserRunner.ApplySingleStep(s, table, cfg.MissingPolicy, msg => log($"    {msg}"));
                }

                yield return (rule, table);
            }
        }

        // ===== Build <HeaderInfo .../> from scalar rules =====
        private static void AppendScalars(XElement headerInfo, IEnumerable<(RuleDefinition rule, Table table)> runs)
        {
            foreach (var (rule, table) in runs)
            {
                if (!table.IsScalar) continue;

                var baseName = XmlNameHelper.MakeAttributeName(rule.Name);
                var unique = UniqueAttributeName(headerInfo, baseName);
                headerInfo.SetAttributeValue(unique, table.ScalarValue ?? "");
            }
        }

        // ===== Build per-table-rule elements with <Row/> children =====
        private static void AppendTables(XElement root, IEnumerable<(RuleDefinition rule, Table table)> runs)
        {
            foreach (var (rule, table) in runs)
            {
                if (table.IsScalar) continue;

                var desired = XmlNameHelper.MakeElementName(rule.Name);
                var uniqueLocal = UniqueChildElementLocalName(root, desired);

                // Build and (if needed) rename to unique name
                var elem = BuildTableElement(rule.Name, table);
                if (!string.Equals(elem.Name.LocalName, uniqueLocal, StringComparison.Ordinal))
                    elem.Name = uniqueLocal;

                root.Add(elem);
            }
        }

        // ===== Existing helpers kept from your earlier file =====
        public static XDocument TableToResultDocument(Table t, string elementName = "Current")
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("Result",
                new XAttribute("ranAt", DateTime.UtcNow.ToString("o")));
            doc.Add(root);

            var tableElem = BuildTableElement(elementName, t);
            root.Add(tableElem);

            return doc;
        }

        public static XElement BuildParserInfo(string parser, string? subparser, bool routed, string? tagRuleName, string? tagValue)
        {
            var info = new XElement("ParserInfo");
            info.SetAttributeValue("parser", parser ?? "");
            if (!string.IsNullOrWhiteSpace(subparser)) info.SetAttributeValue("subparser", subparser);
            info.SetAttributeValue("routed", routed);
            if (!string.IsNullOrWhiteSpace(tagRuleName)) info.SetAttributeValue("tagRule", tagRuleName);
            if (!string.IsNullOrWhiteSpace(tagValue)) info.SetAttributeValue("tagValue", tagValue);
            return info;
        }

        public static XElement BuildHeaderInfo(IDictionary<string, string> scalars)
        {
            var header = new XElement("HeaderInfo");
            foreach (var kv in scalars)
            {
                var attrName = XmlNameHelper.MakeAttributeName(kv.Key);
                header.SetAttributeValue(attrName, kv.Value ?? "");
            }
            return header;
        }

        public static XElement BuildTableElement(string ruleName, Table t)
        {
            string elemName = XmlNameHelper.MakeElementName(ruleName);
            var rule = new XElement(elemName);
            rule.SetAttributeValue("rowCount", t.Rows.Count);

            var attrNames = PrepareUniqueAttributeNames(t.ColumnNames);

            for (int i = 0; i < t.Rows.Count; i++)
            {
                var row = new XElement("Row");
                row.SetAttributeValue("i", i);
                if (i < t.RowPages.Count) row.SetAttributeValue("page", t.RowPages[i]);

                var cells = t.Rows[i];
                for (int c = 0; c < attrNames.Count; c++)
                {
                    string attrName = attrNames[c];
                    string val = c < cells.Length ? (cells[c] ?? "") : "";
                    row.SetAttributeValue(attrName, val);
                }

                rule.Add(row);
            }

            return rule;
        }

        public static List<string> PrepareUniqueAttributeNames(IList<string> columnNames)
        {
            var result = new List<string>(columnNames.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columnNames.Count; i++)
            {
                string baseCandidate = string.IsNullOrWhiteSpace(columnNames[i]) ? $"Col{i}" : columnNames[i];
                string candidate = XmlNameHelper.MakeAttributeName(baseCandidate);
                string orig = candidate;
                int k = 2;
                while (!seen.Add(candidate)) candidate = orig + "_" + k++;
                result.Add(candidate);
            }

            return result;
        }

        // ===== Local name uniqueness helpers =====
        private static string UniqueAttributeName(XElement element, string baseName)
        {
            // Attributes are case-sensitive by XML rules; use exact local name matching here.
            if (element.Attribute(baseName) == null) return baseName;
            int k = 2;
            while (element.Attribute(baseName + "_" + k) != null) k++;
            return baseName + "_" + k;
        }

        private static string UniqueChildElementLocalName(XElement parent, string desiredLocalName)
        {
            if (!parent.Elements().Any(e => e.Name.LocalName.Equals(desiredLocalName, StringComparison.Ordinal)))
                return desiredLocalName;

            int k = 2;
            string candidate;
            do
            {
                candidate = desiredLocalName + "_" + k++;
            } while (parent.Elements().Any(e => e.Name.LocalName.Equals(candidate, StringComparison.Ordinal)));

            return candidate;
        }
    }
}
