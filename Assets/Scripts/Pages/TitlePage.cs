using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitlePage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject settingPage;

    protected override string PageTitle => "TITLE";
    protected override string PageDescription => "PS260714";

    protected override void BuildButtons()
    {
        CreateMenuButton("START", HandleStartClicked);
        CreateMenuButton("SETTINGS", HandleSettingsClicked);
        CreateMenuButton("QUIT", HandleQuitClicked);
    }

    private void HandleStartClicked()
    {
        NavigateTo(mainPage);
    }

    private void HandleSettingsClicked()
    {
        if (settingPage != null &&
            settingPage.TryGetComponent(out SettingPage settings))
        {
            settings.OpenFrom(gameObject);
            return;
        }

        NavigateTo(settingPage);
    }

    private static void HandleQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
