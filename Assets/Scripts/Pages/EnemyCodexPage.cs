using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyCodexPage : RuntimeMenuPageBase
{
    private readonly struct EnemyCodexEntry
    {
        public string EnemyId { get; }
        public string DisplayName { get; }
        public string CardCode { get; }
        public EEnemyGrade Grade { get; }
        public EEnemyType Type { get; }
        public int BaseHealth { get; }
        public float SpawnIntervalMultiplier { get; }
        public float ThreatCost { get; }
        public bool TargetPriorityExcluded { get; }
        public string AbilityDescription { get; }

        public EnemyCodexEntry(EnemySO definition)
        {
            EnemyId = definition.EnemyId;
            DisplayName = EnemyLocalization.GetName(definition);
            CardCode = definition.CardCode;
            Grade = definition.Grade;
            Type = definition.Type;
            BaseHealth = definition.BaseHealth;
            SpawnIntervalMultiplier = definition.SpawnIntervalMultiplier;
            ThreatCost = definition.ThreatCost;
            TargetPriorityExcluded =
                EnemyLocalization.HasTargetPriorityExclusion(definition);
            AbilityDescription = EnemyLocalization.GetAbility(definition);
        }
    }

    [Header("Page Navigation")]
    [SerializeField] private GameObject codexPage;
    [SerializeField] private GameObject dungeonPage;

    [Header("Enemy Definitions")]
    [SerializeField] private EnemySO[] enemyDefinitions =
        Array.Empty<EnemySO>();

    private readonly List<EnemyCodexEntry> _entries = new();
    private readonly List<EnemyCodexEntry> _visibleEntries = new();
    private CodexBrowserView _browser;
    private Image _detailPanelImage;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _identityText;
    private TextMeshProUGUI _statText;
    private TextMeshProUGUI _abilityTitleText;
    private TextMeshProUGUI _abilityText;
    private Button _backButton;
    private string _selectedEnemyId;
    private string _searchQuery = string.Empty;
    private int _filterIndex;
    private int _sortIndex;

    protected override string PageTitle => LocalizationService.Get(
        LocalizationKeys.CodexEnemyTitle);
    protected override string PageDescription =>
        LocalizationService.Get(LocalizationKeys.CodexEnemyDescription);
    protected override Vector2 PanelSize => new(1220f, 860f);
    protected override bool FillAvailableSpace => true;

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildEnemyBrowser();
        BuildDetailPanel(_browser.DetailRoot);
        _backButton = CreateLocalizedTopLeftOverlayMenuButton(
            "btnBACKTOCODEX",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        RefreshBrowser();
    }

    private void RefreshEntries()
    {
        _entries.Clear();
        List<EnemySO> uniqueDefinitions = new();
        HashSet<EnemySO> registeredReferences = new();
        HashSet<string> registeredIds =
            new(StringComparer.OrdinalIgnoreCase);
        HashSet<EEnemyType> registeredTypes = new();
        AddDefinitions(
            enemyDefinitions,
            uniqueDefinitions,
            registeredReferences,
            registeredIds,
            registeredTypes);

        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            AddDefinitions(
                dungeon.GetCodexEnemyDefinitions(),
                uniqueDefinitions,
                registeredReferences,
                registeredIds,
                registeredTypes);
        }

        foreach (EnemySO definition in uniqueDefinitions)
        {
            _entries.Add(new EnemyCodexEntry(definition));
        }

        foreach (EEnemyType type in Enum.GetValues(typeof(EEnemyType)))
        {
            if (registeredTypes.Contains(type))
                continue;

            EnemySO fallback = EnemySO.CreateRuntimeDefault(type, 20);
            _entries.Add(new EnemyCodexEntry(fallback));
            ReleaseTemporaryDefinition(fallback);
        }

        _entries.Sort((left, right) =>
        {
            int typeOrder = left.Type.CompareTo(right.Type);
            return typeOrder != 0
                ? typeOrder
                : string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void AddDefinitions(
        IReadOnlyList<EnemySO> definitions,
        List<EnemySO> uniqueDefinitions,
        HashSet<EnemySO> registeredReferences,
        HashSet<string> registeredIds,
        HashSet<EEnemyType> registeredTypes)
    {
        if (definitions == null || uniqueDefinitions == null ||
            registeredReferences == null || registeredIds == null ||
            registeredTypes == null)
        {
            return;
        }

        foreach (EnemySO definition in definitions)
        {
            if (definition == null ||
                !registeredReferences.Add(definition))
            {
                continue;
            }

            string enemyId = definition.EnemyId?.Trim();
            if (!string.IsNullOrWhiteSpace(enemyId) &&
                registeredIds.Contains(enemyId))
            {
                continue;
            }

            if (registeredTypes.Contains(definition.Type))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(enemyId))
                registeredIds.Add(enemyId);
            registeredTypes.Add(definition.Type);
            uniqueDefinitions.Add(definition);
        }
    }

    private void BuildEnemyBrowser()
    {
        _browser = CodexBrowserView.Build(ButtonRoot);
        _browser.HideLegacyList("grpEnemyTabStrip");
        _browser.AdoptExistingDetail("grpEnemyDetail");
        _browser.SetCallbacks(
            query =>
            {
                _searchQuery = (query ?? string.Empty).Trim();
                RefreshBrowser();
            },
            () =>
            {
                _filterIndex = (_filterIndex + 1) % 5;
                RefreshBrowser();
            },
            () =>
            {
                _sortIndex = (_sortIndex + 1) % 3;
                RefreshBrowser();
            },
            SelectEnemy);
        RefreshBrowserToolbar();
    }

    private void BuildDetailPanel(Transform parent)
    {
        bool created = parent.Find("grpEnemyDetail") == null;
        GameObject detailObject = GetOrCreateChild(
            parent,
            "grpEnemyDetail",
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
            detailLayout.preferredHeight = 400f;
            detailLayout.flexibleHeight = 1f;

            VerticalLayoutGroup layout =
                detailObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        _detailTitle = CreateContentText(
            detailObject.transform,
            "txtEnemyName",
            string.Empty,
            38f,
            58f,
            FontStyles.Bold);
        _identityText = CreateContentText(
            detailObject.transform,
            "txtEnemyIdentity",
            string.Empty,
            20f,
            42f,
            FontStyles.Bold);
        _statText = CreateContentText(
            detailObject.transform,
            "txtEnemyStats",
            string.Empty,
            22f,
            92f);
        _abilityTitleText = CreateContentText(
            detailObject.transform,
            "txtAbilityTitle",
            LocalizationService.Get(LocalizationKeys.CodexEnemyAbility),
            20f,
            32f,
            FontStyles.Bold);
        _abilityText = CreateContentText(
            detailObject.transform,
            "txtEnemyAbility",
            string.Empty,
            21f,
            108f);
    }

    private void RefreshBrowser()
    {
        if (_browser == null)
            return;

        RefreshBrowserToolbar();
        _visibleEntries.Clear();
        foreach (EnemyCodexEntry entry in _entries)
        {
            if (!MatchesSearch(entry) || !MatchesFilter(entry))
                continue;

            _visibleEntries.Add(entry);
        }

        _visibleEntries.Sort(CompareEntries);
        List<CodexBrowserItemModel> items =
            new(_visibleEntries.Count);
        foreach (EnemyCodexEntry entry in _visibleEntries)
        {
            items.Add(new CodexBrowserItemModel(
                entry.EnemyId,
                entry.DisplayName,
                null,
                false,
                GetGradeColor(entry.Grade)));
        }

        bool selectionVisible = _visibleEntries.Exists(entry =>
            string.Equals(
                entry.EnemyId,
                _selectedEnemyId,
                StringComparison.Ordinal));
        if (!selectionVisible)
        {
            _selectedEnemyId = _visibleEntries.Count > 0
                ? _visibleEntries[0].EnemyId
                : string.Empty;
        }

        _browser.SetItems(items, _selectedEnemyId);
        if (_visibleEntries.Count > 0)
            SelectEnemy(_selectedEnemyId);
        else
            ShowEmptyState();
    }

    private bool MatchesSearch(EnemyCodexEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        return ContainsIgnoreCase(entry.DisplayName, _searchQuery) ||
               ContainsIgnoreCase(entry.EnemyId, _searchQuery) ||
               ContainsIgnoreCase(entry.CardCode, _searchQuery) ||
               ContainsIgnoreCase(entry.Type.ToString(), _searchQuery);
    }

    private bool MatchesFilter(EnemyCodexEntry entry)
    {
        return _filterIndex switch
        {
            1 => entry.Grade == EEnemyGrade.Normal,
            2 => entry.Grade == EEnemyGrade.Special,
            3 => entry.Grade == EEnemyGrade.Elite,
            4 => entry.Grade == EEnemyGrade.Boss,
            _ => true
        };
    }

    private int CompareEntries(
        EnemyCodexEntry left,
        EnemyCodexEntry right)
    {
        int primary = _sortIndex switch
        {
            1 => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase),
            2 => left.Grade.CompareTo(right.Grade),
            _ => left.Type.CompareTo(right.Type)
        };
        if (primary != 0)
            return primary;
        int name = string.Compare(
            left.DisplayName,
            right.DisplayName,
            StringComparison.OrdinalIgnoreCase);
        return name != 0
            ? name
            : string.Compare(
                left.EnemyId,
                right.EnemyId,
                StringComparison.Ordinal);
    }

    private void RefreshBrowserToolbar()
    {
        if (_browser == null)
            return;

        string filter = _filterIndex switch
        {
            1 => IsKoreanLocale ? "필터: 일반" : "FILTER: NORMAL",
            2 => IsKoreanLocale ? "필터: 특수" : "FILTER: SPECIAL",
            3 => IsKoreanLocale ? "필터: 정예" : "FILTER: ELITE",
            4 => IsKoreanLocale ? "필터: 보스" : "FILTER: BOSS",
            _ => IsKoreanLocale ? "필터: 전체" : "FILTER: ALL"
        };
        string sort = _sortIndex switch
        {
            1 => IsKoreanLocale ? "정렬: 이름" : "SORT: NAME",
            2 => IsKoreanLocale ? "정렬: 등급" : "SORT: GRADE",
            _ => IsKoreanLocale ? "정렬: 유형" : "SORT: TYPE"
        };
        _browser.SetToolbar(
            _searchQuery,
            IsKoreanLocale ? "이름 또는 ID" : "NAME OR ID",
            IsKoreanLocale ? "검색" : "SEARCH",
            filter,
            sort);
    }

    private void SelectEnemy(string enemyId)
    {
        int index = _visibleEntries.FindIndex(entry => string.Equals(
            entry.EnemyId,
            enemyId,
            StringComparison.Ordinal));
        if (index < 0)
            return;

        _selectedEnemyId = enemyId;
        _browser?.SetSelected(enemyId);
        EnemyCodexEntry entry = _visibleEntries[index];
        Color accentColor = GetGradeColor(entry.Grade);

        if (_detailPanelImage != null)
        {
            _detailPanelImage.color = Color.Lerp(
                PanelColor,
                accentColor,
                0.18f);
        }

        string cardCode = string.IsNullOrWhiteSpace(entry.CardCode)
            ? "--"
            : entry.CardCode;
        _detailTitle.text = $"{entry.DisplayName}  [{cardCode}]";
        _identityText.text = LocalizationService.Get(
            LocalizationKeys.CodexEnemyIdentity,
            LocalizationService.Arg("id", entry.EnemyId),
            LocalizationService.Arg(
                "grade",
                EnemyLocalization.GetGrade(entry.Grade)),
            LocalizationService.Arg(
                "type",
                EnemyLocalization.GetName(entry.Type)));
        _statText.text = LocalizationService.Get(
            LocalizationKeys.CodexEnemyStats,
            LocalizationService.Arg("health", entry.BaseHealth),
            LocalizationService.Arg("threat", entry.ThreatCost),
            LocalizationService.Arg(
                "interval",
                entry.SpawnIntervalMultiplier),
            LocalizationService.Arg(
                "priority",
                EnemyLocalization.GetPriority(
                    entry.TargetPriorityExcluded)));
        _abilityText.text = entry.AbilityDescription;

        ApplyLocalizedFont(_detailTitle, "title");
        ApplyLocalizedFont(_identityText, "body");
        ApplyLocalizedFont(_statText, "number");
        ApplyLocalizedFont(_abilityTitleText, "title");
        ApplyLocalizedFont(_abilityText, "body");
    }

    private void ShowEmptyState()
    {
        if (_detailTitle != null)
            _detailTitle.text = LocalizationService.Get(
                LocalizationKeys.CodexEnemyEmptyTitle);
        if (_identityText != null)
            _identityText.text = string.Empty;
        if (_statText != null)
            _statText.text = LocalizationService.Get(
                LocalizationKeys.CodexEnemyEmptyBody);
        if (_abilityText != null)
            _abilityText.text = string.Empty;
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
            {
                title.text = PageTitle;
                ApplyLocalizedFont(title, "title");
            }

            if (description != null)
            {
                description.text = PageDescription;
                ApplyLocalizedFont(description, "body");
            }
        }

        if (_abilityTitleText != null)
        {
            _abilityTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbility);
            ApplyLocalizedFont(_abilityTitleText, "title");
        }

        if (_backButton != null)
        {
            TextMeshProUGUI backLabel = _backButton
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (backLabel != null)
            {
                backLabel.text = LocalizationService.Get(
                    LocalizationKeys.UiCommonBack);
                ApplyLocalizedFont(backLabel, "body");
            }
        }

    }

    private static void ApplyLocalizedFont(
        TMP_Text text,
        string fontRole)
    {
        LocalizationFontResolver.ApplyGameDefault(text, fontRole);
    }

    private void HandleBackClicked()
    {
        NavigateTo(codexPage, PageOpenMode.Resume);
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

    private static Color GetGradeColor(EEnemyGrade grade)
    {
        return grade switch
        {
            EEnemyGrade.Special => new Color(0.5f, 0.34f, 0.72f, 1f),
            EEnemyGrade.Elite => new Color(0.78f, 0.48f, 0.16f, 1f),
            EEnemyGrade.Boss => new Color(0.72f, 0.2f, 0.18f, 1f),
            _ => new Color(0.22f, 0.48f, 0.64f, 1f),
        };
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

    private static void ReleaseTemporaryDefinition(EnemySO definition)
    {
        if (definition == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(definition);
            return;
        }
#endif
        Destroy(definition);
    }
}
