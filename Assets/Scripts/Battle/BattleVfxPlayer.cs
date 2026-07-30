using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

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
    [SerializeField, Min(0.0001f)]
    private float referenceTileWorldSize = 1f;
    [SerializeField]
    private bool useCameraRotationForScreenAnchors = true;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioMixerGroup sfxMixerGroup;

    [Header("Quality And Budget")]
    [SerializeField]
    private BattleVfxQualityProfileSO qualityProfile;

    [Header("Diagnostics")]
    [SerializeField]
    private bool logSkippedRequests;

    private readonly List<ActiveVfx> _active = new();
    private readonly List<ScheduledVfx> _scheduled = new();
    private readonly List<ScheduledVfxClip> _scheduledClips = new();
    private readonly Dictionary<GameObject, Stack<PooledVfx>>
        _pools = new();
    private readonly HashSet<BattleVfxCueSO> _prewarmedCues = new();
    private IBattleVfxTargetResolver _targetResolver;
    private Transform _ownedSpawnRoot;
    private Transform _inactiveSpawnRoot;
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
    public int ScheduledRequestCount =>
        _scheduled.Count + _scheduledClips.Count;
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
        {
            audioSource = playbackAudioSource;
            RouteAudioSourceToSfx(audioSource);
        }
    }

    public void ConfigureAudioMixerGroup(AudioMixerGroup mixerGroup)
    {
        sfxMixerGroup = mixerGroup;
        RouteAudioSourceToSfx(audioSource);
        RouteAllPooledAudioToSfx();
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
        if (cue.UsesClipTimeline)
        {
            PlayCompositeNow(request);
            return;
        }

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
            ++_nextSequence,
            null,
            _nextSequence,
            ResolveNaturalDuration(pooled)));
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

            float deltaTime = active.UseBattleTime
                ? battleDeltaTime
                : unscaledDeltaTime;
            if (active.HasMotion)
            {
                active.MotionElapsed += deltaTime;
                if (active.Clip != null)
                    ApplyClipMotionTransform(active);
                else
                    ApplyMotionTransform(active);
            }
            else if (active.AttachMode ==
                     BattleVfxAttachMode.FollowTarget)
            {
                RefreshFollowAnchor(active);
            }

            if (active.PlaybackFit ==
                BattleVfxPlaybackFit.LoopToDuration &&
                active.NaturalDuration > 0.01f)
            {
                active.PlaybackCycleElapsed += deltaTime;
                if (active.PlaybackCycleElapsed >=
                    active.NaturalDuration)
                {
                    active.PlaybackCycleElapsed %=
                        active.NaturalDuration;
                    Restart(active.Pooled);
                    if (active.Clip != null)
                    {
                        ConfigurePlayback(
                            active.Pooled,
                            active.Clip,
                            active.NaturalDuration);
                    }
                }
            }

            if (active.IsPersistent && !active.IsStopping)
                continue;

            active.RemainingTime -= deltaTime;
            if (active.RemainingTime <= 0f)
                ReleaseAt(index, false);
        }

        AdvanceScheduled(battleDeltaTime, unscaledDeltaTime);
        AdvanceScheduledClips(battleDeltaTime, unscaledDeltaTime);
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

    private void AdvanceScheduledClips(
        float battleDeltaTime,
        float unscaledDeltaTime)
    {
        for (int index = _scheduledClips.Count - 1; index >= 0; index--)
        {
            ScheduledVfxClip scheduled = _scheduledClips[index];
            float deltaTime = scheduled.Clip.UseBattleTime
                ? battleDeltaTime
                : unscaledDeltaTime;
            scheduled.RemainingTime -= deltaTime;
            if (scheduled.RemainingTime > 0f)
                continue;

            _scheduledClips.RemoveAt(index);
            PlayClipNow(
                scheduled.Request,
                scheduled.Clip,
                scheduled.GroupSequence);
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

    public bool TryGetActiveInstance(
        BattleVfxCueSO cue,
        BattleVfxTargetHandle targetHandle,
        string clipId,
        out GameObject instance)
    {
        foreach (ActiveVfx active in _active)
        {
            if (active != null &&
                active.Cue == cue &&
                active.TargetHandle == targetHandle &&
                string.Equals(
                    active.Clip?.ClipId,
                    clipId,
                    StringComparison.Ordinal) &&
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
        _scheduledClips.Clear();
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
        if (_inactiveSpawnRoot != null)
            DestroySafely(_inactiveSpawnRoot.gameObject);
        _inactiveSpawnRoot = null;
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
        if (float.IsNaN(referenceTileWorldSize) ||
            float.IsInfinity(referenceTileWorldSize))
        {
            referenceTileWorldSize = 1f;
        }
        referenceTileWorldSize = Mathf.Max(
            0.0001f,
            referenceTileWorldSize);
    }

    private void PlayCompositeNow(BattleVfxRequest request)
    {
        BattleVfxCueSO cue = request.Cue;
        if (cue == null)
            return;

        if (cue.IsPersistent && TryRefreshPersistent(request))
        {
            PlayAudio(cue);
            return;
        }

        EnforceCompositeConcurrentLimit(cue);
        long groupSequence = ++_nextSequence;
        EnsurePrewarmed(cue);
        PlayAudio(cue);

        bool hasOutput = false;
        foreach (BattleVfxClipDefinition clip in cue.Clips)
        {
            if (clip == null ||
                clip.Prefab == null && clip.AudioClip == null)
            {
                continue;
            }

            hasOutput = true;
            if (clip.StartTime > 0f)
            {
                _scheduledClips.Add(new ScheduledVfxClip(
                    request,
                    clip,
                    clip.StartTime,
                    groupSequence,
                    ++_nextSequence));
            }
            else
            {
                PlayClipNow(request, clip, groupSequence);
            }
        }

        if (!hasOutput)
            LogSkipped(cue, "has no playable timeline clips");
    }

    private void PlayClipNow(
        BattleVfxRequest request,
        BattleVfxClipDefinition clip,
        long groupSequence)
    {
        if (request.Cue == null || clip == null)
            return;

        PlayAudio(clip.AudioClip);
        if (clip.Prefab == null)
            return;

        if (!TryResolveClipAnchors(
                request,
                clip,
                out BattleVfxAnchorSnapshot sourceAnchor,
                out BattleVfxAnchorSnapshot targetAnchor))
        {
            LogSkipped(
                request.Cue,
                $"clip '{clip.ClipId}' has no valid placement frame");
            return;
        }

        if (!TryReserveActiveBudget(request.Cue))
            return;

        PooledVfx pooled = Acquire(clip.Prefab);
        if (pooled == null || pooled.Instance == null)
            return;

        ActivateClip(
            pooled,
            clip,
            clip.HasMotion ? sourceAnchor : targetAnchor);
        float naturalDuration = ResolveNaturalDuration(pooled);
        Restart(pooled);
        ConfigurePlayback(pooled, clip, naturalDuration);
        _active.Add(new ActiveVfx(
            pooled,
            request,
            clip.IsPersistent
                ? float.PositiveInfinity
                : Mathf.Max(
                    clip.Duration,
                    clip.HasMotion ? clip.TravelDuration : 0f),
            sourceAnchor,
            targetAnchor,
            ++_nextSequence,
            clip,
            groupSequence,
            naturalDuration));
    }

    private bool TryRefreshPersistent(BattleVfxRequest request)
    {
        bool found = false;
        foreach (ActiveVfx active in _active)
        {
            if (!MatchesPersistent(active, request))
                continue;

            found = true;
            active.Request = request;
            active.IsStopping = false;
            active.RemainingTime = active.IsPersistent
                ? float.PositiveInfinity
                : active.Duration;
            if (active.Clip != null)
            {
                if (TryResolveClipAnchors(
                        request,
                        active.Clip,
                        out BattleVfxAnchorSnapshot sourceAnchor,
                        out BattleVfxAnchorSnapshot targetAnchor))
                {
                    active.MotionStartAnchor = sourceAnchor;
                    active.MotionEndAnchor = targetAnchor;
                    ApplyClipTransform(
                        active.Pooled,
                        active.Clip,
                        active.HasMotion ? sourceAnchor : targetAnchor);
                }
            }
            else if (TryResolveAnchor(
                         request.Target,
                         request.Cue.AnchorType,
                         out BattleVfxAnchorSnapshot anchor))
            {
                ApplyTransform(active.Pooled, request.Cue, anchor);
            }
            active.MotionElapsed = 0f;
            active.PlaybackCycleElapsed = 0f;
            Restart(active.Pooled);
            if (active.Clip != null)
            {
                ConfigurePlayback(
                    active.Pooled,
                    active.Clip,
                    active.NaturalDuration);
            }
        }

        foreach (ScheduledVfxClip scheduled in _scheduledClips)
        {
            if (MatchesPersistent(scheduled.Request, request))
                found = true;
        }

        return found;
    }

    private void StopPersistent(BattleVfxRequest request)
    {
        for (int index = _scheduledClips.Count - 1; index >= 0; index--)
        {
            if (MatchesPersistent(
                    _scheduledClips[index].Request,
                    request))
            {
                _scheduledClips.RemoveAt(index);
            }
        }

        for (int index = _active.Count - 1; index >= 0; index--)
        {
            ActiveVfx active = _active[index];
            if (!MatchesPersistent(active, request))
                continue;

            if (active.StopMode == BattleVfxStopMode.Immediate)
            {
                ReleaseAt(index, true);
                continue;
            }

            StopEmission(active.Pooled);
            active.IsStopping = true;
            active.RemainingTime = Mathf.Max(
                0.01f,
                active.Duration);
        }
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

    private static bool MatchesPersistent(
        BattleVfxRequest candidate,
        BattleVfxRequest request)
    {
        if (candidate.Cue != request.Cue)
            return false;
        if (candidate.Target.Handle.IsValid ||
            request.Target.Handle.IsValid)
        {
            return candidate.Target.Handle == request.Target.Handle;
        }
        return ReferenceEquals(
            GetTargetIdentity(candidate.Target.BattleTarget),
            GetTargetIdentity(request.Target.BattleTarget));
    }

    private void RefreshFollowAnchor(ActiveVfx active)
    {
        if (active.Clip != null)
        {
            if (TryResolveClipAnchors(
                    active.Request,
                    active.Clip,
                    out BattleVfxAnchorSnapshot sourceAnchor,
                    out BattleVfxAnchorSnapshot targetAnchor))
            {
                active.MotionStartAnchor = sourceAnchor;
                active.MotionEndAnchor = targetAnchor;
                ApplyClipTransform(
                    active.Pooled,
                    active.Clip,
                    targetAnchor);
            }
            return;
        }

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

    private bool TryResolveClipAnchors(
        BattleVfxRequest request,
        BattleVfxClipDefinition clip,
        out BattleVfxAnchorSnapshot sourceAnchor,
        out BattleVfxAnchorSnapshot targetAnchor)
    {
        sourceAnchor = default;
        targetAnchor = default;
        if (clip == null)
            return false;

        BattleVfxTarget placementTarget =
            clip.PlacementArea == BattleVfxPlacementArea.Caster &&
            request.SourceTarget.IsValid
                ? request.SourceTarget
                : request.Target;
        if (!TryResolveAnchor(
                placementTarget,
                clip.AnchorType,
                out targetAnchor))
        {
            return false;
        }

        if (!clip.HasMotion)
        {
            sourceAnchor = targetAnchor;
            return true;
        }

        BattleVfxTarget motionSource = request.SourceTarget.IsValid
            ? request.SourceTarget
            : request.Target;
        return TryResolveAnchor(
            motionSource,
            BattleVfxAnchorType.Muzzle,
            out sourceAnchor);
    }

    private void EnsurePrewarmed(BattleVfxCueSO cue)
    {
        if (cue == null || !_prewarmedCues.Add(cue))
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
        if (cue.UsesClipTimeline)
        {
            HashSet<GameObject> prefabs = new();
            foreach (BattleVfxClipDefinition clip in cue.Clips)
            {
                if (clip?.Prefab != null)
                    prefabs.Add(clip.Prefab);
            }
            foreach (GameObject clipPrefab in prefabs)
                PrewarmPrefab(clipPrefab, count);
            return;
        }

        PrewarmPrefab(cue.LegacyPrefab, count);
    }

    private void PrewarmPrefab(GameObject prefab, int count)
    {
        if (prefab == null)
            return;

        Stack<PooledVfx> pool = GetPool(prefab);
        int missingCount = Mathf.Max(0, count - pool.Count);
        for (int index = 0; index < missingCount; index++)
        {
            PooledVfx pooled = CreatePooled(prefab);
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

    private void EnforceCompositeConcurrentLimit(BattleVfxCueSO cue)
    {
        while (CountCompositeGroups(cue) >= cue.MaximumConcurrent)
        {
            long oldestGroup = FindOldestCompositeGroup(cue);
            if (oldestGroup <= 0)
                return;
            ReleaseGroup(cue, oldestGroup);
        }
    }

    private int CountCompositeGroups(BattleVfxCueSO cue)
    {
        HashSet<long> groups = new();
        foreach (ActiveVfx active in _active)
        {
            if (active?.Cue == cue)
                groups.Add(active.GroupSequence);
        }
        foreach (ScheduledVfxClip scheduled in _scheduledClips)
        {
            if (scheduled.Request.Cue == cue)
                groups.Add(scheduled.GroupSequence);
        }
        return groups.Count;
    }

    private long FindOldestCompositeGroup(BattleVfxCueSO cue)
    {
        long oldest = long.MaxValue;
        foreach (ActiveVfx active in _active)
        {
            if (active?.Cue == cue)
                oldest = Math.Min(oldest, active.GroupSequence);
        }
        foreach (ScheduledVfxClip scheduled in _scheduledClips)
        {
            if (scheduled.Request.Cue == cue)
                oldest = Math.Min(oldest, scheduled.GroupSequence);
        }
        return oldest == long.MaxValue ? -1 : oldest;
    }

    private void ReleaseGroup(BattleVfxCueSO cue, long groupSequence)
    {
        for (int index = _scheduledClips.Count - 1; index >= 0; index--)
        {
            ScheduledVfxClip scheduled = _scheduledClips[index];
            if (scheduled.Request.Cue == cue &&
                scheduled.GroupSequence == groupSequence)
            {
                _scheduledClips.RemoveAt(index);
            }
        }
        for (int index = _active.Count - 1; index >= 0; index--)
        {
            ActiveVfx active = _active[index];
            if (active?.Cue == cue &&
                active.GroupSequence == groupSequence)
            {
                ReleaseAt(index, true);
            }
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

        for (int index = _scheduledClips.Count - 1; index >= 0; index--)
        {
            BattleVfxCueSO cue =
                _scheduledClips[index].Request.Cue;
            if (IsCueAllowed(cue))
                continue;

            _scheduledClips.RemoveAt(index);
            _skippedByQualityCount++;
            LogSkipped(cue, "is below the current quality threshold");
        }

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

        GameObject instance = Instantiate(
            prefab,
            GetOrCreateInactiveSpawnRoot());
        instance.name = $"{prefab.name} (Battle VFX)";
        PooledVfx pooled = new(instance, prefab);
        RoutePooledAudioToSfx(pooled);
        StopAndClear(pooled);
        instance.SetActive(false);
        instance.transform.SetParent(GetOrCreateSpawnRoot(), false);
        return pooled;
    }

    private void Activate(
        PooledVfx pooled,
        BattleVfxCueSO cue,
        BattleVfxAnchorSnapshot anchor)
    {
        pooled.Instance.transform.SetParent(GetOrCreateSpawnRoot(), false);
        ApplyTransform(pooled, cue, anchor);
        RoutePooledAudioToSfx(pooled);
        pooled.Instance.SetActive(true);
        Restart(pooled);
    }

    private void ActivateClip(
        PooledVfx pooled,
        BattleVfxClipDefinition clip,
        BattleVfxAnchorSnapshot anchor)
    {
        pooled.Instance.transform.SetParent(GetOrCreateSpawnRoot(), false);
        RestorePlaybackSettings(pooled);
        ApplyClipTransform(pooled, clip, anchor);
        RoutePooledAudioToSfx(pooled);
        pooled.Instance.SetActive(true);
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

        float tileScale = ResolveLegacyTileScale(anchor);
        position += rotation * (cue.LocalPosition * tileScale);
        rotation *= cue.LocalRotation;
        ApplyWorldTransform(
            pooled,
            cue,
            position,
            rotation,
            tileScale);
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
        float tileWorldSize = Mathf.Lerp(
            ResolveTileWorldSize(active.MotionStartAnchor),
            ResolveTileWorldSize(active.MotionEndAnchor),
            progress);
        float tileScale = ResolveLegacyTileScale(tileWorldSize);
        Vector3 position = EvaluateMotionPosition(
            active.Cue.MotionMode,
            active.Cue.ArcHeight * tileScale,
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
                                    active.Cue.MotionMode,
                                    active.Cue.ArcHeight * tileScale,
                                    startPosition,
                                    endPosition,
                                    nextProgress) -
                                EvaluateMotionPosition(
                                    active.Cue.MotionMode,
                                    active.Cue.ArcHeight * tileScale,
                                    startPosition,
                                    endPosition,
                                    previousProgress);
            if (direction.sqrMagnitude > 0.000001f)
                rotation = Quaternion.LookRotation(direction.normalized);
        }

        position += rotation *
                    (active.Cue.LocalPosition * tileScale);
        rotation *= active.Cue.LocalRotation;
        ApplyWorldTransform(
            active.Pooled,
            active.Cue,
            position,
            rotation,
            tileScale);
    }

    private void ApplyClipTransform(
        PooledVfx pooled,
        BattleVfxClipDefinition clip,
        BattleVfxAnchorSnapshot anchor)
    {
        if (pooled?.Instance == null || clip == null)
            return;
        if (!TryConvertAnchor(
                anchor,
                clip.GridPosition,
                out Vector3 position,
                out Quaternion rotation,
                out float tileWorldSize))
        {
            return;
        }

        float tileScale = ResolveTileScale(clip, tileWorldSize);
        position += rotation * (clip.LocalPosition * tileScale);
        rotation *= clip.LocalRotation;
        ApplyWorldTransform(
            pooled,
            clip,
            position,
            rotation,
            tileScale);
    }

    private void ApplyClipMotionTransform(ActiveVfx active)
    {
        BattleVfxClipDefinition clip = active?.Clip;
        if (active?.Pooled?.Instance == null ||
            clip == null ||
            !clip.HasMotion ||
            !TryConvertAnchor(
                active.MotionStartAnchor,
                clip.MotionSourceGridPosition,
                out Vector3 startPosition,
                out Quaternion startRotation,
                out float startTileWorldSize) ||
            !TryConvertAnchor(
                active.MotionEndAnchor,
                clip.GridPosition,
                out Vector3 endPosition,
                out Quaternion endRotation,
                out float endTileWorldSize))
        {
            return;
        }

        float progress = Mathf.Clamp01(
            active.MotionElapsed / clip.TravelDuration);
        float tileWorldSize = Mathf.Lerp(
            startTileWorldSize,
            endTileWorldSize,
            progress);
        float tileScale = ResolveTileScale(clip, tileWorldSize);
        Vector3 position = EvaluateMotionPosition(
            clip.MotionMode,
            clip.ArcHeight * tileScale,
            startPosition,
            endPosition,
            progress);
        Quaternion rotation = Quaternion.Slerp(
            startRotation,
            endRotation,
            progress);
        if (clip.FaceMotionDirection)
        {
            float nextProgress = Mathf.Min(1f, progress + 0.01f);
            float previousProgress = Mathf.Max(0f, progress - 0.01f);
            Vector3 direction = EvaluateMotionPosition(
                                    clip.MotionMode,
                                    clip.ArcHeight * tileScale,
                                    startPosition,
                                    endPosition,
                                    nextProgress) -
                                EvaluateMotionPosition(
                                    clip.MotionMode,
                                    clip.ArcHeight * tileScale,
                                    startPosition,
                                    endPosition,
                                    previousProgress);
            if (direction.sqrMagnitude > 0.000001f)
                rotation = Quaternion.LookRotation(direction.normalized);
        }

        position += rotation * (clip.LocalPosition * tileScale);
        rotation *= clip.LocalRotation;
        ApplyWorldTransform(
            active.Pooled,
            clip,
            position,
            rotation,
            tileScale);
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

    private static Vector3 EvaluateMotionPosition(
        BattleVfxMotionMode motionMode,
        float arcHeight,
        Vector3 start,
        Vector3 end,
        float progress)
    {
        progress = Mathf.Clamp01(progress);
        Vector3 position = Vector3.LerpUnclamped(start, end, progress);
        if (motionMode == BattleVfxMotionMode.Arc)
        {
            position += Vector3.up *
                        (4f * arcHeight * progress * (1f - progress));
        }
        return position;
    }

    private static void ApplyWorldTransform(
        PooledVfx pooled,
        BattleVfxCueSO cue,
        Vector3 position,
        Quaternion rotation,
        float tileScale = 1f)
    {
        if (pooled?.Instance == null || cue == null)
            return;

        Transform instanceTransform = pooled.Instance.transform;
        instanceTransform.SetPositionAndRotation(position, rotation);
        instanceTransform.localScale = Vector3.Scale(
            pooled.AuthoredScale,
            cue.LocalScale * tileScale);
    }

    private static void ApplyWorldTransform(
        PooledVfx pooled,
        BattleVfxClipDefinition clip,
        Vector3 position,
        Quaternion rotation,
        float tileScale)
    {
        if (pooled?.Instance == null || clip == null)
            return;

        Transform instanceTransform = pooled.Instance.transform;
        instanceTransform.SetPositionAndRotation(position, rotation);
        Vector3 clipScale = Vector3.Scale(
            clip.LocalScale,
            Vector3.one * clip.UniformScale);
        instanceTransform.localScale = Vector3.Scale(
            pooled.AuthoredScale,
            clipScale * tileScale);
    }

    private float ResolveTileScale(
        BattleVfxClipDefinition clip,
        float tileWorldSize)
    {
        if (clip == null ||
            clip.ScaleMode == BattleVfxScaleMode.ManualOnly ||
            tileWorldSize <= 0.0001f)
        {
            return 1f;
        }

        return Mathf.Max(
            0.0001f,
            tileWorldSize / referenceTileWorldSize);
    }

    private float ResolveLegacyTileScale(
        BattleVfxAnchorSnapshot anchor)
    {
        return ResolveLegacyTileScale(ResolveTileWorldSize(anchor));
    }

    private float ResolveLegacyTileScale(float tileWorldSize)
    {
        if (tileWorldSize <= 0.0001f)
            return 1f;
        return Mathf.Max(
            0.0001f,
            tileWorldSize / referenceTileWorldSize);
    }

    private float ResolveTileWorldSize(
        BattleVfxAnchorSnapshot anchor)
    {
        if (!anchor.IsValid || !anchor.HasFrame)
            return 0f;
        if (anchor.CoordinateSpace == BattleVfxCoordinateSpace.World)
        {
            return (
                anchor.FrameRight.magnitude +
                anchor.FrameUp.magnitude) * 0.5f;
        }

        Camera camera = WorldCamera;
        if (camera == null)
            return 0f;
        float minimumDepth = Mathf.Max(
            0.01f,
            camera.nearClipPlane + 0.01f);
        float depth = Mathf.Max(minimumDepth, screenAnchorDepth);
        Vector3 screenCenter = anchor.FrameCenter;
        screenCenter.z = depth;
        Vector3 screenRight = anchor.FrameCenter + anchor.FrameRight;
        screenRight.z = depth;
        Vector3 screenUp = anchor.FrameCenter + anchor.FrameUp;
        screenUp.z = depth;
        Vector3 worldCenter = camera.ScreenToWorldPoint(screenCenter);
        return (
            Vector3.Distance(
                worldCenter,
                camera.ScreenToWorldPoint(screenRight)) +
            Vector3.Distance(
                worldCenter,
                camera.ScreenToWorldPoint(screenUp))) * 0.5f;
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
            position = anchor.HasFrame
                ? anchor.FrameCenter
                : anchor.Position;
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

    private bool TryConvertAnchor(
        BattleVfxAnchorSnapshot anchor,
        Vector2 gridPosition,
        out Vector3 position,
        out Quaternion rotation,
        out float tileWorldSize)
    {
        position = default;
        rotation = Quaternion.identity;
        tileWorldSize = 0f;
        if (!anchor.IsValid)
            return false;

        Vector2 normalizedOffset =
            gridPosition / BattleVfxClipDefinition.GridDimension -
            Vector2.one * 0.5f;
        if (anchor.CoordinateSpace == BattleVfxCoordinateSpace.World)
        {
            position = anchor.HasFrame
                ? anchor.FrameCenter
                : anchor.Position;
            if (anchor.HasFrame)
            {
                position += anchor.FrameRight * normalizedOffset.x +
                            anchor.FrameUp * normalizedOffset.y;
                tileWorldSize = (
                    anchor.FrameRight.magnitude +
                    anchor.FrameUp.magnitude) * 0.5f;
            }
            rotation = anchor.Rotation;
            return true;
        }

        Camera camera = WorldCamera;
        if (camera == null)
            return false;

        float minimumDepth = Mathf.Max(
            0.01f,
            camera.nearClipPlane + 0.01f);
        float depth = Mathf.Max(minimumDepth, screenAnchorDepth);
        Vector3 screenPosition = anchor.HasFrame
            ? anchor.FrameCenter
            : anchor.Position;
        if (anchor.HasFrame)
        {
            screenPosition +=
                anchor.FrameRight * normalizedOffset.x +
                anchor.FrameUp * normalizedOffset.y;
            Vector3 screenCenter = anchor.FrameCenter;
            screenCenter.z = depth;
            Vector3 screenRight =
                anchor.FrameCenter + anchor.FrameRight;
            screenRight.z = depth;
            Vector3 screenUp =
                anchor.FrameCenter + anchor.FrameUp;
            screenUp.z = depth;
            Vector3 worldCenter = camera.ScreenToWorldPoint(screenCenter);
            tileWorldSize = (
                Vector3.Distance(
                    worldCenter,
                    camera.ScreenToWorldPoint(screenRight)) +
                Vector3.Distance(
                    worldCenter,
                    camera.ScreenToWorldPoint(screenUp))) * 0.5f;
        }
        screenPosition.z = depth;
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

    private static float ResolveNaturalDuration(PooledVfx pooled)
    {
        if (pooled == null)
            return 0.01f;

        float duration = 0.01f;
        for (int index = 0; index < pooled.Particles.Length; index++)
        {
            ParticleSystem particle = pooled.Particles[index];
            if (particle == null)
                continue;
            ParticleSystem.MainModule main = particle.main;
            float authoredSpeed = Mathf.Max(
                0.0001f,
                Mathf.Abs(pooled.ParticleSimulationSpeeds[index]));
            duration = Mathf.Max(
                duration,
                (main.startDelay.constantMax +
                 main.duration +
                 main.startLifetime.constantMax) / authoredSpeed);
        }
        for (int index = 0; index < pooled.Animators.Length; index++)
        {
            Animator animator = pooled.Animators[index];
            RuntimeAnimatorController controller =
                animator != null
                    ? animator.runtimeAnimatorController
                    : null;
            if (controller == null)
                continue;
            foreach (AnimationClip animationClip in controller.animationClips)
            {
                if (animationClip != null)
                {
                    float authoredSpeed = Mathf.Max(
                        0.0001f,
                        Mathf.Abs(pooled.AnimatorSpeeds[index]));
                    duration = Mathf.Max(
                        duration,
                        animationClip.length / authoredSpeed);
                }
            }
        }
        return Mathf.Max(0.01f, duration);
    }

    private static void ConfigurePlayback(
        PooledVfx pooled,
        BattleVfxClipDefinition clip,
        float naturalDuration)
    {
        if (pooled == null || clip == null)
            return;

        RestorePlaybackSettings(pooled);
        float speedMultiplier = 1f;
        if (clip.PlaybackFit ==
            BattleVfxPlaybackFit.StretchToDuration)
        {
            speedMultiplier = Mathf.Clamp(
                naturalDuration / clip.Duration,
                0.01f,
                100f);
        }

        for (int index = 0; index < pooled.Particles.Length; index++)
        {
            ParticleSystem particle = pooled.Particles[index];
            if (particle == null)
                continue;
            ParticleSystem.MainModule main = particle.main;
            main.simulationSpeed =
                pooled.ParticleSimulationSpeeds[index] *
                speedMultiplier;
            if (clip.PlaybackFit ==
                BattleVfxPlaybackFit.LoopToDuration)
            {
                main.loop = true;
            }
        }
        for (int index = 0; index < pooled.Animators.Length; index++)
        {
            Animator animator = pooled.Animators[index];
            if (animator != null)
            {
                animator.speed =
                    pooled.AnimatorSpeeds[index] * speedMultiplier;
            }
        }
    }

    private static void RestorePlaybackSettings(PooledVfx pooled)
    {
        if (pooled == null)
            return;

        for (int index = 0; index < pooled.Particles.Length; index++)
        {
            ParticleSystem particle = pooled.Particles[index];
            if (particle == null)
                continue;
            ParticleSystem.MainModule main = particle.main;
            main.simulationSpeed =
                pooled.ParticleSimulationSpeeds[index];
            main.loop = pooled.ParticleLoopSettings[index];
        }
        for (int index = 0; index < pooled.Animators.Length; index++)
        {
            Animator animator = pooled.Animators[index];
            if (animator != null)
                animator.speed = pooled.AnimatorSpeeds[index];
        }
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
        foreach (AudioSource source in pooled.AudioSources)
            source?.Stop();
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
        RestorePlaybackSettings(pooled);
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

    private Transform GetOrCreateInactiveSpawnRoot()
    {
        if (_inactiveSpawnRoot != null)
            return _inactiveSpawnRoot;

        GameObject root = new("Battle VFX Inactive Staging Root");
        root.SetActive(false);
        _inactiveSpawnRoot = root.transform;
        return _inactiveSpawnRoot;
    }

    private void PlayAudio(BattleVfxCueSO cue)
    {
        if (cue == null)
        {
            return;
        }

        PlayAudio(cue.AudioClip);
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null ||
            qualityProfile != null && !qualityProfile.EnableAudio)
        {
            return;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        RouteAudioSourceToSfx(audioSource);
        audioSource.PlayOneShot(clip);
    }

    private void RouteAllPooledAudioToSfx()
    {
        foreach (ActiveVfx active in _active)
            RoutePooledAudioToSfx(active?.Pooled);
        foreach (Stack<PooledVfx> pool in _pools.Values)
        {
            foreach (PooledVfx pooled in pool)
                RoutePooledAudioToSfx(pooled);
        }
    }

    private void RoutePooledAudioToSfx(PooledVfx pooled)
    {
        if (pooled == null)
            return;
        foreach (AudioSource source in pooled.AudioSources)
            RouteAudioSourceToSfx(source);
    }

    private bool RouteAudioSourceToSfx(AudioSource source)
    {
        if (source == null)
            return false;

        AudioMixerGroup group = sfxMixerGroup;
        if (group == null)
        {
            AudioManager manager = GameManager.Instance != null
                ? GameManager.Instance.Audio
                : null;
            if (manager != null)
                return manager.TryRouteToSfx(source);
            return false;
        }

        source.outputAudioMixerGroup = group;
        return true;
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
        public BattleVfxClipDefinition Clip { get; }
        public BattleVfxCueSO Cue => Request.Cue;
        public BattleVfxTargetHandle TargetHandle =>
            Request.Target.Handle;
        public object TargetIdentity =>
            GetTargetIdentity(Request.Target.BattleTarget);
        public float RemainingTime { get; set; }
        public float Duration => Clip != null
            ? Clip.Duration
            : Cue.Duration;
        public float MotionElapsed { get; set; }
        public float PlaybackCycleElapsed { get; set; }
        public float NaturalDuration { get; }
        public BattleVfxAnchorSnapshot MotionStartAnchor { get; set; }
        public BattleVfxAnchorSnapshot MotionEndAnchor { get; set; }
        public BattleVfxAttachMode AttachMode => Clip != null
            ? Clip.AttachMode
            : Cue.AttachMode;
        public BattleVfxStopMode StopMode => Clip != null
            ? Clip.StopMode
            : Cue.StopMode;
        public BattleVfxPlaybackFit PlaybackFit => Clip != null
            ? Clip.PlaybackFit
            : BattleVfxPlaybackFit.Natural;
        public bool UseBattleTime => Clip != null
            ? Clip.UseBattleTime
            : Cue.UseBattleTime;
        public bool HasMotion => Clip != null
            ? Clip.HasMotion
            : Cue.HasMotion;
        public bool IsPersistent => Clip != null
            ? Clip.IsPersistent
            : Cue.IsPersistent;
        public bool IsStopping { get; set; }
        public long Sequence { get; }
        public long GroupSequence { get; }

        public ActiveVfx(
            PooledVfx pooled,
            BattleVfxRequest request,
            float remainingTime,
            BattleVfxAnchorSnapshot motionStartAnchor,
            BattleVfxAnchorSnapshot motionEndAnchor,
            long sequence,
            BattleVfxClipDefinition clip,
            long groupSequence,
            float naturalDuration)
        {
            Pooled = pooled;
            Request = request;
            Clip = clip;
            RemainingTime = remainingTime;
            MotionStartAnchor = motionStartAnchor;
            MotionEndAnchor = motionEndAnchor;
            Sequence = sequence;
            GroupSequence = groupSequence;
            NaturalDuration = Mathf.Max(0.01f, naturalDuration);
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

    private sealed class ScheduledVfxClip
    {
        public BattleVfxRequest Request { get; }
        public BattleVfxClipDefinition Clip { get; }
        public float RemainingTime { get; set; }
        public long GroupSequence { get; }
        public long Sequence { get; }

        public ScheduledVfxClip(
            BattleVfxRequest request,
            BattleVfxClipDefinition clip,
            float remainingTime,
            long groupSequence,
            long sequence)
        {
            Request = request;
            Clip = clip;
            RemainingTime = Mathf.Max(0f, remainingTime);
            GroupSequence = groupSequence;
            Sequence = sequence;
        }
    }

    private sealed class PooledVfx
    {
        public GameObject Instance { get; }
        public GameObject Prefab { get; }
        public Vector3 AuthoredScale { get; }
        public ParticleSystem[] Particles { get; }
        public float[] ParticleSimulationSpeeds { get; }
        public bool[] ParticleLoopSettings { get; }
        public TrailRenderer[] Trails { get; }
        public Animator[] Animators { get; }
        public float[] AnimatorSpeeds { get; }
        public AudioSource[] AudioSources { get; }

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
            ParticleSimulationSpeeds = new float[Particles.Length];
            ParticleLoopSettings = new bool[Particles.Length];
            for (int index = 0; index < Particles.Length; index++)
            {
                ParticleSystem particle = Particles[index];
                if (particle == null)
                {
                    ParticleSimulationSpeeds[index] = 1f;
                    continue;
                }
                ParticleSystem.MainModule main = particle.main;
                ParticleSimulationSpeeds[index] = main.simulationSpeed;
                ParticleLoopSettings[index] = main.loop;
            }
            Trails = instance != null
                ? instance.GetComponentsInChildren<TrailRenderer>(true)
                : Array.Empty<TrailRenderer>();
            Animators = instance != null
                ? instance.GetComponentsInChildren<Animator>(true)
                : Array.Empty<Animator>();
            AnimatorSpeeds = new float[Animators.Length];
            for (int index = 0; index < Animators.Length; index++)
            {
                AnimatorSpeeds[index] = Animators[index] != null
                    ? Animators[index].speed
                    : 1f;
            }
            AudioSources = instance != null
                ? instance.GetComponentsInChildren<AudioSource>(true)
                : Array.Empty<AudioSource>();
        }
    }
}
