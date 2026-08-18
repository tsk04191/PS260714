using System;
using System.Collections.Generic;
using UnityEngine;

public enum EBattleState
{
    Uninitialized,
    Idle,
    Running,
    Paused,
    Suspended,
    Completed,
}

[DisallowMultipleComponent]
public sealed class BattleManager : MonoBehaviour, IActiveSkillResource,
    IBattleEnemySummonService
{
    private sealed class ScheduledEnemySummon
    {
        public EnemyRuntime Source { get; }
        public string AbilityId { get; }
        public EnemySummonDefinition Definition { get; }
        public float Remaining { get; private set; }

        public ScheduledEnemySummon(
            EnemyRuntime source,
            string abilityId,
            EnemySummonDefinition definition,
            float delaySeconds)
        {
            Source = source;
            AbilityId = (abilityId ?? string.Empty).Trim();
            Definition = definition;
            Remaining = Mathf.Max(0f, delaySeconds);
        }

        public bool Tick(float deltaTime)
        {
            Remaining = Mathf.Max(
                0f,
                Remaining - Mathf.Max(0f, deltaTime));
            return Remaining <= 0f;
        }
    }

    private sealed class TimedSpawnIntervalModifier
    {
        public string SourceId { get; }
        public float Multiplier { get; private set; }
        public float Remaining { get; private set; }

        public TimedSpawnIntervalModifier(
            string sourceId,
            float multiplier,
            float duration)
        {
            SourceId = sourceId;
            Reapply(multiplier, duration);
        }

        public void Reapply(float multiplier, float duration)
        {
            Multiplier = multiplier;
            Remaining = Mathf.Max(Remaining, duration);
        }

        public bool Tick(float deltaTime)
        {
            Remaining = Mathf.Max(
                0f,
                Remaining - Mathf.Max(0f, deltaTime));
            return Remaining <= 0f;
        }
    }

    private const float DefaultGameSpeed = 1f;
    public const float MinimumSpawnIntervalRandomMultiplier = 0.75f;
    public const float MaximumSpawnIntervalRandomMultiplier = 1.25f;
    public const int DefaultMaximumEnergy = 3;
    public const float DefaultEnergyRechargeDuration = 5f;
    public const int DefaultMaximumActiveSummons = 12;

    private static readonly float[] GameSpeedScales =
    {
        DefaultGameSpeed,
        2f,
        3f,
    };

    private readonly List<EnemyRuntime> _spawnQueue = new();
    private readonly List<IBattleCharacter> _characters = new();
    private readonly List<EnemyRuntime> _summonedEnemies = new();
    private readonly List<ScheduledEnemySummon> _scheduledEnemySummons =
        new();
    private readonly List<TimedSpawnIntervalModifier>
        _spawnIntervalModifiers = new();

    private GameManager _manager;
    private IBattleBoard _board;
    private IBattleObjective _objective;
    private int _maximumEnemyCount;
    private int _spawnedEnemyCount;
    private float _spawnInterval;
    private float _scheduledSpawnInterval;
    private float _spawnTimeRemaining;
    private float _battleDuration;
    private float _battleTimeRemaining;
    private int _gameSpeedIndex;
    private bool _isPaused;
    private bool _manualTargetSelectionPending;
    private IBattleManualTargetSelectionService
        _manualTargetSelectionService;
    private bool _boardFull;
    private bool _spawnRetryRequested;
    private bool _processingSpawnQueue;
    private bool _controlsGameTime;
    private int _activeSkillResource;
    private int _maximumActiveSkillResource = DefaultMaximumEnergy;
    private float _activeSkillRechargeDuration =
        DefaultEnergyRechargeDuration;
    private float _activeSkillRechargeRemaining;
    private BattleSessionOptions _sessionOptions =
        BattleSessionOptions.Standard;

    public EBattleState State { get; private set; } = EBattleState.Uninitialized;
    public bool IsInitialized => _manager != null;
    public bool HasSession => _board != null;
    public bool HasBattleObjective => _objective?.IsActive == true;
    public int BattleObjectiveHealth => _objective?.CurrentHealth ?? 0;
    public int BattleObjectiveMaximumHealth =>
        _objective?.MaximumHealth ?? 0;
    public bool IsPaused => _isPaused;
    public bool IsManualTargetSelectionPending =>
        _manualTargetSelectionPending;
    public bool IsBoardFull => _boardFull;
    public float GameSpeed => GameSpeedScales[_gameSpeedIndex];
    public float SpawnInterval => _scheduledSpawnInterval > 0f
        ? _scheduledSpawnInterval
        : GetNextSpawnInterval();
    public float SpawnTimeRemaining =>
        TimePrecision.FloorToTenth(_spawnTimeRemaining);
    public float BattleDuration => _battleDuration;
    public float BattleTimeRemaining =>
        TimePrecision.FloorToTenth(_battleTimeRemaining);
    public EBattleResult Result { get; private set; }
    public int ActiveSkillResource => _activeSkillResource;
    public int MaximumActiveSkillResource => _maximumActiveSkillResource;
    public float ActiveSkillRechargeDuration =>
        _activeSkillRechargeDuration;
    public float ActiveSkillRechargeRemaining =>
        TimePrecision.FloorToTenth(_activeSkillRechargeRemaining);
    int IActiveSkillResource.Current => _activeSkillResource;
    int IActiveSkillResource.Maximum => _maximumActiveSkillResource;
    public int PendingEnemyCount => _spawnQueue.Count;
    public int SpawnedEnemyCount => _spawnedEnemyCount;
    public int MaximumEnemyCount => _maximumEnemyCount;
    public int RemainingEnemySpawnCount =>
        Mathf.Max(0, _maximumEnemyCount - _spawnedEnemyCount);
    public IReadOnlyList<EnemyRuntime> SpawnQueue => _spawnQueue;
    public IReadOnlyList<IBattleCharacter> Characters => _characters;
    public BattleSessionOptions SessionOptions => _sessionOptions;
    public int MaximumActiveSummons => DefaultMaximumActiveSummons;
    public int PendingScheduledSummonCount =>
        _scheduledEnemySummons.Count;
    public int ActiveSummonCount
    {
        get
        {
            PruneSummonedEnemies();
            return _summonedEnemies.Count;
        }
    }

    public event Action<EBattleState> StateChanged;
    public event Action SpawnQueueChanged;
    public event Action SpawnTimerChanged;
    public event Action BattleTimeChanged;
    public event Action TimeControlChanged;
    public event Action BattleCompleted;
    public event Action<EBattleResult> BattleEnded;
    public event Action<int> ActiveSkillResourceChanged;
    public event Action ActiveSkillRechargeChanged;
    public event Action<int, int> BattleObjectiveHealthChanged;
    public event Action<IReadOnlyList<IBattleCharacter>> CharactersChanged;
    event Action<int> IActiveSkillResource.Changed
    {
        add => ActiveSkillResourceChanged += value;
        remove => ActiveSkillResourceChanged -= value;
    }

    private void Update()
    {
        if (State != EBattleState.Running || _board == null ||
            _manualTargetSelectionPending)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        TickBattleTimer(deltaTime);
        if (State != EBattleState.Running || _board == null)
            return;

        TickActiveSkillRecharge(deltaTime);
        TickScheduledEnemySummons(deltaTime);
        TickSpawnIntervalModifiers(deltaTime);
        _board.TickStatusEffects(deltaTime);
        if (_manualTargetSelectionPending)
            return;
        _board.TickEnemyAbilities(deltaTime, _characters);
        if (State != EBattleState.Running)
            return;
        if (CompleteIfPartyDefeated())
            return;
        if (_manualTargetSelectionPending)
            return;
        foreach (IBattleCharacter character in _characters)
        {
            character.TickBattle(deltaTime, _board);
            if (_manualTargetSelectionPending)
                return;
        }

        TickEnemySpawnQueue(deltaTime);
        if (CompleteIfPartyDefeated())
            return;
        CheckForCompletion();
    }

    public bool Setup(GameManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("BattleManager requires a GameManager.", this);
            return false;
        }

        if (_manager == manager)
            return true;

        Teardown();
        _manager = manager;
        SetState(EBattleState.Idle);
        return true;
    }

    public bool StartBattle(
        IBattleBoard board,
        IReadOnlyList<IBattleCharacter> characters,
        IReadOnlyList<EnemyRuntime> enemies,
        float spawnInterval,
        float timeLimit = 0f,
        int initialEnemyCount = 0,
        bool preserveCharacterHealth = false,
        BattleSessionOptions sessionOptions = null)
    {
        if (!IsInitialized || board == null || characters == null || enemies == null)
        {
            Debug.LogError("BattleManager cannot start without a board, party, and enemies.", this);
            return false;
        }

        ReleaseSession();
        _sessionOptions = sessionOptions ?? BattleSessionOptions.Standard;
        _board = board;
        if (_board is DungeonBoardView dungeonBoard)
            dungeonBoard.BindEnemySummonService(this);
        _board.OccupancyChanged += HandleBoardOccupancyChanged;
        _objective = (board as IBattleObjectiveProvider)?.Objective;
        if (_objective != null)
        {
            _objective.HealthChanged += HandleObjectiveHealthChanged;
            _objective.Destroyed += HandleObjectiveDestroyed;
        }
        _manualTargetSelectionService =
            board as IBattleManualTargetSelectionService;
        if (_manualTargetSelectionService != null)
        {
            _manualTargetSelectionService
                .ManualTargetSelectionPendingChanged +=
                HandleManualTargetSelectionPendingChanged;
        }
        _spawnInterval = TimePrecision.Normalize(spawnInterval, 0.1f);
        _battleDuration = TimePrecision.FloorToTenth(timeLimit);
        _battleTimeRemaining = _battleDuration;
        Result = EBattleResult.None;

        foreach (IBattleCharacter character in characters)
        {
            if (character == null || !character.Initialize())
                continue;

            if (preserveCharacterHealth &&
                character is CharacterRuntime persistentCharacter)
            {
                persistentCharacter.PrepareForNextBattle();
            }
            else
            {
                character.ResetRuntime();
            }
            character.BindBattle(this, _board);
            _characters.Add(character);
        }
        _board.SetBattleCharacters(_characters);
        CharactersChanged?.Invoke(Characters);

        foreach (EnemyRuntime enemy in enemies)
        {
            if (enemy != null)
                _spawnQueue.Add(enemy);
        }

        _maximumEnemyCount = _spawnQueue.Count;
        _board.ClearAllEnemies();
        ResetTimeControl();
        FillInitialBoard(initialEnemyCount);
        ResetSpawnTimerForNextEnemy();
        _boardFull = false;
        SetActiveSkillResource(_maximumActiveSkillResource);
        SetActiveSkillRechargeRemaining(0f);
        if (_objective?.IsActive == true)
        {
            BattleObjectiveHealthChanged?.Invoke(
                _objective.CurrentHealth,
                _objective.MaximumHealth);
        }

        SetState(EBattleState.Running);
        ApplyBattleTimeScale();
        NotifyQueueAndTimerChanged();
        BattleTimeChanged?.Invoke();
        TimeControlChanged?.Invoke();
        CheckForCompletion();
        return true;
    }

    public bool CanSpend(int amount)
    {
        return amount >= 0 && State == EBattleState.Running &&
               !_manualTargetSelectionPending &&
               _activeSkillResource >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount))
            return false;

        bool wasFull = _activeSkillResource >= _maximumActiveSkillResource;
        SetActiveSkillResource(_activeSkillResource - amount);
        if (wasFull && _activeSkillResource < _maximumActiveSkillResource)
        {
            SetActiveSkillRechargeRemaining(
                _activeSkillRechargeDuration);
        }
        return true;
    }

    public bool TryGain(int amount)
    {
        if (amount <= 0 || State != EBattleState.Running ||
            _activeSkillResource >= _maximumActiveSkillResource)
        {
            return false;
        }

        int previous = _activeSkillResource;
        SetActiveSkillResource(_activeSkillResource + amount);
        if (_activeSkillResource >= _maximumActiveSkillResource)
            SetActiveSkillRechargeRemaining(0f);
        return _activeSkillResource > previous;
    }

    public bool TryRefillActiveSkillResource()
    {
        if (!CanMutateSessionRuntime())
            return false;

        SetActiveSkillResource(_maximumActiveSkillResource);
        SetActiveSkillRechargeRemaining(0f);
        return true;
    }

    public bool TryRestoreObjective()
    {
        return CanMutateSessionRuntime() &&
               _objective?.IsActive == true &&
               _objective.RestoreToMaximum();
    }

    public void ConfigureActiveSkillResource(
        int maximumResource,
        float rechargeDuration)
    {
        _maximumActiveSkillResource = Mathf.Max(1, maximumResource);
        _activeSkillRechargeDuration = TimePrecision.Normalize(
            rechargeDuration,
            TimePrecision.Step);
        SetActiveSkillResource(_activeSkillResource);
        if (_activeSkillResource >= _maximumActiveSkillResource)
            SetActiveSkillRechargeRemaining(0f);
        ActiveSkillRechargeChanged?.Invoke();
    }

    public bool ResumeBattle()
    {
        if (!HasSession || State == EBattleState.Completed)
            return false;

        _isPaused = false;
        SetState(EBattleState.Running);
        ApplyBattleTimeScale();
        TimeControlChanged?.Invoke();
        return true;
    }

    public void SuspendBattle()
    {
        if (!HasSession || State == EBattleState.Completed)
            return;

        ResetTimeControl();
        SetState(EBattleState.Suspended);
        RestoreDefaultTimeScale();
        TimeControlChanged?.Invoke();
    }

    public void CycleGameSpeed()
    {
        if (!HasSession || State == EBattleState.Suspended ||
            State == EBattleState.Completed)
        {
            return;
        }

        _gameSpeedIndex = (_gameSpeedIndex + 1) % GameSpeedScales.Length;
        ApplyBattleTimeScale();
        TimeControlChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (!HasSession || (State != EBattleState.Running &&
            State != EBattleState.Paused) ||
            _manualTargetSelectionPending)
        {
            return;
        }

        _isPaused = !_isPaused;
        SetState(_isPaused ? EBattleState.Paused : EBattleState.Running);
        ApplyBattleTimeScale();
        TimeControlChanged?.Invoke();
    }

    public bool QueueEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !HasSession || State == EBattleState.Completed)
        {
            return false;
        }

        bool wasEmpty = _spawnQueue.Count == 0;
        _spawnQueue.Add(enemy);
        _maximumEnemyCount++;
        if (wasEmpty)
            ResetSpawnTimerForNextEnemy();
        if (_boardFull)
            _spawnRetryRequested = true;

        NotifyQueueAndTimerChanged();
        return true;
    }

    public bool TrySetBattleCharacters(
        IReadOnlyList<IBattleCharacter> characters,
        bool preserveNewCharacterHealth = false)
    {
        if (characters == null || !CanMutateSessionRuntime())
            return false;

        List<IBattleCharacter> nextCharacters = new(characters.Count);
        HashSet<IBattleCharacter> unique = new();
        for (int index = 0; index < characters.Count; index++)
        {
            IBattleCharacter character = characters[index];
            if (character == null || !unique.Add(character))
                return false;
            nextCharacters.Add(character);
        }

        bool sequenceChanged = nextCharacters.Count != _characters.Count;
        if (!sequenceChanged)
        {
            for (int index = 0; index < nextCharacters.Count; index++)
            {
                if (ReferenceEquals(
                        nextCharacters[index],
                        _characters[index]))
                {
                    continue;
                }

                sequenceChanged = true;
                break;
            }
        }
        if (!sequenceChanged)
            return true;

        HashSet<IBattleCharacter> current = new(_characters);
        List<IBattleCharacter> additions = new();
        foreach (IBattleCharacter character in nextCharacters)
        {
            if (current.Contains(character))
                continue;
            if (!character.Initialize())
                return false;
            additions.Add(character);
        }

        foreach (IBattleCharacter character in additions)
        {
            if (preserveNewCharacterHealth &&
                character is CharacterRuntime persistentCharacter)
            {
                persistentCharacter.PrepareForNextBattle();
            }
            else
            {
                character.ResetRuntime();
            }
        }

        foreach (IBattleCharacter character in _characters)
        {
            if (!unique.Contains(character))
                character?.BindBattle(null, null);
        }

        _characters.Clear();
        _characters.AddRange(nextCharacters);
        foreach (IBattleCharacter character in additions)
            character.BindBattle(this, _board);

        _board.SetBattleCharacters(_characters);
        CharactersChanged?.Invoke(Characters);
        if (SessionOptions.CompletesOn(
                BattleCompletionPolicy.PartyDefeated))
        {
            CompleteIfPartyDefeated();
        }
        return true;
    }

    public bool TrySpawnEnemyImmediately(EnemyRuntime enemy)
    {
        if (enemy == null || !CanMutateSessionRuntime() ||
            _processingSpawnQueue)
        {
            return false;
        }

        if (!_board.TryAddEnemy(enemy))
        {
            _boardFull = !_board.HasEmptyEnemyTile;
            SpawnTimerChanged?.Invoke();
            return false;
        }

        _maximumEnemyCount =
            BattleValueMath.SaturatingAddNonNegative(
                _maximumEnemyCount,
                1);
        _spawnedEnemyCount =
            BattleValueMath.SaturatingAddNonNegative(
                _spawnedEnemyCount,
                1);
        if (enemy.IsSummoned && !_summonedEnemies.Contains(enemy))
            _summonedEnemies.Add(enemy);
        _boardFull = false;
        _spawnRetryRequested = false;
        NotifyQueueAndTimerChanged();
        return true;
    }

    public bool TryClearAllEnemiesAndSpawns()
    {
        if (!CanMutateSessionRuntime() || _processingSpawnQueue)
            return false;

        _spawnQueue.Clear();
        _summonedEnemies.Clear();
        _scheduledEnemySummons.Clear();
        _spawnIntervalModifiers.Clear();
        _maximumEnemyCount = 0;
        _spawnedEnemyCount = 0;
        _boardFull = false;
        _spawnRetryRequested = false;
        ResetSpawnTimerForNextEnemy();
        _board.ClearAllEnemies();
        NotifyQueueAndTimerChanged();
        CheckForCompletion();
        return true;
    }

    public int TrySummonEnemies(
        EnemyRuntime source,
        string abilityId,
        EnemySummonDefinition definition)
    {
        if (!CanSummonFromSource(source, abilityId, definition) ||
            !HasSession || State == EBattleState.Completed ||
            definition == null)
        {
            return 0;
        }

        PruneSummonedEnemies();
        int globalAvailable = Mathf.Max(
            0,
            MaximumActiveSummons - _summonedEnemies.Count);
        if (globalAvailable <= 0)
            return 0;

        string resolvedAbilityId = (abilityId ?? string.Empty).Trim();
        int activeForDefinition = CountActiveSummons(
            source.Definition.EnemyId,
            resolvedAbilityId);
        int definitionAvailable = definition.MaximumActive > 0
            ? Mathf.Max(0, definition.MaximumActive - activeForDefinition)
            : globalAvailable;
        int available = Mathf.Min(globalAvailable, definitionAvailable);
        if (available <= 0)
            return 0;

        List<EnemySO> candidates = ResolveSummonCandidates(definition);
        if (candidates.Count == 0)
            return 0;

        Dictionary<string, int> candidateCountMap =
            ResolveCandidateCountMap(source, resolvedAbilityId);
        EnemySO fixedCandidate = null;
        int requested;
        if (candidateCountMap.Count > 0)
        {
            List<EnemySO> eligibleCandidates = new();
            foreach (EnemySO candidate in candidates)
            {
                if (candidate != null &&
                    candidateCountMap.TryGetValue(
                        candidate.EnemyId,
                        out int mappedCount) &&
                    mappedCount >= 1 &&
                    mappedCount <= definition.MaximumCount &&
                    mappedCount <= available)
                {
                    eligibleCandidates.Add(candidate);
                }
            }
            if (eligibleCandidates.Count == 0)
                return 0;

            fixedCandidate = eligibleCandidates[
                UnityEngine.Random.Range(0, eligibleCandidates.Count)];
            requested = candidateCountMap[fixedCandidate.EnemyId];
        }
        else
        {
            requested = UnityEngine.Random.Range(
                definition.MinimumCount,
                definition.MaximumCount + 1);
            requested = Mathf.Min(requested, available);
        }

        int summonedCount = 0;
        for (int index = 0; index < requested; index++)
        {
            EnemySO candidate = fixedCandidate != null
                ? fixedCandidate
                : candidates[
                    UnityEngine.Random.Range(0, candidates.Count)];
            EnemyRuntime summoned = candidate?.CreateRuntime();
            if (summoned == null)
                continue;

            summoned.MarkSummoned(
                source,
                resolvedAbilityId,
                definition.ChildHealthMultiplier,
                definition.ChildCoreAttackMultiplier);
            if (!QueueEnemy(summoned))
                continue;

            _summonedEnemies.Add(summoned);
            summonedCount++;
        }

        return summonedCount;
    }

    public bool TryScheduleSummon(
        EnemyRuntime source,
        string abilityId,
        EnemySummonDefinition definition,
        float delaySeconds)
    {
        if (float.IsNaN(delaySeconds) ||
            float.IsInfinity(delaySeconds) ||
            !CanSummonFromSource(source, abilityId, definition) ||
            !HasSession || State == EBattleState.Completed)
        {
            return false;
        }

        if (delaySeconds <= 0f)
            return TrySummonEnemies(source, abilityId, definition) > 0;

        _scheduledEnemySummons.Add(new ScheduledEnemySummon(
            source,
            abilityId,
            definition,
            delaySeconds));
        return true;
    }

    public bool TryAddSpawnIntervalModifier(
        string sourceId,
        float multiplier,
        float duration)
    {
        sourceId = (sourceId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(sourceId) ||
            float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier) ||
            multiplier <= 0f ||
            float.IsNaN(duration) ||
            float.IsInfinity(duration) ||
            duration <= 0f ||
            !HasSession || State == EBattleState.Completed)
        {
            return false;
        }

        float previousMultiplier =
            ResolveSpawnIntervalModifierMultiplier();
        foreach (TimedSpawnIntervalModifier modifier in
                 _spawnIntervalModifiers)
        {
            if (modifier != null && string.Equals(
                    modifier.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                modifier.Reapply(multiplier, duration);
                RescaleActiveSpawnTimer(
                    previousMultiplier,
                    ResolveSpawnIntervalModifierMultiplier());
                return true;
            }
        }

        _spawnIntervalModifiers.Add(new TimedSpawnIntervalModifier(
            sourceId,
            multiplier,
            duration));
        _spawnIntervalModifiers.Sort((left, right) => string.Compare(
            left?.SourceId,
            right?.SourceId,
            StringComparison.Ordinal));
        RescaleActiveSpawnTimer(
            previousMultiplier,
            ResolveSpawnIntervalModifierMultiplier());
        return true;
    }

    private void TickScheduledEnemySummons(float deltaTime)
    {
        for (int index = _scheduledEnemySummons.Count - 1;
             index >= 0;
             index--)
        {
            ScheduledEnemySummon scheduled =
                _scheduledEnemySummons[index];
            if (scheduled == null || !scheduled.Tick(deltaTime))
                continue;

            _scheduledEnemySummons.RemoveAt(index);
            TrySummonEnemies(
                scheduled.Source,
                scheduled.AbilityId,
                scheduled.Definition);
        }
    }

    private void TickSpawnIntervalModifiers(float deltaTime)
    {
        if (_spawnIntervalModifiers.Count == 0)
            return;

        float previousMultiplier =
            ResolveSpawnIntervalModifierMultiplier();
        bool removed = false;
        for (int index = _spawnIntervalModifiers.Count - 1;
             index >= 0;
             index--)
        {
            TimedSpawnIntervalModifier modifier =
                _spawnIntervalModifiers[index];
            if (modifier == null || modifier.Tick(deltaTime))
            {
                _spawnIntervalModifiers.RemoveAt(index);
                removed = true;
            }
        }

        if (removed)
        {
            RescaleActiveSpawnTimer(
                previousMultiplier,
                ResolveSpawnIntervalModifierMultiplier());
        }
    }

    private float ResolveSpawnIntervalModifierMultiplier()
    {
        float result = 1f;
        foreach (TimedSpawnIntervalModifier modifier in
                 _spawnIntervalModifiers)
        {
            if (modifier == null || modifier.Remaining <= 0f)
                continue;
            result *= modifier.Multiplier;
            if (float.IsNaN(result) || float.IsInfinity(result))
                return 1f;
        }

        return Mathf.Max(0.01f, result);
    }

    private void RescaleActiveSpawnTimer(
        float previousMultiplier,
        float currentMultiplier)
    {
        if (_scheduledSpawnInterval <= 0f ||
            previousMultiplier <= 0f ||
            currentMultiplier <= 0f)
        {
            return;
        }

        float ratio = currentMultiplier / previousMultiplier;
        _scheduledSpawnInterval = TimePrecision.Normalize(
            _scheduledSpawnInterval * ratio,
            0.1f);
        _spawnTimeRemaining = TimePrecision.Normalize(
            _spawnTimeRemaining * ratio,
            0.1f);
        SpawnTimerChanged?.Invoke();
    }

    private static bool CanSummonFromSource(
        EnemyRuntime source,
        string abilityId,
        EnemySummonDefinition definition)
    {
        if (source == null || definition == null ||
            (source.IsSummoned && !definition.AllowRecursiveSummon))
        {
            return false;
        }

        if (source.Health > 0)
            return true;

        string resolvedAbilityId = (abilityId ?? string.Empty).Trim();
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state?.Definition;
            if (ability != null &&
                string.Equals(
                    ability.AbilityId,
                    resolvedAbilityId,
                    StringComparison.Ordinal) &&
                ability.RespondsToTrigger(EnemyAbilityTrigger.OnDeath))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, int> ResolveCandidateCountMap(
        EnemyRuntime source,
        string abilityId)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        if (source == null || string.IsNullOrWhiteSpace(abilityId))
            return result;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state?.Definition;
            if (ability == null || !string.Equals(
                    ability.AbilityId,
                    abilityId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            foreach (EnemyAbilityParameterDefinition parameter in
                     ability.Parameters)
            {
                if (parameter == null ||
                    parameter.ValueType !=
                        EnemyAbilityParameterValueType.Text ||
                    !string.Equals(
                        parameter.Key,
                        "candidateCountMap",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string[] entries = parameter.TextValue.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (string entry in entries)
                {
                    string[] pair = entry.Split(':');
                    if (pair.Length != 2)
                        continue;
                    string candidateId = pair[0].Trim();
                    if (!string.IsNullOrEmpty(candidateId) &&
                        int.TryParse(pair[1].Trim(), out int count) &&
                        count > 0)
                    {
                        result[candidateId] = count;
                    }
                }
                return result;
            }

            return result;
        }

        return result;
    }

    private static List<EnemySO> ResolveSummonCandidates(
        EnemySummonDefinition definition)
    {
        List<EnemySO> result = new();
        HashSet<string> visitedIds = new(StringComparer.Ordinal);
        foreach (EnemyReferenceDefinition reference in definition.Candidates)
        {
            if (reference == null)
                continue;
            EnemySO candidate = reference.Enemy != null
                ? reference.Enemy
                : EnemyDefinitionCatalog.FindById(reference.EnemyId);
            if (candidate == null || string.IsNullOrWhiteSpace(
                    candidate.EnemyId) ||
                !visitedIds.Add(candidate.EnemyId))
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    private int CountActiveSummons(
        string summonerEnemyId,
        string abilityId)
    {
        int count = 0;
        foreach (EnemyRuntime enemy in _summonedEnemies)
        {
            if (enemy != null && enemy.Health > 0 &&
                string.Equals(
                    enemy.SummonerEnemyId,
                    summonerEnemyId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    enemy.OriginAbilityId,
                    abilityId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private void PruneSummonedEnemies()
    {
        for (int index = _summonedEnemies.Count - 1;
             index >= 0;
             index--)
        {
            EnemyRuntime enemy = _summonedEnemies[index];
            if (enemy == null || enemy.Health <= 0)
                _summonedEnemies.RemoveAt(index);
        }
    }

    public void NotifyBoardChanged()
    {
        HandleBoardOccupancyChanged();
    }

    public bool EndBattle(IBattleBoard board)
    {
        if (!HasSession || !ReferenceEquals(_board, board))
            return false;

        ReleaseSession();
        NotifyQueueAndTimerChanged();
        TimeControlChanged?.Invoke();
        return true;
    }

    public void Teardown()
    {
        if (_manager == null && State == EBattleState.Uninitialized)
            return;

        ReleaseSession();
        SetState(EBattleState.Uninitialized);
        _manager = null;
        StateChanged = null;
        SpawnQueueChanged = null;
        SpawnTimerChanged = null;
        BattleTimeChanged = null;
        TimeControlChanged = null;
        BattleCompleted = null;
        BattleEnded = null;
        ActiveSkillResourceChanged = null;
        ActiveSkillRechargeChanged = null;
        BattleObjectiveHealthChanged = null;
        CharactersChanged = null;
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private void SetState(EBattleState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(State);
    }

    private void TickEnemySpawnQueue(float deltaTime)
    {
        if (_spawnQueue.Count == 0)
        {
            return;
        }

        if (_boardFull && !_spawnRetryRequested)
            return;

        if (_spawnRetryRequested)
        {
            _spawnRetryRequested = false;
            TrySpawnNextQueuedEnemy();
            return;
        }

        _spawnTimeRemaining = Mathf.Max(
            0f,
            _spawnTimeRemaining - Mathf.Max(0f, deltaTime));
        if (_spawnTimeRemaining <= 0f)
            TrySpawnNextQueuedEnemy();
        else
            SpawnTimerChanged?.Invoke();
    }

    private bool TrySpawnNextQueuedEnemy()
    {
        if (_spawnQueue.Count == 0 || _board == null ||
            _processingSpawnQueue)
        {
            return false;
        }

        _processingSpawnQueue = true;
        _spawnRetryRequested = false;
        try
        {
            for (int queueIndex = 0;
                 queueIndex < _spawnQueue.Count;)
            {
                EnemyRuntime queueSource = _spawnQueue[queueIndex];
                int remainingQueueCount =
                    _spawnQueue.Count - queueIndex;
                EvaluateSpawnQueueAbilities(
                    queueSource,
                    remainingQueueCount,
                    out _,
                    out int expandedCount);
                int spawnCount = (int)Math.Min(
                    remainingQueueCount,
                    1L + expandedCount);
                bool spawned;
                if (spawnCount > 1)
                {
                    List<EnemyRuntime> spawnGroup =
                        _spawnQueue.GetRange(queueIndex, spawnCount);
                    spawned = _board.TryAddEnemiesToDistinctTiles(
                        spawnGroup);
                }
                else
                {
                    spawned = _board.TryAddEnemy(queueSource);
                }

                if (!spawned)
                {
                    queueIndex += spawnCount;
                    continue;
                }

                CommitSpawnQueueAbilities(
                    queueSource,
                    remainingQueueCount);
                _spawnQueue.RemoveRange(queueIndex, spawnCount);
                _spawnedEnemyCount =
                    BattleValueMath.SaturatingAddNonNegative(
                        _spawnedEnemyCount,
                        spawnCount);
                ResetSpawnTimerForNextEnemy();
                _boardFull = false;
                NotifyQueueAndTimerChanged();
                return true;
            }

            _spawnTimeRemaining = 0f;
            _boardFull = true;
            SpawnTimerChanged?.Invoke();
            return false;
        }
        finally
        {
            _processingSpawnQueue = false;
        }
    }

    private void FillInitialBoard(int initialEnemyCount)
    {
        if (_board == null)
            return;

        int requestedCount = initialEnemyCount > 0
            ? initialEnemyCount
            : _board.InitialEnemyCapacity;
        int targetCount = Mathf.Min(
            requestedCount,
            _board.InitialEnemyCapacity,
            _spawnQueue.Count);
        while (_spawnQueue.Count > 0 &&
               _board.LivingEnemyCount < targetCount)
        {
            if (!TrySpawnNextQueuedEnemy())
                break;
        }
    }

    private void CheckForCompletion()
    {
        if (!SessionOptions.CompletesOn(
                BattleCompletionPolicy.EnemiesCleared) ||
            State == EBattleState.Completed || _board == null ||
            _spawnQueue.Count > 0 ||
            _scheduledEnemySummons.Count > 0 ||
            _board.LivingEnemyCount > 0)
        {
            return;
        }

        CompleteBattle(EBattleResult.Victory);
    }

    private bool CompleteIfPartyDefeated()
    {
        if (!SessionOptions.CompletesOn(
                BattleCompletionPolicy.PartyDefeated) ||
            _objective?.IsActive == true)
            return false;

        if (State != EBattleState.Running || _characters.Count == 0)
            return false;

        foreach (IBattleCharacter character in _characters)
        {
            if (character != null && character.CurrentHealth > 0)
                return false;
        }

        CompleteBattle(EBattleResult.Defeat);
        return true;
    }

    private void TickBattleTimer(float deltaTime)
    {
        if (!SessionOptions.CompletesOn(
                BattleCompletionPolicy.TimeExpired) ||
            _battleDuration <= 0f || _battleTimeRemaining <= 0f)
            return;

        _battleTimeRemaining = Mathf.Max(
            0f,
            _battleTimeRemaining - Mathf.Max(0f, deltaTime));
        BattleTimeChanged?.Invoke();
        if (_battleTimeRemaining <= 0f)
            CompleteBattle(EBattleResult.Timeout);
    }

    private void CompleteBattle(EBattleResult result)
    {
        if (State == EBattleState.Completed)
            return;

        Result = result;
        _manualTargetSelectionService?.CancelManualTargetSelection();
        _isPaused = false;
        SetState(EBattleState.Completed);
        RestoreDefaultTimeScale();
        BattleTimeChanged?.Invoke();
        TimeControlChanged?.Invoke();
        BattleEnded?.Invoke(Result);
        if (Result == EBattleResult.Victory)
            BattleCompleted?.Invoke();
    }

    private void ReleaseSession()
    {
        RestoreDefaultTimeScale();
        if (_board != null)
            _board.OccupancyChanged -= HandleBoardOccupancyChanged;
        if (_manualTargetSelectionService != null)
        {
            _manualTargetSelectionService
                .ManualTargetSelectionPendingChanged -=
                HandleManualTargetSelectionPendingChanged;
            _manualTargetSelectionService.CancelManualTargetSelection();
        }
        if (_objective != null)
        {
            _objective.HealthChanged -= HandleObjectiveHealthChanged;
            _objective.Destroyed -= HandleObjectiveDestroyed;
        }
        _objective = null;
        _manualTargetSelectionService = null;
        _manualTargetSelectionPending = false;
        foreach (IBattleCharacter character in _characters)
            character?.BindBattle(null, null);

        _board?.SetBattleCharacters(null);
        if (_board is DungeonBoardView dungeonBoard)
            dungeonBoard.BindEnemySummonService(null);

        _spawnQueue.Clear();
        _summonedEnemies.Clear();
        _scheduledEnemySummons.Clear();
        _spawnIntervalModifiers.Clear();
        _characters.Clear();
        CharactersChanged?.Invoke(Characters);
        _board = null;
        _maximumEnemyCount = 0;
        _spawnedEnemyCount = 0;
        _spawnInterval = 0f;
        _scheduledSpawnInterval = 0f;
        _spawnTimeRemaining = 0f;
        _battleDuration = 0f;
        _battleTimeRemaining = 0f;
        _sessionOptions = BattleSessionOptions.Standard;
        _boardFull = false;
        _spawnRetryRequested = false;
        _processingSpawnQueue = false;
        Result = EBattleResult.None;
        SetActiveSkillResource(0);
        SetActiveSkillRechargeRemaining(0f);
        ResetTimeControl();

        if (IsInitialized)
            SetState(EBattleState.Idle);
    }

    private void ResetTimeControl()
    {
        _gameSpeedIndex = 0;
        _isPaused = false;
        _manualTargetSelectionPending = false;
    }

    private void ApplyBattleTimeScale()
    {
        Time.timeScale = _isPaused || _manualTargetSelectionPending
            ? 0f
            : GameSpeedScales[_gameSpeedIndex];
        _controlsGameTime = true;
    }

    private void HandleManualTargetSelectionPendingChanged(bool pending)
    {
        _manualTargetSelectionPending = pending;
        if (HasSession && State != EBattleState.Completed)
            ApplyBattleTimeScale();
        TimeControlChanged?.Invoke();
    }

    private void HandleBoardOccupancyChanged()
    {
        if (!HasSession)
            return;

        bool wasWaiting = _boardFull;
        _boardFull = false;
        if (wasWaiting && _spawnQueue.Count > 0 &&
            !_processingSpawnQueue)
        {
            _spawnRetryRequested = true;
        }

        SpawnTimerChanged?.Invoke();
        CheckForCompletion();
    }

    private void HandleObjectiveHealthChanged(int current, int maximum)
    {
        BattleObjectiveHealthChanged?.Invoke(current, maximum);
    }

    private void HandleObjectiveDestroyed()
    {
        if (State == EBattleState.Running &&
            SessionOptions.CompletesOn(
                BattleCompletionPolicy.ObjectiveDestroyed))
            CompleteBattle(EBattleResult.Defeat);
    }

    private bool CanMutateSessionRuntime()
    {
        return HasSession && State != EBattleState.Completed &&
               !_manualTargetSelectionPending;
    }

    private void RestoreDefaultTimeScale()
    {
        if (!_controlsGameTime)
            return;

        Time.timeScale = DefaultGameSpeed;
        _controlsGameTime = false;
    }

    private void NotifyQueueAndTimerChanged()
    {
        SpawnQueueChanged?.Invoke();
        SpawnTimerChanged?.Invoke();
    }

    private void TickActiveSkillRecharge(float deltaTime)
    {
        if (deltaTime <= 0f ||
            _activeSkillResource >= _maximumActiveSkillResource)
        {
            return;
        }

        float recoveryMultiplier =
            (_board as IEnemyCombatRuntimeServiceProvider)
            ?.EnemyCombatRuntimeService
            ?.ResolveResourceRecoveryMultiplier() ?? 1f;
        if (float.IsNaN(recoveryMultiplier) ||
            float.IsInfinity(recoveryMultiplier))
        {
            recoveryMultiplier = 1f;
        }

        float remainingDelta = deltaTime * Mathf.Max(
            0f,
            recoveryMultiplier);
        if (remainingDelta <= 0f)
            return;
        if (_activeSkillRechargeRemaining <= 0f)
        {
            SetActiveSkillRechargeRemaining(
                _activeSkillRechargeDuration);
        }

        while (remainingDelta > 0f &&
               _activeSkillResource < _maximumActiveSkillResource)
        {
            float appliedDelta = Mathf.Min(
                remainingDelta,
                _activeSkillRechargeRemaining);
            remainingDelta -= appliedDelta;
            SetActiveSkillRechargeRemaining(
                _activeSkillRechargeRemaining - appliedDelta);
            if (_activeSkillRechargeRemaining > 0f)
                break;

            SetActiveSkillResource(_activeSkillResource + 1);
            SetActiveSkillRechargeRemaining(
                _activeSkillResource < _maximumActiveSkillResource
                    ? _activeSkillRechargeDuration
                    : 0f);
        }
    }

    private void SetActiveSkillResource(int value)
    {
        value = Mathf.Clamp(value, 0, _maximumActiveSkillResource);
        if (_activeSkillResource == value)
            return;

        _activeSkillResource = value;
        ActiveSkillResourceChanged?.Invoke(_activeSkillResource);
    }

    private void SetActiveSkillRechargeRemaining(float value)
    {
        value = Mathf.Max(0f, value);
        if (Mathf.Approximately(_activeSkillRechargeRemaining, value))
            return;

        _activeSkillRechargeRemaining = value;
        ActiveSkillRechargeChanged?.Invoke();
    }

    private void ResetSpawnTimerForNextEnemy()
    {
        _scheduledSpawnInterval = _spawnQueue.Count > 0
            ? ResolveRandomizedSpawnInterval(
                GetNextSpawnInterval(),
                UnityEngine.Random.value)
            : 0f;
        _spawnTimeRemaining = _scheduledSpawnInterval;
    }

    public static float ResolveRandomizedSpawnInterval(
        float baseInterval,
        float randomSample)
    {
        if (baseInterval <= 0f)
            return 0f;

        float multiplier = Mathf.Lerp(
            MinimumSpawnIntervalRandomMultiplier,
            MaximumSpawnIntervalRandomMultiplier,
            Mathf.Clamp01(randomSample));
        return TimePrecision.Normalize(
            baseInterval * multiplier,
            0.1f);
    }

    private float GetNextSpawnInterval()
    {
        if (_spawnInterval <= 0f)
            return 0f;

        float multiplier = 1f;
        if (_spawnQueue.Count > 0 && _spawnQueue[0] != null)
        {
            EvaluateSpawnQueueAbilities(
                _spawnQueue[0],
                _spawnQueue.Count,
                out multiplier,
                out _);
        }
        return TimePrecision.Normalize(
            _spawnInterval * multiplier *
            ResolveSpawnIntervalModifierMultiplier(),
            0.1f);
    }

    private static void EvaluateSpawnQueueAbilities(
        EnemyRuntime source,
        int queueCount,
        out float intervalMultiplier,
        out int expandedCount)
    {
        intervalMultiplier = source?.SpawnIntervalMultiplier ?? 1f;
        expandedCount = 0;
        if (source == null)
            return;

        bool hasAlternateTarget = queueCount > 1;
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                !source.IsAbilityEnabledInCurrentPhase(ability) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.OnSpawnQueueEvaluation) ||
                !EnemyAbilityConditionEvaluator.MatchesSourceOnly(
                    ability,
                    source,
                    hasAlternateTarget))
            {
                continue;
            }

            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled)
                    continue;

                if (operation.Type ==
                    EnemyAbilityOperationType.ModifySpawnInterval)
                {
                    intervalMultiplier *= operation.Multiplier;
                }
                else if (operation.Type ==
                         EnemyAbilityOperationType.ExpandSpawnGroup)
                {
                    long expanded =
                        (long)expandedCount + operation.Count;
                    expandedCount = expanded >= int.MaxValue
                        ? int.MaxValue
                        : (int)expanded;
                }
            }
        }

        if (float.IsNaN(intervalMultiplier) ||
            float.IsInfinity(intervalMultiplier) ||
            intervalMultiplier <= 0f)
        {
            intervalMultiplier = 1f;
        }
        expandedCount = Mathf.Max(0, expandedCount);
    }

    private static void CommitSpawnQueueAbilities(
        EnemyRuntime source,
        int queueCount)
    {
        if (source == null)
            return;

        bool hasAlternateTarget = queueCount > 1;
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                !source.IsAbilityEnabledInCurrentPhase(ability) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.OnSpawnQueueEvaluation) ||
                !EnemyAbilityConditionEvaluator.MatchesSourceOnly(
                    ability,
                    source,
                    hasAlternateTarget))
            {
                continue;
            }

            bool attempted = false;
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation != null && operation.Enabled &&
                    (operation.Type ==
                         EnemyAbilityOperationType.ModifySpawnInterval ||
                     operation.Type ==
                         EnemyAbilityOperationType.ExpandSpawnGroup))
                {
                    attempted = true;
                    break;
                }
            }

            state.RecordActivation(
                attempted,
                attempted,
                source.MaxHealth > 0
                    ? source.Health * 100f / source.MaxHealth
                    : 0f);
        }
    }
}
