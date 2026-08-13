using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PS260714.Localization.Editor
{
    public enum LocalizationValidationSeverity
    {
        Warning,
        Error,
    }

    public readonly struct LocalizationValidationIssue
    {
        public LocalizationValidationIssue(
            LocalizationValidationSeverity severity,
            string location,
            string message)
        {
            Severity = severity;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public LocalizationValidationSeverity Severity { get; }
        public string Location { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{Severity}: {Location}: {Message}";
        }
    }

    public sealed class LocalizationValidationResult
    {
        private readonly List<LocalizationValidationIssue> issues = new();

        public IReadOnlyList<LocalizationValidationIssue> Issues => issues;
        public int ErrorCount => issues.Count(issue =>
            issue.Severity == LocalizationValidationSeverity.Error);
        public int WarningCount => issues.Count(issue =>
            issue.Severity == LocalizationValidationSeverity.Warning);
        public bool IsValid => ErrorCount == 0;

        public void Error(string location, string message)
        {
            issues.Add(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                location,
                message));
        }

        public void Warning(string location, string message)
        {
            issues.Add(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Warning,
                location,
                message));
        }
    }

    public static class LocalizationValidator
    {
        private const string ReferenceLocale = "en-US";
        private static readonly Regex LocalePattern = new Regex(
            "^[a-z]{2}(?:-[A-Z]{2})?$",
            RegexOptions.CultureInvariant);
        private static readonly Regex KeyPattern = new Regex(
            "^[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*)+$",
            RegexOptions.CultureInvariant);

        private static readonly string[] BuiltInFontRoles =
        {
            "body",
            "title",
            "number",
            "tooltip",
        };

        public static LocalizationValidationResult Validate(
            LocalizationSourceModel model,
            bool validateGlyphs = true)
        {
            LocalizationValidationResult result =
                new LocalizationValidationResult();
            if (model == null)
            {
                result.Error("source", "No localization source was loaded.");
                return result;
            }

            HashSet<string> styles = BuildAllowedStyles();
            HashSet<string> icons = BuildAllowedIcons();
            HashSet<string> fontRoles = BuildAllowedFontRoles();
            ValidateLocales(model, fontRoles, result);
            ValidateStrings(model, styles, icons, fontRoles, result);
            ValidateReferenceTextCollisions(model, result);
            ValidateIconAssets(model, result);
            if (validateGlyphs)
            {
                ValidateGlyphs(model, result);
            }

            return result;
        }

        private static void ValidateLocales(
            LocalizationSourceModel model,
            HashSet<string> fontRoles,
            LocalizationValidationResult result)
        {
            if (model.Locales.Count == 0)
            {
                result.Error("locales.csv", "At least one locale is required.");
                return;
            }

            HashSet<string> localeIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < model.Locales.Count; index++)
            {
                LocalizationSourceLocale locale = model.Locales[index];
                string location = $"locales.csv row {index + 2}";
                if (!LocalePattern.IsMatch(locale.Locale ?? string.Empty))
                {
                    result.Error(
                        location,
                        $"Invalid locale id '{locale.Locale}'. Use xx or xx-YY.");
                }

                if (!localeIds.Add(locale.Locale ?? string.Empty))
                {
                    result.Error(
                        location,
                        $"Duplicate locale '{locale.Locale}'.");
                }

                if (string.IsNullOrWhiteSpace(locale.DisplayName))
                {
                    result.Error(location, "display_name is required.");
                }

                if (string.IsNullOrWhiteSpace(locale.DefaultFontRole) ||
                    !fontRoles.Contains(locale.DefaultFontRole))
                {
                    result.Error(
                        location,
                        $"Unknown default font role " +
                        $"'{locale.DefaultFontRole}'.");
                }
            }

            for (int index = 0; index < model.Locales.Count; index++)
            {
                LocalizationSourceLocale locale = model.Locales[index];
                if (!string.IsNullOrWhiteSpace(locale.Fallback) &&
                    !localeIds.Contains(locale.Fallback))
                {
                    result.Error(
                        $"locales.csv row {index + 2}",
                        $"Fallback locale '{locale.Fallback}' does not exist.");
                }
            }
        }

        private static void ValidateStrings(
            LocalizationSourceModel model,
            HashSet<string> styles,
            HashSet<string> icons,
            HashSet<string> fontRoles,
            LocalizationValidationResult result)
        {
            HashSet<string> keys = new HashSet<string>(
                StringComparer.Ordinal);
            for (int row = 0; row < model.Strings.Count; row++)
            {
                LocalizationSourceString entry = model.Strings[row];
                string location = $"strings.csv row {row + 2}";
                if (!KeyPattern.IsMatch(entry.Key ?? string.Empty))
                {
                    result.Error(
                        location,
                        $"Invalid key '{entry.Key}'. Use lower-case dot paths.");
                }

                if (!keys.Add(entry.Key ?? string.Empty))
                {
                    result.Error(location, $"Duplicate key '{entry.Key}'.");
                }

                if (!string.IsNullOrWhiteSpace(entry.FontRole) &&
                    !fontRoles.Contains(entry.FontRole))
                {
                    result.Error(
                        location,
                        $"Unknown font role '{entry.FontRole}'.");
                }

                HashSet<string> referencePlaceholders = null;
                string referenceLocale = null;
                for (int localeIndex = 0;
                     localeIndex < model.Locales.Count;
                     localeIndex++)
                {
                    string locale = model.Locales[localeIndex].Locale;
                    entry.Translations.TryGetValue(locale, out string text);
                    string translationLocation =
                        $"{location}, {locale}, key {entry.Key}";
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        result.Error(
                            translationLocation,
                            "Translation is missing.");
                        continue;
                    }

                    if (text.IndexOf('<') >= 0 || text.IndexOf('>') >= 0)
                    {
                        result.Error(
                            translationLocation,
                            "Raw TMP angle-bracket tags are forbidden.");
                    }

                    HashSet<string> placeholders = ExtractPlaceholders(
                        text,
                        translationLocation,
                        result);
                    ValidateMarkup(
                        text,
                        translationLocation,
                        styles,
                        icons,
                        result);

                    if (referencePlaceholders == null)
                    {
                        referencePlaceholders = placeholders;
                        referenceLocale = locale;
                    }
                    else if (!referencePlaceholders.SetEquals(placeholders))
                    {
                        result.Error(
                            translationLocation,
                            $"Placeholder set differs from {referenceLocale}. " +
                            $"Expected [{string.Join(", ", referencePlaceholders)}], " +
                            $"found [{string.Join(", ", placeholders)}].");
                    }
                }
            }
        }

        private static void ValidateReferenceTextCollisions(
            LocalizationSourceModel model,
            LocalizationValidationResult result)
        {
            IEnumerable<IGrouping<string, LocalizationSourceString>> groups =
                model.Strings
                    .Where(entry => entry.Translations.TryGetValue(
                        ReferenceLocale,
                        out string referenceText) &&
                        !string.IsNullOrEmpty(referenceText))
                    .GroupBy(
                        entry => entry.Translations[ReferenceLocale],
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1);

            foreach (IGrouping<string, LocalizationSourceString> group in
                     groups)
            {
                List<string> conflicts = new List<string>();
                for (int localeIndex = 0;
                     localeIndex < model.Locales.Count;
                     localeIndex++)
                {
                    string locale = model.Locales[localeIndex].Locale;
                    bool hasTranslationConflict = group
                        .Select(entry => entry.Translations.TryGetValue(
                            locale,
                            out string translation)
                                ? translation
                                : string.Empty)
                        .Distinct(StringComparer.Ordinal)
                        .Skip(1)
                        .Any();
                    if (hasTranslationConflict)
                    {
                        conflicts.Add(locale);
                    }
                }

                if (conflicts.Count == 0)
                {
                    // Translation aliases are valid source data. The runtime
                    // reverse index separately compares FontRole and will skip
                    // aliases whose presentation role is ambiguous.
                    continue;
                }

                string preview = group.Key
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n");
                if (preview.Length > 96)
                {
                    preview = preview.Substring(0, 93) + "...";
                }

                string keys = string.Join(
                    ", ",
                    group.Select(entry => entry.Key).OrderBy(
                        key => key,
                        StringComparer.Ordinal));
                result.Error(
                    $"strings.csv {ReferenceLocale} text '{preview}'",
                    "The same reference text maps to conflicting generated " +
                    $"values ({string.Join(", ", conflicts)}), so a scene " +
                    "TMP cannot be bound safely. Use distinct reference text " +
                    "or make every locale translation " +
                    $"consistent. Keys: {keys}.");
            }
        }

        private static HashSet<string> ExtractPlaceholders(
            string text,
            string location,
            LocalizationValidationResult result)
        {
            HashSet<string> placeholders = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '{' &&
                    index + 1 < text.Length &&
                    text[index + 1] == '{')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}' &&
                    index + 1 < text.Length &&
                    text[index + 1] == '}')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    result.Error(location, "Unmatched closing placeholder brace.");
                    continue;
                }

                if (text[index] != '{')
                {
                    continue;
                }

                int close = text.IndexOf('}', index + 1);
                if (close < 0)
                {
                    result.Error(location, "Unclosed placeholder brace.");
                    break;
                }

                string token = text.Substring(index + 1, close - index - 1);
                int separator = token.IndexOf(':');
                string name = (separator >= 0
                    ? token.Substring(0, separator)
                    : token).Trim();
                if (!LocalizationMarkupDefaults.IsSafeIdentifier(name))
                {
                    result.Error(
                        location,
                        $"Invalid placeholder name '{name}'.");
                }
                else
                {
                    placeholders.Add(name);
                }

                index = close;
            }

            return placeholders;
        }

        private static void ValidateMarkup(
            string text,
            string location,
            HashSet<string> styles,
            HashSet<string> icons,
            LocalizationValidationResult result)
        {
            Stack<string> openStyles = new Stack<string>();
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] != '[')
                {
                    continue;
                }

                int close = text.IndexOf(']', index + 1);
                if (close < 0)
                {
                    result.Error(location, "Unclosed markup bracket.");
                    return;
                }

                string token = text.Substring(index + 1, close - index - 1);
                if (string.Equals(token, "br", StringComparison.Ordinal))
                {
                    index = close;
                    continue;
                }

                if (string.Equals(token, "/style", StringComparison.Ordinal))
                {
                    if (openStyles.Count == 0)
                    {
                        result.Error(location, "[/style] has no opening tag.");
                    }
                    else
                    {
                        openStyles.Pop();
                    }

                    index = close;
                    continue;
                }

                if (token.StartsWith("style=", StringComparison.Ordinal))
                {
                    string id = token.Substring("style=".Length).Trim();
                    if (!styles.Contains(id))
                    {
                        result.Error(location, $"Unknown style '{id}'.");
                    }

                    openStyles.Push(id);
                    index = close;
                    continue;
                }

                if (token.StartsWith("icon=", StringComparison.Ordinal))
                {
                    string id = token.Substring("icon=".Length).Trim();
                    if (!icons.Contains(id))
                    {
                        result.Error(location, $"Unknown icon '{id}'.");
                    }

                    index = close;
                    continue;
                }

                result.Error(location, $"Unknown markup tag '[{token}]'.");
                index = close;
            }

            if (openStyles.Count > 0)
            {
                result.Error(
                    location,
                    $"{openStyles.Count} style tag(s) are not closed.");
            }
        }

        private static HashSet<string> BuildAllowedStyles()
        {
            HashSet<string> result = new HashSet<string>(
                LocalizationMarkupDefaults.StyleIds,
                StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationMarkupCatalog catalog in
                     FindAssets<LocalizationMarkupCatalog>())
            {
                foreach (LocalizationMarkupStyleDefinition style in
                         catalog.Styles)
                {
                    if (style != null &&
                        !string.IsNullOrWhiteSpace(style.Id))
                    {
                        result.Add(style.Id);
                    }
                }
            }

            return result;
        }

        private static HashSet<string> BuildAllowedIcons()
        {
            HashSet<string> result = new HashSet<string>(
                LocalizationMarkupDefaults.IconIds,
                StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationMarkupCatalog catalog in
                     FindAssets<LocalizationMarkupCatalog>())
            {
                foreach (LocalizationIconDefinition icon in catalog.Icons)
                {
                    if (icon != null &&
                        !string.IsNullOrWhiteSpace(icon.Id))
                    {
                        result.Add(icon.Id);
                    }
                }
            }

            return result;
        }

        private static HashSet<string> BuildAllowedFontRoles()
        {
            HashSet<string> result = new HashSet<string>(
                BuiltInFontRoles,
                StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationFontCatalog catalog in
                     FindAssets<LocalizationFontCatalog>())
            {
                foreach (string role in catalog.EnumerateRoles())
                {
                    result.Add(role);
                }
            }

            return result;
        }

        private static void ValidateIconAssets(
            LocalizationSourceModel model,
            LocalizationValidationResult result)
        {
            HashSet<string> usedIcons = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int row = 0; row < model.Strings.Count; row++)
            {
                foreach (string text in
                         model.Strings[row].Translations.Values)
                {
                    CollectIconIds(text, usedIcons);
                }
            }

            if (usedIcons.Count == 0)
            {
                return;
            }

            LocalizationMarkupCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LocalizationMarkupCatalog>(
                    "Assets/07_Runtime/Resources/Localization/" +
                    "LocalizationMarkupCatalog.asset");
            foreach (string iconId in usedIcons.OrderBy(id => id))
            {
                if (catalog == null)
                {
                    result.Warning(
                        $"icon {iconId}",
                        "No Markup Catalog exists. The visible text " +
                        $"fallback '[{iconId.ToUpperInvariant()}]' will be used.");
                    continue;
                }

                if (!catalog.TryGetIcon(iconId, out Sprite sprite) ||
                    sprite == null)
                {
                    result.Warning(
                        $"icon {iconId}",
                        "No Sprite is assigned to this icon. A visible text " +
                        $"fallback '[{catalog.GetIconFallback(iconId)}]' " +
                        "will be used.");
                }
            }
        }

        private static void CollectIconIds(
            string text,
            HashSet<string> result)
        {
            const string prefix = "[icon=";
            int searchFrom = 0;
            while (!string.IsNullOrEmpty(text))
            {
                int start = text.IndexOf(
                    prefix,
                    searchFrom,
                    StringComparison.Ordinal);
                if (start < 0)
                {
                    return;
                }

                int valueStart = start + prefix.Length;
                int close = text.IndexOf(']', valueStart);
                if (close < 0)
                {
                    return;
                }

                string id = text.Substring(valueStart, close - valueStart)
                    .Trim();
                if (LocalizationMarkupDefaults.IsSafeIdentifier(id))
                {
                    result.Add(id);
                }

                searchFrom = close + 1;
            }
        }

        private static void ValidateGlyphs(
            LocalizationSourceModel model,
            LocalizationValidationResult result)
        {
            LocalizationFontCatalog catalog =
                FindAssets<LocalizationFontCatalog>().FirstOrDefault();
            TMP_FontAsset defaultFont = catalog != null
                ? null
                : TMP_Settings.defaultFontAsset;

            if (catalog == null)
            {
                result.Warning(
                    "fonts",
                    "No LocalizationFontCatalog asset exists. Runtime will use " +
                    "a supported DynamicOS font or TMP's default font.");
            }

            for (int localeIndex = 0;
                 localeIndex < model.Locales.Count;
                 localeIndex++)
            {
                string locale = model.Locales[localeIndex].Locale;
                StringBuilder characters = new StringBuilder();
                for (int row = 0; row < model.Strings.Count; row++)
                {
                    LocalizationSourceString entry = model.Strings[row];
                    if (entry.Translations.TryGetValue(locale, out string text))
                    {
                        characters.Append(StripMarkup(text));
                    }
                }

                TMP_FontAsset font = catalog != null
                    ? catalog.Resolve(locale, null, LocalizationService.AutoFontId)
                    : defaultFont;
                if (font == null)
                {
                    result.Warning(
                        $"fonts {locale}",
                        "No resolvable TMP font asset. Configure a font asset or " +
                        "DynamicOS family in LocalizationFontCatalog.");
                    continue;
                }

                if (!font.HasCharacters(
                    characters.ToString(),
                    out uint[] missing,
                    true,
                    false))
                {
                    string preview = string.Concat(
                        missing.Take(16).Select(value =>
                            char.ConvertFromUtf32((int)value)));
                    result.Warning(
                        $"fonts {locale}",
                        $"Font '{font.name}' is missing {missing.Length} glyphs " +
                        $"(sample: {preview}). Configure fallback fonts.");
                }
            }
        }

        private static string StripMarkup(string text)
        {
            StringBuilder result = new StringBuilder(text.Length);
            bool bracket = false;
            bool placeholder = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character == '[')
                {
                    bracket = true;
                    continue;
                }

                if (bracket)
                {
                    if (character == ']')
                    {
                        bracket = false;
                    }

                    continue;
                }

                if (character == '{')
                {
                    placeholder = true;
                    continue;
                }

                if (placeholder)
                {
                    if (character == '}')
                    {
                        placeholder = false;
                    }

                    continue;
                }

                result.Append(character);
            }

            return result.ToString();
        }

        private static IEnumerable<T> FindAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int index = 0; index < guids.Length; index++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }
}
