using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonBoardView : MonoBehaviour, IBattleBoard
{
    private const byte AreaEffectAlpha = 155;
    private const float AreaEffectSizeScale = 0.75f;
    private const float AreaEffectAnimationSpeed = 0.5f;
    private const string AreaExplosionFireStateName = "AreaExplosionFire";
    private const string AreaExplosionHiddenStateName = "AreaExplosionHidden";

    private sealed class AreaEffectHandle
    {
        public RectTransform RectTransform { get; }
        public Image Image { get; }
        public CanvasGroup CanvasGroup { get; }
        public Animator Animator { get; }

        public AreaEffectHandle(
            RectTransform rectTransform,
            Image image,
            CanvasGroup canvasGroup,
            Animator animator)
        {
            RectTransform = rectTransform;
            Image = image;
            CanvasGroup = canvasGroup;
            Animator = animator;
        }
    }

    private readonly struct PreparedTarget
    {
        public DungeonTileView Tile { get; }
        public EnemyRuntime Enemy { get; }

        public PreparedTarget(
            DungeonTileView tile,
            EnemyRuntime enemy)
        {
            Tile = tile;
            Enemy = enemy;
        }
    }

    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;

    [SerializeField] private RectTransform boardRect;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private DungeonTileView tilePrefab;

    private readonly List<DungeonTileView> _tiles = new();
    private readonly Dictionary<IBattleCharacter, PreparedTarget>
        _preparedLowestHealthTargets = new();
    private readonly Dictionary<IBattleCharacter, List<PreparedTarget>>
        _preparedRandomTargets = new();
    private readonly Dictionary<int, AreaEffectHandle> _areaEffects = new();
    private Func<EnemyRuntime, bool> _itemTargetHandler;
    private EnemyRuntime _forcedPriorityTarget;
    private float _forcedPriorityRemaining;
    private int _maximumStackSize = 8;
    private bool _initialized;

    public int GridSize { get; private set; } = MinimumGridSize;
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

    public void BindItemTargetHandler(
        Func<EnemyRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
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

        bool applied = tile.TryApplyFireToTop(
            null,
            duration,
            tickInterval,
            tickDamage);
        if (applied)
            tile.ShowAttackRange();
        return applied;
    }

    public bool TryForcePriorityTarget(EnemyRuntime enemy, float duration)
    {
        duration = TimePrecision.Normalize(duration, 0.1f);
        if (duration <= 0f || !TryFindEnemyTile(enemy, out _))
            return false;

        ClearAllPreparedAttacks();
        _forcedPriorityTarget = enemy;
        _forcedPriorityRemaining = duration;
        return true;
    }

    public bool TryPrepareLowestHealthAttack(
        IBattleCharacter source,
        out bool targetChanged)
    {
        targetChanged = false;
        if (source == null)
            return false;

        if (_preparedLowestHealthTargets.TryGetValue(
                source,
                out PreparedTarget currentTarget) &&
            currentTarget.Tile != null &&
            ReferenceEquals(currentTarget.Tile.TopEnemy, currentTarget.Enemy))
        {
            return true;
        }

        if (currentTarget.Tile != null)
            currentTarget.Tile.HideBasicTargetEffect(source);
        _preparedLowestHealthTargets.Remove(source);

        DungeonTileView target = FindLowestHealthTarget();
        if (target == null)
            return false;

        _preparedLowestHealthTargets[source] = new PreparedTarget(
            target,
            target.TopEnemy);
        target.PlayBasicTargetAim(source);
        targetChanged = true;
        return true;
    }

    public int TryResolveLowestHealthAttack(
        IBattleCharacter source,
        int damage)
    {
        if (source == null || damage <= 0 ||
            !_preparedLowestHealthTargets.TryGetValue(
                source,
                out PreparedTarget preparedTarget))
        {
            return 0;
        }

        _preparedLowestHealthTargets.Remove(source);
        DungeonTileView target = preparedTarget.Tile;
        if (target == null ||
            !ReferenceEquals(target.TopEnemy, preparedTarget.Enemy))
        {
            target?.HideBasicTargetEffect(source);
            return 0;
        }

        target.PlayBasicTargetFire(source);
        target.ShowAttackRange();
        return TryDamageTile(target, damage);
    }

    public int TryAttackLowestHealthEnemy(
        IBattleCharacter source,
        int damage)
    {
        if (source == null || damage <= 0)
            return 0;

        DungeonTileView target = FindLowestHealthTarget();
        if (target == null)
            return 0;

        target.PlayBasicTargetFire(source);
        target.ShowAttackRange();
        return TryDamageTile(target, damage);
    }

    public void ClearPreparedAttack(IBattleCharacter source)
    {
        if (source == null)
            return;

        if (_preparedLowestHealthTargets.TryGetValue(
                source,
                out PreparedTarget preparedTarget))
        {
            _preparedLowestHealthTargets.Remove(source);
            preparedTarget.Tile?.HideBasicTargetEffect(source);
        }

        if (_preparedRandomTargets.TryGetValue(
                source,
                out List<PreparedTarget> randomTargets))
        {
            _preparedRandomTargets.Remove(source);
            HidePreparedTargetEffects(source, randomTargets);
        }
    }

    private DungeonTileView FindLowestHealthTarget()
    {
        if (TryGetForcedPriorityTile(out DungeonTileView forcedTarget))
            return forcedTarget;

        DungeonTileView target = null;
        int lowestHealth = int.MaxValue;

        foreach (DungeonTileView tile in CollectPriorityTargetTiles())
        {
            if (tile.TopEnemyHealth >= lowestHealth)
                continue;

            target = tile;
            lowestHealth = tile.TopEnemyHealth;
        }

        return target;
    }

    public bool TryPrepareRandomAttack(
        IBattleCharacter source,
        int targetCount,
        out bool targetChanged)
    {
        targetChanged = false;
        if (source == null || targetCount <= 0)
            return false;

        List<DungeonTileView> targets = CollectPriorityTargetTiles();
        bool hasForcedTarget = TryGetForcedPriorityTile(
            out DungeonTileView forcedTarget);
        if (hasForcedTarget)
            targets.Remove(forcedTarget);
        int preparedCount = Mathf.Min(
            targetCount,
            targets.Count + (hasForcedTarget ? 1 : 0));
        if (_preparedRandomTargets.TryGetValue(
                source,
                out List<PreparedTarget> currentTargets) &&
            currentTargets.Count == preparedCount &&
            ArePreparedTargetsValid(currentTargets))
        {
            return preparedCount > 0;
        }

        if (currentTargets != null)
            HidePreparedTargetEffects(source, currentTargets);
        _preparedRandomTargets.Remove(source);

        if (preparedCount <= 0)
            return false;

        List<PreparedTarget> preparedTargets = new(preparedCount);
        int firstRandomIndex = 0;
        if (hasForcedTarget)
        {
            preparedTargets.Add(new PreparedTarget(
                forcedTarget,
                forcedTarget.TopEnemy));
            forcedTarget.PlayBasicTargetAim(source);
            firstRandomIndex = 1;
        }

        for (int index = firstRandomIndex; index < preparedCount; index++)
        {
            int poolIndex = index - firstRandomIndex;
            int randomIndex = Random.Range(poolIndex, targets.Count);
            (targets[poolIndex], targets[randomIndex]) =
                (targets[randomIndex], targets[poolIndex]);
            DungeonTileView target = targets[poolIndex];
            preparedTargets.Add(new PreparedTarget(target, target.TopEnemy));
            target.PlayBasicTargetAim(source);
        }

        _preparedRandomTargets[source] = preparedTargets;
        targetChanged = true;
        return true;
    }

    public int TryResolveRandomAttack(
        IBattleCharacter source,
        int damage)
    {
        if (source == null || damage <= 0 ||
            !_preparedRandomTargets.TryGetValue(
                source,
                out List<PreparedTarget> preparedTargets))
        {
            return 0;
        }

        _preparedRandomTargets.Remove(source);
        int totalDamage = 0;
        foreach (PreparedTarget preparedTarget in preparedTargets)
        {
            DungeonTileView target = preparedTarget.Tile;
            if (target == null ||
                !ReferenceEquals(target.TopEnemy, preparedTarget.Enemy))
            {
                target?.HideBasicTargetEffect(source);
                continue;
            }

            target.PlayBasicTargetFire(source);
            target.ShowAttackRange();
            totalDamage += TryDamageTile(target, damage);
        }

        return totalDamage;
    }

    private static bool ArePreparedTargetsValid(
        IReadOnlyList<PreparedTarget> targets)
    {
        foreach (PreparedTarget target in targets)
        {
            if (target.Tile == null ||
                !ReferenceEquals(target.Tile.TopEnemy, target.Enemy))
            {
                return false;
            }
        }

        return true;
    }

    private static void HidePreparedTargetEffects(
        IBattleCharacter source,
        IReadOnlyList<PreparedTarget> targets)
    {
        foreach (PreparedTarget target in targets)
            target.Tile?.HideBasicTargetEffect(source);
    }

    public int TryAttackCrossAroundHighestHealthEnemy(
        IBattleCharacter source,
        int damage)
    {
        if (source == null || damage <= 0)
            return 0;

        DungeonTileView center = TryGetForcedPriorityTile(
            out DungeonTileView forcedTarget)
            ? forcedTarget
            : null;
        int highestHealth = 0;
        List<DungeonTileView> automaticTargets = center == null
            ? CollectPriorityTargetTiles()
            : new List<DungeonTileView>();
        foreach (DungeonTileView tile in automaticTargets)
        {
            if (tile.TopEnemyHealth <= highestHealth)
            {
                continue;
            }

            center = tile;
            highestHealth = tile.TopEnemyHealth;
        }

        if (center == null)
            return 0;

        PlayAreaExplosion(source, center, 3);
        ShowAttackRangeTile(center.Row, center.Column);
        ShowAttackRangeTile(center.Row - 1, center.Column);
        ShowAttackRangeTile(center.Row + 1, center.Column);
        ShowAttackRangeTile(center.Row, center.Column - 1);
        ShowAttackRangeTile(center.Row, center.Column + 1);
        int totalDamage = TryDamageTile(center, damage);
        totalDamage += TryDamageTile(center.Row - 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row + 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row, center.Column - 1, damage);
        totalDamage += TryDamageTile(center.Row, center.Column + 1, damage);
        return totalDamage;
    }

    public int TryAttackCrossWithAdjacentSplash(
        IBattleCharacter source,
        int damage,
        int adjacentDamage)
    {
        if (source == null || damage <= 0 || adjacentDamage <= 0)
            return 0;

        DungeonTileView center = TryGetForcedPriorityTile(
            out DungeonTileView forcedTarget)
            ? forcedTarget
            : null;
        int highestHealth = 0;
        List<DungeonTileView> automaticTargets = center == null
            ? CollectPriorityTargetTiles()
            : new List<DungeonTileView>();
        foreach (DungeonTileView tile in automaticTargets)
        {
            if (tile.TopEnemyHealth <= highestHealth)
                continue;

            center = tile;
            highestHealth = tile.TopEnemyHealth;
        }

        if (center == null)
            return 0;

        PlayAreaExplosion(source, center, 5);
        ShowAttackRangeTile(center.Row, center.Column);
        ShowAttackRangeTile(center.Row - 1, center.Column);
        ShowAttackRangeTile(center.Row + 1, center.Column);
        ShowAttackRangeTile(center.Row, center.Column - 1);
        ShowAttackRangeTile(center.Row, center.Column + 1);
        ShowAttackRangeTile(center.Row - 2, center.Column);
        ShowAttackRangeTile(center.Row + 2, center.Column);
        ShowAttackRangeTile(center.Row, center.Column - 2);
        ShowAttackRangeTile(center.Row, center.Column + 2);
        ShowAttackRangeTile(center.Row - 1, center.Column - 1);
        ShowAttackRangeTile(center.Row - 1, center.Column + 1);
        ShowAttackRangeTile(center.Row + 1, center.Column - 1);
        ShowAttackRangeTile(center.Row + 1, center.Column + 1);
        int totalDamage = TryDamageTile(center, damage);
        totalDamage += TryDamageTile(center.Row - 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row + 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row, center.Column - 1, damage);
        totalDamage += TryDamageTile(center.Row, center.Column + 1, damage);
        totalDamage += TryDamageTile(
            center.Row - 2,
            center.Column,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row + 2,
            center.Column,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row,
            center.Column - 2,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row,
            center.Column + 2,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row - 1,
            center.Column - 1,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row - 1,
            center.Column + 1,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row + 1,
            center.Column - 1,
            adjacentDamage);
        totalDamage += TryDamageTile(
            center.Row + 1,
            center.Column + 1,
            adjacentDamage);
        return totalDamage;
    }

    public bool TryApplyFireToRandomEnemy(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        if (TryGetForcedPriorityTile(out DungeonTileView forcedTarget))
        {
            bool forcedApplied = forcedTarget.TryApplyFireToTop(
                source,
                duration,
                tickInterval,
                tickDamage);
            if (forcedApplied)
                forcedTarget.ShowAttackRange();
            return forcedApplied;
        }

        List<DungeonTileView> occupiedTiles = CollectPriorityTargetTiles();
        if (occupiedTiles.Count == 0)
            return false;

        List<DungeonTileView> targetsWithoutFire = new();
        foreach (DungeonTileView tile in occupiedTiles)
        {
            if (!tile.TopEnemyHasFire)
                targetsWithoutFire.Add(tile);
        }

        List<DungeonTileView> targetPool = targetsWithoutFire.Count > 0
            ? targetsWithoutFire
            : occupiedTiles;
        DungeonTileView target = targetPool[Random.Range(0, targetPool.Count)];
        bool applied = target.TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage);
        if (applied)
            target.ShowAttackRange();
        return applied;
    }

    public bool TryApplyFireAroundRandomEnemies(
        IBattleCharacter source,
        int centerTargetCount,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        List<DungeonTileView> occupiedTiles = CollectPriorityTargetTiles();
        if (occupiedTiles.Count == 0)
            return false;

        bool hasForcedTarget = TryGetForcedPriorityTile(
            out DungeonTileView forcedTarget);
        if (hasForcedTarget)
        {
            occupiedTiles.Remove(forcedTarget);
            occupiedTiles.Insert(0, forcedTarget);
        }
        int targetCount = Mathf.Clamp(
            centerTargetCount,
            1,
            occupiedTiles.Count);
        int firstRandomIndex = hasForcedTarget ? 1 : 0;

        for (int index = firstRandomIndex; index < targetCount; index++)
        {
            int swapIndex = Random.Range(index, occupiedTiles.Count);
            (occupiedTiles[index], occupiedTiles[swapIndex]) =
                (occupiedTiles[swapIndex], occupiedTiles[index]);
        }

        Dictionary<DungeonTileView, int> rangeHitCounts = new();
        for (int index = 0; index < targetCount; index++)
        {
            DungeonTileView center = occupiedTiles[index];
            for (int row = center.Row - 1; row <= center.Row + 1; row++)
            {
                for (int column = center.Column - 1;
                     column <= center.Column + 1;
                     column++)
                {
                    if (!TryGetTile(row, column, out DungeonTileView tile))
                        continue;

                    rangeHitCounts.TryGetValue(tile, out int hitCount);
                    rangeHitCounts[tile] = hitCount + 1;
                }
            }
        }

        bool applied = false;
        foreach ((DungeonTileView tile, int hitCount) in rangeHitCounts)
        {
            tile.ShowAttackRange(hitCount);
            if (tile.TopEnemy == null)
                continue;

            applied |= tile.TryApplyFireToTop(
                source,
                duration * hitCount,
                tickInterval,
                tickDamage);
        }

        return applied;
    }

    private void ShowAttackRangeTile(int row, int column)
    {
        if (TryGetTile(row, column, out DungeonTileView tile))
            tile.ShowAttackRange();
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
                EEnemyType.Mechanic => TryDisableHighestDamageCharacter(
                    characters,
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
        ClearAllPreparedAttacks();
        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
        HideAllAreaEffects();
        ClearAllStacks();
    }

    private void PlayAreaExplosion(
        IBattleCharacter source,
        DungeonTileView centerTile,
        int tileSpan)
    {
        if (source == null || centerTile == null ||
            source.PartySlotIndex < 0 ||
            source.TargetEffectSprite == null ||
            source.TargetEffectController == null || boardRect == null)
        {
            return;
        }

        AreaEffectHandle effect = GetOrCreateAreaEffect(source);
        if (effect == null)
            return;

        Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            boardRect,
            centerTile.transform as RectTransform);
        float effectSize = (gridLayout.cellSize.x * tileSpan +
                            gridLayout.spacing.x * (tileSpan - 1)) *
                           AreaEffectSizeScale;

        effect.RectTransform.anchoredPosition = targetBounds.center;
        effect.RectTransform.sizeDelta = new Vector2(effectSize, effectSize);
        effect.RectTransform.SetAsLastSibling();
        effect.Image.sprite = source.TargetEffectSprite;
        Color32 color = source.EffectColor;
        color.a = AreaEffectAlpha;
        effect.Image.color = color;
        effect.Image.enabled = true;
        effect.Animator.runtimeAnimatorController =
            source.TargetEffectController;
        effect.Animator.speed = AreaEffectAnimationSpeed;
        effect.Animator.Play(AreaExplosionFireStateName, 0, 0f);
    }

    private AreaEffectHandle GetOrCreateAreaEffect(IBattleCharacter source)
    {
        if (_areaEffects.TryGetValue(
                source.PartySlotIndex,
                out AreaEffectHandle effect) && effect?.RectTransform != null)
        {
            return effect;
        }

        GameObject effectObject = new(
            $"imgAreaExplosionEffect_S{source.PartySlotIndex + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(Animator));
        effectObject.layer = gameObject.layer;

        RectTransform rectTransform =
            effectObject.GetComponent<RectTransform>();
        rectTransform.SetParent(boardRect, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        Image image = effectObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        CanvasGroup canvasGroup = effectObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Animator animator = effectObject.GetComponent<Animator>();
        animator.runtimeAnimatorController = source.TargetEffectController;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = AreaEffectAnimationSpeed;

        effect = new AreaEffectHandle(
            rectTransform,
            image,
            canvasGroup,
            animator);
        _areaEffects[source.PartySlotIndex] = effect;
        return effect;
    }

    private void HideAllAreaEffects()
    {
        foreach (AreaEffectHandle effect in _areaEffects.Values)
        {
            if (effect?.Animator != null &&
                effect.Animator.runtimeAnimatorController != null &&
                effect.Animator.isActiveAndEnabled)
            {
                effect.Animator.Play(AreaExplosionHiddenStateName, 0, 0f);
            }
            else if (effect?.CanvasGroup != null)
            {
                effect.CanvasGroup.alpha = 0f;
            }
        }
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
        if (targetTile == null || targetTile.TopEnemy == null || damage <= 0)
            return 0;

        DungeonTileView shieldTile = FindProtectingShieldBearer(targetTile);
        DungeonTileView damageReceiver = shieldTile != null
            ? shieldTile
            : targetTile;
        EnemyRuntime damagedEnemy = damageReceiver.TopEnemy;
        int appliedDamage = damageReceiver.TryDamageTop(damage);
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

    private static bool TryDisableHighestDamageCharacter(
        IReadOnlyList<IBattleCharacter> characters,
        float duration)
    {
        if (characters == null || duration <= 0f)
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

        target.DisableFor(duration);
        return true;
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
        ClearAllPreparedAttacks();
    }

    private void ClearAllPreparedAttacks()
    {
        foreach (KeyValuePair<IBattleCharacter, PreparedTarget> prepared in
                 _preparedLowestHealthTargets)
        {
            prepared.Value.Tile?.HideBasicTargetEffect(prepared.Key);
        }
        _preparedLowestHealthTargets.Clear();

        foreach (KeyValuePair<IBattleCharacter, List<PreparedTarget>> prepared in
                 _preparedRandomTargets)
        {
            HidePreparedTargetEffects(prepared.Key, prepared.Value);
        }
        _preparedRandomTargets.Clear();
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
