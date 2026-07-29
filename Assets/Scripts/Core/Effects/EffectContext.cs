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
    BattleLifecycle = 5
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
    public EffectContext CharacterContext => _characterContext;

    private BattleEffectContext(
        EffectContext characterContext,
        BattleEffectOriginKind originKind,
        BattleStatusTarget sourceTarget,
        int previousStacks,
        int currentStacks,
        int occurrenceCount)
    {
        _characterContext = characterContext;
        OriginKind = originKind;
        SourceTarget = sourceTarget;
        PreviousStacks = Math.Max(0, previousStacks);
        CurrentStacks = Math.Max(0, currentStacks);
        OccurrenceCount = Math.Max(1, occurrenceCount);
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
            occurrenceCount);
    }

    private BattleEffectContext Copy(EffectContext context)
    {
        return new BattleEffectContext(
            context,
            OriginKind,
            SourceTarget,
            PreviousStacks,
            CurrentStacks,
            OccurrenceCount);
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
            _ => CharacterActionKind.Attack
        };
    }
}
