using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class PS260714EditorMenu
{
    public const string Root = "PS260714/";
    public const string DungeonRoot = Root + "Dungeon/";
    private const string EffectsRoot = Root + "Effects/";
    private const string LocalizationRoot = Root + "Localization/";
    private const string UiRoot = Root + "UI/";
    private const string DataRoot = Root + "Data/";

    public const int CommonSettingsPriority = 100;
    public const int CharacterEditorPriority = 101;
    public const int ItemEditorPriority = 102;
    public const int EnemyEditorPriority = 103;
    public const int StatusEffectEditorPriority = 104;
    public const int DungeonEditorPriority = 0;
    public const int BattleEditorPriority = 1;
    public const int EventEditorPriority = 2;
    public const int RestEditorPriority = 3;
    public const int ShopEditorPriority = 4;
    public const int BattleVfxEditorPriority = 106;
    public const int ValidateBattleVfxPriority = 107;
    public const int LocalizationEditorPriority = 108;
    public const int ValidateLocalizationPriority = 109;
    public const int GenerateLocalizationPriority = 110;
    public const int StageSelectEditorPriority = DungeonEditorPriority;
    public const int RecruitEditorPriority = 112;
    public const int ValidateDesignerUiPriority = 113;
    public const int InstallDynamicUiPreviewsPriority = 114;
    public const int MigrateBattleItemUsagePriority = 115;
    public const int MigrateCharacterModifierIdsPriority = 116;

    public const string CharacterEditor =
        Root + "Character Editor";
    public const string CommonSettings =
        Root + "Common Settings";
    public const string RecruitEditor =
        UiRoot + "Recruit Editor";
    public const string DungeonEditor =
        DungeonRoot + "Dungeon Editor";
    public const string StageSelectEditor = DungeonEditor;
    public const string ItemEditor =
        Root + "Item Editor";
    public const string EnemyEditor =
        Root + "Enemy Editor";
    public const string StatusEffectEditor =
        Root + "Status Effect Editor";
    public const string BattleEditor =
        DungeonRoot + "Battle Editor";
    public const string EventEditor =
        DungeonRoot + "Event Editor";
    public const string RestEditor =
        DungeonRoot + "Rest Editor";
    public const string ShopEditor =
        DungeonRoot + "Shop Editor";
    public const string BattleVfxEditor =
        EffectsRoot + "Battle VFX Editor";
    public const string ValidateBattleVfx =
        EffectsRoot + "Validate Battle VFX";
    public const string LocalizationEditor =
        LocalizationRoot + "Localization Editor";
    public const string ValidateLocalization =
        LocalizationRoot + "Validate CSV";
    public const string GenerateLocalization =
        LocalizationRoot + "Generate C#";
    public const string ValidateDesignerUi =
        UiRoot + "Validate Designer UI";
    public const string InstallDynamicUiPreviews =
        UiRoot + "Install Dynamic UI Scene Previews";
    public const string MigrateBattleItemUsage =
        DataRoot + "Migrate Battle Item Usage Schema";
    public const string MigrateCharacterModifierIds =
        DataRoot + "Migrate Character Modifier IDs";
}

internal static class PS260714AssetEditorRegistry
{
    internal static bool CanOpen(UnityEngine.Object asset)
    {
        return asset is CharacterSO or
            EnemySO or
            ItemDefinitionSO or
            StatusEffectSO or
            BattleVfxCueSO or
            DungeonDefinition or
            DungeonRoomSO or
            BattleSO or
            CharacterRoleSO or
            CharacterArchetypeSO;
    }

    internal static bool Open(UnityEngine.Object asset)
    {
        switch (asset)
        {
            case CharacterSO character:
                CharacterEditorWindow.Open(character);
                return true;
            case EnemySO enemy:
                EnemyEditorWindow.Open(enemy);
                return true;
            case ItemDefinitionSO item:
                ItemEditorWindow.Open(item);
                return true;
            case StatusEffectSO status:
                StatusEffectEditorWindow.Open(status);
                return true;
            case BattleVfxCueSO cue:
                BattleVfxEditorWindow.Open(cue);
                return true;
            case DungeonDefinition dungeon:
                StageSelectEditorWindow.Open(dungeon);
                return true;
            case DungeonRoomSO room:
                DungeonRoomEditorMenu.Open(room);
                return true;
            case BattleSO battle:
                BattleEditorWindow.Open(battle);
                return true;
            case CharacterRoleSO:
            case CharacterArchetypeSO:
                CommonSettingsProjectProvider.Open(asset);
                return true;
            default:
                return false;
        }
    }
}

internal static class PS260714AssetReferenceField
{
    internal static void Draw(
        SerializedProperty property,
        GUIContent label,
        bool allowSceneObjects = false)
    {
        if (property == null ||
            property.propertyType != SerializedPropertyType.ObjectReference)
        {
            EditorGUILayout.HelpBox(
                $"'{label?.text ?? "Asset"}' reference field was not found.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(property, label);
        UnityEngine.Object value = property.objectReferenceValue;
        using (new EditorGUI.DisabledScope(value == null))
        {
            if (GUILayout.Button("Ping", GUILayout.Width(42f)))
            {
                Selection.activeObject = value;
                EditorGUIUtility.PingObject(value);
            }

            using (new EditorGUI.DisabledScope(
                       !PS260714AssetEditorRegistry.CanOpen(value)))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(42f)))
                    PS260714AssetEditorRegistry.Open(value);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(46f)))
                property.objectReferenceValue = null;
        }
        EditorGUILayout.EndHorizontal();
    }
}

internal static class DungeonRoomEditorMenu
{
    [MenuItem(
        PS260714EditorMenu.EventEditor,
        false,
        PS260714EditorMenu.EventEditorPriority)]
    public static void OpenEventEditor()
    {
        DungeonEventEditorWindow.Open(
            Selection.activeObject as DungeonEventSO);
    }

    [MenuItem(
        PS260714EditorMenu.EventEditor,
        true,
        PS260714EditorMenu.EventEditorPriority)]
    private static bool ValidateOpenEventEditor()
    {
        return CanOpen();
    }

    [MenuItem(
        PS260714EditorMenu.RestEditor,
        false,
        PS260714EditorMenu.RestEditorPriority)]
    public static void OpenRestEditor()
    {
        DungeonRestEditorWindow.Open(
            Selection.activeObject as DungeonRestSO);
    }

    [MenuItem(
        PS260714EditorMenu.RestEditor,
        true,
        PS260714EditorMenu.RestEditorPriority)]
    private static bool ValidateOpenRestEditor()
    {
        return CanOpen();
    }

    [MenuItem(
        PS260714EditorMenu.ShopEditor,
        false,
        PS260714EditorMenu.ShopEditorPriority)]
    public static void OpenShopEditor()
    {
        OpenFirst<DungeonShopSO>("Shop");
    }

    [MenuItem(
        PS260714EditorMenu.ShopEditor,
        true,
        PS260714EditorMenu.ShopEditorPriority)]
    private static bool ValidateOpenShopEditor()
    {
        return CanOpen();
    }

    internal static void Open(DungeonRoomSO room)
    {
        if (room == null)
            return;

        if (room is DungeonEventSO dungeonEvent)
        {
            DungeonEventEditorWindow.Open(dungeonEvent);
            return;
        }

        Selection.activeObject = room;
        EditorUtility.FocusProjectWindow();
        EditorGUIUtility.PingObject(room);
        EditorApplication.delayCall += () =>
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }

    private static void OpenFirst<T>(string roomLabel)
        where T : DungeonRoomSO
    {
        if (Selection.activeObject is T selected)
        {
            Open(selected);
            return;
        }

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        Array.Sort(guids, StringComparer.Ordinal);
        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            T room = AssetDatabase.LoadAssetAtPath<T>(path);
            if (room == null)
                continue;

            Open(room);
            return;
        }

        Debug.LogWarning(
            $"No Dungeon {roomLabel} asset was found. Create one from " +
            $"Assets/Create/Dungeon/Room/{roomLabel}.");
    }

    private static bool CanOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

internal readonly struct PS260714StatusEffectSelectionOptions
{
    internal bool AllowNone { get; }
    internal CharacterTargetFaction? TargetFaction { get; }
    internal bool RequireRemovable { get; }

    internal PS260714StatusEffectSelectionOptions(
        bool allowNone = false,
        CharacterTargetFaction? targetFaction = null,
        bool requireRemovable = false)
    {
        AllowNone = allowNone;
        TargetFaction = targetFaction;
        RequireRemovable = requireRemovable;
    }
}

internal static class PS260714StatusEffectSelection
{
    internal static void Draw(
        SerializedProperty statusEffects,
        SerializedProperty legacyStatusEffect,
        GUIContent label,
        PS260714StatusEffectSelectionOptions options = default)
    {
        if (statusEffects == null)
        {
            if (legacyStatusEffect != null)
                EditorGUILayout.PropertyField(legacyStatusEffect, label);
            return;
        }

        int selectedCount = GetSelectedCount(
            statusEffects,
            legacyStatusEffect);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        if (GUILayout.Button(
                selectedCount == 0
                    ? "상태 선택"
                    : $"{selectedCount}개 선택",
                EditorStyles.popup))
        {
            Rect buttonRect = GUILayoutUtility.GetLastRect();
            PopupWindow.Show(
                buttonRect,
                new StatusEffectMultiSelectPopup(
                    statusEffects.serializedObject.targetObject,
                    statusEffects.propertyPath,
                    legacyStatusEffect?.propertyPath,
                    options));
        }
        EditorGUILayout.EndHorizontal();

        DrawSelectedRows(statusEffects, legacyStatusEffect, options);
    }

    internal static void DrawSingle(
        SerializedProperty statusEffect,
        GUIContent label,
        PS260714StatusEffectSelectionOptions options = default)
    {
        if (statusEffect == null)
        {
            EditorGUILayout.HelpBox(
                "Status effect property could not be found.",
                MessageType.Error);
            return;
        }

        StatusEffectSO selected =
            statusEffect.objectReferenceValue as StatusEffectSO;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        GUIContent buttonContent = selected != null
            ? new GUIContent(
                selected.name,
                PS260714AssetEditorList.GetAssetPreview(selected.Icon),
                selected.StatusId)
            : new GUIContent(options.AllowNone ? "없음" : "상태 선택");
        if (GUILayout.Button(buttonContent, EditorStyles.popup))
        {
            Rect buttonRect = GUILayoutUtility.GetLastRect();
            PopupWindow.Show(
                buttonRect,
                new StatusEffectSingleSelectPopup(
                    statusEffect.serializedObject.targetObject,
                    statusEffect.propertyPath,
                    options));
        }
        using (new EditorGUI.DisabledScope(selected == null))
        {
            if (GUILayout.Button("Edit", GUILayout.Width(42f)))
                StatusEffectEditorWindow.Open(selected);
        }
        EditorGUILayout.EndHorizontal();

        if (selected == null && !options.AllowNone)
        {
            EditorGUILayout.HelpBox(
                "상태 효과를 선택하세요.",
                MessageType.Error);
        }
    }

    private static int GetSelectedCount(
        SerializedProperty statusEffects,
        SerializedProperty legacyStatusEffect)
    {
        if (statusEffects.arraySize > 0)
        {
            int count = 0;
            for (int index = 0; index < statusEffects.arraySize; index++)
            {
                if (statusEffects.GetArrayElementAtIndex(index)
                        .objectReferenceValue is StatusEffectSO)
                {
                    count++;
                }
            }

            return count;
        }

        return legacyStatusEffect?.objectReferenceValue is StatusEffectSO
            ? 1
            : 0;
    }

    private static void DrawSelectedRows(
        SerializedProperty statusEffects,
        SerializedProperty legacyStatusEffect,
        PS260714StatusEffectSelectionOptions options)
    {
        if (statusEffects.arraySize == 0)
        {
            if (legacyStatusEffect?.objectReferenceValue is
                StatusEffectSO legacy)
            {
                DrawSelectedRow(
                    legacy,
                    () =>
                    {
                        legacyStatusEffect.objectReferenceValue = null;
                        legacyStatusEffect.serializedObject
                            .ApplyModifiedProperties();
                    });
            }
            else if (!options.AllowNone)
            {
                EditorGUILayout.HelpBox(
                    "제거할 상태를 하나 이상 선택하세요.",
                    MessageType.Error);
            }

            return;
        }

        int removeIndex = -1;
        for (int index = 0; index < statusEffects.arraySize; index++)
        {
            SerializedProperty item =
                statusEffects.GetArrayElementAtIndex(index);
            StatusEffectSO definition =
                item.objectReferenceValue as StatusEffectSO;
            int capturedIndex = index;
            DrawSelectedRow(
                definition,
                () => removeIndex = capturedIndex);
        }

        if (removeIndex < 0)
            return;

        DeleteArrayElement(statusEffects, removeIndex);
        if (statusEffects.arraySize == 0 &&
            legacyStatusEffect != null)
        {
            legacyStatusEffect.objectReferenceValue = null;
        }
        statusEffects.serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSelectedRow(
        StatusEffectSO definition,
        Action remove)
    {
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                definition,
                typeof(StatusEffectSO),
                false);
        }
        using (new EditorGUI.DisabledScope(definition == null))
        {
            if (GUILayout.Button(
                    "Edit",
                    EditorStyles.miniButton,
                    GUILayout.Width(40f)))
            {
                StatusEffectEditorWindow.Open(definition);
            }
        }
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24f)))
            remove?.Invoke();
        EditorGUILayout.EndHorizontal();
    }

    private static void DeleteArrayElement(
        SerializedProperty array,
        int index)
    {
        int previousSize = array.arraySize;
        array.DeleteArrayElementAtIndex(index);
        if (array.arraySize == previousSize)
            array.DeleteArrayElementAtIndex(index);
    }

    private sealed class StatusEffectSingleSelectPopup : PopupWindowContent
    {
        private enum AlignmentFilter
        {
            All = 0,
            Buff = 1,
            Debuff = 2,
            Neutral = 3
        }

        private static readonly string[] FilterLabels =
        {
            "전체",
            "버프",
            "디버프",
            "중립"
        };

        private readonly UnityEngine.Object _target;
        private readonly string _statusEffectPath;
        private readonly PS260714StatusEffectSelectionOptions _options;
        private readonly List<StatusEffectSO> _definitions = new();

        private Vector2 _scroll;
        private string _searchText = string.Empty;
        private AlignmentFilter _filter;

        internal StatusEffectSingleSelectPopup(
            UnityEngine.Object target,
            string statusEffectPath,
            PS260714StatusEffectSelectionOptions options)
        {
            _target = target;
            _statusEffectPath = statusEffectPath;
            _options = options;
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:StatusEffectSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatusEffectSO definition =
                    AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
                if (definition != null)
                    _definitions.Add(definition);
            }

            _definitions.Sort((left, right) => string.Compare(
                left.name,
                right.name,
                StringComparison.OrdinalIgnoreCase));
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(380f, 430f);
        }

        public override void OnGUI(Rect rect)
        {
            if (_target == null)
            {
                editorWindow.Close();
                return;
            }

            _filter = (AlignmentFilter)GUILayout.Toolbar(
                (int)_filter,
                FilterLabels);
            _searchText = EditorGUILayout.TextField(
                _searchText,
                EditorStyles.toolbarSearchField);

            SerializedObject serialized = new(_target);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty statusEffect =
                serialized.FindProperty(_statusEffectPath);
            if (statusEffect == null)
            {
                EditorGUILayout.HelpBox(
                    "Status effect property could not be found.",
                    MessageType.Error);
                return;
            }

            StatusEffectSO selected =
                statusEffect.objectReferenceValue as StatusEffectSO;
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_scroll))
            {
                _scroll = scroll.scrollPosition;
                if (_options.AllowNone)
                {
                    bool noneSelected = selected == null;
                    bool chooseNone = EditorGUILayout.ToggleLeft(
                        "없음",
                        noneSelected,
                        GUILayout.Height(24f));
                    if (chooseNone && !noneSelected)
                    {
                        SetSelection(serialized, statusEffect, null);
                        return;
                    }
                }

                int visibleCount = 0;
                foreach (StatusEffectSO definition in _definitions)
                {
                    if (!MatchesFilter(definition))
                        continue;

                    visibleCount++;
                    bool isSelected = IsSameStatus(
                        selected,
                        definition);
                    bool choose = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            definition.name,
                            PS260714AssetEditorList.GetAssetPreview(
                                definition.Icon),
                            definition.StatusId),
                        isSelected,
                        GUILayout.Height(24f));
                    if (choose && !isSelected)
                    {
                        SetSelection(
                            serialized,
                            statusEffect,
                            definition);
                        return;
                    }
                    if (!choose && isSelected && _options.AllowNone)
                    {
                        SetSelection(serialized, statusEffect, null);
                        return;
                    }
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "현재 필터에 맞는 상태 효과가 없습니다.",
                        MessageType.Info);
                }
            }
        }

        private void SetSelection(
            SerializedObject serialized,
            SerializedProperty statusEffect,
            StatusEffectSO definition)
        {
            Undo.RecordObject(_target, "Change Status Effect Selection");
            statusEffect.objectReferenceValue = definition;
            serialized.ApplyModifiedProperties();
            editorWindow.Close();
        }

        private bool MatchesFilter(StatusEffectSO definition)
        {
            if (definition == null)
                return false;
            if (_options.RequireRemovable && !definition.Removable)
                return false;
            if (_options.TargetFaction.HasValue)
            {
                bool canTarget = _options.TargetFaction.Value ==
                    CharacterTargetFaction.Ally
                        ? definition.CanTargetAlly
                        : definition.CanTargetEnemy;
                if (!canTarget)
                    return false;
            }

            string search = (_searchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(search) &&
                definition.name.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                (definition.NameLocalizationKey ?? string.Empty).IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                (definition.StatusId ?? string.Empty).IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return _filter switch
            {
                AlignmentFilter.Buff =>
                    definition.Alignment == StatusEffectAlignment.Buff,
                AlignmentFilter.Debuff =>
                    definition.Alignment == StatusEffectAlignment.Debuff,
                AlignmentFilter.Neutral =>
                    definition.Alignment == StatusEffectAlignment.Neutral,
                _ => true
            };
        }
    }

    private sealed class StatusEffectMultiSelectPopup : PopupWindowContent
    {
        private enum AlignmentFilter
        {
            All = 0,
            Buff = 1,
            Debuff = 2,
            Neutral = 3
        }

        private static readonly string[] FilterLabels =
        {
            "전체",
            "버프",
            "디버프",
            "중립"
        };

        private readonly UnityEngine.Object _target;
        private readonly string _statusEffectsPath;
        private readonly string _legacyStatusEffectPath;
        private readonly PS260714StatusEffectSelectionOptions _options;
        private readonly List<StatusEffectSO> _definitions = new();

        private Vector2 _scroll;
        private string _searchText = string.Empty;
        private AlignmentFilter _filter;

        public StatusEffectMultiSelectPopup(
            UnityEngine.Object target,
            string statusEffectsPath,
            string legacyStatusEffectPath,
            PS260714StatusEffectSelectionOptions options)
        {
            _target = target;
            _statusEffectsPath = statusEffectsPath;
            _legacyStatusEffectPath = legacyStatusEffectPath;
            _options = options;
            LoadDefinitions();
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(380f, 430f);
        }

        public override void OnGUI(Rect rect)
        {
            if (_target == null)
            {
                editorWindow.Close();
                return;
            }

            _filter = (AlignmentFilter)GUILayout.Toolbar(
                (int)_filter,
                FilterLabels);
            _searchText = EditorGUILayout.TextField(
                _searchText,
                EditorStyles.toolbarSearchField);

            SerializedObject serialized = new(_target);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty statusEffects =
                serialized.FindProperty(_statusEffectsPath);
            SerializedProperty legacyStatusEffect =
                string.IsNullOrEmpty(_legacyStatusEffectPath)
                    ? null
                    : serialized.FindProperty(_legacyStatusEffectPath);
            if (statusEffects == null)
            {
                EditorGUILayout.HelpBox(
                    "상태 선택 목록을 찾을 수 없습니다.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField(
                $"{GetSelectedCount(statusEffects, legacyStatusEffect)}개 선택",
                EditorStyles.miniLabel);
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_scroll))
            {
                _scroll = scroll.scrollPosition;
                int visibleCount = 0;
                foreach (StatusEffectSO definition in _definitions)
                {
                    if (!MatchesFilter(definition))
                        continue;

                    visibleCount++;
                    bool selected = Contains(
                        statusEffects,
                        legacyStatusEffect,
                        definition);
                    bool toggled = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            definition.name,
                            PS260714AssetEditorList.GetAssetPreview(
                                definition.Icon),
                            definition.StatusId),
                        selected,
                        GUILayout.Height(24f));
                    if (toggled != selected)
                    {
                        Toggle(
                            serialized,
                            statusEffects,
                            legacyStatusEffect,
                            definition,
                            toggled);
                    }
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "조건에 맞는 상태가 없습니다.",
                        MessageType.Info);
                }
            }
        }

        private void LoadDefinitions()
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:StatusEffectSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatusEffectSO definition =
                    AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
                if (definition != null)
                    _definitions.Add(definition);
            }

            _definitions.Sort((left, right) => string.Compare(
                left.name,
                right.name,
                StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesFilter(StatusEffectSO definition)
        {
            if (definition == null)
                return false;
            if (_options.RequireRemovable && !definition.Removable)
                return false;
            if (_options.TargetFaction.HasValue)
            {
                bool canTarget = _options.TargetFaction.Value ==
                    CharacterTargetFaction.Ally
                        ? definition.CanTargetAlly
                        : definition.CanTargetEnemy;
                if (!canTarget)
                    return false;
            }

            string search = (_searchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(search) &&
                definition.name.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                (definition.NameLocalizationKey ?? string.Empty).IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                (definition.StatusId ?? string.Empty).IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return _filter switch
            {
                AlignmentFilter.Buff =>
                    definition.Alignment == StatusEffectAlignment.Buff,
                AlignmentFilter.Debuff =>
                    definition.Alignment == StatusEffectAlignment.Debuff,
                AlignmentFilter.Neutral =>
                    definition.Alignment == StatusEffectAlignment.Neutral,
                _ => true
            };
        }

        private static bool Contains(
            SerializedProperty statusEffects,
            SerializedProperty legacyStatusEffect,
            StatusEffectSO definition)
        {
            if (statusEffects.arraySize == 0)
            {
                return IsSameStatus(
                    legacyStatusEffect?.objectReferenceValue as
                        StatusEffectSO,
                    definition);
            }

            for (int index = 0; index < statusEffects.arraySize; index++)
            {
                if (IsSameStatus(
                        statusEffects.GetArrayElementAtIndex(index)
                            .objectReferenceValue as StatusEffectSO,
                        definition))
                {
                    return true;
                }
            }

            return false;
        }

        private void Toggle(
            SerializedObject serialized,
            SerializedProperty statusEffects,
            SerializedProperty legacyStatusEffect,
            StatusEffectSO definition,
            bool selected)
        {
            Undo.RecordObject(_target, "Change Status Removal Selection");
            MaterializeLegacySelection(
                statusEffects,
                legacyStatusEffect);
            if (selected)
            {
                if (!Contains(statusEffects, null, definition))
                {
                    int index = statusEffects.arraySize;
                    statusEffects.InsertArrayElementAtIndex(index);
                    statusEffects.GetArrayElementAtIndex(index)
                        .objectReferenceValue = definition;
                }
            }
            else
            {
                for (int index = statusEffects.arraySize - 1;
                     index >= 0;
                     index--)
                {
                    if (!IsSameStatus(
                            statusEffects.GetArrayElementAtIndex(index)
                                .objectReferenceValue as StatusEffectSO,
                            definition))
                    {
                        continue;
                    }

                    DeleteArrayElement(statusEffects, index);
                }
            }

            if (statusEffects.arraySize == 0 &&
                legacyStatusEffect != null)
            {
                legacyStatusEffect.objectReferenceValue = null;
            }
            serialized.ApplyModifiedProperties();
        }

        private static void MaterializeLegacySelection(
            SerializedProperty statusEffects,
            SerializedProperty legacyStatusEffect)
        {
            if (statusEffects.arraySize > 0 ||
                legacyStatusEffect?.objectReferenceValue is not
                    StatusEffectSO legacy)
            {
                return;
            }

            statusEffects.InsertArrayElementAtIndex(0);
            statusEffects.GetArrayElementAtIndex(0)
                .objectReferenceValue = legacy;
        }
    }

    private static bool IsSameStatus(
        StatusEffectSO left,
        StatusEffectSO right)
    {
        return CharacterStatusSelection.IsSameStatus(left, right);
    }
}
