using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonBoardView : MonoBehaviour, IBattleBoard
{
    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;

    [SerializeField] private RectTransform boardRect;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private DungeonTileView tilePrefab;

    private readonly List<DungeonTileView> _tiles = new();
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

    public int TryAttackLowestHealthEnemy(int damage)
    {
        if (damage <= 0)
            return 0;

        DungeonTileView target = null;
        int lowestHealth = int.MaxValue;

        foreach (DungeonTileView tile in CollectPriorityTargetTiles())
        {
            if (tile.TopEnemyHealth >= lowestHealth)
            {
                continue;
            }

            target = tile;
            lowestHealth = tile.TopEnemyHealth;
        }

        return target != null ? TryDamageTile(target, damage) : 0;
    }

    public int TryAttackRandomEnemies(int targetCount, int damage)
    {
        if (targetCount <= 0 || damage <= 0)
            return 0;

        List<DungeonTileView> targets = CollectPriorityTargetTiles();
        int attackCount = Mathf.Min(targetCount, targets.Count);
        int totalDamage = 0;

        for (int index = 0; index < attackCount; index++)
        {
            int randomIndex = Random.Range(index, targets.Count);
            (targets[index], targets[randomIndex]) =
                (targets[randomIndex], targets[index]);
            totalDamage += TryDamageTile(targets[index], damage);
        }

        return totalDamage;
    }

    public int TryAttackCrossAroundHighestHealthEnemy(int damage)
    {
        if (damage <= 0)
            return 0;

        DungeonTileView center = null;
        int highestHealth = 0;
        foreach (DungeonTileView tile in CollectPriorityTargetTiles())
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

        int totalDamage = TryDamageTile(center, damage);
        totalDamage += TryDamageTile(center.Row - 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row + 1, center.Column, damage);
        totalDamage += TryDamageTile(center.Row, center.Column - 1, damage);
        totalDamage += TryDamageTile(center.Row, center.Column + 1, damage);
        return totalDamage;
    }

    public int TryAttackCrossWithAdjacentSplash(
        int damage,
        int adjacentDamage)
    {
        if (damage <= 0 || adjacentDamage <= 0)
            return 0;

        DungeonTileView center = null;
        int highestHealth = 0;
        foreach (DungeonTileView tile in CollectPriorityTargetTiles())
        {
            if (tile.TopEnemyHealth <= highestHealth)
                continue;

            center = tile;
            highestHealth = tile.TopEnemyHealth;
        }

        if (center == null)
            return 0;

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
        return target.TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage);
    }

    public bool TryApplyFireAroundRandomEnemy(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        List<DungeonTileView> occupiedTiles = CollectPriorityTargetTiles();
        if (occupiedTiles.Count == 0)
            return false;

        DungeonTileView center = occupiedTiles[
            Random.Range(0, occupiedTiles.Count)];
        bool applied = false;
        for (int row = center.Row - 1; row <= center.Row + 1; row++)
        {
            for (int column = center.Column - 1;
                 column <= center.Column + 1;
                 column++)
            {
                if (!TryGetTile(row, column, out DungeonTileView tile) ||
                    tile.TopEnemy == null)
                {
                    continue;
                }

                applied |= tile.TryApplyFireToTop(
                    source,
                    duration,
                    tickInterval,
                    tickDamage);
            }
        }

        return applied;
    }

    public void TickStatusEffects(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

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
                Destroy(tile.gameObject);
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
        }
    }
}
