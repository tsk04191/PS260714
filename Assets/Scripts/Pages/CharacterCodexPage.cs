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
        public CharacterData Data { get; }

        public CharacterCodexEntry(CharacterSO definition)
        {
            AssetName = definition.name;
            Data = definition.CreateData();
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
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _identityText;
    private TextMeshProUGUI _statText;
    private TextMeshProUGUI _normalAttackTitleText;
    private TextMeshProUGUI _normalAttackText;
    private TextMeshProUGUI _skillTitleText;
    private TextMeshProUGUI _skillText;
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
        _entries.Clear();
        HashSet<CharacterSO> uniqueDefinitions = new();
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
                CharacterLocalization.GetName(entry.Data),
                () => SelectCharacter(selectedIndex),
                60f);
            LayoutElement buttonLayout =
                button.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 156f;
            buttonLayout.preferredWidth = 188f;
            buttonLayout.flexibleWidth = 0f;
            _tabButtons.Add(button);
        }
    }

    private void BuildDetailPanel()
    {
        GameObject detailObject = GetOrCreateChild(
            ButtonRoot,
            "grpCharacterDetail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _detailPanelImage = detailObject.GetComponent<Image>();
        _detailPanelImage.color = PanelColor;
        _detailPanelImage.raycastTarget = false;

        LayoutElement detailLayout =
            detailObject.GetComponent<LayoutElement>();
        detailLayout.preferredHeight = 430f;
        detailLayout.flexibleHeight = 1f;

        VerticalLayoutGroup layout =
            detailObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 20, 20);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _detailTitle = CreateContentText(
            detailObject.transform,
            "txtCharacterName",
            string.Empty,
            38f,
            54f,
            FontStyles.Bold);
        _identityText = CreateContentText(
            detailObject.transform,
            "txtCharacterIdentity",
            string.Empty,
            20f,
            38f,
            FontStyles.Bold);
        _statText = CreateContentText(
            detailObject.transform,
            "txtCharacterStats",
            string.Empty,
            21f,
            72f);
        _normalAttackTitleText = CreateContentText(
            detailObject.transform,
            "txtNormalAttackTitle",
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack),
            19f,
            28f,
            FontStyles.Bold);
        _normalAttackText = CreateContentText(
            detailObject.transform,
            "txtNormalAttack",
            string.Empty,
            20f,
            66f);
        _skillTitleText = CreateContentText(
            detailObject.transform,
            "txtActiveSkillTitle",
            string.Empty,
            19f,
            28f,
            FontStyles.Bold);
        _skillText = CreateContentText(
            detailObject.transform,
            "txtActiveSkill",
            string.Empty,
            20f,
            90f);
    }

    private void SelectCharacter(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;

        _selectedIndex = index;
        CharacterCodexEntry entry = _entries[index];
        CharacterData data = entry.Data;
        Color accentColor = GetAttackTypeColor(data.AttackType);
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

        _detailTitle.text = CharacterLocalization.GetName(data);
        _identityText.text = CharacterLocalization.GetIdentity(
            entry.AssetName,
            data.AttackType);
        _statText.text = CharacterLocalization.GetStats(data);
        _normalAttackText.text =
            CharacterLocalization.GetNormalAttackDescription(data);
        _skillTitleText.text =
            CharacterLocalization.GetActiveSkillTitle(data.ActiveSkillCost);
        _skillText.text =
            CharacterLocalization.GetActiveSkillDescription(data);
    }

    private void ShowEmptyState()
    {
        if (_detailTitle != null)
        {
            _detailTitle.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterEmptyTitle);
        }
        if (_identityText != null)
            _identityText.text = string.Empty;
        if (_statText != null)
        {
            _statText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterEmptyBody);
        }
        if (_normalAttackText != null)
            _normalAttackText.text = string.Empty;
        if (_skillTitleText != null)
        {
            _skillTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterActiveSkill);
        }
        if (_skillText != null)
            _skillText.text = string.Empty;
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        RefreshLocalizedView();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshLocalizedView();
    }

    private void RefreshLocalizedView()
    {
        if (_detailTitle == null)
            return;

        if (_normalAttackTitleText != null)
        {
            _normalAttackTitleText.text = LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack);
        }

        for (int index = 0;
             index < _tabButtons.Count && index < _entries.Count;
             index++)
        {
            TextMeshProUGUI label = _tabButtons[index]
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = CharacterLocalization.GetName(_entries[index].Data);
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

    private static Color GetAttackTypeColor(CharacterAttackType attackType)
    {
        return attackType switch
        {
            CharacterAttackType.RandomMultiple =>
                new Color(0.5f, 0.34f, 0.72f, 1f),
            CharacterAttackType.CrossHighestHealth =>
                new Color(0.22f, 0.62f, 0.44f, 1f),
            CharacterAttackType.FireRandom =>
                new Color(0.82f, 0.3f, 0.14f, 1f),
            _ => new Color(0.22f, 0.48f, 0.68f, 1f),
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
}
