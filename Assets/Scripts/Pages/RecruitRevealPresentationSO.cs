using UnityEngine;

[CreateAssetMenu(
    fileName = "RecruitRevealPresentation",
    menuName = "PS260714/Recruit/Reveal Presentation")]
public sealed class RecruitRevealPresentationSO : ScriptableObject
{
    private const string ResourcePath =
        "Presentation/RecruitRevealPresentation";

    private static RecruitRevealPresentationSO _cached;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float introFadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float rowStartInterval = 1.5f;
    [SerializeField, Min(0.1f)] private float rowSpinDuration = 2f;
    [SerializeField, Range(0.04f, 0.3f)]
    private float flipStepDuration = 0.09f;
    [SerializeField, Min(0f)] private float skipEnableDelay = 0.5f;
    [SerializeField, Min(0f)] private float finalPulseDuration = 0.22f;

    [Header("Layout")]
    [SerializeField, Range(1, 10)] private int maximumRows = 10;
    [SerializeField, Min(36f)] private float multiRowHeight = 56f;
    [SerializeField, Min(48f)] private float singleRowHeight = 96f;
    [SerializeField, Min(0f)] private float rowSpacing = 7f;

    [Header("Colors")]
    [SerializeField] private Color backdropColor =
        new(0.012f, 0.016f, 0.018f, 0.97f);
    [SerializeField] private Color neutralRowColor =
        new(0.055f, 0.07f, 0.075f, 1f);
    [SerializeField] private Color neutralOutlineColor =
        new(0.22f, 0.3f, 0.31f, 0.9f);
    [SerializeField] private Color neutralTextColor =
        new(0.82f, 0.88f, 0.87f, 1f);
    [SerializeField] private Color accentColor =
        new(0.25f, 0.76f, 0.68f, 1f);

    public float IntroFadeDuration => Mathf.Max(0f, introFadeDuration);
    public float RowStartInterval => Mathf.Max(0f, rowStartInterval);
    public float RowSpinDuration => Mathf.Max(0.1f, rowSpinDuration);
    public float FlipStepDuration =>
        Mathf.Clamp(flipStepDuration, 0.04f, 0.3f);
    public float SkipEnableDelay => Mathf.Max(0f, skipEnableDelay);
    public float FinalPulseDuration =>
        Mathf.Max(0f, finalPulseDuration);
    public int MaximumRows => Mathf.Clamp(maximumRows, 1, 10);
    public float MultiRowHeight => Mathf.Max(36f, multiRowHeight);
    public float SingleRowHeight => Mathf.Max(48f, singleRowHeight);
    public float RowSpacing => Mathf.Max(0f, rowSpacing);
    public Color BackdropColor => backdropColor;
    public Color NeutralRowColor => neutralRowColor;
    public Color NeutralOutlineColor => neutralOutlineColor;
    public Color NeutralTextColor => neutralTextColor;
    public Color AccentColor => accentColor;

    public float GetMinimumRevealDuration(int rowCount)
    {
        int count = Mathf.Clamp(rowCount, 1, MaximumRows);
        return (count - 1) * RowStartInterval + RowSpinDuration;
    }

    public float GetFittedMultiRowHeight(
        float availableHeight,
        int rowCount,
        out float fittedSpacing)
    {
        int count = Mathf.Clamp(rowCount, 1, MaximumRows);
        float safeHeight = Mathf.Max(0f, availableHeight);
        if (count <= 1)
        {
            fittedSpacing = 0f;
            return Mathf.Min(MultiRowHeight, safeHeight);
        }

        fittedSpacing = Mathf.Min(
            RowSpacing,
            safeHeight / (count * 3f));
        float rowsHeight = Mathf.Max(
            0f,
            safeHeight - fittedSpacing * (count - 1));
        return Mathf.Min(
            MultiRowHeight,
            rowsHeight / count);
    }

    public static RecruitRevealPresentationSO Load()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<RecruitRevealPresentationSO>(
            ResourcePath);
        if (_cached != null)
            return _cached;

        _cached = CreateInstance<RecruitRevealPresentationSO>();
        _cached.hideFlags = HideFlags.HideAndDontSave;
        return _cached;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _cached = null;
    }

    private void OnValidate()
    {
        introFadeDuration = Mathf.Max(0f, introFadeDuration);
        rowStartInterval = Mathf.Max(0f, rowStartInterval);
        rowSpinDuration = Mathf.Max(0.1f, rowSpinDuration);
        flipStepDuration = Mathf.Clamp(
            flipStepDuration,
            0.04f,
            0.3f);
        skipEnableDelay = Mathf.Max(0f, skipEnableDelay);
        finalPulseDuration = Mathf.Max(0f, finalPulseDuration);
        maximumRows = Mathf.Clamp(maximumRows, 1, 10);
        multiRowHeight = Mathf.Max(36f, multiRowHeight);
        singleRowHeight = Mathf.Max(48f, singleRowHeight);
        rowSpacing = Mathf.Max(0f, rowSpacing);
    }
}
