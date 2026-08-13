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
    [SerializeField, Min(1)] private int coreAttackDamage = 5;
    [SerializeField, Min(0.1f)] private float coreAttackInterval = 2f;
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

    public string EnemyId => enemyId ?? string.Empty;
    public string NameLocalizationKey => nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string Description => description ?? string.Empty;
    public string CardCode => cardCode ?? string.Empty;
    public EEnemyGrade Grade => grade;
    public EEnemyType Type => type;
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
    public int CoreAttackDamage => Mathf.Max(1, coreAttackDamage);
    public float CoreAttackInterval => TimePrecision.Normalize(
        coreAttackInterval,
        0.1f);
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
    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        foreach (EnemyAbilityDefinition ability in Abilities)
        {
            if (ability?.HasUnifiedEffects == true)
                yield return ability;
        }
    }

    internal float AuthoredHealthScale => healthScale;
    internal int AuthoredInitialArmor => initialArmor;
    internal int AuthoredInitialShield => initialShield;
    internal float AuthoredApproachSpeed => approachSpeed;
    internal int AuthoredCoreAttackDamage => coreAttackDamage;
    internal float AuthoredCoreAttackInterval => coreAttackInterval;
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
        ApplyTypeDefaults(type, baseHealth);
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
        baseHealth = Mathf.Max(1, health);
        sortOrder = (int)enemyType * 10;
        healthScale = 1f;
        initialArmor = 0;
        initialShield = 0;
        spawnIntervalMultiplier = 1f;
        threatCost = 0f;
        unlockDifficulty = -1;
        footprintWidth = 1;
        footprintHeight = 1;
        stackingPolicy = EnemyStackingPolicy.Stackable;
        nameLocalizationKey = string.Empty;
        descriptionLocalizationKey = string.Empty;
        description = string.Empty;

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
}
