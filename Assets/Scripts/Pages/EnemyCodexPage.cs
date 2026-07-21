using System;
using System.Collections.Generic;
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
        public float InitialArmorMultiplier { get; }
        public int GuardedHitCount { get; }
        public int CompanionSpawnCount { get; }
        public float AbilityCooldown { get; }
        public int AbilityPower { get; }
        public float DisableDuration { get; }

        public EnemyCodexEntry(EnemySO definition)
        {
            EnemyId = definition.EnemyId;
            DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? EnemyTypeDisplay.GetName(definition.Type)
                : definition.DisplayName;
            CardCode = definition.CardCode;
            Grade = definition.Grade;
            Type = definition.Type;
            BaseHealth = definition.BaseHealth;
            SpawnIntervalMultiplier = definition.SpawnIntervalMultiplier;
            ThreatCost = definition.ThreatCost;
            TargetPriorityExcluded = definition.TargetPriorityExcluded;
            InitialArmorMultiplier = definition.InitialArmorMultiplier;
            GuardedHitCount = definition.GuardedHitCount;
            CompanionSpawnCount = definition.CompanionSpawnCount;
            AbilityCooldown = definition.AbilityCooldown;
            AbilityPower = definition.AbilityPower;
            DisableDuration = definition.DisableDuration;
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
    private TextMeshProUGUI _abilityText;
    private int _selectedIndex;

    protected override string PageTitle => "ENEMY CODEX";
    protected override string PageDescription =>
        "SELECT AN ENEMY TAB TO VIEW ITS INFORMATION";
    protected override Vector2 PanelSize => new(1220f, 860f);

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildEnemyTabStrip();
        BuildDetailPanel();
        CreateMenuButton("BACK TO CODEX", HandleBackClicked);

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
        CreateContentText(
            detailObject.transform,
            "txtAbilityTitle",
            "ABILITY",
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
        _identityText.text =
            $"ID {entry.EnemyId}   |   " +
            $"GRADE {entry.Grade.ToString().ToUpperInvariant()}   |   " +
            $"TYPE {EnemyTypeDisplay.GetName(entry.Type)}";
        _statText.text =
            $"BASE HEALTH  {entry.BaseHealth}     " +
            $"THREAT  {entry.ThreatCost:0.##}\n" +
            $"SPAWN INTERVAL  x{entry.SpawnIntervalMultiplier:0.##}     " +
            $"TARGET PRIORITY  " +
            $"{(entry.TargetPriorityExcluded ? "EXCLUDED" : "NORMAL")}";
        _abilityText.text = GetAbilityDescription(entry);
    }

    private void ShowEmptyState()
    {
        if (_detailTitle != null)
            _detailTitle.text = "NO ENEMY DATA";
        if (_identityText != null)
            _identityText.text = string.Empty;
        if (_statText != null)
            _statText.text = "NO ENEMY DEFINITIONS ARE AVAILABLE.";
        if (_abilityText != null)
            _abilityText.text = string.Empty;
    }

    private static string GetAbilityDescription(EnemyCodexEntry entry)
    {
        return entry.Type switch
        {
            EEnemyType.Assault =>
                $"RAPID DEPLOYMENT\n" +
                $"SPAWN INTERVAL IS MULTIPLIED BY " +
                $"{entry.SpawnIntervalMultiplier:0.##}.",
            EEnemyType.Heavy =>
                $"GUARD\nTHE FIRST {entry.GuardedHitCount} HITS " +
                "ARE REDUCED TO 1 DAMAGE.",
            EEnemyType.Medic =>
                $"FIELD HEAL\nEVERY {entry.AbilityCooldown:0.#}s, " +
                $"HEALS EACH ORTHOGONALLY ADJACENT ENEMY BY " +
                $"{entry.AbilityPower}.",
            EEnemyType.Mechanic =>
                $"SYSTEM DISABLE\nEVERY {entry.AbilityCooldown:0.#}s, " +
                "DISABLES THE HIGHEST-DAMAGE TURRET FOR " +
                $"{entry.DisableDuration:0.#}s.",
            EEnemyType.Pointman =>
                $"FORMATION ENTRY\nSPAWNS TOGETHER WITH " +
                $"{entry.CompanionSpawnCount} COMPANIONS.",
            EEnemyType.ShieldBearer =>
                $"SHIELD FORMATION\nSTARTS WITH " +
                $"{entry.InitialArmorMultiplier * 100f:0.#}% MAX HP " +
                "AS ARMOR AND TAKES DAMAGE FOR ADJACENT ENEMIES.",
            EEnemyType.Infiltrator =>
                "STEALTH\nEXCLUDED FROM NORMAL TARGET PRIORITY " +
                "WHILE ANOTHER VALID TARGET EXISTS.",
            _ => "NO SPECIAL ABILITY.",
        };
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
