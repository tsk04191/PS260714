using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterCodexPage : RuntimeMenuPageBase
{
    private readonly struct CharacterCodexEntry
    {
        public string Id { get; }
        public string AssetName { get; }
        public CharacterSO Definition { get; }
        public CharacterData Data { get; }

        public CharacterCodexEntry(CharacterSO definition)
        {
            Id = definition.CharacterId;
            AssetName = definition.name;
            Definition = definition;
            CharacterCollectionData collection =
                DataManager.Current?.CharacterDatas;
            Data = collection != null
                ? collection.CreatePreviewData(definition)
                : definition.CreateData(new CharacterProgressData(
                    definition.CharacterId,
                    definition.InitiallyOwned));
        }
    }

    [Header("Page Navigation")]
    [SerializeField] private GameObject codexPage;
    [SerializeField] private GameObject dungeonPage;

    [Header("Character Definitions")]
    [SerializeField] private CharacterSO[] characterDefinitions =
        Array.Empty<CharacterSO>();

    private readonly List<CharacterCodexEntry> _entries = new();
    private readonly List<CharacterCodexEntry> _visibleEntries = new();
    private readonly List<CharacterCodexEntry> _ownedEntries = new();
    private CodexBrowserView _browser;
    private OperatorDetailView _operatorDetailView;
    private Image _detailPanelImage;
    private Image _standingImage;
    private ScrollRect _detailScrollRect;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _ownershipText;
    private TextMeshProUGUI _identityText;
    private TextMeshProUGUI _statText;
    private TextMeshProUGUI _passiveTitleText;
    private TextMeshProUGUI _passiveText;
    private TextMeshProUGUI _normalAttackTitleText;
    private TextMeshProUGUI _normalAttackText;
    private TextMeshProUGUI _skillTitleText;
    private TextMeshProUGUI _skillText;
    private TextMeshProUGUI _cumulativeUpgradeTitleText;
    private TextMeshProUGUI _cumulativeUpgradeText;
    private TextMeshProUGUI _dungeonUpgradeTitleText;
    private TextMeshProUGUI _dungeonUpgradeText;
    private CharacterCollectionData _boundCharacterCollection;
    private string _selectedCharacterId;
    private string _searchQuery = string.Empty;
    private int _filterIndex;
    private int _sortIndex;
    private GameObject _returnPageOverride;
    private bool _operatorDetailMode;

    protected override string PageTitle => "CHARACTER CODEX";
    protected override string PageDescription =>
        "SELECT A CHARACTER TAB TO VIEW ITS INFORMATION";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.CodexCharacterTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.CodexCharacterDescription;
    protected override Vector2 PanelSize => new(1220f, 900f);
    protected override bool FillAvailableSpace => true;

    public void PrepareOpen(
        string characterId,
        GameObject returnPage)
    {
        _selectedCharacterId = characterId ?? string.Empty;
        _returnPageOverride = returnPage;
        _operatorDetailMode = true;
        _searchQuery = string.Empty;
        _filterIndex = 0;
        if (_operatorDetailView != null)
        {
            RefreshEntries();
            RefreshOperatorDetail();
            ApplyPresentationMode();
        }
    }

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildCharacterBrowser();
        BuildDetailPanel(_browser.DetailRoot);
        Transform runtimeRoot = transform.Find(RuntimeRootObjectName);
        _operatorDetailView = OperatorDetailView.Build(
            runtimeRoot != null ? runtimeRoot : ButtonRoot);
        _operatorDetailView.SetCallbacks(
            () => CycleOwnedCharacter(-1),
            () => CycleOwnedCharacter(1),
            HandleLobbyRepresentativeChanged);
        Button backButton = CreateLocalizedTopLeftOverlayMenuButton(
            "btnBACKTOCODEX",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        if (backButton != null)
            backButton.transform.SetAsLastSibling();
        RefreshBrowser();
        ApplyPresentationMode();
        if (_operatorDetailMode)
            RefreshOperatorDetail();
    }

    private void ApplyPresentationMode()
    {
        Transform browserRoot = ButtonRoot != null
            ? ButtonRoot.Find("grpCodexBrowser")
            : null;
        if (browserRoot != null)
            browserRoot.gameObject.SetActive(!_operatorDetailMode);
        _operatorDetailView?.SetVisible(_operatorDetailMode);
    }

    private void RefreshOperatorDetail()
    {
        if (_operatorDetailView == null)
            return;

        _ownedEntries.Clear();
        foreach (CharacterCodexEntry entry in _entries)
        {
            if (entry.Data != null && entry.Data.IsOwned)
                _ownedEntries.Add(entry);
        }

        _ownedEntries.Sort((left, right) => string.Compare(
            CharacterLocalization.GetName(left.Data),
            CharacterLocalization.GetName(right.Data),
            StringComparison.OrdinalIgnoreCase));

        int selectedIndex = _ownedEntries.FindIndex(entry =>
            string.Equals(
                entry.Id,
                _selectedCharacterId,
                StringComparison.Ordinal));
        if (selectedIndex < 0)
        {
            selectedIndex = _ownedEntries.Count > 0 ? 0 : -1;
            _selectedCharacterId = selectedIndex >= 0
                ? _ownedEntries[selectedIndex].Id
                : string.Empty;
        }

        if (selectedIndex < 0)
        {
            _operatorDetailView.ShowEmpty(
                IsKoreanLocale
                    ? "보유한 대원이 없습니다"
                    : "NO OWNED OPERATORS");
            return;
        }

        CharacterCodexEntry selected = _ownedEntries[selectedIndex];
        _operatorDetailView.SetData(BuildOperatorDetailModel(
            selected,
            selectedIndex,
            _ownedEntries.Count));
    }

    private OperatorDetailModel BuildOperatorDetailModel(
        CharacterCodexEntry entry,
        int selectedIndex,
        int totalCount)
    {
        CharacterData data = entry.Data;
        bool korean = IsKoreanLocale;
        List<OperatorStatModel> stats = new()
        {
            new(
                korean ? "최대 체력" : "MAX HP",
                data.MaximumHealth.ToString("N0")),
            new(
                korean ? "공격력" : "ATTACK",
                data.AttackPower.ToString("0.##")),
            new(
                korean ? "기본 공격 피해" : "ATTACK DMG",
                data.AttackDamage.ToString("N0")),
            new(
                korean ? "공격 주기" : "INTERVAL",
                $"{data.AttackCooldown:0.##}s"),
            new(
                korean ? "직군" : "CLASS",
                korean ? "준비 중" : "PENDING"),
            new(
                korean ? "세부 직군" : "ARCHETYPE",
                korean ? "준비 중" : "PENDING"),
        };

        List<OperatorAbilityIconModel> passives = new();
        int passiveIndex = 1;
        foreach (CharacterPassiveDefinition passive in
                 data.PassiveDefinitions)
        {
            if (passive == null || passive.IsEmptyPlaceholder)
                continue;

            passives.Add(new OperatorAbilityIconModel(
                passive.IconSprite,
                korean
                    ? $"패시브 {passiveIndex}"
                    : $"PASSIVE {passiveIndex}",
                string.Empty));
            passiveIndex++;
        }

        List<OperatorAbilityIconModel> skills = new();
        int skillIndex = 1;
        foreach (CharacterSkillDefinition skill in
                 data.SkillDefinitions)
        {
            if (skill == null)
                continue;

            skills.Add(new OperatorAbilityIconModel(
                skill.IconSprite,
                korean
                    ? $"스킬 {skillIndex}"
                    : $"SKILL {skillIndex}",
                data.GetSkillCost(skill).ToString()));
            skillIndex++;
        }

        return new OperatorDetailModel(
            CharacterLocalization.GetName(data),
            entry.Id,
            data.StandingSprite != null
                ? data.StandingSprite
                : data.IconSprite,
            korean ? "전투 스탯" : "COMBAT STATS",
            stats,
            CharacterLocalization.GetNormalAttackTitle(data),
            CompactSummary(
                CharacterLocalization.GetNormalAttackDescription(data),
                420),
            korean ? "장비" : "EQUIPMENT",
            korean ? "장비" : "SLOT",
            korean ? "준비 중" : "PENDING",
            korean ? "패시브" : "PASSIVES",
            passives,
            passives.Count > 0
                ? CompactSummary(
                    CharacterLocalization.GetPassiveDescription(data),
                    320)
                : (korean
                    ? "등록된 패시브가 없습니다"
                    : "NO CONFIGURED PASSIVES"),
            korean ? "스킬" : "SKILLS",
            skills,
            skills.Count > 0
                ? CompactSummary(
                    CharacterLocalization.GetActiveSkillDescription(data),
                    380)
                : (korean
                    ? "등록된 스킬이 없습니다"
                    : "NO CONFIGURED SKILLS"),
            korean
                ? "메인 화면 대표 대원"
                : "MAIN LOBBY OPERATOR",
            LobbyRepresentativeSelection.IsSelected(entry.Id),
            $"{selectedIndex + 1:00} / {Mathf.Max(1, totalCount):00}");
    }

    private void CycleOwnedCharacter(int direction)
    {
        if (_ownedEntries.Count == 0 || direction == 0)
            return;

        int currentIndex = _ownedEntries.FindIndex(entry =>
            string.Equals(
                entry.Id,
                _selectedCharacterId,
                StringComparison.Ordinal));
        if (currentIndex < 0)
            currentIndex = 0;
        int nextIndex =
            (currentIndex + direction) % _ownedEntries.Count;
        if (nextIndex < 0)
            nextIndex += _ownedEntries.Count;

        _selectedCharacterId = _ownedEntries[nextIndex].Id;
        RefreshOperatorDetail();
    }

    private void HandleLobbyRepresentativeChanged(bool selected)
    {
        if (string.IsNullOrWhiteSpace(_selectedCharacterId))
            return;

        CharacterCodexEntry entry = _ownedEntries.Find(candidate =>
            string.Equals(
                candidate.Id,
                _selectedCharacterId,
                StringComparison.Ordinal));
        if (entry.Data == null || !entry.Data.IsOwned)
            return;

        LobbyRepresentativeSelection.SetSelected(
            entry.Id,
            selected);
        RefreshOperatorDetail();
    }

    private static string CompactSummary(string value, int maxLength)
    {
        string compact = (value ?? string.Empty)
            .Replace("\r", string.Empty)
            .Trim();
        if (compact.Length <= maxLength)
            return compact;
        return compact.Substring(0, Mathf.Max(0, maxLength - 1))
            .TrimEnd() + "…";
    }

    private void RefreshEntries()
    {
        BindCharacterCollection(DataManager.Current?.CharacterDatas);
        _entries.Clear();
        HashSet<CharacterSO> uniqueDefinitions = new();
        AddDefinitions(
            CharacterDefinitionCatalog.GetAll(),
            uniqueDefinitions);
        AddDefinitions(characterDefinitions, uniqueDefinitions);

        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            AddDefinitions(
                dungeon.GetCodexCharacterDefinitions(),
                uniqueDefinitions);
        }

        foreach (CharacterSO definition in uniqueDefinitions)
            _entries.Add(new CharacterCodexEntry(definition));

        _entries.Sort((left, right) => string.Compare(
            CharacterLocalization.GetName(left.Data),
            CharacterLocalization.GetName(right.Data),
            StringComparison.OrdinalIgnoreCase));
    }

    private static void AddDefinitions(
        IReadOnlyList<CharacterSO> definitions,
        HashSet<CharacterSO> uniqueDefinitions)
    {
        if (definitions == null)
            return;

        foreach (CharacterSO definition in definitions)
        {
            if (definition != null)
                uniqueDefinitions.Add(definition);
        }
    }

    private void BuildCharacterBrowser()
    {
        _browser = CodexBrowserView.Build(ButtonRoot);
        _browser.HideLegacyList("grpCharacterTabStrip");
        _browser.AdoptExistingDetail("grpCharacterDetail");
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
            SelectCharacter);
        RefreshBrowserToolbar();
    }

    private void BuildDetailPanel(Transform parent)
    {
        bool detailCreated = parent.Find("grpCharacterDetail") == null;
        GameObject detailObject = GetOrCreateChild(
            parent,
            "grpCharacterDetail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        _detailPanelImage = detailObject.GetComponent<Image>();
        if (detailCreated)
        {
            _detailPanelImage.color = PanelColor;
            _detailPanelImage.raycastTarget = false;

            VerticalLayoutGroup obsoleteVerticalLayout =
                detailObject.GetComponent<VerticalLayoutGroup>();
            if (obsoleteVerticalLayout != null)
                obsoleteVerticalLayout.enabled = false;

            LayoutElement detailLayout =
                detailObject.GetComponent<LayoutElement>();
            detailLayout.preferredHeight = 440f;
            detailLayout.flexibleHeight = 1f;

            HorizontalLayoutGroup detailLayoutGroup =
                detailObject.GetComponent<HorizontalLayoutGroup>();
            detailLayoutGroup.padding = new RectOffset(20, 20, 18, 18);
            detailLayoutGroup.spacing = 18f;
            detailLayoutGroup.childAlignment = TextAnchor.UpperCenter;
            detailLayoutGroup.childControlWidth = true;
            detailLayoutGroup.childControlHeight = true;
            detailLayoutGroup.childForceExpandWidth = false;
            detailLayoutGroup.childForceExpandHeight = true;
        }

        bool visualsCreated =
            detailObject.transform.Find("grpCharacterVisuals") == null;
        GameObject visualObject = GetOrCreateChild(
            detailObject.transform,
            "grpCharacterVisuals",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        if (visualsCreated)
        {
            LayoutElement visualLayout =
                visualObject.GetComponent<LayoutElement>();
            visualLayout.minWidth = 280f;
            visualLayout.preferredWidth = 340f;
            visualLayout.flexibleWidth = 0f;
            VerticalLayoutGroup visualGroup =
                visualObject.GetComponent<VerticalLayoutGroup>();
            visualGroup.spacing = 12f;
            visualGroup.childAlignment = TextAnchor.UpperCenter;
            visualGroup.childControlWidth = true;
            visualGroup.childControlHeight = true;
            visualGroup.childForceExpandWidth = false;
            visualGroup.childForceExpandHeight = false;
        }

        bool standingCreated =
            visualObject.transform.Find("imgCharacterStanding") == null;
        _standingImage = CreateProfileImage(
            visualObject.transform,
            "imgCharacterStanding",
            320f,
            640f);
        if (standingCreated)
        {
            LayoutElement standingLayout =
                _standingImage.GetComponent<LayoutElement>();
            standingLayout.minWidth = 220f;
            standingLayout.minHeight = 400f;
            standingLayout.flexibleHeight = 1f;
        }

        Transform obsoleteIcon =
            visualObject.transform.Find("imgCharacterIcon");
        if (obsoleteIcon != null)
            obsoleteIcon.gameObject.SetActive(false);

        bool scrollCreated =
            detailObject.transform.Find("scrCharacterDetails") == null;
        GameObject scrollObject = GetOrCreateChild(
            detailObject.transform,
            "scrCharacterDetails",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        Image scrollRaycastImage = scrollObject.GetComponent<Image>();
        if (scrollCreated)
        {
            scrollRaycastImage.color = new Color(0f, 0f, 0f, 0.01f);
            scrollRaycastImage.raycastTarget = true;
            LayoutElement scrollLayout =
                scrollObject.GetComponent<LayoutElement>();
            scrollLayout.flexibleWidth = 1f;
            scrollLayout.flexibleHeight = 1f;
        }

        bool viewportCreated =
            scrollObject.transform.Find("vptCharacterDetails") == null;
        GameObject viewportObject = GetOrCreateChild(
            scrollObject.transform,
            "vptCharacterDetails",
            typeof(RectTransform),
            typeof(RectMask2D));
        if (viewportCreated)
            StretchToParent((RectTransform)viewportObject.transform);

        bool contentCreated =
            viewportObject.transform.Find(
                "grpCharacterDetailContent") == null;
        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpCharacterDetailContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)contentObject.transform;
        if (contentCreated)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 12, 4, 12);
            contentLayout.spacing = 6f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter contentFitter =
                contentObject.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        _detailScrollRect = scrollRect;
        scrollRect.viewport = (RectTransform)viewportObject.transform;
        scrollRect.content = contentRect;
        if (scrollCreated)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.inertia = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;
        }

        string[] detailTextNames =
        {
            "txtCharacterName",
            "txtCharacterOwnership",
            "txtCharacterIdentity",
            "txtCharacterStats",
            "txtPassiveTitle",
            "txtPassive",
            "txtNormalAttackTitle",
            "txtNormalAttack",
            "txtActiveSkillTitle",
            "txtActiveSkill",
            "txtCumulativeUpgradeTitle",
            "txtCumulativeUpgrade",
            "txtDungeonUpgradeTitle",
            "txtDungeonUpgrade",
        };
        foreach (string textName in detailTextNames)
        {
            if (contentCreated)
            {
                MoveExistingChild(
                    detailObject.transform,
                    contentObject.transform,
                    textName);
            }
        }

        _detailTitle = CreateContentText(
            contentObject.transform,
            "txtCharacterName",
            string.Empty,
            34f,
            46f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _ownershipText = CreateContentText(
            contentObject.transform,
            "txtCharacterOwnership",
            string.Empty,
            18f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _identityText = CreateContentText(
            contentObject.transform,
            "txtCharacterIdentity",
            string.Empty,
            18f,
            44f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        _statText = CreateContentText(
            contentObject.transform,
            "txtCharacterStats",
            string.Empty,
            19f,
            58f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        _passiveTitleText = CreateContentText(
            contentObject.transform,
            "txtPassiveTitle",
            string.Empty,
            19f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _passiveText = CreateContentText(
            contentObject.transform,
            "txtPassive",
            string.Empty,
            18f,
            48f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        _normalAttackTitleText = CreateContentText(
            contentObject.transform,
            "txtNormalAttackTitle",
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack),
            19f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _normalAttackText = CreateContentText(
            contentObject.transform,
            "txtNormalAttack",
            string.Empty,
            18f,
            54f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        _skillTitleText = CreateContentText(
            contentObject.transform,
            "txtActiveSkillTitle",
            string.Empty,
            19f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _skillText = CreateContentText(
            contentObject.transform,
            "txtActiveSkill",
            string.Empty,
            18f,
            54f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        _cumulativeUpgradeTitleText = CreateContentText(
            contentObject.transform,
            "txtCumulativeUpgradeTitle",
            string.Empty,
            19f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _cumulativeUpgradeText = CreateContentText(
            contentObject.transform,
            "txtCumulativeUpgrade",
            string.Empty,
            18f,
            40f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        _dungeonUpgradeTitleText = CreateContentText(
            contentObject.transform,
            "txtDungeonUpgradeTitle",
            string.Empty,
            19f,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _dungeonUpgradeText = CreateContentText(
            contentObject.transform,
            "txtDungeonUpgrade",
            string.Empty,
            18f,
            80f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
    }

    private void RefreshBrowser()
    {
        if (_browser == null)
            return;

        RefreshBrowserToolbar();
        _visibleEntries.Clear();
        foreach (CharacterCodexEntry entry in _entries)
        {
            if (!MatchesSearch(entry) || !MatchesFilter(entry))
                continue;

            _visibleEntries.Add(entry);
        }

        _visibleEntries.Sort(CompareEntries);
        List<CodexBrowserItemModel> items =
            new(_visibleEntries.Count);
        Color accentColor = new(0.22f, 0.48f, 0.68f, 1f);
        foreach (CharacterCodexEntry entry in _visibleEntries)
        {
            items.Add(new CodexBrowserItemModel(
                entry.Id,
                CharacterLocalization.GetName(entry.Data),
                entry.Data.IconSprite,
                !entry.Data.IsOwned,
                accentColor));
        }

        bool selectionVisible = _visibleEntries.Exists(entry =>
            string.Equals(
                entry.Id,
                _selectedCharacterId,
                StringComparison.Ordinal));
        if (!selectionVisible)
        {
            _selectedCharacterId = _visibleEntries.Count > 0
                ? _visibleEntries[0].Id
                : string.Empty;
        }

        _browser.SetItems(items, _selectedCharacterId);
        if (_visibleEntries.Count > 0)
            SelectCharacter(_selectedCharacterId);
        else
            ShowEmptyState();
    }

    private bool MatchesSearch(CharacterCodexEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        return ContainsIgnoreCase(
                   CharacterLocalization.GetName(entry.Data),
                   _searchQuery) ||
               ContainsIgnoreCase(entry.AssetName, _searchQuery) ||
               ContainsIgnoreCase(entry.Id, _searchQuery);
    }

    private bool MatchesFilter(CharacterCodexEntry entry)
    {
        return _filterIndex switch
        {
            1 => entry.Data.IsOwned,
            2 => !entry.Data.IsOwned,
            _ => true
        };
    }

    private int CompareEntries(
        CharacterCodexEntry left,
        CharacterCodexEntry right)
    {
        if (_sortIndex == 2)
        {
            int ownedOrder = right.Data.IsOwned.CompareTo(
                left.Data.IsOwned);
            if (ownedOrder != 0)
                return ownedOrder;
        }

        int nameOrder = string.Compare(
            CharacterLocalization.GetName(left.Data),
            CharacterLocalization.GetName(right.Data),
            StringComparison.OrdinalIgnoreCase);
        if (_sortIndex == 1)
            nameOrder = -nameOrder;
        if (nameOrder != 0)
            return nameOrder;
        return string.Compare(
            left.Id,
            right.Id,
            StringComparison.Ordinal);
    }

    private void RefreshBrowserToolbar()
    {
        if (_browser == null)
            return;

        string filter = _filterIndex switch
        {
            1 => IsKoreanLocale ? "필터: 보유" : "FILTER: OWNED",
            2 => IsKoreanLocale ? "필터: 미보유" : "FILTER: LOCKED",
            _ => IsKoreanLocale ? "필터: 전체" : "FILTER: ALL"
        };
        string sort = _sortIndex switch
        {
            1 => IsKoreanLocale ? "정렬: 이름↓" : "SORT: NAME↓",
            2 => IsKoreanLocale ? "정렬: 보유" : "SORT: OWNED",
            _ => IsKoreanLocale ? "정렬: 이름↑" : "SORT: NAME↑"
        };
        _browser.SetToolbar(
            _searchQuery,
            IsKoreanLocale ? "이름 또는 ID" : "NAME OR ID",
            IsKoreanLocale ? "검색" : "SEARCH",
            filter,
            sort);
    }

    private void SelectCharacter(string characterId)
    {
        int index = _visibleEntries.FindIndex(entry => string.Equals(
            entry.Id,
            characterId,
            StringComparison.Ordinal));
        if (index < 0)
            return;

        _selectedCharacterId = characterId;
        _browser?.SetSelected(characterId);
        CharacterCodexEntry entry = _visibleEntries[index];
        CharacterData data = entry.Data;
        Color accentColor = new(0.22f, 0.48f, 0.68f, 1f);

        if (_detailPanelImage != null)
        {
            _detailPanelImage.color = Color.Lerp(
                PanelColor,
                accentColor,
                0.18f);
        }

        SetProfileImage(_standingImage, data.StandingSprite);

        _detailTitle.text = CharacterLocalization.GetName(data);
        _ownershipText.text = CharacterLocalization.GetOwnership(data);
        _ownershipText.color = data.IsOwned
            ? new Color(0.45f, 0.9f, 0.55f, 1f)
            : new Color(0.85f, 0.55f, 0.45f, 1f);
        string description = CharacterLocalization.GetDescription(data);
        _identityText.text = CharacterLocalization.GetIdentity(
            entry.AssetName,
            data) +
            (string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : "\n" + description);
        _statText.text = CharacterLocalization.GetStats(data);
        bool hasPassive = data.HasCustomPassiveDefinitions;
        SetTextSectionActive(_passiveTitleText, hasPassive);
        SetTextSectionActive(_passiveText, hasPassive);
        if (hasPassive)
        {
            _passiveTitleText.text = IsKoreanLocale ? "패시브" : "PASSIVE";
            _passiveText.text =
                CharacterLocalization.GetPassiveDescription(data);
        }
        _normalAttackTitleText.text =
            CharacterLocalization.GetNormalAttackTitle(data);
        _normalAttackText.text =
            CharacterLocalization.GetNormalAttackDescription(data);
        _skillTitleText.text =
            CharacterLocalization.GetActiveSkillTitle(data.ActiveSkillCost);
        _skillText.text =
            CharacterLocalization.GetActiveSkillDescription(data);
        _cumulativeUpgradeTitleText.text = IsKoreanLocale
            ? "업그레이드 - 누적"
            : "CUMULATIVE UPGRADES";
        _cumulativeUpgradeText.text =
            CharacterLocalization.GetCumulativeUpgradeDescription(data);
        bool hasDungeonUpgrades = data.HasCustomDungeonUpgrades;
        SetTextSectionActive(_dungeonUpgradeTitleText, hasDungeonUpgrades);
        SetTextSectionActive(_dungeonUpgradeText, hasDungeonUpgrades);
        if (hasDungeonUpgrades)
        {
            _dungeonUpgradeTitleText.text = IsKoreanLocale
                ? "업그레이드 - 던전"
                : "DUNGEON UPGRADES";
            _dungeonUpgradeText.text =
                CharacterLocalization.GetDungeonUpgradeDescription(data);
        }

        SetTextPreferredHeight(_detailTitle, 46f);
        SetTextPreferredHeight(_ownershipText, 28f);
        UpdateTextPreferredHeight(_identityText, 44f);
        UpdateTextPreferredHeight(_statText, 48f);
        UpdateTextPreferredHeight(_passiveText, 48f);
        UpdateTextPreferredHeight(_normalAttackText, 54f);
        UpdateTextPreferredHeight(_skillText, 54f);
        UpdateTextPreferredHeight(_cumulativeUpgradeText, 40f);
        UpdateTextPreferredHeight(_dungeonUpgradeText, 80f);
        if (_detailScrollRect != null)
            _detailScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ShowEmptyState()
    {
        SetProfileImage(_standingImage, null);
        if (_detailTitle != null)
        {
            _detailTitle.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterEmptyTitle);
        }
        if (_ownershipText != null)
            _ownershipText.text = string.Empty;
        if (_identityText != null)
            _identityText.text = string.Empty;
        if (_statText != null)
        {
            _statText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterEmptyBody);
        }
        if (_normalAttackText != null)
            _normalAttackText.text = string.Empty;
        SetTextSectionActive(_passiveTitleText, false);
        SetTextSectionActive(_passiveText, false);
        if (_normalAttackTitleText != null)
        {
            _normalAttackTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack);
        }
        if (_skillTitleText != null)
        {
            _skillTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterActiveSkill);
        }
        if (_skillText != null)
            _skillText.text = string.Empty;
        if (_cumulativeUpgradeTitleText != null)
            _cumulativeUpgradeTitleText.text = string.Empty;
        if (_cumulativeUpgradeText != null)
            _cumulativeUpgradeText.text = string.Empty;
        SetTextSectionActive(_dungeonUpgradeTitleText, false);
        SetTextSectionActive(_dungeonUpgradeText, false);
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        BindCharacterCollection(DataManager.Current?.CharacterDatas);
        RefreshLocalizedView();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        BindCharacterCollection(null);
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshLocalizedView();
    }

    private void BindCharacterCollection(
        CharacterCollectionData collection)
    {
        if (ReferenceEquals(_boundCharacterCollection, collection))
            return;

        if (_boundCharacterCollection != null)
        {
            _boundCharacterCollection.CharacterProgressChanged -=
                HandleCharacterProgressChanged;
        }

        _boundCharacterCollection = collection;
        if (_boundCharacterCollection != null)
        {
            _boundCharacterCollection.CharacterProgressChanged +=
                HandleCharacterProgressChanged;
        }
    }

    private void HandleCharacterProgressChanged(
        CharacterSO unusedDefinition)
    {
        if (isActiveAndEnabled)
            RefreshLocalizedView();
    }

    private void RefreshLocalizedView()
    {
        if (_detailTitle == null)
            return;

        RefreshEntries();
        if (_operatorDetailMode)
            RefreshOperatorDetail();
        else
            RefreshBrowser();
        ApplyPresentationMode();
    }

    private void HandleBackClicked()
    {
        GameObject destination =
            _returnPageOverride != null
                ? _returnPageOverride
                : codexPage;
        _returnPageOverride = null;
        _operatorDetailMode = false;
        ApplyPresentationMode();
        NavigateTo(destination, PageOpenMode.Resume);
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(
                   value,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetTextPreferredHeight(
        TextMeshProUGUI text,
        float preferredHeight)
    {
        if (text != null && text.TryGetComponent(out LayoutElement layout))
            layout.preferredHeight = preferredHeight;
    }

    private static void UpdateTextPreferredHeight(
        TextMeshProUGUI text,
        float minimumHeight)
    {
        if (text == null || !text.TryGetComponent(out LayoutElement layout))
            return;

        float availableWidth = Mathf.Max(420f, text.rectTransform.rect.width);
        float contentHeight = string.IsNullOrWhiteSpace(text.text)
            ? 0f
            : text.GetPreferredValues(
                text.text,
                availableWidth,
                0f).y + 8f;
        layout.preferredHeight = Mathf.Max(minimumHeight, contentHeight);
    }

    private static void SetTextSectionActive(
        TextMeshProUGUI text,
        bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    private static Image CreateProfileImage(
        Transform parent,
        string objectName,
        float preferredWidth,
        float preferredHeight)
    {
        bool created = parent.Find(objectName) == null;
        GameObject imageObject = GetOrCreateChild(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        Image image = imageObject.GetComponent<Image>();
        if (created)
        {
            image.preserveAspect = true;
            image.raycastTarget = false;
            LayoutElement layout = imageObject.GetComponent<LayoutElement>();
            layout.minWidth = preferredWidth;
            layout.preferredWidth = preferredWidth;
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
        }
        return image;
    }

    private static void SetProfileImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = sprite != null
            ? Color.white
            : new Color(0.12f, 0.15f, 0.13f, 0.65f);
    }

    private static void MoveExistingChild(
        Transform sourceParent,
        Transform destinationParent,
        string childName)
    {
        if (sourceParent == null || destinationParent == null)
            return;

        Transform source = sourceParent.Find(childName);
        if (source == null || source.parent == destinationParent)
            return;

        Transform destination = destinationParent.Find(childName);
        if (destination == null)
            source.SetParent(destinationParent, false);
        else
            source.gameObject.SetActive(false);
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
        {
            foreach (Type componentType in componentTypes)
            {
                if (existing.GetComponent(componentType) == null)
                    existing.gameObject.AddComponent(componentType);
            }
            return existing.gameObject;
        }

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

    private static bool IsKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;
}

public readonly struct OperatorStatModel
{
    public string Label { get; }
    public string Value { get; }

    public OperatorStatModel(string label, string value)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
    }
}

public readonly struct OperatorAbilityIconModel
{
    public Sprite Icon { get; }
    public string Label { get; }
    public string Badge { get; }

    public OperatorAbilityIconModel(
        Sprite icon,
        string label,
        string badge)
    {
        Icon = icon;
        Label = label ?? string.Empty;
        Badge = badge ?? string.Empty;
    }
}

public sealed class OperatorDetailModel
{
    public string Name { get; }
    public string CharacterId { get; }
    public Sprite StandingSprite { get; }
    public string StatsTitle { get; }
    public IReadOnlyList<OperatorStatModel> Stats { get; }
    public string BasicAttackTitle { get; }
    public string BasicAttackSummary { get; }
    public string EquipmentTitle { get; }
    public string EquipmentSlotLabel { get; }
    public string EquipmentPendingLabel { get; }
    public string PassiveTitle { get; }
    public IReadOnlyList<OperatorAbilityIconModel> Passives { get; }
    public string PassiveSummary { get; }
    public string SkillTitle { get; }
    public IReadOnlyList<OperatorAbilityIconModel> Skills { get; }
    public string SkillSummary { get; }
    public string LobbyRepresentativeLabel { get; }
    public bool IsLobbyRepresentative { get; }
    public string PositionLabel { get; }

    public OperatorDetailModel(
        string name,
        string characterId,
        Sprite standingSprite,
        string statsTitle,
        IReadOnlyList<OperatorStatModel> stats,
        string basicAttackTitle,
        string basicAttackSummary,
        string equipmentTitle,
        string equipmentSlotLabel,
        string equipmentPendingLabel,
        string passiveTitle,
        IReadOnlyList<OperatorAbilityIconModel> passives,
        string passiveSummary,
        string skillTitle,
        IReadOnlyList<OperatorAbilityIconModel> skills,
        string skillSummary,
        string lobbyRepresentativeLabel,
        bool isLobbyRepresentative,
        string positionLabel)
    {
        Name = name ?? string.Empty;
        CharacterId = characterId ?? string.Empty;
        StandingSprite = standingSprite;
        StatsTitle = statsTitle ?? string.Empty;
        Stats = stats ?? Array.Empty<OperatorStatModel>();
        BasicAttackTitle = basicAttackTitle ?? string.Empty;
        BasicAttackSummary = basicAttackSummary ?? string.Empty;
        EquipmentTitle = equipmentTitle ?? string.Empty;
        EquipmentSlotLabel = equipmentSlotLabel ?? string.Empty;
        EquipmentPendingLabel = equipmentPendingLabel ?? string.Empty;
        PassiveTitle = passiveTitle ?? string.Empty;
        Passives = passives ?? Array.Empty<OperatorAbilityIconModel>();
        PassiveSummary = passiveSummary ?? string.Empty;
        SkillTitle = skillTitle ?? string.Empty;
        Skills = skills ?? Array.Empty<OperatorAbilityIconModel>();
        SkillSummary = skillSummary ?? string.Empty;
        LobbyRepresentativeLabel =
            lobbyRepresentativeLabel ?? string.Empty;
        IsLobbyRepresentative = isLobbyRepresentative;
        PositionLabel = positionLabel ?? string.Empty;
    }
}

public sealed class OperatorDetailView
{
    private const string RootName = "grpOperatorDetail";
    private const int StatCount = 6;
    private const int EquipmentSlotCount = 6;
    private const int MaximumVisibleAbilities = 7;

    private static readonly Color BackgroundColor =
        new(0.02f, 0.028f, 0.027f, 1f);
    private static readonly Color HeaderColor =
        new(0.045f, 0.06f, 0.058f, 0.99f);
    private static readonly Color PanelColor =
        new(0.055f, 0.075f, 0.07f, 0.97f);
    private static readonly Color SubPanelColor =
        new(0.075f, 0.10f, 0.092f, 0.98f);
    private static readonly Color SlotColor =
        new(0.07f, 0.09f, 0.086f, 1f);
    private static readonly Color AccentColor =
        new(0.25f, 0.76f, 0.68f, 1f);
    private static readonly Color AttackColor =
        new(0.90f, 0.54f, 0.20f, 1f);
    private static readonly Color PassiveColor =
        new(0.24f, 0.70f, 0.68f, 1f);
    private static readonly Color SkillColor =
        new(0.58f, 0.42f, 0.88f, 1f);
    private static readonly Color TextColor =
        new(0.92f, 0.95f, 0.91f, 1f);
    private static readonly Color MutedTextColor =
        new(0.52f, 0.59f, 0.56f, 1f);

    private sealed class AbilityIconView
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Image Icon { get; }
        public TextMeshProUGUI Fallback { get; }
        public TextMeshProUGUI Label { get; }
        public TextMeshProUGUI Badge { get; }

        public AbilityIconView(
            GameObject root,
            Image background,
            Image icon,
            TextMeshProUGUI fallback,
            TextMeshProUGUI label,
            TextMeshProUGUI badge)
        {
            Root = root;
            Background = background;
            Icon = icon;
            Fallback = fallback;
            Label = label;
            Badge = badge;
        }
    }

    private readonly Transform _host;
    private readonly TextMeshProUGUI[] _statLabels =
        new TextMeshProUGUI[StatCount];
    private readonly TextMeshProUGUI[] _statValues =
        new TextMeshProUGUI[StatCount];
    private readonly TextMeshProUGUI[] _equipmentLabels =
        new TextMeshProUGUI[EquipmentSlotCount];
    private readonly TextMeshProUGUI[] _equipmentStatuses =
        new TextMeshProUGUI[EquipmentSlotCount];
    private readonly List<AbilityIconView> _passiveIcons = new();
    private readonly List<AbilityIconView> _skillIcons = new();

    private RectTransform _root;
    private Image _standingImage;
    private TextMeshProUGUI _standingFallback;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _idText;
    private TextMeshProUGUI _positionText;
    private TextMeshProUGUI _statsTitle;
    private TextMeshProUGUI _attackTitle;
    private TextMeshProUGUI _attackSummary;
    private TextMeshProUGUI _equipmentTitle;
    private TextMeshProUGUI _passiveTitle;
    private TextMeshProUGUI _passiveSummary;
    private TextMeshProUGUI _skillTitle;
    private TextMeshProUGUI _skillSummary;
    private ToggleSliderController _lobbyRepresentativeToggle;
    private Toggle _legacyLobbyRepresentativeToggle;
    private TextMeshProUGUI _lobbyRepresentativeLabel;
    private Transform _passiveIconRoot;
    private Transform _skillIconRoot;
    private Button _previousButton;
    private Button _nextButton;
    private Action _previousRequested;
    private Action _nextRequested;
    private Action<bool> _lobbyRepresentativeRequested;

    private OperatorDetailView(Transform host)
    {
        _host = host;
    }

    public static OperatorDetailView Build(Transform host)
    {
        OperatorDetailView view = new(host);
        if (!view.TryBindLayout())
        {
            view.BuildLayout();
            if (!view.TryBindLayout())
            {
                throw new InvalidOperationException(
                    "Failed to build the operator detail layout.");
            }
        }

        SetNavigationLabel(view._previousButton, "<");
        SetNavigationLabel(view._nextButton, ">");
        view.ApplyHeaderLayout();
        view._root.SetAsLastSibling();
        view.SetVisible(false);
        return view;
    }

    public void SetCallbacks(
        Action previousRequested,
        Action nextRequested,
        Action<bool> lobbyRepresentativeRequested)
    {
        _previousRequested = previousRequested;
        _nextRequested = nextRequested;
        _lobbyRepresentativeRequested =
            lobbyRepresentativeRequested;
        _previousButton.onClick.RemoveAllListeners();
        _previousButton.onClick.AddListener(
            () => _previousRequested?.Invoke());
        _nextButton.onClick.RemoveAllListeners();
        _nextButton.onClick.AddListener(
            () => _nextRequested?.Invoke());
        if (_lobbyRepresentativeToggle != null)
        {
            _lobbyRepresentativeToggle.ValueChanged -=
                HandleLobbyRepresentativeToggleChanged;
            _lobbyRepresentativeToggle.ValueChanged +=
                HandleLobbyRepresentativeToggleChanged;
        }
        if (_legacyLobbyRepresentativeToggle != null)
        {
            _legacyLobbyRepresentativeToggle.onValueChanged
                .RemoveAllListeners();
            _legacyLobbyRepresentativeToggle.onValueChanged.AddListener(
                HandleLobbyRepresentativeToggleChanged);
        }
    }

    private void HandleLobbyRepresentativeToggleChanged(bool selected)
    {
        _lobbyRepresentativeRequested?.Invoke(selected);
    }

    private void SetLobbyRepresentativeToggleState(
        bool selected,
        bool interactable)
    {
        if (_lobbyRepresentativeToggle != null)
        {
            _lobbyRepresentativeToggle.SetValueWithoutNotify(selected);
            Button toggleButton =
                _lobbyRepresentativeToggle.GetComponent<Button>();
            if (toggleButton != null)
                toggleButton.interactable = interactable;
        }

        if (_legacyLobbyRepresentativeToggle != null)
        {
            _legacyLobbyRepresentativeToggle.interactable =
                interactable;
            _legacyLobbyRepresentativeToggle.SetIsOnWithoutNotify(
                selected);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_root != null)
            _root.gameObject.SetActive(visible);
    }

    public void SetData(OperatorDetailModel model)
    {
        if (model == null)
            return;

        _nameText.text = model.Name;
        _idText.text = string.IsNullOrWhiteSpace(model.CharacterId)
            ? string.Empty
            : $"ID  {model.CharacterId}";
        _positionText.text = model.PositionLabel;
        _lobbyRepresentativeLabel.text =
            model.LobbyRepresentativeLabel;
        SetLobbyRepresentativeToggleState(
            model.IsLobbyRepresentative,
            true);
        _standingImage.sprite = model.StandingSprite;
        _standingImage.enabled = model.StandingSprite != null;
        _standingFallback.gameObject.SetActive(
            model.StandingSprite == null);
        _standingFallback.text = CreateFallbackLabel(model.Name);

        _statsTitle.text = model.StatsTitle;
        for (int index = 0; index < StatCount; index++)
        {
            bool hasStat = index < model.Stats.Count;
            _statLabels[index].text = hasStat
                ? model.Stats[index].Label
                : string.Empty;
            _statValues[index].text = hasStat
                ? model.Stats[index].Value
                : "-";
        }

        _attackTitle.text = model.BasicAttackTitle;
        _attackSummary.text = model.BasicAttackSummary;
        _equipmentTitle.text = model.EquipmentTitle;
        for (int index = 0; index < EquipmentSlotCount; index++)
        {
            _equipmentLabels[index].text =
                $"{model.EquipmentSlotLabel} {index + 1:00}";
            _equipmentStatuses[index].text =
                model.EquipmentPendingLabel;
        }

        _passiveTitle.text = model.PassiveTitle;
        _passiveSummary.text = model.PassiveSummary;
        BindAbilityIcons(
            _passiveIconRoot,
            _passiveIcons,
            "grpPassiveIcon_",
            model.Passives,
            PassiveColor);

        _skillTitle.text = model.SkillTitle;
        _skillSummary.text = model.SkillSummary;
        BindAbilityIcons(
            _skillIconRoot,
            _skillIcons,
            "grpSkillIcon_",
            model.Skills,
            SkillColor);

        bool canCycle = ExtractTotalCount(model.PositionLabel) > 1;
        _previousButton.interactable = canCycle;
        _nextButton.interactable = canCycle;
    }

    public void ShowEmpty(string message)
    {
        _nameText.text = message ?? string.Empty;
        _idText.text = string.Empty;
        _positionText.text = string.Empty;
        _standingImage.sprite = null;
        _standingImage.enabled = false;
        _standingFallback.gameObject.SetActive(true);
        _standingFallback.text = "-";
        _lobbyRepresentativeLabel.text = string.Empty;
        SetLobbyRepresentativeToggleState(false, false);
        for (int index = 0; index < StatCount; index++)
        {
            _statLabels[index].text = string.Empty;
            _statValues[index].text = "-";
        }

        _attackSummary.text = string.Empty;
        _passiveSummary.text = string.Empty;
        _skillSummary.text = string.Empty;
        BindAbilityIcons(
            _passiveIconRoot,
            _passiveIcons,
            "grpPassiveIcon_",
            Array.Empty<OperatorAbilityIconModel>(),
            PassiveColor);
        BindAbilityIcons(
            _skillIconRoot,
            _skillIcons,
            "grpSkillIcon_",
            Array.Empty<OperatorAbilityIconModel>(),
            SkillColor);
        _previousButton.interactable = false;
        _nextButton.interactable = false;
    }

    private bool TryBindLayout()
    {
        if (_host == null)
            return false;

        Transform root = _host.Find(RootName);
        Transform header = root?.Find("grpOperatorDetailHeader");
        Transform center = root?.Find("grpOperatorDetailCenter");
        Transform right = root?.Find("grpOperatorDetailRight");
        Transform left = root?.Find("grpOperatorDetailVisual");
        Transform representative =
            left?.Find("tglLobbyRepresentative");
        Transform attack = center?.Find("grpOperatorBasicAttack");
        Transform passive = right?.Find("grpOperatorPassives");
        Transform skill = right?.Find("grpOperatorSkills");

        _root = root as RectTransform;
        _nameText = header?.Find("txtOperatorDetailName")
            ?.GetComponent<TextMeshProUGUI>();
        _idText = header?.Find("txtOperatorDetailId")
            ?.GetComponent<TextMeshProUGUI>();
        _positionText = header?.Find("txtOperatorDetailPosition")
            ?.GetComponent<TextMeshProUGUI>();
        _previousButton = header?.Find("btnPreviousOperator")
            ?.GetComponent<Button>();
        _nextButton = header?.Find("btnNextOperator")
            ?.GetComponent<Button>();
        _standingImage = left?.Find("imgOperatorDetailStanding")
            ?.GetComponent<Image>();
        _standingFallback = left?.Find(
                "txtOperatorDetailStandingFallback")
            ?.GetComponent<TextMeshProUGUI>();
        _lobbyRepresentativeToggle =
            representative
                ?.GetComponentInChildren<
                    ToggleSliderController>(true);
        _legacyLobbyRepresentativeToggle =
            representative?.GetComponent<Toggle>();
        _lobbyRepresentativeLabel =
            representative?.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>();
        _statsTitle = center?.Find("txtOperatorStatsTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _attackTitle = attack?.Find("txtBasicAttackTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _attackSummary = attack?.Find("txtBasicAttackSummary")
            ?.GetComponent<TextMeshProUGUI>();
        _equipmentTitle = right?.Find("txtEquipmentTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _passiveTitle = passive?.Find("txtPassiveSectionTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _passiveSummary = passive?.Find("txtPassiveSummary")
            ?.GetComponent<TextMeshProUGUI>();
        _skillTitle = skill?.Find("txtSkillSectionTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _skillSummary = skill?.Find("txtSkillSummary")
            ?.GetComponent<TextMeshProUGUI>();
        _passiveIconRoot = passive?.Find("grpPassiveIconRoot");
        _skillIconRoot = skill?.Find("grpSkillIconRoot");

        for (int index = 0; index < StatCount; index++)
        {
            Transform stat = center?.Find($"grpOperatorStat_{index}");
            _statLabels[index] = stat?.Find("txtStatLabel")
                ?.GetComponent<TextMeshProUGUI>();
            _statValues[index] = stat?.Find("txtStatValue")
                ?.GetComponent<TextMeshProUGUI>();
        }

        for (int index = 0; index < EquipmentSlotCount; index++)
        {
            Transform slot = right?.Find($"grpEquipmentSlot_{index}");
            _equipmentLabels[index] = slot?.Find("txtSlotLabel")
                ?.GetComponent<TextMeshProUGUI>();
            _equipmentStatuses[index] = slot?.Find("txtSlotStatus")
                ?.GetComponent<TextMeshProUGUI>();
        }

        if (_root == null ||
            _nameText == null ||
            _idText == null ||
            _positionText == null ||
            _previousButton == null ||
            _nextButton == null ||
            _standingImage == null ||
            _standingFallback == null ||
            (_lobbyRepresentativeToggle == null &&
             _legacyLobbyRepresentativeToggle == null) ||
            _lobbyRepresentativeLabel == null ||
            _statsTitle == null ||
            _attackTitle == null ||
            _attackSummary == null ||
            _equipmentTitle == null ||
            _passiveTitle == null ||
            _passiveSummary == null ||
            _skillTitle == null ||
            _skillSummary == null ||
            _passiveIconRoot == null ||
            _skillIconRoot == null)
        {
            return false;
        }

        for (int index = 0; index < StatCount; index++)
        {
            if (_statLabels[index] == null ||
                _statValues[index] == null)
            {
                return false;
            }
        }

        for (int index = 0; index < EquipmentSlotCount; index++)
        {
            if (_equipmentLabels[index] == null ||
                _equipmentStatuses[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void BuildLayout()
    {
        GameObject rootObject = GetOrCreateUiObject(
            _host,
            RootName,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform root = (RectTransform)rootObject.transform;
        Stretch(root);
        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = BackgroundColor;
        rootImage.raycastTarget = true;

        BuildHeader(root);
        BuildVisualPanel(root);
        BuildCenterPanel(root);
        BuildRightPanel(root);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObject = GetOrCreateUiObject(
            parent,
            "grpOperatorDetailHeader",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform header = (RectTransform)headerObject.transform;
        ConfigureTopStretch(header, 104f);
        Image headerImage = headerObject.GetComponent<Image>();
        headerImage.color = HeaderColor;
        headerImage.raycastTarget = false;

        GameObject accentObject = GetOrCreateUiObject(
            header,
            "imgOperatorDetailHeaderAccent",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform accent = (RectTransform)accentObject.transform;
        ConfigureTopLeft(
            accent,
            new Vector2(176f, -20f),
            new Vector2(6f, 66f));
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        TextMeshProUGUI name = CreateText(
            header,
            "txtOperatorDetailName",
            38f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        ConfigureTopLeft(
            name.rectTransform,
            new Vector2(200f, -12f),
            new Vector2(1050f, 50f));
        name.fontStyle = FontStyles.Bold;

        TextMeshProUGUI id = CreateText(
            header,
            "txtOperatorDetailId",
            15f,
            TextAlignmentOptions.MidlineLeft,
            MutedTextColor);
        ConfigureTopLeft(
            id.rectTransform,
            new Vector2(200f, -65f),
            new Vector2(1050f, 24f));

        TextMeshProUGUI position = CreateText(
            header,
            "txtOperatorDetailPosition",
            18f,
            TextAlignmentOptions.Center,
            AccentColor);
        RectTransform positionRect = position.rectTransform;
        positionRect.anchorMin = Vector2.one;
        positionRect.anchorMax = Vector2.one;
        positionRect.pivot = Vector2.one;
        positionRect.anchoredPosition = new Vector2(-272f, -36f);
        positionRect.sizeDelta = new Vector2(116f, 40f);

        BuildNavigationButton(
            header,
            "btnPreviousOperator",
            "<",
            new Vector2(-154f, -22f));
        BuildNavigationButton(
            header,
            "btnNextOperator",
            ">",
            new Vector2(-48f, -22f));
    }

    private void BuildVisualPanel(Transform parent)
    {
        GameObject panelObject = BuildBodyPanel(
            parent,
            "grpOperatorDetailVisual",
            28f,
            594f);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.038f, 0.036f, 1f);

        GameObject imageObject = GetOrCreateUiObject(
            panelObject.transform,
            "imgOperatorDetailStanding",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform imageRect = (RectTransform)imageObject.transform;
        Stretch(imageRect);
        imageRect.offsetMin = new Vector2(18f, 18f);
        imageRect.offsetMax = new Vector2(-18f, -18f);
        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        TextMeshProUGUI fallback = CreateText(
            panelObject.transform,
            "txtOperatorDetailStandingFallback",
            68f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        Stretch(fallback.rectTransform);
        fallback.fontStyle = FontStyles.Bold;

        BuildLobbyRepresentativeToggle(panelObject.transform);
    }

    private void BuildLobbyRepresentativeToggle(Transform parent)
    {
        ToggleSliderController togglePrefab =
            GameManager.Instance?.LobbyRepresentativeTogglePrefab;
        if (togglePrefab != null)
        {
            BuildPrefabLobbyRepresentativeToggle(
                parent,
                togglePrefab);
            return;
        }

        Debug.LogWarning(
            "GameManager has no lobby representative toggle prefab. " +
            "Using the legacy runtime toggle.");

        GameObject toggleObject = GetOrCreateUiObject(
            parent,
            "tglLobbyRepresentative",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Toggle),
            typeof(Outline));
        RectTransform toggleRect =
            (RectTransform)toggleObject.transform;
        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = Vector2.zero;
        toggleRect.pivot = Vector2.zero;
        toggleRect.anchoredPosition = new Vector2(20f, 20f);
        toggleRect.sizeDelta = new Vector2(360f, 68f);

        Image background = toggleObject.GetComponent<Image>();
        background.color =
            new Color(0.025f, 0.045f, 0.042f, 0.96f);
        background.raycastTarget = true;
        Outline outline = toggleObject.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.25f, 0.76f, 0.68f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject boxObject = GetOrCreateUiObject(
            toggleRect,
            "imgToggleBox",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform box = (RectTransform)boxObject.transform;
        box.anchorMin = new Vector2(0f, 0.5f);
        box.anchorMax = new Vector2(0f, 0.5f);
        box.pivot = new Vector2(0f, 0.5f);
        box.anchoredPosition = new Vector2(16f, 0f);
        box.sizeDelta = new Vector2(40f, 40f);
        Image boxImage = boxObject.GetComponent<Image>();
        boxImage.color =
            new Color(0.10f, 0.16f, 0.15f, 1f);
        boxImage.raycastTarget = false;

        GameObject checkObject = GetOrCreateUiObject(
            box,
            "imgToggleCheck",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform check = (RectTransform)checkObject.transform;
        Stretch(check);
        check.offsetMin = new Vector2(7f, 7f);
        check.offsetMax = new Vector2(-7f, -7f);
        Image checkImage = checkObject.GetComponent<Image>();
        checkImage.color = AccentColor;
        checkImage.raycastTarget = false;

        TextMeshProUGUI label = CreateText(
            toggleRect,
            "txtLabel",
            18f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(72f, 8f);
        label.rectTransform.offsetMax = new Vector2(-14f, -8f);
        label.fontStyle = FontStyles.Bold;

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkImage;
        toggle.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            Color.Lerp(Color.white, AccentColor, 0.18f);
        colors.pressedColor =
            Color.Lerp(Color.white, Color.black, 0.2f);
        colors.selectedColor = Color.white;
        colors.disabledColor =
            new Color(0.45f, 0.48f, 0.46f, 0.7f);
        colors.fadeDuration = 0.08f;
        toggle.colors = colors;
    }

    private void BuildPrefabLobbyRepresentativeToggle(
        Transform parent,
        ToggleSliderController togglePrefab)
    {
        GameObject containerObject = GetOrCreateUiObject(
            parent,
            "tglLobbyRepresentative",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform container =
            (RectTransform)containerObject.transform;
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.zero;
        container.pivot = Vector2.zero;
        container.anchoredPosition = new Vector2(20f, 20f);
        container.sizeDelta = new Vector2(360f, 68f);

        Image background = containerObject.GetComponent<Image>();
        background.color =
            new Color(0.025f, 0.045f, 0.042f, 0.96f);
        background.raycastTarget = false;

        Outline outline = containerObject.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.25f, 0.76f, 0.68f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI label = CreateText(
            container,
            "txtLabel",
            18f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(16f, 8f);
        label.rectTransform.offsetMax = new Vector2(-122f, -8f);
        label.fontStyle = FontStyles.Bold;

        ToggleSliderController toggle =
            container.Find("btnToggle")
                ?.GetComponent<ToggleSliderController>();
        if (toggle == null)
        {
            toggle = UnityEngine.Object.Instantiate(
                togglePrefab,
                container,
                false);
            toggle.name = "btnToggle";
        }

        RectTransform toggleRect =
            toggle.transform as RectTransform;
        if (toggleRect != null)
        {
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(-14f, 0f);
            toggleRect.sizeDelta = new Vector2(90f, 45f);
            toggleRect.localScale = Vector3.one;
        }
    }

    private void BuildCenterPanel(Transform parent)
    {
        GameObject panelObject = BuildBodyPanel(
            parent,
            "grpOperatorDetailCenter",
            642f,
            410f);
        Transform panel = panelObject.transform;

        TextMeshProUGUI title = CreateText(
            panel,
            "txtOperatorStatsTitle",
            24f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        ConfigureTopLeft(
            title.rectTransform,
            new Vector2(20f, -18f),
            new Vector2(370f, 34f));
        title.fontStyle = FontStyles.Bold;

        for (int index = 0; index < StatCount; index++)
            BuildStatCell(panel, index);

        GameObject attackObject = GetOrCreateUiObject(
            panel,
            "grpOperatorBasicAttack",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform attack = (RectTransform)attackObject.transform;
        ConfigureTopLeft(
            attack,
            new Vector2(20f, -372f),
            new Vector2(370f, 514f));
        Image attackImage = attackObject.GetComponent<Image>();
        attackImage.color = SubPanelColor;
        attackImage.raycastTarget = false;
        Outline outline = attackObject.GetComponent<Outline>();
        outline.effectColor = new Color(
            AttackColor.r,
            AttackColor.g,
            AttackColor.b,
            0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI attackTitle = CreateText(
            attack,
            "txtBasicAttackTitle",
            23f,
            TextAlignmentOptions.MidlineLeft,
            AttackColor);
        ConfigureTopLeft(
            attackTitle.rectTransform,
            new Vector2(18f, -14f),
            new Vector2(334f, 38f));
        attackTitle.fontStyle = FontStyles.Bold;

        TextMeshProUGUI summary = CreateText(
            attack,
            "txtBasicAttackSummary",
            17f,
            TextAlignmentOptions.TopLeft,
            TextColor);
        Stretch(summary.rectTransform);
        summary.rectTransform.offsetMin = new Vector2(18f, 18f);
        summary.rectTransform.offsetMax = new Vector2(-18f, -64f);
        summary.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void BuildStatCell(Transform parent, int index)
    {
        int column = index % 2;
        int row = index / 2;
        GameObject cellObject = GetOrCreateUiObject(
            parent,
            $"grpOperatorStat_{index}",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform cell = (RectTransform)cellObject.transform;
        ConfigureTopLeft(
            cell,
            new Vector2(
                20f + column * 190f,
                -62f - row * 98f),
            new Vector2(180f, 88f));
        Image cellImage = cellObject.GetComponent<Image>();
        cellImage.color = SubPanelColor;
        cellImage.raycastTarget = false;

        TextMeshProUGUI label = CreateText(
            cell,
            "txtStatLabel",
            14f,
            TextAlignmentOptions.TopLeft,
            MutedTextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 42f);
        label.rectTransform.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI value = CreateText(
            cell,
            "txtStatValue",
            25f,
            TextAlignmentOptions.BottomRight,
            TextColor);
        Stretch(value.rectTransform);
        value.rectTransform.offsetMin = new Vector2(12f, 8f);
        value.rectTransform.offsetMax = new Vector2(-12f, -34f);
        value.fontStyle = FontStyles.Bold;
    }

    private void BuildRightPanel(Transform parent)
    {
        GameObject panelObject = BuildBodyPanel(
            parent,
            "grpOperatorDetailRight",
            1072f,
            820f);
        Transform panel = panelObject.transform;

        TextMeshProUGUI equipmentTitle = CreateText(
            panel,
            "txtEquipmentTitle",
            24f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        ConfigureTopLeft(
            equipmentTitle.rectTransform,
            new Vector2(20f, -18f),
            new Vector2(780f, 34f));
        equipmentTitle.fontStyle = FontStyles.Bold;

        for (int index = 0; index < EquipmentSlotCount; index++)
            BuildEquipmentSlot(panel, index);

        BuildAbilitySection(
            panel,
            "grpOperatorPassives",
            "txtPassiveSectionTitle",
            "grpPassiveIconRoot",
            "txtPassiveSummary",
            318f,
            240f,
            PassiveColor);
        BuildAbilitySection(
            panel,
            "grpOperatorSkills",
            "txtSkillSectionTitle",
            "grpSkillIconRoot",
            "txtSkillSummary",
            574f,
            312f,
            SkillColor);
    }

    private void BuildEquipmentSlot(Transform parent, int index)
    {
        int column = index % 3;
        int row = index / 3;
        GameObject slotObject = GetOrCreateUiObject(
            parent,
            $"grpEquipmentSlot_{index}",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform slot = (RectTransform)slotObject.transform;
        ConfigureTopLeft(
            slot,
            new Vector2(
                20f + column * 262f,
                -62f - row * 122f),
            new Vector2(250f, 110f));
        Image slotImage = slotObject.GetComponent<Image>();
        slotImage.color = SlotColor;
        slotImage.raycastTarget = false;
        Outline outline = slotObject.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.28f, 0.36f, 0.33f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI label = CreateText(
            slot,
            "txtSlotLabel",
            17f,
            TextAlignmentOptions.TopLeft,
            TextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(14f, 56f);
        label.rectTransform.offsetMax = new Vector2(-14f, -12f);
        label.fontStyle = FontStyles.Bold;

        TextMeshProUGUI status = CreateText(
            slot,
            "txtSlotStatus",
            14f,
            TextAlignmentOptions.BottomRight,
            MutedTextColor);
        Stretch(status.rectTransform);
        status.rectTransform.offsetMin = new Vector2(14f, 12f);
        status.rectTransform.offsetMax = new Vector2(-14f, -56f);
    }

    private void BuildAbilitySection(
        Transform parent,
        string objectName,
        string titleName,
        string iconRootName,
        string summaryName,
        float top,
        float height,
        Color accentColor)
    {
        GameObject sectionObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform section = (RectTransform)sectionObject.transform;
        ConfigureTopLeft(
            section,
            new Vector2(20f, -top),
            new Vector2(780f, height));
        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = SubPanelColor;
        sectionImage.raycastTarget = false;
        Outline outline = sectionObject.GetComponent<Outline>();
        outline.effectColor = new Color(
            accentColor.r,
            accentColor.g,
            accentColor.b,
            0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI title = CreateText(
            section,
            titleName,
            21f,
            TextAlignmentOptions.MidlineLeft,
            accentColor);
        ConfigureTopLeft(
            title.rectTransform,
            new Vector2(16f, -10f),
            new Vector2(748f, 34f));
        title.fontStyle = FontStyles.Bold;

        GameObject iconRootObject = GetOrCreateUiObject(
            section,
            iconRootName);
        RectTransform iconRoot =
            (RectTransform)iconRootObject.transform;
        ConfigureTopLeft(
            iconRoot,
            new Vector2(14f, -50f),
            new Vector2(752f, 82f));

        TextMeshProUGUI summary = CreateText(
            section,
            summaryName,
            15f,
            TextAlignmentOptions.TopLeft,
            TextColor);
        Stretch(summary.rectTransform);
        summary.rectTransform.offsetMin = new Vector2(16f, 14f);
        summary.rectTransform.offsetMax =
            new Vector2(-16f, -142f);
        summary.overflowMode = TextOverflowModes.Ellipsis;
    }

    private Button BuildNavigationButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 topRightPosition)
    {
        GameObject buttonObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform buttonRect =
            (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.anchoredPosition = topRightPosition;
        buttonRect.sizeDelta = new Vector2(92f, 60f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = SubPanelColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ApplyButtonColors(button, AccentColor);

        TextMeshProUGUI text = CreateText(
            buttonRect,
            "txtLabel",
            38f,
            TextAlignmentOptions.Center,
            TextColor);
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }

    private void BindAbilityIcons(
        Transform parent,
        List<AbilityIconView> views,
        string prefix,
        IReadOnlyList<OperatorAbilityIconModel> items,
        Color accentColor)
    {
        int itemCount = items?.Count ?? 0;
        int visibleCount = Mathf.Min(
            itemCount,
            MaximumVisibleAbilities);
        for (int index = 0; index < visibleCount; index++)
        {
            AbilityIconView view = GetOrCreateAbilityIcon(
                parent,
                views,
                prefix,
                index,
                accentColor);
            OperatorAbilityIconModel item = items[index];
            bool overflow = itemCount > MaximumVisibleAbilities &&
                            index == MaximumVisibleAbilities - 1;
            view.Root.SetActive(true);
            view.Background.color = Color.Lerp(
                SlotColor,
                accentColor,
                0.18f);
            view.Icon.sprite = overflow ? null : item.Icon;
            view.Icon.enabled = !overflow && item.Icon != null;
            view.Fallback.gameObject.SetActive(
                overflow || item.Icon == null);
            view.Fallback.text = overflow
                ? $"+{itemCount - MaximumVisibleAbilities + 1}"
                : CreateFallbackLabel(item.Label);
            view.Label.text = overflow
                ? string.Empty
                : item.Label;
            bool hasBadge = !overflow &&
                            !string.IsNullOrWhiteSpace(item.Badge);
            view.Badge.gameObject.SetActive(hasBadge);
            view.Badge.text = hasBadge ? item.Badge : string.Empty;
        }

        for (int index = visibleCount; index < views.Count; index++)
            views[index].Root.SetActive(false);

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child == null ||
                !child.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring(prefix.Length);
            if (int.TryParse(suffix, out int parsedIndex) &&
                parsedIndex >= visibleCount)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private AbilityIconView GetOrCreateAbilityIcon(
        Transform parent,
        List<AbilityIconView> views,
        string prefix,
        int index,
        Color accentColor)
    {
        while (views.Count <= index)
        {
            int viewIndex = views.Count;
            Transform existing = parent.Find(prefix + viewIndex);
            views.Add(existing != null
                ? BindExistingAbilityIcon(existing.gameObject)
                : BuildAbilityIcon(
                    parent,
                    prefix + viewIndex,
                    viewIndex,
                    accentColor));
        }

        return views[index];
    }

    private AbilityIconView BuildAbilityIcon(
        Transform parent,
        string objectName,
        int index,
        Color accentColor)
    {
        GameObject rootObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform root = (RectTransform)rootObject.transform;
        ConfigureTopLeft(
            root,
            new Vector2(index * 106f, 0f),
            new Vector2(96f, 80f));
        Image background = rootObject.GetComponent<Image>();
        background.color = Color.Lerp(
            SlotColor,
            accentColor,
            0.18f);
        Button button = rootObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        ApplyButtonColors(button, accentColor);

        GameObject iconObject = GetOrCreateUiObject(
            root,
            "imgAbilityIcon",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform iconRect = (RectTransform)iconObject.transform;
        ConfigureTopLeft(
            iconRect,
            new Vector2(8f, -8f),
            new Vector2(48f, 48f));
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI fallback = CreateText(
            root,
            "txtAbilityFallback",
            22f,
            TextAlignmentOptions.Center,
            accentColor);
        ConfigureTopLeft(
            fallback.rectTransform,
            new Vector2(8f, -8f),
            new Vector2(48f, 48f));
        fallback.fontStyle = FontStyles.Bold;

        TextMeshProUGUI label = CreateText(
            root,
            "txtAbilityLabel",
            12f,
            TextAlignmentOptions.BottomLeft,
            TextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(8f, 6f);
        label.rectTransform.offsetMax = new Vector2(-6f, -54f);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI badge = CreateText(
            root,
            "txtAbilityBadge",
            13f,
            TextAlignmentOptions.Center,
            TextColor);
        badge.rectTransform.anchorMin = Vector2.one;
        badge.rectTransform.anchorMax = Vector2.one;
        badge.rectTransform.pivot = Vector2.one;
        badge.rectTransform.anchoredPosition =
            new Vector2(-5f, -5f);
        badge.rectTransform.sizeDelta = new Vector2(30f, 24f);
        badge.fontStyle = FontStyles.Bold;

        return new AbilityIconView(
            rootObject,
            background,
            icon,
            fallback,
            label,
            badge);
    }

    private static AbilityIconView BindExistingAbilityIcon(
        GameObject rootObject)
    {
        Button button = rootObject.GetComponent<Button>();
        Image background = rootObject.GetComponent<Image>();
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        ApplyButtonColors(button, AccentColor);
        return new AbilityIconView(
            rootObject,
            background,
            rootObject.transform.Find("imgAbilityIcon")
                ?.GetComponent<Image>(),
            rootObject.transform.Find("txtAbilityFallback")
                ?.GetComponent<TextMeshProUGUI>(),
            rootObject.transform.Find("txtAbilityLabel")
                ?.GetComponent<TextMeshProUGUI>(),
            rootObject.transform.Find("txtAbilityBadge")
                ?.GetComponent<TextMeshProUGUI>());
    }

    private GameObject BuildBodyPanel(
        Transform parent,
        string objectName,
        float left,
        float width)
    {
        GameObject panelObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panel = (RectTransform)panelObject.transform;
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(left, -50f);
        panel.sizeDelta = new Vector2(width, -148f);
        Image image = panelObject.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;
        return panelObject;
    }

    private static int ExtractTotalCount(string positionLabel)
    {
        if (string.IsNullOrWhiteSpace(positionLabel))
            return 0;

        int separator = positionLabel.LastIndexOf('/');
        if (separator < 0)
            return 0;
        return int.TryParse(
            positionLabel.Substring(separator + 1).Trim(),
            out int total)
            ? total
            : 0;
    }

    private static string CreateFallbackLabel(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return "?";
        return normalized.Length <= 2
            ? normalized
            : normalized.Substring(0, 2);
    }

    private static GameObject GetOrCreateUiObject(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = new GameObject(
                objectName,
                typeof(RectTransform));
            target.layer = parent != null
                ? parent.gameObject.layer
                : 0;
            target.transform.SetParent(parent, false);
        }

        if (componentTypes != null)
        {
            foreach (Type componentType in componentTypes)
            {
                if (componentType != null &&
                    target.GetComponent(componentType) == null)
                {
                    target.AddComponent(componentType);
                }
            }
        }

        return target;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 7f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void ApplyButtonColors(
        Button button,
        Color accentColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(
            Color.white,
            accentColor,
            0.32f);
        colors.pressedColor =
            Color.Lerp(Color.white, Color.black, 0.22f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor =
            new Color(0.45f, 0.48f, 0.46f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void SetNavigationLabel(
        Button button,
        string label)
    {
        TextMeshProUGUI text = button != null
            ? button.transform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (text != null)
            text.text = label ?? string.Empty;
    }

    private void ApplyHeaderLayout()
    {
        Transform header = _root != null
            ? _root.Find("grpOperatorDetailHeader")
            : null;
        RectTransform accent = header
            ?.Find("imgOperatorDetailHeaderAccent")
            as RectTransform;
        if (accent != null)
        {
            ConfigureTopLeft(
                accent,
                new Vector2(176f, -20f),
                new Vector2(6f, 66f));
        }

        if (_nameText != null)
        {
            ConfigureTopLeft(
                _nameText.rectTransform,
                new Vector2(200f, -12f),
                new Vector2(1050f, 50f));
        }

        if (_idText != null)
        {
            ConfigureTopLeft(
                _idText.rectTransform,
                new Vector2(200f, -65f),
                new Vector2(1050f, 24f));
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ConfigureTopStretch(
        RectTransform rect,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void ConfigureTopLeft(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
