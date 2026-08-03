using System;

public enum LocalDataLoadStatus
{
    NotLoaded = 0,
    Success = 1,
    MissingInitialized = 2,
    Migrated = 3,
    RecoveredFromBackup = 4,
    Corrupt = 5,
    UnsupportedVersion = 6,
}

internal static class LocalSaveJson
{
    public static bool HasTopLevelProperty(
        string json,
        string propertyName)
    {
        return TryGetTopLevelPropertyValueStart(
            json,
            propertyName,
            out _);
    }

    public static bool HasNonNullTopLevelProperty(
        string json,
        string propertyName)
    {
        if (!TryGetTopLevelPropertyValueStart(
                json,
                propertyName,
                out int valueStart))
        {
            return false;
        }

        const string NullLiteral = "null";
        return json.Length - valueStart < NullLiteral.Length ||
               string.CompareOrdinal(
                   json,
                   valueStart,
                   NullLiteral,
                   0,
                   NullLiteral.Length) != 0;
    }

    private static bool TryGetTopLevelPropertyValueStart(
        string json,
        string propertyName,
        out int propertyValueStart)
    {
        propertyValueStart = -1;
        if (string.IsNullOrWhiteSpace(json) ||
            string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        int depth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char current = json[index];
            if (current == '{' || current == '[')
            {
                depth++;
                continue;
            }

            if (current == '}' || current == ']')
            {
                depth--;
                continue;
            }

            if (current != '"')
                continue;

            int valueStart = index + 1;
            int valueEnd = FindStringEnd(json, valueStart);
            if (valueEnd < 0)
                return false;

            if (depth == 1 &&
                valueEnd - valueStart == propertyName.Length &&
                string.CompareOrdinal(
                    json,
                    valueStart,
                    propertyName,
                    0,
                    propertyName.Length) == 0)
            {
                int separator = valueEnd + 1;
                while (separator < json.Length &&
                       char.IsWhiteSpace(json[separator]))
                {
                    separator++;
                }

                if (separator < json.Length && json[separator] == ':')
                {
                    separator++;
                    while (separator < json.Length &&
                           char.IsWhiteSpace(json[separator]))
                    {
                        separator++;
                    }

                    propertyValueStart = separator;
                    return true;
                }
            }

            index = valueEnd;
        }

        return false;
    }

    private static int FindStringEnd(string json, int start)
    {
        bool escaped = false;
        for (int index = start; index < json.Length; index++)
        {
            char current = json[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
                return index;
        }

        return -1;
    }
}
