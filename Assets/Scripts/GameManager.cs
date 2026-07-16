using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameEventManager Events { get; private set; }
    public BattleManager Battle => battleManager;

    [SerializeField] private BattleManager battleManager;
    public DataManager Data;
    public AudioManager Audio;

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

    private void SubscribeEvents()
    {
        Events.DisplayModeChangeRequested += ApplyDisplayMode;
        Events.ResolutionChangeRequested += ApplyResolution;
    }

    private void UnsubscribeEvents()
    {
        if (Events == null)
            return;

        Events.DisplayModeChangeRequested -= ApplyDisplayMode;
        Events.ResolutionChangeRequested -= ApplyResolution;
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
