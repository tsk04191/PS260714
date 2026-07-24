using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EBattleCardCodexCategory
{
    Skills,
    Items,
}

[DisallowMultipleComponent]
public sealed class BattleCardCodexPage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject codexPage;

    [Header("Catalog Category")]
    [SerializeField] private EBattleCardCodexCategory category;

    private readonly List<BattleItemDefinition> _entries = new();
    private readonly List<Button> _tabButtons = new();
    private Image _detailPanelImage;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _classificationText;
    private TextMeshProUGUI _resourceText;
    private TextMeshProUGUI _effectTitleText;
    private TextMeshProUGUI _effectText;
    private TextMeshProUGUI _usageText;
    private Button _backButton;
    private int _selectedIndex;

    protected override string PageTitle => category ==
        EBattleCardCodexCategory.Skills
            ? LocalizationService.Get(LocalizationKeys.CodexSkillTitle)
            : LocalizationService.Get(LocalizationKeys.CodexItemTitle);

    protected override string PageDescription => category ==
        EBattleCardCodexCategory.Skills
            ? LocalizationService.Get(
                LocalizationKeys.CodexSkillDescription)
            : LocalizationService.Get(
                LocalizationKeys.CodexItemDescription);

    protected override Vector2 PanelSize => new(1120f, 820f);

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildTabStrip();
        BuildDetailPanel();
        _backButton = CreateStyledButton(
            ButtonRoot,
            "btnBACKTOCODEX",
            LocalizationService.Get(LocalizationKeys.CodexBattleBack),
            HandleBackClicked,
            72f);

        if (_entries.Count > 0)
        {
            SelectEntry(Mathf.Clamp(
                _selectedIndex,
                0,
                _entries.Count - 1));
        }
        else
        {
            ShowEmptyState();
        }
    }

    private void RefreshEntries()
    {
        _entries.Clear();
        Array values = Enum.GetValues(typeof(EBattleItemType));
        foreach (EBattleItemType type in values)
        {
            BattleItemDefinition definition = BattleItemCatalog.Get(type);
            bool categoryMatches = category ==
                EBattleCardCodexCategory.Skills
                    ? definition.IsReusable
                    : !definition.IsReusable;
            if (categoryMatches)
                _entries.Add(definition);
        }

        _entries.Sort((left, right) => string.Compare(
            left.DisplayName,
            right.DisplayName,
            StringComparison.OrdinalIgnoreCase));
    }

    private void BuildTabStrip()
    {
        GameObject tabStripObject = GetOrCreateChild(
            ButtonRoot,
            "grpBattleCardTabStrip",
            typeof(RectTransform),
            typeof(ScrollRect),
            typeof(LayoutElement));
        tabStripObject.GetComponent<LayoutElement>().preferredHeight = 70f;

        GameObject viewportObject = GetOrCreateChild(
            tabStripObject.transform,
            "vptBattleCardTabs",
            typeof(RectTransform),
            typeof(RectMask2D));
        StretchToParent((RectTransform)viewportObject.transform);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpBattleCardTabContent",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)contentObject.transform;
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup layout =
            contentObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        ContentSizeFitter fitter =
            contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = tabStripObject.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewportObject.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 28f;

        _tabButtons.Clear();
        for (int index = 0; index < _entries.Count; index++)
        {
            int selectedIndex = index;
            Button button = CreateStyledButton(
                contentObject.transform,
                $"btnBattleCardTab_{index}",
                _entries[index].DisplayName,
                () => SelectEntry(selectedIndex),
                60f);
            LayoutElement buttonLayout = button.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 168f;
            buttonLayout.preferredWidth = 210f;
            buttonLayout.flexibleWidth = 0f;
            _tabButtons.Add(button);
        }

        SyncIndexedChildren(
            contentObject.transform,
            "btnBattleCardTab_",
            _entries.Count);
    }

    private void BuildDetailPanel()
    {
        GameObject detailObject = GetOrCreateChild(
            ButtonRoot,
            "grpBattleCardDetail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _detailPanelImage = detailObject.GetComponent<Image>();
        _detailPanelImage.color = PanelColor;
        _detailPanelImage.raycastTarget = false;

        LayoutElement detailLayout = detailObject.GetComponent<LayoutElement>();
        detailLayout.preferredHeight = 390f;
        detailLayout.flexibleHeight = 1f;

        VerticalLayoutGroup layout =
            detailObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 22, 22);
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _detailTitle = CreateContentText(
            detailObject.transform,
            "txtBattleCardName",
            string.Empty,
            38f,
            54f,
            FontStyles.Bold);
        _classificationText = CreateContentText(
            detailObject.transform,
            "txtBattleCardClassification",
            string.Empty,
            20f,
            38f,
            FontStyles.Bold);
        _resourceText = CreateContentText(
            detailObject.transform,
            "txtBattleCardResource",
            string.Empty,
            21f,
            52f);
        _effectTitleText = CreateContentText(
            detailObject.transform,
            "txtBattleCardEffectTitle",
            LocalizationService.Get(
                LocalizationKeys.CodexBattleEffectTitle),
            19f,
            28f,
            FontStyles.Bold);
        _effectText = CreateContentText(
            detailObject.transform,
            "txtBattleCardEffect",
            string.Empty,
            21f,
            70f);
        _usageText = CreateContentText(
            detailObject.transform,
            "txtBattleCardUsage",
            string.Empty,
            18f,
            62f);
    }

    private void SelectEntry(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;

        _selectedIndex = index;
        BattleItemDefinition definition = _entries[index];
        Color accentColor = definition.IsReusable
            ? new Color(0.24f, 0.52f, 0.7f, 1f)
            : new Color(0.72f, 0.4f, 0.18f, 1f);
        for (int buttonIndex = 0;
             buttonIndex < _tabButtons.Count;
             buttonIndex++)
        {
            SetButtonColor(
                _tabButtons[buttonIndex],
                buttonIndex == index ? accentColor : ButtonColor);
        }

        if (_detailPanelImage != null)
        {
            _detailPanelImage.color = Color.Lerp(
                PanelColor,
                accentColor,
                0.18f);
        }

        _detailTitle.text = definition.DisplayName;
        _classificationText.text = LocalizationService.Get(
            definition.IsReusable
                ? LocalizationKeys.CodexBattleClassificationReusable
                : LocalizationKeys.CodexBattleClassificationConsumable);

        LocalizationArgument cost = LocalizationService.Arg(
            "cost",
            definition.EnergyCost);
        LocalizationArgument target = LocalizationService.Arg(
            "target",
            GetTargetName(definition.TargetType));
        _resourceText.text = definition.IsReusable
            ? LocalizationService.Get(
                LocalizationKeys.CodexBattleResourceReusable,
                cost,
                target,
                LocalizationService.Arg(
                    "cooldown",
                    definition.Cooldown))
            : LocalizationService.Get(
                LocalizationKeys.CodexBattleResourceConsumable,
                cost,
                target);
        _effectText.text = definition.Description;
        _usageText.text = LocalizationService.Get(
            definition.IsReusable
                ? LocalizationKeys.CodexBattleUsageReusable
                : LocalizationKeys.CodexBattleUsageConsumable);

        ApplyLocalizedFont(_detailTitle, "title");
        ApplyLocalizedFont(_classificationText, "body");
        ApplyLocalizedFont(_resourceText, "number");
        ApplyLocalizedFont(_effectText, "tooltip");
        ApplyLocalizedFont(_usageText, "body");
    }

    private void ShowEmptyState()
    {
        if (_detailTitle != null)
            _detailTitle.text = LocalizationService.Get(
                LocalizationKeys.CodexBattleEmptyTitle);
        if (_classificationText != null)
            _classificationText.text = string.Empty;
        if (_resourceText != null)
            _resourceText.text = string.Empty;
        if (_effectText != null)
            _effectText.text = LocalizationService.Get(
                LocalizationKeys.CodexBattleEmptyEffect);
        if (_usageText != null)
            _usageText.text = string.Empty;
    }

    private static string GetTargetName(EBattleItemTargetType targetType)
    {
        return LocalizationService.Get(
            targetType == EBattleItemTargetType.Turret
                ? LocalizationKeys.CodexBattleTargetTurret
                : LocalizationKeys.CodexBattleTargetEnemy);
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        LocalizationService.FontChanged += HandleFontChanged;
        RefreshLocalizedView();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        LocalizationService.FontChanged -= HandleFontChanged;
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshLocalizedView();
    }

    private void HandleFontChanged(string unusedFontId)
    {
        RefreshLocalizedView();
    }

    private void RefreshLocalizedView()
    {
        if (_detailTitle == null)
            return;

        RefreshEntries();
        BuildTabStrip();
        for (int index = 0;
             index < _tabButtons.Count && index < _entries.Count;
             index++)
        {
            TextMeshProUGUI label = _tabButtons[index]
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = _entries[index].DisplayName;
        }

        Transform runtimeRoot = transform.Find(RuntimeRootObjectName);
        Transform panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel")
            : null;
        if (panel != null)
        {
            TextMeshProUGUI title = panel.Find("txtPageTitle")
                ?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI description = panel.Find("txtPageDescription")
                ?.GetComponent<TextMeshProUGUI>();
            if (title != null)
                title.text = PageTitle;
            if (description != null)
                description.text = PageDescription;
        }

        if (_effectTitleText != null)
        {
            _effectTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexBattleEffectTitle);
        }

        if (_backButton != null)
        {
            TextMeshProUGUI backLabel = _backButton
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (backLabel != null)
            {
                backLabel.text = LocalizationService.Get(
                    LocalizationKeys.CodexBattleBack);
            }
        }

        if (_entries.Count > 0)
        {
            SelectEntry(Mathf.Clamp(
                _selectedIndex,
                0,
                _entries.Count - 1));
        }
        else
        {
            ShowEmptyState();
        }
    }

    private static void ApplyLocalizedFont(
        TMP_Text text,
        string fontRole)
    {
        LocalizationFontResolver.Current?.Apply(text, fontRole);
    }

    private void HandleBackClicked()
    {
        NavigateTo(codexPage, PageOpenMode.Resume);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
            return;

        if (button.targetGraphic is Image image)
            image.color = color;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Color.Lerp(color, Color.black, 0.5f);
        button.colors = colors;
    }

    private static GameObject GetOrCreateChild(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        if (existing != null)
            return existing.gameObject;

        GameObject child = new(objectName, componentTypes);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
