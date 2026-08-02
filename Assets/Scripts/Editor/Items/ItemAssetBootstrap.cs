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
    private const string BattleFolder =
        ItemRoot + "/Battle";
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
        CreateBattleItem(
            BattleFolder + "/FocusItem.asset",
            CoreBattleItemIds.Focus,
            "\uC9D1\uC911 \uD45C\uC2DD",
            "FOCUS MARKER",
            "\uC120\uD0DD\uD55C \uC801\uC744 5\uCD08 \uB3D9\uC548 \uCD5C\uC6B0\uC120 \uACF5\uACA9 \uB300\uC0C1\uC73C\uB85C \uC9C0\uC815\uD558\uB294 \uC77C\uD68C\uC6A9 \uC544\uC774\uD15C\uC785\uB2C8\uB2E4.",
            "A single-use item that marks an enemy as the highest-priority target for 5 seconds.",
            100,
            ItemRarity.Uncommon,
            BattleItemTargetType.Enemy,
            1,
            BattleItemEffectType.ForcePriorityTarget,
            1,
            5f,
            1f,
            1f);
        CreateBattleItem(
            BattleFolder + "/Molotov.asset",
            CoreBattleItemIds.Molotov,
            "\uD654\uC5FC\uBCD1",
            "MOLOTOV",
            "\uC120\uD0DD\uD55C \uC801\uC5D0\uAC8C 3\uCD08 \uB3D9\uC548 \uB9E4\uCD08 1\uC758 \uD654\uC5FC \uD53C\uD574\uB97C \uC8FC\uB294 \uC77C\uD68C\uC6A9 \uC544\uC774\uD15C\uC785\uB2C8\uB2E4.",
            "A single-use item that deals 1 fire damage per second to an enemy for 3 seconds.",
            110,
            ItemRarity.Uncommon,
            BattleItemTargetType.Enemy,
            3,
            BattleItemEffectType.ApplyFire,
            1,
            3f,
            1f,
            1f);
        CreateBattleItem(
            BattleFolder + "/PrecisionShot.asset",
            CoreBattleItemIds.PrecisionShot,
            "\uC815\uBC00 \uC0AC\uACA9",
            "PRECISION SHOT",
            "\uC120\uD0DD\uD55C \uC801\uC5D0\uAC8C \uC989\uC2DC 5\uC758 \uD53C\uD574\uB97C \uC8FC\uB294 \uC77C\uD68C\uC6A9 \uC544\uC774\uD15C\uC785\uB2C8\uB2E4.",
            "A single-use item that immediately deals 5 damage to an enemy.",
            120,
            ItemRarity.Uncommon,
            BattleItemTargetType.Enemy,
            2,
            BattleItemEffectType.FixedDamage,
            5,
            0f,
            1f,
            1f);
        CreateBattleItem(
            BattleFolder + "/OverSupply.asset",
            CoreBattleItemIds.OverSupply,
            "\uACFC\uC789 \uBCF4\uAE09",
            "OVER SUPPLY",
            "\uC120\uD0DD\uD55C \uD130\uB81B\uC758 \uACF5\uACA9 \uC18D\uB3C4\uB97C 5\uCD08 \uB3D9\uC548 2\uBC30\uB85C \uB9CC\uB4DC\uB294 \uC77C\uD68C\uC6A9 \uC544\uC774\uD15C\uC785\uB2C8\uB2E4.",
            "A single-use item that doubles a turret's attack speed for 5 seconds.",
            130,
            ItemRarity.Rare,
            BattleItemTargetType.Turret,
            3,
            BattleItemEffectType.AttackSpeedBoost,
            1,
            5f,
            1f,
            2f);
        CreateBattleItem(
            BattleFolder + "/Overheat.asset",
            CoreBattleItemIds.Overheat,
            "\uACFC\uC5F4",
            "OVERHEAT",
            "\uC120\uD0DD\uD55C \uD130\uB81B\uC758 \uACF5\uACA9\uB825\uC744 3\uCD08 \uB3D9\uC548 2\uBC30\uB85C \uB9CC\uB4DC\uB294 \uC77C\uD68C\uC6A9 \uC544\uC774\uD15C\uC785\uB2C8\uB2E4.",
            "A single-use item that doubles a turret's power for 3 seconds.",
            140,
            ItemRarity.Rare,
            BattleItemTargetType.Turret,
            3,
            BattleItemEffectType.PowerBoost,
            1,
            3f,
            1f,
            2f);
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

    private static void CreateBattleItem(
        string path,
        string itemId,
        string koreanName,
        string englishName,
        string koreanDescription,
        string englishDescription,
        int sortOrder,
        ItemRarity rarity,
        BattleItemTargetType targetType,
        int energyCost,
        BattleItemEffectType effectType,
        int amount,
        float duration,
        float interval,
        float multiplier)
    {
        BattleItemSO item =
            GetOrCreate<BattleItemSO>(path, out bool created);
        if (!created)
            return;

        SerializedObject serialized = new(item);
        ConfigureCommon(
            serialized,
            itemId,
            ItemCategory.Consumable,
            rarity,
            sortOrder,
            koreanName,
            englishName,
            koreanDescription,
            englishDescription);
        SetBool(serialized, "hiddenInStorage", true);
        SetEnum(serialized, "targetType", (int)targetType);
        SetEnum(
            serialized,
            "usePolicy",
            (int)BattleItemUsePolicy.SingleUse);
        SetInt(serialized, "limitedUses", 2);
        SetInt(serialized, "maximumRunUses", 0);
        SetInt(serialized, "energyCost", energyCost);
        SetFloat(serialized, "cooldown", 0f);
        SetBool(serialized, "availableAsDungeonReward", true);
        SetBool(serialized, "availableAsStartingItem", true);

        SerializedProperty effects = serialized.FindProperty("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("effectType").enumValueIndex =
            (int)effectType;
        effect.FindPropertyRelative("amount").intValue = amount;
        effect.FindPropertyRelative("duration").floatValue = duration;
        effect.FindPropertyRelative("interval").floatValue = interval;
        effect.FindPropertyRelative("multiplier").floatValue = multiplier;

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
        EnsureFolder(ItemRoot, "Battle");
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

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
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
