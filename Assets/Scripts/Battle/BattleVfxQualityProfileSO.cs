using System;
using UnityEngine;

public enum BattleVfxQualityTier
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3
}

[CreateAssetMenu(
    fileName = "BattleVfxQualityProfile",
    menuName = "PS260714/Effects/Battle VFX Quality Profile",
    order = 301)]
public sealed class BattleVfxQualityProfileSO : ScriptableObject
{
    [Header("Quality")]
    [SerializeField]
    private BattleVfxQualityTier qualityTier = BattleVfxQualityTier.High;
    [SerializeField]
    private BattleVfxImportance minimumImportance =
        BattleVfxImportance.Low;
    [SerializeField]
    private bool enableAudio = true;

    [Header("Runtime Budget")]
    [SerializeField, Min(1)]
    private int maximumActiveInstances = 64;
    [SerializeField, Min(1)]
    private int maximumScheduledRequests = 64;
    [SerializeField, Range(0f, 1f)]
    private float prewarmScale = 1f;

    public BattleVfxQualityTier QualityTier => qualityTier;
    public BattleVfxImportance MinimumImportance => minimumImportance;
    public bool EnableAudio => enableAudio;
    public int MaximumActiveInstances =>
        Mathf.Max(1, maximumActiveInstances);
    public int MaximumScheduledRequests =>
        Mathf.Max(1, maximumScheduledRequests);
    public float PrewarmScale => Mathf.Clamp01(prewarmScale);

    public void ValidateDefinition()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (!Enum.IsDefined(typeof(BattleVfxQualityTier), qualityTier))
            qualityTier = BattleVfxQualityTier.High;
        if (!Enum.IsDefined(
                typeof(BattleVfxImportance),
                minimumImportance))
        {
            minimumImportance = BattleVfxImportance.Low;
        }

        maximumActiveInstances = Mathf.Max(1, maximumActiveInstances);
        maximumScheduledRequests = Mathf.Max(
            1,
            maximumScheduledRequests);
        prewarmScale = IsFinite(prewarmScale)
            ? Mathf.Clamp01(prewarmScale)
            : 1f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
