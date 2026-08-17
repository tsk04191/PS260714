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
        public string Description { get; }
        public string CardCode { get; }
        public Sprite Icon { get; }
        public int SortOrder { get; }
        public EEnemyGrade Grade { get; }
        public EEnemyType Type { get; }
        public int BaseHealth { get; }
        public float HealthScale { get; }
        public int InitialArmor { get; }
        public int InitialShield { get; }
        public float SpawnIntervalMultiplier { get; }
        public float ThreatCost { get; }
        public int UnlockDifficulty { get; }
        public int FootprintWidth { get; }
        public int FootprintHeight { get; }
        public EnemyStackingPolicy StackingPolicy { get; }
        public bool TargetPriorityExcluded { get; }
        public string AbilityDescription { get; }

        public EnemyCodexEntry(EnemySO definition)
        {
            EnemyId = definition.EnemyId;
            DisplayName = EnemyLocalization.GetName(definition);
            Description = EnemyLocalization.GetDescription(definition);
            CardCode = definition.CardCode;
            Icon = definition.IconSprite;
            SortOrder = definition.SortOrder;
            Grade = definition.Grade;
            Type = definition.Type;
            BaseHealth = definition.BaseHealth;
            HealthScale = definition.HealthScale;
            InitialArmor = definition.InitialArmor;
            InitialShield = definition.InitialShield;
            SpawnIntervalMultiplier = definition.SpawnIntervalMultiplier;
            ThreatCost = definition.ThreatCost;
            UnlockDifficulty = definition.UnlockDifficulty;
            FootprintWidth = definition.FootprintWidth;
            FootprintHeight = definition.FootprintHeight;
            StackingPolicy = definition.StackingPolicy;
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
    private TextMeshProUGUI _enemyDescriptionText;
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
            int sortOrder = left.SortOrder.CompareTo(right.SortOrder);
            return sortOrder != 0
                ? sortOrder
                : string.Compare(
                    left.EnemyId,
                    right.EnemyId,
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
        Transform detail = parent?.Find("grpEnemyDetail");
        if (detail == null)
        {
            Debug.LogError(
                "Enemy detail UI must be authored in the Scene.",
                this);
            return;
        }

        _detailPanelImage = detail.GetComponent<Image>();
        _detailTitle = CreateContentText(
            detail, "txtEnemyName", string.Empty, 38f, 58f,
            FontStyles.Bold);
        _identityText = CreateContentText(
            detail, "txtEnemyIdentity", string.Empty, 20f, 42f,
            FontStyles.Bold);
        _enemyDescriptionText = CreateContentText(
            detail, "txtEnemyDescription", string.Empty, 19f, 54f);
        _statText = CreateContentText(
            detail, "txtEnemyStats", string.Empty, 22f, 92f);
        _abilityTitleText = CreateContentText(
            detail, "txtAbilityTitle",
            LocalizationService.Get(LocalizationKeys.CodexEnemyAbility),
            20f, 32f, FontStyles.Bold);
        _abilityText = CreateContentText(
            detail, "txtEnemyAbility", string.Empty, 21f, 108f);
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
                entry.Icon,
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
               ContainsIgnoreCase(entry.Description, _searchQuery) ||
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
            _ => left.SortOrder.CompareTo(right.SortOrder)
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
        _enemyDescriptionText.text = entry.Description;
        string baseStats = LocalizationService.Get(
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
        string extendedStats = IsKoreanLocale
            ? $"체력 배율 {entry.HealthScale:0.##} | 방어도 {entry.InitialArmor} | 보호막 {entry.InitialShield}\n" +
              $"점유 크기 {entry.FootprintWidth}x{entry.FootprintHeight} | " +
              $"배치 {(entry.StackingPolicy == EnemyStackingPolicy.Exclusive ? "독점" : "중첩 가능")} | " +
              $"해금 난이도 {entry.UnlockDifficulty}"
            : $"HEALTH SCALE {entry.HealthScale:0.##} | ARMOR {entry.InitialArmor} | SHIELD {entry.InitialShield}\n" +
              $"FOOTPRINT {entry.FootprintWidth}x{entry.FootprintHeight} | " +
              $"PLACEMENT {(entry.StackingPolicy == EnemyStackingPolicy.Exclusive ? "EXCLUSIVE" : "STACKABLE")} | " +
              $"UNLOCK {entry.UnlockDifficulty}";
        _statText.text = $"{baseStats}\n{extendedStats}";
        _abilityText.text = entry.AbilityDescription;

        ApplyLocalizedFont(_detailTitle, "title");
        ApplyLocalizedFont(_identityText, "body");
        ApplyLocalizedFont(_enemyDescriptionText, "body");
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
        if (_enemyDescriptionText != null)
            _enemyDescriptionText.text = string.Empty;
        if (_statText != null)
            _statText.text = LocalizationService.Get(
                LocalizationKeys.CodexEnemyEmptyBody);
        if (_abilityText != null)
            _abilityText.text = string.Empty;
    }

    private void OnEnable()
    {
        RefreshLocalizedView();
    }

    protected override void OnLocalizationChanged()
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
