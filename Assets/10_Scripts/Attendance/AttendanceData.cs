using System;
using UnityEngine;

[Serializable]
internal sealed class AttendanceSaveData
{
    public const int CurrentVersion = 3;

    [SerializeField] private int version = CurrentVersion;
    [SerializeField] private string scheduleId = string.Empty;
    [SerializeField] private int claimedCount;
    [SerializeField] private int lastClaimedDayKey;
    [SerializeField] private int lastObservedDayKey;

    public int Version => version;
    public string ScheduleId => scheduleId?.Trim() ?? string.Empty;
    public int ClaimedCount => Math.Max(0, claimedCount);
    public int LastClaimedDayKey => Math.Max(0, lastClaimedDayKey);
    public int LastObservedDayKey => Math.Max(0, lastObservedDayKey);

    public AttendanceSaveData(
        string scheduleId,
        int claimedCount,
        int lastClaimedDayKey,
        int lastObservedDayKey)
    {
        this.scheduleId = scheduleId?.Trim() ?? string.Empty;
        this.claimedCount = Math.Max(0, claimedCount);
        this.lastClaimedDayKey = Math.Max(0, lastClaimedDayKey);
        this.lastObservedDayKey = Math.Max(0, lastObservedDayKey);
    }
}

[Serializable]
internal sealed class AttendanceSaveDataV2
{
    [SerializeField] private string scheduleId = string.Empty;
    [SerializeField] private int claimedDayMask;
    [SerializeField] private int extraDayClaimedMask;
    [SerializeField] private int lastClaimedDayKey;
    [SerializeField] private int lastObservedDayKey;

    public string ScheduleId => scheduleId?.Trim() ?? string.Empty;
    public int ClaimedCount =>
        CountBits(claimedDayMask & 0x0FFFFFFF) +
        CountBits(extraDayClaimedMask & 0x7);
    public int LastClaimedDayKey => Math.Max(0, lastClaimedDayKey);
    public int LastObservedDayKey => Math.Max(0, lastObservedDayKey);

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }
}

[Serializable]
public sealed class AttendanceData
{
    internal const string PlayerPrefsKey = "Attendance.Progress.v1";
    internal const string BackupPlayerPrefsKey =
        PlayerPrefsKey + ".backup";
    internal const string CorruptPlayerPrefsKey =
        PlayerPrefsKey + ".corrupt";

    [NonSerialized] private string _scheduleId = string.Empty;
    [NonSerialized] private int _claimedCount;
    [NonSerialized] private int _lastClaimedDayKey;
    [NonSerialized] private int _lastObservedDayKey;
    [NonSerialized] private LocalDataLoadStatus _lastLoadStatus =
        LocalDataLoadStatus.NotLoaded;
    [NonSerialized] private bool _saveBlocked;

    public event Action Changed;

    public string ScheduleId => _scheduleId ?? string.Empty;
    public int ClaimedCount => Math.Max(0, _claimedCount);
    public int LastClaimedDayKey => Math.Max(0, _lastClaimedDayKey);
    public int LastObservedDayKey => Math.Max(0, _lastObservedDayKey);
    public LocalDataLoadStatus LastLoadStatus => _lastLoadStatus;
    public bool IsSaveBlocked => _saveBlocked;

    public void Save(bool flush = true)
    {
        if (_saveBlocked)
        {
            Debug.LogWarning(
                "Attendance save was skipped because the primary save " +
                "data could not be loaded safely. Reset or recover local " +
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
            InitializeNewAccount();
            SetLoadState(LocalDataLoadStatus.MissingInitialized, false);
            Save();
            return _lastLoadStatus;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (TryDeserialize(
                json,
                out AttendanceSaveData saveData,
                out LocalDataLoadStatus loadStatus,
                out LocalDataLoadStatus failureStatus))
        {
            ApplySaveData(saveData);
            SetLoadState(loadStatus, false);
            return _lastLoadStatus;
        }

        if (TryRecoverFromBackup(json))
            return _lastLoadStatus;

        InitializeNewAccount();
        SetLoadState(failureStatus, true);
        Debug.LogError(
            "Attendance save data is corrupt or uses an unsupported " +
            "version. The original PlayerPrefs value was preserved and " +
            "attendance saving is blocked until local data is reset or " +
            "recovered.");
        return _lastLoadStatus;
    }

    public string ExportJson()
    {
        return JsonUtility.ToJson(new AttendanceSaveData(
            ScheduleId,
            ClaimedCount,
            LastClaimedDayKey,
            LastObservedDayKey));
    }

    public bool ImportJson(string json)
    {
        if (!TryDeserialize(
                json,
                out AttendanceSaveData saveData,
                out LocalDataLoadStatus loadStatus,
                out _))
            return false;

        ApplySaveData(saveData);
        SetLoadState(loadStatus, false);
        return true;
    }

    internal bool ApplySchedule(string progressId)
    {
        string normalized = progressId?.Trim() ?? string.Empty;
        if (string.Equals(
                ScheduleId,
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        _scheduleId = normalized;
        _claimedCount = 0;
        return true;
    }

    internal bool ObserveDay(int dayKey)
    {
        dayKey = Math.Max(0, dayKey);
        if (dayKey <= _lastObservedDayKey)
            return false;

        _lastObservedDayKey = dayKey;
        return true;
    }

    internal void CommitClaim(string progressId, int dayKey)
    {
        ApplySchedule(progressId);
        if (_claimedCount < int.MaxValue)
            _claimedCount++;
        _lastClaimedDayKey = Math.Max(0, dayKey);
        _lastObservedDayKey = Math.Max(_lastObservedDayKey, dayKey);
    }

    internal void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void InitializeNewAccount()
    {
        _scheduleId = string.Empty;
        _claimedCount = 0;
        _lastClaimedDayKey = 0;
        _lastObservedDayKey = 0;
    }

    private void ApplySaveData(AttendanceSaveData saveData)
    {
        _scheduleId = saveData.ScheduleId;
        _claimedCount = saveData.ClaimedCount;
        _lastClaimedDayKey = saveData.LastClaimedDayKey;
        _lastObservedDayKey = Math.Max(
            saveData.LastObservedDayKey,
            saveData.LastClaimedDayKey);
    }

    private bool TryRecoverFromBackup(string corruptPrimaryJson)
    {
        if (!LocalSaveRecovery.TryRecover(
                BackupPlayerPrefsKey,
                CorruptPlayerPrefsKey,
                corruptPrimaryJson,
                TryDeserializeBackup,
                out AttendanceSaveData backup))
        {
            return false;
        }

        ApplySaveData(backup);
        SetLoadState(LocalDataLoadStatus.RecoveredFromBackup, false);
        Debug.LogWarning(
            "Attendance save data was restored from its last valid " +
            "backup. The rejected primary value was preserved under the " +
            "corrupt key.");
        return true;
    }

    private static bool IsValidSerializedSave(string json)
    {
        return TryDeserialize(json, out _, out _, out _);
    }

    private static bool TryDeserializeBackup(
        string json,
        out AttendanceSaveData saveData)
    {
        return TryDeserialize(json, out saveData, out _, out _);
    }

    private static bool TryDeserialize(
        string json,
        out AttendanceSaveData saveData,
        out LocalDataLoadStatus loadStatus,
        out LocalDataLoadStatus failureStatus)
    {
        saveData = null;
        loadStatus = LocalDataLoadStatus.Success;
        failureStatus = LocalDataLoadStatus.Corrupt;
        if (string.IsNullOrWhiteSpace(json) ||
            !LocalSaveJson.HasTopLevelProperty(json, "version") ||
            !LocalSaveJson.HasNonNullTopLevelProperty(json, "scheduleId") ||
            !LocalSaveJson.HasTopLevelProperty(json, "claimedCount") ||
            !LocalSaveJson.HasTopLevelProperty(json, "lastClaimedDayKey") ||
            !LocalSaveJson.HasTopLevelProperty(json, "lastObservedDayKey"))
        {
            return false;
        }

        try
        {
            saveData = JsonUtility.FromJson<AttendanceSaveData>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning(
                $"Failed to load attendance data: {exception.Message}");
            return false;
        }

        if (saveData == null)
            return false;

        if (saveData.Version < 1 ||
            saveData.Version > AttendanceSaveData.CurrentVersion)
        {
            saveData = null;
            failureStatus = LocalDataLoadStatus.UnsupportedVersion;
            return false;
        }

        int serializedVersion = saveData.Version;

        if (saveData.Version == 2 &&
            (!LocalSaveJson.HasTopLevelProperty(json, "monthKey") ||
             !LocalSaveJson.HasTopLevelProperty(json, "claimedDayMask") ||
             !LocalSaveJson.HasTopLevelProperty(
                 json,
                 "extraDayClaimedMask")))
        {
            saveData = null;
            return false;
        }

        if (saveData.Version == 2)
        {
            AttendanceSaveDataV2 legacy;
            try
            {
                legacy = JsonUtility.FromJson<AttendanceSaveDataV2>(json);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning(
                    $"Failed to migrate attendance data: " +
                    exception.Message);
                saveData = null;
                return false;
            }

            if (legacy == null)
            {
                saveData = null;
                return false;
            }

            saveData = new AttendanceSaveData(
                legacy.ScheduleId,
                legacy.ClaimedCount,
                legacy.LastClaimedDayKey,
                legacy.LastObservedDayKey);
        }

        if (serializedVersion != AttendanceSaveData.CurrentVersion)
            loadStatus = LocalDataLoadStatus.Migrated;

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
