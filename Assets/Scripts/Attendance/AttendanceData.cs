using System;
using UnityEngine;

[Serializable]
internal sealed class AttendanceSaveData
{
    public const int CurrentVersion = 2;

    [SerializeField] private int version = CurrentVersion;
    [SerializeField] private string scheduleId = string.Empty;
    [SerializeField] private int claimedCount;
    [SerializeField] private int monthKey;
    [SerializeField] private int claimedDayMask;
    [SerializeField] private int extraDayClaimedMask;
    [SerializeField] private int lastClaimedDayKey;
    [SerializeField] private int lastObservedDayKey;

    public int Version => version;
    public string ScheduleId => scheduleId?.Trim() ?? string.Empty;
    public int ClaimedCount => Math.Max(0, claimedCount);
    public int MonthKey => Math.Max(0, monthKey);
    public int ClaimedDayMask => claimedDayMask & 0x0FFFFFFF;
    public int ExtraDayClaimedMask => extraDayClaimedMask & 0x7;
    public int LastClaimedDayKey => Math.Max(0, lastClaimedDayKey);
    public int LastObservedDayKey => Math.Max(0, lastObservedDayKey);

    public AttendanceSaveData(
        string scheduleId,
        int monthKey,
        int claimedDayMask,
        int extraDayClaimedMask,
        int lastClaimedDayKey,
        int lastObservedDayKey)
    {
        this.scheduleId = scheduleId?.Trim() ?? string.Empty;
        this.monthKey = Math.Max(0, monthKey);
        this.claimedDayMask = claimedDayMask & 0x0FFFFFFF;
        this.extraDayClaimedMask = extraDayClaimedMask & 0x7;
        claimedCount = CountBits(this.claimedDayMask) +
                       CountBits(this.extraDayClaimedMask);
        this.lastClaimedDayKey = Math.Max(0, lastClaimedDayKey);
        this.lastObservedDayKey = Math.Max(0, lastObservedDayKey);
    }

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
    [NonSerialized] private int _monthKey;
    [NonSerialized] private int _claimedDayMask;
    [NonSerialized] private int _extraDayClaimedMask;
    [NonSerialized] private int _lastClaimedDayKey;
    [NonSerialized] private int _lastObservedDayKey;
    [NonSerialized] private LocalDataLoadStatus _lastLoadStatus =
        LocalDataLoadStatus.NotLoaded;
    [NonSerialized] private bool _saveBlocked;

    public event Action Changed;

    public string ScheduleId => _scheduleId ?? string.Empty;
    public int MonthKey => Math.Max(0, _monthKey);
    public int ClaimedDayMask => _claimedDayMask & 0x0FFFFFFF;
    public int ExtraDayClaimedMask => _extraDayClaimedMask & 0x7;
    public int ClaimedCount =>
        CountBits(ClaimedDayMask) + CountBits(ExtraDayClaimedMask);
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

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (TryDeserialize(
                json,
                out AttendanceSaveData saveData,
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
            MonthKey,
            ClaimedDayMask,
            ExtraDayClaimedMask,
            LastClaimedDayKey,
            LastObservedDayKey));
    }

    public bool ImportJson(string json)
    {
        if (!TryDeserialize(json, out AttendanceSaveData saveData, out _))
            return false;

        ApplySaveData(saveData);
        SetLoadState(LocalDataLoadStatus.Success, false);
        return true;
    }

    internal bool ApplySchedule(string progressId, int monthKey)
    {
        string normalized = progressId?.Trim() ?? string.Empty;
        monthKey = Math.Max(0, monthKey);
        if (string.Equals(ScheduleId, normalized, StringComparison.Ordinal) &&
            MonthKey == monthKey)
        {
            return false;
        }

        _scheduleId = normalized;
        _monthKey = monthKey;
        _claimedDayMask = 0;
        _extraDayClaimedMask = 0;
        return true;
    }

    internal bool IsDayClaimed(int dayOfMonth)
    {
        if (dayOfMonth >= 1 && dayOfMonth <= 28)
            return (ClaimedDayMask & (1 << (dayOfMonth - 1))) != 0;
        if (dayOfMonth >= 29 && dayOfMonth <= 31)
        {
            return (ExtraDayClaimedMask &
                    (1 << (dayOfMonth - 29))) != 0;
        }
        return false;
    }

    internal bool ObserveDay(int dayKey)
    {
        dayKey = Math.Max(0, dayKey);
        if (dayKey <= _lastObservedDayKey)
            return false;

        _lastObservedDayKey = dayKey;
        return true;
    }

    internal void CommitClaim(
        string progressId,
        int monthKey,
        int dayKey)
    {
        ApplySchedule(progressId, monthKey);
        int dayOfMonth = Math.Max(0, dayKey) % 100;
        if (dayOfMonth >= 1 && dayOfMonth <= 28)
            _claimedDayMask |= 1 << (dayOfMonth - 1);
        else if (dayOfMonth >= 29 && dayOfMonth <= 31)
            _extraDayClaimedMask |= 1 << (dayOfMonth - 29);
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
        _monthKey = 0;
        _claimedDayMask = 0;
        _extraDayClaimedMask = 0;
        _lastClaimedDayKey = 0;
        _lastObservedDayKey = 0;
    }

    private void ApplySaveData(AttendanceSaveData saveData)
    {
        _scheduleId = saveData.ScheduleId;
        if (saveData.Version >= 2)
        {
            _monthKey = saveData.MonthKey;
            _claimedDayMask = saveData.ClaimedDayMask;
            _extraDayClaimedMask = saveData.ExtraDayClaimedMask;
        }
        else
        {
            int legacyMonthSource = Math.Max(
                saveData.LastObservedDayKey,
                saveData.LastClaimedDayKey);
            _monthKey = legacyMonthSource / 100;
            int migratedCount = Math.Min(28, saveData.ClaimedCount);
            _claimedDayMask = migratedCount <= 0
                ? 0
                : (1 << migratedCount) - 1;
            _extraDayClaimedMask = 0;
            int legacyClaimedDay = saveData.LastClaimedDayKey % 100;
            if (legacyClaimedDay >= 29 && legacyClaimedDay <= 31)
            {
                _extraDayClaimedMask |=
                    1 << (legacyClaimedDay - 29);
            }
        }
        _lastClaimedDayKey = saveData.LastClaimedDayKey;
        _lastObservedDayKey = Math.Max(
            saveData.LastObservedDayKey,
            saveData.LastClaimedDayKey);
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
                out AttendanceSaveData backup,
                out _))
        {
            return false;
        }

        PlayerPrefs.SetString(CorruptPlayerPrefsKey, corruptPrimaryJson);
        PlayerPrefs.Save();
        ApplySaveData(backup);
        SetLoadState(LocalDataLoadStatus.RecoveredFromBackup, false);
        Debug.LogWarning(
            "Attendance save data was restored from its last valid " +
            "backup. The rejected primary value was preserved under the " +
            "corrupt key.");
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
        out AttendanceSaveData saveData,
        out LocalDataLoadStatus failureStatus)
    {
        saveData = null;
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

        if (saveData.Version >= 2 &&
            (!LocalSaveJson.HasTopLevelProperty(json, "monthKey") ||
             !LocalSaveJson.HasTopLevelProperty(json, "claimedDayMask") ||
             !LocalSaveJson.HasTopLevelProperty(
                 json,
                 "extraDayClaimedMask")))
        {
            saveData = null;
            return false;
        }

        return true;
    }

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

    private void SetLoadState(
        LocalDataLoadStatus status,
        bool saveBlocked)
    {
        _lastLoadStatus = status;
        _saveBlocked = saveBlocked;
    }
}
