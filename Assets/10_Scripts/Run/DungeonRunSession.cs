using System;
using System.Collections.Generic;

public enum EDungeonRunActivity
{
    None,
    StartingSelection,
    StartingItemSelection,
    TutorialGuide,
    Battle,
    Event,
    Rest,
    Shop,
    Result,
}

[Flags]
public enum EDungeonPauseReason
{
    None = 0,
    PageHidden = 1 << 0,
    TutorialGuide = 1 << 1,
    UserPause = 1 << 2,
    Result = 1 << 3,
    NonBattlePhase = 1 << 4,
    BattleReward = 1 << 5,
}

public sealed class DungeonPauseCoordinator
{
    public EDungeonPauseReason Reasons { get; private set; }
    public bool IsPaused => Reasons != EDungeonPauseReason.None;
    public bool HasBlockingReason =>
        (Reasons & ~EDungeonPauseReason.UserPause) !=
        EDungeonPauseReason.None;
    public bool IsUserPaused =>
        (Reasons & EDungeonPauseReason.UserPause) != 0;

    public event Action<EDungeonPauseReason> Changed;

    public void Add(EDungeonPauseReason reason)
    {
        EDungeonPauseReason next = Reasons | reason;
        if (next == Reasons)
            return;

        Reasons = next;
        Changed?.Invoke(Reasons);
    }

    public void Remove(EDungeonPauseReason reason)
    {
        EDungeonPauseReason next = Reasons & ~reason;
        if (next == Reasons)
            return;

        Reasons = next;
        Changed?.Invoke(Reasons);
    }

    public void Clear()
    {
        if (Reasons == EDungeonPauseReason.None)
            return;

        Reasons = EDungeonPauseReason.None;
        Changed?.Invoke(Reasons);
    }
}

public sealed class DungeonStateBag
{
    private readonly Dictionary<string, int> _integers =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _floats =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings =
        new(StringComparer.Ordinal);

    public void SetInt(string key, int value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _integers[key] = value;
    }

    public int GetInt(string key, int fallback = 0)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               _integers.TryGetValue(key, out int value)
            ? value
            : fallback;
    }

    public void SetFloat(string key, float value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _floats[key] = value;
    }

    public float GetFloat(string key, float fallback = 0f)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               _floats.TryGetValue(key, out float value)
            ? value
            : fallback;
    }

    public void SetString(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _strings[key] = value ?? string.Empty;
    }

    public string GetString(string key, string fallback = "")
    {
        return !string.IsNullOrWhiteSpace(key) &&
               _strings.TryGetValue(key, out string value)
            ? value
            : fallback;
    }

    public void Clear()
    {
        _integers.Clear();
        _floats.Clear();
        _strings.Clear();
    }
}

public sealed class DungeonRunSession
{
    private IReadOnlyList<EDungeonPhase> _phaseSequence =
        Array.Empty<EDungeonPhase>();

    public DungeonDefinition Definition { get; private set; }
    public string DungeonId => Definition != null
        ? Definition.DungeonId
        : string.Empty;
    public int ContentVersion => Definition != null
        ? Definition.ContentVersion
        : 0;
    public int RunSeed { get; private set; }
    public int TotalBattleCount { get; private set; }
    public int CurrentBattleNumber { get; private set; }
    public int TutorialStepIndex { get; private set; } = -1;
    public float PreferredGameSpeed { get; private set; } = 1f;
    public EDungeonRunActivity Activity { get; private set; }
    public EDungeonRunResult Result { get; private set; }
    public int RunCurrency { get; private set; }
    public IReadOnlyList<EDungeonPhase> PhaseSequence => _phaseSequence;
    public DungeonPauseCoordinator Pause { get; } = new();
    public DungeonStateBag State { get; } = new();
    public bool IsActive => Definition != null &&
                            Activity != EDungeonRunActivity.None;

    public void Begin(
        DungeonDefinition definition,
        int runSeed,
        int battleCount,
        IReadOnlyList<EDungeonPhase> phases,
        int initialRunCurrency = 0)
    {
        Definition = definition ?? throw new ArgumentNullException(
            nameof(definition));
        RunSeed = runSeed;
        TotalBattleCount = Math.Max(1, battleCount);
        CurrentBattleNumber = 1;
        TutorialStepIndex = -1;
        PreferredGameSpeed = 1f;
        Activity = EDungeonRunActivity.StartingSelection;
        Result = EDungeonRunResult.None;
        RunCurrency = Math.Max(0, initialRunCurrency);
        _phaseSequence = phases ?? Array.Empty<EDungeonPhase>();
        Pause.Clear();
        State.Clear();
    }

    public void SetActivity(EDungeonRunActivity activity)
    {
        Activity = activity;
    }

    public void AddRunCurrency(int amount)
    {
        long next = (long)RunCurrency + amount;
        RunCurrency = (int)Math.Max(0L, Math.Min(int.MaxValue, next));
    }

    public bool TrySpendRunCurrency(int amount)
    {
        amount = Math.Max(0, amount);
        if (RunCurrency < amount)
            return false;

        RunCurrency -= amount;
        return true;
    }

    public void SetBattleNumber(int battleNumber)
    {
        CurrentBattleNumber = Math.Max(1, battleNumber);
    }

    public void SetTutorialStep(int stepIndex)
    {
        TutorialStepIndex = Math.Max(-1, stepIndex);
    }

    public void SetPreferredGameSpeed(float speed)
    {
        PreferredGameSpeed = Math.Max(1f, speed);
    }

    public void Finish(EDungeonRunResult result)
    {
        Result = result;
        Activity = EDungeonRunActivity.Result;
        Pause.Add(EDungeonPauseReason.Result);
    }

    public void Reset()
    {
        Definition = null;
        RunSeed = 0;
        TotalBattleCount = 0;
        CurrentBattleNumber = 0;
        TutorialStepIndex = -1;
        PreferredGameSpeed = 1f;
        Activity = EDungeonRunActivity.None;
        Result = EDungeonRunResult.None;
        RunCurrency = 0;
        _phaseSequence = Array.Empty<EDungeonPhase>();
        Pause.Clear();
        State.Clear();
    }
}
