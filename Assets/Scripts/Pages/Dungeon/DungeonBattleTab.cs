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
    [SerializeField] private Sprite activeSkillResourceIcon;

    [Header("Enemy Spawn Queue")]
    [SerializeField] private DungeonSpawnQueueView spawnQueueView;

    [Header("Page Navigation")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private GameObject settingPage;

    private BattleManager _battleManager;
    private DungeonPage _page;
    private DungeonItemHandView _itemHandView;
    private DungeonActiveSkillResourceView _activeSkillResourceView;
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

        ConfigurePlayerPartyLayout();
        EnsureActiveSkillResourceView();

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

    private void ConfigurePlayerPartyLayout()
    {
        Transform partyInfo = transform.Find("grpPlayerPartyInfo");
        if (partyInfo == null && activeSkillResourceText != null)
            partyInfo = activeSkillResourceText.transform.parent;
        if (partyInfo == null)
            return;

        RectTransform partyRect = partyInfo as RectTransform;
        if (partyRect != null)
            partyRect.sizeDelta = new Vector2(partyRect.sizeDelta.x, 152f);

        HorizontalLayoutGroup layout =
            partyInfo.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (layout == null)
            return;

        layout.padding = new RectOffset(16, 16, 4, 4);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutRebuilder.MarkLayoutForRebuild(
            layout.transform as RectTransform);
    }

    private void EnsureActiveSkillResourceView()
    {
        if (activeSkillResourceText == null)
            return;

        if (_activeSkillResourceView == null)
        {
            _activeSkillResourceView =
                GetComponentInChildren<DungeonActiveSkillResourceView>(true);
        }

        Transform partyInfo = transform.Find("grpPlayerPartyInfo");
        if (_activeSkillResourceView == null)
        {
            GameObject resourceObject = new(
                "grpActiveSkillResource",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(DungeonActiveSkillResourceView));
            resourceObject.transform.SetParent(transform, false);
            _activeSkillResourceView =
                resourceObject.GetComponent<DungeonActiveSkillResourceView>();
            if (partyInfo != null)
            {
                resourceObject.transform.SetSiblingIndex(
                    partyInfo.GetSiblingIndex());
            }
        }

        RectTransform resourceRect =
            _activeSkillResourceView.transform as RectTransform;
        resourceRect.anchorMin = Vector2.zero;
        resourceRect.anchorMax = Vector2.zero;
        resourceRect.pivot = Vector2.zero;
        resourceRect.anchoredPosition = new Vector2(24f, 164f);
        resourceRect.sizeDelta = new Vector2(104f, 104f);
        resourceRect.localScale = Vector3.one;
        _activeSkillResourceView.Configure(
            activeSkillResourceIcon,
            activeSkillResourceText);
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
public sealed class DungeonActiveSkillResourceView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Image _iconImage;
    private Image _cooldownOverlay;
    private TextMeshProUGUI _fallbackIconText;
    private TextMeshProUGUI _amountText;
    private TextMeshProUGUI _tooltipText;
    private GameObject _tooltip;

    public void Configure(
        Sprite iconSprite,
        TextMeshProUGUI amountText)
    {
        Image hitArea = GetComponent<Image>() ??
                        gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        Outline outline = GetComponent<Outline>() ??
                          gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.78f, 0.58f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);

        Image iconFrame = EnsureImage(transform, "grpResourceIcon");
        RectTransform iconFrameRect = iconFrame.rectTransform;
        iconFrameRect.anchorMin = Vector2.zero;
        iconFrameRect.anchorMax = Vector2.zero;
        iconFrameRect.pivot = Vector2.zero;
        iconFrameRect.anchoredPosition = Vector2.zero;
        iconFrameRect.sizeDelta = new Vector2(104f, 104f);
        iconFrame.color = new Color(0.035f, 0.075f, 0.065f, 0.96f);

        _iconImage = EnsureImage(iconFrameRect, "imgResourceIcon");
        RectTransform iconRect = _iconImage.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(10f, 10f);
        iconRect.offsetMax = new Vector2(-10f, -10f);
        _iconImage.sprite = iconSprite;
        _iconImage.preserveAspect = true;
        _iconImage.enabled = iconSprite != null;
        _iconImage.color = new Color(0.35f, 0.92f, 0.68f, 0.72f);

        _fallbackIconText = EnsureText(iconFrameRect, "txtResourceIcon");
        RectTransform fallbackIconRect = _fallbackIconText.rectTransform;
        fallbackIconRect.anchorMin = Vector2.zero;
        fallbackIconRect.anchorMax = Vector2.one;
        fallbackIconRect.offsetMin = Vector2.zero;
        fallbackIconRect.offsetMax = Vector2.zero;
        _fallbackIconText.text = "◆";
        _fallbackIconText.fontSize = 68f;
        _fallbackIconText.color =
            new Color(0.35f, 0.92f, 0.68f, 0.38f);
        _fallbackIconText.alignment = TextAlignmentOptions.Center;
        _fallbackIconText.raycastTarget = false;
        _fallbackIconText.enabled = iconSprite == null;
        LocalizationFontResolver.ApplyGameDefault(_fallbackIconText);

        _cooldownOverlay =
            EnsureImage(iconFrameRect, "imgResourceCooldownOverlay");
        RectTransform overlayRect = _cooldownOverlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = new Vector2(10f, 10f);
        overlayRect.offsetMax = new Vector2(-10f, -10f);
        _cooldownOverlay.sprite = iconSprite;
        _cooldownOverlay.preserveAspect = true;
        _cooldownOverlay.color = new Color(0f, 0f, 0f, 0.68f);
        _cooldownOverlay.type = Image.Type.Filled;
        _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        _cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        _cooldownOverlay.fillClockwise = true;
        _cooldownOverlay.fillAmount = 0f;
        _cooldownOverlay.enabled = false;

        _amountText = amountText;
        _amountText.transform.SetParent(iconFrameRect, false);
        RectTransform amountRect = _amountText.rectTransform;
        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.offsetMin = new Vector2(4f, 4f);
        amountRect.offsetMax = new Vector2(-4f, -4f);
        amountRect.localScale = Vector3.one;
        _amountText.alignment = TextAlignmentOptions.Center;
        _amountText.fontSize = 25f;
        _amountText.fontStyle = FontStyles.Bold;
        _amountText.enableAutoSizing = true;
        _amountText.fontSizeMin = 16f;
        _amountText.fontSizeMax = 25f;
        _amountText.color = Color.white;
        _amountText.raycastTarget = false;
        LocalizationFontResolver.ApplyGameDefault(_amountText);
        _amountText.transform.SetAsLastSibling();

        Transform legacyGauge = transform.Find("grpResourceRechargeGauge");
        if (legacyGauge != null)
            legacyGauge.gameObject.SetActive(false);

        EnsureTooltip();
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
        _tooltip?.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.SetActive(false);
    }

    private void OnDisable()
    {
        _tooltip?.SetActive(false);
    }

    private void EnsureTooltip()
    {
        if (_tooltip != null)
            return;

        Transform existing = transform.Find("grpResourceTooltip");
        _tooltip = existing != null
            ? existing.gameObject
            : new GameObject(
                "grpResourceTooltip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
        if (existing == null)
            _tooltip.transform.SetParent(transform, false);

        RectTransform tooltipRect = _tooltip.transform as RectTransform;
        tooltipRect.anchorMin = new Vector2(0f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 0.5f);
        tooltipRect.anchoredPosition = new Vector2(114f, 0f);
        tooltipRect.sizeDelta = new Vector2(360f, 118f);
        Image tooltipImage = _tooltip.GetComponent<Image>() ??
                             _tooltip.AddComponent<Image>();
        tooltipImage.color = new Color(0.035f, 0.055f, 0.045f, 0.99f);
        tooltipImage.raycastTarget = false;

        Transform existingText = tooltipRect.Find("txtResourceTooltip");
        if (existingText != null)
        {
            _tooltipText = existingText.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            GameObject textObject = new(
                "txtResourceTooltip",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(tooltipRect, false);
            _tooltipText = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform textRect = _tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        LocalizationFontResolver.ApplyGameDefault(_tooltipText);
        _tooltipText.fontSize = 17f;
        _tooltipText.fontStyle = FontStyles.Bold;
        _tooltipText.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        _tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        _tooltipText.raycastTarget = false;
        _tooltip.SetActive(false);
    }

    private static Image EnsureImage(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
        if (existing == null)
            imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>() ??
                      imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI EnsureText(
        Transform parent,
        string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
        if (existing == null)
            textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TextMeshProUGUI>() ??
               textObject.AddComponent<TextMeshProUGUI>();
    }
}

[DisallowMultipleComponent]
public sealed class DungeonItemHandView : MonoBehaviour
{
    private const string DefaultCardPrefabResourcePath =
        "Presentation/BattleItemCard";
    private const float HandHeight = 620f;
    private const float MaximumCardStep = 132f;

    [SerializeField] private DungeonItemCardView cardPrefab;

    private readonly List<DungeonItemCardView> _cards = new();
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
        root.anchoredPosition = new Vector2(18f, 56f);
        root.sizeDelta = new Vector2(176f, HandHeight);
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

        foreach (DungeonItemCardView card in _cards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
        }
        _cards.Clear();

        DungeonItemCardView resolvedCardPrefab = ResolveCardPrefab();
        if (resolvedCardPrefab == null)
        {
            Debug.LogError(
                $"Battle item card prefab was not found at Resources/" +
                $"{DefaultCardPrefabResourcePath}.",
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
        float cardStep = visibleCount > 1
            ? Mathf.Min(
                MaximumCardStep,
                (HandHeight - cardHeight) / (visibleCount - 1))
            : 0f;
        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            int itemCardCount = GetVisibleCardCount(item);
            if (itemCardCount <= 0)
                continue;

            for (int copyIndex = 0; copyIndex < itemCardCount; copyIndex++)
            {
                DungeonItemCardView card = Instantiate(
                    resolvedCardPrefab,
                    transform,
                    false);
                card.name =
                    $"crdBattleItem_{item.ItemId.Replace('.', '_')}_" +
                    $"{copyIndex + 1}";
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
                    Destroy(card.gameObject);
                    continue;
                }
                _cards.Add(card);
                visibleIndex++;
            }
        }

        RefreshTurretBindings();
        RefreshCards();
    }

    private DungeonItemCardView ResolveCardPrefab()
    {
        if (cardPrefab == null)
        {
            GameObject prefabObject = Resources.Load<GameObject>(
                DefaultCardPrefabResourcePath);
            cardPrefab = prefabObject != null
                ? prefabObject.GetComponent<DungeonItemCardView>()
                : null;
        }

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
        popup?.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        popup?.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left)
        {
            _clicked?.Invoke(_item);
        }
    }

}
