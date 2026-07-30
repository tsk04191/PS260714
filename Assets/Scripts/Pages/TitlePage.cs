using PS260714.Localization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitlePage : RuntimeMenuPageBase
{
    private const string NoticePopupPath =
        RuntimeRootObjectName + "/grpNoticePopup";

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject settingPage;

    private Button _startButton;
    private Button _noticeCloseButton;
    private GameObject _noticePopup;
    private bool _startRequested;

    protected override string PageTitle => "TITLE";
    protected override string PageDescription => "PS260714";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.UiTitleTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.UiTitleDescription;

    protected override void BuildButtons()
    {
        _startButton = CreateLocalizedMenuButton(
            "btnSTARTFullscreen",
            LocalizationKeys.UiTitleClickToStart,
            HandleStartClicked);
        CreateLocalizedTopLeftOverlayMenuButton(
            "btnNOTICEOverlay",
            LocalizationKeys.UiTitleNotice,
            HandleNoticeClicked);
        CreateLocalizedOverlayMenuButton(
            "btnSETTINGSOverlay",
            LocalizationKeys.UiCommonSettings,
            HandleSettingsClicked);

        HideLegacyButton("btnSTART");
        HideLegacyButton("btnSETTINGS");
        HideLegacyButton("btnQUIT");
        BindNoticePopup();
    }

    protected override void OnDestroy()
    {
        if (_noticeCloseButton != null)
            _noticeCloseButton.onClick.RemoveListener(HideNotice);
        base.OnDestroy();
    }

    private void OnEnable()
    {
        _startRequested = false;
        if (_startButton != null)
            _startButton.interactable = true;
        HideNotice();
    }

    private void HandleStartClicked()
    {
        if (_startRequested || mainPage == null)
            return;

        _startRequested = true;
        if (_startButton != null)
            _startButton.interactable = false;
        NavigateTo(mainPage);
    }

    private void HandleSettingsClicked()
    {
        HideNotice();
        if (settingPage != null &&
            settingPage.TryGetComponent(out SettingPage settings))
        {
            settings.OpenFrom(gameObject);
            return;
        }

        NavigateTo(settingPage);
    }

    private void HandleNoticeClicked()
    {
        BindNoticePopup();
        if (_noticePopup == null)
        {
            Debug.LogWarning(
                "Title notice popup is missing from the designer UI.",
                this);
            return;
        }

        _noticePopup.SetActive(true);
        _noticePopup.transform.SetAsLastSibling();
    }

    private void HideNotice()
    {
        if (_noticePopup != null)
            _noticePopup.SetActive(false);
    }

    private void BindNoticePopup()
    {
        _noticePopup ??= transform.Find(NoticePopupPath)?.gameObject;
        if (_noticePopup == null)
            return;

        Button closeButton = _noticePopup.transform.Find(
                "grpNoticePanel/btnNOTICECLOSE")
            ?.GetComponent<Button>();
        closeButton ??= _noticePopup.GetComponent<Button>();
        if (closeButton == null || closeButton == _noticeCloseButton)
            return;

        if (_noticeCloseButton != null)
            _noticeCloseButton.onClick.RemoveListener(HideNotice);
        _noticeCloseButton = closeButton;
        _noticeCloseButton.onClick.RemoveAllListeners();
        _noticeCloseButton.onClick.AddListener(HideNotice);
    }

    private void HideLegacyButton(string objectName)
    {
        Transform legacy = ButtonRoot != null
            ? ButtonRoot.Find(objectName)
            : null;
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }
}
