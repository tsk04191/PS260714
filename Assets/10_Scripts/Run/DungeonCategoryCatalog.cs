using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonCategoryCatalog
{
    public const string DebugRoomId = "debug_room";
    public const string FreeId = "free";

    private const string ResourcesPath = "DungeonCategories";
    private static readonly Dictionary<string, DungeonCategorySO>
        Categories = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static IReadOnlyList<DungeonCategorySO> GetVisible()
    {
        EnsureLoaded();
        List<DungeonCategorySO> result = new();
        foreach (DungeonCategorySO category in Categories.Values)
        {
            if (category != null && category.ResolveDungeons().Count > 0)
                result.Add(category);
        }
        result.Sort(CompareCategories);
        return result;
    }

    public static DungeonCategorySO Get(string categoryId)
    {
        EnsureLoaded();
        string normalized = (categoryId ?? string.Empty).Trim();
        return Categories.TryGetValue(
            normalized,
            out DungeonCategorySO category)
            ? category
            : null;
    }

    public static void Invalidate()
    {
        Categories.Clear();
        _loaded = false;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Categories.Clear();
        DungeonCategorySO[] loaded =
            Resources.LoadAll<DungeonCategorySO>(ResourcesPath);
        for (int index = 0; index < loaded.Length; index++)
        {
            DungeonCategorySO category = loaded[index];
            if (category == null)
                continue;
            if (!category.TryValidate(out string error))
            {
                Debug.LogError(
                    $"Invalid dungeon category '{category.name}': " +
                    error,
                    category);
                continue;
            }
            if (!Categories.TryAdd(category.CategoryId, category))
            {
                Debug.LogError(
                    "Duplicate dungeon category id: " +
                    category.CategoryId,
                    category);
            }
        }
        _loaded = true;
    }

    private static int CompareCategories(
        DungeonCategorySO left,
        DungeonCategorySO right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        int order = left.DisplayOrder.CompareTo(right.DisplayOrder);
        return order != 0
            ? order
            : string.Compare(
                left.CategoryId,
                right.CategoryId,
                StringComparison.Ordinal);
    }
}
