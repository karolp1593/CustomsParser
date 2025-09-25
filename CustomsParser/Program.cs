// Program.cs — Console UI for PDF -> line table -> actions -> export
// Requires: itext7 (NuGet) and Core/*.cs from this project.

using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PdfTableMvp.Core;
using System.Text;
using System.Text.Json; // for CloneSteps deep copy of StepBase list
using System.Xml.Linq;  // <-- added so we can Save() XDocument
using CoreTable = PdfTableMvp.Core.Table;

namespace PdfTableMvp
{
    internal class Program
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== PDF Table MVP (Console) — Parser > Rule > RuleSteps ===");

            // 1) Load PDF (all pages)
            Console.Write("Enter PDF path: ");
            var pdfPath = (Console.ReadLine() ?? "").Trim('"', ' ');
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            var pages = ExtractPages(pdfPath);
            Console.WriteLine($"\nLoaded {pages.Count} page(s).");

            // 2) Build initial one-column table (each line = row), track page per row
            var originalLines = new List<string>();
            var originalRowPages = new List<int>();
            for (int p = 0; p < pages.Count; p++)
            {
                var lines = pages[p].Split('\n').Select(s => s.Replace("\r", "")).ToList();
                originalLines.AddRange(lines);
                originalRowPages.AddRange(Enumerable.Repeat(p + 1, lines.Count)); // 1-based page number
            }

            var table = CoreTable.FromSingleColumnLines(originalLines, originalRowPages);
            Console.WriteLine($"\nInitial table: {table.Rows.Count} row(s) × {table.ColumnCount} column(s).");

            Console.WriteLine("\n--- ALL ROWS (index + page) ---");
            PreviewTable(table, table.Rows.Count, showPages: true, showRowIndex: true);

            // Session: collect steps (these become RuleSteps when saved)
            List<StepBase> sessionSteps = new();

            // Interactive loop
            while (true)
            {
                Console.WriteLine(@"
Actions:
 1) Keep Table Section (start & end regex, repeated per page)
 2) Keep rows where column matches regex
 3) Keep rows where column is NOT empty
 4) Trim all cells
 5) Transform a column:
    1=Trim
    2=Replace regex
    3=Left N
    4=Right N
    5=Cut last W words (in-place)
    6=Fill empty from NEXT non-empty row
    7=Fill empty from PREVIOUS non-empty row
    8=Fill empty with ROW INDEX (start at X)
    9=Fill empty with STATIC VALUE
    10=To UPPER
    11=To lower
    12=To TitleCase
 6) Split a column (creates NEW columns on the right):
    a=Split on keyword (first or ALL occurrences)
    b=Split after N characters
    c=Cut last W words → NEW column
    d=Split on regex delimiter (first match)
    e=Split after W words
    f=Cut last N chars → NEW column
 7) Keep ONLY selected columns
 8) Drop first row (header)
 9) Rename columns (become JSON/XML attribute names)
10) Show preview (limited)
11) Export current table to JSON

--- Extra column/row utilities ---
19) Insert blank column
20) Copy column → column (new/overwrite/append)
21) Merge rows (group by start/end regex, strategy)
22) Regex extract (in-place or to new column[s])

--- Parser menu (Parser > Rule > RuleSteps) ---
12) Save session steps into a Parser & Rule (append or replace)
13) List Parsers & their Rules
14) Load a Parser & Run (auto-router if configured) on this PDF
15) Show session steps
16) Clear session steps
17) Run Parser (with router if present) and export ONE XML
18) Convert table to single header value (pick cell / optional regex)

--- Edit saved Rule as SESSION (load & modify without losing work) ---
23) Load a Parser & Rule INTO SESSION (to edit)
24) Delete a session step
25) Move a session step (reorder)
26) Toggle enable/disable a session step
27) Re-run session on original text (refresh preview)

--- Optional: Multi-Parser Router ---
28) Configure Router for a Parser (Tag rule + routes + optional excludes)
29) Dry-run Router: compute Tag & show which target parser would match

 0) Exit
");
                Console.Write("Choose (e.g., 5, 6c, 12): ");
                var choice = Normalize((Console.ReadLine() ?? ""));

                switch (choice)
                {
                    case "0": Console.WriteLine("Bye."); return;

                    // ====== ACTIONS ======
                    case "1":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Start regex: "); var startRx = Console.ReadLine() ?? "";
                            Console.Write("End regex (empty = to page end): "); var endRx = Console.ReadLine() ?? "";
                            bool ci = AskYesNo("Case-insensitive? (y/n): ");
                            bool includeStart = AskYesNo("Include start line? (y/n): ");
                            bool includeEnd = AskYesNo("Include end line? (y/n): ");
                            var step = new KeepTableSectionStep { Col = colSel, StartRegex = startRx, EndRegex = endRx, CaseInsensitive = ci, IncludeStart = includeStart, IncludeEnd = includeEnd };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            break;
                        }
                    case "2":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Regex (e.g., ^\\d+$ or (?i)^sku): "); var rx = Console.ReadLine() ?? ".*";
                            var step = new KeepRowsWhereRegexStep { Col = colSel, Regex = rx };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            break;
                        }
                    case "3":
                        {
                            var colSel = AskColumnSelector(table);
                            var step = new KeepRowsWhereNotEmptyStep { Col = colSel };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            break;
                        }
                    case "4":
                        {
                            var step = new TrimAllStep();
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine("Trimmed all cells.");
                            break;
                        }
                    case "5":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.WriteLine("Transform (pick number): 1=Trim, 2=Replace regex, 3=Left N, 4=Right N, 5=Cut last W words, 6=Fill from NEXT, 7=Fill from PREVIOUS, 8=Fill empty with ROW INDEX, 9=Fill empty with STATIC VALUE, 10=To UPPERCASE, 11=To lowercase, 12=To TitleCase");
                            Console.Write("Pick: "); var t = (Console.ReadLine() ?? "").Trim();
                            switch (t)
                            {
                                case "1": ApplyAndRecord(new TransformTrimStep { Col = colSel }); break;
                                case "2":
                                    Console.Write("Pattern: "); var pat = Console.ReadLine() ?? "";
                                    Console.Write("Replacement: "); var repl = Console.ReadLine() ?? "";
                                    ApplyAndRecord(new TransformReplaceRegexStep { Col = colSel, Pattern = pat, Replacement = repl }); break;
                                case "3": ApplyAndRecord(new TransformLeftStep { Col = colSel, N = AskInt("N (chars): ", 0, int.MaxValue) }); break;
                                case "4": ApplyAndRecord(new TransformRightStep { Col = colSel, N = AskInt("N (chars): ", 0, int.MaxValue) }); break;
                                case "5": ApplyAndRecord(new TransformCutLastWordsStep { Col = colSel, W = AskInt("W (words): ", 0, int.MaxValue) }); break;
                                case "6": ApplyAndRecord(new FillEmptyStep { Col = colSel, Direction = FillDirection.Next }); break;
                                case "7": ApplyAndRecord(new FillEmptyStep { Col = colSel, Direction = FillDirection.Previous }); break;
                                case "8":
                                    {
                                        int start = AskInt("Starting index (e.g., 0 or 1): ", int.MinValue + 1, int.MaxValue);
                                        ApplyAndRecord(new FillEmptyWithRowIndexStep { Col = colSel, StartIndex = start });
                                        break;
                                    }
                                case "9":
                                    {
                                        Console.Write("Static value to fill: ");
                                        var val = Console.ReadLine() ?? "";
                                        ApplyAndRecord(new FillEmptyWithStaticValueStep { Col = colSel, Value = val });
                                        break;
                                    }
                                case "10":
                                    {
                                        Console.Write("Culture (blank = current, e.g., en-US): ");
                                        var culture = (Console.ReadLine() ?? "").Trim();
                                        if (culture.Length == 0) culture = null;
                                        ApplyAndRecord(new TransformToUpperStep { Col = colSel, Culture = culture });
                                        break;
                                    }
                                case "11":
                                    {
                                        Console.Write("Culture (blank = current, e.g., en-US): ");
                                        var culture = (Console.ReadLine() ?? "").Trim();
                                        if (culture.Length == 0) culture = null;
                                        ApplyAndRecord(new TransformToLowerStep { Col = colSel, Culture = culture });
                                        break;
                                    }
                                case "12":
                                    {
                                        bool lowerFirst = AskYesNo("Force lowercase before TitleCase? (y/n): ");
                                        Console.Write("Culture (blank = current, e.g., en-US): ");
                                        var culture = (Console.ReadLine() ?? "").Trim();
                                        if (culture.Length == 0) culture = null;
                                        ApplyAndRecord(new TransformToTitleCaseStep { Col = colSel, ForceLowerFirst = lowerFirst, Culture = culture });
                                        break;
                                    }

                                default: Console.WriteLine("Unknown transform."); break;
                            }
                            break;

                            void ApplyAndRecord(StepBase s) { ApplyStep(s, table); sessionSteps.Add(s); }
                        }
                    case "6":
                        {
                            Console.WriteLine("Split: a=keyword, b=after N chars, c=cut last W words → NEW column, d=regex delimiter, e=after W words, f=cut last N chars → NEW column");
                            Console.Write("Pick (a/b/c/d/e/f): ");
                            choice = "6" + Normalize((Console.ReadLine() ?? ""));
                            goto case "6a";
                        }
                    case "6a":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Keyword to split on: "); var kw = Console.ReadLine() ?? "";
                            bool ci = AskYesNo("Case-insensitive? (y/n): ");
                            bool all = AskYesNo("Split on EACH occurrence? (y/n): ");
                            var step = new SplitOnKeywordStep { Col = colSel, Keyword = kw, CaseInsensitive = ci, AllOccurrences = all };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }
                    case "6b":
                        {
                            var colSel = AskColumnSelector(table);
                            int n = AskInt("Split after N characters: ", 0, int.MaxValue);
                            var step = new SplitAfterCharsStep { Col = colSel, N = n };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }
                    case "6c":
                        {
                            var colSel = AskColumnSelector(table);
                            int w = AskInt("Cut last W words (to NEW column): ", 0, int.MaxValue);
                            var step = new SplitCutLastWordsToNewColumnStep { Col = colSel, W = w };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }
                    case "6d":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Regex delimiter (first match is the split point): "); var rx = Console.ReadLine() ?? "";
                            bool ci = AskYesNo("Case-insensitive? (y/n): ");
                            var step = new SplitOnRegexDelimiterStep { Col = colSel, Pattern = rx, CaseInsensitive = ci };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }
                    case "6e":
                        {
                            var colSel = AskColumnSelector(table);
                            int w = AskInt("Split after W words: ", 0, int.MaxValue);
                            var step = new SplitAfterWordsStep { Col = colSel, W = w };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }
                    case "6f":
                        {
                            var colSel = AskColumnSelector(table);
                            int n = AskInt("Cut last N chars (to NEW column): ", 0, int.MaxValue);
                            var step = new SplitCutLastCharsToNewColumnStep { Col = colSel, N = n };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Split done. Now {table.ColumnCount} column(s).");
                            break;
                        }

                    case "7":
                        {
                            Console.WriteLine("Enter column indexes to keep, comma-separated (e.g., 0,2,3): ");
                            var keep = (Console.ReadLine() ?? "")
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .Select(p => int.TryParse(p, out var n) ? n : -1)
                                .Where(n => n >= 0 && n < table.ColumnCount)
                                .ToArray();
                            if (keep.Length == 0) Console.WriteLine("No valid columns selected.");
                            else
                            {
                                var step = new KeepColumnsStep { Keep = keep.Select(i => ColumnSelector.ByIndex(i)).ToList() };
                                ApplyStep(step, table); sessionSteps.Add(step);
                                Console.WriteLine($"Kept {table.ColumnCount} column(s).");
                            }
                            break;
                        }
                    case "8":
                        {
                            var step = new DropFirstRowStep();
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine("Dropped first row (if present).");
                            break;
                        }
                    case "9":
                        {
                            Console.WriteLine($"Current columns ({table.ColumnCount}): " + string.Join(", ", table.ColumnNames));
                            Console.WriteLine($"Enter {table.ColumnCount} names, comma-separated (missing = keep old; duplicates auto-suffixed).");
                            var raw = Console.ReadLine() ?? "";
                            var names = raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                            var step = new RenameColumnsStep { Names = names };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine("Renamed.");
                            break;
                        }
                    case "10":
                        {
                            int max = AskInt("Max rows to preview (e.g., 50): ", 1, int.MaxValue);
                            PreviewTable(table, max, showPages: true, showRowIndex: true);
                            break;
                        }
                    case "11":
                        {
                            Console.Write("Output JSON path (e.g., output.json): ");
                            var outPath = (Console.ReadLine() ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(outPath)) outPath = "output.json";
                            table.ExportToJson(outPath);
                            Console.WriteLine($"Saved: {Path.GetFullPath(outPath)}");
                            break;
                        }

                    // ====== PARSER ======
                    case "12":
                        {
                            if (sessionSteps.Count == 0) { Console.WriteLine("No session steps to save."); break; }

                            Console.Write("Parser name (e.g., Procter): ");
                            var parserName = (Console.ReadLine() ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(parserName)) { Console.WriteLine("Name required."); break; }

                            Console.Write("Rule name (e.g., InvoiceItems): ");
                            var ruleName = (Console.ReadLine() ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(ruleName)) { Console.WriteLine("Name required."); break; }

                            Console.Write("Missing column policy for this parser (skip|warn|fail) [warn]: ");
                            var pol = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                            var policy = pol switch { "skip" => MissingColumnPolicy.Skip, "fail" => MissingColumnPolicy.Fail, _ => MissingColumnPolicy.Warn };

                            var cfg = ParserStore.Exists(parserName) ? ParserStore.Load(parserName) : new ParserConfig { Name = parserName, Version = "1.0.0" };
                            cfg.MissingPolicy = policy;

                            var rule = cfg.Rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
                            if (rule == null) { rule = new RuleDefinition { Name = ruleName, RuleSteps = new List<StepBase>() }; cfg.Rules.Add(rule); }

                            Console.Write("Append to existing Rule or replace its steps? (append/replace) [append]: ");
                            var mode = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                            if (mode == "replace") rule.RuleSteps = new List<StepBase>(sessionSteps);
                            else rule.RuleSteps.AddRange(sessionSteps);

                            // Build a fresh preview using the steps that will be saved (for the partition wizard)
                            var previewForPartition = RebuildFromSession(
                                originalLines,
                                originalRowPages,
                                rule.RuleSteps,
                                cfg.MissingPolicy,
                                msg => Console.WriteLine("[preview] " + msg));

                            // ---- Partition wizard ----
                            rule.Partition = ConfigurePartitionWizard(previewForPartition, rule.Partition);

                            ParserStore.Save(cfg);
                            Console.WriteLine($"Saved {rule.RuleSteps.Count} step(s) under Parser '{cfg.Name}' › Rule '{rule.Name}'"
                                + (rule.Partition != null ? " [partition configured]." : "."));
                            break;
                        }

                    case "13":
                        {
                            var names = ParserStore.ListParsers();
                            if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); break; }

                            Console.WriteLine("Parsers:");
                            foreach (var n in names)
                            {
                                var cfg = ParserStore.Load(n);
                                Console.WriteLine($" - {cfg.Name} (policy={cfg.MissingPolicy}, rules={cfg.Rules.Count})");
                                foreach (var r in cfg.Rules)
                                {
                                    int enabled = r.RuleSteps.Count(s => s.Enabled);
                                    int disabled = r.RuleSteps.Count - enabled;
                                    var tail = disabled > 0 ? $"{enabled} step(s), {disabled} disabled" : $"{enabled} step(s)";
                                    var part = r.Partition != null ? " [partition]" : "";
                                    Console.WriteLine($"    • {r.Name}{part} — {tail}");
                                }
                            }
                            break;
                        }

                    case "14":
                        {
                            var names = ParserStore.ListParsers();
                            if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); break; }

                            Console.WriteLine("Available parsers:");
                            for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                            int pick = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;

                            var cfg = ParserStore.Load(names[pick]);

                            // Transparent Multi-Parser: if router exists, auto-route and run (legacy preview mode)
                            if (ParserStore.RouterExists(cfg.Name) &&
                                ParserRunner.TryRunWithRouting(
                                    cfg,
                                    () => CoreTable.FromSingleColumnLines(originalLines, originalRowPages),
                                    out var routed,
                                    msg => Console.WriteLine(msg)))
                            {
                                table = routed!;
                                Console.WriteLine("\n--- ROUTED RESULT PREVIEW ---");
                                PreviewTable(table, 50, showPages: true, showRowIndex: true);
                                break;
                            }

                            // Fallback: manual single-rule run
                            if (cfg.Rules.Count == 0) { Console.WriteLine("This parser has no Rules."); break; }

                            Console.WriteLine($"\nParser '{cfg.Name}' rules:");
                            for (int i = 0; i < cfg.Rules.Count; i++)
                                Console.WriteLine($"{i + 1}) {cfg.Rules[i].Name} ({cfg.Rules[i].RuleSteps.Count} step(s))");

                            int ri = AskInt($"Run which Rule (1..{cfg.Rules.Count}): ", 1, cfg.Rules.Count) - 1;

                            var fresh = CoreTable.FromSingleColumnLines(originalLines, originalRowPages);
                            ParserRunner.RunRule(cfg, cfg.Rules[ri].Name, fresh, msg => Console.WriteLine(msg));

                            Console.WriteLine("\n--- RESULT PREVIEW ---");
                            PreviewTable(fresh, 50, showPages: true, showRowIndex: true);

                            table = fresh;
                            break;
                        }

                    case "15":
                        {
                            if (sessionSteps.Count == 0) { Console.WriteLine("No session steps."); break; }
                            Console.WriteLine("\nSession steps:");
                            for (int i = 0; i < sessionSteps.Count; i++)
                                Console.WriteLine($"{i + 1}. {sessionSteps[i].Describe()}");
                            break;
                        }

                    case "16": sessionSteps.Clear(); Console.WriteLine("Session steps cleared."); break;

                    case "17":
                        {
                            var names = ParserStore.ListParsers();
                            if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); break; }

                            Console.WriteLine("Available parsers:");
                            for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                            int pick = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;

                            var cfg = ParserStore.Load(names[pick]);

                            Console.Write("Output XML path (e.g., result.xml): ");
                            var outPath = (Console.ReadLine() ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(outPath)) outPath = "result.xml";

                            // Correct: get the XDocument, then save it
                            var xdoc = XmlExporter.ExportParserOrRoutedToXml(
                                cfg,
                                () => CoreTable.FromSingleColumnLines(originalLines, originalRowPages),
                                msg => Console.WriteLine(msg));

                            xdoc.Save(outPath);
                            Console.WriteLine($"Saved: {Path.GetFullPath(outPath)}");
                            break;
                        }

                    case "18":
                        {
                            var colSel = AskColumnSelector(table);
                            int maxRow = Math.Max(0, table.Rows.Count - 1);
                            int row = AskInt($"Row index (0..{maxRow}): ", 0, maxRow);
                            Console.Write("Optional regex to extract (blank = skip): ");
                            var pat = Console.ReadLine();
                            int grp = 1;
                            if (!string.IsNullOrWhiteSpace(pat))
                                grp = AskInt("Capture group number [1]: ", 0, 99);
                            bool req = AskYesNo("Require table to be 1x1? (y/n): ");
                            bool trim = AskYesNo("Trim result? (y/n): ");

                            var step = new ToScalarFromCellStep
                            {
                                Col = colSel,
                                Row = row,
                                Pattern = string.IsNullOrWhiteSpace(pat) ? null : pat,
                                Group = grp,
                                RequireSingleCell = req,
                                Trim = trim
                            };
                            ApplyStep(step, table);
                            sessionSteps.Add(step);

                            Console.WriteLine("Converted to scalar. In option 17 (run ALL rules), this Rule will appear in HeaderInfo.");
                            break;
                        }
                    case "19":
                        {
                            int pos = AskInt($"Insert position (0..{table.ColumnCount}, {table.ColumnCount}=end): ", 0, table.ColumnCount);
                            Console.Write("New column name (blank = auto): "); var name = Console.ReadLine() ?? "";
                            var step = new InsertBlankColumnStep { InsertIndex = pos, ColumnName = name };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Inserted. Cols={table.ColumnCount}");
                            break;
                        }
                    case "20":
                        {
                            var srcSel = AskColumnSelector(table);
                            bool createNew = AskYesNo("Create NEW destination column? (y/n): ");
                            int destIndex;
                            string newName = "";
                            if (createNew)
                            {
                                destIndex = AskInt($"Insert new dest at index (0..{table.ColumnCount}, {table.ColumnCount}=end): ", 0, table.ColumnCount);
                                Console.Write("New column name (blank = auto): "); newName = Console.ReadLine() ?? "";
                            }
                            else
                            {
                                destIndex = AskInt($"Destination index (0..{Math.Max(0, table.ColumnCount - 1)}): ", 0, Math.Max(0, table.ColumnCount - 1));
                            }

                            bool append = false; bool overwrite = true; string sep = " ";
                            if (!createNew)
                            {
                                append = AskYesNo("Append to destination? (y=append / n=overwrite): ");
                                overwrite = !append;
                                if (append)
                                {
                                    Console.Write("Append separator [space]: "); var ssep = Console.ReadLine() ?? "";
                                    if (!string.IsNullOrEmpty(ssep)) sep = ssep;
                                }
                            }
                            bool onlyNonEmpty = AskYesNo("Only copy when SOURCE is non-empty? (y/n): ");

                            var step = new CopyColumnStep
                            {
                                Source = srcSel,
                                CreateNewDestination = createNew,
                                DestinationIndex = destIndex,
                                NewColumnName = newName,
                                Append = append,
                                Overwrite = overwrite,
                                Separator = sep,
                                OnlyWhenSourceNonEmpty = onlyNonEmpty
                            };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine("Copied.");
                            break;
                        }
                    case "21":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Group START regex: "); var start = Console.ReadLine() ?? "";
                            Console.Write("Group END regex (blank = until next START/table end): "); var end = Console.ReadLine() ?? "";
                            bool ci = AskYesNo("Case-insensitive? (y/n): ");
                            bool perPage = AskYesNo("Reset grouping at page boundaries? (y/n): ");
                            Console.WriteLine("Merge strategy: 1=Concat space, 2=Concat newline, 3=First non-empty, 4=Last non-empty");
                            int strat = AskInt("Pick (1..4): ", 1, 4);
                            var strategy = strat switch
                            {
                                1 => MergeJoinStrategy.ConcatSpace,
                                2 => MergeJoinStrategy.ConcatNewline,
                                3 => MergeJoinStrategy.FirstNonEmpty,
                                4 => MergeJoinStrategy.LastNonEmpty,
                                _ => MergeJoinStrategy.ConcatSpace
                            };
                            var step = new MergeRowsByGroupStep
                            {
                                Col = colSel,
                                StartPattern = start,
                                EndPattern = string.IsNullOrWhiteSpace(end) ? null : end,
                                CaseInsensitive = ci,
                                ResetPerPage = perPage,
                                Strategy = strategy
                            };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine($"Merged. Rows={table.Rows.Count}");
                            break;
                        }
                    case "22":
                        {
                            var colSel = AskColumnSelector(table);
                            Console.Write("Regex pattern: "); var pat = Console.ReadLine() ?? "";
                            bool ci = AskYesNo("Case-insensitive? (y/n): ");
                            bool all = AskYesNo("Extract ALL matches? (y/n): ");
                            Console.Write("Capture group (0=whole, default 1): ");
                            var gi = Console.ReadLine();
                            int groupIndex = 1;
                            if (int.TryParse(gi, out var gtemp) && gtemp >= 0 && gtemp <= 99) groupIndex = gtemp;

                            bool inPlace = AskYesNo("Write IN-PLACE? (y = in the same column / n = to new column[s]): ");

                            bool expand = false; string joinSep = ", "; string newName = "";
                            if (!inPlace)
                            {
                                if (all)
                                {
                                    expand = AskYesNo("Expand to MULTIPLE new columns (one per match)? (y/n): ");
                                    if (!expand)
                                    {
                                        Console.Write("Join separator for all matches [, ]: ");
                                        var js = Console.ReadLine();
                                        if (!string.IsNullOrEmpty(js)) joinSep = js!;
                                    }
                                }
                                Console.Write("New column base name (blank = auto): ");
                                newName = Console.ReadLine() ?? "";
                            }

                            var step = new RegexExtractStep
                            {
                                Col = colSel,
                                Pattern = pat,
                                CaseInsensitive = ci,
                                Group = groupIndex,
                                AllMatches = all,
                                InPlace = inPlace,
                                ExpandToMultipleColumns = !inPlace && all && expand,
                                JoinSeparator = joinSep,
                                NewColumnName = newName
                            };
                            ApplyStep(step, table); sessionSteps.Add(step);
                            Console.WriteLine("Regex extract done.");
                            break;
                        }


                    // ====== SESSION editing ======
                    case "23":
                        {
                            var names = ParserStore.ListParsers();
                            if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); break; }

                            Console.WriteLine("Available parsers:");
                            for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                            int pick = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;

                            var cfg = ParserStore.Load(names[pick]);
                            if (cfg.Rules.Count == 0) { Console.WriteLine("This parser has no Rules."); break; }

                            Console.WriteLine($"\nParser '{cfg.Name}' rules:");
                            for (int i = 0; i < cfg.Rules.Count; i++)
                                Console.WriteLine($"{i + 1}) {cfg.Rules[i].Name} ({cfg.Rules[i].RuleSteps.Count} step(s))");

                            int ri = AskInt($"Load which Rule into SESSION (1..{cfg.Rules.Count}): ", 1, cfg.Rules.Count) - 1;

                            sessionSteps = CloneSteps(cfg.Rules[ri].RuleSteps);
                            Console.WriteLine($"Loaded {sessionSteps.Count} step(s) from '{cfg.Name}' › '{cfg.Rules[ri].Name}' into SESSION.");

                            table = RebuildFromSession(originalLines, originalRowPages, sessionSteps, MissingColumnPolicy.Warn, msg => Console.WriteLine("[run] " + msg));
                            Console.WriteLine("\n--- SESSION PREVIEW ---");
                            PreviewTable(table, 50, showPages: true, showRowIndex: true);
                            break;
                        }
                    case "24":
                        {
                            if (sessionSteps.Count == 0) { Console.WriteLine("No session steps."); break; }
                            for (int i = 0; i < sessionSteps.Count; i++)
                                Console.WriteLine($"{i + 1}. {(sessionSteps[i].Enabled ? "[on] " : "[off]")}{sessionSteps[i].Describe()}");

                            int idx = AskInt($"Delete which step (1..{sessionSteps.Count}): ", 1, sessionSteps.Count) - 1;
                            var removed = sessionSteps[idx].Describe();
                            sessionSteps.RemoveAt(idx);
                            Console.WriteLine($"Deleted: {removed}");

                            table = RebuildFromSession(originalLines, originalRowPages, sessionSteps, MissingColumnPolicy.Warn, msg => Console.WriteLine("[run] " + msg));
                            PreviewTable(table, 50, showPages: true, showRowIndex: true);
                            break;
                        }
                    case "25":
                        {
                            if (sessionSteps.Count < 2) { Console.WriteLine("Need at least 2 steps."); break; }
                            for (int i = 0; i < sessionSteps.Count; i++)
                                Console.WriteLine($"{i + 1}. {(sessionSteps[i].Enabled ? "[on] " : "[off]")}{sessionSteps[i].Describe()}");

                            int from = AskInt($"Move FROM (1..{sessionSteps.Count}): ", 1, sessionSteps.Count) - 1;
                            int to = AskInt($"Move TO   (1..{sessionSteps.Count}): ", 1, sessionSteps.Count) - 1;

                            var s = sessionSteps[from];
                            sessionSteps.RemoveAt(from);
                            sessionSteps.Insert(to, s);
                            Console.WriteLine($"Moved step to position {to + 1}.");

                            table = RebuildFromSession(originalLines, originalRowPages, sessionSteps, MissingColumnPolicy.Warn, msg => Console.WriteLine("[run] " + msg));
                            PreviewTable(table, 50, showPages: true, showRowIndex: true);
                            break;
                        }
                    case "26":
                        {
                            if (sessionSteps.Count == 0) { Console.WriteLine("No session steps."); break; }
                            for (int i = 0; i < sessionSteps.Count; i++)
                                Console.WriteLine($"{i + 1}. {(sessionSteps[i].Enabled ? "[on] " : "[off]")}{sessionSteps[i].Describe()}");

                            int idx = AskInt($"Toggle which step (1..{sessionSteps.Count}): ", 1, sessionSteps.Count) - 1;
                            sessionSteps[idx].Enabled = !sessionSteps[idx].Enabled;
                            Console.WriteLine($"{(sessionSteps[idx].Enabled ? "Enabled" : "Disabled")}: {sessionSteps[idx].Describe()}");

                            table = RebuildFromSession(originalLines, originalRowPages, sessionSteps, MissingColumnPolicy.Warn, msg => Console.WriteLine("[run] " + msg));
                            PreviewTable(table, 50, showPages: true, showRowIndex: true);
                            break;
                        }
                    case "27":
                        {
                            table = RebuildFromSession(originalLines, originalRowPages, sessionSteps, MissingColumnPolicy.Warn, msg => Console.WriteLine("[run] " + msg));
                            Console.WriteLine("\n--- SESSION PREVIEW ---");
                            PreviewTable(table, 50, showPages: true, showRowIndex: true);
                            break;
                        }

                    // ====== Router config ======
                    case "28":
                        {
                            ConfigureRouterInteractive();
                            break;
                        }

                    case "29":
                        {
                            var names = ParserStore.ListParsers();
                            if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); break; }
                            Console.WriteLine("Pick parser to DRY-RUN router:");
                            for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                            int pick = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;

                            var cfg = ParserStore.Load(names[pick]);
                            if (!ParserStore.RouterExists(cfg.Name))
                            {
                                Console.WriteLine("This parser has no router configured.");
                                break;
                            }
                            var router = ParserStore.LoadRouter(cfg.Name);
                            Console.WriteLine("\nCurrent router:");
                            Console.WriteLine(router.Describe());

                            if (!ParserRunner.TryEvaluateTag(cfg, router.TagRuleName,
                                    () => CoreTable.FromSingleColumnLines(originalLines, originalRowPages),
                                    msg => Console.WriteLine(msg),
                                    out var tag))
                            {
                                Console.WriteLine("Could not compute Tag.");
                                break;
                            }

                            // show match without running
                            RouteRule? winner = null;
                            foreach (var r in router.Routes)
                            {
                                bool match = r.Kind == RouteMatchKind.Exact
                                    ? string.Equals(tag, r.Pattern, r.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture)
                                    : System.Text.RegularExpressions.Regex.IsMatch(tag ?? "", r.Pattern ?? "", r.CaseInsensitive ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None);
                                if (match) { winner = r; break; }
                            }

                            if (winner != null)
                                Console.WriteLine($"Dry-run: Tag=\"{tag}\" matches => {winner.TargetParser}");
                            else if (!string.IsNullOrWhiteSpace(router.DefaultTargetParser))
                                Console.WriteLine($"Dry-run: no rule matched; would use DEFAULT => {router.DefaultTargetParser}");
                            else
                                Console.WriteLine("Dry-run: no rule matched and no default specified.");
                            break;
                        }

                    default: Console.WriteLine("Unknown command."); break;
                }
            }

            // ------------ local helpers ------------
            void ConfigureRouterInteractive()
            {
                var names = ParserStore.ListParsers();
                if (names.Count == 0) { Console.WriteLine("No parsers saved yet."); return; }

                Console.WriteLine("Pick parser to configure router:");
                for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                int pick = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;

                var parserName = names[pick];
                var router = ParserStore.LoadRouter(parserName);

                Console.WriteLine("\nCurrent router:");
                Console.WriteLine(router.Describe());

                Console.Write($"Tag rule name [{router.TagRuleName}]: ");
                var tagName = (Console.ReadLine() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(tagName)) router.TagRuleName = tagName;

                if (router.Routes.Count > 0 && AskYesNo("Clear existing routes? (y/n): "))
                    router.Routes.Clear();

                while (AskYesNo("Add a route? (y/n): "))
                {
                    Console.Write("Match kind (1=Exact, 2=Regex): ");
                    var mk = (Console.ReadLine() ?? "").Trim();
                    var kind = mk == "2" ? RouteMatchKind.Regex : RouteMatchKind.Exact;

                    Console.Write("Pattern to match: ");
                    var pattern = Console.ReadLine() ?? "";

                    bool ci = AskYesNo("Case-insensitive? (y/n): ");

                    // Pick target parser
                    Console.WriteLine("Pick TARGET parser:");
                    for (int i = 0; i < names.Count; i++) Console.WriteLine($"{i + 1}) {names[i]}");
                    int tpi = AskInt($"Pick (1..{names.Count}): ", 1, names.Count) - 1;
                    var targetParser = ParserStore.Load(names[tpi]);

                    router.Routes.Add(new RouteRule
                    {
                        Kind = kind,
                        Pattern = pattern,
                        CaseInsensitive = ci,
                        TargetParser = targetParser.Name
                    });
                    Console.WriteLine("Route added.");
                }

                // Exclude rules (optional)
                Console.Write("Exclude rules when running the TARGET parser (comma-separated, blank = none): ");
                var excl = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(excl))
                    router.ExcludeRules = new List<string>();
                else
                    router.ExcludeRules = excl.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

                if (AskYesNo("Configure DEFAULT (fallback) target parser? (y/n): "))
                {
                    var names2 = ParserStore.ListParsers();
                    for (int i = 0; i < names2.Count; i++) Console.WriteLine($"{i + 1}) {names2[i]}");
                    int di = AskInt($"Pick default parser (1..{names2.Count}): ", 1, names2.Count) - 1;
                    var defParser = ParserStore.Load(names2[di]);
                    router.DefaultTargetParser = defParser.Name;
                }
                else
                {
                    router.DefaultTargetParser = null;
                }

                ParserStore.SaveRouter(parserName, router);
                Console.WriteLine("\nRouter saved.\n");
                Console.WriteLine(router.Describe());
            }
        }

        // ------------ Helpers ------------
        static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant().Replace(" ", "");

        static ColumnSelector AskColumnSelector(CoreTable t)
        {
            Console.WriteLine("Select column by: 1=Index, 2=Exact Name, 3=Name Regex, 4=Last column");
            Console.Write("Choice: ");
            var ch = (Console.ReadLine() ?? "").Trim();
            return ch switch
            {
                "1" => ColumnSelector.ByIndex(AskInt($"Column index (0..{Math.Max(0, t.ColumnCount - 1)}): ", 0, Math.Max(0, t.ColumnCount - 1))),
                "2" => ColumnSelector.ByName(Read("Enter exact name: ")),
                "3" => ColumnSelector.ByNameRegex(Read("Enter name regex: ")),
                "4" => ColumnSelector.Last(),
                _ => ColumnSelector.ByIndex(0)
            };
            static string Read(string p) { Console.Write(p); return Console.ReadLine() ?? ""; }
        }

        static List<string> ExtractPages(string pdfPath)
        {
            var result = new List<string>();
            using var pdf = new PdfDocument(new PdfReader(pdfPath));
            int n = pdf.GetNumberOfPages();
            for (int i = 1; i <= n; i++)
            {
                var strategy = new LocationTextExtractionStrategy();
                string text = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), strategy);
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                result.Add(text);
            }
            return result;
        }

        static int AskInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                var s = Console.ReadLine();
                if (int.TryParse(s, out int v) && v >= min && v <= max) return v;
                Console.WriteLine($"Please enter an integer between {min} and {max}.");
            }
        }

        static bool AskYesNo(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                if (s is "y" or "yes") return true; if (s is "n" or "no") return false;
            }
        }

        static void ApplyStep(StepBase step, CoreTable t)
        {
            try { ParserRunner.ApplySingleStep(step, t, MissingColumnPolicy.Warn, msg => Console.WriteLine("[step] " + msg)); }
            catch (Exception ex) { Console.WriteLine($"Step error: {ex.Message}"); }
        }

        // Deep-copy steps so editing session doesn't mutate loaded objects
        static List<StepBase> CloneSteps(List<StepBase> steps)
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new StepJsonConverter());
            var json = System.Text.Json.JsonSerializer.Serialize(steps, opts);
            return System.Text.Json.JsonSerializer.Deserialize<List<StepBase>>(json, opts) ?? new List<StepBase>();
        }

        // Rebuild table from original text + current session (enabled steps)
        static CoreTable RebuildFromSession(
            List<string> originalLines,
            List<int> originalRowPages,
            List<StepBase> sessionSteps,
            MissingColumnPolicy policy,
            Action<string> log)
        {
            var fresh = CoreTable.FromSingleColumnLines(originalLines, originalRowPages);
            foreach (var s in sessionSteps.Where(x => x.Enabled))
            {
                try { ParserRunner.ApplySingleStep(s, fresh, policy, log); }
                catch (Exception ex) { log($"Step error at '{s.Describe()}': {ex.Message}"); }
            }
            return fresh;
        }

        static void PreviewTable(CoreTable t, int maxRows, bool showPages, bool showRowIndex)
        {
            if (t.IsScalar)
            {
                Console.WriteLine($"[SCALAR] {t.ScalarValue}");
                return;
            }

            int rows = Math.Min(maxRows, t.Rows.Count);
            Console.WriteLine($"Preview: {rows}/{t.Rows.Count} row(s), {t.ColumnCount} col(s)");
            Console.WriteLine(new string('-', 80));
            for (int i = 0; i < rows; i++)
            {
                var cells = t.Rows[i];
                var left = new List<string>();
                if (showRowIndex) left.Add($"#{i:0000}");
                if (showPages && i < t.RowPages.Count) left.Add($"p{t.RowPages[i]}");
                var prefix = left.Count > 0 ? string.Join(" ", left) + " | " : "";
                Console.Write(prefix);
                for (int c = 0; c < t.ColumnCount; c++)
                {
                    string val = c < cells.Length ? (cells[c] ?? "") : "";
                    if (c > 0) Console.Write(" | ");
                    Console.Write(val.Replace("\n", "\\n"));
                }
                Console.WriteLine();
            }
            Console.WriteLine(new string('-', 80));
            Console.WriteLine("Columns: " + string.Join(" | ", t.ColumnNames));
        }

        // ---------------- Partition wizard ----------------

        static TablePartition? ConfigurePartitionWizard(CoreTable preview, TablePartition? existing)
        {
            Console.WriteLine();
            if (existing == null)
            {
                bool add = AskYesNo("Partition this rule's TABLE output into multiple elements by a key column? (y/n): ");
                if (!add) return null;

                // New config
                var col = AskColumnSelector(preview);

                Console.Write("Attribute name for the key (blank = auto from column name): ");
                var attr = (Console.ReadLine() ?? "").Trim();
                if (attr.Length == 0) attr = null;

                bool keepKey = AskYesNo("Keep the key column as a Row attribute too? (y/n): ");
                bool dropEmpty = AskYesNo("Drop rows where key is EMPTY? (y/n): ");
                string emptyLabel = "(empty)";
                if (!dropEmpty)
                {
                    Console.Write("Label for EMPTY key values [(empty)]: ");
                    var inp = Console.ReadLine() ?? "";
                    if (!string.IsNullOrWhiteSpace(inp)) emptyLabel = inp.Trim();
                }
                bool trim = AskYesNo("Trim whitespace around the key before grouping? (y/n): ");
                bool ci = AskYesNo("Case-insensitive grouping? (y/n): ");

                // Show a tiny preview of distinct keys
                ShowDistinctKeyPreview(preview, col, trim, ci, dropEmpty, emptyLabel);

                return new TablePartition
                {
                    Column = col,
                    AttributeName = attr,
                    KeepKeyInRows = keepKey,
                    DropEmptyKeyRows = dropEmpty,
                    EmptyKeyLabel = emptyLabel,
                    TrimKey = trim,
                    CaseInsensitive = ci
                };
            }
            else
            {
                Console.WriteLine("A partition is already configured for this rule.");
                Console.WriteLine("Choose: 1=Keep as is, 2=Modify, 3=Remove");
                Console.Write("Pick (1/2/3): ");
                var pick = (Console.ReadLine() ?? "").Trim();
                if (pick == "1" || string.IsNullOrEmpty(pick)) return existing;
                if (pick == "3") return null;

                // MODIFY
                var current = existing;

                Console.Write("Change key column? (y/n, blank=keep current): ");
                var chCol = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                var col = current.Column;
                if (chCol is "y" or "yes")
                    col = AskColumnSelector(preview);

                Console.Write($"Attribute name for the key (blank=keep, '-'=auto). Current: {(current.AttributeName ?? "(auto)")} : ");
                var attrIn = (Console.ReadLine() ?? "").Trim();
                string? attr = current.AttributeName;
                if (attrIn == "-") attr = null;
                else if (attrIn.Length > 0) attr = attrIn;

                bool? keepKey = AskYesNoNullable($"Keep key column as Row attribute? (y/n, blank=keep current [{current.KeepKeyInRows}]): ");
                bool? dropEmpty = AskYesNoNullable($"Drop rows where key is EMPTY? (y/n, blank=keep current [{current.DropEmptyKeyRows}]): ");

                string emptyLabel = current.EmptyKeyLabel;
                if (!(dropEmpty ?? current.DropEmptyKeyRows))
                {
                    Console.Write($"Label for EMPTY key values [current: {current.EmptyKeyLabel}]: ");
                    var inp = Console.ReadLine() ?? "";
                    if (!string.IsNullOrWhiteSpace(inp)) emptyLabel = inp.Trim();
                }

                bool? trim = AskYesNoNullable($"Trim whitespace around key? (y/n, blank=keep current [{current.TrimKey}]): ");
                bool? ci = AskYesNoNullable($"Case-insensitive grouping? (y/n, blank=keep current [{current.CaseInsensitive}]): ");

                // Show distinct key preview with (possibly) updated knobs
                ShowDistinctKeyPreview(
                    preview,
                    col,
                    (trim ?? current.TrimKey),
                    (ci ?? current.CaseInsensitive),
                    (dropEmpty ?? current.DropEmptyKeyRows),
                    emptyLabel);

                return new TablePartition
                {
                    Column = col,
                    AttributeName = attr,
                    KeepKeyInRows = keepKey ?? current.KeepKeyInRows,
                    DropEmptyKeyRows = dropEmpty ?? current.DropEmptyKeyRows,
                    EmptyKeyLabel = emptyLabel,
                    TrimKey = trim ?? current.TrimKey,
                    CaseInsensitive = ci ?? current.CaseInsensitive
                };
            }
        }

        static bool? AskYesNoNullable(string prompt)
        {
            Console.Write(prompt);
            var s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) return null;
            if (s is "y" or "yes") return true;
            if (s is "n" or "no") return false;
            return AskYesNoNullable(prompt); // re-ask if invalid
        }

        static void ShowDistinctKeyPreview(
            CoreTable preview,
            ColumnSelector keySelector,
            bool trimKey,
            bool caseInsensitive,
            bool dropEmpty,
            string emptyLabel)
        {
            if (!keySelector.TryResolveIndex(preview, out int keyCol) || keyCol < 0 || keyCol >= preview.ColumnCount)
            {
                Console.WriteLine("(!) Could not resolve the key column on the preview table; partition will still be saved.");
                return;
            }

            var cmp = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var set = new HashSet<string>(cmp);
            var sample = new List<string>();

            for (int i = 0; i < preview.Rows.Count; i++)
            {
                var cells = preview.Rows[i];
                var raw = keyCol < cells.Length ? (cells[keyCol] ?? "") : "";
                var k = trimKey ? raw.Trim() : raw;
                if (string.IsNullOrEmpty(k))
                {
                    if (dropEmpty) continue;
                    k = emptyLabel ?? "";
                }
                if (set.Add(k) && sample.Count < 10)
                    sample.Add(k);
            }

            Console.WriteLine($"Distinct key preview: {set.Count} unique value(s). Sample: {string.Join(" | ", sample)}");
        }
    }
}
