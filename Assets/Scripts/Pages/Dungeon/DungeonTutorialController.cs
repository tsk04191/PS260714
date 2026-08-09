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

    [SerializeField] private RectTransform[] _dimmers =
        new RectTransform[4];
    [SerializeField] private RectTransform[] _borders =
        new RectTransform[4];
    private readonly Vector3[] _worldCorners = new Vector3[4];

    private DungeonPage _page;
    private DungeonFieldView _fieldView;
    private DungeonTutorialDefinition _definition;
    [SerializeField] private RectTransform _overlayRoot;
    [SerializeField] private RectTransform _targetBlocker;
    [SerializeField] private RectTransform _descriptionPanel;
    [SerializeField] private RectTransform _messageRect;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private TextMeshProUGUI _nextButtonText;
    [SerializeField] private Button _nextButton;
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
        _overlayRoot ??= transform.Find("grpTutorialOverlay")
            as RectTransform;
        if (_overlayRoot == null)
        {
            Debug.LogError(
                "Tutorial overlay must be placed in the Scene.",
                this);
            return;
        }

        for (int index = 0; index < 4; index++)
        {
            _dimmers[index] ??= _overlayRoot
                .Find($"imgTutorialDim_{index + 1}") as RectTransform;
            _borders[index] ??= _overlayRoot
                .Find($"imgTutorialBorder_{index + 1}") as RectTransform;
        }

        _targetBlocker ??= _overlayRoot
            .Find("imgTutorialTargetBlocker") as RectTransform;
        _descriptionPanel ??= _overlayRoot
            .Find("grpTutorialDescription") as RectTransform;
        _progressText ??= _descriptionPanel
            ?.Find("txtTutorialProgress")?.GetComponent<TextMeshProUGUI>();
        _messageText ??= _descriptionPanel
            ?.Find("txtTutorialMessage")?.GetComponent<TextMeshProUGUI>();
        _messageRect ??= _messageText?.rectTransform;
        _nextButton ??= _descriptionPanel
            ?.Find("btnTutorialNext")?.GetComponent<Button>();
        _nextButtonText ??= _nextButton
            ?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (_targetBlocker == null || _descriptionPanel == null ||
            _progressText == null || _messageText == null ||
            _nextButton == null || _nextButtonText == null ||
            ArrayHasNull(_dimmers) || ArrayHasNull(_borders))
        {
            Debug.LogError(
                "Tutorial overlay Scene references are incomplete.",
                this);
            return;
        }

        _nextButton.onClick.RemoveAllListeners();
        _nextButton.onClick.AddListener(HandleNextClicked);

        _overlayRoot.gameObject.SetActive(false);
    }

    private static bool ArrayHasNull(RectTransform[] values)
    {
        if (values == null || values.Length < 4)
            return true;
        for (int index = 0; index < 4; index++)
        {
            if (values[index] == null)
                return true;
        }
        return false;
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
