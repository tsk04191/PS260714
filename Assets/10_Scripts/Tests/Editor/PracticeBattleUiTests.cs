using System;
using System.Collections.Generic;
using NUnit.Framework;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TestReflection;

public sealed class PracticeBattleUiTests
{
    private readonly List<UnityEngine.Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            if (_created[index] != null)
                UnityEngine.Object.DestroyImmediate(_created[index]);
        }
        _created.Clear();
    }

    [Test]
    public void BindController_HidesStandardBattle_AndRoutesFilteredCatalog()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out RectTransform content,
            out Button controlButton,
            out _,
            out _);
        FakePracticeController standard = new(false);

        Assert.That(panel.BindController(standard), Is.True);
        Assert.That(panel.gameObject.activeSelf, Is.False);
        Assert.That(controlButton.gameObject.activeSelf, Is.False);

        CharacterSO character = CreateCharacter("practice.hero", "Hero");
        EnemySO enemy = CreateEnemy("practice.enemy", "Enemy");
        BattleCardSO card = CreateCard("practice.card", "Card");
        FakePracticeController practice = new(
            true,
            new[] { character },
            new[] { enemy },
            new[] { card });

        Assert.That(panel.BindController(practice), Is.True);
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(controlButton.gameObject.activeSelf, Is.True);
        Assert.That(
            GetField<TextMeshProUGUI>(panel, "collapseText").text,
            Is.EqualTo(ResolveLocalizedOrFallback(
                LocalizationKeys.UiPracticeControl,
                "CONTROL")));
        Assert.That(panel.ActiveCategory,
            Is.EqualTo(PracticeBattleCatalogCategory.Characters));
        Assert.That(panel.VisibleCatalogItemCount, Is.EqualTo(1));

        panel.SetSearchForTests("absent");
        Assert.That(panel.VisibleCatalogItemCount, Is.Zero);
        panel.SetSearchForTests("practice.hero");
        Assert.That(panel.VisibleCatalogItemCount, Is.EqualTo(1));
        ClickOnlyCatalogItem(content);
        Assert.That(practice.LastCharacter, Is.SameAs(character));
        Assert.That(practice.LastCharacterSlot, Is.Zero);

        panel.SetCategoryForTests(
            PracticeBattleCatalogCategory.Enemies);
        panel.SetSearchForTests("enemy");
        Assert.That(panel.VisibleCatalogItemCount, Is.EqualTo(1));
        ClickOnlyCatalogItem(content);
        Assert.That(practice.LastEnemy, Is.SameAs(enemy));
        Assert.That(practice.LastEnemyCount, Is.EqualTo(1));
        Assert.That(practice.LastEnemyQueued, Is.False);

        panel.SetCategoryForTests(PracticeBattleCatalogCategory.Cards);
        panel.SetSearchForTests("practice.card");
        Assert.That(panel.VisibleCatalogItemCount, Is.EqualTo(1));
        ClickOnlyCatalogItem(content);
        Assert.That(practice.LastCard, Is.SameAs(card));
    }

    [Test]
    public void DungeonBattleTab_HidesQueueOnlyWhilePracticeIsBound()
    {
        GameObject tabRoot = CreateRoot(
            "PracticeBattleUiTests_DungeonBattleTab");
        DungeonBattleTab tab = tabRoot.AddComponent<DungeonBattleTab>();
        GameObject queueRoot = CreateChild(tabRoot.transform, "Queue");
        DungeonSpawnQueueView queue =
            queueRoot.AddComponent<DungeonSpawnQueueView>();
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out _,
            out _,
            out _);
        panel.transform.SetParent(tabRoot.transform, false);
        SetField(tab, "spawnQueueView", queue);
        SetField(tab, "practiceBattlePanel", panel);

        Assert.That(
            tab.BindPracticeBattleController(new FakePracticeController(true)),
            Is.True);
        Assert.That(queue.gameObject.activeSelf, Is.False);

        Assert.That(
            tab.BindPracticeBattleController(new FakePracticeController(false)),
            Is.True);
        Assert.That(queue.gameObject.activeSelf, Is.True);

        Assert.That(
            tab.BindPracticeBattleController(new FakePracticeController(true)),
            Is.True);
        Assert.That(queue.gameObject.activeSelf, Is.False);
        tab.Teardown();
        Assert.That(queue.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void Disable_RemovesOnlyPanelListeners_AndRebindsOnce()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out Button collapseButton,
            out Button[] slotButtons,
            out _);
        int collapseDesignerCalls = 0;
        int slotDesignerCalls = 0;
        int debugDesignerCalls = 0;
        collapseButton.onClick.AddListener(() => collapseDesignerCalls++);
        slotButtons[1].onClick.AddListener(() => slotDesignerCalls++);
        Button debugButton = GetField<Button>(panel, "debugButton");
        debugButton.onClick.AddListener(() => debugDesignerCalls++);
        FakePracticeController controller = new(true);

        Assert.That(
            panel.BindController(controller),
            Is.True);
        InvokeMethod(panel, "OnEnable");
        collapseButton.onClick.Invoke();
        slotButtons[1].onClick.Invoke();
        debugButton.onClick.Invoke();
        Assert.That(collapseDesignerCalls, Is.EqualTo(1));
        Assert.That(slotDesignerCalls, Is.EqualTo(1));
        Assert.That(debugDesignerCalls, Is.EqualTo(1));
        Assert.That(panel.IsCollapsed, Is.True);
        Assert.That(panel.SelectedCharacterSlot, Is.EqualTo(1));
        Assert.That(controller.IsDebugVisualizationEnabled, Is.True);
        Assert.That(controller.DebugVisualizationSetCallCount, Is.EqualTo(1));

        panel.gameObject.SetActive(false);
        InvokeMethod(panel, "OnDisable");
        SetField(panel, "_collapsed", false);
        SetField(panel, "_selectedCharacterSlot", 0);
        collapseButton.onClick.Invoke();
        slotButtons[1].onClick.Invoke();
        debugButton.onClick.Invoke();
        Assert.That(collapseDesignerCalls, Is.EqualTo(2));
        Assert.That(slotDesignerCalls, Is.EqualTo(2));
        Assert.That(debugDesignerCalls, Is.EqualTo(2));
        Assert.That(panel.IsCollapsed, Is.False);
        Assert.That(panel.SelectedCharacterSlot, Is.Zero);
        Assert.That(controller.IsDebugVisualizationEnabled, Is.False);
        Assert.That(controller.DebugVisualizationSetCallCount, Is.EqualTo(2));

        panel.gameObject.SetActive(true);
        InvokeMethod(panel, "OnEnable");
        collapseButton.onClick.Invoke();
        slotButtons[1].onClick.Invoke();
        debugButton.onClick.Invoke();
        Assert.That(collapseDesignerCalls, Is.EqualTo(3));
        Assert.That(slotDesignerCalls, Is.EqualTo(3));
        Assert.That(debugDesignerCalls, Is.EqualTo(3));
        Assert.That(panel.IsCollapsed, Is.True);
        Assert.That(panel.SelectedCharacterSlot, Is.EqualTo(1));
        Assert.That(controller.IsDebugVisualizationEnabled, Is.True);
        Assert.That(controller.DebugVisualizationSetCallCount, Is.EqualTo(3));
    }

    [Test]
    public void DebugButton_Toggles_AndReplacementAndUnbindDisableIt()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out _,
            out _,
            out _);
        Button debugButton = GetField<Button>(panel, "debugButton");
        TextMeshProUGUI debugText = GetField<TextMeshProUGUI>(
            panel,
            "debugButtonText");
        LocalizedText staleLocalization =
            debugText.gameObject.AddComponent<LocalizedText>();
        staleLocalization.SetKey(
            LocalizationKeys.UiPracticeDebugOn,
            false);
        FakePracticeController practice = new(true);
        int changedCount = 0;
        practice.Changed += () => changedCount++;

        Assert.That(panel.BindController(practice), Is.True);
        InvokeMethod(panel, "OnEnable");
        Assert.That(staleLocalization.enabled, Is.False);
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOn,
            "DEBUG ON")));

        debugButton.onClick.Invoke();

        Assert.That(practice.IsDebugVisualizationEnabled, Is.True);
        Assert.That(practice.DebugVisualizationSetCallCount, Is.EqualTo(1));
        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOff,
            "DEBUG OFF")));
        practice.NotifyChanged();
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOff,
            "DEBUG OFF")));

        FakePracticeController standard = new(false);
        Assert.That(panel.BindController(standard), Is.True);
        Assert.That(practice.IsDebugVisualizationEnabled, Is.False);
        Assert.That(practice.DebugVisualizationSetCallCount, Is.EqualTo(2));
        Assert.That(changedCount, Is.EqualTo(2));
        Assert.That(standard.DebugVisualizationSetCallCount, Is.Zero);
        Assert.That(panel.gameObject.activeSelf, Is.False);
        InvokeMethod(panel, "OnDisable");

        Assert.That(panel.BindController(practice), Is.True);
        InvokeMethod(panel, "OnEnable");
        debugButton.onClick.Invoke();
        Assert.That(practice.IsDebugVisualizationEnabled, Is.True);
        Assert.That(panel.BindController(null), Is.True);
        Assert.That(practice.IsDebugVisualizationEnabled, Is.False);
        Assert.That(practice.DebugVisualizationSetCallCount, Is.EqualTo(4));
        Assert.That(changedCount, Is.EqualTo(4));
        Assert.That(panel.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void DebugButton_FailedToggleKeepsActualStateAndActionLabel()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out _,
            out _,
            out _);
        Button debugButton = GetField<Button>(panel, "debugButton");
        TextMeshProUGUI debugText = GetField<TextMeshProUGUI>(
            panel,
            "debugButtonText");
        FakePracticeController practice = new(true)
        {
            DebugVisualizationChangesAllowed = false,
        };
        int changedCount = 0;
        practice.Changed += () => changedCount++;

        Assert.That(panel.BindController(practice), Is.True);
        InvokeMethod(panel, "OnEnable");
        debugButton.onClick.Invoke();

        Assert.That(practice.DebugVisualizationSetCallCount, Is.EqualTo(1));
        Assert.That(practice.IsDebugVisualizationEnabled, Is.False);
        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOn,
            "DEBUG ON")));
    }

    [Test]
    public void SceneLocalizationBinder_DoesNotCaptureDebugActionLabel()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out _,
            out _,
            out _);
        FakePracticeController practice = new(true);
        Assert.That(panel.BindController(practice), Is.True);

        TextMeshProUGUI debugText = GetField<TextMeshProUGUI>(
            panel,
            "debugButtonText");
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOn,
            "DEBUG ON")));

        SceneLocalizedTextBinder binder =
            panel.gameObject.AddComponent<SceneLocalizedTextBinder>();
        binder.BindHierarchy();

        Assert.That(debugText.GetComponent<LocalizedText>(), Is.Null);
        Button debugButton = GetField<Button>(panel, "debugButton");
        InvokeMethod(panel, "OnEnable");
        debugButton.onClick.Invoke();
        binder.BindHierarchy();
        Assert.That(debugText.text, Is.EqualTo(ResolveLocalizedOrFallback(
            LocalizationKeys.UiPracticeDebugOff,
            "DEBUG OFF")));
        Assert.That(debugText.GetComponent<LocalizedText>(), Is.Null);
    }

    [Test]
    public void ResetAndExitButtons_DisableVisualizationBeforeRouting()
    {
        PracticeBattlePanelView panel = CreatePanel(
            out _,
            out _,
            out _,
            out _);
        Button debugButton = GetField<Button>(panel, "debugButton");
        Button resetButton = GetField<Button>(panel, "resetPracticeButton");
        Button exitButton = GetField<Button>(panel, "exitPracticeButton");
        FakePracticeController practice = new(true);

        Assert.That(panel.BindController(practice), Is.True);
        InvokeMethod(panel, "OnEnable");
        debugButton.onClick.Invoke();
        resetButton.onClick.Invoke();

        Assert.That(practice.ResetCallCount, Is.EqualTo(1));
        Assert.That(practice.DebugEnabledWhenResetCalled, Is.False);
        Assert.That(practice.IsDebugVisualizationEnabled, Is.False);

        debugButton.onClick.Invoke();
        exitButton.onClick.Invoke();

        Assert.That(practice.ExitCallCount, Is.EqualTo(1));
        Assert.That(practice.DebugEnabledWhenExitCalled, Is.False);
        Assert.That(practice.IsDebugVisualizationEnabled, Is.False);
    }

    private PracticeBattlePanelView CreatePanel(
        out RectTransform catalogContent,
        out Button collapseButton,
        out Button[] slotButtons,
        out TMP_InputField searchInput)
    {
        GameObject root = CreateRoot("PracticeBattleUiTests_Panel");
        root.SetActive(false);
        GameObject body = CreateChild(root.transform, "Body");
        collapseButton = CreateButton(root.transform, "Collapse");
        TextMeshProUGUI collapseText = CreateText(
            collapseButton.transform,
            "CollapseText");
        TextMeshProUGUI statusText = CreateText(
            body.transform,
            "Status");
        searchInput = CreateInput(body.transform);
        Button charactersButton = CreateButton(body.transform, "Characters");
        Button enemiesButton = CreateButton(body.transform, "Enemies");
        Button cardsButton = CreateButton(body.transform, "Cards");

        GameObject scrollObject = CreateChild(body.transform, "Scroll");
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        GameObject contentObject = CreateChild(
            scrollObject.transform,
            "Content");
        catalogContent = contentObject.transform as RectTransform;
        scroll.content = catalogContent;

        PracticeBattleCatalogItemView itemPrefab = CreateCatalogItemPrefab();
        slotButtons = new Button[DungeonPage.MaximumPartySize];
        TextMeshProUGUI[] slotTexts =
            new TextMeshProUGUI[DungeonPage.MaximumPartySize];
        for (int index = 0; index < slotButtons.Length; index++)
        {
            slotButtons[index] = CreateButton(
                body.transform,
                "Slot" + index);
            slotTexts[index] = CreateText(
                slotButtons[index].transform,
                "SlotText" + index);
        }

        Button remove = CreateButton(body.transform, "Remove");
        Button decrease = CreateButton(body.transform, "Decrease");
        Button increase = CreateButton(body.transform, "Increase");
        TextMeshProUGUI count = CreateText(body.transform, "Count");
        Button queue = CreateButton(body.transform, "Queue");
        TextMeshProUGUI queueText = CreateText(
            queue.transform,
            "QueueText");
        Button clear = CreateButton(body.transform, "Clear");
        Button restoreParty = CreateButton(body.transform, "RestoreParty");
        Button restoreCore = CreateButton(body.transform, "RestoreCore");
        Button refill = CreateButton(body.transform, "Refill");
        Button debug = CreateButton(body.transform, "Debug");
        TextMeshProUGUI debugText = CreateText(
            debug.transform,
            "DebugText");
        Button reset = CreateButton(body.transform, "Reset");
        Button exit = CreateButton(body.transform, "Exit");

        PracticeBattlePanelView panel =
            root.AddComponent<PracticeBattlePanelView>();
        SetField(panel, "panelBody", body);
        SetField(panel, "collapseButton", collapseButton);
        SetField(panel, "collapseText", collapseText);
        SetField(panel, "statusText", statusText);
        SetField(panel, "searchInput", searchInput);
        SetField(panel, "charactersButton", charactersButton);
        SetField(panel, "enemiesButton", enemiesButton);
        SetField(panel, "cardsButton", cardsButton);
        SetField(panel, "catalogScroll", scroll);
        SetField(panel, "catalogContent", catalogContent);
        SetField(panel, "catalogItemPrefab", itemPrefab);
        SetField(panel, "characterSlotButtons", slotButtons);
        SetField(panel, "characterSlotTexts", slotTexts);
        SetField(panel, "removeCharacterButton", remove);
        SetField(panel, "decreaseSpawnCountButton", decrease);
        SetField(panel, "increaseSpawnCountButton", increase);
        SetField(panel, "spawnCountText", count);
        SetField(panel, "queueModeButton", queue);
        SetField(panel, "queueModeText", queueText);
        SetField(panel, "clearEnemiesButton", clear);
        SetField(panel, "restorePartyButton", restoreParty);
        SetField(panel, "restoreCoreButton", restoreCore);
        SetField(panel, "refillEnergyButton", refill);
        SetField(panel, "debugButton", debug);
        SetField(panel, "debugButtonText", debugText);
        SetField(panel, "resetPracticeButton", reset);
        SetField(panel, "exitPracticeButton", exit);
        Assert.That(panel.HasDesignerReferences, Is.True);
        return panel;
    }

    private PracticeBattleCatalogItemView CreateCatalogItemPrefab()
    {
        GameObject root = CreateRoot("PracticeBattleUiTests_ItemPrefab");
        root.SetActive(false);
        Image background = root.AddComponent<Image>();
        Button actionButton = root.AddComponent<Button>();
        actionButton.targetGraphic = background;
        Image icon = CreateChild(root.transform, "Icon")
            .AddComponent<Image>();
        TextMeshProUGUI name = CreateText(root.transform, "Name");
        TextMeshProUGUI id = CreateText(root.transform, "Id");
        TextMeshProUGUI action = CreateText(root.transform, "Action");
        PracticeBattleCatalogItemView view =
            root.AddComponent<PracticeBattleCatalogItemView>();
        SetField(view, "actionButton", actionButton);
        SetField(view, "background", background);
        SetField(view, "icon", icon);
        SetField(view, "nameText", name);
        SetField(view, "idText", id);
        SetField(view, "actionText", action);
        Assert.That(view.HasDesignerReferences, Is.True);
        return view;
    }

    private static void ClickOnlyCatalogItem(RectTransform content)
    {
        Assert.That(content.childCount, Is.EqualTo(1));
        PracticeBattleCatalogItemView item = content.GetChild(0)
            .GetComponent<PracticeBattleCatalogItemView>();
        Assert.That(item, Is.Not.Null);
        Button actionButton = GetField<Button>(item, "actionButton");
        actionButton.onClick.Invoke();
    }

    private GameObject CreateRoot(string name)
    {
        GameObject result = new(name, typeof(RectTransform));
        _created.Add(result);
        return result;
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject result = CreateChild(parent, name);
        Image image = result.AddComponent<Image>();
        Button button = result.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name)
    {
        GameObject result = CreateChild(parent, name);
        return result.AddComponent<TextMeshProUGUI>();
    }

    private static TMP_InputField CreateInput(Transform parent)
    {
        GameObject root = CreateChild(parent, "Search");
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        GameObject viewport = CreateChild(root.transform, "Viewport");
        TextMeshProUGUI text = CreateText(viewport.transform, "Text");
        input.textViewport = viewport.transform as RectTransform;
        input.textComponent = text;
        return input;
    }

    private static string ResolveLocalizedOrFallback(
        string key,
        string fallback)
    {
        string resolved = LocalizationService.Get(key);
        return string.IsNullOrWhiteSpace(resolved) ||
               string.Equals(resolved, key, StringComparison.Ordinal)
            ? fallback
            : resolved;
    }

    private CharacterSO CreateCharacter(string id, string displayName)
    {
        CharacterSO result = ScriptableObject.CreateInstance<CharacterSO>();
        _created.Add(result);
        SetField(result, "characterId", id);
        SetField(result, "characterName", displayName);
        return result;
    }

    private EnemySO CreateEnemy(string id, string displayName)
    {
        EnemySO result = ScriptableObject.CreateInstance<EnemySO>();
        _created.Add(result);
        SetField(result, "enemyId", id);
        SetField(result, "displayName", displayName);
        return result;
    }

    private BattleCardSO CreateCard(string id, string displayName)
    {
        BattleCardSO result = ScriptableObject.CreateInstance<BattleCardSO>();
        _created.Add(result);
        SetField(result, "cardId", id);
        SetField(result, "fallbackName", displayName);
        return result;
    }

    private sealed class FakePracticeController : IPracticeBattleController
    {
        public FakePracticeController(
            bool isPracticeBattle,
            IReadOnlyList<CharacterSO> characters = null,
            IReadOnlyList<EnemySO> enemies = null,
            IReadOnlyList<BattleCardSO> cards = null)
        {
            IsPracticeBattle = isPracticeBattle;
            CharacterCatalog = characters ?? Array.Empty<CharacterSO>();
            EnemyCatalog = enemies ?? Array.Empty<EnemySO>();
            CardCatalog = cards ?? Array.Empty<BattleCardSO>();
        }

        public bool IsPracticeBattle { get; }
        public bool IsDebugVisualizationEnabled { get; private set; }
        public IReadOnlyList<CharacterSO> CharacterCatalog { get; }
        public IReadOnlyList<EnemySO> EnemyCatalog { get; }
        public IReadOnlyList<BattleCardSO> CardCatalog { get; }
        public IReadOnlyList<CharacterRuntime> ActiveCharacters =>
            Array.Empty<CharacterRuntime>();
        public string LastMessageKey => string.Empty;
        public CharacterSO LastCharacter { get; private set; }
        public int LastCharacterSlot { get; private set; } = -1;
        public EnemySO LastEnemy { get; private set; }
        public int LastEnemyCount { get; private set; }
        public bool LastEnemyQueued { get; private set; }
        public BattleCardSO LastCard { get; private set; }
        public int DebugVisualizationSetCallCount { get; private set; }
        public bool DebugVisualizationChangesAllowed { get; set; } = true;
        public int ResetCallCount { get; private set; }
        public int ExitCallCount { get; private set; }
        public bool DebugEnabledWhenResetCalled { get; private set; }
        public bool DebugEnabledWhenExitCalled { get; private set; }

        public event Action Changed;

        public bool TrySetCharacter(CharacterSO definition, int slotIndex)
        {
            LastCharacter = definition;
            LastCharacterSlot = slotIndex;
            return true;
        }

        public bool TryRemoveCharacter(int slotIndex) => true;
        public bool TryPlaceCharacter(int slotIndex, Vector2 worldPoint) =>
            true;

        public bool TrySpawnEnemy(
            EnemySO definition,
            int count,
            bool queue)
        {
            LastEnemy = definition;
            LastEnemyCount = count;
            LastEnemyQueued = queue;
            return true;
        }

        public bool TryAddCard(BattleCardSO definition)
        {
            LastCard = definition;
            return true;
        }

        public bool TryClearEnemies() => true;
        public bool TryRestoreParty() => true;
        public bool TryRestoreCore() => true;
        public bool TryRefillEnergy() => true;

        public bool TrySetDebugVisualization(bool enabled)
        {
            DebugVisualizationSetCallCount++;
            if (enabled && !IsPracticeBattle)
                return false;
            if (!DebugVisualizationChangesAllowed)
            {
                Changed?.Invoke();
                return false;
            }
            IsDebugVisualizationEnabled = enabled;
            Changed?.Invoke();
            return true;
        }

        public bool TryResetPractice()
        {
            ResetCallCount++;
            DebugEnabledWhenResetCalled = IsDebugVisualizationEnabled;
            return true;
        }

        public void ExitPractice()
        {
            ExitCallCount++;
            DebugEnabledWhenExitCalled = IsDebugVisualizationEnabled;
        }

        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
