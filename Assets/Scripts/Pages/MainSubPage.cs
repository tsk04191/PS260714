using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EMainSubPageType
{
    Base,
    Roster,
    Shop,
    Recruit,
    Storage
}

[DisallowMultipleComponent]
public sealed class MainSubPage : RuntimeMenuPageBase
{
    [SerializeField] private EMainSubPageType pageType;
    [SerializeField, HideInInspector]
    private int fullScreenDesignerLayoutVersion;

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject enemyCodexPage;
    [SerializeField] private GameObject characterCodexPage;
    [SerializeField] private GameObject skillCodexPage;
    [SerializeField] private GameObject itemCodexPage;

    [Header("Recruit Banner Pages")]
    [SerializeField] private RecruitBannerPageDefinition[] recruitBannerPages =
    {
        new RecruitBannerPageDefinition(),
    };

    private readonly List<CharacterData> _rosterEntries = new();
    private OperatorRosterView _rosterBrowser;
    private RecruitBannerView _recruitBannerView;
    private RecruitRevealOverlay _recruitRevealOverlay;
    private CharacterCollectionData _boundCharacterCollection;
    private InventoryData _boundRecruitInventory;
    private string _rosterSearchQuery = string.Empty;
    private bool _rosterDescending;
    private bool _rosterEventsBound;
    private bool _recruitLocaleEventBound;
    private bool _recruitRevealInProgress;
    private string _lastRecruitResultMessage = string.Empty;

    protected override string PageTitle => pageType switch
    {
        EMainSubPageType.Base => "BASE",
        EMainSubPageType.Roster => "ROSTER",
        EMainSubPageType.Shop => "SHOP",
        EMainSubPageType.Recruit => "RECRUIT",
        EMainSubPageType.Storage => "STORAGE",
        _ => "PAGE"
    };

    protected override string PageDescription => pageType switch
    {
        EMainSubPageType.Base =>
            "BASE MANAGEMENT | MISSIONS | RECORDS",
        EMainSubPageType.Roster => "OWNED OPERATORS",
        EMainSubPageType.Shop => "DUNGEON CLEAR CURRENCY SHOP",
        EMainSubPageType.Recruit => "RECRUIT NEW OPERATORS",
        EMainSubPageType.Storage =>
            "RESOURCES | CONSUMABLE ITEMS | TICKETS",
        _ => string.Empty
    };

    protected override string PageTitleLocalizationKey => pageType switch
    {
        EMainSubPageType.Base => LocalizationKeys.UiBaseTitle,
        EMainSubPageType.Roster => LocalizationKeys.UiRosterTitle,
        EMainSubPageType.Shop => LocalizationKeys.UiShopTitle,
        EMainSubPageType.Recruit => LocalizationKeys.UiRecruitTitle,
        EMainSubPageType.Storage => LocalizationKeys.UiStorageTitle,
        _ => string.Empty
    };

    protected override string PageDescriptionLocalizationKey => pageType switch
    {
        EMainSubPageType.Base => LocalizationKeys.UiBaseDescription,
        EMainSubPageType.Roster => LocalizationKeys.UiRosterDescription,
        EMainSubPageType.Shop => LocalizationKeys.UiShopDescription,
        EMainSubPageType.Recruit =>
            LocalizationKeys.UiRecruitDescription,
        EMainSubPageType.Storage => LocalizationKeys.UiStorageDescription,
        _ => string.Empty
    };

    protected override Vector2 PanelSize => pageType == EMainSubPageType.Base
        ? new Vector2(680f, 840f)
        : new Vector2(680f, 720f);
    protected override bool FillAvailableSpace =>
        pageType == EMainSubPageType.Roster ||
        pageType == EMainSubPageType.Recruit;

    protected override void BuildButtons()
    {
        switch (pageType)
        {
            case EMainSubPageType.Base:
                CreateLocalizedPlaceholderButton(
                    "btnBASEFACILITIES-COMINGSOON",
                    LocalizationKeys.UiBaseFacilitiesComingSoon);
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
            case EMainSubPageType.Recruit:
                BuildRecruitBrowser();
                CreateLocalizedTopLeftOverlayMenuButton(
                    "btnBACKTOMAIN",
                    LocalizationKeys.UiCommonBack,
                    HandleBackClicked);
                return;
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
        if (pageType == EMainSubPageType.Roster)
        {
            BindRosterEvents();
            RefreshRosterBrowser();
            return;
        }

        if (pageType == EMainSubPageType.Recruit)
        {
            BindRecruitEvents();
            RefreshRecruitBannerView();
        }
    }

    private void OnDisable()
    {
        CancelRecruitReveal();
        UnbindRosterEvents();
        UnbindRecruitEvents();
    }

    protected override void OnDestroy()
    {
        CancelRecruitReveal();
        UnbindRosterEvents();
        UnbindRecruitEvents();
        base.OnDestroy();
    }

    private void BuildRosterBrowser()
    {
        SetLegacyRosterControlActive(
            "btnOWNEDCHARACTERS-EMPTY",
            false);
        SetLegacyRosterControlActive("btnBACK", false);
        ConfigureFullScreenContainer();

        _rosterBrowser = OperatorRosterView.Build(ButtonRoot);
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
                OpenRosterCharacter(characterId);
            });
        RefreshRosterBrowser();
    }

    private void BuildRecruitBrowser()
    {
        SetLegacyRosterControlActive(
            "btnRECRUITMENT-COMINGSOON",
            false);
        SetLegacyRosterControlActive("btnBACK", false);
        ConfigureFullScreenContainer();

        _recruitBannerView = RecruitBannerView.Build(ButtonRoot);
        _recruitBannerView.SetRecruitRequested(
            HandleRecruitRequested);
        _recruitRevealOverlay =
            RecruitRevealOverlay.Build(this, ButtonRoot);
        RefreshRecruitBannerView();
    }

    public bool EnsureRecruitRewardPoolData()
    {
        if (recruitBannerPages == null)
            return false;

        bool changed = false;
        for (int index = 0;
             index < recruitBannerPages.Length;
             index++)
        {
            RecruitBannerPageDefinition banner =
                recruitBannerPages[index];
            if (banner != null &&
                banner.EnsureRewardPoolData())
            {
                changed = true;
            }
        }
        return changed;
    }

#if UNITY_EDITOR
    public bool SyncRecruitEditorPreview(
        int bannerIndex,
        int revealPreviewCount,
        out string error)
    {
        error = string.Empty;
        if (Application.isPlaying)
        {
            error = "플레이 모드에서는 모집 씬 프리뷰를 동기화할 수 없습니다.";
            return false;
        }
        if (pageType != EMainSubPageType.Recruit)
        {
            error = "선택한 페이지가 모집 페이지가 아닙니다.";
            return false;
        }

        Init();
        if (_recruitBannerView == null ||
            _recruitRevealOverlay == null)
        {
            try
            {
                BuildRecruitBrowser();
            }
            catch (Exception exception)
            {
                error =
                    "모집 배너 또는 결과창 UI 재바인딩에 실패했습니다.\n" +
                    exception.Message;
                return false;
            }
        }
        if (_recruitBannerView == null ||
            _recruitRevealOverlay == null)
        {
            error = "모집 배너 또는 결과창 UI를 만들지 못했습니다.";
            return false;
        }

        RefreshRecruitBannerView();
        _recruitBannerView.SetPreviewPageIndex(bannerIndex);
        bool bannerCaptured =
            _recruitBannerView.CaptureDesignerLayout();
        bool revealCaptured =
            _recruitRevealOverlay.CaptureDesignerLayout();
        if (!bannerCaptured || !revealCaptured)
        {
            error = "모집 디자이너 UI 참조를 캡처하지 못했습니다.";
            return false;
        }

        int previewCount = revealPreviewCount == 1
            ? 1
            : revealPreviewCount == 10
                ? 10
                : 0;
        if (previewCount == 0)
        {
            _recruitRevealOverlay.HideEditorPreview();
        }
        else
        {
            List<RecruitRevealEntry> previewEntries = new();
            for (int index = 0; index < previewCount; index++)
            {
                previewEntries.Add(new RecruitRevealEntry(
                    $"editor.preview.{index}",
                    RecruitRewardType.Character,
                    $"샘플 대원 {index + 1:00}",
                    (CharacterGrade)(index % 4),
                    null,
                    1L,
                    index == 0));
            }
            _recruitRevealOverlay.ShowEditorPreview(
                previewEntries,
                IsKoreanLocale);
        }

        MarkDesignerLayoutCurrent();
        fullScreenDesignerLayoutVersion = 1;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene);
        return true;
    }
#endif

    private void ConfigureFullScreenContainer()
    {
        if (ButtonRoot == null)
            return;
        if (fullScreenDesignerLayoutVersion > 0)
            return;

        RectTransform buttonRoot = ButtonRoot;
        buttonRoot.anchorMin = Vector2.zero;
        buttonRoot.anchorMax = Vector2.one;
        buttonRoot.pivot = new Vector2(0.5f, 0.5f);
        buttonRoot.anchoredPosition = Vector2.zero;
        buttonRoot.sizeDelta = Vector2.zero;

        LayoutGroup buttonLayout =
            buttonRoot.GetComponent<LayoutGroup>();
        if (buttonLayout != null)
            buttonLayout.enabled = false;
        LayoutElement buttonElement =
            buttonRoot.GetComponent<LayoutElement>();
        if (buttonElement != null)
        {
            buttonElement.ignoreLayout = true;
            buttonElement.flexibleWidth = 1f;
            buttonElement.flexibleHeight = 1f;
        }

        if (buttonRoot.parent is not RectTransform panel)
            return;

        LayoutGroup panelLayout = panel.GetComponent<LayoutGroup>();
        if (panelLayout != null)
            panelLayout.enabled = false;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
        }

        SetPanelChildActive(panel, "txtPageTitle", false);
        SetPanelChildActive(panel, "txtPageDescription", false);
    }

    private static void SetPanelChildActive(
        Transform panel,
        string childName,
        bool active)
    {
        Transform child = panel != null
            ? panel.Find(childName)
            : null;
        if (child != null)
            child.gameObject.SetActive(active);
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
        List<OperatorRosterItemModel> items =
            new(_rosterEntries.Count);
        foreach (CharacterData data in _rosterEntries)
        {
            items.Add(new OperatorRosterItemModel(
                data.CharacterId,
                CharacterLocalization.GetName(data),
                data.StandingSprite != null
                    ? data.StandingSprite
                    : data.IconSprite,
                data.ActiveAbilityIconSprite));
        }

        int totalCount = CharacterDefinitionCatalog.GetAll().Count;
        int ownedCount = CountOwnedRosterCharacters(collection);
        bool korean = IsKoreanLocale;
        _rosterBrowser.SetHeader(
            LocalizationService.Get(LocalizationKeys.UiRosterTitle),
            korean
                ? $"보유 {ownedCount} / 전체 {totalCount}"
                : $"OWNED {ownedCount} / ALL {totalCount}",
            _rosterSearchQuery,
            LocalizationService.Get(
                LocalizationKeys.UiRosterSearchPlaceholder),
            LocalizationService.Get(
                LocalizationKeys.UiRosterSearch),
            LocalizationService.Get(
                _rosterDescending
                    ? LocalizationKeys.UiRosterSortNameDescending
                    : LocalizationKeys.UiRosterSortNameAscending),
            korean ? "이름" : "NAME",
            korean ? "레벨" : "LEVEL",
            korean ? "희귀도" : "RARITY",
            korean ? "신뢰도" : "TRUST",
            korean ? "전체" : "ALL",
            korean ? "직군 필터\n데이터 준비 중" : "ROLE FILTER\nDATA PENDING",
            LocalizationService.Get(LocalizationKeys.UiRosterEmpty));
        _rosterBrowser.SetItems(items);
    }

    private void RefreshRecruitBannerView()
    {
        if (_recruitBannerView == null)
            return;

        InventoryData inventory =
            DataManager.Current?.InventoryDatas;
        if (isActiveAndEnabled)
            BindRecruitInventory(inventory);

        List<RecruitBannerPageModel> pages = new();
        if (recruitBannerPages != null)
        {
            foreach (RecruitBannerPageDefinition definition in
                     recruitBannerPages)
            {
                if (definition != null)
                {
                    pages.Add(definition.CreateModel(
                        IsKoreanLocale,
                        inventory));
                }
            }
        }

        if (pages.Count == 0)
        {
            pages.Add(
                RecruitBannerPageDefinition.CreateDefault()
                    .CreateModel(IsKoreanLocale, inventory));
        }

        _recruitBannerView.SetHeader(
            LocalizationService.Get(LocalizationKeys.UiRecruitTitle),
            LocalizationService.Get(LocalizationKeys.UiCurrencyFree),
            LocalizationService.Get(LocalizationKeys.UiCurrencyPaid));
        _recruitBannerView.SetCurrencyAmounts(
            inventory?.GetAmount(CoreItemIds.FreeCredit) ?? 0L,
            inventory?.GetAmount(CoreItemIds.PaidCredit) ?? 0L);
        _recruitBannerView.SetPages(pages);
    }

    private void HandleRecruitRequested(
        int recruitCount,
        string bannerId)
    {
        if (_recruitRevealInProgress)
            return;

        RecruitBannerPageDefinition definition =
            FindRecruitBanner(bannerId);
        InventoryData inventory =
            DataManager.Current?.InventoryDatas;
        if (definition == null)
        {
            _recruitBannerView?.ShowStatusMessage(
                IsKoreanLocale
                    ? "모집 배너를 찾을 수 없습니다."
                    : "RECRUIT BANNER NOT FOUND.");
            return;
        }

        if (!definition.TryRecruit(
                inventory,
                DataManager.Current?.CharacterDatas,
                recruitCount,
                IsKoreanLocale,
                out RecruitExecutionResult result,
                out string error))
        {
            RefreshRecruitBannerView();
            _recruitBannerView?.ShowStatusMessage(error);
            return;
        }

        _lastRecruitResultMessage =
            BuildRecruitResultMessage(result, IsKoreanLocale);
        RefreshRecruitBannerView();
        List<RecruitRevealEntry> revealEntries =
            BuildRecruitRevealEntries(result);
        _recruitRevealInProgress = true;
        _recruitBannerView?.SetInteractionLocked(true);

        bool shown = _recruitRevealOverlay != null &&
                     _recruitRevealOverlay.Show(
                         revealEntries,
                         IsKoreanLocale,
                         HandleRecruitRevealClosed);
        if (!shown)
        {
            _recruitRevealInProgress = false;
            _recruitBannerView?.SetInteractionLocked(false);
            _recruitBannerView?.ShowStatusMessage(
                _lastRecruitResultMessage);
        }
    }

    private static List<RecruitRevealEntry>
        BuildRecruitRevealEntries(RecruitExecutionResult result)
    {
        List<RecruitRevealEntry> entries = new();
        if (result == null)
            return entries;

        for (int index = 0;
             index < result.Entries.Count;
             index++)
        {
            entries.Add(RecruitRevealEntry.FromReward(
                result.Entries[index],
                index));
        }
        return entries;
    }

    private void HandleRecruitRevealClosed()
    {
        _recruitRevealInProgress = false;
        _recruitBannerView?.SetInteractionLocked(false);
        RefreshRecruitBannerView();
        _recruitBannerView?.ShowStatusMessage(
            _lastRecruitResultMessage);
    }

    private void CancelRecruitReveal()
    {
        _recruitRevealOverlay?.CancelAndHide(false);
        _recruitRevealInProgress = false;
        _recruitBannerView?.SetInteractionLocked(false);
    }

    private RecruitBannerPageDefinition FindRecruitBanner(
        string bannerId)
    {
        if (recruitBannerPages == null)
            return null;

        for (int index = 0;
             index < recruitBannerPages.Length;
             index++)
        {
            RecruitBannerPageDefinition definition =
                recruitBannerPages[index];
            if (definition != null &&
                string.Equals(
                    definition.BannerId,
                    bannerId,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static string BuildRecruitResultMessage(
        RecruitExecutionResult result,
        bool korean)
    {
        if (result == null)
            return string.Empty;

        int[] gradeCounts = new int[4];
        int characterCount = 0;
        int itemCount = 0;
        for (int index = 0; index < result.Entries.Count; index++)
        {
            RecruitRewardResult entry = result.Entries[index];
            if (entry == null)
                continue;
            int grade = Mathf.Clamp((int)entry.Grade, 0, 3);
            gradeCounts[grade]++;
            if (entry.RewardType == RecruitRewardType.Character)
                characterCount++;
            else if (entry.RewardType == RecruitRewardType.Item)
                itemCount++;
        }

        List<string> summaries = new();
        if (characterCount > 0)
        {
            summaries.Add(korean
                ? $"캐릭터×{characterCount}"
                : $"CHARACTER×{characterCount}");
        }
        if (itemCount > 0)
        {
            summaries.Add(korean
                ? $"아이템×{itemCount}"
                : $"ITEM×{itemCount}");
        }
        for (int grade = 0; grade < gradeCounts.Length; grade++)
        {
            if (gradeCounts[grade] <= 0)
                continue;
            summaries.Add(
                korean
                    ? $"{grade}등급×{gradeCounts[grade]}"
                    : $"GRADE {grade}×{gradeCounts[grade]}");
        }

        string paymentName =
            result.Payment.Item?.GetDisplayName(korean) ??
            (korean ? "재화" : "CURRENCY");
        string summary = string.Join(" · ", summaries);
        return korean
            ? $"모집 완료 · {paymentName} -{result.Payment.Cost:N0}\n{summary}"
            : $"RECRUIT COMPLETE · {paymentName} -{result.Payment.Cost:N0}\n{summary}";
    }

    private static int CountOwnedRosterCharacters(
        CharacterCollectionData collection)
    {
        int ownedCount = 0;
        foreach (CharacterSO definition in
                 CharacterDefinitionCatalog.GetAll())
        {
            if (definition == null)
                continue;

            CharacterData data = collection != null
                ? collection.CreatePreviewData(definition)
                : definition.CreateData(new CharacterProgressData(
                    definition.CharacterId,
                    definition.InitiallyOwned));
            if (data != null && data.IsOwned)
                ownedCount++;
        }

        return ownedCount;
    }

    private void OpenRosterCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        if (characterCodexPage == null)
        {
            Transform sibling = transform.parent != null
                ? transform.parent.Find("pagCharacterCodex")
                : null;
            characterCodexPage = sibling != null
                ? sibling.gameObject
                : null;
        }
        if (characterCodexPage == null)
            return;

        if (characterCodexPage.TryGetComponent(
                out CharacterCodexPage characterCodex))
        {
            characterCodex.PrepareOpen(characterId, gameObject);
        }

        NavigateTo(characterCodexPage, PageOpenMode.Fresh);
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

    private static bool IsKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;

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

    private void BindRecruitEvents()
    {
        if (!_recruitLocaleEventBound)
        {
            LocalizationService.LocaleChanged +=
                HandleRecruitLocaleChanged;
            _recruitLocaleEventBound = true;
        }

        BindRecruitInventory(
            DataManager.Current?.InventoryDatas);
    }

    private void UnbindRecruitEvents()
    {
        if (_recruitLocaleEventBound)
        {
            LocalizationService.LocaleChanged -=
                HandleRecruitLocaleChanged;
            _recruitLocaleEventBound = false;
        }

        BindRecruitInventory(null);
    }

    private void BindRecruitInventory(InventoryData inventory)
    {
        if (ReferenceEquals(_boundRecruitInventory, inventory))
            return;

        if (_boundRecruitInventory != null)
        {
            _boundRecruitInventory.AmountChanged -=
                HandleRecruitInventoryChanged;
        }

        _boundRecruitInventory = inventory;
        if (_boundRecruitInventory != null)
        {
            _boundRecruitInventory.AmountChanged +=
                HandleRecruitInventoryChanged;
        }
    }

    private void HandleRecruitInventoryChanged(
        string unusedItemId,
        long unusedAmount)
    {
        if (isActiveAndEnabled)
            RefreshRecruitBannerView();
    }

    private void HandleRecruitLocaleChanged(string unusedLocale)
    {
        RefreshRecruitBannerView();
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

public readonly struct OperatorRosterItemModel
{
    public string Id { get; }
    public string DisplayName { get; }
    public Sprite Portrait { get; }
    public Sprite SkillIcon { get; }

    public OperatorRosterItemModel(
        string id,
        string displayName,
        Sprite portrait,
        Sprite skillIcon)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Portrait = portrait;
        SkillIcon = skillIcon;
    }
}

public sealed class OperatorRosterView
{
    private const string RootName = "grpOperatorRoster";
    private const string CardPrefix = "btnOperatorCard_";
    private const float CardWidth = 220f;
    private const float CardHeight = 308f;

    private static readonly Color BackdropColor =
        new(0.035f, 0.045f, 0.043f, 1f);
    private static readonly Color HeaderColor =
        new(0.055f, 0.075f, 0.071f, 0.98f);
    private static readonly Color RailColor =
        new(0.045f, 0.06f, 0.058f, 0.99f);
    private static readonly Color CardColor =
        new(0.10f, 0.135f, 0.125f, 1f);
    private static readonly Color CardPlateColor =
        new(0.025f, 0.035f, 0.033f, 0.94f);
    private static readonly Color AccentColor =
        new(0.25f, 0.76f, 0.68f, 1f);
    private static readonly Color ActiveButtonColor =
        new(0.12f, 0.39f, 0.36f, 1f);
    private static readonly Color InactiveButtonColor =
        new(0.11f, 0.14f, 0.135f, 1f);
    private static readonly Color TextColor =
        new(0.91f, 0.94f, 0.89f, 1f);
    private static readonly Color MutedTextColor =
        new(0.48f, 0.55f, 0.52f, 1f);

    private sealed class CardView
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Image Portrait { get; }
        public TextMeshProUGUI PortraitFallback { get; }
        public TextMeshProUGUI Name { get; }
        public GameObject SkillRoot { get; }
        public Image SkillIcon { get; }
        public Outline SelectionOutline { get; }
        public Button Button { get; }
        public OperatorRosterCardHighlight Highlight { get; }
        public string BoundId { get; set; }

        public CardView(
            GameObject root,
            Image background,
            Image portrait,
            TextMeshProUGUI portraitFallback,
            TextMeshProUGUI name,
            GameObject skillRoot,
            Image skillIcon,
            Outline selectionOutline,
            Button button,
            OperatorRosterCardHighlight highlight)
        {
            Root = root;
            Background = background;
            Portrait = portrait;
            PortraitFallback = portraitFallback;
            Name = name;
            SkillRoot = skillRoot;
            SkillIcon = skillIcon;
            SelectionOutline = selectionOutline;
            Button = button;
            Highlight = highlight;
        }
    }

    private readonly Transform _host;
    private readonly List<CardView> _cards = new();
    private RectTransform _root;
    private TMP_InputField _searchInput;
    private TextMeshProUGUI _searchPlaceholder;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _countText;
    private TextMeshProUGUI _activeSortLabel;
    private TextMeshProUGUI _levelSortLabel;
    private TextMeshProUGUI _raritySortLabel;
    private TextMeshProUGUI _trustSortLabel;
    private TextMeshProUGUI _sortButtonLabel;
    private TextMeshProUGUI _allFilterLabel;
    private TextMeshProUGUI _pendingFilterText;
    private TextMeshProUGUI _emptyText;
    private Button _searchButton;
    private Button _sortButton;
    private Button _allFilterButton;
    private Transform _cardContent;
    private Action<string> _searchRequested;
    private Action _filterRequested;
    private Action _sortRequested;
    private Action<string> _itemSelected;

    private OperatorRosterView(Transform host)
    {
        _host = host;
    }

    public static OperatorRosterView Build(Transform host)
    {
        OperatorRosterView view = new(host);
        view.HideLegacyBrowser();
        if (!view.TryBindLayout())
        {
            view.BuildLayout();
            if (!view.TryBindLayout())
            {
                throw new InvalidOperationException(
                    "Failed to build the operator roster layout.");
            }
        }

        view.ApplyHeaderLayout();
        view._root.gameObject.SetActive(true);
        view._root.SetAsLastSibling();
        return view;
    }

    public void SetCallbacks(
        Action<string> searchRequested,
        Action filterRequested,
        Action sortRequested,
        Action<string> itemSelected)
    {
        _searchRequested = searchRequested;
        _filterRequested = filterRequested;
        _sortRequested = sortRequested;
        _itemSelected = itemSelected;

        _searchButton.onClick.RemoveAllListeners();
        _searchButton.onClick.AddListener(SubmitSearch);
        _sortButton.onClick.RemoveAllListeners();
        _sortButton.onClick.AddListener(
            () => _sortRequested?.Invoke());
        _allFilterButton.onClick.RemoveAllListeners();
        _allFilterButton.onClick.AddListener(
            () => _filterRequested?.Invoke());
        _searchInput.onSubmit.RemoveAllListeners();
        _searchInput.onSubmit.AddListener(_ => SubmitSearch());
    }

    public void SetHeader(
        string title,
        string count,
        string searchText,
        string searchPlaceholder,
        string searchLabel,
        string sortLabel,
        string activeSortLabel,
        string levelSortLabel,
        string raritySortLabel,
        string trustSortLabel,
        string allFilterLabel,
        string pendingFilterLabel,
        string emptyLabel)
    {
        _titleText.text = title ?? string.Empty;
        _countText.text = count ?? string.Empty;
        _searchInput.SetTextWithoutNotify(searchText ?? string.Empty);
        _searchPlaceholder.text = searchPlaceholder ?? string.Empty;
        SetButtonLabel(_searchButton, searchLabel);
        _sortButtonLabel.text = sortLabel ?? string.Empty;
        _activeSortLabel.text = activeSortLabel ?? string.Empty;
        _levelSortLabel.text = levelSortLabel ?? string.Empty;
        _raritySortLabel.text = raritySortLabel ?? string.Empty;
        _trustSortLabel.text = trustSortLabel ?? string.Empty;
        _allFilterLabel.text = allFilterLabel ?? string.Empty;
        _pendingFilterText.text = pendingFilterLabel ?? string.Empty;
        _emptyText.text = emptyLabel ?? string.Empty;
    }

    public void SetItems(
        IReadOnlyList<OperatorRosterItemModel> items)
    {
        int sourceCount = items?.Count ?? 0;
        int cardCount = 0;
        HashSet<string> registeredIds =
            new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < sourceCount; index++)
        {
            OperatorRosterItemModel item = items[index];
            string itemId = item.Id?.Trim();
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !registeredIds.Add(itemId))
            {
                continue;
            }

            CardView card = GetOrCreateCard(cardCount);
            BindCard(card, item);
            card.Root.SetActive(true);
            cardCount++;
        }

        for (int index = cardCount; index < _cards.Count; index++)
            _cards[index].Root.SetActive(false);
        for (int index = 0; index < _cardContent.childCount; index++)
        {
            Transform child = _cardContent.GetChild(index);
            if (child == null ||
                !child.name.StartsWith(
                    CardPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring(CardPrefix.Length);
            if (int.TryParse(suffix, out int cardIndex) &&
                cardIndex >= cardCount)
            {
                child.gameObject.SetActive(false);
            }
        }

        _emptyText.gameObject.SetActive(cardCount == 0);
        if (_cardContent is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void SubmitSearch()
    {
        _searchRequested?.Invoke(_searchInput.text);
    }

    private void HideLegacyBrowser()
    {
        if (_host == null)
            return;

        Transform legacy = _host.Find("grpCodexBrowser");
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    private bool TryBindLayout()
    {
        if (_host == null)
            return false;

        Transform root = _host.Find(RootName);
        Transform header = root?.Find("grpRosterHeader");
        Transform searchInput = header?.Find("inpRosterSearch");
        Transform searchViewport = searchInput?.Find(
            "vptRosterSearch");
        Transform searchButton = header?.Find("btnRosterSearch");
        Transform sortButton = header?.Find("btnRosterSortDirection");
        Transform rail = root?.Find("grpRosterFilterRail");
        Transform allFilter = rail?.Find("btnRosterFilterAll");
        Transform content = root?.Find(
            "scrRosterList/vptRosterList/grpRosterCardContent");

        _root = root as RectTransform;
        _titleText = header?.Find("txtRosterTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _countText = header?.Find("txtRosterCount")
            ?.GetComponent<TextMeshProUGUI>();
        _searchInput = searchInput?.GetComponent<TMP_InputField>();
        _searchPlaceholder = searchViewport
            ?.Find("txtRosterSearchPlaceholder")
            ?.GetComponent<TextMeshProUGUI>();
        _searchButton = searchButton?.GetComponent<Button>();
        _activeSortLabel = header
            ?.Find("btnRosterSortName/txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _levelSortLabel = header
            ?.Find("btnRosterSortLevel/txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _raritySortLabel = header
            ?.Find("btnRosterSortRarity/txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _trustSortLabel = header
            ?.Find("btnRosterSortTrust/txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _sortButton = sortButton?.GetComponent<Button>();
        _sortButtonLabel = sortButton?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _allFilterButton = allFilter?.GetComponent<Button>();
        _allFilterLabel = allFilter?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _pendingFilterText = rail?.Find("txtRosterFilterPending")
            ?.GetComponent<TextMeshProUGUI>();
        _emptyText = root?.Find("txtRosterEmpty")
            ?.GetComponent<TextMeshProUGUI>();
        _cardContent = content;

        return _root != null &&
               _titleText != null &&
               _countText != null &&
               _searchInput != null &&
               _searchPlaceholder != null &&
               _searchButton != null &&
               _activeSortLabel != null &&
               _levelSortLabel != null &&
               _raritySortLabel != null &&
               _trustSortLabel != null &&
               _sortButton != null &&
               _sortButtonLabel != null &&
               _allFilterButton != null &&
               _allFilterLabel != null &&
               _pendingFilterText != null &&
               _emptyText != null &&
               _cardContent != null;
    }

    private void BuildLayout()
    {
        GameObject rootObject = CreateUiObject(
            _host,
            RootName,
            typeof(CanvasRenderer),
            typeof(Image));
        _root = (RectTransform)rootObject.transform;
        Stretch(_root);
        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = BackdropColor;
        rootImage.raycastTarget = true;

        BuildHeader(_root);
        BuildRosterList(_root);
        BuildFilterRail(_root);

        TextMeshProUGUI empty = CreateText(
            _root,
            "txtRosterEmpty",
            24f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        RectTransform emptyRect = empty.rectTransform;
        emptyRect.anchorMin = new Vector2(0f, 0f);
        emptyRect.anchorMax = new Vector2(1f, 1f);
        emptyRect.offsetMin = new Vector2(32f, 28f);
        emptyRect.offsetMax = new Vector2(-144f, -132f);
        empty.gameObject.SetActive(false);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObject = CreateUiObject(
            parent,
            "grpRosterHeader",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform header = (RectTransform)headerObject.transform;
        ConfigureTopStretch(header, 112f);
        Image headerImage = headerObject.GetComponent<Image>();
        headerImage.color = HeaderColor;
        headerImage.raycastTarget = false;

        GameObject accentObject = CreateUiObject(
            header,
            "imgRosterHeaderAccent",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform accent = (RectTransform)accentObject.transform;
        ConfigureTopLeft(
            accent,
            new Vector2(176f, -24f),
            new Vector2(6f, 64f));
        accentObject.GetComponent<Image>().color = AccentColor;

        TextMeshProUGUI title = CreateText(
            header,
            "txtRosterTitle",
            38f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        ConfigureTopLeft(
            title.rectTransform,
            new Vector2(200f, -16f),
            new Vector2(236f, 52f));
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI count = CreateText(
            header,
            "txtRosterCount",
            17f,
            TextAlignmentOptions.MidlineLeft,
            AccentColor);
        ConfigureTopLeft(
            count.rectTransform,
            new Vector2(200f, -70f),
            new Vector2(236f, 28f));

        BuildSearchInput(header);

        BuildHeaderButton(
            header,
            "btnRosterSortName",
            new Vector2(-716f, -28f),
            new Vector2(120f, 52f),
            true,
            true);
        BuildHeaderButton(
            header,
            "btnRosterSortLevel",
            new Vector2(-586f, -28f),
            new Vector2(120f, 52f),
            false,
            false);
        BuildHeaderButton(
            header,
            "btnRosterSortRarity",
            new Vector2(-456f, -28f),
            new Vector2(120f, 52f),
            false,
            false);
        BuildHeaderButton(
            header,
            "btnRosterSortTrust",
            new Vector2(-326f, -28f),
            new Vector2(120f, 52f),
            false,
            false);
        BuildHeaderButton(
            header,
            "btnRosterSortDirection",
            new Vector2(-146f, -28f),
            new Vector2(170f, 52f),
            true,
            true);
    }

    private void BuildSearchInput(Transform header)
    {
        GameObject inputObject = CreateUiObject(
            header,
            "inpRosterSearch",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField));
        RectTransform inputRect = (RectTransform)inputObject.transform;
        ConfigureTopLeft(
            inputRect,
            new Vector2(456f, -28f),
            new Vector2(300f, 52f));
        inputObject.GetComponent<Image>().color =
            new Color(0.025f, 0.04f, 0.038f, 1f);

        GameObject viewportObject = CreateUiObject(
            inputRect,
            "vptRosterSearch",
            typeof(RectMask2D));
        RectTransform viewport =
            (RectTransform)viewportObject.transform;
        Stretch(viewport);
        viewport.offsetMin = new Vector2(16f, 6f);
        viewport.offsetMax = new Vector2(-16f, -6f);

        TextMeshProUGUI valueText = CreateText(
            viewport,
            "txtRosterSearchValue",
            18f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(valueText.rectTransform);
        valueText.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI placeholder = CreateText(
            viewport,
            "txtRosterSearchPlaceholder",
            18f,
            TextAlignmentOptions.MidlineLeft,
            MutedTextColor);
        Stretch(placeholder.rectTransform);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = valueText;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 64;

        Button searchButton = BuildHeaderButton(
            header,
            "btnRosterSearch",
            Vector2.zero,
            new Vector2(86f, 52f),
            true,
            true);
        ConfigureTopLeft(
            (RectTransform)searchButton.transform,
            new Vector2(766f, -28f),
            new Vector2(86f, 52f));
        TextMeshProUGUI label = searchButton.transform
            .Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        if (label != null)
            label.fontSize = 15f;
    }

    private Button BuildHeaderButton(
        Transform parent,
        string objectName,
        Vector2 topRightPosition,
        Vector2 size,
        bool activeColor,
        bool interactable)
    {
        GameObject buttonObject = CreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = topRightPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        Color color = activeColor
            ? ActiveButtonColor
            : InactiveButtonColor;
        image.color = color;
        image.raycastTarget = interactable;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        ApplyButtonColors(button, color);

        TextMeshProUGUI label = CreateText(
            rect,
            "txtLabel",
            17f,
            TextAlignmentOptions.Center,
            activeColor ? TextColor : MutedTextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(8f, 4f);
        label.rectTransform.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private void BuildRosterList(Transform parent)
    {
        GameObject scrollObject = CreateUiObject(
            parent,
            "scrRosterList",
            typeof(ScrollRect));
        RectTransform scrollRect =
            (RectTransform)scrollObject.transform;
        Stretch(scrollRect);
        scrollRect.offsetMin = new Vector2(28f, 28f);
        scrollRect.offsetMax = new Vector2(-142f, -132f);

        GameObject viewportObject = CreateUiObject(
            scrollRect,
            "vptRosterList",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        RectTransform viewport =
            (RectTransform)viewportObject.transform;
        Stretch(viewport);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = CreateUiObject(
            viewport,
            "grpRosterCardContent",
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content =
            (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        GridLayoutGroup grid =
            contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(16f, 18f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint =
            GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;

        ContentSizeFitter fitter =
            contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.08f;
        scroll.scrollSensitivity = 44f;
    }

    private void BuildFilterRail(Transform parent)
    {
        GameObject railObject = CreateUiObject(
            parent,
            "grpRosterFilterRail",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rail = (RectTransform)railObject.transform;
        rail.anchorMin = new Vector2(1f, 0f);
        rail.anchorMax = Vector2.one;
        rail.pivot = new Vector2(1f, 0.5f);
        rail.anchoredPosition = new Vector2(-18f, -52f);
        rail.sizeDelta = new Vector2(106f, -160f);
        Image railImage = railObject.GetComponent<Image>();
        railImage.color = RailColor;
        railImage.raycastTarget = true;

        GameObject filterObject = CreateUiObject(
            rail,
            "btnRosterFilterAll",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform filterRect =
            (RectTransform)filterObject.transform;
        filterRect.anchorMin = new Vector2(0f, 1f);
        filterRect.anchorMax = Vector2.one;
        filterRect.pivot = new Vector2(0.5f, 1f);
        filterRect.anchoredPosition = Vector2.zero;
        filterRect.sizeDelta = new Vector2(0f, 82f);
        Image filterImage = filterObject.GetComponent<Image>();
        filterImage.color = ActiveButtonColor;
        Button filterButton = filterObject.GetComponent<Button>();
        filterButton.targetGraphic = filterImage;
        ApplyButtonColors(filterButton, ActiveButtonColor);

        TextMeshProUGUI filterLabel = CreateText(
            filterRect,
            "txtLabel",
            22f,
            TextAlignmentOptions.Center,
            TextColor);
        Stretch(filterLabel.rectTransform);

        TextMeshProUGUI pending = CreateText(
            rail,
            "txtRosterFilterPending",
            15f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        pending.rectTransform.anchorMin = new Vector2(0f, 0f);
        pending.rectTransform.anchorMax = new Vector2(1f, 1f);
        pending.rectTransform.offsetMin = new Vector2(8f, 18f);
        pending.rectTransform.offsetMax = new Vector2(-8f, -104f);
    }

    private CardView GetOrCreateCard(int index)
    {
        while (_cards.Count <= index)
        {
            int cardIndex = _cards.Count;
            Transform existing = _cardContent.Find(
                CardPrefix + cardIndex);
            _cards.Add(existing != null
                ? BindExistingCard(existing.gameObject)
                : BuildCard(cardIndex));
        }

        return _cards[index];
    }

    private CardView BuildCard(int index)
    {
        GameObject cardObject = CreateUiObject(
            _cardContent,
            CardPrefix + index,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline),
            typeof(OperatorRosterCardHighlight));
        Image background = cardObject.GetComponent<Image>();
        background.color = CardColor;
        background.raycastTarget = true;

        Button button = cardObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;
        OperatorRosterCardHighlight highlight =
            cardObject.GetComponent<OperatorRosterCardHighlight>();
        highlight.Configure(background, outline);

        GameObject portraitObject = CreateUiObject(
            cardObject.transform,
            "imgOperatorPortrait",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform portraitRect =
            (RectTransform)portraitObject.transform;
        Stretch(portraitRect);
        portraitRect.offsetMin = new Vector2(0f, 64f);
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        TextMeshProUGUI fallback = CreateText(
            cardObject.transform,
            "txtOperatorPortraitFallback",
            54f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        Stretch(fallback.rectTransform);
        fallback.rectTransform.offsetMin = new Vector2(0f, 64f);
        fallback.fontStyle = FontStyles.Bold;

        GameObject markObject = CreateUiObject(
            cardObject.transform,
            "grpOperatorMark",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform markRect = (RectTransform)markObject.transform;
        ConfigureTopLeft(
            markRect,
            new Vector2(10f, -10f),
            new Vector2(38f, 38f));
        markObject.GetComponent<Image>().color =
            new Color(0.025f, 0.04f, 0.038f, 0.94f);
        markObject.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI mark = CreateText(
            markRect,
            "txtMark",
            18f,
            TextAlignmentOptions.Center,
            AccentColor);
        Stretch(mark.rectTransform);
        mark.text = "◆";

        GameObject plateObject = CreateUiObject(
            cardObject.transform,
            "imgOperatorNamePlate",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform plate = (RectTransform)plateObject.transform;
        plate.anchorMin = Vector2.zero;
        plate.anchorMax = new Vector2(1f, 0f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.anchoredPosition = Vector2.zero;
        plate.sizeDelta = new Vector2(0f, 64f);
        Image plateImage = plateObject.GetComponent<Image>();
        plateImage.color = CardPlateColor;
        plateImage.raycastTarget = false;

        GameObject accentObject = CreateUiObject(
            plate,
            "imgOperatorAccent",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform accent = (RectTransform)accentObject.transform;
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(1f, 0f);
        accent.pivot = new Vector2(0.5f, 0f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, 5f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        TextMeshProUGUI name = CreateText(
            plate,
            "txtOperatorName",
            22f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(name.rectTransform);
        name.rectTransform.offsetMin = new Vector2(14f, 7f);
        name.rectTransform.offsetMax = new Vector2(-62f, -5f);
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject skillRoot = CreateUiObject(
            plate,
            "grpOperatorSkill",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform skillRect =
            (RectTransform)skillRoot.transform;
        skillRect.anchorMin = new Vector2(1f, 0.5f);
        skillRect.anchorMax = new Vector2(1f, 0.5f);
        skillRect.pivot = new Vector2(1f, 0.5f);
        skillRect.anchoredPosition = new Vector2(-10f, 0f);
        skillRect.sizeDelta = new Vector2(44f, 44f);
        Image skillBackground = skillRoot.GetComponent<Image>();
        skillBackground.color =
            new Color(0.10f, 0.18f, 0.17f, 1f);
        skillBackground.raycastTarget = false;

        GameObject skillIconObject = CreateUiObject(
            skillRect,
            "imgOperatorSkillIcon",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform skillIconRect =
            (RectTransform)skillIconObject.transform;
        Stretch(skillIconRect);
        skillIconRect.offsetMin = new Vector2(4f, 4f);
        skillIconRect.offsetMax = new Vector2(-4f, -4f);
        Image skillIcon = skillIconObject.GetComponent<Image>();
        skillIcon.preserveAspect = true;
        skillIcon.raycastTarget = false;

        CardView card = new(
            cardObject,
            background,
            portrait,
            fallback,
            name,
            skillRoot,
            skillIcon,
            outline,
            button,
            highlight);
        PrepareCardRaycastTargets(cardObject, background);
        WireCardButton(card);
        return card;
    }

    private CardView BindExistingCard(GameObject cardObject)
    {
        Image background = cardObject.GetComponent<Image>();
        Outline outline = cardObject.GetComponent<Outline>();
        OperatorRosterCardHighlight highlight =
            cardObject.GetComponent<OperatorRosterCardHighlight>();
        if (highlight == null)
        {
            highlight =
                cardObject.AddComponent<OperatorRosterCardHighlight>();
        }
        highlight.Configure(background, outline);

        Button button = cardObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        CardView card = new(
            cardObject,
            background,
            cardObject.transform.Find("imgOperatorPortrait")
                ?.GetComponent<Image>(),
            cardObject.transform.Find("txtOperatorPortraitFallback")
                ?.GetComponent<TextMeshProUGUI>(),
            cardObject.transform
                .Find("imgOperatorNamePlate/txtOperatorName")
                ?.GetComponent<TextMeshProUGUI>(),
            cardObject.transform
                .Find("imgOperatorNamePlate/grpOperatorSkill")
                ?.gameObject,
            cardObject.transform
                .Find(
                    "imgOperatorNamePlate/grpOperatorSkill/" +
                    "imgOperatorSkillIcon")
                ?.GetComponent<Image>(),
            outline,
            button,
            highlight);
        PrepareCardRaycastTargets(cardObject, background);
        WireCardButton(card);
        return card;
    }

    private static void PrepareCardRaycastTargets(
        GameObject cardObject,
        Image background)
    {
        Graphic[] graphics =
            cardObject.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic != null)
                graphic.raycastTarget = graphic == background;
        }
    }

    private void WireCardButton(CardView card)
    {
        card.Button.onClick.RemoveAllListeners();
        card.Button.onClick.AddListener(
            () => _itemSelected?.Invoke(card.BoundId));
    }

    private static void BindCard(
        CardView card,
        OperatorRosterItemModel item)
    {
        card.BoundId = item.Id ?? string.Empty;
        card.Name.text = item.DisplayName ?? string.Empty;
        card.Portrait.sprite = item.Portrait;
        card.Portrait.enabled = item.Portrait != null;
        card.PortraitFallback.gameObject.SetActive(
            item.Portrait == null);
        card.PortraitFallback.text = CreateFallbackLabel(
            item.DisplayName);
        card.SkillRoot.SetActive(item.SkillIcon != null);
        card.SkillIcon.sprite = item.SkillIcon;
    }

    private static string CreateFallbackLabel(string displayName)
    {
        string value = (displayName ?? string.Empty).Trim();
        if (value.Length == 0)
            return "?";
        return value.Length <= 2 ? value : value.Substring(0, 2);
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TextMeshProUGUI label = button != null
            ? button.transform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (label != null)
            label.text = value ?? string.Empty;
    }

    private void ApplyHeaderLayout()
    {
        Transform header = _root != null
            ? _root.Find("grpRosterHeader")
            : null;
        RectTransform accent = header
            ?.Find("imgRosterHeaderAccent")
            as RectTransform;
        if (accent != null)
        {
            ConfigureTopLeft(
                accent,
                new Vector2(176f, -24f),
                new Vector2(6f, 64f));
        }

        if (_titleText != null)
        {
            ConfigureTopLeft(
                _titleText.rectTransform,
                new Vector2(200f, -16f),
                new Vector2(236f, 52f));
        }

        if (_countText != null)
        {
            ConfigureTopLeft(
                _countText.rectTransform,
                new Vector2(200f, -70f),
                new Vector2(236f, 28f));
        }
    }

    private static GameObject CreateUiObject(
        Transform parent,
        string objectName,
        params Type[] components)
    {
        List<Type> types = new() { typeof(RectTransform) };
        if (components != null)
            types.AddRange(components);
        GameObject child = new(objectName, types.ToArray());
        child.layer = parent != null ? parent.gameObject.layer : 0;
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateUiObject(
            parent,
            objectName,
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 8f);
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void ApplyButtonColors(
        Button button,
        Color baseColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor =
            Color.Lerp(baseColor, Color.white, 0.14f);
        colors.pressedColor =
            Color.Lerp(baseColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor =
            Color.Lerp(baseColor, Color.black, 0.48f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ConfigureTopStretch(
        RectTransform rect,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void ConfigureTopLeft(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

[DisallowMultipleComponent]
public sealed class OperatorRosterCardHighlight :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private static readonly Color NormalColor =
        new(0.10f, 0.135f, 0.125f, 1f);
    private static readonly Color HoverColor =
        new(0.16f, 0.30f, 0.27f, 1f);
    private static readonly Color HoverOutlineColor =
        new(0.64f, 0.96f, 0.88f, 1f);

    private Image _background;
    private Outline _outline;
    private bool _hovered;
    private bool _pressed;

    public void Configure(Image background, Outline outline)
    {
        _background = background;
        _outline = outline;
        ApplyVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pressed = false;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (_background != null)
        {
            Color color = _hovered
                ? HoverColor
                : NormalColor;
            if (_pressed)
                color = Color.Lerp(color, Color.black, 0.22f);
            _background.color = color;
        }

        if (_outline == null)
            return;

        _outline.enabled = _hovered;
        _outline.effectColor = HoverOutlineColor;
        _outline.effectDistance = new Vector2(4f, -4f);
    }
}
