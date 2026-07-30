using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class PS260714EditorMenu
{
    public const string Root = "PS260714/";

    public const string CharacterEditor =
        Root + "Character Editor";
    public const string RecruitEditor =
        Root + "Recruit Editor";
    public const string ItemEditor =
        Root + "Item Editor";
    public const string EnemyEditor =
        Root + "Enemy Editor";
    public const string StatusEffectEditor =
        Root + "Status Effect Editor";
    public const string BattleVfxEditor =
        Root + "Effects/Battle VFX Editor";
    public const string ValidateBattleVfx =
        Root + "Effects/Validate Battle VFX";
    public const string LocalizationEditor =
        Root + "Localization/Localization Editor";
    public const string ValidateLocalization =
        Root + "Localization/Validate CSV";
    public const string GenerateLocalization =
        Root + "Localization/Generate C#";
}

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

    public static void Draw(
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
            GUIContent.none,
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
