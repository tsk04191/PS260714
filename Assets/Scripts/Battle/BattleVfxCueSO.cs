using System;
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
    public GameObject Prefab => prefab;
    public AudioClip AudioClip => audioClip;
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
    public float StageDuration => HasMotion ? TravelDuration : Duration;
    public BattleVfxLifetimeMode LifetimeMode => lifetimeMode;
    public float Duration => Mathf.Max(0.01f, duration);
    public BattleVfxStopMode StopMode => stopMode;
    public bool UseBattleTime => useBattleTime;
    public int PrewarmCount => Mathf.Max(0, prewarmCount);
    public int MaximumConcurrent => Mathf.Max(1, maximumConcurrent);
    public BattleVfxImportance Importance => importance;
    public bool IsPersistent =>
        lifetimeMode == BattleVfxLifetimeMode.Persistent;

    public void RegenerateCueId()
    {
        cueId = Guid.NewGuid().ToString("N");
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
