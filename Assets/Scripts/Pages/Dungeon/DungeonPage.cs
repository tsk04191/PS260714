using System.Collections.Generic;
using UnityEngine;

public class DungeonPage : MonoBehaviour, IPage
{
    public const int MaximumPartySize = 4;

    [Header("Dungeon Board")]
    [SerializeField, Range(DungeonBoardView.MinimumGridSize, DungeonBoardView.MaximumGridSize)]
    private int initialGridSize = DungeonBoardView.MinimumGridSize;

    [SerializeField, Range(1, 20)] private int maximumStackSize = 8;
    [SerializeField, Range(0.4f, 0.95f)] private float boardWidthRatio = 0.72f;
    [SerializeField, Range(0.4f, 0.95f)] private float boardHeightRatio = 0.78f;
    [SerializeField, Min(100f)] private float maximumBoardSize = 760f;
    [SerializeField] private DungeonBoardView board;

    [Header("Dungeon Flow")]
    [SerializeField] private DungeonFlowController flowController;
    [SerializeField] private DungeonBattleTab battleTab;

    [Header("Player Party")]
    [SerializeField] private CharacterRuntime[] playerCharacters =
        new CharacterRuntime[MaximumPartySize];

    [Header("Enemy Spawn Queue")]
    [SerializeField, Min(1)] private int maximumEnemiesPerRound = 20;
    [SerializeField, Min(0.1f)] private float enemySpawnInterval = 4f;

    private bool _initialized;
    private bool _flowEventsBound;
    private bool _battleCompletionBound;
    private BattleManager _battleManager;

    public AudioSource Speaker { get; set; }
    public EDungeonPhase CurrentPhase => flowController != null
        ? flowController.CurrentPhase
        : EDungeonPhase.Battle;
    public int GridSize => board != null ? board.GridSize : initialGridSize;
    public int PendingEnemyCount => _battleManager != null
        ? _battleManager.PendingEnemyCount
        : 0;
    public int SpawnedEnemyCount => _battleManager != null
        ? _battleManager.SpawnedEnemyCount
        : 0;
    public int RemainingEnemySpawnCount => _battleManager != null
        ? _battleManager.RemainingEnemySpawnCount
        : maximumEnemiesPerRound;

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        RefreshBoardSize();

        if (_initialized && CurrentPhase == EDungeonPhase.Battle &&
            TryResolveBattleManager() && !_battleManager.HasSession)
        {
            StartNewBattle();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshBoardSize();
    }

    private void OnDisable()
    {
        _battleManager?.SuspendBattle();
    }

    private void OnDestroy()
    {
        UnbindFlowEvents();
        battleTab?.Teardown();

        if (_battleManager != null)
        {
            BattleManager manager = _battleManager;
            UnbindBattleCompletion();
            manager.EndBattle(board);
        }
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

        if (Application.isPlaying && _initialized && board != null)
        {
            board.SetGridSize(initialGridSize);
            _battleManager?.NotifyBoardChanged();
            battleTab?.Refresh();
            RefreshBoardSize();
        }
    }

    public void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        gameObject.SetActive(true);

        if (!_initialized)
            Init();

        if (mode == PageOpenMode.Fresh)
            StartNewBattle();

        if (flowController != null)
        {
            if (mode == PageOpenMode.Fresh)
                flowController.ResetFlow();
            else
                flowController.RefreshCurrentPhase();
        }

        if (mode == PageOpenMode.Resume && CurrentPhase == EDungeonPhase.Battle)
        {
            if (TryResolveBattleManager() && !_battleManager.ResumeBattle() &&
                !_battleManager.HasSession)
            {
                StartNewBattle();
            }
        }

        battleTab?.HideDebugPopup();
        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public void Close()
    {
        _battleManager?.SuspendBattle();
        battleTab?.HideDebugPopup();
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

        if (flowController == null || !flowController.Initialize())
        {
            Debug.LogError("DungeonPage requires a configured dungeon flow controller.", this);
        }
        else
        {
            BindFlowEvents();
        }

        if (!TryResolveBattleManager())
        {
            Debug.LogError(
                "DungeonPage requires a configured DungeonBattleTab and GameManager.Battle.",
                this);
        }

        battleTab?.HideDebugPopup();
        _initialized = true;
        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public bool AdvanceDungeonPhase()
    {
        return flowController != null && flowController.TryAdvance();
    }

    public void SetGridSize(int size)
    {
        if (!TryPrepareBoard())
            return;

        board.SetGridSize(size);
        initialGridSize = board.GridSize;
        _battleManager?.NotifyBoardChanged();
    }

    public bool AddEnemyCard(int row, int column, int health = 1)
    {
        bool added = TryPrepareBoard() && board.TryAddEnemyCard(row, column, health);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool AddEnemyCardToRandomTile(int health = 1)
    {
        bool added = TryPrepareBoard() && board.TryAddEnemyCardToRandomTile(health);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool AddEnemyCardToNextAvailableTile(int health = 1)
    {
        bool added = TryPrepareBoard() &&
                     board.TryAddEnemyCardToNextAvailableTile(health);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool QueueEnemy(int health = 1)
    {
        return TryResolveBattleManager() && _battleManager.QueueEnemy(health);
    }

    public bool RemoveTopEnemyCard(int row, int column)
    {
        bool removed = TryPrepareBoard() && board.TryRemoveTopEnemyCard(row, column);
        if (removed)
            _battleManager?.NotifyBoardChanged();

        return removed;
    }

    public int GetStackCount(int row, int column)
    {
        return TryPrepareBoard() ? board.GetStackCount(row, column) : 0;
    }

    public int GetTopEnemyHealth(int row, int column)
    {
        return TryPrepareBoard() ? board.GetTopEnemyHealth(row, column) : 0;
    }

    public bool SetTopEnemyHealth(int row, int column, int health)
    {
        bool changed = TryPrepareBoard() &&
                       board.TrySetTopEnemyHealth(row, column, health);
        if (changed)
            _battleManager?.NotifyBoardChanged();

        return changed;
    }

    public void ClearBoard()
    {
        if (!TryPrepareBoard())
            return;

        board.ClearAllStacks();
        _battleManager?.NotifyBoardChanged();
    }

    [ContextMenu("Debug/Spawn Next Enemy")]
    private void DebugSpawnNextEnemy()
    {
        if (Application.isPlaying)
            _battleManager?.SpawnNextEnemyImmediately();
    }

    private bool StartNewBattle()
    {
        if (!TryResolveBattleManager() || !_battleManager.IsInitialized)
            return false;

        List<IBattleCharacter> characters = new(MaximumPartySize);
        foreach (CharacterRuntime character in playerCharacters)
        {
            if (character != null)
                characters.Add(character);
        }

        List<DungeonEnemyData> enemies = new(maximumEnemiesPerRound);
        for (int index = 0; index < maximumEnemiesPerRound; index++)
            enemies.Add(new DungeonEnemyData(Random.Range(1, 10)));

        return _battleManager.StartBattle(
            board,
            characters,
            enemies,
            enemySpawnInterval);
    }

    private bool TryResolveBattleManager()
    {
        if (_battleManager == null)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                manager = FindFirstObjectByType<GameManager>();

            _battleManager = manager != null ? manager.Battle : null;
        }

        if (_battleManager == null)
            return false;

        BindBattleCompletion();
        return battleTab != null && battleTab.Initialize(_battleManager);
    }

    private void BindFlowEvents()
    {
        if (_flowEventsBound || flowController == null)
            return;

        flowController.PhaseChanged += HandleDungeonPhaseChanged;
        _flowEventsBound = true;
    }

    private void UnbindFlowEvents()
    {
        if (!_flowEventsBound || flowController == null)
            return;

        flowController.PhaseChanged -= HandleDungeonPhaseChanged;
        _flowEventsBound = false;
    }

    private void HandleDungeonPhaseChanged(EDungeonPhase phase, int _)
    {
        if (!TryResolveBattleManager())
            return;

        if (phase == EDungeonPhase.Battle)
        {
            if (_battleManager.State == EBattleState.Completed)
                StartNewBattle();
            else if (!_battleManager.HasSession)
                StartNewBattle();
            else
                _battleManager.ResumeBattle();
        }
        else
        {
            _battleManager.SuspendBattle();
            battleTab?.HideDebugPopup();
        }

        battleTab?.Refresh();
    }

    private void BindBattleCompletion()
    {
        if (_battleCompletionBound || _battleManager == null)
            return;

        _battleManager.BattleCompleted += HandleBattleCompleted;
        _battleCompletionBound = true;
    }

    private void UnbindBattleCompletion()
    {
        if (!_battleCompletionBound || _battleManager == null)
            return;

        _battleManager.BattleCompleted -= HandleBattleCompleted;
        _battleCompletionBound = false;
    }

    private void HandleBattleCompleted()
    {
        battleTab?.Refresh();
        if (CurrentPhase == EDungeonPhase.Battle && flowController != null &&
            !flowController.IsCompleted)
        {
            flowController.TryAdvance();
        }
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

    private void EnsurePlayerCharacterSlots()
    {
        if (playerCharacters == null)
            playerCharacters = new CharacterRuntime[MaximumPartySize];
        else if (playerCharacters.Length != MaximumPartySize)
            System.Array.Resize(ref playerCharacters, MaximumPartySize);
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
