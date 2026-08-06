using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MonthlyAttendancePopupView : MonoBehaviour
{
    private const string ResourcePath = "Presentation/AttendancePopup";

    [Header("Popup")]
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI monthText;

    [Header("Calendar")]
    [SerializeField] private RectTransform calendarRoot;
    [SerializeField] private AttendanceRewardCellView rewardCellPrefab;
    [SerializeField] private GameObject extraRewardRoot;
    [SerializeField] private AttendanceRewardCellView extraRewardCell;

    [Header("Footer")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI resetText;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimLabel;

    private readonly List<AttendanceRewardCellView> _cells = new();
    private AttendanceService _service;

    public static MonthlyAttendancePopupView BuildOrBind(
        RectTransform parent)
    {
        if (parent == null)
            return null;

        MonthlyAttendancePopupView existing =
            parent.GetComponentInChildren<MonthlyAttendancePopupView>(true);
        if (existing != null)
            return existing;

        MonthlyAttendancePopupView prefab =
            Resources.Load<MonthlyAttendancePopupView>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"Attendance popup prefab is missing at Resources/" +
                $"{ResourcePath}.",
                parent);
            return null;
        }

        MonthlyAttendancePopupView instance =
            Instantiate(prefab, parent, false);
        instance.name = "grpAttendancePopup";
        instance.gameObject.SetActive(false);
        return instance;
    }

    public bool HasRequiredReferences =>
        backdropButton != null && closeButton != null &&
        titleText != null && descriptionText != null && monthText != null &&
        calendarRoot != null && rewardCellPrefab != null &&
        extraRewardRoot != null && extraRewardCell != null &&
        statusText != null && resetText != null && claimButton != null &&
        claimLabel != null;

    public void Bind(AttendanceService service)
    {
        _service = service;
        if (isActiveAndEnabled)
            Refresh();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (!HasRequiredReferences)
            return;

        titleText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceTitle);
        descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceDescription);
        claimLabel.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceClaim);

        AttendanceStatus status = _service?.RefreshStatus();
        AttendanceRewardScheduleSO schedule = _service?.Schedule;
        RefreshMonth(status);
        RefreshCalendar(schedule, status);
        RefreshStatus(status, schedule);
    }

    private void Awake()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError(
                "MonthlyAttendancePopupView prefab references are " +
                "incomplete.",
                this);
            return;
        }

        backdropButton.onClick.RemoveAllListeners();
        backdropButton.onClick.AddListener(Hide);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Hide);
        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(HandleClaimClicked);
        EnsureCells();
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void EnsureCells()
    {
        _cells.Clear();
        for (int index = 0; index < calendarRoot.childCount; index++)
        {
            AttendanceRewardCellView existing = calendarRoot
                .GetChild(index)
                .GetComponent<AttendanceRewardCellView>();
            if (existing != null)
                _cells.Add(existing);
        }

        while (_cells.Count < AttendanceRewardScheduleSO.MonthlyRewardCount)
        {
            AttendanceRewardCellView cell = Instantiate(
                rewardCellPrefab,
                calendarRoot,
                false);
            cell.name = $"grpAttendanceReward{_cells.Count + 1:00}";
            cell.gameObject.SetActive(true);
            _cells.Add(cell);
        }
        for (int index = 0; index < _cells.Count; index++)
        {
            _cells[index].gameObject.SetActive(
                index < AttendanceRewardScheduleSO.MonthlyRewardCount);
        }
    }

    private void RefreshMonth(AttendanceStatus status)
    {
        int monthKey = status?.MonthKey ?? 0;
        monthText.text = monthKey > 0
            ? $"{monthKey / 100:0000}.{monthKey % 100:00}"
            : string.Empty;
    }

    private void RefreshCalendar(
        AttendanceRewardScheduleSO schedule,
        AttendanceStatus status)
    {
        EnsureCells();
        int claimedMask = status?.ClaimedDayMask ?? 0;
        int todayIndex = status != null && status.DayOfMonth <= 28
            ? status.DayOfMonth - 1
            : -1;
        for (int index = 0;
             index < AttendanceRewardScheduleSO.MonthlyRewardCount;
             index++)
        {
            bool claimed = (claimedMask & (1 << index)) != 0;
            _cells[index].Bind(
                schedule?.GetDay(index),
                claimed,
                index == todayIndex);
        }

        int year = (status?.MonthKey ?? 0) / 100;
        int month = (status?.MonthKey ?? 0) % 100;
        bool hasExtraDays = year > 0 && month >= 1 && month <= 12 &&
                            DateTime.DaysInMonth(year, month) > 28;
        extraRewardRoot.SetActive(hasExtraDays);
        if (!hasExtraDays)
            return;

        int dayOfMonth = status?.DayOfMonth ?? 0;
        bool extraToday = dayOfMonth >= 29;
        bool extraClaimed = extraToday &&
                            ((status.ExtraDayClaimedMask &
                              (1 << (dayOfMonth - 29))) != 0);
        extraRewardCell.Bind(
            schedule?.ExtraDayReward,
            extraClaimed,
            extraToday);
    }

    private void RefreshStatus(
        AttendanceStatus status,
        AttendanceRewardScheduleSO schedule)
    {
        AttendanceAvailability availability =
            status?.Availability ?? AttendanceAvailability.NotReady;
        string key = availability switch
        {
            AttendanceAvailability.Claimable =>
                LocalizationKeys.UiAttendanceAvailable,
            AttendanceAvailability.ClaimedToday =>
                LocalizationKeys.UiAttendanceCompletedToday,
            AttendanceAvailability.NotConfigured =>
                LocalizationKeys.UiAttendanceNotConfigured,
            AttendanceAvailability.SaveBlocked =>
                LocalizationKeys.UiAttendanceSaveBlocked,
            AttendanceAvailability.ClockRollback =>
                LocalizationKeys.UiAttendanceClockRollback,
            AttendanceAvailability.InventoryFull =>
                LocalizationKeys.UiAttendanceInventoryFull,
            _ => LocalizationKeys.UiAttendanceNotReady,
        };
        statusText.text = LocalizationService.Get(key);
        claimButton.interactable =
            availability == AttendanceAvailability.Claimable;

        if (status == null || schedule == null ||
            status.NextResetUtc == default)
        {
            resetText.text = string.Empty;
            return;
        }

        DateTimeOffset resetLocal = status.NextResetUtc.ToOffset(
            TimeSpan.FromMinutes(schedule.ResetUtcOffsetMinutes));
        resetText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceNextReset,
            LocalizationService.Arg(
                "time",
                resetLocal.ToString("yyyy-MM-dd HH:mm")));
    }

    private void HandleClaimClicked()
    {
        AttendanceClaimResult result = _service?.TryClaimToday();
        Refresh();
        if (result?.Success != true)
            return;

        statusText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceClaimSuccess,
            LocalizationService.Arg(
                "reward",
                FormatReward(result.Reward)));
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        Refresh();
    }

    private static string FormatReward(AttendanceDayReward day)
    {
        if (day?.Rewards == null)
            return "-";
        List<string> labels = new();
        for (int index = 0; index < day.Rewards.Count; index++)
        {
            AttendanceItemReward reward = day.Rewards[index];
            if (reward?.Item == null)
                continue;
            labels.Add(
                $"{reward.Item.GetLocalizedDisplayName()} ×" +
                $"{reward.Amount:N0}");
        }
        return labels.Count > 0 ? string.Join(" / ", labels) : "-";
    }
}
