using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AttendancePresentationPrefabBootstrap
{
    private const string PresentationRoot =
        "Assets/Resources/Presentation";
    private const string CellPath =
        PresentationRoot + "/AttendanceRewardCell.prefab";
    private const string PopupPath =
        PresentationRoot + "/AttendancePopup.prefab";
    private const string DotPath =
        PresentationRoot + "/NotificationDot.prefab";
    private const string ScheduleRoot =
        "Assets/Resources/Attendance";
    private const string SchedulePath =
        ScheduleRoot + "/DefaultAttendanceSchedule.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleEnsureAssets()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EnsureAssets();
        };
    }

    [MenuItem("Tools/PS260714/UI/Ensure Attendance Prefabs")]
    public static void EnsureAssets()
    {
        EnsureFolder("Assets/Resources", "Presentation");
        EnsureFolder("Assets/Resources", "Attendance");

        bool changed = false;
        NotificationDotView dot = AssetDatabase
            .LoadAssetAtPath<NotificationDotView>(DotPath);
        if (dot == null)
        {
            dot = CreateNotificationDotPrefab();
            changed = dot != null;
        }

        AttendanceRewardCellView cell = AssetDatabase
            .LoadAssetAtPath<AttendanceRewardCellView>(CellPath);
        if (cell == null)
        {
            cell = CreateRewardCellPrefab();
            changed |= cell != null;
        }

        if (cell != null && AssetDatabase
                .LoadAssetAtPath<MonthlyAttendancePopupView>(PopupPath) == null)
        {
            changed |= CreatePopupPrefab(cell) != null;
        }

        if (AssetDatabase.LoadAssetAtPath<AttendanceRewardScheduleSO>(
                SchedulePath) == null)
        {
            changed |= CreateDefaultSchedule() != null;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static NotificationDotView CreateNotificationDotPrefab()
    {
        GameObject root = new(
            "NotificationDot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(NotificationDotView));
        NotificationDotView dot = root.GetComponent<NotificationDotView>();
        dot.color = new Color(0.95f, 0.08f, 0.06f, 1f);
        dot.raycastTarget = false;
        ((RectTransform)root.transform).sizeDelta = new Vector2(18f, 18f);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DotPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab != null
            ? prefab.GetComponent<NotificationDotView>()
            : null;
    }

    private static AttendanceRewardCellView CreateRewardCellPrefab()
    {
        GameObject root = CreateImage(
            null,
            "AttendanceRewardCell",
            new Color(0.12f, 0.155f, 0.13f, 1f));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(104f, 104f);
        AttendanceRewardCellView view =
            root.AddComponent<AttendanceRewardCellView>();

        Image icon = CreateImage(
            root.transform,
            "imgRewardIcon",
            Color.white).GetComponent<Image>();
        Stretch(icon.rectTransform, 10f);
        icon.preserveAspect = true;

        TextMeshProUGUI amount = CreateText(
            root.transform,
            "txtRewardAmount",
            17f,
            TextAlignmentOptions.BottomRight,
            FontStyles.Bold);
        Stretch(amount.rectTransform, 6f);

        TextMeshProUGUI additional = CreateText(
            root.transform,
            "txtAdditionalRewards",
            16f,
            TextAlignmentOptions.TopRight,
            FontStyles.Bold);
        Stretch(additional.rectTransform, 6f);

        Image overlay = CreateImage(
            root.transform,
            "imgClaimedOverlay",
            new Color(0f, 0f, 0f, 0.68f)).GetComponent<Image>();
        Stretch(overlay.rectTransform, 0f);
        overlay.raycastTarget = false;

        GameObject border = CreateBorder(root.transform);
        border.SetActive(false);

        GameObject tooltip = CreateImage(
            root.transform,
            "grpRewardTooltip",
            new Color(0.035f, 0.055f, 0.045f, 0.99f));
        RectTransform tooltipRect = (RectTransform)tooltip.transform;
        tooltipRect.anchorMin = new Vector2(1f, 0.5f);
        tooltipRect.anchorMax = new Vector2(1f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 0.5f);
        tooltipRect.anchoredPosition = new Vector2(12f, 0f);
        tooltipRect.sizeDelta = new Vector2(310f, 120f);
        TextMeshProUGUI tooltipText = CreateText(
            tooltip.transform,
            "txtRewardTooltip",
            17f,
            TextAlignmentOptions.MidlineLeft);
        Stretch(tooltipText.rectTransform, 12f);
        tooltip.SetActive(false);

        SerializedObject serialized = new(view);
        SetObject(serialized, "rewardIcon", icon);
        SetObject(serialized, "amountText", amount);
        SetObject(serialized, "additionalCountText", additional);
        SetObject(serialized, "claimedOverlay", overlay);
        SetObject(serialized, "todayBorder", border);
        SetObject(serialized, "tooltip", tooltip);
        SetObject(serialized, "tooltipText", tooltipText);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CellPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab != null
            ? prefab.GetComponent<AttendanceRewardCellView>()
            : null;
    }

    private static MonthlyAttendancePopupView CreatePopupPrefab(
        AttendanceRewardCellView cellPrefab)
    {
        GameObject root = CreateImage(
            null,
            "AttendancePopup",
            new Color(0.01f, 0.015f, 0.012f, 0.86f));
        Stretch((RectTransform)root.transform, 0f);
        Button backdrop = root.AddComponent<Button>();
        backdrop.targetGraphic = root.GetComponent<Image>();
        MonthlyAttendancePopupView view =
            root.AddComponent<MonthlyAttendancePopupView>();

        GameObject panel = CreateImage(
            root.transform,
            "grpAttendancePanel",
            new Color(0.065f, 0.085f, 0.072f, 0.99f));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(980f, 900f);

        TextMeshProUGUI title = CreateText(
            panel.transform,
            "txtAttendanceTitle",
            36f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(70f, -72f), new Vector2(-70f, -24f));
        TextMeshProUGUI description = CreateText(
            panel.transform,
            "txtAttendanceDescription",
            18f,
            TextAlignmentOptions.Center);
        SetRect(description.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(70f, -112f), new Vector2(-70f, -76f));
        TextMeshProUGUI month = CreateText(
            panel.transform,
            "txtAttendanceMonth",
            24f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        SetRect(month.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(70f, -150f), new Vector2(-70f, -116f));

        Button close = CreateButton(
            panel.transform,
            "btnAttendanceClose",
            "×",
            new Color(0.16f, 0.19f, 0.165f, 1f),
            out _);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-18f, -18f);
        closeRect.sizeDelta = new Vector2(48f, 48f);

        GameObject calendar = new(
            "grpAttendanceCalendar",
            typeof(RectTransform),
            typeof(GridLayoutGroup));
        calendar.transform.SetParent(panel.transform, false);
        RectTransform calendarRect = (RectTransform)calendar.transform;
        calendarRect.anchorMin = calendarRect.anchorMax =
            new Vector2(0.5f, 1f);
        calendarRect.pivot = new Vector2(0.5f, 1f);
        calendarRect.anchoredPosition = new Vector2(0f, -164f);
        calendarRect.sizeDelta = new Vector2(788f, 446f);
        GridLayoutGroup grid = calendar.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(104f, 104f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        GameObject extraRoot = CreateImage(
            panel.transform,
            "grpAttendanceExtraReward",
            new Color(0.09f, 0.12f, 0.1f, 0.9f));
        RectTransform extraRect = (RectTransform)extraRoot.transform;
        extraRect.anchorMin = extraRect.anchorMax = new Vector2(0.5f, 0f);
        extraRect.pivot = new Vector2(0.5f, 0f);
        extraRect.anchoredPosition = new Vector2(0f, 150f);
        extraRect.sizeDelta = new Vector2(360f, 112f);
        TextMeshProUGUI extraLabel = CreateText(
            extraRoot.transform,
            "txtExtraRewardLabel",
            16f,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold);
        SetRect(extraLabel.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(12f, 8f), new Vector2(-124f, -8f));
        extraLabel.text = "29-31";

        GameObject extraCellObject = (GameObject)PrefabUtility
            .InstantiatePrefab(cellPrefab.gameObject);
        extraCellObject.transform.SetParent(extraRoot.transform, false);
        RectTransform extraCellRect =
            (RectTransform)extraCellObject.transform;
        extraCellRect.anchorMin = extraCellRect.anchorMax =
            new Vector2(1f, 0.5f);
        extraCellRect.pivot = new Vector2(1f, 0.5f);
        extraCellRect.anchoredPosition = new Vector2(-4f, 0f);
        extraCellRect.sizeDelta = new Vector2(104f, 104f);
        AttendanceRewardCellView extraCell =
            extraCellObject.GetComponent<AttendanceRewardCellView>();

        TextMeshProUGUI status = CreateText(
            panel.transform,
            "txtAttendanceStatus",
            18f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        SetRect(status.rectTransform, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(60f, 105f), new Vector2(-60f, 140f));
        TextMeshProUGUI reset = CreateText(
            panel.transform,
            "txtAttendanceReset",
            15f,
            TextAlignmentOptions.Center);
        SetRect(reset.rectTransform, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(60f, 78f), new Vector2(-60f, 104f));
        Button claim = CreateButton(
            panel.transform,
            "btnAttendanceClaim",
            string.Empty,
            new Color(0.24f, 0.36f, 0.27f, 1f),
            out TextMeshProUGUI claimLabel);
        RectTransform claimRect = (RectTransform)claim.transform;
        claimRect.anchorMin = claimRect.anchorMax = new Vector2(0.5f, 0f);
        claimRect.pivot = new Vector2(0.5f, 0f);
        claimRect.anchoredPosition = new Vector2(0f, 18f);
        claimRect.sizeDelta = new Vector2(340f, 54f);

        SerializedObject serialized = new(view);
        SetObject(serialized, "backdropButton", backdrop);
        SetObject(serialized, "closeButton", close);
        SetObject(serialized, "titleText", title);
        SetObject(serialized, "descriptionText", description);
        SetObject(serialized, "monthText", month);
        SetObject(serialized, "calendarRoot", calendarRect);
        SetObject(serialized, "rewardCellPrefab", cellPrefab);
        SetObject(serialized, "extraRewardRoot", extraRoot);
        SetObject(serialized, "extraRewardCell", extraCell);
        SetObject(serialized, "statusText", status);
        SetObject(serialized, "resetText", reset);
        SetObject(serialized, "claimButton", claim);
        SetObject(serialized, "claimLabel", claimLabel);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab != null
            ? prefab.GetComponent<MonthlyAttendancePopupView>()
            : null;
    }

    private static AttendanceRewardScheduleSO CreateDefaultSchedule()
    {
        ItemDefinitionSO soft = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
            "Assets/Resources/Items/Currency/SoftCredit.asset");
        ItemDefinitionSO material = AssetDatabase
            .LoadAssetAtPath<ItemDefinitionSO>(
                "Assets/Resources/Items/Material/" +
                "BasicUpgradeMaterial.asset");
        ItemDefinitionSO ticket = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
            "Assets/Resources/Items/Ticket/StandardRecruitTicket.asset");
        ItemDefinitionSO free = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
            "Assets/Resources/Items/Currency/FreeCredit.asset");
        if (soft == null || material == null || ticket == null || free == null)
            return null;

        AttendanceDayReward Day(ItemDefinitionSO item, long amount) =>
            new(new[] { new AttendanceItemReward(item, amount) });
        List<AttendanceDayReward> pattern = new()
        {
            Day(soft, 1000L), Day(material, 5L), Day(ticket, 1L),
            Day(soft, 1500L), Day(material, 10L), Day(free, 50L),
            Day(ticket, 2L),
        };
        AttendanceRewardScheduleSO schedule =
            AttendanceRewardScheduleSO.CreateRuntime(
                "default_attendance",
                2,
                true,
                540,
                0,
                pattern,
                Day(soft, 1000L));
        schedule.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(schedule, SchedulePath);
        return schedule;
    }

    private static GameObject CreateBorder(Transform parent)
    {
        GameObject root = new("grpTodayBorder", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform, 0f);
        const float thickness = 4f;
        Image top = CreateImage(root.transform, "imgTop", Color.white)
            .GetComponent<Image>();
        SetRect(top.rectTransform, new Vector2(0f, 1f), Vector2.one,
            Vector2.zero, new Vector2(0f, thickness));
        Image bottom = CreateImage(root.transform, "imgBottom", Color.white)
            .GetComponent<Image>();
        SetRect(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        Image left = CreateImage(root.transform, "imgLeft", Color.white)
            .GetComponent<Image>();
        SetRect(left.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        Image right = CreateImage(root.transform, "imgRight", Color.white)
            .GetComponent<Image>();
        SetRect(right.rectTransform, new Vector2(1f, 0f), Vector2.one,
            Vector2.zero, new Vector2(thickness, 0f));
        return root;
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject result = new(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        if (parent != null)
            result.transform.SetParent(parent, false);
        Image image = result.GetComponent<Image>();
        image.color = color;
        return result;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
        try
        {
            LocalizationFontResolver.ApplyGameDefault(text);
        }
        catch (NullReferenceException)
        {
            // A minimal batch-mode bootstrap project may not contain
            // TMP_Settings. The real project resolves the configured font
            // when the prefab is loaded or localized.
        }
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Color color,
        out TextMeshProUGUI labelText)
    {
        GameObject result = CreateImage(parent, name, color);
        Button button = result.AddComponent<Button>();
        button.targetGraphic = result.GetComponent<Image>();
        labelText = CreateText(
            result.transform,
            "txtLabel",
            20f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        labelText.text = label;
        Stretch(labelText.rectTransform, 6f);
        return button;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void SetObject(
        SerializedObject serialized,
        string name,
        UnityEngine.Object value)
    {
        serialized.FindProperty(name).objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    private static void SetRect(
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
