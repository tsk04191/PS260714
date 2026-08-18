using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
internal sealed class DungeonProgressEntrySaveData
{
    [SerializeField] private string dungeonId;
    [SerializeField] private bool cleared;
    [SerializeField, Min(0)] private int clearCount;
    [SerializeField, Min(1)] private int clearedContentVersion = 1;

    public string DungeonId => dungeonId ?? string.Empty;
    public bool Cleared => cleared;
    public int ClearCount => Mathf.Max(0, clearCount);
    public int ClearedContentVersion => Mathf.Max(1, clearedContentVersion);

    public DungeonProgressEntrySaveData(
        string id,
        int count,
        int contentVersion)
    {
        dungeonId = id ?? string.Empty;
        cleared = true;
        clearCount = Mathf.Max(1, count);
        clearedContentVersion = Mathf.Max(1, contentVersion);
    }

    public void RecordClear(int contentVersion)
    {
        cleared = true;
        if (clearCount < int.MaxValue)
            clearCount++;
        clearedContentVersion = Mathf.Max(
            ClearedContentVersion,
            contentVersion);
    }

    public void MergeFrom(DungeonProgressEntrySaveData other)
    {
        if (other == null)
            return;

        cleared |= other.Cleared;
        clearCount = Mathf.Max(ClearCount, other.ClearCount);
        clearedContentVersion = Mathf.Max(
            ClearedContentVersion,
            other.ClearedContentVersion);
    }

    public bool Normalize()
    {
        dungeonId = DungeonDefinitionCatalog.NormalizeDungeonId(dungeonId);
        clearCount = Mathf.Max(0, clearCount);
        clearedContentVersion = Mathf.Max(1, clearedContentVersion);
        return cleared && clearCount > 0 &&
               !string.IsNullOrWhiteSpace(dungeonId) &&
               !string.Equals(
                   dungeonId,
                   DungeonDefinitionCatalog.PracticeBattleId,
                   StringComparison.OrdinalIgnoreCase);
    }
}

[Serializable]
internal sealed class DungeonProgressSaveData
{
    public const int CurrentVersion = 1;

    [SerializeField] private int version = CurrentVersion;
    [SerializeField]
    private List<DungeonProgressEntrySaveData> entries = new();

    public int Version => version;
    public bool HasEntries => entries != null;
    public List<DungeonProgressEntrySaveData> Entries =>
        entries ??= new List<DungeonProgressEntrySaveData>();
}

[Serializable]
public sealed class DungeonProgressData
{
    internal const string PlayerPrefsKey = "Dungeons.Progress.v1";
    internal const string BackupPlayerPrefsKey = PlayerPrefsKey + ".backup";
    internal const string CorruptPlayerPrefsKey = PlayerPrefsKey + ".corrupt";

    [NonSerialized]
    private readonly Dictionary<string, DungeonProgressEntrySaveData>
        _entries = new(StringComparer.Ordinal);
    [NonSerialized] private LocalDataLoadStatus _lastLoadStatus =
        LocalDataLoadStatus.NotLoaded;
    [NonSerialized] private bool _saveBlocked;

    public event Action Changed;

    public LocalDataLoadStatus LastLoadStatus => _lastLoadStatus;
    public bool IsSaveBlocked => _saveBlocked;

    public bool IsCleared(DungeonDefinition definition)
    {
        return definition != null && definition.PersistsDungeonProgress &&
               IsCleared(definition.DungeonId);
    }

    public bool IsCleared(string dungeonId)
    {
        return TryGetEntry(dungeonId, out DungeonProgressEntrySaveData entry) &&
               entry.Cleared;
    }

    public int GetClearCount(string dungeonId)
    {
        return TryGetEntry(dungeonId, out DungeonProgressEntrySaveData entry)
            ? entry.ClearCount
            : 0;
    }

    public bool MarkCleared(
        DungeonDefinition definition,
        bool save = true)
    {
        return definition != null && definition.PersistsDungeonProgress &&
               MarkCleared(
            definition.DungeonId,
            definition.ContentVersion,
            save);
    }

    public bool MarkCleared(
        string dungeonId,
        int contentVersion = 1,
        bool save = true)
    {
        if (_saveBlocked)
            return false;

        string normalizedId =
            DungeonDefinitionCatalog.NormalizeDungeonId(dungeonId);
        if (string.IsNullOrEmpty(normalizedId) ||
            string.Equals(
                normalizedId,
                DungeonDefinitionCatalog.PracticeBattleId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_entries.TryGetValue(
                normalizedId,
                out DungeonProgressEntrySaveData entry))
        {
            entry.RecordClear(contentVersion);
        }
        else
        {
            _entries.Add(
                normalizedId,
                new DungeonProgressEntrySaveData(
                    normalizedId,
                    1,
                    contentVersion));
        }

        if (save)
            Save();
        Changed?.Invoke();
        return true;
    }

    public void Save(bool flush = true)
    {
        if (_saveBlocked)
        {
            Debug.LogWarning(
                "Dungeon progress save was skipped because the primary " +
                "save could not be loaded safely. Reset or recover local " +
                "data before saving again.");
            return;
        }

        string json = ExportJson();
        LocalSaveRecovery.BackupCurrentValidSave(
            PlayerPrefsKey,
            BackupPlayerPrefsKey,
            CorruptPlayerPrefsKey,
            json,
            IsValidSerializedSave);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        if (flush)
            PlayerPrefs.Save();
    }

    public LocalDataLoadStatus Load()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            _entries.Clear();
            SetLoadState(LocalDataLoadStatus.MissingInitialized, false);
            Save();
            return _lastLoadStatus;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (TryDeserialize(
                json,
                out DungeonProgressSaveData saveData,
                out LocalDataLoadStatus failureStatus))
        {
            ApplySaveData(saveData);
            SetLoadState(LocalDataLoadStatus.Success, false);
            return _lastLoadStatus;
        }

        if (TryRecoverFromBackup(json))
            return _lastLoadStatus;

        _entries.Clear();
        SetLoadState(failureStatus, true);
        Debug.LogError(
            "Dungeon progress is corrupt or uses an unsupported version. " +
            "The original value was preserved and saving is blocked until " +
            "local data is reset or recovered.");
        return _lastLoadStatus;
    }

    public string ExportJson()
    {
        DungeonProgressSaveData saveData = new();
        List<string> dungeonIds = new(_entries.Keys);
        dungeonIds.Sort(StringComparer.Ordinal);
        foreach (string dungeonId in dungeonIds)
        {
            DungeonProgressEntrySaveData entry = _entries[dungeonId];
            if (entry != null && entry.Normalize())
                saveData.Entries.Add(entry);
        }

        return JsonUtility.ToJson(saveData);
    }

    public bool ImportJson(string json)
    {
        if (!TryDeserialize(json, out DungeonProgressSaveData saveData, out _))
            return false;

        ApplySaveData(saveData);
        SetLoadState(LocalDataLoadStatus.Success, false);
        Changed?.Invoke();
        return true;
    }

    private bool TryGetEntry(
        string dungeonId,
        out DungeonProgressEntrySaveData entry)
    {
        entry = null;
        string normalizedId =
            DungeonDefinitionCatalog.NormalizeDungeonId(dungeonId);
        return !string.IsNullOrEmpty(normalizedId) &&
               _entries.TryGetValue(normalizedId, out entry);
    }

    private void ApplySaveData(DungeonProgressSaveData saveData)
    {
        _entries.Clear();
        if (saveData == null)
            return;

        foreach (DungeonProgressEntrySaveData entry in saveData.Entries)
        {
            if (entry == null || !entry.Normalize())
                continue;

            if (_entries.TryGetValue(
                    entry.DungeonId,
                    out DungeonProgressEntrySaveData existing))
            {
                existing.MergeFrom(entry);
                continue;
            }

            _entries.Add(entry.DungeonId, entry);
        }
    }

    private bool TryRecoverFromBackup(string corruptPrimaryJson)
    {
        if (!LocalSaveRecovery.TryRecover(
                BackupPlayerPrefsKey,
                CorruptPlayerPrefsKey,
                corruptPrimaryJson,
                TryDeserializeBackup,
                out DungeonProgressSaveData backup))
            return false;

        ApplySaveData(backup);
        SetLoadState(LocalDataLoadStatus.RecoveredFromBackup, false);
        Debug.LogWarning(
            "Dungeon progress was restored from its last valid backup. " +
            "The rejected primary value was preserved under the corrupt key.");
        return true;
    }

    private static bool IsValidSerializedSave(string json)
    {
        return TryDeserialize(json, out _, out _);
    }

    private static bool TryDeserializeBackup(
        string json,
        out DungeonProgressSaveData saveData)
    {
        return TryDeserialize(json, out saveData, out _);
    }

    private static bool TryDeserialize(
        string json,
        out DungeonProgressSaveData saveData,
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
            saveData = JsonUtility.FromJson<DungeonProgressSaveData>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning(
                $"Failed to load dungeon progress: {exception.Message}");
            return false;
        }

        if (saveData == null || !saveData.HasEntries)
            return false;
        if (saveData.Version != DungeonProgressSaveData.CurrentVersion)
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
