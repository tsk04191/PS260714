using System;
using System.Collections.Generic;
using PS260714.Localization;

public enum EnemyDefinitionDiagnosticSeverity
{
    Warning = 0,
    Error = 1
}

public readonly struct EnemyDefinitionDiagnostic
{
    public EnemyDefinitionDiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Path { get; }
    public string Message { get; }

    public EnemyDefinitionDiagnostic(
        EnemyDefinitionDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Path = path ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public override string ToString()
    {
        string location = string.IsNullOrWhiteSpace(Path)
            ? "<root>"
            : Path;
        return $"{Severity} [{Code}] {location}: {Message}";
    }
}

public sealed class EnemyDefinitionValidationResult
{
    private readonly List<EnemyDefinitionDiagnostic> _diagnostics = new();

    public IReadOnlyList<EnemyDefinitionDiagnostic> Diagnostics =>
        _diagnostics;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool IsValid => ErrorCount == 0;

    internal void Add(
        EnemyDefinitionDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        _diagnostics.Add(new EnemyDefinitionDiagnostic(
            severity,
            code,
            path,
            message));
        if (severity == EnemyDefinitionDiagnosticSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    internal void Add(
        EnemyDefinitionDiagnostic diagnostic,
        string pathPrefix)
    {
        string path = string.IsNullOrWhiteSpace(diagnostic.Path)
            ? pathPrefix
            : $"{pathPrefix}.{diagnostic.Path}";
        Add(
            diagnostic.Severity,
            diagnostic.Code,
            path,
            diagnostic.Message);
    }
}

public static class EnemyDefinitionValidator
{
    public static EnemyDefinitionValidationResult Validate(
        EnemySO definition)
    {
        return Validate(definition, null);
    }

    public static EnemyDefinitionValidationResult Validate(
        EnemySO definition,
        IReadOnlyList<EnemySO> catalog)
    {
        EnemyDefinitionValidationResult result = new();
        ValidateDefinition(definition, result);
        if (definition != null && catalog != null)
            ValidateDuplicateId(definition, catalog, result);
        return result;
    }

    public static EnemyDefinitionValidationResult ValidateAll(
        IReadOnlyList<EnemySO> definitions)
    {
        EnemyDefinitionValidationResult result = new();
        if (definitions == null)
        {
            AddError(
                result,
                "catalog.null",
                "enemies",
                "Enemy definition catalog is null.");
            return result;
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            EnemyDefinitionValidationResult definitionResult =
                Validate(definitions[index]);
            foreach (EnemyDefinitionDiagnostic diagnostic in
                     definitionResult.Diagnostics)
            {
                result.Add(diagnostic, $"enemies[{index}]");
            }
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Count; index++)
        {
            EnemySO definition = definitions[index];
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.EnemyId))
            {
                continue;
            }

            if (!ids.Add(definition.EnemyId))
            {
                AddError(
                    result,
                    "enemy.id_duplicate",
                    $"enemies[{index}].enemyId",
                    $"EnemyId '{definition.EnemyId}' is duplicated.");
            }
        }

        return result;
    }

    private static void ValidateDefinition(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        if (definition == null)
        {
            AddError(
                result,
                "enemy.null",
                "enemy",
                "Enemy definition is null.");
            return;
        }

        ValidateIdentity(definition, result);
        ValidateBaseData(definition, result);
        ValidateAbilities(definition.Abilities, result);
    }

    private static void ValidateIdentity(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(definition.EnemyId))
        {
            AddError(
                result,
                "enemy.id_missing",
                "enemyId",
                "A persistent EnemyId is required.");
        }
        else if (ContainsWhitespace(definition.EnemyId))
        {
            AddError(
                result,
                "enemy.id_whitespace",
                "enemyId",
                "EnemyId cannot contain whitespace.");
        }

        ValidateLocalization(
            definition.NameLocalizationKey,
            definition.DisplayName,
            "nameLocalizationKey",
            true,
            result);
        ValidateLocalization(
            definition.DescriptionLocalizationKey,
            definition.Description,
            "descriptionLocalizationKey",
            false,
            result);
    }

    private static void ValidateBaseData(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(typeof(EEnemyGrade), definition.Grade))
        {
            AddError(
                result,
                "enemy.grade_invalid",
                "grade",
                $"Enemy grade '{definition.Grade}' is unsupported.");
        }

        if (!Enum.IsDefined(typeof(EEnemyType), definition.Type))
        {
            AddError(
                result,
                "enemy.type_invalid",
                "type",
                $"Enemy type '{definition.Type}' is unsupported.");
        }

        if (definition.BaseHealth <= 0)
        {
            AddError(
                result,
                "enemy.health_invalid",
                "baseHealth",
                "Base health must be at least one.");
        }

        if (!IsFinite(definition.ThreatCost) ||
            definition.ThreatCost <= 0f)
        {
            AddError(
                result,
                "enemy.threat_invalid",
                "threatCost",
                "Resolved threat cost must be finite and greater than zero.");
        }
    }

    private static void ValidateAbilities(
        IReadOnlyList<EnemyAbilityDefinition> abilities,
        EnemyDefinitionValidationResult result)
    {
        if (abilities == null)
            return;

        HashSet<string> abilityIds = new(StringComparer.Ordinal);
        for (int index = 0; index < abilities.Count; index++)
        {
            EnemyAbilityDefinition ability = abilities[index];
            string path = $"abilities[{index}]";
            if (ability == null)
            {
                AddError(
                    result,
                    "ability.null",
                    path,
                    "Ability definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(ability.AbilityId))
            {
                AddError(
                    result,
                    "ability.id_missing",
                    $"{path}.abilityId",
                    "AbilityId is required.");
            }
            else
            {
                if (ContainsWhitespace(ability.AbilityId))
                {
                    AddError(
                        result,
                        "ability.id_whitespace",
                        $"{path}.abilityId",
                        "AbilityId cannot contain whitespace.");
                }
                if (!abilityIds.Add(ability.AbilityId))
                {
                    AddError(
                        result,
                        "ability.id_duplicate",
                        $"{path}.abilityId",
                        $"AbilityId '{ability.AbilityId}' is duplicated.");
                }
            }

            ValidateLocalization(
                ability.NameLocalizationKey,
                ability.FallbackName,
                $"{path}.nameLocalizationKey",
                false,
                result);
            ValidateLocalization(
                ability.DescriptionLocalizationKey,
                ability.FallbackDescription,
                $"{path}.descriptionLocalizationKey",
                false,
                result);

            if (!Enum.IsDefined(
                    typeof(EnemyAbilityTrigger),
                    ability.Trigger))
            {
                AddError(
                    result,
                    "ability.trigger_invalid",
                    $"{path}.trigger",
                    $"Ability trigger '{ability.Trigger}' is unsupported.");
            }

            if (ability.Trigger == EnemyAbilityTrigger.OnCooldown &&
                ability.Cooldown <= 0f)
            {
                AddError(
                    result,
                    "ability.cooldown_required",
                    $"{path}.cooldown",
                    "OnCooldown abilities require a positive cooldown.");
            }
            else if (ability.Trigger != EnemyAbilityTrigger.OnCooldown &&
                     ability.Cooldown > 0f)
            {
                AddWarning(
                    result,
                    "ability.cooldown_unused",
                    $"{path}.cooldown",
                    "Cooldown is only used by OnCooldown abilities.");
            }

            ValidateTarget(ability.Target, path, result);
            ValidateConditions(ability, path, result);
            ValidateOperations(ability, path, result);
        }
    }

    private static void ValidateTarget(
        EnemyAbilityTargetDefinition target,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        if (target == null)
        {
            AddError(
                result,
                "ability.target_null",
                $"{abilityPath}.target",
                "Target definition is null.");
            return;
        }

        if (!Enum.IsDefined(
                typeof(EnemyAbilityTargetFaction),
                target.Faction))
        {
            AddError(
                result,
                "ability.target_faction_invalid",
                $"{abilityPath}.target.faction",
                $"Target faction '{target.Faction}' is unsupported.");
        }
        if (!Enum.IsDefined(
                typeof(EnemyAbilityTargetSubject),
                target.Subject))
        {
            AddError(
                result,
                "ability.target_subject_invalid",
                $"{abilityPath}.target.subject",
                $"Target subject '{target.Subject}' is unsupported.");
        }
        if (!Enum.IsDefined(
                typeof(EnemyAbilityTargetMetric),
                target.Metric))
        {
            AddError(
                result,
                "ability.target_metric_invalid",
                $"{abilityPath}.target.metric",
                $"Target metric '{target.Metric}' is unsupported.");
        }

        bool factionConfigured =
            target.Faction != EnemyAbilityTargetFaction.None;
        bool subjectConfigured =
            target.Subject != EnemyAbilityTargetSubject.None;
        if (factionConfigured != subjectConfigured)
        {
            AddError(
                result,
                "ability.target_incomplete",
                $"{abilityPath}.target",
                "Target faction and subject must either both be configured " +
                "or both be None.");
        }
    }

    private static void ValidateConditions(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        IReadOnlyList<EnemyAbilityConditionDefinition> conditions =
            ability.Conditions;
        for (int index = 0; index < conditions.Count; index++)
        {
            EnemyAbilityConditionDefinition condition = conditions[index];
            string path = $"{abilityPath}.conditions[{index}]";
            if (condition == null)
            {
                AddError(
                    result,
                    "ability.condition_null",
                    path,
                    "Condition definition is null.");
                continue;
            }

            if (!Enum.IsDefined(
                    typeof(EnemyAbilityConditionType),
                    condition.Type))
            {
                AddError(
                    result,
                    "ability.condition_type_invalid",
                    $"{path}.type",
                    $"Condition type '{condition.Type}' is unsupported.");
            }
            if ((condition.Type ==
                    EnemyAbilityConditionType.SourceHasStatus ||
                 condition.Type ==
                    EnemyAbilityConditionType.TargetHasStatus) &&
                condition.StatusEffect == null)
            {
                AddError(
                    result,
                    "ability.condition_status_missing",
                    $"{path}.statusEffect",
                    "A status condition requires a StatusEffectSO.");
            }
            if (condition.Type ==
                    EnemyAbilityConditionType.IncomingDamageType &&
                ability.Trigger != EnemyAbilityTrigger.BeforeSelfDamage &&
                ability.Trigger != EnemyAbilityTrigger.BeforeAllyDamage)
            {
                AddError(
                    result,
                    "ability.condition_damage_trigger_mismatch",
                    path,
                    "IncomingDamageType can only be used by a " +
                    "before-damage trigger.");
            }
        }
    }

    private static void ValidateOperations(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        IReadOnlyList<EnemyAbilityOperationDefinition> operations =
            ability.Operations;
        if (operations.Count == 0)
        {
            AddError(
                result,
                "ability.operations_empty",
                $"{abilityPath}.operations",
                "An ability requires at least one operation.");
            return;
        }

        for (int index = 0; index < operations.Count; index++)
        {
            EnemyAbilityOperationDefinition operation = operations[index];
            string path = $"{abilityPath}.operations[{index}]";
            if (operation == null)
            {
                AddError(
                    result,
                    "ability.operation_null",
                    path,
                    "Operation definition is null.");
                continue;
            }

            if (!Enum.IsDefined(
                    typeof(EnemyAbilityOperationType),
                    operation.Type))
            {
                AddError(
                    result,
                    "ability.operation_type_invalid",
                    $"{path}.type",
                    $"Operation type '{operation.Type}' is unsupported.");
                continue;
            }

            if (!IsCompatible(ability.Trigger, operation.Type))
            {
                AddError(
                    result,
                    "ability.operation_trigger_mismatch",
                    path,
                    $"{operation.Type} is incompatible with " +
                    $"{ability.Trigger}.");
            }

            ValidateOperationValues(operation, path, result);
        }
    }

    private static void ValidateOperationValues(
        EnemyAbilityOperationDefinition operation,
        string path,
        EnemyDefinitionValidationResult result)
    {
        switch (operation.Type)
        {
            case EnemyAbilityOperationType.ExecuteEffects:
                if (operation.Effects.Count == 0)
                {
                    AddError(
                        result,
                        "ability.effects_empty",
                        $"{path}.effects",
                        "ExecuteEffects requires at least one effect.");
                }
                for (int index = 0;
                     index < operation.Effects.Count;
                     index++)
                {
                    if (operation.Effects[index] == null)
                    {
                        AddError(
                            result,
                            "ability.effect_null",
                            $"{path}.effects[{index}]",
                            "Effect definition is null.");
                    }
                }
                break;

            case EnemyAbilityOperationType.ModifySpawnInterval:
                if (!IsFinite(operation.Multiplier) ||
                    operation.Multiplier <= 0f)
                {
                    AddError(
                        result,
                        "ability.multiplier_invalid",
                        $"{path}.multiplier",
                        "Spawn interval multiplier must be finite and " +
                        "greater than zero.");
                }
                break;

            case EnemyAbilityOperationType.ModifyIncomingDamage:
                if (operation.Amount < 0)
                {
                    AddError(
                        result,
                        "ability.amount_invalid",
                        $"{path}.amount",
                        "Incoming damage amount cannot be negative.");
                }
                break;

            case EnemyAbilityOperationType.ExpandSpawnGroup:
                if (operation.Count <= 0)
                {
                    AddError(
                        result,
                        "ability.count_invalid",
                        $"{path}.count",
                        "Spawn group expansion must be at least one.");
                }
                break;

            case EnemyAbilityOperationType.GrantArmor:
                if (operation.Amount <= 0 &&
                    operation.Multiplier <= 0f)
                {
                    AddError(
                        result,
                        "ability.armor_value_missing",
                        path,
                        "GrantArmor requires a positive fixed amount or " +
                        "maximum-health multiplier.");
                }
                break;

            case EnemyAbilityOperationType.RedirectDamage:
                if (operation.Range <= 0)
                {
                    AddError(
                        result,
                        "ability.range_invalid",
                        $"{path}.range",
                        "Redirect range must be at least one.");
                }
                break;

            case EnemyAbilityOperationType.ModifyTargetPriority:
                if (!Enum.IsDefined(
                        typeof(EnemyTargetPriorityMode),
                        operation.TargetPriorityMode))
                {
                    AddError(
                        result,
                        "ability.target_priority_mode_invalid",
                        $"{path}.targetPriorityMode",
                        $"Target priority mode " +
                        $"'{operation.TargetPriorityMode}' is unsupported.");
                }
                else if (operation.TargetPriorityMode ==
                             EnemyTargetPriorityMode.Adjust &&
                         operation.TargetPriorityAdjustment == 0)
                {
                    AddError(
                        result,
                        "ability.target_priority_adjustment_missing",
                        $"{path}.targetPriorityAdjustment",
                        "Adjust target priority requires a non-zero " +
                        "priority adjustment.");
                }
                break;
        }
    }

    private static bool IsCompatible(
        EnemyAbilityTrigger trigger,
        EnemyAbilityOperationType operation)
    {
        return operation switch
        {
            EnemyAbilityOperationType.ModifySpawnInterval =>
                trigger == EnemyAbilityTrigger.OnSpawnQueueEvaluation,
            EnemyAbilityOperationType.ModifyIncomingDamage =>
                trigger == EnemyAbilityTrigger.BeforeSelfDamage,
            EnemyAbilityOperationType.ExpandSpawnGroup =>
                trigger == EnemyAbilityTrigger.OnSpawnQueueEvaluation,
            EnemyAbilityOperationType.GrantArmor =>
                trigger == EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityOperationType.RedirectDamage =>
                trigger == EnemyAbilityTrigger.BeforeAllyDamage,
            EnemyAbilityOperationType.ModifyTargetPriority =>
                trigger == EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityOperationType.ExecuteEffects =>
                trigger != EnemyAbilityTrigger.OnSpawnQueueEvaluation &&
                trigger != EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            _ => false
        };
    }

    private static void ValidateLocalization(
        string localizationKey,
        string fallbackText,
        string path,
        bool required,
        EnemyDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            if (string.IsNullOrWhiteSpace(fallbackText))
            {
                if (required)
                {
                    AddError(
                        result,
                        "localization.text_missing",
                        path,
                        "A localization key or fallback text is required.");
                }
                else
                {
                    AddWarning(
                        result,
                        "localization.text_missing",
                        path,
                        "No localization key or fallback text is configured.");
                }
            }
            else
            {
                AddWarning(
                    result,
                    "localization.key_missing",
                    path,
                    "Only fallback text is configured.");
            }
            return;
        }

        if (!GeneratedLocalizationTables.ReferenceEntries.ContainsKey(
                localizationKey))
        {
            AddError(
                result,
                "localization.key_unknown",
                path,
                $"Localization key '{localizationKey}' does not exist.");
        }
    }

    private static void ValidateDuplicateId(
        EnemySO definition,
        IReadOnlyList<EnemySO> catalog,
        EnemyDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(definition.EnemyId))
            return;

        foreach (EnemySO other in catalog)
        {
            if (other == null || ReferenceEquals(other, definition))
                continue;
            if (!string.Equals(
                    other.EnemyId,
                    definition.EnemyId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            AddError(
                result,
                "enemy.id_duplicate",
                "enemyId",
                $"EnemyId '{definition.EnemyId}' is duplicated.");
            return;
        }
    }

    private static bool ContainsWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
                return true;
        }
        return false;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void AddError(
        EnemyDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            EnemyDefinitionDiagnosticSeverity.Error,
            code,
            path,
            message);
    }

    private static void AddWarning(
        EnemyDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            EnemyDefinitionDiagnosticSeverity.Warning,
            code,
            path,
            message);
    }
}
