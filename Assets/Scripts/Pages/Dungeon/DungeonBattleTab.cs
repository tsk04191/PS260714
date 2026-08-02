using System;
using System.Collections.Generic;
using PS260714.Localization;
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
    private RectTransform _pauseMenuPanel;
    private Button _continueButton;
    private TextMeshProUGUI _continueText;
    private Button _returnToStageButton;
    private TextMeshProUGUI _returnToStageText;
    private Button _quitGameButton;
    private TextMeshProUGUI _quitGameText;

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
    private bool _localizationEventsBound;

    public RectTransform TimerHighlightRect => battleTimeText != null
        ? battleTimeText.rectTransform
        : null;
    public RectTransform QueueHighlightRect => spawnQueueView != null
        ? spawnQueueView.transform as RectTransform
        : null;
    public RectTransform ItemHighlightRect
    {
        get
        {
            if (_itemHandView == null)
                EnsureItemHandView();
            return _itemHandView != null
                ? _itemHandView.HighlightRect
                : null;
        }
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        BindControlEvents();
        BindBattleEvents();
        BindLocalizationEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindControlEvents();
        UnbindBattleEvents();
        UnbindLocalizationEvents();
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
            BindLocalizationEvents();
            Refresh();
        }

        return true;
    }

    public void Teardown()
    {
        UnbindControlEvents();
        UnbindBattleEvents();
        UnbindLocalizationEvents();
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
        TextMeshProUGUI[] initialTexts =
            GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int index = 0; index < initialTexts.Length; index++)
        {
            TextMeshProUGUI text = initialTexts[index];
            LocalizationFontResolver.ApplyGameDefault(text);
            if (activeSkillResourceText != null ||
                text.name != "txtPlayerPartyInfoTitle")
            {
                continue;
            }

            activeSkillResourceText = text;
        }

        if (settingsButton != null)
        {
            TextMeshProUGUI settingsLabel =
                settingsButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (settingsLabel != null)
            {
                LocalizedText localizedText =
                    settingsLabel.GetComponent<LocalizedText>();
                if (localizedText == null)
                {
                    localizedText = settingsLabel.gameObject
                        .AddComponent<LocalizedText>();
                }

                localizedText.SetKey(LocalizationKeys.UiCommonSettings);
            }
        }
        EnsurePauseNavigationButtons();

        if (speedButton == null || speedText == null || pauseButton == null ||
            pauseText == null || pauseOverlay == null ||
            _pauseOverlayText == null || battleTimeText == null ||
            activeSkillResourceText == null ||
            spawnQueueView == null ||
            _pauseMenuPanel == null ||
            _continueButton == null || _continueText == null ||
            _returnToStageButton == null ||
            _returnToStageText == null ||
            _quitGameButton == null || _quitGameText == null ||
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
        _continueButton.onClick.AddListener(HandleContinueClicked);
        _returnToStageButton.onClick.AddListener(
            HandleReturnToStageClicked);
        _quitGameButton.onClick.AddListener(HandleQuitGameClicked);
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
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(
                HandleContinueClicked);
        }
        if (_returnToStageButton != null)
        {
            _returnToStageButton.onClick.RemoveListener(
                HandleReturnToStageClicked);
        }
        if (_quitGameButton != null)
        {
            _quitGameButton.onClick.RemoveListener(
                HandleQuitGameClicked);
        }
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

    private void HandleLocalizationChanged(string unusedValue)
    {
        TextMeshProUGUI[] texts =
            GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int index = 0; index < texts.Length; index++)
            LocalizationFontResolver.ApplyGameDefault(texts[index]);

        Refresh();
    }

    private void HandleSpeedClicked()
    {
        _battleManager?.CycleGameSpeed();
        if (_battleManager != null)
            _page?.RecordBattleSpeed(_battleManager.GameSpeed);
    }

    private void HandlePauseClicked()
    {
        if (_page != null)
            _page.ToggleBattlePause();
        else
            _battleManager?.TogglePause();
    }

    private void HandleSettingsClicked()
    {
        if (settingPage != null &&
            settingPage.TryGetComponent(out SettingPage settings))
        {
            settings.OpenFrom(dungeonPage);
            return;
        }

        PageControl.PagToPag(dungeonPage, settingPage, PageOpenMode.Fresh);
    }

    private void HandleContinueClicked()
    {
        HandlePauseClicked();
    }

    private void HandleReturnToStageClicked()
    {
        _page?.ReturnToStageSelect();
    }

    private static void HandleQuitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
        {
            pauseText.text = LocalizationService.Get(
                isPaused
                    ? LocalizationKeys.UiDungeonResume
                    : LocalizationKeys.UiDungeonPause);
        }
        if (_pauseOverlayText == null && pauseOverlay != null)
        {
            _pauseOverlayText =
                pauseOverlay.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_pauseOverlayText != null)
        {
            _pauseOverlayText.text = LocalizationService.Get(
                isDefeated
                    ? LocalizationKeys.UiDungeonDefeat
                    : LocalizationKeys.UiDungeonPause);
        }
        if (_continueText != null)
        {
            _continueText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonResume);
        }
        if (_returnToStageText != null)
        {
            _returnToStageText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonReturnToStage);
        }
        if (_quitGameText != null)
        {
            _quitGameText.text = LocalizationService.Get(
                LocalizationKeys.UiSettingsQuitGame);
        }
        if (_continueButton != null)
            _continueButton.interactable = isPaused && !isDefeated;
        if (pauseOverlay != null)
            pauseOverlay.SetActive(isPaused || isDefeated);
    }

    private void EnsurePauseNavigationButtons()
    {
        if (pauseOverlay == null || pauseButton == null)
            return;

        _pauseMenuPanel = EnsurePauseMenuPanel();
        if (_pauseMenuPanel == null)
            return;

        if (_pauseOverlayText != null &&
            _pauseOverlayText.transform.parent != _pauseMenuPanel)
        {
            _pauseOverlayText.transform.SetParent(
                _pauseMenuPanel,
                false);
        }

        _continueButton = ResolveOrClonePauseButton(
            _continueButton,
            "btnContinue",
            "txtContinue",
            LocalizationKeys.UiDungeonResume,
            new Vector2(0f, 55f),
            out _continueText);
        _returnToStageButton = ResolveOrClonePauseButton(
            _returnToStageButton,
            "btnReturnToStage",
            "txtReturnToStage",
            LocalizationKeys.UiDungeonReturnToStage,
            new Vector2(0f, -25f),
            out _returnToStageText);
        _quitGameButton = ResolveOrClonePauseButton(
            _quitGameButton,
            "btnQuitGame",
            "txtQuitGame",
            LocalizationKeys.UiSettingsQuitGame,
            new Vector2(0f, -105f),
            out _quitGameText);

        if (_pauseOverlayText != null)
        {
            RectTransform titleRect =
                _pauseOverlayText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 145f);
            titleRect.sizeDelta = new Vector2(380f, 72f);
            _pauseOverlayText.fontSize = 46f;
            _pauseOverlayText.color =
                new Color(0.98f, 0.94f, 0.78f, 1f);
            _pauseOverlayText.alignment =
                TextAlignmentOptions.Center;
            _pauseOverlayText.raycastTarget = false;
            titleRect.SetAsLastSibling();
        }

        Button[] overlayButtons =
            pauseOverlay.GetComponentsInChildren<Button>(true);
        foreach (Button button in overlayButtons)
        {
            if (button == null ||
                button == _continueButton ||
                button == _returnToStageButton ||
                button == _quitGameButton)
            {
                continue;
            }

            button.gameObject.SetActive(false);
        }
    }

    private RectTransform EnsurePauseMenuPanel()
    {
        if (_pauseMenuPanel == null)
        {
            Transform existing =
                pauseOverlay.transform.Find("grpPauseMenuPanel");
            _pauseMenuPanel = existing as RectTransform;
        }

        if (_pauseMenuPanel == null)
        {
            GameObject panelObject = new(
                "grpPauseMenuPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(
                pauseOverlay.transform,
                false);
            _pauseMenuPanel =
                panelObject.GetComponent<RectTransform>();
        }

        _pauseMenuPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _pauseMenuPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _pauseMenuPanel.pivot = new Vector2(0.5f, 0.5f);
        _pauseMenuPanel.anchoredPosition = Vector2.zero;
        _pauseMenuPanel.sizeDelta = new Vector2(440f, 430f);
        _pauseMenuPanel.localScale = Vector3.one;
        _pauseMenuPanel.SetAsFirstSibling();

        Image background =
            _pauseMenuPanel.GetComponent<Image>();
        background.color = new Color(
            0.055f,
            0.075f,
            0.065f,
            0.98f);
        background.raycastTarget = true;

        Outline outline =
            _pauseMenuPanel.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.52f, 0.68f, 0.52f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;
        return _pauseMenuPanel;
    }

    private Button ResolveOrClonePauseButton(
        Button current,
        string objectName,
        string labelName,
        string localizationKey,
        Vector2 anchoredPosition,
        out TextMeshProUGUI label)
    {
        Button button = current;
        if (button == null)
        {
            Transform existing =
                _pauseMenuPanel.Find(objectName);
            button = existing != null
                ? existing.GetComponent<Button>()
                : null;
        }

        if (button == null)
        {
            button = Instantiate(
                pauseButton,
                _pauseMenuPanel,
                false);
            button.name = objectName;
        }
        else if (button.transform.parent != _pauseMenuPanel)
        {
            button.transform.SetParent(_pauseMenuPanel, false);
        }

        button.onClick = new Button.ButtonClickedEvent();
        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(340f, 64f);
            rect.localScale = Vector3.one;
        }

        Image buttonImage = button.targetGraphic as Image ??
                            button.GetComponent<Image>();
        if (buttonImage != null)
        {
            button.targetGraphic = buttonImage;
            buttonImage.color =
                new Color(0.2f, 0.38f, 0.3f, 1f);
            buttonImage.raycastTarget = true;
        }

        ColorBlock colors = button.colors;
        colors.normalColor =
            new Color(0.2f, 0.38f, 0.3f, 1f);
        colors.highlightedColor =
            new Color(0.3f, 0.55f, 0.42f, 1f);
        colors.pressedColor =
            new Color(0.12f, 0.26f, 0.2f, 1f);
        colors.selectedColor =
            new Color(0.26f, 0.48f, 0.36f, 1f);
        colors.disabledColor =
            new Color(0.12f, 0.15f, 0.13f, 0.75f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.name = labelName;
            LocalizedText localizedText =
                label.GetComponent<LocalizedText>();
            if (localizedText == null)
                localizedText =
                    label.gameObject.AddComponent<LocalizedText>();
            localizedText.SetKey(localizationKey);
            LocalizationFontResolver.ApplyGameDefault(label);
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
        return button;
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
            ? LocalizationService.Get(LocalizationKeys.UiDungeonEnergyFull)
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonEnergyRecharge,
                LocalizationService.Arg("seconds", rechargeRemaining));
        activeSkillResourceText.text = _page != null
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonEnergyScale,
                LocalizationService.Arg("current", resource),
                LocalizationService.Arg("max", maximumResource),
                LocalizationService.Arg("recharge", recharge),
                LocalizationService.Arg(
                    "scale",
                    _page.CurrentDifficultyScale))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonEnergyStatus,
                LocalizationService.Arg("current", resource),
                LocalizationService.Arg("max", maximumResource),
                LocalizationService.Arg("recharge", recharge));
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
    private readonly Dictionary<BattleItemSO, DungeonItemCardView> _cards =
        new();
    private readonly List<CharacterRuntime> _boundTurrets = new();
    private DungeonPage _page;
    private BattleManager _battleManager;
    private TextMeshProUGUI _instructionText;
    private BattleItemSO _selectedItem;
    private bool _initialized;

    public RectTransform HighlightRect
    {
        get
        {
            foreach (DungeonItemCardView card in _cards.Values)
            {
                if (card != null && card.gameObject.activeInHierarchy)
                {
                    return card.transform as RectTransform;
                }
            }

            return transform as RectTransform;
        }
    }

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
        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        if (_page.Board != null)
        {
            _page.Board.BindItemTargetHandler(HandleEnemyClicked);
            _page.Board.ManualTargetSelectionPendingChanged +=
                HandleManualTargetSelectionPendingChanged;
            _page.Board.ManualTargetSelectionProgressChanged +=
                RefreshCards;
        }
        _initialized = true;
        RebuildCards();
    }

    public void Teardown()
    {
        if (_page != null)
        {
            _page.BattleItemsChanged -= RebuildCards;
            if (_page.Board != null)
            {
                _page.Board.BindItemTargetHandler(null);
                _page.Board.ManualTargetSelectionPendingChanged -=
                    HandleManualTargetSelectionPendingChanged;
                _page.Board.ManualTargetSelectionProgressChanged -=
                    RefreshCards;
            }
        }
        if (_battleManager != null)
        {
            _battleManager.ActiveSkillResourceChanged -= HandleEnergyChanged;
            _battleManager.StateChanged -= HandleBattleStateChanged;
        }
        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
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
        LocalizationFontResolver.ApplyGameDefault(_instructionText);
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

        int visibleCount = 0;
        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            if (_page.IsBattleItemOwned(item))
                visibleCount++;
        }

        int visibleIndex = 0;
        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            if (item == null || !_page.IsBattleItemOwned(item))
                continue;

            GameObject cardObject = new(
                $"crdBattleItem_{item.ItemId.Replace('.', '_')}",
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
                item,
                HandleCardClicked);
            _cards[item] = card;
            visibleIndex++;
        }

        RefreshTurretBindings();
        RefreshCards();
    }

    private void RefreshCards()
    {
        if (_page == null || _battleManager == null)
            return;

        foreach ((BattleItemSO item, DungeonItemCardView card) in _cards)
        {
            if (card == null)
                continue;

            float cooldown = _page.GetBattleItemCooldown(item);
            card.Refresh(
                _page.GetBattleItemCount(item),
                _battleManager.ActiveSkillResource,
                _battleManager.State == EBattleState.Running,
                cooldown,
                _selectedItem == item);
        }

        RefreshInstruction();
    }

    private void HandleCardClicked(BattleItemSO item)
    {
        if (_page == null || _battleManager == null || item == null)
            return;

        float cooldown = _page.GetBattleItemCooldown(item);
        bool canSelect = _battleManager.State == EBattleState.Running &&
                         _battleManager.CanSpend(item.EnergyCost) &&
                         cooldown <= 0f &&
                         _page.IsBattleItemOwned(item) &&
                         (item.HasUnlimitedUses ||
                          _page.GetBattleItemCount(item) > 0);
        if (!canSelect)
            return;

        _selectedItem = _selectedItem == item
            ? null
            : item;
        RefreshCards();
    }

    private bool HandleEnemyClicked(EnemyRuntime enemy)
    {
        if (_selectedItem == null || enemy == null || _page == null)
            return false;

        BattleItemSO item = _selectedItem;
        if (item.TargetType != BattleItemTargetType.Enemy)
            return false;

        bool used = _page.TryUseBattleItemOnEnemy(item, enemy);
        if (used)
            CompleteSelectedItem();
        return used;
    }

    private bool HandleTurretClicked(CharacterRuntime turret)
    {
        if (_selectedItem == null || _page == null)
            return false;

        BattleItemSO item = _selectedItem;
        if (item.TargetType == BattleItemTargetType.Turret &&
            _page.TryUseBattleItemOnTurret(item, turret))
        {
            CompleteSelectedItem();
        }

        return true;
    }

    private void CompleteSelectedItem()
    {
        _selectedItem = null;
        RebuildCards();
    }

    private void HandleEnergyChanged(int _)
    {
        if (_selectedItem != null && _battleManager != null)
        {
            if (!_battleManager.CanSpend(_selectedItem.EnergyCost))
                _selectedItem = null;
        }
        RefreshCards();
    }

    private void HandleBattleStateChanged(EBattleState state)
    {
        if (state != EBattleState.Running)
            _selectedItem = null;
        RefreshTurretBindings();
        RefreshCards();
    }

    private void HandleManualTargetSelectionPendingChanged(bool pending)
    {
        if (pending)
            _selectedItem = null;
        RefreshCards();
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        if (!_initialized)
            return;

        LocalizationFontResolver.ApplyGameDefault(_instructionText);
        RebuildCards();
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

        DungeonBoardView board = _page?.Board;
        if (board?.IsManualTargetSelectionPending == true)
        {
            BattleManualTargetSelectionRequest request =
                board.CurrentManualTargetRequest;
            int required = request?.RequiredCount ?? 1;
            int selected = board.CurrentManualSelectedCount;
            bool korean = LocalizationService.CurrentLocale?.StartsWith(
                "ko",
                StringComparison.OrdinalIgnoreCase) == true;
            _instructionText.text = korean
                ? $"대상을 선택하세요 ({selected}/{required})"
                : $"Select target(s) ({selected}/{required})";
            return;
        }

        if (_selectedItem == null)
        {
            _instructionText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonItemHand);
            return;
        }

        _instructionText.text = LocalizationService.Get(
            _selectedItem.TargetType == BattleItemTargetType.Enemy
                ? LocalizationKeys.UiDungeonItemSelectEnemy
                : LocalizationKeys.UiDungeonItemSelectTurret,
            LocalizationService.Arg(
                "item",
                _selectedItem.GetLocalizedDisplayName()));
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

    private BattleItemSO _item;
    private System.Action<BattleItemSO> _clicked;
    private Image _background;
    private TextMeshProUGUI _summaryText;
    private GameObject _popup;
    private bool _hovered;

    public void Initialize(
        BattleItemSO item,
        System.Action<BattleItemSO> clicked)
    {
        _item = item;
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
        string header = LocalizationService.Get(
            LocalizationKeys.UiDungeonItemCardHeader,
            LocalizationService.Arg(
                "name",
                item.GetLocalizedDisplayName()),
            LocalizationService.Arg("cost", item.EnergyCost));
        string footer = item.HasUnlimitedUses
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonItemReusable,
                LocalizationService.Arg("cooldown", item.Cooldown))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonItemConsumable);
        detailText.text =
            $"{header}\n{item.GetLocalizedDescription()}\n{footer}";
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
                         energy >= _item.EnergyCost &&
                         cooldown <= 0f &&
                         (_item.HasUnlimitedUses || count > 0);
        _background.color = selected
            ? SelectedColor
            : available
                ? AvailableColor
                : DisabledColor;

        string state = cooldown > 0f
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonItemCooldown,
                LocalizationService.Arg(
                    "cooldown",
                    TimePrecision.FloorToTenth(cooldown)))
            : _item.HasUnlimitedUses
                ? "∞"
                : LocalizationService.Get(
                    LocalizationKeys.UiDungeonItemCount,
                    LocalizationService.Arg("count", count));
        string header = LocalizationService.Get(
            LocalizationKeys.UiDungeonItemCardHeader,
            LocalizationService.Arg(
                "name",
                _item.GetLocalizedDisplayName()),
            LocalizationService.Arg("cost", _item.EnergyCost));
        _summaryText.text = $"{header}\n{state}";
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
            _clicked?.Invoke(_item);
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
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
}
