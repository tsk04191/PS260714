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
    private readonly List<Button> _tabButtons = new();
    private Image _detailPanelImage;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _identityText;
    private TextMeshProUGUI _statText;
    private TextMeshProUGUI _abilityTitleText;
    private TextMeshProUGUI _abilityText;
    private Button _backButton;
    private int _selectedIndex;

    protected override string PageTitle => LocalizationService.Get(
        LocalizationKeys.CodexEnemyTitle);
    protected override string PageDescription =>
        LocalizationService.Get(LocalizationKeys.CodexEnemyDescription);
    protected override Vector2 PanelSize => new(1220f, 860f);

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildEnemyTabStrip();
        BuildDetailPanel();
        _backButton = CreateStyledButton(
            ButtonRoot,
            "btnBACKTOCODEX",
            LocalizationService.Get(LocalizationKeys.UiCommonBack),
            HandleBackClicked,
            72f);

        if (_entries.Count > 0)
            SelectEnemy(Mathf.Clamp(_selectedIndex, 0, _entries.Count - 1));
        else
            ShowEmptyState();
    }

    private void RefreshEntries()
    {
        _entries.Clear();
        HashSet<EnemySO> uniqueDefinitions = new();
        AddDefinitions(enemyDefinitions, uniqueDefinitions);

        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            AddDefinitions(
                dungeon.GetCodexEnemyDefinitions(),
                uniqueDefinitions);
        }

        HashSet<EEnemyType> registeredTypes = new();
        foreach (EnemySO definition in uniqueDefinitions)
        {
            _entries.Add(new EnemyCodexEntry(definition));
            registeredTypes.Add(definition.Type);
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
        HashSet<EnemySO> uniqueDefinitions)
    {
        if (definitions == null)
            return;

        foreach (EnemySO definition in definitions)
        {
            if (definition != null)
                uniqueDefinitions.Add(definition);
        }
    }

    private void BuildEnemyTabStrip()
    {
        GameObject tabStripObject = GetOrCreateChild(
            ButtonRoot,
            "grpEnemyTabStrip",
            typeof(RectTransform),
            typeof(ScrollRect),
            typeof(LayoutElement));
        LayoutElement stripLayout =
            tabStripObject.GetComponent<LayoutElement>();
        stripLayout.preferredHeight = 70f;

        GameObject viewportObject = GetOrCreateChild(
            tabStripObject.transform,
            "vptEnemyTabs",
            typeof(RectTransform),
            typeof(RectMask2D));
        StretchToParent((RectTransform)viewportObject.transform);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpEnemyTabContent",
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
            EnemyCodexEntry entry = _entries[index];
            Button button = CreateStyledButton(
                contentObject.transform,
                $"btnEnemyTab_{index}",
                entry.DisplayName,
                () => SelectEnemy(selectedIndex),
                60f);
            LayoutElement buttonLayout =
                button.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 132f;
            buttonLayout.preferredWidth = 156f;
            buttonLayout.flexibleWidth = 0f;
            _tabButtons.Add(button);
        }

        SyncIndexedChildren(
            contentObject.transform,
            "btnEnemyTab_",
            _entries.Count);
    }

    private void BuildDetailPanel()
    {
        GameObject detailObject = GetOrCreateChild(
            ButtonRoot,
            "grpEnemyDetail",
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

    private void SelectEnemy(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;

        _selectedIndex = index;
        EnemyCodexEntry entry = _entries[index];
        Color accentColor = GetGradeColor(entry.Grade);
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
        BuildEnemyTabStrip();
        for (int index = 0;
             index < _tabButtons.Count && index < _entries.Count;
             index++)
        {
            TextMeshProUGUI label = _tabButtons[index]
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
                continue;

            label.text = _entries[index].DisplayName;
            ApplyLocalizedFont(label, "title");
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

        if (_entries.Count > 0)
        {
            SelectEnemy(Mathf.Clamp(
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
        LocalizationFontResolver.ApplyGameDefault(text, fontRole);
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
