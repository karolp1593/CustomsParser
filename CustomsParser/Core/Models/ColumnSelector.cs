using System.Text.RegularExpressions;

namespace PdfTableMvp.Core
{
    public enum ColumnSelectorKind
    {
        Index,
        Name,
        NameRegex,
        Last
    }

    public class ColumnSelector
    {
        // Potrzebne dla System.Text.Json
        public ColumnSelector() { }

        // Dane serializowalne
        public ColumnSelectorKind Kind { get; set; } = ColumnSelectorKind.Index;
        public int Index { get; set; } = -1;
        public string? Name { get; set; }
        public string? Pattern { get; set; }
        public bool CaseInsensitive { get; set; } = false;

        // Fabryki jak dotąd
        public static ColumnSelector ByIndex(int i) =>
            new ColumnSelector { Kind = ColumnSelectorKind.Index, Index = i };

        public static ColumnSelector ByName(string name) =>
            new ColumnSelector { Kind = ColumnSelectorKind.Name, Name = name };

        public static ColumnSelector ByNameRegex(string pattern) =>
            new ColumnSelector { Kind = ColumnSelectorKind.NameRegex, Pattern = pattern };

        public static ColumnSelector Last() =>
            new ColumnSelector { Kind = ColumnSelectorKind.Last };

        /// <summary>
        /// Spróbuj rozwiązać na indeks kolumny. Zwraca true/false; brak rzutów.
        /// </summary>
        public bool TryResolveIndex(Table t, out int resolved)
        {
            resolved = 0;
            if (t == null || t.ColumnCount <= 0) return false;

            switch (Kind)
            {
                case ColumnSelectorKind.Index:
                    if (Index >= 0 && Index < t.ColumnCount) { resolved = Index; return true; }
                    return false;

                case ColumnSelectorKind.Name:
                    if (string.IsNullOrEmpty(Name)) return false;
                    for (int i = 0; i < t.ColumnCount; i++)
                    {
                        var n = t.ColumnNames[i] ?? "";
                        if (string.Equals(n, Name, StringComparison.OrdinalIgnoreCase))
                        { resolved = i; return true; }
                    }
                    return false;

                case ColumnSelectorKind.NameRegex:
                    if (string.IsNullOrEmpty(Pattern)) return false;
                    var rx = new Regex(Pattern, CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
                    for (int i = 0; i < t.ColumnCount; i++)
                    {
                        var n = t.ColumnNames[i] ?? "";
                        if (rx.IsMatch(n)) { resolved = i; return true; }
                    }
                    return false;

                case ColumnSelectorKind.Last:
                    resolved = Math.Max(0, t.ColumnCount - 1);
                    return t.ColumnCount > 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Rozwiąż na indeks kolumny. Jeśli się nie uda, zwraca sensowny fallback
        /// (ostatnia kolumna lub 0). Uwaga: nie rzuca wyjątków.
        /// </summary>
        public int ResolveIndex(Table t)
        {
            if (TryResolveIndex(t, out var i)) return i;

            // fallback: ostatnia dostępna kolumna (lub 0, gdy brak kolumn)
            if (t != null && t.ColumnCount > 0) return t.ColumnCount - 1;
            return 0;
        }

        public override string ToString()
        {
            return Kind switch
            {
                ColumnSelectorKind.Index => $"Index({Index})",
                ColumnSelectorKind.Name => $"Name(\"{Name}\")",
                ColumnSelectorKind.NameRegex => $"NameRegex(\"{Pattern}\")",
                ColumnSelectorKind.Last => "Last()",
                _ => "Unknown"
            };
        }
    }
}
