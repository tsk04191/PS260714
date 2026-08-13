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
        return BuildInternal(runner, host);
    }

#if UNITY_EDITOR
    public static RecruitRevealOverlay BuildEditor(
        MonoBehaviour runner,
        Transform host)
    {
        return BuildInternal(runner, host);
    }
#endif

    private static RecruitRevealOverlay BuildInternal(
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

            throw new InvalidOperationException(
                "The saved recruit reveal UI is missing. Repair the Scene " +
                "hierarchy and designer bindings.");
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
            return false;

        if (!_designerBindings.HasRequiredReferences &&
            !_designerBindings.CaptureReferencesFromHierarchy())
        {
            return false;
        }
        if (!_designerBindings.HasDesignerLayout)
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

    }
}
