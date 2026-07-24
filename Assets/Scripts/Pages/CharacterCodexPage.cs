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
        public string AssetName { get; }
        public CharacterSO Definition { get; }
        public CharacterData Data { get; }

        public CharacterCodexEntry(CharacterSO definition)
        {
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
    private readonly List<Button> _tabButtons = new();
    private Image _detailPanelImage;
    private Image _standingImage;
    private Image _iconImage;
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
    private int _selectedIndex;

    protected override string PageTitle => "CHARACTER CODEX";
    protected override string PageDescription =>
        "SELECT A CHARACTER TAB TO VIEW ITS INFORMATION";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.CodexCharacterTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.CodexCharacterDescription;
    protected override Vector2 PanelSize => new(1220f, 900f);

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildCharacterTabStrip();
        BuildDetailPanel();
        CreateLocalizedMenuButton(
            "btnBACKTOCODEX",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);

        if (_entries.Count > 0)
        {
            SelectCharacter(Mathf.Clamp(
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

    private void BuildCharacterTabStrip()
    {
        GameObject tabStripObject = GetOrCreateChild(
            ButtonRoot,
            "grpCharacterTabStrip",
            typeof(RectTransform),
            typeof(ScrollRect),
            typeof(LayoutElement));
        LayoutElement stripLayout =
            tabStripObject.GetComponent<LayoutElement>();
        stripLayout.preferredHeight = 70f;

        GameObject viewportObject = GetOrCreateChild(
            tabStripObject.transform,
            "vptCharacterTabs",
            typeof(RectTransform),
            typeof(RectMask2D));
        StretchToParent((RectTransform)viewportObject.transform);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpCharacterTabContent",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform contentRect =
            (RectTransform)contentObject.transform;
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
            CharacterCodexEntry entry = _entries[index];
            Button button = CreateStyledButton(
                contentObject.transform,
                $"btnCharacterTab_{index}",
                GetCharacterTabLabel(entry.Data),
                () => SelectCharacter(selectedIndex),
                60f);
            LayoutElement buttonLayout =
                button.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 156f;
            buttonLayout.preferredWidth = 188f;
            buttonLayout.flexibleWidth = 0f;
            _tabButtons.Add(button);
        }

        SyncIndexedChildren(
            contentObject.transform,
            "btnCharacterTab_",
            _entries.Count);
    }

    private void BuildDetailPanel()
    {
        GameObject detailObject = GetOrCreateChild(
            ButtonRoot,
            "grpCharacterDetail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        _detailPanelImage = detailObject.GetComponent<Image>();
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

        GameObject visualObject = GetOrCreateChild(
            detailObject.transform,
            "grpCharacterVisuals",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        LayoutElement visualLayout = visualObject.GetComponent<LayoutElement>();
        visualLayout.minWidth = 170f;
        visualLayout.preferredWidth = 190f;
        visualLayout.flexibleWidth = 0f;
        VerticalLayoutGroup visualGroup =
            visualObject.GetComponent<VerticalLayoutGroup>();
        visualGroup.spacing = 12f;
        visualGroup.childAlignment = TextAnchor.UpperCenter;
        visualGroup.childControlWidth = true;
        visualGroup.childControlHeight = true;
        visualGroup.childForceExpandWidth = false;
        visualGroup.childForceExpandHeight = false;

        _standingImage = CreateProfileImage(
            visualObject.transform,
            "imgCharacterStanding",
            160f,
            270f);
        _iconImage = CreateProfileImage(
            visualObject.transform,
            "imgCharacterIcon",
            112f,
            112f);

        GameObject scrollObject = GetOrCreateChild(
            detailObject.transform,
            "scrCharacterDetails",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        Image scrollRaycastImage = scrollObject.GetComponent<Image>();
        scrollRaycastImage.color = new Color(0f, 0f, 0f, 0.01f);
        scrollRaycastImage.raycastTarget = true;
        LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
        scrollLayout.flexibleWidth = 1f;
        scrollLayout.flexibleHeight = 1f;

        GameObject viewportObject = GetOrCreateChild(
            scrollObject.transform,
            "vptCharacterDetails",
            typeof(RectTransform),
            typeof(RectMask2D));
        StretchToParent((RectTransform)viewportObject.transform);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpCharacterDetailContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)contentObject.transform;
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
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        _detailScrollRect = scrollRect;
        scrollRect.viewport = (RectTransform)viewportObject.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.inertia = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

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
            MoveExistingChild(
                detailObject.transform,
                contentObject.transform,
                textName);
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

    private void SelectCharacter(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;

        _selectedIndex = index;
        CharacterCodexEntry entry = _entries[index];
        CharacterData data = entry.Data;
        Color accentColor = new(0.22f, 0.48f, 0.68f, 1f);
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

        SetProfileImage(_standingImage, data.StandingSprite);
        SetProfileImage(_iconImage, data.IconSprite);

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
        SetProfileImage(_iconImage, null);
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
        BuildCharacterTabStrip();

        for (int index = 0;
             index < _tabButtons.Count && index < _entries.Count;
             index++)
        {
            TextMeshProUGUI label = _tabButtons[index]
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = GetCharacterTabLabel(_entries[index].Data);
            }
        }

        if (_entries.Count > 0)
        {
            SelectCharacter(Mathf.Clamp(
                _selectedIndex,
                0,
                _entries.Count - 1));
        }
        else
        {
            ShowEmptyState();
        }
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

    private static string GetCharacterTabLabel(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return (data.IsOwned ? "● " : "○ ") +
               CharacterLocalization.GetName(data);
    }

    private static Image CreateProfileImage(
        Transform parent,
        string objectName,
        float preferredWidth,
        float preferredHeight)
    {
        GameObject imageObject = GetOrCreateChild(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        LayoutElement layout = imageObject.GetComponent<LayoutElement>();
        layout.minWidth = preferredWidth;
        layout.preferredWidth = preferredWidth;
        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
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
