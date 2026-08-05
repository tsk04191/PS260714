using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ItemAssetBootstrap
{
    private const string ItemRoot = "Assets/Resources/Items";
    private const string CatalogPath =
        "Assets/Resources/ItemCatalog.asset";
    [MenuItem(
        PS260714EditorMenu.MigrateBattleItemUsage,
        false,
        PS260714EditorMenu.MigrateBattleItemUsagePriority)]
    public static void MigrateBattleItemAssets()
    {
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:BattleItemSO",
                     new[] { ItemRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleItemSO item = AssetDatabase.LoadAssetAtPath<BattleItemSO>(
                path);
            if (item == null)
                continue;

            SerializedObject serialized = new(item);
            SerializedProperty schema = serialized.FindProperty(
                "usageSchemaVersion");
            if (schema != null && schema.intValue <= 0)
            {
                BattleItemUsePolicy legacyPolicy =
                    (BattleItemUsePolicy)(serialized.FindProperty(
                        "usePolicy")?.enumValueIndex ?? 0);
                SetEnum(
                    serialized,
                    "lifecycle",
                    legacyPolicy == BattleItemUsePolicy.SingleUse
                        ? (int)BattleItemLifecycle.Disposable
                        : (int)BattleItemLifecycle.Reusable);
                SetEnum(
                    serialized,
                    "chargeMode",
                    legacyPolicy == BattleItemUsePolicy.UnlimitedUse
                        ? (int)BattleItemChargeMode.Unlimited
                        : (int)BattleItemChargeMode.Limited);
                if (legacyPolicy == BattleItemUsePolicy.SingleUse)
                    SetInt(serialized, "limitedUses", 1);
                schema.intValue = 1;
            }

            SerializedProperty effects = serialized.FindProperty("effects");
            for (int index = 0;
                 effects != null && index < effects.arraySize;
                 index++)
            {
                SerializedProperty effect =
                    effects.GetArrayElementAtIndex(index);
                SerializedProperty effectSchema =
                    effect.FindPropertyRelative("schemaVersion");
                if (effectSchema == null || effectSchema.intValue > 0)
                    continue;

                BattleItemEffectType type =
                    (BattleItemEffectType)(effect.FindPropertyRelative(
                        "effectType")?.enumValueIndex ?? 0);
                effect.FindPropertyRelative("scope").enumValueIndex =
                    (int)BattleItemEffectScope.CurrentBattle;
                effect.FindPropertyRelative("durationMode").enumValueIndex =
                    type == BattleItemEffectType.FixedDamage
                        ? (int)BattleItemEffectDurationMode.Instant
                        : (int)BattleItemEffectDurationMode.Timed;
                effectSchema.intValue = 1;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
            }
        }
    }

    internal static void RefreshCatalog()
    {
        ItemCatalogSO catalog =
            GetOrCreate<ItemCatalogSO>(CatalogPath);
        List<ItemDefinitionSO> items = new();
        string[] guids = AssetDatabase.FindAssets(
            string.Empty,
            new[] { ItemRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemDefinitionSO item =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (item != null)
                items.Add(item);
        }

        items.Sort((left, right) =>
        {
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0
                ? order
                : string.Compare(
                    left.ItemId,
                    right.ItemId,
                    StringComparison.Ordinal);
        });

        SerializedObject serialized = new(catalog);
        SerializedProperty entries = serialized.FindProperty("items");
        entries.arraySize = items.Count;
        for (int index = 0; index < items.Count; index++)
        {
            entries.GetArrayElementAtIndex(index)
                .objectReferenceValue = items[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static T GetOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }
}
