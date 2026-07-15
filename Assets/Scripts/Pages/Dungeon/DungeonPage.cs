using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonPage : MonoBehaviour, IPage
{
    public const int MaximumPartySize = 4;

    private const float DefaultGameSpeed = 1f;

    private static readonly float[] GameSpeedScales =
    {
        DefaultGameSpeed,
        2f,
        3f,
    };

    [Header("Dungeon Board")]
    [SerializeField, Range(DungeonBoardView.MinimumGridSize, DungeonBoardView.MaximumGridSize)]
    private int initialGridSize = DungeonBoardView.MinimumGridSize;

    [SerializeField, Range(1, 20)] private int maximumStackSize = 8;
    [SerializeField, Range(0.4f, 0.95f)] private float boardWidthRatio = 0.72f;
    [SerializeField, Range(0.4f, 0.95f)] private float boardHeightRatio = 0.78f;
    [SerializeField, Min(100f)] private float maximumBoardSize = 760f;
    [SerializeField] private DungeonBoardView board;

    [Header("Player Party")]
    [SerializeField] private CharacterRuntime[] playerCharacters =
        new CharacterRuntime[MaximumPartySize];

    [Header("Dungeon Controls")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseText;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private Button debugButton;
    [SerializeField] private GameObject debugPopup;
    [SerializeField] private Button spawnEnemyButton;

    [Header("Enemy Spawn Queue")]
    [SerializeField, Min(1)] private int maximumEnemiesPerRound = 20;
    [SerializeField, Min(0.1f)] private float enemySpawnInterval = 4f;
    [SerializeField] private DungeonSpawnQueueView spawnQueueView;

    private readonly List<DungeonEnemyData> _spawnQueue = new();
    private bool _initialized;
    private bool _speedButtonBound;
    private bool _pauseButtonBound;
    private bool _debugButtonBound;
    private bool _spawnButtonBound;
    private int _gameSpeedIndex;
    private bool _isGamePaused;
    private bool _controlsGameTime;
    private int _spawnedEnemyCount;
    private float _spawnTimeRemaining;
    private bool _boardFull;

    public AudioSource Speaker { get; set; }
    public int GridSize => board != null ? board.GridSize : initialGridSize;
    public int PendingEnemyCount => _spawnQueue.Count;
    public int SpawnedEnemyCount => _spawnedEnemyCount;
    public int RemainingEnemySpawnCount =>
        Mathf.Max(0, maximumEnemiesPerRound - _spawnedEnemyCount);

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        RefreshBoardSize();
    }

    private void Update()
    {
        if (_initialized)
            TickEnemySpawnQueue(Time.deltaTime);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshBoardSize();
    }

    private void OnDisable()
    {
        RestoreDefaultTimeScale();
        UnbindControlButtons();
    }

    private void OnDestroy()
    {
        RestoreDefaultTimeScale();
        UnbindControlButtons();
    }

    private void OnValidate()
    {
        initialGridSize = Mathf.Clamp(
            initialGridSize,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        maximumStackSize = Mathf.Max(1, maximumStackSize);
        maximumEnemiesPerRound = Mathf.Max(1, maximumEnemiesPerRound);
        enemySpawnInterval = Mathf.Max(0.1f, enemySpawnInterval);
        EnsurePlayerCharacterSlots();

        if (Application.isPlaying && _initialized)
        {
            bool wasQueueEmpty = _spawnQueue.Count == 0;
            TrimSpawnQueueToRoundLimit();
            FillRoundSpawnQueue();
            if (wasQueueEmpty && _spawnQueue.Count > 0)
                _spawnTimeRemaining = enemySpawnInterval;

            RefreshSpawnQueueUi();

            if (board != null)
            {
                board.SetGridSize(initialGridSize);
                RefreshBoardSize();
            }
        }
    }

    public void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        gameObject.SetActive(true);

        if (!_initialized)
            Init();

        BindControlButtons();

        if (mode == PageOpenMode.Fresh && board != null)
        {
            board.ClearAllStacks();
            ResetEnemySpawnQueue();
            ResetGameTimeState();
            ResetPlayerCharacters();
        }
        else
        {
            RefreshSpawnQueueUi();
        }

        ApplyDungeonTimeScale();
        RefreshTimeControlUi();
        HideDebugPopup();
        RefreshBoardSize();
    }

    public void Close()
    {
        RestoreDefaultTimeScale();
        UnbindControlButtons();
        HideDebugPopup();
        gameObject.SetActive(false);
    }

    public void Init()
    {
        if (_initialized)
            return;

        if (board == null)
        {
            Debug.LogError("DungeonPage requires a scene reference to DungeonBoardView.", this);
            return;
        }

        board.Initialize(initialGridSize, maximumStackSize);
        InitializePlayerCharacters();

        if (spawnQueueView == null || !spawnQueueView.Initialize())
            Debug.LogError("DungeonPage requires a configured enemy spawn queue view.", this);

        if (speedButton == null || speedText == null || pauseButton == null ||
            pauseText == null || pauseOverlay == null)
        {
            Debug.LogError("DungeonPage requires configured speed and pause controls.", this);
        }

        HideDebugPopup();
        ResetGameTimeState();
        BindControlButtons();
        _initialized = true;
        ResetEnemySpawnQueue();
        if (gameObject.activeInHierarchy)
            ApplyDungeonTimeScale();

        RefreshTimeControlUi();
        RefreshBoardSize();
    }

    private void InitializePlayerCharacters()
    {
        EnsurePlayerCharacterSlots();
        bool hasCharacter = false;
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null)
                continue;

            hasCharacter = true;
            if (!character.Initialize())
            {
                Debug.LogError(
                    $"Player party slot {index + 1} is not configured.",
                    character);
            }
        }

        if (!hasCharacter)
            Debug.LogError("DungeonPage requires at least one player character.", this);
    }

    private void ResetPlayerCharacters()
    {
        EnsurePlayerCharacterSlots();
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            if (playerCharacters[index] != null)
                playerCharacters[index].ResetRuntime();
        }
    }

    private void EnsurePlayerCharacterSlots()
    {
        if (playerCharacters == null)
            playerCharacters = new CharacterRuntime[MaximumPartySize];
        else if (playerCharacters.Length != MaximumPartySize)
            System.Array.Resize(ref playerCharacters, MaximumPartySize);
    }

    /// <summary>
    /// Changes the board between 3x3 and 9x9. Existing stacks whose coordinates
    /// still fit in the resized board are preserved.
    /// </summary>
    public void SetGridSize(int size)
    {
        if (!TryPrepareBoard())
            return;

        board.SetGridSize(size);
        initialGridSize = board.GridSize;
        _boardFull = false;
        RefreshSpawnQueueTimer();
    }

    /// <summary>
    /// Adds one enemy tile to a zero-based board coordinate.
    /// Health is clamped to a minimum value of one.
    /// </summary>
    public bool AddEnemyCard(int row, int column, int health = 1)
    {
        return TryPrepareBoard() && board.TryAddEnemyCard(row, column, health);
    }

    public bool AddEnemyCardToRandomTile(int health = 1)
    {
        return TryPrepareBoard() && board.TryAddEnemyCardToRandomTile(health);
    }

    public bool AddEnemyCardToNextAvailableTile(int health = 1)
    {
        return TryPrepareBoard() && board.TryAddEnemyCardToNextAvailableTile(health);
    }

    public bool QueueEnemy(int health = 1)
    {
        if (!_initialized)
            Init();

        if (!_initialized ||
            _spawnedEnemyCount + _spawnQueue.Count >= maximumEnemiesPerRound)
        {
            return false;
        }

        bool wasEmpty = _spawnQueue.Count == 0;
        _spawnQueue.Add(new DungeonEnemyData(health));

        if (wasEmpty)
            _spawnTimeRemaining = enemySpawnInterval;

        RefreshSpawnQueueUi();
        return true;
    }

    public bool RemoveTopEnemyCard(int row, int column)
    {
        bool removed = TryPrepareBoard() && board.TryRemoveTopEnemyCard(row, column);
        if (removed)
        {
            _boardFull = false;
            RefreshSpawnQueueTimer();
        }

        return removed;
    }

    public int GetStackCount(int row, int column)
    {
        return TryPrepareBoard() ? board.GetStackCount(row, column) : 0;
    }

    /// <summary>
    /// Returns zero when the selected tile has no enemy.
    /// </summary>
    public int GetTopEnemyHealth(int row, int column)
    {
        return TryPrepareBoard() ? board.GetTopEnemyHealth(row, column) : 0;
    }

    public bool SetTopEnemyHealth(int row, int column, int health)
    {
        return TryPrepareBoard() && board.TrySetTopEnemyHealth(row, column, health);
    }

    public void ClearBoard()
    {
        if (TryPrepareBoard())
        {
            board.ClearAllStacks();
            _boardFull = false;
            RefreshSpawnQueueTimer();
        }
    }

    [ContextMenu("Debug/Spawn Next Enemy")]
    private void DebugSpawnNextEnemy()
    {
        if (Application.isPlaying)
            SpawnNextQueuedEnemyImmediately();
    }

    private void BindControlButtons()
    {
        if (!_speedButtonBound && speedButton != null)
        {
            speedButton.onClick.AddListener(CycleGameSpeed);
            _speedButtonBound = true;
        }

        if (!_pauseButtonBound && pauseButton != null)
        {
            pauseButton.onClick.AddListener(ToggleGamePause);
            _pauseButtonBound = true;
        }

        if (!_debugButtonBound && debugButton != null)
        {
            debugButton.onClick.AddListener(ToggleDebugPopup);
            _debugButtonBound = true;
        }

        if (!_spawnButtonBound && spawnEnemyButton != null)
        {
            spawnEnemyButton.onClick.AddListener(SpawnNextQueuedEnemyImmediately);
            _spawnButtonBound = true;
        }
    }

    private void UnbindControlButtons()
    {
        if (_speedButtonBound && speedButton != null)
        {
            speedButton.onClick.RemoveListener(CycleGameSpeed);
            _speedButtonBound = false;
        }

        if (_pauseButtonBound && pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(ToggleGamePause);
            _pauseButtonBound = false;
        }

        if (_debugButtonBound && debugButton != null)
        {
            debugButton.onClick.RemoveListener(ToggleDebugPopup);
            _debugButtonBound = false;
        }

        if (_spawnButtonBound && spawnEnemyButton != null)
        {
            spawnEnemyButton.onClick.RemoveListener(SpawnNextQueuedEnemyImmediately);
            _spawnButtonBound = false;
        }
    }

    private void CycleGameSpeed()
    {
        _gameSpeedIndex = (_gameSpeedIndex + 1) % GameSpeedScales.Length;
        ApplyDungeonTimeScale();
        RefreshTimeControlUi();
    }

    private void ToggleGamePause()
    {
        _isGamePaused = !_isGamePaused;
        ApplyDungeonTimeScale();
        RefreshTimeControlUi();
    }

    private void ResetGameTimeState()
    {
        _gameSpeedIndex = 0;
        _isGamePaused = false;
    }

    private void ApplyDungeonTimeScale()
    {
        Time.timeScale = _isGamePaused ? 0f : GameSpeedScales[_gameSpeedIndex];
        _controlsGameTime = true;
    }

    private void RestoreDefaultTimeScale()
    {
        if (!_controlsGameTime)
            return;

        Time.timeScale = DefaultGameSpeed;
        _controlsGameTime = false;
    }

    private void RefreshTimeControlUi()
    {
        if (speedText != null)
            speedText.text = $"{GameSpeedScales[_gameSpeedIndex]:0.#}X";

        if (pauseText != null)
            pauseText.text = _isGamePaused ? "RESUME" : "PAUSE";

        if (pauseOverlay != null)
            pauseOverlay.SetActive(_isGamePaused);
    }

    private void ToggleDebugPopup()
    {
        if (debugPopup != null)
            debugPopup.SetActive(!debugPopup.activeSelf);
    }

    private void HideDebugPopup()
    {
        if (debugPopup != null)
            debugPopup.SetActive(false);
    }

    private void SpawnNextQueuedEnemyImmediately()
    {
        if (!_initialized)
            Init();

        if (_initialized)
        {
            _spawnTimeRemaining = 0f;
            TrySpawnNextQueuedEnemy();
        }
    }

    private void TickEnemySpawnQueue(float deltaTime)
    {
        if (_spawnQueue.Count == 0)
        {
            RefreshSpawnQueueTimer();
            return;
        }

        _spawnTimeRemaining = Mathf.Max(0f, _spawnTimeRemaining - Mathf.Max(0f, deltaTime));
        if (_spawnTimeRemaining > 0f)
        {
            RefreshSpawnQueueTimer();
            return;
        }

        TrySpawnNextQueuedEnemy();
    }

    private bool TrySpawnNextQueuedEnemy()
    {
        if (!TryPlaceNextQueuedEnemy())
        {
            RefreshSpawnQueueTimer();
            return false;
        }

        _spawnTimeRemaining = _spawnQueue.Count > 0 ? enemySpawnInterval : 0f;
        RefreshSpawnQueueUi();
        return true;
    }

    private bool TryPlaceNextQueuedEnemy()
    {
        if (_spawnQueue.Count == 0)
            return false;

        DungeonEnemyData nextEnemy = _spawnQueue[0];
        if (board == null || !board.TryAddEnemyCardToNextAvailableTile(nextEnemy))
        {
            _boardFull = true;
            return false;
        }

        _spawnQueue.RemoveAt(0);
        _spawnedEnemyCount++;
        _boardFull = false;
        return true;
    }

    private void ResetEnemySpawnQueue()
    {
        _spawnQueue.Clear();
        _spawnedEnemyCount = 0;
        FillRoundSpawnQueue();
        FillInitialBoard();
        _spawnTimeRemaining = _spawnQueue.Count > 0 ? enemySpawnInterval : 0f;
        _boardFull = false;
        RefreshSpawnQueueUi();
    }

    private void FillInitialBoard()
    {
        if (board == null)
            return;

        int targetCount = Mathf.Min(
            board.GridSize * board.GridSize,
            _spawnQueue.Count);

        for (int index = 0; index < targetCount; index++)
        {
            if (!TryPlaceNextQueuedEnemy())
                break;
        }
    }

    private void FillRoundSpawnQueue()
    {
        int targetCount = Mathf.Max(
            0,
            maximumEnemiesPerRound - _spawnedEnemyCount);

        while (_spawnQueue.Count < targetCount)
            _spawnQueue.Add(new DungeonEnemyData(Random.Range(1, 10)));
    }

    private void TrimSpawnQueueToRoundLimit()
    {
        int maximumPendingCount = Mathf.Max(
            0,
            maximumEnemiesPerRound - _spawnedEnemyCount);
        if (_spawnQueue.Count > maximumPendingCount)
        {
            _spawnQueue.RemoveRange(
                maximumPendingCount,
                _spawnQueue.Count - maximumPendingCount);
        }

        if (_spawnQueue.Count == 0)
            _spawnTimeRemaining = 0f;
    }

    private void RefreshSpawnQueueUi()
    {
        if (spawnQueueView == null)
            return;

        spawnQueueView.RefreshQueue(_spawnQueue);
        RefreshSpawnQueueTimer();
    }

    private void RefreshSpawnQueueTimer()
    {
        if (spawnQueueView != null)
        {
            spawnQueueView.RefreshTimer(
                _spawnTimeRemaining,
                enemySpawnInterval,
                _spawnQueue.Count,
                _boardFull);
        }
    }

    private bool TryPrepareBoard()
    {
        if (!_initialized)
            Init();

        return _initialized && board != null;
    }

    private void RefreshBoardSize()
    {
        if (board == null || transform is not RectTransform pageRect)
            return;

        float availableWidth = pageRect.rect.width * boardWidthRatio;
        float availableHeight = pageRect.rect.height * boardHeightRatio;
        float boardSize = Mathf.Min(maximumBoardSize, availableWidth, availableHeight);

        if (boardSize > 0f)
            board.SetPixelSize(boardSize);
    }
}
