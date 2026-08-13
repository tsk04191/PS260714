using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DungeonStartingItemRule
{
    [SerializeField, Range(1, 5)] private int itemCount = 3;
    [SerializeField, Min(0)] private int rerollsPerSlot = 1;
    [SerializeField, Tooltip(
        "Optional dungeon-specific pool. When empty, every Battle Item " +
        "marked Available As Starting Item is used. Explicit entries must " +
        "also be marked Available As Starting Item.")]
    private List<BattleItemSO> itemPool = new();

    public int ItemCount => Mathf.Clamp(itemCount, 1, 5);
    public int RerollsPerSlot => Mathf.Max(0, rerollsPerSlot);
    public IReadOnlyList<BattleItemSO> ItemPool => itemPool;
    public int MinimumRequiredPoolSize => 1;

    public List<BattleItemSO> ResolveEligibleItems()
    {
        IReadOnlyList<BattleItemSO> source =
            itemPool != null && itemPool.Count > 0
                ? itemPool
                : BattleItemCatalog.GetAll();
        List<BattleItemSO> result = new();
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        if (source == null)
            return result;

        for (int index = 0; index < source.Count; index++)
        {
            BattleItemSO item = source[index];
            if (item == null || !item.AvailableAsStartingItem ||
                string.IsNullOrWhiteSpace(item.ItemId) ||
                !itemIds.Add(item.ItemId))
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    public bool TryValidate(out string error)
    {
        int availableCount = ResolveEligibleItems().Count;
        if (availableCount < MinimumRequiredPoolSize)
        {
            error = $"Starting item selection requires at least " +
                    $"{MinimumRequiredPoolSize} eligible item, " +
                    $"but only {availableCount} are available.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class DungeonStartingItemSelectionState
{
    private readonly List<BattleItemSO> _pool = new();
    private readonly List<BattleItemSO> _items = new();
    private readonly List<int> _rerollsRemaining = new();
    private System.Random _random;

    public IReadOnlyList<BattleItemSO> Items => _items;
    public bool IsPrepared { get; private set; }
    public bool IsConfirmed { get; private set; }

    public bool TryPrepare(
        IReadOnlyList<BattleItemSO> pool,
        int itemCount,
        int rerollsPerSlot,
        int randomSeed,
        out string error)
    {
        Clear();
        itemCount = Math.Max(1, itemCount);
        rerollsPerSlot = Math.Max(0, rerollsPerSlot);

        HashSet<string> itemIds = new(StringComparer.Ordinal);
        if (pool != null)
        {
            for (int index = 0; index < pool.Count; index++)
            {
                BattleItemSO item = pool[index];
                if (item == null || !item.AvailableAsStartingItem ||
                    string.IsNullOrWhiteSpace(item.ItemId) ||
                    !itemIds.Add(item.ItemId))
                {
                    continue;
                }

                _pool.Add(item);
            }
        }

        if (_pool.Count == 0)
        {
            error = "Starting item selection requires at least 1 " +
                    "eligible item but received 0.";
            Clear();
            return false;
        }

        _random = new System.Random(randomSeed);
        for (int index = 0; index < itemCount; index++)
        {
            _items.Add(_pool[_random.Next(_pool.Count)]);
            _rerollsRemaining.Add(rerollsPerSlot);
        }

        IsPrepared = true;
        error = string.Empty;
        return true;
    }

    public BattleItemSO GetItem(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _items.Count
            ? _items[slotIndex]
            : null;
    }

    public int GetRerollsRemaining(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _rerollsRemaining.Count
            ? _rerollsRemaining[slotIndex]
            : 0;
    }

    public bool CanReroll(int slotIndex)
    {
        return IsPrepared && !IsConfirmed &&
               GetRerollsRemaining(slotIndex) > 0 &&
               _pool.Count > 0;
    }

    public bool TryReroll(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _items.Count ||
            !CanReroll(slotIndex))
        {
            return false;
        }

        _items[slotIndex] = _pool[_random.Next(_pool.Count)];
        _rerollsRemaining[slotIndex]--;
        return true;
    }

    public bool TryConfirm()
    {
        if (!IsPrepared || IsConfirmed || _items.Count == 0)
            return false;

        IsConfirmed = true;
        return true;
    }

    public void Clear()
    {
        _pool.Clear();
        _items.Clear();
        _rerollsRemaining.Clear();
        _random = null;
        IsPrepared = false;
        IsConfirmed = false;
    }

}
