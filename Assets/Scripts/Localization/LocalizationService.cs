using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PS260714.Localization
{
    /// <summary>
    /// Lightweight localization runtime backed by generated C# dictionaries.
    /// CSV files are editor-only source data and are never parsed in a build.
    /// </summary>
    public static class LocalizationService
    {
        public const string DefaultLocale = "ko-KR";
        public const string AutoFontId = "AUTO";
        public const string LocalePlayerPrefsKey = "Localization.Locale";
        public const string FontPlayerPrefsKey = "Localization.FontId";
        public const string FontCatalogResourcePath =
            "Localization/LocalizationFontCatalog";
        public const string MarkupCatalogResourcePath =
            "Localization/LocalizationMarkupCatalog";

        private static readonly HashSet<string> LoggedWarnings = new();
        private static readonly List<LocalizationFontOption> FontOptions = new();
        private static bool initialized;
        private static string currentLocale;
        private static string currentFontId;
        private static LocalizationFontCatalog fontCatalog;
        private static LocalizationMarkupCatalog markupCatalog;

        public static event Action<string> LocaleChanged;
        public static event Action<string> FontChanged;

        public static string CurrentLocale
        {
            get
            {
                EnsureInitialized();
                return currentLocale;
            }
        }

        public static string CurrentFontId
        {
            get
            {
                EnsureInitialized();
                return currentFontId;
            }
        }

        public static IReadOnlyList<LocalizationLocaleInfo> SupportedLocales =>
            GeneratedLocalizationTables.Locales;

        public static IReadOnlyList<LocalizationFontOption>
            SupportedFontOptions
        {
            get
            {
                EnsureInitialized();
                RebuildFontOptions();
                return FontOptions;
            }
        }

        public static LocalizationFontCatalog FontCatalog
        {
            get
            {
                EnsureInitialized();
                return fontCatalog;
            }
        }

        public static LocalizationMarkupCatalog MarkupCatalog
        {
            get
            {
                EnsureInitialized();
                return markupCatalog;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            initialized = false;
            currentLocale = null;
            currentFontId = null;
            fontCatalog = null;
            markupCatalog = null;
            LoggedWarnings.Clear();
            FontOptions.Clear();
            LocaleChanged = null;
            FontChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            string savedLocale = PlayerPrefs.GetString(
                LocalePlayerPrefsKey,
                DefaultLocale);
            if (!GeneratedLocalizationTables.TryNormalizeLocale(
                savedLocale,
                out currentLocale))
            {
                currentLocale = GeneratedLocalizationTables.TryNormalizeLocale(
                    DefaultLocale,
                    out string normalizedDefault)
                    ? normalizedDefault
                    : GeneratedLocalizationTables.FirstLocale;
            }

            currentFontId = NormalizeFontId(PlayerPrefs.GetString(
                FontPlayerPrefsKey,
                AutoFontId));
            LoadResourceCatalogs();
            if (fontCatalog != null &&
                !fontCatalog.ContainsSelectableFont(currentFontId))
            {
                currentFontId = AutoFontId;
            }
        }

        public static void Configure(
            LocalizationFontCatalog newFontCatalog,
            LocalizationMarkupCatalog newMarkupCatalog)
        {
            EnsureInitialized();
            // A scene can override either Resources catalog independently.
            // Null fields inherit the standard Resources-backed catalog so an
            // unconfigured runtime-added resolver cannot erase the defaults.
            if (newFontCatalog != null)
            {
                fontCatalog = newFontCatalog;
            }

            if (newMarkupCatalog != null)
            {
                markupCatalog = newMarkupCatalog;
            }

            RebuildFontOptions();

            if (fontCatalog != null &&
                !fontCatalog.ContainsSelectableFont(currentFontId))
            {
                SetFont(AutoFontId);
            }
        }

        public static bool SetLocale(string locale, bool save = true)
        {
            EnsureInitialized();
            if (!GeneratedLocalizationTables.TryNormalizeLocale(
                locale,
                out string normalized))
            {
                LogOnce(
                    "locale:" + (locale ?? "<null>"),
                    $"[Localization] Unsupported locale '{locale}'.");
                return false;
            }

            if (string.Equals(
                currentLocale,
                normalized,
                StringComparison.Ordinal))
            {
                return true;
            }

            currentLocale = normalized;
            if (save)
            {
                PlayerPrefs.SetString(LocalePlayerPrefsKey, currentLocale);
                PlayerPrefs.Save();
            }

            LocaleChanged?.Invoke(currentLocale);
            return true;
        }

        public static bool SetFont(string fontId, bool save = true)
        {
            EnsureInitialized();
            string normalized = NormalizeFontId(fontId);
            if (fontCatalog != null &&
                !fontCatalog.ContainsSelectableFont(normalized))
            {
                LogOnce(
                    "font:" + normalized,
                    $"[Localization] Unsupported font id '{fontId}'.");
                return false;
            }

            if (string.Equals(
                currentFontId,
                normalized,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            currentFontId = normalized;
            if (save)
            {
                PlayerPrefs.SetString(FontPlayerPrefsKey, currentFontId);
                PlayerPrefs.Save();
            }

            FontChanged?.Invoke(currentFontId);
            return true;
        }

        public static LocalizationArgument Arg(string name, object value)
        {
            return new LocalizationArgument(name, value);
        }

        public static LocalizedMessage Resolve(
            string key,
            params LocalizationArgument[] arguments)
        {
            LocalizationArgumentMap map = new LocalizationArgumentMap();
            if (arguments != null)
            {
                for (int index = 0; index < arguments.Length; index++)
                {
                    LocalizationArgument argument = arguments[index];
                    if (!string.IsNullOrWhiteSpace(argument.Name))
                    {
                        map[argument.Name] = argument.Value;
                    }
                }
            }

            return ResolveInternal(key, map);
        }

        public static LocalizedMessage Resolve(
            string key,
            IReadOnlyDictionary<string, object> arguments)
        {
            return ResolveInternal(
                key,
                new LocalizationArgumentMap(arguments));
        }

        public static string Get(
            string key,
            params LocalizationArgument[] arguments)
        {
            return Resolve(key, arguments).Text;
        }

        public static string Get(
            string key,
            IReadOnlyDictionary<string, object> arguments)
        {
            return Resolve(key, arguments).Text;
        }

        public static string Format(
            string key,
            params LocalizationArgument[] arguments)
        {
            return Get(key, arguments);
        }

        public static string Format(
            string key,
            IReadOnlyDictionary<string, object> arguments)
        {
            return Get(key, arguments);
        }

        public static string RenderMarkup(string source)
        {
            EnsureInitialized();
            return LocalizationMarkupParser.Render(source, markupCatalog);
        }

        private static LocalizedMessage ResolveInternal(
            string key,
            LocalizationArgumentMap arguments)
        {
            EnsureInitialized();
            string normalizedKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedKey))
            {
                LogOnce(
                    "empty-key",
                    "[Localization] An empty localization key was requested.");
                return new LocalizedMessage(
                    string.Empty,
                    currentLocale,
                    string.Empty,
                    string.Empty,
                    "body");
            }

            if (!TryResolveEntry(
                currentLocale,
                normalizedKey,
                out string resolvedLocale,
                out LocalizationEntry entry))
            {
                LogOnce(
                    "key:" + normalizedKey,
                    $"[Localization] Missing key '{normalizedKey}' in every " +
                    "locale. The key is displayed as a fallback.");
                string fallbackText = "[MISSING:" + normalizedKey + "]";
                return new LocalizedMessage(
                    normalizedKey,
                    currentLocale,
                    fallbackText,
                    LocalizationMarkupParser.Render(fallbackText, markupCatalog),
                    ResolveDefaultFontRole(currentLocale));
            }

            string formatted = FormatNamed(
                normalizedKey,
                entry.Text,
                resolvedLocale,
                arguments);
            string fontRole = string.IsNullOrWhiteSpace(entry.FontRole)
                ? ResolveDefaultFontRole(resolvedLocale)
                : entry.FontRole;
            return new LocalizedMessage(
                normalizedKey,
                resolvedLocale,
                formatted,
                LocalizationMarkupParser.Render(formatted, markupCatalog),
                fontRole);
        }

        private static bool TryResolveEntry(
            string requestedLocale,
            string key,
            out string resolvedLocale,
            out LocalizationEntry entry)
        {
            HashSet<string> visited = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            string candidate = requestedLocale;

            while (!string.IsNullOrWhiteSpace(candidate) &&
                   visited.Add(candidate))
            {
                if (GeneratedLocalizationTables.TryGet(
                    candidate,
                    key,
                    out entry))
                {
                    resolvedLocale = candidate;
                    return true;
                }

                if (!GeneratedLocalizationTables.TryGetLocale(
                    candidate,
                    out LocalizationLocaleInfo localeInfo))
                {
                    break;
                }

                candidate = localeInfo.Fallback;
            }

            if (!visited.Contains(DefaultLocale) &&
                GeneratedLocalizationTables.TryGet(
                    DefaultLocale,
                    key,
                    out entry))
            {
                resolvedLocale = DefaultLocale;
                return true;
            }

            resolvedLocale = requestedLocale;
            entry = default;
            return false;
        }

        private static string ResolveDefaultFontRole(string locale)
        {
            return GeneratedLocalizationTables.TryGetLocale(
                       locale,
                       out LocalizationLocaleInfo localeInfo) &&
                   !string.IsNullOrWhiteSpace(localeInfo.DefaultFontRole)
                ? localeInfo.DefaultFontRole
                : "body";
        }

        private static string FormatNamed(
            string key,
            string template,
            string locale,
            LocalizationArgumentMap arguments)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            CultureInfo culture = ResolveCulture(locale);
            StringBuilder output = new StringBuilder(template.Length + 16);

            for (int index = 0; index < template.Length; index++)
            {
                char character = template[index];
                if (character == '{' &&
                    index + 1 < template.Length &&
                    template[index + 1] == '{')
                {
                    output.Append('{');
                    index++;
                    continue;
                }

                if (character == '}' &&
                    index + 1 < template.Length &&
                    template[index + 1] == '}')
                {
                    output.Append('}');
                    index++;
                    continue;
                }

                if (character != '{')
                {
                    output.Append(character);
                    continue;
                }

                int close = template.IndexOf('}', index + 1);
                if (close < 0)
                {
                    output.Append(character);
                    continue;
                }

                string token = template.Substring(
                    index + 1,
                    close - index - 1);
                int separator = token.IndexOf(':');
                string name = (separator >= 0
                    ? token.Substring(0, separator)
                    : token).Trim();
                string format = separator >= 0
                    ? token.Substring(separator + 1)
                    : null;

                if (!arguments.TryGetValue(name, out object value))
                {
                    output.Append('{').Append(token).Append('}');
                    LogOnce(
                        "arg:" + key + ":" + name,
                        $"[Localization] Key '{key}' requires argument " +
                        $"'{name}'.");
                    index = close;
                    continue;
                }

                string formatted;
                try
                {
                    formatted = value is IFormattable formattable
                        ? formattable.ToString(format, culture)
                        : value?.ToString() ?? string.Empty;
                }
                catch (FormatException)
                {
                    formatted = value?.ToString() ?? string.Empty;
                    LogOnce(
                        "format:" + key + ":" + name,
                        $"[Localization] Invalid format '{format}' for " +
                        $"'{key}:{name}'.");
                }

                output.Append(LocalizationMarkupParser.EscapeArgument(formatted));
                index = close;
            }

            return output.ToString();
        }

        private static CultureInfo ResolveCulture(string locale)
        {
            try
            {
                return CultureInfo.GetCultureInfo(locale);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private static void LoadResourceCatalogs()
        {
            fontCatalog ??= Resources.Load<LocalizationFontCatalog>(
                FontCatalogResourcePath);
            markupCatalog ??= Resources.Load<LocalizationMarkupCatalog>(
                MarkupCatalogResourcePath);
        }

        private static string NormalizeFontId(string fontId)
        {
            return string.IsNullOrWhiteSpace(fontId)
                ? AutoFontId
                : fontId.Trim();
        }

        private static void RebuildFontOptions()
        {
            FontOptions.Clear();
            FontOptions.Add(new LocalizationFontOption(AutoFontId, "Auto"));
            if (fontCatalog == null)
            {
                return;
            }

            IReadOnlyList<LocalizationSelectableFontDefinition> options =
                fontCatalog.SelectableFonts;
            HashSet<string> seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                AutoFontId,
            };
            for (int index = 0; index < options.Count; index++)
            {
                LocalizationSelectableFontDefinition option = options[index];
                if (option == null ||
                    string.IsNullOrWhiteSpace(option.Id) ||
                    !seen.Add(option.Id))
                {
                    continue;
                }

                FontOptions.Add(new LocalizationFontOption(
                    option.Id,
                    string.IsNullOrWhiteSpace(option.DisplayName)
                        ? option.Id
                        : option.DisplayName));
            }
        }

        private static void LogOnce(string id, string message)
        {
            if (LoggedWarnings.Add(id))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
