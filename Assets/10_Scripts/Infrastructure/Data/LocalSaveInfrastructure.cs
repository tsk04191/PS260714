using System;
using UnityEngine;

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

internal delegate bool LocalSaveDeserializer<T>(
    string json,
    out T saveData);

internal static class LocalSaveRecovery
{
    public static bool TryRecover<T>(
        string backupKey,
        string corruptKey,
        string corruptPrimaryJson,
        LocalSaveDeserializer<T> tryDeserialize,
        out T backup)
    {
        backup = default;
        if (string.IsNullOrWhiteSpace(backupKey) ||
            string.IsNullOrWhiteSpace(corruptKey) ||
            tryDeserialize == null ||
            !PlayerPrefs.HasKey(backupKey))
        {
            return false;
        }

        string backupJson = PlayerPrefs.GetString(backupKey, string.Empty);
        if (!tryDeserialize(backupJson, out backup))
            return false;

        PlayerPrefs.SetString(corruptKey, corruptPrimaryJson ?? string.Empty);
        PlayerPrefs.Save();
        return true;
    }

    public static void BackupCurrentValidSave(
        string primaryKey,
        string backupKey,
        string corruptKey,
        string replacementJson,
        Func<string, bool> isValidSave)
    {
        if (string.IsNullOrWhiteSpace(primaryKey) ||
            string.IsNullOrWhiteSpace(backupKey) ||
            string.IsNullOrWhiteSpace(corruptKey) ||
            isValidSave == null)
        {
            throw new ArgumentException(
                "Save keys and the validation callback are required.");
        }

        if (PlayerPrefs.HasKey(primaryKey))
        {
            string currentJson = PlayerPrefs.GetString(
                primaryKey,
                string.Empty);
            if (isValidSave(currentJson))
            {
                PlayerPrefs.SetString(backupKey, currentJson);
                return;
            }

            PlayerPrefs.SetString(corruptKey, currentJson);
        }

        if (!PlayerPrefs.HasKey(backupKey))
        {
            PlayerPrefs.SetString(
                backupKey,
                replacementJson ?? string.Empty);
        }
    }
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
