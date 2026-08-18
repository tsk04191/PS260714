using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PracticeBattlePanelView : MonoBehaviour
{
    private const int MinimumSpawnCount = 1;
    private const int MaximumSpawnCount = 20;

    [Header("Panel")]
    [SerializeField] private GameObject panelBody;
    [SerializeField] private Button collapseButton;
    [SerializeField] private TextMeshProUGUI collapseText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Search and Categories")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button charactersButton;
    [SerializeField] private Button enemiesButton;
    [SerializeField] private Button cardsButton;

    [Header("Catalog")]
    [SerializeField] private ScrollRect catalogScroll;
    [SerializeField] private RectTransform catalogContent;
    [SerializeField]
    private PracticeBattleCatalogItemView catalogItemPrefab;

    [Header("Character Slots")]
    [SerializeField] private Button[] characterSlotButtons =
        new Button[DungeonPage.MaximumPartySize];
    [SerializeField] private TextMeshProUGUI[] characterSlotTexts =
        new TextMeshProUGUI[DungeonPage.MaximumPartySize];
    [SerializeField] private Button removeCharacterButton;

    [Header("Enemy Spawn")]
    [SerializeField] private Button decreaseSpawnCountButton;
    [SerializeField] private Button increaseSpawnCountButton;
    [SerializeField] private TextMeshProUGUI spawnCountText;
    [SerializeField] private Button queueModeButton;
    [SerializeField] private TextMeshProUGUI queueModeText;

    [Header("Actions")]
    [SerializeField] private Button clearEnemiesButton;
    [SerializeField] private Button restorePartyButton;
    [SerializeField] private Button restoreCoreButton;
    [SerializeField] private Button refillEnergyButton;
    [SerializeField] private Button debugButton;
    [SerializeField] private TextMeshProUGUI debugButtonText;
    [SerializeField] private Button resetPracticeButton;
    [SerializeField] private Button exitPracticeButton;

    private readonly List<PracticeBattleCatalogItemView> _catalogItems =
        new();
    private IPracticeBattleController _controller;
    private PracticeBattleCatalogCategory _category =
        PracticeBattleCatalogCategory.Characters;
    private int _selectedCharacterSlot;
    private bool _characterSlotExplicitlySelected;
    private int _spawnCount = 1;
    private bool _queueEnemy;
    private bool _collapsed;
    private bool _uiEventsBound;
    private bool _controllerEventsBound;
    private bool _localizationEventsBound;
    private UnityAction[] _characterSlotClickActions;

    public IPracticeBattleController Controller => _controller;
    public PracticeBattleCatalogCategory ActiveCategory => _category;
    public int SelectedCharacterSlot => _selectedCharacterSlot;
    public int SpawnCount => _spawnCount;
    public bool QueueEnemy => _queueEnemy;
    public bool IsCollapsed => _collapsed;
    public int VisibleCatalogItemCount => _catalogItems.Count;
    public bool HasDesignerReferences =>
        TryValidateDesignerReferences(out _);

    public bool TryValidateDesignerReferences(out string error)
    {
        List<string> missing = new();
        AddMissing(missing, panelBody, nameof(panelBody));
        AddMissing(missing, collapseButton, nameof(collapseButton));
        AddMissing(missing, collapseText, nameof(collapseText));
        AddMissing(missing, statusText, nameof(statusText));
        AddMissing(missing, searchInput, nameof(searchInput));
        AddMissing(missing, charactersButton, nameof(charactersButton));
        AddMissing(missing, enemiesButton, nameof(enemiesButton));
        AddMissing(missing, cardsButton, nameof(cardsButton));
        AddMissing(missing, catalogScroll, nameof(catalogScroll));
        AddMissing(missing, catalogContent, nameof(catalogContent));
        AddMissing(missing, catalogItemPrefab, nameof(catalogItemPrefab));
        if (catalogItemPrefab != null &&
            !catalogItemPrefab.HasDesignerReferences)
        {
            missing.Add(nameof(catalogItemPrefab) + ".references");
        }
        if (!HasPartySlotReferences())
            missing.Add("characterSlotReferences");
        AddMissing(
            missing,
            removeCharacterButton,
            nameof(removeCharacterButton));
        AddMissing(
            missing,
            decreaseSpawnCountButton,
            nameof(decreaseSpawnCountButton));
        AddMissing(
            missing,
            increaseSpawnCountButton,
            nameof(increaseSpawnCountButton));
        AddMissing(missing, spawnCountText, nameof(spawnCountText));
        AddMissing(missing, queueModeButton, nameof(queueModeButton));
        AddMissing(missing, queueModeText, nameof(queueModeText));
        AddMissing(
            missing,
            clearEnemiesButton,
            nameof(clearEnemiesButton));
        AddMissing(
            missing,
            restorePartyButton,
            nameof(restorePartyButton));
        AddMissing(
            missing,
            restoreCoreButton,
            nameof(restoreCoreButton));
        AddMissing(
            missing,
            refillEnergyButton,
            nameof(refillEnergyButton));
        AddMissing(missing, debugButton, nameof(debugButton));
        AddMissing(
            missing,
            debugButtonText,
            nameof(debugButtonText));
        AddMissing(
            missing,
            resetPracticeButton,
            nameof(resetPracticeButton));
        AddMissing(
            missing,
            exitPracticeButton,
            nameof(exitPracticeButton));

        error = missing.Count == 0
            ? string.Empty
            : string.Join(", ", missing);
        return missing.Count == 0;
    }

    private void OnEnable()
    {
        if (!HasDesignerReferences)
        {
            Debug.LogError(
                "Practice battle panel scene references are incomplete.",
                this);
            return;
        }

        BindUiEvents();
        BindControllerEvents();
        BindLocalizationEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindControllerEvents();
        DisableDebugVisualization(_controller);
        UnbindUiEvents();
        UnbindLocalizationEvents();
        ClearCatalogItems();
    }

    public bool BindController(IPracticeBattleController controller)
    {
        if (!HasDesignerReferences)
        {
            Debug.LogError(
                "Practice battle panel scene references are incomplete.",
                this);
            return false;
        }

        if (!ReferenceEquals(_controller, controller))
        {
            UnbindControllerEvents();
            DisableDebugVisualization(_controller);
            _controller = controller;
        }

        bool visible = _controller?.IsPracticeBattle == true;
        if (!visible)
            DisableDebugVisualization(_controller);
        if (collapseButton != null &&
            collapseButton.gameObject.activeSelf != visible)
        {
            collapseButton.gameObject.SetActive(visible);
        }
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
        if (!visible)
            return true;

        BindControllerEvents();
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (_controller?.IsPracticeBattle != true ||
            !HasDesignerReferences)
        {
            return;
        }

        panelBody.SetActive(!_collapsed);
        collapseText.text = LocalizationService.Get(
            LocalizationKeys.UiPracticeControl);
        charactersButton.interactable =
            _category != PracticeBattleCatalogCategory.Characters;
        enemiesButton.interactable =
            _category != PracticeBattleCatalogCategory.Enemies;
        cardsButton.interactable =
            _category != PracticeBattleCatalogCategory.Cards;
        RefreshCharacterSlots();
        RefreshSpawnControls();
        RefreshDebugControl();
        RefreshStatus();
        RebuildCatalog();
    }

    internal void SetCategoryForTests(
        PracticeBattleCatalogCategory category)
    {
        SetCategory(category);
    }

    internal void SetSearchForTests(string search)
    {
        if (searchInput != null)
            searchInput.SetTextWithoutNotify(search ?? string.Empty);
        RebuildCatalog();
    }

    private void BindUiEvents()
    {
        if (_uiEventsBound)
            return;

        collapseButton.onClick.AddListener(ToggleCollapsed);
        searchInput.onValueChanged.AddListener(HandleSearchChanged);
        charactersButton.onClick.AddListener(ShowCharacters);
        enemiesButton.onClick.AddListener(ShowEnemies);
        cardsButton.onClick.AddListener(ShowCards);
        for (int index = 0;
             index < characterSlotButtons.Length;
             index++)
        {
            int slotIndex = index;
            _characterSlotClickActions ??=
                new UnityAction[characterSlotButtons.Length];
            _characterSlotClickActions[index] ??=
                () => SelectCharacterSlot(slotIndex);
            characterSlotButtons[index].onClick.AddListener(
                _characterSlotClickActions[index]);
        }
        removeCharacterButton.onClick.AddListener(RemoveSelectedCharacter);
        decreaseSpawnCountButton.onClick.AddListener(DecreaseSpawnCount);
        increaseSpawnCountButton.onClick.AddListener(IncreaseSpawnCount);
        queueModeButton.onClick.AddListener(ToggleQueueMode);
        clearEnemiesButton.onClick.AddListener(ClearEnemies);
        restorePartyButton.onClick.AddListener(RestoreParty);
        restoreCoreButton.onClick.AddListener(RestoreCore);
        refillEnergyButton.onClick.AddListener(RefillEnergy);
        debugButton.onClick.AddListener(ToggleDebugVisualization);
        resetPracticeButton.onClick.AddListener(ResetPractice);
        exitPracticeButton.onClick.AddListener(ExitPractice);
        _uiEventsBound = true;
    }

    private void UnbindUiEvents()
    {
        if (!_uiEventsBound)
            return;

        collapseButton?.onClick.RemoveListener(ToggleCollapsed);
        searchInput?.onValueChanged.RemoveListener(HandleSearchChanged);
        charactersButton?.onClick.RemoveListener(ShowCharacters);
        enemiesButton?.onClick.RemoveListener(ShowEnemies);
        cardsButton?.onClick.RemoveListener(ShowCards);
        if (characterSlotButtons != null &&
            _characterSlotClickActions != null)
        {
            int count = Mathf.Min(
                characterSlotButtons.Length,
                _characterSlotClickActions.Length);
            for (int index = 0; index < count; index++)
            {
                if (characterSlotButtons[index] != null &&
                    _characterSlotClickActions[index] != null)
                {
                    characterSlotButtons[index].onClick.RemoveListener(
                        _characterSlotClickActions[index]);
                }
            }
        }
        removeCharacterButton?.onClick.RemoveListener(
            RemoveSelectedCharacter);
        decreaseSpawnCountButton?.onClick.RemoveListener(
            DecreaseSpawnCount);
        increaseSpawnCountButton?.onClick.RemoveListener(
            IncreaseSpawnCount);
        queueModeButton?.onClick.RemoveListener(ToggleQueueMode);
        clearEnemiesButton?.onClick.RemoveListener(ClearEnemies);
        restorePartyButton?.onClick.RemoveListener(RestoreParty);
        restoreCoreButton?.onClick.RemoveListener(RestoreCore);
        refillEnergyButton?.onClick.RemoveListener(RefillEnergy);
        debugButton?.onClick.RemoveListener(ToggleDebugVisualization);
        resetPracticeButton?.onClick.RemoveListener(ResetPractice);
        exitPracticeButton?.onClick.RemoveListener(ExitPractice);
        _uiEventsBound = false;
    }

    private void BindControllerEvents()
    {
        if (_controllerEventsBound || _controller == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        _controller.Changed += HandleControllerChanged;
        _controllerEventsBound = true;
    }

    private void UnbindControllerEvents()
    {
        if (!_controllerEventsBound)
            return;
        if (_controller != null)
            _controller.Changed -= HandleControllerChanged;
        _controllerEventsBound = false;
    }

    private void BindLocalizationEvents()
    {
        if (_localizationEventsBound)
            return;
        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        _localizationEventsBound = true;
    }

    private void UnbindLocalizationEvents()
    {
        if (!_localizationEventsBound)
            return;
        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
        _localizationEventsBound = false;
    }

    private void HandleControllerChanged()
    {
        if (_controller?.IsPracticeBattle != true)
        {
            UnbindControllerEvents();
            DisableDebugVisualization(_controller);
            gameObject.SetActive(false);
            return;
        }
        Refresh();
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        Refresh();
    }

    private void HandleSearchChanged(string unusedValue)
    {
        RebuildCatalog();
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        Refresh();
    }

    private void ShowCharacters()
    {
        SetCategory(PracticeBattleCatalogCategory.Characters);
    }

    private void ShowEnemies()
    {
        SetCategory(PracticeBattleCatalogCategory.Enemies);
    }

    private void ShowCards()
    {
        SetCategory(PracticeBattleCatalogCategory.Cards);
    }

    private void SetCategory(PracticeBattleCatalogCategory category)
    {
        if (!Enum.IsDefined(typeof(PracticeBattleCatalogCategory), category))
            return;
        _category = category;
        Refresh();
    }

    private void SelectCharacterSlot(int slotIndex)
    {
        _selectedCharacterSlot = Mathf.Clamp(
            slotIndex,
            0,
            DungeonPage.MaximumPartySize - 1);
        _characterSlotExplicitlySelected = true;
        RefreshCharacterSlots();
    }

    private void RemoveSelectedCharacter()
    {
        _controller?.TryRemoveCharacter(_selectedCharacterSlot);
        Refresh();
    }

    private void DecreaseSpawnCount()
    {
        _spawnCount = Mathf.Max(MinimumSpawnCount, _spawnCount - 1);
        RefreshSpawnControls();
    }

    private void IncreaseSpawnCount()
    {
        _spawnCount = Mathf.Min(MaximumSpawnCount, _spawnCount + 1);
        RefreshSpawnControls();
    }

    private void ToggleQueueMode()
    {
        _queueEnemy = !_queueEnemy;
        RefreshSpawnControls();
    }

    private void ClearEnemies()
    {
        _controller?.TryClearEnemies();
        Refresh();
    }

    private void RestoreParty()
    {
        _controller?.TryRestoreParty();
        Refresh();
    }

    private void RestoreCore()
    {
        _controller?.TryRestoreCore();
        Refresh();
    }

    private void RefillEnergy()
    {
        _controller?.TryRefillEnergy();
        Refresh();
    }

    private void ToggleDebugVisualization()
    {
        if (_controller?.IsPracticeBattle != true)
            return;

        bool target = !_controller.IsDebugVisualizationEnabled;
        _controller.TrySetDebugVisualization(target);
        RefreshDebugControl(
            _controller.IsDebugVisualizationEnabled);
    }

    private void ResetPractice()
    {
        DisableDebugVisualization(_controller);
        _controller?.TryResetPractice();
        Refresh();
    }

    private void ExitPractice()
    {
        DisableDebugVisualization(_controller);
        _controller?.ExitPractice();
    }

    private void RefreshCharacterSlots()
    {
        IReadOnlyList<CharacterRuntime> active =
            _controller?.ActiveCharacters ?? Array.Empty<CharacterRuntime>();
        for (int slot = 0; slot < characterSlotTexts.Length; slot++)
        {
            CharacterRuntime character = FindCharacterInSlot(active, slot);
            string name = character?.Definition != null
                ? GetCharacterName(character.Definition)
                : ResolveText(LocalizationKeys.UiPracticeEmpty, "EMPTY");
            characterSlotTexts[slot].text = $"{slot + 1} · {name}";
            characterSlotButtons[slot].interactable =
                slot != _selectedCharacterSlot;
        }
    }

    private void RefreshSpawnControls()
    {
        spawnCountText.text = _spawnCount.ToString();
        decreaseSpawnCountButton.interactable =
            _spawnCount > MinimumSpawnCount;
        increaseSpawnCountButton.interactable =
            _spawnCount < MaximumSpawnCount;
        queueModeText.text = _queueEnemy
            ? ResolveText(LocalizationKeys.UiPracticeQueue, "QUEUE")
            : ResolveText(LocalizationKeys.UiPracticeDirect, "DIRECT");
    }

    private void RefreshDebugControl()
    {
        bool available = _controller?.IsPracticeBattle == true;
        bool enabled = available &&
                       _controller.IsDebugVisualizationEnabled;
        RefreshDebugControl(enabled);
    }

    private void RefreshDebugControl(bool enabled)
    {
        bool available = _controller?.IsPracticeBattle == true;
        debugButton.interactable = available;
        LocalizedText fixedLocalization =
            debugButtonText.GetComponent<LocalizedText>();
        if (fixedLocalization != null && fixedLocalization.enabled)
            fixedLocalization.enabled = false;
        debugButtonText.text = enabled
            ? ResolveText(
                LocalizationKeys.UiPracticeDebugOff,
                "DEBUG OFF")
            : ResolveText(
                LocalizationKeys.UiPracticeDebugOn,
                "DEBUG ON");
    }

    private void RefreshStatus()
    {
        string key = _controller?.LastMessageKey;
        statusText.text = string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : ResolveText(key, key);
    }

    private void RebuildCatalog()
    {
        ClearCatalogItems();
        if (_controller?.IsPracticeBattle != true ||
            catalogItemPrefab == null || catalogContent == null)
        {
            return;
        }

        switch (_category)
        {
            case PracticeBattleCatalogCategory.Characters:
                BuildCharacterCatalog();
                break;
            case PracticeBattleCatalogCategory.Enemies:
                BuildEnemyCatalog();
                break;
            case PracticeBattleCatalogCategory.Cards:
                BuildCardCatalog();
                break;
        }

        if (catalogScroll != null)
            catalogScroll.verticalNormalizedPosition = 1f;
    }

    private void BuildCharacterCatalog()
    {
        IReadOnlyList<CharacterSO> catalog =
            _controller.CharacterCatalog ?? Array.Empty<CharacterSO>();
        List<CharacterSO> filtered = new();
        foreach (CharacterSO definition in catalog)
        {
            if (definition != null && MatchesSearch(
                    definition.CharacterId,
                    GetCharacterName(definition)))
            {
                filtered.Add(definition);
            }
        }
        filtered.Sort((left, right) => CompareCatalogEntries(
            GetCharacterName(left),
            left.CharacterId,
            GetCharacterName(right),
            right.CharacterId));
        foreach (CharacterSO definition in filtered)
        {
            AddCatalogItem(
                definition.IconSprite,
                GetCharacterName(definition),
                definition.CharacterId,
                () => SetCharacter(definition));
        }
    }

    private void BuildEnemyCatalog()
    {
        IReadOnlyList<EnemySO> catalog =
            _controller.EnemyCatalog ?? Array.Empty<EnemySO>();
        List<EnemySO> filtered = new();
        foreach (EnemySO definition in catalog)
        {
            if (definition != null && MatchesSearch(
                    definition.EnemyId,
                    EnemyLocalization.GetName(definition)))
            {
                filtered.Add(definition);
            }
        }
        filtered.Sort((left, right) => CompareCatalogEntries(
            EnemyLocalization.GetName(left),
            left.EnemyId,
            EnemyLocalization.GetName(right),
            right.EnemyId));
        foreach (EnemySO definition in filtered)
        {
            AddCatalogItem(
                definition.IconSprite ?? definition.BoardSprite,
                EnemyLocalization.GetName(definition),
                definition.EnemyId,
                () => SpawnEnemy(definition));
        }
    }

    private void BuildCardCatalog()
    {
        IReadOnlyList<BattleCardSO> catalog =
            _controller.CardCatalog ?? Array.Empty<BattleCardSO>();
        List<BattleCardSO> filtered = new();
        foreach (BattleCardSO definition in catalog)
        {
            if (definition != null && MatchesSearch(
                    definition.CardId,
                    definition.GetLocalizedDisplayName()))
            {
                filtered.Add(definition);
            }
        }
        filtered.Sort((left, right) => CompareCatalogEntries(
            left.GetLocalizedDisplayName(),
            left.CardId,
            right.GetLocalizedDisplayName(),
            right.CardId));
        foreach (BattleCardSO definition in filtered)
        {
            AddCatalogItem(
                definition.Icon ?? definition.Illustration,
                definition.GetLocalizedDisplayName(),
                definition.CardId,
                () => AddCard(definition));
        }
    }

    private void AddCatalogItem(
        Sprite icon,
        string displayName,
        string id,
        Action clicked)
    {
        PracticeBattleCatalogItemView item = Instantiate(
            catalogItemPrefab,
            catalogContent,
            false);
        item.gameObject.SetActive(true);
        item.name = "btnPracticeCatalog_" + SanitizeObjectName(id);
        if (!item.Initialize(icon, displayName, id, "+", clicked))
        {
            DestroyObject(item.gameObject);
            return;
        }
        _catalogItems.Add(item);
    }

    private void SetCharacter(CharacterSO definition)
    {
        int slot = _characterSlotExplicitlySelected
            ? _selectedCharacterSlot
            : FindFirstEmptyCharacterSlot();
        if (slot < 0)
            slot = _selectedCharacterSlot;
        _selectedCharacterSlot = slot;
        _controller?.TrySetCharacter(definition, slot);
        Refresh();
    }

    private void SpawnEnemy(EnemySO definition)
    {
        _controller?.TrySpawnEnemy(definition, _spawnCount, _queueEnemy);
        Refresh();
    }

    private void AddCard(BattleCardSO definition)
    {
        _controller?.TryAddCard(definition);
        Refresh();
    }

    private int FindFirstEmptyCharacterSlot()
    {
        IReadOnlyList<CharacterRuntime> active =
            _controller?.ActiveCharacters ?? Array.Empty<CharacterRuntime>();
        for (int slot = 0; slot < DungeonPage.MaximumPartySize; slot++)
        {
            if (FindCharacterInSlot(active, slot) == null)
                return slot;
        }
        return -1;
    }

    private static CharacterRuntime FindCharacterInSlot(
        IReadOnlyList<CharacterRuntime> active,
        int slotIndex)
    {
        if (active == null)
            return null;
        foreach (CharacterRuntime character in active)
        {
            if (character != null &&
                character.PartySlotIndex == slotIndex)
            {
                return character;
            }
        }
        return null;
    }

    private bool MatchesSearch(string id, string displayName)
    {
        string search = searchInput != null
            ? searchInput.text?.Trim()
            : string.Empty;
        return string.IsNullOrWhiteSpace(search) ||
               (!string.IsNullOrWhiteSpace(id) && id.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0) ||
               (!string.IsNullOrWhiteSpace(displayName) &&
                displayName.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static int CompareCatalogEntries(
        string leftName,
        string leftId,
        string rightName,
        string rightId)
    {
        int name = string.Compare(
            leftName,
            rightName,
            StringComparison.CurrentCultureIgnoreCase);
        return name != 0
            ? name
            : string.Compare(
                leftId,
                rightId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCharacterName(CharacterSO definition)
    {
        if (definition == null)
            return string.Empty;
        return ResolveText(
            definition.NameLocalizationKey,
            !string.IsNullOrWhiteSpace(definition.CharacterName)
                ? definition.CharacterName
                : definition.CharacterId);
    }

    private static string ResolveText(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback ?? string.Empty;
        string resolved = LocalizationService.Get(key);
        return string.IsNullOrWhiteSpace(resolved) ||
               string.Equals(resolved, key, StringComparison.Ordinal)
            ? fallback ?? string.Empty
            : resolved;
    }

    private void ClearCatalogItems()
    {
        _catalogItems.Clear();

        if (catalogContent == null)
            return;
        for (int index = catalogContent.childCount - 1; index >= 0; index--)
        {
            Transform child = catalogContent.GetChild(index);
            if (child != null && child.GetComponent<
                    PracticeBattleCatalogItemView>() != null)
            {
                DestroyObject(child.gameObject);
            }
        }
    }

    private bool HasPartySlotReferences()
    {
        if (characterSlotButtons == null || characterSlotTexts == null ||
            characterSlotButtons.Length != DungeonPage.MaximumPartySize ||
            characterSlotTexts.Length != DungeonPage.MaximumPartySize)
        {
            return false;
        }

        for (int index = 0; index < DungeonPage.MaximumPartySize; index++)
        {
            if (characterSlotButtons[index] == null ||
                characterSlotTexts[index] == null)
            {
                return false;
            }
        }
        return true;
    }

    private static void AddMissing(
        ICollection<string> missing,
        UnityEngine.Object value,
        string label)
    {
        if (value == null)
            missing?.Add(label);
    }

    private static void DisableDebugVisualization(
        IPracticeBattleController controller)
    {
        if (controller?.IsDebugVisualizationEnabled == true)
            controller.TrySetDebugVisualization(false);
    }

    private static void DestroyObject(GameObject target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";
        char[] result = value.Trim().ToCharArray();
        for (int index = 0; index < result.Length; index++)
        {
            if (!char.IsLetterOrDigit(result[index]) &&
                result[index] != '_' && result[index] != '-')
            {
                result[index] = '_';
            }
        }
        return new string(result);
    }
}
