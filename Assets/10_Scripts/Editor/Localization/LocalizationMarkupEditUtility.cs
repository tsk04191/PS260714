using System;

namespace PS260714.Localization.Editor
{
    internal readonly struct LocalizationMarkupEditResult
    {
        public LocalizationMarkupEditResult(
            string text,
            int cursorIndex,
            int selectIndex)
        {
            Text = text ?? string.Empty;
            CursorIndex = cursorIndex;
            SelectIndex = selectIndex;
        }

        public string Text { get; }
        public int CursorIndex { get; }
        public int SelectIndex { get; }
    }

    internal static class LocalizationMarkupEditUtility
    {
        public static LocalizationMarkupEditResult ApplyStyle(
            string source,
            int cursorIndex,
            int selectIndex,
            string styleId)
        {
            string text = source ?? string.Empty;
            if (!LocalizationMarkupDefaults.IsSafeIdentifier(styleId))
                return Unchanged(text, cursorIndex, selectIndex);

            NormalizeSelection(
                text.Length,
                cursorIndex,
                selectIndex,
                out int start,
                out int end);
            string opening = $"[style={styleId.Trim()}]";
            const string closing = "[/style]";
            string edited = text.Substring(0, start) +
                            opening +
                            text.Substring(start, end - start) +
                            closing +
                            text.Substring(end);
            int contentStart = start + opening.Length;
            int contentEnd = contentStart + end - start;
            return new LocalizationMarkupEditResult(
                edited,
                contentEnd,
                contentStart);
        }

        public static LocalizationMarkupEditResult InsertIcon(
            string source,
            int cursorIndex,
            int selectIndex,
            string iconId)
        {
            if (!LocalizationMarkupDefaults.IsSafeIdentifier(iconId))
            {
                return Unchanged(
                    source ?? string.Empty,
                    cursorIndex,
                    selectIndex);
            }

            return ReplaceSelection(
                source,
                cursorIndex,
                selectIndex,
                $"[icon={iconId.Trim()}]");
        }

        public static LocalizationMarkupEditResult InsertLineBreak(
            string source,
            int cursorIndex,
            int selectIndex)
        {
            return ReplaceSelection(
                source,
                cursorIndex,
                selectIndex,
                "[br]");
        }

        public static LocalizationMarkupEditResult InsertNumericArgument(
            string source,
            int cursorIndex,
            int selectIndex,
            string argumentName,
            string numberFormat)
        {
            string token = BuildNumericArgumentToken(
                argumentName,
                numberFormat);
            if (string.IsNullOrEmpty(token))
            {
                return Unchanged(
                    source ?? string.Empty,
                    cursorIndex,
                    selectIndex);
            }

            return ReplaceSelection(
                source,
                cursorIndex,
                selectIndex,
                token);
        }

        public static string BuildNumericArgumentToken(
            string argumentName,
            string numberFormat)
        {
            string name = (argumentName ?? string.Empty).Trim();
            string format = (numberFormat ?? string.Empty).Trim();
            if (!LocalizationMarkupDefaults.IsSafeIdentifier(name) ||
                !IsSafeNumberFormat(format))
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(format)
                ? $"{{{name}}}"
                : $"{{{name}:{format}}}";
        }

        private static LocalizationMarkupEditResult ReplaceSelection(
            string source,
            int cursorIndex,
            int selectIndex,
            string token)
        {
            string text = source ?? string.Empty;
            NormalizeSelection(
                text.Length,
                cursorIndex,
                selectIndex,
                out int start,
                out int end);
            string edited = text.Substring(0, start) +
                            token +
                            text.Substring(end);
            int nextIndex = start + token.Length;
            return new LocalizationMarkupEditResult(
                edited,
                nextIndex,
                nextIndex);
        }

        private static LocalizationMarkupEditResult Unchanged(
            string source,
            int cursorIndex,
            int selectIndex)
        {
            NormalizeSelection(
                source.Length,
                cursorIndex,
                selectIndex,
                out int start,
                out int end);
            return new LocalizationMarkupEditResult(
                source,
                end,
                start);
        }

        private static bool IsSafeNumberFormat(string format)
        {
            for (int index = 0; index < format.Length; index++)
            {
                char character = format[index];
                if (char.IsLetterOrDigit(character) ||
                    character == '#' ||
                    character == '.' ||
                    character == ',' ||
                    character == '%' ||
                    character == '+' ||
                    character == '-' ||
                    character == ';' ||
                    character == '(' ||
                    character == ')' ||
                    character == ' ')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void NormalizeSelection(
            int textLength,
            int cursorIndex,
            int selectIndex,
            out int start,
            out int end)
        {
            int cursor = Math.Clamp(cursorIndex, 0, textLength);
            int selection = Math.Clamp(selectIndex, 0, textLength);
            start = Math.Min(cursor, selection);
            end = Math.Max(cursor, selection);
        }
    }
}
