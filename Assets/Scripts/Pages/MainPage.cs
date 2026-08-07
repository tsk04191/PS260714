using System;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainPage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject stageSelectPage;
    [FormerlySerializedAs("codexPage")]
    [SerializeField] private GameObject basePage;
    [SerializeField] private GameObject rosterPage;
    [SerializeField] private GameObject shopPage;
    [FormerlySerializedAs("questPage")]
    [SerializeField] private GameObject recruitPage;
    [SerializeField] private GameObject storagePage;
    [SerializeField] private GameObject settingPage;

    private Image _lobbyCharacterImage;
    private TextMeshProUGUI _lobbyCharacterName;
    private TextMeshProUGUI _lobbyCharacterCaption;
    private Sprite _defaultLobbyCharacterSprite;
    private string _defaultLobbyCharacterName;
    private string _defaultLobbyCharacterCaption;
    private Button _noticeButton;
    private Button _attendanceButton;
    private NotificationDotView _attendanceBadge;
    private MonthlyAttendancePopupView _attendancePopup;
    private LobbyNoticePopupView _noticePopup;
    private AttendanceData _boundAttendanceData;
    private GameEventManager _gameEvents;
    private float _nextAttendanceRefreshTime;

    protected override string PageTitle => "MAIN";
    protected override string PageDescription =>
        "CHOOSE A DESTINATION";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.UiMainTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.UiMainDescription;
    protected override Vector2 PanelSize => new(620f, 820f);

    protected override void BuildButtons()
    {
        CreateLocalizedMenuButton(
            "btnPLAY",
            LocalizationKeys.UiCommonPlay,
            HandlePlayClicked);
        CreateLocalizedMenuButton(
            "btnROSTER",
            LocalizationKeys.UiCommonRoster,
            () => OpenPage(rosterPage));
        CreateLocalizedMenuButton(
            "btnSHOP",
            LocalizationKeys.UiCommonShop,
            () => OpenPage(shopPage));
        CreateLocalizedMenuButton(
            "btnRECRUIT",
            LocalizationKeys.UiCommonRecruit,
            () => OpenPage(recruitPage));
        CreateLocalizedMenuButton(
            "btnBASE",
            LocalizationKeys.UiCommonBase,
            () => OpenPage(basePage));
        CreateLocalizedMenuButton(
            "btnSTORAGE",
            LocalizationKeys.UiCommonStorage,
            () => OpenPage(storagePage));
        CreateLocalizedOverlayMenuButton(
            "btnSETTINGSOverlay",
            LocalizationKeys.UiCommonSettings,
            HandleSettingsClicked);
        _noticeButton = CreateLocalizedTopLeftOverlayMenuButton(
            "btnNOTICEOverlay",
            LocalizationKeys.UiTitleNotice,
            HandleNoticeClicked);
        _attendanceButton = CreateLocalizedTopLeftOverlayMenuButton(
            "btnATTENDANCEOverlay",
            LocalizationKeys.UiMainAttendance,
            HandleAttendanceClicked);
        AlignUtilityButtonAfter(_noticeButton, _attendanceButton);
        _attendanceBadge = NotificationDotView.BuildOrBind(
            _attendanceButton.transform as RectTransform);
        _noticePopup = LobbyNoticePopupView.BuildOrBind(RuntimeRoot);
        _attendancePopup = MonthlyAttendancePopupView.BuildOrBind(
            RuntimeRoot);
        BindLobbyCharacterView();
        RefreshLobbyCharacterView();
    }

    private void OnEnable()
    {
        LobbyRepresentativeSelection.SelectionChanged +=
            HandleLobbyRepresentativeChanged;
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        BindGameEvents(GameManager.Instance?.Events);
        BindAttendanceData(DataManager.Current?.AttendanceDatas);
        _noticePopup?.Hide();
        _attendancePopup?.Hide();
        RefreshLobbyCharacterView();
        RefreshAttendanceAvailability();
    }

    private void OnDisable()
    {
        LobbyRepresentativeSelection.SelectionChanged -=
            HandleLobbyRepresentativeChanged;
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        BindGameEvents(null);
        BindAttendanceData(null);
        _noticePopup?.Hide();
        _attendancePopup?.Hide();
    }

    protected override void OnDestroy()
    {
        LobbyRepresentativeSelection.SelectionChanged -=
            HandleLobbyRepresentativeChanged;
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        BindGameEvents(null);
        BindAttendanceData(null);
        base.OnDestroy();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAttendanceRefreshTime)
            return;

        _nextAttendanceRefreshTime = Time.unscaledTime + 30f;
        RefreshAttendanceAvailability();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            RefreshAttendanceAvailability();
    }

    private void HandlePlayClicked()
    {
        NavigateTo(stageSelectPage, PageOpenMode.Fresh);
    }

    private void OpenPage(GameObject targetPage)
    {
        NavigateTo(targetPage, PageOpenMode.Fresh);
    }

    private void HandleSettingsClicked()
    {
        _noticePopup?.Hide();
        _attendancePopup?.Hide();
        if (settingPage != null &&
            settingPage.TryGetComponent(out SettingPage page))
        {
            page.OpenFrom(gameObject);
            return;
        }

        NavigateTo(settingPage, PageOpenMode.Fresh);
    }

    private void HandleNoticeClicked()
    {
        _attendancePopup?.Hide();
        _noticePopup?.Show();
    }

    private void HandleAttendanceClicked()
    {
        _noticePopup?.Hide();
        AttendanceService service = DataManager.Current?.Attendance;
        _attendancePopup?.Bind(service);
        _attendancePopup?.Show();
        RefreshAttendanceAvailability();
    }

    private void HandleLobbyRepresentativeChanged(
        string unusedCharacterId)
    {
        RefreshLobbyCharacterView();
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshLobbyCharacterView();
        RefreshAttendanceAvailability();
    }

    private void HandleAttendanceChanged()
    {
        RefreshAttendanceAvailability();
    }

    private void HandleDataReady()
    {
        BindAttendanceData(DataManager.Current?.AttendanceDatas);
        _attendancePopup?.Bind(DataManager.Current?.Attendance);
        RefreshAttendanceAvailability();
    }

    private void BindGameEvents(GameEventManager events)
    {
        if (_gameEvents == events)
            return;

        if (_gameEvents != null)
            _gameEvents.DataReady -= HandleDataReady;
        _gameEvents = events;
        if (_gameEvents != null)
            _gameEvents.DataReady += HandleDataReady;
    }

    private void BindAttendanceData(AttendanceData data)
    {
        if (_boundAttendanceData == data)
            return;

        if (_boundAttendanceData != null)
            _boundAttendanceData.Changed -= HandleAttendanceChanged;
        _boundAttendanceData = data;
        if (_boundAttendanceData != null)
            _boundAttendanceData.Changed += HandleAttendanceChanged;
    }

    private void RefreshAttendanceAvailability()
    {
        AttendanceService service = DataManager.Current?.Attendance;
        AttendanceStatus status = service?.RefreshStatus();
        if (_attendanceBadge != null)
        {
            _attendanceBadge.SetVisible(
                status?.Availability ==
                AttendanceAvailability.Claimable);
        }
    }

    private static void AlignUtilityButtonAfter(
        Button leadingButton,
        Button trailingButton)
    {
        if (leadingButton == null || trailingButton == null)
            return;

        RectTransform leading =
            leadingButton.transform as RectTransform;
        RectTransform trailing =
            trailingButton.transform as RectTransform;
        if (leading == null || trailing == null)
            return;

        trailing.anchorMin = leading.anchorMin;
        trailing.anchorMax = leading.anchorMax;
        trailing.pivot = leading.pivot;
        trailing.anchoredPosition = new Vector2(
            leading.anchoredPosition.x + leading.rect.width,
            leading.anchoredPosition.y);
        if (trailing.rect.height <= 0f)
        {
            trailing.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                leading.rect.height);
        }
    }

    private void BindLobbyCharacterView()
    {
        Transform root = transform.Find(RuntimeRootObjectName);
        if (root == null)
            return;

        _lobbyCharacterImage ??= root.Find("imgLobbyCharacter")
            ?.GetComponent<Image>();
        _lobbyCharacterName ??= root
            .Find("grpLobbyIdentity/txtLobbyCharacterName")
            ?.GetComponent<TextMeshProUGUI>();
        _lobbyCharacterCaption ??= root
            .Find("grpLobbyIdentity/txtLobbyCharacterCaption")
            ?.GetComponent<TextMeshProUGUI>();

        if (_defaultLobbyCharacterSprite == null &&
            _lobbyCharacterImage != null)
        {
            _defaultLobbyCharacterSprite =
                _lobbyCharacterImage.sprite;
        }

        if (_defaultLobbyCharacterName == null &&
            _lobbyCharacterName != null)
        {
            _defaultLobbyCharacterName =
                _lobbyCharacterName.text;
        }

        if (_defaultLobbyCharacterCaption == null &&
            _lobbyCharacterCaption != null)
        {
            _defaultLobbyCharacterCaption =
                _lobbyCharacterCaption.text;
        }
    }

    private void RefreshLobbyCharacterView()
    {
        BindLobbyCharacterView();
        if (_lobbyCharacterImage == null)
            return;

        CharacterData selected = ResolveSelectedOwnedCharacter();
        if (selected == null)
        {
            _lobbyCharacterImage.sprite =
                _defaultLobbyCharacterSprite;
            if (_lobbyCharacterName != null)
            {
                _lobbyCharacterName.text =
                    _defaultLobbyCharacterName ?? string.Empty;
            }

            if (_lobbyCharacterCaption != null)
            {
                _lobbyCharacterCaption.text =
                    _defaultLobbyCharacterCaption ?? string.Empty;
            }

            return;
        }

        Sprite standing = selected.StandingSprite != null
            ? selected.StandingSprite
            : selected.IconSprite;
        _lobbyCharacterImage.sprite =
            standing != null
                ? standing
                : _defaultLobbyCharacterSprite;
        if (_lobbyCharacterName != null)
        {
            _lobbyCharacterName.text =
                CharacterLocalization.GetName(selected);
        }

        if (_lobbyCharacterCaption != null)
        {
            bool korean =
                LocalizationService.CurrentLocale?.StartsWith(
                    "ko",
                    StringComparison.OrdinalIgnoreCase) == true;
            _lobbyCharacterCaption.text = korean
                ? $"대표 대원 // {selected.CharacterId}"
                : $"LOBBY OPERATOR // {selected.CharacterId}";
        }
    }

    private static CharacterData ResolveSelectedOwnedCharacter()
    {
        string selectedId =
            LobbyRepresentativeSelection.SelectedCharacterId;
        if (string.IsNullOrWhiteSpace(selectedId))
            return null;

        CharacterCollectionData collection =
            DataManager.Current?.CharacterDatas;
        foreach (CharacterSO definition in
                 CharacterDefinitionCatalog.GetAll())
        {
            if (definition == null ||
                !string.Equals(
                    definition.CharacterId,
                    selectedId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            CharacterData data = collection != null
                ? collection.CreatePreviewData(definition)
                : definition.CreateData(new CharacterProgressData(
                    definition.CharacterId,
                    definition.InitiallyOwned));
            return data != null && data.IsOwned
                ? data
                : null;
        }

        return null;
    }
}

public static class LobbyRepresentativeSelection
{
    private const string PlayerPrefsKey =
        "Lobby.RepresentativeCharacterId";

    public static event Action<string> SelectionChanged;

    public static string SelectedCharacterId
    {
        get
        {
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return PlayerPrefs.GetString(
                    PlayerPrefsKey,
                    string.Empty);
            }

            return GetDefaultCharacterId();
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        SelectionChanged = null;
    }

    public static bool IsSelected(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId) &&
               string.Equals(
                   SelectedCharacterId,
                   characterId,
                   StringComparison.Ordinal);
    }

    public static void SetSelected(
        string characterId,
        bool selected)
    {
        string normalized = (characterId ?? string.Empty).Trim();
        if (selected && string.IsNullOrWhiteSpace(normalized))
            return;

        string previous = SelectedCharacterId;
        string defaultCharacterId = GetDefaultCharacterId();
        string next = selected
            ? normalized
            : string.Equals(
                previous,
                normalized,
                StringComparison.Ordinal)
                ? defaultCharacterId
                : previous;
        if (string.Equals(
                previous,
                next,
                StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(
                next,
                defaultCharacterId,
                StringComparison.Ordinal))
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }
        else
        {
            PlayerPrefs.SetString(PlayerPrefsKey, next);
        }
        PlayerPrefs.Save();
        SelectionChanged?.Invoke(next);
    }

    private static string GetDefaultCharacterId()
    {
        CharacterSO defaultRepresentative =
            GameManager.Instance?.DefaultLobbyRepresentative;
        return defaultRepresentative != null
            ? defaultRepresentative.CharacterId
            : string.Empty;
    }
}
