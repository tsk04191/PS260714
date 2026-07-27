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
    private bool _localeEventBound;
    private bool _usesDesignerLayout;

    protected abstract string PageTitle { get; }
    protected virtual string PageDescription => string.Empty;
    protected virtual string PageTitleLocalizationKey => string.Empty;
    protected virtual string PageDescriptionLocalizationKey => string.Empty;
    protected virtual Vector2 PanelSize => new(520f, 560f);
    protected virtual bool FillAvailableSpace => false;
    protected RectTransform ButtonRoot => _buttonRoot;
    public bool HasDesignerLayout => _designerLayoutVersion > 0;

    public AudioSource Speaker { get; set; }

    protected virtual void Awake()
    {
        BindLocaleEvent();
        Init();
    }

    protected virtual void OnDestroy()
    {
        UnbindLocaleEvent();
    }

    protected virtual void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshLayout();
    }

    public void Open(PageOpenMode mode = PageOpenMode.Fresh)
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

        BuildRuntimeUi();
        BuildButtons();
        _initialized = true;
        RefreshPageLocalization();
        RefreshLayout();
    }

    protected abstract void BuildButtons();

    protected Button CreateMenuButton(string label, Action action)
    {
        if (_buttonRoot == null)
            return null;

        string objectName = $"btn{label.Replace(" ", string.Empty)}";
        return CreateStyledButton(
            _buttonRoot,
            objectName,
            label,
            action,
            72f);
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
        bool created = existingButton == null;
        GameObject buttonObject;
        if (existingButton != null &&
            existingButton.TryGetComponent(out Button existing))
        {
            buttonObject = existingButton.gameObject;
        }
        else
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
        }

        Image image = buttonObject.GetComponent<Image>();
        if (created)
        {
            image.color = ButtonColor;
            image.raycastTarget = true;

            LayoutElement buttonLayout =
                buttonObject.GetComponent<LayoutElement>();
            buttonLayout.preferredHeight = preferredHeight;
        }

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.targetGraphic = image;
        if (created)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor =
                Color.Lerp(ButtonColor, Color.white, 0.14f);
            colors.pressedColor =
                Color.Lerp(ButtonColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor =
                Color.Lerp(ButtonColor, Color.black, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
        if (action != null)
            button.onClick.AddListener(() => action());

        TextMeshProUGUI text = CreateText(
            buttonObject.transform.Find("txtLabel") is Transform labelTransform
                ? labelTransform
                : buttonObject.transform,
            "txtLabel",
            26f,
            preferredHeight,
            buttonObject.transform.Find("txtLabel") == null);
        if (created)
        {
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);
        }
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

        Transform existingText = parent.Find(objectName);
        bool created = existingText == null;
        TextMeshProUGUI text = CreateText(
            existingText != null ? existingText : parent,
            objectName,
            fontSize,
            preferredHeight,
            existingText == null);
        text.text = content;
        if (created)
        {
            text.fontStyle = fontStyle;
            text.alignment = alignment;
        }
        return text;
    }

    protected Button CreateOverlayMenuButton(string label, Action action)
    {
        string objectName = $"btn{label.Replace(" ", string.Empty)}Overlay";
        return CreateOverlayMenuButtonCore(
            objectName,
            label,
            null,
            action);
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

    private Button CreateOverlayMenuButtonCore(
        string objectName,
        string label,
        string localizationKey,
        Action action,
        bool topLeftCompact = false)
    {
        if (_runtimeRoot == null)
            return null;

        if (topLeftCompact && _buttonRoot != null)
        {
            Transform obsoleteButton = _buttonRoot.Find(objectName);
            if (obsoleteButton != null)
                obsoleteButton.gameObject.SetActive(false);
        }

        Transform existingButton = _runtimeRoot.Find(objectName);
        bool created = existingButton == null;
        GameObject buttonObject;
        if (existingButton != null &&
            existingButton.TryGetComponent(out Button existing))
        {
            buttonObject = existingButton.gameObject;
        }
        else
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_runtimeRoot, false);
        }
        buttonObject.SetActive(true);

        Image image = buttonObject.GetComponent<Image>();
        if (created)
        {
            RectTransform buttonRect =
                (RectTransform)buttonObject.transform;
            Vector2 cornerAnchor = topLeftCompact
                ? new Vector2(0f, 1f)
                : Vector2.one;
            buttonRect.anchorMin = cornerAnchor;
            buttonRect.anchorMax = cornerAnchor;
            buttonRect.pivot = cornerAnchor;
            buttonRect.anchoredPosition = topLeftCompact
                ? new Vector2(24f, -24f)
                : new Vector2(-32f, -32f);
            buttonRect.sizeDelta = topLeftCompact
                ? new Vector2(120f, 46f)
                : new Vector2(180f, 60f);
            image.color = ButtonColor;
            image.raycastTarget = true;
        }

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.targetGraphic = image;
        if (created)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor =
                Color.Lerp(ButtonColor, Color.white, 0.14f);
            colors.pressedColor =
                Color.Lerp(ButtonColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor =
                Color.Lerp(ButtonColor, Color.black, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
        if (action != null)
            button.onClick.AddListener(() => action());

        Transform existingLabel = buttonObject.transform.Find("txtLabel");
        TextMeshProUGUI text = CreateText(
            existingLabel != null ? existingLabel : buttonObject.transform,
            "txtLabel",
            topLeftCompact ? 18f : 22f,
            topLeftCompact ? 46f : 60f,
            existingLabel == null);
        if (created)
        {
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
        }
        ApplyLocalizedText(text, localizationKey, label);
        return button;
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

    private void BuildRuntimeUi()
    {
        if (TryBindExistingUi())
        {
            RefreshPageLocalization();
            return;
        }

        _usesDesignerLayout = false;
        GameObject rootObject = new(
            RuntimeRootObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        _runtimeRoot = (RectTransform)rootObject.transform;
        _runtimeRoot.SetParent(transform, false);
        _runtimeRoot.anchorMin = Vector2.zero;
        _runtimeRoot.anchorMax = Vector2.one;
        _runtimeRoot.offsetMin = Vector2.zero;
        _runtimeRoot.offsetMax = Vector2.zero;
        Image background = rootObject.GetComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = true;

        GameObject panelObject = new(
            "grpMenuPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        _panel = (RectTransform)panelObject.transform;
        _panel.SetParent(_runtimeRoot, false);
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;
        _panel.sizeDelta = PanelSize;
        panelObject.GetComponent<Image>().color = PanelColor;

        VerticalLayoutGroup panelLayout =
            panelObject.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(40, 40, 40, 40);
        panelLayout.spacing = 20f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandHeight = false;

        _titleText = CreateText(
            _panel,
            "txtPageTitle",
            46f,
            82f,
            true);
        ApplyLocalizedText(
            _titleText,
            PageTitleLocalizationKey,
            PageTitle);

        if (!string.IsNullOrWhiteSpace(PageDescriptionLocalizationKey) ||
            !string.IsNullOrWhiteSpace(PageDescription))
        {
            _descriptionText = CreateText(
                _panel,
                "txtPageDescription",
                20f,
                64f,
                true);
            _descriptionText.fontStyle = FontStyles.Normal;
            ApplyLocalizedText(
                _descriptionText,
                PageDescriptionLocalizationKey,
                PageDescription);
        }

        GameObject buttonRootObject = new(
            "grpMenuButtons",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _buttonRoot = (RectTransform)buttonRootObject.transform;
        _buttonRoot.SetParent(_panel, false);
        LayoutElement rootLayout =
            buttonRootObject.GetComponent<LayoutElement>();
        rootLayout.preferredHeight = 280f;
        rootLayout.flexibleHeight = 1f;

        VerticalLayoutGroup buttonLayout =
            buttonRootObject.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 14f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandHeight = false;
    }

    private bool TryBindExistingUi()
    {
        Transform existingRoot = _runtimeRoot != null
            ? _runtimeRoot
            : transform.Find(RuntimeRootObjectName);
        Transform existingPanel = _panel != null
            ? _panel
            : existingRoot != null
                ? existingRoot.Find("grpMenuPanel")
                : null;
        Transform existingButtonRoot = _buttonRoot != null
            ? _buttonRoot
            : existingPanel != null
                ? existingPanel.Find("grpMenuButtons")
                : null;
        if (existingRoot is not RectTransform rootRect ||
            existingPanel is not RectTransform panelRect ||
            existingButtonRoot is not RectTransform buttonRootRect)
        {
            return false;
        }

        _runtimeRoot = rootRect;
        _panel = panelRect;
        _buttonRoot = buttonRootRect;
        _titleText ??= existingPanel.Find("txtPageTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _descriptionText ??= existingPanel.Find("txtPageDescription")
            ?.GetComponent<TextMeshProUGUI>();
        _usesDesignerLayout = true;
        return true;
    }

#if UNITY_EDITOR
    public void MarkDesignerLayoutCurrent()
    {
        _designerLayoutVersion = 1;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void RebuildEditorPreview()
    {
        if (Application.isPlaying)
            return;

        Transform existingRoot = transform.Find(RuntimeRootObjectName);
        if (existingRoot != null)
            DestroyImmediate(existingRoot.gameObject);

        _runtimeRoot = null;
        _panel = null;
        _buttonRoot = null;
        _titleText = null;
        _descriptionText = null;
        _initialized = false;
        BuildRuntimeUi();
        BuildButtons();
        RefreshPageLocalization();
        RefreshLayout();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene);
    }
#endif

    private void RefreshLayout()
    {
        if (_runtimeRoot == null || _panel == null)
            return;

        if (_usesDesignerLayout)
            return;

        _runtimeRoot.anchorMin = Vector2.zero;
        _runtimeRoot.anchorMax = Vector2.one;
        _runtimeRoot.offsetMin = Vector2.zero;
        _runtimeRoot.offsetMax = Vector2.zero;

        RectTransform pageRect = transform as RectTransform;
        float pageWidth = pageRect != null && pageRect.rect.width > 0f
            ? pageRect.rect.width
            : PanelSize.x;
        float pageHeight = pageRect != null && pageRect.rect.height > 0f
            ? pageRect.rect.height
            : PanelSize.y;
        if (FillAvailableSpace)
        {
            _panel.sizeDelta = new Vector2(
                Mathf.Max(360f, pageWidth),
                Mathf.Max(340f, pageHeight));
            return;
        }

        float availableWidth = pageWidth - 48f;
        float availableHeight = pageHeight - 48f;
        _panel.sizeDelta = new Vector2(
            Mathf.Min(PanelSize.x, Mathf.Max(360f, availableWidth)),
            Mathf.Min(PanelSize.y, Mathf.Max(340f, availableHeight)));
    }

    private void BindLocaleEvent()
    {
        if (_localeEventBound)
            return;

        LocalizationService.LocaleChanged += HandleLocaleChanged;
        _localeEventBound = true;
    }

    private void UnbindLocaleEvent()
    {
        if (!_localeEventBound)
            return;

        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        _localeEventBound = false;
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshPageLocalization();
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

        LocalizedText localizedText = text.GetComponent<LocalizedText>();
        if (localizedText == null)
            localizedText = text.gameObject.AddComponent<LocalizedText>();
        localizedText.SetKey(localizationKey);
    }

    protected static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        float preferredHeight,
        bool createObject)
    {
        GameObject textObject;
        if (createObject)
        {
            textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
        }
        else
        {
            textObject = parent.gameObject;
        }

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (createObject)
        {
            LocalizationFontResolver.ApplyGameDefault(text);
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(14f, fontSize - 10f);
            text.enableAutoSizing = true;
            text.fontStyle = FontStyles.Bold;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        if (layout == null)
            layout = textObject.AddComponent<LayoutElement>();
        if (createObject)
            layout.preferredHeight = preferredHeight;
        return text;
    }

    protected static void SyncIndexedChildren(
        Transform parent,
        string objectNamePrefix,
        int activeCount)
    {
        if (parent == null || string.IsNullOrEmpty(objectNamePrefix))
            return;

        activeCount = Mathf.Max(0, activeCount);
        for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
        {
            Transform child = parent.GetChild(childIndex);
            if (child == null ||
                !child.name.StartsWith(
                    objectNamePrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring(objectNamePrefix.Length);
            if (!int.TryParse(suffix, out int index))
                continue;

            child.gameObject.SetActive(index >= 0 && index < activeCount);
        }
    }
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

    private static readonly Color BrowserPanelColor =
        new(0.055f, 0.072f, 0.062f, 0.98f);
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
            view.BuildLayout();
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

    public void SetListOnlyMode(bool enabled, int columnCount = 3)
    {
        if (_browserRoot == null || _listRoot == null ||
            DetailRoot == null)
        {
            return;
        }

        DetailRoot.gameObject.SetActive(!enabled);
        LayoutElement listLayout =
            _listRoot.GetComponent<LayoutElement>();
        if (listLayout != null)
        {
            listLayout.minWidth = enabled ? 0f : 700f;
            listLayout.preferredWidth = enabled ? 0f : 720f;
            listLayout.flexibleWidth = enabled ? 1f : 0f;
        }

        if (enabled && _browserRoot is RectTransform browserRect)
        {
            browserRect.anchorMin = Vector2.zero;
            browserRect.anchorMax = Vector2.one;
            browserRect.pivot = new Vector2(0.5f, 0.5f);
            browserRect.anchoredPosition = Vector2.zero;
            browserRect.sizeDelta = Vector2.zero;
        }

        GridLayoutGroup grid =
            _cardContent != null
                ? _cardContent.GetComponent<GridLayoutGroup>()
                : null;
        if (grid != null)
        {
            grid.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columnCount);
        }
    }

    private void BuildLayout()
    {
        GameObject browserObject = GetOrCreateChild(
            _host,
            "grpCodexBrowser",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        _browserRoot = browserObject.transform;
        _designerSettings =
            browserObject.GetComponent<CodexBrowserDesignerSettings>();
        if (_designerSettings == null)
        {
            _designerSettings =
                browserObject.AddComponent<CodexBrowserDesignerSettings>();
        }
        browserObject.SetActive(true);
        browserObject.transform.SetSiblingIndex(0);
        LayoutElement browserLayout =
            browserObject.GetComponent<LayoutElement>();
        browserLayout.preferredHeight = 500f;
        browserLayout.flexibleHeight = 1f;
        HorizontalLayoutGroup browserGroup =
            browserObject.GetComponent<HorizontalLayoutGroup>();
        browserGroup.spacing = 18f;
        browserGroup.childAlignment = TextAnchor.UpperCenter;
        browserGroup.childControlWidth = true;
        browserGroup.childControlHeight = true;
        browserGroup.childForceExpandWidth = false;
        browserGroup.childForceExpandHeight = true;

        GameObject listObject = GetOrCreateChild(
            browserObject.transform,
            "grpCodexList",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _listRoot = listObject.transform;
        Image listImage = listObject.GetComponent<Image>();
        listImage.color = BrowserPanelColor;
        listImage.raycastTarget = true;
        LayoutElement listLayout = listObject.GetComponent<LayoutElement>();
        listLayout.minWidth = 700f;
        listLayout.preferredWidth = 720f;
        listLayout.flexibleWidth = 0f;
        VerticalLayoutGroup listGroup =
            listObject.GetComponent<VerticalLayoutGroup>();
        listGroup.padding = new RectOffset(10, 10, 10, 10);
        listGroup.spacing = 10f;
        listGroup.childAlignment = TextAnchor.UpperCenter;
        listGroup.childControlWidth = true;
        listGroup.childControlHeight = true;
        listGroup.childForceExpandWidth = true;
        listGroup.childForceExpandHeight = false;

        BuildToolbar(listObject.transform);
        BuildScrollView(listObject.transform);

        GameObject detailHost = GetOrCreateChild(
            browserObject.transform,
            "grpCodexDetailHost",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        DetailRoot = detailHost.transform;
        LayoutElement detailLayout =
            detailHost.GetComponent<LayoutElement>();
        detailLayout.minWidth = 430f;
        detailLayout.flexibleWidth = 1f;
        detailLayout.flexibleHeight = 1f;
        VerticalLayoutGroup detailGroup =
            detailHost.GetComponent<VerticalLayoutGroup>();
        detailGroup.childAlignment = TextAnchor.UpperCenter;
        detailGroup.childControlWidth = true;
        detailGroup.childControlHeight = true;
        detailGroup.childForceExpandWidth = true;
        detailGroup.childForceExpandHeight = true;

        listObject.transform.SetSiblingIndex(0);
        detailHost.transform.SetSiblingIndex(1);
        _designerSettings.CaptureReferencesFromHierarchy();
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

    private void BuildToolbar(Transform parent)
    {
        GameObject toolbarObject = GetOrCreateChild(
            parent,
            "grpCodexListToolbar",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        LayoutElement toolbarLayout =
            toolbarObject.GetComponent<LayoutElement>();
        toolbarLayout.preferredHeight = 46f;
        HorizontalLayoutGroup toolbarGroup =
            toolbarObject.GetComponent<HorizontalLayoutGroup>();
        toolbarGroup.spacing = 6f;
        toolbarGroup.childAlignment = TextAnchor.MiddleCenter;
        toolbarGroup.childControlWidth = true;
        toolbarGroup.childControlHeight = true;
        toolbarGroup.childForceExpandWidth = false;
        toolbarGroup.childForceExpandHeight = true;

        _searchInput = BuildSearchInput(toolbarObject.transform);
        _searchButton = BuildToolbarButton(
            toolbarObject.transform,
            "btnCodexSearch",
            64f,
            out _searchButtonLabel);
        _filterButton = BuildToolbarButton(
            toolbarObject.transform,
            "btnCodexFilter",
            92f,
            out _filterButtonLabel);
        _sortButton = BuildToolbarButton(
            toolbarObject.transform,
            "btnCodexSort",
            100f,
            out _sortButtonLabel);

        _searchInput.transform.SetSiblingIndex(0);
        _searchButton.transform.SetSiblingIndex(1);
        _filterButton.transform.SetSiblingIndex(2);
        _sortButton.transform.SetSiblingIndex(3);
    }

    private TMP_InputField BuildSearchInput(Transform parent)
    {
        GameObject inputObject = GetOrCreateChild(
            parent,
            "inpCodexSearch",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));
        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.color = new Color(0.035f, 0.05f, 0.043f, 1f);
        inputImage.raycastTarget = true;
        LayoutElement inputLayout =
            inputObject.GetComponent<LayoutElement>();
        inputLayout.minWidth = 140f;
        inputLayout.preferredWidth = 160f;
        inputLayout.flexibleWidth = 1f;

        GameObject viewportObject = GetOrCreateChild(
            inputObject.transform,
            "vptCodexSearch",
            typeof(RectTransform),
            typeof(RectMask2D));
        RectTransform viewportRect =
            (RectTransform)viewportObject.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 4f);
        viewportRect.offsetMax = new Vector2(-10f, -4f);

        TextMeshProUGUI inputText = GetOrCreateText(
            viewportObject.transform,
            "txtCodexSearchValue",
            18f,
            TextAlignmentOptions.MidlineLeft);
        StretchToParent(inputText.rectTransform);
        inputText.raycastTarget = false;

        _searchPlaceholder = GetOrCreateText(
            viewportObject.transform,
            "txtCodexSearchPlaceholder",
            17f,
            TextAlignmentOptions.MidlineLeft);
        StretchToParent(_searchPlaceholder.rectTransform);
        _searchPlaceholder.color =
            new Color(0.65f, 0.66f, 0.59f, 0.8f);
        _searchPlaceholder.fontStyle = FontStyles.Italic;
        _searchPlaceholder.raycastTarget = false;

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = inputImage;
        input.textViewport = viewportRect;
        input.textComponent = inputText;
        input.placeholder = _searchPlaceholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 64;
        return input;
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject scrollObject = GetOrCreateChild(
            parent,
            "scrCodexList",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.01f);
        scrollImage.raycastTarget = true;
        LayoutElement scrollLayout =
            scrollObject.GetComponent<LayoutElement>();
        scrollLayout.preferredHeight = 400f;
        scrollLayout.flexibleHeight = 1f;

        GameObject viewportObject = GetOrCreateChild(
            scrollObject.transform,
            "vptCodexList",
            typeof(RectTransform),
            typeof(RectMask2D));
        StretchToParent((RectTransform)viewportObject.transform);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            "grpCodexCardContent",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        _cardContent = contentObject.transform;
        RectTransform contentRect =
            (RectTransform)contentObject.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 10);
        grid.spacing = new Vector2(8f, 10f);
        grid.cellSize = CardSize;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        ContentSizeFitter fitter =
            contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewportObject.transform;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
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
            GameObject cardObject;
            if (template != null && template.transform.parent == _cardContent)
            {
                cardObject = UnityEngine.Object.Instantiate(
                    template,
                    _cardContent,
                    false);
                cardObject.name = $"btnCodexCard_{cardIndex}";
                cardObject.SetActive(true);
            }
            else
            {
                cardObject = GetOrCreateChild(
                    _cardContent,
                    $"btnCodexCard_{cardIndex}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button),
                    typeof(Outline));
            }
            Image background = cardObject.GetComponent<Image>();
            Button button = cardObject.GetComponent<Button>();
            button.targetGraphic = background;
            Outline outline = cardObject.GetComponent<Outline>();
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            GameObject iconObject = GetOrCreateChild(
                cardObject.transform,
                "imgCodexCardIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 0.285f);
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(8f, 8f);
            iconRect.offsetMax = new Vector2(-8f, -8f);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI fallbackIcon = GetOrCreateText(
                iconObject.transform,
                "txtCodexCardFallbackIcon",
                54f,
                TextAlignmentOptions.Center);
            StretchToParent(fallbackIcon.rectTransform);
            fallbackIcon.fontStyle = FontStyles.Bold;

            GameObject namePlateObject = GetOrCreateChild(
                cardObject.transform,
                "grpCodexCardNamePlate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform namePlateRect =
                (RectTransform)namePlateObject.transform;
            namePlateRect.anchorMin = Vector2.zero;
            namePlateRect.anchorMax = new Vector2(1f, 0.285f);
            namePlateRect.offsetMin = new Vector2(5f, 5f);
            namePlateRect.offsetMax = new Vector2(-5f, -3f);
            Image namePlate = namePlateObject.GetComponent<Image>();
            namePlate.color = new Color(0.025f, 0.035f, 0.03f, 0.9f);
            namePlate.raycastTarget = false;

            TextMeshProUGUI name = GetOrCreateText(
                namePlateObject.transform,
                "txtCodexCardName",
                17f,
                TextAlignmentOptions.Center);
            StretchToParent(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(5f, 3f);
            name.rectTransform.offsetMax = new Vector2(-5f, -3f);
            name.fontStyle = FontStyles.Bold;

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

    private static Button BuildToolbarButton(
        Transform parent,
        string objectName,
        float preferredWidth,
        out TextMeshProUGUI label)
    {
        GameObject buttonObject = GetOrCreateChild(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minWidth = preferredWidth;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = 0f;
        Image image = buttonObject.GetComponent<Image>();
        image.color = CardColor;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        label = GetOrCreateText(
            buttonObject.transform,
            "txtLabel",
            16f,
            TextAlignmentOptions.Center);
        StretchToParent(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(4f, 2f);
        label.rectTransform.offsetMax = new Vector2(-4f, -2f);
        label.fontStyle = FontStyles.Bold;
        return button;
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

    private static TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = GetOrCreateChild(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 6f);
        text.enableAutoSizing = true;
        text.color = CardTextColor;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject GetOrCreateChild(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject child;
        if (existing != null)
        {
            child = existing.gameObject;
            foreach (Type componentType in componentTypes)
            {
                if (child.GetComponent(componentType) == null)
                    child.AddComponent(componentType);
            }
        }
        else
        {
            child = new GameObject(objectName, componentTypes);
            child.transform.SetParent(parent, false);
        }

        return child;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
