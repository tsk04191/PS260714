using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyWaveCompositionState
{
    private readonly Dictionary<string, int> countsByEnemyId = new(
        StringComparer.OrdinalIgnoreCase);

    public int NormalCount { get; private set; }
    public int SpecialCount { get; private set; }
    public int EliteCount { get; private set; }
    public int SupportCount { get; private set; }

    public int GetCount(EnemySO definition)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(definition.EnemyId))
        {
            return 0;
        }
        return countsByEnemyId.TryGetValue(
            definition.EnemyId,
            out int count)
            ? count
            : 0;
    }

    internal void Register(EnemySO definition)
    {
        if (definition == null)
            return;

        string enemyId = definition.EnemyId;
        if (!string.IsNullOrWhiteSpace(enemyId))
        {
            countsByEnemyId.TryGetValue(enemyId, out int count);
            countsByEnemyId[enemyId] = count + 1;
        }

        switch (definition.Grade)
        {
            case EEnemyGrade.Special:
                SpecialCount++;
                break;
            case EEnemyGrade.Elite:
                EliteCount++;
                break;
            case EEnemyGrade.Boss:
                break;
            default:
                NormalCount++;
                break;
        }

        if (EnemyWaveComposer.HasRoleTag(definition, "support"))
            SupportCount++;
    }
}

public static class EnemyWaveComposer
{
    public const int DefaultMaximumSpecialPerWave = 2;
    public const int DefaultMaximumElitePerWave = 1;
    public const int DefaultMaximumSupportPerWave = 2;

    public static EnemySO SelectAndRegister(
        IReadOnlyList<EnemySO> definitions,
        float progress,
        System.Random random,
        EnemyWaveCompositionState state,
        int maximumSpecialPerWave = DefaultMaximumSpecialPerWave,
        int maximumElitePerWave = DefaultMaximumElitePerWave,
        int maximumSupportPerWave = DefaultMaximumSupportPerWave)
    {
        if (definitions == null || definitions.Count == 0 ||
            random == null || state == null)
        {
            return null;
        }

        List<EnemySO> candidates = new();
        for (int index = 0; index < definitions.Count; index++)
        {
            EnemySO definition = definitions[index];
            if (CanSelect(
                    definition,
                    state,
                    maximumSpecialPerWave,
                    maximumElitePerWave,
                    maximumSupportPerWave))
            {
                candidates.Add(definition);
            }
        }

        if (candidates.Count == 0)
        {
            // A normal, non-support enemy is the safest cap-preserving
            // fallback when authored per-enemy limits exhaust the pool.
            for (int index = 0; index < definitions.Count; index++)
            {
                EnemySO definition = definitions[index];
                if (IsAutomaticEncounterCandidate(definition) &&
                    definition.Grade == EEnemyGrade.Normal &&
                    !HasRoleTag(definition, "support"))
                {
                    candidates.Add(definition);
                }
            }
        }

        if (candidates.Count == 0)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                EnemySO definition = definitions[index];
                if (IsAutomaticEncounterCandidate(definition))
                    candidates.Add(definition);
            }
        }

        EnemySO selected = SelectWeighted(candidates, progress, random);
        if (selected != null)
            state.Register(selected);
        return selected;
    }

    internal static bool HasRoleTag(
        EnemySO definition,
        string roleTag)
    {
        if (definition?.RoleTags == null ||
            string.IsNullOrWhiteSpace(roleTag))
        {
            return false;
        }

        for (int index = 0; index < definition.RoleTags.Count; index++)
        {
            if (string.Equals(
                    definition.RoleTags[index]?.Trim(),
                    roleTag,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CanSelect(
        EnemySO definition,
        EnemyWaveCompositionState state,
        int maximumSpecialPerWave,
        int maximumElitePerWave,
        int maximumSupportPerWave)
    {
        if (!IsAutomaticEncounterCandidate(definition))
            return false;

        int authoredMaximum = definition.RecommendedMaxPerWave;
        if (authoredMaximum > 0 &&
            state.GetCount(definition) >= authoredMaximum)
        {
            return false;
        }

        if (definition.Grade == EEnemyGrade.Special &&
            state.SpecialCount >= Mathf.Max(0, maximumSpecialPerWave))
        {
            return false;
        }
        if (definition.Grade == EEnemyGrade.Elite &&
            state.EliteCount >= Mathf.Max(0, maximumElitePerWave))
        {
            return false;
        }
        if (HasRoleTag(definition, "support") &&
            state.SupportCount >= Mathf.Max(0, maximumSupportPerWave))
        {
            return false;
        }
        return true;
    }

    private static bool IsAutomaticEncounterCandidate(EnemySO definition)
    {
        return definition != null && !definition.EncounterOnly &&
               definition.Grade != EEnemyGrade.Boss;
    }

    private static EnemySO SelectWeighted(
        IReadOnlyList<EnemySO> definitions,
        float progress,
        System.Random random)
    {
        if (definitions == null || definitions.Count == 0)
            return null;

        double exponent = Mathf.Lerp(
            -1.5f,
            0.9f,
            Mathf.Clamp01(progress));
        double totalWeight = 0d;
        double[] weights = new double[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
        {
            double budget = Mathf.Max(
                0.1f,
                definitions[index].SpawnBudget);
            double jitter = 0.9d + random.NextDouble() * 0.2d;
            weights[index] = Math.Pow(budget, exponent) * jitter;
            totalWeight += weights[index];
        }

        double value = random.NextDouble() * totalWeight;
        for (int index = 0; index < definitions.Count; index++)
        {
            value -= weights[index];
            if (value <= 0d)
                return definitions[index];
        }
        return definitions[definitions.Count - 1];
    }
}
