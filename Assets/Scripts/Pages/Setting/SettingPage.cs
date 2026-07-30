using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingPage : MonoBehaviour, IPage
{
    private static readonly Color SelectedTabColor =
        new(0.18f, 0.36f, 0.32f, 1f);
    private static readonly Color UnselectedTabColor =
        new(0.08f, 0.16f, 0.15f, 1f);
    private static readonly Color DataManagementCardColor =
        new(0.075f, 0.14f, 0.13f, 1f);
    private static readonly Color DestructiveButtonColor =
        new(0.52f, 0.12f, 0.1f, 1f);
    private static readonly Color ContinueButtonColor =
        new(0.18f, 0.36f, 0.32f, 1f);
    private static readonly string[] DisplayModeLocalizationKeys =
    {
        LocalizationKeys.UiSettingsModeFullscreen,
        LocalizationKeys.UiSettingsModeBorderless,
        LocalizationKeys.UiSettingsModeWindowed,
    };
    private static readonly int[] FrameRateValues =
    {
        0,
        120,
        60,
        30,
    };

    [Header("Navigation")]
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private Button backButton;

    [Header("Tab Buttons")]
    [SerializeField] private Button displayTabButton;
    [SerializeField] private Button soundTabButton;
    [SerializeField] private Button gameTabButton;
    [SerializeField] private Button miscTabButton;

    [Header("Setting Tabs")]
    [SerializeField] private GameObject displayTab;
    [SerializeField] private GameObject soundTab;
    [SerializeField] private GameObject gameTab;
    [SerializeField] private GameObject miscTab;

    [Header("Display Controls")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TextMeshProUGUI brightnessValueText;

    [Header("Sound Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeValueText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sfxVolumeValueText;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private TextMeshProUGUI uiVolumeValueText;
    [SerializeField] private ToggleSliderController muteInBackgroundToggle;

    [Header("Game Controls (Optional - generated at runtime)")]
    [SerializeField] private TMP_Dropdown localeDropdown;

    [Header("Quit Confirmation")]
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject quitConfirmationPopup;
    [SerializeField] private Button quitOkButton;
    [SerializeField] private Button quitCancelButton;

    private Button _deleteLocalDataButton;
    private GameObject _localDataResetPopup;
    private Button _localDataResetConfirmButton;
    private Button _localDataResetCancelButton;
    private TextMeshProUGUI _localDataResetTitleText;
    private TextMeshProUGUI _localDataResetMessageText;
    private TextMeshProUGUI _localDataResetConfirmText;
    private int _localDataResetConfirmationStep;
    private bool _initialized;
    private bool _eventsBound;
    private bool _isRefreshingControls;
    private int _selectedTabIndex;
    private GameObject _returnPage;
    private PageOpenMode _returnMode = PageOpenMode.Resume;
    private GameEventManager _gameEvents;
    private DataManager _dataManager;
    private readonly List<string> _supportedResolutions = new();
    private readonly List<string> _supportedLocaleIds = new();
    private TextMeshProUGUI _localeLabelText;
    private bool _openedFromDungeon;

    public AudioSource Speaker { get; set; }

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        BindEvents();
        SelectTab(_selectedTabIndex);
        RefreshSettingsControls();
        HideQuitConfirmation();
        HideLocalDataResetConfirmation();
        RefreshQuitButtonAvailability();
    }

    private void OnDisable()
    {
        UnbindEvents();
        HideQuitConfirmation();
        HideLocalDataResetConfirmation();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        gameObject.SetActive(true);

        if (!_initialized)
            Init();
        if (!_initialized)
            return;

        BindEvents();
        if (mode == PageOpenMode.Fresh)
            _selectedTabIndex = 0;

        SelectTab(_selectedTabIndex);
        RefreshSettingsControls();
        HideQuitConfirmation();
        HideLocalDataResetConfirmation();
        RefreshQuitButtonAvailability();
    }

    public void OpenFrom(
        GameObject sourcePage,
        PageOpenMode returnMode = PageOpenMode.Resume)
    {
        if (sourcePage == null)
        {
            Debug.LogError(
                "SettingPage requires a source page for return navigation.",
                this);
            return;
        }

        _returnPage = sourcePage;
        _returnMode = returnMode;
        _openedFromDungeon =
            sourcePage.TryGetComponent(out DungeonPage _);
        PageControl.PagToPag(sourcePage, gameObject, PageOpenMode.Fresh);
    }

    public void Close()
    {
        SaveSettings();
        UnbindEvents();
        HideQuitConfirmation();
        HideLocalDataResetConfirmation();
        gameObject.SetActive(false);
    }

    public void Init()
    {
        if (_initialized)
            return;

        EnsureLocalizationControls();
        if (!ValidateReferences())
            return;

        EnsureLocalDataResetControls();
        BindSceneLocalizedTexts();

        ResolveSettingManagers();
        _returnPage ??= dungeonPage;
        BuildSupportedResolutionList();
        _initialized = true;
        _selectedTabIndex = 0;
        SelectTab(_selectedTabIndex);
        RefreshSettingsControls();
        HideQuitConfirmation();
        HideLocalDataResetConfirmation();
        RefreshQuitButtonAvailability();

        if (isActiveAndEnabled)
            BindEvents();
    }

    private bool ValidateReferences()
    {
        if (dungeonPage == null || backButton == null ||
            displayTabButton == null || soundTabButton == null ||
            gameTabButton == null || miscTabButton == null ||
            displayTab == null || soundTab == null ||
            gameTab == null || miscTab == null ||
            resolutionDropdown == null || displayModeDropdown == null ||
            frameRateDropdown == null || brightnessSlider == null ||
            brightnessValueText == null ||
            masterVolumeSlider == null || masterVolumeValueText == null ||
            musicVolumeSlider == null || musicVolumeValueText == null ||
            sfxVolumeSlider == null || sfxVolumeValueText == null ||
            uiVolumeSlider == null || uiVolumeValueText == null ||
            muteInBackgroundToggle == null ||
            quitButton == null || quitConfirmationPopup == null ||
            quitOkButton == null || quitCancelButton == null)
        {
            Debug.LogError("SettingPage scene references are incomplete.", this);
            return false;
        }

        return true;
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        backButton.onClick.AddListener(HandleBackClicked);
        displayTabButton.onClick.AddListener(HandleDisplayTabClicked);
        soundTabButton.onClick.AddListener(HandleSoundTabClicked);
        gameTabButton.onClick.AddListener(HandleGameTabClicked);
        miscTabButton.onClick.AddListener(HandleMiscTabClicked);
        resolutionDropdown.onValueChanged.AddListener(HandleResolutionSelected);
        displayModeDropdown.onValueChanged.AddListener(HandleDisplayModeSelected);
        frameRateDropdown.onValueChanged.AddListener(HandleFrameRateSelected);
        brightnessSlider.onValueChanged.AddListener(HandleBrightnessChanged);
        masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        uiVolumeSlider.onValueChanged.AddListener(HandleUiVolumeChanged);
        muteInBackgroundToggle.ValueChanged += HandleMuteInBackgroundChanged;
        localeDropdown?.onValueChanged.AddListener(HandleLocaleSelected);
        LocalizationService.LocaleChanged +=
            HandleLocalizationLocaleChanged;
        quitButton.onClick.AddListener(HandleQuitClicked);
        quitOkButton.onClick.AddListener(HandleQuitOkClicked);
        quitCancelButton.onClick.AddListener(HideQuitConfirmation);
        _deleteLocalDataButton?.onClick.AddListener(
            HandleDeleteLocalDataClicked);
        _localDataResetConfirmButton?.onClick.AddListener(
            HandleLocalDataResetConfirmClicked);
        _localDataResetCancelButton?.onClick.AddListener(
            HideLocalDataResetConfirmation);

        if (_gameEvents != null)
        {
            _gameEvents.DataReady += RefreshSettingsControls;
            _gameEvents.DisplayFPSChanged += RefreshFrameRate;
            _gameEvents.DisplayModeChanged += RefreshDisplayMode;
            _gameEvents.ResolutionChanged += RefreshResolution;
            _gameEvents.DisplayBrightnessChanged += RefreshBrightness;
            _gameEvents.AudioVolumeChanged += RefreshAudioVolume;
            _gameEvents.MuteInBackgroundChanged += RefreshMuteInBackground;
            _gameEvents.LocaleChanged += RefreshLocale;
        }

        _eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!_eventsBound)
            return;

        if (backButton != null)
            backButton.onClick.RemoveListener(HandleBackClicked);
        if (displayTabButton != null)
            displayTabButton.onClick.RemoveListener(HandleDisplayTabClicked);
        if (soundTabButton != null)
            soundTabButton.onClick.RemoveListener(HandleSoundTabClicked);
        if (gameTabButton != null)
            gameTabButton.onClick.RemoveListener(HandleGameTabClicked);
        if (miscTabButton != null)
            miscTabButton.onClick.RemoveListener(HandleMiscTabClicked);
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveListener(HandleResolutionSelected);
        if (displayModeDropdown != null)
            displayModeDropdown.onValueChanged.RemoveListener(HandleDisplayModeSelected);
        if (frameRateDropdown != null)
            frameRateDropdown.onValueChanged.RemoveListener(HandleFrameRateSelected);
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(HandleBrightnessChanged);
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        if (uiVolumeSlider != null)
            uiVolumeSlider.onValueChanged.RemoveListener(HandleUiVolumeChanged);
        if (muteInBackgroundToggle != null)
            muteInBackgroundToggle.ValueChanged -= HandleMuteInBackgroundChanged;
        if (localeDropdown != null)
            localeDropdown.onValueChanged.RemoveListener(HandleLocaleSelected);
        LocalizationService.LocaleChanged -=
            HandleLocalizationLocaleChanged;
        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitClicked);
        if (quitOkButton != null)
            quitOkButton.onClick.RemoveListener(HandleQuitOkClicked);
        if (quitCancelButton != null)
            quitCancelButton.onClick.RemoveListener(HideQuitConfirmation);
        if (_deleteLocalDataButton != null)
        {
            _deleteLocalDataButton.onClick.RemoveListener(
                HandleDeleteLocalDataClicked);
        }
        if (_localDataResetConfirmButton != null)
        {
            _localDataResetConfirmButton.onClick.RemoveListener(
                HandleLocalDataResetConfirmClicked);
        }
        if (_localDataResetCancelButton != null)
        {
            _localDataResetCancelButton.onClick.RemoveListener(
                HideLocalDataResetConfirmation);
        }

        if (_gameEvents != null)
        {
            _gameEvents.DataReady -= RefreshSettingsControls;
            _gameEvents.DisplayFPSChanged -= RefreshFrameRate;
            _gameEvents.DisplayModeChanged -= RefreshDisplayMode;
            _gameEvents.ResolutionChanged -= RefreshResolution;
            _gameEvents.DisplayBrightnessChanged -= RefreshBrightness;
            _gameEvents.AudioVolumeChanged -= RefreshAudioVolume;
            _gameEvents.MuteInBackgroundChanged -= RefreshMuteInBackground;
            _gameEvents.LocaleChanged -= RefreshLocale;
        }

        _eventsBound = false;
    }

    private void HandleBackClicked()
    {
        if (_localDataResetPopup != null &&
            _localDataResetPopup.activeSelf)
        {
            HideLocalDataResetConfirmation();
            return;
        }

        if (quitConfirmationPopup != null &&
            quitConfirmationPopup.activeSelf)
        {
            HideQuitConfirmation();
            return;
        }

        GameObject targetPage = _returnPage != null
            ? _returnPage
            : dungeonPage;
        PageControl.PagToPag(gameObject, targetPage, _returnMode);
    }

    private void HandleDisplayTabClicked()
    {
        SelectTab(0);
    }

    private void HandleSoundTabClicked()
    {
        SelectTab(1);
    }

    private void HandleGameTabClicked()
    {
        SelectTab(2);
    }

    private void HandleMiscTabClicked()
    {
        SelectTab(3);
    }

    private void HandleResolutionSelected(int index)
    {
        if (_isRefreshingControls || index < 0 ||
            index >= _supportedResolutions.Count)
        {
            return;
        }

        string selected = _supportedResolutions[index];

        if (_gameEvents != null)
            _gameEvents.RequestResolutionChange(selected);
        else
            _dataManager?.SetResolution(selected);
    }

    private void HandleDisplayModeSelected(int index)
    {
        if (_isRefreshingControls || !DisplayData.IsValidDisplayMode(index))
            return;

        if (_gameEvents != null)
            _gameEvents.RequestDisplayModeChange(index);
        else
            _dataManager?.SetDisplayMode(index);
    }

    private void HandleFrameRateSelected(int index)
    {
        if (_isRefreshingControls || !DisplayData.IsValidFpsMode(index))
            return;

        if (_gameEvents != null)
            _gameEvents.RequestDisplayFPSChange(index);
        else
            _dataManager?.SetDisplayFPS(index);
    }

    private void HandleBrightnessChanged(float value)
    {
        if (_isRefreshingControls)
            return;

        int brightness = Mathf.RoundToInt(value);
        if (_gameEvents != null)
            _gameEvents.RequestDisplayBrightnessChange(brightness);
        else
            _dataManager?.SetDisplayBrightness(brightness);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        RequestAudioVolume(EAudioChannel.Master, value);
    }

    private void HandleMusicVolumeChanged(float value)
    {
        RequestAudioVolume(EAudioChannel.Music, value);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        RequestAudioVolume(EAudioChannel.SFX, value);
    }

    private void HandleUiVolumeChanged(float value)
    {
        RequestAudioVolume(EAudioChannel.UI, value);
    }

    private void HandleMuteInBackgroundChanged(bool value)
    {
        if (_isRefreshingControls)
            return;

        if (_gameEvents != null)
            _gameEvents.RequestMuteInBackgroundChange(value);
        else
            _dataManager?.SetMuteInBackground(value);
    }

    private void HandleLocaleSelected(int index)
    {
        if (_isRefreshingControls || index < 0 ||
            index >= _supportedLocaleIds.Count)
        {
            return;
        }

        string locale = _supportedLocaleIds[index];
        if (_gameEvents != null)
        {
            _gameEvents.RequestLocaleChange(locale);
        }
        else if (LocalizationService.SetLocale(locale))
        {
            RefreshLocale(LocalizationService.CurrentLocale);
        }
    }

    private void HandleLocalizationLocaleChanged(string locale)
    {
        bool wasRefreshing = _isRefreshingControls;
        _isRefreshingControls = true;

        RebuildLocalizedDisplayOptions();
        BuildLocaleOptionList();
        RefreshLocale(locale);
        RefreshLocalizationPresentation();

        _isRefreshingControls = wasRefreshing;
    }

    private void HandleQuitClicked()
    {
        HideLocalDataResetConfirmation();
        quitConfirmationPopup.SetActive(true);
        quitConfirmationPopup.transform.SetAsLastSibling();
    }

    private void HandleQuitOkClicked()
    {
        SaveSettings();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SaveSettings()
    {
        if (_gameEvents != null)
            _gameEvents.RequestSaveAll();
        else
            _dataManager?.SaveALL();
    }

    private void HideQuitConfirmation()
    {
        if (quitConfirmationPopup != null)
            quitConfirmationPopup.SetActive(false);
    }

    private void HandleDeleteLocalDataClicked()
    {
        HideQuitConfirmation();
        ShowLocalDataResetConfirmation(1);
    }

    private void HandleLocalDataResetConfirmClicked()
    {
        if (_localDataResetConfirmationStep == 1)
        {
            ShowLocalDataResetConfirmation(2);
            return;
        }

        if (_localDataResetConfirmationStep != 2 ||
            !LocalDataResetService.TryDeleteAllLocalData())
        {
            return;
        }

        UnbindEvents();
        if (_localDataResetPopup != null)
            _localDataResetPopup.SetActive(false);
        LocalDataResetService.ExitWithoutSaving();
    }

    private void ShowLocalDataResetConfirmation(int step)
    {
        if (_localDataResetPopup == null)
            return;

        _localDataResetConfirmationStep =
            Mathf.Clamp(step, 1, 2);
        bool isFinal = _localDataResetConfirmationStep == 2;

        SetLocalizedTextKey(
            _localDataResetTitleText,
            isFinal
                ? LocalizationKeys
                    .UiSettingsLocalDataConfirmFinalTitle
                : LocalizationKeys
                    .UiSettingsLocalDataConfirmFirstTitle);
        SetLocalizedTextKey(
            _localDataResetMessageText,
            isFinal
                ? LocalizationKeys
                    .UiSettingsLocalDataConfirmFinalMessage
                : LocalizationKeys
                    .UiSettingsLocalDataConfirmFirstMessage);
        SetLocalizedTextKey(
            _localDataResetConfirmText,
            isFinal
                ? LocalizationKeys.UiSettingsLocalDataDeleteAll
                : LocalizationKeys.UiSettingsLocalDataContinue);

        if (_localDataResetConfirmButton != null &&
            _localDataResetConfirmButton.targetGraphic != null)
        {
            _localDataResetConfirmButton.targetGraphic.color =
                isFinal
                    ? DestructiveButtonColor
                    : ContinueButtonColor;
        }

        _localDataResetPopup.SetActive(true);
        _localDataResetPopup.transform.SetAsLastSibling();
    }

    private void HideLocalDataResetConfirmation()
    {
        _localDataResetConfirmationStep = 0;
        if (_localDataResetPopup != null)
            _localDataResetPopup.SetActive(false);
    }

    private void RefreshQuitButtonAvailability()
    {
        if (quitButton != null)
            quitButton.gameObject.SetActive(!_openedFromDungeon);
        if (_openedFromDungeon)
            HideQuitConfirmation();
    }

    private void SelectTab(int index)
    {
        _selectedTabIndex = Mathf.Clamp(index, 0, 3);

        displayTab.SetActive(_selectedTabIndex == 0);
        soundTab.SetActive(_selectedTabIndex == 1);
        gameTab.SetActive(_selectedTabIndex == 2);
        miscTab.SetActive(_selectedTabIndex == 3);

        SetTabButtonColor(displayTabButton, _selectedTabIndex == 0);
        SetTabButtonColor(soundTabButton, _selectedTabIndex == 1);
        SetTabButtonColor(gameTabButton, _selectedTabIndex == 2);
        SetTabButtonColor(miscTabButton, _selectedTabIndex == 3);
    }

    private void ResolveSettingManagers()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<GameManager>();

        _gameEvents = manager != null ? manager.Events : null;
        _dataManager = manager != null ? manager.Data : null;
    }

    private void BuildSupportedResolutionList()
    {
        _supportedResolutions.Clear();
        HashSet<string> uniqueResolutions = new();

        foreach (Resolution resolution in Screen.resolutions)
        {
            string value = $"{resolution.width} x {resolution.height}";
            if (uniqueResolutions.Add(value))
                _supportedResolutions.Add(value);
        }

        string current = DisplayData.GetCurrentResolution();
        if (uniqueResolutions.Add(current))
            _supportedResolutions.Add(current);

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(_supportedResolutions);
        RebuildLocalizedDisplayOptions();
    }

    private void RebuildLocalizedDisplayOptions()
    {
        int displayModeIndex = displayModeDropdown != null
            ? displayModeDropdown.value
            : 0;
        int frameRateIndex = frameRateDropdown != null
            ? frameRateDropdown.value
            : 0;

        List<string> displayModeLabels = new(
            DisplayModeLocalizationKeys.Length);
        for (int index = 0;
             index < DisplayModeLocalizationKeys.Length;
             index++)
        {
            displayModeLabels.Add(LocalizationService.Get(
                DisplayModeLocalizationKeys[index]));
        }

        List<string> frameRateLabels = new(FrameRateValues.Length);
        for (int index = 0; index < FrameRateValues.Length; index++)
        {
            int frameRate = FrameRateValues[index];
            frameRateLabels.Add(frameRate <= 0
                ? LocalizationService.Get(
                    LocalizationKeys.UiSettingsFpsUnlimited)
                : $"{frameRate} FPS");
        }

        ReplaceDropdownOptions(
            displayModeDropdown,
            displayModeLabels,
            displayModeIndex);
        ReplaceDropdownOptions(
            frameRateDropdown,
            frameRateLabels,
            frameRateIndex);
    }

    private static void ReplaceDropdownOptions(
        TMP_Dropdown dropdown,
        List<string> labels,
        int stableIndex)
    {
        if (dropdown == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(labels);
        if (labels.Count > 0)
        {
            dropdown.SetValueWithoutNotify(Mathf.Clamp(
                stableIndex,
                0,
                labels.Count - 1));
        }

        dropdown.RefreshShownValue();
    }

    private void BuildLocaleOptionList()
    {
        _supportedLocaleIds.Clear();
        List<string> localeLabels = new();
        IReadOnlyList<LocalizationLocaleInfo> locales =
            LocalizationService.SupportedLocales;
        for (int index = 0; index < locales.Count; index++)
        {
            LocalizationLocaleInfo locale = locales[index];
            if (string.IsNullOrWhiteSpace(locale.Locale))
                continue;

            _supportedLocaleIds.Add(locale.Locale);
            localeLabels.Add(string.IsNullOrWhiteSpace(locale.DisplayName)
                ? locale.Locale
                : locale.DisplayName);
        }

        localeDropdown?.ClearOptions();
        localeDropdown?.AddOptions(localeLabels);
        RefreshLocale(LocalizationService.CurrentLocale);
    }

    private void RefreshSettingsControls()
    {
        ResolveSettingManagers();
        BuildSupportedResolutionList();

        DisplayData display = _dataManager != null ? _dataManager.DisplayDatas : null;
        AudioData audio = _dataManager != null ? _dataManager.AudioDatas : null;

        _isRefreshingControls = true;
        BuildLocaleOptionList();
        RefreshResolution(display != null ? display.resolution : DisplayData.GetCurrentResolution());
        RefreshDisplayMode(display != null ? display.displayMode : 1);
        RefreshFrameRate(display != null ? display.fps : DisplayData.DefaultFpsMode);
        RefreshBrightness(display != null ? display.brightness : 100);
        RefreshAudioVolume(EAudioChannel.Master, audio != null ? audio.master : 100);
        RefreshAudioVolume(EAudioChannel.Music, audio != null ? audio.music : 100);
        RefreshAudioVolume(EAudioChannel.SFX, audio != null ? audio.sfx : 100);
        RefreshAudioVolume(EAudioChannel.UI, audio != null ? audio.ui : 100);
        RefreshMuteInBackground(audio != null && audio.mute_in_bg);
        RefreshLocale(LocalizationService.CurrentLocale);
        RefreshLocalizationPresentation();
        _isRefreshingControls = false;
    }

    private void RefreshResolution(string value)
    {
        if (resolutionDropdown == null)
            return;

        string normalized = string.IsNullOrWhiteSpace(value)
            ? DisplayData.GetCurrentResolution()
            : value;
        int index = _supportedResolutions.IndexOf(normalized);
        if (index < 0)
        {
            _supportedResolutions.Add(normalized);
            resolutionDropdown.AddOptions(new List<string> { normalized });
            index = _supportedResolutions.Count - 1;
        }

        resolutionDropdown.SetValueWithoutNotify(index);
        resolutionDropdown.RefreshShownValue();
    }

    private void RefreshDisplayMode(int mode)
    {
        int index = Mathf.Clamp(
            mode,
            0,
            DisplayModeLocalizationKeys.Length - 1);
        displayModeDropdown?.SetValueWithoutNotify(index);
        displayModeDropdown?.RefreshShownValue();
    }

    private void RefreshFrameRate(int mode)
    {
        int index = Mathf.Clamp(mode, 0, FrameRateValues.Length - 1);
        frameRateDropdown?.SetValueWithoutNotify(index);
        frameRateDropdown?.RefreshShownValue();
    }

    private void RefreshBrightness(int value)
    {
        value = Mathf.Clamp(value, 0, 100);
        brightnessSlider?.SetValueWithoutNotify(value);

        if (brightnessValueText != null)
            brightnessValueText.text = $"{value}%";
    }

    private void RequestAudioVolume(EAudioChannel channel, float value)
    {
        if (_isRefreshingControls)
            return;

        int volume = Mathf.RoundToInt(value);
        if (_gameEvents != null)
            _gameEvents.RequestAudioVolumeChange(channel, volume);
        else
            _dataManager?.SetAudioVolume(channel, volume);
    }

    private void RefreshAudioVolume(EAudioChannel channel, int value)
    {
        value = Mathf.Clamp(value, 0, 100);
        Slider slider = null;
        TextMeshProUGUI valueText = null;

        switch (channel)
        {
            case EAudioChannel.Master:
                slider = masterVolumeSlider;
                valueText = masterVolumeValueText;
                break;
            case EAudioChannel.Music:
                slider = musicVolumeSlider;
                valueText = musicVolumeValueText;
                break;
            case EAudioChannel.SFX:
                slider = sfxVolumeSlider;
                valueText = sfxVolumeValueText;
                break;
            case EAudioChannel.UI:
                slider = uiVolumeSlider;
                valueText = uiVolumeValueText;
                break;
        }

        slider?.SetValueWithoutNotify(value);
        if (valueText != null)
            valueText.text = $"{value}%";
    }

    private void RefreshMuteInBackground(bool value)
    {
        if (muteInBackgroundToggle == null)
            return;

        bool wasRefreshing = _isRefreshingControls;
        _isRefreshingControls = true;
        muteInBackgroundToggle.SetValue(value);
        _isRefreshingControls = wasRefreshing;
    }

    private void RefreshLocale(string locale)
    {
        if (localeDropdown == null || _supportedLocaleIds.Count == 0)
            return;

        int index = IndexOfStableId(_supportedLocaleIds, locale);
        localeDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
        localeDropdown.RefreshShownValue();
    }

    private void EnsureLocalizationControls()
    {
        if (gameTab == null || resolutionDropdown == null)
            return;

        HideGameTabPlaceholder();
        RemoveLegacyFontControls();
        localeDropdown = ResolveOrCloneDropdown(
            localeDropdown,
            "drdLocale",
            new Vector2(0.38f, 0.64f),
            new Vector2(0.9f, 0.64f));

        _localeLabelText = EnsureControlLabel(
            "txtLocaleLabel",
            LocalizationKeys.UiSettingsLanguage,
            new Vector2(0.08f, 0.64f),
            new Vector2(0.34f, 0.64f));

        RefreshLocalizationPresentation();
    }

    private void RemoveLegacyFontControls()
    {
        RemoveRuntimeControl("drdFont");
        RemoveRuntimeControl("txtFontLabel");
    }

    private void RemoveRuntimeControl(string objectName)
    {
        Transform legacyControl = gameTab.transform.Find(objectName);
        if (legacyControl == null)
            return;

        legacyControl.gameObject.SetActive(false);
        if (Application.isPlaying)
            Destroy(legacyControl.gameObject);
        else
            DestroyImmediate(legacyControl.gameObject);
    }

    private void EnsureLocalDataResetControls()
    {
        if (miscTab == null || quitButton == null ||
            quitConfirmationPopup == null)
        {
            return;
        }

        TextMeshProUGUI miscHeading =
            FindDescendantByName(
                miscTab.transform,
                "txtMiscPlaceholder")
            ?.GetComponent<TextMeshProUGUI>();
        if (miscHeading == null)
            return;

        RectTransform headingRect = miscHeading.rectTransform;
        headingRect.anchorMin = new Vector2(0.06f, 0.78f);
        headingRect.anchorMax = new Vector2(0.94f, 0.94f);
        headingRect.anchoredPosition = Vector2.zero;
        headingRect.sizeDelta = Vector2.zero;
        miscHeading.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject cardObject = new(
            "grpLocalDataManagement",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        cardObject.layer = miscTab.layer;
        cardObject.transform.SetParent(miscTab.transform, false);

        RectTransform cardRect =
            cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.06f, 0.42f);
        cardRect.anchorMax = new Vector2(0.94f, 0.72f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = Vector2.zero;

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = DataManagementCardColor;
        cardImage.raycastTarget = false;

        CreateRuntimeLocalizedText(
            miscHeading,
            cardObject.transform,
            "txtLocalDataTitle",
            LocalizationKeys.UiSettingsLocalDataTitle,
            new Vector2(0.04f, 0.52f),
            new Vector2(0.7f, 0.9f),
            28f,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold);
        TextMeshProUGUI description =
            CreateRuntimeLocalizedText(
                miscHeading,
                cardObject.transform,
                "txtLocalDataDescription",
                LocalizationKeys.UiSettingsLocalDataDescription,
                new Vector2(0.04f, 0.1f),
                new Vector2(0.7f, 0.52f),
                20f,
                TextAlignmentOptions.MidlineLeft,
                FontStyles.Normal);
        description.color = new Color(0.78f, 0.82f, 0.79f, 1f);
        description.enableAutoSizing = true;
        description.fontSizeMin = 16f;
        description.fontSizeMax = 20f;

        _deleteLocalDataButton = Instantiate(
            quitButton,
            cardObject.transform,
            false);
        _deleteLocalDataButton.name = "btnDeleteLocalData";
        _deleteLocalDataButton.onClick.RemoveAllListeners();
        _deleteLocalDataButton.gameObject.SetActive(true);
        if (_deleteLocalDataButton.targetGraphic != null)
        {
            _deleteLocalDataButton.targetGraphic.color =
                DestructiveButtonColor;
        }

        RectTransform deleteButtonRect =
            _deleteLocalDataButton.transform as RectTransform;
        if (deleteButtonRect != null)
        {
            deleteButtonRect.anchorMin = new Vector2(0.84f, 0.5f);
            deleteButtonRect.anchorMax = new Vector2(0.84f, 0.5f);
            deleteButtonRect.pivot = new Vector2(0.5f, 0.5f);
            deleteButtonRect.anchoredPosition = Vector2.zero;
            deleteButtonRect.sizeDelta = new Vector2(260f, 64f);
            deleteButtonRect.localScale = Vector3.one;
        }

        TextMeshProUGUI deleteButtonText =
            _deleteLocalDataButton.GetComponentInChildren<
                TextMeshProUGUI>(true);
        if (deleteButtonText != null)
        {
            deleteButtonText.name = "txtDeleteLocalData";
            SetLocalizedTextKey(
                deleteButtonText,
                LocalizationKeys.UiSettingsLocalDataDelete);
        }

        CreateLocalDataResetPopup();
    }

    private void CreateLocalDataResetPopup()
    {
        _localDataResetPopup = Instantiate(
            quitConfirmationPopup,
            quitConfirmationPopup.transform.parent,
            false);
        _localDataResetPopup.name = "grpLocalDataResetPopup";

        Transform titleTransform = FindDescendantByName(
            _localDataResetPopup.transform,
            "txtQuitConfirmationTitle");
        Transform messageTransform = FindDescendantByName(
            _localDataResetPopup.transform,
            "txtQuitConfirmationMessage");
        Transform confirmTransform = FindDescendantByName(
            _localDataResetPopup.transform,
            "btnQuitOk");
        Transform cancelTransform = FindDescendantByName(
            _localDataResetPopup.transform,
            "btnQuitCancel");

        _localDataResetTitleText =
            titleTransform?.GetComponent<TextMeshProUGUI>();
        _localDataResetMessageText =
            messageTransform?.GetComponent<TextMeshProUGUI>();
        _localDataResetConfirmButton =
            confirmTransform?.GetComponent<Button>();
        _localDataResetCancelButton =
            cancelTransform?.GetComponent<Button>();

        if (titleTransform != null)
            titleTransform.name = "txtLocalDataConfirmationTitle";
        if (messageTransform != null)
            messageTransform.name = "txtLocalDataConfirmationMessage";
        if (confirmTransform != null)
            confirmTransform.name = "btnLocalDataConfirm";
        if (cancelTransform != null)
            cancelTransform.name = "btnLocalDataCancel";

        _localDataResetConfirmButton?.onClick.RemoveAllListeners();
        _localDataResetCancelButton?.onClick.RemoveAllListeners();

        _localDataResetConfirmText =
            _localDataResetConfirmButton
                ?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (_localDataResetConfirmText != null)
            _localDataResetConfirmText.name = "txtLocalDataConfirm";

        TextMeshProUGUI cancelText =
            _localDataResetCancelButton
                ?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (cancelText != null)
        {
            cancelText.name = "txtLocalDataCancel";
            SetLocalizedTextKey(
                cancelText,
                LocalizationKeys.UiCommonCancel);
        }

        if (_localDataResetMessageText != null)
        {
            _localDataResetMessageText.enableAutoSizing = true;
            _localDataResetMessageText.fontSizeMin = 17f;
            _localDataResetMessageText.fontSizeMax = 24f;
        }

        _localDataResetPopup.SetActive(false);
    }

    private static TextMeshProUGUI CreateRuntimeLocalizedText(
        TextMeshProUGUI template,
        Transform parent,
        string objectName,
        string localizationKey,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles fontStyle)
    {
        TextMeshProUGUI text = Instantiate(
            template,
            parent,
            false);
        text.name = objectName;
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;

        SetLocalizedTextKey(text, localizationKey);
        return text;
    }

    private static void SetLocalizedTextKey(
        TextMeshProUGUI text,
        string localizationKey)
    {
        if (text == null)
            return;

        LocalizedText localizedText =
            text.GetComponent<LocalizedText>();
        if (localizedText == null)
            localizedText = text.gameObject.AddComponent<LocalizedText>();
        localizedText.SetKey(localizationKey);
    }

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            if (string.Equals(
                descendants[index].name,
                objectName,
                StringComparison.Ordinal))
            {
                return descendants[index];
            }
        }

        return null;
    }

    private void BindSceneLocalizedTexts()
    {
        BindLocalizedTextHierarchy(transform);
        if (quitConfirmationPopup != null &&
            !quitConfirmationPopup.transform.IsChildOf(transform))
        {
            BindLocalizedTextHierarchy(quitConfirmationPopup.transform);
        }
    }

    private void BindLocalizedTextHierarchy(Transform root)
    {
        if (root == null)
            return;

        TextMeshProUGUI[] texts =
            root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int index = 0; index < texts.Length; index++)
        {
            TextMeshProUGUI text = texts[index];
            string localizationKey = GetSceneLocalizationKey(text);
            if (string.IsNullOrWhiteSpace(localizationKey))
                continue;

            LocalizedText localizedText =
                text.GetComponent<LocalizedText>();
            if (localizedText == null)
                localizedText = text.gameObject.AddComponent<LocalizedText>();
            localizedText.SetKey(localizationKey);
        }
    }

    private string GetSceneLocalizationKey(TextMeshProUGUI text)
    {
        if (text == null)
            return string.Empty;

        if (string.Equals(
            text.name,
            "Item Label",
            StringComparison.Ordinal))
        {
            // TMP_Dropdown clones this template and owns each option label.
            // Adding LocalizedText here would make every cloned entry refresh
            // to the same placeholder when the locale changes.
            return string.Empty;
        }

        return text.name switch
        {
            "txtSettingTitle" => LocalizationKeys.UiSettingsTitle,
            "txtBACK" => LocalizationKeys.UiCommonBack,
            "txtDISPLAY" => LocalizationKeys.UiSettingsTabDisplay,
            "txtSOUND" => LocalizationKeys.UiSettingsTabSound,
            "txtGAME" => LocalizationKeys.UiSettingsTabGame,
            "txtMISC" => LocalizationKeys.UiSettingsTabMisc,
            "txtDisplayPlaceholder" =>
                LocalizationKeys.UiSettingsSectionDisplay,
            "txtSoundPlaceholder" =>
                LocalizationKeys.UiSettingsSectionSound,
            "txtGamePlaceholder" =>
                LocalizationKeys.UiSettingsSectionGame,
            "txtMiscPlaceholder" =>
                LocalizationKeys.UiSettingsSectionMisc,
            "txtQUITGAME" => LocalizationKeys.UiSettingsQuitGame,
            "txtQuitConfirmationTitle" =>
                LocalizationKeys.UiSettingsQuitConfirmTitle,
            "txtQuitConfirmationMessage" =>
                LocalizationKeys.UiSettingsQuitConfirmMessage,
            "txtOK" => LocalizationKeys.UiCommonOk,
            "txtCANCEL" => LocalizationKeys.UiCommonCancel,
            "txtResolutionLabel" =>
                LocalizationKeys.UiSettingsResolution,
            "txtDisplayModeLabel" =>
                LocalizationKeys.UiSettingsDisplayMode,
            "txtFrameRateLabel" =>
                LocalizationKeys.UiSettingsFrameRate,
            "txtBrightnessLabel" =>
                LocalizationKeys.UiSettingsBrightness,
            "txtMasterVolumeLabel" =>
                LocalizationKeys.UiSettingsMasterVolume,
            "txtMusicVolumeLabel" =>
                LocalizationKeys.UiSettingsMusicVolume,
            "txtSfxVolumeLabel" =>
                LocalizationKeys.UiSettingsSfxVolume,
            "txtUiVolumeLabel" =>
                LocalizationKeys.UiSettingsUiVolume,
            "txtMuteInBackgroundLabel" =>
                LocalizationKeys.UiSettingsMuteBackground,
            _ => string.Empty,
        };
    }

    private TMP_Dropdown ResolveOrCloneDropdown(
        TMP_Dropdown current,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        TMP_Dropdown dropdown = current;
        if (dropdown == null)
        {
            Transform existing = gameTab.transform.Find(objectName);
            if (existing != null)
                dropdown = existing.GetComponent<TMP_Dropdown>();
        }

        if (dropdown == null)
        {
            dropdown = Instantiate(
                resolutionDropdown,
                gameTab.transform,
                false);
            dropdown.name = objectName;
            dropdown.onValueChanged.RemoveAllListeners();
        }

        RectTransform rect = dropdown.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 72f);
            rect.localScale = Vector3.one;
        }

        Navigation navigation = dropdown.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        dropdown.navigation = navigation;
        dropdown.gameObject.SetActive(true);
        ApplyDropdownFont(dropdown);
        return dropdown;
    }

    private TextMeshProUGUI EnsureControlLabel(
        string objectName,
        string localizationKey,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform existing = gameTab.transform.Find(objectName);
        TextMeshProUGUI text = existing != null
            ? existing.GetComponent<TextMeshProUGUI>()
            : null;
        if (text == null)
        {
            GameObject labelObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameTab.layer;
            labelObject.transform.SetParent(gameTab.transform, false);
            text = labelObject.GetComponent<TextMeshProUGUI>();

            TMP_Text template = resolutionDropdown.captionText;
            if (template != null)
            {
                text.font = template.font;
                text.fontSize = template.fontSize;
                text.color = template.color;
            }

            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        LocalizedText localizedText = text.GetComponent<LocalizedText>();
        if (localizedText == null)
            localizedText = text.gameObject.AddComponent<LocalizedText>();
        localizedText.SetKey(localizationKey);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 72f);
        rect.localScale = Vector3.one;
        text.gameObject.SetActive(true);
        return text;
    }

    private void RefreshLocalizationPresentation()
    {
        RefreshLocalizedTextHierarchy(transform);
        if (quitConfirmationPopup != null &&
            !quitConfirmationPopup.transform.IsChildOf(transform))
        {
            RefreshLocalizedTextHierarchy(quitConfirmationPopup.transform);
        }

        resolutionDropdown?.RefreshShownValue();
        displayModeDropdown?.RefreshShownValue();
        frameRateDropdown?.RefreshShownValue();
        localeDropdown?.RefreshShownValue();
        ApplyDropdownFont(resolutionDropdown);
        ApplyDropdownFont(displayModeDropdown);
        ApplyDropdownFont(frameRateDropdown);
        ApplyDropdownFont(localeDropdown);
    }

    private static void RefreshLocalizedTextHierarchy(Transform root)
    {
        if (root == null)
            return;

        LocalizedText[] localizedTexts =
            root.GetComponentsInChildren<LocalizedText>(true);
        for (int index = 0; index < localizedTexts.Length; index++)
            localizedTexts[index].Refresh();
    }

    private static void ApplyDropdownFont(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        LocalizationFontResolver.ApplyGameDefault(dropdown.captionText);
        LocalizationFontResolver.ApplyGameDefault(dropdown.itemText);
    }

    private void HideGameTabPlaceholder()
    {
        for (int index = 0; index < gameTab.transform.childCount; index++)
        {
            Transform child = gameTab.transform.GetChild(index);
            if (child.name.IndexOf(
                "placeholder",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static int IndexOfStableId(List<string> values, string value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(
                values[index],
                value,
                StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void SetTabButtonColor(Button button, bool selected)
    {
        if (button != null && button.targetGraphic != null)
        {
            button.targetGraphic.color = selected
                ? SelectedTabColor
                : UnselectedTabColor;
        }
    }
}
