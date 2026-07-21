using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PS260714.Localization.Editor
{
    public sealed class LocalizationCsvDocument
    {
        private readonly List<List<string>> rows = new();

        public List<List<string>> Rows => rows;
        public int RowCount => rows.Count;

        public int ColumnCount
        {
            get
            {
                int maximum = 0;
                for (int index = 0; index < rows.Count; index++)
                {
                    maximum = Math.Max(maximum, rows[index].Count);
                }

                return maximum;
            }
        }

        public string Get(int row, int column)
        {
            if (row < 0 || row >= rows.Count ||
                column < 0 || column >= rows[row].Count)
            {
                return string.Empty;
            }

            return rows[row][column] ?? string.Empty;
        }

        public void Set(int row, int column, string value)
        {
            while (rows.Count <= row)
            {
                rows.Add(new List<string>());
            }

            while (rows[row].Count <= column)
            {
                rows[row].Add(string.Empty);
            }

            rows[row][column] = value ?? string.Empty;
        }

        public Dictionary<string, int> BuildHeaderMap()
        {
            Dictionary<string, int> result = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            if (rows.Count == 0)
            {
                return result;
            }

            for (int index = 0; index < rows[0].Count; index++)
            {
                string header = (rows[0][index] ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(header) &&
                    !result.ContainsKey(header))
                {
                    result.Add(header, index);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// UTF-8 RFC 4180 reader/writer. Quoted commas, escaped quotes and quoted
    /// multiline values round-trip without using string.Split.
    /// </summary>
    public static class LocalizationCsv
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        public static LocalizationCsvDocument ReadFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException(
                    $"CSV must be valid UTF-8: {path}",
                    exception);
            }

            return Parse(text);
        }

        public static void WriteFile(
            string path,
            LocalizationCsvDocument document)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize(document), StrictUtf8);
        }

        public static LocalizationCsvDocument Parse(string text)
        {
            LocalizationCsvDocument document =
                new LocalizationCsvDocument();
            if (string.IsNullOrEmpty(text))
            {
                return document;
            }

            int index = text[0] == '\uFEFF' ? 1 : 0;
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            bool fieldStarted = false;
            bool quotedFieldClosed = false;

            while (index < text.Length)
            {
                char character = text[index];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length &&
                            text[index + 1] == '"')
                        {
                            field.Append('"');
                            index += 2;
                            continue;
                        }

                        quoted = false;
                        quotedFieldClosed = true;
                        index++;
                        continue;
                    }

                    field.Append(character);
                    index++;
                    continue;
                }

                if (character == '"')
                {
                    if (fieldStarted || field.Length > 0)
                    {
                        throw new FormatException(
                            "A quote may only begin an empty CSV field at " +
                            $"character {index}.");
                    }

                    quoted = true;
                    fieldStarted = true;
                    quotedFieldClosed = false;
                    index++;
                    continue;
                }

                if (character == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    quotedFieldClosed = false;
                    index++;
                    continue;
                }

                if (character == '\r' || character == '\n')
                {
                    row.Add(field.ToString());
                    document.Rows.Add(row);
                    row = new List<string>();
                    field.Clear();
                    fieldStarted = false;
                    quotedFieldClosed = false;
                    if (character == '\r' &&
                        index + 1 < text.Length &&
                        text[index + 1] == '\n')
                    {
                        index++;
                    }

                    index++;
                    continue;
                }

                if (quotedFieldClosed)
                {
                    throw new FormatException(
                        "Only a comma or line break may follow a closing " +
                        $"quote at character {index}.");
                }

                field.Append(character);
                fieldStarted = true;
                index++;
            }

            if (quoted)
            {
                throw new FormatException(
                    "CSV ended inside a quoted field.");
            }

            bool endedWithLineBreak = text.Length > 0 &&
                (text[text.Length - 1] == '\r' ||
                 text[text.Length - 1] == '\n');
            if (!endedWithLineBreak || row.Count > 0 || field.Length > 0)
            {
                row.Add(field.ToString());
                document.Rows.Add(row);
            }

            return document;
        }

        public static string Serialize(LocalizationCsvDocument document)
        {
            if (document == null || document.RowCount == 0)
            {
                return string.Empty;
            }

            StringBuilder output = new StringBuilder();
            for (int rowIndex = 0;
                 rowIndex < document.Rows.Count;
                 rowIndex++)
            {
                List<string> row = document.Rows[rowIndex];
                for (int column = 0; column < row.Count; column++)
                {
                    if (column > 0)
                    {
                        output.Append(',');
                    }

                    AppendField(output, row[column] ?? string.Empty);
                }

                output.Append("\r\n");
            }

            return output.ToString();
        }

        private static void AppendField(StringBuilder output, string value)
        {
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ||
                         (!string.IsNullOrEmpty(value) &&
                          (char.IsWhiteSpace(value[0]) ||
                           char.IsWhiteSpace(value[value.Length - 1])));
            if (!quote)
            {
                output.Append(value);
                return;
            }

            output.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '"')
                {
                    output.Append("\"\"");
                }
                else
                {
                    output.Append(value[index]);
                }
            }

            output.Append('"');
        }
    }
}
