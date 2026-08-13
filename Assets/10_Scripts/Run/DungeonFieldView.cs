using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonFieldView : MonoBehaviour
{
    [SerializeField] private DungeonBoardView board;
    [SerializeField] private DungeonFlowController flowController;
    [SerializeField] private DungeonBattleTab battleTab;
    [SerializeField] private CharacterRuntime[] playerCharacters =
        new CharacterRuntime[DungeonPage.MaximumPartySize];

    [Header("Optional Theme Bindings")]
    [SerializeField] private Image background;
    [SerializeField] private Image fieldFrame;
    [SerializeField] private Transform environmentRoot;

    private GameObject _environmentInstance;
    private Sprite _defaultBackgroundSprite;
    private Color _defaultBackgroundColor;
    private Color _defaultFieldFrameColor;
    private bool _themeDefaultsCaptured;

    public DungeonBoardView Board => board;
    public DungeonFlowController FlowController => flowController;
    public DungeonBattleTab BattleTab => battleTab;
    public CharacterRuntime[] PlayerCharacters => playerCharacters;

    public bool IsConfigured => board != null && flowController != null &&
                                battleTab != null;

    private void Awake()
    {
        CaptureThemeDefaults();
    }

    public void BindSceneStructure(
        DungeonBoardView boardView,
        DungeonFlowController flow,
        DungeonBattleTab battle,
        CharacterRuntime[] characters)
    {
        board = boardView;
        flowController = flow;
        battleTab = battle;
        playerCharacters = characters ??
            new CharacterRuntime[DungeonPage.MaximumPartySize];
    }

    public void ApplyTheme(DungeonThemeDefinition theme)
    {
        CaptureThemeDefaults();
        ResetTheme();

        if (theme == null)
            return;

        if (background != null)
        {
            background.color = theme.BackgroundColor;
            if (theme.BackgroundSprite != null)
                background.sprite = theme.BackgroundSprite;
        }
        if (fieldFrame != null)
            fieldFrame.color = theme.FieldFrameColor;

        if (environmentRoot != null && theme.EnvironmentPrefab != null)
        {
            _environmentInstance = Instantiate(
                theme.EnvironmentPrefab,
                environmentRoot,
                false);
        }
    }

    private void CaptureThemeDefaults()
    {
        if (_themeDefaultsCaptured)
            return;

        if (background != null)
        {
            _defaultBackgroundSprite = background.sprite;
            _defaultBackgroundColor = background.color;
        }
        if (fieldFrame != null)
            _defaultFieldFrameColor = fieldFrame.color;

        _themeDefaultsCaptured = true;
    }

    private void ResetTheme()
    {
        if (background != null)
        {
            background.sprite = _defaultBackgroundSprite;
            background.color = _defaultBackgroundColor;
        }
        if (fieldFrame != null)
            fieldFrame.color = _defaultFieldFrameColor;

        if (_environmentInstance != null)
            Destroy(_environmentInstance);
        _environmentInstance = null;
    }

    public RectTransform GetHighlightTarget(
        EDungeonTutorialTarget target,
        DungeonPage page,
        RectTransform startingChoice)
    {
        switch (target)
        {
            case EDungeonTutorialTarget.StartingChoice:
                return startingChoice;
            case EDungeonTutorialTarget.Field:
                return board != null ? board.HighlightRect : null;
            case EDungeonTutorialTarget.Queue:
                return battleTab != null
                    ? battleTab.QueueHighlightRect
                    : null;
            case EDungeonTutorialTarget.Character:
                if (page != null && page.OwnedTurrets.Count > 0 &&
                    page.OwnedTurrets[0] != null)
                {
                    return page.OwnedTurrets[0].transform as RectTransform;
                }
                return null;
            case EDungeonTutorialTarget.Item:
                return battleTab != null
                    ? battleTab.ItemHighlightRect
                    : null;
            case EDungeonTutorialTarget.Timer:
                return battleTab != null
                    ? battleTab.TimerHighlightRect
                    : null;
            default:
                return null;
        }
    }
}
