using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class InventoryEntrySaveData
{
    [SerializeField] private string itemId;
    [SerializeField] private long amount;

    public string ItemId => itemId ?? string.Empty;
    public long Amount => Math.Max(0L, amount);

    public InventoryEntrySaveData(string itemId, long amount)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Math.Max(0L, amount);
    }
}

[Serializable]
internal sealed class InventorySaveData
{
    [SerializeField] private int version = 1;
    [SerializeField] private List<InventoryEntrySaveData> entries =
        new();

    public int Version => version;
    public List<InventoryEntrySaveData> Entries =>
        entries ??= new List<InventoryEntrySaveData>();
}

public readonly struct InventoryDelta
{
    public string ItemId { get; }
    public long Amount { get; }

    public InventoryDelta(string itemId, long amount)
    {
        ItemId = itemId?.Trim() ?? string.Empty;
        Amount = amount;
    }
}

[Serializable]
public sealed class InventoryData
{
    private const string PlayerPrefsKey = "Inventory.Collection.v1";

    [NonSerialized] private readonly Dictionary<string, long> _amounts =
        new(StringComparer.Ordinal);

    public event Action<string, long> AmountChanged;

    public long GetAmount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0L;
        return _amounts.TryGetValue(itemId, out long amount)
            ? Math.Max(0L, amount)
            : 0L;
    }

    public long GetAmount(ItemDefinitionSO item)
    {
        return item != null ? GetAmount(item.ItemId) : 0L;
    }

    public bool CanSpend(string itemId, long amount)
    {
        return amount >= 0L && GetAmount(itemId) >= amount;
    }

    public bool TrySpend(
        string itemId,
        long amount,
        bool save = true)
    {
        if (amount < 0L)
            return false;
        return TryApply(
            new[] { new InventoryDelta(itemId, -amount) },
            save);
    }

    public bool Add(
        string itemId,
        long amount,
        bool save = true)
    {
        if (amount < 0L)
            return false;
        return TryApply(
            new[] { new InventoryDelta(itemId, amount) },
            save);
    }

    public bool TryApply(
        IReadOnlyList<InventoryDelta> deltas,
        bool save = true)
    {
        if (deltas == null || deltas.Count == 0)
            return true;

        Dictionary<string, decimal> aggregated =
            new(StringComparer.Ordinal);
        for (int index = 0; index < deltas.Count; index++)
        {
            InventoryDelta delta = deltas[index];
            if (string.IsNullOrWhiteSpace(delta.ItemId) ||
                ItemDefinitionCatalog.Get(delta.ItemId) == null)
            {
                return false;
            }

            aggregated.TryGetValue(
                delta.ItemId,
                out decimal currentDelta);
            aggregated[delta.ItemId] =
                currentDelta + delta.Amount;
        }

        Dictionary<string, long> nextAmounts =
            new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, decimal> pair in aggregated)
        {
            decimal candidate =
                GetAmount(pair.Key) + pair.Value;
            if (candidate < 0m || candidate > long.MaxValue)
                return false;

            long next = (long)candidate;
            ItemDefinitionSO definition =
                ItemDefinitionCatalog.Get(pair.Key);
            nextAmounts[pair.Key] =
                definition != null
                    ? definition.ClampAmount(next)
                    : next;
        }

        foreach (KeyValuePair<string, long> pair in nextAmounts)
        {
            long previous = GetAmount(pair.Key);
            if (previous == pair.Value)
                continue;

            if (pair.Value == 0L)
                _amounts.Remove(pair.Key);
            else
                _amounts[pair.Key] = pair.Value;
            AmountChanged?.Invoke(pair.Key, pair.Value);
        }

        if (save)
            Save();
        return true;
    }

    public void Save(bool flush = true)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, ExportJson());
        if (flush)
            PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            InitializeNewAccount();
            Save();
            return;
        }

        string json =
            PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        ImportJson(json);
    }

    private void InitializeNewAccount()
    {
        InitializeNewAccount(ItemDefinitionCatalog.GetAll());
    }

    private void InitializeNewAccount(
        IReadOnlyList<ItemDefinitionSO> definitions)
    {
        _amounts.Clear();
        if (definitions == null)
            return;

        for (int index = 0; index < definitions.Count; index++)
        {
            ItemDefinitionSO definition = definitions[index];
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId) ||
                definition.InitialAmount <= 0L ||
                _amounts.ContainsKey(definition.ItemId))
            {
                continue;
            }

            _amounts.Add(
                definition.ItemId,
                definition.InitialAmount);
        }
    }

    public string ExportJson()
    {
        InventorySaveData saveData = new();
        List<string> itemIds = new(_amounts.Keys);
        itemIds.Sort(StringComparer.Ordinal);
        foreach (string itemId in itemIds)
        {
            long amount = GetAmount(itemId);
            if (amount > 0L)
            {
                saveData.Entries.Add(
                    new InventoryEntrySaveData(itemId, amount));
            }
        }

        return JsonUtility.ToJson(saveData);
    }

    public void ImportJson(string json)
    {
        _amounts.Clear();
        if (string.IsNullOrWhiteSpace(json))
            return;

        InventorySaveData saveData;
        try
        {
            saveData = JsonUtility.FromJson<InventorySaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Failed to load inventory data: {exception.Message}");
            return;
        }

        if (saveData == null)
            return;

        foreach (InventoryEntrySaveData entry in saveData.Entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.ItemId) ||
                entry.Amount <= 0L)
            {
                continue;
            }

            _amounts.TryGetValue(
                entry.ItemId,
                out long previous);
            decimal merged = (decimal)previous + entry.Amount;
            long amount = merged >= long.MaxValue
                ? long.MaxValue
                : (long)merged;
            ItemDefinitionSO definition =
                ItemDefinitionCatalog.Get(entry.ItemId);
            _amounts[entry.ItemId] =
                definition != null
                    ? definition.ClampAmount(amount)
                    : amount;
        }
    }
}
