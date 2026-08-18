using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyStackingPolicy
{
    Stackable = 0,
    Exclusive = 1,
}

[CreateAssetMenu(fileName = "Enemy", menuName = "Dungeon/Enemy")]
public sealed class EnemySO : ScriptableObject,
    IBattlePresentationUnitDefinition,
    IBattleAbilityProvider
{
    public const int CurrentCombatStatSchemaVersion = 3;
    public const int CurrentRosterSchemaVersion = 1;
    public const float DefaultFormationRadius = 0.35f;
    public const float DefaultForwardSearchAngle = 60f;
    public const float MaximumForwardSearchAngle = 180f;
    public const int MaximumFootprintSize = 9;

    [Header("Identity")]
    [SerializeField] private string enemyId;
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField] private string displayName = "BASIC";
    [SerializeField, TextArea(2, 6)] private string description;
    [SerializeField] private string cardCode;
    [SerializeField] private EEnemyGrade grade = EEnemyGrade.Normal;
    [SerializeField] private EEnemyType type = EEnemyType.Basic;

    [Header("Roster Metadata")]
    [SerializeField, HideInInspector]
    private int rosterSchemaVersion;
    [SerializeField]
    private EnemyRosterTier rosterTier;
    [SerializeField]
    private List<string> roleTags = new();
    [SerializeField]
    private List<string> counterTags = new();
    [SerializeField, Min(0), Tooltip("0 allows the wave composer default.")]
    private int recommendedMaxPerWave;
    [SerializeField, Min(0f), Tooltip("0 uses resolved Threat Cost.")]
    private float spawnBudget;
    [SerializeField, Tooltip(
        "Only dedicated encounters may select this enemy.")]
    private bool encounterOnly;

    [Header("Presentation")]
    [SerializeField] private Sprite iconSprite;
    [Tooltip("Enemy-specific Sprite used by the dungeon world actor.")]
    [SerializeField] private Sprite boardSprite;
    [SerializeField] private int sortOrder;

    [Header("3D VFX")]
    [SerializeField] private BattleVfxCueSO spawnVfxCue;
    [SerializeField] private BattleVfxCueSO deathVfxCue;

    [Header("Base Stats")]
    [SerializeField, Min(1)] private int baseHealth = 20;
    [SerializeField, Min(0.1f)] private float healthScale = 1f;
    [SerializeField, Min(0)] private int initialArmor;
    [SerializeField, Min(0)] private int initialShield;
    [SerializeField, Min(0.1f)] private float spawnIntervalMultiplier = 1f;
    [SerializeField, Min(0.01f), Tooltip(
        "Normalized radial distance travelled per second in circular battles.")]
    private float approachSpeed = 0.08f;
    [SerializeField, Min(0.01f), Tooltip(
        "World-space occupancy radius used by circular enemy formations.")]
    private float formationRadius = DefaultFormationRadius;
    [SerializeField, Range(0f, MaximumForwardSearchAngle), Tooltip(
        "Full forward cone angle used to find an open path when another " +
        "enemy blocks movement. 60 means 30 degrees to each side. Zero " +
        "waits behind the blocker without steering.")]
    private float forwardSearchAngle = DefaultForwardSearchAngle;
    [SerializeField, HideInInspector]
    private int combatStatSchemaVersion;
    [SerializeField, Min(0.1f)] private float attackPower = 5f;
    [SerializeField, Min(1)] private int coreAttackDamage = 5;
    [SerializeField]
    private EnemyCoreAttackDamagePolicy coreAttackDamagePolicy;
    [SerializeField, Min(0f), Tooltip(
        "Used by Accumulate Fraction. Zero preserves the legacy integer " +
        "damage value.")]
    private float preciseCoreAttackDamage;
    [SerializeField, Min(0.1f)] private float coreAttackInterval = 2f;
    [SerializeField, Min(0f), Tooltip(
        "World-space distance from the defense line at which this enemy " +
        "can attack the core. Zero is melee range.")]
    private float coreAttackRange;
    [SerializeField, Min(0f), Tooltip("0 uses the default threat for this enemy type.")]
    private float threatCost;
    [SerializeField, Range(-1, 100), Tooltip("-1 uses the default unlock difficulty for this enemy type.")]
    private int unlockDifficulty = -1;

    [Header("Board Footprint")]
    [SerializeField, Range(1, MaximumFootprintSize)]
    private int footprintWidth = 1;
    [SerializeField, Range(1, MaximumFootprintSize)]
    private int footprintHeight = 1;
    [SerializeField] private EnemyStackingPolicy stackingPolicy;

    [Header("Abilities")]
    [SerializeField]
    private List<EnemyAbilityDefinition> abilities = new();

    [Header("Boss Phases")]
    [SerializeField]
    private List<EnemyBossPhaseDefinition> phaseDefinitions = new();

    public string EnemyId => enemyId ?? string.Empty;
    public string NameLocalizationKey => nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string Description => description ?? string.Empty;
    public string CardCode => cardCode ?? string.Empty;
    public EEnemyGrade Grade => grade;
    public EEnemyType Type => type;
    public int RosterSchemaVersion => rosterSchemaVersion;
    public EnemyRosterTier RosterTier => rosterSchemaVersion > 0
        ? rosterTier
        : GetRosterTier(grade);
    public IReadOnlyList<string> RoleTags => roleTags != null
        ? roleTags
        : Array.Empty<string>();
    public IReadOnlyList<string> CounterTags => counterTags != null
        ? counterTags
        : Array.Empty<string>();
    public int RecommendedMaxPerWave =>
        Mathf.Max(0, recommendedMaxPerWave);
    public float SpawnBudget => IsFinite(spawnBudget) && spawnBudget > 0f
        ? spawnBudget
        : ThreatCost;
    public bool EncounterOnly => encounterOnly;
    public Sprite IconSprite => iconSprite;
    public Sprite BoardSprite => boardSprite;
    public int SortOrder => sortOrder;
    public BattleVfxCueSO SpawnVfxCue => spawnVfxCue;
    public BattleVfxCueSO DeathVfxCue => deathVfxCue;
    public int BaseHealth => baseHealth;
    public float HealthScale => Mathf.Max(0.1f, healthScale);
    public int InitialArmor => Mathf.Max(0, initialArmor);
    public int InitialShield => Mathf.Max(0, initialShield);
    public float SpawnIntervalMultiplier =>
        TimePrecision.Normalize(spawnIntervalMultiplier, 0.1f);
    public float ApproachSpeed => Mathf.Max(0.01f, approachSpeed);
    public float FormationRadius =>
        IsFinite(formationRadius) && formationRadius > 0f
            ? formationRadius
            : GetDefaultFormationRadius(type);
    public float ForwardSearchAngle =>
        IsFinite(forwardSearchAngle)
            ? Mathf.Clamp(
                forwardSearchAngle,
                0f,
                MaximumForwardSearchAngle)
            : DefaultForwardSearchAngle;
    public float AttackPower => combatStatSchemaVersion > 0
        ? Mathf.Max(0.1f, attackPower)
        : CoreAttackDamage;
    public int CoreAttackDamage => Mathf.Max(1, coreAttackDamage);
    public EnemyCoreAttackDamagePolicy CoreAttackDamagePolicy =>
        coreAttackDamagePolicy;
    public float CoreAttackDamageValue =>
        coreAttackDamagePolicy ==
            EnemyCoreAttackDamagePolicy.AccumulateFraction &&
        IsFinite(preciseCoreAttackDamage) &&
        preciseCoreAttackDamage > 0f
            ? preciseCoreAttackDamage
            : CoreAttackDamage;
    public float CoreAttackInterval => TimePrecision.Normalize(
        coreAttackInterval,
        0.1f);
    public float CoreAttackRange =>
        IsFinite(coreAttackRange) && coreAttackRange >= 0f
            ? coreAttackRange
            : 0f;
    public float ThreatCost => threatCost > 0f
        ? threatCost
        : GetDefaultThreatCost(type);
    public int UnlockDifficulty => unlockDifficulty >= 0
        ? Mathf.Clamp(unlockDifficulty, 0, 100)
        : GetDefaultUnlockDifficulty(type);
    public int FootprintWidth => Mathf.Clamp(
        footprintWidth,
        1,
        MaximumFootprintSize);
    public int FootprintHeight => Mathf.Clamp(
        footprintHeight,
        1,
        MaximumFootprintSize);
    public int FootprintArea => FootprintWidth * FootprintHeight;
    public bool OccupiesMultipleCells => FootprintArea > 1;
    public EnemyStackingPolicy StackingPolicy => OccupiesMultipleCells
        ? EnemyStackingPolicy.Exclusive
        : stackingPolicy;
    public IReadOnlyList<EnemyAbilityDefinition> Abilities =>
        abilities != null
            ? abilities
            : Array.Empty<EnemyAbilityDefinition>();
    public IReadOnlyList<EnemyBossPhaseDefinition> PhaseDefinitions =>
        phaseDefinitions != null
            ? phaseDefinitions
            : Array.Empty<EnemyBossPhaseDefinition>();
    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        foreach (EnemyAbilityDefinition ability in Abilities)
        {
            if (ability?.HasExecutableContent == true)
                yield return ability;
        }
    }

    internal float AuthoredHealthScale => healthScale;
    internal int AuthoredInitialArmor => initialArmor;
    internal int AuthoredInitialShield => initialShield;
    internal float AuthoredApproachSpeed => approachSpeed;
    internal float AuthoredFormationRadius => formationRadius;
    internal float AuthoredForwardSearchAngle => forwardSearchAngle;
    internal int AuthoredCombatStatSchemaVersion =>
        combatStatSchemaVersion;
    internal int AuthoredRosterSchemaVersion => rosterSchemaVersion;
    internal EnemyRosterTier AuthoredRosterTier => rosterTier;
    internal int AuthoredRecommendedMaxPerWave =>
        recommendedMaxPerWave;
    internal float AuthoredSpawnBudget => spawnBudget;
    internal float AuthoredAttackPower => attackPower;
    internal int AuthoredCoreAttackDamage => coreAttackDamage;
    internal float AuthoredPreciseCoreAttackDamage =>
        preciseCoreAttackDamage;
    internal float AuthoredCoreAttackInterval => coreAttackInterval;
    internal float AuthoredCoreAttackRange => coreAttackRange;
    internal int AuthoredUnlockDifficulty => unlockDifficulty;
    internal int AuthoredFootprintWidth => footprintWidth;
    internal int AuthoredFootprintHeight => footprintHeight;
    internal EnemyStackingPolicy AuthoredStackingPolicy => stackingPolicy;

    public EnemyRuntime CreateRuntime(int maximumHealthOverride = 0)
    {
        return new EnemyRuntime(this, maximumHealthOverride);
    }

    private void OnValidate()
    {
        EnemyDefinitionCatalog.Invalidate();
    }

    public void RegenerateEnemyId()
    {
        enemyId = Guid.NewGuid().ToString("N");
    }

    [ContextMenu("Apply Current Type Defaults")]
    private void ApplyCurrentTypeDefaults()
    {
        string persistentId = enemyId;
        ApplyTypeDefaults(type, baseHealth);
        enemyId = persistentId;
    }

    internal static EnemySO CreateRuntimeDefault(
        EEnemyType enemyType,
        int health)
    {
        EnemySO definition = CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.ApplyTypeDefaults(enemyType, health);
        return definition;
    }

    private void ApplyTypeDefaults(EEnemyType enemyType, int health)
    {
        type = enemyType;
        enemyId = EnemyTypeDisplay.GetId(enemyType);
        displayName = EnemyTypeDisplay.GetName(enemyType);
        cardCode = EnemyTypeDisplay.GetCardCode(enemyType);
        grade = IsSpecialType(enemyType)
            ? EEnemyGrade.Special
            : EEnemyGrade.Normal;
        rosterSchemaVersion = CurrentRosterSchemaVersion;
        rosterTier = GetRosterTier(grade);
        roleTags = new List<string>
        {
            EnemyTypeDisplay.GetId(enemyType)
        };
        counterTags = new List<string>();
        recommendedMaxPerWave = grade == EEnemyGrade.Special ? 2 : 0;
        spawnBudget = 0f;
        encounterOnly = false;
        baseHealth = Mathf.Max(1, health);
        sortOrder = (int)enemyType * 10;
        healthScale = 1f;
        initialArmor = 0;
        initialShield = 0;
        spawnIntervalMultiplier = 1f;
        formationRadius = GetDefaultFormationRadius(enemyType);
        forwardSearchAngle = DefaultForwardSearchAngle;
        combatStatSchemaVersion = CurrentCombatStatSchemaVersion;
        attackPower = coreAttackDamage;
        coreAttackDamagePolicy =
            EnemyCoreAttackDamagePolicy.LegacyInteger;
        preciseCoreAttackDamage = coreAttackDamage;
        coreAttackRange = 0f;
        threatCost = 0f;
        unlockDifficulty = -1;
        footprintWidth = 1;
        footprintHeight = 1;
        stackingPolicy = EnemyStackingPolicy.Stackable;
        nameLocalizationKey = string.Empty;
        descriptionLocalizationKey = string.Empty;
        description = string.Empty;
        phaseDefinitions = new List<EnemyBossPhaseDefinition>();

        if (enemyType == EEnemyType.Assault)
            spawnIntervalMultiplier = 0.5f;

        abilities = CreateDefaultAbilities(enemyType);
    }

    private List<EnemyAbilityDefinition> CreateDefaultAbilities(
        EEnemyType enemyType)
    {
        List<EnemyAbilityDefinition> result = new();

        switch (enemyType)
        {
            case EEnemyType.Medic:
            {
                CharacterEffectDefinition heal =
                    CharacterEffectDefinition.CreateFixedRuntimeEffect(
                        CharacterEffectType.Heal,
                        1f);
                EnemyAbilityOperationDefinition operation =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.ExecuteEffects,
                        new[] { heal });
                EnemyAbilityTargetDefinition adjacent =
                    EnemyAbilityTargetDefinition.CreateRuntimePreset(
                        EnemyAbilityTargetFaction.EnemyAllies,
                        EnemyAbilityTargetSubject.Adjacent,
                        EnemyAbilityTargetMetric.Health,
                        targetRange: 1);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.AdjacentHeal,
                    "Field Treatment",
                    "Periodically heal orthogonally adjacent allies.",
                    EnemyAbilityTrigger.OnCooldown,
                    adjacent,
                    new[] { operation },
                    abilityCooldown: 4f));
                break;
            }

            case EEnemyType.Mechanic:
            {
                CharacterEffectDefinition stun =
                    CharacterEffectDefinition.CreateFixedRuntimeEffect(
                        CharacterEffectType.ApplyStatus,
                        1f,
                        StatusEffectDefinitionCatalog.FindById(
                            StatusEffectIds.Stun),
                        5f,
                        1f);
                EnemyAbilityOperationDefinition operation =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.ExecuteEffects,
                        new[] { stun });
                EnemyAbilityTargetDefinition highestDamage =
                    EnemyAbilityTargetDefinition.CreateRuntimePreset(
                        EnemyAbilityTargetFaction.PlayerCharacters,
                        EnemyAbilityTargetSubject.HighestValue,
                        EnemyAbilityTargetMetric.TotalDamageDealt);
                EnemyAbilityConditionDefinition positiveDamage =
                    EnemyAbilityConditionDefinition
                        .CreatePositiveTargetDamagePreset();
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.DisableHighestDamage,
                    "System Disruption",
                    "Stun the player character with the highest damage " +
                    "dealt.",
                    EnemyAbilityTrigger.OnCooldown,
                    highestDamage,
                    new[] { operation },
                    abilityCooldown: 10f,
                    conditionDefinitions:
                    new[] { positiveDamage }));
                break;
            }

        }

        return result;
    }

    private static bool IsSpecialType(EEnemyType enemyType)
    {
        return enemyType == EEnemyType.Pointman ||
               enemyType == EEnemyType.ShieldBearer ||
               enemyType == EEnemyType.Infiltrator;
    }

    internal static EnemyRosterTier GetRosterTier(EEnemyGrade enemyGrade)
    {
        return enemyGrade switch
        {
            EEnemyGrade.Special => EnemyRosterTier.Special,
            EEnemyGrade.Elite => EnemyRosterTier.Elite,
            EEnemyGrade.Boss => EnemyRosterTier.Boss,
            _ => EnemyRosterTier.General,
        };
    }

    private static float GetDefaultThreatCost(EEnemyType enemyType)
    {
        return enemyType switch
        {
            EEnemyType.Assault => 1.15f,
            EEnemyType.Heavy => 1.35f,
            EEnemyType.Medic => 1.3f,
            EEnemyType.Mechanic => 1.8f,
            EEnemyType.Pointman => 1.3f,
            EEnemyType.ShieldBearer => 2.3f,
            EEnemyType.Infiltrator => 1.4f,
            _ => 1f,
        };
    }

    private static int GetDefaultUnlockDifficulty(EEnemyType enemyType)
    {
        return enemyType switch
        {
            EEnemyType.Assault => 10,
            EEnemyType.Heavy => 20,
            EEnemyType.Medic => 30,
            EEnemyType.Infiltrator => 40,
            EEnemyType.Mechanic => 45,
            EEnemyType.Pointman => 55,
            EEnemyType.ShieldBearer => 70,
            _ => 0,
        };
    }

    internal static float GetDefaultFormationRadius(EEnemyType enemyType)
    {
        return enemyType switch
        {
            EEnemyType.Assault => 0.32f,
            EEnemyType.Heavy => 0.45f,
            EEnemyType.Mechanic => 0.38f,
            EEnemyType.ShieldBearer => 0.45f,
            EEnemyType.Infiltrator => 0.3f,
            _ => DefaultFormationRadius,
        };
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
