using System.Collections.Generic;
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
    private static readonly string[] DisplayModeLabels =
    {
        "FULLSCREEN",
        "BORDERLESS",
        "WINDOWED",
    };
    private static readonly string[] FrameRateLabels =
    {
        "UNLIMITED",
        "120 FPS",
        "60 FPS",
        "30 FPS",
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

    [Header("Quit Confirmation")]
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject quitConfirmationPopup;
    [SerializeField] private Button quitOkButton;
    [SerializeField] private Button quitCancelButton;

    private bool _initialized;
    private bool _eventsBound;
    private bool _isRefreshingControls;
    private int _selectedTabIndex;
    private GameEventManager _gameEvents;
    private DataManager _dataManager;
    private readonly List<string> _supportedResolutions = new();

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
    }

    private void OnDisable()
    {
        UnbindEvents();
        HideQuitConfirmation();
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
    }

    public void Close()
    {
        SaveSettings();
        UnbindEvents();
        HideQuitConfirmation();
        gameObject.SetActive(false);
    }

    public void Init()
    {
        if (_initialized)
            return;

        if (!ValidateReferences())
            return;

        ResolveSettingManagers();
        BuildSupportedResolutionList();
        _initialized = true;
        _selectedTabIndex = 0;
        SelectTab(_selectedTabIndex);
        RefreshSettingsControls();
        HideQuitConfirmation();

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
        quitButton.onClick.AddListener(HandleQuitClicked);
        quitOkButton.onClick.AddListener(HandleQuitOkClicked);
        quitCancelButton.onClick.AddListener(HideQuitConfirmation);

        if (_gameEvents != null)
        {
            _gameEvents.DataReady += RefreshSettingsControls;
            _gameEvents.DisplayFPSChanged += RefreshFrameRate;
            _gameEvents.DisplayModeChanged += RefreshDisplayMode;
            _gameEvents.ResolutionChanged += RefreshResolution;
            _gameEvents.DisplayBrightnessChanged += RefreshBrightness;
            _gameEvents.AudioVolumeChanged += RefreshAudioVolume;
            _gameEvents.MuteInBackgroundChanged += RefreshMuteInBackground;
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
        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitClicked);
        if (quitOkButton != null)
            quitOkButton.onClick.RemoveListener(HandleQuitOkClicked);
        if (quitCancelButton != null)
            quitCancelButton.onClick.RemoveListener(HideQuitConfirmation);

        if (_gameEvents != null)
        {
            _gameEvents.DataReady -= RefreshSettingsControls;
            _gameEvents.DisplayFPSChanged -= RefreshFrameRate;
            _gameEvents.DisplayModeChanged -= RefreshDisplayMode;
            _gameEvents.ResolutionChanged -= RefreshResolution;
            _gameEvents.DisplayBrightnessChanged -= RefreshBrightness;
            _gameEvents.AudioVolumeChanged -= RefreshAudioVolume;
            _gameEvents.MuteInBackgroundChanged -= RefreshMuteInBackground;
        }

        _eventsBound = false;
    }

    private void HandleBackClicked()
    {
        PageControl.PagToPag(gameObject, dungeonPage, PageOpenMode.Resume);
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

    private void HandleQuitClicked()
    {
        quitConfirmationPopup.SetActive(true);
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
        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string>(DisplayModeLabels));
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(new List<string>(FrameRateLabels));
    }

    private void RefreshSettingsControls()
    {
        ResolveSettingManagers();
        BuildSupportedResolutionList();

        DisplayData display = _dataManager != null ? _dataManager.DisplayDatas : null;
        AudioData audio = _dataManager != null ? _dataManager.AudioDatas : null;

        _isRefreshingControls = true;
        RefreshResolution(display != null ? display.resolution : DisplayData.GetCurrentResolution());
        RefreshDisplayMode(display != null ? display.displayMode : 1);
        RefreshFrameRate(display != null ? display.fps : DisplayData.DefaultFpsMode);
        RefreshBrightness(display != null ? display.brightness : 100);
        RefreshAudioVolume(EAudioChannel.Master, audio != null ? audio.master : 100);
        RefreshAudioVolume(EAudioChannel.Music, audio != null ? audio.music : 100);
        RefreshAudioVolume(EAudioChannel.SFX, audio != null ? audio.sfx : 100);
        RefreshAudioVolume(EAudioChannel.UI, audio != null ? audio.ui : 100);
        RefreshMuteInBackground(audio != null && audio.mute_in_bg);
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
        int index = Mathf.Clamp(mode, 0, DisplayModeLabels.Length - 1);
        displayModeDropdown?.SetValueWithoutNotify(index);
        displayModeDropdown?.RefreshShownValue();
    }

    private void RefreshFrameRate(int mode)
    {
        int index = Mathf.Clamp(mode, 0, FrameRateLabels.Length - 1);
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
