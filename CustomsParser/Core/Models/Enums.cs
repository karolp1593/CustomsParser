namespace PdfTableMvp.Core
{
    public enum MissingColumnPolicy { Skip, Warn, Fail }
    public enum FillDirection { Previous, Next }
    public enum MergeJoinStrategy { ConcatSpace, ConcatNewline, FirstNonEmpty, LastNonEmpty }
}
