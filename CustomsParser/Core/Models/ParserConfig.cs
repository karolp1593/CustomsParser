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
        /// <summary>
        /// OPTIONAL: If provided and the rule is a TABLE, split output into multiple elements by the key column.
        /// Ignored for scalar rules.
        /// </summary>
        public TablePartition? Partition { get; set; }
    }
}
