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
    public const int CurrentVersion = 1;

    [SerializeField] private int version = CurrentVersion;
    [SerializeField] private List<InventoryEntrySaveData> entries =
        new();

    public int Version => version;
    public bool HasEntries => entries != null;
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
    private const string BackupPlayerPrefsKey =
        PlayerPrefsKey + ".backup";
    private const string CorruptPlayerPrefsKey =
        PlayerPrefsKey + ".corrupt";

    [NonSerialized] private readonly Dictionary<string, long> _amounts =
        new(StringComparer.Ordinal);

    [NonSerialized] private LocalDataLoadStatus _lastLoadStatus =
        LocalDataLoadStatus.NotLoaded;
    [NonSerialized] private bool _saveBlocked;

    public event Action<string, long> AmountChanged;

    public LocalDataLoadStatus LastLoadStatus => _lastLoadStatus;
    public bool IsSaveBlocked => _saveBlocked;

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
        if (_saveBlocked)
            return false;

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
        if (_saveBlocked)
        {
            Debug.LogWarning(
                "Inventory save was skipped because the primary save data " +
                "could not be loaded safely. Reset or recover local data " +
                "before saving again.");
            return;
        }

        string json = ExportJson();
        BackupCurrentValidSave(json);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        if (flush)
            PlayerPrefs.Save();
    }

    public LocalDataLoadStatus Load()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            InitializeNewAccount();
            SetLoadState(LocalDataLoadStatus.MissingInitialized, false);
            Save();
            return _lastLoadStatus;
        }

        string json =
            PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (TryDeserialize(
                json,
                out InventorySaveData saveData,
                out LocalDataLoadStatus failureStatus))
        {
            ApplySaveData(saveData);
            SetLoadState(LocalDataLoadStatus.Success, false);
            return _lastLoadStatus;
        }

        if (TryRecoverFromBackup(json))
            return _lastLoadStatus;

        InitializeNewAccount();
        SetLoadState(failureStatus, true);
        Debug.LogError(
            "Inventory save data is corrupt or uses an unsupported version. " +
            "The original PlayerPrefs value was preserved and inventory " +
            "saving is blocked until local data is reset or recovered.");
        return _lastLoadStatus;
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

    public bool ImportJson(string json)
    {
        if (!TryDeserialize(
                json,
                out InventorySaveData saveData,
                out _))
        {
            return false;
        }

        ApplySaveData(saveData);
        SetLoadState(LocalDataLoadStatus.Success, false);
        return true;
    }

    private void ApplySaveData(InventorySaveData saveData)
    {
        _amounts.Clear();

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

    private bool TryRecoverFromBackup(string corruptPrimaryJson)
    {
        if (!PlayerPrefs.HasKey(BackupPlayerPrefsKey))
            return false;

        string backupJson = PlayerPrefs.GetString(
            BackupPlayerPrefsKey,
            string.Empty);
        if (!TryDeserialize(
                backupJson,
                out InventorySaveData backup,
                out _))
        {
            return false;
        }

        PlayerPrefs.SetString(CorruptPlayerPrefsKey, corruptPrimaryJson);
        PlayerPrefs.Save();
        ApplySaveData(backup);
        SetLoadState(LocalDataLoadStatus.RecoveredFromBackup, false);
        Debug.LogWarning(
            "Inventory save data was restored from its last valid backup. " +
            "The rejected primary value was preserved under the corrupt key.");
        return true;
    }

    private void BackupCurrentValidSave(string replacementJson)
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            string currentJson = PlayerPrefs.GetString(
                PlayerPrefsKey,
                string.Empty);
            if (TryDeserialize(currentJson, out _, out _))
            {
                PlayerPrefs.SetString(BackupPlayerPrefsKey, currentJson);
                return;
            }

            PlayerPrefs.SetString(CorruptPlayerPrefsKey, currentJson);
        }

        if (!PlayerPrefs.HasKey(BackupPlayerPrefsKey))
            PlayerPrefs.SetString(BackupPlayerPrefsKey, replacementJson);
    }

    private static bool TryDeserialize(
        string json,
        out InventorySaveData saveData,
        out LocalDataLoadStatus failureStatus)
    {
        saveData = null;
        failureStatus = LocalDataLoadStatus.Corrupt;
        if (string.IsNullOrWhiteSpace(json) ||
            !LocalSaveJson.HasTopLevelProperty(json, "version") ||
            !LocalSaveJson.HasNonNullTopLevelProperty(json, "entries"))
        {
            return false;
        }

        try
        {
            saveData = JsonUtility.FromJson<InventorySaveData>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning(
                $"Failed to load inventory data: {exception.Message}");
            return false;
        }

        if (saveData == null || !saveData.HasEntries)
            return false;

        if (saveData.Version != InventorySaveData.CurrentVersion)
        {
            saveData = null;
            failureStatus = LocalDataLoadStatus.UnsupportedVersion;
            return false;
        }

        return true;
    }

    private void SetLoadState(
        LocalDataLoadStatus status,
        bool saveBlocked)
    {
        _lastLoadStatus = status;
        _saveBlocked = saveBlocked;
    }
}
