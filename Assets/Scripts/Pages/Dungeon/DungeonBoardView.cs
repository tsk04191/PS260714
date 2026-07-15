using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        List<DungeonEnemyData>[,] previousEnemies = CaptureExistingStacks();
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

    public bool TryAddEnemyCard(int row, int column, int health = 1)
    {
        return TryGetTile(row, column, out DungeonTileView tile) && tile.TryAdd(health);
    }

    public bool TryAddEnemyCardToRandomTile(int health = 1)
    {
        List<DungeonTileView> availableTiles = new();

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null && !tile.IsFull)
                availableTiles.Add(tile);
        }

        if (availableTiles.Count == 0)
            return false;

        int index = Random.Range(0, availableTiles.Count);
        return availableTiles[index].TryAdd(health);
    }

    public bool TryAddEnemyCardToNextAvailableTile(int health = 1)
    {
        return TryAddEnemyCardToNextAvailableTile(new DungeonEnemyData(health));
    }

    internal bool TryAddEnemyCardToNextAvailableTile(DungeonEnemyData enemy)
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

    public bool TryAddEnemy(DungeonEnemyData enemy)
    {
        return TryAddEnemyCardToNextAvailableTile(enemy);
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

    public bool TryAttackLowestHealthEnemy(int damage)
    {
        if (damage <= 0)
            return false;

        DungeonTileView target = null;
        int lowestHealth = int.MaxValue;

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile == null || tile.StackCount == 0 ||
                tile.TopEnemyHealth >= lowestHealth)
            {
                continue;
            }

            target = tile;
            lowestHealth = tile.TopEnemyHealth;
        }

        return target != null && target.TryDamageTop(damage);
    }

    public bool TryAttackRandomEnemies(int targetCount, int damage)
    {
        if (targetCount <= 0 || damage <= 0)
            return false;

        List<DungeonTileView> targets = CollectOccupiedTiles();
        int attackCount = Mathf.Min(targetCount, targets.Count);
        bool attacked = false;

        for (int index = 0; index < attackCount; index++)
        {
            int randomIndex = Random.Range(index, targets.Count);
            (targets[index], targets[randomIndex]) =
                (targets[randomIndex], targets[index]);
            attacked |= targets[index].TryDamageTop(damage);
        }

        return attacked;
    }

    public bool TryAttackCrossAroundHighestHealthEnemy(int damage)
    {
        if (damage <= 0)
            return false;

        DungeonTileView center = null;
        int highestHealth = 0;
        foreach (DungeonTileView tile in _tiles)
        {
            if (tile == null || tile.StackCount == 0 ||
                tile.TopEnemyHealth <= highestHealth)
            {
                continue;
            }

            center = tile;
            highestHealth = tile.TopEnemyHealth;
        }

        if (center == null)
            return false;

        bool attacked = center.TryDamageTop(damage);
        attacked |= TryDamageTile(center.Row - 1, center.Column, damage);
        attacked |= TryDamageTile(center.Row + 1, center.Column, damage);
        attacked |= TryDamageTile(center.Row, center.Column - 1, damage);
        attacked |= TryDamageTile(center.Row, center.Column + 1, damage);
        return attacked;
    }

    public bool TryApplyFireToRandomEnemy(
        float duration,
        float tickInterval,
        int tickDamage)
    {
        List<DungeonTileView> occupiedTiles = CollectOccupiedTiles();
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
        return target.TryApplyFireToTop(duration, tickInterval, tickDamage);
    }

    public void TickStatusEffects(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        foreach (DungeonTileView tile in _tiles)
        {
            if (tile != null)
                tile.TickStatusEffects(deltaTime);
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

    private bool TryDamageTile(int row, int column, int damage)
    {
        return TryGetTile(row, column, out DungeonTileView tile) &&
               tile.TryDamageTop(damage);
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

    private List<DungeonEnemyData>[,] CaptureExistingStacks()
    {
        if (_tiles.Count != GridSize * GridSize)
            return null;

        List<DungeonEnemyData>[,] result = new List<DungeonEnemyData>[GridSize, GridSize];
        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
                result[row, column] = _tiles[row * GridSize + column].CopyEnemyData();
        }

        return result;
    }

    private void RestoreExistingStacks(
        List<DungeonEnemyData>[,] previousEnemies,
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
                foreach (DungeonEnemyData enemy in previousEnemies[row, column])
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
