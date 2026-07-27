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
    private CodexBrowserView _browser;
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

    protected override string PageTitle => "CHARACTER CODEX";
    protected override string PageDescription =>
        "SELECT A CHARACTER TAB TO VIEW ITS INFORMATION";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.CodexCharacterTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.CodexCharacterDescription;
    protected override Vector2 PanelSize => new(1220f, 900f);
    protected override bool FillAvailableSpace => true;

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildCharacterBrowser();
        BuildDetailPanel(_browser.DetailRoot);
        CreateLocalizedTopLeftOverlayMenuButton(
            "btnBACKTOCODEX",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        RefreshBrowser();
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
        RefreshBrowser();
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
