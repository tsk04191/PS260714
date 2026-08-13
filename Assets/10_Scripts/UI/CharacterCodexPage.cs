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
                korean ? "판단 / 지식" : "JUDGMENT / KNOWLEDGE",
                $"{data.Judgment:N0} / {data.Knowledge:N0}"),
            new(
                korean ? "공격 주기" : "INTERVAL",
                $"{data.AttackCooldown:0.##}s"),
            new(
                korean ? "직군" : "CLASS",
                CharacterRolePresentation.GetRoleName(data.Role),
                data.Role?.IconSprite),
            new(
                korean ? "세부 직군" : "ARCHETYPE",
                CharacterRolePresentation.GetArchetypeName(
                    data.Archetype),
                data.Archetype?.IconSprite),
        };

        List<OperatorAbilityIconModel> passives = new();
        int passiveIndex = 1;
        foreach (CharacterResolvedPassive resolved in
                 data.ResolvedPassives)
        {
            CharacterPassiveDefinition passive =
                resolved.Definition;
            if (passive == null || passive.IsEmptyPlaceholder)
                continue;

            CharacterRolePassiveDefinition sharedPassive =
                resolved.IsRolePassive
                    ? resolved.RolePassive
                    : resolved.IsArchetypePassive
                        ? resolved.ArchetypePassive
                        : null;
            string label = sharedPassive != null
                ? sharedPassive.GetDisplayName()
                : (korean
                    ? $"패시브 {passiveIndex}"
                    : $"PASSIVE {passiveIndex}");
            string badge = resolved.IsRolePassive
                ? resolved.Role.GetDisplayName()
                : resolved.IsArchetypePassive
                    ? resolved.Archetype.GetDisplayName()
                    : string.Empty;
            passives.Add(new OperatorAbilityIconModel(
                resolved.IconSprite,
                label,
                badge));
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
            data.Grade,
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
        Transform detail = parent?.Find("grpCharacterDetail");
        Transform visuals = detail?.Find("grpCharacterVisuals");
        Transform scroll = detail?.Find("scrCharacterDetails");
        Transform viewport = scroll?.Find("vptCharacterDetails");
        Transform content = viewport?.Find("grpCharacterDetailContent");
        if (detail == null || visuals == null || scroll == null ||
            viewport == null || content == null)
        {
            Debug.LogError(
                "Character detail UI must be authored in the Scene.",
                this);
            return;
        }

        _detailPanelImage = detail.GetComponent<Image>();
        _standingImage = visuals.Find("imgCharacterStanding")
            ?.GetComponent<Image>();
        _detailScrollRect = scroll.GetComponent<ScrollRect>();
        _detailTitle = CreateContentText(
            content, "txtCharacterName", string.Empty, 34f, 46f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _ownershipText = CreateContentText(
            content, "txtCharacterOwnership", string.Empty, 18f, 28f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _identityText = CreateContentText(
            content, "txtCharacterIdentity", string.Empty, 18f, 44f,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        _statText = CreateContentText(
            content, "txtCharacterStats", string.Empty, 19f, 58f,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        _passiveTitleText = CreateContentText(
            content, "txtPassiveTitle", string.Empty, 19f, 28f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _passiveText = CreateContentText(
            content, "txtPassive", string.Empty, 18f, 48f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _normalAttackTitleText = CreateContentText(
            content, "txtNormalAttackTitle",
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack),
            19f, 28f, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        _normalAttackText = CreateContentText(
            content, "txtNormalAttack", string.Empty, 18f, 54f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _skillTitleText = CreateContentText(
            content, "txtActiveSkillTitle", string.Empty, 19f, 28f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _skillText = CreateContentText(
            content, "txtActiveSkill", string.Empty, 18f, 54f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _cumulativeUpgradeTitleText = CreateContentText(
            content, "txtCumulativeUpgradeTitle", string.Empty, 19f, 28f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _cumulativeUpgradeText = CreateContentText(
            content, "txtCumulativeUpgrade", string.Empty, 18f, 40f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _dungeonUpgradeTitleText = CreateContentText(
            content, "txtDungeonUpgradeTitle", string.Empty, 19f, 28f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _dungeonUpgradeText = CreateContentText(
            content, "txtDungeonUpgrade", string.Empty, 18f, 80f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
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
        foreach (CharacterCodexEntry entry in _visibleEntries)
        {
            CharacterGradeStyle gradeStyle =
                CharacterGradePresentation.GetStyle(entry.Data.Grade);
            items.Add(new CodexBrowserItemModel(
                entry.Id,
                CharacterLocalization.GetName(entry.Data),
                entry.Data.IconSprite != null
                    ? entry.Data.IconSprite
                    : gradeStyle.GradeIcon,
                !entry.Data.IsOwned,
                gradeStyle.PrimaryColor));
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
        CharacterGradeStyle gradeStyle =
            CharacterGradePresentation.GetStyle(data.Grade);
        Color accentColor = gradeStyle.PrimaryColor;

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

    private static void SetProfileImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = sprite != null
            ? Color.white
            : new Color(0.12f, 0.15f, 0.13f, 0.65f);
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
    public Sprite Icon { get; }

    public OperatorStatModel(
        string label,
        string value,
        Sprite icon = null)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
        Icon = icon;
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
    public CharacterGrade Grade { get; }
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
        CharacterGrade grade,
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
        Grade = CharacterGradePresentation.Clamp(grade);
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
    private readonly Image[] _statIcons = new Image[StatCount];
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
    private CharacterGradeIconStrip _gradeIcons;
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
    private TextMeshProUGUI _lobbyRepresentativeLabel;
    private Transform _passiveIconRoot;
    private Transform _skillIconRoot;
    private GameObject _abilityIconPrefab;
    private Button _previousButton;
    private Button _nextButton;
    private Action _previousRequested;
    private Action _nextRequested;
    private Action<bool> _lobbyRepresentativeRequested;
    private string _layoutError = string.Empty;

    private OperatorDetailView(Transform host)
    {
        _host = host;
    }

    public static OperatorDetailView Build(Transform host)
    {
        OperatorDetailView view = new(host);
        if (!view.TryBindLayout())
        {
            throw new InvalidOperationException(
                "The saved operator detail UI is incomplete. Repair the " +
                "Scene hierarchy and inspector references. " +
                view._layoutError);
        }

        SetNavigationLabel(view._previousButton, "<");
        SetNavigationLabel(view._nextButton, ">");
        view.EnsureGradeIconStrip();
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
        _gradeIcons.SetGrade(model.Grade);
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
            Sprite statIcon = hasStat
                ? model.Stats[index].Icon
                : null;
            _statIcons[index].sprite = statIcon;
            _statIcons[index].enabled = statIcon != null;
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
        _gradeIcons.SetGrade(CharacterGrade.Grade0);
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
            _statIcons[index].sprite = null;
            _statIcons[index].enabled = false;
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
        Transform attack = right?.Find("grpOperatorBasicAttack");
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
        _lobbyRepresentativeLabel =
            representative?.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>();
        _statsTitle = center?.Find("txtOperatorStatsTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _attackTitle = attack?.Find("txtBasicAttackTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _attackSummary = attack?.Find("txtBasicAttackSummary")
            ?.GetComponent<TextMeshProUGUI>();
        _equipmentTitle = center?.Find("txtEquipmentTitle")
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
        _abilityIconPrefab = root
            ?.GetComponent<OperatorDetailDesignerSettings>()
            ?.AbilityIconPrefab;

        for (int index = 0; index < StatCount; index++)
        {
            Transform stat = center?.Find($"grpOperatorStat_{index}");
            _statLabels[index] = stat?.Find("txtStatLabel")
                ?.GetComponent<TextMeshProUGUI>();
            _statValues[index] = stat?.Find("txtStatValue")
                ?.GetComponent<TextMeshProUGUI>();
            _statIcons[index] = stat?.Find("imgStatIcon")
                ?.GetComponent<Image>();
        }

        for (int index = 0; index < EquipmentSlotCount; index++)
        {
            Transform slot = center?.Find($"grpEquipmentSlot_{index}");
            _equipmentLabels[index] = slot?.Find("txtSlotLabel")
                ?.GetComponent<TextMeshProUGUI>();
            _equipmentStatuses[index] = slot?.Find("txtSlotStatus")
                ?.GetComponent<TextMeshProUGUI>();
        }

        List<string> missing = new();
        if (_root == null) missing.Add("Root");
        if (_nameText == null) missing.Add("Name");
        if (_idText == null) missing.Add("Id");
        if (_positionText == null) missing.Add("Position");
        if (_previousButton == null) missing.Add("Previous");
        if (_nextButton == null) missing.Add("Next");
        if (_standingImage == null) missing.Add("StandingImage");
        if (_standingFallback == null) missing.Add("StandingFallback");
        if (_lobbyRepresentativeToggle == null)
            missing.Add("RepresentativeToggle");
        if (_lobbyRepresentativeLabel == null) missing.Add("RepresentativeLabel");
        if (_statsTitle == null) missing.Add("StatsTitle");
        if (_attackTitle == null) missing.Add("AttackTitle");
        if (_attackSummary == null) missing.Add("AttackSummary");
        if (_equipmentTitle == null) missing.Add("EquipmentTitle");
        if (_passiveTitle == null) missing.Add("PassiveTitle");
        if (_passiveSummary == null) missing.Add("PassiveSummary");
        if (_skillTitle == null) missing.Add("SkillTitle");
        if (_skillSummary == null) missing.Add("SkillSummary");
        if (_passiveIconRoot == null) missing.Add("PassiveIconRoot");
        if (_skillIconRoot == null) missing.Add("SkillIconRoot");
        if (_abilityIconPrefab == null) missing.Add("AbilityIconPrefab");
        if (missing.Count > 0)
        {
            _layoutError = "Missing: " + string.Join(", ", missing) + ".";
            return false;
        }

        for (int index = 0; index < StatCount; index++)
        {
            if (_statLabels[index] == null ||
                _statValues[index] == null ||
                _statIcons[index] == null)
            {
                _layoutError = $"Stat {index} references are incomplete.";
                return false;
            }
        }

        for (int index = 0; index < EquipmentSlotCount; index++)
        {
            if (_equipmentLabels[index] == null ||
                _equipmentStatuses[index] == null)
            {
                _layoutError = $"Equipment slot {index} references are incomplete.";
                return false;
            }
        }

        _layoutError = string.Empty;
        return true;
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
                index);
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
        int index)
    {
        while (views.Count <= index)
        {
            int viewIndex = views.Count;
            Transform authored = parent.Find(prefix + viewIndex);
            GameObject instance = authored != null
                ? authored.gameObject
                : UnityEngine.Object.Instantiate(
                    _abilityIconPrefab,
                    parent,
                    false);
            instance.name = prefix + viewIndex;
            views.Add(BindExistingAbilityIcon(instance));
        }

        return views[index];
    }

    private static AbilityIconView BindExistingAbilityIcon(
        GameObject rootObject)
    {
        Button button = rootObject.GetComponent<Button>();
        Image background = rootObject.GetComponent<Image>();
        Image icon = rootObject.transform.Find("imgAbilityIcon")
            ?.GetComponent<Image>();
        TextMeshProUGUI fallback = rootObject.transform
            .Find("txtAbilityFallback")
            ?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI label = rootObject.transform
            .Find("txtAbilityLabel")
            ?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI badge = rootObject.transform
            .Find("txtAbilityBadge")
            ?.GetComponent<TextMeshProUGUI>();
        if (button == null || background == null || icon == null ||
            fallback == null || label == null || badge == null)
        {
            throw new InvalidOperationException(
                "Operator ability icon prefab is incomplete.");
        }
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        return new AbilityIconView(
            rootObject,
            background,
            icon,
            fallback,
            label,
            badge);
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

    private void EnsureGradeIconStrip()
    {
        Transform visual = _root != null
            ? _root.Find("grpOperatorDetailVisual")
            : null;
        if (visual == null)
            return;

        _gradeIcons = CharacterGradeIconStrip.Bind(
            visual,
            "grpOperatorDetailGradeIcons",
            28f,
            6f);
        _gradeIcons.SetGrade(CharacterGrade.Grade0);
    }

}
