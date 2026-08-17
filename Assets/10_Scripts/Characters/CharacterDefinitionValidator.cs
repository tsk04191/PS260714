using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

public enum CharacterDefinitionDiagnosticSeverity
{
    Warning,
    Error
}

public readonly struct CharacterDefinitionDiagnostic
{
    public CharacterDefinitionDiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Path { get; }
    public string Message { get; }

    public CharacterDefinitionDiagnostic(
        CharacterDefinitionDiagnosticSeverity severity,
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
        string location = string.IsNullOrWhiteSpace(Path) ? "<root>" : Path;
        return $"{Severity} [{Code}] {location}: {Message}";
    }
}

public sealed class CharacterDefinitionValidationResult
{
    private readonly List<CharacterDefinitionDiagnostic> _diagnostics = new();

    public IReadOnlyList<CharacterDefinitionDiagnostic> Diagnostics =>
        _diagnostics;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool IsValid => ErrorCount == 0;

    internal void Add(
        CharacterDefinitionDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        _diagnostics.Add(new CharacterDefinitionDiagnostic(
            severity,
            code,
            path,
            message));
        if (severity == CharacterDefinitionDiagnosticSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    internal void Add(
        CharacterDefinitionDiagnostic diagnostic,
        string pathPrefix = null)
    {
        string path = string.IsNullOrEmpty(pathPrefix)
            ? diagnostic.Path
            : string.IsNullOrEmpty(diagnostic.Path)
                ? pathPrefix
                : $"{pathPrefix}.{diagnostic.Path}";
        Add(
            diagnostic.Severity,
            diagnostic.Code,
            path,
            diagnostic.Message);
    }
}

public static class CharacterDefinitionValidator
{
    private const string RootPath = "character";

    public static CharacterDefinitionValidationResult Validate(
        CharacterSO definition)
    {
        return Validate(definition, null);
    }

    public static CharacterDefinitionValidationResult Validate(
        CharacterSO definition,
        IReadOnlyList<CharacterSO> catalog)
    {
        CharacterDefinitionValidationResult result = new();
        ValidateDefinition(definition, result);
        if (definition != null && catalog != null)
            ValidateDuplicateId(definition, catalog, result);
        return result;
    }

    public static CharacterDefinitionValidationResult ValidateAll(
        IReadOnlyList<CharacterSO> definitions)
    {
        CharacterDefinitionValidationResult result = new();
        if (definitions == null)
        {
            AddError(
                result,
                "catalog.null",
                "characters",
                "Character definition catalog is null.");
            return result;
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            CharacterDefinitionValidationResult definitionResult =
                Validate(definitions[index]);
            foreach (CharacterDefinitionDiagnostic diagnostic in
                     definitionResult.Diagnostics)
            {
                result.Add(diagnostic, $"characters[{index}]");
            }
        }

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Count; index++)
        {
            CharacterSO definition = definitions[index];
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.CharacterId))
            {
                continue;
            }

            if (!seenIds.Add(definition.CharacterId))
            {
                AddError(
                    result,
                    "character.id_duplicate",
                    $"characters[{index}].characterId",
                    $"CharacterId '{definition.CharacterId}' is duplicated.");
            }
        }

        return result;
    }

    private static void ValidateDefinition(
        CharacterSO definition,
        CharacterDefinitionValidationResult result)
    {
        if (definition == null)
        {
            AddError(
                result,
                "character.null",
                RootPath,
                "Character definition is null.");
            return;
        }

        ValidateIdentity(definition, result);
        if (definition.MaximumHealth < 1)
        {
            AddError(
                result,
                "character.maximum_health_invalid",
                "maximumHealth",
                "Maximum health must be at least one.");
        }
        CharacterTargetFaction? attackTargetFaction =
            ValidateAttacks(definition.AttackDefinitions, result);
        ValidatePassives(
            definition.PassiveDefinitions,
            attackTargetFaction,
            result);
        ValidateSkills(
            definition.SkillDefinitions,
            definition.SkillExecutionPolicy,
            attackTargetFaction,
            definition.AttackDefinitions,
            result);
        ValidateModifierTargetsAndIds(definition, result);
        ValidateCumulativeUpgrades(
            definition.CumulativeUpgradeDefinitions,
            result);
        ValidateDungeonUpgrades(
            definition.DungeonUpgradeDefinitions,
            result);
    }

    private static void ValidateIdentity(
        CharacterSO definition,
        CharacterDefinitionValidationResult result)
    {
        if (!Guid.TryParseExact(
                definition.CharacterId,
                "N",
                out _))
        {
            AddError(
                result,
                "character.id_invalid",
                "characterId",
                "CharacterId must be a persistent 32-character GUID.");
        }

        ValidateLocalization(
            definition.NameLocalizationKey,
            definition.CharacterName,
            "nameLocalizationKey",
            true,
            result);
        ValidateLocalization(
            definition.DescriptionLocalizationKey,
            definition.CharacterDescription,
            "descriptionLocalizationKey",
            false,
            result);
    }

    private static void ValidateLocalization(
        string localizationKey,
        string fallbackText,
        string path,
        bool required,
        CharacterDefinitionValidationResult result)
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
        CharacterSO definition,
        IReadOnlyList<CharacterSO> catalog,
        CharacterDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(definition.CharacterId))
            return;

        foreach (CharacterSO other in catalog)
        {
            if (other == null || ReferenceEquals(other, definition))
                continue;
            if (!string.Equals(
                    definition.CharacterId,
                    other.CharacterId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            AddError(
                result,
                "character.id_duplicate",
                "characterId",
                $"CharacterId is also used by '{other.name}'.");
            return;
        }
    }

    private static CharacterTargetFaction? ValidateAttacks(
        IReadOnlyList<CharacterAttackDefinition> definitions,
        CharacterDefinitionValidationResult result)
    {
        const string listPath = "attackDefinitions";
        if (definitions == null)
        {
            AddError(
                result,
                "attack.list_null",
                listPath,
                "Attack definition list is null.");
            return null;
        }

        if (definitions.Count == 0)
        {
            AddWarning(
                result,
                "attack.list_empty",
                listPath,
                "This character has no basic attack definition.");
            return null;
        }

        CharacterTargetFaction? previousTargetFaction = null;
        CharacterTargetFaction? consistentTargetFaction = null;
        bool hasConsistentTargetFaction = false;
        for (int index = 0; index < definitions.Count; index++)
        {
            string path = $"{listPath}[{index}]";
            CharacterAttackDefinition definition = definitions[index];
            if (definition == null)
            {
                AddError(
                    result,
                    "attack.null",
                    path,
                    "Attack definition is null.");
                previousTargetFaction = null;
                continue;
            }

            ValidateSections(definition.Sections, $"{path}.sections", result);
            if (definition.HasSection(
                    CharacterAttackSectionType.LegacyDamageAmount))
            {
                AddError(
                    result,
                    "attack.legacy_section_unsupported",
                    $"{path}.sections",
                    "LegacyDamageAmount must be migrated explicitly to " +
                    "the Ability section.");
            }
            bool hasSubject = definition.HasSection(
                CharacterAttackSectionType.Subject);
            bool hasAbility = definition.HasSection(
                CharacterAttackSectionType.Ability);
            if (!hasSubject)
            {
                AddError(
                    result,
                    "attack.subject_required",
                    $"{path}.sections",
                    "A basic attack requires a Subject section.");
            }
            if (!hasAbility)
            {
                AddError(
                    result,
                    "attack.ability_required",
                    $"{path}.sections",
                    "A basic attack requires an Ability section.");
            }

            if (index == 0 && definition.HasLinkageSection &&
                definition.Linkage != CharacterActionLinkage.None)
            {
                AddError(
                    result,
                    "attack.first_linkage_unreachable",
                    $"{path}.linkage",
                    "The first basic attack cannot depend on a previous " +
                    "basic attack.");
            }

            ValidateAttackTargetRetention(
                definition,
                hasSubject,
                path,
                result);

            if (hasSubject)
            {
                if ((definition.Subject ==
                         CharacterAttackSubject.Manual ||
                     definition.AreaDefinition.UsesWorldArea) &&
                    definitions.Count > 1)
                {
                    AddError(
                        result,
                        "attack.manual_sequence_unsupported",
                        $"{path}.subject",
                        "Spatial or manual target selection requires a " +
                        "single basic attack definition.");
                }
                if (index == 0 &&
                    definition.Subject == CharacterAttackSubject.None)
                {
                    AddError(
                        result,
                        "attack.first_target_missing",
                        $"{path}.subject",
                        "The first basic attack has no previous target to " +
                        "reuse.");
                }

                if (definition.Subject != CharacterAttackSubject.None)
                {
                    ValidateSubject(
                        definition.TargetFaction,
                        definition.Subject,
                        definition.SubjectMetric,
                        path,
                        result);
                }
            }

            bool reusesTarget = hasSubject &&
                                definition.Subject ==
                                CharacterAttackSubject.None;
            CharacterTargetFaction? abilityTargetFaction = reusesTarget
                ? previousTargetFaction
                : definition.TargetFaction;
            ValidateConditions(
                definition.HasConditionSection,
                definition.ConditionMatchMode,
                definition.NumericConditions,
                abilityTargetFaction,
                path,
                result);
            if (hasAbility)
            {
                if (definition.HasExplicitEffects)
                {
                    ValidateEffects(
                        abilityTargetFaction,
                        definition.Effects,
                        path,
                        result);
                    ValidateBattleArea(
                        BattleAbilityRules.RequiresActionTargetSelection(
                            definition),
                        definition.SubjectCount,
                        definition.AreaDefinition,
                        definition.AreaOffsets,
                        path,
                        result);
                }
                else
                {
                    AddError(
                        result,
                        "attack.legacy_ability_unsupported",
                        $"{path}.effects",
                        "Basic attack abilities require the shared effect " +
                        "list used by character skills.");
                }
            }

            if (!hasSubject || !hasAbility)
            {
                previousTargetFaction = null;
                continue;
            }

            if (!reusesTarget)
                previousTargetFaction = definition.TargetFaction;
            if (!previousTargetFaction.HasValue)
                continue;

            if (!hasConsistentTargetFaction)
            {
                consistentTargetFaction = previousTargetFaction;
                hasConsistentTargetFaction = true;
            }
            else if (consistentTargetFaction != previousTargetFaction)
            {
                consistentTargetFaction = null;
            }
        }

        return consistentTargetFaction;
    }

    private static void ValidateAttackTargetRetention(
        CharacterAttackDefinition definition,
        bool hasSubject,
        string path,
        CharacterDefinitionValidationResult result)
    {
        CharacterAttackTargetRetentionMode retentionMode =
            definition.TargetRetentionMode;
        if (!Enum.IsDefined(
                typeof(CharacterAttackTargetRetentionMode),
                retentionMode))
        {
            AddError(
                result,
                "attack.target_retention_invalid",
                $"{path}.targetRetentionMode",
                $"Unsupported target retention mode '{retentionMode}'.");
            return;
        }

        if (retentionMode ==
            CharacterAttackTargetRetentionMode.ReselectEachAttack)
        {
            return;
        }

        if (!hasSubject ||
            !CharacterAttackDefinition.SupportsTargetRetention(
                definition.Subject,
                definition.SubjectCount))
        {
            AddError(
                result,
                "attack.target_retention_unsupported",
                $"{path}.targetRetentionMode",
                "Target retention requires a fresh single-target selector.");
        }
    }

    private static void ValidatePassives(
        IReadOnlyList<CharacterPassiveDefinition> definitions,
        CharacterTargetFaction? attackTargetFaction,
        CharacterDefinitionValidationResult result)
    {
        const string listPath = "passiveDefinitions";
        if (definitions == null)
        {
            AddError(
                result,
                "passive.list_null",
                listPath,
                "Passive definition list is null.");
            return;
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            string path = $"{listPath}[{index}]";
            CharacterPassiveDefinition definition = definitions[index];
            if (definition == null)
            {
                AddError(
                    result,
                    "passive.null",
                    path,
                    "Passive definition is null.");
                continue;
            }

            if (definition.IsEmptyPlaceholder)
            {
                AddWarning(
                    result,
                    "passive.empty_placeholder",
                    $"{path}.sections",
                    "An empty passive draft is ignored and can be removed.");
                continue;
            }

            ValidateSections(definition.Sections, $"{path}.sections", result);
            bool hasAbility = definition.HasSection(
                CharacterPassiveSectionType.Ability);
            bool hasStatusContribution =
                definition.HasStatusContributionSection;
            bool hasStatModifier = definition.HasStatModifierSection;
            if (!hasAbility && !hasStatusContribution && !hasStatModifier)
            {
                AddError(
                    result,
                    "passive.ability_required",
                    $"{path}.sections",
                    "A passive requires an Ability, Stat Modifier, or " +
                    "Status Contribution section.");
            }
            if (hasStatModifier)
            {
                ValidatePassiveStatModifiers(
                    definition.StatModifiers,
                    $"{path}.statModifiers",
                    result);
            }
            if (hasStatusContribution)
            {
                ValidateStatusContributionMultipliers(
                    definition.StatusContributionMultipliers,
                    $"{path}.statusContributionMultipliers",
                    false,
                    result);
            }

            if (!hasAbility)
                continue;

            CharacterAttackSubject effectiveSubject =
                definition.HasSection(CharacterPassiveSectionType.Subject)
                    ? definition.Subject
                    : CharacterAttackSubject.Random;
            if (definition.Trigger == CharacterPassiveTrigger.OnCooldown &&
                effectiveSubject == CharacterAttackSubject.None)
            {
                AddError(
                    result,
                    "passive.cooldown_target_missing",
                    $"{path}.subject",
                    "A cooldown passive has no event or previous target to " +
                    "reuse.");
            }
            if (definition.Trigger == CharacterPassiveTrigger.OnKill &&
                effectiveSubject == CharacterAttackSubject.None)
            {
                AddError(
                    result,
                    "passive.kill_target_missing",
                    $"{path}.subject",
                    "A kill passive cannot reuse the defeated enemy because " +
                    "it has already left the board.");
            }

            bool reusesTarget =
                effectiveSubject == CharacterAttackSubject.None;
            CharacterTargetFaction? abilityTargetFaction = reusesTarget
                ? GetPassiveInheritedTargetFaction(
                    definition,
                    attackTargetFaction)
                : definition.TargetFaction;
            if (!reusesTarget)
            {
                ValidateSubject(
                    definition.TargetFaction,
                    effectiveSubject,
                    definition.SubjectMetric,
                    path,
                    result);
            }
            ValidateConditions(
                definition.HasConditionSection,
                definition.ConditionMatchMode,
                definition.NumericConditions,
                abilityTargetFaction,
                path,
                result,
                definition.HasAttackTargetRelationCondition);
            if (hasAbility)
            {
                if (definition.HasExplicitEffects)
                {
                    ValidateEffects(
                        abilityTargetFaction,
                        definition.Effects,
                        path,
                        result);
                    ValidateBattleArea(
                        BattleAbilityRules.RequiresActionTargetSelection(
                            definition),
                        definition.SubjectCount,
                        definition.AreaDefinition,
                        definition.AreaOffsets,
                        path,
                        result);
                }
                else
                {
                    AddError(
                        result,
                        "passive.legacy_ability_unsupported",
                        $"{path}.effects",
                        "Passive abilities require the shared effect list " +
                        "used by character skills.");
                }
            }

            ValidatePassiveTrigger(definition, path, result);
            ValidatePassiveAttackTargetRelation(definition, path, result);
            ValidateSelfStatusCost(definition, path, result);
        }
    }

    private static void ValidatePassiveTrigger(
        CharacterPassiveDefinition definition,
        string path,
        CharacterDefinitionValidationResult result)
    {
        if (definition.Trigger == CharacterPassiveTrigger.OnCooldown &&
            (!IsFinite(definition.Cooldown) || definition.Cooldown <= 0f))
        {
            AddError(
                result,
                "passive.cooldown_invalid",
                $"{path}.cooldown",
                "Cooldown passive interval must be greater than zero.");
        }

        if (definition.Trigger != CharacterPassiveTrigger.OnAttack &&
            definition.HasLinkageSection &&
            definition.Linkage != CharacterActionLinkage.None)
        {
            AddWarning(
                result,
                "passive.linkage_ignored",
                $"{path}.linkage",
                "Linkage is evaluated only by OnAttack passives.");
        }

        if (definition.Trigger == CharacterPassiveTrigger.OnKill)
        {
            if (!Enum.IsDefined(
                    typeof(CharacterPassiveKillSource),
                    definition.KillSource))
            {
                AddError(
                    result,
                    "passive.kill_source_invalid",
                    $"{path}.killSource",
                    $"Unsupported kill source '{definition.KillSource}'.");
            }
            else if (definition.KillSource ==
                         CharacterPassiveKillSource.SpecificCharacter &&
                     definition.SpecifiedKillerCharacter == null)
            {
                AddError(
                    result,
                    "passive.kill_character_required",
                    $"{path}.specifiedKillerCharacter",
                    "A specific-character kill passive requires a character.");
            }
        }

        if (definition.Trigger !=
            CharacterPassiveTrigger.OnStatusAcquired)
        {
            return;
        }

        if (!Enum.IsDefined(
                typeof(CharacterPassiveStatusTarget),
                definition.StatusTarget))
        {
            AddError(
                result,
                "passive.trigger_status_target_invalid",
                $"{path}.statusTarget",
                $"Unsupported trigger status target " +
                $"'{definition.StatusTarget}'.");
        }
        if (!Enum.IsDefined(
                typeof(CharacterStatusSelectionScope),
                definition.TriggerStatusScope))
        {
            AddError(
                result,
                "passive.trigger_status_scope_invalid",
                $"{path}.triggerStatusScope",
                $"Unsupported trigger status scope " +
                $"'{definition.TriggerStatusScope}'.");
            return;
        }
        if (definition.TriggerStatusScope !=
            CharacterStatusSelectionScope.SelectedStatuses)
        {
            return;
        }

        CharacterStatusSelection selection =
            definition.TriggerStatusSelection;
        for (int index = 0; index < selection.Count; index++)
        {
            StatusEffectSO status = selection.GetStatus(index);
            string statusPath = selection.UsesStatusList
                ? $"{path}.triggerStatusEffects[{index}]"
                : $"{path}.triggerStatusEffect";
            if (status == null)
            {
                AddError(
                    result,
                    "passive.trigger_status_null",
                    statusPath,
                    "Trigger status selection contains a null entry.");
                continue;
            }

            if (ContainsEarlierStatus(selection, status, index))
            {
                AddError(
                    result,
                    "passive.trigger_status_duplicate",
                    statusPath,
                    $"Status '{status.name}' is selected more than once.");
                continue;
            }

            bool canReachTarget = definition.StatusTarget switch
            {
                CharacterPassiveStatusTarget.Enemy =>
                    status.CanTargetEnemy,
                CharacterPassiveStatusTarget.Ally =>
                    status.CanTargetAlly,
                CharacterPassiveStatusTarget.All =>
                    status.CanTargetEnemy || status.CanTargetAlly,
                CharacterPassiveStatusTarget.Self =>
                    status.CanTargetAlly,
                _ => false
            };
            if (!canReachTarget)
            {
                AddError(
                    result,
                    "passive.trigger_status_faction_mismatch",
                    statusPath,
                    $"Status '{status.name}' cannot be acquired by the " +
                    "selected trigger faction.");
            }
        }
    }

    private static void ValidatePassiveAttackTargetRelation(
        CharacterPassiveDefinition definition,
        string path,
        CharacterDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(
                typeof(CharacterPassiveAttackTargetRelation),
                definition.AttackTargetRelation))
        {
            AddError(
                result,
                "passive.attack_target_relation_invalid",
                $"{path}.attackTargetRelation",
                $"Unsupported attack target relation " +
                $"'{definition.AttackTargetRelation}'.");
            return;
        }

        if (definition.AttackTargetRelation ==
            CharacterPassiveAttackTargetRelation.Any)
        {
            return;
        }

        if (!definition.HasConditionSection)
        {
            AddWarning(
                result,
                "passive.attack_target_relation_section_missing",
                $"{path}.attackTargetRelation",
                "The attack target relation is ignored because the " +
                "Condition section is absent.");
        }

        if (definition.Trigger != CharacterPassiveTrigger.OnAttack &&
            definition.Trigger !=
            CharacterPassiveTrigger.OnAttackTargetSelected)
        {
            AddWarning(
                result,
                "passive.attack_target_relation_trigger_ignored",
                $"{path}.attackTargetRelation",
                "The attack target relation is evaluated only by attack " +
                "passives.");
        }
    }

    private static CharacterTargetFaction?
        GetPassiveInheritedTargetFaction(
            CharacterPassiveDefinition definition,
            CharacterTargetFaction? attackTargetFaction)
    {
        if (definition == null)
            return null;

        if (definition.Trigger == CharacterPassiveTrigger.OnAttack ||
            definition.Trigger ==
            CharacterPassiveTrigger.OnAttackTargetSelected)
        {
            return attackTargetFaction;
        }
        if (definition.Trigger == CharacterPassiveTrigger.OnCooldown ||
            definition.Trigger == CharacterPassiveTrigger.OnKill)
            return null;

        if (definition.StatusTarget == CharacterPassiveStatusTarget.Ally ||
            definition.StatusTarget == CharacterPassiveStatusTarget.Self)
            return CharacterTargetFaction.Ally;
        if (definition.StatusTarget == CharacterPassiveStatusTarget.Enemy)
            return CharacterTargetFaction.Enemy;

        if (definition.TriggerStatusScope !=
            CharacterStatusSelectionScope.SelectedStatuses)
        {
            return null;
        }

        CharacterStatusSelection triggerStatuses =
            definition.TriggerStatusSelection;
        if (triggerStatuses.Count == 0)
        {
            return null;
        }

        CharacterTargetFaction? inferredFaction = null;
        for (int index = 0; index < triggerStatuses.Count; index++)
        {
            StatusEffectSO triggerStatus =
                triggerStatuses.GetStatus(index);
            if (triggerStatus == null ||
                triggerStatus.CanTargetAlly ==
                triggerStatus.CanTargetEnemy)
            {
                return null;
            }

            CharacterTargetFaction statusFaction =
                triggerStatus.CanTargetAlly
                    ? CharacterTargetFaction.Ally
                    : CharacterTargetFaction.Enemy;
            if (inferredFaction.HasValue &&
                inferredFaction.Value != statusFaction)
            {
                return null;
            }

            inferredFaction = statusFaction;
        }

        return inferredFaction;
    }

    private static void ValidateSelfStatusCost(
        CharacterPassiveDefinition definition,
        string path,
        CharacterDefinitionValidationResult result)
    {
        bool hasSection = definition.HasSection(
            CharacterPassiveSectionType.SelfStatusCost);
        CharacterStatusStackCostDefinition cost = definition.SelfStatusCost;
        if (!hasSection)
        {
            if (cost?.IsConfigured == true)
            {
                AddWarning(
                    result,
                    "passive.cost_section_missing",
                    $"{path}.selfStatusCost",
                    "Configured self status cost is ignored because its " +
                    "section is absent.");
            }

            return;
        }

        if (cost == null || cost.StatusEffect == null)
        {
            AddError(
                result,
                "passive.cost_status_required",
                $"{path}.selfStatusCost.statusEffect",
                "SelfStatusCost requires a status effect.");
            return;
        }

        StatusEffectSO status = cost.StatusEffect;
        if (!status.CanTargetAlly)
        {
            AddError(
                result,
                "passive.cost_status_faction_mismatch",
                $"{path}.selfStatusCost.statusEffect",
                $"Cost status '{status.name}' cannot be held by this " +
                "character.");
        }
        if (!status.HasUnlimitedStacks &&
            cost.RequiredStacks > status.MaximumStacks)
        {
            AddError(
                result,
                "passive.cost_unreachable",
                $"{path}.selfStatusCost.requiredStacks",
                $"Required stacks ({cost.RequiredStacks}) exceed status " +
                $"maximum ({status.MaximumStacks}).");
        }
    }

    private static void ValidateSkills(
        IReadOnlyList<CharacterSkillDefinition> definitions,
        CharacterSkillExecutionPolicy executionPolicy,
        CharacterTargetFaction? attackTargetFaction,
        IReadOnlyList<CharacterAttackDefinition> attackDefinitions,
        CharacterDefinitionValidationResult result)
    {
        const string listPath = "skillDefinitions";
        if (definitions == null)
        {
            AddError(
                result,
                "skill.list_null",
                listPath,
                "Skill definition list is null.");
            return;
        }

        CharacterAttackDefinition linkedTargetFallback =
            FindLinkedSkillFallbackAttack(attackDefinitions);
        CharacterTargetFaction? previousSequenceTargetFaction =
            attackTargetFaction;
        for (int index = 0; index < definitions.Count; index++)
        {
            string path = $"{listPath}[{index}]";
            CharacterSkillDefinition definition = definitions[index];
            if (definition == null)
            {
                AddError(
                    result,
                    "skill.null",
                    path,
                    "Skill definition is null.");
                if (executionPolicy ==
                    CharacterSkillExecutionPolicy.SequenceAll)
                {
                    previousSequenceTargetFaction = null;
                }
                continue;
            }

            ValidateSections(definition.Sections, $"{path}.sections", result);
            bool hasAbility = definition.HasSection(
                CharacterSkillSectionType.Ability);
            if (!hasAbility)
            {
                AddError(
                    result,
                    "skill.ability_required",
                    $"{path}.sections",
                    "A skill step requires an Ability section.");
            }

            CharacterAttackSubject effectiveSubject =
                definition.HasSection(CharacterSkillSectionType.Subject)
                    ? definition.Subject
                    : CharacterAttackSubject.Random;
            if (effectiveSubject == CharacterAttackSubject.Manual &&
                definitions.Count > 1)
            {
                AddError(
                    result,
                    "skill.manual_sequence_unsupported",
                    $"{path}.subject",
                    "Manual target selection currently requires a single " +
                    "skill definition.");
            }
            bool reusesTarget =
                effectiveSubject == CharacterAttackSubject.None;
            bool usesActionTargets =
                BattleAbilityRules.RequiresActionTargets(definition);
            if (reusesTarget && usesActionTargets &&
                linkedTargetFallback == null)
            {
                AddWarning(
                    result,
                    "skill.linked_target_fallback_missing",
                    $"{path}.subject",
                    "This skill can reuse an inherited target, but it has " +
                    "no independent basic-attack selector to use when that " +
                    "target is missing or invalid.");
            }
            else if (reusesTarget && usesActionTargets &&
                     linkedTargetFallback.Subject ==
                         CharacterAttackSubject.Manual &&
                     definitions.Count > 1)
            {
                AddError(
                    result,
                    "skill.linked_manual_sequence_unsupported",
                    $"{path}.subject",
                    "A linked skill cannot fall back to manual basic-attack " +
                    "targeting when multiple skill definitions are " +
                    "configured.");
            }
            CharacterTargetFaction? abilityTargetFaction = reusesTarget
                ? executionPolicy ==
                  CharacterSkillExecutionPolicy.SequenceAll
                    ? previousSequenceTargetFaction
                    : attackTargetFaction
                : definition.TargetFaction;
            if (!reusesTarget)
            {
                ValidateSubject(
                    definition.TargetFaction,
                    effectiveSubject,
                    definition.SubjectMetric,
                    path,
                    result);
            }
            ValidateConditions(
                definition.HasConditionSection,
                definition.ConditionMatchMode,
                definition.NumericConditions,
                abilityTargetFaction,
                path,
                result);
            if (hasAbility)
            {
                if (definition.HasExplicitEffects)
                {
                    ValidateEffects(
                        abilityTargetFaction,
                        definition.Effects,
                        path,
                        result);
                    ValidateBattleArea(
                        BattleAbilityRules.RequiresActionTargetSelection(
                            definition),
                        definition.SubjectCount,
                        definition.AreaDefinition,
                        definition.AreaOffsets,
                        path,
                        result);
                }
                else
                {
                    AddError(
                        result,
                        "skill.legacy_ability_unsupported",
                        $"{path}.effects",
                        "Skills require the shared battle effect list.");
                }

                if (executionPolicy ==
                    CharacterSkillExecutionPolicy.SequenceAll)
                {
                    previousSequenceTargetFaction = abilityTargetFaction;
                }
            }
            else if (executionPolicy ==
                     CharacterSkillExecutionPolicy.SequenceAll)
            {
                previousSequenceTargetFaction = null;
            }

            bool hasCost = definition.HasSection(
                CharacterSkillSectionType.Cost);
            if (executionPolicy ==
                    CharacterSkillExecutionPolicy.FirstSuccessful &&
                !hasCost)
            {
                AddWarning(
                    result,
                    "skill.cost_missing",
                    $"{path}.sections",
                    "This fallback skill definition is free because it has " +
                    "no Cost section.");
            }
            else if (executionPolicy ==
                         CharacterSkillExecutionPolicy.SequenceAll &&
                     index == 0 && !hasCost)
            {
                AddWarning(
                    result,
                    "skill.cost_missing",
                    $"{path}.sections",
                    "The active skill is free because its first definition " +
                    "has no Cost section.");
            }
            else if (executionPolicy ==
                         CharacterSkillExecutionPolicy.SequenceAll &&
                     index > 0 &&
                     hasCost)
            {
                AddWarning(
                    result,
                    "skill.sequence_cost_ignored",
                    $"{path}.cost",
                    "SequenceAll uses only the first definition's cost.");
            }
        }
    }

    private static CharacterAttackDefinition FindLinkedSkillFallbackAttack(
        IReadOnlyList<CharacterAttackDefinition> definitions)
    {
        if (definitions == null)
            return null;

        foreach (CharacterAttackDefinition definition in definitions)
        {
            if (definition == null ||
                !definition.HasSection(CharacterAttackSectionType.Subject) ||
                !definition.HasSection(CharacterAttackSectionType.Ability) ||
                definition.Subject == CharacterAttackSubject.None ||
                (definition.HasLinkageSection &&
                 definition.Linkage != CharacterActionLinkage.None))
            {
                continue;
            }

            return definition;
        }

        return null;
    }

    private static void ValidateCumulativeUpgrades(
        IReadOnlyList<CharacterCumulativeUpgradeDefinition> definitions,
        CharacterDefinitionValidationResult result)
    {
        const string listPath = "cumulativeUpgradeDefinitions";
        if (definitions == null)
        {
            AddError(
                result,
                "cumulative.list_null",
                listPath,
                "Cumulative upgrade definition list is null.");
            return;
        }

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Count; index++)
        {
            CharacterCumulativeUpgradeDefinition definition =
                definitions[index];
            string path = $"{listPath}[{index}]";
            if (definition == null)
            {
                AddError(
                    result,
                    "cumulative.null",
                    path,
                    "Cumulative upgrade definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.UpgradeId))
            {
                AddError(
                    result,
                    "cumulative.id_required",
                    $"{path}.upgradeId",
                    "Cumulative upgrade ID is required.");
            }
            else if (!seenIds.Add(definition.UpgradeId))
            {
                AddError(
                    result,
                    "cumulative.id_duplicate",
                    $"{path}.upgradeId",
                    $"Cumulative upgrade ID '{definition.UpgradeId}' is " +
                    "duplicated.");
            }

            IReadOnlyList<CharacterCumulativeUpgradeModifier> modifiers =
                definition.Modifiers;
            if (modifiers == null || modifiers.Count == 0)
            {
                AddError(
                    result,
                    "cumulative.modifier_required",
                    $"{path}.modifiers",
                    "At least one cumulative upgrade modifier is required.");
                continue;
            }

            HashSet<CharacterCumulativeUpgradeModifierType> seenTypes =
                new();
            for (int modifierIndex = 0;
                 modifierIndex < modifiers.Count;
                 modifierIndex++)
            {
                CharacterCumulativeUpgradeModifier modifier =
                    modifiers[modifierIndex];
                string modifierPath =
                    $"{path}.modifiers[{modifierIndex}]";
                if (modifier == null)
                {
                    AddError(
                        result,
                        "cumulative.modifier_null",
                        modifierPath,
                        "Cumulative upgrade modifier is null.");
                    continue;
                }

                if (!Enum.IsDefined(
                        typeof(CharacterCumulativeUpgradeModifierType),
                        modifier.Type))
                {
                    AddError(
                        result,
                        "cumulative.modifier_type_invalid",
                        $"{modifierPath}.type",
                        $"Unsupported cumulative modifier type " +
                        $"'{modifier.Type}'.");
                }
                else if (!seenTypes.Add(modifier.Type))
                {
                    AddWarning(
                        result,
                        "cumulative.modifier_duplicate",
                        $"{modifierPath}.type",
                        $"Modifier type '{modifier.Type}' is duplicated and " +
                        "will be summed.");
                }

                if (!IsFinite(modifier.ValuePerLevel) ||
                    modifier.ValuePerLevel == 0f)
                {
                    AddError(
                        result,
                        "cumulative.modifier_value_invalid",
                        $"{modifierPath}.valuePerLevel",
                        "Modifier value per level must be finite and non-zero.");
                    continue;
                }

                if ((modifier.Type ==
                         CharacterCumulativeUpgradeModifierType
                             .MaximumHealth ||
                     modifier.Type ==
                         CharacterCumulativeUpgradeModifierType
                             .SkillCostReduction) &&
                    !Mathf.Approximately(
                        modifier.ValuePerLevel,
                        Mathf.Round(modifier.ValuePerLevel)))
                {
                    AddError(
                        result,
                        "cumulative.modifier_integer_required",
                        $"{modifierPath}.valuePerLevel",
                        "Maximum health and skill cost modifiers must use " +
                        "whole-number values per level.");
                }
            }
        }
    }

    private static void ValidateDungeonUpgrades(
        IReadOnlyList<CharacterDungeonUpgradeDefinition> definitions,
        CharacterDefinitionValidationResult result)
    {
        const string listPath = "dungeonUpgradeDefinitions";
        if (definitions == null)
        {
            AddError(
                result,
                "upgrade.list_null",
                listPath,
                "Dungeon upgrade definition list is null.");
            return;
        }

        HashSet<string> allUpgradeIds = new(StringComparer.Ordinal);

        for (int definitionIndex = 0;
             definitionIndex < definitions.Count;
             definitionIndex++)
        {
            string path = $"{listPath}[{definitionIndex}]";
            CharacterDungeonUpgradeDefinition definition =
                definitions[definitionIndex];
            if (definition == null)
            {
                AddError(
                    result,
                    "upgrade.null",
                    path,
                    "Dungeon upgrade definition is null.");
                continue;
            }

            IReadOnlyList<CharacterDungeonUpgradeEntry> entries =
                definition.Entries;
            if (entries == null || entries.Count == 0)
            {
                AddError(
                    result,
                    "upgrade.entries_empty",
                    $"{path}.entries",
                    "Dungeon upgrade definition requires at least one entry.");
                continue;
            }

            HashSet<CharacterDungeonUpgradeType> seenTypes = new();
            for (int entryIndex = 0;
                 entryIndex < entries.Count;
                 entryIndex++)
            {
                CharacterDungeonUpgradeEntry entry = entries[entryIndex];
                string entryPath = $"{path}.entries[{entryIndex}]";
                if (entry == null)
                {
                    AddError(
                        result,
                        "upgrade.entry_null",
                        entryPath,
                        "Dungeon upgrade entry is null.");
                    continue;
                }

                if (definition.UsesLegacyProbabilityMode &&
                    !seenTypes.Add(entry.Type))
                {
                    AddError(
                        result,
                        "upgrade.type_duplicate",
                        $"{entryPath}.type",
                        $"Upgrade type '{entry.Type}' is duplicated.");
                }
                if (!allUpgradeIds.Add(entry.UpgradeId))
                {
                    AddError(
                        result,
                        "upgrade.id_duplicate",
                        $"{entryPath}.upgradeId",
                        $"Upgrade id '{entry.UpgradeId}' is duplicated " +
                        "across this character's dungeon upgrade pools.");
                }
                if (!definition.UsesLegacyProbabilityMode &&
                    !entry.HasExplicitUpgradeId)
                {
                    AddError(
                        result,
                        "upgrade.id_missing",
                        $"{entryPath}.upgradeId",
                        "Modular dungeon upgrades require a stable id.");
                }
                for (int moduleIndex = 0;
                     moduleIndex < entry.ModifierModules.Count;
                     moduleIndex++)
                {
                    CharacterModifierModule module =
                        entry.ModifierModules[moduleIndex];
                    string modulePath =
                        $"{entryPath}.modifierModules[{moduleIndex}]";
                    if (module == null)
                    {
                        AddError(
                            result,
                            "upgrade.module_null",
                            modulePath,
                            "Modifier module is null.");
                    }
                    else if (!IsFinite(module.ValuePerStack) ||
                             module.ValuePerStack == 0f)
                    {
                        AddError(
                            result,
                            "upgrade.module_value_invalid",
                            $"{modulePath}.valuePerStack",
                            "Modifier value must be finite and non-zero.");
                    }
                }
                if (!IsFinite(entry.Probability) || entry.Probability < 0f)
                {
                    AddError(
                        result,
                        "upgrade.probability_invalid",
                        $"{entryPath}.probability",
                        "Upgrade probability must be a finite non-negative " +
                        "number.");
                }
                else if (entry.Probability <= 0f)
                {
                    AddWarning(
                        result,
                        "upgrade.probability_zero",
                        $"{entryPath}.probability",
                        "This upgrade entry can never be selected.");
                }
            }

            if (definition.UsesLegacyProbabilityMode &&
                !definition.HasValidProbabilityTotal)
            {
                AddError(
                    result,
                    "upgrade.probability_total",
                    $"{path}.entries",
                    $"Upgrade probabilities total " +
                    $"{definition.TotalProbability:0.###}; expected " +
                    $"{CharacterDungeonUpgradeDefinition.RequiredProbabilityTotal:0.###}.");
            }
        }
    }

    private static void ValidateModifierTargetsAndIds(
        CharacterSO definition,
        CharacterDefinitionValidationResult result)
    {
        Dictionary<CharacterActionKind, Dictionary<string, HashSet<string>>>
            actions = new();
        RegisterActions(
            definition.AttackDefinitions,
            CharacterActionKind.Attack,
            action => action?.ActionId,
            action => action?.Effects,
            "attackDefinitions",
            actions,
            result);
        RegisterActions(
            definition.PassiveDefinitions,
            CharacterActionKind.Passive,
            action => action?.ActionId,
            action => action?.Effects,
            "passiveDefinitions",
            actions,
            result);
        RegisterActions(
            definition.SkillDefinitions,
            CharacterActionKind.Skill,
            action => action?.ActionId,
            action => action?.Effects,
            "skillDefinitions",
            actions,
            result);

        for (int definitionIndex = 0;
             definitionIndex < definition.CumulativeUpgradeDefinitions.Count;
             definitionIndex++)
        {
            CharacterCumulativeUpgradeDefinition upgrade =
                definition.CumulativeUpgradeDefinitions[definitionIndex];
            if (upgrade == null)
                continue;
            ValidateModifierModules(
                upgrade.ModifierModules,
                $"cumulativeUpgradeDefinitions[{definitionIndex}]" +
                ".modifierModules",
                actions,
                result);
        }

        for (int poolIndex = 0;
             poolIndex < definition.DungeonUpgradeDefinitions.Count;
             poolIndex++)
        {
            CharacterDungeonUpgradeDefinition pool =
                definition.DungeonUpgradeDefinitions[poolIndex];
            if (pool == null)
                continue;
            for (int entryIndex = 0;
                 entryIndex < pool.Entries.Count;
                 entryIndex++)
            {
                CharacterDungeonUpgradeEntry entry =
                    pool.Entries[entryIndex];
                if (entry == null)
                    continue;
                ValidateModifierModules(
                    entry.ModifierModules,
                    $"dungeonUpgradeDefinitions[{poolIndex}].entries" +
                    $"[{entryIndex}].modifierModules",
                    actions,
                    result);
            }
        }
    }

    private static void RegisterActions<TAction>(
        IReadOnlyList<TAction> definitions,
        CharacterActionKind actionKind,
        Func<TAction, string> getActionId,
        Func<TAction, IReadOnlyList<CharacterEffectDefinition>> getEffects,
        string path,
        Dictionary<CharacterActionKind,
            Dictionary<string, HashSet<string>>> actions,
        CharacterDefinitionValidationResult result)
        where TAction : class
    {
        Dictionary<string, HashSet<string>> byId =
            new(StringComparer.Ordinal);
        actions[actionKind] = byId;
        if (definitions == null)
            return;

        for (int index = 0; index < definitions.Count; index++)
        {
            TAction action = definitions[index];
            if (action == null)
                continue;
            string actionId = (getActionId(action) ?? string.Empty).Trim();
            IReadOnlyList<CharacterEffectDefinition> effects =
                getEffects(action) ?? Array.Empty<CharacterEffectDefinition>();
            HashSet<string> effectIds = new(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(actionId) &&
                !byId.TryAdd(actionId, effectIds))
            {
                AddError(
                    result,
                    "modifier.action_id_duplicate",
                    $"{path}[{index}].actionId",
                    $"Action id '{actionId}' is duplicated.");
            }

            for (int effectIndex = 0;
                 effectIndex < effects.Count;
                 effectIndex++)
            {
                string effectId =
                    effects[effectIndex]?.EffectId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(effectId))
                    continue;
                if (!effectIds.Add(effectId))
                {
                    AddError(
                        result,
                        "modifier.effect_id_duplicate",
                        $"{path}[{index}].effects[{effectIndex}].effectId",
                        $"Effect id '{effectId}' is duplicated in action " +
                        $"'{actionId}'.");
                }
            }
        }
    }

    private static void ValidateModifierModules(
        IReadOnlyList<CharacterModifierModule> modules,
        string path,
        Dictionary<CharacterActionKind,
            Dictionary<string, HashSet<string>>> actions,
        CharacterDefinitionValidationResult result)
    {
        if (modules == null)
            return;
        HashSet<string> moduleIds = new(StringComparer.Ordinal);
        for (int index = 0; index < modules.Count; index++)
        {
            CharacterModifierModule module = modules[index];
            if (module == null)
                continue;
            string modulePath = $"{path}[{index}]";
            if (!string.IsNullOrWhiteSpace(module.ModuleId) &&
                !moduleIds.Add(module.ModuleId))
            {
                AddError(
                    result,
                    "modifier.module_id_duplicate",
                    $"{modulePath}.moduleId",
                    $"Modifier module id '{module.ModuleId}' is duplicated.");
            }

            CharacterModifierTarget target = module.Target;
            if (target == null ||
                target.Scope == CharacterModifierTargetScope.Character ||
                target.Scope == CharacterModifierTargetScope.ActionKind)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.ActionId) ||
                !actions.TryGetValue(
                    target.ActionKind,
                    out Dictionary<string, HashSet<string>> byId) ||
                !byId.TryGetValue(target.ActionId, out HashSet<string> effects))
            {
                AddError(
                    result,
                    "modifier.action_target_missing",
                    $"{modulePath}.target.actionId",
                    $"Target action '{target.ActionId}' does not exist.");
                continue;
            }

            if (target.Scope == CharacterModifierTargetScope.Effect &&
                (string.IsNullOrWhiteSpace(target.EffectId) ||
                 !effects.Contains(target.EffectId)))
            {
                AddError(
                    result,
                    "modifier.effect_target_missing",
                    $"{modulePath}.target.effectId",
                    $"Target effect '{target.EffectId}' does not exist in " +
                    $"action '{target.ActionId}'.");
            }
        }
    }

    private static void ValidateConditions(
        bool hasConditionSection,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions,
        CharacterTargetFaction? targetFaction,
        string actionPath,
        CharacterDefinitionValidationResult result,
        bool hasAdditionalCondition = false)
    {
        string path = $"{actionPath}.numericConditions";
        if (!hasConditionSection)
        {
            if (conditions != null && conditions.Count > 0)
            {
                AddWarning(
                    result,
                    "condition.section_missing",
                    path,
                    "Configured conditions are ignored because the Condition " +
                    "section is absent.");
            }

            return;
        }

        if (conditions == null || conditions.Count == 0)
        {
            if (hasAdditionalCondition)
                return;

            AddError(
                result,
                "condition.empty",
                path,
                matchMode == CharacterConditionMatchMode.Any
                    ? "An empty Any condition can never match."
                    : "The Condition section has no conditions.");
            return;
        }

        for (int index = 0; index < conditions.Count; index++)
        {
            CharacterNumericCondition condition = conditions[index];
            string conditionPath = $"{path}[{index}]";
            if (condition == null)
            {
                AddError(
                    result,
                    "condition.null",
                    conditionPath,
                    "Condition is null.");
                continue;
            }

            if (!Enum.IsDefined(
                    typeof(CharacterConditionTarget),
                    condition.Target))
            {
                AddError(
                    result,
                    "condition.target_invalid",
                    $"{conditionPath}.target",
                    $"Unsupported condition target '{condition.Target}'.");
                continue;
            }

            CharacterTargetFaction? conditionTargetFaction =
                condition.Target == CharacterConditionTarget.Source
                    ? CharacterTargetFaction.Ally
                    : targetFaction;
            if (!conditionTargetFaction.HasValue)
            {
                AddError(
                    result,
                    "condition.action_target_unavailable",
                    $"{conditionPath}.target",
                    "The action target cannot be resolved for this " +
                    "condition.");
            }

            bool checksStatusStacks =
                condition.Metric ==
                CharacterNumericConditionMetric.StatusStackCount;
            if (checksStatusStacks)
            {
                bool hasValidStatusScope = Enum.IsDefined(
                    typeof(CharacterStatusSelectionScope),
                    condition.StatusSelectionScope);
                if (!hasValidStatusScope)
                {
                    AddError(
                        result,
                        "condition.status_selection_scope_invalid",
                        $"{conditionPath}.statusSelectionScope",
                        $"Unsupported status selection scope " +
                        $"'{condition.StatusSelectionScope}'.");
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
                        "condition.status_required",
                        $"{conditionPath}.statusEffects",
                        "Status stack condition requires at least one " +
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
                        ? $"{conditionPath}.statusEffects[{statusIndex}]"
                        : $"{conditionPath}.statusEffect";
                    if (status == null)
                    {
                        AddError(
                            result,
                            "condition.status_null",
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
                            "condition.status_duplicate",
                            statusPath,
                            $"Status '{status.name}' is selected more than " +
                            "once.");
                        continue;
                    }

                    uniqueStatusCount++;
                    if (conditionTargetFaction.HasValue &&
                        !CanTargetFaction(
                            status,
                            conditionTargetFaction.Value))
                    {
                        AddError(
                            result,
                            "condition.status_faction_mismatch",
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
                        "condition.status_match_mode_invalid",
                        $"{conditionPath}.statusMatchMode",
                        $"Unsupported status match mode " +
                        $"'{condition.StatusMatchMode}'.");
                }
                else if (condition.StatusMatchMode ==
                         CharacterStatusConditionMatchMode.AtLeastCount)
                {
                    if (condition.StatusMatchCount < 1)
                    {
                        AddError(
                            result,
                            "condition.status_match_count_invalid",
                            $"{conditionPath}.statusMatchCount",
                            "Required status match count must be at least 1.");
                    }
                    else if (condition.StatusMatchCount >
                             uniqueStatusCount &&
                             selectsConfiguredStatuses)
                    {
                        AddError(
                            result,
                            "condition.status_match_count_exceeds_selection",
                            $"{conditionPath}.statusMatchCount",
                            "Required status match count cannot exceed the " +
                            "number of selected statuses.");
                    }
                }
            }

            if (conditionTargetFaction.HasValue &&
                !IsConditionMetricSupported(
                    condition.Metric,
                    conditionTargetFaction.Value))
            {
                AddError(
                    result,
                    "condition.metric_faction_mismatch",
                    $"{conditionPath}.metric",
                    $"Metric '{condition.Metric}' is not supported for " +
                    $"{conditionTargetFaction.Value} condition targets.");
            }
            if (!IsFinite(condition.Threshold))
            {
                AddError(
                    result,
                    "condition.threshold_invalid",
                    $"{conditionPath}.threshold",
                    "Condition threshold must be finite.");
            }
            else if (condition.Metric ==
                         CharacterNumericConditionMetric.StackCount ||
                     checksStatusStacks)
            {
                if (condition.Threshold < 0f)
                {
                    AddError(
                        result,
                        "condition.threshold_negative",
                        $"{conditionPath}.threshold",
                        "Stack condition threshold cannot be negative.");
                }
                else if (!Mathf.Approximately(
                             condition.Threshold,
                             Mathf.Round(condition.Threshold)))
                {
                    AddError(
                        result,
                        "condition.threshold_not_integer",
                        $"{conditionPath}.threshold",
                        "Stack condition threshold must be a whole number.");
                }
            }
        }
    }

    private static void ValidateSubject(
        CharacterTargetFaction targetFaction,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        string actionPath,
        CharacterDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(
                typeof(CharacterAttackSubject),
                subject))
        {
            AddError(
                result,
                "subject.mode_invalid",
                $"{actionPath}.subject",
                $"Unsupported target selection mode '{subject}'.");
            return;
        }

        if (targetFaction == CharacterTargetFaction.Enemy &&
            (subject == CharacterAttackSubject.Self ||
             subject == CharacterAttackSubject.AllExceptSelf ||
             subject == CharacterAttackSubject.RandomExceptSelf))
        {
            AddWarning(
                result,
                "subject.enemy_mode_normalized",
                $"{actionPath}.subject",
                $"Enemy targeting normalizes '{subject}' to a non-self " +
                "selection mode.");
        }

        if ((subject == CharacterAttackSubject.HighestValue ||
             subject == CharacterAttackSubject.LowestValue) &&
            !IsSubjectMetricSupported(metric, targetFaction))
        {
            AddError(
                result,
                "subject.metric_faction_mismatch",
                $"{actionPath}.subjectMetric",
                $"Metric '{metric}' is not supported for {targetFaction} " +
                "targets.");
        }
    }

    private static void ValidateEffects(
        CharacterTargetFaction? targetFaction,
        IReadOnlyList<CharacterEffectDefinition> effects,
        string actionPath,
        CharacterDefinitionValidationResult result)
    {
        if (effects == null)
        {
            AddError(
                result,
                "effect.list_null",
                $"{actionPath}.effects",
                "Explicit effect list is null.");
            return;
        }

        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            string effectPath = $"{actionPath}.effects[{index}]";
            if (effect == null)
            {
                AddError(
                    result,
                    "effect.null",
                    effectPath,
                    "Effect definition is null.");
                continue;
            }

            if (!BattleEffectRules.TryValidate(
                    effect,
                    out string sharedEffectError))
            {
                AddError(
                    result,
                    "effect.shared_invalid",
                    effectPath,
                    sharedEffectError);
            }

            if (!Enum.IsDefined(
                    typeof(CharacterEffectPreconditionFailurePolicy),
                    effect.PreconditionFailurePolicy))
            {
                AddError(
                    result,
                    "effect.precondition_policy_invalid",
                    $"{effectPath}.preconditionFailurePolicy",
                    $"Unsupported precondition failure policy " +
                    $"'{effect.PreconditionFailurePolicy}'.");
            }

            if (!Enum.IsDefined(
                    typeof(CharacterEffectFailurePolicy),
                    effect.FailurePolicy))
            {
                AddError(
                    result,
                    "effect.failure_policy_invalid",
                    $"{effectPath}.failurePolicy",
                    $"Unsupported effect failure policy " +
                    $"'{effect.FailurePolicy}'.");
            }
            else if (effect.FailurePolicy ==
                         CharacterEffectFailurePolicy
                             .StopRemainingEffects &&
                     index == effects.Count - 1)
            {
                AddWarning(
                    result,
                    "effect.stop_failure_last_noop",
                    $"{effectPath}.failurePolicy",
                    "Stopping remaining effects has no effect on the last " +
                    "entry.");
            }

            if (!Enum.IsDefined(
                    typeof(CharacterEffectTargetMode),
                    effect.TargetMode))
            {
                AddError(
                    result,
                    "effect.target_mode_invalid",
                    $"{effectPath}.targetMode",
                    $"Unsupported effect target mode " +
                    $"'{effect.TargetMode}'.");
                continue;
            }

            CharacterTargetFaction? effectTargetFaction;
            if (!BattleEffectRules.RequiresTargets(
                    effect.BattleEffectType))
            {
                effectTargetFaction = targetFaction;
                if (effect.TargetMode !=
                    CharacterEffectTargetMode.InheritAction)
                {
                    AddWarning(
                        result,
                        "effect.resource_target_mode_ignored",
                        $"{effectPath}.targetMode",
                        $"{effect.Type} executes once and ignores its " +
                        "target mode.");
                }
            }
            else if (effect.TargetMode ==
                     CharacterEffectTargetMode.Source)
            {
                effectTargetFaction = CharacterTargetFaction.Ally;
            }
            else if (effect.TargetMode ==
                     CharacterEffectTargetMode.FreshSelection)
            {
                effectTargetFaction = ValidateFreshEffectSelector(
                    effect.TargetSelector,
                    effectPath,
                    result);
            }
            else
            {
                effectTargetFaction = targetFaction;
            }

            switch (effect.Type)
            {
                case CharacterEffectType.Damage:
                    ValidateDamageEffect(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        result);
                    break;

                case CharacterEffectType.ApplyStatus:
                    ValidateApplyStatusEffect(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        result);
                    break;

                case CharacterEffectType.RemoveStatus:
                    ValidateRemoveStatusEffect(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        result);
                    break;

                case CharacterEffectType.GainResource:
                    ValidateAmountScaling(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        "effect.resource_gain_invalid",
                        "Resource gain",
                        false,
                        result);
                    break;

                case CharacterEffectType.SpendResource:
                    ValidateResourceSpendEffect(
                        effect,
                        effectPath,
                        result);
                    break;

                case CharacterEffectType.Heal:
                    ValidateAmountScaling(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        "effect.heal_invalid",
                        "Healing",
                        true,
                        result);
                    break;

                case CharacterEffectType.Shield:
                    ValidateAmountScaling(
                        effectTargetFaction,
                        effect,
                        effectPath,
                        "effect.shield_invalid",
                        "Shield",
                        true,
                        result);
                    break;

                case CharacterEffectType.SpendHealth:
                    ValidateHealthSpendEffect(
                        effect,
                        effectPath,
                        result);
                    break;

                case CharacterEffectType.CardDraw:
                    ValidateCardDrawEffect(
                        effect,
                        effectPath,
                        result);
                    break;

                default:
                    AddError(
                        result,
                        "effect.type_unknown",
                        $"{effectPath}.type",
                        $"Unsupported effect type '{effect.Type}'.");
                    break;
            }

            ValidateStatusContributionMultipliers(
                effect.StatusContributionMultipliers,
                $"{effectPath}.statusContributionMultipliers",
                true,
                result);
        }
    }

    private static void ValidateStatusContributionMultipliers(
        IReadOnlyList<CharacterStatusStatContributionMultiplier> modifiers,
        string path,
        bool effectLocal,
        CharacterDefinitionValidationResult result)
    {
        if (modifiers == null)
        {
            AddError(
                result,
                "status_contribution.list_null",
                path,
                "Status contribution multiplier list is null.");
            return;
        }

        for (int index = 0; index < modifiers.Count; index++)
        {
            string modifierPath = $"{path}[{index}]";
            CharacterStatusStatContributionMultiplier modifier =
                modifiers[index];
            if (modifier == null)
            {
                AddError(
                    result,
                    "status_contribution.null",
                    modifierPath,
                    "Status contribution multiplier is null.");
                continue;
            }

            StatusEffectSO status = modifier.StatusEffect;
            if (status == null)
            {
                AddError(
                    result,
                    "status_contribution.status_required",
                    $"{modifierPath}.statusEffect",
                    "A status contribution multiplier requires a status.");
            }
            else if (!status.CanTargetAlly)
            {
                AddError(
                    result,
                    "status_contribution.status_faction_mismatch",
                    $"{modifierPath}.statusEffect",
                    $"Status '{status.name}' cannot be held by the source " +
                    "character.");
            }

            if (!Enum.IsDefined(
                    typeof(StatusEffectStatType),
                    modifier.StatType))
            {
                AddError(
                    result,
                    "status_contribution.stat_invalid",
                    $"{modifierPath}.statType",
                    $"Unsupported status stat type '{modifier.StatType}'.");
            }
            else if (effectLocal &&
                     modifier.StatType != StatusEffectStatType.AttackPower)
            {
                AddError(
                    result,
                    "status_contribution.effect_stat_unsupported",
                    $"{modifierPath}.statType",
                    "Effect-local contribution multipliers currently " +
                    "support AttackPower only.");
            }
            else if (modifier.StatType ==
                     StatusEffectStatType.TargetPriority)
            {
                AddError(
                    result,
                    "status_contribution.stat_unsupported",
                    $"{modifierPath}.statType",
                    "TargetPriority contribution multipliers are not " +
                    "supported.");
            }

            if (!IsFinite(modifier.Multiplier) ||
                modifier.Multiplier < 0f)
            {
                AddError(
                    result,
                    "status_contribution.multiplier_invalid",
                    $"{modifierPath}.multiplier",
                    "Status contribution multiplier must be finite and " +
                    "non-negative.");
            }

            if (!IsFinite(modifier.DungeonStageProgressScale) ||
                modifier.DungeonStageProgressScale < 0f)
            {
                AddError(
                    result,
                    "status_contribution.dungeon_progress_scale_invalid",
                    $"{modifierPath}.dungeonStageProgressScale",
                    "Dungeon-stage progress contribution scale must be " +
                    "finite and non-negative.");
            }

            for (int previous = 0; previous < index; previous++)
            {
                CharacterStatusStatContributionMultiplier earlier =
                    modifiers[previous];
                if (earlier == null ||
                    earlier.StatType != modifier.StatType ||
                    !CharacterStatusSelection.IsSameStatus(
                        earlier.StatusEffect,
                        modifier.StatusEffect))
                {
                    continue;
                }

                AddError(
                    result,
                    "status_contribution.duplicate",
                    modifierPath,
                    "The same status and stat contribution is configured " +
                    "more than once.");
                break;
            }
        }
    }

    private static void ValidatePassiveStatModifiers(
        IReadOnlyList<CharacterPassiveStatModifierDefinition> modifiers,
        string path,
        CharacterDefinitionValidationResult result)
    {
        if (modifiers == null)
        {
            AddError(
                result,
                "passive_stat_modifier.list_null",
                path,
                "Passive stat modifier list is null.");
            return;
        }

        if (modifiers.Count == 0)
        {
            AddError(
                result,
                "passive_stat_modifier.list_empty",
                path,
                "A Stat Modifier section requires at least one modifier.");
            return;
        }

        for (int index = 0; index < modifiers.Count; index++)
        {
            string modifierPath = $"{path}[{index}]";
            CharacterPassiveStatModifierDefinition modifier =
                modifiers[index];
            if (modifier == null)
            {
                AddError(
                    result,
                    "passive_stat_modifier.null",
                    modifierPath,
                    "Passive stat modifier is null.");
                continue;
            }

            if (!Enum.IsDefined(
                    typeof(StatusEffectStatType),
                    modifier.StatType) ||
                !CharacterPassiveStatModifierRules
                    .IsSupportedCharacterStat(modifier.StatType))
            {
                AddError(
                    result,
                    "passive_stat_modifier.stat_unsupported",
                    $"{modifierPath}.statType",
                    $"Unsupported passive character stat " +
                    $"'{modifier.StatType}'.");
            }
            if (!Enum.IsDefined(
                    typeof(StatusEffectStatModifierMode),
                    modifier.Mode))
            {
                AddError(
                    result,
                    "passive_stat_modifier.mode_invalid",
                    $"{modifierPath}.mode",
                    $"Unsupported passive stat modifier mode " +
                    $"'{modifier.Mode}'.");
            }
            if (!IsFinite(modifier.BaseValue))
            {
                AddError(
                    result,
                    "passive_stat_modifier.base_invalid",
                    $"{modifierPath}.baseValue",
                    "Passive stat modifier base value must be finite.");
            }
            if (!IsFinite(modifier.DungeonStageProgressScale))
            {
                AddError(
                    result,
                    "passive_stat_modifier.progress_scale_invalid",
                    $"{modifierPath}.dungeonStageProgressScale",
                    "Passive stat modifier dungeon-stage scale must be " +
                    "finite.");
            }
        }
    }

    private static void ValidateResourceSpendEffect(
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        bool hasFixedPositiveAmount =
            effect.AmountMode == CharacterDamageAmountMode.Fixed &&
            IsFinite(effect.Amount) &&
            effect.Amount >= 1f;
        bool hasUnsupportedScaling =
            effect.SourceResourceScale != 0f ||
            effect.SourceCurrentHealthScale != 0f ||
            effect.SourceMaxHealthScale != 0f ||
            effect.TargetCurrentHealthScale != 0f ||
            effect.TargetMaxHealthScale != 0f ||
            effect.SourceStatusStacksScale != 0f ||
            effect.TargetStatusStacksScale != 0f;
        if (!hasFixedPositiveAmount || hasUnsupportedScaling)
        {
            AddError(
                result,
                "effect.resource_spend_invalid",
                $"{effectPath}.damageAmount",
                "Resource spend requires a fixed amount of at least one " +
                "and does not support scaling terms.");
        }
    }

    private static void ValidateHealthSpendEffect(
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        bool hasFixedPositiveAmount =
            effect.AmountMode == CharacterDamageAmountMode.Fixed &&
            IsFinite(effect.Amount) &&
            effect.Amount >= 1f;
        bool hasUnsupportedScaling =
            effect.SourceResourceScale != 0f ||
            effect.SourceCurrentHealthScale != 0f ||
            effect.SourceMaxHealthScale != 0f ||
            effect.TargetCurrentHealthScale != 0f ||
            effect.TargetMaxHealthScale != 0f ||
            effect.SourceStatusStacksScale != 0f ||
            effect.TargetStatusStacksScale != 0f;
        if (!hasFixedPositiveAmount || hasUnsupportedScaling)
        {
            AddError(
                result,
                "effect.health_spend_invalid",
                $"{effectPath}.damageAmount",
                "Health spend requires a fixed amount of at least one and " +
                "does not support scaling terms.");
        }
    }

    private static void ValidateCardDrawEffect(
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        bool hasFixedPositiveAmount =
            effect.AmountMode == CharacterDamageAmountMode.Fixed &&
            IsFinite(effect.Amount) &&
            effect.Amount >= 1f;
        bool hasUnsupportedScaling =
            effect.SourceResourceScale != 0f ||
            effect.SourceCurrentHealthScale != 0f ||
            effect.SourceMaxHealthScale != 0f ||
            effect.TargetCurrentHealthScale != 0f ||
            effect.TargetMaxHealthScale != 0f ||
            effect.SourceStatusStacksScale != 0f ||
            effect.TargetStatusStacksScale != 0f;
        if (!hasFixedPositiveAmount || hasUnsupportedScaling)
        {
            AddError(
                result,
                "effect.card_draw_invalid",
                $"{effectPath}.damageAmount",
                "Card draw requires a fixed amount of at least one and " +
                "does not support scaling terms.");
        }
    }

    private static CharacterTargetFaction? ValidateFreshEffectSelector(
        CharacterEffectTargetSelector selector,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        string selectorPath = $"{effectPath}.targetSelector";
        if (selector == null)
        {
            AddError(
                result,
                "effect.fresh_selector_required",
                selectorPath,
                "FreshSelection requires an explicit target selector.");
            return null;
        }

        bool validFaction = Enum.IsDefined(
            typeof(CharacterTargetFaction),
            selector.TargetFaction);
        if (!validFaction)
        {
            AddError(
                result,
                "effect.fresh_faction_invalid",
                $"{selectorPath}.targetFaction",
                $"Unsupported fresh target faction " +
                $"'{selector.TargetFaction}'.");
        }

        bool validSubject = Enum.IsDefined(
            typeof(CharacterAttackSubject),
            selector.Subject);
        if (!validSubject)
        {
            AddError(
                result,
                "effect.fresh_subject_invalid",
                $"{selectorPath}.subject",
                $"Unsupported fresh target selection mode " +
                $"'{selector.Subject}'.");
        }
        else if (selector.Subject == CharacterAttackSubject.None)
        {
            AddError(
                result,
                "effect.fresh_subject_none",
                $"{selectorPath}.subject",
                "FreshSelection cannot reuse an action target. Use " +
                "InheritAction or choose an explicit selection mode.");
        }
        else if (selector.Subject == CharacterAttackSubject.Manual)
        {
            AddError(
                result,
                "effect.fresh_subject_manual_unsupported",
                $"{selectorPath}.subject",
                "Manual target selection is currently supported only by " +
                "the action-level subject.");
        }
        else if (validFaction)
        {
            ValidateSubject(
                selector.TargetFaction,
                selector.Subject,
                selector.SubjectMetric,
                selectorPath,
                result);
        }

        if (selector.SubjectCount < 1)
        {
            AddError(
                result,
                "effect.fresh_subject_count_invalid",
                $"{selectorPath}.subjectCount",
                "Fresh target count must be at least one.");
        }

        bool validMatchMode = Enum.IsDefined(
            typeof(CharacterConditionMatchMode),
            selector.ConditionMatchMode);
        if (!validMatchMode)
        {
            AddError(
                result,
                "effect.fresh_condition_match_invalid",
                $"{selectorPath}.conditionMatchMode",
                $"Unsupported fresh condition match mode " +
                $"'{selector.ConditionMatchMode}'.");
        }

        if (validFaction)
        {
            ValidateConditions(
                selector.HasNumericConditions,
                selector.ConditionMatchMode,
                selector.NumericConditions,
                selector.TargetFaction,
                selectorPath,
                result);
            ValidateBattleArea(
                true,
                selector.SubjectCount,
                selector.AreaDefinition,
                selector.AreaOffsets,
                selectorPath,
                result);
        }

        return validFaction
            ? selector.TargetFaction
            : null;
    }

    private static void ValidateDamageEffect(
        CharacterTargetFaction? targetFaction,
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        bool validDamageType =
            effect.DamageType == CharacterAttackDamageType.Physical ||
            effect.DamageType == CharacterAttackDamageType.Magical ||
            effect.DamageType == CharacterAttackDamageType.Fixed;
        if (!validDamageType)
        {
            AddError(
                result,
                "effect.damage_type_invalid",
                $"{effectPath}.damageType",
                $"Damage effect does not support damage type " +
                $"'{effect.DamageType}'.");
        }

        if (targetFaction == CharacterTargetFaction.Ally)
        {
            AddError(
                result,
                "effect.ally_damage_unsupported",
                $"{effectPath}.type",
                "The runtime has no ally receiver for direct damage.");
        }
        else if (!targetFaction.HasValue)
        {
            AddWarning(
                result,
                "effect.inherited_faction_dependent",
                $"{effectPath}.type",
                "Subject.None can execute direct damage only when its " +
                "inherited targets are enemies.");
        }

        ValidateAmountScaling(
            targetFaction,
            effect,
            effectPath,
            "effect.damage_invalid",
            "Damage",
            true,
            result);
    }

    private static void ValidateAmountScaling(
        CharacterTargetFaction? targetFaction,
        CharacterEffectDefinition effect,
        string effectPath,
        string invalidCode,
        string valueName,
        bool supportsTargetScaling,
        CharacterDefinitionValidationResult result)
    {
        bool validScalingMode = Enum.IsDefined(
            typeof(CharacterDamageAmountMode),
            effect.AmountMode);
        if (!validScalingMode)
        {
            AddError(
                result,
                "effect.damage_scaling_mode_invalid",
                $"{effectPath}.damageAmountMode",
                $"Unsupported value scaling mode " +
                $"'{effect.AmountMode}'.");
        }

        bool validTerms =
            IsFinite(effect.Amount) &&
            effect.Amount >= 0f &&
            IsFinite(effect.SourceResourceScale) &&
            effect.SourceResourceScale >= 0f &&
            IsFinite(effect.TargetCurrentHealthScale) &&
            IsFinite(effect.TargetMaxHealthScale) &&
            IsFinite(effect.SourceStatusStacksScale) &&
            IsFinite(effect.TargetStatusStacksScale);
        if (!validScalingMode || !validTerms ||
            !effect.AmountScaling.IsFinite ||
            !effect.AmountScaling.HasPositiveTerm)
        {
            AddError(
                result,
                invalidCode,
                $"{effectPath}.damageAmount",
                $"{valueName} scaling must contain at least one finite, " +
                "positive term. Base, attack power, and current resource " +
                "terms must remain non-negative.");
        }

        ValidateStatusScalingReference(
            effect.SourceStatusScalingEffect,
            effect.SourceStatusStacksScale,
            CharacterTargetFaction.Ally,
            $"{effectPath}.sourceStatusScalingEffect",
            "effect.source_status_scaling_status_required",
            "effect.source_status_scaling_faction_mismatch",
            "Source",
            result);

        if (IsFinite(effect.TargetStatusStacksScale) &&
            effect.TargetStatusStacksScale != 0f)
        {
            StatusEffectSO targetStatus =
                effect.TargetStatusScalingEffect;
            if (targetStatus == null)
            {
                AddError(
                    result,
                    "effect.target_status_scaling_status_required",
                    $"{effectPath}.targetStatusScalingEffect",
                    "Target status stack scaling requires an explicit " +
                    "StatusEffectSO.");
            }
            else if (targetFaction.HasValue &&
                     !CanTargetFaction(
                         targetStatus,
                         targetFaction.Value))
            {
                AddError(
                    result,
                    "effect.target_status_scaling_faction_mismatch",
                    $"{effectPath}.targetStatusScalingEffect",
                    $"Status '{targetStatus.name}' cannot exist on " +
                    $"{targetFaction} targets.");
            }
        }

        if (!supportsTargetScaling &&
            effect.AmountScaling.HasTargetDependentTerm)
        {
            AddError(
                result,
                "effect.target_scaling_unsupported",
                effectPath,
                $"{valueName} executes once per effect, so target health " +
                "and target status scaling are not supported.");
        }

    }

    private static void ValidateStatusScalingReference(
        StatusEffectSO statusEffect,
        float scale,
        CharacterTargetFaction requiredFaction,
        string path,
        string requiredCode,
        string factionCode,
        string valueName,
        CharacterDefinitionValidationResult result)
    {
        if (!IsFinite(scale) || scale == 0f)
            return;

        if (statusEffect == null)
        {
            AddError(
                result,
                requiredCode,
                path,
                $"{valueName} status stack scaling requires an explicit " +
                "StatusEffectSO.");
            return;
        }

        if (!CanTargetFaction(statusEffect, requiredFaction))
        {
            AddError(
                result,
                factionCode,
                path,
                $"Status '{statusEffect.name}' cannot exist on " +
                $"{requiredFaction} targets.");
        }
    }

    private static void ValidateApplyStatusEffect(
        CharacterTargetFaction? targetFaction,
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        StatusEffectSO status = effect.StatusEffect;
        if (status == null)
        {
            AddError(
                result,
                "effect.status_required",
                $"{effectPath}.statusEffect",
                "ApplyStatus effect requires an explicit StatusEffectSO.");
        }
        else if (targetFaction.HasValue &&
                 !CanTargetFaction(status, targetFaction.Value))
        {
            AddError(
                result,
                "effect.status_faction_mismatch",
                $"{effectPath}.statusEffect",
                $"Status '{status.name}' cannot target {targetFaction}.");
        }

        if (!IsFinite(effect.StatusStacks) || effect.StatusStacks <= 0f)
        {
            AddError(
                result,
                "effect.status_stacks_invalid",
                $"{effectPath}.statusStacks",
                "Applied status stacks must be a finite value greater " +
                "than zero.");
        }
        if (status != null &&
            status.DurationMode == StatusEffectDurationMode.Timed &&
            (!IsFinite(effect.StatusDuration) ||
             effect.StatusDuration <= 0f))
        {
            AddError(
                result,
                "effect.status_duration_invalid",
                $"{effectPath}.statusDuration",
                "Timed status duration must be a finite value greater " +
                "than zero.");
        }
    }

    private static void ValidateRemoveStatusEffect(
        CharacterTargetFaction? targetFaction,
        CharacterEffectDefinition effect,
        string effectPath,
        CharacterDefinitionValidationResult result)
    {
        ValidateStatusRemovalPick(
            effect.StatusRemovalPickMode,
            effect.StatusRemovalPickCount,
            effectPath,
            "effect",
            result);
        ValidateStatusRemovalAmount(
            effect.StatusRemovalAmountMode,
            effect.StatusRemovalCount,
            effect.StatusRemovalRatio,
            effectPath,
            "effect",
            result);

        if (!Enum.IsDefined(
                typeof(CharacterStatusRemovalTarget),
                effect.StatusRemovalTarget))
        {
            AddError(
                result,
                "effect.removal_target_invalid",
                $"{effectPath}.statusRemovalTarget",
                $"Unsupported status removal target " +
                $"'{effect.StatusRemovalTarget}'.");
            return;
        }

        if (effect.StatusRemovalTarget !=
            CharacterStatusRemovalTarget.Single)
        {
            return;
        }

        CharacterStatusRemovalSelection selection =
            effect.StatusRemovalSelection;
        if (!selection.HasExplicitStatus)
        {
            AddError(
                result,
                "effect.removal_status_required",
                $"{effectPath}.statusRemovalEffects",
                "Explicit RemoveStatus effect requires at least one " +
                "StatusEffectSO.");
            return;
        }

        HashSet<StatusEffectSO> visited = new();
        for (int index = 0;
             index < selection.ExplicitStatusCount;
             index++)
        {
            StatusEffectSO status = selection.GetExplicitStatus(index);
            string statusPath =
                $"{effectPath}.statusRemovalEffects[{index}]";
            if (status == null)
            {
                AddError(
                    result,
                    "effect.removal_status_null",
                    statusPath,
                    "Status removal selection cannot contain null.");
            }
            else if (!visited.Add(status))
            {
                AddError(
                    result,
                    "effect.removal_status_duplicate",
                    statusPath,
                    $"Status '{status.name}' is selected more than once.");
            }
            else if (!status.Removable)
            {
                AddError(
                    result,
                    "effect.removal_status_not_removable",
                    statusPath,
                    $"Status '{status.name}' is not removable.");
            }
            else if (targetFaction.HasValue &&
                     !CanTargetFaction(status, targetFaction.Value))
            {
                AddError(
                    result,
                    "effect.removal_status_faction_mismatch",
                    statusPath,
                    $"Status '{status.name}' cannot exist on " +
                    $"{targetFaction} targets.");
            }
        }

        if (selection.UsesRandomCount &&
            selection.PickCount > visited.Count)
        {
            AddWarning(
                result,
                "effect.removal_pick_count_exceeds_explicit",
                $"{effectPath}.statusRemovalPickCount",
                "The requested status type count exceeds the number of " +
                "unique explicit statuses. Runtime will remove every " +
                "available explicit status.");
        }
    }

    private static void ValidateStatusRemovalPick(
        CharacterStatusRemovalPickMode mode,
        int count,
        string path,
        string codePrefix,
        CharacterDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(
                typeof(CharacterStatusRemovalPickMode),
                mode))
        {
            AddError(
                result,
                $"{codePrefix}.removal_pick_mode_invalid",
                $"{path}.statusRemovalPickMode",
                $"Unsupported status removal pick mode '{mode}'.");
            return;
        }

        if (count < 1)
        {
            AddError(
                result,
                $"{codePrefix}.removal_pick_count_invalid",
                $"{path}.statusRemovalPickCount",
                "Status removal type count must be at least one.");
        }
    }

    private static void ValidateStatusRemovalAmount(
        CharacterStatusRemovalAmountMode mode,
        int count,
        float ratio,
        string path,
        string codePrefix,
        CharacterDefinitionValidationResult result)
    {
        if (!Enum.IsDefined(
                typeof(CharacterStatusRemovalAmountMode),
                mode))
        {
            AddError(
                result,
                $"{codePrefix}.removal_amount_mode_invalid",
                $"{path}.statusRemovalAmountMode",
                $"Unsupported status removal amount mode '{mode}'.");
            return;
        }

        if (mode == CharacterStatusRemovalAmountMode.FixedStacks)
        {
            if (count < 0)
            {
                AddError(
                    result,
                    $"{codePrefix}.removal_count_invalid",
                    $"{path}.statusRemovalCount",
                    "Status removal count cannot be negative.");
            }
            return;
        }

        if (!IsFinite(ratio) || ratio <= 0f || ratio > 1f)
        {
            AddError(
                result,
                $"{codePrefix}.removal_ratio_invalid",
                $"{path}.statusRemovalRatio",
                "Status removal ratio must be finite and greater than zero " +
                "and no greater than one.");
        }
    }

    private static void ValidateBattleArea(
        bool requiresActionTargetSelection,
        int targetCount,
        BattleAreaDefinition areaDefinition,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets,
        string actionPath,
        CharacterDefinitionValidationResult result)
    {
        if (areaDefinition == null)
        {
            AddError(
                result,
                "ability.area_definition_null",
                $"{actionPath}.areaDefinition",
                "Battle abilities require an area definition.");
            return;
        }

        if (!areaDefinition.IsValid)
        {
            AddError(
                result,
                "ability.area_definition_invalid",
                $"{actionPath}.areaDefinition",
                "Area type, origin, radius, angle, or cast distance is " +
                "invalid.");
            return;
        }

        if (areaOffsets != null && areaOffsets.Count > 0)
        {
            AddError(
                result,
                "ability.tile_area_unsupported",
                $"{actionPath}.areaOffsets",
                "Tile-offset attack areas are no longer supported.");
        }

        if (!areaDefinition.UsesWorldArea)
        {
            if (requiresActionTargetSelection && targetCount < 1)
            {
                AddError(
                    result,
                    "ability.target_count_invalid",
                    $"{actionPath}.subjectCount",
                    "Target areas require at least one target.");
            }
            return;
        }

        if (targetCount < 0)
        {
            AddError(
                result,
                "ability.area_target_count_invalid",
                $"{actionPath}.subjectCount",
                "Circular area target count must be zero or greater.");
        }
    }

    private static void ValidateSections<T>(
        IReadOnlyList<T> sections,
        string path,
        CharacterDefinitionValidationResult result)
        where T : struct, Enum
    {
        if (sections == null)
        {
            AddError(
                result,
                "section.list_null",
                path,
                "Section list is null.");
            return;
        }

        HashSet<T> seen = new();
        for (int index = 0; index < sections.Count; index++)
        {
            T section = sections[index];
            if (!Enum.IsDefined(typeof(T), section))
            {
                AddError(
                    result,
                    "section.type_unknown",
                    $"{path}[{index}]",
                    $"Unsupported section value '{section}'.");
                continue;
            }

            if (!seen.Add(section))
            {
                AddWarning(
                    result,
                    "section.duplicate",
                    $"{path}[{index}]",
                    $"Section '{section}' is duplicated.");
            }
        }
    }

    private static bool IsSubjectMetricSupported(
        CharacterAttackSubjectMetric metric,
        CharacterTargetFaction faction)
    {
        return faction == CharacterTargetFaction.Ally
            ? metric == CharacterAttackSubjectMetric.AttackPower ||
              metric == CharacterAttackSubjectMetric.AttackSpeed ||
              metric == CharacterAttackSubjectMetric.Health ||
              metric == CharacterAttackSubjectMetric.Shield
            : metric == CharacterAttackSubjectMetric.Health ||
              metric == CharacterAttackSubjectMetric.StackCount ||
              metric == CharacterAttackSubjectMetric.Shield;
    }

    private static bool IsConditionMetricSupported(
        CharacterNumericConditionMetric metric,
        CharacterTargetFaction faction)
    {
        return faction == CharacterTargetFaction.Ally
            ? metric == CharacterNumericConditionMetric.AttackPower ||
              metric == CharacterNumericConditionMetric.AttackSpeed ||
              metric == CharacterNumericConditionMetric.Health ||
              metric == CharacterNumericConditionMetric.HealthPercentage ||
              metric == CharacterNumericConditionMetric.MaximumHealth ||
              metric == CharacterNumericConditionMetric
                  .HealthPerformancePercentage ||
              metric == CharacterNumericConditionMetric
                  .HealthPerformanceCap ||
              metric == CharacterNumericConditionMetric.Shield ||
              metric ==
              CharacterNumericConditionMetric.StatusStackCount
            : metric == CharacterNumericConditionMetric.Health ||
              metric == CharacterNumericConditionMetric.HealthPercentage ||
              metric == CharacterNumericConditionMetric.MaximumHealth ||
              metric == CharacterNumericConditionMetric.StackCount ||
              metric == CharacterNumericConditionMetric.Shield ||
              metric ==
              CharacterNumericConditionMetric.StatusStackCount;
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

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void AddError(
        CharacterDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            CharacterDefinitionDiagnosticSeverity.Error,
            code,
            path,
            message);
    }

    private static void AddWarning(
        CharacterDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            CharacterDefinitionDiagnosticSeverity.Warning,
            code,
            path,
            message);
    }
}

public readonly struct StatusEffectDefinitionDiagnostic
{
    public CharacterDefinitionDiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Path { get; }
    public string Message { get; }

    public StatusEffectDefinitionDiagnostic(
        CharacterDefinitionDiagnosticSeverity severity,
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
        string location = string.IsNullOrWhiteSpace(Path) ? "<root>" : Path;
        return $"{Severity} [{Code}] {location}: {Message}";
    }
}

public sealed class StatusEffectDefinitionValidationResult
{
    private readonly List<StatusEffectDefinitionDiagnostic> _diagnostics =
        new();

    public IReadOnlyList<StatusEffectDefinitionDiagnostic> Diagnostics =>
        _diagnostics;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool IsValid => ErrorCount == 0;

    internal void Add(
        CharacterDefinitionDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        _diagnostics.Add(new StatusEffectDefinitionDiagnostic(
            severity,
            code,
            path,
            message));
        if (severity == CharacterDefinitionDiagnosticSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    internal void Add(
        StatusEffectDefinitionDiagnostic diagnostic,
        string pathPrefix = null)
    {
        string path = string.IsNullOrEmpty(pathPrefix)
            ? diagnostic.Path
            : string.IsNullOrEmpty(diagnostic.Path)
                ? pathPrefix
                : $"{pathPrefix}.{diagnostic.Path}";
        Add(
            diagnostic.Severity,
            diagnostic.Code,
            path,
            diagnostic.Message);
    }
}

public static class StatusEffectDefinitionValidator
{
    private const string RootPath = "statusEffect";

    public static StatusEffectDefinitionValidationResult Validate(
        StatusEffectSO definition)
    {
        return Validate(definition, null);
    }

    public static StatusEffectDefinitionValidationResult Validate(
        StatusEffectSO definition,
        IReadOnlyList<StatusEffectSO> catalog)
    {
        StatusEffectDefinitionValidationResult result = new();
        ValidateDefinition(definition, result);
        if (definition != null && catalog != null)
            ValidateDuplicateId(definition, catalog, result);
        return result;
    }

    public static StatusEffectDefinitionValidationResult ValidateAll(
        IReadOnlyList<StatusEffectSO> definitions)
    {
        StatusEffectDefinitionValidationResult result = new();
        if (definitions == null)
        {
            AddError(
                result,
                "status.catalog_null",
                "statusEffects",
                "Status effect definition catalog is null.");
            return result;
        }

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Count; index++)
        {
            StatusEffectSO definition = definitions[index];
            StatusEffectDefinitionValidationResult definitionResult =
                Validate(definition);
            foreach (StatusEffectDefinitionDiagnostic diagnostic in
                     definitionResult.Diagnostics)
            {
                result.Add(diagnostic, $"statusEffects[{index}]");
            }

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.StatusId))
            {
                continue;
            }

            if (!seenIds.Add(definition.StatusId))
            {
                AddError(
                    result,
                    "status.id_duplicate",
                    $"statusEffects[{index}].statusId",
                    $"StatusId '{definition.StatusId}' is duplicated.");
            }
        }

        return result;
    }

    private static void ValidateDefinition(
        StatusEffectSO definition,
        StatusEffectDefinitionValidationResult result)
    {
        if (definition == null)
        {
            AddError(
                result,
                "status.null",
                RootPath,
                "Status effect definition is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.StatusId))
        {
            AddError(
                result,
                "status.id_missing",
                "statusId",
                "StatusId is required.");
        }

        ValidateLocalizationKey(
            definition.NameLocalizationKey,
            "nameLocalizationKey",
            result);
        ValidateLocalizationKey(
            definition.DescriptionLocalizationKey,
            "descriptionLocalizationKey",
            result);

        if (!definition.CanTargetEnemy && !definition.CanTargetAlly)
        {
            AddError(
                result,
                "status.target_missing",
                "targeting",
                "At least one target faction must be enabled.");
        }

        if (definition.DurationMode == StatusEffectDurationMode.Timed &&
            (!IsFinite(definition.ConfiguredDefaultDuration) ||
             definition.ConfiguredDefaultDuration <= 0f))
        {
            AddError(
                result,
                "status.duration_invalid",
                "defaultDuration",
                "Timed status duration must be a finite value greater " +
                "than zero.");
        }

        if (definition.MaximumStacks < 0)
        {
            AddError(
                result,
                "status.maximum_stacks_invalid",
                "maximumStacks",
                "Maximum stacks cannot be negative.");
        }
        if (definition.DefaultAppliedStacks < 1)
        {
            AddError(
                result,
                "status.default_stacks_invalid",
                "defaultAppliedStacks",
                "Default applied stacks must be at least one.");
        }
        else if (definition.MaximumStacks > 0 &&
                 definition.DefaultAppliedStacks >
                 definition.MaximumStacks)
        {
            AddError(
                result,
                "status.default_stacks_exceed_maximum",
                "defaultAppliedStacks",
                "Default applied stacks cannot exceed maximum stacks.");
        }

        if (!definition.Removable &&
            (definition.ConfiguredIncludedInRandomRemoval ||
             definition.ConfiguredIncludedInAllRemoval))
        {
            AddError(
                result,
                "status.non_removable_in_removal_pool",
                "removal",
                "A non-removable status cannot participate in random or " +
                "all-status removal pools.");
        }

        ValidateOperations(definition, result);
        ValidateTriggerBlocks(definition, result);
        ValidatePersistentModules(definition, result);
    }

    private static void ValidateLocalizationKey(
        string localizationKey,
        string path,
        StatusEffectDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            AddError(
                result,
                "status.localization_key_missing",
                path,
                "A localization key is required.");
            return;
        }

        if (!GeneratedLocalizationTables.ReferenceEntries.ContainsKey(
                localizationKey))
        {
            AddError(
                result,
                "status.localization_key_unknown",
                path,
                $"Localization key '{localizationKey}' does not exist.");
        }
    }

    private static void ValidateDuplicateId(
        StatusEffectSO definition,
        IReadOnlyList<StatusEffectSO> catalog,
        StatusEffectDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(definition.StatusId))
            return;

        foreach (StatusEffectSO other in catalog)
        {
            if (other == null || ReferenceEquals(other, definition))
                continue;
            if (!string.Equals(
                    definition.StatusId,
                    other.StatusId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            AddError(
                result,
                "status.id_duplicate",
                "statusId",
                $"StatusId is also used by '{other.name}'.");
            return;
        }
    }

    private static void ValidateOperations(
        StatusEffectSO definition,
        StatusEffectDefinitionValidationResult result)
    {
        IReadOnlyList<StatusEffectOperationDefinition> operations =
            definition.Operations;
        if (operations == null)
        {
            AddError(
                result,
                "status.operation_list_null",
                "operations",
                "Status operation list is null.");
            return;
        }

        bool usesTick = false;
        for (int index = 0; index < operations.Count; index++)
        {
            StatusEffectOperationDefinition operation = operations[index];
            string path = $"operations[{index}]";
            if (operation == null)
            {
                AddError(
                    result,
                    "status.operation_null",
                    path,
                    "Status operation is null.");
                continue;
            }

            bool validTrigger = Enum.IsDefined(
                typeof(StatusEffectOperationTrigger),
                operation.Trigger);
            bool validType = Enum.IsDefined(
                typeof(StatusEffectOperationType),
                operation.OperationType);
            bool validValueMode = Enum.IsDefined(
                typeof(StatusEffectValueMode),
                operation.ValueMode);

            if (!validTrigger)
            {
                AddError(
                    result,
                    "status.operation_trigger_unknown",
                    $"{path}.trigger",
                    $"Unsupported operation trigger '{operation.Trigger}'.");
            }
            else
            {
                usesTick |= operation.Trigger ==
                            StatusEffectOperationTrigger.OnTick;
                if (definition.DurationMode ==
                        StatusEffectDurationMode.Permanent &&
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnExpire)
                {
                    AddWarning(
                        result,
                        "status.operation_permanent_expire_unreachable",
                        $"{path}.trigger",
                        "OnExpire cannot be reached by a permanent status.");
                }
            }

            if (!validType)
            {
                AddError(
                    result,
                    "status.operation_type_unknown",
                    $"{path}.operationType",
                    $"Unsupported operation type " +
                    $"'{operation.OperationType}'.");
            }
            if (!validValueMode)
            {
                AddError(
                    result,
                    "status.operation_value_mode_unknown",
                    $"{path}.valueMode",
                    $"Unsupported value mode '{operation.ValueMode}'.");
            }
            if (!IsFinite(operation.Value))
            {
                AddError(
                    result,
                    "status.operation_value_invalid",
                    $"{path}.value",
                    "Operation value must be finite.");
            }

            if (validTrigger && validType)
            {
                ValidateSupportedOperation(
                    definition,
                    operation,
                    path,
                    result);
            }
        }

        if (usesTick &&
            (!IsFinite(definition.ConfiguredTickInterval) ||
             definition.ConfiguredTickInterval <= 0f))
        {
            AddError(
                result,
                "status.tick_interval_invalid",
                "tickInterval",
                "A status using OnTick must have a finite tick interval " +
                "greater than zero.");
        }
    }

    private static void ValidateSupportedOperation(
        StatusEffectSO definition,
        StatusEffectOperationDefinition operation,
        string path,
        StatusEffectDefinitionValidationResult result)
    {
        switch (operation.OperationType)
        {
            case StatusEffectOperationType.PeriodicDamage:
                ValidateDamageValue(operation, path, result);
                if (operation.Trigger != StatusEffectOperationTrigger.OnTick)
                {
                    AddError(
                        result,
                        "status.periodic_damage_trigger_invalid",
                        $"{path}.trigger",
                        "PeriodicDamage currently supports only OnTick.");
                }
                if (!definition.CanTargetEnemy ||
                    definition.CanTargetAlly)
                {
                    AddError(
                        result,
                        "status.periodic_damage_target_unsupported",
                        $"{path}.operationType",
                        "PeriodicDamage currently supports enemy-only " +
                        "statuses.");
                }
                break;

            case StatusEffectOperationType.InstantDamage:
                ValidateDamageValue(operation, path, result);
                bool supportsInstantTrigger =
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnApply ||
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnExpire ||
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnRemove ||
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnStackChanged;
                if (!supportsInstantTrigger)
                {
                    AddError(
                        result,
                        "status.instant_damage_trigger_invalid",
                        $"{path}.trigger",
                        "InstantDamage supports OnApply, OnExpire, " +
                        "OnRemove, or OnStackChanged. OnTick is reserved " +
                        "for PeriodicDamage.");
                }
                if (!definition.CanTargetEnemy ||
                    definition.CanTargetAlly)
                {
                    AddError(
                        result,
                        "status.instant_damage_target_unsupported",
                        $"{path}.operationType",
                        "InstantDamage supports enemy-only statuses.");
                }
                break;

            case StatusEffectOperationType.AttackPowerModifier:
            case StatusEffectOperationType.AttackSpeedModifier:
                if (operation.Trigger !=
                    StatusEffectOperationTrigger.OnApply)
                {
                    AddError(
                        result,
                        "status.modifier_trigger_invalid",
                        $"{path}.trigger",
                        $"{operation.OperationType} supports only OnApply.");
                }
                if (!definition.CanTargetAlly ||
                    definition.CanTargetEnemy)
                {
                    AddError(
                        result,
                        "status.modifier_target_unsupported",
                        $"{path}.operationType",
                        $"{operation.OperationType} supports ally-only " +
                        "statuses.");
                }
                break;

            case StatusEffectOperationType.DisableAction:
                if (operation.Trigger !=
                    StatusEffectOperationTrigger.OnApply)
                {
                    AddError(
                        result,
                        "status.disable_action_trigger_invalid",
                        $"{path}.trigger",
                        "DisableAction supports only OnApply.");
                }
                if (!definition.CanTargetAlly ||
                    definition.CanTargetEnemy)
                {
                    AddError(
                        result,
                        "status.disable_action_target_unsupported",
                        $"{path}.operationType",
                        "DisableAction supports ally-only statuses.");
                }
                break;
        }
    }

    private static void ValidateTriggerBlocks(
        StatusEffectSO definition,
        StatusEffectDefinitionValidationResult result)
    {
        IReadOnlyList<StatusEffectTriggerBlockDefinition> blocks =
            definition.TriggerBlocks;
        if (blocks == null)
        {
            AddError(
                result,
                "status.trigger_block_list_null",
                "triggerBlocks",
                "Status trigger block list is null.");
            return;
        }

        bool usesTick = false;
        for (int blockIndex = 0;
             blockIndex < blocks.Count;
             blockIndex++)
        {
            StatusEffectTriggerBlockDefinition block =
                blocks[blockIndex];
            string blockPath = $"triggerBlocks[{blockIndex}]";
            if (block == null)
            {
                AddError(
                    result,
                    "status.trigger_block_null",
                    blockPath,
                    "Status trigger block is null.");
                continue;
            }

            if (!Enum.IsDefined(
                    typeof(StatusEffectLifecycleTrigger),
                    block.Trigger))
            {
                AddError(
                    result,
                    "status.trigger_block_trigger_unknown",
                    $"{blockPath}.trigger",
                    $"Unsupported lifecycle trigger '{block.Trigger}'.");
            }
            else
            {
                usesTick |= block.Trigger ==
                            StatusEffectLifecycleTrigger.OnTick;
                if (definition.DurationMode ==
                        StatusEffectDurationMode.Permanent &&
                    block.Trigger ==
                        StatusEffectLifecycleTrigger.OnExpire)
                {
                    AddWarning(
                        result,
                        "status.trigger_block_permanent_expire_unreachable",
                        $"{blockPath}.trigger",
                        "OnExpire cannot be reached by a permanent status.");
                }
            }

            IReadOnlyList<CharacterEffectDefinition> effects =
                block.Effects;
            if (effects == null)
            {
                AddError(
                    result,
                    "status.trigger_block_effect_list_null",
                    $"{blockPath}.effects",
                    "Trigger block effect list is null.");
                continue;
            }
            if (effects.Count == 0)
            {
                AddWarning(
                    result,
                    "status.trigger_block_empty",
                    $"{blockPath}.effects",
                    "Trigger block has no effects.");
                continue;
            }

            for (int effectIndex = 0;
                 effectIndex < effects.Count;
                 effectIndex++)
            {
                ValidateTriggerBlockEffect(
                    effects[effectIndex],
                    $"{blockPath}.effects[{effectIndex}]",
                    result);
            }
        }

        if (usesTick &&
            (!IsFinite(definition.ConfiguredTickInterval) ||
             definition.ConfiguredTickInterval <= 0f))
        {
            AddError(
                result,
                "status.trigger_block_tick_interval_invalid",
                "tickInterval",
                "A status using an OnTick trigger block must have a " +
                "finite tick interval greater than zero.");
        }
    }

    private static void ValidateTriggerBlockEffect(
        CharacterEffectDefinition effect,
        string path,
        StatusEffectDefinitionValidationResult result)
    {
        if (effect == null)
        {
            AddError(
                result,
                "status.trigger_block_effect_null",
                path,
                "Trigger block effect is null.");
            return;
        }

        if (!BattleEffectRules.TryValidate(
                effect,
                out string sharedEffectError))
        {
            AddError(
                result,
                "status.trigger_block_effect_shared_invalid",
                path,
                sharedEffectError);
        }

        if (!Enum.IsDefined(typeof(CharacterEffectType), effect.Type))
        {
            AddError(
                result,
                "status.trigger_block_effect_type_unknown",
                $"{path}.type",
                $"Unsupported effect type '{effect.Type}'.");
        }
        if (!Enum.IsDefined(
                typeof(CharacterEffectTargetMode),
                effect.TargetMode))
        {
            AddError(
                result,
                "status.trigger_block_target_mode_unknown",
                $"{path}.targetMode",
                $"Unsupported target mode '{effect.TargetMode}'.");
        }
        if (!effect.AmountScaling.IsFinite)
        {
            AddError(
                result,
                "status.trigger_block_scaling_invalid",
                $"{path}.amountScaling",
                "Trigger block effect scaling must be finite.");
        }
        if (effect.Type == CharacterEffectType.RemoveStatus)
        {
            if (!Enum.IsDefined(
                    typeof(CharacterStatusRemovalAmountMode),
                    effect.StatusRemovalAmountMode))
            {
                AddError(
                    result,
                    "status.trigger_block_removal_amount_mode_invalid",
                    $"{path}.statusRemovalAmountMode",
                    "Unsupported status removal amount mode.");
            }
            else if (effect.StatusRemovalAmountMode ==
                     CharacterStatusRemovalAmountMode.FixedStacks)
            {
                if (effect.StatusRemovalCount < 0)
                {
                    AddError(
                        result,
                        "status.trigger_block_removal_count_invalid",
                        $"{path}.statusRemovalCount",
                        "Status removal count cannot be negative.");
                }
            }
            else if (!IsFinite(effect.StatusRemovalRatio) ||
                     effect.StatusRemovalRatio <= 0f ||
                     effect.StatusRemovalRatio > 1f)
            {
                AddError(
                    result,
                    "status.trigger_block_removal_ratio_invalid",
                    $"{path}.statusRemovalRatio",
                    "Status removal ratio must be greater than zero and no " +
                    "greater than one.");
            }
        }
    }

    private static void ValidatePersistentModules(
        StatusEffectSO definition,
        StatusEffectDefinitionValidationResult result)
    {
        IReadOnlyList<StatusEffectStatModifierDefinition> modifiers =
            definition.StatModifiers;
        if (modifiers == null)
        {
            AddError(
                result,
                "status.stat_modifier_list_null",
                "statModifiers",
                "Status stat modifier list is null.");
        }
        else
        {
            for (int index = 0; index < modifiers.Count; index++)
            {
                ValidateStatModifier(
                    definition,
                    modifiers[index],
                    $"statModifiers[{index}]",
                    result);
            }
        }

        IReadOnlyList<StatusEffectControlDefinition> controls =
            definition.ControlEffects;
        if (controls == null)
        {
            AddError(
                result,
                "status.control_effect_list_null",
                "controlEffects",
                "Status control effect list is null.");
            return;
        }

        HashSet<StatusEffectControlType> seenControls = new();
        for (int index = 0; index < controls.Count; index++)
        {
            StatusEffectControlDefinition control = controls[index];
            string path = $"controlEffects[{index}]";
            if (control == null)
            {
                AddError(
                    result,
                    "status.control_effect_null",
                    path,
                    "Status control effect is null.");
                continue;
            }
            if (!Enum.IsDefined(
                    typeof(StatusEffectControlType),
                    control.ControlType))
            {
                AddError(
                    result,
                    "status.control_effect_type_unknown",
                    $"{path}.controlType",
                    $"Unsupported control type '{control.ControlType}'.");
                continue;
            }
            if (!seenControls.Add(control.ControlType))
            {
                AddWarning(
                    result,
                    "status.control_effect_duplicate",
                    $"{path}.controlType",
                    $"Control type '{control.ControlType}' is duplicated.");
            }
            if (definition.CanTargetEnemy &&
                control.ControlType !=
                    StatusEffectControlType.DisableAllActions &&
                control.ControlType !=
                    StatusEffectControlType.ForceTargeting)
            {
                AddWarning(
                    result,
                    "status.control_effect_enemy_unsupported",
                    $"{path}.controlType",
                    $"{control.ControlType} currently affects allied " +
                    "characters only.");
            }
        }
    }

    private static void ValidateStatModifier(
        StatusEffectSO definition,
        StatusEffectStatModifierDefinition modifier,
        string path,
        StatusEffectDefinitionValidationResult result)
    {
        if (modifier == null)
        {
            AddError(
                result,
                "status.stat_modifier_null",
                path,
                "Status stat modifier is null.");
            return;
        }
        if (!Enum.IsDefined(
                typeof(StatusEffectStatType),
                modifier.StatType))
        {
            AddError(
                result,
                "status.stat_modifier_stat_unknown",
                $"{path}.statType",
                $"Unsupported stat type '{modifier.StatType}'.");
        }
        if (!Enum.IsDefined(
                typeof(StatusEffectStatModifierMode),
                modifier.Mode))
        {
            AddError(
                result,
                "status.stat_modifier_mode_unknown",
                $"{path}.mode",
                $"Unsupported modifier mode '{modifier.Mode}'.");
        }
        if (!IsFinite(modifier.Value))
        {
            AddError(
                result,
                "status.stat_modifier_value_invalid",
                $"{path}.value",
                "Status stat modifier value must be finite.");
        }
        else if (modifier.Mode ==
                     StatusEffectStatModifierMode.MultiplicativeRatio &&
                 modifier.Value < -1f)
        {
            AddError(
                result,
                "status.stat_modifier_multiplier_below_zero",
                $"{path}.value",
                "Multiplicative ratio cannot be less than -1.");
        }
        if (modifier.StatType == StatusEffectStatType.TargetPriority &&
            modifier.Mode != StatusEffectStatModifierMode.Flat)
        {
            AddError(
                result,
                "status.target_priority_mode_invalid",
                $"{path}.mode",
                "Target priority only supports flat adjustments.");
        }
        bool supportsEnemy =
            modifier.StatType == StatusEffectStatType.IncomingDamage ||
            modifier.StatType == StatusEffectStatType.TargetPriority;
        if (!definition.CanTargetAlly && !supportsEnemy)
        {
            AddWarning(
                result,
                "status.stat_modifier_ally_required",
                path,
                "Attack stat modifiers currently affect allied " +
                "characters only.");
        }
    }

    private static void ValidateDamageValue(
        StatusEffectOperationDefinition operation,
        string path,
        StatusEffectDefinitionValidationResult result)
    {
        if (IsFinite(operation.Value) && operation.Value <= 0f)
        {
            AddError(
                result,
                "status.damage_value_invalid",
                $"{path}.value",
                "Damage operation value must be greater than zero.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void AddError(
        StatusEffectDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            CharacterDefinitionDiagnosticSeverity.Error,
            code,
            path,
            message);
    }

    private static void AddWarning(
        StatusEffectDefinitionValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            CharacterDefinitionDiagnosticSeverity.Warning,
            code,
            path,
            message);
    }
}
