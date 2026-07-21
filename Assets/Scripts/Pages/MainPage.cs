using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainPage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private GameObject codexPage;
    [SerializeField] private GameObject rosterPage;
    [SerializeField] private GameObject shopPage;
    [SerializeField] private GameObject questPage;
    [SerializeField] private GameObject storagePage;
    [SerializeField] private GameObject settingPage;

    protected override string PageTitle => "MAIN";
    protected override string PageDescription =>
        "CHOOSE A DESTINATION";
    protected override Vector2 PanelSize => new(620f, 820f);

    protected override void BuildButtons()
    {
        CreateMenuButton("PLAY", HandlePlayClicked);
        CreateMenuButton("CODEX", () => OpenPage(codexPage));
        CreateMenuButton("ROSTER", () => OpenPage(rosterPage));
        CreateMenuButton("SHOP", () => OpenPage(shopPage));
        CreateMenuButton("QUEST", () => OpenPage(questPage));
        CreateMenuButton("STORAGE", () => OpenPage(storagePage));
        CreateOverlayMenuButton("SETTINGS", HandleSettingsClicked);
    }

    private void HandlePlayClicked()
    {
        NavigateTo(dungeonPage, PageOpenMode.Fresh);
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
