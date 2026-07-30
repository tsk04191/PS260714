using System;
using System.Collections.Generic;
using PS260714.Localization.Editor;
using UnityEditor;
using UnityEngine;

public sealed class CharacterEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.CharacterEditor;

    private const string CharacterFolder = "Assets/Resources/Characters";
    private const string CharacterLocalizationPrefix = "character.";
    private const string PassiveDefinitionsPropertyName = "passiveDefinitions";
    private const string PassiveSectionsPropertyName = "sections";
    private const string PassiveTriggerPropertyName = "trigger";
    private const string PassiveKillSourcePropertyName = "killSource";
    private const string PassiveSpecifiedKillerPropertyName =
        "specifiedKillerCharacter";
    private const string PassiveStatusTargetPropertyName = "statusTarget";
    private const string PassiveTriggerStatusEffectPropertyName =
        "triggerStatusEffect";
    private const string PassiveTriggerStatusEffectsPropertyName =
        "triggerStatusEffects";
    private const string PassiveTriggerStatusScopePropertyName =
        "triggerStatusScope";
    private const string PassiveCooldownPropertyName = "cooldown";
    private const string PassiveAttackTargetRelationPropertyName =
        "attackTargetRelation";
    private const string PassiveSelfStatusCostPropertyName = "selfStatusCost";
    private const string StatusCostEffectPropertyName = "statusEffect";
    private const string StatusCostRequiredStacksPropertyName =
        "requiredStacks";
    private const string StatusCostConsumedStacksPropertyName =
        "consumedStacks";
    private const string SkillDefinitionsPropertyName = "skillDefinitions";
    private const string SkillExecutionPolicyPropertyName =
        "skillExecutionPolicy";
    private const string SkillSectionsPropertyName = "sections";
    private const string SkillCostPropertyName = "cost";
    private const string CumulativeUpgradeDefinitionsPropertyName =
        "cumulativeUpgradeDefinitions";
    private const string CumulativeUpgradeIdPropertyName = "upgradeId";
    private const string CumulativeUpgradeMaxLevelPropertyName = "maxLevel";
    private const string CumulativeUpgradeModifiersPropertyName = "modifiers";
    private const string CumulativeUpgradeModifierTypePropertyName = "type";
    private const string CumulativeUpgradeModifierValuePropertyName =
        "valuePerLevel";
    private const string DungeonUpgradeDefinitionsPropertyName =
        "dungeonUpgradeDefinitions";
    private const string DungeonUpgradeEntriesPropertyName = "entries";
    private const string DungeonUpgradeTypePropertyName = "type";
    private const string DungeonUpgradeProbabilityPropertyName = "probability";
    private const string DungeonUpgradeLimitPropertyName = "limit";
    private const string AttackDefinitionsPropertyName = "attackDefinitions";
    private const string AttackSectionsPropertyName = "sections";
    private const string ActionLinkagePropertyName = "linkage";
    private const string ConditionMatchModePropertyName = "conditionMatchMode";
    private const string NumericConditionsPropertyName = "numericConditions";
    private const string ConditionTypePropertyName = "type";
    private const string ConditionTargetPropertyName = "target";
    private const string NumericConditionMetricPropertyName = "metric";
    private const string NumericComparisonPropertyName = "comparison";
    private const string NumericThresholdPropertyName = "threshold";
    private const string ConditionStatusEffectsPropertyName =
        "statusEffects";
    private const string StatusSelectionScopePropertyName =
        "statusSelectionScope";
    private const string StatusConditionMatchModePropertyName =
        "statusMatchMode";
    private const string StatusConditionMatchCountPropertyName =
        "statusMatchCount";
    private const string TargetFactionPropertyName = "targetFaction";
    private const string AttackSubjectPropertyName = "subject";
    private const string AttackTargetRetentionModePropertyName =
        "targetRetentionMode";
    private const string AttackSubjectCountPropertyName = "subjectCount";
    private const string AttackSubjectMetricPropertyName = "subjectMetric";
    private const string EffectsPropertyName = "effects";
    private const string EffectTypePropertyName = "type";
    private const string EffectTargetModePropertyName = "targetMode";
    private const string EffectPreconditionFailurePolicyPropertyName =
        "preconditionFailurePolicy";
    private const string EffectFailurePolicyPropertyName = "failurePolicy";
    private const string EffectTargetSelectorPropertyName = "targetSelector";
    private const string AttackDamageTypePropertyName = "damageType";
    private const string DamageAmountModePropertyName = "damageAmountMode";
    private const string DamageAmountPropertyName = "damageAmount";
    private const string SourceResourceScalePropertyName =
        "sourceResourceScale";
    private const string TargetCurrentHealthScalePropertyName =
        "targetCurrentHealthScale";
    private const string TargetMaxHealthScalePropertyName =
        "targetMaxHealthScale";
    private const string SourceStatusScalingEffectPropertyName =
        "sourceStatusScalingEffect";
    private const string SourceStatusStacksScalePropertyName =
        "sourceStatusStacksScale";
    private const string TargetStatusScalingEffectPropertyName =
        "targetStatusScalingEffect";
    private const string TargetStatusStacksScalePropertyName =
        "targetStatusStacksScale";
    private const string StatusContributionMultipliersPropertyName =
        "statusContributionMultipliers";
    private const string StatusContributionStatusPropertyName =
        "statusEffect";
    private const string StatusContributionStatTypePropertyName =
        "statType";
    private const string StatusContributionMultiplierPropertyName =
        "multiplier";
    private const string StatusDurationPropertyName = "statusDuration";
    private const string StatusStacksPropertyName = "statusStacks";
    private const string StatusEffectPropertyName = "statusEffect";
    private const string StatusRemovalEffectPropertyName =
        "statusRemovalEffect";
    private const string StatusRemovalEffectsPropertyName =
        "statusRemovalEffects";
    private const string StatusRemovalTargetPropertyName =
        "statusRemovalTarget";
    private const string StatusRemovalPickModePropertyName =
        "statusRemovalPickMode";
    private const string StatusRemovalPickCountPropertyName =
        "statusRemovalPickCount";
    private const string StatusRemovalAmountModePropertyName =
        "statusRemovalAmountMode";
    private const string StatusRemovalCountPropertyName =
        "statusRemovalCount";
    private const string StatusRemovalRatioPropertyName =
        "statusRemovalRatio";
    private const string CastVfxCuePropertyName = "castVfxCue";
    private const string ProjectileVfxCuePropertyName =
        "projectileVfxCue";
    private const string ImpactVfxCuePropertyName = "impactVfxCue";
    private const string AreaOffsetsPropertyName = "areaOffsets";
    private const string AreaRowOffsetPropertyName = "rowOffset";
    private const string AreaColumnOffsetPropertyName = "columnOffset";
    private const string ActionIconSpritePropertyName = "iconSprite";
    private const string ActionAudioClipPropertyName = "audioClip";
    private const string RenameControlName = "CharacterAssetRenameField";
    private static readonly CharacterPassiveSectionType[] PassiveSectionOrder =
    {
        CharacterPassiveSectionType.Linkage,
        CharacterPassiveSectionType.Condition,
        CharacterPassiveSectionType.SelfStatusCost,
        CharacterPassiveSectionType.StatusContribution,
        CharacterPassiveSectionType.Subject,
        CharacterPassiveSectionType.Ability
    };

    private static readonly string[] PassiveTriggerOptions =
    {
        "공격 시",
        "상태 획득 시",
        "쿨다운마다",
        "킬 마다",
        "공격 대상 선택 시"
    };

    private static readonly string[] PassiveKillSourceOptions =
    {
        "자신",
        "자신 외",
        "지정된 캐릭터",
        "전체"
    };

    private static readonly string[] PassiveStatusTargetOptions =
    {
        "적",
        "아군",
        "전체"
    };

    private static readonly string[] PassiveAttackTargetRelationOptions =
    {
        "제한 없음",
        "직전 공격과 동일",
        "직전 공격과 다름"
    };

    private static readonly string[] StatusContributionStatTypeOptions =
    {
        "공격력",
        "공격 속도",
        "받는 피해",
        "대상 우선순위 (미지원)"
    };

    private static readonly CharacterAttackSectionType[] AttackSectionOrder =
    {
        CharacterAttackSectionType.Linkage,
        CharacterAttackSectionType.Condition,
        CharacterAttackSectionType.Subject,
        CharacterAttackSectionType.Ability
    };

    private static readonly CharacterSkillSectionType[] SkillSectionOrder =
    {
        CharacterSkillSectionType.Cost,
        CharacterSkillSectionType.Linkage,
        CharacterSkillSectionType.Condition,
        CharacterSkillSectionType.Subject,
        CharacterSkillSectionType.Ability
    };

    private static readonly CharacterDungeonUpgradeType[] DungeonUpgradeOrder =
    {
        CharacterDungeonUpgradeType.AttackPower,
        CharacterDungeonUpgradeType.Speed,
        CharacterDungeonUpgradeType.PassiveDamage,
        CharacterDungeonUpgradeType.AttackDamage,
        CharacterDungeonUpgradeType.SkillDamage,
        CharacterDungeonUpgradeType.SkillCostReduction
    };

    private static readonly float[] DefaultDungeonUpgradeProbabilities =
    {
        16.6667f,
        16.6667f,
        16.6667f,
        16.6667f,
        16.6667f,
        16.6665f
    };

    private static readonly string[] CumulativeUpgradeModifierOptions =
    {
        "공격력",
        "최대 체력",
        "공격 쿨다운",
        "패시브 피해량",
        "일반 공격 피해량",
        "스킬 피해량",
        "스킬 비용 감소"
    };

    private static readonly string[] ActionLinkageOptions =
    {
        "앞선 공격이 성공할 경우",
        "앞선 공격과 동시에",
        "없음",
        "앞선 공격이 성공하지 못한 경우"
    };

    private static readonly string[] AttackTargetRetentionModeOptions =
    {
        "매 공격마다 다시 선정",
        "대상이 유효한 동안 고정"
    };

    private static readonly string[] ConditionMatchModeOptions =
    {
        "모든 조건 만족 (AND)",
        "하나 이상 만족 (OR)"
    };

    private static readonly string[] StatusConditionMatchModeOptions =
    {
        "하나 이상",
        "모두",
        "N개 이상"
    };

    private static readonly string[] StatusSelectionScopeOptions =
    {
        "선택한 상태",
        "보유한 모든 버프",
        "보유한 모든 디버프"
    };

    private static readonly string[] ConditionTargetOptions =
    {
        "행동 대상",
        "시전자 (자신)"
    };

    private static readonly string[] NumericConditionMetricOptions =
    {
        "체력",
        "체력 비율 (%)",
        "적 타일 스택",
        "보호막",
        "상태 스택"
    };

    private static readonly int[] EnemyNumericConditionMetricValues =
    {
        (int)CharacterNumericConditionMetric.Health,
        (int)CharacterNumericConditionMetric.HealthPercentage,
        (int)CharacterNumericConditionMetric.StackCount,
        (int)CharacterNumericConditionMetric.Shield,
        (int)CharacterNumericConditionMetric.StatusStackCount
    };

    private static readonly string[] AllyNumericConditionMetricOptions =
    {
        "체력",
        "체력 비율 (%)",
        "공격력",
        "속도",
        "보호막",
        "상태 스택"
    };

    private static readonly int[] AllyNumericConditionMetricValues =
    {
        (int)CharacterNumericConditionMetric.Health,
        (int)CharacterNumericConditionMetric.HealthPercentage,
        (int)CharacterNumericConditionMetric.AttackPower,
        (int)CharacterNumericConditionMetric.AttackSpeed,
        (int)CharacterNumericConditionMetric.Shield,
        (int)CharacterNumericConditionMetric.StatusStackCount
    };

    private static readonly string[] NumericComparisonOptions =
    {
        "이상 (≥)",
        "이하 (≤)",
        "초과 (>)",
        "미만 (<)",
        "같음 (=)",
        "다름 (≠)"
    };

    private static readonly string[] AttackSubjectOptions =
    {
        "없음 - 앞선 공격 대상",
        "수동 선택",
        "랜덤",
        "전체",
        "가장 많은 수치",
        "가장 적은 수치"
    };

    private static readonly int[] AttackSubjectValues =
    {
        (int)CharacterAttackSubject.None,
        (int)CharacterAttackSubject.Manual,
        (int)CharacterAttackSubject.Random,
        (int)CharacterAttackSubject.All,
        (int)CharacterAttackSubject.HighestValue,
        (int)CharacterAttackSubject.LowestValue
    };

    private static readonly string[] AllyAttackSubjectOptions =
    {
        "없음 - 앞선 공격 대상",
        "수동 선택",
        "자신",
        "랜덤 - 자신 포함",
        "랜덤 - 자신 제외",
        "전체 - 자신 제외",
        "전체 - 자신 포함",
        "가장 많은 수치",
        "가장 적은 수치"
    };

    private static readonly int[] AllyAttackSubjectValues =
    {
        (int)CharacterAttackSubject.None,
        (int)CharacterAttackSubject.Manual,
        (int)CharacterAttackSubject.Self,
        (int)CharacterAttackSubject.Random,
        (int)CharacterAttackSubject.RandomExceptSelf,
        (int)CharacterAttackSubject.AllExceptSelf,
        (int)CharacterAttackSubject.All,
        (int)CharacterAttackSubject.HighestValue,
        (int)CharacterAttackSubject.LowestValue
    };

    private static readonly string[] TargetFactionOptions =
    {
        "적",
        "아군"
    };

    private static readonly string[] AttackSubjectMetricOptions =
    {
        "체력",
        "스택",
        "보호막"
    };

    private static readonly int[] EnemySubjectMetricValues =
    {
        (int)CharacterAttackSubjectMetric.Health,
        (int)CharacterAttackSubjectMetric.StackCount,
        (int)CharacterAttackSubjectMetric.Shield
    };

    private static readonly string[] AllySubjectMetricOptions =
    {
        "체력",
        "공격력",
        "속도",
        "보호막"
    };

    private static readonly int[] AllySubjectMetricValues =
    {
        (int)CharacterAttackSubjectMetric.Health,
        (int)CharacterAttackSubjectMetric.AttackPower,
        (int)CharacterAttackSubjectMetric.AttackSpeed,
        (int)CharacterAttackSubjectMetric.Shield
    };

    private static readonly string[] AttackDamageTypeOptions =
    {
        "물리",
        "마법",
        "고정",
        "상태 부여",
        "상태 제거"
    };

    private static readonly string[] EffectTypeOptions =
    {
        "피해",
        "상태 부여",
        "상태 제거",
        "자원 획득",
        "자원 소비",
        "체력 회복",
        "체력 소비",
        "보호막 부여"
    };

    private static readonly string[] EffectTargetModeOptions =
    {
        "행동 대상",
        "시전자 자신",
        "별도 새 대상"
    };

    private static readonly string[] EffectPreconditionFailurePolicyOptions =
    {
        "액션 중단",
        "해당 효과 건너뜀"
    };

    private static readonly string[] EffectFailurePolicyOptions =
    {
        "후속 효과 계속",
        "후속 효과 중단"
    };

    private static readonly string[] FreshEnemySubjectOptions =
    {
        "랜덤",
        "전체",
        "가장 많은 수치",
        "가장 적은 수치"
    };

    private static readonly int[] FreshEnemySubjectValues =
    {
        (int)CharacterAttackSubject.Random,
        (int)CharacterAttackSubject.All,
        (int)CharacterAttackSubject.HighestValue,
        (int)CharacterAttackSubject.LowestValue
    };

    private static readonly string[] FreshAllySubjectOptions =
    {
        "자신",
        "랜덤 - 자신 포함",
        "랜덤 - 자신 제외",
        "전체 - 자신 제외",
        "전체 - 자신 포함",
        "가장 많은 수치",
        "가장 적은 수치"
    };

    private static readonly int[] FreshAllySubjectValues =
    {
        (int)CharacterAttackSubject.Self,
        (int)CharacterAttackSubject.Random,
        (int)CharacterAttackSubject.RandomExceptSelf,
        (int)CharacterAttackSubject.AllExceptSelf,
        (int)CharacterAttackSubject.All,
        (int)CharacterAttackSubject.HighestValue,
        (int)CharacterAttackSubject.LowestValue
    };

    private static readonly string[] DirectDamageTypeOptions =
    {
        "물리",
        "마법",
        "고정"
    };

    private static readonly string[] DamageAmountModeOptions =
    {
        "비율",
        "고정"
    };

    private static readonly string[] StatusRemovalTargetOptions =
    {
        "지정 상태",
        "랜덤 상태",
        "모든 버프",
        "모든 디버프",
        "모든 상태"
    };

    private static readonly int[] StatusRemovalTargetValues =
    {
        (int)CharacterStatusRemovalTarget.Single,
        (int)CharacterStatusRemovalTarget.Random,
        (int)CharacterStatusRemovalTarget.Buff,
        (int)CharacterStatusRemovalTarget.Debuff,
        (int)CharacterStatusRemovalTarget.All
    };

    private static readonly string[] StatusRemovalPickModeOptions =
    {
        "조건에 맞는 상태 모두",
        "조건에 맞는 상태 중 N개"
    };

    private static readonly string[] StatusRemovalAmountModeOptions =
    {
        "고정 스택",
        "현재 스택 비율"
    };

    private readonly List<CharacterSO> _characters = new();
    private readonly List<LocalizationKeyOption> _nameKeyOptions = new();
    private readonly List<LocalizationKeyOption> _descriptionKeyOptions = new();

    private CharacterSO _selectedCharacter;
    private SerializedObject _serializedCharacter;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private Vector2 _sdSpriteScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenamingSelectedCharacter;
    private bool _focusRenameField;
    private bool _clearEditingFocusRequested;
    private bool _passiveExpanded;
    private bool _attackExpanded;
    private bool _skillExpanded;
    private bool _validationExpanded = true;
    private bool _cumulativeUpgradeExpanded;
    private bool _dungeonUpgradeExpanded;
    private string _localizationLoadError = string.Empty;

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
        CharacterEditorWindow window = GetWindow<CharacterEditorWindow>();
        window.titleContent = new GUIContent("Character Editor");
        window.minSize = new Vector2(780f, 560f);
        window.Show();
        window.Focus();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Character Editor");
        minSize = new Vector2(780f, 560f);
        RefreshLocalizationKeys();
        RefreshCharacterList();

        if (Selection.activeObject is CharacterSO selected)
            SelectCharacter(selected);
        else if (_selectedCharacter == null && _characters.Count > 0)
            SelectCharacter(_characters[0]);
    }

    private void OnProjectChange()
    {
        StatusEffectDefinitionCatalog.Invalidate();
        RefreshLocalizationKeys();
        RefreshCharacterList();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is not CharacterSO selected)
            return;

        SelectCharacter(selected);
        Repaint();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        ApplyPendingEditingFocusClear();
        DrawTopToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawCharacterList();
        DrawSeparator();
        DrawCharacterSettings();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Characters: {_characters.Count}",
            _selectedCharacter != null,
            () =>
            {
                CreateCharacter();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                SaveSelectedCharacter();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                DuplicateSelectedCharacter();
                GUIUtility.ExitGUI();
            },
            BeginRenameSelectedCharacter,
            () =>
            {
                DeleteSelectedCharacter();
                GUIUtility.ExitGUI();
            },
            () => PS260714AssetEditorList.Ping(_selectedCharacter),
            () =>
            {
                RefreshLocalizationKeys();
                RefreshCharacterList();
            });

        if (_isRenamingSelectedCharacter)
            DrawRenameSelectedCharacter();
    }

    private void DrawRenameSelectedCharacter()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("SO File Name", GUILayout.Width(88f));

        GUI.SetNextControlName(RenameControlName);
        _renameAssetName = EditorGUILayout.TextField(_renameAssetName);
        if (_focusRenameField)
        {
            EditorGUI.FocusTextInControl(RenameControlName);
            _focusRenameField = false;
        }

        bool apply = GUILayout.Button("Apply", GUILayout.Width(56f));
        bool cancel = GUILayout.Button("Cancel", GUILayout.Width(56f));

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown &&
            GUI.GetNameOfFocusedControl() == RenameControlName)
        {
            if (currentEvent.keyCode == KeyCode.Return ||
                currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                apply = true;
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                cancel = true;
                currentEvent.Use();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (cancel)
        {
            CancelRenameSelectedCharacter();
            GUIUtility.ExitGUI();
        }

        if (apply)
        {
            RenameSelectedCharacter();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawCharacterList()
    {
        EditorGUILayout.BeginVertical(
            GUILayout.Width(PS260714AssetEditorList.Width),
            GUILayout.ExpandHeight(true));
        _searchText =
            PS260714AssetEditorList.DrawSearchField(_searchText);

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        bool hasVisibleCharacter = false;
        foreach (CharacterSO character in _characters)
        {
            if (character == null || !MatchesSearch(character))
                continue;

            hasVisibleCharacter = true;
            DrawCharacterListRow(character);
        }

        if (!hasVisibleCharacter)
        {
            EditorGUILayout.HelpBox(
                string.IsNullOrWhiteSpace(_searchText)
                    ? "No CharacterSO assets were found."
                    : "No characters match the search.",
                MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCharacterListRow(CharacterSO character)
    {
        bool selected = character == _selectedCharacter;
        string label = character.name + "\n" +
                       $"G{(int)character.Grade} / " +
                       $"A{character.AttackDefinitions.Count} / " +
                       $"P{character.PassiveDefinitions.Count} / " +
                       $"S{character.SkillDefinitions.Count}";
        bool clicked = PS260714AssetEditorList.DrawRow(
            selected,
            new GUIContent(
                label,
                PS260714AssetEditorList.GetAssetPreview(
                    character.IconSprite),
                character.CharacterId));
        if (clicked)
            SelectCharacter(character);
    }

    private static void DrawSeparator()
    {
        Rect separator = GUILayoutUtility.GetRect(
            1f,
            1f,
            GUILayout.Width(1f),
            GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(separator, new Color(0f, 0f, 0f, 0.35f));
    }

    private void DrawCharacterSettings()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (_selectedCharacter == null)
        {
            EditorGUILayout.HelpBox(
                "Select a character or create a new CharacterSO asset.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawSelectedCharacterHeader();
        _serializedCharacter.UpdateIfRequiredOrScript();
        _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);
        EditorGUILayout.Space(6f);
        DrawValidationDiagnostics();

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            DrawCharacterProfile();
            DrawCharacterPresentation();
            DrawCharacterReferenceStats();
            if (EditorGUI.EndChangeCheck() &&
                _serializedCharacter.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedCharacter);
            }
        }

        DrawPassiveSettingsFoldout();
        DrawAttackSettingsFoldout();
        DrawSkillSettingsFoldout();
        DrawCumulativeUpgradeSettingsFoldout();
        DrawDungeonUpgradeSettingsFoldout();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedCharacterHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(
            _selectedCharacter.name,
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            AssetDatabase.GetAssetPath(_selectedCharacter),
            EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawValidationDiagnostics()
    {
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(
                _selectedCharacter,
                _characters);
        string summary = validation.Diagnostics.Count == 0
            ? "Validation - Passed"
            : $"Validation - {validation.ErrorCount} Error(s), " +
              $"{validation.WarningCount} Warning(s)";

        _validationExpanded = EditorGUILayout.Foldout(
            _validationExpanded,
            summary,
            true,
            EditorStyles.foldoutHeader);
        if (!_validationExpanded)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (validation.Diagnostics.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Character definition validation passed.",
                MessageType.Info);
        }
        else
        {
            foreach (CharacterDefinitionDiagnostic diagnostic in
                     validation.Diagnostics)
            {
                string path = string.IsNullOrWhiteSpace(diagnostic.Path)
                    ? "<root>"
                    : diagnostic.Path;
                MessageType messageType =
                    diagnostic.Severity ==
                    CharacterDefinitionDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning;
                EditorGUILayout.HelpBox(
                    $"[{diagnostic.Code}] {path}\n{diagnostic.Message}",
                    messageType);
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawCharacterProfile()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("캐릭터 기본 정보", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        DrawSpriteProfile(
            "스탠딩 스프라이트",
            "standingSprite",
            new Vector2Int(1024, 2048),
            new Vector2(110f, 220f));
        EditorGUILayout.Space(8f);
        DrawSpriteProfile(
            "Icon 스프라이트",
            "iconSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.Space(12f);

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawLocalizationKeyPopup(
            "nameLocalizationKey",
            "이름 Localization 키",
            _nameKeyOptions,
            ".name");
        EditorGUILayout.Space(4f);
        DrawLocalizationKeyPopup(
            "descriptionLocalizationKey",
            "설명 Localization 키",
            _descriptionKeyOptions,
            ".description");
        if (!string.IsNullOrEmpty(_localizationLoadError))
        {
            EditorGUILayout.HelpBox(
                _localizationLoadError,
                MessageType.Error);
        }
        EditorGUILayout.Space(8f);
        DrawProfileProperty("characterName", "이름");
        EditorGUILayout.Space(4f);
        DrawCharacterGradeProperty();
        EditorGUILayout.Space(4f);
        DrawProfileProperty("initiallyOwned", "기본 보유");
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("설명");
        SerializedProperty description =
            _serializedCharacter.FindProperty("characterDescription");
        if (description != null)
        {
            description.stringValue = EditorGUILayout.TextArea(
                description.stringValue,
                GUILayout.MinHeight(120f),
                GUILayout.ExpandWidth(true));
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Character description property was not found.",
                MessageType.Error);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "SD 스프라이트",
            EditorStyles.boldLabel);
        _sdSpriteScroll = EditorGUILayout.BeginScrollView(
            _sdSpriteScroll,
            true,
            false,
            GUILayout.Height(260f));
        EditorGUILayout.BeginHorizontal();
        DrawSpriteProfile(
            "대기 SD",
            "waitingSdSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.Space(8f);
        DrawSpriteProfile(
            "공격 SD",
            "attackSdSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.Space(8f);
        DrawSpriteProfile(
            "피격 SD",
            "damagedSdSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.Space(8f);
        DrawSpriteProfile(
            "기술 SD",
            "skillSdSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.Space(8f);
        DrawSpriteProfile(
            "패시브 SD",
            "passiveSdSprite",
            new Vector2Int(1024, 1024),
            new Vector2(150f, 150f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8f);
    }

    private void DrawSpriteProfile(
        string label,
        string propertyName,
        Vector2Int expectedPixelSize,
        Vector2 previewSize)
    {
        float expectedAspect =
            expectedPixelSize.x / (float)expectedPixelSize.y;
        EditorGUILayout.BeginVertical(GUILayout.Width(previewSize.x));
        EditorGUILayout.LabelField(
            label,
            EditorStyles.boldLabel,
            GUILayout.Width(previewSize.x));
        EditorGUILayout.LabelField(
            $"{expectedPixelSize.x} × {expectedPixelSize.y} · " +
            FormatAspect(expectedAspect),
            EditorStyles.miniLabel,
            GUILayout.Width(previewSize.x));

        SerializedProperty property =
            _serializedCharacter.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"Property '{propertyName}' was not found.",
                MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }

        property.objectReferenceValue = EditorGUILayout.ObjectField(
            property.objectReferenceValue,
            typeof(Sprite),
            false,
            GUILayout.Width(previewSize.x)) as Sprite;

        Sprite sprite = property.objectReferenceValue as Sprite;
        Rect previewRect = GUILayoutUtility.GetRect(
            previewSize.x,
            previewSize.y,
            GUILayout.Width(previewSize.x),
            GUILayout.Height(previewSize.y));
        EditorGUI.DrawRect(previewRect, new Color(0.08f, 0.08f, 0.08f, 1f));
        if (sprite != null)
        {
            DrawSpritePreview(previewRect, sprite);

            float actualAspect = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 0f;
            int actualWidth = Mathf.RoundToInt(sprite.rect.width);
            int actualHeight = Mathf.RoundToInt(sprite.rect.height);
            if (actualWidth != expectedPixelSize.x ||
                actualHeight != expectedPixelSize.y)
            {
                EditorGUILayout.HelpBox(
                    $"현재 {actualWidth} × {actualHeight} " +
                    $"({actualAspect:0.##}:1) / 권장 " +
                    $"{expectedPixelSize.x} × {expectedPixelSize.y} " +
                    $"({FormatAspect(expectedAspect)})",
                    MessageType.Warning);
            }
        }
        else
        {
            GUI.Label(
                previewRect,
                "Sprite 없음",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                });
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawSpritePreview(Rect previewRect, Sprite sprite)
    {
        if (TryGetSpriteTextureCoordinates(sprite, out Texture2D texture,
                out Rect textureCoordinates))
        {
            float spriteAspect = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 1f;
            Rect contentRect = CalculateAspectFitRect(
                new Rect(
                    previewRect.x + 1f,
                    previewRect.y + 1f,
                    Mathf.Max(0f, previewRect.width - 2f),
                    Mathf.Max(0f, previewRect.height - 2f)),
                spriteAspect);
            GUI.DrawTextureWithTexCoords(
                contentRect,
                texture,
                textureCoordinates,
                true);
            return;
        }

        Texture2D fallback = AssetPreview.GetAssetPreview(sprite) ??
                             AssetPreview.GetMiniThumbnail(sprite);
        if (fallback != null)
            GUI.DrawTexture(previewRect, fallback, ScaleMode.ScaleToFit, true);
    }

    internal static bool TryGetSpriteTextureCoordinates(
        Sprite sprite,
        out Texture2D texture,
        out Rect textureCoordinates)
    {
        texture = sprite != null ? sprite.texture : null;
        textureCoordinates = default;
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return false;

        // textureRect may exclude transparent margins for Tight mesh sprites.
        // Character artwork relies on its full source canvas for composition.
        if (sprite.packed)
            return false;

        Rect sourceRect = sprite.rect;
        textureCoordinates = new Rect(
            sourceRect.x / texture.width,
            sourceRect.y / texture.height,
            sourceRect.width / texture.width,
            sourceRect.height / texture.height);
        return textureCoordinates.width > 0f &&
               textureCoordinates.height > 0f;
    }

    internal static Rect CalculateAspectFitRect(
        Rect bounds,
        float contentAspect)
    {
        if (bounds.width <= 0f ||
            bounds.height <= 0f ||
            contentAspect <= 0f ||
            float.IsNaN(contentAspect) ||
            float.IsInfinity(contentAspect))
        {
            return bounds;
        }

        float boundsAspect = bounds.width / bounds.height;
        if (contentAspect > boundsAspect)
        {
            float height = bounds.width / contentAspect;
            return new Rect(
                bounds.x,
                bounds.y + (bounds.height - height) * 0.5f,
                bounds.width,
                height);
        }

        float width = bounds.height * contentAspect;
        return new Rect(
            bounds.x + (bounds.width - width) * 0.5f,
            bounds.y,
            width,
            bounds.height);
    }

    private void DrawProfileProperty(string propertyName, string label)
    {
        SerializedProperty property =
            _serializedCharacter.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            return;
        }

        EditorGUILayout.HelpBox(
            $"Property '{propertyName}' was not found.",
            MessageType.Error);
    }

    private void DrawCharacterGradeProperty()
    {
        SerializedProperty gradeProperty =
            _serializedCharacter.FindProperty("grade");
        if (gradeProperty == null)
        {
            EditorGUILayout.HelpBox(
                "Property 'grade' was not found.",
                MessageType.Error);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(
                gradeProperty,
                new GUIContent("캐릭터 등급 (0~3)"));
            CharacterGradePaletteSO palette =
                CharacterGradePresentation.Palette;
            using (new EditorGUI.DisabledScope(palette == null))
            {
                if (GUILayout.Button("공통 팔레트", GUILayout.Width(92f)))
                {
                    Selection.activeObject = palette;
                    EditorGUIUtility.PingObject(palette);
                }
            }
        }

        CharacterGrade grade = CharacterGradePresentation.Clamp(
            (CharacterGrade)gradeProperty.enumValueIndex);
        CharacterGradeStyle style =
            CharacterGradePresentation.GetStyle(grade);
        Rect preview = EditorGUILayout.GetControlRect(false, 24f);
        EditorGUI.DrawRect(preview, style.BackgroundColor);
        EditorGUI.DrawRect(
            new Rect(preview.x, preview.y, 8f, preview.height),
            style.PrimaryColor);
        Handles.DrawSolidRectangleWithOutline(
            preview,
            Color.clear,
            style.OutlineColor);

        GUIStyle labelStyle = new(EditorStyles.boldLabel);
        labelStyle.normal.textColor = style.TextColor;
        EditorGUI.LabelField(
            new Rect(
                preview.x + 16f,
                preview.y,
                preview.width - 20f,
                preview.height),
            $"공통 등급 색상 · " +
            $"{CharacterGradePresentation.GetLabel(grade)}",
            labelStyle);
    }

    private void DrawCharacterReferenceStats()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "캐릭터 스탯 (참고)",
            EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        SerializedProperty maximumHealth =
            _serializedCharacter.FindProperty("maximumHealth");
        SerializedProperty attackPower =
            _serializedCharacter.FindProperty("attackPower");
        SerializedProperty attackCooldown =
            _serializedCharacter.FindProperty("attackCooldown");

        if (maximumHealth != null)
        {
            EditorGUILayout.PropertyField(
                maximumHealth,
                new GUIContent("최대 체력"));
        }

        if (attackPower != null)
        {
            EditorGUILayout.PropertyField(
                attackPower,
                new GUIContent("공격력"));
        }

        if (attackCooldown != null)
        {
            EditorGUILayout.PropertyField(
                attackCooldown,
                new GUIContent(
                    "속도",
                    "공격 간격(초)입니다. 값이 낮을수록 빠릅니다."));
        }

        if (maximumHealth == null ||
            attackPower == null || attackCooldown == null)
        {
            EditorGUILayout.HelpBox(
                "캐릭터 스탯 속성을 찾을 수 없습니다.",
                MessageType.Error);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8f);
    }

    private void DrawCharacterPresentation()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "전투 생명주기 3D VFX",
            EditorStyles.boldLabel);
        DrawProfileProperty("spawnVfxCue", "배치 VFX 큐");
        DrawProfileProperty("deathVfxCue", "사망 VFX 큐");
        EditorGUILayout.HelpBox(
            "배치 Cue는 파티가 전투 보드에 등록될 때, 사망 Cue는 체력이 0이 된 위치에서 재생됩니다.",
            MessageType.Info);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8f);
    }

    private void DrawLocalizationKeyPopup(
        string propertyName,
        string label,
        List<LocalizationKeyOption> options,
        string requiredSuffix)
    {
        SerializedProperty property =
            _serializedCharacter.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"Property '{propertyName}' was not found.",
                MessageType.Error);
            return;
        }

        string currentKey = (property.stringValue ?? string.Empty).Trim();
        int currentIndex = 0;
        string[] labels = new string[options.Count + 1];
        labels[0] = "(선택 없음)";
        for (int index = 0; index < options.Count; index++)
        {
            LocalizationKeyOption option = options[index];
            labels[index + 1] = option.Label;
            if (string.Equals(
                    option.Key,
                    currentKey,
                    StringComparison.Ordinal))
            {
                currentIndex = index + 1;
            }
        }

        int selectedIndex = EditorGUILayout.Popup(
            label,
            currentIndex,
            labels);
        if (selectedIndex != currentIndex)
        {
            property.stringValue = selectedIndex <= 0
                ? string.Empty
                : options[selectedIndex - 1].Key;
            currentKey = property.stringValue;
        }

        if (!string.IsNullOrEmpty(currentKey) && currentIndex == 0 &&
            selectedIndex == currentIndex)
        {
            EditorGUILayout.HelpBox(
                $"현재 키 '{currentKey}'는 " +
                $"{CharacterLocalizationPrefix}*{requiredSuffix} 필터에 " +
                "포함되지 않습니다.",
                MessageType.Warning);
        }
        else if (options.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"{CharacterLocalizationPrefix}*{requiredSuffix} 형식의 " +
                "Localization 키가 없습니다.",
                MessageType.Info);
        }
    }

    private static string FormatAspect(float aspect)
    {
        return Mathf.Approximately(aspect, 0.5f) ? "1:2" : "1:1";
    }

    private void DrawPassiveSettingsFoldout()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _passiveExpanded = EditorGUILayout.Foldout(
            _passiveExpanded,
            "1. 패시브",
            true,
            EditorStyles.foldoutHeader);

        bool addPassive;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            addPassive = GUILayout.Button(
                new GUIContent("+", "패시브 블록 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        EditorGUILayout.EndHorizontal();

        if (addPassive)
            AddPassiveDefinition();

        if (_passiveExpanded)
        {
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                _serializedCharacter.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                DrawPassiveDefinitions();
                if (EditorGUI.EndChangeCheck() &&
                    _serializedCharacter.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_selectedCharacter);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawPassiveDefinitions()
    {
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            PassiveDefinitionsPropertyName);
        if (definitions == null)
        {
            EditorGUILayout.HelpBox(
                "패시브 구조를 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        if (definitions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 패시브 블록을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        for (int passiveIndex = 0;
             passiveIndex < definitions.arraySize;
             passiveIndex++)
        {
            if (DrawPassiveDefinition(
                    definitions.GetArrayElementAtIndex(passiveIndex),
                    passiveIndex))
            {
                definitions.DeleteArrayElementAtIndex(passiveIndex);
                GUI.changed = true;
                break;
            }
        }
    }

    private bool DrawPassiveDefinition(
        SerializedProperty definition,
        int passiveIndex)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        definition.isExpanded = EditorGUILayout.Foldout(
            definition.isExpanded,
            $"패시브 {passiveIndex + 1}",
            true,
            EditorStyles.foldoutHeader);

        bool showSectionMenu = GUILayout.Button(
            new GUIContent("+", "패시브 구성 블록 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        bool removePassive = GUILayout.Button(
            new GUIContent("-", "패시브 블록 삭제"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        if (definition.isExpanded)
        {
            DrawActionIconSprite(definition);
            DrawActionAudioClip(definition);
            EditorGUILayout.Space(4f);
            SerializedProperty sections = definition.FindPropertyRelative(
                PassiveSectionsPropertyName);
            DrawPassiveSectionBlocks(definition, sections);
        }

        EditorGUILayout.EndVertical();

        if (showSectionMenu && !removePassive)
            ShowPassiveSectionMenu(passiveIndex);

        return removePassive;
    }

    private void DrawPassiveSectionBlocks(
        SerializedProperty definition,
        SerializedProperty sections)
    {
        if (sections == null || sections.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 구성 블록을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        CharacterPassiveSectionType? sectionToRemove = null;
        foreach (CharacterPassiveSectionType sectionType in PassiveSectionOrder)
        {
            int sectionIndex = FindPassiveSectionIndex(sections, sectionType);
            if (sectionIndex < 0)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                GetPassiveSectionLabel(sectionType),
                EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent("-", "세부 블록 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(20f)))
            {
                sectionToRemove = sectionType;
            }
            EditorGUILayout.EndHorizontal();
            DrawPassiveSectionValue(definition, sectionType);
            EditorGUILayout.EndVertical();

            if (sectionToRemove.HasValue)
                break;
        }

        if (sectionToRemove.HasValue)
        {
            CharacterPassiveSectionType sectionType = sectionToRemove.Value;
            int sectionIndex = FindPassiveSectionIndex(sections, sectionType);
            if (sectionIndex >= 0)
            {
                ResetPassiveSectionValue(definition, sectionType);
                sections.DeleteArrayElementAtIndex(sectionIndex);
                GUI.changed = true;
            }
        }
    }

    private void DrawPassiveSectionValue(
        SerializedProperty definition,
        CharacterPassiveSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterPassiveSectionType.Linkage:
                SerializedProperty triggerProperty =
                    definition.FindPropertyRelative(
                        PassiveTriggerPropertyName);
                DrawAttackEnumPopup(
                    triggerProperty,
                    "트리거",
                    PassiveTriggerOptions);
                CharacterPassiveTrigger trigger = triggerProperty != null
                    ? (CharacterPassiveTrigger)triggerProperty.enumValueIndex
                    : CharacterPassiveTrigger.OnAttack;
                if (trigger != CharacterPassiveTrigger.OnAttack)
                {
                    SetEnumValue(
                        definition,
                        ActionLinkagePropertyName,
                        (int)CharacterActionLinkage.None);
                }
                if (trigger == CharacterPassiveTrigger.OnStatusAcquired)
                {
                    DrawAttackEnumPopup(
                        definition.FindPropertyRelative(
                            PassiveStatusTargetPropertyName),
                        "상태 획득 대상",
                        PassiveStatusTargetOptions);
                    SerializedProperty triggerStatusScope =
                        definition.FindPropertyRelative(
                            PassiveTriggerStatusScopePropertyName);
                    DrawAttackEnumPopup(
                        triggerStatusScope,
                        "상태 필터 범위",
                        StatusSelectionScopeOptions);
                    SerializedProperty triggerStatusEffect =
                        definition.FindPropertyRelative(
                            PassiveTriggerStatusEffectPropertyName);
                    SerializedProperty triggerStatusEffects =
                        definition.FindPropertyRelative(
                            PassiveTriggerStatusEffectsPropertyName);
                    bool selectsTriggerStatuses =
                        triggerStatusScope == null ||
                        triggerStatusScope.enumValueIndex ==
                        (int)CharacterStatusSelectionScope
                            .SelectedStatuses;
                    if (selectsTriggerStatuses &&
                        triggerStatusEffect != null)
                    {
                        SerializedProperty statusTarget =
                            definition.FindPropertyRelative(
                                PassiveStatusTargetPropertyName);
                        CharacterTargetFaction? filterFaction =
                            statusTarget?.enumValueIndex switch
                            {
                                (int)CharacterPassiveStatusTarget.Enemy =>
                                    CharacterTargetFaction.Enemy,
                                (int)CharacterPassiveStatusTarget.Ally =>
                                    CharacterTargetFaction.Ally,
                                _ => null
                            };
                        PS260714StatusEffectSelection.Draw(
                            triggerStatusEffects,
                            triggerStatusEffect,
                            new GUIContent(
                                "상태 필터",
                                "비워 두면 모든 상태 적용에 반응합니다."),
                            new PS260714StatusEffectSelectionOptions(
                                allowNone: true,
                                targetFaction: filterFaction));
                    }
                    else if (!selectsTriggerStatuses)
                    {
                        EditorGUILayout.HelpBox(
                            "획득한 상태의 버프/디버프 분류로 필터링합니다. " +
                            "개별 상태 선택 목록은 사용하지 않습니다.",
                            MessageType.Info);
                    }
                    EditorGUILayout.HelpBox(
                        "선택한 진영의 대상에게 지정 상태가 적용되면 " +
                        "패시브가 발동합니다. 대상 설정의 '없음'은 " +
                        "상태가 적용된 대상을 재사용합니다.",
                        MessageType.Info);
                }
                else if (trigger == CharacterPassiveTrigger.OnCooldown)
                {
                    SerializedProperty cooldownProperty =
                        definition.FindPropertyRelative(
                            PassiveCooldownPropertyName);
                    if (cooldownProperty == null)
                    {
                        EditorGUILayout.HelpBox(
                            "쿨다운 속성을 찾을 수 없습니다.",
                            MessageType.Error);
                    }
                    else
                    {
                        cooldownProperty.floatValue = TimePrecision.Normalize(
                            EditorGUILayout.FloatField(
                                "쿨다운 (초)",
                                cooldownProperty.floatValue),
                            TimePrecision.Step);
                    }

                    EditorGUILayout.HelpBox(
                        "전투 시작 후 설정 시간이 지날 때마다 패시브 발동을 시도합니다. 행동불가 중에는 쿨다운이 정지합니다.",
                        MessageType.Info);
                }
                else if (trigger == CharacterPassiveTrigger.OnKill)
                {
                    SerializedProperty killSourceProperty =
                        definition.FindPropertyRelative(
                            PassiveKillSourcePropertyName);
                    DrawAttackEnumPopup(
                        killSourceProperty,
                        "킬 주체",
                        PassiveKillSourceOptions);
                    CharacterPassiveKillSource killSource =
                        killSourceProperty != null
                            ? (CharacterPassiveKillSource)
                                killSourceProperty.enumValueIndex
                            : CharacterPassiveKillSource.Self;
                    if (killSource ==
                        CharacterPassiveKillSource.SpecificCharacter)
                    {
                        SerializedProperty specifiedKiller =
                            definition.FindPropertyRelative(
                                PassiveSpecifiedKillerPropertyName);
                        if (specifiedKiller == null)
                        {
                            EditorGUILayout.HelpBox(
                                "지정 캐릭터 속성을 찾을 수 없습니다.",
                                MessageType.Error);
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(
                                specifiedKiller,
                                new GUIContent("지정된 캐릭터"));
                        }
                    }

                    EditorGUILayout.HelpBox(
                        "선택한 아군 캐릭터가 적을 처치할 때마다 " +
                        "패시브 발동을 시도합니다. 킬러 정보가 없는 " +
                        "아이템 및 환경 처치는 제외됩니다.",
                        MessageType.Info);
                }
                else if (trigger ==
                         CharacterPassiveTrigger.OnAttackTargetSelected)
                {
                    EditorGUILayout.HelpBox(
                        "기본 공격 대상이 확정된 직후, 공격 효과가 실행되기 " +
                        "전에 패시브 발동을 시도합니다. 대상 설정의 " +
                        "'없음'은 이번에 선택된 공격 대상을 재사용합니다.",
                        MessageType.Info);
                }
                else
                {
                    DrawAttackEnumPopup(
                        definition.FindPropertyRelative(
                            ActionLinkagePropertyName),
                        "연동 방식",
                        ActionLinkageOptions);
                    EditorGUILayout.HelpBox(
                        "트리거가 된 공격의 실행 결과와 연결합니다.",
                        MessageType.Info);
                }
                break;

            case CharacterPassiveSectionType.Condition:
                DrawPassiveAttackTargetRelationCondition(definition);
                DrawNumericConditions(definition);
                break;

            case CharacterPassiveSectionType.SelfStatusCost:
                DrawPassiveSelfStatusCost(definition);
                break;

            case CharacterPassiveSectionType.StatusContribution:
                DrawStatusContributionMultipliers(
                    definition.FindPropertyRelative(
                        StatusContributionMultipliersPropertyName),
                    false);
                break;

            case CharacterPassiveSectionType.Subject:
                DrawAttackSubject(definition, true);
                break;

            case CharacterPassiveSectionType.Ability:
                DrawAbility(definition);
                break;
        }
    }

    private void AddPassiveDefinition()
    {
        if (_selectedCharacter == null || _serializedCharacter == null)
            return;

        _serializedCharacter.UpdateIfRequiredOrScript();
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            PassiveDefinitionsPropertyName);
        if (definitions == null)
            return;

        int newIndex = definitions.arraySize;
        definitions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty definition =
            definitions.GetArrayElementAtIndex(newIndex);
        SerializedProperty sections = definition.FindPropertyRelative(
            PassiveSectionsPropertyName);
        sections?.ClearArray();
        ResetPassiveDefinitionValues(definition);
        definition.isExpanded = true;

        if (_serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedCharacter);

        _passiveExpanded = true;
    }

    private static void ResetPassiveDefinitionValues(
        SerializedProperty definition)
    {
        ResetActionIconSprite(definition);
        ResetActionAudioClip(definition);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.Linkage);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.Condition);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.SelfStatusCost);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.StatusContribution);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.Subject);
        ResetPassiveSectionValue(
            definition,
            CharacterPassiveSectionType.Ability);
        ResetExplicitEffects(definition);
    }

    private static void ResetPassiveSectionValue(
        SerializedProperty definition,
        CharacterPassiveSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterPassiveSectionType.Linkage:
                SetEnumValue(
                    definition,
                    PassiveTriggerPropertyName,
                    (int)CharacterPassiveTrigger.OnAttack);
                SetEnumValue(
                    definition,
                    PassiveKillSourcePropertyName,
                    (int)CharacterPassiveKillSource.Self);
                SerializedProperty specifiedKiller =
                    definition.FindPropertyRelative(
                        PassiveSpecifiedKillerPropertyName);
                if (specifiedKiller != null)
                    specifiedKiller.objectReferenceValue = null;
                SetEnumValue(
                    definition,
                    PassiveStatusTargetPropertyName,
                    (int)CharacterPassiveStatusTarget.Enemy);
                SerializedProperty triggerStatusEffect =
                    definition.FindPropertyRelative(
                        PassiveTriggerStatusEffectPropertyName);
                if (triggerStatusEffect != null)
                    triggerStatusEffect.objectReferenceValue = null;
                SerializedProperty triggerStatusEffects =
                    definition.FindPropertyRelative(
                        PassiveTriggerStatusEffectsPropertyName);
                triggerStatusEffects?.ClearArray();
                SetEnumValue(
                    definition,
                    PassiveTriggerStatusScopePropertyName,
                    (int)CharacterStatusSelectionScope.SelectedStatuses);
                SerializedProperty cooldown = definition.FindPropertyRelative(
                    PassiveCooldownPropertyName);
                if (cooldown != null)
                    cooldown.floatValue = 1f;
                SetEnumValue(
                    definition,
                    ActionLinkagePropertyName,
                    (int)CharacterActionLinkage.None);
                break;

            case CharacterPassiveSectionType.Condition:
                SetEnumValue(
                    definition,
                    PassiveAttackTargetRelationPropertyName,
                    (int)CharacterPassiveAttackTargetRelation.Any);
                ClearNumericConditions(definition);
                break;

            case CharacterPassiveSectionType.SelfStatusCost:
                ResetPassiveSelfStatusCost(definition);
                break;

            case CharacterPassiveSectionType.StatusContribution:
                definition.FindPropertyRelative(
                    StatusContributionMultipliersPropertyName)?.ClearArray();
                break;

            case CharacterPassiveSectionType.Subject:
                SetEnumValue(
                    definition,
                    TargetFactionPropertyName,
                    (int)CharacterTargetFaction.Enemy);
                SetEnumValue(
                    definition,
                    AttackSubjectPropertyName,
                    (int)CharacterAttackSubject.Random);
                SerializedProperty subjectCount =
                    definition.FindPropertyRelative(
                        AttackSubjectCountPropertyName);
                if (subjectCount != null)
                    subjectCount.intValue = 1;
                SetEnumValue(
                    definition,
                    AttackSubjectMetricPropertyName,
                    (int)CharacterAttackSubjectMetric.Health);
                ClearAreaOffsets(definition);
                break;

            case CharacterPassiveSectionType.Ability:
                SetEnumValue(
                    definition,
                    AttackDamageTypePropertyName,
                    (int)CharacterAttackDamageType.Physical);
                SetEnumValue(
                    definition,
                    DamageAmountModePropertyName,
                    (int)CharacterDamageAmountMode.Ratio);
                SerializedProperty damageAmount =
                    definition.FindPropertyRelative(
                        DamageAmountPropertyName);
                if (damageAmount != null)
                    damageAmount.floatValue = 1f;
                ResetStatusEffectValues(definition);
                ClearExplicitEffects(definition);
                break;
        }
    }

    private static void DrawPassiveSelfStatusCost(
        SerializedProperty definition)
    {
        SerializedProperty cost = definition?.FindPropertyRelative(
            PassiveSelfStatusCostPropertyName);
        if (cost == null)
        {
            EditorGUILayout.HelpBox(
                "자기 상태 비용 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        SerializedProperty statusEffect = cost.FindPropertyRelative(
            StatusCostEffectPropertyName);
        SerializedProperty requiredStacks = cost.FindPropertyRelative(
            StatusCostRequiredStacksPropertyName);
        SerializedProperty consumedStacks = cost.FindPropertyRelative(
            StatusCostConsumedStacksPropertyName);
        if (statusEffect == null || requiredStacks == null ||
            consumedStacks == null)
        {
            EditorGUILayout.HelpBox(
                "자기 상태 비용 세부 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        PS260714StatusEffectSelection.DrawSingle(
            statusEffect,
            new GUIContent("요구 상태"),
            new PS260714StatusEffectSelectionOptions(
                targetFaction: CharacterTargetFaction.Ally));
        requiredStacks.intValue = Mathf.Max(
            1,
            EditorGUILayout.IntField(
                "필요 스택",
                requiredStacks.intValue));
        consumedStacks.intValue = Mathf.Clamp(
            EditorGUILayout.IntField(
                "성공 시 소비 스택",
                consumedStacks.intValue),
            1,
            requiredStacks.intValue);
        EditorGUILayout.HelpBox(
            "능력이 실제로 성공한 경우에만 시전자의 지정 상태 " +
            "스택을 소비합니다.",
            MessageType.Info);
    }

    private static void ResetPassiveSelfStatusCost(
        SerializedProperty definition)
    {
        SerializedProperty cost = definition?.FindPropertyRelative(
            PassiveSelfStatusCostPropertyName);
        if (cost == null)
            return;

        SerializedProperty statusEffect = cost.FindPropertyRelative(
            StatusCostEffectPropertyName);
        if (statusEffect != null)
            statusEffect.objectReferenceValue = null;

        SerializedProperty requiredStacks = cost.FindPropertyRelative(
            StatusCostRequiredStacksPropertyName);
        if (requiredStacks != null)
            requiredStacks.intValue = 1;

        SerializedProperty consumedStacks = cost.FindPropertyRelative(
            StatusCostConsumedStacksPropertyName);
        if (consumedStacks != null)
            consumedStacks.intValue = 1;
    }

    private static void DrawStatusContributionMultipliers(
        SerializedProperty modifiers,
        bool effectLocal)
    {
        if (modifiers == null)
        {
            EditorGUILayout.HelpBox(
                "상태 기여 배율 목록을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            effectLocal
                ? "이 효과를 계산할 때 선택한 상태가 제공하는 공격력 " +
                  "기여도에만 배율을 적용합니다."
                : "선택한 상태가 자신에게 제공하는 능력치 기여도에 " +
                  "상시 배율을 적용합니다. 여러 규칙이 일치하면 " +
                  "배율을 서로 곱합니다.",
            MessageType.Info);

        int removeIndex = -1;
        for (int index = 0; index < modifiers.arraySize; index++)
        {
            SerializedProperty modifier =
                modifiers.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"기여 배율 {index + 1}",
                EditorStyles.miniBoldLabel);
            if (GUILayout.Button(
                    "X",
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            PS260714StatusEffectSelection.DrawSingle(
                modifier.FindPropertyRelative(
                    StatusContributionStatusPropertyName),
                new GUIContent("상태"));

            SerializedProperty statType = modifier.FindPropertyRelative(
                StatusContributionStatTypePropertyName);
            if (effectLocal && statType != null)
            {
                statType.enumValueIndex =
                    (int)StatusEffectStatType.AttackPower;
            }
            using (new EditorGUI.DisabledScope(effectLocal))
            {
                DrawAttackEnumPopup(
                    statType,
                    "능력치",
                    StatusContributionStatTypeOptions);
            }

            SerializedProperty multiplier = modifier.FindPropertyRelative(
                StatusContributionMultiplierPropertyName);
            if (multiplier != null)
            {
                multiplier.floatValue = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        new GUIContent(
                            "총 기여 배율",
                            "1은 기본 기여도, 1.5는 150%, 3은 300%입니다."),
                        multiplier.floatValue));
            }
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0)
                break;
        }

        if (removeIndex >= 0)
        {
            modifiers.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }

        if (GUILayout.Button("+ 상태 기여 배율 추가", EditorStyles.miniButton))
        {
            int newIndex = modifiers.arraySize;
            modifiers.InsertArrayElementAtIndex(newIndex);
            SerializedProperty modifier =
                modifiers.GetArrayElementAtIndex(newIndex);
            SetObjectReferenceValue(
                modifier,
                StatusContributionStatusPropertyName,
                null);
            SetEnumValue(
                modifier,
                StatusContributionStatTypePropertyName,
                (int)StatusEffectStatType.AttackPower);
            SetFloatValue(
                modifier,
                StatusContributionMultiplierPropertyName,
                1f);
            GUI.changed = true;
        }
    }

    private void ShowPassiveSectionMenu(int passiveIndex)
    {
        if (_selectedCharacter == null)
            return;

        CharacterSO character = _selectedCharacter;
        SerializedObject serializedCharacter = new(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            PassiveDefinitionsPropertyName);
        if (definitions == null ||
            passiveIndex < 0 ||
            passiveIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(passiveIndex)
            .FindPropertyRelative(PassiveSectionsPropertyName);
        GenericMenu menu = new();
        foreach (CharacterPassiveSectionType sectionType in PassiveSectionOrder)
        {
            CharacterPassiveSectionType capturedType = sectionType;
            GUIContent label = new(GetPassiveSectionLabel(sectionType));
            if (FindPassiveSectionIndex(sections, sectionType) >= 0)
            {
                menu.AddDisabledItem(label, true);
                continue;
            }

            menu.AddItem(
                label,
                false,
                () => AddPassiveSection(
                    character,
                    passiveIndex,
                    capturedType));
        }

        menu.ShowAsContext();
    }

    private void AddPassiveSection(
        CharacterSO character,
        int passiveIndex,
        CharacterPassiveSectionType sectionType)
    {
        if (character == null)
            return;

        SerializedObject serializedCharacter = new(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            PassiveDefinitionsPropertyName);
        if (definitions == null ||
            passiveIndex < 0 ||
            passiveIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(passiveIndex)
            .FindPropertyRelative(PassiveSectionsPropertyName);
        if (sections == null ||
            FindPassiveSectionIndex(sections, sectionType) >= 0)
        {
            return;
        }

        int newIndex = sections.arraySize;
        sections.InsertArrayElementAtIndex(newIndex);
        SerializedProperty section = sections.GetArrayElementAtIndex(newIndex);
        section.enumValueIndex = (int)sectionType;
        ResetPassiveSectionValue(
            definitions.GetArrayElementAtIndex(passiveIndex),
            sectionType);
        if (sectionType == CharacterPassiveSectionType.Ability)
        {
            ResetExplicitEffects(
                definitions.GetArrayElementAtIndex(passiveIndex));
        }
        if (sectionType == CharacterPassiveSectionType.Condition)
        {
            AddDefaultNumericCondition(
                definitions.GetArrayElementAtIndex(passiveIndex));
        }

        if (serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(character);

        Repaint();
    }

    private static int FindPassiveSectionIndex(
        SerializedProperty sections,
        CharacterPassiveSectionType sectionType)
    {
        if (sections == null)
            return -1;

        for (int index = 0; index < sections.arraySize; index++)
        {
            if (sections.GetArrayElementAtIndex(index).enumValueIndex ==
                (int)sectionType)
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetPassiveSectionLabel(
        CharacterPassiveSectionType sectionType)
    {
        return sectionType switch
        {
            CharacterPassiveSectionType.Linkage => "1. 트리거 / 연동",
            CharacterPassiveSectionType.Condition => "2. 조건",
            CharacterPassiveSectionType.SelfStatusCost => "3. 자기 상태 비용",
            CharacterPassiveSectionType.StatusContribution =>
                "4. 상태 능력치 기여 배율",
            CharacterPassiveSectionType.Subject => "5. 대상",
            CharacterPassiveSectionType.Ability => "6. 능력",
            _ => sectionType.ToString()
        };
    }

    private void DrawSkillSettingsFoldout()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _skillExpanded = EditorGUILayout.Foldout(
            _skillExpanded,
            "3. 기술",
            true,
            EditorStyles.foldoutHeader);

        bool addSkill;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            addSkill = GUILayout.Button(
                new GUIContent("+", "기술 블록 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        EditorGUILayout.EndHorizontal();

        if (addSkill)
            AddSkillDefinition();

        if (_skillExpanded)
        {
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                _serializedCharacter.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                DrawSkillDefinitions();
                if (EditorGUI.EndChangeCheck() &&
                    _serializedCharacter.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_selectedCharacter);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawSkillDefinitions()
    {
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            SkillDefinitionsPropertyName);
        if (definitions == null)
        {
            EditorGUILayout.HelpBox(
                "기술 구조를 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        SerializedProperty executionPolicy =
            _serializedCharacter.FindProperty(
                SkillExecutionPolicyPropertyName);
        if (executionPolicy != null)
        {
            EditorGUILayout.PropertyField(
                executionPolicy,
                new GUIContent(
                    "기술 실행 정책",
                    "First Successful: 실행 가능한 첫 블록만 실행\n" +
                    "Sequence All: 실행 가능한 블록을 순서대로 실행"));
            EditorGUILayout.Space(4f);
        }

        if (definitions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 기술 블록을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        for (int skillIndex = 0;
             skillIndex < definitions.arraySize;
             skillIndex++)
        {
            if (DrawSkillDefinition(
                    definitions.GetArrayElementAtIndex(skillIndex),
                    skillIndex))
            {
                definitions.DeleteArrayElementAtIndex(skillIndex);
                GUI.changed = true;
                break;
            }
        }
    }

    private bool DrawSkillDefinition(
        SerializedProperty definition,
        int skillIndex)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        definition.isExpanded = EditorGUILayout.Foldout(
            definition.isExpanded,
            $"기술 {skillIndex + 1}",
            true,
            EditorStyles.foldoutHeader);

        bool showSectionMenu = GUILayout.Button(
            new GUIContent("+", "기술 구성 블록 추가"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        bool removeSkill = GUILayout.Button(
            new GUIContent("-", "기술 블록 삭제"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        if (definition.isExpanded)
        {
            DrawActionIconSprite(definition);
            DrawActionAudioClip(definition);
            EditorGUILayout.Space(4f);
            SerializedProperty sections = definition.FindPropertyRelative(
                SkillSectionsPropertyName);
            DrawSkillSectionBlocks(definition, sections);
        }

        EditorGUILayout.EndVertical();

        if (showSectionMenu && !removeSkill)
            ShowSkillSectionMenu(skillIndex);

        return removeSkill;
    }

    private void DrawSkillSectionBlocks(
        SerializedProperty definition,
        SerializedProperty sections)
    {
        if (sections == null || sections.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 구성 블록을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        CharacterSkillSectionType? sectionToRemove = null;
        foreach (CharacterSkillSectionType sectionType in SkillSectionOrder)
        {
            int sectionIndex = FindSkillSectionIndex(sections, sectionType);
            if (sectionIndex < 0)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                GetSkillSectionLabel(sectionType),
                EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent("-", "세부 블록 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(20f)))
            {
                sectionToRemove = sectionType;
            }
            EditorGUILayout.EndHorizontal();
            DrawSkillSectionValue(definition, sectionType);
            EditorGUILayout.EndVertical();

            if (sectionToRemove.HasValue)
                break;
        }

        if (sectionToRemove.HasValue)
        {
            CharacterSkillSectionType sectionType = sectionToRemove.Value;
            int sectionIndex = FindSkillSectionIndex(sections, sectionType);
            if (sectionIndex >= 0)
            {
                ResetSkillSectionValue(definition, sectionType);
                sections.DeleteArrayElementAtIndex(sectionIndex);
                GUI.changed = true;
            }
        }
    }

    private void DrawSkillSectionValue(
        SerializedProperty definition,
        CharacterSkillSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterSkillSectionType.Cost:
                SerializedProperty cost = definition.FindPropertyRelative(
                    SkillCostPropertyName);
                if (cost != null)
                {
                    cost.intValue = Mathf.Max(
                        1,
                        EditorGUILayout.IntField("코스트", cost.intValue));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "코스트 속성을 찾을 수 없습니다.",
                        MessageType.Error);
                }
                break;

            case CharacterSkillSectionType.Linkage:
                DrawAttackEnumPopup(
                    definition.FindPropertyRelative(
                        ActionLinkagePropertyName),
                    "연동 방식",
                    ActionLinkageOptions);
                EditorGUILayout.HelpBox(
                    "마지막으로 실행된 일반 공격의 결과와 연결합니다.",
                    MessageType.Info);
                break;

            case CharacterSkillSectionType.Condition:
                DrawNumericConditions(definition);
                break;

            case CharacterSkillSectionType.Subject:
                DrawAttackSubject(definition, true);
                break;

            case CharacterSkillSectionType.Ability:
                DrawAbility(definition);
                break;
        }
    }

    private void AddSkillDefinition()
    {
        if (_selectedCharacter == null || _serializedCharacter == null)
            return;

        _serializedCharacter.UpdateIfRequiredOrScript();
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            SkillDefinitionsPropertyName);
        if (definitions == null)
            return;

        int newIndex = definitions.arraySize;
        definitions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty definition =
            definitions.GetArrayElementAtIndex(newIndex);
        SerializedProperty sections = definition.FindPropertyRelative(
            SkillSectionsPropertyName);
        sections?.ClearArray();
        ResetSkillDefinitionValues(definition);
        definition.isExpanded = true;

        if (_serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedCharacter);

        _skillExpanded = true;
    }

    private static void ResetSkillDefinitionValues(
        SerializedProperty definition)
    {
        ResetActionIconSprite(definition);
        ResetActionAudioClip(definition);
        foreach (CharacterSkillSectionType sectionType in SkillSectionOrder)
            ResetSkillSectionValue(definition, sectionType);
        ResetExplicitEffects(definition);
    }

    private static void ResetSkillSectionValue(
        SerializedProperty definition,
        CharacterSkillSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterSkillSectionType.Cost:
                SerializedProperty cost = definition.FindPropertyRelative(
                    SkillCostPropertyName);
                if (cost != null)
                    cost.intValue = 1;
                break;

            case CharacterSkillSectionType.Linkage:
                SetEnumValue(
                    definition,
                    ActionLinkagePropertyName,
                    (int)CharacterActionLinkage.PreviousAttackSucceeded);
                break;

            case CharacterSkillSectionType.Condition:
                ClearNumericConditions(definition);
                break;

            case CharacterSkillSectionType.Subject:
                SetEnumValue(
                    definition,
                    TargetFactionPropertyName,
                    (int)CharacterTargetFaction.Enemy);
                SetEnumValue(
                    definition,
                    AttackSubjectPropertyName,
                    (int)CharacterAttackSubject.Random);
                SerializedProperty subjectCount =
                    definition.FindPropertyRelative(
                        AttackSubjectCountPropertyName);
                if (subjectCount != null)
                    subjectCount.intValue = 1;
                SetEnumValue(
                    definition,
                    AttackSubjectMetricPropertyName,
                    (int)CharacterAttackSubjectMetric.Health);
                ClearAreaOffsets(definition);
                break;

            case CharacterSkillSectionType.Ability:
                SetEnumValue(
                    definition,
                    AttackDamageTypePropertyName,
                    (int)CharacterAttackDamageType.Physical);
                SetEnumValue(
                    definition,
                    DamageAmountModePropertyName,
                    (int)CharacterDamageAmountMode.Ratio);
                SerializedProperty damageAmount =
                    definition.FindPropertyRelative(
                        DamageAmountPropertyName);
                if (damageAmount != null)
                    damageAmount.floatValue = 1f;
                ResetStatusEffectValues(definition);
                ClearExplicitEffects(definition);
                break;
        }
    }

    private void ShowSkillSectionMenu(int skillIndex)
    {
        if (_selectedCharacter == null)
            return;

        CharacterSO character = _selectedCharacter;
        SerializedObject serializedCharacter = new(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            SkillDefinitionsPropertyName);
        if (definitions == null ||
            skillIndex < 0 ||
            skillIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(skillIndex)
            .FindPropertyRelative(SkillSectionsPropertyName);
        GenericMenu menu = new();
        foreach (CharacterSkillSectionType sectionType in SkillSectionOrder)
        {
            CharacterSkillSectionType capturedType = sectionType;
            GUIContent label = new(GetSkillSectionLabel(sectionType));
            if (FindSkillSectionIndex(sections, sectionType) >= 0)
            {
                menu.AddDisabledItem(label, true);
                continue;
            }

            menu.AddItem(
                label,
                false,
                () => AddSkillSection(
                    character,
                    skillIndex,
                    capturedType));
        }

        menu.ShowAsContext();
    }

    private void AddSkillSection(
        CharacterSO character,
        int skillIndex,
        CharacterSkillSectionType sectionType)
    {
        if (character == null)
            return;

        SerializedObject serializedCharacter = new(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            SkillDefinitionsPropertyName);
        if (definitions == null ||
            skillIndex < 0 ||
            skillIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(skillIndex)
            .FindPropertyRelative(SkillSectionsPropertyName);
        if (sections == null ||
            FindSkillSectionIndex(sections, sectionType) >= 0)
        {
            return;
        }

        int newIndex = sections.arraySize;
        sections.InsertArrayElementAtIndex(newIndex);
        SerializedProperty section = sections.GetArrayElementAtIndex(newIndex);
        section.enumValueIndex = (int)sectionType;
        ResetSkillSectionValue(
            definitions.GetArrayElementAtIndex(skillIndex),
            sectionType);
        if (sectionType == CharacterSkillSectionType.Ability)
        {
            ResetExplicitEffects(
                definitions.GetArrayElementAtIndex(skillIndex));
        }
        if (sectionType == CharacterSkillSectionType.Condition)
        {
            AddDefaultNumericCondition(
                definitions.GetArrayElementAtIndex(skillIndex));
        }

        if (serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(character);

        Repaint();
    }

    private static int FindSkillSectionIndex(
        SerializedProperty sections,
        CharacterSkillSectionType sectionType)
    {
        if (sections == null)
            return -1;

        for (int index = 0; index < sections.arraySize; index++)
        {
            if (sections.GetArrayElementAtIndex(index).enumValueIndex ==
                (int)sectionType)
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetSkillSectionLabel(
        CharacterSkillSectionType sectionType)
    {
        return sectionType switch
        {
            CharacterSkillSectionType.Cost => "1. 코스트",
            CharacterSkillSectionType.Linkage => "2. 연동",
            CharacterSkillSectionType.Condition => "3. 조건",
            CharacterSkillSectionType.Subject => "4. 대상",
            CharacterSkillSectionType.Ability => "5. 능력",
            _ => sectionType.ToString()
        };
    }

    private void DrawAttackSettingsFoldout()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _attackExpanded = EditorGUILayout.Foldout(
            _attackExpanded,
            "2. 공격",
            true,
            EditorStyles.foldoutHeader);

        bool addAttack;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            addAttack = GUILayout.Button(
                new GUIContent("+", "공격 구조 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        EditorGUILayout.EndHorizontal();

        if (addAttack)
            AddAttackDefinition();

        if (_attackExpanded)
        {
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                _serializedCharacter.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                DrawAttackDefinitions();
                if (EditorGUI.EndChangeCheck() &&
                    _serializedCharacter.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_selectedCharacter);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawAttackDefinitions()
    {
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            AttackDefinitionsPropertyName);
        if (definitions == null)
        {
            EditorGUILayout.HelpBox(
                "공격 구조를 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        if (definitions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 공격 구조를 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        for (int attackIndex = 0;
             attackIndex < definitions.arraySize;
             attackIndex++)
        {
            if (DrawAttackDefinition(
                    definitions.GetArrayElementAtIndex(attackIndex),
                    attackIndex))
            {
                definitions.DeleteArrayElementAtIndex(attackIndex);
                GUI.changed = true;
                break;
            }
        }
    }

    private bool DrawAttackDefinition(
        SerializedProperty definition,
        int attackIndex)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        definition.isExpanded = EditorGUILayout.Foldout(
            definition.isExpanded,
            $"공격 {attackIndex + 1}",
            true,
            EditorStyles.foldoutHeader);

        bool showSectionMenu;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            showSectionMenu = GUILayout.Button(
                new GUIContent("+", "공격 구성 탭 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        bool removeAttack = GUILayout.Button(
            new GUIContent("-", "공격 블록 삭제"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        if (definition.isExpanded)
        {
            DrawActionAudioClip(definition);
            EditorGUILayout.Space(4f);
            SerializedProperty sections = definition.FindPropertyRelative(
                AttackSectionsPropertyName);
            if (attackIndex == 0 &&
                FindAttackSectionIndex(
                    sections,
                    CharacterAttackSectionType.Linkage) >= 0)
            {
                EditorGUILayout.HelpBox(
                    "첫 공격 블록에는 앞선 공격이 없어 연동을 사용할 수 없습니다. 연동 블록을 제거해 주세요.",
                    MessageType.Warning);
            }
            DrawAttackSectionTabs(definition, sections);
        }

        EditorGUILayout.EndVertical();

        if (showSectionMenu && !removeAttack)
            ShowAttackSectionMenu(attackIndex);

        return removeAttack;
    }

    private void DrawAttackSectionTabs(
        SerializedProperty definition,
        SerializedProperty sections)
    {
        if (sections == null || sections.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 구성 탭을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        CharacterAttackSectionType? sectionToRemove = null;
        foreach (CharacterAttackSectionType sectionType in AttackSectionOrder)
        {
            int sectionIndex = FindAttackSectionIndex(sections, sectionType);
            if (sectionIndex < 0)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                GetAttackSectionLabel(sectionType),
                EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent("-", "세부 탭 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(20f)))
            {
                sectionToRemove = sectionType;
            }
            EditorGUILayout.EndHorizontal();

            DrawAttackSectionValue(definition, sectionType);
            EditorGUILayout.EndVertical();

            if (sectionToRemove.HasValue)
                break;
        }

        if (sectionToRemove.HasValue)
        {
            CharacterAttackSectionType sectionType = sectionToRemove.Value;
            int sectionIndex = FindAttackSectionIndex(sections, sectionType);
            if (sectionIndex >= 0)
            {
                ResetAttackSectionValue(definition, sectionType);
                sections.DeleteArrayElementAtIndex(sectionIndex);
                GUI.changed = true;
            }
        }
    }

    private void DrawAttackSectionValue(
        SerializedProperty definition,
        CharacterAttackSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterAttackSectionType.Linkage:
                DrawAttackEnumPopup(
                    definition.FindPropertyRelative(
                        ActionLinkagePropertyName),
                    "연동 방식",
                    ActionLinkageOptions);
                EditorGUILayout.HelpBox(
                    "바로 앞 공격 블록의 실행 결과와 연결합니다.",
                    MessageType.Info);
                break;

            case CharacterAttackSectionType.Condition:
                DrawNumericConditions(definition);
                break;

            case CharacterAttackSectionType.Subject:
                DrawAttackSubject(definition, true);
                break;

            case CharacterAttackSectionType.Ability:
                DrawAbility(definition);
                break;
        }
    }

    private void DrawAttackSubject(
        SerializedProperty definition,
        bool allowAllyFaction = false,
        bool allowInheritedSubject = true)
    {
        SerializedProperty targetFaction = definition.FindPropertyRelative(
            TargetFactionPropertyName);
        SerializedProperty subject = definition.FindPropertyRelative(
            AttackSubjectPropertyName);
        SerializedProperty subjectCount = definition.FindPropertyRelative(
            AttackSubjectCountPropertyName);
        SerializedProperty subjectMetric = definition.FindPropertyRelative(
            AttackSubjectMetricPropertyName);

        CharacterTargetFaction faction = CharacterTargetFaction.Enemy;
        if (allowAllyFaction)
            faction = DrawTargetFactionToggle(targetFaction);
        else if (targetFaction != null)
            targetFaction.enumValueIndex = (int)faction;

        DrawMappedEnumPopup(
            subject,
            "선정 방식",
            faction == CharacterTargetFaction.Ally
                ? allowInheritedSubject
                    ? AllyAttackSubjectOptions
                    : FreshAllySubjectOptions
                : allowInheritedSubject
                    ? AttackSubjectOptions
                    : FreshEnemySubjectOptions,
            faction == CharacterTargetFaction.Ally
                ? allowInheritedSubject
                    ? AllyAttackSubjectValues
                    : FreshAllySubjectValues
                : allowInheritedSubject
                    ? AttackSubjectValues
                    : FreshEnemySubjectValues);

        bool hasFixedTargetSet = subject != null &&
            (subject.enumValueIndex == (int)CharacterAttackSubject.All ||
             subject.enumValueIndex ==
                 (int)CharacterAttackSubject.AllExceptSelf ||
             subject.enumValueIndex == (int)CharacterAttackSubject.Self ||
             subject.enumValueIndex == (int)CharacterAttackSubject.None);
        bool reusesPreviousTargets = subject != null &&
            subject.enumValueIndex == (int)CharacterAttackSubject.None;
        if (reusesPreviousTargets)
        {
            EditorGUILayout.HelpBox(
                "직전 공격 블록 또는 최근 일반 공격이 선택했던 대상과 동일한 대상을 사용합니다.",
                MessageType.Info);
        }
        if (!hasFixedTargetSet && subjectCount != null)
        {
            subjectCount.intValue = Mathf.Max(
                1,
                EditorGUILayout.IntField(
                    "대상 수",
                    subjectCount.intValue));
        }
        else if (!hasFixedTargetSet)
        {
            EditorGUILayout.HelpBox(
                "대상 수 속성을 찾을 수 없습니다.",
                MessageType.Error);
        }

        bool usesComparisonMetric = subject != null &&
            (subject.enumValueIndex ==
                 (int)CharacterAttackSubject.HighestValue ||
             subject.enumValueIndex ==
                 (int)CharacterAttackSubject.LowestValue);
        if (usesComparisonMetric && faction == CharacterTargetFaction.Ally)
        {
            DrawMappedEnumPopup(
                subjectMetric,
                "비교 수치",
                AllySubjectMetricOptions,
                AllySubjectMetricValues);
        }
        else if (usesComparisonMetric)
        {
            DrawMappedEnumPopup(
                subjectMetric,
                "비교 수치",
                AttackSubjectMetricOptions,
                EnemySubjectMetricValues);
        }

        SerializedProperty targetRetentionMode =
            definition.FindPropertyRelative(
                AttackTargetRetentionModePropertyName);
        if (targetRetentionMode != null)
        {
            int targetCount = subjectCount != null
                ? Mathf.Max(1, subjectCount.intValue)
                : 1;
            CharacterAttackSubject selectedSubject = subject != null
                ? (CharacterAttackSubject)subject.enumValueIndex
                : CharacterAttackSubject.None;
            bool supportsRetention =
                CharacterAttackDefinition.SupportsTargetRetention(
                    selectedSubject,
                    targetCount);
            if (!supportsRetention)
            {
                targetRetentionMode.enumValueIndex =
                    (int)CharacterAttackTargetRetentionMode
                        .ReselectEachAttack;
            }

            using (new EditorGUI.DisabledScope(!supportsRetention))
            {
                DrawAttackEnumPopup(
                    targetRetentionMode,
                    "대상 유지 방식",
                    AttackTargetRetentionModeOptions);
            }

            if (targetRetentionMode.enumValueIndex ==
                (int)CharacterAttackTargetRetentionMode.LockUntilInvalid)
            {
                EditorGUILayout.HelpBox(
                    "처음 선정한 대상을 계속 사용합니다. 대상이 사망·퇴장하거나 " +
                    "현재 조건을 만족하지 않으면 새 대상을 선정합니다.",
                    MessageType.Info);
            }
            else if (!supportsRetention)
            {
                EditorGUILayout.HelpBox(
                    "대상 고정은 직접 선정하는 단일 대상에만 사용할 수 있습니다.",
                    MessageType.Info);
            }
        }

        DrawTargetAreaEditor(
            definition,
            reusesPreviousTargets
                ? CharacterTargetFaction.Enemy
                : faction);
    }

    private void DrawTargetAreaEditor(
        SerializedProperty definition,
        CharacterTargetFaction faction)
    {
        SerializedProperty offsets = definition.FindPropertyRelative(
            AreaOffsetsPropertyName);
        if (offsets == null)
        {
            EditorGUILayout.HelpBox(
                "Area 범위 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        bool targetsAllies = faction == CharacterTargetFaction.Ally;
        bool showAreaEditor;
        Rect buttonRect;
        using (new EditorGUI.DisabledScope(
                   targetsAllies ||
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            showAreaEditor = GUILayout.Button(
                new GUIContent(
                    $"Area ({offsets.arraySize + 1}칸)",
                    targetsAllies
                        ? "아군은 던전 격자 좌표가 없어 Area를 사용할 수 없습니다."
                        : "타겟 칸을 기준으로 범위를 편집합니다."),
                EditorStyles.miniButton,
                GUILayout.Height(22f));
            buttonRect = GUILayoutUtility.GetLastRect();
        }

        if (showAreaEditor && _selectedCharacter != null)
        {
            PopupWindow.Show(
                buttonRect,
                new TargetAreaPopup(
                    _selectedCharacter,
                    definition.propertyPath));
        }
    }

    private static CharacterTargetFaction DrawTargetFactionToggle(
        SerializedProperty property)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                "대상 진영 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return CharacterTargetFaction.Enemy;
        }

        int currentIndex = Mathf.Clamp(
            property.enumValueIndex,
            0,
            TargetFactionOptions.Length - 1);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("대상 진영");
        currentIndex = GUILayout.Toolbar(currentIndex, TargetFactionOptions);
        EditorGUILayout.EndHorizontal();
        property.enumValueIndex = currentIndex;
        return (CharacterTargetFaction)currentIndex;
    }

    internal static void DrawNumericConditions(SerializedProperty definition)
    {
        SerializedProperty matchMode = definition.FindPropertyRelative(
            ConditionMatchModePropertyName);
        SerializedProperty conditions = definition.FindPropertyRelative(
            NumericConditionsPropertyName);
        SerializedProperty targetFaction = definition.FindPropertyRelative(
            TargetFactionPropertyName);
        if (matchMode == null || conditions == null)
        {
            EditorGUILayout.HelpBox(
                "조건 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            matchMode,
            "조건 결합",
            ConditionMatchModeOptions);
        bool targetsAllies = targetFaction != null &&
            targetFaction.enumValueIndex ==
            (int)CharacterTargetFaction.Ally;

        int removeIndex = -1;
        for (int index = 0; index < conditions.arraySize; index++)
        {
            SerializedProperty condition =
                conditions.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"조건 {index + 1}",
                EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent("-", "조건 삭제"),
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(20f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            MigrateLegacyStatusCondition(condition);

            SerializedProperty conditionTarget =
                condition.FindPropertyRelative(ConditionTargetPropertyName);
            DrawAttackEnumPopup(
                conditionTarget,
                "조건 대상",
                ConditionTargetOptions);
            bool checksSource = conditionTarget != null &&
                conditionTarget.enumValueIndex ==
                (int)CharacterConditionTarget.Source;

            SerializedProperty metric = condition.FindPropertyRelative(
                NumericConditionMetricPropertyName);
            DrawMappedEnumPopup(
                metric,
                "비교 수치",
                targetsAllies || checksSource
                    ? AllyNumericConditionMetricOptions
                    : NumericConditionMetricOptions,
                targetsAllies || checksSource
                    ? AllyNumericConditionMetricValues
                    : EnemyNumericConditionMetricValues);

            bool checksStatusStacks = metric != null &&
                metric.enumValueIndex ==
                (int)CharacterNumericConditionMetric.StatusStackCount;
            if (checksStatusStacks)
            {
                SerializedProperty statusSelectionScope =
                    condition.FindPropertyRelative(
                        StatusSelectionScopePropertyName);
                DrawAttackEnumPopup(
                    statusSelectionScope,
                    "상태 선택 범위",
                    StatusSelectionScopeOptions);
                bool selectsConfiguredStatuses =
                    statusSelectionScope == null ||
                    statusSelectionScope.enumValueIndex ==
                    (int)CharacterStatusSelectionScope.SelectedStatuses;
                SerializedProperty heldStatus =
                    condition.FindPropertyRelative(StatusEffectPropertyName);
                SerializedProperty heldStatuses =
                    condition.FindPropertyRelative(
                        ConditionStatusEffectsPropertyName);
                if (selectsConfiguredStatuses && heldStatus == null)
                {
                    EditorGUILayout.HelpBox(
                        "보유 상태 속성을 찾을 수 없습니다.",
                        MessageType.Error);
                }
                else if (selectsConfiguredStatuses)
                {
                    CharacterTargetFaction statusFaction =
                        checksSource || targetsAllies
                            ? CharacterTargetFaction.Ally
                            : CharacterTargetFaction.Enemy;
                    PS260714StatusEffectSelection.Draw(
                        heldStatuses,
                        heldStatus,
                        new GUIContent(
                            "상태 종류",
                            "스택 수를 확인할 상태를 선택합니다."),
                        new PS260714StatusEffectSelectionOptions(
                            targetFaction: statusFaction));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "대상이 현재 보유한 서로 다른 버프/디버프 종류를 " +
                        "아래 판정의 후보로 사용합니다.",
                        MessageType.Info);
                }

                SerializedProperty statusMatchMode =
                    condition.FindPropertyRelative(
                        StatusConditionMatchModePropertyName);
                DrawAttackEnumPopup(
                    statusMatchMode,
                    "상태 선택 판정",
                    StatusConditionMatchModeOptions);
                if (statusMatchMode != null &&
                    statusMatchMode.enumValueIndex ==
                    (int)CharacterStatusConditionMatchMode.AtLeastCount)
                {
                    SerializedProperty statusMatchCount =
                        condition.FindPropertyRelative(
                            StatusConditionMatchCountPropertyName);
                    if (statusMatchCount != null)
                    {
                        statusMatchCount.intValue = Mathf.Max(
                            1,
                            EditorGUILayout.IntField(
                                "필요 상태 수",
                                statusMatchCount.intValue));
                    }
                }

                EditorGUILayout.HelpBox(
                    "범위에 포함된 각 상태에 아래 스택 비교를 적용한 뒤 " +
                    "하나 이상/모두/N개 이상으로 결합합니다.",
                    MessageType.Info);
            }

            DrawAttackEnumPopup(
                condition.FindPropertyRelative(
                    NumericComparisonPropertyName),
                "비교 방식",
                NumericComparisonOptions);

            SerializedProperty threshold =
                condition.FindPropertyRelative(
                    NumericThresholdPropertyName);
            if (threshold != null)
            {
                bool usesWholeNumber = checksStatusStacks ||
                    (metric != null &&
                     metric.enumValueIndex ==
                     (int)CharacterNumericConditionMetric.StackCount);
                if (usesWholeNumber)
                {
                    int currentValue = Mathf.Max(
                        0,
                        Mathf.RoundToInt(threshold.floatValue));
                    threshold.floatValue = Mathf.Max(
                        0,
                        EditorGUILayout.IntField(
                            checksStatusStacks
                                ? "기준 스택"
                                : "기준값",
                            currentValue));
                }
                else
                {
                    threshold.floatValue = EditorGUILayout.FloatField(
                        "기준값",
                        threshold.floatValue);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "기준값 속성을 찾을 수 없습니다.",
                    MessageType.Error);
            }
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0)
                break;
        }

        if (removeIndex >= 0)
        {
            conditions.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }

        if (conditions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "조건이 없으면 모든 대상이 조건을 만족합니다.",
                EditorStyles.miniLabel);
        }

        if (GUILayout.Button("+ 조건 추가", EditorStyles.miniButton))
        {
            AddNumericCondition(
                conditions,
                targetsAllies
                    ? CharacterNumericConditionMetric.AttackPower
                    : CharacterNumericConditionMetric.Health);
            GUI.changed = true;
        }
    }

    private static void DrawPassiveAttackTargetRelationCondition(
        SerializedProperty definition)
    {
        SerializedProperty trigger = definition.FindPropertyRelative(
            PassiveTriggerPropertyName);
        if (trigger == null)
            return;

        CharacterPassiveTrigger triggerType =
            (CharacterPassiveTrigger)trigger.enumValueIndex;
        if (triggerType != CharacterPassiveTrigger.OnAttack &&
            triggerType !=
            CharacterPassiveTrigger.OnAttackTargetSelected)
        {
            return;
        }

        SerializedProperty relation = definition.FindPropertyRelative(
            PassiveAttackTargetRelationPropertyName);
        if (relation == null)
        {
            EditorGUILayout.HelpBox(
                "공격 대상 관계 조건 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            relation,
            "공격 대상 관계",
            PassiveAttackTargetRelationOptions);
        if (relation.enumValueIndex !=
            (int)CharacterPassiveAttackTargetRelation.Any)
        {
            EditorGUILayout.HelpBox(
                "첫 공격 시도는 비교 기준만 저장합니다. 이후 공격부터 " +
                "이번 공격의 최초 선택 대상과 직전 공격 시도의 최초 " +
                "선택 대상을 비교합니다. 수치 조건도 있으면 대상 관계와 " +
                "수치 조건을 모두 만족해야 합니다.",
                MessageType.Info);
        }
    }

    private static void ClearNumericConditions(SerializedProperty definition)
    {
        SetEnumValue(
            definition,
            ConditionMatchModePropertyName,
            (int)CharacterConditionMatchMode.All);
        SerializedProperty conditions = definition.FindPropertyRelative(
            NumericConditionsPropertyName);
        conditions?.ClearArray();
    }

    private static void AddDefaultNumericCondition(
        SerializedProperty definition)
    {
        SerializedProperty conditions = definition.FindPropertyRelative(
            NumericConditionsPropertyName);
        if (conditions != null && conditions.arraySize == 0)
        {
            SerializedProperty targetFaction =
                definition.FindPropertyRelative(TargetFactionPropertyName);
            bool targetsAllies = targetFaction != null &&
                targetFaction.enumValueIndex ==
                (int)CharacterTargetFaction.Ally;
            AddNumericCondition(
                conditions,
                targetsAllies
                    ? CharacterNumericConditionMetric.AttackPower
                    : CharacterNumericConditionMetric.Health);
        }
    }

    private static void AddNumericCondition(
        SerializedProperty conditions,
        CharacterNumericConditionMetric metric)
    {
        if (conditions == null)
            return;

        int newIndex = conditions.arraySize;
        conditions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(newIndex);
        SetEnumValue(
            condition,
            ConditionTypePropertyName,
            (int)CharacterConditionType.Numeric);
        SetEnumValue(
            condition,
            ConditionTargetPropertyName,
            (int)CharacterConditionTarget.ActionTarget);
        SetEnumValue(
            condition,
            NumericConditionMetricPropertyName,
            (int)metric);
        SetEnumValue(
            condition,
            NumericComparisonPropertyName,
            (int)CharacterNumericComparison.GreaterThanOrEqual);
        SerializedProperty threshold = condition.FindPropertyRelative(
            NumericThresholdPropertyName);
        if (threshold != null)
            threshold.floatValue = 0f;
        SerializedProperty statusEffect = condition.FindPropertyRelative(
            StatusEffectPropertyName);
        if (statusEffect != null)
            statusEffect.objectReferenceValue = null;
        SerializedProperty statusEffects = condition.FindPropertyRelative(
            ConditionStatusEffectsPropertyName);
        statusEffects?.ClearArray();
        SetEnumValue(
            condition,
            StatusSelectionScopePropertyName,
            (int)CharacterStatusSelectionScope.SelectedStatuses);
        SetEnumValue(
            condition,
            StatusConditionMatchModePropertyName,
            (int)CharacterStatusConditionMatchMode.Any);
        SerializedProperty statusMatchCount =
            condition.FindPropertyRelative(
                StatusConditionMatchCountPropertyName);
        if (statusMatchCount != null)
            statusMatchCount.intValue = 1;
    }

    private static void MigrateLegacyStatusCondition(
        SerializedProperty condition)
    {
        SerializedProperty conditionType =
            condition?.FindPropertyRelative(ConditionTypePropertyName);
        if (conditionType == null ||
            conditionType.enumValueIndex !=
            (int)CharacterConditionType.HasStatus)
        {
            return;
        }

        conditionType.enumValueIndex =
            (int)CharacterConditionType.Numeric;
        SetEnumValue(
            condition,
            NumericConditionMetricPropertyName,
            (int)CharacterNumericConditionMetric.StatusStackCount);
        SetEnumValue(
            condition,
            NumericComparisonPropertyName,
            (int)CharacterNumericComparison.GreaterThanOrEqual);
        SerializedProperty threshold = condition.FindPropertyRelative(
            NumericThresholdPropertyName);
        if (threshold != null)
            threshold.floatValue = 1f;
        GUI.changed = true;
    }

    private static void DrawMappedEnumPopup(
        SerializedProperty property,
        string label,
        string[] options,
        int[] enumValues)
    {
        if (property == null || options == null || enumValues == null ||
            options.Length == 0 || options.Length != enumValues.Length)
        {
            EditorGUILayout.HelpBox(
                $"{label} 선택지를 구성할 수 없습니다.",
                MessageType.Error);
            return;
        }

        int currentIndex = Array.IndexOf(
            enumValues,
            property.enumValueIndex);
        if (currentIndex < 0)
            currentIndex = 0;
        int selectedIndex = EditorGUILayout.Popup(
            label,
            currentIndex,
            options);
        property.enumValueIndex = enumValues[Mathf.Clamp(
            selectedIndex,
            0,
            enumValues.Length - 1)];
    }

    private static void DrawAttackEnumPopup(
        SerializedProperty property,
        string label,
        string[] options)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"{label} 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        int currentIndex = Mathf.Clamp(
            property.enumValueIndex,
            0,
            options.Length - 1);
        property.enumValueIndex = EditorGUILayout.Popup(
            label,
            currentIndex,
            options);
    }

    private static void DrawStatusRemovalTargetPopup(
        SerializedProperty property,
        string label)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"{label} 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        property.enumValueIndex = EditorGUILayout.IntPopup(
            label,
            property.enumValueIndex,
            StatusRemovalTargetOptions,
            StatusRemovalTargetValues);
    }

    private static void DrawActionAudioClip(SerializedProperty definition)
    {
        SerializedProperty audioClip = definition?.FindPropertyRelative(
            ActionAudioClipPropertyName);
        if (audioClip == null)
        {
            EditorGUILayout.HelpBox(
                "오디오 클립 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(
            audioClip,
            new GUIContent(
                "오디오 클립",
                "이 블록이 실제로 실행될 때 재생됩니다."));
    }

    private static void DrawActionIconSprite(
        SerializedProperty definition)
    {
        SerializedProperty iconSprite = definition?.FindPropertyRelative(
            ActionIconSpritePropertyName);
        if (iconSprite == null)
        {
            EditorGUILayout.HelpBox(
                "아이콘 Sprite 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(
            iconSprite,
            new GUIContent(
                "아이콘 Sprite",
                "인 게임 캐릭터 정보창의 패시브 또는 액티브 아이콘에 표시됩니다."));
    }

    private static void ResetActionIconSprite(
        SerializedProperty definition)
    {
        SerializedProperty iconSprite = definition?.FindPropertyRelative(
            ActionIconSpritePropertyName);
        if (iconSprite != null)
            iconSprite.objectReferenceValue = null;
    }

    private static void ResetActionAudioClip(SerializedProperty definition)
    {
        SerializedProperty audioClip = definition?.FindPropertyRelative(
            ActionAudioClipPropertyName);
        if (audioClip != null)
            audioClip.objectReferenceValue = null;
    }

    private void DrawAbility(SerializedProperty definition)
    {
        SerializedProperty effects = definition?.FindPropertyRelative(
            EffectsPropertyName);
        if (effects == null)
        {
            EditorGUILayout.HelpBox(
                "효과 목록 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        if (effects.arraySize > 0)
        {
            DrawEffectList(effects);
            return;
        }

        DrawLegacyAbility(definition);
        EditorGUILayout.Space(2f);
        if (GUILayout.Button(
                new GUIContent(
                    "효과 목록으로 전환",
                    "현재 단일 능력 설정을 조립식 효과 1개로 변환합니다.")))
        {
            ConvertLegacyAbilityToEffects(definition);
            GUI.changed = true;
        }
    }

    private void DrawLegacyAbility(SerializedProperty definition)
    {
        SerializedProperty damageType = definition.FindPropertyRelative(
            AttackDamageTypePropertyName);
        DrawAttackEnumPopup(
            damageType,
            "능력 종류",
            AttackDamageTypeOptions);
        bool appliesStatus = damageType != null &&
            damageType.enumValueIndex ==
            (int)CharacterAttackDamageType.StatusEffect;
        bool removesStatus = damageType != null &&
            damageType.enumValueIndex ==
            (int)CharacterAttackDamageType.StatusRemoval;
        if (appliesStatus)
            DrawStatusEffectSettings(definition);
        else if (removesStatus)
            DrawStatusRemovalSettings(definition);
        else
            DrawDamageAmount(definition);
    }

    private void DrawEffectList(SerializedProperty effects)
    {
        EditorGUILayout.LabelField("효과 목록", EditorStyles.boldLabel);
        int removeIndex = -1;
        int moveFromIndex = -1;
        int moveToIndex = -1;
        for (int index = 0; index < effects.arraySize; index++)
        {
            SerializedProperty effect = effects.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            effect.isExpanded = EditorGUILayout.Foldout(
                effect.isExpanded,
                $"효과 {index + 1}",
                true);
            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button(
                        new GUIContent("↑", "위로 이동"),
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                {
                    moveFromIndex = index;
                    moveToIndex = index - 1;
                }
            }
            using (new EditorGUI.DisabledScope(
                       index >= effects.arraySize - 1))
            {
                if (GUILayout.Button(
                        new GUIContent("↓", "아래로 이동"),
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                {
                    moveFromIndex = index;
                    moveToIndex = index + 1;
                }
            }
            using (new EditorGUI.DisabledScope(effects.arraySize <= 1))
            {
                if (GUILayout.Button(
                        new GUIContent(
                            "×",
                            effects.arraySize <= 1
                                ? "효과는 최소 1개가 필요합니다."
                                : "효과 삭제"),
                        EditorStyles.miniButton,
                        GUILayout.Width(24f)))
                {
                    removeIndex = index;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (effect.isExpanded)
                DrawEffect(effect);
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0 || moveFromIndex >= 0)
                break;
        }

        if (removeIndex >= 0)
        {
            effects.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }
        else if (moveFromIndex >= 0)
        {
            effects.MoveArrayElement(moveFromIndex, moveToIndex);
            GUI.changed = true;
        }

        if (GUILayout.Button(
                new GUIContent("+ 효과 추가", "새 피해 효과를 추가합니다.")))
        {
            AddDefaultEffect(effects);
            GUI.changed = true;
        }
    }

    private void DrawEffect(SerializedProperty effect)
    {
        SerializedProperty effectType = effect?.FindPropertyRelative(
            EffectTypePropertyName);
        if (effectType == null)
        {
            EditorGUILayout.HelpBox(
                "효과 종류 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        int previousType = effectType.enumValueIndex;
        DrawAttackEnumPopup(effectType, "효과 종류", EffectTypeOptions);
        if (effectType.enumValueIndex != previousType)
        {
            ResetEffectValues(
                effect,
                (CharacterEffectType)effectType.enumValueIndex);
        }

        CharacterEffectType selectedType =
            (CharacterEffectType)effectType.enumValueIndex;
        SerializedProperty targetMode = effect.FindPropertyRelative(
            EffectTargetModePropertyName);
        if (selectedType != CharacterEffectType.GainResource &&
            selectedType != CharacterEffectType.SpendResource &&
            selectedType != CharacterEffectType.SpendHealth)
        {
            if (targetMode != null)
            {
                DrawAttackEnumPopup(
                    targetMode,
                    "효과 대상",
                    EffectTargetModeOptions);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "효과 대상 속성을 찾을 수 없습니다.",
                MessageType.Error);
            }
        }

        SerializedProperty preconditionFailurePolicy =
            effect.FindPropertyRelative(
                EffectPreconditionFailurePolicyPropertyName);
        SerializedProperty failurePolicy = effect.FindPropertyRelative(
            EffectFailurePolicyPropertyName);
        if (preconditionFailurePolicy != null)
        {
            DrawAttackEnumPopup(
                preconditionFailurePolicy,
                "사전조건 실패",
                EffectPreconditionFailurePolicyOptions);
        }
        if (failurePolicy != null)
        {
            DrawAttackEnumPopup(
                failurePolicy,
                "실행 실패",
                EffectFailurePolicyOptions);
        }
        if (preconditionFailurePolicy != null &&
            preconditionFailurePolicy.enumValueIndex ==
            (int)CharacterEffectPreconditionFailurePolicy.SkipEffect)
        {
            EditorGUILayout.HelpBox(
                "대상이나 이후 예약 조건을 충족하지 못하면 이 효과만 " +
                "건너뛰며, 준비 가능한 다른 효과가 있어야 액션이 실행됩니다.",
                MessageType.Info);
        }

        if (selectedType == CharacterEffectType.Damage &&
            targetMode != null &&
            targetMode.enumValueIndex ==
            (int)CharacterEffectTargetMode.Source)
        {
            EditorGUILayout.HelpBox(
                "시전자 자신을 대상으로 하는 직접 피해는 현재 지원하지 않습니다.",
                MessageType.Warning);
        }
        else if (targetMode != null &&
                 targetMode.enumValueIndex ==
                 (int)CharacterEffectTargetMode.Source)
        {
            EditorGUILayout.HelpBox(
                "시전자 자신에게 직접 적용됩니다. 모든 효과가 이 방식이면 " +
                "행동 대상이 없어도 실행할 수 있습니다.",
                MessageType.Info);
        }
        else if (targetMode != null &&
                 targetMode.enumValueIndex ==
                 (int)CharacterEffectTargetMode.FreshSelection)
        {
            SerializedProperty targetSelector =
                effect.FindPropertyRelative(
                    EffectTargetSelectorPropertyName);
            if (targetSelector == null)
            {
                EditorGUILayout.HelpBox(
                    "별도 대상 선택 속성을 찾을 수 없습니다.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "별도 대상 선택",
                    EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "행동 대상과 별도로 준비 단계에서 한 번 선택하며, " +
                    "비용 차감 후에도 같은 대상을 사용합니다.",
                    MessageType.Info);
                DrawAttackSubject(
                    targetSelector,
                    true,
                    false);
                DrawNumericConditions(targetSelector);
            }
        }

        switch (selectedType)
        {
            case CharacterEffectType.ApplyStatus:
                DrawStatusEffectSettings(effect);
                break;
            case CharacterEffectType.RemoveStatus:
                DrawStatusRemovalSettings(
                    effect,
                    StatusEffectPropertyName);
                break;
            case CharacterEffectType.GainResource:
                DrawResourceGainAmount(effect);
                break;
            case CharacterEffectType.SpendResource:
                DrawResourceSpendAmount(effect);
                break;
            case CharacterEffectType.Heal:
                DrawHealAmount(effect);
                break;
            case CharacterEffectType.Shield:
                DrawShieldAmount(effect);
                break;
            case CharacterEffectType.SpendHealth:
                DrawHealthSpendAmount(effect);
                break;
            default:
                DrawAttackEnumPopup(
                    effect.FindPropertyRelative(
                        AttackDamageTypePropertyName),
                    "피해 종류",
                    DirectDamageTypeOptions);
                DrawDamageAmount(effect);
                break;
        }

        EditorGUILayout.LabelField("3D VFX 단계", EditorStyles.miniBoldLabel);
        SerializedProperty castVfx = effect.FindPropertyRelative(
            CastVfxCuePropertyName);
        if (castVfx != null)
        {
            EditorGUILayout.PropertyField(
                castVfx,
                new GUIContent(
                    "시전 VFX 큐",
                    "효과가 성공하면 소스 위치에서 한 번 재생합니다."));
        }

        SerializedProperty projectileVfx = effect.FindPropertyRelative(
            ProjectileVfxCuePropertyName);
        if (projectileVfx != null)
        {
            EditorGUILayout.PropertyField(
                projectileVfx,
                new GUIContent(
                    "투사체 VFX 큐",
                    "효과가 성공한 각 대상까지 이동시키는 3D VFX 큐입니다."));
            if (projectileVfx.objectReferenceValue is BattleVfxCueSO cue &&
                !cue.HasMotion)
            {
                EditorGUILayout.HelpBox(
                    "선택한 투사체 Cue의 이동 방식이 Stationary입니다. " +
                    "Battle VFX Editor에서 Linear 또는 Arc를 선택해야 실제로 이동합니다.",
                    MessageType.Warning);
            }
        }

        SerializedProperty impactVfx = effect.FindPropertyRelative(
            ImpactVfxCuePropertyName);
        if (impactVfx != null)
        {
            EditorGUILayout.PropertyField(
                impactVfx,
                new GUIContent(
                    "적중 VFX 큐",
                    "이 효과가 실제로 성공한 대상마다 재생할 3D VFX 큐입니다."));
        }
    }

    private static void AddDefaultEffect(SerializedProperty effects)
    {
        if (effects == null)
            return;

        int newIndex = effects.arraySize;
        effects.InsertArrayElementAtIndex(newIndex);
        SerializedProperty effect = effects.GetArrayElementAtIndex(newIndex);
        ResetEffectValues(effect, CharacterEffectType.Damage);
        effect.isExpanded = true;
    }

    private static void ConvertLegacyAbilityToEffects(
        SerializedProperty definition)
    {
        SerializedProperty effects = definition?.FindPropertyRelative(
            EffectsPropertyName);
        SerializedProperty legacyDamageType =
            definition?.FindPropertyRelative(AttackDamageTypePropertyName);
        if (effects == null || legacyDamageType == null)
            return;

        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        CharacterAttackDamageType damageType =
            (CharacterAttackDamageType)legacyDamageType.enumValueIndex;
        switch (damageType)
        {
            case CharacterAttackDamageType.StatusEffect:
                ResetEffectValues(effect, CharacterEffectType.ApplyStatus);
                CopyFloatProperty(
                    definition,
                    effect,
                    StatusDurationPropertyName);
                CopyFloatProperty(
                    definition,
                    effect,
                    StatusStacksPropertyName);
                CopyObjectReferenceProperty(
                    definition,
                    StatusEffectPropertyName,
                    effect,
                    StatusEffectPropertyName);
                break;

            case CharacterAttackDamageType.StatusRemoval:
                ResetEffectValues(effect, CharacterEffectType.RemoveStatus);
                CopyEnumProperty(
                    definition,
                    effect,
                    StatusRemovalTargetPropertyName);
                CopyEnumProperty(
                    definition,
                    effect,
                    StatusRemovalPickModePropertyName);
                CopyIntProperty(
                    definition,
                    effect,
                    StatusRemovalPickCountPropertyName);
                CopyEnumProperty(
                    definition,
                    effect,
                    StatusRemovalAmountModePropertyName);
                CopyIntProperty(
                    definition,
                    effect,
                    StatusRemovalCountPropertyName);
                CopyFloatProperty(
                    definition,
                    effect,
                    StatusRemovalRatioPropertyName);
                CopyObjectReferenceProperty(
                    definition,
                    StatusRemovalEffectPropertyName,
                    effect,
                    StatusEffectPropertyName);
                break;

            default:
                ResetEffectValues(effect, CharacterEffectType.Damage);
                CopyEnumProperty(
                    definition,
                    effect,
                    AttackDamageTypePropertyName);
                CopyEnumProperty(
                    definition,
                    effect,
                    DamageAmountModePropertyName);
                CopyFloatProperty(
                    definition,
                    effect,
                    DamageAmountPropertyName);
                break;
        }

        effect.isExpanded = true;
    }

    private static void ResetEffectValues(
        SerializedProperty effect,
        CharacterEffectType effectType)
    {
        if (effect == null)
            return;

        SetEnumValue(
            effect,
            EffectTypePropertyName,
            (int)effectType);
        SetEnumValue(
            effect,
            EffectTargetModePropertyName,
            (int)CharacterEffectTargetMode.InheritAction);
        SetEnumValue(
            effect,
            EffectPreconditionFailurePolicyPropertyName,
            (int)CharacterEffectPreconditionFailurePolicy.AbortAction);
        SetEnumValue(
            effect,
            EffectFailurePolicyPropertyName,
            (int)CharacterEffectFailurePolicy.Continue);
        SerializedProperty targetSelector = effect.FindPropertyRelative(
            EffectTargetSelectorPropertyName);
        if (targetSelector != null)
        {
            SetEnumValue(
                targetSelector,
                TargetFactionPropertyName,
                (int)CharacterTargetFaction.Enemy);
            SetEnumValue(
                targetSelector,
                AttackSubjectPropertyName,
                (int)CharacterAttackSubject.Random);
            SetEnumValue(
                targetSelector,
                AttackSubjectMetricPropertyName,
                (int)CharacterAttackSubjectMetric.Health);
            SerializedProperty selectorCount =
                targetSelector.FindPropertyRelative(
                    AttackSubjectCountPropertyName);
            if (selectorCount != null)
                selectorCount.intValue = 1;
            ClearNumericConditions(targetSelector);
            ClearAreaOffsets(targetSelector);
        }
        SetEnumValue(
            effect,
            AttackDamageTypePropertyName,
            (int)CharacterAttackDamageType.Physical);
        SetEnumValue(
            effect,
            DamageAmountModePropertyName,
            effectType == CharacterEffectType.GainResource ||
            effectType == CharacterEffectType.SpendResource ||
            effectType == CharacterEffectType.SpendHealth
                ? (int)CharacterDamageAmountMode.Fixed
                : (int)CharacterDamageAmountMode.Ratio);
        SerializedProperty damageAmount = effect.FindPropertyRelative(
            DamageAmountPropertyName);
        if (damageAmount != null)
            damageAmount.floatValue = 1f;
        SerializedProperty sourceResourceScale =
            effect.FindPropertyRelative(SourceResourceScalePropertyName);
        if (sourceResourceScale != null)
            sourceResourceScale.floatValue = 0f;
        SetFloatValue(
            effect,
            TargetCurrentHealthScalePropertyName,
            0f);
        SetFloatValue(
            effect,
            TargetMaxHealthScalePropertyName,
            0f);
        SetObjectReferenceValue(
            effect,
            SourceStatusScalingEffectPropertyName,
            null);
        SetFloatValue(
            effect,
            SourceStatusStacksScalePropertyName,
            0f);
        SetObjectReferenceValue(
            effect,
            TargetStatusScalingEffectPropertyName,
            null);
        SetFloatValue(
            effect,
            TargetStatusStacksScalePropertyName,
            0f);
        effect.FindPropertyRelative(
            StatusContributionMultipliersPropertyName)?.ClearArray();

        SerializedProperty duration = effect.FindPropertyRelative(
            StatusDurationPropertyName);
        if (duration != null)
            duration.floatValue = 1f;
        SerializedProperty stacks = effect.FindPropertyRelative(
            StatusStacksPropertyName);
        if (stacks != null)
            stacks.floatValue = 1f;
        SerializedProperty statusEffect = effect.FindPropertyRelative(
            StatusEffectPropertyName);
        if (statusEffect != null)
        {
            statusEffect.objectReferenceValue =
                effectType == CharacterEffectType.ApplyStatus ||
                effectType == CharacterEffectType.RemoveStatus
                    ? StatusEffectDefinitionCatalog.FindById(
                        StatusEffectIds.Fire)
                    : null;
        }

        SetEnumValue(
            effect,
            StatusRemovalTargetPropertyName,
            (int)CharacterStatusRemovalTarget.Single);
        SetEnumValue(
            effect,
            StatusRemovalPickModePropertyName,
            (int)CharacterStatusRemovalPickMode.AllMatches);
        SerializedProperty removalPickCount = effect.FindPropertyRelative(
            StatusRemovalPickCountPropertyName);
        if (removalPickCount != null)
            removalPickCount.intValue = 1;
        effect.FindPropertyRelative(
            StatusRemovalEffectsPropertyName)?.ClearArray();
        SetEnumValue(
            effect,
            StatusRemovalAmountModePropertyName,
            (int)CharacterStatusRemovalAmountMode.FixedStacks);
        SerializedProperty removalCount = effect.FindPropertyRelative(
            StatusRemovalCountPropertyName);
        if (removalCount != null)
            removalCount.intValue = 0;
        SetFloatValue(
            effect,
            StatusRemovalRatioPropertyName,
            0.5f);
        SetObjectReferenceValue(
            effect,
            CastVfxCuePropertyName,
            null);
        SetObjectReferenceValue(
            effect,
            ProjectileVfxCuePropertyName,
            null);
        SetObjectReferenceValue(
            effect,
            ImpactVfxCuePropertyName,
            null);
    }

    private static void ResetExplicitEffects(
        SerializedProperty definition)
    {
        SerializedProperty effects = definition?.FindPropertyRelative(
            EffectsPropertyName);
        if (effects == null)
            return;

        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ResetEffectValues(effect, CharacterEffectType.Damage);
        effect.isExpanded = true;
    }

    private static void ClearExplicitEffects(SerializedProperty definition)
    {
        definition?.FindPropertyRelative(EffectsPropertyName)?.ClearArray();
    }

    private static void CopyEnumProperty(
        SerializedProperty source,
        SerializedProperty destination,
        string propertyName)
    {
        SerializedProperty sourceProperty =
            source?.FindPropertyRelative(propertyName);
        SerializedProperty destinationProperty =
            destination?.FindPropertyRelative(propertyName);
        if (sourceProperty != null && destinationProperty != null)
        {
            destinationProperty.enumValueIndex =
                sourceProperty.enumValueIndex;
        }
    }

    private static void CopyFloatProperty(
        SerializedProperty source,
        SerializedProperty destination,
        string propertyName)
    {
        SerializedProperty sourceProperty =
            source?.FindPropertyRelative(propertyName);
        SerializedProperty destinationProperty =
            destination?.FindPropertyRelative(propertyName);
        if (sourceProperty != null && destinationProperty != null)
            destinationProperty.floatValue = sourceProperty.floatValue;
    }

    private static void CopyIntProperty(
        SerializedProperty source,
        SerializedProperty destination,
        string propertyName)
    {
        SerializedProperty sourceProperty =
            source?.FindPropertyRelative(propertyName);
        SerializedProperty destinationProperty =
            destination?.FindPropertyRelative(propertyName);
        if (sourceProperty != null && destinationProperty != null)
            destinationProperty.intValue = sourceProperty.intValue;
    }

    private static void CopyObjectReferenceProperty(
        SerializedProperty source,
        string sourcePropertyName,
        SerializedProperty destination,
        string destinationPropertyName)
    {
        SerializedProperty sourceProperty =
            source?.FindPropertyRelative(sourcePropertyName);
        SerializedProperty destinationProperty =
            destination?.FindPropertyRelative(destinationPropertyName);
        if (sourceProperty != null && destinationProperty != null)
        {
            destinationProperty.objectReferenceValue =
                sourceProperty.objectReferenceValue;
        }
    }

    private void DrawDamageAmount(SerializedProperty definition)
    {
        SerializedProperty damageAmountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty damageAmount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (damageAmountMode == null || damageAmount == null)
        {
            EditorGUILayout.HelpBox(
                "피해 량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            damageAmountMode,
            "계산 방식",
            DamageAmountModeOptions);

        bool isFixed = damageAmountMode.enumValueIndex ==
                       (int)CharacterDamageAmountMode.Fixed;
        damageAmount.floatValue = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                isFixed ? "고정 피해" : "공격력 배율",
                damageAmount.floatValue));

        if (!isFixed)
        {
            SerializedProperty attackPower =
                _serializedCharacter.FindProperty("attackPower");
            float characterAttackPower = attackPower != null
                ? attackPower.intValue
                : _selectedCharacter.AttackPower;
            float finalAttackPower =
                characterAttackPower * damageAmount.floatValue;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(
                    new GUIContent(
                        "최종 공격력",
                        "캐릭터 공격력 × 공격력 배율"),
                    finalAttackPower);
            }
        }

        DrawSourceResourceScale(definition);
        DrawStatusStackScale(
            definition,
            SourceStatusScalingEffectPropertyName,
            SourceStatusStacksScalePropertyName,
            "시전자 상태",
            "시전자에게 적용된 지정 상태의 스택 수 × 배율");
        DrawTargetHealthScales(definition);
        DrawStatusStackScale(
            definition,
            TargetStatusScalingEffectPropertyName,
            TargetStatusStacksScalePropertyName,
            "대상 상태",
            "각 대상에게 적용된 지정 상태의 스택 수 × 배율");
        SerializedProperty contributionMultipliers =
            definition.FindPropertyRelative(
                StatusContributionMultipliersPropertyName);
        if (contributionMultipliers != null)
        {
            DrawStatusContributionMultipliers(
                contributionMultipliers,
                true);
        }
    }

    private static void DrawResourceGainAmount(
        SerializedProperty definition)
    {
        SerializedProperty amountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty amount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (amountMode == null || amount == null)
        {
            EditorGUILayout.HelpBox(
                "자원 획득량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            amountMode,
            "기본 계산 방식",
            DamageAmountModeOptions);
        bool isFixed = amountMode.enumValueIndex ==
                       (int)CharacterDamageAmountMode.Fixed;
        amount.floatValue = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                isFixed ? "고정 획득량" : "공격력 배율",
                amount.floatValue));
        DrawSourceResourceScale(definition);
        DrawStatusStackScale(
            definition,
            SourceStatusScalingEffectPropertyName,
            SourceStatusStacksScalePropertyName,
            "시전자 상태",
            "시전자에게 적용된 지정 상태의 스택 수 × 배율");
        SerializedProperty contributionMultipliers =
            definition.FindPropertyRelative(
                StatusContributionMultipliersPropertyName);
        if (contributionMultipliers != null)
        {
            DrawStatusContributionMultipliers(
                contributionMultipliers,
                true);
        }
    }

    private static void DrawResourceSpendAmount(
        SerializedProperty definition)
    {
        SerializedProperty amountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty amount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (amountMode == null || amount == null)
        {
            EditorGUILayout.HelpBox(
                "자원 소비량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        amountMode.enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        amount.floatValue = Mathf.Max(
            1f,
            Mathf.Round(
                EditorGUILayout.FloatField(
                    "고정 소비량",
                    amount.floatValue)));
        EditorGUILayout.HelpBox(
            "스킬 기본 비용과 함께 준비 단계에서 누적 예약되며, " +
            "이 효과의 실행 순서에 도달했을 때 실제로 차감됩니다. " +
            "같은 액션의 자원 획득 효과는 예약 가능량에 포함되지 않습니다.",
            MessageType.Info);
    }

    private static void DrawHealAmount(
        SerializedProperty definition)
    {
        SerializedProperty amountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty amount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (amountMode == null || amount == null)
        {
            EditorGUILayout.HelpBox(
                "체력 회복량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            amountMode,
            "기본 계산 방식",
            DamageAmountModeOptions);
        bool isFixed = amountMode.enumValueIndex ==
                       (int)CharacterDamageAmountMode.Fixed;
        amount.floatValue = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                isFixed ? "고정 회복량" : "공격력 배율",
                amount.floatValue));
        DrawSourceResourceScale(definition);
        DrawStatusStackScale(
            definition,
            SourceStatusScalingEffectPropertyName,
            SourceStatusStacksScalePropertyName,
            "시전자 상태",
            "시전자에게 적용된 지정 상태의 스택 수 × 배율");
        DrawTargetHealthScales(definition);
        DrawStatusStackScale(
            definition,
            TargetStatusScalingEffectPropertyName,
            TargetStatusStacksScalePropertyName,
            "대상 상태",
            "각 대상에게 적용된 지정 상태의 스택 수 × 배율");
        SerializedProperty contributionMultipliers =
            definition.FindPropertyRelative(
                StatusContributionMultipliersPropertyName);
        if (contributionMultipliers != null)
        {
            DrawStatusContributionMultipliers(
                contributionMultipliers,
                true);
        }
    }

    private static void DrawHealthSpendAmount(
        SerializedProperty definition)
    {
        SerializedProperty amountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty amount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (amountMode == null || amount == null)
        {
            EditorGUILayout.HelpBox(
                "체력 소비량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        amountMode.enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        amount.floatValue = Mathf.Max(
            1f,
            Mathf.Round(
                EditorGUILayout.FloatField(
                    "고정 소비량",
                    amount.floatValue)));
        EditorGUILayout.HelpBox(
            "시전자 체력을 최소 1 남기는 범위에서 준비 단계에 누적 " +
            "예약되며, 이 효과의 실행 순서에 도달했을 때 차감됩니다. " +
            "같은 액션의 회복 효과는 예약 가능량에 포함되지 않습니다.",
            MessageType.Info);
    }

    private static void DrawShieldAmount(
        SerializedProperty definition)
    {
        SerializedProperty amountMode = definition.FindPropertyRelative(
            DamageAmountModePropertyName);
        SerializedProperty amount = definition.FindPropertyRelative(
            DamageAmountPropertyName);
        if (amountMode == null || amount == null)
        {
            EditorGUILayout.HelpBox(
                "보호막 부여량 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawAttackEnumPopup(
            amountMode,
            "기본 계산 방식",
            DamageAmountModeOptions);
        bool isFixed = amountMode.enumValueIndex ==
                       (int)CharacterDamageAmountMode.Fixed;
        amount.floatValue = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                isFixed ? "고정 보호막" : "공격력 배율",
                amount.floatValue));
        DrawSourceResourceScale(definition);
        DrawStatusStackScale(
            definition,
            SourceStatusScalingEffectPropertyName,
            SourceStatusStacksScalePropertyName,
            "시전자 상태",
            "시전자에게 적용된 지정 상태의 스택 수 × 배율");
        DrawTargetHealthScales(definition);
        DrawStatusStackScale(
            definition,
            TargetStatusScalingEffectPropertyName,
            TargetStatusStacksScalePropertyName,
            "대상 상태",
            "각 대상에게 적용된 지정 상태의 스택 수 × 배율");
        SerializedProperty contributionMultipliers =
            definition.FindPropertyRelative(
                StatusContributionMultipliersPropertyName);
        if (contributionMultipliers != null)
        {
            DrawStatusContributionMultipliers(
                contributionMultipliers,
                true);
        }
        EditorGUILayout.HelpBox(
            "보호막은 대상별로 누적되며 피해보다 먼저 소모됩니다. " +
            "전투 리셋 시 남은 보호막은 제거됩니다.",
            MessageType.Info);
    }

    private static void DrawSourceResourceScale(
        SerializedProperty definition)
    {
        SerializedProperty sourceResourceScale =
            definition.FindPropertyRelative(
                SourceResourceScalePropertyName);
        if (sourceResourceScale == null)
            return;

        sourceResourceScale.floatValue = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                new GUIContent(
                    "현재 자원 배율",
                    "효과 실행 시점의 현재 액티브 스킬 자원 × 배율"),
                sourceResourceScale.floatValue));
    }

    private static void DrawTargetHealthScales(
        SerializedProperty definition)
    {
        SerializedProperty currentHealthScale =
            definition.FindPropertyRelative(
                TargetCurrentHealthScalePropertyName);
        SerializedProperty maximumHealthScale =
            definition.FindPropertyRelative(
                TargetMaxHealthScalePropertyName);
        if (currentHealthScale == null || maximumHealthScale == null)
            return;

        currentHealthScale.floatValue = EditorGUILayout.FloatField(
            new GUIContent(
                "대상 현재 체력 배율",
                "각 대상의 효과 실행 직전 현재 체력 × 배율. " +
                "음수 입력을 허용합니다."),
            currentHealthScale.floatValue);
        maximumHealthScale.floatValue = EditorGUILayout.FloatField(
            new GUIContent(
                "대상 최대 체력 배율",
                "각 대상의 최대 체력 × 배율. 현재 체력 배율을 음수로 " +
                "설정하면 잃은 체력 기반 식을 만들 수 있습니다."),
            maximumHealthScale.floatValue);
    }

    private static void DrawStatusStackScale(
        SerializedProperty definition,
        string statusPropertyName,
        string scalePropertyName,
        string label,
        string tooltip)
    {
        SerializedProperty statusEffect =
            definition.FindPropertyRelative(statusPropertyName);
        SerializedProperty stacksScale =
            definition.FindPropertyRelative(scalePropertyName);
        if (statusEffect == null || stacksScale == null)
            return;

        PS260714StatusEffectSelection.DrawSingle(
            statusEffect,
            new GUIContent($"{label} 기준", tooltip),
            new PS260714StatusEffectSelectionOptions(
                allowNone: true));
        stacksScale.floatValue = EditorGUILayout.FloatField(
            new GUIContent($"{label} 스택 배율", tooltip),
            stacksScale.floatValue);
        if (stacksScale.floatValue != 0f &&
            statusEffect.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                $"{label} 스택 배율을 사용하려면 StatusEffectSO를 " +
                "지정해야 합니다.",
                MessageType.Error);
        }
    }

    private static void DrawStatusEffectSettings(
        SerializedProperty definition)
    {
        SerializedProperty duration = definition.FindPropertyRelative(
            StatusDurationPropertyName);
        SerializedProperty stacks = definition.FindPropertyRelative(
            StatusStacksPropertyName);
        SerializedProperty statusEffect = definition.FindPropertyRelative(
            StatusEffectPropertyName);
        if (duration == null || stacks == null || statusEffect == null)
        {
            EditorGUILayout.HelpBox(
                "상태 부여 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        PS260714StatusEffectSelection.DrawSingle(
            statusEffect,
            new GUIContent(
                "부여 상태",
                "상태 효과 에디터에서 만든 StatusEffectSO를 선택합니다."));

        StatusEffectSO selectedStatus =
            statusEffect.objectReferenceValue as StatusEffectSO;
        if (selectedStatus == null)
        {
            EditorGUILayout.HelpBox(
                "부여할 StatusEffectSO를 선택하세요.",
                MessageType.Error);
        }

        if (selectedStatus == null || selectedStatus.DurationMode ==
            StatusEffectDurationMode.Timed)
        {
            duration.floatValue = TimePrecision.Normalize(
                EditorGUILayout.FloatField("시간 (초)", duration.floatValue),
                0.1f);
        }
        else
        {
            EditorGUILayout.LabelField("시간", "영구");
        }

        stacks.floatValue = Mathf.Max(
            0.1f,
            EditorGUILayout.FloatField(
                new GUIContent(
                    "스택",
                    "부여할 상태의 스택 수입니다."),
                stacks.floatValue));
    }

    private static void ResetStatusEffectValues(
        SerializedProperty definition)
    {
        SerializedProperty duration = definition?.FindPropertyRelative(
            StatusDurationPropertyName);
        if (duration != null)
            duration.floatValue = 1f;

        SerializedProperty stacks = definition?.FindPropertyRelative(
            StatusStacksPropertyName);
        if (stacks != null)
            stacks.floatValue = 1f;

        SerializedProperty appliedEffect = definition?.FindPropertyRelative(
            StatusEffectPropertyName);
        if (appliedEffect != null)
        {
            appliedEffect.objectReferenceValue =
                StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        }

        SetEnumValue(
            definition,
            StatusRemovalTargetPropertyName,
            (int)CharacterStatusRemovalTarget.Single);
        SetEnumValue(
            definition,
            StatusRemovalPickModePropertyName,
            (int)CharacterStatusRemovalPickMode.AllMatches);
        SerializedProperty removalPickCount =
            definition?.FindPropertyRelative(
                StatusRemovalPickCountPropertyName);
        if (removalPickCount != null)
            removalPickCount.intValue = 1;
        SetEnumValue(
            definition,
            StatusRemovalAmountModePropertyName,
            (int)CharacterStatusRemovalAmountMode.FixedStacks);
        SerializedProperty removalEffect = definition?.FindPropertyRelative(
            StatusRemovalEffectPropertyName);
        if (removalEffect != null)
        {
            removalEffect.objectReferenceValue =
                StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        }
        SerializedProperty removalCount = definition?.FindPropertyRelative(
            StatusRemovalCountPropertyName);
        if (removalCount != null)
            removalCount.intValue = 0;
        SetFloatValue(
            definition,
            StatusRemovalRatioPropertyName,
            0.5f);
    }

    private static void DrawStatusRemovalSettings(
        SerializedProperty definition)
    {
        DrawStatusRemovalSettings(
            definition,
            StatusRemovalEffectPropertyName);
    }

    private static void DrawStatusRemovalSettings(
        SerializedProperty definition,
        string statusEffectPropertyName)
    {
        SerializedProperty removalTarget = definition.FindPropertyRelative(
            StatusRemovalTargetPropertyName);
        SerializedProperty removalAmountMode =
            definition.FindPropertyRelative(
                StatusRemovalAmountModePropertyName);
        SerializedProperty removalPickMode =
            definition.FindPropertyRelative(
                StatusRemovalPickModePropertyName);
        SerializedProperty removalPickCount =
            definition.FindPropertyRelative(
                StatusRemovalPickCountPropertyName);
        SerializedProperty removalCount = definition.FindPropertyRelative(
            StatusRemovalCountPropertyName);
        SerializedProperty removalRatio = definition.FindPropertyRelative(
            StatusRemovalRatioPropertyName);
        SerializedProperty removalEffect = definition.FindPropertyRelative(
            statusEffectPropertyName);
        SerializedProperty removalEffects = definition.FindPropertyRelative(
            StatusRemovalEffectsPropertyName);
        if (removalTarget == null || removalPickMode == null ||
            removalPickCount == null || removalAmountMode == null ||
            removalCount == null || removalRatio == null ||
            removalEffect == null)
        {
            EditorGUILayout.HelpBox(
                "상태 제거 속성을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        DrawStatusRemovalTargetPopup(removalTarget, "제거 대상");
        if (removalTarget.enumValueIndex ==
            (int)CharacterStatusRemovalTarget.Random)
        {
            removalPickMode.enumValueIndex =
                (int)CharacterStatusRemovalPickMode.RandomCount;
        }
        else
        {
            DrawAttackEnumPopup(
                removalPickMode,
                "상태 종류 선택",
                StatusRemovalPickModeOptions);
        }
        if (removalPickMode.enumValueIndex ==
            (int)CharacterStatusRemovalPickMode.RandomCount)
        {
            removalPickCount.intValue = Mathf.Max(
                1,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "선택할 상태 종류",
                        "후보 상태 종류 중 중복 없이 무작위로 선택합니다."),
                    removalPickCount.intValue));
        }
        if (removalTarget.enumValueIndex ==
            (int)CharacterStatusRemovalTarget.Single)
        {
            if (removalEffects != null)
            {
                PS260714StatusEffectSelection.Draw(
                    removalEffects,
                    removalEffect,
                    new GUIContent(
                        "제거 상태",
                        "여러 상태를 선택할 수 있으며 분류와 검색으로 목록을 필터링합니다."),
                    new PS260714StatusEffectSelectionOptions(
                        requireRemovable: true));
            }
            else
            {
                EditorGUILayout.PropertyField(
                    removalEffect,
                    new GUIContent(
                        "제거 상태",
                        "프로젝트에 생성된 모든 StatusEffectSO를 선택할 수 있습니다."));
                if (removalEffect.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "지정 상태 제거에 사용할 StatusEffectSO를 선택하세요.",
                        MessageType.Error);
                }
                EditorGUILayout.HelpBox(
                    "복수 상태 선택은 효과 목록으로 전환한 뒤 사용할 수 있습니다.",
                    MessageType.Info);
            }
        }

        DrawAttackEnumPopup(
            removalAmountMode,
            "제거량 방식",
            StatusRemovalAmountModeOptions);
        if (removalAmountMode.enumValueIndex ==
            (int)CharacterStatusRemovalAmountMode.CurrentStacksRatio)
        {
            float percentage = EditorGUILayout.Slider(
                new GUIContent(
                    "현재 스택 비율 (%)",
                    "대상이 현재 보유한 스택을 기준으로 계산하며 소수점은 올림합니다."),
                removalRatio.floatValue * 100f,
                1f,
                100f);
            removalRatio.floatValue = percentage * 0.01f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("빠른 선택", GUILayout.Width(75f));
            if (GUILayout.Button("1/4", EditorStyles.miniButton))
                removalRatio.floatValue = 0.25f;
            if (GUILayout.Button("1/2", EditorStyles.miniButton))
                removalRatio.floatValue = 0.5f;
            if (GUILayout.Button("3/4", EditorStyles.miniButton))
                removalRatio.floatValue = 0.75f;
            if (GUILayout.Button("전체", EditorStyles.miniButton))
                removalRatio.floatValue = 1f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "현재 스택 × 비율의 소수점은 올림합니다. " +
                "예: 3스택의 1/2은 2스택 제거. 전체 범위는 상태 종류마다 각각 계산합니다.",
                MessageType.Info);
        }
        else
        {
            removalCount.intValue = Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "제거 스택",
                        "0이면 대상 상태를 전부 제거하고, 1 이상이면 입력한 수만큼 제거합니다."),
                    removalCount.intValue));
            EditorGUILayout.HelpBox(
                "고정 스택 0: 해당 범위의 상태 전부 제거\n" +
                "고정 스택 1 이상: 입력한 수만큼 상태 스택 제거",
                MessageType.Info);
        }
    }

    private void AddAttackDefinition()
    {
        if (_selectedCharacter == null || _serializedCharacter == null)
            return;

        _serializedCharacter.UpdateIfRequiredOrScript();
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            AttackDefinitionsPropertyName);
        if (definitions == null)
            return;

        int newIndex = definitions.arraySize;
        definitions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty definition =
            definitions.GetArrayElementAtIndex(newIndex);
        SerializedProperty sections = definition.FindPropertyRelative(
            AttackSectionsPropertyName);
        sections?.ClearArray();
        ResetAttackDefinitionValues(definition);
        definition.isExpanded = true;

        if (_serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedCharacter);

        _attackExpanded = true;
    }

    private static void ResetAttackDefinitionValues(
        SerializedProperty definition)
    {
        definition.FindPropertyRelative(AreaOffsetsPropertyName)?.ClearArray();
        ResetActionAudioClip(definition);
        ResetAttackSectionValue(
            definition,
            CharacterAttackSectionType.Linkage);
        ResetAttackSectionValue(
            definition,
            CharacterAttackSectionType.Condition);
        ResetAttackSectionValue(
            definition,
            CharacterAttackSectionType.Subject);
        ResetAttackSectionValue(
            definition,
            CharacterAttackSectionType.Ability);
        ResetExplicitEffects(definition);
    }

    private static void ResetAttackSectionValue(
        SerializedProperty definition,
        CharacterAttackSectionType sectionType)
    {
        switch (sectionType)
        {
            case CharacterAttackSectionType.Linkage:
                SetEnumValue(
                    definition,
                    ActionLinkagePropertyName,
                    (int)CharacterActionLinkage.PreviousAttackSucceeded);
                break;

            case CharacterAttackSectionType.Condition:
                ClearNumericConditions(definition);
                break;

            case CharacterAttackSectionType.Subject:
                SetEnumValue(
                    definition,
                    TargetFactionPropertyName,
                    (int)CharacterTargetFaction.Enemy);
                SetEnumValue(
                    definition,
                    AttackSubjectPropertyName,
                    (int)CharacterAttackSubject.Random);
                SetEnumValue(
                    definition,
                    AttackTargetRetentionModePropertyName,
                    (int)CharacterAttackTargetRetentionMode
                        .ReselectEachAttack);
                SerializedProperty subjectCount =
                    definition.FindPropertyRelative(
                        AttackSubjectCountPropertyName);
                if (subjectCount != null)
                    subjectCount.intValue = 1;
                SetEnumValue(
                    definition,
                    AttackSubjectMetricPropertyName,
                    (int)CharacterAttackSubjectMetric.Health);
                ClearAreaOffsets(definition);
                break;

            case CharacterAttackSectionType.Ability:
                SetEnumValue(
                    definition,
                    AttackDamageTypePropertyName,
                    (int)CharacterAttackDamageType.Physical);
                SetEnumValue(
                    definition,
                    DamageAmountModePropertyName,
                    (int)CharacterDamageAmountMode.Ratio);
                SerializedProperty damageAmount =
                    definition.FindPropertyRelative(
                        DamageAmountPropertyName);
                if (damageAmount != null)
                    damageAmount.floatValue = 1f;
                ResetStatusEffectValues(definition);
                ClearExplicitEffects(definition);
                break;
        }
    }

    private static void SetEnumValue(
        SerializedProperty definition,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            definition.FindPropertyRelative(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetFloatValue(
        SerializedProperty definition,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            definition.FindPropertyRelative(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetObjectReferenceValue(
        SerializedProperty definition,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            definition.FindPropertyRelative(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void ClearAreaOffsets(SerializedProperty definition)
    {
        definition?.FindPropertyRelative(AreaOffsetsPropertyName)?.ClearArray();
    }

    private void ShowAttackSectionMenu(int attackIndex)
    {
        if (_selectedCharacter == null)
            return;

        CharacterSO character = _selectedCharacter;
        SerializedObject serializedCharacter =
            new SerializedObject(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            AttackDefinitionsPropertyName);
        if (definitions == null ||
            attackIndex < 0 ||
            attackIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(attackIndex)
            .FindPropertyRelative(AttackSectionsPropertyName);
        GenericMenu menu = new();
        foreach (CharacterAttackSectionType sectionType in AttackSectionOrder)
        {
            CharacterAttackSectionType capturedType = sectionType;
            GUIContent label = new(GetAttackSectionLabel(sectionType));
            if (attackIndex == 0 &&
                sectionType == CharacterAttackSectionType.Linkage)
            {
                menu.AddDisabledItem(
                    new GUIContent("1. 연동 (첫 공격에는 추가할 수 없음)"));
                continue;
            }
            if (FindAttackSectionIndex(sections, sectionType) >= 0)
            {
                menu.AddDisabledItem(label, true);
                continue;
            }

            menu.AddItem(
                label,
                false,
                () => AddAttackSection(
                    character,
                    attackIndex,
                    capturedType));
        }

        menu.ShowAsContext();
    }

    private void AddAttackSection(
        CharacterSO character,
        int attackIndex,
        CharacterAttackSectionType sectionType)
    {
        if (character == null)
            return;

        SerializedObject serializedCharacter = new(character);
        SerializedProperty definitions = serializedCharacter.FindProperty(
            AttackDefinitionsPropertyName);
        if (definitions == null ||
            attackIndex < 0 ||
            attackIndex >= definitions.arraySize)
        {
            return;
        }

        SerializedProperty sections = definitions
            .GetArrayElementAtIndex(attackIndex)
            .FindPropertyRelative(AttackSectionsPropertyName);
        if (sections == null ||
            FindAttackSectionIndex(sections, sectionType) >= 0)
        {
            return;
        }

        int newIndex = sections.arraySize;
        sections.InsertArrayElementAtIndex(newIndex);
        SerializedProperty section = sections.GetArrayElementAtIndex(newIndex);
        section.enumValueIndex = (int)sectionType;
        ResetAttackSectionValue(
            definitions.GetArrayElementAtIndex(attackIndex),
            sectionType);
        if (sectionType == CharacterAttackSectionType.Ability)
        {
            ResetExplicitEffects(
                definitions.GetArrayElementAtIndex(attackIndex));
        }
        if (sectionType == CharacterAttackSectionType.Condition)
        {
            AddDefaultNumericCondition(
                definitions.GetArrayElementAtIndex(attackIndex));
        }

        if (serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(character);

        Repaint();
    }

    private static int FindAttackSectionIndex(
        SerializedProperty sections,
        CharacterAttackSectionType sectionType)
    {
        if (sections == null)
            return -1;

        for (int index = 0; index < sections.arraySize; index++)
        {
            if (sections.GetArrayElementAtIndex(index).enumValueIndex ==
                (int)sectionType)
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetAttackSectionLabel(
        CharacterAttackSectionType sectionType)
    {
        return sectionType switch
        {
            CharacterAttackSectionType.Linkage => "1. 연동",
            CharacterAttackSectionType.Condition => "2. 조건",
            CharacterAttackSectionType.Subject => "3. 대상",
            CharacterAttackSectionType.Ability => "4. 능력",
            _ => sectionType.ToString()
        };
    }

    private void DrawCumulativeUpgradeSettingsFoldout()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _cumulativeUpgradeExpanded = EditorGUILayout.Foldout(
            _cumulativeUpgradeExpanded,
            "4. 업그레이드 - 누적",
            true,
            EditorStyles.foldoutHeader);

        bool addDefinition;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            addDefinition = GUILayout.Button(
                new GUIContent("+", "누적 업그레이드 정의 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        EditorGUILayout.EndHorizontal();

        if (addDefinition)
            AddCumulativeUpgradeDefinition();

        if (_cumulativeUpgradeExpanded)
        {
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                _serializedCharacter.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                DrawCumulativeUpgradeDefinitions();
                if (EditorGUI.EndChangeCheck() &&
                    _serializedCharacter.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_selectedCharacter);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawCumulativeUpgradeDefinitions()
    {
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            CumulativeUpgradeDefinitionsPropertyName);
        if (definitions == null)
        {
            EditorGUILayout.HelpBox(
                "누적 업그레이드 정의 구조를 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        if (definitions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 누적 업그레이드를 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        int removeIndex = -1;
        for (int index = 0; index < definitions.arraySize; index++)
        {
            SerializedProperty definition =
                definitions.GetArrayElementAtIndex(index);
            if (DrawCumulativeUpgradeDefinition(definition, index))
                removeIndex = index;
        }

        if (removeIndex >= 0)
        {
            definitions.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }
    }

    private static bool DrawCumulativeUpgradeDefinition(
        SerializedProperty definition,
        int index)
    {
        if (definition == null)
            return false;

        SerializedProperty upgradeId = definition.FindPropertyRelative(
            CumulativeUpgradeIdPropertyName);
        SerializedProperty maxLevel = definition.FindPropertyRelative(
            CumulativeUpgradeMaxLevelPropertyName);
        SerializedProperty modifiers = definition.FindPropertyRelative(
            CumulativeUpgradeModifiersPropertyName);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        string label = upgradeId != null &&
                       !string.IsNullOrWhiteSpace(upgradeId.stringValue)
            ? upgradeId.stringValue
            : $"누적 업그레이드 {index + 1}";
        definition.isExpanded = EditorGUILayout.Foldout(
            definition.isExpanded,
            label,
            true);
        bool remove = GUILayout.Button(
            "X",
            EditorStyles.miniButton,
            GUILayout.Width(24f));
        EditorGUILayout.EndHorizontal();

        if (definition.isExpanded)
        {
            EditorGUI.indentLevel++;
            if (upgradeId != null)
            {
                upgradeId.stringValue = EditorGUILayout.TextField(
                    new GUIContent(
                        "Upgrade ID",
                        "저장 데이터와 정의를 연결하는 캐릭터 내부 고유 ID"),
                    upgradeId.stringValue);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Upgrade ID 속성을 찾을 수 없습니다.",
                    MessageType.Error);
            }

            if (maxLevel != null)
            {
                maxLevel.intValue = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        new GUIContent(
                            "최대 레벨",
                            "0이면 레벨 제한이 없습니다."),
                        maxLevel.intValue));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "최대 레벨 속성을 찾을 수 없습니다.",
                    MessageType.Error);
            }

            DrawCumulativeUpgradeModifiers(modifiers);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        return remove;
    }

    private static void DrawCumulativeUpgradeModifiers(
        SerializedProperty modifiers)
    {
        if (modifiers == null)
        {
            EditorGUILayout.HelpBox(
                "누적 보정치 목록을 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("레벨당 보정치", EditorStyles.boldLabel);
        int removeIndex = -1;
        for (int index = 0; index < modifiers.arraySize; index++)
        {
            SerializedProperty modifier =
                modifiers.GetArrayElementAtIndex(index);
            SerializedProperty type = modifier.FindPropertyRelative(
                CumulativeUpgradeModifierTypePropertyName);
            SerializedProperty value = modifier.FindPropertyRelative(
                CumulativeUpgradeModifierValuePropertyName);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"보정치 {index + 1}",
                EditorStyles.miniBoldLabel);
            if (GUILayout.Button(
                    "X",
                    EditorStyles.miniButton,
                    GUILayout.Width(24f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();

            DrawAttackEnumPopup(
                type,
                "대상 수치",
                CumulativeUpgradeModifierOptions);
            if (value != null)
            {
                bool requiresInteger = type != null &&
                    (type.enumValueIndex ==
                         (int)CharacterCumulativeUpgradeModifierType
                             .MaximumHealth ||
                     type.enumValueIndex ==
                         (int)CharacterCumulativeUpgradeModifierType
                             .SkillCostReduction);
                float nextValue = EditorGUILayout.FloatField(
                    "레벨당 값",
                    value.floatValue);
                value.floatValue = requiresInteger
                    ? Mathf.Round(nextValue)
                    : nextValue;
                if (Mathf.Approximately(value.floatValue, 0f))
                {
                    EditorGUILayout.HelpBox(
                        "레벨당 값은 0이 아니어야 합니다.",
                        MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "레벨당 값 속성을 찾을 수 없습니다.",
                    MessageType.Error);
            }
            EditorGUILayout.EndVertical();

            if (removeIndex >= 0)
                break;
        }

        if (removeIndex >= 0)
        {
            modifiers.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }

        if (GUILayout.Button("+ 보정치 추가", EditorStyles.miniButton))
        {
            int newIndex = modifiers.arraySize;
            modifiers.InsertArrayElementAtIndex(newIndex);
            SerializedProperty modifier =
                modifiers.GetArrayElementAtIndex(newIndex);
            SetEnumValue(
                modifier,
                CumulativeUpgradeModifierTypePropertyName,
                (int)CharacterCumulativeUpgradeModifierType.AttackPower);
            SetFloatValue(
                modifier,
                CumulativeUpgradeModifierValuePropertyName,
                1f);
            GUI.changed = true;
        }
    }

    private void AddCumulativeUpgradeDefinition()
    {
        if (_selectedCharacter == null || _serializedCharacter == null)
            return;

        _serializedCharacter.UpdateIfRequiredOrScript();
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            CumulativeUpgradeDefinitionsPropertyName);
        if (definitions == null)
            return;

        int newIndex = definitions.arraySize;
        definitions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty definition =
            definitions.GetArrayElementAtIndex(newIndex);
        SerializedProperty upgradeId = definition.FindPropertyRelative(
            CumulativeUpgradeIdPropertyName);
        if (upgradeId != null)
        {
            upgradeId.stringValue =
                CreateUniqueCumulativeUpgradeId(definitions, newIndex);
        }
        SerializedProperty maxLevel = definition.FindPropertyRelative(
            CumulativeUpgradeMaxLevelPropertyName);
        if (maxLevel != null)
            maxLevel.intValue = 1;

        SerializedProperty modifiers = definition.FindPropertyRelative(
            CumulativeUpgradeModifiersPropertyName);
        if (modifiers != null)
        {
            modifiers.arraySize = 1;
            SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
            SetEnumValue(
                modifier,
                CumulativeUpgradeModifierTypePropertyName,
                (int)CharacterCumulativeUpgradeModifierType.AttackPower);
            SetFloatValue(
                modifier,
                CumulativeUpgradeModifierValuePropertyName,
                1f);
        }

        definition.isExpanded = true;
        if (_serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedCharacter);
        _cumulativeUpgradeExpanded = true;
    }

    private static string CreateUniqueCumulativeUpgradeId(
        SerializedProperty definitions,
        int ignoredIndex)
    {
        int suffix = 1;
        while (true)
        {
            string candidate = $"upgrade_{suffix++}";
            bool exists = false;
            for (int index = 0;
                 index < definitions.arraySize;
                 index++)
            {
                if (index == ignoredIndex)
                    continue;

                SerializedProperty id = definitions
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative(
                        CumulativeUpgradeIdPropertyName);
                if (id != null && string.Equals(
                        id.stringValue,
                        candidate,
                        StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                return candidate;
        }
    }

    private void DrawDungeonUpgradeSettingsFoldout()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _dungeonUpgradeExpanded = EditorGUILayout.Foldout(
            _dungeonUpgradeExpanded,
            "5. 업그레이드 - 던전",
            true,
            EditorStyles.foldoutHeader);

        bool addDefinition;
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode))
        {
            addDefinition = GUILayout.Button(
                new GUIContent("+", "던전 업그레이드 블록 추가"),
                EditorStyles.miniButton,
                GUILayout.Width(28f),
                GUILayout.Height(20f));
        }
        EditorGUILayout.EndHorizontal();

        if (addDefinition)
            AddDungeonUpgradeDefinition();

        if (_dungeonUpgradeExpanded)
        {
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                _serializedCharacter.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                DrawDungeonUpgradeDefinitions();
                if (EditorGUI.EndChangeCheck() &&
                    _serializedCharacter.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_selectedCharacter);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawDungeonUpgradeDefinitions()
    {
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            DungeonUpgradeDefinitionsPropertyName);
        if (definitions == null)
        {
            EditorGUILayout.HelpBox(
                "던전 업그레이드 구조를 찾을 수 없습니다.",
                MessageType.Error);
            return;
        }

        if (definitions.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "+ 버튼으로 던전 업그레이드 블록을 추가할 수 있습니다.",
                EditorStyles.miniLabel);
            return;
        }

        for (int definitionIndex = 0;
             definitionIndex < definitions.arraySize;
             definitionIndex++)
        {
            if (DrawDungeonUpgradeDefinition(
                    definitions.GetArrayElementAtIndex(definitionIndex),
                    definitionIndex))
            {
                definitions.DeleteArrayElementAtIndex(definitionIndex);
                GUI.changed = true;
                break;
            }
        }
    }

    private static bool DrawDungeonUpgradeDefinition(
        SerializedProperty definition,
        int definitionIndex)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        definition.isExpanded = EditorGUILayout.Foldout(
            definition.isExpanded,
            $"던전 업그레이드 {definitionIndex + 1}",
            true,
            EditorStyles.foldoutHeader);
        bool removeDefinition = GUILayout.Button(
            new GUIContent("-", "던전 업그레이드 블록 삭제"),
            EditorStyles.miniButton,
            GUILayout.Width(28f),
            GUILayout.Height(20f));
        EditorGUILayout.EndHorizontal();

        if (definition.isExpanded)
        {
            SerializedProperty entries = definition.FindPropertyRelative(
                DungeonUpgradeEntriesPropertyName);
            DrawDungeonUpgradeEntries(entries);
        }

        EditorGUILayout.EndVertical();
        return removeDefinition;
    }

    private static void DrawDungeonUpgradeEntries(
        SerializedProperty entries)
    {
        if (entries == null ||
            entries.arraySize != DungeonUpgradeOrder.Length)
        {
            EditorGUILayout.HelpBox(
                "던전 업그레이드 항목 구성이 올바르지 않습니다. " +
                "블록을 다시 생성해 주세요.",
                MessageType.Error);
            return;
        }

        float probabilityTotal = 0f;
        for (int index = 0; index < DungeonUpgradeOrder.Length; index++)
        {
            CharacterDungeonUpgradeType expectedType =
                DungeonUpgradeOrder[index];
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            SerializedProperty type = entry.FindPropertyRelative(
                DungeonUpgradeTypePropertyName);
            SerializedProperty probability = entry.FindPropertyRelative(
                DungeonUpgradeProbabilityPropertyName);
            SerializedProperty limit = entry.FindPropertyRelative(
                DungeonUpgradeLimitPropertyName);

            if (type == null || probability == null || limit == null)
            {
                EditorGUILayout.HelpBox(
                    $"{GetDungeonUpgradeLabel(expectedType)} 속성을 " +
                    "찾을 수 없습니다.",
                    MessageType.Error);
                continue;
            }

            type.enumValueIndex = (int)expectedType;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                GetDungeonUpgradeLabel(expectedType),
                EditorStyles.boldLabel);
            probability.floatValue = Mathf.Clamp(
                EditorGUILayout.FloatField(
                    "확률 (%)",
                    probability.floatValue),
                0f,
                100f);
            limit.intValue = Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "Limit",
                        "0이면 한 던전에서 횟수 제한 없이 등장합니다."),
                    limit.intValue));
            EditorGUILayout.EndVertical();

            probabilityTotal += probability.floatValue;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "확률 총합",
            $"{probabilityTotal:0.####}% / " +
            $"{CharacterDungeonUpgradeDefinition.RequiredProbabilityTotal:0}%",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Limit 0은 한 던전에서 무제한 등장을 의미합니다.",
            EditorStyles.miniLabel);
        if (Mathf.Abs(
                probabilityTotal -
                CharacterDungeonUpgradeDefinition.RequiredProbabilityTotal) >
            CharacterDungeonUpgradeDefinition.ProbabilityTolerance)
        {
            EditorGUILayout.HelpBox(
                "각 항목의 확률 총합은 반드시 100%여야 합니다.",
                MessageType.Error);
        }
    }

    private void AddDungeonUpgradeDefinition()
    {
        if (_selectedCharacter == null || _serializedCharacter == null)
            return;

        _serializedCharacter.UpdateIfRequiredOrScript();
        SerializedProperty definitions = _serializedCharacter.FindProperty(
            DungeonUpgradeDefinitionsPropertyName);
        if (definitions == null)
            return;

        int newIndex = definitions.arraySize;
        definitions.InsertArrayElementAtIndex(newIndex);
        SerializedProperty definition =
            definitions.GetArrayElementAtIndex(newIndex);
        SerializedProperty entries = definition.FindPropertyRelative(
            DungeonUpgradeEntriesPropertyName);
        if (entries == null)
            return;

        entries.ClearArray();
        entries.arraySize = DungeonUpgradeOrder.Length;
        for (int index = 0; index < DungeonUpgradeOrder.Length; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            SerializedProperty type = entry.FindPropertyRelative(
                DungeonUpgradeTypePropertyName);
            SerializedProperty probability = entry.FindPropertyRelative(
                DungeonUpgradeProbabilityPropertyName);
            SerializedProperty limit = entry.FindPropertyRelative(
                DungeonUpgradeLimitPropertyName);
            if (type != null)
                type.enumValueIndex = (int)DungeonUpgradeOrder[index];
            if (probability != null)
            {
                probability.floatValue =
                    DefaultDungeonUpgradeProbabilities[index];
            }
            if (limit != null)
                limit.intValue = 1;
        }

        definition.isExpanded = true;
        if (_serializedCharacter.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedCharacter);

        _dungeonUpgradeExpanded = true;
    }

    private static string GetDungeonUpgradeLabel(
        CharacterDungeonUpgradeType type)
    {
        return type switch
        {
            CharacterDungeonUpgradeType.AttackPower => "1. 공격력 +0.5",
            CharacterDungeonUpgradeType.Speed => "2. 속도 -0.1s",
            CharacterDungeonUpgradeType.PassiveDamage =>
                "3. 패시브 피해량 +0.5",
            CharacterDungeonUpgradeType.AttackDamage =>
                "4. 공격 피해량 +0.5 (고정)",
            CharacterDungeonUpgradeType.SkillDamage =>
                "5. 기술 피해량 +1 (고정)",
            CharacterDungeonUpgradeType.SkillCostReduction =>
                "6. 기술 코스트 감소 -1",
            _ => type.ToString()
        };
    }

    private bool MatchesSearch(CharacterSO character)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        string search = _searchText.Trim();
        return (character.name ?? string.Empty).IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               (character.CharacterName ?? string.Empty).IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SelectCharacter(
        CharacterSO character,
        bool resetEditorScroll = true)
    {
        if (character == null)
            return;

        if (_selectedCharacter != character)
        {
            RequestEditingFocusClear();
            CancelRenameSelectedCharacter();
        }

        _selectedCharacter = character;
        _serializedCharacter = new SerializedObject(character);
        if (resetEditorScroll)
            _editorScroll = Vector2.zero;

    }

    private void RequestEditingFocusClear()
    {
        _clearEditingFocusRequested = true;
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
        if (Event.current != null)
            ApplyPendingEditingFocusClear();
    }

    private void ApplyPendingEditingFocusClear()
    {
        if (!_clearEditingFocusRequested)
            return;

        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
        _clearEditingFocusRequested = false;
    }

    private void RefreshLocalizationKeys()
    {
        _nameKeyOptions.Clear();
        _descriptionKeyOptions.Clear();
        _localizationLoadError = string.Empty;

        try
        {
            LocalizationSourceModel model =
                LocalizationCodeGenerator.LoadSource();
            foreach (LocalizationSourceString entry in model.Strings)
            {
                string key = (entry.Key ?? string.Empty).Trim();
                if (!key.StartsWith(
                        CharacterLocalizationPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LocalizationKeyOption option = new(
                    key,
                    BuildLocalizationOptionLabel(entry, key));
                if (key.EndsWith(
                        ".name",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _nameKeyOptions.Add(option);
                }
                else if (key.EndsWith(
                             ".description",
                             StringComparison.OrdinalIgnoreCase))
                {
                    _descriptionKeyOptions.Add(option);
                }
            }

            _nameKeyOptions.Sort(CompareLocalizationOptions);
            _descriptionKeyOptions.Sort(CompareLocalizationOptions);
        }
        catch (Exception exception)
        {
            _localizationLoadError =
                "Localization 키를 불러오지 못했습니다: " +
                exception.Message;
        }
    }

    private static int CompareLocalizationOptions(
        LocalizationKeyOption left,
        LocalizationKeyOption right)
    {
        return string.Compare(
            left.Key,
            right.Key,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLocalizationOptionLabel(
        LocalizationSourceString entry,
        string key)
    {
        entry.Translations.TryGetValue("ko-KR", out string korean);
        entry.Translations.TryGetValue("en-US", out string english);
        korean = (korean ?? string.Empty).Trim();
        english = (english ?? string.Empty).Trim();

        if (!string.IsNullOrEmpty(korean) &&
            !string.IsNullOrEmpty(english))
        {
            return $"{key}  |  {korean} / {english}";
        }
        if (!string.IsNullOrEmpty(korean))
            return $"{key}  |  {korean}";
        if (!string.IsNullOrEmpty(english))
            return $"{key}  |  {english}";
        return key;
    }

    private void RefreshCharacterList()
    {
        string selectedPath = _selectedCharacter != null
            ? AssetDatabase.GetAssetPath(_selectedCharacter)
            : string.Empty;
        _characters.Clear();

        string[] guids = AssetDatabase.FindAssets("t:CharacterSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSO character =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(path);
            if (character != null)
                _characters.Add(character);
        }

        _characters.Sort((left, right) => string.Compare(
            left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty,
            StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(selectedPath))
        {
            CharacterSO restored = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                selectedPath);
            if (restored != null)
                SelectCharacter(restored);
        }
        else if (_selectedCharacter == null && _characters.Count > 0)
        {
            SelectCharacter(_characters[0]);
        }
    }

    private void CreateCharacter()
    {
        EnsureFolder(CharacterFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Character",
            "NewCharacter",
            "asset",
            "Choose a location for the new CharacterSO.",
            CharacterFolder);
        if (string.IsNullOrEmpty(path))
            return;

        CharacterSO character = CreateInstance<CharacterSO>();
        character.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(character, path);
        AssetDatabase.SaveAssetIfDirty(character);
        CharacterDefinitionCatalog.Invalidate();
        RefreshCharacterList();
        SelectCharacter(character);
        EditorGUIUtility.PingObject(character);
    }

    private void SaveSelectedCharacter()
    {
        if (_selectedCharacter == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(_selectedCharacter);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog(
                "Save CharacterSO",
                "The selected CharacterSO is not a saved asset.",
                "OK");
            return;
        }

        if (_serializedCharacter == null ||
            _serializedCharacter.targetObject != _selectedCharacter)
        {
            _serializedCharacter = new SerializedObject(_selectedCharacter);
        }

        _serializedCharacter.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedCharacter);
        AssetDatabase.SaveAssetIfDirty(_selectedCharacter);
        CharacterDefinitionCatalog.Invalidate();
        ShowNotification(new GUIContent(
            $"Saved {System.IO.Path.GetFileName(assetPath)}"));
    }

    private void DeleteSelectedCharacter()
    {
        CharacterSO character = _selectedCharacter;
        if (character == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(character);
        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            AssetDatabase.LoadMainAssetAtPath(assetPath) != character)
        {
            EditorUtility.DisplayDialog(
                "Delete CharacterSO",
                "The selected CharacterSO is not a deletable project asset.",
                "OK");
            return;
        }

        string assetName = character.name;
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete CharacterSO",
            $"'{assetName}' SO 파일을 삭제합니다.\n\n{assetPath}\n\n" +
            "이 작업은 Unity Undo로 복구할 수 없습니다.",
            "OK",
            "Cancel");
        if (!confirmed)
            return;

        if (!AssetDatabase.DeleteAsset(assetPath))
        {
            EditorUtility.DisplayDialog(
                "Delete CharacterSO",
                "The CharacterSO asset could not be deleted.",
                "OK");
            return;
        }

        CharacterDefinitionCatalog.Invalidate();

        if (Selection.activeObject == character)
            Selection.activeObject = null;
        CancelRenameSelectedCharacter();
        _selectedCharacter = null;
        _serializedCharacter = null;
        RefreshCharacterList();

        ShowNotification(new GUIContent($"Deleted {assetName}.asset"));
        Repaint();
    }

    private void BeginRenameSelectedCharacter()
    {
        if (_selectedCharacter == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(_selectedCharacter);
        if (string.IsNullOrEmpty(assetPath))
            return;

        _renameAssetName = System.IO.Path.GetFileNameWithoutExtension(
            assetPath);
        _isRenamingSelectedCharacter = true;
        _focusRenameField = true;
        Repaint();
    }

    private void CancelRenameSelectedCharacter()
    {
        _isRenamingSelectedCharacter = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
    }

    private void RenameSelectedCharacter()
    {
        CharacterSO character = _selectedCharacter;
        if (character == null)
        {
            CancelRenameSelectedCharacter();
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(character);
        if (string.IsNullOrEmpty(sourcePath))
        {
            EditorUtility.DisplayDialog(
                "Rename CharacterSO",
                "The selected CharacterSO is not a saved asset.",
                "OK");
            return;
        }

        string newName = (_renameAssetName ?? string.Empty).Trim();
        if (newName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            newName = newName.Substring(
                0,
                newName.Length - ".asset".Length).Trim();
        }

        if (!IsValidAssetFileName(newName, out string validationError))
        {
            EditorUtility.DisplayDialog(
                "Rename CharacterSO",
                validationError,
                "OK");
            _focusRenameField = true;
            return;
        }

        string currentName = System.IO.Path.GetFileNameWithoutExtension(
            sourcePath);
        if (string.Equals(currentName, newName, StringComparison.Ordinal))
        {
            CancelRenameSelectedCharacter();
            return;
        }

        string directory = System.IO.Path.GetDirectoryName(sourcePath)
            ?.Replace('\\', '/');
        string extension = System.IO.Path.GetExtension(sourcePath);
        string destinationPath = string.IsNullOrEmpty(directory)
            ? $"{newName}{extension}"
            : $"{directory}/{newName}{extension}";
        UnityEngine.Object existingAsset =
            AssetDatabase.LoadMainAssetAtPath(destinationPath);
        if (existingAsset != null && existingAsset != character)
        {
            EditorUtility.DisplayDialog(
                "Rename CharacterSO",
                $"An asset named '{newName}{extension}' already exists in " +
                "this folder.",
                "OK");
            _focusRenameField = true;
            return;
        }

        string renameError = AssetDatabase.RenameAsset(sourcePath, newName);
        if (!string.IsNullOrEmpty(renameError))
        {
            EditorUtility.DisplayDialog(
                "Rename CharacterSO",
                renameError,
                "OK");
            _focusRenameField = true;
            return;
        }

        CancelRenameSelectedCharacter();
        CharacterDefinitionCatalog.Invalidate();
        AssetDatabase.SaveAssets();
        RefreshCharacterList();
        SelectCharacter(character);
        EditorGUIUtility.PingObject(character);
        Repaint();
    }

    private static bool IsValidAssetFileName(
        string fileName,
        out string validationError)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            validationError = "Enter a file name.";
            return false;
        }

        if (fileName == "." || fileName == "..")
        {
            validationError = "The file name cannot be '.' or '..'.";
            return false;
        }

        if (fileName.IndexOfAny(
                System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.IndexOf('/') >= 0 ||
            fileName.IndexOf('\\') >= 0)
        {
            validationError = "The file name contains an invalid character.";
            return false;
        }

        if (fileName.EndsWith(".", StringComparison.Ordinal) ||
            fileName.EndsWith(" ", StringComparison.Ordinal))
        {
            validationError =
                "The file name cannot end with a period or a space.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private void DuplicateSelectedCharacter()
    {
        if (_selectedCharacter == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(_selectedCharacter);
        if (string.IsNullOrEmpty(sourcePath))
            return;

        string directory = System.IO.Path.GetDirectoryName(sourcePath)
            ?.Replace('\\', '/');
        string fileName = System.IO.Path.GetFileNameWithoutExtension(
            sourcePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{fileName} Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            EditorUtility.DisplayDialog(
                "Character Editor",
                "The character asset could not be duplicated.",
                "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        CharacterDefinitionCatalog.Invalidate();
        CharacterSO duplicate = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            destinationPath);
        if (duplicate != null)
        {
            duplicate.RegenerateCharacterId();
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }
        RefreshCharacterList();
        if (duplicate != null)
        {
            SelectCharacter(duplicate);
            EditorGUIUtility.PingObject(duplicate);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separatorIndex = path.LastIndexOf('/');
        if (separatorIndex <= 0)
            return;

        string parent = path.Substring(0, separatorIndex);
        string folderName = path.Substring(separatorIndex + 1);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private sealed class TargetAreaPopup : PopupWindowContent
    {
        private const float CellSize = 24f;
        private const float CellSpacing = 2f;
        private const float HorizontalPadding = 12f;
        private const float VerticalPadding = 10f;

        private readonly CharacterSO _character;
        private readonly string _definitionPropertyPath;

        public TargetAreaPopup(
            CharacterSO character,
            string definitionPropertyPath)
        {
            _character = character;
            _definitionPropertyPath = definitionPropertyPath;
        }

        public override Vector2 GetWindowSize()
        {
            float gridSize = DungeonBoardView.MaximumGridSize *
                             (CellSize + CellSpacing) - CellSpacing;
            return new Vector2(
                gridSize + HorizontalPadding * 2f,
                gridSize + VerticalPadding * 2f + 64f);
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(
                "공격 범위",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "흰색은 고정 타겟 칸입니다.",
                EditorStyles.miniLabel);

            SerializedObject serializedCharacter =
                TryCreateSerializedCharacter();
            SerializedProperty offsets = FindAreaOffsets(
                serializedCharacter);
            if (offsets == null)
            {
                EditorGUILayout.HelpBox(
                    "공격 범위 데이터를 찾을 수 없습니다.",
                    MessageType.Error);
                return;
            }

            int gridSize = DungeonBoardView.MaximumGridSize;
            int center = gridSize / 2;
            float pixelSize = gridSize * (CellSize + CellSpacing) -
                              CellSpacing;
            Rect gridRect = GUILayoutUtility.GetRect(
                pixelSize,
                pixelSize,
                GUILayout.ExpandWidth(false));
            for (int row = 0; row < gridSize; row++)
            {
                for (int column = 0; column < gridSize; column++)
                {
                    int rowOffset = row - center;
                    int columnOffset = column - center;
                    bool isCenter = rowOffset == 0 && columnOffset == 0;
                    bool isSelected = isCenter || ContainsOffset(
                        offsets,
                        rowOffset,
                        columnOffset);
                    Rect cellRect = new(
                        gridRect.x + column * (CellSize + CellSpacing),
                        gridRect.y + row * (CellSize + CellSpacing),
                        CellSize,
                        CellSize);
                    EditorGUI.DrawRect(
                        cellRect,
                        isCenter
                            ? Color.white
                            : isSelected
                                ? new Color(0.82f, 0.25f, 0.2f)
                                : new Color(0.25f, 0.25f, 0.25f));

                    if (isCenter)
                    {
                        GUIStyle centerStyle = new(EditorStyles.miniBoldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.black }
                        };
                        GUI.Label(cellRect, "T", centerStyle);
                        continue;
                    }

                    if (GUI.Button(
                            cellRect,
                            new GUIContent(
                                string.Empty,
                                $"상대 좌표 ({rowOffset}, {columnOffset})"),
                            GUIStyle.none))
                    {
                        ToggleOffset(rowOffset, columnOffset);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("초기화 (타겟 칸만 유지)"))
            {
                ClearOffsets();
                GUIUtility.ExitGUI();
            }
        }

        private SerializedObject TryCreateSerializedCharacter()
        {
            if (_character == null)
                return null;

            SerializedObject serializedCharacter = new(_character);
            serializedCharacter.UpdateIfRequiredOrScript();
            return serializedCharacter;
        }

        private SerializedProperty FindAreaOffsets(
            SerializedObject serializedCharacter)
        {
            if (serializedCharacter == null)
                return null;

            if (string.IsNullOrWhiteSpace(_definitionPropertyPath))
                return null;

            SerializedProperty definition = serializedCharacter.FindProperty(
                _definitionPropertyPath);
            return definition?.FindPropertyRelative(AreaOffsetsPropertyName);
        }

        private static bool ContainsOffset(
            SerializedProperty offsets,
            int rowOffset,
            int columnOffset)
        {
            return FindOffsetIndex(offsets, rowOffset, columnOffset) >= 0;
        }

        private static int FindOffsetIndex(
            SerializedProperty offsets,
            int rowOffset,
            int columnOffset)
        {
            if (offsets == null)
                return -1;

            for (int index = 0; index < offsets.arraySize; index++)
            {
                SerializedProperty offset =
                    offsets.GetArrayElementAtIndex(index);
                SerializedProperty row = offset.FindPropertyRelative(
                    AreaRowOffsetPropertyName);
                SerializedProperty column = offset.FindPropertyRelative(
                    AreaColumnOffsetPropertyName);
                if (row != null && column != null &&
                    row.intValue == rowOffset &&
                    column.intValue == columnOffset)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ToggleOffset(int rowOffset, int columnOffset)
        {
            Undo.RecordObject(_character, "Change Character Attack Area");
            SerializedObject serializedCharacter =
                TryCreateSerializedCharacter();
            SerializedProperty offsets = FindAreaOffsets(
                serializedCharacter);
            if (offsets == null)
                return;

            int existingIndex = FindOffsetIndex(
                offsets,
                rowOffset,
                columnOffset);
            if (existingIndex >= 0)
            {
                offsets.DeleteArrayElementAtIndex(existingIndex);
            }
            else
            {
                int newIndex = offsets.arraySize;
                offsets.InsertArrayElementAtIndex(newIndex);
                SerializedProperty offset =
                    offsets.GetArrayElementAtIndex(newIndex);
                offset.FindPropertyRelative(AreaRowOffsetPropertyName)
                    .intValue = rowOffset;
                offset.FindPropertyRelative(AreaColumnOffsetPropertyName)
                    .intValue = columnOffset;
            }

            ApplyChanges(serializedCharacter);
        }

        private void ClearOffsets()
        {
            Undo.RecordObject(_character, "Clear Character Attack Area");
            SerializedObject serializedCharacter =
                TryCreateSerializedCharacter();
            SerializedProperty offsets = FindAreaOffsets(
                serializedCharacter);
            if (offsets == null)
                return;

            offsets.ClearArray();
            ApplyChanges(serializedCharacter);
        }

        private void ApplyChanges(SerializedObject serializedCharacter)
        {
            if (serializedCharacter == null)
                return;

            serializedCharacter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_character);
            editorWindow?.Repaint();
        }
    }
}
