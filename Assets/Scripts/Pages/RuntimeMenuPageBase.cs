using System;
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

    private RectTransform _runtimeRoot;
    private RectTransform _panel;
    private RectTransform _buttonRoot;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private bool _initialized;
    private bool _localeEventBound;

    protected abstract string PageTitle { get; }
    protected virtual string PageDescription => string.Empty;
    protected virtual string PageTitleLocalizationKey => string.Empty;
    protected virtual string PageDescriptionLocalizationKey => string.Empty;
    protected virtual Vector2 PanelSize => new(520f, 560f);
    protected RectTransform ButtonRoot => _buttonRoot;

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
        image.color = ButtonColor;
        image.raycastTarget = true;

        LayoutElement buttonLayout =
            buttonObject.GetComponent<LayoutElement>();
        buttonLayout.preferredHeight = preferredHeight;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = Color.Lerp(ButtonColor, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(ButtonColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Color.Lerp(ButtonColor, Color.black, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
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
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);
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
        TextMeshProUGUI text = CreateText(
            existingText != null ? existingText : parent,
            objectName,
            fontSize,
            preferredHeight,
            existingText == null);
        text.text = content;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
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

    private Button CreateOverlayMenuButtonCore(
        string objectName,
        string label,
        string localizationKey,
        Action action)
    {
        if (_runtimeRoot == null)
            return null;

        Transform existingButton = _runtimeRoot.Find(objectName);
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

        RectTransform buttonRect =
            (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.anchoredPosition = new Vector2(-32f, -32f);
        buttonRect.sizeDelta = new Vector2(180f, 60f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.targetGraphic = image;
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
        if (action != null)
            button.onClick.AddListener(() => action());

        Transform existingLabel = buttonObject.transform.Find("txtLabel");
        TextMeshProUGUI text = CreateText(
            existingLabel != null ? existingLabel : buttonObject.transform,
            "txtLabel",
            22f,
            60f,
            existingLabel == null);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
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
        Transform existingRoot = transform.Find(RuntimeRootObjectName);
        Transform existingPanel = existingRoot != null
            ? existingRoot.Find("grpMenuPanel")
            : null;
        Transform existingButtonRoot = existingPanel != null
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
        _titleText = existingPanel.Find("txtPageTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _descriptionText = existingPanel.Find("txtPageDescription")
            ?.GetComponent<TextMeshProUGUI>();
        return true;
    }

#if UNITY_EDITOR
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

        _runtimeRoot.anchorMin = Vector2.zero;
        _runtimeRoot.anchorMax = Vector2.one;
        _runtimeRoot.offsetMin = Vector2.zero;
        _runtimeRoot.offsetMax = Vector2.zero;

        RectTransform pageRect = transform as RectTransform;
        float availableWidth = pageRect != null && pageRect.rect.width > 0f
            ? pageRect.rect.width - 48f
            : PanelSize.x;
        float availableHeight = pageRect != null && pageRect.rect.height > 0f
            ? pageRect.rect.height - 48f
            : PanelSize.y;
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
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        if (layout == null)
            layout = textObject.AddComponent<LayoutElement>();
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
