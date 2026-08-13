using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

/// <summary>
/// Localized presentation for enemy data. Canonical English names in
/// EnemyTypeDisplay remain available for serialized data and validation.
/// </summary>
public static class EnemyLocalization
{
    public static string GetName(EnemySO definition)
    {
        if (definition == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(definition.NameLocalizationKey))
        {
            return LocalizationService.Get(
                definition.NameLocalizationKey);
        }

        return !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName
            : GetName(definition.Type);
    }

    public static string GetDescription(EnemySO definition)
    {
        if (definition == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(
            definition.DescriptionLocalizationKey)
            ? LocalizationService.Get(
                definition.DescriptionLocalizationKey)
            : definition.Description;
    }

    public static string GetAbilityName(
        EnemyAbilityDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(
            definition.NameLocalizationKey)
            ? LocalizationService.Get(definition.NameLocalizationKey)
            : definition.FallbackName;
    }

    public static string GetAbilityDescription(
        EnemyAbilityDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(
            definition.DescriptionLocalizationKey)
            ? LocalizationService.Get(
                definition.DescriptionLocalizationKey,
                BattleAbilityLocalizationArguments.Build(definition))
            : definition.FallbackDescription;
    }

    public static string GetName(EEnemyType type)
    {
        return LocalizationService.Get(type switch
        {
            EEnemyType.Assault => LocalizationKeys.EnemyAssaultName,
            EEnemyType.Heavy => LocalizationKeys.EnemyHeavyName,
            EEnemyType.Medic => LocalizationKeys.EnemyMedicName,
            EEnemyType.Mechanic => LocalizationKeys.EnemyMechanicName,
            EEnemyType.Pointman => LocalizationKeys.EnemyPointmanName,
            EEnemyType.ShieldBearer =>
                LocalizationKeys.EnemyShieldBearerName,
            EEnemyType.Infiltrator =>
                LocalizationKeys.EnemyInfiltratorName,
            _ => LocalizationKeys.EnemyBasicName,
        });
    }

    public static string GetGrade(EEnemyGrade grade)
    {
        return LocalizationService.Get(grade switch
        {
            EEnemyGrade.Special =>
                LocalizationKeys.CodexEnemyGradeSpecial,
            EEnemyGrade.Elite => LocalizationKeys.CodexEnemyGradeElite,
            EEnemyGrade.Boss => LocalizationKeys.CodexEnemyGradeBoss,
            _ => LocalizationKeys.CodexEnemyGradeNormal,
        });
    }

    public static string GetPriority(bool excluded)
    {
        return LocalizationService.Get(excluded
            ? LocalizationKeys.CodexEnemyPriorityExcluded
            : LocalizationKeys.CodexEnemyPriorityNormal);
    }

    public static bool HasTargetPriorityExclusion(EnemySO definition)
    {
        if (definition == null)
            return false;

        foreach (EnemyAbilityDefinition ability in definition.Abilities)
        {
            if (ability == null ||
                ability.Trigger !=
                EnemyAbilityTrigger.OnTargetPriorityEvaluation)
            {
                continue;
            }

            if (TryGetEnabledOperation(
                    ability,
                    EnemyAbilityOperationType.ModifyTargetPriority,
                    out EnemyAbilityOperationDefinition operation) &&
                operation.TargetPriorityMode ==
                    EnemyTargetPriorityMode.Exclude)
            {
                return true;
            }
        }

        return false;
    }

    public static string GetAbility(EnemySO definition)
    {
        if (definition == null)
        {
            return LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityBasic);
        }

        List<string> descriptions = new();
        if (!Mathf.Approximately(
                definition.SpawnIntervalMultiplier,
                1f))
        {
            descriptions.Add(LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityAssault,
                LocalizationService.Arg(
                    "interval",
                    definition.SpawnIntervalMultiplier)));
        }

        bool shieldSummaryAdded = false;
        foreach (EnemyAbilityDefinition ability in definition.Abilities)
        {
            if (ability == null)
                continue;

            if (IsShieldFormationAbility(ability.AbilityId) &&
                TryFormatShieldFormation(
                    definition,
                    out string shieldDescription))
            {
                if (!shieldSummaryAdded)
                {
                    descriptions.Add(shieldDescription);
                    shieldSummaryAdded = true;
                }
                continue;
            }

            if (TryFormatKnownAbility(
                    ability,
                    out string knownDescription))
            {
                descriptions.Add(knownDescription);
                continue;
            }

            string genericDescription =
                FormatGenericAbility(ability);
            if (!string.IsNullOrWhiteSpace(genericDescription))
                descriptions.Add(genericDescription);
        }

        return descriptions.Count > 0
            ? string.Join("\n\n", descriptions)
            : LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityBasic);
    }

    private static bool TryFormatKnownAbility(
        EnemyAbilityDefinition ability,
        out string description)
    {
        description = string.Empty;
        switch (ability.AbilityId)
        {
            case EnemyAbilityIds.GuardedHits:
                if (!TryGetEnabledOperation(
                        ability,
                        EnemyAbilityOperationType.ModifyIncomingDamage,
                        out _))
                {
                    return false;
                }
                description = LocalizationService.Get(
                    LocalizationKeys.CodexEnemyAbilityHeavy,
                    LocalizationService.Arg(
                        "hits",
                        ability.InitialCharges));
                return true;

            case EnemyAbilityIds.AdjacentHeal:
                if (!TryGetEffect(
                        ability,
                        CharacterEffectType.Heal,
                        out CharacterEffectDefinition heal))
                {
                    return false;
                }
                description = LocalizationService.Get(
                    LocalizationKeys.CodexEnemyAbilityMedic,
                    LocalizationService.Arg(
                        "cooldown",
                        ability.Cooldown),
                    LocalizationService.Arg(
                        "power",
                        heal.DamageAmount));
                return true;

            case EnemyAbilityIds.DisableHighestDamage:
                if (!TryGetEffect(
                        ability,
                        CharacterEffectType.ApplyStatus,
                        out CharacterEffectDefinition status))
                {
                    return false;
                }
                description = LocalizationService.Get(
                    LocalizationKeys.CodexEnemyAbilityMechanic,
                    LocalizationService.Arg(
                        "cooldown",
                        ability.Cooldown),
                    LocalizationService.Arg(
                        "duration",
                        status.StatusDuration));
                return true;

            case EnemyAbilityIds.ExpandSpawnGroup:
                if (!TryGetEnabledOperation(
                        ability,
                        EnemyAbilityOperationType.ExpandSpawnGroup,
                        out EnemyAbilityOperationDefinition expand))
                {
                    return false;
                }
                description = LocalizationService.Get(
                    LocalizationKeys.CodexEnemyAbilityPointman,
                    LocalizationService.Arg(
                        "count",
                        expand.Count));
                return true;

            case EnemyAbilityIds.TargetPriorityExclusion:
                if (!TryGetEnabledOperation(
                        ability,
                        EnemyAbilityOperationType.ModifyTargetPriority,
                        out EnemyAbilityOperationDefinition priority) ||
                    priority.TargetPriorityMode !=
                        EnemyTargetPriorityMode.Exclude)
                {
                    return false;
                }
                description = LocalizationService.Get(
                    LocalizationKeys.CodexEnemyAbilityInfiltrator);
                return true;

            default:
                return false;
        }
    }

    private static bool TryFormatShieldFormation(
        EnemySO definition,
        out string description)
    {
        description = string.Empty;
        EnemyAbilityDefinition armorAbility =
            FindAbility(definition, EnemyAbilityIds.InitialArmor);
        EnemyAbilityDefinition redirectAbility =
            FindAbility(
                definition,
                EnemyAbilityIds.RedirectAdjacentDamage);
        if (armorAbility == null ||
            redirectAbility == null ||
            !TryGetEnabledOperation(
                armorAbility,
                EnemyAbilityOperationType.GrantArmor,
                out EnemyAbilityOperationDefinition armor) ||
            !TryGetEnabledOperation(
                redirectAbility,
                EnemyAbilityOperationType.RedirectDamage,
                out _))
        {
            return false;
        }

        description = LocalizationService.Get(
            LocalizationKeys.CodexEnemyAbilityShieldBearer,
            LocalizationService.Arg(
                "armor",
                armor.Multiplier * 100f));
        return true;
    }

    private static string FormatGenericAbility(
        EnemyAbilityDefinition ability)
    {
        string name = GetAbilityName(ability);
        string description = GetAbilityDescription(ability);
        if (string.IsNullOrWhiteSpace(name))
            return description;
        if (string.IsNullOrWhiteSpace(description))
            return name;
        return $"{name}\n{description}";
    }

    private static EnemyAbilityDefinition FindAbility(
        EnemySO definition,
        string abilityId)
    {
        foreach (EnemyAbilityDefinition ability in definition.Abilities)
        {
            if (ability != null &&
                string.Equals(
                    ability.AbilityId,
                    abilityId,
                    StringComparison.Ordinal))
            {
                return ability;
            }
        }

        return null;
    }

    private static bool IsShieldFormationAbility(string abilityId)
    {
        return string.Equals(
                   abilityId,
                   EnemyAbilityIds.InitialArmor,
                   StringComparison.Ordinal) ||
               string.Equals(
                   abilityId,
                   EnemyAbilityIds.RedirectAdjacentDamage,
                   StringComparison.Ordinal);
    }

    private static bool TryGetEnabledOperation(
        EnemyAbilityDefinition ability,
        EnemyAbilityOperationType type,
        out EnemyAbilityOperationDefinition result)
    {
        result = null;
        if (ability == null)
            return false;

        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation != null &&
                operation.Enabled &&
                operation.Type == type)
            {
                result = operation;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEffect(
        EnemyAbilityDefinition ability,
        CharacterEffectType type,
        out CharacterEffectDefinition result)
    {
        result = null;
        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation == null ||
                !operation.Enabled ||
                operation.Type !=
                EnemyAbilityOperationType.ExecuteEffects)
            {
                continue;
            }

            foreach (CharacterEffectDefinition effect in
                     operation.Effects)
            {
                if (effect != null && effect.Type == type)
                {
                    result = effect;
                    return true;
                }
            }
        }

        return false;
    }
}
