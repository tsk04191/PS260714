using System;
using System.Collections.Generic;
using System.IO;
using PS260714.Localization.Editor;
using UnityEditor;
using UnityEngine;

public sealed class StatusEffectEditorWindow : EditorWindow
{
    public const string MenuPath = "Tools/Dungeon/Status Effect Editor";

    private const string AssetFolder = "Assets/Resources/StatusEffects";
    private const string LocalizationPrefix = "status.";
    private const string RenameControlName = "StatusEffectRenameField";
    private const float ListWidth = 230f;

    private static readonly string[] AlignmentOptions =
    {
        "버프",
        "디버프",
        "중립"
    };

    private static readonly string[] DurationModeOptions =
    {
        "시간제",
        "영구"
    };

    private static readonly string[] StackModeOptions =
    {
        "스택 추가 + 시간 갱신",
        "스택 추가 + 시간 유지",
        "적용 묶음별 순차 지속시간",
        "기존 상태 교체"
    };

    private static readonly string[] RemovalOrderOptions =
    {
        "오래된 스택부터",
        "새로운 스택부터",
        "랜덤"
    };

    private static readonly string[] OperationTriggerOptions =
    {
        "적용 시",
        "주기마다",
        "만료 시",
        "제거 시",
        "스택 변경 시"
    };

    private static readonly string[] OperationTypeOptions =
    {
        "주기 피해",
        "즉시 피해",
        "공격력 변경",
        "속도 변경",
        "행동 불가"
    };

    private static readonly string[] ValueModeOptions =
    {
        "고정",
        "비율"
    };

    private readonly List<StatusEffectSO> _definitions = new();
    private readonly List<LocalizationKeyOption> _nameKeys = new();
    private readonly List<LocalizationKeyOption> _descriptionKeys = new();

    private StatusEffectSO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private string _localizationLoadError = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _identityExpanded = true;
    private bool _presentationExpanded = true;
    private bool _durationExpanded = true;
    private bool _stackExpanded = true;
    private bool _removalExpanded = true;
    private bool _operationsExpanded = true;

    private readonly struct LocalizationKeyOption
    {
        public string Key { get; }
        public string Label { get; }

        public LocalizationKeyOption(string key, string label)
        {
            Key = key;
            Label = label;
        }
    }

    [MenuItem(MenuPath)]
    public static void Open()
    {
        StatusEffectEditorWindow window =
            GetWindow<StatusEffectEditorWindow>();
        window.titleContent = new GUIContent("Status Effects");
        window.minSize = new Vector2(820f, 520f);
        window.Show();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Status Effects");
        Selection.selectionChanged += HandleSelectionChanged;
        RefreshLocalizationKeys();
        RefreshList();
        HandleSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (_isRenaming)
            DrawRenameRow();

        EditorGUILayout.BeginHorizontal();
        DrawAssetList();
        DrawEditor();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("New", EditorStyles.toolbarButton,
                GUILayout.Width(48f)))
        {
            CreateDefinition();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton,
                GUILayout.Width(62f)))
        {
            RefreshLocalizationKeys();
            RefreshList();
        }

        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(_selected == null))
        {
            if (GUILayout.Button("Save", EditorStyles.toolbarButton,
                    GUILayout.Width(50f)))
            {
                SaveSelected();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton,
                    GUILayout.Width(72f)))
            {
                DuplicateSelected();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Rename", EditorStyles.toolbarButton,
                    GUILayout.Width(62f)))
            {
                BeginRename();
            }

            if (GUILayout.Button("Delete", EditorStyles.toolbarButton,
                    GUILayout.Width(56f)))
            {
                DeleteSelected();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRenameRow()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("SO File Name", GUILayout.Width(90f));
        GUI.SetNextControlName(RenameControlName);
        _renameAssetName = EditorGUILayout.TextField(_renameAssetName);
        bool apply = GUILayout.Button("OK", GUILayout.Width(44f));
        bool cancel = GUILayout.Button("Cancel", GUILayout.Width(58f));
        EditorGUILayout.EndHorizontal();

        if (_focusRenameField)
        {
            EditorGUI.FocusTextInControl(RenameControlName);
            _focusRenameField = false;
        }

        Event current = Event.current;
        if (current.type == EventType.KeyDown)
        {
            if (current.keyCode == KeyCode.Return ||
                current.keyCode == KeyCode.KeypadEnter)
            {
                apply = true;
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape)
            {
                cancel = true;
                current.Use();
            }
        }

        if (cancel)
        {
            CancelRename();
            GUIUtility.ExitGUI();
        }
        if (apply)
        {
            RenameSelected();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawAssetList()
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox,
            GUILayout.Width(ListWidth),
            GUILayout.ExpandHeight(true));
        _searchText = EditorGUILayout.TextField(
            _searchText,
            EditorStyles.toolbarSearchField);
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

        int visibleCount = 0;
        foreach (StatusEffectSO definition in _definitions)
        {
            if (definition == null || !MatchesSearch(definition))
                continue;

            visibleCount++;
            bool selected = ReferenceEquals(definition, _selected);
            GUIStyle style = selected
                ? EditorStyles.miniButtonMid
                : EditorStyles.miniButton;
            if (GUILayout.Button(
                    new GUIContent(definition.name, definition.Icon?.texture),
                    style,
                    GUILayout.Height(26f)))
            {
                SelectDefinition(definition, true);
            }
        }

        if (visibleCount == 0)
        {
            EditorGUILayout.HelpBox(
                _definitions.Count == 0
                    ? "상태 이펙트 SO가 없습니다."
                    : "검색 결과가 없습니다.",
                MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField(
            $"{visibleCount} / {_definitions.Count}",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawEditor()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (_selected == null || _serialized == null)
        {
            EditorGUILayout.HelpBox(
                "편집할 상태 이펙트를 선택하거나 New로 생성하세요.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _serialized.UpdateIfRequiredOrScript();
        _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);
        DrawValidation();
        DrawIdentity();
        DrawPresentation();
        DrawDuration();
        DrawStack();
        DrawRemoval();
        DrawOperations();
        EditorGUILayout.Space(12f);
        EditorGUILayout.EndScrollView();

        if (_serialized.ApplyModifiedProperties())
        {
            _selected.ValidateDefinition();
            EditorUtility.SetDirty(_selected);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawValidation()
    {
        StatusEffectDefinitionValidationResult validation =
            StatusEffectDefinitionValidator.Validate(
                _selected,
                _definitions);
        foreach (StatusEffectDefinitionDiagnostic diagnostic in
                 validation.Diagnostics)
        {
            MessageType messageType = diagnostic.Severity ==
                                      CharacterDefinitionDiagnosticSeverity.Error
                ? MessageType.Error
                : MessageType.Warning;
            string path = string.IsNullOrWhiteSpace(diagnostic.Path)
                ? "<root>"
                : diagnostic.Path;
            EditorGUILayout.HelpBox(
                $"[{diagnostic.Code}] {path}\n{diagnostic.Message}",
                messageType);
        }

        if (!string.IsNullOrEmpty(_localizationLoadError))
            EditorGUILayout.HelpBox(_localizationLoadError, MessageType.Warning);
    }

    private void DrawIdentity()
    {
        if (!BeginFoldout(ref _identityExpanded, "1. 기본 정보"))
            return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(
            Find("icon"),
            new GUIContent("아이콘"),
            GUILayout.Width(280f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        SerializedProperty id = Find("statusId");
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(id, new GUIContent("Status ID"));
        if (GUILayout.Button("ID 재생성", GUILayout.Width(82f)))
        {
            if (EditorUtility.DisplayDialog(
                    "Regenerate Status ID",
                    "이 상태를 참조하는 저장 데이터가 있다면 연결이 끊어질 수 있습니다.",
                    "OK",
                    "Cancel"))
            {
                Undo.RecordObject(_selected, "Regenerate Status ID");
                _selected.RegenerateStatusId();
                EditorUtility.SetDirty(_selected);
                _serialized.Update();
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawLocalizationKey(
            "nameLocalizationKey",
            "이름 키",
            _nameKeys,
            ".name");
        DrawLocalizationKey(
            "descriptionLocalizationKey",
            "설명 키",
            _descriptionKeys,
            ".description");
        DrawEnum("alignment", "분류", AlignmentOptions);

        EditorGUILayout.LabelField("적용 가능 대상", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(
            Find("canTargetEnemy"),
            new GUIContent("적"));
        EditorGUILayout.PropertyField(
            Find("canTargetAlly"),
            new GUIContent("아군"));
        EditorGUILayout.EndHorizontal();
        EndFoldout();
    }

    private void DrawPresentation()
    {
        if (!BeginFoldout(ref _presentationExpanded, "2. 표시 및 오디오"))
            return;

        DrawProperty("visualEffectPrefab", "VFX 프리팹");
        DrawProperty("iconAnimatorController", "아이콘 애니메이터");
        DrawProperty("applyAudioClip", "적용 오디오");
        DrawProperty("tickAudioClip", "틱 오디오");
        DrawProperty("removeAudioClip", "제거 오디오");
        EndFoldout();
    }

    private void DrawDuration()
    {
        if (!BeginFoldout(ref _durationExpanded, "3. 지속시간 및 작동 주기"))
            return;

        DrawEnum("durationMode", "지속 방식", DurationModeOptions);
        SerializedProperty durationMode = Find("durationMode");
        if (durationMode != null && durationMode.enumValueIndex ==
            (int)StatusEffectDurationMode.Timed)
        {
            SerializedProperty duration = Find("defaultDuration");
            duration.floatValue = Mathf.Max(
                0.1f,
                EditorGUILayout.FloatField(
                    "기본 지속시간 (초)",
                    duration.floatValue));
            DrawProperty("refreshDurationOnReapply", "재부여 시 시간 갱신");
        }

        SerializedProperty tick = Find("tickInterval");
        tick.floatValue = Mathf.Max(
            0.1f,
            EditorGUILayout.FloatField("틱 간격 (초)", tick.floatValue));
        EndFoldout();
    }

    private void DrawStack()
    {
        if (!BeginFoldout(ref _stackExpanded, "4. 스택"))
            return;

        DrawEnum("stackMode", "중첩 방식", StackModeOptions);
        SerializedProperty maximum = Find("maximumStacks");
        maximum.intValue = Mathf.Max(
            0,
            EditorGUILayout.IntField("최대 스택", maximum.intValue));
        EditorGUILayout.LabelField(
            "최대 스택 0은 무제한입니다.",
            EditorStyles.miniLabel);

        SerializedProperty applied = Find("defaultAppliedStacks");
        applied.intValue = Mathf.Max(
            1,
            EditorGUILayout.IntField("기본 부여 스택", applied.intValue));
        if (maximum.intValue > 0)
            applied.intValue = Mathf.Min(applied.intValue, maximum.intValue);
        DrawEnum("stackRemovalOrder", "스택 제거 순서", RemovalOrderOptions);
        EndFoldout();
    }

    private void DrawRemoval()
    {
        if (!BeginFoldout(ref _removalExpanded, "5. 제거 규칙"))
            return;

        SerializedProperty removable = Find("removable");
        EditorGUILayout.PropertyField(removable, new GUIContent("제거 가능"));
        using (new EditorGUI.DisabledScope(!removable.boolValue))
        {
            DrawProperty("includedInRandomRemoval", "랜덤 제거에 포함");
            DrawProperty("includedInAllRemoval", "전체 제거에 포함");
        }
        if (!removable.boolValue)
        {
            Find("includedInRandomRemoval").boolValue = false;
            Find("includedInAllRemoval").boolValue = false;
        }
        EndFoldout();
    }

    private void DrawOperations()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _operationsExpanded = EditorGUILayout.Foldout(
            _operationsExpanded,
            "6. 효과 블록",
            true,
            EditorStyles.foldoutHeader);
        bool add = GUILayout.Button(
            new GUIContent("+", "효과 블록 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        SerializedProperty operations = Find("operations");
        if (add)
            AddOperation(operations);
        if (!_operationsExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox(
            "P2-2 런타임 지원 범위:\n" +
            "• PeriodicDamage: 적 전용 / OnTick\n" +
            "• InstantDamage: 적 전용 / OnApply·OnExpire·OnRemove·" +
            "OnStackChanged\n" +
            "• 능력치 변경·행동 불가: 아군 전용 / OnApply 지속 연산\n" +
            "Damage Ratio는 대상 최대 체력 비율, Modifier Ratio는 " +
            "기본 능력치 비율 가산입니다.",
            MessageType.Info);

        if (operations.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "효과 블록이 없습니다. 상태는 표시와 스택만 유지합니다.",
                MessageType.Info);
        }

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < operations.arraySize; index++)
        {
            SerializedProperty operation =
                operations.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            operation.isExpanded = EditorGUILayout.Foldout(
                operation.isExpanded,
                $"효과 {index + 1}: {GetOperationName(operation)}",
                true);
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("▲", EditorStyles.miniButton,
                        GUILayout.Width(25f)))
                {
                    moveFrom = index;
                    moveTo = index - 1;
                }
            }
            using (new EditorGUI.DisabledScope(
                       index >= operations.arraySize - 1))
            {
                if (GUILayout.Button("▼", EditorStyles.miniButton,
                        GUILayout.Width(25f)))
                {
                    moveFrom = index;
                    moveTo = index + 1;
                }
            }
            if (GUILayout.Button("-", EditorStyles.miniButton,
                    GUILayout.Width(25f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            if (operation.isExpanded)
                DrawOperation(operation);
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0 || moveFrom >= 0)
                break;
        }

        if (removeIndex >= 0)
            operations.DeleteArrayElementAtIndex(removeIndex);
        else if (moveFrom >= 0)
            operations.MoveArrayElement(moveFrom, moveTo);
        EditorGUILayout.EndVertical();
    }

    private static void DrawOperation(SerializedProperty operation)
    {
        DrawEnumProperty(
            operation.FindPropertyRelative("trigger"),
            "작동 시점",
            OperationTriggerOptions);
        SerializedProperty operationType =
            operation.FindPropertyRelative("operationType");
        DrawEnumProperty(
            operationType,
            "효과 종류",
            OperationTypeOptions);

        StatusEffectOperationType selectedType = operationType != null
            ? (StatusEffectOperationType)operationType.enumValueIndex
            : StatusEffectOperationType.PeriodicDamage;
        switch (selectedType)
        {
            case StatusEffectOperationType.PeriodicDamage:
                EditorGUILayout.HelpBox(
                    "적 전용 상태에서 OnTick마다 피해를 줍니다.",
                    MessageType.Info);
                break;

            case StatusEffectOperationType.InstantDamage:
                EditorGUILayout.HelpBox(
                    "적 전용 상태에서 적용·만료·제거·스택 변경 시점에 " +
                    "즉시 피해를 줍니다. OnTick은 사용할 수 없습니다.",
                    MessageType.Info);
                break;

            case StatusEffectOperationType.AttackPowerModifier:
                EditorGUILayout.HelpBox(
                    "아군 전용 상태가 유지되는 동안 공격력을 변경합니다. " +
                    "작동 시점은 OnApply를 사용합니다.",
                    MessageType.Info);
                break;

            case StatusEffectOperationType.AttackSpeedModifier:
                EditorGUILayout.HelpBox(
                    "아군 전용 상태가 유지되는 동안 공격 속도를 " +
                    "변경합니다. 작동 시점은 OnApply를 사용합니다.",
                    MessageType.Info);
                break;

            case StatusEffectOperationType.DisableAction:
                EditorGUILayout.HelpBox(
                    "아군 전용 상태가 유지되는 동안 행동을 중지합니다. " +
                    "작동 시점은 OnApply를 사용합니다.",
                    MessageType.Info);
                break;
        }

        if (selectedType == StatusEffectOperationType.DisableAction)
        {
            return;
        }

        SerializedProperty valueMode =
            operation.FindPropertyRelative("valueMode");
        DrawEnumProperty(
            valueMode,
            "수치 방식",
            ValueModeOptions);
        SerializedProperty value = operation.FindPropertyRelative("value");
        value.floatValue = EditorGUILayout.FloatField("수치", value.floatValue);
        bool isModifier = selectedType ==
                          StatusEffectOperationType.AttackPowerModifier ||
                          selectedType ==
                          StatusEffectOperationType.AttackSpeedModifier;
        bool isRatio = valueMode != null &&
                       valueMode.enumValueIndex ==
                       (int)StatusEffectValueMode.Ratio;
        if (isModifier)
        {
            EditorGUILayout.HelpBox(
                isRatio
                    ? "Ratio: 기본 능력치 × 수치를 원시 능력치에 " +
                      "가산합니다. 음수는 감소 효과입니다."
                    : "Fixed: 수치를 원시 능력치에 직접 가산합니다. " +
                      "음수는 감소 효과입니다.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                isRatio
                    ? "Ratio: 대상 최대 체력 × 수치만큼 피해를 줍니다."
                    : "Fixed: 수치만큼 고정된 양의 피해를 줍니다.",
                MessageType.None);
        }

        SerializedProperty scaleWithStacks =
            operation.FindPropertyRelative("scaleWithStacks");
        EditorGUILayout.PropertyField(
            scaleWithStacks,
            new GUIContent("스택 수 적용"));
        EditorGUILayout.HelpBox(
            scaleWithStacks != null && scaleWithStacks.boolValue
                ? "스택 수 적용: 계산된 효과를 현재 스택 수만큼 " +
                  "곱합니다."
                : "스택 수 미적용: 스택 수와 관계없이 한 번의 수치만 " +
                  "적용합니다.",
            MessageType.None);
    }

    private static string GetOperationName(SerializedProperty operation)
    {
        SerializedProperty type = operation.FindPropertyRelative(
            "operationType");
        int index = type != null ? type.enumValueIndex : 0;
        return OperationTypeOptions[Mathf.Clamp(
            index,
            0,
            OperationTypeOptions.Length - 1)];
    }

    private static void AddOperation(SerializedProperty operations)
    {
        int index = operations.arraySize;
        operations.InsertArrayElementAtIndex(index);
        SerializedProperty operation = operations.GetArrayElementAtIndex(index);
        operation.isExpanded = true;
        operation.FindPropertyRelative("trigger").enumValueIndex =
            (int)StatusEffectOperationTrigger.OnTick;
        operation.FindPropertyRelative("operationType").enumValueIndex =
            (int)StatusEffectOperationType.PeriodicDamage;
        operation.FindPropertyRelative("valueMode").enumValueIndex =
            (int)StatusEffectValueMode.Fixed;
        operation.FindPropertyRelative("value").floatValue = 1f;
        operation.FindPropertyRelative("scaleWithStacks").boolValue = true;
    }

    private void DrawLocalizationKey(
        string propertyName,
        string label,
        List<LocalizationKeyOption> options,
        string suffix)
    {
        SerializedProperty property = Find(propertyName);
        property.stringValue = EditorGUILayout.TextField(
            label,
            property.stringValue ?? string.Empty);

        string[] labels = new string[options.Count + 1];
        labels[0] = "(직접 입력 유지)";
        int currentIndex = 0;
        for (int index = 0; index < options.Count; index++)
        {
            labels[index + 1] = options[index].Label;
            if (string.Equals(
                    property.stringValue,
                    options[index].Key,
                    StringComparison.Ordinal))
            {
                currentIndex = index + 1;
            }
        }

        int selectedIndex = EditorGUILayout.Popup(
            "필터 선택",
            currentIndex,
            labels);
        if (selectedIndex > 0 && selectedIndex != currentIndex)
            property.stringValue = options[selectedIndex - 1].Key;

        if (options.Count == 0)
        {
            EditorGUILayout.LabelField(
                $"{LocalizationPrefix}*{suffix} 키가 없습니다.",
                EditorStyles.miniLabel);
        }
    }

    private bool BeginFoldout(ref bool expanded, string title)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(
            expanded,
            title,
            true,
            EditorStyles.foldoutHeader);
        if (expanded)
            return true;

        EditorGUILayout.EndVertical();
        return false;
    }

    private static void EndFoldout()
    {
        EditorGUILayout.EndVertical();
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = Find(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawEnum(string propertyName, string label, string[] options)
    {
        DrawEnumProperty(Find(propertyName), label, options);
    }

    private static void DrawEnumProperty(
        SerializedProperty property,
        string label,
        string[] options)
    {
        if (property == null)
            return;

        property.enumValueIndex = EditorGUILayout.Popup(
            label,
            Mathf.Clamp(property.enumValueIndex, 0, options.Length - 1),
            options);
    }

    private SerializedProperty Find(string propertyName)
    {
        return _serialized?.FindProperty(propertyName);
    }

    private bool MatchesSearch(StatusEffectSO definition)
    {
        string search = (_searchText ?? string.Empty).Trim();
        return string.IsNullOrEmpty(search) ||
               definition.name.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.StatusId.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               (definition.NameLocalizationKey ?? string.Empty).IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void HandleSelectionChanged()
    {
        if (Selection.activeObject is StatusEffectSO definition)
            SelectDefinition(definition, false);
        Repaint();
    }

    private void SelectDefinition(StatusEffectSO definition, bool pingProject)
    {
        if (definition == null)
            return;

        if (!ReferenceEquals(_selected, definition))
            CancelRename();
        _selected = definition;
        _serialized = new SerializedObject(definition);
        _editorScroll = Vector2.zero;
        if (pingProject)
            Selection.activeObject = definition;
    }

    private void RefreshList()
    {
        string selectedPath = _selected != null
            ? AssetDatabase.GetAssetPath(_selected)
            : string.Empty;
        _definitions.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:StatusEffectSO"))
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
        StatusEffectDefinitionCatalog.Invalidate();

        if (!string.IsNullOrEmpty(selectedPath))
        {
            StatusEffectSO restored =
                AssetDatabase.LoadAssetAtPath<StatusEffectSO>(selectedPath);
            if (restored != null)
                SelectDefinition(restored, false);
        }
        else if (_selected == null && _definitions.Count > 0)
        {
            SelectDefinition(_definitions[0], false);
        }
    }

    private void RefreshLocalizationKeys()
    {
        _nameKeys.Clear();
        _descriptionKeys.Clear();
        _localizationLoadError = string.Empty;
        try
        {
            LocalizationSourceModel model =
                LocalizationCodeGenerator.LoadSource();
            foreach (LocalizationSourceString entry in model.Strings)
            {
                string key = (entry.Key ?? string.Empty).Trim();
                if (!key.StartsWith(
                        LocalizationPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LocalizationKeyOption option = new(
                    key,
                    BuildLocalizationLabel(entry, key));
                if (key.EndsWith(".name", StringComparison.OrdinalIgnoreCase))
                    _nameKeys.Add(option);
                else if (key.EndsWith(
                             ".description",
                             StringComparison.OrdinalIgnoreCase))
                    _descriptionKeys.Add(option);
            }

            _nameKeys.Sort(CompareLocalizationKeys);
            _descriptionKeys.Sort(CompareLocalizationKeys);
        }
        catch (Exception exception)
        {
            _localizationLoadError =
                "Localization 키를 불러오지 못했습니다: " +
                exception.Message;
        }
    }

    private static string BuildLocalizationLabel(
        LocalizationSourceString entry,
        string key)
    {
        entry.Translations.TryGetValue("ko-KR", out string korean);
        entry.Translations.TryGetValue("en-US", out string english);
        string preview = !string.IsNullOrWhiteSpace(korean)
            ? korean.Trim()
            : !string.IsNullOrWhiteSpace(english)
                ? english.Trim()
                : string.Empty;
        return string.IsNullOrEmpty(preview) ? key : $"{key} — {preview}";
    }

    private static int CompareLocalizationKeys(
        LocalizationKeyOption left,
        LocalizationKeyOption right)
    {
        return string.Compare(
            left.Key,
            right.Key,
            StringComparison.OrdinalIgnoreCase);
    }

    private void CreateDefinition()
    {
        EnsureFolder(AssetFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Status Effect",
            "NewStatusEffect",
            "asset",
            "상태 이펙트 SO를 생성할 위치를 선택하세요.",
            AssetFolder);
        if (string.IsNullOrEmpty(path))
            return;

        StatusEffectSO definition = CreateInstance<StatusEffectSO>();
        definition.name = Path.GetFileNameWithoutExtension(path);
        definition.RegenerateStatusId();
        definition.ValidateDefinition();
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssetIfDirty(definition);
        RefreshList();
        SelectDefinition(definition, true);
        EditorGUIUtility.PingObject(definition);
    }

    private void SaveSelected()
    {
        if (_selected == null)
            return;

        _serialized?.ApplyModifiedProperties();
        _selected.ValidateDefinition();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssetIfDirty(_selected);
        StatusEffectDefinitionCatalog.Invalidate();
        ShowNotification(new GUIContent($"Saved {_selected.name}.asset"));
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(_selected);
        string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{fileName} Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            return;

        AssetDatabase.SaveAssets();
        StatusEffectSO duplicate =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(destinationPath);
        if (duplicate != null)
        {
            duplicate.RegenerateStatusId();
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }
        RefreshList();
        if (duplicate != null)
            SelectDefinition(duplicate, true);
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        string path = AssetDatabase.GetAssetPath(_selected);
        string assetName = _selected.name;
        if (!EditorUtility.DisplayDialog(
                "Delete StatusEffectSO",
                $"'{assetName}' SO 파일을 삭제합니다.\n\n{path}\n\n" +
                "이 작업은 Unity Undo로 복구할 수 없습니다.",
                "OK",
                "Cancel"))
        {
            return;
        }

        if (!AssetDatabase.DeleteAsset(path))
        {
            EditorUtility.DisplayDialog(
                "Delete StatusEffectSO",
                "SO 파일을 삭제하지 못했습니다.",
                "OK");
            return;
        }

        _selected = null;
        _serialized = null;
        CancelRename();
        RefreshList();
        ShowNotification(new GUIContent($"Deleted {assetName}.asset"));
    }

    private void BeginRename()
    {
        if (_selected == null)
            return;

        _renameAssetName = Path.GetFileNameWithoutExtension(
            AssetDatabase.GetAssetPath(_selected));
        _isRenaming = true;
        _focusRenameField = true;
        Repaint();
    }

    private void CancelRename()
    {
        _isRenaming = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
    }

    private void RenameSelected()
    {
        if (_selected == null)
        {
            CancelRename();
            return;
        }

        string name = (_renameAssetName ?? string.Empty).Trim();
        if (name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 6).Trim();
        if (!IsValidFileName(name, out string error))
        {
            EditorUtility.DisplayDialog("Rename StatusEffectSO", error, "OK");
            _focusRenameField = true;
            return;
        }

        string path = AssetDatabase.GetAssetPath(_selected);
        string renameError = AssetDatabase.RenameAsset(path, name);
        if (!string.IsNullOrEmpty(renameError))
        {
            EditorUtility.DisplayDialog(
                "Rename StatusEffectSO",
                renameError,
                "OK");
            _focusRenameField = true;
            return;
        }

        CancelRename();
        AssetDatabase.SaveAssets();
        RefreshList();
        EditorGUIUtility.PingObject(_selected);
    }

    private static bool IsValidFileName(string fileName, out string error)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "파일 이름을 입력하세요.";
            return false;
        }
        if (fileName == "." || fileName == ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0 ||
            fileName.EndsWith(".", StringComparison.Ordinal) ||
            fileName.EndsWith(" ", StringComparison.Ordinal))
        {
            error = "사용할 수 없는 파일 이름입니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
