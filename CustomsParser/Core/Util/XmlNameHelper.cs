using System;

namespace PdfTableMvp.Core
{
    internal static class XmlNameHelper
    {
        public static string MakeElementName(string? name)
        {
            var s = Normalize(name, "Element");
            // Element names cannot start with "xml" (case-insensitive)
            if (s.StartsWith("xml", StringComparison.OrdinalIgnoreCase)) s = "_" + s;
            return s;
        }

        public static string MakeAttributeName(string? name)
        {
            return Normalize(name, "attr");
        }

        private static string Normalize(string? name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) name = fallback;

            var sb = new System.Text.StringBuilder(name.Length + 4);

            // XML name rules (simplified & safe):
            // - First char: letter, '_' or ':' (we don't use ':' but it's allowed)
            // - Next chars: letters, digits, '_', '-', '.'
            char first = name[0];
            if (!IsNameStartChar(first)) sb.Append('_');

            foreach (var ch in name)
                sb.Append(IsNameChar(ch) ? ch : '_');

            var s = sb.ToString();
            return s.Length == 0 ? fallback : s;
        }

        private static bool IsNameStartChar(char c)
            => char.IsLetter(c) || c == '_' || c == ':';

        private static bool IsNameChar(char c)
            => IsNameStartChar(c) || char.IsDigit(c) || c == '-' || c == '.';
    }
}