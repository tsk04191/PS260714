using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonBoardView : MonoBehaviour, IBattleBoard
{
    private const int MaximumStatusEventsPerDispatch = 128;
    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;

    [SerializeField] private RectTransform boardRect;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private DungeonTileView tilePrefab;

    private readonly List<DungeonTileView> _tiles = new();
    private readonly List<IBattleCharacter> _battleCharacters = new();
    private Func<EnemyRuntime, bool> _itemTargetHandler;
    private EnemyRuntime _forcedPriorityTarget;
    private float _forcedPriorityRemaining;
    private int _maximumStackSize = 8;
    private bool _initialized;
    private readonly Queue<BattleStatusAppliedEvent> _statusEventQueue = new();
    private bool _dispatchingStatusEvents;

    public int GridSize { get; private set; } = MinimumGridSize;
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
                if (tile != null && tile.StackCount == 0)
                    return true;
            }

            return false;
        }
    }
    public event Action<EnemyRuntime> EnemyDefeated;
    public event Action<EnemyRuntime> EnemyClicked;
    public event Action<BattleStatusAppliedEvent> StatusApplied;

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

    public void BindItemTargetHandler(
        Func<EnemyRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
    }

    public void SetBattleCharacters(
        IReadOnlyList<IBattleCharacter> characters)
    {
        _battleCharacters.Clear();
        if (characters == null)
            return;

        foreach (IBattleCharacter character in characters)
        {
            if (character != null && !_battleCharacters.Contains(character))
                _battleCharacters.Add(character);
        }
    }

    public void Initialize(int gridSize, int stackSize)
    {
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

    public void SetPixelSize(float size)
    {
        if (boardRect == null)
            return;

        size = Mathf.Max(1f, size);
        boardRect.anchorMin = new Vector2(0.5f, 0.5f);
        boardRect.anchorMax = new Vector2(0.5f, 0.5f);
        boardRect.pivot = new Vector2(0.5f, 0.5f);
        boardRect.anchoredPosition = Vector2.zero;
        boardRect.sizeDelta = new Vector2(size, size);
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
        RefreshLayout();
    }

    public bool TryAddEnemyCard(
        int row,
        int column,
        EnemyRuntime enemy)
    {
        return enemy != null &&
               TryGetTile(row, column, out DungeonTileView tile) &&
               tile.TryAdd(enemy);
    }

    public bool TryAddEnemyCardToRandomTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<DungeonTileView> availableTiles = new();

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null && !tile.IsFull)
                availableTiles.Add(tile);
        }

        if (availableTiles.Count == 0)
            return false;

        int index = Random.Range(0, availableTiles.Count);
        return availableTiles[index].TryAdd(enemy);
    }

    public bool TryAddEnemyCardToNextAvailableTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<DungeonTileView> candidateTiles = new();
        int smallestStackCount = int.MaxValue;

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile == null || tile.IsFull)
                continue;

            if (tile.StackCount < smallestStackCount)
            {
                smallestStackCount = tile.StackCount;
                candidateTiles.Clear();
            }

            if (tile.StackCount == smallestStackCount)
                candidateTiles.Add(tile);
        }

        if (candidateTiles.Count == 0)
            return false;

        int randomIndex = Random.Range(0, candidateTiles.Count);
        return candidateTiles[randomIndex].TryAdd(enemy);
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

        foreach (EnemyRuntime enemy in enemies)
        {
            if (enemy == null)
                return false;
        }

        List<DungeonTileView> availableTiles = new();
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null && tile.CanAddEnemy)
                availableTiles.Add(tile);
        }

        if (availableTiles.Count < enemies.Count)
            return false;

        List<DungeonTileView> selectedTiles = new(enemies.Count);
        for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
        {
            int smallestStackCount = int.MaxValue;
            List<DungeonTileView> candidates = new();
            foreach (DungeonTileView tile in availableTiles)
            {
                if (tile.StackCount < smallestStackCount)
                {
                    smallestStackCount = tile.StackCount;
                    candidates.Clear();
                }

                if (tile.StackCount == smallestStackCount)
                    candidates.Add(tile);
            }

            DungeonTileView selected = candidates[
                Random.Range(0, candidates.Count)];
            selectedTiles.Add(selected);
            availableTiles.Remove(selected);
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            if (!selectedTiles[index].TryAdd(enemies[index]))
                return false;
        }

        return true;
    }

    public bool TryRemoveTopEnemyCard(int row, int column)
    {
        return TryGetTile(row, column, out DungeonTileView tile) && tile.TryRemoveTop();
    }

    public int GetStackCount(int row, int column)
    {
        return TryGetTile(row, column, out DungeonTileView tile) ? tile.StackCount : 0;
    }

    public int GetTopEnemyHealth(int row, int column)
    {
        return TryGetTile(row, column, out DungeonTileView tile) ? tile.TopEnemyHealth : 0;
    }

    public bool TrySetTopEnemyHealth(int row, int column, int health)
    {
        return TryGetTile(row, column, out DungeonTileView tile) &&
               tile.TrySetTopEnemyHealth(health);
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

        tile.ShowAttackRange();
        return TryDamageTile(tile, damage);
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
            subject == CharacterAttackSubject.None)
            subject = CharacterAttackSubject.Random;
        else if (subject == CharacterAttackSubject.AllExceptSelf)
            subject = CharacterAttackSubject.All;

        targetCount = Mathf.Max(1, targetCount);
        List<DungeonTileView> candidates = CollectPriorityTargetTiles();
        candidates.RemoveAll(tile => !MatchesCharacterConditions(
            tile,
            conditionMatchMode,
            numericConditions));
        if (candidates.Count == 0)
            return Array.Empty<EnemyRuntime>();

        List<DungeonTileView> selected = new(targetCount);
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
                 index < candidates.Count && selected.Count < targetCount;
                 index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
                selected.Add(candidates[index]);
            }
        }
        else
        {
            bool descending = subject == CharacterAttackSubject.HighestValue;
            candidates.Sort((left, right) =>
            {
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

        if (subject == CharacterAttackSubject.None)
            subject = CharacterAttackSubject.Random;

        List<IBattleCharacter> candidates = new();
        foreach (IBattleCharacter character in _battleCharacters)
        {
            if (character != null && MatchesCharacterConditions(
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

    public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
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
            if (tile.TopEnemy != null && uniqueEnemies.Add(tile.TopEnemy))
                result.Add(tile.TopEnemy);
        }

        foreach (EnemyRuntime centerTarget in centerTargets)
        {
            if (!TryFindEnemyTile(centerTarget, out DungeonTileView centerTile))
                continue;

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
            bool matched;
            if (condition.Type == CharacterConditionType.HasStatus)
            {
                matched = EnemyHasCharacterStatus(
                    tile.TopEnemy,
                    condition.StatusEffect);
            }
            else
            {
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
                matched = CompareCharacterCondition(
                    value,
                    condition.Comparison,
                    condition.Threshold);
            }
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool MatchesCharacterConditions(
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
            bool matched;
            if (condition.Type == CharacterConditionType.HasStatus)
            {
                matched = character.HasStatusEffect(
                    condition.StatusEffect);
            }
            else
            {
                float value = condition.Metric switch
                {
                    CharacterNumericConditionMetric.Health =>
                        character.CurrentHealth,
                    CharacterNumericConditionMetric.HealthPercentage =>
                        character.MaximumHealth > 0
                            ? character.CurrentHealth * 100f /
                              character.MaximumHealth
                            : 0f,
                    CharacterNumericConditionMetric.AttackPower =>
                        character.CurrentAttackPower,
                    CharacterNumericConditionMetric.AttackSpeed =>
                        character.CurrentAttackSpeed,
                    CharacterNumericConditionMetric.Shield =>
                        character.CurrentShield,
                    _ => 0f
                };
                matched = CompareCharacterCondition(
                    value,
                    condition.Comparison,
                    condition.Threshold);
            }
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool EnemyHasCharacterStatus(
        EnemyRuntime enemy,
        StatusEffectSO statusEffect)
    {
        return enemy != null && enemy.HasStatusEffect(statusEffect);
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
        if (source == null || targets == null || damage <= 0 ||
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
            totalDamage += TryDamageTile(tile, damage, damageType);
        }

        return totalDamage;
    }

    public int TryHealCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange)
    {
        if (source == null || targets == null || amount <= 0)
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
        if (source == null || targets == null || amount <= 0)
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
        if (source == null || targets == null || amount <= 0)
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
        if (source == null || targets == null || amount <= 0)
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
        if (source == null || targets == null || statusEffect == null ||
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
        if (source == null || targets == null || statusEffect == null ||
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
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        bool showAttackRange)
    {
        if (source == null || targets == null || removalCount < 0)
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
                removalTarget,
                statusEffect,
                removalCount,
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
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount)
    {
        if (source == null || targets == null || removalCount < 0)
            return false;

        bool removedAny = false;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            removedAny |= target.RemoveStatusEffects(
                removalTarget,
                statusEffect,
                removalCount) > 0;
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
            if (enemy == null || !enemy.TickAbilityCooldown(deltaTime))
                continue;

            bool activated = enemy.Type switch
            {
                EEnemyType.Medic => TryHealAdjacentEnemies(
                    tile,
                    enemy.Definition.AbilityPower),
                EEnemyType.Mechanic => TryApplyStatusToHighestDamageCharacter(
                    characters,
                    enemy.Definition.DisableStatusEffect,
                    enemy.Definition.DisableDuration),
                _ => false,
            };
            if (activated)
                enemy.ResetAbilityCooldown();
        }
    }

    public void ClearAllStacks()
    {
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
                tile.ClearStack();
        }
    }

    public void ClearAllEnemies()
    {
        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
        ClearAllStacks();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshLayout();
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
        if (targetTile == null || targetTile.TopEnemy == null || damage <= 0)
            return 0;

        DungeonTileView shieldTile = FindProtectingShieldBearer(targetTile);
        DungeonTileView damageReceiver = shieldTile != null
            ? shieldTile
            : targetTile;
        EnemyRuntime damagedEnemy = damageReceiver.TopEnemy;
        int appliedDamage = damageReceiver.TryDamageTop(damage, damageType);
        if (appliedDamage > 0 && damagedEnemy.Health <= 0)
            EnemyDefeated?.Invoke(damagedEnemy);

        return appliedDamage;
    }

    private DungeonTileView FindProtectingShieldBearer(
        DungeonTileView targetTile)
    {
        if (targetTile == null || targetTile.TopEnemy == null ||
            targetTile.TopEnemy.Type == EEnemyType.ShieldBearer)
        {
            return null;
        }

        for (int row = targetTile.Row - 1; row <= targetTile.Row + 1; row++)
        {
            for (int column = targetTile.Column - 1;
                 column <= targetTile.Column + 1;
                 column++)
            {
                if (!TryGetTile(row, column, out DungeonTileView candidate) ||
                    candidate == targetTile || candidate.TopEnemy == null)
                {
                    continue;
                }

                if (candidate.TopEnemy.Type == EEnemyType.ShieldBearer)
                    return candidate;
            }
        }

        return null;
    }

    private bool TryHealAdjacentEnemies(DungeonTileView medicTile, int amount)
    {
        if (medicTile == null || amount <= 0)
            return false;

        int healedAmount = 0;
        healedAmount += TryHealTile(medicTile.Row - 1, medicTile.Column, amount);
        healedAmount += TryHealTile(medicTile.Row + 1, medicTile.Column, amount);
        healedAmount += TryHealTile(medicTile.Row, medicTile.Column - 1, amount);
        healedAmount += TryHealTile(medicTile.Row, medicTile.Column + 1, amount);
        return healedAmount > 0;
    }

    private int TryHealTile(int row, int column, int amount)
    {
        return TryGetTile(row, column, out DungeonTileView tile)
            ? tile.TryHealTop(amount)
            : 0;
    }

    private static bool TryApplyStatusToHighestDamageCharacter(
        IReadOnlyList<IBattleCharacter> characters,
        StatusEffectSO statusEffect,
        float duration)
    {
        if (characters == null || statusEffect == null || duration <= 0f)
            return false;

        IBattleCharacter target = null;
        int highestDamage = 0;
        foreach (IBattleCharacter character in characters)
        {
            if (character == null || character.TotalDamageDealt <= highestDamage)
                continue;

            target = character;
            highestDamage = character.TotalDamageDealt;
        }

        if (target == null)
            return false;

        return target.ApplyStatusEffect(statusEffect, duration, 1);
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

    private List<DungeonTileView> CollectPriorityTargetTiles()
    {
        List<DungeonTileView> occupiedTiles = CollectOccupiedTiles();
        List<DungeonTileView> priorityTargets = new();
        foreach (DungeonTileView tile in occupiedTiles)
        {
            if (tile.TopEnemy != null &&
                !tile.TopEnemy.IsTargetPriorityExcluded)
            {
                priorityTargets.Add(tile);
            }
        }

        return priorityTargets.Count > 0
            ? priorityTargets
            : occupiedTiles;
    }

    private bool TryFindEnemyTile(
        EnemyRuntime enemy,
        out DungeonTileView targetTile)
    {
        targetTile = null;
        if (enemy == null)
            return false;

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
                DungeonTileView tile = _tiles[row * GridSize + column];
                foreach (EnemyRuntime enemy in previousEnemies[row, column])
                    tile.TryAdd(enemy);
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

    private void HandleEnemyClicked(EnemyRuntime enemy)
    {
        if (enemy == null)
            return;

        if (_itemTargetHandler != null && _itemTargetHandler(enemy))
            return;

        EnemyClicked?.Invoke(enemy);
    }
}
