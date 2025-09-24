using System.Text.Json;
using System.Text.Json.Serialization;


namespace PdfTableMvp.Core
{
    public static class ParserStore
    {
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

        public static bool Exists(string name)
        {
            string root = Path.Combine(Environment.CurrentDirectory, "parsers", Sanitize(name));
            return File.Exists(Path.Combine(root, "parser.json"));
        }

        public static void Save(ParserConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.Name)) throw new ArgumentException("ParserConfig.Name is required.", nameof(cfg));

            string root = Path.Combine(Environment.CurrentDirectory, "parsers", Sanitize(cfg.Name));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "versions"));

            string main = Path.Combine(root, "parser.json");
            string version = Path.Combine(root, "versions", DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");

            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(main, json);
            File.WriteAllText(version, json);
        }

        public static ParserConfig Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

            string root = Path.Combine(Environment.CurrentDirectory, "parsers", Sanitize(name));
            string main = Path.Combine(root, "parser.json");
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

            return Directory.GetDirectories(root)
                .Where(d => File.Exists(Path.Combine(d, "parser.json")))
                .Select(Path.GetFileName)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }
    }
}
