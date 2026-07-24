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
    None = 2
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
    None
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
    Health,
    HealthPercentage,
    StackCount,
    AttackPower,
    AttackSpeed,
    Shield
}

public enum CharacterConditionType
{
    Numeric,
    HasStatus
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
    Shield = 7
}

public enum CharacterEffectTargetMode
{
    InheritAction = 0,
    Source = 1,
    FreshSelection = 2
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
    Single,
    Random,
    All
}

public enum CharacterPassiveSectionType
{
    Linkage = 0,
    Ability = 1,
    Subject = 2,
    Condition = 3,
    SelfStatusCost = 4
}

public enum CharacterPassiveTrigger
{
    OnAttack,
    OnStatusAcquired,
    OnCooldown
}

public enum CharacterPassiveStatusTarget
{
    Enemy,
    Ally,
    All
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

public enum CharacterCumulativeUpgradeModifierType
{
    AttackPower = 0,
    MaximumHealth = 1,
    AttackCooldown = 2,
    PassiveDamage = 3,
    AttackDamage = 4,
    SkillDamage = 5,
    SkillCostReduction = 6
}

[Serializable]
public sealed class CharacterNumericCondition
{
    [SerializeField]
    private CharacterConditionType type;
    [SerializeField]
    private CharacterNumericConditionMetric metric;
    [SerializeField]
    private CharacterNumericComparison comparison;
    [SerializeField]
    private float threshold;

    [SerializeField]
    private StatusEffectSO statusEffect;

    public CharacterConditionType Type => type;
    public CharacterNumericConditionMetric Metric => metric;
    public CharacterNumericComparison Comparison => comparison;
    public float Threshold => threshold;
    public StatusEffectSO StatusEffect => statusEffect;

    public void Validate()
    {
        if (float.IsNaN(threshold) || float.IsInfinity(threshold))
            threshold = 0f;
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
    public bool HasNumericConditions => NumericConditions.Count > 0;

    public void Validate()
    {
        subjectCount = Mathf.Max(1, subjectCount);
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        CharacterTargetAreaOffset.ValidateList(
            areaOffsets,
            DungeonBoardView.MaximumGridSize / 2);
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
    [SerializeField, Min(0)]
    private int maxLevel = 1;
    [SerializeField]
    private List<CharacterCumulativeUpgradeModifier> modifiers = new();

    public string UpgradeId => upgradeId ?? string.Empty;
    public int MaxLevel => Mathf.Max(0, maxLevel);
    public bool HasUnlimitedMaxLevel => MaxLevel == 0;
    public IReadOnlyList<CharacterCumulativeUpgradeModifier> Modifiers =>
        modifiers != null
            ? modifiers
            : Array.Empty<CharacterCumulativeUpgradeModifier>();

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
        maxLevel = Mathf.Max(0, maxLevel);
        modifiers ??= new List<CharacterCumulativeUpgradeModifier>();
        foreach (CharacterCumulativeUpgradeModifier modifier in modifiers)
            modifier?.Validate();
    }
}

[Serializable]
public sealed class CharacterDungeonUpgradeEntry
{
    [SerializeField]
    private CharacterDungeonUpgradeType type;
    [SerializeField, Range(0f, 100f)]
    private float probability;
    [SerializeField, Min(0)]
    private int limit = 1;

    public CharacterDungeonUpgradeType Type => type;
    public float Probability => probability;
    public int Limit => limit;
    public bool HasUnlimitedLimit => limit == 0;
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
        probability = Mathf.Clamp(probability, 0f, 100f);
        limit = Mathf.Max(0, limit);
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

    public void Validate()
    {
        entries ??= new List<CharacterDungeonUpgradeEntry>();
        foreach (CharacterDungeonUpgradeEntry entry in entries)
            entry?.Validate();
    }
}

[Serializable]
public sealed class CharacterEffectDefinition : IBattleEffectDefinition
{
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
    [SerializeField, Min(0.1f)]
    private float statusDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float statusStacks = 1f;
    [SerializeField]
    private StatusEffectSO statusEffect;
    [SerializeField]
    private CharacterStatusRemovalTarget statusRemovalTarget;
    [SerializeField, Min(0)]
    private int statusRemovalCount;

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
        type != CharacterEffectType.GainResource &&
        type != CharacterEffectType.SpendResource &&
        type != CharacterEffectType.SpendHealth &&
        targetMode != CharacterEffectTargetMode.Source &&
        targetMode != CharacterEffectTargetMode.FreshSelection;
    public CharacterAttackDamageType DamageType => damageType;
    public CharacterDamageAmountMode DamageAmountMode => damageAmountMode;
    public float DamageAmount => damageAmount;
    public CharacterDamageAmountMode AmountMode => damageAmountMode;
    public float Amount => damageAmount;
    public float SourceResourceScale => sourceResourceScale;
    public float TargetCurrentHealthScale => targetCurrentHealthScale;
    public float TargetMaxHealthScale => targetMaxHealthScale;
    public StatusEffectSO SourceStatusScalingEffect =>
        sourceStatusScalingEffect;
    public float SourceStatusStacksScale => sourceStatusStacksScale;
    public StatusEffectSO TargetStatusScalingEffect =>
        targetStatusScalingEffect;
    public float TargetStatusStacksScale => targetStatusStacksScale;
    public ScalingValue AmountScaling =>
        ScalingValue.FromLegacy(damageAmountMode, damageAmount) +
        ScalingValue.SourceResource(sourceResourceScale) +
        ScalingValue.TargetCurrentHealth(targetCurrentHealthScale) +
        ScalingValue.TargetMaximumHealth(targetMaxHealthScale) +
        ScalingValue.SourceStatusStacks(sourceStatusStacksScale) +
        ScalingValue.TargetStatusStacks(targetStatusStacksScale);
    public ScalingValue DamageScaling => AmountScaling;
    public float StatusDuration => statusDuration;
    public float StatusStacks => statusStacks;
    public StatusEffectSO StatusEffect => statusEffect;
    public CharacterStatusRemovalTarget StatusRemovalTarget =>
        statusRemovalTarget;
    public int StatusRemovalCount => statusRemovalCount;

    public void Validate()
    {
        targetSelector ??= new CharacterEffectTargetSelector();
        targetSelector.Validate();
        if (float.IsNaN(damageAmount) || float.IsInfinity(damageAmount))
            damageAmount = 0f;
        if (float.IsNaN(sourceResourceScale) ||
            float.IsInfinity(sourceResourceScale))
        {
            sourceResourceScale = 0f;
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
        statusRemovalCount = Mathf.Max(0, statusRemovalCount);
    }
}

[Serializable]
public sealed class CharacterSkillDefinition :
    ICharacterConditionalActionDefinition
{
    [SerializeField]
    private List<CharacterSkillSectionType> sections = new();
    [SerializeField]
    private AudioClip audioClip;
    [SerializeField, Min(1)]
    private int cost = 1;
    [FormerlySerializedAs("condition")]
    [SerializeField]
    private CharacterActionLinkage linkage;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField, Min(1)]
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
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

    public IReadOnlyList<CharacterSkillSectionType> Sections => sections;
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
    public int StatusRemovalCount => statusRemovalCount;
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;

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
        sections ??= new List<CharacterSkillSectionType>();
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        cost = Mathf.Max(1, cost);
        subjectCount = Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        statusRemovalCount = Mathf.Max(0, statusRemovalCount);
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        CharacterTargetAreaOffset.ValidateList(
            areaOffsets,
            DungeonBoardView.MaximumGridSize / 2);
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
    ICharacterConditionalActionDefinition
{
    [SerializeField]
    private List<CharacterPassiveSectionType> sections = new();
    [SerializeField]
    private AudioClip audioClip;
    [SerializeField]
    private CharacterPassiveTrigger trigger;
    [SerializeField]
    private CharacterPassiveStatusTarget statusTarget;
    [SerializeField]
    private StatusEffectSO triggerStatusEffect;
    [SerializeField, Min(0.1f)]
    private float cooldown = 1f;
    [FormerlySerializedAs("detailCondition")]
    [SerializeField]
    private CharacterActionLinkage linkage;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField, Min(1)]
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
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private CharacterStatusStackCostDefinition selfStatusCost = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

    public IReadOnlyList<CharacterPassiveSectionType> Sections => sections;
    public AudioClip AudioClip => audioClip;
    public CharacterPassiveTrigger Trigger => trigger;
    public CharacterPassiveStatusTarget StatusTarget => statusTarget;
    public StatusEffectSO TriggerStatusEffect => triggerStatusEffect;
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
    public int StatusRemovalCount => statusRemovalCount;
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public CharacterStatusStackCostDefinition SelfStatusCost =>
        selfStatusCost;
    public bool HasSelfStatusCost =>
        HasSection(CharacterPassiveSectionType.SelfStatusCost) &&
        selfStatusCost != null && selfStatusCost.IsConfigured;
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;

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
        sections ??= new List<CharacterPassiveSectionType>();
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        cooldown = TimePrecision.Normalize(cooldown, TimePrecision.Step);
        subjectCount = Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        statusRemovalCount = Mathf.Max(0, statusRemovalCount);
        selfStatusCost ??= new CharacterStatusStackCostDefinition();
        selfStatusCost.Validate();
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        CharacterTargetAreaOffset.ValidateList(
            areaOffsets,
            DungeonBoardView.MaximumGridSize / 2);
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
    ICharacterConditionalActionDefinition
{
    [SerializeField]
    private List<CharacterAttackSectionType> sections = new();
    [SerializeField]
    private AudioClip audioClip;
    [FormerlySerializedAs("condition")]
    [SerializeField]
    private CharacterActionLinkage linkage;
    [SerializeField]
    private CharacterConditionMatchMode conditionMatchMode;
    [SerializeField]
    private List<CharacterNumericCondition> numericConditions = new();
    [SerializeField]
    private CharacterTargetFaction targetFaction;
    [SerializeField]
    private CharacterAttackSubject subject;
    [SerializeField, Min(1)]
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
    [SerializeField, Min(0)]
    private int statusRemovalCount;
    [SerializeField]
    private List<CharacterTargetAreaOffset> areaOffsets = new();
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();

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
    public int StatusRemovalCount => statusRemovalCount;
    public IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets =>
        areaOffsets;
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public bool HasExplicitEffects => effects != null && effects.Count > 0;

    public bool HasSection(CharacterAttackSectionType sectionType)
    {
        return sections != null && sections.Contains(sectionType);
    }

    public float CalculateFinalAttackPower(float characterAttackPower)
    {
        return DamageScaling.Evaluate(EffectContext.ForPreview(
            CharacterActionKind.Attack,
            characterAttackPower));
    }

    public void Validate()
    {
        sections ??= new List<CharacterAttackSectionType>();
        numericConditions ??= new List<CharacterNumericCondition>();
        foreach (CharacterNumericCondition condition in numericConditions)
            condition?.Validate();
        NormalizeLegacySections();
        subjectCount = Mathf.Max(1, subjectCount);
        damageAmount = Mathf.Max(0f, damageAmount);
        statusDuration = TimePrecision.Normalize(statusDuration, 0.1f);
        statusStacks = Mathf.Max(0.1f, statusStacks);
        statusRemovalCount = Mathf.Max(0, statusRemovalCount);
        areaOffsets ??= new List<CharacterTargetAreaOffset>();
        CharacterTargetAreaOffset.ValidateList(
            areaOffsets,
            DungeonBoardView.MaximumGridSize / 2);
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();
    }

    private void NormalizeLegacySections()
    {
        bool hasAbility = sections.Contains(
            CharacterAttackSectionType.Ability);
        for (int index = sections.Count - 1; index >= 0; index--)
        {
            if (sections[index] !=
                CharacterAttackSectionType.LegacyDamageAmount)
            {
                continue;
            }

            if (hasAbility)
            {
                sections.RemoveAt(index);
                continue;
            }

            sections[index] = CharacterAttackSectionType.Ability;
            hasAbility = true;
        }
    }
}

[CreateAssetMenu(fileName = "Character", menuName = "Dungeon/Character")]
public sealed class CharacterSO : ScriptableObject
{
    [SerializeField, HideInInspector] private string characterId;
    [SerializeField] private bool initiallyOwned = true;

    [Header("Profile")]
    [SerializeField] private Sprite standingSprite;
    [SerializeField] private Sprite iconSprite;
    [FormerlySerializedAs("idleSdSprite")]
    [SerializeField] private Sprite waitingSdSprite;
    [SerializeField] private Sprite attackSdSprite;
    [SerializeField] private Sprite damagedSdSprite;
    [SerializeField] private Sprite skillSdSprite;
    [SerializeField] private Sprite passiveSdSprite;
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField] private string characterName = "CHARACTER";
    [SerializeField, TextArea(3, 8)] private string characterDescription;

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

    [Header("Combat")]
    [SerializeField, Min(1)] private int maximumHealth = 100;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.5f;
    [SerializeField, Min(0f)] private float activeSkillRecoveryDuration;

    public Sprite StandingSprite => standingSprite;
    public Sprite IconSprite => iconSprite;
    public Sprite WaitingSdSprite => waitingSdSprite;
    public Sprite AttackSdSprite => attackSdSprite;
    public Sprite DamagedSdSprite => damagedSdSprite;
    public Sprite SkillSdSprite => skillSdSprite;
    public Sprite PassiveSdSprite => passiveSdSprite;
    public string CharacterId => !string.IsNullOrWhiteSpace(characterId)
        ? characterId
        : name;
    public bool InitiallyOwned => initiallyOwned;
    public string NameLocalizationKey => nameLocalizationKey;
    public string DescriptionLocalizationKey => descriptionLocalizationKey;
    public string CharacterName => characterName;
    public string CharacterDescription => characterDescription;
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
    public int AttackPower => attackPower;
    public int MaximumHealth => maximumHealth;
    public float AttackCooldown => TimePrecision.Normalize(attackCooldown, 0.1f);
    public float AttackRecoveryDuration =>
        TimePrecision.Normalize(attackRecoveryDuration);
    public float ActiveSkillRecoveryDuration =>
        TimePrecision.Normalize(activeSkillRecoveryDuration);

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
        if (string.IsNullOrWhiteSpace(characterId))
            RegenerateCharacterId();

        passiveDefinitions ??= new List<CharacterPassiveDefinition>();
        foreach (CharacterPassiveDefinition definition in passiveDefinitions)
            definition?.Validate();

        attackDefinitions ??= new List<CharacterAttackDefinition>();
        foreach (CharacterAttackDefinition definition in attackDefinitions)
            definition?.Validate();

        skillDefinitions ??= new List<CharacterSkillDefinition>();
        foreach (CharacterSkillDefinition definition in skillDefinitions)
            definition?.Validate();

        cumulativeUpgradeDefinitions ??=
            new List<CharacterCumulativeUpgradeDefinition>();
        foreach (CharacterCumulativeUpgradeDefinition definition in
                 cumulativeUpgradeDefinitions)
        {
            definition?.Validate();
        }

        dungeonUpgradeDefinitions ??=
            new List<CharacterDungeonUpgradeDefinition>();
        foreach (CharacterDungeonUpgradeDefinition definition in
                 dungeonUpgradeDefinitions)
        {
            definition?.Validate();
        }

        maximumHealth = Mathf.Max(1, maximumHealth);
        attackPower = Mathf.Max(1, attackPower);
        attackCooldown = TimePrecision.Normalize(attackCooldown, 0.1f);
        attackRecoveryDuration = TimePrecision.Normalize(
            Mathf.Max(0f, attackRecoveryDuration));
        activeSkillRecoveryDuration = TimePrecision.Normalize(
            Mathf.Max(0f, activeSkillRecoveryDuration));
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

            if (!characterIds.Add(definition.CharacterId))
            {
                Debug.LogError(
                    $"Duplicate character id '{definition.CharacterId}' " +
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
