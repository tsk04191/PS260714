using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class EnemyEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.EnemyEditor;

    private const string AssetFolder = "Assets/Data/Enemies";
    private const string RenameControlName = "EnemyAssetRenameField";
    private const float ListWidth = 230f;

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

    [MenuItem(MenuPath)]
    public static void Open()
    {
        EnemyEditorWindow window = GetWindow<EnemyEditorWindow>();
        window.titleContent = new GUIContent("Enemy Editor");
        window.minSize = new Vector2(900f, 600f);
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
        titleContent = new GUIContent("Enemy Editor");
        minSize = new Vector2(900f, 600f);
        RefreshList();

        if (Selection.activeObject is EnemySO selected)
            SelectDefinition(selected, false);
        else if (_selected == null && _definitions.Count > 0)
            SelectDefinition(_definitions[0], false);
    }

    private void OnProjectChange()
    {
        EnemyDefinitionCatalog.Invalidate();
        RefreshList();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is EnemySO selected)
        {
            SelectDefinition(selected, false);
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
            RefreshList);
    }

    private void DrawRenameRow()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "SO File Name",
                GUILayout.Width(88f));
            GUI.SetNextControlName(RenameControlName);
            _renameAssetName =
                EditorGUILayout.TextField(_renameAssetName);
            bool apply = GUILayout.Button("Apply", GUILayout.Width(56f));
            bool cancel = GUILayout.Button("Cancel", GUILayout.Width(56f));

            if (_focusRenameField)
            {
                EditorGUI.FocusTextInControl(RenameControlName);
                _focusRenameField = false;
            }

            Event current = Event.current;
            if (current.type == EventType.KeyDown &&
                GUI.GetNameOfFocusedControl() == RenameControlName)
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
    }

    private void DrawAssetList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(ListWidth),
                   GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.Space(4f);
            _searchText = EditorGUILayout.TextField(
                _searchText,
                EditorStyles.toolbarSearchField);
            EditorGUILayout.Space(4f);

            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_listScroll))
            {
                _listScroll = scroll.scrollPosition;
                int visibleCount = 0;
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
                    string label =
                        $"{definition.name}\n" +
                        $"{definition.Type} / " +
                        $"A{definition.Abilities.Count}";
                    if (GUILayout.Toggle(
                            selected,
                            label,
                            "Button",
                            GUILayout.Height(42f)) &&
                        !selected)
                    {
                        SelectDefinition(definition, true);
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

            if (GUILayout.Button(
                    "Select in Project",
                    GUILayout.Width(112f)))
            {
                Selection.activeObject = _selected;
                EditorGUIUtility.PingObject(_selected);
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

            DrawProperty(
                "nameLocalizationKey",
                "Name Localization Key");
            DrawProperty(
                "descriptionLocalizationKey",
                "Description Localization Key");
            DrawProperty("displayName", "Fallback Name");
            DrawProperty("description", "Fallback Description");
            DrawProperty("cardCode", "Card Code");
            DrawProperty("grade", "Grade");
            DrawProperty("type", "Type");
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
            DrawProperty(
                "spawnIntervalMultiplier",
                "Base Spawn Interval Multiplier");
            DrawProperty("threatCost", "Threat Cost (0 = Type Default)");
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
                    DrawAbility(ability);
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
            "Battle Lifecycle 3D VFX",
            true,
            EditorStyles.foldoutHeader);
        if (!_presentationExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawProperty("spawnVfxCue", "Spawn VFX Cue");
            DrawProperty("deathVfxCue", "Death VFX Cue");
            EditorGUILayout.HelpBox(
                "Spawn plays after the enemy card is placed. Death uses the cached card anchor after removal.",
                MessageType.Info);
        }
    }

    private static void DrawAbility(SerializedProperty ability)
    {
        DrawRelative(ability, "abilityId", "Ability ID");
        DrawRelative(
            ability,
            "nameLocalizationKey",
            "Name Localization Key");
        DrawRelative(
            ability,
            "descriptionLocalizationKey",
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
        DrawOperations(ability, (EnemyAbilityTrigger)trigger.enumValueIndex);
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
                        DrawRelative(
                            condition,
                            "statusEffect",
                            "Status Effect");
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
        }
    }

    private static void DrawOperations(
        SerializedProperty ability,
        EnemyAbilityTrigger trigger)
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
                DrawRelative(operation, "type", "Type");
                EnemyAbilityOperationType type =
                    (EnemyAbilityOperationType)operation
                        .FindPropertyRelative("type").enumValueIndex;
                switch (type)
                {
                    case EnemyAbilityOperationType.ExecuteEffects:
                        DrawEffects(
                            operation.FindPropertyRelative("effects"));
                        break;
                    case EnemyAbilityOperationType.ModifySpawnInterval:
                        DrawRelative(
                            operation,
                            "multiplier",
                            "Multiplier");
                        break;
                    case EnemyAbilityOperationType.ModifyIncomingDamage:
                        DrawRelative(
                            operation,
                            "amount",
                            "Resulting Damage");
                        break;
                    case EnemyAbilityOperationType.ExpandSpawnGroup:
                        DrawRelative(
                            operation,
                            "count",
                            "Additional Enemies");
                        break;
                    case EnemyAbilityOperationType.GrantArmor:
                        DrawRelative(
                            operation,
                            "amount",
                            "Fixed Armor");
                        DrawRelative(
                            operation,
                            "multiplier",
                            "Max Health Multiplier");
                        break;
                    case EnemyAbilityOperationType.RedirectDamage:
                        DrawRelative(operation, "range", "Range");
                        DrawRelative(
                            operation,
                            "includeDiagonals",
                            "Include Diagonals");
                        break;
                }
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

    private static void DrawEffects(SerializedProperty effects)
    {
        int removeIndex = -1;
        for (int index = 0; index < effects.arraySize; index++)
        {
            SerializedProperty effect =
                effects.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"Effect {index + 1}",
                        EditorStyles.miniBoldLabel);
                    if (GUILayout.Button(
                            "Remove",
                            EditorStyles.miniButton,
                            GUILayout.Width(58f)))
                    {
                        removeIndex = index;
                    }
                }
                EditorGUILayout.PropertyField(
                    effect,
                    GUIContent.none,
                    true);
            }
        }

        if (removeIndex >= 0)
            effects.DeleteArrayElementAtIndex(removeIndex);
        if (GUILayout.Button("Add Effect", EditorStyles.miniButton))
            AddEffect(effects);
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
        SetEnum(
            condition,
            "incomingDamageType",
            (int)CharacterAttackDamageType.Physical);
        SetBool(condition, "expected", true);
    }

    private static void AddOperation(
        SerializedProperty operations,
        EnemyAbilityTrigger trigger)
    {
        if (operations == null)
            return;

        EnemyAbilityOperationType operationType = trigger switch
        {
            EnemyAbilityTrigger.OnSpawn =>
                EnemyAbilityOperationType.GrantArmor,
            EnemyAbilityTrigger.BeforeSelfDamage =>
                EnemyAbilityOperationType.ModifyIncomingDamage,
            EnemyAbilityTrigger.BeforeAllyDamage =>
                EnemyAbilityOperationType.RedirectDamage,
            EnemyAbilityTrigger.OnSpawnQueueEvaluation =>
                EnemyAbilityOperationType.ModifySpawnInterval,
            EnemyAbilityTrigger.OnTargetPriorityEvaluation =>
                EnemyAbilityOperationType.ModifyTargetPriority,
            _ => EnemyAbilityOperationType.ExecuteEffects
        };

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
        SerializedProperty effects =
            operation.FindPropertyRelative("effects");
        effects.ClearArray();
        if (operationType == EnemyAbilityOperationType.ExecuteEffects)
            AddEffect(effects);
    }

    private static void AddEffect(SerializedProperty effects)
    {
        if (effects == null)
            return;

        int index = effects.arraySize;
        effects.InsertArrayElementAtIndex(index);
        SerializedProperty effect =
            effects.GetArrayElementAtIndex(index);
        effect.isExpanded = true;
        SetEnum(
            effect,
            "type",
            (int)CharacterEffectType.Damage);
        SetEnum(
            effect,
            "targetMode",
            (int)CharacterEffectTargetMode.InheritAction);
        SetEnum(
            effect,
            "preconditionFailurePolicy",
            (int)CharacterEffectPreconditionFailurePolicy.AbortAction);
        SetEnum(
            effect,
            "failurePolicy",
            (int)CharacterEffectFailurePolicy.Continue);
        SetEnum(
            effect,
            "damageType",
            (int)CharacterAttackDamageType.Physical);
        SetEnum(
            effect,
            "damageAmountMode",
            (int)CharacterDamageAmountMode.Fixed);
        SetFloat(effect, "damageAmount", 1f);
        SetFloat(effect, "sourceResourceScale", 0f);
        SetFloat(effect, "targetCurrentHealthScale", 0f);
        SetFloat(effect, "targetMaxHealthScale", 0f);
        SetObject(effect, "sourceStatusScalingEffect", null);
        SetFloat(effect, "sourceStatusStacksScale", 0f);
        SetObject(effect, "targetStatusScalingEffect", null);
        SetFloat(effect, "targetStatusStacksScale", 0f);
        SetFloat(effect, "statusDuration", 1f);
        SetFloat(effect, "statusStacks", 1f);
        SetObject(effect, "statusEffect", null);
        SetEnum(
            effect,
            "statusRemovalTarget",
            (int)CharacterStatusRemovalTarget.Single);
        SetEnum(
            effect,
            "statusRemovalAmountMode",
            (int)CharacterStatusRemovalAmountMode.FixedStacks);
        SetInt(effect, "statusRemovalCount", 0);
        SetFloat(effect, "statusRemovalRatio", 0.5f);
        SetObject(effect, "castVfxCue", null);
        SetObject(effect, "projectileVfxCue", null);
        SetObject(effect, "impactVfxCue", null);

        SerializedProperty selector =
            effect.FindPropertyRelative("targetSelector");
        if (selector != null)
        {
            SetEnum(
                selector,
                "targetFaction",
                (int)CharacterTargetFaction.Enemy);
            SetEnum(
                selector,
                "subject",
                (int)CharacterAttackSubject.Random);
            SetEnum(
                selector,
                "subjectMetric",
                (int)CharacterAttackSubjectMetric.Health);
            SetInt(selector, "subjectCount", 1);
            SetEnum(
                selector,
                "conditionMatchMode",
                (int)CharacterConditionMatchMode.All);
            selector.FindPropertyRelative(
                "numericConditions")?.ClearArray();
            selector.FindPropertyRelative("areaOffsets")?.ClearArray();
        }
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

    private void SelectDefinition(EnemySO definition, bool pingProject)
    {
        if (definition == null)
            return;

        CancelRename();
        _selected = definition;
        _serialized = new SerializedObject(definition);
        _editorScroll = Vector2.zero;
        if (pingProject)
        {
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }
    }

    private void RefreshList()
    {
        string selectedPath = _selected != null
            ? AssetDatabase.GetAssetPath(_selected)
            : string.Empty;
        _definitions.Clear();
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:EnemySO",
                     new[] { AssetFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (definition != null)
                _definitions.Add(definition);
        }

        _definitions.Sort((left, right) => string.Compare(
            left.name,
            right.name,
            StringComparison.OrdinalIgnoreCase));
        EnemyDefinitionCatalog.Invalidate();

        if (!string.IsNullOrEmpty(selectedPath))
        {
            EnemySO restored =
                AssetDatabase.LoadAssetAtPath<EnemySO>(selectedPath);
            if (restored != null)
                SelectDefinition(restored, false);
        }
        else if (_selected == null && _definitions.Count > 0)
        {
            SelectDefinition(_definitions[0], false);
        }
    }

    private void CreateDefinition()
    {
        EnsureAssetFolder();
        EnemySO definition = CreateInstance<EnemySO>();
        definition.RegenerateEnemyId();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            AssetFolder + "/Enemy.asset");
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshList();
        SelectDefinition(definition, true);
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        EnsureAssetFolder();
        string sourcePath = AssetDatabase.GetAssetPath(_selected);
        string destination = AssetDatabase.GenerateUniqueAssetPath(
            AssetFolder + "/" + _selected.name + "_Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destination))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Enemy",
                "Failed to duplicate the selected EnemySO.",
                "OK");
            return;
        }

        AssetDatabase.ImportAsset(destination);
        EnemySO duplicate =
            AssetDatabase.LoadAssetAtPath<EnemySO>(destination);
        if (duplicate != null)
        {
            duplicate.RegenerateEnemyId();
            EditorUtility.SetDirty(duplicate);
        }
        AssetDatabase.SaveAssets();
        RefreshList();
        SelectDefinition(duplicate, true);
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

        string requested = (_renameAssetName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(requested) ||
            requested.IndexOfAny(
                System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            EditorUtility.DisplayDialog(
                "Rename Enemy",
                "Enter a valid file name.",
                "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(_selected);
        string error = AssetDatabase.RenameAsset(path, requested);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog(
                "Rename Enemy",
                error,
                "OK");
            return;
        }

        CancelRename();
        AssetDatabase.SaveAssets();
        RefreshList();
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        string path = AssetDatabase.GetAssetPath(_selected);
        if (!EditorUtility.DisplayDialog(
                "Delete Enemy",
                $"Delete '{_selected.name}'?\n\n{path}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        _selected = null;
        _serialized = null;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        RefreshList();
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Enemies");
    }
}
