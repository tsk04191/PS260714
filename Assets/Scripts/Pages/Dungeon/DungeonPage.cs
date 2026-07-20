using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
    public const float EnergyRechargeUpgradeAmount = 0.5f;
    public const float MinimumEnergyRechargeDuration = 0.5f;

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
    private bool _runActive;
    private bool _eventRewardPending;
    private int _totalBattleCount;
    private int _currentBattleNumber;
    private EDungeonRunResult _runResult;
    private BattleManager _battleManager;
    private DungeonEventTab _eventTab;
    private CharacterSO _startingTurret;
    private readonly List<CharacterRuntime> _ownedTurrets = new();
    private readonly List<CharacterSO> _availableTurrets = new();
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
    public int TotalBattleCount => _totalBattleCount;
    public int CurrentBattleNumber => _currentBattleNumber;
    public int CurrentDifficultyScale => GetBattleDifficultyScale(
        _currentBattleNumber);
    public EDungeonRunResult RunResult => _runResult;
    public IReadOnlyList<CharacterRuntime> OwnedTurrets => _ownedTurrets;
    public IReadOnlyList<CharacterSO> AvailableTurrets => _availableTurrets;
    public IReadOnlyList<DungeonBattlePlan> BattlePlans => _battlePlans;
    public DungeonBoardView Board => board;
    public int MaximumEnergy => _maximumEnergy;
    public float EnergyRechargeDuration => _energyRechargeDuration;

    public event Action<EDungeonRunResult> RunEnded;
    public event Action BattleItemsChanged;

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        RefreshBoardSize();
        if (_initialized && !_runActive && _runResult == EDungeonRunResult.None)
            StartNewDungeonRun();
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
            StartNewDungeonRun();
        }
        else if (_runResult != EDungeonRunResult.None)
        {
            flowController?.ShowEventTab();
            _eventTab?.ShowRunResult(_runResult);
        }
        else
        {
            flowController?.RefreshCurrentPhase();
            if (CurrentPhase == EDungeonPhase.Battle &&
                TryResolveBattleManager() && !_battleManager.ResumeBattle() &&
                !_battleManager.HasSession && _runActive)
            {
                StartNewBattle();
            }
        }

        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public void Close()
    {
        _battleManager?.SuspendBattle();
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

        _initialized = true;
        battleTab?.Refresh();
        RefreshBoardSize();
    }

    public bool AdvanceDungeonPhase()
    {
        if (CurrentPhase == EDungeonPhase.Event && _eventRewardPending)
            return false;

        return flowController != null && flowController.TryAdvance();
    }

    public void StartNewDungeonRun()
    {
        if (!_initialized || flowController == null ||
            !TryResolveBattleManager())
        {
            return;
        }

        if (_battleManager.HasSession)
            _battleManager.EndBattle(board);

        ResetPlayerParty();
        ResetRunResourcesAndItems(includeStartingConsumable: true);
        board.ClearAllStacks();
        _totalBattleCount = UnityEngine.Random.Range(
            MinimumBattleCount,
            MaximumBattleCount + 1);
        GenerateBattlePlans(_totalBattleCount);
        _currentBattleNumber = 1;
        _eventRewardPending = false;
        _runResult = EDungeonRunResult.None;
        _runActive = true;

        if (!flowController.StartBattleEventRun(_totalBattleCount))
        {
            _runActive = false;
            Debug.LogError("Failed to start the dungeon run flow.", this);
        }
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
            _currentBattleNumber - 1,
            0,
            Mathf.Max(0, _battlePlans.Length - 1));
        if (_battlePlans.Length == 0)
        {
            Debug.LogError("Dungeon battle plans were not generated.", this);
            return false;
        }

        if (!TryCreateScaledBattleSetup(
                _battlePlans[battleIndex],
                out BattleSetup setup,
                out string error))
        {
            Debug.LogError($"Failed to create scaled battle: {error}", this);
            return false;
        }

        board.Initialize(setup.FieldSize, setup.MaximumStackSize);
        _battleManager.ConfigureActiveSkillResource(
            _maximumEnergy,
            _energyRechargeDuration);
        return _battleManager.StartBattle(
            board,
            characters,
            setup.Enemies,
            setup.SpawnInterval,
            setup.TimeLimit);
    }

    private void GenerateBattlePlans(int battleCount)
    {
        battleCount = Mathf.Max(1, battleCount);
        _battlePlans = new DungeonBattlePlan[battleCount];
        int runSeed = Environment.TickCount ^
                      UnityEngine.Random.Range(0, int.MaxValue);
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
        int enemyCount = Mathf.Max(
            1,
            Mathf.RoundToInt(Mathf.Lerp(
                baselineEnemyCount,
                maximumScaledEnemyCount,
                progress)));
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
        IReadOnlyList<int> enemyHealthValues = CreateEnemyHealthDistribution(
            enemyCount,
            enemyHealth,
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

        int fieldSize = firstBattle != null
            ? firstBattle.FieldSize
            : initialGridSize;
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
            enemies);
        error = string.Empty;
        return true;
    }

    private int CalculateBaselineEnemyHealth()
    {
        CharacterData startingData = _startingTurret != null
            ? _startingTurret.CreateData()
            : null;
        int attackDamage = startingData != null
            ? startingData.AttackDamage
            : 1;
        float attackCycle = startingData != null
            ? startingData.AttackCooldown +
              CharacterRuntime.TargetAttackRecoveryDuration
            : 1.5f;
        float timeLimit = firstBattle != null
            ? firstBattle.TimeLimit
            : normalBattleTimeLimit;
        int expectedAttackCount = Mathf.Max(
            1,
            Mathf.FloorToInt(timeLimit / Mathf.Max(0.1f, attackCycle)));
        float totalDamageBudget = expectedAttackCount *
                                  attackDamage *
                                  baselineSoloDamageBudgetRatio;
        return Mathf.Max(
            1,
            Mathf.RoundToInt(totalDamageBudget / baselineEnemyCount));
    }

    private IReadOnlyList<EnemySO> GetBattleEnemyPool()
    {
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

    private static IReadOnlyList<int> CreateEnemyHealthDistribution(
        int enemyCount,
        int averageHealth,
        int healthVariance,
        System.Random random)
    {
        enemyCount = Mathf.Max(1, enemyCount);
        averageHealth = Mathf.Max(1, averageHealth);
        healthVariance = Mathf.Max(0, healthVariance);

        int minimumHealth = Mathf.Max(1, averageHealth - healthVariance);
        int maximumHealth = averageHealth + healthVariance;
        int remainingHealth = averageHealth * enemyCount;
        int[] healthValues = new int[enemyCount];
        bool hasVariation = false;

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
            hasVariation |= health != averageHealth;
        }

        if (!hasVariation && enemyCount >= 2 && minimumHealth < averageHealth)
        {
            healthValues[0]--;
            healthValues[1]++;
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
            _currentBattleNumber = flowController.CurrentBattleNumber;
            _eventRewardPending = false;
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
            if (phase == EDungeonPhase.Event)
            {
                _eventRewardPending = true;
                _eventTab?.ShowUpgradeEvent();
            }
        }

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
        if (CurrentPhase == EDungeonPhase.Battle && flowController != null &&
            !flowController.IsCompleted)
        {
            flowController.TryAdvance();
        }
    }

    private void HandleBattleEnded(EBattleResult result)
    {
        if (!_runActive || result == EBattleResult.Victory)
            return;

        _runActive = false;
        _eventRewardPending = false;
        _runResult = EDungeonRunResult.Defeat;
        _battleManager?.EndBattle(board);
        board?.ClearAllStacks();
        ResetPlayerParty();
        ResetRunResourcesAndItems(includeStartingConsumable: false);
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_runResult);
        RunEnded?.Invoke(_runResult);
    }

    private void HandleDungeonFlowCompleted()
    {
        if (!_runActive)
            return;

        _runActive = false;
        _eventRewardPending = false;
        _runResult = EDungeonRunResult.Clear;
        flowController?.ShowEventTab();
        _eventTab?.ShowRunResult(_runResult);
        RunEnded?.Invoke(_runResult);
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
        return _runActive &&
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
        {
            _startingTurret = playerCharacters[0]?.Definition;
            ResetPlayerParty();
        }
    }

    private void ResetPlayerParty()
    {
        _ownedTurrets.Clear();
        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null)
                continue;

            CharacterSO definition = index == 0
                ? _startingTurret
                : _slotDefaultDefinitions[index];
            if (definition != null)
                character.ConfigureDefinition(definition);
            character.ConfigurePartySlot(index, partySlotColors[index]);
            character.gameObject.SetActive(index == 0 && definition != null);
            if (index == 0 && definition != null)
                _ownedTurrets.Add(character);
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

    private readonly Color _panelColor = new(0.075f, 0.095f, 0.08f, 0.98f);
    private readonly Color _buttonColor = new(0.19f, 0.28f, 0.22f, 1f);
    private readonly Color _textColor = new(0.94f, 0.91f, 0.78f, 1f);

    private DungeonPage _page;
    private GameObject _root;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private RectTransform _buttonRoot;
    private readonly List<RewardOption> _currentRewardOptions = new();
    private bool _initialized;

    public void Initialize(GameObject root, DungeonPage page)
    {
        if (root == null || page == null)
            return;

        _root = root;
        _page = page;
        if (_initialized)
            return;

        foreach (TextMeshProUGUI text in
                 _root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == "txtEventPlaceholder")
                text.gameObject.SetActive(false);
        }

        BuildRuntimeUi();
        _initialized = true;
    }

    public void ShowUpgradeEvent()
    {
        if (!EnsureInitialized())
            return;

        GenerateRewardOptions();
        ShowCurrentRewardOptions();
    }

    public void ShowRunResult(EDungeonRunResult result)
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        bool cleared = result == EDungeonRunResult.Clear;
        _titleText.text = cleared ? "DUNGEON CLEAR" : "DEFEAT";
        _descriptionText.text = cleared
            ? $"ALL {_page.TotalBattleCount} BATTLES CLEARED"
            : "THE RUN WAS RESET\nSTART AGAIN WITH ONE BASIC TURRET";
        CreateButton("START NEW RUN", _page.StartNewDungeonRun);
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
        ClearButtons();
        _titleText.text = "CHOOSE REWARD";
        _descriptionText.text =
            $"BATTLE {_page.CurrentBattleNumber} / {_page.TotalBattleCount} CLEAR " +
            $"(SCALE {_page.CurrentDifficultyScale})\n" +
            $"NEXT SCALE {_page.GetBattleDifficultyScale(_page.CurrentBattleNumber + 1)}\n" +
            $"CHOOSE 1 OF {_currentRewardOptions.Count}";

        foreach (RewardOption option in _currentRewardOptions)
        {
            RewardOption selectedOption = option;
            CreateButton(GetRewardOptionLabel(option), () =>
            {
                SelectRewardOption(selectedOption);
            });
        }
    }

    private string GetRewardOptionLabel(RewardOption option)
    {
        if (option.Type == ERewardOptionType.EnergyUpgrade)
        {
            return option.EnergyUpgradeType ==
                   EDungeonEnergyUpgradeType.MaximumEnergy
                ? $"ENERGY UPGRADE | MAX {_page.MaximumEnergy} > " +
                  $"{_page.MaximumEnergy + 1}"
                : $"ENERGY UPGRADE | RECHARGE " +
                  $"{_page.EnergyRechargeDuration:0.0}s > " +
                  $"{Mathf.Max(DungeonPage.MinimumEnergyRechargeDuration, _page.EnergyRechargeDuration - DungeonPage.EnergyRechargeUpgradeAmount):0.0}s";
        }

        if (option.Type == ERewardOptionType.BattleItem)
        {
            BattleItemDefinition item = BattleItemCatalog.Get(
                option.BattleItemType);
            return $"ITEM | {item.DisplayName} x1\n" + item.Description;
        }

        if (option.Type == ERewardOptionType.NewTurret)
        {
            return option.TurretDefinition != null
                ? $"NEW TURRET | {option.TurretDefinition.CharacterName}"
                : "NEW TURRET";
        }

        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        int slotIndex = option.TurretSlotIndex;
        if (slotIndex < 0 || slotIndex >= turrets.Count ||
            turrets[slotIndex]?.Data == null)
        {
            return "TURRET UPGRADE";
        }

        CharacterData data = turrets[slotIndex].Data;
        return $"UPGRADE | S{slotIndex + 1} {data.CharacterName}\n" +
               data.GetUpgradeLabel(option.UpgradeType);
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
        ClearButtons();
        _titleText.text = "REPLACE TURRET";
        _descriptionText.text =
            $"NEW: {newDefinition.CharacterName}\n" +
            "THE REPLACED TURRET'S UPGRADES WILL BE LOST";

        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        for (int index = 0; index < turrets.Count; index++)
        {
            int slotIndex = index;
            CreateButton(
                GetTurretSummary(slotIndex, turrets[index]),
                () => _page.TryAcquireTurret(newDefinition, slotIndex));
        }
        CreateButton("BACK", ShowCurrentRewardOptions);
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
            return $"SLOT {slotIndex + 1}";

        return data.AttackType == CharacterAttackType.FireRandom
            ? $"S{slotIndex + 1} {data.CharacterName} | " +
              $"FIRE {data.FireDuration:0.#}s | TARGET x{data.FireSkillTargetCount}"
            : $"S{slotIndex + 1} {data.CharacterName} | " +
              $"ATK {data.AttackDamage} | SK {data.SkillAttackDamage}";
    }

    private void BuildRuntimeUi()
    {
        GameObject panelObject = new(
            "grpRuntimeEventPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        RectTransform panel = (RectTransform)panelObject.transform;
        panel.SetParent(_root.transform, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(720f, 680f);

        panelObject.GetComponent<Image>().color = _panelColor;
        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 32, 32);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        _titleText = CreateText(panel, "txtEventTitle", 36f, 72f);
        _descriptionText = CreateText(
            panel,
            "txtEventDescription",
            24f,
            90f);

        GameObject buttonRootObject = new(
            "grpEventButtons",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _buttonRoot = (RectTransform)buttonRootObject.transform;
        _buttonRoot.SetParent(panel, false);
        LayoutElement rootLayout = buttonRootObject.GetComponent<LayoutElement>();
        rootLayout.preferredHeight = 460f;
        rootLayout.flexibleHeight = 1f;
        VerticalLayoutGroup buttonLayout =
            buttonRootObject.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childAlignment = TextAnchor.UpperCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandHeight = false;
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
        text.fontSize = fontSize;
        text.color = _textColor;
        text.alignment = TextAlignmentOptions.Center;
        textObject.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
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
        if (_buttonRoot == null)
            return;

        for (int index = _buttonRoot.childCount - 1; index >= 0; index--)
        {
            GameObject child = _buttonRoot.GetChild(index).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
