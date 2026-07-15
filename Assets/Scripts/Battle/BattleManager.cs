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
public sealed class BattleManager : MonoBehaviour
{
    private const float DefaultGameSpeed = 1f;

    private static readonly float[] GameSpeedScales =
    {
        DefaultGameSpeed,
        2f,
        3f,
    };

    private readonly List<DungeonEnemyData> _spawnQueue = new();
    private readonly List<IBattleCharacter> _characters = new();

    private GameManager _manager;
    private IBattleBoard _board;
    private int _maximumEnemyCount;
    private int _spawnedEnemyCount;
    private float _spawnInterval;
    private float _spawnTimeRemaining;
    private int _gameSpeedIndex;
    private bool _isPaused;
    private bool _boardFull;
    private bool _controlsGameTime;

    public EBattleState State { get; private set; } = EBattleState.Uninitialized;
    public bool IsInitialized => _manager != null;
    public bool HasSession => _board != null;
    public bool IsPaused => _isPaused;
    public bool IsBoardFull => _boardFull;
    public float GameSpeed => GameSpeedScales[_gameSpeedIndex];
    public float SpawnInterval => GetNextSpawnInterval();
    public float SpawnTimeRemaining => _spawnTimeRemaining;
    public int PendingEnemyCount => _spawnQueue.Count;
    public int SpawnedEnemyCount => _spawnedEnemyCount;
    public int MaximumEnemyCount => _maximumEnemyCount;
    public int RemainingEnemySpawnCount =>
        Mathf.Max(0, _maximumEnemyCount - _spawnedEnemyCount);
    public IReadOnlyList<DungeonEnemyData> SpawnQueue => _spawnQueue;

    public event Action<EBattleState> StateChanged;
    public event Action SpawnQueueChanged;
    public event Action SpawnTimerChanged;
    public event Action TimeControlChanged;
    public event Action BattleCompleted;

    private void Update()
    {
        if (State != EBattleState.Running || _board == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

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
        IReadOnlyList<DungeonEnemyData> enemies,
        float spawnInterval)
    {
        if (!IsInitialized || board == null || characters == null || enemies == null)
        {
            Debug.LogError("BattleManager cannot start without a board, party, and enemies.", this);
            return false;
        }

        ReleaseSession();
        _board = board;
        _spawnInterval = Mathf.Max(0.1f, spawnInterval);

        foreach (IBattleCharacter character in characters)
        {
            if (character == null || !character.Initialize())
                continue;

            character.ResetRuntime();
            _characters.Add(character);
        }

        foreach (DungeonEnemyData enemy in enemies)
        {
            if (enemy != null)
                _spawnQueue.Add(enemy);
        }

        _maximumEnemyCount = _spawnQueue.Count;
        _board.ClearAllEnemies();
        ResetTimeControl();
        FillInitialBoard();
        ResetSpawnTimerForNextEnemy();
        _boardFull = false;

        SetState(EBattleState.Running);
        ApplyBattleTimeScale();
        NotifyQueueAndTimerChanged();
        TimeControlChanged?.Invoke();
        CheckForCompletion();
        return true;
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

    public bool QueueEnemy(int health)
    {
        if (!HasSession || State == EBattleState.Completed ||
            _spawnedEnemyCount + _spawnQueue.Count >= _maximumEnemyCount)
        {
            return false;
        }

        bool wasEmpty = _spawnQueue.Count == 0;
        _spawnQueue.Add(new DungeonEnemyData(health));
        if (wasEmpty)
            ResetSpawnTimerForNextEnemy();

        NotifyQueueAndTimerChanged();
        return true;
    }

    public bool SpawnNextEnemyImmediately()
    {
        if (!HasSession || State == EBattleState.Completed)
            return false;

        _spawnTimeRemaining = 0f;
        return TrySpawnNextQueuedEnemy();
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
        TimeControlChanged = null;
        BattleCompleted = null;
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

        if (!_board.TryAddEnemy(_spawnQueue[0]))
        {
            _boardFull = true;
            SpawnTimerChanged?.Invoke();
            return false;
        }

        _spawnQueue.RemoveAt(0);
        _spawnedEnemyCount++;
        ResetSpawnTimerForNextEnemy();
        _boardFull = false;
        NotifyQueueAndTimerChanged();
        return true;
    }

    private void FillInitialBoard()
    {
        if (_board == null)
            return;

        int targetCount = Mathf.Min(
            _board.InitialEnemyCapacity,
            _spawnQueue.Count);
        for (int index = 0; index < targetCount; index++)
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

        _isPaused = false;
        SetState(EBattleState.Completed);
        RestoreDefaultTimeScale();
        TimeControlChanged?.Invoke();
        BattleCompleted?.Invoke();
    }

    private void ReleaseSession()
    {
        RestoreDefaultTimeScale();
        _spawnQueue.Clear();
        _characters.Clear();
        _board = null;
        _maximumEnemyCount = 0;
        _spawnedEnemyCount = 0;
        _spawnInterval = 0f;
        _spawnTimeRemaining = 0f;
        _boardFull = false;
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
        return Mathf.Max(0.01f, _spawnInterval * multiplier);
    }
}
