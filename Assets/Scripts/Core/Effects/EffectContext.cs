using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleEffectType
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

public enum BattleEffectTargetMode
{
    InheritContext = 0,
    Source = 1,
    FreshSelection = 2
}

public enum BattleEffectPreconditionFailurePolicy
{
    AbortSequence = 0,
    SkipEffect = 1
}

public enum BattleEffectFailurePolicy
{
    Continue = 0,
    StopRemainingEffects = 1
}

public interface IBattleEffectTargetSelector
{
    CharacterTargetFaction TargetFaction { get; }
    CharacterAttackSubject Subject { get; }
    CharacterAttackSubjectMetric SubjectMetric { get; }
    int SubjectCount { get; }
    CharacterConditionMatchMode ConditionMatchMode { get; }
    IReadOnlyList<CharacterNumericCondition> NumericConditions { get; }
    IReadOnlyList<CharacterTargetAreaOffset> AreaOffsets { get; }
    bool HasNumericConditions { get; }
}

public interface IBattleEffectDefinition
{
    BattleEffectType BattleEffectType { get; }
    BattleEffectTargetMode BattleTargetMode { get; }
    BattleEffectPreconditionFailurePolicy
        BattlePreconditionFailurePolicy { get; }
    BattleEffectFailurePolicy BattleFailurePolicy { get; }
    IBattleEffectTargetSelector BattleTargetSelector { get; }
    bool RequiresActionTargets { get; }
    CharacterAttackDamageType DamageType { get; }
    ScalingValue AmountScaling { get; }
    StatusEffectSO SourceStatusScalingEffect { get; }
    StatusEffectSO TargetStatusScalingEffect { get; }
    float StatusDuration { get; }
    float StatusStacks { get; }
    StatusEffectSO StatusEffect { get; }
    CharacterStatusRemovalTarget StatusRemovalTarget { get; }
    CharacterStatusRemovalSelection StatusRemovalSelection { get; }
    CharacterStatusRemovalAmountMode StatusRemovalAmountMode { get; }
    int StatusRemovalCount { get; }
    float StatusRemovalRatio { get; }
    CharacterStatusRemovalAmount StatusRemovalAmount { get; }
}

/// <summary>
/// Execution boundary shared by battle abilities and run/room actions.
/// The domain is deliberately explicit so a room effect cannot be sent to
/// the battle executor by mistake.
/// </summary>
public enum AbilityExecutionDomain
{
    Battle = 0,
    Run = 1
}

public enum BattleAbilityTargetRelation
{
    None = 0,
    Self = 1,
    Friendly = 2,
    Hostile = 3,
    Any = 4,
    Objective = 5
}

public enum BattleAbilitySelectionMode
{
    None = 0,
    Inherit = 1,
    Self = 2,
    All = 3,
    Random = 4,
    HighestValue = 5,
    LowestValue = 6,
    Adjacent = 7,
    Manual = 8,
    AllExceptSelf = 9,
    RandomExceptSelf = 10
}

public enum BattleAbilityTargetMetric
{
    None = 0,
    Health = 1,
    HealthPercentage = 2,
    StackCount = 3,
    AttackPower = 4,
    AttackSpeed = 5,
    Shield = 6,
    TotalDamageDealt = 7
}

/// <summary>
/// Neutral, read-only targeting view used by every ability owner. Existing
/// serialized fields remain the source of truth during the compatibility
/// migration; adapters expose them through this contract.
/// </summary>
public readonly struct BattleAbilityTargeting
{
    private readonly IReadOnlyList<CharacterTargetAreaOffset>
        _legacyAreaOffsets;

    public BattleAbilityTargetRelation Relation { get; }
    public BattleAbilitySelectionMode SelectionMode { get; }
    public BattleAbilityTargetMetric Metric { get; }
    public int TargetCount { get; }
    public int Range { get; }
    public bool IncludeDiagonals { get; }
    public BattleAreaDefinition AreaDefinition { get; }
    public IReadOnlyList<CharacterTargetAreaOffset> LegacyAreaOffsets =>
        _legacyAreaOffsets ?? Array.Empty<CharacterTargetAreaOffset>();
    public bool HasTarget =>
        Relation != BattleAbilityTargetRelation.None &&
        SelectionMode != BattleAbilitySelectionMode.None;
    public bool UsesWorldArea => AreaDefinition?.UsesWorldArea == true;
    public bool UsesLegacyTileArea =>
        !UsesWorldArea && LegacyAreaOffsets.Count > 0;
    public bool IsValid =>
        Enum.IsDefined(typeof(BattleAbilityTargetRelation), Relation) &&
        Enum.IsDefined(typeof(BattleAbilitySelectionMode), SelectionMode) &&
        Enum.IsDefined(typeof(BattleAbilityTargetMetric), Metric) &&
        TargetCount >= 0 &&
        Range >= 0 &&
        (Relation != BattleAbilityTargetRelation.None ||
         SelectionMode == BattleAbilitySelectionMode.None ||
         SelectionMode == BattleAbilitySelectionMode.Inherit);

    public BattleAbilityTargeting(
        BattleAbilityTargetRelation relation,
        BattleAbilitySelectionMode selectionMode,
        BattleAbilityTargetMetric metric = BattleAbilityTargetMetric.None,
        int targetCount = 1,
        int range = 0,
        bool includeDiagonals = false,
        BattleAreaDefinition areaDefinition = null,
        IReadOnlyList<CharacterTargetAreaOffset> legacyAreaOffsets = null)
    {
        Relation = relation;
        SelectionMode = selectionMode;
        Metric = metric;
        TargetCount = Mathf.Max(0, targetCount);
        Range = Mathf.Max(0, range);
        IncludeDiagonals = includeDiagonals;
        AreaDefinition = areaDefinition;
        _legacyAreaOffsets = legacyAreaOffsets ??
            Array.Empty<CharacterTargetAreaOffset>();
    }

    public static BattleAbilityTargeting FromCharacter(
        CharacterTargetFaction faction,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        BattleAreaDefinition areaDefinition = null,
        IReadOnlyList<CharacterTargetAreaOffset> legacyAreaOffsets = null)
    {
        return new BattleAbilityTargeting(
            faction == CharacterTargetFaction.Ally
                ? BattleAbilityTargetRelation.Friendly
                : BattleAbilityTargetRelation.Hostile,
            ToSelectionMode(subject),
            ToTargetMetric(metric),
            targetCount,
            areaDefinition?.UsesWorldArea == true
                ? Mathf.CeilToInt(areaDefinition.MaxCastDistance)
                : 0,
            false,
            areaDefinition,
            legacyAreaOffsets);
    }

    public static BattleAbilityTargeting FromEnemy(
        EnemyAbilityTargetDefinition target)
    {
        if (target == null)
            return default;

        BattleAbilityTargetRelation relation = target.Faction switch
        {
            EnemyAbilityTargetFaction.Self =>
                BattleAbilityTargetRelation.Self,
            EnemyAbilityTargetFaction.EnemyAllies =>
                BattleAbilityTargetRelation.Friendly,
            EnemyAbilityTargetFaction.PlayerCharacters =>
                BattleAbilityTargetRelation.Hostile,
            _ => BattleAbilityTargetRelation.None,
        };
        BattleAbilitySelectionMode selection = target.Subject switch
        {
            EnemyAbilityTargetSubject.Self =>
                BattleAbilitySelectionMode.Self,
            EnemyAbilityTargetSubject.All =>
                BattleAbilitySelectionMode.All,
            EnemyAbilityTargetSubject.Random =>
                BattleAbilitySelectionMode.Random,
            EnemyAbilityTargetSubject.HighestValue =>
                BattleAbilitySelectionMode.HighestValue,
            EnemyAbilityTargetSubject.LowestValue =>
                BattleAbilitySelectionMode.LowestValue,
            EnemyAbilityTargetSubject.Adjacent =>
                BattleAbilitySelectionMode.Adjacent,
            _ => BattleAbilitySelectionMode.None,
        };
        BattleAbilityTargetMetric metric = target.Metric switch
        {
            EnemyAbilityTargetMetric.Health =>
                BattleAbilityTargetMetric.Health,
            EnemyAbilityTargetMetric.HealthPercentage =>
                BattleAbilityTargetMetric.HealthPercentage,
            EnemyAbilityTargetMetric.Shield =>
                BattleAbilityTargetMetric.Shield,
            EnemyAbilityTargetMetric.TotalDamageDealt =>
                BattleAbilityTargetMetric.TotalDamageDealt,
            EnemyAbilityTargetMetric.StackCount =>
                BattleAbilityTargetMetric.StackCount,
            _ => BattleAbilityTargetMetric.None,
        };
        return new BattleAbilityTargeting(
            relation,
            selection,
            metric,
            target.TargetCount,
            target.Range,
            target.IncludeDiagonals);
    }

    private static BattleAbilitySelectionMode ToSelectionMode(
        CharacterAttackSubject subject)
    {
        return subject switch
        {
            CharacterAttackSubject.Random =>
                BattleAbilitySelectionMode.Random,
            CharacterAttackSubject.All =>
                BattleAbilitySelectionMode.All,
            CharacterAttackSubject.HighestValue =>
                BattleAbilitySelectionMode.HighestValue,
            CharacterAttackSubject.LowestValue =>
                BattleAbilitySelectionMode.LowestValue,
            CharacterAttackSubject.Self =>
                BattleAbilitySelectionMode.Self,
            CharacterAttackSubject.AllExceptSelf =>
                BattleAbilitySelectionMode.AllExceptSelf,
            CharacterAttackSubject.RandomExceptSelf =>
                BattleAbilitySelectionMode.RandomExceptSelf,
            CharacterAttackSubject.Manual =>
                BattleAbilitySelectionMode.Manual,
            _ => BattleAbilitySelectionMode.None,
        };
    }

    private static BattleAbilityTargetMetric ToTargetMetric(
        CharacterAttackSubjectMetric metric)
    {
        return metric switch
        {
            CharacterAttackSubjectMetric.Health =>
                BattleAbilityTargetMetric.Health,
            CharacterAttackSubjectMetric.StackCount =>
                BattleAbilityTargetMetric.StackCount,
            CharacterAttackSubjectMetric.AttackPower =>
                BattleAbilityTargetMetric.AttackPower,
            CharacterAttackSubjectMetric.AttackSpeed =>
                BattleAbilityTargetMetric.AttackSpeed,
            CharacterAttackSubjectMetric.Shield =>
                BattleAbilityTargetMetric.Shield,
            _ => BattleAbilityTargetMetric.None,
        };
    }
}

public interface IAbilityDefinition
{
    string AbilityId { get; }
    AbilityExecutionDomain ExecutionDomain { get; }
    int AbilitySchemaVersion { get; }
}

/// <summary>
/// Common battle ability projection. Owner-specific activation rules such as
/// passive triggers, item charges, or enemy operations stay on the owner.
/// </summary>
public interface IBattleAbilityDefinition : IAbilityDefinition
{
    BattleEffectOriginKind OriginKind { get; }
    BattleAbilityTargeting Targeting { get; }
    IEnumerable<IBattleEffectDefinition> BattleEffects { get; }
    bool UsesLegacyEffectStorage { get; }
    bool HasExecutableContent { get; }
}

public interface IBattleAbilityProvider
{
    IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities();
}

public interface IRunAbilityDefinition : IAbilityDefinition
{
}

public static class AbilityDefinitionValidator
{
    public static bool TryValidateProvider(
        IBattleAbilityProvider provider,
        out string error)
    {
        if (provider == null)
        {
            error = "Battle ability provider is null.";
            return false;
        }

        HashSet<string> abilityIds = new(StringComparer.Ordinal);
        IEnumerable<IBattleAbilityDefinition> abilities =
            provider.EnumerateBattleAbilities();
        if (abilities == null)
        {
            error = "Battle ability provider returned a null sequence.";
            return false;
        }

        int index = 0;
        foreach (IBattleAbilityDefinition ability in abilities)
        {
            if (!TryValidate(ability, out string abilityError))
            {
                error = $"Ability {index + 1}: {abilityError}";
                return false;
            }
            if (!abilityIds.Add(ability.AbilityId))
            {
                error = $"Ability ID '{ability.AbilityId}' is duplicated.";
                return false;
            }
            index++;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidate(
        IAbilityDefinition definition,
        out string error)
    {
        if (definition == null)
        {
            error = "Ability definition is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.AbilityId))
        {
            error = "Ability ID is required.";
            return false;
        }

        if (definition.AbilitySchemaVersion < 0)
        {
            error = $"Ability '{definition.AbilityId}' has an invalid " +
                    "schema version.";
            return false;
        }

        if (definition is IBattleAbilityDefinition battleDefinition)
        {
            if (battleDefinition.ExecutionDomain !=
                AbilityExecutionDomain.Battle)
            {
                error = $"Ability '{definition.AbilityId}' is exposed as " +
                        "a battle ability with a non-battle domain.";
                return false;
            }
            if (!battleDefinition.Targeting.IsValid)
            {
                error = $"Ability '{definition.AbilityId}' has invalid " +
                        "targeting data.";
                return false;
            }
            if (!battleDefinition.HasExecutableContent)
            {
                error = $"Ability '{definition.AbilityId}' has no " +
                        "executable content.";
                return false;
            }

            IEnumerable<IBattleEffectDefinition> effects =
                battleDefinition.BattleEffects;
            if (effects != null)
            {
                int index = 0;
                foreach (IBattleEffectDefinition effect in effects)
                {
                    if (effect == null)
                    {
                        error = $"Ability '{definition.AbilityId}' effect " +
                                $"{index + 1} is null.";
                        return false;
                    }
                    index++;
                }
            }
        }
        else if (definition.ExecutionDomain ==
                 AbilityExecutionDomain.Battle)
        {
            error = $"Ability '{definition.AbilityId}' declares the battle " +
                    "domain without the battle ability contract.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public enum CharacterActionKind
{
    Attack = 0,
    Passive = 1,
    Skill = 2
}

public readonly struct EffectContext
{
    private readonly IReadOnlyList<EnemyRuntime> _enemyTargets;
    private readonly IReadOnlyList<IBattleCharacter> _allyTargets;

    public BattleStatusTarget SourceTarget { get; }
    public IBattleCharacter Source => SourceTarget.Ally;
    public IBattleBoard Board { get; }
    public IActiveSkillResource Resource { get; }
    public CharacterActionKind ActionKind { get; }
    public CharacterTargetFaction TargetFaction { get; }
    public float SourceAttackPower { get; }
    public int SourceCurrentHealth => SourceTarget.CurrentHealth;
    public int SourceMaximumHealth => SourceTarget.MaximumHealth;
    public int SourceResource { get; }
    public int SourceResourceMaximum { get; }
    public BattleStatusTarget Target { get; }
    public bool HasBoundTarget => Target.IsValid;
    public bool HasTargetHealth => Target.IsValid;
    public int TargetCurrentHealth { get; }
    public int TargetMaximumHealth { get; }
    public int SourceStatusStacks { get; }
    public int TargetStatusStacks { get; }
    public IReadOnlyList<EnemyRuntime> EnemyTargets =>
        _enemyTargets ?? Array.Empty<EnemyRuntime>();
    public IReadOnlyList<IBattleCharacter> AllyTargets =>
        _allyTargets ?? Array.Empty<IBattleCharacter>();
    public int TargetCount => TargetFaction == CharacterTargetFaction.Ally
        ? AllyTargets.Count
        : EnemyTargets.Count;
    public bool HasTargets => TargetCount > 0;

    public EffectContext(
        IBattleCharacter source,
        IBattleBoard board,
        IActiveSkillResource resource,
        CharacterActionKind actionKind,
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets,
        float sourceAttackPower)
        : this(
            source != null
                ? BattleStatusTarget.FromAlly(source)
                : default,
            board,
            resource,
            actionKind,
            targetFaction,
            enemyTargets,
            allyTargets,
            sourceAttackPower,
            resource?.Current ?? 0,
            resource?.Maximum ?? 0,
            default,
            0,
            0,
            0,
            0)
    {
    }

    public EffectContext(
        BattleStatusTarget sourceTarget,
        IBattleBoard board,
        IActiveSkillResource resource,
        CharacterActionKind actionKind,
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets,
        float sourceAttackPower)
        : this(
            sourceTarget,
            board,
            resource,
            actionKind,
            targetFaction,
            enemyTargets,
            allyTargets,
            sourceAttackPower,
            resource?.Current ?? 0,
            resource?.Maximum ?? 0,
            default,
            0,
            0,
            0,
            0)
    {
    }

    private EffectContext(
        BattleStatusTarget sourceTarget,
        IBattleBoard board,
        IActiveSkillResource resource,
        CharacterActionKind actionKind,
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets,
        float sourceAttackPower,
        int sourceResource,
        int sourceResourceMaximum,
        BattleStatusTarget target,
        int targetCurrentHealth,
        int targetMaximumHealth,
        int sourceStatusStacks,
        int targetStatusStacks)
    {
        SourceTarget = sourceTarget;
        Board = board;
        Resource = resource;
        ActionKind = actionKind;
        TargetFaction = targetFaction;
        SourceAttackPower = IsFinite(sourceAttackPower)
            ? Mathf.Max(0f, sourceAttackPower)
            : 0f;
        SourceResource = Mathf.Max(0, sourceResource);
        SourceResourceMaximum = Mathf.Max(
            SourceResource,
            sourceResourceMaximum);
        Target = target;
        bool hasTargetHealth = target.IsValid;
        TargetCurrentHealth = hasTargetHealth
            ? Mathf.Max(0, targetCurrentHealth)
            : 0;
        TargetMaximumHealth = hasTargetHealth
            ? Mathf.Max(TargetCurrentHealth, targetMaximumHealth)
            : 0;
        SourceStatusStacks = Mathf.Max(0, sourceStatusStacks);
        TargetStatusStacks = Mathf.Max(0, targetStatusStacks);
        _enemyTargets = targetFaction == CharacterTargetFaction.Enemy
            ? enemyTargets ?? Array.Empty<EnemyRuntime>()
            : Array.Empty<EnemyRuntime>();
        _allyTargets = targetFaction == CharacterTargetFaction.Ally
            ? allyTargets ?? Array.Empty<IBattleCharacter>()
            : Array.Empty<IBattleCharacter>();
    }

    public static EffectContext ForPreview(
        CharacterActionKind actionKind,
        float sourceAttackPower,
        int sourceResource = 0,
        int sourceResourceMaximum = 0)
    {
        return new EffectContext(
            default,
            null,
            null,
            actionKind,
            CharacterTargetFaction.Enemy,
            null,
            null,
            sourceAttackPower,
            sourceResource,
            sourceResourceMaximum,
            default,
            0,
            0,
            0,
            0);
    }

    public EffectContext SnapshotSourceStatus(
        StatusEffectSO statusEffect)
    {
        return CopyWithTarget(
            Target,
            TargetCurrentHealth,
            TargetMaximumHealth,
            SourceTarget.GetStatusStackCount(statusEffect),
            TargetStatusStacks);
    }

    public EffectContext WithSourceAttackPower(float sourceAttackPower)
    {
        return new EffectContext(
            SourceTarget,
            Board,
            Resource,
            ActionKind,
            TargetFaction,
            EnemyTargets,
            AllyTargets,
            sourceAttackPower,
            SourceResource,
            SourceResourceMaximum,
            Target,
            TargetCurrentHealth,
            TargetMaximumHealth,
            SourceStatusStacks,
            TargetStatusStacks);
    }

    public EffectContext RetargetToSource()
    {
        if (SourceTarget.Enemy != null)
        {
            return RetargetTo(
                CharacterTargetFaction.Enemy,
                new[] { SourceTarget.Enemy },
                null);
        }

        IReadOnlyList<IBattleCharacter> sourceTargets = Source == null
            ? Array.Empty<IBattleCharacter>()
            : new[] { Source };
        return RetargetTo(
            CharacterTargetFaction.Ally,
            null,
            sourceTargets);
    }

    public EffectContext RetargetTo(
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets)
    {
        return new EffectContext(
            SourceTarget,
            Board,
            Resource,
            ActionKind,
            targetFaction,
            enemyTargets,
            allyTargets,
            SourceAttackPower,
            SourceResource,
            SourceResourceMaximum,
            default,
            0,
            0,
            SourceStatusStacks,
            0);
    }

    public EffectContext BindEnemyTarget(
        EnemyRuntime target,
        StatusEffectSO scalingStatusEffect)
    {
        BattleStatusTarget statusTarget =
            BattleStatusTarget.FromEnemy(target);
        return CopyWithTarget(
            statusTarget,
            target?.Health ?? 0,
            target?.MaxHealth ?? 0,
            SourceStatusStacks,
            target?.GetStatusStackCount(scalingStatusEffect) ?? 0);
    }

    public EffectContext BindAllyTarget(
        IBattleCharacter target,
        StatusEffectSO scalingStatusEffect)
    {
        BattleStatusTarget statusTarget =
            BattleStatusTarget.FromAlly(target);
        return CopyWithTarget(
            statusTarget,
            target?.CurrentHealth ?? 0,
            target?.MaximumHealth ?? 0,
            SourceStatusStacks,
            target?.GetStatusStackCount(scalingStatusEffect) ?? 0);
    }

    private EffectContext CopyWithTarget(
        BattleStatusTarget target,
        int targetCurrentHealth,
        int targetMaximumHealth,
        int sourceStatusStacks,
        int targetStatusStacks)
    {
        return new EffectContext(
            SourceTarget,
            Board,
            Resource,
            ActionKind,
            TargetFaction,
            EnemyTargets,
            AllyTargets,
            SourceAttackPower,
            SourceResource,
            SourceResourceMaximum,
            target,
            targetCurrentHealth,
            targetMaximumHealth,
            sourceStatusStacks,
            targetStatusStacks);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public enum BattleEffectOriginKind
{
    CharacterAttack = 0,
    CharacterPassive = 1,
    CharacterSkill = 2,
    StatusEffect = 3,
    EnemyAbility = 4,
    BattleLifecycle = 5,
    BattleItem = 6
}

public readonly struct BattleEffectContext
{
    private readonly EffectContext _characterContext;

    public BattleEffectOriginKind OriginKind { get; }
    public BattleStatusTarget SourceTarget { get; }
    public BattleStatusTarget Target => _characterContext.Target;
    public BattleStatusTarget Holder => Target;
    public IBattleCharacter Source => _characterContext.Source;
    public IBattleBoard Board => _characterContext.Board;
    public IActiveSkillResource Resource => _characterContext.Resource;
    public CharacterTargetFaction TargetFaction =>
        _characterContext.TargetFaction;
    public float SourceAttackPower => _characterContext.SourceAttackPower;
    public int SourceCurrentHealth =>
        _characterContext.SourceCurrentHealth;
    public int SourceMaximumHealth =>
        _characterContext.SourceMaximumHealth;
    public int SourceResource => _characterContext.SourceResource;
    public int SourceResourceMaximum =>
        _characterContext.SourceResourceMaximum;
    public bool HasBoundTarget => _characterContext.HasBoundTarget;
    public bool HasTargetHealth => _characterContext.HasTargetHealth;
    public int TargetCurrentHealth =>
        _characterContext.TargetCurrentHealth;
    public int TargetMaximumHealth =>
        _characterContext.TargetMaximumHealth;
    public int SourceStatusStacks =>
        _characterContext.SourceStatusStacks;
    public int TargetStatusStacks =>
        _characterContext.TargetStatusStacks;
    public IReadOnlyList<EnemyRuntime> EnemyTargets =>
        _characterContext.EnemyTargets;
    public IReadOnlyList<IBattleCharacter> AllyTargets =>
        _characterContext.AllyTargets;
    public int TargetCount => _characterContext.TargetCount;
    public bool HasTargets => _characterContext.HasTargets;
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }
    public int AddedStacks =>
        Math.Max(0, CurrentStacks - PreviousStacks);
    public int RemovedStacks =>
        Math.Max(0, PreviousStacks - CurrentStacks);
    public int OccurrenceCount { get; }
    public bool StatusEffectsLastUntilBattleEnd { get; }
    public EffectContext CharacterContext => _characterContext;

    private BattleEffectContext(
        EffectContext characterContext,
        BattleEffectOriginKind originKind,
        BattleStatusTarget sourceTarget,
        int previousStacks,
        int currentStacks,
        int occurrenceCount,
        bool statusEffectsLastUntilBattleEnd = false)
    {
        _characterContext = characterContext;
        OriginKind = originKind;
        SourceTarget = sourceTarget;
        PreviousStacks = Math.Max(0, previousStacks);
        CurrentStacks = Math.Max(0, currentStacks);
        OccurrenceCount = Math.Max(1, occurrenceCount);
        StatusEffectsLastUntilBattleEnd =
            statusEffectsLastUntilBattleEnd;
    }

    public static BattleEffectContext FromCharacter(
        EffectContext context)
    {
        return new BattleEffectContext(
            context,
            ToOriginKind(context.ActionKind),
            context.SourceTarget,
            0,
            0,
            1);
    }

    public static BattleEffectContext ForEnemyAbility(
        EnemyRuntime source,
        IBattleBoard board,
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerCharacterTargets,
        float sourceAttackPower = 0f)
    {
        BattleStatusTarget sourceTarget =
            BattleStatusTarget.FromEnemy(source);
        EffectContext context = new(
            sourceTarget,
            board,
            null,
            CharacterActionKind.Passive,
            targetFaction,
            enemyTargets,
            playerCharacterTargets,
            sourceAttackPower);
        return new BattleEffectContext(
            context,
            BattleEffectOriginKind.EnemyAbility,
            sourceTarget,
            0,
            0,
            1);
    }

    public static BattleEffectContext ForBattleItem(
        BattleStatusTarget selectedTarget,
        IBattleBoard board,
        IActiveSkillResource resource,
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets,
        float sourceAttackPower = 0f,
        bool statusEffectsLastUntilBattleEnd = false)
    {
        EffectContext context = new(
            selectedTarget,
            board,
            resource,
            CharacterActionKind.Skill,
            targetFaction,
            enemyTargets,
            allyTargets,
            sourceAttackPower);
        return new BattleEffectContext(
            context,
            BattleEffectOriginKind.BattleItem,
            selectedTarget,
            0,
            0,
            1,
            statusEffectsLastUntilBattleEnd);
    }

    public static BattleEffectContext ForPreview(
        BattleEffectOriginKind originKind,
        float sourceAttackPower,
        int sourceResource = 0,
        int sourceResourceMaximum = 0)
    {
        EffectContext context = EffectContext.ForPreview(
            ToCharacterActionKind(originKind),
            sourceAttackPower,
            sourceResource,
            sourceResourceMaximum);
        return new BattleEffectContext(
            context,
            originKind,
            default,
            0,
            0,
            1);
    }

    public static BattleEffectContext ForStatus(
        BattleStatusTarget holder,
        BattleStatusTarget sourceTarget,
        IBattleBoard board,
        float sourceAttackPower,
        int previousStacks,
        int currentStacks,
        int occurrenceCount = 1,
        IActiveSkillResource resource = null)
    {
        IReadOnlyList<EnemyRuntime> enemyTargets =
            holder.Enemy != null
                ? new[] { holder.Enemy }
                : Array.Empty<EnemyRuntime>();
        IReadOnlyList<IBattleCharacter> allyTargets =
            holder.Ally != null
                ? new[] { holder.Ally }
                : Array.Empty<IBattleCharacter>();
        EffectContext context = new(
            sourceTarget,
            board,
            resource,
            CharacterActionKind.Passive,
            holder.IsValid
                ? holder.Faction
                : CharacterTargetFaction.Enemy,
            enemyTargets,
            allyTargets,
            sourceAttackPower);
        if (holder.Enemy != null)
            context = context.BindEnemyTarget(holder.Enemy, null);
        else if (holder.Ally != null)
            context = context.BindAllyTarget(holder.Ally, null);

        return new BattleEffectContext(
            context,
            BattleEffectOriginKind.StatusEffect,
            sourceTarget,
            previousStacks,
            currentStacks,
            occurrenceCount);
    }

    public BattleEffectContext SnapshotSourceStatus(
        StatusEffectSO statusEffect)
    {
        return Copy(_characterContext.SnapshotSourceStatus(statusEffect));
    }

    public BattleEffectContext RetargetToSource()
    {
        return Copy(_characterContext.RetargetToSource());
    }

    public BattleEffectContext RetargetTo(
        CharacterTargetFaction targetFaction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets)
    {
        return Copy(_characterContext.RetargetTo(
            targetFaction,
            enemyTargets,
            allyTargets));
    }

    public BattleEffectContext BindEnemyTarget(
        EnemyRuntime target,
        StatusEffectSO scalingStatusEffect)
    {
        return Copy(_characterContext.BindEnemyTarget(
            target,
            scalingStatusEffect));
    }

    public BattleEffectContext BindAllyTarget(
        IBattleCharacter target,
        StatusEffectSO scalingStatusEffect)
    {
        return Copy(_characterContext.BindAllyTarget(
            target,
            scalingStatusEffect));
    }

    public BattleEffectContext WithStatusEvent(
        int previousStacks,
        int currentStacks,
        int occurrenceCount = 1)
    {
        return new BattleEffectContext(
            _characterContext,
            OriginKind,
            SourceTarget,
            previousStacks,
            currentStacks,
            occurrenceCount,
            StatusEffectsLastUntilBattleEnd);
    }

    private BattleEffectContext Copy(EffectContext context)
    {
        return new BattleEffectContext(
            context,
            OriginKind,
            SourceTarget,
            PreviousStacks,
            CurrentStacks,
            OccurrenceCount,
            StatusEffectsLastUntilBattleEnd);
    }

    private static BattleEffectOriginKind ToOriginKind(
        CharacterActionKind actionKind)
    {
        return actionKind switch
        {
            CharacterActionKind.Passive =>
                BattleEffectOriginKind.CharacterPassive,
            CharacterActionKind.Skill =>
                BattleEffectOriginKind.CharacterSkill,
            _ => BattleEffectOriginKind.CharacterAttack
        };
    }

    private static CharacterActionKind ToCharacterActionKind(
        BattleEffectOriginKind originKind)
    {
        return originKind switch
        {
            BattleEffectOriginKind.CharacterPassive =>
                CharacterActionKind.Passive,
            BattleEffectOriginKind.CharacterSkill =>
                CharacterActionKind.Skill,
            BattleEffectOriginKind.StatusEffect =>
                CharacterActionKind.Passive,
            BattleEffectOriginKind.EnemyAbility =>
                CharacterActionKind.Passive,
            BattleEffectOriginKind.BattleItem =>
                CharacterActionKind.Skill,
            _ => CharacterActionKind.Attack
        };
    }
}
