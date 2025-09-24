// Core/Serialization/StepJsonConverter.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfTableMvp.Core
{
    /// <summary>
    /// Polymorphic (de)serializer for StepBase:
    /// - writes: { "type": "<ClassNameWithoutStep>", ...props }
    /// - reads: accepts both "<Name>" and "<Name>Step" (case-insensitive)
    /// </summary>
    public sealed class StepJsonConverter : JsonConverter<StepBase>
    {
        private const string Discriminator = "type";

        // Register ALL concrete Step types here:
        private static readonly Type[] AllStepTypes = new[]
        {
            // Filters / keeps
            typeof(KeepTableSectionStep),
            typeof(KeepRowsWhereRegexStep),
            typeof(KeepRowsWhereNotEmptyStep),

            // Global
            typeof(TrimAllStep),

            // Transforms (in-place)
            typeof(TransformTrimStep),
            typeof(TransformReplaceRegexStep),
            typeof(TransformLeftStep),
            typeof(TransformRightStep),
            typeof(TransformCutLastWordsStep),

            // Fill empties / case transforms
            typeof(FillEmptyStep),
            typeof(FillEmptyWithRowIndexStep),
            typeof(FillEmptyWithStaticValueStep),
            typeof(TransformToUpperStep),
            typeof(TransformToLowerStep),
            typeof(TransformToTitleCaseStep),

            // Splits (new columns)
            typeof(SplitOnKeywordStep),
            typeof(SplitAfterCharsStep),
            typeof(SplitCutLastWordsToNewColumnStep),
            typeof(SplitOnRegexDelimiterStep),
            typeof(SplitAfterWordsStep),
            typeof(SplitCutLastCharsToNewColumnStep),


            // Column ops
            typeof(KeepColumnsStep),
            typeof(DropFirstRowStep),
            typeof(RenameColumnsStep),
            typeof(InsertBlankColumnStep),
            typeof(CopyColumnStep),

            // Row ops
            typeof(MergeRowsByGroupStep),

            // Regex extract
            typeof(RegexExtractStep),

            // Scalar (header/single value)
            typeof(ToScalarFromCellStep),
        };

        private static readonly Dictionary<string, Type> TypeByKey = BuildTypeMap();

        private static Dictionary<string, Type> BuildTypeMap()
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in AllStepTypes)
            {
                // Support both keys: "KeepRowsWhereRegexStep" and "KeepRowsWhereRegex"
                var exact = t.Name;                 // e.g. "KeepRowsWhereRegexStep"
                var bare = StripStepSuffix(exact); // e.g. "KeepRowsWhereRegex"

                if (!map.ContainsKey(exact)) map[exact] = t;
                if (!map.ContainsKey(bare)) map[bare] = t;
            }
            return map;
        }

        private static string StripStepSuffix(string s)
        {
            if (s.EndsWith("Step", StringComparison.OrdinalIgnoreCase))
                return s.Substring(0, s.Length - 4);
            return s;
        }

        public override StepBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty(Discriminator, out var typeProp))
                throw new JsonException($"Missing '{Discriminator}' property in step JSON.");

            var typeKey = typeProp.GetString() ?? "";
            var norm = StripStepSuffix(typeKey);

            if (!TypeByKey.TryGetValue(norm, out var concrete))
                throw new JsonException($"Unknown step type '{typeKey}'.");

            // Re-deserialize the same JSON payload into the concrete type
            string raw = doc.RootElement.GetRawText();
            var obj = (StepBase?)JsonSerializer.Deserialize(raw, concrete, Configure(options));
            if (obj == null)
                throw new JsonException($"Failed to deserialize step '{typeKey}'.");
            return obj;
        }

        public override void Write(Utf8JsonWriter writer, StepBase value, JsonSerializerOptions options)
        {
            var t = value.GetType();
            var typeKey = StripStepSuffix(t.Name);

            // Serialize object first to element (no 'using' – JsonElement is a struct wrapper)
            var element = JsonSerializer.SerializeToElement(value, t, Configure(options));

            writer.WriteStartObject();
            writer.WriteString(Discriminator, typeKey);

            // copy all properties from the element
            foreach (var prop in element.EnumerateObject())
            {
                // in case someone adds a 'type' property on a step in the future, avoid duplicate
                if (prop.NameEquals(Discriminator)) continue;
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        private static JsonSerializerOptions Configure(JsonSerializerOptions baseOptions)
        {
            // Ensure this converter is available for nested (de)serializations if needed.
            // Also copy other settings from baseOptions.
            var opts = new JsonSerializerOptions(baseOptions);
            bool has = false;
            foreach (var c in opts.Converters)
            {
                if (c is StepJsonConverter) { has = true; break; }
            }
            if (!has) opts.Converters.Add(new StepJsonConverter());
            return opts;
        }
    }
}
