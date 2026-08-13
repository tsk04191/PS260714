using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PS260714.Localization
{
    /// <summary>
    /// Converts the deliberately small localization markup language to safe TMP
    /// rich text. Raw TMP tags are displayed with full-width angle brackets and
    /// can therefore never inject TMP commands.
    /// </summary>
    public static class LocalizationMarkupParser
    {
        private const string StylePrefix = "[style=";
        private const string IconPrefix = "[icon=";
        private const string StyleClose = "[/style]";
        private const string LineBreak = "[br]";

        public static string Render(
            string source,
            LocalizationMarkupCatalog catalog = null)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            StringBuilder output = new StringBuilder(source.Length + 24);
            Stack<string> styleClosers = new Stack<string>();

            int index = 0;
            while (index < source.Length)
            {
                if (Matches(source, index, LineBreak))
                {
                    output.Append('\n');
                    index += LineBreak.Length;
                    continue;
                }

                if (Matches(source, index, StyleClose))
                {
                    if (styleClosers.Count > 0)
                    {
                        output.Append(styleClosers.Pop());
                    }
                    else
                    {
                        AppendSafeLiteral(output, StyleClose);
                    }

                    index += StyleClose.Length;
                    continue;
                }

                if (Matches(source, index, StylePrefix) &&
                    TryReadTagValue(
                        source,
                        index + StylePrefix.Length,
                        out string styleId,
                        out int styleEnd) &&
                    TryBuildStyleTags(
                        catalog,
                        styleId,
                        out string opening,
                        out string closing))
                {
                    output.Append(opening);
                    styleClosers.Push(closing);
                    index = styleEnd + 1;
                    continue;
                }

                if (Matches(source, index, IconPrefix) &&
                    TryReadTagValue(
                        source,
                        index + IconPrefix.Length,
                        out string iconId,
                        out int iconEnd))
                {
                    if (TryResolveIcon(
                        catalog,
                        iconId,
                        out string spriteKey))
                    {
                        output.Append("<sprite name=\"");
                        output.Append(spriteKey);
                        output.Append("\">");
                    }
                    else
                    {
                        AppendIconFallback(output, catalog, iconId);
                    }

                    index = iconEnd + 1;
                    continue;
                }

                AppendSafeCharacter(output, source[index]);
                index++;
            }

            while (styleClosers.Count > 0)
            {
                output.Append(styleClosers.Pop());
            }

            return output.ToString();
        }

        /// <summary>
        /// Prevents a runtime argument from injecting either TMP or localization
        /// markup. Translation text itself is parsed separately by Render.
        /// </summary>
        public static string EscapeArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            StringBuilder output = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                switch (value[index])
                {
                    case '[':
                        output.Append('\uFF3B');
                        break;
                    case ']':
                        output.Append('\uFF3D');
                        break;
                    default:
                        AppendSafeCharacter(output, value[index]);
                        break;
                }
            }

            return output.ToString();
        }

        private static bool TryBuildStyleTags(
            LocalizationMarkupCatalog catalog,
            string styleId,
            out string opening,
            out string closing)
        {
            Color color;
            bool bold;
            bool italic;
            bool underline;
            bool found = catalog != null
                ? catalog.TryGetStyle(
                    styleId,
                    out color,
                    out bold,
                    out italic,
                    out underline)
                : LocalizationMarkupDefaults.TryGetStyle(
                    styleId,
                    out color,
                    out bold,
                    out italic,
                    out underline);

            if (!found)
            {
                opening = string.Empty;
                closing = string.Empty;
                return false;
            }

            StringBuilder openBuilder = new StringBuilder();
            StringBuilder closeBuilder = new StringBuilder();
            openBuilder.Append("<color=#");
            openBuilder.Append(ColorUtility.ToHtmlStringRGBA(color));
            openBuilder.Append('>');

            if (bold)
            {
                openBuilder.Append("<b>");
                closeBuilder.Insert(0, "</b>");
            }

            if (italic)
            {
                openBuilder.Append("<i>");
                closeBuilder.Insert(0, "</i>");
            }

            if (underline)
            {
                openBuilder.Append("<u>");
                closeBuilder.Insert(0, "</u>");
            }

            closeBuilder.Append("</color>");
            opening = openBuilder.ToString();
            closing = closeBuilder.ToString();
            return true;
        }

        private static bool TryResolveIcon(
            LocalizationMarkupCatalog catalog,
            string iconId,
            out string spriteKey)
        {
            if (catalog != null)
            {
                return catalog.TryGetRenderableIcon(
                    iconId,
                    out spriteKey);
            }

            spriteKey = string.Empty;
            return false;
        }

        private static void AppendIconFallback(
            StringBuilder output,
            LocalizationMarkupCatalog catalog,
            string iconId)
        {
            string fallback = catalog != null
                ? catalog.GetIconFallback(iconId)
                : LocalizationMarkupDefaults.GetIconFallback(iconId);
            output.Append('[');
            AppendSafeLiteral(output, fallback);
            output.Append(']');
        }

        private static bool TryReadTagValue(
            string source,
            int valueStart,
            out string value,
            out int closingBracket)
        {
            closingBracket = source.IndexOf(']', valueStart);
            if (closingBracket < valueStart)
            {
                value = string.Empty;
                return false;
            }

            value = source.Substring(
                valueStart,
                closingBracket - valueStart).Trim();
            return LocalizationMarkupDefaults.IsSafeIdentifier(value);
        }

        private static bool Matches(string source, int index, string token)
        {
            if (index + token.Length > source.Length)
            {
                return false;
            }

            return string.CompareOrdinal(
                source,
                index,
                token,
                0,
                token.Length) == 0;
        }

        private static void AppendSafeLiteral(
            StringBuilder output,
            string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                AppendSafeCharacter(output, value[index]);
            }
        }

        private static void AppendSafeCharacter(
            StringBuilder output,
            char character)
        {
            switch (character)
            {
                case '<':
                    output.Append('\uFF1C');
                    break;
                case '>':
                    output.Append('\uFF1E');
                    break;
                default:
                    output.Append(character);
                    break;
            }
        }
    }
}
