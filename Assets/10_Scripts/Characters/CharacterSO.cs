using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum CharacterAttackSectionType
{
    Linkage = 0,
    Subject = 1,
    Ability = 2,
    LegacyDamageAmount = 3,
    Condition = 4
}

public enum CharacterActionLinkage
{
    PreviousAttackSucceeded = 0,
    SimultaneousWithPreviousAttack = 1,
    None = 2,
    PreviousAttackFailed = 3
}

public enum CharacterAttackSubject
{
    Random,
    All,
    HighestValue,
    LowestValue,
    Self,
    AllExceptSelf,
    RandomExceptSelf,
    None,
    Manual
}

public enum CharacterAttackTargetRetentionMode
{
    ReselectEachAttack = 0,
    LockUntilInvalid = 1
}

public enum CharacterPassiveAttackTargetRelation
{
    Any = 0,
    SameAsPreviousAttack = 1,
    DifferentFromPreviousAttack = 2
}

public enum CharacterTargetFaction
{
    Enemy,
    Ally
}

public enum CharacterAttackSubjectMetric
{
    Health,
    StackCount,
    AttackPower,
    AttackSpeed,
    Shield
}

public enum CharacterNumericConditionMetric
{
    Health = 0,
    HealthPercentage = 1,
    StackCount = 2,
    AttackPower = 3,
    AttackSpeed = 4,
    Shield = 5,
    StatusStackCount = 6,
    MaximumHealth = 7,
    HealthPerformancePercentage = 8,
    HealthPerformanceCap = 9
}

public enum CharacterConditionType
{
    Numeric,
    HasStatus
}

public enum CharacterConditionTarget
{
    ActionTarget = 0,
    Source = 1
}

public enum CharacterNumericComparison
{
    GreaterThanOrEqual,
    LessThanOrEqual,
    GreaterThan,
    LessThan,
    Equal,
    NotEqual
}

public enum CharacterConditionMatchMode
{
    All,
    Any
}

public enum CharacterStatusConditionMatchMode
{
    Any = 0,
    All = 1,
    AtLeastCount = 2
}

public enum CharacterStatusSelectionScope
{
    SelectedStatuses = 0,
    AllBuffs = 1,
    AllDebuffs = 2
}

public readonly struct CharacterStatusSelection
{
    private readonly IReadOnlyList<StatusEffectSO> _statusEffects;

    public StatusEffectSO LegacyStatusEffect { get; }
    public IReadOnlyList<StatusEffectSO> StatusEffects =>
        _statusEffects ?? Array.Empty<StatusEffectSO>();
    public bool UsesStatusList =>
        _statusEffects != null && _statusEffects.Count > 0;
    public int Count =>
        UsesStatusList
            ? _statusEffects.Count
            : LegacyStatusEffect != null
                ? 1
                : 0;

    public CharacterStatusSelection(
        StatusEffectSO legacyStatusEffect,
        IReadOnlyList<StatusEffectSO> statusEffects = null)
    {
        LegacyStatusEffect = legacyStatusEffect;
        _statusEffects = statusEffects;
    }

    public StatusEffectSO GetStatus(int index)
    {
        if (UsesStatusList)
        {
            return index >= 0 && index < _statusEffects.Count
                ? _statusEffects[index]
                : null;
        }

        return index == 0 ? LegacyStatusEffect : null;
    }

    public bool Contains(StatusEffectSO definition)
    {
        if (definition == null)
            return false;

        for (int index = 0; index < Count; index++)
        {
            if (IsSameStatus(GetStatus(index), definition))
                return true;
        }

        return false;
    }

    public static bool IsSameStatus(
        StatusEffectSO left,
        StatusEffectSO right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null)
            return false;

        return !string.IsNullOrWhiteSpace(left.StatusId) &&
               string.Equals(
                   left.StatusId,
                   right.StatusId,
                   StringComparison.Ordinal);
    }
}

public interface ICharacterConditionalActionDefinition
{
    bool HasLinkageSection { get; }
    CharacterActionLinkage Linkage { get; }
    bool HasConditionSection { get; }
    CharacterConditionMatchMode ConditionMatchMode { get; }
    IReadOnlyList<CharacterNumericCondition> NumericConditions { get; }
}

public enum CharacterAttackDamageType
{
    Physical,
    Magical,
    Fixed,
    StatusEffect,
    StatusRemoval
}

public enum CharacterEffectType
{
    Damage = 0,
    ApplyStatus = 1,
    RemoveStatus = 2,
    GainResource = 3,
    SpendResource = 4,
    Heal = 5,
    SpendHealth = 6,
    Shield = 7,
    CardDraw = 8
}

public enum CharacterEffectTargetMode
{
    InheritAction = 0,
    Source = 1,
    FreshSelection = 2,
    Objective = 3
}

public enum CharacterEffectPreconditionFailurePolicy
{
    AbortAction = 0,
    SkipEffect = 1
}

public enum CharacterEffectFailurePolicy
{
    Continue = 0,
    StopRemainingEffects = 1
}

public enum CharacterDamageAmountMode
{
    Ratio,
    Fixed
}

public enum CharacterStatusRemovalTarget
{
    Single = 0,
    Random = 1,
    All = 2,
    Buff = 3,
    Debuff = 4
}

public enum CharacterStatusRemovalPickMode
{
    AllMatches = 0,
    RandomCount = 1
}

public readonly struct CharacterStatusRemovalSelection
{
    private readonly IReadOnlyList<StatusEffectSO> _statusEffects;

    public CharacterStatusRemovalTarget Target { get; }
    public CharacterStatusRemovalPickMode PickMode { get; }
    public int PickCount { get; }
    public StatusEffectSO LegacyStatusEffect { get; }
    public IReadOnlyList<StatusEffectSO> StatusEffects =>
        _statusEffects ?? Array.Empty<StatusEffectSO>();
    public bool UsesStatusList =>
        _statusEffects != null && _statusEffects.Count > 0;

    public CharacterStatusRemovalSelection(
        CharacterStatusRemovalTarget target,
        StatusEffectSO legacyStatusEffect,
        IReadOnlyList<StatusEffectSO> statusEffects = null,
        CharacterStatusRemovalPickMode pickMode =
            CharacterStatusRemovalPickMode.AllMatches,
        int pickCount = 1)
    {
        Target = target;
        PickMode = target == CharacterStatusRemovalTarget.Random
            ? CharacterStatusRemovalPickMode.RandomCount
            : pickMode;
        PickCount = Math.Max(1, pickCount);
        LegacyStatusEffect = legacyStatusEffect;
        _statusEffects = statusEffects;
    }

    public bool UsesRandomCount =>
        PickMode == CharacterStatusRemovalPickMode.RandomCount;

    public int ExplicitStatusCount =>
        UsesStatusList
            ? _statusEffects.Count
            : LegacyStatusEffect != null
                ? 1
                : 0;

    public StatusEffectSO GetExplicitStatus(int index)
    {
        if (UsesStatusList)
        {
            return index >= 0 && index < _statusEffects.Count
                ? _statusEffects[index]
                : null;
        }

        return index == 0 ? LegacyStatusEffect : null;
    }

    public bool HasExplicitStatus
    {
        get
        {
            for (int index = 0; index < ExplicitStatusCount; index++)
            {
                if (GetExplicitStatus(index) != null)
                    return true;
            }

            return false;
        }
    }

    public bool MatchesStatus(StatusEffectSO definition)
    {
        if (definition == null)
            return false;

        switch (Target)
        {
            case CharacterStatusRemovalTarget.Single:
                if (!definition.Removable)
                    return false;

                for (int index = 0; index < ExplicitStatusCount; index++)
                {
                    if (IsSameStatus(
                            GetExplicitStatus(index),
                            definition))
                    {
                        return true;
                    }
                }

                return false;

            case CharacterStatusRemovalTarget.Random:
                return definition.IncludedInRandomRemoval;

            case CharacterStatusRemovalTarget.All:
                return definition.IncludedInAllRemoval;

            case CharacterStatusRemovalTarget.Buff:
                return definition.Alignment == StatusEffectAlignment.Buff &&
                       definition.IncludedInAllRemoval;

            case CharacterStatusRemovalTarget.Debuff:
                return definition.Alignment == StatusEffectAlignment.Debuff &&
                       definition.IncludedInAllRemoval;

            default:
                return false;
        }
    }

    private static bool IsSameStatus(
        StatusEffectSO left,
        StatusEffectSO right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null)
            return false;

        return !string.IsNullOrWhiteSpace(left.StatusId) &&
               string.Equals(
                   left.StatusId,
                   right.StatusId,
                   StringComparison.Ordinal);
    }
}

public static class CharacterStatusRemovalPick
{
    public static void Normalize(
        ref CharacterStatusRemovalPickMode mode,
        ref int count)
    {
        if (!Enum.IsDefined(
                typeof(CharacterStatusRemovalPickMode),
                mode))
        {
            mode = CharacterStatusRemovalPickMode.AllMatches;
        }

        count = Mathf.Max(1, count);
    }

    public static int SelectInPlace<T>(
        List<T> candidates,
        CharacterStatusRemovalSelection selection)
    {
        if (candidates == null || candidates.Count == 0)
            return 0;
        if (!selection.UsesRandomCount)
            return candidates.Count;

        int selectedCount = Mathf.Min(
            selection.PickCount,
            candidates.Count);
        for (int index = 0; index < selectedCount; index++)
        {
            int randomIndex = UnityEngine.Random.Range(
                index,
                candidates.Count);
            if (randomIndex == index)
                continue;

            (candidates[index], candidates[randomIndex]) =
                (candidates[randomIndex], candidates[index]);
        }

        return selectedCount;
    }
}

public enum CharacterStatusRemovalAmountMode
{
    FixedStacks = 0,
    CurrentStacksRatio = 1
}

public readonly struct CharacterStatusRemovalAmount
{
    public CharacterStatusRemovalAmountMode Mode { get; }
    public int FixedStacks { get; }
    public float CurrentStacksRatio { get; }

    public CharacterStatusRemovalAmount(
        CharacterStatusRemovalAmountMode mode,
        int fixedStacks,
        float currentStacksRatio)
    {
        Mode = mode;
        FixedStacks = Mathf.Max(0, fixedStacks);
        CurrentStacksRatio =
            float.IsNaN(currentStacksRatio) ||
            float.IsInfinity(currentStacksRatio)
                ? 0f
                : currentStacksRatio;
    }

    public static CharacterStatusRemovalAmount Fixed(int stacks)
    {
        return new CharacterStatusRemovalAmount(
            CharacterStatusRemovalAmountMode.FixedStacks,
            stacks,
            0f);
    }

    public static CharacterStatusRemovalAmount Ratio(float ratio)
    {
        return new CharacterStatusRemovalAmount(
            CharacterStatusRemovalAmountMode.CurrentStacksRatio,
            0,
            ratio);
    }

    public CharacterStatusRemovalAmount Multiply(int multiplier)
    {
        multiplier = Mathf.Max(1, multiplier);
        if (Mode == CharacterStatusRemovalAmountMode.CurrentStacksRatio)
            return Ratio(CurrentStacksRatio * multiplier);

        if (FixedStacks == 0)
            return this;
        long scaled = (long)FixedStacks * multiplier;
        return Fixed(
            scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled);
    }

    public int Resolve(int currentStacks)
    {
        currentStacks = Mathf.Max(0, currentStacks);
        if (currentStacks == 0)
            return 0;

        if (Mode == CharacterStatusRemovalAmountMode.CurrentStacksRatio)
        {
            if (CurrentStacksRatio <= 0f)
                return 0;

            float ratio = Mathf.Min(1f, CurrentStacksRatio);
            return Mathf.Clamp(
                Mathf.CeilToInt(currentStacks * ratio),
                1,
                currentStacks);
        }

        return FixedStacks == 0
            ? currentStacks
            : Mathf.Min(FixedStacks, currentStacks);
    }

    public static void Normalize(
        ref CharacterStatusRemovalAmountMode mode,
        ref int fixedStacks,
        ref float currentStacksRatio)
    {
        if (!Enum.IsDefined(
                typeof(CharacterStatusRemovalAmountMode),
                mode))
        {
            mode = CharacterStatusRemovalAmountMode.FixedStacks;
        }

        fixedStacks = Mathf.Max(0, fixedStacks);
        currentStacksRatio =
            float.IsNaN(currentStacksRatio) ||
            float.IsInfinity(currentStacksRatio)
                ? 0.5f
                : Mathf.Clamp(currentStacksRatio, 0.01f, 1f);
    }
}

public enum CharacterPassiveSectionType
{
    Linkage = 0,
    Ability = 1,
    Subject = 2,
    Condition = 3,
    SelfStatusCost = 4,
    StatusContribution = 5,
    StatModifier = 6
}

public static class CharacterPassiveStatModifierRules
{
    public static bool IsSupportedCharacterStat(
        StatusEffectStatType statType)
    {
        return statType == StatusEffectStatType.AttackPower ||
               statType == StatusEffectStatType.AttackSpeed;
    }
}

[Serializable]
public sealed class CharacterPassiveStatModifierDefinition
{
    [SerializeField]
    private StatusEffectStatType statType;
    [SerializeField]
    private StatusEffectStatModifierMode mode;
    [SerializeField]
    private float baseValue;
    [SerializeField]
    private float dungeonStageProgressScale;

    public StatusEffectStatType StatType => statType;
    public StatusEffectStatModifierMode Mode => mode;
    public float BaseValue => baseValue;
    public float DungeonStageProgressScale => dungeonStageProgressScale;

    public float ResolveValue(float dungeonStageProgress)
    {
        float progress = float.IsNaN(dungeonStageProgress) ||
                         float.IsInfinity(dungeonStageProgress)
            ? 0f
            : Mathf.Max(0f, dungeonStageProgress);
        float resolved = baseValue + progress * dungeonStageProgressScale;
        return float.IsNaN(resolved) || float.IsInfinity(resolved)
            ? baseValue
            : resolved;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(StatusEffectStatType), statType))
            statType = StatusEffectStatType.AttackPower;
        if (!Enum.IsDefined(typeof(StatusEffectStatModifierMode), mode))
            mode = StatusEffectStatModifierMode.Flat;
        if (statType == StatusEffectStatType.TargetPriority)
            mode = StatusEffectStatModifierMode.Flat;
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            baseValue = 0f;
        if (float.IsNaN(dungeonStageProgressScale) ||
            float.IsInfinity(dungeonStageProgressScale))
        {
            dungeonStageProgressScale = 0f;
        }
        if (mode == StatusEffectStatModifierMode.MultiplicativeRatio)
            baseValue = Mathf.Max(-1f, baseValue);
    }
}

[Serializable]
public sealed class CharacterStatusStatContributionMultiplier
{
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private StatusEffectStatType statType;
    [SerializeField, Min(0f)]
    private float multiplier = 1f;
    [SerializeField, Min(0f)]
    private float dungeonStageProgressScale;

    public StatusEffectSO StatusEffect => statusEffect;
    public StatusEffectStatType StatType => statType;
    public float Multiplier => multiplier;
    public float DungeonStageProgressScale => dungeonStageProgressScale;

    public float ResolveMultiplier(float dungeonStageProgress)
    {
        float progress = float.IsNaN(dungeonStageProgress) ||
                         float.IsInfinity(dungeonStageProgress)
            ? 0f
            : Mathf.Max(0f, dungeonStageProgress);
        float resolved = multiplier + progress * dungeonStageProgressScale;
        return float.IsNaN(resolved) || float.IsInfinity(resolved)
            ? Mathf.Max(0f, multiplier)
            : Mathf.Max(0f, resolved);
    }

    public void Validate()
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            multiplier = 1f;

        if (float.IsNaN(dungeonStageProgressScale) ||
            float.IsInfinity(dungeonStageProgressScale))
        {
            dungeonStageProgressScale = 0f;
        }

        multiplier = Mathf.Max(0f, multiplier);
        dungeonStageProgressScale =
            Mathf.Max(0f, dungeonStageProgressScale);
    }
}

public enum CharacterPassiveTrigger
{
    OnAttack = 0,
    OnStatusAcquired = 1,
    OnCooldown = 2,
    OnKill = 3,
    OnAttackTargetSelected = 4
}

public enum CharacterPassiveMotionMode
{
    PlayPassiveMotion = 0,
    None = 1
}

public enum CharacterPassiveKillSource
{
    Self,
    Other,
    SpecificCharacter,
    All
}

public enum CharacterPassiveStatusTarget
{
    Enemy,
    Ally,
    All,
    Self
}

public enum CharacterSkillSectionType
{
    Cost = 0,
    Linkage = 1,
    Subject = 2,
    Ability = 3,
    Condition = 4
}

public enum CharacterSkillExecutionPolicy
{
    FirstSuccessful = 0,
    SequenceAll = 1
}

public enum CharacterDungeonUpgradeType
{
    AttackPower,
    Speed,
    PassiveDamage,
    AttackDamage,
    SkillDamage,
    SkillCostReduction
}

public enum CharacterUpgradeLocalizationPreset
{
    Automatic = 0,
    Generic = 1,
    AttackPower = 2,
    AttackSpeed = 3,
    SkillPower = 4,
    SkillCost = 5,
    Custom = 100,
}

public enum CharacterCumulativeUpgradeModifierType
{
    AttackPower = 0,
    MaximumHealth = 1,
    AttackCooldown = 2,
    PassiveDamage = 3,
    AttackDamage = 4,
    SkillDamage = 5,
    SkillCostReduction = 6,
    HealthPerformanceCap = 7
}

public enum CharacterModifierTargetScope
{
    Character = 0,
    ActionKind = 1,
    Action = 2,
    Effect = 3,
}

public enum CharacterModifierStat
{
    MaximumHealth = 0,
    AttackPower = 1,
    AttackCooldown = 2,
    Damage = 3,
    EffectAmount = 4,
    StatusDuration = 5,
    StatusStacks = 6,
    SkillCost = 7,
    HealthPerformanceCap = 8,
}

public enum CharacterModifierOperation
{
    AddFlat = 0,
    AddPercent = 1,
    Multiply = 2,
}

public enum CharacterModifierLifetimeScope
{
    Permanent = 0,
    Dungeon = 1,
    Battle = 2,
}

[Serializable]
public sealed class CharacterModifierTarget
{
    [SerializeField]
    private CharacterModifierTargetScope scope;
    [SerializeField]
    private CharacterActionKind actionKind;
    [SerializeField]
    private string actionId;
    [SerializeField]
    private string effectId;

    public CharacterModifierTargetScope Scope => scope;
    public CharacterActionKind ActionKind => actionKind;
    public string ActionId => actionId ?? string.Empty;
    public string EffectId => effectId ?? string.Empty;

    public CharacterModifierTarget()
    {
    }

    public CharacterModifierTarget(
        CharacterModifierTargetScope targetScope,
        CharacterActionKind targetActionKind = default,
        string targetActionId = null,
        string targetEffectId = null)
    {
        scope = targetScope;
        actionKind = targetActionKind;
        actionId = targetActionId ?? string.Empty;
        effectId = targetEffectId ?? string.Empty;
        Validate();
    }

    public bool Matches(
        CharacterActionKind candidateKind,
        string candidateActionId,
        string candidateEffectId)
    {
        if (scope == CharacterModifierTargetScope.Character)
            return true;
        if (actionKind != candidateKind)
            return false;
        if (scope == CharacterModifierTargetScope.ActionKind)
            return true;
        if (!string.Equals(
                ActionId,
                candidateActionId ?? string.Empty,
                StringComparison.Ordinal))
        {
            return false;
        }
        return scope != CharacterModifierTargetScope.Effect ||
               string.Equals(
                   EffectId,
                   candidateEffectId ?? string.Empty,
                   StringComparison.Ordinal);
    }

    public void Validate()
    {
        actionId = (actionId ?? string.Empty).Trim();
        effectId = (effectId ?? string.Empty).Trim();
        if (scope == CharacterModifierTargetScope.Character)
        {
            actionId = string.Empty;
            effectId = string.Empty;
        }
        else if (scope == CharacterModifierTargetScope.ActionKind)
        {
            actionId = string.Empty;
            effectId = string.Empty;
        }
        else if (scope == CharacterModifierTargetScope.Action)
        {
            effectId = string.Empty;
        }
    }
}

[Serializable]
public sealed class CharacterModifierModule
{
    [SerializeField]
    private string moduleId;
    [SerializeField]
    private CharacterModifierTarget target = new();
    [SerializeField]
    private CharacterModifierStat stat;
    [SerializeField]
    private CharacterModifierOperation operation;
    [SerializeField]
    private float valuePerStack;

    public string ModuleId => moduleId ?? string.Empty;
    public CharacterModifierTarget Target => target;
    public CharacterModifierStat Stat => stat;
    public CharacterModifierOperation Operation => operation;
    public float ValuePerStack => valuePerStack;

    public CharacterModifierModule()
    {
    }

    public CharacterModifierModule(
        string id,
        CharacterModifierTarget modifierTarget,
        CharacterModifierStat modifierStat,
        CharacterModifierOperation modifierOperation,
        float value)
    {
        moduleId = id ?? string.Empty;
        target = modifierTarget ?? new CharacterModifierTarget();
        stat = modifierStat;
        operation = modifierOperation;
        valuePerStack = value;
        Validate();
    }

    public bool Matches(
        CharacterModifierStat candidateStat,
        CharacterActionKind actionKind,
        string actionId,
        string effectId)
    {
        return stat == candidateStat &&
               target != null &&
               target.Matches(actionKind, actionId, effectId);
    }

    public void Validate()
    {
        moduleId = (moduleId ?? string.Empty).Trim();
        target ??= new CharacterModifierTarget();
        target.Validate();
        if (float.IsNaN(valuePerStack) ||
            float.IsInfinity(valuePerStack))
        {
            valuePerStack = 0f;
        }
        if (operation == CharacterModifierOperation.Multiply &&
            valuePerStack < 0f)
        {
            valuePerStack = 0f;
        }
    }
}

public sealed class CharacterModifierInstance
{
    public string SourceId { get; }
    public CharacterModifierModule Module { get; }
    public CharacterModifierLifetimeScope LifetimeScope { get; }
    public int StackCount { get; private set; }
    public bool HasFiniteDuration { get; }
    public float RemainingDuration { get; private set; }

    public bool IsExpired => HasFiniteDuration && RemainingDuration <= 0f;

    public CharacterModifierInstance(
        string sourceId,
        CharacterModifierModule module,
        int stackCount,
        CharacterModifierLifetimeScope lifetimeScope,
        float duration = float.PositiveInfinity)
    {
        SourceId = (sourceId ?? string.Empty).Trim();
        Module = module;
        StackCount = Mathf.Max(1, stackCount);
        LifetimeScope = lifetimeScope;
        HasFiniteDuration = !float.IsPositiveInfinity(duration);
        RemainingDuration = HasFiniteDuration
            ? Mathf.Max(0f, duration)
            : float.PositiveInfinity;
    }

    public void SetStackCount(int stackCount)
    {
        StackCount = Mathf.Max(1, stackCount);
    }

    public bool Tick(float deltaTime)
    {
        if (!HasFiniteDuration || RemainingDuration <= 0f || deltaTime <= 0f)
            return false;

        float previous = RemainingDuration;
        RemainingDuration = Mathf.Max(0f, previous - deltaTime);
        return !Mathf.Approximately(previous, RemainingDuration);
    }
}

public sealed class CharacterModifierCollection
{
    private readonly List<CharacterModifierInstance> _instances = new();

    public IReadOnlyList<CharacterModifierInstance> Instances => _instances;

    public bool ReplaceSource(
        string sourceId,
        IReadOnlyList<CharacterModifierModule> modules,
        int stackCount,
        CharacterModifierLifetimeScope lifetimeScope,
        float duration = float.PositiveInfinity)
    {
        sourceId = (sourceId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(sourceId))
            return false;

        if (modules == null || modules.Count == 0 || stackCount <= 0)
            return false;

        List<CharacterModifierModule> validModules = new();
        foreach (CharacterModifierModule module in modules)
        {
            if (module != null && module.ValuePerStack != 0f)
                validModules.Add(module);
        }
        if (validModules.Count == 0)
            return false;

        RemoveSource(sourceId);

        bool added = false;
        foreach (CharacterModifierModule module in validModules)
        {
            _instances.Add(new CharacterModifierInstance(
                sourceId,
                module,
                stackCount,
                lifetimeScope,
                duration));
            added = true;
        }
        return added;
    }

    public bool RemoveSource(string sourceId)
    {
        int removed = _instances.RemoveAll(instance =>
            instance != null &&
            string.Equals(
                instance.SourceId,
                sourceId,
                StringComparison.Ordinal));
        return removed > 0;
    }

    public bool ClearScope(CharacterModifierLifetimeScope scope)
    {
        return _instances.RemoveAll(instance =>
            instance == null || instance.LifetimeScope == scope) > 0;
    }

    public bool Tick(float deltaTime)
    {
        bool resolvedValuesChanged = false;
        for (int index = _instances.Count - 1; index >= 0; index--)
        {
            CharacterModifierInstance instance = _instances[index];
            if (instance == null)
            {
                _instances.RemoveAt(index);
                resolvedValuesChanged = true;
                continue;
            }

            instance.Tick(deltaTime);
            if (!instance.IsExpired)
                continue;

            _instances.RemoveAt(index);
            resolvedValuesChanged = true;
        }
        return resolvedValuesChanged;
    }

    public float Resolve(
        float baseValue,
        CharacterModifierStat stat,
        CharacterActionKind actionKind = default,
        string actionId = null,
        string effectId = null)
    {
        double flat = 0d;
        double percent = 0d;
        double multiplier = 1d;
        foreach (CharacterModifierInstance instance in _instances)
        {
            CharacterModifierModule module = instance?.Module;
            if (module == null || instance.IsExpired ||
                !module.Matches(stat, actionKind, actionId, effectId))
            {
                continue;
            }

            double value = module.ValuePerStack *
                           (double)instance.StackCount;
            switch (module.Operation)
            {
                case CharacterModifierOperation.AddFlat:
                    flat += value;
                    break;
                case CharacterModifierOperation.AddPercent:
                    percent += value;
                    break;
                case CharacterModifierOperation.Multiply:
                    multiplier *= Math.Pow(
                        Math.Max(0d, module.ValuePerStack),
                        instance.StackCount);
                    break;
            }
        }

        double resolved = (baseValue + flat) * (1d + percent) * multiplier;
        if (double.IsNaN(resolved))
            return baseValue;
        if (resolved >= float.MaxValue)
            return float.MaxValue;
        if (resolved <= -float.MaxValue)
            return -float.MaxValue;
        return (float)resolved;
    }
}

[Serializable]
public sealed class CharacterNumericCondition
{
    [SerializeField]
    private CharacterConditionType type;
    [SerializeField]
    private CharacterConditionTarget target;
    [SerializeField]
    private CharacterNumericConditionMetric metric;
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

    public CharacterConditionType Type => type;
    public CharacterConditionTarget Target => target;
    public CharacterNumericConditionMetric Metric =>
        type == CharacterConditionType.HasStatus
            ? CharacterNumericConditionMetric.StatusStackCount
            : metric;
    public CharacterNumericComparison Comparison =>
        type == CharacterConditionType.HasStatus
            ? CharacterNumericComparison.GreaterThanOrEqual
            : comparison;
    public float Threshold =>
        type == CharacterConditionType.HasStatus ? 1f : threshold;
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
    public int RequiredStatusMatchCount => Math.Max(1, statusMatchCount);

    public void Validate()
    {
        statusEffects ??= new List<StatusEffectSO>();
        if (type == CharacterConditionType.HasStatus)
        {
            type = CharacterConditionType.Numeric;
            metric = CharacterNumericConditionMetric.StatusStackCount;
            comparison = CharacterNumericComparison.GreaterThanOrEqual;
            threshold = 1f;
        }

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

public static class CharacterConditionEvaluator
{
    public static bool MatchesCharacter(
        CharacterNumericCondition condition,
        IBattleCharacter character)
    {
        if (condition == null || character == null)
            return false;

        if (condition.Metric ==
            CharacterNumericConditionMetric.StatusStackCount)
        {
            return MatchesStatusCondition(
                condition,
                character.GetStatusStackCount,
                character.GetActiveStatusEffects());
        }

        float value = condition.Metric switch
        {
            CharacterNumericConditionMetric.Health =>
                character.CurrentHealth,
            CharacterNumericConditionMetric.HealthPercentage =>
                character.MaximumHealth > 0
                    ? character.CurrentHealth * 100f /
                      character.MaximumHealth
                    : 0f,
            CharacterNumericConditionMetric.MaximumHealth =>
                character.MaximumHealth,
            CharacterNumericConditionMetric.HealthPerformancePercentage =>
                ResolveHealthPerformancePercentage(character),
            CharacterNumericConditionMetric.HealthPerformanceCap =>
                ResolveHealthPerformanceCap(character),
            CharacterNumericConditionMetric.AttackPower =>
                character.CurrentAttackPower,
            CharacterNumericConditionMetric.AttackSpeed =>
                character.CurrentAttackSpeed,
            CharacterNumericConditionMetric.Shield =>
                character.CurrentShield,
            _ => 0f
        };
        return Compare(
            value,
            condition.Comparison,
            condition.Threshold);
    }

    public static float ResolveHealthPerformanceCap(
        IBattleCharacter character)
    {
        return character is CharacterRuntime runtime
            ? runtime.HealthPerformanceCap
            : CharacterData.DefaultHealthPerformanceCap;
    }

    public static float ResolveHealthPerformancePercentage(
        IBattleCharacter character)
    {
        if (character == null)
            return 0f;
        return Mathf.Min(
            Mathf.Max(0, character.CurrentHealth),
            ResolveHealthPerformanceCap(character));
    }

    internal static bool MatchesStatusCondition(
        CharacterNumericCondition condition,
        Func<StatusEffectSO, int> getStatusStackCount,
        IReadOnlyList<BattleStatusSnapshot> activeStatuses = null)
    {
        if (condition == null || getStatusStackCount == null)
            return false;

        if (condition.StatusSelectionScope !=
            CharacterStatusSelectionScope.SelectedStatuses)
        {
            return MatchesStatusScope(
                condition,
                activeStatuses);
        }

        CharacterStatusSelection selection = condition.StatusSelection;
        if (selection.Count == 0)
            return false;

        int selectedCount = 0;
        int matchedCount = 0;
        for (int index = 0; index < selection.Count; index++)
        {
            StatusEffectSO status = selection.GetStatus(index);
            if (status == null || IsDuplicateStatus(
                    selection,
                    status,
                    index))
            {
                continue;
            }

            selectedCount++;
            if (Compare(
                    getStatusStackCount(status),
                    condition.Comparison,
                    condition.Threshold))
            {
                matchedCount++;
            }
        }

        if (selectedCount == 0)
            return false;

        return MatchesStatusCount(
            condition,
            selectedCount,
            matchedCount);
    }

    private static bool MatchesStatusScope(
        CharacterNumericCondition condition,
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

        int selectedCount = 0;
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

            selectedCount++;
            if (Compare(
                    snapshot.StackCount,
                    condition.Comparison,
                    condition.Threshold))
            {
                matchedCount++;
            }
        }

        if (selectedCount == 0)
            return false;

        return MatchesStatusCount(
            condition,
            selectedCount,
            matchedCount);
    }

    private static bool MatchesStatusCount(
        CharacterNumericCondition condition,
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

    private static bool IsDuplicateStatus(
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

    public static bool Compare(
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
            CharacterNumericComparison.GreaterThan => value > threshold,
            CharacterNumericComparison.LessThan => value < threshold,
            CharacterNumericComparison.Equal =>
                Mathf.Approximately(value, threshold),
            CharacterNumericComparison.NotEqual =>
                !Mathf.Approximately(value, threshold),
            _ => false
        };
    }

    public static bool AllowsAction(
        IBattleCharacter source,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions,
        bool hasMatchingActionTarget)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        bool hasSourceCondition = false;
        bool hasActionTargetCondition = false;
        bool anySourceMatched = false;
        bool allSourceMatched = true;
        bool evaluatedAny = false;
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            if (condition.Target == CharacterConditionTarget.ActionTarget)
            {
                hasActionTargetCondition = true;
                continue;
            }

            hasSourceCondition = true;
            bool matched = MatchesCharacter(condition, source);
            anySourceMatched |= matched;
            allSourceMatched &= matched;
        }

        if (!evaluatedAny)
            return true;

        if (matchMode == CharacterConditionMatchMode.Any)
        {
            return (hasSourceCondition && anySourceMatched) ||
                   (hasActionTargetCondition && hasMatchingActionTarget);
        }

        return (!hasSourceCondition || allSourceMatched) &&
               (!hasActionTargetCondition || hasMatchingActionTarget);
    }
}

[Serializable]
public sealed class CharacterEffectTargetSelector :
    IBattleEffectTargetSelector
{
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject = CharacterAttackSubject.Random;
    [SerializeField]
    private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField, Min(1)]
    private int subjectCount = 1;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private BattleAreaDefinition areaDefinition = new();

    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public int SubjectCount => subjectCount;
    public CharacterConditionMatchMode ConditionMatchMode =>
        conditionMatchMode;
    public IReadOnlyList<CharacterNumericCondition> NumericConditions =>
        numericConditions != null
            ? numericConditions
            : Array.Empty<CharacterNumericCondition>();
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets != null
            ? areaOffsets
            : Array.Empty<CharacterTargetAreaOffset>();
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public bool HasNumericConditions => NumericConditions.Count > 0;

    public void Validate()
    {
        subjectCount = Mathf.Max(1, subjectCount);
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        areaDefinition ??= new BattleAreaDefinition();
        areaDefinition.Validate();
    }
}

[Serializable]
public sealed class CharacterCumulativeUpgradeModifier
{
    [SerializeField]
    private CharacterCumulativeUpgradeModifierType type;
    [SerializeField]
    private float valuePerLevel;

    public CharacterCumulativeUpgradeModifierType Type => type;
    public float ValuePerLevel => valuePerLevel;

    public CharacterCumulativeUpgradeModifier(
        CharacterCumulativeUpgradeModifierType modifierType,
        float value)
    {
        type = modifierType;
        valuePerLevel = value;
    }

    public void Validate()
    {
        if (float.IsNaN(valuePerLevel) ||
            float.IsInfinity(valuePerLevel))
        {
            valuePerLevel = 0f;
        }
    }
}

[Serializable]
public sealed class CharacterCumulativeUpgradeDefinition
{
    [SerializeField]
    private string upgradeId;
    [SerializeField]
    private CharacterUpgradeLocalizationPreset localizationPreset;
    [SerializeField]
    private string titleLocalizationKey;
    [SerializeField]
    private string descriptionLocalizationKey;
    [SerializeField, Min(0)]
    private int maxLevel = 1;
    [SerializeField]
    private List<CharacterCumulativeUpgradeModifier> modifiers = new();
    [SerializeField]
    private List<CharacterModifierModule> modifierModules = new();

    public string UpgradeId => upgradeId ?? string.Empty;
    public CharacterUpgradeLocalizationPreset LocalizationPreset =>
        localizationPreset;
    public string TitleLocalizationKey => titleLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public bool UsesCustomLocalization =>
        localizationPreset == CharacterUpgradeLocalizationPreset.Custom ||
        (localizationPreset ==
             CharacterUpgradeLocalizationPreset.Automatic &&
         (!string.IsNullOrWhiteSpace(titleLocalizationKey) ||
          !string.IsNullOrWhiteSpace(descriptionLocalizationKey)));
    public int MaxLevel => Mathf.Max(0, maxLevel);
    public bool HasUnlimitedMaxLevel => MaxLevel == 0;
    public IReadOnlyList<CharacterCumulativeUpgradeModifier> Modifiers =>
        modifiers != null
            ? modifiers
            : Array.Empty<CharacterCumulativeUpgradeModifier>();
    public IReadOnlyList<CharacterModifierModule> ModifierModules =>
        modifierModules != null
            ? modifierModules
            : Array.Empty<CharacterModifierModule>();

    public int ClampLevel(int level)
    {
        level = Mathf.Max(0, level);
        return HasUnlimitedMaxLevel
            ? level
            : Mathf.Min(level, MaxLevel);
    }

    public void Validate()
    {
        upgradeId = (upgradeId ?? string.Empty).Trim();
        titleLocalizationKey =
            (titleLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        if (localizationPreset ==
                CharacterUpgradeLocalizationPreset.Automatic &&
            (!string.IsNullOrEmpty(titleLocalizationKey) ||
             !string.IsNullOrEmpty(descriptionLocalizationKey)))
        {
            localizationPreset = CharacterUpgradeLocalizationPreset.Custom;
        }
        maxLevel = Mathf.Max(0, maxLevel);
        modifiers ??= new List<CharacterCumulativeUpgradeModifier>();
        foreach (CharacterCumulativeUpgradeModifier modifier in modifiers)
            modifier?.Validate();
        modifierModules ??= new List<CharacterModifierModule>();
        foreach (CharacterModifierModule module in modifierModules)
            module?.Validate();
    }
}

[Serializable]
public sealed class CharacterDungeonUpgradeEntry
{
    [SerializeField]
    private string upgradeId;
    [SerializeField]
    private CharacterUpgradeLocalizationPreset localizationPreset;
    [SerializeField]
    private string titleLocalizationKey;
    [SerializeField]
    private string descriptionLocalizationKey;
    [SerializeField]
    private CharacterDungeonUpgradeType type;
    [SerializeField, Range(0f, 100f)]
    private float probability;
    [SerializeField, Min(0)]
    private int limit = 1;
    [SerializeField]
    private List<CharacterModifierModule> modifierModules = new();

    public string UpgradeId => !string.IsNullOrWhiteSpace(upgradeId)
        ? upgradeId.Trim()
        : $"legacy.{type.ToString().ToLowerInvariant()}";
    public bool HasExplicitUpgradeId =>
        !string.IsNullOrWhiteSpace(upgradeId);
    public CharacterUpgradeLocalizationPreset LocalizationPreset =>
        localizationPreset;
    public string TitleLocalizationKey => titleLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public bool UsesCustomLocalization =>
        localizationPreset == CharacterUpgradeLocalizationPreset.Custom ||
        (localizationPreset ==
             CharacterUpgradeLocalizationPreset.Automatic &&
         (!string.IsNullOrWhiteSpace(titleLocalizationKey) ||
          !string.IsNullOrWhiteSpace(descriptionLocalizationKey)));
    public CharacterDungeonUpgradeType Type => type;
    public float Probability => probability;
    public int Limit => limit;
    public bool HasUnlimitedLimit => limit == 0;
    public IReadOnlyList<CharacterModifierModule> ModifierModules =>
        modifierModules != null
            ? modifierModules
            : Array.Empty<CharacterModifierModule>();
    public bool HasModifierModules => ModifierModules.Count > 0;
    public float FixedValue => type switch
    {
        CharacterDungeonUpgradeType.AttackPower => 0.5f,
        CharacterDungeonUpgradeType.Speed => -0.1f,
        CharacterDungeonUpgradeType.PassiveDamage => 0.5f,
        CharacterDungeonUpgradeType.AttackDamage => 0.5f,
        CharacterDungeonUpgradeType.SkillDamage => 1f,
        CharacterDungeonUpgradeType.SkillCostReduction => -1f,
        _ => 0f
    };

    public CharacterDungeonUpgradeEntry(
        CharacterDungeonUpgradeType type,
        float probability,
        int limit = 1)
    {
        this.type = type;
        this.probability = probability;
        this.limit = limit;
    }

    public void Validate()
    {
        upgradeId = (upgradeId ?? string.Empty).Trim();
        titleLocalizationKey = (titleLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        if (localizationPreset ==
                CharacterUpgradeLocalizationPreset.Automatic &&
            (!string.IsNullOrEmpty(titleLocalizationKey) ||
             !string.IsNullOrEmpty(descriptionLocalizationKey)))
        {
            localizationPreset = CharacterUpgradeLocalizationPreset.Custom;
        }
        probability = Mathf.Clamp(probability, 0f, 100f);
        limit = Mathf.Max(0, limit);
        modifierModules ??= new List<CharacterModifierModule>();
        foreach (CharacterModifierModule module in modifierModules)
            module?.Validate();
    }
}

[Serializable]
public sealed class CharacterDungeonUpgradeDefinition
{
    public const float RequiredProbabilityTotal = 100f;
    public const float ProbabilityTolerance = 0.001f;

    [SerializeField]
    private List<CharacterDungeonUpgradeEntry> entries = new();

    public IReadOnlyList<CharacterDungeonUpgradeEntry> Entries => entries;

    public float TotalProbability
    {
        get
        {
            float total = 0f;
            if (entries == null)
                return total;

            foreach (CharacterDungeonUpgradeEntry entry in entries)
            {
                if (entry != null)
                    total += entry.Probability;
            }

            return total;
        }
    }

    public bool HasValidProbabilityTotal => Mathf.Abs(
        TotalProbability - RequiredProbabilityTotal) <=
        ProbabilityTolerance;
    public bool UsesLegacyProbabilityMode
    {
        get
        {
            if (entries == null || entries.Count == 0)
                return false;
            foreach (CharacterDungeonUpgradeEntry entry in entries)
            {
                if (entry == null || entry.HasExplicitUpgradeId ||
                    entry.HasModifierModules)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public CharacterDungeonUpgradeEntry GetEntry(
        CharacterDungeonUpgradeType type)
    {
        if (entries == null)
            return null;

        foreach (CharacterDungeonUpgradeEntry entry in entries)
        {
            if (entry != null && entry.Type == type)
                return entry;
        }

        return null;
    }

    public CharacterDungeonUpgradeEntry GetEntry(string upgradeId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(upgradeId))
            return null;

        foreach (CharacterDungeonUpgradeEntry entry in entries)
        {
            if (entry != null && string.Equals(
                    entry.UpgradeId,
                    upgradeId.Trim(),
                    StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    public void Validate()
    {
        entries ??= new List<CharacterDungeonUpgradeEntry>();
        foreach (CharacterDungeonUpgradeEntry entry in entries)
            entry?.Validate();
    }
}

[Serializable]
public sealed class CharacterEffectDefinition :
    IBattleEffectDefinition,
    IBattlePresentationEffectDefinition
{
    [SerializeField]
    private string effectId;
    [SerializeField]
    private CharacterEffectType type;
    [SerializeField]
    private CharacterEffectTargetMode targetMode;
    [SerializeField]
    private CharacterEffectPreconditionFailurePolicy
        preconditionFailurePolicy;
    [SerializeField]
    private CharacterEffectFailurePolicy failurePolicy;
    [SerializeField]
    private CharacterEffectTargetSelector targetSelector = new();
    [SerializeField]
    private CharacterAttackDamageType damageType;
    [SerializeField]
    private CharacterDamageAmountMode damageAmountMode;
    [SerializeField, Min(0f)]
    private float damageAmount = 1f;
    [SerializeField, Min(0f)]
    private float sourceResourceScale;
    [SerializeField]
    private float sourceCurrentHealthScale;
    [SerializeField]
    private float sourceMaxHealthScale;
    [SerializeField]
    private float targetCurrentHealthScale;
    [SerializeField]
    private float targetMaxHealthScale;
    [SerializeField]
    private StatusEffectSO sourceStatusScalingEffect;
    [SerializeField]
    private float sourceStatusStacksScale;
    [SerializeField]
    private StatusEffectSO targetStatusScalingEffect;
    [SerializeField]
    private float targetStatusStacksScale;
    [SerializeField]
    private List<CharacterStatusStatContributionMultiplier>
        statusContributionMultipliers = new();
    [SerializeField, Min(0.1f)]
    private float statusDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float statusStacks = 1f;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private List<StatusEffectSO> statusRemovalEffects = new();
    [SerializeField]
    private CharacterStatusRemovalTarget statusRemovalTarget;
    [SerializeField]
    private CharacterStatusRemovalPickMode statusRemovalPickMode;
    [SerializeField, Min(1)]
    private int statusRemovalPickCount = 1;
    [SerializeField]
    private CharacterStatusRemovalAmountMode statusRemovalAmountMode;
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField, Range(0.01f, 1f)]
    private float statusRemovalRatio = 0.5f;
    [SerializeField]
    private BattleVfxCueSO castVfxCue;
    [SerializeField]
    private BattleVfxCueSO projectileVfxCue;
    [SerializeField]
    private BattleVfxCueSO impactVfxCue;

    internal static CharacterEffectDefinition CreateFixedRuntimeEffect(
        CharacterEffectType effectType,
        float amount,
        StatusEffectSO appliedStatus = null,
        float appliedStatusDuration = 1f,
        float appliedStatusStacks = 1f)
    {
        return new CharacterEffectDefinition
        {
            type = effectType,
            targetMode = CharacterEffectTargetMode.InheritAction,
            preconditionFailurePolicy =
                CharacterEffectPreconditionFailurePolicy.AbortAction,
            failurePolicy = CharacterEffectFailurePolicy.Continue,
            targetSelector = new CharacterEffectTargetSelector(),
            damageType = CharacterAttackDamageType.Physical,
            damageAmountMode = CharacterDamageAmountMode.Fixed,
            damageAmount = Mathf.Max(0f, amount),
            statusDuration = TimePrecision.Normalize(
                appliedStatusDuration,
                0.1f),
            statusStacks = Mathf.Max(0.1f, appliedStatusStacks),
            statusEffect = appliedStatus,
        };
    }

    public string EffectId => effectId ?? string.Empty;
    public CharacterEffectType Type => type;
    public BattleEffectType BattleEffectType =>
        (BattleEffectType)(int)type;
    public CharacterEffectTargetMode TargetMode => targetMode;
    public BattleEffectTargetMode BattleTargetMode =>
        (BattleEffectTargetMode)(int)targetMode;
    public CharacterEffectPreconditionFailurePolicy
        PreconditionFailurePolicy => preconditionFailurePolicy;
    public BattleEffectPreconditionFailurePolicy
        BattlePreconditionFailurePolicy =>
            (BattleEffectPreconditionFailurePolicy)(int)
            preconditionFailurePolicy;
    public CharacterEffectFailurePolicy FailurePolicy => failurePolicy;
    public BattleEffectFailurePolicy BattleFailurePolicy =>
        (BattleEffectFailurePolicy)(int)failurePolicy;
    public CharacterEffectTargetSelector TargetSelector => targetSelector;
    public IBattleEffectTargetSelector BattleTargetSelector =>
        targetSelector;
    public bool RequiresActionTargets =>
        BattleEffectRules.RequiresActionTargets(this);
    public CharacterAttackDamageType DamageType => damageType;
    public CharacterDamageAmountMode DamageAmountMode => damageAmountMode;
    public float DamageAmount => damageAmount;
    public CharacterDamageAmountMode AmountMode => damageAmountMode;
    public float Amount => damageAmount;
    public float SourceResourceScale => sourceResourceScale;
    public float SourceCurrentHealthScale => sourceCurrentHealthScale;
    public float SourceMaxHealthScale => sourceMaxHealthScale;
    public float TargetCurrentHealthScale => targetCurrentHealthScale;
    public float TargetMaxHealthScale => targetMaxHealthScale;
    public StatusEffectSO SourceStatusScalingEffect =>
        sourceStatusScalingEffect;
    public float SourceStatusStacksScale => sourceStatusStacksScale;
    public StatusEffectSO TargetStatusScalingEffect =>
        targetStatusScalingEffect;
    public float TargetStatusStacksScale => targetStatusStacksScale;
    public IReadOnlyList<CharacterStatusStatContributionMultiplier>
        StatusContributionMultipliers =>
            statusContributionMultipliers != null
                ? statusContributionMultipliers
                : Array.Empty<
                    CharacterStatusStatContributionMultiplier>();
    public ScalingValue AmountScaling =>
        ScalingValue.FromLegacy(damageAmountMode, damageAmount) +
        ScalingValue.SourceResource(sourceResourceScale) +
        ScalingValue.SourceCurrentHealth(sourceCurrentHealthScale) +
        ScalingValue.SourceMaximumHealth(sourceMaxHealthScale) +
        ScalingValue.TargetCurrentHealth(targetCurrentHealthScale) +
        ScalingValue.TargetMaximumHealth(targetMaxHealthScale) +
        ScalingValue.SourceStatusStacks(sourceStatusStacksScale) +
        ScalingValue.TargetStatusStacks(targetStatusStacksScale);
    public ScalingValue DamageScaling => AmountScaling;
    public float StatusDuration => statusDuration;
    public float StatusStacks => statusStacks;
    public StatusEffectSO StatusEffect => statusEffect;
    public IReadOnlyList<StatusEffectSO> StatusRemovalEffects =>
        statusRemovalEffects;
    public CharacterStatusRemovalTarget StatusRemovalTarget =>
        statusRemovalTarget;
    public CharacterStatusRemovalPickMode StatusRemovalPickMode =>
        statusRemovalPickMode;
    public int StatusRemovalPickCount => statusRemovalPickCount;
    public CharacterStatusRemovalSelection StatusRemovalSelection =>
        new(
            statusRemovalTarget,
            statusEffect,
            statusRemovalEffects,
            statusRemovalPickMode,
            statusRemovalPickCount);
    public CharacterStatusRemovalAmountMode StatusRemovalAmountMode =>
        statusRemovalAmountMode;
    public int StatusRemovalCount => statusRemovalCount;
    public float StatusRemovalRatio => statusRemovalRatio;
    public CharacterStatusRemovalAmount StatusRemovalAmount =>
        new(
            statusRemovalAmountMode,
            statusRemovalCount,
            statusRemovalRatio);
    public BattleVfxCueSO CastVfxCue => castVfxCue;
    public BattleVfxCueSO ProjectileVfxCue => projectileVfxCue;
    public BattleVfxCueSO ImpactVfxCue => impactVfxCue;

    public void Validate()
    {
        effectId = (effectId ?? string.Empty).Trim();
        targetSelector ??= new CharacterEffectTargetSelector();
        statusRemovalEffects ??= new List<StatusEffectSO>();
        statusContributionMultipliers ??=
            new List<CharacterStatusStatContributionMultiplier>();
        targetSelector.Validate();
        foreach (CharacterStatusStatContributionMultiplier modifier in
                 statusContributionMultipliers)
        {
            modifier?.Validate();
        }
        CharacterStatusRemovalPick.Normalize(
            ref statusRemovalPickMode,
            ref statusRemovalPickCount);
        if (float.IsNaN(damageAmount) || float.IsInfinity(damageAmount))
            damageAmount = 0f;
        if (float.IsNaN(sourceResourceScale) ||
            float.IsInfinity(sourceResourceScale))
        {
            sourceResourceScale = 0f;
        }
        if (float.IsNaN(sourceCurrentHealthScale) ||
            float.IsInfinity(sourceCurrentHealthScale))
        {
            sourceCurrentHealthScale = 0f;
        }
        if (float.IsNaN(sourceMaxHealthScale) ||
            float.IsInfinity(sourceMaxHealthScale))
        {
            sourceMaxHealthScale = 0f;
        }
        if (float.IsNaN(targetCurrentHealthScale) ||
            float.IsInfinity(targetCurrentHealthScale))
        {
            targetCurrentHealthScale = 0f;
        }
        if (float.IsNaN(targetMaxHealthScale) ||
            float.IsInfinity(targetMaxHealthScale))
        {
            targetMaxHealthScale = 0f;
        }
        if (float.IsNaN(sourceStatusStacksScale) ||
            float.IsInfinity(sourceStatusStacksScale))
        {
            sourceStatusStacksScale = 0f;
        }
        if (float.IsNaN(targetStatusStacksScale) ||
            float.IsInfinity(targetStatusStacksScale))
        {
            targetStatusStacksScale = 0f;
        }
        if (float.IsNaN(statusStacks) || float.IsInfinity(statusStacks))
            statusStacks = 1f;

        damageAmount = Mathf.Max(0f, damageAmount);
        sourceResourceScale = Mathf.Max(0f, sourceResourceScale);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        CharacterStatusRemovalAmount.Normalize(
            ref statusRemovalAmountMode,
            ref statusRemovalCount,
            ref statusRemovalRatio);
    }
}

[Serializable]
public sealed class CharacterSkillDefinition :
    ICharacterConditionalActionDefinition,
    IBattleAbilityDefinition
{
    [SerializeField]
    private string actionId;
    [SerializeField]
    private List<CharacterSkillSectionType> sections = new();
    [SerializeField]
    private Sprite iconSprite;
    [SerializeField]
    private AudioClip audioClip;
    [SerializeField, Min(1)]
    private int cost = 1;
    [FormerlySerializedAs("condition")]
    [SerializeField]
    private CharacterActionLinkage linkage =
        CharacterActionLinkage.None;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField, Min(0)]
    private int subjectCount = 1;
    [SerializeField]
    private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField]
    private CharacterAttackDamageType damageType;
    [SerializeField]
    private CharacterDamageAmountMode damageAmountMode;
    [SerializeField, Min(0f)]
    private float damageAmount = 1f;
    [SerializeField, Min(0.1f)]
    private float statusDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float statusStacks = 1f;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private StatusEffectSO statusRemovalEffect;
    [SerializeField]
    private CharacterStatusRemovalTarget statusRemovalTarget;
    [SerializeField]
    private CharacterStatusRemovalPickMode statusRemovalPickMode;
    [SerializeField, Min(1)]
    private int statusRemovalPickCount = 1;
    [SerializeField]
    private CharacterStatusRemovalAmountMode statusRemovalAmountMode;
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField, Range(0.01f, 1f)]
    private float statusRemovalRatio = 0.5f;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private BattleAreaDefinition areaDefinition = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

    public string ActionId => actionId ?? string.Empty;
    public IReadOnlyList<CharacterSkillSectionType> Sections => sections;
    public Sprite IconSprite => iconSprite;
    public AudioClip AudioClip => audioClip;
    public int Cost => cost;
    public bool HasLinkageSection =>
        HasSection(CharacterSkillSectionType.Linkage);
    public CharacterActionLinkage Linkage => linkage;
    public bool HasConditionSection =>
        HasSection(CharacterSkillSectionType.Condition);
    public CharacterConditionMatchMode ConditionMatchMode =>
        conditionMatchMode;
    public IReadOnlyList<CharacterNumericCondition> NumericConditions =>
        numericConditions;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public int SubjectCount => subjectCount;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public CharacterAttackDamageType DamageType => damageType;
    public CharacterDamageAmountMode DamageAmountMode => damageAmountMode;
    public float DamageAmount => damageAmount;
    public ScalingValue DamageScaling =>
        ScalingValue.FromLegacy(damageAmountMode, damageAmount);
    public float StatusDuration => statusDuration;
    public float StatusStacks => statusStacks;
    public StatusEffectSO AppliedStatusEffect => statusEffect;
    public StatusEffectSO StatusRemovalEffect => statusRemovalEffect;
    public CharacterStatusRemovalTarget StatusRemovalTarget =>
        statusRemovalTarget;
    public CharacterStatusRemovalPickMode StatusRemovalPickMode =>
        statusRemovalPickMode;
    public int StatusRemovalPickCount => statusRemovalPickCount;
    public CharacterStatusRemovalSelection StatusRemovalSelection =>
        new(
            statusRemovalTarget,
            statusRemovalEffect,
            null,
            statusRemovalPickMode,
            statusRemovalPickCount);
    public CharacterStatusRemovalAmountMode StatusRemovalAmountMode =>
        statusRemovalAmountMode;
    public int StatusRemovalCount => statusRemovalCount;
    public float StatusRemovalRatio => statusRemovalRatio;
    public CharacterStatusRemovalAmount StatusRemovalAmount =>
        new(
            statusRemovalAmountMode,
            statusRemovalCount,
            statusRemovalRatio);
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;
    public string AbilityId => ActionId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => HasExplicitEffects ? 1 : 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.CharacterSkill;
    public BattleAbilityTargeting Targeting =>
        BattleAbilityTargeting.FromCharacter(
            targetFaction,
            subject,
            subjectMetric,
            subjectCount,
            AreaDefinition);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        (IEnumerable<IBattleEffectDefinition>)effects ??
        Array.Empty<IBattleEffectDefinition>();
    public bool UsesLegacyEffectStorage => !HasExplicitEffects;
    public bool HasExecutableContent =>
        HasExplicitEffects || HasSection(CharacterSkillSectionType.Ability);

    public bool HasSection(CharacterSkillSectionType sectionType)
    {
        return sections != null && sections.Contains(sectionType);
    }

    public float CalculateAbilityDamage(float characterAttackPower)
    {
        return DamageScaling.Evaluate(EffectContext.ForPreview(
            CharacterActionKind.Skill,
            characterAttackPower));
    }

    public void Validate()
    {
        actionId = (actionId ?? string.Empty).Trim();
        sections ??= new List<CharacterSkillSectionType>();
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        cost = Mathf.Max(1, cost);
        subjectCount = AreaDefinition.UsesWorldArea
            ? Mathf.Max(0, subjectCount)
            : Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        CharacterStatusRemovalPick.Normalize(
            ref statusRemovalPickMode,
            ref statusRemovalPickCount);
        CharacterStatusRemovalAmount.Normalize(
            ref statusRemovalAmountMode,
            ref statusRemovalCount,
            ref statusRemovalRatio);
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        areaDefinition ??= new BattleAreaDefinition();
        areaDefinition.Validate();
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();
    }
}

[Serializable]
public sealed class CharacterStatusStackCostDefinition
{
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField, Min(1)]
    private int requiredStacks = 1;
    [SerializeField, Min(1)]
    private int consumedStacks = 1;

    public StatusEffectSO StatusEffect => statusEffect;
    public int RequiredStacks => Mathf.Max(1, requiredStacks);
    public int ConsumedStacks => Mathf.Clamp(
        consumedStacks,
        1,
        RequiredStacks);
    public bool IsConfigured => statusEffect != null;

    public void Validate()
    {
        requiredStacks = Mathf.Max(1, requiredStacks);
        consumedStacks = Mathf.Clamp(
            consumedStacks,
            1,
            requiredStacks);
    }
}

[Serializable]
public sealed class CharacterPassiveDefinition :
    ICharacterConditionalActionDefinition,
    IBattleAbilityDefinition
{
    [SerializeField]
    private string actionId;
    [SerializeField]
    private List<CharacterPassiveSectionType> sections = new();
    [SerializeField]
    private Sprite iconSprite;
    [SerializeField]
    private AudioClip audioClip;
    [SerializeField]
    private CharacterPassiveMotionMode motionMode;
    [SerializeField]
    private CharacterPassiveTrigger trigger;
    [SerializeField]
    private CharacterPassiveKillSource killSource;
    [SerializeField]
    private CharacterSO specifiedKillerCharacter;
    [SerializeField]
    private CharacterPassiveStatusTarget statusTarget;
    [SerializeField]
    private StatusEffectSO triggerStatusEffect;
    [SerializeField]
    private List<StatusEffectSO> triggerStatusEffects = new();
    [SerializeField]
    private CharacterStatusSelectionScope triggerStatusScope;
    [SerializeField, Min(0.1f)]
    private float cooldown = 1f;
    [FormerlySerializedAs("detailCondition")]
    [SerializeField]
    private CharacterActionLinkage linkage =
        CharacterActionLinkage.None;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private CharacterPassiveAttackTargetRelation attackTargetRelation;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField, Min(0)]
    private int subjectCount = 1;
    [SerializeField]
    private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField]
    private CharacterAttackDamageType damageType;
    [SerializeField]
    private CharacterDamageAmountMode damageAmountMode;
    [SerializeField, Min(0f)]
    private float damageAmount = 1f;
    [SerializeField, Min(0.1f)]
    private float statusDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float statusStacks = 1f;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private StatusEffectSO statusRemovalEffect;
    [SerializeField]
    private CharacterStatusRemovalTarget statusRemovalTarget;
    [SerializeField]
    private CharacterStatusRemovalPickMode statusRemovalPickMode;
    [SerializeField, Min(1)]
    private int statusRemovalPickCount = 1;
    [SerializeField]
    private CharacterStatusRemovalAmountMode statusRemovalAmountMode;
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField, Range(0.01f, 1f)]
    private float statusRemovalRatio = 0.5f;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private BattleAreaDefinition areaDefinition = new();
    [SerializeField]
    private CharacterStatusStackCostDefinition selfStatusCost = new();
    [SerializeField]
    private List<CharacterPassiveStatModifierDefinition> statModifiers =
        new();
    [SerializeField]
    private List<CharacterStatusStatContributionMultiplier>
        statusContributionMultipliers = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

    public string ActionId => actionId ?? string.Empty;
    public IReadOnlyList<CharacterPassiveSectionType> Sections => sections;
    public Sprite IconSprite => iconSprite;
    public AudioClip AudioClip => audioClip;
    public CharacterPassiveMotionMode MotionMode => motionMode;
    public bool ShouldPlayPassiveMotion =>
        motionMode == CharacterPassiveMotionMode.PlayPassiveMotion;
    public CharacterPassiveTrigger Trigger => trigger;
    public CharacterPassiveKillSource KillSource => killSource;
    public CharacterSO SpecifiedKillerCharacter => specifiedKillerCharacter;
    public CharacterPassiveStatusTarget StatusTarget => statusTarget;
    public StatusEffectSO TriggerStatusEffect => triggerStatusEffect;
    public IReadOnlyList<StatusEffectSO> TriggerStatusEffects =>
        triggerStatusEffects != null
            ? triggerStatusEffects
            : Array.Empty<StatusEffectSO>();
    public CharacterStatusSelection TriggerStatusSelection =>
        new(triggerStatusEffect, triggerStatusEffects);
    public CharacterStatusSelectionScope TriggerStatusScope =>
        triggerStatusScope;
    public float Cooldown => TimePrecision.Normalize(
        cooldown,
        TimePrecision.Step);
    public bool HasLinkageSection =>
        HasSection(CharacterPassiveSectionType.Linkage);
    public CharacterActionLinkage Linkage => linkage;
    public bool HasConditionSection =>
        HasSection(CharacterPassiveSectionType.Condition);
    public CharacterConditionMatchMode ConditionMatchMode =>
        conditionMatchMode;
    public CharacterPassiveAttackTargetRelation AttackTargetRelation =>
        attackTargetRelation;
    public bool HasAttackTargetRelationCondition =>
        HasConditionSection &&
        attackTargetRelation != CharacterPassiveAttackTargetRelation.Any;
    public IReadOnlyList<CharacterNumericCondition> NumericConditions =>
        numericConditions;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public int SubjectCount => subjectCount;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public CharacterAttackDamageType DamageType => damageType;
    public CharacterDamageAmountMode DamageAmountMode => damageAmountMode;
    public float DamageAmount => damageAmount;
    public ScalingValue DamageScaling =>
        ScalingValue.FromLegacy(damageAmountMode, damageAmount);
    public float StatusDuration => statusDuration;
    public float StatusStacks => statusStacks;
    public StatusEffectSO AppliedStatusEffect => statusEffect;
    public StatusEffectSO StatusRemovalEffect => statusRemovalEffect;
    public CharacterStatusRemovalTarget StatusRemovalTarget =>
        statusRemovalTarget;
    public CharacterStatusRemovalPickMode StatusRemovalPickMode =>
        statusRemovalPickMode;
    public int StatusRemovalPickCount => statusRemovalPickCount;
    public CharacterStatusRemovalSelection StatusRemovalSelection =>
        new(
            statusRemovalTarget,
            statusRemovalEffect,
            null,
            statusRemovalPickMode,
            statusRemovalPickCount);
    public CharacterStatusRemovalAmountMode StatusRemovalAmountMode =>
        statusRemovalAmountMode;
    public int StatusRemovalCount => statusRemovalCount;
    public float StatusRemovalRatio => statusRemovalRatio;
    public CharacterStatusRemovalAmount StatusRemovalAmount =>
        new(
            statusRemovalAmountMode,
            statusRemovalCount,
            statusRemovalRatio);
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public CharacterStatusStackCostDefinition SelfStatusCost =>
        selfStatusCost;
    public bool HasSelfStatusCost =>
        HasSection(CharacterPassiveSectionType.SelfStatusCost) &&
        selfStatusCost != null && selfStatusCost.IsConfigured;
    public IReadOnlyList<CharacterPassiveStatModifierDefinition>
        StatModifiers => statModifiers != null
            ? statModifiers
            : Array.Empty<CharacterPassiveStatModifierDefinition>();
    public bool HasStatModifierSection =>
        HasSection(CharacterPassiveSectionType.StatModifier);
    public IReadOnlyList<CharacterStatusStatContributionMultiplier>
        StatusContributionMultipliers =>
            statusContributionMultipliers != null
                ? statusContributionMultipliers
                : Array.Empty<
                    CharacterStatusStatContributionMultiplier>();
    public bool HasStatusContributionSection =>
        HasSection(CharacterPassiveSectionType.StatusContribution);
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;
    public string AbilityId => ActionId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => HasExplicitEffects ? 1 : 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.CharacterPassive;
    public BattleAbilityTargeting Targeting =>
        BattleAbilityTargeting.FromCharacter(
            targetFaction,
            subject,
            subjectMetric,
            subjectCount,
            AreaDefinition);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        (IEnumerable<IBattleEffectDefinition>)effects ??
        Array.Empty<IBattleEffectDefinition>();
    public bool UsesLegacyEffectStorage => !HasExplicitEffects;
    public bool HasExecutableContent =>
        HasExplicitEffects || HasSection(CharacterPassiveSectionType.Ability);
    public bool IsEmptyPlaceholder => sections != null &&
        sections.Count == 0;

    public bool HasSection(CharacterPassiveSectionType sectionType)
    {
        return sections != null && sections.Contains(sectionType);
    }

    public float CalculateAbilityDamage(float characterAttackPower)
    {
        return DamageScaling.Evaluate(EffectContext.ForPreview(
            CharacterActionKind.Passive,
            characterAttackPower));
    }

    public void Validate()
    {
        actionId = (actionId ?? string.Empty).Trim();
        sections ??= new List<CharacterPassiveSectionType>();
        if (!Enum.IsDefined(typeof(CharacterPassiveMotionMode), motionMode))
        {
            motionMode = CharacterPassiveMotionMode.PlayPassiveMotion;
        }
        if (trigger != CharacterPassiveTrigger.OnAttack)
            linkage = CharacterActionLinkage.None;
        triggerStatusEffects ??= new List<StatusEffectSO>();
        if (!Enum.IsDefined(
                typeof(CharacterStatusSelectionScope),
                triggerStatusScope))
        {
            triggerStatusScope =
                CharacterStatusSelectionScope.SelectedStatuses;
        }
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        statModifiers ??=
            new List<CharacterPassiveStatModifierDefinition>();
        foreach (CharacterPassiveStatModifierDefinition modifier in
                 statModifiers)
        {
            modifier?.Validate();
        }
        statusContributionMultipliers ??=
            new List<CharacterStatusStatContributionMultiplier>();
        foreach (CharacterStatusStatContributionMultiplier modifier in
                 statusContributionMultipliers)
        {
            modifier?.Validate();
        }
        if (!Enum.IsDefined(
                typeof(CharacterPassiveAttackTargetRelation),
                attackTargetRelation))
        {
            attackTargetRelation =
                CharacterPassiveAttackTargetRelation.Any;
        }
        cooldown = TimePrecision.Normalize(cooldown, TimePrecision.Step);
        subjectCount = AreaDefinition.UsesWorldArea
            ? Mathf.Max(0, subjectCount)
            : Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        CharacterStatusRemovalPick.Normalize(
            ref statusRemovalPickMode,
            ref statusRemovalPickCount);
        CharacterStatusRemovalAmount.Normalize(
            ref statusRemovalAmountMode,
            ref statusRemovalCount,
            ref statusRemovalRatio);
        selfStatusCost ??= new CharacterStatusStackCostDefinition();
        selfStatusCost.Validate();
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        areaDefinition ??= new BattleAreaDefinition();
        areaDefinition.Validate();
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();
    }
}

[Serializable]
public sealed class CharacterTargetAreaOffset
{
    [SerializeField]
    private int rowOffset;
    [SerializeField]
    private int columnOffset;

    public int RowOffset => rowOffset;
    public int ColumnOffset => columnOffset;
    public bool IsCenter => rowOffset == 0 && columnOffset == 0;

    public bool IsValid(int maximumRadius)
    {
        maximumRadius = Mathf.Max(0, maximumRadius);
        return !IsCenter &&
               Mathf.Abs(rowOffset) <= maximumRadius &&
               Mathf.Abs(columnOffset) <= maximumRadius;
    }

    public static void ValidateList(
        List<CharacterTargetAreaOffset> offsets,
        int maximumRadius)
    {
        if (offsets == null)
            return;

        HashSet<Vector2Int> uniqueOffsets = new();
        for (int index = offsets.Count - 1; index >= 0; index--)
        {
            CharacterTargetAreaOffset offset = offsets[index];
            Vector2Int coordinate = offset != null
                ? new Vector2Int(offset.RowOffset, offset.ColumnOffset)
                : default;
            if (offset == null || !offset.IsValid(maximumRadius) ||
                !uniqueOffsets.Add(coordinate))
            {
                offsets.RemoveAt(index);
            }
        }
    }
}

[Serializable]
public sealed class CharacterAttackDefinition :
    ICharacterConditionalActionDefinition,
    IBattleAbilityDefinition
{
    [SerializeField]
    private string actionId;
    [SerializeField]
    private List<CharacterAttackSectionType> sections = new();
    [SerializeField]
    private AudioClip audioClip;
    [FormerlySerializedAs("condition")]
    [SerializeField]
    private CharacterActionLinkage linkage =
        CharacterActionLinkage.None;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField]
    private CharacterAttackTargetRetentionMode targetRetentionMode;
    [SerializeField, Min(0)]
    private int subjectCount = 1;
    [SerializeField]
    private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField]
    private CharacterAttackDamageType damageType;
    [SerializeField]
    private CharacterDamageAmountMode damageAmountMode;
    [FormerlySerializedAs("damageRatio")]
    [SerializeField, Min(0f)]
    private float damageAmount = 1f;
    [SerializeField, Min(0.1f)]
    private float statusDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float statusStacks = 1f;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private StatusEffectSO statusRemovalEffect;
    [SerializeField]
    private CharacterStatusRemovalTarget statusRemovalTarget;
    [SerializeField]
    private CharacterStatusRemovalPickMode statusRemovalPickMode;
    [SerializeField, Min(1)]
    private int statusRemovalPickCount = 1;
    [SerializeField]
    private CharacterStatusRemovalAmountMode statusRemovalAmountMode;
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField, Range(0.01f, 1f)]
    private float statusRemovalRatio = 0.5f;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private BattleAreaDefinition areaDefinition = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

    public string ActionId => actionId ?? string.Empty;
    public IReadOnlyList<CharacterAttackSectionType> Sections => sections;
    public AudioClip AudioClip => audioClip;
    public bool HasLinkageSection =>
        HasSection(CharacterAttackSectionType.Linkage);
    public CharacterActionLinkage Linkage => linkage;
    public bool HasConditionSection =>
        HasSection(CharacterAttackSectionType.Condition);
    public CharacterConditionMatchMode ConditionMatchMode =>
        conditionMatchMode;
    public IReadOnlyList<CharacterNumericCondition> NumericConditions =>
        numericConditions;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public CharacterAttackTargetRetentionMode TargetRetentionMode =>
        targetRetentionMode;
    public int SubjectCount => subjectCount;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public CharacterAttackDamageType DamageType => damageType;
    public CharacterDamageAmountMode DamageAmountMode => damageAmountMode;
    public float DamageAmount => damageAmount;
    public ScalingValue DamageScaling =>
        ScalingValue.FromLegacy(damageAmountMode, damageAmount);
    public float StatusDuration => statusDuration;
    public float StatusStacks => statusStacks;
    public StatusEffectSO AppliedStatusEffect => statusEffect;
    public StatusEffectSO StatusRemovalEffect => statusRemovalEffect;
    public CharacterStatusRemovalTarget StatusRemovalTarget =>
        statusRemovalTarget;
    public CharacterStatusRemovalPickMode StatusRemovalPickMode =>
        statusRemovalPickMode;
    public int StatusRemovalPickCount => statusRemovalPickCount;
    public CharacterStatusRemovalSelection StatusRemovalSelection =>
        new(
            statusRemovalTarget,
            statusRemovalEffect,
            null,
            statusRemovalPickMode,
            statusRemovalPickCount);
    public CharacterStatusRemovalAmountMode StatusRemovalAmountMode =>
        statusRemovalAmountMode;
    public int StatusRemovalCount => statusRemovalCount;
    public float StatusRemovalRatio => statusRemovalRatio;
    public CharacterStatusRemovalAmount StatusRemovalAmount =>
        new(
            statusRemovalAmountMode,
            statusRemovalCount,
            statusRemovalRatio);
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;
    public string AbilityId => ActionId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => HasExplicitEffects ? 1 : 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.CharacterAttack;
    public BattleAbilityTargeting Targeting =>
        BattleAbilityTargeting.FromCharacter(
            targetFaction,
            subject,
            subjectMetric,
            subjectCount,
            AreaDefinition);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        (IEnumerable<IBattleEffectDefinition>)effects ??
        Array.Empty<IBattleEffectDefinition>();
    public bool UsesLegacyEffectStorage => !HasExplicitEffects;
    public bool HasExecutableContent =>
        HasExplicitEffects || HasSection(CharacterAttackSectionType.Ability);

    public bool HasSection(CharacterAttackSectionType sectionType)
    {
        return sections != null && sections.Contains(sectionType);
    }

    public static bool SupportsTargetRetention(
        CharacterAttackSubject attackSubject,
        int targetCount)
    {
        if (targetCount != 1)
            return false;

        return attackSubject == CharacterAttackSubject.Random ||
               attackSubject == CharacterAttackSubject.HighestValue ||
               attackSubject == CharacterAttackSubject.LowestValue ||
               attackSubject == CharacterAttackSubject.RandomExceptSelf;
    }

    public float CalculateFinalAttackPower(float characterAttackPower)
    {
        return DamageScaling.Evaluate(EffectContext.ForPreview(
            CharacterActionKind.Attack,
            characterAttackPower));
    }

    public void Validate()
    {
        actionId = (actionId ?? string.Empty).Trim();
        sections ??= new List<CharacterAttackSectionType>();
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        if (!Enum.IsDefined(
                typeof(CharacterAttackTargetRetentionMode),
                targetRetentionMode))
        {
            targetRetentionMode =
                CharacterAttackTargetRetentionMode.ReselectEachAttack;
        }
        subjectCount = AreaDefinition.UsesWorldArea
            ? Mathf.Max(0, subjectCount)
            : Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        CharacterStatusRemovalPick.Normalize(
            ref statusRemovalPickMode,
            ref statusRemovalPickCount);
        CharacterStatusRemovalAmount.Normalize(
            ref statusRemovalAmountMode,
            ref statusRemovalCount,
            ref statusRemovalRatio);
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        areaDefinition ??= new BattleAreaDefinition();
        areaDefinition.Validate();
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();
    }

}

[Serializable]
public sealed class CharacterStandingFraming
{
    public static readonly Vector2 DefaultFocus = new(0.5f, 0.6f);
    public const float DefaultZoom = 1.1f;
    public const float MinimumZoom = 1f;
    public const float MaximumZoom = 4f;

    [SerializeField] private Vector2 focusNormalized = new(0.5f, 0.6f);
    [SerializeField, Range(MinimumZoom, MaximumZoom)]
    private float zoom = DefaultZoom;

    public Vector2 FocusNormalized => new(
        Mathf.Clamp01(focusNormalized.x),
        Mathf.Clamp01(focusNormalized.y));
    public float Zoom => Mathf.Clamp(
        zoom,
        MinimumZoom,
        MaximumZoom);

    internal void Validate()
    {
        focusNormalized = new Vector2(
            Mathf.Clamp01(focusNormalized.x),
            Mathf.Clamp01(focusNormalized.y));
        zoom = Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);
    }
}

[CreateAssetMenu(fileName = "Character", menuName = "Dungeon/Character")]
public sealed class CharacterSO : ScriptableObject,
    IBattlePresentationUnitDefinition,
    IBattleAbilityProvider
{
    [SerializeField, HideInInspector] private string characterId;
    [SerializeField] private bool initiallyOwned = true;

    [Header("Profile")]
    [SerializeField] private CharacterGrade grade;
    [SerializeField] private CharacterRoleSO role;
    [SerializeField] private CharacterArchetypeSO archetype;
    [SerializeField] private Sprite standingSprite;
    [SerializeField] private Sprite iconSprite;
    [FormerlySerializedAs("idleSdSprite")]
    [SerializeField] private Sprite waitingSdSprite;
    [SerializeField] private Sprite attackSdSprite;
    [SerializeField] private Sprite damagedSdSprite;
    [SerializeField] private Sprite defeatSdSprite;
    [SerializeField] private Sprite sittingSdSprite;
    [SerializeField] private Sprite skillSdSprite;
    [SerializeField] private Sprite passiveSdSprite;

    [Header("Dungeon HUD Standing Framing")]
    [SerializeField] private CharacterStandingFraming
        dungeonHudStandingFraming = new();

    [Header("World SD Presentation")]
    [SerializeField, Min(0.1f)] private float worldSdScaleMultiplier = 1f;
    [SerializeField] private float worldSdGroundOffset;
    [SerializeField, Range(0.4f, 1.4f)]
    private float worldSdHeadHeightNormalized = 1f;
    [Tooltip("Enable when the source SD sprite faces right by default.")]
    [SerializeField] private bool worldSdFacesRight = true;
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField] private string characterName = "CHARACTER";
    [SerializeField, TextArea(3, 8)] private string characterDescription;

    [Header("3D VFX")]
    [SerializeField] private BattleVfxCueSO spawnVfxCue;
    [SerializeField] private BattleVfxCueSO deathVfxCue;

    [Header("Editor Passive Definitions")]
    [SerializeField]
    private List<CharacterPassiveDefinition> passiveDefinitions = new();

    [Header("Editor Attack Definitions")]
    [SerializeField]
    private List<CharacterAttackDefinition> attackDefinitions = new();

    [Header("Editor Skill Definitions")]
    [SerializeField]
    private CharacterSkillExecutionPolicy skillExecutionPolicy;
    [SerializeField]
    private List<CharacterSkillDefinition> skillDefinitions = new();

    [Header("Editor Cumulative Upgrade Definitions")]
    [SerializeField]
    private List<CharacterCumulativeUpgradeDefinition>
        cumulativeUpgradeDefinitions = new();

    [Header("Editor Dungeon Upgrade Definitions")]
    [SerializeField]
    private List<CharacterDungeonUpgradeDefinition>
        dungeonUpgradeDefinitions = new();

    [Header("Rest Room Skill")]
    [SerializeField] private CharacterRestSkillDefinition restSkill = new();

    [Header("Combat")]
    [SerializeField, Min(1)] private int maximumHealth = 100;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(0), Tooltip(
        "Adds this many cards to every dungeon battle-card draw cycle.")]
    private int judgment;
    [SerializeField, Min(0), Tooltip(
        "Accelerates the dungeon battle-card draw cooldown. Participating " +
        "party Knowledge is added together.")]
    private int knowledge;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.5f;
    [SerializeField, Min(0f)] private float activeSkillRecoveryDuration;

    public Sprite StandingSprite => standingSprite;
    public Sprite IconSprite => iconSprite;
    public Sprite WaitingSdSprite => waitingSdSprite;
    public Sprite AttackSdSprite => attackSdSprite;
    public Sprite DamagedSdSprite => damagedSdSprite;
    public Sprite DefeatSdSprite => defeatSdSprite;
    public Sprite SittingSdSprite => sittingSdSprite;
    public Sprite SkillSdSprite => skillSdSprite;
    public Sprite PassiveSdSprite => passiveSdSprite;
    public Vector2 DungeonHudStandingFocus =>
        dungeonHudStandingFraming?.FocusNormalized ??
        CharacterStandingFraming.DefaultFocus;
    public float DungeonHudStandingZoom =>
        dungeonHudStandingFraming?.Zoom ??
        CharacterStandingFraming.DefaultZoom;
    public float WorldSdScaleMultiplier =>
        Mathf.Max(0.1f, worldSdScaleMultiplier);
    public float WorldSdGroundOffset => worldSdGroundOffset;
    public float WorldSdHeadHeightNormalized =>
        Mathf.Clamp(worldSdHeadHeightNormalized, 0.4f, 1.4f);
    public bool WorldSdFacesRight => worldSdFacesRight;
    public string CharacterId => !string.IsNullOrWhiteSpace(characterId)
        ? characterId
        : name;
    public bool InitiallyOwned => initiallyOwned;
    public CharacterGrade Grade =>
        CharacterGradePresentation.Clamp(grade);
    public CharacterRoleSO Role => role;
    public CharacterArchetypeSO Archetype => archetype;
    public string NameLocalizationKey => nameLocalizationKey;
    public string DescriptionLocalizationKey => descriptionLocalizationKey;
    public string CharacterName => characterName;
    public string CharacterDescription => characterDescription;
    public BattleVfxCueSO SpawnVfxCue => spawnVfxCue;
    public BattleVfxCueSO DeathVfxCue => deathVfxCue;
    public IReadOnlyList<CharacterPassiveDefinition> PassiveDefinitions =>
        passiveDefinitions;
    public IReadOnlyList<CharacterAttackDefinition> AttackDefinitions =>
        attackDefinitions;
    public IReadOnlyList<CharacterSkillDefinition> SkillDefinitions =>
        skillDefinitions;
    public CharacterSkillExecutionPolicy SkillExecutionPolicy =>
        skillExecutionPolicy;
    public IReadOnlyList<CharacterCumulativeUpgradeDefinition>
        CumulativeUpgradeDefinitions => cumulativeUpgradeDefinitions;
    public IReadOnlyList<CharacterDungeonUpgradeDefinition>
        DungeonUpgradeDefinitions => dungeonUpgradeDefinitions;
    public CharacterRestSkillDefinition RestSkill => restSkill;
    public int AttackPower => attackPower;
    public int MaximumHealth => maximumHealth;
    public int Judgment => Mathf.Max(0, judgment);
    public int Knowledge => Mathf.Max(0, knowledge);
    public float AttackCooldown => TimePrecision.Normalize(attackCooldown, 0.1f);
    public float AttackRecoveryDuration =>
        TimePrecision.Normalize(attackRecoveryDuration);
    public float ActiveSkillRecoveryDuration =>
        TimePrecision.Normalize(activeSkillRecoveryDuration);

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        if (attackDefinitions != null)
        {
            foreach (CharacterAttackDefinition definition in
                     attackDefinitions)
            {
                if (definition?.HasExplicitEffects == true &&
                    definition.HasSection(
                        CharacterAttackSectionType.Ability))
                    yield return definition;
            }
        }
        if (skillDefinitions != null)
        {
            foreach (CharacterSkillDefinition definition in
                     skillDefinitions)
            {
                if (definition?.HasExplicitEffects == true &&
                    definition.HasSection(
                        CharacterSkillSectionType.Ability))
                    yield return definition;
            }
        }
        if (passiveDefinitions != null)
        {
            foreach (CharacterPassiveDefinition definition in
                     passiveDefinitions)
            {
                if (definition?.HasExplicitEffects == true &&
                    definition.HasSection(
                        CharacterPassiveSectionType.Ability))
                    yield return definition;
            }
        }
        if (role != null)
        {
            foreach (IBattleAbilityDefinition definition in
                     role.EnumerateBattleAbilities())
            {
                yield return definition;
            }
        }
        if (archetype != null)
        {
            foreach (IBattleAbilityDefinition definition in
                     archetype.EnumerateBattleAbilities())
            {
                yield return definition;
            }
        }
    }

    public CharacterCumulativeUpgradeDefinition
        GetCumulativeUpgradeDefinition(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId) ||
            cumulativeUpgradeDefinitions == null)
        {
            return null;
        }

        foreach (CharacterCumulativeUpgradeDefinition definition in
                 cumulativeUpgradeDefinitions)
        {
            if (definition != null && string.Equals(
                    definition.UpgradeId,
                    upgradeId,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        CharacterDefinitionCatalog.Invalidate();
    }

    public CharacterData CreateData()
    {
        return new CharacterData(
            this,
            new CharacterProgressData(CharacterId, InitiallyOwned));
    }

    public CharacterData CreateData(CharacterProgressData progress)
    {
        return new CharacterData(this, progress);
    }

    public void RegenerateCharacterId()
    {
        characterId = Guid.NewGuid().ToString("N");
    }
}

public static class CharacterDefinitionCatalog
{
    private const string ResourcesPath = "Characters";

    private static readonly List<CharacterSO> Definitions = new();
    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Definitions.Clear();
        _loaded = false;
    }

    public static IReadOnlyList<CharacterSO> GetAll()
    {
        EnsureLoaded();
        return Definitions;
    }

    public static void Invalidate()
    {
        Definitions.Clear();
        _loaded = false;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Definitions.Clear();
        CharacterSO[] loaded = Resources.LoadAll<CharacterSO>(ResourcesPath);
        HashSet<string> characterIds = new(StringComparer.Ordinal);
        foreach (CharacterSO definition in loaded)
        {
            if (definition == null)
                continue;

            CharacterDefinitionValidationResult ownerValidation =
                CharacterDefinitionValidator.Validate(definition);
            if (!ownerValidation.IsValid)
            {
                Debug.LogError(
                    $"CharacterSO '{definition.name}' was excluded: " +
                    $"{ownerValidation.ErrorCount} owner validation " +
                    "error(s).",
                    definition);
                continue;
            }

            if (!AbilityDefinitionValidator.TryValidateProvider(
                    definition,
                    out string abilityError))
            {
                Debug.LogError(
                    $"CharacterSO '{definition.name}' was excluded: " +
                    abilityError,
                    definition);
                continue;
            }

            string characterId = definition.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
            {
                Debug.LogError(
                    $"CharacterSO '{definition.name}' has no persistent id.",
                    definition);
                continue;
            }

            if (!characterIds.Add(characterId))
            {
                Debug.LogError(
                    $"Duplicate character id '{characterId}' " +
                    $"under Resources/{ResourcesPath}.",
                    definition);
                continue;
            }

            Definitions.Add(definition);
        }

        Definitions.Sort((left, right) => string.Compare(
            left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty,
            StringComparison.OrdinalIgnoreCase));
        _loaded = true;
    }
}
