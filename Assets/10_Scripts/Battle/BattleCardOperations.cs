using System;
using UnityEngine;

public enum BattleCardOperationType
{
    SharedEffect = 0,
    ObjectiveRestore = 1,
    ObjectiveInvulnerability = 2,
    ObjectiveDamageRedirect = 3,
    SpendTargetHealth = 4,
    Revive = 5,
    Draw = 6,
    DiscardSelected = 7,
    ExhaustSelected = 8,
    ReturnDiscarded = 9,
    ShuffleDiscardIntoDraw = 10,
    ShuffleDrawAndDiscard = 11,
    DiscardHand = 12,
    GainEnergy = 13,
    ModifyCardCost = 14,
    ProtectHand = 15,
    Move = 16,
    Swap = 17,
    PullEnemies = 18,
    CreateZone = 19,
    ApplyAttackModifier = 20,
    ApplySkillModifier = 21,
    ApplyHealthTrigger = 22,
    ExtendStatusDuration = 23,
    ForceTarget = 24,
}

public enum BattleCardTargetScope
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Source = 3,
    AllEnemies = 4,
    AllAllies = 5,
    RandomEnemies = 6,
    EnemiesWithStatus = 7,
    AlliesWithRole = 8,
    NearbyPrimaryEnemies = 9,
    BehindPrimaryEnemy = 10,
    DefenseLineEnemies = 11,
    RecentObjectiveAttackers = 12,
    LowestHealthAlly = 13,
    DeadOrLowestHealthAlly = 14,
    SpecificCharacter = 15,
    EnemiesAtDesignatedPoint = 16,
}

public enum BattleCardConditionType
{
    None = 0,
    TargetHealthPercentage = 1,
    ObjectiveHealthPercentage = 2,
    HandCount = 3,
    PartyRoleCount = 4,
    DistinctAllyZoneCount = 5,
    TargetZone = 6,
    TargetHasStatus = 7,
    PreviousOperationSucceeded = 8,
    PreviousOperationFailed = 9,
    PreviousOperationDefeatedAny = 10,
    MatchingTargetCount = 11,
}

public enum BattleCardSpatialZone
{
    Core = 0,
    Inner = 1,
    Outer = 2,
    DefenseLine = 3,
}

public enum BattleCardMovementMode
{
    CorewardByDistance = 0,
    OutwardByDistance = 1,
    ToOuterZone = 2,
    ToWorldPoint = 3,
    ToTargetFlank = 4,
}

public enum BattleCardZoneTrigger
{
    AfterDelay = 0,
    OnEnemyEnter = 1,
}

public enum BattleCardCostModifierMode
{
    Add = 0,
    Set = 1,
}

[Serializable]
public sealed class BattleCardTargetFilter
{
    [SerializeField] private CharacterRoleSO requiredRole;
    [SerializeField] private CharacterSO requiredCharacter;
    [SerializeField] private StatusEffectSO requiredStatus;
    [SerializeField] private bool includeDefeated;

    public CharacterRoleSO RequiredRole => requiredRole;
    public CharacterSO RequiredCharacter => requiredCharacter;
    public StatusEffectSO RequiredStatus => requiredStatus;
    public bool IncludeDefeated => includeDefeated;
    public bool IsConfigured => requiredRole != null ||
                                requiredCharacter != null ||
                                requiredStatus != null ||
                                includeDefeated;
}

[Serializable]
public sealed class BattleCardSecondaryTargetDefinition
{
    [SerializeField] private bool enabled;
    [SerializeField] private bool worldPoint;
    [SerializeField] private CharacterTargetFaction targetFaction;
    [SerializeField] private CharacterAttackSubject subject =
        CharacterAttackSubject.Manual;
    [SerializeField] private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField, Min(0)] private int targetCount = 1;
    [SerializeField] private BattleAreaDefinition areaDefinition = new();
    [SerializeField] private BattleCardTargetFilter filter = new();

    public bool Enabled => enabled;
    public bool UsesWorldPoint => enabled && worldPoint;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public int TargetCount => UsesWorldPoint
        ? Mathf.Max(0, targetCount)
        : Mathf.Max(1, targetCount);
    public bool HasAreaDefinition => areaDefinition != null;
    public bool HasFilterDefinition => filter != null;
    public bool HasValidTargetCount => !enabled ||
                                       worldPoint && targetCount >= 0 ||
                                       !worldPoint && targetCount >= 1;
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ?? new BattleAreaDefinition();
    public BattleCardTargetFilter Filter =>
        filter ?? new BattleCardTargetFilter();
}

[Serializable]
public sealed class BattleCardConditionDefinition
{
    [SerializeField] private BattleCardConditionType type;
    [SerializeField] private CharacterNumericComparison comparison =
        CharacterNumericComparison.GreaterThanOrEqual;
    [SerializeField] private float threshold;
    [SerializeField] private CharacterRoleSO role;
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField] private BattleCardSpatialZone zone;

    public BattleCardConditionType Type => type;
    public CharacterNumericComparison Comparison => comparison;
    public float Threshold => float.IsNaN(threshold) ||
                              float.IsInfinity(threshold)
        ? 0f
        : threshold;
    public CharacterRoleSO Role => role;
    public StatusEffectSO StatusEffect => statusEffect;
    public BattleCardSpatialZone Zone => zone;
    public bool IsConfigured => type != BattleCardConditionType.None;
    public bool HasFiniteThreshold =>
        !float.IsNaN(threshold) && !float.IsInfinity(threshold);
}

[Serializable]
public sealed class BattleCardOperationDefinition
{
    [SerializeField] private string operationId;
    [SerializeField] private BattleCardOperationType type;
    [SerializeField] private BattleCardTargetScope targetScope;
    [SerializeField] private CharacterEffectDefinition sharedEffect = new();
    [SerializeField] private BattleCardConditionDefinition condition = new();
    [SerializeField] private CharacterRoleSO requiredRole;
    [SerializeField] private CharacterSO requiredCharacter;
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField] private StatusEffectSO requiredStatus;
    [SerializeField, Min(0)] private int amount;
    [SerializeField] private float ratio = 1f;
    [SerializeField, Min(0)] private int count = 1;
    [SerializeField, Min(0)] private int minimumSelectionCount = 1;
    [SerializeField, Min(0)] private int maximumSelectionCount = 1;
    [SerializeField, Min(0f)] private float duration;
    [SerializeField, Min(0f)] private float delaySeconds;
    [SerializeField, Min(0f)] private float radius = 1.5f;
    [SerializeField, Min(0.1f)] private float statusDuration = 1f;
    [SerializeField, Min(0.1f)] private float statusStacks = 1f;
    [SerializeField] private bool usePreviousChangedCount;
    [SerializeField] private bool oncePerTarget = true;
    [SerializeField] private BattleCardMovementMode movementMode;
    [SerializeField] private BattleCardZoneTrigger zoneTrigger;
    [SerializeField] private BattleCardCostModifierMode costModifierMode;
    [SerializeField] private BattleCardSpatialZone spatialZone;

    public string OperationId => (operationId ?? string.Empty).Trim();
    public BattleCardOperationType Type => type;
    public BattleCardTargetScope TargetScope => targetScope;
    public CharacterEffectDefinition SharedEffect => sharedEffect;
    public BattleCardConditionDefinition Condition =>
        condition ?? new BattleCardConditionDefinition();
    public bool HasSharedEffectDefinition => sharedEffect != null;
    public bool HasConditionDefinition => condition != null;
    public CharacterRoleSO RequiredRole => requiredRole;
    public CharacterSO RequiredCharacter => requiredCharacter;
    public StatusEffectSO StatusEffect => statusEffect;
    public StatusEffectSO RequiredStatus => requiredStatus;
    public int Amount => Mathf.Max(0, amount);
    public float Ratio => float.IsNaN(ratio) || float.IsInfinity(ratio)
        ? 0f
        : Mathf.Max(0f, ratio);
    public int Count => Mathf.Max(0, count);
    public int MinimumSelectionCount => Mathf.Max(
        0,
        Mathf.Min(minimumSelectionCount, MaximumSelectionCount));
    public int MaximumSelectionCount => Mathf.Max(
        0,
        maximumSelectionCount);
    public float Duration => TimePrecision.Normalize(duration);
    public float DelaySeconds => TimePrecision.Normalize(delaySeconds);
    public float Radius => Mathf.Max(0f, radius);
    public float StatusDuration => TimePrecision.Normalize(
        statusDuration,
        0.1f);
    public float StatusStacks => Mathf.Max(0.1f, statusStacks);
    public bool UsePreviousChangedCount => usePreviousChangedCount;
    public bool OncePerTarget => oncePerTarget;
    public BattleCardMovementMode MovementMode => movementMode;
    public BattleCardZoneTrigger ZoneTrigger => zoneTrigger;
    public BattleCardCostModifierMode CostModifierMode => costModifierMode;
    public BattleCardSpatialZone SpatialZone => spatialZone;

    public bool UsesPrimaryTarget =>
        targetScope == BattleCardTargetScope.Primary ||
        targetScope == BattleCardTargetScope.NearbyPrimaryEnemies ||
        targetScope == BattleCardTargetScope.BehindPrimaryEnemy;

    public bool UsesSecondaryTarget =>
        targetScope == BattleCardTargetScope.Secondary;

    public bool UsesDesignatedPoint =>
        targetScope == BattleCardTargetScope.EnemiesAtDesignatedPoint ||
        type == BattleCardOperationType.CreateZone ||
        type == BattleCardOperationType.PullEnemies ||
        type == BattleCardOperationType.Move &&
            movementMode == BattleCardMovementMode.ToWorldPoint;

    public bool RequiresCardSelection =>
        type == BattleCardOperationType.DiscardSelected ||
        type == BattleCardOperationType.ExhaustSelected ||
        type == BattleCardOperationType.ReturnDiscarded;

    public bool HasValidSelectionRange =>
        minimumSelectionCount >= 0 &&
        maximumSelectionCount >= minimumSelectionCount;

    public bool HasValidNumericValues =>
        amount >= 0 &&
        count >= 0 &&
        IsFiniteAtLeast(ratio, 0f) &&
        IsFiniteAtLeast(duration, 0f) &&
        IsFiniteAtLeast(delaySeconds, 0f) &&
        IsFiniteAtLeast(radius, 0f) &&
        IsFiniteAtLeast(statusDuration, 0f) &&
        IsFiniteAtLeast(statusStacks, 0.1f);

    public void Validate()
    {
        operationId = (operationId ?? string.Empty).Trim();
        sharedEffect ??= new CharacterEffectDefinition();
        condition ??= new BattleCardConditionDefinition();
        amount = Mathf.Max(0, amount);
        count = Mathf.Max(0, count);
        minimumSelectionCount = Mathf.Max(0, minimumSelectionCount);
        maximumSelectionCount = Mathf.Max(
            minimumSelectionCount,
            maximumSelectionCount);
        duration = TimePrecision.Normalize(duration);
        delaySeconds = TimePrecision.Normalize(delaySeconds);
        radius = Mathf.Max(0f, radius);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        if (float.IsNaN(ratio) || float.IsInfinity(ratio))
            ratio = 0f;
        ratio = Mathf.Max(0f, ratio);
        if (type == BattleCardOperationType.SharedEffect)
            sharedEffect.Validate();
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) &&
               value >= minimum;
    }
}
