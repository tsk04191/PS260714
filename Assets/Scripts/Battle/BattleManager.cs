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
public sealed class BattleManager : MonoBehaviour, IActiveSkillResource
{
    private const float DefaultGameSpeed = 1f;
    public const int DefaultMaximumEnergy = 3;
    public const float DefaultEnergyRechargeDuration = 5f;

    private static readonly float[] GameSpeedScales =
    {
        DefaultGameSpeed,
        2f,
        3f,
    };

    private readonly List<EnemyRuntime> _spawnQueue = new();
    private readonly List<IBattleCharacter> _characters = new();

    private GameManager _manager;
    private IBattleBoard _board;
    private int _maximumEnemyCount;
    private int _spawnedEnemyCount;
    private float _spawnInterval;
    private float _spawnTimeRemaining;
    private float _battleDuration;
    private float _battleTimeRemaining;
    private int _gameSpeedIndex;
    private bool _isPaused;
    private bool _boardFull;
    private bool _controlsGameTime;
    private int _activeSkillResource;
    private int _maximumActiveSkillResource = DefaultMaximumEnergy;
    private float _activeSkillRechargeDuration =
        DefaultEnergyRechargeDuration;
    private float _activeSkillRechargeRemaining;

    public EBattleState State { get; private set; } = EBattleState.Uninitialized;
    public bool IsInitialized => _manager != null;
    public bool HasSession => _board != null;
    public bool IsPaused => _isPaused;
    public bool IsBoardFull => _boardFull;
    public float GameSpeed => GameSpeedScales[_gameSpeedIndex];
    public float SpawnInterval => GetNextSpawnInterval();
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

    public event Action<EBattleState> StateChanged;
    public event Action SpawnQueueChanged;
    public event Action SpawnTimerChanged;
    public event Action BattleTimeChanged;
    public event Action TimeControlChanged;
    public event Action BattleCompleted;
    public event Action<EBattleResult> BattleEnded;
    public event Action<int> ActiveSkillResourceChanged;
    public event Action ActiveSkillRechargeChanged;
    event Action<int> IActiveSkillResource.Changed
    {
        add => ActiveSkillResourceChanged += value;
        remove => ActiveSkillResourceChanged -= value;
    }

    private void Update()
    {
        if (State != EBattleState.Running || _board == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        TickBattleTimer(deltaTime);
        if (State != EBattleState.Running || _board == null)
            return;

        TickActiveSkillRecharge(deltaTime);
        _board.TickStatusEffects(deltaTime);
        _board.TickEnemyAbilities(deltaTime, _characters);
        foreach (IBattleCharacter character in _characters)
            character.TickBattle(deltaTime, _board);

        TickEnemySpawnQueue(deltaTime);
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
        int initialEnemyCount = 0)
    {
        if (!IsInitialized || board == null || characters == null || enemies == null)
        {
            Debug.LogError("BattleManager cannot start without a board, party, and enemies.", this);
            return false;
        }

        ReleaseSession();
        _board = board;
        _spawnInterval = TimePrecision.Normalize(spawnInterval, 0.1f);
        _battleDuration = TimePrecision.FloorToTenth(timeLimit);
        _battleTimeRemaining = _battleDuration;
        Result = EBattleResult.None;

        foreach (IBattleCharacter character in characters)
        {
            if (character == null || !character.Initialize())
                continue;

            character.ResetRuntime();
            character.BindBattle(this, _board);
            _characters.Add(character);
        }
        _board.SetBattleCharacters(_characters);

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
            State != EBattleState.Paused))
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

        NotifyQueueAndTimerChanged();
        return true;
    }

    public void NotifyBoardChanged()
    {
        if (!HasSession)
            return;

        _boardFull = false;
        SpawnTimerChanged?.Invoke();

        CheckForCompletion();
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
            SpawnTimerChanged?.Invoke();
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
        if (_spawnQueue.Count == 0 || _board == null)
            return false;

        int spawnCount = _spawnQueue[0].Type == EEnemyType.Pointman
            ? Mathf.Min(
                _spawnQueue[0].Definition.CompanionSpawnCount + 1,
                _spawnQueue.Count)
            : 1;
        bool spawned;
        if (spawnCount > 1)
        {
            List<EnemyRuntime> spawnGroup = _spawnQueue.GetRange(
                0,
                spawnCount);
            spawned = _board.TryAddEnemiesToDistinctTiles(spawnGroup);
        }
        else
        {
            spawned = _board.TryAddEnemy(_spawnQueue[0]);
        }

        if (!spawned)
        {
            _boardFull = true;
            SpawnTimerChanged?.Invoke();
            return false;
        }

        _spawnQueue.RemoveRange(0, spawnCount);
        _spawnedEnemyCount += spawnCount;
        ResetSpawnTimerForNextEnemy();
        _boardFull = false;
        NotifyQueueAndTimerChanged();
        return true;
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
        if (State == EBattleState.Completed || _board == null ||
            _spawnQueue.Count > 0 || _board.LivingEnemyCount > 0)
        {
            return;
        }

        CompleteBattle(EBattleResult.Victory);
    }

    private void TickBattleTimer(float deltaTime)
    {
        if (_battleDuration <= 0f || _battleTimeRemaining <= 0f)
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
        foreach (IBattleCharacter character in _characters)
            character?.BindBattle(null, null);

        _board?.SetBattleCharacters(null);

        _spawnQueue.Clear();
        _characters.Clear();
        _board = null;
        _maximumEnemyCount = 0;
        _spawnedEnemyCount = 0;
        _spawnInterval = 0f;
        _spawnTimeRemaining = 0f;
        _battleDuration = 0f;
        _battleTimeRemaining = 0f;
        _boardFull = false;
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
    }

    private void ApplyBattleTimeScale()
    {
        Time.timeScale = _isPaused ? 0f : GameSpeedScales[_gameSpeedIndex];
        _controlsGameTime = true;
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

        float remainingDelta = deltaTime;
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
        _spawnTimeRemaining = _spawnQueue.Count > 0
            ? GetNextSpawnInterval()
            : 0f;
    }

    private float GetNextSpawnInterval()
    {
        if (_spawnInterval <= 0f)
            return 0f;

        float multiplier = _spawnQueue.Count > 0 && _spawnQueue[0] != null
            ? _spawnQueue[0].SpawnIntervalMultiplier
            : 1f;
        return TimePrecision.Normalize(
            _spawnInterval * multiplier,
            0.1f);
    }
}
