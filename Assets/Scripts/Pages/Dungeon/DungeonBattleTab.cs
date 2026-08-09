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
    internal Button PauseButtonTemplate => pauseButton;
    [SerializeField] private RectTransform _pauseMenuPanel;
    [SerializeField] private Button _continueButton;
    [SerializeField] private TextMeshProUGUI _continueText;
    [SerializeField] private Button _returnToStageButton;
    [SerializeField] private TextMeshProUGUI _returnToStageText;
    [SerializeField] private Button _quitGameButton;
    [SerializeField] private TextMeshProUGUI _quitGameText;
    [SerializeField] private ResponsivePanelFitter _pausePanelFitter;

    [Header("Battle Time")]
    [SerializeField] private TextMeshProUGUI battleTimeText;

    [Header("Active Skill Resource")]
    [SerializeField] private TextMeshProUGUI activeSkillResourceText;
    [SerializeField] private Sprite activeSkillResourceIcon;

    [Header("Enemy Spawn Queue")]
    [SerializeField] private DungeonSpawnQueueView spawnQueueView;

    [Header("Page Navigation")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private GameObject settingPage;

    private BattleManager _battleManager;
    private DungeonPage _page;
    [SerializeField] private DungeonItemHandView _itemHandView;
    [SerializeField]
    private DungeonActiveSkillResourceView _activeSkillResourceView;
    [SerializeField] private TextMeshProUGUI _pauseOverlayText;
    private bool _initialized;
    private bool _controlEventsBound;
    private bool _battleEventsBound;
    private bool _localizationEventsBound;
    [SerializeField] private RectTransform _partyInfoRect;

    public float BottomReservedHeight
    {
        get
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null || _partyInfoRect == null)
                return 0f;

            Bounds bounds = RectTransformUtility
                .CalculateRelativeRectTransformBounds(
                    parent,
                    _partyInfoRect);
            return Mathf.Max(0f, bounds.max.y - parent.rect.yMin);
        }
    }

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

    private void OnRectTransformDimensionsChange()
    {
        RefreshResponsiveLayout();
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

        ResolveActiveSkillResourceView();

        if (settingsButton != null)
        {
            TextMeshProUGUI settingsLabel =
                settingsButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (settingsLabel != null)
            {
                settingsLabel.text = LocalizationService.Get(
                    LocalizationKeys.UiCommonSettings);
            }
        }
        ResolvePauseNavigationButtons();

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
            _pausePanelFitter == null ||
            _activeSkillResourceView == null ||
            _itemHandView == null || _partyInfoRect == null ||
            settingsButton == null || dungeonPage == null ||
            settingPage == null)
        {
            Debug.LogError("DungeonBattleTab scene references are incomplete.", this);
            return false;
        }

        bool initialized = spawnQueueView.Initialize();
        RefreshResponsiveLayout();
        return initialized;
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

    private void ResolvePauseNavigationButtons()
    {
        if (pauseOverlay == null)
            return;

        if (_pauseMenuPanel == null)
        {
            _pauseMenuPanel = pauseOverlay.transform
                .Find("grpPauseMenuPanel") as RectTransform;
        }
        if (_pauseMenuPanel == null)
            return;

        if (_pauseOverlayText == null)
        {
            _pauseOverlayText = _pauseMenuPanel
                .Find("txtPauseOverlay")?.GetComponent<TextMeshProUGUI>();
        }
        if (_continueButton == null)
        {
            _continueButton = _pauseMenuPanel.Find("btnContinue")
                ?.GetComponent<Button>();
        }
        if (_returnToStageButton == null)
        {
            _returnToStageButton = _pauseMenuPanel.Find("btnReturnToStage")
                ?.GetComponent<Button>();
        }
        if (_quitGameButton == null)
        {
            _quitGameButton = _pauseMenuPanel.Find("btnQuitGame")
                ?.GetComponent<Button>();
        }
        if (_continueText == null && _continueButton != null)
        {
            _continueText = _continueButton
                .GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (_returnToStageText == null && _returnToStageButton != null)
        {
            _returnToStageText = _returnToStageButton
                .GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (_quitGameText == null && _quitGameButton != null)
        {
            _quitGameText = _quitGameButton
                .GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (_pausePanelFitter == null)
        {
            _pausePanelFitter =
                _pauseMenuPanel.GetComponent<ResponsivePanelFitter>();
        }
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
        string tooltip = _page != null
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

        float rechargeDuration = _battleManager != null
            ? _battleManager.ActiveSkillRechargeDuration
            : 0f;
        if (_activeSkillResourceView != null)
        {
            _activeSkillResourceView.Refresh(
                resource,
                maximumResource,
                rechargeRemaining,
                rechargeDuration,
                tooltip);
        }
        else
        {
            activeSkillResourceText.text =
                $"{resource}/{maximumResource}";
        }
    }

    private void RefreshResponsiveLayout()
    {
        _pausePanelFitter?.RefreshLayout();
        spawnQueueView?.ConfigureResponsiveBounds(_partyInfoRect);
        _itemHandView?.ConfigureResponsiveBounds(
            _partyInfoRect,
            spawnQueueView != null
                ? spawnQueueView.transform as RectTransform
                : null);
    }

    private void ResolveActiveSkillResourceView()
    {
        if (activeSkillResourceText == null)
            return;

        if (_activeSkillResourceView == null)
        {
            _activeSkillResourceView =
                GetComponentInChildren<DungeonActiveSkillResourceView>(true);
        }

        if (_partyInfoRect == null)
        {
            _partyInfoRect = transform.Find("grpPlayerPartyInfo")
                as RectTransform;
        }
        if (_activeSkillResourceView != null)
        {
            _activeSkillResourceView.Configure(
                activeSkillResourceIcon,
                activeSkillResourceText);
        }
    }

    private void EnsureItemHandView()
    {
        if (_page == null || _battleManager == null)
            return;

        if (_itemHandView == null)
            _itemHandView = GetComponentInChildren<DungeonItemHandView>(true);
        if (_itemHandView == null)
        {
            Debug.LogError(
                "Dungeon battle item hand must be placed in the Scene.",
                this);
            return;
        }

        _itemHandView.ConfigureResponsiveBounds(
            _partyInfoRect,
            spawnQueueView != null
                ? spawnQueueView.transform as RectTransform
                : null);
        _itemHandView.Initialize(_page, _battleManager);
    }
}

public abstract class DungeonActiveSkillResourceViewBase : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private TextMeshProUGUI _fallbackIconText;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private TextMeshProUGUI _tooltipText;
    [SerializeField] private GameObject _tooltip;

    public void Configure(
        Sprite iconSprite,
        TextMeshProUGUI amountText)
    {
        Transform iconFrame = transform.Find("grpResourceIcon");
        if (_iconImage == null)
        {
            _iconImage = iconFrame?.Find("imgResourceIcon")
                ?.GetComponent<Image>();
        }
        if (_fallbackIconText == null)
        {
            _fallbackIconText = iconFrame?.Find("txtResourceIcon")
                ?.GetComponent<TextMeshProUGUI>();
        }
        if (_cooldownOverlay == null)
        {
            _cooldownOverlay = iconFrame
                ?.Find("imgResourceCooldownOverlay")?.GetComponent<Image>();
        }
        if (_tooltip == null)
            _tooltip = transform.Find("grpResourceTooltip")?.gameObject;
        if (_tooltipText == null && _tooltip != null)
        {
            _tooltipText = _tooltip.transform.Find("txtResourceTooltip")
                ?.GetComponent<TextMeshProUGUI>();
        }
        _amountText = amountText != null ? amountText : _amountText;
        if (_iconImage == null || _fallbackIconText == null ||
            _cooldownOverlay == null || _amountText == null ||
            _tooltip == null || _tooltipText == null)
        {
            Debug.LogError(
                "Active skill resource Scene references are incomplete.",
                this);
            return;
        }

        _iconImage.sprite = iconSprite;
        _iconImage.enabled = iconSprite != null;
        _fallbackIconText.enabled = iconSprite == null;
        _cooldownOverlay.sprite = iconSprite;
        _cooldownOverlay.fillAmount = 0f;
        _cooldownOverlay.enabled = false;
        _tooltip.SetActive(false);
    }

    public void Refresh(
        int current,
        int maximum,
        float rechargeRemaining,
        float rechargeDuration,
        string tooltip)
    {
        maximum = Mathf.Max(0, maximum);
        current = Mathf.Clamp(current, 0, maximum);
        if (_amountText != null)
            _amountText.text = $"{current}/{maximum}";

        if (_cooldownOverlay != null)
        {
            float remainingRatio = current < maximum &&
                                   rechargeDuration > 0f
                ? Mathf.Clamp01(rechargeRemaining / rechargeDuration)
                : 0f;
            _cooldownOverlay.fillAmount = remainingRatio;
            _cooldownOverlay.enabled =
                _cooldownOverlay.sprite != null && remainingRatio > 0f;
        }

        if (_tooltipText != null)
            _tooltipText.text = tooltip ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    protected virtual void OnDisable()
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.SetActive(false);
    }

}

public abstract class DungeonItemHandViewBase : MonoBehaviour
{
    [SerializeField] private DungeonItemCardView cardPrefab;
    [SerializeField] private TextMeshProUGUI _instructionText;

    private readonly List<DungeonItemCardView> _cards = new();
    private readonly List<CharacterRuntime> _boundTurrets = new();
    private DungeonPage _page;
    private BattleManager _battleManager;
    private BattleItemSO _selectedItem;
    private bool _initialized;

    public RectTransform HighlightRect
    {
        get
        {
            foreach (DungeonItemCardView card in _cards)
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
        CollectAuthoredCards();
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

    public void ConfigureResponsiveBounds(
        RectTransform bottomReservedArea,
        RectTransform topAlignedArea)
    {
        RefreshCardLayout();
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

    protected virtual void OnDestroy()
    {
        Teardown();
    }

    protected virtual void OnRectTransformDimensionsChange()
    {
        RefreshCardLayout();
    }

    protected virtual void Update()
    {
        if (!_initialized || _battleManager == null)
            return;

        RefreshCards();
    }

    private void ConfigureRoot()
    {
        if (_instructionText == null)
        {
            _instructionText = transform.Find("txtItemTargetInstruction")
                ?.GetComponent<TextMeshProUGUI>();
        }
        if (_instructionText == null || cardPrefab == null)
        {
            Debug.LogError(
                "Dungeon item hand Scene references are incomplete. " +
                "Assign the instruction text and card prefab.",
                this);
        }
    }

    private void RebuildCards()
    {
        if (!_initialized || _page == null)
            return;

        foreach (DungeonItemCardView card in _cards)
        {
            if (card != null)
                card.gameObject.SetActive(false);
        }

        DungeonItemCardView resolvedCardPrefab = ResolveCardPrefab();
        if (resolvedCardPrefab == null)
        {
            Debug.LogError(
                "Battle item card prefab is not assigned in the inspector.",
                this);
            return;
        }

        RectTransform prefabRect =
            resolvedCardPrefab.transform as RectTransform;
        float cardHeight = prefabRect != null
            ? Mathf.Max(1f, prefabRect.sizeDelta.y)
            : 1f;

        int visibleCount = 0;
        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            visibleCount += GetVisibleCardCount(item);
        }

        int visibleIndex = 0;
        float handHeight = ((RectTransform)transform).rect.height;
        float maximumCardStep = cardHeight * 0.6f;
        float cardStep = visibleCount > 1
            ? Mathf.Min(
                maximumCardStep,
                Mathf.Max(0f, handHeight - cardHeight) /
                (visibleCount - 1))
            : 0f;
        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            int itemCardCount = GetVisibleCardCount(item);
            if (itemCardCount <= 0)
                continue;

            for (int copyIndex = 0; copyIndex < itemCardCount; copyIndex++)
            {
                DungeonItemCardView card = GetOrCreateCard(
                    visibleIndex,
                    resolvedCardPrefab);
                card.name =
                    $"crdBattleItem_{item.ItemId.Replace('.', '_')}_" +
                    $"{copyIndex + 1}";
                card.gameObject.SetActive(true);
                RectTransform cardRect =
                    card.transform as RectTransform;
                cardRect.anchorMin = new Vector2(0f, 0.5f);
                cardRect.anchorMax = new Vector2(0f, 0.5f);
                cardRect.pivot = new Vector2(0f, 0.5f);
                float top =
                    (visibleCount - 1) * cardStep * 0.5f;
                cardRect.anchoredPosition = new Vector2(
                    0f,
                    top - visibleIndex * cardStep);

                if (!card.Initialize(item, HandleCardClicked))
                {
                    card.gameObject.SetActive(false);
                    continue;
                }
                visibleIndex++;
            }
        }

        RefreshTurretBindings();
        RefreshCardLayout();
        RefreshCards();
    }

    private void CollectAuthoredCards()
    {
        for (int index = 0; index < transform.childCount; index++)
        {
            DungeonItemCardView card = transform.GetChild(index)
                .GetComponent<DungeonItemCardView>();
            if (card == null || _cards.Contains(card))
                continue;

            card.gameObject.SetActive(false);
            _cards.Add(card);
        }
    }

    private DungeonItemCardView GetOrCreateCard(
        int index,
        DungeonItemCardView prefab)
    {
        while (_cards.Count <= index)
        {
            DungeonItemCardView instance = Instantiate(
                prefab,
                transform,
                false);
            instance.gameObject.SetActive(false);
            _cards.Add(instance);
        }

        return _cards[index];
    }

    private void RefreshCardLayout()
    {
        int visibleCount = 0;
        float cardHeight = 0f;
        for (int index = 0; index < _cards.Count; index++)
        {
            DungeonItemCardView card = _cards[index];
            if (card == null || !card.gameObject.activeSelf)
                continue;

            visibleCount++;
            if (cardHeight <= 0f &&
                card.transform is RectTransform cardRect)
            {
                cardHeight = cardRect.rect.height;
            }
        }

        if (visibleCount == 0 || cardHeight <= 0f)
            return;

        float handHeight = ((RectTransform)transform).rect.height;
        float cardStep = visibleCount > 1
            ? Mathf.Min(
                cardHeight * 0.6f,
                Mathf.Max(0f, handHeight - cardHeight) /
                (visibleCount - 1))
            : 0f;
        float top = (visibleCount - 1) * cardStep * 0.5f;
        int visibleIndex = 0;
        for (int index = 0; index < _cards.Count; index++)
        {
            DungeonItemCardView card = _cards[index];
            if (card == null || !card.gameObject.activeSelf ||
                card.transform is not RectTransform cardRect)
            {
                continue;
            }

            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(0f, 0.5f);
            cardRect.pivot = new Vector2(0f, 0.5f);
            cardRect.anchoredPosition = new Vector2(
                0f,
                top - visibleIndex * cardStep);
            visibleIndex++;
        }
    }

    private DungeonItemCardView ResolveCardPrefab()
    {
        return cardPrefab;
    }

    private int GetVisibleCardCount(BattleItemSO item)
    {
        if (item == null)
            return 0;

        return ResolveVisibleCardCount(
            item,
            _page.IsBattleItemOwned(item),
            _page.GetBattleItemCount(item));
    }

    private static int ResolveVisibleCardCount(
        BattleItemSO item,
        bool isOwned,
        int remainingUses)
    {
        if (item == null || !isOwned)
            return 0;

        return item.HasUnlimitedUses ? 1 : Mathf.Max(0, remainingUses);
    }

    private void RefreshCards()
    {
        if (_page == null || _battleManager == null)
            return;

        foreach (DungeonItemCardView card in _cards)
        {
            if (card == null)
                continue;

            BattleItemSO item = card.Item;
            if (item == null)
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
public class DungeonItemCardView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("Prefab References")]
    [SerializeField] private Image background;
    [SerializeField] private Image illustration;
    [SerializeField] private Image statusOverlay;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private GameObject popup;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("State Colors")]
    [SerializeField] private Color availableColor =
        new(0.18f, 0.28f, 0.22f, 0.98f);
    [SerializeField] private Color selectedColor =
        new(0.22f, 0.48f, 0.3f, 1f);
    [SerializeField] private Color disabledColor =
        new(0.08f, 0.1f, 0.09f, 0.92f);
    [SerializeField] private Color selectedOverlayColor =
        new(0.1f, 0.45f, 0.22f, 0.3f);
    [SerializeField] private Color disabledOverlayColor =
        new(0.015f, 0.02f, 0.018f, 0.68f);

    [Header("Interaction")]
    [SerializeField, Min(1f)] private float hoverScale = 1.12f;
    [SerializeField, Min(0f)] private float hoverResponse = 18f;

    private BattleItemSO _item;
    private System.Action<BattleItemSO> _clicked;
    private bool _hovered;
    private PopupLayerPlacement _popupLayerPlacement;

    public BattleItemSO Item => _item;

    public bool Initialize(
        BattleItemSO item,
        System.Action<BattleItemSO> clicked)
    {
        if (item == null || !HasRequiredPrefabReferences())
        {
            Debug.LogError(
                "DungeonItemCardView prefab references are incomplete.",
                this);
            return false;
        }

        _item = item;
        _clicked = clicked;
        background.color = availableColor;
        illustration.sprite = item.Illustration;
        illustration.enabled = item.Illustration != null;
        icon.sprite = item.Icon;
        icon.enabled = item.Icon != null;
        nameText.text = item.GetLocalizedDisplayName();
        stateText.text = string.Empty;
        costText.text = item.EnergyCost.ToString();
        statusOverlay.color = Color.clear;
        RefreshDetailText(string.Empty);
        popup.SetActive(false);
        return true;
    }

    public bool HasRequiredPrefabReferences()
    {
        return background != null &&
               illustration != null &&
               statusOverlay != null &&
               icon != null &&
               nameText != null &&
               stateText != null &&
               costText != null &&
               popup != null &&
               detailText != null;
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
        background.color = selected
            ? selectedColor
            : available
                ? availableColor
                : disabledColor;
        statusOverlay.color = selected
            ? selectedOverlayColor
            : available
                ? Color.clear
                : disabledOverlayColor;

        string state = cooldown > 0f
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonItemCooldown,
                LocalizationService.Arg(
                    "cooldown",
                    TimePrecision.FloorToTenth(cooldown)))
            : _item.HasUnlimitedUses
                ? "∞"
                : string.Empty;
        nameText.text = _item.GetLocalizedDisplayName();
        stateText.text = state;
        costText.text = _item.EnergyCost.ToString();
        RefreshDetailText(state);
    }

    private void RefreshDetailText(string state)
    {
        string header = LocalizationService.Get(
            LocalizationKeys.UiDungeonItemCardHeader,
            LocalizationService.Arg(
                "name",
                _item.GetLocalizedDisplayName()),
            LocalizationService.Arg("cost", _item.EnergyCost));
        string footer = GetLifecycleText(_item);
        string effectScope = GetEffectScopeText(_item);
        detailText.text =
            $"{header}\n{_item.GetLocalizedDescription()}\n" +
            $"{footer}{effectScope}" +
            (string.IsNullOrWhiteSpace(state) ? string.Empty : $"\n{state}");
    }

    private static string GetLifecycleText(BattleItemSO item)
    {
        if (item.IsDisposable)
        {
            return LocalizationService.Get(
                LocalizationKeys.UiDungeonItemDisposable);
        }
        if (item.HasUnlimitedUses)
        {
            return LocalizationService.Get(
                LocalizationKeys.UiDungeonItemReusableUnlimited);
        }
        return LocalizationService.Get(
            LocalizationKeys.UiDungeonItemReusableLimited,
            LocalizationService.Arg("uses", item.UsesPerBattle));
    }

    private static string GetEffectScopeText(BattleItemSO item)
    {
        if (item?.UsesUnifiedAbilityEffects == true)
        {
            CharacterEffectDefinition ability = item.AbilityEffects[0];
            if (ability?.Type != CharacterEffectType.ApplyStatus ||
                ability.StatusEffect == null)
            {
                return string.Empty;
            }

            string abilityScope = LocalizationService.Get(
                LocalizationKeys.UiDungeonItemScopeBattle);
            float resolvedDuration = ability.StatusDuration > 0f
                ? ability.StatusDuration
                : ability.StatusEffect.DefaultDuration;
            string abilityDuration =
                item.StatusEffectsLastUntilBattleEnd ||
                ability.StatusEffect.DurationMode ==
                    StatusEffectDurationMode.Permanent
                ? LocalizationService.Get(
                    LocalizationKeys.UiDungeonItemDurationPermanent)
                : $"{resolvedDuration:0.##}s";
            return $" · {abilityScope}/{abilityDuration}";
        }

        if (item?.Effects == null || item.Effects.Count == 0)
            return string.Empty;

        BattleItemEffectDefinition effect = item.Effects[0];
        if (effect == null ||
            effect.DurationMode == BattleItemEffectDurationMode.Instant)
        {
            return string.Empty;
        }

        string scope = effect.Scope ==
                       BattleItemEffectScope.CurrentDungeon
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonItemScopeDungeon)
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonItemScopeBattle);
        string duration = effect.DurationMode ==
                          BattleItemEffectDurationMode.Permanent
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonItemDurationPermanent)
            : $"{effect.Duration:0.##}s";
        return $" · {scope}/{duration}";
    }

    private void Update()
    {
        Vector3 targetScale = _hovered
            ? new Vector3(hoverScale, hoverScale, 1f)
            : Vector3.one;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-hoverResponse * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        transform.SetAsLastSibling();
        if (popup != null)
        {
            if (_popupLayerPlacement.IsActive &&
                !PopupLayerUtility.Restore(_popupLayerPlacement))
            {
                return;
            }
            _popupLayerPlacement = default;
            popup.SetActive(true);
            _popupLayerPlacement = PopupLayerUtility.MoveToPopupLayer(
                popup.transform as RectTransform,
                transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        HidePopup();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left)
        {
            _clicked?.Invoke(_item);
        }
    }

    private void OnDisable()
    {
        _hovered = false;
        HidePopup();
        transform.localScale = Vector3.one;
    }

    private void HidePopup()
    {
        if (popup != null)
            popup.SetActive(false);
        if (_popupLayerPlacement.IsActive &&
            PopupLayerUtility.Restore(_popupLayerPlacement))
        {
            _popupLayerPlacement = default;
        }
    }

}
