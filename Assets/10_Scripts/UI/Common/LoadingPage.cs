using System;
using System.Collections;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IPageLoadingTarget
{
    bool RequiresLoading(PageOpenMode mode);
}

[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public sealed class LoadingPage : MonoBehaviour
{
    [SerializeField] private Image backdrop;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private RectTransform spinner;
    [SerializeField, Min(0f)] private float spinnerDegreesPerSecond = 180f;

    private static LoadingPage _current;

    private GameEventManager _events;
    private Coroutine _transitionRoutine;
    private bool _startupPending = true;

    public static LoadingPage Current => _current;
    public bool HasDesignerReferences =>
        backdrop != null && messageText != null && spinner != null;
    public bool IsTransitioning => _transitionRoutine != null;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    private void Awake()
    {
        if (_current != null && _current != this)
        {
            Debug.LogError(
                "ClientScene contains more than one LoadingPage.",
                this);
            gameObject.SetActive(false);
            return;
        }

        _current = this;
        if (!HasDesignerReferences)
        {
            Debug.LogError(
                "LoadingPage scene references are incomplete.",
                this);
            gameObject.SetActive(false);
            return;
        }

        Show();
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        RefreshMessage();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
    }

    private void OnDestroy()
    {
        BindEvents(null);
        if (_current == this)
            _current = null;
    }

    private void Update()
    {
        TryBindGameEvents();
        if (_startupPending && _events?.IsDataReady == true)
            CompleteStartupLoading();

        if (spinner != null)
        {
            spinner.Rotate(
                0f,
                0f,
                -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }
    }

    public static bool TryBeginTransition(Action transition)
    {
        if (_current == null || transition == null ||
            !_current.HasDesignerReferences)
        {
            return false;
        }

        return _current.BeginTransition(transition);
    }

    private bool BeginTransition(Action transition)
    {
        if (_transitionRoutine != null)
            return true;

        Show();
        _transitionRoutine = StartCoroutine(
            RunTransition(transition));
        return true;
    }

    private IEnumerator RunTransition(Action transition)
    {
        // Render the authored loading UI before starting synchronous page
        // initialization and catalog materialization.
        yield return new WaitForEndOfFrame();

        try
        {
            transition();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        // Keep the blocker for one frame so layout and Canvas rebuilds finish
        // before the destination becomes interactive.
        yield return null;
        _transitionRoutine = null;
        if (!_startupPending)
            Hide();
    }

    private void TryBindGameEvents()
    {
        GameEventManager events = GameManager.Instance?.Events;
        if (_events == events)
            return;

        BindEvents(events);
    }

    private void BindEvents(GameEventManager events)
    {
        if (_events != null)
            _events.DataReady -= CompleteStartupLoading;

        _events = events;
        if (_events != null)
            _events.DataReady += CompleteStartupLoading;
    }

    private void CompleteStartupLoading()
    {
        _startupPending = false;
        if (_transitionRoutine == null)
            Hide();
    }

    private void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshMessage();
    }

    private void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        RefreshMessage();
    }

    private void RefreshMessage()
    {
        if (messageText != null)
        {
            messageText.text = LocalizationService.Get(
                LocalizationKeys.UiLoadingPreparing);
        }
    }
}
