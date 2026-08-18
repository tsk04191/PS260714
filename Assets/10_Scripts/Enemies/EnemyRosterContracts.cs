using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyRosterTier
{
    General = 0,
    Special = 1,
    Elite = 2,
    Boss = 3
}

public enum EnemyCoreAttackDamagePolicy
{
    LegacyInteger = 0,
    AccumulateFraction = 1
}

public enum EnemyWorldLayerScope
{
    All = 0,
    Same = 1,
    SameOrAdjacent = 2
}

[Flags]
public enum EnemyChargeInterruptFlags
{
    None = 0,
    Stun = 1 << 0,
    ForcedMovement = 1 << 1,
    DirectDamage = 1 << 2,
    AnyControl = 1 << 3
}

public enum EnemyAbilityParameterValueType
{
    Float = 0,
    Integer = 1,
    Boolean = 2,
    Text = 3,
    EnemyReference = 4
}

[Serializable]
public sealed class EnemyReferenceDefinition
{
    [SerializeField]
    private EnemySO enemy;
    [SerializeField]
    private string enemyId;

    public EnemySO Enemy => enemy;
    public string EnemyId => enemyId ?? string.Empty;
    public string ResolvedEnemyId => enemy != null &&
                                     !string.IsNullOrWhiteSpace(enemy.EnemyId)
        ? enemy.EnemyId
        : EnemyId;
    public bool IsConfigured => enemy != null ||
                                !string.IsNullOrWhiteSpace(enemyId);

    public void Validate()
    {
        enemyId = (enemyId ?? string.Empty).Trim();
    }
}

[Serializable]
public sealed class EnemyAbilityParameterDefinition
{
    [SerializeField]
    private string key;
    [SerializeField]
    private EnemyAbilityParameterValueType valueType;
    [SerializeField]
    private float floatValue;
    [SerializeField]
    private int intValue;
    [SerializeField]
    private bool boolValue;
    [SerializeField]
    private string textValue;
    [SerializeField]
    private EnemyReferenceDefinition enemyReference = new();

    public string Key => key ?? string.Empty;
    public EnemyAbilityParameterValueType ValueType => valueType;
    public float FloatValue => floatValue;
    public int IntValue => intValue;
    public bool BoolValue => boolValue;
    public string TextValue => textValue ?? string.Empty;
    public EnemyReferenceDefinition EnemyReference =>
        enemyReference ??= new EnemyReferenceDefinition();

    public void Validate()
    {
        key = (key ?? string.Empty).Trim();
        textValue ??= string.Empty;
        if (!IsFinite(floatValue))
            floatValue = 0f;
        enemyReference ??= new EnemyReferenceDefinition();
        enemyReference.Validate();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class EnemyAbilityChargeDefinition
{
    [SerializeField]
    private bool enabled;
    [SerializeField, Min(0f)]
    private float duration;
    [SerializeField]
    private bool interruptible = true;
    [SerializeField]
    private EnemyChargeInterruptFlags interrupts =
        EnemyChargeInterruptFlags.Stun;

    public bool IsEnabled => enabled;
    public float Duration => Mathf.Max(0f, duration);
    public bool IsInterruptible => interruptible;
    public EnemyChargeInterruptFlags Interrupts => interrupts;
    internal float AuthoredDuration => duration;

    public void Validate()
    {
        duration = IsFinite(duration) ? Mathf.Max(0f, duration) : 0f;
        if (!interruptible)
            interrupts = EnemyChargeInterruptFlags.None;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class EnemyAbilityTelegraphDefinition
{
    [SerializeField]
    private bool enabled;
    [SerializeField, Min(0f)]
    private float leadTime;
    [SerializeField]
    private string cueId;
    [SerializeField, Min(0f)]
    private float worldRadius;

    public bool IsEnabled => enabled;
    public float LeadTime => Mathf.Max(0f, leadTime);
    public string CueId => cueId ?? string.Empty;
    public float WorldRadius => Mathf.Max(0f, worldRadius);
    internal float AuthoredLeadTime => leadTime;
    internal float AuthoredWorldRadius => worldRadius;

    public void Validate()
    {
        leadTime = IsFinite(leadTime) ? Mathf.Max(0f, leadTime) : 0f;
        cueId = (cueId ?? string.Empty).Trim();
        worldRadius = IsFinite(worldRadius)
            ? Mathf.Max(0f, worldRadius)
            : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class EnemyAbilityCooldownOverrideDefinition
{
    [SerializeField, Range(0f, 100f)]
    private float healthAtOrBelowPercent;
    [SerializeField, Min(0.1f)]
    private float cooldown = 1f;

    public float HealthAtOrBelowPercent =>
        Mathf.Clamp(healthAtOrBelowPercent, 0f, 100f);
    public float Cooldown => Mathf.Max(0f, cooldown);
    internal float AuthoredHealthAtOrBelowPercent =>
        healthAtOrBelowPercent;
    internal float AuthoredCooldown => cooldown;

    public void Validate()
    {
        healthAtOrBelowPercent = IsFinite(healthAtOrBelowPercent)
            ? Mathf.Clamp(healthAtOrBelowPercent, 0f, 100f)
            : 0f;
        cooldown = IsFinite(cooldown) ? Mathf.Max(0f, cooldown) : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class EnemySummonDefinition
{
    [SerializeField]
    private List<EnemyReferenceDefinition> candidates = new();
    [SerializeField, Min(1)]
    private int minimumCount = 1;
    [SerializeField, Min(1)]
    private int maximumCount = 1;
    [SerializeField, Min(0)]
    private int maximumActive;
    [SerializeField, Tooltip(
        "Allow this summon operation when its source was itself summoned. " +
        "Disable to limit summon chains to one generation.")]
    private bool allowRecursiveSummon;
    [SerializeField]
    private bool inheritFormationLayer = true;
    [SerializeField, Min(0.01f)]
    private float childHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)]
    private float childCoreAttackMultiplier = 1f;

    public IReadOnlyList<EnemyReferenceDefinition> Candidates =>
        candidates != null
            ? candidates
            : Array.Empty<EnemyReferenceDefinition>();
    public int MinimumCount => Mathf.Max(1, minimumCount);
    public int MaximumCount => Mathf.Max(MinimumCount, maximumCount);
    public int MaximumActive => Mathf.Max(0, maximumActive);
    public bool AllowRecursiveSummon => allowRecursiveSummon;
    public bool InheritFormationLayer => inheritFormationLayer;
    public float ChildHealthMultiplier =>
        Mathf.Max(0.01f, childHealthMultiplier);
    public float ChildCoreAttackMultiplier =>
        Mathf.Max(0.01f, childCoreAttackMultiplier);
    internal int AuthoredMinimumCount => minimumCount;
    internal int AuthoredMaximumCount => maximumCount;
    internal int AuthoredMaximumActive => maximumActive;
    internal float AuthoredChildHealthMultiplier =>
        childHealthMultiplier;
    internal float AuthoredChildCoreAttackMultiplier =>
        childCoreAttackMultiplier;

    public void Validate()
    {
        candidates ??= new List<EnemyReferenceDefinition>();
        foreach (EnemyReferenceDefinition candidate in candidates)
            candidate?.Validate();

        minimumCount = Mathf.Max(1, minimumCount);
        maximumCount = Mathf.Max(minimumCount, maximumCount);
        maximumActive = Mathf.Max(0, maximumActive);
        childHealthMultiplier = NormalizePositive(
            childHealthMultiplier,
            1f);
        childCoreAttackMultiplier = NormalizePositive(
            childCoreAttackMultiplier,
            1f);
    }

    private static float NormalizePositive(float value, float fallback)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f
            ? value
            : fallback;
    }
}

[Serializable]
public sealed class EnemyBossPhaseDefinition
{
    [SerializeField]
    private string phaseId;
    [SerializeField]
    private string nameLocalizationKey;
    [SerializeField]
    private string fallbackName;
    [SerializeField, Range(0, 100)]
    private int minimumHealthPercent;
    [SerializeField, Range(0, 100)]
    private int maximumHealthPercent = 100;
    [SerializeField, Tooltip(
        "Advance to the next phase on core contact even when the next " +
        "phase health threshold has not been reached. Health threshold " +
        "or core contact can advance the phase; this is not an AND gate.")]
    private bool advanceOnCoreContact;
    [SerializeField]
    private List<string> abilityIds = new();

    public string PhaseId => phaseId ?? string.Empty;
    public string NameLocalizationKey => nameLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public int MinimumHealthPercent => minimumHealthPercent;
    public int MaximumHealthPercent => maximumHealthPercent;
    public bool AdvanceOnCoreContact => advanceOnCoreContact;
    public IReadOnlyList<string> AbilityIds => abilityIds != null
        ? abilityIds
        : Array.Empty<string>();

    public void Validate()
    {
        phaseId = (phaseId ?? string.Empty).Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        minimumHealthPercent = Mathf.Clamp(
            minimumHealthPercent,
            0,
            100);
        maximumHealthPercent = Mathf.Clamp(
            maximumHealthPercent,
            0,
            100);
        abilityIds ??= new List<string>();
        for (int index = 0; index < abilityIds.Count; index++)
        {
            abilityIds[index] =
                (abilityIds[index] ?? string.Empty).Trim();
        }
    }
}

public static class EnemyCoreAttackDamageResolver
{
    public static int Resolve(
        float damage,
        EnemyCoreAttackDamagePolicy policy,
        ref float fractionalRemainder)
    {
        if (!IsFinite(damage) || damage <= 0f)
            return 0;

        if (policy == EnemyCoreAttackDamagePolicy.LegacyInteger)
        {
            fractionalRemainder = 0f;
            if (damage >= int.MaxValue)
                return int.MaxValue;
            return Mathf.Max(1, Mathf.RoundToInt(damage));
        }

        float remainder = IsFinite(fractionalRemainder)
            ? Mathf.Clamp(fractionalRemainder, 0f, 0.9999f)
            : 0f;
        double accumulated = (double)damage + remainder;
        if (double.IsInfinity(accumulated) || accumulated >= int.MaxValue)
        {
            fractionalRemainder = 0f;
            return int.MaxValue;
        }
        int resolved = Mathf.Max(
            0,
            Mathf.FloorToInt((float)(accumulated + 0.0001d)));
        fractionalRemainder = Mathf.Clamp(
            (float)(accumulated - resolved),
            0f,
            0.9999f);
        return resolved;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
