using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonTutorialController : MonoBehaviour
{
    private const float HighlightMargin = 10f;
    private const float BorderThickness = 3f;
    private const float DescriptionGap = 14f;
    private const float DescriptionHeight = 132f;
    private const float RootPadding = 18f;

    private static readonly Color DimColor = new(0f, 0f, 0f, 0.78f);
    private static readonly Color PanelColor =
        new(0.055f, 0.07f, 0.06f, 0.98f);
    private static readonly Color ButtonColor =
        new(0.18f, 0.29f, 0.23f, 1f);
    private static readonly Color TextColor =
        new(0.96f, 0.96f, 0.92f, 1f);

    private readonly RectTransform[] _dimmers = new RectTransform[4];
    private readonly RectTransform[] _borders = new RectTransform[4];
    private readonly Vector3[] _worldCorners = new Vector3[4];

    private DungeonPage _page;
    private DungeonFieldView _fieldView;
    private DungeonTutorialDefinition _definition;
    private RectTransform _overlayRoot;
    private RectTransform _targetBlocker;
    private RectTransform _descriptionPanel;
    private RectTransform _messageRect;
    private TextMeshProUGUI _progressText;
    private TextMeshProUGUI _messageText;
    private TextMeshProUGUI _nextButtonText;
    private Button _nextButton;
    private RectTransform _startingChoiceTarget;
    private int _stepIndex = -1;
    private bool _showingCompletion;
    private bool _initialized;
    private bool _running;
    private bool _localizationEventsBound;

    private DungeonTutorialStepDefinition CurrentStep =>
        _definition != null && _stepIndex >= 0 &&
        _stepIndex < _definition.Steps.Count
            ? _definition.Steps[_stepIndex]
            : null;

    public void Initialize(DungeonPage page, DungeonFieldView fieldView)
    {
        if (page == null || fieldView == null)
            return;

        _page = page;
        _fieldView = fieldView;
        EnsureRuntimeUi();
        BindLocalizationEvents();
        _initialized = true;
    }

    public void BeginStartingChoice(
        DungeonTutorialDefinition definition,
        RectTransform firstChoice)
    {
        if (!_initialized || definition == null || firstChoice == null ||
            definition.Steps.Count == 0)
            return;

        _definition = definition;
        _startingChoiceTarget = firstChoice;
        _stepIndex = 0;
        _showingCompletion = false;
        _page.UpdateTutorialProgress(_stepIndex);
        _running = true;
        _overlayRoot.gameObject.SetActive(true);
        _overlayRoot.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        RefreshCopy();
        RefreshLayout();
    }

    public void BeginBattleWalkthrough()
    {
        if (!_initialized || !_running || _definition == null)
            return;

        _startingChoiceTarget = null;
        if (CurrentStep != null &&
            CurrentStep.Action ==
            EDungeonTutorialAction.SelectStartingCharacter)
        {
            _stepIndex++;
        }
        if (CurrentStep == null)
            return;

        _showingCompletion = false;
        _page.UpdateTutorialProgress(_stepIndex);
        _overlayRoot.gameObject.SetActive(true);
        _overlayRoot.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        RefreshCopy();
        RefreshLayout();
    }

    public void PauseForStartingItemSelection()
    {
        if (!_initialized || !_running)
            return;

        _startingChoiceTarget = null;
        if (_overlayRoot != null)
            _overlayRoot.gameObject.SetActive(false);
    }

    public void ShowCompletion()
    {
        if (!_initialized)
            return;

        _showingCompletion = true;
        _page.UpdateTutorialProgress(-1);
        _running = true;
        _overlayRoot.gameObject.SetActive(true);
        _overlayRoot.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        RefreshCopy();
        RefreshLayout();
    }

    public void StopTutorial()
    {
        _running = false;
        _stepIndex = -1;
        _showingCompletion = false;
        _startingChoiceTarget = null;
        _page?.UpdateTutorialProgress(-1);
        if (_overlayRoot != null)
            _overlayRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_initialized)
            BindLocalizationEvents();
    }

    private void OnDisable()
    {
        UnbindLocalizationEvents();
    }

    private void OnDestroy()
    {
        UnbindLocalizationEvents();
    }

    private void LateUpdate()
    {
        if (_running && _overlayRoot != null &&
            _overlayRoot.gameObject.activeSelf)
        {
            _overlayRoot.SetAsLastSibling();
            RefreshLayout();
        }
    }

    private void HandleNextClicked()
    {
        if (_showingCompletion)
        {
            _page?.CompleteTutorialStage();
            return;
        }

        DungeonTutorialStepDefinition step = CurrentStep;
        if (step == null ||
            step.Action == EDungeonTutorialAction.SelectStartingCharacter)
        {
            return;
        }

        if (step.Action == EDungeonTutorialAction.StartBattle)
        {
            if (_page == null || !_page.BeginTutorialBattle())
                return;

            _running = false;
            _stepIndex = -1;
            _page.UpdateTutorialProgress(-1);
            _overlayRoot.gameObject.SetActive(false);
            return;
        }

        _stepIndex++;
        if (CurrentStep == null)
            return;
        _page.UpdateTutorialProgress(_stepIndex);

        Canvas.ForceUpdateCanvases();
        RefreshCopy();
        RefreshLayout();
    }

    private RectTransform GetCurrentTarget()
    {
        DungeonTutorialStepDefinition step = CurrentStep;
        return !_showingCompletion && step != null && _fieldView != null
            ? _fieldView.GetHighlightTarget(
                step.Target,
                _page,
                _startingChoiceTarget)
            : null;
    }

    private void RefreshCopy()
    {
        if (_progressText == null || _messageText == null ||
            _nextButton == null || _nextButtonText == null)
        {
            return;
        }

        DungeonTutorialStepDefinition step = CurrentStep;
        int progress = !_showingCompletion && step != null
            ? _stepIndex + 1
            : 0;
        int total = _definition != null ? _definition.Steps.Count : 0;
        _progressText.text = progress > 0
            ? $"{progress} / {total}"
            : string.Empty;
        _messageText.text = LocalizationService.Get(GetMessageKey());

        bool isChoice = step != null && step.Action ==
            EDungeonTutorialAction.SelectStartingCharacter;
        bool startsBattle = step != null && step.Action ==
            EDungeonTutorialAction.StartBattle;
        bool isComplete = _showingCompletion;
        _nextButton.gameObject.SetActive(!isChoice);
        _nextButtonText.text = LocalizationService.Get(
            isComplete
                ? _definition.ReturnButtonLocalizationKey
                : startsBattle
                    ? _definition.StartBattleButtonLocalizationKey
                    : _definition.NextButtonLocalizationKey);

        _messageRect.anchorMax = new Vector2(
            isChoice ? 0.96f : 0.76f,
            0.82f);
        LocalizationFontResolver.ApplyGameDefault(_progressText);
        LocalizationFontResolver.ApplyGameDefault(_messageText);
        LocalizationFontResolver.ApplyGameDefault(_nextButtonText);
    }

    private string GetMessageKey()
    {
        if (_definition == null)
            return string.Empty;
        if (_showingCompletion)
            return _definition.CompletionLocalizationKey;
        return CurrentStep != null
            ? CurrentStep.MessageLocalizationKey
            : string.Empty;
    }

    private void RefreshLayout()
    {
        Rect rootBounds = _overlayRoot.rect;
        RectTransform target = GetCurrentTarget();
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            SetRect(_dimmers[0], rootBounds);
            for (int index = 1; index < _dimmers.Length; index++)
                SetRect(_dimmers[index], Rect.zero);
            SetHighlightDecorations(Rect.zero, false);
            PositionDescription(rootBounds, Rect.zero, false);
            return;
        }

        target.GetWorldCorners(_worldCorners);
        float xMin = float.MaxValue;
        float yMin = float.MaxValue;
        float xMax = float.MinValue;
        float yMax = float.MinValue;
        for (int index = 0; index < _worldCorners.Length; index++)
        {
            Vector3 local = _overlayRoot.InverseTransformPoint(
                _worldCorners[index]);
            xMin = Mathf.Min(xMin, local.x);
            yMin = Mathf.Min(yMin, local.y);
            xMax = Mathf.Max(xMax, local.x);
            yMax = Mathf.Max(yMax, local.y);
        }

        Rect highlight = Rect.MinMaxRect(
            Mathf.Clamp(xMin - HighlightMargin, rootBounds.xMin, rootBounds.xMax),
            Mathf.Clamp(yMin - HighlightMargin, rootBounds.yMin, rootBounds.yMax),
            Mathf.Clamp(xMax + HighlightMargin, rootBounds.xMin, rootBounds.xMax),
            Mathf.Clamp(yMax + HighlightMargin, rootBounds.yMin, rootBounds.yMax));

        SetRect(
            _dimmers[0],
            Rect.MinMaxRect(
                rootBounds.xMin,
                rootBounds.yMin,
                highlight.xMin,
                rootBounds.yMax));
        SetRect(
            _dimmers[1],
            Rect.MinMaxRect(
                highlight.xMax,
                rootBounds.yMin,
                rootBounds.xMax,
                rootBounds.yMax));
        SetRect(
            _dimmers[2],
            Rect.MinMaxRect(
                highlight.xMin,
                rootBounds.yMin,
                highlight.xMax,
                highlight.yMin));
        SetRect(
            _dimmers[3],
            Rect.MinMaxRect(
                highlight.xMin,
                highlight.yMax,
                highlight.xMax,
                rootBounds.yMax));

        SetHighlightDecorations(highlight, true);
        PositionDescription(rootBounds, highlight, true);
    }

    private void SetHighlightDecorations(Rect highlight, bool visible)
    {
        _targetBlocker.gameObject.SetActive(
            visible && (CurrentStep == null || CurrentStep.Action !=
                EDungeonTutorialAction.SelectStartingCharacter));
        if (_targetBlocker.gameObject.activeSelf)
            SetRect(_targetBlocker, highlight);

        for (int index = 0; index < _borders.Length; index++)
            _borders[index].gameObject.SetActive(visible);
        if (!visible)
            return;

        SetRect(
            _borders[0],
            Rect.MinMaxRect(
                highlight.xMin - BorderThickness,
                highlight.yMin - BorderThickness,
                highlight.xMin,
                highlight.yMax + BorderThickness));
        SetRect(
            _borders[1],
            Rect.MinMaxRect(
                highlight.xMax,
                highlight.yMin - BorderThickness,
                highlight.xMax + BorderThickness,
                highlight.yMax + BorderThickness));
        SetRect(
            _borders[2],
            Rect.MinMaxRect(
                highlight.xMin,
                highlight.yMin - BorderThickness,
                highlight.xMax,
                highlight.yMin));
        SetRect(
            _borders[3],
            Rect.MinMaxRect(
                highlight.xMin,
                highlight.yMax,
                highlight.xMax,
                highlight.yMax + BorderThickness));
    }

    private void PositionDescription(
        Rect rootBounds,
        Rect highlight,
        bool hasHighlight)
    {
        float width = Mathf.Min(720f, rootBounds.width - RootPadding * 2f);
        float x = hasHighlight ? highlight.center.x : rootBounds.center.x;
        x = Mathf.Clamp(
            x,
            rootBounds.xMin + RootPadding + width * 0.5f,
            rootBounds.xMax - RootPadding - width * 0.5f);

        float y;
        if (!hasHighlight)
        {
            y = rootBounds.center.y;
        }
        else
        {
            y = highlight.yMin - DescriptionGap - DescriptionHeight * 0.5f;
            if (y - DescriptionHeight * 0.5f <
                rootBounds.yMin + RootPadding)
            {
                y = highlight.yMax + DescriptionGap +
                    DescriptionHeight * 0.5f;
            }

            y = Mathf.Clamp(
                y,
                rootBounds.yMin + RootPadding + DescriptionHeight * 0.5f,
                rootBounds.yMax - RootPadding - DescriptionHeight * 0.5f);
        }

        SetRect(
            _descriptionPanel,
            new Rect(
                x - width * 0.5f,
                y - DescriptionHeight * 0.5f,
                width,
                DescriptionHeight));
    }

    private void EnsureRuntimeUi()
    {
        if (_overlayRoot != null)
            return;

        GameObject rootObject = new("grpTutorialOverlay", typeof(RectTransform));
        _overlayRoot = (RectTransform)rootObject.transform;
        _overlayRoot.SetParent(transform, false);
        _overlayRoot.anchorMin = Vector2.zero;
        _overlayRoot.anchorMax = Vector2.one;
        _overlayRoot.offsetMin = Vector2.zero;
        _overlayRoot.offsetMax = Vector2.zero;

        for (int index = 0; index < _dimmers.Length; index++)
        {
            Image dimmer = CreateImage(
                _overlayRoot,
                $"imgTutorialDim_{index + 1}",
                DimColor,
                true);
            _dimmers[index] = dimmer.rectTransform;
        }

        Image blocker = CreateImage(
            _overlayRoot,
            "imgTutorialTargetBlocker",
            Color.clear,
            true);
        _targetBlocker = blocker.rectTransform;

        for (int index = 0; index < _borders.Length; index++)
        {
            Image border = CreateImage(
                _overlayRoot,
                $"imgTutorialBorder_{index + 1}",
                Color.white,
                false);
            _borders[index] = border.rectTransform;
        }

        Image panelImage = CreateImage(
            _overlayRoot,
            "grpTutorialDescription",
            PanelColor,
            true);
        _descriptionPanel = panelImage.rectTransform;

        _progressText = CreateText(
            _descriptionPanel,
            "txtTutorialProgress",
            17f,
            TextAlignmentOptions.Center);
        RectTransform progressRect = _progressText.rectTransform;
        progressRect.anchorMin = new Vector2(0.035f, 0.72f);
        progressRect.anchorMax = new Vector2(0.16f, 0.96f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;

        _messageText = CreateText(
            _descriptionPanel,
            "txtTutorialMessage",
            22f,
            TextAlignmentOptions.MidlineLeft);
        _messageText.enableAutoSizing = true;
        _messageText.fontSizeMin = 15f;
        _messageText.fontSizeMax = 22f;
        _messageText.textWrappingMode = TextWrappingModes.Normal;
        _messageRect = _messageText.rectTransform;
        _messageRect.anchorMin = new Vector2(0.04f, 0.16f);
        _messageRect.anchorMax = new Vector2(0.76f, 0.82f);
        _messageRect.offsetMin = Vector2.zero;
        _messageRect.offsetMax = Vector2.zero;

        Image buttonImage = CreateImage(
            _descriptionPanel,
            "btnTutorialNext",
            ButtonColor,
            true);
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = new Vector2(0.79f, 0.26f);
        buttonRect.anchorMax = new Vector2(0.965f, 0.74f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        _nextButton = buttonImage.gameObject.AddComponent<Button>();
        _nextButton.targetGraphic = buttonImage;
        _nextButton.onClick.AddListener(HandleNextClicked);

        _nextButtonText = CreateText(
            buttonRect,
            "txtTutorialNext",
            19f,
            TextAlignmentOptions.Center);
        RectTransform nextTextRect = _nextButtonText.rectTransform;
        nextTextRect.anchorMin = Vector2.zero;
        nextTextRect.anchorMax = Vector2.one;
        nextTextRect.offsetMin = new Vector2(6f, 3f);
        nextTextRect.offsetMax = new Vector2(-6f, -3f);

        _overlayRoot.gameObject.SetActive(false);
    }

    private static Image CreateImage(
        Transform parent,
        string objectName,
        Color color,
        bool raycastTarget)
    {
        GameObject imageObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.color = TextColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform target, Rect rect)
    {
        if (target == null)
            return;

        target.anchorMin = new Vector2(0.5f, 0.5f);
        target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = rect.center;
        target.sizeDelta = new Vector2(
            Mathf.Max(0f, rect.width),
            Mathf.Max(0f, rect.height));
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

    private void HandleLocalizationChanged(string _)
    {
        if (_running)
            RefreshCopy();
    }
}
