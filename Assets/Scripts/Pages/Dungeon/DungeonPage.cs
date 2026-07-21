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

[DisallowMultipleComponent]
public sealed class DungeonRewardCardHoverView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private bool _hovered;

    private void Update()
    {
        Vector3 targetScale = _hovered
            ? new Vector3(1.03f, 1.03f, 1f)
            : Vector3.one;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
    }

    private void OnDisable()
    {
        _hovered = false;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
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

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject stageSelectPage;

    [Header("Player Party")]
    [SerializeField] private CharacterRuntime[] playerCharacters =
        new CharacterRuntime[MaximumPartySize];
    [SerializeField, ColorUsage(false, false)]
    private Color[] partySlotColors = new Color[MaximumPartySize];

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
    private bool _flowEventsBound;
    private bool _battleEventsBound;
    private bool _eventRewardPending;
    private bool _startingCharacterSelectionPending;
    private BattleManager _battleManager;
    private DungeonEventTab _eventTab;
    private DungeonTutorialController _tutorialController;
    [SerializeField] private DungeonFieldView fieldView;
    private DungeonDefinition _pendingDefinition;
    private readonly DungeonRunSession _session = new();
    private DungeonRuntimeContext _runtimeContext;
    private CharacterSO _startingTurret;
    private readonly List<CharacterRuntime> _ownedTurrets = new();
    private readonly List<CharacterSO> _availableTurrets = new();
    private readonly List<CharacterSO> _startingCharacterChoices = new();
    private readonly CharacterSO[] _slotDefaultDefinitions =
        new CharacterSO[MaximumPartySize];
    private DungeonBattlePlan[] _battlePlans = Array.Empty<DungeonBattlePlan>();
    private readonly List<EnemySO> _fallbackEnemyPool = new();
    private readonly Dictionary<EBattleItemType, int> _consumableItems = new();
    private int _maximumEnergy = BattleManager.DefaultMaximumEnergy;
    private float _energyRechargeDuration =
        BattleManager.DefaultEnergyRechargeDuration;

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
    public IReadOnlyList<CharacterSO> AvailableTurrets => _availableTurrets;
    public IReadOnlyList<CharacterSO> StartingCharacterChoices =>
        _startingCharacterChoices;
    public IReadOnlyList<DungeonBattlePlan> BattlePlans => _battlePlans;
    public DungeonBoardView Board => board;
    public int MaximumEnergy => _maximumEnergy;
    public float EnergyRechargeDuration => _energyRechargeDuration;
    public bool IsStartingCharacterSelectionPending =>
        _startingCharacterSelectionPending;
    public bool IsTutorialBattle =>
        _session.IsActive && _session.Definition != null &&
        _session.Definition.HasTutorial;

    public IReadOnlyList<EnemySO> GetCodexEnemyDefinitions()
    {
        return GetBattleEnemyPool();
    }

    public IReadOnlyList<CharacterSO> GetCodexCharacterDefinitions()
    {
        List<CharacterSO> definitions = new();
        HashSet<CharacterSO> uniqueDefinitions = new();
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
        EnsureFieldView();
        EnsureTutorialController();

        _initialized = true;
        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public bool AdvanceDungeonPhase()
    {
        if (_startingCharacterSelectionPending ||
            CurrentPhase == EDungeonPhase.Event && _eventRewardPending)
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
        _eventRewardPending = false;
        _startingCharacterSelectionPending = false;
        EDungeonCompletionDestination destination =
            _session.Definition.CompletionDestination;

        if (_battleManager != null && _battleManager.HasSession)
            _battleManager.EndBattle(board);
        board?.ClearAllStacks();
        ClearPlayerParty();
        ResetRunResourcesAndItems(includeStartingConsumable: false);
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

        if (_battleManager.HasSession)
            _battleManager.EndBattle(board);

        ClearPlayerParty();
        ResetRunResourcesAndItems(definition.IncludeStartingConsumable);
        board.ClearAllStacks();
        _tutorialController?.StopTutorial();
        int runSeed = Environment.TickCount ^
                      UnityEngine.Random.Range(0, int.MaxValue);
        int battleCount = definition.ResolveBattleCount(runSeed);
        IReadOnlyList<EDungeonPhase> phases =
            definition.BuildPhaseSequence(battleCount, runSeed);
        _session.Begin(definition, runSeed, battleCount, phases);
        fieldView?.ApplyTheme(definition.Theme);
        GenerateBattlePlans(battleCount, runSeed);
        _eventRewardPending = false;
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
            playerCharacters.Length == 0 || playerCharacters[0] == null)
        {
            return false;
        }

        CharacterRuntime startingSlot = playerCharacters[0];
        if (!startingSlot.ConfigureDefinition(definition))
            return false;

        _startingTurret = definition;
        startingSlot.ConfigurePartySlot(0, partySlotColors[0]);
        startingSlot.gameObject.SetActive(true);
        _ownedTurrets.Clear();
        _ownedTurrets.Add(startingSlot);
        _startingCharacterSelectionPending = false;

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

    public bool TryApplyTurretUpgrade(
        int slotIndex,
        ETurretUpgradeType upgradeType)
    {
        if (!_eventRewardPending || slotIndex < 0 ||
            slotIndex >= _ownedTurrets.Count)
        {
            return false;
        }

        CharacterRuntime turret = _ownedTurrets[slotIndex];
        if (turret == null || !turret.ApplyUpgrade(upgradeType))
            return false;

        CompleteEventReward();
        return true;
    }

    public bool CanApplyEnergyUpgrade(EDungeonEnergyUpgradeType upgradeType)
    {
        return upgradeType != EDungeonEnergyUpgradeType.RechargeSpeed ||
               _energyRechargeDuration > MinimumEnergyRechargeDuration;
    }

    public bool TryApplyEnergyUpgrade(EDungeonEnergyUpgradeType upgradeType)
    {
        if (!_eventRewardPending || !CanApplyEnergyUpgrade(upgradeType))
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
        CompleteEventReward();
        return true;
    }

    public int GetBattleItemCount(EBattleItemType itemType)
    {
        if (BattleItemCatalog.Get(itemType).IsReusable)
            return 1;

        return _consumableItems.TryGetValue(itemType, out int count)
            ? Mathf.Max(0, count)
            : 0;
    }

    public bool TryAcquireBattleItem(EBattleItemType itemType)
    {
        if (!_eventRewardPending || !BattleItemCatalog.IsConsumable(itemType))
            return false;

        _consumableItems.TryGetValue(itemType, out int count);
        _consumableItems[itemType] = count + 1;
        BattleItemsChanged?.Invoke();
        CompleteEventReward();
        return true;
    }

    public bool TryUseBattleItemOnEnemy(
        EBattleItemType itemType,
        EnemyRuntime enemy)
    {
        BattleItemDefinition definition = BattleItemCatalog.Get(itemType);
        bool targetTypeMatches =
            definition.TargetType == EBattleItemTargetType.Enemy;
        bool canUse = CanUseBattleItem(definition);
        bool targetable = board != null &&
                          board.ContainsTargetableEnemy(enemy);
        if (!targetTypeMatches || !canUse || !targetable)
            return false;

        bool applied = itemType switch
        {
            EBattleItemType.Focus =>
                board.TryForcePriorityTarget(enemy, 5f),
            EBattleItemType.Molotov =>
                board.TryApplyFireToEnemy(enemy, 3f, 1f, 1),
            EBattleItemType.PrecisionShot =>
                board.TryDamageEnemy(enemy, 5) > 0,
            _ => false,
        };
        return applied && CompleteBattleItemUse(definition);
    }

    public bool TryUseBattleItemOnTurret(
        EBattleItemType itemType,
        CharacterRuntime turret)
    {
        BattleItemDefinition definition = BattleItemCatalog.Get(itemType);
        if (definition.TargetType != EBattleItemTargetType.Turret ||
            !CanUseBattleItem(definition) || turret == null ||
            !_ownedTurrets.Contains(turret))
        {
            return false;
        }

        bool applied = itemType switch
        {
            EBattleItemType.OverSupply =>
                turret.ApplyAttackSpeedBoost(2f, 5f),
            EBattleItemType.Overheat =>
                turret.ApplyPowerBoost(2f, 3f),
            _ => false,
        };
        return applied && CompleteBattleItemUse(definition);
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
        if (!_eventRewardPending || definition == null)
            return false;

        CharacterRuntime slot;
        if (_ownedTurrets.Count < MaximumPartySize)
        {
            slot = playerCharacters[_ownedTurrets.Count];
            if (slot == null || !slot.ConfigureDefinition(definition))
                return false;

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
        }

        CompleteEventReward();
        return true;
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
            if (character != null)
                characters.Add(character);
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
        if (_session.Definition != null &&
            _session.Definition.TryGetFixedBattle(
                battleIndex,
                out BattleSO fixedBattle))
        {
            setupCreated = fixedBattle.TryCreateSetup(
                plan.RandomSeed,
                out setup,
                out error);
        }
        else
        {
            bool useIntroBalance = _session.Definition != null &&
                                   _session.Definition.UseIntroBattleBalance &&
                                   _session.CurrentBattleNumber == 1;
            setupCreated = useIntroBalance
                ? TryCreateTutorialBattleSetup(plan, out setup, out error)
                : TryCreateScaledBattleSetup(plan, out setup, out error);
        }
        if (!setupCreated)
        {
            Debug.LogError($"Failed to create dungeon battle: {error}", this);
            return false;
        }

        board.Initialize(setup.FieldSize, setup.MaximumStackSize);
        _battleManager.ConfigureActiveSkillResource(
            _maximumEnergy,
            _energyRechargeDuration);
        bool started = _battleManager.StartBattle(
            board,
            characters,
            setup.Enemies,
            setup.SpawnInterval,
            setup.TimeLimit,
            setup.InitialEnemyCount);
        if (started)
            NotifyBattleStarted();
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
                plan.DifficultyScale >= GetEnemyUnlockScale(definition.Type))
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
            initialEnemyCount);
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

        CharacterData startingData = _startingTurret != null
            ? _startingTurret.CreateData()
            : null;
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
            TutorialInitialEnemyCount);
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
            data.AttackCooldown +
            (data.AttackType == CharacterAttackType.FireRandom
                ? 0f
                : CharacterRuntime.TargetAttackRecoveryDuration));
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

        return data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                data.AttackDamage * Mathf.Max(1, data.TargetCount),
            CharacterAttackType.CrossHighestHealth =>
                data.AttackDamage * 2f,
            CharacterAttackType.FireRandom =>
                data.FireTickDamage * Mathf.Max(
                    1,
                    Mathf.FloorToInt(
                        data.FireDuration /
                        Mathf.Max(0.1f, data.FireTickInterval))),
            _ => data.AttackDamage,
        };
    }

    private int CalculateBaselineEnemyHealth()
    {
        CharacterData startingData = _startingTurret != null
            ? _startingTurret.CreateData()
            : null;
        float attackCycle = startingData != null
            ? startingData.AttackCooldown +
              (startingData.AttackType == CharacterAttackType.FireRandom
                  ? 0f
                  : CharacterRuntime.TargetAttackRecoveryDuration)
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

    private static int GetEnemyUnlockScale(EEnemyType type)
    {
        return type switch
        {
            EEnemyType.Assault => 10,
            EEnemyType.Heavy => 20,
            EEnemyType.Medic => 30,
            EEnemyType.Infiltrator => 40,
            EEnemyType.Mechanic => 45,
            EEnemyType.Pointman => 55,
            EEnemyType.ShieldBearer => 70,
            _ => 0,
        };
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
            _eventRewardPending = false;
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
            if (phase == EDungeonPhase.Event)
            {
                _eventRewardPending = true;
                _eventTab?.ShowUpgradeEvent();
            }
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
        if (_session.Definition != null &&
            _session.Definition.HasTutorial)
        {
            _eventRewardPending = false;
            _startingCharacterSelectionPending = false;
            _session.Finish(EDungeonRunResult.Clear);
            NotifyRunEnded(EDungeonRunResult.Clear);
            ApplyBattlePauseState();
            _tutorialController?.ShowCompletion();
            return;
        }

        if (CurrentPhase == EDungeonPhase.Battle && flowController != null &&
            !flowController.IsCompleted)
        {
            flowController.TryAdvance();
        }
    }

    private void HandleBattleEnded(EBattleResult result)
    {
        if (!_session.IsActive)
            return;

        NotifyBattleEnded(result);
        if (result == EBattleResult.Victory)
            return;

        _eventRewardPending = false;
        _startingCharacterSelectionPending = false;
        _session.Finish(EDungeonRunResult.Defeat);
        _battleManager?.EndBattle(board);
        board?.ClearAllStacks();
        ClearPlayerParty();
        ResetRunResourcesAndItems(includeStartingConsumable: false);
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_session.Result);
        NotifyRunEnded(_session.Result);
        RunEnded?.Invoke(_session.Result);
    }

    private void HandleDungeonFlowCompleted()
    {
        if (!_session.IsActive)
            return;

        _eventRewardPending = false;
        _startingCharacterSelectionPending = false;
        _session.Finish(EDungeonRunResult.Clear);
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_session.Result);
        NotifyRunEnded(_session.Result);
        RunEnded?.Invoke(_session.Result);
    }

    private void CompleteEventReward()
    {
        if (!_eventRewardPending)
            return;

        _eventRewardPending = false;
        flowController?.TryAdvance();
    }

    private bool CanUseBattleItem(BattleItemDefinition definition)
    {
        return _session.IsActive &&
               _session.Activity == EDungeonRunActivity.Battle &&
               (definition.IsReusable || GetBattleItemCount(definition.Type) > 0) &&
               _battleManager != null &&
               _battleManager.CanSpend(definition.EnergyCost);
    }

    private bool CompleteBattleItemUse(BattleItemDefinition definition)
    {
        if (_battleManager == null ||
            !_battleManager.TrySpend(definition.EnergyCost))
        {
            return false;
        }

        if (!definition.IsReusable)
        {
            int remainingCount = Mathf.Max(
                0,
                GetBattleItemCount(definition.Type) - 1);
            if (remainingCount > 0)
                _consumableItems[definition.Type] = remainingCount;
            else
                _consumableItems.Remove(definition.Type);
            BattleItemsChanged?.Invoke();
        }

        return true;
    }

    private void ResetRunResourcesAndItems(bool includeStartingConsumable)
    {
        _maximumEnergy = BattleManager.DefaultMaximumEnergy;
        _energyRechargeDuration =
            BattleManager.DefaultEnergyRechargeDuration;
        _consumableItems.Clear();
        if (includeStartingConsumable &&
            BattleItemCatalog.Consumables.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(
                0,
                BattleItemCatalog.Consumables.Count);
            EBattleItemType startingItem =
                BattleItemCatalog.Consumables[randomIndex];
            _consumableItems[startingItem] = 1;
        }
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
        _availableTurrets.Clear();
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
                continue;
            }

            character.ConfigurePartySlot(index, partySlotColors[index]);
            CharacterSO definition = character.Definition;
            _slotDefaultDefinitions[index] = definition;
            if (definition != null && !_availableTurrets.Contains(definition))
                _availableTurrets.Add(definition);
        }

        if (!hasCharacter)
            Debug.LogError("DungeonPage requires at least one player character.", this);
        else
            ClearPlayerParty();
    }

    private void ClearPlayerParty()
    {
        _ownedTurrets.Clear();
        _startingTurret = null;
        _startingCharacterSelectionPending = false;
        _startingCharacterChoices.Clear();
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

    private void EnsureTutorialController()
    {
        if (_tutorialController == null)
            _tutorialController = GetComponent<DungeonTutorialController>();
        if (_tutorialController == null)
            _tutorialController = gameObject.AddComponent<DungeonTutorialController>();

        _tutorialController.Initialize(this, fieldView);
    }

    private void EnsureFieldView()
    {
        if (fieldView == null)
            fieldView = GetComponent<DungeonFieldView>();
        if (fieldView == null)
            fieldView = gameObject.AddComponent<DungeonFieldView>();

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
        EDungeonCompletionDestination destination)
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
            gameObject,
            targetPage,
            PageOpenMode.Resume);
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
        ForEachModifier(modifier =>
            modifier.OnRunEnded(GetRuntimeContext(), result));
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
        if (board == null || transform is not RectTransform pageRect)
            return;

        float availableWidth = pageRect.rect.width * boardWidthRatio;
        float availableHeight = pageRect.rect.height * boardHeightRatio;
        float boardSize = Mathf.Min(maximumBoardSize, availableWidth, availableHeight);

        if (boardSize > 0f)
            board.SetPixelSize(boardSize);
    }
}

public sealed class DungeonEventTab
{
    private const int RewardChoiceCount = 3;
    private const int RewardSeedSalt = unchecked((int)0xA511E9B3);

    private static readonly ETurretUpgradeType[] UpgradeTypes =
    {
        ETurretUpgradeType.PrimaryPower,
        ETurretUpgradeType.AttackSpeed,
        ETurretUpgradeType.SkillPower,
        ETurretUpgradeType.SkillCost,
    };

    private enum ERewardOptionType
    {
        TurretUpgrade,
        NewTurret,
        EnergyUpgrade,
        BattleItem,
    }

    private enum EViewMode
    {
        None,
        StartingSelection,
        StartingConfigurationError,
        RunResult,
        RewardSelection,
        ReplacementSelection,
    }

    private readonly struct RewardOption
    {
        public ERewardOptionType Type { get; }
        public int TurretSlotIndex { get; }
        public ETurretUpgradeType UpgradeType { get; }
        public CharacterSO TurretDefinition { get; }
        public EDungeonEnergyUpgradeType EnergyUpgradeType { get; }
        public EBattleItemType BattleItemType { get; }

        private RewardOption(
            ERewardOptionType type,
            int turretSlotIndex,
            ETurretUpgradeType upgradeType,
            CharacterSO turretDefinition,
            EDungeonEnergyUpgradeType energyUpgradeType,
            EBattleItemType battleItemType)
        {
            Type = type;
            TurretSlotIndex = turretSlotIndex;
            UpgradeType = upgradeType;
            TurretDefinition = turretDefinition;
            EnergyUpgradeType = energyUpgradeType;
            BattleItemType = battleItemType;
        }

        public static RewardOption CreateUpgrade(
            int turretSlotIndex,
            ETurretUpgradeType upgradeType)
        {
            return new RewardOption(
                ERewardOptionType.TurretUpgrade,
                turretSlotIndex,
                upgradeType,
                null,
                default,
                default);
        }

        public static RewardOption CreateNewTurret(CharacterSO definition)
        {
            return new RewardOption(
                ERewardOptionType.NewTurret,
                -1,
                default,
                definition,
                default,
                default);
        }

        public static RewardOption CreateEnergyUpgrade(
            EDungeonEnergyUpgradeType upgradeType)
        {
            return new RewardOption(
                ERewardOptionType.EnergyUpgrade,
                -1,
                default,
                null,
                upgradeType,
                default);
        }

        public static RewardOption CreateBattleItem(EBattleItemType itemType)
        {
            return new RewardOption(
                ERewardOptionType.BattleItem,
                -1,
                default,
                null,
                default,
                itemType);
        }
    }

    private readonly struct RewardCardContent
    {
        public string Category { get; }
        public string Title { get; }
        public string Description { get; }
        public string Footer { get; }
        public Color AccentColor { get; }

        public RewardCardContent(
            string category,
            string title,
            string description,
            string footer,
            Color accentColor)
        {
            Category = category ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Footer = footer ?? string.Empty;
            AccentColor = accentColor;
        }
    }

    private readonly Color _panelColor = new(0.075f, 0.095f, 0.08f, 0.98f);
    private readonly Color _buttonColor = new(0.19f, 0.28f, 0.22f, 1f);
    private readonly Color _textColor = new(0.94f, 0.91f, 0.78f, 1f);

    private DungeonPage _page;
    private GameObject _root;
    private RectTransform _panel;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private RectTransform _rewardCardRoot;
    private GridLayoutGroup _rewardCardLayout;
    private RectTransform _buttonRoot;
    private RectTransform _firstStartingChoiceRect;
    private readonly List<RewardOption> _currentRewardOptions = new();
    private readonly List<CharacterSO> _startingChoices = new();
    private EViewMode _viewMode;
    private int _startingAvailableCount;
    private EDungeonRunResult _currentRunResult;
    private CharacterSO _replacementDefinition;
    private bool _initialized;
    private bool _localizationEventsBound;

    public RectTransform FirstStartingChoiceRect =>
        _firstStartingChoiceRect;

    public void Initialize(GameObject root, DungeonPage page)
    {
        if (root == null || page == null)
            return;

        _root = root;
        _page = page;
        if (_initialized)
        {
            BindLocalizationEvents();
            return;
        }

        foreach (TextMeshProUGUI text in
                 _root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == "txtEventPlaceholder")
                text.gameObject.SetActive(false);
        }

        BuildRuntimeUi();
        _initialized = true;
        BindLocalizationEvents();
    }

    public void Teardown()
    {
        UnbindLocalizationEvents();
        _startingChoices.Clear();
        _currentRewardOptions.Clear();
        _replacementDefinition = null;
        _viewMode = EViewMode.None;
        _initialized = false;
        _titleText = null;
        _descriptionText = null;
        _rewardCardRoot = null;
        _rewardCardLayout = null;
        _buttonRoot = null;
        _firstStartingChoiceRect = null;
        _panel = null;
        _root = null;
        _page = null;
    }

    public void ShowUpgradeEvent()
    {
        if (!EnsureInitialized())
            return;

        GenerateRewardOptions();
        _viewMode = EViewMode.RewardSelection;
        RenderRewardSelection();
    }

    public void ShowStartingCharacterSelection(
        IReadOnlyList<CharacterSO> choices)
    {
        if (!EnsureInitialized())
            return;

        _startingChoices.Clear();
        if (choices != null)
        {
            for (int index = 0; index < choices.Count; index++)
                _startingChoices.Add(choices[index]);
        }
        _viewMode = EViewMode.StartingSelection;
        RenderStartingCharacterSelection();
    }

    private void RenderStartingCharacterSelection()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(true);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartDescription);

        int choiceCount = Mathf.Min(
            DungeonPage.StartingCharacterChoiceCount,
            _startingChoices.Count);
        for (int index = 0; index < choiceCount; index++)
        {
            CharacterSO selectedDefinition = _startingChoices[index];
            if (selectedDefinition == null)
                continue;

            RectTransform cardRect = CreateRewardCard(
                GetStartingTurretCardContent(selectedDefinition),
                () => _page.TrySelectStartingCharacter(selectedDefinition));
            if (_firstStartingChoiceRect == null)
                _firstStartingChoiceRect = cardRect;
        }
    }

    public void ShowStartingCharacterConfigurationError(int availableCount)
    {
        if (!EnsureInitialized())
            return;

        _startingAvailableCount = Mathf.Max(0, availableCount);
        _viewMode = EViewMode.StartingConfigurationError;
        RenderStartingCharacterConfigurationError();
    }

    private void RenderStartingCharacterConfigurationError()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartErrorTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartErrorDescription,
            LocalizationService.Arg("count", _startingAvailableCount));
        CreateButton(
            LocalizationService.Get(LocalizationKeys.UiDungeonStartRetry),
            _page.StartNewDungeonRun);
    }

    public void ShowRunResult(EDungeonRunResult result)
    {
        if (!EnsureInitialized())
            return;

        _currentRunResult = result;
        _viewMode = EViewMode.RunResult;
        RenderRunResult();
    }

    private void RenderRunResult()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        bool cleared = _currentRunResult == EDungeonRunResult.Clear;
        _titleText.text = LocalizationService.Get(cleared
            ? LocalizationKeys.UiDungeonResultClearTitle
            : LocalizationKeys.UiDungeonDefeat);
        _descriptionText.text = cleared
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonResultClearDescription,
                LocalizationService.Arg("count", _page.TotalBattleCount))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonResultResetDescription);
        CreateButton(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonResultStartNewRun),
            _page.StartNewDungeonRun);
        if (!cleared)
        {
            CreateButton(
                LocalizationService.Get(
                    _page.CurrentDungeon != null &&
                    _page.CurrentDungeon.CompletionDestination ==
                    EDungeonCompletionDestination.StageSelect
                        ? LocalizationKeys.UiTutorialReturn
                        : LocalizationKeys.UiDungeonResultReturnMain),
                _page.ReturnFromRunResult);
        }
    }

    private void GenerateRewardOptions()
    {
        List<RewardOption> candidates = new();
        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        for (int index = 0; index < turrets.Count; index++)
        {
            CharacterData data = turrets[index]?.Data;
            if (data == null)
                continue;

            foreach (ETurretUpgradeType upgradeType in UpgradeTypes)
            {
                if (data.CanApplyUpgrade(upgradeType))
                {
                    candidates.Add(RewardOption.CreateUpgrade(
                        index,
                        upgradeType));
                }
            }
        }

        foreach (CharacterSO definition in _page.AvailableTurrets)
        {
            if (definition != null)
                candidates.Add(RewardOption.CreateNewTurret(definition));
        }

        candidates.Add(RewardOption.CreateEnergyUpgrade(
            EDungeonEnergyUpgradeType.MaximumEnergy));
        if (_page.CanApplyEnergyUpgrade(
                EDungeonEnergyUpgradeType.RechargeSpeed))
        {
            candidates.Add(RewardOption.CreateEnergyUpgrade(
                EDungeonEnergyUpgradeType.RechargeSpeed));
        }

        foreach (EBattleItemType itemType in BattleItemCatalog.Consumables)
            candidates.Add(RewardOption.CreateBattleItem(itemType));

        _currentRewardOptions.Clear();
        int battlePlanIndex = _page.CurrentBattleNumber - 1;
        int rewardSeed = battlePlanIndex >= 0 &&
                         battlePlanIndex < _page.BattlePlans.Count
            ? _page.BattlePlans[battlePlanIndex].RandomSeed ^ RewardSeedSalt
            : Environment.TickCount;
        System.Random random = new(rewardSeed);
        int choiceCount = Mathf.Min(RewardChoiceCount, candidates.Count);
        for (int index = 0; index < choiceCount; index++)
        {
            int swapIndex = random.Next(index, candidates.Count);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
            _currentRewardOptions.Add(candidates[index]);
        }
    }

    private void ShowCurrentRewardOptions()
    {
        _replacementDefinition = null;
        _viewMode = EViewMode.RewardSelection;
        RenderRewardSelection();
    }

    private void RenderRewardSelection()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(true);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonRewardTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonRewardSummary,
            LocalizationService.Arg("current", _page.CurrentBattleNumber),
            LocalizationService.Arg("total", _page.TotalBattleCount),
            LocalizationService.Arg("scale", _page.CurrentDifficultyScale),
            LocalizationService.Arg(
                "next",
                _page.GetBattleDifficultyScale(
                    _page.CurrentBattleNumber + 1)),
            LocalizationService.Arg("count", _currentRewardOptions.Count));

        foreach (RewardOption option in _currentRewardOptions)
        {
            RewardOption selectedOption = option;
            CreateRewardCard(GetRewardCardContent(option), () =>
            {
                SelectRewardOption(selectedOption);
            });
        }
    }

    private RewardCardContent GetRewardCardContent(RewardOption option)
    {
        if (option.Type == ERewardOptionType.EnergyUpgrade)
        {
            bool maximumEnergy = option.EnergyUpgradeType ==
                                 EDungeonEnergyUpgradeType.MaximumEnergy;
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryEnergy),
                LocalizationService.Get(maximumEnergy
                    ? LocalizationKeys.UiDungeonRewardEnergyMaxTitle
                    : LocalizationKeys.UiDungeonRewardEnergyRechargeTitle),
                maximumEnergy
                    ? LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardEnergyMaxChange,
                        LocalizationService.Arg(
                            "before",
                            _page.MaximumEnergy),
                        LocalizationService.Arg(
                            "after",
                            _page.MaximumEnergy + 1))
                    : LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardEnergyRechargeChange,
                        LocalizationService.Arg(
                            "before",
                            _page.EnergyRechargeDuration),
                        LocalizationService.Arg(
                            "after",
                            Mathf.Max(
                                DungeonPage.MinimumEnergyRechargeDuration,
                                _page.EnergyRechargeDuration -
                                DungeonPage.EnergyRechargeUpgradeAmount))),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardRunFooter),
                new Color(0.82f, 0.64f, 0.2f, 1f));
        }

        if (option.Type == ERewardOptionType.BattleItem)
        {
            BattleItemDefinition item = BattleItemCatalog.Get(
                option.BattleItemType);
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryItem),
                item.DisplayName,
                item.Description,
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardItemFooter,
                    LocalizationService.Arg(
                        "owned",
                        _page.GetBattleItemCount(item.Type))),
                new Color(0.8f, 0.35f, 0.22f, 1f));
        }

        if (option.Type == ERewardOptionType.NewTurret)
        {
            CharacterData newTurretData =
                option.TurretDefinition?.CreateData();
            string description = newTurretData == null
                ? LocalizationService.Get(
                    LocalizationKeys
                        .UiDungeonRewardNewTurretEmptyDescription)
                : newTurretData.AttackType == CharacterAttackType.FireRandom
                    ? LocalizationService.Get(
                        LocalizationKeys
                            .UiDungeonRewardNewTurretFireDescription,
                        LocalizationService.Arg(
                            "duration",
                            newTurretData.FireDuration),
                        LocalizationService.Arg(
                            "targets",
                            newTurretData.FireSkillTargetCount))
                    : LocalizationService.Get(
                        LocalizationKeys
                            .UiDungeonRewardNewTurretDamageDescription,
                        LocalizationService.Arg(
                            "attack",
                            newTurretData.AttackDamage),
                        LocalizationService.Arg(
                            "skill",
                            newTurretData.SkillAttackDamage));
            string footer = newTurretData != null
                ? LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardNewTurretFooter,
                    LocalizationService.Arg(
                        "cost",
                        newTurretData.ActiveSkillCost),
                    LocalizationService.Arg(
                        "cooldown",
                        newTurretData.AttackCooldown))
                : LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardNewTurretEmptySlot);
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryNewTurret),
                newTurretData != null
                    ? CharacterLocalization.GetName(newTurretData)
                    : LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardUnknownTurret),
                description,
                footer,
                new Color(0.25f, 0.52f, 0.78f, 1f));
        }

        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        int slotIndex = option.TurretSlotIndex;
        if (slotIndex < 0 || slotIndex >= turrets.Count ||
            turrets[slotIndex]?.Data == null)
        {
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryTurretUpgrade),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardUnknownUpgrade),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardTurretUnavailable),
                string.Empty,
                new Color(0.3f, 0.68f, 0.4f, 1f));
        }

        CharacterData data = turrets[slotIndex].Data;
        return new RewardCardContent(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardCategoryTurretUpgradeSlot,
                LocalizationService.Arg("slot", slotIndex + 1)),
            CharacterLocalization.GetUpgradeTitle(data, option.UpgradeType),
            CharacterLocalization.GetName(data) + "\n" +
            CharacterLocalization.GetUpgradeDescription(
                data,
                option.UpgradeType),
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardPermanentFooter),
            new Color(0.3f, 0.68f, 0.4f, 1f));
    }

    private static RewardCardContent GetStartingTurretCardContent(
        CharacterSO definition)
    {
        CharacterData data = definition?.CreateData();
        if (data == null)
        {
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryStartingTurret),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardUnknownTurret),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardTurretUnavailable),
                string.Empty,
                new Color(0.25f, 0.52f, 0.78f, 1f));
        }

        string description = data.AttackType switch
        {
            CharacterAttackType.RandomMultiple => LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartRandom,
                LocalizationService.Arg("count", data.TargetCount),
                LocalizationService.Arg("attack", data.AttackDamage)),
            CharacterAttackType.CrossHighestHealth => LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartCross,
                LocalizationService.Arg("attack", data.AttackDamage)),
            CharacterAttackType.FireRandom => LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartFire,
                LocalizationService.Arg("duration", data.FireDuration),
                LocalizationService.Arg("damage", data.FireTickDamage),
                LocalizationService.Arg("interval", data.FireTickInterval)),
            _ => LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartLowest,
                LocalizationService.Arg("attack", data.AttackDamage)),
        };
        Color accent = data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                new Color(0.55f, 0.38f, 0.82f, 1f),
            CharacterAttackType.CrossHighestHealth =>
                new Color(0.25f, 0.68f, 0.48f, 1f),
            CharacterAttackType.FireRandom =>
                new Color(0.88f, 0.32f, 0.16f, 1f),
            _ => new Color(0.25f, 0.52f, 0.78f, 1f),
        };
        return new RewardCardContent(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardCategoryStartingTurret),
            CharacterLocalization.GetName(data),
            description,
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartFooter,
                LocalizationService.Arg("cost", data.ActiveSkillCost),
                LocalizationService.Arg("cooldown", data.AttackCooldown)),
            accent);
    }

    private void SelectRewardOption(RewardOption option)
    {
        if (option.Type == ERewardOptionType.TurretUpgrade)
        {
            _page.TryApplyTurretUpgrade(
                option.TurretSlotIndex,
                option.UpgradeType);
            return;
        }

        if (option.Type == ERewardOptionType.EnergyUpgrade)
        {
            _page.TryApplyEnergyUpgrade(option.EnergyUpgradeType);
            return;
        }

        if (option.Type == ERewardOptionType.BattleItem)
        {
            _page.TryAcquireBattleItem(option.BattleItemType);
            return;
        }

        CharacterSO definition = option.TurretDefinition;
        if (definition == null)
            return;

        if (_page.OwnedTurrets.Count < DungeonPage.MaximumPartySize)
            _page.TryAcquireTurret(definition);
        else
            ShowReplacementSelection(definition);
    }

    private void ShowReplacementSelection(CharacterSO newDefinition)
    {
        _replacementDefinition = newDefinition;
        _viewMode = EViewMode.ReplacementSelection;
        RenderReplacementSelection();
    }

    private void RenderReplacementSelection()
    {
        if (!EnsureInitialized() || _replacementDefinition == null)
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        CharacterSO selectedDefinition = _replacementDefinition;
        CharacterData newData = selectedDefinition.CreateData();
        string newName = newData != null
            ? CharacterLocalization.GetName(newData)
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardUnknownTurret);
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonReplaceTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonReplaceDescription,
            LocalizationService.Arg("name", newName));

        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        for (int index = 0; index < turrets.Count; index++)
        {
            int slotIndex = index;
            CreateButton(
                GetTurretSummary(slotIndex, turrets[index]),
                () => _page.TryAcquireTurret(
                    selectedDefinition,
                    slotIndex));
        }
        CreateButton(
            LocalizationService.Get(LocalizationKeys.UiCommonBack),
            ShowCurrentRewardOptions);
    }

    private void BindLocalizationEvents()
    {
        if (_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        _localizationEventsBound = true;
    }

    private void UnbindLocalizationEvents()
    {
        if (!_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
        _localizationEventsBound = false;
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        if (!_initialized)
            return;

        LocalizationFontResolver.ApplyGameDefault(_titleText);
        LocalizationFontResolver.ApplyGameDefault(_descriptionText);
        RenderCurrentView();
    }

    private void RenderCurrentView()
    {
        switch (_viewMode)
        {
            case EViewMode.StartingSelection:
                RenderStartingCharacterSelection();
                break;
            case EViewMode.StartingConfigurationError:
                RenderStartingCharacterConfigurationError();
                break;
            case EViewMode.RunResult:
                RenderRunResult();
                break;
            case EViewMode.RewardSelection:
                RenderRewardSelection();
                break;
            case EViewMode.ReplacementSelection:
                RenderReplacementSelection();
                break;
        }
    }

    private bool EnsureInitialized()
    {
        if (!_initialized && _root != null && _page != null)
            Initialize(_root, _page);
        return _initialized && _root != null && _page != null;
    }

    private string GetTurretSummary(int slotIndex, CharacterRuntime turret)
    {
        CharacterData data = turret?.Data;
        if (data == null)
        {
            return LocalizationService.Get(
                LocalizationKeys.UiDungeonReplaceEmptySlot,
                LocalizationService.Arg("slot", slotIndex + 1));
        }

        return data.AttackType == CharacterAttackType.FireRandom
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonReplaceFireSlot,
                LocalizationService.Arg("slot", slotIndex + 1),
                LocalizationService.Arg(
                    "name",
                    CharacterLocalization.GetName(data)),
                LocalizationService.Arg("duration", data.FireDuration),
                LocalizationService.Arg(
                    "targets",
                    data.FireSkillTargetCount))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonReplaceDamageSlot,
                LocalizationService.Arg("slot", slotIndex + 1),
                LocalizationService.Arg(
                    "name",
                    CharacterLocalization.GetName(data)),
                LocalizationService.Arg("attack", data.AttackDamage),
                LocalizationService.Arg("skill", data.SkillAttackDamage));
    }

    private void BuildRuntimeUi()
    {
        GameObject panelObject = new(
            "grpRuntimeEventPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        _panel = (RectTransform)panelObject.transform;
        _panel.SetParent(_root.transform, false);
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(900f, 650f);

        panelObject.GetComponent<Image>().color = _panelColor;
        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 32, 32);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        _titleText = CreateText(_panel, "txtEventTitle", 34f, 56f);
        _descriptionText = CreateText(
            _panel,
            "txtEventDescription",
            21f,
            76f);

        GameObject rewardCardRootObject = new(
            "grpRewardCards",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(LayoutElement));
        _rewardCardRoot =
            (RectTransform)rewardCardRootObject.transform;
        _rewardCardRoot.SetParent(_panel, false);
        LayoutElement rewardRootLayout =
            rewardCardRootObject.GetComponent<LayoutElement>();
        rewardRootLayout.preferredHeight = 350f;
        rewardRootLayout.flexibleHeight = 1f;
        _rewardCardLayout =
            rewardCardRootObject.GetComponent<GridLayoutGroup>();
        _rewardCardLayout.padding = new RectOffset(0, 0, 0, 0);
        _rewardCardLayout.spacing = new Vector2(20f, 0f);
        _rewardCardLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _rewardCardLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        _rewardCardLayout.childAlignment = TextAnchor.MiddleCenter;
        _rewardCardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _rewardCardLayout.constraintCount = RewardChoiceCount;
        _rewardCardLayout.cellSize = new Vector2(250f, 350f);

        GameObject buttonRootObject = new(
            "grpEventButtons",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _buttonRoot = (RectTransform)buttonRootObject.transform;
        _buttonRoot.SetParent(_panel, false);
        LayoutElement rootLayout = buttonRootObject.GetComponent<LayoutElement>();
        rootLayout.preferredHeight = 420f;
        rootLayout.flexibleHeight = 1f;
        VerticalLayoutGroup buttonLayout =
            buttonRootObject.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childAlignment = TextAnchor.UpperCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandHeight = false;

        _rewardCardRoot.gameObject.SetActive(false);
        _buttonRoot.gameObject.SetActive(false);
        RefreshRuntimeLayout();
    }

    private void SetRewardCardMode(bool showRewardCards)
    {
        if (_rewardCardRoot != null)
            _rewardCardRoot.gameObject.SetActive(showRewardCards);
        if (_buttonRoot != null)
            _buttonRoot.gameObject.SetActive(!showRewardCards);
    }

    private void RefreshRuntimeLayout()
    {
        if (_panel == null)
            return;

        RectTransform rootRect = _root != null
            ? _root.transform as RectTransform
            : null;
        float rootWidth = rootRect != null && rootRect.rect.width > 0f
            ? rootRect.rect.width
            : 960f;
        float rootHeight = rootRect != null && rootRect.rect.height > 0f
            ? rootRect.rect.height
            : 700f;
        float panelWidth = Mathf.Min(900f, Mathf.Max(540f, rootWidth - 48f));
        float panelHeight = Mathf.Min(650f, Mathf.Max(500f, rootHeight - 48f));
        _panel.sizeDelta = new Vector2(panelWidth, panelHeight);

        if (_rewardCardLayout == null || _rewardCardRoot == null)
            return;

        const float horizontalPadding = 64f;
        const float totalCardSpacing = 40f;
        const float reservedHeaderHeight = 228f;
        float widthBound =
            (panelWidth - horizontalPadding - totalCardSpacing) /
            RewardChoiceCount;
        float heightBound =
            Mathf.Max(140f, panelHeight - reservedHeaderHeight) / 1.4f;
        float cardWidth = Mathf.Clamp(
            Mathf.Min(widthBound, heightBound),
            140f,
            250f);
        float cardHeight = cardWidth * 1.4f;
        _rewardCardLayout.cellSize = new Vector2(cardWidth, cardHeight);
        LayoutElement rewardLayout =
            _rewardCardRoot.GetComponent<LayoutElement>();
        if (rewardLayout != null)
            rewardLayout.preferredHeight = cardHeight;
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        float preferredHeight)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.color = _textColor;
        text.alignment = TextAlignmentOptions.Center;
        textObject.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
        return text;
    }

    private RectTransform CreateRewardCard(
        RewardCardContent content,
        Action action)
    {
        if (_rewardCardRoot == null)
            return null;

        GameObject cardObject = new(
            "btnRewardCard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(DungeonRewardCardHoverView));
        cardObject.transform.SetParent(_rewardCardRoot, false);

        Color normalColor = Color.Lerp(
            _buttonColor,
            content.AccentColor,
            0.18f);
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = normalColor;
        cardImage.raycastTarget = true;

        Button button = cardObject.GetComponent<Button>();
        button.targetGraphic = cardImage;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(normalColor, content.AccentColor, 0.5f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Color.Lerp(normalColor, Color.black, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (action != null)
            button.onClick.AddListener(() => action());

        GameObject accentObject = new(
            "imgRewardAccent",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        accentObject.transform.SetParent(cardObject.transform, false);
        RectTransform accentRect =
            (RectTransform)accentObject.transform;
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 8f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = content.AccentColor;
        accentImage.raycastTarget = false;

        TextMeshProUGUI category = CreateRewardCardText(
            cardObject.transform,
            "txtRewardCategory",
            new Vector2(0f, 0.83f),
            new Vector2(1f, 0.96f),
            14f);
        category.color = Color.Lerp(_textColor, content.AccentColor, 0.45f);
        category.text = content.Category;

        TextMeshProUGUI title = CreateRewardCardText(
            cardObject.transform,
            "txtRewardTitle",
            new Vector2(0f, 0.63f),
            new Vector2(1f, 0.83f),
            24f);
        title.text = content.Title;

        TextMeshProUGUI description = CreateRewardCardText(
            cardObject.transform,
            "txtRewardDescription",
            new Vector2(0f, 0.22f),
            new Vector2(1f, 0.63f),
            18f);
        description.fontStyle = FontStyles.Normal;
        description.text = content.Description;

        TextMeshProUGUI footer = CreateRewardCardText(
            cardObject.transform,
            "txtRewardFooter",
            new Vector2(0f, 0.04f),
            new Vector2(1f, 0.2f),
            14f);
        footer.color = Color.Lerp(_textColor, content.AccentColor, 0.35f);
        footer.text = content.Footer;
        return cardObject.transform as RectTransform;
    }

    private TextMeshProUGUI CreateRewardCardText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float maximumFontSize)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.offsetMin = new Vector2(14f, 4f);
        textRect.offsetMax = new Vector2(-14f, -4f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = maximumFontSize;
        text.fontSizeMax = maximumFontSize;
        text.fontSizeMin = Mathf.Max(11f, maximumFontSize - 6f);
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontStyle = FontStyles.Bold;
        text.color = _textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void CreateButton(string label, Action action)
    {
        GameObject buttonObject = new(
            "btnEventChoice",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(_buttonRoot, false);
        buttonObject.GetComponent<Image>().color = _buttonColor;
        float buttonHeight = label != null && label.Contains("\n")
            ? 88f
            : 64f;
        buttonObject.GetComponent<LayoutElement>().preferredHeight =
            buttonHeight;

        Button button = buttonObject.GetComponent<Button>();
        if (action != null)
            button.onClick.AddListener(() => action());

        TextMeshProUGUI text = CreateText(
            buttonObject.transform,
            "txtChoice",
            label != null && label.Contains("\n") ? 18f : 22f,
            buttonHeight);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        text.text = label;
    }

    private void ClearButtons()
    {
        _firstStartingChoiceRect = null;
        ClearChildren(_rewardCardRoot);
        ClearChildren(_buttonRoot);
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            GameObject child = root.GetChild(index).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
