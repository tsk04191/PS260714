using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Dungeon/Enemy")]
public sealed class EnemySO : ScriptableObject,
    IBattlePresentationUnitDefinition
{
    [Header("Identity")]
    [SerializeField] private string enemyId;
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField] private string displayName = "BASIC";
    [SerializeField, TextArea(2, 6)] private string description;
    [SerializeField] private string cardCode;
    [SerializeField] private EEnemyGrade grade = EEnemyGrade.Normal;
    [SerializeField] private EEnemyType type = EEnemyType.Basic;

    [Header("3D VFX")]
    [SerializeField] private BattleVfxCueSO spawnVfxCue;
    [SerializeField] private BattleVfxCueSO deathVfxCue;

    [Header("Base Stats")]
    [SerializeField, Min(1)] private int baseHealth = 20;
    [SerializeField, Min(0.1f)] private float spawnIntervalMultiplier = 1f;
    [SerializeField, Min(0f), Tooltip("0 uses the default threat for this enemy type.")]
    private float threatCost;

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
    public BattleVfxCueSO SpawnVfxCue => spawnVfxCue;
    public BattleVfxCueSO DeathVfxCue => deathVfxCue;
    public int BaseHealth => baseHealth;
    public float SpawnIntervalMultiplier =>
        TimePrecision.Normalize(spawnIntervalMultiplier, 0.1f);
    public float ThreatCost => threatCost > 0f
        ? threatCost
        : GetDefaultThreatCost(type);
    public IReadOnlyList<EnemyAbilityDefinition> Abilities =>
        abilities != null
            ? abilities
            : Array.Empty<EnemyAbilityDefinition>();

    public EnemyRuntime CreateRuntime(int maximumHealthOverride = 0)
    {
        return new EnemyRuntime(this, maximumHealthOverride);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            RegenerateEnemyId();
        else
            enemyId = enemyId.Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = EnemyTypeDisplay.GetName(type);
        else
            displayName = displayName.Trim();
        description ??= string.Empty;
        if (string.IsNullOrWhiteSpace(cardCode))
            cardCode = EnemyTypeDisplay.GetCardCode(type);
        else
            cardCode = cardCode.Trim();

        baseHealth = Mathf.Max(1, baseHealth);
        spawnIntervalMultiplier =
            TimePrecision.Normalize(spawnIntervalMultiplier, 0.1f);
        threatCost = Mathf.Max(0f, threatCost);

        abilities ??= new List<EnemyAbilityDefinition>();
        foreach (EnemyAbilityDefinition ability in abilities)
            ability?.Validate();
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
        enemyId = enemyType.ToString().ToLowerInvariant();
        displayName = EnemyTypeDisplay.GetName(enemyType);
        cardCode = EnemyTypeDisplay.GetCardCode(enemyType);
        grade = IsSpecialType(enemyType)
            ? EEnemyGrade.Special
            : EEnemyGrade.Normal;
        baseHealth = Mathf.Max(1, health);
        spawnIntervalMultiplier = 1f;
        threatCost = 0f;
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
        EnemyAbilityTargetDefinition self =
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.Self,
                EnemyAbilityTargetSubject.Self,
                EnemyAbilityTargetMetric.Health);
        EnemyAbilityTargetDefinition noTarget =
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.None,
                EnemyAbilityTargetSubject.None);

        switch (enemyType)
        {
            case EEnemyType.Heavy:
            {
                EnemyAbilityConditionDefinition physical =
                    EnemyAbilityConditionDefinition
                        .CreateIncomingDamagePreset(
                            CharacterAttackDamageType.Physical);
                EnemyAbilityConditionDefinition magical =
                    EnemyAbilityConditionDefinition
                        .CreateIncomingDamagePreset(
                            CharacterAttackDamageType.Magical);
                EnemyAbilityOperationDefinition operation =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.ModifyIncomingDamage,
                        fixedAmount: 1);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.GuardedHits,
                    "Guarded Hits",
                    "Reduce a limited number of physical or magical " +
                    "hits to 1.",
                    EnemyAbilityTrigger.BeforeSelfDamage,
                    self,
                    new[] { operation },
                    charges: 3,
                    matchMode: CharacterConditionMatchMode.Any,
                    conditionDefinitions:
                    new[] { physical, magical }));
                break;
            }

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

            case EEnemyType.Pointman:
            {
                EnemyAbilityOperationDefinition operation =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.ExpandSpawnGroup,
                        additionalCount: 2);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.ExpandSpawnGroup,
                    "Coordinated Entry",
                    "Spawn additional queued enemies in the same group.",
                    EnemyAbilityTrigger.OnSpawnQueueEvaluation,
                    noTarget,
                    new[] { operation },
                    charges: 1));
                break;
            }

            case EEnemyType.ShieldBearer:
            {
                EnemyAbilityOperationDefinition armor =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.GrantArmor,
                        valueMultiplier: 1f);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.InitialArmor,
                    "Initial Armor",
                    "Gain armor based on maximum health when spawned.",
                    EnemyAbilityTrigger.OnSpawn,
                    self,
                    new[] { armor },
                    charges: 1));

                EnemyAbilityOperationDefinition redirect =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.RedirectDamage,
                        operationRange: 1,
                        diagonals: true);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.RedirectAdjacentDamage,
                    "Shield Formation",
                    "Take damage for adjacent allies, including diagonals.",
                    EnemyAbilityTrigger.BeforeAllyDamage,
                    noTarget,
                    new[] { redirect }));
                break;
            }

            case EEnemyType.Infiltrator:
            {
                EnemyAbilityConditionDefinition alternate =
                    EnemyAbilityConditionDefinition
                        .CreateAlternateTargetPreset();
                EnemyAbilityOperationDefinition operation =
                    EnemyAbilityOperationDefinition.CreateRuntimePreset(
                        EnemyAbilityOperationType.ModifyTargetPriority);
                result.Add(EnemyAbilityDefinition.CreateRuntimePreset(
                    EnemyAbilityIds.TargetPriorityExclusion,
                    "Concealment",
                    "Avoid target selection while another target is " +
                    "available.",
                    EnemyAbilityTrigger.OnTargetPriorityEvaluation,
                    noTarget,
                    new[] { operation },
                    conditionDefinitions: new[] { alternate }));
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
}
