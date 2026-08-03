using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PS260714.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AttendanceTests
{
    private const string AttendanceKey = "Attendance.Progress.v1";
    private const string AttendanceBackupKey =
        AttendanceKey + ".backup";
    private const string AttendanceCorruptKey =
        AttendanceKey + ".corrupt";
    private const string InventoryKey = "Inventory.Collection.v1";
    private const string InventoryBackupKey =
        InventoryKey + ".backup";
    private const string InventoryCorruptKey =
        InventoryKey + ".corrupt";

    private static readonly string[] SaveKeys =
    {
        AttendanceKey,
        AttendanceBackupKey,
        AttendanceCorruptKey,
        InventoryKey,
        InventoryBackupKey,
        InventoryCorruptKey,
    };

    private readonly Dictionary<string, (bool Exists, string Value)>
        _savedPlayerPrefs = new();
    private readonly List<AttendanceRewardScheduleSO> _schedules = new();

    [SetUp]
    public void PreservePlayerPrefs()
    {
        _savedPlayerPrefs.Clear();
        foreach (string key in SaveKeys)
        {
            bool exists = PlayerPrefs.HasKey(key);
            _savedPlayerPrefs[key] = (
                exists,
                exists ? PlayerPrefs.GetString(key) : string.Empty);
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        ItemDefinitionCatalog.Invalidate();
    }

    [TearDown]
    public void RestorePlayerPrefs()
    {
        for (int index = 0; index < _schedules.Count; index++)
        {
            if (_schedules[index] != null)
                UnityEngine.Object.DestroyImmediate(_schedules[index]);
        }
        _schedules.Clear();

        foreach (string key in SaveKeys)
        {
            (bool exists, string value) = _savedPlayerPrefs[key];
            if (exists)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        ItemDefinitionCatalog.Invalidate();
    }

    [Test]
    public void ServiceDay_ChangesAtKoreanMidnight()
    {
        DateTimeOffset beforeReset = new(
            2026, 8, 3, 14, 59, 59, TimeSpan.Zero);
        DateTimeOffset atReset = new(
            2026, 8, 3, 15, 0, 0, TimeSpan.Zero);

        Assert.That(
            AttendanceService.GetServiceDayKey(beforeReset, 540, 0),
            Is.EqualTo(20260803));
        Assert.That(
            AttendanceService.GetServiceDayKey(atReset, 540, 0),
            Is.EqualTo(20260804));
    }

    [Test]
    public void Claim_GrantsRewardAndBlocksSameDayDuplicate()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 3, 2, 0, 0, TimeSpan.Zero));
        AttendanceService service = CreateService(clock, out var attendance,
            out var inventory);
        long previous = inventory.GetAmount(CoreItemIds.SoftCredit);

        AttendanceClaimResult first = service.TryClaimToday();
        AttendanceClaimResult second = service.TryClaimToday();

        Assert.That(first.Success, Is.True, first.Detail);
        Assert.That(second.Success, Is.False);
        Assert.That(
            second.Availability,
            Is.EqualTo(AttendanceAvailability.ClaimedToday));
        Assert.That(attendance.ClaimedCount, Is.EqualTo(1));
        Assert.That(
            inventory.GetAmount(CoreItemIds.SoftCredit),
            Is.EqualTo(previous + 1000L));
    }

    [Test]
    public void MissedDays_DoNotSkipSequentialReward()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
        AttendanceService service = CreateService(clock, out var attendance,
            out var inventory);

        Assert.That(service.TryClaimToday().Success, Is.True);
        clock.UtcNow = clock.UtcNow.AddDays(10);
        AttendanceClaimResult next = service.TryClaimToday();

        Assert.That(next.Success, Is.True, next.Detail);
        Assert.That(next.RewardIndex, Is.EqualTo(1));
        Assert.That(attendance.ClaimedCount, Is.EqualTo(2));
        Assert.That(
            inventory.GetAmount(CoreItemIds.BasicUpgradeMaterial),
            Is.EqualTo(5L));
    }

    [Test]
    public void RepeatingSchedule_WrapsToFirstReward()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            10L,
            CoreItemIds.BasicUpgradeMaterial,
            2L);
        AttendanceService service = CreateService(
            clock,
            schedule,
            out AttendanceData attendance,
            out _);

        Assert.That(service.TryClaimToday().RewardIndex, Is.EqualTo(0));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        Assert.That(service.TryClaimToday().RewardIndex, Is.EqualTo(1));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        Assert.That(service.TryClaimToday().RewardIndex, Is.EqualTo(0));
        Assert.That(attendance.ClaimedCount, Is.EqualTo(3));
    }

    [Test]
    public void NonRepeatingSchedule_CompletesAfterFinalReward()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            false,
            CoreItemIds.SoftCredit,
            10L);
        AttendanceService service = CreateService(
            clock,
            schedule,
            out _,
            out _);

        Assert.That(service.TryClaimToday().Success, Is.True);
        clock.UtcNow = clock.UtcNow.AddDays(1);

        Assert.That(
            service.RefreshStatus().Availability,
            Is.EqualTo(AttendanceAvailability.ScheduleCompleted));
    }

    [Test]
    public void ClockRollback_BlocksClaiming()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 3, 2, 0, 0, TimeSpan.Zero));
        AttendanceService service = CreateService(clock, out _, out _);
        Assert.That(
            service.RefreshStatus().Availability,
            Is.EqualTo(AttendanceAvailability.Claimable));

        clock.UtcNow = clock.UtcNow.AddDays(-1);

        Assert.That(
            service.RefreshStatus().Availability,
            Is.EqualTo(AttendanceAvailability.ClockRollback));
        Assert.That(service.TryClaimToday().Success, Is.False);
    }

    [Test]
    public void ScheduleVersionChange_PreservesSameDayClaimBlock()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 3, 2, 0, 0, TimeSpan.Zero));
        AttendanceData attendance = LoadAttendance();
        InventoryData inventory = LoadInventory();
        AttendanceRewardScheduleSO versionOne = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            10L,
            contentVersion: 1);
        AttendanceRewardScheduleSO versionTwo = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            20L,
            contentVersion: 2);

        AttendanceService first = new(
            attendance,
            inventory,
            versionOne,
            clock);
        Assert.That(first.TryClaimToday().Success, Is.True);

        AttendanceService changed = new(
            attendance,
            inventory,
            versionTwo,
            clock);
        AttendanceStatus status = changed.RefreshStatus();

        Assert.That(
            status.Availability,
            Is.EqualTo(AttendanceAvailability.ClaimedToday));
        Assert.That(attendance.ClaimedCount, Is.Zero);
    }

    [Test]
    public void StackLimit_BlocksPartialReward()
    {
        ItemDefinitionSO softCredit =
            ItemDefinitionCatalog.Get(CoreItemIds.SoftCredit);
        Assert.That(softCredit, Is.Not.Null);
        SerializedObject serialized = new(softCredit);
        SerializedProperty maximum = serialized.FindProperty("maximumStack");
        long previousMaximum = maximum.longValue;
        try
        {
            maximum.longValue = 500L;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            TestClock clock = new(new DateTimeOffset(
                2026, 8, 3, 2, 0, 0, TimeSpan.Zero));
            AttendanceService service = CreateService(clock, out _, out var inventory);

            AttendanceStatus status = service.RefreshStatus();

            Assert.That(
                status.Availability,
                Is.EqualTo(AttendanceAvailability.InventoryFull));
            Assert.That(service.TryClaimToday().Success, Is.False);
            Assert.That(
                inventory.GetAmount(CoreItemIds.SoftCredit),
                Is.Zero);
        }
        finally
        {
            serialized.Update();
            maximum = serialized.FindProperty("maximumStack");
            maximum.longValue = previousMaximum;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [Test]
    public void CorruptPrimary_BlocksSaveAndPreservesRawValue()
    {
        const string corruptJson = "{broken";
        PlayerPrefs.SetString(AttendanceKey, corruptJson);
        PlayerPrefs.Save();
        LogAssert.Expect(
            LogType.Error,
            new Regex("Attendance save data is corrupt"));

        AttendanceData attendance = new();
        LocalDataLoadStatus status = attendance.Load();
        LogAssert.Expect(
            LogType.Warning,
            new Regex("Attendance save was skipped"));
        attendance.Save();

        Assert.That(status, Is.EqualTo(LocalDataLoadStatus.Corrupt));
        Assert.That(attendance.IsSaveBlocked, Is.True);
        Assert.That(
            PlayerPrefs.GetString(AttendanceKey),
            Is.EqualTo(corruptJson));
    }

    [Test]
    public void CorruptPrimary_RecoversValidBackup()
    {
        const string corruptJson = "{broken";
        const string backupJson =
            "{\"version\":1,\"scheduleId\":\"test.v1\"," +
            "\"claimedCount\":3,\"lastClaimedDayKey\":20260802," +
            "\"lastObservedDayKey\":20260803}";
        PlayerPrefs.SetString(AttendanceKey, corruptJson);
        PlayerPrefs.SetString(AttendanceBackupKey, backupJson);
        PlayerPrefs.Save();
        LogAssert.Expect(
            LogType.Warning,
            new Regex("Attendance save data was restored"));

        AttendanceData attendance = new();
        LocalDataLoadStatus status = attendance.Load();

        Assert.That(
            status,
            Is.EqualTo(LocalDataLoadStatus.RecoveredFromBackup));
        Assert.That(attendance.IsSaveBlocked, Is.False);
        Assert.That(attendance.ClaimedCount, Is.EqualTo(3));
        Assert.That(attendance.LastClaimedDayKey, Is.EqualTo(20260802));
        Assert.That(
            PlayerPrefs.GetString(AttendanceCorruptKey),
            Is.EqualTo(corruptJson));
    }

    [Test]
    public void AttendanceLocalizationKeys_AreGenerated()
    {
        string[] keys =
        {
            LocalizationKeys.UiMainAttendance,
            LocalizationKeys.UiAttendanceTitle,
            LocalizationKeys.UiAttendanceClaim,
            LocalizationKeys.UiAttendanceAvailable,
            LocalizationKeys.UiAttendanceClockRollback,
        };

        foreach (string key in keys)
        {
            Assert.That(
                GeneratedLocalizationTables.ReferenceEntries.ContainsKey(key),
                Is.True,
                key);
        }
    }

    private AttendanceService CreateService(
        TestClock clock,
        out AttendanceData attendance,
        out InventoryData inventory)
    {
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            1000L,
            CoreItemIds.BasicUpgradeMaterial,
            5L,
            CoreItemIds.StandardRecruitTicket,
            1L);
        return CreateService(
            clock,
            schedule,
            out attendance,
            out inventory);
    }

    private static AttendanceService CreateService(
        TestClock clock,
        AttendanceRewardScheduleSO schedule,
        out AttendanceData attendance,
        out InventoryData inventory)
    {
        attendance = LoadAttendance();
        inventory = LoadInventory();
        return new AttendanceService(
            attendance,
            inventory,
            schedule,
            clock);
    }

    private AttendanceRewardScheduleSO CreateSchedule(
        bool repeat,
        params object[] rewardPairs)
    {
        return CreateSchedule(repeat, rewardPairs, 1);
    }

    private AttendanceRewardScheduleSO CreateSchedule(
        bool repeat,
        string itemId,
        long amount,
        int contentVersion)
    {
        return CreateSchedule(
            repeat,
            new object[] { itemId, amount },
            contentVersion);
    }

    private AttendanceRewardScheduleSO CreateSchedule(
        bool repeat,
        object[] rewardPairs,
        int contentVersion)
    {
        List<AttendanceDayReward> days = new();
        for (int index = 0; index < rewardPairs.Length; index += 2)
        {
            string itemId = (string)rewardPairs[index];
            long amount = Convert.ToInt64(rewardPairs[index + 1]);
            ItemDefinitionSO item = ItemDefinitionCatalog.Get(itemId);
            Assert.That(item, Is.Not.Null, itemId);
            days.Add(new AttendanceDayReward(new[]
            {
                new AttendanceItemReward(item, amount),
            }));
        }

        AttendanceRewardScheduleSO schedule =
            AttendanceRewardScheduleSO.CreateRuntime(
                "test_attendance",
                contentVersion,
                repeat,
                540,
                0,
                days);
        _schedules.Add(schedule);
        return schedule;
    }

    private static AttendanceData LoadAttendance()
    {
        AttendanceData attendance = new();
        attendance.Load();
        return attendance;
    }

    private static InventoryData LoadInventory()
    {
        InventoryData inventory = new();
        inventory.Load();
        return inventory;
    }

    private sealed class TestClock : IAttendanceClock
    {
        public DateTimeOffset UtcNow { get; set; }

        public TestClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }
    }
}
