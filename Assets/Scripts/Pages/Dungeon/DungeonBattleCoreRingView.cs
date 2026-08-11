using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonBattleCoreWorldGaugeView : MonoBehaviour
{
    [SerializeField] private DungeonWorldPolylineRenderer track;
    [SerializeField] private DungeonWorldPolylineRenderer delayedFill;
    [SerializeField] private DungeonWorldPolylineRenderer healthFill;

    private float _arenaRadius = BattleArenaSetup.DefaultWorldRadius;
    private float _targetProgress = 1f;
    private float _displayProgress = 1f;
    private float _delayedProgress = 1f;
    private float _damageDelayRemaining;
    private bool _hasHealth;

    public void SetArenaRadius(float radius)
    {
        _arenaRadius = BattleArenaSetup.NormalizeWorldRadius(radius);
        RefreshGeometry();
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
        if (visible)
            RefreshGeometry();
    }

    public void SetHealth(int current, int maximum, bool animate)
    {
        int resolvedMaximum = Mathf.Max(1, maximum);
        int resolvedCurrent = Mathf.Clamp(current, 0, resolvedMaximum);
        float progress = resolvedCurrent / (float)resolvedMaximum;

        if (!_hasHealth || !animate)
        {
            _targetProgress = progress;
            _displayProgress = progress;
            _delayedProgress = progress;
            _damageDelayRemaining = 0f;
            _hasHealth = true;
            RefreshGeometry();
            return;
        }

        if (Mathf.Approximately(_targetProgress, progress))
            return;

        if (progress < _targetProgress)
        {
            _damageDelayRemaining = DungeonHudPresentation.Load()
                .BattleCoreRingDamageDelay;
        }
        else
        {
            _delayedProgress = progress;
            _damageDelayRemaining = 0f;
        }

        _targetProgress = progress;
        RefreshGeometry();
    }

    private void Awake()
    {
        track?.SetSortingOrder(-32000);
        delayedFill?.SetSortingOrder(-31999);
        healthFill?.SetSortingOrder(-31998);
    }

    private void OnEnable()
    {
        RefreshGeometry();
    }

    private void Update()
    {
        if (!_hasHealth)
            return;

        DungeonHudPresentationSO style = DungeonHudPresentation.Load();
        float deltaTime = Time.unscaledDeltaTime;
        float previousDisplay = _displayProgress;
        float previousDelayed = _delayedProgress;

        _displayProgress = Mathf.MoveTowards(
            _displayProgress,
            _targetProgress,
            deltaTime / style.BattleCoreRingAnimationDuration);

        if (_damageDelayRemaining > 0f)
        {
            _damageDelayRemaining = Mathf.Max(
                0f,
                _damageDelayRemaining - deltaTime);
        }
        else
        {
            _delayedProgress = Mathf.MoveTowards(
                _delayedProgress,
                _targetProgress,
                deltaTime / style.BattleCoreRingDelayedDuration);
        }

        if (!Mathf.Approximately(previousDisplay, _displayProgress) ||
            !Mathf.Approximately(previousDelayed, _delayedProgress))
        {
            RefreshGeometry();
        }
    }

    private void RefreshGeometry()
    {
        if (track == null || delayedFill == null || healthFill == null)
            return;

        DungeonHudPresentationSO style = DungeonHudPresentation.Load();
        float radius = _arenaRadius + style.BattleCoreRingGap;
        float width = style.BattleCoreRingThickness;
        float height = style.BattleCoreRingGroundHeight;
        int segments = style.BattleCoreRingSegments;
        float startAngle = style.BattleCoreRingStartAngle;
        float sweepAngle = style.BattleCoreRingSweepAngle;
        bool clockwise = style.BattleCoreRingClockwise;

        track.SetRing(
            radius,
            1f,
            width + 0.018f,
            style.BattleCoreRingTrackColor,
            new Vector3(0f, height, 0f),
            segments,
            startAngle,
            sweepAngle,
            clockwise);

        if (_delayedProgress > _displayProgress + 0.0001f)
        {
            delayedFill.SetRing(
                radius,
                _delayedProgress,
                width,
                style.BattleCoreRingDelayedColor,
                new Vector3(0f, height + 0.001f, 0f),
                segments,
                startAngle,
                sweepAngle,
                clockwise);
        }
        else
        {
            delayedFill.SetVisible(false);
        }

        healthFill.SetRing(
            radius,
            _displayProgress,
            width,
            ResolveHealthColor(style, _displayProgress),
            new Vector3(0f, height + 0.002f, 0f),
            segments,
            startAngle,
            sweepAngle,
            clockwise);
    }

    private static Color ResolveHealthColor(
        DungeonHudPresentationSO style,
        float progress)
    {
        float threshold = style.BattleCoreRingCriticalThreshold;
        if (threshold <= 0f || progress >= threshold)
            return style.BattleCoreRingHealthyColor;

        float blend = Mathf.Clamp01(progress / threshold);
        return Color.Lerp(
            style.BattleCoreRingCriticalColor,
            style.BattleCoreRingHealthyColor,
            blend);
    }
}
