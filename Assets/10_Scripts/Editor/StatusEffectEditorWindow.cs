using System;
using System.Collections.Generic;
using System.IO;
using PS260714.Localization.Editor;
using UnityEditor;
using UnityEngine;

public sealed class StatusEffectEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.StatusEffectEditor;

    private const string AssetFolder = "Assets/06_Runtime/Resources/StatusEffects";
    private const string RenameControlName = "StatusEffectRenameField";
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
        "무작위"
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
        "공격 속도 변경",
        "행동 불가"
    };

    private static readonly string[] ValueModeOptions =
    {
        "고정",
        "비율"
    };

    private static readonly string[] LifecycleTriggerOptions =
    {
        "최초 적용 시",
        "재적용 시",
        "주기마다",
        "스택 변경 시",
        "자연 만료 시",
        "수동 제거 시"
    };

    private static readonly string[] StatTypeOptions =
    {
        "공격력",
        "공격 속도",
        "받는 피해",
        "대상 우선순위"
    };

    private static readonly string[] StatModifierModeOptions =
    {
        "고정 가산",
        "기본값 기준 비율 가산",
        "곱연산 비율"
    };

    private static readonly string[] ControlTypeOptions =
    {
        "전체 행동 불가",
        "기본 공격 금지",
        "액티브 스킬 금지",
        "패시브 쿨다운 정지",
        "강제 포커싱"
    };

    private readonly List<StatusEffectSO> _definitions = new();
    private StatusEffectSO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _identityExpanded = true;
    private bool _presentationExpanded = true;
    private bool _durationExpanded = true;
    private bool _stackExpanded = true;
    private bool _removalExpanded = true;
    private bool _triggerBlocksExpanded = true;
    private bool _statModifiersExpanded = true;
    private bool _controlEffectsExpanded = true;
    private bool _operationsExpanded;


    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.StatusEffectEditorPriority)]
    public static void Open()
    {
        StatusEffectEditorWindow window =
            GetWindow<StatusEffectEditorWindow>();
        window.titleContent = new GUIContent("Status Effects");
        window.minSize = new Vector2(820f, 520f);
        window.Show();
    }

    public static void Open(StatusEffectSO definition)
    {
        Open();
        StatusEffectEditorWindow window =
            GetWindow<StatusEffectEditorWindow>();
        window.RefreshList();
        if (definition != null)
            window.SelectDefinition(definition);
        window.Repaint();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.StatusEffectEditorPriority)]
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

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAssetList();
            DrawEditor();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Status Effects: {_definitions.Count}",
            _selected != null,
            () =>
            {
                CreateDefinition();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                SaveSelected();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                DuplicateSelected();
                GUIUtility.ExitGUI();
            },
            BeginRename,
            () =>
            {
                DeleteSelected();
                GUIUtility.ExitGUI();
            },
            () => PS260714AssetEditorList.Ping(_selected),
            () =>
            {
                RefreshLocalizationKeys();
                RefreshList();
            });
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

    private void DrawAssetList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width),
                   GUILayout.ExpandHeight(true)))
        {
        _searchText =
            PS260714AssetEditorList.DrawSearchField(_searchText);
        int visibleCount = 0;
        using (EditorGUILayout.ScrollViewScope scrollView =
               new(_listScroll))
        {
            _listScroll = scrollView.scrollPosition;

        foreach (StatusEffectSO definition in _definitions)
        {
            if (definition == null || !MatchesSearch(definition))
                continue;

            visibleCount++;
            bool selected = ReferenceEquals(definition, _selected);
            if (PS260714AssetEditorList.DrawAssetRow(
                    selected,
                    definition,
                    definition.Icon,
                    definition.name,
                    definition.Alignment.ToString(),
                    definition.StatusId))
            {
                SelectDefinition(definition);
            }
        }

        if (visibleCount == 0)
        {
            EditorGUILayout.HelpBox(
                _definitions.Count == 0
                    ? "상태 효과 SO가 없습니다."
                    : "검색 결과가 없습니다.",
                MessageType.Info);
        }

        }
        PS260714AssetEditorList.DrawCountFooter(
            visibleCount,
            _definitions.Count);
        }
    }

    private static Texture GetIconTexture(StatusEffectSO definition)
    {
        if (definition == null)
            return null;

        return PS260714AssetEditorList.GetAssetPreview(
            definition.Icon);
    }

    private void DrawEditor()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (_selected == null || _serialized == null)
        {
            EditorGUILayout.HelpBox(
                "편집할 상태 효과를 선택하거나 New로 생성하세요.",
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
        DrawTriggerBlocks();
        DrawStatModifiers();
        DrawControlEffects();
        DrawOperations();
        EditorGUILayout.Space(12f);
        EditorGUILayout.EndScrollView();

        if (_serialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_selected);
            StatusEffectDefinitionCatalog.Invalidate();
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

        PS260714LocalizationKeyField.DrawLoadError();
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
            "이름 키");
        DrawLocalizationKey(
            "descriptionLocalizationKey",
            "설명 키");
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

        DrawProperty("applyVfxCue", "적용 VFX 큐");
        DrawProperty("loopVfxCue", "유지 VFX 큐");
        DrawProperty("tickVfxCue", "틱 VFX 큐");
        DrawProperty("removeVfxCue", "제거 VFX 큐");
        SerializedProperty legacyVfx = Find("visualEffectPrefab");
        if (legacyVfx != null && legacyVfx.objectReferenceValue != null)
        {
            EditorGUILayout.HelpBox(
                "기존 VFX 프리팹은 호환 목적으로만 유지됩니다. " +
                "3D 전환에는 위 VFX 큐를 사용하세요.",
                MessageType.Warning);
            DrawProperty("visualEffectPrefab", "기존 VFX 프리팹");
        }
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
            DrawProperty("includedInRandomRemoval", "무작위 제거에 포함");
            DrawProperty("includedInAllRemoval", "전체 제거에 포함");
        }
        EndFoldout();
    }

    private void DrawTriggerBlocks()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _triggerBlocksExpanded = EditorGUILayout.Foldout(
            _triggerBlocksExpanded,
            "6. 수명주기 트리거 블록",
            true,
            EditorStyles.foldoutHeader);
        bool add = GUILayout.Button(
            new GUIContent("+", "트리거 블록 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        SerializedProperty blocks = Find("triggerBlocks");
        if (blocks == null)
        {
            EditorGUILayout.HelpBox(
                "트리거 블록 속성을 찾을 수 없습니다.",
                MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }
        if (add)
            AddTriggerBlock(blocks);
        if (!_triggerBlocksExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox(
            "상태의 최초 적용·재적용·틱·스택 변경·만료·제거 시점마다 " +
            "공통 BattleEffect 목록을 에셋에 저장된 순서대로 실행합니다.",
            MessageType.Info);
        if (blocks.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "트리거 블록이 없습니다.",
                MessageType.Info);
        }

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < blocks.arraySize; index++)
        {
            SerializedProperty block = blocks.GetArrayElementAtIndex(index);
            SerializedProperty trigger =
                block.FindPropertyRelative("trigger");
            string triggerName = GetOptionName(
                LifecycleTriggerOptions,
                trigger?.enumValueIndex ?? 0);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            block.isExpanded = EditorGUILayout.Foldout(
                block.isExpanded,
                $"블록 {index + 1}: {triggerName}",
                true);
            DrawMoveButtons(
                index,
                blocks.arraySize,
                ref moveFrom,
                ref moveTo);
            if (GUILayout.Button(
                    new GUIContent("×", "블록 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            if (block.isExpanded)
            {
                DrawEnumProperty(
                    trigger,
                    "작동 시점",
                    LifecycleTriggerOptions);
                EditorGUILayout.PropertyField(
                    block.FindPropertyRelative("scaleWithCurrentStacks"),
                    new GUIContent(
                        "현재 이벤트 스택 적용",
                        "공통 효과 수치에 현재 이벤트 스택 수를 곱합니다."));
                EditorGUILayout.PropertyField(
                    block.FindPropertyRelative("scaleWithOccurrences"),
                    new GUIContent(
                        "발생 횟수 적용",
                        "누적 틱처럼 한 이벤트에 여러 번 발생한 횟수를 " +
                        "공통 효과 수치에 곱합니다."));
                BattleAbilityEditorGUI.DrawEffectList(
                    block.FindPropertyRelative("effects"),
                    _selected);
            }
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0 || moveFrom >= 0)
                break;
        }

        ApplyListAction(blocks, removeIndex, moveFrom, moveTo);
        EditorGUILayout.EndVertical();
    }

    private void DrawStatModifiers()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _statModifiersExpanded = EditorGUILayout.Foldout(
            _statModifiersExpanded,
            "7. 지속 능력치 수정자",
            true,
            EditorStyles.foldoutHeader);
        bool add = GUILayout.Button(
            new GUIContent("+", "능력치 수정자 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        SerializedProperty modifiers = Find("statModifiers");
        if (modifiers == null)
        {
            EditorGUILayout.HelpBox(
                "능력치 수정자 속성을 찾을 수 없습니다.",
                MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }
        if (add)
            AddStatModifier(modifiers);
        if (!_statModifiersExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox(
            "계산 순서: (기본값 + 고정 가산 + 기본값×비율 가산) " +
            "× 곱연산 비율",
            MessageType.Info);
        DrawStatModifierList(modifiers);
        EditorGUILayout.EndVertical();
    }

    private static void DrawStatModifierList(SerializedProperty modifiers)
    {
        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < modifiers.arraySize; index++)
        {
            SerializedProperty modifier =
                modifiers.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"수정자 {index + 1}",
                EditorStyles.boldLabel);
            DrawMoveButtons(
                index,
                modifiers.arraySize,
                ref moveFrom,
                ref moveTo);
            if (GUILayout.Button(
                    new GUIContent("×", "수정자 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            SerializedProperty statType =
                modifier.FindPropertyRelative("statType");
            DrawEnumProperty(
                statType,
                "능력치",
                StatTypeOptions);
            bool isTargetPriority = statType != null &&
                                    statType.enumValueIndex ==
                                    (int)StatusEffectStatType.TargetPriority;
            SerializedProperty mode = modifier.FindPropertyRelative("mode");
            if (isTargetPriority && mode != null)
                mode.enumValueIndex =
                    (int)StatusEffectStatModifierMode.Flat;
            using (new EditorGUI.DisabledScope(isTargetPriority))
            {
                DrawEnumProperty(
                    mode,
                    "연산",
                    StatModifierModeOptions);
            }
            SerializedProperty value = modifier.FindPropertyRelative("value");
            if (value != null)
            {
                value.floatValue = EditorGUILayout.FloatField(
                    isTargetPriority ? "우선순위 가감" : "수치",
                    value.floatValue);
                if (mode != null &&
                    mode.enumValueIndex ==
                        (int)StatusEffectStatModifierMode.MultiplicativeRatio)
                {
                    value.floatValue = Mathf.Max(-1f, value.floatValue);
                }
            }
            EditorGUILayout.PropertyField(
                modifier.FindPropertyRelative("scaleWithStacks"),
                new GUIContent("스택 수 적용"));
            if (isTargetPriority)
            {
                EditorGUILayout.HelpBox(
                    "양수는 도발처럼 먼저 선택되며, 음수는 선택 " +
                    "우선순위를 낮춥니다. 기존 체력·보호막·무작위 " +
                    "대상 규칙보다 먼저 비교합니다.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0 || moveFrom >= 0)
                break;
        }

        ApplyListAction(modifiers, removeIndex, moveFrom, moveTo);
    }

    private void DrawControlEffects()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _controlEffectsExpanded = EditorGUILayout.Foldout(
            _controlEffectsExpanded,
            "8. 제어 효과",
            true,
            EditorStyles.foldoutHeader);
        bool add = GUILayout.Button(
            new GUIContent("+", "제어 효과 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        SerializedProperty controls = Find("controlEffects");
        if (controls == null)
        {
            EditorGUILayout.HelpBox(
                "제어 효과 속성을 찾을 수 없습니다.",
                MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }
        if (add)
            AddControlEffect(controls);
        if (!_controlEffectsExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < controls.arraySize; index++)
        {
            SerializedProperty control =
                controls.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            SerializedProperty controlType =
                control.FindPropertyRelative("controlType");
            DrawEnumProperty(
                controlType,
                $"제어 {index + 1}",
                ControlTypeOptions);
            DrawMoveButtons(
                index,
                controls.arraySize,
                ref moveFrom,
                ref moveTo);
            if (GUILayout.Button(
                    new GUIContent("×", "제어 효과 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();
            if (controlType != null &&
                controlType.enumValueIndex ==
                (int)StatusEffectControlType.ForceTargeting)
            {
                EditorGUILayout.HelpBox(
                    "이 상태의 대상은 모든 우선순위 가감보다 먼저 " +
                    "선택됩니다. 여러 강제 대상이 있으면 원래 대상 " +
                    "선택 규칙으로 순서를 결정합니다.",
                    MessageType.Info);
            }

            if (removeIndex >= 0 || moveFrom >= 0)
                break;
        }

        ApplyListAction(controls, removeIndex, moveFrom, moveTo);
        EditorGUILayout.EndVertical();
    }

    private void DrawOperations()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _operationsExpanded = EditorGUILayout.Foldout(
            _operationsExpanded,
            "9. 기존 호환 효과 (Legacy)",
            true,
            EditorStyles.foldoutHeader);
        bool add = GUILayout.Button(
            new GUIContent("+", "Legacy 효과 추가"),
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
            "기존 Fire/Stun 및 이전 에셋 호환용 영역입니다. 새 상태는 " +
            "위의 트리거·능력치·제어 모듈을 사용하세요.\n\n" +
            "현재 Legacy 런타임 지원 범위:\n" +
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
        {
            operations.MoveArrayElement(moveFrom, moveTo);
            ClearEditingFocus();
            GUI.changed = true;
        }
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

    private static void AddTriggerBlock(SerializedProperty blocks)
    {
        if (blocks == null)
            return;

        int index = blocks.arraySize;
        blocks.InsertArrayElementAtIndex(index);
        SerializedProperty block = blocks.GetArrayElementAtIndex(index);
        block.isExpanded = true;
        block.FindPropertyRelative("trigger").enumValueIndex =
            (int)StatusEffectLifecycleTrigger.OnApply;
        block.FindPropertyRelative("scaleWithCurrentStacks").boolValue =
            false;
        block.FindPropertyRelative("scaleWithOccurrences").boolValue =
            true;
        SerializedProperty effects = block.FindPropertyRelative("effects");
        effects.ClearArray();
        BattleAbilityEditorGUI.AddDefaultEffect(effects);
    }

    private static void AddStatModifier(SerializedProperty modifiers)
    {
        if (modifiers == null)
            return;

        int index = modifiers.arraySize;
        modifiers.InsertArrayElementAtIndex(index);
        SerializedProperty modifier =
            modifiers.GetArrayElementAtIndex(index);
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)StatusEffectStatType.AttackPower;
        modifier.FindPropertyRelative("mode").enumValueIndex =
            (int)StatusEffectStatModifierMode.Flat;
        modifier.FindPropertyRelative("value").floatValue = 0f;
        modifier.FindPropertyRelative("scaleWithStacks").boolValue = true;
    }

    private static void AddControlEffect(SerializedProperty controls)
    {
        if (controls == null)
            return;

        int index = controls.arraySize;
        controls.InsertArrayElementAtIndex(index);
        controls.GetArrayElementAtIndex(index)
            .FindPropertyRelative("controlType")
            .enumValueIndex =
            (int)StatusEffectControlType.DisableAllActions;
    }

    private static void DrawMoveButtons(
        int index,
        int count,
        ref int moveFrom,
        ref int moveTo)
    {
        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUILayout.Button(
                    new GUIContent("↑", "위로 이동"),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                moveFrom = index;
                moveTo = index - 1;
            }
        }
        using (new EditorGUI.DisabledScope(index >= count - 1))
        {
            if (GUILayout.Button(
                    new GUIContent("↓", "아래로 이동"),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                moveFrom = index;
                moveTo = index + 1;
            }
        }
    }

    private static void ApplyListAction(
        SerializedProperty list,
        int removeIndex,
        int moveFrom,
        int moveTo)
    {
        if (list == null)
            return;
        if (removeIndex >= 0)
        {
            list.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }
        else if (moveFrom >= 0)
        {
            list.MoveArrayElement(moveFrom, moveTo);
            ClearEditingFocus();
            GUI.changed = true;
        }
    }

    private static void ClearEditingFocus()
    {
        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
    }

    private static string GetOptionName(string[] options, int index)
    {
        return options != null && options.Length > 0
            ? options[Mathf.Clamp(index, 0, options.Length - 1)]
            : string.Empty;
    }

    private void DrawLocalizationKey(
        string propertyName,
        string label)
    {
        SerializedProperty property = Find(propertyName);
        PS260714LocalizationKeyField.Draw(property, label);
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
            SelectDefinition(definition);
        Repaint();
    }

    private void SelectDefinition(StatusEffectSO definition)
    {
        if (definition == null)
            return;

        if (!ReferenceEquals(_selected, definition))
            CancelRename();
        _selected = definition;
        _serialized = new SerializedObject(definition);
        _editorScroll = Vector2.zero;
    }

    private void RefreshList()
    {
        string selectedPath =
            PS260714EditorAssetUtility.CapturePath(_selected);
        PS260714EditorAssetUtility.LoadAssets(
            _definitions,
            "t:StatusEffectSO");
        StatusEffectDefinitionCatalog.Invalidate();
        SelectDefinition(PS260714EditorAssetUtility.RestoreSelection(
            selectedPath,
            _definitions));
    }

    private void RefreshLocalizationKeys()
    {
        PS260714LocalizationKeyField.Refresh();
    }

    private void CreateDefinition()
    {
        EnsureFolder(AssetFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Status Effect",
            "NewStatusEffect",
            "asset",
            "상태 효과 SO를 생성할 위치를 선택하세요.",
            AssetFolder);
        if (string.IsNullOrEmpty(path))
            return;

        StatusEffectSO definition = CreateInstance<StatusEffectSO>();
        definition.name = Path.GetFileNameWithoutExtension(path);
        definition.RegenerateStatusId();
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssetIfDirty(definition);
        RefreshList();
        SelectDefinition(definition);
        EditorGUIUtility.PingObject(definition);
    }

    private void SaveSelected()
    {
        if (_selected == null)
            return;

        _serialized?.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssetIfDirty(_selected);
        StatusEffectDefinitionCatalog.Invalidate();
        ShowNotification(new GUIContent($"Saved {_selected.name}.asset"));
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        if (!PS260714EditorAssetUtility.TryDuplicate(
                _selected,
                null,
                " Copy",
                out StatusEffectSO duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate StatusEffectSO",
                duplicateError,
                "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        if (duplicate != null)
        {
            duplicate.RegenerateStatusId();
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }
        RefreshList();
        if (duplicate != null)
            SelectDefinition(duplicate);
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        string assetName = _selected.name;
        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                _selected,
                "StatusEffectSO"))
            return;

        _selected = null;
        _serialized = null;
        CancelRename();
        RefreshList();
        ShowNotification(new GUIContent(
            $"Moved {assetName}.asset to Trash"));
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

        if (!PS260714EditorAssetUtility.TryRename(
                _selected,
                _renameAssetName,
                out string renameError))
        {
            EditorUtility.DisplayDialog(
                "Rename StatusEffectSO",
                renameError,
                "OK");
            _focusRenameField = true;
            return;
        }

        CancelRename();
        RefreshList();
        EditorGUIUtility.PingObject(_selected);
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
