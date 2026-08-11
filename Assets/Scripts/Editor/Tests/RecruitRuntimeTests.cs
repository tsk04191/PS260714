using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RecruitRuntimeTests
{
    private const string InventoryPlayerPrefsKey =
        "Inventory.Collection.v1";
    private const string CharacterPlayerPrefsKey =
        "Characters.Collection.v1";

    [Test]
    public void MissingLocalization_UsesSingleBannerFallback()
    {
        RecruitBannerPageDefinition banner = new();
        string missingKey = $"test.missing.{System.Guid.NewGuid():N}";
        SetPrivateField(banner, "titleLocalizationKey", missingKey + ".title");
        SetPrivateField(
            banner,
            "descriptionLocalizationKey",
            missingKey + ".description");
        SetPrivateField(banner, "periodLocalizationKey", missingKey + ".period");
        SetPrivateField(banner, "fallbackTitle", "BANNER_IDENTIFIER");
        SetPrivateField(
            banner,
            "fallbackDescription",
            "BANNER_DESCRIPTION_IDENTIFIER");
        SetPrivateField(banner, "fallbackPeriod", "PERIOD_IDENTIFIER");

        RecruitBannerPageModel korean = banner.CreateModel(true);
        RecruitBannerPageModel english = banner.CreateModel(false);

        Assert.That(korean.Title, Is.EqualTo("BANNER_IDENTIFIER"));
        Assert.That(english.Title, Is.EqualTo(korean.Title));
        Assert.That(
            english.Description,
            Is.EqualTo("BANNER_DESCRIPTION_IDENTIFIER"));
        Assert.That(english.Description, Is.EqualTo(korean.Description));
        Assert.That(english.Period, Is.EqualTo("PERIOD_IDENTIFIER"));
        Assert.That(english.Period, Is.EqualTo(korean.Period));

        string[] removedFields =
        {
            "koreanTitle",
            "englishTitle",
            "koreanDescription",
            "englishDescription",
            "koreanPeriod",
            "englishPeriod",
        };
        foreach (string fieldName in removedFields)
        {
            Assert.That(
                typeof(RecruitBannerPageDefinition).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                fieldName);
        }
    }

    [Test]
    public void PaymentRoute_PrefersOwnedAndAffordableResource()
    {
        RecruitTicketItemSO ticket =
            ScriptableObject.CreateInstance<RecruitTicketItemSO>();
        CurrencyItemSO credit =
            ScriptableObject.CreateInstance<CurrencyItemSO>();
        try
        {
            ConfigureItem(ticket, "test.ticket");
            ConfigureItem(credit, "test.credit");

            RecruitPaymentRouteDefinition ticketRoute =
                CreateRoute(ticket, 1L, 1);
            RecruitPaymentRouteDefinition creditRoute =
                CreateRoute(credit, 500L, 2);
            RecruitBannerPageDefinition banner =
                CreateBanner(
                    ticketRoute,
                    creditRoute);

            InventoryData ticketInventory = CreateInventory(
                ("test.ticket", 1L),
                ("test.credit", 1000L));
            RecruitPaymentRouteSelection ticketSelection =
                banner.ResolvePaymentRoute(1, ticketInventory);
            Assert.That(ticketSelection.Item, Is.SameAs(ticket));

            InventoryData creditInventory = CreateInventory(
                ("test.ticket", 0L),
                ("test.credit", 1000L));
            RecruitPaymentRouteSelection creditSelection =
                banner.ResolvePaymentRoute(1, creditInventory);
            Assert.That(creditSelection.Item, Is.SameAs(credit));

            InventoryData tenInventory = CreateInventory(
                ("test.ticket", 5L),
                ("test.credit", 5000L));
            RecruitPaymentRouteSelection tenSelection =
                banner.ResolvePaymentRoute(10, tenInventory);
            Assert.That(tenSelection.Item, Is.SameAs(credit));
        }
        finally
        {
            Object.DestroyImmediate(ticket);
            Object.DestroyImmediate(credit);
        }
    }

    [Test]
    public void LegacyRewardPool_MigratesWithoutChangingFinalProbabilities()
    {
        RecruitBannerPageDefinition banner = new();
        SetPrivateField(
            banner,
            "rateInputMode",
            RecruitRateInputMode.Percentage);
        SetPrivateField(
            banner,
            "dummyPool",
            new List<RecruitDummyPoolEntry>
            {
                CreateDummyReward(
                    "GRADE 0",
                    CharacterGrade.Grade0,
                    40f),
                CreateDummyReward(
                    "GRADE 1-A",
                    CharacterGrade.Grade1,
                    30f),
                CreateDummyReward(
                    "GRADE 1-B",
                    CharacterGrade.Grade1,
                    30f),
            });

        Assert.That(banner.EnsureRewardPoolData(), Is.True);
        Assert.That(banner.GradePools.Count, Is.EqualTo(4));
        Assert.That(
            RecruitGradeProbabilityTable.TryCreate(
                banner.GradePools,
                banner.RateInputMode,
                out RecruitGradeProbabilityTable table,
                out string error),
            Is.True,
            error);

        int grade0Pool = FindGradePool(
            banner.GradePools,
            CharacterGrade.Grade0);
        int grade1Pool = FindGradePool(
            banner.GradePools,
            CharacterGrade.Grade1);
        Assert.That(
            table.GetGradeProbability(grade0Pool),
            Is.EqualTo(0.4d).Within(0.000001d));
        Assert.That(
            table.GetFinalProbability(grade0Pool, 0),
            Is.EqualTo(0.4d).Within(0.000001d));
        Assert.That(
            table.GetGradeProbability(grade1Pool),
            Is.EqualTo(0.6d).Within(0.000001d));
        Assert.That(
            table.GetFinalProbability(grade1Pool, 0),
            Is.EqualTo(0.3d).Within(0.000001d));
        Assert.That(
            table.GetFinalProbability(grade1Pool, 1),
            Is.EqualTo(0.3d).Within(0.000001d));
        Assert.That(
            table.SampleGrade(0.2d),
            Is.EqualTo(grade0Pool));
        Assert.That(
            table.SampleGrade(0.8d),
            Is.EqualTo(grade1Pool));
        Assert.That(
            table.SampleReward(grade1Pool, 0.2d),
            Is.EqualTo(0));
        Assert.That(
            table.SampleReward(grade1Pool, 0.8d),
            Is.EqualTo(1));
    }

    [Test]
    public void RecruitExecution_SpendsCurrencyAndReturnsResult()
    {
        bool hadSave = PlayerPrefs.HasKey(
            InventoryPlayerPrefsKey);
        string previousSave = hadSave
            ? PlayerPrefs.GetString(InventoryPlayerPrefsKey)
            : string.Empty;

        try
        {
            CurrencyItemSO credit =
                Resources.Load<CurrencyItemSO>(
                    "Items/Currency/FreeCredit");
            Assert.That(credit, Is.Not.Null);

            RecruitBannerPageDefinition banner =
                CreateBanner(CreateRoute(credit, 500L, 0));
            InventoryData inventory = CreateInventory(
                (credit.ItemId, 1000L));

            bool succeeded = banner.TryRecruit(
                inventory,
                1,
                true,
                out RecruitExecutionResult result,
                out string error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DrawCount, Is.EqualTo(1));
            Assert.That(
                inventory.GetAmount(credit),
                Is.EqualTo(500L));
        }
        finally
        {
            if (hadSave)
            {
                PlayerPrefs.SetString(
                    InventoryPlayerPrefsKey,
                    previousSave);
            }
            else
            {
                PlayerPrefs.DeleteKey(
                    InventoryPlayerPrefsKey);
            }
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void RecruitExecution_GrantsCharacterAndMarksOnlyFirstAsNew()
    {
        bool hadInventorySave =
            PlayerPrefs.HasKey(InventoryPlayerPrefsKey);
        string previousInventorySave = hadInventorySave
            ? PlayerPrefs.GetString(InventoryPlayerPrefsKey)
            : string.Empty;
        bool hadCharacterSave =
            PlayerPrefs.HasKey(CharacterPlayerPrefsKey);
        string previousCharacterSave = hadCharacterSave
            ? PlayerPrefs.GetString(CharacterPlayerPrefsKey)
            : string.Empty;

        try
        {
            CurrencyItemSO credit =
                Resources.Load<CurrencyItemSO>(
                    "Items/Currency/FreeCredit");
            CharacterSO character =
                Resources.Load<CharacterSO>(
                    "Characters/2_Byeolha");
            Assert.That(credit, Is.Not.Null);
            Assert.That(character, Is.Not.Null);
            Assert.That(character.InitiallyOwned, Is.False);

            RecruitBannerPageDefinition banner =
                CreateBanner(CreateRoute(credit, 100L, 0));
            SetPrivateField(
                banner,
                "dummyPool",
                new List<RecruitDummyPoolEntry>
                {
                    CreateRewardEntry(
                        RecruitRewardType.Character,
                        character,
                        null,
                        1L),
                });
            InventoryData inventory = CreateInventory(
                (credit.ItemId, 500L));
            CharacterCollectionData characters = new();

            Assert.That(
                banner.TryRecruit(
                    inventory,
                    characters,
                    1,
                    true,
                    out RecruitExecutionResult first,
                    out string firstError),
                Is.True,
                firstError);
            Assert.That(first.Entries[0].Character, Is.SameAs(character));
            Assert.That(first.Entries[0].IsNew, Is.True);
            Assert.That(
                characters.GetOrCreate(character, false).IsOwned,
                Is.True);

            Assert.That(
                banner.TryRecruit(
                    inventory,
                    characters,
                    1,
                    true,
                    out RecruitExecutionResult duplicate,
                    out string duplicateError),
                Is.True,
                duplicateError);
            Assert.That(duplicate.Entries[0].IsNew, Is.False);
        }
        finally
        {
            RestorePlayerPrefs(
                InventoryPlayerPrefsKey,
                hadInventorySave,
                previousInventorySave);
            RestorePlayerPrefs(
                CharacterPlayerPrefsKey,
                hadCharacterSave,
                previousCharacterSave);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void RecruitExecution_GrantsConfiguredItemAmount()
    {
        bool hadSave = PlayerPrefs.HasKey(
            InventoryPlayerPrefsKey);
        string previousSave = hadSave
            ? PlayerPrefs.GetString(InventoryPlayerPrefsKey)
            : string.Empty;

        try
        {
            CurrencyItemSO credit =
                Resources.Load<CurrencyItemSO>(
                    "Items/Currency/FreeCredit");
            ItemDefinitionSO material =
                Resources.Load<ItemDefinitionSO>(
                    "Items/Material/BasicUpgradeMaterial");
            Assert.That(credit, Is.Not.Null);
            Assert.That(material, Is.Not.Null);

            RecruitBannerPageDefinition banner =
                CreateBanner(CreateRoute(credit, 100L, 0));
            SetPrivateField(
                banner,
                "dummyPool",
                new List<RecruitDummyPoolEntry>
                {
                    CreateRewardEntry(
                        RecruitRewardType.Item,
                        null,
                        material,
                        7L),
                });
            InventoryData inventory = CreateInventory(
                (credit.ItemId, 500L),
                (material.ItemId, 3L));

            Assert.That(
                banner.TryRecruit(
                    inventory,
                    new CharacterCollectionData(),
                    1,
                    true,
                    out RecruitExecutionResult result,
                    out string error),
                Is.True,
                error);
            Assert.That(inventory.GetAmount(credit), Is.EqualTo(400L));
            Assert.That(inventory.GetAmount(material), Is.EqualTo(10L));
            Assert.That(
                result.Entries[0].RewardType,
                Is.EqualTo(RecruitRewardType.Item));
            Assert.That(result.Entries[0].Item, Is.SameAs(material));
            Assert.That(result.Entries[0].Amount, Is.EqualTo(7L));
        }
        finally
        {
            RestorePlayerPrefs(
                InventoryPlayerPrefsKey,
                hadSave,
                previousSave);
            PlayerPrefs.Save();
        }
    }

    private static RecruitBannerPageDefinition CreateBanner(
        params RecruitPaymentRouteDefinition[] routes)
    {
        RecruitBannerPageDefinition banner = new();
        SetPrivateField(
            banner,
            "paymentRoutes",
            new List<RecruitPaymentRouteDefinition>(routes));
        SetPrivateField(
            banner,
            "dummyPool",
            new List<RecruitDummyPoolEntry>
            {
                new(),
            });
        return banner;
    }

    private static RecruitPaymentRouteDefinition CreateRoute(
        ItemDefinitionSO item,
        long singleCost,
        int priority)
    {
        RecruitPaymentRouteDefinition route = new();
        SetPrivateField(route, "item", item);
        SetPrivateField(route, "singleCost", singleCost);
        SetPrivateField(route, "priority", priority);
        return route;
    }

    private static RecruitDummyPoolEntry CreateRewardEntry(
        RecruitRewardType rewardType,
        CharacterSO character,
        ItemDefinitionSO item,
        long itemAmount)
    {
        RecruitDummyPoolEntry entry = new();
        SetPrivateField(entry, "rewardType", rewardType);
        SetPrivateField(entry, "character", character);
        SetPrivateField(entry, "item", item);
        SetPrivateField(entry, "itemAmount", itemAmount);
        return entry;
    }

    private static RecruitDummyPoolEntry CreateDummyReward(
        string displayName,
        CharacterGrade grade,
        float rate)
    {
        RecruitDummyPoolEntry entry = new();
        SetPrivateField(entry, "displayName", displayName);
        SetPrivateField(entry, "grade", grade);
        SetPrivateField(entry, "rate", rate);
        return entry;
    }

    private static int FindGradePool(
        IReadOnlyList<RecruitGradePoolDefinition> pools,
        CharacterGrade grade)
    {
        for (int index = 0; index < pools.Count; index++)
        {
            if (pools[index] != null &&
                pools[index].Grade == grade)
            {
                return index;
            }
        }
        Assert.Fail($"{(int)grade}등급 풀을 찾지 못했습니다.");
        return -1;
    }

    private static InventoryData CreateInventory(
        params (string itemId, long amount)[] entries)
    {
        InventoryData inventory = new();
        string json = "{\"version\":1,\"entries\":[";
        for (int index = 0; index < entries.Length; index++)
        {
            if (index > 0)
                json += ",";
            json +=
                $"{{\"itemId\":\"{entries[index].itemId}\"," +
                $"\"amount\":{entries[index].amount}}}";
        }
        json += "]}";
        inventory.ImportJson(json);
        return inventory;
    }

    private static void ConfigureItem(
        ItemDefinitionSO item,
        string itemId)
    {
        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }

    private static void RestorePlayerPrefs(
        string key,
        bool hadValue,
        string previousValue)
    {
        if (hadValue)
            PlayerPrefs.SetString(key, previousValue);
        else
            PlayerPrefs.DeleteKey(key);
    }
}
