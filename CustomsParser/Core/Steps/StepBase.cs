using iText.Layout.Element;

namespace PdfTableMvp.Core
{
    public abstract class StepBase
    {
        public bool Enabled { get; set; } = true;
        public string? Comment { get; set; }

        public abstract void Apply(Table t, MissingColumnPolicy policy, Action<string> log);
        public virtual string Describe() => GetType().Name;

        protected int ResolveOrPolicy(Table t, ColumnSelector sel, MissingColumnPolicy policy, Action<string> log)
        {
            int idx = sel.ResolveIndex(t);
            if (idx >= 0) return idx;

            var msg = $"Column not found for selector {sel}.";
            if (policy == MissingColumnPolicy.Skip || policy == MissingColumnPolicy.Warn)
            {
                log(msg + $" [{policy.ToString().ToLower()}]");
                return -1;
            }
            throw new Exception(msg + " [fail]");
        }
    }
}
