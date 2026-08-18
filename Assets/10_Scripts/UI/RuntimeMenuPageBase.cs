using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class RuntimeMenuPageBase : MonoBehaviour, IPage
{
    public const string RuntimeRootObjectName = "grpRuntimeMenuPage";

    protected static readonly Color BackgroundColor =
        new(0.035f, 0.05f, 0.045f, 1f);
    protected static readonly Color PanelColor =
        new(0.075f, 0.095f, 0.08f, 0.98f);
    protected static readonly Color ButtonColor =
        new(0.19f, 0.28f, 0.22f, 1f);
    protected static readonly Color TextColor =
        new(0.94f, 0.91f, 0.78f, 1f);

    [Header("Designer-owned UI references")]
    [SerializeField] private RectTransform _runtimeRoot;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private RectTransform _buttonRoot;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField, HideInInspector] private int _designerLayoutVersion;

    private bool _initialized;
    private bool _localizationEventsBound;
    private ResponsivePanelFitter _designerPanelFitter;

    protected abstract string PageTitle { get; }
    protected virtual string PageDescription => string.Empty;
    protected virtual string PageTitleLocalizationKey => string.Empty;
    protected virtual string PageDescriptionLocalizationKey => string.Empty;
    protected RectTransform RuntimeRoot => _runtimeRoot;
    protected RectTransform PanelRoot => _panel;
    protected RectTransform ButtonRoot => _buttonRoot;
    protected bool IsInitialized => _initialized;
    public bool HasDesignerLayout => _designerLayoutVersion > 0;

    public AudioSource Speaker { get; set; }

    protected virtual void Awake()
    {
        BindLocalizationEvents();
        Init();
    }

    protected virtual void OnDestroy()
    {
        UnbindLocalizationEvents();
    }

    protected virtual void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshLayout();
    }

    public virtual void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        gameObject.SetActive(true);
        if (!_initialized)
            Init();
        RefreshPageLocalization();
        RefreshLayout();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Init()
    {
        if (_initialized)
            return;

        if (!BuildRuntimeUi())
            return;
        BuildButtons();
        _initialized = true;
        RefreshPageLocalization();
        RefreshLayout();
    }

    protected abstract void BuildButtons();

    protected virtual void OnLocalizationChanged()
    {
    }

    protected Button CreateLocalizedMenuButton(
        string stableName,
        string localizationKey,
        Action action)
    {
        if (_buttonRoot == null)
            return null;

        return CreateStyledButton(
            _buttonRoot,
            stableName,
            LocalizationService.Get(localizationKey),
            action,
            72f,
            localizationKey);
    }

    protected Button CreateStyledButton(
        Transform parent,
        string objectName,
        string label,
        Action action,
        float preferredHeight = 60f,
        string localizationKey = null)
    {
        if (parent == null)
            return null;

        Transform existingButton = parent.Find(objectName);
        Button button = existingButton != null
            ? existingButton.GetComponent<Button>()
            : null;
        TextMeshProUGUI text = existingButton != null
            ? existingButton.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (button == null || text == null)
        {
            Debug.LogError(
                $"{name}: required scene button '{objectName}' is " +
                "missing or incomplete.",
                this);
            return null;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(() => action());
        ApplyLocalizedText(text, localizationKey, label);
        return button;
    }

    protected TextMeshProUGUI CreateContentText(
        Transform parent,
        string objectName,
        string content,
        float fontSize,
        float preferredHeight,
        FontStyles fontStyle = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        if (parent == null)
            return null;

        TextMeshProUGUI text = parent.Find(objectName)
            ?.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            Debug.LogError(
                $"{name}: required scene text '{objectName}' is missing.",
                this);
            return null;
        }
        text.text = content;
        return text;
    }

    protected Button CreateLocalizedOverlayMenuButton(
        string stableName,
        string localizationKey,
        Action action)
    {
        return CreateOverlayMenuButtonCore(
            stableName,
            LocalizationService.Get(localizationKey),
            localizationKey,
            action);
    }

    protected Button CreateLocalizedTopLeftOverlayMenuButton(
        string stableName,
        string localizationKey,
        Action action)
    {
        return CreateOverlayMenuButtonCore(
            stableName,
            LocalizationService.Get(localizationKey),
            localizationKey,
            action,
            true);
    }

    protected Button BindLocalizedOverlayMenuButton(
        string stableName,
        string localizationKey,
        Action action)
    {
        if (_runtimeRoot == null)
            return null;

        Transform buttonTransform = _runtimeRoot.Find(stableName);
        Button button = buttonTransform != null
            ? buttonTransform.GetComponent<Button>()
            : null;
        TextMeshProUGUI label = buttonTransform != null
            ? buttonTransform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (button == null || label == null)
        {
            Debug.LogError(
                $"{name}: saved designer button '{stableName}' is " +
                "missing or has incomplete references.",
                this);
            return null;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(() => action());
        ApplyLocalizedText(
            label,
            localizationKey,
            LocalizationService.Get(localizationKey));
        return button;
    }

    private Button CreateOverlayMenuButtonCore(
        string objectName,
        string label,
        string localizationKey,
        Action action,
        bool topLeftCompact = false)
    {
        if (_runtimeRoot == null)
            return null;

        return BindLocalizedOverlayMenuButton(
            objectName,
            localizationKey,
            action);
    }

    protected void NavigateTo(
        GameObject targetPage,
        PageOpenMode mode = PageOpenMode.Fresh)
    {
        if (targetPage == null)
        {
            Debug.LogError(
                $"{GetType().Name} target page is not assigned.",
                this);
            return;
        }

        PageControl.PagToPag(gameObject, targetPage, mode);
    }

    private bool BuildRuntimeUi()
    {
        if (TryBindExistingUi())
        {
            RefreshPageLocalization();
            return true;
        }

        Debug.LogError(
            $"{name}: required scene UI references are missing. " +
            "Fixed UI is not created at runtime.",
            this);
        return false;
    }

    private bool TryBindExistingUi()
    {
        Transform existingRoot = _runtimeRoot;
        Transform existingPanel = _panel;
        Transform existingButtonRoot = _buttonRoot;
        if (existingRoot is not RectTransform rootRect ||
            existingPanel is not RectTransform panelRect ||
            existingButtonRoot is not RectTransform buttonRootRect)
        {
            return false;
        }

        _runtimeRoot = rootRect;
        _panel = panelRect;
        _buttonRoot = buttonRootRect;
        _designerPanelFitter =
            panelRect.GetComponent<ResponsivePanelFitter>();
        return _titleText != null;
    }

#if UNITY_EDITOR
    public void MarkDesignerLayoutCurrent()
    {
        _designerLayoutVersion = 1;
        UnityEditor.EditorUtility.SetDirty(this);
    }

#endif

    private void RefreshLayout()
    {
        if (_runtimeRoot == null || _panel == null)
            return;

        _designerPanelFitter?.RefreshLayout();
    }

    private void BindLocalizationEvents()
    {
        if (_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        _localizationEventsBound = true;
    }

    private void UnbindLocalizationEvents()
    {
        if (!_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
        _localizationEventsBound = false;
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        RefreshPageLocalization();
        if (isActiveAndEnabled)
            OnLocalizationChanged();
    }

    private void RefreshPageLocalization()
    {
        ApplyLocalizedText(
            _titleText,
            PageTitleLocalizationKey,
            PageTitle);
        ApplyLocalizedText(
            _descriptionText,
            PageDescriptionLocalizationKey,
            PageDescription);
    }

    private static void ApplyLocalizedText(
        TMP_Text text,
        string localizationKey,
        string fallbackText)
    {
        if (text == null)
            return;

        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            text.text = fallbackText ?? string.Empty;
            return;
        }

        text.text = LocalizationService.Get(localizationKey);
    }

    protected static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(
                   query,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    protected static bool IsKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;

}

public readonly struct CodexBrowserItemModel
{
    public string Id { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    public bool Dimmed { get; }
    public Color AccentColor { get; }

    public CodexBrowserItemModel(
        string id,
        string displayName,
        Sprite icon,
        bool dimmed,
        Color accentColor)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Icon = icon;
        Dimmed = dimmed;
        AccentColor = accentColor;
    }
}

public sealed class CodexBrowserView
{
    public const float CardHeightToWidthRatio = 1.4f;
    public static readonly Vector2 CardSize = new(
        160f,
        160f * CardHeightToWidthRatio);

    private sealed class CardView
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Image Icon { get; }
        public TextMeshProUGUI FallbackIcon { get; }
        public TextMeshProUGUI Name { get; }
        public Outline SelectionOutline { get; }
        public Button Button { get; }
        public string BoundId { get; set; }

        public CardView(
            GameObject root,
            Image background,
            Image icon,
            TextMeshProUGUI fallbackIcon,
            TextMeshProUGUI name,
            Outline selectionOutline,
            Button button)
        {
            Root = root;
            Background = background;
            Icon = icon;
            FallbackIcon = fallbackIcon;
            Name = name;
            SelectionOutline = selectionOutline;
            Button = button;
        }
    }

    private static readonly Color CardColor =
        new(0.16f, 0.235f, 0.19f, 1f);
    private static readonly Color CardTextColor =
        new(0.94f, 0.91f, 0.78f, 1f);

    private readonly Transform _host;
    private readonly List<CardView> _cards = new();
    private TMP_InputField _searchInput;
    private TextMeshProUGUI _searchPlaceholder;
    private Button _searchButton;
    private Button _filterButton;
    private Button _sortButton;
    private TextMeshProUGUI _searchButtonLabel;
    private TextMeshProUGUI _filterButtonLabel;
    private TextMeshProUGUI _sortButtonLabel;
    private Transform _cardContent;
    private Action<string> _searchRequested;
    private Action _filterRequested;
    private Action _sortRequested;
    private Action<string> _itemSelected;
    private CodexBrowserDesignerSettings _designerSettings;
    private Transform _browserRoot;
    private Transform _listRoot;

    public Transform DetailRoot { get; private set; }

    private CodexBrowserView(Transform host)
    {
        _host = host;
    }

    public static CodexBrowserView Build(Transform host)
    {
        CodexBrowserView view = new(host);
        if (!view.TryBindLayout())
        {
            Debug.LogError(
                "Codex browser fixed UI is missing. Author the browser " +
                "hierarchy in the Scene and assign its designer settings.",
                host);
            return view;
        }
        view.BindResponsiveGrid();
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
        _filterButton.onClick.RemoveAllListeners();
        _filterButton.onClick.AddListener(() => _filterRequested?.Invoke());
        _sortButton.onClick.RemoveAllListeners();
        _sortButton.onClick.AddListener(() => _sortRequested?.Invoke());
        _searchInput.onSubmit.RemoveAllListeners();
        _searchInput.onSubmit.AddListener(_ => SubmitSearch());
    }

    public void SetToolbar(
        string searchText,
        string searchPlaceholder,
        string searchLabel,
        string filterLabel,
        string sortLabel)
    {
        _searchInput.SetTextWithoutNotify(searchText ?? string.Empty);
        _searchPlaceholder.text = searchPlaceholder ?? string.Empty;
        _searchButtonLabel.text = searchLabel ?? string.Empty;
        _filterButtonLabel.text = filterLabel ?? string.Empty;
        _sortButtonLabel.text = sortLabel ?? string.Empty;
    }

    public void AdoptExistingDetail(string detailObjectName)
    {
        if (_host == null || DetailRoot == null ||
            string.IsNullOrWhiteSpace(detailObjectName))
        {
            return;
        }

        Transform existing = _host.Find(detailObjectName);
        if (existing != null && existing.parent != DetailRoot)
            existing.SetParent(DetailRoot, false);
    }

    public void HideLegacyList(string legacyObjectName)
    {
        if (_host == null || string.IsNullOrWhiteSpace(legacyObjectName))
            return;

        Transform legacy = _host.Find(legacyObjectName);
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    public void SetItems(
        IReadOnlyList<CodexBrowserItemModel> items,
        string selectedId)
    {
        int sourceCount = items?.Count ?? 0;
        int cardCount = 0;
        HashSet<string> registeredIds =
            new(StringComparer.OrdinalIgnoreCase);
        HashSet<GameObject> activeCardObjects = new();
        for (int index = 0; index < sourceCount; index++)
        {
            CodexBrowserItemModel item = items[index];
            string itemId = item.Id?.Trim();
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !registeredIds.Add(itemId))
            {
                continue;
            }

            CardView card = GetOrCreateCard(cardCount);
            BindCard(card, item, selectedId);
            card.Root.SetActive(true);
            activeCardObjects.Add(card.Root);
            cardCount++;
        }

        for (int index = cardCount; index < _cards.Count; index++)
            _cards[index].Root.SetActive(false);

        for (int index = 0; index < _cardContent.childCount; index++)
        {
            Transform child = _cardContent.GetChild(index);
            if (child == null ||
                !child.name.StartsWith(
                    "btnCodexCard_",
                    StringComparison.Ordinal))
            {
                continue;
            }

            // Designer previews can retain cards from an older, larger data
            // set or even contain duplicate numeric names. Only card objects
            // bound during this refresh may remain visible.
            child.gameObject.SetActive(
                activeCardObjects.Contains(child.gameObject));
        }
    }

    public void SetSelected(string selectedId)
    {
        for (int index = 0; index < _cards.Count; index++)
        {
            CardView card = _cards[index];
            if (!card.Root.activeSelf)
                continue;

            bool selected = string.Equals(
                card.BoundId,
                selectedId,
                StringComparison.Ordinal);
            card.SelectionOutline.enabled = selected;
        }
    }

    private void BindResponsiveGrid()
    {
        GridLayoutGroup grid = _cardContent != null
            ? _cardContent.GetComponent<GridLayoutGroup>()
            : null;
        if (grid == null)
            return;

        ResponsiveGridConstraint constraint =
            grid.GetComponent<ResponsiveGridConstraint>();
        if (constraint == null)
        {
            Debug.LogError(
                "Codex card grid requires a pre-authored " +
                "ResponsiveGridConstraint component.",
                grid);
            return;
        }

        constraint.RefreshLayout();
    }

    private bool TryBindLayout()
    {
        if (_host == null)
            return false;

        _designerSettings =
            _host.GetComponentInChildren<CodexBrowserDesignerSettings>(true);
        Transform browser = _designerSettings != null
            ? _designerSettings.transform
            : _host.Find("grpCodexBrowser");
        Transform list = browser != null
            ? _designerSettings != null &&
              _designerSettings.ListPanel != null
                ? _designerSettings.ListPanel
                : browser.Find("grpCodexList")
            : null;
        Transform toolbar = list != null
            ? _designerSettings != null &&
              _designerSettings.Toolbar != null
                ? _designerSettings.Toolbar
                : list.Find("grpCodexListToolbar")
            : null;
        Transform search = _designerSettings != null &&
                           _designerSettings.SearchInput != null
            ? _designerSettings.SearchInput.transform
            : toolbar != null
                ? toolbar.Find("inpCodexSearch")
                : null;
        Transform searchViewport = search != null
            ? search.Find("vptCodexSearch")
            : null;
        Transform searchButton = _designerSettings != null &&
                                 _designerSettings.SearchButton != null
            ? _designerSettings.SearchButton.transform
            : toolbar != null
                ? toolbar.Find("btnCodexSearch")
                : null;
        Transform filterButton = _designerSettings != null &&
                                 _designerSettings.FilterButton != null
            ? _designerSettings.FilterButton.transform
            : toolbar != null
                ? toolbar.Find("btnCodexFilter")
                : null;
        Transform sortButton = _designerSettings != null &&
                               _designerSettings.SortButton != null
            ? _designerSettings.SortButton.transform
            : toolbar != null
                ? toolbar.Find("btnCodexSort")
                : null;
        Transform cardContent = _designerSettings != null &&
                                _designerSettings.CardContent != null
            ? _designerSettings.CardContent
            : list != null
                ? list.Find(
                    "scrCodexList/vptCodexList/grpCodexCardContent")
                : null;
        Transform detail = _designerSettings != null &&
                           _designerSettings.DetailRoot != null
            ? _designerSettings.DetailRoot
            : browser != null
                ? browser.Find("grpCodexDetailHost")
                : null;

        _searchInput = search != null
            ? search.GetComponent<TMP_InputField>()
            : null;
        _browserRoot = browser;
        _listRoot = list;
        _searchPlaceholder = searchViewport != null
            ? searchViewport.Find("txtCodexSearchPlaceholder")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        _searchButton = searchButton != null
            ? searchButton.GetComponent<Button>()
            : null;
        _filterButton = filterButton != null
            ? filterButton.GetComponent<Button>()
            : null;
        _sortButton = sortButton != null
            ? sortButton.GetComponent<Button>()
            : null;
        _searchButtonLabel = searchButton != null
            ? searchButton.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        _filterButtonLabel = filterButton != null
            ? filterButton.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        _sortButtonLabel = sortButton != null
            ? sortButton.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        _cardContent = cardContent;
        DetailRoot = detail;
        _designerSettings ??= browser != null
            ? browser.GetComponent<CodexBrowserDesignerSettings>()
            : null;

        return _searchInput != null &&
               _searchPlaceholder != null &&
               _searchButton != null &&
               _filterButton != null &&
               _sortButton != null &&
               _searchButtonLabel != null &&
               _filterButtonLabel != null &&
               _sortButtonLabel != null &&
               _cardContent != null &&
               DetailRoot != null;
    }

    private CardView GetOrCreateCard(int index)
    {
        while (_cards.Count <= index)
        {
            int cardIndex = _cards.Count;
            if (TryBindExistingCard(cardIndex, out CardView existingCard))
            {
                _cards.Add(existingCard);
                continue;
            }

            GameObject template = _designerSettings != null
                ? _designerSettings.CardTemplate
                : null;
            if (template == null)
            {
                throw new InvalidOperationException(
                    "CodexBrowserDesignerSettings.CardTemplate must " +
                    "reference a prefab asset.");
            }

            GameObject cardObject = UnityEngine.Object.Instantiate(
                template,
                _cardContent,
                false);
            cardObject.name = $"btnCodexCard_{cardIndex}";
            cardObject.SetActive(true);
            Image background = cardObject.GetComponent<Image>();
            Button button = cardObject.GetComponent<Button>();
            Outline outline = cardObject.GetComponent<Outline>();

            Transform iconObject = cardObject.transform.Find(
                "imgCodexCardIcon");
            Image icon = iconObject != null
                ? iconObject.GetComponent<Image>()
                : null;
            TextMeshProUGUI fallbackIcon = iconObject != null
                ? iconObject.Find("txtCodexCardFallbackIcon")
                    ?.GetComponent<TextMeshProUGUI>()
                : null;
            TextMeshProUGUI name = cardObject.transform.Find(
                    "grpCodexCardNamePlate/txtCodexCardName")
                ?.GetComponent<TextMeshProUGUI>();
            if (background == null || button == null || outline == null ||
                icon == null || fallbackIcon == null || name == null)
            {
                UnityEngine.Object.Destroy(cardObject);
                throw new InvalidOperationException(
                    "The Codex card prefab is missing required UI " +
                    "components or named child references.");
            }

            CardView card = new(
                cardObject,
                background,
                icon,
                fallbackIcon,
                name,
                outline,
                button);
            _cards.Add(card);
        }

        return _cards[index];
    }

    private bool TryBindExistingCard(
        int index,
        out CardView card)
    {
        card = null;
        Transform root = _cardContent != null
            ? _cardContent.Find($"btnCodexCard_{index}")
            : null;
        Transform iconTransform = root != null
            ? root.Find("imgCodexCardIcon")
            : null;
        Transform fallbackTransform = iconTransform != null
            ? iconTransform.Find("txtCodexCardFallbackIcon")
            : null;
        Transform namePlate = root != null
            ? root.Find("grpCodexCardNamePlate")
            : null;
        Transform nameTransform = namePlate != null
            ? namePlate.Find("txtCodexCardName")
            : null;

        Image background = root != null
            ? root.GetComponent<Image>()
            : null;
        Image icon = iconTransform != null
            ? iconTransform.GetComponent<Image>()
            : null;
        TextMeshProUGUI fallback = fallbackTransform != null
            ? fallbackTransform.GetComponent<TextMeshProUGUI>()
            : null;
        TextMeshProUGUI name = nameTransform != null
            ? nameTransform.GetComponent<TextMeshProUGUI>()
            : null;
        Outline outline = root != null
            ? root.GetComponent<Outline>()
            : null;
        Button button = root != null
            ? root.GetComponent<Button>()
            : null;
        if (root == null || background == null || icon == null ||
            fallback == null || name == null || outline == null ||
            button == null)
        {
            return false;
        }

        card = new CardView(
            root.gameObject,
            background,
            icon,
            fallback,
            name,
            outline,
            button);
        return true;
    }

    private void BindCard(
        CardView card,
        CodexBrowserItemModel item,
        string selectedId)
    {
        card.BoundId = item.Id;
        card.Name.text = item.DisplayName;
        card.Icon.sprite = item.Icon;
        card.Icon.gameObject.SetActive(item.Icon != null);
        card.FallbackIcon.gameObject.SetActive(item.Icon == null);
        card.FallbackIcon.text = GetFallbackIcon(item.DisplayName);

        Color ownedCardColor = _designerSettings != null
            ? _designerSettings.OwnedCardColor
            : CardColor;
        Color ownedTextColor = _designerSettings != null
            ? _designerSettings.OwnedTextColor
            : CardTextColor;
        float unownedDarken = _designerSettings != null
            ? _designerSettings.UnownedDarken
            : 0.58f;
        Color unownedIconColor = _designerSettings != null
            ? _designerSettings.UnownedIconColor
            : new Color(0.34f, 0.34f, 0.34f, 1f);
        Color unownedTextColor = _designerSettings != null
            ? _designerSettings.UnownedTextColor
            : new Color(0.48f, 0.48f, 0.43f, 1f);
        Color normalColor = item.Dimmed
            ? Color.Lerp(ownedCardColor, Color.black, unownedDarken)
            : ownedCardColor;
        card.Background.color = normalColor;
        card.Icon.color = item.Dimmed
            ? unownedIconColor
            : Color.white;
        card.FallbackIcon.color = item.Dimmed
            ? new Color(0.38f, 0.4f, 0.36f, 1f)
            : Color.Lerp(ownedTextColor, item.AccentColor, 0.35f);
        card.Name.color = item.Dimmed
            ? unownedTextColor
            : ownedTextColor;
        card.SelectionOutline.effectColor = item.AccentColor;
        card.SelectionOutline.enabled = string.Equals(
            item.Id,
            selectedId,
            StringComparison.Ordinal);

        ColorBlock colors = card.Button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor =
            Color.Lerp(normalColor, Color.white, 0.16f);
        colors.pressedColor =
            Color.Lerp(normalColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = normalColor;
        colors.fadeDuration = 0.08f;
        card.Button.colors = colors;
        card.Button.interactable = true;
        card.Button.onClick.RemoveAllListeners();
        string selectedItemId = item.Id;
        card.Button.onClick.AddListener(
            () => _itemSelected?.Invoke(selectedItemId));
    }

    private void SubmitSearch()
    {
        _searchRequested?.Invoke(_searchInput.text ?? string.Empty);
    }

    private static string GetFallbackIcon(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        string trimmed = displayName.Trim();
        return trimmed.Substring(0, 1).ToUpperInvariant();
    }

}
