using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfTableMvp.Core
{
    public static class ParserStore
    {
        private static string RootFor(string name)
            => Path.Combine(Environment.CurrentDirectory, "parsers", Sanitize(name));

        private static string ParserJsonPath(string name)
            => Path.Combine(RootFor(name), "parser.json");

        private static string RouterJsonPath(string name)
            => Path.Combine(RootFor(name), "router.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(),
                new StepJsonConverter()
            }
        };

        private static readonly JsonSerializerOptions RouterJsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public static bool Exists(string name)
            => File.Exists(ParserJsonPath(name));

        public static void Save(ParserConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.Name))
                throw new ArgumentException("ParserConfig.Name is required.", nameof(cfg));

            string root = RootFor(cfg.Name);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "versions"));

            string main = ParserJsonPath(cfg.Name);
            string version = Path.Combine(root, "versions", DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");

            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(main, json);
            File.WriteAllText(version, json);
        }

        public static ParserConfig Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            string main = ParserJsonPath(name);
            if (!File.Exists(main)) throw new FileNotFoundException("parser.json not found for: " + name, main);

            var json = File.ReadAllText(main);
            var cfg = JsonSerializer.Deserialize<ParserConfig>(json, JsonOpts);
            if (cfg == null) throw new Exception("Invalid parser.json (deserialized to null).");
            if (cfg.Rules == null) cfg.Rules = new List<RuleDefinition>();
            return cfg;
        }

        public static List<string> ListParsers()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "parsers");
            if (!Directory.Exists(root)) return new List<string>();

            // Avoid nullability warning by coalescing null to empty
            return Directory.GetDirectories(root)
                .Where(d => File.Exists(Path.Combine(d, "parser.json")))
                .Select(d => Path.GetFileName(d) ?? string.Empty)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ---- Router I/O ----

        public static bool RouterExists(string parserName)
            => File.Exists(RouterJsonPath(parserName));

        public static RouterConfig LoadRouter(string parserName)
        {
            var path = RouterJsonPath(parserName);
            if (!File.Exists(path)) return new RouterConfig(); // empty config
            var json = File.ReadAllText(path);
            var rc = JsonSerializer.Deserialize<RouterConfig>(json, RouterJsonOpts) ?? new RouterConfig();
            if (rc.Routes == null) rc.Routes = new List<RouteRule>();
            if (rc.ExcludeRules == null) rc.ExcludeRules = new List<string>();
            if (string.IsNullOrWhiteSpace(rc.TagRuleName)) rc.TagRuleName = "Tag";
            return rc;
        }

        public static void SaveRouter(string parserName, RouterConfig router)
        {
            if (string.IsNullOrWhiteSpace(parserName)) throw new ArgumentException("parserName required");
            Directory.CreateDirectory(RootFor(parserName));
            var json = JsonSerializer.Serialize(router, RouterJsonOpts);
            File.WriteAllText(RouterJsonPath(parserName), json);
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }
    }
}
