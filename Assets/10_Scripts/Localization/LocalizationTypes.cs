using System;
using System.Collections.Generic;

namespace PS260714.Localization
{
    /// <summary>
    /// A localized source entry generated from strings.csv.
    /// </summary>
    public readonly struct LocalizationEntry
    {
        public LocalizationEntry(string text, string fontRole)
        {
            Text = text ?? string.Empty;
            FontRole = fontRole ?? string.Empty;
        }

        public string Text { get; }
        public string FontRole { get; }
    }

    /// <summary>
    /// The fully resolved value consumed by a TMP view.
    /// </summary>
    public readonly struct LocalizedMessage
    {
        public LocalizedMessage(
            string key,
            string locale,
            string rawText,
            string text,
            string fontRole)
        {
            Key = key ?? string.Empty;
            Locale = locale ?? string.Empty;
            RawText = rawText ?? string.Empty;
            Text = text ?? string.Empty;
            FontRole = fontRole ?? string.Empty;
        }

        public string Key { get; }
        public string Locale { get; }
        public string RawText { get; }
        public string Text { get; }
        public string FontRole { get; }
    }

    /// <summary>
    /// A named value used by placeholders such as {duration:0.#}.
    /// </summary>
    public readonly struct LocalizationArgument
    {
        public LocalizationArgument(string name, object value)
        {
            Name = name ?? string.Empty;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }
    }

    public readonly struct LocalizationLocaleInfo
    {
        public LocalizationLocaleInfo(
            string locale,
            string displayName,
            string fallback,
            string defaultFontRole)
        {
            Locale = locale ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Fallback = fallback ?? string.Empty;
            DefaultFontRole = defaultFontRole ?? string.Empty;
        }

        public string Locale { get; }
        public string DisplayName { get; }
        public string Fallback { get; }
        public string DefaultFontRole { get; }
    }

    public readonly struct LocalizationFontOption
    {
        public LocalizationFontOption(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    internal sealed class LocalizationArgumentMap
        : Dictionary<string, object>
    {
        public LocalizationArgumentMap()
            : base(StringComparer.Ordinal)
        {
        }

        public LocalizationArgumentMap(
            IReadOnlyDictionary<string, object> arguments)
            : this()
        {
            if (arguments == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in arguments)
            {
                this[pair.Key] = pair.Value;
            }
        }
    }
}
