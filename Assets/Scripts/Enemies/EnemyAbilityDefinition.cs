using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyAbilityTrigger
{
    OnSpawn = 0,
    OnCooldown = 1,
    BeforeSelfDamage = 2,
    BeforeAllyDamage = 3,
    OnDeath = 4,
    OnSpawnQueueEvaluation = 5,
    OnTargetPriorityEvaluation = 6
}

public enum EnemyAbilityOperationType
{
    ExecuteEffects = 0,
    ModifySpawnInterval = 1,
    ModifyIncomingDamage = 2,
    ExpandSpawnGroup = 3,
    GrantArmor = 4,
    RedirectDamage = 5,
    ModifyTargetPriority = 6
}

public enum EnemyTargetPriorityMode
{
    Exclude = 0,
    Adjust = 1,
    ForceFocus = 2
}

public enum EnemyAbilityCooldownResetPolicy
{
    OnSuccessfulActivation = 0,
    OnAttempt = 1
}

public enum EnemyAbilityChargeConsumptionPolicy
{
    OnSuccessfulActivation = 0,
    OnAttempt = 1
}

public enum EnemyAbilityTargetFaction
{
    None = 0,
    Self = 1,
    EnemyAllies = 2,
    PlayerCharacters = 3
}

public enum EnemyAbilityTargetSubject
{
    None = 0,
    Self = 1,
    All = 2,
    Random = 3,
    HighestValue = 4,
    LowestValue = 5,
    Adjacent = 6
}

public enum EnemyAbilityTargetMetric
{
    None = 0,
    Health = 1,
    HealthPercentage = 2,
    Shield = 3,
    TotalDamageDealt = 4,
    StackCount = 5
}

public enum EnemyAbilityConditionType
{
    SourceHealth = 0,
    SourceHealthPercentage = 1,
    SourceHasStatus = 2,
    TargetHealth = 3,
    TargetHealthPercentage = 4,
    TargetHasStatus = 5,
    IncomingDamageType = 6,
    HasAlternateTarget = 7,
    TargetTotalDamageDealt = 8
}

public static class EnemyAbilityIds
{
    public const string GuardedHits = "guarded_hits";
    public const string AdjacentHeal = "adjacent_heal";
    public const string DisableHighestDamage =
        "disable_highest_damage";
    public const string ExpandSpawnGroup = "expand_spawn_group";
    public const string InitialArmor = "initial_armor";
    public const string RedirectAdjacentDamage =
        "redirect_adjacent_damage";
    public const string TargetPriorityExclusion =
        "target_priority_exclusion";
}

[Serializable]
public sealed class EnemyAbilityTargetDefinition
{
    [SerializeField]
    private EnemyAbilityTargetFaction faction;
    [SerializeField]
    private EnemyAbilityTargetSubject subject;
    [SerializeField]
    private EnemyAbilityTargetMetric metric;
    [SerializeField, Min(1)]
    private int targetCount = 1;
    [SerializeField, Min(1)]
    private int range = 1;
    [SerializeField]
    private bool includeDiagonals;

    public EnemyAbilityTargetFaction Faction => faction;
    public EnemyAbilityTargetSubject Subject => subject;
    public EnemyAbilityTargetMetric Metric => metric;
    public int TargetCount => targetCount;
    public int Range => range;
    public bool IncludeDiagonals => includeDiagonals;
    public bool HasTarget =>
        faction != EnemyAbilityTargetFaction.None &&
        subject != EnemyAbilityTargetSubject.None;

    internal static EnemyAbilityTargetDefinition CreateRuntimePreset(
        EnemyAbilityTargetFaction targetFaction,
        EnemyAbilityTargetSubject targetSubject,
        EnemyAbilityTargetMetric targetMetric =
            EnemyAbilityTargetMetric.None,
        int count = 1,
        int targetRange = 1,
        bool diagonals = false)
    {
        return new EnemyAbilityTargetDefinition
        {
            faction = targetFaction,
            subject = targetSubject,
            metric = targetMetric,
            targetCount = Mathf.Max(1, count),
            range = Mathf.Max(1, targetRange),
            includeDiagonals = diagonals,
        };
    }

    public void Validate()
    {
        targetCount = Mathf.Max(1, targetCount);
        range = Mathf.Clamp(
            range,
            1,
            DungeonBoardView.MaximumGridSize - 1);
    }
}

[Serializable]
public sealed class EnemyAbilityConditionDefinition
{
    [SerializeField]
    private EnemyAbilityConditionType type;
    [SerializeField]
    private CharacterNumericComparison comparison;
    [SerializeField]
    private float threshold;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private List<StatusEffectSO> statusEffects = new();
    [SerializeField]
    private CharacterStatusSelectionScope statusSelectionScope;
    [SerializeField]
    private CharacterStatusConditionMatchMode statusMatchMode;
    [SerializeField, Min(1)]
    private int statusMatchCount = 1;
    [SerializeField]
    private CharacterAttackDamageType incomingDamageType;
    [SerializeField]
    private bool expected = true;

    public EnemyAbilityConditionType Type => type;
    public CharacterNumericComparison Comparison => comparison;
    public float Threshold => threshold;
    public StatusEffectSO StatusEffect => statusEffect;
    public IReadOnlyList<StatusEffectSO> StatusEffects =>
        statusEffects != null
            ? statusEffects
            : Array.Empty<StatusEffectSO>();
    public CharacterStatusSelection StatusSelection =>
        new(statusEffect, statusEffects);
    public CharacterStatusSelectionScope StatusSelectionScope =>
        statusSelectionScope;
    public CharacterStatusConditionMatchMode StatusMatchMode =>
        statusMatchMode;
    public int StatusMatchCount => statusMatchCount;
    public int RequiredStatusMatchCount => Mathf.Max(1, statusMatchCount);
    public CharacterAttackDamageType IncomingDamageType =>
        incomingDamageType;
    public bool Expected => expected;

    internal static EnemyAbilityConditionDefinition
        CreateIncomingDamagePreset(
            CharacterAttackDamageType damageType)
    {
        return new EnemyAbilityConditionDefinition
        {
            type = EnemyAbilityConditionType.IncomingDamageType,
            incomingDamageType = damageType,
            expected = true,
        };
    }

    internal static EnemyAbilityConditionDefinition
        CreateAlternateTargetPreset()
    {
        return new EnemyAbilityConditionDefinition
        {
            type = EnemyAbilityConditionType.HasAlternateTarget,
            expected = true,
        };
    }

    internal static EnemyAbilityConditionDefinition
        CreatePositiveTargetDamagePreset()
    {
        return new EnemyAbilityConditionDefinition
        {
            type = EnemyAbilityConditionType.TargetTotalDamageDealt,
            comparison = CharacterNumericComparison.GreaterThan,
            threshold = 0f,
            expected = true,
        };
    }

    public void Validate()
    {
        statusEffects ??= new List<StatusEffectSO>();
        if (!Enum.IsDefined(
                typeof(CharacterStatusSelectionScope),
                statusSelectionScope))
        {
            statusSelectionScope =
                CharacterStatusSelectionScope.SelectedStatuses;
        }
        if (!Enum.IsDefined(
                typeof(CharacterStatusConditionMatchMode),
                statusMatchMode))
        {
            statusMatchMode = CharacterStatusConditionMatchMode.Any;
        }
        statusMatchCount = Mathf.Max(1, statusMatchCount);
        if (float.IsNaN(threshold) || float.IsInfinity(threshold))
            threshold = 0f;
    }
}

[Serializable]
public sealed class EnemyAbilityOperationDefinition
{
    [SerializeField]
    private EnemyAbilityOperationType type;
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();
    [SerializeField]
    private float multiplier = 1f;
    [SerializeField, Min(0)]
    private int amount = 1;
    [SerializeField, Min(0)]
    private int count = 1;
    [SerializeField, Min(1)]
    private int range = 1;
    [SerializeField]
    private bool includeDiagonals;
    [SerializeField]
    private bool enabled = true;
    [SerializeField]
    private EnemyTargetPriorityMode targetPriorityMode;
    [SerializeField]
    private int targetPriorityAdjustment;

    public EnemyAbilityOperationType Type => type;
    public IReadOnlyList<CharacterEffectDefinition> Effects =>
        effects != null
            ? effects
            : Array.Empty<CharacterEffectDefinition>();
    public float Multiplier => multiplier;
    public int Amount => amount;
    public int Count => count;
    public int Range => range;
    public bool IncludeDiagonals => includeDiagonals;
    public bool Enabled => enabled;
    public EnemyTargetPriorityMode TargetPriorityMode =>
        targetPriorityMode;
    public int TargetPriorityAdjustment => targetPriorityAdjustment;

    internal static EnemyAbilityOperationDefinition CreateRuntimePreset(
        EnemyAbilityOperationType operationType,
        IReadOnlyList<CharacterEffectDefinition> effectDefinitions = null,
        float valueMultiplier = 0f,
        int fixedAmount = 0,
        int additionalCount = 1,
        int operationRange = 1,
        bool diagonals = false,
        EnemyTargetPriorityMode priorityMode =
            EnemyTargetPriorityMode.Exclude,
        int priorityAdjustment = 0)
    {
        List<CharacterEffectDefinition> copiedEffects = new();
        if (effectDefinitions != null)
        {
            foreach (CharacterEffectDefinition effect in effectDefinitions)
            {
                if (effect != null)
                    copiedEffects.Add(effect);
            }
        }

        return new EnemyAbilityOperationDefinition
        {
            type = operationType,
            effects = copiedEffects,
            multiplier = Mathf.Max(0f, valueMultiplier),
            amount = Mathf.Max(0, fixedAmount),
            count = Mathf.Max(0, additionalCount),
            range = Mathf.Max(1, operationRange),
            includeDiagonals = diagonals,
            enabled = true,
            targetPriorityMode = priorityMode,
            targetPriorityAdjustment = priorityAdjustment,
        };
    }

    public void Validate()
    {
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();

        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            multiplier = 1f;
        multiplier = Mathf.Max(0f, multiplier);
        amount = Mathf.Max(0, amount);
        count = Mathf.Max(0, count);
        range = Mathf.Clamp(
            range,
            1,
            DungeonBoardView.MaximumGridSize - 1);
        if (!Enum.IsDefined(
                typeof(EnemyTargetPriorityMode),
                targetPriorityMode))
        {
            targetPriorityMode = EnemyTargetPriorityMode.Exclude;
        }
    }
}

[Serializable]
public sealed class EnemyAbilityDefinition : IBattleAbilityDefinition
{
    [SerializeField]
    private string abilityId;
    [SerializeField]
    private string nameLocalizationKey;
    [SerializeField]
    private string descriptionLocalizationKey;
    [SerializeField]
    private string fallbackName;
    [SerializeField, TextArea(2, 6)]
    private string fallbackDescription;
    [SerializeField]
    private EnemyAbilityTrigger trigger;
    [SerializeField]
    private int priority;
    [SerializeField, Min(0f)]
    private float cooldown;
    [SerializeField]
    private EnemyAbilityCooldownResetPolicy cooldownResetPolicy;
    [SerializeField]
    private bool pauseCooldownWhileDisabled = true;
    [SerializeField, Min(0)]
    private int initialCharges;
    [SerializeField]
    private EnemyAbilityChargeConsumptionPolicy chargeConsumptionPolicy;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<EnemyAbilityConditionDefinition> conditions = new();
    [SerializeField]
    private EnemyAbilityTargetDefinition target = new();
    [SerializeField]
    private List<EnemyAbilityOperationDefinition> operations = new();

    public string AbilityId => abilityId ?? string.Empty;
    public string NameLocalizationKey => nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public string FallbackDescription => fallbackDescription ?? string.Empty;
    public EnemyAbilityTrigger Trigger => trigger;
    public int Priority => priority;
    public float Cooldown =>
        TimePrecision.FloorToTenth(Mathf.Max(0f, cooldown));
    public EnemyAbilityCooldownResetPolicy CooldownResetPolicy =>
        cooldownResetPolicy;
    public bool PauseCooldownWhileDisabled => pauseCooldownWhileDisabled;
    public int InitialCharges => Mathf.Max(0, initialCharges);
    public bool HasUnlimitedCharges => InitialCharges == 0;
    public EnemyAbilityChargeConsumptionPolicy ChargeConsumptionPolicy =>
        chargeConsumptionPolicy;
    public CharacterConditionMatchMode ConditionMatchMode =>
        conditionMatchMode;
    public IReadOnlyList<EnemyAbilityConditionDefinition> Conditions =>
        conditions != null
            ? conditions
            : Array.Empty<EnemyAbilityConditionDefinition>();
    public EnemyAbilityTargetDefinition Target => target;
    public IReadOnlyList<EnemyAbilityOperationDefinition> Operations =>
        operations != null
            ? operations
            : Array.Empty<EnemyAbilityOperationDefinition>();
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => 1;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.EnemyAbility;
    public BattleAbilityTargeting Targeting =>
        BattleAbilityTargeting.FromEnemy(Target);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        EnumerateBattleEffects();
    public bool UsesLegacyEffectStorage => false;
    public bool HasExecutableContent => Operations.Count > 0;

    private IEnumerable<IBattleEffectDefinition> EnumerateBattleEffects()
    {
        foreach (EnemyAbilityOperationDefinition operation in Operations)
        {
            if (operation == null)
                continue;

            foreach (CharacterEffectDefinition effect in operation.Effects)
            {
                if (effect != null)
                    yield return effect;
            }
        }
    }

    internal static EnemyAbilityDefinition CreateRuntimePreset(
        string id,
        string name,
        string description,
        EnemyAbilityTrigger abilityTrigger,
        EnemyAbilityTargetDefinition targetDefinition,
        IReadOnlyList<EnemyAbilityOperationDefinition>
            operationDefinitions,
        float abilityCooldown = 0f,
        int charges = 0,
        CharacterConditionMatchMode matchMode =
            CharacterConditionMatchMode.All,
        IReadOnlyList<EnemyAbilityConditionDefinition>
            conditionDefinitions = null)
    {
        List<EnemyAbilityConditionDefinition> copiedConditions = new();
        if (conditionDefinitions != null)
        {
            foreach (EnemyAbilityConditionDefinition condition in
                     conditionDefinitions)
            {
                if (condition != null)
                    copiedConditions.Add(condition);
            }
        }

        List<EnemyAbilityOperationDefinition> copiedOperations = new();
        if (operationDefinitions != null)
        {
            foreach (EnemyAbilityOperationDefinition operation in
                     operationDefinitions)
            {
                if (operation != null)
                    copiedOperations.Add(operation);
            }
        }

        EnemyAbilityDefinition definition = new()
        {
            abilityId = id ?? string.Empty,
            fallbackName = name ?? string.Empty,
            fallbackDescription = description ?? string.Empty,
            trigger = abilityTrigger,
            cooldown = Mathf.Max(0f, abilityCooldown),
            cooldownResetPolicy =
                EnemyAbilityCooldownResetPolicy.OnSuccessfulActivation,
            pauseCooldownWhileDisabled = true,
            initialCharges = Mathf.Max(0, charges),
            chargeConsumptionPolicy =
                EnemyAbilityChargeConsumptionPolicy.OnSuccessfulActivation,
            conditionMatchMode = matchMode,
            conditions = copiedConditions,
            target = targetDefinition ??
                     new EnemyAbilityTargetDefinition(),
            operations = copiedOperations,
        };
        definition.Validate();
        return definition;
    }

    public void Validate()
    {
        abilityId = (abilityId ?? string.Empty).Trim();
        nameLocalizationKey = (nameLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        fallbackDescription = fallbackDescription ?? string.Empty;
        cooldown = TimePrecision.FloorToTenth(
            Mathf.Max(0f, cooldown));
        initialCharges = Mathf.Max(0, initialCharges);

        conditions ??= new List<EnemyAbilityConditionDefinition>();
        foreach (EnemyAbilityConditionDefinition condition in conditions)
            condition?.Validate();

        target ??= new EnemyAbilityTargetDefinition();
        target.Validate();

        operations ??= new List<EnemyAbilityOperationDefinition>();
        foreach (EnemyAbilityOperationDefinition operation in operations)
            operation?.Validate();
    }
}

internal sealed class EnemyAbilityRuntimeState
{
    private int _remainingCharges;
    private float _cooldownRemaining;

    public EnemyAbilityDefinition Definition { get; }
    public float CooldownRemaining =>
        TimePrecision.FloorToTenth(_cooldownRemaining);
    public int RemainingCharges => Definition.HasUnlimitedCharges
        ? 0
        : _remainingCharges;
    public bool HasRemainingCharge =>
        Definition.HasUnlimitedCharges || _remainingCharges > 0;
    public bool CanActivate => HasRemainingCharge;

    public EnemyAbilityRuntimeState(EnemyAbilityDefinition definition)
    {
        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        _remainingCharges = Definition.InitialCharges;
        ResetCooldown();
    }

    public bool TickCooldown(float deltaTime, bool sourceDisabled)
    {
        if (Definition.Trigger != EnemyAbilityTrigger.OnCooldown ||
            Definition.Cooldown <= 0f ||
            deltaTime <= 0f ||
            !HasRemainingCharge)
        {
            return false;
        }

        if (sourceDisabled && Definition.PauseCooldownWhileDisabled)
            return false;

        _cooldownRemaining = Mathf.Max(
            0f,
            _cooldownRemaining - deltaTime);
        return _cooldownRemaining <= 0f && !sourceDisabled;
    }

    public void RecordActivation(bool attempted, bool succeeded)
    {
        if (!attempted)
            return;

        bool consumeCharge =
            Definition.ChargeConsumptionPolicy ==
                EnemyAbilityChargeConsumptionPolicy.OnAttempt ||
            succeeded;
        if (consumeCharge &&
            !Definition.HasUnlimitedCharges &&
            _remainingCharges > 0)
        {
            _remainingCharges--;
        }

        bool resetCooldown =
            Definition.CooldownResetPolicy ==
                EnemyAbilityCooldownResetPolicy.OnAttempt ||
            succeeded;
        if (resetCooldown)
            ResetCooldown();
    }

    private void ResetCooldown()
    {
        _cooldownRemaining = Definition.Trigger ==
                             EnemyAbilityTrigger.OnCooldown
            ? Definition.Cooldown
            : 0f;
    }
}

internal static class EnemyAbilityConditionEvaluator
{
    public static bool MatchesSourceOnly(
        EnemyAbilityDefinition ability,
        EnemyRuntime source,
        bool hasAlternateTarget)
    {
        if (ability == null || source == null)
            return false;

        IReadOnlyList<EnemyAbilityConditionDefinition> conditions =
            ability.Conditions;
        if (conditions == null || conditions.Count == 0)
            return true;

        bool matchAny =
            ability.ConditionMatchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (EnemyAbilityConditionDefinition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            bool matched = condition.Type switch
            {
                EnemyAbilityConditionType.SourceHealth =>
                    Compare(
                        source.Health,
                        condition.Comparison,
                        condition.Threshold),
                EnemyAbilityConditionType.SourceHealthPercentage =>
                    Compare(
                        source.MaxHealth > 0
                            ? source.Health * 100f / source.MaxHealth
                            : 0f,
                        condition.Comparison,
                        condition.Threshold),
                EnemyAbilityConditionType.SourceHasStatus =>
                    MatchesStatusSelection(
                        condition,
                        source.HasStatusEffect,
                        source.GetActiveStatusEffects()) ==
                    condition.Expected,
                EnemyAbilityConditionType.HasAlternateTarget =>
                    hasAlternateTarget == condition.Expected,
                _ => false
            };
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    internal static bool MatchesStatusSelection(
        EnemyAbilityConditionDefinition condition,
        Func<StatusEffectSO, bool> hasStatus,
        IReadOnlyList<BattleStatusSnapshot> activeStatuses = null)
    {
        if (condition == null || hasStatus == null)
            return false;

        if (condition.StatusSelectionScope !=
            CharacterStatusSelectionScope.SelectedStatuses)
        {
            return MatchesStatusScope(
                condition,
                activeStatuses);
        }

        CharacterStatusSelection selection = condition.StatusSelection;
        int selectedCount = 0;
        int matchedCount = 0;
        for (int index = 0; index < selection.Count; index++)
        {
            StatusEffectSO status = selection.GetStatus(index);
            if (status == null || ContainsEarlierStatus(
                    selection,
                    status,
                    index))
            {
                continue;
            }

            selectedCount++;
            if (hasStatus(status))
                matchedCount++;
        }

        if (selectedCount == 0)
            return false;

        return MatchesStatusCount(
            condition,
            selectedCount,
            matchedCount);
    }

    private static bool MatchesStatusScope(
        EnemyAbilityConditionDefinition condition,
        IReadOnlyList<BattleStatusSnapshot> activeStatuses)
    {
        if (activeStatuses == null)
            return false;

        StatusEffectAlignment expectedAlignment =
            condition.StatusSelectionScope switch
            {
                CharacterStatusSelectionScope.AllBuffs =>
                    StatusEffectAlignment.Buff,
                CharacterStatusSelectionScope.AllDebuffs =>
                    StatusEffectAlignment.Debuff,
                _ => (StatusEffectAlignment)(-1)
            };
        if (!Enum.IsDefined(
                typeof(StatusEffectAlignment),
                expectedAlignment))
        {
            return false;
        }

        int matchedCount = 0;
        for (int index = 0; index < activeStatuses.Count; index++)
        {
            BattleStatusSnapshot snapshot = activeStatuses[index];
            StatusEffectSO status = snapshot.Definition;
            if (!snapshot.IsValid ||
                status.Alignment != expectedAlignment ||
                ContainsEarlierStatus(
                    activeStatuses,
                    status,
                    index))
            {
                continue;
            }

            matchedCount++;
        }

        if (matchedCount == 0)
            return false;

        return MatchesStatusCount(
            condition,
            matchedCount,
            matchedCount);
    }

    private static bool MatchesStatusCount(
        EnemyAbilityConditionDefinition condition,
        int selectedCount,
        int matchedCount)
    {
        return condition.StatusMatchMode switch
        {
            CharacterStatusConditionMatchMode.Any =>
                matchedCount >= 1,
            CharacterStatusConditionMatchMode.All =>
                matchedCount == selectedCount,
            CharacterStatusConditionMatchMode.AtLeastCount =>
                matchedCount >= condition.RequiredStatusMatchCount,
            _ => false
        };
    }

    private static bool ContainsEarlierStatus(
        IReadOnlyList<BattleStatusSnapshot> statuses,
        StatusEffectSO status,
        int index)
    {
        for (int previous = 0; previous < index; previous++)
        {
            if (CharacterStatusSelection.IsSameStatus(
                    statuses[previous].Definition,
                    status))
            {
                return true;
            }
        }

        return false;
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

    private static bool Compare(
        float value,
        CharacterNumericComparison comparison,
        float threshold)
    {
        return comparison switch
        {
            CharacterNumericComparison.GreaterThanOrEqual =>
                value >= threshold,
            CharacterNumericComparison.LessThanOrEqual =>
                value <= threshold,
            CharacterNumericComparison.GreaterThan =>
                value > threshold,
            CharacterNumericComparison.LessThan =>
                value < threshold,
            CharacterNumericComparison.Equal =>
                Mathf.Approximately(value, threshold),
            CharacterNumericComparison.NotEqual =>
                !Mathf.Approximately(value, threshold),
            _ => false
        };
    }
}
