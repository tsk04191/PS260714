using UnityEngine;
using PS260714.Localization;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameEventManager Events { get; private set; }
    public BattleManager Battle => battleManager;

    [SerializeField] private BattleManager battleManager;
    public DataManager Data;
    public AudioManager Audio;

    [Header("Main Lobby")]
    [SerializeField] private CharacterSO defaultLobbyRepresentative;

    public CharacterSO DefaultLobbyRepresentative =>
        IsEligibleDefaultLobbyRepresentative(
            defaultLobbyRepresentative)
            ? defaultLobbyRepresentative
            : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Application.runInBackground = true;
        Events = new GameEventManager();

        if (battleManager == null || !battleManager.Setup(this))
            Debug.LogError("GameManager requires a configured BattleManager.", this);

        Audio?.Setup(this);
        SubscribeEvents();

        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        UnsubscribeEvents();
        if (battleManager != null)
            battleManager.Teardown();

        if (Audio != null)
            Audio.Teardown();

        Instance = null;
    }

    private void OnValidate()
    {
        if (defaultLobbyRepresentative != null &&
            !IsEligibleDefaultLobbyRepresentative(
                defaultLobbyRepresentative))
        {
            Debug.LogWarning(
                "The default lobby representative must be a " +
                "CharacterSO marked as initially owned.",
                this);
        }
    }

    public static bool IsEligibleDefaultLobbyRepresentative(
        CharacterSO definition)
    {
        return definition != null && definition.InitiallyOwned;
    }

    private void SubscribeEvents()
    {
        Events.DisplayModeChangeRequested += ApplyDisplayMode;
        Events.ResolutionChangeRequested += ApplyResolution;
        Events.LocaleChangeRequested += ApplyLocale;
        Events.FontChangeRequested += ApplyFont;
        LocalizationService.LocaleChanged += NotifyLocaleChanged;
        LocalizationService.FontChanged += NotifyFontChanged;

        Events.NotifyLocaleChanged(LocalizationService.CurrentLocale);
        Events.NotifyFontChanged(LocalizationService.CurrentFontId);
    }

    private void UnsubscribeEvents()
    {
        if (Events == null)
            return;

        Events.DisplayModeChangeRequested -= ApplyDisplayMode;
        Events.ResolutionChangeRequested -= ApplyResolution;
        Events.LocaleChangeRequested -= ApplyLocale;
        Events.FontChangeRequested -= ApplyFont;
        LocalizationService.LocaleChanged -= NotifyLocaleChanged;
        LocalizationService.FontChanged -= NotifyFontChanged;
    }

    private static void ApplyLocale(string locale)
    {
        LocalizationService.SetLocale(locale);
    }

    private static void ApplyFont(string fontId)
    {
        LocalizationService.SetFont(fontId);
    }

    private void NotifyLocaleChanged(string locale)
    {
        Events?.NotifyLocaleChanged(locale);
    }

    private void NotifyFontChanged(string fontId)
    {
        Events?.NotifyFontChanged(fontId);
    }

    private void ApplyDisplayMode(int mode)
    {
        switch (mode)
        {
            case 0:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 1:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 2:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }
    }

    private void ApplyResolution(string resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution))
            return;

        string[] resolutionParts = resolution.Split('x', 'X');
        if (resolutionParts.Length != 2)
            return;

        if (!int.TryParse(resolutionParts[0].Trim(), out int width) ||
            !int.TryParse(resolutionParts[1].Trim(), out int height) ||
            width <= 0 ||
            height <= 0)
        {
            return;
        }

        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }
}
