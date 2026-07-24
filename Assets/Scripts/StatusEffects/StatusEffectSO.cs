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

public static class StatusEffectIds
{
    public const string Fire = "fire";
    public const string Stun = "stun";
    public const string EmergencyKit = "emergency_kit";
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
    public float TickInterval { get; }
    public IBattleCharacter Source { get; }

    public StatusEffectRuntimeBatch(
        int stacks,
        float remainingDuration,
        float tickInterval,
        IBattleCharacter source)
    {
        Stacks = Mathf.Max(1, stacks);
        RemainingDuration = remainingDuration;
        TickInterval = TimePrecision.Normalize(
            tickInterval,
            TimePrecision.Step);
        Source = source;
    }
}

internal readonly struct StatusEffectRuntimeMutation
{
    public bool Succeeded { get; }
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }
    public IBattleCharacter Source { get; }
    public bool StackChanged => PreviousStacks != CurrentStacks;
    public int RemovedStacks => Mathf.Max(0, PreviousStacks - CurrentStacks);

    public StatusEffectRuntimeMutation(
        bool succeeded,
        int previousStacks,
        int currentStacks,
        IBattleCharacter source)
    {
        Succeeded = succeeded;
        PreviousStacks = Mathf.Max(0, previousStacks);
        CurrentStacks = Mathf.Max(0, currentStacks);
        Source = source;
    }
}

internal sealed class StatusEffectRuntimeState
{
    private const float TickEpsilon = 0.0001f;

    private readonly List<StatusEffectRuntimeBatch> _batches = new();
    private float _tickElapsed;

    public StatusEffectSO Definition { get; }
    public bool HasStacks => _batches.Count > 0;
    public StatusEffectRuntimeBatch ActiveBatch =>
        _batches.Count > 0 ? _batches[0] : null;
    public int StackCount
    {
        get
        {
            int total = 0;
            foreach (StatusEffectRuntimeBatch batch in _batches)
                total += Mathf.Max(0, batch.Stacks);
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

    public StatusEffectRuntimeState(StatusEffectSO definition)
    {
        Definition = definition;
    }

    public StatusEffectRuntimeMutation Apply(
        int stacks,
        float remainingDuration,
        float tickInterval,
        IBattleCharacter source)
    {
        int previousTotalStacks = StackCount;
        stacks = ClampIncomingStacks(stacks);
        if (stacks <= 0)
            return default;

        if (!HasStacks)
        {
            AddBatch(stacks, remainingDuration, tickInterval, source);
            return new StatusEffectRuntimeMutation(
                true,
                previousTotalStacks,
                StackCount,
                source);
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
                source);
            return new StatusEffectRuntimeMutation(
                true,
                previousTotalStacks,
                StackCount,
                source);
        }

        StatusEffectRuntimeBatch active = ActiveBatch;
        int previousStacks = active.Stacks;
        float previousDuration = active.RemainingDuration;
        int nextStacks = Definition.StackMode ==
                         StatusEffectStackMode.Replace
            ? stacks
            : ClampTotalStacks(active.Stacks + stacks);
        float nextDuration = previousDuration;
        if (Definition.DurationMode ==
            StatusEffectDurationMode.Permanent)
        {
            nextDuration = float.PositiveInfinity;
        }
        else if (Definition.StackMode !=
                     StatusEffectStackMode.AddKeepDuration ||
                 Definition.RefreshDurationOnReapply)
        {
            nextDuration = remainingDuration;
        }

        bool changed = nextStacks != previousStacks ||
                       !DurationsEqual(nextDuration, previousDuration);
        if (!changed)
            return default;

        _batches.Clear();
        AddBatch(nextStacks, nextDuration, tickInterval, source);
        return new StatusEffectRuntimeMutation(
            true,
            previousTotalStacks,
            StackCount,
            source);
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

        IBattleCharacter source = null;
        bool sourceCaptured = false;
        while (remaining > 0 && _batches.Count > 0)
        {
            int batchIndex = GetRemovalBatchIndex();
            StatusEffectRuntimeBatch batch = _batches[batchIndex];
            if (!sourceCaptured)
            {
                source = batch.Source;
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
            source);
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
        IBattleCharacter source = active.Source;
        _batches.RemoveAt(0);
        if (!HasStacks)
            _tickElapsed = 0f;
        return new StatusEffectRuntimeMutation(
            true,
            previousStacks,
            StackCount,
            source);
    }

    private void AddBatch(
        int stacks,
        float remainingDuration,
        float tickInterval,
        IBattleCharacter source)
    {
        _batches.Add(new StatusEffectRuntimeBatch(
            stacks,
            remainingDuration,
            tickInterval,
            source));
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
public sealed class StatusEffectSO : ScriptableObject
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

    public string StatusId => statusId;
    public Sprite Icon => icon;
    public string NameLocalizationKey => nameLocalizationKey;
    public string DescriptionLocalizationKey => descriptionLocalizationKey;
    public StatusEffectAlignment Alignment => alignment;
    public bool CanTargetEnemy => canTargetEnemy;
    public bool CanTargetAlly => canTargetAlly;
    public GameObject VisualEffectPrefab => visualEffectPrefab;
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
    public bool IncludedInRandomRemoval =>
        removable && includedInRandomRemoval;
    public bool IncludedInAllRemoval => removable && includedInAllRemoval;
    public IReadOnlyList<StatusEffectOperationDefinition> Operations =>
        operations;

    private void OnEnable()
    {
        EnsureStatusId();
    }

    private void OnValidate()
    {
        EnsureStatusId();
        ValidateDefinition();
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
    }

    public void RegenerateStatusId()
    {
        statusId = Guid.NewGuid().ToString("N");
    }

    private void EnsureStatusId()
    {
        if (string.IsNullOrWhiteSpace(statusId))
            RegenerateStatusId();
    }
}

public static class StatusEffectDefinitionCatalog
{
    private static StatusEffectSO[] _cached;

    public static IReadOnlyList<StatusEffectSO> LoadAll()
    {
        _cached ??= Resources.LoadAll<StatusEffectSO>("StatusEffects");
        return _cached;
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
