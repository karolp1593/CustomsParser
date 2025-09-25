using System.Text;

namespace PdfTableMvp.Core
{
    public enum RouteMatchKind { Exact, Regex }

    public class RouteRule
    {
        public RouteMatchKind Kind { get; set; } = RouteMatchKind.Exact;
        public string Pattern { get; set; } = "";
        public bool CaseInsensitive { get; set; } = true;

        // Now only the target PARSER. We run ALL rules of this parser.
        public string TargetParser { get; set; } = "";

        public override string ToString()
        {
            var kind = Kind == RouteMatchKind.Exact ? "EXACT" : "REGEX";
            var ci = CaseInsensitive ? "ci" : "cs";
            return $"{kind} '{Pattern}' -> {TargetParser} ({ci})";
        }
    }

    public class RouterConfig
    {
        // Which rule in the PARENT parser computes the Tag
        public string TagRuleName { get; set; } = "Tag";

        public List<RouteRule> Routes { get; set; } = new();

        // Optional fallback if nothing matches
        public string? DefaultTargetParser { get; set; }

        // Optional: when running ALL rules of the target parser,
        // skip rules listed here (case-insensitive compare).
        public List<string> ExcludeRules { get; set; } = new();

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tag rule: {TagRuleName}");
            if (Routes.Count == 0) sb.AppendLine("No routes.");
            else
            {
                sb.AppendLine("Routes:");
                for (int i = 0; i < Routes.Count; i++)
                    sb.AppendLine($" {i + 1}) {Routes[i]}");
            }
            if (!string.IsNullOrWhiteSpace(DefaultTargetParser))
                sb.AppendLine($"Default: {DefaultTargetParser}");
            if (ExcludeRules != null && ExcludeRules.Count > 0)
                sb.AppendLine("ExcludeRules: " + string.Join(", ", ExcludeRules));
            return sb.ToString();
        }
    }
}
