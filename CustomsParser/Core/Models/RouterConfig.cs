using System.Text;

namespace PdfTableMvp.Core
{
    public enum RouteMatchKind { Exact, Regex }

    public class RouteRule
    {
        public RouteMatchKind Kind { get; set; } = RouteMatchKind.Exact;
        public string Pattern { get; set; } = "";
        public bool CaseInsensitive { get; set; } = true;
        public string TargetParser { get; set; } = "";
        public string TargetRule { get; set; } = "Main";

        public override string ToString()
        {
            var kind = Kind == RouteMatchKind.Exact ? "EXACT" : "REGEX";
            var ci = CaseInsensitive ? "ci" : "cs";
            return $"{kind} '{Pattern}' -> {TargetParser}/{TargetRule} ({ci})";
        }
    }

    public class RouterConfig
    {
        public string TagRuleName { get; set; } = "Tag";
        public List<RouteRule> Routes { get; set; } = new();

        // Optional fallback if nothing matches
        public string? DefaultTargetParser { get; set; }
        public string? DefaultTargetRule { get; set; }

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
                sb.AppendLine($"Default: {DefaultTargetParser}/{DefaultTargetRule}");
            return sb.ToString();
        }
    }
}
