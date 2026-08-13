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

public enum OperatorRosterSortCriterion
{
    Name = 0,
    Rarity = 1,
    Trust = 2,
    Role = 3
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
    private CharacterRoleSO _rosterRoleFilter;
    private OperatorRosterSortCriterion _rosterSortCriterion =
        OperatorRosterSortCriterion.Name;
    private bool _rosterDescending;
    private bool _rosterEventsBound;
    private bool _recruitLocaleEventBound;
    private bool _recruitRevealInProgress;
    private string _lastRecruitResultMessage = string.Empty;
#if UNITY_EDITOR
    private bool _recruitEditorSyncInProgress;
#endif

    public bool IsRecruitPage => pageType == EMainSubPageType.Recruit;

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
    protected override bool RequiresSavedDesignerUiAtRuntime =>
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
                Transform obsoleteSkillsButton =
                    ButtonRoot.Find("btnSKILLS");
                if (obsoleteSkillsButton != null)
                    obsoleteSkillsButton.gameObject.SetActive(false);
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
                BindRecruitBackButton();
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
        _rosterBrowser = OperatorRosterView.Build(ButtonRoot);
        _rosterBrowser.SetCallbacks(
            query =>
            {
                _rosterSearchQuery = (query ?? string.Empty).Trim();
                RefreshRosterBrowser();
            },
            criterion =>
            {
                _rosterSortCriterion = criterion;
                RefreshRosterBrowser();
            },
            role =>
            {
                _rosterRoleFilter = role;
                RefreshRosterBrowser();
            },
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
#if UNITY_EDITOR
        _recruitBannerView = _recruitEditorSyncInProgress
            ? RecruitBannerView.BuildEditor(ButtonRoot)
            : RecruitBannerView.Build(ButtonRoot);
#else
        _recruitBannerView = RecruitBannerView.Build(ButtonRoot);
#endif
        _recruitBannerView.SetRecruitRequested(
            HandleRecruitRequested);
#if UNITY_EDITOR
        _recruitRevealOverlay = _recruitEditorSyncInProgress
            ? RecruitRevealOverlay.BuildEditor(this, ButtonRoot)
            : RecruitRevealOverlay.Build(this, ButtonRoot);
#else
        _recruitRevealOverlay =
            RecruitRevealOverlay.Build(this, ButtonRoot);
#endif
        RefreshRecruitBannerView();
    }

    private void BindRecruitBackButton()
    {
        BindLocalizedOverlayMenuButton(
            "btnBACKTOMAIN",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
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
    public bool ValidateRecruitEditorUi(out string error)
    {
        error = string.Empty;
        if (pageType != EMainSubPageType.Recruit)
        {
            error = "선택한 페이지가 모집 페이지가 아닙니다.";
            return false;
        }

        RecruitBannerDesignerBindings banner =
            GetComponentInChildren<RecruitBannerDesignerBindings>(true);
        RecruitRevealDesignerBindings reveal =
            GetComponentInChildren<RecruitRevealDesignerBindings>(true);
        Transform root = transform.Find(RuntimeRootObjectName);
        Transform back = root != null
            ? root.Find("btnBACKTOMAIN")
            : null;
        if (banner == null || !banner.HasDesignerLayout ||
            !banner.HasRequiredReferences)
        {
            error = "모집 배너 디자이너 UI 참조가 누락되었습니다.";
            return false;
        }
        if (reveal == null || !reveal.HasDesignerLayout ||
            !reveal.HasRequiredReferences)
        {
            error = "모집 결과창 디자이너 UI 참조가 누락되었습니다.";
            return false;
        }
        if (back == null || back.GetComponent<Button>() == null ||
            back.Find("txtLabel")?.GetComponent<TextMeshProUGUI>() == null)
        {
            error = "모집 페이지 뒤로가기 버튼 참조가 누락되었습니다.";
            return false;
        }
        return true;
    }

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

        _recruitEditorSyncInProgress = true;
        try
        {
            Init();
            if (_recruitBannerView == null ||
                _recruitRevealOverlay == null)
            {
                BuildRecruitBrowser();
            }
            BindRecruitBackButton();
        }
        catch (Exception exception)
        {
            error =
                "모집 배너 또는 결과창 UI 재바인딩에 실패했습니다.\n" +
                exception.Message;
            return false;
        }
        finally
        {
            _recruitEditorSyncInProgress = false;
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
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene);
        return true;
    }
#endif

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
                !MatchesRosterRoleFilter(data) ||
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
            CharacterGradeStyle gradeStyle =
                CharacterGradePresentation.GetStyle(data.Grade);
            items.Add(new OperatorRosterItemModel(
                data.CharacterId,
                CharacterLocalization.GetName(data),
                data.StandingSprite != null
                    ? data.StandingSprite
                    : data.IconSprite,
                data.Role?.IconSprite,
                data.Grade,
                gradeStyle.BackgroundColor,
                gradeStyle.PrimaryColor,
                gradeStyle.OutlineColor,
                gradeStyle.TextColor));
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
            _rosterDescending
                    ? (korean ? "내림차순" : "DESCENDING")
                    : (korean ? "오름차순" : "ASCENDING"),
            _rosterSortCriterion,
            korean ? "이름" : "NAME",
            korean ? "희귀도" : "RARITY",
            korean ? "신뢰도" : "TRUST",
            korean ? "직군" : "ROLE",
                    korean ? "접기 <" : "HIDE <",
                    korean ? "펼치기 >" : "SHOW >",
            korean ? "전체" : "ALL",
            LocalizationService.Get(LocalizationKeys.UiRosterEmpty));
        _rosterBrowser.SetRoleFilters(
            CharacterRolePresentation.Roles,
            _rosterRoleFilter);
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
            string gradeLabel = CharacterGradePresentation.GetLabel(
                (CharacterGrade)grade);
            summaries.Add(
                korean
                    ? $"{gradeLabel}×{gradeCounts[grade]}"
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
                   _rosterSearchQuery) ||
               ContainsIgnoreCase(
                   CharacterRolePresentation.GetRoleName(data.Role),
                   _rosterSearchQuery) ||
               ContainsIgnoreCase(
                   CharacterRolePresentation.GetArchetypeName(
                       data.Archetype),
                   _rosterSearchQuery);
    }

    private bool MatchesRosterRoleFilter(CharacterData data)
    {
        return _rosterRoleFilter == null ||
               data?.Role == _rosterRoleFilter;
    }

    private int CompareRosterEntries(
        CharacterData left,
        CharacterData right)
    {
        int comparison = _rosterSortCriterion switch
        {
            OperatorRosterSortCriterion.Rarity =>
                left.Grade.CompareTo(right.Grade),
            OperatorRosterSortCriterion.Trust =>
                left.Trust.CompareTo(right.Trust),
            OperatorRosterSortCriterion.Role => string.Compare(
                CharacterRolePresentation.GetRoleName(left.Role),
                CharacterRolePresentation.GetRoleName(right.Role),
                StringComparison.OrdinalIgnoreCase),
            _ => string.Compare(
                CharacterLocalization.GetName(left),
                CharacterLocalization.GetName(right),
                StringComparison.OrdinalIgnoreCase)
        };
        if (_rosterDescending)
            comparison = -comparison;
        if (comparison != 0)
            return comparison;
        comparison = string.Compare(
            CharacterLocalization.GetName(left),
            CharacterLocalization.GetName(right),
            StringComparison.OrdinalIgnoreCase);
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
    public Sprite RoleIcon { get; }
    public CharacterGrade Grade { get; }
    public Color CardColor { get; }
    public Color AccentColor { get; }
    public Color OutlineColor { get; }
    public Color TextColor { get; }

    public OperatorRosterItemModel(
        string id,
        string displayName,
        Sprite portrait,
        Sprite roleIcon,
        CharacterGrade grade,
        Color cardColor,
        Color accentColor,
        Color outlineColor,
        Color textColor)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Portrait = portrait;
        RoleIcon = roleIcon;
        Grade = CharacterGradePresentation.Clamp(grade);
        CardColor = cardColor;
        AccentColor = accentColor;
        OutlineColor = outlineColor;
        TextColor = textColor;
    }
}

public sealed class OperatorRosterView
{
    private const string RootName = "grpOperatorRoster";
    private const string CardPrefix = "btnOperatorCard_";

    private sealed class CardView
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Image Portrait { get; }
        public TextMeshProUGUI PortraitFallback { get; }
        public TextMeshProUGUI Name { get; }
        public GameObject RoleRoot { get; }
        public Image RoleIcon { get; }
        public CharacterGradeIconStrip GradeIcons { get; }
        public Image Accent { get; }
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
            GameObject roleRoot,
            Image roleIcon,
            CharacterGradeIconStrip gradeIcons,
            Image accent,
            Outline selectionOutline,
            Button button,
            OperatorRosterCardHighlight highlight)
        {
            Root = root;
            Background = background;
            Portrait = portrait;
            PortraitFallback = portraitFallback;
            Name = name;
            RoleRoot = roleRoot;
            RoleIcon = roleIcon;
            GradeIcons = gradeIcons;
            Accent = accent;
            SelectionOutline = selectionOutline;
            Button = button;
            Highlight = highlight;
        }
    }

    private sealed class RoleFilterView
    {
        public Button Button { get; }
        public Image Background { get; }
        public Graphic Content { get; }
        public CharacterRoleSO Role { get; set; }

        public RoleFilterView(
            Button button,
            Image background,
            Graphic content)
        {
            Button = button;
            Background = background;
            Content = content;
        }
    }

    private readonly Transform _host;
    private readonly List<CardView> _cards = new();
    private readonly Button[] _sortCriterionButtons = new Button[4];
    private readonly TextMeshProUGUI[] _sortCriterionLabels =
        new TextMeshProUGUI[4];
    private readonly string[] _sortCriterionNames = new string[4];
    private readonly List<RoleFilterView> _roleFilters = new();
    private RectTransform _root;
    private TMP_InputField _searchInput;
    private TextMeshProUGUI _searchPlaceholder;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _countText;
    private TextMeshProUGUI _sortButtonLabel;
    private TextMeshProUGUI _allFilterLabel;
    private TextMeshProUGUI _railToggleLabel;
    private TextMeshProUGUI _emptyText;
    private Button _searchButton;
    private Button _sortButton;
    private Button _railToggleButton;
    private Button _allFilterButton;
    private Image _railBackground;
    private Transform _cardContent;
    private GameObject _cardPrefab;
    private OperatorRosterDesignerSettings _designerSettings;
    private OperatorRosterSortCriterion _activeSortCriterion;
    private CharacterRoleSO _activeRoleFilter;
    private bool _sortMenuExpanded;
    private bool _railExpanded = true;
    private string _railExpandedLabel = string.Empty;
    private string _railCollapsedLabel = string.Empty;
    private Action<string> _searchRequested;
    private Action<OperatorRosterSortCriterion> _sortCriterionRequested;
    private Action<CharacterRoleSO> _roleFilterRequested;
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
            throw new InvalidOperationException(
                "The saved operator roster UI is incomplete. Repair the " +
                "Scene hierarchy and inspector references.");
        }

        view.BindResponsiveGrid();
        view._root.gameObject.SetActive(true);
        return view;
    }

    public void SetCallbacks(
        Action<string> searchRequested,
        Action<OperatorRosterSortCriterion> sortCriterionRequested,
        Action<CharacterRoleSO> roleFilterRequested,
        Action sortRequested,
        Action<string> itemSelected)
    {
        _searchRequested = searchRequested;
        _sortCriterionRequested = sortCriterionRequested;
        _roleFilterRequested = roleFilterRequested;
        _sortRequested = sortRequested;
        _itemSelected = itemSelected;

        _searchButton.onClick.RemoveAllListeners();
        _searchButton.onClick.AddListener(SubmitSearch);
        _sortButton.onClick.RemoveAllListeners();
        _sortButton.onClick.AddListener(
            () => _sortRequested?.Invoke());
        for (int index = 0;
             index < _sortCriterionButtons.Length;
             index++)
        {
            int criterionIndex = index;
            Button button = _sortCriterionButtons[index];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(
                () => HandleSortCriterionClicked(
                    (OperatorRosterSortCriterion)criterionIndex));
        }
        _allFilterButton.onClick.RemoveAllListeners();
        _allFilterButton.onClick.AddListener(
            () => SelectRoleFilter(null));
        for (int index = 1; index < _roleFilters.Count; index++)
        {
            RoleFilterView filter = _roleFilters[index];
            filter.Button.onClick.RemoveAllListeners();
            filter.Button.onClick.AddListener(
                () => SelectRoleFilter(filter.Role));
        }
        _railToggleButton.onClick.RemoveAllListeners();
        _railToggleButton.onClick.AddListener(ToggleRoleFilterRail);
        _searchInput.onSubmit.RemoveAllListeners();
        _searchInput.onSubmit.AddListener(_ => SubmitSearch());
    }

    public void SetHeader(
        string title,
        string count,
        string searchText,
        string searchPlaceholder,
        string searchLabel,
        string sortDirectionLabel,
        OperatorRosterSortCriterion sortCriterion,
        string nameSortLabel,
        string raritySortLabel,
        string trustSortLabel,
        string roleSortLabel,
        string railExpandedLabel,
        string railCollapsedLabel,
        string allFilterLabel,
        string emptyLabel)
    {
        _titleText.text = title ?? string.Empty;
        _countText.text = count ?? string.Empty;
        _searchInput.SetTextWithoutNotify(searchText ?? string.Empty);
        _searchPlaceholder.text = searchPlaceholder ?? string.Empty;
        SetButtonLabel(_searchButton, searchLabel);
        _sortButtonLabel.text = sortDirectionLabel ?? string.Empty;
        _activeSortCriterion = sortCriterion;
        _sortCriterionNames[(int)OperatorRosterSortCriterion.Name] =
            nameSortLabel ?? string.Empty;
        _sortCriterionNames[(int)OperatorRosterSortCriterion.Rarity] =
            raritySortLabel ?? string.Empty;
        _sortCriterionNames[(int)OperatorRosterSortCriterion.Trust] =
            trustSortLabel ?? string.Empty;
        _sortCriterionNames[(int)OperatorRosterSortCriterion.Role] =
            roleSortLabel ?? string.Empty;
        _railExpandedLabel = railExpandedLabel ?? string.Empty;
        _railCollapsedLabel = railCollapsedLabel ?? string.Empty;
        _allFilterLabel.text = allFilterLabel ?? string.Empty;
        _emptyText.text = emptyLabel ?? string.Empty;
        RefreshSortMenu();
        RefreshRoleFilterRail();
    }

    public void SetRoleFilters(
        IReadOnlyList<CharacterRoleSO> roles,
        CharacterRoleSO selectedRole)
    {
        _activeRoleFilter = selectedRole;
        int roleCount = roles?.Count ?? 0;
        for (int index = 1; index < _roleFilters.Count; index++)
        {
            RoleFilterView filter = _roleFilters[index];
            int roleIndex = index - 1;
            CharacterRoleSO role = roleIndex < roleCount
                ? roles[roleIndex]
                : null;
            filter.Role = role;
            filter.Button.gameObject.SetActive(
                _railExpanded && role != null);
            if (filter.Content is Image roleIcon)
            {
                roleIcon.sprite = role?.IconSprite;
                roleIcon.enabled = role?.IconSprite != null;
            }
        }

        RefreshRoleFilterSelection();
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
            card.Root.transform.SetSiblingIndex(cardCount);
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

    private void HandleSortCriterionClicked(
        OperatorRosterSortCriterion criterion)
    {
        if (!_sortMenuExpanded && criterion == _activeSortCriterion)
        {
            _sortMenuExpanded = true;
            RefreshSortMenu();
            return;
        }

        _sortMenuExpanded = false;
        if (criterion != _activeSortCriterion)
        {
            _activeSortCriterion = criterion;
            _sortCriterionRequested?.Invoke(criterion);
        }
        RefreshSortMenu();
    }

    private void RefreshSortMenu()
    {
        for (int index = 0;
             index < _sortCriterionButtons.Length;
             index++)
        {
            bool selected = index == (int)_activeSortCriterion;
            _sortCriterionButtons[index].gameObject.SetActive(
                _sortMenuExpanded || selected);
            _sortCriterionLabels[index].text =
                _sortCriterionNames[index] +
                    (!_sortMenuExpanded && selected ? "  v" : string.Empty);
            if (_designerSettings != null &&
                _sortCriterionButtons[index].targetGraphic is Image
                    background)
            {
                background.color = selected
                    ? _designerSettings.SelectedFilterBackground
                    : _designerSettings.UnselectedFilterBackground;
                _sortCriterionLabels[index].color = selected
                    ? _designerSettings.SelectedFilterContent
                    : _designerSettings.UnselectedFilterContent;
            }
        }
    }

    private void SelectRoleFilter(CharacterRoleSO role)
    {
        if (_activeRoleFilter == role)
            return;

        _activeRoleFilter = role;
        RefreshRoleFilterSelection();
        _roleFilterRequested?.Invoke(role);
    }

    private void ToggleRoleFilterRail()
    {
        _railExpanded = !_railExpanded;
        RefreshRoleFilterRail();
    }

    private void RefreshRoleFilterRail()
    {
        if (_railBackground != null)
            _railBackground.enabled = _railExpanded;
        _railToggleLabel.text = _railExpanded
            ? _railExpandedLabel
            : _railCollapsedLabel;

        for (int index = 0; index < _roleFilters.Count; index++)
        {
            RoleFilterView filter = _roleFilters[index];
            bool hasRole = index == 0 || filter.Role != null;
            filter.Button.gameObject.SetActive(
                _railExpanded && hasRole);
        }
        RefreshRoleFilterSelection();
    }

    private void RefreshRoleFilterSelection()
    {
        if (_designerSettings == null)
            return;

        for (int index = 0; index < _roleFilters.Count; index++)
        {
            RoleFilterView filter = _roleFilters[index];
            bool selected = index == 0
                ? _activeRoleFilter == null
                : filter.Role == _activeRoleFilter;
            filter.Background.color = selected
                ? _designerSettings.SelectedFilterBackground
                : _designerSettings.UnselectedFilterBackground;
            filter.Content.color = selected
                ? _designerSettings.SelectedFilterContent
                : _designerSettings.UnselectedFilterContent;
        }
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
        Transform sortDropdown = header?.Find("grpRosterSortDropdown");
        Transform sortButton = header?.Find("btnRosterSortDirection");
        Transform rail = root?.Find("grpRosterFilterRail");
        Transform allFilter = rail?.Find("btnRosterFilterAll");
        Transform railToggle = rail?.Find("btnRosterFilterToggle");
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
        BindSortCriterion(
            sortDropdown,
            "btnRosterSortName",
            OperatorRosterSortCriterion.Name);
        BindSortCriterion(
            sortDropdown,
            "btnRosterSortRarity",
            OperatorRosterSortCriterion.Rarity);
        BindSortCriterion(
            sortDropdown,
            "btnRosterSortTrust",
            OperatorRosterSortCriterion.Trust);
        BindSortCriterion(
            sortDropdown,
            "btnRosterSortRole",
            OperatorRosterSortCriterion.Role);
        _sortButton = sortButton?.GetComponent<Button>();
        _sortButtonLabel = sortButton?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _allFilterButton = allFilter?.GetComponent<Button>();
        _allFilterLabel = allFilter?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _railToggleButton = railToggle?.GetComponent<Button>();
        _railToggleLabel = railToggle?.GetComponent<TextMeshProUGUI>();
        _railBackground = rail?.GetComponent<Image>();
        _emptyText = root?.Find("txtRosterEmpty")
            ?.GetComponent<TextMeshProUGUI>();
        _cardContent = content;
        _designerSettings = root
            ?.GetComponent<OperatorRosterDesignerSettings>();
        _cardPrefab = _designerSettings?.CardPrefab;

        _roleFilters.Clear();
        AddRoleFilter(allFilter, _allFilterLabel);
        for (int index = 0; index < 5; index++)
        {
            Transform roleFilter = rail?.Find(
                $"btnRosterFilterRole{index}");
            AddRoleFilter(
                roleFilter,
                roleFilter?.GetComponent<Image>());
        }

        return _root != null &&
               _titleText != null &&
               _countText != null &&
               _searchInput != null &&
               _searchPlaceholder != null &&
               _searchButton != null &&
               Array.TrueForAll(
                   _sortCriterionButtons,
                   button => button != null) &&
               Array.TrueForAll(
                   _sortCriterionLabels,
                   label => label != null) &&
               _sortButton != null &&
               _sortButtonLabel != null &&
               _allFilterButton != null &&
               _allFilterLabel != null &&
               _railToggleButton != null &&
               _railToggleLabel != null &&
               _railBackground != null &&
               _roleFilters.Count == 6 &&
               _emptyText != null &&
               _cardContent != null &&
               _cardPrefab != null;
    }

    private void BindSortCriterion(
        Transform parent,
        string objectName,
        OperatorRosterSortCriterion criterion)
    {
        Transform item = parent?.Find(objectName);
        int index = (int)criterion;
        _sortCriterionButtons[index] = item?.GetComponent<Button>();
        if (_sortCriterionButtons[index] != null)
        {
            _sortCriterionButtons[index].transition =
                Selectable.Transition.None;
        }
        _sortCriterionLabels[index] = item?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
    }

    private void AddRoleFilter(
        Transform root,
        Graphic content)
    {
        Button button = root?.GetComponent<Button>();
        Image background = root?.GetComponent<Image>();
        if (button == null || background == null || content == null)
            return;
        button.transition = Selectable.Transition.None;
        _roleFilters.Add(new RoleFilterView(
            button,
            background,
            content));
    }

    private CardView GetOrCreateCard(int index)
    {
        while (_cards.Count <= index)
        {
            int cardIndex = _cards.Count;
            Transform authored = _cardContent.Find(
                CardPrefix + cardIndex);
            GameObject instance = authored != null
                ? authored.gameObject
                : UnityEngine.Object.Instantiate(
                    _cardPrefab,
                    _cardContent,
                    false);
            instance.name = CardPrefix + cardIndex;
            _cards.Add(BindExistingCard(instance));
        }

        return _cards[index];
    }

    private CardView BindExistingCard(GameObject cardObject)
    {
        Image background = cardObject.GetComponent<Image>();
        Outline outline = cardObject.GetComponent<Outline>();
        Transform namePlate =
            cardObject.transform.Find("imgOperatorNamePlate");
        Transform roleRoot = namePlate?.Find("grpOperatorRole");
        Image roleIcon = roleRoot?.Find("imgOperatorRoleIcon")
            ?.GetComponent<Image>();
        OperatorRosterCardHighlight highlight =
            cardObject.GetComponent<OperatorRosterCardHighlight>();
        Button button = cardObject.GetComponent<Button>();
        if (background == null || outline == null ||
            namePlate == null || roleRoot == null || roleIcon == null ||
            highlight == null || button == null)
        {
            throw new InvalidOperationException(
                "Operator roster card prefab is incomplete: " +
                $"Image={background != null}, Outline={outline != null}, " +
                $"NamePlate={namePlate != null}, Role={roleRoot != null}, " +
                $"RoleIcon={roleIcon != null}, Highlight={highlight != null}, " +
                $"Button={button != null}.");
        }
        highlight.Configure(background, outline);

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
            roleRoot.gameObject,
            roleIcon,
            CharacterGradeIconStrip.Bind(
                cardObject.transform,
                "grpOperatorGradeIcons",
                14f,
                3f),
            cardObject.transform
                .Find("imgOperatorNamePlate/imgOperatorAccent")
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
        card.PortraitFallback.color = item.AccentColor;
        card.Name.color = item.TextColor;
        card.GradeIcons.SetGrade(item.Grade);
        card.RoleRoot.SetActive(item.RoleIcon != null);
        card.RoleIcon.sprite = item.RoleIcon;
        if (card.Accent != null)
            card.Accent.color = item.AccentColor;
        card.SelectionOutline.effectColor = item.OutlineColor;
        card.Highlight.SetPalette(
            item.CardColor,
            item.AccentColor,
            item.OutlineColor);
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

    private void BindResponsiveGrid()
    {
        GridLayoutGroup grid = _cardContent != null
            ? _cardContent.GetComponent<GridLayoutGroup>()
            : null;
        if (grid == null)
            return;

        ResponsiveGridConstraint.Bind(
            grid,
            grid.transform.parent as RectTransform);
    }

}
