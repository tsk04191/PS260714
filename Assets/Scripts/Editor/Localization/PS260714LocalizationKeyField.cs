using System;
using System.Collections.Generic;
using PS260714.Localization.Editor;
using UnityEditor;
using UnityEngine;

internal static class PS260714LocalizationKeyField
{
    private const string EmptyLabel = "(선택 없음)";
    private const string SelectPrefixLabel = "이 키 선택";

    private static readonly List<LocalizationKeyEntry> Entries = new();
    private static readonly HashSet<string> Keys = new(
        StringComparer.Ordinal);

    private static bool _loaded;
    private static string _loadError = string.Empty;

    private readonly struct LocalizationKeyEntry
    {
        public string Key { get; }
        public string Preview { get; }

        public LocalizationKeyEntry(string key, string preview)
        {
            Key = key;
            Preview = preview;
        }
    }

    public static void Refresh()
    {
        Entries.Clear();
        Keys.Clear();
        _loadError = string.Empty;
        _loaded = true;

        try
        {
            LocalizationSourceModel source =
                LocalizationCodeGenerator.LoadSource();
            foreach (LocalizationSourceString sourceEntry in source.Strings)
            {
                string key = (sourceEntry.Key ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(key) || !Keys.Add(key))
                    continue;

                Entries.Add(new LocalizationKeyEntry(
                    key,
                    BuildPreview(sourceEntry)));
            }

            Entries.Sort((left, right) => string.Compare(
                left.Key,
                right.Key,
                StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            _loadError =
                "Localization 키를 불러오지 못했습니다. " +
                exception.Message;
        }
    }

    public static void Draw(
        SerializedProperty property,
        string label)
    {
        Draw(property, new GUIContent(label));
    }

    public static void Draw(
        SerializedProperty property,
        GUIContent label)
    {
        EnsureLoaded();
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"'{label?.text ?? "Localization Key"}' 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        Rect position = EditorGUILayout.GetControlRect();
        Draw(position, property, label);

        string currentKey = (property.stringValue ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(currentKey) && !Keys.Contains(currentKey))
        {
            EditorGUILayout.HelpBox(
                $"현재 키 '{currentKey}'는 strings.csv에 없습니다. " +
                "다른 키를 선택하거나 (선택 없음)으로 지워 주세요.",
                MessageType.Warning);
        }
    }

    public static void Draw(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EnsureLoaded();
        if (property == null)
            return;

        string currentKey = (property.stringValue ?? string.Empty).Trim();
        bool missing = !string.IsNullOrEmpty(currentKey) &&
                       !Keys.Contains(currentKey);
        string displayText = string.IsNullOrEmpty(currentKey)
            ? EmptyLabel
            : currentKey;
        string tooltip = missing
            ? "strings.csv에 없는 키입니다."
            : FindPreview(currentKey);

        EditorGUI.BeginProperty(position, label, property);
        int controlId = GUIUtility.GetControlID(
            FocusType.Keyboard,
            position);
        Rect fieldRect = EditorGUI.PrefixLabel(
            position,
            controlId,
            label ?? GUIContent.none);
        const float editButtonWidth = 40f;
        const float spacing = 3f;
        Rect editRect = new(
            fieldRect.xMax - editButtonWidth,
            fieldRect.y,
            editButtonWidth,
            fieldRect.height);
        Rect dropdownRect = new(
            fieldRect.x,
            fieldRect.y,
            Mathf.Max(0f, fieldRect.width - editButtonWidth - spacing),
            fieldRect.height);

        Color previousColor = GUI.color;
        if (missing)
            GUI.color = new Color(1f, 0.72f, 0.72f, 1f);

        if (GUI.Button(
                dropdownRect,
                new GUIContent(displayText, tooltip),
                EditorStyles.popup))
        {
            ShowMenu(dropdownRect, property, currentKey);
        }

        GUI.color = previousColor;
        using (new EditorGUI.DisabledScope(
                   string.IsNullOrEmpty(currentKey)))
        {
            if (GUI.Button(
                    editRect,
                    new GUIContent("Edit", "Localization 편집기에서 이 키를 엽니다."),
                    EditorStyles.miniButton))
            {
                LocalizationEditorWindow.OpenAtKey(currentKey);
            }
        }
        EditorGUI.EndProperty();
    }

    public static void DrawLoadError()
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(_loadError))
            EditorGUILayout.HelpBox(_loadError, MessageType.Error);
    }

    internal static IReadOnlyList<string> GetKeys()
    {
        EnsureLoaded();
        List<string> result = new(Entries.Count);
        foreach (LocalizationKeyEntry entry in Entries)
            result.Add(entry.Key);
        return result;
    }

    internal static string GetMenuPath(string key)
    {
        EnsureLoaded();
        string normalized = (key ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized))
            return EmptyLabel;

        string path = normalized.Replace('.', '/');
        if (HasChildKey(normalized))
            path += "/" + SelectPrefixLabel;
        return path;
    }

    private static void ShowMenu(
        Rect dropdownRect,
        SerializedProperty property,
        string currentKey)
    {
        GenericMenu menu = new();
        AddAssignmentItem(
            menu,
            EmptyLabel,
            string.IsNullOrEmpty(currentKey),
            property,
            string.Empty);
        menu.AddSeparator(string.Empty);

        if (!string.IsNullOrWhiteSpace(_loadError))
        {
            menu.AddDisabledItem(new GUIContent(_loadError));
        }
        else if (Entries.Count == 0)
        {
            menu.AddDisabledItem(
                new GUIContent("strings.csv에 키가 없습니다."));
        }
        else
        {
            foreach (LocalizationKeyEntry entry in Entries)
            {
                AddAssignmentItem(
                    menu,
                    GetMenuPath(entry.Key),
                    string.Equals(
                        entry.Key,
                        currentKey,
                        StringComparison.Ordinal),
                    property,
                    entry.Key);
            }
        }

        menu.DropDown(dropdownRect);
    }

    private static void AddAssignmentItem(
        GenericMenu menu,
        string menuPath,
        bool selected,
        SerializedProperty property,
        string value)
    {
        UnityEngine.Object[] targets =
            property.serializedObject.targetObjects;
        string propertyPath = property.propertyPath;
        menu.AddItem(
            new GUIContent(menuPath),
            selected,
            () => Assign(targets, propertyPath, value));
    }

    private static void Assign(
        UnityEngine.Object[] targets,
        string propertyPath,
        string value)
    {
        if (targets == null || targets.Length == 0)
            return;

        SerializedObject serialized = new(targets);
        serialized.UpdateIfRequiredOrScript();
        SerializedProperty targetProperty =
            serialized.FindProperty(propertyPath);
        if (targetProperty == null ||
            targetProperty.propertyType != SerializedPropertyType.String)
        {
            return;
        }

        targetProperty.stringValue = value;
        serialized.ApplyModifiedProperties();
        foreach (UnityEngine.Object target in targets)
        {
            if (target != null)
                EditorUtility.SetDirty(target);
        }
    }

    private static bool HasChildKey(string key)
    {
        string prefix = key + ".";
        foreach (LocalizationKeyEntry entry in Entries)
        {
            if (entry.Key.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string FindPreview(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "Localization 키를 선택합니다.";

        foreach (LocalizationKeyEntry entry in Entries)
        {
            if (string.Equals(
                    entry.Key,
                    key,
                    StringComparison.Ordinal))
            {
                return string.IsNullOrEmpty(entry.Preview)
                    ? entry.Key
                    : entry.Preview;
            }
        }
        return key;
    }

    private static string BuildPreview(LocalizationSourceString entry)
    {
        entry.Translations.TryGetValue("ko-KR", out string korean);
        entry.Translations.TryGetValue("en-US", out string english);
        korean = NormalizePreview(korean);
        english = NormalizePreview(english);

        if (!string.IsNullOrEmpty(korean) &&
            !string.IsNullOrEmpty(english))
        {
            return korean + " / " + english;
        }
        return !string.IsNullOrEmpty(korean) ? korean : english;
    }

    private static string NormalizePreview(string value)
    {
        string result = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        const int maxLength = 160;
        return result.Length <= maxLength
            ? result
            : result.Substring(0, maxLength - 1) + "…";
    }

    private static void EnsureLoaded()
    {
        if (!_loaded)
            Refresh();
    }
}
