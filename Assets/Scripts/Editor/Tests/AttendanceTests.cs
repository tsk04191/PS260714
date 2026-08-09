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
            2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
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
    public void MonthChange_PreservesSequentialProgress()
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
        clock.UtcNow = clock.UtcNow.AddMonths(1);
        Assert.That(service.TryClaimToday().RewardIndex, Is.EqualTo(1));
        Assert.That(attendance.ClaimedCount, Is.EqualTo(2));
    }

    [Test]
    public void CalendarDate_DoesNotChooseTheRewardIndex()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 29, 2, 0, 0, TimeSpan.Zero));
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            false,
            CoreItemIds.SoftCredit,
            10L);
        AttendanceService service = CreateService(
            clock,
            schedule,
            out _,
            out _);

        AttendanceClaimResult result = service.TryClaimToday();

        Assert.That(result.Success, Is.True, result.Detail);
        Assert.That(result.RewardIndex, Is.EqualTo(0));
        Assert.That(
            service.RefreshStatus().Availability,
            Is.EqualTo(AttendanceAvailability.ClaimedToday));
    }

    [Test]
    public void RepeatingSchedule_WrapsAfterTheFinalReward()
    {
        TestClock clock = new(new DateTimeOffset(
            2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            10L);
        AttendanceService service = CreateService(
            clock,
            schedule,
            out AttendanceData attendance,
            out _);

        for (int index = 0;
             index < AttendanceRewardScheduleSO.CycleRewardCount;
             index++)
        {
            AttendanceClaimResult claim = service.TryClaimToday();
            Assert.That(claim.Success, Is.True, claim.Detail);
            Assert.That(claim.RewardIndex, Is.EqualTo(index));
            clock.UtcNow = clock.UtcNow.AddDays(1);
        }

        AttendanceClaimResult wrapped = service.TryClaimToday();
        Assert.That(wrapped.Success, Is.True, wrapped.Detail);
        Assert.That(wrapped.RewardIndex, Is.Zero);
        Assert.That(
            attendance.ClaimedCount,
            Is.EqualTo(AttendanceRewardScheduleSO.CycleRewardCount + 1));
    }

    [Test]
    public void NonRepeatingSchedule_CompletesAfterTheFinalReward()
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

        for (int index = 0;
             index < AttendanceRewardScheduleSO.CycleRewardCount;
             index++)
        {
            Assert.That(service.TryClaimToday().Success, Is.True);
            clock.UtcNow = clock.UtcNow.AddDays(1);
        }

        AttendanceStatus completed = service.RefreshStatus();
        Assert.That(
            completed.Availability,
            Is.EqualTo(AttendanceAvailability.ScheduleCompleted));
        Assert.That(completed.CanClaim, Is.False);
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
                2026, 8, 1, 2, 0, 0, TimeSpan.Zero));
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
    public void VersionTwoCalendarSave_MigratesClaimMasksToCount()
    {
        const string versionTwoJson =
            "{\"version\":2,\"scheduleId\":\"test.v1\"," +
            "\"claimedCount\":99,\"monthKey\":202608," +
            "\"claimedDayMask\":17,\"extraDayClaimedMask\":5," +
            "\"lastClaimedDayKey\":20260831," +
            "\"lastObservedDayKey\":20260831}";
        PlayerPrefs.SetString(AttendanceKey, versionTwoJson);
        PlayerPrefs.Save();

        AttendanceData attendance = new();
        LocalDataLoadStatus status = attendance.Load();

        Assert.That(status, Is.EqualTo(LocalDataLoadStatus.Success));
        Assert.That(attendance.ClaimedCount, Is.EqualTo(4));
        Assert.That(attendance.LastClaimedDayKey, Is.EqualTo(20260831));
        string migratedJson = attendance.ExportJson();
        StringAssert.Contains("\"version\":3", migratedJson);
        StringAssert.Contains("\"claimedCount\":4", migratedJson);
        StringAssert.DoesNotContain("monthKey", migratedJson);
        StringAssert.DoesNotContain("claimedDayMask", migratedJson);
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

    [Test]
    public void RuntimeSchedule_ExpandsContentsToFourBySeven()
    {
        AttendanceRewardScheduleSO schedule = CreateSchedule(
            true,
            CoreItemIds.SoftCredit,
            10L,
            CoreItemIds.BasicUpgradeMaterial,
            2L);

        Assert.That(
            schedule.DayCount,
            Is.EqualTo(AttendanceRewardScheduleSO.CycleRewardCount));
        Assert.That(schedule.TryValidate(out string reason), Is.True, reason);
    }

    [Test]
    public void NotificationDot_DoesNotOverwriteAuthoredLayout()
    {
        GameObject buttonObject = new("Button", typeof(RectTransform));
        GameObject dotObject = new(
            "Dot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(NotificationDotView));
        dotObject.transform.SetParent(buttonObject.transform, false);
        try
        {
            NotificationDotView dot =
                dotObject.GetComponent<NotificationDotView>();
            RectTransform rect = (RectTransform)dotObject.transform;
            rect.sizeDelta = new Vector2(27f, 31f);
            rect.anchoredPosition = new Vector2(8f, 11f);

            dot.SetVisible(false);
            dot.SetVisible(true);

            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(27f, 31f)));
            Assert.That(
                rect.anchoredPosition,
                Is.EqualTo(new Vector2(8f, 11f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buttonObject);
        }
    }

    [Test]
    public void AttendancePresentationPrefabs_AreDesignerEditableAssets()
    {
        MonthlyAttendancePopupView popup =
            Resources.Load<MonthlyAttendancePopupView>(
                "Presentation/AttendancePopup");
        AttendanceRewardCellView cell =
            Resources.Load<AttendanceRewardCellView>(
                "Presentation/AttendanceRewardCell");
        NotificationDotView dot = Resources.Load<NotificationDotView>(
            "Presentation/NotificationDot");

        Assert.That(popup, Is.Not.Null);
        Assert.That(popup.HasRequiredReferences, Is.True);
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.HasRequiredReferences, Is.True);
        RectTransform rewardIcon = cell.transform
            .Find("imgRewardIcon") as RectTransform;
        Assert.That(rewardIcon, Is.Not.Null);
        Assert.That(rewardIcon.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(rewardIcon.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(rewardIcon.sizeDelta, Is.EqualTo(new Vector2(56f, 56f)));
        Assert.That(cell.transform.Find("grpRewardTooltip"), Is.Null);
        Assert.That(dot, Is.Not.Null);
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
