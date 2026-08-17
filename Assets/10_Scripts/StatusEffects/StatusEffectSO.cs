using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusEffectAlignment
{
    Buff = 0,
    Debuff = 1,
    Neutral = 2
}

public enum StatusEffectDurationMode
{
    Timed = 0,
    Permanent = 1
}

public enum StatusEffectStackMode
{
    AddAndRefreshDuration = 0,
    AddKeepDuration = 1,
    IndependentDuration = 2,
    Replace = 3
}

public enum StatusEffectStackRemovalOrder
{
    Oldest = 0,
    Newest = 1,
    Random = 2
}

public enum StatusEffectOperationTrigger
{
    OnApply = 0,
    OnTick = 1,
    OnExpire = 2,
    OnRemove = 3,
    OnStackChanged = 4
}

public enum StatusEffectLifecycleTrigger
{
    OnApply = 0,
    OnReapply = 1,
    OnTick = 2,
    OnStackChanged = 3,
    OnExpire = 4,
    OnRemove = 5
}

public enum StatusEffectOperationType
{
    PeriodicDamage = 0,
    InstantDamage = 1,
    AttackPowerModifier = 2,
    AttackSpeedModifier = 3,
    DisableAction = 4
}

public enum StatusEffectValueMode
{
    Fixed = 0,
    Ratio = 1
}

public enum StatusEffectStatType
{
    AttackPower = 0,
    AttackSpeed = 1,
    IncomingDamage = 2,
    TargetPriority = 3
}

public enum StatusEffectStatModifierMode
{
    Flat = 0,
    AdditiveRatio = 1,
    MultiplicativeRatio = 2
}

public enum StatusEffectControlType
{
    DisableAllActions = 0,
    DisableBasicAttack = 1,
    DisableActiveSkill = 2,
    PausePassiveCooldowns = 3,
    ForceTargeting = 4
}

public static class StatusEffectIds
{
    public const string Fire = "fire";
    public const string Stun = "stun";
    public const string EmergencyKit = "emergency_kit";
    public const string Opening = "opening";
}

[Serializable]
public sealed class StatusEffectStatModifierDefinition
{
    [SerializeField]
    private StatusEffectStatType statType;
    [SerializeField]
    private StatusEffectStatModifierMode mode;
    [SerializeField]
    private float value;
    [SerializeField]
    private bool scaleWithStacks = true;

    public StatusEffectStatType StatType => statType;
    public StatusEffectStatModifierMode Mode => mode;
    public float Value => value;
    public bool ScaleWithStacks => scaleWithStacks;

    public void Validate()
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            value = 0f;
        if (statType == StatusEffectStatType.TargetPriority)
            mode = StatusEffectStatModifierMode.Flat;
        if (mode == StatusEffectStatModifierMode.MultiplicativeRatio)
            value = Mathf.Max(-1f, value);
    }
}

[Serializable]
public sealed class StatusEffectControlDefinition
{
    [SerializeField]
    private StatusEffectControlType controlType;

    public StatusEffectControlType ControlType => controlType;
}

public struct StatusEffectStatAccumulator
{
    private float _flat;
    private float _additiveRatio;
    private float _multiplicativeFactor;
    private bool _initialized;

    public float Flat => _flat;
    public float AdditiveRatio => _additiveRatio;
    public float MultiplicativeFactor =>
        _initialized ? _multiplicativeFactor : 1f;

    public void Add(
        StatusEffectStatModifierDefinition modifier,
        int stacks,
        float contributionMultiplier = 1f)
    {
        if (modifier == null ||
            float.IsNaN(modifier.Value) ||
            float.IsInfinity(modifier.Value) ||
            float.IsNaN(contributionMultiplier) ||
            float.IsInfinity(contributionMultiplier))
        {
            return;
        }

        EnsureInitialized();
        contributionMultiplier = Mathf.Max(0f, contributionMultiplier);
        int multiplier = modifier.ScaleWithStacks
            ? Mathf.Max(1, stacks)
            : 1;
        float scaledValue = modifier.Value * contributionMultiplier;
        switch (modifier.Mode)
        {
            case StatusEffectStatModifierMode.Flat:
                _flat += scaledValue * multiplier;
                break;

            case StatusEffectStatModifierMode.AdditiveRatio:
                _additiveRatio += scaledValue * multiplier;
                break;

            case StatusEffectStatModifierMode.MultiplicativeRatio:
                float factor = Mathf.Max(0f, 1f + scaledValue);
                _multiplicativeFactor *= Mathf.Pow(factor, multiplier);
                break;
        }
    }

    public void Add(
        StatusEffectStatModifierMode mode,
        float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return;

        EnsureInitialized();
        switch (mode)
        {
            case StatusEffectStatModifierMode.Flat:
                _flat += value;
                break;

            case StatusEffectStatModifierMode.AdditiveRatio:
                _additiveRatio += value;
                break;

            case StatusEffectStatModifierMode.MultiplicativeRatio:
                _multiplicativeFactor *= Mathf.Max(0f, 1f + value);
                break;
        }
    }

    public void AddFlat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return;

        EnsureInitialized();
        _flat += value;
    }

    public void AddAdditiveRatio(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return;

        EnsureInitialized();
        _additiveRatio += value;
    }

    public float Evaluate(float baseValue)
    {
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            return 0f;

        float value = (baseValue + _flat +
                       baseValue * _additiveRatio) *
                      MultiplicativeFactor;
        return float.IsNaN(value) || float.IsInfinity(value)
            ? 0f
            : value;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        _multiplicativeFactor = 1f;
    }
}

public readonly struct StatusEffectTargetPriority
{
    public bool IsForced { get; }
    public float Adjustment { get; }

    public StatusEffectTargetPriority(bool isForced, float adjustment)
    {
        IsForced = isForced;
        Adjustment =
            float.IsNaN(adjustment) || float.IsInfinity(adjustment)
                ? 0f
                : adjustment;
    }
}

public static class StatusEffectTargetPriorityResolver
{
    public static StatusEffectTargetPriority Resolve(
        IReadOnlyList<BattleStatusSnapshot> statuses)
    {
        if (statuses == null || statuses.Count == 0)
            return default;

        bool forced = false;
        StatusEffectStatAccumulator accumulator = default;
        foreach (BattleStatusSnapshot status in statuses)
        {
            if (!status.IsValid || status.Definition == null)
                continue;

            StatusEffectSO definition = status.Definition;
            if (definition.HasControl(
                    StatusEffectControlType.ForceTargeting))
            {
                forced = true;
            }

            IReadOnlyList<StatusEffectStatModifierDefinition> modifiers =
                definition.StatModifiers;
            if (modifiers == null)
                continue;

            foreach (StatusEffectStatModifierDefinition modifier in
                     modifiers)
            {
                if (modifier != null &&
                    modifier.StatType ==
                        StatusEffectStatType.TargetPriority)
                {
                    accumulator.Add(
                        modifier,
                        Mathf.Max(1, status.StackCount));
                }
            }
        }

        return new StatusEffectTargetPriority(
            forced,
            accumulator.Evaluate(0f));
    }
}

[Serializable]
public sealed class StatusEffectTriggerBlockDefinition
{
    [SerializeField]
    private StatusEffectLifecycleTrigger trigger;
    [SerializeField]
    private List<CharacterEffectDefinition> effects = new();
    [SerializeField]
    private bool scaleWithCurrentStacks;
    [SerializeField]
    private bool scaleWithOccurrences = true;

    public StatusEffectLifecycleTrigger Trigger => trigger;
    public IReadOnlyList<CharacterEffectDefinition> Effects => effects;
    public IReadOnlyList<IBattleEffectDefinition> BattleEffects => effects;
    public bool HasEffects => effects != null && effects.Count > 0;
    public bool ScaleWithCurrentStacks => scaleWithCurrentStacks;
    public bool ScaleWithOccurrences => scaleWithOccurrences;

    public void Validate()
    {
        effects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in effects)
            effect?.Validate();
    }

    public int GetAmountMultiplier(
        StatusEffectLifecycleEvent eventData)
    {
        long multiplier = 1;
        if (scaleWithCurrentStacks)
            multiplier *= Mathf.Max(1, eventData.CurrentStacks);
        if (scaleWithOccurrences)
            multiplier *= Mathf.Max(1, eventData.OccurrenceCount);
        return multiplier >= int.MaxValue
            ? int.MaxValue
            : Mathf.Max(1, (int)multiplier);
    }
}

public readonly struct StatusEffectLifecycleEvent
{
    public StatusEffectSO Definition { get; }
    public StatusEffectLifecycleTrigger Trigger { get; }
    public BattleStatusTarget Target { get; }
    public BattleAbilityUser User { get; }
    public BattleStatusTarget SourceTarget => User.Unit;
    public IBattleCharacter Source => User.Unit.Ally;
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }
    public int OccurrenceCount { get; }
    public int AddedStacks =>
        Mathf.Max(0, CurrentStacks - PreviousStacks);
    public int RemovedStacks =>
        Mathf.Max(0, PreviousStacks - CurrentStacks);
    public bool HasStackChange => PreviousStacks != CurrentStacks;
    public bool IsValid =>
        Definition != null &&
        Target.IsValid &&
        Enum.IsDefined(typeof(StatusEffectLifecycleTrigger), Trigger) &&
        OccurrenceCount > 0;

    public StatusEffectLifecycleEvent(
        StatusEffectSO definition,
        StatusEffectLifecycleTrigger trigger,
        BattleStatusTarget target,
        BattleAbilityUser user,
        int previousStacks,
        int currentStacks,
        int occurrenceCount = 1)
    {
        Definition = definition;
        Trigger = trigger;
        Target = target;
        User = user;
        PreviousStacks = Mathf.Max(0, previousStacks);
        CurrentStacks = Mathf.Max(0, currentStacks);
        OccurrenceCount = Mathf.Max(1, occurrenceCount);
    }

    public StatusEffectLifecycleEvent(
        StatusEffectSO definition,
        StatusEffectLifecycleTrigger trigger,
        BattleStatusTarget target,
        IBattleCharacter source,
        int previousStacks,
        int currentStacks,
        int occurrenceCount = 1)
        : this(
            definition,
            trigger,
            target,
            source != null
                ? BattleAbilityUser.FromCharacter(source)
                : BattleAbilityUser.ForStatusEffect(),
            previousStacks,
            currentStacks,
            occurrenceCount)
    {
    }

    public BattleEffectContext CreateEffectContext(IBattleBoard board)
    {
        return BattleEffectContext.ForStatus(
            Target,
            User,
            board,
            PreviousStacks,
            CurrentStacks,
            OccurrenceCount,
            User.Resource);
    }
}

public readonly struct StatusEffectTriggerInvocation
{
    public StatusEffectLifecycleEvent Event { get; }
    public StatusEffectTriggerBlockDefinition Block { get; }
    public int BlockIndex { get; }
    public bool IsValid =>
        Event.IsValid &&
        Block != null &&
        Block.Trigger == Event.Trigger &&
        Block.HasEffects &&
        BlockIndex >= 0;

    public StatusEffectTriggerInvocation(
        StatusEffectLifecycleEvent eventData,
        StatusEffectTriggerBlockDefinition block,
        int blockIndex)
    {
        Event = eventData;
        Block = block;
        BlockIndex = blockIndex;
    }
}

public static class StatusEffectLifecycleResolver
{
    public static IReadOnlyList<StatusEffectLifecycleEvent> Resolve(
        BattleStatusChangedEvent change)
    {
        if (!change.IsValid)
            return Array.Empty<StatusEffectLifecycleEvent>();

        List<StatusEffectLifecycleEvent> events = new(2);
        BattleAbilityUser user = change.Current.ActiveUser.HasUnit ||
                                 change.Current.ActiveUser.Role != default
            ? change.Current.ActiveUser
            : change.Previous.ActiveUser;
        switch (change.ChangeType)
        {
            case BattleStatusChangeType.Applied:
                Add(
                    events,
                    change,
                    user,
                    StatusEffectLifecycleTrigger.OnApply);
                AddStackChange(events, change, user);
                break;

            case BattleStatusChangeType.Reapplied:
                Add(
                    events,
                    change,
                    user,
                    StatusEffectLifecycleTrigger.OnReapply);
                AddStackChange(events, change, user);
                break;

            case BattleStatusChangeType.StackChanged:
                AddStackChange(events, change, user);
                break;

            case BattleStatusChangeType.Expired:
                AddStackChange(events, change, user);
                Add(
                    events,
                    change,
                    user,
                    StatusEffectLifecycleTrigger.OnExpire);
                break;

            case BattleStatusChangeType.Removed:
                AddStackChange(events, change, user);
                Add(
                    events,
                    change,
                    user,
                    StatusEffectLifecycleTrigger.OnRemove);
                break;
        }

        return events.Count > 0
            ? events.ToArray()
            : Array.Empty<StatusEffectLifecycleEvent>();
    }

    public static StatusEffectLifecycleEvent ResolveTick(
        BattleStatusTarget target,
        BattleStatusSnapshot snapshot,
        int occurrenceCount)
    {
        if (!target.IsValid || !snapshot.IsValid || occurrenceCount <= 0)
            return default;

        return new StatusEffectLifecycleEvent(
            snapshot.Definition,
            StatusEffectLifecycleTrigger.OnTick,
            target,
            snapshot.ActiveUser,
            snapshot.StackCount,
            snapshot.StackCount,
            occurrenceCount);
    }

    public static IReadOnlyList<StatusEffectTriggerInvocation>
        ResolveInvocations(StatusEffectLifecycleEvent eventData)
    {
        IReadOnlyList<StatusEffectTriggerBlockDefinition> blocks =
            eventData.Definition?.TriggerBlocks;
        if (!eventData.IsValid || blocks == null || blocks.Count == 0)
            return Array.Empty<StatusEffectTriggerInvocation>();

        List<StatusEffectTriggerInvocation> invocations = new();
        for (int index = 0; index < blocks.Count; index++)
        {
            StatusEffectTriggerBlockDefinition block = blocks[index];
            if (block == null ||
                block.Trigger != eventData.Trigger ||
                !block.HasEffects)
            {
                continue;
            }

            invocations.Add(new StatusEffectTriggerInvocation(
                eventData,
                block,
                index));
        }

        return invocations.Count > 0
            ? invocations.ToArray()
            : Array.Empty<StatusEffectTriggerInvocation>();
    }

    private static void AddStackChange(
        ICollection<StatusEffectLifecycleEvent> events,
        BattleStatusChangedEvent change,
        BattleAbilityUser user)
    {
        if (change.PreviousStacks == change.CurrentStacks)
            return;

        Add(
            events,
            change,
            user,
            StatusEffectLifecycleTrigger.OnStackChanged);
    }

    private static void Add(
        ICollection<StatusEffectLifecycleEvent> events,
        BattleStatusChangedEvent change,
        BattleAbilityUser user,
        StatusEffectLifecycleTrigger trigger)
    {
        events.Add(new StatusEffectLifecycleEvent(
            change.StatusEffect,
            trigger,
            change.Target,
            user,
            change.PreviousStacks,
            change.CurrentStacks));
    }
}

public static class StatusEffectTriggerExecutor
{
    public static BattleEffectResult Execute(
        StatusEffectLifecycleEvent eventData,
        IBattleBoard board = null,
        Func<int, IBattleCharacter, bool>
            inheritedEnemyDamageFallback = null)
    {
        if (!eventData.IsValid)
            return default;

        CharacterRuntime sourceRuntime =
            eventData.SourceTarget.Ally as CharacterRuntime;
        board ??= sourceRuntime?.BoundBattleBoard ??
                  eventData.SourceTarget.Enemy?.BoundBattleBoard;
        BattleEffectContext context =
            eventData.CreateEffectContext(board);
        IReadOnlyList<StatusEffectTriggerInvocation> invocations =
            StatusEffectLifecycleResolver.ResolveInvocations(eventData);
        BattleEffectResult combined = default;
        foreach (StatusEffectTriggerInvocation invocation in invocations)
        {
            if (!invocation.IsValid)
                continue;

            BattleEffectResult current =
                BattleEffectExecutor.ExecuteSequence(
                    context,
                    invocation.Block.BattleEffects,
                    sourceRuntime?.Data,
                    invocation.Block.GetAmountMultiplier(eventData),
                    inheritedEnemyDamageFallback);
            combined = combined.Combine(current);
        }

        return combined;
    }
}

[Serializable]
public sealed class StatusEffectOperationDefinition
{
    [SerializeField]
    private StatusEffectOperationTrigger trigger;
    [SerializeField]
    private StatusEffectOperationType operationType;
    [SerializeField]
    private StatusEffectValueMode valueMode;
    [SerializeField]
    private float value = 1f;
    [SerializeField]
    private bool scaleWithStacks = true;

    public StatusEffectOperationTrigger Trigger => trigger;
    public StatusEffectOperationType OperationType => operationType;
    public StatusEffectValueMode ValueMode => valueMode;
    public float Value => value;
    public bool ScaleWithStacks => scaleWithStacks;

    public void Validate()
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            value = 0f;
    }
}

internal sealed class StatusEffectRuntimeBatch
{
    public int Stacks { get; set; }
    public float RemainingDuration { get; set; }
    public float TotalDuration { get; }
    public float TickInterval { get; }
    public BattleAbilityUser User { get; }

    public StatusEffectRuntimeBatch(
        int stacks,
        float remainingDuration,
        float tickInterval,
        BattleAbilityUser user,
        float totalDuration = -1f)
    {
        Stacks = Mathf.Max(1, stacks);
        RemainingDuration = remainingDuration;
        TotalDuration = totalDuration >= 0f
            ? totalDuration
            : remainingDuration;
        TickInterval = TimePrecision.Normalize(
            tickInterval,
            TimePrecision.Step);
        User = user;
    }
}

internal readonly struct StatusEffectRuntimeMutation
{
    public bool Succeeded { get; }
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }
    public BattleAbilityUser User { get; }
    public bool StackChanged => PreviousStacks != CurrentStacks;
    public int RemovedStacks => Mathf.Max(0, PreviousStacks - CurrentStacks);

    public StatusEffectRuntimeMutation(
        bool succeeded,
        int previousStacks,
        int currentStacks,
        BattleAbilityUser user)
    {
        Succeeded = succeeded;
        PreviousStacks = Mathf.Max(0, previousStacks);
        CurrentStacks = Mathf.Max(0, currentStacks);
        User = user;
    }
}

internal sealed class StatusEffectRuntimeState
{
    private const float TickEpsilon = 0.0001f;

    private readonly List<StatusEffectRuntimeBatch> _batches = new();
    private float _tickElapsed;

    public StatusEffectSO Definition { get; }
    public bool HasStacks => _batches.Count > 0;
    public bool IsPermanent
    {
        get
        {
            if (Definition.DurationMode ==
                StatusEffectDurationMode.Permanent)
            {
                return true;
            }

            foreach (StatusEffectRuntimeBatch batch in _batches)
            {
                if (batch != null && float.IsPositiveInfinity(
                        batch.RemainingDuration))
                {
                    return true;
                }
            }

            return false;
        }
    }
    public StatusEffectRuntimeBatch ActiveBatch =>
        _batches.Count > 0 ? _batches[0] : null;
    public int StackCount
    {
        get
        {
            int total = 0;
            foreach (StatusEffectRuntimeBatch batch in _batches)
            {
                total = BattleValueMath.SaturatingAddNonNegative(
                    total,
                    batch?.Stacks ?? 0);
            }
            return total;
        }
    }
    public float RemainingDuration
    {
        get
        {
            if (Definition.DurationMode ==
                StatusEffectDurationMode.Permanent)
            {
                return float.PositiveInfinity;
            }

            float total = 0f;
            foreach (StatusEffectRuntimeBatch batch in _batches)
                total += Mathf.Max(0f, batch.RemainingDuration);
            return total;
        }
    }
    public float TotalDuration
    {
        get
        {
            if (Definition.DurationMode ==
                StatusEffectDurationMode.Permanent)
            {
                return float.PositiveInfinity;
            }

            float total = 0f;
            foreach (StatusEffectRuntimeBatch batch in _batches)
                total += Mathf.Max(0f, batch.TotalDuration);
            return total;
        }
    }

    public StatusEffectRuntimeState(StatusEffectSO definition)
    {
        Definition = definition;
    }

    public StatusEffectRuntimeMutation Apply(
        int stacks,
        float remainingDuration,
        float tickInterval,
        BattleAbilityUser user)
    {
        int previousTotalStacks = StackCount;
        stacks = ClampIncomingStacks(stacks);
        if (stacks <= 0)
            return default;

        if (!HasStacks)
        {
            AddBatch(stacks, remainingDuration, tickInterval, user);
            return new StatusEffectRuntimeMutation(
                true,
                previousTotalStacks,
                StackCount,
                user);
        }

        if (Definition.StackMode ==
            StatusEffectStackMode.IndependentDuration)
        {
            int available = Definition.HasUnlimitedStacks
                ? stacks
                : Mathf.Max(0, Definition.MaximumStacks - StackCount);
            int addedStacks = Mathf.Min(stacks, available);
            if (addedStacks <= 0)
                return default;

            AddBatch(
                addedStacks,
                remainingDuration,
                tickInterval,
                user);
            return new StatusEffectRuntimeMutation(
                true,
                previousTotalStacks,
                StackCount,
                user);
        }

        StatusEffectRuntimeBatch active = ActiveBatch;
        int previousStacks = active.Stacks;
        float previousDuration = active.RemainingDuration;
        float previousTotalDuration = active.TotalDuration;
        int nextStacks = Definition.StackMode ==
                         StatusEffectStackMode.Replace
            ? stacks
            : ClampTotalStacks(active.Stacks + stacks);
        float nextDuration = previousDuration;
        float nextTotalDuration = previousTotalDuration;
        if (Definition.DurationMode ==
                StatusEffectDurationMode.Permanent ||
            IsPermanent ||
            float.IsPositiveInfinity(remainingDuration))
        {
            nextDuration = float.PositiveInfinity;
            nextTotalDuration = float.PositiveInfinity;
        }
        else if (Definition.StackMode !=
                     StatusEffectStackMode.AddKeepDuration ||
                 Definition.RefreshDurationOnReapply)
        {
            nextDuration = remainingDuration;
            nextTotalDuration = remainingDuration;
        }

        bool changed = nextStacks != previousStacks ||
                       !DurationsEqual(nextDuration, previousDuration);
        if (!changed)
            return default;

        _batches.Clear();
        AddBatch(
            nextStacks,
            nextDuration,
            tickInterval,
            user,
            nextTotalDuration);
        return new StatusEffectRuntimeMutation(
            true,
            previousTotalStacks,
            StackCount,
            user);
    }

    public StatusEffectRuntimeMutation RemoveStacks(int removalCount)
    {
        int available = StackCount;
        if (available <= 0)
            return default;

        int remaining = removalCount == 0
            ? available
            : Mathf.Min(Mathf.Max(0, removalCount), available);
        if (remaining <= 0)
            return default;

        BattleAbilityUser user = default;
        bool sourceCaptured = false;
        while (remaining > 0 && _batches.Count > 0)
        {
            int batchIndex = GetRemovalBatchIndex();
            StatusEffectRuntimeBatch batch = _batches[batchIndex];
            if (!sourceCaptured)
            {
                user = batch.User;
                sourceCaptured = true;
            }
            if (batch.Stacks <= remaining)
            {
                remaining -= batch.Stacks;
                _batches.RemoveAt(batchIndex);
                continue;
            }

            batch.Stacks -= remaining;
            remaining = 0;
        }

        if (!HasStacks)
            _tickElapsed = 0f;
        return new StatusEffectRuntimeMutation(
            true,
            available,
            StackCount,
            user);
    }

    public float AdvanceActiveDuration(float deltaTime)
    {
        StatusEffectRuntimeBatch active = ActiveBatch;
        if (active == null || deltaTime <= 0f)
            return 0f;

        float activeDelta = Definition.DurationMode ==
                            StatusEffectDurationMode.Permanent
            ? deltaTime
            : Mathf.Min(
                deltaTime,
                Mathf.Max(0f, active.RemainingDuration));
        if (Definition.DurationMode !=
            StatusEffectDurationMode.Permanent)
        {
            active.RemainingDuration = Mathf.Max(
                0f,
                active.RemainingDuration - activeDelta);
        }

        _tickElapsed += activeDelta;
        return activeDelta;
    }

    public int ConsumePendingTickCount()
    {
        StatusEffectRuntimeBatch active = ActiveBatch;
        if (active == null || active.TickInterval <= 0f)
            return 0;

        int tickCount = Mathf.FloorToInt(
            (_tickElapsed + TickEpsilon) / active.TickInterval);
        if (tickCount <= 0)
            return 0;

        _tickElapsed = Mathf.Max(
            0f,
            _tickElapsed - tickCount * active.TickInterval);
        return tickCount;
    }

    public StatusEffectRuntimeMutation RemoveExpiredActiveBatch()
    {
        StatusEffectRuntimeBatch active = ActiveBatch;
        if (active == null ||
            Definition.DurationMode ==
            StatusEffectDurationMode.Permanent ||
            active.RemainingDuration > 0f)
        {
            return default;
        }

        int previousStacks = StackCount;
        BattleAbilityUser user = active.User;
        _batches.RemoveAt(0);
        if (!HasStacks)
            _tickElapsed = 0f;
        return new StatusEffectRuntimeMutation(
            true,
            previousStacks,
            StackCount,
            user);
    }

    private void AddBatch(
        int stacks,
        float remainingDuration,
        float tickInterval,
        BattleAbilityUser user,
        float totalDuration = -1f)
    {
        _batches.Add(new StatusEffectRuntimeBatch(
            stacks,
            remainingDuration,
            tickInterval,
            user,
            totalDuration));
    }

    private int ClampIncomingStacks(int stacks)
    {
        stacks = Mathf.Max(1, stacks);
        return Definition.HasUnlimitedStacks
            ? stacks
            : Mathf.Min(stacks, Definition.MaximumStacks);
    }

    private int ClampTotalStacks(int stacks)
    {
        stacks = Mathf.Max(1, stacks);
        return Definition.HasUnlimitedStacks
            ? stacks
            : Mathf.Min(stacks, Definition.MaximumStacks);
    }

    private int GetRemovalBatchIndex()
    {
        return Definition.StackRemovalOrder switch
        {
            StatusEffectStackRemovalOrder.Newest => _batches.Count - 1,
            StatusEffectStackRemovalOrder.Random =>
                UnityEngine.Random.Range(0, _batches.Count),
            _ => 0
        };
    }

    private static bool DurationsEqual(float left, float right)
    {
        return left.Equals(right) || Mathf.Approximately(left, right);
    }
}

[CreateAssetMenu(
    fileName = "StatusEffect",
    menuName = "Dungeon/Status Effect")]
public sealed class StatusEffectSO : ScriptableObject,
    IBattleAbilityDefinition,
    IBattleAbilityProvider
{
    [SerializeField, HideInInspector]
    private string statusId;

    [Header("Identity")]
    [SerializeField]
    private Sprite icon;
    [SerializeField]
    private string nameLocalizationKey;
    [SerializeField]
    private string descriptionLocalizationKey;
    [SerializeField]
    private StatusEffectAlignment alignment = StatusEffectAlignment.Debuff;
    [SerializeField]
    private bool canTargetEnemy = true;
    [SerializeField]
    private bool canTargetAlly;

    [Header("Presentation")]
    [SerializeField]
    private GameObject visualEffectPrefab;
    [SerializeField]
    private BattleVfxCueSO applyVfxCue;
    [SerializeField]
    private BattleVfxCueSO loopVfxCue;
    [SerializeField]
    private BattleVfxCueSO tickVfxCue;
    [SerializeField]
    private BattleVfxCueSO removeVfxCue;
    [SerializeField]
    private RuntimeAnimatorController iconAnimatorController;
    [SerializeField]
    private AudioClip applyAudioClip;
    [SerializeField]
    private AudioClip tickAudioClip;
    [SerializeField]
    private AudioClip removeAudioClip;

    [Header("Duration")]
    [SerializeField]
    private StatusEffectDurationMode durationMode;
    [SerializeField, Min(0.1f)]
    private float defaultDuration = 1f;
    [SerializeField]
    private bool refreshDurationOnReapply = true;
    [SerializeField, Min(0.1f)]
    private float tickInterval = 1f;

    [Header("Stack")]
    [SerializeField]
    private StatusEffectStackMode stackMode =
        StatusEffectStackMode.AddAndRefreshDuration;
    [SerializeField, Min(0)]
    private int maximumStacks;
    [SerializeField, Min(1)]
    private int defaultAppliedStacks = 1;
    [SerializeField]
    private StatusEffectStackRemovalOrder stackRemovalOrder;

    [Header("Removal")]
    [SerializeField]
    private bool removable = true;
    [SerializeField]
    private bool includedInRandomRemoval = true;
    [SerializeField]
    private bool includedInAllRemoval = true;

    [Header("Operations")]
    [SerializeField]
    private List<StatusEffectOperationDefinition> operations = new();

    [Header("Lifecycle Trigger Blocks")]
    [SerializeField]
    private List<StatusEffectTriggerBlockDefinition> triggerBlocks = new();

    [Header("Persistent Stat Modifiers")]
    [SerializeField]
    private List<StatusEffectStatModifierDefinition> statModifiers = new();

    [Header("Control Effects")]
    [SerializeField]
    private List<StatusEffectControlDefinition> controlEffects = new();

    public string StatusId => statusId;
    public Sprite Icon => icon;
    public string NameLocalizationKey => nameLocalizationKey;
    public string DescriptionLocalizationKey => descriptionLocalizationKey;
    public StatusEffectAlignment Alignment => alignment;
    public bool CanTargetEnemy => canTargetEnemy;
    public bool CanTargetAlly => canTargetAlly;
    public GameObject VisualEffectPrefab => visualEffectPrefab;
    public BattleVfxCueSO ApplyVfxCue => applyVfxCue;
    public BattleVfxCueSO LoopVfxCue => loopVfxCue;
    public BattleVfxCueSO TickVfxCue => tickVfxCue;
    public BattleVfxCueSO RemoveVfxCue => removeVfxCue;
    public RuntimeAnimatorController IconAnimatorController =>
        iconAnimatorController;
    public AudioClip ApplyAudioClip => applyAudioClip;
    public AudioClip TickAudioClip => tickAudioClip;
    public AudioClip RemoveAudioClip => removeAudioClip;
    public StatusEffectDurationMode DurationMode => durationMode;
    public float ConfiguredDefaultDuration => defaultDuration;
    public float DefaultDuration => durationMode ==
        StatusEffectDurationMode.Permanent
            ? 0f
            : TimePrecision.Normalize(defaultDuration, 0.1f);
    public bool RefreshDurationOnReapply => refreshDurationOnReapply;
    public float ConfiguredTickInterval => tickInterval;
    public float TickInterval => TimePrecision.Normalize(tickInterval, 0.1f);
    public StatusEffectStackMode StackMode => stackMode;
    public int MaximumStacks => maximumStacks;
    public bool HasUnlimitedStacks => maximumStacks == 0;
    public int DefaultAppliedStacks => defaultAppliedStacks;
    public StatusEffectStackRemovalOrder StackRemovalOrder =>
        stackRemovalOrder;
    public bool Removable => removable;
    public bool ConfiguredIncludedInRandomRemoval =>
        includedInRandomRemoval;
    public bool ConfiguredIncludedInAllRemoval => includedInAllRemoval;
    public bool IncludedInRandomRemoval =>
        removable && includedInRandomRemoval;
    public bool IncludedInAllRemoval => removable && includedInAllRemoval;
    public IReadOnlyList<StatusEffectOperationDefinition> Operations =>
        operations;
    public IReadOnlyList<StatusEffectTriggerBlockDefinition> TriggerBlocks =>
        triggerBlocks;
    public bool HasTriggerBlocks =>
        triggerBlocks != null && triggerBlocks.Count > 0;
    public IReadOnlyList<StatusEffectStatModifierDefinition> StatModifiers =>
        statModifiers;
    public IReadOnlyList<StatusEffectControlDefinition> ControlEffects =>
        controlEffects;
    public bool HasPersistentModifiers =>
        statModifiers != null && statModifiers.Count > 0;
    public bool HasControlEffects =>
        controlEffects != null && controlEffects.Count > 0;
    public string AbilityId => StatusId ?? string.Empty;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => HasTriggerBlocks ? 1 : 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.StatusEffect;
    public BattleAbilityTargeting Targeting => new(
        BattleAbilityTargetRelation.Any,
        BattleAbilitySelectionMode.Inherit,
        BattleAbilityTargetMetric.None,
        1,
        areaDefinition: new BattleAreaDefinition());
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        EnumerateBattleEffects();
    public bool UsesLegacyEffectStorage =>
        !HasTriggerBlocks &&
        ((operations != null && operations.Count > 0) ||
         HasPersistentModifiers || HasControlEffects);
    public bool HasExecutableContent =>
        HasTriggerBlocks ||
        (operations != null && operations.Count > 0) ||
        HasPersistentModifiers ||
        HasControlEffects;

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        if (HasTriggerBlocks)
            yield return this;
    }

    private IEnumerable<IBattleEffectDefinition> EnumerateBattleEffects()
    {
        foreach (StatusEffectTriggerBlockDefinition block in TriggerBlocks ??
                 Array.Empty<StatusEffectTriggerBlockDefinition>())
        {
            if (block == null)
                continue;

            foreach (IBattleEffectDefinition effect in block.BattleEffects)
            {
                if (effect != null)
                    yield return effect;
            }
        }
    }

    private void OnValidate()
    {
        StatusEffectDefinitionCatalog.Invalidate();
    }

    public void ValidateDefinition()
    {
        defaultDuration = TimePrecision.Normalize(defaultDuration, 0.1f);
        tickInterval = TimePrecision.Normalize(tickInterval, 0.1f);
        maximumStacks = Mathf.Max(0, maximumStacks);
        defaultAppliedStacks = Mathf.Max(1, defaultAppliedStacks);
        if (maximumStacks > 0)
            defaultAppliedStacks = Mathf.Min(defaultAppliedStacks, maximumStacks);

        if (!removable)
        {
            includedInRandomRemoval = false;
            includedInAllRemoval = false;
        }

        operations ??= new List<StatusEffectOperationDefinition>();
        foreach (StatusEffectOperationDefinition operation in operations)
            operation?.Validate();

        triggerBlocks ??= new List<StatusEffectTriggerBlockDefinition>();
        foreach (StatusEffectTriggerBlockDefinition block in triggerBlocks)
            block?.Validate();

        statModifiers ??= new List<StatusEffectStatModifierDefinition>();
        foreach (StatusEffectStatModifierDefinition modifier in statModifiers)
            modifier?.Validate();

        controlEffects ??= new List<StatusEffectControlDefinition>();
    }

    public bool HasControl(StatusEffectControlType controlType)
    {
        if (controlEffects == null)
            return false;

        foreach (StatusEffectControlDefinition control in controlEffects)
        {
            if (control != null && control.ControlType == controlType)
                return true;
        }

        return false;
    }

    public void RegenerateStatusId()
    {
        statusId = Guid.NewGuid().ToString("N");
    }

}

public static class StatusEffectDefinitionCatalog
{
    private static List<StatusEffectSO> _cached;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _cached = null;
    }

    public static IReadOnlyList<StatusEffectSO> LoadAll()
    {
        if (_cached != null)
            return _cached;

        List<StatusEffectSO> loaded = new();
        _cached = loaded;
        HashSet<string> statusIds = new(StringComparer.Ordinal);
        foreach (StatusEffectSO definition in
                 Resources.LoadAll<StatusEffectSO>("StatusEffects"))
        {
            if (definition == null)
                continue;
            StatusEffectDefinitionValidationResult ownerValidation =
                StatusEffectDefinitionValidator.Validate(definition);
            if (!ownerValidation.IsValid)
            {
                Debug.LogError(
                    $"Status effect '{definition.name}' was excluded: " +
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
                    $"Status effect '{definition.name}' was excluded: " +
                    abilityError,
                    definition);
                continue;
            }
            if (string.IsNullOrWhiteSpace(definition.StatusId) ||
                !statusIds.Add(definition.StatusId))
            {
                Debug.LogError(
                    $"Status effect '{definition.name}' has a missing or " +
                    $"duplicate id '{definition.StatusId}'.",
                    definition);
                continue;
            }
            loaded.Add(definition);
        }
        return loaded;
    }

    public static StatusEffectSO FindById(string statusId)
    {
        if (string.IsNullOrWhiteSpace(statusId))
            return null;

        foreach (StatusEffectSO definition in LoadAll())
        {
            if (definition != null && string.Equals(
                    definition.StatusId,
                    statusId,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    public static void Invalidate()
    {
        _cached = null;
    }
}
