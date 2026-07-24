using System;
using System.Collections.Generic;
using UnityEngine;

public static class EnemyDefinitionCatalog
{
    private const string ResourcesPath = "Enemies";

    private static readonly List<EnemySO> Definitions = new();
    private static readonly Dictionary<string, EnemySO> DefinitionsById =
        new(StringComparer.Ordinal);
    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Definitions.Clear();
        DefinitionsById.Clear();
        _loaded = false;
    }

    public static IReadOnlyList<EnemySO> GetAll()
    {
        EnsureLoaded();
        return Definitions;
    }

    public static EnemySO FindById(string enemyId)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(enemyId) &&
               DefinitionsById.TryGetValue(
                   enemyId.Trim(),
                   out EnemySO definition)
            ? definition
            : null;
    }

    public static void Invalidate()
    {
        Definitions.Clear();
        DefinitionsById.Clear();
        _loaded = false;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Definitions.Clear();
        DefinitionsById.Clear();
        EnemySO[] loaded = Resources.LoadAll<EnemySO>(ResourcesPath);
        foreach (EnemySO definition in loaded)
        {
            if (definition == null)
                continue;

            string enemyId = definition.EnemyId;
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                Debug.LogError(
                    $"EnemySO '{definition.name}' has no persistent id.",
                    definition);
                continue;
            }

            if (!DefinitionsById.TryAdd(enemyId, definition))
            {
                Debug.LogError(
                    $"Duplicate enemy id '{enemyId}' under " +
                    $"Resources/{ResourcesPath}.",
                    definition);
                continue;
            }

            Definitions.Add(definition);
        }

        Definitions.Sort((left, right) => string.Compare(
            left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty,
            StringComparison.OrdinalIgnoreCase));
        _loaded = true;
    }
}
