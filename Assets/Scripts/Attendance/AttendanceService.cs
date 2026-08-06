using System;
using System.Collections.Generic;
using UnityEngine;

public interface IAttendanceClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemAttendanceClock : IAttendanceClock
{
    public static readonly SystemAttendanceClock Instance = new();

    private SystemAttendanceClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public enum AttendanceAvailability
{
    NotReady = 0,
    NotConfigured = 1,
    SaveBlocked = 2,
    ClockRollback = 3,
    ClaimedToday = 4,
    Claimable = 5,
    ScheduleCompleted = 6,
    InventoryFull = 7,
}

public sealed class AttendanceStatus
{
    public AttendanceAvailability Availability { get; }
    public int ServiceDayKey { get; }
    public int ClaimedCount { get; }
    public int RewardIndex { get; }
    public int ClaimedInDisplayedCycle { get; }
    public int MonthKey { get; }
    public int DayOfMonth { get; }
    public int ClaimedDayMask { get; }
    public int ExtraDayClaimedMask { get; }
    public bool IsExtraDayReward => DayOfMonth > 28;
    public DateTimeOffset NextResetUtc { get; }
    public AttendanceDayReward Reward { get; }
    public string Detail { get; }

    public bool CanClaim =>
        Availability == AttendanceAvailability.Claimable;

    internal AttendanceStatus(
        AttendanceAvailability availability,
        int serviceDayKey,
        int claimedCount,
        int rewardIndex,
        int claimedInDisplayedCycle,
        int monthKey,
        int dayOfMonth,
        int claimedDayMask,
        int extraDayClaimedMask,
        DateTimeOffset nextResetUtc,
        AttendanceDayReward reward,
        string detail = "")
    {
        Availability = availability;
        ServiceDayKey = serviceDayKey;
        ClaimedCount = Math.Max(0, claimedCount);
        RewardIndex = rewardIndex;
        ClaimedInDisplayedCycle = Math.Max(0, claimedInDisplayedCycle);
        MonthKey = Math.Max(0, monthKey);
        DayOfMonth = Mathf.Clamp(dayOfMonth, 0, 31);
        ClaimedDayMask = claimedDayMask & 0x0FFFFFFF;
        ExtraDayClaimedMask = extraDayClaimedMask & 0x7;
        NextResetUtc = nextResetUtc;
        Reward = reward;
        Detail = detail ?? string.Empty;
    }
}

public sealed class AttendanceClaimResult
{
    public bool Success { get; }
    public AttendanceAvailability Availability { get; }
    public int RewardIndex { get; }
    public AttendanceDayReward Reward { get; }
    public string Detail { get; }

    internal AttendanceClaimResult(
        bool success,
        AttendanceAvailability availability,
        int rewardIndex,
        AttendanceDayReward reward,
        string detail = "")
    {
        Success = success;
        Availability = availability;
        RewardIndex = rewardIndex;
        Reward = reward;
        Detail = detail ?? string.Empty;
    }
}

public sealed class AttendanceService
{
    private readonly AttendanceData _attendance;
    private readonly InventoryData _inventory;
    private readonly AttendanceRewardScheduleSO _schedule;
    private readonly IAttendanceClock _clock;
    private readonly Func<bool> _isReady;

    public AttendanceRewardScheduleSO Schedule => _schedule;

    public AttendanceService(
        AttendanceData attendance,
        InventoryData inventory,
        AttendanceRewardScheduleSO schedule,
        IAttendanceClock clock = null,
        Func<bool> isReady = null)
    {
        _attendance = attendance;
        _inventory = inventory;
        _schedule = schedule;
        _clock = clock ?? SystemAttendanceClock.Instance;
        _isReady = isReady;
    }

    public AttendanceStatus RefreshStatus(bool persistChanges = true)
    {
        if (_attendance == null || _inventory == null ||
            (_isReady != null && !_isReady()))
        {
            return BuildStatus(AttendanceAvailability.NotReady, 0);
        }

        string scheduleError = "The attendance schedule is missing.";
        if (_schedule == null ||
            !_schedule.TryValidate(out scheduleError))
        {
            return BuildStatus(
                AttendanceAvailability.NotConfigured,
                0,
                scheduleError);
        }

        DateTimeOffset nowUtc = _clock.UtcNow.ToUniversalTime();
        int serviceDayKey = GetServiceDayKey(
            nowUtc,
            _schedule.ResetUtcOffsetMinutes,
            _schedule.ResetHour);
        int monthKey = serviceDayKey / 100;
        int dayOfMonth = serviceDayKey % 100;

        if (_attendance.IsSaveBlocked || _inventory.IsSaveBlocked)
        {
            return BuildStatus(
                AttendanceAvailability.SaveBlocked,
                serviceDayKey);
        }

        if ((_attendance.LastObservedDayKey > 0 &&
             serviceDayKey < _attendance.LastObservedDayKey) ||
            (_attendance.LastClaimedDayKey > 0 &&
             serviceDayKey < _attendance.LastClaimedDayKey))
        {
            return BuildStatus(
                AttendanceAvailability.ClockRollback,
                serviceDayKey);
        }

        bool changed = _attendance.ApplySchedule(
            _schedule.ProgressId,
            monthKey);
        changed |= _attendance.ObserveDay(serviceDayKey);
        if (changed)
        {
            if (persistChanges)
            {
                _attendance.Save();
                _attendance.NotifyChanged();
            }
        }

        if (_attendance.LastClaimedDayKey == serviceDayKey ||
            _attendance.IsDayClaimed(dayOfMonth))
        {
            return BuildStatus(
                AttendanceAvailability.ClaimedToday,
                serviceDayKey);
        }

        AttendanceStatus claimable = BuildStatus(
            AttendanceAvailability.Claimable,
            serviceDayKey);
        if (!CanReceiveExactly(claimable.Reward, out string capacityError))
        {
            return BuildStatus(
                AttendanceAvailability.InventoryFull,
                serviceDayKey,
                capacityError);
        }

        return claimable;
    }

    public AttendanceClaimResult TryClaimToday()
    {
        AttendanceStatus status = RefreshStatus();
        if (!status.CanClaim)
        {
            return new AttendanceClaimResult(
                false,
                status.Availability,
                status.RewardIndex,
                status.Reward,
                status.Detail);
        }

        if (!TryBuildDeltas(
                status.Reward,
                out List<InventoryDelta> deltas,
                out string rewardError))
        {
            return new AttendanceClaimResult(
                false,
                AttendanceAvailability.NotConfigured,
                status.RewardIndex,
                status.Reward,
                rewardError);
        }

        if (!CanReceiveExactly(status.Reward, out string capacityError))
        {
            return new AttendanceClaimResult(
                false,
                AttendanceAvailability.InventoryFull,
                status.RewardIndex,
                status.Reward,
                capacityError);
        }

        string inventorySnapshot = _inventory.ExportJson();
        string attendanceSnapshot = _attendance.ExportJson();
        try
        {
            if (!_inventory.TryApply(deltas, save: false))
            {
                return new AttendanceClaimResult(
                    false,
                    AttendanceAvailability.InventoryFull,
                    status.RewardIndex,
                    status.Reward,
                    "The reward could not be added to the inventory.");
            }

            _attendance.CommitClaim(
                _schedule.ProgressId,
                status.MonthKey,
                status.ServiceDayKey);
            _inventory.Save(flush: false);
            _attendance.Save(flush: false);
            PlayerPrefs.Save();
            _attendance.NotifyChanged();

            return new AttendanceClaimResult(
                true,
                AttendanceAvailability.ClaimedToday,
                status.RewardIndex,
                status.Reward);
        }
        catch (Exception exception)
        {
            _inventory.ImportJson(inventorySnapshot);
            _attendance.ImportJson(attendanceSnapshot);
            Debug.LogError(
                "Attendance reward transaction failed and was rolled " +
                $"back in memory: {exception.Message}");
            return new AttendanceClaimResult(
                false,
                AttendanceAvailability.SaveBlocked,
                status.RewardIndex,
                status.Reward,
                exception.Message);
        }
    }

    public static int GetServiceDayKey(
        DateTimeOffset utcNow,
        int resetUtcOffsetMinutes,
        int resetHour)
    {
        TimeSpan offset = TimeSpan.FromMinutes(
            Mathf.Clamp(resetUtcOffsetMinutes, -720, 840));
        DateTimeOffset serviceTime = utcNow
            .ToUniversalTime()
            .ToOffset(offset)
            .AddHours(-Mathf.Clamp(resetHour, 0, 23));
        return serviceTime.Year * 10000 +
               serviceTime.Month * 100 +
               serviceTime.Day;
    }

    public static DateTimeOffset GetNextResetUtc(
        DateTimeOffset utcNow,
        int resetUtcOffsetMinutes,
        int resetHour)
    {
        TimeSpan offset = TimeSpan.FromMinutes(
            Mathf.Clamp(resetUtcOffsetMinutes, -720, 840));
        DateTimeOffset localNow = utcNow.ToUniversalTime().ToOffset(offset);
        DateTime localResetDateTime = new(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            Mathf.Clamp(resetHour, 0, 23),
            0,
            0,
            DateTimeKind.Unspecified);
        DateTimeOffset next = new(localResetDateTime, offset);
        if (localNow >= next)
            next = next.AddDays(1);
        return next.ToUniversalTime();
    }

    private AttendanceStatus BuildStatus(
        AttendanceAvailability availability,
        int serviceDayKey,
        string detail = "")
    {
        int claimedCount = _attendance?.ClaimedCount ?? 0;
        int monthKey = Math.Max(0, serviceDayKey / 100);
        int dayOfMonth = Math.Max(0, serviceDayKey % 100);
        int rewardIndex = dayOfMonth >= 1 && dayOfMonth <= 28
            ? dayOfMonth - 1
            : -1;
        int claimedInCycle = CountBits(
            _attendance?.ClaimedDayMask ?? 0);
        AttendanceDayReward reward = rewardIndex >= 0
            ? _schedule?.GetDay(rewardIndex)
            : dayOfMonth >= 29 && dayOfMonth <= 31
                ? _schedule?.ExtraDayReward
                : null;
        DateTimeOffset nextReset = _schedule != null
            ? GetNextResetUtc(
                _clock.UtcNow,
                _schedule.ResetUtcOffsetMinutes,
                _schedule.ResetHour)
            : default;
        return new AttendanceStatus(
            availability,
            serviceDayKey,
            claimedCount,
            rewardIndex,
            claimedInCycle,
            monthKey,
            dayOfMonth,
            _attendance?.ClaimedDayMask ?? 0,
            _attendance?.ExtraDayClaimedMask ?? 0,
            nextReset,
            reward,
            detail);
    }

    private bool CanReceiveExactly(
        AttendanceDayReward day,
        out string error)
    {
        error = string.Empty;
        if (!TryAggregateRewards(
                day,
                out Dictionary<string, decimal> rewards,
                out error))
        {
            return false;
        }

        foreach (KeyValuePair<string, decimal> pair in rewards)
        {
            decimal candidate = _inventory.GetAmount(pair.Key) + pair.Value;
            if (candidate > long.MaxValue)
            {
                error = $"Item '{pair.Key}' would overflow its amount.";
                return false;
            }

            long nextAmount = (long)candidate;
            ItemDefinitionSO definition = ItemDefinitionCatalog.Get(pair.Key);
            if (definition == null ||
                definition.ClampAmount(nextAmount) != nextAmount)
            {
                error = $"Item '{pair.Key}' has reached its stack limit.";
                return false;
            }
        }

        return true;
    }

    private static bool TryBuildDeltas(
        AttendanceDayReward day,
        out List<InventoryDelta> deltas,
        out string error)
    {
        deltas = new List<InventoryDelta>();
        if (!TryAggregateRewards(day, out var rewards, out error))
            return false;

        foreach (KeyValuePair<string, decimal> pair in rewards)
        {
            if (pair.Value <= 0m || pair.Value > long.MaxValue)
            {
                error = $"Item '{pair.Key}' has an invalid reward amount.";
                return false;
            }
            deltas.Add(new InventoryDelta(pair.Key, (long)pair.Value));
        }

        return true;
    }

    private static bool TryAggregateRewards(
        AttendanceDayReward day,
        out Dictionary<string, decimal> rewards,
        out string error)
    {
        rewards = new Dictionary<string, decimal>(StringComparer.Ordinal);
        error = string.Empty;
        if (day == null || day.Rewards == null || day.Rewards.Count == 0)
        {
            error = "The attendance day has no rewards.";
            return false;
        }

        for (int index = 0; index < day.Rewards.Count; index++)
        {
            AttendanceItemReward reward = day.Rewards[index];
            if (reward == null || reward.Item == null ||
                string.IsNullOrWhiteSpace(reward.ItemId) ||
                reward.Amount <= 0L ||
                ItemDefinitionCatalog.Get(reward.ItemId) == null)
            {
                error = $"Attendance reward {index + 1} is invalid.";
                return false;
            }

            rewards.TryGetValue(reward.ItemId, out decimal current);
            rewards[reward.ItemId] = current + reward.Amount;
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
}
