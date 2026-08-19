using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal enum EnemyEditorListSortMode
{
    Name = 0,
    Grade = 1
}

public sealed class EnemyEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.EnemyEditor;

    private const string AssetFolder = "Assets/06_Runtime/Resources/Enemies";
    private const string RenameControlName = "EnemyAssetRenameField";
    private static readonly string[] GradeFilterLabels =
    {
        "All Grades",
        "Normal",
        "Special",
        "Elite",
        "Boss"
    };
    private static readonly string[] SortModeLabels =
    {
        "Name Order",
        "Grade Order"
    };
    private readonly List<EnemySO> _definitions = new();
    private readonly List<EnemySO> _visibleDefinitions = new();

    private EnemySO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private string _searchText = string.Empty;
    [SerializeField] private int _gradeFilterIndex;
    [SerializeField] private EnemyEditorListSortMode _sortMode;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _validationExpanded = true;
    private bool _identityExpanded = true;
    private bool _statsExpanded = true;
    private bool _presentationExpanded = true;
    private bool _abilitiesExpanded = true;
    private bool _phasesExpanded = true;

    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.EnemyEditorPriority)]
    public static void Open()
    {
        EnemyEditorWindow window = GetWindow<EnemyEditorWindow>();
        window.titleContent = new GUIContent("Enemy Editor");
        window.minSize = new Vector2(900f, 600f);
        window.Show();
        window.Focus();
    }

    public static void Open(EnemySO enemy)
    {
        Open();
        EnemyEditorWindow window = GetWindow<EnemyEditorWindow>();
        window.RefreshList();
        if (enemy != null)
            window.SelectDefinition(enemy);
        window.Repaint();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.EnemyEditorPriority)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Enemy Editor");
        minSize = new Vector2(900f, 600f);
        PS260714LocalizationKeyField.Refresh();
        RefreshList();

        if (Selection.activeObject is EnemySO selected)
            SelectDefinition(selected);
        else if (_selected == null && _definitions.Count > 0)
            SelectDefinition(_definitions[0]);
    }

    private void OnProjectChange()
    {
        EnemyDefinitionCatalog.Invalidate();
        PS260714LocalizationKeyField.Refresh();
        RefreshList();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is EnemySO selected)
        {
            SelectDefinition(selected);
            Repaint();
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (_isRenaming)
            DrawRenameRow();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAssetList();
            DrawSeparator();
            DrawEditor();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Enemies: {_definitions.Count}",
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
            RefreshList);
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
            DrawListOptions();
            BuildVisibleDefinitions();

            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_listScroll))
            {
                _listScroll = scroll.scrollPosition;
                foreach (EnemySO definition in _visibleDefinitions)
                {
                    bool selected =
                        ReferenceEquals(definition, _selected);
                    string detail =
                        $"{definition.Grade} / {definition.Type} / " +
                        $"A{definition.Abilities.Count}";
                    if (PS260714AssetEditorList.DrawAssetRow(
                            selected,
                            definition,
                            definition.IconSprite,
                            GetDisplayName(definition),
                            detail,
                            definition.EnemyId))
                    {
                        SelectDefinition(definition);
                    }
                }

                if (_visibleDefinitions.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        _definitions.Count == 0
                            ? "No EnemySO assets were found."
                            : "No enemies match the search.",
                        MessageType.Info);
                }
            }
            PS260714AssetEditorList.DrawCountFooter(
                _visibleDefinitions.Count,
                _definitions.Count);
        }
    }

    private void DrawListOptions()
    {
        _gradeFilterIndex = Mathf.Clamp(
            _gradeFilterIndex,
            0,
            GradeFilterLabels.Length - 1);
        if (!Enum.IsDefined(typeof(EnemyEditorListSortMode), _sortMode))
            _sortMode = EnemyEditorListSortMode.Name;

        using (new EditorGUILayout.HorizontalScope())
        {
            _gradeFilterIndex = EditorGUILayout.Popup(
                _gradeFilterIndex,
                GradeFilterLabels,
                GUILayout.Width(112f));
            _sortMode = (EnemyEditorListSortMode)EditorGUILayout.Popup(
                (int)_sortMode,
                SortModeLabels,
                GUILayout.Width(112f));
        }
    }

    private void BuildVisibleDefinitions()
    {
        _visibleDefinitions.Clear();
        foreach (EnemySO definition in _definitions)
        {
            if (definition != null &&
                MatchesGradeFilter(definition, _gradeFilterIndex) &&
                MatchesSearch(definition))
            {
                _visibleDefinitions.Add(definition);
            }
        }

        _visibleDefinitions.Sort((left, right) =>
            CompareDefinitions(left, right, _sortMode));
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

    private void DrawEditor()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.ExpandWidth(true)))
        {
            if (_selected == null || _serialized == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an enemy or create a new EnemySO asset.",
                    MessageType.Info);
                return;
            }

            DrawSelectedHeader();
            _serialized.UpdateIfRequiredOrScript();
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_editorScroll))
            {
                _editorScroll = scroll.scrollPosition;
                DrawValidation();

                using (new EditorGUI.DisabledScope(
                           EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    EditorGUI.BeginChangeCheck();
                    DrawIdentity();
                    DrawBaseStats();
                    DrawPresentation();
                    DrawAbilities();
                    DrawBossPhases();

                    if (EditorGUI.EndChangeCheck() &&
                        _serialized.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(_selected);
                        EnemyDefinitionCatalog.Invalidate();
                    }
                }

                EditorGUILayout.Space(12f);
            }
        }
    }

    private void DrawSelectedHeader()
    {
        using (new EditorGUILayout.HorizontalScope(
                   EditorStyles.helpBox))
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(
                    _selected.name,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    AssetDatabase.GetAssetPath(_selected),
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawValidation()
    {
        EnemyDefinitionValidationResult validation =
            EnemyDefinitionValidator.Validate(
                _selected,
                _definitions);
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

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (validation.Diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Enemy definition validation passed.",
                    MessageType.Info);
                return;
            }

            foreach (EnemyDefinitionDiagnostic diagnostic in
                     validation.Diagnostics)
            {
                string path = string.IsNullOrWhiteSpace(diagnostic.Path)
                    ? "<root>"
                    : diagnostic.Path;
                MessageType type =
                    diagnostic.Severity ==
                    EnemyDefinitionDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning;
                EditorGUILayout.HelpBox(
                    $"[{diagnostic.Code}] {path}\n" +
                    diagnostic.Message,
                    type);
            }
        }
    }

    private void DrawIdentity()
    {
        _identityExpanded = EditorGUILayout.Foldout(
            _identityExpanded,
            "Identity and Localization",
            true,
            EditorStyles.foldoutHeader);
        if (!_identityExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    DrawProperty("enemyId", "Enemy ID");
                if (GUILayout.Button(
                        "Regenerate",
                        GUILayout.Width(84f)))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Regenerate Enemy ID",
                        "References that use this persistent ID may break.",
                        "Regenerate",
                        "Cancel");
                    if (confirmed)
                    {
                        Undo.RecordObject(_selected, "Regenerate Enemy ID");
                        _selected.RegenerateEnemyId();
                        EditorUtility.SetDirty(_selected);
                        _serialized.Update();
                    }
                }
            }

            PS260714LocalizationKeyField.Draw(
                Find("nameLocalizationKey"),
                "Name Localization Key");
            PS260714LocalizationKeyField.Draw(
                Find("descriptionLocalizationKey"),
                "Description Localization Key");
            PS260714LocalizationKeyField.DrawLoadError();
            DrawProperty("displayName", "Fallback Name");
            DrawProperty("description", "Fallback Description");
            DrawProperty("cardCode", "Card Code");
            DrawProperty("grade", "Grade");
            DrawProperty("type", "Type");
            DrawProperty("sortOrder", "Sort Order");
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Roster Metadata",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                DrawProperty("rosterSchemaVersion", "Roster Schema");
            DrawProperty("rosterTier", "Roster Tier");
            DrawPropertyWithChildren("roleTags", "Role Tags");
            DrawPropertyWithChildren("counterTags", "Counter Tags");
            DrawProperty(
                "recommendedMaxPerWave",
                "Recommended Maximum Per Wave (0 = Default)");
            DrawProperty(
                "spawnBudget",
                "Spawn Budget (0 = Threat Cost)");
            DrawProperty("encounterOnly", "Dedicated Encounter Only");
        }
    }

    private void DrawBaseStats()
    {
        _statsExpanded = EditorGUILayout.Foldout(
            _statsExpanded,
            "Base Stats",
            true,
            EditorStyles.foldoutHeader);
        if (!_statsExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawProperty("baseHealth", "Base Health");
            DrawProperty("healthScale", "Health Scale");
            DrawProperty("initialArmor", "Initial Armor");
            DrawProperty("initialShield", "Initial Shield");
            DrawProperty(
                "spawnIntervalMultiplier",
                "Base Spawn Interval Multiplier");
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Circular Defense",
                EditorStyles.boldLabel);
            DrawProperty("approachSpeed", "Approach Speed");
            DrawProperty("formationRadius", "Formation Radius");
            DrawProperty(
                "forwardSearchAngle",
                "Forward Search Angle");
            DrawProperty("attackPower", "Attack Power");
            DrawProperty(
                "coreAttackDamage",
                "Legacy Core Attack Damage");
            DrawProperty(
                "coreAttackDamagePolicy",
                "Core Damage Resolution");
            SerializedProperty damagePolicy =
                Find("coreAttackDamagePolicy");
            if (damagePolicy != null &&
                damagePolicy.enumValueIndex ==
                (int)EnemyCoreAttackDamagePolicy.AccumulateFraction)
            {
                DrawProperty(
                    "preciseCoreAttackDamage",
                    "Precise Core Attack Damage");
                EditorGUILayout.HelpBox(
                    "Fractional damage carries into later core attacks " +
                    "instead of being discarded per hit.",
                    MessageType.Info);
            }
            DrawProperty("coreAttackInterval", "Core Attack Interval");
            DrawProperty("coreAttackRange", "Core Attack Range");
            DrawProperty("threatCost", "Threat Cost (0 = Type Default)");
            DrawProperty(
                "unlockDifficulty",
                "Unlock Difficulty (-1 = Type Default)");
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Board Footprint",
                EditorStyles.boldLabel);
            DrawProperty("footprintWidth", "Width");
            DrawProperty("footprintHeight", "Height");
            DrawProperty("stackingPolicy", "Stacking Policy");
            SerializedProperty width = Find("footprintWidth");
            SerializedProperty height = Find("footprintHeight");
            if (width != null && height != null &&
                (width.intValue > 1 || height.intValue > 1))
            {
                EditorGUILayout.HelpBox(
                    "Footprints larger than 1x1 always use exclusive board occupancy.",
                    MessageType.Info);
            }
        }
    }

    private void DrawAbilities()
    {
        SerializedProperty abilities = Find("abilities");
        _abilitiesExpanded = EditorGUILayout.Foldout(
            _abilitiesExpanded,
            $"Abilities ({abilities?.arraySize ?? 0})",
            true,
            EditorStyles.foldoutHeader);
        if (!_abilitiesExpanded || abilities == null)
            return;

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < abilities.arraySize; index++)
        {
            SerializedProperty ability =
                abilities.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string id = ability.FindPropertyRelative(
                        "abilityId").stringValue;
                    ability.isExpanded = EditorGUILayout.Foldout(
                        ability.isExpanded,
                        string.IsNullOrWhiteSpace(id)
                            ? $"Ability {index + 1}"
                            : id,
                        true);
                    DrawMoveButtons(
                        index,
                        abilities.arraySize,
                        ref moveFrom,
                        ref moveTo);
                    if (GUILayout.Button(
                            "Remove",
                            EditorStyles.miniButton,
                            GUILayout.Width(58f)))
                    {
                        removeIndex = index;
                    }
                }

                if (ability.isExpanded)
                    DrawAbility(ability, _selected);
            }
        }

        ApplyListAction(
            abilities,
            removeIndex,
            moveFrom,
            moveTo);
        if (GUILayout.Button("Add Ability"))
            AddAbility(abilities);
    }

    private void DrawBossPhases()
    {
        SerializedProperty phases = Find("phaseDefinitions");
        _phasesExpanded = EditorGUILayout.Foldout(
            _phasesExpanded,
            $"Boss Phases ({phases?.arraySize ?? 0})",
            true,
            EditorStyles.foldoutHeader);
        if (!_phasesExpanded || phases == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "Phase ranges use inclusive whole-number health " +
                "percentages and must cover 0 through 100 exactly once. " +
                "Ability IDs reference entries in this enemy's ability " +
                "list. Advance On Core Contact is an early OR transition: " +
                "either the next health threshold or core contact advances " +
                "the phase.",
                MessageType.Info);
            EditorGUILayout.PropertyField(
                phases,
                new GUIContent("Phase Definitions"),
                true);
        }
    }

    private void DrawPresentation()
    {
        _presentationExpanded = EditorGUILayout.Foldout(
            _presentationExpanded,
            "Presentation and Battle Lifecycle VFX",
            true,
            EditorStyles.foldoutHeader);
        if (!_presentationExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawProperty("iconSprite", "Codex Icon");
            DrawBoardSprite();
            DrawBoardSpriteDirection();
            PS260714AssetReferenceField.Draw(
                Find("spawnVfxCue"),
                new GUIContent("Spawn VFX Cue"));
            PS260714AssetReferenceField.Draw(
                Find("deathVfxCue"),
                new GUIContent("Death VFX Cue"));
            EditorGUILayout.HelpBox(
                "Spawn plays after the enemy card is placed. Death uses the cached card anchor after removal.",
                MessageType.Info);
        }
    }

    private void DrawBoardSpriteDirection()
    {
        SerializedProperty property = Find("boardSpriteFacesRight");
        if (property == null)
            return;

        PS260714EditorSdDirectionField.Draw(property);
        EditorGUILayout.HelpBox(
            "Choose the direction shown by the source SD sprite. " +
            "In battle, the sprite turns to face the shield center.",
            MessageType.Info);
    }

    private void DrawBoardSprite()
    {
        SerializedProperty property = Find("boardSprite");
        if (property == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            Rect fieldRect = GUILayoutUtility.GetRect(
                116f,
                116f,
                GUILayout.Width(116f),
                GUILayout.Height(116f));
            EditorGUI.BeginProperty(
                fieldRect,
                GUIContent.none,
                property);
            property.objectReferenceValue = EditorGUI.ObjectField(
                fieldRect,
                GUIContent.none,
                property.objectReferenceValue,
                typeof(Sprite),
                false);
            EditorGUI.EndProperty();

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(
                    "Dungeon Board Sprite (1:1)",
                    EditorStyles.boldLabel);
                Sprite sprite = property.objectReferenceValue as Sprite;
                if (sprite == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign this enemy's square Sprite. Enemies without " +
                        "this reference are not rendered in the dungeon world.",
                        MessageType.Warning);
                    return;
                }

                float width = sprite.rect.width;
                float height = sprite.rect.height;
                bool isSquare = Mathf.Abs(width - height) <= 0.5f;
                EditorGUILayout.LabelField(
                    $"Sprite Rect: {width:0.#} x {height:0.#}",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    isSquare
                        ? "1:1 sprite ratio verified."
                        : "The selected Sprite is not 1:1. Crop or slice it " +
                          "to a square rect before use.",
                    isSquare ? MessageType.Info : MessageType.Error);
            }
        }
    }

    private static void DrawAbility(
        SerializedProperty ability,
        UnityEngine.Object owner)
    {
        DrawRelative(ability, "abilityId", "Ability ID");
        PS260714LocalizationKeyField.Draw(
            ability.FindPropertyRelative("nameLocalizationKey"),
            "Name Localization Key");
        PS260714LocalizationKeyField.Draw(
            ability.FindPropertyRelative("descriptionLocalizationKey"),
            "Description Localization Key");
        DrawRelative(ability, "fallbackName", "Fallback Name");
        DrawRelative(
            ability,
            "fallbackDescription",
            "Fallback Description");
        DrawRelative(ability, "abilityTypeId", "Ability Type ID");
        SerializedProperty parameters =
            ability.FindPropertyRelative("parameters");
        if (parameters != null)
        {
            EditorGUILayout.PropertyField(
                parameters,
                new GUIContent("Ability Parameters"),
                true);
        }
        DrawRelative(ability, "trigger", "Trigger");
        DrawRelativeWithChildren(
            ability,
            "triggerEvents",
            "Additional Trigger Events (OR)");
        DrawRelative(ability, "priority", "Priority");

        SerializedProperty trigger =
            ability.FindPropertyRelative("trigger");
        if (SerializedAbilityUsesTrigger(
                ability,
                EnemyAbilityTrigger.OnCooldown))
        {
            DrawRelative(ability, "cooldown", "Cooldown");
            DrawRelativeWithChildren(
                ability,
                "cooldownOverrides",
                "Health-Based Cooldown Overrides");
            DrawRelative(
                ability,
                "cooldownResetPolicy",
                "Cooldown Reset");
            DrawRelative(
                ability,
                "pauseCooldownWhileDisabled",
                "Pause While Disabled");
        }
        if (SerializedAbilityUsesTrigger(
                ability,
                EnemyAbilityTrigger.OnHealthThreshold))
        {
            DrawRelative(
                ability,
                "healthThresholdPercent",
                "Health Threshold Percent");
        }
        if (SerializedAbilityUsesTrigger(
                ability,
                EnemyAbilityTrigger.AfterNoDamage))
        {
            DrawRelative(
                ability,
                "noDamageDuration",
                "No-Damage Duration");
        }

        DrawRelative(
            ability,
            "initialCharges",
            "Initial Charges (0 = Unlimited)");
        if (ability.FindPropertyRelative("initialCharges").intValue > 0)
        {
            DrawRelative(
                ability,
                "chargeConsumptionPolicy",
                "Charge Consumption");
        }

        SerializedProperty charge =
            ability.FindPropertyRelative("charge");
        SerializedProperty telegraph =
            ability.FindPropertyRelative("telegraph");
        EditorGUILayout.PropertyField(
            charge,
            new GUIContent("Charge"),
            true);
        EditorGUILayout.PropertyField(
            telegraph,
            new GUIContent("Telegraph"),
            true);

        DrawConditions(ability);
        DrawTarget(ability);
        DrawOperations(
            ability,
            (EnemyAbilityTrigger)trigger.enumValueIndex,
            owner);
    }

    private static void DrawConditions(SerializedProperty ability)
    {
        SerializedProperty conditions =
            ability.FindPropertyRelative("conditions");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Conditions ({conditions.arraySize})",
            EditorStyles.boldLabel);
        if (conditions.arraySize > 1)
        {
            DrawRelative(
                ability,
                "conditionMatchMode",
                "Condition Match");
        }

        int removeIndex = -1;
        for (int index = 0; index < conditions.arraySize; index++)
        {
            SerializedProperty condition =
                conditions.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"Condition {index + 1}",
                        EditorStyles.miniBoldLabel);
                    if (GUILayout.Button(
                            "Remove",
                            EditorStyles.miniButton,
                            GUILayout.Width(58f)))
                    {
                        removeIndex = index;
                    }
                }

                DrawRelative(condition, "type", "Type");
                EnemyAbilityConditionType type =
                    (EnemyAbilityConditionType)condition
                        .FindPropertyRelative("type").enumValueIndex;
                switch (type)
                {
                    case EnemyAbilityConditionType.SourceHealth:
                    case EnemyAbilityConditionType.SourceHealthPercentage:
                    case EnemyAbilityConditionType.TargetHealth:
                    case EnemyAbilityConditionType.TargetHealthPercentage:
                    case EnemyAbilityConditionType.TargetTotalDamageDealt:
                        DrawRelative(
                            condition,
                            "comparison",
                            "Comparison");
                        DrawRelative(
                            condition,
                            "threshold",
                            "Threshold");
                        break;

                    case EnemyAbilityConditionType.SourceHasStatus:
                    case EnemyAbilityConditionType.TargetHasStatus:
                        SerializedProperty statusSelectionScope =
                            condition.FindPropertyRelative(
                                "statusSelectionScope");
                        DrawRelative(
                            condition,
                            "statusSelectionScope",
                            "Status Scope");
                        bool selectsConfiguredStatuses =
                            statusSelectionScope == null ||
                            statusSelectionScope.enumValueIndex ==
                            (int)CharacterStatusSelectionScope
                                .SelectedStatuses;
                        SerializedProperty legacyStatus =
                            condition.FindPropertyRelative("statusEffect");
                        SerializedProperty statuses =
                            condition.FindPropertyRelative("statusEffects");
                        CharacterTargetFaction? statusFaction =
                            ResolveConditionStatusFaction(ability, type);
                        if (selectsConfiguredStatuses)
                        {
                            PS260714StatusEffectSelection.Draw(
                                statuses,
                                legacyStatus,
                                new GUIContent("Status Effects"),
                                new PS260714StatusEffectSelectionOptions(
                                    targetFaction: statusFaction));
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                "The condition counts distinct active " +
                                "buffs/debuffs on the evaluated unit.",
                                MessageType.Info);
                        }
                        DrawRelative(
                            condition,
                            "statusMatchMode",
                            "Status Match");
                        SerializedProperty statusMatchMode =
                            condition.FindPropertyRelative(
                                "statusMatchMode");
                        if (statusMatchMode != null &&
                            statusMatchMode.enumValueIndex ==
                            (int)CharacterStatusConditionMatchMode
                                .AtLeastCount)
                        {
                            SerializedProperty matchCount =
                                condition.FindPropertyRelative(
                                    "statusMatchCount");
                            if (matchCount != null)
                            {
                                matchCount.intValue = Mathf.Max(
                                    1,
                                    EditorGUILayout.IntField(
                                        "Required Status Count",
                                        matchCount.intValue));
                            }
                        }
                        DrawRelative(
                            condition,
                            "expected",
                            "Must Have Status");
                        break;

                    case EnemyAbilityConditionType.IncomingDamageType:
                        DrawRelative(
                            condition,
                            "incomingDamageType",
                            "Damage Type");
                        DrawRelative(
                            condition,
                            "expected",
                            "Must Match");
                        break;

                    case EnemyAbilityConditionType.HasAlternateTarget:
                        DrawRelative(
                            condition,
                            "expected",
                            "Expected");
                        break;

                    case EnemyAbilityConditionType.RepeatedDamageSource:
                        DrawRelative(
                            condition,
                            "windowDuration",
                            "Source History Window (Seconds)");
                        DrawRelative(
                            condition,
                            "expected",
                            "Must Be Repeated Source");
                        EditorGUILayout.HelpBox(
                            "The combat event supplies the current incoming " +
                            "source ID. This condition matches when the same " +
                            "source damaged the evaluated enemy inside the " +
                            "configured window.",
                            MessageType.Info);
                        break;
                }
            }
        }

        if (removeIndex >= 0)
            conditions.DeleteArrayElementAtIndex(removeIndex);
        if (GUILayout.Button(
                "Add Condition",
                EditorStyles.miniButton))
        {
            AddCondition(conditions);
        }
    }

    private static void DrawTarget(SerializedProperty ability)
    {
        SerializedProperty target =
            ability.FindPropertyRelative("target");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Target Selection",
            EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawRelative(target, "faction", "Faction");
            DrawRelative(target, "subject", "Subject");
            EnemyAbilityTargetSubject subject =
                (EnemyAbilityTargetSubject)target
                    .FindPropertyRelative("subject").enumValueIndex;
            if (subject == EnemyAbilityTargetSubject.Random ||
                subject == EnemyAbilityTargetSubject.HighestValue ||
                subject == EnemyAbilityTargetSubject.LowestValue)
            {
                DrawRelative(target, "targetCount", "Target Count");
            }
            if (subject == EnemyAbilityTargetSubject.HighestValue ||
                subject == EnemyAbilityTargetSubject.LowestValue)
            {
                DrawRelative(target, "metric", "Metric");
            }
            if (subject == EnemyAbilityTargetSubject.Adjacent)
            {
                DrawRelative(target, "range", "Range");
                DrawRelative(
                    target,
                    "includeDiagonals",
                    "Include Diagonals");
            }
            else if (subject == EnemyAbilityTargetSubject.WorldRadius)
            {
                DrawRelative(target, "worldRadius", "World Radius");
                DrawRelative(target, "includeSource", "Include Source");
                DrawRelative(target, "layerScope", "Formation Layers");
            }
            SerializedProperty areaDefinition =
                target.FindPropertyRelative("areaDefinition");
            using (new EditorGUI.DisabledScope(true))
            {
                BattleAbilityEditorGUI.DrawAreaDefinition(
                    areaDefinition,
                    null,
                    null);
            }
            EditorGUILayout.HelpBox(
                "Enemy abilities use automatic target selection, so their " +
                "shared area definition remains Target.",
                MessageType.Info);
        }
    }

    private static void DrawOperations(
        SerializedProperty ability,
        EnemyAbilityTrigger trigger,
        UnityEngine.Object owner)
    {
        SerializedProperty operations =
            ability.FindPropertyRelative("operations");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Operations ({operations.arraySize})",
            EditorStyles.boldLabel);

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < operations.arraySize; index++)
        {
            SerializedProperty operation =
                operations.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty typeProperty =
                        operation.FindPropertyRelative("type");
                    operation.isExpanded = EditorGUILayout.Foldout(
                        operation.isExpanded,
                        typeProperty.enumDisplayNames[
                            Mathf.Clamp(
                                typeProperty.enumValueIndex,
                                0,
                                typeProperty.enumDisplayNames.Length - 1)],
                        true);
                    DrawMoveButtons(
                        index,
                        operations.arraySize,
                        ref moveFrom,
                        ref moveTo);
                    if (GUILayout.Button(
                            "Remove",
                            EditorStyles.miniButton,
                            GUILayout.Width(58f)))
                    {
                        removeIndex = index;
                    }
                }

                if (!operation.isExpanded)
                    continue;

                DrawRelative(operation, "enabled", "Enabled");
                SerializedProperty operationType =
                    operation.FindPropertyRelative("type");
                DrawRelative(operation, "type", "Type");
                if (operationType.enumValueIndex !=
                    (int)EnemyAbilityOperationType.ExecuteEffects)
                {
                    DrawSpecializedOperation(operation, operationType);
                    continue;
                }
                BattleAbilityEditorGUI.DrawEffectList(
                    operation.FindPropertyRelative("effects"),
                    owner,
                    (owner as EnemySO)?.AttackPower);
            }
        }

        ApplyListAction(
            operations,
            removeIndex,
            moveFrom,
            moveTo);
        if (GUILayout.Button(
                "Add Operation",
                EditorStyles.miniButton))
        {
            AddOperation(operations, trigger);
        }
    }

    private static void DrawSpecializedOperation(
        SerializedProperty operation,
        SerializedProperty operationType)
    {
        EnemyAbilityOperationType type =
            (EnemyAbilityOperationType)operationType.enumValueIndex;
        EditorGUILayout.HelpBox(
            "This trigger-specific operation is validated by the enemy " +
            "ability validator and stays outside the shared effect list.",
            MessageType.Info);
        DrawRelative(operation, "sourceId", "Modifier Source ID");
        switch (type)
        {
            case EnemyAbilityOperationType.ModifySpawnInterval:
                DrawRelative(operation, "multiplier", "Interval Multiplier");
                break;

            case EnemyAbilityOperationType.ModifyIncomingDamage:
                DrawRelative(operation, "amount", "Flat Amount");
                DrawRelative(operation, "percentage", "Additive Percent");
                DrawRelative(operation, "multiplier", "Multiplier");
                break;

            case EnemyAbilityOperationType.ExpandSpawnGroup:
                DrawRelative(operation, "count", "Additional Count");
                break;

            case EnemyAbilityOperationType.GrantArmor:
                DrawRelative(operation, "amount", "Fixed Armor");
                DrawRelative(
                    operation,
                    "multiplier",
                    "Maximum Health Multiplier");
                break;

            case EnemyAbilityOperationType.RedirectDamage:
                DrawRelative(operation, "range", "Range");
                DrawRelative(
                    operation,
                    "includeDiagonals",
                    "Include Diagonals");
                break;

            case EnemyAbilityOperationType.ModifyTargetPriority:
                DrawRelative(
                    operation,
                    "targetPriorityMode",
                    "Priority Mode");
                DrawRelative(
                    operation,
                    "targetPriorityAdjustment",
                    "Priority Adjustment");
                break;

            case EnemyAbilityOperationType.ModifyCoreAttackDamage:
                DrawRelative(operation, "amount", "Flat Amount");
                DrawRelative(operation, "percentage", "Additive Percent");
                DrawRelative(operation, "multiplier", "Multiplier");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "maximumStacks", "Maximum Stacks");
                break;

            case EnemyAbilityOperationType.ModifyCoreAttackInterval:
            case EnemyAbilityOperationType.ModifyStatusDuration:
                DrawRelative(operation, "multiplier", "Multiplier");
                DrawRelative(operation, "percentage", "Additive Percent");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "worldRadius", "World Radius");
                break;

            case EnemyAbilityOperationType.GrantStatusImmunity:
                DrawRelative(
                    operation,
                    "duration",
                    "Duration (0 = While Active)");
                DrawRelative(operation, "worldRadius", "World Radius");
                break;

            case EnemyAbilityOperationType.ChargeCoreAttack:
                DrawRelative(operation, "multiplier", "Damage Multiplier");
                break;

            case EnemyAbilityOperationType.SummonEnemy:
                DrawRelativeWithChildren(
                    operation,
                    "summon",
                    "Summon Definition");
                break;

            case EnemyAbilityOperationType.ApplyCoreEffect:
                DrawRelative(operation, "amount", "Flat Amount");
                DrawRelative(operation, "percentage", "Additive Percent");
                DrawRelative(operation, "multiplier", "Multiplier");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "interval", "Tick Interval");
                DrawRelative(operation, "maximumStacks", "Maximum Stacks");
                break;

            case EnemyAbilityOperationType.CreateWorldZone:
                DrawRelative(operation, "worldRadius", "World Radius");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "interval", "Tick Interval");
                break;

            case EnemyAbilityOperationType.LinkTargets:
                DrawRelative(operation, "count", "Maximum Linked Targets");
                DrawRelative(operation, "worldRadius", "World Radius");
                DrawRelative(operation, "percentage", "Shared Percent");
                DrawRelative(operation, "duration", "Duration");
                break;

            case EnemyAbilityOperationType.ReflectDamage:
                DrawRelative(operation, "percentage", "Reflected Percent");
                DrawRelative(operation, "duration", "Duration");
                break;

            case EnemyAbilityOperationType.ReplayAbility:
                DrawRelative(
                    operation,
                    "referencedAbilityId",
                    "Referenced Ability ID (Empty = Last)");
                DrawRelativeWithChildren(
                    operation,
                    "reference",
                    "Enemy Reference");
                DrawRelative(operation, "multiplier", "Power Multiplier");
                break;

            case EnemyAbilityOperationType.ModifyCardCost:
                DrawRelative(operation, "amount", "Cost Increase");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "maximumStacks", "Maximum Stacks");
                break;

            case EnemyAbilityOperationType.LockCard:
                DrawRelative(operation, "count", "Locked Card Count");
                DrawRelative(operation, "duration", "Duration");
                break;

            case EnemyAbilityOperationType.ModifyResourceRecovery:
            case EnemyAbilityOperationType.ModifyCoreRecovery:
            case EnemyAbilityOperationType.ModifyCoreMaximumHealth:
                DrawRelative(operation, "amount", "Flat Amount");
                DrawRelative(operation, "percentage", "Additive Percent");
                DrawRelative(operation, "multiplier", "Multiplier");
                DrawRelative(operation, "duration", "Duration");
                DrawRelative(operation, "interval", "Tick Interval");
                DrawRelative(operation, "maximumStacks", "Maximum Stacks");
                break;

            case EnemyAbilityOperationType.SetUntargetable:
                DrawRelative(operation, "duration", "Duration");
                break;

            case EnemyAbilityOperationType.ModifyPlayerActionInterval:
                DrawRelative(
                    operation,
                    "multiplier",
                    "Player Action Interval Multiplier");
                DrawRelative(
                    operation,
                    "duration",
                    "Duration (0 = While Active)");
                break;

            case EnemyAbilityOperationType.ConvertCoreDamageToSelfShield:
                DrawRelative(
                    operation,
                    "percentage",
                    "Core Damage Conversion Percent");
                break;

            default:
                EditorGUILayout.HelpBox(
                    $"Unsupported operation type '{type}'.",
                    MessageType.Error);
                break;
        }
    }

    private static void AddAbility(SerializedProperty abilities)
    {
        if (abilities == null)
            return;

        string abilityId = CreateUniqueAbilityId(abilities);
        int index = abilities.arraySize;
        abilities.InsertArrayElementAtIndex(index);
        SerializedProperty ability =
            abilities.GetArrayElementAtIndex(index);
        ability.isExpanded = true;
        SetString(ability, "abilityId", abilityId);
        SetString(ability, "nameLocalizationKey", string.Empty);
        SetString(ability, "descriptionLocalizationKey", string.Empty);
        SetString(ability, "abilityTypeId", abilityId);
        ability.FindPropertyRelative("parameters")?.ClearArray();
        SetString(ability, "fallbackName", "New Ability");
        SetString(ability, "fallbackDescription", string.Empty);
        SetEnum(
            ability,
            "trigger",
            (int)EnemyAbilityTrigger.OnCooldown);
        ability.FindPropertyRelative("triggerEvents")?.ClearArray();
        SetInt(ability, "priority", 0);
        SetFloat(ability, "cooldown", 1f);
        ability.FindPropertyRelative("cooldownOverrides")?.ClearArray();
        SetEnum(
            ability,
            "cooldownResetPolicy",
            (int)EnemyAbilityCooldownResetPolicy.OnSuccessfulActivation);
        SetBool(ability, "pauseCooldownWhileDisabled", true);
        SetInt(ability, "initialCharges", 0);
        SetEnum(
            ability,
            "chargeConsumptionPolicy",
            (int)EnemyAbilityChargeConsumptionPolicy.OnSuccessfulActivation);
        SetEnum(
            ability,
            "conditionMatchMode",
            (int)CharacterConditionMatchMode.All);
        SetFloat(ability, "healthThresholdPercent", 50f);
        SetFloat(ability, "noDamageDuration", 3f);
        SerializedProperty charge =
            ability.FindPropertyRelative("charge");
        SetBool(charge, "enabled", false);
        SetFloat(charge, "duration", 1f);
        SetBool(charge, "interruptible", true);
        SetEnum(
            charge,
            "interrupts",
            (int)EnemyChargeInterruptFlags.Stun);
        SerializedProperty telegraph =
            ability.FindPropertyRelative("telegraph");
        SetBool(telegraph, "enabled", false);
        SetFloat(telegraph, "leadTime", 0.5f);
        SetString(telegraph, "cueId", string.Empty);
        SetFloat(telegraph, "worldRadius", 0f);
        ability.FindPropertyRelative("conditions").ClearArray();

        SerializedProperty target =
            ability.FindPropertyRelative("target");
        SetEnum(
            target,
            "faction",
            (int)EnemyAbilityTargetFaction.Self);
        SetEnum(
            target,
            "subject",
            (int)EnemyAbilityTargetSubject.Self);
        SetEnum(
            target,
            "metric",
            (int)EnemyAbilityTargetMetric.Health);
        SetInt(target, "targetCount", 1);
        SetInt(target, "range", 1);
        SetBool(target, "includeDiagonals", false);
        SetFloat(target, "worldRadius", 0f);
        SetBool(target, "includeSource", false);
        SetEnum(
            target,
            "layerScope",
            (int)EnemyWorldLayerScope.All);

        SerializedProperty operations =
            ability.FindPropertyRelative("operations");
        operations.ClearArray();
        AddOperation(operations, EnemyAbilityTrigger.OnCooldown);
    }

    private static void AddCondition(SerializedProperty conditions)
    {
        if (conditions == null)
            return;

        int index = conditions.arraySize;
        conditions.InsertArrayElementAtIndex(index);
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(index);
        SetEnum(
            condition,
            "type",
            (int)EnemyAbilityConditionType.SourceHealthPercentage);
        SetEnum(
            condition,
            "comparison",
            (int)CharacterNumericComparison.LessThanOrEqual);
        SetFloat(condition, "threshold", 50f);
        SetObject(condition, "statusEffect", null);
        SerializedProperty statuses =
            condition.FindPropertyRelative("statusEffects");
        statuses?.ClearArray();
        SetEnum(
            condition,
            "statusSelectionScope",
            (int)CharacterStatusSelectionScope.SelectedStatuses);
        SetEnum(
            condition,
            "statusMatchMode",
            (int)CharacterStatusConditionMatchMode.Any);
        SerializedProperty statusMatchCount =
            condition.FindPropertyRelative("statusMatchCount");
        if (statusMatchCount != null)
            statusMatchCount.intValue = 1;
        SetEnum(
            condition,
            "incomingDamageType",
            (int)CharacterAttackDamageType.Physical);
        SetBool(condition, "expected", true);
        SetFloat(condition, "windowDuration", 1f);
    }

    private static CharacterTargetFaction? ResolveConditionStatusFaction(
        SerializedProperty ability,
        EnemyAbilityConditionType type)
    {
        if (type == EnemyAbilityConditionType.SourceHasStatus)
            return CharacterTargetFaction.Enemy;

        SerializedProperty faction = ability?
            .FindPropertyRelative("target")?
            .FindPropertyRelative("faction");
        if (faction == null)
            return null;

        return (EnemyAbilityTargetFaction)faction.enumValueIndex switch
        {
            EnemyAbilityTargetFaction.Self =>
                CharacterTargetFaction.Enemy,
            EnemyAbilityTargetFaction.EnemyAllies =>
                CharacterTargetFaction.Enemy,
            EnemyAbilityTargetFaction.PlayerCharacters =>
                CharacterTargetFaction.Ally,
            _ => null
        };
    }

    private static bool SerializedAbilityUsesTrigger(
        SerializedProperty ability,
        EnemyAbilityTrigger expected)
    {
        SerializedProperty primary =
            ability?.FindPropertyRelative("trigger");
        if (primary != null &&
            primary.enumValueIndex == (int)expected)
        {
            return true;
        }

        SerializedProperty additional =
            ability?.FindPropertyRelative("triggerEvents");
        if (additional == null)
            return false;
        for (int index = 0; index < additional.arraySize; index++)
        {
            if (additional.GetArrayElementAtIndex(index).enumValueIndex ==
                (int)expected)
            {
                return true;
            }
        }
        return false;
    }

    private static void AddOperation(
        SerializedProperty operations,
        EnemyAbilityTrigger trigger)
    {
        if (operations == null)
            return;

        const EnemyAbilityOperationType operationType =
            EnemyAbilityOperationType.ExecuteEffects;

        int index = operations.arraySize;
        operations.InsertArrayElementAtIndex(index);
        SerializedProperty operation =
            operations.GetArrayElementAtIndex(index);
        operation.isExpanded = true;
        SetEnum(operation, "type", (int)operationType);
        SetFloat(operation, "multiplier", 1f);
        SetInt(operation, "amount", 1);
        SetInt(operation, "count", 1);
        SetInt(operation, "range", 1);
        SetBool(operation, "includeDiagonals", true);
        SetBool(operation, "enabled", true);
        SetEnum(
            operation,
            "targetPriorityMode",
            (int)EnemyTargetPriorityMode.Exclude);
        SetInt(operation, "targetPriorityAdjustment", 0);
        SetString(operation, "sourceId", string.Empty);
        SetFloat(operation, "duration", 0f);
        SetFloat(operation, "interval", 0f);
        SetFloat(operation, "worldRadius", 0f);
        SetFloat(operation, "percentage", 0f);
        SetInt(operation, "maximumStacks", 0);
        SetString(operation, "referencedAbilityId", string.Empty);
        SerializedProperty reference =
            operation.FindPropertyRelative("reference");
        SetObject(reference, "enemy", null);
        SetString(reference, "enemyId", string.Empty);
        SerializedProperty summon =
            operation.FindPropertyRelative("summon");
        summon.FindPropertyRelative("candidates")?.ClearArray();
        SetInt(summon, "minimumCount", 1);
        SetInt(summon, "maximumCount", 1);
        SetInt(summon, "maximumActive", 0);
        SetBool(summon, "allowRecursiveSummon", false);
        SetBool(summon, "inheritFormationLayer", true);
        SetFloat(summon, "childHealthMultiplier", 1f);
        SetFloat(summon, "childCoreAttackMultiplier", 1f);
        SerializedProperty effects =
            operation.FindPropertyRelative("effects");
        effects.ClearArray();
        BattleAbilityEditorGUI.AddDefaultEffect(effects);
    }

    private static string CreateUniqueAbilityId(
        SerializedProperty abilities)
    {
        HashSet<string> existing = new(StringComparer.Ordinal);
        for (int index = 0; index < abilities.arraySize; index++)
        {
            SerializedProperty ability =
                abilities.GetArrayElementAtIndex(index);
            existing.Add(
                ability.FindPropertyRelative("abilityId").stringValue);
        }

        int suffix = abilities.arraySize + 1;
        string candidate;
        do
        {
            candidate = $"ability_{suffix++}";
        }
        while (existing.Contains(candidate));
        return candidate;
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
                    "↑",
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
                    "↓",
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
            GUI.changed = true;
        }
    }

    private SerializedProperty Find(string propertyName)
    {
        return _serialized?.FindProperty(propertyName);
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = Find(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawPropertyWithChildren(string propertyName, string label)
    {
        SerializedProperty property = Find(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label),
                true);
        }
    }

    private static void DrawRelative(
        SerializedProperty parent,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private static void DrawRelativeWithChildren(
        SerializedProperty parent,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label),
                true);
        }
    }

    private static void SetString(
        SerializedProperty parent,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.stringValue = value ?? string.Empty;
    }

    private static void SetEnum(
        SerializedProperty parent,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetInt(
        SerializedProperty parent,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetFloat(
        SerializedProperty parent,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(
        SerializedProperty parent,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetObject(
        SerializedProperty parent,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private bool MatchesSearch(EnemySO definition)
    {
        string search = (_searchText ?? string.Empty).Trim();
        return string.IsNullOrEmpty(search) ||
               definition.name.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.EnemyId.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               GetDisplayName(definition).IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.DisplayName.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.Type.ToString().IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.Grade.ToString().IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool MatchesGradeFilter(
        EnemySO definition,
        int gradeFilterIndex)
    {
        if (definition == null)
            return false;
        if (gradeFilterIndex <= 0 ||
            gradeFilterIndex >= GradeFilterLabels.Length)
        {
            return true;
        }

        return definition.Grade ==
               (EEnemyGrade)(gradeFilterIndex - 1);
    }

    internal static int CompareDefinitions(
        EnemySO left,
        EnemySO right,
        EnemyEditorListSortMode sortMode)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        if (sortMode == EnemyEditorListSortMode.Grade)
        {
            int gradeComparison = left.Grade.CompareTo(right.Grade);
            if (gradeComparison != 0)
                return gradeComparison;
        }

        int nameComparison = string.Compare(
            GetDisplayName(left),
            GetDisplayName(right),
            StringComparison.CurrentCultureIgnoreCase);
        if (nameComparison != 0)
            return nameComparison;

        return string.Compare(
            left.name,
            right.name,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetDisplayName(EnemySO definition)
    {
        return definition != null
            ? PS260714EditorAssetDisplayName.Resolve(
                definition,
                definition.NameLocalizationKey,
                definition.DisplayName)
            : string.Empty;
    }

    private void SelectDefinition(EnemySO definition)
    {
        if (definition == null)
            return;

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
            "t:EnemySO",
            new[] { AssetFolder });
        EnemyDefinitionCatalog.Invalidate();
        SelectDefinition(PS260714EditorAssetUtility.RestoreSelection(
            selectedPath,
            _definitions));
    }

    private void CreateDefinition()
    {
        EnsureAssetFolder();
        EnemySO definition = CreateInstance<EnemySO>();
        definition.RegenerateEnemyId();
        SerializedObject serialized = new(definition);
        serialized.FindProperty("rosterSchemaVersion").intValue =
            EnemySO.CurrentRosterSchemaVersion;
        serialized.FindProperty("rosterTier").enumValueIndex =
            (int)EnemyRosterTier.General;
        SerializedProperty roleTags =
            serialized.FindProperty("roleTags");
        roleTags.arraySize = 1;
        roleTags.GetArrayElementAtIndex(0).stringValue =
            EnemyTypeDisplay.GetId(definition.Type);
        serialized.FindProperty("combatStatSchemaVersion").intValue =
            EnemySO.CurrentCombatStatSchemaVersion;
        serialized.FindProperty("attackPower").floatValue =
            Mathf.Max(
                0.1f,
                serialized.FindProperty("coreAttackDamage").intValue);
        serialized.FindProperty("preciseCoreAttackDamage").floatValue =
            serialized.FindProperty("coreAttackDamage").intValue;
        serialized.FindProperty("formationRadius").floatValue =
            EnemySO.GetDefaultFormationRadius(definition.Type);
        serialized.FindProperty("forwardSearchAngle").floatValue =
            EnemySO.DefaultForwardSearchAngle;
        serialized.FindProperty("coreAttackRange").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            AssetFolder + "/Enemy.asset");
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshList();
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
                out EnemySO duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Enemy",
                duplicateError,
                "OK");
            return;
        }
        if (duplicate != null)
        {
            duplicate.RegenerateEnemyId();
            EditorUtility.SetDirty(duplicate);
        }
        AssetDatabase.SaveAssets();
        RefreshList();
        SelectDefinition(duplicate);
    }

    private void SaveSelected()
    {
        if (_selected == null || _serialized == null)
            return;

        _serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssetIfDirty(_selected);
        EnemyDefinitionCatalog.Invalidate();
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
        _isRenaming = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
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
            EditorUtility.DisplayDialog(
                "Rename Enemy",
                error,
                "OK");
            return;
        }

        CancelRename();
        RefreshList();
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        EnemySO selected = _selected;
        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                selected,
                "EnemySO"))
            return;

        _selected = null;
        _serialized = null;
        RefreshList();
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/06_Runtime/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/06_Runtime/Resources", "Enemies");
    }
}
