using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonDefinitionCatalog
{
    public const string FreeBattleId = "free_battle";
    public const string TutorialFieldId = "tutorial_field";
    public const string PracticeBattleId = "practice_battle";
    public const string LegacyTestFieldId = "test_field";

    [Obsolete(
        "Use TutorialFieldId. The canonical dungeon id is tutorial_field.")]
    public const string TestFieldId = TutorialFieldId;

    private const string ResourcesPath = "Dungeons";

    private static readonly Dictionary<string, DungeonDefinition>
        Definitions = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static DungeonDefinition Get(string dungeonId)
    {
        EnsureLoaded();
        string normalizedId = NormalizeDungeonId(dungeonId);
        if (!string.IsNullOrWhiteSpace(normalizedId) &&
            Definitions.TryGetValue(normalizedId, out DungeonDefinition value))
        {
            return value;
        }

        bool tutorialStage = string.Equals(
            normalizedId,
            TutorialFieldId,
            StringComparison.OrdinalIgnoreCase);
        bool practiceStage = string.Equals(
            normalizedId,
            PracticeBattleId,
            StringComparison.OrdinalIgnoreCase);
        string fallbackId = tutorialStage
            ? TutorialFieldId
            : practiceStage
                ? PracticeBattleId
                : FreeBattleId;
        DungeonDefinition fallback = DungeonDefinition.CreateRuntimeFallback(
            fallbackId,
            tutorialStage);
        Definitions[fallback.DungeonId] = fallback;
        Debug.LogWarning(
            $"Dungeon definition '{dungeonId}' was not found under " +
            $"Resources/{ResourcesPath}. A runtime fallback is being used.");
        return fallback;
    }

    public static string NormalizeDungeonId(string dungeonId)
    {
        string normalized = (dungeonId ?? string.Empty).Trim();
        if (string.Equals(
                normalized,
                LegacyTestFieldId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                normalized,
                TutorialFieldId,
                StringComparison.OrdinalIgnoreCase))
        {
            return TutorialFieldId;
        }
        if (string.Equals(
                normalized,
                PracticeBattleId,
                StringComparison.OrdinalIgnoreCase))
        {
            return PracticeBattleId;
        }
        if (string.Equals(
                normalized,
                FreeBattleId,
                StringComparison.OrdinalIgnoreCase))
        {
            return FreeBattleId;
        }

        return normalized;
    }

    public static IReadOnlyCollection<DungeonDefinition> GetAll()
    {
        EnsureLoaded();
        return Definitions.Values;
    }

    public static void Invalidate()
    {
        Definitions.Clear();
        _loaded = false;
    }

    public static IReadOnlyList<DungeonDefinition>
        GetStageSelectDefinitions()
    {
        EnsureLoaded();
        List<DungeonDefinition> result = new();
        foreach (DungeonDefinition definition in Definitions.Values)
        {
            if (definition != null && definition.IsListedInStageSelect)
                result.Add(definition);
        }

        result.Sort(CompareStageSelectDefinitions);
        return result;
    }

    private static int CompareStageSelectDefinitions(
        DungeonDefinition left,
        DungeonDefinition right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int order = left.StageOrder.CompareTo(right.StageOrder);
        return order != 0
            ? order
            : string.Compare(
                left.DungeonId,
                right.DungeonId,
                StringComparison.Ordinal);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Definitions.Clear();
        DungeonDefinition[] loaded =
            Resources.LoadAll<DungeonDefinition>(ResourcesPath);
        foreach (DungeonDefinition definition in loaded)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.DungeonId))
            {
                continue;
            }

            if (!definition.TryValidate(out string error))
            {
                Debug.LogError(
                    $"Invalid dungeon definition '{definition.name}': " +
                    error,
                    definition);
                continue;
            }

            string normalizedId = NormalizeDungeonId(definition.DungeonId);
            if (!Definitions.TryAdd(normalizedId, definition))
            {
                Debug.LogError(
                    $"Duplicate dungeon definition id: " +
                    normalizedId,
                    definition);
            }
        }

        _loaded = true;
    }
}
