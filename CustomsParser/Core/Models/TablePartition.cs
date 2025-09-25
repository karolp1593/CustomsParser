using System;

namespace PdfTableMvp.Core
{
    /// <summary>
    /// Optional per-rule output partition config (table rules only).
    /// Splits a rule's output into multiple sibling XML elements (one per unique key).
    /// </summary>
    public class TablePartition
    {
        /// <summary>Column selector to pick the key column on the FINAL table (after all steps).</summary>
        public ColumnSelector Column { get; set; } = ColumnSelector.ByIndex(0);

        /// <summary>Attribute name to place the key on each partition element. Default = sanitized key column name.</summary>
        public string? AttributeName { get; set; }

        /// <summary>If true, keep the key column as a Row attribute as well. Default: false.</summary>
        public bool KeepKeyInRows { get; set; } = false;

        /// <summary>If true, rows with empty key are dropped. Default: false.</summary>
        public bool DropEmptyKeyRows { get; set; } = false;

        /// <summary>Label used as the key value when key is empty and not dropped. Default: "(empty)".</summary>
        public string EmptyKeyLabel { get; set; } = "(empty)";

        /// <summary>Trim whitespace around the key before grouping. Default: true.</summary>
        public bool TrimKey { get; set; } = true;

        /// <summary>Case-insensitive grouping of key values. Default: true.</summary>
        public bool CaseInsensitive { get; set; } = true;
    }
}
