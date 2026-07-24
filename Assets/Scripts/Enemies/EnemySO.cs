using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Dungeon/Enemy")]
public sealed class EnemySO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string enemyId = "basic";
    [SerializeField] private string displayName = "BASIC";
    [SerializeField] private string cardCode;
    [SerializeField] private EEnemyGrade grade = EEnemyGrade.Normal;
    [SerializeField] private EEnemyType type = EEnemyType.Basic;

    [Header("Base Stats")]
    [SerializeField, Min(1)] private int baseHealth = 20;
    [SerializeField, Min(0.1f)] private float spawnIntervalMultiplier = 1f;
    [SerializeField, Min(0f), Tooltip("0 uses the default threat for this enemy type.")]
    private float threatCost;
    [SerializeField] private bool targetPriorityExcluded;
    [SerializeField, Min(0f)] private float initialArmorMultiplier;

    [Header("Ability")]
    [SerializeField, Min(0)] private int guardedHitCount;
    [SerializeField, Min(0)] private int companionSpawnCount;
    [SerializeField, Min(0f)] private float abilityCooldown;
    [SerializeField, Min(0)] private int abilityPower = 1;
    [SerializeField, Min(0f)] private float disableDuration;
    [SerializeField] private StatusEffectSO disableStatusEffect;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public string CardCode => cardCode;
    public EEnemyGrade Grade => grade;
    public EEnemyType Type => type;
    public int BaseHealth => baseHealth;
    public float SpawnIntervalMultiplier =>
        TimePrecision.Normalize(spawnIntervalMultiplier, 0.1f);
    public float ThreatCost => threatCost > 0f
        ? threatCost
        : GetDefaultThreatCost(type);
    public bool TargetPriorityExcluded => targetPriorityExcluded;
    public float InitialArmorMultiplier => initialArmorMultiplier;
    public int GuardedHitCount => guardedHitCount;
    public int CompanionSpawnCount => companionSpawnCount;
    public float AbilityCooldown => TimePrecision.FloorToTenth(abilityCooldown);
    public int AbilityPower => abilityPower;
    public float DisableDuration => TimePrecision.FloorToTenth(disableDuration);
    public StatusEffectSO DisableStatusEffect => disableStatusEffect != null
        ? disableStatusEffect
        : StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Stun);

    public EnemyRuntime CreateRuntime(int maximumHealthOverride = 0)
    {
        return new EnemyRuntime(this, maximumHealthOverride);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            enemyId = type.ToString().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = EnemyTypeDisplay.GetName(type);
        if (string.IsNullOrWhiteSpace(cardCode))
            cardCode = EnemyTypeDisplay.GetCardCode(type);

        baseHealth = Mathf.Max(1, baseHealth);
        spawnIntervalMultiplier =
            TimePrecision.Normalize(spawnIntervalMultiplier, 0.1f);
        threatCost = Mathf.Max(0f, threatCost);
        initialArmorMultiplier = Mathf.Max(0f, initialArmorMultiplier);
        guardedHitCount = Mathf.Max(0, guardedHitCount);
        companionSpawnCount = Mathf.Max(0, companionSpawnCount);
        abilityCooldown = TimePrecision.FloorToTenth(abilityCooldown);
        abilityPower = Mathf.Max(0, abilityPower);
        disableDuration = TimePrecision.FloorToTenth(disableDuration);
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
        targetPriorityExcluded = false;
        initialArmorMultiplier = 0f;
        guardedHitCount = 0;
        companionSpawnCount = 0;
        abilityCooldown = 0f;
        abilityPower = 1;
        disableDuration = 0f;

        switch (enemyType)
        {
            case EEnemyType.Assault:
                spawnIntervalMultiplier = 0.5f;
                break;
            case EEnemyType.Heavy:
                guardedHitCount = 3;
                break;
            case EEnemyType.Medic:
                abilityCooldown = 4f;
                break;
            case EEnemyType.Mechanic:
                abilityCooldown = 10f;
                disableDuration = 5f;
                break;
            case EEnemyType.Pointman:
                companionSpawnCount = 2;
                break;
            case EEnemyType.ShieldBearer:
                initialArmorMultiplier = 1f;
                break;
            case EEnemyType.Infiltrator:
                targetPriorityExcluded = true;
                break;
        }
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
