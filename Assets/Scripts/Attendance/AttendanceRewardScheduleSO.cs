using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AttendanceItemReward
{
    [SerializeField] private ItemDefinitionSO item;
    [SerializeField, Min(1)] private long amount = 1L;

    public ItemDefinitionSO Item => item;
    public string ItemId => item != null
        ? item.ItemId
        : string.Empty;
    public long Amount => Math.Max(0L, amount);

    public AttendanceItemReward(
        ItemDefinitionSO item,
        long amount)
    {
        this.item = item;
        this.amount = Math.Max(1L, amount);
    }
}

[Serializable]
public sealed class AttendanceDayReward
{
    [SerializeField] private List<AttendanceItemReward> rewards = new();

    public IReadOnlyList<AttendanceItemReward> Rewards =>
        rewards ??= new List<AttendanceItemReward>();

    public AttendanceDayReward(
        IEnumerable<AttendanceItemReward> rewards)
    {
        this.rewards = rewards != null
            ? new List<AttendanceItemReward>(rewards)
            : new List<AttendanceItemReward>();
    }
}

[CreateAssetMenu(
    fileName = "AttendanceRewardSchedule",
    menuName = "PS260714/Attendance/Reward Schedule")]
public sealed class AttendanceRewardScheduleSO : ScriptableObject
{
    public const int CycleRewardCount = 28;

    [Header("Identity")]
    [SerializeField] private string scheduleId = "default_attendance";
    [SerializeField, Min(1)] private int contentVersion = 1;

    [Header("Cycle")]
    [SerializeField] private bool repeat = true;
    [SerializeField, Range(-720, 840)]
    private int resetUtcOffsetMinutes = 540;
    [SerializeField, Range(0, 23)] private int resetHour;

    [Header("Rewards")]
    [SerializeField] private List<AttendanceDayReward> days = new();

    public string ScheduleId => scheduleId?.Trim() ?? string.Empty;
    public int ContentVersion => Math.Max(1, contentVersion);
    public string ProgressId =>
        $"{ScheduleId}.v{ContentVersion}";
    public bool Repeat => repeat;
    public int ResetUtcOffsetMinutes =>
        Mathf.Clamp(resetUtcOffsetMinutes, -720, 840);
    public int ResetHour => Mathf.Clamp(resetHour, 0, 23);
    public IReadOnlyList<AttendanceDayReward> Days =>
        days ??= new List<AttendanceDayReward>();
    public int DayCount => Days.Count;

    public AttendanceDayReward GetDay(int index)
    {
        return index >= 0 && index < DayCount
            ? Days[index]
            : null;
    }

    public bool TryValidate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(ScheduleId))
        {
            reason = "The schedule id is empty.";
            return false;
        }

        if (DayCount != CycleRewardCount)
        {
            reason = $"The attendance schedule must contain exactly " +
                     $"{CycleRewardCount} sequential rewards.";
            return false;
        }

        for (int dayIndex = 0; dayIndex < DayCount; dayIndex++)
        {
            AttendanceDayReward day = Days[dayIndex];
            if (day == null || day.Rewards.Count == 0)
            {
                reason = $"Day {dayIndex + 1} has no rewards.";
                return false;
            }

            for (int rewardIndex = 0;
                 rewardIndex < day.Rewards.Count;
                 rewardIndex++)
            {
                AttendanceItemReward reward = day.Rewards[rewardIndex];
                if (reward == null || reward.Item == null ||
                    string.IsNullOrWhiteSpace(reward.ItemId) ||
                    reward.Amount <= 0L)
                {
                    reason =
                        $"Day {dayIndex + 1} reward {rewardIndex + 1} " +
                        "is invalid.";
                    return false;
                }

                if (ItemDefinitionCatalog.Get(reward.ItemId) == null)
                {
                    reason =
                        $"Item '{reward.ItemId}' is not registered in " +
                        "the item catalog.";
                    return false;
                }
            }
        }

        reason = string.Empty;
        return true;
    }

    public static AttendanceRewardScheduleSO CreateRuntime(
        string scheduleId,
        int contentVersion,
        bool repeat,
        int resetUtcOffsetMinutes,
        int resetHour,
        IEnumerable<AttendanceDayReward> days)
    {
        AttendanceRewardScheduleSO schedule =
            CreateInstance<AttendanceRewardScheduleSO>();
        schedule.hideFlags = HideFlags.DontSave;
        schedule.scheduleId = scheduleId?.Trim() ?? string.Empty;
        schedule.contentVersion = Math.Max(1, contentVersion);
        schedule.repeat = repeat;
        schedule.resetUtcOffsetMinutes =
            Mathf.Clamp(resetUtcOffsetMinutes, -720, 840);
        schedule.resetHour = Mathf.Clamp(resetHour, 0, 23);
        List<AttendanceDayReward> authoredDays = days != null
            ? new List<AttendanceDayReward>(days)
            : new List<AttendanceDayReward>();
        schedule.days = ExpandCycleRewards(authoredDays);
        return schedule;
    }

    private void OnValidate()
    {
        scheduleId = scheduleId?.Trim() ?? string.Empty;
        contentVersion = Math.Max(1, contentVersion);
        resetUtcOffsetMinutes =
            Mathf.Clamp(resetUtcOffsetMinutes, -720, 840);
        resetHour = Mathf.Clamp(resetHour, 0, 23);
        days ??= new List<AttendanceDayReward>();
        days = ExpandCycleRewards(days);
    }

    private static List<AttendanceDayReward> ExpandCycleRewards(
        IReadOnlyList<AttendanceDayReward> source)
    {
        List<AttendanceDayReward> result = new(CycleRewardCount);
        int sourceCount = source?.Count ?? 0;
        for (int index = 0; index < CycleRewardCount; index++)
        {
            result.Add(sourceCount > 0
                ? source[index % sourceCount]
                : new AttendanceDayReward(null));
        }
        return result;
    }
}

public static class AttendanceRewardScheduleCatalog
{
    private const string DefaultResourcePath =
        "Attendance/DefaultAttendanceSchedule";

    private static AttendanceRewardScheduleSO _cached;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _cached = null;
    }

    public static AttendanceRewardScheduleSO GetDefault()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<AttendanceRewardScheduleSO>(
            DefaultResourcePath);
        if (_cached == null)
            _cached = BuildDevelopmentFallback();
        return _cached;
    }

    public static void Invalidate()
    {
        _cached = null;
    }

    private static AttendanceRewardScheduleSO BuildDevelopmentFallback()
    {
        AttendanceDayReward Day(string itemId, long amount)
        {
            return new AttendanceDayReward(new[]
            {
                new AttendanceItemReward(
                    ItemDefinitionCatalog.Get(itemId),
                    amount),
            });
        }

        return AttendanceRewardScheduleSO.CreateRuntime(
            "default_attendance",
            1,
            true,
            540,
            0,
            new[]
            {
                Day(CoreItemIds.SoftCredit, 1000L),
                Day(CoreItemIds.BasicUpgradeMaterial, 5L),
                Day(CoreItemIds.StandardRecruitTicket, 1L),
                Day(CoreItemIds.SoftCredit, 1500L),
                Day(CoreItemIds.BasicUpgradeMaterial, 10L),
                Day(CoreItemIds.FreeCredit, 50L),
                Day(CoreItemIds.StandardRecruitTicket, 2L),
            });
    }
}
