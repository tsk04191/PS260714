using System;
using System.Collections;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RecruitRevealOverlay
{
    private const string RootName = "grpRecruitRevealOverlay";

    private static readonly string[] NeutralFlipLabels =
    {
        "RECRUIT",
        "TRANSFER",
        "DESTINATION",
        "PLATFORM",
        "0000",
        "////",
        "STANDBY",
        "ARRIVAL",
    };

    private readonly MonoBehaviour _runner;
    private readonly Transform _host;
    private readonly RecruitRevealPresentationSO _presentation;
    private readonly List<RecruitFlipRowView> _rows = new();
    private readonly List<Coroutine> _rowRoutines = new();
    private readonly List<RecruitRevealEntry> _entries = new();
    private readonly List<string> _flipLabels = new();

    private RecruitRevealDesignerBindings _designerBindings;
    private RectTransform _root;
    private CanvasGroup _canvasGroup;
    private Image _backdrop;
    private Button _backdropButton;
    private RectTransform _rowsContainer;
    private VerticalLayoutGroup _rowsLayout;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _instruction;
    private Button _skipButton;
    private TextMeshProUGUI _skipLabel;
    private Button _confirmButton;
    private TextMeshProUGUI _confirmLabel;
    private Coroutine _sequenceRoutine;
    private Action _closed;
    private bool _isKorean;
    private bool _isPlaying;
    private bool _isComplete;
    private float _shownAt;

    private RecruitRevealOverlay(
        MonoBehaviour runner,
        Transform host)
    {
        _runner = runner;
        _host = host;
        _presentation = RecruitRevealPresentationSO.Load();
    }

    public bool IsVisible =>
        _root != null && _root.gameObject.activeSelf;
    public bool IsPlaying => _isPlaying;

    public static RecruitRevealOverlay Build(
        MonoBehaviour runner,
        Transform host)
    {
        if (runner == null || host == null)
            return null;

        RecruitRevealOverlay overlay = new(runner, host);
        if (!overlay.TryBindDesignerLayout())
        {
            RecruitRevealDesignerBindings existingBindings =
                host.Find(RootName)
                    ?.GetComponent<RecruitRevealDesignerBindings>();
            if (existingBindings != null &&
                existingBindings.HasDesignerLayout)
            {
                throw new InvalidOperationException(
                    "The designer-owned recruit reveal overlay has missing " +
                    "UI references. Repair its bindings instead of " +
                    "rebuilding the scene layout.");
            }

            overlay.BuildLayout();
            if (!overlay.TryBindDesignerLayout())
            {
                throw new InvalidOperationException(
                    "Failed to build the recruit reveal designer layout.");
            }
        }
        overlay.WireButtons();
        overlay.CancelAndHide(false);
        return overlay;
    }

#if UNITY_EDITOR
    public bool CaptureDesignerLayout()
    {
        if (_root == null)
            return false;
        _designerBindings ??=
            _root.GetComponent<RecruitRevealDesignerBindings>();
        if (_designerBindings == null)
        {
            _designerBindings =
                UnityEditor.Undo.AddComponent<
                    RecruitRevealDesignerBindings>(
                    _root.gameObject);
        }

        if (!_designerBindings.HasRequiredReferences &&
            !_designerBindings.CaptureReferencesFromHierarchy())
        {
            return false;
        }
        for (int index = 0; index < _rows.Count; index++)
            _rows[index].EnsureRewardIconSlot();
        if (!_designerBindings.HasDesignerLayout)
            _root.SetAsLastSibling();
        _designerBindings.MarkDesignerLayoutCurrent();
        UnityEditor.EditorUtility.SetDirty(_root.gameObject);
        return true;
    }

    public void ShowEditorPreview(
        IReadOnlyList<RecruitRevealEntry> entries,
        bool korean)
    {
        if (_root == null || entries == null || entries.Count == 0)
            return;

        CancelAndHide(false);
        int count = Mathf.Min(
            entries.Count,
            _presentation.MaximumRows);
        for (int index = 0; index < count; index++)
            _entries.Add(entries[index]);
        if (_entries.Count == 0)
            return;

        _isKorean = korean;
        _isPlaying = false;
        _isComplete = true;
        _canvasGroup.alpha = 1f;
        _root.gameObject.SetActive(true);
        _title.text = korean
            ? "모집 행선 정보"
            : "RECRUIT DESTINATION BOARD";
        _instruction.text = korean
            ? "에디터 결과 레이아웃 미리보기"
            : "EDITOR RESULT LAYOUT PREVIEW";
        _skipLabel.text = korean ? "스킵" : "SKIP";
        _confirmLabel.text = korean ? "확인" : "CONFIRM";
        _skipButton.gameObject.SetActive(false);
        _confirmButton.gameObject.SetActive(true);
        ConfigureRows();
        for (int index = 0; index < _entries.Count; index++)
        {
            _rows[index].ShowFinal(
                _entries[index],
                _presentation,
                korean);
        }
    }

    public void HideEditorPreview()
    {
        CancelAndHide(false);
    }
#endif

    public bool Show(
        IReadOnlyList<RecruitRevealEntry> entries,
        bool korean,
        Action closed)
    {
        if (_root == null || entries == null || entries.Count == 0)
            return false;

        CancelAndHide(false);
        _entries.Clear();
        int count = Mathf.Min(
            entries.Count,
            _presentation.MaximumRows);
        for (int index = 0; index < count; index++)
            _entries.Add(entries[index]);

        if (_entries.Count == 0)
            return false;

        _isKorean = korean;
        _closed = closed;
        _isPlaying = true;
        _isComplete = false;
        _shownAt = Time.unscaledTime;
        _canvasGroup.alpha = 0f;
        _root.gameObject.SetActive(true);
        _root.SetAsLastSibling();

        _title.text = korean
            ? "모집 행선 정보"
            : "RECRUIT DESTINATION BOARD";
        _instruction.text = korean
            ? "결과 확인 중"
            : "CONFIRMING ARRIVALS";
        _skipLabel.text = korean ? "스킵" : "SKIP";
        _confirmLabel.text = korean ? "확인" : "CONFIRM";
        _skipButton.gameObject.SetActive(true);
        _skipButton.interactable = false;
        _confirmButton.gameObject.SetActive(false);

        ConfigureRows();
        BuildFlipLabels();
        _sequenceRoutine = _runner.StartCoroutine(
            PlaySequence());
        return true;
    }

    public void CancelAndHide(bool notifyClosed)
    {
        StopAnimations();
        _isPlaying = false;
        _isComplete = false;
        if (_root != null)
            _root.gameObject.SetActive(false);

        Action callback = notifyClosed ? _closed : null;
        _closed = null;
        _entries.Clear();
        _flipLabels.Clear();
        callback?.Invoke();
    }

    private IEnumerator PlaySequence()
    {
        yield return FadeCanvas(
            0f,
            1f,
            _presentation.IntroFadeDuration);

        int completedRows = 0;
        for (int index = 0; index < _entries.Count; index++)
        {
            int capturedIndex = index;
            Coroutine routine = _runner.StartCoroutine(
                _rows[index].Play(
                    _entries[index],
                    _flipLabels,
                    _presentation,
                    _isKorean,
                    () => completedRows++));
            _rowRoutines.Add(routine);

            if (capturedIndex < _entries.Count - 1)
            {
                yield return WaitUnscaled(
                    _presentation.RowStartInterval);
            }
        }

        while (_isPlaying && completedRows < _entries.Count)
        {
            UpdateSkipAvailability();
            yield return null;
        }

        if (_isPlaying)
            SetCompleteState();
        _sequenceRoutine = null;
    }

    private IEnumerator FadeCanvas(
        float from,
        float to,
        float duration)
    {
        _canvasGroup.alpha = from;
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(
                from,
                to,
                Mathf.Clamp01(elapsed / duration));
            UpdateSkipAvailability();
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (_isPlaying && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            UpdateSkipAvailability();
            yield return null;
        }
    }

    private void UpdateSkipAvailability()
    {
        if (!_isPlaying ||
            _skipButton == null ||
            _skipButton.interactable)
        {
            return;
        }

        if (Time.unscaledTime - _shownAt >=
            _presentation.SkipEnableDelay)
        {
            _skipButton.interactable = true;
        }
    }

    private void HandleBackdropClicked()
    {
        if (_isPlaying)
        {
            if (_skipButton.interactable)
                CompleteImmediately();
            return;
        }

        if (_isComplete)
            Close();
    }

    private void CompleteImmediately()
    {
        if (!_isPlaying)
            return;

        StopAnimations();
        for (int index = 0; index < _entries.Count; index++)
        {
            _rows[index].ShowFinal(
                _entries[index],
                _presentation,
                _isKorean);
        }
        SetCompleteState();
    }

    private void SetCompleteState()
    {
        _isPlaying = false;
        _isComplete = true;
        _skipButton.gameObject.SetActive(false);
        _confirmButton.gameObject.SetActive(true);
        _instruction.text = _isKorean
            ? "모든 결과가 도착했습니다 · 화면을 눌러 확인"
            : "ALL RESULTS ARRIVED · TAP TO CONFIRM";
    }

    private void Close()
    {
        if (!_isComplete)
            return;

        StopAnimations();
        _isPlaying = false;
        _isComplete = false;
        _root.gameObject.SetActive(false);
        Action callback = _closed;
        _closed = null;
        _entries.Clear();
        _flipLabels.Clear();
        callback?.Invoke();
    }

    private void StopAnimations()
    {
        if (_sequenceRoutine != null)
        {
            _runner.StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        for (int index = 0; index < _rowRoutines.Count; index++)
        {
            if (_rowRoutines[index] != null)
                _runner.StopCoroutine(_rowRoutines[index]);
        }
        _rowRoutines.Clear();
    }

    private void ConfigureRows()
    {
        bool single = _entries.Count == 1;
        Canvas.ForceUpdateCanvases();
        float availableHeight = Mathf.Max(
            0f,
            _rowsContainer.rect.height);
        float rowHeight;
        if (single)
        {
            _rowsLayout.spacing = 0f;
            rowHeight = Mathf.Min(
                _presentation.SingleRowHeight,
                availableHeight);
        }
        else
        {
            rowHeight =
                _presentation.GetFittedMultiRowHeight(
                    availableHeight,
                    _entries.Count,
                    out float fittedSpacing);
            _rowsLayout.spacing = fittedSpacing;
        }

        _rowsLayout.childAlignment = TextAnchor.MiddleCenter;
        for (int index = 0; index < _rows.Count; index++)
        {
            bool active = index < _entries.Count;
            _rows[index].SetActive(active);
            if (!active)
                continue;

            _rows[index].Configure(
                index,
                rowHeight,
                single,
                _presentation);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            _rowsContainer);
    }

    private void BuildFlipLabels()
    {
        _flipLabels.Clear();
        for (int index = 0;
             index < NeutralFlipLabels.Length;
             index++)
        {
            _flipLabels.Add(NeutralFlipLabels[index]);
        }

        for (int index = 0; index < _entries.Count; index++)
        {
            string label = _entries[index].DisplayName;
            if (!string.IsNullOrWhiteSpace(label) &&
                !_flipLabels.Contains(label))
            {
                _flipLabels.Add(label);
            }
        }
    }

    private bool TryBindDesignerLayout()
    {
        Transform root = _host?.Find(RootName);
        _designerBindings =
            root?.GetComponent<RecruitRevealDesignerBindings>();
        if (_designerBindings == null)
            return false;
        if (!_designerBindings.HasRequiredReferences)
            _designerBindings.CaptureReferencesFromHierarchy();
        if (!_designerBindings.HasRequiredReferences)
            return false;

        _root = _designerBindings.Root;
        _canvasGroup = _designerBindings.CanvasGroup;
        _backdrop = _designerBindings.Backdrop;
        _backdropButton = _designerBindings.BackdropButton;
        _rowsContainer = _designerBindings.RowsContainer;
        _rowsLayout = _designerBindings.RowsLayout;
        _title = _designerBindings.Title;
        _instruction = _designerBindings.Instruction;
        _skipButton = _designerBindings.SkipButton;
        _skipLabel = _designerBindings.SkipLabel;
        _confirmButton = _designerBindings.ConfirmButton;
        _confirmLabel = _designerBindings.ConfirmLabel;

        _rows.Clear();
        for (int index = 0;
             index < _designerBindings.ResultRows.Count;
             index++)
        {
            RecruitFlipRowView row =
                RecruitFlipRowView.Bind(
                    _designerBindings.ResultRows[index],
                    index);
            if (row == null)
            {
                _rows.Clear();
                return false;
            }
            _rows.Add(row);
        }
        return _rows.Count == _presentation.MaximumRows;
    }

    private void BuildLayout()
    {
        GameObject rootObject = GetOrCreateUiObject(
            _host,
            RootName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(CanvasGroup),
            typeof(RecruitRevealDesignerBindings));
        _root = (RectTransform)rootObject.transform;
        Stretch(_root);
        _backdrop = rootObject.GetComponent<Image>();
        _backdrop.color = _presentation.BackdropColor;
        _backdrop.raycastTarget = true;
        _backdropButton = rootObject.GetComponent<Button>();
        _backdropButton.targetGraphic = _backdrop;
        _backdropButton.transition = Selectable.Transition.None;
        _canvasGroup = rootObject.GetComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        GameObject accentObject = GetOrCreateUiObject(
            _root,
            "imgRevealTopAccent",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform accent =
            (RectTransform)accentObject.transform;
        accent.anchorMin = new Vector2(0.12f, 1f);
        accent.anchorMax = new Vector2(0.88f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = new Vector2(0f, -96f);
        accent.sizeDelta = new Vector2(0f, 4f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = _presentation.AccentColor;
        accentImage.raycastTarget = false;

        _title = CreateText(
            _root,
            "txtRevealTitle",
            34f,
            TextAlignmentOptions.Center,
            Color.white);
        _title.rectTransform.anchorMin =
            new Vector2(0.12f, 1f);
        _title.rectTransform.anchorMax =
            new Vector2(0.88f, 1f);
        _title.rectTransform.pivot =
            new Vector2(0.5f, 1f);
        _title.rectTransform.anchoredPosition =
            new Vector2(0f, -42f);
        _title.rectTransform.sizeDelta =
            new Vector2(0f, 48f);
        _title.fontStyle = FontStyles.Bold;

        GameObject rowsObject = GetOrCreateUiObject(
            _root,
            "grpRevealRows",
            typeof(VerticalLayoutGroup));
        _rowsContainer =
            (RectTransform)rowsObject.transform;
        _rowsContainer.anchorMin = new Vector2(0.12f, 0.16f);
        _rowsContainer.anchorMax = new Vector2(0.88f, 0.84f);
        _rowsContainer.offsetMin = Vector2.zero;
        _rowsContainer.offsetMax = Vector2.zero;
        _rowsLayout =
            rowsObject.GetComponent<VerticalLayoutGroup>();
        _rowsLayout.padding = new RectOffset(0, 0, 0, 0);
        _rowsLayout.childAlignment = TextAnchor.MiddleCenter;
        _rowsLayout.childControlWidth = true;
        _rowsLayout.childControlHeight = true;
        _rowsLayout.childForceExpandWidth = true;
        _rowsLayout.childForceExpandHeight = false;

        for (int index = 0;
             index < _presentation.MaximumRows;
             index++)
        {
            _rows.Add(RecruitFlipRowView.Build(
                _rowsContainer,
                index,
                _presentation));
        }

        _instruction = CreateText(
            _root,
            "txtRevealInstruction",
            17f,
            TextAlignmentOptions.Center,
            _presentation.NeutralTextColor);
        _instruction.rectTransform.anchorMin =
            new Vector2(0.18f, 0f);
        _instruction.rectTransform.anchorMax =
            new Vector2(0.82f, 0f);
        _instruction.rectTransform.pivot =
            new Vector2(0.5f, 0f);
        _instruction.rectTransform.anchoredPosition =
            new Vector2(0f, 42f);
        _instruction.rectTransform.sizeDelta =
            new Vector2(0f, 36f);

        _skipButton = BuildButton(
            _root,
            "btnRevealSkip",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-42f, -38f),
            new Vector2(142f, 52f),
            new Color(0.10f, 0.14f, 0.15f, 0.96f),
            out _skipLabel);
        _confirmButton = BuildButton(
            _root,
            "btnRevealConfirm",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 34f),
            new Vector2(220f, 58f),
            new Color(0.16f, 0.48f, 0.43f, 0.98f),
            out _confirmLabel);

        _designerBindings =
            rootObject.GetComponent<RecruitRevealDesignerBindings>();
        _designerBindings.CaptureReferencesFromHierarchy();
    }

    private void WireButtons()
    {
        _backdropButton.onClick.RemoveAllListeners();
        _backdropButton.onClick.AddListener(
            HandleBackdropClicked);
        _skipButton.onClick.RemoveAllListeners();
        _skipButton.onClick.AddListener(
            CompleteImmediately);
        _confirmButton.onClick.RemoveAllListeners();
        _confirmButton.onClick.AddListener(Close);
    }

    private static Button BuildButton(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        Color color,
        out TextMeshProUGUI label)
    {
        GameObject buttonObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        RectTransform rect =
            (RectTransform)buttonObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.8f, 0.7f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);

        label = CreateText(
            rect,
            "txtLabel",
            19f,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyles.Bold;
        return button;
    }

    private static GameObject GetOrCreateUiObject(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent.Find(objectName);
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = new GameObject(
                objectName,
                typeof(RectTransform));
            target.transform.SetParent(parent, false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    target,
                    "Create Recruit Reveal Designer UI");
            }
#endif
        }

        for (int index = 0;
             index < componentTypes.Length;
             index++)
        {
            Type type = componentTypes[index];
            if (target.GetComponent(type) == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.AddComponent(target, type);
                else
                    target.AddComponent(type);
#else
                target.AddComponent(type);
#endif
            }
        }
        return target;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        LocalizationFontResolver.ApplyGameDefault(text);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private sealed class RecruitFlipRowView
    {
        private readonly int _index;
        private RectTransform _root;
        private LayoutElement _layout;
        private Image _background;
        private Outline _outline;
        private Image _accent;
        private Image _rewardIcon;
        private TextMeshProUGUI _indexLabel;
        private TextMeshProUGUI _baseLabel;
        private RectTransform _flap;
        private Image _flapImage;
        private TextMeshProUGUI _flapLabel;
        private Image _splitLine;

        private RecruitFlipRowView(int index)
        {
            _index = index;
        }

        public static RecruitFlipRowView Build(
            Transform parent,
            int index,
            RecruitRevealPresentationSO presentation)
        {
            RecruitFlipRowView row =
                new RecruitFlipRowView(index);
            row.BuildLayout(parent, presentation);
            return row;
        }

        public static RecruitFlipRowView Bind(
            RectTransform root,
            int index)
        {
            if (root == null)
                return null;
            RecruitFlipRowView row =
                new RecruitFlipRowView(index);
            return row.TryBindLayout(root)
                ? row
                : null;
        }

        public void SetActive(bool active)
        {
            _root.gameObject.SetActive(active);
        }

        public void Configure(
            int index,
            float height,
            bool single,
            RecruitRevealPresentationSO presentation)
        {
            _layout.preferredHeight = height;
            _layout.minHeight = height;
            _layout.flexibleHeight = 0f;
            _root.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                height);
            _root.localScale = Vector3.one;
            _flap.localScale = Vector3.one;
            _indexLabel.text = $"{index + 1:00}";
            _indexLabel.fontSize = single ? 25f : 18f;
            _baseLabel.fontSize = single ? 34f : 22f;
            _flapLabel.fontSize = single ? 34f : 22f;
            ApplyNeutralStyle(presentation);
            SetLabels("— — —");
        }

        public IEnumerator Play(
            RecruitRevealEntry entry,
            IReadOnlyList<string> flipLabels,
            RecruitRevealPresentationSO presentation,
            bool korean,
            Action completed)
        {
            ApplyNeutralStyle(presentation);
            float elapsed = 0f;
            string current = GetRandomFlipLabel(flipLabels);
            SetLabels(current);

            while (elapsed < presentation.RowSpinDuration)
            {
                float duration = Mathf.Min(
                    presentation.FlipStepDuration,
                    presentation.RowSpinDuration - elapsed);
                string next = GetRandomFlipLabel(flipLabels);
                yield return FlipTo(next, duration);
                elapsed += duration;
            }

            ShowFinal(entry, presentation, korean);
            yield return Pulse(
                presentation.FinalPulseDuration);
            completed?.Invoke();
        }

        public void ShowFinal(
            RecruitRevealEntry entry,
            RecruitRevealPresentationSO presentation,
            bool korean)
        {
            _root.localScale = Vector3.one;
            _flap.localScale = Vector3.one;
            CharacterGradeStyle style =
                CharacterGradePresentation.GetStyle(entry.Grade);
            _background.color = style.BackgroundColor;
            _flapImage.color = style.BackgroundColor;
            _outline.effectColor = style.OutlineColor;
            _accent.color = style.PrimaryColor;
            _indexLabel.color = style.PrimaryColor;
            _baseLabel.color = style.TextColor;
            _flapLabel.color = style.TextColor;
            if (_rewardIcon != null)
            {
                Sprite displayIcon = entry.Icon != null
                    ? entry.Icon
                    : style.GradeIcon;
                _rewardIcon.sprite = displayIcon;
                _rewardIcon.enabled = displayIcon != null;
                _rewardIcon.preserveAspect = true;
            }

            string grade = korean
                ? CharacterGradePresentation.GetLabel(entry.Grade)
                : $"GRADE {(int)entry.Grade}";
            string newBadge = entry.IsNew
                ? (korean ? "  신규" : "  NEW")
                : string.Empty;
            string amount = entry.RewardType == RecruitRewardType.Item
                ? $"  ×{entry.Amount:N0}"
                : string.Empty;
            SetLabels(
                $"{entry.DisplayName}{amount}    {grade}{newBadge}");
        }

        private IEnumerator FlipTo(string next, float duration)
        {
            if (duration <= 0f)
            {
                SetLabels(next);
                yield break;
            }

            float half = duration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float scale = 1f -
                              Mathf.Clamp01(elapsed / half);
                SetFlapScale(scale);
                yield return null;
            }

            SetLabels(next);
            SetFlapScale(0f);
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float scale = Mathf.Clamp01(elapsed / half);
                SetFlapScale(scale);
                yield return null;
            }
            SetFlapScale(1f);
        }

        private IEnumerator Pulse(float duration)
        {
            if (duration <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized =
                    Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(normalized * Mathf.PI);
                _root.localScale =
                    Vector3.one * (1f + pulse * 0.035f);
                yield return null;
            }
            _root.localScale = Vector3.one;
        }

        private void ApplyNeutralStyle(
            RecruitRevealPresentationSO presentation)
        {
            _background.color = presentation.NeutralRowColor;
            _flapImage.color = presentation.NeutralRowColor;
            _outline.effectColor =
                presentation.NeutralOutlineColor;
            _accent.color = presentation.AccentColor;
            _indexLabel.color = presentation.AccentColor;
            _baseLabel.color = presentation.NeutralTextColor;
            _flapLabel.color = presentation.NeutralTextColor;
            if (_rewardIcon != null)
            {
                _rewardIcon.sprite = null;
                _rewardIcon.enabled = false;
            }
            _splitLine.color = new Color(
                presentation.NeutralOutlineColor.r,
                presentation.NeutralOutlineColor.g,
                presentation.NeutralOutlineColor.b,
                0.7f);
        }

        private void SetLabels(string label)
        {
            string value = string.IsNullOrWhiteSpace(label)
                ? "— — —"
                : label;
            _baseLabel.text = value;
            _flapLabel.text = value;
        }

        private void SetFlapScale(float scale)
        {
            Vector3 next = _flap.localScale;
            next.y = Mathf.Clamp01(scale);
            _flap.localScale = next;
        }

        private static string GetRandomFlipLabel(
            IReadOnlyList<string> labels)
        {
            if (labels == null || labels.Count == 0)
                return "RECRUIT";
            int index = UnityEngine.Random.Range(0, labels.Count);
            return labels[index];
        }

        private bool TryBindLayout(RectTransform root)
        {
            _root = root;
            _layout = root.GetComponent<LayoutElement>();
            _background = root.GetComponent<Image>();
            _outline = root.GetComponent<Outline>();
            _accent = root.Find("imgRowAccent")
                ?.GetComponent<Image>();
            _rewardIcon = root.Find("imgRewardIcon")
                ?.GetComponent<Image>();
            _indexLabel = root.Find("txtRowIndex")
                ?.GetComponent<TextMeshProUGUI>();
            _baseLabel = root.Find("txtRowBase")
                ?.GetComponent<TextMeshProUGUI>();
            _flap = root.Find("grpRowFlap") as RectTransform;
            _flapImage = _flap != null
                ? _flap.GetComponent<Image>()
                : null;
            _flapLabel = _flap != null
                ? _flap.Find("txtRowFlap")
                    ?.GetComponent<TextMeshProUGUI>()
                : null;
            _splitLine = root.Find("imgRowSplit")
                ?.GetComponent<Image>();
            return _layout != null &&
                   _background != null &&
                   _outline != null &&
                   _accent != null &&
                   _indexLabel != null &&
                   _baseLabel != null &&
                   _flap != null &&
                   _flapImage != null &&
                   _flapLabel != null &&
                   _splitLine != null;
        }

        public void EnsureRewardIconSlot()
        {
            if (_root == null || _rewardIcon != null)
                return;

            GameObject iconObject = GetOrCreateUiObject(
                _root,
                "imgRewardIcon",
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform iconRect =
                (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(110f, 0f);
            iconRect.sizeDelta = new Vector2(46f, 46f);
            iconRect.SetAsLastSibling();
            _rewardIcon = iconObject.GetComponent<Image>();
            _rewardIcon.raycastTarget = false;
            _rewardIcon.preserveAspect = true;
            _rewardIcon.enabled = false;

            if (_baseLabel != null)
            {
                Vector2 offset = _baseLabel.rectTransform.offsetMin;
                _baseLabel.rectTransform.offsetMin =
                    new Vector2(Mathf.Max(offset.x, 148f), offset.y);
            }
            if (_flapLabel != null)
            {
                Vector2 offset = _flapLabel.rectTransform.offsetMin;
                _flapLabel.rectTransform.offsetMin =
                    new Vector2(Mathf.Max(offset.x, 70f), offset.y);
            }
        }

        private void BuildLayout(
            Transform parent,
            RecruitRevealPresentationSO presentation)
        {
            GameObject rowObject = GetOrCreateUiObject(
                parent,
                $"grpRevealRow{_index + 1:00}",
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(LayoutElement));
            _root = (RectTransform)rowObject.transform;
            _layout = rowObject.GetComponent<LayoutElement>();
            _layout.flexibleWidth = 1f;
            _background = rowObject.GetComponent<Image>();
            _background.raycastTarget = false;
            _outline = rowObject.GetComponent<Outline>();
            _outline.effectDistance = new Vector2(1f, -1f);

            GameObject accentObject = GetOrCreateUiObject(
                _root,
                "imgRowAccent",
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform accentRect =
                (RectTransform)accentObject.transform;
            accentRect.anchorMin = Vector2.zero;
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(7f, 0f);
            _accent = accentObject.GetComponent<Image>();
            _accent.raycastTarget = false;

            _indexLabel = CreateText(
                _root,
                "txtRowIndex",
                18f,
                TextAlignmentOptions.Center,
                presentation.AccentColor);
            _indexLabel.rectTransform.anchorMin =
                new Vector2(0f, 0f);
            _indexLabel.rectTransform.anchorMax =
                new Vector2(0f, 1f);
            _indexLabel.rectTransform.pivot =
                new Vector2(0f, 0.5f);
            _indexLabel.rectTransform.anchoredPosition =
                new Vector2(16f, 0f);
            _indexLabel.rectTransform.sizeDelta =
                new Vector2(62f, 0f);
            _indexLabel.fontStyle = FontStyles.Bold;

            _baseLabel = CreateText(
                _root,
                "txtRowBase",
                22f,
                TextAlignmentOptions.Center,
                presentation.NeutralTextColor);
            Stretch(_baseLabel.rectTransform);
            _baseLabel.rectTransform.offsetMin =
                new Vector2(86f, 4f);
            _baseLabel.rectTransform.offsetMax =
                new Vector2(-24f, -4f);
            _baseLabel.fontStyle = FontStyles.Bold;

            GameObject flapObject = GetOrCreateUiObject(
                _root,
                "grpRowFlap",
                typeof(CanvasRenderer),
                typeof(Image));
            _flap = (RectTransform)flapObject.transform;
            Stretch(_flap);
            _flap.offsetMin = new Vector2(78f, 0f);
            _flap.offsetMax = Vector2.zero;
            _flap.pivot = new Vector2(0.5f, 0.5f);
            _flapImage = flapObject.GetComponent<Image>();
            _flapImage.raycastTarget = false;

            _flapLabel = CreateText(
                _flap,
                "txtRowFlap",
                22f,
                TextAlignmentOptions.Center,
                presentation.NeutralTextColor);
            Stretch(_flapLabel.rectTransform);
            _flapLabel.rectTransform.offsetMin =
                new Vector2(8f, 4f);
            _flapLabel.rectTransform.offsetMax =
                new Vector2(-24f, -4f);
            _flapLabel.fontStyle = FontStyles.Bold;

            GameObject splitObject = GetOrCreateUiObject(
                _root,
                "imgRowSplit",
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform split =
                (RectTransform)splitObject.transform;
            split.anchorMin = new Vector2(0f, 0.5f);
            split.anchorMax = new Vector2(1f, 0.5f);
            split.pivot = new Vector2(0.5f, 0.5f);
            split.anchoredPosition = Vector2.zero;
            split.sizeDelta = new Vector2(0f, 2f);
            _splitLine = splitObject.GetComponent<Image>();
            _splitLine.raycastTarget = false;

            EnsureRewardIconSlot();
            ApplyNeutralStyle(presentation);
        }
    }
}
