using System;
using System.Collections.Generic;
using System.IO;

namespace PS260714.Localization.Editor
{
    public sealed class LocalizationSourceLocale
    {
        public string Locale;
        public string DisplayName;
        public string Fallback;
        public string DefaultFontRole;
    }

    public sealed class LocalizationSourceString
    {
        public string Key;
        public string Context;
        public string FontRole;
        public string Note;
        public readonly Dictionary<string, string> Translations =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class LocalizationSourceModel
    {
        public readonly List<LocalizationSourceLocale> Locales = new();
        public readonly List<LocalizationSourceString> Strings = new();
        public LocalizationCsvDocument LocalesDocument;
        public LocalizationCsvDocument StringsDocument;

        public static LocalizationSourceModel Load(
            string localesPath,
            string stringsPath)
        {
            if (!File.Exists(localesPath))
            {
                throw new FileNotFoundException(
                    "Localization locale source is missing.",
                    localesPath);
            }

            if (!File.Exists(stringsPath))
            {
                throw new FileNotFoundException(
                    "Localization string source is missing.",
                    stringsPath);
            }

            return FromDocuments(
                LocalizationCsv.ReadFile(localesPath),
                LocalizationCsv.ReadFile(stringsPath));
        }

        public static LocalizationSourceModel FromDocuments(
            LocalizationCsvDocument localesDocument,
            LocalizationCsvDocument stringsDocument)
        {
            if (localesDocument == null)
            {
                throw new ArgumentNullException(
                    nameof(localesDocument));
            }
            if (stringsDocument == null)
            {
                throw new ArgumentNullException(
                    nameof(stringsDocument));
            }

            LocalizationSourceModel model = new LocalizationSourceModel
            {
                LocalesDocument = localesDocument,
                StringsDocument = stringsDocument,
            };
            model.ParseLocales();
            model.ParseStrings();
            return model;
        }

        private void ParseLocales()
        {
            Dictionary<string, int> headers =
                LocalesDocument.BuildHeaderMap();
            if (!TryGetRequiredHeader(headers, "locale", out int localeColumn) ||
                !TryGetRequiredHeader(
                    headers,
                    "display_name",
                    out int displayNameColumn) ||
                !TryGetRequiredHeader(
                    headers,
                    "fallback",
                    out int fallbackColumn) ||
                !TryGetRequiredHeader(
                    headers,
                    "default_font_role",
                    out int fontRoleColumn))
            {
                throw new FormatException(
                    "locales.csv requires locale, display_name, fallback and " +
                    "default_font_role headers.");
            }

            for (int row = 1; row < LocalesDocument.RowCount; row++)
            {
                string locale = LocalesDocument.Get(row, localeColumn).Trim();
                if (string.IsNullOrEmpty(locale) && IsEmptyRow(
                    LocalesDocument,
                    row))
                {
                    continue;
                }

                Locales.Add(new LocalizationSourceLocale
                {
                    Locale = locale,
                    DisplayName = LocalesDocument.Get(
                        row,
                        displayNameColumn).Trim(),
                    Fallback = LocalesDocument.Get(
                        row,
                        fallbackColumn).Trim(),
                    DefaultFontRole = LocalesDocument.Get(
                        row,
                        fontRoleColumn).Trim(),
                });
            }
        }

        private void ParseStrings()
        {
            Dictionary<string, int> headers =
                StringsDocument.BuildHeaderMap();
            if (!TryGetRequiredHeader(headers, "key", out int keyColumn) ||
                !TryGetRequiredHeader(
                    headers,
                    "context",
                    out int contextColumn) ||
                !TryGetRequiredHeader(
                    headers,
                    "font_role",
                    out int fontRoleColumn) ||
                !TryGetRequiredHeader(headers, "note", out int noteColumn))
            {
                throw new FormatException(
                    "strings.csv requires key, context, font_role and note " +
                    "headers.");
            }

            Dictionary<string, int> localeColumns =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < Locales.Count; index++)
            {
                LocalizationSourceLocale locale = Locales[index];
                if (headers.TryGetValue(locale.Locale, out int column))
                {
                    localeColumns[locale.Locale] = column;
                }
            }

            for (int row = 1; row < StringsDocument.RowCount; row++)
            {
                string key = StringsDocument.Get(row, keyColumn).Trim();
                if (string.IsNullOrEmpty(key) && IsEmptyRow(
                    StringsDocument,
                    row))
                {
                    continue;
                }

                LocalizationSourceString entry =
                    new LocalizationSourceString
                    {
                        Key = key,
                        Context = StringsDocument.Get(
                            row,
                            contextColumn).Trim(),
                        FontRole = StringsDocument.Get(
                            row,
                            fontRoleColumn).Trim(),
                        Note = StringsDocument.Get(row, noteColumn).Trim(),
                    };

                foreach (KeyValuePair<string, int> pair in localeColumns)
                {
                    entry.Translations[pair.Key] =
                        StringsDocument.Get(row, pair.Value);
                }

                Strings.Add(entry);
            }
        }

        private static bool TryGetRequiredHeader(
            Dictionary<string, int> headers,
            string name,
            out int column)
        {
            return headers.TryGetValue(name, out column);
        }

        private static bool IsEmptyRow(
            LocalizationCsvDocument document,
            int row)
        {
            for (int column = 0;
                 column < document.Rows[row].Count;
                 column++)
            {
                if (!string.IsNullOrWhiteSpace(
                    document.Rows[row][column]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
