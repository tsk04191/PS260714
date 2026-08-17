using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

internal static class PS260714AssetEditorToolbar
{
    internal static readonly string[] ButtonOrder =
    {
        "New",
        "Save",
        "Duplicate",
        "Rename",
        "Delete",
        "Ping",
        "Refresh"
    };

    internal static void Draw(
        string summary,
        bool hasSelection,
        Action create,
        Action save,
        Action duplicate,
        Action rename,
        Action delete,
        Action ping,
        Action refresh)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(
                summary,
                EditorStyles.miniLabel,
                GUILayout.Width(136f));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(
                        ButtonOrder[0],
                        EditorStyles.toolbarButton,
                        GUILayout.Width(56f)))
                {
                    create?.Invoke();
                }

                using (new EditorGUI.DisabledScope(!hasSelection))
                {
                    if (GUILayout.Button(
                            ButtonOrder[1],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(56f)))
                    {
                        save?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[2],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(76f)))
                    {
                        duplicate?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[3],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(64f)))
                    {
                        rename?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[4],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(60f)))
                    {
                        delete?.Invoke();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button(
                        ButtonOrder[5],
                        EditorStyles.toolbarButton,
                        GUILayout.Width(52f)))
                {
                    ping?.Invoke();
                }
            }

            if (GUILayout.Button(
                    ButtonOrder[6],
                    EditorStyles.toolbarButton,
                    GUILayout.Width(64f)))
            {
                refresh?.Invoke();
            }
        }
    }
}

internal static class PS260714AssetEditorList
{
    internal const float Width = 230f;
    internal const float RowHeight = 42f;
    private const float IconSize = 34f;
    private const float ContentPadding = 5f;

    private static GUIStyle _leftLabelStyle;
    private static GUIStyle _centeredLabelStyle;

    internal static string DrawSearchField(string searchText)
    {
        EditorGUILayout.Space(4f);
        string result = EditorGUILayout.TextField(
            searchText,
            EditorStyles.toolbarSearchField);
        EditorGUILayout.Space(4f);
        return result;
    }

    internal static bool DrawRow(
        bool selected,
        GUIContent content,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Rect rowRect = GUILayoutUtility.GetRect(
            1f,
            RowHeight,
            GUILayout.ExpandWidth(true));
        bool toggled = GUI.Toggle(
            rowRect,
            selected,
            new GUIContent(string.Empty, content.tooltip),
            GUI.skin.button);

        Rect labelRect = new(
            rowRect.x + ContentPadding,
            rowRect.y,
            rowRect.width - ContentPadding * 2f,
            rowRect.height);
        if (content.image != null)
        {
            Rect iconRect = new(
                labelRect.x,
                rowRect.y + (rowRect.height - IconSize) * 0.5f,
                IconSize,
                IconSize);
            GUI.DrawTexture(
                iconRect,
                content.image,
                ScaleMode.ScaleToFit,
                true);
            labelRect.xMin = iconRect.xMax + ContentPadding;
        }

        GUIStyle labelStyle = alignment == TextAnchor.MiddleCenter
            ? CenteredLabelStyle
            : LeftLabelStyle;
        GUI.Label(
            labelRect,
            new GUIContent(content.text, content.tooltip),
            labelStyle);
        EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
        return toggled && !selected;
    }

    internal static Texture GetAssetPreview(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;

        return AssetPreview.GetAssetPreview(asset) ??
               AssetPreview.GetMiniThumbnail(asset);
    }

    internal static Texture GetAssetPreview(
        UnityEngine.Object preferredPreview,
        UnityEngine.Object fallbackAsset)
    {
        Texture preview = GetAssetPreview(preferredPreview);
        if (preview != null)
            return preview;
        if (fallbackAsset == null)
            return null;
        return AssetPreview.GetMiniTypeThumbnail(fallbackAsset.GetType());
    }

    internal static bool DrawAssetRow(
        bool selected,
        UnityEngine.Object asset,
        UnityEngine.Object preferredPreview,
        string title,
        string detail,
        string tooltip = null)
    {
        string text = string.IsNullOrWhiteSpace(detail)
            ? title ?? string.Empty
            : $"{title}\n{detail}";
        return DrawRow(
            selected,
            new GUIContent(
                text,
                GetAssetPreview(preferredPreview, asset),
                tooltip ?? AssetDatabase.GetAssetPath(asset)));
    }

    internal static void DrawCountFooter(int visibleCount, int totalCount)
    {
        EditorGUILayout.LabelField(
            $"{visibleCount} / {totalCount}",
            EditorStyles.centeredGreyMiniLabel);
    }

    internal static void Ping(UnityEngine.Object asset)
    {
        if (asset != null)
            EditorGUIUtility.PingObject(asset);
    }

    private static GUIStyle LeftLabelStyle =>
        _leftLabelStyle ??= CreateLabelStyle(TextAnchor.MiddleLeft);

    private static GUIStyle CenteredLabelStyle =>
        _centeredLabelStyle ??= CreateLabelStyle(TextAnchor.MiddleCenter);

    private static GUIStyle CreateLabelStyle(TextAnchor alignment)
    {
        return new GUIStyle(EditorStyles.label)
        {
            alignment = alignment,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
    }
}

internal enum PS260714AssetRenameCommand
{
    None,
    Apply,
    Cancel
}

internal static class PS260714EditorAssetUtility
{
    internal static PS260714AssetRenameCommand DrawRenameRow(
        string label,
        string controlName,
        ref string requestedName,
        ref bool focusRequested)
    {
        PS260714AssetRenameCommand command =
            PS260714AssetRenameCommand.None;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(88f));
            GUI.SetNextControlName(controlName);
            requestedName = EditorGUILayout.TextField(requestedName);
            if (focusRequested)
            {
                EditorGUI.FocusTextInControl(controlName);
                focusRequested = false;
            }

            if (GUILayout.Button("Apply", GUILayout.Width(56f)))
                command = PS260714AssetRenameCommand.Apply;
            if (GUILayout.Button("Cancel", GUILayout.Width(56f)))
                command = PS260714AssetRenameCommand.Cancel;

            Event current = Event.current;
            if (current.type == EventType.KeyDown &&
                GUI.GetNameOfFocusedControl() == controlName)
            {
                if (current.keyCode == KeyCode.Return ||
                    current.keyCode == KeyCode.KeypadEnter)
                {
                    command = PS260714AssetRenameCommand.Apply;
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    command = PS260714AssetRenameCommand.Cancel;
                    current.Use();
                }
            }
        }

        return command;
    }

    internal static bool TryRename(
        UnityEngine.Object asset,
        string requestedName,
        out string error)
    {
        string requested = (requestedName ?? string.Empty).Trim();
        if (requested.EndsWith(
                ".asset",
                StringComparison.OrdinalIgnoreCase))
        {
            requested = requested.Substring(
                0,
                requested.Length - 6).Trim();
        }
        if (string.IsNullOrWhiteSpace(requested) ||
            requested == "." || requested == ".." ||
            requested.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            requested.IndexOf('/') >= 0 ||
            requested.IndexOf('\\') >= 0 ||
            requested.EndsWith(".", StringComparison.Ordinal))
        {
            error = "Enter a valid asset file name.";
            return false;
        }

        if (!TryGetAssetPath(asset, out string path))
        {
            error = "The selected object is not a project asset.";
            return false;
        }

        error = AssetDatabase.RenameAsset(path, requested);
        if (!string.IsNullOrWhiteSpace(error))
            return false;

        AssetDatabase.SaveAssets();
        return true;
    }

    internal static bool TryDuplicate<T>(
        T source,
        string destinationDirectory,
        string fileNameSuffix,
        out T duplicate,
        out string error)
        where T : UnityEngine.Object
    {
        duplicate = null;
        if (!TryGetAssetPath(source, out string sourcePath))
        {
            error = "The selected object is not a project asset.";
            return false;
        }

        string directory = string.IsNullOrWhiteSpace(destinationDirectory)
            ? Path.GetDirectoryName(sourcePath)?.Replace('\\', '/')
            : destinationDirectory.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(directory))
        {
            error = "The duplicate destination folder is invalid.";
            return false;
        }

        string suffix = string.IsNullOrEmpty(fileNameSuffix)
            ? " Copy"
            : fileNameSuffix;
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{sourceName}{suffix}.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            error = "AssetDatabase.CopyAsset failed.";
            return false;
        }

        AssetDatabase.ImportAsset(destinationPath);
        duplicate = AssetDatabase.LoadAssetAtPath<T>(destinationPath);
        if (duplicate == null)
        {
            error = "The copied asset could not be loaded.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static void LoadAssets<T>(
        List<T> destination,
        string searchFilter,
        string[] searchFolders = null,
        Comparison<T> comparison = null)
        where T : UnityEngine.Object
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        string[] guids = searchFolders != null && searchFolders.Length > 0
            ? AssetDatabase.FindAssets(searchFilter, searchFolders)
            : AssetDatabase.FindAssets(searchFilter);
        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                destination.Add(asset);
        }

        destination.Sort(comparison ?? CompareAssetNames);
    }

    internal static string CapturePath(UnityEngine.Object asset)
    {
        return asset != null
            ? AssetDatabase.GetAssetPath(asset)
            : string.Empty;
    }

    internal static T RestoreSelection<T>(
        string selectedPath,
        IReadOnlyList<T> assets,
        T preferred = null,
        bool selectFirstWhenMissing = true)
        where T : UnityEngine.Object
    {
        if (preferred != null && ContainsReference(assets, preferred))
            return preferred;

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            T restored = AssetDatabase.LoadAssetAtPath<T>(selectedPath);
            if (restored != null && ContainsReference(assets, restored))
                return restored;
        }

        return selectFirstWhenMissing && assets != null && assets.Count > 0
            ? assets[0]
            : null;
    }

    internal static T GetNeighborAfterDelete<T>(
        IReadOnlyList<T> assets,
        T selected)
        where T : UnityEngine.Object
    {
        if (assets == null || selected == null || assets.Count <= 1)
            return null;
        int selectedIndex = -1;
        for (int index = 0; index < assets.Count; index++)
        {
            if (!ReferenceEquals(assets[index], selected))
                continue;
            selectedIndex = index;
            break;
        }
        if (selectedIndex < 0)
            return null;
        return assets[selectedIndex == assets.Count - 1
            ? selectedIndex - 1
            : selectedIndex + 1];
    }

    internal static void Save(UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    internal static bool TryGetAssetPath(
        UnityEngine.Object asset,
        out string path)
    {
        path = asset != null
            ? AssetDatabase.GetAssetPath(asset)
            : string.Empty;
        return !string.IsNullOrWhiteSpace(path) &&
               path.StartsWith("Assets/", StringComparison.Ordinal) &&
               AssetDatabase.LoadMainAssetAtPath(path) == asset;
    }

    private static bool ContainsReference<T>(
        IReadOnlyList<T> assets,
        T candidate)
        where T : UnityEngine.Object
    {
        if (assets == null)
            return false;
        for (int index = 0; index < assets.Count; index++)
        {
            if (ReferenceEquals(assets[index], candidate))
                return true;
        }
        return false;
    }

    private static int CompareAssetNames<T>(T left, T right)
        where T : UnityEngine.Object
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        return string.Compare(
            left.name,
            right.name,
            StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class PS260714UIToolkitAssetToolbar : Toolbar
{
    private readonly List<ToolbarButton> _selectionButtons = new();
    private readonly ToolbarButton _createButton;
    private bool _hasSelection;

    internal Label StatusLabel { get; }

    internal PS260714UIToolkitAssetToolbar(
        Action create,
        Action save,
        Action duplicate,
        Action rename,
        Action delete,
        Action ping,
        Action refresh,
        string initialStatus = "")
    {
        _createButton = AddButton(
            PS260714AssetEditorToolbar.ButtonOrder[0],
            create,
            false);
        AddButton(PS260714AssetEditorToolbar.ButtonOrder[1], save, true);
        AddButton(
            PS260714AssetEditorToolbar.ButtonOrder[2],
            duplicate,
            true);
        AddButton(PS260714AssetEditorToolbar.ButtonOrder[3], rename, true);
        AddButton(PS260714AssetEditorToolbar.ButtonOrder[4], delete, true);
        AddButton(PS260714AssetEditorToolbar.ButtonOrder[5], ping, true);
        AddButton(
            PS260714AssetEditorToolbar.ButtonOrder[6],
            refresh,
            false);
        Add(new ToolbarSpacer { flex = true });

        StatusLabel = new Label();
        PS260714EditorText.SetText(StatusLabel, initialStatus);
        StatusLabel.style.marginLeft = 8f;
        StatusLabel.style.minWidth = 180f;
        Add(StatusLabel);
        RegisterCallback<AttachToPanelEvent>(_ =>
            EditorApplication.playModeStateChanged +=
                HandlePlayModeStateChanged);
        RegisterCallback<DetachFromPanelEvent>(_ =>
            EditorApplication.playModeStateChanged -=
                HandlePlayModeStateChanged);
        SetHasSelection(false);
    }

    internal void SetHasSelection(bool hasSelection)
    {
        _hasSelection = hasSelection;
        bool editable =
            !EditorApplication.isPlayingOrWillChangePlaymode;
        _createButton.SetEnabled(editable);
        for (int index = 0; index < _selectionButtons.Count; index++)
        {
            ToolbarButton button = _selectionButtons[index];
            bool ping = string.Equals(
                button.userData as string,
                PS260714AssetEditorToolbar.ButtonOrder[5],
                StringComparison.Ordinal);
            button.SetEnabled(hasSelection && (editable || ping));
        }
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange _)
    {
        SetHasSelection(_hasSelection);
    }

    private ToolbarButton AddButton(
        string text,
        Action action,
        bool requiresSelection)
    {
        ToolbarButton button = new(action)
        {
            text = PS260714EditorText.Tr(text),
            tooltip = PS260714EditorText.BuildTooltip(
                PS260714EditorText.Tr(text)),
            userData = text,
        };
        Add(button);
        if (requiresSelection)
            _selectionButtons.Add(button);
        return button;
    }
}

internal sealed class PS260714UIToolkitAssetList<T> : VisualElement
    where T : UnityEngine.Object
{
    private readonly List<T> _allItems = new();
    private readonly List<T> _visibleItems = new();
    private readonly Func<T, string> _title;
    private readonly Func<T, string> _detail;
    private readonly Func<T, string> _searchText;
    private readonly Action<T> _selectionChanged;
    private readonly ToolbarSearchField _searchField;
    private readonly ListView _list;
    private readonly Label _countLabel;
    private bool _suppressSelection;

    internal VisualElement HeaderExtras { get; }

    internal PS260714UIToolkitAssetList(
        string header,
        float width,
        Func<T, string> title,
        Func<T, string> detail,
        Func<T, string> searchText,
        Action<T> selectionChanged)
    {
        _title = title ?? (asset => asset != null ? asset.name : string.Empty);
        _detail = detail;
        _searchText = searchText ?? _title;
        _selectionChanged = selectionChanged;

        style.width = width;
        style.flexShrink = 0f;
        style.borderRightWidth = 1f;
        style.borderRightColor = new Color(0.2f, 0.22f, 0.24f);

        Label headerLabel = new();
        PS260714EditorText.SetText(headerLabel, header);
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.paddingLeft = 8f;
        headerLabel.style.paddingTop = 8f;
        headerLabel.style.paddingBottom = 6f;
        Add(headerLabel);

        _searchField = new ToolbarSearchField();
        _searchField.tooltip = PS260714EditorText.BuildTooltip(
            PS260714EditorText.Tr("Search"));
        _searchField.style.marginLeft = 4f;
        _searchField.style.marginRight = 4f;
        _searchField.style.marginBottom = 4f;
        _searchField.RegisterValueChangedCallback(_ => ApplyFilter());
        Add(_searchField);

        HeaderExtras = new VisualElement();
        Add(HeaderExtras);

        _list = new ListView
        {
            itemsSource = _visibleItems,
            fixedItemHeight = PS260714AssetEditorList.RowHeight,
            selectionType = SelectionType.Single,
            makeItem = MakeRow,
            bindItem = BindRow
        };
        _list.style.flexGrow = 1f;
        _list.selectionChanged += HandleSelectionChanged;
        Add(_list);

        _countLabel = new Label("0 / 0");
        _countLabel.tooltip = PS260714EditorText.Tr(
            "Shows the visible asset count and total asset count.");
        _countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _countLabel.style.color = new Color(0.55f, 0.58f, 0.6f);
        _countLabel.style.paddingTop = 3f;
        _countLabel.style.paddingBottom = 3f;
        Add(_countLabel);
    }

    internal void SetItems(IReadOnlyList<T> items, T preferred)
    {
        _allItems.Clear();
        if (items != null)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (items[index] != null)
                    _allItems.Add(items[index]);
            }
        }
        ApplyFilter(preferred);
    }

    internal void SelectWithoutNotify(T selected)
    {
        int index = selected != null
            ? _visibleItems.IndexOf(selected)
            : -1;
        _suppressSelection = true;
        _list.SetSelectionWithoutNotify(
            index >= 0 ? new[] { index } : Array.Empty<int>());
        _suppressSelection = false;
    }

    private void ApplyFilter(T preferred = null)
    {
        T previous = preferred;
        if (previous == null && _list.selectedIndex >= 0 &&
            _list.selectedIndex < _visibleItems.Count)
        {
            previous = _visibleItems[_list.selectedIndex];
        }

        string query = (_searchField.value ?? string.Empty).Trim();
        _visibleItems.Clear();
        for (int index = 0; index < _allItems.Count; index++)
        {
            T item = _allItems[index];
            string searchable = _searchText(item) ?? string.Empty;
            if (query.Length == 0 || searchable.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _visibleItems.Add(item);
            }
        }

        _list.Rebuild();
        SelectWithoutNotify(previous);
        _countLabel.text = $"{_visibleItems.Count} / {_allItems.Count}";
    }

    private VisualElement MakeRow()
    {
        VisualElement row = new();
        row.style.flexDirection = FlexDirection.Column;
        row.style.justifyContent = Justify.Center;
        row.style.paddingLeft = 7f;
        row.Add(new Label { name = "title" });
        Label detail = new() { name = "detail" };
        detail.style.fontSize = 10f;
        detail.style.color = new Color(0.58f, 0.62f, 0.64f);
        row.Add(detail);
        return row;
    }

    private void BindRow(VisualElement element, int index)
    {
        T item = index >= 0 && index < _visibleItems.Count
            ? _visibleItems[index]
            : null;
        element.Q<Label>("title").text = item != null
            ? _title(item)
            : PS260714EditorText.Tr("Missing Asset");
        Label detail = element.Q<Label>("detail");
        string detailText = item != null ? _detail?.Invoke(item) : null;
        detail.text = detailText ?? string.Empty;
        detail.style.display = string.IsNullOrWhiteSpace(detailText)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        element.tooltip = item != null
            ? AssetDatabase.GetAssetPath(item)
            : string.Empty;
    }

    private void HandleSelectionChanged(IEnumerable<object> selected)
    {
        if (_suppressSelection)
            return;
        foreach (object value in selected)
        {
            _selectionChanged?.Invoke(value as T);
            return;
        }
        _selectionChanged?.Invoke(null);
    }
}

internal sealed class PS260714UIToolkitRenameRow : VisualElement
{
    internal TextField Field { get; }

    internal PS260714UIToolkitRenameRow(
        Action apply,
        Action cancel)
    {
        style.flexDirection = FlexDirection.Row;
        style.paddingLeft = 4f;
        style.paddingRight = 4f;
        style.paddingBottom = 5f;
        style.display = DisplayStyle.None;

        Field = new TextField();
        Field.tooltip = PS260714EditorText.BuildTooltip(
            PS260714EditorText.Tr("Asset Name"));
        Field.style.flexGrow = 1f;
        Field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return ||
                evt.keyCode == KeyCode.KeypadEnter)
            {
                apply?.Invoke();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                cancel?.Invoke();
                evt.StopPropagation();
            }
        });
        Add(Field);
        Button applyButton = new(apply);
        PS260714EditorText.SetText(applyButton, "Apply");
        Add(applyButton);
        Button cancelButton = new(cancel);
        PS260714EditorText.SetText(cancelButton, "Cancel");
        Add(cancelButton);
    }

    internal void Show(UnityEngine.Object asset)
    {
        Field.value = Path.GetFileNameWithoutExtension(
            AssetDatabase.GetAssetPath(asset));
        style.display = DisplayStyle.Flex;
        Field.schedule.Execute(Field.Focus);
    }

    internal void Hide()
    {
        style.display = DisplayStyle.None;
        Field.SetValueWithoutNotify(string.Empty);
    }
}

internal static class PS260714SafeAssetDelete
{
    private const int VisibleReferenceLimit = 10;

    internal static bool TryMoveToTrash(
        UnityEngine.Object asset,
        string assetLabel,
        bool checkReferences = true)
    {
        if (!PS260714EditorAssetUtility.TryGetAssetPath(
                asset,
                out string path))
        {
            EditorUtility.DisplayDialog(
                $"Delete {assetLabel}",
                "The selected object is not a deletable project asset.",
                "OK");
            return false;
        }

        if (checkReferences)
        {
            IReadOnlyList<string> references = FindReferences(asset);
            if (references.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    $"Delete {assetLabel} Blocked",
                    BuildReferenceMessage(references),
                    "OK");
                return false;
            }
        }

        if (!EditorUtility.DisplayDialog(
                $"Delete {assetLabel}",
                $"Move '{asset.name}' to the system trash?\n\n{path}\n\n" +
                "The asset can be restored from the system trash.",
                "Move to Trash",
                "Cancel"))
        {
            return false;
        }

        if (!AssetDatabase.MoveAssetToTrash(path))
        {
            EditorUtility.DisplayDialog(
                $"Delete {assetLabel}",
                "Failed to move the asset to the system trash.",
                "OK");
            return false;
        }

        AssetDatabase.SaveAssets();
        return true;
    }

    internal static IReadOnlyList<string> FindReferences(
        UnityEngine.Object asset)
    {
        if (!PS260714EditorAssetUtility.TryGetAssetPath(
                asset,
                out string targetPath))
        {
            return Array.Empty<string>();
        }

        List<string> references = new();
        string[] allPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string candidatePath in allPaths)
        {
            if (!IsReferenceCandidate(candidatePath, targetPath))
                continue;
            if (asset is ItemDefinitionSO && string.Equals(
                    candidatePath,
                    "Assets/06_Runtime/Resources/ItemCatalog.asset",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] dependencies;
            try
            {
                dependencies = AssetDatabase.GetDependencies(
                    candidatePath,
                    false);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (string dependency in dependencies)
            {
                if (!string.Equals(
                        dependency,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                references.Add(candidatePath);
                break;
            }
        }

        references.Sort(StringComparer.OrdinalIgnoreCase);
        return references;
    }

    private static bool IsReferenceCandidate(
        string candidatePath,
        string targetPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.Equals(
                candidatePath,
                targetPath,
                StringComparison.OrdinalIgnoreCase) ||
            !candidatePath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        string extension = Path.GetExtension(candidatePath);
        return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".controller", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".overrideController", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".playable", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReferenceMessage(
        IReadOnlyList<string> references)
    {
        int visibleCount = Math.Min(references.Count, VisibleReferenceLimit);
        string message =
            "The asset is still referenced and cannot be deleted.\n\n";
        for (int index = 0; index < visibleCount; index++)
            message += $"- {references[index]}\n";
        if (references.Count > visibleCount)
        {
            message +=
                $"- {references.Count - visibleCount} more reference(s)";
        }
        return message;
    }
}
