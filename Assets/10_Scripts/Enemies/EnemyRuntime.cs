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
    private readonly List<EnemyCombatModifierRuntimeState>
        _combatModifiers = new();
    private readonly List<EnemyCombatModifier>
        _nextCoreAttackModifiers = new();
    private readonly HashSet<string> _triggeredHealthThresholds =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _nextNoDamageActivations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _lastDamageSourceTimes =
        new(StringComparer.Ordinal);
    private IBattleBoard _boundBattleBoard;
    private EnemyActiveChargeRuntimeState _activeCharge;
    private EnemyCombatModifier _readyCoreAttackModifier;
    private bool _hasReadyCoreAttackModifier;
    private float _fractionalCoreDamageRemainder;
    private float _combatElapsedTime;
    private float _timeSinceLastDamage;
    private float _summonedCoreAttackMultiplier = 1f;
    private float _untargetableRemaining;
    private float _reflectionRemaining;
    private float _reflectionRatio;
    private float _pendingCoreProtectionBypass;
    private int _currentPhaseIndex = -1;
    private int _statusMutationDepth;
    private bool _dispatchingStatusChanges;
    internal IBattleBoard BoundBattleBoard => _boundBattleBoard;
    private IEnemyCombatRuntimeService CombatRuntimeService =>
        (_boundBattleBoard as IEnemyCombatRuntimeServiceProvider)
        ?.EnemyCombatRuntimeService;

    internal void BindBattleBoard(IBattleBoard board)
    {
        _boundBattleBoard = board;
    }

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
    public bool IsTargetPriorityExcluded
    {
        get
        {
            if (IsUntargetable)
                return true;

            foreach (EnemyAbilityRuntimeState state in _abilityStates)
            {
                if (!IsAbilityEnabledInCurrentPhase(state.Definition) ||
                    !state.Definition.RespondsToTrigger(
                        EnemyAbilityTrigger.OnTargetPriorityEvaluation))
                {
                    continue;
                }

                foreach (EnemyAbilityOperationDefinition operation in
                         state.Definition.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType.ModifyTargetPriority &&
                        operation.TargetPriorityMode ==
                            EnemyTargetPriorityMode.Exclude)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    public float SpawnIntervalMultiplier => Definition.SpawnIntervalMultiplier;
    public bool IsUntargetable => Health > 0 &&
                                  _untargetableRemaining > 0f;
    public float ApproachSpeed => Definition.ApproachSpeed;
    public float FormationRadius => Definition.FormationRadius;
    public float CurrentAttackPower => Mathf.Max(
        0f,
        GetStatusModifiedStat(
            Definition.AttackPower,
            StatusEffectStatType.AttackPower,
            StatusEffectOperationType.AttackPowerModifier));
    public float CoreAttackDamageValue
    {
        get
        {
            float value = GetStatusModifiedStat(
                Definition.CoreAttackDamageValue *
                _summonedCoreAttackMultiplier,
                StatusEffectStatType.AttackPower,
                StatusEffectOperationType.AttackPowerModifier);
            value = EvaluateCombatModifiers(
                EnemyCombatModifierType.CoreAttackDamage,
                value);
            IEnemyCombatRuntimeService service = CombatRuntimeService;
            if (service != null)
            {
                value = service.ResolvePassiveModifier(
                    this,
                    EnemyCombatModifierType.CoreAttackDamage,
                    value);
            }

            return NormalizeNonNegative(value);
        }
    }
    public int CoreAttackDamage => SaturatingRoundToInt(
        CoreAttackDamageValue);
    public float CoreAttackInterval
    {
        get
        {
            float baseInterval = Definition.CoreAttackInterval;
            float baseSpeed = baseInterval > 0f
                ? 1f / baseInterval
                : 0f;
            float modifiedSpeed = GetStatusModifiedStat(
                baseSpeed,
                StatusEffectStatType.AttackSpeed,
                StatusEffectOperationType.AttackSpeedModifier);
            float value = modifiedSpeed > 0f
                ? 1f / modifiedSpeed
                : float.MaxValue;
            value = EvaluateCombatModifiers(
                EnemyCombatModifierType.CoreAttackInterval,
                value);
            IEnemyCombatRuntimeService service = CombatRuntimeService;
            if (service != null)
            {
                value = service.ResolvePassiveModifier(
                    this,
                    EnemyCombatModifierType.CoreAttackInterval,
                    value);
            }

            return Mathf.Max(TimePrecision.Step, NormalizeFinite(value));
        }
    }
    public float CoreAttackRange => Definition.CoreAttackRange;
    public float TimeSinceLastDamage =>
        TimePrecision.FloorToTenth(_timeSinceLastDamage);
    public bool IsCharging => _activeCharge != null;
    internal bool HasReadyChargedCoreAttack =>
        _hasReadyCoreAttackModifier;
    internal float PendingCoreProtectionBypass =>
        Mathf.Clamp01(_pendingCoreProtectionBypass);
    public EnemyChargeSnapshot ActiveCharge => _activeCharge != null
        ? _activeCharge.CreateSnapshot(this)
        : default;
    public bool IsSummoned { get; private set; }
    public int SummonDepth { get; private set; }
    public string SummonerEnemyId { get; private set; } = string.Empty;
    public string OriginAbilityId { get; private set; } = string.Empty;
    public int CurrentPhaseIndex => _currentPhaseIndex;
    public EnemyBossPhaseDefinition CurrentPhase =>
        _currentPhaseIndex >= 0 &&
        _currentPhaseIndex < Definition.PhaseDefinitions.Count
            ? Definition.PhaseDefinitions[_currentPhaseIndex]
            : null;
    public string CurrentPhaseId => CurrentPhase?.PhaseId ?? string.Empty;
    public float AbilityCooldownRemaining
    {
        get
        {
            foreach (EnemyAbilityRuntimeState state in _abilityStates)
            {
                if (IsAbilityEnabledInCurrentPhase(state.Definition) &&
                    state.Definition.RespondsToTrigger(
                        EnemyAbilityTrigger.OnCooldown))
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

    public bool TryExtendStatusDuration(
        StatusEffectSO definition,
        float seconds)
    {
        BeginStatusMutation();
        try
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.StatusId) ||
                !_statusEffects.TryGetValue(
                    definition.StatusId,
                    out StatusEffectRuntimeState state) ||
                state == null ||
                !state.HasStacks)
            {
                return false;
            }

            BattleStatusSnapshot previousSnapshot =
                CreateStatusSnapshot(state);
            if (!state.TryExtendDuration(seconds))
                return false;

            NotifyStatusChanged(
                BattleStatusChangeType.Reapplied,
                previousSnapshot,
                CreateStatusSnapshot(state));
            return true;
        }
        finally
        {
            EndStatusMutation();
        }
    }

    public EnemyRuntime(EnemySO definition, int maximumHealthOverride = 0)
    {
        Definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        int configuredHealth = maximumHealthOverride > 0
            ? maximumHealthOverride
            : Definition.BaseHealth;
        MaxHealth = Mathf.RoundToInt(
            configuredHealth * Definition.HealthScale);
        MaxHealth = Mathf.Max(1, MaxHealth);
        Health = MaxHealth;
        Armor = Definition.InitialArmor;
        CurrentShield = Definition.InitialShield;
        InitializeAbilityStates();
        InitializePhaseState();
    }

    internal int ResolveCoreAttackDamageForHit()
    {
        float value = CoreAttackDamageValue;
        if (_hasReadyCoreAttackModifier)
        {
            value = _readyCoreAttackModifier.IsValid
                ? EvaluateModifier(_readyCoreAttackModifier, value, 1)
                : value;
            _readyCoreAttackModifier = default;
            _hasReadyCoreAttackModifier = false;
        }
        foreach (EnemyCombatModifier modifier in _nextCoreAttackModifiers)
        {
            if (modifier.IsValid)
                value = EvaluateModifier(modifier, value, 1);
        }
        _nextCoreAttackModifiers.Clear();

        return EnemyCoreAttackDamageResolver.Resolve(
            NormalizeNonNegative(value),
            Definition.CoreAttackDamagePolicy,
            ref _fractionalCoreDamageRemainder);
    }

    internal bool ReserveNextCoreAttackModifier(
        EnemyCombatModifier modifier)
    {
        if (!modifier.IsValid || modifier.Type !=
            EnemyCombatModifierType.CoreAttackDamage)
        {
            return false;
        }

        for (int index = 0;
             index < _nextCoreAttackModifiers.Count;
             index++)
        {
            if (string.Equals(
                    _nextCoreAttackModifiers[index].SourceId,
                    modifier.SourceId,
                    StringComparison.Ordinal))
            {
                _nextCoreAttackModifiers[index] = modifier;
                return true;
            }
        }

        _nextCoreAttackModifiers.Add(modifier);
        _nextCoreAttackModifiers.Sort((left, right) => string.Compare(
            left.SourceId,
            right.SourceId,
            StringComparison.Ordinal));
        return true;
    }

    internal bool ReserveReadyChargedCoreAttackModifier(
        EnemyCombatModifier modifier)
    {
        if (!modifier.IsValid || modifier.Type !=
            EnemyCombatModifierType.CoreAttackDamage)
        {
            return false;
        }

        _readyCoreAttackModifier = modifier;
        _hasReadyCoreAttackModifier = true;
        return true;
    }

    internal bool ReserveNextCoreProtectionBypass(float ratio)
    {
        if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0f)
            return false;

        float resolved = Mathf.Clamp01(ratio);
        if (resolved <= _pendingCoreProtectionBypass)
            return false;
        _pendingCoreProtectionBypass = resolved;
        return true;
    }

    internal float ConsumeNextCoreProtectionBypass()
    {
        float resolved = Mathf.Clamp01(_pendingCoreProtectionBypass);
        _pendingCoreProtectionBypass = 0f;
        return resolved;
    }

    internal void MarkSummoned(
        EnemyRuntime summoner,
        string abilityId,
        float healthMultiplier,
        float coreAttackMultiplier)
    {
        IsSummoned = true;
        SummonDepth = Mathf.Max(1, (summoner?.SummonDepth ?? 0) + 1);
        SummonerEnemyId = summoner?.Definition?.EnemyId ?? string.Empty;
        OriginAbilityId = (abilityId ?? string.Empty).Trim();
        float appliedHealthMultiplier = IsFinitePositive(healthMultiplier)
            ? healthMultiplier
            : 1f;
        _summonedCoreAttackMultiplier =
            IsFinitePositive(coreAttackMultiplier)
                ? coreAttackMultiplier
                : 1f;
        MaxHealth = Mathf.Max(
            1,
            SaturatingRoundToInt(MaxHealth * appliedHealthMultiplier));
        Health = MaxHealth;
    }

    internal bool ApplyCombatModifier(EnemyCombatModifier modifier)
    {
        if (!modifier.IsValid)
            return false;

        foreach (EnemyCombatModifierRuntimeState state in _combatModifiers)
        {
            if (state != null &&
                state.Definition.Type == modifier.Type &&
                string.Equals(
                    state.Definition.SourceId,
                    modifier.SourceId,
                    StringComparison.Ordinal))
            {
                return state.Reapply();
            }
        }

        _combatModifiers.Add(
            new EnemyCombatModifierRuntimeState(modifier));
        SortCombatModifiers();
        return true;
    }

    internal bool TrySetUntargetable(float duration)
    {
        duration = TimePrecision.Normalize(duration);
        if (Health <= 0 || duration <= 0f || float.IsNaN(duration) ||
            float.IsInfinity(duration) ||
            duration <= _untargetableRemaining)
        {
            return false;
        }

        _untargetableRemaining = duration;
        return true;
    }

    internal bool TryReserveDamageReflection(float ratio, float duration)
    {
        ratio = Mathf.Clamp01(ratio);
        duration = TimePrecision.Normalize(duration);
        if (Health <= 0 || ratio <= 0f || float.IsNaN(ratio) ||
            float.IsInfinity(ratio) || float.IsNaN(duration) ||
            float.IsInfinity(duration) || duration < 0f)
        {
            return false;
        }

        _reflectionRatio = Mathf.Max(_reflectionRatio, ratio);
        _reflectionRemaining = duration > 0f
            ? Mathf.Max(_reflectionRemaining, duration)
            : float.PositiveInfinity;
        return true;
    }

    internal bool TryConsumeDamageReflection(out float ratio)
    {
        ratio = 0f;
        if (Health <= 0 || _reflectionRatio <= 0f ||
            _reflectionRemaining <= 0f)
        {
            return false;
        }

        ratio = _reflectionRatio;
        _reflectionRatio = 0f;
        _reflectionRemaining = 0f;
        return true;
    }

    internal int RemoveCombatModifiers(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return 0;

        int removed = 0;
        for (int index = _combatModifiers.Count - 1;
             index >= 0;
             index--)
        {
            EnemyCombatModifierRuntimeState state =
                _combatModifiers[index];
            if (state != null && string.Equals(
                    state.Definition.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                _combatModifiers.RemoveAt(index);
                removed++;
            }
        }

        return removed;
    }

    internal int RemoveCombatModifierStacks(
        EnemyCombatModifierType type,
        int count)
    {
        count = Mathf.Max(0, count);
        int removed = 0;
        for (int index = _combatModifiers.Count - 1;
             index >= 0 && removed < count;
             index--)
        {
            EnemyCombatModifierRuntimeState state =
                _combatModifiers[index];
            if (state == null || state.Definition.Type != type ||
                state.Definition.Percentage <= 0f)
            {
                continue;
            }

            removed += state.RemoveStacks(count - removed);
            if (!state.IsActive)
                _combatModifiers.RemoveAt(index);
        }
        return removed;
    }

    internal void TickCombatRuntime(
        float deltaTime,
        out EnemyActiveChargeRuntimeState completedCharge)
    {
        completedCharge = null;
        if (deltaTime <= 0f || Health <= 0)
            return;

        _combatElapsedTime = Mathf.Min(
            float.MaxValue,
            _combatElapsedTime + deltaTime);
        _timeSinceLastDamage = Mathf.Min(
            float.MaxValue,
            _timeSinceLastDamage + deltaTime);
        _untargetableRemaining = Mathf.Max(
            0f,
            _untargetableRemaining - deltaTime);
        if (!float.IsPositiveInfinity(_reflectionRemaining))
        {
            _reflectionRemaining = Mathf.Max(
                0f,
                _reflectionRemaining - deltaTime);
            if (_reflectionRemaining <= 0f)
                _reflectionRatio = 0f;
        }
        for (int index = _combatModifiers.Count - 1;
             index >= 0;
             index--)
        {
            EnemyCombatModifierRuntimeState state =
                _combatModifiers[index];
            if (state == null || state.Tick(deltaTime))
                _combatModifiers.RemoveAt(index);
        }

        if (_activeCharge == null)
            return;

        bool completed = _activeCharge.Tick(
            deltaTime,
            out bool telegraphStarted);
        if (telegraphStarted)
        {
            CombatRuntimeService?.PublishEnemyCombatEvent(
                new EnemyCombatEvent(
                    EnemyCombatEventType.TelegraphStarted,
                    this,
                    ability: _activeCharge.AbilityState?.Definition,
                    charge: _activeCharge.CreateSnapshot(this)));
        }
        if (!completed)
            return;

        completedCharge = _activeCharge;
        _activeCharge = null;
        if (completedCharge.IsCoreAttackCharge)
        {
            _readyCoreAttackModifier =
                completedCharge.CoreAttackModifier;
            _hasReadyCoreAttackModifier =
                _readyCoreAttackModifier.IsValid;
        }
        CombatRuntimeService?.PublishEnemyCombatEvent(
            new EnemyCombatEvent(
                EnemyCombatEventType.ChargeCompleted,
                this,
                ability: completedCharge.AbilityState?.Definition,
                charge: completedCharge.CreateSnapshot(this)));
    }

    internal bool TryBeginAbilityCharge(
        EnemyAbilityRuntimeState abilityState,
        out EnemyChargeSnapshot charge)
    {
        charge = default;
        EnemyAbilityDefinition ability = abilityState?.Definition;
        EnemyAbilityChargeDefinition definition = ability?.Charge;
        if (ability == null || definition?.IsEnabled != true ||
            definition.Duration <= 0f || _activeCharge != null)
        {
            return false;
        }

        string sourceId = ResolveModifierSourceId(ability, null);
        _activeCharge = new EnemyActiveChargeRuntimeState(
            abilityState,
            sourceId,
            definition.Duration,
            false,
            definition.IsInterruptible,
            definition.Interrupts,
            ability.Telegraph);
        charge = _activeCharge.CreateSnapshot(this);
        CombatRuntimeService?.PublishEnemyCombatEvent(
            new EnemyCombatEvent(
                EnemyCombatEventType.ChargeStarted,
                this,
                ability: ability,
                charge: charge));
        if (_activeCharge.ShouldTelegraph &&
            ability.Telegraph.LeadTime >= definition.Duration)
        {
            CombatRuntimeService?.PublishEnemyCombatEvent(
                new EnemyCombatEvent(
                    EnemyCombatEventType.TelegraphStarted,
                    this,
                    ability: ability,
                    charge: charge));
        }
        return true;
    }

    internal bool TryBeginCoreAttackCharge(
        EnemyAbilityRuntimeState abilityState,
        EnemyAbilityOperationDefinition operation,
        out EnemyChargeSnapshot charge)
    {
        charge = default;
        if (abilityState?.Definition == null || operation == null ||
            _activeCharge != null)
        {
            return false;
        }

        EnemyAbilityDefinition ability = abilityState.Definition;
        EnemyAbilityChargeDefinition definition = ability.Charge;
        float duration = operation.Duration > 0f
            ? operation.Duration
            : definition.IsEnabled
                ? definition.Duration
                : 0f;
        if (duration <= 0f)
            return false;

        string sourceId = ResolveModifierSourceId(ability, operation);
        EnemyCombatModifier modifier = new(
            sourceId,
            EnemyCombatModifierType.CoreAttackDamage,
            operation.Amount,
            operation.Percentage,
            operation.Multiplier,
            maximumStacks: 1);
        bool interruptible = definition.IsEnabled
            ? definition.IsInterruptible
            : true;
        EnemyChargeInterruptFlags interrupts = definition.IsEnabled
            ? definition.Interrupts
            : EnemyChargeInterruptFlags.Stun |
              EnemyChargeInterruptFlags.DirectDamage;
        _activeCharge = new EnemyActiveChargeRuntimeState(
            abilityState,
            sourceId,
            duration,
            true,
            interruptible,
            interrupts,
            ability.Telegraph,
            modifier);
        charge = _activeCharge.CreateSnapshot(this);
        CombatRuntimeService?.PublishEnemyCombatEvent(
            new EnemyCombatEvent(
                EnemyCombatEventType.ChargeStarted,
                this,
                ability: ability,
                charge: charge));
        return true;
    }

    internal bool TryInterruptCharge(
        EnemyChargeInterruptReason reason,
        out EnemyActiveChargeRuntimeState interruptedCharge)
    {
        interruptedCharge = null;
        if (_activeCharge == null || !_activeCharge.CanInterrupt(reason))
            return false;

        interruptedCharge = _activeCharge;
        _activeCharge = null;
        CombatRuntimeService?.PublishEnemyCombatEvent(
            new EnemyCombatEvent(
                EnemyCombatEventType.ChargeInterrupted,
                this,
                ability: interruptedCharge.AbilityState?.Definition,
                charge: interruptedCharge.CreateSnapshot(this)));
        return true;
    }

    internal bool IsAbilityCharging(EnemyAbilityDefinition ability)
    {
        return ability != null &&
               ReferenceEquals(
                   _activeCharge?.AbilityState?.Definition,
                   ability);
    }

    internal bool WasDamagedBySourceWithin(
        string damageSourceId,
        float windowDuration)
    {
        damageSourceId = (damageSourceId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(damageSourceId) ||
            float.IsNaN(windowDuration) ||
            float.IsInfinity(windowDuration) ||
            windowDuration <= 0f ||
            !_lastDamageSourceTimes.TryGetValue(
                damageSourceId,
                out float lastDamageTime))
        {
            return false;
        }

        float elapsed = _combatElapsedTime - lastDamageTime;
        return elapsed >= 0f && elapsed <= windowDuration;
    }

    internal void RecordDamageTaken(string damageSourceId = null)
    {
        _timeSinceLastDamage = 0f;
        _nextNoDamageActivations.Clear();

        damageSourceId = (damageSourceId ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(damageSourceId))
            _lastDamageSourceTimes[damageSourceId] = _combatElapsedTime;
    }

    internal bool TryMarkHealthThresholdCrossed(
        EnemyAbilityDefinition ability,
        int previousHealth,
        int currentHealth)
    {
        if (ability == null || ability.HealthThresholdPercent <= 0f ||
            MaxHealth <= 0 || currentHealth >= previousHealth ||
            _triggeredHealthThresholds.Contains(ability.AbilityId))
        {
            return false;
        }

        float previousPercent = previousHealth * 100f / MaxHealth;
        float currentPercent = currentHealth * 100f / MaxHealth;
        if (previousPercent <= ability.HealthThresholdPercent ||
            currentPercent > ability.HealthThresholdPercent)
        {
            return false;
        }

        _triggeredHealthThresholds.Add(ability.AbilityId);
        return true;
    }

    internal bool TryMarkNoDamageDurationReached(
        EnemyAbilityDefinition ability)
    {
        if (ability == null || ability.NoDamageDuration <= 0f)
        {
            return false;
        }

        float nextActivation = _nextNoDamageActivations.TryGetValue(
            ability.AbilityId,
            out float configuredNext)
                ? configuredNext
                : ability.NoDamageDuration;
        if (_timeSinceLastDamage < nextActivation)
            return false;

        float interval = ResolveNoDamageRepeatInterval(ability);
        _nextNoDamageActivations[ability.AbilityId] = interval > 0f
            ? nextActivation + interval
            : float.PositiveInfinity;
        return true;
    }

    internal bool IsAbilityEnabledInCurrentPhase(
        EnemyAbilityDefinition ability)
    {
        if (ability == null)
            return false;

        EnemyBossPhaseDefinition phase = CurrentPhase;
        if (phase == null || phase.AbilityIds.Count == 0)
            return true;

        foreach (string abilityId in phase.AbilityIds)
        {
            if (string.Equals(
                    abilityId,
                    ability.AbilityId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal bool TryAdvancePhaseForHealth(
        out EnemyBossPhaseDefinition previousPhase,
        out EnemyBossPhaseDefinition currentPhase)
    {
        previousPhase = CurrentPhase;
        currentPhase = previousPhase;
        IReadOnlyList<EnemyBossPhaseDefinition> phases =
            Definition.PhaseDefinitions;
        if (phases == null || phases.Count == 0 ||
            _currentPhaseIndex >= phases.Count - 1)
        {
            return false;
        }

        float healthPercent = MaxHealth > 0
            ? Health * 100f / MaxHealth
            : 0f;
        int resolvedIndex = _currentPhaseIndex;
        for (int index = _currentPhaseIndex + 1;
             index < phases.Count;
             index++)
        {
            EnemyBossPhaseDefinition phase = phases[index];
            if (phase == null)
                continue;
            if (healthPercent <= phase.MaximumHealthPercent &&
                healthPercent >= phase.MinimumHealthPercent)
            {
                resolvedIndex = index;
            }
        }

        if (resolvedIndex <= _currentPhaseIndex)
            return false;

        _currentPhaseIndex = resolvedIndex;
        currentPhase = CurrentPhase;
        return true;
    }

    internal bool TryAdvancePhaseOnCoreContact(
        out EnemyBossPhaseDefinition previousPhase,
        out EnemyBossPhaseDefinition currentPhase)
    {
        previousPhase = CurrentPhase;
        currentPhase = previousPhase;
        IReadOnlyList<EnemyBossPhaseDefinition> phases =
            Definition.PhaseDefinitions;
        if (previousPhase?.AdvanceOnCoreContact != true ||
            phases == null || _currentPhaseIndex < 0 ||
            _currentPhaseIndex >= phases.Count - 1)
        {
            return false;
        }

        _currentPhaseIndex++;
        currentPhase = CurrentPhase;
        return true;
    }

    private void InitializePhaseState()
    {
        IReadOnlyList<EnemyBossPhaseDefinition> phases =
            Definition.PhaseDefinitions;
        _currentPhaseIndex = phases != null && phases.Count > 0
            ? 0
            : -1;
        TryAdvancePhaseForHealth(out _, out _);
    }

    private static float ResolveNoDamageRepeatInterval(
        EnemyAbilityDefinition ability)
    {
        float interval = 0f;
        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation == null || !operation.Enabled ||
                operation.Interval <= 0f)
            {
                continue;
            }

            interval = interval <= 0f
                ? operation.Interval
                : Mathf.Min(interval, operation.Interval);
        }

        return interval;
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

        if (damageType == CharacterAttackDamageType.StatusEffect ||
            damageType == CharacterAttackDamageType.StatusRemoval)
            return 0;

        bool ignoresProtection =
            damageType == CharacterAttackDamageType.Fixed;
        damage = ResolveIncomingDamage(damage, ignoresProtection);
        if (damage <= 0)
            return 0;

        if (ignoresProtection)
        {
            int fixedDamage = Mathf.Min(Health, damage);
            Health -= fixedDamage;
            return fixedDamage;
        }

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

    private int ResolveIncomingDamage(
        int damage,
        bool ignoreProtectiveModifiers = false)
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
                    StatusEffectStatType.IncomingDamage &&
                    (!ignoreProtectiveModifiers || modifier.Value >= 0f))
                {
                    accumulator.Add(modifier, stacks);
                }
            }
        }

        float modifiedDamage = accumulator.Evaluate(damage);
        foreach (EnemyCombatModifierRuntimeState state in _combatModifiers)
        {
            if (state == null || !state.IsActive ||
                state.Definition.Type !=
                    EnemyCombatModifierType.IncomingDamage)
            {
                continue;
            }

            float candidate = state.Evaluate(modifiedDamage);
            if (!ignoreProtectiveModifiers || candidate >= modifiedDamage)
                modifiedDamage = candidate;
        }
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
            source != null
                ? BattleAbilityUser.FromCharacter(source)
                : BattleAbilityUser.ForStatusEffect(),
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
            BattleAbilityUser.ForStatusEffect(),
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
            source != null
                ? BattleAbilityUser.FromCharacter(source)
                : BattleAbilityUser.ForStatusEffect(),
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
        return ApplyStatusEffect(
            definition,
            duration,
            stacks,
            source != null
                ? BattleAbilityUser.FromCharacter(source)
                : BattleAbilityUser.ForStatusEffect(),
            tickInterval,
            applyDamage);
    }

    internal bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        BattleAbilityUser user,
        float tickInterval)
    {
        return ApplyStatusEffect(
            definition,
            duration,
            stacks,
            user,
            tickInterval,
            null);
    }

    internal bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        BattleAbilityUser user,
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
                user,
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
        BattleAbilityUser user,
        float tickInterval,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        if (definition == null || !definition.CanTargetEnemy || stacks <= 0 ||
            string.IsNullOrWhiteSpace(definition.StatusId))
        {
            return false;
        }

        float requestedDuration = ResolveStatusDuration(definition, duration);
        EnemyStatusApplicationPolicy applicationPolicy =
            ResolveStatusApplicationPolicy(definition, requestedDuration);
        if (!applicationPolicy.CanApply || applicationPolicy.Duration <= 0f)
            return false;
        float remainingDuration = applicationPolicy.Duration;

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
            user);
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
            mutation.User,
            applyDamage);
        if (continueExecution && mutation.StackChanged)
        {
            ExecuteStatusDamageOperations(
                definition,
                StatusEffectOperationTrigger.OnStackChanged,
                mutation.CurrentStacks,
                1,
                mutation.User,
                applyDamage);
        }

        CombatRuntimeService?.PublishEnemyCombatEvent(
            new EnemyCombatEvent(
                EnemyCombatEventType.StatusApplied,
                this));

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
        if (removalCount < 0)
            return 0;

        return RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            null);
    }

    internal int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        if (removalCount < 0)
            return 0;

        return RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            applyDamage);
    }

    internal int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        CharacterStatusRemovalAmount removalAmount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        return RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            removalAmount,
            applyDamage);
    }

    internal int RemoveStatusEffects(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        BeginStatusMutation();
        try
        {
            return RemoveStatusEffectsCore(
                removalSelection,
                removalAmount,
                applyDamage);
        }
        finally
        {
            EndStatusMutation();
        }
    }

    internal int ClearStatusEffectsOnDefeat()
    {
        IReadOnlyList<BattleStatusSnapshot> removedStatuses =
            GetActiveStatusEffects();
        if (removedStatuses.Count == 0)
            return 0;

        _statusEffects.Clear();
        foreach (BattleStatusSnapshot removedStatus in removedStatuses)
        {
            BattleStatusChangedEvent eventData = new(
                BattleStatusTarget.FromEnemy(this),
                BattleStatusChangeType.Removed,
                removedStatus,
                new BattleStatusSnapshot(
                    removedStatus.Definition,
                    0,
                    0f));
            if (!eventData.IsValid)
                continue;

            StatusChanged?.Invoke(eventData);
            foreach (StatusEffectLifecycleEvent lifecycleEvent in
                     StatusEffectLifecycleResolver.Resolve(eventData))
            {
                // Defeat clears presentation state immediately, but must not
                // execute gameplay OnRemove effects for an already dead unit.
                StatusLifecycle?.Invoke(lifecycleEvent);
            }
        }

        return removedStatuses.Count;
    }

    private int RemoveStatusEffectsCore(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        return RemoveMatchingStatusEffects(
            removalSelection,
            removalAmount,
            applyDamage);
    }

    private int RemoveStatusEffect(
        StatusEffectSO statusEffect,
        CharacterStatusRemovalAmount removalAmount,
        Func<int, IBattleCharacter, bool> applyDamage,
        out bool continueExecution)
    {
        continueExecution = true;
        if (statusEffect == null || !statusEffect.Removable)
            return 0;

        int removalCount = removalAmount.Resolve(
            GetStatusStackCount(statusEffect));
        return removalCount > 0
            ? RemoveStatusStacks(
                statusEffect.StatusId,
                removalCount,
                applyDamage,
                out continueExecution)
            : 0;
    }

    private int RemoveMatchingStatusEffects(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        List<StatusEffectSO> candidates =
            CollectStatusRemovalCandidates(removalSelection);
        int selectedCount = CharacterStatusRemovalPick.SelectInPlace(
            candidates,
            removalSelection);
        int removed = 0;
        for (int index = 0; index < selectedCount; index++)
        {
            removed = BattleValueMath.SaturatingAddNonNegative(
                removed,
                RemoveStatusEffect(
                    candidates[index],
                    removalAmount,
                    applyDamage,
                    out bool continueExecution));
            if (!continueExecution)
                break;
        }

        return removed;
    }

    private List<StatusEffectSO> CollectStatusRemovalCandidates(
        CharacterStatusRemovalSelection removalSelection)
    {
        List<StatusEffectSO> candidates = new();
        HashSet<string> visitedIds = new(StringComparer.Ordinal);
        if (removalSelection.Target ==
            CharacterStatusRemovalTarget.Single)
        {
            for (int index = 0;
                 index < removalSelection.ExplicitStatusCount;
                 index++)
            {
                AddStatusRemovalCandidate(
                    candidates,
                    visitedIds,
                    removalSelection.GetExplicitStatus(index),
                    removalSelection);
            }
        }
        else
        {
            foreach (StatusEffectRuntimeState state in
                     _statusEffects.Values)
            {
                AddStatusRemovalCandidate(
                    candidates,
                    visitedIds,
                    state?.Definition,
                    removalSelection);
            }
        }

        candidates.Sort((left, right) => string.Compare(
            left?.StatusId,
            right?.StatusId,
            StringComparison.Ordinal));
        return candidates;
    }

    private void AddStatusRemovalCandidate(
        List<StatusEffectSO> candidates,
        HashSet<string> visitedIds,
        StatusEffectSO definition,
        CharacterStatusRemovalSelection removalSelection)
    {
        if (definition == null || !definition.Removable ||
            string.IsNullOrWhiteSpace(definition.StatusId) ||
            !visitedIds.Add(definition.StatusId) ||
            !_statusEffects.ContainsKey(definition.StatusId) ||
            !removalSelection.MatchesStatus(definition))
        {
            return;
        }

        candidates.Add(definition);
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
            mutation.User,
            applyDamage);
        if (continueExecution && mutation.CurrentStacks == 0)
        {
            continueExecution = ExecuteStatusDamageOperations(
                state.Definition,
                StatusEffectOperationTrigger.OnRemove,
                mutation.CurrentStacks,
                1,
                mutation.User,
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
                            activeBatch.User),
                        tickCount);
                StatusLifecycle?.Invoke(tick);
                BattleEffectResult triggerResult =
                    StatusEffectTriggerExecutor.Execute(
                        tick,
                        _boundBattleBoard,
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
                        activeBatch.User,
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
            mutation.User,
            applyDamage);
        if (!continueExecution || mutation.CurrentStacks > 0)
            return continueExecution;

        return ExecuteStatusDamageOperations(
            definition,
            StatusEffectOperationTrigger.OnExpire,
            mutation.CurrentStacks,
            1,
            mutation.User,
            applyDamage);
    }

    private bool ExecuteStatusDamageOperations(
        StatusEffectSO definition,
        StatusEffectOperationTrigger trigger,
        int eventStacks,
        int occurrenceCount,
        BattleAbilityUser user,
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
            if (!applyDamage(damage, user.Unit.Ally))
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
                state.ActiveBatch != null
                    ? state.ActiveBatch.User
                    : default)
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
                    StatusEffectTriggerExecutor.Execute(
                        lifecycleEvent,
                        _boundBattleBoard);
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
        if (definition.DurationMode == StatusEffectDurationMode.Permanent ||
            float.IsPositiveInfinity(duration))
        {
            return float.PositiveInfinity;
        }

        return TimePrecision.Normalize(
            duration > 0f ? duration : definition.DefaultDuration,
            0.1f);
    }

    private EnemyStatusApplicationPolicy ResolveStatusApplicationPolicy(
        StatusEffectSO definition,
        float duration)
    {
        bool permanent = float.IsPositiveInfinity(duration);
        float resolvedDuration = duration;
        foreach (EnemyCombatModifierRuntimeState state in _combatModifiers)
        {
            if (state == null || !state.IsActive ||
                !state.MatchesStatus(definition))
            {
                continue;
            }

            if (state.Definition.Type ==
                EnemyCombatModifierType.StatusImmunity)
            {
                return EnemyStatusApplicationPolicy.Immune(duration);
            }
            if (!permanent && state.Definition.Type ==
                EnemyCombatModifierType.StatusDuration)
            {
                resolvedDuration = state.Evaluate(resolvedDuration);
            }
        }

        EnemyStatusApplicationPolicy local =
            resolvedDuration > 0f || permanent
                ? EnemyStatusApplicationPolicy.Allowed(resolvedDuration)
                : new EnemyStatusApplicationPolicy(false, 0f);
        if (!local.CanApply)
            return local;

        IEnemyCombatRuntimeService service = CombatRuntimeService;
        return service != null
            ? service.ResolveStatusApplication(
                this,
                definition,
                local.Duration)
            : local;
    }

    private float GetStatusModifiedStat(
        float baseValue,
        StatusEffectStatType statType,
        StatusEffectOperationType operationType)
    {
        StatusEffectStatAccumulator accumulator = default;
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }

            int stacks = Mathf.Max(1, state.StackCount);
            IReadOnlyList<StatusEffectStatModifierDefinition> modifiers =
                state.Definition.StatModifiers;
            if (modifiers != null)
            {
                foreach (StatusEffectStatModifierDefinition modifier in
                         modifiers)
                {
                    if (modifier != null && modifier.StatType == statType)
                        accumulator.Add(modifier, stacks);
                }
            }

            IReadOnlyList<StatusEffectOperationDefinition> operations =
                state.Definition.Operations;
            if (operations == null)
                continue;
            foreach (StatusEffectOperationDefinition operation in operations)
            {
                if (operation == null ||
                    operation.Trigger != StatusEffectOperationTrigger.OnApply ||
                    operation.OperationType != operationType ||
                    float.IsNaN(operation.Value) ||
                    float.IsInfinity(operation.Value))
                {
                    continue;
                }

                float value = operation.Value *
                    (operation.ScaleWithStacks ? stacks : 1);
                if (operation.ValueMode == StatusEffectValueMode.Fixed)
                    accumulator.AddFlat(value);
                else
                    accumulator.AddAdditiveRatio(value);
            }
        }

        return accumulator.Evaluate(baseValue);
    }

    private float EvaluateCombatModifiers(
        EnemyCombatModifierType type,
        float baseValue)
    {
        float result = baseValue;
        foreach (EnemyCombatModifierRuntimeState state in _combatModifiers)
        {
            if (state != null && state.IsActive &&
                state.Definition.Type == type)
            {
                result = state.Evaluate(result);
            }
        }

        return result;
    }

    private static float EvaluateModifier(
        EnemyCombatModifier modifier,
        float baseValue,
        int stacks)
    {
        stacks = Mathf.Max(1, stacks);
        float value = (baseValue + modifier.Amount * stacks) *
                      Mathf.Max(0f, 1f + modifier.Percentage * stacks) *
                      Mathf.Pow(modifier.Multiplier, stacks);
        return NormalizeNonNegative(value);
    }

    private void SortCombatModifiers()
    {
        _combatModifiers.Sort((left, right) =>
        {
            int typeOrder = left.Definition.Type.CompareTo(
                right.Definition.Type);
            return typeOrder != 0
                ? typeOrder
                : string.Compare(
                    left.Definition.SourceId,
                    right.Definition.SourceId,
                    StringComparison.Ordinal);
        });
    }

    internal bool HasStatusEffectId(string statusId)
    {
        return TryGetStatusState(statusId, out _);
    }

    internal string ResolveModifierSourceId(
        EnemyAbilityDefinition ability,
        EnemyAbilityOperationDefinition operation)
    {
        if (!string.IsNullOrWhiteSpace(operation?.SourceId))
            return operation.SourceId.Trim();

        string enemyId = Definition.EnemyId;
        string abilityId = ability?.AbilityId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(enemyId))
            enemyId = "enemy";
        if (string.IsNullOrWhiteSpace(abilityId))
            abilityId = "runtime";
        return $"{enemyId}:{abilityId}";
    }

    private static float NormalizeFinite(float value)
    {
        if (float.IsNaN(value))
            return 0f;
        return float.IsPositiveInfinity(value)
            ? float.MaxValue
            : Mathf.Max(0f, value);
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) &&
               value > 0f;
    }

    private static float NormalizeNonNegative(float value)
    {
        return NormalizeFinite(value);
    }

    private static int SaturatingRoundToInt(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
            return 0;
        if (float.IsInfinity(value) || value >= int.MaxValue)
            return int.MaxValue;
        return Mathf.Max(0, Mathf.RoundToInt(value));
    }

    internal bool TickAbilityCooldown(float deltaTime)
    {
        foreach (EnemyAbilityRuntimeState state in _abilityStates)
        {
            if (IsAbilityEnabledInCurrentPhase(state.Definition) &&
                state.Definition.RespondsToTrigger(
                    EnemyAbilityTrigger.OnCooldown))
            {
                return state.TickCooldown(
                    deltaTime,
                    AreAllActionsDisabled,
                    MaxHealth > 0 ? Health * 100f / MaxHealth : 0f);
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
