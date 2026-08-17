using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class EnemyEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.EnemyEditor;

    private const string AssetFolder = "Assets/06_Runtime/Resources/Enemies";
    private const string RenameControlName = "EnemyAssetRenameField";
    private readonly List<EnemySO> _definitions = new();

    private EnemySO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _validationExpanded = true;
    private bool _identityExpanded = true;
    private bool _statsExpanded = true;
    private bool _presentationExpanded = true;
    private bool _abilitiesExpanded = true;

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

            int visibleCount = 0;
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_listScroll))
            {
                _listScroll = scroll.scrollPosition;
                foreach (EnemySO definition in _definitions)
                {
                    if (definition == null ||
                        !MatchesSearch(definition))
                    {
                        continue;
                    }

                    visibleCount++;
                    bool selected =
                        ReferenceEquals(definition, _selected);
                    string detail =
                        $"{definition.Type} / " +
                        $"A{definition.Abilities.Count}";
                    if (PS260714AssetEditorList.DrawAssetRow(
                            selected,
                            definition,
                            definition.IconSprite,
                            definition.name,
                            detail,
                            definition.EnemyId))
                    {
                        SelectDefinition(definition);
                    }
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        _definitions.Count == 0
                            ? "No EnemySO assets were found."
                            : "No enemies match the search.",
                        MessageType.Info);
                }
            }
            PS260714AssetEditorList.DrawCountFooter(
                visibleCount,
                _definitions.Count);
        }
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
            DrawProperty("attackPower", "Attack Power");
            DrawProperty("coreAttackDamage", "Core Attack Damage");
            DrawProperty("coreAttackInterval", "Core Attack Interval");
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
        DrawRelative(ability, "trigger", "Trigger");
        DrawRelative(ability, "priority", "Priority");

        SerializedProperty trigger =
            ability.FindPropertyRelative("trigger");
        if (trigger.enumValueIndex ==
            (int)EnemyAbilityTrigger.OnCooldown)
        {
            DrawRelative(ability, "cooldown", "Cooldown");
            DrawRelative(
                ability,
                "cooldownResetPolicy",
                "Cooldown Reset");
            DrawRelative(
                ability,
                "pauseCooldownWhileDisabled",
                "Pause While Disabled");
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
                using (new EditorGUI.DisabledScope(true))
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
        switch (type)
        {
            case EnemyAbilityOperationType.ModifySpawnInterval:
                DrawRelative(operation, "multiplier", "Interval Multiplier");
                break;

            case EnemyAbilityOperationType.ModifyIncomingDamage:
                DrawRelative(operation, "amount", "Resolved Damage");
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
        SetString(ability, "fallbackName", "New Ability");
        SetString(ability, "fallbackDescription", string.Empty);
        SetEnum(
            ability,
            "trigger",
            (int)EnemyAbilityTrigger.OnCooldown);
        SetInt(ability, "priority", 0);
        SetFloat(ability, "cooldown", 1f);
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
               definition.DisplayName.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               definition.Type.ToString().IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
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
        serialized.FindProperty("combatStatSchemaVersion").intValue =
            EnemySO.CurrentCombatStatSchemaVersion;
        serialized.FindProperty("attackPower").floatValue =
            Mathf.Max(
                0.1f,
                serialized.FindProperty("coreAttackDamage").intValue);
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
