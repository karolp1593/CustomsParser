using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PdfTableMvp.Core
{
    public static class XmlExporter
    {
        // ------------------- Public high-level exports -------------------

        /// <summary>
        /// Unified entry: if router exists & matches, export parent+target;
        /// otherwise export just the single parser.
        /// </summary>
        public static XDocument ExportParserOrRoutedToXml(
            ParserConfig cfg,
            Func<Table> freshTableFactory,
            Action<string> log)
        {
            if (!ParserStore.RouterExists(cfg.Name))
            {
                log("Router: not configured; exporting single parser.");
                return ExportParserToXml(cfg, freshTableFactory, log);
            }

            var router = ParserStore.LoadRouter(cfg.Name);

            // Compute Tag
            if (!ParserRunner.TryEvaluateTag(cfg, router.TagRuleName, freshTableFactory, log, out var tag))
            {
                log("Router: could not compute Tag; exporting single parser.");
                return ExportParserToXml(cfg, freshTableFactory, log);
            }

            // Find matching route
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

            string? targetParserName = winner?.TargetParser ?? router.DefaultTargetParser;

            if (string.IsNullOrWhiteSpace(targetParserName))
            {
                log("Router: no route matched and no default target; exporting single parser.");
                return ExportParserToXml(cfg, freshTableFactory, log);
            }

            var targetCfg = ParserStore.Load(targetParserName);
            log($"Router: exporting PARENT='{cfg.Name}' + TARGET='{targetCfg.Name}' (Tag='{tag}').");

            return ExportParentAndTargetToXml(cfg, router, tag ?? string.Empty, targetCfg, freshTableFactory, log);
        }

        /// <summary>
        /// Run ALL rules for a single parser and produce the XML document.
        /// - Scalars become attributes on &lt;HeaderInfo&gt;
        /// - Tables become elements named by rule.Name
        /// - If rule.Partition is set, the table is split into multiple sibling elements by the key
        /// </summary>
        public static XDocument ExportParserToXml(ParserConfig cfg, Func<Table> freshTableFactory, Action<string> log)
        {
            var doc = NewResultDoc(out var root);

            // ParserInfo (single-parser)
            root.Add(BuildParserInfo(cfg.Name, subparser: null, routed: false, tagRuleName: null, tagValue: null));

            // Collect scalars (from all rules)
            var headerScalars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in cfg.Rules)
            {
                var t = RunRuleToTable(cfg, rule, freshTableFactory, log);

                if (t.IsScalar)
                {
                    headerScalars[rule.Name] = t.ScalarValue ?? "";
                }
                else
                {
                    if (rule.Partition != null)
                    {
                        foreach (var e in BuildPartitionedTableElements(rule.Name, t, rule.Partition))
                            root.Add(e);
                    }
                    else
                    {
                        root.Add(BuildTableElement(rule.Name, t));
                    }
                }
            }

            // HeaderInfo first
            root.AddFirst(BuildHeaderInfo(headerScalars));
            return doc;
        }

        /// <summary>
        /// Combined export for Multi-Parser (parent + target subparser).
        /// HeaderInfo includes scalars from BOTH.
        /// Tables from BOTH are appended under &lt;Result&gt;.
        /// If router.ExcludeRules is configured, target rules listed there are skipped.
        /// </summary>
        public static XDocument ExportParentAndTargetToXml(
            ParserConfig parentCfg,
            RouterConfig router,
            string tag,
            ParserConfig targetCfg,
            Func<Table> freshTableFactory,
            Action<string> log)
        {
            var doc = NewResultDoc(out var root);

            // ParserInfo (routed)
            root.Add(BuildParserInfo(
                parser: parentCfg.Name,
                subparser: targetCfg.Name,
                routed: true,
                tagRuleName: string.IsNullOrWhiteSpace(router.TagRuleName) ? "Tag" : router.TagRuleName,
                tagValue: tag));

            var headerScalars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void RunOne(ParserConfig cfg, ISet<string>? exclude)
            {
                foreach (var rule in cfg.Rules)
                {
                    if (exclude != null && exclude.Contains(rule.Name))
                    {
                        log($"[skip] Rule '{rule.Name}' excluded.");
                        continue;
                    }

                    var t = RunRuleToTable(cfg, rule, freshTableFactory, msg => log($"[{cfg.Name}] {msg}"));

                    if (t.IsScalar)
                    {
                        headerScalars[rule.Name] = t.ScalarValue ?? "";
                    }
                    else
                    {
                        if (rule.Partition != null)
                        {
                            foreach (var e in BuildPartitionedTableElements(rule.Name, t, rule.Partition))
                                root.Add(e);
                        }
                        else
                        {
                            root.Add(BuildTableElement(rule.Name, t));
                        }
                    }
                }
            }

            // Parent
            RunOne(parentCfg, exclude: null);

            // Target (with optional excludes from router)
            ISet<string>? excludes = null;
            if (router.ExcludeRules != null && router.ExcludeRules.Count > 0)
                excludes = new HashSet<string>(router.ExcludeRules, StringComparer.OrdinalIgnoreCase);

            RunOne(targetCfg, excludes);

            // HeaderInfo first
            root.AddFirst(BuildHeaderInfo(headerScalars));

            return doc;
        }

        // ------------------- Partitioning core -------------------

        /// <summary>
        /// Split one table rule into multiple sibling elements by key column.
        /// </summary>
        public static IEnumerable<XElement> BuildPartitionedTableElements(string ruleName, Table t, TablePartition partition)
        {
            if (partition == null)
            {
                yield return BuildTableElement(ruleName, t);
                yield break;
            }

            if (!partition.Column.TryResolveIndex(t, out int keyCol))
            {
                // If can't resolve partition column, fall back to single element.
                yield return BuildTableElement(ruleName, t);
                yield break;
            }

            var comparer = partition.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var groups = new Dictionary<string, List<int>>(comparer);

            for (int i = 0; i < t.Rows.Count; i++)
            {
                var cells = t.Rows[i];
                string key = keyCol < cells.Length ? (cells[keyCol] ?? "") : "";

                if (partition.TrimKey) key = key.Trim();

                if (string.IsNullOrEmpty(key))
                {
                    if (partition.DropEmptyKeyRows) continue;
                    key = partition.EmptyKeyLabel ?? "";
                }

                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    groups[key] = list;
                }
                list.Add(i);
            }

            string elementName = XmlNameHelper.MakeElementName(ruleName);
            string keyAttrName = XmlNameHelper.MakeAttributeName(
                !string.IsNullOrWhiteSpace(partition.AttributeName)
                    ? partition.AttributeName!
                    : (keyCol >= 0 && keyCol < t.ColumnNames.Count ? t.ColumnNames[keyCol] : "Key"));

            var attrNames = PrepareUniqueAttributeNames(t.ColumnNames);

            foreach (var kv in groups) // stable-enough order by first appearance
            {
                var elem = new XElement(elementName);
                elem.SetAttributeValue(keyAttrName, kv.Key);
                elem.SetAttributeValue("rowCount", kv.Value.Count);

                foreach (var rowIndex in kv.Value)
                {
                    var row = new XElement("Row");
                    row.SetAttributeValue("i", rowIndex);
                    if (rowIndex < t.RowPages.Count) row.SetAttributeValue("page", t.RowPages[rowIndex]);

                    var cells = t.Rows[rowIndex];

                    for (int c = 0; c < attrNames.Count; c++)
                    {
                        if (!partition.KeepKeyInRows && c == keyCol) continue;

                        string an = attrNames[c];
                        string val = c < cells.Length ? (cells[c] ?? "") : "";
                        row.SetAttributeValue(an, val);
                    }

                    elem.Add(row);
                }

                yield return elem;
            }
        }

        // ------------------- Shared building blocks -------------------

        public static XDocument TableToResultDocument(Table t, string elementName = "Current")
        {
            var doc = NewResultDoc(out var root);
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

        // ------------------- Internals -------------------

        private static XDocument NewResultDoc(out XElement root)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            root = new XElement("Result",
                new XAttribute("ranAt", DateTime.UtcNow.ToString("o")));
            doc.Add(root);
            return doc;
        }

        private static Table RunRuleToTable(ParserConfig cfg, RuleDefinition rule, Func<Table> freshTableFactory, Action<string> log)
        {
            var t = freshTableFactory();
            int totalEnabled = rule.RuleSteps.Count(s => s.Enabled);
            int step = 0;

            foreach (var s in rule.RuleSteps)
            {
                if (!s.Enabled) continue;
                step++;
                ParserRunner.ApplySingleStep(s, t, cfg.MissingPolicy, _ => { });
            }

            return t;
        }
    }
}
