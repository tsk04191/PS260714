using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PS260714.Localization.Editor
{
    public sealed class LocalizationEditorWindow : EditorWindow
    {
        private const string ResourcesDirectory =
            "Assets/Resources";
        private const string CatalogDirectory =
            ResourcesDirectory + "/Localization";
        private const string FontCatalogPath =
            CatalogDirectory + "/LocalizationFontCatalog.asset";
        private const string MarkupCatalogPath =
            CatalogDirectory + "/LocalizationMarkupCatalog.asset";

        private static readonly string[] Tabs =
        {
            "Strings",
            "Locales",
            "Font & Markup",
        };

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
        private LocalizationFontCatalog fontCatalog;
        private LocalizationMarkupCatalog markupCatalog;
        private UnityEditor.Editor fontCatalogEditor;
        private UnityEditor.Editor markupCatalogEditor;
        private bool showFontCatalog = true;
        private bool showMarkupCatalog = true;

        [MenuItem(PS260714EditorMenu.LocalizationEditor)]
        public static void Open()
        {
            GetWindow<LocalizationEditorWindow>("Localization");
        }

        private void OnEnable()
        {
            minSize = new Vector2(820f, 520f);
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
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
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

                GUI.enabled = locales != null && strings != null;
                if (GUILayout.Button("Save CSV", EditorStyles.toolbarButton))
                {
                    Save();
                }

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                {
                    Validate();
                }

                if (GUILayout.Button(
                    "Generate C#",
                    EditorStyles.toolbarButton))
                {
                    Save();
                    Generate();
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    dirty ? "Unsaved CSV changes" : "CSV saved",
                    EditorStyles.miniLabel);
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
                    }

                    for (int column = 0;
                         column < document.ColumnCount;
                         column++)
                    {
                        string before = document.Get(row, column);
                        float width = ResolveColumnWidth(
                            document.Get(0, column));
                        string after = EditorGUILayout.TextField(
                            before,
                            GUILayout.Width(width));
                        if (!string.Equals(
                            before,
                            after,
                            StringComparison.Ordinal))
                        {
                            document.Set(row, column, after);
                            dirty = true;
                        }
                    }

                    if (GUILayout.Button(
                        "X",
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                    {
                        document.Rows.RemoveAt(row);
                        if (isStrings)
                        {
                            if (selectedStringRow == row)
                                selectedStringRow = -1;
                            else if (selectedStringRow > row)
                                selectedStringRow--;
                        }
                        dirty = true;
                        row--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
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

            selectedStringRow = -1;
            for (int row = 1; row < document.RowCount; row++)
            {
                if (!MatchesStringFilters(document, row))
                    continue;

                selectedStringRow = row;
                return;
            }
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
            EditorGUILayout.LabelField("Markup preview", EditorStyles.boldLabel);
            string[] localeLabels = new string[strings.ColumnCount - 4];
            for (int index = 0; index < localeLabels.Length; index++)
            {
                localeLabels[index] = strings.Get(0, index + 4);
            }

            previewLocaleColumn = Mathf.Clamp(
                previewLocaleColumn,
                4,
                strings.ColumnCount - 1);
            int selected = EditorGUILayout.Popup(
                "Locale",
                previewLocaleColumn - 4,
                localeLabels);
            previewLocaleColumn = selected + 4;
            string source = strings.Get(
                selectedStringRow,
                previewLocaleColumn);
            EditorGUILayout.LabelField("Source");
            EditorGUILayout.SelectableLabel(
                source,
                EditorStyles.textArea,
                GUILayout.Height(42f));
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
                dirty = false;
                Validate();
            }
            catch (Exception exception)
            {
                validation = new LocalizationValidationResult();
                validation.Error("source", exception.Message);
            }
        }

        private void Save()
        {
            if (locales == null || strings == null)
            {
                return;
            }

            SynchronizeStringLocaleColumns();
            LocalizationCsv.WriteFile(
                LocalizationCodeGenerator.LocalesPath,
                locales);
            LocalizationCsv.WriteFile(
                LocalizationCodeGenerator.StringsPath,
                strings);
            dirty = false;
            AssetDatabase.ImportAsset(
                LocalizationCodeGenerator.LocalesPath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                LocalizationCodeGenerator.StringsPath,
                ImportAssetOptions.ForceUpdate);
            Validate();
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

        private void Validate()
        {
            validation = LocalizationCodeGenerator.Validate();
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
