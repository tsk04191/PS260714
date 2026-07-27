using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleVfxPlayer : MonoBehaviour, IBattleVfxRequestSink
{
    [Header("World Playback")]
    [SerializeField]
    private Camera worldCamera;
    [SerializeField]
    private Transform spawnRoot;
    [SerializeField, Min(0.01f)]
    private float screenAnchorDepth = 10f;
    [SerializeField]
    private bool useCameraRotationForScreenAnchors = true;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [Header("Quality And Budget")]
    [SerializeField]
    private BattleVfxQualityProfileSO qualityProfile;

    [Header("Diagnostics")]
    [SerializeField]
    private bool logSkippedRequests;

    private readonly List<ActiveVfx> _active = new();
    private readonly List<ScheduledVfx> _scheduled = new();
    private readonly Dictionary<GameObject, Stack<PooledVfx>>
        _pools = new();
    private readonly HashSet<BattleVfxCueSO> _prewarmedCues = new();
    private IBattleVfxTargetResolver _targetResolver;
    private Transform _ownedSpawnRoot;
    private long _nextSequence;
    private int _skippedByQualityCount;
    private int _skippedByActiveBudgetCount;
    private int _skippedByScheduledBudgetCount;

    public Camera WorldCamera => worldCamera != null
        ? worldCamera
        : Camera.main;
    public Transform SpawnRoot => spawnRoot != null
        ? spawnRoot
        : _ownedSpawnRoot;
    public int ActiveInstanceCount => _active.Count;
    public int ScheduledRequestCount => _scheduled.Count;
    public BattleVfxQualityProfileSO QualityProfile => qualityProfile;
    public int SkippedByQualityCount => _skippedByQualityCount;
    public int SkippedByActiveBudgetCount =>
        _skippedByActiveBudgetCount;
    public int SkippedByScheduledBudgetCount =>
        _skippedByScheduledBudgetCount;
    public int SkippedRequestCount =>
        _skippedByQualityCount +
        _skippedByActiveBudgetCount +
        _skippedByScheduledBudgetCount;
    public int PooledInstanceCount
    {
        get
        {
            int count = 0;
            foreach (Stack<PooledVfx> pool in _pools.Values)
                count += pool.Count;
            return count;
        }
    }

    public void Configure(
        Camera playbackCamera,
        Transform playbackRoot = null,
        AudioSource playbackAudioSource = null)
    {
        worldCamera = playbackCamera;
        spawnRoot = playbackRoot;
        if (playbackAudioSource != null)
            audioSource = playbackAudioSource;
    }

    public void BindTargetResolver(IBattleVfxTargetResolver resolver)
    {
        _targetResolver = resolver;
    }

    public void ConfigureQuality(BattleVfxQualityProfileSO profile)
    {
        bool profileChanged = qualityProfile != profile;
        qualityProfile = profile;
        if (profileChanged)
            _prewarmedCues.Clear();
        PruneScheduledByQuality();
        PruneActiveByQuality();
        EnforceActiveBudget();
    }

    public void ResetDiagnostics()
    {
        _skippedByQualityCount = 0;
        _skippedByActiveBudgetCount = 0;
        _skippedByScheduledBudgetCount = 0;
    }

    public void Enqueue(BattleVfxRequest request)
    {
        if (!request.IsValid)
            return;

        if (request.Phase != BattleVfxPhase.StatusLoopStop &&
            !IsCueAllowed(request.Cue))
        {
            _skippedByQualityCount++;
            LogSkipped(
                request.Cue,
                "is below the current quality threshold");
            return;
        }

        if (request.DelaySeconds > 0f)
        {
            if (request.Phase == BattleVfxPhase.StatusLoopStop)
            {
                _scheduled.Add(new ScheduledVfx(
                    request,
                    request.DelaySeconds,
                    ++_nextSequence));
            }
            else
            {
                TrySchedule(request);
            }
            return;
        }

        PlayNow(request);
    }

    private void PlayNow(BattleVfxRequest request)
    {
        if (request.Phase == BattleVfxPhase.StatusLoopStop)
        {
            StopPersistent(request);
            return;
        }

        BattleVfxCueSO cue = request.Cue;
        if (cue.IsPersistent &&
            TryRefreshPersistent(request))
        {
            PlayAudio(cue);
            return;
        }

        if (cue.Prefab == null)
        {
            PlayAudio(cue);
            LogSkipped(cue, "has no prefab");
            return;
        }

        if (!TryResolveAnchor(
                request.Target,
                cue.AnchorType,
                out BattleVfxAnchorSnapshot targetAnchor))
        {
            LogSkipped(cue, "has no valid target anchor");
            return;
        }

        BattleVfxAnchorSnapshot sourceAnchor = default;
        if (cue.HasMotion &&
            !TryResolveAnchor(
                request.SourceTarget,
                cue.MotionSourceAnchorType,
                out sourceAnchor))
        {
            LogSkipped(cue, "has no valid motion source anchor");
            return;
        }

        EnsurePrewarmed(cue);
        EnforceConcurrentLimit(cue);
        if (!TryReserveActiveBudget(cue))
            return;

        PlayAudio(cue);
        PooledVfx pooled = Acquire(cue.Prefab);
        if (pooled == null || pooled.Instance == null)
            return;

        Activate(
            pooled,
            cue,
            cue.HasMotion ? sourceAnchor : targetAnchor);
        _active.Add(new ActiveVfx(
            pooled,
            request,
            Mathf.Max(
                ResolveLifetime(cue, pooled),
                cue.HasMotion ? cue.TravelDuration : 0f),
            sourceAnchor,
            targetAnchor,
            ++_nextSequence));
    }

    public void Advance(
        float battleDeltaTime,
        float unscaledDeltaTime)
    {
        battleDeltaTime = Mathf.Max(0f, battleDeltaTime);
        unscaledDeltaTime = Mathf.Max(0f, unscaledDeltaTime);
        for (int index = _active.Count - 1; index >= 0; index--)
        {
            ActiveVfx active = _active[index];
            if (active == null ||
                active.Pooled == null ||
                active.Pooled.Instance == null ||
                active.Cue == null)
            {
                ReleaseAt(index, true);
                continue;
            }

            float deltaTime = active.Cue.UseBattleTime
                ? battleDeltaTime
                : unscaledDeltaTime;
            if (active.Cue.HasMotion)
            {
                active.MotionElapsed += deltaTime;
                ApplyMotionTransform(active);
            }
            else if (active.Cue.AttachMode ==
                     BattleVfxAttachMode.FollowTarget)
            {
                RefreshFollowAnchor(active);
            }

            if (active.Cue.IsPersistent && !active.IsStopping)
                continue;

            active.RemainingTime -= deltaTime;
            if (active.RemainingTime <= 0f)
                ReleaseAt(index, false);
        }

        AdvanceScheduled(battleDeltaTime, unscaledDeltaTime);
    }

    private void AdvanceScheduled(
        float battleDeltaTime,
        float unscaledDeltaTime)
    {
        for (int index = _scheduled.Count - 1; index >= 0; index--)
        {
            ScheduledVfx scheduled = _scheduled[index];
            float deltaTime = scheduled.Request.DelayUsesBattleTime
                ? battleDeltaTime
                : unscaledDeltaTime;
            scheduled.RemainingTime -= deltaTime;
            if (scheduled.RemainingTime > 0f)
                continue;

            _scheduled.RemoveAt(index);
            PlayNow(scheduled.Request);
        }
    }

    public bool TryGetActiveInstance(
        BattleVfxCueSO cue,
        BattleVfxTargetHandle targetHandle,
        out GameObject instance)
    {
        foreach (ActiveVfx active in _active)
        {
            if (active != null &&
                active.Cue == cue &&
                active.TargetHandle == targetHandle &&
                active.Pooled?.Instance != null)
            {
                instance = active.Pooled.Instance;
                return true;
            }
        }

        instance = null;
        return false;
    }

    public void ClearActive()
    {
        _scheduled.Clear();
        for (int index = _active.Count - 1; index >= 0; index--)
            ReleaseAt(index, true);
    }

    public void ClearPool()
    {
        foreach (Stack<PooledVfx> pool in _pools.Values)
        {
            while (pool.Count > 0)
            {
                PooledVfx pooled = pool.Pop();
                if (pooled?.Instance != null)
                    DestroySafely(pooled.Instance);
            }
        }

        _pools.Clear();
        _prewarmedCues.Clear();
    }

    private void Update()
    {
        Advance(Time.deltaTime, Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        ClearActive();
    }

    private void OnDestroy()
    {
        ClearActive();
        ClearPool();
        if (_ownedSpawnRoot != null)
            DestroySafely(_ownedSpawnRoot.gameObject);
        _ownedSpawnRoot = null;
    }

    private void OnValidate()
    {
        if (float.IsNaN(screenAnchorDepth) ||
            float.IsInfinity(screenAnchorDepth))
        {
            screenAnchorDepth = 10f;
        }

        screenAnchorDepth = Mathf.Max(0.01f, screenAnchorDepth);
    }

    private bool TryRefreshPersistent(BattleVfxRequest request)
    {
        ActiveVfx existing = FindPersistent(request);
        if (existing == null)
            return false;

        existing.Request = request;
        existing.IsStopping = false;
        existing.RemainingTime = float.PositiveInfinity;
        if (TryResolveAnchor(
                request.Target,
                request.Cue.AnchorType,
                out BattleVfxAnchorSnapshot anchor))
        {
            ApplyTransform(existing.Pooled, request.Cue, anchor);
        }
        Restart(existing.Pooled);
        return true;
    }

    private void StopPersistent(BattleVfxRequest request)
    {
        for (int index = _active.Count - 1; index >= 0; index--)
        {
            ActiveVfx active = _active[index];
            if (!MatchesPersistent(active, request))
                continue;

            if (active.Cue.StopMode == BattleVfxStopMode.Immediate)
            {
                ReleaseAt(index, true);
                continue;
            }

            StopEmission(active.Pooled);
            active.IsStopping = true;
            active.RemainingTime = Mathf.Max(
                0.01f,
                active.Cue.Duration);
        }
    }

    private ActiveVfx FindPersistent(BattleVfxRequest request)
    {
        foreach (ActiveVfx active in _active)
        {
            if (MatchesPersistent(active, request))
                return active;
        }

        return null;
    }

    private static bool MatchesPersistent(
        ActiveVfx active,
        BattleVfxRequest request)
    {
        if (active == null || active.Cue != request.Cue)
            return false;
        if (active.TargetHandle.IsValid || request.Target.Handle.IsValid)
            return active.TargetHandle == request.Target.Handle;
        return ReferenceEquals(
            active.TargetIdentity,
            GetTargetIdentity(request.Target.BattleTarget));
    }

    private void RefreshFollowAnchor(ActiveVfx active)
    {
        if (!TryResolveAnchor(
                active.Request.Target,
                active.Cue.AnchorType,
                out BattleVfxAnchorSnapshot anchor))
        {
            return;
        }

        ApplyTransform(active.Pooled, active.Cue, anchor);
    }

    private bool TryResolveAnchor(
        BattleVfxTarget target,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot anchor)
    {
        if (_targetResolver != null &&
            target.BattleTarget.IsValid)
        {
            BattleVfxTarget resolved = _targetResolver.ResolveVfxTarget(
                target.BattleTarget,
                anchorType);
            if (resolved.HasAnchor)
            {
                anchor = resolved.Anchor;
                return true;
            }
        }

        anchor = target.Anchor;
        return anchor.IsValid;
    }

    private void EnsurePrewarmed(BattleVfxCueSO cue)
    {
        if (cue == null || cue.Prefab == null ||
            !_prewarmedCues.Add(cue))
        {
            return;
        }

        int requestedCount = cue.PrewarmCount;
        if (qualityProfile != null)
        {
            requestedCount = Mathf.FloorToInt(
                requestedCount * qualityProfile.PrewarmScale);
        }

        int count = Mathf.Min(
            requestedCount,
            cue.MaximumConcurrent);
        if (qualityProfile != null)
        {
            count = Mathf.Min(
                count,
                qualityProfile.MaximumActiveInstances);
        }
        Stack<PooledVfx> pool = GetPool(cue.Prefab);
        int missingCount = Mathf.Max(0, count - pool.Count);
        for (int index = 0; index < missingCount; index++)
        {
            PooledVfx pooled = CreatePooled(cue.Prefab);
            if (pooled != null)
                pool.Push(pooled);
        }
    }

    private void EnforceConcurrentLimit(BattleVfxCueSO cue)
    {
        while (CountActive(cue) >= cue.MaximumConcurrent)
        {
            int oldestIndex = FindOldestActiveIndex(cue);
            if (oldestIndex < 0)
                return;
            ReleaseAt(oldestIndex, true);
        }
    }

    private int CountActive(BattleVfxCueSO cue)
    {
        int count = 0;
        foreach (ActiveVfx active in _active)
        {
            if (active?.Cue == cue)
                count++;
        }
        return count;
    }

    private int FindOldestActiveIndex(BattleVfxCueSO cue)
    {
        int oldestIndex = -1;
        long oldestSequence = long.MaxValue;
        for (int index = 0; index < _active.Count; index++)
        {
            ActiveVfx active = _active[index];
            if (active?.Cue == cue &&
                active.Sequence < oldestSequence)
            {
                oldestIndex = index;
                oldestSequence = active.Sequence;
            }
        }
        return oldestIndex;
    }

    private bool IsCueAllowed(BattleVfxCueSO cue)
    {
        return cue != null &&
               (qualityProfile == null ||
                cue.Importance >= qualityProfile.MinimumImportance);
    }

    private void TrySchedule(BattleVfxRequest request)
    {
        if (qualityProfile == null ||
            _scheduled.Count <
            qualityProfile.MaximumScheduledRequests)
        {
            _scheduled.Add(new ScheduledVfx(
                request,
                request.DelaySeconds,
                ++_nextSequence));
            return;
        }

        int replaceIndex = FindScheduledReplacementIndex(
            request.Cue.Importance);
        if (replaceIndex < 0)
        {
            _skippedByScheduledBudgetCount++;
            LogSkipped(
                request.Cue,
                "exceeds the scheduled request budget");
            return;
        }

        BattleVfxCueSO replacedCue =
            _scheduled[replaceIndex].Request.Cue;
        _scheduled.RemoveAt(replaceIndex);
        _skippedByScheduledBudgetCount++;
        LogSkipped(
            replacedCue,
            "was replaced by a higher-priority scheduled request");
        _scheduled.Add(new ScheduledVfx(
            request,
            request.DelaySeconds,
            ++_nextSequence));
    }

    private int FindScheduledReplacementIndex(
        BattleVfxImportance incomingImportance)
    {
        int candidateIndex = -1;
        BattleVfxImportance candidateImportance =
            BattleVfxImportance.Critical;
        long candidateSequence = long.MaxValue;
        for (int index = 0; index < _scheduled.Count; index++)
        {
            ScheduledVfx scheduled = _scheduled[index];
            if (scheduled.Request.Phase ==
                BattleVfxPhase.StatusLoopStop)
            {
                continue;
            }

            BattleVfxImportance importance =
                scheduled.Request.Cue.Importance;
            if (importance >= incomingImportance)
                continue;
            if (candidateIndex >= 0 &&
                (importance > candidateImportance ||
                 importance == candidateImportance &&
                 scheduled.Sequence >= candidateSequence))
            {
                continue;
            }

            candidateIndex = index;
            candidateImportance = importance;
            candidateSequence = scheduled.Sequence;
        }

        return candidateIndex;
    }

    private bool TryReserveActiveBudget(BattleVfxCueSO incomingCue)
    {
        if (qualityProfile == null ||
            _active.Count < qualityProfile.MaximumActiveInstances)
        {
            return true;
        }

        int replaceIndex = FindActiveReplacementIndex(
            incomingCue.Importance);
        if (replaceIndex < 0)
        {
            _skippedByActiveBudgetCount++;
            LogSkipped(
                incomingCue,
                "exceeds the active instance budget");
            return false;
        }

        ReleaseAt(replaceIndex, true);
        return true;
    }

    private int FindActiveReplacementIndex(
        BattleVfxImportance incomingImportance)
    {
        int candidateIndex = -1;
        BattleVfxImportance candidateImportance =
            BattleVfxImportance.Critical;
        long candidateSequence = long.MaxValue;
        for (int index = 0; index < _active.Count; index++)
        {
            ActiveVfx active = _active[index];
            if (active?.Cue == null)
                continue;

            BattleVfxImportance importance = active.Cue.Importance;
            if (importance >= incomingImportance)
                continue;
            if (candidateIndex >= 0 &&
                (importance > candidateImportance ||
                 importance == candidateImportance &&
                 active.Sequence >= candidateSequence))
            {
                continue;
            }

            candidateIndex = index;
            candidateImportance = importance;
            candidateSequence = active.Sequence;
        }

        return candidateIndex;
    }

    private void PruneScheduledByQuality()
    {
        if (qualityProfile == null)
            return;

        for (int index = _scheduled.Count - 1; index >= 0; index--)
        {
            BattleVfxCueSO cue = _scheduled[index].Request.Cue;
            if (_scheduled[index].Request.Phase ==
                BattleVfxPhase.StatusLoopStop)
            {
                continue;
            }
            if (IsCueAllowed(cue))
                continue;

            _scheduled.RemoveAt(index);
            _skippedByQualityCount++;
            LogSkipped(cue, "is below the current quality threshold");
        }

        while (_scheduled.Count >
               qualityProfile.MaximumScheduledRequests)
        {
            int removeIndex = FindLowestScheduledIndex();
            if (removeIndex < 0)
                break;
            BattleVfxCueSO cue = _scheduled[removeIndex].Request.Cue;
            _scheduled.RemoveAt(removeIndex);
            _skippedByScheduledBudgetCount++;
            LogSkipped(cue, "exceeds the scheduled request budget");
        }
    }

    private int FindLowestScheduledIndex()
    {
        int candidateIndex = -1;
        BattleVfxImportance candidateImportance =
            BattleVfxImportance.Critical;
        long candidateSequence = long.MaxValue;
        for (int index = 0; index < _scheduled.Count; index++)
        {
            ScheduledVfx scheduled = _scheduled[index];
            if (scheduled.Request.Phase ==
                BattleVfxPhase.StatusLoopStop)
            {
                continue;
            }

            BattleVfxImportance importance =
                scheduled.Request.Cue.Importance;
            if (candidateIndex >= 0 &&
                (importance > candidateImportance ||
                 importance == candidateImportance &&
                 scheduled.Sequence >= candidateSequence))
            {
                continue;
            }

            candidateIndex = index;
            candidateImportance = importance;
            candidateSequence = scheduled.Sequence;
        }

        return candidateIndex;
    }

    private void EnforceActiveBudget()
    {
        if (qualityProfile == null)
            return;

        while (_active.Count >
               qualityProfile.MaximumActiveInstances)
        {
            int removeIndex = FindLowestActiveIndex();
            if (removeIndex < 0)
                return;
            ReleaseAt(removeIndex, true);
        }
    }

    private void PruneActiveByQuality()
    {
        if (qualityProfile == null)
            return;

        for (int index = _active.Count - 1; index >= 0; index--)
        {
            BattleVfxCueSO cue = _active[index]?.Cue;
            if (IsCueAllowed(cue))
                continue;

            ReleaseAt(index, true);
            _skippedByQualityCount++;
            LogSkipped(cue, "is below the current quality threshold");
        }
    }

    private int FindLowestActiveIndex()
    {
        int candidateIndex = -1;
        BattleVfxImportance candidateImportance =
            BattleVfxImportance.Critical;
        long candidateSequence = long.MaxValue;
        for (int index = 0; index < _active.Count; index++)
        {
            ActiveVfx active = _active[index];
            if (active?.Cue == null)
                continue;

            BattleVfxImportance importance = active.Cue.Importance;
            if (candidateIndex >= 0 &&
                (importance > candidateImportance ||
                 importance == candidateImportance &&
                 active.Sequence >= candidateSequence))
            {
                continue;
            }

            candidateIndex = index;
            candidateImportance = importance;
            candidateSequence = active.Sequence;
        }

        return candidateIndex;
    }

    private PooledVfx Acquire(GameObject prefab)
    {
        Stack<PooledVfx> pool = GetPool(prefab);
        while (pool.Count > 0)
        {
            PooledVfx pooled = pool.Pop();
            if (pooled?.Instance != null)
                return pooled;
        }

        return CreatePooled(prefab);
    }

    private Stack<PooledVfx> GetPool(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out Stack<PooledVfx> pool))
        {
            pool = new Stack<PooledVfx>();
            _pools.Add(prefab, pool);
        }
        return pool;
    }

    private PooledVfx CreatePooled(GameObject prefab)
    {
        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, GetOrCreateSpawnRoot());
        instance.name = $"{prefab.name} (Battle VFX)";
        PooledVfx pooled = new(instance, prefab);
        StopAndClear(pooled);
        instance.SetActive(false);
        return pooled;
    }

    private void Activate(
        PooledVfx pooled,
        BattleVfxCueSO cue,
        BattleVfxAnchorSnapshot anchor)
    {
        pooled.Instance.transform.SetParent(GetOrCreateSpawnRoot(), false);
        ApplyTransform(pooled, cue, anchor);
        pooled.Instance.SetActive(true);
        Restart(pooled);
    }

    private void ApplyTransform(
        PooledVfx pooled,
        BattleVfxCueSO cue,
        BattleVfxAnchorSnapshot anchor)
    {
        if (pooled?.Instance == null || cue == null)
            return;
        if (!TryConvertAnchor(
                anchor,
                out Vector3 position,
                out Quaternion rotation))
        {
            return;
        }

        position += rotation * cue.LocalPosition;
        rotation *= cue.LocalRotation;
        ApplyWorldTransform(pooled, cue, position, rotation);
    }

    private void ApplyMotionTransform(ActiveVfx active)
    {
        if (active?.Pooled?.Instance == null ||
            active.Cue == null ||
            !active.Cue.HasMotion ||
            !TryConvertAnchor(
                active.MotionStartAnchor,
                out Vector3 startPosition,
                out Quaternion startRotation) ||
            !TryConvertAnchor(
                active.MotionEndAnchor,
                out Vector3 endPosition,
                out Quaternion endRotation))
        {
            return;
        }

        float progress = Mathf.Clamp01(
            active.MotionElapsed / active.Cue.TravelDuration);
        Vector3 position = EvaluateMotionPosition(
            active.Cue,
            startPosition,
            endPosition,
            progress);
        Quaternion rotation = Quaternion.Slerp(
            startRotation,
            endRotation,
            progress);
        if (active.Cue.FaceMotionDirection)
        {
            float nextProgress = Mathf.Min(1f, progress + 0.01f);
            float previousProgress = Mathf.Max(0f, progress - 0.01f);
            Vector3 direction = EvaluateMotionPosition(
                                    active.Cue,
                                    startPosition,
                                    endPosition,
                                    nextProgress) -
                                EvaluateMotionPosition(
                                    active.Cue,
                                    startPosition,
                                    endPosition,
                                    previousProgress);
            if (direction.sqrMagnitude > 0.000001f)
                rotation = Quaternion.LookRotation(direction.normalized);
        }

        position += rotation * active.Cue.LocalPosition;
        rotation *= active.Cue.LocalRotation;
        ApplyWorldTransform(active.Pooled, active.Cue, position, rotation);
    }

    private static Vector3 EvaluateMotionPosition(
        BattleVfxCueSO cue,
        Vector3 start,
        Vector3 end,
        float progress)
    {
        progress = Mathf.Clamp01(progress);
        Vector3 position = Vector3.LerpUnclamped(start, end, progress);
        if (cue.MotionMode == BattleVfxMotionMode.Arc)
        {
            position += Vector3.up *
                        (4f * cue.ArcHeight * progress * (1f - progress));
        }

        return position;
    }

    private static void ApplyWorldTransform(
        PooledVfx pooled,
        BattleVfxCueSO cue,
        Vector3 position,
        Quaternion rotation)
    {
        if (pooled?.Instance == null || cue == null)
            return;

        Transform instanceTransform = pooled.Instance.transform;
        instanceTransform.SetPositionAndRotation(position, rotation);
        instanceTransform.localScale = Vector3.Scale(
            pooled.AuthoredScale,
            cue.LocalScale);
    }

    private bool TryConvertAnchor(
        BattleVfxAnchorSnapshot anchor,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (!anchor.IsValid)
            return false;

        if (anchor.CoordinateSpace == BattleVfxCoordinateSpace.World)
        {
            position = anchor.Position;
            rotation = anchor.Rotation;
            return true;
        }

        Camera camera = WorldCamera;
        if (camera == null)
            return false;

        float minimumDepth = Mathf.Max(
            0.01f,
            camera.nearClipPlane + 0.01f);
        Vector3 screenPosition = anchor.Position;
        screenPosition.z = Mathf.Max(minimumDepth, screenAnchorDepth);
        position = camera.ScreenToWorldPoint(screenPosition);
        rotation = useCameraRotationForScreenAnchors
            ? camera.transform.rotation
            : anchor.Rotation;
        return true;
    }

    private static float ResolveLifetime(
        BattleVfxCueSO cue,
        PooledVfx pooled)
    {
        if (cue.IsPersistent)
            return float.PositiveInfinity;
        if (cue.LifetimeMode == BattleVfxLifetimeMode.Timed)
            return cue.Duration;

        float lifetime = cue.Duration;
        foreach (ParticleSystem particle in pooled.Particles)
        {
            if (particle == null)
                continue;

            ParticleSystem.MainModule main = particle.main;
            if (main.loop)
                continue;
            lifetime = Mathf.Max(
                lifetime,
                main.startDelay.constantMax +
                main.duration +
                main.startLifetime.constantMax);
        }
        return Mathf.Max(0.01f, lifetime);
    }

    private static void Restart(PooledVfx pooled)
    {
        if (pooled == null)
            return;

        foreach (TrailRenderer trail in pooled.Trails)
            trail?.Clear();
        foreach (Animator animator in pooled.Animators)
        {
            if (animator == null)
                continue;
            animator.Rebind();
            animator.Update(0f);
        }
        foreach (ParticleSystem particle in pooled.Particles)
        {
            if (particle == null)
                continue;
            particle.Stop(true, ParticleSystemStopBehavior
                .StopEmittingAndClear);
            particle.Play(true);
        }
    }

    private static void StopEmission(PooledVfx pooled)
    {
        if (pooled == null)
            return;
        foreach (ParticleSystem particle in pooled.Particles)
        {
            particle?.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static void StopAndClear(PooledVfx pooled)
    {
        if (pooled == null)
            return;
        foreach (ParticleSystem particle in pooled.Particles)
        {
            particle?.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (TrailRenderer trail in pooled.Trails)
            trail?.Clear();
    }

    private void ReleaseAt(int index, bool immediate)
    {
        if (index < 0 || index >= _active.Count)
            return;

        ActiveVfx active = _active[index];
        _active.RemoveAt(index);
        PooledVfx pooled = active?.Pooled;
        if (pooled?.Instance == null)
            return;

        if (immediate)
            StopAndClear(pooled);
        pooled.Instance.SetActive(false);
        pooled.Instance.transform.SetParent(GetOrCreateSpawnRoot(), false);
        GetPool(pooled.Prefab).Push(pooled);
    }

    private Transform GetOrCreateSpawnRoot()
    {
        if (spawnRoot != null)
            return spawnRoot;
        if (_ownedSpawnRoot != null)
            return _ownedSpawnRoot;

        GameObject root = new("Battle VFX World Root");
        _ownedSpawnRoot = root.transform;
        return _ownedSpawnRoot;
    }

    private void PlayAudio(BattleVfxCueSO cue)
    {
        if (cue == null ||
            cue.AudioClip == null ||
            qualityProfile != null && !qualityProfile.EnableAudio)
        {
            return;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.PlayOneShot(cue.AudioClip);
    }

    private void LogSkipped(BattleVfxCueSO cue, string reason)
    {
        if (!logSkippedRequests)
            return;
        Debug.LogWarning(
            $"Battle VFX cue '{cue.CueId}' was skipped because it {reason}.",
            this);
    }

    private static object GetTargetIdentity(BattleStatusTarget target)
    {
        return target.Enemy != null
            ? target.Enemy
            : target.Ally;
    }

    private static void DestroySafely(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private sealed class ActiveVfx
    {
        public PooledVfx Pooled { get; }
        public BattleVfxRequest Request { get; set; }
        public BattleVfxCueSO Cue => Request.Cue;
        public BattleVfxTargetHandle TargetHandle =>
            Request.Target.Handle;
        public object TargetIdentity =>
            GetTargetIdentity(Request.Target.BattleTarget);
        public float RemainingTime { get; set; }
        public float MotionElapsed { get; set; }
        public BattleVfxAnchorSnapshot MotionStartAnchor { get; }
        public BattleVfxAnchorSnapshot MotionEndAnchor { get; }
        public bool IsStopping { get; set; }
        public long Sequence { get; }

        public ActiveVfx(
            PooledVfx pooled,
            BattleVfxRequest request,
            float remainingTime,
            BattleVfxAnchorSnapshot motionStartAnchor,
            BattleVfxAnchorSnapshot motionEndAnchor,
            long sequence)
        {
            Pooled = pooled;
            Request = request;
            RemainingTime = remainingTime;
            MotionStartAnchor = motionStartAnchor;
            MotionEndAnchor = motionEndAnchor;
            Sequence = sequence;
        }
    }

    private sealed class ScheduledVfx
    {
        public BattleVfxRequest Request { get; }
        public float RemainingTime { get; set; }
        public long Sequence { get; }

        public ScheduledVfx(
            BattleVfxRequest request,
            float remainingTime,
            long sequence)
        {
            Request = request;
            RemainingTime = Mathf.Max(0f, remainingTime);
            Sequence = sequence;
        }
    }

    private sealed class PooledVfx
    {
        public GameObject Instance { get; }
        public GameObject Prefab { get; }
        public Vector3 AuthoredScale { get; }
        public ParticleSystem[] Particles { get; }
        public TrailRenderer[] Trails { get; }
        public Animator[] Animators { get; }

        public PooledVfx(GameObject instance, GameObject prefab)
        {
            Instance = instance;
            Prefab = prefab;
            AuthoredScale = instance != null
                ? instance.transform.localScale
                : Vector3.one;
            Particles = instance != null
                ? instance.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
            Trails = instance != null
                ? instance.GetComponentsInChildren<TrailRenderer>(true)
                : Array.Empty<TrailRenderer>();
            Animators = instance != null
                ? instance.GetComponentsInChildren<Animator>(true)
                : Array.Empty<Animator>();
        }
    }
}
