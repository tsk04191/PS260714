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

        ValidateCatalogReferences(definitions, ids, result);

        return result;
    }

    private static void ValidateCatalogReferences(
        IReadOnlyList<EnemySO> definitions,
        ISet<string> knownIds,
        EnemyDefinitionValidationResult result)
    {
        for (int enemyIndex = 0;
             enemyIndex < definitions.Count;
             enemyIndex++)
        {
            EnemySO definition = definitions[enemyIndex];
            if (definition == null)
                continue;
            for (int abilityIndex = 0;
                 abilityIndex < definition.Abilities.Count;
                 abilityIndex++)
            {
                EnemyAbilityDefinition ability =
                    definition.Abilities[abilityIndex];
                if (ability == null)
                    continue;
                string abilityPath =
                    $"enemies[{enemyIndex}].abilities[{abilityIndex}]";

                for (int parameterIndex = 0;
                     parameterIndex < ability.Parameters.Count;
                     parameterIndex++)
                {
                    EnemyAbilityParameterDefinition parameter =
                        ability.Parameters[parameterIndex];
                    if (parameter == null ||
                        parameter.ValueType !=
                            EnemyAbilityParameterValueType.EnemyReference)
                    {
                        continue;
                    }
                    ValidateCatalogReference(
                        parameter.EnemyReference,
                        knownIds,
                        $"{abilityPath}.parameters[{parameterIndex}]" +
                        ".enemyReference",
                        result);
                }

                for (int operationIndex = 0;
                     operationIndex < ability.Operations.Count;
                     operationIndex++)
                {
                    EnemyAbilityOperationDefinition operation =
                        ability.Operations[operationIndex];
                    if (operation == null)
                        continue;
                    string operationPath =
                        $"{abilityPath}.operations[{operationIndex}]";
                    if (operation.Reference.IsConfigured)
                    {
                        ValidateCatalogReference(
                            operation.Reference,
                            knownIds,
                            $"{operationPath}.reference",
                            result);
                    }
                    if (operation.Type !=
                        EnemyAbilityOperationType.SummonEnemy)
                    {
                        continue;
                    }
                    for (int candidateIndex = 0;
                         candidateIndex < operation.Summon.Candidates.Count;
                         candidateIndex++)
                    {
                        ValidateCatalogReference(
                            operation.Summon.Candidates[candidateIndex],
                            knownIds,
                            $"{operationPath}.summon.candidates" +
                            $"[{candidateIndex}]",
                            result);
                    }
                }
            }
        }
    }

    private static void ValidateCatalogReference(
        EnemyReferenceDefinition reference,
        ISet<string> knownIds,
        string path,
        EnemyDefinitionValidationResult result)
    {
        if (reference == null ||
            string.IsNullOrWhiteSpace(reference.ResolvedEnemyId))
        {
            return;
        }
        if (reference.Enemy != null &&
            !string.IsNullOrWhiteSpace(reference.EnemyId) &&
            !string.Equals(
                reference.Enemy.EnemyId,
                reference.EnemyId,
                StringComparison.Ordinal))
        {
            AddError(
                result,
                "enemy.reference_id_mismatch",
                path,
                $"EnemySO reference ID '{reference.Enemy.EnemyId}' " +
                $"does not match authored ID '{reference.EnemyId}'.");
            return;
        }
        if (knownIds.Contains(reference.ResolvedEnemyId))
            return;

        AddError(
            result,
            "enemy.reference_unknown",
            path,
            $"Enemy reference '{reference.ResolvedEnemyId}' does not " +
            "exist in this catalog.");
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
        ValidateRosterMetadata(definition, result);
        ValidateBaseData(definition, result);
        ValidatePresentation(definition, result);
        ValidateAbilities(definition.Abilities, result);
        ValidateBossPhases(definition, result);
    }

    private static void ValidatePresentation(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        if (definition.BoardSprite == null)
        {
            AddWarning(
                result,
                "enemy.board_sprite_missing",
                "boardSprite",
                "An enemy-specific Board Sprite is required for " +
                "dungeon world rendering.");
        }
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

    private static void ValidateRosterMetadata(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        if (definition.AuthoredRosterSchemaVersion < 0 ||
            definition.AuthoredRosterSchemaVersion >
                EnemySO.CurrentRosterSchemaVersion)
        {
            AddError(
                result,
                "enemy.roster_schema_unsupported",
                "rosterSchemaVersion",
                $"Roster schema {definition.AuthoredRosterSchemaVersion} " +
                $"is unsupported; current schema is " +
                $"{EnemySO.CurrentRosterSchemaVersion}.");
        }
        else if (definition.AuthoredRosterSchemaVersion <
                 EnemySO.CurrentRosterSchemaVersion)
        {
            AddWarning(
                result,
                "enemy.roster_schema_outdated",
                "rosterSchemaVersion",
                "Run Tools/PS260714/Migrations/Migrate Enemy Roster " +
                "Metadata.");
        }

        if (!Enum.IsDefined(
                typeof(EnemyRosterTier),
                definition.AuthoredRosterTier))
        {
            AddError(
                result,
                "enemy.roster_tier_invalid",
                "rosterTier",
                $"Roster tier '{definition.AuthoredRosterTier}' is " +
                "unsupported.");
        }

        ValidateTags(
            definition.RoleTags,
            "roleTags",
            definition.AuthoredRosterSchemaVersion > 0,
            result);
        ValidateTags(
            definition.CounterTags,
            "counterTags",
            false,
            result);

        if (definition.AuthoredRecommendedMaxPerWave < 0)
        {
            AddError(
                result,
                "enemy.wave_cap_invalid",
                "recommendedMaxPerWave",
                "Recommended maximum per wave cannot be negative.");
        }

        if (!IsFinite(definition.AuthoredSpawnBudget) ||
            definition.AuthoredSpawnBudget < 0f)
        {
            AddError(
                result,
                "enemy.spawn_budget_invalid",
                "spawnBudget",
                "Spawn budget must be finite and cannot be negative.");
        }

        if (definition.RosterTier == EnemyRosterTier.Boss &&
            !definition.EncounterOnly)
        {
            AddWarning(
                result,
                "enemy.boss_not_encounter_only",
                "encounterOnly",
                "Boss roster entries should be limited to dedicated " +
                "encounters.");
        }
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

        if (!IsFinite(definition.AuthoredHealthScale) ||
            definition.AuthoredHealthScale < 0.1f)
        {
            AddError(
                result,
                "enemy.health_scale_invalid",
                "healthScale",
                "Health scale must be finite and at least 0.1.");
        }

        if (definition.AuthoredInitialArmor < 0 ||
            definition.AuthoredInitialShield < 0)
        {
            AddError(
                result,
                "enemy.initial_defense_invalid",
                "initialArmor",
                "Initial armor and shield cannot be negative.");
        }

        if (!IsFinite(definition.AuthoredApproachSpeed) ||
            definition.AuthoredApproachSpeed <= 0f)
        {
            AddError(
                result,
                "enemy.approach_speed_invalid",
                "approachSpeed",
                "Circular approach speed must be finite and greater than zero.");
        }

        if (!IsFinite(definition.AuthoredFormationRadius) ||
            definition.AuthoredFormationRadius <= 0f)
        {
            AddError(
                result,
                "enemy.formation_radius_invalid",
                "formationRadius",
                "Formation radius must be finite and greater than zero.");
        }

        if (!IsFinite(definition.AuthoredForwardSearchAngle) ||
            definition.AuthoredForwardSearchAngle < 0f ||
            definition.AuthoredForwardSearchAngle >
            EnemySO.MaximumForwardSearchAngle)
        {
            AddError(
                result,
                "enemy.forward_search_angle_invalid",
                "forwardSearchAngle",
                "Forward search angle must be finite and between 0 and " +
                $"{EnemySO.MaximumForwardSearchAngle} degrees.");
        }

        if (definition.AuthoredCombatStatSchemaVersion !=
            EnemySO.CurrentCombatStatSchemaVersion)
        {
            AddError(
                result,
                "enemy.combat_stat_schema_outdated",
                "combatStatSchemaVersion",
                "Run Tools/PS260714/Migrations/Migrate Enemy Combat Stats.");
        }

        if (!IsFinite(definition.AuthoredAttackPower) ||
            definition.AuthoredAttackPower <= 0f)
        {
            AddError(
                result,
                "enemy.attack_power_invalid",
                "attackPower",
                "Attack power must be finite and greater than zero.");
        }

        if (definition.AuthoredCoreAttackDamage <= 0)
        {
            AddError(
                result,
                "enemy.core_attack_damage_invalid",
                "coreAttackDamage",
                "Core attack damage must be at least one.");
        }

        if (!Enum.IsDefined(
                typeof(EnemyCoreAttackDamagePolicy),
                definition.CoreAttackDamagePolicy))
        {
            AddError(
                result,
                "enemy.core_attack_damage_policy_invalid",
                "coreAttackDamagePolicy",
                $"Core attack damage policy " +
                $"'{definition.CoreAttackDamagePolicy}' is unsupported.");
        }
        else if (definition.CoreAttackDamagePolicy ==
                 EnemyCoreAttackDamagePolicy.AccumulateFraction &&
                 (!IsFinite(definition.AuthoredPreciseCoreAttackDamage) ||
                  definition.AuthoredPreciseCoreAttackDamage <= 0f))
        {
            AddError(
                result,
                "enemy.precise_core_attack_damage_invalid",
                "preciseCoreAttackDamage",
                "Accumulate Fraction requires finite core attack damage " +
                "greater than zero.");
        }

        if (!IsFinite(definition.AuthoredCoreAttackInterval) ||
            definition.AuthoredCoreAttackInterval <= 0f)
        {
            AddError(
                result,
                "enemy.core_attack_interval_invalid",
                "coreAttackInterval",
                "Core attack interval must be finite and greater than zero.");
        }

        if (!IsFinite(definition.AuthoredCoreAttackRange) ||
            definition.AuthoredCoreAttackRange < 0f)
        {
            AddError(
                result,
                "enemy.core_attack_range_invalid",
                "coreAttackRange",
                "Core attack range must be finite and cannot be negative.");
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

        if (definition.AuthoredUnlockDifficulty < -1 ||
            definition.AuthoredUnlockDifficulty > 100)
        {
            AddError(
                result,
                "enemy.unlock_difficulty_invalid",
                "unlockDifficulty",
                "Unlock difficulty must be -1 or between 0 and 100.");
        }

        if (definition.AuthoredFootprintWidth < 1 ||
            definition.AuthoredFootprintWidth >
                EnemySO.MaximumFootprintSize ||
            definition.AuthoredFootprintHeight < 1 ||
            definition.AuthoredFootprintHeight >
                EnemySO.MaximumFootprintSize)
        {
            AddError(
                result,
                "enemy.footprint_invalid",
                "footprintWidth",
                $"Enemy footprint dimensions must be between 1 and " +
                $"{EnemySO.MaximumFootprintSize}.");
        }

        if (!Enum.IsDefined(
                typeof(EnemyStackingPolicy),
                definition.AuthoredStackingPolicy))
        {
            AddError(
                result,
                "enemy.stacking_policy_invalid",
                "stackingPolicy",
                $"Stacking policy '{definition.AuthoredStackingPolicy}' " +
                "is unsupported.");
        }

        if ((definition.AuthoredFootprintWidth > 1 ||
             definition.AuthoredFootprintHeight > 1) &&
            definition.AuthoredStackingPolicy !=
                EnemyStackingPolicy.Exclusive)
        {
            AddError(
                result,
                "enemy.large_footprint_must_be_exclusive",
                "stackingPolicy",
                "Enemies larger than 1x1 must use exclusive occupancy.");
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

            ValidateAbilityMetadata(ability, path, result);

            ValidateTriggerEvents(ability, path, result);

            bool usesCooldown = ability.RespondsToTrigger(
                EnemyAbilityTrigger.OnCooldown);
            if (usesCooldown &&
                ability.Cooldown <= 0f)
            {
                AddError(
                    result,
                    "ability.cooldown_required",
                    $"{path}.cooldown",
                    "OnCooldown abilities require a positive cooldown.");
            }
            else if (!usesCooldown &&
                     ability.Cooldown > 0f)
            {
                AddWarning(
                    result,
                    "ability.cooldown_unused",
                    $"{path}.cooldown",
                    "Cooldown is only used by OnCooldown abilities.");
            }

            if (ability.RespondsToTrigger(
                    EnemyAbilityTrigger.OnHealthThreshold) &&
                (ability.HealthThresholdPercent <= 0f ||
                 ability.HealthThresholdPercent >= 100f))
            {
                AddError(
                    result,
                    "ability.health_threshold_invalid",
                    $"{path}.healthThresholdPercent",
                    "Health threshold triggers require a percentage " +
                    "greater than zero and less than 100.");
            }

            if (ability.RespondsToTrigger(
                    EnemyAbilityTrigger.AfterNoDamage) &&
                ability.NoDamageDuration <= 0f)
            {
                AddError(
                    result,
                    "ability.no_damage_duration_required",
                    $"{path}.noDamageDuration",
                    "AfterNoDamage requires a positive duration.");
            }

            ValidateCooldownOverrides(ability, path, result);

            ValidateChargeAndTelegraph(ability, path, result);

            ValidateTarget(ability.Target, path, result);
            ValidateConditions(ability, path, result);
            ValidateOperations(ability, path, result);
        }
    }

    private static void ValidateBossPhases(
        EnemySO definition,
        EnemyDefinitionValidationResult result)
    {
        IReadOnlyList<EnemyBossPhaseDefinition> phases =
            definition.PhaseDefinitions;
        if (phases.Count == 0)
        {
            if (definition.AuthoredRosterSchemaVersion > 0 &&
                definition.RosterTier == EnemyRosterTier.Boss)
            {
                AddError(
                    result,
                    "enemy.boss_phases_missing",
                    "phaseDefinitions",
                    "Boss roster entries require at least one phase.");
            }
            return;
        }

        if (definition.RosterTier != EnemyRosterTier.Boss)
        {
            AddWarning(
                result,
                "enemy.phases_on_non_boss",
                "phaseDefinitions",
                "Phase definitions are normally authored only for boss " +
                "roster entries.");
        }

        HashSet<string> knownAbilityIds = new(StringComparer.Ordinal);
        foreach (EnemyAbilityDefinition ability in definition.Abilities)
        {
            if (ability != null &&
                !string.IsNullOrWhiteSpace(ability.AbilityId))
            {
                knownAbilityIds.Add(ability.AbilityId);
            }
        }

        HashSet<string> phaseIds = new(StringComparer.Ordinal);
        int[] coverage = new int[101];
        for (int index = 0; index < phases.Count; index++)
        {
            EnemyBossPhaseDefinition phase = phases[index];
            string path = $"phaseDefinitions[{index}]";
            if (phase == null)
            {
                AddError(
                    result,
                    "enemy.boss_phase_null",
                    path,
                    "Boss phase definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(phase.PhaseId))
            {
                AddError(
                    result,
                    "enemy.boss_phase_id_missing",
                    $"{path}.phaseId",
                    "Boss phase ID is required.");
            }
            else if (!phaseIds.Add(phase.PhaseId))
            {
                AddError(
                    result,
                    "enemy.boss_phase_id_duplicate",
                    $"{path}.phaseId",
                    $"Boss phase ID '{phase.PhaseId}' is duplicated.");
            }

            ValidateLocalization(
                phase.NameLocalizationKey,
                phase.FallbackName,
                $"{path}.nameLocalizationKey",
                false,
                result);

            int minimum = phase.MinimumHealthPercent;
            int maximum = phase.MaximumHealthPercent;
            if (minimum < 0 || maximum > 100 || minimum > maximum)
            {
                AddError(
                    result,
                    "enemy.boss_phase_range_invalid",
                    $"{path}.minimumHealthPercent",
                    "Boss phase health range must satisfy 0 <= minimum " +
                    "<= maximum <= 100.");
            }
            else
            {
                for (int percent = minimum; percent <= maximum; percent++)
                    coverage[percent]++;
            }

            HashSet<string> phaseAbilityIds =
                new(StringComparer.Ordinal);
            for (int abilityIndex = 0;
                 abilityIndex < phase.AbilityIds.Count;
                 abilityIndex++)
            {
                string abilityId = phase.AbilityIds[abilityIndex];
                string abilityPath =
                    $"{path}.abilityIds[{abilityIndex}]";
                if (string.IsNullOrWhiteSpace(abilityId))
                {
                    AddError(
                        result,
                        "enemy.boss_phase_ability_id_missing",
                        abilityPath,
                        "Boss phase ability IDs cannot be empty.");
                }
                else if (!phaseAbilityIds.Add(abilityId))
                {
                    AddError(
                        result,
                        "enemy.boss_phase_ability_id_duplicate",
                        abilityPath,
                        $"Boss phase ability ID '{abilityId}' is " +
                        "duplicated in this phase.");
                }
                else if (!knownAbilityIds.Contains(abilityId))
                {
                    AddError(
                        result,
                        "enemy.boss_phase_ability_unknown",
                        abilityPath,
                        $"Boss phase references unknown ability " +
                        $"'{abilityId}'.");
                }
            }
        }

        List<int> uncovered = new();
        List<int> overlapping = new();
        for (int percent = 0; percent <= 100; percent++)
        {
            if (coverage[percent] == 0)
                uncovered.Add(percent);
            else if (coverage[percent] > 1)
                overlapping.Add(percent);
        }

        if (uncovered.Count > 0)
        {
            AddError(
                result,
                "enemy.boss_phase_coverage_gap",
                "phaseDefinitions",
                "Boss phases do not cover every whole-number health " +
                $"percentage. First uncovered value: {uncovered[0]}%.");
        }
        if (overlapping.Count > 0)
        {
            AddError(
                result,
                "enemy.boss_phase_coverage_overlap",
                "phaseDefinitions",
                "Boss phase ranges overlap. First overlapping value: " +
                $"{overlapping[0]}%.");
        }
    }

    private static void ValidateTriggerEvents(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(
                typeof(EnemyAbilityTrigger),
                ability.Trigger))
        {
            AddError(
                result,
                "ability.trigger_invalid",
                $"{abilityPath}.trigger",
                $"Ability trigger '{ability.Trigger}' is unsupported.");
        }

        HashSet<EnemyAbilityTrigger> triggers = new()
        {
            ability.Trigger
        };
        for (int index = 0;
             index < ability.AdditionalTriggers.Count;
             index++)
        {
            EnemyAbilityTrigger additional =
                ability.AdditionalTriggers[index];
            string path = $"{abilityPath}.triggerEvents[{index}]";
            if (!Enum.IsDefined(typeof(EnemyAbilityTrigger), additional))
            {
                AddError(
                    result,
                    "ability.trigger_invalid",
                    path,
                    $"Ability trigger '{additional}' is unsupported.");
            }
            else if (!triggers.Add(additional))
            {
                AddError(
                    result,
                    "ability.trigger_duplicate",
                    path,
                    $"Ability trigger '{additional}' is duplicated.");
            }
        }
    }

    private static void ValidateCooldownOverrides(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        if (ability.CooldownOverrides.Count == 0)
            return;

        if (!ability.RespondsToTrigger(EnemyAbilityTrigger.OnCooldown))
        {
            AddError(
                result,
                "ability.cooldown_override_trigger_mismatch",
                $"{abilityPath}.cooldownOverrides",
                "Cooldown overrides require an OnCooldown trigger event.");
        }

        HashSet<float> thresholds = new();
        for (int index = 0;
             index < ability.CooldownOverrides.Count;
             index++)
        {
            EnemyAbilityCooldownOverrideDefinition rule =
                ability.CooldownOverrides[index];
            string path = $"{abilityPath}.cooldownOverrides[{index}]";
            if (rule == null)
            {
                AddError(
                    result,
                    "ability.cooldown_override_null",
                    path,
                    "Cooldown override is null.");
                continue;
            }

            float threshold = rule.AuthoredHealthAtOrBelowPercent;
            if (!IsFinite(threshold) ||
                threshold <= 0f ||
                threshold >= 100f)
            {
                AddError(
                    result,
                    "ability.cooldown_override_threshold_invalid",
                    $"{path}.healthAtOrBelowPercent",
                    "Cooldown override health threshold must be finite, " +
                    "greater than zero, and less than 100.");
            }
            else if (!thresholds.Add(threshold))
            {
                AddError(
                    result,
                    "ability.cooldown_override_threshold_duplicate",
                    $"{path}.healthAtOrBelowPercent",
                    $"Cooldown override threshold " +
                    $"{threshold}% is " +
                    "duplicated.");
            }

            if (!IsFinite(rule.AuthoredCooldown) ||
                rule.AuthoredCooldown <= 0f)
            {
                AddError(
                    result,
                    "ability.cooldown_override_value_invalid",
                    $"{path}.cooldown",
                    "Cooldown override must be finite and greater than " +
                    "zero.");
            }
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
        if (!Enum.IsDefined(
                typeof(EnemyWorldLayerScope),
                target.LayerScope))
        {
            AddError(
                result,
                "ability.target_layer_scope_invalid",
                $"{abilityPath}.target.layerScope",
                $"World layer scope '{target.LayerScope}' is " +
                "unsupported.");
        }
        if (target.Subject == EnemyAbilityTargetSubject.WorldRadius)
        {
            if (!IsFinite(target.AuthoredWorldRadius) ||
                target.AuthoredWorldRadius <= 0f)
            {
                AddError(
                    result,
                    "ability.target_world_radius_invalid",
                    $"{abilityPath}.target.worldRadius",
                    "World Radius targets require a finite, positive " +
                    "radius.");
            }
        }
        else if (IsFinite(target.AuthoredWorldRadius) &&
                 target.AuthoredWorldRadius > 0f)
        {
            AddWarning(
                result,
                "ability.target_world_radius_unused",
                $"{abilityPath}.target.worldRadius",
                "World radius is only used by WorldRadius targets.");
        }
        if (target.AreaDefinition == null)
        {
            AddError(
                result,
                "ability.area_definition_null",
                $"{abilityPath}.target.areaDefinition",
                "Enemy abilities require an area definition.");
        }
        else if (!target.AreaDefinition.IsValid)
        {
            AddError(
                result,
                "ability.area_definition_invalid",
                $"{abilityPath}.target.areaDefinition",
                "Enemy ability area definition is invalid.");
        }
        else if (target.AreaDefinition.UsesWorldArea)
        {
            AddError(
                result,
                "ability.world_area_unsupported",
                $"{abilityPath}.target.areaDefinition",
                "Enemy abilities currently support Target range only.");
        }
    }

    private static void ValidateAbilityMetadata(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(ability.AbilityTypeId))
        {
            AddWarning(
                result,
                "ability.type_id_missing",
                $"{abilityPath}.abilityTypeId",
                "Ability Type ID is recommended for roster import and " +
                "telemetry.");
        }
        else if (ContainsWhitespace(ability.AbilityTypeId))
        {
            AddError(
                result,
                "ability.type_id_whitespace",
                $"{abilityPath}.abilityTypeId",
                "Ability Type ID cannot contain whitespace.");
        }

        HashSet<string> parameterKeys = new(StringComparer.Ordinal);
        IReadOnlyList<EnemyAbilityParameterDefinition> parameters =
            ability.Parameters;
        for (int index = 0; index < parameters.Count; index++)
        {
            EnemyAbilityParameterDefinition parameter = parameters[index];
            string path = $"{abilityPath}.parameters[{index}]";
            if (parameter == null)
            {
                AddError(
                    result,
                    "ability.parameter_null",
                    path,
                    "Ability parameter is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameter.Key))
            {
                AddError(
                    result,
                    "ability.parameter_key_missing",
                    $"{path}.key",
                    "Ability parameter key is required.");
            }
            else if (!parameterKeys.Add(parameter.Key))
            {
                AddError(
                    result,
                    "ability.parameter_key_duplicate",
                    $"{path}.key",
                    $"Ability parameter key '{parameter.Key}' is " +
                    "duplicated.");
            }

            if (!Enum.IsDefined(
                    typeof(EnemyAbilityParameterValueType),
                    parameter.ValueType))
            {
                AddError(
                    result,
                    "ability.parameter_type_invalid",
                    $"{path}.valueType",
                    $"Parameter value type '{parameter.ValueType}' is " +
                    "unsupported.");
            }
            else if (parameter.ValueType ==
                         EnemyAbilityParameterValueType.Float &&
                     !IsFinite(parameter.FloatValue))
            {
                AddError(
                    result,
                    "ability.parameter_float_invalid",
                    $"{path}.floatValue",
                    "Float parameter values must be finite.");
            }
            else if (parameter.ValueType ==
                         EnemyAbilityParameterValueType.EnemyReference &&
                     (parameter.EnemyReference == null ||
                      !parameter.EnemyReference.IsConfigured))
            {
                AddError(
                    result,
                    "ability.parameter_reference_missing",
                    $"{path}.enemyReference",
                    "Enemy reference parameters require an EnemySO or " +
                    "Enemy ID.");
            }
        }
    }

    private static void ValidateChargeAndTelegraph(
        EnemyAbilityDefinition ability,
        string abilityPath,
        EnemyDefinitionValidationResult result)
    {
        EnemyAbilityChargeDefinition charge = ability.Charge;
        if (charge == null)
        {
            AddError(
                result,
                "ability.charge_null",
                $"{abilityPath}.charge",
                "Charge definition is null.");
        }
        else if (charge.IsEnabled)
        {
            if (!IsFinite(charge.AuthoredDuration) ||
                charge.AuthoredDuration <= 0f)
            {
                AddError(
                    result,
                    "ability.charge_duration_invalid",
                    $"{abilityPath}.charge.duration",
                    "Enabled charges require a finite, positive duration.");
            }

            if (charge.IsInterruptible &&
                charge.Interrupts == EnemyChargeInterruptFlags.None)
            {
                AddError(
                    result,
                    "ability.charge_interrupts_missing",
                    $"{abilityPath}.charge.interrupts",
                    "Interruptible charges require at least one interrupt " +
                    "reason.");
            }
            const EnemyChargeInterruptFlags knownInterrupts =
                EnemyChargeInterruptFlags.Stun |
                EnemyChargeInterruptFlags.ForcedMovement |
                EnemyChargeInterruptFlags.DirectDamage |
                EnemyChargeInterruptFlags.AnyControl;
            if ((charge.Interrupts & ~knownInterrupts) != 0)
            {
                AddError(
                    result,
                    "ability.charge_interrupts_invalid",
                    $"{abilityPath}.charge.interrupts",
                    $"Charge interrupt flags '{charge.Interrupts}' contain " +
                    "unsupported values.");
            }
        }

        EnemyAbilityTelegraphDefinition telegraph = ability.Telegraph;
        if (telegraph == null)
        {
            AddError(
                result,
                "ability.telegraph_null",
                $"{abilityPath}.telegraph",
                "Telegraph definition is null.");
        }
        else if (telegraph.IsEnabled)
        {
            if (!IsFinite(telegraph.AuthoredLeadTime) ||
                telegraph.AuthoredLeadTime < 0f)
            {
                AddError(
                    result,
                    "ability.telegraph_lead_time_invalid",
                    $"{abilityPath}.telegraph.leadTime",
                    "Enabled telegraphs require a finite, non-negative " +
                    "lead time. Zero represents an immediate ready cue.");
            }

            if (!IsFinite(telegraph.AuthoredWorldRadius) ||
                telegraph.AuthoredWorldRadius < 0f)
            {
                AddError(
                    result,
                    "ability.telegraph_radius_invalid",
                    $"{abilityPath}.telegraph.worldRadius",
                    "Telegraph world radius must be finite and cannot be " +
                    "negative.");
            }
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
            if (condition.Type ==
                    EnemyAbilityConditionType.SourceHasStatus ||
                condition.Type ==
                    EnemyAbilityConditionType.TargetHasStatus)
            {
                bool hasValidStatusScope = Enum.IsDefined(
                    typeof(CharacterStatusSelectionScope),
                    condition.StatusSelectionScope);
                if (!hasValidStatusScope)
                {
                    AddError(
                        result,
                        "ability.condition_status_scope_invalid",
                        $"{path}.statusSelectionScope",
                        $"Status selection scope " +
                        $"'{condition.StatusSelectionScope}' is " +
                        "unsupported.");
                }
                bool selectsConfiguredStatuses =
                    condition.StatusSelectionScope ==
                    CharacterStatusSelectionScope.SelectedStatuses;
                CharacterStatusSelection selection =
                    condition.StatusSelection;
                if (selectsConfiguredStatuses &&
                    selection.Count == 0)
                {
                    AddError(
                        result,
                        "ability.condition_status_missing",
                        $"{path}.statusEffects",
                        "A status condition requires at least one " +
                        "StatusEffectSO.");
                }

                int uniqueStatusCount = 0;
                for (int statusIndex = 0;
                     selectsConfiguredStatuses &&
                     statusIndex < selection.Count;
                     statusIndex++)
                {
                    StatusEffectSO status =
                        selection.GetStatus(statusIndex);
                    string statusPath = selection.UsesStatusList
                        ? $"{path}.statusEffects[{statusIndex}]"
                        : $"{path}.statusEffect";
                    if (status == null)
                    {
                        AddError(
                            result,
                            "ability.condition_status_null",
                            statusPath,
                            "Status selection contains a null entry.");
                        continue;
                    }

                    if (ContainsEarlierStatus(
                            selection,
                            status,
                            statusIndex))
                    {
                        AddError(
                            result,
                            "ability.condition_status_duplicate",
                            statusPath,
                            $"Status '{status.name}' is selected more than " +
                            "once.");
                        continue;
                    }

                    uniqueStatusCount++;
                    CharacterTargetFaction? faction =
                        GetConditionStatusFaction(ability, condition.Type);
                    if (faction.HasValue &&
                        !CanTargetFaction(status, faction.Value))
                    {
                        AddError(
                            result,
                            "ability.condition_status_faction_mismatch",
                            statusPath,
                            $"Status '{status.name}' cannot exist on the " +
                            "selected condition target.");
                    }
                }

                if (!Enum.IsDefined(
                        typeof(CharacterStatusConditionMatchMode),
                        condition.StatusMatchMode))
                {
                    AddError(
                        result,
                        "ability.condition_status_match_mode_invalid",
                        $"{path}.statusMatchMode",
                        $"Status match mode '{condition.StatusMatchMode}' " +
                        "is unsupported.");
                }
                else if (condition.StatusMatchMode ==
                         CharacterStatusConditionMatchMode.AtLeastCount)
                {
                    if (condition.StatusMatchCount < 1)
                    {
                        AddError(
                            result,
                            "ability.condition_status_match_count_invalid",
                            $"{path}.statusMatchCount",
                            "Required status count must be at least 1.");
                    }
                    else if (condition.StatusMatchCount >
                             uniqueStatusCount &&
                             selectsConfiguredStatuses)
                    {
                        AddError(
                            result,
                            "ability.condition_status_match_count_exceeds_selection",
                            $"{path}.statusMatchCount",
                            "Required status count cannot exceed the number " +
                            "of selected statuses.");
                    }
                }
            }
            if (condition.Type ==
                    EnemyAbilityConditionType.IncomingDamageType &&
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.BeforeSelfDamage) &&
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.BeforeAllyDamage))
            {
                AddError(
                    result,
                    "ability.condition_damage_trigger_mismatch",
                    path,
                    "IncomingDamageType can only be used by a " +
                    "before-damage trigger.");
            }
            if (condition.Type ==
                    EnemyAbilityConditionType.RepeatedDamageSource)
            {
                if (!IsFinite(condition.AuthoredWindowDuration) ||
                    condition.AuthoredWindowDuration <= 0f)
                {
                    AddError(
                        result,
                        "ability.condition_damage_source_window_invalid",
                        $"{path}.windowDuration",
                        "RepeatedDamageSource requires a finite, positive " +
                        "history window in seconds.");
                }
                if (!ability.RespondsToTrigger(
                        EnemyAbilityTrigger.BeforeSelfDamage) &&
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.BeforeAllyDamage))
                {
                    AddError(
                        result,
                        "ability.condition_damage_trigger_mismatch",
                        path,
                        "RepeatedDamageSource can only be used by a " +
                        "before-damage trigger.");
                }
            }
        }
    }

    private static CharacterTargetFaction? GetConditionStatusFaction(
        EnemyAbilityDefinition ability,
        EnemyAbilityConditionType type)
    {
        if (type == EnemyAbilityConditionType.SourceHasStatus)
            return CharacterTargetFaction.Enemy;

        return ability.Target?.Faction switch
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

    private static bool CanTargetFaction(
        StatusEffectSO status,
        CharacterTargetFaction faction)
    {
        return status != null &&
               (faction == CharacterTargetFaction.Ally
                   ? status.CanTargetAlly
                   : status.CanTargetEnemy);
    }

    private static bool ContainsEarlierStatus(
        CharacterStatusSelection selection,
        StatusEffectSO status,
        int index)
    {
        for (int previous = 0; previous < index; previous++)
        {
            if (CharacterStatusSelection.IsSameStatus(
                    selection.GetStatus(previous),
                    status))
            {
                return true;
            }
        }

        return false;
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

            if (ContainsWhitespace(operation.SourceId))
            {
                AddError(
                    result,
                    "ability.source_id_whitespace",
                    $"{path}.sourceId",
                    "Operation Source ID cannot contain whitespace.");
            }

            if (!IsFinite(operation.AuthoredDuration) ||
                operation.AuthoredDuration < 0f)
            {
                AddError(
                    result,
                    "ability.duration_invalid",
                    $"{path}.duration",
                    "Operation duration must be finite and cannot be " +
                    "negative.");
            }
            if (!IsFinite(operation.AuthoredInterval) ||
                operation.AuthoredInterval < 0f)
            {
                AddError(
                    result,
                    "ability.interval_invalid",
                    $"{path}.interval",
                    "Operation interval must be finite and cannot be " +
                    "negative.");
            }
            if (!IsFinite(operation.AuthoredWorldRadius) ||
                operation.AuthoredWorldRadius < 0f)
            {
                AddError(
                    result,
                    "ability.world_radius_invalid",
                    $"{path}.worldRadius",
                    "Operation world radius must be finite and cannot be " +
                    "negative.");
            }
            if (operation.AuthoredMaximumStacks < 0)
            {
                AddError(
                    result,
                    "ability.maximum_stacks_invalid",
                    $"{path}.maximumStacks",
                    "Operation maximum stacks cannot be negative.");
            }

            if (!IsCompatibleWithAnyTrigger(ability, operation.Type))
            {
                AddError(
                    result,
                    "ability.operation_trigger_mismatch",
                    path,
                    $"{operation.Type} is incompatible with " +
                    $"{ability.Trigger}.");
            }

            ValidateOperationValues(ability, operation, path, result);
        }
    }

    private static void ValidateOperationValues(
        EnemyAbilityDefinition ability,
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
                    CharacterEffectDefinition effect =
                        operation.Effects[index];
                    if (effect == null)
                    {
                        AddError(
                            result,
                            "ability.effect_null",
                            $"{path}.effects[{index}]",
                            "Effect definition is null.");
                        continue;
                    }
                    if (!BattleEffectRules.TryValidate(
                            effect,
                            out string effectError))
                    {
                        AddError(
                            result,
                            "ability.effect_shared_invalid",
                            $"{path}.effects[{index}]",
                            effectError);
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
                ValidateModifierValues(operation, path, result);
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

            case EnemyAbilityOperationType.ModifyCoreAttackDamage:
            case EnemyAbilityOperationType.ModifyStatusDuration:
            case EnemyAbilityOperationType.ModifyResourceRecovery:
            case EnemyAbilityOperationType.ModifyCoreRecovery:
            case EnemyAbilityOperationType.ModifyCoreMaximumHealth:
                ValidateModifierValues(operation, path, result);
                break;

            case EnemyAbilityOperationType.ModifyCoreAttackInterval:
                if (!IsFinite(operation.Multiplier) ||
                    operation.Multiplier <= 0f)
                {
                    AddError(
                        result,
                        "ability.multiplier_invalid",
                        $"{path}.multiplier",
                        "Core attack interval multiplier must be finite " +
                        "and greater than zero.");
                }
                break;

            case EnemyAbilityOperationType.GrantStatusImmunity:
                ValidateDuration(operation, path, allowPermanent: true, result);
                break;

            case EnemyAbilityOperationType.ChargeCoreAttack:
                if (!ability.Charge.IsEnabled)
                {
                    AddError(
                        result,
                        "ability.charge_definition_required",
                        $"{path}.type",
                        "ChargeCoreAttack requires an enabled ability " +
                        "charge definition.");
                }
                break;

            case EnemyAbilityOperationType.SummonEnemy:
                ValidateSummon(operation.Summon, path, result);
                break;

            case EnemyAbilityOperationType.ApplyCoreEffect:
                ValidateModifierValues(operation, path, result);
                if (operation.Interval > 0f && operation.Duration <= 0f)
                {
                    AddError(
                        result,
                        "ability.effect_duration_required",
                        $"{path}.duration",
                        "Periodic core effects require a positive " +
                        "duration.");
                }
                break;

            case EnemyAbilityOperationType.CreateWorldZone:
                if (!IsFinite(operation.WorldRadius) ||
                    operation.WorldRadius <= 0f)
                {
                    AddError(
                        result,
                        "ability.world_radius_invalid",
                        $"{path}.worldRadius",
                        "World zones require a finite, positive radius.");
                }
                ValidateDuration(operation, path, allowPermanent: false, result);
                break;

            case EnemyAbilityOperationType.LinkTargets:
                if (operation.Count < 1)
                {
                    AddError(
                        result,
                        "ability.link_count_invalid",
                        $"{path}.count",
                        "LinkTargets requires at least one linked target.");
                }
                break;

            case EnemyAbilityOperationType.ReflectDamage:
                if (!IsFinite(operation.Percentage) ||
                    operation.Percentage <= 0f)
                {
                    AddError(
                        result,
                        "ability.percentage_invalid",
                        $"{path}.percentage",
                        "Reflected damage percentage must be finite and " +
                        "greater than zero.");
                }
                break;

            case EnemyAbilityOperationType.ReplayAbility:
                if (!string.IsNullOrWhiteSpace(
                        operation.ReferencedAbilityId) &&
                    ContainsWhitespace(operation.ReferencedAbilityId))
                {
                    AddError(
                        result,
                        "ability.reference_id_whitespace",
                        $"{path}.referencedAbilityId",
                        "Referenced Ability ID cannot contain whitespace.");
                }
                break;

            case EnemyAbilityOperationType.ModifyCardCost:
                if (operation.Amount <= 0)
                {
                    AddError(
                        result,
                        "ability.amount_invalid",
                        $"{path}.amount",
                        "Card cost modification requires a positive " +
                        "amount.");
                }
                ValidateDuration(operation, path, allowPermanent: false, result);
                break;

            case EnemyAbilityOperationType.ModifyPlayerActionInterval:
                if (!IsFinite(operation.Multiplier) ||
                    operation.Multiplier <= 0f)
                {
                    AddError(
                        result,
                        "ability.multiplier_invalid",
                        $"{path}.multiplier",
                        "Player action interval multiplier must be finite " +
                        "and greater than zero.");
                }
                ValidateDuration(operation, path, allowPermanent: true, result);
                break;

            case EnemyAbilityOperationType.ConvertCoreDamageToSelfShield:
                if (!IsFinite(operation.Percentage) ||
                    operation.Percentage <= 0f)
                {
                    AddError(
                        result,
                        "ability.percentage_invalid",
                        $"{path}.percentage",
                        "Core damage conversion requires a finite, " +
                        "positive percentage.");
                }
                break;

            case EnemyAbilityOperationType.LockCard:
                if (operation.Count <= 0)
                {
                    AddError(
                        result,
                        "ability.count_invalid",
                        $"{path}.count",
                        "LockCard requires at least one card.");
                }
                ValidateDuration(operation, path, allowPermanent: false, result);
                break;

            case EnemyAbilityOperationType.SetUntargetable:
                ValidateDuration(operation, path, allowPermanent: false, result);
                break;
        }
    }

    private static void ValidateModifierValues(
        EnemyAbilityOperationDefinition operation,
        string path,
        EnemyDefinitionValidationResult result)
    {
        if (!IsFinite(operation.Multiplier) || operation.Multiplier < 0f)
        {
            AddError(
                result,
                "ability.multiplier_invalid",
                $"{path}.multiplier",
                "Modifier multiplier must be finite and cannot be " +
                "negative.");
        }
        if (!IsFinite(operation.Percentage))
        {
            AddError(
                result,
                "ability.percentage_invalid",
                $"{path}.percentage",
                "Modifier percentage must be finite.");
        }
    }

    private static void ValidateDuration(
        EnemyAbilityOperationDefinition operation,
        string path,
        bool allowPermanent,
        EnemyDefinitionValidationResult result)
    {
        if (!IsFinite(operation.AuthoredDuration) ||
            (!allowPermanent && operation.AuthoredDuration <= 0f) ||
            (allowPermanent && operation.AuthoredDuration < 0f))
        {
            AddError(
                result,
                "ability.duration_invalid",
                $"{path}.duration",
                allowPermanent
                    ? "Duration must be finite and cannot be negative; " +
                      "zero means permanent."
                    : "Duration must be finite and greater than zero.");
        }
    }

    private static void ValidateSummon(
        EnemySummonDefinition summon,
        string path,
        EnemyDefinitionValidationResult result)
    {
        if (summon == null)
        {
            AddError(
                result,
                "ability.summon_null",
                $"{path}.summon",
                "Summon definition is null.");
            return;
        }

        if (summon.AuthoredMinimumCount <= 0 ||
            summon.AuthoredMaximumCount < summon.AuthoredMinimumCount)
        {
            AddError(
                result,
                "ability.summon_count_invalid",
                $"{path}.summon.maximumCount",
                "Summon maximum count must be at least the positive " +
                "minimum count.");
        }

        if (summon.AuthoredMaximumActive < 0)
        {
            AddError(
                result,
                "ability.summon_active_cap_invalid",
                $"{path}.summon.maximumActive",
                "Summon active cap cannot be negative; zero means no " +
                "explicit cap for non-recursive summons.");
        }
        if (!IsFinite(summon.AuthoredChildHealthMultiplier) ||
            summon.AuthoredChildHealthMultiplier <= 0f ||
            !IsFinite(summon.AuthoredChildCoreAttackMultiplier) ||
            summon.AuthoredChildCoreAttackMultiplier <= 0f)
        {
            AddError(
                result,
                "ability.summon_child_multiplier_invalid",
                $"{path}.summon.childHealthMultiplier",
                "Summoned child health and core-attack multipliers must " +
                "be finite and greater than zero.");
        }

        if (summon.Candidates.Count == 0)
        {
            AddError(
                result,
                "ability.summon_candidates_empty",
                $"{path}.summon.candidates",
                "SummonEnemy requires at least one enemy reference.");
            return;
        }

        HashSet<string> candidateIds = new(StringComparer.Ordinal);
        for (int index = 0; index < summon.Candidates.Count; index++)
        {
            EnemyReferenceDefinition candidate = summon.Candidates[index];
            string candidatePath =
                $"{path}.summon.candidates[{index}]";
            if (candidate == null ||
                string.IsNullOrWhiteSpace(candidate.ResolvedEnemyId))
            {
                AddError(
                    result,
                    "ability.summon_candidate_missing",
                    candidatePath,
                    "Summon candidates require an EnemySO or Enemy ID.");
                continue;
            }

            if (!candidateIds.Add(candidate.ResolvedEnemyId))
            {
                AddError(
                    result,
                    "ability.summon_candidate_duplicate",
                    candidatePath,
                    $"Summon candidate '{candidate.ResolvedEnemyId}' is " +
                    "duplicated.");
            }

        }

        if (summon.AllowRecursiveSummon &&
            summon.AuthoredMaximumActive <= 0)
        {
            AddError(
                result,
                "ability.recursive_summon_cap_missing",
                $"{path}.summon.maximumActive",
                "Recursive summoning requires a positive active " +
                "summon cap.");
        }
    }

    private static bool IsCompatible(
        EnemyAbilityTrigger trigger,
        EnemyAbilityOperationType operation)
    {
        return operation switch
        {
            EnemyAbilityOperationType.ModifySpawnInterval =>
                trigger == EnemyAbilityTrigger.OnSpawnQueueEvaluation ||
                trigger == EnemyAbilityTrigger.OnCooldown ||
                trigger == EnemyAbilityTrigger.AlwaysWhileActive ||
                trigger == EnemyAbilityTrigger.OnPhaseChanged,
            EnemyAbilityOperationType.ModifyIncomingDamage =>
                trigger != EnemyAbilityTrigger.OnSpawnQueueEvaluation &&
                trigger != EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityOperationType.ExpandSpawnGroup =>
                trigger == EnemyAbilityTrigger.OnSpawnQueueEvaluation,
            EnemyAbilityOperationType.GrantArmor =>
                trigger == EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityOperationType.RedirectDamage =>
                trigger == EnemyAbilityTrigger.BeforeAllyDamage,
            EnemyAbilityOperationType.ModifyTargetPriority =>
                trigger == EnemyAbilityTrigger.OnTargetPriorityEvaluation ||
                trigger == EnemyAbilityTrigger.OnCooldown ||
                trigger == EnemyAbilityTrigger.AlwaysWhileActive,
            EnemyAbilityOperationType.ExecuteEffects =>
                trigger != EnemyAbilityTrigger.OnSpawnQueueEvaluation &&
                trigger != EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityOperationType.ModifyCoreAttackDamage or
            EnemyAbilityOperationType.ModifyCoreAttackInterval or
            EnemyAbilityOperationType.ModifyStatusDuration or
            EnemyAbilityOperationType.GrantStatusImmunity or
            EnemyAbilityOperationType.ChargeCoreAttack or
            EnemyAbilityOperationType.SummonEnemy or
            EnemyAbilityOperationType.ApplyCoreEffect or
            EnemyAbilityOperationType.CreateWorldZone or
            EnemyAbilityOperationType.LinkTargets or
            EnemyAbilityOperationType.ReflectDamage or
            EnemyAbilityOperationType.ReplayAbility or
            EnemyAbilityOperationType.ModifyCardCost or
            EnemyAbilityOperationType.LockCard or
            EnemyAbilityOperationType.ModifyResourceRecovery or
            EnemyAbilityOperationType.ModifyCoreRecovery or
            EnemyAbilityOperationType.ModifyCoreMaximumHealth or
            EnemyAbilityOperationType.SetUntargetable =>
                trigger != EnemyAbilityTrigger.OnSpawnQueueEvaluation &&
                trigger != EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityOperationType.ModifyPlayerActionInterval or
            EnemyAbilityOperationType.ConvertCoreDamageToSelfShield =>
                trigger != EnemyAbilityTrigger.OnSpawnQueueEvaluation &&
                trigger != EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            _ => false
        };
    }

    private static bool IsCompatibleWithAnyTrigger(
        EnemyAbilityDefinition ability,
        EnemyAbilityOperationType operation)
    {
        if (ability == null)
            return false;
        if (IsCompatible(ability.Trigger, operation))
            return true;
        foreach (EnemyAbilityTrigger trigger in ability.AdditionalTriggers)
        {
            if (IsCompatible(trigger, operation))
                return true;
        }
        return false;
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

    private static void ValidateTags(
        IReadOnlyList<string> tags,
        string path,
        bool required,
        EnemyDefinitionValidationResult result)
    {
        if (tags == null || tags.Count == 0)
        {
            if (required)
            {
                AddError(
                    result,
                    "enemy.role_tags_missing",
                    path,
                    "Current roster entries require at least one role " +
                    "tag.");
            }
            return;
        }

        HashSet<string> uniqueTags = new(StringComparer.Ordinal);
        for (int index = 0; index < tags.Count; index++)
        {
            string tag = tags[index];
            string tagPath = $"{path}[{index}]";
            if (string.IsNullOrWhiteSpace(tag))
            {
                AddError(
                    result,
                    "enemy.tag_empty",
                    tagPath,
                    "Enemy tags cannot be empty.");
                continue;
            }

            if (ContainsWhitespace(tag))
            {
                AddError(
                    result,
                    "enemy.tag_whitespace",
                    tagPath,
                    "Enemy tags cannot contain whitespace.");
            }

            if (!uniqueTags.Add(tag))
            {
                AddError(
                    result,
                    "enemy.tag_duplicate",
                    tagPath,
                    $"Enemy tag '{tag}' is duplicated.");
            }
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
