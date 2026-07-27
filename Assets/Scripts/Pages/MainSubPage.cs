using System;
using System.Collections.Generic;
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

    private readonly List<CharacterData> _rosterEntries = new();
    private CodexBrowserView _rosterBrowser;
    private CharacterCollectionData _boundCharacterCollection;
    private string _rosterSearchQuery = string.Empty;
    private string _selectedRosterCharacterId = string.Empty;
    private bool _rosterDescending;
    private bool _rosterEventsBound;

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
            "ENEMIES | CHARACTERS | SKILLS | ITEMS",
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
                Transform obsoleteEventsButton = ButtonRoot.Find("btnEVENTS");
                if (obsoleteEventsButton != null)
                    obsoleteEventsButton.gameObject.SetActive(false);
                break;
            case EMainSubPageType.Roster:
                BuildRosterBrowser();
                CreateLocalizedTopLeftOverlayMenuButton(
                    "btnBACKTOMAIN",
                    LocalizationKeys.UiCommonBack,
                    HandleBackClicked);
                return;
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

    private void OnEnable()
    {
        if (pageType != EMainSubPageType.Roster)
            return;

        BindRosterEvents();
        RefreshRosterBrowser();
    }

    private void OnDisable()
    {
        UnbindRosterEvents();
    }

    protected override void OnDestroy()
    {
        UnbindRosterEvents();
        base.OnDestroy();
    }

    private void BuildRosterBrowser()
    {
        SetLegacyRosterControlActive(
            "btnOWNEDCHARACTERS-EMPTY",
            false);
        SetLegacyRosterControlActive("btnBACK", false);

        _rosterBrowser = CodexBrowserView.Build(ButtonRoot);
        _rosterBrowser.SetListOnlyMode(true, 3);
        _rosterBrowser.SetCallbacks(
            query =>
            {
                _rosterSearchQuery = (query ?? string.Empty).Trim();
                RefreshRosterBrowser();
            },
            RefreshRosterBrowser,
            () =>
            {
                _rosterDescending = !_rosterDescending;
                RefreshRosterBrowser();
            },
            characterId =>
            {
                _selectedRosterCharacterId =
                    characterId ?? string.Empty;
                _rosterBrowser.SetSelected(
                    _selectedRosterCharacterId);
            });
        RefreshRosterBrowser();
    }

    private void SetLegacyRosterControlActive(
        string objectName,
        bool active)
    {
        Transform control = ButtonRoot != null
            ? ButtonRoot.Find(objectName)
            : null;
        if (control != null)
            control.gameObject.SetActive(active);
    }

    private void RefreshRosterBrowser()
    {
        if (_rosterBrowser == null)
            return;

        CharacterCollectionData collection =
            DataManager.Current?.CharacterDatas;
        BindCharacterCollection(collection);
        _rosterEntries.Clear();
        foreach (CharacterSO definition in
                 CharacterDefinitionCatalog.GetAll())
        {
            CharacterData data = collection != null
                ? collection.CreatePreviewData(definition)
                : definition?.CreateData(new CharacterProgressData(
                    definition.CharacterId,
                    definition.InitiallyOwned));
            if (data == null || !data.IsOwned ||
                !MatchesRosterSearch(data))
            {
                continue;
            }

            _rosterEntries.Add(data);
        }

        _rosterEntries.Sort(CompareRosterEntries);
        List<CodexBrowserItemModel> items =
            new(_rosterEntries.Count);
        Color accentColor = new(0.22f, 0.48f, 0.68f, 1f);
        foreach (CharacterData data in _rosterEntries)
        {
            items.Add(new CodexBrowserItemModel(
                data.CharacterId,
                CharacterLocalization.GetName(data),
                data.IconSprite,
                false,
                accentColor));
        }

        bool selectionVisible = _rosterEntries.Exists(data =>
            string.Equals(
                data.CharacterId,
                _selectedRosterCharacterId,
                StringComparison.Ordinal));
        if (!selectionVisible)
        {
            _selectedRosterCharacterId =
                _rosterEntries.Count > 0
                    ? _rosterEntries[0].CharacterId
                    : string.Empty;
        }

        _rosterBrowser.SetToolbar(
            _rosterSearchQuery,
            LocalizationService.Get(
                LocalizationKeys.UiRosterSearchPlaceholder),
            LocalizationService.Get(
                LocalizationKeys.UiRosterSearch),
            LocalizationService.Get(
                LocalizationKeys.UiRosterFilterOwned),
            LocalizationService.Get(
                _rosterDescending
                    ? LocalizationKeys.UiRosterSortNameDescending
                    : LocalizationKeys.UiRosterSortNameAscending));
        _rosterBrowser.SetItems(
            items,
            _selectedRosterCharacterId);
    }

    private bool MatchesRosterSearch(CharacterData data)
    {
        if (string.IsNullOrWhiteSpace(_rosterSearchQuery))
            return true;

        return ContainsIgnoreCase(
                   CharacterLocalization.GetName(data),
                   _rosterSearchQuery) ||
               ContainsIgnoreCase(
                   data.Definition != null
                       ? data.Definition.name
                       : string.Empty,
                   _rosterSearchQuery) ||
               ContainsIgnoreCase(
                   data.CharacterId,
                   _rosterSearchQuery);
    }

    private int CompareRosterEntries(
        CharacterData left,
        CharacterData right)
    {
        int comparison = string.Compare(
            CharacterLocalization.GetName(left),
            CharacterLocalization.GetName(right),
            StringComparison.OrdinalIgnoreCase);
        if (_rosterDescending)
            comparison = -comparison;
        if (comparison != 0)
            return comparison;
        return string.Compare(
            left.CharacterId,
            right.CharacterId,
            StringComparison.Ordinal);
    }

    private static bool ContainsIgnoreCase(
        string value,
        string query)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(
                   query,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void BindRosterEvents()
    {
        if (_rosterEventsBound)
            return;

        LocalizationService.LocaleChanged +=
            HandleRosterLocaleChanged;
        BindCharacterCollection(
            DataManager.Current?.CharacterDatas);
        _rosterEventsBound = true;
    }

    private void UnbindRosterEvents()
    {
        if (!_rosterEventsBound)
            return;

        LocalizationService.LocaleChanged -=
            HandleRosterLocaleChanged;
        BindCharacterCollection(null);
        _rosterEventsBound = false;
    }

    private void BindCharacterCollection(
        CharacterCollectionData collection)
    {
        if (ReferenceEquals(
                _boundCharacterCollection,
                collection))
        {
            return;
        }

        if (_boundCharacterCollection != null)
        {
            _boundCharacterCollection.CharacterProgressChanged -=
                HandleCharacterProgressChanged;
        }

        _boundCharacterCollection = collection;
        if (_boundCharacterCollection != null)
        {
            _boundCharacterCollection.CharacterProgressChanged +=
                HandleCharacterProgressChanged;
        }
    }

    private void HandleRosterLocaleChanged(string unusedLocale)
    {
        RefreshRosterBrowser();
    }

    private void HandleCharacterProgressChanged(
        CharacterSO unusedDefinition)
    {
        if (isActiveAndEnabled)
            RefreshRosterBrowser();
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
