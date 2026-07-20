using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonBattleTab : MonoBehaviour
{
    [Header("Time Controls")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseText;
    [SerializeField] private GameObject pauseOverlay;

    [Header("Battle Time")]
    [SerializeField] private TextMeshProUGUI battleTimeText;

    [Header("Active Skill Resource")]
    [SerializeField] private TextMeshProUGUI activeSkillResourceText;

    [Header("Enemy Spawn Queue")]
    [SerializeField] private DungeonSpawnQueueView spawnQueueView;

    [Header("Page Navigation")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private GameObject settingPage;

    private BattleManager _battleManager;
    private DungeonPage _page;
    private DungeonItemHandView _itemHandView;
    private TextMeshProUGUI _pauseOverlayText;
    private bool _initialized;
    private bool _controlEventsBound;
    private bool _battleEventsBound;

    private void OnEnable()
    {
        if (!_initialized)
            return;

        BindControlEvents();
        BindBattleEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindControlEvents();
        UnbindBattleEvents();
    }

    private void OnDestroy()
    {
        Teardown();
    }

    public bool Initialize(BattleManager battleManager)
    {
        if (battleManager == null)
        {
            Debug.LogError("DungeonBattleTab requires a BattleManager.", this);
            return false;
        }

        if (!_initialized && !ValidateReferences())
            return false;

        if (_battleManager != battleManager)
        {
            UnbindBattleEvents();
            _battleManager = battleManager;
        }

        _page = dungeonPage != null
            ? dungeonPage.GetComponent<DungeonPage>()
            : null;
        EnsureItemHandView();

        _initialized = true;
        if (isActiveAndEnabled)
        {
            BindControlEvents();
            BindBattleEvents();
            Refresh();
        }

        return true;
    }

    public void Teardown()
    {
        UnbindControlEvents();
        UnbindBattleEvents();
        _itemHandView?.Teardown();
        _battleManager = null;
        _page = null;
        _initialized = false;
    }

    public void Refresh()
    {
        RefreshSpawnQueue();
        RefreshBattleTime();
        RefreshActiveSkillResource();
        RefreshTimeControls();
    }

    private bool ValidateReferences()
    {
        _pauseOverlayText = pauseOverlay != null
            ? pauseOverlay.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (activeSkillResourceText == null)
        {
            foreach (TextMeshProUGUI text in
                     GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name != "txtPlayerPartyInfoTitle")
                    continue;

                activeSkillResourceText = text;
                break;
            }
        }

        if (speedButton == null || speedText == null || pauseButton == null ||
            pauseText == null || pauseOverlay == null ||
            _pauseOverlayText == null || battleTimeText == null ||
            activeSkillResourceText == null ||
            spawnQueueView == null ||
            settingsButton == null || dungeonPage == null ||
            settingPage == null)
        {
            Debug.LogError("DungeonBattleTab scene references are incomplete.", this);
            return false;
        }

        return spawnQueueView.Initialize();
    }

    private void BindControlEvents()
    {
        if (_controlEventsBound)
            return;

        speedButton.onClick.AddListener(HandleSpeedClicked);
        pauseButton.onClick.AddListener(HandlePauseClicked);
        settingsButton.onClick.AddListener(HandleSettingsClicked);
        _controlEventsBound = true;
    }

    private void UnbindControlEvents()
    {
        if (!_controlEventsBound)
            return;

        if (speedButton != null)
            speedButton.onClick.RemoveListener(HandleSpeedClicked);
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(HandlePauseClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        _controlEventsBound = false;
    }

    private void BindBattleEvents()
    {
        if (_battleEventsBound || _battleManager == null)
            return;

        _battleManager.StateChanged += HandleBattleStateChanged;
        _battleManager.SpawnQueueChanged += RefreshSpawnQueue;
        _battleManager.SpawnTimerChanged += RefreshSpawnTimer;
        _battleManager.BattleTimeChanged += RefreshBattleTime;
        _battleManager.TimeControlChanged += RefreshTimeControls;
        _battleManager.ActiveSkillResourceChanged +=
            HandleActiveSkillResourceChanged;
        _battleManager.ActiveSkillRechargeChanged +=
            RefreshActiveSkillResource;
        _battleEventsBound = true;
    }

    private void UnbindBattleEvents()
    {
        if (!_battleEventsBound)
            return;

        if (_battleManager != null)
        {
            _battleManager.StateChanged -= HandleBattleStateChanged;
            _battleManager.SpawnQueueChanged -= RefreshSpawnQueue;
            _battleManager.SpawnTimerChanged -= RefreshSpawnTimer;
            _battleManager.BattleTimeChanged -= RefreshBattleTime;
            _battleManager.TimeControlChanged -= RefreshTimeControls;
            _battleManager.ActiveSkillResourceChanged -=
                HandleActiveSkillResourceChanged;
            _battleManager.ActiveSkillRechargeChanged -=
                RefreshActiveSkillResource;
        }

        _battleEventsBound = false;
    }

    private void HandleSpeedClicked()
    {
        _battleManager?.CycleGameSpeed();
    }

    private void HandlePauseClicked()
    {
        _battleManager?.TogglePause();
    }

    private void HandleSettingsClicked()
    {
        PageControl.PagToPag(dungeonPage, settingPage, PageOpenMode.Fresh);
    }

    private void HandleBattleStateChanged(EBattleState _)
    {
        RefreshTimeControls();
    }

    private void HandleActiveSkillResourceChanged(int _)
    {
        RefreshActiveSkillResource();
    }

    private void RefreshSpawnQueue()
    {
        if (spawnQueueView == null)
            return;

        IReadOnlyList<EnemyRuntime> enemies = _battleManager != null
            ? _battleManager.SpawnQueue
            : Array.Empty<EnemyRuntime>();
        spawnQueueView.RefreshQueue(enemies);
        RefreshSpawnTimer();
    }

    private void RefreshSpawnTimer()
    {
        if (spawnQueueView == null)
            return;

        spawnQueueView.RefreshTimer(
            _battleManager != null ? _battleManager.SpawnTimeRemaining : 0f,
            _battleManager != null ? _battleManager.SpawnInterval : 0f,
            _battleManager != null ? _battleManager.PendingEnemyCount : 0,
            _battleManager != null && _battleManager.IsBoardFull);
    }

    private void RefreshTimeControls()
    {
        float gameSpeed = _battleManager != null ? _battleManager.GameSpeed : 1f;
        bool isPaused = _battleManager != null && _battleManager.IsPaused;
        bool isDefeated = _battleManager != null &&
                          _battleManager.State == EBattleState.Completed &&
                          _battleManager.Result == EBattleResult.Timeout;

        if (speedText != null)
            speedText.text = $"{gameSpeed:0.#}X";
        if (pauseText != null)
            pauseText.text = isPaused ? "RESUME" : "PAUSE";
        if (_pauseOverlayText == null && pauseOverlay != null)
        {
            _pauseOverlayText =
                pauseOverlay.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_pauseOverlayText != null)
            _pauseOverlayText.text = isDefeated ? "DEFEAT" : "PAUSE";
        if (pauseOverlay != null)
            pauseOverlay.SetActive(isPaused || isDefeated);
    }

    private void RefreshBattleTime()
    {
        if (battleTimeText == null)
            return;

        if (_battleManager == null || _battleManager.BattleDuration <= 0f)
        {
            battleTimeText.text = "00:00";
            return;
        }

        int totalSeconds = Mathf.CeilToInt(
            _battleManager.BattleTimeRemaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        battleTimeText.text = $"{minutes:00}:{seconds:00}";
    }

    private void RefreshActiveSkillResource()
    {
        if (activeSkillResourceText == null)
            return;

        int resource = _battleManager != null
            ? _battleManager.ActiveSkillResource
            : 0;
        int maximumResource = _battleManager != null
            ? _battleManager.MaximumActiveSkillResource
            : BattleManager.DefaultMaximumEnergy;
        float rechargeRemaining = _battleManager != null
            ? _battleManager.ActiveSkillRechargeRemaining
            : 0f;
        string recharge = resource >= maximumResource
            ? "FULL"
            : $"+1 IN {rechargeRemaining:0.0}s";
        activeSkillResourceText.text = _page != null
            ? $"ENERGY {resource}/{maximumResource} | {recharge} | " +
              $"SCALE {_page.CurrentDifficultyScale}"
            : $"ENERGY {resource}/{maximumResource} | {recharge}";
    }

    private void EnsureItemHandView()
    {
        if (_page == null || _battleManager == null)
            return;

        if (_itemHandView == null)
            _itemHandView = GetComponentInChildren<DungeonItemHandView>(true);
        if (_itemHandView == null)
        {
            GameObject handObject = new(
                "grpBattleItemHand",
                typeof(RectTransform),
                typeof(DungeonItemHandView));
            handObject.transform.SetParent(transform, false);
            _itemHandView = handObject.GetComponent<DungeonItemHandView>();
        }

        _itemHandView.Initialize(_page, _battleManager);
    }
}

[DisallowMultipleComponent]
public sealed class DungeonItemHandView : MonoBehaviour
{
    private static readonly EBattleItemType[] DisplayOrder =
    {
        EBattleItemType.Focus,
        EBattleItemType.Molotov,
        EBattleItemType.PrecisionShot,
        EBattleItemType.OverSupply,
        EBattleItemType.Overheat,
    };

    private readonly Dictionary<EBattleItemType, DungeonItemCardView> _cards =
        new();
    private readonly List<CharacterRuntime> _boundTurrets = new();
    private DungeonPage _page;
    private BattleManager _battleManager;
    private TextMeshProUGUI _instructionText;
    private EBattleItemType? _selectedItem;
    private float _focusCooldownRemaining;
    private EBattleState _previousBattleState;
    private bool _initialized;

    public void Initialize(DungeonPage page, BattleManager battleManager)
    {
        if (page == null || battleManager == null)
            return;

        if (_initialized && _page == page && _battleManager == battleManager)
        {
            RebuildCards();
            return;
        }

        Teardown();
        _page = page;
        _battleManager = battleManager;
        ConfigureRoot();
        _page.BattleItemsChanged += RebuildCards;
        _battleManager.ActiveSkillResourceChanged += HandleEnergyChanged;
        _battleManager.StateChanged += HandleBattleStateChanged;
        if (_page.Board != null)
            _page.Board.BindItemTargetHandler(HandleEnemyClicked);
        _previousBattleState = _battleManager.State;
        _focusCooldownRemaining = 0f;
        _initialized = true;
        RebuildCards();
    }

    public void Teardown()
    {
        if (_page != null)
        {
            _page.BattleItemsChanged -= RebuildCards;
            if (_page.Board != null)
                _page.Board.BindItemTargetHandler(null);
        }
        if (_battleManager != null)
        {
            _battleManager.ActiveSkillResourceChanged -= HandleEnergyChanged;
            _battleManager.StateChanged -= HandleBattleStateChanged;
        }
        UnbindTurrets();
        _page = null;
        _battleManager = null;
        _selectedItem = null;
        _initialized = false;
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private void Update()
    {
        if (!_initialized || _battleManager == null)
            return;

        if (_battleManager.State == EBattleState.Running &&
            _focusCooldownRemaining > 0f)
        {
            _focusCooldownRemaining = Mathf.Max(
                0f,
                _focusCooldownRemaining - Time.deltaTime);
        }

        RefreshCards();
    }

    private void ConfigureRoot()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.anchoredPosition = new Vector2(18f, 0f);
        root.sizeDelta = new Vector2(132f, 620f);
        root.SetAsLastSibling();

        if (_instructionText != null)
            return;

        GameObject instructionObject = new(
            "txtItemTargetInstruction",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        instructionObject.transform.SetParent(transform, false);
        RectTransform instructionRect =
            (RectTransform)instructionObject.transform;
        instructionRect.anchorMin = new Vector2(0f, 1f);
        instructionRect.anchorMax = new Vector2(0f, 1f);
        instructionRect.pivot = new Vector2(0f, 1f);
        instructionRect.anchoredPosition = new Vector2(0f, 0f);
        instructionRect.sizeDelta = new Vector2(310f, 46f);
        _instructionText = instructionObject.GetComponent<TextMeshProUGUI>();
        _instructionText.fontSize = 18f;
        _instructionText.fontStyle = FontStyles.Bold;
        _instructionText.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        _instructionText.alignment = TextAlignmentOptions.Left;
        _instructionText.raycastTarget = false;
    }

    private void RebuildCards()
    {
        if (!_initialized || _page == null)
            return;

        foreach (DungeonItemCardView card in _cards.Values)
        {
            if (card != null)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
        }
        _cards.Clear();

        int visibleCount = 1;
        foreach (EBattleItemType itemType in BattleItemCatalog.Consumables)
        {
            if (_page.GetBattleItemCount(itemType) > 0)
                visibleCount++;
        }

        int visibleIndex = 0;
        foreach (EBattleItemType itemType in DisplayOrder)
        {
            if (itemType != EBattleItemType.Focus &&
                _page.GetBattleItemCount(itemType) <= 0)
            {
                continue;
            }

            GameObject cardObject = new(
                $"crdBattleItem_{itemType}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(DungeonItemCardView));
            cardObject.transform.SetParent(transform, false);
            RectTransform cardRect = (RectTransform)cardObject.transform;
            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(0f, 0.5f);
            cardRect.pivot = new Vector2(0f, 0.5f);
            float top = (visibleCount - 1) * 39f;
            cardRect.anchoredPosition = new Vector2(
                0f,
                top - visibleIndex * 78f);
            cardRect.sizeDelta = new Vector2(124f, 94f);

            DungeonItemCardView card =
                cardObject.GetComponent<DungeonItemCardView>();
            card.Initialize(
                BattleItemCatalog.Get(itemType),
                HandleCardClicked);
            _cards[itemType] = card;
            visibleIndex++;
        }

        RefreshTurretBindings();
        RefreshCards();
    }

    private void RefreshCards()
    {
        if (_page == null || _battleManager == null)
            return;

        foreach ((EBattleItemType itemType, DungeonItemCardView card) in _cards)
        {
            if (card == null)
                continue;

            float cooldown = itemType == EBattleItemType.Focus
                ? _focusCooldownRemaining
                : 0f;
            card.Refresh(
                _page.GetBattleItemCount(itemType),
                _battleManager.ActiveSkillResource,
                _battleManager.State == EBattleState.Running,
                cooldown,
                _selectedItem == itemType);
        }

        RefreshInstruction();
    }

    private void HandleCardClicked(EBattleItemType itemType)
    {
        if (_page == null || _battleManager == null)
            return;

        BattleItemDefinition definition = BattleItemCatalog.Get(itemType);
        float cooldown = itemType == EBattleItemType.Focus
            ? _focusCooldownRemaining
            : 0f;
        bool canSelect = _battleManager.State == EBattleState.Running &&
                         _battleManager.CanSpend(definition.EnergyCost) &&
                         cooldown <= 0f &&
                         (definition.IsReusable ||
                          _page.GetBattleItemCount(itemType) > 0);
        if (!canSelect)
            return;

        _selectedItem = _selectedItem == itemType
            ? null
            : itemType;
        RefreshCards();
    }

    private bool HandleEnemyClicked(EnemyRuntime enemy)
    {
        if (!_selectedItem.HasValue || enemy == null || _page == null)
            return false;

        EBattleItemType itemType = _selectedItem.Value;
        BattleItemDefinition definition = BattleItemCatalog.Get(itemType);
        if (definition.TargetType != EBattleItemTargetType.Enemy)
            return false;

        bool used = _page.TryUseBattleItemOnEnemy(itemType, enemy);
        if (used)
            CompleteSelectedItem(definition);
        return used;
    }

    private bool HandleTurretClicked(CharacterRuntime turret)
    {
        if (!_selectedItem.HasValue || _page == null)
            return false;

        EBattleItemType itemType = _selectedItem.Value;
        BattleItemDefinition definition = BattleItemCatalog.Get(itemType);
        if (definition.TargetType == EBattleItemTargetType.Turret &&
            _page.TryUseBattleItemOnTurret(itemType, turret))
        {
            CompleteSelectedItem(definition);
        }

        return true;
    }

    private void CompleteSelectedItem(BattleItemDefinition definition)
    {
        if (definition.IsReusable)
            _focusCooldownRemaining = definition.Cooldown;
        _selectedItem = null;
        RebuildCards();
    }

    private void HandleEnergyChanged(int _)
    {
        if (_selectedItem.HasValue && _battleManager != null)
        {
            BattleItemDefinition selected = BattleItemCatalog.Get(
                _selectedItem.Value);
            if (!_battleManager.CanSpend(selected.EnergyCost))
                _selectedItem = null;
        }
        RefreshCards();
    }

    private void HandleBattleStateChanged(EBattleState state)
    {
        bool isNewBattle = state == EBattleState.Running &&
                           (_previousBattleState == EBattleState.Idle ||
                            _previousBattleState == EBattleState.Completed);
        if (isNewBattle)
            _focusCooldownRemaining = 0f;
        if (state != EBattleState.Running)
            _selectedItem = null;
        _previousBattleState = state;
        RefreshTurretBindings();
        RefreshCards();
    }

    private void RefreshTurretBindings()
    {
        UnbindTurrets();
        if (_page == null)
            return;

        foreach (CharacterRuntime turret in _page.OwnedTurrets)
        {
            if (turret == null)
                continue;

            turret.BindItemTargetHandler(HandleTurretClicked);
            _boundTurrets.Add(turret);
        }
    }

    private void UnbindTurrets()
    {
        foreach (CharacterRuntime turret in _boundTurrets)
            turret?.BindItemTargetHandler(null);
        _boundTurrets.Clear();
    }

    private void RefreshInstruction()
    {
        if (_instructionText == null)
            return;

        if (!_selectedItem.HasValue)
        {
            _instructionText.text = "ITEM HAND";
            return;
        }

        BattleItemDefinition definition = BattleItemCatalog.Get(
            _selectedItem.Value);
        _instructionText.text = definition.TargetType ==
                                EBattleItemTargetType.Enemy
            ? $"{definition.DisplayName}: SELECT ENEMY"
            : $"{definition.DisplayName}: SELECT TURRET";
    }
}

[DisallowMultipleComponent]
public sealed class DungeonItemCardView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private static readonly Color AvailableColor =
        new(0.18f, 0.28f, 0.22f, 0.98f);
    private static readonly Color SelectedColor =
        new(0.22f, 0.48f, 0.3f, 1f);
    private static readonly Color DisabledColor =
        new(0.08f, 0.1f, 0.09f, 0.92f);

    private BattleItemDefinition _definition;
    private System.Action<EBattleItemType> _clicked;
    private Image _background;
    private TextMeshProUGUI _summaryText;
    private GameObject _popup;
    private bool _hovered;

    public void Initialize(
        BattleItemDefinition definition,
        System.Action<EBattleItemType> clicked)
    {
        _definition = definition;
        _clicked = clicked;
        _background = GetComponent<Image>();
        _background.color = AvailableColor;
        _background.raycastTarget = true;

        _summaryText = CreateText(
            transform,
            "txtItemSummary",
            Vector2.zero,
            new Vector2(1f, 1f),
            new Vector2(8f, 6f),
            new Vector2(-8f, -6f),
            15f,
            TextAlignmentOptions.Center);

        _popup = new GameObject(
            "grpItemPopup",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        _popup.transform.SetParent(transform, false);
        RectTransform popupRect = (RectTransform)_popup.transform;
        popupRect.anchorMin = new Vector2(0f, 0.5f);
        popupRect.anchorMax = new Vector2(0f, 0.5f);
        popupRect.pivot = new Vector2(0f, 0.5f);
        popupRect.anchoredPosition = new Vector2(132f, 0f);
        popupRect.sizeDelta = new Vector2(300f, 108f);
        Image popupImage = _popup.GetComponent<Image>();
        popupImage.color = new Color(0.055f, 0.075f, 0.062f, 0.99f);
        popupImage.raycastTarget = false;
        TextMeshProUGUI detailText = CreateText(
            popupRect,
            "txtItemDetail",
            Vector2.zero,
            Vector2.one,
            new Vector2(12f, 8f),
            new Vector2(-12f, -8f),
            17f,
            TextAlignmentOptions.MidlineLeft);
        detailText.text = $"{definition.DisplayName} [C{definition.EnergyCost}]\n" +
                          definition.Description +
                          (definition.IsReusable
                              ? $"\nREUSABLE | CD {definition.Cooldown:0.#}s"
                              : "\nCONSUMABLE");
        _popup.SetActive(false);
    }

    public void Refresh(
        int count,
        int energy,
        bool battleRunning,
        float cooldown,
        bool selected)
    {
        bool available = battleRunning &&
                         energy >= _definition.EnergyCost &&
                         cooldown <= 0f &&
                         (_definition.IsReusable || count > 0);
        _background.color = selected
            ? SelectedColor
            : available
                ? AvailableColor
                : DisabledColor;

        string state = cooldown > 0f
            ? $"CD {TimePrecision.FloorToTenth(cooldown):0.0}s"
            : _definition.IsReusable
                ? "REUSABLE"
                : $"x{count}";
        _summaryText.text =
            $"C{_definition.EnergyCost}  {_definition.DisplayName}\n{state}";
    }

    private void Update()
    {
        Vector3 targetScale = _hovered
            ? new Vector3(1.12f, 1.12f, 1f)
            : Vector3.one;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        transform.SetAsLastSibling();
        _popup?.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _popup?.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left)
        {
            _clicked?.Invoke(_definition.Type);
        }
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
}
