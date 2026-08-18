using System;
using UnityEngine;

/// <summary>
/// Runtime events emitted by enemies independently of presentation. These
/// events are intentionally broader than authored ability triggers so combat
/// UI, telemetry, and future encounter logic can observe the same facts.
/// </summary>
public enum EnemyCombatEventType
{
    CoreRangeEntered = 0,
    CoreRangeExited = 1,
    FirstCoreContact = 2,
    CoreContact = 3,
    CoreAttackPreparing = 4,
    CoreAttackResolved = 5,
    CoreDamageApplied = 6,
    DamageTaken = 7,
    HealthThresholdCrossed = 8,
    NoDamageDurationReached = 9,
    NearbyEnemyDefeated = 10,
    ChargeStarted = 11,
    TelegraphStarted = 12,
    ChargeInterrupted = 13,
    ChargeCompleted = 14,
    StatusApplied = 15,
    PhaseChanged = 16,
}

public enum EnemyCombatModifierType
{
    CoreAttackDamage = 0,
    CoreAttackInterval = 1,
    StatusDuration = 2,
    StatusImmunity = 3,
    IncomingDamage = 4,
}

public enum EnemyStatusModifierScope
{
    All = 0,
    Debuffs = 1,
    Controls = 2,
}

public enum EnemyChargeInterruptReason
{
    None = 0,
    Stun = 1,
    ForcedMovement = 2,
    DirectDamage = 3,
    OtherControl = 4,
}

/// <summary>
/// A normalized, owner-neutral modifier. Amount is flat, Percentage is an
/// additive ratio, and Multiplier is a multiplicative factor. A zero duration
/// is permanent until its source id is removed.
/// </summary>
public readonly struct EnemyCombatModifier
{
    public string SourceId { get; }
    public EnemyCombatModifierType Type { get; }
    public float Amount { get; }
    public float Percentage { get; }
    public float Multiplier { get; }
    public float Duration { get; }
    public int MaximumStacks { get; }
    public EnemyStatusModifierScope StatusScope { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(SourceId) &&
        Enum.IsDefined(typeof(EnemyCombatModifierType), Type) &&
        IsFinite(Amount) && IsFinite(Percentage) &&
        IsFinite(Multiplier) && Multiplier >= 0f &&
        IsFinite(Duration) && Duration >= 0f;

    public EnemyCombatModifier(
        string sourceId,
        EnemyCombatModifierType type,
        float amount = 0f,
        float percentage = 0f,
        float multiplier = 1f,
        float duration = 0f,
        int maximumStacks = 1,
        EnemyStatusModifierScope statusScope =
            EnemyStatusModifierScope.All)
    {
        SourceId = (sourceId ?? string.Empty).Trim();
        Type = type;
        Amount = IsFinite(amount) ? amount : 0f;
        Percentage = IsFinite(percentage) ? percentage : 0f;
        Multiplier = IsFinite(multiplier)
            ? Mathf.Max(0f, multiplier)
            : 1f;
        Duration = IsFinite(duration) ? Mathf.Max(0f, duration) : 0f;
        MaximumStacks = Mathf.Max(1, maximumStacks);
        StatusScope = Enum.IsDefined(
            typeof(EnemyStatusModifierScope),
            statusScope)
                ? statusScope
                : EnemyStatusModifierScope.All;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public readonly struct EnemyStatusApplicationPolicy
{
    public bool CanApply { get; }
    public float Duration { get; }
    public bool WasImmune { get; }

    public EnemyStatusApplicationPolicy(
        bool canApply,
        float duration,
        bool wasImmune = false)
    {
        CanApply = canApply;
        Duration = float.IsPositiveInfinity(duration)
            ? float.PositiveInfinity
            : Mathf.Max(0f, duration);
        WasImmune = wasImmune;
    }

    public static EnemyStatusApplicationPolicy Allowed(float duration)
    {
        return new EnemyStatusApplicationPolicy(true, duration);
    }

    public static EnemyStatusApplicationPolicy Immune(float duration)
    {
        return new EnemyStatusApplicationPolicy(false, duration, true);
    }
}

public readonly struct EnemyChargeSnapshot
{
    public EnemyRuntime Source { get; }
    public EnemyAbilityDefinition Ability { get; }
    public string SourceId { get; }
    public float Duration { get; }
    public float Remaining { get; }
    public bool IsCoreAttackCharge { get; }
    public string TelegraphCueId { get; }
    public float TelegraphRadius { get; }

    public bool IsValid => Source != null &&
                           (!string.IsNullOrWhiteSpace(SourceId) ||
                            Ability != null);
    public float Progress => Duration > 0f
        ? Mathf.Clamp01(1f - Remaining / Duration)
        : 1f;

    internal EnemyChargeSnapshot(
        EnemyRuntime source,
        EnemyAbilityDefinition ability,
        string sourceId,
        float duration,
        float remaining,
        bool isCoreAttackCharge,
        string telegraphCueId,
        float telegraphRadius)
    {
        Source = source;
        Ability = ability;
        SourceId = sourceId ?? string.Empty;
        Duration = Mathf.Max(0f, duration);
        Remaining = Mathf.Max(0f, remaining);
        IsCoreAttackCharge = isCoreAttackCharge;
        TelegraphCueId = telegraphCueId ?? string.Empty;
        TelegraphRadius = Mathf.Max(0f, telegraphRadius);
    }
}

public readonly struct EnemyCombatEvent
{
    public EnemyCombatEventType Type { get; }
    public EnemyRuntime Source { get; }
    public EnemyRuntime RelatedEnemy { get; }
    public IBattleCharacter RelatedCharacter { get; }
    public EnemyAbilityDefinition Ability { get; }
    public int RequestedDamage { get; }
    public int AppliedDamage { get; }
    public int PreviousHealth { get; }
    public int CurrentHealth { get; }
    public string DamageSourceId { get; }
    public float ThresholdPercent { get; }
    public float ElapsedTime { get; }
    public Vector2 WorldPosition { get; }
    public EnemyChargeSnapshot Charge { get; }

    public bool IsValid => Source != null &&
                           Enum.IsDefined(
                               typeof(EnemyCombatEventType),
                               Type);

    public EnemyCombatEvent(
        EnemyCombatEventType type,
        EnemyRuntime source,
        EnemyRuntime relatedEnemy = null,
        IBattleCharacter relatedCharacter = null,
        EnemyAbilityDefinition ability = null,
        int requestedDamage = 0,
        int appliedDamage = 0,
        int previousHealth = 0,
        int currentHealth = 0,
        string damageSourceId = null,
        float thresholdPercent = 0f,
        float elapsedTime = 0f,
        Vector2 worldPosition = default,
        EnemyChargeSnapshot charge = default)
    {
        Type = type;
        Source = source;
        RelatedEnemy = relatedEnemy;
        RelatedCharacter = relatedCharacter;
        Ability = ability;
        RequestedDamage = Mathf.Max(0, requestedDamage);
        AppliedDamage = Mathf.Max(0, appliedDamage);
        PreviousHealth = Mathf.Max(0, previousHealth);
        CurrentHealth = Mathf.Max(0, currentHealth);
        DamageSourceId = (damageSourceId ?? string.Empty).Trim();
        ThresholdPercent = Mathf.Clamp(thresholdPercent, 0f, 100f);
        ElapsedTime = Mathf.Max(0f, elapsedTime);
        WorldPosition = worldPosition;
        Charge = charge;
    }
}

public interface IEnemyCombatRuntimeServiceProvider
{
    IEnemyCombatRuntimeService EnemyCombatRuntimeService { get; }
}

/// <summary>
/// Board-owned service used by EnemyRuntime without coupling the runtime to a
/// specific scene implementation. Passive aura resolution stays on the board
/// because it owns live units and their world positions.
/// </summary>
public interface IEnemyCombatRuntimeService
{
    event Action<EnemyCombatEvent> EnemyCombatEventRaised;

    float ResolvePassiveModifier(
        EnemyRuntime target,
        EnemyCombatModifierType modifierType,
        float baseValue);

    EnemyStatusApplicationPolicy ResolveStatusApplication(
        EnemyRuntime target,
        StatusEffectSO statusEffect,
        float duration);

    float ResolvePlayerActionPeriodMultiplier(
        IBattleCharacter target);

    float ResolveResourceRecoveryMultiplier();

    void PublishEnemyCombatEvent(EnemyCombatEvent eventData);
}

internal sealed class EnemyCombatModifierRuntimeState
{
    private float _remaining;

    public EnemyCombatModifier Definition { get; }
    public int Stacks { get; private set; }
    public float Remaining => float.IsPositiveInfinity(_remaining)
        ? float.PositiveInfinity
        : TimePrecision.FloorToTenth(_remaining);
    public bool IsActive => Stacks > 0 &&
                            (float.IsPositiveInfinity(_remaining) ||
                             _remaining > 0f);

    public EnemyCombatModifierRuntimeState(EnemyCombatModifier definition)
    {
        Definition = definition;
        Stacks = 1;
        RefreshDuration();
    }

    public bool Reapply()
    {
        int previous = Stacks;
        Stacks = Mathf.Min(Definition.MaximumStacks, Stacks + 1);
        float previousRemaining = _remaining;
        RefreshDuration();
        return previous != Stacks ||
               !Mathf.Approximately(previousRemaining, _remaining);
    }

    public bool Tick(float deltaTime)
    {
        if (!IsActive || float.IsPositiveInfinity(_remaining) ||
            deltaTime <= 0f)
        {
            return false;
        }

        _remaining = Mathf.Max(0f, _remaining - deltaTime);
        if (_remaining > 0f)
            return false;

        Stacks = 0;
        return true;
    }

    public float Evaluate(float baseValue)
    {
        if (!IsActive)
            return baseValue;

        float result =
            (baseValue + Definition.Amount * Stacks) *
            Mathf.Max(0f, 1f + Definition.Percentage * Stacks) *
            Mathf.Pow(Definition.Multiplier, Stacks);
        return float.IsNaN(result) ? 0f : result;
    }

    public int RemoveStacks(int count)
    {
        count = Mathf.Max(0, count);
        int removed = Mathf.Min(Stacks, count);
        Stacks -= removed;
        if (Stacks <= 0)
            _remaining = 0f;
        return removed;
    }

    public bool MatchesStatus(StatusEffectSO statusEffect)
    {
        if (statusEffect == null)
            return false;

        return Definition.StatusScope switch
        {
            EnemyStatusModifierScope.All => true,
            EnemyStatusModifierScope.Debuffs =>
                statusEffect.Alignment == StatusEffectAlignment.Debuff,
            EnemyStatusModifierScope.Controls =>
                EnemyStatusRules.HasControlEffect(statusEffect),
            _ => false,
        };
    }

    private void RefreshDuration()
    {
        _remaining = Definition.Duration > 0f
            ? Definition.Duration
            : float.PositiveInfinity;
    }
}

internal static class EnemyStatusRules
{
    public static bool HasControlEffect(StatusEffectSO statusEffect)
    {
        if (statusEffect == null)
            return false;
        if (statusEffect.HasControlEffects)
            return true;

        foreach (StatusEffectOperationDefinition operation in
                 statusEffect.Operations)
        {
            if (operation != null &&
                operation.OperationType ==
                    StatusEffectOperationType.DisableAction)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class EnemyActiveChargeRuntimeState
{
    private readonly EnemyAbilityTelegraphDefinition _telegraph;

    public EnemyAbilityRuntimeState AbilityState { get; }
    public EnemyCombatModifier CoreAttackModifier { get; }
    public string SourceId { get; }
    public float Duration { get; }
    public float Remaining { get; private set; }
    public bool IsCoreAttackCharge { get; }
    public bool IsInterruptible { get; }
    public EnemyChargeInterruptFlags Interrupts { get; }
    public bool TelegraphStarted { get; private set; }
    public bool IsComplete => Remaining <= 0f;

    public EnemyActiveChargeRuntimeState(
        EnemyAbilityRuntimeState abilityState,
        string sourceId,
        float duration,
        bool isCoreAttackCharge,
        bool isInterruptible,
        EnemyChargeInterruptFlags interrupts,
        EnemyAbilityTelegraphDefinition telegraph,
        EnemyCombatModifier coreAttackModifier = default)
    {
        AbilityState = abilityState;
        CoreAttackModifier = coreAttackModifier;
        SourceId = (sourceId ?? string.Empty).Trim();
        Duration = Mathf.Max(TimePrecision.Step, duration);
        Remaining = Duration;
        IsCoreAttackCharge = isCoreAttackCharge;
        IsInterruptible = isInterruptible;
        Interrupts = isInterruptible
            ? interrupts
            : EnemyChargeInterruptFlags.None;
        _telegraph = telegraph;
        TelegraphStarted = !ShouldTelegraph;
    }

    public bool ShouldTelegraph =>
        _telegraph?.IsEnabled == true;

    public bool Tick(float deltaTime, out bool telegraphStarted)
    {
        telegraphStarted = false;
        if (deltaTime <= 0f || IsComplete)
            return IsComplete;

        Remaining = Mathf.Max(0f, Remaining - deltaTime);
        if (!TelegraphStarted && ShouldTelegraph &&
            Remaining <= _telegraph.LeadTime)
        {
            TelegraphStarted = true;
            telegraphStarted = true;
        }

        return IsComplete;
    }

    public bool CanInterrupt(EnemyChargeInterruptReason reason)
    {
        if (!IsInterruptible || reason == EnemyChargeInterruptReason.None)
            return false;

        EnemyChargeInterruptFlags required = reason switch
        {
            EnemyChargeInterruptReason.Stun =>
                EnemyChargeInterruptFlags.Stun |
                EnemyChargeInterruptFlags.AnyControl,
            EnemyChargeInterruptReason.ForcedMovement =>
                EnemyChargeInterruptFlags.ForcedMovement,
            EnemyChargeInterruptReason.DirectDamage =>
                EnemyChargeInterruptFlags.DirectDamage,
            EnemyChargeInterruptReason.OtherControl =>
                EnemyChargeInterruptFlags.AnyControl,
            _ => EnemyChargeInterruptFlags.None,
        };
        return (Interrupts & required) != 0;
    }

    public EnemyChargeSnapshot CreateSnapshot(EnemyRuntime source)
    {
        return new EnemyChargeSnapshot(
            source,
            AbilityState?.Definition,
            SourceId,
            Duration,
            Remaining,
            IsCoreAttackCharge,
            _telegraph?.CueId,
            _telegraph?.WorldRadius ?? 0f);
    }
}
