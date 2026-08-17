using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EDungeonRunResult
{
    None,
    Clear,
    Defeat,
}
public enum EDungeonEnergyUpgradeType
{
    MaximumEnergy,
    RechargeSpeed,
}

public readonly struct DungeonBattlePlan
{
    public int DifficultyScale { get; }
    public int RandomSeed { get; }

    public DungeonBattlePlan(int difficultyScale, int randomSeed)
    {
        DifficultyScale = Mathf.Clamp(difficultyScale, 0, 100);
        RandomSeed = randomSeed;
    }
}

public class DungeonPage : MonoBehaviour, IPage
{
    public const int MaximumPartySize = 4;
    public const int MinimumBattleCount = 5;
    public const int MaximumBattleCount = 8;
    public const int StartingCharacterChoiceCount = 3;
    public const float EnergyRechargeUpgradeAmount = 0.5f;
    public const float MinimumEnergyRechargeDuration = 0.5f;

    private const int TutorialGridSize = 3;
    private const int TutorialEnemyCount = 12;
    private const int TutorialInitialEnemyCount =
        TutorialGridSize * TutorialGridSize;
    private const float TutorialSpawnInterval = 1f;
    private const float TutorialTimeLimit = 45f;
    private const float TutorialTargetAutoClearDuration = 34f;
    private const float TutorialDamageBudgetRatio = 0.9f;
    private const int StartingChoiceSeedSalt = unchecked((int)0x5A17C0DE);
    private const int StartingItemSeedSalt = unchecked((int)0x1E7A51A9);
    private static readonly Color[] DefaultPartySlotColors =
    {
        new Color32(0x45, 0xB7, 0xFF, 0xFF),
        new Color32(0xFF, 0xB5, 0x47, 0xFF),
        new Color32(0xE6, 0x6B, 0xFF, 0xFF),
        new Color32(0x72, 0xE5, 0x8A, 0xFF),
    };

    private static readonly EEnemyType[] FallbackNormalEnemyTypes =
    {
        EEnemyType.Basic,
        EEnemyType.Assault,
        EEnemyType.Heavy,
        EEnemyType.Medic,
        EEnemyType.Mechanic,
    };

    [Header("Dungeon Battle World")]
    [SerializeField, Range(DungeonBoardView.MinimumGridSize, DungeonBoardView.MaximumGridSize)]
    private int initialGridSize = DungeonBoardView.MinimumGridSize;

    [SerializeField, Range(1, 20)] private int maximumStackSize = 8;
    [SerializeField] private DungeonBoardView board;

    [Header("Dungeon Flow")]
    [SerializeField] private DungeonFlowController flowController;
    [SerializeField] private DungeonBattleTab battleTab;

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject stageSelectPage;

    [Header("Player Party")]
    [SerializeField] private CharacterRuntime characterInfoPrefab;
    [SerializeField] private RectTransform playerCharacterRoot;
    [SerializeField] private CharacterRuntime[] playerCharacters =
        new CharacterRuntime[MaximumPartySize];
    [SerializeField, ColorUsage(false, false)]
    private Color[] partySlotColors = new Color[MaximumPartySize];

    [Header("Dynamic Dungeon UI Prefabs")]
    [SerializeField] private DungeonRewardCardView rewardCardPrefab;
    [SerializeField]
    private DungeonDynamicChoiceButtonView choiceButtonPrefab;
    [SerializeField]
    private DungeonStartingItemSlotView startingItemSlotPrefab;
    [SerializeField] private Image restCharacterSdPrefab;

    [Header("First Battle")]
    [SerializeField, HideInInspector] private BattleSO firstBattle;

    [Header("Enemy Spawn Queue")]
    [SerializeField] private EnemySO defaultEnemy;
    [SerializeField] private EnemySO[] normalEnemyPool = new EnemySO[0];
    [SerializeField, Min(1)] private int minimumEnemyHealth = 20;
    [SerializeField, Min(1)] private int maximumEnemiesPerRound = 20;
    [SerializeField, Min(0.1f)] private float enemySpawnInterval = 4f;
    [SerializeField, Min(1f)] private float normalBattleTimeLimit = 180f;

    [Header("Runtime Difficulty Scale")]
    [SerializeField, Min(1)] private int baselineEnemyCount = 16;
    [SerializeField, Min(1)] private int maximumScaledEnemyCount = 28;
    [SerializeField, Min(1f)] private float maximumHealthMultiplier = 3f;
    [SerializeField, Min(0)] private int baselineEnemyHealthVariance = 2;
    [SerializeField, Min(0)] private int maximumEnemyHealthVariance = 3;
    [SerializeField, Min(0.1f)] private float minimumScaledSpawnInterval = 2.5f;
    [SerializeField, Range(0, 25)] private int difficultyScaleJitter = 8;
    [SerializeField, Range(0.5f, 1f)]
    private float baselineSoloDamageBudgetRatio = 0.93f;

    private bool _initialized;
    private bool _characterInfoInstancesPrepared;
    private bool _flowEventsBound;
    private bool _battleEventsBound;
    private bool _battleRewardPending;
    private bool _startingCharacterSelectionPending;
    private bool _startingItemSelectionPending;
    private BattleManager _battleManager;
    private DungeonEventTab _eventTab;
    private DungeonEventTab _battleRewardOverlay;
    [SerializeField] private GameObject _battleRewardOverlayRoot;
    private DungeonRoomView _eventRoomView;
    private DungeonRoomView _restRoomView;
    private DungeonRoomView _shopRoomView;
    [SerializeField] private DungeonTutorialController _tutorialController;
    [SerializeField] private DungeonFieldView fieldView;
    private DungeonDefinition _pendingDefinition;
    private readonly DungeonRunSession _session = new();
    private DungeonRuntimeContext _runtimeContext;
    private CharacterSO _startingTurret;
    private readonly List<CharacterRuntime> _ownedTurrets = new();
    private readonly List<CharacterSO> _availableTurrets = new();
    private readonly HashSet<string> _acquiredCharacterIds =
        new(StringComparer.Ordinal);
    private readonly List<CharacterSO> _startingCharacterChoices = new();
    private readonly DungeonStartingItemSelectionState
        _startingItemSelection = new();
    private readonly CharacterSO[] _slotDefaultDefinitions =
        new CharacterSO[MaximumPartySize];
    private DungeonBattlePlan[] _battlePlans = Array.Empty<DungeonBattlePlan>();
    private readonly List<EnemySO> _fallbackEnemyPool = new();
    private readonly Dictionary<string, BattleItemRunState> _battleItems =
        new(StringComparer.Ordinal);
    private readonly BattleCardDeckRuntime _battleCardDeck = new();
    private readonly List<BattleCardSO> _acquiredBattleCards = new();
    private int _maximumEnergy = BattleManager.DefaultMaximumEnergy;
    private float _energyRechargeDuration =
        DungeonDefinition.DefaultActiveSkillCostRecoveryDuration;
    private int _dungeonShieldCurrentHealth;
    private int _dungeonShieldMaximumHealth;

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
    public int TotalBattleCount => _session.TotalBattleCount;
    public int CurrentBattleNumber => _session.CurrentBattleNumber;
    public int CurrentDifficultyScale => GetBattleDifficultyScale(
        _session.CurrentBattleNumber);
    public EDungeonRunResult RunResult => _session.Result;
    public DungeonRunSession RunSession => _session;
    public DungeonDefinition CurrentDungeon => _session.Definition;
    public IReadOnlyList<CharacterRuntime> OwnedTurrets => _ownedTurrets;
    public bool UsesBattleCards =>
        _session.Definition?.UseBattleCards == true;
    public BattleCardDeckRuntime BattleCardDeck => _battleCardDeck;
    public IReadOnlyList<BattleCardSO> AcquiredBattleCards =>
        _acquiredBattleCards;
    public IReadOnlyList<CharacterSO> AvailableTurrets => _availableTurrets;
    public IReadOnlyList<CharacterSO> StartingCharacterChoices =>
        _startingCharacterChoices;
    public IReadOnlyList<BattleItemSO> StartingItemChoices =>
        _startingItemSelection.Items;
    internal Button PreparationNavigationButtonTemplate =>
        battleTab != null ? battleTab.PauseButtonTemplate : null;
    internal Image RestCharacterSdPrefab => restCharacterSdPrefab;
    public IReadOnlyList<DungeonBattlePlan> BattlePlans => _battlePlans;
    public DungeonBoardView Board => board;
    public int MaximumEnergy => _maximumEnergy;
    public float EnergyRechargeDuration => _energyRechargeDuration;
    public int DungeonShieldCurrentHealth =>
        Mathf.Clamp(
            _dungeonShieldCurrentHealth,
            0,
            Mathf.Max(0, _dungeonShieldMaximumHealth));
    public int DungeonShieldMaximumHealth =>
        Mathf.Max(0, _dungeonShieldMaximumHealth);
    public bool IsStartingCharacterSelectionPending =>
        _startingCharacterSelectionPending;
    public bool IsStartingItemSelectionPending =>
        _startingItemSelectionPending;
    public bool IsTutorialBattle =>
        _session.IsActive && _session.Definition != null &&
        _session.Definition.HasTutorial;
    internal DungeonRewardCardView RewardCardPrefab => rewardCardPrefab;
    internal DungeonDynamicChoiceButtonView ChoiceButtonPrefab =>
        choiceButtonPrefab;
    internal DungeonStartingItemSlotView StartingItemSlotPrefab =>
        startingItemSlotPrefab;

    public IReadOnlyList<EnemySO> GetCodexEnemyDefinitions()
    {
        return GetBattleEnemyPool();
    }

    public IReadOnlyList<CharacterSO> GetCodexCharacterDefinitions()
    {
        List<CharacterSO> definitions = new();
        HashSet<CharacterSO> uniqueDefinitions = new();
        foreach (CharacterSO definition in CharacterDefinitionCatalog.GetAll())
        {
            if (definition != null && uniqueDefinitions.Add(definition))
                definitions.Add(definition);
        }

        if (playerCharacters != null)
        {
            foreach (CharacterRuntime character in playerCharacters)
            {
                CharacterSO definition = character != null
                    ? character.Definition
                    : null;
                if (definition != null && uniqueDefinitions.Add(definition))
                    definitions.Add(definition);
            }
        }

        foreach (CharacterSO definition in _availableTurrets)
        {
            if (definition != null && uniqueDefinitions.Add(definition))
                definitions.Add(definition);
        }

        return definitions.AsReadOnly();
    }

    public event Action<EDungeonRunResult> RunEnded;
    public event Action BattleItemsChanged;
    public event Action BattleCardsChanged
    {
        add => _battleCardDeck.Changed += value;
        remove => _battleCardDeck.Changed -= value;
    }

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        RefreshBoardSize();
        if (_initialized && !_session.IsActive &&
            _session.Result == EDungeonRunResult.None)
        {
            StartNewDungeonRun();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshBoardSize();
    }

    private void Update()
    {
        if (_session.IsActive &&
            _battleManager != null &&
            _battleManager.State == EBattleState.Running)
        {
            TickBattleItemCooldowns(Time.deltaTime);
            if (UsesBattleCards)
                _battleCardDeck.Tick(Time.deltaTime);
        }

        if (!_session.IsActive || _session.Definition == null ||
            _session.Definition.Modifiers.Count == 0 ||
            _battleManager == null ||
            _battleManager.State != EBattleState.Running)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        ForEachModifier(modifier =>
            modifier.OnRunTick(GetRuntimeContext(), deltaTime));
    }

    private void OnDisable()
    {
        if (!_session.IsActive)
            return;

        _session.Pause.Add(EDungeonPauseReason.PageHidden);
        ApplyBattlePauseState();
    }

    private void OnDestroy()
    {
        UnbindFlowEvents();
        battleTab?.Teardown();
        _eventTab?.Teardown();
        _battleRewardOverlay?.Teardown();
        _eventRoomView?.Teardown();
        _restRoomView?.Teardown();
        _shopRoomView?.Teardown();

        if (_battleManager != null)
        {
            BattleManager manager = _battleManager;
            UnbindBattleEvents();
            manager.EndBattle(board);
        }

        ReleaseFallbackEnemyDefinitions();
    }

    private void OnValidate()
    {
        initialGridSize = Mathf.Clamp(
            initialGridSize,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        maximumStackSize = Mathf.Max(1, maximumStackSize);
        minimumEnemyHealth = Mathf.Max(1, minimumEnemyHealth);
        maximumEnemiesPerRound = Mathf.Max(1, maximumEnemiesPerRound);
        enemySpawnInterval =
            TimePrecision.Normalize(enemySpawnInterval, 0.1f);
        normalBattleTimeLimit =
            TimePrecision.Normalize(normalBattleTimeLimit, 1f);
        baselineEnemyCount = Mathf.Max(1, baselineEnemyCount);
        maximumScaledEnemyCount = Mathf.Max(
            baselineEnemyCount,
            maximumScaledEnemyCount);
        maximumHealthMultiplier = Mathf.Max(1f, maximumHealthMultiplier);
        baselineEnemyHealthVariance = Mathf.Max(
            0,
            baselineEnemyHealthVariance);
        maximumEnemyHealthVariance = Mathf.Max(
            baselineEnemyHealthVariance,
            maximumEnemyHealthVariance);
        minimumScaledSpawnInterval = TimePrecision.Normalize(
            minimumScaledSpawnInterval,
            0.1f);
        difficultyScaleJitter = Mathf.Clamp(difficultyScaleJitter, 0, 25);
        baselineSoloDamageBudgetRatio = Mathf.Clamp(
            baselineSoloDamageBudgetRatio,
            0.5f,
            1f);
        EnsurePlayerCharacterSlots();
        EnsurePartySlotColors();

        if (Application.isPlaying && _initialized && board != null)
        {
            ApplyPlayerCharacterSlotColors();
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
        {
            DungeonDefinition definition = _pendingDefinition ??
                DungeonDefinitionCatalog.Get(
                    DungeonDefinitionCatalog.FreeBattleId);
            _pendingDefinition = null;
            StartNewRun(definition);
        }
        else if (_session.Result != EDungeonRunResult.None)
        {
            if (_session.Definition != null &&
                _session.Definition.HasTutorial &&
                _session.Result == EDungeonRunResult.Clear)
            {
                flowController?.RefreshCurrentPhaseView();
                _tutorialController?.ShowCompletion();
            }
            else
            {
                flowController?.ShowEventTab();
                _eventTab?.ShowRunResult(_session.Result);
            }
        }
        else if (_startingItemSelectionPending)
        {
            flowController?.ShowEventTab();
            _eventTab?.ShowStartingItemSelection();
        }
        else if (_startingCharacterSelectionPending)
        {
            flowController?.ShowEventTab();
            _eventTab?.ShowStartingCharacterSelection(
                _startingCharacterChoices);
            if (_session.Definition != null &&
                _session.Definition.HasTutorial)
            {
                _tutorialController?.BeginStartingChoice(
                    _session.Definition.Tutorial,
                    _eventTab?.FirstStartingChoiceRect);
            }
        }
        else
        {
            flowController?.RefreshCurrentPhaseView();
            if (CurrentPhase == EDungeonPhase.Battle &&
                TryResolveBattleManager())
            {
                if (!_battleManager.HasSession && _session.IsActive)
                    StartNewBattle();
            }
        }

        _session.Pause.Remove(EDungeonPauseReason.PageHidden);
        ApplyBattlePauseState();

        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public void Close()
    {
        if (_session.IsActive)
        {
            if (_battleManager != null && _battleManager.HasSession)
            {
                _session.SetPreferredGameSpeed(
                    _battleManager.GameSpeed);
            }
            _session.Pause.Add(EDungeonPauseReason.PageHidden);
            ApplyBattlePauseState();
        }
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

        EnsureCharacterInfoInstances();
        board.BindCardDrawService(_battleCardDeck);
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

        InitializeEventTab();
        InitializeBattleRewardOverlay();
        InitializeRoomViews();
        EnsureFieldView();
        EnsureTutorialController();

        _initialized = true;
        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public bool AdvanceDungeonPhase()
    {
        if (_startingCharacterSelectionPending ||
            _startingItemSelectionPending ||
            _battleRewardPending)
        {
            return false;
        }

        return flowController != null && flowController.TryAdvance();
    }

    public void StartNewDungeonRun()
    {
        DungeonDefinition definition =
            _session.Definition != null &&
            !_session.Definition.HasTutorial
                ? _session.Definition
                : DungeonDefinitionCatalog.Get(
                    DungeonDefinitionCatalog.FreeBattleId);
        StartNewRun(definition);
    }

    public void PrepareDungeon(DungeonDefinition definition)
    {
        _pendingDefinition = definition ?? DungeonDefinitionCatalog.Get(
            DungeonDefinitionCatalog.FreeBattleId);
    }

    public void PrepareTutorialStage()
    {
        PrepareDungeon(DungeonDefinitionCatalog.Get(
            DungeonDefinitionCatalog.TestFieldId));
    }

    public void PrepareFreeBattle()
    {
        PrepareDungeon(DungeonDefinitionCatalog.Get(
            DungeonDefinitionCatalog.FreeBattleId));
    }

    public bool BeginTutorialBattle()
    {
        if (_session.Definition == null ||
            !_session.Definition.HasTutorial || !_session.IsActive ||
            !TryResolveBattleManager() || !_battleManager.HasSession)
        {
            return false;
        }

        _session.SetTutorialStep(-1);
        _session.SetActivity(EDungeonRunActivity.Battle);
        _session.Pause.Remove(EDungeonPauseReason.TutorialGuide);
        ApplyBattlePauseState();
        battleTab?.Refresh();
        return _battleManager.State == EBattleState.Running;
    }

    public void UpdateTutorialProgress(int stepIndex)
    {
        if (_session.Definition != null &&
            _session.Definition.HasTutorial)
        {
            _session.SetTutorialStep(stepIndex);
        }
    }

    public void CompleteTutorialStage()
    {
        if (_session.Definition == null ||
            !_session.Definition.HasTutorial)
        {
            return;
        }

        _tutorialController?.StopTutorial();
        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        EDungeonCompletionDestination destination =
            _session.Definition.CompletionDestination;

        if (_battleManager != null && _battleManager.HasSession)
            _battleManager.EndBattle(board);
        board?.ClearAllStacks();
        ClearPlayerParty();
        ResetRunResourcesAndItems();
        _session.Reset();
        _pendingDefinition = null;

        NavigateToCompletionDestination(destination);
    }

    private void StartNewRun(DungeonDefinition definition)
    {
        if (!_initialized || flowController == null ||
            !TryResolveBattleManager() || definition == null)
        {
            return;
        }

        RefreshAvailableCharacterDefinitions();
        if (_battleManager.HasSession)
            _battleManager.EndBattle(board);

        ClearPlayerParty();
        ResetRunResourcesAndItems(definition);
        board.ClearAllStacks();
        _tutorialController?.StopTutorial();
        int runSeed = Environment.TickCount ^
                      UnityEngine.Random.Range(0, int.MaxValue);
        int battleCount = definition.ResolveBattleCount(runSeed);
        IReadOnlyList<EDungeonPhase> phases =
            definition.BuildPhaseSequence(battleCount, runSeed);
        _session.Begin(
            definition,
            runSeed,
            battleCount,
            phases,
            definition.InitialRunCurrency);
        _dungeonShieldMaximumHealth =
            definition.BattleShieldMaximumHealth;
        _dungeonShieldCurrentHealth = _dungeonShieldMaximumHealth;
        RequestDungeonBgm(
            definition,
            EDungeonBgmState.Ready);
        fieldView?.ApplyTheme(definition.Theme);
        GenerateBattlePlans(battleCount, runSeed);
        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        NotifyRunStarted();

        if (!PrepareStartingCharacterSelection())
        {
            _session.Finish(EDungeonRunResult.Defeat);
            flowController.ShowEventTab();
            _eventTab?.ShowStartingCharacterConfigurationError(
                _availableTurrets.Count);
            return;
        }

        if (!definition.SelectStartingCharacter &&
            _startingCharacterChoices.Count > 0)
        {
            TrySelectStartingCharacter(_startingCharacterChoices[0]);
        }
    }

    public void ReturnToMain()
    {
        GameObject targetPage = mainPage;
        if (targetPage == null && transform.parent != null)
        {
            Transform targetTransform = transform.parent.Find("pagMain");
            targetPage = targetTransform != null
                ? targetTransform.gameObject
                : null;
        }

        if (targetPage == null)
        {
            Debug.LogError(
                "DungeonPage requires a MainPage navigation target.",
                this);
            return;
        }

        PageControl.PagToPag(
            gameObject,
            targetPage,
            PageOpenMode.Resume);
    }

    public void ReturnFromRunResult()
    {
        EDungeonCompletionDestination destination =
            _session.Definition != null
                ? _session.Definition.CompletionDestination
                : EDungeonCompletionDestination.Main;
        NavigateToCompletionDestination(destination);
    }

    public void ReturnToStageSelect(GameObject sourcePage = null)
    {
        ResetCurrentRunForNavigation();
        NavigateToCompletionDestination(
            EDungeonCompletionDestination.StageSelect,
            sourcePage);
    }

    public void ToggleBattlePause()
    {
        if (!_session.IsActive || _battleManager == null ||
            !_battleManager.HasSession ||
            _session.Activity != EDungeonRunActivity.Battle)
        {
            return;
        }

        if (_session.Pause.IsUserPaused)
            _session.Pause.Remove(EDungeonPauseReason.UserPause);
        else
            _session.Pause.Add(EDungeonPauseReason.UserPause);
        ApplyBattlePauseState();
        battleTab?.Refresh();
    }

    public void RecordBattleSpeed(float speed)
    {
        if (_session.IsActive)
            _session.SetPreferredGameSpeed(speed);
    }

    public bool TrySelectStartingCharacter(CharacterSO definition)
    {
        if (!_startingCharacterSelectionPending || definition == null ||
            !_startingCharacterChoices.Contains(definition) ||
            !IsCharacterOwnedForDungeon(definition) ||
            playerCharacters.Length == 0 || playerCharacters[0] == null)
        {
            return false;
        }

        CharacterRuntime startingSlot = playerCharacters[0];
        if (!startingSlot.ConfigureDefinition(definition))
            return false;
        startingSlot.BeginDungeonRun();

        _startingTurret = definition;
        startingSlot.ConfigurePartySlot(0, partySlotColors[0]);
        startingSlot.gameObject.SetActive(true);
        _ownedTurrets.Clear();
        _ownedTurrets.Add(startingSlot);
        _acquiredCharacterIds.Clear();
        RecordAcquiredCharacter(definition);
        _startingCharacterSelectionPending = false;

        if (_session.Definition != null &&
            !_session.Definition.UseBattleCards &&
            _session.Definition.SelectStartingItems)
        {
            return PrepareStartingItemSelection();
        }

        return BeginPreparedDungeonFlow();
    }

    public int GetStartingItemRerollsRemaining(int slotIndex)
    {
        return _startingItemSelection.GetRerollsRemaining(slotIndex);
    }

    public bool CanRerollStartingItem(int slotIndex)
    {
        return _startingItemSelectionPending &&
               _startingItemSelection.CanReroll(slotIndex);
    }

    public bool TryRerollStartingItem(int slotIndex)
    {
        if (!_startingItemSelectionPending ||
            !_startingItemSelection.TryReroll(slotIndex))
        {
            return false;
        }

        _eventTab?.ShowStartingItemSelection();
        return true;
    }

    public bool TryConfirmStartingItems()
    {
        if (!_startingItemSelectionPending ||
            _battleItems.Count > 0)
        {
            return false;
        }

        Dictionary<string, BattleItemSO> grantedDefinitions =
            new(StringComparer.Ordinal);
        Dictionary<string, int> grantedCounts =
            new(StringComparer.Ordinal);
        IReadOnlyList<BattleItemSO> selectedItems =
            _startingItemSelection.Items;
        for (int index = 0; index < selectedItems.Count; index++)
        {
            BattleItemSO item = selectedItems[index];
            if (item == null || !item.AvailableAsStartingItem ||
                string.IsNullOrWhiteSpace(item.ItemId))
            {
                return false;
            }

            grantedDefinitions.TryAdd(item.ItemId, item);
            grantedCounts[item.ItemId] =
                grantedCounts.TryGetValue(item.ItemId, out int count)
                    ? count + 1
                    : 1;
        }

        Dictionary<string, BattleItemRunState> grantedItems =
            new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, BattleItemSO> pair in
                 grantedDefinitions)
        {
            BattleItemRunState state = new(pair.Value);
            if (!state.AcquireCopies(
                    pair.Value,
                    grantedCounts[pair.Key]))
            {
                return false;
            }
            grantedItems.Add(pair.Key, state);
        }

        if (!_startingItemSelection.TryConfirm())
            return false;

        foreach (KeyValuePair<string, BattleItemRunState> pair in grantedItems)
            _battleItems.Add(pair.Key, pair.Value);
        _startingItemSelectionPending = false;
        BattleItemsChanged?.Invoke();
        return BeginPreparedDungeonFlow();
    }

    private bool PrepareStartingItemSelection()
    {
        DungeonStartingItemRule rule =
            _session.Definition?.StartingItemRule;
        List<BattleItemSO> pool = rule?.ResolveEligibleItems() ?? new();
        int requiredCount = rule?.MinimumRequiredPoolSize ?? 0;
        string error = "Starting item rule is not configured.";
        if (rule == null || !_startingItemSelection.TryPrepare(
                pool,
                rule.ItemCount,
                rule.RerollsPerSlot,
                _session.RunSeed ^ StartingItemSeedSalt,
                out error))
        {
            _startingItemSelectionPending = false;
            _session.Finish(EDungeonRunResult.Defeat);
            flowController?.ShowEventTab();
            _eventTab?.ShowStartingItemConfigurationError(
                pool.Count,
                requiredCount);
            Debug.LogError(error, this);
            return false;
        }

        _startingItemSelectionPending = true;
        _session.SetActivity(EDungeonRunActivity.StartingItemSelection);
        flowController.ShowEventTab();
        _eventTab?.ShowStartingItemSelection();
        _tutorialController?.PauseForStartingItemSelection();
        return true;
    }

    private bool BeginPreparedDungeonFlow()
    {
        if (_session.Definition?.SelectStartingItems == true &&
            _session.Definition.UseBattleCards == false &&
            (_session.PhaseSequence == null ||
             _session.PhaseSequence.Count == 0 ||
             _session.PhaseSequence[0] != EDungeonPhase.Battle))
        {
            ClearPlayerParty();
            _session.Finish(EDungeonRunResult.Defeat);
            Debug.LogError(
                "A prepared dungeon run must begin with a Battle phase.",
                this);
            return false;
        }

        if (flowController.StartRun(_session.PhaseSequence))
        {
            if (_session.Definition != null &&
                _session.Definition.HasTutorial)
            {
                _session.SetActivity(EDungeonRunActivity.TutorialGuide);
                _session.Pause.Add(EDungeonPauseReason.TutorialGuide);
                ApplyBattlePauseState();
                battleTab?.Refresh();
                _tutorialController?.BeginBattleWalkthrough();
            }

            return true;
        }

        ClearPlayerParty();
        _session.Finish(EDungeonRunResult.Defeat);
        Debug.LogError("Failed to start the dungeon run flow.", this);
        return false;
    }

    public bool TryApplyCharacterDungeonUpgrade(
        int slotIndex,
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        if (!_battleRewardPending || slotIndex < 0 ||
            slotIndex >= _ownedTurrets.Count)
        {
            return false;
        }

        CharacterRuntime character = _ownedTurrets[slotIndex];
        if (character == null || !character.ApplyDungeonUpgrade(
                definitionIndex,
                upgradeType))
        {
            return false;
        }

        CompleteBattleReward();
        return true;
    }

    public bool TryApplyCharacterDungeonUpgrade(
        int slotIndex,
        int definitionIndex,
        string upgradeId)
    {
        if (!_battleRewardPending || slotIndex < 0 ||
            slotIndex >= _ownedTurrets.Count)
        {
            return false;
        }

        CharacterRuntime character = _ownedTurrets[slotIndex];
        if (character == null || !character.ApplyDungeonUpgrade(
                definitionIndex,
                upgradeId))
        {
            return false;
        }

        CompleteBattleReward();
        return true;
    }

    public bool CanApplyEnergyUpgrade(EDungeonEnergyUpgradeType upgradeType)
    {
        return upgradeType != EDungeonEnergyUpgradeType.RechargeSpeed ||
               _energyRechargeDuration > MinimumEnergyRechargeDuration;
    }

    public bool TryApplyEnergyUpgrade(EDungeonEnergyUpgradeType upgradeType)
    {
        if (!_battleRewardPending || !CanApplyEnergyUpgrade(upgradeType))
            return false;

        switch (upgradeType)
        {
            case EDungeonEnergyUpgradeType.MaximumEnergy:
                _maximumEnergy++;
                break;
            case EDungeonEnergyUpgradeType.RechargeSpeed:
                _energyRechargeDuration = TimePrecision.Normalize(
                    _energyRechargeDuration - EnergyRechargeUpgradeAmount,
                    MinimumEnergyRechargeDuration);
                break;
            default:
                return false;
        }

        _battleManager?.ConfigureActiveSkillResource(
            _maximumEnergy,
            _energyRechargeDuration);
        CompleteBattleReward();
        return true;
    }

    public bool IsBattleItemOwned(BattleItemSO item)
    {
        return TryGetBattleItemState(item, out BattleItemRunState state) &&
               state.IsOwned;
    }

    public int GetBattleItemCount(BattleItemSO item)
    {
        if (!TryGetBattleItemState(item, out BattleItemRunState state) ||
            !state.IsOwned)
        {
            return 0;
        }

        return item.HasUnlimitedUses
            ? Mathf.Max(1, state.OwnedCopies)
            : state.RemainingUses;
    }

    public float GetBattleItemCooldown(BattleItemSO item)
    {
        return TryGetBattleItemState(item, out BattleItemRunState state)
            ? state.CooldownRemaining
            : 0f;
    }

    public bool CanAcquireBattleItem(BattleItemSO item)
    {
        if (item == null || !item.AvailableAsDungeonReward)
            return false;

        if (!TryGetBattleItemState(item, out BattleItemRunState state))
            return true;
        if (!item.UsesLegacyUsagePolicy)
            return !state.IsOwned && !state.IsRemoved;
        if (item.HasUnlimitedUses)
            return !state.IsOwned;
        return item.MaximumRunUses == 0 ||
               state.RemainingUses < item.MaximumRunUses;
    }

    public bool TryAcquireBattleItem(BattleItemSO item)
    {
        if (!_battleRewardPending || !AcquireBattleItemInternal(item))
            return false;
        CompleteBattleReward();
        return true;
    }

    private bool AcquireBattleItemInternal(BattleItemSO item)
    {
        if (!CanAcquireBattleItem(item))
            return false;

        BattleItemRunState state = GetOrCreateBattleItemState(item);
        if (state == null || !state.Acquire(item))
            return false;

        BattleItemsChanged?.Invoke();
        return true;
    }

    public bool CanAcquireBattleCard(BattleCardSO card)
    {
        return UsesBattleCards && card != null &&
               card.AvailableAsDungeonReward &&
               card.IsEligible(GetCurrentPartyDefinitions());
    }

    public int GetAcquiredBattleCardCount(BattleCardSO card)
    {
        if (card == null)
            return 0;
        int count = 0;
        foreach (BattleCardSO acquired in _acquiredBattleCards)
        {
            if (ReferenceEquals(acquired, card))
                count++;
        }
        return count;
    }

    public bool TryAcquireBattleCard(BattleCardSO card)
    {
        if (!_battleRewardPending || !CanAcquireBattleCard(card))
            return false;
        _acquiredBattleCards.Add(card);
        CompleteBattleReward();
        return true;
    }

    public bool TryUseBattleItemOnEnemy(
        BattleItemSO item,
        EnemyRuntime enemy)
    {
        if (item == null ||
            item.TargetType != BattleItemTargetType.Enemy ||
            !CanUseBattleItem(item) ||
            board == null ||
            !board.ContainsTargetableEnemy(enemy) ||
            _battleManager == null ||
            !_battleManager.TrySpend(item.EnergyCost))
        {
            return false;
        }

        if (!BattleItemUseExecutor.TryApplyToEnemy(
                item,
                board,
                enemy,
                _battleManager))
        {
            _battleManager.TryGain(item.EnergyCost);
            return false;
        }

        return CompleteBattleItemUse(item);
    }

    public bool TryUseBattleItemOnTurret(
        BattleItemSO item,
        CharacterRuntime turret)
    {
        if (item == null ||
            item.TargetType != BattleItemTargetType.Turret ||
            !CanUseBattleItem(item) || turret == null ||
            !_ownedTurrets.Contains(turret) ||
            _battleManager == null ||
            !_battleManager.TrySpend(item.EnergyCost))
        {
            return false;
        }

        if (!BattleItemUseExecutor.TryApplyToTurret(
                item,
                board,
                turret,
                _battleManager))
        {
            _battleManager.TryGain(item.EnergyCost);
            return false;
        }

        return CompleteBattleItemUse(item);
    }

    public int GetBattleDifficultyScale(int battleNumber)
    {
        int index = battleNumber - 1;
        return index >= 0 && index < _battlePlans.Length
            ? _battlePlans[index].DifficultyScale
            : 0;
    }

    public bool TryAcquireTurret(
        CharacterSO definition,
        int replacementSlotIndex = -1)
    {
        if (!_battleRewardPending || definition == null ||
            !CanAcquireCharacterReward(definition))
        {
            return false;
        }

        CharacterRuntime slot;
        if (_ownedTurrets.Count < MaximumPartySize)
        {
            slot = playerCharacters[_ownedTurrets.Count];
            if (slot == null || !slot.ConfigureDefinition(definition))
                return false;

            slot.BeginDungeonRun();
            slot.gameObject.SetActive(true);
            _ownedTurrets.Add(slot);
        }
        else
        {
            if (replacementSlotIndex < 0 ||
                replacementSlotIndex >= _ownedTurrets.Count)
            {
                return false;
            }

            slot = _ownedTurrets[replacementSlotIndex];
            if (slot == null || !slot.ConfigureDefinition(definition))
                return false;
            slot.BeginDungeonRun();
        }

        RecordAcquiredCharacter(definition);
        CompleteBattleReward();
        return true;
    }

    public IReadOnlyList<CharacterSO>
        GetAvailableCharacterRewardDefinitions()
    {
        if (_availableTurrets.Count == 0)
            return Array.Empty<CharacterSO>();

        List<CharacterSO> candidates = new();
        HashSet<string> uniqueCharacterIds =
            new(StringComparer.Ordinal);
        foreach (CharacterSO definition in _availableTurrets)
        {
            if (!CanAcquireCharacterReward(definition))
                continue;

            string characterId = definition.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId) ||
                !uniqueCharacterIds.Add(characterId))
            {
                continue;
            }

            candidates.Add(definition);
        }

        return candidates.Count > 0
            ? candidates.ToArray()
            : Array.Empty<CharacterSO>();
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
        EnemyRuntime enemy = CreateEnemyRuntime(null, health);
        bool added = TryPrepareBoard() &&
                     board.TryAddEnemyCard(row, column, enemy);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool AddEnemyCardToRandomTile(int health = 1)
    {
        EnemyRuntime enemy = CreateEnemyRuntime(null, health);
        bool added = TryPrepareBoard() &&
                     board.TryAddEnemyCardToRandomTile(enemy);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool AddEnemyCardToNextAvailableTile(int health = 1)
    {
        EnemyRuntime enemy = CreateEnemyRuntime(null, health);
        bool added = TryPrepareBoard() &&
                     board.TryAddEnemyCardToNextAvailableTile(enemy);
        if (added)
            _battleManager?.NotifyBoardChanged();

        return added;
    }

    public bool QueueEnemy(int health = 1)
    {
        EnemyRuntime enemy = CreateEnemyRuntime(null, health);
        return TryResolveBattleManager() && _battleManager.QueueEnemy(enemy);
    }

    public bool QueueEnemy(EnemySO definition, int maximumHealthOverride = 0)
    {
        EnemyRuntime enemy = CreateEnemyRuntime(
            definition,
            maximumHealthOverride);
        return TryResolveBattleManager() && _battleManager.QueueEnemy(enemy);
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

    private bool StartNewBattle()
    {
        if (!TryResolveBattleManager() || !_battleManager.IsInitialized)
            return false;

        List<IBattleCharacter> characters = new(MaximumPartySize);
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character != null && character.CanParticipate)
                characters.Add(character);
        }

        if (characters.Count == 0)
        {
            HandleBattleEnded(EBattleResult.Defeat);
            return false;
        }

        int battleIndex = Mathf.Clamp(
            _session.CurrentBattleNumber - 1,
            0,
            Mathf.Max(0, _battlePlans.Length - 1));
        if (_battlePlans.Length == 0)
        {
            Debug.LogError("Dungeon battle plans were not generated.", this);
            return false;
        }

        DungeonBattlePlan plan = _battlePlans[battleIndex];
        BattleSetup setup;
        string error;
        bool setupCreated;
        BattleSO fixedBattle = null;
        if (_session.Definition != null &&
            _session.Definition.TryGetFixedBattle(
                battleIndex,
                out fixedBattle))
        {
            setupCreated = fixedBattle.TryCreateSetup(
                plan.RandomSeed,
                out setup,
                out error);
        }
        else
        {
            bool useTutorialSetup =
                _session.Definition != null &&
                _session.Definition.UsesTutorialBattleSetup &&
                _session.CurrentBattleNumber == 1;
            setupCreated = useTutorialSetup
                ? TryCreateTutorialBattleSetup(plan, out setup, out error)
                : TryCreateScaledBattleSetup(plan, out setup, out error);
        }
        if (!setupCreated)
        {
            Debug.LogError($"Failed to create dungeon battle: {error}", this);
            return false;
        }

        float dungeonStageProgress = flowController != null
            ? flowController.CurrentStageProgress
            : Mathf.Max(0, battleIndex);
        board.SetDungeonStageProgress(dungeonStageProgress);
        BattleArenaSetup arena = setup.Arena;
        if (_session.Definition != null)
        {
            arena = arena.WithWorldRadius(
                    _session.Definition.BattleArenaRadius)
                .WithCoreMaximumHealth(
                    _session.Definition.BattleShieldMaximumHealth);
        }
        _dungeonShieldMaximumHealth = arena.CoreMaximumHealth;
        _dungeonShieldCurrentHealth = Mathf.Clamp(
            _dungeonShieldCurrentHealth,
            0,
            _dungeonShieldMaximumHealth);
        board.ConfigureArena(
            arena,
            setup.Environment,
            _dungeonShieldCurrentHealth);
        board.Initialize(setup.FieldSize, setup.MaximumStackSize);
        ResetBattleItemCooldowns();
        _battleManager.ConfigureActiveSkillResource(
            _maximumEnergy,
            _energyRechargeDuration);
        PrepareBattleCardDeck(plan.RandomSeed);
        bool started = _battleManager.StartBattle(
            board,
            characters,
            setup.Enemies,
            setup.SpawnInterval,
            setup.TimeLimit,
            setup.InitialEnemyCount,
            true);
        if (started)
        {
            RequestDungeonBgm(
                _session.Definition,
                EDungeonBgmState.Battle,
                fixedBattle != null ? fixedBattle.BgmOverride : null);
            NotifyBattleStarted();
        }
        return started;
    }

    private void GenerateBattlePlans(int battleCount, int runSeed)
    {
        battleCount = Mathf.Max(1, battleCount);
        _battlePlans = new DungeonBattlePlan[battleCount];
        System.Random random = new(runSeed);
        int previousScale = -1;
        for (int index = 0; index < battleCount; index++)
        {
            int difficultyScale;
            if (index == 0)
            {
                difficultyScale = 0;
            }
            else if (index == battleCount - 1)
            {
                difficultyScale = 100;
            }
            else
            {
                int baseScale = Mathf.RoundToInt(
                    index * 100f / (battleCount - 1));
                int jitter = random.Next(
                    -difficultyScaleJitter,
                    difficultyScaleJitter + 1);
                int remainingBattles = battleCount - index - 1;
                difficultyScale = Mathf.Clamp(
                    baseScale + jitter,
                    previousScale + 1,
                    100 - remainingBattles);
            }

            _battlePlans[index] = new DungeonBattlePlan(
                difficultyScale,
                random.Next());
            previousScale = difficultyScale;
        }
    }

    private bool TryCreateScaledBattleSetup(
        DungeonBattlePlan plan,
        out BattleSetup setup,
        out string error)
    {
        setup = null;
        IReadOnlyList<EnemySO> allDefinitions = GetBattleEnemyPool();
        if (allDefinitions.Count == 0)
        {
            error = "At least one enemy definition is required.";
            return false;
        }

        float progress = plan.DifficultyScale / 100f;
        int fieldSize = GetScaledGridSize(plan.DifficultyScale);
        int initialEnemyCount = fieldSize * fieldSize;
        int scaledEnemyCount = Mathf.Max(
            1,
            Mathf.RoundToInt(Mathf.Lerp(
                baselineEnemyCount,
                maximumScaledEnemyCount,
                progress)));
        int enemyCount = Mathf.Max(
            initialEnemyCount,
            scaledEnemyCount);
        int baselineHealth = CalculateBaselineEnemyHealth();
        int enemyHealth = Mathf.Max(
            1,
            Mathf.RoundToInt(
                baselineHealth * Mathf.Lerp(
                    1f,
                    maximumHealthMultiplier,
                    progress)));
        int healthVariance = Mathf.RoundToInt(Mathf.Lerp(
            baselineEnemyHealthVariance,
            maximumEnemyHealthVariance,
            progress));
        float baseSpawnInterval = firstBattle != null
            ? firstBattle.SpawnInterval
            : enemySpawnInterval;
        float spawnInterval = TimePrecision.Normalize(
            Mathf.Lerp(
                baseSpawnInterval,
                minimumScaledSpawnInterval,
                progress),
            0.1f);

        List<EnemySO> eligibleDefinitions = new();
        foreach (EnemySO definition in allDefinitions)
        {
            if (definition != null &&
                plan.DifficultyScale >= definition.UnlockDifficulty)
            {
                eligibleDefinitions.Add(definition);
            }
        }

        if (eligibleDefinitions.Count == 0)
        {
            EnemySO safestDefinition = allDefinitions[0];
            foreach (EnemySO definition in allDefinitions)
            {
                if (definition != null &&
                    definition.ThreatCost < safestDefinition.ThreatCost)
                {
                    safestDefinition = definition;
                }
            }
            eligibleDefinitions.Add(safestDefinition);
        }

        System.Random random = new(plan.RandomSeed);
        int totalEnemyHealth = Mathf.Max(
            enemyCount,
            scaledEnemyCount * enemyHealth);
        IReadOnlyList<int> enemyHealthValues =
            CreateEnemyHealthDistributionFromTotal(
            enemyCount,
            totalEnemyHealth,
            healthVariance,
            random);
        List<EnemyRuntime> enemies = new(enemyCount);
        int normalCount = 0;
        int specialCount = 0;
        int eliteCount = 0;
        int bossCount = 0;
        for (int index = 0; index < enemyCount; index++)
        {
            EnemySO definition = SelectScaledEnemy(
                eligibleDefinitions,
                progress,
                random);
            int maximumHealth = enemyHealthValues[index];
            enemies.Add(definition.CreateRuntime(maximumHealth));
            switch (definition.Grade)
            {
                case EEnemyGrade.Special:
                    specialCount++;
                    break;
                case EEnemyGrade.Elite:
                    eliteCount++;
                    break;
                case EEnemyGrade.Boss:
                    bossCount++;
                    break;
                default:
                    normalCount++;
                    break;
            }
        }

        for (int index = enemies.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(0, index + 1);
            (enemies[index], enemies[swapIndex]) =
                (enemies[swapIndex], enemies[index]);
        }

        int stackSize = firstBattle != null
            ? firstBattle.MaximumStackSize
            : maximumStackSize;
        float timeLimit = firstBattle != null
            ? firstBattle.TimeLimit
            : normalBattleTimeLimit;
        setup = new BattleSetup(
            fieldSize,
            stackSize,
            spawnInterval,
            timeLimit,
            new BattleEnemyGradeCounts(
                normalCount,
                specialCount,
                eliteCount,
                bossCount),
            enemies,
            initialEnemyCount,
            firstBattle != null
                ? firstBattle.CreateArenaSetup()
                : BattleArenaSetup.CreateCircular(),
            firstBattle != null
                ? firstBattle.CreateEnvironmentSetup()
                : BattleEnvironmentSetup.Default);
        error = string.Empty;
        return true;
    }

    private bool TryCreateTutorialBattleSetup(
        DungeonBattlePlan plan,
        out BattleSetup setup,
        out string error)
    {
        setup = null;
        IReadOnlyList<EnemySO> allDefinitions = GetBattleEnemyPool();
        if (allDefinitions.Count == 0)
        {
            error = "At least one enemy definition is required.";
            return false;
        }

        EnemySO tutorialEnemy = null;
        foreach (EnemySO definition in allDefinitions)
        {
            if (definition == null)
                continue;

            if (tutorialEnemy == null || definition.Type == EEnemyType.Basic ||
                definition.ThreatCost < tutorialEnemy.ThreatCost)
            {
                tutorialEnemy = definition;
            }

            if (definition.Type == EEnemyType.Basic)
                break;
        }

        if (tutorialEnemy == null)
        {
            error = "A valid tutorial enemy definition is required.";
            return false;
        }

        CharacterData startingData =
            CreateCharacterPreviewData(_startingTurret);
        int totalHealth = CalculateTutorialTotalEnemyHealth(startingData);
        System.Random random = new(plan.RandomSeed);
        IReadOnlyList<int> healthValues =
            CreateEnemyHealthDistributionFromTotal(
                TutorialEnemyCount,
                totalHealth,
                1,
                random);
        List<EnemyRuntime> enemies = new(TutorialEnemyCount);
        for (int index = 0; index < TutorialEnemyCount; index++)
            enemies.Add(tutorialEnemy.CreateRuntime(healthValues[index]));

        for (int index = enemies.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(0, index + 1);
            (enemies[index], enemies[swapIndex]) =
                (enemies[swapIndex], enemies[index]);
        }

        BattleEnemyGradeCounts gradeCounts = tutorialEnemy.Grade switch
        {
            EEnemyGrade.Special => new BattleEnemyGradeCounts(
                0, TutorialEnemyCount, 0, 0),
            EEnemyGrade.Elite => new BattleEnemyGradeCounts(
                0, 0, TutorialEnemyCount, 0),
            EEnemyGrade.Boss => new BattleEnemyGradeCounts(
                0, 0, 0, TutorialEnemyCount),
            _ => new BattleEnemyGradeCounts(
                TutorialEnemyCount, 0, 0, 0),
        };
        setup = new BattleSetup(
            TutorialGridSize,
            firstBattle != null
                ? firstBattle.MaximumStackSize
                : maximumStackSize,
            TutorialSpawnInterval,
            TutorialTimeLimit,
            gradeCounts,
            enemies,
            TutorialInitialEnemyCount,
            firstBattle != null
                ? firstBattle.CreateArenaSetup()
                : BattleArenaSetup.CreateCircular(),
            firstBattle != null
                ? firstBattle.CreateEnvironmentSetup()
                : BattleEnvironmentSetup.Default);
        error = string.Empty;
        return true;
    }

    private static int CalculateTutorialTotalEnemyHealth(
        CharacterData data)
    {
        if (data == null)
            return TutorialEnemyCount;

        float attackCycle = Mathf.Max(
            0.1f,
            data.AttackCooldown + data.AttackRecoveryDuration);
        int expectedAttackCount = Mathf.Max(
            1,
            Mathf.FloorToInt(
                TutorialTargetAutoClearDuration / attackCycle));
        float damagePerAttack = CalculateEstimatedNormalDamagePerAttack(data);
        return Mathf.Max(
            TutorialEnemyCount,
            Mathf.RoundToInt(
                expectedAttackCount *
                damagePerAttack *
                TutorialDamageBudgetRatio));
    }

    private static int GetScaledGridSize(int difficultyScale)
    {
        int size = difficultyScale switch
        {
            < 30 => 3,
            < 65 => 4,
            < 90 => 5,
            _ => 6,
        };
        return Mathf.Clamp(
            size,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
    }

    private static float CalculateEstimatedNormalDamagePerAttack(
        CharacterData data)
    {
        if (data == null)
            return 1f;

        float estimatedDamage = 0f;
        foreach (CharacterAttackDefinition definition in
                 data.AttackDefinitions)
        {
            if (definition == null ||
                definition.DamageType == CharacterAttackDamageType.StatusEffect ||
                definition.DamageType == CharacterAttackDamageType.StatusRemoval)
            {
                continue;
            }

            int selectedTargets = Mathf.Max(1, definition.SubjectCount);
            estimatedDamage += data.CalculateAttackDamage(definition) *
                               selectedTargets;
        }

        return Mathf.Max(1f, estimatedDamage);
    }

    private int CalculateBaselineEnemyHealth()
    {
        CharacterData startingData =
            CreateCharacterPreviewData(_startingTurret);
        float attackCycle = startingData != null
            ? startingData.AttackCooldown +
              startingData.AttackRecoveryDuration
            : 1.5f;
        float timeLimit = firstBattle != null
            ? firstBattle.TimeLimit
            : normalBattleTimeLimit;
        int expectedAttackCount = Mathf.Max(
            1,
            Mathf.FloorToInt(timeLimit / Mathf.Max(0.1f, attackCycle)));
        float totalDamageBudget = expectedAttackCount *
                                  CalculateEstimatedNormalDamagePerAttack(
                                      startingData) *
                                  baselineSoloDamageBudgetRatio;
        return Mathf.Max(
            1,
            Mathf.RoundToInt(totalDamageBudget / baselineEnemyCount));
    }

    private IReadOnlyList<EnemySO> GetBattleEnemyPool()
    {
        IReadOnlyList<EnemySO> overridePool =
            _session.Definition != null
                ? _session.Definition.EnemyPoolOverride
                : null;
        if (overridePool != null && overridePool.Count > 0)
            return overridePool;

        if (firstBattle != null)
        {
            IReadOnlyList<EnemySO> configuredDefinitions =
                firstBattle.GetAllEnemyDefinitions();
            if (configuredDefinitions.Count > 0)
                return configuredDefinitions;
        }

        return GetNormalEnemyPool();
    }

    internal static CharacterData CreateCharacterPreviewData(
        CharacterSO definition)
    {
        if (definition == null)
            return null;

        CharacterCollectionData collection =
            DataManager.Current?.CharacterDatas;
        return collection != null
            ? collection.CreatePreviewData(definition)
            : definition.CreateData(new CharacterProgressData(
                definition.CharacterId,
                definition.InitiallyOwned));
    }

    private static EnemySO SelectScaledEnemy(
        IReadOnlyList<EnemySO> definitions,
        float progress,
        System.Random random)
    {
        double exponent = Mathf.Lerp(-1.5f, 0.9f, progress);
        double totalWeight = 0d;
        double[] weights = new double[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
        {
            double threat = Mathf.Max(0.1f, definitions[index].ThreatCost);
            double jitter = 0.9d + random.NextDouble() * 0.2d;
            weights[index] = Math.Pow(threat, exponent) * jitter;
            totalWeight += weights[index];
        }

        double value = random.NextDouble() * totalWeight;
        for (int index = 0; index < definitions.Count; index++)
        {
            value -= weights[index];
            if (value <= 0d)
                return definitions[index];
        }

        return definitions[definitions.Count - 1];
    }

    private static IReadOnlyList<int> CreateEnemyHealthDistributionFromTotal(
        int enemyCount,
        int totalHealth,
        int healthVariance,
        System.Random random)
    {
        enemyCount = Mathf.Max(1, enemyCount);
        totalHealth = Mathf.Max(enemyCount, totalHealth);
        healthVariance = Mathf.Max(0, healthVariance);
        float averageHealth = totalHealth / (float)enemyCount;
        int minimumHealth = Mathf.Max(
            1,
            Mathf.FloorToInt(averageHealth) - healthVariance);
        int maximumHealth = Mathf.Max(
            minimumHealth,
            Mathf.CeilToInt(averageHealth) + healthVariance);
        int[] healthValues = new int[enemyCount];
        int remainingHealth = totalHealth;

        for (int index = 0; index < enemyCount; index++)
        {
            int remainingEnemyCount = enemyCount - index - 1;
            int minimumAllowed = Mathf.Max(
                minimumHealth,
                remainingHealth - remainingEnemyCount * maximumHealth);
            int maximumAllowed = Mathf.Min(
                maximumHealth,
                remainingHealth - remainingEnemyCount * minimumHealth);
            int health = remainingEnemyCount == 0
                ? remainingHealth
                : random.Next(minimumAllowed, maximumAllowed + 1);
            healthValues[index] = health;
            remainingHealth -= health;
        }

        return healthValues;
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

        BindBattleEvents();
        return battleTab != null && battleTab.Initialize(_battleManager);
    }

    private EnemyRuntime CreateEnemyRuntime(
        EnemySO definition,
        int maximumHealthOverride)
    {
        EnemySO resolvedDefinition = definition != null
            ? definition
            : ResolveDefaultEnemyDefinition();
        return resolvedDefinition != null
            ? resolvedDefinition.CreateRuntime(maximumHealthOverride)
            : null;
    }

    private EnemySO ResolveDefaultEnemyDefinition()
    {
        if (defaultEnemy != null)
            return defaultEnemy;

        if (normalEnemyPool != null)
        {
            foreach (EnemySO definition in normalEnemyPool)
            {
                if (definition != null)
                    return definition;
            }
        }

        IReadOnlyList<EnemySO> catalogEnemies =
            EnemyDefinitionCatalog.GetAll();
        if (catalogEnemies.Count > 0)
            return catalogEnemies[0];

        EnsureFallbackEnemyDefinitions();
        return _fallbackEnemyPool.Count > 0
            ? _fallbackEnemyPool[0]
            : null;
    }

    private IReadOnlyList<EnemySO> GetNormalEnemyPool()
    {
        List<EnemySO> configuredEnemies = new();
        if (normalEnemyPool != null)
        {
            foreach (EnemySO definition in normalEnemyPool)
            {
                if (definition != null)
                    configuredEnemies.Add(definition);
            }
        }

        if (configuredEnemies.Count > 0)
            return configuredEnemies;

        IReadOnlyList<EnemySO> catalogEnemies =
            EnemyDefinitionCatalog.GetAll();
        if (catalogEnemies.Count > 0)
            return catalogEnemies;

        EnsureFallbackEnemyDefinitions();
        return _fallbackEnemyPool;
    }

    private void EnsureFallbackEnemyDefinitions()
    {
        if (_fallbackEnemyPool.Count > 0)
            return;

        foreach (EEnemyType type in FallbackNormalEnemyTypes)
        {
            _fallbackEnemyPool.Add(EnemySO.CreateRuntimeDefault(
                type,
                minimumEnemyHealth));
        }
    }

    private void ReleaseFallbackEnemyDefinitions()
    {
        foreach (EnemySO definition in _fallbackEnemyPool)
        {
            if (definition != null)
                Destroy(definition);
        }

        _fallbackEnemyPool.Clear();
    }

    private void BindFlowEvents()
    {
        if (_flowEventsBound || flowController == null)
            return;

        flowController.PhaseChanged += HandleDungeonPhaseChanged;
        flowController.FlowCompleted += HandleDungeonFlowCompleted;
        _flowEventsBound = true;
    }

    private void UnbindFlowEvents()
    {
        if (!_flowEventsBound || flowController == null)
            return;

        flowController.PhaseChanged -= HandleDungeonPhaseChanged;
        flowController.FlowCompleted -= HandleDungeonFlowCompleted;
        _flowEventsBound = false;
    }

    private void HandleDungeonPhaseChanged(EDungeonPhase phase, int _)
    {
        if (!TryResolveBattleManager())
            return;

        if (phase == EDungeonPhase.Battle)
        {
            _session.SetBattleNumber(flowController.CurrentBattleNumber);
            _session.SetActivity(EDungeonRunActivity.Battle);
            _session.Pause.Remove(EDungeonPauseReason.NonBattlePhase);
            _session.Pause.Remove(EDungeonPauseReason.BattleReward);
            _battleRewardPending = false;
            HideBattleRewardOverlay();
            HideRoomViews();
            if (_battleManager.State == EBattleState.Completed)
                StartNewBattle();
            else if (!_battleManager.HasSession)
                StartNewBattle();
        }
        else
        {
            _session.SetActivity(phase switch
            {
                EDungeonPhase.Rest => EDungeonRunActivity.Rest,
                EDungeonPhase.Shop => EDungeonRunActivity.Shop,
                _ => EDungeonRunActivity.Event,
            });
            _session.Pause.Add(EDungeonPauseReason.NonBattlePhase);
            _battleRewardPending = false;
            HideBattleRewardOverlay();
            ShowDungeonRoom(phase);
        }

        NotifyPhaseEntered(phase);
        ApplyBattlePauseState();
        battleTab?.Refresh();
    }

    private void BindBattleEvents()
    {
        if (_battleEventsBound || _battleManager == null)
            return;

        _battleManager.BattleCompleted += HandleBattleCompleted;
        _battleManager.BattleEnded += HandleBattleEnded;
        _battleEventsBound = true;
    }

    private void UnbindBattleEvents()
    {
        if (!_battleEventsBound || _battleManager == null)
            return;

        _battleManager.BattleCompleted -= HandleBattleCompleted;
        _battleManager.BattleEnded -= HandleBattleEnded;
        _battleEventsBound = false;
    }

    private void HandleBattleCompleted()
    {
        battleTab?.Refresh();
        CaptureDungeonShieldHealth();
        ApplyClearedBattleHealthCost();
        if (_session.Definition != null &&
            _session.Definition.HasTutorial)
        {
            CompleteTutorialRun();
            return;
        }

        if (CurrentPhase == EDungeonPhase.Battle && flowController != null &&
            !flowController.IsCompleted)
        {
            _battleRewardPending = true;
            _session.Pause.Add(EDungeonPauseReason.BattleReward);
            RequestDungeonBgm(
                _session.Definition,
                EDungeonBgmState.Ready);
            ShowBattleRewardOverlay();
        }
    }

    private void CaptureDungeonShieldHealth()
    {
        IBattleObjective objective = board?.Objective;
        if (objective?.IsActive != true)
            return;

        _dungeonShieldMaximumHealth = Mathf.Max(
            1,
            objective.MaximumHealth);
        _dungeonShieldCurrentHealth = Mathf.Clamp(
            objective.CurrentHealth,
            0,
            _dungeonShieldMaximumHealth);
    }

    private void ApplyClearedBattleHealthCost()
    {
        int healthCost = _session.Definition?
            .ResolveClearedBattleHealthCost(CurrentDifficultyScale) ?? 0;
        if (healthCost <= 0)
            return;

        foreach (CharacterRuntime character in _ownedTurrets)
            character?.ApplyRunHealthLoss(healthCost);
    }

    private void HandleBattleEnded(EBattleResult result)
    {
        if (!_session.IsActive)
            return;

        NotifyBattleEnded(result);
        if (result == EBattleResult.Timeout &&
            _session.Definition != null &&
            _session.Definition.HasTutorial)
        {
            CompleteTutorialRun();
            return;
        }

        if (result == EBattleResult.Victory)
            return;

        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        _session.Finish(EDungeonRunResult.Defeat);
        _battleManager?.EndBattle(board);
        board?.ClearAllStacks();
        ClearPlayerParty();
        ResetRunResourcesAndItems();
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_session.Result);
        NotifyRunEnded(_session.Result);
        RunEnded?.Invoke(_session.Result);
    }

    private void CompleteTutorialRun()
    {
        if (!_session.IsActive || _session.Definition == null ||
            !_session.Definition.HasTutorial)
        {
            return;
        }

        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        _startingItemSelectionPending = false;
        _session.Finish(EDungeonRunResult.Clear);
        NotifyRunEnded(EDungeonRunResult.Clear);
        RunEnded?.Invoke(EDungeonRunResult.Clear);
        ApplyBattlePauseState();
        _tutorialController?.ShowCompletion();
    }

    private void HandleDungeonFlowCompleted()
    {
        if (!_session.IsActive)
            return;

        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        _session.Finish(EDungeonRunResult.Clear);
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_session.Result);
        NotifyRunEnded(_session.Result);
        RunEnded?.Invoke(_session.Result);
    }

    internal void CompleteBattleReward()
    {
        if (!_battleRewardPending)
            return;

        _battleRewardPending = false;
        _session.Pause.Remove(EDungeonPauseReason.BattleReward);
        HideBattleRewardOverlay();
        flowController?.TryAdvance();
    }

    public int ResolveBattleShieldRecoveryAmount()
    {
        DungeonShieldRecoveryRule rule = ResolveShieldRecoveryRule();
        return rule?.ResolveAmount(DungeonShieldMaximumHealth) ?? 0;
    }

    public bool TryRecoverBattleShield()
    {
        if (!_battleRewardPending || DungeonShieldMaximumHealth <= 0)
            return false;

        int amount = ResolveBattleShieldRecoveryAmount();
        if (amount <= 0)
            return false;

        int previous = DungeonShieldCurrentHealth;
        _dungeonShieldCurrentHealth = Mathf.Min(
            DungeonShieldMaximumHealth,
            previous + amount);
        if (board?.Objective is BattleCoreRuntime core)
            core.Heal(amount);
        CompleteBattleReward();
        return true;
    }

    public IReadOnlyList<BattleCardSO> GetBattleCardRewardCandidates()
    {
        BattleSO battle = GetCurrentBattleRewardDefinition();
        List<BattleCardSO> result = BuildBattleCardRewardCandidates(
            battle?.CardRewardPool);
        if (result.Count > 0)
            return result;
        result = BuildBattleCardRewardCandidates(
            _session.Definition?.BattleCardRewardPool);
        return result.Count > 0
            ? result
            : BuildBattleCardRewardCandidates(BattleCardCatalog.GetAll());
    }

    private List<BattleCardSO> BuildBattleCardRewardCandidates(
        IReadOnlyList<BattleCardSO> source)
    {
        List<BattleCardSO> result = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (BattleCardSO card in source)
            {
                if (CanAcquireBattleCard(card) &&
                    ids.Add(card.CardId))
                {
                    result.Add(card);
                }
            }
        }
        return result;
    }

    public IReadOnlyList<BattleItemSO> GetConsumableRewardCandidates()
    {
        BattleSO battle = GetCurrentBattleRewardDefinition();
        List<BattleItemSO> result = BuildConsumableRewardCandidates(
            battle?.ConsumableRewardPool);
        if (result.Count > 0)
            return result;
        result = BuildConsumableRewardCandidates(
            _session.Definition?.ConsumableRewardPool);
        return result.Count > 0
            ? result
            : BuildConsumableRewardCandidates(BattleItemCatalog.GetAll());
    }

    private List<BattleItemSO> BuildConsumableRewardCandidates(
        IReadOnlyList<BattleItemSO> source)
    {
        List<BattleItemSO> result = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (BattleItemSO item in source)
            {
                if (item != null && item.IsDisposable &&
                    CanAcquireBattleItem(item) && ids.Add(item.ItemId))
                {
                    result.Add(item);
                }
            }
        }
        return result;
    }

    private DungeonShieldRecoveryRule ResolveShieldRecoveryRule()
    {
        BattleSO battle = GetCurrentBattleRewardDefinition();
        return battle != null && battle.OverrideShieldRecoveryReward
            ? battle.ShieldRecoveryReward
            : _session.Definition?.ShieldRecoveryReward;
    }

    private BattleSO GetCurrentBattleRewardDefinition()
    {
        int index = Mathf.Max(0, _session.CurrentBattleNumber - 1);
        if (_session.Definition != null &&
            _session.Definition.TryGetFixedBattle(index, out BattleSO battle))
        {
            return battle;
        }
        return firstBattle;
    }

    private bool CanUseBattleItem(BattleItemSO item)
    {
        return item != null &&
               _session.IsActive &&
               _session.Activity == EDungeonRunActivity.Battle &&
               TryGetBattleItemState(item, out BattleItemRunState state) &&
               state.CanUse(item) &&
               _battleManager != null &&
               _battleManager.CanSpend(item.EnergyCost);
    }

    public bool TryBeginBattleCardUse(BattleCardInstance instance)
    {
        if (!UsesBattleCards || instance?.Definition == null ||
            !_session.IsActive ||
            _session.Activity != EDungeonRunActivity.Battle ||
            _battleManager == null ||
            _battleManager.State != EBattleState.Running ||
            _battleManager.IsManualTargetSelectionPending ||
            !_battleCardDeck.CanPlay(instance) ||
            !_battleManager.CanSpend(instance.Definition.EnergyCost))
        {
            return false;
        }

        BattleCardSO card = instance.Definition;
        CharacterRuntime source = ResolveBattleCardSource(card);
        bool usesCharacterUser =
            card.Affiliation != BattleCardAffiliation.Neutral;
        if ((usesCharacterUser && (source == null || !source.IsAlive)) ||
            board == null)
            return false;

        if (!BattleAbilityRules.RequiresActionTargets(card))
        {
            return ExecuteBattleCard(
                instance,
                source,
                Array.Empty<EnemyRuntime>(),
                Array.Empty<IBattleCharacter>());
        }

        CharacterTargetFaction faction = card.TargetFaction;
        IReadOnlyList<CharacterNumericCondition> noConditions =
            Array.Empty<CharacterNumericCondition>();
        if (card.Subject == CharacterAttackSubject.Manual ||
            card.AreaDefinition?.UsesWorldArea == true)
        {
            if (board is not IBattleManualTargetSelectionService service)
                return false;

            bool usesArea = card.AreaDefinition?.UsesWorldArea == true;
            CharacterAttackSubject candidateSubject = usesArea &&
                card.Subject != CharacterAttackSubject.Manual &&
                card.Subject != CharacterAttackSubject.None
                    ? card.Subject
                    : CharacterAttackSubject.All;
            int candidateTargetCount = usesArea ? int.MaxValue : 1;
            IReadOnlyList<EnemyRuntime> enemyCandidates =
                faction == CharacterTargetFaction.Enemy
                    ? board.SelectCharacterTargets(
                        source,
                        candidateSubject,
                        card.SubjectMetric,
                        candidateTargetCount,
                        CharacterConditionMatchMode.All,
                        noConditions)
                    : Array.Empty<EnemyRuntime>();
            IReadOnlyList<IBattleCharacter> allyCandidates =
                faction == CharacterTargetFaction.Ally
                    ? board.SelectAlliedCharacters(
                        source,
                        candidateSubject,
                        card.SubjectMetric,
                        candidateTargetCount,
                        CharacterConditionMatchMode.All,
                        noConditions)
                    : Array.Empty<IBattleCharacter>();
            int candidateCount = faction == CharacterTargetFaction.Ally
                ? allyCandidates.Count
                : enemyCandidates.Count;
            if (candidateCount == 0)
                return false;

            BattleManualTargetSelectionRequest request = new(
                source,
                faction,
                card.TargetCount,
                enemyCandidates,
                allyCandidates,
                true,
                result => HandleBattleCardTargetSelection(
                    instance,
                    source,
                    result),
                card.AreaDefinition,
                card.Subject,
                card.SubjectMetric,
                BattleManualAreaPlacementMode.FreePointer);
            return service.TryBeginManualTargetSelection(request);
        }

        IReadOnlyList<EnemyRuntime> enemyTargets =
            faction == CharacterTargetFaction.Enemy
                ? board.SelectCharacterTargets(
                    source,
                    card.Subject,
                    card.SubjectMetric,
                    card.TargetCount,
                    CharacterConditionMatchMode.All,
                    noConditions)
                : Array.Empty<EnemyRuntime>();
        IReadOnlyList<IBattleCharacter> allyTargets =
            faction == CharacterTargetFaction.Ally
                ? board.SelectAlliedCharacters(
                    source,
                    card.Subject,
                    card.SubjectMetric,
                    card.TargetCount,
                    CharacterConditionMatchMode.All,
                    noConditions)
                : Array.Empty<IBattleCharacter>();
        return ExecuteBattleCard(
            instance,
            source,
            enemyTargets,
            allyTargets);
    }

    public bool TryMulliganBattleCards()
    {
        return UsesBattleCards &&
               _battleManager != null &&
               _battleManager.State == EBattleState.Running &&
               !_battleManager.IsManualTargetSelectionPending &&
               _battleCardDeck.TryMulligan();
    }

    private void HandleBattleCardTargetSelection(
        BattleCardInstance instance,
        CharacterRuntime source,
        BattleManualTargetSelectionResult result)
    {
        if (result.Cancelled || !result.HasTargets)
            return;
        ExecuteBattleCard(
            instance,
            source,
            result.EnemyTargets,
            result.AllyTargets);
    }

    private bool ExecuteBattleCard(
        BattleCardInstance instance,
        CharacterRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets)
    {
        BattleCardSO card = instance?.Definition;
        bool usesCharacterUser = card != null &&
            card.Affiliation != BattleCardAffiliation.Neutral;
        if (card == null ||
            (usesCharacterUser && (source == null || !source.IsAlive)) ||
            !_battleCardDeck.CanPlay(instance) ||
            _battleManager == null || board == null ||
            !_battleManager.TrySpend(card.EnergyCost))
        {
            return false;
        }

        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            source,
            board,
            _battleManager,
            card.TargetFaction,
            enemyTargets,
            allyTargets,
            source?.CurrentAttackPower ?? 0f,
            _battleCardDeck,
            usesCharacterUser);
        BattleEffectResult result = BattleEffectExecutor.ExecuteAbility(
            context,
            card,
            usesCharacterUser ? source.Data : null);
        if (!result.Succeeded)
        {
            _battleManager.TryGain(card.EnergyCost);
            return false;
        }

        return _battleCardDeck.CompleteSuccessfulPlay(instance);
    }

    private CharacterRuntime ResolveBattleCardSource(BattleCardSO card)
    {
        if (card == null)
            return null;

        if (card.Affiliation == BattleCardAffiliation.CharacterExclusive ||
            card.SourcePolicy == BattleCardSourcePolicy.FixedCharacter)
        {
            return FindLivingCharacter(card.OwnerCharacter);
        }

        if (card.SourcePolicy ==
            BattleCardSourcePolicy.FirstRequiredCharacter)
        {
            foreach (CharacterSO required in card.RequiredCharacters)
            {
                CharacterRuntime resolved = FindLivingCharacter(required);
                if (resolved != null)
                    return resolved;
            }
        }

        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character != null && character.IsAlive)
                return character;
        }
        return null;
    }

    private CharacterRuntime FindLivingCharacter(CharacterSO definition)
    {
        if (definition == null)
            return null;
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character != null && character.IsAlive &&
                ReferenceEquals(character.Definition, definition))
            {
                return character;
            }
        }
        return null;
    }

    private void PrepareBattleCardDeck(int battleSeed)
    {
        if (!UsesBattleCards)
        {
            _battleCardDeck.Clear();
            return;
        }

        IReadOnlyList<CharacterSO> party = GetCurrentPartyDefinitions();
        List<BattleCardSO> resolvedDeck = new();
        _session.Definition.BattleCardDeckRules.BuildDeck(
            party,
            resolvedDeck);
        foreach (BattleCardSO acquired in _acquiredBattleCards)
        {
            if (acquired != null && acquired.IsEligible(party))
                resolvedDeck.Add(acquired);
        }
        BattleCardDeckRules deckRules =
            _session.Definition.BattleCardDeckRules;
        int partyKnowledge = ResolveParticipatingPartyKnowledge();
        _battleCardDeck.ConfigureResolvedDeck(
            deckRules,
            resolvedDeck,
            battleSeed ^ unchecked((int)0xCA4D51A7),
            deckRules.ResolveCardsDrawnPerTurn(
                ResolveParticipatingPartyJudgment()),
            deckRules.ResolveRedrawCooldown(partyKnowledge),
            deckRules.ResolveMulliganCooldown(partyKnowledge));
        if (!_battleCardDeck.BeginBattle())
        {
            Debug.LogError(
                "Battle card deck has no eligible cards. Configure the " +
                "dungeon deck or create eligible BattleCardSO assets.",
                this);
        }
    }

    private IReadOnlyList<CharacterSO> GetCurrentPartyDefinitions()
    {
        List<CharacterSO> party = new(_ownedTurrets.Count);
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character?.Definition != null)
                party.Add(character.Definition);
        }
        return party;
    }

    private int ResolveParticipatingPartyJudgment()
    {
        int total = 0;
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character == null || !character.CanParticipate)
                continue;

            long next = (long)total + (character.Data?.Judgment ?? 0);
            total = next >= int.MaxValue ? int.MaxValue : (int)next;
        }
        return total;
    }

    private int ResolveParticipatingPartyKnowledge()
    {
        int total = 0;
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character == null || !character.CanParticipate)
                continue;

            long next = (long)total + (character.Data?.Knowledge ?? 0);
            total = next >= int.MaxValue ? int.MaxValue : (int)next;
        }
        return total;
    }

    private bool CompleteBattleItemUse(BattleItemSO item)
    {
        if (!TryGetBattleItemState(item, out BattleItemRunState state) ||
            !state.CompleteSuccessfulUse(item))
        {
            return false;
        }

        BattleItemsChanged?.Invoke();
        return true;
    }

    private BattleItemRunState GetOrCreateBattleItemState(BattleItemSO item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            return null;

        if (_battleItems.TryGetValue(
                item.ItemId,
                out BattleItemRunState state))
        {
            return state;
        }

        state = new BattleItemRunState(item);
        _battleItems.Add(item.ItemId, state);
        return state;
    }

    private bool TryGetBattleItemState(
        BattleItemSO item,
        out BattleItemRunState state)
    {
        state = null;
        return item != null &&
               !string.IsNullOrWhiteSpace(item.ItemId) &&
               _battleItems.TryGetValue(item.ItemId, out state);
    }

    private void TickBattleItemCooldowns(float deltaTime)
    {
        foreach (BattleItemRunState state in _battleItems.Values)
            state.TickCooldown(deltaTime);
    }

    private void ResetBattleItemCooldowns()
    {
        foreach (BattleItemRunState state in _battleItems.Values)
        {
            BattleItemSO item = BattleItemCatalog.Get(state.ItemId);
            if (item != null)
                state.BeginBattle(item);
            else
                state.ResetCooldown();
        }
        BattleItemsChanged?.Invoke();
    }

    private void ResetRunResourcesAndItems(
        DungeonDefinition definition = null)
    {
        foreach (CharacterRuntime turret in _ownedTurrets)
        {
            turret?.Data?.ClearModifierScope(
                CharacterModifierLifetimeScope.Battle);
            turret?.Data?.ClearModifierScope(
                CharacterModifierLifetimeScope.Dungeon);
        }
        _maximumEnergy = BattleManager.DefaultMaximumEnergy;
        _energyRechargeDuration = definition != null
            ? definition.ActiveSkillCostRecoveryDuration
            : DungeonDefinition.DefaultActiveSkillCostRecoveryDuration;
        _battleItems.Clear();
        _acquiredBattleCards.Clear();
        _battleCardDeck.Clear();
        _battleManager?.ConfigureActiveSkillResource(
            _maximumEnergy,
            _energyRechargeDuration);
        BattleItemsChanged?.Invoke();
    }

    private bool PrepareStartingCharacterSelection()
    {
        _startingCharacterChoices.Clear();
        if (_availableTurrets.Count < StartingCharacterChoiceCount)
        {
            Debug.LogError(
                $"DungeonPage requires at least {StartingCharacterChoiceCount} " +
                "different character definitions for the starting choice.",
                this);
            return false;
        }

        List<CharacterSO> candidates = new(_availableTurrets);
        int randomSeed = _battlePlans.Length > 0
            ? _battlePlans[0].RandomSeed ^ StartingChoiceSeedSalt
            : Environment.TickCount;
        System.Random random = new(randomSeed);
        for (int index = 0; index < StartingCharacterChoiceCount; index++)
        {
            int swapIndex = random.Next(index, candidates.Count);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
            _startingCharacterChoices.Add(candidates[index]);
        }

        _startingCharacterSelectionPending = true;
        flowController.ShowEventTab();
        _eventTab?.ShowStartingCharacterSelection(
            _startingCharacterChoices);
        if (_session.Definition != null &&
            _session.Definition.HasTutorial)
        {
            _tutorialController?.BeginStartingChoice(
                _session.Definition.Tutorial,
                _eventTab?.FirstStartingChoiceRect);
        }
        return true;
    }

    private void InitializePlayerCharacters()
    {
        EnsurePlayerCharacterSlots();
        EnsurePartySlotColors();
        IReadOnlyList<CharacterSO> catalog =
            CharacterDefinitionCatalog.GetAll();
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null)
                continue;

            CharacterSO definition = character.Definition;
            if (definition == null && index < catalog.Count)
            {
                definition = catalog[index];
                character.ConfigureDefinition(definition);
            }

            if (definition == null)
            {
                _slotDefaultDefinitions[index] = null;
                character.ConfigurePartySlot(index, partySlotColors[index]);
                character.gameObject.SetActive(false);
                continue;
            }

            if (!character.Initialize())
            {
                Debug.LogError(
                    $"Player party slot {index + 1} is not configured.",
                    character);
                continue;
            }

            character.ConfigurePartySlot(index, partySlotColors[index]);
            _slotDefaultDefinitions[index] = definition;
        }

        RefreshAvailableCharacterDefinitions();
        bool hasCharacter = _availableTurrets.Count > 0;
        if (!hasCharacter)
        {
            Debug.LogError(
                "DungeonPage requires at least one owned player character.",
                this);
        }
        else
            ClearPlayerParty();
    }

    private void RefreshAvailableCharacterDefinitions()
    {
        _availableTurrets.Clear();
        AddOwnedCharacterDefinitions(
            CharacterDefinitionCatalog.GetAll());
        if (playerCharacters == null)
            return;

        foreach (CharacterRuntime character in playerCharacters)
        {
            CharacterSO definition = character != null
                ? character.Definition
                : null;
            if (IsCharacterOwnedForDungeon(definition) &&
                !_availableTurrets.Contains(definition))
            {
                _availableTurrets.Add(definition);
            }
        }
    }

    private void AddOwnedCharacterDefinitions(
        IReadOnlyList<CharacterSO> definitions)
    {
        if (definitions == null)
            return;

        foreach (CharacterSO definition in definitions)
        {
            if (IsCharacterOwnedForDungeon(definition) &&
                !_availableTurrets.Contains(definition))
            {
                _availableTurrets.Add(definition);
            }
        }
    }

    internal static bool IsCharacterOwnedForDungeon(
        CharacterSO definition,
        CharacterCollectionData collection = null)
    {
        if (definition == null)
            return false;

        collection ??= DataManager.Current?.CharacterDatas;
        if (collection == null)
            return definition.InitiallyOwned;

        CharacterData data =
            collection.CreatePreviewData(definition);
        return data != null && data.IsOwned;
    }

    private void ClearPlayerParty()
    {
        _ownedTurrets.Clear();
        _acquiredCharacterIds.Clear();
        _startingTurret = null;
        _startingCharacterSelectionPending = false;
        _startingItemSelectionPending = false;
        _startingCharacterChoices.Clear();
        _startingItemSelection.Clear();
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null)
                continue;

            CharacterSO definition = _slotDefaultDefinitions[index];
            if (definition != null)
                character.ConfigureDefinition(definition);
            character.ConfigurePartySlot(index, partySlotColors[index]);
            character.gameObject.SetActive(false);
        }
    }

    private bool CanAcquireCharacterReward(CharacterSO definition)
    {
        return IsCharacterOwnedForDungeon(definition) &&
               !HasAcquiredCharacter(definition);
    }

    private bool HasAcquiredCharacter(CharacterSO definition)
    {
        if (definition == null)
            return false;

        string characterId = definition.CharacterId;
        if (!string.IsNullOrWhiteSpace(characterId) &&
            _acquiredCharacterIds.Contains(characterId))
        {
            return true;
        }

        foreach (CharacterRuntime character in _ownedTurrets)
        {
            CharacterSO ownedDefinition = character?.Definition;
            if (ReferenceEquals(ownedDefinition, definition) ||
                ownedDefinition != null &&
                string.Equals(
                    ownedDefinition.CharacterId,
                    characterId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordAcquiredCharacter(CharacterSO definition)
    {
        string characterId = definition?.CharacterId;
        if (!string.IsNullOrWhiteSpace(characterId))
            _acquiredCharacterIds.Add(characterId);
    }

    private void InitializeEventTab()
    {
        GameObject eventTabObject = flowController != null
            ? flowController.EventTab
            : null;
        if (eventTabObject == null)
            return;

        _eventTab ??= new DungeonEventTab();
        _eventTab.Initialize(eventTabObject, this);
    }

    private void InitializeBattleRewardOverlay()
    {
        if (_battleRewardOverlayRoot == null)
        {
            Debug.LogError(
                "Dungeon battle reward overlay must be placed in the " +
                "Scene and assigned in the inspector.",
                this);
            return;
        }

        _battleRewardOverlay ??= new DungeonEventTab();
        _battleRewardOverlay.Initialize(
            _battleRewardOverlayRoot,
            this,
            true);
        _battleRewardOverlayRoot.SetActive(false);
    }

    private void InitializeRoomViews()
    {
        if (flowController == null)
            return;

        _eventRoomView ??= new DungeonRoomView();
        _eventRoomView.Initialize(
            flowController.EventTab,
            this,
            EDungeonPhase.Event);
        _restRoomView ??= new DungeonRoomView();
        _restRoomView.Initialize(
            flowController.RestTab,
            this,
            EDungeonPhase.Rest);
        _shopRoomView ??= new DungeonRoomView();
        _shopRoomView.Initialize(
            flowController.ShopTab,
            this,
            EDungeonPhase.Shop);
        HideRoomViews();
    }

    private void ShowBattleRewardOverlay()
    {
        InitializeBattleRewardOverlay();
        if (_battleRewardOverlayRoot == null)
            return;

        _battleRewardOverlayRoot.SetActive(true);
        _battleRewardOverlay?.ShowUpgradeEvent();
    }

    private void HideBattleRewardOverlay()
    {
        if (_battleRewardOverlayRoot != null)
            _battleRewardOverlayRoot.SetActive(false);
    }

    private void HideRoomViews()
    {
        _eventRoomView?.Hide();
        _restRoomView?.Hide();
        _shopRoomView?.Hide();
    }

    private void EnsureTutorialController()
    {
        if (_tutorialController == null)
            _tutorialController = GetComponent<DungeonTutorialController>();
        if (_tutorialController == null)
        {
            Debug.LogError(
                "DungeonTutorialController must be placed on DungeonPage.",
                this);
            return;
        }

        _tutorialController.Initialize(this, fieldView);
    }

    private void EnsureFieldView()
    {
        if (fieldView == null)
            fieldView = GetComponent<DungeonFieldView>();
        if (fieldView == null)
        {
            Debug.LogError(
                "DungeonFieldView must be placed on DungeonPage.",
                this);
            return;
        }

        fieldView.BindSceneStructure(
            board,
            flowController,
            battleTab,
            playerCharacters);
    }

    private void ApplyBattlePauseState()
    {
        if (_battleManager == null || !_battleManager.HasSession ||
            _battleManager.State == EBattleState.Completed)
        {
            return;
        }

        if (_session.Pause.HasBlockingReason)
        {
            _battleManager.SuspendBattle();
            return;
        }

        if (_session.Pause.IsUserPaused)
        {
            if (_battleManager.State == EBattleState.Suspended)
                _battleManager.ResumeBattle();
            RestorePreferredGameSpeed();
            if (_battleManager.State == EBattleState.Running)
                _battleManager.TogglePause();
            return;
        }

        if (_battleManager.State == EBattleState.Suspended ||
            _battleManager.State == EBattleState.Paused)
        {
            _battleManager.ResumeBattle();
        }
        RestorePreferredGameSpeed();
    }

    private void RestorePreferredGameSpeed()
    {
        if (_battleManager == null ||
            _battleManager.State != EBattleState.Running)
        {
            return;
        }

        int guard = 0;
        while (!Mathf.Approximately(
                   _battleManager.GameSpeed,
                   _session.PreferredGameSpeed) && guard < 3)
        {
            _battleManager.CycleGameSpeed();
            guard++;
        }
    }

    private void NavigateToCompletionDestination(
        EDungeonCompletionDestination destination,
        GameObject sourcePage = null)
    {
        GameObject targetPage = destination ==
            EDungeonCompletionDestination.StageSelect
                ? stageSelectPage
                : mainPage;
        string fallbackName = destination ==
            EDungeonCompletionDestination.StageSelect
                ? "pagStageSelect"
                : "pagMain";
        if (targetPage == null && transform.parent != null)
        {
            Transform targetTransform = transform.parent.Find(fallbackName);
            targetPage = targetTransform != null
                ? targetTransform.gameObject
                : null;
        }

        if (targetPage == null)
        {
            Debug.LogError(
                $"DungeonPage could not resolve completion destination " +
                $"'{fallbackName}'.",
                this);
            return;
        }

        PageControl.PagToPag(
            sourcePage != null ? sourcePage : gameObject,
            targetPage,
            PageOpenMode.Resume);
    }

    private void ResetCurrentRunForNavigation()
    {
        _tutorialController?.StopTutorial();
        _battleRewardPending = false;
        HideBattleRewardOverlay();
        HideRoomViews();
        _startingCharacterSelectionPending = false;
        if (_battleManager != null && _battleManager.HasSession)
            _battleManager.EndBattle(board);
        board?.ClearAllStacks();
        ClearPlayerParty();
        ResetRunResourcesAndItems();
        _session.Reset();
        _pendingDefinition = null;
        battleTab?.Refresh();
    }

    private void NotifyRunStarted()
    {
        ForEachModifier(modifier =>
            modifier.OnRunStarted(GetRuntimeContext()));
    }

    private void NotifyPhaseEntered(EDungeonPhase phase)
    {
        ForEachModifier(modifier =>
            modifier.OnPhaseEntered(GetRuntimeContext(), phase));
    }

    private void NotifyBattleStarted()
    {
        ForEachModifier(modifier =>
            modifier.OnBattleStarted(GetRuntimeContext()));
    }

    private void NotifyBattleEnded(EBattleResult result)
    {
        ForEachModifier(modifier =>
            modifier.OnBattleEnded(GetRuntimeContext(), result));
    }

    private void NotifyRunEnded(EDungeonRunResult result)
    {
        RequestDungeonBgm(
            _session.Definition,
            EDungeonBgmState.Ready);

        if (result == EDungeonRunResult.Clear &&
            _session.Definition != null)
        {
            DataManager.Current?.DungeonProgressDatas?.MarkCleared(
                _session.Definition);
        }

        ForEachModifier(modifier =>
            modifier.OnRunEnded(GetRuntimeContext(), result));
    }

    private int GetCurrentRoomIndex(EDungeonPhase phase)
    {
        IReadOnlyList<EDungeonPhase> phases = _session.PhaseSequence;
        if (phases == null || flowController == null)
            return 0;

        int roomCount = 0;
        int end = Mathf.Min(
            flowController.CurrentStepIndex,
            phases.Count - 1);
        for (int index = 0; index <= end; index++)
        {
            if (phases[index] == phase)
                roomCount++;
        }

        return Mathf.Max(0, roomCount - 1);
    }

    private void ShowDungeonRoom(EDungeonPhase phase)
    {
        HideRoomViews();
        _eventTab?.SetPanelVisible(false);
        int roomIndex = GetCurrentRoomIndex(phase);
        DungeonRoomSO room = null;
        DungeonRoomView view = null;
        DungeonDefinition definition = _session.Definition;

        switch (phase)
        {
            case EDungeonPhase.Event:
                view = _eventRoomView;
                if (definition != null &&
                    definition.TryGetFixedEvent(
                        roomIndex,
                        out DungeonEventSO dungeonEvent))
                {
                    room = dungeonEvent;
                }
                break;
            case EDungeonPhase.Rest:
                view = _restRoomView;
                if (definition != null &&
                    definition.TryGetFixedRest(
                        roomIndex,
                        out DungeonRestSO dungeonRest))
                {
                    room = dungeonRest;
                }
                break;
            case EDungeonPhase.Shop:
                view = _shopRoomView;
                if (definition != null &&
                    definition.TryGetFixedShop(
                        roomIndex,
                        out DungeonShopSO dungeonShop))
                {
                    room = dungeonShop;
                }
                break;
        }

        RequestDungeonBgm(
            definition,
            EDungeonBgmState.Rest,
            room != null ? room.BgmOverride : null);
        view?.Show(room, roomIndex);
    }

    internal bool CanUseDungeonRoomChoice(
        EDungeonPhase phase,
        int roomIndex,
        int choiceIndex,
        DungeonRoomChoiceDefinition choice)
    {
        if (!_session.IsActive || CurrentPhase != phase || choice == null ||
            _session.RunCurrency < choice.RunCurrencyCost)
        {
            return false;
        }

        if (phase == EDungeonPhase.Shop && choice.SinglePurchase &&
            IsShopProductSold(roomIndex, choiceIndex))
        {
            return false;
        }

        IReadOnlyList<DungeonRoomConditionDefinition> conditions =
            choice.Conditions;
        for (int index = 0; index < conditions.Count; index++)
        {
            if (!EvaluateDungeonRoomCondition(conditions[index]))
                return false;
        }

        long currency = _session.RunCurrency - choice.RunCurrencyCost;
        IReadOnlyList<DungeonRoomEffectDefinition> effects = choice.Effects;
        HashSet<BattleItemSO> grantedItems = new();
        for (int index = 0; index < effects.Count; index++)
        {
            DungeonRoomEffectDefinition effect = effects[index];
            if (effect == null)
                return false;
            if (effect.EffectType == EDungeonRoomEffectType.RunCurrency)
            {
                currency += effect.Amount;
                if (currency < 0)
                    return false;
            }
            else if (effect.EffectType == EDungeonRoomEffectType.BattleItem &&
                     (!grantedItems.Add(effect.BattleItem) ||
                      !CanAcquireBattleItem(effect.BattleItem)))
            {
                return false;
            }
        }

        return true;
    }

    internal int GetMaximumRestActionCount(
        DungeonRestSO room,
        int roomIndex)
    {
        if (room == null)
            return 0;

        string initializedKey = GetRestActionInitializedStateKey(roomIndex);
        if (_session.State.GetInt(initializedKey) != 0)
        {
            return Mathf.Max(
                0,
                _session.State.GetInt(
                    GetRestActionMaximumStateKey(roomIndex)));
        }

        int maximum = room.BaseActionCount;
        DungeonRuntimeContext context = GetRuntimeContext();
        IReadOnlyList<DungeonModifier> modifiers =
            _session.Definition?.Modifiers;
        if (modifiers != null)
        {
            for (int index = 0; index < modifiers.Count; index++)
            {
                if (modifiers[index] is not
                    IDungeonRestActionAllowanceProvider provider)
                {
                    continue;
                }

                try
                {
                    maximum += Mathf.Max(
                        0,
                        provider.GetAdditionalRestActionCount(
                            context,
                            room,
                            roomIndex));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, modifiers[index]);
                }
            }
        }

        for (int index = 0; index < _ownedTurrets.Count; index++)
        {
            CharacterRestSkillDefinition skill =
                _ownedTurrets[index]?.Definition?.RestSkill;
            if (skill != null && skill.Enabled)
                maximum += skill.AdditionalRoomActions;
        }

        foreach (BattleItemRunState state in _battleItems.Values)
        {
            if (!state.IsOwned)
                continue;
            BattleItemSO item = BattleItemCatalog.Get(state.ItemId);
            if (item != null)
            {
                maximum += item.AdditionalRestActions *
                           Mathf.Max(1, state.OwnedCopies);
            }
        }

        maximum = Mathf.Max(1, maximum);
        _session.State.SetInt(
            GetRestActionMaximumStateKey(roomIndex),
            maximum);
        _session.State.SetInt(initializedKey, 1);
        return maximum;
    }

    internal void GetAvailableRestActions(
        DungeonRestSO room,
        int roomIndex,
        List<DungeonRestActionDefinition> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        if (room == null)
            return;
        for (int index = 0; index < room.Actions.Count; index++)
        {
            if (room.Actions[index] != null)
                results.Add(room.Actions[index]);
        }

        IReadOnlyList<DungeonModifier> modifiers =
            _session.Definition?.Modifiers;
        if (modifiers == null)
            return;

        DungeonRuntimeContext context = GetRuntimeContext();
        for (int index = 0; index < modifiers.Count; index++)
        {
            if (modifiers[index] is not IDungeonRestActionProvider provider)
                continue;

            try
            {
                IReadOnlyList<DungeonRestActionDefinition> additions =
                    provider.GetAdditionalRestActions(
                        context,
                        room,
                        roomIndex);
                if (additions == null)
                    continue;
                for (int actionIndex = 0;
                     actionIndex < additions.Count;
                     actionIndex++)
                {
                    if (additions[actionIndex] != null)
                        results.Add(additions[actionIndex]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, modifiers[index]);
            }
        }
    }

    internal int GetRemainingRestActionCount(
        DungeonRestSO room,
        int roomIndex)
    {
        return Mathf.Max(
            0,
            GetMaximumRestActionCount(room, roomIndex) -
            _session.State.GetInt(GetRestActionUsedStateKey(roomIndex)));
    }

    internal bool CanUseDungeonRestAction(
        DungeonRestSO room,
        int roomIndex,
        int actionIndex,
        DungeonRestActionDefinition action)
    {
        if (room == null || action == null ||
            CurrentPhase != EDungeonPhase.Rest ||
            GetRemainingRestActionCount(room, roomIndex) <= 0 ||
            !CanUseDungeonRoomChoice(
                EDungeonPhase.Rest,
                roomIndex,
                actionIndex,
                action.Choice))
        {
            return false;
        }

        if (action.ActionType != EDungeonRestActionType.UseRestItem)
            return true;

        foreach (BattleItemRunState state in _battleItems.Values)
        {
            BattleItemSO item = BattleItemCatalog.Get(state.ItemId);
            if (state.CanUseInRest(item))
                return true;
        }

        return false;
    }

    internal bool TryUseDungeonRestAction(
        DungeonRestSO room,
        int roomIndex,
        int actionIndex,
        DungeonRestActionDefinition action,
        CharacterRuntime target)
    {
        if (!CanUseDungeonRestAction(
                room,
                roomIndex,
                actionIndex,
                action) ||
            action.ActionType == EDungeonRestActionType.UseRestItem)
        {
            return false;
        }

        bool coreApplied;
        switch (action.ActionType)
        {
            case EDungeonRestActionType.HealSelectedCharacter:
                coreApplied = TryHealRestTarget(
                    target,
                    action.Amount,
                    true,
                    action.AllowRevive);
                break;
            case EDungeonRestActionType.UpgradeSelectedCharacter:
                coreApplied = TryApplyRestUpgrade(target, roomIndex);
                break;
            case EDungeonRestActionType.LegacyImmediate:
                coreApplied = true;
                break;
            default:
                return false;
        }

        if (!coreApplied ||
            !_session.TrySpendRunCurrency(action.Choice.RunCurrencyCost))
        {
            return false;
        }

        IReadOnlyList<DungeonRoomEffectDefinition> effects =
            action.Choice.Effects;
        for (int index = 0; index < effects.Count; index++)
            ApplyDungeonRoomEffect(effects[index]);

        _session.State.SetString(
            $"room:{EDungeonPhase.Rest}:{roomIndex}:choice",
            action.Choice.ChoiceId);
        CompleteRestAction(room, roomIndex);
        return true;
    }

    internal void GetUsableRestItems(List<BattleItemSO> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        foreach (BattleItemRunState state in _battleItems.Values)
        {
            BattleItemSO item = BattleItemCatalog.Get(state.ItemId);
            if (state.CanUseInRest(item))
                results.Add(item);
        }

        results.Sort((left, right) => string.Compare(
            left != null ? left.GetLocalizedDisplayName() : string.Empty,
            right != null ? right.GetLocalizedDisplayName() : string.Empty,
            StringComparison.CurrentCulture));
    }

    internal bool TryUseRestItem(
        DungeonRestSO room,
        int roomIndex,
        int actionIndex,
        DungeonRestActionDefinition action,
        BattleItemSO item,
        CharacterRuntime target)
    {
        if (room == null || item == null || target == null ||
            action?.ActionType != EDungeonRestActionType.UseRestItem ||
            !CanUseDungeonRestAction(
                room,
                roomIndex,
                actionIndex,
                action) ||
            !TryGetBattleItemState(item, out BattleItemRunState state) ||
            !state.CanUseInRest(item) ||
            !TryApplyRestTargetEffects(
                item.RestEffects,
                target,
                roomIndex))
        {
            return false;
        }

        if (!_session.TrySpendRunCurrency(action.Choice.RunCurrencyCost))
            return false;

        IReadOnlyList<DungeonRoomEffectDefinition> choiceEffects =
            action.Choice.Effects;
        for (int index = 0; index < choiceEffects.Count; index++)
            ApplyDungeonRoomEffect(choiceEffects[index]);

        if (!state.CompleteSuccessfulRestUse(item))
            return false;

        BattleItemsChanged?.Invoke();
        _session.State.SetString(
            $"rest:{roomIndex}:item",
            item.ItemId);
        CompleteRestAction(room, roomIndex);
        return true;
    }

    internal bool CanUseCharacterRestSkill(
        DungeonRestSO room,
        int roomIndex,
        CharacterRuntime target)
    {
        CharacterRestSkillDefinition skill =
            target?.Definition?.RestSkill;
        return room != null && target != null && skill != null &&
               skill.IsUsable && CurrentPhase == EDungeonPhase.Rest &&
               GetRemainingRestActionCount(room, roomIndex) > 0 &&
               _session.State.GetInt(GetRestSkillUseStateKey(
                   roomIndex,
                   target.Definition.CharacterId,
                   skill.SkillId)) < skill.UsesPerRoom;
    }

    internal bool TryUseCharacterRestSkill(
        DungeonRestSO room,
        int roomIndex,
        CharacterRuntime target)
    {
        if (!CanUseCharacterRestSkill(room, roomIndex, target))
            return false;

        CharacterRestSkillDefinition skill = target.Definition.RestSkill;
        if (!TryApplyRestTargetEffects(skill.Effects, target, roomIndex))
            return false;

        string useKey = GetRestSkillUseStateKey(
            roomIndex,
            target.Definition.CharacterId,
            skill.SkillId);
        _session.State.SetInt(
            useKey,
            _session.State.GetInt(useKey) + 1);
        _session.State.SetString(
            $"rest:{roomIndex}:skill",
            $"{target.Definition.CharacterId}:{skill.SkillId}");
        CompleteRestAction(room, roomIndex);
        return true;
    }

    private bool EvaluateDungeonRoomCondition(
        DungeonRoomConditionDefinition condition)
    {
        if (condition == null)
            return false;

        switch (condition.ConditionType)
        {
            case EDungeonRoomConditionType.MinimumRunCurrency:
                return _session.RunCurrency >= condition.Amount;
            case EDungeonRoomConditionType.PartyHasInjuredMember:
                foreach (CharacterRuntime character in _ownedTurrets)
                {
                    if (character != null &&
                        character.CurrentHealth < character.MaximumHealth)
                    {
                        return true;
                    }
                }
                return false;
            case EDungeonRoomConditionType.OwnsBattleItem:
                return IsBattleItemOwned(condition.BattleItem);
            case EDungeonRoomConditionType.DoesNotOwnBattleItem:
                return !IsBattleItemOwned(condition.BattleItem);
            default:
                return false;
        }
    }

    internal bool TryUseDungeonRoomChoice(
        EDungeonPhase phase,
        int roomIndex,
        int choiceIndex,
        DungeonRoomChoiceDefinition choice)
    {
        if (!CanUseDungeonRoomChoice(
                phase,
                roomIndex,
                choiceIndex,
                choice) ||
            !_session.TrySpendRunCurrency(choice.RunCurrencyCost))
        {
            return false;
        }

        IReadOnlyList<DungeonRoomEffectDefinition> effects = choice.Effects;
        for (int index = 0; index < effects.Count; index++)
            ApplyDungeonRoomEffect(effects[index]);

        if (phase == EDungeonPhase.Shop)
        {
            if (choice.SinglePurchase)
            {
                _session.State.SetInt(
                    GetShopProductStateKey(roomIndex, choiceIndex),
                    1);
            }
            return true;
        }

        _session.State.SetString(
            $"room:{phase}:{roomIndex}:choice",
            choice.ChoiceId);
        CompleteDungeonRoom();
        return true;
    }

    internal void GetActiveDungeonEventChoices(
        DungeonEventSO dungeonEvent,
        int roomIndex,
        List<DungeonEventChoiceNodeDefinition> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        if (dungeonEvent == null)
            return;

        string activeIds = _session.State.GetString(
            GetEventActiveChoicesStateKey(roomIndex));
        if (string.IsNullOrWhiteSpace(activeIds))
        {
            dungeonEvent.GetEntryChoices(results);
            return;
        }

        string[] ids = activeIds.Split(
            new[] { '|' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < ids.Length; index++)
        {
            if (dungeonEvent.TryGetChoiceNode(ids[index], out var node))
                results.Add(node);
        }
    }

    internal bool TryUseDungeonEventChoice(
        DungeonEventSO dungeonEvent,
        int roomIndex,
        DungeonEventChoiceNodeDefinition node)
    {
        if (dungeonEvent == null || node == null ||
            CurrentPhase != EDungeonPhase.Event ||
            !dungeonEvent.UsesChoiceGraph ||
            !IsDungeonEventChoiceActive(
                dungeonEvent,
                roomIndex,
                node.NodeId))
        {
            return false;
        }

        string visitKey = GetEventNodeVisitStateKey(
            roomIndex,
            node.NodeId);
        if (_session.State.GetInt(visitKey) > 0)
        {
            Debug.LogError(
                $"Dungeon event '{dungeonEvent.EventId}' attempted to " +
                $"visit choice node '{node.NodeId}' more than once.",
                this);
            return false;
        }

        int choiceIndex = dungeonEvent.FindChoiceIndex(node.NodeId);
        if (choiceIndex < 0 || !CanUseDungeonRoomChoice(
                EDungeonPhase.Event,
                roomIndex,
                choiceIndex,
                node) ||
            !_session.TrySpendRunCurrency(node.RunCurrencyCost))
        {
            return false;
        }

        IReadOnlyList<DungeonRoomEffectDefinition> effects = node.Effects;
        for (int index = 0; index < effects.Count; index++)
            ApplyDungeonRoomEffect(effects[index]);

        _session.State.SetInt(visitKey, 1);
        _session.State.SetString(
            GetEventLastChoiceStateKey(roomIndex),
            node.NodeId);
        if (node.EndsEvent)
        {
            _session.State.SetString(
                GetEventActiveChoicesStateKey(roomIndex),
                string.Empty);
            CompleteDungeonRoom();
            return true;
        }

        _session.State.SetString(
            GetEventActiveChoicesStateKey(roomIndex),
            string.Join("|", node.NextChoiceNodeIds));
        return true;
    }

    internal string GetDungeonEventResultDescription(
        DungeonEventSO dungeonEvent,
        int roomIndex)
    {
        if (dungeonEvent == null)
            return string.Empty;

        string nodeId = _session.State.GetString(
            GetEventLastChoiceStateKey(roomIndex));
        return dungeonEvent.TryGetChoiceNode(nodeId, out var node)
            ? node.ResultDescription
            : string.Empty;
    }

    private bool IsDungeonEventChoiceActive(
        DungeonEventSO dungeonEvent,
        int roomIndex,
        string nodeId)
    {
        string activeIds = _session.State.GetString(
            GetEventActiveChoicesStateKey(roomIndex));
        if (string.IsNullOrWhiteSpace(activeIds))
            return dungeonEvent.IsEntryChoice(nodeId);

        string[] ids = activeIds.Split(
            new[] { '|' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < ids.Length; index++)
        {
            if (string.Equals(ids[index], nodeId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ApplyDungeonRoomEffect(DungeonRoomEffectDefinition effect)
    {
        switch (effect.EffectType)
        {
            case EDungeonRoomEffectType.RunCurrency:
                _session.AddRunCurrency(effect.Amount);
                break;
            case EDungeonRoomEffectType.HealPartyFlat:
                HealParty(effect.Amount, false);
                break;
            case EDungeonRoomEffectType.HealPartyPercent:
                HealParty(effect.Amount, true);
                break;
            case EDungeonRoomEffectType.MaximumEnergy:
                _maximumEnergy = Mathf.Max(
                    1,
                    _maximumEnergy + effect.Amount);
                _battleManager?.ConfigureActiveSkillResource(
                    _maximumEnergy,
                    _energyRechargeDuration);
                break;
            case EDungeonRoomEffectType.RechargeSpeed:
                _energyRechargeDuration = TimePrecision.Normalize(
                    _energyRechargeDuration -
                    EnergyRechargeUpgradeAmount * effect.Amount,
                    MinimumEnergyRechargeDuration);
                _battleManager?.ConfigureActiveSkillResource(
                    _maximumEnergy,
                    _energyRechargeDuration);
                break;
            case EDungeonRoomEffectType.BattleItem:
                AcquireBattleItemInternal(effect.BattleItem);
                break;
        }
    }

    private bool TryApplyRestTargetEffects(
        IReadOnlyList<DungeonRestTargetEffectDefinition> effects,
        CharacterRuntime target,
        int roomIndex)
    {
        if (effects == null || effects.Count == 0 || target == null)
            return false;

        bool applied = false;
        for (int index = 0; index < effects.Count; index++)
        {
            DungeonRestTargetEffectDefinition effect = effects[index];
            if (effect == null)
                continue;

            switch (effect.EffectType)
            {
                case EDungeonRestTargetEffectType.HealFlat:
                    applied |= TryHealRestTarget(
                        target,
                        effect.Amount,
                        false,
                        effect.AllowRevive);
                    break;
                case EDungeonRestTargetEffectType.HealPercent:
                    applied |= TryHealRestTarget(
                        target,
                        effect.Amount,
                        true,
                        effect.AllowRevive);
                    break;
                case EDungeonRestTargetEffectType.DungeonUpgrade:
                    for (int count = 0; count < effect.Amount; count++)
                        applied |= TryApplyRestUpgrade(target, roomIndex);
                    break;
                case EDungeonRestTargetEffectType.AddRoomAction:
                    AddRestActionAllowance(roomIndex, effect.Amount);
                    applied = true;
                    break;
            }
        }

        return applied;
    }

    private static bool TryHealRestTarget(
        CharacterRuntime target,
        int amount,
        bool percentage,
        bool allowRevive)
    {
        if (target == null || amount <= 0 ||
            target.CurrentHealth >= target.MaximumHealth ||
            (target.CurrentHealth <= 0 && !allowRevive))
        {
            return false;
        }

        int healAmount = percentage
            ? Mathf.CeilToInt(target.MaximumHealth * amount / 100f)
            : amount;
        return target.RestoreHealth(healAmount, allowRevive) > 0;
    }

    private bool TryApplyRestUpgrade(
        CharacterRuntime target,
        int roomIndex)
    {
        CharacterData data = target?.Data;
        if (data == null)
            return false;

        int used = _session.State.GetInt(
            GetRestActionUsedStateKey(roomIndex));
        int characterHash = StableHash(
            target.Definition != null
                ? target.Definition.CharacterId
                : target.name);
        System.Random random = new(
            _session.RunSeed ^
            unchecked(roomIndex * 486187739) ^
            unchecked(used * 16777619) ^
            characterHash);

        for (int definitionIndex = 0;
             definitionIndex < data.DungeonUpgradeDefinitions.Count;
             definitionIndex++)
        {
            if (data.TryRollDungeonUpgrade(
                    definitionIndex,
                    random,
                    out string upgradeId) &&
                target.ApplyDungeonUpgrade(definitionIndex, upgradeId))
            {
                return true;
            }
        }

        return false;
    }

    private void CompleteRestAction(
        DungeonRestSO room,
        int roomIndex)
    {
        string usedKey = GetRestActionUsedStateKey(roomIndex);
        _session.State.SetInt(
            usedKey,
            _session.State.GetInt(usedKey) + 1);
        if (GetRemainingRestActionCount(room, roomIndex) <= 0)
            CompleteDungeonRoom();
    }

    private void AddRestActionAllowance(int roomIndex, int amount)
    {
        if (amount <= 0)
            return;

        string maximumKey = GetRestActionMaximumStateKey(roomIndex);
        _session.State.SetInt(
            maximumKey,
            Mathf.Max(0, _session.State.GetInt(maximumKey)) + amount);
        _session.State.SetInt(
            GetRestActionInitializedStateKey(roomIndex),
            1);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619;
            }
            return hash;
        }
    }

    private void HealParty(int amount, bool percentage)
    {
        foreach (CharacterRuntime character in _ownedTurrets)
        {
            if (character == null)
                continue;
            int healAmount = percentage
                ? Mathf.CeilToInt(character.MaximumHealth * amount / 100f)
                : amount;
            character.RestoreHealth(healAmount, true);
        }
    }

    internal bool IsShopProductSold(int roomIndex, int productIndex)
    {
        return _session.State.GetInt(
                   GetShopProductStateKey(roomIndex, productIndex)) != 0;
    }

    internal void CompleteDungeonRoom()
    {
        if (CurrentPhase == EDungeonPhase.Battle)
            return;

        flowController?.TryAdvance();
    }

    private static string GetShopProductStateKey(
        int roomIndex,
        int productIndex)
    {
        return $"shop:{roomIndex}:product:{productIndex}:sold";
    }

    private static string GetRestActionInitializedStateKey(int roomIndex)
    {
        return $"rest:{roomIndex}:actions:initialized";
    }

    private static string GetRestActionMaximumStateKey(int roomIndex)
    {
        return $"rest:{roomIndex}:actions:maximum";
    }

    private static string GetRestActionUsedStateKey(int roomIndex)
    {
        return $"rest:{roomIndex}:actions:used";
    }

    private static string GetRestSkillUseStateKey(
        int roomIndex,
        string characterId,
        string skillId)
    {
        return $"rest:{roomIndex}:skill:{characterId}:{skillId}:used";
    }

    private static string GetEventActiveChoicesStateKey(int roomIndex)
    {
        return $"event:{roomIndex}:active";
    }

    private static string GetEventLastChoiceStateKey(int roomIndex)
    {
        return $"event:{roomIndex}:last-choice";
    }

    private static string GetEventNodeVisitStateKey(
        int roomIndex,
        string nodeId)
    {
        return $"event:{roomIndex}:visited:{nodeId}";
    }

    private static void RequestDungeonBgm(
        DungeonDefinition definition,
        EDungeonBgmState state,
        AudioClip overrideClip = null)
    {
        AudioManager audioManager = GameManager.Instance?.Audio;
        DungeonBgmProfile profile = definition != null
            ? definition.BgmProfile
            : null;
        if (audioManager == null || profile == null)
            return;

        audioManager.PlayDungeonBgm(profile, state, overrideClip);
    }

    private DungeonRuntimeContext GetRuntimeContext()
    {
        if (_runtimeContext == null ||
            !ReferenceEquals(_runtimeContext.BattleManager, _battleManager) ||
            !ReferenceEquals(_runtimeContext.FieldView, fieldView))
        {
            _runtimeContext = new DungeonRuntimeContext(
                _session,
                this,
                fieldView,
                _battleManager);
        }

        return _runtimeContext;
    }

    private void ForEachModifier(Action<DungeonModifier> callback)
    {
        if (callback == null || _session.Definition == null)
            return;

        IReadOnlyList<DungeonModifier> modifiers =
            _session.Definition.Modifiers;
        for (int index = 0; index < modifiers.Count; index++)
        {
            DungeonModifier modifier = modifiers[index];
            if (modifier == null)
                continue;

            try
            {
                callback(modifier);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, modifier);
            }
        }
    }

    private void EnsurePlayerCharacterSlots()
    {
        if (playerCharacters == null)
            playerCharacters = new CharacterRuntime[MaximumPartySize];
        else if (playerCharacters.Length != MaximumPartySize)
            System.Array.Resize(ref playerCharacters, MaximumPartySize);
    }

    private void EnsureCharacterInfoInstances()
    {
        if (_characterInfoInstancesPrepared)
            return;

        EnsurePlayerCharacterSlots();
        CharacterRuntime prefab = characterInfoPrefab;
        if (prefab == null)
        {
            Debug.LogError(
                "Character info prefab is not assigned on DungeonPage.",
                this);
            return;
        }

        Transform slotParent = playerCharacterRoot;

        if (slotParent == null && battleTab != null)
        {
            slotParent = battleTab.transform.Find(
                "grpPlayerPartyInfo/grpPlayerPartySlots");
        }

        if (slotParent == null)
        {
            Debug.LogError(
                "DungeonPage could not resolve the player character info container.",
                this);
            return;
        }

        playerCharacterRoot = slotParent as RectTransform;

        CharacterSO[] definitions =
            new CharacterSO[MaximumPartySize];
        CharacterRuntime[] previousSlots = playerCharacters;
        for (int index = 0; index < previousSlots.Length; index++)
        {
            if (previousSlots[index] != null)
                definitions[index] = previousSlots[index].Definition;
        }

        CharacterRuntime[] instances =
            new CharacterRuntime[MaximumPartySize];
        int instanceCount = 0;
        for (int index = 0;
             index < slotParent.childCount &&
             instanceCount < instances.Length;
             index++)
        {
            CharacterRuntime existing = slotParent.GetChild(index)
                .GetComponent<CharacterRuntime>();
            if (existing == null)
                continue;

            instances[instanceCount] = existing;
            if (definitions[instanceCount] == null)
                definitions[instanceCount] = existing.Definition;
            instanceCount++;
        }

        for (int index = instanceCount; index < instances.Length; index++)
        {
            CharacterRuntime instance = Instantiate(
                prefab,
                slotParent,
                false);
            instance.name = $"grpPlayerCharacterSlot_{index + 1}";
            instance.transform.SetSiblingIndex(index);
            if (definitions[index] != null)
                instance.ConfigureDefinition(definitions[index]);
            instances[index] = instance;
        }

        for (int index = 0; index < instances.Length; index++)
        {
            CharacterRuntime instance = instances[index];
            instance.name = $"grpPlayerCharacterSlot_{index + 1}";
            instance.transform.SetSiblingIndex(index);
            if (definitions[index] != null &&
                instance.Definition != definitions[index])
            {
                instance.ConfigureDefinition(definitions[index]);
            }
            instance.ConfigureWorldSdPresentation(
                board != null && board.SupportsWorldPresentation);
        }

        playerCharacters = instances;
        characterInfoPrefab = prefab;
        _characterInfoInstancesPrepared = true;
    }

    private void EnsurePartySlotColors()
    {
        if (partySlotColors == null)
            partySlotColors = new Color[MaximumPartySize];
        else if (partySlotColors.Length != MaximumPartySize)
            System.Array.Resize(ref partySlotColors, MaximumPartySize);

        for (int index = 0; index < partySlotColors.Length; index++)
        {
            if (partySlotColors[index].a <= 0f)
                partySlotColors[index] = DefaultPartySlotColors[index];
        }
    }

    private void ApplyPlayerCharacterSlotColors()
    {
        EnsurePlayerCharacterSlots();
        EnsurePartySlotColors();
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character != null)
                character.ConfigurePartySlot(index, partySlotColors[index]);
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
        if (board == null)
            return;
        board.ApplyResponsiveViewport();
    }
}
