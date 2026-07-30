using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ItemAssetBootstrap
{
    private const string RequestPath =
        "Temp/CreateCoreItemAssets.request";
    private const string ItemRoot = "Assets/Resources/Items";
    private const string CurrencyFolder =
        ItemRoot + "/Currency";
    private const string TicketFolder =
        ItemRoot + "/Ticket";
    private const string MaterialFolder =
        ItemRoot + "/Material";
    private const string CatalogPath =
        "Assets/Resources/ItemCatalog.asset";
    private const string MenuPath =
        "PS260714/Data/Create Core Item Assets";

    static ItemAssetBootstrap()
    {
        EditorApplication.delayCall += CreateRequestedAssets;
    }

    [MenuItem(MenuPath)]
    public static void CreateCoreItemAssets()
    {
        EnsureFolders();

        CreateCurrency(
            CurrencyFolder + "/SoftCredit.asset",
            CoreItemIds.SoftCredit,
            "인게임 크레딧",
            "IN-GAME CREDIT",
            "게임 플레이에서 획득하고 사용하는 기본 재화입니다.",
            "Basic currency earned and spent through gameplay.",
            10,
            CurrencyKind.Soft,
            false);
        CreateCurrency(
            CurrencyFolder + "/PaidCredit.asset",
            CoreItemIds.PaidCredit,
            "유료 크레딧",
            "PAID CREDIT",
            "결제를 통해 획득하는 유료 재화입니다.",
            "Premium currency obtained through purchases.",
            20,
            CurrencyKind.PremiumPaid,
            true);
        CreateCurrency(
            CurrencyFolder + "/FreeCredit.asset",
            CoreItemIds.FreeCredit,
            "무료 크레딧",
            "FREE CREDIT",
            "게임 보상으로 획득하는 무료 프리미엄 재화입니다.",
            "Free premium currency earned as gameplay rewards.",
            30,
            CurrencyKind.PremiumFree,
            false);
        CreateRecruitTicket(
            TicketFolder + "/StandardRecruitTicket.asset");
        CreateUpgradeMaterial(
            MaterialFolder + "/BasicUpgradeMaterial.asset");
        RefreshCatalog();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ItemDefinitionCatalog.Invalidate();
        Debug.Log("Core item assets created or verified.");
    }

    private static void CreateRequestedAssets()
    {
        bool requested = File.Exists(RequestPath);
        bool catalogMissing =
            AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(
                CatalogPath) == null;
        if (!requested && !catalogMissing)
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateRequestedAssets;
            return;
        }

        CreateCoreItemAssets();
        if (requested)
            File.Delete(RequestPath);
    }

    private static void CreateCurrency(
        string path,
        string itemId,
        string koreanName,
        string englishName,
        string koreanDescription,
        string englishDescription,
        int sortOrder,
        CurrencyKind currencyKind,
        bool purchasedWithRealMoney)
    {
        CurrencyItemSO item =
            GetOrCreate<CurrencyItemSO>(path, out bool created);
        if (!created)
            return;

        SerializedObject serialized = new(item);
        ConfigureCommon(
            serialized,
            itemId,
            ItemCategory.Currency,
            ItemRarity.Common,
            sortOrder,
            koreanName,
            englishName,
            koreanDescription,
            englishDescription);
        SetEnum(serialized, "currencyKind", (int)currencyKind);
        SetBool(
            serialized,
            "purchasedWithRealMoney",
            purchasedWithRealMoney);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void CreateRecruitTicket(string path)
    {
        RecruitTicketItemSO item =
            GetOrCreate<RecruitTicketItemSO>(
                path,
                out bool created);
        if (!created)
            return;

        SerializedObject serialized = new(item);
        ConfigureCommon(
            serialized,
            CoreItemIds.StandardRecruitTicket,
            ItemCategory.RecruitTicket,
            ItemRarity.Rare,
            40,
            "일반 모집권",
            "STANDARD RECRUIT TICKET",
            "일반 모집에서 1회 사용할 수 있는 모집권입니다.",
            "A ticket used for one standard recruitment.");
        SetString(serialized, "bannerGroupId", "standard");
        SetInt(serialized, "recruitsPerItem", 1);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void CreateUpgradeMaterial(string path)
    {
        UpgradeMaterialItemSO item =
            GetOrCreate<UpgradeMaterialItemSO>(
                path,
                out bool created);
        if (!created)
            return;

        SerializedObject serialized = new(item);
        ConfigureCommon(
            serialized,
            CoreItemIds.BasicUpgradeMaterial,
            ItemCategory.UpgradeMaterial,
            ItemRarity.Common,
            50,
            "기초 강화 재료",
            "BASIC UPGRADE MATERIAL",
            "대원과 장비 강화에 사용하는 기초 재료입니다.",
            "A basic material used for operator and equipment upgrades.");
        SetEnum(
            serialized,
            "target",
            (int)UpgradeMaterialTarget.Any);
        SetInt(serialized, "grade", 1);
        SetInt(serialized, "upgradeValue", 0);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void ConfigureCommon(
        SerializedObject serialized,
        string itemId,
        ItemCategory category,
        ItemRarity rarity,
        int sortOrder,
        string koreanName,
        string englishName,
        string koreanDescription,
        string englishDescription)
    {
        SetString(serialized, "itemId", itemId);
        SetEnum(serialized, "category", (int)category);
        SetEnum(serialized, "rarity", (int)rarity);
        SetInt(serialized, "sortOrder", sortOrder);
        SetString(serialized, "koreanName", koreanName);
        SetString(serialized, "englishName", englishName);
        SetString(
            serialized,
            "koreanDescription",
            koreanDescription);
        SetString(
            serialized,
            "englishDescription",
            englishDescription);
        SetLong(serialized, "maximumStack", 0L);
        SetLong(serialized, "initialAmount", 0L);
        SetBool(serialized, "hiddenInStorage", false);
    }

    internal static void RefreshCatalog()
    {
        ItemCatalogSO catalog =
            GetOrCreate<ItemCatalogSO>(
                CatalogPath,
                out bool unusedCreated);
        List<ItemDefinitionSO> items = new();
        string[] guids = AssetDatabase.FindAssets(
            string.Empty,
            new[] { ItemRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemDefinitionSO item =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                    path);
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
        SerializedProperty entries =
            serialized.FindProperty("items");
        entries.arraySize = items.Count;
        for (int index = 0; index < items.Count; index++)
        {
            entries.GetArrayElementAtIndex(index)
                .objectReferenceValue = items[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static T GetOrCreate<T>(
        string path,
        out bool created)
        where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        created = true;
        return asset;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Items");
        EnsureFolder(ItemRoot, "Currency");
        EnsureFolder(ItemRoot, "Ticket");
        EnsureFolder(ItemRoot, "Material");
    }

    private static void EnsureFolder(
        string parent,
        string folderName)
    {
        string path = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value ?? string.Empty;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetLong(
        SerializedObject serialized,
        string propertyName,
        long value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.longValue = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }
}
