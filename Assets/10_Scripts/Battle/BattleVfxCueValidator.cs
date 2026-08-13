using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleVfxCueDiagnosticSeverity
{
    Warning = 0,
    Error = 1
}

public readonly struct BattleVfxCueDiagnostic
{
    public BattleVfxCueDiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Path { get; }
    public string Message { get; }

    public BattleVfxCueDiagnostic(
        BattleVfxCueDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Path = path ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public override string ToString()
    {
        string location = string.IsNullOrWhiteSpace(Path)
            ? "<root>"
            : Path;
        return $"{Severity} [{Code}] {location}: {Message}";
    }
}

public sealed class BattleVfxCueValidationResult
{
    private readonly List<BattleVfxCueDiagnostic> _diagnostics = new();

    public IReadOnlyList<BattleVfxCueDiagnostic> Diagnostics =>
        _diagnostics;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool IsValid => ErrorCount == 0;

    internal void Add(
        BattleVfxCueDiagnosticSeverity severity,
        string code,
        string path,
        string message)
    {
        _diagnostics.Add(new BattleVfxCueDiagnostic(
            severity,
            code,
            path,
            message));
        if (severity == BattleVfxCueDiagnosticSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    internal void Add(
        BattleVfxCueDiagnostic diagnostic,
        string pathPrefix)
    {
        string path = string.IsNullOrWhiteSpace(diagnostic.Path)
            ? pathPrefix
            : $"{pathPrefix}.{diagnostic.Path}";
        Add(
            diagnostic.Severity,
            diagnostic.Code,
            path,
            diagnostic.Message);
    }
}

public static class BattleVfxCueValidator
{
    public static BattleVfxCueValidationResult Validate(
        BattleVfxCueSO cue)
    {
        return Validate(cue, null);
    }

    public static BattleVfxCueValidationResult Validate(
        BattleVfxCueSO cue,
        IReadOnlyList<BattleVfxCueSO> catalog)
    {
        BattleVfxCueValidationResult result = new();
        ValidateCue(cue, result);
        if (cue != null && catalog != null)
            ValidateDuplicateId(cue, catalog, result);
        return result;
    }

    public static BattleVfxCueValidationResult ValidateAll(
        IReadOnlyList<BattleVfxCueSO> cues)
    {
        BattleVfxCueValidationResult result = new();
        if (cues == null)
        {
            AddError(
                result,
                "vfx.catalog_null",
                "cues",
                "Battle VFX cue catalog is null.");
            return result;
        }

        Dictionary<string, int> firstIndexById =
            new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < cues.Count; index++)
        {
            BattleVfxCueSO cue = cues[index];
            BattleVfxCueValidationResult cueResult = Validate(cue);
            foreach (BattleVfxCueDiagnostic diagnostic in
                     cueResult.Diagnostics)
            {
                result.Add(diagnostic, $"cues[{index}]");
            }

            string cueId = cue != null
                ? (cue.CueId ?? string.Empty).Trim()
                : string.Empty;
            if (string.IsNullOrEmpty(cueId))
                continue;

            if (firstIndexById.TryGetValue(
                    cueId,
                    out int firstIndex))
            {
                AddError(
                    result,
                    "vfx.id_duplicate",
                    $"cues[{index}].cueId",
                    $"CueId '{cueId}' duplicates cues[{firstIndex}].");
            }
            else
            {
                firstIndexById.Add(cueId, index);
            }
        }

        return result;
    }

    private static void ValidateCue(
        BattleVfxCueSO cue,
        BattleVfxCueValidationResult result)
    {
        if (cue == null)
        {
            AddError(
                result,
                "vfx.null",
                "cue",
                "Battle VFX cue is null.");
            return;
        }

        string cueId = (cue.CueId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cueId))
        {
            AddError(
                result,
                "vfx.id_missing",
                "cueId",
                "A persistent CueId is required.");
        }
        else if (ContainsWhitespace(cueId))
        {
            AddWarning(
                result,
                "vfx.id_whitespace",
                "cueId",
                "CueId contains whitespace. Regenerate the ID for a stable key.");
        }

        if (cue.UsesClipTimeline)
        {
            ValidateTimeline(cue, result);
            if (cue.LegacyPrefab != null)
            {
                AddWarning(
                    result,
                    "vfx.legacy_prefab_ignored",
                    "prefab",
                    "Timeline clips are active, so the legacy single prefab is ignored.");
            }
        }
        else
        {
            GameObject prefab = cue.LegacyPrefab;
            if (prefab == null)
            {
                if (cue.AudioClip == null)
                {
                    AddError(
                        result,
                        "vfx.output_missing",
                        "prefab",
                        "Assign a 3D prefab, an audio clip, or timeline clips.");
                }
                else
                {
                    AddWarning(
                        result,
                        "vfx.prefab_missing",
                        "prefab",
                        "This cue is audio-only and does not create a 3D effect.");
                }
            }
            else
            {
                ValidatePrefab(
                    prefab,
                    cue.LifetimeMode,
                    "prefab",
                    result);
            }

            if (cue.HasMotion)
            {
                if (cue.IsPersistent)
                {
                    AddError(
                        result,
                        "vfx.motion_persistent",
                        "lifetimeMode",
                        "A moving cue cannot use Persistent lifetime.");
                }

                if (cue.AttachMode ==
                    BattleVfxAttachMode.FollowTarget)
                {
                    AddWarning(
                        result,
                        "vfx.motion_follow",
                        "attachMode",
                        "Motion controls the transform, so Follow Target is ignored while the cue travels.");
                }
            }
        }

        if (cue.PrewarmCount > cue.MaximumConcurrent)
        {
            AddWarning(
                result,
                "vfx.prewarm_clamped",
                "prewarmCount",
                "Prewarm count exceeds the concurrent limit and will be clamped at runtime.");
        }
    }

    private static void ValidateTimeline(
        BattleVfxCueSO cue,
        BattleVfxCueValidationResult result)
    {
        HashSet<string> clipIds =
            new(StringComparer.OrdinalIgnoreCase);
        bool hasOutput = cue.AudioClip != null;
        for (int index = 0; index < cue.Clips.Count; index++)
        {
            BattleVfxClipDefinition clip = cue.Clips[index];
            string path = $"clips[{index}]";
            if (clip == null)
            {
                AddError(
                    result,
                    "vfx.clip_null",
                    path,
                    "Timeline clip is null.");
                continue;
            }

            string clipId = (clip.ClipId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(clipId))
            {
                AddError(
                    result,
                    "vfx.clip_id_missing",
                    $"{path}.clipId",
                    "Timeline clips require a stable ClipId.");
            }
            else if (!clipIds.Add(clipId))
            {
                AddError(
                    result,
                    "vfx.clip_id_duplicate",
                    $"{path}.clipId",
                    $"ClipId '{clipId}' is duplicated in this cue.");
            }

            if (clip.Prefab == null && clip.AudioClip == null)
            {
                AddError(
                    result,
                    "vfx.clip_output_missing",
                    $"{path}.prefab",
                    "Assign a 3D prefab or audio clip.");
            }
            else
            {
                hasOutput = true;
            }

            if (clip.Prefab != null)
            {
                ValidatePrefab(
                    clip.Prefab,
                    clip.LifetimeMode,
                    $"{path}.prefab",
                    result);
                bool canAdjustPlayback =
                    clip.Prefab.GetComponentInChildren<ParticleSystem>(
                        true) != null ||
                    clip.Prefab.GetComponentInChildren<Animator>(
                        true) != null;
                if (clip.PlaybackFit != BattleVfxPlaybackFit.Natural &&
                    !canAdjustPlayback)
                {
                    AddWarning(
                        result,
                        "vfx.clip_playback_unsupported",
                        $"{path}.playbackFit",
                        "Speed and loop fitting require a ParticleSystem or Animator.");
                }
            }

            if (clip.HasMotion && clip.IsPersistent)
            {
                AddError(
                    result,
                    "vfx.clip_motion_persistent",
                    $"{path}.lifetimeMode",
                    "A moving timeline clip cannot be persistent.");
            }
            if (clip.HasMotion &&
                clip.AttachMode == BattleVfxAttachMode.FollowTarget)
            {
                AddWarning(
                    result,
                    "vfx.clip_motion_follow",
                    $"{path}.attachMode",
                    "Motion controls the transform, so Follow Target is ignored while the clip travels.");
            }
        }

        if (!hasOutput)
        {
            AddError(
                result,
                "vfx.output_missing",
                "clips",
                "The timeline does not contain a playable prefab or audio clip.");
        }
    }

    private static void ValidatePrefab(
        GameObject prefab,
        BattleVfxLifetimeMode lifetimeMode,
        string path,
        BattleVfxCueValidationResult result)
    {
        bool hasParticleSystem =
            prefab.GetComponentInChildren<ParticleSystem>(true) != null;
        if (lifetimeMode == BattleVfxLifetimeMode.ParticleSystem &&
            !hasParticleSystem)
        {
            AddWarning(
                result,
                "vfx.particle_missing",
                path,
                "ParticleSystem lifetime is selected, but the prefab has no ParticleSystem.");
        }

        if (prefab.GetComponent<RectTransform>() != null)
        {
            AddWarning(
                result,
                "vfx.ui_prefab",
                path,
                "The prefab root uses RectTransform. Confirm that this is a world-space 3D effect.");
        }
    }

    private static void ValidateDuplicateId(
        BattleVfxCueSO cue,
        IReadOnlyList<BattleVfxCueSO> catalog,
        BattleVfxCueValidationResult result)
    {
        string cueId = (cue.CueId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cueId))
            return;

        for (int index = 0; index < catalog.Count; index++)
        {
            BattleVfxCueSO other = catalog[index];
            if (other == null || ReferenceEquals(other, cue))
                continue;
            if (!string.Equals(
                    cueId,
                    (other.CueId ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddError(
                result,
                "vfx.id_duplicate",
                "cueId",
                $"CueId '{cueId}' is also used by '{other.name}'.");
            return;
        }
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
                return true;
        }

        return false;
    }

    private static void AddWarning(
        BattleVfxCueValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            BattleVfxCueDiagnosticSeverity.Warning,
            code,
            path,
            message);
    }

    private static void AddError(
        BattleVfxCueValidationResult result,
        string code,
        string path,
        string message)
    {
        result.Add(
            BattleVfxCueDiagnosticSeverity.Error,
            code,
            path,
            message);
    }
}
