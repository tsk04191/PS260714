using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleVfxAnchorType
{
    Center = 0,
    Ground = 1,
    Head = 2,
    Muzzle = 3,
    Status = 4,
    Tile = 5
}

public enum BattleVfxAttachMode
{
    SpawnAtAnchor = 0,
    FollowTarget = 1
}

public enum BattleVfxMotionMode
{
    Stationary = 0,
    Linear = 1,
    Arc = 2
}

public enum BattleVfxLifetimeMode
{
    ParticleSystem = 0,
    Timed = 1,
    Persistent = 2
}

public enum BattleVfxStopMode
{
    Immediate = 0,
    StopEmission = 1
}

public enum BattleVfxImportance
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum BattleVfxPlacementArea
{
    Target = 0,
    Caster = 1
}

public enum BattleVfxScaleMode
{
    TileRelative = 0,
    ManualOnly = 1
}

public enum BattleVfxPlaybackFit
{
    Natural = 0,
    StretchToDuration = 1,
    LoopToDuration = 2
}

[Serializable]
public sealed class BattleVfxClipDefinition
{
    public const int GridDimension = 10;

    [SerializeField, HideInInspector]
    private string clipId;

    [Header("Output")]
    [SerializeField]
    private GameObject prefab;
    [SerializeField]
    private AudioClip audioClip;
    [SerializeField, Range(0, 100)]
    private int audioVolumePercent = 100;
    [SerializeField]
    private bool required = true;

    [Header("Timeline")]
    [SerializeField, Min(0f)]
    private float startTime;
    [SerializeField, Min(0.01f)]
    private float duration = 1f;
    [SerializeField]
    private BattleVfxPlaybackFit playbackFit =
        BattleVfxPlaybackFit.Natural;

    [Header("10 x 10 Tile Placement")]
    [SerializeField]
    private BattleVfxPlacementArea placementArea =
        BattleVfxPlacementArea.Target;
    [SerializeField]
    private BattleVfxAnchorType anchorType =
        BattleVfxAnchorType.Center;
    [SerializeField]
    private BattleVfxAttachMode attachMode =
        BattleVfxAttachMode.SpawnAtAnchor;
    [SerializeField]
    private Vector2 gridPosition = new(5f, 5f);
    [SerializeField]
    private Vector3 localPosition;
    [SerializeField]
    private Vector3 localEulerAngles;

    [Header("Scale")]
    [SerializeField]
    private BattleVfxScaleMode scaleMode =
        BattleVfxScaleMode.TileRelative;
    [SerializeField, Min(0.0001f)]
    private float uniformScale = 1f;
    [SerializeField]
    private Vector3 localScale = Vector3.one;

    [Header("Motion")]
    [SerializeField]
    private BattleVfxMotionMode motionMode =
        BattleVfxMotionMode.Stationary;
    [SerializeField]
    private Vector2 motionSourceGridPosition = new(5f, 5f);
    [SerializeField, Min(0.01f)]
    private float travelDuration = 0.25f;
    [SerializeField, Min(0f)]
    private float arcHeight = 0.5f;
    [SerializeField]
    private bool faceMotionDirection = true;

    [Header("Lifetime")]
    [SerializeField]
    private BattleVfxLifetimeMode lifetimeMode =
        BattleVfxLifetimeMode.Timed;
    [SerializeField]
    private BattleVfxStopMode stopMode =
        BattleVfxStopMode.StopEmission;
    [SerializeField]
    private bool useBattleTime = true;

    public string ClipId => clipId ?? string.Empty;
    public GameObject Prefab => prefab;
    public AudioClip AudioClip => audioClip;
    public int AudioVolumePercent => Mathf.Clamp(
        audioVolumePercent,
        0,
        100);
    public float AudioVolumeScale => AudioVolumePercent / 100f;
    public bool Required => required;
    public float StartTime => Mathf.Max(0f, startTime);
    public float Duration => Mathf.Max(0.01f, duration);
    public float TimelineEnd => StartTime + Mathf.Max(
        Duration,
        HasMotion ? TravelDuration : 0f);
    public BattleVfxPlaybackFit PlaybackFit => playbackFit;
    public BattleVfxPlacementArea PlacementArea => placementArea;
    public BattleVfxAnchorType AnchorType => anchorType;
    public BattleVfxAttachMode AttachMode => attachMode;
    public Vector2 GridPosition => ClampGridPosition(gridPosition);
    public Vector3 LocalPosition => localPosition;
    public Quaternion LocalRotation =>
        Quaternion.Euler(localEulerAngles);
    public BattleVfxScaleMode ScaleMode => scaleMode;
    public float UniformScale => Mathf.Max(0.0001f, uniformScale);
    public Vector3 LocalScale => localScale;
    public BattleVfxMotionMode MotionMode => motionMode;
    public Vector2 MotionSourceGridPosition =>
        ClampGridPosition(motionSourceGridPosition);
    public float TravelDuration => Mathf.Max(0.01f, travelDuration);
    public float ArcHeight => Mathf.Max(0f, arcHeight);
    public bool FaceMotionDirection => faceMotionDirection;
    public bool HasMotion => motionMode != BattleVfxMotionMode.Stationary;
    public BattleVfxLifetimeMode LifetimeMode => lifetimeMode;
    public BattleVfxStopMode StopMode => stopMode;
    public bool UseBattleTime => useBattleTime;
    public bool IsPersistent =>
        lifetimeMode == BattleVfxLifetimeMode.Persistent;

    public void RegenerateClipId()
    {
        clipId = Guid.NewGuid().ToString("N");
    }

    internal void CopyLegacy(BattleVfxCueSO cue)
    {
        if (cue == null)
            return;

        RegenerateClipId();
        prefab = cue.LegacyPrefab;
        audioVolumePercent = 100;
        placementArea = BattleVfxPlacementArea.Target;
        anchorType = cue.AnchorType;
        attachMode = cue.AttachMode;
        gridPosition = new Vector2(5f, 5f);
        localPosition = cue.LocalPosition;
        localEulerAngles = cue.LocalRotation.eulerAngles;
        scaleMode = BattleVfxScaleMode.TileRelative;
        uniformScale = 1f;
        localScale = cue.LocalScale;
        motionMode = cue.MotionMode;
        motionSourceGridPosition = new Vector2(5f, 5f);
        travelDuration = cue.TravelDuration;
        arcHeight = cue.ArcHeight;
        faceMotionDirection = cue.FaceMotionDirection;
        lifetimeMode = cue.LifetimeMode;
        duration = cue.Duration;
        stopMode = cue.StopMode;
        useBattleTime = cue.UseBattleTime;
    }

    internal void ValidateDefinition()
    {
        if (string.IsNullOrWhiteSpace(clipId))
            RegenerateClipId();

        audioVolumePercent = Mathf.Clamp(audioVolumePercent, 0, 100);
        startTime = IsFinite(startTime)
            ? Mathf.Max(0f, startTime)
            : 0f;
        duration = IsFinite(duration)
            ? Mathf.Max(0.01f, duration)
            : 1f;
        gridPosition = ClampGridPosition(gridPosition);
        motionSourceGridPosition =
            ClampGridPosition(motionSourceGridPosition);
        if (!IsFinite(localPosition))
            localPosition = Vector3.zero;
        if (!IsFinite(localEulerAngles))
            localEulerAngles = Vector3.zero;
        if (!IsFinite(localScale) || localScale == Vector3.zero)
            localScale = Vector3.one;
        uniformScale = IsFinite(uniformScale)
            ? Mathf.Max(0.0001f, uniformScale)
            : 1f;
        travelDuration = IsFinite(travelDuration)
            ? Mathf.Max(0.01f, travelDuration)
            : 0.25f;
        arcHeight = IsFinite(arcHeight)
            ? Mathf.Max(0f, arcHeight)
            : 0.5f;
    }

    private static Vector2 ClampGridPosition(Vector2 value)
    {
        if (!IsFinite(value))
            return new Vector2(5f, 5f);
        return new Vector2(
            Mathf.Clamp(value.x, 0f, GridDimension),
            Mathf.Clamp(value.y, 0f, GridDimension));
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[CreateAssetMenu(
    fileName = "BattleVfxCue",
    menuName = "PS260714/Effects/Battle VFX Cue",
    order = 300)]
public sealed class BattleVfxCueSO : ScriptableObject
{
    [SerializeField, HideInInspector]
    private string cueId;

    [Header("Prefab")]
    [SerializeField]
    private GameObject prefab;
    [SerializeField]
    private AudioClip audioClip;

    [Header("Composite Timeline")]
    [SerializeField]
    private List<BattleVfxClipDefinition> clips = new();

    [Header("Anchor")]
    [SerializeField]
    private BattleVfxAnchorType anchorType = BattleVfxAnchorType.Center;
    [SerializeField]
    private BattleVfxAttachMode attachMode =
        BattleVfxAttachMode.SpawnAtAnchor;
    [SerializeField]
    private Vector3 localPosition;
    [SerializeField]
    private Vector3 localEulerAngles;
    [SerializeField]
    private Vector3 localScale = Vector3.one;

    [Header("Motion")]
    [SerializeField]
    private BattleVfxMotionMode motionMode =
        BattleVfxMotionMode.Stationary;
    [SerializeField]
    private BattleVfxAnchorType motionSourceAnchorType =
        BattleVfxAnchorType.Muzzle;
    [SerializeField, Min(0.01f)]
    private float travelDuration = 0.25f;
    [SerializeField, Min(0f)]
    private float arcHeight = 0.5f;
    [SerializeField]
    private bool faceMotionDirection = true;

    [Header("Lifetime")]
    [SerializeField]
    private BattleVfxLifetimeMode lifetimeMode =
        BattleVfxLifetimeMode.ParticleSystem;
    [SerializeField, Min(0.01f)]
    private float duration = 1f;
    [SerializeField]
    private BattleVfxStopMode stopMode = BattleVfxStopMode.StopEmission;
    [SerializeField]
    private bool useBattleTime = true;

    [Header("Pool And Quality")]
    [SerializeField, Min(0)]
    private int prewarmCount = 1;
    [SerializeField, Min(1)]
    private int maximumConcurrent = 16;
    [SerializeField]
    private BattleVfxImportance importance = BattleVfxImportance.Normal;

    public string CueId => !string.IsNullOrWhiteSpace(cueId)
        ? cueId
        : name;
    public GameObject Prefab
    {
        get
        {
            if (prefab != null)
                return prefab;
            if (clips == null)
                return null;
            foreach (BattleVfxClipDefinition clip in clips)
            {
                if (clip?.Prefab != null)
                    return clip.Prefab;
            }
            return null;
        }
    }
    public GameObject LegacyPrefab => prefab;
    public AudioClip AudioClip => audioClip;
    public IReadOnlyList<BattleVfxClipDefinition> Clips => clips;
    public bool UsesClipTimeline => clips != null && clips.Count > 0;
    public BattleVfxAnchorType AnchorType => anchorType;
    public BattleVfxAttachMode AttachMode => attachMode;
    public Vector3 LocalPosition => localPosition;
    public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
    public Vector3 LocalScale => localScale;
    public BattleVfxMotionMode MotionMode => motionMode;
    public BattleVfxAnchorType MotionSourceAnchorType =>
        motionSourceAnchorType;
    public float TravelDuration => Mathf.Max(0.01f, travelDuration);
    public float ArcHeight => Mathf.Max(0f, arcHeight);
    public bool FaceMotionDirection => faceMotionDirection;
    public bool HasMotion => motionMode != BattleVfxMotionMode.Stationary;
    public float StageDuration
    {
        get
        {
            if (!UsesClipTimeline)
                return HasMotion ? TravelDuration : Duration;

            float stageDuration = 0.01f;
            foreach (BattleVfxClipDefinition clip in clips)
            {
                if (clip != null)
                    stageDuration = Mathf.Max(
                        stageDuration,
                        clip.TimelineEnd);
            }
            return stageDuration;
        }
    }
    public BattleVfxLifetimeMode LifetimeMode => lifetimeMode;
    public float Duration => Mathf.Max(0.01f, duration);
    public BattleVfxStopMode StopMode => stopMode;
    public bool UseBattleTime => useBattleTime;
    public int PrewarmCount => Mathf.Max(0, prewarmCount);
    public int MaximumConcurrent => Mathf.Max(1, maximumConcurrent);
    public BattleVfxImportance Importance => importance;
    public bool IsPersistent
    {
        get
        {
            if (!UsesClipTimeline)
            {
                return lifetimeMode ==
                       BattleVfxLifetimeMode.Persistent;
            }

            foreach (BattleVfxClipDefinition clip in clips)
            {
                if (clip != null && clip.IsPersistent)
                    return true;
            }
            return false;
        }
    }

    public void RegenerateCueId()
    {
        cueId = Guid.NewGuid().ToString("N");
    }

    public bool MigrateLegacyPrefabToTimeline()
    {
        if (UsesClipTimeline || prefab == null)
            return false;

        BattleVfxClipDefinition clip = new();
        clip.CopyLegacy(this);
        clips.Add(clip);
        prefab = null;
        ValidateDefinition();
        return true;
    }

    public void ValidateDefinition()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(cueId))
            cueId = name;

        if (!IsFinite(localPosition))
            localPosition = Vector3.zero;
        if (!IsFinite(localEulerAngles))
            localEulerAngles = Vector3.zero;
        if (!IsFinite(localScale) || localScale == Vector3.zero)
            localScale = Vector3.one;

        duration = IsFinite(duration)
            ? Mathf.Max(0.01f, duration)
            : 1f;
        travelDuration = IsFinite(travelDuration)
            ? Mathf.Max(0.01f, travelDuration)
            : 0.25f;
        arcHeight = IsFinite(arcHeight)
            ? Mathf.Max(0f, arcHeight)
            : 0.5f;
        prewarmCount = Mathf.Max(0, prewarmCount);
        maximumConcurrent = Mathf.Max(1, maximumConcurrent);

        clips ??= new List<BattleVfxClipDefinition>();
        foreach (BattleVfxClipDefinition clip in clips)
            clip?.ValidateDefinition();
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
