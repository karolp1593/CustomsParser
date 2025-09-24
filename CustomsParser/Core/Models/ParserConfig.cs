namespace PdfTableMvp.Core
{
    public class ParserConfig
    {
        public string Name { get; set; } = "Unnamed";
        public string Version { get; set; } = "1.0.0";
        public MissingColumnPolicy MissingPolicy { get; set; } = MissingColumnPolicy.Warn;
        public List<RuleDefinition> Rules { get; set; } = new();
    }

    public class RuleDefinition
    {
        public string Name { get; set; } = "Rule";
        public List<StepBase> RuleSteps { get; set; } = new();
    }
}
