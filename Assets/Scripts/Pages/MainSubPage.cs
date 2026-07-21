using PS260714.Localization;
using UnityEngine;
using UnityEngine.UI;

public enum EMainSubPageType
{
    Codex,
    Roster,
    Shop,
    Quest,
    Storage
}

[DisallowMultipleComponent]
public sealed class MainSubPage : RuntimeMenuPageBase
{
    [SerializeField] private EMainSubPageType pageType;

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject enemyCodexPage;
    [SerializeField] private GameObject characterCodexPage;
    [SerializeField] private GameObject skillCodexPage;
    [SerializeField] private GameObject itemCodexPage;

    protected override string PageTitle => pageType switch
    {
        EMainSubPageType.Codex => "CODEX",
        EMainSubPageType.Roster => "ROSTER",
        EMainSubPageType.Shop => "SHOP",
        EMainSubPageType.Quest => "QUEST",
        EMainSubPageType.Storage => "STORAGE",
        _ => "PAGE"
    };

    protected override string PageDescription => pageType switch
    {
        EMainSubPageType.Codex =>
            "ENEMIES | CHARACTERS | SKILLS | ITEMS | EVENTS",
        EMainSubPageType.Roster => "OWNED CHARACTERS",
        EMainSubPageType.Shop => "DUNGEON CLEAR CURRENCY SHOP",
        EMainSubPageType.Quest => "QUEST PROGRESS",
        EMainSubPageType.Storage =>
            "RESOURCES | CONSUMABLE ITEMS | TICKETS",
        _ => string.Empty
    };

    protected override string PageTitleLocalizationKey => pageType switch
    {
        EMainSubPageType.Codex => LocalizationKeys.UiCodexTitle,
        EMainSubPageType.Roster => LocalizationKeys.UiRosterTitle,
        EMainSubPageType.Shop => LocalizationKeys.UiShopTitle,
        EMainSubPageType.Quest => LocalizationKeys.UiQuestTitle,
        EMainSubPageType.Storage => LocalizationKeys.UiStorageTitle,
        _ => string.Empty
    };

    protected override string PageDescriptionLocalizationKey => pageType switch
    {
        EMainSubPageType.Codex => LocalizationKeys.UiCodexDescription,
        EMainSubPageType.Roster => LocalizationKeys.UiRosterDescription,
        EMainSubPageType.Shop => LocalizationKeys.UiShopDescription,
        EMainSubPageType.Quest => LocalizationKeys.UiQuestDescription,
        EMainSubPageType.Storage => LocalizationKeys.UiStorageDescription,
        _ => string.Empty
    };

    protected override Vector2 PanelSize => pageType == EMainSubPageType.Codex
        ? new Vector2(680f, 840f)
        : new Vector2(680f, 720f);

    protected override void BuildButtons()
    {
        switch (pageType)
        {
            case EMainSubPageType.Codex:
                CreateLocalizedMenuButton(
                    "btnENEMIES",
                    LocalizationKeys.UiCommonEnemies,
                    () => NavigateTo(
                        enemyCodexPage,
                        PageOpenMode.Fresh));
                CreateLocalizedMenuButton(
                    "btnCHARACTERS",
                    LocalizationKeys.UiCommonCharacters,
                    () => NavigateTo(
                        characterCodexPage,
                        PageOpenMode.Fresh));
                CreateLocalizedMenuButton(
                    "btnSKILLS",
                    LocalizationKeys.UiCommonSkills,
                    () => NavigateTo(
                        skillCodexPage,
                        PageOpenMode.Fresh));
                CreateLocalizedMenuButton(
                    "btnITEMS",
                    LocalizationKeys.UiCommonItems,
                    () => NavigateTo(
                        itemCodexPage,
                        PageOpenMode.Fresh));
                CreateLocalizedPlaceholderButton(
                    "btnEVENTS",
                    LocalizationKeys.UiCommonEvents);
                break;
            case EMainSubPageType.Roster:
                CreateLocalizedPlaceholderButton(
                    "btnOWNEDCHARACTERS-EMPTY",
                    LocalizationKeys.UiRosterEmpty);
                break;
            case EMainSubPageType.Shop:
                CreateLocalizedPlaceholderButton(
                    "btnDUNGEONCURRENCY-0",
                    LocalizationKeys.UiShopCurrency);
                CreateLocalizedPlaceholderButton(
                    "btnSHOPITEMS-COMINGSOON",
                    LocalizationKeys.UiShopComingSoon);
                break;
            case EMainSubPageType.Quest:
                CreateLocalizedPlaceholderButton(
                    "btnQUESTLIST-EMPTY",
                    LocalizationKeys.UiQuestEmpty);
                break;
            case EMainSubPageType.Storage:
                CreateLocalizedPlaceholderButton(
                    "btnRESOURCES",
                    LocalizationKeys.UiCommonResources);
                CreateLocalizedPlaceholderButton(
                    "btnCONSUMABLEITEMS",
                    LocalizationKeys.UiCommonConsumableItems);
                CreateLocalizedPlaceholderButton(
                    "btnTICKETS",
                    LocalizationKeys.UiCommonTickets);
                break;
        }

        CreateLocalizedMenuButton(
            "btnBACK",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
    }

    private void CreateLocalizedPlaceholderButton(
        string stableName,
        string localizationKey)
    {
        Button button = CreateLocalizedMenuButton(
            stableName,
            localizationKey,
            null);
        if (button != null)
            button.interactable = false;
    }

    private void HandleBackClicked()
    {
        NavigateTo(mainPage, PageOpenMode.Resume);
    }
}
