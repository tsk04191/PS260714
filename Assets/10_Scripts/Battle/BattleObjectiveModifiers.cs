using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleObjectiveModifierType
{
    HealingReceivedMultiplier = 0,
    IncomingDamageMultiplier = 1,
    MaximumHealthReduction = 2,
}

public interface IBattleObjectiveModifierService
{
    int BaseMaximumHealth { get; }
    float HealingReceivedMultiplier { get; }
    float IncomingDamageMultiplier { get; }
    float MaximumHealthReductionRatio { get; }
    int ActiveModifierCount { get; }
    int ActiveDamageOverTimeCount { get; }

    int TakeDamage(int amount, float protectionBypassRatio);
    bool TryAddTimedModifier(
        string sourceId,
        BattleObjectiveModifierType type,
        float value,
        float duration,
        int maximumStacks = 1);
    bool TryApplyDamageOverTime(
        string sourceId,
        int damagePerTick,
        float tickInterval,
        float duration,
        int maximumStacks = 1);
    int GetModifierStackCount(
        string sourceId,
        BattleObjectiveModifierType type);
    int GetDamageOverTimeStackCount(string sourceId);
    void ClearTransientModifiers();
}

public interface IBattleObjectiveModifierServiceProvider
{
    IBattleObjectiveModifierService ObjectiveModifierService { get; }
}

public sealed partial class BattleCoreRuntime
{
    private sealed class TimedObjectiveModifier
    {
        public string SourceId { get; }
        public BattleObjectiveModifierType Type { get; }
        public float Value { get; }
        public float RemainingDuration { get; set; }

        public TimedObjectiveModifier(
            string sourceId,
            BattleObjectiveModifierType type,
            float value,
            float duration)
        {
            SourceId = sourceId;
            Type = type;
            Value = value;
            RemainingDuration = duration;
        }
    }

    private sealed class ObjectiveDamageOverTime
    {
        public string SourceId { get; }
        public int DamagePerTick { get; }
        public float TickInterval { get; }
        public float RemainingDuration { get; set; }
        public float TimeUntilTick { get; set; }

        public ObjectiveDamageOverTime(
            string sourceId,
            int damagePerTick,
            float tickInterval,
            float duration)
        {
            SourceId = sourceId;
            DamagePerTick = damagePerTick;
            TickInterval = tickInterval;
            RemainingDuration = duration;
            TimeUntilTick = tickInterval;
        }
    }

    private readonly List<TimedObjectiveModifier> objectiveModifiers = new();
    private readonly List<ObjectiveDamageOverTime> objectiveDamageOverTime =
        new();
    private int baseMaximumHealth;

    public int BaseMaximumHealth => baseMaximumHealth;
    public int ActiveModifierCount => objectiveModifiers.Count;
    public int ActiveDamageOverTimeCount => objectiveDamageOverTime.Count;

    public float HealingReceivedMultiplier => ResolveScalarMultiplier(
        BattleObjectiveModifierType.HealingReceivedMultiplier);

    public float IncomingDamageMultiplier => ResolveScalarMultiplier(
        BattleObjectiveModifierType.IncomingDamageMultiplier);

    public float MaximumHealthReductionRatio
    {
        get
        {
            float reduction = 0f;
            for (int index = 0; index < objectiveModifiers.Count; index++)
            {
                TimedObjectiveModifier modifier = objectiveModifiers[index];
                if (modifier?.Type ==
                    BattleObjectiveModifierType.MaximumHealthReduction)
                {
                    reduction += Mathf.Clamp01(modifier.Value);
                }
            }
            return Mathf.Clamp(reduction, 0f, 0.95f);
        }
    }

    public bool TryAddTimedModifier(
        string sourceId,
        BattleObjectiveModifierType type,
        float value,
        float duration,
        int maximumStacks = 1)
    {
        sourceId = sourceId?.Trim();
        duration = TimePrecision.Normalize(duration);
        if (!IsActive || IsDestroyed || string.IsNullOrEmpty(sourceId) ||
            !Enum.IsDefined(typeof(BattleObjectiveModifierType), type) ||
            float.IsNaN(value) || float.IsInfinity(value) ||
            float.IsNaN(duration) || float.IsInfinity(duration) ||
            duration < 0f || maximumStacks <= 0 ||
            !IsValidModifierValue(type, value))
        {
            return false;
        }

        float resolvedDuration = duration > 0f
            ? duration
            : float.PositiveInfinity;

        int stackCount = 0;
        TimedObjectiveModifier shortest = null;
        for (int index = 0; index < objectiveModifiers.Count; index++)
        {
            TimedObjectiveModifier modifier = objectiveModifiers[index];
            if (modifier == null || modifier.Type != type ||
                !string.Equals(
                    modifier.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            stackCount++;
            if (shortest == null ||
                modifier.RemainingDuration < shortest.RemainingDuration)
            {
                shortest = modifier;
            }
        }

        if (stackCount >= maximumStacks)
        {
            if (shortest == null ||
                shortest.RemainingDuration >= resolvedDuration)
            {
                return false;
            }
            shortest.RemainingDuration = resolvedDuration;
        }
        else
        {
            objectiveModifiers.Add(new TimedObjectiveModifier(
                sourceId,
                type,
                value,
                resolvedDuration));
        }

        if (type == BattleObjectiveModifierType.MaximumHealthReduction)
            RecalculateMaximumHealth();
        return true;
    }

    public bool TryApplyDamageOverTime(
        string sourceId,
        int damagePerTick,
        float tickInterval,
        float duration,
        int maximumStacks = 1)
    {
        sourceId = sourceId?.Trim();
        tickInterval = TimePrecision.Normalize(
            tickInterval,
            TimePrecision.Step);
        duration = TimePrecision.Normalize(duration, TimePrecision.Step);
        if (!IsActive || IsDestroyed || string.IsNullOrEmpty(sourceId) ||
            damagePerTick <= 0 || tickInterval <= 0f ||
            duration < tickInterval || maximumStacks <= 0)
        {
            return false;
        }

        int stackCount = 0;
        ObjectiveDamageOverTime shortest = null;
        for (int index = 0; index < objectiveDamageOverTime.Count; index++)
        {
            ObjectiveDamageOverTime effect = objectiveDamageOverTime[index];
            if (effect == null || !string.Equals(
                    effect.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            stackCount++;
            if (shortest == null ||
                effect.RemainingDuration < shortest.RemainingDuration)
            {
                shortest = effect;
            }
        }

        if (stackCount >= maximumStacks)
        {
            if (shortest == null || shortest.RemainingDuration >= duration)
                return false;
            shortest.RemainingDuration = duration;
            return true;
        }

        objectiveDamageOverTime.Add(new ObjectiveDamageOverTime(
            sourceId,
            damagePerTick,
            tickInterval,
            duration));
        return true;
    }

    public int GetModifierStackCount(
        string sourceId,
        BattleObjectiveModifierType type)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return 0;

        int count = 0;
        for (int index = 0; index < objectiveModifiers.Count; index++)
        {
            TimedObjectiveModifier modifier = objectiveModifiers[index];
            if (modifier?.Type == type && string.Equals(
                    modifier.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    public int GetDamageOverTimeStackCount(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return 0;

        int count = 0;
        for (int index = 0; index < objectiveDamageOverTime.Count; index++)
        {
            if (string.Equals(
                    objectiveDamageOverTime[index]?.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    public void ClearTransientModifiers()
    {
        bool maximumChanged = MaximumHealth != baseMaximumHealth &&
                              baseMaximumHealth > 0;
        objectiveModifiers.Clear();
        objectiveDamageOverTime.Clear();
        if (baseMaximumHealth > 0)
        {
            MaximumHealth = baseMaximumHealth;
            CurrentHealth = Mathf.Clamp(
                CurrentHealth,
                0,
                MaximumHealth);
        }
        if (maximumChanged)
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
    }

    private int ResolveModifiedDamage(
        int amount,
        float protectionBypassRatio)
    {
        protectionBypassRatio = Mathf.Clamp01(protectionBypassRatio);
        if (IsDamageImmune)
            return 0;

        float multiplier = IncomingDamageMultiplier;
        if (multiplier < 1f)
        {
            multiplier = Mathf.Lerp(
                multiplier,
                1f,
                protectionBypassRatio);
        }
        return Mathf.Max(0, Mathf.RoundToInt(amount * multiplier));
    }

    private int ResolveModifiedHealing(int amount)
    {
        return Mathf.Max(
            0,
            Mathf.RoundToInt(amount * HealingReceivedMultiplier));
    }

    private void TickObjectiveModifiers(float deltaTime)
    {
        bool recalculateMaximum = false;
        for (int index = objectiveModifiers.Count - 1; index >= 0; index--)
        {
            TimedObjectiveModifier modifier = objectiveModifiers[index];
            if (modifier == null)
            {
                objectiveModifiers.RemoveAt(index);
                recalculateMaximum = true;
                continue;
            }

            if (float.IsPositiveInfinity(modifier.RemainingDuration))
                continue;

            modifier.RemainingDuration = Mathf.Max(
                0f,
                modifier.RemainingDuration - deltaTime);
            if (modifier.RemainingDuration > 0f)
                continue;

            recalculateMaximum |= modifier.Type ==
                BattleObjectiveModifierType.MaximumHealthReduction;
            objectiveModifiers.RemoveAt(index);
        }
        if (recalculateMaximum)
            RecalculateMaximumHealth();

        for (int index = objectiveDamageOverTime.Count - 1;
             index >= 0;
             index--)
        {
            ObjectiveDamageOverTime effect = objectiveDamageOverTime[index];
            if (effect == null)
            {
                objectiveDamageOverTime.RemoveAt(index);
                continue;
            }

            float activeDelta = Mathf.Min(
                deltaTime,
                effect.RemainingDuration);
            effect.RemainingDuration = Mathf.Max(
                0f,
                effect.RemainingDuration - deltaTime);
            effect.TimeUntilTick -= activeDelta;
            while (effect.TimeUntilTick <= 0f && !IsDestroyed)
            {
                TakeDamage(effect.DamagePerTick);
                effect.TimeUntilTick += effect.TickInterval;
            }

            if (effect.RemainingDuration <= 0f)
                objectiveDamageOverTime.RemoveAt(index);
        }
    }

    private float ResolveScalarMultiplier(BattleObjectiveModifierType type)
    {
        float multiplier = 1f;
        for (int index = 0; index < objectiveModifiers.Count; index++)
        {
            TimedObjectiveModifier modifier = objectiveModifiers[index];
            if (modifier?.Type == type)
                multiplier *= Mathf.Max(0f, modifier.Value);
        }
        return Mathf.Clamp(multiplier, 0f, 10f);
    }

    private void RecalculateMaximumHealth()
    {
        int previousMaximum = MaximumHealth;
        MaximumHealth = IsActive
            ? Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseMaximumHealth *
                    (1f - MaximumHealthReductionRatio)))
            : 0;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaximumHealth);
        if (MaximumHealth != previousMaximum)
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
    }

    private static bool IsValidModifierValue(
        BattleObjectiveModifierType type,
        float value)
    {
        return type switch
        {
            BattleObjectiveModifierType.HealingReceivedMultiplier =>
                value >= 0f && value <= 10f,
            BattleObjectiveModifierType.IncomingDamageMultiplier =>
                value >= 0f && value <= 10f,
            BattleObjectiveModifierType.MaximumHealthReduction =>
                value > 0f && value < 1f,
            _ => false,
        };
    }
}
