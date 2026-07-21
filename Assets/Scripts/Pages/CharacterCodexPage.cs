using System;
using System.Collections.Generic;
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
    private TextMeshProUGUI _normalAttackText;
    private TextMeshProUGUI _skillTitleText;
    private TextMeshProUGUI _skillText;
    private int _selectedIndex;

    protected override string PageTitle => "CHARACTER CODEX";
    protected override string PageDescription =>
        "SELECT A CHARACTER TAB TO VIEW ITS INFORMATION";
    protected override Vector2 PanelSize => new(1220f, 900f);

    protected override void BuildButtons()
    {
        RefreshEntries();
        BuildCharacterTabStrip();
        BuildDetailPanel();
        CreateMenuButton("BACK TO CODEX", HandleBackClicked);

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
            left.Data.CharacterName,
            right.Data.CharacterName,
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
                entry.Data.CharacterName,
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
        CreateContentText(
            detailObject.transform,
            "txtNormalAttackTitle",
            "NORMAL ATTACK",
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

        _detailTitle.text = data.CharacterName;
        _identityText.text =
            $"ASSET {entry.AssetName}   |   " +
            $"TYPE {GetAttackTypeName(data.AttackType)}";
        _statText.text = GetStatDescription(data);
        _normalAttackText.text = GetNormalAttackDescription(data);
        _skillTitleText.text =
            $"ACTIVE SKILL   [COST {data.ActiveSkillCost}]";
        _skillText.text = GetActiveSkillDescription(data);
    }

    private void ShowEmptyState()
    {
        if (_detailTitle != null)
            _detailTitle.text = "NO CHARACTER DATA";
        if (_identityText != null)
            _identityText.text = string.Empty;
        if (_statText != null)
            _statText.text = "NO CHARACTER DEFINITIONS ARE AVAILABLE.";
        if (_normalAttackText != null)
            _normalAttackText.text = string.Empty;
        if (_skillTitleText != null)
            _skillTitleText.text = "ACTIVE SKILL";
        if (_skillText != null)
            _skillText.text = string.Empty;
    }

    private static string GetStatDescription(CharacterData data)
    {
        if (data.AttackType == CharacterAttackType.FireRandom)
        {
            return $"ATTACK COOLDOWN  {data.AttackCooldown:0.#}s     " +
                   $"FIRE DURATION  {data.FireDuration:0.#}s\n" +
                   $"FIRE TICK  {data.FireTickDamage} DAMAGE / " +
                   $"{data.FireTickInterval:0.#}s     " +
                   $"POWER WEIGHT  x{data.AttackWeight:0.##}";
        }

        return $"ATTACK DAMAGE  {data.AttackDamage}     " +
               $"ATTACK COOLDOWN  {data.AttackCooldown:0.#}s\n" +
               $"SKILL DAMAGE  {data.SkillAttackDamage}     " +
               $"POWER WEIGHT  x{data.AttackWeight:0.##}";
    }

    private static string GetNormalAttackDescription(CharacterData data)
    {
        return data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                $"ATTACKS {data.TargetCount} RANDOM PRIORITY TARGETS " +
                $"FOR {data.AttackDamage} DAMAGE EACH.",
            CharacterAttackType.CrossHighestHealth =>
                "ATTACKS THE CROSS-SHAPED AREA AROUND THE " +
                $"HIGHEST-HEALTH ENEMY FOR {data.AttackDamage} DAMAGE.",
            CharacterAttackType.FireRandom =>
                "APPLIES FIRE TO ONE RANDOM PRIORITY TARGET FOR " +
                $"{data.FireDuration:0.#}s. FIRE DEALS " +
                $"{data.FireTickDamage} DAMAGE EVERY " +
                $"{data.FireTickInterval:0.#}s.",
            _ =>
                $"DEALS {data.AttackDamage} DAMAGE TO THE " +
                "LOWEST-HEALTH PRIORITY TARGET.",
        };
    }

    private static string GetActiveSkillDescription(CharacterData data)
    {
        return data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                $"FOR {data.ActiveSkillDuration:0.#}s, ATTACKS TARGET " +
                $"{data.TargetCount + 2} ENEMIES AND DEAL " +
                $"{data.SkillAttackDamage} DAMAGE EACH.",
            CharacterAttackType.CrossHighestHealth =>
                $"THE NEXT {data.ActiveSkillAttackCount} ATTACKS DEAL " +
                $"{data.SkillAttackDamage} INNER-CROSS DAMAGE AND " +
                $"{Mathf.Max(1, Mathf.FloorToInt(data.SkillAttackDamage * 0.5f))} " +
                "OUTER AND DIAGONAL DAMAGE.",
            CharacterAttackType.FireRandom =>
                $"THE NEXT {data.ActiveSkillAttackCount} ATTACKS CHOOSE " +
                $"{data.FireSkillTargetCount} CENTERS AND APPLY FIRE " +
                $"FOR {data.FireDuration:0.#}s IN EACH 3x3 AREA. " +
                "OVERLAPPING AREAS STACK DURATION.",
            _ =>
                $"DEALS {data.SkillAttackDamage} DAMAGE TO THE " +
                "LOWEST-HEALTH ENEMY IMMEDIATELY.",
        };
    }

    private static string GetAttackTypeName(CharacterAttackType attackType)
    {
        return attackType switch
        {
            CharacterAttackType.RandomMultiple => "RANDOM MULTIPLE",
            CharacterAttackType.CrossHighestHealth => "CROSS AREA",
            CharacterAttackType.FireRandom => "FIRE STATUS",
            _ => "LOWEST HEALTH",
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
