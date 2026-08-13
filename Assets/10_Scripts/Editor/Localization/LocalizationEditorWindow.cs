using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PS260714.Localization.Editor
{
    public sealed class LocalizationEditorWindow : EditorWindow
    {
        private const string ResourcesDirectory =
            "Assets/07_Runtime/Resources";
        private const string CatalogDirectory =
            ResourcesDirectory + "/Localization";
        private const string FontCatalogPath =
            CatalogDirectory + "/LocalizationFontCatalog.asset";
        private const string MarkupCatalogPath =
            CatalogDirectory + "/LocalizationMarkupCatalog.asset";
        private const string TranslationEditorControlName =
            "LocalizationTranslationEditor";

        private static readonly string[] Tabs =
        {
            "Strings",
            "Locales",
            "Font & Markup",
        };

        private static readonly NumericArgumentPreset[]
            NumericArgumentPresets =
            {
                new("Armor", "armor", "0.#"),
                new(
                    "Attack",
                    "attack",
                    "0.##",
                    "Source unit's attack-power stat before effect " +
                    "scaling."),
                new(
                    "Damage",
                    "damage",
                    "0.#",
                    "Damage amount calculated for an attack or effect."),
                new(
                    "Radius",
                    "radius",
                    "0.##",
                    "Circle or sector area radius in world units."),
                new("Duration", "duration", "0.#"),
                new("Cooldown", "cooldown", "0.#"),
                new("Interval", "interval", "0.##"),
                new("Seconds", "seconds", "0.0"),
                new("Threat", "threat", "0.##"),
                new("Before", "before", "0.#"),
                new("After", "after", "0.#"),
                new("Health", "health", string.Empty),
                new("Power", "power", string.Empty),
                new("Cost", "cost", string.Empty),
                new("Count", "count", string.Empty),
                new("Stacks", "stacks", "0.#"),
                new("Draw Count", "drawCount", "0"),
                new("Amount", "amount", string.Empty),
                new("Uses", "uses", string.Empty),
            };

        private readonly struct NumericArgumentPreset
        {
            public NumericArgumentPreset(
                string label,
                string argumentName,
                string numberFormat,
                string description = null)
            {
                Label = label;
                ArgumentName = argumentName;
                NumberFormat = numberFormat;
                Description = description ?? string.Empty;
            }

            public string Label { get; }
            public string ArgumentName { get; }
            public string NumberFormat { get; }
            public string Description { get; }
            public string Token =>
                LocalizationMarkupEditUtility.BuildNumericArgumentToken(
                    ArgumentName,
                    NumberFormat);
        }

        private LocalizationCsvDocument locales;
        private LocalizationCsvDocument strings;
        private LocalizationValidationResult validation;
        private Vector2 tableScroll;
        private Vector2 validationScroll;
        private Vector2 catalogScroll;
        private int tab;
        private int selectedStringRow = 1;
        private int previewLocaleColumn = 4;
        private string stringCategoryFilter = string.Empty;
        private string stringSearchText = string.Empty;
        private bool dirty;
        private readonly HashSet<string> persistedStringKeys =
            new(StringComparer.Ordinal);
        private LocalizationFontCatalog fontCatalog;
        private LocalizationMarkupCatalog markupCatalog;
        private UnityEditor.Editor fontCatalogEditor;
        private UnityEditor.Editor markupCatalogEditor;
        private bool showFontCatalog = true;
        private bool showMarkupCatalog = true;
        private int translationCursorIndex = -1;
        private int translationSelectIndex = -1;
        private bool restoreTranslationSelection;
        private string numericArgumentName = "value";
        private string numericArgumentFormat = "0.#";

        [MenuItem(
            PS260714EditorMenu.LocalizationEditor,
            false,
            PS260714EditorMenu.LocalizationEditorPriority)]
        public static void Open()
        {
            GetWindow<LocalizationEditorWindow>("Localization");
        }

        public static void OpenAtKey(string key)
        {
            LocalizationEditorWindow window =
                GetWindow<LocalizationEditorWindow>("Localization");
            window.SelectStringKey(key);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            minSize = new Vector2(820f, 620f);
            Reload();
            FindCatalogs();
        }

        private void OnDisable()
        {
            DestroyImmediate(fontCatalogEditor);
            DestroyImmediate(markupCatalogEditor);
        }

        private void OnGUI()
        {
            DrawToolbar();
            tab = GUILayout.Toolbar(tab, Tabs);
            EditorGUILayout.Space(4f);

            if (tab == 2)
            {
                catalogScroll = EditorGUILayout.BeginScrollView(
                    catalogScroll);
                DrawCatalogs();
                DrawValidation();
                EditorGUILayout.EndScrollView();
                return;
            }

            switch (tab)
            {
                case 0:
                    DrawCsv(strings, true);
                    DrawPreview();
                    break;
                case 1:
                    DrawCsv(locales, false);
                    break;
            }

            DrawValidation();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    dirty ? "Unsaved CSV changes" : "CSV saved",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                GUI.enabled = locales != null && strings != null;
                if (GUILayout.Button(
                        "Save & Apply",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(92f)))
                {
                    Save();
                }

                if (GUILayout.Button(
                        "Validate",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(64f)))
                {
                    Validate();
                }

                if (GUILayout.Button(
                        "Generate C#",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(84f)))
                {
                    if (Save())
                    {
                        LocalizationSourcePostprocessor
                            .CancelQueuedGeneration();
                        Generate();
                    }
                }

                GUI.enabled = true;
                if (GUILayout.Button(
                        "Reload",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(64f)))
                {
                    if (!dirty || EditorUtility.DisplayDialog(
                            "Discard changes?",
                            "Reload CSV and discard unsaved editor changes?",
                            "Reload",
                            "Cancel"))
                    {
                        Reload();
                    }
                }
            }
        }

        private void DrawCsv(
            LocalizationCsvDocument document,
            bool isStrings)
        {
            if (document == null || document.RowCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "CSV source could not be loaded.",
                    MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    isStrings
                        ? LocalizationCodeGenerator.StringsPath
                        : LocalizationCodeGenerator.LocalesPath,
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Row", GUILayout.Width(90f)))
                {
                    List<string> row = new List<string>();
                    for (int index = 0;
                         index < document.ColumnCount;
                         index++)
                    {
                        row.Add(string.Empty);
                    }

                    if (isStrings &&
                        !string.IsNullOrEmpty(stringCategoryFilter) &&
                        row.Count > 0)
                    {
                        row[0] = stringCategoryFilter + ".";
                    }
                    document.Rows.Add(row);
                    if (isStrings)
                    {
                        selectedStringRow = document.RowCount - 1;
                        stringSearchText = string.Empty;
                        ResetTranslationSelection();
                    }
                    dirty = true;
                }
            }

            if (isStrings)
            {
                DrawStringFilters(document);
                EnsureSelectedStringRowVisible(document);
            }

            tableScroll = EditorGUILayout.BeginScrollView(
                tableScroll,
                GUILayout.MinHeight(220f));
            DrawHeader(document);
            for (int row = 1; row < document.RowCount; row++)
            {
                if (isStrings && !MatchesStringFilters(document, row))
                    continue;

                GUIStyle selectionStyle =
                    GUI.skin.FindStyle("SelectionRect") ?? GUIStyle.none;
                GUIStyle background = isStrings && row == selectedStringRow
                    ? selectionStyle
                    : GUIStyle.none;
                using (new EditorGUILayout.HorizontalScope(background))
                {
                    if (isStrings && GUILayout.Button(
                        row == selectedStringRow ? ">" : " ",
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                    {
                        selectedStringRow = row;
                        ResetTranslationSelection();
                    }

                    for (int column = 0;
                         column < document.ColumnCount;
                         column++)
                    {
                        string before = document.Get(row, column);
                        float width = ResolveColumnWidth(
                            document.Get(0, column));
                        Rect inputRect = EditorGUILayout.GetControlRect(
                            false,
                            EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(width));
                        bool selectInput = isStrings &&
                                           IsPrimaryInputClick(
                                               Event.current,
                                               inputRect);

                        string after = EditorGUI.TextField(
                            inputRect,
                            before,
                            EditorStyles.textField);
                        if (selectInput)
                            SelectStringInput(row, column);
                        if (!string.Equals(
                            before,
                            after,
                            StringComparison.Ordinal))
                        {
                            document.Set(row, column, after);
                            if (isStrings &&
                                row == selectedStringRow &&
                                column >= 4)
                            {
                                previewLocaleColumn = column;
                                ResetTranslationSelection();
                            }
                            dirty = true;
                        }
                    }

                    if (GUILayout.Button(
                        "X",
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                    {
                        if (TryStageRowDeletion(document, row))
                        {
                            if (isStrings)
                            {
                                if (selectedStringRow == row)
                                {
                                    selectedStringRow = -1;
                                    ResetTranslationSelection();
                                }
                                else if (selectedStringRow > row)
                                    selectedStringRow--;
                            }
                            dirty = true;
                            row--;
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void SelectStringInput(int row, int column)
        {
            bool rowChanged = selectedStringRow != row;
            bool localeChanged = column >= 4 &&
                                 previewLocaleColumn != column;
            if (!rowChanged && !localeChanged)
                return;

            selectedStringRow = row;
            if (column >= 4)
                previewLocaleColumn = column;
            ResetTranslationSelection();
            Repaint();
        }

        internal static bool IsPrimaryInputClick(
            Event current,
            Rect inputRect)
        {
            return current != null &&
                   current.type == EventType.MouseDown &&
                   current.button == 0 &&
                   inputRect.Contains(current.mousePosition);
        }

        /// <summary>
        /// Removes a row from the editor's in-memory document. The deletion is
        /// validated and written together with every other pending edit when
        /// Save &amp; Apply is used.
        /// </summary>
        internal static bool TryStageRowDeletion(
            LocalizationCsvDocument document,
            int row)
        {
            if (document == null || row <= 0 || row >= document.RowCount)
                return false;

            document.Rows.RemoveAt(row);
            return true;
        }

        private static Dictionary<string, List<string>>
            FindLocalizationKeyReferences(IReadOnlyList<string> keys)
        {
            Dictionary<string, List<string>> references =
                new(StringComparer.Ordinal);
            Dictionary<string, string> generatedIdentifiers =
                new(StringComparer.Ordinal);
            HashSet<string> keySet = new(keys, StringComparer.Ordinal);
            foreach (string key in keys)
            {
                references[key] = new List<string>();
                generatedIdentifiers[key] =
                    "LocalizationKeys." +
                    ResolveLocalizationIdentifier(key);
            }

            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!IsLocalizationReferenceCandidate(path))
                    continue;

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception)
                {
                    continue;
                }

                string extension = Path.GetExtension(path);
                bool isCode = string.Equals(
                    extension,
                    ".cs",
                    StringComparison.OrdinalIgnoreCase);
                HashSet<string> matchedKeys = new(StringComparer.Ordinal);
                foreach (string key in keys)
                {
                    bool found = isCode
                        ? content.IndexOf(
                              generatedIdentifiers[key],
                              StringComparison.Ordinal) >= 0 ||
                          content.IndexOf(
                              $"\"{key}\"",
                              StringComparison.Ordinal) >= 0
                        : content.IndexOf(
                              $"\"{key}\"",
                              StringComparison.Ordinal) >= 0 ||
                          content.IndexOf(
                              $"'{key}'",
                              StringComparison.Ordinal) >= 0;
                    if (found)
                        matchedKeys.Add(key);
                }

                if (!isCode && matchedKeys.Count < keys.Count)
                {
                    CollectSerializedStringMatches(
                        content,
                        keySet,
                        matchedKeys);
                }

                foreach (string key in matchedKeys)
                    references[key].Add(path);
            }

            foreach (List<string> paths in references.Values)
                paths.Sort(StringComparer.OrdinalIgnoreCase);
            return references;
        }

        private static bool IsLocalizationReferenceCandidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                string.Equals(
                    path,
                    LocalizationCodeGenerator.StringsPath,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    path,
                    LocalizationCodeGenerator.KeysOutputPath,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    path,
                    LocalizationCodeGenerator.TablesOutputPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".controller", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".overrideController", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".playable", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".uxml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectSerializedStringMatches(
            string content,
            HashSet<string> keys,
            HashSet<string> matches)
        {
            using StringReader reader = new(content);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string value = line.Trim();
                int separator = value.IndexOf(':');
                if (separator >= 0)
                    value = value.Substring(separator + 1).Trim();
                else if (value.StartsWith("- ", StringComparison.Ordinal))
                    value = value.Substring(2).Trim();
                else
                    continue;

                if (value.Length >= 2 &&
                    ((value[0] == '\"' && value[^1] == '\"') ||
                     (value[0] == '\'' && value[^1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (keys.Contains(value))
                    matches.Add(value);
            }
        }

        private static string ResolveLocalizationIdentifier(string key)
        {
            string fallback = ToLocalizationIdentifier(key);
            if (!File.Exists(LocalizationCodeGenerator.KeysOutputPath))
                return fallback;

            string expectedAssignment = $"= \"{key}\";";
            try
            {
                foreach (string line in File.ReadLines(
                             LocalizationCodeGenerator.KeysOutputPath))
                {
                    string trimmed = line.Trim();
                    const string declaration = "public const string ";
                    if (!trimmed.StartsWith(
                            declaration,
                            StringComparison.Ordinal) ||
                        !trimmed.EndsWith(
                            expectedAssignment,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int assignmentIndex = trimmed.IndexOf(
                        '=',
                        declaration.Length);
                    if (assignmentIndex <= declaration.Length)
                        continue;
                    return trimmed.Substring(
                            declaration.Length,
                            assignmentIndex - declaration.Length)
                        .Trim();
                }
            }
            catch (Exception)
            {
                return fallback;
            }

            return fallback;
        }

        private static string ToLocalizationIdentifier(string key)
        {
            StringBuilder result = new();
            bool capitalize = true;
            foreach (char character in key ?? string.Empty)
            {
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
                result.Append("Key");
            if (char.IsDigit(result[0]))
                result.Insert(0, '_');
            return result.ToString();
        }

        private static string BuildLocalizationReferenceMessage(
            string key,
            IReadOnlyList<string> references)
        {
            const int visibleLimit = 10;
            StringBuilder message = new();
            message.Append("'").Append(key)
                .AppendLine("' is still referenced and cannot be deleted.")
                .AppendLine();
            int visibleCount = Mathf.Min(references.Count, visibleLimit);
            for (int index = 0; index < visibleCount; index++)
                message.Append("• ").AppendLine(references[index]);
            if (references.Count > visibleCount)
            {
                message.Append("… and ")
                    .Append(references.Count - visibleCount)
                    .Append(" more");
            }
            return message.ToString();
        }

        private static string BuildBatchLocalizationReferenceMessage(
            Dictionary<string, List<string>> references)
        {
            const int visibleKeyLimit = 8;
            const int visiblePathLimit = 4;
            StringBuilder message = new();
            message.Append(references.Count)
                .AppendLine(
                    " deleted localization key(s) are still referenced.")
                .AppendLine("Save & Apply was cancelled.")
                .AppendLine();

            int shownKeys = 0;
            foreach (KeyValuePair<string, List<string>> pair in references)
            {
                if (shownKeys >= visibleKeyLimit)
                    break;

                message.Append("- ").AppendLine(pair.Key);
                int shownPaths = Mathf.Min(
                    pair.Value.Count,
                    visiblePathLimit);
                for (int index = 0; index < shownPaths; index++)
                    message.Append("    ").AppendLine(pair.Value[index]);
                if (pair.Value.Count > shownPaths)
                {
                    message.Append("    and ")
                        .Append(pair.Value.Count - shownPaths)
                        .AppendLine(" more");
                }
                shownKeys++;
            }

            if (references.Count > shownKeys)
            {
                message.Append("and ")
                    .Append(references.Count - shownKeys)
                    .Append(" more referenced key(s)");
            }
            return message.ToString();
        }

        private void SelectStringKey(string key)
        {
            string normalized = (key ?? string.Empty).Trim();
            tab = 0;
            stringCategoryFilter = string.Empty;
            stringSearchText = normalized;
            tableScroll = Vector2.zero;

            if (strings == null || string.IsNullOrEmpty(normalized))
            {
                Repaint();
                return;
            }

            int keyColumn = ResolveKeyColumn(strings);
            for (int row = 1; row < strings.RowCount; row++)
            {
                if (!string.Equals(
                        strings.Get(row, keyColumn).Trim(),
                        normalized,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                selectedStringRow = row;
                ResetTranslationSelection();
                break;
            }

            Repaint();
        }

        private void DrawStringFilters(LocalizationCsvDocument document)
        {
            List<string> categories = CollectStringCategories(document);
            string[] categoryLabels = new string[categories.Count + 1];
            categoryLabels[0] = "All categories";
            int currentCategoryIndex = 0;
            for (int index = 0; index < categories.Count; index++)
            {
                categoryLabels[index + 1] = categories[index];
                if (string.Equals(
                        categories[index],
                        stringCategoryFilter,
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentCategoryIndex = index + 1;
                }
            }

            if (!string.IsNullOrEmpty(stringCategoryFilter) &&
                currentCategoryIndex == 0)
            {
                stringCategoryFilter = string.Empty;
            }

            string previousCategory = stringCategoryFilter;
            string previousSearch = stringSearchText;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    "Category",
                    EditorStyles.miniLabel,
                    GUILayout.Width(54f));
                int selectedCategoryIndex = EditorGUILayout.Popup(
                    currentCategoryIndex,
                    categoryLabels,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(150f));
                stringCategoryFilter = selectedCategoryIndex <= 0
                    ? string.Empty
                    : categories[selectedCategoryIndex - 1];

                GUILayout.Space(8f);
                GUILayout.Label(
                    "Search",
                    EditorStyles.miniLabel,
                    GUILayout.Width(42f));
                stringSearchText = GUILayout.TextField(
                    stringSearchText ?? string.Empty,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(180f),
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button(
                        "Clear",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(48f)))
                {
                    stringCategoryFilter = string.Empty;
                    stringSearchText = string.Empty;
                }

                int visibleCount = CountVisibleStringRows(document);
                GUILayout.Label(
                    $"{visibleCount}/{Mathf.Max(0, document.RowCount - 1)}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(64f));
            }

            if (!string.Equals(
                    previousCategory,
                    stringCategoryFilter,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    previousSearch,
                    stringSearchText,
                    StringComparison.Ordinal))
            {
                tableScroll = Vector2.zero;
            }
        }

        private static List<string> CollectStringCategories(
            LocalizationCsvDocument document)
        {
            SortedSet<string> categories = new(
                StringComparer.OrdinalIgnoreCase);
            int keyColumn = ResolveKeyColumn(document);
            for (int row = 1; row < document.RowCount; row++)
            {
                string category = GetTopLevelCategory(
                    document.Get(row, keyColumn));
                if (!string.IsNullOrEmpty(category))
                    categories.Add(category);
            }

            return new List<string>(categories);
        }

        private int CountVisibleStringRows(
            LocalizationCsvDocument document)
        {
            int count = 0;
            for (int row = 1; row < document.RowCount; row++)
            {
                if (MatchesStringFilters(document, row))
                    count++;
            }
            return count;
        }

        private void EnsureSelectedStringRowVisible(
            LocalizationCsvDocument document)
        {
            if (selectedStringRow > 0 &&
                selectedStringRow < document.RowCount &&
                MatchesStringFilters(document, selectedStringRow))
            {
                return;
            }

            int previousRow = selectedStringRow;
            selectedStringRow = -1;
            for (int row = 1; row < document.RowCount; row++)
            {
                if (!MatchesStringFilters(document, row))
                    continue;

                selectedStringRow = row;
                break;
            }

            if (selectedStringRow != previousRow)
                ResetTranslationSelection();
        }

        private bool MatchesStringFilters(
            LocalizationCsvDocument document,
            int row)
        {
            if (row <= 0 || row >= document.RowCount)
                return false;

            int keyColumn = ResolveKeyColumn(document);
            if (!string.IsNullOrEmpty(stringCategoryFilter) &&
                !string.Equals(
                    GetTopLevelCategory(document.Get(row, keyColumn)),
                    stringCategoryFilter,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string search = (stringSearchText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(search))
                return true;

            for (int column = 0; column < document.ColumnCount; column++)
            {
                string value = document.Get(row, column);
                if (!string.IsNullOrEmpty(value) &&
                    value.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveKeyColumn(
            LocalizationCsvDocument document)
        {
            Dictionary<string, int> headers = document.BuildHeaderMap();
            return headers.TryGetValue("key", out int keyColumn)
                ? keyColumn
                : 0;
        }

        private static string GetTopLevelCategory(string key)
        {
            string normalized = (key ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            int separatorIndex = normalized.IndexOf('.');
            return separatorIndex > 0
                ? normalized.Substring(0, separatorIndex)
                : normalized;
        }

        private static void DrawHeader(LocalizationCsvDocument document)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(24f);
                for (int column = 0;
                     column < document.ColumnCount;
                     column++)
                {
                    string header = document.Get(0, column);
                    GUILayout.Label(
                        header,
                        EditorStyles.boldLabel,
                        GUILayout.Width(ResolveColumnWidth(header)));
                }

                GUILayout.Space(24f);
            }
        }

        private void DrawPreview()
        {
            if (strings == null ||
                selectedStringRow <= 0 ||
                selectedStringRow >= strings.RowCount ||
                strings.ColumnCount <= 4)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Selected translation",
                EditorStyles.boldLabel);
            string[] localeLabels = new string[strings.ColumnCount - 4];
            for (int index = 0; index < localeLabels.Length; index++)
            {
                localeLabels[index] = strings.Get(0, index + 4);
            }

            previewLocaleColumn = Mathf.Clamp(
                previewLocaleColumn,
                4,
                strings.ColumnCount - 1);
            int previousLocaleColumn = previewLocaleColumn;
            int selected = EditorGUILayout.Popup(
                "Locale",
                previewLocaleColumn - 4,
                localeLabels);
            previewLocaleColumn = selected + 4;
            if (previewLocaleColumn != previousLocaleColumn)
                ResetTranslationSelection();

            EditorGUILayout.LabelField(
                "Key",
                strings.Get(
                    selectedStringRow,
                    ResolveKeyColumn(strings)));
            string source = strings.Get(
                selectedStringRow,
                previewLocaleColumn);
            EditorGUILayout.LabelField("Translation source");
            GUI.SetNextControlName(TranslationEditorControlName);
            GUIStyle textArea = new(EditorStyles.textArea)
            {
                wordWrap = true,
            };
            string edited = EditorGUILayout.TextArea(
                source,
                textArea,
                GUILayout.MinHeight(72f));
            if (!string.Equals(
                    source,
                    edited,
                    StringComparison.Ordinal))
            {
                strings.Set(
                    selectedStringRow,
                    previewLocaleColumn,
                    edited);
                source = edited;
                dirty = true;
            }

            RestoreTranslationSelection(source);
            CaptureTranslationSelection();
            DrawMarkupTools(source);

            source = strings.Get(
                selectedStringRow,
                previewLocaleColumn);
            EditorGUILayout.LabelField("Generated TMP rich text");
            EditorGUILayout.SelectableLabel(
                LocalizationMarkupParser.Render(source, markupCatalog),
                EditorStyles.textArea,
                GUILayout.Height(42f));

            GUIStyle renderedPreview = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8),
            };
            EditorGUILayout.LabelField("Rendered style preview");
            GUILayout.Label(
                LocalizationMarkupParser.Render(source, markupCatalog),
                renderedPreview,
                GUILayout.MinHeight(42f));
        }

        private void DrawMarkupTools(string source)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Insert",
                        EditorStyles.boldLabel,
                        GUILayout.Width(42f));
                    if (GUILayout.Button(
                            "Line Break",
                            GUILayout.Width(82f)))
                    {
                        ApplyMarkupEdit(
                            source,
                            LocalizationMarkupEditUtility
                                .InsertLineBreak(
                                    source,
                                    ResolveTranslationCursor(source),
                                    ResolveTranslationSelection(source)));
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                            "Manage Markup",
                            GUILayout.Width(108f)))
                    {
                        if (markupCatalog == null)
                        {
                            markupCatalog =
                                CreateCatalog<
                                    LocalizationMarkupCatalog>(
                                    "LocalizationMarkupCatalog.asset");
                        }

                        tab = 2;
                        showMarkupCatalog = true;
                        if (markupCatalog != null)
                        {
                            Selection.activeObject = markupCatalog;
                            EditorGUIUtility.PingObject(markupCatalog);
                        }
                        GUIUtility.ExitGUI();
                    }
                }

                DrawNumericArgumentTools(source);

                if (markupCatalog == null)
                {
                    EditorGUILayout.HelpBox(
                        "Create a Markup Catalog to insert registered " +
                        "styles and icons.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Style",
                        GUILayout.Width(42f));
                    foreach (LocalizationMarkupStyleDefinition style in
                             markupCatalog.Styles)
                    {
                        if (style == null ||
                            string.IsNullOrWhiteSpace(style.Id))
                        {
                            continue;
                        }

                        Color previousBackground = GUI.backgroundColor;
                        GUI.backgroundColor = style.Color;
                        string traits =
                            (style.Bold ? "Bold " : string.Empty) +
                            (style.Italic ? "Italic " : string.Empty) +
                            (style.Underline
                                ? "Underline"
                                : string.Empty);
                        GUIContent content = new(
                            style.Id,
                            $"Wrap the selected text with '{style.Id}'. " +
                            traits.Trim());
                        bool clicked = GUILayout.Button(
                            content,
                            GUILayout.MinWidth(64f),
                            GUILayout.Height(24f));
                        GUI.backgroundColor = previousBackground;
                        if (clicked)
                        {
                            ApplyMarkupEdit(
                                source,
                                LocalizationMarkupEditUtility.ApplyStyle(
                                    source,
                                    ResolveTranslationCursor(source),
                                    ResolveTranslationSelection(source),
                                    style.Id));
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Icon",
                        GUILayout.Width(42f));
                    foreach (LocalizationIconDefinition icon in
                             markupCatalog.Icons)
                    {
                        if (icon == null ||
                            string.IsNullOrWhiteSpace(icon.Id))
                        {
                            continue;
                        }

                        Texture thumbnail = icon.Sprite != null
                            ? AssetPreview.GetMiniThumbnail(icon.Sprite)
                            : null;
                        GUIContent content = new(
                            icon.Id,
                            thumbnail,
                            $"Insert icon '{icon.Id}'. Fallback: " +
                            markupCatalog.GetIconFallback(icon.Id));
                        if (GUILayout.Button(
                                content,
                                GUILayout.MinWidth(72f),
                                GUILayout.Height(28f)))
                        {
                            ApplyMarkupEdit(
                                source,
                                LocalizationMarkupEditUtility.InsertIcon(
                                    source,
                                    ResolveTranslationCursor(source),
                                    ResolveTranslationSelection(source),
                                    icon.Id));
                        }
                    }
                }
            }
        }

        private void DrawNumericArgumentTools(string source)
        {
            const int presetsPerRow = 8;
            for (int start = 0;
                 start < NumericArgumentPresets.Length;
                 start += presetsPerRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        start == 0 ? "Value" : string.Empty,
                        GUILayout.Width(42f));
                    int end = Mathf.Min(
                        start + presetsPerRow,
                        NumericArgumentPresets.Length);
                    for (int index = start; index < end; index++)
                    {
                        NumericArgumentPreset preset =
                            NumericArgumentPresets[index];
                        string buttonLabel = string.IsNullOrEmpty(
                            preset.NumberFormat)
                            ? preset.Label
                            : $"{preset.Label} {preset.NumberFormat}";
                        GUIContent content = new(
                            buttonLabel,
                            string.IsNullOrWhiteSpace(preset.Description)
                                ? $"Insert {preset.Token}."
                                : preset.Description + "\nInsert " +
                                  preset.Token + ".");
                        if (GUILayout.Button(
                                content,
                                GUILayout.MinWidth(72f),
                                GUILayout.Height(22f)))
                        {
                            ApplyMarkupEdit(
                                source,
                                LocalizationMarkupEditUtility
                                    .InsertNumericArgument(
                                        source,
                                        ResolveTranslationCursor(source),
                                        ResolveTranslationSelection(source),
                                        preset.ArgumentName,
                                        preset.NumberFormat));
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.HelpBox(
                "Attack = the source unit's attack-power stat. " +
                "Damage = the damage amount calculated for one attack " +
                "or effect. Radius = a circle/sector area's radius in " +
                "world units. These tokens are placeholders; the UI " +
                "code that resolves the localization key must supply " +
                "their values.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Custom",
                    GUILayout.Width(42f));
                EditorGUILayout.LabelField(
                    "Name",
                    EditorStyles.miniLabel,
                    GUILayout.Width(34f));
                numericArgumentName = EditorGUILayout.TextField(
                    numericArgumentName,
                    GUILayout.Width(100f));
                EditorGUILayout.LabelField(
                    "Format",
                    EditorStyles.miniLabel,
                    GUILayout.Width(42f));
                numericArgumentFormat = EditorGUILayout.TextField(
                    numericArgumentFormat,
                    GUILayout.Width(72f));

                string token = LocalizationMarkupEditUtility
                    .BuildNumericArgumentToken(
                        numericArgumentName,
                        numericArgumentFormat);
                using (new EditorGUI.DisabledScope(
                           string.IsNullOrEmpty(token)))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Insert",
                                string.IsNullOrEmpty(token)
                                    ? "Enter a safe argument name and " +
                                      "number format."
                                    : $"Insert {token}."),
                            GUILayout.Width(64f)))
                    {
                        ApplyMarkupEdit(
                            source,
                            LocalizationMarkupEditUtility
                                .InsertNumericArgument(
                                    source,
                                    ResolveTranslationCursor(source),
                                    ResolveTranslationSelection(source),
                                    numericArgumentName,
                                    numericArgumentFormat));
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void ApplyMarkupEdit(
            string source,
            LocalizationMarkupEditResult result)
        {
            if (!string.Equals(
                    source,
                    result.Text,
                    StringComparison.Ordinal))
            {
                strings.Set(
                    selectedStringRow,
                    previewLocaleColumn,
                    result.Text);
                dirty = true;
            }

            translationCursorIndex = result.CursorIndex;
            translationSelectIndex = result.SelectIndex;
            restoreTranslationSelection = true;
            Validate(false);
            Repaint();
        }

        private int ResolveTranslationCursor(string source)
        {
            return translationCursorIndex >= 0
                ? translationCursorIndex
                : (source ?? string.Empty).Length;
        }

        private int ResolveTranslationSelection(string source)
        {
            return translationSelectIndex >= 0
                ? translationSelectIndex
                : ResolveTranslationCursor(source);
        }

        private void ResetTranslationSelection()
        {
            translationCursorIndex = -1;
            translationSelectIndex = -1;
            restoreTranslationSelection = false;
        }

        private void RestoreTranslationSelection(string source)
        {
            if (!restoreTranslationSelection)
                return;

            EditorGUI.FocusTextInControl(
                TranslationEditorControlName);
            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(
                typeof(TextEditor),
                GUIUtility.keyboardControl);
            editor.text = source ?? string.Empty;
            editor.cursorIndex = Mathf.Clamp(
                translationCursorIndex,
                0,
                editor.text.Length);
            editor.selectIndex = Mathf.Clamp(
                translationSelectIndex,
                0,
                editor.text.Length);
            translationCursorIndex = editor.cursorIndex;
            translationSelectIndex = editor.selectIndex;
            restoreTranslationSelection = false;
        }

        private void CaptureTranslationSelection()
        {
            if (!string.Equals(
                    GUI.GetNameOfFocusedControl(),
                    TranslationEditorControlName,
                    StringComparison.Ordinal))
            {
                return;
            }

            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(
                typeof(TextEditor),
                GUIUtility.keyboardControl);
            translationCursorIndex = editor.cursorIndex;
            translationSelectIndex = editor.selectIndex;
        }

        private void DrawCatalogs()
        {
            EditorGUILayout.HelpBox(
                "The Font Catalog's Global Default Font is the single " +
                "game-wide font source. Markup styles and icons must be " +
                "registered explicitly in the Markup Catalog; there are no " +
                "built-in markup presets. Icon entries accept Sprite " +
                "references directly and build the required TMP assets " +
                "automatically. Catalogs remain in " +
                "Resources/Localization, and a catalog assigned directly " +
                "to a scene resolver overrides the Resources catalog.",
                MessageType.Info);

            showFontCatalog = EditorGUILayout.BeginFoldoutHeaderGroup(
                showFontCatalog,
                "Font");
            if (showFontCatalog)
                DrawFontCatalog();
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(8f);
            showMarkupCatalog = EditorGUILayout.BeginFoldoutHeaderGroup(
                showMarkupCatalog,
                "Markup");
            if (showMarkupCatalog)
                DrawMarkupCatalog();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFontCatalog()
        {
            LocalizationFontCatalog newFontCatalog =
                (LocalizationFontCatalog)EditorGUILayout.ObjectField(
                    "Catalog",
                    fontCatalog,
                    typeof(LocalizationFontCatalog),
                    false);
            if (newFontCatalog != fontCatalog)
            {
                fontCatalog = newFontCatalog;
                DestroyImmediate(fontCatalogEditor);
                fontCatalogEditor = null;
            }

            if (fontCatalog == null && GUILayout.Button("Create Font Catalog"))
            {
                fontCatalog = CreateCatalog<LocalizationFontCatalog>(
                    "LocalizationFontCatalog.asset");
            }

            if (fontCatalog == null)
                return;

            UnityEditor.Editor.CreateCachedEditor(
                fontCatalog,
                null,
                ref fontCatalogEditor);
            fontCatalogEditor.OnInspectorGUI();
        }

        private void DrawMarkupCatalog()
        {
            LocalizationMarkupCatalog newMarkupCatalog =
                (LocalizationMarkupCatalog)EditorGUILayout.ObjectField(
                    "Catalog",
                    markupCatalog,
                    typeof(LocalizationMarkupCatalog),
                    false);
            if (newMarkupCatalog != markupCatalog)
            {
                markupCatalog = newMarkupCatalog;
                DestroyImmediate(markupCatalogEditor);
                markupCatalogEditor = null;
            }

            if (markupCatalog == null &&
                GUILayout.Button("Create Markup Catalog"))
            {
                markupCatalog = CreateCatalog<LocalizationMarkupCatalog>(
                    "LocalizationMarkupCatalog.asset");
            }

            if (markupCatalog == null)
                return;

            UnityEditor.Editor.CreateCachedEditor(
                markupCatalog,
                null,
                ref markupCatalogEditor);
            markupCatalogEditor.OnInspectorGUI();
        }

        private void DrawValidation()
        {
            if (validation == null)
            {
                return;
            }

            EditorGUILayout.Space(5f);
            MessageType type = validation.ErrorCount > 0
                ? MessageType.Error
                : validation.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"Validation: {validation.ErrorCount} error(s), " +
                $"{validation.WarningCount} warning(s).",
                type);
            validationScroll = EditorGUILayout.BeginScrollView(
                validationScroll,
                GUILayout.MaxHeight(110f));
            foreach (LocalizationValidationIssue issue in validation.Issues)
            {
                EditorGUILayout.LabelField(issue.ToString(), EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Reload()
        {
            try
            {
                locales = LocalizationCsv.ReadFile(
                    LocalizationCodeGenerator.LocalesPath);
                strings = LocalizationCsv.ReadFile(
                    LocalizationCodeGenerator.StringsPath);
                CapturePersistedStringKeys();
                dirty = false;
                Validate();
            }
            catch (Exception exception)
            {
                validation = new LocalizationValidationResult();
                validation.Error("source", exception.Message);
            }
        }

        private bool Save()
        {
            if (locales == null || strings == null)
                return false;

            SynchronizeStringLocaleColumns();
            if (!ValidatePendingKeyDeletions())
                return false;

            LocalizationCsv.WriteFile(
                LocalizationCodeGenerator.LocalesPath,
                locales);
            LocalizationCsv.WriteFile(
                LocalizationCodeGenerator.StringsPath,
                strings);
            CapturePersistedStringKeys();
            dirty = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                AssetDatabase.ImportAsset(
                    LocalizationCodeGenerator.LocalesPath,
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    LocalizationCodeGenerator.StringsPath,
                    ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Validate();
            return true;
        }

        private void CapturePersistedStringKeys()
        {
            persistedStringKeys.Clear();
            if (strings == null || strings.RowCount <= 1)
                return;

            int keyColumn = ResolveKeyColumn(strings);
            for (int row = 1; row < strings.RowCount; row++)
            {
                string key = strings.Get(row, keyColumn).Trim();
                if (!string.IsNullOrEmpty(key))
                    persistedStringKeys.Add(key);
            }
        }

        private bool ValidatePendingKeyDeletions()
        {
            if (persistedStringKeys.Count == 0 || strings == null)
                return true;

            HashSet<string> currentKeys = new(StringComparer.Ordinal);
            int keyColumn = ResolveKeyColumn(strings);
            for (int row = 1; row < strings.RowCount; row++)
            {
                string key = strings.Get(row, keyColumn).Trim();
                if (!string.IsNullOrEmpty(key))
                    currentKeys.Add(key);
            }

            List<string> removedKeys = new();
            foreach (string key in persistedStringKeys)
            {
                if (!currentKeys.Contains(key))
                    removedKeys.Add(key);
            }
            if (removedKeys.Count == 0)
                return true;

            removedKeys.Sort(StringComparer.Ordinal);
            Dictionary<string, List<string>> allReferences =
                FindLocalizationKeyReferences(removedKeys);
            Dictionary<string, List<string>> blockingReferences =
                new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> pair in allReferences)
            {
                if (pair.Value.Count > 0)
                    blockingReferences.Add(pair.Key, pair.Value);
            }
            if (blockingReferences.Count == 0)
                return true;

            EditorUtility.DisplayDialog(
                "Save Localization Changes Blocked",
                BuildBatchLocalizationReferenceMessage(
                    blockingReferences),
                "OK");
            return false;
        }

        /// <summary>
        /// Adding a locale row also makes its translation column available in
        /// strings.csv. Orphaned columns are intentionally preserved so that
        /// renaming or temporarily disabling a locale never destroys text.
        /// </summary>
        private void SynchronizeStringLocaleColumns()
        {
            Dictionary<string, int> localeHeaders =
                locales.BuildHeaderMap();
            if (!localeHeaders.TryGetValue("locale", out int localeColumn))
            {
                return;
            }

            Dictionary<string, int> stringHeaders =
                strings.BuildHeaderMap();
            for (int row = 1; row < locales.RowCount; row++)
            {
                string locale = locales.Get(row, localeColumn).Trim();
                if (string.IsNullOrEmpty(locale) ||
                    stringHeaders.ContainsKey(locale))
                {
                    continue;
                }

                int newColumn = strings.ColumnCount;
                strings.Set(0, newColumn, locale);
                for (int stringRow = 1;
                     stringRow < strings.RowCount;
                     stringRow++)
                {
                    strings.Set(stringRow, newColumn, string.Empty);
                }

                stringHeaders[locale] = newColumn;
                dirty = true;
            }
        }

        private void Validate(bool validateGlyphs = true)
        {
            try
            {
                validation = LocalizationValidator.Validate(
                    LocalizationSourceModel.FromDocuments(
                        locales,
                        strings),
                    validateGlyphs);
            }
            catch (Exception exception)
            {
                validation = new LocalizationValidationResult();
                validation.Error("source", exception.Message);
            }
            Repaint();
        }

        private void Generate()
        {
            validation = LocalizationCodeGenerator.Generate();
            if (validation.IsValid)
            {
                Debug.Log(
                    "[Localization] Generated C# tables from CSV " +
                    $"({validation.WarningCount} warning(s)).");
            }
            else
            {
                Debug.LogError(
                    "[Localization] Generation stopped because CSV validation " +
                    $"has {validation.ErrorCount} error(s).");
            }

            Repaint();
        }

        private void FindCatalogs()
        {
            fontCatalog = AssetDatabase.LoadAssetAtPath<
                LocalizationFontCatalog>(FontCatalogPath);
            markupCatalog = AssetDatabase.LoadAssetAtPath<
                LocalizationMarkupCatalog>(MarkupCatalogPath);

        }

        private static T CreateCatalog<T>(string fileName)
            where T : ScriptableObject
        {
            EnsureAssetFolder(ResourcesDirectory);
            EnsureAssetFolder(CatalogDirectory);
            string path = CatalogDirectory + "/" + fileName;
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return existing;
            }

            T asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static float ResolveColumnWidth(string header)
        {
            switch ((header ?? string.Empty).ToLowerInvariant())
            {
                case "key":
                    return 190f;
                case "context":
                case "note":
                    return 130f;
                case "font_role":
                case "locale":
                case "fallback":
                case "default_font_role":
                    return 120f;
                case "display_name":
                    return 150f;
                default:
                    return 340f;
            }
        }
    }
}
