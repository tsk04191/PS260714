using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StageSelectPage : RuntimeMenuPageBase, IPageLoadingTarget
{
    private enum EDungeonSelectView
    {
        Categories = 0,
        Dungeons = 1,
    }

    private const string CategoryTitleKey = LocalizationKeys.UiDungeonSelectTitle;
    private const string CategoryDescriptionKey =
        LocalizationKeys.UiDungeonSelectDescription;
    private const string EnterKey = LocalizationKeys.UiDungeonSelectEnter;
    private const string ClearedKey = LocalizationKeys.UiDungeonSelectCleared;
    private const string NotClearedKey =
        LocalizationKeys.UiDungeonSelectNotCleared;
    private const string NoProgressKey =
        LocalizationKeys.UiDungeonSelectNoProgress;
    private const string BattleCountKey =
        LocalizationKeys.UiDungeonSelectBattleCount;
    private const string PracticeRulesKey =
        LocalizationKeys.UiDungeonSelectPracticeRules;
    private const string StandardRulesKey =
        LocalizationKeys.UiDungeonSelectStandardRules;

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject dungeonPage;

    [Header("Shared Presentation")]
    [SerializeField] private UiMaskedCoverImageView backdropView;
    [SerializeField] private TextMeshProUGUI breadcrumbText;

    [Header("Category Browser")]
    [SerializeField] private GameObject categoryView;
    [SerializeField] private ScrollRect categoryScroll;
    [SerializeField] private RectTransform categoryContent;
    [SerializeField] private DungeonSelectCategoryCardView categoryCardPrefab;

    [Header("Dungeon Browser")]
    [SerializeField] private GameObject dungeonView;
    [SerializeField] private TextMeshProUGUI categoryTitleText;
    [SerializeField] private ScrollRect dungeonScroll;
    [SerializeField] private RectTransform dungeonContent;
    [SerializeField] private DungeonSelectDungeonRowView dungeonRowPrefab;
    [SerializeField] private UiMaskedCoverImageView detailHeroView;
    [SerializeField] private TextMeshProUGUI detailCategoryText;
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailRulesText;
    [SerializeField] private TextMeshProUGUI detailProgressText;
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI enterButtonText;

    private readonly List<DungeonSelectCategoryCardView> _categoryCards =
        new();
    private readonly List<DungeonSelectDungeonRowView> _dungeonRows = new();
    private EDungeonSelectView _view;
    private DungeonCategorySO _selectedCategory;
    private DungeonDefinition _selectedDungeon;
    private bool _built;

    protected override string PageTitle => "DUNGEON SELECT";
    protected override string PageDescription => "SELECT A CATEGORY";
    protected override string PageTitleLocalizationKey => CategoryTitleKey;
    protected override string PageDescriptionLocalizationKey =>
        CategoryDescriptionKey;

    public bool RequestMainMenuBgm()
    {
        if (mainPage == null ||
            !mainPage.TryGetComponent(out PageBgmSelection selection))
        {
            return false;
        }

        return selection.RequestSelectedBgm();
    }

    public bool RequiresLoading(PageOpenMode mode)
    {
        return !IsInitialized || !_built;
    }

    public override void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        base.Open(mode);
        if (!_built)
            return;

        if (mode == PageOpenMode.Fresh || _selectedCategory == null)
        {
            ShowCategoryBrowser(true);
        }
        else if (_view == EDungeonSelectView.Dungeons)
        {
            OpenCategory(_selectedCategory, true);
        }
        else
        {
            ShowCategoryBrowser(true);
        }
    }

    protected override void BuildButtons()
    {
        if (!TryValidateDesignerReferences(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        BindLocalizedOverlayMenuButton(
            "btnBACK",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        enterButton.onClick.RemoveListener(HandleEnterClicked);
        enterButton.onClick.AddListener(HandleEnterClicked);
        BuildCategoryCards();
        _built = true;
        ShowCategoryBrowser(false);
    }

    protected override void OnLocalizationChanged()
    {
        for (int index = 0; index < _categoryCards.Count; index++)
            _categoryCards[index]?.RefreshContent();
        RefreshDungeonRows();
        if (_view == EDungeonSelectView.Dungeons)
        {
            RefreshSelectedCategoryHeader();
            RefreshDungeonDetail();
        }
        else
        {
            PreviewCategory(_selectedCategory ?? ResolveFirstCategory());
        }
        RefreshFixedLocalizedText();
    }

    protected override void OnDestroy()
    {
        if (enterButton != null)
            enterButton.onClick.RemoveListener(HandleEnterClicked);
        base.OnDestroy();
    }

    public bool TryValidateDesignerReferences(out string error)
    {
        List<string> missing = new();
        if (backdropView == null || !backdropView.HasDesignerReferences)
            missing.Add(nameof(backdropView));
        AddMissing(missing, breadcrumbText, nameof(breadcrumbText));
        AddMissing(missing, categoryView, nameof(categoryView));
        AddMissing(missing, categoryScroll, nameof(categoryScroll));
        AddMissing(missing, categoryContent, nameof(categoryContent));
        if (categoryCardPrefab == null ||
            !categoryCardPrefab.HasDesignerReferences)
        {
            missing.Add(nameof(categoryCardPrefab));
        }
        AddMissing(missing, dungeonView, nameof(dungeonView));
        AddMissing(missing, categoryTitleText, nameof(categoryTitleText));
        AddMissing(missing, dungeonScroll, nameof(dungeonScroll));
        AddMissing(missing, dungeonContent, nameof(dungeonContent));
        if (dungeonRowPrefab == null || !dungeonRowPrefab.HasDesignerReferences)
            missing.Add(nameof(dungeonRowPrefab));
        if (detailHeroView == null || !detailHeroView.HasDesignerReferences)
            missing.Add(nameof(detailHeroView));
        AddMissing(missing, detailCategoryText, nameof(detailCategoryText));
        AddMissing(missing, detailTitleText, nameof(detailTitleText));
        AddMissing(
            missing,
            detailDescriptionText,
            nameof(detailDescriptionText));
        AddMissing(missing, detailRulesText, nameof(detailRulesText));
        AddMissing(missing, detailProgressText, nameof(detailProgressText));
        AddMissing(missing, enterButton, nameof(enterButton));
        AddMissing(missing, enterButtonText, nameof(enterButtonText));

        error = missing.Count == 0
            ? string.Empty
            : $"{name}: Dungeon Select designer references are " +
              "incomplete: " + string.Join(", ", missing) + ".";
        return missing.Count == 0;
    }

    private void BuildCategoryCards()
    {
        ClearViews(_categoryCards);
        IReadOnlyList<DungeonCategorySO> categories =
            DungeonCategoryCatalog.GetVisible();
        for (int index = 0; index < categories.Count; index++)
        {
            DungeonSelectCategoryCardView card = Instantiate(
                categoryCardPrefab,
                categoryContent,
                false);
            card.gameObject.name = "btnDungeonCategory_" +
                                   categories[index].CategoryId;
            card.Configure(
                categories[index],
                PreviewCategory,
                category => OpenCategory(category, false));
            _categoryCards.Add(card);
        }

        DungeonCategorySO initial = _selectedCategory;
        if (initial == null && categories.Count > 0)
            initial = categories[0];
        PreviewCategory(initial);
    }

    private void ShowCategoryBrowser(bool focusSelection)
    {
        _view = EDungeonSelectView.Categories;
        categoryView.SetActive(true);
        dungeonView.SetActive(false);
        RefreshFixedLocalizedText();
        PreviewCategory(_selectedCategory ?? ResolveFirstCategory());
        if (focusSelection)
        {
            DungeonSelectCategoryCardView card = FindCategoryCard(
                _selectedCategory);
            FocusButton(card != null ? card.Button : null);
        }
    }

    private void PreviewCategory(DungeonCategorySO category)
    {
        if (category == null)
            return;
        _selectedCategory = category;
        for (int index = 0; index < _categoryCards.Count; index++)
        {
            DungeonSelectCategoryCardView card = _categoryCards[index];
            card?.SetSelected(ReferenceEquals(card.Category, category));
        }
        SetBackdrop(category.BackdropSprite, category.BackdropFraming);
        if (breadcrumbText != null)
            breadcrumbText.text = ResolveText(
                CategoryTitleKey,
                "DUNGEON SELECT");
    }

    private void OpenCategory(
        DungeonCategorySO category,
        bool preserveDungeon)
    {
        if (category == null)
            return;
        IReadOnlyList<DungeonDefinition> dungeons =
            category.ResolveDungeons();
        if (dungeons.Count == 0)
            return;

        _selectedCategory = category;
        _view = EDungeonSelectView.Dungeons;
        categoryView.SetActive(false);
        dungeonView.SetActive(true);
        BuildDungeonRows(dungeons);
        RefreshSelectedCategoryHeader();
        DungeonDefinition target = preserveDungeon &&
                                   ContainsDungeon(dungeons, _selectedDungeon)
            ? _selectedDungeon
            : dungeons[0];
        SelectDungeon(target, true);
    }

    private void BuildDungeonRows(
        IReadOnlyList<DungeonDefinition> dungeons)
    {
        ClearViews(_dungeonRows);
        for (int index = 0; index < dungeons.Count; index++)
        {
            DungeonDefinition definition = dungeons[index];
            DungeonSelectDungeonRowView row = Instantiate(
                dungeonRowPrefab,
                dungeonContent,
                false);
            row.gameObject.name = "btnDungeon_" + definition.DungeonId;
            row.Configure(
                definition,
                index,
                ResolveProgressState(definition),
                selected => SelectDungeon(selected, false));
            _dungeonRows.Add(row);
        }
    }

    private void SelectDungeon(DungeonDefinition definition, bool focus)
    {
        if (definition == null)
            return;
        _selectedDungeon = definition;
        for (int index = 0; index < _dungeonRows.Count; index++)
        {
            DungeonSelectDungeonRowView row = _dungeonRows[index];
            row?.SetSelected(ReferenceEquals(row.Definition, definition));
        }
        SetBackdrop(
            definition.StageBackdropSprite,
            definition.StageBackdropFraming);
        RefreshDungeonDetail();
        if (focus)
        {
            DungeonSelectDungeonRowView row = FindDungeonRow(definition);
            FocusButton(row != null ? row.Button : null);
        }
    }

    private void RefreshSelectedCategoryHeader()
    {
        if (_selectedCategory == null)
            return;
        string title = ResolveText(
            _selectedCategory.TitleLocalizationKey,
            _selectedCategory.FallbackTitle);
        if (categoryTitleText != null)
            categoryTitleText.text = title;
        if (breadcrumbText != null)
        {
            breadcrumbText.text = ResolveText(
                CategoryTitleKey,
                "DUNGEON SELECT") + " / " + title.ToUpperInvariant();
        }
    }

    private void RefreshDungeonDetail()
    {
        if (_selectedDungeon == null)
            return;
        detailHeroView?.Configure(
            _selectedDungeon.StageCoverSprite,
            _selectedDungeon.StageCoverFraming);
        if (detailCategoryText != null && _selectedCategory != null)
        {
            detailCategoryText.text = ResolveText(
                _selectedCategory.TitleLocalizationKey,
                _selectedCategory.FallbackTitle);
        }
        if (detailTitleText != null)
        {
            detailTitleText.text = ResolveText(
                _selectedDungeon.TitleLocalizationKey,
                _selectedDungeon.FallbackTitle);
        }
        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = ResolveText(
                _selectedDungeon.DescriptionLocalizationKey,
                _selectedDungeon.FallbackDescription);
        }
        if (detailRulesText != null)
            detailRulesText.text = ResolveRules(_selectedDungeon);
        if (detailProgressText != null)
            detailProgressText.text = ResolveProgressState(_selectedDungeon);
        RefreshFixedLocalizedText();
    }

    private void RefreshDungeonRows()
    {
        for (int index = 0; index < _dungeonRows.Count; index++)
        {
            DungeonSelectDungeonRowView row = _dungeonRows[index];
            if (row != null)
            {
                row.RefreshLocalizedContent(
                    ResolveProgressState(row.Definition));
            }
        }
    }

    private void RefreshFixedLocalizedText()
    {
        if (enterButtonText != null)
            enterButtonText.text = ResolveText(EnterKey, "ENTER DUNGEON");
    }

    private string ResolveRules(DungeonDefinition definition)
    {
        if (definition == null)
            return string.Empty;
        string battleCount = LocalizationService.Get(
            BattleCountKey,
            LocalizationService.Arg("minimum", definition.MinimumBattleCount),
            LocalizationService.Arg("maximum", definition.MaximumBattleCount));
        string rules = ResolveText(
            definition.IsPractice ? PracticeRulesKey : StandardRulesKey,
            definition.IsPractice
                ? "NO TIME LIMIT · NO REWARDS · NO PROGRESS SAVE"
                : "CHARACTER SELECTION · ITEMS · REWARDS");
        return battleCount + "\n" + rules;
    }

    private string ResolveProgressState(DungeonDefinition definition)
    {
        if (definition == null)
            return string.Empty;
        if (!definition.PersistsDungeonProgress)
            return ResolveText(NoProgressKey, "NO PROGRESS SAVE");

        DungeonProgressData progress =
            DataManager.Current?.DungeonProgressDatas;
        int clearCount = progress?.GetClearCount(definition.DungeonId) ?? 0;
        if (clearCount <= 0)
            return ResolveText(NotClearedKey, "NOT CLEARED");
        return LocalizationService.Get(
            ClearedKey,
            LocalizationService.Arg("count", clearCount));
    }

    private void HandleEnterClicked()
    {
        if (_selectedDungeon == null)
            return;
        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            dungeon.PrepareDungeon(_selectedDungeon);
        }
        NavigateTo(dungeonPage, PageOpenMode.Fresh);
    }

    private void HandleBackClicked()
    {
        if (_view == EDungeonSelectView.Dungeons)
        {
            ShowCategoryBrowser(true);
            return;
        }
        NavigateTo(mainPage, PageOpenMode.Resume);
    }

    private void SetBackdrop(Sprite sprite, UiArtworkFraming framing)
    {
        backdropView?.Configure(sprite, framing);
    }

    private DungeonCategorySO ResolveFirstCategory()
    {
        IReadOnlyList<DungeonCategorySO> categories =
            DungeonCategoryCatalog.GetVisible();
        return categories.Count > 0 ? categories[0] : null;
    }

    private DungeonSelectCategoryCardView FindCategoryCard(
        DungeonCategorySO category)
    {
        for (int index = 0; index < _categoryCards.Count; index++)
        {
            if (ReferenceEquals(_categoryCards[index]?.Category, category))
                return _categoryCards[index];
        }
        return null;
    }

    private DungeonSelectDungeonRowView FindDungeonRow(
        DungeonDefinition definition)
    {
        for (int index = 0; index < _dungeonRows.Count; index++)
        {
            if (ReferenceEquals(_dungeonRows[index]?.Definition, definition))
                return _dungeonRows[index];
        }
        return null;
    }

    private static void FocusButton(Button button)
    {
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static bool ContainsDungeon(
        IReadOnlyList<DungeonDefinition> dungeons,
        DungeonDefinition definition)
    {
        if (definition == null)
            return false;
        for (int index = 0; index < dungeons.Count; index++)
        {
            if (ReferenceEquals(dungeons[index], definition))
                return true;
        }
        return false;
    }

    private static string ResolveText(string key, string fallback)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               LocalizationService.TryGet(key, out string localized)
            ? localized
            : fallback ?? string.Empty;
    }

    private static void AddMissing(
        List<string> missing,
        UnityEngine.Object value,
        string fieldName)
    {
        if (value == null)
            missing.Add(fieldName);
    }

    private static void ClearViews<T>(List<T> views)
        where T : Component
    {
        for (int index = views.Count - 1; index >= 0; index--)
        {
            T view = views[index];
            if (view == null)
                continue;
            if (Application.isPlaying)
                Destroy(view.gameObject);
            else
                DestroyImmediate(view.gameObject);
        }
        views.Clear();
    }

#if UNITY_EDITOR
    public bool ValidateEditorUi(out string error)
    {
        if (Application.isPlaying)
        {
            error = "Dungeon Select UI cannot be validated in Play Mode.";
            return false;
        }
        return TryValidateDesignerReferences(out error);
    }

    public bool SyncEditorUi(out string error)
    {
        return ValidateEditorUi(out error);
    }
#endif
}
