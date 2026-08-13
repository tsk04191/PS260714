using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonDefinitionCatalog
{
    public const string FreeBattleId = "free_battle";
    public const string TestFieldId = "test_field";

    private const string ResourcesPath = "Dungeons";

    private static readonly Dictionary<string, DungeonDefinition>
        Definitions = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static DungeonDefinition Get(string dungeonId)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(dungeonId) &&
            Definitions.TryGetValue(dungeonId.Trim(), out DungeonDefinition value))
        {
            return value;
        }

        bool tutorialStage = string.Equals(
            dungeonId,
            TestFieldId,
            StringComparison.OrdinalIgnoreCase);
        DungeonDefinition fallback = DungeonDefinition.CreateRuntimeFallback(
            tutorialStage ? TestFieldId : FreeBattleId,
            tutorialStage);
        Definitions[fallback.DungeonId] = fallback;
        Debug.LogWarning(
            $"Dungeon definition '{dungeonId}' was not found under " +
            $"Resources/{ResourcesPath}. A runtime fallback is being used.");
        return fallback;
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

            if (!Definitions.TryAdd(definition.DungeonId, definition))
            {
                Debug.LogError(
                    $"Duplicate dungeon definition id: " +
                    definition.DungeonId,
                    definition);
            }
        }

        _loaded = true;
    }
}
