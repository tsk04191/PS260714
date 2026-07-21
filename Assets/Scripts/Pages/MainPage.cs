using PS260714.Localization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainPage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject stageSelectPage;
    [SerializeField] private GameObject codexPage;
    [SerializeField] private GameObject rosterPage;
    [SerializeField] private GameObject shopPage;
    [SerializeField] private GameObject questPage;
    [SerializeField] private GameObject storagePage;
    [SerializeField] private GameObject settingPage;

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
            "btnCODEX",
            LocalizationKeys.UiCommonCodex,
            () => OpenPage(codexPage));
        CreateLocalizedMenuButton(
            "btnROSTER",
            LocalizationKeys.UiCommonRoster,
            () => OpenPage(rosterPage));
        CreateLocalizedMenuButton(
            "btnSHOP",
            LocalizationKeys.UiCommonShop,
            () => OpenPage(shopPage));
        CreateLocalizedMenuButton(
            "btnQUEST",
            LocalizationKeys.UiCommonQuest,
            () => OpenPage(questPage));
        CreateLocalizedMenuButton(
            "btnSTORAGE",
            LocalizationKeys.UiCommonStorage,
            () => OpenPage(storagePage));
        CreateLocalizedOverlayMenuButton(
            "btnSETTINGSOverlay",
            LocalizationKeys.UiCommonSettings,
            HandleSettingsClicked);
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
        if (settingPage != null &&
            settingPage.TryGetComponent(out SettingPage page))
        {
            page.OpenFrom(gameObject);
            return;
        }

        NavigateTo(settingPage, PageOpenMode.Fresh);
    }
}
