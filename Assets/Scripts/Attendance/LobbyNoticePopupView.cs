using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyNoticePopupView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI message;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private ResponsivePanelFitter panelFitter;

    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        ResolveSceneReferences();
        if (backdropButton != null)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Hide);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnEnable()
    {
        panelFitter?.RefreshLayout();
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnRectTransformDimensionsChange()
    {
        panelFitter?.RefreshLayout();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    public void ResolveSceneReferences()
    {
        Transform panel = transform.Find("grpMainNoticePanel");
        title ??= panel?.Find("txtMainNoticeTitle")
            ?.GetComponent<TextMeshProUGUI>();
        message ??= panel?.Find("txtMainNoticeMessage")
            ?.GetComponent<TextMeshProUGUI>();
        closeButton ??= panel?.Find("btnMainNoticeClose")
            ?.GetComponent<Button>();
        backdropButton ??= GetComponent<Button>();
        panelFitter ??= panel?.GetComponent<ResponsivePanelFitter>();
        if (title == null || message == null || closeButton == null ||
            backdropButton == null || panelFitter == null)
        {
            Debug.LogError(
                "Lobby notice popup Scene references are incomplete.",
                this);
        }
    }

    private void Refresh()
    {
        if (title != null)
            title.text = LocalizationService.Get(
                LocalizationKeys.UiTitleNotice);
        if (message != null)
            message.text = LocalizationService.Get(
                LocalizationKeys.UiTitleNoticeEmpty);
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        Refresh();
    }
}
