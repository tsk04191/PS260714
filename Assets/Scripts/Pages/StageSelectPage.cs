using PS260714.Localization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StageSelectPage : RuntimeMenuPageBase
{
    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject dungeonPage;

    protected override string PageTitle => "DUNGEON STAGE";
    protected override string PageDescription => "SELECT A STAGE";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.UiStageSelectTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.UiStageSelectDescription;
    protected override Vector2 PanelSize => new(620f, 560f);

    protected override void BuildButtons()
    {
        Button testFieldButton = CreateLocalizedMenuButton(
            "btnSTAGE0TESTFIELD",
            LocalizationKeys.UiStageSelectTestField,
            HandleTestFieldClicked);
        if (testFieldButton != null)
            testFieldButton.interactable = true;

        CreateLocalizedMenuButton(
            "btnFREEBATTLE",
            LocalizationKeys.UiStageSelectFreeBattle,
            HandleFreeBattleClicked);
        CreateLocalizedMenuButton(
            "btnBACK",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
    }

    private void HandleTestFieldClicked()
    {
        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            dungeon.PrepareDungeon(
                DungeonDefinitionCatalog.Get(
                    DungeonDefinitionCatalog.TestFieldId));
        }

        NavigateTo(dungeonPage, PageOpenMode.Fresh);
    }

    private void HandleFreeBattleClicked()
    {
        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            dungeon.PrepareDungeon(
                DungeonDefinitionCatalog.Get(
                    DungeonDefinitionCatalog.FreeBattleId));
        }

        NavigateTo(dungeonPage, PageOpenMode.Fresh);
    }

    private void HandleBackClicked()
    {
        NavigateTo(mainPage, PageOpenMode.Resume);
    }
}
