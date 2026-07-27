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
    private readonly List<BattleItemDefinition> _visibleEntries = new();
    private CodexBrowserView _browser;
    private Image _detailPanelImage;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _classificationText;
    private TextMeshProUGUI _resourceText;
    private TextMeshProUGUI _effectTitleText;
    private TextMeshProUGUI _effectText;
    private TextMeshProUGUI _usageText;
    private Button _backButton;
    private string _selectedEntryId;
    private string _searchQuery = string.Empty;
    private int _filterIndex;
    private int _sortIndex;

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
    protected override bool FillAvailableSpace => true;

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildBrowser();
        BuildDetailPanel(_browser.DetailRoot);
        _backButton = CreateLocalizedTopLeftOverlayMenuButton(
            "btnBACKTOCODEX",
            LocalizationKeys.CodexBattleBack,
            HandleBackClicked);
        RefreshBrowser();
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

    private void BuildBrowser()
    {
        _browser = CodexBrowserView.Build(ButtonRoot);
        _browser.HideLegacyList("grpBattleCardTabStrip");
        _browser.AdoptExistingDetail("grpBattleCardDetail");
        _browser.SetCallbacks(
            query =>
            {
                _searchQuery = (query ?? string.Empty).Trim();
                RefreshBrowser();
            },
            () =>
            {
                _filterIndex = (_filterIndex + 1) % 3;
                RefreshBrowser();
            },
            () =>
            {
                _sortIndex = (_sortIndex + 1) % 3;
                RefreshBrowser();
            },
            SelectEntry);
        RefreshBrowserToolbar();
    }

    private void BuildDetailPanel(Transform parent)
    {
        bool created = parent.Find("grpBattleCardDetail") == null;
        GameObject detailObject = GetOrCreateChild(
            parent,
            "grpBattleCardDetail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _detailPanelImage = detailObject.GetComponent<Image>();
        if (created)
        {
            _detailPanelImage.color = PanelColor;
            _detailPanelImage.raycastTarget = false;

            LayoutElement detailLayout =
                detailObject.GetComponent<LayoutElement>();
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
        }

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

    private void RefreshBrowser()
    {
        if (_browser == null)
            return;

        RefreshBrowserToolbar();
        _visibleEntries.Clear();
        foreach (BattleItemDefinition entry in _entries)
        {
            if (!MatchesSearch(entry) || !MatchesFilter(entry))
                continue;

            _visibleEntries.Add(entry);
        }

        _visibleEntries.Sort(CompareEntries);
        List<CodexBrowserItemModel> items =
            new(_visibleEntries.Count);
        foreach (BattleItemDefinition entry in _visibleEntries)
        {
            Color accent = entry.IsReusable
                ? new Color(0.24f, 0.52f, 0.7f, 1f)
                : new Color(0.72f, 0.4f, 0.18f, 1f);
            items.Add(new CodexBrowserItemModel(
                GetEntryId(entry),
                entry.DisplayName,
                null,
                false,
                accent));
        }

        bool selectionVisible = _visibleEntries.Exists(entry =>
            string.Equals(
                GetEntryId(entry),
                _selectedEntryId,
                StringComparison.Ordinal));
        if (!selectionVisible)
        {
            _selectedEntryId = _visibleEntries.Count > 0
                ? GetEntryId(_visibleEntries[0])
                : string.Empty;
        }

        _browser.SetItems(items, _selectedEntryId);
        if (_visibleEntries.Count > 0)
            SelectEntry(_selectedEntryId);
        else
            ShowEmptyState();
    }

    private bool MatchesSearch(BattleItemDefinition entry)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        return ContainsIgnoreCase(entry.DisplayName, _searchQuery) ||
               ContainsIgnoreCase(entry.Type.ToString(), _searchQuery) ||
               ContainsIgnoreCase(entry.Description, _searchQuery);
    }

    private bool MatchesFilter(BattleItemDefinition entry)
    {
        return _filterIndex switch
        {
            1 => entry.TargetType == EBattleItemTargetType.Enemy,
            2 => entry.TargetType != EBattleItemTargetType.Enemy,
            _ => true
        };
    }

    private int CompareEntries(
        BattleItemDefinition left,
        BattleItemDefinition right)
    {
        int primary;
        if (_sortIndex == 2)
        {
            primary = left.EnergyCost.CompareTo(right.EnergyCost);
        }
        else
        {
            primary = string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);
            if (_sortIndex == 1)
                primary = -primary;
        }

        return primary != 0
            ? primary
            : left.Type.CompareTo(right.Type);
    }

    private void RefreshBrowserToolbar()
    {
        if (_browser == null)
            return;

        string filter = _filterIndex switch
        {
            1 => IsKoreanLocale ? "필터: 적" : "FILTER: ENEMY",
            2 => IsKoreanLocale ? "필터: 기타" : "FILTER: OTHER",
            _ => IsKoreanLocale ? "필터: 전체" : "FILTER: ALL"
        };
        string sort = _sortIndex switch
        {
            1 => IsKoreanLocale ? "정렬: 이름↓" : "SORT: NAME↓",
            2 => IsKoreanLocale ? "정렬: 비용" : "SORT: COST",
            _ => IsKoreanLocale ? "정렬: 이름↑" : "SORT: NAME↑"
        };
        _browser.SetToolbar(
            _searchQuery,
            IsKoreanLocale ? "이름 또는 유형" : "NAME OR TYPE",
            IsKoreanLocale ? "검색" : "SEARCH",
            filter,
            sort);
    }

    private void SelectEntry(string entryId)
    {
        int index = _visibleEntries.FindIndex(entry => string.Equals(
            GetEntryId(entry),
            entryId,
            StringComparison.Ordinal));
        if (index < 0)
            return;

        _selectedEntryId = entryId;
        _browser?.SetSelected(entryId);
        BattleItemDefinition definition = _visibleEntries[index];
        Color accentColor = definition.IsReusable
            ? new Color(0.24f, 0.52f, 0.7f, 1f)
            : new Color(0.72f, 0.4f, 0.18f, 1f);

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
        RefreshBrowser();

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

    private static string GetEntryId(BattleItemDefinition entry)
    {
        return entry.Type.ToString();
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(
                   value,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;

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
