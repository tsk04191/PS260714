using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonBoardView : MonoBehaviour, IBattleBoard,
    IDungeonStageProgressProvider,
    IBattlePresentationEventPublisher,
    IBattleVfxTargetResolver,
    IBattleManualTargetSelectionService
{
    private const int MaximumStatusEventsPerDispatch = 128;
    private const int MaximumDefeatEventsPerDispatch = 128;
    private const int MaximumPresentationEventsPerDispatch = 256;
    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;

    private sealed class EnemyPlacement
    {
        public EnemyRuntime Enemy { get; }
        public DungeonTileView Anchor { get; }
        public IReadOnlyList<DungeonTileView> OccupiedTiles { get; }
        public bool IsExclusive { get; }

        public EnemyPlacement(
            EnemyRuntime enemy,
            DungeonTileView anchor,
            IReadOnlyList<DungeonTileView> occupiedTiles,
            bool isExclusive)
        {
            Enemy = enemy;
            Anchor = anchor;
            OccupiedTiles = occupiedTiles;
            IsExclusive = isExclusive;
        }
    }

    private readonly struct PendingEnemyPlacement
    {
        public EnemyRuntime Enemy { get; }
        public DungeonTileView Anchor { get; }
        public IReadOnlyList<DungeonTileView> OccupiedTiles { get; }
        public bool IsExclusive { get; }

        public PendingEnemyPlacement(
            EnemyRuntime enemy,
            DungeonTileView anchor,
            IReadOnlyList<DungeonTileView> occupiedTiles,
            bool isExclusive)
        {
            Enemy = enemy;
            Anchor = anchor;
            OccupiedTiles = occupiedTiles;
            IsExclusive = isExclusive;
        }
    }

    [SerializeField] private RectTransform boardRect;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private DungeonTileView tilePrefab;

    [Header("3D VFX")]
    [SerializeField] private BattleVfxPlayer vfxPlayer;
    [SerializeField]
    private BattleVfxQualityProfileSO vfxQualityProfile;

    private readonly List<DungeonTileView> _tiles = new();
    private readonly Dictionary<EnemyRuntime, EnemyPlacement>
        _enemyPlacements = new();
    private readonly Dictionary<DungeonTileView, EnemyRuntime>
        _exclusiveOccupants = new();
    private readonly List<IBattleCharacter> _battleCharacters = new();
    private Func<EnemyRuntime, bool> _itemTargetHandler;
    private BattleManualTargetSelectionRequest _manualTargetRequest;
    private readonly List<EnemyRuntime> _manualEnemyTargets = new();
    private readonly List<IBattleCharacter> _manualAllyTargets = new();
    private EnemyRuntime _forcedPriorityTarget;
    private float _forcedPriorityRemaining;
    private int _maximumStackSize = 8;
    private bool _initialized;
    private readonly Queue<BattleStatusAppliedEvent> _statusEventQueue = new();
    private bool _dispatchingStatusEvents;
    private readonly Queue<BattleEnemyDefeatedEvent> _defeatEventQueue = new();
    private bool _dispatchingDefeatEvents;
    private readonly Queue<BattleEffectResolvedEvent>
        _effectResolvedEventQueue = new();
    private readonly Queue<StatusEffectLifecycleEvent>
        _statusLifecycleEventQueue = new();
    private readonly Queue<BattleUnitLifecycleEvent>
        _unitLifecycleEventQueue = new();
    private bool _dispatchingEffectResolvedEvents;
    private bool _dispatchingStatusLifecycleEvents;
    private bool _dispatchingUnitLifecycleEvents;
    private readonly HashSet<EnemyRuntime> _boundPresentationEnemies = new();
    private readonly HashSet<CharacterRuntime>
        _boundPresentationCharacters = new();
    private readonly Dictionary<EnemyRuntime, BattleVfxTargetHandle>
        _enemyVfxHandles = new();
    private readonly Dictionary<IBattleCharacter, BattleVfxTargetHandle>
        _allyVfxHandles = new();
    private readonly Dictionary<
        EnemyRuntime,
        Dictionary<BattleVfxAnchorType, BattleVfxAnchorSnapshot>>
        _lastEnemyVfxAnchors = new();
    private int _nextVfxTargetHandle = 1;
    private BattlePresentationDispatcher _presentationDispatcher;

    public int GridSize { get; private set; } = MinimumGridSize;
    public float DungeonStageProgress { get; private set; }
    public RectTransform HighlightRect => boardRect != null
        ? boardRect
        : transform as RectTransform;
    public int InitialEnemyCapacity => GridSize * GridSize;
    public int LivingEnemyCount
    {
        get
        {
            int count = 0;
            foreach (DungeonTileView tile in _tiles)
            {
                if (tile != null)
                    count += tile.StackCount;
            }

            return count;
        }
    }
    public bool HasEmptyEnemyTile
    {
        get
        {
            foreach (DungeonTileView tile in _tiles)
            {
                if (tile != null &&
                    tile.StackCount == 0 &&
                    !_exclusiveOccupants.ContainsKey(tile))
                    return true;
            }

            return false;
        }
    }
    public BattleVfxPlayer VfxPlayer => vfxPlayer;
    public bool IsManualTargetSelectionPending =>
        _manualTargetRequest != null;
    public BattleManualTargetSelectionRequest CurrentManualTargetRequest =>
        _manualTargetRequest;
    public int CurrentManualSelectedCount =>
        _manualTargetRequest?.Faction == CharacterTargetFaction.Ally
            ? _manualAllyTargets.Count
            : _manualEnemyTargets.Count;
    public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
    public event Action OccupancyChanged;
    public event Action<EnemyRuntime> EnemyClicked;
    public event Action<BattleStatusAppliedEvent> StatusApplied;
    public event Action<BattleEffectResolvedEvent> EffectResolved;
    public event Action<StatusEffectLifecycleEvent> StatusLifecycle;
    public event Action<BattleUnitLifecycleEvent> UnitLifecycle;
    public event Action<bool> ManualTargetSelectionPendingChanged;
    public event Action ManualTargetSelectionProgressChanged;

    public void PublishEffectResolved(BattleEffectResolvedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _effectResolvedEventQueue.Enqueue(eventData);
        if (_dispatchingEffectResolvedEvents)
            return;

        _dispatchingEffectResolvedEvents = true;
        try
        {
            int processedCount = 0;
            while (_effectResolvedEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    EffectResolved,
                    _effectResolvedEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _effectResolvedEventQueue,
                "effect result");
        }
        finally
        {
            _dispatchingEffectResolvedEvents = false;
        }
    }

    public void PublishStatusLifecycle(StatusEffectLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _statusLifecycleEventQueue.Enqueue(eventData);
        if (_dispatchingStatusLifecycleEvents)
            return;

        _dispatchingStatusLifecycleEvents = true;
        try
        {
            int processedCount = 0;
            while (_statusLifecycleEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    StatusLifecycle,
                    _statusLifecycleEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _statusLifecycleEventQueue,
                "status lifecycle");
        }
        finally
        {
            _dispatchingStatusLifecycleEvents = false;
        }
    }

    public void PublishUnitLifecycle(BattleUnitLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _unitLifecycleEventQueue.Enqueue(eventData);
        if (_dispatchingUnitLifecycleEvents)
            return;

        _dispatchingUnitLifecycleEvents = true;
        try
        {
            int processedCount = 0;
            while (_unitLifecycleEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    UnitLifecycle,
                    _unitLifecycleEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _unitLifecycleEventQueue,
                "unit lifecycle");
        }
        finally
        {
            _dispatchingUnitLifecycleEvents = false;
        }
    }

    private void InvokeSafely<T>(Action<T> handlers, T eventData)
    {
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler).Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void DiscardExcessPresentationEvents<T>(
        Queue<T> queue,
        string eventName)
    {
        if (queue.Count == 0)
            return;

        int discardedCount = queue.Count;
        queue.Clear();
        Debug.LogError(
            $"Battle presentation {eventName} dispatch exceeded " +
            $"{MaximumPresentationEventsPerDispatch} events. " +
            $"Discarded {discardedCount} queued events.",
            this);
    }

    public void NotifyStatusApplied(BattleStatusAppliedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _statusEventQueue.Enqueue(eventData);
        if (_dispatchingStatusEvents)
            return;

        _dispatchingStatusEvents = true;
        try
        {
            int processedCount = 0;
            while (_statusEventQueue.Count > 0 &&
                   processedCount < MaximumStatusEventsPerDispatch)
            {
                BattleStatusAppliedEvent queuedEvent =
                    _statusEventQueue.Dequeue();
                StatusApplied?.Invoke(queuedEvent);
                processedCount++;
            }

            if (_statusEventQueue.Count > 0)
            {
                Debug.LogError(
                    "Status event dispatch limit exceeded. " +
                    "Remaining chained status events were discarded.",
                    this);
                _statusEventQueue.Clear();
            }
        }
        finally
        {
            _dispatchingStatusEvents = false;
        }
    }

    private void NotifyEnemyDefeated(BattleEnemyDefeatedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _defeatEventQueue.Enqueue(eventData);
        if (_dispatchingDefeatEvents)
            return;

        _dispatchingDefeatEvents = true;
        try
        {
            int processedCount = 0;
            while (_defeatEventQueue.Count > 0 &&
                   processedCount < MaximumDefeatEventsPerDispatch)
            {
                BattleEnemyDefeatedEvent queuedEvent =
                    _defeatEventQueue.Dequeue();
                EnemyDefeated?.Invoke(queuedEvent);
                processedCount++;
            }

            if (_defeatEventQueue.Count > 0)
            {
                Debug.LogError(
                    "Enemy defeat event dispatch limit exceeded. " +
                    "Remaining chained defeat events were discarded.",
                    this);
                _defeatEventQueue.Clear();
            }
        }
        finally
        {
            _dispatchingDefeatEvents = false;
        }
    }

    public void BindItemTargetHandler(
        Func<EnemyRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
    }

    public bool TryBeginManualTargetSelection(
        BattleManualTargetSelectionRequest request)
    {
        if (request == null || request.Source == null ||
            request.RequiredCount <= 0 ||
            IsManualTargetSelectionPending)
        {
            return false;
        }

        _manualTargetRequest = request;
        _manualEnemyTargets.Clear();
        _manualAllyTargets.Clear();
        RefreshManualTargetHighlights();
        ManualTargetSelectionPendingChanged?.Invoke(true);
        ManualTargetSelectionProgressChanged?.Invoke();
        return true;
    }

    public void CancelManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        ClearManualTargetSelection();
        request.Complete(new BattleManualTargetSelectionResult(
            request.Faction,
            null,
            null,
            true));
    }

    private void CompleteManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        BattleManualTargetSelectionResult result = new(
            request.Faction,
            _manualEnemyTargets.ToArray(),
            _manualAllyTargets.ToArray());
        ClearManualTargetSelection();
        request.Complete(result);
    }

    private void ClearManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        _manualTargetRequest = null;
        _manualEnemyTargets.Clear();
        _manualAllyTargets.Clear();
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        ManualTargetSelectionPendingChanged?.Invoke(false);
    }

    private bool HandleManualEnemyClicked(EnemyRuntime enemy)
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        if (request == null ||
            request.Faction != CharacterTargetFaction.Enemy ||
            enemy == null ||
            !ContainsReference(request.EnemyCandidates, enemy))
        {
            return false;
        }

        if (!_manualEnemyTargets.Remove(enemy))
            _manualEnemyTargets.Add(enemy);
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        if (_manualEnemyTargets.Count >= request.RequiredCount)
            CompleteManualTargetSelection();
        return true;
    }

    private bool HandleManualAllyClicked(CharacterRuntime character)
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        if (request == null ||
            request.Faction != CharacterTargetFaction.Ally ||
            character == null ||
            !ContainsReference(request.AllyCandidates, character))
        {
            return false;
        }

        if (!_manualAllyTargets.Remove(character))
            _manualAllyTargets.Add(character);
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        if (_manualAllyTargets.Count >= request.RequiredCount)
            CompleteManualTargetSelection();
        return true;
    }

    private void RefreshManualTargetHighlights()
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        foreach (DungeonTileView tile in _tiles)
        {
            EnemyRuntime enemy = tile?.TopEnemy;
            bool candidate = request != null &&
                             request.Faction ==
                             CharacterTargetFaction.Enemy &&
                             ContainsReference(
                                 request.EnemyCandidates,
                                 enemy);
            tile?.SetManualSelectionState(
                candidate,
                candidate && _manualEnemyTargets.Contains(enemy));
        }

        foreach (CharacterRuntime character in
                 _boundPresentationCharacters)
        {
            bool candidate = request != null &&
                             request.Faction ==
                             CharacterTargetFaction.Ally &&
                             ContainsReference(
                                 request.AllyCandidates,
                                 character);
            character?.SetManualTargetSelectionState(
                candidate,
                candidate && _manualAllyTargets.Contains(character));
        }
    }

    private static bool ContainsReference<T>(
        IReadOnlyList<T> values,
        T target)
        where T : class
    {
        if (values == null || target == null)
            return false;

        foreach (T value in values)
        {
            if (ReferenceEquals(value, target))
                return true;
        }

        return false;
    }

    public void SetBattleCharacters(
        IReadOnlyList<IBattleCharacter> characters)
    {
        HashSet<IBattleCharacter> previousCharacters =
            new(_battleCharacters);
        UnbindAllPresentationCharacters();
        _battleCharacters.Clear();
        if (characters == null)
            return;

        foreach (IBattleCharacter character in characters)
        {
            if (character != null && !_battleCharacters.Contains(character))
            {
                _battleCharacters.Add(character);
                CharacterRuntime runtime = character as CharacterRuntime;
                BindPresentationCharacter(runtime);
                if (!previousCharacters.Contains(character) &&
                    runtime?.Definition != null)
                {
                    PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                        BattleUnitLifecycleType.Spawned,
                        BattleStatusTarget.FromAlly(character),
                        runtime.Definition));
                }
            }
        }
    }

    public BattleVfxTarget ResolveVfxTarget(
        BattleStatusTarget target,
        BattleVfxAnchorType anchorType)
    {
        if (!target.IsValid)
            return default;

        if (target.Enemy != null)
        {
            BattleVfxTargetHandle handle =
                GetOrCreateEnemyVfxHandle(target.Enemy);
            BattleVfxAnchorSnapshot anchor = default;
            if (TryFindEnemyTile(target.Enemy, out DungeonTileView tile) &&
                tile.TryGetEnemyVfxAnchor(
                    target.Enemy,
                    anchorType,
                    out BattleVfxAnchorSnapshot liveAnchor))
            {
                anchor = liveAnchor;
                StoreEnemyVfxAnchor(target.Enemy, anchorType, liveAnchor);
            }
            else
            {
                TryGetStoredEnemyVfxAnchor(
                    target.Enemy,
                    anchorType,
                    out anchor);
            }

            return new BattleVfxTarget(handle, target, anchor);
        }

        BattleVfxTargetHandle allyHandle =
            GetOrCreateAllyVfxHandle(target.Ally);
        BattleVfxAnchorSnapshot allyAnchor = default;
        if (target.Ally is IBattleVfxAnchorProvider provider)
        {
            provider.TryGetVfxAnchor(anchorType, out allyAnchor);
            if (!allyAnchor.HasFrame &&
                TryGetVfxTileFrame(out RectTransform tileFrame))
            {
                BattleVfxUiAnchorUtility.TryAttachScreenFrame(
                    allyAnchor,
                    tileFrame,
                    out allyAnchor);
            }
        }
        return new BattleVfxTarget(allyHandle, target, allyAnchor);
    }

    private bool TryGetVfxTileFrame(out RectTransform tileFrame)
    {
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile == null || !tile.gameObject.activeInHierarchy)
                continue;

            tileFrame = tile.transform as RectTransform;
            if (tileFrame != null)
                return true;
        }

        tileFrame = null;
        return false;
    }

    public void Initialize(int gridSize, int stackSize)
    {
        EnsurePresentationPipeline();
        if (boardRect == null || gridLayout == null || tilePrefab == null)
        {
            Debug.LogError("DungeonBoardView scene and prefab references are incomplete.", this);
            return;
        }

        _maximumStackSize = Mathf.Max(1, stackSize);
        _initialized = true;
        CollectSceneTiles(gridSize);
        SetGridSize(gridSize);
    }

    public void SetDungeonStageProgress(float progress)
    {
        DungeonStageProgress = float.IsNaN(progress) ||
                               float.IsInfinity(progress)
            ? 0f
            : Mathf.Max(0f, progress);
    }

    public void SetPixelSize(float size)
    {
        if (boardRect == null)
            return;

        size = Mathf.Max(1f, size);
        boardRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            size);
        boardRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            size);
        RefreshLayout();
    }

    public void SetGridSize(int size)
    {
        if (!_initialized)
            return;

        size = Mathf.Clamp(size, MinimumGridSize, MaximumGridSize);

        if (size == GridSize && _tiles.Count == size * size)
        {
            RefreshLayout();
            return;
        }

        List<EnemyRuntime>[,] previousEnemies = CaptureExistingStacks();
        int previousSize = GridSize;

        ClearTileObjects();
        _enemyPlacements.Clear();
        _exclusiveOccupants.Clear();
        GridSize = size;
        gridLayout.constraintCount = GridSize;

        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                DungeonTileView tile = Instantiate(tilePrefab, gridLayout.transform);
                tile.name = $"grpDungeonTile_{row}_{column}";
                tile.Initialize(row, column, _maximumStackSize);
                BindTile(tile);
                _tiles.Add(tile);
            }
        }

        RestoreExistingStacks(previousEnemies, previousSize);
        SynchronizeEnemyPresentationBindings();
        RefreshLayout();
        OccupancyChanged?.Invoke();
    }

    public bool TryAddEnemyCard(
        int row,
        int column,
        EnemyRuntime enemy)
    {
        if (enemy == null ||
            !TryBuildPlacementAt(
                row,
                column,
                enemy,
                null,
                out PendingEnemyPlacement placement))
        {
            return false;
        }

        return TryCommitPlacements(new[] { placement });
    }

    public bool TryAddEnemyCardToRandomTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, null);
        if (candidates.Count == 0)
            return false;

        int index = Random.Range(0, candidates.Count);
        return TryCommitPlacements(new[] { candidates[index] });
    }

    public bool TryAddEnemyCardToNextAvailableTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, null);
        if (candidates.Count == 0)
            return false;

        List<PendingEnemyPlacement> best = new();
        int smallestStackCount = int.MaxValue;
        foreach (PendingEnemyPlacement candidate in candidates)
        {
            int stackCount = candidate.Anchor.StackCount;
            if (stackCount < smallestStackCount)
            {
                smallestStackCount = stackCount;
                best.Clear();
            }

            if (stackCount == smallestStackCount)
                best.Add(candidate);
        }

        int randomIndex = Random.Range(0, best.Count);
        return TryCommitPlacements(new[] { best[randomIndex] });
    }

    public bool TryAddEnemy(EnemyRuntime enemy)
    {
        return TryAddEnemyCardToNextAvailableTile(enemy);
    }

    public bool TryAddEnemiesToDistinctTiles(
        IReadOnlyList<EnemyRuntime> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            return false;

        HashSet<DungeonTileView> reservedTiles = new();
        List<PendingEnemyPlacement> placements = new(enemies.Count);
        return TryBuildGroupPlacements(
                   enemies,
                   0,
                   reservedTiles,
                   placements) &&
               TryCommitPlacements(placements);
    }

    private bool TryBuildGroupPlacements(
        IReadOnlyList<EnemyRuntime> enemies,
        int index,
        HashSet<DungeonTileView> reservedTiles,
        List<PendingEnemyPlacement> placements)
    {
        if (index >= enemies.Count)
            return true;

        EnemyRuntime enemy = enemies[index];
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, reservedTiles);
        foreach (PendingEnemyPlacement candidate in candidates)
        {
            foreach (DungeonTileView tile in candidate.OccupiedTiles)
                reservedTiles.Add(tile);
            placements.Add(candidate);

            if (TryBuildGroupPlacements(
                    enemies,
                    index + 1,
                    reservedTiles,
                    placements))
            {
                return true;
            }

            placements.RemoveAt(placements.Count - 1);
            foreach (DungeonTileView tile in candidate.OccupiedTiles)
                reservedTiles.Remove(tile);
        }

        return false;
    }

    private List<PendingEnemyPlacement> CollectPlacementCandidates(
        EnemyRuntime enemy,
        ISet<DungeonTileView> reservedTiles)
    {
        List<PendingEnemyPlacement> result = new();
        if (enemy == null)
            return result;

        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                if (TryBuildPlacementAt(
                        row,
                        column,
                        enemy,
                        reservedTiles,
                        out PendingEnemyPlacement placement))
                {
                    result.Add(placement);
                }
            }
        }

        result.Sort((left, right) =>
        {
            int stack = left.Anchor.StackCount.CompareTo(
                right.Anchor.StackCount);
            if (stack != 0)
                return stack;
            int row = left.Anchor.Row.CompareTo(right.Anchor.Row);
            return row != 0
                ? row
                : left.Anchor.Column.CompareTo(right.Anchor.Column);
        });
        return result;
    }

    private bool TryBuildPlacementAt(
        int row,
        int column,
        EnemyRuntime enemy,
        ISet<DungeonTileView> reservedTiles,
        out PendingEnemyPlacement placement)
    {
        placement = default;
        if (enemy?.Definition == null ||
            _enemyPlacements.ContainsKey(enemy))
        {
            return false;
        }

        EnemySO definition = enemy.Definition;
        bool exclusive = definition.StackingPolicy ==
                         EnemyStackingPolicy.Exclusive;
        int width = exclusive ? definition.FootprintWidth : 1;
        int height = exclusive ? definition.FootprintHeight : 1;
        if (row < 0 || column < 0 ||
            row + height > GridSize ||
            column + width > GridSize)
        {
            return false;
        }

        List<DungeonTileView> occupiedTiles = new(width * height);
        for (int rowOffset = 0; rowOffset < height; rowOffset++)
        {
            for (int columnOffset = 0;
                 columnOffset < width;
                 columnOffset++)
            {
                if (!TryGetTile(
                        row + rowOffset,
                        column + columnOffset,
                        out DungeonTileView tile) ||
                    tile == null ||
                    reservedTiles?.Contains(tile) == true ||
                    _exclusiveOccupants.ContainsKey(tile))
                {
                    return false;
                }

                if (exclusive && tile.StackCount > 0)
                    return false;
                occupiedTiles.Add(tile);
            }
        }

        DungeonTileView anchor = occupiedTiles[0];
        if (!anchor.CanAddEnemy ||
            (!exclusive && anchor.IsFull))
        {
            return false;
        }

        placement = new PendingEnemyPlacement(
            enemy,
            anchor,
            occupiedTiles,
            exclusive);
        return true;
    }

    private bool TryCommitPlacements(
        IReadOnlyList<PendingEnemyPlacement> placements)
    {
        if (placements == null || placements.Count == 0)
            return false;

        List<PendingEnemyPlacement> added = new(placements.Count);
        foreach (PendingEnemyPlacement placement in placements)
        {
            if (placement.Enemy == null ||
                placement.Anchor == null ||
                !placement.Anchor.TryAdd(placement.Enemy))
            {
                for (int index = added.Count - 1; index >= 0; index--)
                    added[index].Anchor.TryRemoveTop();
                return false;
            }
            added.Add(placement);
        }

        foreach (PendingEnemyPlacement pending in added)
            RegisterPlacement(pending);

        foreach (PendingEnemyPlacement pending in added)
        {
            EnemyRuntime enemy = pending.Enemy;
            BindPresentationEnemy(enemy);
            PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                BattleUnitLifecycleType.Spawned,
                BattleStatusTarget.FromEnemy(enemy),
                enemy.Definition));
            ExecuteSpawnAbilities(pending.Anchor, enemy);
            pending.Anchor.RefreshTopEnemyCard();
        }

        OccupancyChanged?.Invoke();
        return true;
    }

    private void RegisterPlacement(PendingEnemyPlacement pending)
    {
        EnemyPlacement placement = new(
            pending.Enemy,
            pending.Anchor,
            pending.OccupiedTiles,
            pending.IsExclusive);
        _enemyPlacements[pending.Enemy] = placement;
        if (!pending.IsExclusive)
            return;

        foreach (DungeonTileView tile in pending.OccupiedTiles)
        {
            _exclusiveOccupants[tile] = pending.Enemy;
            tile.SetExclusiveFootprintOccupant(
                pending.Enemy,
                ReferenceEquals(tile, pending.Anchor));
        }
    }

    private void ReleasePlacement(
        EnemyRuntime enemy,
        bool notify = true)
    {
        if (enemy == null ||
            !_enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement))
        {
            return;
        }

        _enemyPlacements.Remove(enemy);

        if (placement.IsExclusive)
        {
            foreach (DungeonTileView tile in placement.OccupiedTiles)
            {
                if (tile != null &&
                    _exclusiveOccupants.TryGetValue(
                        tile,
                        out EnemyRuntime occupant) &&
                    ReferenceEquals(occupant, enemy))
                {
                    _exclusiveOccupants.Remove(tile);
                    tile.SetExclusiveFootprintOccupant(null, false);
                }
            }
        }

        if (notify)
            OccupancyChanged?.Invoke();
    }

    private EnemyRuntime GetEnemyAtTile(DungeonTileView tile)
    {
        if (tile == null)
            return null;
        return _exclusiveOccupants.TryGetValue(
            tile,
            out EnemyRuntime occupant)
            ? occupant
            : tile.TopEnemy;
    }

    private DungeonTileView ResolveAnchorTile(DungeonTileView tile)
    {
        EnemyRuntime enemy = GetEnemyAtTile(tile);
        return enemy != null &&
               _enemyPlacements.TryGetValue(
                   enemy,
                   out EnemyPlacement placement)
            ? placement.Anchor
            : tile;
    }

    public bool TryRemoveTopEnemyCard(int row, int column)
    {
        if (!TryGetTile(row, column, out DungeonTileView selectedTile))
            return false;

        DungeonTileView tile = ResolveAnchorTile(selectedTile);
        EnemyRuntime enemy = GetEnemyAtTile(selectedTile);
        if (tile == null || enemy == null ||
            !ReferenceEquals(tile.TopEnemy, enemy))
        {
            return false;
        }

        CaptureEnemyVfxAnchor(enemy, tile);
        bool removed = tile.TryRemoveTop();
        if (removed)
        {
            ReleasePlacement(enemy, false);
            SynchronizeEnemyPresentationBindings();
            OccupancyChanged?.Invoke();
        }
        return removed;
    }

    public int GetStackCount(int row, int column)
    {
        if (!TryGetTile(row, column, out DungeonTileView tile))
            return 0;
        return _exclusiveOccupants.ContainsKey(tile)
            ? 1
            : tile.StackCount;
    }

    public int GetTopEnemyHealth(int row, int column)
    {
        return TryGetTile(row, column, out DungeonTileView tile)
            ? GetEnemyAtTile(tile)?.Health ?? 0
            : 0;
    }

    public bool TrySetTopEnemyHealth(int row, int column, int health)
    {
        if (!TryGetTile(row, column, out DungeonTileView tile))
            return false;
        DungeonTileView anchor = ResolveAnchorTile(tile);
        return anchor != null && anchor.TrySetTopEnemyHealth(health);
    }

    public bool ContainsTargetableEnemy(EnemyRuntime enemy)
    {
        return TryFindEnemyTile(enemy, out _);
    }

    public int TryDamageEnemy(EnemyRuntime enemy, int damage)
    {
        if (damage <= 0 ||
            !TryFindEnemyTile(enemy, out DungeonTileView tile))
        {
            return 0;
        }

        bool wasAlive = enemy.Health > 0;
        tile.ShowAttackRange();
        int appliedDamage = TryDamageTile(tile, damage);
        if (wasAlive && enemy.Health <= 0)
        {
            PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                BattleUnitLifecycleType.Defeated,
                BattleStatusTarget.FromEnemy(enemy),
                enemy.Definition));
        }
        return appliedDamage;
    }

    public bool TryApplyFireToEnemy(
        EnemyRuntime enemy,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        if (!TryFindEnemyTile(enemy, out DungeonTileView tile))
            return false;

        bool applied = TryApplyFireStatus(
            tile,
            null,
            duration,
            tickInterval,
            tickDamage);
        if (applied && ReferenceEquals(tile.TopEnemy, enemy))
            tile.ShowAttackRange();
        return applied;
    }

    public bool TryForcePriorityTarget(EnemyRuntime enemy, float duration)
    {
        duration = TimePrecision.Normalize(duration, 0.1f);
        if (duration <= 0f || !TryFindEnemyTile(enemy, out _))
            return false;

        _forcedPriorityTarget = enemy;
        _forcedPriorityRemaining = duration;
        return true;
    }

    public IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null)
            return Array.Empty<EnemyRuntime>();

        // Ally-only modes are normalized defensively in case serialized data
        // changes faction without going through the character editor.
        if (subject == CharacterAttackSubject.Self ||
            subject == CharacterAttackSubject.RandomExceptSelf ||
            subject == CharacterAttackSubject.None ||
            subject == CharacterAttackSubject.Manual)
            subject = CharacterAttackSubject.Random;
        else if (subject == CharacterAttackSubject.AllExceptSelf)
            subject = CharacterAttackSubject.All;

        List<DungeonTileView> candidates = CollectPriorityTargetTiles(
            out bool hasAlternateTarget);
        candidates.RemoveAll(tile => !MatchesCharacterConditions(
            source,
            tile,
            conditionMatchMode,
            numericConditions));
        if (candidates.Count == 0)
            return Array.Empty<EnemyRuntime>();

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        List<DungeonTileView> selected = new(candidates.Count);
        if (TryGetForcedPriorityTile(out DungeonTileView forcedTarget) &&
            candidates.Remove(forcedTarget))
        {
            selected.Add(forcedTarget);
        }

        if (subject == CharacterAttackSubject.All)
        {
            selected.AddRange(candidates);
        }
        else if (subject == CharacterAttackSubject.Random)
        {
            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
            StableSortByTargetPriority(
                candidates,
                hasAlternateTarget);
            for (int index = 0;
                 index < candidates.Count && selected.Count < targetCount;
                 index++)
            {
                selected.Add(candidates[index]);
            }
        }
        else
        {
            bool descending = subject == CharacterAttackSubject.HighestValue;
            candidates.Sort((left, right) =>
            {
                int priorityComparison = CompareTargetPriority(
                    left,
                    right,
                    hasAlternateTarget);
                if (priorityComparison != 0)
                    return priorityComparison;

                int leftValue = metric == CharacterAttackSubjectMetric.Health
                    ? left.TopEnemyHealth
                    : metric == CharacterAttackSubjectMetric.Shield
                        ? left.TopEnemy.CurrentShield
                        : left.StackCount;
                int rightValue = metric == CharacterAttackSubjectMetric.Health
                    ? right.TopEnemyHealth
                    : metric == CharacterAttackSubjectMetric.Shield
                        ? right.TopEnemy.CurrentShield
                        : right.StackCount;
                int comparison = leftValue.CompareTo(rightValue);
                return descending ? -comparison : comparison;
            });

            for (int index = 0;
                 index < candidates.Count && selected.Count < targetCount;
                 index++)
            {
                selected.Add(candidates[index]);
            }
        }

        List<EnemyRuntime> result = new(selected.Count);
        foreach (DungeonTileView tile in selected)
        {
            if (tile?.TopEnemy != null)
                result.Add(tile.TopEnemy);
        }

        return result;
    }

    public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null)
            return Array.Empty<IBattleCharacter>();

        if (subject == CharacterAttackSubject.None ||
            subject == CharacterAttackSubject.Manual)
            subject = CharacterAttackSubject.Random;

        List<IBattleCharacter> candidates = new();
        foreach (IBattleCharacter character in _battleCharacters)
        {
            if (character != null && MatchesCharacterConditions(
                    source,
                    character,
                    conditionMatchMode,
                    numericConditions))
            {
                candidates.Add(character);
            }
        }

        if (candidates.Count == 0)
            return Array.Empty<IBattleCharacter>();

        if (subject == CharacterAttackSubject.Self)
        {
            foreach (IBattleCharacter candidate in candidates)
            {
                if (ReferenceEquals(candidate, source))
                    return new[] { candidate };
            }

            return Array.Empty<IBattleCharacter>();
        }

        if (subject == CharacterAttackSubject.AllExceptSelf)
        {
            candidates.RemoveAll(candidate => ReferenceEquals(
                candidate,
                source));
            return candidates;
        }

        if (subject == CharacterAttackSubject.RandomExceptSelf)
        {
            candidates.RemoveAll(candidate => ReferenceEquals(
                candidate,
                source));
            if (candidates.Count == 0)
                return Array.Empty<IBattleCharacter>();
            subject = CharacterAttackSubject.Random;
        }

        if (subject == CharacterAttackSubject.All)
            return candidates;

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        if (subject == CharacterAttackSubject.Random)
        {
            for (int index = 0; index < targetCount; index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
        }
        else
        {
            bool descending = subject == CharacterAttackSubject.HighestValue;
            candidates.Sort((left, right) =>
            {
                float leftValue = GetCharacterMetric(left, metric);
                float rightValue = GetCharacterMetric(right, metric);
                int comparison = leftValue.CompareTo(rightValue);
                return descending ? -comparison : comparison;
            });
        }

        if (candidates.Count > targetCount)
            candidates.RemoveRange(targetCount, candidates.Count - targetCount);
        return candidates;
    }

    public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null || targets == null || targets.Count == 0)
            return Array.Empty<EnemyRuntime>();

        List<EnemyRuntime> result = new(targets.Count);
        foreach (EnemyRuntime target in targets)
        {
            if (target != null &&
                TryFindEnemyTile(target, out DungeonTileView tile) &&
                MatchesCharacterConditions(
                    source,
                    tile,
                    conditionMatchMode,
                    numericConditions))
            {
                result.Add(target);
            }
        }

        return result;
    }

    public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null || targets == null || targets.Count == 0)
            return Array.Empty<IBattleCharacter>();

        List<IBattleCharacter> result = new(targets.Count);
        foreach (IBattleCharacter target in targets)
        {
            if (target != null &&
                _battleCharacters.Contains(target) &&
                MatchesCharacterConditions(
                    source,
                    target,
                    conditionMatchMode,
                    numericConditions))
            {
                result.Add(target);
            }
        }

        return result;
    }

    public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
    {
        return ExpandCharacterAreaTargets(
            centerTargets,
            areaOffsets,
            true);
    }

    public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets,
        bool includeCenterTargets)
    {
        if (centerTargets == null || centerTargets.Count == 0)
            return Array.Empty<EnemyRuntime>();

        List<EnemyRuntime> result = new();
        HashSet<EnemyRuntime> uniqueEnemies = new();
        HashSet<DungeonTileView> uniqueTiles = new();

        void AddAreaTile(DungeonTileView tile)
        {
            if (tile == null || !uniqueTiles.Add(tile))
                return;

            tile.ShowAttackRange();
            EnemyRuntime enemy = GetEnemyAtTile(tile);
            if (enemy != null && uniqueEnemies.Add(enemy))
                result.Add(enemy);
        }

        foreach (EnemyRuntime centerTarget in centerTargets)
        {
            if (!TryFindEnemyTile(centerTarget, out DungeonTileView centerTile))
                continue;

            if (includeCenterTargets)
                AddAreaTile(centerTile);
            if (areaOffsets == null)
                continue;

            foreach (CharacterTargetAreaOffset offset in areaOffsets)
            {
                if (offset == null || offset.IsCenter ||
                    !TryGetTile(
                        centerTile.Row + offset.RowOffset,
                        centerTile.Column + offset.ColumnOffset,
                        out DungeonTileView areaTile))
                {
                    continue;
                }

                AddAreaTile(areaTile);
            }
        }

        return result;
    }

    private static bool MatchesCharacterConditions(
        IBattleCharacter source,
        DungeonTileView tile,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;
        if (tile?.TopEnemy == null)
            return false;

        bool matchAny = matchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            if (condition.Target == CharacterConditionTarget.Source)
            {
                bool sourceMatched =
                    CharacterConditionEvaluator.MatchesCharacter(
                        condition,
                        source);
                if (matchAny && sourceMatched)
                    return true;
                if (!matchAny && !sourceMatched)
                    return false;
                continue;
            }

            if (condition.Metric ==
                CharacterNumericConditionMetric.StatusStackCount)
            {
                bool statusMatched =
                    CharacterConditionEvaluator.MatchesStatusCondition(
                        condition,
                        tile.TopEnemy.GetStatusStackCount,
                        tile.TopEnemy.GetActiveStatusEffects());
                if (matchAny && statusMatched)
                    return true;
                if (!matchAny && !statusMatched)
                    return false;
                continue;
            }

            float value = condition.Metric switch
            {
                CharacterNumericConditionMetric.Health =>
                    tile.TopEnemy.Health,
                CharacterNumericConditionMetric.HealthPercentage =>
                    tile.TopEnemy.MaxHealth > 0
                        ? tile.TopEnemy.Health * 100f /
                          tile.TopEnemy.MaxHealth
                        : 0f,
                CharacterNumericConditionMetric.StackCount =>
                    tile.StackCount,
                CharacterNumericConditionMetric.Shield =>
                    tile.TopEnemy.CurrentShield,
                _ => 0f
            };
            bool matched = CharacterConditionEvaluator.Compare(
                value,
                condition.Comparison,
                condition.Threshold);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool MatchesCharacterConditions(
        IBattleCharacter source,
        IBattleCharacter character,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;
        if (character == null)
            return false;

        bool matchAny = matchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            IBattleCharacter evaluatedCharacter =
                condition.Target == CharacterConditionTarget.Source
                    ? source
                    : character;
            bool matched =
                CharacterConditionEvaluator.MatchesCharacter(
                    condition,
                    evaluatedCharacter);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static float GetCharacterMetric(
        IBattleCharacter character,
        CharacterAttackSubjectMetric metric)
    {
        if (character == null)
            return 0f;

        return metric switch
        {
            CharacterAttackSubjectMetric.Health =>
                character.CurrentHealth,
            CharacterAttackSubjectMetric.Shield =>
                character.CurrentShield,
            CharacterAttackSubjectMetric.AttackSpeed =>
                character.CurrentAttackSpeed,
            _ => character.CurrentAttackPower,
        };
    }

    private static bool CompareCharacterCondition(
        float value,
        CharacterNumericComparison comparison,
        float threshold)
    {
        return comparison switch
        {
            CharacterNumericComparison.GreaterThanOrEqual =>
                value >= threshold,
            CharacterNumericComparison.LessThanOrEqual => value <= threshold,
            CharacterNumericComparison.GreaterThan => value > threshold,
            CharacterNumericComparison.LessThan => value < threshold,
            CharacterNumericComparison.Equal =>
                Mathf.Approximately(value, threshold),
            CharacterNumericComparison.NotEqual =>
                !Mathf.Approximately(value, threshold),
            _ => true
        };
    }

    public int TryDamageCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int damage,
        CharacterAttackDamageType damageType,
        bool showAttackRange)
    {
        if (targets == null || damage <= 0 ||
            damageType == CharacterAttackDamageType.StatusEffect ||
            damageType == CharacterAttackDamageType.StatusRemoval)
        {
            return 0;
        }

        int totalDamage = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonTileView tile))
            {
                continue;
            }

            if (showAttackRange)
                tile.ShowAttackRange();
            totalDamage += TryDamageTile(
                tile,
                damage,
                damageType,
                source);
        }

        return totalDamage;
    }

    public int TryHealCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalHealed = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonTileView tile) ||
                !ReferenceEquals(tile.TopEnemy, enemy))
            {
                continue;
            }

            if (showAttackRange)
                tile.ShowAttackRange();
            totalHealed += tile.TryHealTop(amount);
        }

        return totalHealed;
    }

    public int TryHealAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalHealed = 0;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target != null && uniqueTargets.Add(target))
                totalHealed += target.Heal(amount);
        }

        return totalHealed;
    }

    public int TryGrantShieldToCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalGranted = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonTileView tile) ||
                !ReferenceEquals(tile.TopEnemy, enemy))
            {
                continue;
            }

            if (showAttackRange)
                tile.ShowAttackRange();
            totalGranted += tile.TryGrantShieldTop(amount);
        }

        return totalGranted;
    }

    public int TryGrantShieldToAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalGranted = 0;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target != null && uniqueTargets.Add(target))
                totalGranted += target.GainShield(amount);
        }

        return totalGranted;
    }

    public bool TryApplyCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks,
        float tickInterval,
        bool showAttackRange)
    {
        if (targets == null || statusEffect == null ||
            !statusEffect.CanTargetEnemy || stacks <= 0f)
        {
            return false;
        }

        float effectiveDuration = statusEffect.DurationMode ==
            StatusEffectDurationMode.Permanent
                ? 0f
                : (duration > 0f ? duration : statusEffect.DefaultDuration);
        if (statusEffect.DurationMode == StatusEffectDurationMode.Timed &&
            effectiveDuration <= 0f)
        {
            return false;
        }

        int stackCount = Mathf.Max(
            1,
            Mathf.RoundToInt(stacks));

        bool applied = false;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonTileView tile))
            {
                continue;
            }

            int previousStacks = enemy.GetStatusStackCount(statusEffect);
            bool targetApplied = tile.TryApplyStatusToTop(
                statusEffect,
                effectiveDuration,
                stackCount,
                source,
                tickInterval,
                TryDamageTile);
            if (targetApplied)
            {
                NotifyStatusApplied(new BattleStatusAppliedEvent(
                    BattleStatusTarget.FromEnemy(enemy),
                    statusEffect,
                    previousStacks,
                    enemy.GetStatusStackCount(statusEffect),
                    source));
            }
            if (targetApplied && showAttackRange &&
                ReferenceEquals(tile.TopEnemy, enemy))
                tile.ShowAttackRange();
            applied |= targetApplied;
        }

        return applied;
    }

    public bool TryApplyAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks)
    {
        if (targets == null || statusEffect == null ||
            !statusEffect.CanTargetAlly || stacks <= 0f)
        {
            return false;
        }

        int stackCount = Mathf.Max(1, Mathf.RoundToInt(stacks));
        bool applied = false;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            applied |= target.ApplyStatusEffect(
                statusEffect,
                duration,
                stackCount,
                source);
        }

        return applied;
    }

    public bool TryRemoveCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        bool showAttackRange)
    {
        if (targets == null)
            return false;

        bool removedAny = false;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonTileView tile))
            {
                continue;
            }

            int removed = tile.TryRemoveStatusFromTop(
                removalSelection,
                removalAmount,
                TryDamageTile);
            if (removed <= 0)
                continue;

            if (showAttackRange &&
                ReferenceEquals(tile.TopEnemy, enemy))
                tile.ShowAttackRange();
            removedAny = true;
        }

        return removedAny;
    }

    public bool TryRemoveAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount)
    {
        if (targets == null)
            return false;

        bool removedAny = false;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            removedAny |= target.RemoveStatusEffects(
                removalSelection,
                removalAmount) > 0;
        }

        return removedAny;
    }

    private bool TryApplyFireStatus(
        DungeonTileView tile,
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        tile = ResolveAnchorTile(tile);
        if (tile == null)
            return false;

        EnemyRuntime enemy = tile.TopEnemy;
        StatusEffectSO fire =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        int previousStacks = enemy?.GetStatusStackCount(fire) ?? 0;
        bool applied = tile.TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage,
            TryDamageTile);
        if (applied && enemy != null)
        {
            NotifyStatusApplied(new BattleStatusAppliedEvent(
                BattleStatusTarget.FromEnemy(enemy),
                fire,
                previousStacks,
                enemy.GetStatusStackCount(fire),
                source));
        }
        return applied;
    }

    public void TickStatusEffects(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        TickForcedPriorityTarget(deltaTime);
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
                tile.TickStatusEffects(deltaTime, TryDamageTile);
        }
    }

    public void TickEnemyAbilities(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (deltaTime <= 0f)
            return;

        foreach (DungeonTileView tile in _tiles)
        {
            EnemyRuntime enemy = tile != null ? tile.TopEnemy : null;
            if (enemy == null)
                continue;

            TickModularEnemyAbilities(
                tile,
                enemy,
                deltaTime,
                characters);
        }
    }

    private void TickModularEnemyAbilities(
        DungeonTileView sourceTile,
        EnemyRuntime source,
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters)
    {
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            if (!state.TickCooldown(
                    deltaTime,
                    source.AreAllActionsDisabled))
            {
                continue;
            }

            BattleEffectResult result = ExecuteCooldownAbility(
                sourceTile,
                source,
                state.Definition,
                characters);
            state.RecordActivation(result.Attempted, result.Succeeded);
        }
    }

    private BattleEffectResult ExecuteCooldownAbility(
        DungeonTileView sourceTile,
        EnemyRuntime source,
        EnemyAbilityDefinition ability,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (ability == null ||
            ability.Trigger != EnemyAbilityTrigger.OnCooldown ||
            !TryResolveEnemyAbilityTargets(
                sourceTile,
                source,
                ability.Target,
                characters,
                out CharacterTargetFaction targetFaction,
                out IReadOnlyList<EnemyRuntime> enemyTargets,
                out IReadOnlyList<IBattleCharacter> playerTargets) ||
            !MatchesEnemyAbilityConditions(
                ability,
                source,
                enemyTargets,
                playerTargets))
        {
            return default;
        }

        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                this,
                targetFaction,
                enemyTargets,
                playerTargets);
        return ExecuteEffectOperations(ability, context);
    }

    private void ExecuteSpawnAbilities(
        DungeonTileView sourceTile,
        EnemyRuntime source)
    {
        if (source == null)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                ability.Trigger != EnemyAbilityTrigger.OnSpawn ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ability.Target,
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteEffectOperations(ability, context);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled ||
                    operation.Type !=
                        EnemyAbilityOperationType.GrantArmor)
                {
                    continue;
                }

                int armor = ResolveGrantedArmor(source, operation);
                int granted = source.GainArmor(armor);
                combined = combined.Combine(
                    new BattleEffectResult(true, granted > 0));
            }

            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private int ExecuteBeforeSelfDamageAbilities(
        DungeonTileView sourceTile,
        EnemyRuntime source,
        int damage,
        CharacterAttackDamageType damageType)
    {
        if (source == null || source.AreAllActionsDisabled)
        {
            return damage;
        }

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                ability.Trigger !=
                    EnemyAbilityTrigger.BeforeSelfDamage ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ability.Target,
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets,
                    damageType))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteEffectOperations(ability, context);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled ||
                    operation.Type !=
                        EnemyAbilityOperationType.ModifyIncomingDamage)
                {
                    continue;
                }

                damage = Mathf.Max(0, operation.Amount);
                combined = combined.Combine(
                    new BattleEffectResult(true, true));
            }

            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }

        return damage;
    }

    private DungeonTileView FindModularDamageRedirect(
        DungeonTileView targetTile,
        CharacterAttackDamageType damageType)
    {
        EnemyRuntime target = targetTile?.TopEnemy;
        if (target == null)
            return null;

        foreach (DungeonTileView sourceTile in _tiles)
        {
            EnemyRuntime source = sourceTile?.TopEnemy;
            if (source == null || ReferenceEquals(source, target) ||
                source.AreAllActionsDisabled)
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!state.CanActivate ||
                    ability.Trigger !=
                        EnemyAbilityTrigger.BeforeAllyDamage)
                {
                    continue;
                }

                IReadOnlyList<EnemyRuntime> enemyTargets =
                    new[] { target };
                if (!MatchesEnemyAbilityConditions(
                        ability,
                        source,
                        enemyTargets,
                        Array.Empty<IBattleCharacter>(),
                        damageType))
                {
                    continue;
                }

                EnemyAbilityOperationDefinition redirect = null;
                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType.RedirectDamage &&
                        IsWithinAbilityRange(
                            sourceTile,
                            targetTile,
                            operation.Range,
                            operation.IncludeDiagonals))
                    {
                        redirect = operation;
                        break;
                    }
                }
                if (redirect == null)
                    continue;

                BattleEffectContext context =
                    BattleEffectContext.ForEnemyAbility(
                        source,
                        this,
                        CharacterTargetFaction.Enemy,
                        enemyTargets,
                        null);
                BattleEffectResult combined =
                    ExecuteEffectOperations(ability, context).Combine(
                        new BattleEffectResult(true, true));
                state.RecordActivation(
                    combined.Attempted,
                    combined.Succeeded);
                return sourceTile;
            }
        }

        return null;
    }

    private void ExecuteDeathAbilities(
        DungeonTileView sourceTile,
        EnemyRuntime source)
    {
        if (source == null)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                ability.Trigger != EnemyAbilityTrigger.OnDeath ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ability.Target,
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteEffectOperations(ability, context);
            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private static BattleEffectResult ExecuteEffectOperations(
        EnemyAbilityDefinition ability,
        BattleEffectContext context)
    {
        BattleEffectResult combined = default;
        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation == null || !operation.Enabled ||
                operation.Type !=
                    EnemyAbilityOperationType.ExecuteEffects)
            {
                continue;
            }

            combined = combined.Combine(
                BattleEffectExecutor.ExecuteSequence(
                    context,
                    operation.Effects));
        }

        return combined;
    }

    private static int ResolveGrantedArmor(
        EnemyRuntime source,
        EnemyAbilityOperationDefinition operation)
    {
        double amount = operation.Amount +
                        source.MaxHealth * (double)operation.Multiplier;
        if (double.IsNaN(amount) || amount <= 0d)
            return 0;
        if (double.IsInfinity(amount) || amount >= int.MaxValue)
            return int.MaxValue;
        return Mathf.Max(0, Mathf.RoundToInt((float)amount));
    }

    private bool IsWithinAbilityRange(
        DungeonTileView source,
        DungeonTileView target,
        int range,
        bool includeDiagonals)
    {
        if (source == null || target == null)
            return false;

        range = Mathf.Max(1, range);
        EnemyRuntime sourceEnemy = GetEnemyAtTile(source);
        EnemyRuntime targetEnemy = GetEnemyAtTile(target);
        IReadOnlyList<DungeonTileView> sourceTiles =
            GetOccupiedTiles(sourceEnemy, source);
        IReadOnlyList<DungeonTileView> targetTiles =
            GetOccupiedTiles(targetEnemy, target);

        foreach (DungeonTileView sourceTile in sourceTiles)
        {
            foreach (DungeonTileView targetTile in targetTiles)
            {
                int rowDistance = Mathf.Abs(
                    sourceTile.Row - targetTile.Row);
                int columnDistance = Mathf.Abs(
                    sourceTile.Column - targetTile.Column);
                if (includeDiagonals
                        ? Mathf.Max(rowDistance, columnDistance) <= range
                        : rowDistance + columnDistance <= range)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IReadOnlyList<DungeonTileView> GetOccupiedTiles(
        EnemyRuntime enemy,
        DungeonTileView fallback)
    {
        if (enemy != null &&
            _enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement))
        {
            return placement.OccupiedTiles;
        }

        return fallback != null
            ? new[] { fallback }
            : Array.Empty<DungeonTileView>();
    }

    private bool TryResolveEnemyAbilityTargets(
        DungeonTileView sourceTile,
        EnemyRuntime source,
        EnemyAbilityTargetDefinition target,
        IReadOnlyList<IBattleCharacter> characters,
        out CharacterTargetFaction targetFaction,
        out IReadOnlyList<EnemyRuntime> enemyTargets,
        out IReadOnlyList<IBattleCharacter> playerTargets)
    {
        targetFaction = CharacterTargetFaction.Enemy;
        enemyTargets = Array.Empty<EnemyRuntime>();
        playerTargets = Array.Empty<IBattleCharacter>();
        if (target == null || !target.HasTarget)
            return true;

        if (target.Faction == EnemyAbilityTargetFaction.Self ||
            (target.Faction ==
                 EnemyAbilityTargetFaction.EnemyAllies &&
             target.Subject == EnemyAbilityTargetSubject.Self))
        {
            enemyTargets = source != null
                ? new[] { source }
                : Array.Empty<EnemyRuntime>();
            return enemyTargets.Count > 0;
        }

        if (target.Faction ==
            EnemyAbilityTargetFaction.PlayerCharacters)
        {
            if (target.Subject == EnemyAbilityTargetSubject.Self ||
                target.Subject == EnemyAbilityTargetSubject.Adjacent)
            {
                return false;
            }

            targetFaction = CharacterTargetFaction.Ally;
            List<IBattleCharacter> candidates = new();
            if (characters != null)
            {
                foreach (IBattleCharacter character in characters)
                {
                    if (character != null &&
                        character.CurrentHealth > 0)
                    {
                        candidates.Add(character);
                    }
                }
            }

            SelectEnemyAbilityTargets(
                candidates,
                target.Subject,
                target.Metric,
                target.TargetCount,
                GetPlayerAbilityTargetMetric,
                GetPlayerTargetPriority);
            playerTargets = candidates;
            return candidates.Count > 0;
        }

        if (target.Faction !=
            EnemyAbilityTargetFaction.EnemyAllies)
        {
            return false;
        }

        List<EnemyRuntime> enemyCandidates =
            target.Subject == EnemyAbilityTargetSubject.Adjacent
                ? CollectAdjacentEnemyTargets(
                    sourceTile,
                    target.Range,
                    target.IncludeDiagonals)
                : CollectEnemyAbilityTargets(source);
        SelectEnemyAbilityTargets(
            enemyCandidates,
            target.Subject,
            target.Metric,
            target.TargetCount,
            GetEnemyAbilityTargetMetric,
            GetEnemyAllyTargetPriority);
        enemyTargets = enemyCandidates;
        return enemyCandidates.Count > 0;
    }

    private List<EnemyRuntime> CollectEnemyAbilityTargets(
        EnemyRuntime source)
    {
        List<EnemyRuntime> result = new();
        foreach (DungeonTileView tile in _tiles)
        {
            EnemyRuntime candidate = tile?.TopEnemy;
            if (candidate != null &&
                !ReferenceEquals(candidate, source) &&
                candidate.Health > 0)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private List<EnemyRuntime> CollectAdjacentEnemyTargets(
        DungeonTileView sourceTile,
        int range,
        bool includeDiagonals)
    {
        List<EnemyRuntime> result = new();
        if (sourceTile == null)
            return result;

        EnemyRuntime source = GetEnemyAtTile(sourceTile);
        HashSet<EnemyRuntime> unique = new();
        foreach (DungeonTileView candidateTile in _tiles)
        {
            EnemyRuntime candidate = candidateTile?.TopEnemy;
            if (candidate == null ||
                ReferenceEquals(candidate, source) ||
                candidate.Health <= 0 ||
                !unique.Add(candidate))
            {
                continue;
            }

            if (IsWithinAbilityRange(
                    sourceTile,
                    candidateTile,
                    range,
                    includeDiagonals))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static void SelectEnemyAbilityTargets<T>(
        List<T> candidates,
        EnemyAbilityTargetSubject subject,
        EnemyAbilityTargetMetric metric,
        int targetCount,
        Func<T, EnemyAbilityTargetMetric, float> getMetric,
        Func<T, StatusEffectTargetPriority> getPriority)
    {
        if (candidates == null || candidates.Count == 0 ||
            subject == EnemyAbilityTargetSubject.All ||
            subject == EnemyAbilityTargetSubject.Adjacent)
        {
            return;
        }

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        if (subject == EnemyAbilityTargetSubject.Random)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
            StableSortEnemyAbilityTargetsByPriority(
                candidates,
                getPriority);
        }
        else
        {
            bool descending =
                subject == EnemyAbilityTargetSubject.HighestValue;
            for (int index = 1; index < candidates.Count; index++)
            {
                T candidate = candidates[index];
                float candidateValue = getMetric(candidate, metric);
                StatusEffectTargetPriority candidatePriority =
                    getPriority(candidate);
                int insertionIndex = index - 1;
                while (insertionIndex >= 0)
                {
                    StatusEffectTargetPriority previousPriority =
                        getPriority(candidates[insertionIndex]);
                    int priorityComparison = CompareTargetPriority(
                        candidatePriority,
                        previousPriority);
                    float previousValue = getMetric(
                        candidates[insertionIndex],
                        metric);
                    bool shouldMove = priorityComparison < 0 ||
                                      (priorityComparison == 0 &&
                                       (descending
                                           ? previousValue < candidateValue
                                           : previousValue > candidateValue));
                    if (!shouldMove)
                        break;

                    candidates[insertionIndex + 1] =
                        candidates[insertionIndex];
                    insertionIndex--;
                }

                candidates[insertionIndex + 1] = candidate;
            }
        }

        if (candidates.Count > targetCount)
            candidates.RemoveRange(targetCount, candidates.Count - targetCount);
    }

    private static void StableSortEnemyAbilityTargetsByPriority<T>(
        List<T> candidates,
        Func<T, StatusEffectTargetPriority> getPriority)
    {
        for (int index = 1; index < candidates.Count; index++)
        {
            T candidate = candidates[index];
            StatusEffectTargetPriority candidatePriority =
                getPriority(candidate);
            int insertionIndex = index - 1;
            while (insertionIndex >= 0 &&
                   CompareTargetPriority(
                       candidatePriority,
                       getPriority(candidates[insertionIndex])) < 0)
            {
                candidates[insertionIndex + 1] =
                    candidates[insertionIndex];
                insertionIndex--;
            }

            candidates[insertionIndex + 1] = candidate;
        }
    }

    private static int CompareTargetPriority(
        StatusEffectTargetPriority left,
        StatusEffectTargetPriority right)
    {
        if (left.IsForced != right.IsForced)
            return left.IsForced ? -1 : 1;

        return right.Adjustment.CompareTo(left.Adjustment);
    }

    private static StatusEffectTargetPriority GetPlayerTargetPriority(
        IBattleCharacter target)
    {
        return StatusEffectTargetPriorityResolver.Resolve(
            target?.GetActiveStatusEffects());
    }

    private static StatusEffectTargetPriority GetEnemyAllyTargetPriority(
        EnemyRuntime target)
    {
        return StatusEffectTargetPriorityResolver.Resolve(
            target?.GetActiveStatusEffects());
    }

    private float GetEnemyAbilityTargetMetric(
        EnemyRuntime target,
        EnemyAbilityTargetMetric metric)
    {
        if (target == null)
            return 0f;

        return metric switch
        {
            EnemyAbilityTargetMetric.Health => target.Health,
            EnemyAbilityTargetMetric.HealthPercentage =>
                target.MaxHealth > 0
                    ? target.Health * 100f / target.MaxHealth
                    : 0f,
            EnemyAbilityTargetMetric.Shield => target.CurrentShield,
            EnemyAbilityTargetMetric.StackCount =>
                TryFindEnemyTile(target, out DungeonTileView tile)
                    ? tile.StackCount
                    : 0f,
            _ => 0f
        };
    }

    private static float GetPlayerAbilityTargetMetric(
        IBattleCharacter target,
        EnemyAbilityTargetMetric metric)
    {
        if (target == null)
            return 0f;

        return metric switch
        {
            EnemyAbilityTargetMetric.Health => target.CurrentHealth,
            EnemyAbilityTargetMetric.HealthPercentage =>
                target.MaximumHealth > 0
                    ? target.CurrentHealth * 100f / target.MaximumHealth
                    : 0f,
            EnemyAbilityTargetMetric.Shield => target.CurrentShield,
            EnemyAbilityTargetMetric.TotalDamageDealt =>
                target.TotalDamageDealt,
            EnemyAbilityTargetMetric.StackCount =>
                CountStatusStacks(target.GetActiveStatusEffects()),
            _ => 0f
        };
    }

    private static int CountStatusStacks(
        IReadOnlyList<BattleStatusSnapshot> statuses)
    {
        int total = 0;
        if (statuses == null)
            return total;

        foreach (BattleStatusSnapshot status in statuses)
            total += Mathf.Max(0, status.StackCount);
        return total;
    }

    private static bool MatchesEnemyAbilityConditions(
        EnemyAbilityDefinition ability,
        EnemyRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        CharacterAttackDamageType? incomingDamageType = null)
    {
        IReadOnlyList<EnemyAbilityConditionDefinition> conditions =
            ability.Conditions;
        if (conditions == null || conditions.Count == 0)
            return true;

        bool matchAny =
            ability.ConditionMatchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (EnemyAbilityConditionDefinition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            bool matched = MatchesEnemyAbilityCondition(
                condition,
                source,
                enemyTargets,
                playerTargets,
                incomingDamageType);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool MatchesEnemyAbilityCondition(
        EnemyAbilityConditionDefinition condition,
        EnemyRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        CharacterAttackDamageType? incomingDamageType)
    {
        switch (condition.Type)
        {
            case EnemyAbilityConditionType.SourceHealth:
                return CompareCharacterCondition(
                    source?.Health ?? 0,
                    condition.Comparison,
                    condition.Threshold);

            case EnemyAbilityConditionType.SourceHealthPercentage:
                return CompareCharacterCondition(
                    source != null && source.MaxHealth > 0
                        ? source.Health * 100f / source.MaxHealth
                        : 0f,
                    condition.Comparison,
                    condition.Threshold);

            case EnemyAbilityConditionType.SourceHasStatus:
            {
                bool hasStatus = source != null &&
                    EnemyAbilityConditionEvaluator.MatchesStatusSelection(
                        condition,
                        source.HasStatusEffect,
                        source.GetActiveStatusEffects());
                return hasStatus == condition.Expected;
            }

            case EnemyAbilityConditionType.TargetHealth:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    target => target.Health,
                    target => target.CurrentHealth,
                    condition);

            case EnemyAbilityConditionType.TargetHealthPercentage:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    target => target.MaxHealth > 0
                        ? target.Health * 100f / target.MaxHealth
                        : 0f,
                    target => target.MaximumHealth > 0
                        ? target.CurrentHealth * 100f /
                          target.MaximumHealth
                        : 0f,
                    condition);

            case EnemyAbilityConditionType.TargetTotalDamageDealt:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    _ => 0f,
                    target => target.TotalDamageDealt,
                    condition);

            case EnemyAbilityConditionType.TargetHasStatus:
            {
                bool hasStatus = false;
                foreach (EnemyRuntime target in enemyTargets)
                {
                    hasStatus |= target != null &&
                        EnemyAbilityConditionEvaluator
                            .MatchesStatusSelection(
                                condition,
                                target.HasStatusEffect,
                                target.GetActiveStatusEffects());
                }
                foreach (IBattleCharacter target in playerTargets)
                {
                    hasStatus |= target != null &&
                        EnemyAbilityConditionEvaluator
                            .MatchesStatusSelection(
                                condition,
                                target.HasStatusEffect,
                                target.GetActiveStatusEffects());
                }
                return hasStatus == condition.Expected;
            }

            case EnemyAbilityConditionType.IncomingDamageType:
            {
                bool matches =
                    incomingDamageType.HasValue &&
                    incomingDamageType.Value ==
                    condition.IncomingDamageType;
                return matches == condition.Expected;
            }

            case EnemyAbilityConditionType.HasAlternateTarget:
            {
                bool hasTarget =
                    (enemyTargets?.Count ?? 0) +
                    (playerTargets?.Count ?? 0) > 0;
                return hasTarget == condition.Expected;
            }

            default:
                return false;
        }
    }

    private static bool AnyTargetMatches(
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        Func<EnemyRuntime, float> enemyValue,
        Func<IBattleCharacter, float> playerValue,
        EnemyAbilityConditionDefinition condition)
    {
        foreach (EnemyRuntime target in enemyTargets)
        {
            if (target != null && CompareCharacterCondition(
                    enemyValue(target),
                    condition.Comparison,
                    condition.Threshold))
            {
                return true;
            }
        }
        foreach (IBattleCharacter target in playerTargets)
        {
            if (target != null && CompareCharacterCondition(
                    playerValue(target),
                    condition.Comparison,
                    condition.Threshold))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearAllStacks()
    {
        UnbindAllPresentationEnemies();
        foreach (DungeonTileView tile in _exclusiveOccupants.Keys)
        {
            if (tile != null)
                tile.SetExclusiveFootprintOccupant(null, false);
        }
        _exclusiveOccupants.Clear();
        _enemyPlacements.Clear();
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
                tile.ClearStack();
        }
        OccupancyChanged?.Invoke();
    }

    public void ClearAllEnemies()
    {
        CancelManualTargetSelection();
        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
        _statusEventQueue.Clear();
        _defeatEventQueue.Clear();
        _effectResolvedEventQueue.Clear();
        _statusLifecycleEventQueue.Clear();
        _unitLifecycleEventQueue.Clear();
        ClearAllStacks();
        _enemyVfxHandles.Clear();
        _lastEnemyVfxAnchors.Clear();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshLayout();
    }

    private void OnDestroy()
    {
        _presentationDispatcher?.Dispose();
        _presentationDispatcher = null;
        UnbindAllPresentationEnemies();
        UnbindAllPresentationCharacters();
    }

    private void Awake()
    {
        EnsurePresentationPipeline();
    }

    private void OnEnable()
    {
        EnsurePresentationPipeline();
    }

    private void OnDisable()
    {
        _presentationDispatcher?.Unbind();
        vfxPlayer?.ClearActive();
    }

    private void EnsurePresentationPipeline()
    {
        if (vfxPlayer == null)
            vfxPlayer = GetComponent<BattleVfxPlayer>();
        if (vfxPlayer == null)
            vfxPlayer = gameObject.AddComponent<BattleVfxPlayer>();

        if (vfxQualityProfile != null)
            vfxPlayer.ConfigureQuality(vfxQualityProfile);
        vfxPlayer.BindTargetResolver(this);
        _presentationDispatcher ??=
            new BattlePresentationDispatcher(vfxPlayer);
        _presentationDispatcher.Bind(this, this);
    }

    private void RefreshLayout()
    {
        if (!_initialized || boardRect == null || gridLayout == null)
            return;

        float boardSize = boardRect.rect.width;
        if (boardSize <= 0f)
            boardSize = boardRect.sizeDelta.x;
        if (boardSize <= 0f)
            return;

        int padding = Mathf.RoundToInt(boardSize * 0.045f);
        float spacing = Mathf.Max(4f, boardSize * 0.018f);
        float usableSize = boardSize - padding * 2f - spacing * (GridSize - 1);
        float cellSize = Mathf.Max(1f, usableSize / GridSize);

        gridLayout.padding = new RectOffset(padding, padding, padding, padding);
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.constraintCount = GridSize;

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
                tile.RefreshLayout(cellSize);
        }
    }

    private bool TryGetTile(int row, int column, out DungeonTileView tile)
    {
        tile = null;
        if (row < 0 || row >= GridSize || column < 0 || column >= GridSize)
            return false;

        int index = row * GridSize + column;
        if (index < 0 || index >= _tiles.Count)
            return false;

        tile = _tiles[index];
        return tile != null;
    }

    private int TryDamageTile(int row, int column, int damage)
    {
        return TryGetTile(row, column, out DungeonTileView tile)
            ? TryDamageTile(tile, damage)
            : 0;
    }

    private int TryDamageTile(DungeonTileView targetTile, int damage)
    {
        return TryDamageTile(
            targetTile,
            damage,
            CharacterAttackDamageType.Physical);
    }

    private int TryDamageTile(
        DungeonTileView targetTile,
        int damage,
        CharacterAttackDamageType damageType)
    {
        return TryDamageTile(targetTile, damage, damageType, null);
    }

    private int TryDamageTile(
        DungeonTileView targetTile,
        int damage,
        IBattleCharacter source)
    {
        return TryDamageTile(
            targetTile,
            damage,
            CharacterAttackDamageType.Physical,
            source);
    }

    private int TryDamageTile(
        DungeonTileView targetTile,
        int damage,
        CharacterAttackDamageType damageType,
        IBattleCharacter source)
    {
        targetTile = ResolveAnchorTile(targetTile);
        if (targetTile == null || targetTile.TopEnemy == null || damage <= 0)
            return 0;

        DungeonTileView redirectTile =
            FindModularDamageRedirect(targetTile, damageType);
        DungeonTileView damageReceiver = redirectTile != null
            ? redirectTile
            : targetTile;
        EnemyRuntime damagedEnemy = damageReceiver.TopEnemy;
        CaptureEnemyVfxAnchor(damagedEnemy, damageReceiver);
        damage = ExecuteBeforeSelfDamageAbilities(
            damageReceiver,
            damagedEnemy,
            damage,
            damageType);
        if (damage <= 0 || damagedEnemy.Health <= 0)
            return 0;

        int appliedDamage = damageReceiver.TryDamageTop(damage, damageType);
        if (appliedDamage > 0 && damagedEnemy.Health <= 0)
        {
            ReleasePlacement(damagedEnemy, false);
            ExecuteDeathAbilities(damageReceiver, damagedEnemy);
            NotifyEnemyDefeated(new BattleEnemyDefeatedEvent(
                damagedEnemy,
                source));
            SynchronizeEnemyPresentationBindings();
            OccupancyChanged?.Invoke();
        }

        return appliedDamage;
    }

    private List<DungeonTileView> CollectOccupiedTiles()
    {
        List<DungeonTileView> result = new();
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null && tile.StackCount > 0)
                result.Add(tile);
        }

        return result;
    }

    private readonly struct EnemyTargetPriorityEvaluation
    {
        public bool IsExcluded { get; }
        public bool IsForced { get; }
        public float Adjustment { get; }

        public EnemyTargetPriorityEvaluation(
            bool isExcluded,
            bool isForced,
            float adjustment)
        {
            IsExcluded = isExcluded;
            IsForced = isForced;
            Adjustment =
                float.IsNaN(adjustment) || float.IsInfinity(adjustment)
                    ? 0f
                    : adjustment;
        }
    }

    private List<DungeonTileView> CollectPriorityTargetTiles(
        out bool hasAlternateTarget)
    {
        List<DungeonTileView> occupiedTiles = CollectOccupiedTiles();
        hasAlternateTarget = occupiedTiles.Count > 1;
        List<DungeonTileView> priorityTargets = new();
        foreach (DungeonTileView tile in occupiedTiles)
        {
            EnemyRuntime enemy = tile.TopEnemy;
            if (enemy == null)
                continue;

            EnemyTargetPriorityEvaluation evaluation =
                EvaluateTargetPriority(enemy, hasAlternateTarget);
            bool itemForced =
                _forcedPriorityRemaining > 0f &&
                ReferenceEquals(enemy, _forcedPriorityTarget);
            if (itemForced || evaluation.IsForced ||
                !evaluation.IsExcluded)
            {
                priorityTargets.Add(tile);
            }
        }

        return priorityTargets.Count > 0
            ? priorityTargets
            : occupiedTiles;
    }

    private static EnemyTargetPriorityEvaluation EvaluateTargetPriority(
        EnemyRuntime source,
        bool hasAlternateTarget)
    {
        if (source == null)
            return default;

        StatusEffectTargetPriority statusPriority =
            StatusEffectTargetPriorityResolver.Resolve(
                source.GetActiveStatusEffects());
        bool excluded = false;
        bool forced = statusPriority.IsForced;
        double adjustment = statusPriority.Adjustment;
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                ability.Trigger !=
                    EnemyAbilityTrigger.OnTargetPriorityEvaluation ||
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
                if (operation != null && operation.Enabled &&
                    operation.Type ==
                        EnemyAbilityOperationType.ModifyTargetPriority)
                {
                    switch (operation.TargetPriorityMode)
                    {
                        case EnemyTargetPriorityMode.Exclude:
                            excluded = true;
                            break;
                        case EnemyTargetPriorityMode.Adjust:
                            adjustment +=
                                operation.TargetPriorityAdjustment;
                            break;
                        case EnemyTargetPriorityMode.ForceFocus:
                            forced = true;
                            break;
                    }
                }
            }
        }

        float clampedAdjustment = adjustment > float.MaxValue
            ? float.MaxValue
            : adjustment < -float.MaxValue
                ? -float.MaxValue
                : (float)adjustment;
        return new EnemyTargetPriorityEvaluation(
            excluded,
            forced,
            clampedAdjustment);
    }

    private static int CompareTargetPriority(
        DungeonTileView left,
        DungeonTileView right,
        bool hasAlternateTarget)
    {
        EnemyTargetPriorityEvaluation leftPriority =
            EvaluateTargetPriority(left?.TopEnemy, hasAlternateTarget);
        EnemyTargetPriorityEvaluation rightPriority =
            EvaluateTargetPriority(right?.TopEnemy, hasAlternateTarget);
        if (leftPriority.IsForced != rightPriority.IsForced)
            return leftPriority.IsForced ? -1 : 1;

        return rightPriority.Adjustment.CompareTo(
            leftPriority.Adjustment);
    }

    private static void StableSortByTargetPriority(
        List<DungeonTileView> candidates,
        bool hasAlternateTarget)
    {
        for (int index = 1; index < candidates.Count; index++)
        {
            DungeonTileView candidate = candidates[index];
            int insertionIndex = index - 1;
            while (insertionIndex >= 0 &&
                   CompareTargetPriority(
                       candidate,
                       candidates[insertionIndex],
                       hasAlternateTarget) < 0)
            {
                candidates[insertionIndex + 1] =
                    candidates[insertionIndex];
                insertionIndex--;
            }

            candidates[insertionIndex + 1] = candidate;
        }
    }

    private bool TryFindEnemyTile(
        EnemyRuntime enemy,
        out DungeonTileView targetTile)
    {
        targetTile = null;
        if (enemy == null)
            return false;

        if (_enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement) &&
            placement?.Anchor != null &&
            ReferenceEquals(placement.Anchor.TopEnemy, enemy))
        {
            targetTile = placement.Anchor;
            return true;
        }

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null && ReferenceEquals(tile.TopEnemy, enemy))
            {
                targetTile = tile;
                return true;
            }
        }

        return false;
    }

    private bool TryGetForcedPriorityTile(out DungeonTileView targetTile)
    {
        if (_forcedPriorityRemaining > 0f &&
            TryFindEnemyTile(_forcedPriorityTarget, out targetTile))
        {
            return true;
        }

        targetTile = null;
        return false;
    }

    private void TickForcedPriorityTarget(float deltaTime)
    {
        if (_forcedPriorityTarget == null)
            return;

        _forcedPriorityRemaining = Mathf.Max(
            0f,
            _forcedPriorityRemaining - Mathf.Max(0f, deltaTime));
        if (_forcedPriorityRemaining > 0f &&
            TryFindEnemyTile(_forcedPriorityTarget, out _))
        {
            return;
        }

        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
    }

    private List<EnemyRuntime>[,] CaptureExistingStacks()
    {
        if (_tiles.Count != GridSize * GridSize)
            return null;

        List<EnemyRuntime>[,] result =
            new List<EnemyRuntime>[GridSize, GridSize];
        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
                result[row, column] =
                    _tiles[row * GridSize + column].CopyEnemyRuntimes();
        }

        return result;
    }

    private void RestoreExistingStacks(
        List<EnemyRuntime>[,] previousEnemies,
        int previousSize)
    {
        if (previousEnemies == null)
            return;

        int preservedSize = Mathf.Min(previousSize, GridSize);
        for (int row = 0; row < preservedSize; row++)
        {
            for (int column = 0; column < preservedSize; column++)
            {
                foreach (EnemyRuntime enemy in previousEnemies[row, column])
                {
                    if (!TryBuildPlacementAt(
                            row,
                            column,
                            enemy,
                            null,
                            out PendingEnemyPlacement pending) ||
                        !pending.Anchor.TryAdd(enemy))
                    {
                        continue;
                    }

                    RegisterPlacement(pending);
                }
            }
        }
    }

    private void ClearTileObjects()
    {
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
            {
                UnbindTile(tile);
                Destroy(tile.gameObject);
            }
        }

        _tiles.Clear();
    }

    private void CollectSceneTiles(int gridSize)
    {
        _tiles.Clear();
        GridSize = Mathf.Clamp(gridSize, MinimumGridSize, MaximumGridSize);

        for (int index = 0; index < gridLayout.transform.childCount; index++)
        {
            Transform child = gridLayout.transform.GetChild(index);
            if (child.TryGetComponent(out DungeonTileView tile))
                _tiles.Add(tile);
        }

        if (_tiles.Count != GridSize * GridSize)
            return;

        for (int index = 0; index < _tiles.Count; index++)
        {
            int row = index / GridSize;
            int column = index % GridSize;
            _tiles[index].Initialize(row, column, _maximumStackSize);
            BindTile(_tiles[index]);
        }
    }

    private void BindTile(DungeonTileView tile)
    {
        if (tile == null)
            return;

        tile.EnemyClicked -= HandleEnemyClicked;
        tile.EnemyClicked += HandleEnemyClicked;
    }

    private void UnbindTile(DungeonTileView tile)
    {
        if (tile != null)
            tile.EnemyClicked -= HandleEnemyClicked;
    }

    private void BindPresentationEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_boundPresentationEnemies.Add(enemy))
            return;

        enemy.StatusLifecycle += HandleStatusLifecycle;
        GetOrCreateEnemyVfxHandle(enemy);
    }

    private void UnbindPresentationEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_boundPresentationEnemies.Remove(enemy))
            return;

        enemy.StatusLifecycle -= HandleStatusLifecycle;
    }

    private void UnbindAllPresentationEnemies()
    {
        foreach (EnemyRuntime enemy in _boundPresentationEnemies)
        {
            if (enemy != null)
                enemy.StatusLifecycle -= HandleStatusLifecycle;
        }

        _boundPresentationEnemies.Clear();
    }

    private void SynchronizeEnemyPresentationBindings()
    {
        HashSet<EnemyRuntime> currentEnemies = new();
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile == null)
                continue;

            foreach (EnemyRuntime enemy in tile.CopyEnemyRuntimes())
            {
                if (enemy != null)
                    currentEnemies.Add(enemy);
            }
        }

        List<EnemyRuntime> removedEnemies = new();
        foreach (EnemyRuntime enemy in _boundPresentationEnemies)
        {
            if (enemy == null || !currentEnemies.Contains(enemy))
                removedEnemies.Add(enemy);
        }
        foreach (EnemyRuntime enemy in removedEnemies)
            UnbindPresentationEnemy(enemy);
        foreach (EnemyRuntime enemy in currentEnemies)
            BindPresentationEnemy(enemy);
    }

    private void BindPresentationCharacter(CharacterRuntime character)
    {
        if (character == null ||
            !_boundPresentationCharacters.Add(character))
        {
            return;
        }

        character.StatusLifecycle += HandleStatusLifecycle;
        character.BindManualTargetHandler(HandleManualAllyClicked);
        GetOrCreateAllyVfxHandle(character);
    }

    private void UnbindAllPresentationCharacters()
    {
        foreach (CharacterRuntime character in _boundPresentationCharacters)
        {
            if (character != null)
            {
                character.StatusLifecycle -= HandleStatusLifecycle;
                character.BindManualTargetHandler(null);
                character.SetManualTargetSelectionState(false, false);
            }
        }

        _boundPresentationCharacters.Clear();
    }

    private void HandleStatusLifecycle(StatusEffectLifecycleEvent eventData)
    {
        PublishStatusLifecycle(eventData);
    }

    private BattleVfxTargetHandle GetOrCreateEnemyVfxHandle(
        EnemyRuntime enemy)
    {
        if (enemy == null)
            return default;
        if (_enemyVfxHandles.TryGetValue(enemy, out BattleVfxTargetHandle handle))
            return handle;

        handle = CreateVfxTargetHandle();
        _enemyVfxHandles.Add(enemy, handle);
        return handle;
    }

    private BattleVfxTargetHandle GetOrCreateAllyVfxHandle(
        IBattleCharacter ally)
    {
        if (ally == null)
            return default;
        if (_allyVfxHandles.TryGetValue(ally, out BattleVfxTargetHandle handle))
            return handle;

        handle = CreateVfxTargetHandle();
        _allyVfxHandles.Add(ally, handle);
        return handle;
    }

    private BattleVfxTargetHandle CreateVfxTargetHandle()
    {
        if (_nextVfxTargetHandle <= 0)
            _nextVfxTargetHandle = 1;

        BattleVfxTargetHandle handle =
            new(_nextVfxTargetHandle);
        _nextVfxTargetHandle = _nextVfxTargetHandle == int.MaxValue
            ? 1
            : _nextVfxTargetHandle + 1;
        return handle;
    }

    private void CaptureEnemyVfxAnchor(
        EnemyRuntime enemy,
        DungeonTileView tile)
    {
        if (enemy == null || tile == null)
            return;

        foreach (BattleVfxAnchorType anchorType in
                 Enum.GetValues(typeof(BattleVfxAnchorType)))
        {
            if (tile.TryGetEnemyVfxAnchor(
                    enemy,
                    anchorType,
                    out BattleVfxAnchorSnapshot snapshot))
            {
                StoreEnemyVfxAnchor(enemy, anchorType, snapshot);
            }
        }
    }

    private void StoreEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        BattleVfxAnchorSnapshot snapshot)
    {
        if (enemy == null || !snapshot.IsValid)
            return;

        if (!_lastEnemyVfxAnchors.TryGetValue(
                enemy,
                out Dictionary<
                    BattleVfxAnchorType,
                    BattleVfxAnchorSnapshot> anchors))
        {
            anchors = new Dictionary<
                BattleVfxAnchorType,
                BattleVfxAnchorSnapshot>();
            _lastEnemyVfxAnchors.Add(enemy, anchors);
        }

        anchors[anchorType] = snapshot;
    }

    private bool TryGetStoredEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        snapshot = default;
        return enemy != null &&
               _lastEnemyVfxAnchors.TryGetValue(
                   enemy,
                   out Dictionary<
                       BattleVfxAnchorType,
                       BattleVfxAnchorSnapshot> anchors) &&
               anchors.TryGetValue(anchorType, out snapshot) &&
               snapshot.IsValid;
    }

    private void HandleEnemyClicked(EnemyRuntime enemy)
    {
        if (enemy == null)
            return;

        if (HandleManualEnemyClicked(enemy))
            return;

        if (_itemTargetHandler != null && _itemTargetHandler(enemy))
            return;

        EnemyClicked?.Invoke(enemy);
    }
}
