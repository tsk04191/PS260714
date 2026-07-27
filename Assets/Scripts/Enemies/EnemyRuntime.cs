using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Independent combat state for one enemy created from an EnemySO definition.
/// </summary>
public sealed class EnemyRuntime
{
    private const int MaximumStatusChangesPerDispatch = 128;

    private readonly Dictionary<string, StatusEffectRuntimeState>
        _statusEffects =
        new(StringComparer.Ordinal);
    private readonly Queue<BattleStatusChangedEvent> _statusChangeQueue =
        new();
    private readonly List<EnemyAbilityRuntimeState> _abilityStates = new();
    private int _statusMutationDepth;
    private bool _dispatchingStatusChanges;

    public EnemySO Definition { get; }
    public EEnemyGrade Grade => Definition.Grade;
    public EEnemyType Type => Definition.Type;
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public int CurrentShield { get; private set; }
    public int Armor { get; private set; }
    public bool HasFire => TryGetStatusState(
        StatusEffectIds.Fire,
        out _);
    public int FireStackCount => TryGetStatusState(
        StatusEffectIds.Fire,
        out StatusEffectRuntimeState fireState)
            ? fireState.StackCount
            : 0;
    public float FireRemainingDuration => TryGetStatusState(
        StatusEffectIds.Fire,
        out StatusEffectRuntimeState fireState)
            ? TimePrecision.FloorToTenth(fireState.RemainingDuration)
            : 0f;
    public Sprite FireStatusSprite => TryGetStatusState(
        StatusEffectIds.Fire,
        out StatusEffectRuntimeState fireState)
            ? fireState.Definition?.Icon
            : null;
    public bool IsTargetPriorityExcluded
    {
        get
        {
            foreach (EnemyAbilityRuntimeState state in _abilityStates)
            {
                if (state.Definition.Trigger !=
                    EnemyAbilityTrigger.OnTargetPriorityEvaluation)
                {
                    continue;
                }

                foreach (EnemyAbilityOperationDefinition operation in
                         state.Definition.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType.ModifyTargetPriority)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    public float SpawnIntervalMultiplier => Definition.SpawnIntervalMultiplier;
    public float AbilityCooldownRemaining
    {
        get
        {
            foreach (EnemyAbilityRuntimeState state in _abilityStates)
            {
                if (state.Definition.Trigger ==
                    EnemyAbilityTrigger.OnCooldown)
                {
                    return state.CooldownRemaining;
                }
            }

            return 0f;
        }
    }
    public bool AreAllActionsDisabled =>
        HasStatusControl(StatusEffectControlType.DisableAllActions);
    public event Action<BattleStatusChangedEvent> StatusChanged;
    public event Action<StatusEffectLifecycleEvent> StatusLifecycle;

    public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
    {
        if (_statusEffects.Count == 0)
            return Array.Empty<BattleStatusSnapshot>();

        List<BattleStatusSnapshot> snapshots =
            new(_statusEffects.Count);
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }

            snapshots.Add(CreateStatusSnapshot(state));
        }

        snapshots.Sort((left, right) => string.Compare(
            left.Definition?.StatusId,
            right.Definition?.StatusId,
            StringComparison.Ordinal));
        return snapshots.Count > 0
            ? snapshots.ToArray()
            : Array.Empty<BattleStatusSnapshot>();
    }

    public EnemyRuntime(EnemySO definition, int maximumHealthOverride = 0)
    {
        Definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        MaxHealth = maximumHealthOverride > 0
            ? maximumHealthOverride
            : Definition.BaseHealth;
        MaxHealth = Mathf.Max(1, MaxHealth);
        Health = MaxHealth;
        Armor = 0;
        InitializeAbilityStates();
    }

    internal void SetHealth(int health)
    {
        Health = Mathf.Max(1, health);
        MaxHealth = Mathf.Max(MaxHealth, Health);
    }

    internal int TakeDamage(int damage)
    {
        return TakeDamage(damage, CharacterAttackDamageType.Physical);
    }

    internal int TakeDamage(
        int damage,
        CharacterAttackDamageType damageType)
    {
        damage = Mathf.Max(0, damage);
        if (damage <= 0 || Health <= 0)
            return 0;

        damage = ResolveIncomingDamage(damage);
        if (damage <= 0)
            return 0;

        if (damageType == CharacterAttackDamageType.StatusEffect ||
            damageType == CharacterAttackDamageType.StatusRemoval)
            return 0;

        int appliedDamage = 0;
        if (CurrentShield > 0)
        {
            int shieldDamage = Mathf.Min(CurrentShield, damage);
            CurrentShield -= shieldDamage;
            damage -= shieldDamage;
            appliedDamage += shieldDamage;
        }

        if (damage <= 0)
            return appliedDamage;

        if (damageType == CharacterAttackDamageType.Fixed)
        {
            int fixedDamage = Mathf.Min(Health, damage);
            Health -= fixedDamage;
            return appliedDamage + fixedDamage;
        }

        if (damageType == CharacterAttackDamageType.Physical && Armor > 0)
        {
            int armorDamage = Mathf.Min(Armor, damage);
            Armor -= armorDamage;
            damage -= armorDamage;
            appliedDamage += armorDamage;
        }

        if (damage <= 0)
            return appliedDamage;

        int healthDamage = Mathf.Min(Health, damage);
        Health -= healthDamage;
        return appliedDamage + healthDamage;
    }

    private int ResolveIncomingDamage(int damage)
    {
        StatusEffectStatAccumulator accumulator = default;
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }

            IReadOnlyList<StatusEffectStatModifierDefinition> modifiers =
                state.Definition.StatModifiers;
            if (modifiers == null)
                continue;

            int stacks = Mathf.Max(1, state.StackCount);
            foreach (StatusEffectStatModifierDefinition modifier in
                     modifiers)
            {
                if (modifier != null &&
                    modifier.StatType ==
                    StatusEffectStatType.IncomingDamage)
                {
                    accumulator.Add(modifier, stacks);
                }
            }
        }

        float modifiedDamage = accumulator.Evaluate(damage);
        if (float.IsNaN(modifiedDamage) || modifiedDamage <= 0f)
            return 0;
        if (float.IsInfinity(modifiedDamage) ||
            modifiedDamage >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, Mathf.RoundToInt(modifiedDamage));
    }

    public int Heal(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || Health <= 0 || Health >= MaxHealth)
            return 0;

        int previousHealth = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        return Health - previousHealth;
    }

    public int GainShield(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || Health <= 0 || CurrentShield == int.MaxValue)
            return 0;

        int previousShield = CurrentShield;
        long total = (long)CurrentShield + amount;
        CurrentShield = total >= int.MaxValue
            ? int.MaxValue
            : (int)total;
        return CurrentShield - previousShield;
    }

    internal int GainArmor(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || Health <= 0 || Armor == int.MaxValue)
            return 0;

        int previousArmor = Armor;
        long total = (long)Armor + amount;
        Armor = total >= int.MaxValue
            ? int.MaxValue
            : (int)total;
        return Armor - previousArmor;
    }

    internal bool CanSpendHealth(int amount)
    {
        return amount > 0 && Health - amount >= 1;
    }

    internal bool TrySpendHealth(int amount)
    {
        if (!CanSpendHealth(amount))
            return false;

        Health -= amount;
        return true;
    }

    internal void ApplyFire(
        float duration,
        float tickInterval,
        int tickDamage,
        IBattleCharacter source)
    {
        StatusEffectSO fire =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        ApplyStatusEffect(
            fire,
            duration,
            tickDamage,
            source,
            tickInterval);
    }

    internal bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks)
    {
        return ApplyStatusEffect(
            definition,
            duration,
            stacks,
            null,
            definition != null ? definition.TickInterval : 0f);
    }

    internal bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval)
    {
        return ApplyStatusEffect(
            definition,
            duration,
            stacks,
            source,
            tickInterval,
            null);
    }

    internal bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        BeginStatusMutation();
        try
        {
            return ApplyStatusEffectCore(
                definition,
                duration,
                stacks,
                source,
                tickInterval,
                applyDamage);
        }
        finally
        {
            EndStatusMutation();
        }
    }

    private bool ApplyStatusEffectCore(
        StatusEffectSO definition,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        if (definition == null || !definition.CanTargetEnemy || stacks <= 0 ||
            string.IsNullOrWhiteSpace(definition.StatusId))
        {
            return false;
        }

        float remainingDuration = ResolveStatusDuration(definition, duration);
        if (remainingDuration <= 0f)
            return false;

        tickInterval = TimePrecision.Normalize(
            tickInterval > 0f ? tickInterval : definition.TickInterval,
            TimePrecision.Step);
        BattleStatusSnapshot previousSnapshot = default;
        bool wasActive = false;
        if (!_statusEffects.TryGetValue(
                definition.StatusId,
                out StatusEffectRuntimeState state))
        {
            state = new StatusEffectRuntimeState(definition);
            _statusEffects.Add(definition.StatusId, state);
        }
        else if (state != null && state.HasStacks)
        {
            wasActive = true;
            previousSnapshot = CreateStatusSnapshot(state);
        }

        StatusEffectRuntimeMutation mutation = state.Apply(
            stacks,
            remainingDuration,
            tickInterval,
            source);
        if (!mutation.Succeeded && !state.HasStacks)
            _statusEffects.Remove(definition.StatusId);
        if (!mutation.Succeeded)
            return false;

        NotifyStatusChanged(
            wasActive
                ? BattleStatusChangeType.Reapplied
                : BattleStatusChangeType.Applied,
            previousSnapshot,
            CreateStatusSnapshot(state));
        bool continueExecution = ExecuteStatusDamageOperations(
            definition,
            StatusEffectOperationTrigger.OnApply,
            mutation.CurrentStacks,
            1,
            mutation.Source,
            applyDamage);
        if (continueExecution && mutation.StackChanged)
        {
            ExecuteStatusDamageOperations(
                definition,
                StatusEffectOperationTrigger.OnStackChanged,
                mutation.CurrentStacks,
                1,
                mutation.Source,
                applyDamage);
        }

        return true;
    }

    internal bool TickStatusEffects(
        float deltaTime,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        BeginStatusMutation();
        try
        {
            return TickStatusEffectsCore(deltaTime, applyDamage);
        }
        finally
        {
            EndStatusMutation();
        }
    }

    private bool TickStatusEffectsCore(
        float deltaTime,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        if (deltaTime <= 0f || _statusEffects.Count == 0)
            return false;

        bool changed = false;
        List<string> statusIds = new(_statusEffects.Keys);
        foreach (string statusId in statusIds)
        {
            if (!_statusEffects.TryGetValue(
                    statusId,
                    out StatusEffectRuntimeState state))
            {
                continue;
            }

            bool continueTick = TickStatusEffectState(
                state,
                deltaTime,
                applyDamage,
                ref changed);
            if (!state.HasStacks)
                _statusEffects.Remove(statusId);
            if (!continueTick)
                return changed;
        }

        return changed;
    }

    internal bool HasStatusEffect(StatusEffectSO definition)
    {
        if (definition == null)
            return false;

        return TryGetStatusState(definition.StatusId, out _);
    }

    internal int GetStatusStackCount(StatusEffectSO definition)
    {
        if (definition == null)
            return 0;

        return TryGetStatusState(
            definition.StatusId,
            out StatusEffectRuntimeState state)
                ? state.StackCount
                : 0;
    }

    internal float GetStatusRemainingDuration(StatusEffectSO definition)
    {
        if (definition == null)
            return 0f;

        return TryGetStatusState(
            definition.StatusId,
            out StatusEffectRuntimeState state)
                ? state.RemainingDuration
                : 0f;
    }

    internal int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount)
    {
        return RemoveStatusEffects(
            removalTarget,
            statusEffect,
            removalCount,
            null);
    }

    internal int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        BeginStatusMutation();
        try
        {
            return RemoveStatusEffectsCore(
                removalTarget,
                statusEffect,
                removalCount,
                applyDamage);
        }
        finally
        {
            EndStatusMutation();
        }
    }

    private int RemoveStatusEffectsCore(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        removalCount = Mathf.Max(0, removalCount);
        return removalTarget switch
        {
            CharacterStatusRemovalTarget.Single =>
                RemoveStatusEffect(
                    statusEffect,
                    removalCount,
                    applyDamage,
                    out _),
            CharacterStatusRemovalTarget.Random =>
                RemoveRandomStatusEffect(
                    removalCount,
                    applyDamage),
            CharacterStatusRemovalTarget.All =>
                RemoveAllStatusEffects(
                    removalCount,
                    applyDamage),
            _ => 0
        };
    }

    private int RemoveStatusEffect(
        StatusEffectSO statusEffect,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage,
        out bool continueExecution)
    {
        continueExecution = true;
        if (statusEffect == null || !statusEffect.Removable)
            return 0;

        return RemoveStatusStacks(
            statusEffect.StatusId,
            removalCount,
            applyDamage,
            out continueExecution);
    }

    private int RemoveRandomStatusEffect(
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        List<StatusEffectSO> candidates = new();
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state.Definition != null &&
                state.Definition.IncludedInRandomRemoval)
            {
                candidates.Add(state.Definition);
            }
        }

        if (candidates.Count == 0)
            return 0;

        return RemoveStatusEffect(
            candidates[UnityEngine.Random.Range(0, candidates.Count)],
            removalCount,
            applyDamage,
            out _);
    }

    private int RemoveAllStatusEffects(
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        // 카운트가 1 이상이면 각 상태 종류에서 해당 수만큼 제거한다.
        // 0이면 모든 종류의 모든 스택을 제거한다.
        int removed = 0;
        List<string> statusIds = new(_statusEffects.Keys);
        foreach (string statusId in statusIds)
        {
            if (_statusEffects.TryGetValue(
                    statusId,
                    out StatusEffectRuntimeState state) &&
                state.Definition != null &&
                state.Definition.IncludedInAllRemoval)
            {
                removed += RemoveStatusStacks(
                    statusId,
                    removalCount,
                    applyDamage,
                    out bool continueExecution);
                if (!continueExecution)
                    break;
            }
        }

        return removed;
    }

    private int RemoveStatusStacks(
        string statusId,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage,
        out bool continueExecution)
    {
        continueExecution = true;
        if (string.IsNullOrWhiteSpace(statusId) ||
            !_statusEffects.TryGetValue(
                statusId,
                out StatusEffectRuntimeState state) ||
            state.Definition == null || !state.Definition.Removable)
        {
            return 0;
        }

        BattleStatusSnapshot previousSnapshot =
            CreateStatusSnapshot(state);
        StatusEffectRuntimeMutation mutation =
            state.RemoveStacks(removalCount);
        if (!mutation.Succeeded)
            return 0;

        BattleStatusSnapshot currentSnapshot =
            CreateStatusSnapshot(state);
        if (!state.HasStacks)
            _statusEffects.Remove(statusId);
        NotifyStatusChanged(
            state.HasStacks
                ? BattleStatusChangeType.StackChanged
                : BattleStatusChangeType.Removed,
            previousSnapshot,
            currentSnapshot);
        continueExecution = ExecuteStatusDamageOperations(
            state.Definition,
            StatusEffectOperationTrigger.OnStackChanged,
            mutation.CurrentStacks,
            1,
            mutation.Source,
            applyDamage);
        if (continueExecution && mutation.CurrentStacks == 0)
        {
            continueExecution = ExecuteStatusDamageOperations(
                state.Definition,
                StatusEffectOperationTrigger.OnRemove,
                mutation.CurrentStacks,
                1,
                mutation.Source,
                applyDamage);
        }

        return mutation.RemovedStacks;
    }

    private bool TickStatusEffectState(
        StatusEffectRuntimeState state,
        float deltaTime,
        Func<int, IBattleCharacter, bool> applyDamage,
        ref bool changed)
    {
        float remainingDelta = deltaTime;
        while (remainingDelta > 0f && state.HasStacks)
        {
            StatusEffectRuntimeMutation expiredMutation =
                RemoveExpiredStatusBatch(state);
            if (expiredMutation.Succeeded)
            {
                changed = true;
                if (!ExecuteExpirationOperations(
                        state.Definition,
                        expiredMutation,
                        applyDamage))
                {
                    return false;
                }
                continue;
            }

            StatusEffectRuntimeBatch activeBatch = state.ActiveBatch;
            float activeDelta = state.AdvanceActiveDuration(remainingDelta);
            if (activeDelta <= 0f)
                break;

            remainingDelta -= activeDelta;
            int tickCount = state.ConsumePendingTickCount();
            if (tickCount > 0)
            {
                StatusEffectLifecycleEvent tick =
                    StatusEffectLifecycleResolver.ResolveTick(
                        BattleStatusTarget.FromEnemy(this),
                        new BattleStatusSnapshot(
                            state.Definition,
                            activeBatch.Stacks,
                            activeBatch.RemainingDuration,
                            activeBatch.Source),
                        tickCount);
                StatusLifecycle?.Invoke(tick);
                BattleEffectResult triggerResult =
                    StatusEffectTriggerExecutor.Execute(
                        tick,
                        null,
                        applyDamage);
                if (triggerResult.Attempted &&
                    !triggerResult.Succeeded)
                {
                    return false;
                }
                if (!ExecuteStatusDamageOperations(
                        state.Definition,
                        StatusEffectOperationTrigger.OnTick,
                        activeBatch.Stacks,
                        tickCount,
                        activeBatch.Source,
                        applyDamage))
                {
                    return false;
                }
            }

            expiredMutation = RemoveExpiredStatusBatch(state);
            if (expiredMutation.Succeeded)
            {
                changed = true;
                if (!ExecuteExpirationOperations(
                        state.Definition,
                        expiredMutation,
                        applyDamage))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private StatusEffectRuntimeMutation RemoveExpiredStatusBatch(
        StatusEffectRuntimeState state)
    {
        if (state == null)
            return default;

        BattleStatusSnapshot previousSnapshot =
            CreateStatusSnapshot(state);
        StatusEffectRuntimeMutation mutation =
            state.RemoveExpiredActiveBatch();
        if (!mutation.Succeeded)
            return default;

        BattleStatusSnapshot currentSnapshot =
            CreateStatusSnapshot(state);
        NotifyStatusChanged(
            state.HasStacks
                ? BattleStatusChangeType.StackChanged
                : BattleStatusChangeType.Expired,
            previousSnapshot,
            currentSnapshot);
        return mutation;
    }

    private bool ExecuteExpirationOperations(
        StatusEffectSO definition,
        StatusEffectRuntimeMutation mutation,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        bool continueExecution = ExecuteStatusDamageOperations(
            definition,
            StatusEffectOperationTrigger.OnStackChanged,
            mutation.CurrentStacks,
            1,
            mutation.Source,
            applyDamage);
        if (!continueExecution || mutation.CurrentStacks > 0)
            return continueExecution;

        return ExecuteStatusDamageOperations(
            definition,
            StatusEffectOperationTrigger.OnExpire,
            mutation.CurrentStacks,
            1,
            mutation.Source,
            applyDamage);
    }

    private bool ExecuteStatusDamageOperations(
        StatusEffectSO definition,
        StatusEffectOperationTrigger trigger,
        int eventStacks,
        int occurrenceCount,
        IBattleCharacter source,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        IReadOnlyList<StatusEffectOperationDefinition> operations =
            definition?.Operations;
        if (operations == null || occurrenceCount <= 0)
            return true;

        StatusEffectOperationType supportedOperation =
            trigger == StatusEffectOperationTrigger.OnTick
                ? StatusEffectOperationType.PeriodicDamage
                : StatusEffectOperationType.InstantDamage;
        foreach (StatusEffectOperationDefinition operation in operations)
        {
            if (operation == null ||
                operation.Trigger != trigger ||
                operation.OperationType != supportedOperation)
            {
                continue;
            }

            int damage = ResolveStatusDamage(
                operation,
                eventStacks,
                occurrenceCount);
            if (damage <= 0 || applyDamage == null)
                continue;
            if (!applyDamage(damage, source))
                return false;
        }

        return true;
    }

    private int ResolveStatusDamage(
        StatusEffectOperationDefinition operation,
        int eventStacks,
        int occurrenceCount)
    {
        float value = operation.ValueMode switch
        {
            StatusEffectValueMode.Fixed => operation.Value,
            StatusEffectValueMode.Ratio => MaxHealth * operation.Value,
            _ => 0f
        };
        if (operation.ScaleWithStacks)
            value *= Mathf.Max(1, eventStacks);
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            return 0;

        int damagePerOccurrence = Mathf.Max(0, Mathf.RoundToInt(value));
        long totalDamage = (long)damagePerOccurrence *
                           Mathf.Max(0, occurrenceCount);
        return (int)Math.Min(int.MaxValue, totalDamage);
    }

    private static BattleStatusSnapshot CreateStatusSnapshot(
        StatusEffectRuntimeState state)
    {
        return state?.Definition != null
            ? new BattleStatusSnapshot(
                state.Definition,
                state.StackCount,
                state.RemainingDuration,
                state.ActiveBatch?.Source)
            : default;
    }

    private void NotifyStatusChanged(
        BattleStatusChangeType changeType,
        BattleStatusSnapshot previous,
        BattleStatusSnapshot current)
    {
        BattleStatusChangedEvent eventData = new(
            BattleStatusTarget.FromEnemy(this),
            changeType,
            previous,
            current);
        if (eventData.IsValid)
            _statusChangeQueue.Enqueue(eventData);
        DispatchStatusChanges();
    }

    private void BeginStatusMutation()
    {
        _statusMutationDepth++;
    }

    private void EndStatusMutation()
    {
        _statusMutationDepth = Mathf.Max(0, _statusMutationDepth - 1);
        DispatchStatusChanges();
    }

    private void DispatchStatusChanges()
    {
        if (_statusMutationDepth > 0 || _dispatchingStatusChanges)
            return;

        _dispatchingStatusChanges = true;
        try
        {
            int dispatchedCount = 0;
            while (_statusChangeQueue.Count > 0)
            {
                if (dispatchedCount >= MaximumStatusChangesPerDispatch)
                {
                    int discardedCount = _statusChangeQueue.Count;
                    _statusChangeQueue.Clear();
                    Debug.LogError(
                        $"Status change dispatch exceeded " +
                        $"{MaximumStatusChangesPerDispatch} events. " +
                        $"Discarded {discardedCount} queued events to stop " +
                        $"a re-entrant lifecycle loop.");
                    break;
                }

                BattleStatusChangedEvent eventData =
                    _statusChangeQueue.Dequeue();
                dispatchedCount++;
                StatusChanged?.Invoke(eventData);
                foreach (StatusEffectLifecycleEvent lifecycleEvent in
                         StatusEffectLifecycleResolver.Resolve(eventData))
                {
                    StatusLifecycle?.Invoke(lifecycleEvent);
                    StatusEffectTriggerExecutor.Execute(lifecycleEvent);
                }
            }
        }
        finally
        {
            _dispatchingStatusChanges = false;
        }
    }

    private bool TryGetStatusState(
        string statusId,
        out StatusEffectRuntimeState state)
    {
        state = null;
        return !string.IsNullOrWhiteSpace(statusId) &&
               _statusEffects.TryGetValue(statusId, out state) &&
               state != null && state.HasStacks;
    }

    private static float ResolveStatusDuration(
        StatusEffectSO definition,
        float duration)
    {
        if (definition.DurationMode == StatusEffectDurationMode.Permanent)
            return float.PositiveInfinity;

        return TimePrecision.Normalize(
            duration > 0f ? duration : definition.DefaultDuration,
            0.1f);
    }

    internal bool TickAbilityCooldown(float deltaTime)
    {
        foreach (EnemyAbilityRuntimeState state in _abilityStates)
        {
            if (state.Definition.Trigger ==
                EnemyAbilityTrigger.OnCooldown)
            {
                return state.TickCooldown(
                    deltaTime,
                    AreAllActionsDisabled);
            }
        }

        return false;
    }

    internal IReadOnlyList<EnemyAbilityRuntimeState> AbilityStates =>
        _abilityStates;

    internal int GetAbilityRemainingCharges(string abilityId)
    {
        foreach (EnemyAbilityRuntimeState state in _abilityStates)
        {
            if (string.Equals(
                    state.Definition.AbilityId,
                    abilityId,
                    StringComparison.Ordinal))
            {
                return state.RemainingCharges;
            }
        }

        return 0;
    }

    private void InitializeAbilityStates()
    {
        _abilityStates.Clear();
        foreach (EnemyAbilityDefinition ability in Definition.Abilities)
        {
            if (ability != null)
                _abilityStates.Add(new EnemyAbilityRuntimeState(ability));
        }

        _abilityStates.Sort((left, right) =>
        {
            int priority = right.Definition.Priority.CompareTo(
                left.Definition.Priority);
            return priority != 0
                ? priority
                : string.Compare(
                    left.Definition.AbilityId,
                    right.Definition.AbilityId,
                    StringComparison.Ordinal);
        });
    }

    private bool HasStatusControl(StatusEffectControlType controlType)
    {
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }
            if (state.Definition.HasControl(controlType))
                return true;
            if (controlType != StatusEffectControlType.DisableAllActions ||
                state.Definition.Operations == null)
            {
                continue;
            }

            foreach (StatusEffectOperationDefinition operation in
                     state.Definition.Operations)
            {
                if (operation != null &&
                    operation.Trigger ==
                        StatusEffectOperationTrigger.OnApply &&
                    operation.OperationType ==
                        StatusEffectOperationType.DisableAction)
                {
                    return true;
                }
            }
        }

        return false;
    }

}
