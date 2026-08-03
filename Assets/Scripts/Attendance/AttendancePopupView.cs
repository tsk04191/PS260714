using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AttendancePopupView : MonoBehaviour
{
    private static readonly Color OverlayColor =
        new(0.01f, 0.015f, 0.012f, 0.86f);
    private static readonly Color PanelColor =
        new(0.065f, 0.085f, 0.072f, 0.99f);
    private static readonly Color CardColor =
        new(0.12f, 0.155f, 0.13f, 1f);
    private static readonly Color ClaimedColor =
        new(0.09f, 0.12f, 0.1f, 0.82f);
    private static readonly Color TodayColor =
        new(0.22f, 0.34f, 0.255f, 1f);
    private static readonly Color AccentColor =
        new(0.75f, 0.82f, 0.5f, 1f);
    private static readonly Color TextColor =
        new(0.94f, 0.91f, 0.78f, 1f);

    private AttendanceService _service;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _resetText;
    private RectTransform _listContent;
    private Button _claimButton;
    private TextMeshProUGUI _claimLabel;
    private Button _closeButton;
    private Button _backdropButton;
    private bool _built;

    public static AttendancePopupView BuildOrBind(RectTransform parent)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find("grpAttendancePopup");
        GameObject root;
        if (existing != null)
        {
            root = existing.gameObject;
        }
        else
        {
            root = new GameObject(
                "grpAttendancePopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            root.transform.SetParent(parent, false);
        }

        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        Image image = root.GetComponent<Image>();
        image.color = OverlayColor;
        image.raycastTarget = true;

        AttendancePopupView view =
            root.GetComponent<AttendancePopupView>() ??
            root.AddComponent<AttendancePopupView>();
        view.BuildUi();
        root.SetActive(false);
        return view;
    }

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
        if (!_built)
            BuildUi();

        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceDescription);
        _claimLabel.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceClaim);

        AttendanceStatus status = _service?.RefreshStatus();
        AttendanceRewardScheduleSO schedule = _service?.Schedule;
        RebuildRewardRows(schedule, status);
        RefreshStatus(status, schedule);
    }

    private void Awake()
    {
        BuildUi();
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
        if (_claimButton != null)
            _claimButton.onClick.RemoveListener(HandleClaimClicked);
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
        if (_backdropButton != null)
            _backdropButton.onClick.RemoveListener(Hide);
    }

    private void BuildUi()
    {
        if (_built)
            return;

        _backdropButton = GetComponent<Button>();
        _backdropButton.onClick.RemoveAllListeners();
        _backdropButton.onClick.AddListener(Hide);

        GameObject panel = GetOrCreate(
            transform,
            "grpAttendancePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(860f, 760f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        _titleText = CreateText(
            panel.transform,
            "txtAttendanceTitle",
            36f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        SetAnchoredRect(
            _titleText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(60f, -82f),
            new Vector2(-60f, -24f));

        _descriptionText = CreateText(
            panel.transform,
            "txtAttendanceDescription",
            19f,
            TextAlignmentOptions.Center);
        SetAnchoredRect(
            _descriptionText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(70f, -126f),
            new Vector2(-70f, -86f));

        _closeButton = CreateButton(
            panel.transform,
            "btnAttendanceClose",
            "×",
            new Color(0.16f, 0.19f, 0.165f, 1f),
            out _);
        RectTransform closeRect =
            (RectTransform)_closeButton.transform;
        closeRect.anchorMin = Vector2.one;
        closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-18f, -18f);
        closeRect.sizeDelta = new Vector2(48f, 48f);
        _closeButton.onClick.RemoveAllListeners();
        _closeButton.onClick.AddListener(Hide);

        GameObject scrollObject = GetOrCreate(
            panel.transform,
            "scrAttendanceRewards",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect));
        RectTransform scrollRect = (RectTransform)scrollObject.transform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(62f, 190f);
        scrollRect.offsetMax = new Vector2(-62f, -142f);
        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.16f);
        scrollImage.raycastTarget = true;

        GameObject viewport = GetOrCreate(
            scrollObject.transform,
            "vptAttendanceRewards",
            typeof(RectTransform),
            typeof(RectMask2D));
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -8f);

        GameObject content = GetOrCreate(
            viewport.transform,
            "grpAttendanceRewardContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        _listContent = (RectTransform)content.transform;
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.anchoredPosition = Vector2.zero;
        _listContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout =
            content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = _listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        _statusText = CreateText(
            panel.transform,
            "txtAttendanceStatus",
            20f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        SetAnchoredRect(
            _statusText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(60f, 132f),
            new Vector2(-60f, 174f));

        _resetText = CreateText(
            panel.transform,
            "txtAttendanceReset",
            16f,
            TextAlignmentOptions.Center);
        _resetText.color = new Color(0.72f, 0.72f, 0.65f, 1f);
        SetAnchoredRect(
            _resetText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(60f, 101f),
            new Vector2(-60f, 130f));

        _claimButton = CreateButton(
            panel.transform,
            "btnAttendanceClaim",
            string.Empty,
            new Color(0.24f, 0.36f, 0.27f, 1f),
            out _claimLabel);
        RectTransform claimRect =
            (RectTransform)_claimButton.transform;
        claimRect.anchorMin = new Vector2(0.5f, 0f);
        claimRect.anchorMax = new Vector2(0.5f, 0f);
        claimRect.pivot = new Vector2(0.5f, 0f);
        claimRect.anchoredPosition = new Vector2(0f, 30f);
        claimRect.sizeDelta = new Vector2(340f, 62f);
        _claimButton.onClick.RemoveAllListeners();
        _claimButton.onClick.AddListener(HandleClaimClicked);

        _built = true;
    }

    private void RebuildRewardRows(
        AttendanceRewardScheduleSO schedule,
        AttendanceStatus status)
    {
        for (int index = _listContent.childCount - 1; index >= 0; index--)
        {
            GameObject child = _listContent.GetChild(index).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            }
            else
                DestroyImmediate(child);
        }

        if (schedule == null)
            return;

        int claimedInCycle = status?.ClaimedInDisplayedCycle ?? 0;
        int todayIndex = status?.RewardIndex ?? -1;
        for (int index = 0; index < schedule.DayCount; index++)
        {
            bool claimed = index < claimedInCycle;
            bool today = index == todayIndex;
            BuildRewardRow(
                index,
                schedule.GetDay(index),
                claimed,
                today,
                status?.Availability ?? AttendanceAvailability.NotReady);
        }
    }

    private void BuildRewardRow(
        int index,
        AttendanceDayReward day,
        bool claimed,
        bool today,
        AttendanceAvailability availability)
    {
        GameObject row = GetOrCreate(
            _listContent,
            $"grpAttendanceDay_{index + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 68f;
        row.GetComponent<Image>().color = claimed
            ? ClaimedColor
            : today
                ? TodayColor
                : CardColor;

        TextMeshProUGUI dayText = CreateText(
            row.transform,
            "txtDay",
            20f,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold);
        dayText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceDay,
            LocalizationService.Arg("day", index + 1));
        SetAnchoredRect(
            dayText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.24f, 1f),
            new Vector2(18f, 4f),
            new Vector2(-4f, -4f));

        TextMeshProUGUI rewardText = CreateText(
            row.transform,
            "txtReward",
            18f,
            TextAlignmentOptions.MidlineLeft);
        rewardText.text = FormatReward(day);
        SetAnchoredRect(
            rewardText.rectTransform,
            new Vector2(0.24f, 0f),
            new Vector2(0.76f, 1f),
            new Vector2(4f, 4f),
            new Vector2(-4f, -4f));

        TextMeshProUGUI stateText = CreateText(
            row.transform,
            "txtState",
            16f,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Bold);
        stateText.color = claimed || today ? AccentColor : TextColor;
        if (claimed)
        {
            stateText.text = LocalizationService.Get(
                LocalizationKeys.UiAttendanceClaimed);
        }
        else if (today &&
                 availability == AttendanceAvailability.ClaimedToday)
        {
            stateText.text = LocalizationService.Get(
                LocalizationKeys.UiAttendanceClaimed);
        }
        else if (today)
        {
            stateText.text = LocalizationService.Get(
                LocalizationKeys.UiAttendanceToday);
        }
        else
        {
            stateText.text = LocalizationService.Get(
                LocalizationKeys.UiAttendanceUpcoming);
        }
        SetAnchoredRect(
            stateText.rectTransform,
            new Vector2(0.76f, 0f),
            new Vector2(1f, 1f),
            new Vector2(4f, 4f),
            new Vector2(-18f, -4f));
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
            AttendanceAvailability.ScheduleCompleted =>
                LocalizationKeys.UiAttendanceScheduleComplete,
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
        _statusText.text = LocalizationService.Get(key);
        _statusText.color = availability == AttendanceAvailability.Claimable
            ? AccentColor
            : TextColor;
        _claimButton.interactable =
            availability == AttendanceAvailability.Claimable;

        if (status == null || schedule == null ||
            status.NextResetUtc == default)
        {
            _resetText.text = string.Empty;
            return;
        }

        DateTimeOffset resetLocal = status.NextResetUtc.ToOffset(
            TimeSpan.FromMinutes(schedule.ResetUtcOffsetMinutes));
        _resetText.text = LocalizationService.Get(
            LocalizationKeys.UiAttendanceNextReset,
            LocalizationService.Arg(
                "time",
                resetLocal.ToString("yyyy-MM-dd HH:mm")));
    }

    private void HandleClaimClicked()
    {
        AttendanceClaimResult result = _service?.TryClaimToday();
        Refresh();
        if (result != null && result.Success)
        {
            _statusText.text = LocalizationService.Get(
                LocalizationKeys.UiAttendanceClaimSuccess,
                LocalizationService.Arg(
                    "reward",
                    FormatReward(result.Reward)));
        }
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        Refresh();
    }

    private static string FormatReward(AttendanceDayReward day)
    {
        if (day == null || day.Rewards == null)
            return "-";

        List<string> labels = new();
        for (int index = 0; index < day.Rewards.Count; index++)
        {
            AttendanceItemReward reward = day.Rewards[index];
            if (reward?.Item == null)
                continue;
            labels.Add(
                $"{reward.Item.GetLocalizedDisplayName()} × {reward.Amount:N0}");
        }
        return labels.Count > 0
            ? string.Join("  /  ", labels)
            : "-";
    }

    private static GameObject GetOrCreate(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject result;
        if (existing != null)
        {
            result = existing.gameObject;
            for (int index = 0; index < componentTypes.Length; index++)
            {
                Type type = componentTypes[index];
                if (result.GetComponent(type) == null)
                    result.AddComponent(type);
            }
        }
        else
        {
            result = new GameObject(objectName, componentTypes);
            result.transform.SetParent(parent, false);
        }
        return result;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = TextColor;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Color color,
        out TextMeshProUGUI labelText)
    {
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = result.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Color.Lerp(color, Color.black, 0.48f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        labelText = CreateText(
            result.transform,
            "txtLabel",
            22f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        labelText.text = label;
        Stretch(labelText.rectTransform);
        labelText.rectTransform.offsetMin = new Vector2(8f, 4f);
        labelText.rectTransform.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

[DisallowMultipleComponent]
public sealed class LobbyNoticePopupView : MonoBehaviour
{
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _message;
    private Button _close;
    private bool _built;

    public static LobbyNoticePopupView BuildOrBind(RectTransform parent)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find("grpMainNoticePopup");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject(
                "grpMainNoticePopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
        if (existing == null)
            root.transform.SetParent(parent, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color =
            new Color(0.01f, 0.015f, 0.012f, 0.86f);

        LobbyNoticePopupView view =
            root.GetComponent<LobbyNoticePopupView>() ??
            root.AddComponent<LobbyNoticePopupView>();
        view.BuildUi();
        root.SetActive(false);
        return view;
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

    private void Awake()
    {
        BuildUi();
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

    private void BuildUi()
    {
        if (_built)
            return;

        Button backdrop = GetComponent<Button>();
        backdrop.onClick.RemoveAllListeners();
        backdrop.onClick.AddListener(Hide);

        GameObject panel = AttendancePopupViewHelper.GetOrCreate(
            transform,
            "grpMainNoticePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(700f, 400f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.065f, 0.085f, 0.072f, 0.99f);
        panelImage.raycastTarget = true;

        _title = AttendancePopupViewHelper.CreateText(
            panel.transform,
            "txtMainNoticeTitle",
            34f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        AttendancePopupViewHelper.SetRect(
            _title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(56f, -86f),
            new Vector2(-56f, -28f));

        _message = AttendancePopupViewHelper.CreateText(
            panel.transform,
            "txtMainNoticeMessage",
            22f,
            TextAlignmentOptions.Center);
        AttendancePopupViewHelper.SetRect(
            _message.rectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(60f, 80f),
            new Vector2(-60f, -112f));

        _close = AttendancePopupViewHelper.CreateButton(
            panel.transform,
            "btnMainNoticeClose",
            "OK",
            out _);
        RectTransform closeRect = (RectTransform)_close.transform;
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);
        closeRect.sizeDelta = new Vector2(220f, 58f);
        _close.onClick.RemoveAllListeners();
        _close.onClick.AddListener(Hide);
        _built = true;
    }

    private void Refresh()
    {
        if (!_built)
            BuildUi();
        _title.text = LocalizationService.Get(
            LocalizationKeys.UiTitleNotice);
        _message.text = LocalizationService.Get(
            LocalizationKeys.UiTitleNoticeEmpty);
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        Refresh();
    }
}

internal static class AttendancePopupViewHelper
{
    public static GameObject GetOrCreate(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent.Find(objectName);
        GameObject result = existing != null
            ? existing.gameObject
            : new GameObject(objectName, componentTypes);
        if (existing == null)
            result.transform.SetParent(parent, false);
        for (int index = 0; index < componentTypes.Length; index++)
        {
            if (result.GetComponent(componentTypes[index]) == null)
                result.AddComponent(componentTypes[index]);
        }
        return result;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        out TextMeshProUGUI labelText)
    {
        Color color = new(0.24f, 0.36f, 0.27f, 1f);
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = result.GetComponent<Button>();
        button.targetGraphic = image;
        labelText = CreateText(
            result.transform,
            "txtLabel",
            22f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        labelText.text = label;
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = new Vector2(8f, 4f);
        labelText.rectTransform.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    public static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
