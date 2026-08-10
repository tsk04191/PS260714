using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StageSelectEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.StageSelectEditor;

    private const string AssetFolder = "Assets/Resources/Dungeons";
    private const string RenameControlName = "StageAssetRenameField";
    private const float SquareRatioTolerance = 0.01f;

    private readonly List<DungeonDefinition> _definitions = new();
    private DungeonDefinition _selected;
    private SerializedObject _serialized;
    private StageSelectPage _targetPage;
    private Vector2 _listScroll;
    private Vector2 _detailsScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _identityExpanded = true;
    private bool _stageSelectExpanded = true;
    private bool _flowExpanded = true;
    private bool _rulesExpanded = true;
    private bool _encountersExpanded;
    private bool _presentationExpanded = true;
    private bool _modifiersExpanded;
    private bool _validationExpanded = true;

    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.StageSelectEditorPriority)]
    public static void Open()
    {
        StageSelectEditorWindow window =
            GetWindow<StageSelectEditorWindow>();
        window.titleContent = new GUIContent("Dungeon Editor");
        window.minSize = new Vector2(920f, 620f);
        window.Show();
        window.Focus();
    }

    public static void Open(DungeonDefinition definition)
    {
        Open();
        StageSelectEditorWindow window =
            GetWindow<StageSelectEditorWindow>();
        window.RefreshDefinitions();
        if (definition != null)
            window.SelectDefinition(definition);
        window.Repaint();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.StageSelectEditorPriority)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Dungeon Editor");
        minSize = new Vector2(920f, 620f);
        EditorApplication.projectChanged += HandleProjectChanged;
        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        PS260714LocalizationKeyField.Refresh();
        RefreshDefinitions();
        FindStageSelectPage();

        if (Selection.activeObject is DungeonDefinition selected)
            SelectDefinition(selected);
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleProjectChanged;
        EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is not DungeonDefinition selected)
            return;

        string path = AssetDatabase.GetAssetPath(selected);
        if (!path.StartsWith(AssetFolder, StringComparison.OrdinalIgnoreCase))
            return;

        SelectDefinition(selected);
        Repaint();
    }

    private void HandleProjectChanged()
    {
        PS260714LocalizationKeyField.Refresh();
        RefreshDefinitions();
        Repaint();
    }

    private void HandleHierarchyChanged()
    {
        if (_targetPage == null)
            FindStageSelectPage();
        Repaint();
    }

    private void OnGUI()
    {
        DrawAssetToolbar();
        if (_isRenaming)
            DrawRenameRow();
        DrawScenePreviewToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawDefinitionList();
            DrawDetails();
        }
    }

    private void DrawAssetToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Stages: {_definitions.Count}",
            _selected != null,
            () => ExitAfter(CreateDefinition),
            SaveSelected,
            () => ExitAfter(DuplicateSelected),
            BeginRename,
            () => ExitAfter(DeleteSelected),
            () => PS260714AssetEditorList.Ping(_selected),
            RefreshDefinitions);
    }

    private static void ExitAfter(Action action)
    {
        action?.Invoke();
        GUIUtility.ExitGUI();
    }

    private void DrawRenameRow()
    {
        PS260714AssetRenameCommand command =
            PS260714EditorAssetUtility.DrawRenameRow(
                "SO File Name",
                RenameControlName,
                ref _renameAssetName,
                ref _focusRenameField);
        if (command == PS260714AssetRenameCommand.None)
            return;
        if (command == PS260714AssetRenameCommand.Apply)
            RenameSelected();
        else
            CancelRename();
        GUIUtility.ExitGUI();
    }

    private void DrawScenePreviewToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Scene Preview",
                EditorStyles.boldLabel,
                GUILayout.Width(92f));

            EditorGUI.BeginChangeCheck();
            StageSelectPage selectedPage = EditorGUILayout.ObjectField(
                _targetPage,
                typeof(StageSelectPage),
                true,
                GUILayout.MinWidth(180f)) as StageSelectPage;
            if (EditorGUI.EndChangeCheck())
                SetTargetPage(selectedPage);

            if (GUILayout.Button("Auto Find", GUILayout.Width(72f)))
                FindStageSelectPage();

            using (new EditorGUI.DisabledScope(_targetPage == null))
            {
                if (GUILayout.Button("Select", GUILayout.Width(58f)))
                {
                    Selection.activeObject = _targetPage;
                    EditorGUIUtility.PingObject(_targetPage);
                }
                if (GUILayout.Button("Sync Preview", GUILayout.Width(96f)))
                {
                    SyncScenePreview(false);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(
                        "Sync & Save Scene",
                        GUILayout.Width(126f)))
                {
                    SyncScenePreview(true);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (_targetPage == null)
        {
            EditorGUILayout.HelpBox(
                "Open ClientScene to synchronize the saved Stage Select UI. " +
                "DungeonDefinition assets can still be edited here.",
                MessageType.Info);
        }
    }

    private void DrawDefinitionList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width),
                   GUILayout.ExpandHeight(true)))
        {
            _searchText =
                PS260714AssetEditorList.DrawSearchField(_searchText);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanMoveSelected(-1)))
                {
                    if (GUILayout.Button("Move Up"))
                    {
                        MoveSelected(-1);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(!CanMoveSelected(1)))
                {
                    if (GUILayout.Button("Move Down"))
                    {
                        MoveSelected(1);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            int visibleCount = 0;
            foreach (DungeonDefinition definition in _definitions)
            {
                if (definition == null || !MatchesSearch(definition))
                    continue;

                visibleCount++;
                string visibility = definition.IsListedInStageSelect
                    ? $"Order {definition.StageOrder}"
                    : "Hidden";
                if (PS260714AssetEditorList.DrawAssetRow(
                        definition == _selected,
                        definition,
                        definition.StageCoverSprite,
                        definition.FallbackTitle,
                        $"{visibility} - {definition.DungeonId}",
                        AssetDatabase.GetAssetPath(definition)))
                {
                    SelectDefinition(definition);
                }
            }
            EditorGUILayout.EndScrollView();
            PS260714AssetEditorList.DrawCountFooter(
                visibleCount,
                _definitions.Count);
        }
    }

    private void DrawDetails()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.ExpandWidth(true),
                   GUILayout.ExpandHeight(true)))
        {
            if (_selected == null || _serialized == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "Select a stage or create one with New.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            _serialized.UpdateIfRequiredOrScript();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                _selected.FallbackTitle,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                AssetDatabase.GetAssetPath(_selected),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            DrawIdentitySection();
            DrawStageSelectSection();
            DrawFlowSection();
            DrawRulesSection();
            DrawEncountersSection();
            DrawPresentationSection();
            DrawModifiersSection();
            bool guiChanged = EditorGUI.EndChangeCheck();
            bool applied = _serialized.ApplyModifiedProperties();
            if (guiChanged || applied)
            {
                EditorUtility.SetDirty(_selected);
                DungeonDefinitionCatalog.Invalidate();
            }

            DrawValidationSection();
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawIdentitySection()
    {
        _identityExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _identityExpanded,
            "Identity");
        if (_identityExpanded)
        {
            DrawProperty("dungeonId", "Dungeon ID");
            DrawProperty("contentVersion", "Content Version");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawStageSelectSection()
    {
        _stageSelectExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _stageSelectExpanded,
            "Stage Select");
        if (_stageSelectExpanded)
        {
            DrawProperty("stageSelectVisibility", "Visibility");
            DrawProperty("stageOrder", "Display Order");
            PS260714LocalizationKeyField.Draw(
                _serialized.FindProperty("titleLocalizationKey"),
                "Title Localization Key");
            PS260714LocalizationKeyField.DrawLoadError();
            DrawProperty("fallbackTitle", "Fallback Title");
            DrawProperty("stageCoverSprite", "Square Stage Banner");

            EditorGUILayout.HelpBox(
                "The stage banner is displayed in a fixed 1:1 square " +
                "frame (320 x 320 by default). Square source sprites are " +
                "recommended.",
                MessageType.Info);
            DrawSquareBannerPreview();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawFlowSection()
    {
        _flowExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _flowExpanded,
            "Dungeon Flow");
        if (_flowExpanded)
        {
            DrawProperty("minimumBattleCount", "Minimum Battles");
            DrawProperty("maximumBattleCount", "Maximum Battles");
            DrawProperty(
                "insertEventBetweenBattles",
                "Rooms Between Battles");
            DrawProperty("roomPattern", "Room Pattern");
            PS260714AssetReferenceField.Draw(
                _serialized.FindProperty("flowPolicy"),
                new GUIContent("Flow Policy"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawRulesSection()
    {
        _rulesExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _rulesExpanded,
            "Run Rules");
        if (_rulesExpanded)
        {
            DrawProperty(
                "selectStartingCharacter",
                "Select Starting Character");
            DrawProperty(
                "selectStartingItems",
                "Select Starting Items");
            DrawProperty(
                "startingItemRule",
                "Starting Item Rule");
            DrawProperty(
                "useIntroBattleBalance",
                "Use Intro Battle Balance");
            DrawProperty(
                "completionDestination",
                "Completion Destination");
            DrawProperty("initialRunCurrency", "Initial Run Currency");
            DrawProperty(
                "activeSkillCostRecoveryDuration",
                "Cost Recovery Interval (Seconds)");
            DrawProperty(
                "clearedBattleHealthCost",
                "Cleared Battle Health Cost Override (-1 = Auto)");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawEncountersSection()
    {
        _encountersExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _encountersExpanded,
            "Encounters");
        if (_encountersExpanded)
        {
            DrawAssetReferenceArray("fixedBattles", "Fixed Battles");
            DrawAssetReferenceArray("fixedEvents", "Fixed Events");
            DrawProperty("defaultEvent", "Default Event");
            DrawAssetReferenceArray("fixedRests", "Fixed Rest Rooms");
            DrawProperty("defaultRest", "Default Rest Room");
            DrawAssetReferenceArray("fixedShops", "Fixed Shops");
            DrawProperty("defaultShop", "Default Shop");
            DrawAssetReferenceArray(
                "enemyPoolOverride",
                "Enemy Pool Override");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPresentationSection()
    {
        _presentationExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _presentationExpanded,
            "Presentation");
        if (_presentationExpanded)
        {
            PS260714AssetReferenceField.Draw(
                _serialized.FindProperty("fieldViewPrefab"),
                new GUIContent("Field View Prefab"));
            PS260714AssetReferenceField.Draw(
                _serialized.FindProperty("theme"),
                new GUIContent("Theme"));
            PS260714AssetReferenceField.Draw(
                _serialized.FindProperty("bgmProfile"),
                new GUIContent("Dungeon BGM Profile"));
            PS260714AssetReferenceField.Draw(
                _serialized.FindProperty("tutorial"),
                new GUIContent("Tutorial"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawModifiersSection()
    {
        _modifiersExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _modifiersExpanded,
            "Optional Rule Modules");
        if (_modifiersExpanded)
            DrawProperty("modifiers", "Modifiers", true);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawValidationSection()
    {
        _validationExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _validationExpanded,
            "Validation");
        if (_validationExpanded)
        {
            if (_selected.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(
                    "Dungeon definition is valid.",
                    MessageType.Info);

                if (_selected.SelectStartingItems &&
                    !_selected.StartingItemRule.TryValidate(
                        out string startingItemError))
                {
                    EditorGUILayout.HelpBox(
                        startingItemError,
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            if (HasDuplicateDungeonId(_selected))
            {
                EditorGUILayout.HelpBox(
                    "Dungeon ID is already used by another definition.",
                    MessageType.Error);
            }

            Sprite banner = _selected.StageCoverSprite;
            if (banner != null && !IsSquare(banner.rect))
            {
                EditorGUILayout.HelpBox(
                    $"Banner source is {banner.rect.width:0} x " +
                    $"{banner.rect.height:0}. The UI frame is square, but " +
                    "a square source avoids empty space.",
                    MessageType.Warning);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSquareBannerPreview()
    {
        Sprite sprite = _selected != null
            ? _selected.StageCoverSprite
            : null;
        if (sprite == null)
            return;

        Rect preview = GUILayoutUtility.GetRect(
            180f,
            180f,
            GUILayout.ExpandWidth(false));
        Texture previewTexture =
            AssetPreview.GetAssetPreview(sprite) ?? sprite.texture;
        EditorGUI.DrawPreviewTexture(
            preview,
            previewTexture,
            null,
            ScaleMode.ScaleToFit);
    }

    private void DrawProperty(
        string propertyName,
        string label,
        bool includeChildren = false)
    {
        SerializedProperty property =
            _serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"Missing serialized field: {propertyName}",
                MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label),
            includeChildren);
    }

    private void DrawAssetReferenceArray(
        string propertyName,
        string label)
    {
        SerializedProperty array = _serialized.FindProperty(propertyName);
        if (array == null || !array.isArray)
        {
            EditorGUILayout.HelpBox(
                $"Missing serialized array: {propertyName}",
                MessageType.Error);
            return;
        }

        array.isExpanded = EditorGUILayout.Foldout(
            array.isExpanded,
            $"{label} ({array.arraySize})",
            true);
        if (!array.isExpanded)
            return;

        int removeIndex = -1;
        EditorGUI.indentLevel++;
        for (int index = 0; index < array.arraySize; index++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                PS260714AssetReferenceField.Draw(
                    array.GetArrayElementAtIndex(index),
                    new GUIContent($"Element {index + 1}"));
                if (GUILayout.Button("−", GUILayout.Width(24f)))
                    removeIndex = index;
            }
        }
        EditorGUI.indentLevel--;

        if (removeIndex >= 0)
        {
            int previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(removeIndex);
            if (array.arraySize == previousSize)
                array.DeleteArrayElementAtIndex(removeIndex);
        }
        if (GUILayout.Button($"Add {label}"))
        {
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).objectReferenceValue = null;
        }
    }

    private bool MatchesSearch(DungeonDefinition definition)
    {
        string search = (_searchText ?? string.Empty).Trim();
        return string.IsNullOrEmpty(search) ||
               definition.name.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.DungeonId.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.FallbackTitle.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SelectDefinition(DungeonDefinition definition)
    {
        CancelRename();
        _selected = definition;
        _serialized = definition != null
            ? new SerializedObject(definition)
            : null;
        _detailsScroll = Vector2.zero;
    }

    private void RefreshDefinitions()
    {
        string selectedPath =
            PS260714EditorAssetUtility.CapturePath(_selected);
        PS260714EditorAssetUtility.LoadAssets(
            _definitions,
            "t:DungeonDefinition",
            new[] { AssetFolder },
            CompareDefinitions);
        DungeonDefinitionCatalog.Invalidate();
        SelectDefinition(PS260714EditorAssetUtility.RestoreSelection(
            selectedPath,
            _definitions));
    }

    private static int CompareDefinitions(
        DungeonDefinition left,
        DungeonDefinition right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int visibility = right.IsListedInStageSelect.CompareTo(
            left.IsListedInStageSelect);
        if (visibility != 0)
            return visibility;
        int order = left.StageOrder.CompareTo(right.StageOrder);
        return order != 0
            ? order
            : string.Compare(
                left.DungeonId,
                right.DungeonId,
                StringComparison.OrdinalIgnoreCase);
    }

    private void CreateDefinition()
    {
        EnsureAssetFolder();
        DungeonDefinition definition =
            CreateInstance<DungeonDefinition>();
        SerializedObject serialized = new(definition);
        serialized.FindProperty("dungeonId").stringValue =
            GenerateUniqueDungeonId("new_stage");
        serialized.FindProperty("stageOrder").intValue =
            GetNextStageOrder();
        serialized.FindProperty("fallbackTitle").stringValue =
            "NEW STAGE";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            AssetFolder + "/NewStage.asset");
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        RefreshDefinitions();
        SelectDefinition(definition);
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        EnsureAssetFolder();
        if (!PS260714EditorAssetUtility.TryDuplicate(
                _selected,
                AssetFolder,
                "_Copy",
                out DungeonDefinition duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Stage",
                duplicateError,
                "OK");
            return;
        }
        if (duplicate != null)
        {
            SerializedObject serialized = new(duplicate);
            serialized.FindProperty("dungeonId").stringValue =
                GenerateUniqueDungeonId(_selected.DungeonId + "_copy");
            serialized.FindProperty("stageOrder").intValue =
                GetNextStageOrder();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(duplicate);
        }

        AssetDatabase.SaveAssets();
        RefreshDefinitions();
        SelectDefinition(duplicate);
    }

    private void SaveSelected()
    {
        if (_selected == null || _serialized == null)
            return;

        _serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssetIfDirty(_selected);
        DungeonDefinitionCatalog.Invalidate();
        RefreshDefinitions();
    }

    private void BeginRename()
    {
        if (_selected == null)
            return;
        _renameAssetName = _selected.name;
        _isRenaming = true;
        _focusRenameField = true;
    }

    private void CancelRename()
    {
        _renameAssetName = string.Empty;
        _isRenaming = false;
        _focusRenameField = false;
    }

    private void RenameSelected()
    {
        if (_selected == null)
            return;

        if (!PS260714EditorAssetUtility.TryRename(
                _selected,
                _renameAssetName,
                out string error))
        {
            EditorUtility.DisplayDialog("Rename Stage", error, "OK");
            return;
        }

        CancelRename();
        RefreshDefinitions();
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                _selected,
                "Stage"))
            return;

        _selected = null;
        _serialized = null;
        RefreshDefinitions();
    }

    private bool CanMoveSelected(int direction)
    {
        if (_selected == null || !_selected.IsListedInStageSelect)
            return false;
        List<DungeonDefinition> listed = GetListedDefinitions();
        int index = listed.IndexOf(_selected);
        int destination = index + direction;
        return index >= 0 && destination >= 0 && destination < listed.Count;
    }

    private void MoveSelected(int direction)
    {
        List<DungeonDefinition> listed = GetListedDefinitions();
        int sourceIndex = listed.IndexOf(_selected);
        int destinationIndex = sourceIndex + direction;
        if (sourceIndex < 0 || destinationIndex < 0 ||
            destinationIndex >= listed.Count)
        {
            return;
        }

        (listed[sourceIndex], listed[destinationIndex]) =
            (listed[destinationIndex], listed[sourceIndex]);
        UnityEngine.Object[] targets = new UnityEngine.Object[listed.Count];
        for (int index = 0; index < listed.Count; index++)
            targets[index] = listed[index];
        Undo.RecordObjects(targets, "Reorder Stage Select Stages");

        for (int index = 0; index < listed.Count; index++)
        {
            SerializedObject serialized = new(listed[index]);
            serialized.FindProperty("stageOrder").intValue = index;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(listed[index]);
        }

        AssetDatabase.SaveAssets();
        DungeonDefinitionCatalog.Invalidate();
        RefreshDefinitions();
    }

    private List<DungeonDefinition> GetListedDefinitions()
    {
        List<DungeonDefinition> listed = _definitions.FindAll(
            definition => definition != null &&
                          definition.IsListedInStageSelect);
        listed.Sort(CompareDefinitions);
        return listed;
    }

    private int GetNextStageOrder()
    {
        int maximum = -1;
        foreach (DungeonDefinition definition in _definitions)
        {
            if (definition != null && definition.IsListedInStageSelect)
                maximum = Mathf.Max(maximum, definition.StageOrder);
        }
        return maximum + 1;
    }

    private string GenerateUniqueDungeonId(string requested)
    {
        string source = (requested ?? string.Empty).Trim().ToLowerInvariant();
        char[] characters = source.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]))
                characters[index] = '_';
        }

        string baseId = new string(characters).Trim('_');
        if (string.IsNullOrEmpty(baseId))
            baseId = "stage";

        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        foreach (DungeonDefinition definition in _definitions)
        {
            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.DungeonId))
            {
                existing.Add(definition.DungeonId);
            }
        }

        if (!existing.Contains(baseId))
            return baseId;
        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidate = $"{baseId}_{suffix}";
            if (!existing.Contains(candidate))
                return candidate;
        }
        return Guid.NewGuid().ToString("N");
    }

    private bool HasDuplicateDungeonId(DungeonDefinition selected)
    {
        if (selected == null ||
            string.IsNullOrWhiteSpace(selected.DungeonId))
        {
            return false;
        }

        foreach (DungeonDefinition definition in _definitions)
        {
            if (definition != null && definition != selected &&
                string.Equals(
                    definition.DungeonId,
                    selected.DungeonId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsSquare(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return false;
        return Mathf.Abs(rect.width / rect.height - 1f) <=
               SquareRatioTolerance;
    }

    private void FindStageSelectPage()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        StageSelectPage fallback = null;
        foreach (StageSelectPage page in
                 Resources.FindObjectsOfTypeAll<StageSelectPage>())
        {
            if (page == null ||
                EditorUtility.IsPersistent(page) ||
                !page.gameObject.scene.IsValid() ||
                !page.gameObject.scene.isLoaded)
            {
                continue;
            }

            fallback ??= page;
            if (page.gameObject.scene == activeScene)
            {
                SetTargetPage(page);
                return;
            }
        }
        SetTargetPage(fallback);
    }

    private void SetTargetPage(StageSelectPage page)
    {
        _targetPage = page != null &&
                      page.gameObject.scene.IsValid() &&
                      page.gameObject.scene.isLoaded
            ? page
            : null;
        Repaint();
    }

    private void SyncScenePreview(bool saveScene)
    {
        if (_targetPage == null)
            return;

        _serialized?.ApplyModifiedProperties();
        if (_selected != null)
            EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssets();
        DungeonDefinitionCatalog.Invalidate();

        if (!_targetPage.SyncEditorUi(out string error))
        {
            Debug.LogError(error, _targetPage);
            return;
        }

        Scene scene = _targetPage.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene && !EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError(
                "Failed to save the Stage Select scene.",
                _targetPage);
            return;
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Debug.Log(
            saveScene
                ? "Stage Select preview synchronized and scene saved."
                : "Stage Select preview synchronized.",
            _targetPage);
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Dungeons");
    }
}
