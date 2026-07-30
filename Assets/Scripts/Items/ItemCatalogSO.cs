using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemCatalog",
    menuName = "PS260714/Items/Item Catalog")]
public sealed class ItemCatalogSO : ScriptableObject
{
    [SerializeField] private List<ItemDefinitionSO> items = new();

    public IReadOnlyList<ItemDefinitionSO> Items =>
        items ??= new List<ItemDefinitionSO>();

    private void OnValidate()
    {
        items ??= new List<ItemDefinitionSO>();
        HashSet<string> registered =
            new(StringComparer.Ordinal);
        foreach (ItemDefinitionSO item in items)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.ItemId))
            {
                continue;
            }

            if (!registered.Add(item.ItemId))
            {
                Debug.LogError(
                    $"Duplicate item id '{item.ItemId}' in catalog.",
                    this);
            }
        }
    }
}

public static class ItemDefinitionCatalog
{
    private const string CatalogResourcePath = "ItemCatalog";
    private const string ItemResourcesPath = "Items";

    private static readonly List<ItemDefinitionSO> Definitions = new();
    private static readonly Dictionary<string, ItemDefinitionSO> ById =
        new(StringComparer.Ordinal);
    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Definitions.Clear();
        ById.Clear();
        _loaded = false;
    }

    public static IReadOnlyList<ItemDefinitionSO> GetAll()
    {
        EnsureLoaded();
        return Definitions;
    }

    public static ItemDefinitionSO Get(string itemId)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(itemId) &&
               ById.TryGetValue(itemId, out ItemDefinitionSO item)
            ? item
            : null;
    }

    public static bool TryGet(
        string itemId,
        out ItemDefinitionSO item)
    {
        item = Get(itemId);
        return item != null;
    }

    public static void Invalidate()
    {
        Definitions.Clear();
        ById.Clear();
        _loaded = false;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Definitions.Clear();
        ById.Clear();

        ItemCatalogSO catalog =
            Resources.Load<ItemCatalogSO>(CatalogResourcePath);
        if (catalog != null)
        {
            foreach (ItemDefinitionSO definition in catalog.Items)
                Register(definition);
        }
        else
        {
            ItemDefinitionSO[] loaded =
                Resources.LoadAll<ItemDefinitionSO>(
                    ItemResourcesPath);
            foreach (ItemDefinitionSO definition in loaded)
                Register(definition);
        }

        Definitions.Sort((left, right) =>
        {
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0
                ? order
                : string.Compare(
                    left.ItemId,
                    right.ItemId,
                    StringComparison.Ordinal);
        });
        _loaded = true;
    }

    private static void Register(ItemDefinitionSO definition)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(definition.ItemId))
        {
            return;
        }

        if (ById.ContainsKey(definition.ItemId))
        {
            Debug.LogError(
                $"Duplicate item id '{definition.ItemId}'.",
                definition);
            return;
        }

        ById.Add(definition.ItemId, definition);
        Definitions.Add(definition);
    }
}
