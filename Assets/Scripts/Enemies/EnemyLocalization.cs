using PS260714.Localization;

/// <summary>
/// Localized presentation for enemy data. Canonical English names in
/// EnemyTypeDisplay remain available for serialized data and validation.
/// </summary>
public static class EnemyLocalization
{
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

    public static string GetAbility(EnemySO definition)
    {
        if (definition == null)
        {
            return LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityBasic);
        }

        return definition.Type switch
        {
            EEnemyType.Assault => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityAssault,
                LocalizationService.Arg(
                    "interval",
                    definition.SpawnIntervalMultiplier)),
            EEnemyType.Heavy => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityHeavy,
                LocalizationService.Arg(
                    "hits",
                    definition.GuardedHitCount)),
            EEnemyType.Medic => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityMedic,
                LocalizationService.Arg(
                    "cooldown",
                    definition.AbilityCooldown),
                LocalizationService.Arg(
                    "power",
                    definition.AbilityPower)),
            EEnemyType.Mechanic => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityMechanic,
                LocalizationService.Arg(
                    "cooldown",
                    definition.AbilityCooldown),
                LocalizationService.Arg(
                    "duration",
                    definition.DisableDuration)),
            EEnemyType.Pointman => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityPointman,
                LocalizationService.Arg(
                    "count",
                    definition.CompanionSpawnCount)),
            EEnemyType.ShieldBearer => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityShieldBearer,
                LocalizationService.Arg(
                    "armor",
                    definition.InitialArmorMultiplier * 100f)),
            EEnemyType.Infiltrator => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityInfiltrator),
            _ => LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityBasic),
        };
    }
}
