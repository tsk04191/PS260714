using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace PS260714.Localization.Editor
{
    public static class LocalizationCodeGenerator
    {
        private const string ReferenceLocale = "en-US";

        public const string SourceDirectory = "Assets/LocalizationSource";
        public const string LocalesPath =
            SourceDirectory + "/locales.csv";
        public const string StringsPath =
            SourceDirectory + "/strings.csv";
        public const string OutputDirectory =
            "Assets/Scripts/Localization/Generated";
        public const string KeysOutputPath =
            OutputDirectory + "/LocalizationKeys.g.cs";
        public const string TablesOutputPath =
            OutputDirectory + "/LocalizationTables.g.cs";

        private static readonly UTF8Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        public static LocalizationSourceModel LoadSource()
        {
            return LocalizationSourceModel.Load(LocalesPath, StringsPath);
        }

        public static LocalizationValidationResult Validate(
            bool validateGlyphs = true)
        {
            try
            {
                return LocalizationValidator.Validate(
                    LoadSource(),
                    validateGlyphs);
            }
            catch (Exception exception)
            {
                LocalizationValidationResult result =
                    new LocalizationValidationResult();
                result.Error("source", exception.Message);
                return result;
            }
        }

        public static LocalizationValidationResult Generate(
            bool refreshAssetDatabase = true)
        {
            LocalizationSourceModel model;
            try
            {
                model = LoadSource();
            }
            catch (Exception exception)
            {
                LocalizationValidationResult failed =
                    new LocalizationValidationResult();
                failed.Error("source", exception.Message);
                return failed;
            }

            LocalizationValidationResult result =
                LocalizationValidator.Validate(model);
            if (!result.IsValid)
            {
                return result;
            }

            Directory.CreateDirectory(OutputDirectory);
            string hash = ComputeSourceHash();
            bool changed = WriteIfChanged(
                KeysOutputPath,
                GenerateKeys(model));
            changed |= WriteIfChanged(
                TablesOutputPath,
                GenerateTables(model, hash));

            if (changed && refreshAssetDatabase)
            {
                AssetDatabase.ImportAsset(
                    KeysOutputPath,
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    TablesOutputPath,
                    ImportAssetOptions.ForceUpdate);
            }

            return result;
        }

        public static void GenerateForBatchMode()
        {
            LocalizationValidationResult result = Generate();
            foreach (LocalizationValidationIssue issue in result.Issues)
            {
                if (issue.Severity ==
                    LocalizationValidationSeverity.Error)
                {
                    UnityEngine.Debug.LogError(
                        $"[Localization] {issue}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        $"[Localization] {issue}");
                }
            }

            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    $"Localization generation failed with " +
                    $"{result.ErrorCount} error(s).");
            }
        }

        public static bool IsStale(
            out string expectedHash,
            out string generatedHash)
        {
            expectedHash = File.Exists(LocalesPath) && File.Exists(StringsPath)
                ? ComputeSourceHash()
                : string.Empty;
            generatedHash = ReadGeneratedHash();
            return string.IsNullOrEmpty(expectedHash) ||
                   !string.Equals(
                       expectedHash,
                       generatedHash,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static string ComputeSourceHash()
        {
            byte[] localeBytes = File.ReadAllBytes(LocalesPath);
            byte[] stringBytes = File.ReadAllBytes(StringsPath);
            using SHA256 sha = SHA256.Create();
            sha.TransformBlock(
                localeBytes,
                0,
                localeBytes.Length,
                null,
                0);
            byte[] separator = { 0 };
            sha.TransformBlock(separator, 0, separator.Length, null, 0);
            sha.TransformFinalBlock(stringBytes, 0, stringBytes.Length);
            return BitConverter.ToString(sha.Hash ?? Array.Empty<byte>())
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string GenerateKeys(LocalizationSourceModel model)
        {
            StringBuilder output = new StringBuilder();
            AppendHeader(output, "strings.csv");
            output.AppendLine("namespace PS260714.Localization");
            output.AppendLine("{");
            output.AppendLine("    public static class LocalizationKeys");
            output.AppendLine("    {");

            HashSet<string> identifiers = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (LocalizationSourceString entry in model.Strings
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                string baseIdentifier = ToIdentifier(entry.Key);
                string identifier = baseIdentifier;
                int suffix = 2;
                while (!identifiers.Add(identifier))
                {
                    identifier = baseIdentifier + suffix;
                    suffix++;
                }

                output.Append("        public const string ");
                output.Append(identifier);
                output.Append(" = ");
                output.Append(ToLiteral(entry.Key));
                output.AppendLine(";");
            }

            output.AppendLine("    }");
            output.AppendLine("}");
            return output.ToString();
        }

        private static string GenerateTables(
            LocalizationSourceModel model,
            string hash)
        {
            StringBuilder output = new StringBuilder(16384);
            AppendHeader(output, "locales.csv and strings.csv");
            output.AppendLine("using System;");
            output.AppendLine("using System.Collections.Generic;");
            output.AppendLine();
            output.AppendLine("namespace PS260714.Localization");
            output.AppendLine("{");
            output.AppendLine("    public static class GeneratedLocalizationTables");
            output.AppendLine("    {");
            output.Append("        public const string SourceHash = ");
            output.Append(ToLiteral(hash));
            output.AppendLine(";");
            output.Append("        public const string ReferenceLocale = ");
            output.Append(ToLiteral(ReferenceLocale));
            output.AppendLine(";");
            output.AppendLine();
            output.AppendLine("        private static readonly LocalizationLocaleInfo[] LocaleData =");
            output.AppendLine("        {");
            for (int index = 0; index < model.Locales.Count; index++)
            {
                LocalizationSourceLocale locale = model.Locales[index];
                output.Append("            new LocalizationLocaleInfo(");
                output.Append(ToLiteral(locale.Locale)).Append(", ");
                output.Append(ToLiteral(locale.DisplayName)).Append(", ");
                output.Append(ToLiteral(locale.Fallback)).Append(", ");
                output.Append(ToLiteral(locale.DefaultFontRole));
                output.AppendLine("),");
            }

            output.AppendLine("        };");
            output.AppendLine();
            output.AppendLine("        private static readonly IReadOnlyList<LocalizationLocaleInfo>");
            output.AppendLine("            LocaleView = Array.AsReadOnly(LocaleData);");
            output.AppendLine();

            Dictionary<string, string> localeVariables =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedVariables = new HashSet<string>(
                StringComparer.Ordinal);
            for (int localeIndex = 0;
                 localeIndex < model.Locales.Count;
                 localeIndex++)
            {
                string locale = model.Locales[localeIndex].Locale;
                string variableBase = ToIdentifier(locale);
                string variable = variableBase;
                int suffix = 2;
                while (!usedVariables.Add(variable))
                {
                    variable = variableBase + suffix;
                    suffix++;
                }

                localeVariables[locale] = variable;
                output.AppendLine("        private static readonly Dictionary<string, LocalizationEntry>");
                output.Append("            ").Append(variable);
                output.AppendLine(" = new Dictionary<string, LocalizationEntry>(");
                output.AppendLine("                StringComparer.Ordinal)");
                output.AppendLine("            {");
                foreach (LocalizationSourceString entry in model.Strings)
                {
                    entry.Translations.TryGetValue(locale, out string text);
                    output.Append("                { ");
                    output.Append(ToLiteral(entry.Key));
                    output.Append(", new LocalizationEntry(");
                    output.Append(ToLiteral(text ?? string.Empty));
                    output.Append(", ");
                    output.Append(ToLiteral(entry.FontRole));
                    output.AppendLine(") },");
                }

                output.AppendLine("            };");
                output.AppendLine();
            }

            AppendReferenceLookupData(
                output,
                localeVariables);
            AppendLookupMethods(output, model, localeVariables);
            output.AppendLine("    }");
            output.AppendLine("}");
            return output.ToString();
        }

        private static void AppendReferenceLookupData(
            StringBuilder output,
            Dictionary<string, string> localeVariables)
        {
            output.AppendLine(
                "        private static readonly IReadOnlyDictionary<string, LocalizationEntry>");
            output.AppendLine("            ReferenceEntryView =");
            if (localeVariables.TryGetValue(
                    ReferenceLocale,
                    out string referenceVariable))
            {
                output.Append("                ");
                output.Append(referenceVariable);
                output.AppendLine(";");
            }
            else
            {
                output.AppendLine(
                    "                new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);");
            }

            output.AppendLine();
            output.AppendLine(
                "        private static readonly Dictionary<string, string>");
            output.AppendLine(
                "            ReferenceTextToUniqueKey = BuildReferenceTextIndex();");
            output.AppendLine();
            output.AppendLine(
                "        private static Dictionary<string, string> BuildReferenceTextIndex()");
            output.AppendLine("        {");
            output.AppendLine(
                "            Dictionary<string, string> index = new Dictionary<string, string>(");
            output.AppendLine("                StringComparer.Ordinal);");
            output.AppendLine(
                "            Dictionary<string, string> representativeKeys =");
            output.AppendLine(
                "                new Dictionary<string, string>(StringComparer.Ordinal);");
            output.AppendLine(
                "            HashSet<string> ambiguousTexts = new HashSet<string>(");
            output.AppendLine("                StringComparer.Ordinal);");
            output.AppendLine(
                "            foreach (KeyValuePair<string, LocalizationEntry> pair in");
            output.AppendLine("                     ReferenceEntryView)");
            output.AppendLine("            {");
            output.AppendLine("                string sourceText = pair.Value.Text;");
            output.AppendLine(
                "                if (string.IsNullOrEmpty(sourceText) ||");
            output.AppendLine("                    ambiguousTexts.Contains(sourceText))");
            output.AppendLine("                {");
            output.AppendLine("                    continue;");
            output.AppendLine("                }");
            output.AppendLine();
            output.AppendLine(
                "                if (!representativeKeys.TryGetValue(");
            output.AppendLine("                        sourceText,");
            output.AppendLine("                        out string representativeKey))");
            output.AppendLine("                {");
            output.AppendLine(
                "                    representativeKeys.Add(sourceText, pair.Key);");
            output.AppendLine("                    index.Add(sourceText, pair.Key);");
            output.AppendLine("                }");
            output.AppendLine(
                "                else if (!HaveEquivalentEntries(");
            output.AppendLine("                             representativeKey,");
            output.AppendLine("                             pair.Key))");
            output.AppendLine("                {");
            output.AppendLine("                    index.Remove(sourceText);");
            output.AppendLine("                    ambiguousTexts.Add(sourceText);");
            output.AppendLine("                }");
            output.AppendLine("            }");
            output.AppendLine();
            output.AppendLine("            return index;");
            output.AppendLine("        }");
            output.AppendLine();
            output.AppendLine(
                "        private static bool HaveEquivalentEntries(");
            output.AppendLine("            string leftKey,");
            output.AppendLine("            string rightKey)");
            output.AppendLine("        {");
            output.AppendLine(
                "            for (int index = 0; index < LocaleData.Length; index++)");
            output.AppendLine("            {");
            output.AppendLine("                string locale = LocaleData[index].Locale;");
            output.AppendLine("                if (!TryGet(locale, leftKey, out LocalizationEntry left) ||");
            output.AppendLine("                    !TryGet(locale, rightKey, out LocalizationEntry right) ||");
            output.AppendLine("                    !string.Equals(");
            output.AppendLine("                        left.Text,");
            output.AppendLine("                        right.Text,");
            output.AppendLine("                        StringComparison.Ordinal) ||");
            output.AppendLine("                    !string.Equals(");
            output.AppendLine("                        left.FontRole,");
            output.AppendLine("                        right.FontRole,");
            output.AppendLine("                        StringComparison.Ordinal))");
            output.AppendLine("                {");
            output.AppendLine("                    return false;");
            output.AppendLine("                }");
            output.AppendLine("            }");
            output.AppendLine();
            output.AppendLine("            return true;");
            output.AppendLine("        }");
            output.AppendLine();
        }

        private static void AppendLookupMethods(
            StringBuilder output,
            LocalizationSourceModel model,
            Dictionary<string, string> localeVariables)
        {
            output.AppendLine("        public static IReadOnlyList<LocalizationLocaleInfo> Locales =>");
            output.AppendLine("            LocaleView;");
            output.AppendLine();
            output.AppendLine("        public static string FirstLocale =>");
            output.AppendLine("            LocaleData.Length > 0 ? LocaleData[0].Locale : string.Empty;");
            output.AppendLine();
            output.AppendLine(
                "        public static IReadOnlyDictionary<string, LocalizationEntry>");
            output.AppendLine("            ReferenceEntries => ReferenceEntryView;");
            output.AppendLine();
            output.AppendLine(
                "        public static bool TryGetUniqueKeyByReferenceText(");
            output.AppendLine("            string sourceText,");
            output.AppendLine("            out string key)");
            output.AppendLine("        {");
            output.AppendLine("            if (sourceText != null &&");
            output.AppendLine(
                "                ReferenceTextToUniqueKey.TryGetValue(sourceText, out key))");
            output.AppendLine("            {");
            output.AppendLine("                return true;");
            output.AppendLine("            }");
            output.AppendLine();
            output.AppendLine("            key = string.Empty;");
            output.AppendLine("            return false;");
            output.AppendLine("        }");
            output.AppendLine();
            output.AppendLine("        public static bool TryNormalizeLocale(");
            output.AppendLine("            string locale,");
            output.AppendLine("            out string normalized)");
            output.AppendLine("        {");
            output.AppendLine("            for (int index = 0; index < LocaleData.Length; index++)");
            output.AppendLine("            {");
            output.AppendLine("                if (string.Equals(");
            output.AppendLine("                    LocaleData[index].Locale,");
            output.AppendLine("                    locale,");
            output.AppendLine("                    StringComparison.OrdinalIgnoreCase))");
            output.AppendLine("                {");
            output.AppendLine("                    normalized = LocaleData[index].Locale;");
            output.AppendLine("                    return true;");
            output.AppendLine("                }");
            output.AppendLine("            }");
            output.AppendLine();
            output.AppendLine("            normalized = string.Empty;");
            output.AppendLine("            return false;");
            output.AppendLine("        }");
            output.AppendLine();
            output.AppendLine("        public static bool TryGetLocale(");
            output.AppendLine("            string locale,");
            output.AppendLine("            out LocalizationLocaleInfo localeInfo)");
            output.AppendLine("        {");
            output.AppendLine("            for (int index = 0; index < LocaleData.Length; index++)");
            output.AppendLine("            {");
            output.AppendLine("                if (string.Equals(");
            output.AppendLine("                    LocaleData[index].Locale,");
            output.AppendLine("                    locale,");
            output.AppendLine("                    StringComparison.OrdinalIgnoreCase))");
            output.AppendLine("                {");
            output.AppendLine("                    localeInfo = LocaleData[index];");
            output.AppendLine("                    return true;");
            output.AppendLine("                }");
            output.AppendLine("            }");
            output.AppendLine();
            output.AppendLine("            localeInfo = default;");
            output.AppendLine("            return false;");
            output.AppendLine("        }");
            output.AppendLine();
            output.AppendLine("        public static bool TryGet(");
            output.AppendLine("            string locale,");
            output.AppendLine("            string key,");
            output.AppendLine("            out LocalizationEntry entry)");
            output.AppendLine("        {");
            for (int index = 0; index < model.Locales.Count; index++)
            {
                string locale = model.Locales[index].Locale;
                output.AppendLine("            if (string.Equals(");
                output.Append("                locale, ").Append(ToLiteral(locale));
                output.AppendLine(",");
                output.AppendLine("                StringComparison.OrdinalIgnoreCase))");
                output.AppendLine("            {");
                output.Append("                return ");
                output.Append(localeVariables[locale]);
                output.AppendLine(".TryGetValue(key, out entry);");
                output.AppendLine("            }");
                output.AppendLine();
            }

            output.AppendLine("            entry = default;");
            output.AppendLine("            return false;");
            output.AppendLine("        }");
        }

        private static void AppendHeader(StringBuilder output, string source)
        {
            output.AppendLine("// <auto-generated>");
            output.Append("// Generated from Assets/LocalizationSource/");
            output.AppendLine(source + ".");
            output.AppendLine("// Edit the CSV source, not this file.");
            output.AppendLine("// </auto-generated>");
        }

        private static string ToIdentifier(string key)
        {
            StringBuilder result = new StringBuilder();
            bool capitalize = true;
            for (int index = 0; index < (key ?? string.Empty).Length; index++)
            {
                char character = key[index];
                if (!char.IsLetterOrDigit(character))
                {
                    capitalize = true;
                    continue;
                }

                result.Append(capitalize
                    ? char.ToUpperInvariant(character)
                    : character);
                capitalize = false;
            }

            if (result.Length == 0)
            {
                result.Append("Key");
            }

            if (char.IsDigit(result[0]))
            {
                result.Insert(0, '_');
            }

            return result.ToString();
        }

        private static string ToLiteral(string value)
        {
            StringBuilder result = new StringBuilder();
            result.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '"':
                        result.Append("\\\"");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    default:
                        if (character < 32 || character > 126)
                        {
                            result.Append("\\u");
                            result.Append(((int)character).ToString("X4"));
                        }
                        else
                        {
                            result.Append(character);
                        }

                        break;
                }
            }

            result.Append('"');
            return result.ToString();
        }

        private static bool WriteIfChanged(string path, string contents)
        {
            if (File.Exists(path) &&
                string.Equals(
                    File.ReadAllText(path),
                    contents,
                    StringComparison.Ordinal))
            {
                return false;
            }

            File.WriteAllText(path, contents, Utf8WithoutBom);
            return true;
        }

        private static string ReadGeneratedHash()
        {
            if (!File.Exists(TablesOutputPath))
            {
                return string.Empty;
            }

            const string marker = "public const string SourceHash = \"";
            string contents = File.ReadAllText(TablesOutputPath);
            int start = contents.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }

            start += marker.Length;
            int end = contents.IndexOf('"', start);
            return end > start
                ? contents.Substring(start, end - start)
                : string.Empty;
        }
    }
}
