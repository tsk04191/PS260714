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

    protected override Vector2 PanelSize => pageType == EMainSubPageType.Codex
        ? new Vector2(680f, 840f)
        : new Vector2(680f, 720f);

    protected override void BuildButtons()
    {
        switch (pageType)
        {
            case EMainSubPageType.Codex:
                CreateMenuButton(
                    "ENEMIES",
                    () => NavigateTo(
                        enemyCodexPage,
                        PageOpenMode.Fresh));
                CreateMenuButton(
                    "CHARACTERS",
                    () => NavigateTo(
                        characterCodexPage,
                        PageOpenMode.Fresh));
                CreateMenuButton(
                    "SKILLS",
                    () => NavigateTo(
                        skillCodexPage,
                        PageOpenMode.Fresh));
                CreateMenuButton(
                    "ITEMS",
                    () => NavigateTo(
                        itemCodexPage,
                        PageOpenMode.Fresh));
                CreatePlaceholderButton("EVENTS");
                break;
            case EMainSubPageType.Roster:
                CreatePlaceholderButton("OWNED CHARACTERS - EMPTY");
                break;
            case EMainSubPageType.Shop:
                CreatePlaceholderButton("DUNGEON CURRENCY - 0");
                CreatePlaceholderButton("SHOP ITEMS - COMING SOON");
                break;
            case EMainSubPageType.Quest:
                CreatePlaceholderButton("QUEST LIST - EMPTY");
                break;
            case EMainSubPageType.Storage:
                CreatePlaceholderButton("RESOURCES");
                CreatePlaceholderButton("CONSUMABLE ITEMS");
                CreatePlaceholderButton("TICKETS");
                break;
        }

        CreateMenuButton("BACK", HandleBackClicked);
    }

    private void CreatePlaceholderButton(string label)
    {
        Button button = CreateMenuButton(label, null);
        if (button != null)
            button.interactable = false;
    }

    private void HandleBackClicked()
    {
        NavigateTo(mainPage, PageOpenMode.Resume);
    }
}
