using System;
using System.Collections.Generic;
using UnityEngine;

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

    public IBattleCharacter Source { get; }
    public IBattleBoard Board { get; }
    public IActiveSkillResource Resource { get; }
    public CharacterActionKind ActionKind { get; }
    public CharacterTargetFaction TargetFaction { get; }
    public float SourceAttackPower { get; }
    public int SourceResource { get; }
    public int SourceResourceMaximum { get; }
    public BattleStatusTarget Target { get; }
    public bool HasBoundTarget => Target.IsValid;
    public bool HasTargetHealth => Target.Enemy != null;
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
            source,
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
        IBattleCharacter source,
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
        Source = source;
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
        bool hasTargetHealth = target.Enemy != null;
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
            null,
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
            Source?.GetStatusStackCount(statusEffect) ?? 0,
            TargetStatusStacks);
    }

    public EffectContext RetargetToSource()
    {
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
            Source,
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
            Source,
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
